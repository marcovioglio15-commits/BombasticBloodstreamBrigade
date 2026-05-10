using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Initializes enemy pools for wave-based spawners by creating one pool per referenced prefab.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(EnemySystemGroup))]
public partial struct EnemyPoolInitializeSystem : ISystem
{
    #region Constants
    private const int MaxInitialPoolEntitiesPerFrame = 1024;
    private const int MaxInitialPoolEntitiesPerPoolPerFrame = 256;
    #endregion

    #region Fields
    private EntityQuery initializeQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the query used to find spawners that still need pool setup.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        initializeQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemySpawner, EnemySpawnerState, EnemySpawnerPrefabRequirementElement, EnemySpawnerPrefabPoolMapElement>()
            .Build(ref state);
        state.RequireForUpdate(initializeQuery);
    }

    /// <summary>
    /// Creates missing pool entities and progressively prewarms them within a fixed frame budget.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        int remainingInitializationBudget = MaxInitialPoolEntitiesPerFrame;
        int pendingInitializationCount = 0;
        NativeArray<Entity> spawnerEntities = initializeQuery.ToEntityArray(Allocator.Temp);

        try
        {
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            {
                Entity spawnerEntity = spawnerEntities[spawnerIndex];

                if (!entityManager.Exists(spawnerEntity))
                    continue;

                EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

                if (spawnerState.Initialized != 0)
                    continue;

                EnemySpawner spawner = entityManager.GetComponentData<EnemySpawner>(spawnerEntity);
                int requirementCount = entityManager.GetBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity).Length;
                EnsurePoolEntities(entityManager, spawnerEntity, spawner, requirementCount);

                bool allPoolsReady = true;

                for (int requirementIndex = 0; requirementIndex < requirementCount; requirementIndex++)
                {
                    DynamicBuffer<EnemySpawnerPrefabRequirementElement> requirements = entityManager.GetBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity);
                    EnemySpawnerPrefabRequirementElement requirement = requirements[requirementIndex];

                    if (requirement.PrefabEntity == Entity.Null)
                        continue;

                    if (!entityManager.Exists(requirement.PrefabEntity))
                    {
                        allPoolsReady = false;
                        continue;
                    }

                    Entity poolEntity;
                    DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

                    if (!TryGetPoolEntity(poolMap, requirement.PrefabEntity, out poolEntity))
                    {
                        allPoolsReady = false;
                        continue;
                    }

                    if (!entityManager.Exists(poolEntity))
                    {
                        allPoolsReady = false;
                        continue;
                    }

                    if (!entityManager.HasComponent<EnemyPoolState>(poolEntity))
                    {
                        allPoolsReady = false;
                        continue;
                    }

                    if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
                    {
                        allPoolsReady = false;
                        continue;
                    }

                    EnemyPoolState poolState = entityManager.GetComponentData<EnemyPoolState>(poolEntity);
                    DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);
                    int remainingPoolCapacity = poolState.InitialCapacity - poolBuffer.Length;

                    if (remainingPoolCapacity > 0)
                    {
                        allPoolsReady = false;

                        if (remainingInitializationBudget <= 0)
                            continue;

                        int cappedExpandCount = math.min(remainingPoolCapacity, MaxInitialPoolEntitiesPerPoolPerFrame);
                        int expandCount = math.min(cappedExpandCount, remainingInitializationBudget);

                        if (expandCount <= 0)
                            continue;

                        EnemyPoolUtility.ExpandPool(entityManager,
                                                    poolEntity,
                                                    spawnerEntity,
                                                    requirement.PrefabEntity,
                                                    expandCount);
                        remainingInitializationBudget -= expandCount;
                        remainingPoolCapacity = poolState.InitialCapacity - entityManager.GetBuffer<EnemyPoolElement>(poolEntity).Length;
                    }

                    byte shouldBeInitialized = remainingPoolCapacity <= 0 ? (byte)1 : (byte)0;

                    if (poolState.Initialized != shouldBeInitialized)
                    {
                        poolState.Initialized = shouldBeInitialized;
                        entityManager.SetComponentData(poolEntity, poolState);
                    }

                    if (remainingPoolCapacity > 0)
                        allPoolsReady = false;
                }

                if (!allPoolsReady)
                {
                    pendingInitializationCount++;
                    continue;
                }

                FinalizeSpawnerInitialization(entityManager, spawnerEntity, spawnerState, elapsedTime);
            }
        }
        finally
        {
            if (spawnerEntities.IsCreated)
                spawnerEntities.Dispose();
        }

    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates missing runtime pool entities for each unique prefab requirement of the spawner.
    /// entityManager: Entity manager used to create pool entities.
    /// spawnerEntity: Spawner that owns the requirements.
    /// spawner: Spawner configuration containing generic pool settings.
    /// requirementCount: Stable requirement count snapshot used while structural changes are occurring.
    /// returns None.
    /// </summary>
    private static void EnsurePoolEntities(EntityManager entityManager,
                                           Entity spawnerEntity,
                                           EnemySpawner spawner,
                                           int requirementCount)
    {
        for (int requirementIndex = 0; requirementIndex < requirementCount; requirementIndex++)
        {
            DynamicBuffer<EnemySpawnerPrefabRequirementElement> requirements = entityManager.GetBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity);
            EnemySpawnerPrefabRequirementElement requirement = requirements[requirementIndex];

            if (requirement.PrefabEntity == Entity.Null)
                continue;

            if (!entityManager.Exists(requirement.PrefabEntity))
                continue;

            Entity poolEntity;
            DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

            if (TryGetPoolEntity(poolMap, requirement.PrefabEntity, out poolEntity))
                continue;

            poolEntity = entityManager.CreateEntity();
            int initialCapacity = math.max(0, math.min(spawner.InitialPoolCapacityPerPrefab, requirement.TotalPlannedCount));
            int expandBatch = math.max(1, spawner.ExpandBatchPerPrefab);
            entityManager.AddComponentData(poolEntity, new EnemyPoolState
            {
                PrefabEntity = requirement.PrefabEntity,
                InitialCapacity = initialCapacity,
                ExpandBatch = expandBatch,
                Initialized = initialCapacity <= 0 ? (byte)1 : (byte)0
            });
            entityManager.AddBuffer<EnemyPoolElement>(poolEntity);
            poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);
            poolMap.Add(new EnemySpawnerPrefabPoolMapElement
            {
                PrefabEntity = requirement.PrefabEntity,
                PoolEntity = poolEntity
            });
        }
    }

    /// <summary>
    /// Resolves the pool entity associated with one prefab in the spawner map buffer.
    /// poolMap: Prefab-to-pool map buffer stored on the spawner.
    /// prefabEntity: Referenced prefab to resolve.
    /// poolEntity: Resolved pool entity when found.
    /// returns True when a mapping exists, otherwise false.
    /// </summary>
    private static bool TryGetPoolEntity(DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap,
                                         Entity prefabEntity,
                                         out Entity poolEntity)
    {
        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            EnemySpawnerPrefabPoolMapElement mapEntry = poolMap[mapIndex];

            if (mapEntry.PrefabEntity != prefabEntity)
                continue;

            poolEntity = mapEntry.PoolEntity;
            return true;
        }

        poolEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Marks one spawner as fully initialized and records the logical spawner start time.
    /// entityManager: Entity manager used to write the state.
    /// spawnerEntity: Spawner whose initialization is complete.
    /// spawnerState: Current mutable spawner state.
    /// elapsedTime: Current elapsed world time.
    /// returns None.
    /// </summary>
    private static void FinalizeSpawnerInitialization(EntityManager entityManager,
                                                      Entity spawnerEntity,
                                                      EnemySpawnerState spawnerState,
                                                      float elapsedTime)
    {
        EnemySpawnerState nextState = spawnerState;
        nextState.Initialized = 1;
        nextState.StartTime = elapsedTime;
        nextState.AliveCount = math.max(0, nextState.AliveCount);
        entityManager.SetComponentData(spawnerEntity, nextState);
    }
    #endregion

    #endregion
}
