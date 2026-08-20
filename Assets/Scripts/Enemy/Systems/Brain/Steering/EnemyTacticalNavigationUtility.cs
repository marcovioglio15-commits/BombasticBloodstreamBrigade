using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Provides Burst tactical candidate scoring for standard enemy movement.
/// </summary>
internal static class EnemyTacticalNavigationUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float NeighborClearancePadding = 0.08f;
    private const float PathCommitSeconds = 0.28f;
    private const float HighLodRadius = 16f;
    private const float MediumLodRadius = 34f;
    #endregion

    #region Jobs
    /// <summary>
    /// Scores tactical movement candidates in parallel and returns one desired velocity per evaluated enemy.
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    internal struct EnemyTacticalCandidateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> EvaluatedEnemyIndices;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float2> SpeedData;
        [ReadOnly] public NativeArray<float> BodyRadii;
        [ReadOnly] public NativeArray<float> SeparationWeights;
        [ReadOnly] public NativeArray<int> PriorityTiers;
        [ReadOnly] public NativeArray<float> SteeringAggressiveness;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<int2> CellCoordinates;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        [ReadOnly] public NativeArray<float3> ApproachResults;
        [ReadOnly] public NativeArray<float3> SeparationResults;
        [ReadOnly] public NativeArray<float> SeparationUrgencyResults;
        [ReadOnly] public NativeArray<float3> NavigationVelocityResults;
        [ReadOnly] public NativeArray<EnemyTacticalNavigationConfig> TacticalConfigs;
        [ReadOnly] public NativeArray<EnemyNavigationRuntimeState> RuntimeStates;
        [ReadOnly] public float3 PlayerPosition;
        [ReadOnly] public float3 PlayerVelocity;
        [ReadOnly] public float DeltaTime;
        public NativeArray<float3> Results;
        public NativeArray<EnemyNavigationRuntimeState> RuntimeResults;

        /// <summary>
        /// Executes tactical candidate scoring for one evaluated enemy.
        /// </summary>
        /// <param name="index">Evaluated enemy index inside the compact job arrays.</param>
        public void Execute(int index)
        {
            int enemyIndex = EvaluatedEnemyIndices[index];
            float3 position = Positions[enemyIndex];
            float2 speedData = SpeedData[enemyIndex];
            float desiredSpeed = ResolveDesiredSpeed(speedData);
            EnemyTacticalNavigationConfig config = TacticalConfigs[enemyIndex];
            EnemyNavigationRuntimeState runtimeState = RuntimeStates[enemyIndex];
            float3 playerPrediction = ResolvePredictedPlayerPosition(position, config);
            float3 toPredictedPlayer = playerPrediction - position;
            toPredictedPlayer.y = 0f;
            float distanceToPredictedPlayer = math.length(toPredictedPlayer);
            float3 targetDirection = distanceToPredictedPlayer > DirectionEpsilon
                ? toPredictedPlayer / distanceToPredictedPlayer
                : float3.zero;
            float3 separationVelocity = ResolveSeparationVelocity(index, enemyIndex, desiredSpeed, config);
            EnemyTacticalCandidateBudget budget = ResolveEffectiveBudget(config.CandidateBudget, position);
            float bestScore = float.NegativeInfinity;
            float3 bestVelocity = float3.zero;
            sbyte bestSideSign = 0;

            UpdateStuckState(ref runtimeState, position, Velocities[enemyIndex], desiredSpeed);

            // Direct approach remains a valid pressure candidate so tactical steering does not become evasive-only.
            TryScoreCandidate(index,
                              enemyIndex,
                              ApproachResults[index],
                              0,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);

            // Flow-field movement is scored as a first-class candidate when the shared navigation system has a useful answer.
            TryScoreCandidate(index,
                              enemyIndex,
                              NavigationVelocityResults[index],
                              0,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);

            // Clearance can win under crowd pressure, but it remains scored against progress to avoid passive drifting.
            TryScoreCandidate(index,
                              enemyIndex,
                              separationVelocity,
                              runtimeState.LastSideSign,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);

            if (budget != EnemyTacticalCandidateBudget.Low)
                ScoreBalancedCandidates(index,
                                        enemyIndex,
                                        desiredSpeed,
                                        targetDirection,
                                        separationVelocity,
                                        config,
                                        in runtimeState,
                                        ref bestScore,
                                        ref bestVelocity,
                                        ref bestSideSign);

            if (budget == EnemyTacticalCandidateBudget.High)
                ScoreHighBudgetCandidates(index,
                                          enemyIndex,
                                          desiredSpeed,
                                          targetDirection,
                                          separationVelocity,
                                          config,
                                          in runtimeState,
                                          ref bestScore,
                                          ref bestVelocity,
                                          ref bestSideSign);

            if (math.lengthsq(bestVelocity) <= DirectionEpsilon)
                bestVelocity = ApproachResults[index];

            UpdateCommittedState(ref runtimeState, position, bestVelocity, bestSideSign);
            Results[index] = bestVelocity;
            RuntimeResults[index] = runtimeState;
        }

        /// <summary>
        /// Scores the balanced side-pass and predictive intercept candidates.
        /// </summary>
        /// <param name="compactIndex">Evaluated enemy index inside compact arrays.</param>
        /// <param name="enemyIndex">Full active-enemy array index.</param>
        /// <param name="desiredSpeed">Desired movement speed.</param>
        /// <param name="targetDirection">Direction toward the predicted player point.</param>
        /// <param name="separationVelocity">Resolved local separation velocity.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <param name="bestScore">Mutable best score.</param>
        /// <param name="bestVelocity">Mutable best velocity.</param>
        /// <param name="bestSideSign">Mutable best side sign.</param>
        private void ScoreBalancedCandidates(int compactIndex,
                                             int enemyIndex,
                                             float desiredSpeed,
                                             float3 targetDirection,
                                             float3 separationVelocity,
                                             in EnemyTacticalNavigationConfig config,
                                             in EnemyNavigationRuntimeState runtimeState,
                                             ref float bestScore,
                                             ref float3 bestVelocity,
                                             ref sbyte bestSideSign)
        {
            if (desiredSpeed <= DirectionEpsilon || math.lengthsq(targetDirection) <= DirectionEpsilon)
                return;

            float3 lateral = new float3(-targetDirection.z, 0f, targetDirection.x);
            TryScoreDirection(compactIndex,
                              enemyIndex,
                              math.normalizesafe(targetDirection + lateral * 0.78f, targetDirection),
                              1,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);
            TryScoreDirection(compactIndex,
                              enemyIndex,
                              math.normalizesafe(targetDirection - lateral * 0.78f, targetDirection),
                              -1,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);

            float3 interceptDirection = math.normalizesafe(targetDirection + math.normalizesafe(PlayerVelocity, float3.zero) * 0.16f, targetDirection);
            TryScoreDirection(compactIndex,
                              enemyIndex,
                              interceptDirection,
                              0,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);
        }

        /// <summary>
        /// Scores extra angular offsets used only for high LOD enemies near the player.
        /// </summary>
        /// <param name="compactIndex">Evaluated enemy index inside compact arrays.</param>
        /// <param name="enemyIndex">Full active-enemy array index.</param>
        /// <param name="desiredSpeed">Desired movement speed.</param>
        /// <param name="targetDirection">Direction toward the predicted player point.</param>
        /// <param name="separationVelocity">Resolved local separation velocity.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <param name="bestScore">Mutable best score.</param>
        /// <param name="bestVelocity">Mutable best velocity.</param>
        /// <param name="bestSideSign">Mutable best side sign.</param>
        private void ScoreHighBudgetCandidates(int compactIndex,
                                               int enemyIndex,
                                               float desiredSpeed,
                                               float3 targetDirection,
                                               float3 separationVelocity,
                                               in EnemyTacticalNavigationConfig config,
                                               in EnemyNavigationRuntimeState runtimeState,
                                               ref float bestScore,
                                               ref float3 bestVelocity,
                                               ref sbyte bestSideSign)
        {
            if (desiredSpeed <= DirectionEpsilon || math.lengthsq(targetDirection) <= DirectionEpsilon)
                return;

            float3 lateral = new float3(-targetDirection.z, 0f, targetDirection.x);
            TryScoreDirection(compactIndex,
                              enemyIndex,
                              math.normalizesafe(targetDirection + lateral * 1.18f, targetDirection),
                              1,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);
            TryScoreDirection(compactIndex,
                              enemyIndex,
                              math.normalizesafe(targetDirection - lateral * 1.18f, targetDirection),
                              -1,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);
        }

        /// <summary>
        /// Converts one direction candidate into velocity and scores it.
        /// </summary>
        /// <param name="compactIndex">Evaluated enemy index inside compact arrays.</param>
        /// <param name="enemyIndex">Full active-enemy array index.</param>
        /// <param name="direction">Normalized candidate direction.</param>
        /// <param name="sideSign">Side-pass sign associated with this candidate.</param>
        /// <param name="desiredSpeed">Desired movement speed.</param>
        /// <param name="targetDirection">Direction toward the predicted player point.</param>
        /// <param name="separationVelocity">Resolved local separation velocity.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <param name="bestScore">Mutable best score.</param>
        /// <param name="bestVelocity">Mutable best velocity.</param>
        /// <param name="bestSideSign">Mutable best side sign.</param>
        private void TryScoreDirection(int compactIndex,
                                       int enemyIndex,
                                       float3 direction,
                                       sbyte sideSign,
                                       float desiredSpeed,
                                       float3 targetDirection,
                                       float3 separationVelocity,
                                       in EnemyTacticalNavigationConfig config,
                                       in EnemyNavigationRuntimeState runtimeState,
                                       ref float bestScore,
                                       ref float3 bestVelocity,
                                       ref sbyte bestSideSign)
        {
            if (math.lengthsq(direction) <= DirectionEpsilon)
                return;

            TryScoreCandidate(compactIndex,
                              enemyIndex,
                              direction * desiredSpeed,
                              sideSign,
                              desiredSpeed,
                              targetDirection,
                              separationVelocity,
                              config,
                              in runtimeState,
                              ref bestScore,
                              ref bestVelocity,
                              ref bestSideSign);
        }

        /// <summary>
        /// Scores one candidate velocity against progress, flow alignment, crowd prediction, and direction memory.
        /// </summary>
        /// <param name="compactIndex">Evaluated enemy index inside compact arrays.</param>
        /// <param name="enemyIndex">Full active-enemy array index.</param>
        /// <param name="candidateVelocity">Candidate velocity to score.</param>
        /// <param name="sideSign">Side-pass sign associated with the candidate.</param>
        /// <param name="desiredSpeed">Desired movement speed.</param>
        /// <param name="targetDirection">Direction toward the predicted player point.</param>
        /// <param name="separationVelocity">Resolved local separation velocity.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <param name="bestScore">Mutable best score.</param>
        /// <param name="bestVelocity">Mutable best velocity.</param>
        /// <param name="bestSideSign">Mutable best side sign.</param>
        private void TryScoreCandidate(int compactIndex,
                                       int enemyIndex,
                                       float3 candidateVelocity,
                                       sbyte sideSign,
                                       float desiredSpeed,
                                       float3 targetDirection,
                                       float3 separationVelocity,
                                       in EnemyTacticalNavigationConfig config,
                                       in EnemyNavigationRuntimeState runtimeState,
                                       ref float bestScore,
                                       ref float3 bestVelocity,
                                       ref sbyte bestSideSign)
        {
            float candidateSpeed = math.length(candidateVelocity);

            if (candidateSpeed <= DirectionEpsilon)
                return;

            float3 candidateDirection = candidateVelocity / candidateSpeed;
            float progressScore = math.max(0f, math.dot(candidateDirection, targetDirection));
            float3 navigationVelocity = NavigationVelocityResults[compactIndex];
            float navigationScore = 0f;
            float navigationDetourStrength = 0f;

            if (math.lengthsq(navigationVelocity) > DirectionEpsilon)
            {
                float3 navigationDirection = math.normalizesafe(navigationVelocity, targetDirection);
                navigationScore = math.max(0f, math.dot(candidateDirection, navigationDirection));
                float navigationTargetAlignment = math.saturate(math.dot(navigationDirection, targetDirection));
                navigationDetourStrength = math.saturate((0.96f - navigationTargetAlignment) / 0.96f);
            }

            float3 separationDirection = math.normalizesafe(separationVelocity, float3.zero);
            float separationScore = math.lengthsq(separationDirection) > DirectionEpsilon
                ? math.max(0f, math.dot(candidateDirection, separationDirection))
                : 0f;
            float oscillationPenalty = ResolveOscillationPenalty(candidateDirection, config, in runtimeState);
            float crowdRisk = ResolveCrowdRisk(enemyIndex, candidateVelocity, config);
            float sideScore = ResolveSideScore(sideSign, config, in runtimeState);
            float stuckBoost = ResolveStuckBoost(candidateDirection, config, in runtimeState);
            float urgency = math.saturate(SeparationUrgencyResults[compactIndex]);
            float progressWeight = math.lerp(1.35f, 0.42f, navigationDetourStrength);
            float navigationWeight = math.saturate(config.NavigationInfluence) + navigationDetourStrength * 1.65f;
            float score = progressScore * progressWeight +
                          navigationScore * navigationWeight +
                          separationScore * math.saturate(config.CrowdLanePreference) * (0.4f + urgency) +
                          sideScore +
                          stuckBoost -
                          oscillationPenalty -
                          crowdRisk;

            if (score <= bestScore)
                return;

            float speed = desiredSpeed > DirectionEpsilon ? math.min(candidateSpeed, desiredSpeed) : candidateSpeed;
            bestScore = score;
            bestVelocity = candidateDirection * speed;
            bestSideSign = sideSign;
        }

        /// <summary>
        /// Resolves nearby predicted collision risk for a candidate velocity.
        /// </summary>
        /// <param name="enemyIndex">Full active-enemy array index.</param>
        /// <param name="candidateVelocity">Candidate velocity being scored.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <returns>Penalty value for predicted crowd conflicts.</returns>
        private float ResolveCrowdRisk(int enemyIndex, float3 candidateVelocity, in EnemyTacticalNavigationConfig config)
        {
            float predictionTime = math.max(0.05f, config.PredictionHorizonSeconds);
            float3 position = Positions[enemyIndex];
            float3 predictedSelfPosition = position + candidateVelocity * predictionTime;
            int2 cell = CellCoordinates[enemyIndex];
            float selfRadius = math.max(0.05f, BodyRadii[enemyIndex]);
            int selfPriorityTier = PriorityTiers[enemyIndex];
            float risk = 0f;

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int key = EnemySpatialHashUtility.EncodeCell(cell.x + offsetX, cell.y + offsetY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int neighborIndex;

                    if (!CellMap.TryGetFirstValue(key, out neighborIndex, out iterator))
                        continue;

                    do
                    {
                        if (neighborIndex == enemyIndex)
                            continue;

                        float3 predictedNeighborPosition = Positions[neighborIndex] + Velocities[neighborIndex] * predictionTime;
                        float3 delta = predictedSelfPosition - predictedNeighborPosition;
                        delta.y = 0f;
                        float distance = math.length(delta);
                        float requiredClearance = selfRadius + math.max(0.05f, BodyRadii[neighborIndex]) + NeighborClearancePadding;
                        float riskGate = requiredClearance * 1.65f;

                        if (distance >= riskGate)
                            continue;

                        float normalizedRisk = math.saturate((riskGate - distance) / math.max(0.01f, riskGate));

                        if (selfPriorityTier < PriorityTiers[neighborIndex])
                            normalizedRisk *= 1.45f;

                        risk += normalizedRisk;
                    }
                    while (CellMap.TryGetNextValue(out neighborIndex, ref iterator));
                }
            }

            return risk * (0.32f + math.saturate(config.CrowdLanePreference) * 0.7f);
        }

        /// <summary>
        /// Resolves the local separation velocity contribution used by candidate scoring.
        /// </summary>
        /// <param name="compactIndex">Evaluated enemy index inside compact arrays.</param>
        /// <param name="enemyIndex">Full active-enemy array index.</param>
        /// <param name="desiredSpeed">Desired movement speed.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <returns>Weighted separation velocity.</returns>
        private float3 ResolveSeparationVelocity(int compactIndex,
                                                 int enemyIndex,
                                                 float desiredSpeed,
                                                 in EnemyTacticalNavigationConfig config)
        {
            float urgency = math.saturate(SeparationUrgencyResults[compactIndex]);
            float aggressiveness = EnemySteeringUtility.ResolveSteeringAggressiveness(SteeringAggressiveness[enemyIndex]);
            float responseScale = EnemySteeringUtility.ResolveAggressivenessScale(aggressiveness, 0.72f, 1.95f);
            float urgencyBoost = math.lerp(1f, EnemySteeringUtility.SeparationUrgencyMaxBoost, urgency);
            float weight = math.max(0f, SeparationWeights[enemyIndex]) * urgencyBoost * responseScale;
            float3 separationVelocity = SeparationResults[compactIndex] * weight * math.max(0.2f, aggressiveness);
            float speed = math.length(separationVelocity);

            if (desiredSpeed > DirectionEpsilon && speed > desiredSpeed)
                separationVelocity *= desiredSpeed / math.max(speed, DirectionEpsilon);

            return separationVelocity;
        }

        /// <summary>
        /// Resolves player prediction using configured horizon and distance gating.
        /// </summary>
        /// <param name="position">Current enemy position.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <returns>Predicted player position used for candidate scoring.</returns>
        private float3 ResolvePredictedPlayerPosition(float3 position, in EnemyTacticalNavigationConfig config)
        {
            float3 toPlayer = PlayerPosition - position;
            toPlayer.y = 0f;
            float distanceToPlayer = math.length(toPlayer);
            float horizon = math.clamp(config.PredictionHorizonSeconds, 0f, 2f);
            float closeRangeScale = math.saturate(distanceToPlayer / 4f);
            return PlayerPosition + PlayerVelocity * horizon * closeRangeScale;
        }

        /// <summary>
        /// Resolves a candidate budget after applying distance-based LOD clamps.
        /// </summary>
        /// <param name="candidateBudget">Authored budget.</param>
        /// <param name="position">Current enemy position.</param>
        /// <returns>Effective budget for this frame.</returns>
        private EnemyTacticalCandidateBudget ResolveEffectiveBudget(EnemyTacticalCandidateBudget candidateBudget, float3 position)
        {
            float3 delta = PlayerPosition - position;
            delta.y = 0f;
            float squaredDistance = math.lengthsq(delta);

            if (squaredDistance > MediumLodRadius * MediumLodRadius)
                return EnemyTacticalCandidateBudget.Low;

            if (squaredDistance > HighLodRadius * HighLodRadius &&
                candidateBudget == EnemyTacticalCandidateBudget.High)
            {
                return EnemyTacticalCandidateBudget.Balanced;
            }

            return candidateBudget;
        }

        /// <summary>
        /// Updates stuck recovery state using actual displacement between tactical evaluations.
        /// </summary>
        /// <param name="runtimeState">Mutable runtime navigation memory.</param>
        /// <param name="position">Current enemy position.</param>
        /// <param name="currentVelocity">Current enemy velocity.</param>
        /// <param name="desiredSpeed">Desired movement speed.</param>
        private void UpdateStuckState(ref EnemyNavigationRuntimeState runtimeState,
                                      float3 position,
                                      float3 currentVelocity,
                                      float desiredSpeed)
        {
            runtimeState.PathCommitTimer = math.max(0f, runtimeState.PathCommitTimer - DeltaTime);

            if (runtimeState.HadValidDirection == 0)
            {
                runtimeState.LastResolvedPosition = position;
                runtimeState.StuckTimer = 0f;
                return;
            }

            float expectedDistance = math.max(math.length(currentVelocity), desiredSpeed) * DeltaTime;
            float3 displacement = position - runtimeState.LastResolvedPosition;
            displacement.y = 0f;
            float movedDistance = math.length(displacement);

            if (expectedDistance > 0.02f && movedDistance < expectedDistance * 0.22f)
                runtimeState.StuckTimer += DeltaTime;
            else
                runtimeState.StuckTimer = math.max(0f, runtimeState.StuckTimer - DeltaTime * 1.5f);

            runtimeState.LastResolvedPosition = position;
        }

        /// <summary>
        /// Writes final movement memory after a candidate has been selected.
        /// </summary>
        /// <param name="runtimeState">Mutable runtime navigation memory.</param>
        /// <param name="position">Current enemy position.</param>
        /// <param name="bestVelocity">Selected desired velocity.</param>
        /// <param name="sideSign">Selected side sign.</param>
        private static void UpdateCommittedState(ref EnemyNavigationRuntimeState runtimeState,
                                                 float3 position,
                                                 float3 bestVelocity,
                                                 sbyte sideSign)
        {
            float3 direction = math.normalizesafe(bestVelocity, float3.zero);

            if (math.lengthsq(direction) <= DirectionEpsilon)
                return;

            runtimeState.LastDesiredDirection = direction;
            runtimeState.HadValidDirection = 1;
            runtimeState.LastResolvedPosition = position;
            runtimeState.PathCommitTimer = math.max(runtimeState.PathCommitTimer, PathCommitSeconds * 0.55f);

            if (sideSign != 0)
            {
                runtimeState.LastSideSign = sideSign;
                runtimeState.PathCommitTimer = math.max(runtimeState.PathCommitTimer, PathCommitSeconds);
            }
        }

        /// <summary>
        /// Resolves penalty for reversing the previous committed direction.
        /// </summary>
        /// <param name="candidateDirection">Candidate normalized direction.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <returns>Oscillation penalty value.</returns>
        private static float ResolveOscillationPenalty(float3 candidateDirection,
                                                       in EnemyTacticalNavigationConfig config,
                                                       in EnemyNavigationRuntimeState runtimeState)
        {
            if (runtimeState.HadValidDirection == 0)
                return 0f;

            float3 lastDirection = math.normalizesafe(runtimeState.LastDesiredDirection, float3.zero);

            if (math.lengthsq(lastDirection) <= DirectionEpsilon)
                return 0f;

            float reversal = math.saturate(-math.dot(candidateDirection, lastDirection));
            return reversal * math.saturate(config.OscillationDamping) * (runtimeState.PathCommitTimer > 0f ? 1.35f : 0.72f);
        }

        /// <summary>
        /// Resolves score bonus for committed side-pass consistency.
        /// </summary>
        /// <param name="sideSign">Candidate side sign.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <returns>Side consistency score.</returns>
        private static float ResolveSideScore(sbyte sideSign,
                                              in EnemyTacticalNavigationConfig config,
                                              in EnemyNavigationRuntimeState runtimeState)
        {
            if (sideSign == 0)
                return 0f;

            float score = math.saturate(config.SidePassPreference) * 0.55f;

            if (runtimeState.PathCommitTimer > 0f && runtimeState.LastSideSign == sideSign)
                score += math.saturate(config.CrowdLanePreference) * 0.42f;

            if (runtimeState.PathCommitTimer > 0f && runtimeState.LastSideSign != 0 && runtimeState.LastSideSign != sideSign)
                score -= math.saturate(config.OscillationDamping) * 0.6f;

            return score;
        }

        /// <summary>
        /// Resolves a temporary bonus for candidates that align with recovery movement after poor displacement.
        /// </summary>
        /// <param name="candidateDirection">Candidate normalized direction.</param>
        /// <param name="config">Tactical navigation config.</param>
        /// <param name="runtimeState">Current navigation memory.</param>
        /// <returns>Stuck recovery score bonus.</returns>
        private static float ResolveStuckBoost(float3 candidateDirection,
                                               in EnemyTacticalNavigationConfig config,
                                               in EnemyNavigationRuntimeState runtimeState)
        {
            if (runtimeState.StuckTimer < math.max(0.05f, config.StuckRecoverySeconds))
                return 0f;

            float3 lastDirection = math.normalizesafe(runtimeState.LastDesiredDirection, float3.zero);

            if (math.lengthsq(lastDirection) <= DirectionEpsilon)
                return math.saturate(config.WallTangentPreference) * 0.35f;

            float3 tangent = new float3(-lastDirection.z, 0f, lastDirection.x);
            float tangentAlignment = math.abs(math.dot(candidateDirection, tangent));
            return tangentAlignment * math.saturate(config.WallTangentPreference) * 1.15f;
        }

        /// <summary>
        /// Resolves desired speed from baked move and max speed pair.
        /// </summary>
        /// <param name="speedData">Move speed in x and max speed in y.</param>
        /// <returns>Desired movement speed.</returns>
        private static float ResolveDesiredSpeed(float2 speedData)
        {
            float moveSpeed = math.max(0f, speedData.x);
            float maxSpeed = math.max(0f, speedData.y);
            return maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
        }
    }
    #endregion
}
