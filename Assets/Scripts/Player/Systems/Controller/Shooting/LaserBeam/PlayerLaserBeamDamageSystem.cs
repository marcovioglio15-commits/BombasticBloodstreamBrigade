using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies continuous Laser Beam damage and moving tick-packet damage against enemies intersecting resolved beam lanes.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[UpdateBefore(typeof(EnemyElementalEffectsSystem))]
public partial struct PlayerLaserBeamDamageSystem : ISystem
{
    #region Fields
    private const float MaximumContinuousDamageSliceIntervalSeconds = 0.15f;
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the enemy query used by the Laser Beam hit-resolution path.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, EnemyKnockbackState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
    }

    /// <summary>
    /// Resolves Laser Beam damage work only when at least one beam has a fresh tick budget or active storm packets to process.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasBeamWorkToProcess = false;

        foreach (RefRO<PlayerLaserBeamState> laserBeamState in SystemAPI.Query<RefRO<PlayerLaserBeamState>>())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;

            if (currentLaserBeamState.IsActive == 0 &&
                currentLaserBeamState.IsTickReady == 0 &&
                currentLaserBeamState.StormTickPulses.Length <= 0)
            {
                continue;
            }

            hasBeamWorkToProcess = true;
            break;
        }

        if (!hasBeamWorkToProcess)
            return;

        EntityManager entityManager = state.EntityManager;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
        {
            ConsumeBeamTicksWithoutTargets(ref state);
            return;
        }

        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(frameAllocator);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(frameAllocator);
        NativeArray<EnemyHealth> projectedEnemyHealth = enemyQuery.ToComponentDataArray<EnemyHealth>(frameAllocator);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = enemyQuery.ToComponentDataArray<EnemyRuntimeState>(frameAllocator);
        NativeArray<EnemyKnockbackState> projectedEnemyKnockback = enemyQuery.ToComponentDataArray<EnemyKnockbackState>(frameAllocator);
        NativeArray<byte> enemyDirtyFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.ClearMemory);
        NativeArray<byte> enemyFlashDirtyFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.ClearMemory);
        NativeArray<byte> enemyKnockbackDirtyFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.ClearMemory);
        NativeArray<float3> enemyPositions = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> enemyBodyRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        float maximumEnemyRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
            float bodyRadius = math.max(0.05f, enemyDataArray[enemyIndex].BodyRadius);
            enemyBodyRadii[enemyIndex] = bodyRadius;

            if (bodyRadius > maximumEnemyRadius)
                maximumEnemyRadius = bodyRadius;
        }

        float cellSize = EnemySpatialHashUtility.ResolveCellSize(maximumEnemyRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, frameAllocator);
        EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);

        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);
        ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyHitVfxConfig>(true);
        ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup = SystemAPI.GetComponentLookup<EnemySpawnInactivityLock>(true);
        ComponentLookup<EnemyDespawnRequest> despawnRequestLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates = new NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate>(32, frameAllocator);
        NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> traversedHitCandidates = new NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate>(16, frameAllocator);

        foreach ((RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                  RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeShootingConfig>,
                                    DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot>,
                                    RefRO<PlayerPassiveToolsState>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>>()
                             .WithEntityAccess())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;
            PlayerPassiveToolsState effectivePassiveToolsState = PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState.ValueRO,
                                                                                                                                in currentLaserBeamState);
            bool hasTriggeredActiveLaser = PlayerLaserBeamStateUtility.HasTriggeredActiveLaser(in currentLaserBeamState);

            if (currentLaserBeamState.IsActive == 0)
                continue;

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;
            float tickIntervalSeconds = math.max(0.0001f, laserBeamConfig.DamageTickIntervalSeconds);
            int pendingTickCount = 0;

            if (currentLaserBeamState.IsTickReady != 0)
            {
                pendingTickCount = ResolvePendingTickCount(ref currentLaserBeamState, tickIntervalSeconds);
                currentLaserBeamState.IsTickReady = 0;

                if (pendingTickCount > 0)
                    PlayerLaserBeamStateUtility.EnqueueStormTickPulses(ref currentLaserBeamState, in laserBeamConfig, pendingTickCount);
            }

            if (laserBeamLanes.Length <= 0 || effectivePassiveToolsState.HasLaserBeam == 0)
            {
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(ref currentLaserBeamState, in laserBeamConfig);
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            ElementalEffectConfig unusedElementalEffect = default;
            PlayerProjectileRequestTemplate projectileTemplate = hasTriggeredActiveLaser
                ? currentLaserBeamState.TriggeredActiveProjectileTemplate
                : PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig.ValueRO,
                                                                         appliedElementSlots,
                                                                         in effectivePassiveToolsState,
                                                                         1f,
                                                                         1f,
                                                                         1f,
                                                                         1f,
                                                                         1f,
                                                                         false,
                                                                         in unusedElementalEffect,
                                                                         0f);
            float chargeImpulseDamageMultiplier = !hasTriggeredActiveLaser && currentLaserBeamState.ChargeImpulseRemainingSeconds > 0f
                ? math.max(1f, currentLaserBeamState.ChargeImpulseDamageMultiplier)
                : 1f;
            float baseDamagePerSecond = ResolveBaseDamagePerSecond(projectileTemplate,
                                                                   runtimeShootingConfig.ValueRO,
                                                                   chargeImpulseDamageMultiplier);
            float continuousDamageSliceIntervalSeconds = math.min(tickIntervalSeconds, MaximumContinuousDamageSliceIntervalSeconds);
            int continuousDamageSliceCount = ResolvePendingContinuousDamageSliceCount(ref currentLaserBeamState,
                                                                                      deltaTime,
                                                                                      continuousDamageSliceIntervalSeconds);

            // Quantize the flat continuous channel on a capped internal cadence so average DPS stays stable
            // without forcing a full-lane health pass every rendered frame, even on beam presets with large tick intervals.
            float continuousDamagePerTick = math.max(0f,
                                                     baseDamagePerSecond *
                                                     math.max(0f, laserBeamConfig.ContinuousDamagePerSecondMultiplier) *
                                                     continuousDamageSliceIntervalSeconds *
                                                     continuousDamageSliceCount);
            float tickDamagePerPulse = math.max(0f,
                                                baseDamagePerSecond *
                                                math.max(0f, laserBeamConfig.DamageMultiplier) *
                                                tickIntervalSeconds);
            bool hasActiveStormTickPulses = tickDamagePerPulse > 0f &&
                                            currentLaserBeamState.StormTickPulses.Length > 0;

            if (continuousDamagePerTick <= 0f && !hasActiveStormTickPulses)
            {
                PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(ref currentLaserBeamState, in laserBeamConfig);
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            ProjectilePenetrationMode penetrationMode;
            int maximumPenetrations;

            if (hasTriggeredActiveLaser)
            {
                penetrationMode = currentLaserBeamState.TriggeredActivePenetrationMode;
                maximumPenetrations = math.max(0, currentLaserBeamState.TriggeredActiveMaxPenetrations);
            }
            else
            {
                PlayerProjectileRequestUtility.ResolvePenetrationSettings(in runtimeShootingConfig.ValueRO.Values,
                                                                          ProjectilePenetrationMode.None,
                                                                          0,
                                                                          out penetrationMode,
                                                                          out maximumPenetrations);
            }

            bool canEnqueueVfxRequests = vfxRequestLookup.HasBuffer(playerEntity);
            DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests = default;

            if (canEnqueueVfxRequests)
                shooterVfxRequests = vfxRequestLookup[playerEntity];

            int segmentStartIndex = 0;

            while (segmentStartIndex < laserBeamLanes.Length)
            {
                PlayerLaserBeamLaneElement firstSegment = laserBeamLanes[segmentStartIndex];
                int laneIndex = firstSegment.LaneIndex;
                int segmentEndIndex = segmentStartIndex + 1;
                float laneLength = math.max(0f, firstSegment.Length);

                while (segmentEndIndex < laserBeamLanes.Length &&
                       laserBeamLanes[segmentEndIndex].LaneIndex == laneIndex)
                {
                    laneLength += math.max(0f, laserBeamLanes[segmentEndIndex].Length);
                    segmentEndIndex++;
                }

                PlayerLaserBeamDamageResolutionUtility.CollectHitCandidates(laserBeamLanes,
                                                                            segmentStartIndex,
                                                                            segmentEndIndex,
                                                                            enemyCount,
                                                                            in enemyPositions,
                                                                            in enemyBodyRadii,
                                                                            in enemyCellMap,
                                                                            inverseCellSize,
                                                                            maximumEnemyRadius,
                                                                            ref hitCandidates);

                if (hitCandidates.Length > 0)
                {
                    if (continuousDamagePerTick > 0f)
                    {
                        PlayerLaserBeamDamageResolutionUtility.ApplyContinuousLaneDamageBudget(continuousDamagePerTick,
                                                                                               in firstSegment,
                                                                                               in hitCandidates,
                                                                                               enemyEntities,
                                                                                               ref projectedEnemyHealth,
                                                                                               ref enemyDirtyFlags,
                                                                                               in despawnRequestLookup,
                                                                                               ref commandBuffer);
                    }

                    if (hasActiveStormTickPulses)
                    {
                        ApplyStormTickPulseLaneHits(playerEntity,
                                                    laneLength,
                                                    tickDamagePerPulse,
                                                    deltaTime,
                                                    penetrationMode,
                                                    maximumPenetrations,
                                                    projectileTemplate,
                                                    in currentLaserBeamState,
                                                    in laserBeamConfig,
                                                    in laserBeamLanes,
                                                    segmentStartIndex,
                                                    in hitCandidates,
                                                    ref traversedHitCandidates,
                                                    enemyEntities,
                                                    ref projectedEnemyHealth,
                                                    in enemyPositions,
                                                    in enemyRuntimeArray,
                                                    in enemyDataArray,
                                                    ref projectedEnemyKnockback,
                                                    ref enemyDirtyFlags,
                                                    ref enemyFlashDirtyFlags,
                                                    ref enemyKnockbackDirtyFlags,
                                                    in elementalVfxConfigLookup,
                                                    in elementalVfxAnchorLookup,
                                                    in enemyHitVfxConfigLookup,
                                                    in spawnInactivityLockLookup,
                                                    canEnqueueVfxRequests,
                                                    ref shooterVfxRequests,
                                                    ref elementalStackLookup,
                                                    in despawnRequestLookup,
                                                    ref commandBuffer);
                    }
                }

                segmentStartIndex = segmentEndIndex;
            }

            PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(ref currentLaserBeamState, in laserBeamConfig);
            laserBeamState.ValueRW = currentLaserBeamState;
        }

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            if (enemyDirtyFlags[enemyIndex] == 0 &&
                enemyFlashDirtyFlags[enemyIndex] == 0 &&
                enemyKnockbackDirtyFlags[enemyIndex] == 0)
            {
                continue;
            }

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (!entityManager.Exists(enemyEntity))
                continue;

            if (enemyKnockbackDirtyFlags[enemyIndex] != 0)
                entityManager.SetComponentData(enemyEntity, projectedEnemyKnockback[enemyIndex]);

            if (enemyDirtyFlags[enemyIndex] == 0)
            {
                if (enemyFlashDirtyFlags[enemyIndex] != 0)
                    DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);

                continue;
            }

            if (canEnqueueAudioRequests)
                GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, GameAudioEventId.PlayerLaserImpact, enemyTransforms[enemyIndex].Position);

            EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];
            EnemyExtraComboPointsRuntimeUtility.MarkEnemyDamaged(ref enemyRuntimeState);
            entityManager.SetComponentData(enemyEntity, enemyRuntimeState);
            entityManager.SetComponentData(enemyEntity, projectedEnemyHealth[enemyIndex]);

            if (enemyFlashDirtyFlags[enemyIndex] != 0)
                DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Consumes beam ticks and retires completed traveling packets even when no enemies are currently present.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    private void ConsumeBeamTicksWithoutTargets(ref SystemState state)
    {
        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRW<PlayerLaserBeamState> laserBeamState)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>, RefRW<PlayerLaserBeamState>>())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;
            PlayerPassiveToolsState effectivePassiveToolsState = PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState.ValueRO,
                                                                                                                                in currentLaserBeamState);

            if (currentLaserBeamState.IsActive == 0 &&
                currentLaserBeamState.IsTickReady == 0 &&
                currentLaserBeamState.StormTickPulses.Length <= 0)
            {
                continue;
            }

            if (effectivePassiveToolsState.HasLaserBeam == 0)
            {
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;

            if (currentLaserBeamState.IsActive != 0 && currentLaserBeamState.IsTickReady != 0)
            {
                float tickIntervalSeconds = math.max(0.0001f, laserBeamConfig.DamageTickIntervalSeconds);
                int pendingTickCount = ResolvePendingTickCount(ref currentLaserBeamState, tickIntervalSeconds);

                if (pendingTickCount > 0)
                    PlayerLaserBeamStateUtility.EnqueueStormTickPulses(ref currentLaserBeamState, in laserBeamConfig, pendingTickCount);

                currentLaserBeamState.IsTickReady = 0;
            }

            PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(ref currentLaserBeamState, in laserBeamConfig);
            laserBeamState.ValueRW = currentLaserBeamState;
        }
    }

    /// <summary>
    /// Resolves how many authored beam ticks elapsed since the last damage update and rewinds the timer back into the valid range.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params tickIntervalSeconds Authored tick interval.
    /// /returns Number of ticks that must be consumed this frame.
    /// </summary>
    private static int ResolvePendingTickCount(ref PlayerLaserBeamState laserBeamState,
                                               float tickIntervalSeconds)
    {
        float safeTickIntervalSeconds = math.max(0.0001f, tickIntervalSeconds);

        if (laserBeamState.DamageTickTimer > 0f)
            return 0;

        float overdueSeconds = -laserBeamState.DamageTickTimer;
        int additionalTickCount = (int)math.floor(overdueSeconds / safeTickIntervalSeconds);
        int pendingTickCount = 1 + math.max(0, additionalTickCount);
        laserBeamState.DamageTickTimer += pendingTickCount * safeTickIntervalSeconds;

        if (laserBeamState.DamageTickTimer <= 0f)
            laserBeamState.DamageTickTimer = safeTickIntervalSeconds;

        return pendingTickCount;
    }

    /// <summary>
    /// Resolves the projectile-derived damage-per-second budget shared by continuous beam damage and moving tick packets.
    /// /params projectileTemplate Projectile template built from the current shooting config.
    /// /params runtimeShootingConfig Current runtime shooting config.
    /// /params chargeImpulseDamageMultiplier Active charge-impulse damage multiplier.
    /// /returns Base damage-per-second budget before beam-specific multipliers.
    /// </summary>
    private static float ResolveBaseDamagePerSecond(PlayerProjectileRequestTemplate projectileTemplate,
                                                    in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                    float chargeImpulseDamageMultiplier)
    {
        return math.max(0f,
                        projectileTemplate.Damage *
                        math.max(0f, runtimeShootingConfig.Values.RateOfFire) *
                        math.max(1f, chargeImpulseDamageMultiplier));
    }

    /// <summary>
    /// Accumulates elapsed beam lifetime into a capped continuous-damage cadence and returns how many slices must be applied this frame.
    /// /params laserBeamState Mutable beam state that stores the running continuous-damage accumulator.
    /// /params deltaTime Frame delta time added to the accumulator.
    /// /params sliceIntervalSeconds Maximum interval between two flat continuous-damage applications.
    /// /returns Number of continuous-damage slices that should be emitted this frame.
    /// </summary>
    private static int ResolvePendingContinuousDamageSliceCount(ref PlayerLaserBeamState laserBeamState,
                                                                float deltaTime,
                                                                float sliceIntervalSeconds)
    {
        float safeSliceIntervalSeconds = math.max(0.0001f, sliceIntervalSeconds);
        laserBeamState.ContinuousDamageAccumulatorSeconds += math.max(0f, deltaTime);

        if (laserBeamState.ContinuousDamageAccumulatorSeconds < safeSliceIntervalSeconds)
            return 0;

        int pendingSliceCount = (int)math.floor(laserBeamState.ContinuousDamageAccumulatorSeconds / safeSliceIntervalSeconds);
        laserBeamState.ContinuousDamageAccumulatorSeconds -= pendingSliceCount * safeSliceIntervalSeconds;
        return math.max(0, pendingSliceCount);
    }

    /// <summary>
    /// Applies every active traveling tick packet as a moving storm trail that damages the lane portion already traversed during the current frame.
    /// /params shooterEntity Player entity owning the beam.
    /// /params laneLength Total length of the current lane.
    /// /params tickDamagePerPulse Damage carried by one authored tick packet before lane multipliers.
    /// /params penetrationMode Projectile penetration mode inherited from the current shooting config.
    /// /params maximumPenetrations Maximum penetration budget inherited from the current shooting config.
    /// /params projectileTemplate Projectile template used to resolve hit payloads.
    /// /params laserBeamState Current beam runtime state containing active traveling packets.
    /// /params laserBeamConfig Aggregated Laser Beam passive configuration.
    /// /params laserBeamLanes Resolved lane buffer of the current player.
    /// /params segmentStartIndex First segment index belonging to the current lane.
    /// /params hitCandidates Sorted lane hit candidates.
    /// /params traversedHitCandidates Reusable output list used to store the candidates currently covered by one packet trail.
    /// /params enemyEntities Projected enemy entities.
    /// /params projectedEnemyHealth Mutable projected enemy health buffer.
    /// /params enemyPositions Cached world positions of projected enemies.
    /// /params enemyRuntimeArray Cached runtime states of projected enemies.
    /// /params enemyDataArray Cached immutable data of projected enemies.
    /// /params projectedEnemyKnockback Mutable projected knockback buffer.
    /// /params enemyDirtyFlags Per-enemy dirty flags tracking health updates.
    /// /params enemyKnockbackDirtyFlags Per-enemy dirty flags tracking knockback updates.
    /// /params elementalVfxConfigLookup Lookup of player-owned elemental VFX config.
    /// /params elementalVfxAnchorLookup Lookup of enemy-owned elemental VFX anchors.
    /// /params enemyHitVfxConfigLookup Lookup of enemy hit VFX config.
    /// /params spawnInactivityLockLookup Lookup used by hit VFX payload spawning.
    /// /params canEnqueueVfxRequests True when the shooter can enqueue VFX requests this frame.
    /// /params shooterVfxRequests Mutable shooter VFX buffer.
    /// /params elementalStackLookup Mutable elemental stack lookup on enemies.
    /// /params despawnRequestLookup Lookup used to avoid duplicate despawn requests.
    /// /params commandBuffer ECB used to enqueue despawn requests.
    /// /returns None.
    /// </summary>
    private static void ApplyStormTickPulseLaneHits(Entity shooterEntity,
                                                    float laneLength,
                                                    float tickDamagePerPulse,
                                                    float deltaTime,
                                                    ProjectilePenetrationMode penetrationMode,
                                                    int maximumPenetrations,
                                                    PlayerProjectileRequestTemplate projectileTemplate,
                                                    in PlayerLaserBeamState laserBeamState,
                                                    in LaserBeamPassiveConfig laserBeamConfig,
                                                    in DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                                    int segmentStartIndex,
                                                    in NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> hitCandidates,
                                                    ref NativeList<PlayerLaserBeamDamageResolutionUtility.LaserBeamHitCandidate> traversedHitCandidates,
                                                    NativeArray<Entity> enemyEntities,
                                                    ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                                    in NativeArray<float3> enemyPositions,
                                                    in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                                    in NativeArray<EnemyData> enemyDataArray,
                                                    ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                                    ref NativeArray<byte> enemyDirtyFlags,
                                                    ref NativeArray<byte> enemyFlashDirtyFlags,
                                                    ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                                    in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                                    in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                                    in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                                    in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                                    bool canEnqueueVfxRequests,
                                                    ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                                    ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                                    in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                                    ref EntityCommandBuffer commandBuffer)
    {
        if (tickDamagePerPulse <= 0f || laneLength <= 0f || laserBeamState.StormTickPulses.Length <= 0)
            return;

        float travelSpeed = math.max(0f, laserBeamConfig.StormTickTravelSpeed);

        if (travelSpeed <= 0f)
            return;

        float travelDurationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTravelDurationSeconds(travelSpeed);
        float totalDurationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTotalDurationSeconds(in laserBeamConfig);
        float frameDamagePerPulse = math.max(0f,
                                             tickDamagePerPulse *
                                             math.max(0f, deltaTime) /
                                             math.max(0.0001f, totalDurationSeconds));

        if (frameDamagePerPulse <= 0f)
            return;

        float damageLengthTolerance = math.max(0f, laserBeamConfig.StormTickDamageLengthTolerance);

        for (int pulseIndex = 0; pulseIndex < laserBeamState.StormTickPulses.Length; pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = laserBeamState.StormTickPulses[pulseIndex];
            
            if (pulse.CurrentElapsedSeconds < 0f || pulse.CurrentElapsedSeconds >= totalDurationSeconds)
                continue;

            float headDistance = laneLength * PlayerLaserBeamStateUtility.ResolveNormalizedStormTickProgress(pulse.CurrentElapsedSeconds, travelSpeed);
            float coverageDistance = pulse.CurrentElapsedSeconds >= travelDurationSeconds
                ? laneLength
                : math.min(laneLength, headDistance + damageLengthTolerance);

            if (coverageDistance <= 0f)
                continue;

            PlayerLaserBeamDamageResolutionUtility.CollectTraversedHitCandidates(in hitCandidates,
                                                                                 0f,
                                                                                 coverageDistance,
                                                                                 ref traversedHitCandidates);

            if (traversedHitCandidates.Length <= 0)
                continue;

            PlayerLaserBeamDamageResolutionUtility.ResolveLaneHits(shooterEntity,
                                                                  frameDamagePerPulse,
                                                                  penetrationMode,
                                                                  maximumPenetrations,
                                                                  projectileTemplate,
                                                                  in laserBeamLanes,
                                                                  segmentStartIndex,
                                                                  in traversedHitCandidates,
                                                                  enemyEntities,
                                                                  ref projectedEnemyHealth,
                                                                  in enemyPositions,
                                                                  in enemyRuntimeArray,
                                                                  in enemyDataArray,
                                                                  ref projectedEnemyKnockback,
                                                                  ref enemyDirtyFlags,
                                                                  ref enemyFlashDirtyFlags,
                                                                  ref enemyKnockbackDirtyFlags,
                                                                  in elementalVfxConfigLookup,
                                                                  in elementalVfxAnchorLookup,
                                                                  in enemyHitVfxConfigLookup,
                                                                  in spawnInactivityLockLookup,
                                                                  canEnqueueVfxRequests,
                                                                  ref shooterVfxRequests,
                                                                  ref elementalStackLookup,
                                                                  in despawnRequestLookup,
                                                                  ref commandBuffer);
        }
    }
    #endregion

    #endregion
}
