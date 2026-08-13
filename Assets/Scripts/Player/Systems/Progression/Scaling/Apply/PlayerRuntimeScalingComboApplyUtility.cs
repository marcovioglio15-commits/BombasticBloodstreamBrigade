using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds combo runtime data and applies active combo-rank bonuses onto the effective scalable-stat view.
/// </summary>
internal static class PlayerRuntimeScalingComboApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the mutable combo runtime config and rank thresholds from immutable baselines plus Add Scaling metadata.
    /// </summary>
    /// <param name="baseComboConfig">Immutable combo baseline.</param>
    /// <param name="runtimeComboConfig">Mutable runtime combo config rebuilt in place.</param>
    /// <param name="baseComboRanks">Immutable combo-rank baseline buffer.</param>
    /// <param name="runtimeComboRanks">Mutable runtime combo-rank buffer rebuilt in place.</param>
    /// <param name="basePassiveUnlocks">Immutable combo passive-unlock baseline buffer.</param>
    /// <param name="runtimePassiveUnlocks">Mutable runtime combo passive-unlock buffer rebuilt in place.</param>
    /// <param name="comboScaling">Combo scaling metadata baked from Add Scaling rules.</param>
    /// <param name="variableContext">Current typed scalable-stat context used to evaluate formulas.</param>
    public static void RebuildRuntimeComboCounter(in PlayerBaseComboCounterConfig baseComboConfig,
                                                  ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                                  DynamicBuffer<PlayerBaseComboRankElement> baseComboRanks,
                                                  DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                  DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                                  DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                                  DynamicBuffer<PlayerRuntimeComboCounterScalingElement> comboScaling,
                                                  IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        runtimeComboConfig = new PlayerRuntimeComboCounterConfig
        {
            Enabled = baseComboConfig.Enabled,
            Mode = baseComboConfig.Mode,
            ComboGainPerKill = baseComboConfig.ComboGainPerKill,
            DamageBreakMode = baseComboConfig.DamageBreakMode,
            ShieldDamageBreaksCombo = baseComboConfig.ShieldDamageBreaksCombo,
            PreventDecayIntoNonDecayingRanks = baseComboConfig.PreventDecayIntoNonDecayingRanks,
            SingleRankId = baseComboConfig.SingleRankId,
            SingleRankMaximumComboValue = baseComboConfig.SingleRankMaximumComboValue,
            SingleRankPointsDecayPerSecond = baseComboConfig.SingleRankPointsDecayPerSecond,
            SingleRankValueDisplayMode = baseComboConfig.SingleRankValueDisplayMode,
            SingleRankFormulaDistributionMode = baseComboConfig.SingleRankFormulaDistributionMode,
            SingleRankLinearBonusRangeMode = baseComboConfig.SingleRankLinearBonusRangeMode,
            SingleRankShowMeterOnlyAfterFirstMilestone = baseComboConfig.SingleRankShowMeterOnlyAfterFirstMilestone,
            SingleRankStartLinearBonusesAtFirstMilestone = baseComboConfig.SingleRankStartLinearBonusesAtFirstMilestone
        };

        if (!runtimeComboRanks.IsCreated || !runtimePassiveUnlocks.IsCreated)
        {
            return;
        }

        runtimeComboRanks.Clear();
        runtimePassiveUnlocks.Clear();

        if (baseComboRanks.IsCreated)
        {
            for (int rankIndex = 0; rankIndex < baseComboRanks.Length; rankIndex++)
            {
                PlayerBaseComboRankElement baseRank = baseComboRanks[rankIndex];
                runtimeComboRanks.Add(new PlayerRuntimeComboRankElement
                {
                    Mode = baseRank.Mode,
                    RankId = baseRank.RankId,
                    Enabled = baseRank.Enabled,
                    RequiredComboValue = baseRank.RequiredComboValue,
                    RequiredProgressPercent = baseRank.RequiredProgressPercent,
                    PointsDecayPerSecond = baseRank.PointsDecayPerSecond,
                    ProgressiveBoostPercent = baseRank.ProgressiveBoostPercent,
                    BonusFormulaStartIndex = baseRank.BonusFormulaStartIndex,
                    BonusFormulaCount = baseRank.BonusFormulaCount,
                    PassiveUnlockStartIndex = baseRank.PassiveUnlockStartIndex,
                    PassiveUnlockCount = baseRank.PassiveUnlockCount
                });
            }
        }

        if (basePassiveUnlocks.IsCreated)
        {
            for (int unlockIndex = 0; unlockIndex < basePassiveUnlocks.Length; unlockIndex++)
            {
                PlayerBaseComboPassiveUnlockElement baseUnlock = basePassiveUnlocks[unlockIndex];
                runtimePassiveUnlocks.Add(new PlayerRuntimeComboPassiveUnlockElement
                {
                    PassivePowerUpId = baseUnlock.PassivePowerUpId,
                    IsEnabled = baseUnlock.IsEnabled
                });
            }
        }

        if (!comboScaling.IsCreated)
        {
            PlayerRuntimeScalingComboFieldApplyUtility.RefreshSingleRankMilestoneThresholds(in runtimeComboConfig,
                                                                                             runtimeComboRanks);
            return;
        }

        for (int scalingIndex = 0; scalingIndex < comboScaling.Length; scalingIndex++)
        {
            PlayerRuntimeComboCounterScalingElement scalingElement = comboScaling[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    continue;
                }

                PlayerRuntimeScalingComboFieldApplyUtility.ApplyBooleanValue(scalingElement.FieldId,
                                                                              scalingElement.EntryMode,
                                                                              scalingElement.RankIndex,
                                                                              scalingElement.PassiveUnlockIndex,
                                                                              resolvedBoolean,
                                                                              ref runtimeComboConfig,
                                                                              runtimeComboRanks,
                                                                              runtimePassiveUnlocks);
                continue;
            }

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Token)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                        scalingElement.BaseTokenValue.ToString(),
                                                                                        variableContext,
                                                                                        out string resolvedToken))
                {
                    continue;
                }

                PlayerRuntimeScalingComboFieldApplyUtility.ApplyTokenValue(scalingElement.FieldId,
                                                                            scalingElement.EntryMode,
                                                                            scalingElement.RankIndex,
                                                                            scalingElement.PassiveUnlockIndex,
                                                                            resolvedToken,
                                                                            ref runtimeComboConfig,
                                                                            runtimeComboRanks,
                                                                            runtimePassiveUnlocks);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimeScalingComboFieldApplyUtility.ApplyNumericValue(scalingElement.FieldId,
                                                                          scalingElement.EntryMode,
                                                                          scalingElement.RankIndex,
                                                                          resolvedValue,
                                                                          ref runtimeComboConfig,
                                                                          runtimeComboRanks);
        }

        PlayerRuntimeScalingComboFieldApplyUtility.RefreshSingleRankMilestoneThresholds(in runtimeComboConfig,
                                                                                         runtimeComboRanks);
    }

    /// <summary>
    /// Copies the current scalable-stat buffer into the mutable list that receives temporary combo rank bonuses.
    /// </summary>
    /// <param name="scalableStats">Source scalable-stat buffer.</param>
    /// <param name="destination">Mutable list reused as effective scalable-stat state.</param>
    public static void CopyBaseScalableStats(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                             List<PlayerScalableStatElement> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
        {
            return;
        }

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            destination.Add(scalableStats[statIndex]);
        }
    }

    /// <summary>
    /// Applies cumulative Character Tuning formulas from every active combo rank onto the effective scalable-stat list.
    /// </summary>
    /// <param name="activeRankIndex">Highest currently active combo-rank index, or -1 when no rank is active.</param>
    /// <param name="comboValue">Current combo value used to resolve progressive next-rank boost weight.</param>
    /// <param name="runtimeComboConfig">Current combo topology and single-rank formula distribution settings.</param>
    /// <param name="runtimeComboRanks">Current runtime combo-rank buffer.</param>
    /// <param name="characterTuningFormulas">Shared Character Tuning formula buffer.</param>
    /// <param name="mutableScalableStats">Mutable effective scalable-stat list updated in place.</param>
    public static void ApplyActiveComboRankBonuses(int activeRankIndex,
                                                   int comboValue,
                                                   in PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                                   DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                   DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                   List<PlayerScalableStatElement> mutableScalableStats)
    {
        if (mutableScalableStats == null || mutableScalableStats.Count <= 0)
        {
            return;
        }

        if (!runtimeComboRanks.IsCreated || runtimeComboRanks.Length <= 0)
        {
            return;
        }

        if (runtimeComboConfig.Mode == PlayerComboCounterMode.SingleRankProgression &&
            runtimeComboConfig.SingleRankFormulaDistributionMode == PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression)
        {
            ApplyLinearSingleRankBonuses(comboValue,
                                         in runtimeComboConfig,
                                         runtimeComboRanks,
                                         characterTuningFormulas,
                                         mutableScalableStats);
            return;
        }

        for (int rankIndex = 0; rankIndex <= activeRankIndex && rankIndex < runtimeComboRanks.Length; rankIndex++)
        {
            PlayerRuntimeComboRankElement runtimeRank = runtimeComboRanks[rankIndex];

            if (runtimeRank.Mode != runtimeComboConfig.Mode || runtimeRank.Enabled == 0)
                continue;

            PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningRange(runtimeRank.BonusFormulaStartIndex,
                                                                                    runtimeRank.BonusFormulaCount,
                                                                                    characterTuningFormulas,
                                                                                    mutableScalableStats,
                                                                                    out int _);
        }

        int nextRankIndex = ResolveNextEnabledEntryIndex(activeRankIndex,
                                                         runtimeComboConfig.Mode,
                                                         runtimeComboRanks);

        if (nextRankIndex < 0 || nextRankIndex >= runtimeComboRanks.Length)
        {
            return;
        }

        PlayerRuntimeComboRankElement nextRuntimeRank = runtimeComboRanks[nextRankIndex];
        float progressiveBoostPercent = math.saturate(nextRuntimeRank.ProgressiveBoostPercent * 0.01f);

        if (progressiveBoostPercent <= 0f)
        {
            return;
        }

        float progressToNextRank = PlayerComboCounterRuntimeUtility.ResolveProgressToRank(comboValue,
                                                                                          activeRankIndex,
                                                                                          nextRankIndex,
                                                                                          runtimeComboRanks);
        float applicationWeight = progressToNextRank * progressiveBoostPercent;

        if (applicationWeight <= 0f)
        {
            return;
        }

        PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningRange(nextRuntimeRank.BonusFormulaStartIndex,
                                                                                nextRuntimeRank.BonusFormulaCount,
                                                                                characterTuningFormulas,
                                                                                mutableScalableStats,
                                                                                applicationWeight,
                                                                                out int _);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies every enabled single-rank numeric formula with a weight derived from the configured progression range.
    /// </summary>
    /// <param name="comboValue">Current combo value used to resolve formula application weights.</param>
    /// <param name="runtimeComboConfig">Current single-rank progression maximum and linear range rules.</param>
    /// <param name="runtimeComboRanks">Combined runtime reward-entry buffer.</param>
    /// <param name="characterTuningFormulas">Shared flattened Character Tuning formula buffer.</param>
    /// <param name="mutableScalableStats">Mutable effective scalable-stat list updated in place.</param>
    private static void ApplyLinearSingleRankBonuses(int comboValue,
                                                     in PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                                     DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                     DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                     List<PlayerScalableStatElement> mutableScalableStats)
    {
        float sharedApplicationWeight = 0f;

        if (runtimeComboConfig.SingleRankLinearBonusRangeMode == PlayerComboSingleRankLinearBonusRangeMode.EntireProgression)
        {
            sharedApplicationWeight = PlayerComboSingleRankLinearBonusUtility.ResolveRankProgressNormalized(comboValue,
                                                                                                              in runtimeComboConfig,
                                                                                                              runtimeComboRanks);

            if (sharedApplicationWeight <= 0f)
                return;
        }

        // Apply each milestone with either the shared rank weight or its threshold-local segment weight.
        for (int entryIndex = 0; entryIndex < runtimeComboRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement entry = runtimeComboRanks[entryIndex];

            if (entry.Mode != PlayerComboCounterMode.SingleRankProgression || entry.Enabled == 0)
                continue;

            float applicationWeight;

            switch (runtimeComboConfig.SingleRankLinearBonusRangeMode)
            {
                case PlayerComboSingleRankLinearBonusRangeMode.MilestoneToNextMilestone:
                    applicationWeight = PlayerComboSingleRankLinearBonusUtility.ResolveMilestoneProgressNormalized(comboValue,
                                                                                                                    entryIndex,
                                                                                                                    in runtimeComboConfig,
                                                                                                                    runtimeComboRanks);
                    break;
                default:
                    applicationWeight = sharedApplicationWeight;
                    break;
            }

            if (applicationWeight <= 0f)
                continue;

            PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningRange(entry.BonusFormulaStartIndex,
                                                                                    entry.BonusFormulaCount,
                                                                                    characterTuningFormulas,
                                                                                    mutableScalableStats,
                                                                                    applicationWeight,
                                                                                    out int _);
        }
    }

    /// <summary>
    /// Resolves the next enabled reward entry belonging to one combo topology.
    /// </summary>
    /// <param name="activeEntryIndex">Current absolute entry index, or -1 before the first reward.</param>
    /// <param name="mode">Combo topology whose next entry should be resolved.</param>
    /// <param name="runtimeComboRanks">Combined runtime reward-entry buffer.</param>
    /// <returns>Next matching absolute entry index, or -1 when no later entry exists.</returns>
    private static int ResolveNextEnabledEntryIndex(int activeEntryIndex,
                                                    PlayerComboCounterMode mode,
                                                    DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks)
    {
        for (int entryIndex = math.max(0, activeEntryIndex + 1); entryIndex < runtimeComboRanks.Length; entryIndex++)
        {
            PlayerRuntimeComboRankElement entry = runtimeComboRanks[entryIndex];

            if (entry.Mode == mode && entry.Enabled != 0)
                return entryIndex;
        }

        return -1;
    }
    #endregion

    #endregion
}
