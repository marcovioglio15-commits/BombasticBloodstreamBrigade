using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Drop Components
/// <summary>
/// Stores summary flags for the baked drop-item modules available on one enemy.
/// </summary>
public struct EnemyDropItemsConfig : IComponentData
{
    public EnemyDropItemsModuleCombineMode ModuleCombineMode;
    public byte HasExperienceDrops;
    public byte HasExtraComboPoints;
    public byte HasRecoveryDrops;
    public int ExperienceModuleCount;
    public int ExtraComboPointsModuleCount;
    public int RecoveryModuleCount;
    public int SelectionModuleCount;
    public int MinimumSelectedModules;
    public int MaximumSelectedModules;
    public int EstimatedDropsPerDeath;
}

/// <summary>
/// Maps one authored Drop Items module entry to its runtime payload buffer for weighted death-time selection.
/// </summary>
public struct EnemyDropItemsModuleSelectionElement : IBufferElementData
{
    public EnemyDropItemsPayloadKind PayloadKind;
    public int ModuleIndex;
    public float SelectionWeight;
}

/// <summary>
/// Declares the runtime reward payload carried by one pooled drop entity.
/// </summary>
public enum EnemyDropPickupRewardKind : byte
{
    Experience = 0,
    Recovery = 1
}

/// <summary>
/// Stores one baked experience-drop module entry.
/// Definition indices address a slice inside EnemyExperienceDropDefinitionElement.
/// </summary>
public struct EnemyExperienceDropModuleElement : IBufferElementData
{
    public float MinimumTotalExperienceDrop;
    public float MaximumTotalExperienceDrop;
    public float Distribution;
    public float DropRadius;
    public float GroundHeightOffset;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float SpawnAnimationMinDuration;
    public float SpawnAnimationMaxDuration;
    public int DefinitionStartIndex;
    public int DefinitionCount;
    public int EstimatedDropsPerDeath;
    public float SelectionWeight;
}

/// <summary>
/// Stores one baked experience drop-definition entry.
/// </summary>
public struct EnemyExperienceDropDefinitionElement : IBufferElementData
{
    public Entity PrefabEntity;
    public float ExperienceAmount;
}

/// <summary>
/// Stores one baked health/shield recovery-drop module entry.
/// Definition indices address a slice inside EnemyRecoveryDropDefinitionElement.
/// </summary>
public struct EnemyRecoveryDropModuleElement : IBufferElementData
{
    public float DropChance;
    public float DropRadius;
    public float GroundHeightOffset;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float SpawnAnimationMinDuration;
    public float SpawnAnimationMaxDuration;
    public int DefinitionStartIndex;
    public int DefinitionCount;
    public int EstimatedDropsPerDeath;
    public float SelectionWeight;
}

/// <summary>
/// Stores one baked health/shield recovery drop-definition entry.
/// </summary>
public struct EnemyRecoveryDropDefinitionElement : IBufferElementData
{
    public Entity PrefabEntity;
    public float HealthRestoreAmount;
    public float ShieldRestoreAmount;
    public int Count;
}

/// <summary>
/// Stores one baked Extra Combo Points module entry.
/// Condition indices address a slice inside EnemyExtraComboPointsConditionElement.
/// </summary>
public struct EnemyExtraComboPointsModuleElement : IBufferElementData
{
    public float BaseMultiplier;
    public float MinimumFinalMultiplier;
    public float MaximumFinalMultiplier;
    public EnemyExtraComboPointsConditionCombineMode ConditionCombineMode;
    public int ConditionStartIndex;
    public int ConditionCount;
    public float SelectionWeight;
}

/// <summary>
/// Stores one baked Extra Combo Points condition entry with a normalized sampled response curve.
/// </summary>
public struct EnemyExtraComboPointsConditionElement : IBufferElementData
{
    public EnemyExtraComboPointsMetric Metric;
    public float MinimumValue;
    public byte UseMaximumValue;
    public float MaximumValue;
    public float MinimumMultiplier;
    public float MaximumMultiplier;
    public FixedList64Bytes<float> NormalizedMultiplierCurveSamples;
}

/// <summary>
/// Stores runtime data for one spawned experience drop entity.
/// </summary>
public struct EnemyExperienceDrop : IComponentData
{
    public EnemyDropPickupRewardKind RewardKind;
    public float ExperienceAmount;
    public float HealthRestoreAmount;
    public float ShieldRestoreAmount;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float3 SpawnStartPosition;
    public float3 SpawnTargetPosition;
    public float SpawnAnimationDuration;
    public float SpawnAnimationElapsed;
    public Entity PoolEntity;
    public byte IsAttracting;
    public byte ConsumeWhenUnusable;
    public byte IsRoomClearAttraction;
}

/// <summary>
/// Marks active pooled experience drop entities that must be simulated.
/// </summary>
public struct EnemyExperienceDropActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Singleton registry state for experience drop pools.
/// </summary>
public struct EnemyExperienceDropPoolRegistry : IComponentData
{
    public byte Initialized;
    public uint SourceHash;
}

/// <summary>
/// Maps one drop prefab entity to its dedicated pool entity.
/// </summary>
public struct EnemyExperienceDropPoolMapElement : IBufferElementData
{
    public Entity PrefabEntity;
    public Entity PoolEntity;
}

/// <summary>
/// Stores one experience drop pool configuration and initialization state.
/// </summary>
public struct EnemyExperienceDropPoolState : IComponentData
{
    public Entity PrefabEntity;
    public int InitialCapacity;
    public int ExpandBatch;
    public byte Initialized;
}

/// <summary>
/// Stores pooled experience drop entity references for one pool.
/// </summary>
public struct EnemyExperienceDropPoolElement : IBufferElementData
{
    public Entity DropEntity;
}

/// <summary>
/// Identifies the unique runtime queue shared by active power-ups and authoritative room-clear collection.
/// </summary>
public struct EnemyDropCollectionRequestQueue : IComponentData
{
    public uint LastQueuedRunSeed;
    public uint LastQueuedGenerationVersion;
    public uint LastQueuedRoomClearVersion;
    public byte HasQueuedRoomClear;
}

/// <summary>
/// Stores one merged request that attracts drops inside the widest requested player-centered radius.
/// </summary>
[InternalBufferCapacity(1)]
public struct EnemyDropCollectionRequest : IBufferElementData
{
    public float AttractionRadius;
    public byte ConsumeUnusableDrops;
}
#endregion
