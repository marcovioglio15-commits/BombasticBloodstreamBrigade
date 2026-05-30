using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Stores the runtime trauma and damage-detection baselines for both camera-shake channels (damage on hit and fire
/// on primary-shot spawn), together with the per-frame combined shake output. Trauma and output are evolved once
/// per frame by <see cref="PlayerCameraFollowSystem"/> (the single owner). The resulting offset/roll are consumed by
/// both player camera systems when they write the camera transform, so room-fixed and follow cameras stay in sync
/// without recomputing or double-counting trauma. The two channels keep independent trauma and rumble magnitudes so
/// fire-rate stacking does not interfere with damage feedback and the gamepad rumble is the sum of both envelopes.
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
    #endregion

    #region Fire Trauma State
    // Current accumulated fire trauma in the [0..1] range. Each primary-shot spawn adds trauma and it decays linearly.
    public float FireTrauma;

    // Set to 1 by any system that emits one or more player primary shots this frame. The camera follow system
    // consumes the flag (clearing it back to 0) when it evolves the fire trauma envelope, so a single fire pulse
    // adds trauma exactly once even if multiple producers run before the consumer.
    public byte FireRequestPending;
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

    // World-space shake offset added to the camera position this frame (sum of damage and fire channels).
    public float3 PositionOffset;

    // View-axis roll in radians layered on top of the base camera rotation this frame (sum of damage and fire channels).
    public float RollRadians;

    // Offset applied last frame; removed before smoothing so the shake never feeds back into the follow spring.
    public float3 PreviousAppliedPositionOffset;

    // Roll applied last frame; removed before re-applying so the shake never accumulates into the base rotation.
    public float PreviousAppliedRollRadians;
    #endregion
}
#endregion
