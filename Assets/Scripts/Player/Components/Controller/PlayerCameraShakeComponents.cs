using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Stores runtime trauma and damage-detection baselines for damage, firing, and return-start camera-shake channels,
/// together with the per-frame combined shake output. Trauma and output are evolved once
/// per frame by <see cref="PlayerCameraFollowSystem"/> (the single owner). The resulting offset/roll/FOV delta are
/// consumed by the player camera systems when they write the camera transform and the FOV, so room-fixed and follow
/// cameras stay in sync without recomputing or double-counting trauma. Damage, firing, and return feedback keep
/// independent envelopes so rapid pulses do not overwrite one another, while gamepad output remains separately mixed.
/// The Continuous motion mode uses perlin noise; the SingleImpulse motion mode keeps a clean
/// per-channel direction sample chosen at trauma onset so the offset feels like a clear push instead of an oscillation.
/// </summary>
public struct PlayerCameraShakeState : IComponentData
{
    #region Damage Trauma State
    // Current accumulated damage trauma in the [0..1] range. A hit adds trauma and it decays linearly to zero.
    public float Trauma;

    // Last observed PlayerDamageGraceState.IgnoreDamageUntilTime, used to detect a fresh accepted hit this frame.
    public float LastDamageDeadline;

    // Last observed health-plus-shield total, used to size damage-scaled trauma from the survivability drop.
    public float LastSurvivability;

    // 0 until the first observed frame seeds the damage-detection baselines, preventing a spawn-time shake.
    public byte Initialized;

    // Sign in {-1, 0, 1} chosen for each axis at the last accepted damage hit; consumed by the SingleImpulse path so
    // every fresh hit picks a fully random direction once instead of oscillating through the noise field.
    public float3 DamageImpulseDirection;

    // Sign in {-1, 0, 1} chosen for the view-axis roll at the last accepted damage hit; consumed by the SingleImpulse
    // path to give the roll a clear direction instead of sampling noise.
    public float DamageImpulseRollSign;

    // Seconds remaining on the damage single-impulse rumble burst, only consumed while RumbleMotionMode == SingleImpulse.
    public float DamageRumbleImpulseRemainingSeconds;
    #endregion

    #region Fire Trauma State
    // Current accumulated fire trauma in the [0..1] range. Each primary-shot spawn adds trauma and it decays linearly.
    public float FireTrauma;

    // Set to 1 by any system that emits one or more player primary shots this frame. The camera follow system
    // consumes the flag (clearing it back to 0) when it evolves the fire trauma envelope, so a single fire pulse
    // adds trauma exactly once even if multiple producers run before the consumer.
    public byte FireRequestPending;

    // Sign in {-1, 0, 1} chosen for each axis at the last consumed fire pulse; same role as DamageImpulseDirection.
    public float3 FireImpulseDirection;

    // Sign in {-1, 0, 1} chosen for the view-axis roll at the last consumed fire pulse.
    public float FireImpulseRollSign;

    // Seconds remaining on the fire single-impulse rumble burst, only consumed while RumbleMotionMode == SingleImpulse.
    public float FireRumbleImpulseRemainingSeconds;
    #endregion

    #region Return Camera Shake State
    // Largest return-start camera multiplier enqueued during the current simulation frame. Presentation consumes it once.
    public float ReturnCameraShakeRequestMultiplier;

    // Independent normalized trauma envelope for camera-only feedback when a projectile starts return travel.
    public float ReturnCameraShakeTrauma;

    // Module strength retained for the lifetime of the current return camera-shake envelope.
    public float ReturnCameraShakeMultiplier;

    // Stable per-pulse direction used when firing camera shake is configured as SingleImpulse.
    public float3 ReturnCameraShakeImpulseDirection;

    // Stable per-pulse roll sign used when firing camera shake is configured as SingleImpulse.
    public float ReturnCameraShakeImpulseRollSign;
    #endregion

    #region Return Rumble State
    // Largest return-start multiplier enqueued during the current simulation frame. Presentation consumes it once.
    public float ReturnRumbleRequestMultiplier;

    // Normalized haptic-only envelope used when the firing rumble is configured for Continuous motion.
    public float ReturnRumbleEnvelope;

    // Authored module strength retained while the current haptic-only return envelope remains active.
    public float ReturnRumbleMultiplier;

    // Seconds remaining on the haptic-only return burst when firing rumble uses SingleImpulse motion.
    public float ReturnRumbleImpulseRemainingSeconds;
    #endregion

    #region Frame Output
    // Smooth envelope magnitude in the [0..1] range resolved from the remaining damage trauma this frame, before any
    // noise modulation. Drives the connected-gamepad rumble so the haptic ramps down cleanly instead of buzzing
    // with the per-axis noise that shapes the camera offset.
    public float ShakeMagnitude;

    // Smooth envelope magnitude in the [0..1] range resolved from the remaining fire trauma this frame. Kept on a
    // separate channel from <see cref="ShakeMagnitude"/> so the shared rumble system can mix each channel through its
    // own motor amplitudes before driving the gamepad.
    public float FireShakeMagnitude;

    // Smooth return-start camera envelope after its module multiplier, kept separate from firing rumble resolution.
    public float ReturnCameraShakeMagnitude;

    // World-space shake offset added to the camera position this frame (sum of damage, fire, and return channels).
    public float3 PositionOffset;

    // View-axis roll in radians layered on top of the base camera rotation this frame (sum of damage, fire, and return channels).
    public float RollRadians;

    // Field-of-view delta in degrees applied on top of the base camera FOV this frame (sum of damage, fire, and return channels).
    public float FovDelta;

    // Offset applied last frame; removed before smoothing so the shake never feeds back into the follow spring.
    public float3 PreviousAppliedPositionOffset;

    // Roll applied last frame; removed before re-applying so the shake never accumulates into the base rotation.
    public float PreviousAppliedRollRadians;

    // FOV delta applied last frame; consumed by the camera follow system to restore the un-shaken base FOV before
    // re-layering this frame's delta, so the zoom can never accumulate into the authored field of view.
    public float PreviousAppliedFovDelta;
    #endregion
}
#endregion
