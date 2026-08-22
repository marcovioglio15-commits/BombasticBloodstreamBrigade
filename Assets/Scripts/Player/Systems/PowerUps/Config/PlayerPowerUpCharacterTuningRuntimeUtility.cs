using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies Character Tuning formulas to runtime scalable stats and synchronizes dependent progression state.
/// </summary>
public static class PlayerPowerUpCharacterTuningRuntimeUtility
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether the provided Character Tuning entry should be applied permanently on acquisition.
    /// </summary>
    /// <param name="unlockCatalogEntry">Unlock catalog entry inspected for runtime-scoped application rules.</param>
    /// <returns>True when acquisition should apply the formulas immediately; otherwise false.</returns>
    public static bool ShouldApplyOnAcquisition(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry)
    {
        if (unlockCatalogEntry.CharacterTuningFormulaCount <= 0)
            return false;

        if (IsRuntimeScopedCharacterTuning(in unlockCatalogEntry))
            return false;

        return true;
    }

    /// <summary>
    /// Resolves whether the provided Character Tuning entry belongs to an active that must apply formulas only while its runtime state remains active.
    /// </summary>
    /// <param name="unlockCatalogEntry">Unlock catalog entry inspected for temporary active-state application rules.</param>
    /// <returns>True when the entry is runtime-scoped; otherwise false.</returns>
    public static bool IsRuntimeScopedCharacterTuning(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry)
    {
        if (unlockCatalogEntry.CharacterTuningFormulaCount <= 0)
            return false;

        if (unlockCatalogEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive)
            return true;

        if (unlockCatalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Active)
            return false;

        if (unlockCatalogEntry.ActiveSlotConfig.IsDefined == 0)
            return false;

        if (unlockCatalogEntry.ActiveSlotConfig.ToolKind == ActiveToolKind.ChargeShot)
            return true;

        if (unlockCatalogEntry.ActiveSlotConfig.Toggleable != 0)
            return true;

        return IsActiveTriggerScopedCharacterTuning(in unlockCatalogEntry);
    }

    /// <summary>
    /// Resolves whether one active slot applies Character Tuning only during the activation trigger execution.
    /// </summary>
    /// <param name="slotConfig">Active slot configuration inspected for trigger-scoped Character Tuning.</param>
    /// <returns>True when Character Tuning is scoped to a single active trigger; otherwise false.</returns>
    public static bool IsActiveTriggerScopedCharacterTuning(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ApplyCharacterTuningOnActiveTrigger == 0)
            return false;

        if (slotConfig.Toggleable != 0)
            return false;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot ||
            slotConfig.ToolKind == ActiveToolKind.PassiveToggle ||
            slotConfig.ToolKind == ActiveToolKind.Custom)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves whether one catalog entry applies Character Tuning only during the activation trigger execution.
    /// </summary>
    /// <param name="unlockCatalogEntry">Unlock catalog entry inspected for trigger-scoped Character Tuning.</param>
    /// <returns>True when the entry has trigger-scoped formulas; otherwise false.</returns>
    public static bool IsActiveTriggerScopedCharacterTuning(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry)
    {
        if (unlockCatalogEntry.CharacterTuningFormulaCount <= 0)
            return false;

        if (unlockCatalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Active)
            return false;

        return IsActiveTriggerScopedCharacterTuning(in unlockCatalogEntry.ActiveSlotConfig);
    }

    /// <summary>
    /// Applies all Character Tuning formulas referenced by one unlock catalog entry and synchronizes progression state.
    /// </summary>
    /// <param name="unlockCatalogEntry">Catalog entry containing the flattened formula range.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer updated in place.</param>
    /// <param name="progressionConfig">Runtime progression config used to synchronize level requirements.</param>
    /// <param name="playerExperience">Mutable runtime experience component synchronized after formula execution.</param>
    /// <param name="playerLevel">Mutable runtime level component synchronized after formula execution.</param>
    /// <param name="appliedFormulaCount">Number of formulas successfully applied.</param>
    /// <returns>True when at least one formula changed runtime scalable stats; otherwise false.</returns>
    public static bool TryApplyCharacterTuning(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry,
                                               DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                               DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                               PlayerProgressionConfig progressionConfig,
                                               DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                               ref PlayerExperience playerExperience,
                                               ref PlayerLevel playerLevel,
                                               out int appliedFormulaCount)
    {
        if (!TryApplyCharacterTuningFormulas(in unlockCatalogEntry,
                                             characterTuningFormulas,
                                             scalableStats,
                                             out appliedFormulaCount))
        {
            return false;
        }

        SyncProgressionRuntimeState(scalableStats,
                                    progressionConfig,
                                    runtimeGamePhases,
                                    ref playerExperience,
                                    ref playerLevel);
        return true;
    }

    /// <summary>
    /// Applies all Character Tuning formulas referenced by one unlock catalog entry without synchronizing dependent progression state.
    /// </summary>
    /// <param name="unlockCatalogEntry">Catalog entry containing the flattened formula range.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer updated in place.</param>
    /// <param name="appliedFormulaCount">Number of formulas successfully applied.</param>
    /// <returns>True when at least one formula changed runtime scalable stats; otherwise false.</returns>
    public static bool TryApplyCharacterTuningFormulas(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry,
                                                       DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                       DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                       out int appliedFormulaCount)
    {
        return TryApplyCharacterTuningRange(unlockCatalogEntry.CharacterTuningFormulaStartIndex,
                                            unlockCatalogEntry.CharacterTuningFormulaCount,
                                            characterTuningFormulas,
                                            scalableStats,
                                            out appliedFormulaCount);
    }

    /// <summary>
    /// Applies one flattened Character Tuning formula range to a managed scalable-stat collection without synchronizing dependent progression state.
    /// </summary>
    /// <param name="startIndex">Inclusive start index inside the flattened formula buffer.</param>
    /// <param name="formulaCount">Number of formulas to evaluate from startIndex.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Managed scalable-stat list updated in place.</param>
    /// <param name="appliedFormulaCount">Number of formulas successfully applied.</param>
    /// <returns>True when at least one formula changed runtime scalable stats; otherwise false.</returns>
    public static bool TryApplyCharacterTuningRange(int startIndex,
                                                    int formulaCount,
                                                    DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                    List<PlayerScalableStatElement> scalableStats,
                                                    out int appliedFormulaCount)
    {
        return TryApplyCharacterTuningRange(startIndex,
                                            formulaCount,
                                            characterTuningFormulas,
                                            scalableStats,
                                            1f,
                                            out appliedFormulaCount);
    }

    /// <summary>
    /// Applies one flattened Character Tuning formula range to a managed scalable-stat collection using a partial numeric weight.
    /// </summary>
    /// <param name="startIndex">Inclusive start index inside the flattened formula buffer.</param>
    /// <param name="formulaCount">Number of formulas to evaluate from startIndex.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Managed scalable-stat list updated in place.</param>
    /// <param name="applicationWeight">Numeric blend factor from the current value toward the fully evaluated formula result.</param>
    /// <param name="appliedFormulaCount">Number of formulas successfully applied.</param>
    /// <returns>True when at least one formula changed runtime scalable stats; otherwise false.</returns>
    public static bool TryApplyCharacterTuningRange(int startIndex,
                                                    int formulaCount,
                                                    DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                    List<PlayerScalableStatElement> scalableStats,
                                                    float applicationWeight,
                                                    out int appliedFormulaCount)
    {
        appliedFormulaCount = 0;

        if (scalableStats == null || scalableStats.Count <= 0)
            return false;

        if (!characterTuningFormulas.IsCreated || formulaCount <= 0)
            return false;

        int clampedStartIndex = math.max(0, startIndex);
        int endIndex = math.min(characterTuningFormulas.Length, clampedStartIndex + math.max(0, formulaCount));

        if (clampedStartIndex >= endIndex)
            return false;

        float clampedApplicationWeight = math.saturate(applicationWeight);

        if (clampedApplicationWeight <= 0f)
            return false;

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

        for (int formulaIndex = clampedStartIndex; formulaIndex < endIndex; formulaIndex++)
        {
            string formula = characterTuningFormulas[formulaIndex].Formula.ToString();

            if (string.IsNullOrWhiteSpace(formula))
                continue;

            if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formula,
                                                                               out string targetStatName,
                                                                               out string expression,
                                                                               out string _))
            {
                continue;
            }

            int scalableStatIndex = FindScalableStatIndex(scalableStats, targetStatName);

            if (scalableStatIndex < 0)
                continue;

            PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
            PlayerFormulaValue currentValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);

            if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(expression,
                                                                       currentValue,
                                                                       variableContext,
                                                                       out PlayerFormulaValue evaluatedValue,
                                                                       out string _,
                                                                       false))
            {
                continue;
            }

            if (clampedApplicationWeight < 1f)
            {
                if (currentValue.Type != PlayerFormulaValueType.Number || evaluatedValue.Type != PlayerFormulaValueType.Number)
                    continue;

                evaluatedValue = PlayerFormulaValue.CreateNumber(math.lerp(currentValue.NumberValue,
                                                                           evaluatedValue.NumberValue,
                                                                           clampedApplicationWeight));
            }

            if (PlayerFormulaValue.AreEqual(in currentValue, in evaluatedValue))
                continue;

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat, evaluatedValue, out string _))
                continue;

            scalableStats[scalableStatIndex] = scalableStat;
            variableContext[targetStatName] = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
            appliedFormulaCount += 1;
        }

        return appliedFormulaCount > 0;
    }

    /// <summary>
    /// Synchronizes progression runtime components and reserved scalable stats after Character Tuning changes.
    /// </summary>
    /// <param name="scalableStats">Mutable scalable-stat buffer containing the latest values.</param>
    /// <param name="progressionConfig">Runtime progression config used to resolve the current level requirement.</param>
    /// <param name="playerExperience">Mutable runtime experience component.</param>
    /// <param name="playerLevel">Mutable runtime level component.</param>
    public static void SyncProgressionRuntimeState(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                   PlayerProgressionConfig progressionConfig,
                                                   DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                   ref PlayerExperience playerExperience,
                                                   ref PlayerLevel playerLevel)
    {
        float resolvedExperience = math.max(0f, ResolveScalableStatValue(scalableStats, "experience", playerExperience.Current));
        int levelCap = PlayerProgressionPhaseUtility.ResolveLevelCap(progressionConfig);
        int resolvedLevel = math.clamp((int)Math.Round(ResolveScalableStatValue(scalableStats, "level", playerLevel.Current),
                                                      MidpointRounding.AwayFromZero),
                                       0,
                                       levelCap);
        int activeGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig, resolvedLevel);
        float requiredExperienceForNextLevel = 0f;

        if (resolvedLevel < levelCap)
        {
            requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                                             runtimeGamePhases,
                                                                                                             resolvedLevel,
                                                                                                             out activeGamePhaseIndex,
                                                                                                             out bool _,
                                                                                                             out int _);
        }

        playerExperience = new PlayerExperience
        {
            Current = resolvedExperience
        };
        playerLevel = new PlayerLevel
        {
            Current = resolvedLevel,
            ActiveGamePhaseIndex = activeGamePhaseIndex,
            RequiredExperienceForNextLevel = requiredExperienceForNextLevel
        };

        TryWriteReservedStatValue(scalableStats, "experience", resolvedExperience);
        TryWriteReservedStatValue(scalableStats, "level", resolvedLevel);
    }

    /// <summary>
    /// Resolves one Character Tuning assignment target stat name from the raw formula string.
    /// </summary>
    /// <param name="formula">Raw Character Tuning formula string.</param>
    /// <param name="targetStatName">Parsed target scalable-stat name when successful.</param>
    /// <returns>True when the assignment target is valid; otherwise false.</returns>
    public static bool TryResolveTargetStatName(string formula, out string targetStatName)
    {
        targetStatName = string.Empty;

        if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formula,
                                                                           out targetStatName,
                                                                           out string _,
                                                                           out string _))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(targetStatName);
    }

    /// <summary>
    /// Resolves one scalable-stat buffer index by name using case-insensitive lookup semantics.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer to scan.</param>
    /// <param name="statName">Requested scalable-stat identifier.</param>
    /// <returns>Buffer index when found; otherwise -1.</returns>
    public static int FindScalableStatIndex(DynamicBuffer<PlayerScalableStatElement> scalableStats, string statName)
    {
        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (!string.Equals(scalableStat.Name.ToString(), statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return statIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves one scalable-stat list index by name using case-insensitive lookup semantics.
    /// </summary>
    /// <param name="scalableStats">Managed scalable-stat list to scan.</param>
    /// <param name="statName">Requested scalable-stat identifier.</param>
    /// <returns>List index when found; otherwise -1.</returns>
    public static int FindScalableStatIndex(IReadOnlyList<PlayerScalableStatElement> scalableStats, string statName)
    {
        if (scalableStats == null)
            return -1;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (!string.Equals(scalableStat.Name.ToString(), statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return statIndex;
        }

        return -1;
    }

    /// <summary>
    /// Normalizes one evaluated formula result according to the target scalable-stat type.
    /// </summary>
    /// <param name="scalableStat">Target scalable-stat metadata.</param>
    /// <param name="evaluatedValue">Raw evaluated formula result.</param>
    /// <returns>Stored runtime value after type normalization.</returns>
    public static float ResolveStatValue(in PlayerScalableStatElement scalableStat, float evaluatedValue)
    {
        return PlayerScalableStatClampUtility.ResolveNormalizedValue(in scalableStat, evaluatedValue);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one flattened Character Tuning formula range without synchronizing dependent progression state.
    /// </summary>
    /// <param name="startIndex">Inclusive start index inside the flattened formula buffer.</param>
    /// <param name="formulaCount">Number of formulas to evaluate from startIndex.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer updated in place.</param>
    /// <param name="appliedFormulaCount">Number of formulas successfully applied.</param>
    /// <returns>True when at least one formula changed runtime scalable stats; otherwise false.</returns>
    private static bool TryApplyCharacterTuningRange(int startIndex,
                                                     int formulaCount,
                                                     DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                     DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                     out int appliedFormulaCount)
    {
        appliedFormulaCount = 0;

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
            return false;

        if (!characterTuningFormulas.IsCreated || formulaCount <= 0)
            return false;

        int clampedStartIndex = math.max(0, startIndex);
        int endIndex = math.min(characterTuningFormulas.Length, clampedStartIndex + math.max(0, formulaCount));

        if (clampedStartIndex >= endIndex)
            return false;

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

        for (int formulaIndex = clampedStartIndex; formulaIndex < endIndex; formulaIndex++)
        {
            string formula = characterTuningFormulas[formulaIndex].Formula.ToString();

            if (string.IsNullOrWhiteSpace(formula))
                continue;

            if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formula,
                                                                               out string targetStatName,
                                                                               out string expression,
                                                                               out string _))
            {
                continue;
            }

            int scalableStatIndex = FindScalableStatIndex(scalableStats, targetStatName);

            if (scalableStatIndex < 0)
                continue;

            PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
            PlayerFormulaValue currentValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);

            if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(expression,
                                                                       currentValue,
                                                                       variableContext,
                                                                       out PlayerFormulaValue evaluatedValue,
                                                                       out string _,
                                                                       false))
            {
                continue;
            }

            if (PlayerFormulaValue.AreEqual(in currentValue, in evaluatedValue))
                continue;

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat, evaluatedValue, out string _))
                continue;

            scalableStats[scalableStatIndex] = scalableStat;
            variableContext[targetStatName] = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
            appliedFormulaCount += 1;
        }

        return appliedFormulaCount > 0;
    }

    /// <summary>
    /// Resolves one scalable-stat numeric projection or returns a fallback when the stat is not present.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer to scan.</param>
    /// <param name="statName">Requested scalable-stat identifier.</param>
    /// <param name="fallbackValue">Fallback value returned when the stat does not exist.</param>
    /// <returns>Resolved numeric projection or the provided fallback.</returns>
    private static float ResolveScalableStatValue(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                  string statName,
                                                  float fallbackValue)
    {
        int scalableStatIndex = FindScalableStatIndex(scalableStats, statName);

        if (scalableStatIndex < 0)
            return fallbackValue;

        PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
        return PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);
    }

    /// <summary>
    /// Writes one reserved scalable-stat numeric value back to the runtime buffer when the stat exists.
    /// </summary>
    /// <param name="scalableStats">Mutable scalable-stat buffer updated in place.</param>
    /// <param name="statName">Reserved scalable-stat identifier to update.</param>
    /// <param name="value">New runtime value written to the buffer.</param>
    private static void TryWriteReservedStatValue(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                  string statName,
                                                  float value)
    {
        int scalableStatIndex = FindScalableStatIndex(scalableStats, statName);

        if (scalableStatIndex < 0)
            return;

        PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];

        if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat,
                                                                 PlayerFormulaValue.CreateNumber(value),
                                                                 out string _))
        {
            return;
        }

        scalableStats[scalableStatIndex] = scalableStat;
    }
    #endregion

    #endregion
}
