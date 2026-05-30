using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes the trauma model, Perlin-noise sampling and transform layering used by the damage-driven and
/// fire-driven camera shakes. Keeping the math here lets <see cref="PlayerCameraFollowSystem"/> own the single
/// per-frame trauma update for both channels while every camera system applies the resulting offset/roll through
/// the same feedback-safe helpers. The two channels evolve independent trauma envelopes and their outputs are
/// summed into the final per-frame transform write so a fire-burst never cancels a hit recoil and vice-versa.
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

    // Fire channel uses different noise seeds so the two shakes never align into a doubled spike at the same phase.
    private const float FireNoiseSeedX = 71.91f;
    private const float FireNoiseSeedY = 113.27f;
    private const float FireNoiseSeedRoll = 167.83f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Advances both shake channels once per frame: detects a fresh accepted hit on the damage channel and consumes
    /// any pending fire pulse on the fire channel, evolves the two independent trauma envelopes, then resolves the
    /// summed world-space offset and roll for this frame. The previous frame's applied output is carried into the
    /// previous-applied slots first so the camera systems can remove it before re-applying, avoiding feedback.
    /// </summary>
    /// <param name="state">Mutable shake state owned by the player entity.</param>
    /// <param name="damageConfig">Resolved runtime damage shake config baked from the preset and Add Scaling.</param>
    /// <param name="fireConfig">Resolved runtime fire shake config baked from the preset and Add Scaling.</param>
    /// <param name="currentDamageDeadline">Current PlayerDamageGraceState deadline used to detect a new valid hit.</param>
    /// <param name="currentSurvivability">Current health-plus-shield total used to size damage-scaled trauma.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    /// <param name="noiseTime">Monotonic clock (seconds) used to sample the noise field.</param>
    /// <param name="cameraRight">Camera world right axis used to place the offset in the screen plane.</param>
    /// <param name="cameraUp">Camera world up axis used to place the offset in the screen plane.</param>
    public static void UpdateState(ref PlayerCameraShakeState state,
                                   in CameraShakeBlob damageConfig,
                                   in CameraFireShakeBlob fireConfig,
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
            state.FireTrauma = 0f;
            state.FireRequestPending = 0;
            state.ShakeMagnitude = 0f;
            state.FireShakeMagnitude = 0f;
            state.PositionOffset = float3.zero;
            state.RollRadians = 0f;
            return;
        }

        EvolveDamageTrauma(ref state, in damageConfig, currentDamageDeadline, currentSurvivability, deltaTime);
        EvolveFireTrauma(ref state, in fireConfig, deltaTime);

        // Combine both channels into the final per-frame output. Each channel is sampled with its own noise seeds so
        // simultaneous shakes do not align into doubled spikes; the rumble magnitudes stay separated on the state.
        float3 damageOffset;
        float damageRoll;
        ResolveChannelOutput(state.Trauma,
                             in damageConfig,
                             noiseTime,
                             cameraRight,
                             cameraUp,
                             NoiseSeedX,
                             NoiseSeedY,
                             NoiseSeedRoll,
                             out float damageMagnitude,
                             out damageOffset,
                             out damageRoll);

        float3 fireOffset;
        float fireRoll;
        ResolveFireChannelOutput(state.FireTrauma,
                                 in fireConfig,
                                 noiseTime,
                                 cameraRight,
                                 cameraUp,
                                 out float fireMagnitude,
                                 out fireOffset,
                                 out fireRoll);

        state.ShakeMagnitude = damageMagnitude;
        state.FireShakeMagnitude = fireMagnitude;
        state.PositionOffset = damageOffset + fireOffset;
        state.RollRadians = damageRoll + fireRoll;
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
    /// Evolves the damage-shake trauma channel: detects a fresh accepted hit and adds the configured trauma, refreshes
    /// the damage-detection baselines and decays the remaining trauma linearly over the configured duration. Disabled
    /// shakes still decay any leftover trauma so toggling the channel off cannot leave a residual envelope active.
    /// </summary>
    /// <param name="state">Mutable shake state holding the damage channel trauma and baselines.</param>
    /// <param name="config">Resolved runtime damage shake config.</param>
    /// <param name="currentDamageDeadline">Current PlayerDamageGraceState deadline used to detect a new valid hit.</param>
    /// <param name="currentSurvivability">Current health-plus-shield total used to size damage-scaled trauma.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    private static void EvolveDamageTrauma(ref PlayerCameraShakeState state,
                                            in CameraShakeBlob config,
                                            float currentDamageDeadline,
                                            float currentSurvivability,
                                            float deltaTime)
    {
        bool shakeEnabled = config.Enabled != 0;

        // A fresh accepted hit advances the grace deadline; add trauma sized by the configured policy.
        if (shakeEnabled && currentDamageDeadline > state.LastDamageDeadline + DamageDeadlineEpsilon)
        {
            float survivabilityDrop = math.max(0f, state.LastSurvivability - currentSurvivability);
            state.Trauma = math.saturate(state.Trauma + ResolveAddedDamageTrauma(in config, survivabilityDrop));
        }

        // Track the latest baselines every frame so heals, regen and re-enabling never read as damage.
        state.LastDamageDeadline = currentDamageDeadline;
        state.LastSurvivability = currentSurvivability;

        float decayDuration = math.max(MinimumDurationSeconds, config.DurationSeconds);
        state.Trauma = math.max(0f, state.Trauma - math.max(0f, deltaTime) / decayDuration);
    }

    /// <summary>
    /// Evolves the fire-shake trauma channel: consumes any pending fire pulse and adds a unit of trauma, then decays
    /// the remaining trauma linearly over the configured duration. The pending flag is always cleared so a single
    /// frame with multiple producers still adds trauma exactly once, while rapid-fire shots stack into a sustained
    /// shake bounded by the [0..1] saturation.
    /// </summary>
    /// <param name="state">Mutable shake state holding the fire channel trauma and the pending request flag.</param>
    /// <param name="config">Resolved runtime fire shake config.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    private static void EvolveFireTrauma(ref PlayerCameraShakeState state,
                                          in CameraFireShakeBlob config,
                                          float deltaTime)
    {
        bool shakeEnabled = config.Enabled != 0;
        bool pulsed = state.FireRequestPending != 0;
        state.FireRequestPending = 0;

        if (shakeEnabled && pulsed)
            state.FireTrauma = math.saturate(state.FireTrauma + 1f);

        float decayDuration = math.max(MinimumDurationSeconds, config.DurationSeconds);
        state.FireTrauma = math.max(0f, state.FireTrauma - math.max(0f, deltaTime) / decayDuration);
    }

    /// <summary>
    /// Resolves the per-frame planar offset and roll produced by one shake channel from its trauma, frequency and
    /// amplitude. Returns zeroed output when the channel is disabled or its trauma has fully decayed so the camera
    /// systems can skip rotation writes on idle frames without extra branching.
    /// </summary>
    /// <param name="trauma">Current trauma remaining on the channel in the [0..1] range.</param>
    /// <param name="config">Resolved damage shake config providing falloff, amplitudes and frequency.</param>
    /// <param name="noiseTime">Monotonic clock (seconds) used to sample the noise field.</param>
    /// <param name="cameraRight">Camera world right axis used to place the offset in the screen plane.</param>
    /// <param name="cameraUp">Camera world up axis used to place the offset in the screen plane.</param>
    /// <param name="noiseSeedX">Decorrelated noise seed along the right axis for this channel.</param>
    /// <param name="noiseSeedY">Decorrelated noise seed along the up axis for this channel.</param>
    /// <param name="noiseSeedRoll">Decorrelated noise seed for the roll component of this channel.</param>
    /// <param name="magnitude">Smooth envelope magnitude resolved from the trauma this frame.</param>
    /// <param name="positionOffset">Resolved planar position offset for the channel this frame.</param>
    /// <param name="rollRadians">Resolved view-axis roll in radians for the channel this frame.</param>
    private static void ResolveChannelOutput(float trauma,
                                              in CameraShakeBlob config,
                                              float noiseTime,
                                              float3 cameraRight,
                                              float3 cameraUp,
                                              float noiseSeedX,
                                              float noiseSeedY,
                                              float noiseSeedRoll,
                                              out float magnitude,
                                              out float3 positionOffset,
                                              out float rollRadians)
    {
        if (config.Enabled == 0 || trauma <= 0f)
        {
            magnitude = 0f;
            positionOffset = float3.zero;
            rollRadians = 0f;
            return;
        }

        magnitude = ResolveEnvelope(config.Falloff, trauma);
        float noisePhase = noiseTime * math.max(0f, config.Frequency);
        float positionalAmplitude = math.max(0f, config.PositionalAmplitude) * magnitude;
        positionOffset = cameraRight * (SampleSignedNoise(noisePhase, noiseSeedX) * positionalAmplitude)
                       + cameraUp * (SampleSignedNoise(noisePhase, noiseSeedY) * positionalAmplitude);
        rollRadians = math.radians(math.max(0f, config.RotationalAmplitude) * magnitude * SampleSignedNoise(noisePhase, noiseSeedRoll));
    }

    /// <summary>
    /// Mirror of <see cref="ResolveChannelOutput"/> for the fire shake config, which carries the same envelope and
    /// noise parameters but lacks the damage-scaling block, so the two channels can share the math without coupling
    /// either struct to the other's authoring fields.
    /// </summary>
    /// <param name="trauma">Current fire trauma remaining on the channel in the [0..1] range.</param>
    /// <param name="config">Resolved fire shake config providing falloff, amplitudes and frequency.</param>
    /// <param name="noiseTime">Monotonic clock (seconds) used to sample the noise field.</param>
    /// <param name="cameraRight">Camera world right axis used to place the offset in the screen plane.</param>
    /// <param name="cameraUp">Camera world up axis used to place the offset in the screen plane.</param>
    /// <param name="magnitude">Smooth envelope magnitude resolved from the fire trauma this frame.</param>
    /// <param name="positionOffset">Resolved planar position offset for the fire channel this frame.</param>
    /// <param name="rollRadians">Resolved view-axis roll in radians for the fire channel this frame.</param>
    private static void ResolveFireChannelOutput(float trauma,
                                                  in CameraFireShakeBlob config,
                                                  float noiseTime,
                                                  float3 cameraRight,
                                                  float3 cameraUp,
                                                  out float magnitude,
                                                  out float3 positionOffset,
                                                  out float rollRadians)
    {
        if (config.Enabled == 0 || trauma <= 0f)
        {
            magnitude = 0f;
            positionOffset = float3.zero;
            rollRadians = 0f;
            return;
        }

        magnitude = ResolveEnvelope(config.Falloff, trauma);
        float noisePhase = noiseTime * math.max(0f, config.Frequency);
        float positionalAmplitude = math.max(0f, config.PositionalAmplitude) * magnitude;
        positionOffset = cameraRight * (SampleSignedNoise(noisePhase, FireNoiseSeedX) * positionalAmplitude)
                       + cameraUp * (SampleSignedNoise(noisePhase, FireNoiseSeedY) * positionalAmplitude);
        rollRadians = math.radians(math.max(0f, config.RotationalAmplitude) * magnitude * SampleSignedNoise(noisePhase, FireNoiseSeedRoll));
    }

    /// <summary>
    /// Resolves the trauma added by one accepted hit, optionally scaled by how much survivability it removed.
    /// </summary>
    /// <param name="config">Resolved runtime damage shake config.</param>
    /// <param name="survivabilityDrop">Health-plus-shield amount removed by the hit.</param>
    /// <returns>Trauma to add in the [0..1] range.</returns>
    private static float ResolveAddedDamageTrauma(in CameraShakeBlob config, float survivabilityDrop)
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
