using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds responsive Scene Brush controls without coupling persistent editor state to the panel layout.
/// </summary>
internal static class GameWavesSceneBrushControlsUtility
{
    #region Constants
    private const float ResponsiveControlMinimumWidth = 190f;
    private const float ResponsiveControlMaximumWidth = 430f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds wave selection, reusable brush selection and preview framing controls in a wrapping foldout.
    /// </summary>
    /// <param name="rootElement">Configuration area receiving the controls.</param>
    /// <param name="wavePreset">Wave asset supplying ordered and parallel choices.</param>
    /// <param name="wavesPreset">Waves preset supplying reusable brush categories.</param>
    /// <param name="selectedWaveIndex">Currently selected flat wave index.</param>
    /// <param name="selectedCategoryIndex">Currently selected brush-category index.</param>
    /// <param name="enemyCount">Enemy amount assigned to newly painted cells.</param>
    /// <param name="erase">Whether left-click removes cells.</param>
    /// <param name="zoom">Current top-down camera framing multiplier.</param>
    /// <param name="waveChanged">Callback receiving a newly selected flat wave index.</param>
    /// <param name="categoryChanged">Callback receiving a newly selected category index.</param>
    /// <param name="enemyCountChanged">Callback receiving the authored brush count.</param>
    /// <param name="eraseChanged">Callback receiving the authored erase state.</param>
    /// <param name="zoomChanged">Callback receiving preview framing changes.</param>
    public static void BuildPaintingControls(VisualElement rootElement,
                                             EnemyWavePreset wavePreset,
                                             GameWavesPreset wavesPreset,
                                             int selectedWaveIndex,
                                             int selectedCategoryIndex,
                                             int enemyCount,
                                             bool erase,
                                             float zoom,
                                             Action<int> waveChanged,
                                             Action<int> categoryChanged,
                                             Action<int> enemyCountChanged,
                                             Action<bool> eraseChanged,
                                             Action<float> zoomChanged)
    {
        Foldout foldout = CreateFoldout("Painting Controls", true,
                                        "Select the visible wave, brush payload and bounded top-down framing.");
        VisualElement row = CreateWrappingRow();
        GameWavesPanelUiUtility.AddWaveSequenceSelectors(row,
                                                         wavePreset,
                                                         selectedWaveIndex,
                                                         waveChanged);

        PopupField<string> categoryPopup = new PopupField<string>(
            "Brush",
            GameWavesPanelUiUtility.BuildCategoryChoices(wavesPreset),
            GameWavesPanelUiUtility.ClampIndex(selectedCategoryIndex,
                                                wavesPreset.BrushCategories.Count));
        categoryPopup.tooltip = "Reusable category painted into a cell; runtime selects one eligible weighted enemy preset.";
        ConfigureResponsiveControl(categoryPopup);
        categoryPopup.RegisterValueChangedCallback(evt =>
        {
            if (categoryChanged != null)
                categoryChanged(categoryPopup.index);
        });
        row.Add(categoryPopup);

        IntegerField countField = new IntegerField("Count")
        {
            value = enemyCount,
            isDelayed = true
        };
        countField.tooltip = "Enemy amount emitted from each newly painted logical cell.";
        ConfigureResponsiveControl(countField);
        countField.RegisterValueChangedCallback(evt =>
        {
            if (enemyCountChanged != null)
                enemyCountChanged(evt.newValue);
        });
        row.Add(countField);

        Toggle eraseToggle = new Toggle("Erase") { value = erase };
        eraseToggle.tooltip = "Remove cells with left click. Holding Shift temporarily erases while Paint is active.";
        ConfigureResponsiveControl(eraseToggle);
        eraseToggle.RegisterValueChangedCallback(evt =>
        {
            if (eraseChanged != null)
                eraseChanged(evt.newValue);
        });
        row.Add(eraseToggle);

        Slider zoomSlider = new Slider("Zoom", 0.35f, 4f)
        {
            value = zoom,
            showInputField = true
        };
        zoomSlider.tooltip = "Frame more of the room below 1, or magnify around the selected cell above 1. Fit restores the complete grid.";
        ConfigureResponsiveControl(zoomSlider);
        zoomSlider.RegisterValueChangedCallback(evt =>
        {
            if (zoomChanged != null)
                zoomChanged(evt.newValue);
        });
        row.Add(zoomSlider);

        Button fitButton = GameWavesPanelUiUtility.CreateToolbarButton(
            "Fit",
            "Restore the complete grid framing at zoom 1.",
            () =>
            {
                zoomSlider.SetValueWithoutNotify(1f);

                if (zoomChanged != null)
                    zoomChanged(1f);
            });
        fitButton.style.flexShrink = 0f;
        row.Add(fitButton);
        foldout.Add(row);
        rootElement.Add(foldout);
    }

    /// <summary>
    /// Builds timing and optional difficulty fields for only the wave visible in the Scene Brush preview.
    /// </summary>
    /// <param name="rootElement">Configuration area receiving the selected-wave foldout.</param>
    /// <param name="waveSerializedObject">Serialized Enemy Wave preset receiving authored values.</param>
    /// <param name="selectedWaveIndex">Flat wave index currently visible in the preview.</param>
    /// <param name="addWave">Action creating the first wave when the preset is empty.</param>
    public static void BuildSelectedWaveSettings(VisualElement rootElement,
                                                 SerializedObject waveSerializedObject,
                                                 int selectedWaveIndex,
                                                 Action addWave)
    {
        SerializedProperty waves = waveSerializedObject.FindProperty("waves");

        if (waves == null || waves.arraySize == 0)
        {
            Button addWaveButton = new Button(addWave) { text = "Add First Wave" };
            addWaveButton.tooltip = "Create the first independently schedulable wave for this room.";
            rootElement.Add(addWaveButton);
            return;
        }

        selectedWaveIndex = GameWavesPanelUiUtility.ClampIndex(selectedWaveIndex, waves.arraySize);
        SerializedProperty wave = waves.GetArrayElementAtIndex(selectedWaveIndex);
        Foldout settings = CreateFoldout(
            GameWavesPanelUiUtility.BuildWaveSelectionContext(
                waveSerializedObject.targetObject as EnemyWavePreset,
                selectedWaveIndex),
            false,
            "Edit timing and difficulty selection for only the wave visible in the preview.");
        GameWavesPanelUiUtility.AddBoundWaveProperty(settings,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("waveLabel"),
                                                     "Label");
        GameWavesPanelUiUtility.AddBoundWaveProperty(settings,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("startMode"),
                                                     "Start Condition");
        GameWavesPanelUiUtility.AddBoundWaveProperty(settings,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("startDelaySeconds"),
                                                     "Start Delay Seconds");
        GameWavesPanelUiUtility.AddBoundWaveProperty(settings,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("spawnDurationSeconds"),
                                                     "Spawn Duration Seconds");
        GameWavesPanelUiUtility.AddBoundWaveProperty(settings,
                                                     waveSerializedObject,
                                                     wave.FindPropertyRelative("defaultDistributionCurve"),
                                                     "Distribution Curve");
        GameWavesSequenceEditorUtility.AddDifficultyFields(settings, waveSerializedObject, wave);
        rootElement.Add(settings);
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Creates one non-compressing thematic foldout with an explanatory tooltip.
    /// </summary>
    /// <param name="text">Foldout heading.</param>
    /// <param name="expanded">Initial expansion state.</param>
    /// <param name="tooltip">Designer-facing section description.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string text, bool expanded, string tooltip)
    {
        Foldout foldout = new Foldout
        {
            text = text,
            value = expanded,
            tooltip = tooltip
        };
        foldout.style.flexShrink = 0f;
        foldout.style.minWidth = 0f;
        return foldout;
    }

    /// <summary>
    /// Creates one row that grows vertically when horizontal space is constrained.
    /// </summary>
    /// <returns>Responsive row container.</returns>
    private static VisualElement CreateWrappingRow()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.alignItems = Align.FlexStart;
        row.style.minWidth = 0f;
        return row;
    }

    /// <summary>
    /// Gives one labeled control readable bounds while allowing it to share or wrap a narrow row.
    /// </summary>
    /// <param name="control">UI Toolkit field receiving responsive sizing.</param>
    private static void ConfigureResponsiveControl(VisualElement control)
    {
        control.style.flexGrow = 1f;
        control.style.flexShrink = 1f;
        control.style.minWidth = ResponsiveControlMinimumWidth;
        control.style.maxWidth = ResponsiveControlMaximumWidth;
    }
    #endregion

    #endregion
}
