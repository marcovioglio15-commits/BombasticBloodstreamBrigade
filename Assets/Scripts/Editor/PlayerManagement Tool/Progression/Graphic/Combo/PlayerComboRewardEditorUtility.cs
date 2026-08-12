using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Provides shared formula and passive-unlock validation for rank and single-rank combo reward entries.
/// </summary>
internal static class PlayerComboRewardEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends unified Character Tuning validation warnings for one combo reward payload.
    /// </summary>
    /// <param name="serializedObject">Serialized progression preset owning the reward.</param>
    /// <param name="bonusesProperty">Serialized Character Tuning module payload.</param>
    /// <param name="usesPartialNumericWeight">True when numeric formulas can be partially blended before full activation.</param>
    /// <param name="warningLines">Destination warning line list.</param>
    /// <returns>True when at least one authored formula entry exists.</returns>
    public static bool AppendFormulaWarnings(SerializedObject serializedObject,
                                             SerializedProperty bonusesProperty,
                                             bool usesPartialNumericWeight,
                                             List<string> warningLines)
    {
        SerializedProperty formulasProperty = bonusesProperty != null
            ? bonusesProperty.FindPropertyRelative("formulas")
            : null;

        if (serializedObject == null || formulasProperty == null || !formulasProperty.isArray)
        {
            warningLines.Add("Character Tuning formulas are not available.");
            return false;
        }

        if (formulasProperty.arraySize <= 0)
            return false;

        HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject);
        Dictionary<string, PlayerFormulaValueType> variableTypes = PlayerScalingFormulaValidationUtility.BuildScopedVariableTypeMap(serializedObject);
        Dictionary<string, PlayerScalableStatType> scalableStatTypes = PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject);

        for (int formulaIndex = 0; formulaIndex < formulasProperty.arraySize; formulaIndex++)
        {
            SerializedProperty formulaEntryProperty = formulasProperty.GetArrayElementAtIndex(formulaIndex);
            SerializedProperty formulaProperty = formulaEntryProperty != null
                ? formulaEntryProperty.FindPropertyRelative("formula")
                : null;

            if (formulaProperty == null)
            {
                warningLines.Add(string.Format("Formula #{0} payload is invalid.", formulaIndex + 1));
                continue;
            }

            string formulaValue = formulaProperty.stringValue;

            if (string.IsNullOrWhiteSpace(formulaValue))
            {
                warningLines.Add(string.Format("Formula #{0} is empty.", formulaIndex + 1));
                continue;
            }

            if (usesPartialNumericWeight &&
                PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formulaValue,
                                                                              out string targetStatName,
                                                                              out string _,
                                                                              out string _) &&
                scalableStatTypes.TryGetValue(targetStatName, out PlayerScalableStatType targetStatType) &&
                (targetStatType == PlayerScalableStatType.Boolean || targetStatType == PlayerScalableStatType.Token))
            {
                warningLines.Add(string.Format("Formula #{0} targets non-numeric stat '{1}'. Partial combo progression affects numeric formulas only; this formula activates at full weight.",
                                               formulaIndex + 1,
                                               targetStatName));
            }

            if (!PlayerCharacterTuningFormulaValidationUtility.TryValidateAssignmentFormula(formulaValue,
                                                                                              allowedVariables,
                                                                                              variableTypes,
                                                                                              out string warningMessage))
            {
                warningLines.Add(string.Format("Formula #{0}: {1}", formulaIndex + 1, warningMessage));
            }
        }

        return true;
    }

    /// <summary>
    /// Appends warnings for temporary passive unlocks that cannot resolve in the scoped Power-Ups preset.
    /// </summary>
    /// <param name="passivePowerUpUnlocksProperty">Serialized passive unlock list.</param>
    /// <param name="warningLines">Destination warning line list.</param>
    public static void AppendPassiveUnlockWarnings(SerializedProperty passivePowerUpUnlocksProperty,
                                                   List<string> warningLines)
    {
        if (passivePowerUpUnlocksProperty == null || !passivePowerUpUnlocksProperty.isArray)
        {
            warningLines.Add("Passive Power-Up Unlocks are not available.");
            return;
        }

        if (passivePowerUpUnlocksProperty.arraySize <= 0)
            return;

        HashSet<string> passivePowerUpIds = BuildScopedPassivePowerUpIdSet();

        if (passivePowerUpIds.Count <= 0)
            warningLines.Add("Passive unlocks are configured, but no passive PowerUpId is available from the scoped Power-Ups preset.");

        for (int unlockIndex = 0; unlockIndex < passivePowerUpUnlocksProperty.arraySize; unlockIndex++)
        {
            SerializedProperty unlockProperty = passivePowerUpUnlocksProperty.GetArrayElementAtIndex(unlockIndex);
            SerializedProperty isEnabledProperty = unlockProperty != null ? unlockProperty.FindPropertyRelative("isEnabled") : null;
            SerializedProperty passivePowerUpIdProperty = unlockProperty != null ? unlockProperty.FindPropertyRelative("passivePowerUpId") : null;
            bool isEnabled = isEnabledProperty == null || isEnabledProperty.boolValue;
            string passivePowerUpId = passivePowerUpIdProperty != null && !string.IsNullOrWhiteSpace(passivePowerUpIdProperty.stringValue)
                ? passivePowerUpIdProperty.stringValue.Trim()
                : string.Empty;

            if (!isEnabled)
                continue;

            if (string.IsNullOrWhiteSpace(passivePowerUpId))
            {
                warningLines.Add(string.Format("Passive unlock #{0} is enabled but has no Passive PowerUpId.", unlockIndex + 1));
                continue;
            }

            if (passivePowerUpIds.Count > 0 && !passivePowerUpIds.Contains(passivePowerUpId))
            {
                warningLines.Add(string.Format("Passive unlock #{0} references '{1}', which is not a passive PowerUpId in the scoped Power-Ups preset.",
                                               unlockIndex + 1,
                                               passivePowerUpId));
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the passive PowerUpId set from the currently scoped Power-Ups preset.
    /// </summary>
    /// <returns>Case-insensitive passive PowerUpId set.</returns>
    private static HashSet<string> BuildScopedPassivePowerUpIdSet()
    {
        HashSet<string> passivePowerUpIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        if (!PlayerProgressionTierOptionsUtility.TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset scopedPreset))
            return passivePowerUpIds;

        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = scopedPreset.PassivePowerUps;

        if (passivePowerUps == null)
            return passivePowerUpIds;

        for (int powerUpIndex = 0; powerUpIndex < passivePowerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition passivePowerUp = passivePowerUps[powerUpIndex];

            if (passivePowerUp == null || passivePowerUp.CommonData == null || string.IsNullOrWhiteSpace(passivePowerUp.CommonData.PowerUpId))
                continue;

            passivePowerUpIds.Add(passivePowerUp.CommonData.PowerUpId.Trim());
        }

        return passivePowerUpIds;
    }
    #endregion

    #endregion
}
