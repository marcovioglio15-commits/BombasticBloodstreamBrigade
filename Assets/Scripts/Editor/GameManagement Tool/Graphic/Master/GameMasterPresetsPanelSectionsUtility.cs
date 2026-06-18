using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds selected game master preset detail sections and sub-preset creation flows.
/// </summary>
internal static class GameMasterPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Selects one master preset and rebuilds the active detail section.
    /// </summary>
    /// <param name="panel">Owning panel with detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    public static void SelectPreset(GameMasterPresetsPanel panel, GameMasterPreset preset)
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

        if (panel.SelectedPreset == null)
        {
            panel.DetailsRoot.Add(new Label("Select or create a game master preset to edit."));
            return;
        }

        panel.PresetSerializedObject = new SerializedObject(panel.SelectedPreset);
        panel.DetailSectionButtonsRoot = BuildDetailsSectionButtons(panel);
        panel.DetailSectionContentRoot = new VisualElement();
        panel.DetailSectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.DetailSectionButtonsRoot);
        panel.DetailsRoot.Add(panel.DetailSectionContentRoot);
        BuildActiveDetailsSection(panel);
    }

    /// <summary>
    /// Rebuilds the currently active detail section.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    public static void BuildActiveDetailsSection(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.DetailSectionContentRoot == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.DetailSectionContentRoot.Clear();

        switch (panel.ActiveDetailsSection)
        {
            case GameMasterPresetsPanel.DetailsSectionType.SubPresets:
                BuildSubPresetsSection(panel);
                break;
            case GameMasterPresetsPanel.DetailsSectionType.ActiveAuthoring:
                BuildActiveAuthoringSection(panel);
                break;
            case GameMasterPresetsPanel.DetailsSectionType.Navigation:
                BuildNavigationSection(panel);
                break;
            default:
                BuildMetadataSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.DetailSectionContentRoot);
    }

    /// <summary>
    /// Creates, registers and assigns a new Audio Manager preset to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel with selected master preset context.</param>
    public static void CreateAudioManagerPreset(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameAudioManagerPreset newPreset = GameAudioManagerPresetLibraryUtility.CreatePresetAsset("GameAudioManagerPreset");

        if (newPreset == null)
            return;

        GameAudioManagerPresetLibrary audioLibrary = GameAudioManagerPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Audio Manager Preset");
        Undo.RecordObject(audioLibrary, "Add Audio Manager Preset");
        audioLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(audioLibrary);

        AssignSubPreset(panel, "audioManagerPreset", newPreset);
        panel.OpenSidePanel(GameManagementWindow.PanelType.AudioManager);
    }

    /// <summary>
    /// Creates, registers and assigns a new Scene Manager preset to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel with selected master preset context.</param>
    public static void CreateSceneManagerPreset(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameSceneManagerPreset newPreset = GameSceneManagerPresetLibraryUtility.CreatePresetAsset("GameSceneManagerPreset");

        if (newPreset == null)
            return;

        GameSceneManagerPresetLibrary sceneLibrary = GameSceneManagerPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Scene Manager Preset");
        Undo.RecordObject(sceneLibrary, "Add Scene Manager Preset");
        sceneLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(sceneLibrary);

        AssignSubPreset(panel, "sceneManagerPreset", newPreset);
        panel.OpenSidePanel(GameManagementWindow.PanelType.SceneManager);
    }

    /// <summary>
    /// Assigns one sub-preset reference to the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel with serialized master preset.</param>
    /// <param name="propertyName">Serialized property receiving the object reference.</param>
    /// <param name="preset">Preset object to assign.</param>
    public static void AssignSubPreset(GameMasterPresetsPanel panel, string propertyName, UnityEngine.Object preset)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null)
            return;

        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Game Sub Preset");
        panel.PresetSerializedObject.Update();
        property.objectReferenceValue = preset;
        panel.PresetSerializedObject.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        BuildActiveDetailsSection(panel);
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds metadata fields for the selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    private static void BuildMetadataSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Preset Details");
        AddBoundTextField(panel, section, "Preset Name", "presetName", true, false);
        AddBoundTextField(panel, section, "Version", "version", false, false);
        AddBoundTextField(panel, section, "Description", "description", false, true);

        SerializedProperty idProperty = panel.PresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        PropertyField idField = new PropertyField(idProperty, "Preset ID");
        idField.tooltip = "Stable ID used by Game Management Tool for this master preset.";
        idField.BindProperty(idProperty);
        idField.SetEnabled(false);
        section.Add(idField);
    }

    /// <summary>
    /// Builds Audio Manager sub-preset assignment controls.
    /// </summary>
    /// <param name="panel">Owning panel with selected master preset context.</param>
    private static void BuildSubPresetsSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Sub Presets");
        AddSubPresetControl(panel,
                            section,
                            "Audio Manager Preset",
                            "audioManagerPreset",
                            typeof(GameAudioManagerPreset),
                            "Audio Manager preset used for FMOD gameplay event bindings.",
                            GameManagementWindow.PanelType.AudioManager,
                            "Audio Manager",
                            panel.CreateAudioManagerPreset);
        AddSubPresetControl(panel,
                            section,
                            "Scene Manager Preset",
                            "sceneManagerPreset",
                            typeof(GameSceneManagerPreset),
                            "Scene Manager preset used for scene loading, transitions, fade and trigger defaults.",
                            GameManagementWindow.PanelType.SceneManager,
                            "Scene Manager",
                            panel.CreateSceneManagerPreset);
    }

    /// <summary>
    /// Builds prefab authoring controls for the selected game master preset.
    /// </summary>
    /// <param name="panel">Owning panel that stores selected prefab state.</param>
    private static void BuildActiveAuthoringSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Active Authoring");

        ObjectField audioPrefabField = new ObjectField("Audio Manager Prefab");
        audioPrefabField.objectType = typeof(GameObject);
        audioPrefabField.tooltip = "Prefab containing GameAudioManagerAuthoring to receive this Game Master preset.";
        audioPrefabField.SetValueWithoutNotify(panel.SelectedAudioPrefab);
        audioPrefabField.RegisterValueChangedCallback(evt =>
        {
            panel.SelectedAudioPrefab = evt.newValue as GameObject;
            GameMasterPresetsPanelSidePanelUtility.SaveSelectedAudioPrefabState(panel);
            panel.RefreshActiveStatus();
        });
        panel.AudioPrefabField = audioPrefabField;
        section.Add(audioPrefabField);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button findButton = new Button(panel.FindAudioManagerPrefab);
        findButton.text = "Find";
        findButton.tooltip = "Find a prefab with GameAudioManagerAuthoring.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(findButton, 48f);
        row.Add(findButton);

        Button assignButton = new Button(panel.AssignPresetToAuthoringPrefab);
        assignButton.text = "Set Active Preset";
        assignButton.tooltip = "Assign this game master preset to the selected authoring prefab.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(assignButton, 128f);
        assignButton.style.marginLeft = 4f;
        row.Add(assignButton);
        section.Add(row);

        Label activeStatusLabel = new Label();
        activeStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        panel.ActiveStatusLabel = activeStatusLabel;
        section.Add(activeStatusLabel);

        ObjectField scenePrefabField = new ObjectField("Scene Manager Prefab");
        scenePrefabField.objectType = typeof(GameObject);
        scenePrefabField.tooltip = "Prefab containing GameSceneManagerAuthoring to receive this Game Master preset.";
        scenePrefabField.SetValueWithoutNotify(panel.SelectedScenePrefab);
        scenePrefabField.RegisterValueChangedCallback(evt =>
        {
            panel.SelectedScenePrefab = evt.newValue as GameObject;
            GameMasterPresetsPanelSidePanelUtility.SaveSelectedScenePrefabState(panel);
            panel.RefreshActiveStatus();
        });
        panel.ScenePrefabField = scenePrefabField;
        section.Add(scenePrefabField);

        VisualElement sceneRow = new VisualElement();
        sceneRow.style.flexDirection = FlexDirection.Row;
        sceneRow.style.flexWrap = Wrap.Wrap;

        Button findSceneButton = new Button(panel.FindSceneManagerPrefab);
        findSceneButton.text = "Find";
        findSceneButton.tooltip = "Find a prefab with GameSceneManagerAuthoring.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(findSceneButton, 48f);
        sceneRow.Add(findSceneButton);

        Button assignSceneButton = new Button(panel.AssignPresetToSceneAuthoringPrefab);
        assignSceneButton.text = "Set Active Preset";
        assignSceneButton.tooltip = "Assign this game master preset to the selected Scene Manager authoring prefab.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(assignSceneButton, 128f);
        assignSceneButton.style.marginLeft = 4f;
        sceneRow.Add(assignSceneButton);
        section.Add(sceneRow);

        Label sceneActiveStatusLabel = new Label();
        sceneActiveStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        panel.SceneActiveStatusLabel = sceneActiveStatusLabel;
        section.Add(sceneActiveStatusLabel);
        panel.RefreshActiveStatus();
    }

    /// <summary>
    /// Builds quick navigation controls for implemented game sub-sections.
    /// </summary>
    /// <param name="panel">Owning panel that opens side panels.</param>
    private static void BuildNavigationSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Open Sections");
        Button audioButton = new Button(() => panel.OpenSidePanel(GameManagementWindow.PanelType.AudioManager));
        audioButton.text = "Open Audio Manager";
        audioButton.tooltip = "Open the Audio Manager preset panel.";
        audioButton.style.flexShrink = 0f;
        audioButton.style.minWidth = 144f;
        section.Add(audioButton);

        Button sceneButton = new Button(() => panel.OpenSidePanel(GameManagementWindow.PanelType.SceneManager));
        sceneButton.text = "Open Scene Manager";
        sceneButton.tooltip = "Open the Scene Manager preset panel.";
        sceneButton.style.flexShrink = 0f;
        sceneButton.style.minWidth = 148f;
        sceneButton.style.marginTop = 4f;
        section.Add(sceneButton);
    }
    #endregion

    #region Detail Helpers
    /// <summary>
    /// Adds one master sub-preset object field with section open and asset create actions.
    /// </summary>
    /// <param name="panel">Owning panel with serialized master preset context.</param>
    /// <param name="section">Parent section.</param>
    /// <param name="label">Object field label.</param>
    /// <param name="propertyName">Serialized object reference property.</param>
    /// <param name="objectType">Accepted preset asset type.</param>
    /// <param name="tooltip">Object field tooltip.</param>
    /// <param name="panelType">Side panel opened by the row.</param>
    /// <param name="sectionName"> section name.</param>
    /// <param name="createCallback">Callback used to create and assign a new preset.</param>
    private static void AddSubPresetControl(GameMasterPresetsPanel panel,
                                            VisualElement section,
                                            string label,
                                            string propertyName,
                                            System.Type objectType,
                                            string tooltip,
                                            GameManagementWindow.PanelType panelType,
                                            string sectionName,
                                            System.Action createCallback)
    {
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        ObjectField objectField = new ObjectField(label);
        objectField.objectType = objectType;
        objectField.tooltip = tooltip;
        objectField.BindProperty(property);
        objectField.RegisterValueChangedCallback(evt =>
        {
            GameManagementDraftSession.MarkDirty();
        });
        section.Add(objectField);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button openButton = new Button(() => panel.OpenSidePanel(panelType));
        openButton.text = "Open Section";
        openButton.tooltip = "Open the " + sectionName + " section.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(openButton, 108f);
        row.Add(openButton);

        Button newButton = new Button(createCallback);
        newButton.text = "New";
        newButton.tooltip = "Create and assign a new " + sectionName + " preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(newButton, 48f);
        newButton.style.marginLeft = 4f;
        row.Add(newButton);
        section.Add(row);
    }

    /// <summary>
    /// Builds detail section selector buttons.
    /// </summary>
    /// <param name="panel">Owning panel that stores the active section state.</param>
    /// <returns>Detail section button row.</returns>
    private static VisualElement BuildDetailsSectionButtons(GameMasterPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.SubPresets, "Sub Presets");
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.ActiveAuthoring, "Active Authoring");
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.Navigation, "Navigation");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one detail section selector button.
    /// </summary>
    /// <param name="panel">Owning panel that receives the selected section.</param>
    /// <param name="parent">Parent row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible button label.</param>
    private static void AddDetailsSectionButton(GameMasterPresetsPanel panel, VisualElement parent, GameMasterPresetsPanel.DetailsSectionType sectionType, string label)
    {
        Button button = new Button(() =>
        {
            panel.ActiveDetailsSection = sectionType;
            GameMasterPresetsPanelSidePanelUtility.SaveActiveDetailsSection(panel);
            BuildActiveDetailsSection(panel);
        });
        button.text = label;
        button.tooltip = "Show the " + label + " section.";
        button.style.flexShrink = 0f;
        button.style.minWidth = ResolveDetailsSectionButtonWidth(sectionType);
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Resolves a stable minimum width for Game Master detail section buttons.
    /// </summary>
    /// <param name="sectionType">Section represented by the selector button.</param>
    /// <returns>Minimum width that keeps the label readable before wrapping to a new row.</returns>
    private static float ResolveDetailsSectionButtonWidth(GameMasterPresetsPanel.DetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case GameMasterPresetsPanel.DetailsSectionType.SubPresets:
                return 96f;
            case GameMasterPresetsPanel.DetailsSectionType.ActiveAuthoring:
                return 124f;
            case GameMasterPresetsPanel.DetailsSectionType.Navigation:
                return 92f;
            default:
                return 84f;
        }
    }

    /// <summary>
    /// Creates a styled section container and registers its heading for recolor utilities.
    /// </summary>
    /// <param name="panel">Owning panel with active details root.</param>
    /// <param name="title">Section title.</param>
    /// <returns>Section container.</returns>
    private static VisualElement CreateSection(GameMasterPresetsPanel panel, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.Master." + title);
        section.Add(label);
        panel.DetailSectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Adds one bound text field and marks the draft dirty on edits.
    /// </summary>
    /// <param name="panel">Owning panel with serialized preset context.</param>
    /// <param name="parent">Parent section.</param>
    /// <param name="label">Display label.</param>
    /// <param name="propertyName">Serialized property name.</param>
    /// <param name="refreshList">True when list labels should update after change.</param>
    /// <param name="multiline">True when the field should use multiline editing.</param>
    private static void AddBoundTextField(GameMasterPresetsPanel panel, VisualElement parent, string label, string propertyName, bool refreshList, bool multiline)
    {
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        TextField field = new TextField(label);
        field.tooltip = "Edit " + label + " for this game master preset.";
        field.isDelayed = true;
        field.multiline = multiline;
        field.BindProperty(property);
        field.RegisterValueChangedCallback(evt =>
        {
            if (panel.SelectedPreset != null)
                Undo.RecordObject(panel.SelectedPreset, "Edit Game Master Preset");

            panel.PresetSerializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();

            if (refreshList)
                panel.RefreshPresetList();
        });
        parent.Add(field);
    }
    #endregion

    #endregion
}
