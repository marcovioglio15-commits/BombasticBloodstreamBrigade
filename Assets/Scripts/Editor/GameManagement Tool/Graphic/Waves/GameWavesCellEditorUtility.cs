using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds focused single-cell editing controls below the embedded Waves scene preview.
/// </summary>
internal static class GameWavesCellEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds contextual single-cell controls or a concise selection hint below the scene preview.
    /// </summary>
    /// <param name="root">Scene Brush tab receiving the contextual editor.</param>
    /// <param name="wavePresetObject">Serialized wave asset owning the selected cell.</param>
    /// <param name="waveIndex">Currently previewed wave index.</param>
    /// <param name="selectedCoordinate">Optional selected painted-cell coordinate.</param>
    /// <param name="wavesPreset">Category source used to build the brush selector.</param>
    /// <param name="clearSelection">Callback clearing the panel selection.</param>
    /// <param name="rebuild">Callback rebuilding conditional controls after structural changes.</param>
    public static void Build(VisualElement root,
                             SerializedObject wavePresetObject,
                             int waveIndex,
                             Vector2Int? selectedCoordinate,
                             GameWavesPreset wavesPreset,
                             Action clearSelection,
                             Action rebuild)
    {
        if (!selectedCoordinate.HasValue)
        {
            root.Add(new HelpBox("Right-click a painted cell to edit its category, count and distribution curve.",
                                 HelpBoxMessageType.Info));
            return;
        }

        wavePresetObject.UpdateIfRequiredOrScript();
        SerializedProperty wave = FindWave(wavePresetObject, waveIndex);
        SerializedProperty cell = FindCell(wave, selectedCoordinate.Value);

        if (cell == null)
        {
            clearSelection();
            root.Add(new HelpBox("The selected cell no longer exists in this wave.", HelpBoxMessageType.Info));
            return;
        }

        Foldout editor = new Foldout
        {
            text = "Selected Cell [" + selectedCoordinate.Value.x + ", " + selectedCoordinate.Value.y + "]",
            value = true
        };
        editor.tooltip = "Focused controls for the painted cell selected from the room preview.";
        AddCategoryField(editor,
                         wavePresetObject,
                         waveIndex,
                         selectedCoordinate.Value,
                         wavesPreset);
        AddCountField(editor, wavePresetObject, waveIndex, selectedCoordinate.Value, cell);
        AddDistributionFields(editor,
                              wavePresetObject,
                              waveIndex,
                              selectedCoordinate.Value,
                              wave,
                              cell,
                              rebuild);
        AddActions(editor,
                   wavePresetObject,
                   waveIndex,
                   selectedCoordinate.Value,
                   clearSelection,
                   rebuild);
        root.Add(editor);
    }
    #endregion

    #region Field Methods
    /// <summary>
    /// Adds a stable category popup for the selected painted cell.
    /// </summary>
    /// <param name="root">Cell editor receiving the popup.</param>
    /// <param name="wavePresetObject">Serialized wave asset being edited.</param>
    /// <param name="waveIndex">Selected wave index.</param>
    /// <param name="coordinate">Selected cell coordinate.</param>
    /// <param name="wavesPreset">Preset supplying reusable categories.</param>
    private static void AddCategoryField(VisualElement root,
                                         SerializedObject wavePresetObject,
                                         int waveIndex,
                                         Vector2Int coordinate,
                                         GameWavesPreset wavesPreset)
    {
        if (wavesPreset == null || wavesPreset.BrushCategories.Count == 0)
        {
            root.Add(new HelpBox("Create at least one Brush Category before assigning this cell.",
                                 HelpBoxMessageType.Warning));
            return;
        }

        SerializedProperty cell = FindCell(FindWave(wavePresetObject, waveIndex), coordinate);
        string currentCategoryId = cell.FindPropertyRelative("brushCategoryId").stringValue;
        List<string> choices = GameWavesPanelUiUtility.BuildCategoryChoices(wavesPreset);
        int selectedIndex = FindCategoryIndex(wavesPreset, currentCategoryId);

        if (selectedIndex < 0)
        {
            selectedIndex = choices.Count;
            choices.Add(string.IsNullOrWhiteSpace(currentCategoryId)
                ? "Select Category"
                : "Unresolved: " + currentCategoryId);
        }

        PopupField<string> categoryField = new PopupField<string>("Brush Category", choices, selectedIndex);
        categoryField.tooltip = "Reusable difficulty-aware category resolved for this painted cell at runtime.";
        categoryField.RegisterValueChangedCallback(evt =>
        {
            if (categoryField.index < 0 || categoryField.index >= wavesPreset.BrushCategories.Count)
                return;

            EnemyBrushCategoryDefinition category = wavesPreset.BrushCategories[categoryField.index];

            if (category == null)
                return;

            ApplyCellChange(wavePresetObject,
                            waveIndex,
                            coordinate,
                            "Change Enemy Wave Cell Category",
                            (waveProperty, cellProperty) =>
                            {
                                cellProperty.FindPropertyRelative("brushCategoryId").stringValue =
                                    category.TechnicalId;
                            });
        });
        root.Add(categoryField);
    }

    /// <summary>
    /// Adds the selected cell enemy-count field and a type-consistent validation warning.
    /// </summary>
    /// <param name="root">Cell editor receiving the count field.</param>
    /// <param name="wavePresetObject">Serialized wave asset being edited.</param>
    /// <param name="waveIndex">Selected wave index.</param>
    /// <param name="coordinate">Selected cell coordinate.</param>
    /// <param name="cell">Current serialized cell.</param>
    private static void AddCountField(VisualElement root,
                                      SerializedObject wavePresetObject,
                                      int waveIndex,
                                      Vector2Int coordinate,
                                      SerializedProperty cell)
    {
        int currentCount = cell.FindPropertyRelative("enemyCount").intValue;
        IntegerField countField = new IntegerField("Enemy Count") { value = currentCount };
        countField.tooltip = "Total number of enemies emitted from this cell during the selected wave.";
        countField.RegisterValueChangedCallback(evt =>
        {
            ApplyCellChange(wavePresetObject,
                            waveIndex,
                            coordinate,
                            "Change Enemy Wave Cell Count",
                            (waveProperty, cellProperty) =>
                            {
                                cellProperty.FindPropertyRelative("enemyCount").intValue = evt.newValue;
                            });
        });
        root.Add(countField);

        if (currentCount <= 0)
            root.Add(new HelpBox("Enemy Count must be greater than zero before baking.", HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds effective curve editing while preserving the old default-to-local override workflow.
    /// </summary>
    /// <param name="root">Cell editor receiving the curve controls.</param>
    /// <param name="wavePresetObject">Serialized wave asset being edited.</param>
    /// <param name="waveIndex">Selected wave index.</param>
    /// <param name="coordinate">Selected cell coordinate.</param>
    /// <param name="wave">Current serialized wave.</param>
    /// <param name="cell">Current serialized cell.</param>
    /// <param name="rebuild">Callback rebuilding conditional controls.</param>
    private static void AddDistributionFields(VisualElement root,
                                              SerializedObject wavePresetObject,
                                              int waveIndex,
                                              Vector2Int coordinate,
                                              SerializedProperty wave,
                                              SerializedProperty cell,
                                              Action rebuild)
    {
        bool useWaveDefault = cell.FindPropertyRelative("useWaveDefaultDistribution").boolValue;
        AnimationCurve waveCurve = wave.FindPropertyRelative("defaultDistributionCurve").animationCurveValue;
        AnimationCurve localCurve = cell.FindPropertyRelative("distributionCurveOverride").animationCurveValue;
        Toggle useDefaultField = new Toggle("Use Wave Default Distribution") { value = useWaveDefault };
        useDefaultField.tooltip = "Use the selected wave distribution curve instead of this cell's local override.";
        useDefaultField.RegisterValueChangedCallback(evt =>
        {
            ApplyCellChange(wavePresetObject,
                            waveIndex,
                            coordinate,
                            "Change Enemy Wave Cell Distribution Mode",
                            (waveProperty, cellProperty) =>
                            {
                                if (!evt.newValue &&
                                    cellProperty.FindPropertyRelative("useWaveDefaultDistribution").boolValue)
                                {
                                    cellProperty.FindPropertyRelative("distributionCurveOverride").animationCurveValue =
                                        EnemySpawnerAuthoringEditorWaveUtility.CloneAnimationCurve(
                                            waveProperty.FindPropertyRelative("defaultDistributionCurve")
                                                        .animationCurveValue);
                                }

                                cellProperty.FindPropertyRelative("useWaveDefaultDistribution").boolValue = evt.newValue;
                            });
            rebuild();
        });
        root.Add(useDefaultField);

        CurveField curveField = new CurveField("Distribution Curve")
        {
            value = EnemySpawnerAuthoringEditorWaveUtility.CloneAnimationCurve(
                useWaveDefault ? waveCurve : localCurve)
        };
        curveField.tooltip = useWaveDefault
            ? "Effective wave curve. Editing it creates a local override for this cell."
            : "Local cumulative distribution curve used by this cell.";
        curveField.RegisterValueChangedCallback(evt =>
        {
            ApplyCellChange(wavePresetObject,
                            waveIndex,
                            coordinate,
                            "Change Enemy Wave Cell Distribution Curve",
                            (waveProperty, cellProperty) =>
                            {
                                cellProperty.FindPropertyRelative("useWaveDefaultDistribution").boolValue = false;
                                cellProperty.FindPropertyRelative("distributionCurveOverride").animationCurveValue =
                                    EnemySpawnerAuthoringEditorWaveUtility.CloneAnimationCurve(evt.newValue);
                            });
            rebuild();
        });
        root.Add(curveField);

        if (useWaveDefault)
            return;

        Button restoreDefaultButton = new Button(() =>
        {
            ApplyCellChange(wavePresetObject,
                            waveIndex,
                            coordinate,
                            "Use Enemy Wave Default Distribution",
                            (waveProperty, cellProperty) =>
                            {
                                cellProperty.FindPropertyRelative("useWaveDefaultDistribution").boolValue = true;
                            });
            rebuild();
        })
        {
            text = "Use Wave Default Again",
            tooltip = "Discard the active local curve and return to the selected wave distribution curve."
        };
        root.Add(restoreDefaultButton);
    }

    /// <summary>
    /// Adds explicit deselection and removal actions for the selected painted cell.
    /// </summary>
    /// <param name="root">Cell editor receiving the action toolbar.</param>
    /// <param name="wavePresetObject">Serialized wave asset being edited.</param>
    /// <param name="waveIndex">Selected wave index.</param>
    /// <param name="coordinate">Selected cell coordinate.</param>
    /// <param name="clearSelection">Callback clearing the current selection.</param>
    /// <param name="rebuild">Callback rebuilding the Scene Brush tab.</param>
    private static void AddActions(VisualElement root,
                                   SerializedObject wavePresetObject,
                                   int waveIndex,
                                   Vector2Int coordinate,
                                   Action clearSelection,
                                   Action rebuild)
    {
        Toolbar actions = new Toolbar();
        actions.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Close Cell Editor",
            "Hide the detailed controls without changing the painted cell.",
            () =>
            {
                clearSelection();
                rebuild();
            }));
        actions.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Remove Cell",
            "Delete the selected painted cell from this wave.",
            () =>
            {
                RemoveCell(wavePresetObject, waveIndex, coordinate);
                clearSelection();
                rebuild();
            }));
        root.Add(actions);
    }
    #endregion

    #region Mutation Methods
    /// <summary>
    /// Applies one focused cell mutation through Undo and re-resolves serialized paths to avoid stale handles.
    /// </summary>
    /// <param name="wavePresetObject">Serialized wave asset receiving the mutation.</param>
    /// <param name="waveIndex">Selected wave index.</param>
    /// <param name="coordinate">Selected cell coordinate.</param>
    /// <param name="undoName">Designer-facing Undo operation name.</param>
    /// <param name="mutation">Mutation receiving the current wave and cell properties.</param>
    private static void ApplyCellChange(SerializedObject wavePresetObject,
                                        int waveIndex,
                                        Vector2Int coordinate,
                                        string undoName,
                                        Action<SerializedProperty, SerializedProperty> mutation)
    {
        Undo.RecordObject(wavePresetObject.targetObject, undoName);
        wavePresetObject.UpdateIfRequiredOrScript();
        SerializedProperty wave = FindWave(wavePresetObject, waveIndex);
        SerializedProperty cell = FindCell(wave, coordinate);

        if (wave == null || cell == null)
            return;

        mutation(wave, cell);
        wavePresetObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(wavePresetObject.targetObject);
        GameManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Removes one painted cell through the same serialization stream used by the contextual editor.
    /// </summary>
    /// <param name="wavePresetObject">Serialized wave asset receiving the removal.</param>
    /// <param name="waveIndex">Selected wave index.</param>
    /// <param name="coordinate">Selected cell coordinate.</param>
    private static void RemoveCell(SerializedObject wavePresetObject,
                                   int waveIndex,
                                   Vector2Int coordinate)
    {
        Undo.RecordObject(wavePresetObject.targetObject, "Remove Enemy Wave Cell");
        wavePresetObject.UpdateIfRequiredOrScript();
        SerializedProperty wave = FindWave(wavePresetObject, waveIndex);
        SerializedProperty cells = wave == null ? null : wave.FindPropertyRelative("paintedCells");

        if (cells == null)
            return;

        for (int cellIndex = 0; cellIndex < cells.arraySize; cellIndex++)
        {
            if (cells.GetArrayElementAtIndex(cellIndex)
                     .FindPropertyRelative("cellCoordinate")
                     .vector2IntValue != coordinate)
            {
                continue;
            }

            cells.DeleteArrayElementAtIndex(cellIndex);
            wavePresetObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(wavePresetObject.targetObject);
            GameManagementDraftSession.MarkDirty();
            return;
        }
    }
    #endregion

    #region Lookup Methods
    /// <summary>
    /// Resolves one wave by index from the serialized preset.
    /// </summary>
    /// <param name="wavePresetObject">Serialized wave asset.</param>
    /// <param name="waveIndex">Requested wave index.</param>
    /// <returns>Serialized wave, or null when the index is invalid.</returns>
    private static SerializedProperty FindWave(SerializedObject wavePresetObject, int waveIndex)
    {
        if (wavePresetObject == null)
            return null;

        SerializedProperty waves = wavePresetObject.FindProperty("waves");
        return waves == null || waveIndex < 0 || waveIndex >= waves.arraySize
            ? null
            : waves.GetArrayElementAtIndex(waveIndex);
    }

    /// <summary>
    /// Resolves one sparse painted cell by grid coordinate.
    /// </summary>
    /// <param name="wave">Serialized wave containing sparse cells.</param>
    /// <param name="coordinate">Requested cell coordinate.</param>
    /// <returns>Serialized cell, or null when the coordinate is not painted.</returns>
    private static SerializedProperty FindCell(SerializedProperty wave, Vector2Int coordinate)
    {
        SerializedProperty cells = wave == null ? null : wave.FindPropertyRelative("paintedCells");

        if (cells == null)
            return null;

        for (int cellIndex = 0; cellIndex < cells.arraySize; cellIndex++)
        {
            SerializedProperty cell = cells.GetArrayElementAtIndex(cellIndex);

            if (cell.FindPropertyRelative("cellCoordinate").vector2IntValue == coordinate)
                return cell;
        }

        return null;
    }

    /// <summary>
    /// Finds a brush category by its stable technical identifier.
    /// </summary>
    /// <param name="wavesPreset">Preset supplying categories.</param>
    /// <param name="categoryId">Stable identifier to locate.</param>
    /// <returns>Category index, or -1 when unresolved.</returns>
    private static int FindCategoryIndex(GameWavesPreset wavesPreset, string categoryId)
    {
        for (int categoryIndex = 0; categoryIndex < wavesPreset.BrushCategories.Count; categoryIndex++)
        {
            EnemyBrushCategoryDefinition category = wavesPreset.BrushCategories[categoryIndex];

            if (category != null && category.TechnicalId == categoryId)
                return categoryIndex;
        }

        return -1;
    }
    #endregion

    #endregion
}
