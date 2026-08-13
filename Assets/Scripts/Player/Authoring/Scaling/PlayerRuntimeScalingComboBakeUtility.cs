using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds combo-counter baselines, runtime data, and Add Scaling metadata used by progression baking.
/// </summary>
internal static class PlayerRuntimeScalingComboBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Populates combo base/runtime configs, combo ranks, and flattened rank-bonus formulas from progression presets.
    /// </summary>
    /// <param name="scaledPreset">Scaled progression preset currently used by bake.</param>
    /// <param name="sourcePreset">Unscaled progression preset used as immutable baseline.</param>
    /// <param name="baseRanks">Destination immutable combo-rank buffer.</param>
    /// <param name="runtimeRanks">Destination runtime combo-rank buffer initialized from the scaled preset.</param>
    /// <param name="basePassiveUnlocks">Destination immutable combo passive-unlock buffer.</param>
    /// <param name="runtimePassiveUnlocks">Destination runtime combo passive-unlock buffer initialized from the scaled preset.</param>
    /// <param name="characterTuningFormulaBuffer">Shared flattened Character Tuning formula buffer appended with combo rank bonuses.</param>
    /// <param name="baseConfig">Resolved immutable combo runtime config.</param>
    /// <param name="runtimeConfig">Resolved scaled combo runtime config.</param>
    public static void PopulateComboCounterRuntimeData(PlayerProgressionPreset scaledPreset,
                                                       PlayerProgressionPreset sourcePreset,
                                                       DynamicBuffer<PlayerBaseComboRankElement> baseRanks,
                                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                                       DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                                       DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                                       DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer,
                                                       out PlayerBaseComboCounterConfig baseConfig,
                                                       out PlayerRuntimeComboCounterConfig runtimeConfig)
    {
        baseRanks.Clear();
        runtimeRanks.Clear();
        basePassiveUnlocks.Clear();
        runtimePassiveUnlocks.Clear();

        PlayerComboCounterDefinition sourceCombo = sourcePreset != null ? sourcePreset.ComboCounter : null;
        PlayerComboCounterDefinition scaledCombo = scaledPreset != null ? scaledPreset.ComboCounter : null;
        PlayerBaseComboCounterConfig resolvedBaseConfig = BuildComboConfig(sourceCombo);
        PlayerBaseComboCounterConfig resolvedRuntimeSourceConfig = BuildComboConfig(scaledCombo != null ? scaledCombo : sourceCombo);
        baseConfig = resolvedBaseConfig;
        runtimeConfig = new PlayerRuntimeComboCounterConfig
        {
            Enabled = resolvedRuntimeSourceConfig.Enabled,
            Mode = resolvedRuntimeSourceConfig.Mode,
            ComboGainPerKill = resolvedRuntimeSourceConfig.ComboGainPerKill,
            DamageBreakMode = resolvedRuntimeSourceConfig.DamageBreakMode,
            ShieldDamageBreaksCombo = resolvedRuntimeSourceConfig.ShieldDamageBreaksCombo,
            PreventDecayIntoNonDecayingRanks = resolvedRuntimeSourceConfig.PreventDecayIntoNonDecayingRanks,
            SingleRankId = resolvedRuntimeSourceConfig.SingleRankId,
            SingleRankMaximumComboValue = resolvedRuntimeSourceConfig.SingleRankMaximumComboValue,
            SingleRankPointsDecayPerSecond = resolvedRuntimeSourceConfig.SingleRankPointsDecayPerSecond,
            SingleRankValueDisplayMode = resolvedRuntimeSourceConfig.SingleRankValueDisplayMode,
            SingleRankFormulaDistributionMode = resolvedRuntimeSourceConfig.SingleRankFormulaDistributionMode,
            SingleRankLinearBonusRangeMode = resolvedRuntimeSourceConfig.SingleRankLinearBonusRangeMode,
            SingleRankShowMeterOnlyAfterFirstMilestone = resolvedRuntimeSourceConfig.SingleRankShowMeterOnlyAfterFirstMilestone,
            SingleRankStartLinearBonusesAtFirstMilestone = resolvedRuntimeSourceConfig.SingleRankStartLinearBonusesAtFirstMilestone
        };

        IReadOnlyList<PlayerComboRankDefinition> sourceRanks = sourceCombo != null ? sourceCombo.RankDefinitions : null;
        IReadOnlyList<PlayerComboRankDefinition> scaledRanks = scaledCombo != null ? scaledCombo.RankDefinitions : null;
        int sourceRankCount = sourceRanks != null ? sourceRanks.Count : 0;
        int scaledRankCount = scaledRanks != null ? scaledRanks.Count : 0;
        int rankCount = math.max(sourceRankCount, scaledRankCount);

        for (int rankIndex = 0; rankIndex < rankCount; rankIndex++)
        {
            PlayerComboRankDefinition sourceRank = sourceRanks != null && rankIndex < sourceRankCount ? sourceRanks[rankIndex] : null;
            PlayerComboRankDefinition scaledRank = scaledRanks != null && rankIndex < scaledRankCount ? scaledRanks[rankIndex] : null;
            PlayerComboRankDefinition formulaSourceRank = sourceRank != null ? sourceRank : scaledRank;
            string rankId = ResolveRankId(rankIndex, sourceRank, scaledRank);
            int requiredBaseValue = sourceRank != null
                ? sourceRank.RequiredComboValue
                : scaledRank != null ? scaledRank.RequiredComboValue : 0;
            int requiredRuntimeValue = scaledRank != null
                ? scaledRank.RequiredComboValue
                : sourceRank != null ? sourceRank.RequiredComboValue : 0;
            float pointsDecayPerSecondBaseValue = sourceRank != null
                ? sourceRank.PointsDecayPerSecond
                : scaledRank != null ? scaledRank.PointsDecayPerSecond : 0f;
            float pointsDecayPerSecondRuntimeValue = scaledRank != null
                ? scaledRank.PointsDecayPerSecond
                : sourceRank != null ? sourceRank.PointsDecayPerSecond : 0f;
            float progressiveBoostPercentBaseValue = sourceRank != null
                ? sourceRank.ProgressiveBoostPercent
                : scaledRank != null ? scaledRank.ProgressiveBoostPercent : 0f;
            float progressiveBoostPercentRuntimeValue = scaledRank != null
                ? scaledRank.ProgressiveBoostPercent
                : sourceRank != null ? sourceRank.ProgressiveBoostPercent : 0f;
            int formulaStartIndex = characterTuningFormulaBuffer.Length;
            int formulaCount = AppendBonusFormulas(formulaSourceRank != null ? formulaSourceRank.RankBonuses : null,
                                                   characterTuningFormulaBuffer);
            int passiveUnlockStartIndex = basePassiveUnlocks.Length;
            int passiveUnlockCount = AppendPassiveUnlocks(sourceRank != null ? sourceRank.PassivePowerUpUnlocks : null,
                                                           scaledRank != null ? scaledRank.PassivePowerUpUnlocks : null,
                                                           basePassiveUnlocks,
                                                           runtimePassiveUnlocks);

            baseRanks.Add(new PlayerBaseComboRankElement
            {
                Mode = PlayerComboCounterMode.Ranks,
                RankId = new FixedString64Bytes(rankId),
                Enabled = 1,
                RequiredComboValue = requiredBaseValue,
                RequiredProgressPercent = 0f,
                PointsDecayPerSecond = pointsDecayPerSecondBaseValue,
                ProgressiveBoostPercent = progressiveBoostPercentBaseValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
            runtimeRanks.Add(new PlayerRuntimeComboRankElement
            {
                Mode = PlayerComboCounterMode.Ranks,
                RankId = new FixedString64Bytes(rankId),
                Enabled = 1,
                RequiredComboValue = requiredRuntimeValue,
                RequiredProgressPercent = 0f,
                PointsDecayPerSecond = pointsDecayPerSecondRuntimeValue,
                ProgressiveBoostPercent = progressiveBoostPercentRuntimeValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
        }

        AppendSingleRankMilestones(sourceCombo != null ? sourceCombo.SingleRankProgression : null,
                                   scaledCombo != null ? scaledCombo.SingleRankProgression : null,
                                   in resolvedBaseConfig,
                                   in resolvedRuntimeSourceConfig,
                                   baseRanks,
                                   runtimeRanks,
                                   basePassiveUnlocks,
                                   runtimePassiveUnlocks,
                                   characterTuningFormulaBuffer);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates combo Add Scaling metadata from the unscaled progression preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled progression preset inspected for enabled Add Scaling rules.</param>
    /// <param name="scalingBuffer">Destination combo scaling metadata buffer.</param>
    public static void PopulateComboCounterScalingMetadata(PlayerProgressionPreset sourcePreset,
                                                           DynamicBuffer<PlayerRuntimeComboCounterScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
        {
            return;
        }

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
            {
                continue;
            }

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
            {
                continue;
            }

            if (!PlayerRuntimeScalingComboFieldMappingUtility.TryMapFieldId(property.propertyPath,
                                                                            out PlayerComboCounterMode entryMode,
                                                                            out int rankIndex,
                                                                            out int passiveUnlockIndex,
                                                                            out PlayerRuntimeComboCounterFieldId fieldId))
            {
                continue;
            }

            if (!PlayerRuntimeScalingComboFieldMappingUtility.TryResolveBaseMetadata(property,
                                                                                      out byte valueType,
                                                                                      out float baseValue,
                                                                                      out byte baseBooleanValue,
                                                                                      out byte isInteger,
                                                                                      out FixedString64Bytes baseTokenValue))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeComboCounterScalingElement
            {
                FieldId = fieldId,
                EntryMode = entryMode,
                RankIndex = rankIndex,
                PassiveUnlockIndex = passiveUnlockIndex,
                ValueType = valueType,
                BaseValue = baseValue,
                BaseBooleanValue = baseBooleanValue,
                IsInteger = isInteger,
                BaseTokenValue = baseTokenValue,
                Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                       property,
                                                                                                       null))
            });
        }
    }
#endif
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts one authored combo definition into the runtime config struct used by base/runtime buffers.
    /// </summary>
    /// <param name="comboDefinition">Authored combo definition inspected for runtime values.</param>
    /// <returns>Resolved combo runtime config.</returns>
    private static PlayerBaseComboCounterConfig BuildComboConfig(PlayerComboCounterDefinition comboDefinition)
    {
        return new PlayerBaseComboCounterConfig
        {
            Enabled = comboDefinition != null && comboDefinition.IsEnabled ? (byte)1 : (byte)0,
            Mode = comboDefinition != null ? comboDefinition.Mode : PlayerComboCounterMode.Ranks,
            ComboGainPerKill = comboDefinition != null ? comboDefinition.ComboGainPerKill : 0,
            DamageBreakMode = comboDefinition != null ? comboDefinition.DamageBreakMode : PlayerComboDamageBreakMode.ResetCombo,
            ShieldDamageBreaksCombo = comboDefinition != null && comboDefinition.ShieldDamageBreaksCombo ? (byte)1 : (byte)0,
            PreventDecayIntoNonDecayingRanks = comboDefinition != null && comboDefinition.PreventDecayIntoNonDecayingRanks ? (byte)1 : (byte)0,
            SingleRankId = new FixedString64Bytes(ResolveSingleRankId(comboDefinition != null ? comboDefinition.SingleRankProgression : null)),
            SingleRankMaximumComboValue = comboDefinition != null && comboDefinition.SingleRankProgression != null
                ? comboDefinition.SingleRankProgression.MaximumComboValue
                : 0,
            SingleRankPointsDecayPerSecond = comboDefinition != null && comboDefinition.SingleRankProgression != null
                ? comboDefinition.SingleRankProgression.PointsDecayPerSecond
                : 0f,
            SingleRankValueDisplayMode = comboDefinition != null && comboDefinition.SingleRankProgression != null
                ? comboDefinition.SingleRankProgression.ValueDisplayMode
                : PlayerComboSingleRankValueDisplayMode.CurrentValue,
            SingleRankFormulaDistributionMode = comboDefinition != null && comboDefinition.SingleRankProgression != null
                ? comboDefinition.SingleRankProgression.FormulaDistributionMode
                : PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps,
            SingleRankLinearBonusRangeMode = comboDefinition != null && comboDefinition.SingleRankProgression != null
                ? comboDefinition.SingleRankProgression.LinearBonusRangeMode
                : PlayerComboSingleRankLinearBonusRangeMode.EntireProgression,
            SingleRankShowMeterOnlyAfterFirstMilestone = comboDefinition != null &&
                                                         comboDefinition.SingleRankProgression != null &&
                                                         comboDefinition.SingleRankProgression.ShowMeterOnlyAfterFirstMilestone
                ? (byte)1
                : (byte)0,
            SingleRankStartLinearBonusesAtFirstMilestone = comboDefinition != null &&
                                                           comboDefinition.SingleRankProgression != null &&
                                                           comboDefinition.SingleRankProgression.StartLinearBonusesAtFirstMilestone
                ? (byte)1
                : (byte)0
        };
    }

    /// <summary>
    /// Resolves the authored single-rank identifier with a stable presentation fallback.
    /// </summary>
    /// <param name="singleRankDefinition">Authored single-rank progression settings.</param>
    /// <returns>Trimmed rank identifier or SYNCHRO when no identifier is available.</returns>
    private static string ResolveSingleRankId(PlayerComboSingleRankDefinition singleRankDefinition)
    {
        if (singleRankDefinition == null || string.IsNullOrWhiteSpace(singleRankDefinition.RankId))
            return "SYNCHRO";

        return singleRankDefinition.RankId.Trim();
    }

    /// <summary>
    /// Resolves the stable runtime rank identifier without mutating authoring data.
    /// </summary>
    /// <param name="rankIndex">Zero-based authored rank index.</param>
    /// <param name="sourceRank">Rank entry taken from the unscaled preset when available.</param>
    /// <param name="scaledRank">Rank entry taken from the scaled preset when available.</param>
    /// <returns>Stable runtime rank identifier used for presentation and Add Scaling keys.</returns>
    private static string ResolveRankId(int rankIndex,
                                        PlayerComboRankDefinition sourceRank,
                                        PlayerComboRankDefinition scaledRank)
    {
        string resolvedRankId = sourceRank != null && !string.IsNullOrWhiteSpace(sourceRank.RankId)
            ? sourceRank.RankId.Trim()
            : scaledRank != null && !string.IsNullOrWhiteSpace(scaledRank.RankId)
                ? scaledRank.RankId.Trim()
                : string.Empty;

        if (!string.IsNullOrWhiteSpace(resolvedRankId))
        {
            return resolvedRankId;
        }

        return string.Format("Rank{0:00}", rankIndex + 1);
    }

    /// <summary>
    /// Appends both source and scaled single-rank reward milestones so runtime formula scaling can switch topology without rebaking.
    /// </summary>
    /// <param name="sourceDefinition">Unscaled single-rank definition used for immutable baselines.</param>
    /// <param name="scaledDefinition">Scaled single-rank definition used for initial runtime values.</param>
    /// <param name="baseConfig">Immutable combo config containing the authored progression maximum.</param>
    /// <param name="runtimeConfig">Initial runtime combo config containing the scaled progression maximum.</param>
    /// <param name="baseRanks">Destination immutable reward-entry buffer.</param>
    /// <param name="runtimeRanks">Destination mutable reward-entry buffer.</param>
    /// <param name="basePassiveUnlocks">Destination immutable passive-unlock buffer.</param>
    /// <param name="runtimePassiveUnlocks">Destination mutable passive-unlock buffer.</param>
    /// <param name="characterTuningFormulaBuffer">Shared flattened Character Tuning formula buffer.</param>
    private static void AppendSingleRankMilestones(PlayerComboSingleRankDefinition sourceDefinition,
                                                   PlayerComboSingleRankDefinition scaledDefinition,
                                                   in PlayerBaseComboCounterConfig baseConfig,
                                                   in PlayerBaseComboCounterConfig runtimeConfig,
                                                   DynamicBuffer<PlayerBaseComboRankElement> baseRanks,
                                                   DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                                   DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                                   DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                                   DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer)
    {
        IReadOnlyList<PlayerComboBonusMilestoneDefinition> sourceMilestones = sourceDefinition != null ? sourceDefinition.BonusMilestones : null;
        IReadOnlyList<PlayerComboBonusMilestoneDefinition> scaledMilestones = scaledDefinition != null ? scaledDefinition.BonusMilestones : null;
        int sourceMilestoneCount = sourceMilestones != null ? sourceMilestones.Count : 0;
        int scaledMilestoneCount = scaledMilestones != null ? scaledMilestones.Count : 0;
        int milestoneCount = math.max(sourceMilestoneCount, scaledMilestoneCount);

        for (int milestoneIndex = 0; milestoneIndex < milestoneCount; milestoneIndex++)
        {
            PlayerComboBonusMilestoneDefinition sourceMilestone = sourceMilestones != null && milestoneIndex < sourceMilestoneCount
                ? sourceMilestones[milestoneIndex]
                : null;
            PlayerComboBonusMilestoneDefinition scaledMilestone = scaledMilestones != null && milestoneIndex < scaledMilestoneCount
                ? scaledMilestones[milestoneIndex]
                : null;
            PlayerComboBonusMilestoneDefinition formulaSource = sourceMilestone != null ? sourceMilestone : scaledMilestone;
            float baseProgressPercent = sourceMilestone != null
                ? sourceMilestone.RequiredProgressPercent
                : scaledMilestone != null ? scaledMilestone.RequiredProgressPercent : 0f;
            float runtimeProgressPercent = scaledMilestone != null
                ? scaledMilestone.RequiredProgressPercent
                : sourceMilestone != null ? sourceMilestone.RequiredProgressPercent : 0f;
            int formulaStartIndex = characterTuningFormulaBuffer.Length;
            int formulaCount = AppendBonusFormulas(formulaSource != null ? formulaSource.Bonuses : null,
                                                   characterTuningFormulaBuffer);
            int passiveUnlockStartIndex = basePassiveUnlocks.Length;
            int passiveUnlockCount = AppendPassiveUnlocks(sourceMilestone != null ? sourceMilestone.PassivePowerUpUnlocks : null,
                                                           scaledMilestone != null ? scaledMilestone.PassivePowerUpUnlocks : null,
                                                           basePassiveUnlocks,
                                                           runtimePassiveUnlocks);

            baseRanks.Add(new PlayerBaseComboRankElement
            {
                Mode = PlayerComboCounterMode.SingleRankProgression,
                RankId = new FixedString64Bytes(ResolveMilestoneId(milestoneIndex, sourceMilestone, scaledMilestone)),
                Enabled = ResolveMilestoneEnabled(sourceMilestone, scaledMilestone),
                RequiredComboValue = PlayerComboCounterRuntimeUtility.ResolveSingleRankMilestoneRequiredValue(baseConfig.SingleRankMaximumComboValue,
                                                                                                               baseProgressPercent),
                RequiredProgressPercent = baseProgressPercent,
                PointsDecayPerSecond = 0f,
                ProgressiveBoostPercent = 0f,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
            runtimeRanks.Add(new PlayerRuntimeComboRankElement
            {
                Mode = PlayerComboCounterMode.SingleRankProgression,
                RankId = new FixedString64Bytes(ResolveMilestoneId(milestoneIndex, scaledMilestone, sourceMilestone)),
                Enabled = ResolveMilestoneEnabled(scaledMilestone, sourceMilestone),
                RequiredComboValue = PlayerComboCounterRuntimeUtility.ResolveSingleRankMilestoneRequiredValue(runtimeConfig.SingleRankMaximumComboValue,
                                                                                                               runtimeProgressPercent),
                RequiredProgressPercent = runtimeProgressPercent,
                PointsDecayPerSecond = 0f,
                ProgressiveBoostPercent = 0f,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
        }
    }

    /// <summary>
    /// Appends all valid Character Tuning formulas owned by one combo reward entry.
    /// </summary>
    /// <param name="bonuses">Authored Character Tuning payload inspected for formulas.</param>
    /// <param name="characterTuningFormulaBuffer">Shared flattened Character Tuning formula buffer.</param>
    /// <returns>Number of formulas appended for the provided reward entry.</returns>
    private static int AppendBonusFormulas(PowerUpCharacterTuningModuleData bonuses,
                                           DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer)
    {
        IReadOnlyList<PowerUpCharacterTuningFormulaData> formulas = bonuses != null ? bonuses.Formulas : null;

        if (formulas == null)
        {
            return 0;
        }

        int appendedFormulaCount = 0;

        for (int formulaIndex = 0; formulaIndex < formulas.Count; formulaIndex++)
        {
            PowerUpCharacterTuningFormulaData formulaData = formulas[formulaIndex];
            string formula = formulaData != null ? formulaData.Formula : string.Empty;

            if (string.IsNullOrWhiteSpace(formula))
            {
                continue;
            }

            characterTuningFormulaBuffer.Add(new PlayerPowerUpCharacterTuningFormulaElement
            {
                Formula = new FixedString128Bytes(formula.Trim())
            });
            appendedFormulaCount += 1;
        }

        return appendedFormulaCount;
    }

    /// <summary>
    /// Appends base and runtime passive unlock entries authored under one combo rank.
    /// </summary>
    /// <param name="sourceUnlocks">Unscaled passive unlock list used for immutable baseline values.</param>
    /// <param name="scaledUnlocks">Scaled passive unlock list used for initial runtime values.</param>
    /// <param name="basePassiveUnlocks">Destination immutable passive unlock buffer.</param>
    /// <param name="runtimePassiveUnlocks">Destination mutable passive unlock buffer.</param>
    /// <returns>Number of unlock entries appended for the rank.</returns>
    private static int AppendPassiveUnlocks(IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> sourceUnlocks,
                                            IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> scaledUnlocks,
                                            DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                            DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        int sourceUnlockCount = sourceUnlocks != null ? sourceUnlocks.Count : 0;
        int scaledUnlockCount = scaledUnlocks != null ? scaledUnlocks.Count : 0;
        int unlockCount = math.max(sourceUnlockCount, scaledUnlockCount);

        for (int unlockIndex = 0; unlockIndex < unlockCount; unlockIndex++)
        {
            PlayerComboPassivePowerUpUnlockDefinition sourceUnlock = sourceUnlocks != null && unlockIndex < sourceUnlockCount ? sourceUnlocks[unlockIndex] : null;
            PlayerComboPassivePowerUpUnlockDefinition scaledUnlock = scaledUnlocks != null && unlockIndex < scaledUnlockCount ? scaledUnlocks[unlockIndex] : null;
            basePassiveUnlocks.Add(new PlayerBaseComboPassiveUnlockElement
            {
                PassivePowerUpId = new FixedString64Bytes(ResolvePassivePowerUpId(sourceUnlock, scaledUnlock)),
                IsEnabled = ResolvePassiveUnlockEnabled(sourceUnlock, scaledUnlock)
            });
            runtimePassiveUnlocks.Add(new PlayerRuntimeComboPassiveUnlockElement
            {
                PassivePowerUpId = new FixedString64Bytes(ResolvePassivePowerUpId(scaledUnlock, sourceUnlock)),
                IsEnabled = ResolvePassiveUnlockEnabled(scaledUnlock, sourceUnlock)
            });
        }

        return unlockCount;
    }

    /// <summary>
    /// Resolves one milestone identifier from preferred and fallback authoring entries.
    /// </summary>
    /// <param name="milestoneIndex">Zero-based milestone index used by the generated fallback.</param>
    /// <param name="preferredMilestone">Preferred authored milestone.</param>
    /// <param name="fallbackMilestone">Fallback authored milestone.</param>
    /// <returns>Stable non-empty milestone identifier.</returns>
    private static string ResolveMilestoneId(int milestoneIndex,
                                             PlayerComboBonusMilestoneDefinition preferredMilestone,
                                             PlayerComboBonusMilestoneDefinition fallbackMilestone)
    {
        if (preferredMilestone != null && !string.IsNullOrWhiteSpace(preferredMilestone.MilestoneId))
            return preferredMilestone.MilestoneId.Trim();

        if (fallbackMilestone != null && !string.IsNullOrWhiteSpace(fallbackMilestone.MilestoneId))
            return fallbackMilestone.MilestoneId.Trim();

        return string.Format("Milestone{0:00}", milestoneIndex + 1);
    }

    /// <summary>
    /// Resolves one milestone enable flag from preferred and fallback authoring entries.
    /// </summary>
    /// <param name="preferredMilestone">Preferred authored milestone.</param>
    /// <param name="fallbackMilestone">Fallback authored milestone.</param>
    /// <returns>One when the resolved milestone is enabled; otherwise zero.</returns>
    private static byte ResolveMilestoneEnabled(PlayerComboBonusMilestoneDefinition preferredMilestone,
                                                PlayerComboBonusMilestoneDefinition fallbackMilestone)
    {
        if (preferredMilestone != null)
            return preferredMilestone.IsEnabled ? (byte)1 : (byte)0;

        return fallbackMilestone != null && fallbackMilestone.IsEnabled ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Resolves one passive PowerUpId from a preferred unlock entry with fallback support.
    /// </summary>
    /// <param name="preferredUnlock">Preferred unlock entry.</param>
    /// <param name="fallbackUnlock">Fallback unlock entry.</param>
    /// <returns>Trimmed PowerUpId or an empty string when no valid ID is authored.</returns>
    private static string ResolvePassivePowerUpId(PlayerComboPassivePowerUpUnlockDefinition preferredUnlock,
                                                  PlayerComboPassivePowerUpUnlockDefinition fallbackUnlock)
    {
        if (preferredUnlock != null && !string.IsNullOrWhiteSpace(preferredUnlock.PassivePowerUpId))
        {
            return preferredUnlock.PassivePowerUpId.Trim();
        }

        if (fallbackUnlock != null && !string.IsNullOrWhiteSpace(fallbackUnlock.PassivePowerUpId))
        {
            return fallbackUnlock.PassivePowerUpId.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves one passive unlock enable flag from a preferred unlock entry with fallback support.
    /// </summary>
    /// <param name="preferredUnlock">Preferred unlock entry.</param>
    /// <param name="fallbackUnlock">Fallback unlock entry.</param>
    /// <returns>One when the resolved unlock is enabled; otherwise zero.</returns>
    private static byte ResolvePassiveUnlockEnabled(PlayerComboPassivePowerUpUnlockDefinition preferredUnlock,
                                                    PlayerComboPassivePowerUpUnlockDefinition fallbackUnlock)
    {
        if (preferredUnlock != null)
        {
            return preferredUnlock.IsEnabled ? (byte)1 : (byte)0;
        }

        if (fallbackUnlock != null)
        {
            return fallbackUnlock.IsEnabled ? (byte)1 : (byte)0;
        }

        return 0;
    }

    #endregion

    #endregion
}
