using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Root orchestration panel for HUD Manager presets.
/// </summary>
public sealed class GameHudManagerPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.GameManagement.HudManager.ActiveSection";
    internal const string SelectedPresetPathStateKey = "NashCore.GameManagement.HudManager.SelectedPreset";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<GameHudManagerPreset> filteredPresets = new List<GameHudManagerPreset>();
    private readonly List<string> validationWarnings = new List<string>();

    private GameHudManagerPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;
    private DetailsSectionType activeSection = DetailsSectionType.Metadata;
    private GameHudManagerPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    internal GameHudManagerPreset SelectedPreset
    {
        get
        {
            return selectedPreset;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes the HUD Manager panel and restores its active details section.
    /// </summary>
    public GameHudManagerPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;
        library = GameHudManagerPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, DetailsSectionType.Metadata);
        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds the panel from current HUD Manager assets after draft session changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameHudManagerPreset previousSelection = selectedPreset;
        library = GameHudManagerPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previousSelection != null && filteredPresets.Contains(previousSelection))
        {
            if (selectedPreset == previousSelection && presetSerializedObject != null && presetSerializedObject.targetObject == previousSelection)
                presetSerializedObject.UpdateIfRequiredOrScript();
            else
                SelectPreset(previousSelection);
        }
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the split preset browser and details panel.
    /// </summary>
    private void BuildUI()
    {
        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(LeftPaneWidth);
        splitView.Add(BuildLeftPane());
        splitView.Add(BuildRightPane());
        root.Add(splitView);
    }

    /// <summary>
    /// Builds the left preset browser pane.
    /// </summary>
    /// <returns>Left pane visual element.</returns>
    private VisualElement BuildLeftPane()
    {
        VisualElement leftPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(leftPane);
        leftPane.Add(BuildToolbar());

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter HUD Manager presets by name.";
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
        leftPane.Add(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(leftPane, searchField);

        listView = new ListView();
        GameManagementPanelLayoutUtility.ConfigureListView(listView);
        listView.itemsSource = filteredPresets;
        listView.selectionType = SelectionType.Single;
        listView.makeItem = MakePresetItem;
        listView.bindItem = BindPresetItem;
        listView.selectionChanged += OnPresetSelectionChanged;
        leftPane.Add(listView);
        return leftPane;
    }

    /// <summary>
    /// Builds create, duplicate and delete buttons for HUD Manager presets.
    /// </summary>
    /// <returns>Toolbar visual element.</returns>
    private Toolbar BuildToolbar()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(CreatePreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new HUD Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 52f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => DuplicatePreset(selectedPreset));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected HUD Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 72f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => DeletePreset(selectedPreset));
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Stage the selected HUD Manager preset for deletion.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 52f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the selected preset detail scroll area.
    /// </summary>
    /// <returns>Right pane visual element.</returns>
    private VisualElement BuildRightPane()
    {
        VisualElement rightPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(rightPane);

        detailsRoot = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsScrollView(detailsRoot);
        rightPane.Add(detailsRoot);
        return rightPane;
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes visible HUD Manager presets from the current library and search filter.
    /// </summary>
    internal void RefreshPresetList()
    {
        filteredPresets.Clear();
        string searchText = searchField != null ? searchField.value : string.Empty;

        if (library != null)
            AddMatchingPresets(searchText);

        if (listView != null)
            listView.Rebuild();

        if (filteredPresets.Count <= 0)
        {
            SelectPreset(null);
            return;
        }

        if (selectedPreset == null || !filteredPresets.Contains(selectedPreset))
        {
            GameHudManagerPreset restoredPreset = ManagementToolStateUtility.LoadAsset<GameHudManagerPreset>(SelectedPresetPathStateKey);
            GameHudManagerPreset initialPreset = restoredPreset != null && filteredPresets.Contains(restoredPreset)
                ? restoredPreset
                : filteredPresets[0];
            SelectPreset(initialPreset);
        }
    }

    /// <summary>
    /// Adds library presets that pass search and staged-delete filters.
    /// </summary>
    /// <param name="searchText">Current search text.</param>
    private void AddMatchingPresets(string searchText)
    {
        for (int index = 0; index < library.Presets.Count; index++)
        {
            GameHudManagerPreset preset = library.Presets[index];

            if (preset == null)
                continue;

            if (GameManagementDraftSession.IsAssetStagedForDeletion(preset))
                continue;

            if (GameHudManagerPresetsPanelUtility.MatchesSearch(preset, searchText))
                filteredPresets.Add(preset);
        }
    }

    /// <summary>
    /// Creates one list row label with context actions.
    /// </summary>
    /// <returns>List row label.</returns>
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            GameHudManagerPreset preset = label.userData as GameHudManagerPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one row to a filtered HUD Manager preset.
    /// </summary>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Filtered preset index.</param>
    private void BindPresetItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        GameHudManagerPreset preset = filteredPresets[index];
        label.userData = preset;
        label.text = GameHudManagerPresetsPanelUtility.GetPresetDisplayName(preset);
        label.tooltip = preset != null ? preset.Description : string.Empty;
    }

    /// <summary>
    /// Selects the first preset included in the ListView selection event.
    /// </summary>
    /// <param name="selection">Current ListView selection.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            GameHudManagerPreset preset = item as GameHudManagerPreset;

            if (preset == null)
                continue;

            if (selectedPreset == preset)
                return;

            SelectPreset(preset);
            return;
        }

        if (selectedPreset != null)
            SelectPreset(null);
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new HUD Manager preset.
    /// </summary>
    private void CreatePreset()
    {
        GameHudManagerPreset newPreset = GameHudManagerPresetLibraryUtility.CreatePresetAsset("GameHudManagerPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create HUD Manager Preset");
        Undo.RecordObject(library, "Add HUD Manager Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        GameManagementDraftSession.MarkDirty();
        RefreshPresetList();
        SelectPreset(newPreset);
    }

    /// <summary>
    /// Duplicates one HUD Manager preset asset and registers it.
    /// </summary>
    /// <param name="preset">Source preset to duplicate.</param>
    private void DuplicatePreset(GameHudManagerPreset preset)
    {
        if (preset == null)
            return;

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalPath) || string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string duplicateBaseName = GameManagementDraftSession.NormalizeAssetName(GameHudManagerPresetsPanelUtility.GetPresetDisplayName(preset) + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "GameHudManagerPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        GameHudManagerPreset duplicatedPreset = ScriptableObject.CreateInstance<GameHudManagerPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = Path.GetFileNameWithoutExtension(duplicatedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        GameHudManagerPresetsPanelUtility.SynchronizePresetMetadata(duplicatedPreset, duplicatedPreset.name, true);

        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate HUD Manager Preset");
        Undo.RecordObject(library, "Duplicate HUD Manager Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        GameManagementDraftSession.MarkDirty();
        RefreshPresetList();
        SelectPreset(duplicatedPreset);
    }

    /// <summary>
    /// Stages one HUD Manager preset for deletion after confirmation.
    /// </summary>
    /// <param name="preset">Preset to delete.</param>
    private void DeletePreset(GameHudManagerPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete HUD Manager Preset",
                                                     "Delete the selected HUD Manager preset asset?",
                                                     "Delete",
                                                     "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(library, "Delete HUD Manager Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        GameManagementDraftSession.StageDeleteAsset(preset);
        RefreshPresetList();
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one HUD Manager preset and rebuilds details.
    /// </summary>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    private void SelectPreset(GameHudManagerPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();

        if (listView != null && preset != null)
        {
            int selectedIndex = filteredPresets.IndexOf(preset);

            if (selectedIndex >= 0)
                listView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        ManagementToolStateUtility.SaveAssetPath(SelectedPresetPathStateKey, preset);

        if (selectedPreset == null)
        {
            detailsRoot.Add(new Label("Select or create a HUD manager preset to edit."));
            return;
        }

        selectedPreset.EnsureInitialized();
        presetSerializedObject = new SerializedObject(selectedPreset);
        sectionButtonsRoot = BuildSectionButtons();
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(sectionButtonsRoot);
        detailsRoot.Add(sectionContentRoot);
        BuildActiveSection();
    }

    /// <summary>
    /// Rebuilds the active HUD Manager details section.
    /// </summary>
    private void BuildActiveSection()
    {
        if (sectionContentRoot == null || presetSerializedObject == null)
            return;

        presetSerializedObject.Update();
        sectionContentRoot.Clear();

        switch (activeSection)
        {
            case DetailsSectionType.LevelExperience:
                GameHudManagerPresetsPanelUtility.BuildLevelExperienceSection(CreateSection("Level & Experience"), presetSerializedObject);
                break;
            case DetailsSectionType.ActivePowerUps:
                GameHudManagerPresetsPanelUtility.BuildActivePowerUpsSection(CreateSection("Active Power-Ups"), presetSerializedObject);
                break;
            case DetailsSectionType.RunTimer:
                GameHudManagerPresetsPanelUtility.BuildRunTimerSection(CreateSection("Run Timer"), presetSerializedObject);
                break;
            case DetailsSectionType.Combo:
                GameHudManagerPresetsPanelUtility.BuildComboCounterSection(CreateSection("Combo Counter"), presetSerializedObject);
                break;
            case DetailsSectionType.Milestone:
                GameHudManagerPresetsPanelUtility.BuildMilestoneSelectionSection(CreateSection("Milestone Selection"), presetSerializedObject);
                break;
            case DetailsSectionType.Damage:
                GameHudManagerPresetsPanelUtility.BuildDamageVignetteSection(CreateSection("Damage Vignettes"), presetSerializedObject);
                break;
            case DetailsSectionType.Validation:
                BuildValidationSection();
                break;
            default:
                BuildMetadataSection();
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(sectionContentRoot);
    }

    /// <summary>
    /// Builds metadata fields for the selected HUD Manager preset.
    /// </summary>
    private void BuildMetadataSection()
    {
        VisualElement section = CreateSection("Preset Details");
        AddProperty(section, "presetName", "Preset Name");
        AddProperty(section, "version", "Version");
        AddProperty(section, "description", "Description");

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        PropertyField idField = new PropertyField(idProperty, "Preset ID");
        idField.tooltip = "Stable ID used by Game Management Tool for this HUD preset.";
        idField.BindProperty(idProperty);
        idField.SetEnabled(false);
        section.Add(idField);
    }

    /// <summary>
    /// Builds one serialized settings section from a root property.
    /// </summary>
    /// <param name="title">Section title.</param>
    /// <param name="propertyName">Serialized root property name.</param>
    private void BuildSerializedSettingsSection(string title, string propertyName)
    {
        VisualElement section = CreateSection(title);
        SerializedProperty property = presetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        GameHudManagerPresetsPanelUtility.AddChildProperties(section, property, presetSerializedObject);
    }

    /// <summary>
    /// Builds validation warnings for the selected HUD Manager preset.
    /// </summary>
    private void BuildValidationSection()
    {
        VisualElement section = CreateSection("Validation");
        GameHudManagerPresetValidationUtility.CollectWarnings(selectedPreset, validationWarnings);
        GameHudManagerPresetSceneValidationUtility.CollectWarnings(selectedPreset, validationWarnings);

        if (validationWarnings.Count <= 0)
        {
            Label okLabel = new Label("No HUD Manager warnings.");
            okLabel.tooltip = "The selected HUD Manager preset passed non-mutating validation.";
            section.Add(okLabel);
            return;
        }

        for (int warningIndex = 0; warningIndex < validationWarnings.Count; warningIndex++)
        {
            HelpBox warningBox = new HelpBox(validationWarnings[warningIndex], HelpBoxMessageType.Warning);
            section.Add(warningBox);
        }
    }
    #endregion

    #region Detail Helpers
    /// <summary>
    /// Builds detail section selector buttons.
    /// </summary>
    /// <returns>Detail section button row.</returns>
    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddSectionButton(buttonsRoot, DetailsSectionType.Metadata, "Metadata", 84f);
        AddSectionButton(buttonsRoot, DetailsSectionType.LevelExperience, "Level & Experience", 148f);
        AddSectionButton(buttonsRoot, DetailsSectionType.ActivePowerUps, "Active Power-Ups", 132f);
        AddSectionButton(buttonsRoot, DetailsSectionType.RunTimer, "Run Timer", 88f);
        AddSectionButton(buttonsRoot, DetailsSectionType.Combo, "Combo", 72f);
        AddSectionButton(buttonsRoot, DetailsSectionType.Milestone, "Milestone", 92f);
        AddSectionButton(buttonsRoot, DetailsSectionType.Damage, "Damage", 84f);
        AddSectionButton(buttonsRoot, DetailsSectionType.Validation, "Validation", 92f);
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one detail section selector button.
    /// </summary>
    /// <param name="parent">Parent row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible button label.</param>
    /// <param name="minimumWidth">Minimum button width.</param>
    private void AddSectionButton(VisualElement parent, DetailsSectionType sectionType, string label, float minimumWidth)
    {
        Button button = new Button(() =>
        {
            activeSection = sectionType;
            ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
            BuildActiveSection();
        });
        button.text = label;
        button.tooltip = "Show the " + label + " section.";
        button.style.flexShrink = 0f;
        button.style.minWidth = minimumWidth;
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Creates a styled section container and registers its heading for recolor utilities.
    /// </summary>
    /// <param name="title">Section title.</param>
    /// <returns>Section container.</returns>
    private VisualElement CreateSection(string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.HUD." + title);
        section.Add(label);
        section.RegisterCallback<SerializedPropertyChangeEvent>(HandleSerializedPropertyChanged);
        sectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Adds every direct child property in a serialized settings object.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="rootProperty">Root settings property.</param>
    private void AddChildProperties(VisualElement parent, SerializedProperty rootProperty)
    {
        GameHudManagerPresetsPanelUtility.AddChildProperties(parent, rootProperty, presetSerializedObject);
    }

    /// <summary>
    /// Adds one serialized property field.
    /// </summary>
    /// <param name="parent">Parent visual element.</param>
    /// <param name="propertyPath">Serialized property path.</param>
    /// <param name="label">Displayed field label.</param>
    private void AddProperty(VisualElement parent, string propertyPath, string label)
    {
        GameHudManagerPresetsPanelUtility.AddProperty(parent, presetSerializedObject, propertyPath, label);
    }

    /// <summary>
    /// Applies serialized field edits and marks the draft session dirty.
    /// </summary>
    /// <param name="evt">Serialized property change event emitted by UI Toolkit.</param>
    private void HandleSerializedPropertyChanged(SerializedPropertyChangeEvent evt)
    {
        string changedPropertyPath = evt != null && evt.changedProperty != null
            ? evt.changedProperty.propertyPath
            : string.Empty;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Edit HUD Manager Preset");

        presetSerializedObject.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();

        if (activeSection == DetailsSectionType.Validation && !string.IsNullOrWhiteSpace(changedPropertyPath))
            BuildActiveSection();

        if (listView != null)
            listView.Rebuild();
    }

    #endregion

    #endregion
}
