using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

/// <summary>
/// Drives a connected gamepad's rumble from the damage-shake trauma already evolved by
/// <see cref="PlayerCameraFollowSystem"/> (the single trauma owner). It runs after the follow system so it reads the
/// smooth shake magnitude resolved this frame, scales the two motor amplitudes by it and writes them to the active
/// gamepad. The same pause/end-of-run gates as the camera systems force the motors to rest, and redundant motor
/// writes are skipped so the input backend is only touched when the haptic intensity actually changes.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerDamageShakeRumbleSystem : ISystem, ISystemStartStop
{
    #region Constants
    // Minimum motor-speed delta that justifies re-sending haptics, avoiding per-frame writes while the value is flat.
    private const float MotorSpeedEpsilon = 0.0025f;
    #endregion

    #region Fields
    private EntityQuery runOutcomeQuery;
    private float lastAppliedLowFrequency;
    private float lastAppliedHighFrequency;
    private int lastGamepadDeviceId;
    private byte hasAppliedMotorSpeeds;
    #endregion

    #region Lifecycle
    /// <summary>
    /// Requires the player shake state and runtime camera config, and caches the run-outcome query used to freeze the
    /// rumble during end-of-run flows exactly like the camera systems do.
    /// </summary>
    /// <param name="state">System state for the owning world.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCameraShakeState>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        runOutcomeQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                               ComponentType.ReadOnly<PlayerRunOutcomeState>());
    }

    /// <summary>
    /// Resolves the target motor speeds for the active player and applies them to the connected gamepad once per
    /// frame, silencing the rumble while gameplay is hard-paused or the run outcome is finalized.
    /// </summary>
    /// <param name="state">System state for the owning world.</param>
    public void OnUpdate(ref SystemState state)
    {
        bool isSceneTransitioning = GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning();
        float targetLowFrequency = 0f;
        float targetHighFrequency = 0f;

        // While not silenced, take the single player's shake magnitude and scale the configured motor amplitudes.
        if (!ShouldSilenceRumble(isSceneTransitioning))
        {
            foreach ((RefRO<PlayerCameraShakeState> shakeState, RefRO<PlayerRuntimeCameraConfig> cameraConfig)
                     in SystemAPI.Query<RefRO<PlayerCameraShakeState>, RefRO<PlayerRuntimeCameraConfig>>())
            {
                ResolveMotorSpeeds(in cameraConfig.ValueRO.Shake,
                                   shakeState.ValueRO.ShakeMagnitude,
                                   out targetLowFrequency,
                                   out targetHighFrequency);
                break;
            }
        }

        ApplyMotorSpeeds(targetLowFrequency, targetHighFrequency);
    }

    /// <summary>
    /// Resets motor tracking when the system starts running so a freshly spawned player re-sends a clean baseline.
    /// </summary>
    /// <param name="state">System state for the owning world.</param>
    public void OnStartRunning(ref SystemState state)
    {
        ResetMotorTracking();
    }

    /// <summary>
    /// Stops every connected gamepad's rumble when the system stops running (player despawn, scene unload) so a hit
    /// taken mid-shake can never leave a controller buzzing once the simulation no longer drives the motors.
    /// </summary>
    /// <param name="state">System state for the owning world.</param>
    public void OnStopRunning(ref SystemState state)
    {
        StopAllGamepadRumble();
    }

    /// <summary>
    /// Final teardown safety: clears rumble on world destruction so leaving play mode never strands a vibration.
    /// </summary>
    /// <param name="state">System state for the owning world.</param>
    public void OnDestroy(ref SystemState state)
    {
        StopAllGamepadRumble();
    }
    #endregion

    #region Methods

    #region Gating
    /// <summary>
    /// Resolves whether the rumble must rest this frame, mirroring the camera systems' pause and end-of-run guards.
    /// </summary>
    /// <param name="isSceneTransitioning">True while the scene manager is loading or fading between scenes.</param>
    /// <returns>True when the motors must be forced to rest for the current frame.</returns>
    private readonly bool ShouldSilenceRumble(bool isSceneTransitioning)
    {
        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery))
            return true;

        // A hard time-scale pause freezes gameplay, but a transition-owned pause must still settle the motors to rest.
        return PlayerGameplayPauseUtility.IsTimeScaleHardPaused() && !isSceneTransitioning;
    }
    #endregion

    #region Motor Resolution
    /// <summary>
    /// Resolves the two normalized motor speeds from the rumble config and the current shake envelope magnitude.
    /// Both motors share the shake's trauma envelope, so the vibration ramps down together with the on-screen kick.
    /// </summary>
    /// <param name="shake">Resolved runtime shake config carrying the rumble enable flag and motor amplitudes.</param>
    /// <param name="shakeMagnitude">Smooth envelope magnitude in the [0..1] range resolved by the shake utility.</param>
    /// <param name="lowFrequency">Resolved heavy (low-frequency) motor speed in the [0..1] range.</param>
    /// <param name="highFrequency">Resolved light (high-frequency) motor speed in the [0..1] range.</param>
    private static void ResolveMotorSpeeds(in CameraShakeBlob shake,
                                           float shakeMagnitude,
                                           out float lowFrequency,
                                           out float highFrequency)
    {
        float magnitude = math.saturate(shakeMagnitude);

        // No rumble requested or no trauma left this frame leaves both motors at rest.
        if (shake.RumbleEnabled == 0 || magnitude <= 0f)
        {
            lowFrequency = 0f;
            highFrequency = 0f;
            return;
        }

        lowFrequency = math.saturate(shake.RumbleLowFrequency) * magnitude;
        highFrequency = math.saturate(shake.RumbleHighFrequency) * magnitude;
    }
    #endregion

    #region Motor Output
    /// <summary>
    /// Writes the resolved motor speeds to the active gamepad, skipping redundant writes while the value is flat and
    /// re-sending unconditionally when the active gamepad changes so a newly selected pad receives the current value.
    /// </summary>
    /// <param name="lowFrequency">Heavy (low-frequency) motor speed to apply in the [0..1] range.</param>
    /// <param name="highFrequency">Light (high-frequency) motor speed to apply in the [0..1] range.</param>
    private void ApplyMotorSpeeds(float lowFrequency, float highFrequency)
    {
        Gamepad gamepad = Gamepad.current;

        // No connected gamepad: nothing to drive. Invalidate tracking so a reconnecting pad gets a fresh write.
        if (gamepad == null)
        {
            hasAppliedMotorSpeeds = 0;
            return;
        }

        bool deviceChanged = gamepad.deviceId != lastGamepadDeviceId;
        lastGamepadDeviceId = gamepad.deviceId;

        // Skip the input-backend write while the same pad keeps an unchanged intensity within the dead band.
        if (!deviceChanged &&
            hasAppliedMotorSpeeds == 1 &&
            math.abs(lowFrequency - lastAppliedLowFrequency) < MotorSpeedEpsilon &&
            math.abs(highFrequency - lastAppliedHighFrequency) < MotorSpeedEpsilon)
            return;

        gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
        lastAppliedLowFrequency = lowFrequency;
        lastAppliedHighFrequency = highFrequency;
        hasAppliedMotorSpeeds = 1;
    }

    /// <summary>
    /// Resets the cached motor tracking so the next write is always sent.
    /// </summary>
    private void ResetMotorTracking()
    {
        lastAppliedLowFrequency = 0f;
        lastAppliedHighFrequency = 0f;
        lastGamepadDeviceId = 0;
        hasAppliedMotorSpeeds = 0;
    }

    /// <summary>
    /// Clears the rumble on every connected gamepad and resets tracking, used by the stop/destroy safety hooks.
    /// </summary>
    private void StopAllGamepadRumble()
    {
        ResetAllConnectedGamepadHaptics();
        ResetMotorTracking();
    }

    /// <summary>
    /// Resets haptics on every currently connected gamepad.
    /// </summary>
    private static void ResetAllConnectedGamepadHaptics()
    {
        for (int index = 0; index < Gamepad.all.Count; index++)
            Gamepad.all[index].ResetHaptics();
    }
    #endregion

    #endregion
}
