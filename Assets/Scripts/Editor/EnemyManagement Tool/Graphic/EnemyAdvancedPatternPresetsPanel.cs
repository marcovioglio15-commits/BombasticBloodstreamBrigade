using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for creating, editing, duplicating and deleting enemy advanced pattern presets.
/// </summary>
public sealed class EnemyAdvancedPatternPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.EnemyManagement.AdvancedPattern.ActiveSection";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyAdvancedPatternPreset> filteredPresets = new List<EnemyAdvancedPatternPreset>();
    private readonly EnemyAdvancedPatternSharedPresetViewState sharedPresetViewState = new EnemyAdvancedPatternSharedPresetViewState();

    private EnemyAdvancedPatternPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;

    private EnemyAdvancedPatternPreset selectedPreset;
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

    internal EnemyAdvancedPatternPreset SelectedPreset
    {
        get
        {
            return selectedPreset;
        }
    }

    internal VisualElement DetailsSectionContentRoot
    {
        get
        {
            return detailsSectionContentRoot;
        }
    }

    internal EnemyAdvancedPatternSharedPresetViewState SharedPresetViewState
    {
        get
        {
            return sharedPresetViewState;
        }
    }
    #endregion

    #region Constructors
    public EnemyAdvancedPatternPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void RefreshFromSessionChange()
    {
        CaptureDetailsViewState();
        EnemyAdvancedPatternPreset previouslySelectedPreset = selectedPreset;
        library = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();
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

    public void SelectPresetFromExternal(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();

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
            EnemyAdvancedPatternPreset preset = label.userData as EnemyAdvancedPatternPreset;

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

        EnemyAdvancedPatternPreset preset = filteredPresets[index];

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
            EnemyAdvancedPatternPreset preset = item as EnemyAdvancedPatternPreset;

            if (preset != null)
            {
                SelectPreset(preset);
                return;
            }
        }

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
                EnemyAdvancedPatternPreset preset = library.Presets[index];

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

    private static bool IsMatchingSearch(EnemyAdvancedPatternPreset preset, string searchText)
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
        EnemyAdvancedPatternPreset newPreset = EnemyAdvancedPatternPresetLibraryUtility.CreatePresetAsset("EnemyAdvancedPatternPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Advanced Pattern Preset Asset");
        Undo.RecordObject(library, "Add Enemy Advanced Pattern Preset");
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

    private void DuplicatePreset(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        EnemyAdvancedPatternPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyAdvancedPatternPreset>();
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
            duplicateBaseName = "EnemyAdvancedPatternPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Advanced Pattern Preset Asset");
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

        Undo.RecordObject(library, "Duplicate Enemy Advanced Pattern Preset");
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

    private void DeletePreset(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Advanced Pattern Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(library, "Delete Enemy Advanced Pattern Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(EnemyAdvancedPatternPreset preset)
    {
        CaptureDetailsViewState();
        selectedPreset = preset;
        presetSerializedObject = null;
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create a preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(label);
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);
        ManagementToolScrollStateUtility.BindScrollView(detailsRoot, BuildDetailsScrollStateKey());
        detailsSectionButtonsRoot = BuildDetailsSectionButtons();
        detailsSectionContentRoot = new VisualElement();
        detailsSectionContentRoot.style.flexDirection = FlexDirection.Column;
        detailsSectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(detailsSectionButtonsRoot);
        detailsRoot.Add(detailsSectionContentRoot);

        BuildActiveDetailsSection();
        RestoreDetailsViewState();
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(detailsRoot);
    }

    private VisualElement BuildDetailsSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddDetailsSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddDetailsSectionButton(buttonsRoot, SectionType.ModulesAndPatternsPreset, "Modules & Patterns Preset");
        AddDetailsSectionButton(buttonsRoot, SectionType.PatternLoadout, "Pattern Loadout");
        return buttonsRoot;
    }

    private static void AddDetailsSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetSectionFromButton(parent, sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    private static void SetSectionFromButton(VisualElement parent, SectionType sectionType)
    {
        if (parent == null)
            return;

        EnemyAdvancedPatternPresetsPanel panel = parent.userData as EnemyAdvancedPatternPresetsPanel;

        if (panel == null)
            return;

        panel.SetActiveSection(sectionType);
    }

    private void SetActiveSection(SectionType sectionType)
    {
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
        BuildActiveDetailsSection();
    }

    internal void BuildActiveDetailsSection()
    {
        CaptureDetailsViewState();

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
                EnemyAdvancedPatternPresetsPanelSectionsUtility.BuildMetadataSection(this);
                break;

            case SectionType.ModulesAndPatternsPreset:
                EnemyAdvancedPatternPresetsPanelSectionsUtility.BuildModulesAndPatternsPresetSection(this);
                break;

            case SectionType.PatternLoadout:
                EnemyAdvancedPatternPresetsPanelSectionsUtility.BuildPatternLoadoutSection(this);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(detailsSectionContentRoot);
        RestoreDetailsViewState();
    }

    /// <summary>
    /// Captures foldout and scroll state before the active details subtree is rebuilt.
    /// </summary>
    internal void CaptureDetailsViewState()
    {
        if (detailsRoot == null)
            return;

        ManagementToolFoldoutStateUtility.CaptureFoldoutStates(detailsRoot);
        ManagementToolScrollStateUtility.CaptureScrollOffsets(detailsRoot);
    }

    /// <summary>
    /// Restores the details scroll offset after UI Toolkit completes the redraw.
    /// </summary>
    private void RestoreDetailsViewState()
    {
        if (detailsRoot == null)
            return;

        ManagementToolScrollStateUtility.RestoreScrollOffset(detailsRoot);
    }

    /// <summary>
    /// Builds the stable scroll key for the selected preset details and active subsection.
    /// </summary>
    /// <returns>Stable scroll state key scoped by selected preset and subsection.</returns>
    private string BuildDetailsScrollStateKey()
    {
        if (presetSerializedObject == null)
            return string.Empty;

        string objectStateKey = ManagementToolFoldoutStateUtility.BuildSerializedObjectStateKey(presetSerializedObject);

        if (string.IsNullOrWhiteSpace(objectStateKey))
            return string.Empty;

        return objectStateKey + "|AdvancedPatternDetails|" + activeSection;
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Advanced Pattern Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Applies a committed preset-name edit coming from the metadata UI.
    /// </summary>
    /// <param name="newName">New preset name confirmed by the delayed text field.</param>
    internal void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(EnemyAdvancedPatternPreset preset, string newName)
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

    private void ShowRenamePopup(VisualElement anchor, EnemyAdvancedPatternPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Enemy Advanced Pattern Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(preset, newName));
    }

    private static string GetPresetDisplayName(EnemyAdvancedPatternPreset preset)
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
    private enum SectionType
    {
        Metadata = 0,
        ModulesAndPatternsPreset = 1,
        PatternLoadout = 2
    }

    internal struct PatternLoadoutOption
    {
        public string PatternId;
        public string DisplayLabel;
    }
    #endregion
}
