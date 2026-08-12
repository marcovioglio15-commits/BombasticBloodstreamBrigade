#if UNITY_EDITOR
using System;
using Unity.Collections;
using UnityEditor;

/// <summary>
/// Maps stable combo Add Scaling keys and serialized baselines into compact ECS scaling metadata.
/// </summary>
internal static class PlayerRuntimeScalingComboFieldMappingUtility
{
    #region Constants
    private const string ComboRanksRoot = "comboCounter.rankDefinitions.Array.data[";
    private const string SingleRankMilestonesRoot = "comboCounter.singleRankProgression.bonusMilestones.Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves typed baseline metadata for a combo Add Scaling target, including token-backed identifiers.
    /// </summary>
    /// <param name="property">Serialized property targeted by Add Scaling.</param>
    /// <param name="valueType">Runtime formula value type.</param>
    /// <param name="baseValue">Numeric baseline when applicable.</param>
    /// <param name="baseBooleanValue">Boolean baseline when applicable.</param>
    /// <param name="isInteger">One when numeric results require integer rounding.</param>
    /// <param name="baseTokenValue">Token baseline when applicable.</param>
    /// <returns>True when the property can be represented by combo scaling metadata.</returns>
    public static bool TryResolveBaseMetadata(SerializedProperty property,
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
            baseTokenValue = new FixedString64Bytes(string.IsNullOrWhiteSpace(property.stringValue)
                ? string.Empty
                : property.stringValue.Trim());
            return true;
        }

        return PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                              out valueType,
                                                                              out baseValue,
                                                                              out baseBooleanValue,
                                                                              out isInteger);
    }

    /// <summary>
    /// Maps one stable progression key to a combo config or mode-specific reward-entry field.
    /// </summary>
    /// <param name="statKey">Stable Add Scaling stat key emitted by the progression preset.</param>
    /// <param name="entryMode">Combo topology owning a nested reward entry.</param>
    /// <param name="rankIndex">Logical rank or milestone index when the target is nested.</param>
    /// <param name="passiveUnlockIndex">Logical passive unlock index when the target is nested twice.</param>
    /// <param name="fieldId">Resolved compact runtime field identifier.</param>
    /// <returns>True when the key targets a supported combo field.</returns>
    public static bool TryMapFieldId(string statKey,
                                     out PlayerComboCounterMode entryMode,
                                     out int rankIndex,
                                     out int passiveUnlockIndex,
                                     out PlayerRuntimeComboCounterFieldId fieldId)
    {
        entryMode = PlayerComboCounterMode.Ranks;
        rankIndex = -1;
        passiveUnlockIndex = -1;
        fieldId = default;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        switch (statKey)
        {
            case "comboCounter.isEnabled":
                fieldId = PlayerRuntimeComboCounterFieldId.Enabled;
                return true;
            case "comboCounter.mode":
                fieldId = PlayerRuntimeComboCounterFieldId.Mode;
                return true;
            case "comboCounter.comboGainPerKill":
                fieldId = PlayerRuntimeComboCounterFieldId.ComboGainPerKill;
                return true;
            case "comboCounter.shieldDamageBreaksCombo":
                fieldId = PlayerRuntimeComboCounterFieldId.ShieldDamageBreaksCombo;
                return true;
            case "comboCounter.preventDecayIntoNonDecayingRanks":
                fieldId = PlayerRuntimeComboCounterFieldId.PreventDecayIntoNonDecayingRanks;
                return true;
            case "comboCounter.damageBreakMode":
                fieldId = PlayerRuntimeComboCounterFieldId.DamageBreakMode;
                return true;
            case "comboCounter.singleRankProgression.rankId":
                fieldId = PlayerRuntimeComboCounterFieldId.SingleRankId;
                return true;
            case "comboCounter.singleRankProgression.maximumComboValue":
                fieldId = PlayerRuntimeComboCounterFieldId.SingleRankMaximumComboValue;
                return true;
            case "comboCounter.singleRankProgression.pointsDecayPerSecond":
                fieldId = PlayerRuntimeComboCounterFieldId.SingleRankPointsDecayPerSecond;
                return true;
            case "comboCounter.singleRankProgression.valueDisplayMode":
                fieldId = PlayerRuntimeComboCounterFieldId.SingleRankValueDisplayMode;
                return true;
            case "comboCounter.singleRankProgression.formulaDistributionMode":
                fieldId = PlayerRuntimeComboCounterFieldId.SingleRankFormulaDistributionMode;
                return true;
        }

        if (statKey.StartsWith(ComboRanksRoot, StringComparison.Ordinal))
            return TryMapEntryField(statKey, PlayerComboCounterMode.Ranks, out entryMode, out rankIndex, out passiveUnlockIndex, out fieldId);

        if (statKey.StartsWith(SingleRankMilestonesRoot, StringComparison.Ordinal))
            return TryMapEntryField(statKey, PlayerComboCounterMode.SingleRankProgression, out entryMode, out rankIndex, out passiveUnlockIndex, out fieldId);

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Maps one rank or single-rank milestone suffix after resolving its stable array indices.
    /// </summary>
    /// <param name="statKey">Stable nested combo key.</param>
    /// <param name="mode">Topology owning the nested entry.</param>
    /// <param name="entryMode">Resolved topology output.</param>
    /// <param name="rankIndex">Resolved logical entry index.</param>
    /// <param name="passiveUnlockIndex">Resolved nested passive unlock index.</param>
    /// <param name="fieldId">Resolved compact field identifier.</param>
    /// <returns>True when the nested key suffix is supported.</returns>
    private static bool TryMapEntryField(string statKey,
                                         PlayerComboCounterMode mode,
                                         out PlayerComboCounterMode entryMode,
                                         out int rankIndex,
                                         out int passiveUnlockIndex,
                                         out PlayerRuntimeComboCounterFieldId fieldId)
    {
        entryMode = mode;
        rankIndex = -1;
        passiveUnlockIndex = -1;
        fieldId = default;

        if (!TryParseStableArrayIndex(statKey, 0, out rankIndex))
            return false;

        if (statKey.Contains(".passivePowerUpUnlocks.Array.data[", StringComparison.Ordinal))
        {
            if (!TryParseStableArrayIndex(statKey, 1, out passiveUnlockIndex))
                return false;

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

            return false;
        }

        if (mode == PlayerComboCounterMode.Ranks)
        {
            if (statKey.EndsWith(".requiredComboValue", StringComparison.Ordinal))
                fieldId = PlayerRuntimeComboCounterFieldId.RankRequiredComboValue;
            else if (statKey.EndsWith(".pointsDecayPerSecond", StringComparison.Ordinal))
                fieldId = PlayerRuntimeComboCounterFieldId.RankPointsDecayPerSecond;
            else if (statKey.EndsWith(".progressiveBoostPercent", StringComparison.Ordinal))
                fieldId = PlayerRuntimeComboCounterFieldId.RankProgressiveBoostPercent;
            else
                return false;

            return true;
        }

        if (statKey.EndsWith(".milestoneId", StringComparison.Ordinal))
            fieldId = PlayerRuntimeComboCounterFieldId.SingleRankMilestoneId;
        else if (statKey.EndsWith(".isEnabled", StringComparison.Ordinal))
            fieldId = PlayerRuntimeComboCounterFieldId.SingleRankMilestoneEnabled;
        else if (statKey.EndsWith(".requiredProgressPercent", StringComparison.Ordinal))
            fieldId = PlayerRuntimeComboCounterFieldId.SingleRankMilestoneRequiredProgressPercent;
        else
            return false;

        return true;
    }

    /// <summary>
    /// Extracts one authored list index from a stable key token such as data[2|milestoneId:Boost].
    /// </summary>
    /// <param name="statKey">Stable key containing one or more array tokens.</param>
    /// <param name="occurrenceIndex">Zero-based data token occurrence to parse.</param>
    /// <param name="arrayIndex">Parsed authored list index.</param>
    /// <returns>True when the requested token contains a numeric fallback index.</returns>
    private static bool TryParseStableArrayIndex(string statKey, int occurrenceIndex, out int arrayIndex)
    {
        arrayIndex = -1;
        int dataStartIndex = -1;
        int searchStartIndex = 0;

        for (int currentOccurrenceIndex = 0; currentOccurrenceIndex <= occurrenceIndex; currentOccurrenceIndex++)
        {
            dataStartIndex = statKey.IndexOf("data[", searchStartIndex, StringComparison.Ordinal);

            if (dataStartIndex < 0)
                return false;

            searchStartIndex = dataStartIndex + 5;
        }

        int dataEndIndex = statKey.IndexOf(']', dataStartIndex);

        if (dataEndIndex <= dataStartIndex)
            return false;

        string token = statKey.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5);
        int separatorIndex = token.IndexOf('|');
        return int.TryParse(separatorIndex >= 0 ? token.Substring(0, separatorIndex) : token, out arrayIndex);
    }
    #endregion

    #endregion
}
#endif
