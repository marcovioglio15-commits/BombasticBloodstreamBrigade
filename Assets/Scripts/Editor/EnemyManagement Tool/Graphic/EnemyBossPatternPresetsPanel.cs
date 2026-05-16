using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides the Enemy Management Tool panel used to author boss-only pattern presets.
/// </summary>
public sealed class EnemyBossPatternPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.EnemyManagement.BossPattern.ActiveSection";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyBossPatternPreset> filteredPresets = new List<EnemyBossPatternPreset>();

    private EnemyBossPatternPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;
    private EnemyBossPatternPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
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
    }

    internal EnemyBossPatternPreset SelectedPreset
    {
        get
        {
            return selectedPreset;
        }
    }

    internal EnemyBossPatternPresetLibrary Library
    {
        get
        {
            return library;
        }
    }

    internal List<EnemyBossPatternPreset> FilteredPresets
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
    }

    internal VisualElement DetailsSectionContentRoot
    {
        get
        {
            return detailsSectionContentRoot;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates the panel, restores its active subsection and loads the boss preset library.
    /// </summary>
    public EnemyBossPatternPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyBossPatternPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reloads library references and keeps the current selection after session-level changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        EnemyBossPatternPreset previouslySelectedPreset = selectedPreset;
        library = EnemyBossPatternPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previouslySelectedPreset == null)
            return;

        int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
            return;

        if (listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(previouslySelectedPreset);
    }

    /// <summary>
    /// Selects one boss preset from an external owner, clearing filters if needed.
    /// </summary>
    /// <param name="preset">Boss pattern preset to select.</param>
    public void SelectPresetFromExternal(EnemyBossPatternPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyBossPatternPresetLibraryUtility.GetOrCreateLibrary();

        RefreshPresetList();
        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex < 0 && searchField != null && !string.IsNullOrWhiteSpace(searchField.value))
        {
            searchField.SetValueWithoutNotify(string.Empty);
            RefreshPresetList();
            presetIndex = filteredPresets.IndexOf(preset);
        }

        if (presetIndex < 0)
            return;

        if (listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(preset);
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Builds the split-view layout for list and details panes.
    /// </summary>
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        splitView.Add(BuildLeftPane());
        splitView.Add(BuildRightPane());
        root.Add(splitView);
    }

    /// <summary>
    /// Builds the left pane containing actions, search and preset list.
    /// </summary>
    /// <returns>The created left pane.</returns>
    private VisualElement BuildLeftPane()
    {
        VisualElement leftPane = new VisualElement();
        leftPane.style.flexGrow = 1f;
        leftPane.style.paddingLeft = 6f;
        leftPane.style.paddingRight = 6f;
        leftPane.style.paddingTop = 6f;
        leftPane.style.overflow = Overflow.Hidden;

        Toolbar toolbar = new Toolbar();
        toolbar.style.marginBottom = 4f;

        Button createButton = new Button(CreatePreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new boss pattern preset asset.";
        toolbar.Add(createButton);

        Button duplicateButton = new Button(DuplicatePreset);
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected boss pattern preset.";
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(DeletePreset);
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Stage the selected boss pattern preset for deletion.";
        toolbar.Add(deleteButton);

        leftPane.Add(toolbar);

        searchField = new ToolbarSearchField();
        searchField.style.width = Length.Percent(100f);
        searchField.style.maxWidth = Length.Percent(100f);
        searchField.style.flexShrink = 1f;
        searchField.style.marginBottom = 4f;
        searchField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        leftPane.Add(searchField);

        listView = new ListView();
        listView.style.flexGrow = 1f;
        listView.itemsSource = filteredPresets;
        listView.selectionType = SelectionType.Single;
        listView.makeItem = MakePresetItem;
        listView.bindItem = BindPresetItem;
        listView.selectionChanged += OnPresetSelectionChanged;
        leftPane.Add(listView);

        return leftPane;
    }

    /// <summary>
    /// Builds the scrollable details pane.
    /// </summary>
    /// <returns>The created right pane.</returns>
    private VisualElement BuildRightPane()
    {
        VisualElement rightPane = new VisualElement();
        rightPane.style.flexGrow = 1f;
        rightPane.style.paddingLeft = 10f;
        rightPane.style.paddingRight = 10f;
        rightPane.style.paddingTop = 6f;

        detailsRoot = new ScrollView();
        detailsRoot.style.flexGrow = 1f;
        rightPane.Add(detailsRoot);

        return rightPane;
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Creates one list item used by the boss preset list.
    /// </summary>
    /// <returns>The list item label.</returns>
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            EnemyBossPatternPreset preset = label.userData as EnemyBossPatternPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => ShowRenamePopup(label, preset), DropdownMenuAction.AlwaysEnabled);
        }));

        return label;
    }

    /// <summary>
    /// Binds one boss preset to a list item.
    /// </summary>
    /// <param name="element">List element to bind.</param>
    /// <param name="index">Filtered preset index.</param>
    private void BindPresetItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        EnemyBossPatternPreset preset = filteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = EnemyBossPatternPresetsPanelPresetUtility.GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }

    /// <summary>
    /// Applies list selection changes to the details pane.
    /// </summary>
    /// <param name="selection">Selected list objects.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            EnemyBossPatternPreset preset = item as EnemyBossPatternPreset;

            if (preset != null)
            {
                SelectPreset(preset);
                return;
            }
        }

        SelectPreset(null);
    }

    /// <summary>
    /// Rebuilds the filtered boss preset list and repairs selection when needed.
    /// </summary>
    internal void RefreshPresetList()
    {
        filteredPresets.Clear();

        if (library != null)
        {
            string searchText = searchField != null ? searchField.value : string.Empty;

            for (int index = 0; index < library.Presets.Count; index++)
            {
                EnemyBossPatternPreset preset = library.Presets[index];

                if (preset == null)
                    continue;

                if (EnemyManagementDraftSession.IsAssetStagedForDeletion(preset))
                    continue;

                if (IsMatchingSearch(preset, searchText))
                    filteredPresets.Add(preset);
            }
        }

        if (listView != null)
            listView.Rebuild();

        if (filteredPresets.Count == 0)
        {
            SelectPreset(null);
            return;
        }

        if (selectedPreset == null || !filteredPresets.Contains(selectedPreset))
        {
            SelectPreset(filteredPresets[0]);

            if (listView != null)
                listView.SetSelectionWithoutNotify(new int[] { 0 });
        }
    }

    /// <summary>
    /// Checks whether one preset matches the current search text.
    /// </summary>
    /// <param name="preset">Candidate preset.</param>
    /// <param name="searchText">Current search text.</param>
    /// <returns>True when the preset should be visible.</returns>
    private static bool IsMatchingSearch(EnemyBossPatternPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string presetName = preset.PresetName;

        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        return presetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Preset Actions
    /// <summary>
    /// Creates and selects one new boss pattern preset asset.
    /// </summary>
    private void CreatePreset()
    {
        EnemyBossPatternPresetsPanelPresetUtility.CreatePreset(this);
    }

    /// <summary>
    /// Duplicates the currently selected boss pattern preset.
    /// </summary>
    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    /// <summary>
    /// Duplicates a specific boss pattern preset asset and registers the copy.
    /// </summary>
    /// <param name="preset">Preset to duplicate.</param>
    private void DuplicatePreset(EnemyBossPatternPreset preset)
    {
        EnemyBossPatternPresetsPanelPresetUtility.DuplicatePreset(this, preset);
    }

    /// <summary>
    /// Stages the currently selected boss pattern preset for deletion.
    /// </summary>
    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    /// <summary>
    /// Stages one boss pattern preset for deletion after confirmation.
    /// </summary>
    /// <param name="preset">Preset to delete.</param>
    private void DeletePreset(EnemyBossPatternPreset preset)
    {
        EnemyBossPatternPresetsPanelPresetUtility.DeletePreset(this, preset);
    }
    #endregion

    #region Preset Details
    /// <summary>
    /// Selects one boss preset and rebuilds the details pane.
    /// </summary>
    /// <param name="preset">Boss pattern preset to edit.</param>
    private void SelectPreset(EnemyBossPatternPreset preset)
    {
        SelectPresetInternal(preset);
    }

    /// <summary>
    /// Selects one boss preset and rebuilds the details pane.
    /// </summary>
    /// <param name="preset">Boss pattern preset to edit.</param>
    internal void SelectPresetInternal(EnemyBossPatternPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create a boss pattern preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(label);
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);
        detailsSectionButtonsRoot = BuildDetailsSectionButtons();
        detailsSectionContentRoot = new VisualElement();
        detailsSectionContentRoot.style.flexDirection = FlexDirection.Column;
        detailsSectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(detailsSectionButtonsRoot);
        detailsRoot.Add(detailsSectionContentRoot);

        BuildActiveDetailsSection();
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(detailsRoot);
    }

    /// <summary>
    /// Builds the subsection tab row for the selected boss preset.
    /// </summary>
    /// <returns>The created tab row.</returns>
    private VisualElement BuildDetailsSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        buttonsRoot.userData = this;

        AddDetailsSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddDetailsSectionButton(buttonsRoot, SectionType.SourcePatterns, "Module & Patterns Preset");
        AddDetailsSectionButton(buttonsRoot, SectionType.PatternAssemble, "Pattern Assemble");
        AddDetailsSectionButton(buttonsRoot, SectionType.MinionSpawn, "Minion Spawn");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one subsection button to the selected boss preset details.
    /// </summary>
    /// <param name="parent">Tab row parent.</param>
    /// <param name="sectionType">Target subsection.</param>
    /// <param name="buttonLabel">Visible button label.</param>
    private static void AddDetailsSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetSectionFromButton(parent, sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    /// <summary>
    /// Resolves the panel stored on the button row and activates the requested subsection.
    /// </summary>
    /// <param name="parent">Tab row parent.</param>
    /// <param name="sectionType">Target subsection.</param>
    private static void SetSectionFromButton(VisualElement parent, SectionType sectionType)
    {
        if (parent == null)
            return;

        EnemyBossPatternPresetsPanel panel = parent.userData as EnemyBossPatternPresetsPanel;

        if (panel == null)
            return;

        panel.SetActiveSection(sectionType);
    }

    /// <summary>
    /// Persists and rebuilds the active boss preset subsection.
    /// </summary>
    /// <param name="sectionType">Target subsection.</param>
    private void SetActiveSection(SectionType sectionType)
    {
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
        BuildActiveDetailsSection();
    }

    /// <summary>
    /// Rebuilds the currently active details subsection.
    /// </summary>
    internal void BuildActiveDetailsSection()
    {
        if (detailsSectionButtonsRoot != null)
            detailsSectionButtonsRoot.userData = this;

        if (detailsSectionContentRoot == null)
            return;

        if (presetSerializedObject == null)
            return;

        presetSerializedObject.Update();
        detailsSectionContentRoot.Clear();

        switch (activeSection)
        {
            case SectionType.Metadata:
                EnemyBossPatternPresetsPanelSectionsUtility.BuildMetadataSection(this);
                break;

            case SectionType.SourcePatterns:
                EnemyBossPatternPresetsPanelSectionsUtility.BuildSourcePatternsSection(this);
                break;

            case SectionType.PatternAssemble:
                EnemyBossPatternPresetsPanelSectionsUtility.BuildPatternAssembleSection(this);
                break;

            case SectionType.MinionSpawn:
                EnemyBossPatternPresetsPanelSectionsUtility.BuildMinionSpawnSection(this);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(detailsSectionContentRoot);
    }

    /// <summary>
    /// Regenerates the selected boss preset ID.
    /// </summary>
    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Boss Pattern Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Applies a committed preset-name edit from the metadata section.
    /// </summary>
    /// <param name="newName">New preset name.</param>
    internal void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    /// <summary>
    /// Renames the preset asset and display metadata.
    /// </summary>
    /// <param name="preset">Preset to rename.</param>
    /// <param name="newName">Requested name.</param>
    private void RenamePreset(EnemyBossPatternPreset preset, string newName)
    {
        EnemyBossPatternPresetsPanelPresetUtility.RenamePreset(this, preset, newName);
    }

    /// <summary>
    /// Opens the rename popup for a boss preset list item.
    /// </summary>
    /// <param name="anchor">UI anchor for popup placement.</param>
    /// <param name="preset">Preset being renamed.</param>
    private void ShowRenamePopup(VisualElement anchor, EnemyBossPatternPreset preset)
    {
        EnemyBossPatternPresetsPanelPresetUtility.ShowRenamePopup(this, anchor, preset);
    }
    #endregion

    #endregion

    #region Nested Types
    internal enum SectionType
    {
        Metadata = 0,
        SourcePatterns = 1,
        PatternAssemble = 2,
        MinionSpawn = 3
    }
    #endregion
}
