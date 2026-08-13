using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one single-rank bonus milestone with scalable identity, enablement, percentage, and reward validation.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerComboBonusMilestoneDefinition))]
public sealed class PlayerComboBonusMilestoneDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float AvailableVariablesBoxHeight = 76f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit editor for one percentage-based single-rank reward milestone.
    /// </summary>
    /// <param name="property">Serialized bonus milestone property.</param>
    /// <returns>Root visual element bound to the milestone.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty milestoneIdProperty = property.FindPropertyRelative("milestoneId");
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty requiredProgressPercentProperty = property.FindPropertyRelative("requiredProgressPercent");
        SerializedProperty bonusesProperty = property.FindPropertyRelative("bonuses");
        SerializedProperty passivePowerUpUnlocksProperty = property.FindPropertyRelative("passivePowerUpUnlocks");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (milestoneIdProperty == null ||
            isEnabledProperty == null ||
            requiredProgressPercentProperty == null ||
            bonusesProperty == null ||
            passivePowerUpUnlocksProperty == null)
        {
            root.Add(new HelpBox("Single-rank bonus milestone fields are missing.", HelpBoxMessageType.Warning));
            return root;
        }

        root.Add(new HelpBox("Milestone rewards remain cumulative while progression stays above their percentage. Linear numeric formulas use the interval selected by Single Rank Progression, while passive unlocks still activate directly at their milestone percentage.", HelpBoxMessageType.Info));
        root.Add(PlayerScalingFieldElementFactory.CreateField(milestoneIdProperty,
                                                              scalingRulesProperty,
                                                              "Milestone ID",
                                                              null,
                                                              true));
        root.Add(PlayerScalingFieldElementFactory.CreateField(isEnabledProperty,
                                                              scalingRulesProperty,
                                                              "Enabled"));

        VisualElement enabledOptions = new VisualElement();
        enabledOptions.style.marginLeft = 10f;
        enabledOptions.Add(PlayerScalingFieldElementFactory.CreateField(requiredProgressPercentProperty,
                                                                        scalingRulesProperty,
                                                                        "Required Progress Percent"));
        SerializedProperty formulasProperty = bonusesProperty.FindPropertyRelative("formulas");

        if (formulasProperty != null)
        {
            PropertyField formulasField = new PropertyField(formulasProperty, "Bonus Formulas");
            formulasField.BindProperty(formulasProperty);
            enabledOptions.Add(formulasField);
        }

        PropertyField passiveUnlocksField = new PropertyField(passivePowerUpUnlocksProperty, "Passive Power-Up Unlocks");
        passiveUnlocksField.BindProperty(passivePowerUpUnlocksProperty);
        enabledOptions.Add(passiveUnlocksField);
        root.Add(enabledOptions);

        ScrollView availableVariablesScrollView = new ScrollView(ScrollViewMode.Vertical);
        availableVariablesScrollView.style.marginTop = 2f;
        availableVariablesScrollView.style.height = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.maxHeight = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.flexShrink = 0f;
        Label availableVariablesLabel = new Label();
        availableVariablesLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        availableVariablesLabel.style.whiteSpace = WhiteSpace.Normal;
        availableVariablesScrollView.Add(availableVariablesLabel);
        enabledOptions.Add(availableVariablesScrollView);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);
        RefreshEnabledVisibility(isEnabledProperty, enabledOptions);
        RefreshAvailableVariables(property.serializedObject, availableVariablesLabel);
        RefreshWarnings(property.serializedObject,
                        milestoneIdProperty,
                        isEnabledProperty,
                        requiredProgressPercentProperty,
                        bonusesProperty,
                        passivePowerUpUnlocksProperty,
                        warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshEnabledVisibility(isEnabledProperty, enabledOptions);
            RefreshAvailableVariables(property.serializedObject, availableVariablesLabel);
            RefreshWarnings(property.serializedObject,
                            milestoneIdProperty,
                            isEnabledProperty,
                            requiredProgressPercentProperty,
                            bonusesProperty,
                            passivePowerUpUnlocksProperty,
                            warningBox);
        });
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Shows reward settings only while the milestone is authored as enabled.
    /// </summary>
    /// <param name="isEnabledProperty">Serialized milestone enable flag.</param>
    /// <param name="enabledOptions">Container holding percentage and reward settings.</param>
    private static void RefreshEnabledVisibility(SerializedProperty isEnabledProperty, VisualElement enabledOptions)
    {
        enabledOptions.style.display = isEnabledProperty != null && isEnabledProperty.boolValue
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Refreshes the available scalable-stat variables shown beside milestone formulas.
    /// </summary>
    /// <param name="serializedObject">Serialized progression preset owning the milestone.</param>
    /// <param name="availableVariablesLabel">Label updated with scoped variables.</param>
    private static void RefreshAvailableVariables(SerializedObject serializedObject, Label availableVariablesLabel)
    {
        HashSet<string> allowedVariables = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject)
            : new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PlayerScalableStatType> variableTypes = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject)
            : new Dictionary<string, PlayerScalableStatType>(System.StringComparer.OrdinalIgnoreCase);
        availableVariablesLabel.text = PlayerScalingFormulaValidationUtility.BuildAvailableVariablesLabelText(allowedVariables, variableTypes);
    }

    /// <summary>
    /// Rebuilds non-mutating milestone warnings using the current single-rank formula distribution mode.
    /// </summary>
    /// <param name="serializedObject">Serialized progression preset owning the milestone.</param>
    /// <param name="milestoneIdProperty">Serialized stable milestone identifier.</param>
    /// <param name="isEnabledProperty">Serialized milestone enable flag.</param>
    /// <param name="requiredProgressPercentProperty">Serialized activation percentage.</param>
    /// <param name="bonusesProperty">Serialized Character Tuning payload.</param>
    /// <param name="passivePowerUpUnlocksProperty">Serialized temporary passive unlock list.</param>
    /// <param name="warningBox">Warning help box refreshed in place.</param>
    private static void RefreshWarnings(SerializedObject serializedObject,
                                        SerializedProperty milestoneIdProperty,
                                        SerializedProperty isEnabledProperty,
                                        SerializedProperty requiredProgressPercentProperty,
                                        SerializedProperty bonusesProperty,
                                        SerializedProperty passivePowerUpUnlocksProperty,
                                        HelpBox warningBox)
    {
        List<string> warningLines = new List<string>();

        if (string.IsNullOrWhiteSpace(milestoneIdProperty.stringValue))
            warningLines.Add("Milestone ID should not be empty.");

        if (!isEnabledProperty.boolValue)
        {
            SetWarnings(warningLines, warningBox);
            return;
        }

        if (float.IsNaN(requiredProgressPercentProperty.floatValue) ||
            float.IsInfinity(requiredProgressPercentProperty.floatValue) ||
            requiredProgressPercentProperty.floatValue < 0f ||
            requiredProgressPercentProperty.floatValue > 100f)
            warningLines.Add("Required Progress Percent should be finite and stay between 0 and 100. Runtime constrains the effective threshold without changing the authored value.");

        bool usesLinearDistribution = serializedObject != null &&
                                      serializedObject.FindProperty("comboCounter.singleRankProgression.formulaDistributionMode")?.enumValueIndex ==
                                      (int)PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression;
        bool hasFormulas = PlayerComboRewardEditorUtility.AppendFormulaWarnings(serializedObject,
                                                                                 bonusesProperty,
                                                                                 usesLinearDistribution,
                                                                                 warningLines);

        if (!hasFormulas && passivePowerUpUnlocksProperty.arraySize <= 0)
            warningLines.Add("This enabled milestone has no Character Tuning formulas or passive unlocks.");

        PlayerComboRewardEditorUtility.AppendPassiveUnlockWarnings(passivePowerUpUnlocksProperty, warningLines);
        SetWarnings(warningLines, warningBox);
    }

    /// <summary>
    /// Applies collected warning lines to one reusable help box.
    /// </summary>
    /// <param name="warningLines">Collected warning messages.</param>
    /// <param name="warningBox">Warning help box updated in place.</param>
    private static void SetWarnings(List<string> warningLines, HelpBox warningBox)
    {
        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = warningLines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
