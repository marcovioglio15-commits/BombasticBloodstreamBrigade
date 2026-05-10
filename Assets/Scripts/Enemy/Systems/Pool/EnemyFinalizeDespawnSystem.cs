using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Finalizes queued enemy despawns and returns the instances to their owning prefab pool.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyWaveProgressSystem))]
[UpdateAfter(typeof(EnemyKillCounterSystem))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyExperienceDropSpawnSystem))]
public partial struct EnemyFinalizeDespawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum runtime dependencies required by the system.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDespawnRequest>();
    }

    /// <summary>
    /// Returns despawned enemies to their pool and removes the pending despawn request component.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<EnemyOwnerSpawner> ownerSpawnerLookup = SystemAPI.GetComponentLookup<EnemyOwnerSpawner>(true);
        ComponentLookup<EnemyOwnerPool> ownerPoolLookup = SystemAPI.GetComponentLookup<EnemyOwnerPool>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<EnemyDespawnRequest> _,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyDespawnRequest>>().WithEntityAccess())
        {
            bool isActive = entityManager.HasComponent<EnemyActive>(enemyEntity) && entityManager.IsComponentEnabled<EnemyActive>(enemyEntity);

            if (isActive)
            {
                Entity spawnerEntity = Entity.Null;
                Entity poolEntity = Entity.Null;

                if (ownerSpawnerLookup.HasComponent(enemyEntity))
                    spawnerEntity = ownerSpawnerLookup[enemyEntity].SpawnerEntity;

                if (ownerPoolLookup.HasComponent(enemyEntity))
                    poolEntity = ownerPoolLookup[enemyEntity].PoolEntity;

                EnemyPoolUtility.PrepareEnemyForPool(entityManager, enemyEntity, spawnerEntity, poolEntity);

                if (poolEntity != Entity.Null && entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
                {
                    DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);
                    poolBuffer.Add(new EnemyPoolElement
                    {
                        EnemyEntity = enemyEntity
                    });
                }
            }

            commandBuffer.RemoveComponent<EnemyDespawnRequest>(enemyEntity);
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
