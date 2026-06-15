using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves projectile hits against active enemies using a spatial hash.
/// Applies damage, elemental payloads and split-projectile generation.
/// Each projectile can hit all enemies overlapped by its runtime impact radius.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
[BurstCompile]
public partial struct EnemyProjectileHitSystem : ISystem
{
    #region Constants
    private const float BaseProjectileHitRadius = 0.05f;
    #endregion

    #region Nested Types
    private struct ProjectileHitCandidate
    {
        public int EnemyIndex;
        public float DistanceSquared;
    }
    #endregion

    #region Fields
    private EntityQuery enemyQuery;
    private EntityQuery projectileQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates cached enemy and projectile queries required by the spatial hit-resolution pass.
    /// </summary>
    /// <param name="state">Mutable system state used to build and require runtime queries.</param>
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, EnemyKnockbackState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        projectileQuery = SystemAPI.QueryBuilder()
            .WithAll<Projectile, ProjectileOwner, ProjectileSplitState, ProjectileElementalPayload, LocalTransform, ProjectileActive>()
            .Build();

        state.RequireForUpdate(enemyQuery);
        state.RequireForUpdate(projectileQuery);
    }

    /// <summary>
    /// Resolves projectile overlaps, applies projected damage and payloads, then commits enemy feedback once per frame.
    /// </summary>
    /// <param name="state">Mutable system state used to read and commit projectile hit results.</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
            return;

        int projectileCount = projectileQuery.CalculateEntityCount();

        if (projectileCount <= 0)
            return;

        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(frameAllocator);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(frameAllocator);
        NativeArray<EnemyHealth> enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(frameAllocator);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = enemyQuery.ToComponentDataArray<EnemyRuntimeState>(frameAllocator);
        NativeArray<EnemyKnockbackState> enemyKnockbackArray = enemyQuery.ToComponentDataArray<EnemyKnockbackState>(frameAllocator);
        enemyCount = enemyEntities.Length;

        if (enemyCount <= 0)
            return;

        NativeArray<Entity> projectileEntities = projectileQuery.ToEntityArray(frameAllocator);
        NativeArray<Projectile> projectileDataArray = projectileQuery.ToComponentDataArray<Projectile>(frameAllocator);
        NativeArray<ProjectileOwner> projectileOwnerArray = projectileQuery.ToComponentDataArray<ProjectileOwner>(frameAllocator);
        NativeArray<ProjectileSplitState> projectileSplitArray = projectileQuery.ToComponentDataArray<ProjectileSplitState>(frameAllocator);
        NativeArray<ProjectileElementalPayload> projectileElementalArray = projectileQuery.ToComponentDataArray<ProjectileElementalPayload>(frameAllocator);
        NativeArray<LocalTransform> projectileTransforms = projectileQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
        int projectileWriteIndex = 0;
        ComponentLookup<PlayerControllerConfig> playerControllerLookup = SystemAPI.GetComponentLookup<PlayerControllerConfig>(true);

        for (int readIndex = 0; readIndex < projectileCount; readIndex++)
        {
            Entity shooterEntity = projectileOwnerArray[readIndex].ShooterEntity;

            if (!playerControllerLookup.HasComponent(shooterEntity))
                continue;

            if (projectileWriteIndex != readIndex)
            {
                projectileEntities[projectileWriteIndex] = projectileEntities[readIndex];
                projectileDataArray[projectileWriteIndex] = projectileDataArray[readIndex];
                projectileOwnerArray[projectileWriteIndex] = projectileOwnerArray[readIndex];
                projectileSplitArray[projectileWriteIndex] = projectileSplitArray[readIndex];
                projectileElementalArray[projectileWriteIndex] = projectileElementalArray[readIndex];
                projectileTransforms[projectileWriteIndex] = projectileTransforms[readIndex];
            }

            projectileWriteIndex++;
        }

        projectileCount = projectileWriteIndex;

        if (projectileCount <= 0)
            return;

        NativeArray<float3> enemyPositions = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> enemyRadiusAxes = CollectionHelper.CreateNativeArray<float2>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);

        float maxEnemyRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            EnemyData currentEnemyData = enemyDataArray[enemyIndex];
            LocalTransform currentEnemyTransform = enemyTransforms[enemyIndex];
            enemyPositions[enemyIndex] = EnemyHitboxCenterUtility.ResolveWorldCenter(in currentEnemyTransform, in currentEnemyData);
            float bodyRadiusX = math.max(0.05f, currentEnemyData.BodyRadiusX);
            float bodyRadiusZ = math.max(0.05f, currentEnemyData.BodyRadiusZ);
            float bodyRadius = math.max(math.max(0.05f, currentEnemyData.BodyRadius), math.max(bodyRadiusX, bodyRadiusZ));
            enemyRadiusAxes[enemyIndex] = new float2(bodyRadiusX, bodyRadiusZ);

            if (bodyRadius > maxEnemyRadius)
            {
                maxEnemyRadius = bodyRadius;
            }
        }

        float cellSize = EnemySpatialHashUtility.ResolveCellSize(maxEnemyRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, frameAllocator);
        EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);

        NativeArray<float3> projectilePositions = CollectionHelper.CreateNativeArray<float3>(projectileCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> projectileRadii = CollectionHelper.CreateNativeArray<float>(projectileCount, frameAllocator, NativeArrayOptions.UninitializedMemory);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            projectilePositions[projectileIndex] = projectileTransforms[projectileIndex].Position;
            float projectileScale = math.max(0.01f, projectileTransforms[projectileIndex].Scale);
            float explosionRadius = math.max(0f, projectileDataArray[projectileIndex].ExplosionRadius);
            projectileRadii[projectileIndex] = math.max(0.005f, BaseProjectileHitRadius * projectileScale + explosionRadius);
        }

        NativeStream projectileHitStream = new NativeStream(projectileCount, frameAllocator);

        EnemyProjectileHitCollectJob hitCollectJob = new EnemyProjectileHitCollectJob
        {
            ProjectilePositions = projectilePositions,
            ProjectileRadii = projectileRadii,
            EnemyPositions = enemyPositions,
            EnemyRadiusAxes = enemyRadiusAxes,
            CellMap = enemyCellMap,
            InverseCellSize = inverseCellSize,
            MaxEnemyRadius = maxEnemyRadius,
            HitStreamWriter = projectileHitStream.AsWriter()
        };

        JobHandle hitCollectHandle = hitCollectJob.Schedule(projectileCount, 64, state.Dependency);
        hitCollectHandle.Complete();

        NativeArray<EnemyHealth> projectedEnemyHealth = CollectionHelper.CreateNativeArray<EnemyHealth>(enemyHealthArray, frameAllocator);
        NativeArray<EnemyKnockbackState> projectedEnemyKnockback = CollectionHelper.CreateNativeArray<EnemyKnockbackState>(enemyKnockbackArray, frameAllocator);
        NativeArray<float3> enemyElasticHitDirections = CollectionHelper.CreateNativeArray<float3>(enemyCount,
                                                                                                   frameAllocator,
                                                                                                   NativeArrayOptions.ClearMemory);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup = SystemAPI.GetBufferLookup<ProjectileHitHistoryElement>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<ProjectileContactState> projectileContactStateLookup = SystemAPI.GetComponentLookup<ProjectileContactState>(false);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyHitVfxConfig>(true);
        ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup = SystemAPI.GetComponentLookup<EnemySpawnInactivityLock>(true);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        NativeStream.Reader projectileHitReader = projectileHitStream.AsReader();
        NativeList<int> currentOverlapEnemyIndices = new NativeList<int>(16, frameAllocator);
        NativeList<ProjectileHitCandidate> hitCandidates = new NativeList<ProjectileHitCandidate>(16, frameAllocator);

        // Hits are resolved sequentially per projectile so penetration modes can update projected health correctly.
        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            int hitCount = projectileHitReader.BeginForEachIndex(projectileIndex);
            Entity projectileEntity = projectileEntities[projectileIndex];
            Projectile projectileData = projectileDataArray[projectileIndex];
            bool needsHitHistory = projectileData.PenetrationMode != ProjectilePenetrationMode.None;
            bool canTrackProjectileHits = needsHitHistory && projectileHitHistoryLookup.HasBuffer(projectileEntity);
            DynamicBuffer<ProjectileHitHistoryElement> projectileHitHistory = default;

            if (canTrackProjectileHits)
                projectileHitHistory = projectileHitHistoryLookup[projectileEntity];

            if (hitCount <= 0)
            {
                projectileHitReader.EndForEachIndex();

                if (canTrackProjectileHits)
                    projectileHitHistory.Clear();

                continue;
            }

            ProjectileOwner projectileOwner = projectileOwnerArray[projectileIndex];
            ProjectileSplitState splitState = projectileSplitArray[projectileIndex];
            ProjectileElementalPayload elementalPayload = projectileElementalArray[projectileIndex];
            LocalTransform projectileTransform = projectileTransforms[projectileIndex];
            float currentScaleMultiplier = ResolveCurrentScaleMultiplier(projectileEntities[projectileIndex],
                                                                        projectileTransform.Scale,
                                                                        in projectileBaseScaleLookup);
            bool hasValidHit = false;
            bool canProjectileContinue = false;
            bool enemyKilledByProjectile = false;
            bool canEnqueueShooterVfxRequests = vfxRequestLookup.HasBuffer(projectileOwner.ShooterEntity);
            DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests = default;

            if (canEnqueueShooterVfxRequests)
                shooterVfxRequests = vfxRequestLookup[projectileOwner.ShooterEntity];

            currentOverlapEnemyIndices.Clear();
            hitCandidates.Clear();

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                int enemyIndex = projectileHitReader.Read<int>();

                if (enemyIndex < 0 || enemyIndex >= enemyCount)
                    continue;

                if (HasCurrentOverlapEnemyIndex(currentOverlapEnemyIndices, enemyIndex))
                    continue;

                currentOverlapEnemyIndices.Add(enemyIndex);
            }

            projectileHitReader.EndForEachIndex();
            PruneProjectileHitHistoryToCurrentOverlaps(canTrackProjectileHits,
                                                       currentOverlapEnemyIndices,
                                                       enemyEntities,
                                                       ref projectileHitHistory);

            for (int overlapIndex = 0; overlapIndex < currentOverlapEnemyIndices.Length; overlapIndex++)
            {
                int enemyIndex = currentOverlapEnemyIndices[overlapIndex];

                if (canTrackProjectileHits && HasProjectileAlreadyHitEnemy(projectileHitHistory, enemyEntities[enemyIndex]))
                    continue;

                if (HasHitCandidate(hitCandidates, enemyIndex))
                    continue;

                float3 delta = projectilePositions[projectileIndex] - enemyPositions[enemyIndex];
                delta.y = 0f;
                AddHitCandidateSorted(ref hitCandidates, enemyIndex, math.lengthsq(delta));
            }

            if (hitCandidates.Length <= 0)
                continue;

            switch (projectileData.PenetrationMode)
            {
                case ProjectilePenetrationMode.Infinite:
                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;

                        if (!TryApplyFlatDamageHit(ref projectedEnemyHealth, enemyIndex, math.max(0f, projectileData.Damage), out bool enemyKilled))
                            continue;

                        hasValidHit = true;
                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        RegisterProjectileEnemyHit(canTrackProjectileHits,
                                                   enemyEntities[enemyIndex],
                                                   ref projectileHitHistory);
                        ApplyHitPayloads(enemyIndex,
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         enemyDataArray,
                                         ref projectedEnemyKnockback,
                                         ref enemyElasticHitDirections,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);
                    }

                    canProjectileContinue = hasValidHit;
                    break;
                case ProjectilePenetrationMode.FixedHits:
                    int originalRemainingPenetrations = math.max(0, projectileData.RemainingPenetrations);
                    int maximumHitCount = 1 + originalRemainingPenetrations;
                    int appliedHitCount = 0;

                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length && appliedHitCount < maximumHitCount; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;

                        if (!TryApplyFlatDamageHit(ref projectedEnemyHealth, enemyIndex, math.max(0f, projectileData.Damage), out bool enemyKilled))
                            continue;

                        hasValidHit = true;
                        appliedHitCount++;
                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        RegisterProjectileEnemyHit(canTrackProjectileHits,
                                                   enemyEntities[enemyIndex],
                                                   ref projectileHitHistory);
                        ApplyHitPayloads(enemyIndex,
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         enemyDataArray,
                                         ref projectedEnemyKnockback,
                                         ref enemyElasticHitDirections,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);
                    }

                    projectileData.RemainingPenetrations = math.max(0, originalRemainingPenetrations - appliedHitCount);
                    canProjectileContinue = hasValidHit && appliedHitCount <= originalRemainingPenetrations;
                    break;
                case ProjectilePenetrationMode.DamageBased:
                    int originalDamagePenetrations = math.max(0, projectileData.RemainingPenetrations);
                    int consumedPenetrations = 0;
                    float remainingDamage = math.max(0f, projectileData.Damage);

                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;
                        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

                        if (enemyHealth.Current <= 0f)
                            continue;

                        hasValidHit = true;
                        bool enemyKilled = false;
                        float leftoverDamage = 0f;

                        if (remainingDamage > 0f)
                        {
                            leftoverDamage = ApplyDamageBasedHit(ref projectedEnemyHealth, enemyIndex, remainingDamage, out enemyKilled);
                        }

                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        RegisterProjectileEnemyHit(canTrackProjectileHits,
                                                   enemyEntities[enemyIndex],
                                                   ref projectileHitHistory);
                        ApplyHitPayloads(enemyIndex,
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         enemyDataArray,
                                         ref projectedEnemyKnockback,
                                         ref enemyElasticHitDirections,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);

                        if (remainingDamage <= 0f || leftoverDamage <= 0f || !enemyKilled)
                        {
                            remainingDamage = 0f;
                            break;
                        }

                        consumedPenetrations++;
                        remainingDamage = leftoverDamage;

                        if (consumedPenetrations > originalDamagePenetrations)
                        {
                            remainingDamage = 0f;
                            break;
                        }
                    }

                    projectileData.Damage = remainingDamage;
                    projectileData.RemainingPenetrations = math.max(0, originalDamagePenetrations - consumedPenetrations);
                    canProjectileContinue = hasValidHit && remainingDamage > 0f && consumedPenetrations <= originalDamagePenetrations;
                    break;
                default:
                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;

                        if (!TryApplyFlatDamageHit(ref projectedEnemyHealth, enemyIndex, math.max(0f, projectileData.Damage), out bool enemyKilled))
                            continue;

                        hasValidHit = true;
                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        RegisterProjectileEnemyHit(canTrackProjectileHits,
                                                   enemyEntities[enemyIndex],
                                                   ref projectileHitHistory);
                        ApplyHitPayloads(enemyIndex,
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         enemyDataArray,
                                         ref projectedEnemyKnockback,
                                         ref enemyElasticHitDirections,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);
                        break;
                    }

                    canProjectileContinue = false;
                    break;
            }

            if (!hasValidHit)
                continue;

            if (projectileContactStateLookup.HasComponent(projectileEntity))
            {
                ProjectileContactState contactState = projectileContactStateLookup[projectileEntity];
                contactState.HasHitTarget = 1;
                projectileContactStateLookup[projectileEntity] = contactState;
            }

            if (canEnqueueAudioRequests)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.BulletImpactEnemy, projectileTransform.Position);

            bool shouldSplitOnHitEvent = ProjectileSplitUtility.ShouldSplitOnHitEvent(in splitState, enemyKilledByProjectile);

            if (canProjectileContinue)
            {
                entityManager.SetComponentData(projectileEntities[projectileIndex], projectileData);

                if (shouldSplitOnHitEvent)
                {
                    // Split creation is centralized in ProjectileSplitUtility to keep hit/despawn behavior consistent.
                    ProjectileSplitUtility.TryEnqueueSplitRequests(in projectileData,
                                                                   in splitState,
                                                                   in projectileTransform,
                                                                   currentScaleMultiplier,
                                                                   in elementalPayload,
                                                                   in projectileOwner,
                                                                   ref shootRequestLookup);
                    splitState.CanSplit = 0;
                    entityManager.SetComponentData(projectileEntities[projectileIndex], splitState);
                }

                continue;
            }

            bool shouldSplitOnDespawnEvent = ProjectileSplitUtility.ShouldSplitOnDespawn(in splitState);

            if (shouldSplitOnHitEvent || shouldSplitOnDespawnEvent)
            {
                // Same utility path for despawn-triggered splits to avoid duplicate split math in this system.
                ProjectileSplitUtility.TryEnqueueSplitRequests(in projectileData,
                                                               in splitState,
                                                               in projectileTransform,
                                                               currentScaleMultiplier,
                                                               in elementalPayload,
                                                               in projectileOwner,
                                                               ref shootRequestLookup);
                splitState.CanSplit = 0;
                entityManager.SetComponentData(projectileEntities[projectileIndex], splitState);
            }

            DespawnProjectile(entityManager, projectileEntities[projectileIndex], projectileOwner, ref projectilePoolLookup);
        }

        ComponentLookup<EnemyDespawnRequest> despawnLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            Entity enemyEntity = enemyEntities[enemyIndex];

            if (!entityManager.Exists(enemyEntity))
                continue;

            bool healthChanged = projectedEnemyHealth[enemyIndex].Current != enemyHealthArray[enemyIndex].Current ||
                                 projectedEnemyHealth[enemyIndex].CurrentShield != enemyHealthArray[enemyIndex].CurrentShield;
            bool knockbackChanged = DidKnockbackStateChange(projectedEnemyKnockback[enemyIndex], enemyKnockbackArray[enemyIndex]);

            if (!healthChanged && !knockbackChanged)
                continue;

            if (knockbackChanged)
                entityManager.SetComponentData(enemyEntity, projectedEnemyKnockback[enemyIndex]);

            if (!healthChanged)
                continue;

            EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];
            EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];
            EnemyExtraComboPointsRuntimeUtility.MarkEnemyDamaged(ref enemyRuntimeState);
            entityManager.SetComponentData(enemyEntity, enemyRuntimeState);
            entityManager.SetComponentData(enemyEntity, enemyHealth);
            DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);
            EnemyElasticHitRuntimeUtility.Trigger(entityManager,
                                                  enemyEntity,
                                                  in enemyHealth,
                                                  enemyElasticHitDirections[enemyIndex],
                                                  true,
                                                  elapsedTime);

            if (enemyHealth.Current <= 0f)
            {
                if (!despawnLookup.HasComponent(enemyEntity))
                {
                    commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
                    {
                        Reason = EnemyDespawnReason.Killed
                    });
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

    }
    #endregion

    #region Helpers
    private static void AddHitCandidateSorted(ref NativeList<ProjectileHitCandidate> hitCandidates,
                                              int enemyIndex,
                                              float distanceSquared)
    {
        hitCandidates.Add(new ProjectileHitCandidate
        {
            EnemyIndex = enemyIndex,
            DistanceSquared = distanceSquared
        });

        for (int candidateIndex = hitCandidates.Length - 1; candidateIndex > 0; candidateIndex--)
        {
            ProjectileHitCandidate currentCandidate = hitCandidates[candidateIndex];
            ProjectileHitCandidate previousCandidate = hitCandidates[candidateIndex - 1];

            if (previousCandidate.DistanceSquared <= currentCandidate.DistanceSquared)
                break;

            hitCandidates[candidateIndex - 1] = currentCandidate;
            hitCandidates[candidateIndex] = previousCandidate;
        }
    }

    /// <summary>
    /// Checks whether a projectile overlap list already contains the enemy index emitted by the spatial hash.
    /// </summary>
    /// <param name="currentOverlapEnemyIndices">Current projectile overlap list.</param>
    /// <param name="enemyIndex">Enemy index to search for.</param>
    /// <returns>True when the enemy index is already present.</returns>
    private static bool HasCurrentOverlapEnemyIndex(NativeList<int> currentOverlapEnemyIndices, int enemyIndex)
    {
        for (int overlapIndex = 0; overlapIndex < currentOverlapEnemyIndices.Length; overlapIndex++)
        {
            if (currentOverlapEnemyIndices[overlapIndex] == enemyIndex)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes contact-hit records for enemies that are no longer overlapped by the projectile this frame.
    /// </summary>
    /// <param name="canTrackProjectileHits">True when the projectile owns a mutable contact-hit buffer.</param>
    /// <param name="currentOverlapEnemyIndices">Current projectile overlap list resolved from spatial collision.</param>
    /// <param name="enemyEntities">Stable enemy entity snapshot used to translate overlap indices.</param>
    /// <param name="hitHistory">Mutable projectile contact-hit buffer.</param>
    private static void PruneProjectileHitHistoryToCurrentOverlaps(bool canTrackProjectileHits,
                                                                   NativeList<int> currentOverlapEnemyIndices,
                                                                   NativeArray<Entity> enemyEntities,
                                                                   ref DynamicBuffer<ProjectileHitHistoryElement> hitHistory)
    {
        if (!canTrackProjectileHits)
            return;

        for (int historyIndex = hitHistory.Length - 1; historyIndex >= 0; historyIndex--)
        {
            Entity trackedEnemyEntity = hitHistory[historyIndex].EnemyEntity;

            if (HasCurrentOverlapEnemyEntity(currentOverlapEnemyIndices, enemyEntities, trackedEnemyEntity))
                continue;

            hitHistory.RemoveAt(historyIndex);
        }
    }

    /// <summary>
    /// Checks whether the current overlap list contains the provided enemy entity.
    /// </summary>
    /// <param name="currentOverlapEnemyIndices">Current projectile overlap list.</param>
    /// <param name="enemyEntities">Stable enemy entity snapshot used to translate overlap indices.</param>
    /// <param name="enemyEntity">Enemy entity to search for.</param>
    /// <returns>True when the enemy is still overlapped this frame.</returns>
    private static bool HasCurrentOverlapEnemyEntity(NativeList<int> currentOverlapEnemyIndices,
                                                     NativeArray<Entity> enemyEntities,
                                                     Entity enemyEntity)
    {
        if (enemyEntity == Entity.Null)
            return false;

        for (int overlapIndex = 0; overlapIndex < currentOverlapEnemyIndices.Length; overlapIndex++)
        {
            int enemyIndex = currentOverlapEnemyIndices[overlapIndex];

            if (enemyIndex < 0 || enemyIndex >= enemyEntities.Length)
                continue;

            if (enemyEntities[enemyIndex] == enemyEntity)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a projectile candidate list already contains the enemy index emitted by the spatial hash.
    /// </summary>
    /// <param name="hitCandidates">Current projectile hit candidate list.</param>
    /// <param name="enemyIndex">Enemy index to search for.</param>
    /// <returns>True when the candidate is already present.</returns>
    private static bool HasHitCandidate(NativeList<ProjectileHitCandidate> hitCandidates, int enemyIndex)
    {
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (hitCandidates[candidateIndex].EnemyIndex == enemyIndex)
                return true;
        }

        return false;
    }

    private static bool TryApplyFlatDamageHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                              int enemyIndex,
                                              float damage,
                                              out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return false;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return false;

        EnemyDamageUtility.ApplyFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return true;
    }

    /// <summary>
    /// Checks whether one projectile has already applied a hit during the current overlap contact.
    /// </summary>
    /// <param name="hitHistory">Projectile contact-hit buffer for the currently processed projectile.</param>
    /// <param name="enemyEntity">Enemy entity candidate being tested.</param>
    /// <returns>True when the enemy already exists in the current projectile contact history.</returns>
    private static bool HasProjectileAlreadyHitEnemy(DynamicBuffer<ProjectileHitHistoryElement> hitHistory,
                                                     Entity enemyEntity)
    {
        for (int historyIndex = 0; historyIndex < hitHistory.Length; historyIndex++)
        {
            if (hitHistory[historyIndex].EnemyEntity == enemyEntity)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds one enemy to a projectile contact-hit history after a successful hit application.
    /// </summary>
    /// <param name="canTrackProjectileHits">True when the projectile owns a mutable history buffer.</param>
    /// <param name="enemyEntity">Enemy entity that was just hit.</param>
    /// <param name="hitHistory">Mutable projectile contact-hit buffer.</param>
    private static void RegisterProjectileEnemyHit(bool canTrackProjectileHits,
                                                   Entity enemyEntity,
                                                   ref DynamicBuffer<ProjectileHitHistoryElement> hitHistory)
    {
        if (!canTrackProjectileHits)
            return;

        if (enemyEntity == Entity.Null)
            return;

        if (HasProjectileAlreadyHitEnemy(hitHistory, enemyEntity))
            return;

        hitHistory.Add(new ProjectileHitHistoryElement
        {
            EnemyEntity = enemyEntity
        });
    }

    private static float ApplyDamageBasedHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                             int enemyIndex,
                                             float damage,
                                             out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return 0f;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return 0f;

        float leftoverDamage = EnemyDamageUtility.ConsumeFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return leftoverDamage;
    }

    /// <summary>
    /// Applies non-health hit payloads for one already accepted projectile impact.
    /// </summary>
    /// <param name="enemyIndex">Enemy index inside the active enemy arrays.</param>
    /// <param name="shooterEntity">Player entity that owns the projectile.</param>
    /// <param name="projectileData">Projectile payload applied to the enemy.</param>
    /// <param name="projectileTransform">Projectile transform at impact time.</param>
    /// <param name="elementalPayload">Elemental payload carried by the projectile.</param>
    /// <param name="enemyEntities">Active enemy entity array.</param>
    /// <param name="enemyPositions">Active enemy position array.</param>
    /// <param name="enemyRuntimeArray">Active enemy runtime state array.</param>
    /// <param name="enemyDataArray">Active enemy immutable data array.</param>
    /// <param name="projectedEnemyKnockback">Mutable projected knockback state array.</param>
    /// <param name="enemyElasticHitDirections">Mutable last direct impact direction per enemy.</param>
    /// <param name="elementalVfxConfigLookup">Lookup used to resolve player elemental VFX configuration.</param>
    /// <param name="elementalVfxAnchorLookup">Lookup used to resolve enemy elemental VFX anchors.</param>
    /// <param name="enemyHitVfxConfigLookup">Lookup used to resolve enemy hit VFX configuration.</param>
    /// <param name="spawnInactivityLockLookup">Lookup used to suppress knockback during spawn locks.</param>
    /// <param name="canEnqueueShooterVfxRequests">True when VFX requests can be queued on the shooter.</param>
    /// <param name="shooterVfxRequests">Buffer receiving VFX spawn requests.</param>
    /// <param name="elementalStackLookup">Lookup used to mutate enemy elemental stacks.</param>
    private static void ApplyHitPayloads(int enemyIndex,
                                         Entity shooterEntity,
                                         in Projectile projectileData,
                                         in LocalTransform projectileTransform,
                                         in ProjectileElementalPayload elementalPayload,
                                         NativeArray<Entity> enemyEntities,
                                         NativeArray<float3> enemyPositions,
                                         NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                         NativeArray<EnemyData> enemyDataArray,
                                         ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                         ref NativeArray<float3> enemyElasticHitDirections,
                                         in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                         in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                         in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                         in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                         bool canEnqueueShooterVfxRequests,
                                         ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                         ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        float3 impactPosition = enemyPositions[enemyIndex];
        enemyElasticHitDirections[enemyIndex] = impactPosition - projectileTransform.Position;
        EnemyHitPayloadRuntimeUtility.ApplyEnemyHitPayloads(enemyIndex,
                                                            shooterEntity,
                                                            impactPosition,
                                                            in projectileData,
                                                            in projectileTransform,
                                                            in elementalPayload,
                                                            enemyEntities,
                                                            enemyPositions,
                                                            enemyRuntimeArray,
                                                            enemyDataArray,
                                                            ref projectedEnemyKnockback,
                                                            in elementalVfxConfigLookup,
                                                            in elementalVfxAnchorLookup,
                                                            in enemyHitVfxConfigLookup,
                                                            in spawnInactivityLockLookup,
                                                            canEnqueueShooterVfxRequests,
                                                            ref shooterVfxRequests,
                                                            ref elementalStackLookup);
    }

    private static float ResolveCurrentScaleMultiplier(Entity projectileEntity,
                                                       float currentScale,
                                                       in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup)
    {
        if (!projectileBaseScaleLookup.HasComponent(projectileEntity))
            return math.max(0.01f, currentScale);

        float baseScale = math.max(0.0001f, projectileBaseScaleLookup[projectileEntity].Value);
        return math.max(0.01f, currentScale / baseScale);
    }

    private static bool DidKnockbackStateChange(EnemyKnockbackState leftValue, EnemyKnockbackState rightValue)
    {
        return leftValue.RemainingTime != rightValue.RemainingTime ||
               leftValue.Velocity.x != rightValue.Velocity.x ||
               leftValue.Velocity.y != rightValue.Velocity.y ||
               leftValue.Velocity.z != rightValue.Velocity.z;
    }

    private static void DespawnProjectile(EntityManager entityManager,
                                          Entity projectileEntity,
                                          ProjectileOwner projectileOwner,
                                          ref BufferLookup<ProjectilePoolElement> projectilePoolLookup)
    {
        if (!entityManager.Exists(projectileEntity))
            return;

        ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
        entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (!projectilePoolLookup.HasBuffer(shooterEntity))
            return;

        DynamicBuffer<ProjectilePoolElement> shooterPool = projectilePoolLookup[shooterEntity];
        shooterPool.Add(new ProjectilePoolElement
        {
            ProjectileEntity = projectileEntity
        });
    }
    #endregion

    #endregion

    #region Jobs
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    private struct EnemyProjectileHitCollectJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> ProjectilePositions;
        [ReadOnly] public NativeArray<float> ProjectileRadii;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float2> EnemyRadiusAxes;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        [ReadOnly] public float InverseCellSize;
        [ReadOnly] public float MaxEnemyRadius;
        public NativeStream.Writer HitStreamWriter;

        public void Execute(int index)
        {
            float3 projectilePosition = ProjectilePositions[index];
            float projectileRadius = math.max(0.005f, ProjectileRadii[index]);
            float searchRadius = math.max(0.01f, projectileRadius + math.max(0.05f, MaxEnemyRadius));
            EnemySpatialHashUtility.ResolveCellBounds(projectilePosition,
                                                      searchRadius,
                                                      InverseCellSize,
                                                      out int minCellX,
                                                      out int maxCellX,
                                                      out int minCellY,
                                                      out int maxCellY);

            NativeStream.Writer streamWriter = HitStreamWriter;
            streamWriter.BeginForEachIndex(index);

            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    int key = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (!CellMap.TryGetFirstValue(key, out enemyIndex, out iterator))
                        continue;

                    do
                    {
                        float3 delta = projectilePosition - EnemyPositions[enemyIndex];
                        delta.y = 0f;

                        if (!IsProjectileOverlappingEnemyEllipse(delta, projectileRadius, EnemyRadiusAxes[enemyIndex]))
                            continue;

                        streamWriter.Write(enemyIndex);
                    }
                    while (CellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            streamWriter.EndForEachIndex();
        }

        /// <summary>
        /// Tests one projectile circle against an enemy ellipse in planar XZ space using inflated half-axes.
        /// </summary>
        /// <param name="delta">Planar vector from enemy center to projectile center.</param>
        /// <param name="projectileRadius">Projectile impact radius added to both ellipse axes.</param>
        /// <param name="enemyRadiusAxes">Enemy body ellipse half-axes in X and Z world units.</param>
        /// <returns>True when the projectile circle overlaps the inflated enemy hit ellipse.</returns>
        private static bool IsProjectileOverlappingEnemyEllipse(float3 delta, float projectileRadius, float2 enemyRadiusAxes)
        {
            float2 inflatedAxes = math.max(new float2(0.01f, 0.01f), enemyRadiusAxes + math.max(0.005f, projectileRadius));
            float normalizedX = delta.x / inflatedAxes.x;
            float normalizedZ = delta.z / inflatedAxes.y;
            return normalizedX * normalizedX + normalizedZ * normalizedZ <= 1f;
        }
    }
    #endregion
}
