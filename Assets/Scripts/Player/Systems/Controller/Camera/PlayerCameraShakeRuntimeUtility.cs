using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes the trauma model, multi-axis sampling, FOV-zoom output and transform layering used by the damage-driven
/// and fire-driven camera shakes. <see cref="PlayerCameraFollowSystem"/> owns the single per-frame trauma update for
/// both channels; every camera system applies the resulting offset/roll/FOV through the same feedback-safe helpers.
/// The two channels evolve independent trauma envelopes and their outputs are summed into the final per-frame transform
/// write so a fire-burst never cancels a hit recoil and vice-versa. Two motion modes are supported per channel:
/// Continuous samples a decorrelated perlin field so the shake oscillates, SingleImpulse picks a stable direction at
/// trauma onset so each hit reads as a clean tactile jolt instead of a vibration.
/// </summary>
internal static class PlayerCameraShakeRuntimeUtility
{
    #region Constants
    // Trauma model and detection tuning kept defensive so authored or scaled values can never break the math.
    private const float MinimumDurationSeconds = 0.0001f;
    private const float MinimumDamageForFullStrength = 0.0001f;
    private const float DamageDeadlineEpsilon = 0.0001f;
    private const float RollEpsilon = 0.00001f;
    private const float FovEpsilon = 0.00001f;

    // Decorrelating offsets along the noise field so the X, Y, Z and roll channels never move in lockstep.
    private const float DamageNoiseSeedX = 0f;
    private const float DamageNoiseSeedY = 23.17f;
    private const float DamageNoiseSeedZ = 41.91f;
    private const float DamageNoiseSeedRoll = 51.73f;

    // Fire channel uses different noise seeds so the two shakes never align into a doubled spike at the same phase.
    private const float FireNoiseSeedX = 71.91f;
    private const float FireNoiseSeedY = 113.27f;
    private const float FireNoiseSeedZ = 137.43f;
    private const float FireNoiseSeedRoll = 167.83f;

    // Impact Frame uses a third decorrelated field and a stable authored impulse direction because its envelope is
    // already owned by the Impact Frame timeline rather than by the trauma state.
    private const float ImpactFrameNoiseSeedX = 191.17f;
    private const float ImpactFrameNoiseSeedY = 223.49f;
    private const float ImpactFrameNoiseSeedZ = 251.83f;
    private const float ImpactFrameNoiseSeedRoll = 277.31f;

    // SingleImpulse path picks one direction per accepted pulse from a small palette mapped through a phase-derived
    // hash. Keeping the palette discrete (sign in {-1, 0, 1}) yields a clean jolt without drifting.
    private const float ImpulseDirectionHashPhase = 0.49283f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Advances both shake channels once per frame: detects a fresh accepted hit on the damage channel and consumes
    /// any pending fire pulse on the fire channel, evolves the two independent trauma envelopes, then resolves the
    /// summed world-space offset, roll and FOV delta for this frame. The previous frame's applied output is carried
    /// into the previous-applied slots first so the camera systems can remove it before re-applying, avoiding feedback.
    /// </summary>
    /// <param name="state">Mutable shake state owned by the player entity.</param>
    /// <param name="damageConfig">Resolved runtime damage shake config baked from the preset and Add Scaling.</param>
    /// <param name="fireConfig">Resolved runtime fire shake config baked from the preset and Add Scaling.</param>
    /// <param name="currentDamageDeadline">Current PlayerDamageGraceState deadline used to detect a new valid hit.</param>
    /// <param name="currentSurvivability">Current health-plus-shield total used to size damage-scaled trauma.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    /// <param name="noiseTime">Monotonic clock (seconds) used to sample the noise field.</param>
    /// <param name="cameraRight">Camera world right axis used to place the offset along the planar Right.</param>
    /// <param name="cameraUp">Camera world up axis used to place the offset along the planar Up.</param>
    /// <param name="cameraForward">Camera world forward axis used to place the forward (depth) offset.</param>
    public static void UpdateState(ref PlayerCameraShakeState state,
                                   in CameraShakeBlob damageConfig,
                                   in CameraFireShakeBlob fireConfig,
                                   float currentDamageDeadline,
                                   float currentSurvivability,
                                   float deltaTime,
                                   float noiseTime,
                                   float3 cameraRight,
                                   float3 cameraUp,
                                   float3 cameraForward)
    {
        // Carry this frame's applied output into the previous-frame slots before recomputing.
        state.PreviousAppliedPositionOffset = state.PositionOffset;
        state.PreviousAppliedRollRadians = state.RollRadians;
        state.PreviousAppliedFovDelta = state.FovDelta;

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
            state.FovDelta = 0f;
            state.DamageImpulseDirection = float3.zero;
            state.DamageImpulseRollSign = 0f;
            state.DamageRumbleImpulseRemainingSeconds = 0f;
            state.FireImpulseDirection = float3.zero;
            state.FireImpulseRollSign = 0f;
            state.FireRumbleImpulseRemainingSeconds = 0f;
            return;
        }

        EvolveDamageTrauma(ref state, in damageConfig, currentDamageDeadline, currentSurvivability, deltaTime, noiseTime);
        EvolveFireTrauma(ref state, in fireConfig, deltaTime, noiseTime);

        // Resolve each channel through its own noise seeds (Continuous) or stable impulse direction (SingleImpulse).
        ResolveDamageChannelOutput(state.Trauma,
                                   in damageConfig,
                                   state.DamageImpulseDirection,
                                   state.DamageImpulseRollSign,
                                   noiseTime,
                                   cameraRight,
                                   cameraUp,
                                   cameraForward,
                                   out float damageMagnitude,
                                   out float3 damageOffset,
                                   out float damageRoll,
                                   out float damageFov);

        ResolveFireChannelOutput(state.FireTrauma,
                                 in fireConfig,
                                 state.FireImpulseDirection,
                                 state.FireImpulseRollSign,
                                 noiseTime,
                                 cameraRight,
                                 cameraUp,
                                 cameraForward,
                                 out float fireMagnitude,
                                 out float3 fireOffset,
                                 out float fireRoll,
                                 out float fireFov);

        state.ShakeMagnitude = damageMagnitude;
        state.FireShakeMagnitude = fireMagnitude;
        state.PositionOffset = damageOffset + fireOffset;
        state.RollRadians = damageRoll + fireRoll;
        state.FovDelta = damageFov + fireFov;
    }

    /// <summary>
    /// Layers one Impact Frame camera profile onto the damage and fire outputs already resolved for this frame.
    /// </summary>
    /// <param name="state">Mutable camera shake state receiving additive position, roll, and FOV output.</param>
    /// <param name="config">Impact Frame camera feedback profile.</param>
    /// <param name="blend">Current Impact Frame timeline or build-in blend.</param>
    /// <param name="noiseTime">Monotonic clock used to sample continuous camera motion.</param>
    /// <param name="cameraRight">Camera world right axis.</param>
    /// <param name="cameraUp">Camera world up axis.</param>
    /// <param name="cameraForward">Camera world forward axis.</param>
    public static void AddImpactFrameOutput(ref PlayerCameraShakeState state,
                                            in ImpactFrameCameraFeedbackConfig config,
                                            float blend,
                                            float noiseTime,
                                            float3 cameraRight,
                                            float3 cameraUp,
                                            float3 cameraForward)
    {
        float magnitude = math.saturate(blend);

        if (config.Enabled == 0 || magnitude <= 0f)
            return;

        ResolveAxisSamples(config.MotionMode,
                           noiseTime * math.max(0f, config.Frequency),
                           ImpactFrameNoiseSeedX,
                           ImpactFrameNoiseSeedY,
                           ImpactFrameNoiseSeedZ,
                           ImpactFrameNoiseSeedRoll,
                           new float3(1f, -1f, 1f),
                           1f,
                           out float sampleRight,
                           out float sampleUp,
                           out float sampleForward,
                           out float sampleRoll);

        float planarAmplitude = math.max(0f, config.PositionalAmplitude) * magnitude;
        float depthAmplitude = math.max(0f, config.ForwardAmplitude) * magnitude;

        if (config.AxisRightEnabled != 0)
            state.PositionOffset += cameraRight * (sampleRight * planarAmplitude);

        if (config.AxisUpEnabled != 0)
            state.PositionOffset += cameraUp * (sampleUp * planarAmplitude);

        if (config.AxisForwardEnabled != 0)
            state.PositionOffset += cameraForward * (sampleForward * depthAmplitude);

        state.RollRadians += math.radians(math.max(0f, config.RotationalAmplitude) * magnitude * sampleRoll);

        if (config.ZoomEnabled != 0)
            state.FovDelta += config.ZoomFovDelta * magnitude;
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

    /// <summary>
    /// Applies this frame's resolved FOV delta to the gameplay camera. The previously applied delta is removed first so
    /// the un-shaken authored FOV is restored before re-layering, exactly like the position/roll feedback-safe path.
    /// Skips the write entirely on idle frames so an idle player never pays the cost of touching the Camera field.
    /// </summary>
    /// <param name="camera">Gameplay camera to write.</param>
    /// <param name="state">Shake state providing this frame's and last frame's FOV delta.</param>
    public static void ApplyFovToCamera(Camera camera, in PlayerCameraShakeState state)
    {
        if (camera == null)
            return;

        bool hasFov = math.abs(state.FovDelta) > FovEpsilon || math.abs(state.PreviousAppliedFovDelta) > FovEpsilon;

        if (!hasFov)
            return;

        // Recover the base FOV by removing last frame's delta, then re-layer this frame's delta.
        float baseFov = camera.fieldOfView - state.PreviousAppliedFovDelta;
        camera.fieldOfView = math.max(MinimumDurationSeconds, baseFov + state.FovDelta);
    }
    #endregion

    #region Private Methods - Trauma Evolution
    /// <summary>
    /// Evolves the damage-shake trauma channel: detects a fresh accepted hit and adds the configured trauma, refreshes
    /// the damage-detection baselines and decays the remaining trauma linearly over the configured duration. On a fresh
    /// hit the SingleImpulse impulse direction (per axis and roll) is sampled once so the impulse path reads as one
    /// stable jolt for the lifetime of the trauma envelope. Disabled shakes still decay any leftover trauma so toggling
    /// the channel off cannot leave a residual envelope active.
    /// </summary>
    /// <param name="state">Mutable shake state holding the damage channel trauma and baselines.</param>
    /// <param name="config">Resolved runtime damage shake config.</param>
    /// <param name="currentDamageDeadline">Current PlayerDamageGraceState deadline used to detect a new valid hit.</param>
    /// <param name="currentSurvivability">Current health-plus-shield total used to size damage-scaled trauma.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    /// <param name="noiseTime">Monotonic clock used to derive a stable per-pulse impulse direction.</param>
    private static void EvolveDamageTrauma(ref PlayerCameraShakeState state,
                                            in CameraShakeBlob config,
                                            float currentDamageDeadline,
                                            float currentSurvivability,
                                            float deltaTime,
                                            float noiseTime)
    {
        bool shakeEnabled = config.Enabled != 0;
        bool freshHit = currentDamageDeadline > state.LastDamageDeadline + DamageDeadlineEpsilon;
        float clampedDelta = math.max(0f, deltaTime);

        // A fresh accepted hit advances the grace deadline; add trauma sized by the configured policy and seed
        // the impulse direction so SingleImpulse keeps a stable push for the lifetime of the envelope.
        if (shakeEnabled && freshHit)
        {
            float survivabilityDrop = math.max(0f, state.LastSurvivability - currentSurvivability);
            state.Trauma = math.saturate(state.Trauma + ResolveAddedDamageTrauma(in config, survivabilityDrop));
            SampleImpulseDirection(noiseTime,
                                   DamageNoiseSeedX,
                                   DamageNoiseSeedY,
                                   DamageNoiseSeedZ,
                                   DamageNoiseSeedRoll,
                                   out state.DamageImpulseDirection,
                                   out state.DamageImpulseRollSign);

            // Refresh the single-impulse rumble window so a fresh hit always lands a clean burst.
            if (config.RumbleEnabled != 0 && config.RumbleMotionMode == CameraShakeRumbleMotionMode.SingleImpulse)
                state.DamageRumbleImpulseRemainingSeconds = math.max(0f, config.RumbleImpulseDurationSeconds);
        }

        // Track the latest baselines every frame so heals, regen and re-enabling never read as damage.
        state.LastDamageDeadline = currentDamageDeadline;
        state.LastSurvivability = currentSurvivability;

        float decayDuration = math.max(MinimumDurationSeconds, config.DurationSeconds);
        state.Trauma = math.max(0f, state.Trauma - clampedDelta / decayDuration);
        state.DamageRumbleImpulseRemainingSeconds = math.max(0f, state.DamageRumbleImpulseRemainingSeconds - clampedDelta);
    }

    /// <summary>
    /// Evolves the fire-shake trauma channel: consumes any pending fire pulse and adds a unit of trauma, then decays
    /// the remaining trauma linearly over the configured duration. The pending flag is always cleared so a single
    /// frame with multiple producers still adds trauma exactly once, while rapid-fire shots stack into a sustained
    /// shake bounded by the [0..1] saturation. On a consumed pulse the impulse direction is resampled so SingleImpulse
    /// jolts read as one clear push instead of stacking through the noise field.
    /// </summary>
    /// <param name="state">Mutable shake state holding the fire channel trauma and the pending request flag.</param>
    /// <param name="config">Resolved runtime fire shake config.</param>
    /// <param name="deltaTime">Presentation delta time used to decay trauma.</param>
    /// <param name="noiseTime">Monotonic clock used to derive a stable per-pulse impulse direction.</param>
    private static void EvolveFireTrauma(ref PlayerCameraShakeState state,
                                          in CameraFireShakeBlob config,
                                          float deltaTime,
                                          float noiseTime)
    {
        bool shakeEnabled = config.Enabled != 0;
        bool pulsed = state.FireRequestPending != 0;
        state.FireRequestPending = 0;
        float clampedDelta = math.max(0f, deltaTime);

        if (shakeEnabled && pulsed)
        {
            state.FireTrauma = math.saturate(state.FireTrauma + 1f);
            SampleImpulseDirection(noiseTime,
                                   FireNoiseSeedX,
                                   FireNoiseSeedY,
                                   FireNoiseSeedZ,
                                   FireNoiseSeedRoll,
                                   out state.FireImpulseDirection,
                                   out state.FireImpulseRollSign);

            if (config.RumbleEnabled != 0 && config.RumbleMotionMode == CameraShakeRumbleMotionMode.SingleImpulse)
                state.FireRumbleImpulseRemainingSeconds = math.max(0f, config.RumbleImpulseDurationSeconds);
        }

        float decayDuration = math.max(MinimumDurationSeconds, config.DurationSeconds);
        state.FireTrauma = math.max(0f, state.FireTrauma - clampedDelta / decayDuration);
        state.FireRumbleImpulseRemainingSeconds = math.max(0f, state.FireRumbleImpulseRemainingSeconds - clampedDelta);
    }
    #endregion

    #region Private Methods - Channel Output
    /// <summary>
    /// Resolves the per-frame planar offset, forward push, roll and FOV delta produced by the damage shake channel.
    /// Returns zeroed output when the channel is disabled or its trauma has fully decayed so the camera systems can
    /// skip work on idle frames without extra branching.
    /// </summary>
    /// <param name="trauma">Current trauma remaining on the channel in the [0..1] range.</param>
    /// <param name="config">Resolved damage shake config providing falloff, amplitudes, frequency and zoom.</param>
    /// <param name="impulseDirection">Stable per-pulse impulse direction sampled on the last fresh hit.</param>
    /// <param name="impulseRollSign">Stable per-pulse roll sign sampled on the last fresh hit.</param>
    /// <param name="noiseTime">Monotonic clock used to sample the noise field for Continuous mode.</param>
    /// <param name="cameraRight">Camera world right axis used to place the planar Right offset.</param>
    /// <param name="cameraUp">Camera world up axis used to place the planar Up offset.</param>
    /// <param name="cameraForward">Camera world forward axis used to place the depth offset.</param>
    /// <param name="magnitude">Smooth envelope magnitude resolved from the trauma this frame.</param>
    /// <param name="positionOffset">Resolved position offset for the channel this frame.</param>
    /// <param name="rollRadians">Resolved view-axis roll in radians for the channel this frame.</param>
    /// <param name="fovDelta">Resolved FOV delta in degrees for the channel this frame.</param>
    private static void ResolveDamageChannelOutput(float trauma,
                                                    in CameraShakeBlob config,
                                                    float3 impulseDirection,
                                                    float impulseRollSign,
                                                    float noiseTime,
                                                    float3 cameraRight,
                                                    float3 cameraUp,
                                                    float3 cameraForward,
                                                    out float magnitude,
                                                    out float3 positionOffset,
                                                    out float rollRadians,
                                                    out float fovDelta)
    {
        if (config.Enabled == 0 || trauma <= 0f)
        {
            magnitude = 0f;
            positionOffset = float3.zero;
            rollRadians = 0f;
            fovDelta = 0f;
            return;
        }

        magnitude = ResolveEnvelope(config.Falloff, trauma);
        ResolveAxisSamples(config.MotionMode,
                           noiseTime * math.max(0f, config.Frequency),
                           DamageNoiseSeedX,
                           DamageNoiseSeedY,
                           DamageNoiseSeedZ,
                           DamageNoiseSeedRoll,
                           impulseDirection,
                           impulseRollSign,
                           out float sampleRight,
                           out float sampleUp,
                           out float sampleForward,
                           out float sampleRoll);

        float planarAmplitude = math.max(0f, config.PositionalAmplitude) * magnitude;
        float depthAmplitude = math.max(0f, config.ForwardAmplitude) * magnitude;
        float3 rightContribution = config.AxisRightEnabled != 0 ? cameraRight * (sampleRight * planarAmplitude) : float3.zero;
        float3 upContribution = config.AxisUpEnabled != 0 ? cameraUp * (sampleUp * planarAmplitude) : float3.zero;
        float3 forwardContribution = config.AxisForwardEnabled != 0 ? cameraForward * (sampleForward * depthAmplitude) : float3.zero;
        positionOffset = rightContribution + upContribution + forwardContribution;
        rollRadians = math.radians(math.max(0f, config.RotationalAmplitude) * magnitude * sampleRoll);
        fovDelta = config.ZoomEnabled != 0 ? config.ZoomFovDelta * magnitude : 0f;
    }

    /// <summary>
    /// Mirror of <see cref="ResolveDamageChannelOutput"/> for the fire shake config, which carries the same envelope,
    /// axis, zoom and motion-mode fields but lacks the damage-scaling block; sharing the math without coupling either
    /// blob to the other's authoring keeps both layers focused.
    /// </summary>
    /// <param name="trauma">Current fire trauma remaining on the channel in the [0..1] range.</param>
    /// <param name="config">Resolved fire shake config providing falloff, amplitudes, frequency and zoom.</param>
    /// <param name="impulseDirection">Stable per-pulse impulse direction sampled on the last consumed pulse.</param>
    /// <param name="impulseRollSign">Stable per-pulse roll sign sampled on the last consumed pulse.</param>
    /// <param name="noiseTime">Monotonic clock used to sample the noise field for Continuous mode.</param>
    /// <param name="cameraRight">Camera world right axis.</param>
    /// <param name="cameraUp">Camera world up axis.</param>
    /// <param name="cameraForward">Camera world forward axis.</param>
    /// <param name="magnitude">Smooth envelope magnitude resolved from the fire trauma this frame.</param>
    /// <param name="positionOffset">Resolved position offset for the fire channel this frame.</param>
    /// <param name="rollRadians">Resolved view-axis roll in radians for the fire channel this frame.</param>
    /// <param name="fovDelta">Resolved FOV delta in degrees for the fire channel this frame.</param>
    private static void ResolveFireChannelOutput(float trauma,
                                                  in CameraFireShakeBlob config,
                                                  float3 impulseDirection,
                                                  float impulseRollSign,
                                                  float noiseTime,
                                                  float3 cameraRight,
                                                  float3 cameraUp,
                                                  float3 cameraForward,
                                                  out float magnitude,
                                                  out float3 positionOffset,
                                                  out float rollRadians,
                                                  out float fovDelta)
    {
        if (config.Enabled == 0 || trauma <= 0f)
        {
            magnitude = 0f;
            positionOffset = float3.zero;
            rollRadians = 0f;
            fovDelta = 0f;
            return;
        }

        magnitude = ResolveEnvelope(config.Falloff, trauma);
        ResolveAxisSamples(config.MotionMode,
                           noiseTime * math.max(0f, config.Frequency),
                           FireNoiseSeedX,
                           FireNoiseSeedY,
                           FireNoiseSeedZ,
                           FireNoiseSeedRoll,
                           impulseDirection,
                           impulseRollSign,
                           out float sampleRight,
                           out float sampleUp,
                           out float sampleForward,
                           out float sampleRoll);

        float planarAmplitude = math.max(0f, config.PositionalAmplitude) * magnitude;
        float depthAmplitude = math.max(0f, config.ForwardAmplitude) * magnitude;
        float3 rightContribution = config.AxisRightEnabled != 0 ? cameraRight * (sampleRight * planarAmplitude) : float3.zero;
        float3 upContribution = config.AxisUpEnabled != 0 ? cameraUp * (sampleUp * planarAmplitude) : float3.zero;
        float3 forwardContribution = config.AxisForwardEnabled != 0 ? cameraForward * (sampleForward * depthAmplitude) : float3.zero;
        positionOffset = rightContribution + upContribution + forwardContribution;
        rollRadians = math.radians(math.max(0f, config.RotationalAmplitude) * magnitude * sampleRoll);
        fovDelta = config.ZoomEnabled != 0 ? config.ZoomFovDelta * magnitude : 0f;
    }

    /// <summary>
    /// Routes the per-axis sample resolution through the active motion mode. Continuous samples a decorrelated perlin
    /// field for each axis. SingleImpulse uses the stable per-pulse direction so the offset reads as a clean jolt for
    /// the lifetime of the trauma envelope (every fresh pulse resamples the direction in <see cref="EvolveDamageTrauma"/>
    /// or <see cref="EvolveFireTrauma"/>).
    /// </summary>
    /// <param name="motionMode">Active motion mode for the channel.</param>
    /// <param name="noisePhase">Phase advanced by time and frequency, used by Continuous.</param>
    /// <param name="seedX">Noise seed for the Right axis (Continuous).</param>
    /// <param name="seedY">Noise seed for the Up axis (Continuous).</param>
    /// <param name="seedZ">Noise seed for the Forward axis (Continuous).</param>
    /// <param name="seedRoll">Noise seed for the view-axis roll (Continuous).</param>
    /// <param name="impulseDirection">Stable per-pulse direction sign for each axis (SingleImpulse).</param>
    /// <param name="impulseRollSign">Stable per-pulse roll sign (SingleImpulse).</param>
    /// <param name="sampleRight">Resolved sample in [-1..1] for the Right axis.</param>
    /// <param name="sampleUp">Resolved sample in [-1..1] for the Up axis.</param>
    /// <param name="sampleForward">Resolved sample in [-1..1] for the Forward axis.</param>
    /// <param name="sampleRoll">Resolved sample in [-1..1] for the view-axis roll.</param>
    private static void ResolveAxisSamples(CameraShakeMotionMode motionMode,
                                            float noisePhase,
                                            float seedX,
                                            float seedY,
                                            float seedZ,
                                            float seedRoll,
                                            float3 impulseDirection,
                                            float impulseRollSign,
                                            out float sampleRight,
                                            out float sampleUp,
                                            out float sampleForward,
                                            out float sampleRoll)
    {
        switch (motionMode)
        {
            case CameraShakeMotionMode.SingleImpulse:
                sampleRight = impulseDirection.x;
                sampleUp = impulseDirection.y;
                sampleForward = impulseDirection.z;
                sampleRoll = impulseRollSign;
                return;
            default:
                sampleRight = SampleSignedNoise(noisePhase, seedX);
                sampleUp = SampleSignedNoise(noisePhase, seedY);
                sampleForward = SampleSignedNoise(noisePhase, seedZ);
                sampleRoll = SampleSignedNoise(noisePhase, seedRoll);
                return;
        }
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

    /// <summary>
    /// Samples one stable impulse direction per pulse from a small palette of signs. The direction stays constant for
    /// the lifetime of the trauma envelope so the shake reads as a clear push along an axis instead of an oscillation,
    /// then gets replaced on the next fresh pulse so the next jolt picks a fresh direction.
    /// </summary>
    /// <param name="noiseTime">Monotonic clock used to derive a stable per-pulse hash.</param>
    /// <param name="seedX">Channel seed for the Right axis hash.</param>
    /// <param name="seedY">Channel seed for the Up axis hash.</param>
    /// <param name="seedZ">Channel seed for the Forward axis hash.</param>
    /// <param name="seedRoll">Channel seed for the roll hash.</param>
    /// <param name="direction">Resolved sign palette per axis in {-1, 0, 1} stored as float3.</param>
    /// <param name="rollSign">Resolved roll sign in {-1, 0, 1}.</param>
    private static void SampleImpulseDirection(float noiseTime,
                                                float seedX,
                                                float seedY,
                                                float seedZ,
                                                float seedRoll,
                                                out float3 direction,
                                                out float rollSign)
    {
        float phase = noiseTime * ImpulseDirectionHashPhase;
        direction = new float3(ResolveImpulseSign(SampleSignedNoise(phase, seedX)),
                               ResolveImpulseSign(SampleSignedNoise(phase, seedY)),
                               ResolveImpulseSign(SampleSignedNoise(phase, seedZ)));
        rollSign = ResolveImpulseSign(SampleSignedNoise(phase, seedRoll));
    }

    /// <summary>
    /// Quantizes a signed noise sample into the {-1, 0, 1} palette used by the SingleImpulse path.
    /// </summary>
    /// <param name="signedNoiseSample">Raw signed noise sample.</param>
    /// <returns>Quantized impulse sign.</returns>
    private static float ResolveImpulseSign(float signedNoiseSample)
    {
        if (signedNoiseSample > 0.05f)
            return 1f;

        if (signedNoiseSample < -0.05f)
            return -1f;

        return 0f;
    }
    #endregion

    #endregion
}
