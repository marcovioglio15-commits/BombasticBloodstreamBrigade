using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Simulates custom movement modules compiled from EnemyAdvancedPatternPreset.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
[UpdateBefore(typeof(EnemyProjectileHitSystem))]
public partial struct EnemyPatternMovementSystem : ISystem
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float RotationSpeedEpsilon = 1e-4f;
    private const float ClearanceEpsilon = 1e-4f;
    private const float BasicClearanceBlendScale = 0.12f;
    private const float DvdClearanceBlend = 0.75f;
    private const float BasicMinimumForwardSpeedRatio = 0.62f;
    private const float DvdMinimumForwardSpeedRatio = 0.8f;
    private const float WallBlockedRepathThreshold = 0.72f;
    private const float WallClearanceCorrectionRepathThreshold = 0.3f;
    private const float WallComfortRepathThreshold = 0.58f;
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    private EntityQuery occupancyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();
        occupancyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyPatternConfig, EnemyPatternRuntimeState, EnemyRuntimeState, EnemyKnockbackState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<EnemyPatternConfig>();
        state.RequireForUpdate<EnemyPatternRuntimeState>();
        state.RequireForUpdate<EnemyCustomPatternMovementTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Entity playerEntity = Entity.Null;
        float3 playerPosition = float3.zero;

        // Find player
        foreach ((RefRO<LocalTransform> playerTransform,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                                                           .WithAll<PlayerControllerConfig>()
                                                           .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerPosition = playerTransform.ValueRO.Position;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
            return;

        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        ComponentLookup<EnemyShooterControlState> shooterControlLookup = SystemAPI.GetComponentLookup<EnemyShooterControlState>(true);
        ComponentLookup<EnemyElementalRuntimeState> elementalRuntimeLookup = SystemAPI.GetComponentLookup<EnemyElementalRuntimeState>(true);
        EnemyNavigationGridState navigationGridState = default;
        DynamicBuffer<EnemyNavigationCellElement> navigationCells = default;
        bool navigationFlowReady = false;

        if (SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out navigationGridState) &&
            SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out navigationCells))
        {
            navigationFlowReady = navigationGridState.FlowReady != 0 && navigationCells.Length > 0;
        }

        int occupancyCapacity = math.max(1, occupancyQuery.CalculateEntityCount());
        NativeList<Entity> occupancyEntities = new NativeList<Entity>(occupancyCapacity, frameAllocator);
        NativeList<float3> occupancyPositions = new NativeList<float3>(occupancyCapacity, frameAllocator);
        NativeList<float3> occupancyVelocities = new NativeList<float3>(occupancyCapacity, frameAllocator);
        NativeList<float> occupancyRadii = new NativeList<float>(occupancyCapacity, frameAllocator);
        NativeList<byte> occupancyDvdFlags = new NativeList<byte>(occupancyCapacity, frameAllocator);
        NativeList<float> occupancyDvdBounceDamping = new NativeList<float>(occupancyCapacity, frameAllocator);
        NativeList<int> occupancyPriorityTiers = new NativeList<int>(occupancyCapacity, frameAllocator);
        float occupancyMaxRadius = 0.05f;
        float occupancyMaxPlanarSpeed = 0f;

        // Build spatial hash map of nearby enemies for efficient avoidance queries.
        // Only active non-despawning enemies
        // are considered as occupancy since they are the only ones relevant for movement.
        foreach ((RefRO<EnemyData> occupancyEnemyData,
                  RefRO<EnemyPatternConfig> occupancyPatternConfig,
                  RefRO<EnemyPatternRuntimeState> occupancyPatternRuntimeState,
                  RefRO<EnemyRuntimeState> occupancyEnemyRuntimeState,
                  RefRO<EnemyKnockbackState> occupancyEnemyKnockbackState,
                  RefRO<LocalTransform> occupancyEnemyTransform,
                  Entity occupancyEnemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>,
                                    RefRO<EnemyPatternConfig>,
                                    RefRO<EnemyPatternRuntimeState>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<EnemyKnockbackState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            occupancyEntities.Add(occupancyEnemyEntity);
            occupancyPositions.Add(occupancyEnemyTransform.ValueRO.Position);

            float3 planarVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(occupancyEnemyRuntimeState.ValueRO.Velocity,
                                                                                        in occupancyEnemyKnockbackState.ValueRO);
            planarVelocity.y = 0f;
            occupancyVelocities.Add(planarVelocity);
            float3 occupancyToPlayer = playerPosition - occupancyEnemyTransform.ValueRO.Position;
            occupancyToPlayer.y = 0f;
            float occupancyPlayerDistance = math.length(occupancyToPlayer);
            EnemyPatternConfig occupancyActivePatternConfig = EnemyPatternMovementRuntimeUtility.BuildActivePatternConfig(in occupancyPatternConfig.ValueRO,
                                                                                                                          in occupancyPatternRuntimeState.ValueRO,
                                                                                                                          occupancyPlayerDistance);
            occupancyDvdFlags.Add(occupancyActivePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd ? (byte)1 : (byte)0);
            occupancyDvdBounceDamping.Add(math.clamp(occupancyActivePatternConfig.DvdBounceDamping, 0f, 1f));

            float occupancyRadius = math.max(0.05f, occupancyEnemyData.ValueRO.BodyRadius);
            occupancyRadii.Add(occupancyRadius);
            occupancyPriorityTiers.Add(math.clamp(occupancyEnemyData.ValueRO.PriorityTier, -128, 128));
            float occupancySpeed = math.length(planarVelocity);

            if (occupancyRadius > occupancyMaxRadius)
                occupancyMaxRadius = occupancyRadius;

            if (occupancySpeed > occupancyMaxPlanarSpeed)
                occupancyMaxPlanarSpeed = occupancySpeed;
        }

        int occupancyCount = occupancyEntities.Length;
        float occupancyCellSize = math.max(0.25f, occupancyMaxRadius + 0.35f);
        float occupancyInverseCellSize = 1f / occupancyCellSize;
        NativeParallelMultiHashMap<int, int> occupancyCellMap = new NativeParallelMultiHashMap<int, int>(math.max(1, occupancyCount * 2), frameAllocator);

        // Populate spatial hash map with nearby enemy positions.
        for (int occupancyIndex = 0; occupancyIndex < occupancyCount; occupancyIndex++)
        {
            float3 occupancyPosition = occupancyPositions[occupancyIndex];
            int cellX = (int)math.floor(occupancyPosition.x * occupancyInverseCellSize);
            int cellY = (int)math.floor(occupancyPosition.z * occupancyInverseCellSize);
            occupancyCellMap.Add(EnemyPatternWandererUtility.EncodeCell(cellX, cellY), occupancyIndex);
        }

        EnemyPatternWandererUtility.OccupancyContext occupancyContext = new EnemyPatternWandererUtility.OccupancyContext(occupancyEntities.AsArray(),
                                                                                                                          occupancyPositions.AsArray(),
                                                                                                                          occupancyVelocities.AsArray(),
                                                                                                                          occupancyRadii.AsArray(),
                                                                                                                          occupancyPriorityTiers.AsArray(),
                                                                                                                          occupancyCellMap,
                                                                                                                          occupancyInverseCellSize,
                                                                                                                          occupancyMaxRadius);
        EnemyPatternDvdCollisionUtility.OccupancyContext dvdCollisionContext = new EnemyPatternDvdCollisionUtility.OccupancyContext(occupancyEntities.AsArray(),
                                                                                                                                    occupancyPositions.AsArray(),
                                                                                                                                    occupancyVelocities.AsArray(),
                                                                                                                                    occupancyRadii.AsArray(),
                                                                                                                                    occupancyDvdFlags.AsArray(),
                                                                                                                                    occupancyDvdBounceDamping.AsArray(),
                                                                                                                                    occupancyCellMap,
                                                                                                                                    occupancyInverseCellSize,
                                                                                                                                    occupancyMaxRadius,
                                                                                                                                    occupancyMaxPlanarSpeed);

        foreach ((RefRO<EnemyData> enemyData,
                  RefRO<EnemyPatternConfig> patternConfig,
                  RefRW<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRW<EnemyRuntimeState> enemyRuntimeState,
                  RefRW<EnemyKnockbackState> enemyKnockbackState,
                  RefRW<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>,
                                    RefRO<EnemyPatternConfig>,
                                    RefRW<EnemyPatternRuntimeState>,
                                    RefRW<EnemyRuntimeState>,
                                    RefRW<EnemyKnockbackState>,
                                    RefRW<LocalTransform>>()
                             .WithAll<EnemyActive, EnemyCustomPatternMovementTag>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            EnemyData currentEnemyData = enemyData.ValueRO;
            EnemyPatternConfig currentPatternConfig = patternConfig.ValueRO;
            EnemyPatternRuntimeState currentPatternRuntimeState = patternRuntimeState.ValueRO;
            EnemyRuntimeState currentEnemyRuntimeState = enemyRuntimeState.ValueRO;
            EnemyKnockbackState currentEnemyKnockbackState = enemyKnockbackState.ValueRO;
            LocalTransform currentEnemyTransform = enemyTransform.ValueRO;

            float elementalSlowPercent = 0f;

            if (elementalRuntimeLookup.HasComponent(enemyEntity))
                elementalSlowPercent = math.clamp(elementalRuntimeLookup[enemyEntity].SlowPercent, 0f, 100f);

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            float moveSpeed = math.max(0f, currentEnemyData.MoveSpeed) * slowMultiplier;
            float maxSpeed = math.max(0f, currentEnemyData.MaxSpeed) * slowMultiplier;
            float acceleration = math.max(0f, currentEnemyData.Acceleration);
            float deceleration = math.max(0f, currentEnemyData.Deceleration);
            float steeringAggressiveness = EnemyPatternMovementMathUtility.ResolveSteeringAggressiveness(currentEnemyData.SteeringAggressiveness);
            float minimumWallDistance = math.max(0f, currentEnemyData.MinimumWallDistance);
            float3 toPlayer = playerPosition - currentEnemyTransform.Position;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            bool wasShortRangeInteractionActive = currentPatternRuntimeState.ShortRangeInteractionActive != 0;
            bool wasShortRangeOverrideDriving = EnemyPatternMovementRuntimeUtility.IsShortRangeOverrideDriving(in currentPatternConfig,
                                                                                                                in currentPatternRuntimeState,
                                                                                                                wasShortRangeInteractionActive);

            if (currentPatternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
                EnemyPatternShortRangeDashUtility.UpdateCooldown(ref currentPatternRuntimeState, deltaTime);

            bool shortRangeInteractionActive = EnemyPatternMovementRuntimeUtility.ResolveShortRangeInteractionActive(in currentPatternConfig,
                                                                                                                     in currentPatternRuntimeState,
                                                                                                                     playerDistance);

            if (!shortRangeInteractionActive &&
                currentPatternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
            {
                EnemyPatternShortRangeDashUtility.HandleShortRangeBandReleased(ref currentPatternRuntimeState);
            }

            currentPatternRuntimeState.ShortRangeInteractionActive = shortRangeInteractionActive ? (byte)1 : (byte)0;
            EnemyPatternConfig activePatternConfig = EnemyPatternMovementRuntimeUtility.BuildActivePatternConfig(in currentPatternConfig,
                                                                                                                 in currentPatternRuntimeState,
                                                                                                                 playerDistance);
            bool shortRangeOverrideDriving = EnemyPatternMovementRuntimeUtility.IsShortRangeOverrideDriving(in currentPatternConfig,
                                                                                                            in currentPatternRuntimeState,
                                                                                                            shortRangeInteractionActive);

            if (shortRangeOverrideDriving && !wasShortRangeOverrideDriving)
                EnemyPatternMovementRuntimeUtility.PrepareShortRangeTakeover(in currentPatternConfig, ref currentPatternRuntimeState);

            bool shortRangeTakeoverThisFrame = shortRangeOverrideDriving && !wasShortRangeOverrideDriving;
            EnemyShooterControlState shooterControlState = default;

            if (shooterControlLookup.HasComponent(enemyEntity))
                shooterControlState = shooterControlLookup[enemyEntity];

            bool movementLocked = shooterControlState.MovementLocked != 0;
            bool ignoreSteeringAndPriority = EnemyPatternMovementMathUtility.ShouldIgnoreSteeringAndPriority(in activePatternConfig);

            if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Grunt)
            {
                patternRuntimeState.ValueRW = currentPatternRuntimeState;
                continue;
            }

            float3 desiredVelocity = float3.zero;
            float priorityYieldUrgency = 0f;
            float priorityYieldGapNormalized = 0f;
            EnemyShortRangeDashPhase shortRangeDashPhase = EnemyShortRangeDashPhase.Idle;

            // Determine desired velocity based on movement pattern kind and configuration.
            switch (activePatternConfig.MovementKind)
            {
                case EnemyCompiledMovementPatternKind.Stationary:
                    desiredVelocity = float3.zero;
                    break;

                case EnemyCompiledMovementPatternKind.WandererBasic:
                    desiredVelocity = EnemyPatternWandererUtility.ResolveWandererBasicVelocity(enemyEntity,
                                                                                               in currentEnemyData,
                                                                                               in activePatternConfig,
                                                                                               ref currentPatternRuntimeState,
                                                                                               currentEnemyTransform.Position,
                                                                                               playerPosition,
                                                                                               minimumWallDistance,
                                                                                               moveSpeed,
                                                                                               maxSpeed,
                                                                                               steeringAggressiveness,
                                                                                               elapsedTime,
                                                                                               deltaTime,
                                                                                               in physicsWorldSingleton,
                                                                                               wallsLayerMask,
                                                                                               wallsEnabled,
                                                                                               in occupancyContext);
                    break;

                case EnemyCompiledMovementPatternKind.Coward:
                    desiredVelocity = EnemyPatternCowardUtility.ResolveCowardVelocity(enemyEntity,
                                                                                      in currentEnemyData,
                                                                                      in activePatternConfig,
                                                                                      ref currentPatternRuntimeState,
                                                                                      currentEnemyTransform.Position,
                                                                                      playerPosition,
                                                                                      minimumWallDistance,
                                                                                      moveSpeed,
                                                                                      maxSpeed,
                                                                                      steeringAggressiveness,
                                                                                      elapsedTime,
                                                                                      deltaTime,
                                                                                      in physicsWorldSingleton,
                                                                                      wallsLayerMask,
                                                                                      wallsEnabled,
                                                                                      navigationFlowReady,
                                                                                      in navigationGridState,
                                                                                      navigationCells,
                                                                                      in occupancyContext);
                    break;

                case EnemyCompiledMovementPatternKind.WandererDvd:
                    desiredVelocity = EnemyPatternMovementRuntimeUtility.ResolveWandererDvdVelocity(enemyEntity,
                                                                                                   in activePatternConfig,
                                                                                                   ref currentPatternRuntimeState,
                                                                                                   moveSpeed,
                                                                                                   maxSpeed,
                                                                                                   elapsedTime);
                    break;

                case EnemyCompiledMovementPatternKind.ShortRangeDash:
                    desiredVelocity = EnemyPatternShortRangeDashUtility.ResolveVelocity(enemyEntity,
                                                                                       in activePatternConfig,
                                                                                       ref currentPatternRuntimeState,
                                                                                       currentEnemyTransform.Position,
                                                                                       playerPosition,
                                                                                       moveSpeed,
                                                                                       deltaTime,
                                                                                       elapsedTime,
                                                                                       out shortRangeDashPhase);
                    break;

                default:
                    desiredVelocity = float3.zero;
                    break;
            }

            if (shortRangeDashPhase != EnemyShortRangeDashPhase.Idle)
                movementLocked = false;

            if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd ||
                activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
            {
                if (!ignoreSteeringAndPriority)
                {
                    float clearanceSpeedCap = maxSpeed > 0f ? maxSpeed : moveSpeed;
                    float minimumEnemyClearance = math.max(0f, activePatternConfig.BasicMinimumEnemyClearance);
                    float3 clearanceVelocity = EnemyPatternWandererUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                                         currentEnemyData.PriorityTier,
                                                                                                         currentEnemyTransform.Position,
                                                                                                         currentEnemyData.BodyRadius,
                                                                                                         minimumEnemyClearance,
                                                                                                         clearanceSpeedCap,
                                                                                                         steeringAggressiveness,
                                                                                                         out float clearanceYieldUrgency,
                                                                                                         out float clearanceYieldGapNormalized,
                                                                                                         in occupancyContext);

                    if (clearanceYieldUrgency > priorityYieldUrgency)
                        priorityYieldUrgency = clearanceYieldUrgency;

                    if (clearanceYieldGapNormalized > priorityYieldGapNormalized)
                        priorityYieldGapNormalized = clearanceYieldGapNormalized;

                    if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                        activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
                    {
                        float clearanceBlendScale = EnemyPatternMovementMathUtility.ResolveAggressivenessScale(steeringAggressiveness, 0.78f, 1.25f);
                        float clearanceBlend = math.saturate(activePatternConfig.BasicFreeTrajectoryPreference * BasicClearanceBlendScale * clearanceBlendScale);
                        desiredVelocity = EnemyPatternMovementMathUtility.ComposeDesiredVelocityWithClearance(desiredVelocity,
                                                                                                              clearanceVelocity,
                                                                                                              clearanceBlend,
                                                                                                              BasicMinimumForwardSpeedRatio);
                    }
                    else
                    {
                        float dvdClearanceBlend = math.saturate(DvdClearanceBlend * EnemyPatternMovementMathUtility.ResolveAggressivenessScale(steeringAggressiveness, 0.85f, 1.35f));
                        desiredVelocity = EnemyPatternMovementMathUtility.ComposeDesiredVelocityWithClearance(desiredVelocity,
                                                                                                              clearanceVelocity,
                                                                                                              dvdClearanceBlend,
                                                                                                              DvdMinimumForwardSpeedRatio);
                    }
                }
            }

            if (movementLocked)
                desiredVelocity = float3.zero;
            else if (wallsEnabled &&
                     (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                      activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward))
            {
                float patternDesiredSpeed = math.length(desiredVelocity);
                float wallComfortDesiredSpeed = math.max(patternDesiredSpeed, maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed);
                desiredVelocity = EnemyPatternMovementRuntimeUtility.ResolveWallComfortVelocity(desiredVelocity,
                                                                                                currentEnemyTransform.Position,
                                                                                                currentEnemyData.BodyRadius,
                                                                                                minimumWallDistance,
                                                                                                wallComfortDesiredSpeed,
                                                                                                in physicsWorldSingleton,
                                                                                                wallsLayerMask,
                                                                                                wallsEnabled,
                                                                                                out float wallComfortPressure);

                if (wallComfortPressure >= WallComfortRepathThreshold)
                    EnemyPatternMovementRuntimeUtility.ConsumeWanderTargetOnClearance(ref currentPatternRuntimeState, in activePatternConfig);
            }

            float effectiveMaxSpeed = maxSpeed;
            float dashDesiredSpeed = math.length(desiredVelocity);

            if (shortRangeDashPhase == EnemyShortRangeDashPhase.Dashing && dashDesiredSpeed > effectiveMaxSpeed)
                effectiveMaxSpeed = dashDesiredSpeed;

            if (!movementLocked && effectiveMaxSpeed > 0f && priorityYieldUrgency > 0f)
            {
                float speedBoost = EnemyPatternMovementMathUtility.ResolvePriorityYieldSpeedBoost(priorityYieldUrgency, priorityYieldGapNormalized, steeringAggressiveness);
                effectiveMaxSpeed *= 1f + speedBoost;
            }

            float desiredSpeed = dashDesiredSpeed;

            if (effectiveMaxSpeed > 0f && desiredSpeed > effectiveMaxSpeed && desiredSpeed > DirectionEpsilon)
                desiredVelocity *= effectiveMaxSpeed / desiredSpeed;

            float3 velocityDelta = desiredVelocity - currentEnemyRuntimeState.Velocity;
            float accelerationRate = EnemyPatternMovementMathUtility.ResolveVelocityChangeRate(currentEnemyRuntimeState.Velocity,
                                                                                               desiredVelocity,
                                                                                               acceleration,
                                                                                               deceleration);

            if (!movementLocked && priorityYieldUrgency > 0f)
            {
                float accelerationBoost = EnemyPatternMovementMathUtility.ResolvePriorityYieldAccelerationBoost(priorityYieldUrgency, priorityYieldGapNormalized, steeringAggressiveness);
                accelerationRate *= 1f + accelerationBoost;
            }

            if (!movementLocked && shortRangeOverrideDriving)
                accelerationRate *= EnemyPatternMovementMathUtility.ResolveShortRangePriorityAccelerationMultiplier(shortRangeTakeoverThisFrame);

            float maxVelocityDelta = accelerationRate * deltaTime;
            float velocityDeltaMagnitude = math.length(velocityDelta);

            if (shortRangeDashPhase == EnemyShortRangeDashPhase.Dashing)
                currentEnemyRuntimeState.Velocity = desiredVelocity;
            else if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > DirectionEpsilon)
                currentEnemyRuntimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
            else
                currentEnemyRuntimeState.Velocity = desiredVelocity;

            if (effectiveMaxSpeed > 0f)
            {
                float velocityMagnitude = math.length(currentEnemyRuntimeState.Velocity);

                if (velocityMagnitude > effectiveMaxSpeed && velocityMagnitude > DirectionEpsilon)
                    currentEnemyRuntimeState.Velocity *= effectiveMaxSpeed / velocityMagnitude;
            }

            float3 desiredDisplacement = currentEnemyRuntimeState.Velocity * deltaTime;
            float3 nextPosition = currentEnemyTransform.Position;
            float baseCollisionRadius = math.max(0.01f, currentEnemyData.BodyRadius);
            float additionalWallClearance = EnemyPatternMovementRuntimeUtility.ResolveWallClearanceForMovement(in activePatternConfig,
                                                                                                               in currentEnemyData);
            float wallCollisionRadius = math.max(0.01f, baseCollisionRadius + additionalWallClearance);
            bool expandedWallClearance = wallCollisionRadius > baseCollisionRadius + ClearanceEpsilon;

            if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd &&
                EnemyPatternDvdCollisionUtility.TryResolveBounceVelocity(enemyEntity,
                                                                        currentEnemyTransform.Position,
                                                                        currentEnemyRuntimeState.Velocity,
                                                                        baseCollisionRadius,
                                                                        activePatternConfig.DvdBounceDamping,
                                                                        deltaTime,
                                                                        in dvdCollisionContext,
                                                                        out float3 bouncedVelocity,
                                                                        out float collisionTimeSeconds))
            {
                float preCollisionTime = math.clamp(collisionTimeSeconds, 0f, deltaTime);
                float postCollisionTime = math.max(0f, deltaTime - preCollisionTime);
                float3 preCollisionVelocity = currentEnemyRuntimeState.Velocity;
                currentEnemyRuntimeState.Velocity = bouncedVelocity;
                currentPatternRuntimeState.DvdDirection = math.normalizesafe(new float3(bouncedVelocity.x, 0f, bouncedVelocity.z), currentPatternRuntimeState.DvdDirection);
                desiredDisplacement = preCollisionVelocity * preCollisionTime + bouncedVelocity * postCollisionTime;
            }

            // Handle wall collisions and clearance if enabled, otherwise apply desired displacement directly.
            if (wallsEnabled)
            {
                bool clearanceReachedTarget = false;
                float requestedDisplacementDistance = math.length(desiredDisplacement);
                bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       currentEnemyTransform.Position,
                                                                                       desiredDisplacement,
                                                                                       wallCollisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 hitNormal);

                nextPosition += allowedDisplacement;

                // Handle wall collisions based on movement kind and pattern configuration.
                if (hitWall)
                {
                    if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd)
                    {
                        float3 wallBouncedVelocity = WorldWallCollisionUtility.ComputeBounceVelocity(currentEnemyRuntimeState.Velocity,
                                                                                                     hitNormal,
                                                                                                     activePatternConfig.DvdBounceDamping);

                        if (math.lengthsq(wallBouncedVelocity) <= DirectionEpsilon)
                            wallBouncedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, hitNormal);

                        currentEnemyRuntimeState.Velocity = wallBouncedVelocity;
                        currentPatternRuntimeState.DvdDirection = math.normalizesafe(new float3(wallBouncedVelocity.x, 0f, wallBouncedVelocity.z), float3.zero);

                        if (activePatternConfig.DvdCornerNudgeDistance > 0f)
                            nextPosition += math.normalizesafe(hitNormal, float3.zero) * activePatternConfig.DvdCornerNudgeDistance;
                    }
                    else
                    {
                        currentEnemyRuntimeState.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, hitNormal);
                        
                        if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
                        {
                            EnemyPatternShortRangeDashUtility.HandleWallHit(in activePatternConfig, ref currentPatternRuntimeState);
                        }
                        else
                        {
                            currentPatternRuntimeState.WanderRetryTimer = math.max(currentPatternRuntimeState.WanderRetryTimer,
                                                                                  activePatternConfig.BasicBlockedPathRetryDelay);

                            if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                                activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
                            {
                                float allowedDisplacementDistance = math.length(allowedDisplacement);
                                float blockedDisplacementRatio = EnemyPatternMovementRuntimeUtility.ResolveBlockedDisplacementRatio(requestedDisplacementDistance, allowedDisplacementDistance);

                                if (blockedDisplacementRatio >= WallBlockedRepathThreshold)
                                    clearanceReachedTarget = true;
                            }
                        }
                    }
                }

                if (expandedWallClearance)
                {
                    bool requiresClearanceCorrection = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                                             nextPosition,
                                                                                                             wallCollisionRadius,
                                                                                                             wallsLayerMask,
                                                                                                             out float3 clearanceCorrectionDisplacement,
                                                                                                             out float3 clearanceNormal);

                    if (requiresClearanceCorrection)
                    {
                        nextPosition += clearanceCorrectionDisplacement;
                        currentEnemyRuntimeState.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, clearanceNormal);

                        if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
                        {
                            EnemyPatternShortRangeDashUtility.HandleWallHit(in activePatternConfig, ref currentPatternRuntimeState);
                        }
                        else if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                                 activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
                        {
                            float clearanceCorrectionDistance = math.length(clearanceCorrectionDisplacement);
                            float clearanceCorrectionRatio = math.saturate(clearanceCorrectionDistance / math.max(0.01f, wallCollisionRadius));

                            if (clearanceCorrectionRatio >= WallClearanceCorrectionRepathThreshold)
                                clearanceReachedTarget = true;
                        }
                    }
                }

                if (clearanceReachedTarget)
                    EnemyPatternMovementRuntimeUtility.ConsumeWanderTargetOnClearance(ref currentPatternRuntimeState, in activePatternConfig);
            }
            else
            {
                nextPosition += desiredDisplacement;
            }

            EnemyKnockbackRuntimeUtility.ApplyDisplacement(ref currentEnemyKnockbackState,
                                                           ref nextPosition,
                                                           wallCollisionRadius,
                                                           in physicsWorldSingleton,
                                                           wallsLayerMask,
                                                           wallsEnabled,
                                                           deltaTime);
            nextPosition.y = currentEnemyTransform.Position.y;
            currentEnemyTransform.Position = nextPosition;

            bool freezeRotation = activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Stationary &&
                                  activePatternConfig.StationaryFreezeRotation != 0;

            if (!freezeRotation)
            {
                if (activePatternConfig.MovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash &&
                    EnemyPatternShortRangeDashUtility.TryResolveLookDirection(in currentPatternRuntimeState, out float3 dashLookDirection))
                {
                    float lookTurnRateDegrees = EnemySteeringUtility.ResolveAggressivenessScale(steeringAggressiveness,
                                                                                                 EnemySteeringUtility.LookRotationMinDegreesPerSecond,
                                                                                                 EnemySteeringUtility.LookRotationMaxDegreesPerSecond);
                    float maxRadiansDelta = math.radians(lookTurnRateDegrees) * math.max(0f, deltaTime);
                    currentEnemyTransform.Rotation = EnemySteeringUtility.RotateTowardsPlanar(currentEnemyTransform.Rotation,
                                                                                               dashLookDirection,
                                                                                               maxRadiansDelta);
                }
                else
                {
                    float rotationSpeedDegreesPerSecond = currentEnemyData.RotationSpeedDegreesPerSecond;
                    bool hasSelfRotation = math.abs(rotationSpeedDegreesPerSecond) > RotationSpeedEpsilon;

                    if (hasSelfRotation)
                    {
                        float deltaYawRadians = math.radians(rotationSpeedDegreesPerSecond) * deltaTime;
                        quaternion deltaRotation = quaternion.RotateY(deltaYawRadians);
                        currentEnemyTransform.Rotation = math.normalize(math.mul(currentEnemyTransform.Rotation, deltaRotation));
                    }
                    else
                    {
                        float3 facingVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(currentEnemyRuntimeState.Velocity,
                                                                                                    in currentEnemyKnockbackState);
                        currentEnemyTransform.Rotation = EnemySteeringUtility.ResolveDynamicLookRotation(currentEnemyTransform.Rotation,
                                                                                                          facingVelocity,
                                                                                                          math.max(math.max(moveSpeed, effectiveMaxSpeed), math.length(facingVelocity)),
                                                                                                          in shooterControlState,
                                                                                                          steeringAggressiveness,
                                                                                                          deltaTime);
                    }
                }
            }

            enemyRuntimeState.ValueRW = currentEnemyRuntimeState;
            enemyKnockbackState.ValueRW = currentEnemyKnockbackState;
            patternRuntimeState.ValueRW = currentPatternRuntimeState;
            enemyTransform.ValueRW = currentEnemyTransform;
        }

    }
    #endregion

    #endregion
}
