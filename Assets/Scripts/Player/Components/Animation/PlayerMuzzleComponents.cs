using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores the current animated muzzle pose plus a root-local offset that can be reconstructed against the latest ECS player transform.
/// Used by gameplay systems that need projectile origins aligned with animated weapon motion without inheriting stale world-space drift.
/// None.
/// </summary>
public struct PlayerAnimatedMuzzleWorldPose : IComponentData
{
    #region Fields
    public float3 Position;
    public quaternion Rotation;
    public float3 LocalPosition;
    public float ForwardShotOffset;
    public float MinimumPlanarDistanceFromPlayer;
    public byte IsValid;
    #endregion
}
