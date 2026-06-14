using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternShortRangeInteractionAssembly.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternShortRangeInteractionAssembly))]
public sealed class EnemyPatternShortRangeInteractionAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the short-range interaction assembly UI.
    /// </summary>
    /// <param name="property">Serialized short-range assembly property.</param>
    /// <returns>The built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty activationRangeProperty = property.FindPropertyRelative("activationRange");
        SerializedProperty releaseDistanceBufferProperty = property.FindPropertyRelative("releaseDistanceBuffer");
        SerializedProperty displayBehaviourEngagementTriggerProperty = property.FindPropertyRelative("displayBehaviourEngagementTrigger");
        SerializedProperty useEngagementFeedbackOverrideProperty = property.FindPropertyRelative("useEngagementFeedbackOverride");
        SerializedProperty engagementFeedbackOverrideProperty = property.FindPropertyRelative("engagementFeedbackOverride");
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");
        SerializedProperty moduleIdProperty = bindingProperty != null ? bindingProperty.FindPropertyRelative("moduleId") : null;

        if (enabledProperty == null ||
            activationRangeProperty == null ||
            releaseDistanceBufferProperty == null ||
            displayBehaviourEngagementTriggerProperty == null ||
            useEngagementFeedbackOverrideProperty == null ||
            engagementFeedbackOverrideProperty == null ||
            bindingProperty == null)
        {
            Label errorLabel = new Label("Short-Range Interaction assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Short-Range Interaction");

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, activationRangeProperty, "Activation Range");
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, releaseDistanceBufferProperty, "Release Distance Buffer");

        PropertyField bindingField = new PropertyField(bindingProperty, "Short-Range Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Binding selected only while the player stays inside Activation Range plus Release Distance Buffer.";
        settingsContainer.Add(bindingField);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer,
                                                   displayBehaviourEngagementTriggerProperty,
                                                   "Display Behaviour Engagement Trigger");

        HelpBox unsupportedModuleBox = new HelpBox("Display Behaviour Engagement Trigger currently supports only ShortRangeDash in the Short-Range Interaction slot.", HelpBoxMessageType.Warning);
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
                         displayBehaviourEngagementTriggerProperty,
                         useEngagementFeedbackOverrideProperty,
                         bindingProperty,
                         settingsContainer,
                         feedbackOptionsContainer,
                         feedbackOverrideContainer,
                         unsupportedModuleBox);
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            UpdateVisibility(changedProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             bindingProperty,
                             settingsContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });
        root.TrackPropertyValue(displayBehaviourEngagementTriggerProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             changedProperty,
                             useEngagementFeedbackOverrideProperty,
                             bindingProperty,
                             settingsContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });
        root.TrackPropertyValue(useEngagementFeedbackOverrideProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             displayBehaviourEngagementTriggerProperty,
                             changedProperty,
                             bindingProperty,
                             settingsContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });

        if (moduleIdProperty != null)
        {
            root.TrackPropertyValue(moduleIdProperty, changedProperty =>
            {
                UpdateVisibility(enabledProperty,
                                 displayBehaviourEngagementTriggerProperty,
                                 useEngagementFeedbackOverrideProperty,
                                 bindingProperty,
                                 settingsContainer,
                                 feedbackOptionsContainer,
                                 feedbackOverrideContainer,
                                 unsupportedModuleBox);
            });
        }

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates nested settings visibility and unsupported-module warnings from the current toggle state.
    /// </summary>
    /// <param name="enabledProperty">Serialized enabled property.</param>
    /// <param name="displayTriggerProperty">Serialized trigger toggle.</param>
    /// <param name="useOverrideProperty">Serialized override toggle.</param>
    /// <param name="bindingProperty">Serialized module binding.</param>
    /// <param name="settingsContainer">Main nested settings container.</param>
    /// <param name="feedbackOptionsContainer">Nested feedback options container.</param>
    /// <param name="feedbackOverrideContainer">Nested override settings container.</param>
    /// <param name="unsupportedModuleBox">Warning box shown for unsupported module kinds.</param>
    private static void UpdateVisibility(SerializedProperty enabledProperty,
                                         SerializedProperty displayTriggerProperty,
                                         SerializedProperty useOverrideProperty,
                                         SerializedProperty bindingProperty,
                                         VisualElement settingsContainer,
                                         VisualElement feedbackOptionsContainer,
                                         VisualElement feedbackOverrideContainer,
                                         HelpBox unsupportedModuleBox)
    {
        bool isInteractionEnabled = enabledProperty != null && enabledProperty.boolValue;
        bool isTriggerEnabled = isInteractionEnabled &&
                                displayTriggerProperty != null &&
                                displayTriggerProperty.boolValue;
        bool isOverrideEnabled = isTriggerEnabled &&
                                 useOverrideProperty != null &&
                                 useOverrideProperty.boolValue;
        bool showUnsupportedModuleWarning = isTriggerEnabled &&
                                            !EnemyOffensiveEngagementFeedbackDrawerUtility.SupportsDisplayTrigger(bindingProperty,
                                                                                                                 EnemyPatternModuleCatalogSection.ShortRangeInteraction);

        if (settingsContainer != null)
        {
            settingsContainer.style.display = isInteractionEnabled
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
    }

    #endregion

    #endregion
}
