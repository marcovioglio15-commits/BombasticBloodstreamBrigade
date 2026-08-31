using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds Scene Manager preset detail sections, validation output and setup actions.
/// </summary>
internal static class GameSceneManagerPresetsPanelSectionsUtility
{
    #region Constants
    private const string ActiveSectionStateKey = "NashCore.GameManagement.SceneManager.ActiveSection";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the persisted active Scene Manager details section.
    /// </summary>
    /// <returns>Persisted section value or Metadata when none exists.</returns>
    public static GameSceneManagerPresetsPanel.DetailsSectionType LoadActiveSection()
    {
        return ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, GameSceneManagerPresetsPanel.DetailsSectionType.Metadata);
    }

    /// <summary>
    /// Selects one Scene Manager preset and rebuilds details.
    /// </summary>
    /// <param name="panel">Owning panel with detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    public static void SelectPreset(GameSceneManagerPresetsPanel panel, GameSceneManagerPreset preset)
    {
        if (panel == null || panel.DetailsRoot == null)
            return;

        panel.SelectedPreset = preset;
        // Persist this side panel's own selection so close/reopen lands on the same preset
        // independently from the master preset that drove the previous workflow.
        ManagementToolStateUtility.SaveAssetPath(GameSceneManagerPresetsPanel.SelectedPresetPathStateKey, preset);
        panel.DetailsRoot.Clear();

        if (panel.PresetListView != null && panel.SelectedPreset != null)
        {
            int selectedIndex = panel.FilteredPresets.IndexOf(panel.SelectedPreset);

            if (selectedIndex >= 0)
                panel.PresetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        if (panel.SelectedPreset == null)
        {
            panel.DetailsRoot.Add(new Label("Select or create a Scene Manager preset to edit."));
            return;
        }

        panel.SelectedPreset.EnsureInitialized();
        panel.PresetSerializedObject = new SerializedObject(panel.SelectedPreset);
        panel.SectionButtonsRoot = BuildSectionButtons(panel);
        panel.SectionContentRoot = new VisualElement();
        panel.SectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.SectionButtonsRoot);
        panel.DetailsRoot.Add(panel.SectionContentRoot);
        BuildActiveSection(panel);
    }

    /// <summary>
    /// Rebuilds the currently selected Scene Manager details section.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    public static void BuildActiveSection(GameSceneManagerPresetsPanel panel)
    {
        if (panel == null || panel.SectionContentRoot == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.SectionContentRoot.Clear();

        switch (panel.ActiveSection)
        {
            case GameSceneManagerPresetsPanel.DetailsSectionType.Startup:
                BuildStartupSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.SceneTable:
                BuildSceneTableSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Transitions:
                BuildTransitionsSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Fade:
                BuildPropertySection(panel, "Fade", "fadeSettings", "Default fade timing and visual settings for scene transitions.");
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.LoadingProgress:
                BuildPropertySection(panel, "Loading Progress", "loadingProgressSettings", "Circular loading-progress indicator shown during black-screen transition loading.");
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.GameplayCamera:
                BuildGameplayCameraSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.BuildFeatures:
                BuildBuildFeaturesSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Triggers:
                BuildTriggerSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Validation:
                BuildValidationSection(panel);
                break;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Addressables:
                BuildAddressablesSection(panel);
                break;
            default:
                BuildMetadataSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.SectionContentRoot);
    }

    /// <summary>
    /// Marks the selected Scene Manager preset dirty in the draft session.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    public static void MarkSelectedPresetDirty(GameSceneManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(panel.SelectedPreset);
        GameManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds metadata fields for the selected Scene Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildMetadataSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Preset Details");
        AddBoundTextField(panel, section, "Preset Name", "presetName", true, false);
        AddBoundTextField(panel, section, "Version", "version", false, false);
        AddBoundTextField(panel, section, "Description", "description", false, true);

        SerializedProperty idProperty = panel.PresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        PropertyField idField = new PropertyField(idProperty, "Preset ID");
        idField.tooltip = "Stable ID used by Game Management Tool for this Scene Manager preset.";
        idField.BindProperty(idProperty);
        idField.SetEnabled(false);
        section.Add(idField);
    }

    /// <summary>
    /// Builds startup scene flow and backend controls.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildStartupSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Startup Flow");
        AddPropertyField(panel, section, "bootstrapSceneId", "Scene ID for the persistent bootstrap scene.");
        AddPropertyField(panel, section, "initialSceneId", "Scene ID loaded automatically after bootstrap.");
        AddPropertyField(panel, section, "mainMenuSceneId", "Scene ID loaded by main menu commands.");
        AddPropertyField(panel, section, "defaultGameplaySceneId", "Scene ID loaded by the default Play command.");
        AddPropertyField(panel, section, "autoLoadInitialScene", "Automatically load Initial Scene Id after bootstrap.");
        AddPropertyField(panel, section, "loadBackend", "Scene loading backend used by non-bootstrap managed scenes.");
        AddPropertyField(panel, section, "logTransitions", "Log runtime scene transition lifecycle messages.");
    }

    /// <summary>
    /// Builds scene table controls and build settings maintenance actions.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildSceneTableSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Scene Table");
        section.Add(BuildSceneTableToolbar(panel));
        AddPropertyField(panel, section, "sceneDefinitions", "Ordered managed scenes. SceneAsset entries synchronize runtime metadata.");
    }

    /// <summary>
    /// Builds transition graph controls.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildTransitionsSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Transitions");
        AddPropertyField(panel, section, "transitionDefinitions", "Directed transition graph for UI commands, scripted requests and trigger volumes.");
    }

    /// <summary>
    /// Builds a simple single-property section for grouped preset settings.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="title">Section title.</param>
    /// <param name="propertyName">Serialized property shown in the section.</param>
    /// <param name="tooltip">Tooltip applied to the generated property field.</param>
    private static void BuildPropertySection(GameSceneManagerPresetsPanel panel, string title, string propertyName, string tooltip)
    {
        VisualElement section = CreateSection(panel, title);
        AddPropertyField(panel, section, propertyName, tooltip);
    }

    /// <summary>
    /// Builds gameplay-camera presentation controls baked by the selected Scene Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildGameplayCameraSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Gameplay Camera");
        AddPropertyField(panel,
                         section,
                         "enablePlayerCameraOcclusion",
                         "Hide environment renderers that block the camera's view of the player while preserving collision and simulation.");
        SerializedProperty enableBoundariesProperty = panel.PresetSerializedObject.FindProperty("enableCameraBoundaries");
        SerializedProperty boundaryModeProperty = panel.PresetSerializedObject.FindProperty("cameraBoundaryMode");
        SerializedProperty softZoneProperty = panel.PresetSerializedObject.FindProperty("cameraBoundarySoftZoneDistance");

        if (enableBoundariesProperty == null || boundaryModeProperty == null || softZoneProperty == null)
            return;

        PropertyField enableBoundariesField = new PropertyField(enableBoundariesProperty);
        enableBoundariesField.tooltip = "Enable camera constraints from authored Camera Boundary footprints.";
        enableBoundariesField.BindProperty(enableBoundariesProperty);
        section.Add(enableBoundariesField);

        PropertyField boundaryModeField = new PropertyField(boundaryModeProperty);
        boundaryModeField.tooltip = "Keep the camera inside the player-selected volume or treat every footprint as an impassable obstacle.";
        boundaryModeField.BindProperty(boundaryModeProperty);
        section.Add(boundaryModeField);

        PropertyField softZoneField = new PropertyField(softZoneProperty);
        softZoneField.tooltip = "Set the braking distance used before the camera reaches a hard boundary edge.";
        softZoneField.BindProperty(softZoneProperty);
        section.Add(softZoneField);

        enableBoundariesField.RegisterCallback<SerializedPropertyChangeEvent>(changeEvent =>
        {
            panel.MarkSelectedPresetDirty();
            RefreshCameraBoundaryFieldVisibility(enableBoundariesProperty, boundaryModeField, softZoneField);
        });
        boundaryModeField.RegisterCallback<SerializedPropertyChangeEvent>(changeEvent => panel.MarkSelectedPresetDirty());
        softZoneField.RegisterCallback<SerializedPropertyChangeEvent>(changeEvent => panel.MarkSelectedPresetDirty());
        RefreshCameraBoundaryFieldVisibility(enableBoundariesProperty, boundaryModeField, softZoneField);
    }

    /// <summary>
    /// Shows boundary braking controls only while camera boundaries are enabled.
    /// </summary>
    /// <param name="enableBoundariesProperty">Serialized toggle controlling boundary runtime support.</param>
    /// <param name="boundaryModeField">Dependent boundary-mode field.</param>
    /// <param name="softZoneField">Dependent braking-distance field.</param>
    private static void RefreshCameraBoundaryFieldVisibility(SerializedProperty enableBoundariesProperty,
                                                             VisualElement boundaryModeField,
                                                             VisualElement softZoneField)
    {
        DisplayStyle display = enableBoundariesProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        boundaryModeField.style.display = display;
        softZoneField.style.display = display;
    }

    /// <summary>
    /// Builds project-wide player-build feature switches that are independent from the selected preset draft.
    /// </summary>
    /// <param name="panel">Owning panel receiving the project build controls.</param>
    private static void BuildBuildFeaturesSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Build Features");
        Toggle excludeSpawnerToolToggle = new Toggle("Exclude Runtime Enemy Spawner Tool From Player Builds");
        excludeSpawnerToolToggle.tooltip = "When enabled, player builds omit the main-menu button, authored panel hierarchy, runtime catalog, ECS override buffers, and all executable runtime-spawner test logic.";
        excludeSpawnerToolToggle.SetValueWithoutNotify(EnemySpawnerRuntimeToolBuildFeatureUtility.IsExcludedFromPlayerBuilds);
        excludeSpawnerToolToggle.RegisterValueChangedCallback(changeEvent =>
            EnemySpawnerRuntimeToolBuildFeatureUtility.SetExcludedFromPlayerBuilds(changeEvent.newValue));
        section.Add(excludeSpawnerToolToggle);

        HelpBox scopeBox = new HelpBox("This project-wide switch applies immediately and is not part of the selected preset draft. The tool remains available in Editor for test-scene authoring; excluded player builds contain neither its UI nor its runtime data and code.",
                                       HelpBoxMessageType.Info);
        section.Add(scopeBox);
    }

    /// <summary>
    /// Builds trigger defaults and layer maintenance controls.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildTriggerSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Trigger Authoring");
        AddPropertyField(panel, section, "triggerSettings", "Shared trigger defaults and expected transition layer.");
        GameSceneTriggerSettings triggerSettings = panel.SelectedPreset != null ? panel.SelectedPreset.TriggerSettings : null;
        string layerName = triggerSettings != null ? triggerSettings.TransitionLayerName : string.Empty;

        if (GameSceneTransitionLayerUtility.LayerExists(layerName))
        {
            HelpBox cleanBox = new HelpBox("Transition layer exists: " + layerName + ".", HelpBoxMessageType.Info);
            section.Add(cleanBox);
            return;
        }

        HelpBox warningBox = new HelpBox("Transition layer is missing: " + layerName + ".", HelpBoxMessageType.Warning);
        section.Add(warningBox);

        Button createLayerButton = new Button(() => CreateTransitionLayer(panel, layerName));
        createLayerButton.text = "Create Transition Layer";
        createLayerButton.tooltip = "Create the configured transition layer in Project Settings.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createLayerButton, 156f);
        section.Add(createLayerButton);
    }

    /// <summary>
    /// Builds Addressables status and future backend controls.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void BuildAddressablesSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Addressables");
        HelpBox infoBox = new HelpBox("Addressables backend loads non-bootstrap managed scenes through Addressables. DOTS SubScenes referenced by those scenes are registered into player builds through the Scene Manager build-additions hook.", HelpBoxMessageType.Info);
        section.Add(infoBox);

        Button syncButton = new Button(() => SynchronizeAddressableScenes(panel));
        syncButton.text = "Sync Addressable Scenes";
        syncButton.tooltip = "Create Addressables settings and register every non-bootstrap managed scene with its authored key.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(syncButton, 176f);
        section.Add(syncButton);

        AddPropertyField(panel, section, "loadBackend", "Select Addressables to load managed scenes through Addressables.");
        AddPropertyField(panel, section, "sceneDefinitions", "Scene entries include Addressables keys for managed scenes. Build Settings indices are only required by the active Build Settings backend.");

        panel.ValidationWarnings.Clear();
        GameSceneAddressablesEditorUtility.CollectWarnings(panel.SelectedPreset, panel.ValidationWarnings);

        for (int index = 0; index < panel.ValidationWarnings.Count; index++)
        {
            HelpBox warningBox = new HelpBox(panel.ValidationWarnings[index], HelpBoxMessageType.Warning);
            section.Add(warningBox);
        }
    }

    /// <summary>
    /// Builds non-mutating validation warning output.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset and warning buffer.</param>
    private static void BuildValidationSection(GameSceneManagerPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Validation");
        Button refreshButton = new Button(panel.BuildActiveSection);
        refreshButton.text = "Refresh";
        refreshButton.tooltip = "Refresh non-mutating Scene Manager validation warnings.";
        section.Add(refreshButton);

        GameSceneManagerPresetValidationUtility.CollectWarnings(panel.SelectedPreset, panel.ValidationWarnings);
        GameSceneManagementBuildSettingsUtility.CollectBuildSettingsWarnings(panel.SelectedPreset, panel.ValidationWarnings);
        GameSceneAddressablesEditorUtility.CollectWarnings(panel.SelectedPreset, panel.ValidationWarnings);
        GameSceneTransitionLayerUtility.CollectLayerWarnings(panel.SelectedPreset, panel.ValidationWarnings);

        if (panel.ValidationWarnings.Count <= 0)
        {
            Label cleanLabel = new Label("No warnings.");
            cleanLabel.tooltip = "The selected Scene Manager preset has no validation warnings.";
            section.Add(cleanLabel);
            return;
        }

        for (int index = 0; index < panel.ValidationWarnings.Count; index++)
        {
            HelpBox warningBox = new HelpBox(panel.ValidationWarnings[index], HelpBoxMessageType.Warning);
            section.Add(warningBox);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds buttons for Scene Manager detail sections.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active section.</param>
    /// <returns>Section button row.</returns>
    private static VisualElement BuildSectionButtons(GameSceneManagerPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Startup, "Startup");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.SceneTable, "Scene Table");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Transitions, "Transitions");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Fade, "Fade");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.LoadingProgress, "Loading Progress");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.GameplayCamera, "Gameplay Camera");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.BuildFeatures, "Build Features");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Triggers, "Triggers");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Validation, "Validation");
        AddSectionButton(panel, buttonsRoot, GameSceneManagerPresetsPanel.DetailsSectionType.Addressables, "Addressables");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one Scene Manager detail section selector button.
    /// </summary>
    /// <param name="panel">Owning panel receiving the selected section.</param>
    /// <param name="parent">Parent button row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible label.</param>
    private static void AddSectionButton(GameSceneManagerPresetsPanel panel,
                                         VisualElement parent,
                                         GameSceneManagerPresetsPanel.DetailsSectionType sectionType,
                                         string label)
    {
        Button button = new Button(() =>
        {
            panel.ActiveSection = sectionType;
            ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, panel.ActiveSection);
            BuildActiveSection(panel);
        });
        button.text = label;
        button.tooltip = "Show the " + label + " section.";
        button.style.flexShrink = 0f;
        button.style.minWidth = ResolveSectionButtonWidth(sectionType);
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Resolves a stable minimum width for Scene Manager section buttons.
    /// </summary>
    /// <param name="sectionType">Section represented by the selector button.</param>
    /// <returns>Minimum width that keeps the label readable before wrapping to a new row.</returns>
    private static float ResolveSectionButtonWidth(GameSceneManagerPresetsPanel.DetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case GameSceneManagerPresetsPanel.DetailsSectionType.SceneTable:
                return 96f;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Addressables:
                return 112f;
            case GameSceneManagerPresetsPanel.DetailsSectionType.LoadingProgress:
                return 136f;
            case GameSceneManagerPresetsPanel.DetailsSectionType.GameplayCamera:
                return 132f;
            case GameSceneManagerPresetsPanel.DetailsSectionType.BuildFeatures:
                return 112f;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Transitions:
                return 96f;
            case GameSceneManagerPresetsPanel.DetailsSectionType.Validation:
                return 88f;
            default:
                return 84f;
        }
    }

    /// <summary>
    /// Creates a styled section container and registers its heading for recolor utilities.
    /// </summary>
    /// <param name="panel">Owning panel with active details content root.</param>
    /// <param name="title">Section title.</param>
    /// <returns>Section container.</returns>
    private static VisualElement CreateSection(GameSceneManagerPresetsPanel panel, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.SceneManager." + title);
        section.Add(label);
        panel.SectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Adds one property field and marks the draft dirty on serialized edits.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="tooltip">Field tooltip.</param>
    private static void AddPropertyField(GameSceneManagerPresetsPanel panel, VisualElement parent, string propertyName, string tooltip)
    {
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.isExpanded = true;
        PropertyField field = new PropertyField(property);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => panel.MarkSelectedPresetDirty());
        parent.Add(field);
    }

    /// <summary>
    /// Adds one bound text field and marks the draft dirty on edit.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="label">Display label.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="refreshList">True when list labels should update after change.</param>
    /// <param name="multiline">True when multiline editing is enabled.</param>
    private static void AddBoundTextField(GameSceneManagerPresetsPanel panel,
                                          VisualElement parent,
                                          string label,
                                          string propertyName,
                                          bool refreshList,
                                          bool multiline)
    {
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        TextField field = new TextField(label);
        field.tooltip = "Edit " + label + " for this Scene Manager preset.";
        field.isDelayed = true;
        field.multiline = multiline;
        field.BindProperty(property);
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(panel.SelectedPreset, "Edit Scene Manager Preset");
            panel.PresetSerializedObject.ApplyModifiedProperties();
            panel.MarkSelectedPresetDirty();

            if (refreshList)
                panel.RefreshPresetList();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Builds scene table maintenance buttons.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <returns>Toolbar visual element.</returns>
    private static Toolbar BuildSceneTableToolbar(GameSceneManagerPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button syncButton = new Button(() => SynchronizeBuildMetadata(panel));
        syncButton.text = "Sync Build Metadata";
        syncButton.tooltip = "Refresh scene GUIDs and Build Settings indexes for every scene definition.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(syncButton, 148f);
        toolbar.Add(syncButton);

        Button applyBuildSettingsButton = new Button(() => ApplySceneOrderToBuildSettings(panel));
        applyBuildSettingsButton.text = "Apply Build Order";
        applyBuildSettingsButton.tooltip = "Replace Build Settings scene order with the preset scene table, skipping SubScene and Addressables-owned entries.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(applyBuildSettingsButton, 128f);
        toolbar.Add(applyBuildSettingsButton);
        return toolbar;
    }

    /// <summary>
    /// Refreshes build index metadata on serialized scene definitions.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void SynchronizeBuildMetadata(GameSceneManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null)
            return;

        SerializedProperty scenesProperty = panel.PresetSerializedObject.FindProperty("sceneDefinitions");

        if (scenesProperty == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Sync Scene Build Metadata");
        panel.PresetSerializedObject.Update();

        for (int index = 0; index < scenesProperty.arraySize; index++)
        {
            SerializedProperty sceneProperty = scenesProperty.GetArrayElementAtIndex(index);
            SerializedProperty pathProperty = sceneProperty.FindPropertyRelative("scenePath");
            SerializedProperty buildIndexProperty = sceneProperty.FindPropertyRelative("buildIndex");
            SerializedProperty guidProperty = sceneProperty.FindPropertyRelative("sceneGuid");

            if (pathProperty == null)
                continue;

            if (buildIndexProperty != null)
                buildIndexProperty.intValue = GameSceneManagementBuildSettingsUtility.ResolveBuildIndex(pathProperty.stringValue);

            if (guidProperty != null && !string.IsNullOrWhiteSpace(pathProperty.stringValue))
                guidProperty.stringValue = AssetDatabase.AssetPathToGUID(pathProperty.stringValue);
        }

        panel.PresetSerializedObject.ApplyModifiedProperties();
        panel.MarkSelectedPresetDirty();
        panel.BuildActiveSection();
    }

    /// <summary>
    /// Applies authored non-SubScene scene order to Editor Build Settings.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void ApplySceneOrderToBuildSettings(GameSceneManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Apply Scene Build Order",
                                                     "Replace Build Settings scenes with the non-SubScene entries from this Scene Manager preset?",
                                                     "Apply",
                                                     "Cancel");

        if (!confirmed)
            return;

        if (GameSceneManagementBuildSettingsUtility.ApplySceneOrderToBuildSettings(panel.SelectedPreset))
            SynchronizeBuildMetadata(panel);
    }

    /// <summary>
    /// Creates the configured transition layer and refreshes the visible section.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    /// <param name="layerName">Layer name to create.</param>
    private static void CreateTransitionLayer(GameSceneManagerPresetsPanel panel, string layerName)
    {
        if (GameSceneTransitionLayerUtility.TryCreateLayer(layerName))
            panel.BuildActiveSection();
    }

    /// <summary>
    /// Synchronizes Addressables settings and scene entries for the selected Scene Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with selected preset context.</param>
    private static void SynchronizeAddressableScenes(GameSceneManagerPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameSceneAddressablesEditorUtility.EnsureSceneEntries(panel.SelectedPreset);
        panel.BuildActiveSection();
    }
    #endregion

    #endregion
}
