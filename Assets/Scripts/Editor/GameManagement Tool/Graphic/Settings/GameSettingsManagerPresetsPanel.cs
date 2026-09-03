using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Root orchestration panel for Settings Manager presets.
/// </summary>
public sealed class GameSettingsManagerPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    internal const string SelectedPresetPathStateKey = "NashCore.GameManagement.SettingsManager.SelectedPreset";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<GameSettingsManagerPreset> filteredPresets = new List<GameSettingsManagerPreset>();
    private readonly List<string> validationWarnings = new List<string>();

    private GameSettingsManagerPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;
    private DetailsSectionType activeSection = DetailsSectionType.Metadata;
    private GameSettingsManagerPreset selectedPreset;
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

    internal GameSettingsManagerPresetLibrary Library
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

    internal List<GameSettingsManagerPreset> FilteredPresets
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

    internal GameSettingsManagerPreset SelectedPreset
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
    /// Initializes the Settings Manager panel and restores its active details section.
    /// </summary>
    public GameSettingsManagerPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;
        library = GameSettingsManagerPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = GameSettingsManagerPresetsPanelSectionsUtility.LoadActiveSection();
        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds the panel from current Settings Manager assets after draft session changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameSettingsManagerPreset previousSelection = selectedPreset;
        library = GameSettingsManagerPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        // Preserve open details when the same asset survives apply, discard or undo.
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
    /// <param name="preset">Settings Manager preset to select.</param>
    public void SelectPresetFromExternal(GameSettingsManagerPreset preset)
    {
        if (preset == null)
            return;

        if (!filteredPresets.Contains(preset))
            RefreshPresetList();

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
        root.Add(GameSettingsManagerPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth));
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes visible Settings Manager presets from the current library and search filter.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameSettingsManagerPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new Settings Manager preset.
    /// </summary>
    internal void CreatePreset()
    {
        GameSettingsManagerPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates one Settings Manager preset asset and registers it.
    /// </summary>
    /// <param name="preset">Source preset to duplicate.</param>
    internal void DuplicatePreset(GameSettingsManagerPreset preset)
    {
        GameSettingsManagerPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages one Settings Manager preset for deletion after confirmation.
    /// </summary>
    /// <param name="preset">Preset to delete.</param>
    internal void DeletePreset(GameSettingsManagerPreset preset)
    {
        GameSettingsManagerPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one Settings Manager preset and rebuilds details.
    /// </summary>
    /// <param name="preset">Preset to select, or null to clear details.</param>
    internal void SelectPreset(GameSettingsManagerPreset preset)
    {
        GameSettingsManagerPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    /// <summary>
    /// Rebuilds the active Settings Manager details section.
    /// </summary>
    internal void BuildActiveSection()
    {
        GameSettingsManagerPresetsPanelSectionsUtility.BuildActiveSection(this);
    }

    /// <summary>
    /// Marks the selected Settings Manager preset dirty in the draft session.
    /// </summary>
    internal void MarkSelectedPresetDirty()
    {
        GameSettingsManagerPresetsPanelSectionsUtility.MarkSelectedPresetDirty(this);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves display text for one Settings Manager preset.
    /// </summary>
    /// <param name="preset">Preset to display.</param>
    /// <returns>Display text for list rows.</returns>
    internal string GetPresetDisplayName(GameSettingsManagerPreset preset)
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
    /// Detail sections shown for a Settings Manager preset.
    /// </summary>
    internal enum DetailsSectionType
    {
        Metadata = 0,
        Audio = 1,
        Gameplay = 2,
        Validation = 3,
        ControllerNavigation = 4,
        DataCollection = 5
    }
    #endregion
}
