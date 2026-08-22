using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Prewarms lightweight pool partitions for equipped projectile-replacement modules before their first activation input.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectilePoolInitializeSystem))]
[UpdateAfter(typeof(PlayerPowerUpContainerSwapResolveSystem))]
[UpdateAfter(typeof(PlayerPowerUpRechargeSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct ProjectileSpecializedPoolPrewarmSystem : ISystem
{
    #region Nested Types
    private struct PrewarmRequest
    {
        public Entity ShooterEntity;
        public Entity ProjectilePrefabEntity;
    }
    #endregion

    #region Constants
    private const int PrewarmCapacity = 1;
    #endregion

    #region Fields
    private EntityQuery candidateQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Tracks only player loadout chunks whose active or aggregated passive projectile configuration changed.
    /// </summary>
    /// <param name="state">Current ECS system state used to build the filtered query.</param>
    public void OnCreate(ref SystemState state)
    {
        candidateQuery = SystemAPI.QueryBuilder()
            .WithAll<ShooterProjectilePrefab,
                     ProjectilePoolState,
                     ProjectilePoolElement,
                     PlayerPowerUpsConfigElement,
                     PlayerPassiveToolsStateElement,
                     PlayerPowerUpsState>()
            .Build();
        candidateQuery.SetChangedVersionFilter(new ComponentType[]
        {
            ComponentType.ReadOnly<PlayerPowerUpsConfigElement>(),
            ComponentType.ReadOnly<PlayerPassiveToolsStateElement>()
        });
        state.RequireForUpdate(candidateQuery);
    }

    /// <summary>
    /// Collects changed replacement prefabs without structural mutations, then creates one parked instance per missing partition.
    /// </summary>
    /// <param name="state">Current ECS system state providing read-only loadout data and structural pool access.</param>
    public void OnUpdate(ref SystemState state)
    {
        int candidateCount = candidateQuery.CalculateEntityCount();

        if (candidateCount <= 0)
            return;

        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeArray<Entity> candidateEntities = candidateQuery.ToEntityArray(frameAllocator);
        NativeList<PrewarmRequest> prewarmRequests = new NativeList<PrewarmRequest>(candidateCount, frameAllocator);
        ComponentLookup<ShooterProjectilePrefab> shooterPrefabLookup = SystemAPI.GetComponentLookup<ShooterProjectilePrefab>(true);
        ComponentLookup<ProjectilePoolState> poolStateLookup = SystemAPI.GetComponentLookup<ProjectilePoolState>(true);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(true);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(true);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(true);
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);

        // Resolve active and passive replacement partitions before performing structural pool expansion.
        for (int candidateIndex = 0; candidateIndex < candidateEntities.Length; candidateIndex++)
        {
            Entity shooterEntity = candidateEntities[candidateIndex];

            if (poolStateLookup[shooterEntity].Initialized == 0)
                continue;

            DynamicBuffer<ProjectilePoolElement> projectilePool = projectilePoolLookup[shooterEntity];
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigLookup[shooterEntity], out PlayerPowerUpsConfig powerUpsConfig);
            PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateLookup[shooterEntity], out PlayerPassiveToolsState passiveToolsState);
            PlayerPowerUpsState powerUpsState = powerUpsStateLookup[shooterEntity];
            Entity basePrefabEntity = shooterPrefabLookup[shooterEntity].PrefabEntity;

            TryCollectActiveSlot(in powerUpsConfig.PrimarySlot,
                                 powerUpsState.PrimaryReturningProjectileCount,
                                 shooterEntity,
                                 basePrefabEntity,
                                 projectilePool,
                                 entityManager,
                                 ref prewarmRequests);
            TryCollectActiveSlot(in powerUpsConfig.SecondarySlot,
                                 powerUpsState.SecondaryReturningProjectileCount,
                                 shooterEntity,
                                 basePrefabEntity,
                                 projectilePool,
                                 entityManager,
                                 ref prewarmRequests);

            if (passiveToolsState.HasReturningProjectiles != 0)
                TryCollectReplacement(in passiveToolsState.ReturningProjectiles,
                                      false,
                                      shooterEntity,
                                      basePrefabEntity,
                                      projectilePool,
                                      entityManager,
                                      ref prewarmRequests);
        }

        // A single parked specialized projectile removes asset conversion and archetype migration from the launch frame.
        for (int requestIndex = 0; requestIndex < prewarmRequests.Length; requestIndex++)
        {
            PrewarmRequest request = prewarmRequests[requestIndex];

            if (!entityManager.Exists(request.ShooterEntity) ||
                !entityManager.Exists(request.ProjectilePrefabEntity))
                continue;

            ProjectilePoolUtility.ExpandPool(entityManager,
                                             request.ShooterEntity,
                                             request.ProjectilePrefabEntity,
                                             PrewarmCapacity);
        }
    }
    #endregion

    #region Collection
    /// <summary>
    /// Collects one active-slot replacement when no live non-concurrent projectile already owns that slot.
    /// </summary>
    /// <param name="slotConfig">Current active-slot configuration.</param>
    /// <param name="liveProjectileCount">Registered returning projectiles currently owned by the slot.</param>
    /// <param name="shooterEntity">Player entity that owns the projectile pool.</param>
    /// <param name="basePrefabEntity">Player's standard projectile prefab.</param>
    /// <param name="projectilePool">Current parked projectile entries.</param>
    /// <param name="entityManager">Entity manager used to validate replacement prefab entities.</param>
    /// <param name="prewarmRequests">Unique replacement partitions queued for expansion.</param>
    private static void TryCollectActiveSlot(in PlayerPowerUpSlotConfig slotConfig,
                                             int liveProjectileCount,
                                             Entity shooterEntity,
                                             Entity basePrefabEntity,
                                             DynamicBuffer<ProjectilePoolElement> projectilePool,
                                             EntityManager entityManager,
                                             ref NativeList<PrewarmRequest> prewarmRequests)
    {
        if (slotConfig.IsDefined == 0 || slotConfig.HasReturningProjectiles == 0)
            return;

        bool liveProjectileOwnsNonConcurrentSlot =
            slotConfig.ReturningProjectiles.AllowConcurrentActiveProjectiles == 0 && liveProjectileCount > 0;
        TryCollectReplacement(in slotConfig.ReturningProjectiles,
                              liveProjectileOwnsNonConcurrentSlot,
                              shooterEntity,
                              basePrefabEntity,
                              projectilePool,
                              entityManager,
                              ref prewarmRequests);
    }

    /// <summary>
    /// Adds one valid missing replacement partition while suppressing base-prefab and duplicate requests.
    /// </summary>
    /// <param name="config">Returning-projectile configuration containing the optional replacement prefab.</param>
    /// <param name="liveProjectileOwnsPartition">Whether an existing non-concurrent projectile already satisfies this slot.</param>
    /// <param name="shooterEntity">Player entity that owns the projectile pool.</param>
    /// <param name="basePrefabEntity">Player's standard projectile prefab.</param>
    /// <param name="projectilePool">Current parked projectile entries.</param>
    /// <param name="entityManager">Entity manager used to validate replacement prefab entities.</param>
    /// <param name="prewarmRequests">Unique replacement partitions queued for expansion.</param>
    private static void TryCollectReplacement(in ReturningProjectilesConfig config,
                                              bool liveProjectileOwnsPartition,
                                              Entity shooterEntity,
                                              Entity basePrefabEntity,
                                              DynamicBuffer<ProjectilePoolElement> projectilePool,
                                              EntityManager entityManager,
                                              ref NativeList<PrewarmRequest> prewarmRequests)
    {
        Entity replacementPrefabEntity = config.ReplacementProjectilePrefabEntity;

        if (liveProjectileOwnsPartition ||
            replacementPrefabEntity == Entity.Null ||
            replacementPrefabEntity == basePrefabEntity ||
            !entityManager.Exists(replacementPrefabEntity) ||
            ProjectileSpawnPoolSelectionUtility.CountAvailable(projectilePool, replacementPrefabEntity) > 0)
            return;

        for (int requestIndex = 0; requestIndex < prewarmRequests.Length; requestIndex++)
        {
            PrewarmRequest request = prewarmRequests[requestIndex];

            if (request.ShooterEntity == shooterEntity &&
                request.ProjectilePrefabEntity == replacementPrefabEntity)
                return;
        }

        prewarmRequests.Add(new PrewarmRequest
        {
            ShooterEntity = shooterEntity,
            ProjectilePrefabEntity = replacementPrefabEntity
        });
    }
    #endregion

    #endregion
}
