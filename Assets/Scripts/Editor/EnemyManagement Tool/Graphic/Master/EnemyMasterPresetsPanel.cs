using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for managing enemy master presets and linked sub presets.
/// </summary>
public sealed class EnemyMasterPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActivePanelStateKey = "NashCore.EnemyManagement.Master.ActivePanel";
    private const string OpenPanelsStateKey = "NashCore.EnemyManagement.Master.OpenPanels";
    private const string ActiveDetailsSectionStateKey = "NashCore.EnemyManagement.Master.ActiveDetailsSection";
    private const string SelectedPrefabPathStateKey = "NashCore.EnemyManagement.Master.SelectedPrefabPath";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyMasterPreset> filteredPresets = new List<EnemyMasterPreset>();
    private readonly Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry> sidePanels = new Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry>();
    private readonly List<GameObject> availableEnemyPrefabs = new List<GameObject>();



    private EnemyMasterPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement detailSectionButtonsRoot;
    private VisualElement detailSectionContentRoot;
    private VisualElement mainContentRoot;
    private VisualElement tabBar;
    private VisualElement contentHost;
    private EnemyManagementWindow.PanelType activePanel = EnemyManagementWindow.PanelType.EnemyMasterPresets;
    private DetailsSectionType activeDetailsSection = DetailsSectionType.Metadata;


    private EnemyMasterPreset selectedPreset;
    private SerializedObject presetSerializedObject;


    private PopupField<GameObject> enemyPrefabPopup;
    private Label activeStatusLabel;
    private GameObject selectedEnemyPrefab;
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

    internal EnemyMasterPreset SelectedPreset
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

    internal EnemyMasterPresetLibrary Library
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

    internal List<EnemyMasterPreset> FilteredPresets
    {
        get
        {
            return filteredPresets;
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

    internal VisualElement DetailsRoot
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

    internal Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry> SidePanels
    {
        get
        {
            return sidePanels;
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

    internal VisualElement MainContentRoot
    {
        get
        {
            return mainContentRoot;
        }
    }

    internal EnemyManagementWindow.PanelType ActivePanel
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

    internal List<GameObject> AvailableEnemyPrefabs
    {
        get
        {
            return availableEnemyPrefabs;
        }
    }

    internal PopupField<GameObject> EnemyPrefabPopup
    {
        get
        {
            return enemyPrefabPopup;
        }
        set
        {
            enemyPrefabPopup = value;
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

    internal GameObject SelectedEnemyPrefab
    {
        get
        {
            return selectedEnemyPrefab;
        }
        set
        {
            selectedEnemyPrefab = value;
        }
    }
    #endregion

    #region Constructors
    public EnemyMasterPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;

        library = EnemyMasterPresetLibraryUtility.GetOrCreateLibrary();
        EnemyMasterPresetsPanelSidePanelUtility.RestorePersistedState(this);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes library-driven UI after external asset changes (Apply, Undo, Discard) and restores
    /// the previously selected preset when still valid. When the selection itself did not change and the
    /// SerializedObject still points to a live asset the detail subtree is kept intact and only its
    /// bindings are refreshed; this avoids tearing down PopupField dropdowns, ScrollView offsets and any
    /// other focused control that the user is currently interacting with.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        EnemyMasterPreset previouslySelectedPreset = selectedPreset;
        library = EnemyMasterPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previouslySelectedPreset != null)
        {
            int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

            // If the previously selected preset is still alive and reachable, keep the existing detail
            // subtree untouched and only refresh bindings/status — see PlayerMasterPresetsPanel sibling.
            if (presetIndex >= 0)
            {
                if (listView != null)
                    listView.SetSelectionWithoutNotify(new int[] { presetIndex });

                if (selectedPreset == previouslySelectedPreset && presetSerializedObject != null && presetSerializedObject.targetObject == previouslySelectedPreset)
                    SoftRefreshSelectedPresetBindings();
                else
                    SelectPreset(previouslySelectedPreset);
            }
        }

        RefreshOpenSidePanels();
    }

    /// <summary>
    /// Refreshes the bindings of the currently selected preset without destroying its UI subtree. Used
    /// by RefreshFromSessionChange to preserve scroll offsets and open popups during Apply.
    /// </summary>
    internal void SoftRefreshSelectedPresetBindings()
    {
        if (presetSerializedObject == null || presetSerializedObject.targetObject == null)
            return;

        presetSerializedObject.UpdateIfRequiredOrScript();
        RefreshActiveStatus();
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        mainContentRoot = BuildMainContent();
        BuildPanelsContainer();
    }


    private VisualElement BuildMainContent()
    {
        return EnemyMasterPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth);
    }



    private void BuildPanelsContainer()
    {
        EnemyMasterPresetsPanelSidePanelUtility.BuildPanelsContainer(this);
    }

    private VisualElement BuildLeftPane()
    {
        return EnemyMasterPresetsPanelPresetUtility.BuildLeftPane(this);
    }


    private VisualElement BuildRightPane()
    {
        return EnemyMasterPresetsPanelPresetUtility.BuildRightPane(this);
    }
    #endregion

    #region Preset List
    private VisualElement MakePresetItem()
    {
        return EnemyMasterPresetsPanelPresetUtility.MakePresetItem(this);
    }


    private void BindPresetItem(VisualElement element, int index)
    {
        EnemyMasterPresetsPanelPresetUtility.BindPresetItem(this, element, index);
    }



    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        EnemyMasterPresetsPanelPresetUtility.OnPresetSelectionChanged(this, selection);
    }


    internal void RefreshPresetList()
    {
        EnemyMasterPresetsPanelPresetUtility.RefreshPresetList(this);
    }


    #endregion

    #region Preset Actions
    private void CreatePreset()
    {
        EnemyMasterPresetsPanelPresetUtility.CreatePreset(this);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    internal void DuplicatePreset(EnemyMasterPreset preset)
    {
        EnemyMasterPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    internal void DeletePreset(EnemyMasterPreset preset)
    {
        EnemyMasterPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Preset Details
    internal void SelectPreset(EnemyMasterPreset preset)
    {
        EnemyMasterPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Master Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    internal void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(EnemyMasterPreset preset, string newName)
    {
        EnemyMasterPresetsPanelPresetUtility.RenamePreset(this, preset, newName);
    }

    internal void ShowRenamePopup(VisualElement anchor, EnemyMasterPreset preset)
    {
        EnemyMasterPresetsPanelPresetUtility.ShowRenamePopup(this, anchor, preset);
    }

    private VisualElement BuildDetailsSectionButtons()
    {
        return EnemyMasterPresetsPanelSectionsUtility.BuildDetailsSectionButtons(this);
    }

    private void AddDetailsSectionButton(VisualElement parent, DetailsSectionType sectionType, string buttonLabel)
    {
        EnemyMasterPresetsPanelSectionsUtility.AddDetailsSectionButton(this, parent, sectionType, buttonLabel);
    }

    /// <summary>
    /// Sets the currently active details subsection and persists it for future reopen.
    /// Called by details tab buttons.
    /// Takes in the target details section enum.
    /// </summary>
    /// <param name="sectionType">Details subsection to display.</param>
    private void SetActiveDetailsSection(DetailsSectionType sectionType)
    {
        EnemyMasterPresetsPanelSectionsUtility.SetActiveDetailsSection(this, sectionType);
    }

    internal void BuildActiveDetailsSection()
    {
        EnemyMasterPresetsPanelSectionsUtility.BuildActiveDetailsSection(this);
    }
    #endregion

    #region Sub Preset Creation
    internal void CreateBrainPreset()
    {
        EnemyMasterPresetsPanelSectionsUtility.CreateBrainPreset(this);
    }

    internal void CreateVisualPreset()
    {
        EnemyMasterPresetsPanelSectionsUtility.CreateVisualPreset(this);
    }

    internal void CreateUiVisualPreset()
    {
        EnemyMasterPresetsPanelSectionsUtility.CreateUiVisualPreset(this);
    }

    internal void CreateAdvancedPatternPreset()
    {
        EnemyMasterPresetsPanelSectionsUtility.CreateAdvancedPatternPreset(this);
    }

    internal void CreateBossPatternPreset()
    {
        EnemyMasterPresetsPanelSectionsUtility.CreateBossPatternPreset(this);
    }

    internal void AssignSubPreset(string propertyName, UnityEngine.Object preset)
    {
        EnemyMasterPresetsPanelSectionsUtility.AssignSubPreset(this, propertyName, preset);
    }
    #endregion

    #region Prefab Activation
    internal void RefreshAvailableEnemyPrefabs()
    {
        EnemyMasterPresetsPanelPrefabActivationUtility.RefreshAvailableEnemyPrefabs(this);
    }

    internal int ResolveSelectedEnemyPrefabIndex()
    {
        return EnemyMasterPresetsPanelPrefabActivationUtility.ResolveSelectedEnemyPrefabIndex(this);
    }

    internal static string ResolveEnemyPrefabDisplayName(GameObject prefabAsset)
    {
        if (prefabAsset == null)
            return "<None>";

        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

        if (string.IsNullOrWhiteSpace(prefabPath))
            return prefabAsset.name;

        return string.Format("{0} ({1})", prefabAsset.name, prefabPath);
    }

    internal static EnemyAuthoring ResolveEnemyAuthoringInPrefab(GameObject prefabAsset)
    {
        if (prefabAsset == null)
            return null;

        EnemyAuthoring directAuthoring = prefabAsset.GetComponent<EnemyAuthoring>();

        if (directAuthoring != null)
            return directAuthoring;

        return prefabAsset.GetComponentInChildren<EnemyAuthoring>(true);
    }

    internal void RefreshEnemyPrefabSelection()
    {
        EnemyMasterPresetsPanelPrefabActivationUtility.RefreshEnemyPrefabSelection(this);
    }

    internal void PingSelectedEnemyPrefab()
    {
        EnemyMasterPresetsPanelPrefabActivationUtility.PingSelectedEnemyPrefab(this);
    }

    internal void AssignPresetToPrefab()
    {
        EnemyMasterPresetsPanelPrefabActivationUtility.AssignPresetToPrefab(this);
    }

    internal void ReloadSelectedPrefabReference()
    {
        EnemyMasterPresetsPanelPrefabActivationUtility.ReloadSelectedPrefabReference(this);
    }

    internal void RefreshActiveStatus()
    {
        EnemyMasterPresetsPanelPrefabActivationUtility.RefreshActiveStatus(this);
    }
    #endregion

    #region Helpers
    private void RefreshOpenSidePanels()
    {
        EnemyMasterPresetsPanelSidePanelUtility.RefreshOpenSidePanels(this);
    }

    internal string GetPresetDisplayName(EnemyMasterPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }
    #endregion

    #endregion

    #region Nested Types
    internal enum DetailsSectionType
    {
        Metadata = 0,
        SubPresets = 1,
        ActivePreset = 2,
        Navigation = 3
    }

    internal sealed class SidePanelEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
        public EnemyBrainPresetsPanel BrainPanel;
        public EnemyVisualPresetsPanel VisualPanel;
        public EnemyUiVisualPresetsPanel UiVisualPanel;
        public EnemyAdvancedPatternPresetsPanel AdvancedPatternPanel;
        public EnemyBossPatternPresetsPanel BossPatternPanel;
    }
    #endregion
}
