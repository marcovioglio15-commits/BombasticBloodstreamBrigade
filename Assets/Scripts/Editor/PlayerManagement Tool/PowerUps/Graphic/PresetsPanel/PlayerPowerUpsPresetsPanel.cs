using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// UI panel for creating, editing, duplicating and deleting Player Power-Ups presets.
/// Used as side panel content by PlayerMasterPresetsPanel when Power-Ups section is opened.
/// </summary>
public sealed class PlayerPowerUpsPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.PlayerManagement.PowerUps.ActiveSection";
    private const string DetailsScrollOffsetStateKey = "NashCore.PlayerManagement.PowerUps.DetailsScroll";
    private const string SelectedPresetPathStateKey = "NashCore.PlayerManagement.PowerUps.SelectedPreset";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerPowerUpsPreset> filteredPresets = new List<PlayerPowerUpsPreset>();
    internal readonly Dictionary<string, bool> moduleDefinitionFoldoutStateByKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, bool> activePowerUpFoldoutStateByKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, bool> passivePowerUpFoldoutStateByKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private readonly PlayerPowerUpsPresetLibrary library;
    internal readonly InputActionAsset inputAsset;

    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;
    internal VisualElement sectionButtonsRoot;
    internal VisualElement sectionContentRoot;

    internal PlayerPowerUpsPreset selectedPreset;
    internal SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
    internal string moduleIdFilterText = string.Empty;
    internal string moduleDisplayNameFilterText = string.Empty;
    internal string activePowerUpIdFilterText = string.Empty;
    internal string activePowerUpDisplayNameFilterText = string.Empty;
    internal string passivePowerUpIdFilterText = string.Empty;
    internal string passivePowerUpDisplayNameFilterText = string.Empty;
    private bool activeSectionRebuildScheduled;
    private bool presetSerializedChangeRefreshScheduled;
    private int presetSerializedChangeHandlingSuspendCounter;
    private readonly Queue<Action> deferredStructuralActions = new Queue<Action>();
    private bool deferredStructuralActionsScheduled;
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

    #region Methods

    #region Public Methods
    public PlayerPowerUpsPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();
        inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        BuildUI();
        RefreshPresetList();
    }

    public void SelectPresetFromExternal(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        RefreshPresetList();

        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex < 0)
            return;

        // External sync: skip detail rebuild when the preset is already active so dropdowns/scroll stay.
        if (selectedPreset == preset && presetSerializedObject != null && presetSerializedObject.targetObject == preset)
        {
            if (listView != null && listView.selectedIndex != presetIndex)
                listView.SetSelectionWithoutNotify(new int[] { presetIndex });

            presetSerializedObject.UpdateIfRequiredOrScript();
            return;
        }

        if (listView == null)
        {
            SelectPreset(preset);
            return;
        }

        if (listView.selectedIndex != presetIndex)
        {
            listView.SetSelection(presetIndex);
            return;
        }

        SelectPreset(preset);
    }

    public void RefreshFromSessionChange()
    {
        PlayerPowerUpsPreset previouslySelectedPreset = selectedPreset;
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

    #region UI
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        VisualElement leftPane = BuildLeftPane();
        VisualElement rightPane = BuildRightPanel();

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

    private VisualElement BuildRightPanel()
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

    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            PlayerPowerUpsPreset preset = label.userData as PlayerPowerUpsPreset;

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

        PlayerPowerUpsPreset preset = filteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }
    #endregion

    #region Preset List
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            PlayerPowerUpsPreset preset = selectedObject as PlayerPowerUpsPreset;

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
                PlayerPowerUpsPreset preset = library.Presets[index];

                if (preset == null)
                    continue;

                if (PlayerManagementDraftSession.IsAssetStagedForDeletion(preset))
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
            // Prefer this side panel's own persisted preset so close/reopen lands on the same record
            // independently from the master preset's referenced sub-preset.
            PlayerPowerUpsPreset restoredPreset = ManagementToolStateUtility.LoadAsset<PlayerPowerUpsPreset>(SelectedPresetPathStateKey);
            PlayerPowerUpsPreset initialPreset = restoredPreset != null && filteredPresets.Contains(restoredPreset)
                ? restoredPreset
                : filteredPresets[0];
            SelectPreset(initialPreset);

            int initialIndex = filteredPresets.IndexOf(initialPreset);

            if (initialIndex >= 0 && listView != null)
                listView.SetSelectionWithoutNotify(new int[] { initialIndex });
        }
    }

    private bool IsMatchingSearch(PlayerPowerUpsPreset preset, string searchText)
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
        PlayerPowerUpsPreset newPreset = PlayerPowerUpsPresetLibraryUtility.CreatePresetAsset("PlayerPowerUpsPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Power Ups Preset Asset");
        Undo.RecordObject(library, "Add Power Ups Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        if (searchField != null)
            searchField.SetValueWithoutNotify(string.Empty);

        RefreshPresetList();
        SelectPresetInList(newPreset);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        PlayerPowerUpsPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
            return;

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = PlayerManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "PlayerPowerUpsPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Power Ups Preset Asset");
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerializedObject = new SerializedObject(duplicatedPreset);
        SerializedProperty presetIdProperty = duplicatedSerializedObject.FindProperty("presetId");
        SerializedProperty presetNameProperty = duplicatedSerializedObject.FindProperty("presetName");

        if (presetIdProperty != null)
            presetIdProperty.stringValue = Guid.NewGuid().ToString("N");

        if (presetNameProperty != null)
            presetNameProperty.stringValue = finalName;

        duplicatedSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Power Ups Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        if (searchField != null)
            searchField.SetValueWithoutNotify(string.Empty);

        RefreshPresetList();
        SelectPresetInList(duplicatedPreset);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Power Ups Preset", "Delete the selected power ups preset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(library, "Delete Power Ups Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }

    internal void RenamePreset(PlayerPowerUpsPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

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

    private void ShowRenamePopup(VisualElement anchor, PlayerPowerUpsPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect, "Rename Power Ups Preset", preset.PresetName, newName => RenamePreset(preset, newName));
    }
    #endregion

    #region Details
    private void SelectPreset(PlayerPowerUpsPreset preset)
    {
        selectedPreset = preset;
        // Persist this side panel's own selection so close/reopen lands on the same preset
        // independently from the master preset that drove the previous workflow.
        ManagementToolStateUtility.SaveAssetPath(SelectedPresetPathStateKey, preset);
        PlayerManagementSelectionContext.SetActivePowerUpsPreset(selectedPreset);
        sectionButtonsRoot = null;
        sectionContentRoot = null;

        if (detailsRoot == null)
            return;

        PlayerManagementFoldoutStateUtility.CaptureFoldoutStates(detailsRoot);
        detailsRoot.Clear();

        if (selectedPreset == null)
        {
            Label emptyLabel = new Label("Select or create a power ups preset to edit.");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(emptyLabel);
            return;
        }

        if (selectedPreset.EnsureDefaultModularSetup())
        {
            EditorUtility.SetDirty(selectedPreset);
            AssetDatabase.SaveAssetIfDirty(selectedPreset);
        }

        presetSerializedObject = new SerializedObject(selectedPreset);

        sectionButtonsRoot = BuildSectionButtons();
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexDirection = FlexDirection.Column;
        sectionContentRoot.style.flexGrow = 1f;

        detailsRoot.Add(sectionButtonsRoot);
        detailsRoot.Add(sectionContentRoot);
        sectionContentRoot.TrackSerializedObjectValue(presetSerializedObject, OnPresetSerializedChanged);

        BuildActiveSection();
    }

    private void SelectPresetInList(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex >= 0 && listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(preset);
    }

    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddSectionButton(buttonsRoot, SectionType.Tiers, "Drop Pools & Tiers");
        AddSectionButton(buttonsRoot, SectionType.ModulesManagement, "Modules Management");
        AddSectionButton(buttonsRoot, SectionType.ActivePowerUps, "Active Power Ups");
        AddSectionButton(buttonsRoot, SectionType.PassivePowerUps, "Passive Power Ups");
        AddSectionButton(buttonsRoot, SectionType.LoadoutInput, "Loadout & Inputs");

        return buttonsRoot;
    }

    private void AddSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
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
        BuildActiveSection();
    }

    internal void BuildActiveSection()
    {
        if (sectionContentRoot == null)
            return;

        PlayerManagementFoldoutStateUtility.CaptureFoldoutStates(sectionContentRoot);
        sectionContentRoot.Clear();

        switch (activeSection)
        {
            case SectionType.Metadata:
                PlayerPowerUpsPresetsPanelMetadataUtility.BuildMetadataSection(this);
                return;
            case SectionType.Tiers:
                PlayerPowerUpsPresetsPanelMetadataUtility.BuildDropPoolsAndTiersSection(this);
                return;
            case SectionType.ModulesManagement:
                PlayerPowerUpsPresetsPanelModulesUtility.BuildModulesManagementSection(this);
                return;
            case SectionType.ActivePowerUps:
                PlayerPowerUpsPresetsPanelEntriesUtility.BuildActivePowerUpsSection(this);
                return;
            case SectionType.PassivePowerUps:
                PlayerPowerUpsPresetsPanelEntriesUtility.BuildPassivePowerUpsSection(this);
                return;
            case SectionType.LoadoutInput:
                PlayerPowerUpsPresetsPanelLoadoutUtility.BuildLoadoutInputSection(this);
                return;
        }
    }

    internal void ScheduleActiveSectionRebuild()
    {
        if (sectionContentRoot == null)
        {
            BuildActiveSection();
            return;
        }

        if (activeSectionRebuildScheduled)
            return;

        activeSectionRebuildScheduled = true;
        sectionContentRoot.schedule.Execute(() =>
        {
            activeSectionRebuildScheduled = false;
            BuildActiveSection();
        });
    }

    internal void ScheduleDeferredStructuralAction(Action action)
    {
        if (action == null)
            return;

        deferredStructuralActions.Enqueue(action);

        if (deferredStructuralActionsScheduled)
            return;

        deferredStructuralActionsScheduled = true;
        VisualElement scheduleTarget = sectionContentRoot ?? root;

        if (scheduleTarget == null)
        {
            PrepareDeferredStructuralActions();
            return;
        }

        scheduleTarget.schedule.Execute(PrepareDeferredStructuralActions);
    }

    internal void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        if (presetSerializedObject == null)
            return;

        SerializedProperty presetIdProperty = presetSerializedObject.FindProperty("presetId");

        if (presetIdProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Power Ups Preset ID");
        presetSerializedObject.Update();
        presetIdProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    internal void OnPresetSerializedChanged(SerializedObject changedObject)
    {
        if (changedObject == null)
            return;

        if (presetSerializedChangeHandlingSuspendCounter > 0)
            return;

        PlayerManagementDraftSession.MarkDirty();
        SchedulePresetSerializedChangeRefresh();
    }

    private string GetPresetDisplayName(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string currentName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string currentVersion = preset.Version;

        if (string.IsNullOrWhiteSpace(currentVersion))
            return currentName;

        return currentName + " v. " + currentVersion;
    }

    /// <summary>
    /// Detaches the active section UI before structural preset mutations so nested property trackers cannot react to moving array paths.
    /// none
    /// </summary>
    private void PrepareDeferredStructuralActions()
    {
        if (deferredStructuralActions.Count <= 0)
        {
            deferredStructuralActionsScheduled = false;
            return;
        }

        SuspendPresetSerializedChangeHandling();

        if (sectionContentRoot != null)
        {
            PlayerManagementFoldoutStateUtility.CaptureFoldoutStates(sectionContentRoot);
            sectionContentRoot.SetEnabled(false);
            sectionContentRoot.Clear();
        }

        VisualElement scheduleTarget = sectionContentRoot ?? root;

        if (scheduleTarget == null)
        {
            ProcessDeferredStructuralActions();
            return;
        }

        scheduleTarget.schedule.Execute(ProcessDeferredStructuralActions);
    }

    /// <summary>
    /// Executes queued structural actions after the current section UI has been torn down, then rebuilds dependent views once.
    /// none
    /// </summary>
    private void ProcessDeferredStructuralActions()
    {
        deferredStructuralActionsScheduled = false;
        bool processedStructuralAction = false;

        try
        {
            while (deferredStructuralActions.Count > 0)
            {
                Action deferredAction = deferredStructuralActions.Dequeue();

                if (deferredAction == null)
                    continue;

                processedStructuralAction = true;
                deferredAction.Invoke();
            }
        }
        finally
        {
            if (sectionContentRoot != null)
                sectionContentRoot.SetEnabled(true);

            ResumePresetSerializedChangeHandling();

            if (processedStructuralAction)
            {
                ScheduleActiveSectionRebuild();
                SchedulePresetSerializedChangeRefresh();
            }
        }
    }

    /// <summary>
    /// Queues a single preset-level refresh for the list pane and cross-panel dependents even if many serialized callbacks arrive in the same frame.
    /// none
    /// </summary>
    private void SchedulePresetSerializedChangeRefresh()
    {
        if (presetSerializedChangeRefreshScheduled)
            return;

        presetSerializedChangeRefreshScheduled = true;
        VisualElement scheduleTarget = sectionContentRoot ?? root;

        if (scheduleTarget == null)
        {
            RefreshPresetSerializedChangeDependents();
            return;
        }

        scheduleTarget.schedule.Execute(RefreshPresetSerializedChangeDependents).StartingIn(120);
    }

    /// <summary>
    /// Refreshes the left preset list and dependent panels after the current batch of serialized mutations settles.
    /// </summary>
    private void RefreshPresetSerializedChangeDependents()
    {
        presetSerializedChangeRefreshScheduled = false;

        if (selectedPreset == null)
            return;

        if (activeSection == SectionType.Metadata)
            RefreshPresetList();

        PlayerManagementSelectionContext.NotifyPowerUpsPresetContentChanged();
    }

    /// <summary>
    /// Temporarily suppresses preset change callbacks while the section is being structurally rebuilt.
    /// none
    /// </summary>
    private void SuspendPresetSerializedChangeHandling()
    {
        presetSerializedChangeHandlingSuspendCounter += 1;
    }

    /// <summary>
    /// Re-enables preset change callbacks after a structural mutation batch completes.
    /// none
    /// </summary>
    private void ResumePresetSerializedChangeHandling()
    {
        if (presetSerializedChangeHandlingSuspendCounter <= 0)
            return;

        presetSerializedChangeHandlingSuspendCounter -= 1;
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        Tiers = 1,
        ModulesManagement = 2,
        ActivePowerUps = 3,
        PassivePowerUps = 4,
        LoadoutInput = 5
    }

    internal struct LoadoutPowerUpOption
    {
        public string PowerUpId;
        public string DisplayLabel;
    }
    #endregion
}
