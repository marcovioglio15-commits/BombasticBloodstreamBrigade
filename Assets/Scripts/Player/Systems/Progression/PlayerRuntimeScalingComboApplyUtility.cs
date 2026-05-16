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
            ComboGainPerKill = baseComboConfig.ComboGainPerKill,
            DamageBreakMode = baseComboConfig.DamageBreakMode,
            ShieldDamageBreaksCombo = baseComboConfig.ShieldDamageBreaksCombo,
            PreventDecayIntoNonDecayingRanks = baseComboConfig.PreventDecayIntoNonDecayingRanks
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
                    RankId = baseRank.RankId,
                    RequiredComboValue = baseRank.RequiredComboValue,
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

                ApplyComboBooleanValue(scalingElement.FieldId,
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

                ApplyComboTokenValue(scalingElement.FieldId,
                                     scalingElement.RankIndex,
                                     scalingElement.PassiveUnlockIndex,
                                     resolvedToken,
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

            ApplyComboNumericValue(scalingElement.FieldId,
                                   scalingElement.RankIndex,
                                   resolvedValue,
                                   ref runtimeComboConfig,
                                   runtimeComboRanks);
        }
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
    /// <param name="runtimeComboRanks">Current runtime combo-rank buffer.</param>
    /// <param name="characterTuningFormulas">Shared Character Tuning formula buffer.</param>
    /// <param name="mutableScalableStats">Mutable effective scalable-stat list updated in place.</param>
    public static void ApplyActiveComboRankBonuses(int activeRankIndex,
                                                   int comboValue,
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

        for (int rankIndex = 0; rankIndex <= activeRankIndex && rankIndex < runtimeComboRanks.Length; rankIndex++)
        {
            PlayerRuntimeComboRankElement runtimeRank = runtimeComboRanks[rankIndex];
            PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningRange(runtimeRank.BonusFormulaStartIndex,
                                                                                    runtimeRank.BonusFormulaCount,
                                                                                    characterTuningFormulas,
                                                                                    mutableScalableStats,
                                                                                    out int _);
        }

        int nextRankIndex = activeRankIndex + 1;

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
    /// Applies one boolean combo scaling result to the runtime combo config.
    /// </summary>
    /// <param name="fieldId">Target combo field identifier.</param>
    /// <param name="rankIndex">Runtime rank index addressed by the scaling metadata.</param>
    /// <param name="passiveUnlockIndex">Runtime passive unlock index addressed by the scaling metadata.</param>
    /// <param name="resolvedBoolean">Evaluated boolean value.</param>
    /// <param name="runtimeComboConfig">Mutable runtime combo config updated in place.</param>
    /// <param name="runtimeComboRanks">Mutable runtime combo-rank buffer updated in place.</param>
    /// <param name="runtimePassiveUnlocks">Mutable runtime passive-unlock buffer updated in place.</param>
    private static void ApplyComboBooleanValue(PlayerRuntimeComboCounterFieldId fieldId,
                                               int rankIndex,
                                               int passiveUnlockIndex,
                                               bool resolvedBoolean,
                                               ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                               DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                               DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.Enabled:
                runtimeComboConfig.Enabled = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.ShieldDamageBreaksCombo:
                runtimeComboConfig.ShieldDamageBreaksCombo = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.PreventDecayIntoNonDecayingRanks:
                runtimeComboConfig.PreventDecayIntoNonDecayingRanks = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.RankPassiveUnlockEnabled:
                if (!TryResolvePassiveUnlockAbsoluteIndex(rankIndex, passiveUnlockIndex, runtimeComboRanks, runtimePassiveUnlocks, out int absoluteUnlockIndex))
                {
                    return;
                }

                PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[absoluteUnlockIndex];
                passiveUnlock.IsEnabled = resolvedBoolean ? (byte)1 : (byte)0;
                runtimePassiveUnlocks[absoluteUnlockIndex] = passiveUnlock;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric combo scaling result to the runtime combo config or one runtime combo rank.
    /// </summary>
    /// <param name="fieldId">Target combo field identifier.</param>
    /// <param name="rankIndex">Runtime rank index addressed by the scaling metadata.</param>
    /// <param name="resolvedValue">Evaluated numeric value.</param>
    /// <param name="runtimeComboConfig">Mutable runtime combo config updated in place.</param>
    /// <param name="runtimeComboRanks">Mutable runtime combo-rank buffer updated in place.</param>
    private static void ApplyComboNumericValue(PlayerRuntimeComboCounterFieldId fieldId,
                                               int rankIndex,
                                               float resolvedValue,
                                               ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                               DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks)
    {
        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.ComboGainPerKill:
                runtimeComboConfig.ComboGainPerKill = math.max(0, (int)math.round(resolvedValue));
                break;
            case PlayerRuntimeComboCounterFieldId.DamageBreakMode:
                runtimeComboConfig.DamageBreakMode = PlayerRuntimeScalingEnumUtility.ResolveComboDamageBreakMode(resolvedValue);
                break;
            case PlayerRuntimeComboCounterFieldId.RankRequiredComboValue:
                if (rankIndex < 0 || rankIndex >= runtimeComboRanks.Length)
                {
                    return;
                }

                PlayerRuntimeComboRankElement runtimeRank = runtimeComboRanks[rankIndex];
                runtimeRank.RequiredComboValue = math.max(0, (int)math.round(resolvedValue));
                runtimeComboRanks[rankIndex] = runtimeRank;
                break;
            case PlayerRuntimeComboCounterFieldId.RankPointsDecayPerSecond:
                if (rankIndex < 0 || rankIndex >= runtimeComboRanks.Length)
                {
                    return;
                }

                PlayerRuntimeComboRankElement decayRuntimeRank = runtimeComboRanks[rankIndex];
                decayRuntimeRank.PointsDecayPerSecond = math.max(0f, resolvedValue);
                runtimeComboRanks[rankIndex] = decayRuntimeRank;
                break;
            case PlayerRuntimeComboCounterFieldId.RankProgressiveBoostPercent:
                if (rankIndex < 0 || rankIndex >= runtimeComboRanks.Length)
                {
                    return;
                }

                PlayerRuntimeComboRankElement progressiveRuntimeRank = runtimeComboRanks[rankIndex];
                progressiveRuntimeRank.ProgressiveBoostPercent = resolvedValue;
                runtimeComboRanks[rankIndex] = progressiveRuntimeRank;
                break;
        }
    }

    /// <summary>
    /// Applies one token combo scaling result to a runtime passive unlock entry.
    /// </summary>
    /// <param name="fieldId">Target combo field identifier.</param>
    /// <param name="rankIndex">Runtime rank index addressed by the scaling metadata.</param>
    /// <param name="passiveUnlockIndex">Runtime passive unlock index addressed by the scaling metadata.</param>
    /// <param name="resolvedToken">Evaluated token value.</param>
    /// <param name="runtimeComboRanks">Runtime combo-rank buffer used to resolve the nested unlock range.</param>
    /// <param name="runtimePassiveUnlocks">Mutable runtime passive-unlock buffer updated in place.</param>
    private static void ApplyComboTokenValue(PlayerRuntimeComboCounterFieldId fieldId,
                                             int rankIndex,
                                             int passiveUnlockIndex,
                                             string resolvedToken,
                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                             DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        if (fieldId != PlayerRuntimeComboCounterFieldId.RankPassiveUnlockPowerUpId)
        {
            return;
        }

        if (!TryResolvePassiveUnlockAbsoluteIndex(rankIndex, passiveUnlockIndex, runtimeComboRanks, runtimePassiveUnlocks, out int absoluteUnlockIndex))
        {
            return;
        }

        PlayerRuntimeComboPassiveUnlockElement passiveUnlock = runtimePassiveUnlocks[absoluteUnlockIndex];
        passiveUnlock.PassivePowerUpId = new FixedString64Bytes(string.IsNullOrWhiteSpace(resolvedToken) ? string.Empty : resolvedToken.Trim());
        runtimePassiveUnlocks[absoluteUnlockIndex] = passiveUnlock;
    }

    /// <summary>
    /// Resolves one rank-local passive unlock index to its absolute runtime buffer index.
    /// </summary>
    /// <param name="rankIndex">Runtime rank index owning the passive unlock.</param>
    /// <param name="passiveUnlockIndex">Rank-local passive unlock index.</param>
    /// <param name="runtimeComboRanks">Runtime rank buffer containing unlock ranges.</param>
    /// <param name="runtimePassiveUnlocks">Runtime passive-unlock buffer addressed by resolved absolute index.</param>
    /// <param name="absoluteUnlockIndex">Resolved absolute runtime passive-unlock index.</param>
    /// <returns>True when the nested index resolves to a valid passive-unlock element.</returns>
    private static bool TryResolvePassiveUnlockAbsoluteIndex(int rankIndex,
                                                             int passiveUnlockIndex,
                                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                             DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                                             out int absoluteUnlockIndex)
    {
        absoluteUnlockIndex = -1;

        if (!runtimeComboRanks.IsCreated || !runtimePassiveUnlocks.IsCreated)
        {
            return false;
        }

        if (rankIndex < 0 || rankIndex >= runtimeComboRanks.Length || passiveUnlockIndex < 0)
        {
            return false;
        }

        PlayerRuntimeComboRankElement runtimeRank = runtimeComboRanks[rankIndex];

        if (passiveUnlockIndex >= runtimeRank.PassiveUnlockCount)
        {
            return false;
        }

        int resolvedUnlockIndex = runtimeRank.PassiveUnlockStartIndex + passiveUnlockIndex;

        if (resolvedUnlockIndex < 0 || resolvedUnlockIndex >= runtimePassiveUnlocks.Length)
        {
            return false;
        }

        absoluteUnlockIndex = resolvedUnlockIndex;
        return true;
    }
    #endregion

    #endregion
}
