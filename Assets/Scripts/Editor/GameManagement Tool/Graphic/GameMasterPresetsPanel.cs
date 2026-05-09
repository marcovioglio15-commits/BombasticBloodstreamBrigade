using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Root orchestrator for game master preset management and game-wide sub preset panels.
/// /params None.
/// /returns None.
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
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameMasterPreset previousSelection = selectedPreset;
        library = GameMasterPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previousSelection != null && filteredPresets.Contains(previousSelection))
            SelectPreset(previousSelection);

        RefreshOpenSidePanels();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the split master preset area and the tabbed side-panel host.
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameMasterPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new game master preset.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void CreatePreset()
    {
        GameMasterPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates the provided game master preset.
    /// /params preset Source preset.
    /// /returns None.
    /// </summary>
    internal void DuplicatePreset(GameMasterPreset preset)
    {
        GameMasterPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages the provided game master preset for deletion.
    /// /params preset Preset to stage.
    /// /returns None.
    /// </summary>
    internal void DeletePreset(GameMasterPreset preset)
    {
        GameMasterPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one preset and rebuilds detail controls.
    /// /params preset Preset to select, or null to clear details.
    /// /returns None.
    /// </summary>
    internal void SelectPreset(GameMasterPreset preset)
    {
        GameMasterPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    /// <summary>
    /// Rebuilds the active master preset detail section.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void BuildActiveDetailsSection()
    {
        GameMasterPresetsPanelSectionsUtility.BuildActiveDetailsSection(this);
    }

    /// <summary>
    /// Assigns one sub-preset object to the selected master preset.
    /// /params propertyName Serialized property receiving the reference.
    /// /params preset Preset object to assign.
    /// /returns None.
    /// </summary>
    internal void AssignSubPreset(string propertyName, UnityEngine.Object preset)
    {
        GameMasterPresetsPanelSectionsUtility.AssignSubPreset(this, propertyName, preset);
    }
    #endregion

    #region Audio Manager
    /// <summary>
    /// Creates a new Audio Manager preset and assigns it to the selected master preset.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void CreateAudioManagerPreset()
    {
        GameMasterPresetsPanelSectionsUtility.CreateAudioManagerPreset(this);
    }

    /// <summary>
    /// Creates a new Scene Manager preset and assigns it to the selected master preset.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void CreateSceneManagerPreset()
    {
        GameMasterPresetsPanelSectionsUtility.CreateSceneManagerPreset(this);
    }

    /// <summary>
    /// Opens or activates one side panel.
    /// /params panelType Target panel type.
    /// /returns None.
    /// </summary>
    internal void OpenSidePanel(GameManagementWindow.PanelType panelType)
    {
        GameMasterPresetsPanelSidePanelUtility.OpenSidePanel(this, panelType);
    }
    #endregion

    #region Audio Authoring
    /// <summary>
    /// Finds a prefab containing GameAudioManagerAuthoring and selects it.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void FindAudioManagerPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.FindAudioManagerPrefab(this);
    }

    /// <summary>
    /// Finds a prefab containing GameSceneManagerAuthoring and selects it.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void FindSceneManagerPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.FindSceneManagerPrefab(this);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameAudioManagerAuthoring prefab.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void AssignPresetToAuthoringPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.AssignPresetToAuthoringPrefab(this);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameSceneManagerAuthoring prefab.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void AssignPresetToSceneAuthoringPrefab()
    {
        GameMasterPresetsPanelAuthoringUtility.AssignPresetToSceneAuthoringPrefab(this);
    }

    /// <summary>
    /// Refreshes the active authoring status label.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void RefreshActiveStatus()
    {
        GameMasterPresetsPanelAuthoringUtility.RefreshActiveStatus(this);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Refreshes open side panel controllers and synchronizes their selected presets.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void RefreshOpenSidePanels()
    {
        GameMasterPresetsPanelSidePanelUtility.RefreshOpenSidePanels(this);
    }

    /// <summary>
    /// Resolves display text for one game master preset.
    /// /params preset Preset to display.
    /// /returns Display text for list rows.
    /// </summary>
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
    /// /params None.
    /// /returns None.
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
    /// /params None.
    /// /returns None.
    /// </summary>
    internal sealed class SidePanelEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
        public GameAudioManagerPresetsPanel AudioPanel;
        public GameSceneManagerPresetsPanel ScenePanel;
    }
    #endregion
}
