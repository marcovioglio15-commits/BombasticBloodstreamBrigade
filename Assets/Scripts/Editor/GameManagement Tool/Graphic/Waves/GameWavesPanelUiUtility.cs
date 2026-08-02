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
    /// Adds separate ordered-step and parallel-wave selectors while retaining flat serialized wave indices.
    /// </summary>
    /// <param name="rootElement">Toolbar receiving both selectors.</param>
    /// <param name="preset">Enemy Wave preset supplying ordered steps and parallel members.</param>
    /// <param name="selectedWaveIndex">Currently selected flat wave index.</param>
    /// <param name="selectionChanged">Callback receiving the selected flat wave index.</param>
    public static void AddWaveSequenceSelectors(VisualElement rootElement,
                                                EnemyWavePreset preset,
                                                int selectedWaveIndex,
                                                Action<int> selectionChanged)
    {
        if (preset == null || preset.Waves.Count == 0)
            return;

        selectedWaveIndex = ClampIndex(selectedWaveIndex, preset.Waves.Count);
        List<int> stepIndices = BuildStepIndices(preset);

        if (stepIndices.Count == 0)
            return;

        EnemySpawnWaveAuthoring selectedWave = preset.Waves[selectedWaveIndex];
        int selectedStepIndex = selectedWave == null
            ? stepIndices[0]
            : selectedWave.SequenceStepIndex;
        int selectedStepPosition = Mathf.Max(0, stepIndices.IndexOf(selectedStepIndex));
        List<string> stepChoices = new List<string>(stepIndices.Count);

        // Describe each ordered barrier independently from the parallel members it contains.
        for (int stepPosition = 0; stepPosition < stepIndices.Count; stepPosition++)
        {
            int parallelCount = CountWavesInStep(preset, stepIndices[stepPosition]);
            stepChoices.Add("Step " + (stepIndices[stepPosition] + 1) + " (" + parallelCount +
                            (parallelCount == 1 ? " wave)" : " parallel waves)"));
        }

        PopupField<string> stepPopup = new PopupField<string>("Step", stepChoices, selectedStepPosition);
        stepPopup.tooltip = "Ordered sequence step. Every wave inside the selected step starts as a parallel member.";
        stepPopup.RegisterValueChangedCallback(evt =>
        {
            List<int> selectedStepWaves = BuildWaveIndicesForStep(preset, stepIndices[stepPopup.index]);

            if (selectedStepWaves.Count > 0 && selectionChanged != null)
                selectionChanged(selectedStepWaves[0]);
        });
        rootElement.Add(stepPopup);

        List<int> waveIndices = BuildWaveIndicesForStep(preset, selectedStepIndex);
        List<string> waveChoices = new List<string>(waveIndices.Count);

        // Preserve the authored parallel order while making duplicate labels distinguishable by position.
        for (int wavePosition = 0; wavePosition < waveIndices.Count; wavePosition++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndices[wavePosition]];
            string label = wave == null || string.IsNullOrWhiteSpace(wave.WaveLabel)
                ? "Unnamed Wave"
                : wave.WaveLabel;
            waveChoices.Add("Parallel " + (wavePosition + 1) + " - " + label);
        }

        int selectedWavePosition = Mathf.Max(0, waveIndices.IndexOf(selectedWaveIndex));
        PopupField<string> wavePopup = new PopupField<string>("Parallel Wave", waveChoices, selectedWavePosition);
        wavePopup.tooltip = "Single parallel wave displayed and painted inside the selected ordered step.";
        wavePopup.RegisterValueChangedCallback(evt =>
        {
            if (wavePopup.index >= 0 && wavePopup.index < waveIndices.Count && selectionChanged != null)
                selectionChanged(waveIndices[wavePopup.index]);
        });
        rootElement.Add(wavePopup);
    }

    /// <summary>
    /// Builds the selected-wave heading with separate ordered-step and parallel-member positions.
    /// </summary>
    /// <param name="preset">Enemy Wave preset containing the selected wave.</param>
    /// <param name="selectedWaveIndex">Selected flat wave index.</param>
    /// <returns>Readable step and parallel-member context.</returns>
    public static string BuildWaveSelectionContext(EnemyWavePreset preset, int selectedWaveIndex)
    {
        if (preset == null || preset.Waves.Count == 0)
            return "Selected Wave";

        selectedWaveIndex = ClampIndex(selectedWaveIndex, preset.Waves.Count);
        EnemySpawnWaveAuthoring selectedWave = preset.Waves[selectedWaveIndex];

        if (selectedWave == null)
            return "Selected Wave";

        List<int> waveIndices = BuildWaveIndicesForStep(preset, selectedWave.SequenceStepIndex);
        int parallelPosition = Mathf.Max(0, waveIndices.IndexOf(selectedWaveIndex));
        return "Selected Wave - Step " + (selectedWave.SequenceStepIndex + 1) +
               " / Parallel " + (parallelPosition + 1) + " of " + waveIndices.Count;
    }

    /// <summary>
    /// Collects ascending authored sequence-step identifiers from one wave preset.
    /// </summary>
    /// <param name="preset">Enemy Wave preset to inspect.</param>
    /// <returns>Ascending unique step identifiers.</returns>
    private static List<int> BuildStepIndices(EnemyWavePreset preset)
    {
        List<int> stepIndices = new List<int>();

        for (int waveIndex = 0; waveIndex < preset.Waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndex];

            if (wave != null && !stepIndices.Contains(wave.SequenceStepIndex))
                stepIndices.Add(wave.SequenceStepIndex);
        }

        stepIndices.Sort();
        return stepIndices;
    }

    /// <summary>
    /// Collects flat wave indices belonging to one ordered sequence step.
    /// </summary>
    /// <param name="preset">Enemy Wave preset to inspect.</param>
    /// <param name="stepIndex">Authored sequence step to match.</param>
    /// <returns>Parallel member indices in authored order.</returns>
    private static List<int> BuildWaveIndicesForStep(EnemyWavePreset preset, int stepIndex)
    {
        List<int> waveIndices = new List<int>();

        for (int waveIndex = 0; waveIndex < preset.Waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndex];

            if (wave != null && wave.SequenceStepIndex == stepIndex)
                waveIndices.Add(waveIndex);
        }

        return waveIndices;
    }

    /// <summary>
    /// Counts parallel members assigned to one sequence step.
    /// </summary>
    /// <param name="preset">Enemy Wave preset to inspect.</param>
    /// <param name="stepIndex">Authored sequence step to count.</param>
    /// <returns>Number of parallel waves in the step.</returns>
    private static int CountWavesInStep(EnemyWavePreset preset, int stepIndex)
    {
        int count = 0;

        for (int waveIndex = 0; waveIndex < preset.Waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = preset.Waves[waveIndex];

            if (wave != null && wave.SequenceStepIndex == stepIndex)
                count++;
        }

        return count;
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
