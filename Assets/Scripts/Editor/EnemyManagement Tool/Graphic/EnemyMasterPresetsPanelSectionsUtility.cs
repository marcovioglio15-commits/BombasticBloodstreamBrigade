using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and prefab activation UI for enemy master preset panels.
/// </summary>
internal static class EnemyMasterPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the metadata section for the selected enemy master preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildMetadataSection(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

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
            EnemyManagementDraftSession.MarkDirty();
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
            EnemyManagementDraftSession.MarkDirty();
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
    /// Builds the sub preset assignment section.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildSubPresetsSection(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Sub Presets");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty brainProperty = presetSerializedObject.FindProperty("brainPreset");
        SerializedProperty visualProperty = presetSerializedObject.FindProperty("visualPreset");
        SerializedProperty advancedPatternProperty = presetSerializedObject.FindProperty("advancedPatternPreset");
        SerializedProperty bossPatternProperty = presetSerializedObject.FindProperty("bossPatternPreset");

        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Brain Preset",
                                               typeof(EnemyBrainPreset),
                                               brainProperty,
                                               panel.CreateBrainPreset,
                                               () => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyBrainPresets),
                                               EnemyManagementWindow.PanelType.EnemyBrainPresets));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Visual Preset",
                                               typeof(EnemyVisualPreset),
                                               visualProperty,
                                               panel.CreateVisualPreset,
                                               () => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyVisualPresets),
                                               EnemyManagementWindow.PanelType.EnemyVisualPresets));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Advanced Pattern Preset",
                                               typeof(EnemyAdvancedPatternPreset),
                                               advancedPatternProperty,
                                               panel.CreateAdvancedPatternPreset,
                                               () => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets),
                                               EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets));
        sectionContainer.Add(BuildSubPresetRow(panel,
                                               "Boss Pattern Preset",
                                               typeof(EnemyBossPatternPreset),
                                               bossPatternProperty,
                                               panel.CreateBossPatternPreset,
                                               () => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyBossPatternPresets),
                                               EnemyManagementWindow.PanelType.EnemyBossPatternPresets));
    }

    /// <summary>
    /// Builds the active preset section used to assign the selected master preset to one enemy prefab.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>

    public static void BuildActivePresetSection(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Active on Enemy Prefab");

        if (sectionContainer == null)
            return;

        panel.RefreshAvailableEnemyPrefabs();
        int selectedPrefabIndex = panel.ResolveSelectedEnemyPrefabIndex();
        PopupField<GameObject> enemyPrefabPopup = new PopupField<GameObject>("Enemy Prefab",
                                                                             panel.AvailableEnemyPrefabs,
                                                                             selectedPrefabIndex,
                                                                             EnemyMasterPresetsPanel.ResolveEnemyPrefabDisplayName,
                                                                             EnemyMasterPresetsPanel.ResolveEnemyPrefabDisplayName);
        enemyPrefabPopup.tooltip = "Project prefab selector filtered to assets containing EnemyAuthoring in hierarchy.";
        enemyPrefabPopup.RegisterValueChangedCallback(evt =>
        {
            panel.SelectedEnemyPrefab = evt.newValue;
            EnemyMasterPresetsPanelSidePanelUtility.SaveSelectedPrefabState(panel);
            panel.BuildActiveDetailsSection();
        });
        panel.EnemyPrefabPopup = enemyPrefabPopup;
        sectionContainer.Add(enemyPrefabPopup);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.marginTop = 2f;

        Button refreshPrefabsButton = new Button(panel.RefreshEnemyPrefabSelection);
        refreshPrefabsButton.text = "Refresh Prefabs";
        refreshPrefabsButton.tooltip = "Rescan project prefabs containing EnemyAuthoring.";
        buttonRow.Add(refreshPrefabsButton);

        Button pingPrefabButton = new Button(panel.PingSelectedEnemyPrefab);
        pingPrefabButton.text = "Ping";
        pingPrefabButton.tooltip = "Highlight selected enemy prefab asset in Project window.";
        pingPrefabButton.style.marginLeft = 4f;
        pingPrefabButton.SetEnabled(panel.SelectedEnemyPrefab != null);
        buttonRow.Add(pingPrefabButton);

        Button setActiveButton = new Button(panel.AssignPresetToPrefab);
        setActiveButton.text = "Set Active Preset";
        setActiveButton.tooltip = "Assign selected Master Preset and its sub-presets to this enemy prefab only.";
        setActiveButton.style.marginLeft = 4f;
        setActiveButton.SetEnabled(panel.SelectedEnemyPrefab != null);
        buttonRow.Add(setActiveButton);

        sectionContainer.Add(buttonRow);

        Label activeStatusLabel = new Label();
        activeStatusLabel.style.marginTop = 2f;
        activeStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        panel.ActiveStatusLabel = activeStatusLabel;
        sectionContainer.Add(activeStatusLabel);
        panel.RefreshActiveStatus();

        BuildTestUiActionsSection(panel, sectionContainer);
        BuildTestUiSettingsSection(panel, sectionContainer);
    }

    /// <summary>
    /// Builds the navigation section used to open related preset panels.
    /// </summary>
    /// <param name="panel">Owning panel that provides side-panel callbacks.</param>

    public static void BuildNavigationSection(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Open Sections");

        if (sectionContainer == null)
            return;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button openBrainButton = new Button(() => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyBrainPresets));
        openBrainButton.text = "Open Brain";
        row.Add(openBrainButton);

        Button openVisualButton = new Button(() => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyVisualPresets));
        openVisualButton.text = "Open Visual";
        openVisualButton.style.marginLeft = 4f;
        row.Add(openVisualButton);

        Button openAdvancedPatternButton = new Button(() => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets));
        openAdvancedPatternButton.text = "Open Advanced Pattern";
        openAdvancedPatternButton.style.marginLeft = 4f;
        row.Add(openAdvancedPatternButton);

        Button openBossPatternButton = new Button(() => EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyBossPatternPresets));
        openBossPatternButton.text = "Open Boss Patterns";
        openBossPatternButton.style.marginLeft = 4f;
        row.Add(openBossPatternButton);

        sectionContainer.Add(row);
    }

    /// <summary>
    /// Selects one preset and rebuilds the detail area plus linked side-panel synchronization.
    /// </summary>
    /// <param name="panel">Owning panel that stores selection and detail roots.</param>
    /// <param name="preset">Preset to select, or null to clear the detail view.</param>

    public static void SelectPreset(EnemyMasterPresetsPanel panel, EnemyMasterPreset preset)
    {
        if (panel == null)
            return;

        panel.SelectedPreset = preset;
        panel.DetailsRoot.Clear();
        panel.DetailSectionButtonsRoot = null;
        panel.DetailSectionContentRoot = null;

        if (panel.SelectedPreset == null)
        {
            Label label = new Label("Select or create an enemy master preset to edit.");
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
        EnemyMasterPresetsPanelSidePanelUtility.SyncOpenSidePanels(panel);
    }

    /// <summary>
    /// Builds the detail section tab row.
    /// </summary>
    /// <param name="panel">Owning panel that provides section activation callbacks.</param>
    /// <returns>Returns the constructed tab row.</returns>
    public static VisualElement BuildDetailsSectionButtons(EnemyMasterPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddDetailsSectionButton(panel, buttonsRoot, EnemyMasterPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddDetailsSectionButton(panel, buttonsRoot, EnemyMasterPresetsPanel.DetailsSectionType.SubPresets, "Sub Presets");
        AddDetailsSectionButton(panel, buttonsRoot, EnemyMasterPresetsPanel.DetailsSectionType.ActivePreset, "Active Preset");
        AddDetailsSectionButton(panel, buttonsRoot, EnemyMasterPresetsPanel.DetailsSectionType.Navigation, "Navigation");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one detail section button to the provided tab row.
    /// </summary>
    /// <param name="panel">Owning panel that receives the activation callback.</param>
    /// <param name="parent">Parent row that receives the button.</param>
    /// <param name="sectionType">Target details section.</param>
    /// <param name="buttonLabel">Button label.</param>

    public static void AddDetailsSectionButton(EnemyMasterPresetsPanel panel,
                                               VisualElement parent,
                                               EnemyMasterPresetsPanel.DetailsSectionType sectionType,
                                               string buttonLabel)
    {
        Button sectionButton = new Button(() => SetActiveDetailsSection(panel, sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    /// <summary>
    /// Sets the active detail section and rebuilds the section content.
    /// </summary>
    /// <param name="panel">Owning panel that stores active section state.</param>
    /// <param name="sectionType">Target detail section.</param>

    public static void SetActiveDetailsSection(EnemyMasterPresetsPanel panel, EnemyMasterPresetsPanel.DetailsSectionType sectionType)
    {
        panel.ActiveDetailsSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue("NashCore.EnemyManagement.Master.ActiveDetailsSection", panel.ActiveDetailsSection);
        BuildActiveDetailsSection(panel);
    }

    /// <summary>
    /// Rebuilds the active detail section content according to the current panel state.
    /// </summary>
    /// <param name="panel">Owning panel that stores serialized preset context and active section state.</param>

    public static void BuildActiveDetailsSection(EnemyMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        if (panel.DetailSectionContentRoot == null)
            return;

        if (panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.DetailSectionContentRoot.Clear();

        switch (panel.ActiveDetailsSection)
        {
            case EnemyMasterPresetsPanel.DetailsSectionType.Metadata:
                BuildMetadataSection(panel);
                break;
            case EnemyMasterPresetsPanel.DetailsSectionType.SubPresets:
                BuildSubPresetsSection(panel);
                break;
            case EnemyMasterPresetsPanel.DetailsSectionType.ActivePreset:
                BuildActivePresetSection(panel);
                break;
            case EnemyMasterPresetsPanel.DetailsSectionType.Navigation:
                BuildNavigationSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.DetailSectionContentRoot);
    }

    /// <summary>
    /// Creates one new enemy brain preset, registers it in the library, assigns it and opens the related side panel.
    /// </summary>
    /// <param name="panel">Owning panel that provides assignment callbacks and selection sync.</param>

    public static void CreateBrainPreset(EnemyMasterPresetsPanel panel)
    {
        EnemyBrainPreset newPreset = EnemyBrainPresetLibraryUtility.CreatePresetAsset("EnemyBrainPreset");

        if (newPreset == null)
            return;

        EnemyBrainPresetLibrary brainLibrary = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Brain Preset Asset");
        Undo.RecordObject(brainLibrary, "Add Enemy Brain Preset");
        brainLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(brainLibrary);
        EnemyManagementDraftSession.MarkDirty();

        AssignSubPreset(panel, "brainPreset", newPreset);
        EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyBrainPresets);
    }

    /// <summary>
    /// Creates one new enemy advanced pattern preset, registers it in the library, assigns it and opens the related side panel.
    /// </summary>
    /// <param name="panel">Owning panel that provides assignment callbacks and selection sync.</param>

    public static void CreateAdvancedPatternPreset(EnemyMasterPresetsPanel panel)
    {
        EnemyAdvancedPatternPreset newPreset = EnemyAdvancedPatternPresetLibraryUtility.CreatePresetAsset("EnemyAdvancedPatternPreset");

        if (newPreset == null)
            return;

        EnemyAdvancedPatternPresetLibrary advancedPatternLibrary = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Advanced Pattern Preset Asset");
        Undo.RecordObject(advancedPatternLibrary, "Add Enemy Advanced Pattern Preset");
        advancedPatternLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(advancedPatternLibrary);
        EnemyManagementDraftSession.MarkDirty();

        AssignSubPreset(panel, "advancedPatternPreset", newPreset);
        EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets);
    }

    /// <summary>
    /// Creates one new boss pattern preset, registers it in the library, assigns it and opens the related side panel.
    /// </summary>
    /// <param name="panel">Owning panel that provides assignment callbacks and selection sync.</param>

    public static void CreateBossPatternPreset(EnemyMasterPresetsPanel panel)
    {
        EnemyBossPatternPreset newPreset = EnemyBossPatternPresetLibraryUtility.CreatePresetAsset("EnemyBossPatternPreset");

        if (newPreset == null)
            return;

        EnemyBossPatternPresetLibrary bossPatternLibrary = EnemyBossPatternPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Boss Pattern Preset Asset");
        Undo.RecordObject(bossPatternLibrary, "Add Enemy Boss Pattern Preset");
        bossPatternLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(bossPatternLibrary);
        EnemyManagementDraftSession.MarkDirty();

        AssignSubPreset(panel, "bossPatternPreset", newPreset);
        EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyBossPatternPresets);
    }

    /// <summary>
    /// Creates one new enemy visual preset, registers it in the library, assigns it and opens the related side panel.
    /// </summary>
    /// <param name="panel">Owning panel that provides assignment callbacks and selection sync.</param>

    public static void CreateVisualPreset(EnemyMasterPresetsPanel panel)
    {
        EnemyVisualPreset newPreset = EnemyVisualPresetLibraryUtility.CreatePresetAsset("EnemyVisualPreset");

        if (newPreset == null)
            return;

        EnemyVisualPresetLibrary visualLibrary = EnemyVisualPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Visual Preset Asset");
        Undo.RecordObject(visualLibrary, "Add Enemy Visual Preset");
        visualLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(visualLibrary);
        EnemyManagementDraftSession.MarkDirty();

        AssignSubPreset(panel, "visualPreset", newPreset);
        EnemyMasterPresetsPanelSidePanelUtility.OpenSidePanel(panel, EnemyManagementWindow.PanelType.EnemyVisualPresets);
    }

    /// <summary>
    /// Assigns one linked sub preset reference on the currently selected master preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and side-panel synchronization.</param>
    /// <param name="propertyName">Serialized reference property name on the master preset.</param>
    /// <param name="preset">Sub preset asset to assign.</param>

    public static void AssignSubPreset(EnemyMasterPresetsPanel panel, string propertyName, UnityEngine.Object preset)
    {
        if (panel == null)
            return;

        if (panel.SelectedPreset == null)
            return;

        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Enemy Sub Preset");
        panel.PresetSerializedObject.Update();
        property.objectReferenceValue = preset;
        panel.PresetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        EnemyMasterPresetsPanelSidePanelUtility.SyncOpenSidePanels(panel);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the standard details section container under the panel content root.
    /// </summary>
    /// <param name="panel">Owning panel that provides the content root.</param>
    /// <param name="sectionTitle">Section header text.</param>
    /// <returns>Returns the created section container, or null when the panel is not ready.</returns>
    private static VisualElement CreateDetailsSectionContainer(EnemyMasterPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        if (panel.DetailSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.Master.Section." + sectionTitle);
        container.Add(header);
        panel.DetailSectionContentRoot.Add(container);
        return container;
    }

    /// <summary>
    /// Adds one tracked property field that marks the draft session dirty on change.
    /// </summary>
    /// <param name="parent">Parent container that receives the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">Display label shown by the field.</param>

    private static void AddTrackedPropertyField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
    }

    /// <summary>
    /// Builds one object-field row for assigning and managing one linked sub preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides synchronization callbacks.</param>
    /// <param name="label">Object field label.</param>
    /// <param name="presetType">Expected object type for the sub preset reference.</param>
    /// <param name="presetProperty">Serialized sub preset property.</param>
    /// <param name="createAction">Callback used to create and assign a new sub preset.</param>
    /// <param name="openSectionAction">Callback used to open the related side panel.</param>
    /// <param name="panelType">Target side panel type associated with the sub preset.</param>
    /// <returns>Returns the constructed row container.</returns>
    private static VisualElement BuildSubPresetRow(EnemyMasterPresetsPanel panel,
                                                   string label,
                                                   Type presetType,
                                                   SerializedProperty presetProperty,
                                                   Action createAction,
                                                   Action openSectionAction,
                                                   EnemyManagementWindow.PanelType panelType)
    {
        VisualElement container = new VisualElement();
        container.style.marginBottom = 6f;

        ObjectField presetField = new ObjectField(label);
        presetField.objectType = presetType;
        presetField.allowSceneObjects = false;

        if (presetProperty != null)
            presetField.SetValueWithoutNotify(presetProperty.objectReferenceValue);

        presetField.RegisterValueChangedCallback(evt =>
        {
            if (panel == null || panel.SelectedPreset == null || presetProperty == null)
                return;

            Undo.RecordObject(panel.SelectedPreset, "Assign Enemy Sub Preset");
            panel.PresetSerializedObject.Update();
            presetProperty.objectReferenceValue = evt.newValue;
            panel.PresetSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel.SelectedPreset);
            EnemyManagementDraftSession.MarkDirty();

            if (panelType != EnemyManagementWindow.PanelType.EnemyMasterPresets)
                EnemyMasterPresetsPanelSidePanelUtility.SyncOpenSidePanels(panel);
        });
        container.Add(presetField);

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.marginTop = 2f;

        Button openButton = new Button(openSectionAction);
        openButton.text = "Open Section";
        openButton.tooltip = "Open the corresponding sub preset section.";
        buttonsRow.Add(openButton);

        Button newButton = new Button(createAction);
        newButton.text = "New";
        newButton.tooltip = "Create and assign a new sub preset.";
        newButton.style.marginLeft = 4f;
        buttonsRow.Add(newButton);

        Button selectButton = new Button(() =>
        {
            if (presetProperty == null)
                return;

            UnityEngine.Object target = presetProperty.objectReferenceValue;

            if (target == null)
                return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        });
        selectButton.text = "Select in Project";
        selectButton.tooltip = "Select the assigned sub preset in the Project window.";
        selectButton.style.marginLeft = 4f;
        buttonsRow.Add(selectButton);

        container.Add(buttonsRow);
        return container;
    }

    /// <summary>
    /// Builds the Test UI actions subsection under the active preset section.
    /// </summary>
    /// <param name="panel">Owning panel that provides prefab and action callbacks.</param>
    /// <param name="sectionContainer">Section container that receives the subsection.</param>

    private static void BuildTestUiActionsSection(EnemyMasterPresetsPanel panel, VisualElement sectionContainer)
    {
        if (sectionContainer == null)
            return;

        VisualElement actionsContainer = new VisualElement();
        actionsContainer.style.marginTop = 10f;
        sectionContainer.Add(actionsContainer);

        Label headerLabel = new Label("Test UI Actions");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.marginBottom = 4f;
        actionsContainer.Add(headerLabel);

        if (panel.SelectedEnemyPrefab == null)
        {
            Label noPrefabLabel = new Label("Select an enemy prefab to enable Test UI actions.");
            noPrefabLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            actionsContainer.Add(noPrefabLabel);
            return;
        }

        EnemyAuthoring enemyAuthoring = panel.SelectedEnemyPrefab.GetComponentInChildren<EnemyAuthoring>(true);

        if (enemyAuthoring == null)
        {
            Label missingAuthoringLabel = new Label("EnemyAuthoring component not found on selected prefab.");
            missingAuthoringLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            actionsContainer.Add(missingAuthoringLabel);
            return;
        }

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.marginTop = 2f;

        Button generateButton = new Button(panel.GenerateTestUiOnPrefab);
        generateButton.text = "Generate Test UI";
        generateButton.tooltip = "Generate world-space health and shield bars on selected prefab and assign them to EnemyAuthoring.";
        buttonsRow.Add(generateButton);

        bool hasGeneratedTestUi = EnemyStatusBarsTestUiPrefabUtility.HasGeneratedTestUi(panel.SelectedEnemyPrefab);

        Button deleteButton = new Button(panel.DeleteTestUiOnPrefab);
        deleteButton.text = "Delete Test UI";
        deleteButton.tooltip = "Delete generated test status bars from selected prefab and clear assignment if generated.";
        deleteButton.style.marginLeft = 4f;
        deleteButton.SetEnabled(hasGeneratedTestUi);
        buttonsRow.Add(deleteButton);

        actionsContainer.Add(buttonsRow);

        Label testUiStatusLabel = new Label();
        testUiStatusLabel.style.marginTop = 2f;
        testUiStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        panel.TestUiStatusLabel = testUiStatusLabel;
        actionsContainer.Add(testUiStatusLabel);
        panel.RefreshTestUiStatus();
    }

    /// <summary>
    /// Builds the Test UI settings subsection under the active preset section.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context.</param>
    /// <param name="sectionContainer">Section container that receives the subsection.</param>

    private static void BuildTestUiSettingsSection(EnemyMasterPresetsPanel panel, VisualElement sectionContainer)
    {
        if (panel == null)
            return;

        if (sectionContainer == null)
            return;

        SerializedProperty testUiSettingsProperty = panel.PresetSerializedObject.FindProperty("testUiSettings");

        if (testUiSettingsProperty == null)
            return;

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginTop = 10f;
        sectionContainer.Add(settingsContainer);

        Label headerLabel = new Label("Test UI Settings");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.marginBottom = 4f;
        settingsContainer.Add(headerLabel);

        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("worldOffset"), "World Offset");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("rootWidthPixels"), "Root Width Pixels");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("rootHeightPixels"), "Root Height Pixels");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("worldScale"), "World Scale");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("canvasSortingOrder"), "Canvas Sorting Order");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBarWidthPixels"), "Health Bar Width");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBarHeightPixels"), "Health Bar Height");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBarYOffsetPixels"), "Health Bar Y Offset");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBarWidthPixels"), "Shield Bar Width");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBarHeightPixels"), "Shield Bar Height");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBarYOffsetPixels"), "Shield Bar Y Offset");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthFillColor"), "Health Fill Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBackgroundColor"), "Health Background Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldFillColor"), "Shield Fill Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBackgroundColor"), "Shield Background Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("hideShieldWhenEmpty"), "Hide Shield When Empty");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("hideWhenEnemyInactive"), "Hide When Enemy Inactive");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("hideWhenEnemyCulled"), "Hide When Enemy Culled");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldSmoothingSeconds"), "Shield Smoothing Seconds");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("billboardToCamera"), "Billboard To Camera");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("billboardYawOnly"), "Billboard Yaw Only");
    }
    #endregion

    #endregion
}
