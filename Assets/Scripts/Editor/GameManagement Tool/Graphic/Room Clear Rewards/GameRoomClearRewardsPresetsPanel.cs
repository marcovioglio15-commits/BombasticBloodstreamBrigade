using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Orchestrates Room Clear Rewards preset browsing and focused authoring sections.
/// </summary>
public sealed class GameRoomClearRewardsPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    internal const string SelectedPresetStateKey =
        "NashCore.GameManagement.RoomClearRewards.SelectedPreset";
    private const string ActiveTabStateKey =
        "NashCore.GameManagement.RoomClearRewards.ActiveTab";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<GameRoomClearRewardsPreset> filteredPresets =
        new List<GameRoomClearRewardsPreset>();

    private GameRoomClearRewardsPresetLibrary library;
    private ListView presetListView;
    private ToolbarSearchField presetSearchField;
    private ScrollView detailsRoot;
    private VisualElement tabHost;
    private VisualElement contentRoot;
    private GameRoomClearRewardsPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private DetailsTab activeTab;
    #endregion

    #region Properties
    public VisualElement Root => root;
    public GameRoomClearRewardsPreset SelectedPreset => selectedPreset;
    internal SerializedObject PresetSerializedObject => presetSerializedObject;

    internal GameRoomClearRewardsPresetLibrary Library
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

    internal List<GameRoomClearRewardsPreset> FilteredPresets => filteredPresets;

    internal ListView PresetListView
    {
        get
        {
            return presetListView;
        }
        set
        {
            presetListView = value;
        }
    }

    internal ToolbarSearchField PresetSearchField
    {
        get
        {
            return presetSearchField;
        }
        set
        {
            presetSearchField = value;
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
    #endregion

    #region Constructors
    /// <summary>
    /// Builds the responsive preset browser and restores the last selection and details tab.
    /// </summary>
    public GameRoomClearRewardsPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;
        library = GameRoomClearRewardsPresetLibraryUtility.GetOrCreateLibrary();
        activeTab = ManagementToolStateUtility.LoadEnumValue(ActiveTabStateKey, DetailsTab.Metadata);
        root.Add(GameRoomClearRewardsPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth));
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds the library and current selection after Apply, Discard, Undo or Redo.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameRoomClearRewardsPreset previousSelection = selectedPreset;
        library = GameRoomClearRewardsPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previousSelection == null || !filteredPresets.Contains(previousSelection))
            return;

        if (selectedPreset == previousSelection &&
            presetSerializedObject != null &&
            presetSerializedObject.targetObject == previousSelection)
        {
            presetSerializedObject.UpdateIfRequiredOrScript();
            BuildActiveTab();
            return;
        }

        SelectPreset(previousSelection);
    }

    /// <summary>
    /// Selects a preset requested by the owning Game Master panel.
    /// </summary>
    /// <param name="preset">Preset that should become active.</param>
    public void SelectPresetFromExternal(GameRoomClearRewardsPreset preset)
    {
        if (preset == null)
            return;

        if (!filteredPresets.Contains(preset))
            RefreshPresetList();

        if (selectedPreset == preset &&
            presetSerializedObject != null &&
            presetSerializedObject.targetObject == preset)
        {
            presetSerializedObject.UpdateIfRequiredOrScript();
            return;
        }

        if (filteredPresets.Contains(preset))
            SelectPreset(preset);
    }
    #endregion

    #region Preset Browser Methods
    /// <summary>
    /// Refreshes the filtered preset browser and restores a live selection.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameRoomClearRewardsPresetsPanelPresetUtility.RefreshPresetList(this);
    }

    /// <summary>
    /// Creates and selects a Room Clear Rewards preset through the draft workflow.
    /// </summary>
    internal void CreatePreset()
    {
        GameRoomClearRewardsPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates one preset while regenerating every nested technical identity.
    /// </summary>
    /// <param name="preset">Source preset copied into a new registered asset.</param>
    internal void DuplicatePreset(GameRoomClearRewardsPreset preset)
    {
        GameRoomClearRewardsPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages one registered preset for deletion after confirmation.
    /// </summary>
    /// <param name="preset">Preset staged for deletion.</param>
    internal void DeletePreset(GameRoomClearRewardsPreset preset)
    {
        GameRoomClearRewardsPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Selection Methods
    /// <summary>
    /// Selects one preset and rebuilds its tab hosts without exposing technical identifiers.
    /// </summary>
    /// <param name="preset">Preset to edit, or null to clear the details area.</param>
    internal void SelectPreset(GameRoomClearRewardsPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();

        if (presetListView != null && preset != null)
        {
            int selectedIndex = filteredPresets.IndexOf(preset);

            if (selectedIndex >= 0)
                presetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        ManagementToolStateUtility.SaveAssetPath(SelectedPresetStateKey, preset);

        if (selectedPreset == null)
        {
            presetSerializedObject = null;
            detailsRoot.Add(new Label("Select or create a Room Clear Rewards preset to edit."));
            return;
        }

        selectedPreset.EnsureInitialized();
        presetSerializedObject = new SerializedObject(selectedPreset);
        tabHost = BuildTabs();
        contentRoot = new VisualElement();
        contentRoot.style.flexGrow = 1f;
        detailsRoot.Add(tabHost);
        detailsRoot.Add(contentRoot);
        BuildActiveTab();
    }

    /// <summary>
    /// Rebuilds only the selected tab and binds it to the current serialized preset.
    /// </summary>
    private void BuildActiveTab()
    {
        if (contentRoot == null || presetSerializedObject == null)
            return;

        presetSerializedObject.UpdateIfRequiredOrScript();
        contentRoot.Clear();
        GameRoomClearRewardsPresetsPanelSectionUtility.Build(contentRoot,
                                                             presetSerializedObject,
                                                             activeTab);
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds the complete Room Clear Rewards details-tab selector.
    /// </summary>
    /// <returns>Responsive selector row containing every authoring section.</returns>
    private VisualElement BuildTabs()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 6f;
        AddTabButton(row, "Metadata", DetailsTab.Metadata, 84f);
        AddTabButton(row, "Reward Modules", DetailsTab.Modules, 116f);
        AddTabButton(row, "Room Rewards", DetailsTab.Rewards, 104f);
        AddTabButton(row, "Presentation Mappings", DetailsTab.Presentation, 156f);
        AddTabButton(row, "Player Log", DetailsTab.PlayerLog, 82f);
        AddTabButton(row, "Portal Log", DetailsTab.PortalLog, 108f);
        return row;
    }

    /// <summary>
    /// Adds one persisted details-tab action using the shared responsive button rhythm.
    /// </summary>
    /// <param name="parent">Selector row receiving the button.</param>
    /// <param name="label">-facing tab label.</param>
    /// <param name="tab">Tab selected by the button.</param>
    /// <param name="minimumWidth">Minimum readable width for the button.</param>
    private void AddTabButton(VisualElement parent,
                              string label,
                              DetailsTab tab,
                              float minimumWidth)
    {
        Button button = new Button(() =>
        {
            activeTab = tab;
            ManagementToolStateUtility.SaveEnumValue(ActiveTabStateKey, activeTab);
            BuildActiveTab();
        });
        button.text = label;
        button.tooltip = "Show Room Clear Rewards " + label + " settings.";
        button.style.flexShrink = 0f;
        button.style.minWidth = minimumWidth;
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Resolves the readable browser label for one registered preset.
    /// </summary>
    /// <param name="preset">Preset displayed in the browser.</param>
    /// <returns>-facing preset name followed by its optional version.</returns>
    internal string GetPresetDisplayName(GameRoomClearRewardsPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string displayName = string.IsNullOrWhiteSpace(preset.PresetName)
            ? preset.name
            : preset.PresetName;

        if (string.IsNullOrWhiteSpace(preset.Version))
            return displayName;

        return displayName + " v. " + preset.Version;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Identifies the independently rebuilt Room Clear Rewards detail areas.
    /// </summary>
    internal enum DetailsTab
    {
        Metadata = 0,
        Modules = 1,
        Rewards = 2,
        Presentation = 3,
        PlayerLog = 4,
        PortalLog = 5
    }
    #endregion
}
