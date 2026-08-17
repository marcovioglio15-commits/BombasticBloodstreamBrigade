using Unity.Mathematics;

/// <summary>
/// Evolves and resolves the camera-only feedback emitted when a returning projectile starts its inbound trajectory.
/// It reuses the player's firing-shake profile while keeping trauma, strength, and impulse direction independent.
/// </summary>
internal static class PlayerReturnCameraShakeRuntimeUtility
{
    #region Constants
    private const float MinimumDurationSeconds = 0.0001f;
    private const float NoiseSeedX = 307.73f;
    private const float NoiseSeedY = 337.91f;
    private const float NoiseSeedZ = 367.49f;
    private const float NoiseSeedRoll = 397.27f;
    #endregion

    #region Methods

    #region Update
    /// <summary>
    /// Consumes the strongest pending return request, advances its envelope, and adds its scaled firing-shake output.
    /// </summary>
    /// <param name="state">Mutable player camera state receiving the independent return channel and frame output.</param>
    /// <param name="config">Resolved firing-shake profile reused for motion, amplitudes, axes, falloff, and zoom.</param>
    /// <param name="deltaTime">Presentation delta time used to decay the return trauma envelope.</param>
    /// <param name="noiseTime">Monotonic clock used for continuous motion and per-pulse impulse direction.</param>
    /// <param name="cameraRight">Camera world right axis used by enabled planar output.</param>
    /// <param name="cameraUp">Camera world up axis used by enabled planar output.</param>
    /// <param name="cameraForward">Camera world forward axis used by enabled depth output.</param>
    public static void UpdateState(ref PlayerCameraShakeState state,
                                   in CameraFireShakeBlob config,
                                   float deltaTime,
                                   float noiseTime,
                                   float3 cameraRight,
                                   float3 cameraUp,
                                   float3 cameraForward)
    {
        // Consume and advance the independent return channel before resolving its frame contribution.
        EvolveTrauma(ref state, in config, deltaTime, noiseTime);

        // Keep the published magnitude clean once the envelope has finished or the profile is disabled.
        if (state.ReturnCameraShakeTrauma <= 0f)
        {
            state.ReturnCameraShakeMagnitude = 0f;
            return;
        }

        // Reuse the firing profile with separate noise state so firing and returning pulses never synchronize.
        PlayerCameraShakeRuntimeUtility.ResolveFireChannelOutput(state.ReturnCameraShakeTrauma,
                                                                 in config,
                                                                 state.ReturnCameraShakeImpulseDirection,
                                                                 state.ReturnCameraShakeImpulseRollSign,
                                                                 noiseTime,
                                                                 cameraRight,
                                                                 cameraUp,
                                                                 cameraForward,
                                                                 out float magnitude,
                                                                 out float3 positionOffset,
                                                                 out float rollRadians,
                                                                 out float fovDelta);

        // Apply the module multiplier only to this channel, then layer it onto the already-resolved frame output.
        float multiplier = math.max(0f, state.ReturnCameraShakeMultiplier);
        state.ReturnCameraShakeMagnitude = magnitude * multiplier;
        state.PositionOffset += positionOffset * multiplier;
        state.RollRadians += rollRadians * multiplier;
        state.FovDelta += fovDelta * multiplier;
    }
    #endregion

    #region Reset
    /// <summary>
    /// Clears every camera-only return value when state initializes or transient projectiles are removed out of band.
    /// </summary>
    /// <param name="state">Mutable player camera state whose return camera channel must be cleared.</param>
    public static void Clear(ref PlayerCameraShakeState state)
    {
        state.ReturnCameraShakeRequestMultiplier = 0f;
        state.ReturnCameraShakeTrauma = 0f;
        state.ReturnCameraShakeMultiplier = 0f;
        state.ReturnCameraShakeImpulseDirection = float3.zero;
        state.ReturnCameraShakeImpulseRollSign = 0f;
        state.ReturnCameraShakeMagnitude = 0f;
    }
    #endregion

    #region Evolution
    /// <summary>
    /// Coalesces simultaneous requests, samples SingleImpulse direction once, and decays the independent envelope.
    /// </summary>
    /// <param name="state">Mutable player state carrying pending and active return camera feedback.</param>
    /// <param name="config">Firing-shake profile providing enable, duration, falloff, and motion mode.</param>
    /// <param name="deltaTime">Presentation delta time used for linear trauma decay.</param>
    /// <param name="noiseTime">Monotonic clock used to choose a stable impulse direction on each accepted pulse.</param>
    private static void EvolveTrauma(ref PlayerCameraShakeState state,
                                     in CameraFireShakeBlob config,
                                     float deltaTime,
                                     float noiseTime)
    {
        // Consume the strongest request once even if camera shake is unavailable for the current firing profile.
        float requestedMultiplier = math.max(0f, state.ReturnCameraShakeRequestMultiplier);
        state.ReturnCameraShakeRequestMultiplier = 0f;

        // A disabled firing profile cannot provide a camera-shake shape, so discard any residual return envelope.
        if (config.Enabled == 0)
        {
            Clear(ref state);
            return;
        }

        // A new pulse restarts the authored envelope while retaining the stronger of new and still-visible feedback.
        if (requestedMultiplier > 0f)
        {
            float currentStrength = PlayerCameraShakeRuntimeUtility.ResolveEnvelope(config.Falloff,
                                                                                     state.ReturnCameraShakeTrauma) *
                                    math.max(0f, state.ReturnCameraShakeMultiplier);
            state.ReturnCameraShakeTrauma = 1f;
            state.ReturnCameraShakeMultiplier = math.max(currentStrength, requestedMultiplier);
            PlayerCameraShakeRuntimeUtility.SampleImpulseDirection(noiseTime,
                                                                    NoiseSeedX,
                                                                    NoiseSeedY,
                                                                    NoiseSeedZ,
                                                                    NoiseSeedRoll,
                                                                    out state.ReturnCameraShakeImpulseDirection,
                                                                    out state.ReturnCameraShakeImpulseRollSign);
        }

        // Advance the authored firing envelope without allowing negative or zero-duration instability.
        float decayDuration = math.max(MinimumDurationSeconds, config.DurationSeconds);
        state.ReturnCameraShakeTrauma = math.max(0f,
                                                 state.ReturnCameraShakeTrauma -
                                                 math.max(0f, deltaTime) / decayDuration);

        // Release retained strength as soon as its envelope reaches rest.
        if (state.ReturnCameraShakeTrauma <= 0f)
            state.ReturnCameraShakeMultiplier = 0f;
    }
    #endregion

    #endregion
}
