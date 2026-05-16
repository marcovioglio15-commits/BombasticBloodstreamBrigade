using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Selects Coward retreat destinations using wall, occupancy and navigation topology scoring.
/// none.
/// </summary>
public static class EnemyPatternCowardSelectionUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float RetreatNavigationGainNormalization = 6f;
    private const float OpenSpaceProbeMaximumDistance = 1.6f;
    private const int OpenSpaceSampleCount = 4;
    private const float CandidateClearanceProbeSpeedMinimum = 0.75f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Picks the best retreat destination by combining free-space, wall openness, and navigation-cost gains away from the player.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy priority tier.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="minimumWallDistance">Extra distance kept from walls.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether wall collision checks are enabled.</param>
    /// <param name="navigationFlowReady">Whether the shared flow field is currently valid.</param>
    /// <param name="navigationGridState">Shared navigation grid state.</param>
    /// <param name="navigationCells">Shared navigation cells buffer.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <param name="selectedTarget">Selected target output.</param>
    /// <param name="selectedDirectionAngle">Selected direction angle output.</param>
    /// <returns>True when a valid retreat destination is found.</returns>
    public static bool TryPickRetreatDestination(Entity enemyEntity,
                                                 int selfPriorityTier,
                                                 float bodyRadius,
                                                 in EnemyPatternConfig patternConfig,
                                                 float3 enemyPosition,
                                                 float3 playerPosition,
                                                 float minimumWallDistance,
                                                 float elapsedTime,
                                                 in PhysicsWorldSingleton physicsWorldSingleton,
                                                 int wallsLayerMask,
                                                 bool wallsEnabled,
                                                 bool navigationFlowReady,
                                                 in EnemyNavigationGridState navigationGridState,
                                                 DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                 in EnemyPatternWandererUtility.OccupancyContext occupancyContext,
                                                 out float3 selectedTarget,
                                                 out float selectedDirectionAngle)
    {
        selectedTarget = enemyPosition;
        selectedDirectionAngle = 0f;

        float searchRadius = math.max(0.5f, patternConfig.BasicSearchRadius);
        float minimumDistance = math.max(0f, patternConfig.BasicMinimumTravelDistance);
        float maximumDistance = math.max(minimumDistance, patternConfig.BasicMaximumTravelDistance);
        float minimumEnemyClearance = EnemyPatternCowardSharedUtility.ResolveRetreatEnemyClearance(bodyRadius,
                                                                                                   patternConfig.BasicMinimumEnemyClearance);
        float trajectoryPredictionTime = math.max(0f, patternConfig.BasicTrajectoryPredictionTime);
        float freeTrajectoryPreference = math.max(0f, patternConfig.BasicFreeTrajectoryPreference);
        float retreatDirectionPreference = math.max(0f, patternConfig.CowardRetreatDirectionPreference);
        float openSpacePreference = math.max(0f, patternConfig.CowardOpenSpacePreference);
        float navigationPreference = math.max(0f, patternConfig.CowardNavigationPreference);
        float clearanceProbeSpeed = math.max(CandidateClearanceProbeSpeedMinimum,
                                             math.min(maximumDistance, searchRadius));
        bool useInfiniteDirectionSampling = patternConfig.BasicUseInfiniteDirectionSampling != 0;
        int sampleCount = EnemyPatternCowardSharedUtility.ResolveSampleCount(in patternConfig, useInfiniteDirectionSampling, 1);
        float directionStepDegrees = math.clamp(patternConfig.BasicInfiniteDirectionStepDegrees, 0.5f, 90f);
        float phaseAngleRadians = EnemyPatternCowardSharedUtility.ResolvePhaseAngleRadians(enemyEntity,
                                                                                           elapsedTime,
                                                                                           useInfiniteDirectionSampling,
                                                                                           733);
        float stepRadians = math.radians(directionStepDegrees);
        float3 retreatDirection = EnemyPatternCowardSharedUtility.ResolveRetreatDirection(enemyEntity,
                                                                                          enemyPosition,
                                                                                          playerPosition,
                                                                                          elapsedTime);
        float currentPlayerDistance = math.distance(new float2(enemyPosition.x, enemyPosition.z), new float2(playerPosition.x, playerPosition.z));
        float collisionRadius = math.max(0.01f, bodyRadius + math.max(0f, minimumWallDistance));
        float bestScore = float.NegativeInfinity;
        bool foundCandidate = false;
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float angleRadians = EnemyPatternCowardSharedUtility.ResolveSampleAngleRadians(enemyEntity,
                                                                                           sampleIndex,
                                                                                           elapsedTime,
                                                                                           useInfiniteDirectionSampling,
                                                                                           phaseAngleRadians,
                                                                                           stepRadians);
            float3 sampleDirection = new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
            float alignmentToRetreat = math.saturate(math.dot(sampleDirection, retreatDirection) * 0.5f + 0.5f);
            if (alignmentToRetreat <= 0.2f)
                continue;
            float targetDistance = EnemyPatternCowardSharedUtility.ResolveSampleDistance(enemyEntity,
                                                                                         sampleIndex,
                                                                                         elapsedTime,
                                                                                         minimumDistance,
                                                                                         maximumDistance,
                                                                                         searchRadius);
            if (targetDistance <= 0.01f)
                continue;
            float3 candidate = enemyPosition + sampleDirection * targetDistance;
            if (!IsCandidateWallSafe(enemyPosition,
                                     candidate,
                                     collisionRadius,
                                     in physicsWorldSingleton,
                                     wallsLayerMask,
                                     wallsEnabled))
            {
                continue;
            }
            if (!EnemyPatternWandererMovementUtility.TryEvaluateTrajectoryFreedom(enemyEntity,
                                                                                  selfPriorityTier,
                                                                                  enemyPosition,
                                                                                  candidate,
                                                                                  math.max(0.01f, bodyRadius),
                                                                                  minimumEnemyClearance,
                                                                                  trajectoryPredictionTime,
                                                                                  in occupancyContext,
                                                                                  out float freeTrajectoryScore,
                                                                                  out float freeSpaceScore))
            {
                continue;
            }
            if (!TryResolveNavigationEscapeScore(enemyPosition,
                                                 candidate,
                                                 navigationFlowReady,
                                                 in navigationGridState,
                                                 navigationCells,
                                                 out float navigationEscapeScore))
            {
                continue;
            }
            float candidatePlayerDistance = math.distance(new float2(candidate.x, candidate.z), new float2(playerPosition.x, playerPosition.z));
            float distanceGainScore = math.saturate((candidatePlayerDistance - currentPlayerDistance) /
                                                    math.max(0.25f, maximumDistance + searchRadius));
            float openSpaceScore = EvaluateOpenSpaceScore(candidate,
                                                          collisionRadius,
                                                          searchRadius,
                                                          elapsedTime,
                                                          enemyEntity,
                                                          in physicsWorldSingleton,
                                                          wallsLayerMask,
                                                          wallsEnabled);
            float localSeparationScore = EvaluateCandidateSeparationScore(enemyEntity,
                                                                          selfPriorityTier,
                                                                          candidate,
                                                                          bodyRadius,
                                                                          minimumEnemyClearance,
                                                                          clearanceProbeSpeed,
                                                                          in occupancyContext);
            float safetyScore = math.saturate(freeTrajectoryScore * 0.54f +
                                              freeSpaceScore * 0.12f +
                                              openSpaceScore * 0.14f +
                                              localSeparationScore * 0.2f);
            float retreatScore = math.saturate(alignmentToRetreat * 0.5f + distanceGainScore * 0.5f);
            float safetyWeight = math.max(1f, freeTrajectoryPreference * 10f);
            float score = safetyWeight * safetyScore +
                          retreatDirectionPreference * retreatScore +
                          openSpacePreference * openSpaceScore +
                          navigationPreference * navigationEscapeScore +
                          localSeparationScore * 2.1f;
            if (score <= bestScore)
                continue;
            bestScore = score;
            selectedTarget = candidate;
            selectedDirectionAngle = math.degrees(math.atan2(sampleDirection.x, sampleDirection.z));
            foundCandidate = true;
        }
        return foundCandidate;
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// Returns whether the candidate position and its direct segment preserve enough wall clearance.
    /// </summary>
    /// <param name="origin">Current enemy position.</param>
    /// <param name="candidate">Candidate position being tested.</param>
    /// <param name="collisionRadius">Effective collision radius including wall clearance.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether wall checks are enabled.</param>
    /// <returns>True when the candidate is wall-safe enough to consider.</returns>
    private static bool IsCandidateWallSafe(float3 origin,
                                            float3 candidate,
                                            float collisionRadius,
                                            in PhysicsWorldSingleton physicsWorldSingleton,
                                            int wallsLayerMask,
                                            bool wallsEnabled)
    {
        if (!wallsEnabled)
            return true;
        float3 candidateDisplacement = candidate - origin;
        bool blocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                               origin,
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
                return false;
        }
        bool violatesCandidateClearance = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                               candidate,
                                                                                               collisionRadius,
                                                                                               wallsLayerMask,
                                                                                               out float3 _,
                                                                                               out float3 _);
        return !violatesCandidateClearance;
    }

    /// <summary>
    /// Estimates how much navigation distance and topology safety a candidate gains over the current position.
    /// </summary>
    /// <param name="currentPosition">Current enemy position.</param>
    /// <param name="candidate">Candidate position being tested.</param>
    /// <param name="navigationFlowReady">Whether the shared flow field is currently valid.</param>
    /// <param name="navigationGridState">Shared navigation grid state.</param>
    /// <param name="navigationCells">Shared navigation cells buffer.</param>
    /// <param name="navigationEscapeScore">Output normalized navigation gain score.</param>
    /// <returns>True when the candidate has a valid reachable navigation analysis.</returns>
    private static bool TryResolveNavigationEscapeScore(float3 currentPosition,
                                                        float3 candidate,
                                                        bool navigationFlowReady,
                                                        in EnemyNavigationGridState navigationGridState,
                                                        DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                        out float navigationEscapeScore)
    {
        navigationEscapeScore = 0f;

        if (!navigationFlowReady)
            return true;

        if (!EnemyNavigationFlowFieldUtility.TryResolveNavigationCost(currentPosition,
                                                                     in navigationGridState,
                                                                     navigationCells,
                                                                     out int currentCost))
        {
            return false;
        }

        if (!EnemyNavigationFlowFieldUtility.TryResolveNavigationCost(candidate,
                                                                     in navigationGridState,
                                                                     navigationCells,
                                                                     out int candidateCost))
        {
            return false;
        }

        float costGain = math.max(0f, candidateCost - currentCost);
        navigationEscapeScore = math.saturate(costGain / RetreatNavigationGainNormalization);
        return true;
    }

    /// <summary>
    /// Estimates how open a candidate position is by probing short wall-safe displacements around it.
    /// </summary>
    /// <param name="candidate">Candidate position being evaluated.</param>
    /// <param name="collisionRadius">Collision radius used by wall probes.</param>
    /// <param name="searchRadius">Current candidate search radius.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether wall checks are enabled.</param>
    /// <returns>Normalized openness score in the [0..1] range.</returns>
    private static float EvaluateOpenSpaceScore(float3 candidate,
                                                float collisionRadius,
                                                float searchRadius,
                                                float elapsedTime,
                                                Entity enemyEntity,
                                                in PhysicsWorldSingleton physicsWorldSingleton,
                                                int wallsLayerMask,
                                                bool wallsEnabled)
    {
        if (!wallsEnabled)
            return 1f;

        float sampleDistance = math.max(collisionRadius * 1.35f,
                                        math.min(searchRadius * 0.28f, OpenSpaceProbeMaximumDistance));
        float opennessScore = 0f;

        for (int sampleIndex = 0; sampleIndex < OpenSpaceSampleCount; sampleIndex++)
        {
            uint angleSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 23f)));
            float angleRadians = EnemyPatternWandererMovementUtility.ResolveHash01(angleSeed) * math.PI * 2f;
            float3 direction = new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
            float3 probeDisplacement = direction * sampleDistance;
            bool blocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                   candidate,
                                                                                   probeDisplacement,
                                                                                   collisionRadius,
                                                                                   wallsLayerMask,
                                                                                   out float3 allowedDisplacement,
                                                                                   out float3 _);
            float openRatio = 1f;

            if (blocked)
                openRatio = math.saturate(math.length(allowedDisplacement) / math.max(sampleDistance, DirectionEpsilon));

            bool violatesProbeClearance = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                               candidate + direction * math.length(allowedDisplacement),
                                                                                               collisionRadius,
                                                                                               wallsLayerMask,
                                                                                               out float3 _,
                                                                                               out float3 _);

            if (violatesProbeClearance)
                openRatio *= 0.25f;

            opennessScore += openRatio;
        }

        return opennessScore / OpenSpaceSampleCount;
    }

    /// <summary>
    /// Evaluates how much crowding pressure exists around one candidate by sampling the local clearance field.
    /// </summary>
    /// <param name="candidate">Candidate position being evaluated.</param>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy priority tier.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="minimumEnemyClearance">Effective minimum enemy clearance.</param>
    /// <param name="probeSpeed">Speed cap used while sampling the clearance field.</param>
    /// <param name="occupancyContext">Occupancy context used for neighbor lookup.</param>
    /// <returns>Normalized separation score in the [0..1] range.</returns>
    private static float EvaluateCandidateSeparationScore(Entity enemyEntity,
                                                          int selfPriorityTier,
                                                          float3 candidate,
                                                          float bodyRadius,
                                                          float minimumEnemyClearance,
                                                          float probeSpeed,
                                                          in EnemyPatternWandererUtility.OccupancyContext occupancyContext)
    {
        float constrainedProbeSpeed = math.max(CandidateClearanceProbeSpeedMinimum, probeSpeed);
        float3 clearanceVelocity = EnemyPatternWandererUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                             selfPriorityTier,
                                                                                             candidate,
                                                                                             bodyRadius,
                                                                                             minimumEnemyClearance,
                                                                                             constrainedProbeSpeed,
                                                                                             1f,
                                                                                             out float priorityYieldUrgency,
                                                                                             out float _,
                                                                                             in occupancyContext);
        float separationPenalty = math.length(clearanceVelocity) / constrainedProbeSpeed;
        separationPenalty = math.max(separationPenalty, priorityYieldUrgency * 0.9f);
        return 1f - math.saturate(separationPenalty);
    }
    #endregion

    #endregion
}
