using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Root orchestration panel for Scene Manager presets.
/// /params None.
/// /returns None.
/// </summary>
public sealed class GameSceneManagerPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<GameSceneManagerPreset> filteredPresets = new List<GameSceneManagerPreset>();
    private readonly List<string> validationWarnings = new List<string>();

    private GameSceneManagerPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;
    private DetailsSectionType activeSection = DetailsSectionType.Metadata;
    private GameSceneManagerPreset selectedPreset;
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

    internal GameSceneManagerPresetLibrary Library
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

    internal List<GameSceneManagerPreset> FilteredPresets
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

    internal GameSceneManagerPreset SelectedPreset
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
    /// Initializes the Scene Manager panel and restores its active details section.
    /// /params None.
    /// /returns None.
    /// </summary>
    public GameSceneManagerPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;
        library = GameSceneManagerPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = GameSceneManagerPresetsPanelSectionsUtility.LoadActiveSection();
        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds the panel from current Scene Manager assets after draft session changes.
    /// /params None.
    /// /returns None.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameSceneManagerPreset previousSelection = selectedPreset;
        library = GameSceneManagerPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previousSelection != null && filteredPresets.Contains(previousSelection))
            SelectPreset(previousSelection);
    }

    /// <summary>
    /// Selects a preset assigned by the parent Game Master panel.
    /// /params preset Scene Manager preset to select.
    /// /returns None.
    /// </summary>
    public void SelectPresetFromExternal(GameSceneManagerPreset preset)
    {
        if (preset == null)
            return;

        if (!filteredPresets.Contains(preset))
            RefreshPresetList();

        SelectPreset(preset);
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the split preset browser and details panel.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void BuildUI()
    {
        root.Add(GameSceneManagerPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth));
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes visible Scene Manager presets from the current library and search filter.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameSceneManagerPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects a new Scene Manager preset.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void CreatePreset()
    {
        GameSceneManagerPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates one Scene Manager preset asset and registers it.
    /// /params preset Source preset to duplicate.
    /// /returns None.
    /// </summary>
    internal void DuplicatePreset(GameSceneManagerPreset preset)
    {
        GameSceneManagerPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages one Scene Manager preset for deletion after confirmation.
    /// /params preset Preset to delete.
    /// /returns None.
    /// </summary>
    internal void DeletePreset(GameSceneManagerPreset preset)
    {
        GameSceneManagerPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one Scene Manager preset and rebuilds details.
    /// /params preset Preset to select, or null to clear details.
    /// /returns None.
    /// </summary>
    internal void SelectPreset(GameSceneManagerPreset preset)
    {
        GameSceneManagerPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    /// <summary>
    /// Rebuilds the active Scene Manager details section.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void BuildActiveSection()
    {
        GameSceneManagerPresetsPanelSectionsUtility.BuildActiveSection(this);
    }

    /// <summary>
    /// Marks the selected Scene Manager preset dirty in the draft session.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal void MarkSelectedPresetDirty()
    {
        GameSceneManagerPresetsPanelSectionsUtility.MarkSelectedPresetDirty(this);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves display text for one Scene Manager preset.
    /// /params preset Preset to display.
    /// /returns Display text for list rows.
    /// </summary>
    internal string GetPresetDisplayName(GameSceneManagerPreset preset)
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
    /// Detail sections shown for a Scene Manager preset.
    /// /params None.
    /// /returns None.
    /// </summary>
    internal enum DetailsSectionType
    {
        Metadata = 0,
        Startup = 1,
        SceneTable = 2,
        Transitions = 3,
        Fade = 4,
        Triggers = 5,
        Validation = 6,
        Addressables = 7,
        LoadingProgress = 8
    }
    #endregion
}
