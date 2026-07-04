using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and sub-preset creation flows for player master preset panels.
/// </summary>
internal static class PlayerMasterPresetsPanelSectionsUtility
{
    #region Methods
    #region Public Methods
    /// <summary>
    /// Builds the metadata section for the selected player master preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildMetadataSection(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;
        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Preset Details");
        if (sectionContainer == null)
            return;
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty idProperty = presetSerializedObject.FindProperty("m_PresetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("m_PresetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("m_Description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("m_Version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        sectionContainer.Add(idRow);
    }

    /// <summary>
    /// Builds the sub-preset assignment section.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildSubPresetsSection(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;
        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Sub Presets");
        if (sectionContainer == null)
            return;
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty controllerProperty = presetSerializedObject.FindProperty("m_ControllerPreset");
        SerializedProperty progressionProperty = presetSerializedObject.FindProperty("m_ProgressionPreset");
        SerializedProperty powerUpsProperty = presetSerializedObject.FindProperty("m_PowerUpsPreset");
        SerializedProperty visualProperty = presetSerializedObject.FindProperty("visualPreset");
        SerializedProperty uiVisualProperty = presetSerializedObject.FindProperty("uiVisualPreset");
        SerializedProperty animationProperty = presetSerializedObject.FindProperty("m_AnimationBindingsPreset");

        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Controller Preset",
                                               typeof(PlayerControllerPreset),
                                               controllerProperty,
                                               panel.CreateControllerPreset,
                                               () => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PlayerControllerPresets),
                                               PlayerManagementWindow.PanelType.PlayerControllerPresets));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Level-Up & Progression Preset",
                                               typeof(PlayerProgressionPreset),
                                               progressionProperty,
                                               panel.CreateProgressionPreset,
                                               () => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.LevelUpProgression),
                                               PlayerManagementWindow.PanelType.LevelUpProgression));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Power-Ups Preset",
                                               typeof(PlayerPowerUpsPreset),
                                               powerUpsProperty,
                                               panel.CreatePowerUpsPreset,
                                               () => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PowerUps),
                                               PlayerManagementWindow.PanelType.PowerUps));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Gameplay Visual Preset",
                                               typeof(PlayerVisualPreset),
                                               visualProperty,
                                               panel.CreateVisualPreset,
                                               () => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PlayerVisualPresets),
                                               PlayerManagementWindow.PanelType.PlayerVisualPresets));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "UI Visual Preset",
                                               typeof(PlayerUiVisualPreset),
                                               uiVisualProperty,
                                               panel.CreateUiVisualPreset,
                                               () => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PlayerUiVisualPresets),
                                               PlayerManagementWindow.PanelType.PlayerUiVisualPresets));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Animation Bindings Preset",
                                               typeof(PlayerAnimationBindingsPreset),
                                               animationProperty,
                                               panel.CreateAnimationPreset,
                                               () => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.AnimationBindings),
                                               PlayerManagementWindow.PanelType.AnimationBindings));
    }

    /// <summary>
    /// Builds the active preset section used to assign the selected master preset to one player prefab.
    /// </summary>
    /// <param name="panel">Owning panel that provides prefab activation callbacks.</param>

    public static void BuildActivePresetSection(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;
        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Active on Player Prefab");
        if (sectionContainer == null)
            return;
        ObjectField playerPrefabField = new ObjectField("Player Prefab");
        playerPrefabField.objectType = typeof(GameObject);
        playerPrefabField.RegisterValueChangedCallback(evt =>
        {
            panel.SelectedPlayerPrefab = evt.newValue as GameObject;
            PlayerMasterPresetsPanelSidePanelUtility.SaveSelectedPrefabState(panel);
            panel.RefreshActiveStatus();
        });
        playerPrefabField.SetValueWithoutNotify(panel.SelectedPlayerPrefab);
        panel.PlayerPrefabField = playerPrefabField;
        sectionContainer.Add(playerPrefabField);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.marginTop = 2f;

        Button findButton = new Button(panel.FindPlayerPrefab);
        findButton.text = "Find";
        findButton.tooltip = "Search the project for a prefab with PlayerAuthoring.";
        buttonRow.Add(findButton);

        Button setActiveButton = new Button(panel.AssignPresetToPrefab);
        setActiveButton.text = "Set Active Preset";
        setActiveButton.tooltip = "Assign this master preset to the selected player prefab.";
        setActiveButton.style.marginLeft = 4f;
        buttonRow.Add(setActiveButton);

        sectionContainer.Add(buttonRow);

        Label activeStatusLabel = new Label();
        activeStatusLabel.style.marginTop = 2f;
        activeStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        panel.ActiveStatusLabel = activeStatusLabel;
        sectionContainer.Add(activeStatusLabel);
        panel.RefreshActiveStatus();
    }

    /// <summary>
    /// Builds the navigation section used to open related preset panels.
    /// </summary>
    /// <param name="panel">Owning panel that provides side-panel callbacks.</param>

    public static void BuildNavigationSection(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;
        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Open Sections");
        if (sectionContainer == null)
            return;
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button controllerButton = new Button(() => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PlayerControllerPresets));
        controllerButton.text = "Open Controller";
        row.Add(controllerButton);

        Button progressionButton = new Button(() => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.LevelUpProgression));
        progressionButton.text = "Open Progression";
        progressionButton.style.marginLeft = 4f;
        row.Add(progressionButton);

        Button powerUpsButton = new Button(() => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PowerUps));
        powerUpsButton.text = "Open Power-Ups";
        powerUpsButton.style.marginLeft = 4f;
        row.Add(powerUpsButton);

        Button gameplayVisualButton = new Button(() => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PlayerVisualPresets));
        gameplayVisualButton.text = "Open Gameplay Visual";
        gameplayVisualButton.style.marginLeft = 4f;
        row.Add(gameplayVisualButton);

        Button uiVisualButton = new Button(() => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.PlayerUiVisualPresets));
        uiVisualButton.text = "Open UI Visual";
        uiVisualButton.style.marginLeft = 4f;
        row.Add(uiVisualButton);

        Button animationButton = new Button(() => PlayerMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, PlayerManagementWindow.PanelType.AnimationBindings));
        animationButton.text = "Open Animations";
        animationButton.style.marginLeft = 4f;
        row.Add(animationButton);

        sectionContainer.Add(row);
    }

    /// <summary>
    /// Builds the world layers section for the selected player master preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>

    public static void BuildLayersSection(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;
        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "World Layers");
        if (sectionContainer == null)
            return;

        SerializedProperty wallsLayerNameProperty = panel.PresetSerializedObject.FindProperty("wallsLayerName");

        if (wallsLayerNameProperty == null)
            return;

        string configuredLayerName = wallsLayerNameProperty.stringValue;
        int configuredLayerIndex = LayerMask.NameToLayer(configuredLayerName);
        int defaultLayerIndex = LayerMask.NameToLayer(WorldWallCollisionUtility.DefaultWallsLayerName);
        int initialLayerIndex = configuredLayerIndex >= 0 ? configuredLayerIndex : defaultLayerIndex;

        if (initialLayerIndex < 0)
            initialLayerIndex = 0;

        LayerField wallsLayerField = new LayerField("Walls Layer");
        wallsLayerField.tooltip = "Layer considered as solid map walls for player, enemies, bombs and projectiles.";
        wallsLayerField.SetValueWithoutNotify(initialLayerIndex);
        wallsLayerField.RegisterValueChangedCallback(evt =>
        {
            string selectedLayerName = LayerMask.LayerToName(evt.newValue);

            if (string.IsNullOrWhiteSpace(selectedLayerName))
                return;

            SerializedProperty updatedProperty = panel.PresetSerializedObject.FindProperty("wallsLayerName");

            if (updatedProperty == null)
                return;

            if (panel.SelectedPreset != null)
                Undo.RecordObject(panel.SelectedPreset, "Change Walls Layer");

            panel.PresetSerializedObject.Update();
            updatedProperty.stringValue = selectedLayerName;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        sectionContainer.Add(wallsLayerField);

        if (configuredLayerIndex < 0 && !string.IsNullOrWhiteSpace(configuredLayerName))
        {
            HelpBox warningBox = new HelpBox(string.Format("Layer '{0}' does not exist. Select an existing layer.", configuredLayerName), HelpBoxMessageType.Warning);
            sectionContainer.Add(warningBox);
        }
    }

    /// <summary>
    /// Selects one preset and rebuilds the detail area plus linked side-panel synchronization.
    /// </summary>
    /// <param name="panel">Owning panel that stores selection and detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear the detail view.</param>

    public static void SelectPreset(PlayerMasterPresetsPanel panel, PlayerMasterPreset preset)
    {
        if (panel == null)
            return;

        panel.SelectedPreset = preset;
        // Persist the selection so close/reopen lands on the same preset.
        PlayerMasterPresetsPanelSidePanelUtility.SaveSelectedPresetState(panel);
        PlayerManagementSelectionContext.SetActiveMasterPreset(panel.SelectedPreset);
        panel.DetailsRoot.Clear();
        panel.DetailSectionButtonsRoot = null;
        panel.DetailSectionContentRoot = null;

        if (panel.SelectedPreset == null)
        {
            Label label = new Label("Select or create a master preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            panel.DetailsRoot.Add(label);
            panel.RefreshActiveStatus();
            return;
        }

        panel.PresetSerializedObject = new SerializedObject(panel.SelectedPreset);
        panel.DetailSectionButtonsRoot = BuildDetailsSectionButtons(panel);
        panel.DetailSectionContentRoot = new VisualElement();
        panel.DetailSectionContentRoot.style.flexDirection = FlexDirection.Column;
        panel.DetailSectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.DetailSectionButtonsRoot);
        panel.DetailsRoot.Add(panel.DetailSectionContentRoot);

        BuildActiveDetailsSection(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.DetailsRoot);
        panel.RefreshActiveStatus();
    }

    /// <summary>
    /// Builds the detail section tab row.
    /// </summary>
    /// <param name="panel">Owning panel that stores detail section state.</param>
    /// <returns>Returns the detail tab row.</returns>
    public static VisualElement BuildDetailsSectionButtons(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return null;

        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddDetailsSectionButton(panel, buttonsRoot, PlayerMasterPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddDetailsSectionButton(panel, buttonsRoot, PlayerMasterPresetsPanel.DetailsSectionType.SubPresets, "Sub Presets");
        AddDetailsSectionButton(panel, buttonsRoot, PlayerMasterPresetsPanel.DetailsSectionType.ActivePreset, "Active Preset");
        AddDetailsSectionButton(panel, buttonsRoot, PlayerMasterPresetsPanel.DetailsSectionType.Navigation, "Navigation");
        AddDetailsSectionButton(panel, buttonsRoot, PlayerMasterPresetsPanel.DetailsSectionType.Layers, "Layers");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one detail section button to the provided row.
    /// </summary>
    /// <param name="panel">Owning panel that receives the section activation callback.</param>
    /// <param name="parent">Parent row that receives the button.</param>
    /// <param name="sectionType">Section represented by the button.</param>
    /// <param name="buttonLabel">Visible button label.</param>

    public static void AddDetailsSectionButton(PlayerMasterPresetsPanel panel, VisualElement parent, PlayerMasterPresetsPanel.DetailsSectionType sectionType, string buttonLabel)
    {
        if (panel == null || parent == null)
            return;

        Button sectionButton = new Button(() => SetActiveDetailsSection(panel, sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    /// <summary>
    /// Sets the active detail section, persists it and rebuilds the visible content.
    /// </summary>
    /// <param name="panel">Owning panel that stores detail section state.</param>
    /// <param name="sectionType">Section to activate.</param>

    public static void SetActiveDetailsSection(PlayerMasterPresetsPanel panel, PlayerMasterPresetsPanel.DetailsSectionType sectionType)
    {
        if (panel == null)
            return;

        panel.ActiveDetailsSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(PlayerMasterPresetsPanelSidePanelUtility.ActiveDetailsSectionStateKey, panel.ActiveDetailsSection);
        BuildActiveDetailsSection(panel);
    }

    /// <summary>
    /// Rebuilds the currently active detail section.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active detail section and serialized preset context.</param>

    public static void BuildActiveDetailsSection(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        if (panel.DetailSectionContentRoot == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.DetailSectionContentRoot.Clear();

        switch (panel.ActiveDetailsSection)
        {
            case PlayerMasterPresetsPanel.DetailsSectionType.Metadata:
                BuildMetadataSection(panel);
                break;
            case PlayerMasterPresetsPanel.DetailsSectionType.SubPresets:
                BuildSubPresetsSection(panel);
                break;
            case PlayerMasterPresetsPanel.DetailsSectionType.ActivePreset:
                BuildActivePresetSection(panel);
                break;
            case PlayerMasterPresetsPanel.DetailsSectionType.Navigation:
                BuildNavigationSection(panel);
                break;
            case PlayerMasterPresetsPanel.DetailsSectionType.Layers:
                BuildLayersSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.DetailSectionContentRoot);
    }

    /// <summary>
    /// Creates one new controller sub-preset and assigns it to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>

    public static void CreateControllerPreset(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        PlayerControllerPreset newPreset = PlayerControllerPresetLibraryUtility.CreatePresetAsset("PlayerControllerPreset");

        if (newPreset == null)
            return;

        PlayerControllerPresetLibrary controllerLibrary = PlayerControllerPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Controller Preset Asset");
        Undo.RecordObject(controllerLibrary, "Add Controller Preset");
        controllerLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(controllerLibrary);
        PlayerManagementDraftSession.MarkDirty();
        AssignSubPreset(panel, "m_ControllerPreset", newPreset);
    }

    /// <summary>
    /// Creates one new progression sub-preset and assigns it to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>

    public static void CreateProgressionPreset(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        PlayerProgressionPreset newPreset = CreateSubPresetAsset<PlayerProgressionPreset>("PlayerProgressionPreset", PlayerMasterPresetsPanel.ProgressionPresetsFolder);

        if (newPreset != null)
        {
            PlayerProgressionPresetLibrary progressionLibrary = PlayerProgressionPresetLibraryUtility.GetOrCreateLibrary();
            Undo.RegisterCreatedObjectUndo(newPreset, "Create Progression Preset Asset");
            Undo.RecordObject(progressionLibrary, "Add Progression Preset");
            progressionLibrary.AddPreset(newPreset);
            EditorUtility.SetDirty(progressionLibrary);
            PlayerManagementDraftSession.MarkDirty();
        }

        AssignSubPreset(panel, "m_ProgressionPreset", newPreset);
    }

    /// <summary>
    /// Creates one new power-ups sub-preset and assigns it to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>

    public static void CreatePowerUpsPreset(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        PlayerPowerUpsPreset newPreset = CreateSubPresetAsset<PlayerPowerUpsPreset>("PlayerPowerUpsPreset", PlayerMasterPresetsPanel.PowerUpsPresetsFolder);

        if (newPreset != null)
        {
            PlayerPowerUpsPresetLibrary powerUpsLibrary = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();
            Undo.RegisterCreatedObjectUndo(newPreset, "Create Power Ups Preset Asset");
            Undo.RecordObject(powerUpsLibrary, "Add Power Ups Preset");
            powerUpsLibrary.AddPreset(newPreset);
            EditorUtility.SetDirty(powerUpsLibrary);
            PlayerManagementDraftSession.MarkDirty();
        }

        AssignSubPreset(panel, "m_PowerUpsPreset", newPreset);
    }

    /// <summary>
    /// Creates one new animation bindings sub-preset and assigns it to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>

    public static void CreateAnimationPreset(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        PlayerAnimationBindingsPreset newPreset = CreateSubPresetAsset<PlayerAnimationBindingsPreset>("PlayerAnimationBindingsPreset", PlayerMasterPresetsPanel.AnimationPresetsFolder);
        AssignSubPreset(panel, "m_AnimationBindingsPreset", newPreset);
    }

    /// <summary>
    /// Creates one new visual sub-preset and assigns it to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>

    public static void CreateVisualPreset(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        PlayerVisualPreset newPreset = CreateSubPresetAsset<PlayerVisualPreset>("PlayerVisualPreset", PlayerMasterPresetsPanel.VisualPresetsFolder);

        if (newPreset != null)
        {
            PlayerVisualPresetLibrary visualLibrary = PlayerVisualPresetLibraryUtility.GetOrCreateLibrary();
            Undo.RegisterCreatedObjectUndo(newPreset, "Create Visual Preset Asset");
            Undo.RecordObject(visualLibrary, "Add Visual Preset");
            visualLibrary.AddPreset(newPreset);
            EditorUtility.SetDirty(visualLibrary);
            PlayerManagementDraftSession.MarkDirty();
        }

        AssignSubPreset(panel, "visualPreset", newPreset);
    }

    /// <summary>
    /// Creates one new UI visual sub-preset and assigns it to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected preset state.</param>
    public static void CreateUiVisualPreset(PlayerMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        PlayerUiVisualPreset newPreset = CreateSubPresetAsset<PlayerUiVisualPreset>("PlayerUiVisualPreset", PlayerMasterPresetsPanel.UiVisualPresetsFolder);

        if (newPreset != null)
        {
            PlayerUiVisualPresetLibrary uiVisualLibrary = PlayerUiVisualPresetLibraryUtility.GetOrCreateLibrary();
            Undo.RegisterCreatedObjectUndo(newPreset, "Create UI Visual Preset Asset");
            Undo.RecordObject(uiVisualLibrary, "Add UI Visual Preset");
            uiVisualLibrary.AddPreset(newPreset);
            EditorUtility.SetDirty(uiVisualLibrary);
            PlayerManagementDraftSession.MarkDirty();
        }

        AssignSubPreset(panel, "uiVisualPreset", newPreset);
    }

    /// <summary>
    /// Assigns one sub-preset reference on the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores the selected preset and serialized object.</param>
    /// <param name="propertyName">Serialized property name that receives the reference.</param>
    /// <param name="preset">Sub-preset to assign.</param>

    public static void AssignSubPreset(PlayerMasterPresetsPanel panel, string propertyName, UnityEngine.Object preset)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Sub Preset");
        panel.PresetSerializedObject.Update();
        property.objectReferenceValue = preset;
        panel.PresetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();

        if (string.Equals(propertyName, "visualPreset", StringComparison.Ordinal) ||
            string.Equals(propertyName, "uiVisualPreset", StringComparison.Ordinal))
        {
            PlayerManagementSelectionContext.NotifyVisualPresetContentChanged();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one object field row and action row for a sub-preset reference.
    /// </summary>
    /// <param name="panel">Owning panel that needs synchronization when references change.</param>
    /// <param name="label">Object field label.</param>
    /// <param name="presetType">Expected preset asset type.</param>
    /// <param name="presetProperty">Serialized property bound to the field.</param>
    /// <param name="createAction">Callback that creates and assigns a new preset.</param>
    /// <param name="openSectionAction">Callback that opens the related management section.</param>
    /// <param name="panelType">Panel type associated with the referenced preset.</param>
    /// <param name="openButtonLabel">Visible label for the primary open button.</param>
    /// <param name="secondaryOpenSectionAction">Optional callback that opens an additional related section.</param>
    /// <param name="secondaryOpenButtonLabel">Optional visible label for the secondary open button.</param>
    /// <returns>Returns the constructed sub-preset row.</returns>
    private static VisualElement BuildSubPresetRow(PlayerMasterPresetsPanel panel,
                                                   string label,
                                                   Type presetType,
                                                   SerializedProperty presetProperty,
                                                   Action createAction,
                                                   Action openSectionAction,
                                                   PlayerManagementWindow.PanelType panelType,
                                                   string openButtonLabel = "Open Section",
                                                   Action secondaryOpenSectionAction = null,
                                                   string secondaryOpenButtonLabel = null)
    {
        VisualElement container = new VisualElement();
        container.style.marginBottom = 6f;

        ObjectField presetField = new ObjectField(label);
        presetField.objectType = presetType;
        presetField.BindProperty(presetProperty);
        presetField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();

            if (panelType == PlayerManagementWindow.PanelType.PlayerVisualPresets ||
                panelType == PlayerManagementWindow.PanelType.PlayerUiVisualPresets)
            {
                PlayerManagementSelectionContext.NotifyVisualPresetContentChanged();
            }
        });
        container.Add(presetField);

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.marginTop = 2f;

        Button openButton = new Button(openSectionAction);
        openButton.text = openButtonLabel;
        openButton.tooltip = "Open the corresponding section.";
        buttonsRow.Add(openButton);

        if (secondaryOpenSectionAction != null && !string.IsNullOrWhiteSpace(secondaryOpenButtonLabel))
        {
            Button secondaryOpenButton = new Button(secondaryOpenSectionAction);
            secondaryOpenButton.text = secondaryOpenButtonLabel;
            secondaryOpenButton.tooltip = "Open the corresponding section.";
            secondaryOpenButton.style.marginLeft = 4f;
            buttonsRow.Add(secondaryOpenButton);
        }

        Button newButton = new Button(createAction);
        newButton.text = "New";
        newButton.tooltip = "Create and assign a new preset.";
        newButton.style.marginLeft = 4f;
        buttonsRow.Add(newButton);

        Button selectButton = new Button(() => SelectSubPresetInProject(presetProperty));
        selectButton.text = "Select in Project";
        selectButton.tooltip = "Select the assigned preset in the Project window.";
        selectButton.style.marginLeft = 4f;
        buttonsRow.Add(selectButton);

        container.Add(buttonsRow);
        return container;
    }

    /// <summary>
    /// Selects the referenced sub-preset asset in the Project window.
    /// </summary>
    /// <param name="presetProperty">Serialized property that stores the referenced asset.</param>

    private static void SelectSubPresetInProject(SerializedProperty presetProperty)
    {
        if (presetProperty == null)
            return;

        UnityEngine.Object target = presetProperty.objectReferenceValue;

        if (target == null)
            return;

        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    /// <summary>
    /// Creates one section container with a header and registers it into the active detail root.
    /// </summary>
    /// <param name="panel">Owning panel that stores the detail root.</param>
    /// <param name="sectionTitle">Visible section title.</param>
    /// <returns>Returns the created section container.</returns>
    private static VisualElement CreateDetailsSectionContainer(PlayerMasterPresetsPanel panel, string sectionTitle)
    {
        if (panel.DetailSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.PlayerManagement.Master.Section." + sectionTitle);
        container.Add(header);
        panel.DetailSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Creates one sub-preset asset in the requested folder and initializes its preset name when present.
    /// </summary>
    /// <param name="presetName">Base asset name requested by the caller.</param>
    /// <param name="folderPath">Destination folder path.</param>
    /// <typeparam name="T">Concrete ScriptableObject preset type.</typeparam>
    /// <returns>Returns the created asset instance, or null when creation fails.</returns>
    private static T CreateSubPresetAsset<T>(string presetName, string folderPath) where T : ScriptableObject
    {
        EnsureFolder(folderPath);

        string normalizedName = string.IsNullOrWhiteSpace(presetName) ? typeof(T).Name : PlayerManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = typeof(T).Name;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        T preset = ScriptableObject.CreateInstance<T>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);
        Undo.RegisterCreatedObjectUndo(preset, "Create Sub Preset Asset");

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("m_PresetName");

        if (nameProperty == null)
            nameProperty = serializedPreset.FindProperty("presetName");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        PlayerVisualPreset visualPreset = preset as PlayerVisualPreset;

        if (visualPreset != null)
        {
            SerializedProperty laserBeamProperty = serializedPreset.FindProperty("laserBeam");

            if (laserBeamProperty != null)
            {
                SerializedProperty bodyMaterialProperty = laserBeamProperty.FindPropertyRelative("bodyMaterial");
                SerializedProperty sourceEffectMaterialProperty = laserBeamProperty.FindPropertyRelative("sourceEffectMaterial");
                SerializedProperty terminalCapMaterialProperty = laserBeamProperty.FindPropertyRelative("terminalCapMaterial");
                Material laserBeamMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath);
                Material sourceEffectMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceEffectMaterialPath);
                Material terminalCapMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultTerminalCapMaterialPath);

                if (bodyMaterialProperty != null && laserBeamMaterial != null)
                    bodyMaterialProperty.objectReferenceValue = laserBeamMaterial;

                if (sourceEffectMaterialProperty != null && sourceEffectMaterial != null)
                    sourceEffectMaterialProperty.objectReferenceValue = sourceEffectMaterial;

                if (terminalCapMaterialProperty != null && terminalCapMaterial != null)
                    terminalCapMaterialProperty.objectReferenceValue = terminalCapMaterial;
            }
        }

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();

        if (visualPreset != null)
            visualPreset.LaserBeam.Validate();

        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
        return preset;
    }

    /// <summary>
    /// Ensures that the requested folder path exists inside the AssetDatabase.
    /// </summary>
    /// <param name="folderPath">Folder path to create recursively when missing.</param>

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion

    #endregion
}
