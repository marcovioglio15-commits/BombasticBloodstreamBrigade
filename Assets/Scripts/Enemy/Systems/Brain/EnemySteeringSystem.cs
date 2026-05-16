using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Runs enemy pursuit and separation steering using Burst jobs and LOD-driven update frequency.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
public partial struct EnemySteeringSystem : ISystem
{
    #region Fields
    private EntityQuery activeEnemiesQuery;
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        activeEnemiesQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData,
                     EnemyRuntimeState,
                     EnemyKnockbackState,
                     LocalTransform,
                     EnemyActive,
                     EnemyElementalRuntimeState,
                     EnemyShooterControlState>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(activeEnemiesQuery);
        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        int enemyCount = activeEnemiesQuery.CalculateEntityCount();

        if (enemyCount <= 0)
            return;

        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerConfig>(out Entity playerEntity))
            return;

        float3 playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        if (enemyTimeScale <= 0f)
            return;

        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeArray<Entity> enemyEntities = activeEnemiesQuery.ToEntityArray(frameAllocator);
        NativeArray<LocalTransform> enemyTransforms = activeEnemiesQuery.ToComponentDataArray<LocalTransform>(frameAllocator);
        NativeArray<EnemyData> enemyDataArray = activeEnemiesQuery.ToComponentDataArray<EnemyData>(frameAllocator);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = activeEnemiesQuery.ToComponentDataArray<EnemyRuntimeState>(frameAllocator);
        NativeArray<EnemyKnockbackState> enemyKnockbackArray = activeEnemiesQuery.ToComponentDataArray<EnemyKnockbackState>(frameAllocator);
        NativeArray<EnemyElementalRuntimeState> enemyElementalRuntimeArray = activeEnemiesQuery.ToComponentDataArray<EnemyElementalRuntimeState>(frameAllocator);
        NativeArray<EnemyShooterControlState> enemyShooterControlArray = activeEnemiesQuery.ToComponentDataArray<EnemyShooterControlState>(frameAllocator);

        NativeArray<float3> positions = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> speedData = CollectionHelper.CreateNativeArray<float2>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> contactRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> bodyRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> priorityTiers = CollectionHelper.CreateNativeArray<int>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> steeringAggressiveness = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float3> planarVelocities = CollectionHelper.CreateNativeArray<float3>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> wandererMovementFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> customPatternMovementFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> spawnInactivityLockFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> shooterMovementLockedFlags = CollectionHelper.CreateNativeArray<byte>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> separationRadii = CollectionHelper.CreateNativeArray<float>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> ownedBossIndices = CollectionHelper.CreateNativeArray<int>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> cellCoordinates = default;
        NativeArray<int> enemyToEvaluatedIndex = CollectionHelper.CreateNativeArray<int>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
        NativeParallelHashMap<Entity, int> enemyIndexByEntity = new NativeParallelHashMap<Entity, int>(enemyCount, frameAllocator);

        NativeList<int> evaluatedEnemyIndices = new NativeList<int>(enemyCount, frameAllocator);
        ComponentLookup<EnemyCustomPatternMovementTag> customPatternMovementTagLookup = SystemAPI.GetComponentLookup<EnemyCustomPatternMovementTag>(true);
        ComponentLookup<EnemyPatternConfig> patternConfigLookup = SystemAPI.GetComponentLookup<EnemyPatternConfig>(true);
        ComponentLookup<EnemyPatternRuntimeState> patternRuntimeStateLookup = SystemAPI.GetComponentLookup<EnemyPatternRuntimeState>(true);
        ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup = SystemAPI.GetComponentLookup<EnemySpawnInactivityLock>(true);
        ComponentLookup<EnemyBossMinionOwner> bossMinionOwnerLookup = SystemAPI.GetComponentLookup<EnemyBossMinionOwner>(true);

        float maxSeparationRadius = 0.25f;
        float maxBodyRadius = 0.05f;
        float maxSteeringAggressiveness = EnemySteeringUtility.DefaultSteeringAggressiveness;
        int frameCount = Time.frameCount;

        for (int index = 0; index < enemyCount; index++)
            enemyIndexByEntity.TryAdd(enemyEntities[index], index);

        for (int index = 0; index < enemyCount; index++)
        {
            EnemyData enemyData = enemyDataArray[index];
            LocalTransform enemyTransform = enemyTransforms[index];

            positions[index] = enemyTransform.Position;
            Entity enemyEntity = enemyEntities[index];
            float elementalSlowPercent = math.clamp(enemyElementalRuntimeArray[index].SlowPercent, 0f, 100f);
            EnemyKnockbackState currentKnockbackState = enemyKnockbackArray[index];

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            speedData[index] = new float2(math.max(0f, enemyData.MoveSpeed) * slowMultiplier, math.max(0f, enemyData.MaxSpeed) * slowMultiplier);
            contactRadii[index] = math.max(0f, enemyData.ContactRadius);
            bodyRadii[index] = math.max(0.05f, enemyData.BodyRadius);
            priorityTiers[index] = math.clamp(enemyData.PriorityTier, -128, 128);
            float enemySteeringAggressiveness = EnemySteeringUtility.ResolveSteeringAggressiveness(enemyData.SteeringAggressiveness);
            steeringAggressiveness[index] = enemySteeringAggressiveness;
            float3 planarVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(enemyRuntimeArray[index].Velocity,
                                                                                        in currentKnockbackState);
            planarVelocity.y = 0f;
            planarVelocities[index] = planarVelocity;
            ownedBossIndices[index] = ResolveOwnedBossIndex(enemyEntity, in bossMinionOwnerLookup, enemyIndexByEntity);
            wandererMovementFlags[index] = 0;
            spawnInactivityLockFlags[index] = spawnInactivityLockLookup.HasComponent(enemyEntity) && spawnInactivityLockLookup.IsComponentEnabled(enemyEntity)
                ? (byte)1
                : (byte)0;
            bool hasCustomPatternMovementTag = customPatternMovementTagLookup.HasComponent(enemyEntity);
            bool usesCustomPatternMovement = hasCustomPatternMovementTag;

            if (patternConfigLookup.HasComponent(enemyEntity))
            {
                EnemyPatternConfig patternConfig = patternConfigLookup[enemyEntity];
                EnemyPatternRuntimeState patternRuntimeState = patternRuntimeStateLookup.HasComponent(enemyEntity)
                    ? patternRuntimeStateLookup[enemyEntity]
                    : default;
                float3 toPlayer = playerPosition - enemyTransform.Position;
                toPlayer.y = 0f;
                float playerDistance = math.length(toPlayer);
                EnemyCompiledMovementPatternKind activeMovementKind = EnemyPatternMovementRuntimeUtility.ResolveActiveMovementKind(in patternConfig,
                                                                                                                                    in patternRuntimeState,
                                                                                                                                    playerDistance);

                if (hasCustomPatternMovementTag && activeMovementKind == EnemyCompiledMovementPatternKind.Grunt)
                    usesCustomPatternMovement = false;

                if (activeMovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                    activeMovementKind == EnemyCompiledMovementPatternKind.WandererDvd ||
                    activeMovementKind == EnemyCompiledMovementPatternKind.Coward)
                {
                    wandererMovementFlags[index] = 1;
                }
            }

            customPatternMovementFlags[index] = usesCustomPatternMovement ? (byte)1 : (byte)0;
            shooterMovementLockedFlags[index] = 0;

            if (enemyShooterControlArray[index].MovementLocked != 0)
                shooterMovementLockedFlags[index] = 1;

            if (bodyRadii[index] > maxBodyRadius)
                maxBodyRadius = bodyRadii[index];

            float separationRadius = math.max(0.05f, enemyData.SeparationRadius);
            separationRadii[index] = separationRadius;

            if (separationRadius > maxSeparationRadius)
                maxSeparationRadius = separationRadius;

            if (enemySteeringAggressiveness > maxSteeringAggressiveness)
                maxSteeringAggressiveness = enemySteeringAggressiveness;

            enemyToEvaluatedIndex[index] = -1;

            // LOD gating reduces per-frame steering cost for far enemies while keeping a stable cadence.
            EnemySteeringUtility.SteeringLodLevel lodLevel = EnemySteeringUtility.EvaluateLod(playerPosition, enemyTransform.Position);
            bool shouldEvaluate = EnemySteeringUtility.ShouldEvaluateLod(lodLevel, frameCount, enemyEntity.Index);

            if (!shouldEvaluate && ownedBossIndices[index] >= 0)
            {
                int ownedBossIndex = ownedBossIndices[index];
                shouldEvaluate = EnemyBossMinionSteeringUtility.ShouldForceBossAvoidanceEvaluation(enemyTransform.Position,
                                                                                                   enemyTransforms[ownedBossIndex].Position,
                                                                                                   enemyData.BodyRadius,
                                                                                                   enemyData.SeparationRadius,
                                                                                                   enemySteeringAggressiveness);
            }

            if (shouldEvaluate && customPatternMovementFlags[index] == 0 && spawnInactivityLockFlags[index] == 0)
            {
                int evaluatedIndex = evaluatedEnemyIndices.Length;
                enemyToEvaluatedIndex[index] = evaluatedIndex;
                evaluatedEnemyIndices.Add(index);
            }
        }

        int evaluatedCount = evaluatedEnemyIndices.Length;
        NativeParallelMultiHashMap<int, int> cellMap = default;
        NativeArray<float3> approachResults = default;
        NativeArray<float3> separationResults = default;
        NativeArray<float> separationUrgencyResults = default;
        NativeArray<float> priorityYieldUrgencyResults = default;
        NativeArray<float> priorityYieldGapResults = default;

        if (evaluatedCount > 0)
        {
            float clearanceAggressivenessScale = EnemySteeringUtility.ResolveAggressivenessScale(maxSteeringAggressiveness, 0.82f, 1.35f);
            float maxHardClearanceRadius = (maxBodyRadius * 2f + EnemySteeringUtility.SeparationClearancePadding) * 2.75f * clearanceAggressivenessScale;
            float maxScaledSeparationRadius = maxSeparationRadius * EnemySteeringUtility.ResolveAggressivenessScale(maxSteeringAggressiveness, 0.9f, 1.45f);
            float cellSize = math.max(0.25f, math.max(maxScaledSeparationRadius, maxHardClearanceRadius));
            float inverseCellSize = 1f / cellSize;
            cellCoordinates = CollectionHelper.CreateNativeArray<int2>(enemyCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            cellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, frameAllocator);

            for (int index = 0; index < enemyCount; index++)
            {
                float3 position = positions[index];
                int cellX = (int)math.floor(position.x * inverseCellSize);
                int cellY = (int)math.floor(position.z * inverseCellSize);
                int2 cell = new int2(cellX, cellY);
                cellCoordinates[index] = cell;
                cellMap.Add(EnemySpatialHashUtility.EncodeCell(cellX, cellY), index);
            }

            approachResults = CollectionHelper.CreateNativeArray<float3>(evaluatedCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            separationResults = CollectionHelper.CreateNativeArray<float3>(evaluatedCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            separationUrgencyResults = CollectionHelper.CreateNativeArray<float>(evaluatedCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            priorityYieldUrgencyResults = CollectionHelper.CreateNativeArray<float>(evaluatedCount, frameAllocator, NativeArrayOptions.UninitializedMemory);
            priorityYieldGapResults = CollectionHelper.CreateNativeArray<float>(evaluatedCount, frameAllocator, NativeArrayOptions.UninitializedMemory);

            // Approach and separation are independent, so they run in parallel and sync once before integration.
            EnemySteeringUtility.EnemyApproachJob approachJob = new EnemySteeringUtility.EnemyApproachJob
            {
                EvaluatedEnemyIndices = evaluatedEnemyIndices.AsArray(),
                Positions = positions,
                SpeedData = speedData,
                ContactRadii = contactRadii,
                PlayerPosition = playerPosition,
                Results = approachResults
            };

            EnemySteeringUtility.EnemySeparationJob separationJob = new EnemySteeringUtility.EnemySeparationJob
            {
                EvaluatedEnemyIndices = evaluatedEnemyIndices.AsArray(),
                Positions = positions,
                BodyRadii = bodyRadii,
                PriorityTiers = priorityTiers,
                SteeringAggressiveness = steeringAggressiveness,
                Velocities = planarVelocities,
                WandererMovementFlags = wandererMovementFlags,
                OwnedBossIndices = ownedBossIndices,
                SeparationRadii = separationRadii,
                CellCoordinates = cellCoordinates,
                CellMap = cellMap,
                Results = separationResults,
                UrgencyResults = separationUrgencyResults,
                YieldUrgencyResults = priorityYieldUrgencyResults,
                YieldPriorityGapResults = priorityYieldGapResults
            };

            JobHandle approachHandle = approachJob.Schedule(evaluatedCount, 64, state.Dependency);
            JobHandle separationHandle = separationJob.Schedule(evaluatedCount, 64, state.Dependency);
            JobHandle combinedHandle = JobHandle.CombineDependencies(approachHandle, separationHandle);
            combinedHandle.Complete();
        }

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();
        EnemyNavigationGridState navigationGridState = default;
        DynamicBuffer<EnemyNavigationCellElement> navigationCells = default;
        bool navigationReady = false;

        if (SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out navigationGridState) &&
            SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out navigationCells))
        {
            navigationReady = true;
        }

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            Entity enemyEntity = enemyEntities[enemyIndex];

            if (customPatternMovementFlags[enemyIndex] != 0)
                continue;

            if (spawnInactivityLockFlags[enemyIndex] != 0)
            {
                EnemyRuntimeState lockedRuntimeState = enemyRuntimeArray[enemyIndex];
                lockedRuntimeState.Velocity = float3.zero;
                enemyRuntimeArray[enemyIndex] = lockedRuntimeState;
                continue;
            }

            EnemyData enemyData = enemyDataArray[enemyIndex];
            EnemyRuntimeState runtimeState = enemyRuntimeArray[enemyIndex];
            EnemyKnockbackState knockbackState = enemyKnockbackArray[enemyIndex];
            LocalTransform enemyTransform = enemyTransforms[enemyIndex];
            float velocityMaxSpeed = math.max(0f, speedData[enemyIndex].y);
            float enemySteeringAggressiveness = steeringAggressiveness[enemyIndex];

            int evaluatedIndex = enemyToEvaluatedIndex[enemyIndex];

            if (evaluatedIndex >= 0)
            {
                float separationWeight = math.max(0f, enemyData.SeparationWeight);
                float3 desiredVelocity = approachResults[evaluatedIndex];

                if (navigationReady && wallsEnabled)
                {
                    float navigationDesiredSpeed = velocityMaxSpeed > 0f ? velocityMaxSpeed : math.max(0f, enemyData.MoveSpeed);
                    float navigationCollisionRadius = math.max(0.01f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));

                    if (EnemyNavigationFlowFieldUtility.TryResolveNavigationVelocity(enemyTransform.Position,
                                                                                    playerPosition,
                                                                                    navigationCollisionRadius,
                                                                                    navigationDesiredSpeed,
                                                                                    in physicsWorldSingleton,
                                                                                    wallsLayerMask,
                                                                                    in navigationGridState,
                                                                                    navigationCells,
                                                                                    out float3 navigationVelocity))
                    {
                        desiredVelocity = navigationVelocity;
                    }
                }

                float urgency = math.saturate(separationUrgencyResults[evaluatedIndex]);
                float urgencyBoost = math.lerp(1f, EnemySteeringUtility.SeparationUrgencyMaxBoost, urgency);
                float separationResponseScale = EnemySteeringUtility.ResolveAggressivenessScale(enemySteeringAggressiveness, 0.72f, 1.95f);
                desiredVelocity += separationResults[evaluatedIndex] * separationWeight * urgencyBoost * separationResponseScale * enemySteeringAggressiveness;
                float priorityYieldUrgency = math.saturate(priorityYieldUrgencyResults[evaluatedIndex]);
                float priorityYieldGap = math.saturate(priorityYieldGapResults[evaluatedIndex]);

                if (velocityMaxSpeed > 0f && priorityYieldUrgency > 0f)
                {
                    float speedBoost = EnemySteeringUtility.ResolvePriorityYieldSpeedBoost(priorityYieldUrgency, priorityYieldGap, enemySteeringAggressiveness);
                    velocityMaxSpeed *= 1f + speedBoost;
                }

                float desiredSpeed = math.length(desiredVelocity);

                if (velocityMaxSpeed > 0f && desiredSpeed > velocityMaxSpeed && desiredSpeed > EnemySteeringUtility.DirectionEpsilon)
                    desiredVelocity *= velocityMaxSpeed / desiredSpeed;

                float acceleration = math.max(0f, enemyData.Acceleration);
                float deceleration = math.max(0f, enemyData.Deceleration);
                float accelerationRate = EnemySteeringUtility.ResolveVelocityChangeRate(runtimeState.Velocity,
                                                                                       desiredVelocity,
                                                                                       acceleration,
                                                                                       deceleration);

                if (priorityYieldUrgency > 0f)
                {
                    float accelerationBoost = EnemySteeringUtility.ResolvePriorityYieldAccelerationBoost(priorityYieldUrgency, priorityYieldGap, enemySteeringAggressiveness);
                    accelerationRate *= 1f + accelerationBoost;
                }

                float maxVelocityDelta = accelerationRate * deltaTime;
                float3 velocityDelta = desiredVelocity - runtimeState.Velocity;
                float velocityDeltaMagnitude = math.length(velocityDelta);

                if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > EnemySteeringUtility.DirectionEpsilon)
                    runtimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
                else
                    runtimeState.Velocity = desiredVelocity;
            }

            float velocityMagnitude = math.length(runtimeState.Velocity);

            if (velocityMaxSpeed > 0f && velocityMagnitude > velocityMaxSpeed && velocityMagnitude > EnemySteeringUtility.DirectionEpsilon)
                runtimeState.Velocity *= velocityMaxSpeed / velocityMagnitude;

            if (shooterMovementLockedFlags[enemyIndex] != 0)
                runtimeState.Velocity = float3.zero;

            float3 desiredDisplacement = runtimeState.Velocity * deltaTime;
            float desiredDisplacementSquared = math.lengthsq(desiredDisplacement);
            float3 position = enemyTransform.Position;
            float3 resolvedDisplacement = desiredDisplacement;
            float3 resolvedVelocity = runtimeState.Velocity;

            if (wallsEnabled && desiredDisplacementSquared > EnemySteeringUtility.DirectionEpsilon)
            {
                float collisionRadius = math.max(0.01f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));
                bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       position,
                                                                                       desiredDisplacement,
                                                                                       collisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 hitNormal);

                resolvedDisplacement = allowedDisplacement;

                if (hitWall)
                {
                    resolvedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(runtimeState.Velocity, hitNormal);

                    // If direct motion is blocked, try a short wall-circumnavigation displacement before stopping.
                    if (evaluatedIndex >= 0 &&
                        EnemyWallSteeringUtility.TryResolveCircumnavigationDisplacement(physicsWorldSingleton,
                                                                                        enemyEntity.Index,
                                                                                        enemyTransform.Position,
                                                                                        playerPosition,
                                                                                        runtimeState.Velocity,
                                                                                        desiredDisplacement,
                                                                                        allowedDisplacement,
                                                                                        hitNormal,
                                                                                        collisionRadius,
                                                                                        wallsLayerMask,
                                                                                        deltaTime,
                                                                                        out float3 bypassDisplacement,
                                                                                        out float3 bypassVelocity,
                                                                                        out float3 bypassHitNormal))
                    {
                        resolvedDisplacement = bypassDisplacement;
                        resolvedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(bypassVelocity, bypassHitNormal);
                    }
                }
            }

            position += resolvedDisplacement;
            runtimeState.Velocity = resolvedVelocity;
            float knockbackCollisionRadius = math.max(0.01f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));
            EnemyKnockbackRuntimeUtility.ApplyDisplacement(ref knockbackState,
                                                           ref position,
                                                           knockbackCollisionRadius,
                                                           in physicsWorldSingleton,
                                                           wallsLayerMask,
                                                           wallsEnabled,
                                                           deltaTime);
            position.y = enemyTransform.Position.y;
            enemyTransform.Position = position;

            float rotationSpeedDegreesPerSecond = enemyData.RotationSpeedDegreesPerSecond;
            bool hasSelfRotation = math.abs(rotationSpeedDegreesPerSecond) > EnemySteeringUtility.RotationSpeedEpsilon;

            if (hasSelfRotation)
            {
                float deltaYawRadians = math.radians(rotationSpeedDegreesPerSecond) * deltaTime;
                quaternion deltaRotation = quaternion.RotateY(deltaYawRadians);
                enemyTransform.Rotation = math.normalize(math.mul(enemyTransform.Rotation, deltaRotation));
            }
            else
            {
                EnemyShooterControlState shooterControlState = enemyShooterControlArray[enemyIndex];
                float3 facingVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(runtimeState.Velocity, in knockbackState);
                enemyTransform.Rotation = EnemySteeringUtility.ResolveDynamicLookRotation(enemyTransform.Rotation,
                                                                                          facingVelocity,
                                                                                          math.max(velocityMaxSpeed, math.length(facingVelocity)),
                                                                                          in shooterControlState,
                                                                                          enemySteeringAggressiveness,
                                                                                          deltaTime);
            }

            enemyRuntimeArray[enemyIndex] = runtimeState;
            enemyKnockbackArray[enemyIndex] = knockbackState;
            enemyTransforms[enemyIndex] = enemyTransform;
        }

        activeEnemiesQuery.CopyFromComponentDataArray(enemyRuntimeArray);
        activeEnemiesQuery.CopyFromComponentDataArray(enemyKnockbackArray);
        activeEnemiesQuery.CopyFromComponentDataArray(enemyTransforms);

    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the active-array index of the boss that owns one minion.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity to inspect.</param>
    /// <param name="bossMinionOwnerLookup">Lookup used to read boss-minion ownership.</param>
    /// <param name="enemyIndexByEntity">Active enemy entity-to-array-index map.</param>
    /// <returns>Owning boss index, or -1 when the enemy is not an active boss-owned minion.</returns>
    private static int ResolveOwnedBossIndex(Entity enemyEntity,
                                             in ComponentLookup<EnemyBossMinionOwner> bossMinionOwnerLookup,
                                             NativeParallelHashMap<Entity, int> enemyIndexByEntity)
    {
        if (!bossMinionOwnerLookup.HasComponent(enemyEntity))
            return -1;

        EnemyBossMinionOwner owner = bossMinionOwnerLookup[enemyEntity];

        if (owner.BossEntity == Entity.Null)
            return -1;

        if (!enemyIndexByEntity.TryGetValue(owner.BossEntity, out int bossIndex))
            return -1;

        return bossIndex;
    }
    #endregion

    #endregion
}
