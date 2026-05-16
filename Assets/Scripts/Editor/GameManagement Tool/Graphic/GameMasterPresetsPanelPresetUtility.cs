using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds list/detail shell controls and preset mutations for Game Management master presets.
/// </summary>
internal static class GameMasterPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the main split view containing the master preset list and selected preset details.
    /// </summary>
    /// <param name="panel">Owning panel that stores UI state.</param>
    /// <param name="leftPaneWidth">Fixed width used by the preset browser.</param>
    /// <returns>Main content visual root.</returns>
    public static VisualElement BuildMainContent(GameMasterPresetsPanel panel, float leftPaneWidth)
    {
        VisualElement container = new VisualElement();
        container.style.flexGrow = 1f;
        container.style.flexShrink = 1f;
        container.style.minWidth = 0f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(leftPaneWidth);
        splitView.Add(BuildLeftPane(panel));
        splitView.Add(BuildRightPane(panel));
        container.Add(splitView);
        return container;
    }

    /// <summary>
    /// Refreshes filtered preset rows while keeping a valid selection active.
    /// </summary>
    /// <param name="panel">Owning panel with library, search and list state.</param>
    public static void RefreshPresetList(GameMasterPresetsPanel panel)
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
    /// Creates and registers a new game master preset asset.
    /// </summary>
    /// <param name="panel">Owning panel that receives the new selection.</param>
    public static void CreatePreset(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        GameMasterPreset newPreset = GameMasterPresetLibraryUtility.CreatePresetAsset("GameMasterPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Game Master Preset");
        Undo.RecordObject(panel.Library, "Add Game Master Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(newPreset);
    }

    /// <summary>
    /// Duplicates one game master preset and registers the copy in the library.
    /// </summary>
    /// <param name="panel">Owning panel that receives the duplicate selection.</param>
    /// <param name="preset">Source preset to duplicate.</param>
    public static void DuplicatePreset(GameMasterPresetsPanel panel, GameMasterPreset preset)
    {
        if (panel == null || preset == null)
            return;

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
            return;

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string duplicateBaseName = GameManagementDraftSession.NormalizeAssetName(panel.GetPresetDisplayName(preset) + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "GameMasterPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        GameMasterPreset duplicatedPreset = ScriptableObject.CreateInstance<GameMasterPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = Path.GetFileNameWithoutExtension(duplicatedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        SynchronizePresetMetadata(duplicatedPreset, duplicatedPreset.name, true);

        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Game Master Preset");
        Undo.RecordObject(panel.Library, "Duplicate Game Master Preset");
        panel.Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(duplicatedPreset);
    }

    /// <summary>
    /// Stages one master preset asset for deletion after confirmation.
    /// </summary>
    /// <param name="panel">Owning panel that stores the library.</param>
    /// <param name="preset">Preset to remove from the visible library list.</param>
    public static void DeletePreset(GameMasterPresetsPanel panel, GameMasterPreset preset)
    {
        if (panel == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Game Master Preset",
                                                     "Delete the selected game master preset asset?",
                                                     "Delete",
                                                     "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Game Master Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.StageDeleteAsset(preset);
        panel.RefreshPresetList();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the left browser pane with toolbar, search and list view.
    /// </summary>
    /// <param name="panel">Owning panel whose list state is bound.</param>
    /// <returns>Left pane visual element.</returns>
    private static VisualElement BuildLeftPane(GameMasterPresetsPanel panel)
    {
        VisualElement leftPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(leftPane);

        leftPane.Add(BuildToolbar(panel));

        ToolbarSearchField searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Game Master presets by display name.";
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
    /// Builds preset creation and deletion controls.
    /// </summary>
    /// <param name="panel">Owning panel used by button callbacks.</param>
    /// <returns>Toolbar visual element.</returns>
    private static Toolbar BuildToolbar(GameMasterPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(panel.CreatePreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new game master preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 52f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => panel.DuplicatePreset(panel.SelectedPreset));
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected game master preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 72f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => panel.DeletePreset(panel.SelectedPreset));
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Stage the selected game master preset for deletion.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 52f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the selected preset detail scroll root.
    /// </summary>
    /// <param name="panel">Owning panel receiving the details root.</param>
    /// <returns>Right pane visual element.</returns>
    private static VisualElement BuildRightPane(GameMasterPresetsPanel panel)
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
    /// Adds library presets that pass the current search and staging filters.
    /// </summary>
    /// <param name="panel">Owning panel that stores the filtered output list.</param>
    /// <param name="searchText">Current search text.</param>
    private static void AddMatchingPresets(GameMasterPresetsPanel panel, string searchText)
    {
        for (int index = 0; index < panel.Library.Presets.Count; index++)
        {
            GameMasterPreset preset = panel.Library.Presets[index];

            if (preset == null)
                continue;

            if (GameManagementDraftSession.IsAssetStagedForDeletion(preset))
                continue;

            if (MatchesSearch(preset, searchText))
                panel.FilteredPresets.Add(preset);
        }
    }

    /// <summary>
    /// Creates one list item label with row-level context actions.
    /// </summary>
    /// <param name="panel">Owning panel used by context actions.</param>
    /// <returns>List item label.</returns>
    private static VisualElement MakePresetItem(GameMasterPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            GameMasterPreset preset = label.userData as GameMasterPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => panel.DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => panel.DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one list row to the filtered preset at the requested index.
    /// </summary>
    /// <param name="panel">Owning panel with filtered presets.</param>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Filtered preset index.</param>
    private static void BindPresetItem(GameMasterPresetsPanel panel, VisualElement element, int index)
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

        GameMasterPreset preset = panel.FilteredPresets[index];
        label.userData = preset;
        label.text = panel.GetPresetDisplayName(preset);
        label.tooltip = preset != null ? preset.Description : string.Empty;
    }

    /// <summary>
    /// Selects the first preset included in the ListView selection event.
    /// </summary>
    /// <param name="panel">Owning panel receiving the selected preset.</param>
    /// <param name="selection">Current ListView selection.</param>
    private static void OnPresetSelectionChanged(GameMasterPresetsPanel panel, IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            GameMasterPreset preset = item as GameMasterPreset;

            if (preset == null)
                continue;

            panel.SelectPreset(preset);
            return;
        }

        panel.SelectPreset(null);
    }

    /// <summary>
    /// Checks whether one preset matches the current search text.
    /// </summary>
    /// <param name="preset">Preset to inspect.</param>
    /// <param name="searchText">Current search text.</param>
    /// <returns>True when visible.</returns>
    private static bool MatchesSearch(GameMasterPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        if (preset == null || string.IsNullOrWhiteSpace(preset.PresetName))
            return false;

        return preset.PresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Writes duplicated preset metadata after a copy operation.
    /// </summary>
    /// <param name="preset">Preset to update.</param>
    /// <param name="name">New preset name.</param>
    /// <param name="regenerateId">True when a fresh ID should be assigned.</param>
    private static void SynchronizePresetMetadata(GameMasterPreset preset, string name, bool regenerateId)
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
