using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Procedural Level preset browser and routes every asset mutation through the draft session.
/// </summary>
internal static class GameProceduralLevelPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the responsive preset browser and detail area owned by the panel.
    /// </summary>
    /// <param name="panel">Panel receiving browser controls and detail roots.</param>
    /// <param name="leftPaneWidth">Initial fixed width of the browser pane.</param>
    /// <returns>Split view containing browser and details.</returns>
    public static VisualElement BuildMainContent(GameProceduralLevelPresetsPanel panel, float leftPaneWidth)
    {
        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(leftPaneWidth);
        splitView.Add(BuildLeftPane(panel));
        splitView.Add(BuildRightPane(panel));
        return splitView;
    }

    /// <summary>
    /// Rebuilds the filtered browser collection and restores the last live selection.
    /// </summary>
    /// <param name="panel">Panel whose library and browser state are refreshed.</param>
    public static void RefreshPresetList(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null)
            return;

        panel.FilteredPresets.Clear();
        string searchText = panel.PresetSearchField != null ? panel.PresetSearchField.value : string.Empty;

        if (panel.Library != null)
            AddMatchingPresets(panel, searchText);

        if (panel.PresetListView != null)
            panel.PresetListView.Rebuild();

        if (panel.FilteredPresets.Count == 0)
        {
            panel.SelectPreset(null);
            return;
        }

        if (panel.SelectedPreset != null && panel.FilteredPresets.Contains(panel.SelectedPreset))
            return;

        GameProceduralLevelPreset restoredPreset = ManagementToolStateUtility.LoadAsset<GameProceduralLevelPreset>(GameProceduralLevelPresetsPanel.SelectedPresetPathStateKey);
        panel.SelectPreset(restoredPreset != null && panel.FilteredPresets.Contains(restoredPreset)
            ? restoredPreset
            : panel.FilteredPresets[0]);
    }

    /// <summary>
    /// Creates, registers and selects a new Procedural Level preset asset.
    /// </summary>
    /// <param name="panel">Panel receiving the newly created preset.</param>
    public static void CreatePreset(GameProceduralLevelPresetsPanel panel)
    {
        if (panel == null || panel.Library == null)
            return;

        GameProceduralLevelPreset newPreset = GameProceduralLevelPresetLibraryUtility.CreatePresetAsset("GameProceduralLevelPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Procedural Level Preset");
        Undo.RecordObject(panel.Library, "Add Procedural Level Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(newPreset);
    }

    /// <summary>
    /// Copies one preset into a new asset and regenerates all technical identities before registration.
    /// </summary>
    /// <param name="panel">Panel receiving the duplicated asset.</param>
    /// <param name="preset">Preset whose serialized configuration is copied.</param>
    public static void DuplicatePreset(GameProceduralLevelPresetsPanel panel, GameProceduralLevelPreset preset)
    {
        if (panel == null || panel.Library == null || preset == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(preset);
        string sourceDirectory = Path.GetDirectoryName(sourcePath);

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(sourceDirectory))
            return;

        string sourceName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateName = GameManagementDraftSession.NormalizeAssetName(sourceName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateName))
            duplicateName = "GameProceduralLevelPreset Copy";

        string requestedPath = Path.Combine(sourceDirectory, duplicateName + ".asset").Replace('\\', '/');
        string duplicatePath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        GameProceduralLevelPreset duplicate = ScriptableObject.CreateInstance<GameProceduralLevelPreset>();
        EditorUtility.CopySerialized(preset, duplicate);
        duplicate.name = Path.GetFileNameWithoutExtension(duplicatePath);
        AssetDatabase.CreateAsset(duplicate, duplicatePath);
        SynchronizeDuplicatedPreset(duplicate);

        Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate Procedural Level Preset");
        Undo.RecordObject(panel.Library, "Register Duplicated Procedural Level Preset");
        panel.Library.AddPreset(duplicate);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(duplicate);
    }

    /// <summary>
    /// Confirms and stages one preset deletion without immediately removing its asset from disk.
    /// </summary>
    /// <param name="panel">Panel whose library reference is updated.</param>
    /// <param name="preset">Preset asset staged for deletion.</param>
    public static void DeletePreset(GameProceduralLevelPresetsPanel panel, GameProceduralLevelPreset preset)
    {
        if (panel == null || panel.Library == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Procedural Level Preset",
                                                     "Delete the selected Procedural Level preset asset when changes are applied?",
                                                     "Delete",
                                                     "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Procedural Level Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.StageDeleteAsset(preset);
        panel.RefreshPresetList();
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds the preset list, search control and draft-aware mutation toolbar.
    /// </summary>
    /// <param name="panel">Panel used by all browser callbacks.</param>
    /// <returns>Configured browser pane.</returns>
    private static VisualElement BuildLeftPane(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement leftPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(leftPane);
        leftPane.Add(BuildToolbar(panel));

        ToolbarSearchField searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Procedural Level presets by display name.";
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        searchField.RegisterValueChangedCallback(evt => panel.RefreshPresetList());
        panel.PresetSearchField = searchField;
        leftPane.Add(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(leftPane, searchField);

        ListView listView = new ListView();
        GameManagementPanelLayoutUtility.ConfigureListView(listView);
        listView.itemsSource = panel.FilteredPresets;
        listView.selectionType = SelectionType.Single;
        listView.makeItem = () => MakePresetItem(panel);
        listView.bindItem = (element, index) => BindPresetItem(panel, element, index);
        listView.selectionChanged += selection => OnPresetSelectionChanged(panel, selection);
        panel.PresetListView = listView;
        leftPane.Add(listView);
        return leftPane;
    }

    /// <summary>
    /// Builds create, duplicate and staged-delete actions for preset assets.
    /// </summary>
    /// <param name="panel">Panel handling each toolbar action.</param>
    /// <returns>Configured wrapping toolbar.</returns>
    private static Toolbar BuildToolbar(GameProceduralLevelPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(panel.CreatePreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new Procedural Level preset in the draft session.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 52f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => panel.DuplicatePreset(panel.SelectedPreset));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected preset with fresh nested technical IDs.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 72f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => panel.DeletePreset(panel.SelectedPreset));
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Stage the selected preset for deletion when Apply is pressed.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 52f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the scrollable selected-preset detail host.
    /// </summary>
    /// <param name="panel">Panel receiving the detail root reference.</param>
    /// <returns>Configured detail pane.</returns>
    private static VisualElement BuildRightPane(GameProceduralLevelPresetsPanel panel)
    {
        VisualElement rightPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(rightPane);

        ScrollView scrollView = new ScrollView();
        GameManagementPanelLayoutUtility.ConfigureDetailsScrollView(scrollView);
        panel.DetailsRoot = scrollView;
        rightPane.Add(scrollView);
        return rightPane;
    }
    #endregion

    #region Browser Methods
    /// <summary>
    /// Adds library assets that are neither staged for deletion nor filtered by search text.
    /// </summary>
    /// <param name="panel">Panel receiving matched presets.</param>
    /// <param name="searchText">Case-insensitive display-name filter.</param>
    private static void AddMatchingPresets(GameProceduralLevelPresetsPanel panel, string searchText)
    {
        for (int index = 0; index < panel.Library.Presets.Count; index++)
        {
            GameProceduralLevelPreset preset = panel.Library.Presets[index];

            if (preset == null || GameManagementDraftSession.IsAssetStagedForDeletion(preset))
                continue;

            if (MatchesSearch(preset, searchText))
                panel.FilteredPresets.Add(preset);
        }
    }

    /// <summary>
    /// Creates a reusable list row with duplicate and staged-delete context actions.
    /// </summary>
    /// <param name="panel">Panel handling context callbacks.</param>
    /// <returns>Reusable preset row label.</returns>
    private static VisualElement MakePresetItem(GameProceduralLevelPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            GameProceduralLevelPreset preset = label.userData as GameProceduralLevelPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => panel.DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => panel.DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one reusable browser row to a filtered preset.
    /// </summary>
    /// <param name="panel">Panel containing the filtered collection.</param>
    /// <param name="element">Reusable row element.</param>
    /// <param name="index">Filtered preset index.</param>
    private static void BindPresetItem(GameProceduralLevelPresetsPanel panel, VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= panel.FilteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        GameProceduralLevelPreset preset = panel.FilteredPresets[index];
        label.userData = preset;
        label.text = panel.GetPresetDisplayName(preset);
        label.tooltip = preset != null ? preset.Description : string.Empty;
    }

    /// <summary>
    /// Updates the selected preset from the single-selection browser event.
    /// </summary>
    /// <param name="panel">Panel receiving the selected preset.</param>
    /// <param name="selection">Current browser selection enumeration.</param>
    private static void OnPresetSelectionChanged(GameProceduralLevelPresetsPanel panel, IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            GameProceduralLevelPreset preset = selectedObject as GameProceduralLevelPreset;

            if (preset == null)
                continue;

            if (panel.SelectedPreset != preset)
                panel.SelectPreset(preset);

            return;
        }

        if (panel.SelectedPreset != null)
            panel.SelectPreset(null);
    }

    /// <summary>
    /// Checks whether one preset display name contains the current search text.
    /// </summary>
    /// <param name="preset">Preset inspected by the filter.</param>
    /// <param name="searchText">Case-insensitive search text.</param>
    /// <returns>True when the preset should remain visible.</returns>
    private static bool MatchesSearch(GameProceduralLevelPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        if (preset == null || string.IsNullOrWhiteSpace(preset.PresetName))
            return false;

        return preset.PresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Synchronizes duplicated preset metadata and regenerates every copied technical identity.
    /// </summary>
    /// <param name="preset">Newly created duplicated preset.</param>
    private static void SynchronizeDuplicatedPreset(GameProceduralLevelPreset preset)
    {
        if (preset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedObject.FindProperty("presetName");
        serializedObject.Update();

        if (presetNameProperty != null)
            presetNameProperty.stringValue = preset.name;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        preset.RegenerateTechnicalIds();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
