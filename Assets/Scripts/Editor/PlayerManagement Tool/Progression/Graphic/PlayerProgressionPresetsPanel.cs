using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for managing, creating, editing, and deleting player progression presets.
/// </summary>
public sealed class PlayerProgressionPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.PlayerManagement.Progression.ActiveSection";
    private static readonly HashSet<string> MilestonesExcludedRootPropertyNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "presetId",
        "presetName",
        "description",
        "version",
        "scalableStats",
        "scalingRules",
        "schedules",
        "equippedScheduleId",
        "comboCounter",
        "legacyExperienceRequiredPerLevel",
        "legacyBaseStats"
    };
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerProgressionPreset> filteredPresets = new List<PlayerProgressionPreset>();
    private readonly PlayerProgressionPresetLibrary library;

    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement sectionButtonsRoot;
    internal VisualElement sectionContentRoot;

    internal PlayerProgressionPreset selectedPreset;
    internal SerializedObject presetSerializedObject;
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
    #endregion

    #region Constructors
    public PlayerProgressionPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = PlayerProgressionPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        RegisterReactiveCallbacks();
        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void SelectPresetFromExternal(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        RefreshPresetList();

        int index = filteredPresets.IndexOf(preset);

        if (index < 0)
        {
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

    public void RefreshFromSessionChange()
    {
        PlayerProgressionPreset previouslySelectedPreset = selectedPreset;
        RefreshPresetList();

        if (previouslySelectedPreset == null)
        {
            return;
        }

        int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
        {
            return;
        }

        if (listView != null)
        {
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });
        }

        SelectPreset(previouslySelectedPreset);
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

    private void RegisterReactiveCallbacks()
    {
        root.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged += RefreshMilestonesIfVisible;
            PlayerManagementSelectionContext.PowerUpsPresetContentChanged += RefreshMilestonesIfVisible;
        });

        root.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged -= RefreshMilestonesIfVisible;
            PlayerManagementSelectionContext.PowerUpsPresetContentChanged -= RefreshMilestonesIfVisible;
        });
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
            PlayerProgressionPreset preset = label.userData as PlayerProgressionPreset;

            if (preset == null)
            {
                return;
            }

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
        {
            return;
        }

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        PlayerProgressionPreset preset = filteredPresets[index];

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
            PlayerProgressionPreset preset = item as PlayerProgressionPreset;

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
                PlayerProgressionPreset preset = library.Presets[index];

                if (preset == null)
                {
                    continue;
                }

                if (PlayerManagementDraftSession.IsAssetStagedForDeletion(preset))
                {
                    continue;
                }

                if (IsMatchingSearch(preset, searchText))
                {
                    filteredPresets.Add(preset);
                }
            }
        }

        if (listView != null)
        {
            listView.Rebuild();
        }

        if (filteredPresets.Count == 0)
        {
            SelectPreset(null);
            return;
        }

        if (selectedPreset == null || !filteredPresets.Contains(selectedPreset))
        {
            SelectPreset(filteredPresets[0]);

            if (listView != null)
            {
                listView.SetSelectionWithoutNotify(new int[] { 0 });
            }
        }
    }

    private bool IsMatchingSearch(PlayerProgressionPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string currentPresetName = preset.PresetName;

        if (string.IsNullOrWhiteSpace(currentPresetName))
        {
            return false;
        }

        return currentPresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Preset Actions
    private void CreatePreset()
    {
        PlayerProgressionPreset newPreset = PlayerProgressionPresetLibraryUtility.CreatePresetAsset("PlayerProgressionPreset");

        if (newPreset == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Progression Preset Asset");
        Undo.RecordObject(library, "Add Progression Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(newPreset);

        int index = filteredPresets.IndexOf(newPreset);

        if (index >= 0)
        {
            listView.SetSelection(index);
        }
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        PlayerProgressionPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerProgressionPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return;
        }

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
        {
            return;
        }

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = PlayerManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
        {
            duplicateBaseName = "PlayerProgressionPreset Copy";
        }

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Progression Preset Asset");
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
        {
            idProperty.stringValue = Guid.NewGuid().ToString("N");
        }

        if (nameProperty != null)
        {
            nameProperty.stringValue = finalName;
        }

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Progression Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = filteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0)
        {
            listView.SetSelection(index);
        }
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog("Delete Progression Preset", "Delete the selected progression preset asset?", "Delete", "Cancel");

        if (!confirmed)
        {
            return;
        }

        Undo.RecordObject(library, "Delete Progression Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(PlayerProgressionPreset preset)
    {
        selectedPreset = preset;
        PlayerManagementSelectionContext.SetActiveProgressionPreset(selectedPreset);
        detailsRoot.Clear();
        sectionButtonsRoot = null;
        sectionContentRoot = null;

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create a progression preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(label);
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);
        sectionButtonsRoot = BuildSectionButtons();
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexGrow = 1f;

        detailsRoot.Add(sectionButtonsRoot);
        detailsRoot.Add(sectionContentRoot);

        BuildActiveSection();
    }

    private void BuildMetadataSection()
    {
        PlayerProgressionPresetsPanelSectionsUtility.BuildMetadataSection(this);
    }

    private void BuildScalableStatsSection()
    {
        PlayerProgressionPresetsPanelSectionsUtility.BuildScalableStatsSection(this);
    }

    private void BuildMilestonesSection()
    {
        PlayerProgressionPresetsPanelSectionsUtility.BuildMilestonesSection(this, MilestonesExcludedRootPropertyNames);
    }

    private void BuildSchedulesSection()
    {
        PlayerProgressionPresetsPanelSectionsUtility.BuildSchedulesSection(this);
    }

    private void BuildComboCounterSection()
    {
        PlayerProgressionPresetsPanelSectionsUtility.BuildComboCounterSection(this);
    }

    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddSectionButton(buttonsRoot, SectionType.Milestones, "Milestones");
        AddSectionButton(buttonsRoot, SectionType.Schedules, "Schedules");
        AddSectionButton(buttonsRoot, SectionType.ComboCounter, "Combo Counter");
        AddSectionButton(buttonsRoot, SectionType.ScalableStats, "Scalable Stats");
        return buttonsRoot;
    }

    private void AddSectionButton(VisualElement parent, SectionType sectionType, string label)
    {
        Button button = new Button(() => SetActiveSection(sectionType));
        button.text = label;
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Sets active details section and persists the selection for reopen.
    /// Called by section buttons in progression preset details.
    /// Takes in the target section enum.
    /// </summary>
    /// <param name="sectionType">Section to activate.</param>
    private void SetActiveSection(SectionType sectionType)
    {
        // Persist selected section and rebuild detail content.
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
        BuildActiveSection();
    }

    private void BuildActiveSection()
    {
        if (sectionContentRoot == null)
            return;

        sectionContentRoot.Clear();

        switch (activeSection)
        {
            case SectionType.Metadata:
                BuildMetadataSection();
                return;
            case SectionType.Milestones:
                BuildMilestonesSection();
                return;
            case SectionType.Schedules:
                BuildSchedulesSection();
                return;
            case SectionType.ComboCounter:
                BuildComboCounterSection();
                return;
            case SectionType.ScalableStats:
                BuildScalableStatsSection();
                return;
        }
    }

    private void RefreshMilestonesIfVisible()
    {
        if (selectedPreset == null)
            return;

        if (activeSection != SectionType.Milestones)
            return;

        BuildActiveSection();
    }

    internal VisualElement CreateSectionContainer(string sectionTitle)
    {
        Foldout container = ManagementToolFoldoutStateUtility.CreateFoldout(sectionTitle,
                                                                            "NashCore.PlayerManagement.Progression.Section." + sectionTitle,
                                                                            true);
        container.style.marginTop = 8f;

        if (sectionContentRoot != null)
            sectionContentRoot.Add(container);

        return container;
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
        {
            return;
        }

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
        {
            return;
        }

        Undo.RecordObject(selectedPreset, "Regenerate Progression Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
    }

    internal void RenamePreset(PlayerProgressionPreset preset, string newName)
    {
        if (preset == null)
        {
            return;
        }

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedPreset.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            serializedPreset.Update();
            presetNameProperty.stringValue = normalizedName;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, PlayerProgressionPreset preset)
    {
        if (anchor == null || preset == null)
        {
            return;
        }

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect, "Rename Progression Preset", preset.PresetName, newName => RenamePreset(preset, newName));
    }

    private string GetPresetDisplayName(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return "<Missing Preset>";
        }

        string currentPresetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string currentVersion = preset.Version;

        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return currentPresetName;
        }

        return currentPresetName + " v. " + currentVersion;
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        Milestones = 1,
        Schedules = 2,
        ComboCounter = 3,
        ScalableStats = 4
    }
    #endregion
}
