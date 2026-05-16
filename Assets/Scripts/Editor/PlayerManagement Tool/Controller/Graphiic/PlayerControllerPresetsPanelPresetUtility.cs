using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds preset list UI and manages create, duplicate, delete and rename flows for player controller preset panels.
/// </summary>
internal static class PlayerControllerPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the split-view main content that combines preset list and detail panes.
    /// </summary>
    /// <param name="panel">Owning panel that stores UI references.</param>
    /// <param name="leftPaneWidth">Initial split width for the preset list pane.</param>
    /// <returns>Returns the constructed main content root.</returns>
    public static VisualElement BuildMainContent(PlayerControllerPresetsPanel panel, float leftPaneWidth)
    {
        VisualElement container = new VisualElement();
        container.style.flexGrow = 1f;
        container.style.flexShrink = 1f;

        TwoPaneSplitView splitView = new TwoPaneSplitView(0, leftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        splitView.Add(BuildLeftPane(panel));
        splitView.Add(BuildRightPane(panel));
        container.Add(splitView);
        return container;
    }

    /// <summary>
    /// Builds the left pane that hosts toolbar actions, search and preset list.
    /// </summary>
    /// <param name="panel">Owning panel that stores preset list state and callbacks.</param>
    /// <returns>Returns the constructed left pane visual element.</returns>
    public static VisualElement BuildLeftPane(PlayerControllerPresetsPanel panel)
    {
        VisualElement leftPane = new VisualElement();
        leftPane.style.flexGrow = 1f;
        leftPane.style.paddingLeft = 6f;
        leftPane.style.paddingRight = 6f;
        leftPane.style.paddingTop = 6f;
        leftPane.style.overflow = Overflow.Hidden;

        Toolbar toolbar = new Toolbar();
        toolbar.style.marginBottom = 4f;

        Button createButton = new Button(() => CreatePreset(panel));
        createButton.text = "Create";
        toolbar.Add(createButton);

        Button duplicateButton = new Button(() => DuplicatePreset(panel, panel.SelectedPreset));
        duplicateButton.text = "Duplicate";
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => DeletePreset(panel, panel.SelectedPreset));
        deleteButton.text = "Delete";
        toolbar.Add(deleteButton);

        leftPane.Add(toolbar);

        ToolbarSearchField searchField = new ToolbarSearchField();
        searchField.style.width = Length.Percent(100f);
        searchField.style.maxWidth = Length.Percent(100f);
        searchField.style.flexShrink = 1f;
        searchField.style.marginBottom = 4f;
        searchField.RegisterValueChangedCallback(evt =>
        {
            panel.RefreshPresetList();
        });
        panel.PresetSearchField = searchField;
        leftPane.Add(searchField);

        ListView listView = new ListView();
        listView.style.flexGrow = 1f;
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
    /// Builds the right pane that hosts the preset detail scroll view.
    /// </summary>
    /// <param name="panel">Owning panel that stores the detail root reference.</param>
    /// <returns>Returns the constructed right pane visual element.</returns>
    public static VisualElement BuildRightPane(PlayerControllerPresetsPanel panel)
    {
        VisualElement rightPane = new VisualElement();
        rightPane.style.flexGrow = 1f;
        rightPane.style.paddingLeft = 10f;
        rightPane.style.paddingRight = 10f;
        rightPane.style.paddingTop = 6f;

        ScrollView detailsRoot = new ScrollView();
        detailsRoot.style.flexGrow = 1f;
        rightPane.Add(detailsRoot);
        panel.DetailsRoot = detailsRoot;
        return rightPane;
    }

    /// <summary>
    /// Creates one reusable preset list item with contextual menu actions.
    /// </summary>
    /// <param name="panel">Owning panel that provides preset callbacks.</param>
    /// <returns>Returns the list item visual element.</returns>
    public static VisualElement MakePresetItem(PlayerControllerPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            PlayerControllerPreset preset = label.userData as PlayerControllerPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(panel, preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(panel, preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => ShowRenamePopup(panel, label, preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one preset list item at the requested filtered index.
    /// </summary>
    /// <param name="panel">Owning panel that provides filtered preset data.</param>
    /// <param name="element">List item visual element to bind.</param>
    /// <param name="index">Filtered preset index.</param>

    public static void BindPresetItem(PlayerControllerPresetsPanel panel, VisualElement element, int index)
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

        PlayerControllerPreset preset = panel.FilteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = panel.GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }

    /// <summary>
    /// Handles preset list selection changes and forwards the resolved preset to the detail view.
    /// </summary>
    /// <param name="panel">Owning panel that provides the detail selection callback.</param>
    /// <param name="selection">Current ListView selection.</param>

    public static void OnPresetSelectionChanged(PlayerControllerPresetsPanel panel, IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            PlayerControllerPreset preset = item as PlayerControllerPreset;

            if (preset != null)
            {
                panel.SelectPreset(preset);
                return;
            }
        }

        panel.SelectPreset(null);
    }

    /// <summary>
    /// Rebuilds the filtered preset list using the current search field value and maintains valid selection.
    /// </summary>
    /// <param name="panel">Owning panel that stores library, filtered list and selected preset state.</param>

    public static void RefreshPresetList(PlayerControllerPresetsPanel panel)
    {
        panel.FilteredPresets.Clear();

        if (panel.Library != null)
        {
            string searchText = panel.PresetSearchField != null ? panel.PresetSearchField.value : string.Empty;

            for (int index = 0; index < panel.Library.Presets.Count; index++)
            {
                PlayerControllerPreset preset = panel.Library.Presets[index];

                if (preset == null)
                    continue;

                if (PlayerManagementDraftSession.IsAssetStagedForDeletion(preset))
                    continue;

                if (IsMatchingSearch(preset, searchText))
                    panel.FilteredPresets.Add(preset);
            }
        }

        if (panel.PresetListView != null)
            panel.PresetListView.Rebuild();

        if (panel.FilteredPresets.Count == 0)
        {
            panel.SelectPreset(null);
            return;
        }

        if (panel.SelectedPreset == null || !panel.FilteredPresets.Contains(panel.SelectedPreset))
        {
            panel.SelectPreset(panel.FilteredPresets[0]);

            if (panel.PresetListView != null)
                panel.PresetListView.SetSelectionWithoutNotify(new int[] { 0 });
        }
    }

    /// <summary>
    /// Creates one new player controller preset asset, registers it in the library and selects it.
    /// </summary>
    /// <param name="panel">Owning panel that stores library and selection state.</param>

    public static void CreatePreset(PlayerControllerPresetsPanel panel)
    {
        PlayerControllerPreset newPreset = PlayerControllerPresetLibraryUtility.CreatePresetAsset("PlayerControllerPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Controller Preset Asset");
        Undo.RecordObject(panel.Library, "Add Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        PlayerManagementDraftSession.MarkDirty();

        panel.RefreshPresetList();
        panel.SelectPreset(newPreset);

        int index = panel.FilteredPresets.IndexOf(newPreset);

        if (index >= 0 && panel.PresetListView != null)
            panel.PresetListView.SetSelection(index);
    }

    /// <summary>
    /// Duplicates one player controller preset asset, assigns a fresh identifier and selects the copy.
    /// </summary>
    /// <param name="panel">Owning panel that stores library and selection state.</param>
    /// <param name="preset">Preset to duplicate.</param>

    public static void DuplicatePreset(PlayerControllerPresetsPanel panel, PlayerControllerPreset preset)
    {
        if (panel == null || preset == null)
            return;

        PlayerControllerPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerControllerPreset>();
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
            duplicateBaseName = "PlayerControllerPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Controller Preset Asset");
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(panel.Library, "Duplicate Preset");
        panel.Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(panel.Library);
        PlayerManagementDraftSession.MarkDirty();

        panel.RefreshPresetList();
        panel.SelectPreset(duplicatedPreset);

        int index = panel.FilteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0 && panel.PresetListView != null)
            panel.PresetListView.SetSelection(index);
    }

    /// <summary>
    /// Deletes one player controller preset after confirmation and refreshes the list.
    /// </summary>
    /// <param name="panel">Owning panel that stores library state.</param>
    /// <param name="preset">Preset to delete.</param>

    public static void DeletePreset(PlayerControllerPresetsPanel panel, PlayerControllerPreset preset)
    {
        if (panel == null || preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        PlayerManagementDraftSession.StageDeleteAsset(preset);
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Renames one preset asset and updates the serialized preset name field.
    /// </summary>
    /// <param name="panel">Owning panel that refreshes list UI after the rename.</param>
    /// <param name="preset">Preset to rename.</param>
    /// <param name="newName">Requested new display name.</param>

    public static void RenamePreset(PlayerControllerPresetsPanel panel, PlayerControllerPreset preset, string newName)
    {
        if (panel == null || preset == null)
            return;

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

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
        PlayerManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Opens the inline rename popup for one preset item.
    /// </summary>
    /// <param name="panel">Owning panel that applies the rename callback.</param>
    /// <param name="anchor">UI anchor used for popup placement.</param>
    /// <param name="preset">Preset to rename.</param>

    public static void ShowRenamePopup(PlayerControllerPresetsPanel panel, VisualElement anchor, PlayerControllerPreset preset)
    {
        if (panel == null || anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Controller Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(panel, preset, newName));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Evaluates whether one preset should stay visible for the current search text.
    /// </summary>
    /// <param name="preset">Preset to test.</param>
    /// <param name="searchText">Current search text.</param>
    /// <returns>Returns true when the preset matches the filter.</returns>
    private static bool IsMatchingSearch(PlayerControllerPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string presetName = preset.PresetName;

        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        return presetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #endregion
}
