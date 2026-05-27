using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies main-menu spawner overrides to newly loaded enemy spawner entities before pool initialization.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(EnemyExperienceDropPoolInitializeSystem))]
[UpdateBefore(typeof(EnemyPoolInitializeSystem))]
public partial struct EnemySpawnerRuntimeOverrideSystem : ISystem
{
    #region Fields
    private EntityQuery spawnerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the runtime-overridable spawner query, including entities disabled by their authored defaults.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        spawnerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<EnemySpawner>()
            .WithAllRW<EnemySpawnerState>()
            .WithAllRW<EnemySpawnerRuntimeOverrideState>()
            .WithAll<EnemySpawnerRuntimeIdentity>()
            .WithAll<EnemySpawnerWaveDefinitionElement>()
            .WithAll<EnemySpawnerWaveRuntimeElement>()
            .WithAll<EnemySpawnerWaveEventElement>()
            .WithAll<EnemySpawnerPrefabRequirementElement>()
            .WithAll<EnemySpawnerPrefabPoolMapElement>()
            .WithAll<EnemySpawnerWavePresetVariantElement>()
            .WithAll<EnemySpawnerWavePresetVariantDefinitionElement>()
            .WithAll<EnemySpawnerWavePresetVariantEventElement>()
            .WithAll<EnemySpawnerWavePresetVariantRequirementElement>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
            .Build(ref state);
        state.RequireForUpdate(spawnerQuery);
    }

    /// <summary>
    /// Applies pending override-store changes to every loaded spawner whose applied version is stale.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        uint storeVersion = EnemySpawnerRuntimeOverrideStore.Version;
        NativeArray<Entity> spawnerEntities = spawnerQuery.ToEntityArray(Allocator.Temp);

        try
        {
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
                ApplyOverrideIfNeeded(entityManager, spawnerEntities[spawnerIndex], storeVersion);
        }
        finally
        {
            if (spawnerEntities.IsCreated)
                spawnerEntities.Dispose();
        }
    }
    #endregion

    #region Apply
    /// <summary>
    /// Applies the current store version to one spawner entity when its local state is stale.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read and mutate ECS data.</param>
    /// <param name="spawnerEntity">Spawner entity to inspect.</param>
    /// <param name="storeVersion">Current global override store version.</param>
    private static void ApplyOverrideIfNeeded(EntityManager entityManager, Entity spawnerEntity, uint storeVersion)
    {
        if (!entityManager.Exists(spawnerEntity))
            return;

        EnemySpawnerRuntimeOverrideState overrideState = entityManager.GetComponentData<EnemySpawnerRuntimeOverrideState>(spawnerEntity);

        if (overrideState.AppliedStoreVersion == storeVersion)
            return;

        EnemySpawnerRuntimeIdentity identity = entityManager.GetComponentData<EnemySpawnerRuntimeIdentity>(spawnerEntity);
        FixedString64Bytes requestedWavePresetGuid;
        byte requestedEnabled;
        ResolveRequestedOverride(identity, out requestedEnabled, out requestedWavePresetGuid);

        if (overrideState.AppliedEnabled == requestedEnabled &&
            overrideState.AppliedWavePresetGuid.Equals(requestedWavePresetGuid))
        {
            overrideState.AppliedStoreVersion = storeVersion;
            entityManager.SetComponentData(spawnerEntity, overrideState);
            ApplyEnabledState(entityManager, spawnerEntity, requestedEnabled);
            MarkDisabledSpawnerReady(entityManager, spawnerEntity, requestedEnabled);
            return;
        }

        if (requestedWavePresetGuid.Length > 0)
            TryApplyWavePresetVariant(entityManager, spawnerEntity, requestedWavePresetGuid);

        ApplyEnabledState(entityManager, spawnerEntity, requestedEnabled);
        MarkDisabledSpawnerReady(entityManager, spawnerEntity, requestedEnabled);
        overrideState.AppliedStoreVersion = storeVersion;
        overrideState.AppliedWavePresetGuid = requestedWavePresetGuid;
        overrideState.AppliedEnabled = requestedEnabled;
        entityManager.SetComponentData(spawnerEntity, overrideState);
    }

    /// <summary>
    /// Resolves the requested override payload or falls back to the baked authoring defaults.
    /// </summary>
    /// <param name="identity">Spawner identity baked from authoring data.</param>
    /// <param name="requestedEnabled">Resolved enabled state.</param>
    /// <param name="requestedWavePresetGuid">Resolved wave preset GUID.</param>
    private static void ResolveRequestedOverride(EnemySpawnerRuntimeIdentity identity,
                                                 out byte requestedEnabled,
                                                 out FixedString64Bytes requestedWavePresetGuid)
    {
        string sceneGuid = identity.SceneGuid.ToString();
        string spawnerGuid = identity.SpawnerGuid.ToString();
        EnemySpawnerRuntimeOverrideValue overrideValue;

        if (EnemySpawnerRuntimeOverrideStore.TryGetOverride(sceneGuid, spawnerGuid, out overrideValue))
        {
            requestedEnabled = overrideValue.Enabled ? (byte)1 : (byte)0;
            requestedWavePresetGuid = string.IsNullOrWhiteSpace(overrideValue.WavePresetGuid)
                ? identity.DefaultWavePresetGuid
                : new FixedString64Bytes(overrideValue.WavePresetGuid);
            return;
        }

        requestedEnabled = identity.DefaultEnabled;
        requestedWavePresetGuid = identity.DefaultWavePresetGuid;
    }

    /// <summary>
    /// Applies the requested enabled state by adding or removing the ECS Disabled tag.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate structural state.</param>
    /// <param name="spawnerEntity">Spawner entity to enable or disable.</param>
    /// <param name="requestedEnabled">Non-zero when the spawner should participate in gameplay queries.</param>
    private static void ApplyEnabledState(EntityManager entityManager, Entity spawnerEntity, byte requestedEnabled)
    {
        bool hasDisabledComponent = entityManager.HasComponent<Disabled>(spawnerEntity);

        if (requestedEnabled != 0)
        {
            if (hasDisabledComponent)
                entityManager.RemoveComponent<Disabled>(spawnerEntity);

            return;
        }

        if (!hasDisabledComponent)
            entityManager.AddComponent<Disabled>(spawnerEntity);
    }

    /// <summary>
    /// Marks disabled spawners ready so transition readiness does not wait for pools that are intentionally skipped.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write mutable spawner state.</param>
    /// <param name="spawnerEntity">Spawner entity being disabled by the runtime override.</param>
    /// <param name="requestedEnabled">Non-zero when the spawner remains active.</param>
    private static void MarkDisabledSpawnerReady(EntityManager entityManager, Entity spawnerEntity, byte requestedEnabled)
    {
        if (requestedEnabled != 0)
            return;

        EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);
        spawnerState.Initialized = 1;
        spawnerState.AliveCount = 0;
        spawnerState.StartTime = 0f;
        spawnerState.StartTimeInitialized = 0;
        entityManager.SetComponentData(spawnerEntity, spawnerState);
    }
    #endregion

    #region Variant Copy
    /// <summary>
    /// Copies one pre-baked wave-preset variant into the active spawner buffers.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate buffers.</param>
    /// <param name="spawnerEntity">Spawner entity receiving the variant.</param>
    /// <param name="requestedWavePresetGuid">Wave preset GUID requested by the runtime tool.</param>
    /// <returns>True when a matching variant was applied, otherwise false.</returns>
    private static bool TryApplyWavePresetVariant(EntityManager entityManager,
                                                  Entity spawnerEntity,
                                                  FixedString64Bytes requestedWavePresetGuid)
    {
        DynamicBuffer<EnemySpawnerWavePresetVariantElement> variants = entityManager.GetBuffer<EnemySpawnerWavePresetVariantElement>(spawnerEntity);
        int variantIndex;

        if (!TryFindVariant(variants, requestedWavePresetGuid, out variantIndex))
            return false;

        EnemySpawnerWavePresetVariantElement variant = variants[variantIndex];
        DestroyMappedPools(entityManager, spawnerEntity);
        CopyDefinitions(entityManager, spawnerEntity, variant);
        CopyEvents(entityManager, spawnerEntity, variant);
        CopyRequirements(entityManager, spawnerEntity, variant);
        ResetSpawnerState(entityManager, spawnerEntity, variant);
        return true;
    }

    /// <summary>
    /// Finds the variant slice associated with one wave preset GUID.
    /// </summary>
    /// <param name="variants">Variant table baked on the spawner.</param>
    /// <param name="requestedWavePresetGuid">Wave preset GUID requested by the runtime tool.</param>
    /// <param name="variantIndex">Resolved variant index when found.</param>
    /// <returns>True when a matching variant exists, otherwise false.</returns>
    private static bool TryFindVariant(DynamicBuffer<EnemySpawnerWavePresetVariantElement> variants,
                                       FixedString64Bytes requestedWavePresetGuid,
                                       out int variantIndex)
    {
        for (int index = 0; index < variants.Length; index++)
        {
            if (!variants[index].WavePresetGuid.Equals(requestedWavePresetGuid))
                continue;

            variantIndex = index;
            return true;
        }

        variantIndex = -1;
        return false;
    }

    /// <summary>
    /// Destroys old pools owned by a spawner before replacing its prefab requirements.
    /// </summary>
    /// <param name="entityManager">Entity manager used to destroy pool and pooled enemy entities.</param>
    /// <param name="spawnerEntity">Spawner whose pool map should be cleared.</param>
    private static void DestroyMappedPools(EntityManager entityManager, Entity spawnerEntity)
    {
        DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            Entity poolEntity = poolMap[mapIndex].PoolEntity;

            if (poolEntity == Entity.Null || !entityManager.Exists(poolEntity))
                continue;

            if (entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            {
                DynamicBuffer<EnemyPoolElement> pooledEnemies = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

                for (int enemyIndex = 0; enemyIndex < pooledEnemies.Length; enemyIndex++)
                {
                    Entity enemyEntity = pooledEnemies[enemyIndex].EnemyEntity;

                    if (enemyEntity != Entity.Null && entityManager.Exists(enemyEntity))
                        entityManager.DestroyEntity(enemyEntity);
                }
            }

            entityManager.DestroyEntity(poolEntity);
        }

        poolMap.Clear();
    }

    /// <summary>
    /// Copies variant wave definitions and recreates matching runtime state entries.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate buffers.</param>
    /// <param name="spawnerEntity">Spawner receiving definitions.</param>
    /// <param name="variant">Variant slice metadata.</param>
    private static void CopyDefinitions(EntityManager entityManager,
                                        Entity spawnerEntity,
                                        EnemySpawnerWavePresetVariantElement variant)
    {
        DynamicBuffer<EnemySpawnerWavePresetVariantDefinitionElement> sourceDefinitions = entityManager.GetBuffer<EnemySpawnerWavePresetVariantDefinitionElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveDefinitionElement> targetDefinitions = entityManager.GetBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> targetRuntime = entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);
        targetDefinitions.Clear();
        targetRuntime.Clear();

        for (int definitionOffset = 0; definitionOffset < variant.DefinitionCount; definitionOffset++)
        {
            int sourceIndex = variant.FirstDefinitionIndex + definitionOffset;

            if (sourceIndex < 0 || sourceIndex >= sourceDefinitions.Length)
                break;

            EnemySpawnerWavePresetVariantDefinitionElement sourceDefinition = sourceDefinitions[sourceIndex];
            targetDefinitions.Add(new EnemySpawnerWaveDefinitionElement
            {
                StartMode = sourceDefinition.StartMode,
                StartDelaySeconds = sourceDefinition.StartDelaySeconds,
                SpawnDurationSeconds = sourceDefinition.SpawnDurationSeconds,
                MaximumSpawnWarningLeadTimeSeconds = sourceDefinition.MaximumSpawnWarningLeadTimeSeconds,
                FirstEventIndex = sourceDefinition.FirstEventIndex,
                EventCount = sourceDefinition.EventCount
            });
            targetRuntime.Add(CreateDefaultWaveRuntime());
        }
    }

    /// <summary>
    /// Copies exact spawn events from one variant into the active event buffer.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate buffers.</param>
    /// <param name="spawnerEntity">Spawner receiving events.</param>
    /// <param name="variant">Variant slice metadata.</param>
    private static void CopyEvents(EntityManager entityManager,
                                   Entity spawnerEntity,
                                   EnemySpawnerWavePresetVariantElement variant)
    {
        DynamicBuffer<EnemySpawnerWavePresetVariantEventElement> sourceEvents = entityManager.GetBuffer<EnemySpawnerWavePresetVariantEventElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveEventElement> targetEvents = entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);
        targetEvents.Clear();

        for (int eventOffset = 0; eventOffset < variant.EventCount; eventOffset++)
        {
            int sourceIndex = variant.FirstEventIndex + eventOffset;

            if (sourceIndex < 0 || sourceIndex >= sourceEvents.Length)
                break;

            EnemySpawnerWavePresetVariantEventElement sourceEvent = sourceEvents[sourceIndex];
            targetEvents.Add(new EnemySpawnerWaveEventElement
            {
                WaveIndex = sourceEvent.WaveIndex,
                RelativeTime = sourceEvent.RelativeTime,
                LocalSpawnPosition = sourceEvent.LocalSpawnPosition,
                PrefabEntity = sourceEvent.PrefabEntity,
                ReservedEnemyEntity = Entity.Null,
                HasSpawnWarningOverride = sourceEvent.HasSpawnWarningOverride,
                SpawnWarningOverride = sourceEvent.SpawnWarningOverride
            });
        }
    }

    /// <summary>
    /// Copies prefab requirements from one variant into the active requirement buffer.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate buffers.</param>
    /// <param name="spawnerEntity">Spawner receiving requirements.</param>
    /// <param name="variant">Variant slice metadata.</param>
    private static void CopyRequirements(EntityManager entityManager,
                                         Entity spawnerEntity,
                                         EnemySpawnerWavePresetVariantElement variant)
    {
        DynamicBuffer<EnemySpawnerWavePresetVariantRequirementElement> sourceRequirements = entityManager.GetBuffer<EnemySpawnerWavePresetVariantRequirementElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerPrefabRequirementElement> targetRequirements = entityManager.GetBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity);
        targetRequirements.Clear();

        for (int requirementOffset = 0; requirementOffset < variant.RequirementCount; requirementOffset++)
        {
            int sourceIndex = variant.FirstRequirementIndex + requirementOffset;

            if (sourceIndex < 0 || sourceIndex >= sourceRequirements.Length)
                break;

            EnemySpawnerWavePresetVariantRequirementElement sourceRequirement = sourceRequirements[sourceIndex];
            targetRequirements.Add(new EnemySpawnerPrefabRequirementElement
            {
                PrefabEntity = sourceRequirement.PrefabEntity,
                TotalPlannedCount = sourceRequirement.TotalPlannedCount
            });
        }
    }

    /// <summary>
    /// Resets mutable spawner state after active buffers are replaced.
    /// </summary>
    /// <param name="entityManager">Entity manager used to write components.</param>
    /// <param name="spawnerEntity">Spawner receiving the reset.</param>
    /// <param name="variant">Variant metadata used to update aggregate spawner configuration.</param>
    private static void ResetSpawnerState(EntityManager entityManager,
                                          Entity spawnerEntity,
                                          EnemySpawnerWavePresetVariantElement variant)
    {
        EnemySpawner spawner = entityManager.GetComponentData<EnemySpawner>(spawnerEntity);
        spawner.MaximumSpawnDistanceFromCenter = math.max(0f, variant.MaximumSpawnDistanceFromCenter);
        spawner.TotalPlannedEnemyCount = math.max(0, variant.TotalPlannedEnemyCount);
        entityManager.SetComponentData(spawnerEntity, spawner);
        entityManager.SetComponentData(spawnerEntity, new EnemySpawnerState
        {
            StartTime = 0f,
            AliveCount = 0,
            Initialized = 0,
            StartTimeInitialized = 0
        });
    }

    /// <summary>
    /// Creates default mutable state for one wave after a runtime variant copy.
    /// </summary>
    /// <returns>Default wave runtime state.</returns>
    private static EnemySpawnerWaveRuntimeElement CreateDefaultWaveRuntime()
    {
        return new EnemySpawnerWaveRuntimeElement
        {
            ScheduledStartTime = 0f,
            SpawnStartTime = 0f,
            SpawnEndTime = 0f,
            CompletionTime = 0f,
            FirstKillTime = 0f,
            NextEventIndex = 0,
            NextWarningEventIndex = 0,
            AliveCount = 0,
            SpawnedCount = 0,
            StartScheduled = 0,
            Started = 0,
            SpawnFinished = 0,
            Completed = 0,
            FirstKillRegistered = 0
        };
    }
    #endregion

    #endregion
}
