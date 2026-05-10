using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Builds and initializes experience drop pools from enemy drop configurations.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(EnemySystemGroup))]
public partial struct EnemyExperienceDropPoolInitializeSystem : ISystem
{
    #region Constants
    private const int MaxDropPoolPrewarmEntitiesPerFrame = 1024;
    private const int MaxDropPoolPrewarmEntitiesPerPoolPerFrame = 256;
    #endregion

    #region Fields
    private EntityQuery registryQuery;
    private EntityQuery spawnerQuery;
    private EntityQuery dropPoolQuery;
    private EntityQuery dropEntityQuery;
    private byte poolSettingsApplied;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates registry singleton dependencies required by drop pooling.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        registryQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAll<EnemyExperienceDropPoolRegistry>()
                            .Build(ref state);
        spawnerQuery = new EntityQueryBuilder(Allocator.Temp)
                           .WithAll<EnemySpawner, EnemySpawnerPrefabRequirementElement>()
                           .Build(ref state);
        dropPoolQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAll<EnemyExperienceDropPoolState>()
                            .Build(ref state);
        dropEntityQuery = new EntityQueryBuilder(Allocator.Temp)
                              .WithAll<EnemyExperienceDrop>()
                              .Build(ref state);
        poolSettingsApplied = 0;
        state.RequireForUpdate(spawnerQuery);
        EnsureRegistrySingleton(ref state);

        if (TryGetRegistryEntity(out Entity registryEntity))
        {
            EnemyExperienceDropPoolRegistry registry = state.EntityManager.GetComponentData<EnemyExperienceDropPoolRegistry>(registryEntity);

            if (registry.Initialized != 0)
                poolSettingsApplied = 1;
        }
    }

    /// <summary>
    /// Initializes drop pools once by aggregating prefab demand from all enemy spawners.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        EnsureRegistrySingleton(ref state);

        if (TryGetRegistryEntity(out Entity registryEntity) == false)
            return;

        EntityManager entityManager = state.EntityManager;
        EnemyExperienceDropPoolRegistry registry = entityManager.GetComponentData<EnemyExperienceDropPoolRegistry>(registryEntity);
        Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab = BuildPoolSettingsByPrefab(ref state);
        uint sourceHash = ComputePoolSettingsHash(poolSettingsByPrefab);
        bool registryMatchesPoolSettings = RegistryMatchesPoolSettings(entityManager,
                                                                      registryEntity,
                                                                      poolSettingsByPrefab);
        bool requiresRegistryRefresh = poolSettingsApplied == 0 ||
                                       registry.SourceHash != sourceHash ||
                                       !registryMatchesPoolSettings;

        if (requiresRegistryRefresh)
        {
            ResetRegistry(entityManager,
                          registryEntity,
                          dropPoolQuery,
                          dropEntityQuery);
            ApplyPoolSettings(entityManager, registryEntity, poolSettingsByPrefab);
            poolSettingsApplied = 1;
            registry = entityManager.GetComponentData<EnemyExperienceDropPoolRegistry>(registryEntity);
            registry.Initialized = 0;
            registry.SourceHash = sourceHash;
            entityManager.SetComponentData(registryEntity, registry);
        }

        if (registry.Initialized != 0)
            return;

        if (InitializePools(entityManager, registryEntity, MaxDropPoolPrewarmEntitiesPerFrame) == false)
            return;

        registry.Initialized = 1;
        registry.SourceHash = sourceHash;
        entityManager.SetComponentData(registryEntity, registry);
    }
    #endregion

    #region Setup
    /// <summary>
    /// Ensures that a singleton registry entity for experience drop pools exists.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    private void EnsureRegistrySingleton(ref SystemState state)
    {
        if (registryQuery.IsEmptyIgnoreFilter == false)
            return;

        Entity registryEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(registryEntity, new EnemyExperienceDropPoolRegistry
        {
            Initialized = 0,
            SourceHash = 0u
        });
        state.EntityManager.AddBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);
    }

    /// <summary>
    /// Tries to resolve the singleton registry entity from the cached registry query.
    /// </summary>
    /// <param name="registryEntity">Resolved registry entity when present; Entity.Null otherwise.</param>
    /// <returns>True when the registry exists; otherwise false.<returns>
    private bool TryGetRegistryEntity(out Entity registryEntity)
    {
        if (registryQuery.IsEmptyIgnoreFilter)
        {
            registryEntity = Entity.Null;
            return false;
        }

        registryEntity = registryQuery.GetSingletonEntity();
        return true;
    }
    #endregion

    #region Aggregation
    /// <summary>
    /// Builds the current per-prefab drop-pool settings from every active enemy spawner in the world.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Current per-prefab build settings keyed by experience-drop prefab entity.<returns>
    private Dictionary<Entity, PoolBuildSettings> BuildPoolSettingsByPrefab(ref SystemState state)
    {
        int spawnerCount = spawnerQuery.CalculateEntityCountWithoutFiltering();
        int dictionaryCapacity = math.max(4, spawnerCount * 2);
        Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab = new Dictionary<Entity, PoolBuildSettings>(dictionaryCapacity);
        BufferLookup<EnemySpawnerPrefabRequirementElement> requirementLookup = SystemAPI.GetBufferLookup<EnemySpawnerPrefabRequirementElement>(true);

        // Aggregate every authored spawner contribution into one per-prefab pool plan.
        foreach ((RefRO<EnemySpawner> spawner,
                  Entity spawnerEntity)
                 in SystemAPI.Query<RefRO<EnemySpawner>>()
                             .WithEntityAccess())
        {
            if (!requirementLookup.HasBuffer(spawnerEntity))
                continue;

            AggregateSpawnerPoolSettings(state.EntityManager,
                                         spawner.ValueRO,
                                         requirementLookup[spawnerEntity],
                                         poolSettingsByPrefab);
        }

        return poolSettingsByPrefab;
    }

    /// <summary>
    /// Aggregates pool sizing requirements from a single spawner into per-prefab build settings.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read spawner and prefab data.</param>
    /// <param name="spawner">Spawner source configuration.</param>
    /// <param name="poolSettingsByPrefab">Accumulated pool build settings grouped by prefab entity.</param>

    private static void AggregateSpawnerPoolSettings(EntityManager entityManager,
                                                     EnemySpawner spawner,
                                                     DynamicBuffer<EnemySpawnerPrefabRequirementElement> requirements,
                                                     Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab)
    {
        for (int requirementIndex = 0; requirementIndex < requirements.Length; requirementIndex++)
        {
            EnemySpawnerPrefabRequirementElement requirement = requirements[requirementIndex];
            Entity enemyPrefab = requirement.PrefabEntity;

            if (enemyPrefab == Entity.Null)
                continue;

            if (!entityManager.Exists(enemyPrefab))
                continue;

            if (!entityManager.HasComponent<EnemyDropItemsConfig>(enemyPrefab))
                continue;

            EnemyDropItemsConfig dropItemsConfig = entityManager.GetComponentData<EnemyDropItemsConfig>(enemyPrefab);

            if (dropItemsConfig.HasExperienceDrops == 0 || dropItemsConfig.ExperienceModuleCount <= 0)
                continue;

            if (!entityManager.HasBuffer<EnemyExperienceDropModuleElement>(enemyPrefab))
                continue;

            DynamicBuffer<EnemyExperienceDropModuleElement> experienceModules = entityManager.GetBuffer<EnemyExperienceDropModuleElement>(enemyPrefab);

            if (!entityManager.HasBuffer<EnemyExperienceDropDefinitionElement>(enemyPrefab))
                continue;

            DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions = entityManager.GetBuffer<EnemyExperienceDropDefinitionElement>(enemyPrefab);

            if (experienceModules.Length <= 0 || definitions.Length <= 0)
                continue;

            for (int moduleIndex = 0; moduleIndex < experienceModules.Length; moduleIndex++)
            {
                EnemyExperienceDropModuleElement experienceModule = experienceModules[moduleIndex];
                int definitionCount = math.max(0, experienceModule.DefinitionCount);

                if (definitionCount <= 0 || experienceModule.MaximumTotalExperienceDrop <= 0f)
                    continue;

                int estimatedDropsPerDeath = math.max(1, experienceModule.EstimatedDropsPerDeath);

                if (experienceModule.EstimatedDropsPerDeath <= 0)
                {
                    float deliveredExperience;
                    float absoluteError;
                    estimatedDropsPerDeath = EnemyExperienceDropDistributionUtility.EstimateDropsPerDeath(definitions,
                                                                                                           experienceModule.DefinitionStartIndex,
                                                                                                           definitionCount,
                                                                                                           experienceModule.MaximumTotalExperienceDrop,
                                                                                                           experienceModule.Distribution,
                                                                                                           out deliveredExperience,
                                                                                                           out absoluteError);
                    estimatedDropsPerDeath = math.max(1, estimatedDropsPerDeath);
                }

                int initialCapacityPerPrefab = EstimateInitialCapacity(requirement.TotalPlannedCount, estimatedDropsPerDeath);
                int expandBatchPerPrefab = EstimateExpandBatch(spawner.ExpandBatchPerPrefab, estimatedDropsPerDeath);
                int definitionStartIndex = math.max(0, experienceModule.DefinitionStartIndex);
                int definitionEndIndex = math.min(definitions.Length, definitionStartIndex + definitionCount);

                for (int definitionIndex = definitionStartIndex; definitionIndex < definitionEndIndex; definitionIndex++)
                {
                    EnemyExperienceDropDefinitionElement definition = definitions[definitionIndex];

                    if (definition.PrefabEntity == Entity.Null)
                        continue;

                    if (definition.ExperienceAmount <= 0f)
                        continue;

                    PoolBuildSettings currentSettings;

                    if (poolSettingsByPrefab.TryGetValue(definition.PrefabEntity, out currentSettings))
                    {
                        currentSettings.InitialCapacity += initialCapacityPerPrefab;

                        if (expandBatchPerPrefab > currentSettings.ExpandBatch)
                            currentSettings.ExpandBatch = expandBatchPerPrefab;

                        poolSettingsByPrefab[definition.PrefabEntity] = currentSettings;
                        continue;
                    }

                    poolSettingsByPrefab[definition.PrefabEntity] = new PoolBuildSettings
                    {
                        InitialCapacity = initialCapacityPerPrefab,
                        ExpandBatch = expandBatchPerPrefab
                    };
                }
            }
        }
    }

    /// <summary>
    /// Computes the initial pool capacity for a prefab from max alive enemies and expected drop count.
    /// </summary>
    /// <param name="plannedEnemyCount">Maximum planned instances of the enemy prefab for the spawner.</param>
    /// <param name="estimatedDropsPerDeath">Estimated drop instances generated per enemy death.</param>
    /// <returns>Resolved initial pool capacity clamped to a safe runtime range.<returns>
    private static int EstimateInitialCapacity(int plannedEnemyCount, int estimatedDropsPerDeath)
    {
        int sanitizedPlannedEnemyCount = math.max(1, plannedEnemyCount);
        int sanitizedEstimatedDrops = math.max(1, estimatedDropsPerDeath);
        float baseCapacity = sanitizedPlannedEnemyCount * sanitizedEstimatedDrops;
        float safetyCapacity = baseCapacity * 0.1f;
        int resolvedCapacity = (int)math.ceil(baseCapacity + safetyCapacity) + 4;
        return math.clamp(math.max(4, resolvedCapacity), 4, 262144);
    }

    /// <summary>
    /// Computes the pool expansion batch size from spawn burst size and expected drop count.
    /// </summary>
    /// <param name="expandBatchPerPrefab">Enemy pool expansion batch configured on the spawner.</param>
    /// <param name="estimatedDropsPerDeath">Estimated drop instances generated per enemy death.</param>
    /// <returns>Resolved expansion batch size clamped to a safe runtime range.<returns>
    private static int EstimateExpandBatch(int expandBatchPerPrefab, int estimatedDropsPerDeath)
    {
        int sanitizedExpandBatch = math.max(1, expandBatchPerPrefab);
        int sanitizedEstimatedDrops = math.max(1, estimatedDropsPerDeath);
        float baseBatch = sanitizedExpandBatch * sanitizedEstimatedDrops * 0.5f;
        int resolvedBatch = (int)math.ceil(baseBatch) + 2;
        return math.clamp(math.max(4, resolvedBatch), 4, 16384);
    }
    #endregion

    #region Pool Setup
    /// <summary>
    /// Returns an order-independent hash of the current pool-build settings so scene reloads can invalidate stale registries.
    /// </summary>
    /// <param name="poolSettingsByPrefab">Resolved pool sizing settings grouped by prefab entity.</param>
    /// <returns>Order-independent signature of the current pool settings.<returns>
    private static uint ComputePoolSettingsHash(Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab)
    {
        uint xorHash = 0u;
        uint sumHash = 0u;

        foreach (KeyValuePair<Entity, PoolBuildSettings> pair in poolSettingsByPrefab)
        {
            PoolBuildSettings settings = pair.Value;
            uint entryHash = math.hash(new int4(pair.Key.Index,
                                                pair.Key.Version,
                                                settings.InitialCapacity,
                                                settings.ExpandBatch));
            xorHash ^= entryHash;
            sumHash += entryHash * 16777619u;
        }

        return math.hash(new uint4(xorHash,
                                   sumHash,
                                   (uint)poolSettingsByPrefab.Count,
                                   2246822519u));
    }

    /// <summary>
    /// Validates that the registry still maps every current prefab requirement to a live pool entity with compatible settings.
    /// </summary>
    /// <param name="entityManager">Entity manager used to inspect existing pool entities.</param>
    /// <param name="registryEntity">Registry singleton entity that stores the pool map buffer.</param>
    /// <param name="poolSettingsByPrefab">Current pool build settings grouped by prefab entity.</param>
    /// <returns>True when the registry fully matches the current spawner-derived settings, otherwise false.<returns>
    private static bool RegistryMatchesPoolSettings(EntityManager entityManager,
                                                    Entity registryEntity,
                                                    Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab)
    {
        if (entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity) == false)
            return poolSettingsByPrefab.Count <= 0;

        DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

        if (poolMap.Length != poolSettingsByPrefab.Count)
            return false;

        foreach (KeyValuePair<Entity, PoolBuildSettings> pair in poolSettingsByPrefab)
        {
            Entity poolEntity;

            if (!EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, pair.Key, out poolEntity))
                return false;

            if (poolEntity == Entity.Null || !entityManager.Exists(poolEntity))
                return false;

            if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                return false;

            EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);

            if (poolState.PrefabEntity != pair.Key)
                return false;

            if (poolState.InitialCapacity < pair.Value.InitialCapacity)
                return false;

            if (poolState.ExpandBatch < pair.Value.ExpandBatch)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Clears all current experience-drop pools and active drop entities so the registry can be rebuilt safely for a new scene run.
    /// </summary>
    /// <param name="entityManager">Entity manager used for structural cleanup.</param>
    /// <param name="registryEntity">Registry singleton entity that stores the pool map buffer.</param>
    /// <param name="dropPoolQuery">Query selecting every experience-drop pool entity.</param>
    /// <param name="dropEntityQuery">Query selecting every spawned experience-drop entity.</param>
    /// <returns>None.<returns>
    private static void ResetRegistry(EntityManager entityManager,
                                      Entity registryEntity,
                                      EntityQuery dropPoolQuery,
                                      EntityQuery dropEntityQuery)
    {
        // Destroy all live or pooled drops first so no stale entities survive across scene reloads.
        NativeArray<Entity> dropEntities = dropEntityQuery.ToEntityArray(Allocator.Temp);

        if (dropEntities.Length > 0)
            entityManager.DestroyEntity(dropEntities);

        dropEntities.Dispose();

        // Destroy every existing pool entity so the map can be recreated from the current spawner bake.
        NativeArray<Entity> poolEntities = dropPoolQuery.ToEntityArray(Allocator.Temp);

        if (poolEntities.Length > 0)
            entityManager.DestroyEntity(poolEntities);

        poolEntities.Dispose();

        if (entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity))
            entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity).Clear();

        if (entityManager.HasComponent<EnemyExperienceDropPoolRegistry>(registryEntity))
        {
            entityManager.SetComponentData(registryEntity, new EnemyExperienceDropPoolRegistry
            {
                Initialized = 0,
                SourceHash = 0u
            });
        }
    }

    /// <summary>
    /// Applies aggregated pool settings by updating existing pools or creating new pool entities.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate pool entities.</param>
    /// <param name="registryEntity">Registry singleton entity that stores the pool map buffer.</param>
    /// <param name="poolSettingsByPrefab">Resolved pool sizing settings grouped by prefab entity.</param>

    private static void ApplyPoolSettings(EntityManager entityManager,
                                          Entity registryEntity,
                                          Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab)
    {
        foreach (KeyValuePair<Entity, PoolBuildSettings> pair in poolSettingsByPrefab)
        {
            Entity prefabEntity = pair.Key;
            PoolBuildSettings settings = pair.Value;
            DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);
            Entity poolEntity;

            if (EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, prefabEntity, out poolEntity))
            {
                if (poolEntity == Entity.Null || entityManager.Exists(poolEntity) == false)
                    continue;

                if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                    continue;

                EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);
                poolState.InitialCapacity = math.max(poolState.InitialCapacity, settings.InitialCapacity);
                poolState.ExpandBatch = math.max(poolState.ExpandBatch, settings.ExpandBatch);
                entityManager.SetComponentData(poolEntity, poolState);
                continue;
            }

            Entity createdPoolEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(createdPoolEntity, new EnemyExperienceDropPoolState
            {
                PrefabEntity = prefabEntity,
                InitialCapacity = math.max(0, settings.InitialCapacity),
                ExpandBatch = math.max(1, settings.ExpandBatch),
                Initialized = 0
            });
            entityManager.AddBuffer<EnemyExperienceDropPoolElement>(createdPoolEntity);
            DynamicBuffer<EnemyExperienceDropPoolMapElement> refreshedPoolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);
            refreshedPoolMap.Add(new EnemyExperienceDropPoolMapElement
            {
                PrefabEntity = prefabEntity,
                PoolEntity = createdPoolEntity
            });
        }
    }

    /// <summary>
    /// Performs progressive prewarm expansion for non-initialized pool entities in the registry map.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read and mutate pool entities.</param>
    /// <param name="registryEntity">Registry singleton entity that stores the pool map buffer.</param>
    /// <param name="frameBudget">Maximum pooled instances that can be prewarmed in the current frame.</param>
    /// <returns>True when all pools are initialized; otherwise false.<returns>
    private static bool InitializePools(EntityManager entityManager, Entity registryEntity, int frameBudget)
    {
        int remainingBudget = math.max(0, frameBudget);
        int poolMapLength = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity).Length;

        for (int mapIndex = 0; mapIndex < poolMapLength; mapIndex++)
        {
            DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

            if (mapIndex >= poolMap.Length)
                break;

            EnemyExperienceDropPoolMapElement mapElement = poolMap[mapIndex];
            Entity poolEntity = mapElement.PoolEntity;

            if (poolEntity == Entity.Null || entityManager.Exists(poolEntity) == false)
                continue;

            if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                continue;

            EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);

            if (poolState.Initialized != 0)
                continue;

            int initialCapacity = math.max(0, poolState.InitialCapacity);
            int currentCapacity = 0;

            if (entityManager.HasBuffer<EnemyExperienceDropPoolElement>(poolEntity))
                currentCapacity = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length;

            int remainingCapacity = initialCapacity - currentCapacity;

            if (remainingCapacity > 0)
            {
                if (remainingBudget <= 0)
                    break;

                int perPoolBudget = math.min(MaxDropPoolPrewarmEntitiesPerPoolPerFrame, remainingBudget);
                int expandCount = math.min(remainingCapacity, perPoolBudget);

                if (poolState.ExpandBatch > 0)
                    expandCount = math.min(expandCount, poolState.ExpandBatch);

                if (expandCount > 0)
                {
                    EnemyExperienceDropPoolUtility.ExpandPool(entityManager, poolEntity, poolState.PrefabEntity, expandCount);
                    remainingBudget -= expandCount;
                }

                int refreshedCapacity = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length;
                remainingCapacity = initialCapacity - refreshedCapacity;

                if (remainingCapacity > 0)
                    continue;
            }

            poolState.Initialized = 1;
            entityManager.SetComponentData(poolEntity, poolState);
        }

        return AreAllPoolsInitialized(entityManager, registryEntity);
    }

    private static bool AreAllPoolsInitialized(EntityManager entityManager, Entity registryEntity)
    {
        if (entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity) == false)
            return true;

        DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            Entity poolEntity = poolMap[mapIndex].PoolEntity;

            if (poolEntity == Entity.Null || entityManager.Exists(poolEntity) == false)
                continue;

            if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                continue;

            if (entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity).Initialized == 0)
                return false;
        }

        return true;
    }
    #endregion

    #endregion

    #region Nested Types
    private struct PoolBuildSettings
    {
        public int InitialCapacity;
        public int ExpandBatch;
    }
    #endregion
}
