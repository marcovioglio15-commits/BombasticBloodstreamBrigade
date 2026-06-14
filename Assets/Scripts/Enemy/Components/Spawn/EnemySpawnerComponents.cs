using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

#region Enemy Spawner Components
/// <summary>
/// Defines immutable runtime configuration for a finite wave-based enemy spawner.
/// </summary>
public struct EnemySpawner : IComponentData
{
    public int InitialPoolCapacityPerPrefab;
    public int ExpandBatchPerPrefab;
    public float DespawnDistance;
    public float MaximumSpawnDistanceFromCenter;
    public int TotalPlannedEnemyCount;
}

/// <summary>
/// Stores mutable runtime state shared by all waves of a spawner.
/// </summary>
public struct EnemySpawnerState : IComponentData
{
    public float StartTime;
    public int AliveCount;
    public byte Initialized;
    public byte StartTimeInitialized;
}

/// <summary>
/// Identifies one authored spawner across runtime scene loads and main-menu override edits.
/// </summary>
public struct EnemySpawnerRuntimeIdentity : IComponentData
{
    public FixedString64Bytes SceneGuid;
    public FixedString128Bytes SpawnerGuid;
    public FixedString128Bytes DisplayName;
    public FixedString64Bytes DefaultWavePresetGuid;
    public byte DefaultEnabled;
}

/// <summary>
/// Stores the last runtime override version applied to one spawner entity.
/// </summary>
public struct EnemySpawnerRuntimeOverrideState : IComponentData
{
    public uint AppliedStoreVersion;
    public FixedString64Bytes AppliedWavePresetGuid;
    public uint FailedStoreVersion;
    public FixedString64Bytes FailedWavePresetGuid;
    public byte AppliedEnabled;
}

/// <summary>
/// Declares how one wave derives its start time from the previous wave.
/// </summary>
public enum EnemyWaveStartMode : byte
{
    FromSpawnerStart = 0,
    AfterPreviousWaveStart = 1,
    AfterPreviousWaveSpawnEnd = 2,
    AfterPreviousWaveCompleted = 3,
    AfterPreviousWaveFirstKill = 4
}

/// <summary>
/// Stores immutable wave timing and event-slice data.
/// </summary>
public struct EnemySpawnerWaveDefinitionElement : IBufferElementData
{
    public EnemyWaveStartMode StartMode;
    public float StartDelaySeconds;
    public float SpawnDurationSeconds;
    public float MaximumSpawnWarningLeadTimeSeconds;
    public int FirstEventIndex;
    public int EventCount;
}

/// <summary>
/// Stores mutable runtime state for one authored wave.
/// </summary>
public struct EnemySpawnerWaveRuntimeElement : IBufferElementData
{
    public float ScheduledStartTime;
    public float SpawnStartTime;
    public float SpawnEndTime;
    public float CompletionTime;
    public float FirstKillTime;
    public int NextEventIndex;
    public int NextWarningEventIndex;
    public int AliveCount;
    public int SpawnedCount;
    public byte StartScheduled;
    public byte Started;
    public byte SpawnFinished;
    public byte Completed;
    public byte FirstKillRegistered;
}

/// <summary>
/// Stores one exact spawn event baked from authored wave cells and curves.
/// </summary>
public struct EnemySpawnerWaveEventElement : IBufferElementData
{
    public int WaveIndex;
    public float RelativeTime;
    public float3 LocalSpawnPosition;
    public Entity PrefabEntity;
    public Entity ReservedEnemyEntity;
    public byte HasSpawnWarningOverride;
    public EnemySpawnWarningConfig SpawnWarningOverride;
}

/// <summary>
/// Stores one unique enemy prefab referenced by the spawner and its planned usage count.
/// </summary>
public struct EnemySpawnerPrefabRequirementElement : IBufferElementData
{
    public Entity PrefabEntity;
    public int TotalPlannedCount;
}

/// <summary>
/// Maps a referenced enemy prefab to the runtime pool entity created for it.
/// </summary>
public struct EnemySpawnerPrefabPoolMapElement : IBufferElementData
{
    public Entity PrefabEntity;
    public Entity PoolEntity;
}

/// <summary>
/// Describes one pre-baked wave-preset variant available to a runtime-overridable spawner.
/// </summary>
public struct EnemySpawnerWavePresetVariantElement : IBufferElementData
{
    public FixedString64Bytes WavePresetGuid;
    public int FirstDefinitionIndex;
    public int DefinitionCount;
    public int FirstEventIndex;
    public int EventCount;
    public int FirstRequirementIndex;
    public int RequirementCount;
    public float MaximumSpawnDistanceFromCenter;
    public int TotalPlannedEnemyCount;
}

/// <summary>
/// Stores immutable wave definitions for all pre-baked preset variants on one spawner.
/// </summary>
public struct EnemySpawnerWavePresetVariantDefinitionElement : IBufferElementData
{
    public EnemyWaveStartMode StartMode;
    public float StartDelaySeconds;
    public float SpawnDurationSeconds;
    public float MaximumSpawnWarningLeadTimeSeconds;
    public int FirstEventIndex;
    public int EventCount;
}

/// <summary>
/// Stores exact spawn events for all pre-baked preset variants on one spawner.
/// </summary>
public struct EnemySpawnerWavePresetVariantEventElement : IBufferElementData
{
    public int WaveIndex;
    public float RelativeTime;
    public float3 LocalSpawnPosition;
    public Entity PrefabEntity;
    public byte HasSpawnWarningOverride;
    public EnemySpawnWarningConfig SpawnWarningOverride;
}

/// <summary>
/// Stores prefab requirements for all pre-baked preset variants on one spawner.
/// </summary>
public struct EnemySpawnerWavePresetVariantRequirementElement : IBufferElementData
{
    public FixedString64Bytes WavePresetGuid;
    public Entity PrefabEntity;
    public int TotalPlannedCount;
}

/// <summary>
/// Stores immutable runtime settings for one enemy pool entity.
/// </summary>
public struct EnemyPoolState : IComponentData
{
    public Entity PrefabEntity;
    public int InitialCapacity;
    public int ExpandBatch;
    public byte Initialized;
}

/// <summary>
/// Stores pooled enemy entity references on one enemy pool entity.
/// </summary>
public struct EnemyPoolElement : IBufferElementData
{
    public Entity EnemyEntity;
}
#endregion
