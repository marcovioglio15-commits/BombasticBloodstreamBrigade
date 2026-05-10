using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Provides helper methods for experience drop pooling lifecycle.
/// </summary>
public static class EnemyExperienceDropPoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -12000f, 0f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Expands one experience drop pool by instantiating additional prefab instances.
    /// </summary>
    /// <param name="entityManager">EntityManager used for structural changes.</param>
    /// <param name="poolEntity">Pool entity receiving the instantiated drops.</param>
    /// <param name="prefabEntity">Drop prefab entity to instantiate.</param>
    /// <param name="count">Amount of instances to create.</param>

    public static void ExpandPool(EntityManager entityManager, Entity poolEntity, Entity prefabEntity, int count)
    {
        if (count <= 0)
            return;

        if (poolEntity == Entity.Null || prefabEntity == Entity.Null)
            return;

        if (entityManager.Exists(poolEntity) == false || entityManager.Exists(prefabEntity) == false)
            return;

        if (entityManager.HasBuffer<EnemyExperienceDropPoolElement>(poolEntity) == false)
            return;

        NativeArray<Entity> spawnedDrops = new NativeArray<Entity>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        try
        {
            entityManager.Instantiate(prefabEntity, spawnedDrops);

            for (int index = 0; index < spawnedDrops.Length; index++)
            {
                Entity dropEntity = spawnedDrops[index];
                EnsureDropComponents(entityManager, dropEntity);
                ParkDrop(entityManager, dropEntity);
                entityManager.SetComponentEnabled<EnemyExperienceDropActive>(dropEntity, false);
            }

            DynamicBuffer<EnemyExperienceDropPoolElement> poolElements = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity);

            for (int index = 0; index < spawnedDrops.Length; index++)
                poolElements.Add(new EnemyExperienceDropPoolElement
                {
                    DropEntity = spawnedDrops[index]
                });
        }
        finally
        {
            if (spawnedDrops.IsCreated)
                spawnedDrops.Dispose();
        }
    }

    /// <summary>
    /// Ensures required runtime components exist on one pooled drop entity.
    /// </summary>
    /// <param name="entityManager">EntityManager used for component checks and additions.</param>
    /// <param name="dropEntity">Drop entity to sanitize.</param>

    public static void EnsureDropComponents(EntityManager entityManager, Entity dropEntity)
    {
        if (dropEntity == Entity.Null)
            return;

        if (entityManager.Exists(dropEntity) == false)
            return;

        if (entityManager.HasComponent<LocalTransform>(dropEntity) == false)
            entityManager.AddComponentData(dropEntity, LocalTransform.Identity);

        if (entityManager.HasComponent<EnemyExperienceDrop>(dropEntity) == false)
            entityManager.AddComponentData(dropEntity, default(EnemyExperienceDrop));

        if (entityManager.HasComponent<EnemyExperienceDropActive>(dropEntity) == false)
            entityManager.AddComponent<EnemyExperienceDropActive>(dropEntity);
    }

    /// <summary>
    /// Attempts to acquire one drop entity from a pool, expanding it when empty.
    /// </summary>
    /// <param name="entityManager">EntityManager used for pool reads and writes.</param>
    /// <param name="poolEntity">Pool entity that stores pooled drop references.</param>
    /// <param name="dropEntity">Acquired drop entity when successful.</param>
    /// <returns>True when one drop entity is acquired, otherwise false.<returns>
    public static bool TryAcquireDrop(EntityManager entityManager, Entity poolEntity, out Entity dropEntity)
    {
        int unrestrictedRuntimeExpandBudget = int.MaxValue;
        return TryAcquireDrop(entityManager, poolEntity, out dropEntity, ref unrestrictedRuntimeExpandBudget);
    }

    public static bool TryAcquireDrop(EntityManager entityManager,
                                      Entity poolEntity,
                                      out Entity dropEntity,
                                      ref int runtimeExpandBudget)
    {
        dropEntity = Entity.Null;

        if (poolEntity == Entity.Null)
            return false;

        if (entityManager.Exists(poolEntity) == false)
            return false;

        if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
            return false;

        if (entityManager.HasBuffer<EnemyExperienceDropPoolElement>(poolEntity) == false)
            return false;

        EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);
        DynamicBuffer<EnemyExperienceDropPoolElement> poolElements = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity);

        if (poolElements.Length <= 0)
        {
            if (runtimeExpandBudget <= 0)
                return false;

            int expandCount = math.min(math.max(1, poolState.ExpandBatch), runtimeExpandBudget);
            ExpandPool(entityManager, poolEntity, poolState.PrefabEntity, expandCount);
            runtimeExpandBudget -= expandCount;
            poolElements = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity);
        }

        if (poolElements.Length <= 0)
            return false;

        int lastIndex = poolElements.Length - 1;
        dropEntity = poolElements[lastIndex].DropEntity;
        poolElements.RemoveAt(lastIndex);

        if (dropEntity == Entity.Null || entityManager.Exists(dropEntity) == false)
        {
            dropEntity = Entity.Null;
            return false;
        }

        EnsureDropComponents(entityManager, dropEntity);
        return true;
    }

    /// <summary>
    /// Returns one drop entity back to its pool and parks it out of gameplay space.
    /// </summary>
    /// <param name="entityManager">EntityManager used for pool writes.</param>
    /// <param name="poolEntity">Target pool entity receiving the drop reference.</param>
    /// <param name="dropEntity">Drop entity to release.</param>

    public static void ReleaseDrop(EntityManager entityManager, Entity poolEntity, Entity dropEntity)
    {
        if (dropEntity == Entity.Null)
            return;

        if (entityManager.Exists(dropEntity) == false)
            return;

        EnsureDropComponents(entityManager, dropEntity);
        ParkDrop(entityManager, dropEntity);
        entityManager.SetComponentEnabled<EnemyExperienceDropActive>(dropEntity, false);

        if (poolEntity == Entity.Null)
            return;

        if (entityManager.Exists(poolEntity) == false)
            return;

        if (entityManager.HasBuffer<EnemyExperienceDropPoolElement>(poolEntity) == false)
            return;

        DynamicBuffer<EnemyExperienceDropPoolElement> poolElements = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity);
        poolElements.Add(new EnemyExperienceDropPoolElement
        {
            DropEntity = dropEntity
        });
    }

    /// <summary>
    /// Parks one drop entity in an off-screen position while preserving rotation and scale.
    /// </summary>
    /// <param name="entityManager">EntityManager used for transform writes.</param>
    /// <param name="dropEntity">Drop entity to park.</param>

    public static void ParkDrop(EntityManager entityManager, Entity dropEntity)
    {
        if (dropEntity == Entity.Null)
            return;

        if (entityManager.Exists(dropEntity) == false)
            return;

        if (entityManager.HasComponent<LocalTransform>(dropEntity) == false)
            return;

        LocalTransform transform = entityManager.GetComponentData<LocalTransform>(dropEntity);
        transform.Position = ParkingPosition;
        entityManager.SetComponentData(dropEntity, transform);

        if (entityManager.HasComponent<LocalToWorld>(dropEntity))
        {
            entityManager.SetComponentData(dropEntity, new LocalToWorld
            {
                Value = transform.ToMatrix()
            });
        }
    }

    /// <summary>
    /// Resolves one pool entity from registry map by prefab entity.
    /// </summary>
    /// <param name="poolMap">Registry map buffer containing prefab-pool pairs.</param>
    /// <param name="prefabEntity">Drop prefab entity key.</param>
    /// <param name="poolEntity">Resolved pool entity when found.</param>
    /// <returns>True when a matching pool is found, otherwise false.<returns>
    public static bool TryResolvePoolEntity(DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap,
                                            Entity prefabEntity,
                                            out Entity poolEntity)
    {
        poolEntity = Entity.Null;

        if (prefabEntity == Entity.Null)
            return false;

        for (int index = 0; index < poolMap.Length; index++)
        {
            EnemyExperienceDropPoolMapElement mapElement = poolMap[index];

            if (mapElement.PrefabEntity != prefabEntity)
                continue;

            poolEntity = mapElement.PoolEntity;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one pool entity from a snapshot map array by prefab entity.
    /// </summary>
    /// <param name="poolMap">Snapshot map containing prefab-pool pairs.</param>
    /// <param name="prefabEntity">Drop prefab entity key.</param>
    /// <param name="poolEntity">Resolved pool entity when found.</param>
    /// <returns>True when a matching pool is found, otherwise false.<returns>
    public static bool TryResolvePoolEntity(NativeArray<EnemyExperienceDropPoolMapElement> poolMap,
                                            Entity prefabEntity,
                                            out Entity poolEntity)
    {
        poolEntity = Entity.Null;

        if (prefabEntity == Entity.Null)
            return false;

        for (int index = 0; index < poolMap.Length; index++)
        {
            EnemyExperienceDropPoolMapElement mapElement = poolMap[index];

            if (mapElement.PrefabEntity != prefabEntity)
                continue;

            poolEntity = mapElement.PoolEntity;
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
