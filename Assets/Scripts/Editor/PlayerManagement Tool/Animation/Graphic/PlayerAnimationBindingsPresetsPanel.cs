using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI panel for creating and editing PlayerAnimationBindingsPreset assets.
/// Includes import/remap tools to transfer setups across different character models.
/// </summary>
public sealed class PlayerAnimationBindingsPresetsPanel
{
    #region Constants
    private const string DetailsScrollOffsetStateKey = "NashCore.PlayerManagement.AnimationBindings.DetailsScroll";
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerAnimationBindingsPreset> filteredPresets = new List<PlayerAnimationBindingsPreset>();

    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;

    private PlayerAnimationBindingsPreset selectedPreset;
    private SerializedObject selectedPresetSerializedObject;

    private ObjectField sourcePresetField;
    private ObjectField targetClipsFolderField;
    private Toggle recursiveSearchToggle;
    private Toggle overwriteExistingSlotsToggle;
    private Label importStatusLabel;
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

    #region Lifecycle
    public PlayerAnimationBindingsPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Public Methods
    public void SelectPresetFromExternal(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return;

        RefreshPresetList();
        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex < 0)
            return;

        // External sync: when the preset is already selected keep the detail subtree intact and only
        // fix listView selection — avoids destroying open dropdowns/scroll on Apply syncs.
        if (selectedPreset == preset && selectedPresetSerializedObject != null && selectedPresetSerializedObject.targetObject == preset)
        {
            if (listView != null && listView.selectedIndex != presetIndex)
                listView.SetSelectionWithoutNotify(new int[] { presetIndex });

            selectedPresetSerializedObject.UpdateIfRequiredOrScript();
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
        PlayerAnimationBindingsPreset previouslySelectedPreset = selectedPreset;
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
        if (selectedPreset == previouslySelectedPreset && selectedPresetSerializedObject != null && selectedPresetSerializedObject.targetObject == previouslySelectedPreset)
            selectedPresetSerializedObject.UpdateIfRequiredOrScript();
        else
            SelectPreset(previouslySelectedPreset);
    }
    #endregion

    #region UI
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

    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            PlayerAnimationBindingsPreset preset = label.userData as PlayerAnimationBindingsPreset;

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

        PlayerAnimationBindingsPreset preset = filteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = PlayerAnimationBindingsPanelUtility.GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }
    #endregion

    #region Preset List
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            PlayerAnimationBindingsPreset preset = selectedObject as PlayerAnimationBindingsPreset;

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

    private void RefreshPresetList()
    {
        filteredPresets.Clear();
        List<PlayerAnimationBindingsPreset> allPresets = PlayerAnimationBindingsPresetEditorUtility.LoadAllPresets();
        string searchText = searchField != null ? searchField.value : string.Empty;

        for (int presetIndex = 0; presetIndex < allPresets.Count; presetIndex++)
        {
            PlayerAnimationBindingsPreset preset = allPresets[presetIndex];

            if (preset == null)
                continue;

            if (!PlayerAnimationBindingsPanelUtility.MatchesSearch(preset, searchText))
                continue;

            filteredPresets.Add(preset);
        }

        if (listView != null)
            listView.Rebuild();

        if (selectedPreset == null)
        {
            if (filteredPresets.Count > 0 && listView != null)
                listView.SetSelectionWithoutNotify(new int[] { 0 });

            if (filteredPresets.Count > 0)
                SelectPreset(filteredPresets[0]);
            else
                SelectPreset(null);

            return;
        }

        if (!filteredPresets.Contains(selectedPreset))
        {
            SelectPreset(null);
            return;
        }

        if (listView != null)
        {
            int selectedIndex = filteredPresets.IndexOf(selectedPreset);

            if (selectedIndex >= 0)
                listView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }
    }

    #endregion

    #region Selection
    private void SelectPreset(PlayerAnimationBindingsPreset preset)
    {
        selectedPreset = preset;

        if (selectedPreset == null)
        {
            selectedPresetSerializedObject = null;
            RebuildDetails();
            return;
        }

        selectedPresetSerializedObject = new SerializedObject(selectedPreset);
        RebuildDetails();
    }

    private void RebuildDetails()
    {
        if (detailsRoot == null)
            return;

        detailsRoot.Clear();

        if (selectedPreset == null || selectedPresetSerializedObject == null)
        {
            Label emptyLabel = new Label("Select an Animation Bindings preset to edit.");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(emptyLabel);
            return;
        }

        selectedPresetSerializedObject.Update();

        BuildMetadataSection();
        BuildAnimatorSetupSection();
        BuildParametersSection();
        BuildProceduralSection();
        BuildClipSlotsSection();
        BuildImportSection();

        selectedPresetSerializedObject.ApplyModifiedProperties();
    }

    private void BuildMetadataSection()
    {
        VisualElement section = PlayerAnimationBindingsPanelUtility.CreateSection(detailsRoot, "Metadata");
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "presetName", "Preset Name"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "description", "Description"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "version", "Version"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreateReadOnlyText("Preset Id", selectedPreset.PresetId));
    }

    private void BuildAnimatorSetupSection()
    {
        VisualElement section = PlayerAnimationBindingsPanelUtility.CreateSection(detailsRoot, "Animator Setup");
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "animatorController", "Animator Controller"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyAvatarMask", "Upper Body Mask"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "lowerBodyAvatarMask", "Lower Body Mask"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "disableRootMotion", "Disable Root Motion"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "useFloatDamping", "Use Float Damping"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "floatDampTime", "Float Damp Time"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "movingSpeedThreshold", "Moving Speed Threshold"));
    }

    private void BuildParametersSection()
    {
        VisualElement section = PlayerAnimationBindingsPanelUtility.CreateSection(detailsRoot, "Animator Parameters");
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveXParameter", "MoveX"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveYParameter", "MoveY"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveSpeedParameter", "MoveSpeed"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "aimXParameter", "AimX"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "aimYParameter", "AimY"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "isMovingParameter", "IsMoving"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "isShootingParameter", "IsShooting"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "isDashingParameter", "IsDashing"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "shotPulseParameter", "ShotPulse Trigger"));
    }

    private void BuildProceduralSection()
    {
        VisualElement section = PlayerAnimationBindingsPanelUtility.CreateSection(detailsRoot, "Procedural Animation");
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralRecoilEnabled", "Enable Recoil"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralRecoilKick", "Recoil Kick"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralRecoilRecoveryPerSecond", "Recoil Recovery / Sec"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralRecoilParameter", "Recoil Parameter"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralAimWeightEnabled", "Enable Aim Weight"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralAimWeightSmoothing", "Aim Weight Smoothing"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralAimWeightParameter", "Aim Weight Parameter"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralLeanEnabled", "Enable Lean"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralLeanSmoothing", "Lean Smoothing"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "proceduralLeanParameter", "Lean Parameter"));
    }

    private void BuildClipSlotsSection()
    {
        VisualElement section = PlayerAnimationBindingsPanelUtility.CreateSection(detailsRoot, "Clip Slots");
        HelpBox upperBodyInfoBox = new HelpBox("Switch Weapon and Trigger Hold Charge payloads select the weapon, charge, and release slots below through fully scalable enum fields. UpperBody.ST_Upper_Shoot must use one of these action clips as its authored override anchor. Empty optional shoot slots fall back to Player Visual Preset > Weapon Visuals > Default Shoot Animation Clip.",
                                               HelpBoxMessageType.Info);
        section.Add(upperBodyInfoBox);
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "idleClip", "Idle"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveForwardClip", "Move Forward"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveBackwardClip", "Move Backward"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveLeftClip", "Move Left"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "moveRightClip", "Move Right"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "aimForwardClip", "Aim Forward"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "aimBackwardClip", "Aim Backward"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "aimLeftClip", "Aim Left"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "aimRightClip", "Aim Right"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.cannonShootClip", "Shoot Cannon"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.gatlingShootClip", "Shoot Gatling"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.railgunShootClip", "Shoot Railgun"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.primaryChargeClip", "Charge Primary"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.secondaryChargeClip", "Charge Secondary"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.primaryReleaseClip", "Release Primary"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "upperBodyActionClips.secondaryReleaseClip", "Release Secondary"));
        section.Add(PlayerAnimationBindingsPanelUtility.CreatePropertyField(selectedPresetSerializedObject, "dashClip", "Dash"));
    }

    private void BuildImportSection()
    {
        VisualElement section = PlayerAnimationBindingsPanelUtility.CreateSection(detailsRoot, "Import / Remap");

        sourcePresetField = new ObjectField("Source Preset");
        sourcePresetField.objectType = typeof(PlayerAnimationBindingsPreset);
        sourcePresetField.allowSceneObjects = false;
        sourcePresetField.tooltip = "Preset source used to import animator setup, parameters and optional clip slots.";
        section.Add(sourcePresetField);

        targetClipsFolderField = new ObjectField("Target Clips Folder");
        targetClipsFolderField.objectType = typeof(DefaultAsset);
        targetClipsFolderField.allowSceneObjects = false;
        targetClipsFolderField.tooltip = "Root folder scanned for clip remapping by semantic slot keywords.";
        section.Add(targetClipsFolderField);

        recursiveSearchToggle = new Toggle("Search Subfolders");
        recursiveSearchToggle.value = true;
        recursiveSearchToggle.tooltip = "Include all nested folders when searching clips for auto-remap.";
        section.Add(recursiveSearchToggle);

        overwriteExistingSlotsToggle = new Toggle("Overwrite Existing Slots");
        overwriteExistingSlotsToggle.value = false;
        overwriteExistingSlotsToggle.tooltip = "Replace already assigned clip slots when auto-remapping.";
        section.Add(overwriteExistingSlotsToggle);

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.flexWrap = Wrap.Wrap;
        buttonsRow.style.marginTop = 4f;

        Button importSettingsButton = new Button(ImportSettingsOnly);
        importSettingsButton.text = "Import Settings";
        importSettingsButton.tooltip = "Copy animator setup and parameter names from the source preset.";
        buttonsRow.Add(importSettingsButton);

        Button importAndCopyClipsButton = new Button(ImportSettingsAndCopyClips);
        importAndCopyClipsButton.text = "Import + Copy Clips";
        importAndCopyClipsButton.style.marginLeft = 4f;
        importAndCopyClipsButton.tooltip = "Copy setup, parameters and current clip slot assignments from source preset.";
        buttonsRow.Add(importAndCopyClipsButton);

        Button importAndRemapButton = new Button(ImportSettingsAndRemapClips);
        importAndRemapButton.text = "Import + Remap Clips";
        importAndRemapButton.style.marginLeft = 4f;
        importAndRemapButton.tooltip = "Copy setup/parameters from source and remap clip slots using folder clip names.";
        buttonsRow.Add(importAndRemapButton);

        Button remapOnlyButton = new Button(AutoRemapOnly);
        remapOnlyButton.text = "Auto Remap";
        remapOnlyButton.style.marginLeft = 4f;
        remapOnlyButton.tooltip = "Remap clip slots using selected folder without importing other settings.";
        buttonsRow.Add(remapOnlyButton);

        section.Add(buttonsRow);

        importStatusLabel = new Label();
        importStatusLabel.style.marginTop = 4f;
        importStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        section.Add(importStatusLabel);
    }

    #endregion

    #region Import Actions
    private void ImportSettingsOnly()
    {
        if (selectedPreset == null)
            return;

        PlayerAnimationBindingsPreset sourcePreset = sourcePresetField != null ? sourcePresetField.value as PlayerAnimationBindingsPreset : null;

        if (sourcePreset == null)
        {
            SetImportStatus("Select a source preset first.");
            return;
        }

        PlayerAnimationBindingsPresetEditorUtility.ImportSettingsFromSource(selectedPreset, sourcePreset, false);
        SetImportStatus("Settings imported.");
        RebuildDetails();
    }

    private void ImportSettingsAndCopyClips()
    {
        if (selectedPreset == null)
            return;

        PlayerAnimationBindingsPreset sourcePreset = sourcePresetField != null ? sourcePresetField.value as PlayerAnimationBindingsPreset : null;

        if (sourcePreset == null)
        {
            SetImportStatus("Select a source preset first.");
            return;
        }

        PlayerAnimationBindingsPresetEditorUtility.ImportSettingsFromSource(selectedPreset, sourcePreset, true);
        SetImportStatus("Settings and clip slots imported.");
        RebuildDetails();
    }

    private void ImportSettingsAndRemapClips()
    {
        if (selectedPreset == null)
            return;

        PlayerAnimationBindingsPreset sourcePreset = sourcePresetField != null ? sourcePresetField.value as PlayerAnimationBindingsPreset : null;

        if (sourcePreset == null)
        {
            SetImportStatus("Select a source preset first.");
            return;
        }

        DefaultAsset clipsFolder = targetClipsFolderField != null ? targetClipsFolderField.value as DefaultAsset : null;

        if (clipsFolder == null)
        {
            SetImportStatus("Select a target clips folder first.");
            return;
        }

        PlayerAnimationBindingsPresetEditorUtility.ImportSettingsFromSource(selectedPreset, sourcePreset, false);
        int mappedCount = AutoRemapClips(clipsFolder);
        SetImportStatus(string.Format("Settings imported. Remapped slots: {0}", mappedCount));
        RebuildDetails();
    }

    private void AutoRemapOnly()
    {
        if (selectedPreset == null)
            return;

        DefaultAsset clipsFolder = targetClipsFolderField != null ? targetClipsFolderField.value as DefaultAsset : null;

        if (clipsFolder == null)
        {
            SetImportStatus("Select a target clips folder first.");
            return;
        }

        int mappedCount = AutoRemapClips(clipsFolder);
        SetImportStatus(string.Format("Remapped slots: {0}", mappedCount));
        RebuildDetails();
    }

    private int AutoRemapClips(DefaultAsset clipsFolder)
    {
        bool recursive = recursiveSearchToggle == null || recursiveSearchToggle.value;
        bool overwrite = overwriteExistingSlotsToggle != null && overwriteExistingSlotsToggle.value;
        List<AnimationClip> clips = PlayerAnimationBindingsPresetEditorUtility.LoadClipsFromFolder(clipsFolder, recursive);

        if (clips.Count == 0)
            return 0;

        return PlayerAnimationBindingsPresetEditorUtility.AutoMapClipSlots(selectedPreset, clips, overwrite);
    }

    private void SetImportStatus(string message)
    {
        if (importStatusLabel != null)
            importStatusLabel.text = message;
    }
    #endregion

    #region Preset Commands
    private void CreatePreset()
    {
        PlayerAnimationBindingsPreset newPreset = PlayerAnimationBindingsPresetEditorUtility.CreatePresetAsset("PlayerAnimationBindingsPreset");

        if (newPreset == null)
            return;

        RefreshPresetList();
        SelectPresetFromExternal(newPreset);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return;

        PlayerAnimationBindingsPreset duplicatedPreset = PlayerAnimationBindingsPresetEditorUtility.DuplicatePresetAsset(preset);

        if (duplicatedPreset == null)
            return;

        RefreshPresetList();
        SelectPresetFromExternal(duplicatedPreset);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return;

        bool deleted = PlayerAnimationBindingsPresetEditorUtility.DeletePresetAsset(preset);

        if (!deleted)
            return;

        if (selectedPreset == preset)
            selectedPreset = null;

        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, PlayerAnimationBindingsPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect,
                               "Rename Animation Preset",
                               preset.PresetName,
                               newName =>
                               {
                                   PlayerAnimationBindingsPanelUtility.RenamePreset(preset, newName);
                                   RefreshPresetList();
                               });
    }

    #endregion
}
