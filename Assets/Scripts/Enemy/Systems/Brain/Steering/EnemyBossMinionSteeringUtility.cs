using Unity.Mathematics;

/// <summary>
/// Provides focused steering helpers that make boss-spawned minions yield quickly to their owning boss.
/// </summary>
internal static class EnemyBossMinionSteeringUtility
{
    #region Constants
    private const float BossClearancePadding = 0.18f;
    private const float BossInfluenceScale = 3.15f;
    private const float BossSpeedInfluenceSeconds = 0.22f;
    private const float PredictionBaseSeconds = 0.22f;
    private const float PredictionSpeedScale = 0.045f;
    private const float PredictionMaxSeconds = 0.72f;
    private const float ForcedEvaluationScale = 3.7f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether a minion should bypass normal LOD cadence because its boss is close enough to require yielding.
    /// </summary>
    /// <param name="minionPosition">Current minion world position.</param>
    /// <param name="bossPosition">Current owning boss world position.</param>
    /// <param name="bodyRadius">Minion body radius.</param>
    /// <param name="separationRadius">Minion authored separation radius.</param>
    /// <param name="steeringAggressiveness">Minion steering aggressiveness.</param>
    /// <returns>True when the minion should evaluate steering this frame.</returns>
    public static bool ShouldForceBossAvoidanceEvaluation(float3 minionPosition,
                                                         float3 bossPosition,
                                                         float bodyRadius,
                                                         float separationRadius,
                                                         float steeringAggressiveness)
    {
        float3 delta = minionPosition - bossPosition;
        delta.y = 0f;
        float safeBodyRadius = math.max(0.05f, bodyRadius);
        float safeSeparationRadius = math.max(0.05f, separationRadius);
        float aggressivenessScale = EnemySteeringUtility.ResolveAggressivenessScale(steeringAggressiveness, 1f, 1.45f);
        float forcedRadius = math.max(safeSeparationRadius, safeBodyRadius * ForcedEvaluationScale) * aggressivenessScale;
        return math.lengthsq(delta) <= forcedRadius * forcedRadius;
    }

    /// <summary>
    /// Resolves a strong avoidance vector and urgency values for one minion yielding to its owning boss.
    /// </summary>
    /// <param name="enemyIndex">Current minion index in steering arrays.</param>
    /// <param name="bossIndex">Owning boss index in steering arrays.</param>
    /// <param name="minionPosition">Current minion world position.</param>
    /// <param name="minionVelocity">Current minion planar velocity.</param>
    /// <param name="minionBodyRadius">Minion body radius.</param>
    /// <param name="minionSeparationRadius">Minion authored separation radius.</param>
    /// <param name="minionSteeringAggressiveness">Minion steering aggressiveness.</param>
    /// <param name="bossPosition">Current boss world position.</param>
    /// <param name="bossVelocity">Current boss planar velocity.</param>
    /// <param name="bossBodyRadius">Boss body radius.</param>
    /// <param name="avoidance">Output avoidance vector to add to normal separation.</param>
    /// <param name="urgency">Output separation urgency.</param>
    /// <param name="yieldUrgency">Output priority-yield urgency.</param>
    /// <returns>True when boss avoidance contributes this frame.</returns>
    public static bool TryResolveBossAvoidance(int enemyIndex,
                                               int bossIndex,
                                               float3 minionPosition,
                                               float3 minionVelocity,
                                               float minionBodyRadius,
                                               float minionSeparationRadius,
                                               float minionSteeringAggressiveness,
                                               float3 bossPosition,
                                               float3 bossVelocity,
                                               float bossBodyRadius,
                                               out float3 avoidance,
                                               out float urgency,
                                               out float yieldUrgency)
    {
        avoidance = float3.zero;
        urgency = 0f;
        yieldUrgency = 0f;

        if (bossIndex < 0 || bossIndex == enemyIndex)
            return false;

        float3 delta = minionPosition - bossPosition;
        delta.y = 0f;
        float distanceSquared = math.lengthsq(delta);
        float safeMinionBodyRadius = math.max(0.05f, minionBodyRadius);
        float safeBossBodyRadius = math.max(0.05f, bossBodyRadius);
        float hardClearance = safeMinionBodyRadius + safeBossBodyRadius + BossClearancePadding;
        float bossSpeed = math.length(bossVelocity);
        float influenceRadius = math.max(math.max(0.05f, minionSeparationRadius) * BossInfluenceScale,
                                         hardClearance * BossInfluenceScale + bossSpeed * BossSpeedInfluenceSeconds);

        if (distanceSquared > influenceRadius * influenceRadius)
            return false;

        float predictionSeconds = math.clamp(PredictionBaseSeconds + bossSpeed * PredictionSpeedScale,
                                             PredictionBaseSeconds,
                                             PredictionMaxSeconds);
        float3 predictedDelta = minionPosition + minionVelocity * predictionSeconds - (bossPosition + bossVelocity * predictionSeconds);
        predictedDelta.y = 0f;
        float predictedDistanceSquared = math.lengthsq(predictedDelta);
        bool usePredictedDelta = predictedDistanceSquared < distanceSquared;
        float3 effectiveDelta = usePredictedDelta ? predictedDelta : delta;
        float effectiveDistanceSquared = usePredictedDelta ? predictedDistanceSquared : distanceSquared;
        float distance = math.sqrt(math.max(effectiveDistanceSquared, 0f));
        float3 awayDirection = distance > EnemySteeringUtility.DirectionEpsilon
            ? effectiveDelta / distance
            : ResolveDeterministicDirection(enemyIndex, bossIndex);
        float spacingPressure = math.saturate((influenceRadius - distance) / math.max(0.01f, influenceRadius));
        float hardPressure = math.saturate((hardClearance - distance) / math.max(0.01f, hardClearance));
        float3 lateralDirection = ResolveLateralDirection(awayDirection, minionVelocity, bossVelocity, enemyIndex, bossIndex);
        float lateralWeight = 0.35f + spacingPressure * 0.85f + hardPressure * 0.95f;
        float3 avoidanceDirection = math.normalizesafe(awayDirection + lateralDirection * lateralWeight, awayDirection);
        float aggressivenessScale = EnemySteeringUtility.ResolveAggressivenessScale(minionSteeringAggressiveness, 1.05f, 1.65f);
        float weight = (1f + spacingPressure * 3.2f + hardPressure * 5.4f) * aggressivenessScale;
        avoidance = avoidanceDirection * weight;
        urgency = math.saturate(spacingPressure * 0.72f + hardPressure * 1.15f);
        yieldUrgency = math.saturate(urgency + hardPressure * 0.35f);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a lateral side-step direction that prefers moving out of the boss travel lane.
    /// </summary>
    /// <param name="awayDirection">Direction from boss to minion.</param>
    /// <param name="minionVelocity">Current minion planar velocity.</param>
    /// <param name="bossVelocity">Current boss planar velocity.</param>
    /// <param name="enemyIndex">Current minion index.</param>
    /// <param name="bossIndex">Owning boss index.</param>
    /// <returns>Normalized lateral avoidance direction.</returns>
    private static float3 ResolveLateralDirection(float3 awayDirection,
                                                  float3 minionVelocity,
                                                  float3 bossVelocity,
                                                  int enemyIndex,
                                                  int bossIndex)
    {
        float3 lateral = new float3(-awayDirection.z, 0f, awayDirection.x);
        lateral = math.normalizesafe(lateral, ResolveDeterministicDirection(enemyIndex, bossIndex));
        float3 bossTravelDirection = math.normalizesafe(new float3(bossVelocity.x, 0f, bossVelocity.z), float3.zero);

        if (math.lengthsq(bossTravelDirection) > EnemySteeringUtility.DirectionEpsilon)
        {
            float alignment = math.dot(bossTravelDirection, lateral);

            if (math.abs(alignment) > 0.08f)
                return alignment >= 0f ? lateral : -lateral;
        }

        float3 relativeVelocity = minionVelocity - bossVelocity;
        float3 relativeDirection = math.normalizesafe(new float3(relativeVelocity.x, 0f, relativeVelocity.z), float3.zero);

        if (math.lengthsq(relativeDirection) > EnemySteeringUtility.DirectionEpsilon)
        {
            float alignment = math.dot(relativeDirection, lateral);

            if (math.abs(alignment) > 0.08f)
                return alignment >= 0f ? lateral : -lateral;
        }

        uint hash = math.hash(new int2(enemyIndex * 31 + 7, bossIndex * 37 + 11));
        return (hash & 1u) == 0u ? lateral : -lateral;
    }

    /// <summary>
    /// Resolves a deterministic planar direction for exact overlaps.
    /// </summary>
    /// <param name="enemyIndex">Current minion index.</param>
    /// <param name="bossIndex">Owning boss index.</param>
    /// <returns>Normalized fallback direction.</returns>
    private static float3 ResolveDeterministicDirection(int enemyIndex, int bossIndex)
    {
        uint hash = math.hash(new int2(enemyIndex * 13 + 5, bossIndex * 17 + 9));
        float angleRadians = (hash & 0x0000FFFFu) / 65535f * math.PI * 2f;
        return new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
    }
    #endregion

    #endregion
}
