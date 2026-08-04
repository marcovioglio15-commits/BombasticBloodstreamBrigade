using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds transactional grid and spawn-warning controls for the unique spawner mapped to Scene Brush.
/// </summary>
internal static class GameWavesSpawnerSettingsEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds thematic spawner-setting foldouts backed by an editor-only draft committed through Apply.
    /// </summary>
    /// <param name="rootElement">Configuration area receiving the spawner controls.</param>
    /// <param name="draft">Transactional settings copied from the mapped SubScene spawner.</param>
    /// <param name="gridChanged">Callback refreshing grid geometry and removing cells made invalid by a valid resize.</param>
    public static void Build(VisualElement rootElement,
                             GameWavesSpawnerSettingsDraft draft,
                             Action gridChanged)
    {
        SerializedObject serializedDraft = new SerializedObject(draft);
        BuildGridSettings(rootElement, serializedDraft, draft, gridChanged);
        BuildSpawnWarningSettings(rootElement, serializedDraft, draft);
    }
    #endregion

    #region Grid Methods
    /// <summary>
    /// Builds grid dimensions, cell sizing and local placement fields with non-destructive validation warnings.
    /// </summary>
    /// <param name="rootElement">Configuration area receiving the grid foldout.</param>
    /// <param name="serializedDraft">Serialized transactional settings.</param>
    /// <param name="draft">Typed settings used by validation and preview callbacks.</param>
    /// <param name="gridChanged">Callback invoked after a genuine grid edit has been serialized.</param>
    private static void BuildGridSettings(VisualElement rootElement,
                                          SerializedObject serializedDraft,
                                          GameWavesSpawnerSettingsDraft draft,
                                          Action gridChanged)
    {
        Foldout foldout = CreateFoldout(
            "Grid Settings",
            false,
            "Edit the mapped spawner grid transactionally. Valid reductions remove cells outside the new bounds from every wave.");
        VisualElement warnings = new VisualElement();
        AddDraftField(foldout,
                      serializedDraft,
                      "gridSizeX",
                      "Horizontal Cells",
                      () => RefreshGridState(foldout, serializedDraft, draft, warnings, gridChanged));
        AddDraftField(foldout,
                      serializedDraft,
                      "gridSizeZ",
                      "Vertical Cells",
                      () => RefreshGridState(foldout, serializedDraft, draft, warnings, gridChanged));
        AddDraftField(foldout,
                      serializedDraft,
                      "cellSize",
                      "Cell Size",
                      () => RefreshGridState(foldout, serializedDraft, draft, warnings, gridChanged));
        AddDraftField(foldout,
                      serializedDraft,
                      "originOffset",
                      "Origin Offset",
                      () => RefreshGridState(foldout, serializedDraft, draft, warnings, gridChanged));
        AddDraftField(foldout,
                      serializedDraft,
                      "spawnHeightOffset",
                      "Spawn Height Offset",
                      () => RefreshGridState(foldout, serializedDraft, draft, warnings, gridChanged));
        foldout.Add(warnings);
        RefreshGridWarnings(warnings, draft);
        rootElement.Add(foldout);
    }

    /// <summary>
    /// Refreshes draft serialization, validation output and preview geometry after one grid field changes.
    /// </summary>
    /// <param name="schedulerOwner">Element scheduling work after binding has committed the new value.</param>
    /// <param name="serializedDraft">Serialized transactional settings.</param>
    /// <param name="draft">Typed settings used by validation and preview callbacks.</param>
    /// <param name="warnings">Container receiving current validation messages.</param>
    /// <param name="gridChanged">Callback refreshing the mapped wave and preview.</param>
    private static void RefreshGridState(VisualElement schedulerOwner,
                                         SerializedObject serializedDraft,
                                         GameWavesSpawnerSettingsDraft draft,
                                         VisualElement warnings,
                                         Action gridChanged)
    {
        schedulerOwner.schedule.Execute(() =>
        {
            serializedDraft.UpdateIfRequiredOrScript();
            RefreshGridWarnings(warnings, draft);

            if (gridChanged != null)
                gridChanged();
        });
    }

    /// <summary>
    /// Reports invalid grid values without coercing designer-authored input.
    /// </summary>
    /// <param name="warnings">Container receiving current validation messages.</param>
    /// <param name="draft">Typed grid settings to validate.</param>
    private static void RefreshGridWarnings(VisualElement warnings, GameWavesSpawnerSettingsDraft draft)
    {
        warnings.Clear();

        if (draft.GridSizeX <= 0 || draft.GridSizeZ <= 0)
        {
            warnings.Add(new HelpBox("Horizontal Cells and Vertical Cells must both be greater than zero. " +
                                     "Invalid dimensions are not applied to cell cleanup or runtime preview geometry.",
                                     HelpBoxMessageType.Warning));
        }

        if (draft.CellSize <= 0f)
        {
            warnings.Add(new HelpBox("Cell Size must be greater than zero before the spawner can bake valid positions.",
                                     HelpBoxMessageType.Warning));
        }
    }
    #endregion

    #region Spawn Warning Methods
    /// <summary>
    /// Builds conditional spawn-warning controls and keeps irrelevant fields hidden while warnings are disabled.
    /// </summary>
    /// <param name="rootElement">Configuration area receiving the warning foldout.</param>
    /// <param name="serializedDraft">Serialized transactional settings.</param>
    /// <param name="draft">Typed settings used by conditional display and validation.</param>
    private static void BuildSpawnWarningSettings(VisualElement rootElement,
                                                  SerializedObject serializedDraft,
                                                  GameWavesSpawnerSettingsDraft draft)
    {
        Foldout foldout = CreateFoldout(
            "Spawn Warning",
            false,
            "Configure the spawner-level warning used when an enemy presentation does not provide an override.");
        VisualElement conditionalFields = new VisualElement();
        VisualElement warnings = new VisualElement();
        PropertyField enabledField = AddDraftField(foldout,
                                                   serializedDraft,
                                                   "enableSpawnWarning",
                                                   "Enabled",
                                                   () => RefreshSpawnWarningState(foldout,
                                                                                 serializedDraft,
                                                                                 draft,
                                                                                 conditionalFields,
                                                                                 warnings));
        enabledField.tooltip = "Enable the spawner-level fallback warning before enemy activation.";
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningLeadTimeSeconds",
                      "Lead Time Seconds",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningRadiusScale",
                      "Radius Scale",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningRingWidth",
                      "Ring Width",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningHeightOffset",
                      "Height Offset",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningMaximumAlpha",
                      "Maximum Alpha",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningFadeOutSeconds",
                      "Fade Out Seconds",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        AddDraftField(conditionalFields,
                      serializedDraft,
                      "spawnWarningColor",
                      "Color",
                      () => RefreshSpawnWarningState(foldout,
                                                    serializedDraft,
                                                    draft,
                                                    conditionalFields,
                                                    warnings));
        conditionalFields.Add(warnings);
        foldout.Add(conditionalFields);
        RefreshSpawnWarningDisplay(conditionalFields, warnings, draft);
        rootElement.Add(foldout);
    }

    /// <summary>
    /// Refreshes conditional warning fields and their validation after serialized binding commits an edit.
    /// </summary>
    /// <param name="schedulerOwner">Element scheduling work after binding has committed the new value.</param>
    /// <param name="serializedDraft">Serialized transactional settings.</param>
    /// <param name="draft">Typed settings used by conditional display and validation.</param>
    /// <param name="conditionalFields">Container hidden while warnings are disabled.</param>
    /// <param name="warnings">Container receiving current warning messages.</param>
    private static void RefreshSpawnWarningState(VisualElement schedulerOwner,
                                                 SerializedObject serializedDraft,
                                                 GameWavesSpawnerSettingsDraft draft,
                                                 VisualElement conditionalFields,
                                                 VisualElement warnings)
    {
        schedulerOwner.schedule.Execute(() =>
        {
            serializedDraft.UpdateIfRequiredOrScript();
            RefreshSpawnWarningDisplay(conditionalFields, warnings, draft);
        });
    }

    /// <summary>
    /// Applies intelligent visibility and reports invalid warning ranges without rewriting source values.
    /// </summary>
    /// <param name="conditionalFields">Container hidden while warnings are disabled.</param>
    /// <param name="warnings">Container receiving current warning messages.</param>
    /// <param name="draft">Typed warning settings to inspect.</param>
    private static void RefreshSpawnWarningDisplay(VisualElement conditionalFields,
                                                   VisualElement warnings,
                                                   GameWavesSpawnerSettingsDraft draft)
    {
        conditionalFields.style.display = draft.EnableSpawnWarning ? DisplayStyle.Flex : DisplayStyle.None;
        warnings.Clear();

        if (!draft.EnableSpawnWarning)
            return;

        if (draft.SpawnWarningLeadTimeSeconds < 0f || draft.SpawnWarningLeadTimeSeconds > 3f)
            warnings.Add(CreateRangeWarning("Lead Time Seconds", "0 to 3"));

        if (draft.SpawnWarningRadiusScale < 0.1f || draft.SpawnWarningRadiusScale > 2f)
            warnings.Add(CreateRangeWarning("Radius Scale", "0.1 to 2"));

        if (draft.SpawnWarningRingWidth < 0.02f || draft.SpawnWarningRingWidth > 1f)
            warnings.Add(CreateRangeWarning("Ring Width", "0.02 to 1"));

        if (draft.SpawnWarningHeightOffset < 0f || draft.SpawnWarningHeightOffset > 1f)
            warnings.Add(CreateRangeWarning("Height Offset", "0 to 1"));

        if (draft.SpawnWarningMaximumAlpha < 0f || draft.SpawnWarningMaximumAlpha > 1f)
            warnings.Add(CreateRangeWarning("Maximum Alpha", "0 to 1"));

        if (draft.SpawnWarningFadeOutSeconds < 0f || draft.SpawnWarningFadeOutSeconds > 1f)
            warnings.Add(CreateRangeWarning("Fade Out Seconds", "0 to 1"));
    }
    #endregion

    #region Field and Layout Methods
    /// <summary>
    /// Adds one bound draft field and schedules its dependent refresh only after a genuine serialized edit.
    /// </summary>
    /// <param name="rootElement">Container receiving the field.</param>
    /// <param name="serializedDraft">Serialized transactional settings.</param>
    /// <param name="propertyName">Private serialized field name.</param>
    /// <param name="label">Designer-facing control label.</param>
    /// <param name="changed">Refresh action invoked after binding commits the value.</param>
    /// <returns>Created property field.</returns>
    private static PropertyField AddDraftField(VisualElement rootElement,
                                               SerializedObject serializedDraft,
                                               string propertyName,
                                               string label,
                                               Action changed)
    {
        SerializedProperty property = serializedDraft.FindProperty(propertyName);
        PropertyField field = GameWavesPanelUiUtility.AddBoundProperty(rootElement, property, label);
        field.RegisterValueChangeCallback(evt =>
        {
            if (changed != null)
                changed();
        });
        return field;
    }

    /// <summary>
    /// Creates one non-compressing settings foldout with an explanatory tooltip.
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
    /// Creates one consistent warning for an authored value outside its supported range.
    /// </summary>
    /// <param name="label">Designer-facing field label.</param>
    /// <param name="range">Readable supported range.</param>
    /// <returns>Configured warning box.</returns>
    private static HelpBox CreateRangeWarning(string label, string range)
    {
        return new HelpBox(label + " is outside its supported range of " + range +
                           ". The tool preserves the authored value until it is corrected.",
                           HelpBoxMessageType.Warning);
    }
    #endregion

    #endregion
}
