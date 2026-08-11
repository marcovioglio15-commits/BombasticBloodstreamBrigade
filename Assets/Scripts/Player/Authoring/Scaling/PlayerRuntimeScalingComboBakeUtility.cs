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
/// none.
/// </summary>
internal static class PlayerRuntimeScalingComboBakeUtility
{
    #region Constants
    private const string ComboRanksRoot = "comboCounter.rankDefinitions.Array.data[";
    #endregion

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
            ComboGainPerKill = resolvedRuntimeSourceConfig.ComboGainPerKill,
            DamageBreakMode = resolvedRuntimeSourceConfig.DamageBreakMode,
            ShieldDamageBreaksCombo = resolvedRuntimeSourceConfig.ShieldDamageBreaksCombo,
            PreventDecayIntoNonDecayingRanks = resolvedRuntimeSourceConfig.PreventDecayIntoNonDecayingRanks
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
            int formulaCount = AppendRankBonusFormulas(formulaSourceRank, characterTuningFormulaBuffer);
            int passiveUnlockStartIndex = basePassiveUnlocks.Length;
            int passiveUnlockCount = AppendPassiveUnlocks(sourceRank,
                                                          scaledRank,
                                                          basePassiveUnlocks,
                                                          runtimePassiveUnlocks);

            baseRanks.Add(new PlayerBaseComboRankElement
            {
                RankId = new FixedString64Bytes(rankId),
                RequiredComboValue = requiredBaseValue,
                PointsDecayPerSecond = pointsDecayPerSecondBaseValue,
                ProgressiveBoostPercent = progressiveBoostPercentBaseValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
            runtimeRanks.Add(new PlayerRuntimeComboRankElement
            {
                RankId = new FixedString64Bytes(rankId),
                RequiredComboValue = requiredRuntimeValue,
                PointsDecayPerSecond = pointsDecayPerSecondRuntimeValue,
                ProgressiveBoostPercent = progressiveBoostPercentRuntimeValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
        }
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

            if (!TryMapComboFieldId(scalingRule.StatKey,
                                    out int rankIndex,
                                    out int passiveUnlockIndex,
                                    out PlayerRuntimeComboCounterFieldId fieldId))
            {
                continue;
            }

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
            {
                continue;
            }

            if (!TryResolveComboScalingBaseMetadata(property,
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
            ComboGainPerKill = comboDefinition != null ? comboDefinition.ComboGainPerKill : 0,
            DamageBreakMode = comboDefinition != null ? comboDefinition.DamageBreakMode : PlayerComboDamageBreakMode.ResetCombo,
            ShieldDamageBreaksCombo = comboDefinition != null && comboDefinition.ShieldDamageBreaksCombo ? (byte)1 : (byte)0,
            PreventDecayIntoNonDecayingRanks = comboDefinition != null && comboDefinition.PreventDecayIntoNonDecayingRanks ? (byte)1 : (byte)0
        };
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
    /// Appends all valid Character Tuning formulas defined by one combo rank into the shared flattened runtime buffer.
    /// </summary>
    /// <param name="rankDefinition">Authored combo rank inspected for bonus formulas.</param>
    /// <param name="characterTuningFormulaBuffer">Shared flattened Character Tuning formula buffer.</param>
    /// <returns>Number of formulas appended for the provided rank.</returns>
    private static int AppendRankBonusFormulas(PlayerComboRankDefinition rankDefinition,
                                               DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer)
    {
        PowerUpCharacterTuningModuleData rankBonuses = rankDefinition != null ? rankDefinition.RankBonuses : null;
        IReadOnlyList<PowerUpCharacterTuningFormulaData> formulas = rankBonuses != null ? rankBonuses.Formulas : null;

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
    /// <param name="sourceRank">Unscaled rank used for immutable baseline values.</param>
    /// <param name="scaledRank">Scaled rank used for initial runtime values.</param>
    /// <param name="basePassiveUnlocks">Destination immutable passive unlock buffer.</param>
    /// <param name="runtimePassiveUnlocks">Destination mutable passive unlock buffer.</param>
    /// <returns>Number of unlock entries appended for the rank.</returns>
    private static int AppendPassiveUnlocks(PlayerComboRankDefinition sourceRank,
                                            PlayerComboRankDefinition scaledRank,
                                            DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                            DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> sourceUnlocks = sourceRank != null ? sourceRank.PassivePowerUpUnlocks : null;
        IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> scaledUnlocks = scaledRank != null ? scaledRank.PassivePowerUpUnlocks : null;
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

#if UNITY_EDITOR
    /// <summary>
    /// Resolves combo scaling baseline metadata, including token-backed passive PowerUpId fields.
    /// </summary>
    /// <param name="property">Serialized property targeted by Add Scaling.</param>
    /// <param name="valueType">Runtime formula value type.</param>
    /// <param name="baseValue">Numeric base value when applicable.</param>
    /// <param name="baseBooleanValue">Boolean base value when applicable.</param>
    /// <param name="isInteger">True when numeric values should be rounded before assignment.</param>
    /// <param name="baseTokenValue">Token base value when applicable.</param>
    /// <returns>True when the serialized property can be converted to combo scaling metadata.</returns>
    private static bool TryResolveComboScalingBaseMetadata(SerializedProperty property,
                                                           out byte valueType,
                                                           out float baseValue,
                                                           out byte baseBooleanValue,
                                                           out byte isInteger,
                                                           out FixedString64Bytes baseTokenValue)
    {
        baseTokenValue = default;

        if (property != null && property.propertyType == SerializedPropertyType.String)
        {
            valueType = (byte)PlayerFormulaValueType.Token;
            baseValue = 0f;
            baseBooleanValue = 0;
            isInteger = 0;
            string tokenValue = string.IsNullOrWhiteSpace(property.stringValue)
                ? string.Empty
                : property.stringValue.Trim();
            baseTokenValue = new FixedString64Bytes(tokenValue);
            return true;
        }

        return PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                             out valueType,
                                                                             out baseValue,
                                                                             out baseBooleanValue,
                                                                             out isInteger);
    }

    /// <summary>
    /// Maps one progression Add Scaling stat key to the combo runtime field targeted by that rule.
    /// </summary>
    /// <param name="statKey">Stable Add Scaling stat key emitted by the progression preset.</param>
    /// <param name="rankIndex">Resolved combo rank index when the mapping targets one rank milestone.</param>
    /// <param name="passiveUnlockIndex">Resolved passive unlock index when the mapping targets one nested passive unlock.</param>
    /// <param name="fieldId">Resolved combo runtime field identifier.</param>
    /// <returns>True when the stat key targets the combo module; otherwise false.</returns>
    private static bool TryMapComboFieldId(string statKey,
                                           out int rankIndex,
                                           out int passiveUnlockIndex,
                                           out PlayerRuntimeComboCounterFieldId fieldId)
    {
        rankIndex = -1;
        passiveUnlockIndex = -1;
        fieldId = default;

        if (string.IsNullOrWhiteSpace(statKey))
        {
            return false;
        }

        if (string.Equals(statKey, "comboCounter.isEnabled", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.Enabled;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.comboGainPerKill", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.ComboGainPerKill;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.shieldDamageBreaksCombo", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.ShieldDamageBreaksCombo;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.preventDecayIntoNonDecayingRanks", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.PreventDecayIntoNonDecayingRanks;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.damageBreakMode", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.DamageBreakMode;
            return true;
        }

        if (!statKey.StartsWith(ComboRanksRoot, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseStableArrayIndex(statKey, 0, out rankIndex))
        {
            rankIndex = -1;
            return false;
        }

        if (statKey.Contains(".passivePowerUpUnlocks.Array.data[", StringComparison.Ordinal))
        {
            if (!TryParseStableArrayIndex(statKey, 1, out passiveUnlockIndex))
            {
                rankIndex = -1;
                passiveUnlockIndex = -1;
                return false;
            }

            if (statKey.EndsWith(".isEnabled", StringComparison.Ordinal))
            {
                fieldId = PlayerRuntimeComboCounterFieldId.RankPassiveUnlockEnabled;
                return true;
            }

            if (statKey.EndsWith(".passivePowerUpId", StringComparison.Ordinal))
            {
                fieldId = PlayerRuntimeComboCounterFieldId.RankPassiveUnlockPowerUpId;
                return true;
            }

            rankIndex = -1;
            passiveUnlockIndex = -1;
            return false;
        }

        if (statKey.EndsWith(".requiredComboValue", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.RankRequiredComboValue;
            return true;
        }

        if (statKey.EndsWith(".pointsDecayPerSecond", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.RankPointsDecayPerSecond;
            return true;
        }

        if (statKey.EndsWith(".progressiveBoostPercent", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.RankProgressiveBoostPercent;
            return true;
        }

        rankIndex = -1;
        passiveUnlockIndex = -1;
        return false;
    }

    /// <summary>
    /// Extracts one authored array index from a stable Add Scaling key token such as data[2|rankId:S].
    /// </summary>
    /// <param name="statKey">Stable Add Scaling stat key containing array tokens.</param>
    /// <param name="occurrenceIndex">Zero-based data[] occurrence to parse.</param>
    /// <param name="arrayIndex">Parsed authored array index.</param>
    /// <returns>True when the requested array token was parsed successfully; otherwise false.</returns>
    private static bool TryParseStableArrayIndex(string statKey, int occurrenceIndex, out int arrayIndex)
    {
        arrayIndex = -1;

        int dataStartIndex = -1;
        int searchStartIndex = 0;

        for (int currentOccurrenceIndex = 0; currentOccurrenceIndex <= occurrenceIndex; currentOccurrenceIndex++)
        {
            dataStartIndex = statKey.IndexOf("data[", searchStartIndex, StringComparison.Ordinal);

            if (dataStartIndex < 0)
            {
                return false;
            }

            searchStartIndex = dataStartIndex + 5;
        }

        int dataEndIndex = statKey.IndexOf(']', dataStartIndex);

        if (dataStartIndex < 0 || dataEndIndex <= dataStartIndex)
        {
            return false;
        }

        string token = statKey.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5);
        int separatorIndex = token.IndexOf('|');
        string indexText = separatorIndex >= 0 ? token.Substring(0, separatorIndex) : token;
        return int.TryParse(indexText, out arrayIndex);
    }
#endif

    #endregion

    #endregion
}
