using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves Extra Combo Points kill multipliers from enemy lifetime and damage history tracked in ECS runtime state.
/// </summary>
internal static class EnemyExtraComboPointsRuntimeUtility
{
    #region Constants
    private const float DefaultKillMultiplier = 1f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Marks one enemy runtime state as damaged at its current lifetime timestamp.
    /// </summary>
    /// <param name="runtimeState">Mutable runtime state updated in place.</param>
    public static void MarkEnemyDamaged(ref EnemyRuntimeState runtimeState)
    {
        float currentLifetimeSeconds = math.max(0f, runtimeState.LifetimeSeconds);

        if (runtimeState.HasTakenDamage == 0)
        {
            runtimeState.FirstDamageLifetimeSeconds = currentLifetimeSeconds;
            runtimeState.HasTakenDamage = 1;
        }

        runtimeState.LastDamageLifetimeSeconds = currentLifetimeSeconds;
    }

    /// <summary>
    /// Resolves the final combo-points multiplier granted by the killed enemy.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="dropItemsConfig">Summary drop-items flags baked on the enemy.</param>
    /// <param name="extraComboPointModules">Compiled Extra Combo Points module buffer owned by the enemy.</param>
    /// <param name="extraComboPointConditions">Compiled Extra Combo Points condition buffer owned by the enemy.</param>
    /// <returns>Final non-negative combo-points multiplier granted by the kill.</returns>
    public static float ResolveKillComboPointMultiplier(in EnemyRuntimeState runtimeState,
                                                        in EnemyDropItemsConfig dropItemsConfig,
                                                        DynamicBuffer<EnemyExtraComboPointsModuleElement> extraComboPointModules,
                                                        DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointConditions)
    {
        if (dropItemsConfig.HasExtraComboPoints == 0 || !extraComboPointModules.IsCreated || extraComboPointModules.Length <= 0)
            return DefaultKillMultiplier;

        float resolvedMultiplier = DefaultKillMultiplier;

        for (int moduleIndex = 0; moduleIndex < extraComboPointModules.Length; moduleIndex++)
        {
            float moduleMultiplier = ResolveModuleMultiplier(in runtimeState,
                                                            extraComboPointModules[moduleIndex],
                                                            extraComboPointConditions);
            resolvedMultiplier *= math.max(0f, moduleMultiplier);
        }

        return math.max(0f, resolvedMultiplier);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one module-local multiplier from the provided condition slice.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="module">Compiled Extra Combo Points module currently being evaluated.</param>
    /// <param name="extraComboPointConditions">Shared condition buffer that owns the module slice.</param>
    /// <returns>Final non-negative multiplier produced by the module.</returns>
    private static float ResolveModuleMultiplier(in EnemyRuntimeState runtimeState,
                                                 EnemyExtraComboPointsModuleElement module,
                                                 DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointConditions)
    {
        float resolvedMultiplier = math.max(0f, module.BaseMultiplier);
        int conditionStartIndex = math.max(0, module.ConditionStartIndex);
        int conditionEndIndex = conditionStartIndex + math.max(0, module.ConditionCount);

        if (!extraComboPointConditions.IsCreated)
            return ClampModuleMultiplier(resolvedMultiplier, module.MinimumFinalMultiplier, module.MaximumFinalMultiplier);

        if (conditionEndIndex > extraComboPointConditions.Length)
            conditionEndIndex = extraComboPointConditions.Length;

        switch (module.ConditionCombineMode)
        {
            case EnemyExtraComboPointsConditionCombineMode.HighestMatchingMultiplier:
                return ClampModuleMultiplier(resolvedMultiplier * ResolveHighestMatchingMultiplier(in runtimeState,
                                                                                                   extraComboPointConditions,
                                                                                                   conditionStartIndex,
                                                                                                   conditionEndIndex),
                                             module.MinimumFinalMultiplier,
                                             module.MaximumFinalMultiplier);

            case EnemyExtraComboPointsConditionCombineMode.LowestMatchingMultiplier:
                return ClampModuleMultiplier(resolvedMultiplier * ResolveLowestMatchingMultiplier(in runtimeState,
                                                                                                  extraComboPointConditions,
                                                                                                  conditionStartIndex,
                                                                                                  conditionEndIndex),
                                             module.MinimumFinalMultiplier,
                                             module.MaximumFinalMultiplier);

            default:
                return ClampModuleMultiplier(resolvedMultiplier * ResolveProductOfMatchingMultipliers(in runtimeState,
                                                                                                     extraComboPointConditions,
                                                                                                     conditionStartIndex,
                                                                                                     conditionEndIndex),
                                             module.MinimumFinalMultiplier,
                                             module.MaximumFinalMultiplier);
        }
    }

    /// <summary>
    /// Resolves the product of every matching condition multiplier in one module slice.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="extraComboPointConditions">Shared condition buffer that owns the module slice.</param>
    /// <param name="conditionStartIndex">Inclusive slice start index.</param>
    /// <param name="conditionEndIndex">Exclusive slice end index.</param>
    /// <returns>Product of every matching multiplier, or 1 when none match.</returns>
    private static float ResolveProductOfMatchingMultipliers(in EnemyRuntimeState runtimeState,
                                                             DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointConditions,
                                                             int conditionStartIndex,
                                                             int conditionEndIndex)
    {
        float resolvedMultiplier = 1f;

        for (int conditionIndex = conditionStartIndex; conditionIndex < conditionEndIndex; conditionIndex++)
        {
            EnemyExtraComboPointsConditionElement condition = extraComboPointConditions[conditionIndex];

            if (!TryResolveConditionMultiplier(in runtimeState, condition, out float conditionMultiplier))
                continue;

            resolvedMultiplier *= math.max(0f, conditionMultiplier);
        }

        return resolvedMultiplier;
    }

    /// <summary>
    /// Resolves the highest matching condition multiplier in one module slice.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="extraComboPointConditions">Shared condition buffer that owns the module slice.</param>
    /// <param name="conditionStartIndex">Inclusive slice start index.</param>
    /// <param name="conditionEndIndex">Exclusive slice end index.</param>
    /// <returns>Highest matching multiplier, or 1 when none match.</returns>
    private static float ResolveHighestMatchingMultiplier(in EnemyRuntimeState runtimeState,
                                                          DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointConditions,
                                                          int conditionStartIndex,
                                                          int conditionEndIndex)
    {
        float highestMatchingMultiplier = 1f;
        bool hasMatch = false;

        for (int conditionIndex = conditionStartIndex; conditionIndex < conditionEndIndex; conditionIndex++)
        {
            EnemyExtraComboPointsConditionElement condition = extraComboPointConditions[conditionIndex];

            if (!TryResolveConditionMultiplier(in runtimeState, condition, out float conditionMultiplier))
                continue;

            if (!hasMatch || conditionMultiplier > highestMatchingMultiplier)
                highestMatchingMultiplier = conditionMultiplier;

            hasMatch = true;
        }

        return hasMatch ? highestMatchingMultiplier : 1f;
    }

    /// <summary>
    /// Resolves the lowest matching condition multiplier in one module slice.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="extraComboPointConditions">Shared condition buffer that owns the module slice.</param>
    /// <param name="conditionStartIndex">Inclusive slice start index.</param>
    /// <param name="conditionEndIndex">Exclusive slice end index.</param>
    /// <returns>Lowest matching multiplier, or 1 when none match.</returns>
    private static float ResolveLowestMatchingMultiplier(in EnemyRuntimeState runtimeState,
                                                         DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointConditions,
                                                         int conditionStartIndex,
                                                         int conditionEndIndex)
    {
        float lowestMatchingMultiplier = 1f;
        bool hasMatch = false;

        for (int conditionIndex = conditionStartIndex; conditionIndex < conditionEndIndex; conditionIndex++)
        {
            EnemyExtraComboPointsConditionElement condition = extraComboPointConditions[conditionIndex];

            if (!TryResolveConditionMultiplier(in runtimeState, condition, out float conditionMultiplier))
                continue;

            if (!hasMatch || conditionMultiplier < lowestMatchingMultiplier)
                lowestMatchingMultiplier = conditionMultiplier;

            hasMatch = true;
        }

        return hasMatch ? lowestMatchingMultiplier : 1f;
    }

    /// <summary>
    /// Evaluates whether one condition matches the runtime metric sampled at kill time.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="condition">Compiled condition currently being evaluated.</param>
    /// <param name="resolvedMultiplier">Evaluated multiplier when the condition matches.</param>
    /// <returns>True when the condition matches.</returns>
    private static bool TryResolveConditionMultiplier(in EnemyRuntimeState runtimeState,
                                                      EnemyExtraComboPointsConditionElement condition,
                                                      out float resolvedMultiplier)
    {
        resolvedMultiplier = 1f;

        if (!TryResolveMetricValue(in runtimeState, condition.Metric, out float metricValue))
            return false;

        if (metricValue < condition.MinimumValue)
            return false;

        if (condition.UseMaximumValue != 0 && metricValue > condition.MaximumValue)
            return false;

        float normalizedMetric = ResolveNormalizedMetric(metricValue,
                                                         condition.MinimumValue,
                                                         condition.MaximumValue);
        float normalizedCurveValue = EvaluateNormalizedCurveSamples(condition.NormalizedMultiplierCurveSamples, normalizedMetric);
        resolvedMultiplier = ResolveConditionMultiplierRangeValue(condition.MinimumMultiplier,
                                                                 condition.MaximumMultiplier,
                                                                 normalizedCurveValue);
        return true;
    }

    /// <summary>
    /// Resolves one metric value from the current enemy runtime state.
    /// </summary>
    /// <param name="runtimeState">Enemy lifetime and damage timing state sampled at kill time.</param>
    /// <param name="metric">Requested metric kind.</param>
    /// <param name="metricValue">Resolved metric value when available.</param>
    /// <returns>True when the metric is available for the current enemy state.</returns>
    private static bool TryResolveMetricValue(in EnemyRuntimeState runtimeState,
                                              EnemyExtraComboPointsMetric metric,
                                              out float metricValue)
    {
        metricValue = 0f;

        switch (metric)
        {
            case EnemyExtraComboPointsMetric.TimeSinceFirstDamageSeconds:
                if (runtimeState.HasTakenDamage == 0)
                    return false;

                metricValue = math.max(0f, runtimeState.LifetimeSeconds - runtimeState.FirstDamageLifetimeSeconds);
                return true;

            case EnemyExtraComboPointsMetric.TimeSinceLastDamageSeconds:
                if (runtimeState.HasTakenDamage == 0)
                    return false;

                metricValue = math.max(0f, runtimeState.LifetimeSeconds - runtimeState.LastDamageLifetimeSeconds);
                return true;

            case EnemyExtraComboPointsMetric.DamageWindowSeconds:
                if (runtimeState.HasTakenDamage == 0)
                    return false;

                metricValue = math.max(0f, runtimeState.LastDamageLifetimeSeconds - runtimeState.FirstDamageLifetimeSeconds);
                return true;

            case EnemyExtraComboPointsMetric.SpawnToFirstDamageSeconds:
                if (runtimeState.HasTakenDamage == 0)
                    return false;

                metricValue = math.max(0f, runtimeState.FirstDamageLifetimeSeconds);
                return true;

            default:
                metricValue = math.max(0f, runtimeState.LifetimeSeconds);
                return true;
        }
    }

    /// <summary>
    /// Clamps one module-local multiplier inside the authored minimum and maximum range.
    /// </summary>
    /// <param name="multiplier">Raw module-local multiplier.</param>
    /// <param name="minimumValue">Authored minimum final multiplier.</param>
    /// <param name="maximumValue">Authored maximum final multiplier.</param>
    /// <returns>Clamped non-negative module-local multiplier.</returns>
    private static float ClampModuleMultiplier(float multiplier, float minimumValue, float maximumValue)
    {
        float resolvedMinimumValue = math.max(0f, math.min(minimumValue, maximumValue));
        float resolvedMaximumValue = math.max(resolvedMinimumValue, math.max(0f, math.max(minimumValue, maximumValue)));
        return math.clamp(math.max(0f, multiplier), resolvedMinimumValue, resolvedMaximumValue);
    }

    /// <summary>
    /// Resolves the normalized metric position sampled against the authored condition range.
    /// </summary>
    /// <param name="metricValue">Runtime metric sampled at kill time.</param>
    /// <param name="minimumValue">Authored curve-start metric value.</param>
    /// <param name="maximumValue">Authored curve-end metric value.</param>
    /// <returns>Normalized metric position in the 0..1 range.</returns>
    private static float ResolveNormalizedMetric(float metricValue, float minimumValue, float maximumValue)
    {
        float resolvedRange = maximumValue - minimumValue;

        if (resolvedRange <= 0.0001f)
        {
            return 0f;
        }

        return math.saturate((metricValue - minimumValue) / resolvedRange);
    }

    /// <summary>
    /// Evaluates one baked normalized multiplier curve from uniformly sampled values.
    /// </summary>
    /// <param name="normalizedCurveSamples">Authored response curve sampled during bake.</param>
    /// <param name="normalizedMetric">Normalized metric position in the 0..1 range.</param>
    /// <returns>Saturated normalized curve value.</returns>
    private static float EvaluateNormalizedCurveSamples(FixedList64Bytes<float> normalizedCurveSamples, float normalizedMetric)
    {
        int sampleCount = normalizedCurveSamples.Length;

        if (sampleCount <= 0)
        {
            return math.saturate(1f - normalizedMetric);
        }

        if (sampleCount == 1)
        {
            return math.saturate(normalizedCurveSamples[0]);
        }

        float scaledIndex = math.saturate(normalizedMetric) * (sampleCount - 1);
        int lowerSampleIndex = math.clamp((int)math.floor(scaledIndex), 0, sampleCount - 1);
        int upperSampleIndex = math.min(sampleCount - 1, lowerSampleIndex + 1);
        float interpolationFactor = scaledIndex - lowerSampleIndex;
        float lowerSample = normalizedCurveSamples[lowerSampleIndex];
        float upperSample = normalizedCurveSamples[upperSampleIndex];
        return math.saturate(math.lerp(lowerSample, upperSample, interpolationFactor));
    }

    /// <summary>
    /// Resolves the final condition multiplier by interpolating inside the authored multiplier range.
    /// </summary>
    /// <param name="minimumMultiplier">Multiplier returned when the normalized curve evaluates to 0.</param>
    /// <param name="maximumMultiplier">Multiplier returned when the normalized curve evaluates to 1.</param>
    /// <param name="normalizedCurveValue">Saturated normalized response sampled from the baked curve.</param>
    /// <returns>Non-negative condition multiplier.</returns>
    private static float ResolveConditionMultiplierRangeValue(float minimumMultiplier,
                                                              float maximumMultiplier,
                                                              float normalizedCurveValue)
    {
        float resolvedMinimumMultiplier = math.max(0f, minimumMultiplier);
        float resolvedMaximumMultiplier = math.max(0f, maximumMultiplier);
        return math.lerp(resolvedMinimumMultiplier,
                         resolvedMaximumMultiplier,
                         math.saturate(normalizedCurveValue));
    }
    #endregion

    #endregion
}
