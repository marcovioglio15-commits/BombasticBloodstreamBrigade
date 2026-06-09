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
        // Persist the selection so close/reopen lands on the same preset.
        EnemyMasterPresetsPanelSidePanelUtility.SaveSelectedPresetState(panel);
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

    #endregion

    #endregion
}
