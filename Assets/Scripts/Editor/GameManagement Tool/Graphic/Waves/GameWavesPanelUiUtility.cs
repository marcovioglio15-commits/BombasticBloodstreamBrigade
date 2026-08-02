using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes reusable Waves fields, popup labels and compact sequence choices.
/// </summary>
internal static class GameWavesPanelUiUtility
{
    #region Methods

    #region Field Methods
    /// <summary>
    /// Creates one compact toolbar button with a clear editor tooltip.
    /// </summary>
    /// <param name="text">Button label.</param>
    /// <param name="tooltip">Designer-facing behavior description.</param>
    /// <param name="action">Action invoked when clicked.</param>
    /// <returns>Configured toolbar button.</returns>
    public static Button CreateToolbarButton(string text, string tooltip, Action action)
    {
        return new Button(action) { text = text, tooltip = tooltip };
    }

    /// <summary>
    /// Builds one reusable row label for the Waves preset browser.
    /// </summary>
    /// <returns>Configured row label.</returns>
    public static VisualElement MakePresetRow()
    {
        Label label = new Label();
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        return label;
    }

    /// <summary>
    /// Adds one bound Game Management property field with tooltip and draft tracking.
    /// </summary>
    /// <param name="rootElement">Container receiving the field.</param>
    /// <param name="property">Serialized property to edit.</param>
    /// <param name="label">Designer-facing field label.</param>
    /// <returns>Created bound PropertyField.</returns>
    public static PropertyField AddBoundProperty(VisualElement rootElement,
                                                 SerializedProperty property,
                                                 string label)
    {
        PropertyField field = new PropertyField(property, label);
        field.tooltip = property == null ? string.Empty : property.tooltip;

        if (property != null)
            field.BindProperty(property);

        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        rootElement.Add(field);
        return field;
    }

    /// <summary>
    /// Adds an object-reference field that writes through serialization only after a genuine user change.
    /// This avoids binding-time callbacks rebuilding the owning tab while a picker is opening.
    /// </summary>
    /// <param name="rootElement">Container receiving the field and scheduling the follow-up refresh.</param>
    /// <param name="serializedObject">Serialized owner receiving the selected asset reference.</param>
    /// <param name="property">Object-reference property edited by the field.</param>
    /// <param name="label">Designer-facing field label.</param>
    /// <param name="objectType">Unity asset type accepted by the picker.</param>
    /// <param name="valueChanged">Follow-up action invoked after the serialized value is committed.</param>
    /// <returns>Created unbound object-reference field.</returns>
    public static ObjectField AddObjectReferenceField(VisualElement rootElement,
                                                       SerializedObject serializedObject,
                                                       SerializedProperty property,
                                                       string label,
                                                       Type objectType,
                                                       Action valueChanged)
    {
        string propertyPath = property.propertyPath;
        ObjectField field = new ObjectField(label)
        {
            objectType = objectType,
            allowSceneObjects = false,
            tooltip = property.tooltip
        };
        field.SetValueWithoutNotify(property.objectReferenceValue);
        field.RegisterValueChangedCallback(evt =>
        {
            if (evt.previousValue == evt.newValue)
                return;

            // Resolve the property again because a preceding UI rebuild can invalidate handles.
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty refreshedProperty = serializedObject.FindProperty(propertyPath);

            if (refreshedProperty == null)
                return;

            Undo.RecordObjects(serializedObject.targetObjects, "Change " + label);
            refreshedProperty.objectReferenceValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedObject.targetObject);
            GameManagementDraftSession.MarkDirty();

            if (valueChanged != null)
                rootElement.schedule.Execute(valueChanged);
        });
        rootElement.Add(field);
        return field;
    }

    /// <summary>
    /// Adds one field bound to the separately serialized Enemy Wave preset.
    /// </summary>
    /// <param name="rootElement">Container receiving the field.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset receiving changes.</param>
    /// <param name="property">Wave property to edit.</param>
    /// <param name="label">Designer-facing label.</param>
    public static void AddBoundWaveProperty(VisualElement rootElement,
                                            SerializedObject waveSerializedObject,
                                            SerializedProperty property,
                                            string label)
    {
        PropertyField field = AddBoundProperty(rootElement, property, label);
        field.RegisterValueChangeCallback(evt =>
        {
            EditorUtility.SetDirty(waveSerializedObject.targetObject);
        });
    }
    #endregion

    #region Choice Methods
    /// <summary>
    /// Builds readable scene popup choices from current serialized mappings.
    /// </summary>
    /// <param name="sceneMappings">Serialized room-scene mapping array.</param>
    /// <returns>Non-empty popup choice list.</returns>
    public static List<string> BuildSceneChoices(SerializedProperty sceneMappings)
    {
        List<string> choices = new List<string>();

        for (int sceneIndex = 0; sceneIndex < sceneMappings.arraySize; sceneIndex++)
        {
            string displayName = sceneMappings.GetArrayElementAtIndex(sceneIndex)
                                           .FindPropertyRelative("displayName")
                                           .stringValue;
            choices.Add(string.IsNullOrWhiteSpace(displayName) ? "Room " + (sceneIndex + 1) : displayName);
        }

        if (choices.Count == 0)
            choices.Add("No Scenes");

        return choices;
    }

    /// <summary>
    /// Builds readable wave popup choices while preserving the authored order.
    /// </summary>
    /// <param name="preset">Enemy Wave preset supplying waves.</param>
    /// <returns>Non-empty popup choice list.</returns>
    public static List<string> BuildWaveChoices(EnemyWavePreset preset)
    {
        List<string> choices = new List<string>();

        for (int waveIndex = 0; waveIndex < preset.Waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndex];
            string label = wave == null || string.IsNullOrWhiteSpace(wave.WaveLabel)
                ? "Wave " + (waveIndex + 1)
                : wave.WaveLabel;
            choices.Add(wave == null
                ? label
                : "Step " + (wave.SequenceStepIndex + 1) + " - " + label);
        }

        if (choices.Count == 0)
            choices.Add("No Waves");

        return choices;
    }

    /// <summary>
    /// Builds readable brush category choices from one Waves preset.
    /// </summary>
    /// <param name="preset">Waves preset supplying categories.</param>
    /// <returns>Non-empty popup choice list.</returns>
    public static List<string> BuildCategoryChoices(GameWavesPreset preset)
    {
        List<string> choices = new List<string>();

        for (int categoryIndex = 0; categoryIndex < preset.BrushCategories.Count; categoryIndex++)
        {
            EnemyBrushCategoryDefinition category = preset.BrushCategories[categoryIndex];
            choices.Add(category == null || string.IsNullOrWhiteSpace(category.DisplayName)
                ? "Category " + (categoryIndex + 1)
                : category.DisplayName);
        }

        if (choices.Count == 0)
            choices.Add("No Categories");

        return choices;
    }
    #endregion

    #region Query Methods
    /// <summary>
    /// Clamps one selection index to a serialized collection while preserving zero for empty collections.
    /// </summary>
    /// <param name="index">Requested selection index.</param>
    /// <param name="count">Current collection size.</param>
    /// <returns>Valid index, or zero when the collection is empty.</returns>
    public static int ClampIndex(int index, int count)
    {
        return count <= 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
    }
    #endregion

    #endregion
}
