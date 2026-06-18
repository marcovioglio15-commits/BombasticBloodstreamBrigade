using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one milestone skip-compensation entry with contextual help for experience percentages.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerMilestoneSkipCompensationDefinition))]
public sealed class PlayerMilestoneSkipCompensationDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one milestone skip-compensation definition.
    /// </summary>
    /// <param name="property">Serialized skip-compensation property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty resourceTypeProperty = property.FindPropertyRelative("resourceType");
        SerializedProperty applyModeProperty = property.FindPropertyRelative("applyMode");
        SerializedProperty valueProperty = property.FindPropertyRelative("value");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (resourceTypeProperty == null || applyModeProperty == null || valueProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone skip compensation fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PropertyField resourceTypeField = new PropertyField(resourceTypeProperty, "Resource");
        resourceTypeField.BindProperty(resourceTypeProperty);
        root.Add(resourceTypeField);

        PropertyField applyModeField = new PropertyField(applyModeProperty, "Apply Mode");
        applyModeField.BindProperty(applyModeProperty);
        root.Add(applyModeField);

        VisualElement valueField = PlayerScalingFieldElementFactory.CreateField(valueProperty,
                                                                                scalingRulesProperty,
                                                                                "Value");
        root.Add(valueField);

        HelpBox experiencePercentInfoBox = new HelpBox("Experience percentages are evaluated against the EXP required for the next level-up, not against the current EXP value.", HelpBoxMessageType.Info);
        root.Add(experiencePercentInfoBox);

        void RefreshExperiencePercentInfo()
        {
            bool isExperience = resourceTypeProperty.enumValueIndex == (int)PlayerMilestoneSkipCompensationResourceType.Experience;
            bool isPercent = applyModeProperty.enumValueIndex == (int)PlayerMilestoneCompensationApplyMode.Percent;
            experiencePercentInfoBox.style.display = isExperience && isPercent ? DisplayStyle.Flex : DisplayStyle.None;
        }

        resourceTypeField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshExperiencePercentInfo());
        applyModeField.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshExperiencePercentInfo());
        RefreshExperiencePercentInfo();
        return root;
    }
    #endregion

    #endregion
}
