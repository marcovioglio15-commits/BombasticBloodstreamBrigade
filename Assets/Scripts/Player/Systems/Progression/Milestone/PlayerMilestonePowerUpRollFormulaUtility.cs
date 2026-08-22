using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves scalable milestone tier percentages and tier-entry weights through the unified runtime formula context.
/// </summary>
internal static class PlayerMilestonePowerUpRollFormulaUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one baked milestone tier percentage against the effective runtime stat context.
    /// </summary>
    /// <param name="tierRoll">Baked tier candidate containing the runtime value, immutable baseline, and optional formula.</param>
    /// <param name="variableContext">Effective typed stat context including temporary and combo-derived bonuses.</param>
    /// <returns>Non-negative tier weight resolved from the formula, or the baked runtime value when no formula can be applied.</returns>
    public static float ResolveTierRollPercentage(ref PlayerMilestoneTierRollBlob tierRoll,
                                                  IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        return ResolveTierRollPercentage(tierRoll.SelectionPercentage,
                                         tierRoll.BaseSelectionPercentage,
                                         tierRoll.ScalingFormula.ToString(),
                                         variableContext);
    }

    /// <summary>
    /// Resolves one milestone tier percentage from explicit values for deterministic validation and non-blob consumers.
    /// </summary>
    /// <param name="selectionPercentage">Pre-scaled baked value used when runtime formula evaluation is unavailable.</param>
    /// <param name="baseSelectionPercentage">Immutable authored value mapped to the formula's reserved this token.</param>
    /// <param name="scalingFormula">Optional unified numeric scaling formula.</param>
    /// <param name="variableContext">Effective typed stat context including temporary and combo-derived bonuses.</param>
    /// <returns>Non-negative tier weight resolved from the formula, or the supplied runtime value when evaluation fails.</returns>
    public static float ResolveTierRollPercentage(float selectionPercentage,
                                                  float baseSelectionPercentage,
                                                  string scalingFormula,
                                                  IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        float fallbackPercentage = math.max(0f, selectionPercentage);

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return fallbackPercentage;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(scalingFormula,
                                                                   baseSelectionPercentage,
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
            return fallbackPercentage;

        return math.max(0f, evaluatedValue);
    }

    /// <summary>
    /// Resolves one power-up entry weight from its baked value and optional Add Scaling metadata.
    /// </summary>
    /// <param name="tierEntries">Flattened tier-entry buffer containing the baked runtime values.</param>
    /// <param name="tierEntryScaling">Optional scaling metadata keyed by flattened tier-entry index.</param>
    /// <param name="tierEntryIndex">Flattened tier-entry index being evaluated.</param>
    /// <param name="variableContext">Effective typed stat context including temporary and combo-derived bonuses.</param>
    /// <returns>Non-negative runtime entry weight, or zero when the requested entry index is invalid.</returns>
    public static float ResolveTierEntryWeight(DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                               DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                               int tierEntryIndex,
                                               IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        if (tierEntryIndex < 0 || tierEntryIndex >= tierEntries.Length)
            return 0f;

        float fallbackWeight = math.max(0f, tierEntries[tierEntryIndex].SelectionWeight);

        if (!TryResolveTierEntryScaling(tierEntryScaling,
                                        tierEntryIndex,
                                        out PlayerPowerUpTierEntryScalingElement scalingEntry))
            return fallbackWeight;

        string scalingFormula = scalingEntry.ScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return fallbackWeight;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(scalingFormula,
                                                                   scalingEntry.BaseSelectionWeight,
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
            return fallbackWeight;

        return math.max(0f, evaluatedValue);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Finds optional Add Scaling metadata for one flattened tier-entry index.
    /// </summary>
    /// <param name="tierEntryScaling">Scaling metadata buffer produced during the power-up catalog bake.</param>
    /// <param name="tierEntryIndex">Flattened tier-entry index whose metadata is requested.</param>
    /// <param name="scalingEntry">Resolved metadata when present.</param>
    /// <returns>True when matching scaling metadata exists; otherwise false.</returns>
    private static bool TryResolveTierEntryScaling(DynamicBuffer<PlayerPowerUpTierEntryScalingElement> tierEntryScaling,
                                                   int tierEntryIndex,
                                                   out PlayerPowerUpTierEntryScalingElement scalingEntry)
    {
        scalingEntry = default;

        if (!tierEntryScaling.IsCreated)
            return false;

        for (int scalingIndex = 0; scalingIndex < tierEntryScaling.Length; scalingIndex++)
        {
            PlayerPowerUpTierEntryScalingElement candidate = tierEntryScaling[scalingIndex];

            if (candidate.TierEntryIndex != tierEntryIndex)
                continue;

            scalingEntry = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
