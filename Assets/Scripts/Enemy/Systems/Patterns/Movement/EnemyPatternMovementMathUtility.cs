using Unity.Mathematics;

/// <summary>
/// Centralizes math helpers and tuning scalars used by enemy pattern movement runtime.
/// </summary>
internal static class EnemyPatternMovementMathUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float PriorityYieldMaxSpeedBoost = 0.65f;
    private const float PriorityYieldMaxAccelerationBoost = 1.9f;
    private const float PriorityYieldGapSpeedScaleMin = 0.62f;
    private const float PriorityYieldGapSpeedScaleMax = 1.4f;
    private const float PriorityYieldGapAccelerationScaleMin = 0.72f;
    private const float PriorityYieldGapAccelerationScaleMax = 1.58f;
    private const float ShortRangePriorityAccelerationMultiplier = 2.25f;
    private const float ShortRangeTakeoverAccelerationMultiplier = 3.4f;
    private const float MinimumSteeringAggressiveness = 0f;
    private const float MaximumSteeringAggressiveness = 2.5f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether current movement pattern ignores steering and priority interactions.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <returns>True when the active movement explicitly requests steering and priority bypass.</returns>
    public static bool ShouldIgnoreSteeringAndPriority(in EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
            return true;

        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererDvd)
            return false;

        return patternConfig.DvdIgnoreSteeringAndPriority != 0;
    }

    /// <summary>
    /// Blends clearance with the current desired velocity while preserving stable forward speed.
    /// </summary>
    /// <param name="baseVelocity">Current desired planar velocity before clearance.</param>
    /// <param name="clearanceVelocity">Planar clearance contribution.</param>
    /// <param name="clearanceBlend">Clearance blend scalar.</param>
    /// <param name="minimumForwardSpeedRatio">Minimum retained forward speed ratio in [0..1].</param>
    /// <returns>Blended desired velocity.</returns>
    public static float3 ComposeDesiredVelocityWithClearance(float3 baseVelocity,
                                                            float3 clearanceVelocity,
                                                            float clearanceBlend,
                                                            float minimumForwardSpeedRatio)
    {
        float blend = math.max(0f, clearanceBlend);

        if (blend <= 0f)
            return baseVelocity;

        // Blend the lateral avoidance while keeping enough forward momentum to avoid stalls.
        float3 blendedClearance = clearanceVelocity * blend;
        float baseSpeed = math.length(baseVelocity);

        if (baseSpeed <= DirectionEpsilon)
            return baseVelocity + blendedClearance;

        float3 forwardDirection = baseVelocity / math.max(baseSpeed, DirectionEpsilon);
        float forwardDelta = math.dot(blendedClearance, forwardDirection);
        float minimumForwardSpeed = baseSpeed * math.clamp(minimumForwardSpeedRatio, 0f, 1f);
        float maximumForwardSpeed = baseSpeed * 1.15f;
        float forwardSpeed = math.clamp(baseSpeed + forwardDelta, minimumForwardSpeed, maximumForwardSpeed);
        float3 lateralClearance = blendedClearance - forwardDirection * forwardDelta;
        return forwardDirection * forwardSpeed + lateralClearance;
    }

    /// <summary>
    /// Resolves per-frame velocity change rate using acceleration for speed-up and deceleration for slow-down.
    /// </summary>
    /// <param name="currentVelocity">Current planar velocity.</param>
    /// <param name="desiredVelocity">Target planar velocity.</param>
    /// <param name="acceleration">Configured acceleration.</param>
    /// <param name="deceleration">Configured deceleration.</param>
    /// <returns>Velocity delta rate in units per second.</returns>
    public static float ResolveVelocityChangeRate(float3 currentVelocity,
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

    /// <summary>
    /// Resolves one steering aggressiveness value with safe defaults and clamps.
    /// </summary>
    /// <param name="rawAggressiveness">Serialized aggressiveness value.</param>
    /// <returns>Resolved aggressiveness value ready for runtime use.</returns>
    public static float ResolveSteeringAggressiveness(float rawAggressiveness)
    {
        if (rawAggressiveness < 0f)
            return MinimumSteeringAggressiveness;

        return math.clamp(rawAggressiveness, MinimumSteeringAggressiveness, MaximumSteeringAggressiveness);
    }

    /// <summary>
    /// Maps steering aggressiveness to a configurable scalar range.
    /// </summary>
    /// <param name="aggressiveness">Resolved aggressiveness value.</param>
    /// <param name="minimumScale">Output scale at minimum aggressiveness.</param>
    /// <param name="maximumScale">Output scale at maximum aggressiveness.</param>
    /// <returns>Interpolated scalar in the requested range.</returns>
    public static float ResolveAggressivenessScale(float aggressiveness, float minimumScale, float maximumScale)
    {
        float normalizedAggressiveness = math.saturate((aggressiveness - MinimumSteeringAggressiveness) /
                                                       math.max(0.0001f, MaximumSteeringAggressiveness - MinimumSteeringAggressiveness));
        return math.lerp(minimumScale, maximumScale, normalizedAggressiveness);
    }

    /// <summary>
    /// Resolves temporary max-speed boost applied while yielding to higher-priority neighbors.
    /// </summary>
    /// <param name="yieldUrgency">Yield urgency in [0..1].</param>
    /// <param name="priorityGapNormalized">Normalized priority-tier gap in [0..1].</param>
    /// <param name="aggressiveness">Resolved steering aggressiveness.</param>
    /// <returns>Additional speed ratio in [0..+].</returns>
    public static float ResolvePriorityYieldSpeedBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.85f, 1.22f);
        float gapScale = math.lerp(PriorityYieldGapSpeedScaleMin,
                                   PriorityYieldGapSpeedScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxSpeedBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves temporary acceleration boost applied while yielding to higher-priority neighbors.
    /// </summary>
    /// <param name="yieldUrgency">Yield urgency in [0..1].</param>
    /// <param name="priorityGapNormalized">Normalized priority-tier gap in [0..1].</param>
    /// <param name="aggressiveness">Resolved steering aggressiveness.</param>
    /// <returns>Additional acceleration ratio in [0..+].</returns>
    public static float ResolvePriorityYieldAccelerationBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.9f, 1.3f);
        float gapScale = math.lerp(PriorityYieldGapAccelerationScaleMin,
                                   PriorityYieldGapAccelerationScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxAccelerationBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves the acceleration multiplier used when a short-range interaction is currently driving movement.
    /// </summary>
    /// <param name="shortRangeTakeoverThisFrame">True when the short-range interaction took over on the current frame.</param>
    /// <returns>Acceleration multiplier applied to the pattern movement update.</returns>
    public static float ResolveShortRangePriorityAccelerationMultiplier(bool shortRangeTakeoverThisFrame)
    {
        if (shortRangeTakeoverThisFrame)
            return ShortRangeTakeoverAccelerationMultiplier;

        return ShortRangePriorityAccelerationMultiplier;
    }
    #endregion

    #endregion
}
