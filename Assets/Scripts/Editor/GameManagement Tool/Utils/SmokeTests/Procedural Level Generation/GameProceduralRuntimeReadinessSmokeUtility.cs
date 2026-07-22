#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// Validates persistent player resources and active-room enemy spawning readiness during procedural Play Mode smoke tests.
/// </summary>
public static class GameProceduralRuntimeReadinessSmokeUtility
{
    #region Methods

    #region Player Readiness
    /// <summary>
    /// Resolves the unique persistent player used by restart and projectile-pool assertions.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="playerEntity">Resolved persistent player entity.</param>
    /// <returns>True when exactly one player exists.</returns>
    public static bool TryResolvePlayerEntity(EntityManager entityManager, out Entity playerEntity)
    {
        playerEntity = Entity.Null;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                            ComponentType.ReadOnly<PlayerRunOutcomeState>());

        try
        {
            if (query.CalculateEntityCount() != 1)
                return false;

            playerEntity = query.GetSingletonEntity();
            return true;
        }
        finally
        {
            query.Dispose();
        }
    }

    /// <summary>
    /// Verifies cleanup rebuilt the surviving player's projectile pool with valid parked entities.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing player shooting runtime.</param>
    /// <param name="failure">Diagnostic message when pool initialization or entity ownership is stale.</param>
    /// <returns>True when the player pool is initialized and contains only valid projectile entities.</returns>
    public static bool ValidatePlayerProjectilePoolReady(EntityManager entityManager, out string failure)
    {
        failure = string.Empty;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                            ComponentType.ReadOnly<ShooterProjectilePrefab>(),
                                                            ComponentType.ReadOnly<ProjectilePoolState>(),
                                                            ComponentType.ReadOnly<ProjectilePoolElement>());

        try
        {
            if (query.CalculateEntityCount() != 1)
            {
                failure = "Projectile-pool validation requires exactly one persistent player shooter.";
                return false;
            }

            Entity playerEntity = query.GetSingletonEntity();
            ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(playerEntity);
            DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(playerEntity, true);

            if (poolState.Initialized == 0 || projectilePool.Length < Mathf.Max(0, poolState.InitialCapacity))
            {
                failure = "The persistent player projectile pool was not rebuilt after room cleanup.";
                return false;
            }

            // Every pool reference must resolve to a live, inactive projectile before gameplay is revealed.
            for (int projectileIndex = 0; projectileIndex < projectilePool.Length; projectileIndex++)
            {
                Entity projectileEntity = projectilePool[projectileIndex].ProjectileEntity;

                if (!entityManager.Exists(projectileEntity) ||
                    !entityManager.HasComponent<ProjectileActive>(projectileEntity) ||
                    entityManager.IsComponentEnabled<ProjectileActive>(projectileEntity))
                {
                    failure = "The persistent player projectile pool contains a missing or active entity after transition cleanup.";
                    return false;
                }
            }

            return true;
        }
        finally
        {
            query.Dispose();
        }
    }
    #endregion

    #region Enemy Readiness
    /// <summary>
    /// Verifies every loaded room spawner completed pool initialization and resumed its wave clock after traversal.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the single loaded room instance.</param>
    /// <param name="ready">True when every discovered spawner can schedule and activate its authored waves.</param>
    /// <param name="failure">Diagnostic message when a spawner has structurally invalid runtime data.</param>
    /// <returns>True when the readiness state was evaluated without detecting invalid data.</returns>
    public static bool TryValidateEnemySpawnersReady(EntityManager entityManager,
                                                     out bool ready,
                                                     out string failure)
    {
        ready = true;
        failure = string.Empty;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawner>(),
                                                            ComponentType.ReadOnly<EnemySpawnerState>(),
                                                            ComponentType.ReadOnly<SceneTag>());

        try
        {
            using NativeArray<Entity> spawnerEntities = query.ToEntityArray(Allocator.Temp);

            // Single-slot streaming guarantees every loaded scene-owned spawner belongs to the target room.
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            {
                Entity spawnerEntity = spawnerEntities[spawnerIndex];
                EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

                if (!entityManager.HasBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity))
                {
                    failure = "A target-room enemy spawner has no prefab-to-pool runtime map.";
                    return false;
                }

                if (spawnerState.Initialized == 0 || spawnerState.StartTimeInitialized == 0)
                {
                    ready = false;
                    continue;
                }

                DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity, true);

                // Initialized spawners must retain only live and fully prewarmed pool mappings.
                for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
                {
                    Entity poolEntity = poolMap[mapIndex].PoolEntity;

                    if (poolEntity == Entity.Null ||
                        !entityManager.Exists(poolEntity) ||
                        !entityManager.HasComponent<EnemyPoolState>(poolEntity))
                    {
                        failure = "An initialized target-room enemy spawner references a missing pool entity.";
                        return false;
                    }

                    if (entityManager.GetComponentData<EnemyPoolState>(poolEntity).Initialized == 0)
                        ready = false;
                }
            }

            return true;
        }
        finally
        {
            query.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
