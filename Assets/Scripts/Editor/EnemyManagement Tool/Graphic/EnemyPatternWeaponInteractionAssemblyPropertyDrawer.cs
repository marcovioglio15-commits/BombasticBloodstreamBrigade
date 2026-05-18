using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternWeaponInteractionAssembly.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternWeaponInteractionAssembly))]
public sealed class EnemyPatternWeaponInteractionAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the weapon-interaction assembly UI.
    /// </summary>
    /// <param name="property">Serialized weapon-interaction assembly property.</param>
    /// <returns>The built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty useMinimumRangeProperty = property.FindPropertyRelative("useMinimumRange");
        SerializedProperty minimumRangeProperty = property.FindPropertyRelative("minimumRange");
        SerializedProperty useMaximumRangeProperty = property.FindPropertyRelative("useMaximumRange");
        SerializedProperty maximumRangeProperty = property.FindPropertyRelative("maximumRange");
        SerializedProperty exclusiveLookDirectionControlProperty = property.FindPropertyRelative("exclusiveLookDirectionControl");
        SerializedProperty activationGatesProperty = property.FindPropertyRelative("activationGates");
        SerializedProperty maximumActivationSpeedProperty = property.FindPropertyRelative("maximumActivationSpeed");
        SerializedProperty recentlyDamagedWindowSecondsProperty = property.FindPropertyRelative("recentlyDamagedWindowSeconds");
        SerializedProperty displayBehaviourEngagementTriggerProperty = property.FindPropertyRelative("displayBehaviourEngagementTrigger");
        SerializedProperty useEngagementFeedbackOverrideProperty = property.FindPropertyRelative("useEngagementFeedbackOverride");
        SerializedProperty engagementFeedbackOverrideProperty = property.FindPropertyRelative("engagementFeedbackOverride");
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");
        SerializedProperty moduleIdProperty = bindingProperty != null ? bindingProperty.FindPropertyRelative("moduleId") : null;

        if (enabledProperty == null ||
            useMinimumRangeProperty == null ||
            minimumRangeProperty == null ||
            useMaximumRangeProperty == null ||
            maximumRangeProperty == null ||
            exclusiveLookDirectionControlProperty == null ||
            activationGatesProperty == null ||
            maximumActivationSpeedProperty == null ||
            recentlyDamagedWindowSecondsProperty == null ||
            displayBehaviourEngagementTriggerProperty == null ||
            useEngagementFeedbackOverrideProperty == null ||
            engagementFeedbackOverrideProperty == null ||
            bindingProperty == null)
        {
            Label errorLabel = new Label("Weapon Interaction assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Weapon Interaction");

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, useMinimumRangeProperty, "Use Minimum Range Gate");

        VisualElement minimumRangeContainer = new VisualElement();
        minimumRangeContainer.style.marginLeft = 12f;
        settingsContainer.Add(minimumRangeContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(minimumRangeContainer, minimumRangeProperty, "Minimum Range");

        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, useMaximumRangeProperty, "Use Maximum Range Gate");

        VisualElement maximumRangeContainer = new VisualElement();
        maximumRangeContainer.style.marginLeft = 12f;
        settingsContainer.Add(maximumRangeContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(maximumRangeContainer, maximumRangeProperty, "Maximum Range");

        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer,
                                                   exclusiveLookDirectionControlProperty,
                                                   "Exclusive Look Direction Control");
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer,
                                                   activationGatesProperty,
                                                   "Activation Gates");

        VisualElement activationGateDetailsContainer = new VisualElement();
        activationGateDetailsContainer.style.marginLeft = 12f;
        settingsContainer.Add(activationGateDetailsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(activationGateDetailsContainer,
                                                   maximumActivationSpeedProperty,
                                                   "Maximum Activation Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(activationGateDetailsContainer,
                                                   recentlyDamagedWindowSecondsProperty,
                                                   "Recently Damaged Window Seconds");

        PropertyField bindingField = new PropertyField(bindingProperty, "Weapon Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Binding selected after the shared minimum and maximum range gates defined in this assembly evaluate true.";
        settingsContainer.Add(bindingField);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer,
                                                   displayBehaviourEngagementTriggerProperty,
                                                   "Display Behaviour Engagement Trigger");

        HelpBox unsupportedModuleBox = new HelpBox("Display Behaviour Engagement Trigger supports Shooter and Power-Up Stealer in the Weapon Interaction slot.", HelpBoxMessageType.Warning);
        unsupportedModuleBox.style.marginTop = 4f;
        settingsContainer.Add(unsupportedModuleBox);

        VisualElement feedbackOptionsContainer = new VisualElement();
        feedbackOptionsContainer.style.marginLeft = 12f;
        settingsContainer.Add(feedbackOptionsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(feedbackOptionsContainer,
                                                   useEngagementFeedbackOverrideProperty,
                                                   "Use Engagement Feedback Override");

        VisualElement feedbackOverrideContainer = new VisualElement();
        feedbackOverrideContainer.style.marginLeft = 12f;
        feedbackOptionsContainer.Add(feedbackOverrideContainer);
        feedbackOverrideContainer.Add(EnemyOffensiveEngagementFeedbackDrawerUtility.BuildSettingsEditor(engagementFeedbackOverrideProperty));

        UpdateVisibility(enabledProperty,
                         useMinimumRangeProperty,
                         useMaximumRangeProperty,
                         displayBehaviourEngagementTriggerProperty,
                         useEngagementFeedbackOverrideProperty,
                         activationGatesProperty,
                         bindingProperty,
                         settingsContainer,
                         minimumRangeContainer,
                         maximumRangeContainer,
                         feedbackOptionsContainer,
                         feedbackOverrideContainer,
                         unsupportedModuleBox,
                         activationGateDetailsContainer,
                         maximumActivationSpeedProperty,
                         recentlyDamagedWindowSecondsProperty);
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            UpdateVisibility(changedProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             activationGatesProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox,
                             activationGateDetailsContainer,
                             maximumActivationSpeedProperty,
                             recentlyDamagedWindowSecondsProperty);
        });
        root.TrackPropertyValue(useMinimumRangeProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             changedProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             activationGatesProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox,
                             activationGateDetailsContainer,
                             maximumActivationSpeedProperty,
                             recentlyDamagedWindowSecondsProperty);
        });
        root.TrackPropertyValue(useMaximumRangeProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             changedProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             activationGatesProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox,
                             activationGateDetailsContainer,
                             maximumActivationSpeedProperty,
                             recentlyDamagedWindowSecondsProperty);
        });
        root.TrackPropertyValue(activationGatesProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             activationGatesProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox,
                             activationGateDetailsContainer,
                             maximumActivationSpeedProperty,
                             recentlyDamagedWindowSecondsProperty);
        });
        root.TrackPropertyValue(displayBehaviourEngagementTriggerProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             changedProperty,
                             useEngagementFeedbackOverrideProperty,
                             activationGatesProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox,
                             activationGateDetailsContainer,
                             maximumActivationSpeedProperty,
                             recentlyDamagedWindowSecondsProperty);
        });
        root.TrackPropertyValue(useEngagementFeedbackOverrideProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             changedProperty,
                             activationGatesProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox,
                             activationGateDetailsContainer,
                             maximumActivationSpeedProperty,
                             recentlyDamagedWindowSecondsProperty);
        });

        if (moduleIdProperty != null)
        {
            root.TrackPropertyValue(moduleIdProperty, changedProperty =>
            {
                UpdateVisibility(enabledProperty,
                                 useMinimumRangeProperty,
                                 useMaximumRangeProperty,
                                 displayBehaviourEngagementTriggerProperty,
                                 useEngagementFeedbackOverrideProperty,
                                 activationGatesProperty,
                                 bindingProperty,
                                 settingsContainer,
                                 minimumRangeContainer,
                                 maximumRangeContainer,
                                 feedbackOptionsContainer,
                                 feedbackOverrideContainer,
                                 unsupportedModuleBox,
                                 activationGateDetailsContainer,
                                 maximumActivationSpeedProperty,
                                 recentlyDamagedWindowSecondsProperty);
            });
        }

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates nested settings visibility, range-gated fields, and unsupported-module warnings from the current toggle state.
    /// </summary>
    /// <param name="enabledProperty">Serialized enabled property.</param>
    /// <param name="useMinimumRangeProperty">Serialized minimum-range toggle.</param>
    /// <param name="useMaximumRangeProperty">Serialized maximum-range toggle.</param>
    /// <param name="displayTriggerProperty">Serialized trigger toggle.</param>
    /// <param name="useOverrideProperty">Serialized override toggle.</param>
    /// <param name="bindingProperty">Serialized module binding.</param>
    /// <param name="settingsContainer">Main nested settings container.</param>
    /// <param name="minimumRangeContainer">Nested minimum-range field container.</param>
    /// <param name="maximumRangeContainer">Nested maximum-range field container.</param>
    /// <param name="feedbackOptionsContainer">Nested feedback options container.</param>
    /// <param name="feedbackOverrideContainer">Nested override settings container.</param>
    /// <param name="unsupportedModuleBox">Warning box shown for unsupported module kinds.</param>
    private static void UpdateVisibility(SerializedProperty enabledProperty,
                                         SerializedProperty useMinimumRangeProperty,
                                         SerializedProperty useMaximumRangeProperty,
                                         SerializedProperty displayTriggerProperty,
                                         SerializedProperty useOverrideProperty,
                                         SerializedProperty activationGatesProperty,
                                         SerializedProperty bindingProperty,
                                         VisualElement settingsContainer,
                                         VisualElement minimumRangeContainer,
                                         VisualElement maximumRangeContainer,
                                         VisualElement feedbackOptionsContainer,
                                         VisualElement feedbackOverrideContainer,
                                         HelpBox unsupportedModuleBox,
                                         VisualElement activationGateDetailsContainer,
                                         SerializedProperty maximumActivationSpeedProperty,
                                         SerializedProperty recentlyDamagedWindowSecondsProperty)
    {
        bool isInteractionEnabled = enabledProperty != null && enabledProperty.boolValue;
        bool useMinimumRange = isInteractionEnabled &&
                               useMinimumRangeProperty != null &&
                               useMinimumRangeProperty.boolValue;
        bool useMaximumRange = isInteractionEnabled &&
                               useMaximumRangeProperty != null &&
                               useMaximumRangeProperty.boolValue;
        bool isTriggerEnabled = isInteractionEnabled &&
                                displayTriggerProperty != null &&
                                displayTriggerProperty.boolValue;
        bool isOverrideEnabled = isTriggerEnabled &&
                                 useOverrideProperty != null &&
                                 useOverrideProperty.boolValue;
        bool showUnsupportedModuleWarning = isTriggerEnabled &&
                                            !EnemyOffensiveEngagementFeedbackDrawerUtility.SupportsDisplayTrigger(bindingProperty,
                                                                                                                 EnemyPatternModuleCatalogSection.WeaponInteraction);
        EnemyWeaponInteractionActivationGate gates = ResolveActivationGates(activationGatesProperty);
        bool showActivationGateDetails = isInteractionEnabled && gates != EnemyWeaponInteractionActivationGate.Always;

        if (settingsContainer != null)
        {
            settingsContainer.style.display = isInteractionEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (minimumRangeContainer != null)
        {
            minimumRangeContainer.style.display = useMinimumRange
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (maximumRangeContainer != null)
        {
            maximumRangeContainer.style.display = useMaximumRange
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (feedbackOptionsContainer != null)
        {
            feedbackOptionsContainer.style.display = isTriggerEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (feedbackOverrideContainer != null)
        {
            feedbackOverrideContainer.style.display = isOverrideEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (unsupportedModuleBox != null)
        {
            unsupportedModuleBox.style.display = showUnsupportedModuleWarning
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (activationGateDetailsContainer != null)
        {
            activationGateDetailsContainer.style.display = showActivationGateDetails
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Resolves activation gates from the serialized enum property.
    /// </summary>
    /// <param name="activationGatesProperty">Serialized activation gates property.</param>
    /// <returns>Current activation gate flags.</returns>
    private static EnemyWeaponInteractionActivationGate ResolveActivationGates(SerializedProperty activationGatesProperty)
    {
        if (activationGatesProperty == null || activationGatesProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyWeaponInteractionActivationGate.Always;

        return (EnemyWeaponInteractionActivationGate)activationGatesProperty.intValue;
    }
    #endregion

    #endregion
}
