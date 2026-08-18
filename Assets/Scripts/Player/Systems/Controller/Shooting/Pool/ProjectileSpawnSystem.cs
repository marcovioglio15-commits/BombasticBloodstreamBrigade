using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// This system processes shooter entities with pending shoot requests, 
/// ensuring that their projectile pools are expanded as needed. It is updated after the 
/// PlayerShootingIntentSystem, which generates shoot requests based on player input and shooting state,
/// and after the ProjectilePoolInitializeSystem, which initializes projectile pools for shooters.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerShootingIntentSystem))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateAfter(typeof(ProjectilePoolInitializeSystem))]
public partial struct ProjectileSpawnSystem : ISystem
{
    #region Nested Types
    private struct PoolExpansionRequest
    {
        public Entity ShooterEntity;
        public Entity ProjectilePrefab;
        public int ExpandCount;
    }
    #endregion

    #region Constants
    private const float MinimumProjectileScale = 0.0001f;
    #endregion

    #region Fields
    private EntityQuery shootersWithRequestsQuery;
    #endregion


    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for shooter entities that have a ShooterProjectilePrefab, 
    /// ProjectilePoolState, ProjectilePoolElement buffer, and ShootRequest buffer.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        // Define an EntityQuery to select shooter entities that have the necessary components
        // for processing shoot requests. This includes the ShooterProjectilePrefab to identify
        // the projectile prefab to use,
        shootersWithRequestsQuery = SystemAPI.QueryBuilder()
            .WithAll<ShooterProjectilePrefab, ProjectilePoolState, ProjectilePoolElement, ShootRequest>()
            .Build();

        // Require the query for updates to ensure the system only runs when there are shooter entities with shoot requests to process
        state.RequireForUpdate(shootersWithRequestsQuery);
    }
    
    /// <summary>
    /// Processes shooter entities with pending shoot requests, expands projectile pools as needed, and spawns and
    /// initializes projectiles based on the requests.
    /// </summary>
    /// <param name="state">The current system state used to access the EntityManager and other ECS data.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeList<PoolExpansionRequest> expansionRequests = new NativeList<PoolExpansionRequest>(frameAllocator);
        BufferLookup<PlayerPassiveToolsStateElement> preExpansionPassiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);

        // Two-phase flow: collect requests first, then apply structural pool growth outside query iteration.
        CollectPoolExpansionRequests(ref state,
                                     entityManager,
                                     in preExpansionPassiveToolsLookup,
                                     ref expansionRequests);
        ExecutePoolExpansionRequests(entityManager, in expansionRequests);

        // Refresh lookups after structural changes performed during pool expansion.
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);
        BufferLookup<PlayerProjectileSizePowerUpMultiplierElement> projectileSizePowerUpMultipliersLookup = SystemAPI.GetBufferLookup<PlayerProjectileSizePowerUpMultiplierElement>(true);
        ComponentLookup<PlayerShootingState> shootingStateLookup = SystemAPI.GetComponentLookup<PlayerShootingState>(false);
        ComponentLookup<PlayerCameraShakeState> cameraShakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(false);
        ComponentLookup<LocalTransform> projectileTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
        ComponentLookup<Projectile> projectileLookup = SystemAPI.GetComponentLookup<Projectile>(false);
        ComponentLookup<ProjectileRuntimeState> projectileRuntimeLookup = SystemAPI.GetComponentLookup<ProjectileRuntimeState>(false);
        ComponentLookup<ProjectileContactState> projectileContactStateLookup = SystemAPI.GetComponentLookup<ProjectileContactState>(false);
        ComponentLookup<ProjectileOwner> projectileOwnerLookup = SystemAPI.GetComponentLookup<ProjectileOwner>(false);
        ComponentLookup<EnemyProjectileOffscreenWarningConfig> enemyProjectileOffscreenWarningLookup = SystemAPI.GetComponentLookup<EnemyProjectileOffscreenWarningConfig>(true);
        ComponentLookup<ProjectileOffscreenWarningState> projectileOffscreenWarningLookup = SystemAPI.GetComponentLookup<ProjectileOffscreenWarningState>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<ProjectilePerfectCircleState> perfectCircleLookup = SystemAPI.GetComponentLookup<ProjectilePerfectCircleState>(false);
        ComponentLookup<ProjectileBounceState> bounceLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(false);
        ComponentLookup<ProjectileSplitState> splitLookup = SystemAPI.GetComponentLookup<ProjectileSplitState>(false);
        ComponentLookup<ProjectileElementalPayload> elementalPayloadLookup = SystemAPI.GetComponentLookup<ProjectileElementalPayload>(false);
        ComponentLookup<ProjectileReturnState> returnStateLookup = SystemAPI.GetComponentLookup<ProjectileReturnState>(false);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false);
        ComponentLookup<ProjectileActive> projectileActiveLookup = SystemAPI.GetComponentLookup<ProjectileActive>(false);
        ComponentLookup<PlayerProjectileAttachedVfxConfig> projectileAttachedVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerProjectileAttachedVfxConfig>(true);
        ComponentLookup<PlayerMuzzleFlashVfxConfig> muzzleFlashVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerMuzzleFlashVfxConfig>(true);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup = SystemAPI.GetBufferLookup<ProjectileHitHistoryElement>(false);
        BufferLookup<ProjectileReturnPathPoint> returnPathLookup = SystemAPI.GetBufferLookup<ProjectileReturnPathPoint>(false);

        ProcessShootRequests(ref state,
                             entityManager,
                             (float)SystemAPI.Time.ElapsedTime,
                             in passiveToolsLookup,
                             in projectileSizePowerUpMultipliersLookup,
                             ref shootingStateLookup,
                             ref cameraShakeStateLookup,
                             ref projectileTransformLookup,
                             ref projectileLookup,
                             ref projectileRuntimeLookup,
                             ref projectileContactStateLookup,
                             ref projectileOwnerLookup,
                             in enemyProjectileOffscreenWarningLookup,
                             ref projectileOffscreenWarningLookup,
                             in projectileBaseScaleLookup,
                             ref perfectCircleLookup,
                             ref bounceLookup,
                             ref splitLookup,
                             ref elementalPayloadLookup,
                             ref returnStateLookup,
                             ref powerUpsStateLookup,
                             ref projectileActiveLookup,
                             in projectileAttachedVfxConfigLookup,
                             in muzzleFlashVfxConfigLookup,
                             ref powerUpVfxRequestLookup,
                             ref projectileHitHistoryLookup,
                             ref returnPathLookup);
    }
    #endregion

    #region Pool Expansion
    /// <summary>
    /// Collects pool expansion requests without applying structural changes during entity iteration.
    /// </summary>
    /// <param name="state">Current ECS system state used by the shooter query.</param>
    /// <param name="entityManager">EntityManager used to inspect shooter state and buffers.</param>
    /// <param name="passiveToolsLookup">Read-only aggregated passive state used to select replacement prefabs.</param>
    /// <param name="expansionRequests">Mutable list that receives expansion requests.</param>

    private void CollectPoolExpansionRequests(ref SystemState state,
                                              EntityManager entityManager,
                                              in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                              ref NativeList<PoolExpansionRequest> expansionRequests)
    {
        foreach ((DynamicBuffer<ShootRequest> shootRequests,
                  DynamicBuffer<ProjectilePoolElement> projectilePool,
                  RefRO<ShooterProjectilePrefab> projectilePrefab,
                  RefRO<ProjectilePoolState> poolStateValue,
                  Entity shooterEntity) in SystemAPI.Query<DynamicBuffer<ShootRequest>,
                                                           DynamicBuffer<ProjectilePoolElement>,
                                                           RefRO<ShooterProjectilePrefab>,
                                                           RefRO<ProjectilePoolState>>()
                                                   .WithEntityAccess())
        {
            if (!IsShooterEligibleForSpawn(entityManager, shooterEntity, shootRequests.Length, poolStateValue.ValueRO))
                continue;

            Entity prefabEntity = projectilePrefab.ValueRO.PrefabEntity;

            if (!IsValidPrefab(entityManager, prefabEntity))
            {
                shootRequests.Clear();
                continue;
            }

            PlayerPassiveToolsState passiveToolsState;
            ResolvePassiveToolsState(shooterEntity, in passiveToolsLookup, out passiveToolsState);
            int expandBatch = math.max(1, poolStateValue.ValueRO.ExpandBatch);

            // Partition demand by prefab so replacement projectiles never consume an incompatible pooled entity.
            for (int requestIndex = 0; requestIndex < shootRequests.Length; requestIndex++)
            {
                Entity requestPrefab = ProjectileSpawnPoolSelectionUtility.ResolveProjectilePrefab(in shootRequests.ElementAt(requestIndex),
                                                                                                    in passiveToolsState,
                                                                                                    prefabEntity,
                                                                                                    entityManager);
                bool alreadyCounted = false;

                for (int previousIndex = 0; previousIndex < requestIndex; previousIndex++)
                {
                    Entity previousPrefab = ProjectileSpawnPoolSelectionUtility.ResolveProjectilePrefab(in shootRequests.ElementAt(previousIndex),
                                                                                                          in passiveToolsState,
                                                                                                          prefabEntity,
                                                                                                          entityManager);

                    if (previousPrefab == requestPrefab)
                    {
                        alreadyCounted = true;
                        break;
                    }
                }

                if (alreadyCounted)
                    continue;

                int demand = 0;

                for (int candidateIndex = requestIndex; candidateIndex < shootRequests.Length; candidateIndex++)
                {
                    Entity candidatePrefab = ProjectileSpawnPoolSelectionUtility.ResolveProjectilePrefab(in shootRequests.ElementAt(candidateIndex),
                                                                                                           in passiveToolsState,
                                                                                                           prefabEntity,
                                                                                                           entityManager);

                    if (candidatePrefab == requestPrefab)
                        demand++;
                }

                int missingProjectiles = demand - ProjectileSpawnPoolSelectionUtility.CountAvailable(projectilePool, requestPrefab);

                if (missingProjectiles <= 0)
                    continue;

                expansionRequests.Add(new PoolExpansionRequest
                {
                    ShooterEntity = shooterEntity,
                    ProjectilePrefab = requestPrefab,
                    ExpandCount = math.max(expandBatch, missingProjectiles)
                });
            }
        }
    }

    /// <summary>
    /// Executes queued pool expansion requests after entity iteration to avoid structural-change exceptions.
    /// </summary>
    /// <param name="entityManager">EntityManager used for pool expansion operations.</param>
    /// <param name="expansionRequests">Queued expansion requests collected during query iteration.</param>

    private static void ExecutePoolExpansionRequests(EntityManager entityManager, in NativeList<PoolExpansionRequest> expansionRequests)
    {
        for (int requestIndex = 0; requestIndex < expansionRequests.Length; requestIndex++)
        {
            PoolExpansionRequest expansionRequest = expansionRequests[requestIndex];

            if (expansionRequest.ExpandCount <= 0)
                continue;

            if (!entityManager.Exists(expansionRequest.ShooterEntity))
                continue;

            if (entityManager.HasComponent<Projectile>(expansionRequest.ShooterEntity))
                continue;

            if (!entityManager.HasBuffer<ProjectilePoolElement>(expansionRequest.ShooterEntity))
                continue;

            if (!IsValidPrefab(entityManager, expansionRequest.ProjectilePrefab))
                continue;

            ProjectilePoolUtility.ExpandPool(entityManager,
                                             expansionRequest.ShooterEntity,
                                             expansionRequest.ProjectilePrefab,
                                             expansionRequest.ExpandCount);
        }
    }
    #endregion

    #region Request Processing
    /// <summary>
    /// Spawns projectiles for all pending shoot requests using already initialized pooled entities.
    /// </summary>
    /// <param name="state">Current ECS system state used by the shooter query.</param>
    /// <param name="entityManager">EntityManager used for component read/write operations.</param>
    /// <param name="elapsedTime">Current world time used by visual shooting pulses.</param>
    /// <param name="passiveToolsLookup">Read-only lookup for passive tool runtime state.</param>
    /// <param name="projectileSizePowerUpMultipliersLookup">Read-only per-power-up projectile-size provenance lookup.</param>
    /// <param name="shootingStateLookup">Mutable shooter animation-pulse lookup.</param>
    /// <param name="cameraShakeStateLookup">Mutable player fire-shake lookup.</param>
    /// <param name="projectileTransformLookup">Mutable pooled projectile transform lookup.</param>
    /// <param name="projectileLookup">Mutable projectile behavior lookup.</param>
    /// <param name="projectileRuntimeLookup">Mutable projectile range and lifetime lookup.</param>
    /// <param name="projectileContactStateLookup">Mutable contact-state lookup reset on reuse.</param>
    /// <param name="projectileOwnerLookup">Mutable owner and pool-partition lookup.</param>
    /// <param name="enemyProjectileOffscreenWarningLookup">Read-only enemy warning configuration lookup.</param>
    /// <param name="projectileOffscreenWarningLookup">Mutable projectile warning-state lookup.</param>
    /// <param name="projectileBaseScaleLookup">Read-only cached prefab scale lookup.</param>
    /// <param name="perfectCircleLookup">Mutable orbital trajectory lookup.</param>
    /// <param name="bounceLookup">Mutable bounce-state lookup.</param>
    /// <param name="splitLookup">Mutable split-state lookup.</param>
    /// <param name="elementalPayloadLookup">Mutable elemental payload lookup.</param>
    /// <param name="returnStateLookup">Mutable optional return-state lookup.</param>
    /// <param name="powerUpsStateLookup">Mutable active concurrency state lookup.</param>
    /// <param name="projectileActiveLookup">Mutable enableable projectile activity lookup.</param>
    /// <param name="projectileAttachedVfxConfigLookup">Read-only projectile-attached VFX lookup.</param>
    /// <param name="muzzleFlashVfxConfigLookup">Read-only muzzle-flash VFX lookup.</param>
    /// <param name="powerUpVfxRequestLookup">Mutable player VFX request-buffer lookup.</param>
    /// <param name="projectileHitHistoryLookup">Mutable per-projectile overlap history lookup.</param>
    /// <param name="returnPathLookup">Mutable optional outbound path-buffer lookup.</param>

    private void ProcessShootRequests(ref SystemState state,
                                      EntityManager entityManager,
                                      float elapsedTime,
                                      in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                      in BufferLookup<PlayerProjectileSizePowerUpMultiplierElement> projectileSizePowerUpMultipliersLookup,
                                      ref ComponentLookup<PlayerShootingState> shootingStateLookup,
                                      ref ComponentLookup<PlayerCameraShakeState> cameraShakeStateLookup,
                                      ref ComponentLookup<LocalTransform> projectileTransformLookup,
                                      ref ComponentLookup<Projectile> projectileLookup,
                                      ref ComponentLookup<ProjectileRuntimeState> projectileRuntimeLookup,
                                      ref ComponentLookup<ProjectileContactState> projectileContactStateLookup,
                                      ref ComponentLookup<ProjectileOwner> projectileOwnerLookup,
                                      in ComponentLookup<EnemyProjectileOffscreenWarningConfig> enemyProjectileOffscreenWarningLookup,
                                      ref ComponentLookup<ProjectileOffscreenWarningState> projectileOffscreenWarningLookup,
                                      in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup,
                                      ref ComponentLookup<ProjectilePerfectCircleState> perfectCircleLookup,
                                      ref ComponentLookup<ProjectileBounceState> bounceLookup,
                                      ref ComponentLookup<ProjectileSplitState> splitLookup,
                                      ref ComponentLookup<ProjectileElementalPayload> elementalPayloadLookup,
                                      ref ComponentLookup<ProjectileReturnState> returnStateLookup,
                                      ref ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup,
                                      ref ComponentLookup<ProjectileActive> projectileActiveLookup,
                                      in ComponentLookup<PlayerProjectileAttachedVfxConfig> projectileAttachedVfxConfigLookup,
                                      in ComponentLookup<PlayerMuzzleFlashVfxConfig> muzzleFlashVfxConfigLookup,
                                      ref BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup,
                                      ref BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup,
                                      ref BufferLookup<ProjectileReturnPathPoint> returnPathLookup)
    {
        foreach ((DynamicBuffer<ShootRequest> shootRequests,
                  DynamicBuffer<ProjectilePoolElement> projectilePool,
                  RefRO<ShooterProjectilePrefab> projectilePrefab,
                  RefRO<ProjectilePoolState> poolStateValue,
                  Entity shooterEntity) in SystemAPI.Query<DynamicBuffer<ShootRequest>,
                                                           DynamicBuffer<ProjectilePoolElement>,
                                                           RefRO<ShooterProjectilePrefab>,
                                                           RefRO<ProjectilePoolState>>()
                                                   .WithEntityAccess())
        {
            if (!IsShooterEligibleForSpawn(entityManager, shooterEntity, shootRequests.Length, poolStateValue.ValueRO))
                continue;

            DynamicBuffer<ShootRequest> shooterShootRequests = shootRequests;
            DynamicBuffer<ProjectilePoolElement> shooterProjectilePool = projectilePool;
            Entity prefabEntity = projectilePrefab.ValueRO.PrefabEntity;

            if (!IsValidPrefab(entityManager, prefabEntity))
            {
                shooterShootRequests.Clear();
                continue;
            }

            PlayerPassiveToolsState passiveToolsState;
            ResolvePassiveToolsState(shooterEntity,
                                     in passiveToolsLookup,
                                     out passiveToolsState);
            DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> projectileSizePowerUpMultipliers = projectileSizePowerUpMultipliersLookup.HasBuffer(shooterEntity)
                ? projectileSizePowerUpMultipliersLookup[shooterEntity]
                : default;
            int requestsCount = shooterShootRequests.Length;
            int spawnedProjectileCount = 0;

            // Captured from the first spawned primary projectile so the per-volley muzzle flash uses the real shot origin and direction.
            float3 muzzleFlashOrigin = float3.zero;
            quaternion muzzleFlashRotation = quaternion.identity;
            bool spawnedPrimaryShot = false;
            bool allowsMuzzleFlashVfx = false;

            for (int requestIndex = 0; requestIndex < requestsCount; requestIndex++)
            {
                ShootRequest request = shooterShootRequests[requestIndex];
                bool hasResolvedShotModifiers = request.ShotModifiers.HasResolvedModifiers != 0;
                bool hasPerfectCircle = hasResolvedShotModifiers
                    ? request.ShotModifiers.HasPerfectCircle != 0
                    : passiveToolsState.HasPerfectCircle != 0;
                PerfectCirclePassiveConfig perfectCircleConfig = hasResolvedShotModifiers
                    ? request.ShotModifiers.PerfectCircle
                    : passiveToolsState.PerfectCircle;
                bool hasBouncingProjectiles = hasResolvedShotModifiers
                    ? request.ShotModifiers.HasBouncingProjectiles != 0
                    : passiveToolsState.HasBouncingProjectiles != 0;
                BouncingProjectilesPassiveConfig bouncingProjectilesConfig = hasResolvedShotModifiers
                    ? request.ShotModifiers.BouncingProjectiles
                    : passiveToolsState.BouncingProjectiles;
                bool hasSplittingProjectiles = hasResolvedShotModifiers
                    ? request.ShotModifiers.HasSplittingProjectiles != 0
                    : passiveToolsState.HasSplittingProjectiles != 0;
                SplittingProjectilesPassiveConfig splittingProjectilesConfig = hasResolvedShotModifiers
                    ? request.ShotModifiers.SplittingProjectiles
                    : passiveToolsState.SplittingProjectiles;
                bool hasElementalProjectiles = hasResolvedShotModifiers
                    ? request.ShotModifiers.HasElementalProjectiles != 0
                    : passiveToolsState.HasElementalProjectiles != 0;
                ElementalProjectilesPassiveConfig elementalProjectilesConfig = hasResolvedShotModifiers
                    ? request.ShotModifiers.ElementalProjectiles
                    : passiveToolsState.ElementalProjectiles;
                Entity requestPrefab = ProjectileSpawnPoolSelectionUtility.ResolveProjectilePrefab(in request,
                                                                                                    in passiveToolsState,
                                                                                                    prefabEntity,
                                                                                                    entityManager);

                if (!ProjectileSpawnPoolSelectionUtility.TryAcquire(shooterProjectilePool,
                                                                    requestPrefab,
                                                                    out Entity projectileEntity))
                {
                    continue;
                }

                if (!entityManager.Exists(projectileEntity))
                    continue;

                float3 direction = math.normalizesafe(request.Direction, new float3(0f, 0f, 1f));
                float speed = math.max(0f, request.Speed);

                if (hasPerfectCircle)
                    speed = math.max(0f, perfectCircleConfig.RadialEntrySpeed);

                if (!projectileTransformLookup.HasComponent(projectileEntity))
                    continue;

                LocalTransform projectileTransform = projectileTransformLookup[projectileEntity];
                projectileTransform.Position = request.Position;
                projectileTransform.Rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));

                float baseScale = ResolveProjectileBaseScale(projectileEntity, projectileTransform.Scale, in projectileBaseScaleLookup);

                bool hasReturningProjectiles = ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request,
                                                                                                                   in passiveToolsState,
                                                                                                                   out ReturningProjectilesConfig returningProjectilesConfig);
                float embeddedPowerUpSizeMultiplier = request.ProjectileSizePowerUpMultiplier > 0f
                    ? request.ProjectileSizePowerUpMultiplier
                    : 1f;
                float appliedPowerUpSizeMultiplier = hasReturningProjectiles
                    ? ProjectileReturnPowerUpInteractionUtility.ResolveProjectileSizePowerUpMultiplier(in returningProjectilesConfig,
                                                                                                        embeddedPowerUpSizeMultiplier,
                                                                                                        projectileSizePowerUpMultipliers)
                    : embeddedPowerUpSizeMultiplier;
                float scaleMultiplier = math.max(0.01f,
                                                 request.ProjectileScaleMultiplier /
                                                 embeddedPowerUpSizeMultiplier *
                                                 appliedPowerUpSizeMultiplier) *
                                        (hasReturningProjectiles ? math.max(0.01f, returningProjectilesConfig.OutboundSizeMultiplier) : 1f);
                request.ProjectileSizePowerUpMultiplier = appliedPowerUpSizeMultiplier;
                projectileTransform.Scale = baseScale * scaleMultiplier;
                projectileTransformLookup[projectileEntity] = projectileTransform;

                Projectile projectileData = new Projectile
                {
                    Velocity = direction * speed,
                    Damage = math.max(0f, request.Damage),
                    ExplosionRadius = math.max(0f, request.ExplosionRadius),
                    MaxRange = request.Range * (hasReturningProjectiles
                        ? math.max(0.01f, returningProjectilesConfig.OutboundRangeMultiplier)
                        : 1f),
                    MaxLifetime = request.Lifetime * (hasReturningProjectiles
                        ? math.max(0.01f, returningProjectilesConfig.OutboundLifetimeMultiplier)
                        : 1f),
                    PenetrationMode = request.PenetrationMode,
                    RemainingPenetrations = math.max(0, request.MaxPenetrations),
                    KnockbackEnabled = request.KnockbackEnabled,
                    KnockbackStrength = math.max(0f, request.KnockbackStrength),
                    KnockbackDurationSeconds = math.max(0f, request.KnockbackDurationSeconds),
                    KnockbackDirectionMode = request.KnockbackDirectionMode,
                    KnockbackStackingMode = request.KnockbackStackingMode,
                    InheritPlayerSpeed = request.InheritPlayerSpeed,
                    IgnoreInheritedPlayerVelocityX = request.IgnoreInheritedPlayerVelocityX,
                    IgnoreInheritedPlayerVelocityZ = request.IgnoreInheritedPlayerVelocityZ
                };

                projectileLookup[projectileEntity] = projectileData;
                projectileRuntimeLookup[projectileEntity] = new ProjectileRuntimeState
                {
                    TraveledDistance = 0f,
                    ElapsedLifetime = 0f
                };
                projectileContactStateLookup[projectileEntity] = default;
                projectileOwnerLookup[projectileEntity] = new ProjectileOwner
                {
                    ShooterEntity = shooterEntity,
                    PoolPrefabEntity = requestPrefab
                };
                ProjectileSpawnInitializationUtility.ConfigureProjectileOffscreenWarning(projectileEntity,
                                                                                          shooterEntity,
                                                                                          in enemyProjectileOffscreenWarningLookup,
                                                                                          ref projectileOffscreenWarningLookup);
                ProjectileSpawnInitializationUtility.ResetProjectileHitHistory(projectileEntity, ref projectileHitHistoryLookup);

                ProjectilePerfectCircleState perfectCircleState = ProjectileSpawnInitializationUtility.BuildPerfectCircleState(in perfectCircleConfig,
                                                                                                                                 requestIndex,
                                                                                                                                 shooterEntity,
                                                                                                                                 request.Position,
                                                                                                                                 direction,
                                                                                                                                 projectileData.Velocity,
                                                                                                                                 request.OrbitLayerIndex,
                                                                                                                                 request.OrbitLayerCount,
                                                                                                                                 hasPerfectCircle);
                perfectCircleLookup[projectileEntity] = perfectCircleState;

                ProjectileBounceState bounceState = ProjectileSpawnInitializationUtility.BuildBounceState(in bouncingProjectilesConfig,
                                                                                                            hasBouncingProjectiles);
                bounceLookup[projectileEntity] = bounceState;

                ProjectileSplitState splitState = ProjectileSpawnInitializationUtility.BuildSplitState(in splittingProjectilesConfig,
                                                                                                         hasSplittingProjectiles,
                                                                                                        request.IsSplitChild != 0,
                                                                                                        hasReturningProjectiles,
                                                                                                        in returningProjectilesConfig);
                splitLookup[projectileEntity] = splitState;

                ProjectileElementalPayload elementalPayload = ProjectileSpawnInitializationUtility.ResolveElementalPayload(in request,
                                                                                                                             in elementalProjectilesConfig,
                                                                                                                             hasElementalProjectiles);
                elementalPayloadLookup[projectileEntity] = elementalPayload;
                ProjectileReturnRuntimeUtility.InitializeSpawnedProjectile(projectileEntity,
                                                                           shooterEntity,
                                                                           in request,
                                                                           in returningProjectilesConfig,
                                                                           hasReturningProjectiles,
                                                                           speed,
                                                                           projectileData.Damage,
                                                                           request.Position,
                                                                           ref returnStateLookup,
                                                                           ref powerUpsStateLookup,
                                                                           ref returnPathLookup);

                projectileActiveLookup.SetComponentEnabled(projectileEntity, true);
                if (ProjectileReturnVfxPolicyUtility.AllowsProjectileVfx(requestPrefab,
                                                                         hasReturningProjectiles,
                                                                         in returningProjectilesConfig))
                    ProjectileSpawnInitializationUtility.TryEnqueueProjectileAttachedVfx(shooterEntity,
                                                                                         projectileEntity,
                                                                                         in projectileTransform,
                                                                                         scaleMultiplier,
                                                                                         in projectileAttachedVfxConfigLookup,
                                                                                         ref powerUpVfxRequestLookup);

                // Cache the first spawned primary (non-split) shot pose so a single muzzle flash represents the whole volley.
                // Split-child projectiles spawn from despawn/hit points, not the muzzle, so they must not retrigger the flash.
                if (request.IsSplitChild == 0)
                {
                    if (!spawnedPrimaryShot)
                    {
                        muzzleFlashOrigin = projectileTransform.Position;
                        muzzleFlashRotation = projectileTransform.Rotation;
                        spawnedPrimaryShot = true;
                    }

                    allowsMuzzleFlashVfx |= ProjectileReturnVfxPolicyUtility.AllowsMuzzleFlashVfx(requestPrefab,
                                                                                                   hasReturningProjectiles,
                                                                                                   in returningProjectilesConfig);
                }

                spawnedProjectileCount++;
            }

            if (spawnedProjectileCount > 0)
                ProjectileSpawnInitializationUtility.RegisterShooterShotPulse(shooterEntity,
                                                                              elapsedTime,
                                                                              ref shootingStateLookup);

            // Only primary shots originate from the weapon muzzle, so split-child spawns never retrigger the flash.
            if (spawnedPrimaryShot)
            {
                if (allowsMuzzleFlashVfx)
                    ProjectileSpawnInitializationUtility.TryEnqueueMuzzleFlashVfx(shooterEntity,
                                                                                  muzzleFlashOrigin,
                                                                                  muzzleFlashRotation,
                                                                                  in muzzleFlashVfxConfigLookup,
                                                                                  ref powerUpVfxRequestLookup);
                // Same primary-shot gate keeps split-child spawns from retriggering the Fire Shake camera feedback.
                ProjectileSpawnInitializationUtility.EnqueueFireShakeRequest(shooterEntity, ref cameraShakeStateLookup);
            }

            shooterShootRequests.Clear();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether a shooter can be processed for pool expansion and request spawning.
    /// </summary>
    /// <param name="entityManager">EntityManager used for component existence checks.</param>
    /// <param name="shooterEntity">Shooter entity to inspect.</param>
    /// <param name="shootRequestsCount">Current number of queued shoot requests.</param>
    /// <param name="poolState">Current shooter projectile pool state.</param>
    /// <returns>True when shooter is valid and initialized for spawn processing.</returns>
    private static bool IsShooterEligibleForSpawn(EntityManager entityManager,
                                                  Entity shooterEntity,
                                                  int shootRequestsCount,
                                                  ProjectilePoolState poolState)
    {
        if (!entityManager.Exists(shooterEntity))
            return false;

        if (entityManager.HasComponent<Projectile>(shooterEntity))
            return false;

        if (shootRequestsCount <= 0)
            return false;

        if (poolState.Initialized == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Validates that a projectile prefab entity exists before it is used for pool expansion.
    /// </summary>
    /// <param name="entityManager">EntityManager used for existence checks.</param>
    /// <param name="prefabEntity">Candidate projectile prefab entity.</param>
    /// <returns>True when prefab is non-null and alive in the world.</returns>
    private static bool IsValidPrefab(EntityManager entityManager, Entity prefabEntity)
    {
        if (prefabEntity == Entity.Null)
            return false;

        return entityManager.Exists(prefabEntity);
    }

    /// <summary>
    /// Resolves cached projectile base scale without performing structural changes during query iteration.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity being spawned.</param>
    /// <param name="transformScale">Current LocalTransform scale fallback.</param>
    /// <param name="projectileBaseScaleLookup">Lookup used for cached projectile base scale reads.</param>
    /// <returns>Clamped base scale value used for spawn scale multiplier.</returns>
    private static float ResolveProjectileBaseScale(Entity projectileEntity,
                                                    float transformScale,
                                                    in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup)
    {
        if (projectileBaseScaleLookup.HasComponent(projectileEntity))
            return math.max(MinimumProjectileScale, projectileBaseScaleLookup[projectileEntity].Value);

        return math.max(MinimumProjectileScale, transformScale);
    }

    /// <summary>
    /// Reads the latest aggregated passive snapshot for one shooter without adding components or buffers.
    /// </summary>
    /// <param name="shooterEntity">Shooter whose passive state is required.</param>
    /// <param name="passiveToolsLookup">Read-only passive-state buffer lookup.</param>
    /// <param name="passiveToolsState">Resolved passive snapshot or default.</param>
    private static void ResolvePassiveToolsState(Entity shooterEntity,
                                                 in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                                 out PlayerPassiveToolsState passiveToolsState)
    {
        PlayerPassiveToolsStateBufferUtility.Read(shooterEntity,
                                                  in passiveToolsLookup,
                                                  out passiveToolsState);
    }

    #endregion

    #endregion

}
