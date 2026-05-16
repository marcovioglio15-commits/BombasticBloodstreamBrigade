using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Orchestrates Coward retreat and bounded-wander patrol state transitions.
/// none.
/// </summary>
public static class EnemyPatternCowardUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float PatrolMinimumRadius = 0.5f;
    private const float PatrolLooseAnchorMultiplier = 1.65f;
    private const float PatrolRetargetRadiusMultiplier = 1.15f;
    private const float ImmediateRetreatAlignmentThreshold = 0.18f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves Coward desired velocity and updates retreat or patrol runtime state depending on player proximity.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyData">Immutable enemy data.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from walls.</param>
    /// <param name="moveSpeed">Resolved movement speed.</param>
    /// <param name="maxSpeed">Resolved max movement speed.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="deltaTime">Current simulation delta time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether wall collision checks are enabled.</param>
    /// <param name="navigationFlowReady">Whether the shared flow field is currently valid.</param>
    /// <param name="navigationGridState">Shared navigation grid state.</param>
    /// <param name="navigationCells">Shared navigation cells buffer.</param>
    /// <param name="occupancyContext">Occupancy context used for clearance and trajectory scoring.</param>
    /// <returns>Desired planar velocity for the current frame.</returns>
    public static float3 ResolveCowardVelocity(Entity enemyEntity,
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
                                               bool navigationFlowReady,
                                               in EnemyNavigationGridState navigationGridState,
                                               DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                               in EnemyPatternWandererUtility.OccupancyContext occupancyContext)
    {
        EnsurePatrolAnchorInitialized(ref patternRuntimeState, enemyPosition);

        float baseSpeed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
        baseSpeed = math.max(0f, baseSpeed);

        if (baseSpeed <= DirectionEpsilon)
        {
            ClearTargetState(ref patternRuntimeState);
            return float3.zero;
        }

        if (patternRuntimeState.WanderWaitTimer > 0f)
            patternRuntimeState.WanderWaitTimer = math.max(0f, patternRuntimeState.WanderWaitTimer - deltaTime);

        if (patternRuntimeState.WanderRetryTimer > 0f)
            patternRuntimeState.WanderRetryTimer = math.max(0f, patternRuntimeState.WanderRetryTimer - deltaTime);

        float3 toPlayer = playerPosition - enemyPosition;
        toPlayer.y = 0f;
        float playerDistance = math.length(toPlayer);
        float detectionRadius = math.max(0f, patternConfig.CowardDetectionRadius);
        float releaseDistance = detectionRadius + math.max(0f, patternConfig.CowardReleaseDistanceBuffer);
        bool retreatAlreadyActive = patternRuntimeState.WanderHasTarget != 0 || patternRuntimeState.WanderRetryTimer > 0f;
        bool shouldRetreat = playerDistance <= detectionRadius ||
                             retreatAlreadyActive && playerDistance <= releaseDistance;

        if (shouldRetreat)
        {
            float retreatSpeed = baseSpeed * ResolveRetreatSpeedMultiplier(in patternConfig, playerDistance);
            retreatSpeed = math.max(0f, retreatSpeed);
            return ResolveRetreatVelocity(enemyEntity,
                                          enemyData.PriorityTier,
                                          enemyData.BodyRadius,
                                          in patternConfig,
                                          ref patternRuntimeState,
                                          enemyPosition,
                                          playerPosition,
                                          minimumWallDistance,
                                          retreatSpeed,
                                          steeringAggressiveness,
                                          elapsedTime,
                                          in physicsWorldSingleton,
                                          wallsLayerMask,
                                          wallsEnabled,
                                          navigationFlowReady,
                                          in navigationGridState,
                                          navigationCells,
                                          in occupancyContext);
        }

        float patrolRadius = math.max(PatrolMinimumRadius, patternConfig.CowardPatrolRadius);
        RefreshPatrolAnchorIfNeeded(ref patternRuntimeState, enemyPosition, patrolRadius);
        float patrolSpeed = baseSpeed * math.max(0.1f, patternConfig.CowardPatrolSpeedMultiplier);
        patrolSpeed = math.max(0f, patrolSpeed);
        return ResolvePatrolVelocity(enemyEntity,
                                     in enemyData,
                                     in patternConfig,
                                     ref patternRuntimeState,
                                     enemyPosition,
                                     minimumWallDistance,
                                     patrolRadius,
                                     patrolSpeed,
                                     steeringAggressiveness,
                                     elapsedTime,
                                     in physicsWorldSingleton,
                                     wallsLayerMask,
                                     wallsEnabled,
                                     in occupancyContext);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves flee behavior while the player remains inside the Coward threat area.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy priority tier.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from walls.</param>
    /// <param name="desiredSpeed">Current flee speed.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether wall collision checks are enabled.</param>
    /// <param name="navigationFlowReady">Whether the shared flow field is currently valid.</param>
    /// <param name="navigationGridState">Shared navigation grid state.</param>
    /// <param name="navigationCells">Shared navigation cells buffer.</param>
    /// <param name="occupancyContext">Occupancy context used for clearance and trajectory scoring.</param>
    /// <returns>Desired planar flee velocity.</returns>
    private static float3 ResolveRetreatVelocity(Entity enemyEntity,
                                                 int selfPriorityTier,
                                                 float bodyRadius,
                                                 in EnemyPatternConfig patternConfig,
                                                 ref EnemyPatternRuntimeState patternRuntimeState,
                                                 float3 enemyPosition,
                                                 float3 playerPosition,
                                                 float minimumWallDistance,
                                                 float desiredSpeed,
                                                 float steeringAggressiveness,
                                                 float elapsedTime,
                                                 in PhysicsWorldSingleton physicsWorldSingleton,
                                                 int wallsLayerMask,
                                                 bool wallsEnabled,
                                                 bool navigationFlowReady,
                                                 in EnemyNavigationGridState navigationGridState,
                                                 DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                 in EnemyPatternWandererUtility.OccupancyContext occupancyContext)
    {
        float3 targetToPlayer = patternRuntimeState.WanderTargetPosition - playerPosition;
        targetToPlayer.y = 0f;
        float targetPlayerDistance = math.length(targetToPlayer);
        float3 currentToPlayer = enemyPosition - playerPosition;
        currentToPlayer.y = 0f;
        float currentPlayerDistance = math.length(currentToPlayer);

        if (patternRuntimeState.WanderHasTarget != 0 &&
            targetPlayerDistance <= currentPlayerDistance + math.max(0.45f, patternConfig.BasicMinimumTravelDistance * 0.25f))
        {
            patternRuntimeState.WanderHasTarget = 0;
            patternRuntimeState.WanderWaitTimer = math.max(patternRuntimeState.WanderWaitTimer,
                                                           EnemyPatternCowardSharedUtility.ResolveDecisionCooldown(enemyEntity, 0.04f, 0.08f));
        }

        float3 retreatFallbackVelocity = float3.zero;
        bool retreatFallbackComputed = false;

        if (patternRuntimeState.WanderHasTarget != 0)
        {
            if (patternRuntimeState.WanderRetryTimer > 0f)
            {
                retreatFallbackVelocity = EnemyPatternCowardMovementUtility.ResolveRetreatFallbackVelocity(enemyEntity,
                                                                                                           selfPriorityTier,
                                                                                                           enemyPosition,
                                                                                                           playerPosition,
                                                                                                           bodyRadius,
                                                                                                           minimumWallDistance,
                                                                                                           desiredSpeed,
                                                                                                           steeringAggressiveness,
                                                                                                           elapsedTime,
                                                                                                           in patternConfig,
                                                                                                           in physicsWorldSingleton,
                                                                                                           wallsLayerMask,
                                                                                                           wallsEnabled,
                                                                                                           navigationFlowReady,
                                                                                                           in navigationGridState,
                                                                                                           navigationCells,
                                                                                                           in occupancyContext);
                retreatFallbackVelocity = EnemyPatternCowardMovementUtility.ResolveWallAwareVelocity(enemyEntity,
                                                                                                     enemyPosition,
                                                                                                     retreatFallbackVelocity,
                                                                                                     bodyRadius,
                                                                                                     minimumWallDistance,
                                                                                                     desiredSpeed,
                                                                                                     in physicsWorldSingleton,
                                                                                                     wallsLayerMask,
                                                                                                     wallsEnabled);
                retreatFallbackComputed = true;
            }

            if (EnemyPatternCowardMovementUtility.TryResolveVelocityTowardTarget(enemyEntity,
                                                                                 selfPriorityTier,
                                                                                 enemyPosition,
                                                                                 bodyRadius,
                                                                                 minimumWallDistance,
                                                                                 desiredSpeed,
                                                                                 steeringAggressiveness,
                                                                                 in patternConfig,
                                                                                 ref patternRuntimeState,
                                                                                 true,
                                                                                 retreatFallbackVelocity,
                                                                                 in physicsWorldSingleton,
                                                                                 wallsLayerMask,
                                                                                 wallsEnabled,
                                                                                 navigationFlowReady,
                                                                                 in navigationGridState,
                                                                                 navigationCells,
                                                                                 in occupancyContext,
                                                                                 out float3 resolvedVelocity))
            {
                return resolvedVelocity;
            }
        }

        if (!retreatFallbackComputed)
        {
            retreatFallbackVelocity = EnemyPatternCowardMovementUtility.ResolveRetreatFallbackVelocity(enemyEntity,
                                                                                                       selfPriorityTier,
                                                                                                       enemyPosition,
                                                                                                       playerPosition,
                                                                                                       bodyRadius,
                                                                                                       minimumWallDistance,
                                                                                                       desiredSpeed,
                                                                                                       steeringAggressiveness,
                                                                                                       elapsedTime,
                                                                                                       in patternConfig,
                                                                                                       in physicsWorldSingleton,
                                                                                                       wallsLayerMask,
                                                                                                       wallsEnabled,
                                                                                                       navigationFlowReady,
                                                                                                       in navigationGridState,
                                                                                                       navigationCells,
                                                                                                       in occupancyContext);
            retreatFallbackVelocity = EnemyPatternCowardMovementUtility.ResolveWallAwareVelocity(enemyEntity,
                                                                                                 enemyPosition,
                                                                                                 retreatFallbackVelocity,
                                                                                                 bodyRadius,
                                                                                                 minimumWallDistance,
                                                                                                 desiredSpeed,
                                                                                                 in physicsWorldSingleton,
                                                                                                 wallsLayerMask,
                                                                                                 wallsEnabled);
        }

        if (patternRuntimeState.WanderWaitTimer > 0f || patternRuntimeState.WanderRetryTimer > 0f)
            return retreatFallbackVelocity;

        bool pickedDestination = EnemyPatternCowardSelectionUtility.TryPickRetreatDestination(enemyEntity,
                                                                                              selfPriorityTier,
                                                                                              bodyRadius,
                                                                                              in patternConfig,
                                                                                              enemyPosition,
                                                                                              playerPosition,
                                                                                              minimumWallDistance,
                                                                                              elapsedTime,
                                                                                              in physicsWorldSingleton,
                                                                                              wallsLayerMask,
                                                                                              wallsEnabled,
                                                                                              navigationFlowReady,
                                                                                              in navigationGridState,
                                                                                              navigationCells,
                                                                                              in occupancyContext,
                                                                                              out float3 selectedTarget,
                                                                                              out float selectedDirectionAngle);

        if (pickedDestination)
        {
            patternRuntimeState.WanderTargetPosition = selectedTarget;
            patternRuntimeState.LastWanderDirectionAngle = selectedDirectionAngle;
            patternRuntimeState.WanderInitialized = 1;
            patternRuntimeState.WanderHasTarget = 1;
            patternRuntimeState.WanderWaitTimer = 0f;

            float3 immediateVelocity = math.normalizesafe(new float3(selectedTarget.x - enemyPosition.x, 0f, selectedTarget.z - enemyPosition.z), float3.zero) * desiredSpeed;
            immediateVelocity = EnemyPatternCowardMovementUtility.ResolvePathAwareRetreatVelocity(enemyPosition,
                                                                                                  selectedTarget - enemyPosition,
                                                                                                  bodyRadius,
                                                                                                  minimumWallDistance,
                                                                                                  desiredSpeed,
                                                                                                  in patternConfig,
                                                                                                  in physicsWorldSingleton,
                                                                                                  wallsLayerMask,
                                                                                                  wallsEnabled,
                                                                                                  navigationFlowReady,
                                                                                                  in navigationGridState,
                                                                                                  navigationCells,
                                                                                                  immediateVelocity);
            immediateVelocity = EnemyPatternCowardMovementUtility.ResolveWallAwareVelocity(enemyEntity,
                                                                                           enemyPosition,
                                                                                           immediateVelocity,
                                                                                           bodyRadius,
                                                                                           minimumWallDistance,
                                                                                           desiredSpeed,
                                                                                           in physicsWorldSingleton,
                                                                                           wallsLayerMask,
                                                                                           wallsEnabled);

            float3 immediateDirection = math.normalizesafe(immediateVelocity, float3.zero);
            float3 targetDirection = math.normalizesafe(selectedTarget - enemyPosition, float3.zero);
            float immediateAlignment = math.dot(immediateDirection, targetDirection);

            if (math.lengthsq(immediateVelocity) > DirectionEpsilon &&
                immediateAlignment >= ImmediateRetreatAlignmentThreshold)
            {
                return immediateVelocity;
            }

            patternRuntimeState.WanderHasTarget = 0;
            patternRuntimeState.WanderRetryTimer = math.max(patternRuntimeState.WanderRetryTimer,
                                                            EnemyPatternCowardSharedUtility.ResolveDecisionCooldown(enemyEntity,
                                                                                                                   0.02f,
                                                                                                                   0.05f));
            return retreatFallbackVelocity;
        }

        patternRuntimeState.WanderRetryTimer = math.max(0.02f, patternConfig.BasicBlockedPathRetryDelay * 0.6f);
        patternRuntimeState.WanderWaitTimer = math.max(patternRuntimeState.WanderWaitTimer,
                                                       EnemyPatternCowardSharedUtility.ResolveDecisionCooldown(enemyEntity, 0.05f, 0.1f));
        return retreatFallbackVelocity;
    }

    /// <summary>
    /// Resolves local patrol movement outside the threat area by reusing bounded Wanderer Basic logic.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyData">Immutable enemy data.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from walls.</param>
    /// <param name="patrolRadius">Current patrol radius.</param>
    /// <param name="desiredSpeed">Current patrol speed.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness scalar.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether wall collision checks are enabled.</param>
    /// <param name="occupancyContext">Occupancy context used for clearance and trajectory scoring.</param>
    /// <returns>Desired planar patrol velocity.</returns>
    private static float3 ResolvePatrolVelocity(Entity enemyEntity,
                                                in EnemyData enemyData,
                                                in EnemyPatternConfig patternConfig,
                                                ref EnemyPatternRuntimeState patternRuntimeState,
                                                float3 enemyPosition,
                                                float minimumWallDistance,
                                                float patrolRadius,
                                                float desiredSpeed,
                                                float steeringAggressiveness,
                                                float elapsedTime,
                                                in PhysicsWorldSingleton physicsWorldSingleton,
                                                int wallsLayerMask,
                                                bool wallsEnabled,
                                                in EnemyPatternWandererUtility.OccupancyContext occupancyContext)
    {
        float3 patrolAnchor = patternRuntimeState.CowardPatrolAnchorPosition;

        if (patternRuntimeState.WanderHasTarget != 0 &&
            !IsInsidePatrolArea(patternRuntimeState.WanderTargetPosition,
                                patrolAnchor,
                                patrolRadius * PatrolRetargetRadiusMultiplier))
        {
            ClearTargetState(ref patternRuntimeState);
        }

        EnemyPatternConfig patrolPatternConfig = BuildPatrolPatternConfig(in patternConfig, patrolRadius);
        return EnemyPatternWandererUtility.ResolveBoundedWandererBasicVelocity(enemyEntity,
                                                                               in enemyData,
                                                                               in patrolPatternConfig,
                                                                               ref patternRuntimeState,
                                                                               enemyPosition,
                                                                               patrolAnchor,
                                                                               patrolRadius,
                                                                               minimumWallDistance,
                                                                               desiredSpeed,
                                                                               desiredSpeed,
                                                                               steeringAggressiveness,
                                                                               elapsedTime,
                                                                               0f,
                                                                               in physicsWorldSingleton,
                                                                               wallsLayerMask,
                                                                               wallsEnabled,
                                                                               in occupancyContext);
    }

    /// <summary>
    /// Ensures the patrol anchor exists for this Coward instance.
    /// </summary>
    /// <param name="patternRuntimeState">Mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    private static void EnsurePatrolAnchorInitialized(ref EnemyPatternRuntimeState patternRuntimeState, float3 enemyPosition)
    {
        if (patternRuntimeState.CowardPatrolAnchorInitialized != 0)
            return;

        patternRuntimeState.CowardPatrolAnchorPosition = enemyPosition;
        patternRuntimeState.CowardPatrolAnchorInitialized = 1;
    }

    /// <summary>
    /// Reanchors the local patrol area when the enemy ended up far away after an extended retreat.
    /// </summary>
    /// <param name="patternRuntimeState">Mutable pattern runtime state.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="patrolRadius">Current patrol radius.</param>
    private static void RefreshPatrolAnchorIfNeeded(ref EnemyPatternRuntimeState patternRuntimeState,
                                                    float3 enemyPosition,
                                                    float patrolRadius)
    {
        float3 anchorDelta = enemyPosition - patternRuntimeState.CowardPatrolAnchorPosition;
        anchorDelta.y = 0f;

        if (math.length(anchorDelta) <= patrolRadius * PatrolLooseAnchorMultiplier)
            return;

        patternRuntimeState.CowardPatrolAnchorPosition = enemyPosition;
    }

    /// <summary>
    /// Clears the transient retreat target while keeping the local patrol anchor intact.
    /// </summary>
    /// <param name="patternRuntimeState">Mutable pattern runtime state.</param>
    private static void ClearTargetState(ref EnemyPatternRuntimeState patternRuntimeState)
    {
        patternRuntimeState.WanderHasTarget = 0;
        patternRuntimeState.WanderWaitTimer = 0f;
        patternRuntimeState.WanderRetryTimer = 0f;
    }

    /// <summary>
    /// Resolves the authored flee speed multiplier from player proximity.
    /// </summary>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="playerDistance">Current distance from the player.</param>
    /// <returns>Speed multiplier applied while retreating.</returns>
    private static float ResolveRetreatSpeedMultiplier(in EnemyPatternConfig patternConfig, float playerDistance)
    {
        float farMultiplier = math.max(0f, patternConfig.CowardRetreatSpeedMultiplierFar);
        float nearMultiplier = math.max(farMultiplier, patternConfig.CowardRetreatSpeedMultiplierNear);
        float influenceDistance = math.max(0.01f, patternConfig.CowardDetectionRadius + math.max(0f, patternConfig.CowardReleaseDistanceBuffer));
        float normalizedProximity = 1f - math.saturate(playerDistance / influenceDistance);
        return math.lerp(farMultiplier, nearMultiplier, normalizedProximity);
    }

    /// <summary>
    /// Builds one temporary patrol config that keeps Coward patrol close to basic Wanderer pacing while remaining bounded.
    /// </summary>
    /// <param name="patternConfig">Source Coward pattern config.</param>
    /// <param name="patrolRadius">Current patrol radius.</param>
    /// <returns>Temporary config used only while patrolling.</returns>
    private static EnemyPatternConfig BuildPatrolPatternConfig(in EnemyPatternConfig patternConfig, float patrolRadius)
    {
        EnemyPatternConfig patrolPatternConfig = patternConfig;
        float constrainedPatrolRadius = math.max(PatrolMinimumRadius, patrolRadius);
        float constrainedSearchRadius = math.min(math.max(0.5f, patternConfig.BasicSearchRadius), constrainedPatrolRadius);
        float constrainedMaximumDistance = math.min(math.max(0.05f, patternConfig.BasicMaximumTravelDistance), constrainedSearchRadius);
        float constrainedMinimumDistance = math.min(math.max(0f, patternConfig.BasicMinimumTravelDistance), constrainedMaximumDistance);

        patrolPatternConfig.BasicSearchRadius = constrainedSearchRadius;
        patrolPatternConfig.BasicMinimumTravelDistance = constrainedMinimumDistance;
        patrolPatternConfig.BasicMaximumTravelDistance = constrainedMaximumDistance;
        patrolPatternConfig.BasicWaitCooldownSeconds = math.max(0f, patternConfig.CowardPatrolWaitSeconds);
        patrolPatternConfig.BasicUnexploredDirectionPreference = math.max(0.65f, patternConfig.BasicUnexploredDirectionPreference);
        patrolPatternConfig.BasicTowardPlayerPreference = 0f;
        return patrolPatternConfig;
    }

    /// <summary>
    /// Returns whether one position stays inside the local Coward patrol area on the XZ plane.
    /// </summary>
    /// <param name="position">Position to test.</param>
    /// <param name="patrolAnchor">Current patrol anchor.</param>
    /// <param name="patrolRadius">Current patrol radius.</param>
    /// <returns>True when the position is inside the patrol area.</returns>
    private static bool IsInsidePatrolArea(float3 position, float3 patrolAnchor, float patrolRadius)
    {
        float constrainedRadius = math.max(0f, patrolRadius);
        float3 delta = position - patrolAnchor;
        delta.y = 0f;
        return math.lengthsq(delta) <= constrainedRadius * constrainedRadius;
    }
    #endregion

    #endregion
}
