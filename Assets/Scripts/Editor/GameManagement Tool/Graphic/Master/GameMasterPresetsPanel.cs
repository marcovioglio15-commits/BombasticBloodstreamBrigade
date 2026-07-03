using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Root orchestrator for game master preset management and game-wide sub preset panels.
/// </summary>
public sealed class GameMasterPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<GameMasterPreset> filteredPresets = new List<GameMasterPreset>();
    private readonly Dictionary<GameManagementWindow.PanelType, SidePanelEntry> sidePanels = new Dictionary<GameManagementWindow.PanelType, SidePanelEntry>();

    private GameMasterPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement detailSectionButtonsRoot;
    private VisualElement detailSectionContentRoot;
    private VisualElement mainContentRoot;
    private VisualElement tabBar;
    private VisualElement contentHost;
    private GameManagementWindow.PanelType activePanel = GameManagementWindow.PanelType.GameMasterPresets;
    private DetailsSectionType activeDetailsSection = DetailsSectionType.Metadata;
    private GameMasterPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private GameObject selectedAudioPrefab;
    private GameObject selectedScenePrefab;
    private ObjectField audioPrefabField;
    private ObjectField scenePrefabField;
    private Label activeStatusLabel;
    private Label sceneActiveStatusLabel;
    private bool suppressStateWrite;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    internal GameMasterPresetLibrary Library
    {
        get
        {
            return library;
        }
        set
        {
            library = value;
        }
    }

    internal List<GameMasterPreset> FilteredPresets
    {
        get
        {
            return filteredPresets;
        }
    }

    internal Dictionary<GameManagementWindow.PanelType, SidePanelEntry> SidePanels
    {
        get
        {
            return sidePanels;
        }
    }

    internal ListView PresetListView
    {
        get
        {
            return listView;
        }
        set
        {
            listView = value;
        }
    }

    internal ToolbarSearchField PresetSearchField
    {
        get
        {
            return searchField;
        }
        set
        {
            searchField = value;
        }
    }

    internal ScrollView DetailsRoot
    {
        get
        {
            return detailsRoot;
        }
        set
        {
            detailsRoot = value;
        }
    }

    internal VisualElement DetailSectionButtonsRoot
    {
        get
        {
            return detailSectionButtonsRoot;
        }
        set
        {
            detailSectionButtonsRoot = value;
        }
    }

    internal VisualElement DetailSectionContentRoot
    {
        get
        {
            return detailSectionContentRoot;
        }
        set
        {
            detailSectionContentRoot = value;
        }
    }

    internal VisualElement MainContentRoot
    {
        get
        {
            return mainContentRoot;
        }
    }

    internal VisualElement TabBar
    {
        get
        {
            return tabBar;
        }
        set
        {
            tabBar = value;
        }
    }

    internal VisualElement ContentHost
    {
        get
        {
            return contentHost;
        }
        set
        {
            contentHost = value;
        }
    }

    internal GameManagementWindow.PanelType ActivePanel
    {
        get
        {
            return activePanel;
        }
        set
        {
            activePanel = value;
        }
    }

    internal DetailsSectionType ActiveDetailsSection
    {
        get
        {
            return activeDetailsSection;
        }
        set
        {
            activeDetailsSection = value;
        }
    }

    internal GameMasterPreset SelectedPreset
    {
        get
        {
            return selectedPreset;
        }
        set
        {
            selectedPreset = value;
        }
    }

    internal SerializedObject PresetSerializedObject
    {
        get
        {
            return presetSerializedObject;
        }
        set
        {
            presetSerializedObject = value;
        }
    }

    internal GameObject SelectedAudioPrefab
    {
        get
        {
            return selectedAudioPrefab;
        }
        set
        {
            selectedAudioPrefab = value;
        }
    }

    internal GameObject SelectedScenePrefab
    {
        get
        {
            return selectedScenePrefab;
        }
        set
        {
            selectedScenePrefab = value;
        }
    }

    internal ObjectField AudioPrefabField
    {
        get
        {
            return audioPrefabField;
        }
        set
        {
            audioPrefabField = value;
        }
    }

    internal ObjectField ScenePrefabField
    {
        get
        {
            return scenePrefabField;
        }
        set
        {
            scenePrefabField = value;
        }
    }

    internal Label ActiveStatusLabel
    {
        get
        {
            return activeStatusLabel;
        }
        set
        {
            activeStatusLabel = value;
        }
    }

    internal Label SceneActiveStatusLabel
    {
        get
        {
            return sceneActiveStatusLabel;
        }
        set
        {
            sceneActiveStatusLabel = value;
        }
    }

    internal bool SuppressStateWrite
    {
        get
        {
            return suppressStateWrite;
        }
        set
        {
            suppressStateWrite = value;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes the game management root panel and restores persisted editor state.
    /// </summary>
    public GameMasterPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;

        library = GameMasterPresetLibraryUtility.GetOrCreateLibrary();
        GameMasterPresetsPanelSidePanelUtility.RestorePersistedState(this);
        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds this panel from current assets after apply, discard, undo or redo operations.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameMasterPreset previousSelection = selectedPreset;
        library = GameMasterPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        // Soft-refresh when the selection still points to the same live asset: this keeps the detail
        // subtree (and any open dropdowns/scroll offsets) intact across Apply/Discard/Undo flows.
        if (previousSelection != null && filteredPresets.Contains(previousSelection))
        {
            if (selectedPreset == previousSelection && presetSerializedObject != null && presetSerializedObject.targetObject == previousSelection)
                presetSerializedObject.UpdateIfRequiredOrScript();
            else
                SelectPreset(previousSelection);
        }

        RefreshOpenSidePanels();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the split master preset area and the tabbed side-panel host.
    /// </summary>
    private void BuildUI()
    {
        mainContentRoot = GameMasterPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth);
        GameMasterPresetsPanelSidePanelUtility.BuildPanelsContainer(this);
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes the game master preset list from the active library.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameMasterPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new game master preset.
    /// </summary>
    internal void CreatePreset()
    {
        GameMasterPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates the provided game master preset.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    internal void DuplicatePreset(GameMasterPreset preset)
    {
        GameMasterPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages the provided game master preset for deletion.
    /// </summary>
    /// <param name="preset">Preset to stage.</param>
    internal void DeletePreset(GameMasterPreset preset)
    {
        GameMasterPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one preset and rebuilds detail controls.
    /// </summary>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    internal void SelectPreset(GameMasterPreset preset)
    {
        GameMasterPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    /// <summary>
    /// Rebuilds the active master preset detail section.
    /// </summary>
    internal void BuildActiveDetailsSection()
    {
        GameMasterPresetsPanelSectionsUtility.BuildActiveDetailsSection(this);
    }

    /// <summary>
    /// Assigns one sub-preset object to the selected master preset.
    /// </summary>
    /// <param name="propertyName">Serialized property receiving the reference.</param>
    /// <param name="preset">Preset object to assign.</param>
    internal void AssignSubPreset(string propertyName, UnityEngine.Object preset)
    {
        GameMasterPresetsPanelSectionsUtility.AssignSubPreset(this, propertyName, preset);
    }
    #endregion

    #region Audio Manager
    /// <summary>
    /// Creates a new Audio Manager preset and assigns it to the selected master preset.
    /// </summary>
    internal void CreateAudioManagerPreset()
    {
        GameMasterPresetsPanelSectionsUtility.CreateAudioManagerPreset(this);
    }

    /// <summary>
    /// Creates a new Settings Manager preset and assigns it to the selected master preset.
    /// </summary>
    internal void CreateSettingsManagerPreset()
    {
        GameMasterPresetsPanelSectionsUtility.CreateSettingsManagerPreset(this);
    }

    /// <summary>
    /// Creates a new HUD Manager preset and assigns it to the selected master preset.
    /// </summary>
    internal void CreateHudManagerPreset()
    {
        GameMasterPresetsPanelSectionsUtility.CreateHudManagerPreset(this);
    }

    /// <summary>
    /// Creates a new Scene Manager preset and assigns it to the selected master preset.
    /// </summary>
    internal void CreateSceneManagerPreset()
    {
        GameMasterPresetsPanelSectionsUtility.CreateSceneManagerPreset(this);
    }

    /// <summary>
    /// Opens or activates one side panel.
    /// </summary>
    /// <param name="panelType">Target panel type.</param>
    internal void OpenSidePanel(GameManagementWindow.PanelType panelType)
    {
        GameMasterPresetsPanelSidePanelUtility.OpenSidePanel(this, panelType);
    }
    #endregion

    #region Audio Authoring
    /// <summary>
    /// Finds a prefab containing GameAudioManagerAuthoring and selects it.
    /// </summary>
    internal void FindAudioManagerPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.FindAudioManagerPrefab(this);
    }

    /// <summary>
    /// Finds a prefab containing GameSceneManagerAuthoring and selects it.
    /// </summary>
    internal void FindSceneManagerPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.FindSceneManagerPrefab(this);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameAudioManagerAuthoring prefab.
    /// </summary>
    internal void AssignPresetToAuthoringPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.AssignPresetToAuthoringPrefab(this);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameSceneManagerAuthoring prefab.
    /// </summary>
    internal void AssignPresetToSceneAuthoringPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.AssignPresetToSceneAuthoringPrefab(this);
    }

    /// <summary>
    /// Refreshes the active authoring status label.
    /// </summary>
    internal void RefreshActiveStatus()
    {
        GameMasterPresetsPanelAuthoringUtility.RefreshActiveStatus(this);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Refreshes open side panel controllers and synchronizes their selected presets.
    /// </summary>
    private void RefreshOpenSidePanels()
    {
        GameMasterPresetsPanelSidePanelUtility.RefreshOpenSidePanels(this);
    }

    /// <summary>
    /// Resolves display text for one game master preset.
    /// </summary>
    /// <param name="preset">Preset to display.</param>
    /// <returns>Display text for list rows.</returns>
    internal string GetPresetDisplayName(GameMasterPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;

        if (string.IsNullOrWhiteSpace(preset.Version))
            return presetName;

        return presetName + " v. " + preset.Version;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Detail sections available for the selected game master preset.
    /// </summary>
    internal enum DetailsSectionType
    {
        Metadata = 0,
        SubPresets = 1,
        ActiveAuthoring = 2,
        Navigation = 3
    }

    /// <summary>
    /// Stores one opened side-panel tab and optional typed panel controller.
    /// </summary>
    internal sealed class SidePanelEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
        public GameAudioManagerPresetsPanel AudioPanel;
        public GameSceneManagerPresetsPanel ScenePanel;
        public GameSettingsManagerPresetsPanel SettingsPanel;
        public GameHudManagerPresetsPanel HudPanel;
    }
    #endregion
}
