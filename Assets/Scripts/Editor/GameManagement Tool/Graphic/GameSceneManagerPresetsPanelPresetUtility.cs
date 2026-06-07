using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds Scene Manager preset browser UI and handles preset asset mutations.
/// </summary>
internal static class GameSceneManagerPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the main split view containing the Scene Manager preset list and details.
    /// </summary>
    /// <param name="panel">Owning panel that stores UI state.</param>
    /// <param name="leftPaneWidth">Fixed browser pane width.</param>
    /// <returns>Main content visual root.</returns>
    public static VisualElement BuildMainContent(GameSceneManagerPresetsPanel panel, float leftPaneWidth)
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
    public static void RefreshPresetList(GameSceneManagerPresetsPanel panel)
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
            panel.SelectPreset(panel.FilteredPresets[0]);
    }

    /// <summary>
    /// Creates and selects a new Scene Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel that receives the new selection.</param>
    public static void CreatePreset(GameSceneManagerPresetsPanel panel)
    {
        if (panel == null)
            return;

        GameSceneManagerPreset newPreset = GameSceneManagerPresetLibraryUtility.CreatePresetAsset("GameSceneManagerPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Scene Manager Preset");
        Undo.RecordObject(panel.Library, "Add Scene Manager Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(newPreset);
    }

    /// <summary>
    /// Duplicates one Scene Manager preset asset and registers it.
    /// </summary>
    /// <param name="panel">Owning panel that receives the duplicate selection.</param>
    /// <param name="preset">Source preset to duplicate.</param>
    public static void DuplicatePreset(GameSceneManagerPresetsPanel panel, GameSceneManagerPreset preset)
    {
        if (panel == null || preset == null)
            return;

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalPath) || string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string duplicateBaseName = GameManagementDraftSession.NormalizeAssetName(panel.GetPresetDisplayName(preset) + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "GameSceneManagerPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        GameSceneManagerPreset duplicatedPreset = ScriptableObject.CreateInstance<GameSceneManagerPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = Path.GetFileNameWithoutExtension(duplicatedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        SynchronizePresetMetadata(duplicatedPreset, duplicatedPreset.name, true);

        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Scene Manager Preset");
        Undo.RecordObject(panel.Library, "Duplicate Scene Manager Preset");
        panel.Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(duplicatedPreset);
    }

    /// <summary>
    /// Stages one Scene Manager preset for deletion after confirmation.
    /// </summary>
    /// <param name="panel">Owning panel with library state.</param>
    /// <param name="preset">Preset to delete.</param>
    public static void DeletePreset(GameSceneManagerPresetsPanel panel, GameSceneManagerPreset preset)
    {
        if (panel == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Scene Manager Preset",
                                                     "Delete the selected Scene Manager preset asset?",
                                                     "Delete",
                                                     "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Scene Manager Preset");
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
    private static VisualElement BuildLeftPane(GameSceneManagerPresetsPanel panel)
    {
        VisualElement leftPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(leftPane);
        leftPane.Add(BuildToolbar(panel));

        ToolbarSearchField searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Scene Manager presets by name.";
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
    /// Builds create, duplicate and delete buttons for Scene Manager presets.
    /// </summary>
    /// <param name="panel">Owning panel used by callbacks.</param>
    /// <returns>Toolbar visual element.</returns>
    private static Toolbar BuildToolbar(GameSceneManagerPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(panel.CreatePreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new Scene Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 52f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => panel.DuplicatePreset(panel.SelectedPreset));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected Scene Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 72f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => panel.DeletePreset(panel.SelectedPreset));
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Stage the selected Scene Manager preset for deletion.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 52f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the selected preset detail scroll area.
    /// </summary>
    /// <param name="panel">Owning panel receiving the details root.</param>
    /// <returns>Right pane visual element.</returns>
    private static VisualElement BuildRightPane(GameSceneManagerPresetsPanel panel)
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
    private static void AddMatchingPresets(GameSceneManagerPresetsPanel panel, string searchText)
    {
        for (int index = 0; index < panel.Library.Presets.Count; index++)
        {
            GameSceneManagerPreset preset = panel.Library.Presets[index];

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
    private static VisualElement MakePresetItem(GameSceneManagerPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            GameSceneManagerPreset preset = label.userData as GameSceneManagerPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => panel.DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => panel.DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one row to a filtered Scene Manager preset.
    /// </summary>
    /// <param name="panel">Owning panel with filtered presets.</param>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Filtered preset index.</param>
    private static void BindPresetItem(GameSceneManagerPresetsPanel panel, VisualElement element, int index)
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

        GameSceneManagerPreset preset = panel.FilteredPresets[index];
        label.userData = preset;
        label.text = panel.GetPresetDisplayName(preset);
        label.tooltip = preset != null ? preset.Description : string.Empty;
    }

    /// <summary>
    /// Selects the first preset included in the ListView selection event.
    /// </summary>
    /// <param name="panel">Owning panel receiving the selection.</param>
    /// <param name="selection">Current ListView selection.</param>
    private static void OnPresetSelectionChanged(GameSceneManagerPresetsPanel panel, IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            GameSceneManagerPreset preset = item as GameSceneManagerPreset;

            if (preset == null)
                continue;

            // Re-fired selectionChanged events (e.g. after ListView.Rebuild) would otherwise tear down
            // the detail subtree even when the selection did not actually change.
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
    private static bool MatchesSearch(GameSceneManagerPreset preset, string searchText)
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
    private static void SynchronizePresetMetadata(GameSceneManagerPreset preset, string name, bool regenerateId)
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
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
