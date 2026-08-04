using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// Hosts Waves preset browsing, top-down scene painting, brush categories and ordered parallel wave steps.
/// </summary>
public sealed class GameWavesPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string AssetFolder = "Assets/Scriptable Objects/Game/Waves";
    private const string SelectedPresetStateKey = "NashCore.GameManagement.Waves.SelectedPreset";
    private const string ActiveTabStateKey = "NashCore.GameManagement.Waves.ActiveTab";
    #endregion

    #region Fields
    private readonly VisualElement root = new VisualElement();
    private readonly List<GameWavesPreset> filteredPresets = new List<GameWavesPreset>();
    private readonly GameWavesPreviewRenderer previewRenderer = new GameWavesPreviewRenderer();
    private ListView presetListView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement tabContentRoot;
    private GameWavesPreset selectedPreset;
    private SerializedObject serializedPreset;
    private SerializedObject waveSerializedObject;
    private EnemyWavePreset selectedSequenceWavePreset;
    private DetailsTab activeTab;
    private int selectedSceneIndex;
    private int selectedWaveIndex;
    private int selectedCategoryIndex;
    private Vector2Int? selectedCellCoordinate;
    private int brushEnemyCount = 1;
    private float previewZoom = 1f;
    private bool eraseBrush;
    private string sceneWarning;
    #endregion

    #region Properties
    public VisualElement Root => root;
    public GameWavesPreset SelectedPreset => selectedPreset;
    #endregion
    #region Constructors
    /// <summary>
    /// Builds the Waves split view and restores its last preset and sub-tab.
    /// </summary>
    public GameWavesPresetsPanel()
    {
        activeTab = ManagementToolStateUtility.LoadEnumValue(ActiveTabStateKey, DetailsTab.SceneBrush);
        root.style.flexGrow = 1f;
        root.Add(BuildContent());
        root.RegisterCallback<DetachFromPanelEvent>(evt => previewRenderer.Dispose());
        RefreshFromSessionChange();
    }
    #endregion
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reloads draft-visible mapping assets while retaining a valid current selection.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameWavesPreset previousSelection = selectedPreset;
        RefreshPresetList();

        if (previousSelection != null && filteredPresets.Contains(previousSelection))
        {
            SelectPreset(previousSelection);
            return;
        }

        GameWavesPreset restoredPreset = AssetDatabase.LoadAssetAtPath<GameWavesPreset>(
            EditorPrefs.GetString(SelectedPresetStateKey, string.Empty));
        SelectPreset(restoredPreset != null && filteredPresets.Contains(restoredPreset)
            ? restoredPreset
            : filteredPresets.Count > 0 ? filteredPresets[0] : null);
    }

    /// <summary>
    /// Selects a Waves preset requested by the owning Game Master panel.
    /// </summary>
    /// <param name="preset">Waves preset that should become active.</param>
    public void SelectPresetFromExternal(GameWavesPreset preset)
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
    /// Builds the persistent split view containing preset browser and tabbed mapping details.
    /// </summary>
    /// <returns>Configured split-view root.</returns>
    private VisualElement BuildContent()
    {
        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(LeftPaneWidth);
        VisualElement browser = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(browser);
        searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Waves presets by asset or designer-facing name.";
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(browser, searchField);
        browser.Add(searchField);

        Toolbar actions = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(actions);
        actions.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "New", "Create a new Waves preset.", CreatePreset));
        actions.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Duplicate", "Duplicate the selected Waves preset.", DuplicateSelectedPreset));
        actions.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Delete", "Stage the selected preset for deletion on Apply.", DeleteSelectedPreset));
        browser.Add(actions);

        presetListView = new ListView(filteredPresets,
                                      22f,
                                      GameWavesPanelUiUtility.MakePresetRow,
                                      BindPresetRow);
        presetListView.selectionType = SelectionType.Single;
        presetListView.selectionChanged += selection => SelectFirst(selection);
        GameManagementPanelLayoutUtility.ConfigureListView(presetListView);
        browser.Add(presetListView);

        detailsRoot = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(detailsRoot);
        splitView.Add(browser);
        splitView.Add(detailsRoot);
        return splitView;
    }

    /// <summary>
    /// Binds one browser row to a live Waves preset.
    /// </summary>
    /// <param name="element">Reusable row root.</param>
    /// <param name="index">Filtered preset index.</param>
    private void BindPresetRow(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null || index < 0 || index >= filteredPresets.Count)
            return;

        GameWavesPreset preset = filteredPresets[index];
        label.text = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        label.tooltip = AssetDatabase.GetAssetPath(preset);
    }

    /// <summary>
    /// Builds the scene brush, category, wave sequence and validation sub-tab toolbar.
    /// </summary>
    /// <returns>Configured details toolbar.</returns>
    private VisualElement BuildTabs()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);
        AddTabButton(toolbar, DetailsTab.SceneBrush, "Scene Brush");
        AddTabButton(toolbar, DetailsTab.BrushCategories, "Brush Categories");
        AddTabButton(toolbar, DetailsTab.WaveSequence, "Wave Sequence");
        AddTabButton(toolbar, DetailsTab.Validation, "Validation");
        toolbar.style.flexShrink = 0f;
        return toolbar;
    }

    /// <summary>
    /// Adds one stateful mapping details-tab button.
    /// </summary>
    /// <param name="toolbar">Toolbar receiving the button.</param>
    /// <param name="tab">Tab activated by the button.</param>
    /// <param name="label">Designer-facing label.</param>
    private void AddTabButton(Toolbar toolbar, DetailsTab tab, string label)
    {
        Button button = new Button(() =>
        {
            activeTab = tab;
            ManagementToolStateUtility.SaveEnumValue(ActiveTabStateKey, activeTab);
            BuildActiveTab();
        });
        button.text = label;
        button.tooltip = "Show Waves " + label + ".";
        toolbar.Add(button);
    }
    #endregion

    #region Preset Methods
    /// <summary>
    /// Refreshes filtered Waves assets without modifying current tuning.
    /// </summary>
    private void RefreshPresetList()
    {
        List<GameWavesPreset> allPresets =
            GameManagementStandalonePresetAssetUtility.FindAssets<GameWavesPreset>();
        string searchText = searchField == null ? string.Empty : searchField.value;
        filteredPresets.Clear();

        for (int presetIndex = 0; presetIndex < allPresets.Count; presetIndex++)
        {
            GameWavesPreset preset = allPresets[presetIndex];

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
    /// Creates and selects a new initialized Waves preset.
    /// </summary>
    private void CreatePreset()
    {
        GameWavesPreset preset =
            GameManagementStandalonePresetAssetUtility.CreateAsset<GameWavesPreset>(
                AssetFolder,
                "GameWavesPreset",
                createdPreset => createdPreset.EnsureInitialized());
        RefreshPresetList();
        SelectPreset(preset);
    }

    /// <summary>
    /// Duplicates and selects the current Waves preset.
    /// </summary>
    private void DuplicateSelectedPreset()
    {
        GameWavesPreset duplicate =
            GameManagementStandalonePresetAssetUtility.DuplicateAsset(selectedPreset,
                                                                      createdPreset => createdPreset.EnsureInitialized());

        if (duplicate == null)
            return;

        RefreshPresetList();
        SelectPreset(duplicate);
    }

    /// <summary>
    /// Stages the current Waves preset for deletion after confirmation.
    /// </summary>
    private void DeleteSelectedPreset()
    {
        if (selectedPreset == null ||
            !EditorUtility.DisplayDialog("Delete Waves Preset",
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
            SelectPreset(selectedObject as GameWavesPreset);
            return;
        }
    }

    /// <summary>
    /// Rebinds all details tabs to one Waves preset.
    /// </summary>
    /// <param name="preset">Preset to edit, or null to clear details.</param>
    private void SelectPreset(GameWavesPreset preset)
    {
        selectedPreset = preset;
        previewRenderer.Dispose();
        detailsRoot.Clear();

        if (preset == null)
        {
            serializedPreset = null;
            detailsRoot.Add(new Label("Select or create a Waves preset to edit."));
            return;
        }

        preset.EnsureInitialized();
        EditorPrefs.SetString(SelectedPresetStateKey, AssetDatabase.GetAssetPath(preset));
        serializedPreset = new SerializedObject(preset);
        VisualElement tabs = BuildTabs();
        tabContentRoot = new VisualElement();
        tabContentRoot.style.flexGrow = 1f;
        tabContentRoot.style.minHeight = 0f;
        tabContentRoot.style.minWidth = 0f;
        tabContentRoot.style.overflow = Overflow.Hidden;
        detailsRoot.Add(tabs);
        detailsRoot.Add(tabContentRoot);
        BuildActiveTab();
    }
    #endregion

    #region Details Methods
    /// <summary>
    /// Rebuilds only the selected Waves sub-tab.
    /// </summary>
    private void BuildActiveTab()
    {
        if (serializedPreset == null || tabContentRoot == null)
            return;

        serializedPreset.UpdateIfRequiredOrScript();
        tabContentRoot.Clear();

        switch (activeTab)
        {
            case DetailsTab.BrushCategories:
                GameWavesPanelContentUtility.BuildCategories(tabContentRoot, serializedPreset);
                break;
            case DetailsTab.WaveSequence:
                BuildWaveSequenceTab();
                break;
            case DetailsTab.Validation:
                GameWavesPanelContentUtility.BuildValidation(tabContentRoot, selectedPreset);
                break;
            default:
                BuildSceneBrushTab();
                break;
        }
    }

    /// <summary>
    /// Builds scene mapping, compact brush controls and the concrete embedded room preview.
    /// </summary>
    private void BuildSceneBrushTab()
    {
        SerializedProperty sceneMappings = serializedPreset.FindProperty("sceneMappings");
        ScrollView configurationRoot = new ScrollView();
        configurationRoot.style.flexShrink = 0f;
        configurationRoot.style.minHeight = 0f;
        configurationRoot.style.maxHeight = 340f;
        configurationRoot.style.minWidth = 0f;
        configurationRoot.contentContainer.style.flexShrink = 0f;
        tabContentRoot.Add(configurationRoot);
        Toolbar sceneToolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(sceneToolbar);
        PopupField<string> scenePopup = new PopupField<string>(
            "Scene",
            GameWavesPanelUiUtility.BuildSceneChoices(sceneMappings),
            GameWavesPanelUiUtility.ClampIndex(selectedSceneIndex, sceneMappings.arraySize));
        scenePopup.tooltip = "Managed room scene whose single SubScene and enemy spawner are edited in isolation.";
        scenePopup.style.flexGrow = 1f;
        scenePopup.style.flexShrink = 1f;
        scenePopup.style.minWidth = 220f;
        scenePopup.RegisterValueChangedCallback(evt =>
        {
            selectedSceneIndex = scenePopup.index;
            selectedWaveIndex = 0;
            selectedCellCoordinate = null;
            sceneWarning = string.Empty;
            BuildActiveTab();
        });
        sceneToolbar.Add(scenePopup);
        sceneToolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Add Scene", "Add a managed room-to-SubScene mapping.", AddSceneMapping));
        sceneToolbar.Add(GameWavesPanelUiUtility.CreateToolbarButton(
            "Remove", "Remove the selected scene mapping.", RemoveSceneMapping));
        configurationRoot.Add(sceneToolbar);

        if (sceneMappings.arraySize == 0)
        {
            configurationRoot.Add(new HelpBox("Add a scene mapping to begin painting enemy waves.",
                                              HelpBoxMessageType.Info));
            return;
        }

        selectedSceneIndex = GameWavesPanelUiUtility.ClampIndex(selectedSceneIndex,
                                                                              sceneMappings.arraySize);
        SerializedProperty mapping = sceneMappings.GetArrayElementAtIndex(selectedSceneIndex);
        Foldout mappingDetails = new Foldout
        {
            text = "Scene Mapping",
            value = false,
            tooltip = "Inspect or change the main room, its resolved SubScene and the wave asset painted by this mapping."
        };
        mappingDetails.style.flexShrink = 0f;
        GameWavesPanelUiUtility.AddBoundProperty(mappingDetails,
                                                          mapping.FindPropertyRelative("displayName"),
                                                          "Display Name");
        GameWavesPanelUiUtility.AddObjectReferenceField(
            mappingDetails,
            serializedPreset,
            mapping.FindPropertyRelative("mainSceneAsset"),
            "Main Room Scene",
            typeof(SceneAsset),
            SynchronizeSelectedScene);
        GameWavesPanelUiUtility.AddObjectReferenceField(
            mappingDetails,
            serializedPreset,
            mapping.FindPropertyRelative("wavePreset"),
            "Enemy Wave Preset",
            typeof(EnemyWavePreset),
            SynchronizeSelectedWavePreset);
        configurationRoot.Add(mappingDetails);

        serializedPreset.ApplyModifiedProperties();
        EnemyWavePreset wavePreset = mapping.FindPropertyRelative("wavePreset").objectReferenceValue as EnemyWavePreset;

        if (!string.IsNullOrWhiteSpace(sceneWarning))
            configurationRoot.Add(new HelpBox(sceneWarning, HelpBoxMessageType.Warning));

        if (wavePreset == null)
        {
            configurationRoot.Add(new HelpBox("The unique SubScene spawner must reference an Enemy Wave preset.",
                                              HelpBoxMessageType.Warning));
            return;
        }

        waveSerializedObject = new SerializedObject(wavePreset);
        GameWavesSceneBrushControlsUtility.BuildPaintingControls(
            configurationRoot,
            wavePreset,
            selectedPreset,
            selectedWaveIndex,
            selectedCategoryIndex,
            brushEnemyCount,
            eraseBrush,
            previewZoom,
            SelectBrushWave,
            categoryIndex => selectedCategoryIndex = categoryIndex,
            enemyCount => brushEnemyCount = enemyCount,
            erase => eraseBrush = erase,
            zoom =>
            {
                previewZoom = zoom;
                tabContentRoot.MarkDirtyRepaint();
            });
        GameWavesSceneBrushControlsUtility.BuildSelectedWaveSettings(configurationRoot,
                                                                     waveSerializedObject,
                                                                     selectedWaveIndex,
                                                                     AddWave);
        string mainScenePath = mapping.FindPropertyRelative("mainScenePath").stringValue;
        string subScenePath = mapping.FindPropertyRelative("subScenePath").stringValue;
        previewRenderer.Load(mainScenePath, subScenePath);
        GameWavesSpawnerSettingsDraft settingsDraft = null;

        if (GameWavesSpawnerSettingsDraftSession.TryGetOrCreate(subScenePath,
                                                                out settingsDraft,
                                                                out string settingsWarning))
        {
            previewRenderer.ApplySpawnerSettings(settingsDraft);
            GameWavesSpawnerSettingsEditorUtility.Build(
                configurationRoot,
                settingsDraft,
                () => RefreshGridAfterSettingsChange(settingsDraft));
        }
        else if (!string.IsNullOrWhiteSpace(settingsWarning))
        {
            configurationRoot.Add(new HelpBox(settingsWarning, HelpBoxMessageType.Warning));
        }

        IMGUIContainer preview = new IMGUIContainer(DrawPreview);
        preview.style.flexGrow = 1f;
        preview.style.flexShrink = 1f;
        preview.style.flexBasis = 0f;
        preview.style.minHeight = 280f;
        preview.style.minWidth = 0f;
        preview.style.overflow = Overflow.Hidden;
        preview.tooltip = "Concrete isolated room preview. Paint the selected wave directly on its ECS spawner grid.";
        tabContentRoot.Add(preview);
        ScrollView cellEditorRoot = new ScrollView();
        cellEditorRoot.style.flexShrink = 0f;
        cellEditorRoot.style.minHeight = 0f;
        cellEditorRoot.style.maxHeight = 260f;
        cellEditorRoot.style.minWidth = 0f;
        cellEditorRoot.contentContainer.style.flexShrink = 0f;
        tabContentRoot.Add(cellEditorRoot);
        GameWavesCellEditorUtility.Build(cellEditorRoot,
                                         waveSerializedObject,
                                         selectedWaveIndex,
                                         selectedCellCoordinate,
                                         selectedPreset,
                                         ClearSelectedCell,
                                         ScheduleActiveTabRebuild);
    }

    /// <summary>
    /// Applies one valid grid draft to the preview and removes cells invalidated across every mapped wave.
    /// </summary>
    /// <param name="settingsDraft">Transactional spawner grid currently edited in Scene Brush.</param>
    private void RefreshGridAfterSettingsChange(GameWavesSpawnerSettingsDraft settingsDraft)
    {
        if (settingsDraft == null)
            return;

        GameWavesGridResizeUtility.RemoveOutOfBoundsCells(waveSerializedObject,
                                                         settingsDraft.GridSizeX,
                                                         settingsDraft.GridSizeZ);
        previewRenderer.ApplySpawnerSettings(settingsDraft);

        if (selectedCellCoordinate.HasValue &&
            !EnemySpawnerWaveBakeUtility.IsCellInsideGrid(selectedCellCoordinate.Value,
                                                          settingsDraft.GridSizeX,
                                                          settingsDraft.GridSizeZ))
        {
            selectedCellCoordinate = null;
        }

        tabContentRoot.MarkDirtyRepaint();
    }

    /// <summary>
    /// Builds the explicit ordered step editor with any number of parallel waves inside each step.
    /// </summary>
    private void BuildWaveSequenceTab()
    {
        selectedSequenceWavePreset = GameWavesWavePresetBrowserUtility.Build(
            tabContentRoot,
            serializedPreset,
            selectedSceneIndex,
            selectedSequenceWavePreset,
            preset =>
            {
                selectedSequenceWavePreset = preset;
                BuildActiveTab();
            },
            BuildActiveTab);
    }

    #endregion

    #region Scene and Wave Mutation Methods
    /// <summary>
    /// Adds one empty scene mapping and selects it for synchronization.
    /// </summary>
    private void AddSceneMapping()
    {
        selectedSceneIndex = GameWavesSceneMappingMutationUtility.Add(serializedPreset, selectedPreset);
        selectedCellCoordinate = null;
        sceneWarning = string.Empty;
        BuildActiveTab();
    }

    /// <summary>
    /// Removes the selected scene mapping without deleting either scene or wave assets.
    /// </summary>
    private void RemoveSceneMapping()
    {
        if (!GameWavesSceneMappingMutationUtility.Remove(serializedPreset,
                                                         selectedPreset,
                                                         ref selectedSceneIndex))
        {
            return;
        }

        selectedCellCoordinate = null;
        sceneWarning = string.Empty;
        BuildActiveTab();
    }

    /// <summary>
    /// Resolves the selected main scene into its single SubScene, spawner and wave preset.
    /// </summary>
    private void SynchronizeSelectedScene()
    {
        sceneWarning = GameWavesSceneMappingMutationUtility.SynchronizeScene(serializedPreset,
                                                                            selectedPreset,
                                                                            selectedSceneIndex);
        selectedCellCoordinate = null;
        BuildActiveTab();
    }

    /// <summary>
    /// Links a manually selected wave asset to the active category source and rebuilds scene-bound controls.
    /// </summary>
    private void SynchronizeSelectedWavePreset()
    {
        GameWavesSceneMappingMutationUtility.SynchronizeWavePreset(serializedPreset,
                                                                   selectedPreset,
                                                                   selectedSceneIndex);
        selectedWaveIndex = 0;
        selectedCellCoordinate = null;
        BuildActiveTab();
    }

    /// <summary>
    /// Selects one flat wave index resolved from the separate step and parallel-wave controls.
    /// </summary>
    /// <param name="waveIndex">Flat serialized wave index selected for painting.</param>
    private void SelectBrushWave(int waveIndex)
    {
        selectedWaveIndex = waveIndex;
        selectedCellCoordinate = null;
        BuildActiveTab();
    }


    /// <summary>
    /// Creates one default wave inside the mapped Enemy Wave preset.
    /// </summary>
    private void AddWave()
    {
        if (waveSerializedObject == null)
            return;

        GameWavesSequenceEditorUtility.AddWaveToStep(waveSerializedObject, 0, BuildActiveTab);
    }

    /// <summary>
    /// Draws the embedded concrete room preview using current brush settings.
    /// </summary>
    private void DrawPreview()
    {
        GameWavesPanelContentUtility.DrawPreview(previewRenderer,
                                                 waveSerializedObject,
                                                 selectedWaveIndex,
                                                 selectedPreset,
                                                 selectedCategoryIndex,
                                                 brushEnemyCount,
                                                 eraseBrush,
                                                 previewZoom,
                                                 selectedCellCoordinate,
                                                 SelectPreviewCell);
    }

    /// <summary>
    /// Selects one painted preview cell and schedules a safe UI Toolkit rebuild after the current IMGUI event.
    /// </summary>
    /// <param name="coordinate">Grid coordinate selected in the embedded preview.</param>
    private void SelectPreviewCell(Vector2Int coordinate)
    {
        selectedCellCoordinate = coordinate;

        if (tabContentRoot != null)
            tabContentRoot.schedule.Execute(BuildActiveTab);
    }

    /// <summary>
    /// Clears the detailed cell selection after removal or an explicit close action.
    /// </summary>
    private void ClearSelectedCell()
    {
        selectedCellCoordinate = null;
    }

    /// <summary>
    /// Schedules conditional Scene Brush controls after the current UI event has completed.
    /// </summary>
    private void ScheduleActiveTabRebuild()
    {
        if (tabContentRoot != null)
            tabContentRoot.schedule.Execute(BuildActiveTab);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Waves detail tabs used to separate painting from reusable data and validation.
    /// </summary>
    private enum DetailsTab
    {
        SceneBrush = 0,
        BrushCategories = 1,
        WaveSequence = 2,
        Validation = 3
    }
    #endregion
}
