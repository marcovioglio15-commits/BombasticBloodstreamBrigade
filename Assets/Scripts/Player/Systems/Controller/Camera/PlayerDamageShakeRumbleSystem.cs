using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;
#endif

/// <summary>
/// Drives a connected gamepad's rumble from both shake channels (damage and fire) already evolved by
/// <see cref="PlayerCameraFollowSystem"/> (the single trauma owner). It runs after the follow system so it reads the
/// smooth shake magnitudes resolved this frame, mixes the per-channel motor amplitudes by them and writes the sum to
/// the active gamepad. The same pause/end-of-run gates as the camera systems force the motors to rest, and once at
/// rest the stop is sent a single time so the idle majority of the run costs no input-backend writes while a fading
/// shake can never strand a residual motor speed (an endless faint rumble).
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerDamageShakeRumbleSystem : ISystem, ISystemStartStop
{
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
        float damageRumbleMultiplier = 1f;
        float fireRumbleMultiplier = 1f;

        if (SystemAPI.TryGetSingleton<PlayerUserExperienceSettings>(out PlayerUserExperienceSettings userSettings))
        {
            damageRumbleMultiplier = math.max(0f, userSettings.DamageRumbleMultiplier);
            fireRumbleMultiplier = math.max(0f, userSettings.FireRumbleMultiplier);
        }

        // While not silenced, sum the per-channel motor speeds so a simultaneous hit and fire shake mix on the gamepad.
        if (!ShouldSilenceRumble(isSceneTransitioning))
        {
            foreach ((RefRO<PlayerCameraShakeState> shakeState, RefRO<PlayerRuntimeCameraConfig> cameraConfig)
                     in SystemAPI.Query<RefRO<PlayerCameraShakeState>, RefRO<PlayerRuntimeCameraConfig>>())
            {
                ResolveDamageMotorSpeeds(in cameraConfig.ValueRO.Shake,
                                          shakeState.ValueRO.ShakeMagnitude,
                                          shakeState.ValueRO.DamageRumbleImpulseRemainingSeconds,
                                          out float damageLowFrequency,
                                          out float damageHighFrequency);
                ResolveFireMotorSpeeds(in cameraConfig.ValueRO.FireShake,
                                        shakeState.ValueRO.FireShakeMagnitude,
                                        shakeState.ValueRO.FireRumbleImpulseRemainingSeconds,
                                        out float fireLowFrequency,
                                        out float fireHighFrequency);
                // Cap the sum at the gamepad's normalized [0..1] motor range, otherwise overlapping shakes would clip.
                targetLowFrequency = math.saturate(damageLowFrequency * damageRumbleMultiplier + fireLowFrequency * fireRumbleMultiplier);
                targetHighFrequency = math.saturate(damageHighFrequency * damageRumbleMultiplier + fireHighFrequency * fireRumbleMultiplier);
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
    /// Dying bypasses the hard-pause guard: the freeze system pins gameplay time to zero on the lethal hit, but the
    /// rumble feedback must keep playing along the trauma envelope so the final beat is felt on the controller.
    /// </summary>
    /// <param name="isSceneTransitioning">True while the scene manager is loading or fading between scenes.</param>
    /// <returns>True when the motors must be forced to rest for the current frame.</returns>
    private readonly bool ShouldSilenceRumble(bool isSceneTransitioning)
    {
        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery))
            return true;

        if (PlayerGameplayPauseUtility.IsDyingRunOutcomeActive(runOutcomeQuery))
            return false;

        // A hard time-scale pause freezes gameplay, but a transition-owned pause must still settle the motors to rest.
        return PlayerGameplayPauseUtility.IsTimeScaleHardPaused() && !isSceneTransitioning;
    }
    #endregion

    #region Motor Resolution
    /// <summary>
    /// Resolves the two normalized motor speeds from the damage rumble config and the current damage envelope
    /// magnitude. Both motors share the same trauma envelope, so the vibration ramps down together with the
    /// on-screen kick.
    /// </summary>
    /// <param name="shake">Resolved runtime damage shake config carrying the rumble enable flag and motor amplitudes.</param>
    /// <param name="shakeMagnitude">Smooth damage envelope magnitude in the [0..1] range resolved by the shake utility.</param>
    /// <param name="impulseRemainingSeconds">Seconds left on the SingleImpulse burst; ignored while Continuous.</param>
    /// <param name="lowFrequency">Resolved heavy (low-frequency) motor speed in the [0..1] range.</param>
    /// <param name="highFrequency">Resolved light (high-frequency) motor speed in the [0..1] range.</param>
    private static void ResolveDamageMotorSpeeds(in CameraShakeBlob shake,
                                                  float shakeMagnitude,
                                                  float impulseRemainingSeconds,
                                                  out float lowFrequency,
                                                  out float highFrequency)
    {
        // No rumble requested leaves both motors at rest regardless of trauma or impulse window.
        if (shake.RumbleEnabled == 0)
        {
            lowFrequency = 0f;
            highFrequency = 0f;
            return;
        }

        switch (shake.RumbleMotionMode)
        {
            case CameraShakeRumbleMotionMode.SingleImpulse:
                ResolveImpulseMotorSpeeds(impulseRemainingSeconds,
                                          shake.RumbleLowFrequency,
                                          shake.RumbleHighFrequency,
                                          out lowFrequency,
                                          out highFrequency);
                return;
            default:
                ResolveContinuousMotorSpeeds(shakeMagnitude,
                                             shake.RumbleLowFrequency,
                                             shake.RumbleHighFrequency,
                                             out lowFrequency,
                                             out highFrequency);
                return;
        }
    }

    /// <summary>
    /// Mirror of <see cref="ResolveDamageMotorSpeeds"/> for the fire shake channel. The two channels carry independent
    /// rumble settings, so a designer can drive only one motor for fire while damage uses both, or balance the two
    /// shakes without coupling their amplitudes.
    /// </summary>
    /// <param name="shake">Resolved runtime fire shake config carrying the rumble enable flag and motor amplitudes.</param>
    /// <param name="shakeMagnitude">Smooth fire envelope magnitude in the [0..1] range resolved by the shake utility.</param>
    /// <param name="impulseRemainingSeconds">Seconds left on the SingleImpulse burst; ignored while Continuous.</param>
    /// <param name="lowFrequency">Resolved heavy (low-frequency) motor speed in the [0..1] range.</param>
    /// <param name="highFrequency">Resolved light (high-frequency) motor speed in the [0..1] range.</param>
    private static void ResolveFireMotorSpeeds(in CameraFireShakeBlob shake,
                                                float shakeMagnitude,
                                                float impulseRemainingSeconds,
                                                out float lowFrequency,
                                                out float highFrequency)
    {
        if (shake.RumbleEnabled == 0)
        {
            lowFrequency = 0f;
            highFrequency = 0f;
            return;
        }

        switch (shake.RumbleMotionMode)
        {
            case CameraShakeRumbleMotionMode.SingleImpulse:
                ResolveImpulseMotorSpeeds(impulseRemainingSeconds,
                                          shake.RumbleLowFrequency,
                                          shake.RumbleHighFrequency,
                                          out lowFrequency,
                                          out highFrequency);
                return;
            default:
                ResolveContinuousMotorSpeeds(shakeMagnitude,
                                             shake.RumbleLowFrequency,
                                             shake.RumbleHighFrequency,
                                             out lowFrequency,
                                             out highFrequency);
                return;
        }
    }

    /// <summary>
    /// Resolves the motor speeds for the Continuous rumble mode: both motors track the trauma envelope so the vibration
    /// ramps down together with the on-screen kick.
    /// </summary>
    /// <param name="shakeMagnitude">Smooth envelope magnitude in the [0..1] range.</param>
    /// <param name="rumbleLowFrequencyAmplitude">Authored low-frequency motor amplitude.</param>
    /// <param name="rumbleHighFrequencyAmplitude">Authored high-frequency motor amplitude.</param>
    /// <param name="lowFrequency">Resolved heavy motor speed in [0..1].</param>
    /// <param name="highFrequency">Resolved light motor speed in [0..1].</param>
    private static void ResolveContinuousMotorSpeeds(float shakeMagnitude,
                                                      float rumbleLowFrequencyAmplitude,
                                                      float rumbleHighFrequencyAmplitude,
                                                      out float lowFrequency,
                                                      out float highFrequency)
    {
        float magnitude = math.saturate(shakeMagnitude);

        if (magnitude <= 0f)
        {
            lowFrequency = 0f;
            highFrequency = 0f;
            return;
        }

        lowFrequency = math.saturate(rumbleLowFrequencyAmplitude) * magnitude;
        highFrequency = math.saturate(rumbleHighFrequencyAmplitude) * magnitude;
    }

    /// <summary>
    /// Resolves the motor speeds for the SingleImpulse rumble mode: a clean burst at the authored amplitudes lasts for
    /// the configured impulse duration and then rests. Decoupling from the trauma envelope keeps the burst feel sharp
    /// and predictable so a long-tailed envelope does not stretch the rumble into a sustained vibration.
    /// </summary>
    /// <param name="impulseRemainingSeconds">Seconds left on the impulse window resolved by the shake utility.</param>
    /// <param name="rumbleLowFrequencyAmplitude">Authored low-frequency motor amplitude.</param>
    /// <param name="rumbleHighFrequencyAmplitude">Authored high-frequency motor amplitude.</param>
    /// <param name="lowFrequency">Resolved heavy motor speed in [0..1].</param>
    /// <param name="highFrequency">Resolved light motor speed in [0..1].</param>
    private static void ResolveImpulseMotorSpeeds(float impulseRemainingSeconds,
                                                   float rumbleLowFrequencyAmplitude,
                                                   float rumbleHighFrequencyAmplitude,
                                                   out float lowFrequency,
                                                   out float highFrequency)
    {
        if (impulseRemainingSeconds <= 0f)
        {
            lowFrequency = 0f;
            highFrequency = 0f;
            return;
        }

        lowFrequency = math.saturate(rumbleLowFrequencyAmplitude);
        highFrequency = math.saturate(rumbleHighFrequencyAmplitude);
    }
    #endregion

    #region Motor Output
    /// <summary>
    /// Writes the resolved motor speeds to the active gamepad. While the motors are at rest the stop is delivered
    /// exactly once and then suppressed, which both avoids per-frame writes during the idle majority of the run and
    /// guarantees a fading hit always lands on a clean zero: a residual sub-perceptible motor speed can never get
    /// stranded on (the cause of an endless faint rumble). Active intensities are refreshed every frame over the
    /// short shake window, and a write is forced whenever the active gamepad changes so a newly selected pad syncs.
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

        // Once already resting on the same pad, suppress further idle writes; any non-rest target always writes.
        bool targetAtRest = lowFrequency <= 0f && highFrequency <= 0f;
        bool alreadyResting = hasAppliedMotorSpeeds == 1 && lastAppliedLowFrequency <= 0f && lastAppliedHighFrequency <= 0f;

        if (targetAtRest && alreadyResting && !deviceChanged)
            return;

#if UNITY_WEBGL && !UNITY_EDITOR
        WebGlGamepadRumbleRuntimeUtility.SetMotorSpeeds(lowFrequency, highFrequency);
#else
        gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGlGamepadRumbleRuntimeUtility.Reset();
#else
        for (int index = 0; index < Gamepad.all.Count; index++)
            Gamepad.all[index].ResetHaptics();
#endif
    }
    #endregion

    #endregion
}

#if UNITY_WEBGL && !UNITY_EDITOR
/// <summary>
/// Bridges the ECS rumble mixer to the browser Gamepad API because Unity Input System does not expose WebGL rumble.
/// </summary>
internal static class WebGlGamepadRumbleRuntimeUtility
{
    #region Constants
    private const float RefreshIntervalSeconds = 0.05f;
    private const int EffectDurationMilliseconds = 100;
    #endregion

    #region Fields
    private static float nextRefreshTime;
    private static bool effectActive;
    #endregion

    #region Native Methods
    [DllImport("__Internal")]
    private static extern void BombasticWebGLGamepadSetRumble(float lowFrequency,
                                                              float highFrequency,
                                                              int durationMilliseconds);

    [DllImport("__Internal")]
    private static extern void BombasticWebGLGamepadResetRumble();
    #endregion

    #region Methods
    /// <summary>
    /// Refreshes a short browser haptic effect while rumble is active and stops it immediately when both motors rest.
    /// </summary>
    /// <param name="lowFrequency">Heavy motor intensity in the normalized 0..1 range.</param>
    /// <param name="highFrequency">Light motor intensity in the normalized 0..1 range.</param>
    public static void SetMotorSpeeds(float lowFrequency, float highFrequency)
    {
        float clampedLowFrequency = Mathf.Clamp01(lowFrequency);
        float clampedHighFrequency = Mathf.Clamp01(highFrequency);
        bool targetActive = clampedLowFrequency > 0f || clampedHighFrequency > 0f;

        if (!targetActive)
        {
            Reset();
            return;
        }

        float currentTime = Time.unscaledTime;

        if (effectActive && currentTime < nextRefreshTime)
            return;

        BombasticWebGLGamepadSetRumble(clampedLowFrequency,
                                      clampedHighFrequency,
                                      EffectDurationMilliseconds);
        nextRefreshTime = currentTime + RefreshIntervalSeconds;
        effectActive = true;
    }

    /// <summary>
    /// Stops every browser gamepad actuator that may have received a previous effect.
    /// </summary>
    public static void Reset()
    {
        if (!effectActive)
            return;

        BombasticWebGLGamepadResetRumble();
        nextRefreshTime = 0f;
        effectActive = false;
    }
    #endregion
}
#endif
