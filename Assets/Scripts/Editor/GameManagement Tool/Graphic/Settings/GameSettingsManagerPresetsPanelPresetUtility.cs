using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds Settings Manager preset browser UI and handles preset asset mutations.
/// </summary>
internal static class GameSettingsManagerPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the main split view containing the Settings Manager preset list and details.
    /// </summary>
    /// <param name="panel">Owning panel that stores UI state.</param>
    /// <param name="leftPaneWidth">Fixed browser pane width.</param>
    /// <returns>Main content visual root.</returns>
    public static VisualElement BuildMainContent(GameSettingsManagerPresetsPanel panel, float leftPaneWidth)
    {
        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(leftPaneWidth);
        splitView.Add(BuildLeftPane(panel));
        splitView.Add(BuildRightPane(panel));
        return splitView;
    }

    /// <summary>
    /// Refreshes visible presets from the current library and search filter.
    /// </summary>
    /// <param name="panel">Owning panel with library and list state.</param>
    public static void RefreshPresetList(GameSettingsManagerPresetsPanel panel)
    {
        if (panel == null)
            return;

        panel.FilteredPresets.Clear();
        string searchText = panel.PresetSearchField != null ? panel.PresetSearchField.value : string.Empty;

        if (panel.Library != null)
            AddMatchingPresets(panel, searchText);

        if (panel.PresetListView != null)
            panel.PresetListView.Rebuild();

        if (panel.FilteredPresets.Count <= 0)
        {
            panel.SelectPreset(null);
            return;
        }

        if (panel.SelectedPreset == null || !panel.FilteredPresets.Contains(panel.SelectedPreset))
        {
            GameSettingsManagerPreset restoredPreset = ManagementToolStateUtility.LoadAsset<GameSettingsManagerPreset>(GameSettingsManagerPresetsPanel.SelectedPresetPathStateKey);
            GameSettingsManagerPreset initialPreset = restoredPreset != null && panel.FilteredPresets.Contains(restoredPreset)
                ? restoredPreset
                : panel.FilteredPresets[0];
            panel.SelectPreset(initialPreset);
        }
    }

    /// <summary>
    /// Creates and selects a new Settings Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel that receives the new selection.</param>
    public static void CreatePreset(GameSettingsManagerPresetsPanel panel)
    {
        if (panel == null)
            return;

        GameSettingsManagerPreset newPreset = GameSettingsManagerPresetLibraryUtility.CreatePresetAsset("GameSettingsManagerPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Settings Manager Preset");
        Undo.RecordObject(panel.Library, "Add Settings Manager Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(newPreset);
    }

    /// <summary>
    /// Duplicates one Settings Manager preset asset and registers it.
    /// </summary>
    /// <param name="panel">Owning panel that receives the duplicate selection.</param>
    /// <param name="preset">Source preset to duplicate.</param>
    public static void DuplicatePreset(GameSettingsManagerPresetsPanel panel, GameSettingsManagerPreset preset)
    {
        if (panel == null || preset == null)
            return;

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalPath) || string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string duplicateBaseName = GameManagementDraftSession.NormalizeAssetName(panel.GetPresetDisplayName(preset) + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "GameSettingsManagerPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        GameSettingsManagerPreset duplicatedPreset = ScriptableObject.CreateInstance<GameSettingsManagerPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = Path.GetFileNameWithoutExtension(duplicatedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        SynchronizePresetMetadata(duplicatedPreset, duplicatedPreset.name, true);

        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Settings Manager Preset");
        Undo.RecordObject(panel.Library, "Duplicate Settings Manager Preset");
        panel.Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(duplicatedPreset);
    }

    /// <summary>
    /// Stages one Settings Manager preset for deletion after confirmation.
    /// </summary>
    /// <param name="panel">Owning panel with library state.</param>
    /// <param name="preset">Preset to delete.</param>
    public static void DeletePreset(GameSettingsManagerPresetsPanel panel, GameSettingsManagerPreset preset)
    {
        if (panel == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Settings Manager Preset",
                                                     "Delete the selected Settings Manager preset asset?",
                                                     "Delete",
                                                     "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Settings Manager Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.StageDeleteAsset(preset);
        panel.RefreshPresetList();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the left preset browser pane.
    /// </summary>
    /// <param name="panel">Owning panel used by controls.</param>
    /// <returns>Left pane visual element.</returns>
    private static VisualElement BuildLeftPane(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement leftPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(leftPane);
        leftPane.Add(BuildToolbar(panel));

        ToolbarSearchField searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Settings Manager presets by name.";
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
    /// Builds create, duplicate and delete buttons for Settings Manager presets.
    /// </summary>
    /// <param name="panel">Owning panel used by callbacks.</param>
    /// <returns>Toolbar visual element.</returns>
    private static Toolbar BuildToolbar(GameSettingsManagerPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(panel.CreatePreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new Settings Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 52f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => panel.DuplicatePreset(panel.SelectedPreset));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected Settings Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 72f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => panel.DeletePreset(panel.SelectedPreset));
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Stage the selected Settings Manager preset for deletion.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 52f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the selected preset detail scroll area.
    /// </summary>
    /// <param name="panel">Owning panel receiving the details root.</param>
    /// <returns>Right pane visual element.</returns>
    private static VisualElement BuildRightPane(GameSettingsManagerPresetsPanel panel)
    {
        VisualElement rightPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(rightPane);

        ScrollView detailsRoot = new ScrollView();
        detailsRoot.style.flexGrow = 1f;
        detailsRoot.style.flexShrink = 1f;
        detailsRoot.style.minWidth = 0f;
        panel.DetailsRoot = detailsRoot;
        rightPane.Add(detailsRoot);
        return rightPane;
    }

    /// <summary>
    /// Adds library presets that pass search and staged-delete filters.
    /// </summary>
    /// <param name="panel">Owning panel with filtered output list.</param>
    /// <param name="searchText">Current search text.</param>
    private static void AddMatchingPresets(GameSettingsManagerPresetsPanel panel, string searchText)
    {
        for (int index = 0; index < panel.Library.Presets.Count; index++)
        {
            GameSettingsManagerPreset preset = panel.Library.Presets[index];

            if (preset == null)
                continue;

            if (GameManagementDraftSession.IsAssetStagedForDeletion(preset))
                continue;

            if (MatchesSearch(preset, searchText))
                panel.FilteredPresets.Add(preset);
        }
    }

    /// <summary>
    /// Creates one list row label with context actions.
    /// </summary>
    /// <param name="panel">Owning panel used by context callbacks.</param>
    /// <returns>List row label.</returns>
    private static VisualElement MakePresetItem(GameSettingsManagerPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            GameSettingsManagerPreset preset = label.userData as GameSettingsManagerPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => panel.DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => panel.DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one row to a filtered Settings Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with filtered presets.</param>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Filtered preset index.</param>
    private static void BindPresetItem(GameSettingsManagerPresetsPanel panel, VisualElement element, int index)
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

        GameSettingsManagerPreset preset = panel.FilteredPresets[index];
        label.userData = preset;
        label.text = panel.GetPresetDisplayName(preset);
        label.tooltip = preset != null ? preset.Description : string.Empty;
    }

    /// <summary>
    /// Selects the first preset included in the ListView selection event.
    /// </summary>
    /// <param name="panel">Owning panel receiving the selection.</param>
    /// <param name="selection">Current ListView selection.</param>
    private static void OnPresetSelectionChanged(GameSettingsManagerPresetsPanel panel, IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            GameSettingsManagerPreset preset = item as GameSettingsManagerPreset;

            if (preset == null)
                continue;

            if (panel.SelectedPreset == preset)
                return;

            panel.SelectPreset(preset);
            return;
        }

        if (panel.SelectedPreset != null)
            panel.SelectPreset(null);
    }

    /// <summary>
    /// Checks whether one preset matches the current search text.
    /// </summary>
    /// <param name="preset">Preset to inspect.</param>
    /// <param name="searchText">Current search text.</param>
    /// <returns>True when visible.</returns>
    private static bool MatchesSearch(GameSettingsManagerPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        if (preset == null || string.IsNullOrWhiteSpace(preset.PresetName))
            return false;

        return preset.PresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Updates duplicated preset metadata and optionally regenerates the stable ID.
    /// </summary>
    /// <param name="preset">Preset to update.</param>
    /// <param name="name">New preset name.</param>
    /// <param name="regenerateId">True when a fresh ID should be assigned.</param>
    private static void SynchronizePresetMetadata(GameSettingsManagerPreset preset, string name, bool regenerateId)
    {
        SerializedObject serializedObject = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedObject.FindProperty("presetName");
        SerializedProperty idProperty = serializedObject.FindProperty("presetId");
        serializedObject.Update();

        if (nameProperty != null)
            nameProperty.stringValue = name;

        if (regenerateId && idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
