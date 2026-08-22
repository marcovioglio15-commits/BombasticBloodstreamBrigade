using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Removes transient gameplay entities that are created at runtime and therefore are not owned by scene streaming.
/// </summary>
internal static class GameSceneTransitionGameplayRuntimeCleanupUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Destroys runtime-only gameplay entities before a transition crosses a run boundary or reloads the active run.
    /// </summary>
    /// <param name="entityManager">Default world entity manager.</param>
    /// <param name="preserveRoomClearAttraction">True when room-clear-attracted drops must survive a procedural room boundary.</param>
    public static void DestroyTransientGameplayRuntimeEntities(EntityManager entityManager,
                                                               bool preserveRoomClearAttraction)
    {
        DestroyNonPrefabEntitiesWith<Projectile>(entityManager);
        ResetProjectilePoolsAfterCleanup(entityManager);
        ResetReturningProjectileRuntimeAfterCleanup(entityManager);
        DestroyNonPrefabEntitiesWith<EnemyData>(entityManager);
        DestroyEntitiesWith<EnemyPoolState>(entityManager);
        DestroyExperienceDrops(entityManager, preserveRoomClearAttraction);
        DestroyEntitiesWith<EnemyExperienceDropPoolState>(entityManager);
        DestroyEntitiesWith<EnemyExperienceDropPoolRegistry>(entityManager);
        DestroyNonPrefabEntitiesWith<PlayerPowerUpVfxPooled>(entityManager);
        DestroyNonPrefabEntitiesWith<ElementalTrailSegment>(entityManager);
        DestroyNonPrefabEntitiesWith<EnemyDetachedAcidTrailState>(entityManager);
        DestroyNonPrefabEntitiesWith<EnemyBombardierBomb>(entityManager);
        DestroyNonPrefabEntitiesWith<BombFuseState>(entityManager);
        DestroyNonPrefabEntitiesWith<PlayerOrbitalProjectionInstance>(entityManager);
        DestroyNonPrefabEntitiesWith<PlayerDroppedPowerUpContainerContent>(entityManager);
        PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();
        EnemySpawnWarningPresentationSystem.DestroyRuntimeState();
        EnemyProjectileOffscreenWarningPresentationSystem.DestroyRuntimeState();
        EnemyDamageFlashPresentationSystem.DestroyRuntimeState();
        EnemyGroundIndicatorSyncSystem.DestroyRuntimeState();
        PlayerDroppedPowerUpContainerViewRuntimeUtility.Shutdown();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Destroys transient reward drops while optionally retaining active drops already committed to room-clear attraction.
    /// </summary>
    /// <param name="entityManager">Entity manager used for filtering and destruction.</param>
    /// <param name="preserveRoomClearAttraction">True when active persistent-attraction drops must remain alive.</param>
    private static void DestroyExperienceDrops(EntityManager entityManager,
                                               bool preserveRoomClearAttraction)
    {
        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<EnemyExperienceDrop>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<Prefab>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        NativeArray<Entity> candidates = default;

        try
        {
            candidates = query.ToEntityArray(Allocator.Temp);

            // Remove preserved active drops from the cleanup snapshot before linked-group destruction begins.
            if (preserveRoomClearAttraction)
            {
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    Entity candidate = candidates[candidateIndex];
                    EnemyExperienceDrop dropData = entityManager.GetComponentData<EnemyExperienceDrop>(candidate);
                    bool isActive = entityManager.HasComponent<EnemyExperienceDropActive>(candidate) &&
                                    entityManager.IsComponentEnabled<EnemyExperienceDropActive>(candidate);

                    if (isActive && dropData.IsRoomClearAttraction != 0)
                        candidates[candidateIndex] = Entity.Null;
                }
            }

            DestroyCandidates(entityManager, in candidates, true);
            DestroyCandidates(entityManager, in candidates, false);
        }
        finally
        {
            if (candidates.IsCreated)
                candidates.Dispose();

            query.Dispose();
        }
    }

    /// <summary>
    /// Clears stale projectile entity references and marks surviving shooter pools for deterministic reinitialization.
    /// This is required for persistent player shooters after procedural room cleanup destroys their pooled instances.
    /// </summary>
    /// <param name="entityManager">Entity manager owning surviving shooter state and buffers.</param>
    private static void ResetProjectilePoolsAfterCleanup(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadWrite<ProjectilePoolState>(),
                ComponentType.ReadWrite<ProjectilePoolElement>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        NativeArray<Entity> shooterEntities = default;

        try
        {
            shooterEntities = query.ToEntityArray(Allocator.Temp);

            // Reset buffers only after projectile destruction has completed, avoiding invalid aliases across structural changes.
            for (int shooterIndex = 0; shooterIndex < shooterEntities.Length; shooterIndex++)
            {
                Entity shooterEntity = shooterEntities[shooterIndex];

                if (!entityManager.Exists(shooterEntity))
                    continue;

                entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity).Clear();

                if (entityManager.HasBuffer<ShootRequest>(shooterEntity))
                    entityManager.GetBuffer<ShootRequest>(shooterEntity).Clear();

                ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(shooterEntity);
                poolState.Initialized = 0;
                entityManager.SetComponentData(shooterEntity, poolState);
            }
        }
        finally
        {
            if (shooterEntities.IsCreated)
                shooterEntities.Dispose();

            query.Dispose();
        }
    }

    /// <summary>
    /// Invalidates active-slot projectile ownership and return haptics after transition cleanup removes projectiles
    /// without routing them through ordinary pooling systems. This covers procedural room boundaries, scene reloads,
    /// and run-boundary cleanup while allowing the persistent player entity to fire again immediately afterward.
    /// </summary>
    /// <param name="entityManager">Entity manager owning any player state that survives the transition.</param>
    private static void ResetReturningProjectileRuntimeAfterCleanup(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadWrite<PlayerPowerUpsState>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        NativeArray<Entity> playerEntities = default;

        try
        {
            playerEntities = query.ToEntityArray(Allocator.Temp);

            // Reset every surviving player independently from pool initialization so partial transition states are safe.
            for (int playerIndex = 0; playerIndex < playerEntities.Length; playerIndex++)
            {
                Entity playerEntity = playerEntities[playerIndex];

                if (!entityManager.Exists(playerEntity))
                    continue;

                PlayerPowerUpsState powerUpsState = entityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);
                PlayerReturningProjectileLoadoutRuntimeUtility.ResetConcurrency(ref powerUpsState);
                entityManager.SetComponentData(playerEntity, powerUpsState);

                if (!entityManager.HasComponent<PlayerCameraShakeState>(playerEntity))
                    continue;

                PlayerCameraShakeState shakeState = entityManager.GetComponentData<PlayerCameraShakeState>(playerEntity);
                PlayerCameraShakeRuntimeUtility.ClearReturnFeedback(ref shakeState);
                entityManager.SetComponentData(playerEntity, shakeState);
            }
        }
        finally
        {
            if (playerEntities.IsCreated)
                playerEntities.Dispose();

            query.Dispose();
        }
    }

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
    /// Executes and disposes one cleanup query. LinkedEntityGroup roots are destroyed first through individual
    /// entity destruction so DOTS can include every linked child even when the marker query only selects the root.
    /// </summary>
    /// <param name="entityManager">Entity manager used for destruction.</param>
    /// <param name="query">Query selecting cleanup candidates.</param>
    private static void DestroyQuery(EntityManager entityManager, EntityQuery query)
    {
        NativeArray<Entity> candidates = default;

        try
        {
            candidates = query.ToEntityArray(Allocator.Temp);
            DestroyCandidates(entityManager, in candidates, true);
            DestroyCandidates(entityManager, in candidates, false);
        }
        finally
        {
            if (candidates.IsCreated)
                candidates.Dispose();

            query.Dispose();
        }
    }

    /// <summary>
    /// Destroys existing cleanup candidates individually. The first pass targets LinkedEntityGroup roots so their
    /// complete groups disappear before a later pass handles standalone entities or surviving marker children.
    /// </summary>
    /// <param name="entityManager">Entity manager used for existence checks and destruction.</param>
    /// <param name="candidates">Snapshot of entities selected by one transient marker query.</param>
    /// <param name="linkedGroupRootsOnly">True for the root-first pass; false for all remaining candidates.</param>
    private static void DestroyCandidates(EntityManager entityManager,
                                          in NativeArray<Entity> candidates,
                                          bool linkedGroupRootsOnly)
    {
        // Process the captured snapshot without issuing a partial linked-group query destruction.
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            Entity candidate = candidates[candidateIndex];

            if (candidate == Entity.Null)
                continue;

            if (!entityManager.Exists(candidate))
                continue;

            if (linkedGroupRootsOnly && !entityManager.HasBuffer<LinkedEntityGroup>(candidate))
                continue;

            entityManager.DestroyEntity(candidate);
        }
    }
    #endregion

    #endregion
}
