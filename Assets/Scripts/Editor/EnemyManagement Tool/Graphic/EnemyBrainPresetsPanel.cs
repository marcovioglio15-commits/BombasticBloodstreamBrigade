using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for creating, editing, duplicating and deleting enemy brain presets.
/// </summary>
public sealed class EnemyBrainPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.EnemyManagement.Brain.ActiveSection";
    private const string ActiveSubSectionStateKey = "NashCore.EnemyManagement.Brain.ActiveSubSection";
    private const string DetailsScrollOffsetStateKey = "NashCore.EnemyManagement.Brain.DetailsScroll";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyBrainPreset> filteredPresets = new List<EnemyBrainPreset>();

    private EnemyBrainPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;
    private VisualElement brainSubSectionTabBar;
    private VisualElement brainSubSectionContentHost;
    private readonly Dictionary<BrainSubSectionType, BrainSubSectionTabEntry> brainSubSectionTabs = new Dictionary<BrainSubSectionType, BrainSubSectionTabEntry>();

    private EnemyBrainPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
    private BrainSubSectionType activeBrainSubSection = BrainSubSectionType.Movement;
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

    internal VisualElement BrainSubSectionTabBar
    {
        get
        {
            return brainSubSectionTabBar;
        }
        set
        {
            brainSubSectionTabBar = value;
        }
    }

    internal VisualElement BrainSubSectionContentHost
    {
        get
        {
            return brainSubSectionContentHost;
        }
        set
        {
            brainSubSectionContentHost = value;
        }
    }

    internal Dictionary<BrainSubSectionType, BrainSubSectionTabEntry> BrainSubSectionTabs
    {
        get
        {
            return brainSubSectionTabs;
        }
    }

    internal BrainSubSectionType ActiveBrainSubSection
    {
        get
        {
            return activeBrainSubSection;
        }
        set
        {
            activeBrainSubSection = value;
        }
    }
    #endregion

    #region Constructors
    public EnemyBrainPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);
        activeBrainSubSection = ManagementToolStateUtility.LoadEnumValue(ActiveSubSectionStateKey, BrainSubSectionType.Movement);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods
    #region Public Methods
    public void RefreshFromSessionChange()
    {
        EnemyBrainPreset previouslySelectedPreset = selectedPreset;
        library = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();
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

    public void SelectPresetFromExternal(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();

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

        VisualElement leftPane = BuildLeftPane();
        VisualElement rightPane = BuildRightPane();

        splitView.Add(leftPane);
        splitView.Add(rightPane);

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
            EnemyBrainPreset preset = label.userData as EnemyBrainPreset;

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

        EnemyBrainPreset preset = filteredPresets[index];

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
            EnemyBrainPreset preset = item as EnemyBrainPreset;

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
                EnemyBrainPreset preset = library.Presets[index];

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

    private bool IsMatchingSearch(EnemyBrainPreset preset, string searchText)
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
        EnemyBrainPreset newPreset = EnemyBrainPresetLibraryUtility.CreatePresetAsset("EnemyBrainPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Brain Preset Asset");
        Undo.RecordObject(library, "Add Enemy Brain Preset");
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
    private void DuplicatePreset(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        EnemyBrainPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyBrainPreset>();
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
            duplicateBaseName = "EnemyBrainPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Brain Preset Asset");
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

        Undo.RecordObject(library, "Duplicate Enemy Brain Preset");
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
    private void DeletePreset(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Brain Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(library, "Delete Enemy Brain Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(EnemyBrainPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;
        brainSubSectionTabBar = null;
        brainSubSectionContentHost = null;
        brainSubSectionTabs.Clear();

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
        AddDetailsSectionButton(buttonsRoot, SectionType.Brain, "Brain");
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
    /// <summary>
    /// Sets active top-level details section and persists it for next reopen.
    /// Called by section tab buttons in the details pane.
    /// Takes in the target section enum.
    /// </summary>
    /// <param name="sectionType">Section to display in details content.</param>
    private void SetActiveSection(SectionType sectionType)
    {
        // Persist selected section and rebuild details content.
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
                EnemyBrainPresetsPanelSectionsUtility.BuildMetadataSection(this);
                break;

            case SectionType.Brain:
                EnemyBrainPresetsPanelSectionsUtility.BuildBrainSection(this);
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

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Brain Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    internal void SetActiveBrainSubSection(BrainSubSectionType subSectionType)
    {
        activeBrainSubSection = subSectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSubSectionStateKey, activeBrainSubSection);
        ShowActiveBrainSubSection();
    }

    internal void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(EnemyBrainPreset preset, string newName)
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


    private void ShowRenamePopup(VisualElement anchor, EnemyBrainPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Enemy Brain Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(preset, newName));
    }


    private string GetPresetDisplayName(EnemyBrainPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }

    internal void ShowActiveBrainSubSection()
    {
        EnemyBrainPresetsPanelSectionsUtility.ShowActiveBrainSubSection(this);
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        Brain = 1
    }



    internal enum BrainSubSectionType
    {
        Movement = 0,
        Steering = 1,
        TacticalNavigation = 2,
        Damage = 3,
        HealthStatistics = 4
    }


    internal sealed class BrainSubSectionTabEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
    }
    #endregion
}
