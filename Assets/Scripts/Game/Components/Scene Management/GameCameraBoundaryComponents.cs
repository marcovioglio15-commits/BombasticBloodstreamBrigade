using Unity.Entities;
using Unity.Mathematics;

#region Defaults
/// <summary>
/// Selects whether camera-boundary footprints contain the camera or act as impassable planar obstacles.
/// </summary>
public enum GameCameraBoundaryMode : byte
{
    ContainmentVolume = 0,
    ImpassableVolume = 1
}

/// <summary>
/// Defines shared defaults used when a camera-boundary preset is unavailable.
/// </summary>
public static class GameCameraBoundaryDefaults
{
    public const float SoftZoneDistance = 3f;
}
#endregion

#region Baked Configuration
/// <summary>
/// Stores one immutable oriented camera-boundary footprint on the world XZ plane.
/// </summary>
public struct GameCameraBoundary : IComponentData
{
    public float2 Center;
    public float2 HalfExtents;
    public float2 PlanarRight;
    public int Priority;
}
#endregion

#region Runtime State
/// <summary>
/// Stores the containment group selected for the local player and the resolved braking distance consumed by camera systems.
/// </summary>
public struct GameCameraBoundaryRuntimeState : IComponentData
{
    public Entity BoundaryEntity;
    public GameCameraBoundary Boundary;
    public float SoftZoneDistance;
    public GameCameraBoundaryMode Mode;
    public byte Enabled;
    public byte HasBoundary;
}

/// <summary>
/// Stores one member of the active same-priority containment group built from overlapping boundary footprints.
/// </summary>
[InternalBufferCapacity(8)]
public struct GameCameraBoundaryContainmentElement : IBufferElementData
{
    public Entity BoundaryEntity;
    public GameCameraBoundary Boundary;
}
#endregion

#region Fast Play State
/// <summary>
/// Marks the transient player used by Camera Boundary Fast Play and stores its preset-derived movement speed.
/// </summary>
public struct GameCameraBoundaryFastPlayPlayer : IComponentData
{
    public float MoveSpeed;
}

/// <summary>
/// Supplies Scene Manager Camera Boundary settings when Fast Play intentionally bypasses the normal bootstrap scene.
/// </summary>
public struct GameCameraBoundaryFastPlaySettings : IComponentData
{
    public float SoftZoneDistance;
    public GameCameraBoundaryMode Mode;
    public byte EnableCameraBoundaries;
}
#endregion
