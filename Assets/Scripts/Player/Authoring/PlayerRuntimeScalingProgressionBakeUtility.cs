using Unity.Collections;
using Unity.Entities;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds progression Add Scaling bake metadata used to rebuild runtime game phases after scalable-stat changes.
/// </summary>
internal static class PlayerRuntimeScalingProgressionBakeUtility
{
    #region Methods

    #region Public Methods
#if UNITY_EDITOR
    /// <summary>
    /// Populates progression scaling metadata from the unscaled progression preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source progression preset.</param>
    /// <param name="scalingBuffer">Destination scaling metadata buffer.</param>
    public static void PopulateProgressionScalingMetadata(PlayerProgressionPreset sourcePreset,
                                                          DynamicBuffer<PlayerRuntimeProgressionScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!TryMapProgressionFieldId(scalingRule.StatKey, out int phaseIndex, out PlayerRuntimeProgressionFieldId fieldId))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            float baseValue = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
            scalingBuffer.Add(new PlayerRuntimeProgressionScalingElement
            {
                PhaseIndex = phaseIndex,
                FieldId = fieldId,
                BaseValue = baseValue,
                IsInteger = property.propertyType == SerializedPropertyType.Integer ? (byte)1 : (byte)0,
                Formula = new FixedString512Bytes(scalingRule.Formula)
            });
        }
    }
#endif
    #endregion

    #region Private Methods
#if UNITY_EDITOR
    /// <summary>
    /// Maps one serialized progression stat key to the runtime progression field id plus the target phase index.
    /// </summary>
    /// <param name="statKey">Serialized stat key baked by Add Scaling.</param>
    /// <param name="phaseIndex">Resolved phase index when the mapping succeeds.</param>
    /// <param name="fieldId">Resolved runtime progression field id.</param>
    /// <returns>True when the key targets a supported progression field.</returns>
    private static bool TryMapProgressionFieldId(string statKey,
                                                 out int phaseIndex,
                                                 out PlayerRuntimeProgressionFieldId fieldId)
    {
        phaseIndex = -1;
        fieldId = default;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        if (!statKey.StartsWith("gamePhasesDefinition.Array.data[", System.StringComparison.Ordinal))
            return false;

        int dataStartIndex = statKey.IndexOf("data[", System.StringComparison.Ordinal);
        int dataEndIndex = statKey.IndexOf(']', dataStartIndex);

        if (dataStartIndex < 0 || dataEndIndex <= dataStartIndex)
            return false;

        string token = statKey.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5);
        int separatorIndex = token.IndexOf('|');
        string indexText = separatorIndex >= 0 ? token.Substring(0, separatorIndex) : token;

        if (!int.TryParse(indexText, out phaseIndex))
            return false;

        if (statKey.EndsWith(".startingRequiredLevelUpExp", System.StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeProgressionFieldId.PhaseStartingRequiredLevelUpExp;
            return true;
        }

        if (statKey.EndsWith(".requiredExperienceGrouth", System.StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeProgressionFieldId.PhaseRequiredExperienceGrouth;
            return true;
        }

        phaseIndex = -1;
        return false;
    }
#endif
    #endregion

    #endregion
}
