using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

#region Portal Components
/// <summary>
/// Stores one baked room portal volume, arrival pose and graph-facing authoring policy.
/// </summary>
public struct GameRoomPortal : IComponentData
{
    public FixedString64Bytes PortalId;
    public GameRoomPortalSide Side;
    public GameRoomPortalCapability Capability;
    public GameRoomPortalConnectionPolicy Policy;
    public float3 Center;
    public float3 HalfExtents;
    public quaternion Rotation;
    public float3 ArrivalPosition;
    public quaternion ArrivalRotation;
    public float InwardOffset;
    public byte RequireRoomClear;
}

/// <summary>
/// Stores mutable assignment and entry-latch state for one room portal instance.
/// </summary>
public struct GameRoomPortalRuntimeState : IComponentData
{
    public int AssignedEdgeIndex;
    public byte TraversalEnabled;
    public byte WasPlayerInside;
    public byte HasTriggered;
}

/// <summary>
/// Stores the fail-closed Unity Physics collider owned by one logical room portal.
/// </summary>
public struct GameRoomPortalBlocker : IComponentData
{
    public FixedString64Bytes PortalId;
    public BlobAssetReference<Collider> BlockingCollider;
    public byte IsBlocking;
}
#endregion

#region Arrival Components
/// <summary>
/// Stores the baked fallback pose used when a level enables center-based room arrival.
/// </summary>
public struct GameRoomCenterAnchor : IComponentData
{
    public float3 Position;
    public quaternion Rotation;
}
#endregion
