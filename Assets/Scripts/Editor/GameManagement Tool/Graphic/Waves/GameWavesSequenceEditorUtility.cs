using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the ordered sequence-step editor where each step can contain any number of parallel waves.
/// </summary>
internal static class GameWavesSequenceEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds stable bound wave fields and limits full UI reconstruction to structural sequence mutations.
    /// </summary>
    /// <param name="root">Container receiving the complete sequence editor.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset being edited.</param>
    /// <param name="rebuild">Callback rebuilding the tab after a structural mutation.</param>
    public static void Build(VisualElement root,
                             SerializedObject waveSerializedObject,
                             Action rebuild)
    {
        waveSerializedObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);
        toolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Add Step",
            "Append one ordered sequence step containing a single wave.",
            () => AddStep(waveSerializedObject, rebuild)));
        root.Add(toolbar);
        HelpBox sequenceHelp = new HelpBox(
            "Steps run in ascending order. Waves inside the same step run in parallel; each later step evaluates its start condition against the preceding step as a whole.",
            HelpBoxMessageType.Info);
        sequenceHelp.style.flexShrink = 0f;
        root.Add(sequenceHelp);
        List<string> labelWarnings = GameWavesValidationUtility.BuildParallelLabelWarnings(
            waveSerializedObject.targetObject as EnemyWavePreset);

        for (int warningIndex = 0; warningIndex < labelWarnings.Count; warningIndex++)
        {
            HelpBox warning = new HelpBox(labelWarnings[warningIndex], HelpBoxMessageType.Warning);
            warning.style.flexShrink = 0f;
            root.Add(warning);
        }

        if (waves.arraySize == 0)
        {
            root.Add(new HelpBox("Add the first step to begin authoring the room wave sequence.",
                                 HelpBoxMessageType.Info));
            return;
        }

        List<int> stepIndices = BuildStepIndices(waves);
        ScrollView scrollView = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsScrollView(scrollView);

        // Render each ordered step with parallel wave children and structural controls.
        for (int stepPosition = 0; stepPosition < stepIndices.Count; stepPosition++)
        {
            int stepIndex = stepIndices[stepPosition];
            BuildStep(scrollView,
                      waveSerializedObject,
                      waves,
                      stepIndices,
                      stepPosition,
                      stepIndex,
                      rebuild);
        }

        root.Add(scrollView);
    }

    /// <summary>
    /// Appends a fully initialized wave to the requested sequence step and persists the structural edit.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset receiving the new wave.</param>
    /// <param name="stepIndex">Zero-based sequence step assigned to the new wave.</param>
    /// <param name="rebuild">Callback rebuilding the owning tab after insertion.</param>
    public static void AddWaveToStep(SerializedObject waveSerializedObject,
                                     int stepIndex,
                                     Action rebuild)
    {
        waveSerializedObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");
        Undo.RecordObject(waveSerializedObject.targetObject, "Add Parallel Enemy Wave");
        int waveIndex = waves.arraySize;
        waves.arraySize++;
        InitializeWave(waves.GetArrayElementAtIndex(waveIndex), stepIndex, waveIndex);
        ApplyStructuralChange(waveSerializedObject, rebuild);
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds one ordered step foldout and all parallel wave definitions assigned to it.
    /// </summary>
    /// <param name="root">Sequence scroll view receiving the step.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset owning the step.</param>
    /// <param name="waves">Serialized flat wave storage.</param>
    /// <param name="stepIndices">Ordered unique authored step identifiers.</param>
    /// <param name="stepPosition">Position of this step inside the ordered identifier list.</param>
    /// <param name="stepIndex">Zero-based authored step identifier.</param>
    /// <param name="rebuild">Callback rebuilding the tab after structural mutations.</param>
    private static void BuildStep(VisualElement root,
                                  SerializedObject waveSerializedObject,
                                  SerializedProperty waves,
                                  IReadOnlyList<int> stepIndices,
                                  int stepPosition,
                                  int stepIndex,
                                  Action rebuild)
    {
        List<int> waveIndices = BuildWaveIndicesForStep(waves, stepIndex);
        Foldout stepFoldout = new Foldout
        {
            text = "Step " + (stepPosition + 1) + " - " + waveIndices.Count +
                   (waveIndices.Count == 1 ? " wave" : " parallel waves"),
            value = true
        };
        stepFoldout.tooltip = "Every enabled wave in this step runs in parallel before the next ordered step condition is evaluated.";
        Toolbar stepToolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(stepToolbar);
        stepToolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Add Parallel Wave",
            "Add another wave that executes in parallel inside this step.",
            () => AddWaveToStep(waveSerializedObject, stepIndex, rebuild)));

        if (stepPosition > 0)
        {
            stepToolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
                "Move Earlier",
                "Swap this complete step with the preceding step.",
                () => SwapSteps(waveSerializedObject,
                                stepIndex,
                                stepIndices[stepPosition - 1],
                                rebuild)));
        }

        if (stepPosition < stepIndices.Count - 1)
        {
            stepToolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
                "Move Later",
                "Swap this complete step with the following step.",
                () => SwapSteps(waveSerializedObject,
                                stepIndex,
                                stepIndices[stepPosition + 1],
                                rebuild)));
        }

        stepToolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Remove Step",
            "Remove this step and every parallel wave it contains. The operation supports Undo.",
            () => RemoveStep(waveSerializedObject, stepIndex, rebuild)));
        stepFoldout.Add(stepToolbar);

        // Bind every parallel child independently so ordinary field changes never recreate the menu.
        for (int wavePosition = 0; wavePosition < waveIndices.Count; wavePosition++)
        {
            int waveIndex = waveIndices[wavePosition];
            BuildWave(stepFoldout,
                      waveSerializedObject,
                      waves.GetArrayElementAtIndex(waveIndex),
                      waveIndex,
                      rebuild);
        }

        root.Add(stepFoldout);
    }

    /// <summary>
    /// Builds stable bound fields for one parallel wave and conditionally shows its difficulty configuration.
    /// </summary>
    /// <param name="root">Step foldout receiving the wave editor.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset owning the wave.</param>
    /// <param name="wave">Serialized wave element being edited.</param>
    /// <param name="waveIndex">Flat serialized index used by structural removal.</param>
    /// <param name="rebuild">Callback rebuilding the tab after wave removal.</param>
    private static void BuildWave(VisualElement root,
                                  SerializedObject waveSerializedObject,
                                  SerializedProperty wave,
                                  int waveIndex,
                                  Action rebuild)
    {
        Foldout waveFoldout = new Foldout
        {
            text = BuildWaveTitle(wave, waveIndex),
            value = true
        };
        waveFoldout.tooltip = "Timing and optional difficulty selection for one wave running inside this parallel step.";
        GameWavesPanelUiUtility.AddBoundWaveProperty(waveFoldout,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("waveLabel"),
                                                     "Label");
        GameWavesPanelUiUtility.AddBoundWaveProperty(waveFoldout,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("startMode"),
                                                     "Start Condition");
        GameWavesPanelUiUtility.AddBoundWaveProperty(waveFoldout,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("startDelaySeconds"),
                                                     "Start Delay Seconds");
        GameWavesPanelUiUtility.AddBoundWaveProperty(waveFoldout,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("spawnDurationSeconds"),
                                                     "Spawn Duration Seconds");
        GameWavesPanelUiUtility.AddBoundWaveProperty(waveFoldout,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("defaultDistributionCurve"),
                                                     "Distribution Curve");
        AddDifficultyFields(waveFoldout, waveSerializedObject, wave);
        Foldout advancedFoldout = new Foldout
        {
            text = "Advanced Dependency Override",
            value = false
        };
        advancedFoldout.tooltip = "Optionally target one exact wave instead of the preceding sequence step. Validation reports missing or cyclic references.";
        GameWavesPanelUiUtility.AddBoundWaveProperty(advancedFoldout,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("referenceWaveId"),
                                                     "Explicit Prerequisite Wave ID");
        waveFoldout.Add(advancedFoldout);
        waveFoldout.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Remove Wave",
            "Remove only this parallel wave while preserving the remaining step. The operation supports Undo.",
            () => RemoveWave(waveSerializedObject, waveIndex, rebuild)));
        root.Add(waveFoldout);
    }

    /// <summary>
    /// Adds a persistent difficulty-selection toggle and a stable conditionally visible field container.
    /// </summary>
    /// <param name="root">Wave foldout receiving the controls.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset receiving bound changes.</param>
    /// <param name="wave">Serialized wave supplying difficulty properties.</param>
    public static void AddDifficultyFields(VisualElement root,
                                           SerializedObject waveSerializedObject,
                                           SerializedProperty wave)
    {
        SerializedProperty enabledProperty = wave.FindPropertyRelative("useDifficultySelection");
        Toggle enabledToggle = new Toggle("Use Difficulty Selection");
        enabledToggle.tooltip = enabledProperty.tooltip;
        enabledToggle.BindProperty(enabledProperty);
        VisualElement fields = new VisualElement();
        fields.style.marginLeft = 16f;
        fields.style.display = enabledProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        GameWavesPanelUiUtility.AddBoundWaveProperty(fields,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("difficultySelectionGroupId"),
                                                     "Selection Group");
        fields.Add(GameDifficultyEditorVariableFieldUtility.CreateCoefficientPopup(
            wave.FindPropertyRelative("difficultyCoefficientId"),
            "Difficulty Coefficient",
            false));
        GameWavesPanelUiUtility.AddBoundWaveProperty(fields,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("minimumDifficulty"),
                                                     "Minimum Difficulty");
        GameWavesPanelUiUtility.AddBoundWaveProperty(fields,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("maximumDifficulty"),
                                                     "Maximum Difficulty");
        GameWavesPanelUiUtility.AddBoundWaveProperty(fields,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("selectionWeight"),
                                                     "Selection Weight");
        enabledToggle.RegisterValueChangedCallback(evt =>
        {
            fields.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            EditorUtility.SetDirty(waveSerializedObject.targetObject);
            GameManagementDraftSession.MarkDirty();
        });
        root.Add(enabledToggle);
        root.Add(fields);
    }
    #endregion

    #region Mutation Methods
    /// <summary>
    /// Appends a new ordered step after the highest currently authored step.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset receiving the step.</param>
    /// <param name="rebuild">Callback rebuilding the tab after insertion.</param>
    private static void AddStep(SerializedObject waveSerializedObject, Action rebuild)
    {
        waveSerializedObject.UpdateIfRequiredOrScript();
        List<int> stepIndices = BuildStepIndices(waveSerializedObject.FindProperty("waves"));
        int stepIndex = stepIndices.Count == 0 ? 0 : stepIndices[stepIndices.Count - 1] + 1;
        AddWaveToStep(waveSerializedObject, stepIndex, rebuild);
    }

    /// <summary>
    /// Removes one wave from flat serialized storage while preserving all other step assignments.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset being mutated.</param>
    /// <param name="waveIndex">Flat wave index to remove.</param>
    /// <param name="rebuild">Callback rebuilding the tab after removal.</param>
    private static void RemoveWave(SerializedObject waveSerializedObject, int waveIndex, Action rebuild)
    {
        waveSerializedObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");

        if (waveIndex < 0 || waveIndex >= waves.arraySize)
            return;

        Undo.RecordObject(waveSerializedObject.targetObject, "Remove Enemy Wave");
        int removedStepIndex = waves.GetArrayElementAtIndex(waveIndex)
                                    .FindPropertyRelative("sequenceStepIndex")
                                    .intValue;
        int parallelWaveCount = BuildWaveIndicesForStep(waves, removedStepIndex).Count;
        waves.DeleteArrayElementAtIndex(waveIndex);

        if (parallelWaveCount == 1)
            CompactStepsAfterRemoval(waves, removedStepIndex);

        ApplyStructuralChange(waveSerializedObject, rebuild);
    }

    /// <summary>
    /// Removes every wave assigned to one step and compacts all later step indices.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset being mutated.</param>
    /// <param name="stepIndex">Authored step identifier to remove.</param>
    /// <param name="rebuild">Callback rebuilding the tab after removal.</param>
    private static void RemoveStep(SerializedObject waveSerializedObject, int stepIndex, Action rebuild)
    {
        waveSerializedObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");
        Undo.RecordObject(waveSerializedObject.targetObject, "Remove Enemy Wave Step");

        // Delete backwards so flat indices remain valid throughout the mutation.
        for (int waveIndex = waves.arraySize - 1; waveIndex >= 0; waveIndex--)
        {
            SerializedProperty stepProperty = waves.GetArrayElementAtIndex(waveIndex)
                                                   .FindPropertyRelative("sequenceStepIndex");

            if (stepProperty.intValue == stepIndex)
                waves.DeleteArrayElementAtIndex(waveIndex);
        }

        CompactStepsAfterRemoval(waves, stepIndex);
        ApplyStructuralChange(waveSerializedObject, rebuild);
    }

    /// <summary>
    /// Compacts step membership after the last wave of one ordered step is removed.
    /// </summary>
    /// <param name="waves">Serialized flat wave collection being compacted.</param>
    /// <param name="removedStepIndex">Removed step whose later siblings shift earlier.</param>
    private static void CompactStepsAfterRemoval(SerializedProperty waves, int removedStepIndex)
    {
        // Keep the remaining sequence contiguous and designer-readable.
        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            SerializedProperty stepProperty = waves.GetArrayElementAtIndex(waveIndex)
                                                   .FindPropertyRelative("sequenceStepIndex");

            if (stepProperty.intValue > removedStepIndex)
                stepProperty.intValue--;
        }
    }

    /// <summary>
    /// Swaps two complete sequence steps without reordering or duplicating their parallel wave data.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset being mutated.</param>
    /// <param name="firstStepIndex">First authored step identifier.</param>
    /// <param name="secondStepIndex">Second authored step identifier.</param>
    /// <param name="rebuild">Callback rebuilding the tab after the swap.</param>
    private static void SwapSteps(SerializedObject waveSerializedObject,
                                  int firstStepIndex,
                                  int secondStepIndex,
                                  Action rebuild)
    {
        waveSerializedObject.UpdateIfRequiredOrScript();
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");
        Undo.RecordObject(waveSerializedObject.targetObject, "Reorder Enemy Wave Steps");

        // Swap only compact integer membership; the complete wave payload remains untouched.
        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            SerializedProperty stepProperty = waves.GetArrayElementAtIndex(waveIndex)
                                                   .FindPropertyRelative("sequenceStepIndex");

            if (stepProperty.intValue == firstStepIndex)
                stepProperty.intValue = secondStepIndex;
            else if (stepProperty.intValue == secondStepIndex)
                stepProperty.intValue = firstStepIndex;
        }

        ApplyStructuralChange(waveSerializedObject, rebuild);
    }

    /// <summary>
    /// Initializes every serialized field of a newly inserted wave so Unity cannot retain copied array data.
    /// </summary>
    /// <param name="wave">New serialized wave element receiving defaults.</param>
    /// <param name="stepIndex">Zero-based sequence step assigned to the wave.</param>
    /// <param name="waveIndex">Flat index used to generate a readable default label.</param>
    private static void InitializeWave(SerializedProperty wave, int stepIndex, int waveIndex)
    {
        wave.FindPropertyRelative("waveId").stringValue = Guid.NewGuid().ToString("N");
        wave.FindPropertyRelative("waveLabel").stringValue = "Wave " + (waveIndex + 1);
        wave.FindPropertyRelative("sequenceStepIndex").intValue = stepIndex;
        wave.FindPropertyRelative("referenceWaveId").stringValue = string.Empty;
        wave.FindPropertyRelative("previewInScene").boolValue = false;
        wave.FindPropertyRelative("startMode").enumValueIndex = stepIndex == 0
            ? (int)EnemyWaveStartMode.FromSpawnerStart
            : (int)EnemyWaveStartMode.AfterPreviousWaveCompleted;
        wave.FindPropertyRelative("startDelaySeconds").floatValue = 0f;
        wave.FindPropertyRelative("spawnDurationSeconds").floatValue = 4f;
        wave.FindPropertyRelative("defaultDistributionCurve").animationCurveValue =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);
        wave.FindPropertyRelative("paintedCells").ClearArray();
        wave.FindPropertyRelative("useDifficultySelection").boolValue = false;
        wave.FindPropertyRelative("difficultySelectionGroupId").stringValue = string.Empty;
        wave.FindPropertyRelative("difficultyCoefficientId").stringValue = string.Empty;
        wave.FindPropertyRelative("minimumDifficulty").floatValue = 0f;
        wave.FindPropertyRelative("maximumDifficulty").floatValue = 100f;
        wave.FindPropertyRelative("selectionWeight").floatValue = 1f;
    }

    /// <summary>
    /// Persists one structural sequence mutation and notifies both Unity and the Game Management draft session.
    /// </summary>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset containing the mutation.</param>
    /// <param name="rebuild">Callback rebuilding the owning tab after persistence.</param>
    private static void ApplyStructuralChange(SerializedObject waveSerializedObject, Action rebuild)
    {
        waveSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(waveSerializedObject.targetObject);
        GameManagementDraftSession.MarkDirty();
        rebuild?.Invoke();
    }
    #endregion

    #region Query Methods
    /// <summary>
    /// Collects sorted unique step identifiers from flat serialized wave storage.
    /// </summary>
    /// <param name="waves">Serialized flat wave collection.</param>
    /// <returns>Ascending unique sequence-step identifiers.</returns>
    private static List<int> BuildStepIndices(SerializedProperty waves)
    {
        List<int> stepIndices = new List<int>();

        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            int stepIndex = waves.GetArrayElementAtIndex(waveIndex)
                                 .FindPropertyRelative("sequenceStepIndex")
                                 .intValue;

            if (!stepIndices.Contains(stepIndex))
                stepIndices.Add(stepIndex);
        }

        stepIndices.Sort();
        return stepIndices;
    }

    /// <summary>
    /// Collects flat wave indices assigned to one sequence step while preserving authored order.
    /// </summary>
    /// <param name="waves">Serialized flat wave collection.</param>
    /// <param name="stepIndex">Step membership to match.</param>
    /// <returns>Ordered flat wave indices belonging to the step.</returns>
    private static List<int> BuildWaveIndicesForStep(SerializedProperty waves, int stepIndex)
    {
        List<int> waveIndices = new List<int>();

        for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
        {
            if (waves.GetArrayElementAtIndex(waveIndex)
                     .FindPropertyRelative("sequenceStepIndex")
                     .intValue == stepIndex)
            {
                waveIndices.Add(waveIndex);
            }
        }

        return waveIndices;
    }

    /// <summary>
    /// Builds the stable foldout title shown for one parallel wave definition.
    /// </summary>
    /// <param name="wave">Serialized wave providing its designer label.</param>
    /// <param name="waveIndex">Flat index used by the fallback title.</param>
    /// <returns>Readable wave foldout title.</returns>
    private static string BuildWaveTitle(SerializedProperty wave, int waveIndex)
    {
        string label = wave.FindPropertyRelative("waveLabel").stringValue;
        return string.IsNullOrWhiteSpace(label) ? "Wave " + (waveIndex + 1) : label;
    }
    #endregion

    #endregion
}
