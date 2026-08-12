using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one combo-rank entry with scalable threshold editing and Character Tuning validation feedback.
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
        SerializedProperty formulasProperty = rankBonusesProperty.FindPropertyRelative("formulas");

        if (formulasProperty != null)
        {
            PropertyField rankBonusesField = new PropertyField(formulasProperty, "Rank Bonus Formulas");
            rankBonusesField.BindProperty(formulasProperty);
            root.Add(rankBonusesField);
        }

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

        bool progressiveBoostEnabled = progressiveBoostPercentProperty != null && progressiveBoostPercentProperty.floatValue > 0f;
        bool hasRankBonusFormulas = PlayerComboRewardEditorUtility.AppendFormulaWarnings(serializedObject,
                                                                                         rankBonusesProperty,
                                                                                         progressiveBoostEnabled,
                                                                                         warningLines);

        if (!hasRankBonusFormulas)
        {
            bool hasDecayEffect = pointsDecayPerSecondProperty != null && pointsDecayPerSecondProperty.floatValue > 0f;
            warningLines.Add(hasDecayEffect
                ? "No Character Tuning formulas configured. This rank currently changes presentation and point decay only."
                : "No Character Tuning formulas configured. This rank currently changes only presentation.");
        }

        if (progressiveBoostPercentProperty != null &&
            progressiveBoostPercentProperty.floatValue > 0f &&
            !hasRankBonusFormulas)
        {
            warningLines.Add("Progressive Boost Percent is above 0, but this rank has no Character Tuning formulas to distribute.");
        }

        PlayerComboRewardEditorUtility.AppendPassiveUnlockWarnings(passivePowerUpUnlocksProperty, warningLines);

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    #endregion

    #endregion
}
