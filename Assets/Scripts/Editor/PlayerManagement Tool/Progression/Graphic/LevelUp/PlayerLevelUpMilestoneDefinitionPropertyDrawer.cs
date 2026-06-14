using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Custom drawer for one level-up milestone definition with scoped drop-pool selection support.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerLevelUpMilestoneDefinition))]
public sealed class PlayerLevelUpMilestoneDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one UI Toolkit inspector for one serialized milestone definition.
    /// </summary>
    /// <param name="property">Serialized milestone property.</param>
    /// <returns>Generated root visual element for the property drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty milestoneLevelProperty = property.FindPropertyRelative("milestoneLevel");
        SerializedProperty specialExpRequirementProperty = property.FindPropertyRelative("specialExpRequirement");
        SerializedProperty recurringProperty = property.FindPropertyRelative("recurring");
        SerializedProperty recurrenceIntervalLevelsProperty = property.FindPropertyRelative("recurrenceIntervalLevels");
        SerializedProperty powerUpUnlocksProperty = property.FindPropertyRelative("powerUpUnlocks");
        SerializedProperty skipCompensationResourcesProperty = property.FindPropertyRelative("skipCompensationResources");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (milestoneLevelProperty == null ||
            specialExpRequirementProperty == null ||
            recurringProperty == null ||
            recurrenceIntervalLevelsProperty == null ||
            powerUpUnlocksProperty == null ||
            skipCompensationResourcesProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        VisualElement milestoneLevelField = PlayerScalingFieldElementFactory.CreateField(milestoneLevelProperty,
                                                                                         scalingRulesProperty,
                                                                                         "Milestone Level");
        root.Add(milestoneLevelField);

        VisualElement specialRequirementField = PlayerScalingFieldElementFactory.CreateField(specialExpRequirementProperty,
                                                                                             scalingRulesProperty,
                                                                                             "EXP Required To Reach This Milestone");
        root.Add(specialRequirementField);

        HelpBox requirementInfoBox = new HelpBox("This requirement is consumed on the previous level-up step that reaches the milestone level.", HelpBoxMessageType.Info);
        root.Add(requirementInfoBox);

        PropertyField recurringField = new PropertyField(recurringProperty, "Recurring");
        recurringField.BindProperty(recurringProperty);
        recurringField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        root.Add(recurringField);

        PropertyField recurrenceIntervalField = new PropertyField(recurrenceIntervalLevelsProperty, "Repeat Every X Levels");
        recurrenceIntervalField.BindProperty(recurrenceIntervalLevelsProperty);
        recurrenceIntervalField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        root.Add(recurrenceIntervalField);
        RefreshRecurrenceFieldVisibility();

        recurringField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RefreshRecurrenceFieldVisibility();
        });

        List<string> dropPoolIdOptions = PlayerProgressionTierOptionsUtility.BuildDropPoolIdOptionsFromPowerUpsLibrary();

        if (dropPoolIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Pool IDs found in the scoped Power-Ups preset. Milestone power-up unlocks cannot be configured yet.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
        }

        PropertyField powerUpUnlocksField = new PropertyField(powerUpUnlocksProperty, "Power-Up Unlocks");
        powerUpUnlocksField.BindProperty(powerUpUnlocksProperty);
        root.Add(powerUpUnlocksField);

        HelpBox unlocksInfoBox = new HelpBox("Each entry defines one custom offer roll. Milestones support up to 6 power-up unlock rolls.", HelpBoxMessageType.Info);
        root.Add(unlocksInfoBox);

        PropertyField skipCompensationsField = new PropertyField(skipCompensationResourcesProperty, "Skip Compensation Resources");
        skipCompensationsField.BindProperty(skipCompensationResourcesProperty);
        root.Add(skipCompensationsField);
        return root;

        void RefreshRecurrenceFieldVisibility()
        {
            recurrenceIntervalField.style.display = recurringProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #endregion
}
