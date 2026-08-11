using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one combo-rank entry with scalable threshold editing and Character Tuning validation feedback.
/// none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerComboRankDefinition))]
public sealed class PlayerComboRankDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float AvailableVariablesBoxHeight = 76f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one combo-rank entry.
    /// </summary>
    /// <param name="property">Serialized combo-rank property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty rankIdProperty = property.FindPropertyRelative("rankId");
        SerializedProperty requiredComboValueProperty = property.FindPropertyRelative("requiredComboValue");
        SerializedProperty pointsDecayPerSecondProperty = property.FindPropertyRelative("pointsDecayPerSecond");
        SerializedProperty progressiveBoostPercentProperty = property.FindPropertyRelative("progressiveBoostPercent");
        SerializedProperty rankBonusesProperty = property.FindPropertyRelative("rankBonuses");
        SerializedProperty passivePowerUpUnlocksProperty = property.FindPropertyRelative("passivePowerUpUnlocks");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (rankIdProperty == null ||
            requiredComboValueProperty == null ||
            pointsDecayPerSecondProperty == null ||
            progressiveBoostPercentProperty == null ||
            rankBonusesProperty == null ||
            passivePowerUpUnlocksProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo rank fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox infoBox = new HelpBox("Each reached rank applies its Character Tuning formulas cumulatively together with all lower ranks that are still reached. Progressive Boost Percent distributes part of this rank's numeric formulas before the threshold is reached, while passive unlocks stay active only while the owning rank remains reached.", HelpBoxMessageType.Info);
        root.Add(infoBox);
        root.Add(CreateBoundField(rankIdProperty, "Rank ID"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(requiredComboValueProperty,
                                                              scalingRulesProperty,
                                                              "Required Combo Value"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(pointsDecayPerSecondProperty,
                                                              scalingRulesProperty,
                                                              "Points Decay Per Second"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(progressiveBoostPercentProperty,
                                                              scalingRulesProperty,
                                                              "Progressive Boost Percent"));
        PropertyField rankBonusesField = new PropertyField(rankBonusesProperty, "Rank Bonuses");
        rankBonusesField.BindProperty(rankBonusesProperty);
        root.Add(rankBonusesField);

        PropertyField passiveUnlocksField = new PropertyField(passivePowerUpUnlocksProperty, "Passive Power-Up Unlocks");
        passiveUnlocksField.BindProperty(passivePowerUpUnlocksProperty);
        root.Add(passiveUnlocksField);

        ScrollView availableVariablesScrollView = CreateAvailableVariablesScrollView();
        Label availableVariablesLabel = CreateAvailableVariablesLabel();
        availableVariablesScrollView.Add(availableVariablesLabel);
        root.Add(availableVariablesScrollView);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshAvailableVariables(property.serializedObject, availableVariablesLabel);
            RefreshWarnings(property.serializedObject,
                            rankIdProperty,
                            requiredComboValueProperty,
                            pointsDecayPerSecondProperty,
                            progressiveBoostPercentProperty,
                            rankBonusesProperty,
                            passivePowerUpUnlocksProperty,
                            warningBox);
        });

        RefreshAvailableVariables(property.serializedObject, availableVariablesLabel);
        RefreshWarnings(property.serializedObject,
                        rankIdProperty,
                        requiredComboValueProperty,
                        pointsDecayPerSecondProperty,
                        progressiveBoostPercentProperty,
                        rankBonusesProperty,
                        passivePowerUpUnlocksProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one bound property field with the requested display label.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Inspector label shown for the bound field.</param>
    /// <returns>Configured property field bound to the serialized property.</returns>
    private static PropertyField CreateBoundField(SerializedProperty property, string label)
    {
        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        return propertyField;
    }

    /// <summary>
    /// Builds the scroll view that hosts the Available Variables helper text for combo rank formulas.
    /// none.
    /// </summary>
    /// <returns>Configured scroll view used by the inspector.</returns>
    private static ScrollView CreateAvailableVariablesScrollView()
    {
        ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.marginTop = 2f;
        scrollView.style.height = AvailableVariablesBoxHeight;
        scrollView.style.maxHeight = AvailableVariablesBoxHeight;
        scrollView.style.flexShrink = 0f;
        return scrollView;
    }

    /// <summary>
    /// Builds the label that shows the currently available scalable-stat variables for combo rank formulas.
    /// none.
    /// </summary>
    /// <returns>Configured label used by the inspector.</returns>
    private static Label CreateAvailableVariablesLabel()
    {
        Label label = new Label(string.Empty);
        label.style.unityFontStyleAndWeight = FontStyle.Italic;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexShrink = 0f;
        return label;
    }

    /// <summary>
    /// Refreshes the helper label that lists the scalable-stat variables available to combo rank formulas.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the combo rank.</param>
    /// <param name="availableVariablesLabel">Label refreshed in place.</param>
    private static void RefreshAvailableVariables(SerializedObject serializedObject, Label availableVariablesLabel)
    {
        if (availableVariablesLabel == null)
        {
            return;
        }

        HashSet<string> allowedVariables = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject)
            : new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PlayerScalableStatType> variableTypes = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject)
            : new Dictionary<string, PlayerScalableStatType>(System.StringComparer.OrdinalIgnoreCase);
        availableVariablesLabel.text = PlayerScalingFormulaValidationUtility.BuildAvailableVariablesLabelText(allowedVariables, variableTypes);
    }

    /// <summary>
    /// Rebuilds the warning message shown for one combo rank.
    /// </summary>
    /// <param name="serializedObject">Serialized object owning the combo rank.</param>
    /// <param name="rankIdProperty">Serialized rank identifier property.</param>
    /// <param name="requiredComboValueProperty">Serialized combo threshold property.</param>
    /// <param name="pointsDecayPerSecondProperty">Serialized time-based combo point decay property.</param>
    /// <param name="progressiveBoostPercentProperty">Serialized progressive boost distribution percentage.</param>
    /// <param name="rankBonusesProperty">Serialized Character Tuning payload property.</param>
    /// <param name="passivePowerUpUnlocksProperty">Serialized passive power-up unlock list property.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshWarnings(SerializedObject serializedObject,
                                        SerializedProperty rankIdProperty,
                                        SerializedProperty requiredComboValueProperty,
                                        SerializedProperty pointsDecayPerSecondProperty,
                                        SerializedProperty progressiveBoostPercentProperty,
                                        SerializedProperty rankBonusesProperty,
                                        SerializedProperty passivePowerUpUnlocksProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
        {
            return;
        }

        List<string> warningLines = new List<string>();
        string rankId = rankIdProperty != null ? rankIdProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(rankId))
        {
            warningLines.Add("Rank ID should not be empty.");
        }

        if (requiredComboValueProperty != null && requiredComboValueProperty.intValue < 0)
        {
            warningLines.Add("Required Combo Value should be >= 0.");
        }

        if (pointsDecayPerSecondProperty != null && pointsDecayPerSecondProperty.floatValue < 0f)
        {
            warningLines.Add("Points Decay Per Second should be >= 0.");
        }

        if (progressiveBoostPercentProperty != null &&
            (progressiveBoostPercentProperty.floatValue < 0f || progressiveBoostPercentProperty.floatValue > 100f))
        {
            warningLines.Add("Progressive Boost Percent should stay between 0 and 100. Runtime clamps the applied share, but the authored value is not snapped.");
        }

        SerializedProperty formulasProperty = rankBonusesProperty != null
            ? rankBonusesProperty.FindPropertyRelative("formulas")
            : null;
        bool hasRankBonusFormulas = formulasProperty != null && formulasProperty.isArray && formulasProperty.arraySize > 0;

        if (serializedObject == null || formulasProperty == null || !formulasProperty.isArray)
        {
            warningLines.Add("Rank Bonuses formulas are not available.");
        }
        else if (formulasProperty.arraySize <= 0)
        {
            bool hasDecayEffect = pointsDecayPerSecondProperty != null && pointsDecayPerSecondProperty.floatValue > 0f;
            warningLines.Add(hasDecayEffect
                ? "No Character Tuning formulas configured. This rank currently changes presentation and point decay only."
                : "No Character Tuning formulas configured. This rank currently changes only presentation.");
        }
        else
        {
            HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject);
            Dictionary<string, PlayerFormulaValueType> variableTypes = PlayerScalingFormulaValidationUtility.BuildScopedVariableTypeMap(serializedObject);
            Dictionary<string, PlayerScalableStatType> scalableStatTypes = PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject);
            bool progressiveBoostEnabled = progressiveBoostPercentProperty != null && progressiveBoostPercentProperty.floatValue > 0f;

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

                if (progressiveBoostEnabled &&
                    PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formulaValue,
                                                                                  out string targetStatName,
                                                                                  out string _,
                                                                                  out string _) &&
                    scalableStatTypes.TryGetValue(targetStatName, out PlayerScalableStatType targetStatType) &&
                    (targetStatType == PlayerScalableStatType.Boolean || targetStatType == PlayerScalableStatType.Token))
                {
                    warningLines.Add(string.Format("Formula #{0} targets non-numeric stat '{1}'. Progressive Boost Percent affects numeric Character Tuning formulas only; this formula applies at the actual rank-up.", formulaIndex + 1, targetStatName));
                }

                if (PlayerCharacterTuningFormulaValidationUtility.TryValidateAssignmentFormula(formulaValue,
                                                                                              allowedVariables,
                                                                                              variableTypes,
                                                                                              out string warningMessage))
                {
                    continue;
                }

                warningLines.Add(string.Format("Formula #{0}: {1}", formulaIndex + 1, warningMessage));
            }
        }

        if (progressiveBoostPercentProperty != null &&
            progressiveBoostPercentProperty.floatValue > 0f &&
            !hasRankBonusFormulas)
        {
            warningLines.Add("Progressive Boost Percent is above 0, but this rank has no Character Tuning formulas to distribute.");
        }

        AppendPassiveUnlockWarnings(passivePowerUpUnlocksProperty, warningLines);

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Adds warnings for passive unlock entries that cannot resolve against the scoped Power-Ups preset.
    /// </summary>
    /// <param name="passivePowerUpUnlocksProperty">Serialized passive unlock list property.</param>
    /// <param name="warningLines">Destination warning line list.</param>
    private static void AppendPassiveUnlockWarnings(SerializedProperty passivePowerUpUnlocksProperty, List<string> warningLines)
    {
        if (passivePowerUpUnlocksProperty == null || !passivePowerUpUnlocksProperty.isArray)
        {
            warningLines.Add("Passive Power-Up Unlocks are not available.");
            return;
        }

        if (passivePowerUpUnlocksProperty.arraySize <= 0)
        {
            return;
        }

        HashSet<string> passivePowerUpIds = BuildScopedPassivePowerUpIdSet();

        if (passivePowerUpIds.Count <= 0)
        {
            warningLines.Add("Passive unlocks are configured, but no passive PowerUpId is available from the scoped Power-Ups preset.");
        }

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
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(passivePowerUpId))
            {
                warningLines.Add(string.Format("Passive unlock #{0} is enabled but has no Passive PowerUpId.", unlockIndex + 1));
                continue;
            }

            if (passivePowerUpIds.Count > 0 && !passivePowerUpIds.Contains(passivePowerUpId))
            {
                warningLines.Add(string.Format("Passive unlock #{0} references '{1}', which is not a passive PowerUpId in the scoped Power-Ups preset.", unlockIndex + 1, passivePowerUpId));
            }
        }
    }

    /// <summary>
    /// Builds the passive PowerUpId set from the currently scoped Power-Ups preset.
    /// none.
    /// </summary>
    /// <returns>Case-insensitive passive PowerUpId set.</returns>
    private static HashSet<string> BuildScopedPassivePowerUpIdSet()
    {
        HashSet<string> passivePowerUpIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        if (!PlayerProgressionTierOptionsUtility.TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset scopedPreset))
        {
            return passivePowerUpIds;
        }

        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = scopedPreset.PassivePowerUps;

        if (passivePowerUps == null)
        {
            return passivePowerUpIds;
        }

        for (int powerUpIndex = 0; powerUpIndex < passivePowerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition passivePowerUp = passivePowerUps[powerUpIndex];

            if (passivePowerUp == null || passivePowerUp.CommonData == null || string.IsNullOrWhiteSpace(passivePowerUp.CommonData.PowerUpId))
            {
                continue;
            }

            passivePowerUpIds.Add(passivePowerUp.CommonData.PowerUpId.Trim());
        }

        return passivePowerUpIds;
    }
    #endregion

    #endregion
}
