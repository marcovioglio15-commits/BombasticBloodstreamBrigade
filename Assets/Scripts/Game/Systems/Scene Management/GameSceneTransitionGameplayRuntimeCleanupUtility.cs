using Unity.Entities;

/// <summary>
/// Removes transient gameplay entities that are created at runtime and therefore are not owned by scene streaming.
/// </summary>
internal static class GameSceneTransitionGameplayRuntimeCleanupUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Destroys runtime-only gameplay entities after the old gameplay scene has been unloaded and before the new run loads.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    public static void DestroyTransientGameplayRuntimeEntities(EntityManager entityManager)
    {
        PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();
        EnemySpawnWarningPresentationSystem.DestroyRuntimeState();
        EnemyOffensiveEngagementBillboardRuntimeUtility.Shutdown();
        EnemyGroundIndicatorSyncSystem.DestroyRuntimeState();
        PlayerDroppedPowerUpContainerViewRuntimeUtility.Shutdown();
        DestroyNonPrefabEntitiesWith<Projectile>(entityManager);
        DestroyNonPrefabEntitiesWith<EnemyData>(entityManager);
        DestroyEntitiesWith<EnemyPoolState>(entityManager);
        DestroyNonPrefabEntitiesWith<EnemyExperienceDrop>(entityManager);
        DestroyEntitiesWith<EnemyExperienceDropPoolState>(entityManager);
        DestroyEntitiesWith<EnemyExperienceDropPoolRegistry>(entityManager);
        DestroyNonPrefabEntitiesWith<PlayerPowerUpVfxPooled>(entityManager);
        DestroyNonPrefabEntitiesWith<ElementalTrailSegment>(entityManager);
        DestroyNonPrefabEntitiesWith<BombFuseState>(entityManager);
        DestroyNonPrefabEntitiesWith<PlayerDroppedPowerUpContainerContent>(entityManager);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Destroys every entity with a runtime marker component, excluding prefab entities.
    /// </summary>
    /// <param name="entityManager">Entity manager used for destruction.</param>
    private static void DestroyNonPrefabEntitiesWith<TComponent>(EntityManager entityManager)
        where TComponent : unmanaged, IComponentData
    {
        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<TComponent>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<Prefab>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });

        DestroyQuery(entityManager, query);
    }

    /// <summary>
    /// Destroys every runtime singleton or pool entity with the provided component.
    /// </summary>
    /// <param name="entityManager">Entity manager used for destruction.</param>
    private static void DestroyEntitiesWith<TComponent>(EntityManager entityManager)
        where TComponent : unmanaged, IComponentData
    {
        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<TComponent>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        DestroyQuery(entityManager, query);
    }

    /// <summary>
    /// Executes and disposes one cleanup query.
    /// </summary>
    /// <param name="entityManager">Entity manager used for destruction.</param>
    /// <param name="query">Query selecting cleanup candidates.</param>
    private static void DestroyQuery(EntityManager entityManager, EntityQuery query)
    {
        try
        {
            if (!query.IsEmptyIgnoreFilter)
                entityManager.DestroyEntity(query);
        }
        finally
        {
            query.Dispose();
        }
    }
    #endregion

    #endregion
}
