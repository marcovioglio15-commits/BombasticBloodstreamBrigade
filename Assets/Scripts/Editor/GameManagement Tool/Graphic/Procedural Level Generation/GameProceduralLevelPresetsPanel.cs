using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Orchestrates Procedural Level preset browsing, editing and stable nested-level selection.
/// </summary>
public sealed class GameProceduralLevelPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    internal const string SelectedPresetPathStateKey = "NashCore.GameManagement.ProceduralLevel.SelectedPreset";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly GameMasterPresetsPanel masterPanel;
    private readonly List<GameProceduralLevelPreset> filteredPresets = new List<GameProceduralLevelPreset>();

    private GameProceduralLevelPresetLibrary library;
    private ListView presetListView;
    private ToolbarSearchField presetSearchField;
    private ScrollView detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;
    private GameProceduralLevelPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private DetailsSectionType activeSection = DetailsSectionType.Metadata;
    private string selectedLevelTechnicalId;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    internal GameProceduralLevelPresetLibrary Library
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

    internal List<GameProceduralLevelPreset> FilteredPresets
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

    internal GameProceduralLevelPreset SelectedPreset
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

    internal string SelectedLevelTechnicalId
    {
        get
        {
            return selectedLevelTechnicalId;
        }
        set
        {
            selectedLevelTechnicalId = value;
        }
    }

    internal GameSceneManagerPreset RuntimeSceneCatalogPreset
    {
        get
        {
            return masterPanel != null && masterPanel.SelectedPreset != null
                ? masterPanel.SelectedPreset.SceneManagerPreset
                : null;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Builds the Procedural Level panel and restores the last edited preset and detail section.
    /// </summary>
    /// <param name="masterPanel">Owning master panel supplying the exact runtime Scene Manager catalog.</param>
    public GameProceduralLevelPresetsPanel(GameMasterPresetsPanel masterPanel)
    {
        this.masterPanel = masterPanel;
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;
        library = GameProceduralLevelPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = GameProceduralLevelPresetsPanelStateUtility.LoadActiveSection();
        root.Add(GameProceduralLevelPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth));
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebinds library, preset and nested level state after Apply, Discard, Undo or Redo.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        GameProceduralLevelPreset previousSelection = selectedPreset;
        library = GameProceduralLevelPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previousSelection == null || !filteredPresets.Contains(previousSelection))
            return;

        if (selectedPreset == previousSelection && presetSerializedObject != null && presetSerializedObject.targetObject == previousSelection)
        {
            presetSerializedObject.UpdateIfRequiredOrScript();
            BuildActiveSection();
            return;
        }

        SelectPreset(previousSelection);
    }

    /// <summary>
    /// Selects a Procedural Level preset requested by a parent panel without changing its asset data.
    /// </summary>
    /// <param name="preset">Preset that should become active in this browser.</param>
    public void SelectPresetFromExternal(GameProceduralLevelPreset preset)
    {
        if (preset == null)
            return;

        if (!filteredPresets.Contains(preset))
            RefreshPresetList();

        SelectPreset(preset);
    }
    #endregion

    #region Preset Browser
    /// <summary>
    /// Refreshes the visible preset collection and restores a valid browser selection.
    /// </summary>
    internal void RefreshPresetList()
    {
        GameProceduralLevelPresetsPanelPresetUtility.RefreshPresetList(this);
    }

    /// <summary>
    /// Creates a new Procedural Level preset through the shared draft workflow.
    /// </summary>
    internal void CreatePreset()
    {
        GameProceduralLevelPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates a preset while regenerating every nested technical identifier.
    /// </summary>
    /// <param name="preset">Source preset copied into a new asset.</param>
    internal void DuplicatePreset(GameProceduralLevelPreset preset)
    {
        GameProceduralLevelPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Removes a preset from the library and stages its asset deletion for Apply.
    /// </summary>
    /// <param name="preset">Preset staged for deletion.</param>
    internal void DeletePreset(GameProceduralLevelPreset preset)
    {
        GameProceduralLevelPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Details
    /// <summary>
    /// Selects one preset and rebuilds its detail controls.
    /// </summary>
    /// <param name="preset">Preset to edit, or null to clear the details area.</param>
    internal void SelectPreset(GameProceduralLevelPreset preset)
    {
        GameProceduralLevelPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    /// <summary>
    /// Rebuilds the currently selected detail section from serialized state.
    /// </summary>
    internal void BuildActiveSection()
    {
        GameProceduralLevelPresetsPanelSectionsUtility.BuildActiveSection(this);
    }

    /// <summary>
    /// Resolves whether the edited preset uses the exact Scene Manager catalog that the selected Game Master will bake.
    /// </summary>
    /// <returns>True only when both catalog references exist and identify the same preset asset.</returns>
    internal bool HasCompatibleRuntimeSceneCatalog()
    {
        return selectedPreset != null &&
               RuntimeSceneCatalogPreset != null &&
               selectedPreset.SceneCatalogPreset == RuntimeSceneCatalogPreset;
    }

    /// <summary>
    /// Resolves the readable browser label for one Procedural Level preset.
    /// </summary>
    /// <param name="preset">Preset displayed in the browser.</param>
    /// <returns>Preset name followed by its optional semantic version.</returns>
    internal string GetPresetDisplayName(GameProceduralLevelPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string displayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;

        if (string.IsNullOrWhiteSpace(preset.Version))
            return displayName;

        return displayName + " v. " + preset.Version;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Identifies the independently rebuilt detail areas of a Procedural Level preset.
    /// </summary>
    internal enum DetailsSectionType
    {
        Metadata = 0,
        Generation = 1,
        Transition = 2,
        Levels = 3
    }
    #endregion
}
