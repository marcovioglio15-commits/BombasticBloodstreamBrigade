using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Stores the runtime trauma and damage-detection baselines for the damage-driven camera shake, together with the
/// per-frame shake output. Trauma and output are evolved once per frame by <see cref="PlayerCameraFollowSystem"/>
/// (the single owner) and the resulting offset/roll are consumed by both player camera systems when they write the
/// camera transform, so room-fixed and follow cameras stay in sync without recomputing or double-counting trauma.
/// </summary>
public struct PlayerCameraShakeState : IComponentData
{
    #region Trauma State
    // Current accumulated trauma in the [0..1] range. A hit adds trauma and it decays linearly to zero.
    public float Trauma;

    // Last observed PlayerDamageGraceState.IgnoreDamageUntilTime, used to detect a fresh accepted hit this frame.
    public float LastDamageDeadline;

    // Last observed health-plus-shield total, used to size damage-scaled trauma from the survivability drop.
    public float LastSurvivability;

    // 0 until the first observed frame seeds the damage-detection baselines, preventing a spawn-time shake.
    public byte Initialized;
    #endregion

    #region Frame Output
    // World-space shake offset added to the camera position this frame.
    public float3 PositionOffset;

    // View-axis roll in radians layered on top of the base camera rotation this frame.
    public float RollRadians;

    // Offset applied last frame; removed before smoothing so the shake never feeds back into the follow spring.
    public float3 PreviousAppliedPositionOffset;

    // Roll applied last frame; removed before re-applying so the shake never accumulates into the base rotation.
    public float PreviousAppliedRollRadians;
    #endregion
}
#endregion
