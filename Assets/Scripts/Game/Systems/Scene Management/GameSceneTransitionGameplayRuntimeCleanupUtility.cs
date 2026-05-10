using Unity.Entities;

/// <summary>
/// Removes transient gameplay entities that are created at runtime and therefore are not owned by scene streaming.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneTransitionGameplayRuntimeCleanupUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Destroys runtime-only gameplay entities after the old gameplay scene has been unloaded and before the new run loads.
    /// /params entityManager Default world entity manager.
    /// /returns None.
    /// </summary>
    public static void DestroyTransientGameplayRuntimeEntities(EntityManager entityManager)
    {
        PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();
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
    /// /params entityManager Entity manager used for destruction.
    /// /returns None.
    /// </summary>
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
    /// /params entityManager Entity manager used for destruction.
    /// /returns None.
    /// </summary>
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
    /// /params entityManager Entity manager used for destruction.
    /// /params query Query selecting cleanup candidates.
    /// /returns None.
    /// </summary>
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
