using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for creating, editing, duplicating and deleting enemy UI visual presets.
/// </summary>
public sealed class EnemyUiVisualPresetsPanel : IEnemyVisualPresetEditorPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string SelectedPresetPathStateKey = "NashCore.EnemyManagement.UiVisual.SelectedPreset";
    private const string ActiveSectionStateKey = "NashCore.EnemyManagement.UiVisual.ActiveSection";
    private const string ActiveSubSectionStateKey = "NashCore.EnemyManagement.UiVisual.ActiveSubSection";
    private const string DetailsScrollOffsetStateKey = "NashCore.EnemyManagement.UiVisual.DetailsScroll";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyUiVisualPreset> filteredPresets = new List<EnemyUiVisualPreset>();
    private readonly Dictionary<UiVisualSubSectionType, UiVisualSubSectionTabEntry> uiVisualSubSectionTabs = new Dictionary<UiVisualSubSectionType, UiVisualSubSectionTabEntry>();

    private EnemyUiVisualPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;
    private VisualElement uiVisualSubSectionTabBar;
    private VisualElement uiVisualSubSectionContentHost;
    private EnemyUiVisualPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
    private UiVisualSubSectionType activeUiVisualSubSection = UiVisualSubSectionType.Footprint;
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

    SerializedObject IEnemyVisualPresetEditorPanel.PresetSerializedObject
    {
        get
        {
            return presetSerializedObject;
        }
    }

    internal VisualElement DetailsSectionContentRoot
    {
        get
        {
            return detailsSectionContentRoot;
        }
    }

    internal VisualElement UiVisualSubSectionTabBar
    {
        get
        {
            return uiVisualSubSectionTabBar;
        }
        set
        {
            uiVisualSubSectionTabBar = value;
        }
    }

    internal VisualElement UiVisualSubSectionContentHost
    {
        get
        {
            return uiVisualSubSectionContentHost;
        }
        set
        {
            uiVisualSubSectionContentHost = value;
        }
    }

    internal Dictionary<UiVisualSubSectionType, UiVisualSubSectionTabEntry> UiVisualSubSectionTabs
    {
        get
        {
            return uiVisualSubSectionTabs;
        }
    }

    internal UiVisualSubSectionType ActiveUiVisualSubSection
    {
        get
        {
            return activeUiVisualSubSection;
        }
        set
        {
            activeUiVisualSubSection = value;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes the enemy UI visual preset panel and restores the previously selected sections.
    /// </summary>
    public EnemyUiVisualPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyUiVisualPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);
        activeUiVisualSubSection = ManagementToolStateUtility.LoadEnumValue(ActiveSubSectionStateKey, UiVisualSubSectionType.Footprint);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes the panel after asset changes and preserves the selection when possible.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        EnemyUiVisualPreset previouslySelectedPreset = selectedPreset;
        library = EnemyUiVisualPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previouslySelectedPreset == null)
            return;

        int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
            return;

        if (listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        if (selectedPreset == previouslySelectedPreset && presetSerializedObject != null && presetSerializedObject.targetObject == previouslySelectedPreset)
            presetSerializedObject.UpdateIfRequiredOrScript();
        else
            SelectPreset(previouslySelectedPreset);
    }

    /// <summary>
    /// Selects one UI visual preset from an external caller such as the master side panel.
    /// </summary>
    /// <param name="preset">Preset to select.</param>
    public void SelectPresetFromExternal(EnemyUiVisualPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyUiVisualPresetLibraryUtility.GetOrCreateLibrary();

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

        if (selectedPreset == preset && presetSerializedObject != null && presetSerializedObject.targetObject == preset)
        {
            presetSerializedObject.UpdateIfRequiredOrScript();
            return;
        }

        SelectPreset(preset);
    }
    #endregion

    #region UI Construction
    /// <summary>
    /// Builds the split-view root that hosts preset list and details.
    /// </summary>
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        splitView.Add(BuildLeftPane());
        splitView.Add(BuildRightPane());
        root.Add(splitView);
    }

    /// <summary>
    /// Builds the searchable preset list and action toolbar.
    /// </summary>
    /// <returns>Configured left pane.</returns>
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
        toolbar.Add(createButton);

        Button duplicateButton = new Button(DuplicatePreset);
        duplicateButton.text = "Duplicate";
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(DeletePreset);
        deleteButton.text = "Delete";
        toolbar.Add(deleteButton);
        leftPane.Add(toolbar);

        searchField = new ToolbarSearchField();
        searchField.style.width = Length.Percent(100f);
        searchField.style.maxWidth = Length.Percent(100f);
        searchField.style.flexShrink = 1f;
        searchField.style.marginBottom = 4f;
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
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
    /// Builds the right-side details scroll view.
    /// </summary>
    /// <returns>Configured right pane.</returns>
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
        ManagementToolScrollStateUtility.Attach(detailsRoot, DetailsScrollOffsetStateKey);
        return rightPane;
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Creates one list row label with context menu actions.
    /// </summary>
    /// <returns>Configured list row element.</returns>
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            EnemyUiVisualPreset preset = label.userData as EnemyUiVisualPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => ShowRenamePopup(label, preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one UI visual preset to a list row.
    /// </summary>
    /// <param name="element">Row element created by the list view.</param>
    /// <param name="index">Filtered preset index to bind.</param>
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

        EnemyUiVisualPreset preset = filteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }

    /// <summary>
    /// Applies the selected list item to the detail panel without rebuilding for duplicate events.
    /// </summary>
    /// <param name="selection">ListView selection payload.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            EnemyUiVisualPreset preset = item as EnemyUiVisualPreset;

            if (preset == null)
                continue;

            if (selectedPreset == preset)
                return;

            SelectPreset(preset);
            return;
        }

        if (selectedPreset != null)
            SelectPreset(null);
    }

    /// <summary>
    /// Rebuilds the filtered preset list from the dedicated UI visual library.
    /// </summary>
    public void RefreshPresetList()
    {
        filteredPresets.Clear();

        if (library != null)
        {
            string searchText = searchField != null ? searchField.value : string.Empty;

            for (int index = 0; index < library.Presets.Count; index++)
            {
                EnemyUiVisualPreset preset = library.Presets[index];

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
            EnemyUiVisualPreset restoredPreset = ManagementToolStateUtility.LoadAsset<EnemyUiVisualPreset>(SelectedPresetPathStateKey);
            EnemyUiVisualPreset initialPreset = restoredPreset != null && filteredPresets.Contains(restoredPreset)
                ? restoredPreset
                : filteredPresets[0];
            SelectPreset(initialPreset);

            int initialIndex = filteredPresets.IndexOf(initialPreset);

            if (initialIndex >= 0 && listView != null)
                listView.SetSelectionWithoutNotify(new int[] { initialIndex });
        }
    }

    /// <summary>
    /// Checks whether one preset matches the current search text.
    /// </summary>
    /// <param name="preset">Preset candidate.</param>
    /// <param name="searchText">Search text entered by the user.</param>
    /// <returns>True when the preset should remain visible.</returns>
    private static bool IsMatchingSearch(EnemyUiVisualPreset preset, string searchText)
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
    /// Creates and selects one new enemy UI visual preset asset.
    /// </summary>
    private void CreatePreset()
    {
        EnemyUiVisualPreset newPreset = EnemyUiVisualPresetLibraryUtility.CreatePresetAsset("EnemyUiVisualPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy UI Visual Preset Asset");
        Undo.RecordObject(library, "Add Enemy UI Visual Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
        SelectPreset(newPreset);

        int index = filteredPresets.IndexOf(newPreset);

        if (index >= 0)
            listView.SetSelection(index);
    }

    /// <summary>
    /// Duplicates the currently selected UI visual preset.
    /// </summary>
    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    /// <summary>
    /// Duplicates one UI visual preset asset and assigns fresh metadata.
    /// </summary>
    /// <param name="preset">Preset to duplicate.</param>
    private void DuplicatePreset(EnemyUiVisualPreset preset)
    {
        if (preset == null)
            return;

        EnemyUiVisualPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyUiVisualPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
            return;

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = EnemyManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "EnemyUiVisualPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy UI Visual Preset Asset");
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();
        Undo.RecordObject(library, "Duplicate Enemy UI Visual Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = filteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0)
            listView.SetSelection(index);
    }

    /// <summary>
    /// Deletes the currently selected UI visual preset after confirmation.
    /// </summary>
    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    /// <summary>
    /// Removes one UI visual preset from the library and stages the asset for deletion.
    /// </summary>
    /// <param name="preset">Preset to delete.</param>
    private void DeletePreset(EnemyUiVisualPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy UI Visual Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(library, "Delete Enemy UI Visual Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);
        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    /// <summary>
    /// Selects one UI visual preset and rebuilds the details panel.
    /// </summary>
    /// <param name="preset">Preset to edit.</param>
    private void SelectPreset(EnemyUiVisualPreset preset)
    {
        selectedPreset = preset;
        ManagementToolStateUtility.SaveAssetPath(SelectedPresetPathStateKey, preset);
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;
        uiVisualSubSectionTabBar = null;
        uiVisualSubSectionContentHost = null;
        uiVisualSubSectionTabs.Clear();

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create a preset to edit.");
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
    /// Builds top-level detail section buttons.
    /// </summary>
    /// <returns>Configured buttons row.</returns>
    private VisualElement BuildDetailsSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddDetailsSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddDetailsSectionButton(buttonsRoot, SectionType.Visual, "Visual");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one top-level detail section button.
    /// </summary>
    /// <param name="parent">Buttons row receiving the button.</param>
    /// <param name="sectionType">Section selected by the button.</param>
    /// <param name="buttonLabel">Visible button label.</param>
    private void AddDetailsSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetActiveSection(sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    /// <summary>
    /// Stores and rebuilds the active top-level section.
    /// </summary>
    /// <param name="sectionType">Section selected by the user.</param>
    private void SetActiveSection(SectionType sectionType)
    {
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
        BuildActiveDetailsSection();
    }

    /// <summary>
    /// Rebuilds the visible details section from the selected preset.
    /// </summary>
    private void BuildActiveDetailsSection()
    {
        if (detailsSectionContentRoot == null)
            return;

        if (presetSerializedObject == null)
            return;

        presetSerializedObject.Update();
        detailsSectionContentRoot.Clear();

        switch (activeSection)
        {
            case SectionType.Metadata:
                EnemyUiVisualPresetsPanelSectionsUtility.BuildMetadataSection(this);
                break;

            case SectionType.Visual:
                EnemyUiVisualPresetsPanelSectionsUtility.BuildVisualSection(this);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(detailsSectionContentRoot);
    }

    /// <summary>
    /// Assigns a new stable ID to the selected UI visual preset.
    /// </summary>
    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy UI Visual Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Stores and shows the selected UI visual subsection.
    /// </summary>
    /// <param name="subSectionType">Subsection selected by the user.</param>
    internal void SetActiveUiVisualSubSection(UiVisualSubSectionType subSectionType)
    {
        activeUiVisualSubSection = subSectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSubSectionStateKey, activeUiVisualSubSection);
        ShowActiveUiVisualSubSection();
    }

    /// <summary>
    /// Applies a delayed preset name edit to asset metadata.
    /// </summary>
    /// <param name="newName">New preset name entered by the user.</param>
    internal void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    /// <summary>
    /// Renames one UI visual preset asset and updates its serialized display name.
    /// </summary>
    /// <param name="preset">Preset to rename.</param>
    /// <param name="newName">Requested new name.</param>
    private void RenamePreset(EnemyUiVisualPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        SerializedObject presetSerialized = new SerializedObject(preset);
        SerializedProperty presetNameProperty = presetSerialized.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            presetSerialized.Update();
            presetNameProperty.stringValue = normalizedName;
            presetSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    /// <summary>
    /// Opens the rename popup anchored to a list row.
    /// </summary>
    /// <param name="anchor">Visual row used as popup anchor.</param>
    /// <param name="preset">Preset to rename.</param>
    private void ShowRenamePopup(VisualElement anchor, EnemyUiVisualPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect,
                               "Rename Enemy UI Visual Preset",
                               preset.PresetName,
                               newName => RenamePreset(preset, newName));
    }

    /// <summary>
    /// Builds the display name shown in the preset list.
    /// </summary>
    /// <param name="preset">Preset to format.</param>
    /// <returns>List display name.</returns>
    private static string GetPresetDisplayName(EnemyUiVisualPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }

    /// <summary>
    /// Shows the active UI visual subsection in the right-side details host.
    /// </summary>
    internal void ShowActiveUiVisualSubSection()
    {
        EnemyUiVisualPresetsPanelSectionsUtility.ShowActiveUiVisualSubSection(this);
    }

    /// <summary>
    /// Rebuilds the current details section after conditional UI changes.
    /// </summary>
    public void RebuildActiveDetailsSection()
    {
        BuildActiveDetailsSection();
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        Visual = 1
    }

    internal enum UiVisualSubSectionType
    {
        Footprint = 0,
        BossUi = 1,
        ProjectileOffscreenWarning = 2
    }

    internal sealed class UiVisualSubSectionTabEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
    }
    #endregion
}
