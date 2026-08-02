using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Hosts split-view browsing and focused authoring tabs for game difficulty coefficient presets.
/// </summary>
public sealed class GameDifficultyScalingPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string AssetFolder = "Assets/Scriptable Objects/Game/Difficulty Scaling";
    private const string SelectedPresetStateKey = "NashCore.GameManagement.DifficultyScaling.SelectedPreset";
    private const string ActiveTabStateKey = "NashCore.GameManagement.DifficultyScaling.ActiveTab";
    #endregion

    #region Fields
    private readonly VisualElement root = new VisualElement();
    private readonly List<GameDifficultyScalingPreset> filteredPresets = new List<GameDifficultyScalingPreset>();
    private ListView presetListView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement tabRoot;
    private VisualElement tabContentRoot;
    private GameDifficultyScalingPreset selectedPreset;
    private SerializedObject serializedPreset;
    private DetailsTab activeTab;
    #endregion

    #region Properties
    public VisualElement Root => root;
    public GameDifficultyScalingPreset SelectedPreset => selectedPreset;
    #endregion

    #region Constructors
    /// <summary>
    /// Builds the Difficulty Scaling split view and restores the last selected asset and sub-tab.
    /// </summary>
    public GameDifficultyScalingPresetsPanel()
    {
        activeTab = ManagementToolStateUtility.LoadEnumValue(ActiveTabStateKey, DetailsTab.Coefficients);
        root.style.flexGrow = 1f;
        root.Add(BuildContent());
        RefreshFromSessionChange();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reloads draft-visible assets and retains the active preset whenever it still exists.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameDifficultyScalingPreset previousSelection = selectedPreset;
        RefreshPresetList();

        if (previousSelection != null && filteredPresets.Contains(previousSelection))
        {
            SelectPreset(previousSelection);
            return;
        }

        string selectedPath = EditorPrefs.GetString(SelectedPresetStateKey, string.Empty);
        GameDifficultyScalingPreset restoredPreset = AssetDatabase.LoadAssetAtPath<GameDifficultyScalingPreset>(selectedPath);

        if (restoredPreset != null && filteredPresets.Contains(restoredPreset))
        {
            SelectPreset(restoredPreset);
            return;
        }

        SelectPreset(filteredPresets.Count > 0 ? filteredPresets[0] : null);
    }

    /// <summary>
    /// Selects a preset requested by the owning Game Master panel.
    /// </summary>
    /// <param name="preset">Difficulty preset that should become active.</param>
    public void SelectPresetFromExternal(GameDifficultyScalingPreset preset)
    {
        if (preset == null)
            return;

        if (!filteredPresets.Contains(preset))
            RefreshPresetList();

        if (filteredPresets.Contains(preset))
            SelectPreset(preset);
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds the persistent browser and details panes used by every difficulty authoring tab.
    /// </summary>
    /// <returns>Configured horizontal split view.</returns>
    private VisualElement BuildContent()
    {
        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(LeftPaneWidth);
        VisualElement browserPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(browserPane);

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Difficulty Scaling presets by asset or designer-facing name.";
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(browserPane, searchField);
        browserPane.Add(searchField);

        Toolbar actions = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(actions);
        Button createButton = new Button(CreatePreset) { text = "New" };
        createButton.tooltip = "Create a new Difficulty Scaling preset in the Game asset root.";
        Button duplicateButton = new Button(DuplicateSelectedPreset) { text = "Duplicate" };
        duplicateButton.tooltip = "Duplicate the selected coefficient graph.";
        Button deleteButton = new Button(DeleteSelectedPreset) { text = "Delete" };
        deleteButton.tooltip = "Stage the selected preset for deletion when draft changes are applied.";
        actions.Add(createButton);
        actions.Add(duplicateButton);
        actions.Add(deleteButton);
        browserPane.Add(actions);

        presetListView = new ListView(filteredPresets, 22f, MakePresetRow, BindPresetRow);
        presetListView.selectionType = SelectionType.Single;
        presetListView.selectionChanged += selection => SelectFirst(selection);
        GameManagementPanelLayoutUtility.ConfigureListView(presetListView);
        browserPane.Add(presetListView);

        detailsRoot = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(detailsRoot);
        splitView.Add(browserPane);
        splitView.Add(detailsRoot);
        return splitView;
    }

    /// <summary>
    /// Builds one reusable row label for the difficulty preset browser.
    /// </summary>
    /// <returns>Configured row label.</returns>
    private static VisualElement MakePresetRow()
    {
        Label label = new Label();
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        return label;
    }

    /// <summary>
    /// Binds one browser row to a live Difficulty Scaling preset.
    /// </summary>
    /// <param name="element">Reusable row root supplied by the ListView.</param>
    /// <param name="index">Filtered preset index represented by the row.</param>
    private void BindPresetRow(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null || index < 0 || index >= filteredPresets.Count)
            return;

        GameDifficultyScalingPreset preset = filteredPresets[index];
        label.text = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        label.tooltip = AssetDatabase.GetAssetPath(preset);
    }

    /// <summary>
    /// Builds the compact details sub-tab toolbar for the selected preset.
    /// </summary>
    /// <returns>Toolbar containing metadata, coefficients and validation tabs.</returns>
    private VisualElement BuildTabs()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);
        AddTabButton(toolbar, DetailsTab.Metadata, "Metadata");
        AddTabButton(toolbar, DetailsTab.Coefficients, "Coefficients");
        AddTabButton(toolbar, DetailsTab.Validation, "Validation");
        return toolbar;
    }

    /// <summary>
    /// Adds one stateful details-tab button.
    /// </summary>
    /// <param name="toolbar">Toolbar receiving the button.</param>
    /// <param name="tab">Details tab activated by the button.</param>
    /// <param name="label">Designer-facing button label.</param>
    private void AddTabButton(Toolbar toolbar, DetailsTab tab, string label)
    {
        Button button = new Button(() =>
        {
            activeTab = tab;
            ManagementToolStateUtility.SaveEnumValue(ActiveTabStateKey, activeTab);
            BuildActiveTab();
        });
        button.text = label;
        button.tooltip = "Show Difficulty Scaling " + label + ".";
        toolbar.Add(button);
    }
    #endregion

    #region Preset Methods
    /// <summary>
    /// Refreshes the filtered asset list without changing serialized tuning.
    /// </summary>
    private void RefreshPresetList()
    {
        List<GameDifficultyScalingPreset> allPresets =
            GameManagementStandalonePresetAssetUtility.FindAssets<GameDifficultyScalingPreset>();
        string searchText = searchField == null ? string.Empty : searchField.value;
        filteredPresets.Clear();

        // Apply the current search to both asset and display names.
        for (int presetIndex = 0; presetIndex < allPresets.Count; presetIndex++)
        {
            GameDifficultyScalingPreset preset = allPresets[presetIndex];

            if (!string.IsNullOrWhiteSpace(searchText) &&
                preset.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                preset.PresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            filteredPresets.Add(preset);
        }

        if (presetListView != null)
            presetListView.Rebuild();
    }

    /// <summary>
    /// Creates and selects a new initialized Difficulty Scaling preset.
    /// </summary>
    private void CreatePreset()
    {
        GameDifficultyScalingPreset preset =
            GameManagementStandalonePresetAssetUtility.CreateAsset<GameDifficultyScalingPreset>(
                AssetFolder,
                "GameDifficultyScalingPreset",
                createdPreset => createdPreset.EnsureInitialized());
        RefreshPresetList();
        SelectPreset(preset);
    }

    /// <summary>
    /// Duplicates and selects the current Difficulty Scaling preset.
    /// </summary>
    private void DuplicateSelectedPreset()
    {
        GameDifficultyScalingPreset duplicate =
            GameManagementStandalonePresetAssetUtility.DuplicateAsset(selectedPreset,
                                                                      createdPreset => createdPreset.EnsureInitialized());

        if (duplicate == null)
            return;

        RefreshPresetList();
        SelectPreset(duplicate);
    }

    /// <summary>
    /// Stages the current Difficulty Scaling preset for deletion after explicit confirmation.
    /// </summary>
    private void DeleteSelectedPreset()
    {
        if (selectedPreset == null ||
            !EditorUtility.DisplayDialog("Delete Difficulty Scaling Preset",
                                         "Stage '" + selectedPreset.name + "' for deletion?",
                                         "Delete",
                                         "Cancel"))
        {
            return;
        }

        GameManagementStandalonePresetAssetUtility.StageDelete(selectedPreset);
        selectedPreset = null;
        RefreshPresetList();
        SelectPreset(filteredPresets.Count > 0 ? filteredPresets[0] : null);
    }

    /// <summary>
    /// Selects the first object supplied by a ListView selection event.
    /// </summary>
    /// <param name="selection">Selected preset objects.</param>
    private void SelectFirst(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            SelectPreset(selectedObject as GameDifficultyScalingPreset);
            return;
        }
    }

    /// <summary>
    /// Rebinds the details view to one difficulty preset.
    /// </summary>
    /// <param name="preset">Preset to edit, or null to clear the details pane.</param>
    private void SelectPreset(GameDifficultyScalingPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();

        if (preset == null)
        {
            serializedPreset = null;
            detailsRoot.Add(new Label("Select or create a Difficulty Scaling preset to edit."));
            return;
        }

        preset.EnsureInitialized();
        EditorPrefs.SetString(SelectedPresetStateKey, AssetDatabase.GetAssetPath(preset));
        serializedPreset = new SerializedObject(preset);
        tabRoot = BuildTabs();
        tabContentRoot = new VisualElement();
        tabContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(tabRoot);
        detailsRoot.Add(tabContentRoot);
        BuildActiveTab();
    }
    #endregion

    #region Details Methods
    /// <summary>
    /// Rebuilds only the active difficulty authoring tab.
    /// </summary>
    private void BuildActiveTab()
    {
        if (serializedPreset == null || tabContentRoot == null)
            return;

        serializedPreset.UpdateIfRequiredOrScript();
        tabContentRoot.Clear();

        switch (activeTab)
        {
            case DetailsTab.Metadata:
                AddBoundProperty("presetName", "Preset Name");
                AddBoundProperty("version", "Version");
                AddBoundProperty("description", "Description");
                AddBoundProperty("playerContextPreset", "Player Context Preset");
                break;
            case DetailsTab.Validation:
                BuildValidationTab();
                break;
            default:
                AddBoundProperty("coefficients", "Difficulty Coefficients");
                break;
        }
    }

    /// <summary>
    /// Adds one serialized property with draft tracking and its authored tooltip.
    /// </summary>
    /// <param name="propertyName">Serialized field name in the selected preset.</param>
    /// <param name="label">Designer-facing field label.</param>
    private void AddBoundProperty(string propertyName, string label)
    {
        SerializedProperty property = serializedPreset.FindProperty(propertyName);

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = property.tooltip;
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt => GameManagementDraftSession.MarkDirty());
        tabContentRoot.Add(field);
    }

    /// <summary>
    /// Displays current coefficient, formula-variable and dependency warnings without modifying the preset.
    /// </summary>
    private void BuildValidationTab()
    {
        List<string> warnings = GameDifficultyScalingValidationUtility.BuildWarnings(selectedPreset);

        if (selectedPreset.PlayerContextPreset != null &&
            selectedPreset.PlayerContextPreset.ProgressionPreset != null)
        {
            SerializedObject progressionObject =
                new SerializedObject(selectedPreset.PlayerContextPreset.ProgressionPreset);
            warnings.AddRange(
                PlayerScalingDependencyValidationUtility.BuildDifficultyCrossDependencyWarnings(
                    progressionObject.FindProperty("scalableStats"),
                    progressionObject.FindProperty("scalingRules")));
        }

        Label summary = new Label(warnings.Count == 0
            ? "No validation warnings. The coefficient graph is bake-safe."
            : warnings.Count + " warning(s) require attention before baking.");
        summary.style.unityFontStyleAndWeight = FontStyle.Bold;
        tabContentRoot.Add(summary);

        // Keep every warning visible and independently readable for designers.
        for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
        {
            HelpBox warning = new HelpBox(warnings[warningIndex], HelpBoxMessageType.Warning);
            tabContentRoot.Add(warning);
        }

        Foldout variables = new Foldout { text = "Available Formula Variables" };
        HashSet<string> availableVariables = GameDifficultyScalingValidationUtility.BuildAvailableVariableSet(selectedPreset);
        List<string> orderedVariables = new List<string>(availableVariables);
        orderedVariables.Sort(StringComparer.OrdinalIgnoreCase);
        variables.Add(new Label(string.Join("\n", orderedVariables)));
        tabContentRoot.Add(variables);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Difficulty Scaling detail tabs kept separate to preserve a compact authoring surface.
    /// </summary>
    private enum DetailsTab
    {
        Metadata = 0,
        Coefficients = 1,
        Validation = 2
    }
    #endregion
}
