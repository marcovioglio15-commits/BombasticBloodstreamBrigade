using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Provides the root orchestration for player controller preset management and delegates UI construction to focused utilities.
/// </summary>
public sealed class PlayerControllerPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    internal const string ActiveSectionStateKey = "NashCore.PlayerManagement.Controller.ActiveSection";
    internal const string DetailsScrollOffsetStateKey = "NashCore.PlayerManagement.Controller.DetailsScroll";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerControllerPreset> filteredPresets = new List<PlayerControllerPreset>();

    private PlayerControllerPresetLibrary library;
    private InputActionAsset inputAsset;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;
    private SectionType activeSection = SectionType.Metadata;
    private PlayerControllerPreset selectedPreset;
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

    internal PlayerControllerPresetLibrary Library
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

    internal InputActionAsset InputAsset
    {
        get
        {
            return inputAsset;
        }
        set
        {
            inputAsset = value;
        }
    }

    internal List<PlayerControllerPreset> FilteredPresets
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

    internal SectionType ActiveSection
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

    internal PlayerControllerPreset SelectedPreset
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
    /// Initializes the panel, loads preset and input assets, restores the last active section and builds the UI.
    /// </summary>
    /// <param name="None">No parameters.</param>

    public PlayerControllerPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = PlayerControllerPresetLibraryUtility.GetOrCreateLibrary();
        inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Selects one preset from an external panel and aligns the internal list selection when the preset is available.
    /// </summary>
    /// <param name="preset">Preset to select from outside this panel.</param>

    public void SelectPresetFromExternal(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        RefreshPresetList();

        int index = filteredPresets.IndexOf(preset);

        if (index < 0)
            return;

        // External sync: when the preset is already the current one keep the detail subtree intact and
        // only fix listView selection. Otherwise route through SelectPreset (which builds detail UI).
        if (selectedPreset == preset && presetSerializedObject != null && presetSerializedObject.targetObject == preset)
        {
            if (listView != null && listView.selectedIndex != index)
                listView.SetSelectionWithoutNotify(new int[] { index });

            presetSerializedObject.UpdateIfRequiredOrScript();
            return;
        }

        if (listView == null)
        {
            SelectPreset(preset);
            return;
        }

        if (listView.selectedIndex != index)
        {
            listView.SetSelection(index);
            return;
        }

        SelectPreset(preset);
    }

    /// <summary>
    /// Refreshes the panel after external asset changes and restores the previous selection when still valid.
    /// </summary>
    /// <param name="None">No parameters.</param>

    public void RefreshFromSessionChange()
    {
        PlayerControllerPreset previouslySelectedPreset = selectedPreset;
        library = PlayerControllerPresetLibraryUtility.GetOrCreateLibrary();
        inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        RefreshPresetList();

        if (previouslySelectedPreset == null)
            return;

        int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
            return;

        if (listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        // Soft-refresh when the selection still points to the same live asset: this keeps the detail
        // subtree (and any open dropdowns/scroll offsets) intact across Apply/Discard/Undo flows.
        if (selectedPreset == previouslySelectedPreset && presetSerializedObject != null && presetSerializedObject.targetObject == previouslySelectedPreset)
            presetSerializedObject.UpdateIfRequiredOrScript();
        else
            SelectPreset(previouslySelectedPreset);
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        root.Add(PlayerControllerPresetsPanelPresetUtility.BuildMainContent(this, LeftPaneWidth));
    }
    #endregion

    #region Preset List
    internal void RefreshPresetList()
    {
        PlayerControllerPresetsPanelPresetUtility.RefreshPresetList(this);
    }
    #endregion

    #region Preset Actions
    internal void DuplicatePreset(PlayerControllerPreset preset)
    {
        PlayerControllerPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    internal void DeletePreset(PlayerControllerPreset preset)
    {
        PlayerControllerPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Preset Details
    internal void SelectPreset(PlayerControllerPreset preset)
    {
        PlayerControllerPresetsPanelSectionsUtility.SelectPreset(this, preset);
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
    }

    internal void HandlePresetNameChanged(string newName)
    {
        PlayerControllerPresetsPanelPresetUtility.RenamePreset(this, selectedPreset, newName);
    }

    internal void ShowRenamePopup(VisualElement anchor, PlayerControllerPreset preset)
    {
        PlayerControllerPresetsPanelPresetUtility.ShowRenamePopup(this, anchor, preset);
    }

    internal void SetActiveSection(SectionType sectionType)
    {
        PlayerControllerPresetsPanelSectionsUtility.SetActiveSection(this, sectionType);
    }

    internal void BuildActiveSection()
    {
        PlayerControllerPresetsPanelSectionsUtility.BuildActiveSection(this);
    }
    #endregion

    #region Helpers
    internal string GetPresetDisplayName(PlayerControllerPreset preset)
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
    internal enum SectionType
    {
        Metadata = 0,
        Movement = 1,
        Look = 2,
        Shooting = 3,
        Camera = 4,
        HealthStatistics = 5
    }
    #endregion
}
