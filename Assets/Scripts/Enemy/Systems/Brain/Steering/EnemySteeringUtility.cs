using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores steering constants, Burst jobs and math helpers shared by the enemy steering system.
/// </summary>
internal static class EnemySteeringUtility
{
    #region Constants
    internal static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    internal static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    internal const float DirectionEpsilon = 1e-6f;
    internal const float RotationSpeedEpsilon = 1e-4f;
    internal const float SeparationClearancePadding = 0.05f;
    internal const float SeparationPredictionBaseSeconds = 0.14f;
    internal const float SeparationPredictionSpeedScale = 0.04f;
    internal const float SeparationPredictionMaxSeconds = 0.55f;
    internal const float PriorityApproachUrgencyWeight = 2.2f;
    internal const float SeparationUrgencyMaxBoost = 3.6f;
    internal const float PriorityYieldMaxSpeedBoost = 0.75f;
    internal const float PriorityYieldMaxAccelerationBoost = 2.25f;
    internal const float PriorityYieldGapNormalization = 6f;
    internal const float PriorityYieldGapSpeedScaleMin = 0.62f;
    internal const float PriorityYieldGapSpeedScaleMax = 1.45f;
    internal const float PriorityYieldGapAccelerationScaleMin = 0.7f;
    internal const float PriorityYieldGapAccelerationScaleMax = 1.7f;
    internal const float DefaultSteeringAggressiveness = 1f;
    internal const float MinimumSteeringAggressiveness = 0f;
    internal const float MaximumSteeringAggressiveness = 2.5f;
    internal const float LookRotationSpeedGateRatio = 0.12f;
    internal const float LookRotationFallbackSpeed = 0.2f;
    internal const float LookRotationMinDegreesPerSecond = 360f;
    internal const float LookRotationMaxDegreesPerSecond = 820f;
    private const float HighLodRadius = 16f;
    private const float MediumLodRadius = 34f;
    private const int MediumLodUpdateInterval = 2;
    private const int LowLodUpdateInterval = 4;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one LOD bucket from player and enemy planar distance.
    /// </summary>
    /// <param name="playerPosition">Current player world position.</param>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <returns>Returns the steering LOD bucket used for cadence gating.</returns>
    internal static SteeringLodLevel EvaluateLod(float3 playerPosition, float3 enemyPosition)
    {
        float3 delta = enemyPosition - playerPosition;
        delta.y = 0f;
        float sqrDistance = math.lengthsq(delta);

        float highSqr = HighLodRadius * HighLodRadius;

        if (sqrDistance <= highSqr)
            return SteeringLodLevel.High;

        float mediumSqr = MediumLodRadius * MediumLodRadius;

        if (sqrDistance <= mediumSqr)
            return SteeringLodLevel.Medium;

        return SteeringLodLevel.Low;
    }

    /// <summary>
    /// Resolves whether one enemy should be evaluated this frame according to its LOD cadence.
    /// </summary>
    /// <param name="lodLevel">Resolved LOD level for the enemy.</param>
    /// <param name="frameCount">Current engine frame count.</param>
    /// <param name="stableIndex">Stable integer used to stagger update cadence.</param>
    /// <returns>Returns true when steering should run this frame.</returns>
    internal static bool ShouldEvaluateLod(SteeringLodLevel lodLevel, int frameCount, int stableIndex)
    {
        if (lodLevel == SteeringLodLevel.High)
            return true;

        int interval = lodLevel == SteeringLodLevel.Medium ? MediumLodUpdateInterval : LowLodUpdateInterval;
        int token = frameCount + math.abs(stableIndex);
        return token % interval == 0;
    }

    /// <summary>
    /// Resolves one steering aggressiveness value with safe defaults and clamps.
    /// </summary>
    /// <param name="rawAggressiveness">Serialized aggressiveness value.</param>
    /// <returns>Returns the runtime-safe aggressiveness value.</returns>
    internal static float ResolveSteeringAggressiveness(float rawAggressiveness)
    {
        if (rawAggressiveness < 0f)
            return MinimumSteeringAggressiveness;

        return math.clamp(rawAggressiveness, MinimumSteeringAggressiveness, MaximumSteeringAggressiveness);
    }

    /// <summary>
    /// Maps steering aggressiveness to one configurable scalar range.
    /// </summary>
    /// <param name="aggressiveness">Resolved aggressiveness value.</param>
    /// <param name="minimumScale">Output scale at minimum aggressiveness.</param>
    /// <param name="maximumScale">Output scale at maximum aggressiveness.</param>
    /// <returns>Returns one interpolated scalar value.</returns>
    internal static float ResolveAggressivenessScale(float aggressiveness, float minimumScale, float maximumScale)
    {
        float normalizedAggressiveness = math.saturate((aggressiveness - MinimumSteeringAggressiveness) /
                                                       math.max(0.0001f, MaximumSteeringAggressiveness - MinimumSteeringAggressiveness));
        return math.lerp(minimumScale, maximumScale, normalizedAggressiveness);
    }

    /// <summary>
    /// Resolves one temporary max-speed boost while yielding to higher-priority neighbors.
    /// </summary>
    /// <param name="yieldUrgency">Yield urgency in the [0..1] range.</param>
    /// <param name="priorityGapNormalized">Normalized priority gap in the [0..1] range.</param>
    /// <param name="aggressiveness">Resolved steering aggressiveness.</param>
    /// <returns>Returns one additive speed ratio.</returns>
    internal static float ResolvePriorityYieldSpeedBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.85f, 1.25f);
        float gapScale = math.lerp(PriorityYieldGapSpeedScaleMin,
                                   PriorityYieldGapSpeedScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxSpeedBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves one temporary acceleration boost while yielding to higher-priority neighbors.
    /// </summary>
    /// <param name="yieldUrgency">Yield urgency in the [0..1] range.</param>
    /// <param name="priorityGapNormalized">Normalized priority gap in the [0..1] range.</param>
    /// <param name="aggressiveness">Resolved steering aggressiveness.</param>
    /// <returns>Returns one additive acceleration ratio.</returns>
    internal static float ResolvePriorityYieldAccelerationBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.9f, 1.35f);
        float gapScale = math.lerp(PriorityYieldGapAccelerationScaleMin,
                                   PriorityYieldGapAccelerationScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxAccelerationBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves one planar speed threshold used to skip noisy look updates.
    /// </summary>
    /// <param name="maxSpeed">Current movement max speed after modifiers.</param>
    /// <returns>Returns the planar speed threshold for look rotation.</returns>
    internal static float ResolveLookSpeedThreshold(float maxSpeed)
    {
        float normalizedMaxSpeed = math.max(0f, maxSpeed);

        if (normalizedMaxSpeed <= DirectionEpsilon)
            return LookRotationFallbackSpeed;

        return math.max(LookRotationFallbackSpeed, normalizedMaxSpeed * LookRotationSpeedGateRatio);
    }

    /// <summary>
    /// Resolves one planar facing direction by prioritizing active shooter aim and falling back to movement direction.
    /// </summary>
    /// <param name="velocity">Current planar velocity candidate.</param>
    /// <param name="velocityMaxSpeed">Current movement max speed used to filter noisy velocity look updates.</param>
    /// <param name="shooterControlState">Current shooter control state that may expose an active aim direction.</param>
    /// <param name="facingDirection">Resolved planar facing direction when available.</param>
    /// <returns>Returns true when a valid facing direction is available.</returns>
    internal static bool TryResolveFacingDirection(float3 velocity,
                                                   float velocityMaxSpeed,
                                                   in EnemyShooterControlState shooterControlState,
                                                   out float3 facingDirection)
    {
        if (shooterControlState.HasAimDirection != 0)
        {
            float3 aimDirection = new float3(shooterControlState.AimDirection.x, 0f, shooterControlState.AimDirection.z);

            if (math.lengthsq(aimDirection) > DirectionEpsilon)
            {
                facingDirection = math.normalizesafe(aimDirection, ForwardAxis);
                return true;
            }
        }

        float3 planarVelocity = new float3(velocity.x, 0f, velocity.z);
        float planarVelocitySquared = math.lengthsq(planarVelocity);

        if (planarVelocitySquared <= DirectionEpsilon)
        {
            facingDirection = float3.zero;
            return false;
        }

        float planarSpeed = math.sqrt(planarVelocitySquared);
        float lookSpeedThreshold = ResolveLookSpeedThreshold(velocityMaxSpeed);

        if (planarSpeed <= lookSpeedThreshold)
        {
            facingDirection = float3.zero;
            return false;
        }

        facingDirection = math.normalizesafe(planarVelocity, ForwardAxis);
        return true;
    }

    /// <summary>
    /// Resolves one smoothed look rotation used by enemy movement and shooter systems.
    /// </summary>
    /// <param name="currentRotation">Current world rotation.</param>
    /// <param name="velocity">Current planar velocity candidate.</param>
    /// <param name="velocityMaxSpeed">Current movement max speed used to filter noisy velocity look updates.</param>
    /// <param name="shooterControlState">Current shooter control state that may expose an active aim direction.</param>
    /// <param name="steeringAggressiveness">Resolved steering aggressiveness used to determine turn speed.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <returns>Returns the updated world rotation.</returns>
    internal static quaternion ResolveDynamicLookRotation(quaternion currentRotation,
                                                          float3 velocity,
                                                          float velocityMaxSpeed,
                                                          in EnemyShooterControlState shooterControlState,
                                                          float steeringAggressiveness,
                                                          float deltaTime)
    {
        float3 facingDirection;

        if (!TryResolveFacingDirection(velocity, velocityMaxSpeed, in shooterControlState, out facingDirection))
            return currentRotation;

        float lookTurnRateDegrees = ResolveAggressivenessScale(steeringAggressiveness,
                                                               LookRotationMinDegreesPerSecond,
                                                               LookRotationMaxDegreesPerSecond);
        float maxRadiansDelta = math.radians(lookTurnRateDegrees) * math.max(0f, deltaTime);
        return RotateTowardsPlanar(currentRotation, facingDirection, maxRadiansDelta);
    }

    /// <summary>
    /// Rotates one current orientation toward one planar forward direction with a bounded angular delta.
    /// </summary>
    /// <param name="currentRotation">Current world rotation.</param>
    /// <param name="targetForward">Target planar forward direction.</param>
    /// <param name="maxRadiansDelta">Maximum radians allowed this frame.</param>
    /// <returns>Returns the smoothed rotation result.</returns>
    internal static quaternion RotateTowardsPlanar(quaternion currentRotation, float3 targetForward, float maxRadiansDelta)
    {
        float normalizedDelta = math.max(0f, maxRadiansDelta);

        if (normalizedDelta <= DirectionEpsilon)
            return currentRotation;

        quaternion targetRotation = quaternion.LookRotationSafe(targetForward, UpAxis);
        float4 currentValue = currentRotation.value;
        float4 targetValue = targetRotation.value;
        float dot = math.clamp(math.dot(currentValue, targetValue), -1f, 1f);
        float absoluteDot = math.abs(dot);
        float angle = math.acos(math.min(1f, absoluteDot)) * 2f;

        if (angle <= normalizedDelta || angle <= DirectionEpsilon)
            return targetRotation;

        float interpolation = math.saturate(normalizedDelta / math.max(angle, DirectionEpsilon));
        quaternion rotated = math.slerp(currentRotation, targetRotation, interpolation);
        return math.normalize(rotated);
    }

    /// <summary>
    /// Resolves one per-frame velocity change rate using acceleration or deceleration depending on the target speed.
    /// </summary>
    /// <param name="currentVelocity">Current planar velocity.</param>
    /// <param name="desiredVelocity">Target planar velocity.</param>
    /// <param name="acceleration">Configured acceleration.</param>
    /// <param name="deceleration">Configured deceleration.</param>
    /// <returns>Returns the velocity delta rate in units per second.</returns>
    internal static float ResolveVelocityChangeRate(float3 currentVelocity,
                                                    float3 desiredVelocity,
                                                    float acceleration,
                                                    float deceleration)
    {
        float currentSpeed = math.length(currentVelocity);
        float desiredSpeed = math.length(desiredVelocity);

        if (desiredSpeed + DirectionEpsilon >= currentSpeed)
            return math.max(0f, acceleration);

        if (deceleration > 0f)
            return deceleration;

        return math.max(0f, acceleration);
    }
    #endregion

    #endregion

    #region Jobs
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    internal struct EnemyApproachJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> EvaluatedEnemyIndices;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float2> SpeedData;
        [ReadOnly] public NativeArray<float> ContactRadii;
        [ReadOnly] public float3 PlayerPosition;
        public NativeArray<float3> Results;

        public void Execute(int index)
        {
            int enemyIndex = EvaluatedEnemyIndices[index];
            float3 position = Positions[enemyIndex];
            float3 toPlayer = PlayerPosition - position;
            toPlayer.y = 0f;

            float sqrDistance = math.lengthsq(toPlayer);

            if (sqrDistance <= 1e-6f)
            {
                Results[index] = float3.zero;
                return;
            }

            float distance = math.sqrt(sqrDistance);
            float contactRadius = math.max(0f, ContactRadii[enemyIndex]);

            if (distance <= contactRadius)
            {
                Results[index] = float3.zero;
                return;
            }

            float3 direction = toPlayer / math.max(distance, 1e-6f);
            float moveSpeed = math.max(0f, SpeedData[enemyIndex].x);
            float maxSpeed = math.max(0f, SpeedData[enemyIndex].y);
            float speed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
            Results[index] = direction * speed;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    internal struct EnemySeparationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> EvaluatedEnemyIndices;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> BodyRadii;
        [ReadOnly] public NativeArray<int> PriorityTiers;
        [ReadOnly] public NativeArray<float> SteeringAggressiveness;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<byte> WandererMovementFlags;
        [ReadOnly] public NativeArray<int> OwnedBossIndices;
        [ReadOnly] public NativeArray<float> SeparationRadii;
        [ReadOnly] public NativeArray<int2> CellCoordinates;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        public NativeArray<float3> Results;
        public NativeArray<float> UrgencyResults;
        public NativeArray<float> YieldUrgencyResults;
        public NativeArray<float> YieldPriorityGapResults;

        public void Execute(int index)
        {
            int enemyIndex = EvaluatedEnemyIndices[index];
            float separationRadius = math.max(0.01f, SeparationRadii[enemyIndex]);
            float bodyRadius = math.max(0.01f, BodyRadii[enemyIndex]);
            int selfPriorityTier = PriorityTiers[enemyIndex];
            float selfSteeringAggressiveness = ResolveSteeringAggressiveness(SteeringAggressiveness[enemyIndex]);
            float3 position = Positions[enemyIndex];
            float3 selfVelocity = Velocities[enemyIndex];
            float selfSpeed = math.length(selfVelocity);
            int2 cell = CellCoordinates[enemyIndex];
            float3 separation = float3.zero;
            float highestUrgency = 0f;
            float highestYieldUrgency = 0f;
            float highestYieldPriorityGap = 0f;

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

                        float3 delta = position - Positions[neighborIndex];
                        delta.y = 0f;
                        float sqrDistance = math.lengthsq(delta);
                        float3 neighborVelocity = Velocities[neighborIndex];
                        float neighborSpeed = math.length(neighborVelocity);
                        float neighborBodyRadius = math.max(0.01f, BodyRadii[neighborIndex]);
                        int neighborPriorityTier = PriorityTiers[neighborIndex];
                        float neighborSteeringAggressiveness = ResolveSteeringAggressiveness(SteeringAggressiveness[neighborIndex]);
                        float pairSteeringAggressiveness = math.max(selfSteeringAggressiveness, neighborSteeringAggressiveness);
                        float pairClearanceScale = ResolveAggressivenessScale(pairSteeringAggressiveness, 0.82f, 1.35f);
                        bool neighborIsWanderer = WandererMovementFlags[neighborIndex] != 0;
                        float priorityClearanceMultiplier = ResolvePriorityClearanceMultiplier(selfPriorityTier, neighborPriorityTier);
                        float hardClearanceDistance = (bodyRadius + neighborBodyRadius + SeparationClearancePadding) * priorityClearanceMultiplier * pairClearanceScale;

                        if (neighborIsWanderer)
                            hardClearanceDistance *= 1.18f;

                        float selfRadiusScale = ResolveAggressivenessScale(selfSteeringAggressiveness, 0.9f, 1.45f);
                        float influenceRadius = math.max(separationRadius * selfRadiusScale, hardClearanceDistance * 1.35f);

                        if (selfPriorityTier < neighborPriorityTier)
                            influenceRadius = math.max(influenceRadius, hardClearanceDistance * 1.65f);

                        if (neighborIsWanderer)
                            influenceRadius = math.max(influenceRadius, hardClearanceDistance * 1.95f);

                        float relativeSpeed = math.length(selfVelocity - neighborVelocity);
                        float predictionSeconds = math.clamp(SeparationPredictionBaseSeconds + relativeSpeed * SeparationPredictionSpeedScale,
                                                             SeparationPredictionBaseSeconds,
                                                             SeparationPredictionMaxSeconds);

                        if (selfPriorityTier < neighborPriorityTier)
                            predictionSeconds = math.min(SeparationPredictionMaxSeconds, predictionSeconds * 1.3f);

                        float3 predictedSelfPosition = position + selfVelocity * predictionSeconds;
                        float3 predictedNeighborPosition = Positions[neighborIndex] + neighborVelocity * predictionSeconds;
                        float3 predictedDelta = predictedSelfPosition - predictedNeighborPosition;
                        predictedDelta.y = 0f;
                        float predictedDistanceSquared = math.lengthsq(predictedDelta);
                        bool usePredictedDelta = predictedDistanceSquared < sqrDistance;
                        float3 effectiveDelta = usePredictedDelta ? predictedDelta : delta;
                        float effectiveDistanceSquared = usePredictedDelta ? predictedDistanceSquared : sqrDistance;
                        float influenceRadiusSquared = influenceRadius * influenceRadius;

                        if (effectiveDistanceSquared > influenceRadiusSquared)
                            continue;

                        float distance = math.sqrt(math.max(effectiveDistanceSquared, 0f));
                        float3 direction = distance > DirectionEpsilon
                            ? effectiveDelta / distance
                            : ResolveDeterministicSeparationDirection(enemyIndex, neighborIndex);

                        float weight;

                        if (distance < hardClearanceDistance)
                        {
                            float penetration = hardClearanceDistance - distance;
                            weight = 1f + penetration / math.max(0.01f, hardClearanceDistance);
                        }
                        else
                        {
                            float softDenominator = math.max(0.01f, influenceRadius - hardClearanceDistance);
                            weight = (influenceRadius - distance) / softDenominator;
                        }

                        float hardSpacingPressure = 1f - math.saturate(distance / math.max(0.01f, hardClearanceDistance));
                        float spacingPressure = math.saturate((influenceRadius - distance) / math.max(0.01f, influenceRadius));
                        weight *= 1f + spacingPressure * 0.18f + hardSpacingPressure * 0.9f;

                        if (selfPriorityTier < neighborPriorityTier)
                        {
                            float3 toNeighborDirection = math.normalizesafe(Positions[neighborIndex] - position, float3.zero);
                            float closingSpeed = math.dot(selfVelocity - neighborVelocity, toNeighborDirection);
                            float closingFactor = 0f;

                            if (closingSpeed > 0f)
                            {
                                float speedNormalization = math.max(0.1f, selfSpeed + neighborSpeed);
                                closingFactor = math.saturate(closingSpeed / speedNormalization);
                                weight *= 1f + closingFactor * PriorityApproachUrgencyWeight;
                            }

                            float priorityGap = math.min(PriorityYieldGapNormalization, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                            float priorityGapNormalized = math.saturate(priorityGap / PriorityYieldGapNormalization);
                            float yieldDistanceGate = math.max(hardClearanceDistance * 1.1f, influenceRadius * 0.92f);
                            float distanceUrgency = math.saturate((yieldDistanceGate - distance) / math.max(0.01f, yieldDistanceGate));
                            float yieldUrgency = math.saturate(distanceUrgency * 0.72f + closingFactor * 0.28f);
                            yieldUrgency *= 1f + priorityGapNormalized * 0.45f;

                            if (neighborIsWanderer)
                                yieldUrgency = math.max(yieldUrgency, math.saturate(distanceUrgency + 0.12f));

                            if (yieldUrgency > highestYieldUrgency)
                                highestYieldUrgency = yieldUrgency;

                            if (priorityGapNormalized > highestYieldPriorityGap)
                                highestYieldPriorityGap = priorityGapNormalized;
                        }

                        float priorityWeight = ResolvePriorityAvoidanceWeight(selfPriorityTier, neighborPriorityTier);

                        if (neighborIsWanderer)
                            priorityWeight *= 1.55f;

                        priorityWeight *= 1f + hardSpacingPressure * 0.22f;

                        float sideStepWeight = spacingPressure;
                        sideStepWeight *= ResolveAggressivenessScale(selfSteeringAggressiveness, 0.35f, 1.1f);
                        sideStepWeight *= 1f + spacingPressure * 0.18f + hardSpacingPressure * 0.45f;

                        if (selfPriorityTier < neighborPriorityTier)
                            sideStepWeight *= 1.25f;

                        float3 lateralDirection = ResolveLateralAvoidanceDirection(direction,
                                                                                   selfVelocity,
                                                                                   neighborVelocity,
                                                                                   enemyIndex,
                                                                                   neighborIndex);

                        float3 avoidanceDirection = math.normalizesafe(direction + lateralDirection * sideStepWeight, direction);
                        separation += avoidanceDirection * math.max(0f, weight) * priorityWeight;

                        float urgencyDistanceGate = math.max(hardClearanceDistance, influenceRadius * 0.8f);
                        float urgency = math.saturate((urgencyDistanceGate - distance) / math.max(0.01f, urgencyDistanceGate));
                        urgency = math.max(urgency, math.saturate(spacingPressure * 0.52f + hardSpacingPressure * 0.8f));

                        if (neighborIsWanderer)
                            urgency = math.max(urgency, math.saturate((influenceRadius - distance) / math.max(0.01f, influenceRadius)));

                        if (selfPriorityTier < neighborPriorityTier)
                        {
                            float priorityGap = math.min(4f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                            urgency *= 1f + priorityGap * 0.35f;
                        }

                        float3 toNeighborDirectionForUrgency = math.normalizesafe(Positions[neighborIndex] - position, float3.zero);
                        float closingSpeedForUrgency = math.dot(selfVelocity - neighborVelocity, toNeighborDirectionForUrgency);

                        if (closingSpeedForUrgency > 0f)
                        {
                            float speedNormalization = math.max(0.1f, selfSpeed + neighborSpeed);
                            float closingFactor = math.saturate(closingSpeedForUrgency / speedNormalization);
                            urgency = math.max(urgency, math.saturate(urgency + closingFactor * (neighborIsWanderer ? 0.85f : 0.55f)));
                        }

                        urgency *= ResolveAggressivenessScale(selfSteeringAggressiveness, 0.85f, 1.2f);

                        if (urgency > highestUrgency)
                            highestUrgency = urgency;
                    }
                    while (CellMap.TryGetNextValue(out neighborIndex, ref iterator));
                }
            }

            int ownedBossIndex = OwnedBossIndices[enemyIndex];

            if (ownedBossIndex >= 0 &&
                ownedBossIndex < Positions.Length &&
                EnemyBossMinionSteeringUtility.TryResolveBossAvoidance(enemyIndex,
                                                                       ownedBossIndex,
                                                                       position,
                                                                       selfVelocity,
                                                                       bodyRadius,
                                                                       separationRadius,
                                                                       selfSteeringAggressiveness,
                                                                       Positions[ownedBossIndex],
                                                                       Velocities[ownedBossIndex],
                                                                       math.max(0.01f, BodyRadii[ownedBossIndex]),
                                                                       out float3 bossAvoidance,
                                                                       out float bossAvoidanceUrgency,
                                                                       out float bossYieldUrgency))
            {
                separation += bossAvoidance;
                highestUrgency = math.max(highestUrgency, bossAvoidanceUrgency);
                highestYieldUrgency = math.max(highestYieldUrgency, bossYieldUrgency);
                highestYieldPriorityGap = math.max(highestYieldPriorityGap, 1f);
            }

            Results[index] = separation;
            UrgencyResults[index] = math.saturate(highestUrgency);
            YieldUrgencyResults[index] = math.saturate(highestYieldUrgency);
            YieldPriorityGapResults[index] = math.saturate(highestYieldPriorityGap);
        }

        private static float3 ResolveDeterministicSeparationDirection(int enemyIndex, int neighborIndex)
        {
            uint hash = math.hash(new int2(enemyIndex * 3 + 17, neighborIndex * 5 + 29));
            float angleRadians = (hash & 0x0000FFFFu) / 65535f * math.PI * 2f;
            return new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
        }

        private static float3 ResolveLateralAvoidanceDirection(float3 awayDirection,
                                                               float3 selfVelocity,
                                                               float3 neighborVelocity,
                                                               int enemyIndex,
                                                               int neighborIndex)
        {
            float3 lateral = new float3(-awayDirection.z, 0f, awayDirection.x);
            float lateralLengthSquared = lateral.x * lateral.x + lateral.z * lateral.z;

            if (lateralLengthSquared <= DirectionEpsilon)
                return ResolveDeterministicSeparationDirection(enemyIndex, neighborIndex);

            float inverseLateralLength = math.rsqrt(lateralLengthSquared);
            float3 normalizedLateral = lateral * inverseLateralLength;
            float3 relativeVelocity = selfVelocity - neighborVelocity;
            float3 normalizedRelativeVelocity = math.normalizesafe(new float3(relativeVelocity.x, 0f, relativeVelocity.z), float3.zero);
            float alignment = math.dot(normalizedRelativeVelocity, normalizedLateral);

            if (math.abs(alignment) > 0.12f)
                return alignment >= 0f ? normalizedLateral : -normalizedLateral;

            uint hash = math.hash(new int2(enemyIndex * 19 + 3, neighborIndex * 23 + 5));

            if ((hash & 1u) == 0u)
                return normalizedLateral;

            return -normalizedLateral;
        }

        private static float ResolvePriorityClearanceMultiplier(int selfPriorityTier, int neighborPriorityTier)
        {
            if (selfPriorityTier < neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                return 1.75f + priorityGap * 0.22f;
            }

            if (selfPriorityTier > neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, selfPriorityTier - neighborPriorityTier));
                return math.max(0.5f, 0.94f - priorityGap * 0.07f);
            }

            return 1.14f;
        }

        private static float ResolvePriorityAvoidanceWeight(int selfPriorityTier, int neighborPriorityTier)
        {
            if (selfPriorityTier < neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                return 3.4f + priorityGap * 1.1f;
            }

            if (selfPriorityTier > neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, selfPriorityTier - neighborPriorityTier));
                return math.max(0.15f, 0.6f - priorityGap * 0.08f);
            }

            return 1.32f;
        }
    }
    #endregion

    #region Nested Types
    internal enum SteeringLodLevel : byte
    {
        High = 0,
        Medium = 1,
        Low = 2
    }
    #endregion
}
