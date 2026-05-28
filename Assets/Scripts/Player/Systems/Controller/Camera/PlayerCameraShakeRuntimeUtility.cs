using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes the trauma model, Perlin-noise sampling and transform layering used by the damage-driven camera
/// shake. Keeping the math here lets <see cref="PlayerCameraFollowSystem"/> own the single per-frame trauma update
/// while both player camera systems apply the resulting offset/roll through the same feedback-safe helpers.
/// </summary>
internal static class PlayerCameraShakeRuntimeUtility
{
    #region Constants
    // Trauma model and detection tuning kept defensive so authored or scaled values can never break the math.
    private const float MinimumDurationSeconds = 0.0001f;
    private const float MinimumDamageForFullStrength = 0.0001f;
    private const float DamageDeadlineEpsilon = 0.0001f;
    private const float RollEpsilon = 0.00001f;

    // Decorrelating offsets along the noise field so the X, Y and roll channels never move in lockstep.
    private const float NoiseSeedX = 0f;
    private const float NoiseSeedY = 23.17f;
    private const float NoiseSeedRoll = 51.73f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Advances the shake trauma once per frame: detects a fresh accepted hit, accumulates trauma, decays it and
    /// resolves the world-space offset and roll for this frame. The previous frame's applied output is carried into
    /// the previous-applied slots first so the camera systems can remove it before re-applying, avoiding feedback.
    /// </summary>
    /// <param name="state">Mutable shake state owned by the player entity.</param>
    /// <param name="config">Resolved runtime shake config baked from the preset and Add Scaling.</param>
    /// <param name="currentDamageDeadline">Current PlayerDamageGraceState deadline used to detect a new valid hit.</param>
    /// <param name="currentSurvivability">Current health-plus-shield total used to size damage-scaled trauma.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    /// <param name="noiseTime">Monotonic clock (seconds) used to sample the noise field.</param>
    /// <param name="cameraRight">Camera world right axis used to place the offset in the screen plane.</param>
    /// <param name="cameraUp">Camera world up axis used to place the offset in the screen plane.</param>
    public static void UpdateState(ref PlayerCameraShakeState state,
                                   in CameraShakeBlob config,
                                   float currentDamageDeadline,
                                   float currentSurvivability,
                                   float deltaTime,
                                   float noiseTime,
                                   float3 cameraRight,
                                   float3 cameraUp)
    {
        // Carry this frame's applied output into the previous-frame slots before recomputing.
        state.PreviousAppliedPositionOffset = state.PositionOffset;
        state.PreviousAppliedRollRadians = state.RollRadians;

        // Seed the damage-detection baselines on the first observed frame so spawning never shakes the camera.
        if (state.Initialized == 0)
        {
            state.Initialized = 1;
            state.LastDamageDeadline = currentDamageDeadline;
            state.LastSurvivability = currentSurvivability;
            state.Trauma = 0f;
            state.PositionOffset = float3.zero;
            state.RollRadians = 0f;
            return;
        }

        bool shakeEnabled = config.Enabled != 0;

        // A fresh accepted hit advances the grace deadline; add trauma sized by the configured policy.
        if (shakeEnabled && currentDamageDeadline > state.LastDamageDeadline + DamageDeadlineEpsilon)
        {
            float survivabilityDrop = math.max(0f, state.LastSurvivability - currentSurvivability);
            state.Trauma = math.saturate(state.Trauma + ResolveAddedTrauma(in config, survivabilityDrop));
        }

        // Track the latest baselines every frame so heals, regen and re-enabling never read as damage.
        state.LastDamageDeadline = currentDamageDeadline;
        state.LastSurvivability = currentSurvivability;

        // Decay trauma linearly so a full-strength impulse fades over the configured duration.
        float decayDuration = math.max(MinimumDurationSeconds, config.DurationSeconds);
        state.Trauma = math.max(0f, state.Trauma - math.max(0f, deltaTime) / decayDuration);

        // No active shake leaves a zeroed output so the camera systems apply nothing this frame.
        if (!shakeEnabled || state.Trauma <= 0f)
        {
            state.PositionOffset = float3.zero;
            state.RollRadians = 0f;
            return;
        }

        // Map the remaining trauma through the chosen envelope and modulate the noise sample by it.
        float magnitude = ResolveEnvelope(config.Falloff, state.Trauma);
        float noisePhase = noiseTime * math.max(0f, config.Frequency);
        float positionalAmplitude = math.max(0f, config.PositionalAmplitude) * magnitude;
        float3 planarOffset = cameraRight * (SampleSignedNoise(noisePhase, NoiseSeedX) * positionalAmplitude)
                            + cameraUp * (SampleSignedNoise(noisePhase, NoiseSeedY) * positionalAmplitude);
        state.PositionOffset = planarOffset;
        state.RollRadians = math.radians(math.max(0f, config.RotationalAmplitude) * magnitude * SampleSignedNoise(noisePhase, NoiseSeedRoll));
    }

    /// <summary>
    /// Recovers the un-shaken camera position by removing the offset applied last frame, so the follow spring
    /// smooths against the real target instead of chasing its own shake displacement.
    /// </summary>
    /// <param name="cameraPosition">Current camera world position (still carrying last frame's shake offset).</param>
    /// <param name="state">Shake state holding the previously applied offset.</param>
    /// <returns>The smoothing source position with the previous shake offset removed.</returns>
    public static float3 ResolveSmoothingSource(float3 cameraPosition, in PlayerCameraShakeState state)
    {
        return cameraPosition - state.PreviousAppliedPositionOffset;
    }

    /// <summary>
    /// Writes the camera transform from a resolved base position, layering this frame's shake offset and roll.
    /// Position always adds the offset. Rotation is only touched when a roll is active this frame, was active last
    /// frame (so it can be cleared) or the caller fully owns the base rotation, keeping non-shaking frames untouched.
    /// </summary>
    /// <param name="cameraTransform">Camera transform to write.</param>
    /// <param name="basePosition">Un-shaken base position for this frame (direct or spring-smoothed).</param>
    /// <param name="state">Shake state providing this frame's and last frame's applied output.</param>
    /// <param name="overrideBaseRotation">True when the caller supplies an authoritative base rotation (child-of-player).</param>
    /// <param name="baseRotationOverride">Authoritative base rotation used when <paramref name="overrideBaseRotation"/> is true.</param>
    public static void ApplyToCamera(Transform cameraTransform,
                                     float3 basePosition,
                                     in PlayerCameraShakeState state,
                                     bool overrideBaseRotation,
                                     quaternion baseRotationOverride)
    {
        cameraTransform.position = basePosition + state.PositionOffset;

        // Skip rotation writes entirely on idle frames so the shake never fights externally owned camera rotation.
        bool hasRoll = math.abs(state.RollRadians) > RollEpsilon || math.abs(state.PreviousAppliedRollRadians) > RollEpsilon;

        if (!overrideBaseRotation && !hasRoll)
            return;

        // Recover the base rotation by removing last frame's roll, then layer the current roll about the view axis.
        quaternion baseRotation = overrideBaseRotation
            ? baseRotationOverride
            : math.mul(cameraTransform.rotation, math.inverse(quaternion.RotateZ(state.PreviousAppliedRollRadians)));
        cameraTransform.rotation = math.mul(baseRotation, quaternion.RotateZ(state.RollRadians));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the trauma added by one accepted hit, optionally scaled by how much survivability it removed.
    /// </summary>
    /// <param name="config">Resolved runtime shake config.</param>
    /// <param name="survivabilityDrop">Health-plus-shield amount removed by the hit.</param>
    /// <returns>Trauma to add in the [0..1] range.</returns>
    private static float ResolveAddedTrauma(in CameraShakeBlob config, float survivabilityDrop)
    {
        if (config.ScaleWithDamage == 0)
            return 1f;

        float damageForFullStrength = math.max(MinimumDamageForFullStrength, config.DamageForFullStrength);
        return math.saturate(survivabilityDrop / damageForFullStrength);
    }

    /// <summary>
    /// Maps remaining trauma to a shake magnitude through the selected decay envelope.
    /// </summary>
    /// <param name="falloff">Envelope shape selected on the preset.</param>
    /// <param name="trauma">Current trauma in the [0..1] range.</param>
    /// <returns>Shake magnitude in the [0..1] range.</returns>
    private static float ResolveEnvelope(CameraShakeFalloff falloff, float trauma)
    {
        float clampedTrauma = math.saturate(trauma);

        switch (falloff)
        {
            case CameraShakeFalloff.Smooth:
                return math.smoothstep(0f, 1f, clampedTrauma);
            case CameraShakeFalloff.Quadratic:
                return clampedTrauma * clampedTrauma;
            default:
                return clampedTrauma;
        }
    }

    /// <summary>
    /// Samples the classic Perlin field along one decorrelated channel, returning a signed value around [-1..1].
    /// </summary>
    /// <param name="samplePhase">Phase coordinate advanced by time and frequency.</param>
    /// <param name="channelSeed">Channel offset that decorrelates this axis from the others.</param>
    /// <returns>Signed noise sample used to drive one shake axis.</returns>
    private static float SampleSignedNoise(float samplePhase, float channelSeed)
    {
        return noise.cnoise(new float2(samplePhase, channelSeed));
    }
    #endregion

    #endregion
}
