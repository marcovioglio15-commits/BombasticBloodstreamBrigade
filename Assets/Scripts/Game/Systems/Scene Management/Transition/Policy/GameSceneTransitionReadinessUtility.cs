using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves whether managed Unity scenes and DOTS SubScenes are ready to be revealed after loading.
/// </summary>
internal static class GameSceneTransitionReadinessUtility
{
    #region Fields
    private static readonly List<SubScene> subSceneBuffer = new List<SubScene>(8);
    private const float ParkedProjectileHeightThreshold = -1000f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks every loaded scene surface that must be ready before fade-in can start.
    /// </summary>
    /// <param name="targetScene">Main transition target scene.</param>
    /// <param name="hasTargetCompanionScene">True when a companion scene was loaded with the target.</param>
    /// <param name="targetCompanionScene">Companion scene definition.</param>
    /// <param name="persistentPlayerScenes">Direct DOTS player scenes required by the target.</param>
    /// <returns>True when Unity scene activation and DOTS streaming are complete.</returns>
    public static bool AreTransitionScenesReady(GameSceneDefinitionElement targetScene,
                                                bool hasTargetCompanionScene,
                                                GameSceneDefinitionElement targetCompanionScene,
                                                List<GameSceneDefinitionElement> persistentPlayerScenes)
    {
        if (!IsManagedUnitySceneReady(targetScene))
            return false;

        if (hasTargetCompanionScene && !IsManagedUnitySceneReady(targetCompanionScene))
            return false;

        if (GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene) && !IsGameplayRuntimeReady())
            return false;

        return ArePersistentPlayerScenesReady(persistentPlayerScenes);
    }
    #endregion

    #region Managed Scene Readiness
    /// <summary>
    /// Checks whether one loaded Unity scene and its auto-loaded SubScenes are ready.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition to inspect.</param>
    /// <returns>True when the Unity scene is loaded and all auto SubScenes report loaded.</returns>
    private static bool IsManagedUnitySceneReady(GameSceneDefinitionElement sceneDefinition)
    {
        Scene loadedScene = GameSceneLoadBackendUtility.ResolveLoadedScene(sceneDefinition);

        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            return false;

        GameObject[] rootObjects = loadedScene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            subSceneBuffer.Clear();
            rootObjects[rootIndex].GetComponentsInChildren(true, subSceneBuffer);

            for (int subSceneIndex = 0; subSceneIndex < subSceneBuffer.Count; subSceneIndex++)
            {
                SubScene subScene = subSceneBuffer[subSceneIndex];

                if (!ShouldWaitForSubScene(subScene))
                    continue;

                if (!IsSubSceneLoaded(subScene))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves whether a SubScene should block transition reveal.
    /// </summary>
    /// <param name="subScene">SubScene component found in a loaded Unity scene.</param>
    /// <returns>True when the SubScene has a valid GUID and auto-loads content.</returns>
    private static bool ShouldWaitForSubScene(SubScene subScene)
    {
        if (subScene == null)
            return false;

        if (!subScene.AutoLoadScene)
            return false;

        return subScene.SceneGUID.IsValid;
    }

    /// <summary>
    /// Checks SceneSystem load state for one SubScene component.
    /// </summary>
    /// <param name="subScene">SubScene component with a SceneSystem GUID.</param>
    /// <returns>True when SceneSystem reports the scene entity as loaded.</returns>
    private static bool IsSubSceneLoaded(SubScene subScene)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID);

        if (sceneEntity == Entity.Null)
            return false;

        return SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity);
    }
    #endregion

    #region Persistent Player Readiness
    /// <summary>
    /// Checks direct DOTS persistent player scenes required by the target scene.
    /// </summary>
    /// <param name="persistentPlayerScenes">Persistent player scene definitions loaded for gameplay.</param>
    /// <returns>True when every required persistent player scene is loaded.</returns>
    private static bool ArePersistentPlayerScenesReady(List<GameSceneDefinitionElement> persistentPlayerScenes)
    {
        for (int index = 0; index < persistentPlayerScenes.Count; index++)
        {
            if (!GameScenePersistentPlayerSceneUtility.IsSceneLoaded(persistentPlayerScenes[index]))
                return false;
        }

        return true;
    }
    #endregion

    #region Gameplay Runtime Readiness
    /// <summary>
    /// Checks gameplay runtime surfaces that are created after scene load callbacks, before revealing gameplay.
    /// </summary>
    /// <returns>True when input, camera and the single player entity are ready for the first visible frame.</returns>
    private static bool IsGameplayRuntimeReady()
    {
        if (!PlayerInputRuntime.IsReady)
            return false;

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera) || camera == null)
            return false;

        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityManager entityManager = world.EntityManager;
        EntityQuery playerReadyQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                       ComponentType.ReadOnly<PlayerInputState>(),
                                                                       ComponentType.ReadOnly<PlayerMovementState>(),
                                                                       ComponentType.ReadOnly<PlayerRunOutcomeState>(),
                                                                       ComponentType.ReadOnly<PlayerRuntimeMovementConfig>(),
                                                                       ComponentType.ReadOnly<PlayerRuntimeCameraConfig>(),
                                                                       ComponentType.ReadOnly<LocalTransform>());

        try
        {
            if (playerReadyQuery.CalculateEntityCount() != 1)
                return false;

            return AreGameplayPoolsReady(entityManager);
        }
        finally
        {
            playerReadyQuery.Dispose();
        }
    }

    /// <summary>
    /// Checks every known gameplay pool that can expose prefab positions while prewarming.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <returns>True when player, enemy and experience-drop pools are initialized and parked.</returns>
    private static bool AreGameplayPoolsReady(EntityManager entityManager)
    {
        return AreProjectilePoolsReady(entityManager) &&
               AreEnemyPoolsReady(entityManager) &&
               AreExperienceDropPoolsReady(entityManager);
    }

    /// <summary>
    /// Checks whether active shooter projectile pools have finished prewarming and all pooled visuals are parked.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <returns>True when no prewarm projectile can still render at its prefab/world origin.</returns>
    private static bool AreProjectilePoolsReady(EntityManager entityManager)
    {
        EntityQuery poolQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ShooterProjectilePrefab>(),
                                                               ComponentType.ReadOnly<ProjectilePoolState>(),
                                                               ComponentType.ReadOnly<ProjectilePoolElement>());

        try
        {
            if (poolQuery.CalculateEntityCount() <= 0)
                return true;

            using NativeArray<Entity> shooterEntities = poolQuery.ToEntityArray(Allocator.Temp);

            for (int shooterIndex = 0; shooterIndex < shooterEntities.Length; shooterIndex++)
            {
                Entity shooterEntity = shooterEntities[shooterIndex];

                if (IsInactiveEnemyShooter(entityManager, shooterEntity) ||
                    IsOrphanedEnemyShooter(entityManager, shooterEntity))
                {
                    continue;
                }

                ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(shooterEntity);

                if (poolState.Initialized == 0)
                    return false;

                DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);

                if (projectilePool.Length < math.max(0, poolState.InitialCapacity))
                    return false;

                for (int projectileIndex = 0; projectileIndex < projectilePool.Length; projectileIndex++)
                {
                    if (!IsPooledProjectileParked(entityManager, projectilePool[projectileIndex].ProjectileEntity))
                        return false;
                }
            }

            return true;
        }
        finally
        {
            poolQuery.Dispose();
        }
    }

    /// <summary>
    /// Resolves whether a shooter belongs to an inactive pooled enemy whose projectile pool is intentionally lazy.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="shooterEntity">Shooter entity to inspect.</param>
    /// <returns>True when the shooter is an inactive enemy instance.</returns>
    private static bool IsInactiveEnemyShooter(EntityManager entityManager, Entity shooterEntity)
    {
        return entityManager.HasComponent<EnemyActive>(shooterEntity) &&
               !entityManager.IsComponentEnabled<EnemyActive>(shooterEntity);
    }

    /// <summary>
    /// Resolves whether an enemy shooter belongs to a stale runtime entity from a previous scene instance.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="shooterEntity">Shooter entity to inspect.</param>
    /// <returns>True when the shooter is an enemy whose owner spawner or pool is no longer alive.</returns>
    private static bool IsOrphanedEnemyShooter(EntityManager entityManager, Entity shooterEntity)
    {
        if (!entityManager.HasComponent<EnemyActive>(shooterEntity))
            return false;

        if (!entityManager.HasComponent<EnemyOwnerSpawner>(shooterEntity) ||
            !entityManager.HasComponent<EnemyOwnerPool>(shooterEntity))
        {
            return true;
        }

        EnemyOwnerSpawner ownerSpawner = entityManager.GetComponentData<EnemyOwnerSpawner>(shooterEntity);
        EnemyOwnerPool ownerPool = entityManager.GetComponentData<EnemyOwnerPool>(shooterEntity);

        return ownerSpawner.SpawnerEntity == Entity.Null ||
               ownerPool.PoolEntity == Entity.Null ||
               !entityManager.Exists(ownerSpawner.SpawnerEntity) ||
               !entityManager.Exists(ownerPool.PoolEntity);
    }

    /// <summary>
    /// Checks one pooled projectile's runtime and render transforms.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="projectileEntity">Pooled projectile entity to inspect.</param>
    /// <returns>True when the projectile is inactive and parked far below the gameplay floor.</returns>
    private static bool IsPooledProjectileParked(EntityManager entityManager, Entity projectileEntity)
    {
        if (projectileEntity == Entity.Null || !entityManager.Exists(projectileEntity))
            return false;

        if (entityManager.HasComponent<ProjectileActive>(projectileEntity) &&
            entityManager.IsComponentEnabled<ProjectileActive>(projectileEntity))
        {
            return false;
        }

        if (entityManager.HasComponent<LocalTransform>(projectileEntity) &&
            !IsParkedPosition(entityManager.GetComponentData<LocalTransform>(projectileEntity).Position))
        {
            return false;
        }

        if (entityManager.HasComponent<LocalToWorld>(projectileEntity) &&
            !IsParkedPosition(entityManager.GetComponentData<LocalToWorld>(projectileEntity).Position))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether wave enemy pools have completed prewarm and all inactive instances are parked.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <returns>True when enemy spawners and their pool entities are ready.</returns>
    private static bool AreEnemyPoolsReady(EntityManager entityManager)
    {
        EntityQuery spawnerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawnerState>(),
                                                                   ComponentType.ReadOnly<EnemySpawnerPrefabRequirementElement>(),
                                                                   ComponentType.ReadOnly<EnemySpawnerPrefabPoolMapElement>());

        try
        {
            using NativeArray<Entity> spawnerEntities = spawnerQuery.ToEntityArray(Allocator.Temp);

            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            {
                Entity spawnerEntity = spawnerEntities[spawnerIndex];
                EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

                if (spawnerState.Initialized == 0)
                    return false;

                DynamicBuffer<EnemySpawnerPrefabRequirementElement> requirements = entityManager.GetBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity);
                DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

                for (int requirementIndex = 0; requirementIndex < requirements.Length; requirementIndex++)
                {
                    Entity prefabEntity = requirements[requirementIndex].PrefabEntity;

                    if (prefabEntity == Entity.Null)
                        continue;

                    if (!entityManager.Exists(prefabEntity))
                        continue;

                    if (!TryResolveEnemyPool(poolMap, prefabEntity, out Entity poolEntity))
                        return false;

                    if (!IsEnemyPoolReady(entityManager, poolEntity))
                        return false;
                }
            }

            return true;
        }
        finally
        {
            spawnerQuery.Dispose();
        }
    }

    /// <summary>
    /// Resolves the runtime pool entity associated with one enemy prefab in a spawner map.
    /// </summary>
    /// <param name="poolMap">Spawner prefab-to-pool map.</param>
    /// <param name="prefabEntity">Enemy prefab entity to resolve.</param>
    /// <param name="poolEntity">Resolved pool entity when present.</param>
    /// <returns>True when the map contains the prefab.</returns>
    private static bool TryResolveEnemyPool(DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap,
                                            Entity prefabEntity,
                                            out Entity poolEntity)
    {
        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            EnemySpawnerPrefabPoolMapElement mapElement = poolMap[mapIndex];

            if (mapElement.PrefabEntity != prefabEntity)
                continue;

            poolEntity = mapElement.PoolEntity;
            return true;
        }

        poolEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Checks one enemy pool referenced by a currently loaded spawner.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="poolEntity">Runtime pool entity to inspect.</param>
    /// <returns>True when the pool is initialized, filled to initial capacity and parked.</returns>
    private static bool IsEnemyPoolReady(EntityManager entityManager, Entity poolEntity)
    {
        if (poolEntity == Entity.Null || !entityManager.Exists(poolEntity))
            return false;

        if (!entityManager.HasComponent<EnemyPoolState>(poolEntity) ||
            !entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
        {
            return false;
        }

        EnemyPoolState poolState = entityManager.GetComponentData<EnemyPoolState>(poolEntity);

        if (poolState.Initialized == 0)
            return false;

        DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

        if (poolBuffer.Length < math.max(0, poolState.InitialCapacity))
            return false;

        for (int enemyIndex = 0; enemyIndex < poolBuffer.Length; enemyIndex++)
        {
            if (!IsPooledEnemyParked(entityManager, poolBuffer[enemyIndex].EnemyEntity))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks one pooled enemy's active flag and render transforms.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="enemyEntity">Pooled enemy entity to inspect.</param>
    /// <returns>True when the enemy is inactive and parked.</returns>
    private static bool IsPooledEnemyParked(EntityManager entityManager, Entity enemyEntity)
    {
        if (enemyEntity == Entity.Null || !entityManager.Exists(enemyEntity))
            return false;

        if (entityManager.HasComponent<EnemyActive>(enemyEntity) &&
            entityManager.IsComponentEnabled<EnemyActive>(enemyEntity))
        {
            return false;
        }

        return HasParkedRuntimeAndRenderTransform(entityManager, enemyEntity);
    }

    /// <summary>
    /// Checks whether experience-drop pools have completed prewarm and all inactive drops are parked.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <returns>True when every experience-drop pool registry and pool entity is ready.</returns>
    private static bool AreExperienceDropPoolsReady(EntityManager entityManager)
    {
        EntityQuery registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemyExperienceDropPoolRegistry>());
        bool hasExperienceDropPoolSources = HasExperienceDropPoolSources(entityManager);

        try
        {
            int registryCount = registryQuery.CalculateEntityCount();

            if (registryCount > 1)
                return false;

            if (registryCount == 0)
                return !hasExperienceDropPoolSources;

            if (registryCount == 1)
            {
                EnemyExperienceDropPoolRegistry registry = registryQuery.GetSingleton<EnemyExperienceDropPoolRegistry>();

                if (registry.Initialized == 0 && hasExperienceDropPoolSources)
                    return false;
            }
        }
        finally
        {
            registryQuery.Dispose();
        }

        EntityQuery poolQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemyExperienceDropPoolState>(),
                                                               ComponentType.ReadOnly<EnemyExperienceDropPoolElement>());

        try
        {
            using NativeArray<Entity> poolEntities = poolQuery.ToEntityArray(Allocator.Temp);

            for (int poolIndex = 0; poolIndex < poolEntities.Length; poolIndex++)
            {
                Entity poolEntity = poolEntities[poolIndex];
                EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);

                if (poolState.Initialized == 0)
                    return false;

                DynamicBuffer<EnemyExperienceDropPoolElement> poolBuffer = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity);

                if (poolBuffer.Length < math.max(0, poolState.InitialCapacity))
                    return false;

                for (int dropIndex = 0; dropIndex < poolBuffer.Length; dropIndex++)
                {
                    if (!IsPooledExperienceDropParked(entityManager, poolBuffer[dropIndex].DropEntity))
                        return false;
                }
            }

            return true;
        }
        finally
        {
            poolQuery.Dispose();
        }
    }

    /// <summary>
    /// Checks whether the loaded gameplay world contains authored sources that should build experience-drop pools.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <returns>True when enemy spawners can contribute experience-drop pool requirements.</returns>
    private static bool HasExperienceDropPoolSources(EntityManager entityManager)
    {
        EntityQuery sourceQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EnemySpawner>(),
                                                                 ComponentType.ReadOnly<EnemySpawnerPrefabRequirementElement>());

        try
        {
            return sourceQuery.CalculateEntityCount() > 0;
        }
        finally
        {
            sourceQuery.Dispose();
        }
    }

    /// <summary>
    /// Checks one pooled experience drop's active flag and render transforms.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="dropEntity">Pooled drop entity to inspect.</param>
    /// <returns>True when the drop is inactive and parked.</returns>
    private static bool IsPooledExperienceDropParked(EntityManager entityManager, Entity dropEntity)
    {
        if (dropEntity == Entity.Null || !entityManager.Exists(dropEntity))
            return false;

        if (entityManager.HasComponent<EnemyExperienceDropActive>(dropEntity) &&
            entityManager.IsComponentEnabled<EnemyExperienceDropActive>(dropEntity))
        {
            return false;
        }

        return HasParkedRuntimeAndRenderTransform(entityManager, dropEntity);
    }

    /// <summary>
    /// Checks both LocalTransform and LocalToWorld when present so render state cannot lag behind runtime parking.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="entity">Entity to inspect.</param>
    /// <returns>True when all present transform surfaces are parked.</returns>
    private static bool HasParkedRuntimeAndRenderTransform(EntityManager entityManager, Entity entity)
    {
        if (entityManager.HasComponent<LocalTransform>(entity) &&
            !IsParkedPosition(entityManager.GetComponentData<LocalTransform>(entity).Position))
        {
            return false;
        }

        if (entityManager.HasComponent<LocalToWorld>(entity) &&
            !IsParkedPosition(entityManager.GetComponentData<LocalToWorld>(entity).Position))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves whether a transform position is safely outside the visible gameplay space.
    /// </summary>
    /// <param name="position">Transform position to inspect.</param>
    /// <returns>True when the position is below the projectile parking threshold.</returns>
    private static bool IsParkedPosition(float3 position)
    {
        return position.y <= ParkedProjectileHeightThreshold;
    }
    #endregion

    #endregion
}
