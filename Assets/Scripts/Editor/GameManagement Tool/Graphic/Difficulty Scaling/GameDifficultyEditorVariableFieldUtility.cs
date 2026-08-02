using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds constrained editor selectors for difficulty variables and coefficient references.
/// </summary>
internal static class GameDifficultyEditorVariableFieldUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a popup backed by every built-in, Player scalable-stat and coefficient variable in context.
    /// </summary>
    /// <param name="property">Serialized variable-name property receiving the selection.</param>
    /// <param name="label">Designer-facing popup label.</param>
    /// <returns>Configured constrained variable popup.</returns>
    public static PopupField<string> CreateVariablePopup(SerializedProperty property, string label)
    {
        List<string> choices = BuildVariableChoices(property.serializedObject);
        EnsureCurrentChoice(choices, property.stringValue);
        PopupField<string> popup = new PopupField<string>(label,
                                                          choices,
                                                          ResolveChoiceIndex(choices, property.stringValue));
        popup.tooltip = property.tooltip;
        popup.RegisterValueChangedCallback(evt => ApplyStringValue(property, evt.newValue));
        return popup;
    }

    /// <summary>
    /// Creates a popup backed by all authored Difficulty Scaling coefficient identifiers in the project.
    /// </summary>
    /// <param name="property">Serialized coefficient-ID property receiving the selection.</param>
    /// <param name="label">Designer-facing popup label.</param>
    /// <param name="includeEmpty">Whether an explicit unbound choice is available.</param>
    /// <returns>Configured coefficient popup.</returns>
    public static PopupField<string> CreateCoefficientPopup(SerializedProperty property,
                                                             string label,
                                                             bool includeEmpty)
    {
        List<string> choices = BuildCoefficientChoices(includeEmpty);
        EnsureCurrentChoice(choices, property.stringValue);
        PopupField<string> popup = new PopupField<string>(label,
                                                          choices,
                                                          ResolveChoiceIndex(choices, property.stringValue));
        popup.tooltip = property.tooltip;
        popup.RegisterValueChangedCallback(evt => ApplyStringValue(property,
                                                                    evt.newValue == "<None>"
                                                                        ? string.Empty
                                                                        : evt.newValue));
        return popup;
    }

    /// <summary>
    /// Adds a compact variable-token insertion toolbar below one unified formula field.
    /// </summary>
    /// <param name="root">Container receiving the formula field and insertion controls.</param>
    /// <param name="formulaProperty">Serialized unified formula string.</param>
    /// <param name="label">Designer-facing formula label.</param>
    public static void AddFormulaEditor(VisualElement root,
                                        SerializedProperty formulaProperty,
                                        string label)
    {
        UnityEditor.UIElements.PropertyField formulaField =
            new UnityEditor.UIElements.PropertyField(formulaProperty, label);
        formulaField.tooltip = formulaProperty.tooltip;
        root.Add(formulaField);
        List<string> variables = BuildVariableChoices(formulaProperty.serializedObject);
        PopupField<string> variablePopup = new PopupField<string>("Insert Variable", variables, 0);
        variablePopup.tooltip = "Select a valid context variable to append as a unified formula token.";
        Button insertButton = new Button(() =>
        {
            formulaProperty.serializedObject.Update();
            formulaProperty.stringValue += "[" + variablePopup.value + "]";
            formulaProperty.serializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();
        });
        insertButton.text = "Insert";
        insertButton.tooltip = "Append the selected [variable] token to the formula.";
        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.Add(variablePopup);
        toolbar.Add(insertButton);
        root.Add(toolbar);
    }
    #endregion

    #region Choice Methods
    /// <summary>
    /// Builds all variables available to the Difficulty Scaling preset owning one serialized property.
    /// </summary>
    /// <param name="serializedObject">Serialized owner used to resolve the active preset.</param>
    /// <returns>Sorted non-empty variable choices.</returns>
    private static List<string> BuildVariableChoices(SerializedObject serializedObject)
    {
        GameDifficultyScalingPreset preset = serializedObject.targetObject as GameDifficultyScalingPreset;
        HashSet<string> variables = GameDifficultyScalingValidationUtility.BuildAvailableVariableSet(preset);
        List<string> choices = new List<string>(variables);
        choices.Sort(StringComparer.OrdinalIgnoreCase);

        if (choices.Count == 0)
            choices.Add(GameDifficultyVariableNames.RoomsCleared);

        return choices;
    }

    /// <summary>
    /// Builds a unique ordered coefficient list across current Game Management difficulty presets.
    /// </summary>
    /// <param name="includeEmpty">Whether to prepend an explicit unbound choice.</param>
    /// <returns>Sorted coefficient choices.</returns>
    private static List<string> BuildCoefficientChoices(bool includeEmpty)
    {
        HashSet<string> uniqueChoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] presetGuids = AssetDatabase.FindAssets("t:GameDifficultyScalingPreset", new string[] { "Assets" });

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            GameDifficultyScalingPreset preset = AssetDatabase.LoadAssetAtPath<GameDifficultyScalingPreset>(
                AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]));

            if (preset == null)
                continue;

            for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
            {
                GameDifficultyCoefficientDefinition coefficient = preset.Coefficients[coefficientIndex];

                if (coefficient != null && !string.IsNullOrWhiteSpace(coefficient.CoefficientId))
                    uniqueChoices.Add(coefficient.CoefficientId);
            }
        }

        List<string> choices = new List<string>(uniqueChoices);
        choices.Sort(StringComparer.OrdinalIgnoreCase);

        if (includeEmpty)
            choices.Insert(0, "<None>");

        if (choices.Count == 0)
            choices.Add("<None>");

        return choices;
    }

    /// <summary>
    /// Adds a serialized current value when it is not present in the current project catalog.
    /// </summary>
    /// <param name="choices">Mutable popup choice list.</param>
    /// <param name="currentValue">Serialized value that must remain selectable.</param>
    private static void EnsureCurrentChoice(List<string> choices, string currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue) || choices.Contains(currentValue))
            return;

        choices.Add(currentValue);
        choices.Sort(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a safe popup index for one serialized string value.
    /// </summary>
    /// <param name="choices">Available popup choices.</param>
    /// <param name="currentValue">Serialized current value.</param>
    /// <returns>Matching index, or zero when unbound.</returns>
    private static int ResolveChoiceIndex(List<string> choices, string currentValue)
    {
        int index = choices.IndexOf(string.IsNullOrWhiteSpace(currentValue) ? "<None>" : currentValue);
        return index < 0 ? 0 : index;
    }

    /// <summary>
    /// Applies one popup selection through the owning SerializedObject and marks the draft dirty.
    /// </summary>
    /// <param name="property">Serialized string property receiving the value.</param>
    /// <param name="value">Selected variable or coefficient identifier.</param>
    private static void ApplyStringValue(SerializedProperty property, string value)
    {
        property.serializedObject.Update();
        property.stringValue = value;
        property.serializedObject.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #endregion
}
