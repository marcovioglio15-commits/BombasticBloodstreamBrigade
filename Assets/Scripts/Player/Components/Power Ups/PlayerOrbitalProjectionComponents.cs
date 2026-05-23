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
    public FixedList512Bytes<OrbitalProjectionConfig> Projections;
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
/// Runtime state stored on one orbital projection entity.
/// </summary>
public struct PlayerOrbitalProjectionInstance : IComponentData
{
    public Entity OwnerEntity;
    public FixedString64Bytes PowerUpId;
    public int ProjectionIndex;
    public int SourceInstanceId;
    public byte Persistent;
    public PlayerOrbitalProjectionPhase Phase;
    public OrbitalProjectionConfig Config;
    public float RemainingLifetimeSeconds;
    public float CurrentHealth;
    public float AngleDegrees;
    public float FollowAngleDegrees;
    public sbyte OrbitBounceDirection;
    public float PhaseElapsedSeconds;
    public float3 DespawnStartPosition;
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
