using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Runtime animation phase for one player-owned orbital projection.
/// </summary>
public enum PlayerOrbitalProjectionPhase : byte
{
    Spawning = 0,
    Active = 1,
    Despawning = 2
}

/// <summary>
/// Request buffer entry used by active power-ups to spawn timed orbital projections.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerOrbitalProjectionSpawnRequest : IBufferElementData
{
    public Entity OwnerEntity;
    public FixedString64Bytes PowerUpId;
    public byte Persistent;
    public int SourceInstanceId;
    public FixedList4096Bytes<OrbitalProjectionConfig> Projections;
}

/// <summary>
/// Remappable prefab binding table used by orbital projection configs stored inside fixed lists.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerOrbitalProjectionPrefabElement : IBufferElementData
{
    public int BindingIndex;
    public Entity PrefabEntity;
}

/// <summary>
/// Player-owned table of baked XZ convex-hull vertices for prefab-bound orbital projections.
/// Vertices are stored counter-clockwise in prefab-local space and keyed by the same binding index
/// used by the prefab binding table, so spawn can copy the matching silhouette onto each instance.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerOrbitalProjectionHullVertexElement : IBufferElementData
{
    public int BindingIndex;
    public float2 LocalPositionXZ;
}

/// <summary>
/// Per-projection collision silhouette copied at spawn from the player hull table. Stored
/// counter-clockwise in prefab-local XZ space; empty when the projection uses the plain radius.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerOrbitalProjectionCollisionVertexElement : IBufferElementData
{
    public float2 LocalPositionXZ;
}

/// <summary>
/// Permanent runtime loss marker for persistent health-based orbital projections.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerOrbitalProjectionLostElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public int ProjectionIndex;
    public int SourceInstanceId;
}

/// <summary>
/// Runtime state stored on one orbital projection entity. StableOrderKey assigns deterministic shared-ring
/// slots without relying on volatile entity indices. FollowLookAngleDegrees stores a continuously unwrapped
/// player-look target, while FollowAngularVelocityDegrees exposes the resulting visible follow velocity.
/// </summary>
public struct PlayerOrbitalProjectionInstance : IComponentData
{
    public Entity OwnerEntity;
    public FixedString64Bytes PowerUpId;
    public int ProjectionIndex;
    public int SourceInstanceId;
    public int StableOrderKey;
    public byte Persistent;
    public PlayerOrbitalProjectionPhase Phase;
    public OrbitalProjectionConfig Config;
    public float RemainingLifetimeSeconds;
    public float CurrentHealth;
    public float AngleDegrees;
    public float FollowAngleDegrees;
    public float FollowLookAngleDegrees;
    public float FollowAngularVelocityDegrees;
    public sbyte OrbitBounceDirection;
    public float PhaseElapsedSeconds;
    public float3 DespawnStartPosition;
}

/// <summary>
/// Tracks render entities suppressed together with their owning player's death animation. The previous rendering
/// state is preserved so fresh-run restoration never exposes a projection renderer hidden by another presentation
/// feature before the death-animation handoff.
/// </summary>
public struct PlayerOrbitalProjectionDeathVisibilityState : IComponentData
{
    public byte WasRenderingDisabled;
}

/// <summary>
/// Per-enemy contact cooldown tracked by one orbital projection.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerOrbitalProjectionEnemyContactElement : IBufferElementData
{
    public Entity EnemyEntity;
    public float CooldownRemainingSeconds;
}
