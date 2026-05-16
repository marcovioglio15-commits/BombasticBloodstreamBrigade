using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Provides Wanderer Basic destination selection and occupancy-aware free-trajectory scoring.
/// </summary>
public static class EnemyPatternWandererUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float ClearancePredictionMinimumSeconds = 0.1f;
    #endregion

    #region Nested Types
    /// <summary>
    /// Immutable occupancy context used while scoring Wanderer candidates.
    /// </summary>
    public readonly struct OccupancyContext
    {
        public readonly NativeArray<Entity> Entities;
        public readonly NativeArray<float3> Positions;
        public readonly NativeArray<float3> Velocities;
        public readonly NativeArray<float> Radii;
        public readonly NativeArray<int> PriorityTiers;
        public readonly NativeParallelMultiHashMap<int, int> CellMap;
        public readonly float InverseCellSize;
        public readonly float MaxRadius;

        public OccupancyContext(NativeArray<Entity> entities,
                                NativeArray<float3> positions,
                                NativeArray<float3> velocities,
                                NativeArray<float> radii,
                                NativeArray<int> priorityTiers,
                                NativeParallelMultiHashMap<int, int> cellMap,
                                float inverseCellSize,
                                float maxRadius)
        {
            Entities = entities;
            Positions = positions;
            Velocities = velocities;
            Radii = radii;
            PriorityTiers = priorityTiers;
            CellMap = cellMap;
            InverseCellSize = inverseCellSize;
            MaxRadius = maxRadius;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves Wanderer Basic desired velocity and updates runtime target/wait/retry state.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyData">Enemy immutable data.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Pattern runtime state to mutate.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from static walls by the active brain movement settings.</param>
    /// <param name="moveSpeed">Resolved movement speed.</param>
    /// <param name="maxSpeed">Resolved max speed.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="deltaTime">Current simulation delta time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether walls collisions are enabled.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <returns>Desired planar velocity for this frame.</returns>
    public static float3 ResolveWandererBasicVelocity(Entity enemyEntity,
                                                      in EnemyData enemyData,
                                                      in EnemyPatternConfig patternConfig,
                                                      ref EnemyPatternRuntimeState patternRuntimeState,
                                                      float3 enemyPosition,
                                                      float3 playerPosition,
                                                      float minimumWallDistance,
                                                      float moveSpeed,
                                                      float maxSpeed,
                                                      float steeringAggressiveness,
                                                      float elapsedTime,
                                                      float deltaTime,
                                                      in PhysicsWorldSingleton physicsWorldSingleton,
                                                      int wallsLayerMask,
                                                      bool wallsEnabled,
                                                      in OccupancyContext occupancyContext)
    {
        return ResolveWandererBasicVelocityInternal(enemyEntity,
                                                    in enemyData,
                                                    in patternConfig,
                                                    ref patternRuntimeState,
                                                    enemyPosition,
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
                                                    in occupancyContext,
                                                    false,
                                                    enemyPosition,
                                                    0f);
    }

    /// <summary>
    /// Resolves bounded Wanderer Basic movement inside one local area, preserving the same target, retry and yield logic.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyData">Enemy immutable data.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Pattern runtime state to mutate.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="wanderAreaCenter">Center of the bounded wander area.</param>
    /// <param name="wanderAreaRadius">Radius of the bounded wander area.</param>
    /// <param name="minimumWallDistance">Extra distance kept from static walls by the active brain movement settings.</param>
    /// <param name="moveSpeed">Resolved movement speed.</param>
    /// <param name="maxSpeed">Resolved max speed.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="deltaTime">Current simulation delta time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether walls collisions are enabled.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <returns>Desired planar velocity for this frame.</returns>
    public static float3 ResolveBoundedWandererBasicVelocity(Entity enemyEntity,
                                                             in EnemyData enemyData,
                                                             in EnemyPatternConfig patternConfig,
                                                             ref EnemyPatternRuntimeState patternRuntimeState,
                                                             float3 enemyPosition,
                                                             float3 wanderAreaCenter,
                                                             float wanderAreaRadius,
                                                             float minimumWallDistance,
                                                             float moveSpeed,
                                                             float maxSpeed,
                                                             float steeringAggressiveness,
                                                             float elapsedTime,
                                                             float deltaTime,
                                                             in PhysicsWorldSingleton physicsWorldSingleton,
                                                             int wallsLayerMask,
                                                             bool wallsEnabled,
                                                             in OccupancyContext occupancyContext)
    {
        return ResolveWandererBasicVelocityInternal(enemyEntity,
                                                    in enemyData,
                                                    in patternConfig,
                                                    ref patternRuntimeState,
                                                    enemyPosition,
                                                    wanderAreaCenter,
                                                    minimumWallDistance,
                                                    moveSpeed,
                                                    maxSpeed,
                                                    steeringAggressiveness,
                                                    elapsedTime,
                                                    deltaTime,
                                                    in physicsWorldSingleton,
                                                    wallsLayerMask,
                                                    wallsEnabled,
                                                    in occupancyContext,
                                                    true,
                                                    wanderAreaCenter,
                                                    wanderAreaRadius);
    }

    /// <summary>
    /// Encodes integer grid coordinates into a stable hash-map key.
    /// </summary>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    /// <returns>Encoded integer key.</returns>
    public static int EncodeCell(int x, int y)
    {
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
    }

    /// <summary>
    /// Computes a planar clearance velocity used to reduce overlap and deadlocks with nearby enemies.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy general priority tier.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="minimumEnemyClearance">Extra minimum clearance from neighbors.</param>
    /// <param name="maxSpeed">Current movement speed cap.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="priorityYieldUrgency">Output urgency in [0..1] when yielding to higher-priority neighbors is required.</param>
    /// <param name="priorityYieldGapNormalized">Output normalized priority-tier gap in [0..1] for active yield pressure.</param>
    /// <param name="occupancyContext">Occupancy context used for neighbor lookup.</param>
    /// <returns>Planar clearance velocity contribution.</returns>
    public static float3 ResolveLocalClearanceVelocity(Entity enemyEntity,
                                                       int selfPriorityTier,
                                                       float3 enemyPosition,
                                                       float bodyRadius,
                                                       float minimumEnemyClearance,
                                                       float maxSpeed,
                                                       float steeringAggressiveness,
                                                       out float priorityYieldUrgency,
                                                       out float priorityYieldGapNormalized,
                                                       in OccupancyContext occupancyContext)
    {
        return EnemyPatternWandererMovementUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                 selfPriorityTier,
                                                                                 enemyPosition,
                                                                                 bodyRadius,
                                                                                 minimumEnemyClearance,
                                                                                 maxSpeed,
                                                                                 steeringAggressiveness,
                                                                                 out priorityYieldUrgency,
                                                                                 out priorityYieldGapNormalized,
                                                                                 in occupancyContext);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves shared Wanderer Basic movement, optionally constraining destinations to one bounded area.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyData">Enemy immutable data.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Pattern runtime state to mutate.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from static walls by the active brain movement settings.</param>
    /// <param name="moveSpeed">Resolved movement speed.</param>
    /// <param name="maxSpeed">Resolved max speed.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="deltaTime">Current simulation delta time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether walls collisions are enabled.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <param name="constrainToArea">Whether the destination must stay inside one local bounded area.</param>
    /// <param name="wanderAreaCenter">Center of the bounded wander area.</param>
    /// <param name="wanderAreaRadius">Radius of the bounded wander area.</param>
    /// <returns>Desired planar velocity for this frame.</returns>
    private static float3 ResolveWandererBasicVelocityInternal(Entity enemyEntity,
                                                               in EnemyData enemyData,
                                                               in EnemyPatternConfig patternConfig,
                                                               ref EnemyPatternRuntimeState patternRuntimeState,
                                                               float3 enemyPosition,
                                                               float3 playerPosition,
                                                               float minimumWallDistance,
                                                               float moveSpeed,
                                                               float maxSpeed,
                                                               float steeringAggressiveness,
                                                               float elapsedTime,
                                                               float deltaTime,
                                                               in PhysicsWorldSingleton physicsWorldSingleton,
                                                               int wallsLayerMask,
                                                               bool wallsEnabled,
                                                               in OccupancyContext occupancyContext,
                                                               bool constrainToArea,
                                                               float3 wanderAreaCenter,
                                                               float wanderAreaRadius)
    {
        if (constrainToArea &&
            patternRuntimeState.WanderHasTarget != 0 &&
            !IsPositionInsideWanderArea(patternRuntimeState.WanderTargetPosition, wanderAreaCenter, wanderAreaRadius))
        {
            patternRuntimeState.WanderHasTarget = 0;
            patternRuntimeState.WanderWaitTimer = 0f;
            patternRuntimeState.WanderRetryTimer = 0f;
        }

        if (patternRuntimeState.WanderWaitTimer > 0f)
        {
            patternRuntimeState.WanderWaitTimer = math.max(0f, patternRuntimeState.WanderWaitTimer - deltaTime);
            return float3.zero;
        }

        float resolvedSteeringAggressiveness = EnemyPatternWandererMovementUtility.ResolveSteeringAggressiveness(steeringAggressiveness);
        float bodyRadius = math.max(0.05f, enemyData.BodyRadius);
        float minimumEnemyClearance = math.max(0f, patternConfig.BasicMinimumEnemyClearance);
        float desiredSpeed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
        desiredSpeed = math.max(0f, desiredSpeed);

        if (patternRuntimeState.WanderRetryTimer > 0f)
            patternRuntimeState.WanderRetryTimer = math.max(0f, patternRuntimeState.WanderRetryTimer - deltaTime);

        if (patternRuntimeState.WanderHasTarget != 0)
        {
            float3 toTarget = patternRuntimeState.WanderTargetPosition - enemyPosition;
            toTarget.y = 0f;
            float distanceToTarget = math.length(toTarget);

            if (distanceToTarget <= math.max(0.05f, patternConfig.BasicArrivalTolerance))
            {
                patternRuntimeState.WanderHasTarget = 0;
                patternRuntimeState.WanderWaitTimer = math.max(0f, patternConfig.BasicWaitCooldownSeconds);
                return float3.zero;
            }

            if (patternRuntimeState.WanderRetryTimer > 0f)
            {
                float3 retryClearanceVelocity = ResolveLocalClearanceVelocity(enemyEntity,
                                                                              enemyData.PriorityTier,
                                                                              enemyPosition,
                                                                              bodyRadius,
                                                                              minimumEnemyClearance,
                                                                              desiredSpeed,
                                                                              resolvedSteeringAggressiveness,
                                                                              out float _,
                                                                              out float _,
                                                                              in occupancyContext);
                float retrySideStepScale = EnemyPatternWandererMovementUtility.ResolveAggressivenessScale(resolvedSteeringAggressiveness, 0.55f, 0.95f);

                if (math.lengthsq(retryClearanceVelocity) > DirectionEpsilon)
                    return retryClearanceVelocity * retrySideStepScale;

                return float3.zero;
            }

            float3 targetDirection = toTarget / math.max(distanceToTarget, DirectionEpsilon);
            float3 desiredVelocity = targetDirection * desiredSpeed;
            float predictionTime = math.max(ClearancePredictionMinimumSeconds, patternConfig.BasicTrajectoryPredictionTime);

            if (EnemyPatternWandererMovementUtility.ShouldYieldToNeighbor(enemyEntity,
                                                                         enemyData.PriorityTier,
                                                                         enemyPosition,
                                                                         desiredVelocity,
                                                                         distanceToTarget,
                                                                         bodyRadius,
                                                                         minimumEnemyClearance,
                                                                         predictionTime,
                                                                         resolvedSteeringAggressiveness,
                                                                         in occupancyContext))
            {
                float3 yieldClearanceVelocity = ResolveLocalClearanceVelocity(enemyEntity,
                                                                              enemyData.PriorityTier,
                                                                              enemyPosition,
                                                                              bodyRadius,
                                                                              minimumEnemyClearance,
                                                                              desiredSpeed,
                                                                              resolvedSteeringAggressiveness,
                                                                              out float _,
                                                                              out float _,
                                                                              in occupancyContext);
                float3 yieldCorrectionVelocity = EnemyPatternWandererMovementUtility.ComposeYieldCorrectionVelocity(desiredVelocity,
                                                                                                                    yieldClearanceVelocity,
                                                                                                                    resolvedSteeringAggressiveness);

                if (math.lengthsq(yieldCorrectionVelocity) > DirectionEpsilon)
                {
                    float yieldCorrectionRetrySeconds = math.max(0.02f, patternConfig.BasicBlockedPathRetryDelay * 0.45f);
                    patternRuntimeState.WanderRetryTimer = math.max(patternRuntimeState.WanderRetryTimer, yieldCorrectionRetrySeconds * 0.55f);
                    return yieldCorrectionVelocity;
                }

                float yieldRetrySeconds = math.max(0.02f, patternConfig.BasicBlockedPathRetryDelay * 0.45f);
                patternRuntimeState.WanderRetryTimer = math.max(patternRuntimeState.WanderRetryTimer, yieldRetrySeconds);
                return float3.zero;
            }

            return desiredVelocity;
        }

        if (patternRuntimeState.WanderRetryTimer > 0f)
        {
            float3 retryClearanceVelocity = ResolveLocalClearanceVelocity(enemyEntity,
                                                                          enemyData.PriorityTier,
                                                                          enemyPosition,
                                                                          bodyRadius,
                                                                          minimumEnemyClearance,
                                                                          desiredSpeed,
                                                                          resolvedSteeringAggressiveness,
                                                                          out float _,
                                                                          out float _,
                                                                          in occupancyContext);
            float retryDriftScale = EnemyPatternWandererMovementUtility.ResolveAggressivenessScale(resolvedSteeringAggressiveness, 0.5f, 0.9f);

            if (math.lengthsq(retryClearanceVelocity) > DirectionEpsilon)
                return retryClearanceVelocity * retryDriftScale;

            return float3.zero;
        }

        bool pickedDestination = TryPickWanderDestination(enemyEntity,
                                                          enemyData.PriorityTier,
                                                          enemyData.BodyRadius,
                                                          in patternConfig,
                                                          enemyPosition,
                                                          playerPosition,
                                                          minimumWallDistance,
                                                          patternRuntimeState.LastWanderDirectionAngle,
                                                          patternRuntimeState.WanderInitialized != 0,
                                                          elapsedTime,
                                                          in physicsWorldSingleton,
                                                          wallsLayerMask,
                                                          wallsEnabled,
                                                          constrainToArea,
                                                          wanderAreaCenter,
                                                          wanderAreaRadius,
                                                          in occupancyContext,
                                                          out float3 selectedTarget,
                                                          out float selectedDirectionAngle);

        if (pickedDestination)
        {
            patternRuntimeState.WanderTargetPosition = selectedTarget;
            patternRuntimeState.LastWanderDirectionAngle = selectedDirectionAngle;
            patternRuntimeState.WanderInitialized = 1;
            patternRuntimeState.WanderHasTarget = 1;

            float3 toTarget = selectedTarget - enemyPosition;
            toTarget.y = 0f;
            float distanceToTarget = math.length(toTarget);
            float3 targetDirection = toTarget / math.max(distanceToTarget, DirectionEpsilon);
            return targetDirection * desiredSpeed;
        }

        patternRuntimeState.WanderRetryTimer = math.max(0f, patternConfig.BasicBlockedPathRetryDelay);
        return float3.zero;
    }

    /// <summary>
    /// Picks the best Wanderer destination by prioritizing collision-safe trajectories and then designer biases.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy general priority tier.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from static walls by the active brain movement settings.</param>
    /// <param name="lastDirectionAngle">Previously selected direction angle in degrees.</param>
    /// <param name="hasPreviousDirection">Whether a previous direction exists.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether walls collisions are enabled.</param>
    /// <param name="constrainToArea">Whether the destination must stay inside one local bounded area.</param>
    /// <param name="wanderAreaCenter">Center of the bounded wander area.</param>
    /// <param name="wanderAreaRadius">Radius of the bounded wander area.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <param name="selectedTarget">Selected target output.</param>
    /// <param name="selectedDirectionAngle">Selected direction angle output.</param>
    /// <returns>True when a valid destination is found.</returns>
    private static bool TryPickWanderDestination(Entity enemyEntity,
                                                 int selfPriorityTier,
                                                 float bodyRadius,
                                                 in EnemyPatternConfig patternConfig,
                                                 float3 enemyPosition,
                                                 float3 playerPosition,
                                                 float minimumWallDistance,
                                                 float lastDirectionAngle,
                                                 bool hasPreviousDirection,
                                                 float elapsedTime,
                                                 in PhysicsWorldSingleton physicsWorldSingleton,
                                                 int wallsLayerMask,
                                                 bool wallsEnabled,
                                                 bool constrainToArea,
                                                 float3 wanderAreaCenter,
                                                 float wanderAreaRadius,
                                                 in OccupancyContext occupancyContext,
                                                 out float3 selectedTarget,
                                                 out float selectedDirectionAngle)
    {
        selectedTarget = enemyPosition;
        selectedDirectionAngle = 0f;

        float searchRadius = math.max(0.5f, patternConfig.BasicSearchRadius);

        if (constrainToArea)
            searchRadius = math.min(searchRadius, math.max(0.5f, wanderAreaRadius));

        float minimumDistance = math.max(0f, patternConfig.BasicMinimumTravelDistance);
        float maximumDistance = math.max(minimumDistance, patternConfig.BasicMaximumTravelDistance);
        float unexploredPreference = math.max(0f, patternConfig.BasicUnexploredDirectionPreference);
        float towardPlayerPreference = math.max(0f, patternConfig.BasicTowardPlayerPreference);
        float minimumEnemyClearance = math.max(0f, patternConfig.BasicMinimumEnemyClearance);
        float trajectoryPredictionTime = math.max(0f, patternConfig.BasicTrajectoryPredictionTime);
        float freeTrajectoryPreference = math.max(0f, patternConfig.BasicFreeTrajectoryPreference);
        bool useInfiniteDirectionSampling = patternConfig.BasicUseInfiniteDirectionSampling != 0;
        int sampleCount = math.max(1, patternConfig.BasicCandidateSampleCount);
        float directionStepDegrees = math.clamp(patternConfig.BasicInfiniteDirectionStepDegrees, 0.5f, 90f);

        if (useInfiniteDirectionSampling)
            sampleCount = math.max(8, (int)math.ceil(360f / directionStepDegrees));

        float phaseAngleRadians = 0f;

        if (useInfiniteDirectionSampling)
        {
            uint phaseSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, (int)(elapsedTime * 11f), 191));
            phaseAngleRadians = EnemyPatternWandererMovementUtility.ResolveHash01(phaseSeed) * math.PI * 2f;
        }

        float stepRadians = math.radians(directionStepDegrees);
        float collisionRadius = math.max(0.01f, bodyRadius + math.max(0f, minimumWallDistance));
        float bestScore = float.NegativeInfinity;
        bool foundCandidate = false;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float angleRadians;

            if (useInfiniteDirectionSampling)
                angleRadians = phaseAngleRadians + stepRadians * sampleIndex;
            else
            {
                uint directionSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 17f)));
                angleRadians = EnemyPatternWandererMovementUtility.ResolveHash01(directionSeed) * math.PI * 2f;
            }

            float3 direction = new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
            uint distanceSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 37f)));
            float distance01 = EnemyPatternWandererMovementUtility.ResolveHash01(distanceSeed);
            float targetDistance = math.lerp(minimumDistance, maximumDistance, distance01);
            targetDistance = math.min(targetDistance, searchRadius);

            if (targetDistance <= 0.01f)
                continue;

            float3 candidate = enemyPosition + direction * targetDistance;
            float3 candidateDisplacement = candidate - enemyPosition;

            if (constrainToArea && !IsPositionInsideWanderArea(candidate, wanderAreaCenter, wanderAreaRadius))
                continue;

            if (wallsEnabled)
            {
                bool blocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       enemyPosition,
                                                                                       candidateDisplacement,
                                                                                       collisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 _);

                if (blocked)
                {
                    float requestedDistanceSquared = math.lengthsq(candidateDisplacement);
                    float allowedDistanceSquared = math.lengthsq(allowedDisplacement);

                    if (requestedDistanceSquared <= DirectionEpsilon || allowedDistanceSquared < requestedDistanceSquared * 0.96f)
                        continue;
                }
            }

            bool freeTrajectory = EnemyPatternWandererMovementUtility.TryEvaluateTrajectoryFreedom(enemyEntity,
                                                                                                   selfPriorityTier,
                                                                                                   enemyPosition,
                                                                                                   candidate,
                                                                                                   math.max(0.01f, bodyRadius),
                                                                                                   minimumEnemyClearance,
                                                                                                   trajectoryPredictionTime,
                                                                                                   in occupancyContext,
                                                                                                   out float freeTrajectoryScore,
                                                                                                   out float freeSpaceScore);

            if (!freeTrajectory)
                continue;

            float3 toPlayer = playerPosition - candidate;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            float playerDistanceScore = 1f / (1f + playerDistance);
            float angleDegrees = math.degrees(math.atan2(direction.x, direction.z));
            float unexploredScore = 1f;

            if (hasPreviousDirection)
            {
                float deltaDegrees = math.abs(math.degrees(math.atan2(math.sin(math.radians(angleDegrees - lastDirectionAngle)),
                                                                      math.cos(math.radians(angleDegrees - lastDirectionAngle)))));
                unexploredScore = deltaDegrees / 180f;
            }

            float safetyScore = math.saturate(freeTrajectoryScore * 0.7f + freeSpaceScore * 0.3f);
            float safetyWeight = math.max(1f, freeTrajectoryPreference * 10f);
            float score = safetyWeight * safetyScore +
                          unexploredPreference * unexploredScore +
                          towardPlayerPreference * playerDistanceScore;

            if (score <= bestScore)
                continue;

            bestScore = score;
            selectedTarget = candidate;
            selectedDirectionAngle = angleDegrees;
            foundCandidate = true;
        }

        return foundCandidate;
    }

    /// <summary>
    /// Returns whether one candidate point lies inside the bounded wander area on the XZ plane.
    /// </summary>
    /// <param name="position">Candidate position to test.</param>
    /// <param name="wanderAreaCenter">Center of the bounded wander area.</param>
    /// <param name="wanderAreaRadius">Radius of the bounded wander area.</param>
    /// <returns>True when the point lies inside the bounded area.</returns>
    private static bool IsPositionInsideWanderArea(float3 position, float3 wanderAreaCenter, float wanderAreaRadius)
    {
        float constrainedRadius = math.max(0f, wanderAreaRadius);
        float3 delta = position - wanderAreaCenter;
        delta.y = 0f;
        return math.lengthsq(delta) <= constrainedRadius * constrainedRadius;
    }

    #endregion

    #endregion
}
