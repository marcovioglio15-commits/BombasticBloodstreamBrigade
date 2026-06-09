using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Root orchestration panel for FMOD-backed Audio Manager presets.
/// </summary>
public sealed class GameAudioManagerPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    internal const string SelectedPresetPathStateKey = "NashCore.GameManagement.AudioManager.SelectedPreset";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<GameAudioManagerPreset> filteredPresets = new List<GameAudioManagerPreset>();
    private readonly List<string> validationWarnings = new List<string>();

    private GameAudioManagerPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;
    private DetailsSectionType activeSection = DetailsSectionType.Metadata;
    private GameAudioManagerPreset selectedPreset;
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

    internal GameAudioManagerPresetLibrary Library
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

    internal List<GameAudioManagerPreset> FilteredPresets
    {
        get
        {
            return filteredPresets;
        }
    }

    internal List<string> ValidationWarnings
    {
        get
        {
            return validationWarnings;
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

    internal VisualElement SectionButtonsRoot
    {
        get
        {
            return sectionButtonsRoot;
        }
        set
        {
            sectionButtonsRoot = value;
        }
    }

    internal VisualElement SectionContentRoot
    {
        get
        {
            return sectionContentRoot;
        }
        set
        {
            sectionContentRoot = value;
        }
    }

    internal DetailsSectionType ActiveSection
    {
        get
        {
            return activeSection;
        }
        set
        {
            activeSection = value;
        }
    }

    internal GameAudioManagerPreset SelectedPreset
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
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes the Audio Manager panel and restores its active details section.
    /// </summary>
    public GameAudioManagerPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;
        library = GameAudioManagerPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = GameAudioManagerPresetsPanelSectionsUtility.LoadActiveSection();
        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds the panel from current Audio Manager assets after draft session changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameAudioManagerPreset previousSelection = selectedPreset;
        library = GameAudioManagerPresetLibraryUtility.GetOrCreateLibrary();
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
    }

    /// <summary>
    /// Selects a preset assigned by the parent Game Master panel.
    /// </summary>
    /// <param name="preset">Audio Manager preset to select.</param>
    public void SelectPresetFromExternal(GameAudioManagerPreset preset)
    {
        if (preset == null)
            return;

        if (!filteredPresets.Contains(preset))
            RefreshPresetList();

        // External sync: skip detail rebuild when the preset is already active so dropdowns/scroll stay.
        if (selectedPreset == preset && presetSerializedObject != null && presetSerializedObject.targetObject == preset)
        {
            presetSerializedObject.UpdateIfRequiredOrScript();
            return;
        }

        SelectPreset(preset);
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the split preset browser and details panel.
    /// </summary>
    private void BuildUI()
    {
        root.Add(GameAudioManagerPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth));
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes visible Audio Manager presets from the current library and search filter.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameAudioManagerPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new Audio Manager preset.
    /// </summary>
    internal void CreatePreset()
    {
        GameAudioManagerPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates one Audio Manager preset asset and registers it.
    /// </summary>
    /// <param name="preset">Source preset to duplicate.</param>
    internal void DuplicatePreset(GameAudioManagerPreset preset)
    {
        GameAudioManagerPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages one Audio Manager preset for deletion after confirmation.
    /// </summary>
    /// <param name="preset">Preset to delete.</param>
    internal void DeletePreset(GameAudioManagerPreset preset)
    {
        GameAudioManagerPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one Audio Manager preset and rebuilds details.
    /// </summary>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    internal void SelectPreset(GameAudioManagerPreset preset)
    {
        GameAudioManagerPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    /// <summary>
    /// Rebuilds the active Audio Manager details section.
    /// </summary>
    internal void BuildActiveSection()
    {
        GameAudioManagerPresetsPanelSectionsUtility.BuildActiveSection(this);
    }

    /// <summary>
    /// Marks the selected Audio Manager preset dirty in the draft session.
    /// </summary>
    internal void MarkSelectedPresetDirty()
    {
        GameAudioManagerPresetsPanelSectionsUtility.MarkSelectedPresetDirty(this);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves display text for one Audio Manager preset.
    /// </summary>
    /// <param name="preset">Preset to display.</param>
    /// <returns>Display text for list rows.</returns>
    internal string GetPresetDisplayName(GameAudioManagerPreset preset)
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
    /// Detail sections shown for an Audio Manager preset.
    /// </summary>
    internal enum DetailsSectionType
    {
        Metadata = 0,
        Playback = 1,
        Routing = 2,
        BackgroundMusic = 3,
        EventMap = 4,
        RateLimits = 5,
        Validation = 6
    }
    #endregion
}
