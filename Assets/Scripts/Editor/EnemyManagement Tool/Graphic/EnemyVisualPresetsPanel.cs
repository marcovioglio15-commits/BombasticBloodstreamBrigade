using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for creating, editing, duplicating and deleting enemy visual presets.
/// </summary>
public sealed class EnemyVisualPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.EnemyManagement.Visual.ActiveSection";
    private const string ActiveSubSectionStateKey = "NashCore.EnemyManagement.Visual.ActiveSubSection";
    private const string DetailsScrollOffsetStateKey = "NashCore.EnemyManagement.Visual.DetailsScroll";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyVisualPreset> filteredPresets = new List<EnemyVisualPreset>();

    private EnemyVisualPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;
    private VisualElement visualSubSectionTabBar;
    private VisualElement visualSubSectionContentHost;
    private readonly Dictionary<VisualSubSectionType, VisualSubSectionTabEntry> visualSubSectionTabs = new Dictionary<VisualSubSectionType, VisualSubSectionTabEntry>();

    private EnemyVisualPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
    private VisualSubSectionType activeVisualSubSection = VisualSubSectionType.Visibility;
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

    internal VisualElement DetailsSectionContentRoot
    {
        get
        {
            return detailsSectionContentRoot;
        }
    }

    internal VisualElement VisualSubSectionTabBar
    {
        get
        {
            return visualSubSectionTabBar;
        }
        set
        {
            visualSubSectionTabBar = value;
        }
    }

    internal VisualElement VisualSubSectionContentHost
    {
        get
        {
            return visualSubSectionContentHost;
        }
        set
        {
            visualSubSectionContentHost = value;
        }
    }

    internal Dictionary<VisualSubSectionType, VisualSubSectionTabEntry> VisualSubSectionTabs
    {
        get
        {
            return visualSubSectionTabs;
        }
    }

    internal VisualSubSectionType ActiveVisualSubSection
    {
        get
        {
            return activeVisualSubSection;
        }
        set
        {
            activeVisualSubSection = value;
        }
    }
    #endregion

    #region Constructors
    public EnemyVisualPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyVisualPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);
        activeVisualSubSection = ManagementToolStateUtility.LoadEnumValue(ActiveSubSectionStateKey, VisualSubSectionType.Visibility);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void RefreshFromSessionChange()
    {
        EnemyVisualPreset previouslySelectedPreset = selectedPreset;
        library = EnemyVisualPresetLibraryUtility.GetOrCreateLibrary();
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

    public void SelectPresetFromExternal(EnemyVisualPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyVisualPresetLibraryUtility.GetOrCreateLibrary();

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

        // External sync: skip detail rebuild when the preset is already active so dropdowns/scroll stay.
        if (selectedPreset == preset && presetSerializedObject != null && presetSerializedObject.targetObject == preset)
        {
            presetSerializedObject.UpdateIfRequiredOrScript();
            return;
        }

        SelectPreset(preset);
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        splitView.Add(BuildLeftPane());
        splitView.Add(BuildRightPane());
        root.Add(splitView);
    }

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

        Button createButton = new Button();
        createButton.text = "Create";
        createButton.clicked += CreatePreset;
        toolbar.Add(createButton);

        Button duplicateButton = new Button();
        duplicateButton.text = "Duplicate";
        duplicateButton.clicked += DuplicatePreset;
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button();
        deleteButton.text = "Delete";
        deleteButton.clicked += DeletePreset;
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
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            EnemyVisualPreset preset = label.userData as EnemyVisualPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => ShowRenamePopup(label, preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

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

        EnemyVisualPreset preset = filteredPresets[index];

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

    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            EnemyVisualPreset preset = item as EnemyVisualPreset;

            if (preset == null)
                continue;

            // Re-fired selectionChanged events (e.g. after ListView.Rebuild) would otherwise tear down
            // the detail subtree even when the selection did not actually change.
            if (selectedPreset == preset)
                return;

            SelectPreset(preset);
            return;
        }

        if (selectedPreset != null)
            SelectPreset(null);
    }

    internal void RefreshPresetList()
    {
        filteredPresets.Clear();

        if (library != null)
        {
            string searchText = searchField != null ? searchField.value : string.Empty;

            for (int index = 0; index < library.Presets.Count; index++)
            {
                EnemyVisualPreset preset = library.Presets[index];

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

    private bool IsMatchingSearch(EnemyVisualPreset preset, string searchText)
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
    private void CreatePreset()
    {
        EnemyVisualPreset newPreset = EnemyVisualPresetLibraryUtility.CreatePresetAsset("EnemyVisualPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Visual Preset Asset");
        Undo.RecordObject(library, "Add Enemy Visual Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(newPreset);

        int index = filteredPresets.IndexOf(newPreset);

        if (index >= 0)
            listView.SetSelection(index);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(EnemyVisualPreset preset)
    {
        if (preset == null)
            return;

        EnemyVisualPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyVisualPreset>();
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
            duplicateBaseName = "EnemyVisualPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Visual Preset Asset");
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
        Undo.RecordObject(library, "Duplicate Enemy Visual Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = filteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0)
            listView.SetSelection(index);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(EnemyVisualPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Visual Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(library, "Delete Enemy Visual Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);
        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(EnemyVisualPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;
        visualSubSectionTabBar = null;
        visualSubSectionContentHost = null;
        visualSubSectionTabs.Clear();

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

    private void AddDetailsSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetActiveSection(sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    private void SetActiveSection(SectionType sectionType)
    {
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
        BuildActiveDetailsSection();
    }

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
                EnemyVisualPresetsPanelSectionsUtility.BuildMetadataSection(this);
                break;

            case SectionType.Visual:
                EnemyVisualPresetsPanelSectionsUtility.BuildVisualSection(this);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(detailsSectionContentRoot);
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Visual Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    internal void SetActiveVisualSubSection(VisualSubSectionType subSectionType)
    {
        activeVisualSubSection = subSectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSubSectionStateKey, activeVisualSubSection);
        ShowActiveVisualSubSection();
    }

    internal void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(EnemyVisualPreset preset, string newName)
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

    private void ShowRenamePopup(VisualElement anchor, EnemyVisualPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect,
                               "Rename Enemy Visual Preset",
                               preset.PresetName,
                               newName => RenamePreset(preset, newName));
    }

    private string GetPresetDisplayName(EnemyVisualPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }

    internal void ShowActiveVisualSubSection()
    {
        EnemyVisualPresetsPanelSectionsUtility.ShowActiveVisualSubSection(this);
    }

    internal void RebuildActiveDetailsSection()
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

    internal enum VisualSubSectionType
    {
        Visibility = 0,
        Outline = 1,
        DamageFeedback = 2,
        Prefabs = 3,
        OffensiveEngagementFeedback = 4,
        BossPatternChangeFeedback = 5,
        SpawnOverrides = 6,
        BossUi = 7,
        ProjectileOffscreenWarning = 8,
        Footprint = 9
    }

    internal sealed class VisualSubSectionTabEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
    }
    #endregion
}
