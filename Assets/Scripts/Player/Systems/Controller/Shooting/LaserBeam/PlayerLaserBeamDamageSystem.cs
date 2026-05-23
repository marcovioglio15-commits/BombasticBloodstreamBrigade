using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies continuous Laser Beam damage and moving tick-packet damage against enemies intersecting resolved beam lanes.
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
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, EnemyKnockbackState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerLaserBeamStormTickPulse>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
        state.RequireForUpdate<PlayerLaserBeamPulseHitElement>();
    }

    /// <summary>
    /// Resolves Laser Beam damage work only when at least one beam has a fresh tick budget or active storm packets to process.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasBeamWorkToProcess = false;

        foreach ((RefRO<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses)
                 in SystemAPI.Query<RefRO<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamStormTickPulse>>())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;

            if (currentLaserBeamState.IsActive == 0 &&
                currentLaserBeamState.IsTickReady == 0 &&
                stormTickPulses.Length <= 0)
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
        NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet =
            new NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey>(math.max(64, enemyCount * 64), frameAllocator);

        foreach ((RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                  DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                  DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeShootingConfig>,
                                    DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot>,
                                    DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamStormTickPulse>,
                                    DynamicBuffer<PlayerLaserBeamPulseHitElement>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>>()
                             .WithEntityAccess())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;
            PlayerPassiveToolsState passiveToolsState = PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer);
            PlayerPassiveToolsState effectivePassiveToolsState = PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState,
                                                                                                                                in currentLaserBeamState);
            bool hasTriggeredActiveLaser = PlayerLaserBeamStateUtility.HasTriggeredActiveLaser(in currentLaserBeamState);

            if (currentLaserBeamState.IsActive == 0)
            {
                PlayerLaserBeamPulseHitUtility.ClearPulseHits(pulseHits);
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;
            float tickIntervalSeconds = math.max(0.0001f, laserBeamConfig.DamageTickIntervalSeconds);
            int pendingTickCount = 0;

            if (currentLaserBeamState.IsTickReady != 0)
            {
                pendingTickCount = ResolvePendingTickCount(ref currentLaserBeamState, tickIntervalSeconds);
                currentLaserBeamState.IsTickReady = 0;

                if (pendingTickCount > 0)
                    PlayerLaserBeamStateUtility.EnqueueStormTickPulses(ref currentLaserBeamState,
                                                                       stormTickPulses,
                                                                       in laserBeamConfig,
                                                                       pendingTickCount);
            }

            if (effectivePassiveToolsState.HasLaserBeam == 0)
            {
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamPulseHitUtility.ClearPulseHits(pulseHits);
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            if (laserBeamLanes.Length <= 0)
            {
                currentLaserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
                PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(stormTickPulses, in laserBeamConfig);
                PlayerLaserBeamPulseHitUtility.RetainActivePulseHits(pulseHits, in stormTickPulses);
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
                                            stormTickPulses.Length > 0;

            if (continuousDamagePerTick <= 0f && !hasActiveStormTickPulses)
            {
                PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(stormTickPulses, in laserBeamConfig);
                PlayerLaserBeamPulseHitUtility.RetainActivePulseHits(pulseHits, in stormTickPulses);
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

            PlayerLaserBeamPulseHitUtility.PopulatePulseHitSet(in pulseHits, ref pulseHitSet);
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
                                                    penetrationMode,
                                                    maximumPenetrations,
                                                    projectileTemplate,
                                                    in stormTickPulses,
                                                    in laserBeamConfig,
                                                    pulseHits,
                                                    ref pulseHitSet,
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

            PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(stormTickPulses, in laserBeamConfig);
            PlayerLaserBeamPulseHitUtility.RetainActivePulseHits(pulseHits, in stormTickPulses);
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
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    private void ConsumeBeamTicksWithoutTargets(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                  DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits)
                 in SystemAPI.Query<DynamicBuffer<PlayerPassiveToolsStateElement>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamStormTickPulse>,
                                    DynamicBuffer<PlayerLaserBeamPulseHitElement>>())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;
            PlayerPassiveToolsState passiveToolsState = PlayerPassiveToolsStateBufferUtility.Read(passiveToolsStateBuffer);
            PlayerPassiveToolsState effectivePassiveToolsState = PlayerLaserBeamStateUtility.ResolveEffectivePassiveToolsState(in passiveToolsState,
                                                                                                                                in currentLaserBeamState);

            if (currentLaserBeamState.IsActive == 0 &&
                currentLaserBeamState.IsTickReady == 0 &&
                stormTickPulses.Length <= 0)
            {
                continue;
            }

            if (effectivePassiveToolsState.HasLaserBeam == 0)
            {
                laserBeamState.ValueRW = currentLaserBeamState;
                PlayerLaserBeamPulseHitUtility.ClearPulseHits(pulseHits);
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = effectivePassiveToolsState.LaserBeam;

            if (currentLaserBeamState.IsActive != 0 && currentLaserBeamState.IsTickReady != 0)
            {
                float tickIntervalSeconds = math.max(0.0001f, laserBeamConfig.DamageTickIntervalSeconds);
                int pendingTickCount = ResolvePendingTickCount(ref currentLaserBeamState, tickIntervalSeconds);

                if (pendingTickCount > 0)
                    PlayerLaserBeamStateUtility.EnqueueStormTickPulses(ref currentLaserBeamState,
                                                                       stormTickPulses,
                                                                       in laserBeamConfig,
                                                                       pendingTickCount);

                currentLaserBeamState.IsTickReady = 0;
            }

            PlayerLaserBeamStateUtility.RemoveCompletedStormTickPulses(stormTickPulses, in laserBeamConfig);
            PlayerLaserBeamPulseHitUtility.RetainActivePulseHits(pulseHits, in stormTickPulses);
            laserBeamState.ValueRW = currentLaserBeamState;
        }
    }

    /// <summary>
    /// Resolves how many authored beam ticks elapsed since the last damage update and rewinds the timer back into the valid range.
    /// </summary>
    /// <param name="laserBeamState">Mutable Laser Beam runtime state.</param>
    /// <param name="tickIntervalSeconds">Authored tick interval.</param>
    /// <returns>Number of ticks that must be consumed this frame.</returns>
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
    /// </summary>
    /// <param name="projectileTemplate">Projectile template built from the current shooting config.</param>
    /// <param name="runtimeShootingConfig">Current runtime shooting config.</param>
    /// <param name="chargeImpulseDamageMultiplier">Active charge-impulse damage multiplier.</param>
    /// <returns>Base damage-per-second budget before beam-specific multipliers.</returns>
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
    /// </summary>
    /// <param name="laserBeamState">Mutable beam state that stores the running continuous-damage accumulator.</param>
    /// <param name="deltaTime">Frame delta time added to the accumulator.</param>
    /// <param name="sliceIntervalSeconds">Maximum interval between two flat continuous-damage applications.</param>
    /// <returns>Number of continuous-damage slices that should be emitted this frame.</returns>
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
    /// Applies every active traveling tick packet as a pulse span that can damage each enemy once for the pulse lifetime.
    /// </summary>
    /// <param name="shooterEntity">Player entity owning the beam.</param>
    /// <param name="laneLength">Total length of the current lane.</param>
    /// <param name="tickDamagePerPulse">Full damage carried by one authored tick packet before lane multipliers.</param>
    /// <param name="penetrationMode">Projectile penetration mode inherited from the current shooting config.</param>
    /// <param name="maximumPenetrations">Maximum penetration budget inherited from the current shooting config.</param>
    /// <param name="projectileTemplate">Projectile template used to resolve hit payloads.</param>
    /// <param name="stormTickPulses">Active traveling tick packets owned by the beam.</param>
    /// <param name="laserBeamConfig">Aggregated Laser Beam passive configuration.</param>
    /// <param name="pulseHits">Mutable pulse-hit history used to prevent duplicate enemy hits by the same pulse.</param>
    /// <param name="pulseHitSet">Mutable frame-local pulse-hit lookup synchronized with the persistent hit buffer.</param>
    /// <param name="laserBeamLanes">Resolved lane buffer of the current player.</param>
    /// <param name="segmentStartIndex">First segment index belonging to the current lane.</param>
    /// <param name="hitCandidates">Sorted lane hit candidates.</param>
    /// <param name="traversedHitCandidates">Reusable output list used to store the candidates currently covered by one pulse span.</param>
    /// <param name="enemyEntities">Projected enemy entities.</param>
    /// <param name="projectedEnemyHealth">Mutable projected enemy health buffer.</param>
    /// <param name="enemyPositions">Cached world positions of projected enemies.</param>
    /// <param name="enemyRuntimeArray">Cached runtime states of projected enemies.</param>
    /// <param name="enemyDataArray">Cached immutable data of projected enemies.</param>
    /// <param name="projectedEnemyKnockback">Mutable projected knockback buffer.</param>
    /// <param name="enemyDirtyFlags">Per-enemy dirty flags tracking health updates.</param>
    /// <param name="enemyKnockbackDirtyFlags">Per-enemy dirty flags tracking knockback updates.</param>
    /// <param name="elementalVfxConfigLookup">Lookup of player-owned elemental VFX config.</param>
    /// <param name="elementalVfxAnchorLookup">Lookup of enemy-owned elemental VFX anchors.</param>
    /// <param name="enemyHitVfxConfigLookup">Lookup of enemy hit VFX config.</param>
    /// <param name="spawnInactivityLockLookup">Lookup used by hit VFX payload spawning.</param>
    /// <param name="canEnqueueVfxRequests">True when the shooter can enqueue VFX requests this frame.</param>
    /// <param name="shooterVfxRequests">Mutable shooter VFX buffer.</param>
    /// <param name="elementalStackLookup">Mutable elemental stack lookup on enemies.</param>
    /// <param name="despawnRequestLookup">Lookup used to avoid duplicate despawn requests.</param>
    /// <param name="commandBuffer">ECB used to enqueue despawn requests.</param>
    private static void ApplyStormTickPulseLaneHits(Entity shooterEntity,
                                                    float laneLength,
                                                    float tickDamagePerPulse,
                                                    ProjectilePenetrationMode penetrationMode,
                                                    int maximumPenetrations,
                                                    PlayerProjectileRequestTemplate projectileTemplate,
                                                    in DynamicBuffer<PlayerLaserBeamStormTickPulse> stormTickPulses,
                                                    in LaserBeamPassiveConfig laserBeamConfig,
                                                    DynamicBuffer<PlayerLaserBeamPulseHitElement> pulseHits,
                                                    ref NativeParallelHashSet<PlayerLaserBeamPulseHitUtility.PulseHitKey> pulseHitSet,
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
        if (tickDamagePerPulse <= 0f || laneLength <= 0f || stormTickPulses.Length <= 0)
            return;

        float travelSpeed = math.max(0f, laserBeamConfig.StormTickTravelSpeed);

        if (travelSpeed <= 0f)
            return;

        float travelDurationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTravelDurationSeconds(travelSpeed);
        float totalDurationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTotalDurationSeconds(in laserBeamConfig);
        float damagePerPulse = math.max(0f, tickDamagePerPulse);

        if (damagePerPulse <= 0f)
            return;

        float damageLengthTolerance = math.max(0f, laserBeamConfig.StormTickDamageLengthTolerance);

        for (int pulseIndex = 0; pulseIndex < stormTickPulses.Length; pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = stormTickPulses[pulseIndex];
            
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
                                                                  damagePerPulse,
                                                                  pulse.PulseId,
                                                                  pulseHits,
                                                                  ref pulseHitSet,
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
