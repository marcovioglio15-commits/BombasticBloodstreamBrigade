using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one level-up schedule step with enum-style scalable-stat name selection.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerLevelUpScheduleStepDefinition))]
public sealed class PlayerLevelUpScheduleStepDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one UI Toolkit inspector for one serialized schedule-step definition.
    /// </summary>
    /// <param name="property">Serialized schedule-step property.</param>
    /// <returns>Generated root visual element for the property drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty statNameProperty = property.FindPropertyRelative("statName");
        SerializedProperty applyModeProperty = property.FindPropertyRelative("applyMode");
        SerializedProperty valueProperty = property.FindPropertyRelative("value");

        if (statNameProperty == null || applyModeProperty == null || valueProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Schedule-step fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        List<string> statNameOptions = BuildScalableStatNameOptions(property.serializedObject);

        if (statNameOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No scalable stats available. Add scalable stats before configuring schedule steps.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
            TextField statNamePreviewField = new TextField("Stat Name");
            statNamePreviewField.value = "<No scalable stats>";
            statNamePreviewField.SetEnabled(false);
            root.Add(statNamePreviewField);
        }
        else
        {
            string selectedStatName = ResolveSelectedStatName(statNameOptions, statNameProperty.stringValue);

            if (!string.Equals(statNameProperty.stringValue, selectedStatName, StringComparison.Ordinal))
            {
                statNameProperty.serializedObject.Update();
                statNameProperty.stringValue = selectedStatName;
                statNameProperty.serializedObject.ApplyModifiedProperties();
            }

            PopupField<string> statNamePopup = new PopupField<string>("Stat Name", statNameOptions, selectedStatName);
            statNamePopup.RegisterValueChangedCallback(evt =>
            {
                statNameProperty.serializedObject.Update();
                statNameProperty.stringValue = evt.newValue;
                statNameProperty.serializedObject.ApplyModifiedProperties();
            });
            root.Add(statNamePopup);
        }

        PropertyField applyModeField = new PropertyField(applyModeProperty, "Apply Mode");
        applyModeField.BindProperty(applyModeProperty);
        root.Add(applyModeField);

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement valueField = PlayerScalingFieldElementFactory.CreateField(valueProperty,
                                                                                scalingRulesProperty,
                                                                                "Value");
        root.Add(valueField);

        return root;
    }
    #endregion

    #region Private Methods
    private static List<string> BuildScalableStatNameOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty scalableStatsProperty = serializedObject.FindProperty("scalableStats");

        if (scalableStatsProperty == null)
            return options;

        HashSet<string> visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int statIndex = 0; statIndex < scalableStatsProperty.arraySize; statIndex++)
        {
            SerializedProperty statProperty = scalableStatsProperty.GetArrayElementAtIndex(statIndex);

            if (statProperty == null)
                continue;

            SerializedProperty statNameProperty = statProperty.FindPropertyRelative("statName");

            if (statNameProperty == null || string.IsNullOrWhiteSpace(statNameProperty.stringValue))
                continue;

            string statName = statNameProperty.stringValue.Trim();

            if (!visitedNames.Add(statName))
                continue;

            options.Add(statName);
        }

        options.Sort(StringComparer.OrdinalIgnoreCase);
        return options;
    }

    private static string ResolveSelectedStatName(List<string> options, string currentStatName)
    {
        if (options == null || options.Count <= 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(currentStatName))
            return options[0];

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            string option = options[optionIndex];

            if (string.Equals(option, currentStatName, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }
    #endregion

    #endregion
}
