using Unity.Mathematics;

/// <summary>
/// Evaluates boss pattern extraction triggers with shared OR semantics for top-level patterns and internal module slots.
/// </summary>
internal static class EnemyBossPatternExtractionRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether any enabled top-level extraction trigger is currently satisfied.
    /// </summary>
    /// <param name="extractionConfig">Top-level boss extraction settings.</param>
    /// <param name="runtimeState">Current boss extraction runtime state.</param>
    /// <param name="health">Current boss health state.</param>
    /// <returns>True when one enabled trigger is satisfied.</returns>
    public static bool IsAnyPatternExtractionTriggerSatisfied(in EnemyBossPatternExtractionConfig extractionConfig,
                                                              in EnemyBossPatternRuntimeState runtimeState,
                                                              in EnemyHealth health)
    {
        // Evaluate authored triggers as independent alternatives after the caller-applied cooldown gate.
        if (extractionConfig.UseElapsedIntervalExtraction != 0 &&
            extractionConfig.ElapsedIntervalSeconds > 0f &&
            runtimeState.ExtractionElapsedSeconds >= extractionConfig.ElapsedIntervalSeconds)
        {
            return true;
        }

        if (extractionConfig.UseMissingHealthStepExtraction != 0 &&
            extractionConfig.MissingHealthStepPercent > 0f &&
            EnemyBossPatternSelectionRuntimeUtility.ResolveMissingHealthPercent(in health) >= runtimeState.LastExtractionMissingHealthPercent + extractionConfig.MissingHealthStepPercent)
        {
            return true;
        }

        if (extractionConfig.UseTravelledDistanceExtraction != 0 &&
            extractionConfig.TravelledDistanceSinceLastExtraction > 0f &&
            runtimeState.DistanceSinceLastExtraction >= extractionConfig.TravelledDistanceSinceLastExtraction)
        {
            return true;
        }

        if (extractionConfig.PlayerDistanceCondition != EnemyBossPatternPlayerDistanceCondition.Disabled &&
            extractionConfig.PlayerDistanceHoldSeconds > 0f &&
            runtimeState.PlayerDistanceHoldSeconds >= extractionConfig.PlayerDistanceHoldSeconds)
        {
            return true;
        }

        // Damage-window extraction is the final OR branch because it has no extra hold timer to update here.
        return extractionConfig.UseDamageWindowExtraction != 0 &&
               extractionConfig.DamageThreshold > 0f &&
               runtimeState.DamageWindowAccumulated >= extractionConfig.DamageThreshold;
    }

    /// <summary>
    /// Checks whether any enabled internal module extraction trigger is currently satisfied.
    /// </summary>
    /// <param name="extraction">Internal module extraction settings.</param>
    /// <param name="slotRuntime">Current runtime state for the slot being evaluated.</param>
    /// <param name="health">Current boss health state.</param>
    /// <returns>True when one enabled trigger is satisfied.</returns>
    public static bool IsAnyModuleExtractionTriggerSatisfied(in EnemyBossPatternModuleExtractionElement extraction,
                                                             in EnemyBossPatternSlotRuntimeElement slotRuntime,
                                                             in EnemyHealth health)
    {
        // Module slots share the same OR trigger model as top-level pattern extraction.
        if (extraction.UseElapsedIntervalExtraction != 0 &&
            extraction.ElapsedIntervalSeconds > 0f &&
            slotRuntime.ExtractionElapsedSeconds >= extraction.ElapsedIntervalSeconds)
        {
            return true;
        }

        if (extraction.UseMissingHealthStepExtraction != 0 &&
            extraction.MissingHealthStepPercent > 0f &&
            EnemyBossPatternSelectionRuntimeUtility.ResolveMissingHealthPercent(in health) >= slotRuntime.LastExtractionMissingHealthPercent + extraction.MissingHealthStepPercent)
        {
            return true;
        }

        if (extraction.UseTravelledDistanceExtraction != 0 &&
            extraction.TravelledDistanceSinceLastExtraction > 0f &&
            slotRuntime.DistanceSinceLastExtraction >= extraction.TravelledDistanceSinceLastExtraction)
        {
            return true;
        }

        if (extraction.PlayerDistanceCondition != EnemyBossPatternPlayerDistanceCondition.Disabled &&
            extraction.PlayerDistanceHoldSeconds > 0f &&
            slotRuntime.PlayerDistanceHoldSeconds >= extraction.PlayerDistanceHoldSeconds)
        {
            return true;
        }

        // Damage-window extraction is checked last to keep the simple timer/metric branches grouped above.
        return extraction.UseDamageWindowExtraction != 0 &&
               extraction.DamageThreshold > 0f &&
               slotRuntime.DamageWindowAccumulated >= extraction.DamageThreshold;
    }
    #endregion

    #endregion
}
