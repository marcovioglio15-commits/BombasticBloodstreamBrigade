using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Room Clear Rewards preset browser and routes asset mutations through the draft session.
/// </summary>
internal static class GameRoomClearRewardsPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the responsive preset browser and scrollable details area.
    /// </summary>
    /// <param name="panel">Panel receiving browser controls and details state.</param>
    /// <param name="leftPaneWidth">Initial fixed width of the browser pane.</param>
    /// <returns>Split view containing the browser and selected-preset details.</returns>
    public static VisualElement BuildMainContent(GameRoomClearRewardsPresetsPanel panel, float leftPaneWidth)
    {
        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(leftPaneWidth);
        splitView.Add(BuildLeftPane(panel));
        splitView.Add(BuildRightPane(panel));
        return splitView;
    }

    /// <summary>
    /// Rebuilds the search-filtered collection and restores a valid selected preset.
    /// </summary>
    /// <param name="panel">Panel whose library, list and selection are refreshed.</param>
    public static void RefreshPresetList(GameRoomClearRewardsPresetsPanel panel)
    {
        if (panel == null)
            return;

        panel.FilteredPresets.Clear();
        string searchText = panel.PresetSearchField != null
            ? panel.PresetSearchField.value
            : string.Empty;

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

        GameRoomClearRewardsPreset restored =
            ManagementToolStateUtility.LoadAsset<GameRoomClearRewardsPreset>(
                GameRoomClearRewardsPresetsPanel.SelectedPresetStateKey);
        panel.SelectPreset(restored != null && panel.FilteredPresets.Contains(restored)
            ? restored
            : panel.FilteredPresets[0]);
    }

    /// <summary>
    /// Creates, registers and selects one Room Clear Rewards preset.
    /// </summary>
    /// <param name="panel">Panel receiving the newly created preset.</param>
    public static void CreatePreset(GameRoomClearRewardsPresetsPanel panel)
    {
        if (panel == null || panel.Library == null)
            return;

        GameRoomClearRewardsPreset preset =
            GameRoomClearRewardsPresetLibraryUtility.CreatePresetAsset("GameRoomClearRewardsPreset");

        if (preset == null)
            return;

        Undo.RegisterCreatedObjectUndo(preset, "Create Room Clear Rewards Preset");
        Undo.RecordObject(panel.Library, "Register Room Clear Rewards Preset");
        panel.Library.AddPreset(preset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(preset);
    }

    /// <summary>
    /// Copies one preset into a registered asset with detached nested technical identities.
    /// </summary>
    /// <param name="panel">Panel receiving the duplicated asset.</param>
    /// <param name="preset">Source preset whose serialized configuration is copied.</param>
    public static void DuplicatePreset(GameRoomClearRewardsPresetsPanel panel,
                                       GameRoomClearRewardsPreset preset)
    {
        if (panel == null || panel.Library == null || preset == null)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(preset);
        string directory = Path.GetDirectoryName(sourcePath);

        if (string.IsNullOrWhiteSpace(directory))
            return;

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(directory, preset.name + " Copy.asset").Replace('\\', '/'));
        GameRoomClearRewardsPreset duplicate =
            ScriptableObject.CreateInstance<GameRoomClearRewardsPreset>();
        EditorUtility.CopySerialized(preset, duplicate);
        duplicate.name = Path.GetFileNameWithoutExtension(targetPath);
        duplicate.RegenerateTechnicalIds();
        AssetDatabase.CreateAsset(duplicate, targetPath);
        Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate Room Clear Rewards Preset");
        Undo.RecordObject(panel.Library, "Register Room Clear Rewards Preset Copy");
        panel.Library.AddPreset(duplicate);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
        panel.SelectPreset(duplicate);
    }

    /// <summary>
    /// Removes one preset from its library and stages its asset deletion after confirmation.
    /// </summary>
    /// <param name="panel">Panel whose library reference and browser are updated.</param>
    /// <param name="preset">Preset asset staged for deletion.</param>
    public static void DeletePreset(GameRoomClearRewardsPresetsPanel panel,
                                    GameRoomClearRewardsPreset preset)
    {
        if (panel == null || panel.Library == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Delete Room Clear Rewards Preset",
            "Delete the selected Room Clear Rewards preset asset when changes are applied?",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Room Clear Rewards Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        GameManagementDraftSession.StageDeleteAsset(preset);
        panel.RefreshPresetList();
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Builds the searchable preset list and its draft-aware asset actions.
    /// </summary>
    /// <param name="panel">Panel used by browser callbacks.</param>
    /// <returns>Configured browser pane.</returns>
    private static VisualElement BuildLeftPane(GameRoomClearRewardsPresetsPanel panel)
    {
        VisualElement leftPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(leftPane);
        leftPane.Add(BuildToolbar(panel));

        ToolbarSearchField searchField = new ToolbarSearchField();
        searchField.tooltip = "Filter Room Clear Rewards presets by display name.";
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
    /// Builds create, duplicate and staged-delete actions using the shared toolbar style.
    /// </summary>
    /// <param name="panel">Panel handling each toolbar action.</param>
    /// <returns>Configured wrapping toolbar.</returns>
    private static Toolbar BuildToolbar(GameRoomClearRewardsPresetsPanel panel)
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);
        AddToolbarButton(toolbar,
                         "Create",
                         "Create a Room Clear Rewards preset in the draft session.",
                         panel.CreatePreset,
                         52f);
        AddToolbarButton(toolbar,
                         "Duplicate",
                         "Duplicate the selected preset with fresh nested technical IDs.",
                         () => panel.DuplicatePreset(panel.SelectedPreset),
                         72f);
        AddToolbarButton(toolbar,
                         "Delete",
                         "Stage the selected preset for deletion when Apply is pressed.",
                         () => panel.DeletePreset(panel.SelectedPreset),
                         52f);
        return toolbar;
    }

    /// <summary>
    /// Builds the scrollable selected-preset details host.
    /// </summary>
    /// <param name="panel">Panel receiving the details-root reference.</param>
    /// <returns>Configured details pane.</returns>
    private static VisualElement BuildRightPane(GameRoomClearRewardsPresetsPanel panel)
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
    /// Adds live library entries that match the current case-insensitive browser filter.
    /// </summary>
    /// <param name="panel">Panel receiving matched presets.</param>
    /// <param name="searchText">Search text applied to each visible preset label.</param>
    private static void AddMatchingPresets(GameRoomClearRewardsPresetsPanel panel, string searchText)
    {
        for (int index = 0; index < panel.Library.Presets.Count; index++)
        {
            GameRoomClearRewardsPreset preset = panel.Library.Presets[index];

            if (preset == null || GameManagementDraftSession.IsAssetStagedForDeletion(preset))
                continue;

            if (MatchesSearch(panel, preset, searchText))
                panel.FilteredPresets.Add(preset);
        }
    }

    /// <summary>
    /// Creates a reusable browser label with contextual duplicate and delete actions.
    /// </summary>
    /// <param name="panel">Panel handling contextual actions.</param>
    /// <returns>Reusable preset row label.</returns>
    private static VisualElement MakePresetItem(GameRoomClearRewardsPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            GameRoomClearRewardsPreset preset = label.userData as GameRoomClearRewardsPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate",
                                  action => panel.DuplicatePreset(preset),
                                  DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete",
                                  action => panel.DeletePreset(preset),
                                  DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one reusable browser row to its filtered preset entry.
    /// </summary>
    /// <param name="panel">Panel containing the filtered preset collection.</param>
    /// <param name="element">Reusable row visual.</param>
    /// <param name="index">Filtered preset index.</param>
    private static void BindPresetItem(GameRoomClearRewardsPresetsPanel panel,
                                       VisualElement element,
                                       int index)
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

        GameRoomClearRewardsPreset preset = panel.FilteredPresets[index];
        label.userData = preset;
        label.text = panel.GetPresetDisplayName(preset);
        label.tooltip = preset != null ? preset.Description : string.Empty;
    }

    /// <summary>
    /// Synchronizes the panel selection with the list view's current single selection.
    /// </summary>
    /// <param name="panel">Panel receiving the selected preset.</param>
    /// <param name="selection">Current browser selection enumeration.</param>
    private static void OnPresetSelectionChanged(GameRoomClearRewardsPresetsPanel panel,
                                                 IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            GameRoomClearRewardsPreset preset = selectedObject as GameRoomClearRewardsPreset;

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
    /// Checks whether one preset's browser label contains the active search text.
    /// </summary>
    /// <param name="panel">Panel resolving the visible preset label.</param>
    /// <param name="preset">Preset inspected by the filter.</param>
    /// <param name="searchText">Case-insensitive search text.</param>
    /// <returns>True when the preset should remain visible.</returns>
    private static bool MatchesSearch(GameRoomClearRewardsPresetsPanel panel,
                                      GameRoomClearRewardsPreset preset,
                                      string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        if (preset == null)
            return false;

        return panel.GetPresetDisplayName(preset)
                    .IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Adds one consistently sized action to a wrapping browser toolbar.
    /// </summary>
    /// <param name="toolbar">Toolbar receiving the action.</param>
    /// <param name="label">Visible button text.</param>
    /// <param name="tooltip">Explanation displayed while hovering the action.</param>
    /// <param name="callback">Action invoked by the button.</param>
    /// <param name="width">Fixed readable width used by the shared responsive layout.</param>
    private static void AddToolbarButton(Toolbar toolbar,
                                         string label,
                                         string tooltip,
                                         Action callback,
                                         float width)
    {
        Button button = new Button(callback);
        button.text = label;
        button.tooltip = tooltip;
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(button, width);
        toolbar.Add(button);
    }
    #endregion

    #endregion
}
