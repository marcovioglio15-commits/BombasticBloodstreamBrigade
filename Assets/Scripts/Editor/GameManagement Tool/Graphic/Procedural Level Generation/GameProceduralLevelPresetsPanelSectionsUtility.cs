using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds top-level Procedural Level preset sections and context-sensitive generation presentation fields.
/// </summary>
internal static class GameProceduralLevelPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Selects one preset, restores its stable nested-level selection and builds detail controls.
    /// </summary>
    /// <param name="panel">Panel receiving the selected preset state.</param>
    /// <param name="preset">Preset to edit, or null to display an empty-state message.</param>
    public static void SelectPreset(GameProceduralLevelPresetsPanel panel, GameProceduralLevelPreset preset)
    {
        if (panel == null || panel.DetailsRoot == null)
            return;

        panel.SelectedPreset = preset;
        panel.DetailsRoot.Clear();

        if (panel.PresetListView != null && preset != null)
        {
            int selectedIndex = panel.FilteredPresets.IndexOf(preset);

            if (selectedIndex >= 0)
                panel.PresetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        ManagementToolStateUtility.SaveAssetPath(GameProceduralLevelPresetsPanel.SelectedPresetPathStateKey, preset);

        if (preset == null)
        {
            panel.PresetSerializedObject = null;
            panel.SelectedLevelTechnicalId = string.Empty;
            panel.DetailsRoot.Add(new Label("Select or create a Procedural Level preset to edit."));
            return;
        }

        preset.EnsureInitialized();
        GameRoomMetadataRefreshReport automaticRefreshReport = GameRoomMetadataAutomaticRefreshUtility.RefreshStaleReferencedRooms(preset);
        GameRoomMetadataAutomaticRefreshUtility.MarkDraftDirtyWhenChanged(automaticRefreshReport);

        if (!automaticRefreshReport.Succeeded)
            Debug.LogWarning("[GameRoomMetadata] Preset-load automatic refresh kept invalid rooms stale: " + string.Join(" | ", automaticRefreshReport.Errors));

        panel.PresetSerializedObject = new SerializedObject(preset);
        panel.SelectedLevelTechnicalId = GameProceduralLevelPresetsPanelStateUtility.LoadSelectedLevelTechnicalId(preset);
        GameProceduralLevelPresetsPanelLevelUtility.ResolveSelectedLevel(panel);
        panel.SectionButtonsRoot = BuildSectionButtons(panel);
        panel.SectionContentRoot = new VisualElement();
        panel.SectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.SectionButtonsRoot);
        panel.DetailsRoot.Add(panel.SectionContentRoot);
        BuildActiveSection(panel);
    }

    /// <summary>
    /// Rebuilds only the selected top-level section while preserving preset and nested-level identity.
    /// </summary>
    /// <param name="panel">Panel with a live selected preset and section root.</param>
    public static void BuildActiveSection(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null || panel.SectionContentRoot == null)
            return;

        panel.PresetSerializedObject.UpdateIfRequiredOrScript();
        panel.SectionContentRoot.Clear();

        switch (panel.ActiveSection)
        {
            case GameProceduralLevelPresetsPanel.DetailsSectionType.Metadata:
                BuildMetadataSection(panel);
                break;
            case GameProceduralLevelPresetsPanel.DetailsSectionType.Generation:
                BuildGenerationSection(panel);
                break;
            case GameProceduralLevelPresetsPanel.DetailsSectionType.Transition:
                BuildTransitionSection(panel);
                break;
            case GameProceduralLevelPresetsPanel.DetailsSectionType.Levels:
                GameProceduralLevelPresetsPanelLevelUtility.BuildLevelsSection(panel);
                break;
        }
    }
    #endregion

    #region Section Methods
    /// <summary>
    /// Builds editable preset metadata, scene catalog selection and read-only technical identity.
    /// </summary>
    /// <param name="panel">Panel supplying the serialized preset.</param>
    private static void BuildMetadataSection(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Preset Metadata");
        GameProceduralLevelPresetsPanelFieldUtility.AddDelayedText(section,
                                                                  panel.PresetSerializedObject.FindProperty("presetName"),
                                                                  "Preset Name",
                                                                  "Display name used by the browser and filename applied when the draft is accepted.",
                                                                  false,
                                                                  panel.RefreshPresetList);
        GameProceduralLevelPresetsPanelFieldUtility.AddDelayedText(section,
                                                                  panel.PresetSerializedObject.FindProperty("version"),
                                                                  "Version",
                                                                  "Optional semantic version shown beside this preset in the browser.",
                                                                  false,
                                                                  panel.RefreshPresetList);
        GameProceduralLevelPresetsPanelFieldUtility.AddDelayedText(section,
                                                                  panel.PresetSerializedObject.FindProperty("description"),
                                                                  "Description",
                                                                  "Short designer-facing summary of the ordered level configuration.",
                                                                  true);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    panel.PresetSerializedObject.FindProperty("sceneCatalogPreset"),
                                                                    "Scene Catalog Preset",
                                                                    "Scene Manager preset supplying the enum-like room scene choices and load metadata. It must match the selected Game Master's runtime catalog.",
                                                                    panel.BuildActiveSection);

        Button refreshMetadataButton = new Button(() => GameProceduralLevelMetadataRefreshUiUtility.RefreshReferencedRooms(panel));
        refreshMetadataButton.text = "Refresh All Room Metadata";
        refreshMetadataButton.tooltip = "Scan every unique room referenced by this preset without changing the designer's open-scene setup.";
        refreshMetadataButton.style.width = 164f;
        refreshMetadataButton.style.marginTop = 4f;
        section.Add(refreshMetadataButton);

        SerializedProperty presetIdProperty = panel.PresetSerializedObject.FindProperty("presetId");

        if (presetIdProperty != null)
        {
            PropertyField idField = new PropertyField(presetIdProperty, "Preset Technical ID");
            idField.tooltip = "Immutable technical identity used by editor state and baked references.";
            idField.BindProperty(presetIdProperty);
            idField.SetEnabled(false);
            section.Add(idField);
        }

        if (panel.RuntimeSceneCatalogPreset == null)
            section.Add(CreateInfoLabel("The selected Game Master has no Scene Manager preset, so this procedural preset cannot be previewed or baked.", true));
        else if (panel.SelectedPreset.SceneCatalogPreset == null)
            section.Add(CreateInfoLabel("Assign a Scene Manager preset before adding room scene references.", true));
        else if (!panel.HasCompatibleRuntimeSceneCatalog())
            section.Add(CreateInfoLabel("Scene Catalog mismatch: select the exact Scene Manager preset assigned to the current Game Master. Preview and procedural baking remain disabled until both references match.", true));

        List<string> cacheWarnings = GameRoomMetadataCacheValidationUtility.BuildWarnings(panel.SelectedPreset);

        for (int warningIndex = 0; warningIndex < cacheWarnings.Count; warningIndex++)
            section.Add(CreateInfoLabel(cacheWarnings[warningIndex], true));
    }

    /// <summary>
    /// Builds deterministic seed settings and bounded generation safety limits.
    /// </summary>
    /// <param name="panel">Panel supplying serialized generation settings.</param>
    private static void BuildGenerationSection(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Graph Generation");
        SerializedProperty settingsProperty = panel.PresetSerializedObject.FindProperty("generationSettings");

        if (settingsProperty == null)
            return;

        SerializedProperty seedModeProperty = settingsProperty.FindPropertyRelative("seedMode");
        PropertyField fixedSeedField = null;
        System.Action refreshSeedVisibility = () => RefreshSeedVisibility(seedModeProperty, fixedSeedField);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    seedModeProperty,
                                                                    "Seed Mode",
                                                                    "Chooses random-per-run, fixed preview-compatible or externally supplied run seeds.",
                                                                    refreshSeedVisibility);
        fixedSeedField = GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                                      settingsProperty.FindPropertyRelative("fixedSeed"),
                                                                                      "Fixed Seed",
                                                                                      "Deterministic seed used by runtime generation and editor graph previews in Fixed mode.");

        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    settingsProperty.FindPropertyRelative("maximumNodeCount"),
                                                                    "Maximum Node Count",
                                                                    "Hard safety limit checked by generation without altering authored level ranges.");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    settingsProperty.FindPropertyRelative("maximumDepth"),
                                                                    "Maximum Depth",
                                                                    "Hard graph depth safety limit used to bound solver storage and attempts.");
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    settingsProperty.FindPropertyRelative("maximumGenerationAttempts"),
                                                                    "Maximum Generation Attempts",
                                                                    "Maximum deterministic backtracking attempts before generation reports an explicit failure.");
        refreshSeedVisibility();
    }

    /// <summary>
    /// Builds intra-level fade presentation and player relocation settings with conditional animation fields.
    /// </summary>
    /// <param name="panel">Panel supplying serialized transition settings.</param>
    private static void BuildTransitionSection(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Intra-Level Transition Presentation");
        SerializedProperty settingsProperty = panel.PresetSerializedObject.FindProperty("transitionSettings");

        if (settingsProperty == null)
            return;

        SerializedProperty keepPlayerVisibleProperty = settingsProperty.FindPropertyRelative("keepPlayerVisible");
        SerializedProperty animationProperty = settingsProperty.FindPropertyRelative("playerTransitionAnimation");
        PropertyField animationField = null;
        PropertyField relocationField = null;
        System.Action refreshTransitionVisibility = () => RefreshTransitionVisibility(keepPlayerVisibleProperty,
                                                                                       animationProperty,
                                                                                       animationField,
                                                                                       relocationField);
        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    keepPlayerVisibleProperty,
                                                                    "Keep Player Visible",
                                                                    "Keeps the persistent player presentation above the black environment pass only during room-to-room transitions.",
                                                                    refreshTransitionVisibility);
        animationField = GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                                      animationProperty,
                                                                                      "Player Transition Animation",
                                                                                      "Optional one-shot animation played while the persistent player remains visible during an intra-level transition.",
                                                                                      refreshTransitionVisibility);
        relocationField = GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                                       settingsProperty.FindPropertyRelative("relocationNormalizedTime"),
                                                                                       "Relocation Normalized Time",
                                                                                       "Normalized clip time at which the ready destination arrival pose is applied.");

        GameProceduralLevelPresetsPanelFieldUtility.AddBoundProperty(section,
                                                                    settingsProperty.FindPropertyRelative("clearPlayerVelocity"),
                                                                    "Clear Player Velocity",
                                                                    "Clears persistent player linear and angular velocity when an arrival pose is committed.");
        refreshTransitionVisibility();
    }
    #endregion

    #region Conditional Visibility Methods
    /// <summary>
    /// Shows the fixed seed only while the authored seed policy consumes it.
    /// </summary>
    /// <param name="seedModeProperty">Serialized seed policy controlling visibility.</param>
    /// <param name="fixedSeedField">Fixed seed field whose display state is updated.</param>
    private static void RefreshSeedVisibility(SerializedProperty seedModeProperty, VisualElement fixedSeedField)
    {
        if (seedModeProperty == null || fixedSeedField == null)
            return;

        seedModeProperty.serializedObject.UpdateIfRequiredOrScript();
        fixedSeedField.style.display = seedModeProperty.enumValueIndex == (int)GameProceduralLevelSeedMode.Fixed
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Updates transition-only controls without rebuilding the attached section during a serialized change event.
    /// </summary>
    /// <param name="keepPlayerVisibleProperty">Serialized player visibility toggle.</param>
    /// <param name="animationProperty">Serialized optional transition animation reference.</param>
    /// <param name="animationField">Animation selector controlled by the visibility toggle.</param>
    /// <param name="relocationField">Normalized relocation field controlled by the active animation path.</param>
    private static void RefreshTransitionVisibility(SerializedProperty keepPlayerVisibleProperty,
                                                    SerializedProperty animationProperty,
                                                    VisualElement animationField,
                                                    VisualElement relocationField)
    {
        if (keepPlayerVisibleProperty == null || animationProperty == null || animationField == null || relocationField == null)
            return;

        keepPlayerVisibleProperty.serializedObject.UpdateIfRequiredOrScript();
        bool keepsPlayerVisible = keepPlayerVisibleProperty.boolValue;
        animationField.style.display = keepsPlayerVisible ? DisplayStyle.Flex : DisplayStyle.None;
        relocationField.style.display = keepsPlayerVisible && animationProperty.objectReferenceValue != null
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds the top-level detail section selector row.
    /// </summary>
    /// <param name="panel">Panel whose active section is changed by the buttons.</param>
    /// <returns>Selector row containing every available section.</returns>
    private static VisualElement BuildSectionButtons(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 6f;
        AddSectionButton(panel, row, GameProceduralLevelPresetsPanel.DetailsSectionType.Metadata, "Metadata", 84f);
        AddSectionButton(panel, row, GameProceduralLevelPresetsPanel.DetailsSectionType.Generation, "Generation", 92f);
        AddSectionButton(panel, row, GameProceduralLevelPresetsPanel.DetailsSectionType.Transition, "Transition", 88f);
        AddSectionButton(panel, row, GameProceduralLevelPresetsPanel.DetailsSectionType.Levels, "Levels", 68f);
        return row;
    }

    /// <summary>
    /// Adds one top-level detail section selector with persisted state.
    /// </summary>
    /// <param name="panel">Panel receiving the new active section.</param>
    /// <param name="parent">Selector row receiving the button.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible button text.</param>
    /// <param name="minimumWidth">Minimum readable button width.</param>
    private static void AddSectionButton(GameProceduralLevelPresetsPanel panel,
                                         VisualElement parent,
                                         GameProceduralLevelPresetsPanel.DetailsSectionType sectionType,
                                         string label,
                                         float minimumWidth)
    {
        Button button = new Button(() =>
        {
            panel.ActiveSection = sectionType;
            GameProceduralLevelPresetsPanelStateUtility.SaveActiveSection(sectionType);
            panel.BuildActiveSection();
        });
        button.text = label;
        button.tooltip = "Show Procedural Level " + label + " settings.";
        button.style.flexShrink = 0f;
        button.style.minWidth = minimumWidth;
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Creates a consistently styled section and registers its heading with tool color customization.
    /// </summary>
    /// <param name="panel">Panel whose active content receives the section.</param>
    /// <param name="title">Visible section heading.</param>
    /// <returns>Section container ready for fields.</returns>
    private static VisualElement CreateSection(GameProceduralLevelPresetsPanel panel, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label heading = new Label(title);
        heading.tooltip = "Section header: " + title + ".";
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(heading, "NashCore.GameManagement.ProceduralLevel." + title);
        section.Add(heading);
        panel.SectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Creates a compact explanatory or warning label for conditionally unavailable settings.
    /// </summary>
    /// <param name="text">Message displayed to designers.</param>
    /// <param name="isWarning">True when the label should use the warning color.</param>
    /// <returns>Styled information label.</returns>
    private static Label CreateInfoLabel(string text, bool isWarning)
    {
        Label label = new Label(text);
        label.tooltip = text;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 4f;

        if (isWarning)
            label.style.color = new Color(1f, 0.72f, 0.2f);

        return label;
    }
    #endregion

    #endregion
}
