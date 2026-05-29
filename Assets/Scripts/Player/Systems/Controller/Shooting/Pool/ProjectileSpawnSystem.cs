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
    private const float MinimumVfxScale = 0.01f;
    private const float MinimumVfxLifetimeSeconds = 0.05f;
    private const float VisualShootingPulseDuration = 0.12f;
    #endregion

    #region Fields
    private EntityQuery shootersWithRequestsQuery;
    #endregion


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

        // Two-phase flow: collect requests first, then apply structural pool growth outside query iteration.
        CollectPoolExpansionRequests(ref state, entityManager, ref expansionRequests);
        ExecutePoolExpansionRequests(entityManager, in expansionRequests);

        // Refresh lookups after structural changes performed during pool expansion.
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true);
        ComponentLookup<PlayerShootingState> shootingStateLookup = SystemAPI.GetComponentLookup<PlayerShootingState>(false);
        ComponentLookup<LocalTransform> projectileTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
        ComponentLookup<Projectile> projectileLookup = SystemAPI.GetComponentLookup<Projectile>(false);
        ComponentLookup<ProjectileRuntimeState> projectileRuntimeLookup = SystemAPI.GetComponentLookup<ProjectileRuntimeState>(false);
        ComponentLookup<ProjectileOwner> projectileOwnerLookup = SystemAPI.GetComponentLookup<ProjectileOwner>(false);
        ComponentLookup<EnemyProjectileOffscreenWarningConfig> enemyProjectileOffscreenWarningLookup = SystemAPI.GetComponentLookup<EnemyProjectileOffscreenWarningConfig>(true);
        ComponentLookup<ProjectileOffscreenWarningState> projectileOffscreenWarningLookup = SystemAPI.GetComponentLookup<ProjectileOffscreenWarningState>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<ProjectilePerfectCircleState> perfectCircleLookup = SystemAPI.GetComponentLookup<ProjectilePerfectCircleState>(false);
        ComponentLookup<ProjectileBounceState> bounceLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(false);
        ComponentLookup<ProjectileSplitState> splitLookup = SystemAPI.GetComponentLookup<ProjectileSplitState>(false);
        ComponentLookup<ProjectileElementalPayload> elementalPayloadLookup = SystemAPI.GetComponentLookup<ProjectileElementalPayload>(false);
        ComponentLookup<ProjectileActive> projectileActiveLookup = SystemAPI.GetComponentLookup<ProjectileActive>(false);
        ComponentLookup<PlayerProjectileAttachedVfxConfig> projectileAttachedVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerProjectileAttachedVfxConfig>(true);
        ComponentLookup<PlayerMuzzleFlashVfxConfig> muzzleFlashVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerMuzzleFlashVfxConfig>(true);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup = SystemAPI.GetBufferLookup<ProjectileHitHistoryElement>(false);

        ProcessShootRequests(ref state,
                             entityManager,
                             (float)SystemAPI.Time.ElapsedTime,
                             in passiveToolsLookup,
                             ref shootingStateLookup,
                             ref projectileTransformLookup,
                             ref projectileLookup,
                             ref projectileRuntimeLookup,
                             ref projectileOwnerLookup,
                             in enemyProjectileOffscreenWarningLookup,
                             ref projectileOffscreenWarningLookup,
                             in projectileBaseScaleLookup,
                             ref perfectCircleLookup,
                             ref bounceLookup,
                             ref splitLookup,
                             ref elementalPayloadLookup,
                             ref projectileActiveLookup,
                             in projectileAttachedVfxConfigLookup,
                             in muzzleFlashVfxConfigLookup,
                             ref powerUpVfxRequestLookup,
                             ref projectileHitHistoryLookup);
    }

    /// <summary>
    /// Collects pool expansion requests without applying structural changes during entity iteration.
    /// </summary>
    /// <param name="entityManager">EntityManager used to inspect shooter state and buffers.</param>
    /// <param name="expansionRequests">Mutable list that receives expansion requests.</param>

    private void CollectPoolExpansionRequests(ref SystemState state,
                                              EntityManager entityManager,
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

            int missingProjectiles = shootRequests.Length - projectilePool.Length;

            if (missingProjectiles <= 0)
                continue;

            int expandBatch = math.max(1, poolStateValue.ValueRO.ExpandBatch);
            int expandCount = math.max(expandBatch, missingProjectiles);
            expansionRequests.Add(new PoolExpansionRequest
            {
                ShooterEntity = shooterEntity,
                ProjectilePrefab = prefabEntity,
                ExpandCount = expandCount
            });
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

    /// <summary>
    /// Spawns projectiles for all pending shoot requests using already initialized pooled entities.
    /// </summary>
    /// <param name="entityManager">EntityManager used for component read/write operations.</param>
    /// <param name="passiveToolsLookup">Read-only lookup for passive tool runtime state.</param>

    private void ProcessShootRequests(ref SystemState state,
                                      EntityManager entityManager,
                                      float elapsedTime,
                                      in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                      ref ComponentLookup<PlayerShootingState> shootingStateLookup,
                                      ref ComponentLookup<LocalTransform> projectileTransformLookup,
                                      ref ComponentLookup<Projectile> projectileLookup,
                                      ref ComponentLookup<ProjectileRuntimeState> projectileRuntimeLookup,
                                      ref ComponentLookup<ProjectileOwner> projectileOwnerLookup,
                                      in ComponentLookup<EnemyProjectileOffscreenWarningConfig> enemyProjectileOffscreenWarningLookup,
                                      ref ComponentLookup<ProjectileOffscreenWarningState> projectileOffscreenWarningLookup,
                                      in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup,
                                      ref ComponentLookup<ProjectilePerfectCircleState> perfectCircleLookup,
                                      ref ComponentLookup<ProjectileBounceState> bounceLookup,
                                      ref ComponentLookup<ProjectileSplitState> splitLookup,
                                      ref ComponentLookup<ProjectileElementalPayload> elementalPayloadLookup,
                                      ref ComponentLookup<ProjectileActive> projectileActiveLookup,
                                      in ComponentLookup<PlayerProjectileAttachedVfxConfig> projectileAttachedVfxConfigLookup,
                                      in ComponentLookup<PlayerMuzzleFlashVfxConfig> muzzleFlashVfxConfigLookup,
                                      ref BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup,
                                      ref BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup)
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
            int requestsCount = shooterShootRequests.Length;
            int spawnedProjectileCount = 0;

            // Captured from the first spawned projectile so the per-volley muzzle flash uses the real shot origin and direction.
            float3 muzzleFlashOrigin = float3.zero;
            quaternion muzzleFlashRotation = quaternion.identity;

            for (int requestIndex = 0; requestIndex < requestsCount; requestIndex++)
            {
                if (shooterProjectilePool.Length == 0)
                    break;

                // The pool works as a stack so acquire is O(1) without shifting buffer contents.
                int lastIndex = shooterProjectilePool.Length - 1;
                Entity projectileEntity = shooterProjectilePool[lastIndex].ProjectileEntity;
                shooterProjectilePool.RemoveAt(lastIndex);

                if (!entityManager.Exists(projectileEntity))
                    continue;

                ShootRequest request = shooterShootRequests[requestIndex];
                float3 direction = math.normalizesafe(request.Direction, new float3(0f, 0f, 1f));
                float speed = math.max(0f, request.Speed);

                if (passiveToolsState.HasPerfectCircle != 0)
                    speed = math.max(0f, passiveToolsState.PerfectCircle.RadialEntrySpeed);

                if (!projectileTransformLookup.HasComponent(projectileEntity))
                    continue;

                LocalTransform projectileTransform = projectileTransformLookup[projectileEntity];
                projectileTransform.Position = request.Position;
                projectileTransform.Rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));

                float baseScale = ResolveProjectileBaseScale(projectileEntity, projectileTransform.Scale, in projectileBaseScaleLookup);

                float scaleMultiplier = math.max(0.01f, request.ProjectileScaleMultiplier);
                projectileTransform.Scale = baseScale * scaleMultiplier;
                projectileTransformLookup[projectileEntity] = projectileTransform;

                Projectile projectileData = new Projectile
                {
                    Velocity = direction * speed,
                    Damage = math.max(0f, request.Damage),
                    ExplosionRadius = math.max(0f, request.ExplosionRadius),
                    MaxRange = request.Range,
                    MaxLifetime = request.Lifetime,
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
                projectileOwnerLookup[projectileEntity] = new ProjectileOwner
                {
                    ShooterEntity = shooterEntity
                };
                ConfigureProjectileOffscreenWarning(projectileEntity,
                                                   shooterEntity,
                                                   in enemyProjectileOffscreenWarningLookup,
                                                   ref projectileOffscreenWarningLookup);
                ResetProjectileHitHistory(projectileEntity, ref projectileHitHistoryLookup);

                ProjectilePerfectCircleState perfectCircleState = BuildPerfectCircleState(in passiveToolsState.PerfectCircle,
                                                                                          requestIndex,
                                                                                          shooterEntity,
                                                                                          request.Position,
                                                                                          direction,
                                                                                          projectileData.Velocity,
                                                                                          request.OrbitLayerIndex,
                                                                                          request.OrbitLayerCount,
                                                                                          passiveToolsState.HasPerfectCircle != 0);
                perfectCircleLookup[projectileEntity] = perfectCircleState;

                ProjectileBounceState bounceState = BuildBounceState(in passiveToolsState.BouncingProjectiles, passiveToolsState.HasBouncingProjectiles != 0);
                bounceLookup[projectileEntity] = bounceState;

                ProjectileSplitState splitState = BuildSplitState(in passiveToolsState.SplittingProjectiles, passiveToolsState.HasSplittingProjectiles != 0, request.IsSplitChild != 0);
                splitLookup[projectileEntity] = splitState;

                ProjectileElementalPayload elementalPayload = ResolveElementalPayload(in request,
                                                                                      in passiveToolsState.ElementalProjectiles,
                                                                                      passiveToolsState.HasElementalProjectiles != 0);
                elementalPayloadLookup[projectileEntity] = elementalPayload;

                projectileActiveLookup.SetComponentEnabled(projectileEntity, true);
                TryEnqueueProjectileAttachedVfx(shooterEntity,
                                                projectileEntity,
                                                in projectileTransform,
                                                scaleMultiplier,
                                                in projectileAttachedVfxConfigLookup,
                                                ref powerUpVfxRequestLookup);

                // Cache the first spawned shot pose so a single muzzle flash represents the whole volley.
                if (spawnedProjectileCount == 0)
                {
                    muzzleFlashOrigin = projectileTransform.Position;
                    muzzleFlashRotation = projectileTransform.Rotation;
                }

                spawnedProjectileCount++;
            }

            if (spawnedProjectileCount > 0)
            {
                RegisterShooterShotPulse(shooterEntity, elapsedTime, ref shootingStateLookup);
                TryEnqueueMuzzleFlashVfx(shooterEntity,
                                         muzzleFlashOrigin,
                                         muzzleFlashRotation,
                                         in muzzleFlashVfxConfigLookup,
                                         ref powerUpVfxRequestLookup);
            }

            shooterShootRequests.Clear();
        }
    }

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

    private static void ResolvePassiveToolsState(Entity shooterEntity,
                                                 in BufferLookup<PlayerPassiveToolsStateElement> passiveToolsLookup,
                                                 out PlayerPassiveToolsState passiveToolsState)
    {
        PlayerPassiveToolsStateBufferUtility.Read(shooterEntity,
                                                  in passiveToolsLookup,
                                                  out passiveToolsState);
    }

    /// <summary>
    /// Resets and enables projectile offscreen-warning state when the shooter has an enemy warning config.
    /// </summary>
    /// <param name="projectileEntity">Pooled projectile being reactivated.</param>
    /// <param name="shooterEntity">Shooter that owns the projectile spawn request.</param>
    /// <param name="enemyProjectileOffscreenWarningLookup">Read-only enemy warning config lookup.</param>
    /// <param name="projectileOffscreenWarningLookup">Mutable projectile warning-state lookup.</param>
    private static void ConfigureProjectileOffscreenWarning(Entity projectileEntity,
                                                           Entity shooterEntity,
                                                           in ComponentLookup<EnemyProjectileOffscreenWarningConfig> enemyProjectileOffscreenWarningLookup,
                                                           ref ComponentLookup<ProjectileOffscreenWarningState> projectileOffscreenWarningLookup)
    {
        if (!projectileOffscreenWarningLookup.HasComponent(projectileEntity))
            return;

        ProjectileOffscreenWarningState warningState = default;

        if (enemyProjectileOffscreenWarningLookup.HasComponent(shooterEntity) &&
            enemyProjectileOffscreenWarningLookup[shooterEntity].Enabled != 0)
        {
            warningState.Enabled = 1;
        }

        projectileOffscreenWarningLookup[projectileEntity] = warningState;
    }

    /// <summary>
    /// Clears per-projectile enemy hit memory before a pooled projectile is reused for a new shot.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity being reactivated from the pool.</param>
    /// <param name="projectileHitHistoryLookup">Lookup used to resolve the projectile hit-history buffer.</param>
    private static void ResetProjectileHitHistory(Entity projectileEntity,
                                                  ref BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup)
    {
        if (!projectileHitHistoryLookup.HasBuffer(projectileEntity))
            return;

        DynamicBuffer<ProjectileHitHistoryElement> hitHistory = projectileHitHistoryLookup[projectileEntity];
        hitHistory.Clear();
    }

    /// <summary>
    /// Records a real projectile spawn as a shoot pulse so managed animation sync can trigger one-shot firing clips.
    /// </summary>
    /// <param name="shooterEntity">Shooter entity whose animation state should be pulsed.</param>
    /// <param name="elapsedTime">Current elapsed world time used to hold the shooting visual state briefly.</param>
    /// <param name="shootingStateLookup">Mutable lookup used to update shooter shooting state.</param>
    private static void RegisterShooterShotPulse(Entity shooterEntity,
                                                 float elapsedTime,
                                                 ref ComponentLookup<PlayerShootingState> shootingStateLookup)
    {
        if (!shootingStateLookup.HasComponent(shooterEntity))
            return;

        PlayerShootingState shootingState = shootingStateLookup[shooterEntity];
        shootingState.ShotPulseVersion = shootingState.ShotPulseVersion == uint.MaxValue
            ? 1u
            : shootingState.ShotPulseVersion + 1u;
        shootingState.VisualShootingActive = 1;
        shootingState.VisualShootingUntilTime = math.max(shootingState.VisualShootingUntilTime,
                                                         elapsedTime + VisualShootingPulseDuration);
        shootingStateLookup[shooterEntity] = shootingState;
    }

    private static ProjectilePerfectCircleState BuildPerfectCircleState(in PerfectCirclePassiveConfig perfectCircleConfig,
                                                                        int requestIndex,
                                                                        Entity shooterEntity,
                                                                        float3 spawnPosition,
                                                                        float3 direction,
                                                                        float3 entryVelocity,
                                                                        int orbitLayerIndex,
                                                                        int orbitLayerCount,
                                                                        bool isEnabled)
    {
        if (!isEnabled)
            return default;

        int safeOrbitLayerCount = math.max(1, orbitLayerCount);
        float seed = requestIndex + shooterEntity.Index * 13f;
        float angleRadians = math.radians(math.max(0f, perfectCircleConfig.GoldenAngleDegrees) * seed);
        float3 radialDirection = direction;

        if (math.lengthsq(radialDirection) <= 1e-6f)
            radialDirection = new float3(math.cos(angleRadians), 0f, math.sin(angleRadians));

        radialDirection = math.normalizesafe(radialDirection, new float3(0f, 0f, 1f));

        return new ProjectilePerfectCircleState
        {
            Enabled = 1,
            HasEnteredOrbit = 0,
            CompletedFullOrbit = 0,
            HasOrbitPlaneHeight = 0,
            EntryOrigin = spawnPosition,
            OrbitAngle = angleRadians,
            OrbitBlendProgress = 0f,
            CurrentRadius = 0f,
            AccumulatedOrbitRadians = 0f,
            RadialDirection = radialDirection,
            EntryVelocity = entryVelocity,
            OrbitPlaneHeight = 0f,
            OrbitLayerIndex = math.clamp(orbitLayerIndex, 0, safeOrbitLayerCount - 1),
            OrbitLayerCount = safeOrbitLayerCount
        };
    }

    private static ProjectileBounceState BuildBounceState(in BouncingProjectilesPassiveConfig bouncingProjectilesConfig, bool isEnabled)
    {
        if (!isEnabled || bouncingProjectilesConfig.MaxBounces <= 0)
            return default;

        float minimumSpeedMultiplier = math.max(0f, bouncingProjectilesConfig.MinimumSpeedMultiplierAfterBounce);
        float maximumSpeedMultiplier = math.max(minimumSpeedMultiplier, bouncingProjectilesConfig.MaximumSpeedMultiplierAfterBounce);

        return new ProjectileBounceState
        {
            RemainingBounces = math.max(0, bouncingProjectilesConfig.MaxBounces),
            SpeedPercentChangePerBounce = bouncingProjectilesConfig.SpeedPercentChangePerBounce,
            MinimumSpeedMultiplierAfterBounce = minimumSpeedMultiplier,
            MaximumSpeedMultiplierAfterBounce = maximumSpeedMultiplier,
            CurrentSpeedMultiplier = 1f
        };
    }

    private static ProjectileSplitState BuildSplitState(in SplittingProjectilesPassiveConfig splittingProjectilesConfig, bool isEnabled, bool isSplitChild)
    {
        if (!isEnabled || isSplitChild)
            return default;

        return new ProjectileSplitState
        {
            CanSplit = 1,
            TriggerMode = splittingProjectilesConfig.TriggerMode,
            DirectionMode = splittingProjectilesConfig.DirectionMode,
            SplitProjectileCount = math.max(1, splittingProjectilesConfig.SplitProjectileCount),
            SplitOffsetDegrees = splittingProjectilesConfig.SplitOffsetDegrees,
            CustomAnglesDegrees = splittingProjectilesConfig.CustomAnglesDegrees,
            SplitDamageMultiplier = math.max(0f, splittingProjectilesConfig.SplitDamageMultiplier),
            SplitSizeMultiplier = math.max(0f, splittingProjectilesConfig.SplitSizeMultiplier),
            SplitSpeedMultiplier = math.max(0f, splittingProjectilesConfig.SplitSpeedMultiplier),
            SplitLifetimeMultiplier = math.max(0f, splittingProjectilesConfig.SplitLifetimeMultiplier)
        };
    }

    private static ProjectileElementalPayload ResolveElementalPayload(in ShootRequest request,
                                                                      in ElementalProjectilesPassiveConfig passiveElementalProjectilesConfig,
                                                                      bool hasPassiveElementalPayload)
    {
        ProjectileElementalPayload resolvedPayload = BuildElementalPayloadFromRequest(in request);
        ProjectileElementalPayload passivePayload = BuildElementalPayloadFromPassive(in passiveElementalProjectilesConfig, hasPassiveElementalPayload);
        ProjectileElementalPayloadUtility.MergePayload(ref resolvedPayload, in passivePayload);
        return resolvedPayload;
    }

    private static ProjectileElementalPayload BuildElementalPayloadFromRequest(in ShootRequest request)
    {
        return request.ElementalPayloadOverride;
    }

    private static ProjectileElementalPayload BuildElementalPayloadFromPassive(in ElementalProjectilesPassiveConfig elementalProjectilesConfig, bool isEnabled)
    {
        if (!isEnabled || elementalProjectilesConfig.StacksPerHit <= 0f)
            return default;

        return ProjectileElementalPayloadUtility.BuildSingle(in elementalProjectilesConfig.Effect,
                                                             math.max(0f, elementalProjectilesConfig.StacksPerHit));
    }

    /// <summary>
    /// Queues an attached managed VFX request for a newly activated projectile when the shooter visual preset provides one.
    /// </summary>
    /// <param name="shooterEntity">Player entity that owns the projectile and VFX request buffer.</param>
    /// <param name="projectileEntity">Projectile entity followed by the VFX until despawn.</param>
    /// <param name="projectileTransform">Initial projectile transform used for request placement.</param>
    /// <param name="projectileScaleMultiplier">Projectile size multiplier already applied to the spawned projectile transform.</param>
    /// <param name="projectileAttachedVfxConfigLookup">Read-only lookup for optional projectile VFX config.</param>
    /// <param name="powerUpVfxRequestLookup">Writable lookup for player-owned VFX request buffers.</param>
    private static void TryEnqueueProjectileAttachedVfx(Entity shooterEntity,
                                                        Entity projectileEntity,
                                                        in LocalTransform projectileTransform,
                                                        float projectileScaleMultiplier,
                                                        in ComponentLookup<PlayerProjectileAttachedVfxConfig> projectileAttachedVfxConfigLookup,
                                                        ref BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup)
    {
        if (!projectileAttachedVfxConfigLookup.HasComponent(shooterEntity))
            return;

        if (!powerUpVfxRequestLookup.HasBuffer(shooterEntity))
            return;

        PlayerProjectileAttachedVfxConfig config = projectileAttachedVfxConfigLookup[shooterEntity];

        if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
            return;

        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(projectileTransform.Rotation);
        float resolvedProjectileScaleMultiplier = math.max(MinimumVfxScale, projectileScaleMultiplier);
        float3 scaledSpawnOffset = config.SpawnOffset * resolvedProjectileScaleMultiplier;
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = powerUpVfxRequestLookup[shooterEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.SourcePrefab,
            Position = projectileTransform.Position + math.rotate(rotation, scaledSpawnOffset),
            Rotation = rotation,
            UniformScale = math.max(MinimumVfxScale, config.UniformScale * resolvedProjectileScaleMultiplier),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, config.LifetimeSeconds),
            FollowTargetEntity = projectileEntity,
            FollowPositionOffset = scaledSpawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            KeepAliveWhileFollowTargetValid = 1,
            FollowMuzzlePose = 1
        });
    }

    /// <summary>
    /// Queues one muzzle-flash VFX request at the shot origin once per volley when the shooter visual preset provides one.
    /// The request follows the muzzle pose for its short authored lifetime so the flash stays attached to the weapon while the player moves.
    /// </summary>
    /// <param name="shooterEntity">Player entity that owns the muzzle-flash config and VFX request buffer.</param>
    /// <param name="muzzleOrigin">World-space projectile origin captured from the first spawned shot.</param>
    /// <param name="muzzleRotation">World-space shot rotation captured from the first spawned shot.</param>
    /// <param name="muzzleFlashVfxConfigLookup">Read-only lookup for the optional muzzle-flash VFX config.</param>
    /// <param name="powerUpVfxRequestLookup">Writable lookup for player-owned VFX request buffers.</param>
    private static void TryEnqueueMuzzleFlashVfx(Entity shooterEntity,
                                                 float3 muzzleOrigin,
                                                 quaternion muzzleRotation,
                                                 in ComponentLookup<PlayerMuzzleFlashVfxConfig> muzzleFlashVfxConfigLookup,
                                                 ref BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup)
    {
        if (!muzzleFlashVfxConfigLookup.HasComponent(shooterEntity))
            return;

        if (!powerUpVfxRequestLookup.HasBuffer(shooterEntity))
            return;

        PlayerMuzzleFlashVfxConfig config = muzzleFlashVfxConfigLookup[shooterEntity];

        if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
            return;

        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(muzzleRotation);
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = powerUpVfxRequestLookup[shooterEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.SourcePrefab,
            Position = muzzleOrigin + math.rotate(rotation, config.SpawnOffset),
            Rotation = rotation,
            UniformScale = math.max(MinimumVfxScale, config.UniformScale),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, config.LifetimeSeconds),
            FollowTargetEntity = shooterEntity,
            FollowPositionOffset = config.SpawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            FollowMuzzlePose = 1
        });
    }

    #endregion

}
