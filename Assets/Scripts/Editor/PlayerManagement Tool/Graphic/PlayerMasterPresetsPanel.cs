using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides the root orchestration for player master preset management and delegates UI logic to focused utilities.
/// </summary>
public sealed class PlayerMasterPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    internal const string ProgressionPresetsFolder = "Assets/Scriptable Objects/Player/Progression";
    internal const string PowerUpsPresetsFolder = "Assets/Scriptable Objects/Player/Power-Ups";
    internal const string VisualPresetsFolder = "Assets/Scriptable Objects/Player/Visual";
    internal const string AnimationPresetsFolder = "Assets/Scriptable Objects/Player/Animation Bindings";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerMasterPreset> filteredPresets = new List<PlayerMasterPreset>();
    private readonly Dictionary<PlayerManagementWindow.PanelType, SidePanelEntry> sidePanels = new Dictionary<PlayerManagementWindow.PanelType, SidePanelEntry>();

    private PlayerMasterPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement detailSectionButtonsRoot;
    private VisualElement detailSectionContentRoot;
    private VisualElement mainContentRoot;
    private VisualElement tabBar;
    private VisualElement contentHost;
    private PlayerManagementWindow.PanelType activePanel = PlayerManagementWindow.PanelType.PlayerMasterPresets;
    private DetailsSectionType activeDetailsSection = DetailsSectionType.Metadata;

    private PlayerMasterPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private ObjectField playerPrefabField;
    private Label activeStatusLabel;
    private GameObject selectedPlayerPrefab;
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

    internal PlayerMasterPresetLibrary Library
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

    internal List<PlayerMasterPreset> FilteredPresets
    {
        get
        {
            return filteredPresets;
        }
    }

    internal Dictionary<PlayerManagementWindow.PanelType, SidePanelEntry> SidePanels
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

    internal PlayerManagementWindow.PanelType ActivePanel
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

    internal PlayerMasterPreset SelectedPreset
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

    internal ObjectField PlayerPrefabField
    {
        get
        {
            return playerPrefabField;
        }
        set
        {
            playerPrefabField = value;
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

    internal GameObject SelectedPlayerPrefab
    {
        get
        {
            return selectedPlayerPrefab;
        }
        set
        {
            selectedPlayerPrefab = value;
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
    /// Initializes the panel, restores persisted state and builds the initial player master preset UI.
    /// </summary>
    /// <param name="None">No parameters.</param>

    public PlayerMasterPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;

        library = PlayerMasterPresetLibraryUtility.GetOrCreateLibrary();
        PlayerMasterPresetsPanelSidePanelUtility.RestorePersistedState(this);

        // Seed the cross-panel selection context BEFORE side panels are restored: their Add Scaling
        // sections validate formulas against the master-scoped scalable-stat set at build time, and
        // a still-null context would flag every referenced variable as an unknown scalable stat
        // until the section is manually rebuilt.
        PlayerManagementSelectionContext.SetActiveMasterPreset(PlayerMasterPresetsPanelSidePanelUtility.LoadPersistedSelectedPreset());

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
    /// bindings are refreshed; this avoids tearing down ObjectField popups, ScrollView offsets and any
    /// other focused control that the user is currently interacting with.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        PlayerMasterPreset previouslySelectedPreset = selectedPreset;
        library = PlayerMasterPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previouslySelectedPreset != null)
        {
            int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

            // If the previously selected preset is still alive and reachable, keep the rebuilt detail
            // subtree (which RefreshPresetList may have left in place) untouched and just refresh bindings.
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
    /// by RefreshFromSessionChange to preserve scroll offsets and open dropdown popups during Apply.
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
        return PlayerMasterPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth);
    }

    private void BuildPanelsContainer()
    {
        PlayerMasterPresetsPanelSidePanelUtility.BuildPanelsContainer(this);
    }
    #endregion

    #region Preset List
    internal void RefreshPresetList()
    {
        PlayerMasterPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    internal void DuplicatePreset(PlayerMasterPreset preset)
    {
        PlayerMasterPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    internal void DeletePreset(PlayerMasterPreset preset)
    {
        PlayerMasterPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Preset Details
    internal void SelectPreset(PlayerMasterPreset preset)
    {
        PlayerMasterPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("m_PresetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    internal void HandlePresetNameChanged(string newName)
    {
        PlayerMasterPresetsPanelPresetUtility.RenamePreset(this, selectedPreset, newName);
    }

    internal void ShowRenamePopup(VisualElement anchor, PlayerMasterPreset preset)
    {
        PlayerMasterPresetsPanelPresetUtility.ShowRenamePopup(this, anchor, preset);
    }

    internal void BuildActiveDetailsSection()
    {
        PlayerMasterPresetsPanelSectionsUtility.BuildActiveDetailsSection(this);
    }
    #endregion

    #region Sub Preset Creation
    internal void CreateControllerPreset()
    {
        PlayerMasterPresetsPanelSectionsUtility.CreateControllerPreset(this);
    }

    internal void CreateProgressionPreset()
    {
        PlayerMasterPresetsPanelSectionsUtility.CreateProgressionPreset(this);
    }

    internal void CreatePowerUpsPreset()
    {
        PlayerMasterPresetsPanelSectionsUtility.CreatePowerUpsPreset(this);
    }

    internal void CreateAnimationPreset()
    {
        PlayerMasterPresetsPanelSectionsUtility.CreateAnimationPreset(this);
    }

    internal void CreateVisualPreset()
    {
        PlayerMasterPresetsPanelSectionsUtility.CreateVisualPreset(this);
    }

    internal void AssignSubPreset(string propertyName, UnityEngine.Object preset)
    {
        PlayerMasterPresetsPanelSectionsUtility.AssignSubPreset(this, propertyName, preset);
    }
    #endregion

    #region Prefab Activation
    internal void FindPlayerPrefab()
    {
        PlayerMasterPresetsPanelPrefabActivationUtility.FindPlayerPrefab(this);
    }

    internal void AssignPresetToPrefab()
    {
        PlayerMasterPresetsPanelPrefabActivationUtility.AssignPresetToPrefab(this);
    }

    internal void RefreshActiveStatus()
    {
        PlayerMasterPresetsPanelPrefabActivationUtility.RefreshActiveStatus(this);
    }
    #endregion

    #region Helpers
    private void RefreshOpenSidePanels()
    {
        PlayerMasterPresetsPanelSidePanelUtility.RefreshOpenSidePanels(this);
    }

    internal string GetPresetDisplayName(PlayerMasterPreset preset)
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
        Navigation = 3,
        Layers = 4
    }

    internal sealed class SidePanelEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
        public PlayerControllerPresetsPanel ControllerPanel;
        public PlayerProgressionPresetsPanel ProgressionPanel;
        public PlayerPowerUpsPresetsPanel PowerUpsPanel;
        public PlayerVisualPresetsPanel VisualPanel;
        public PlayerAnimationBindingsPresetsPanel AnimationPanel;
    }
    #endregion
}
