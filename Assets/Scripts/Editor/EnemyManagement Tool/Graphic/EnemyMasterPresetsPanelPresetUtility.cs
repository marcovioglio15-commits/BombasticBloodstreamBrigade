using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds preset list UI and manages create, duplicate, delete and selection flows for enemy master preset panels.
/// </summary>
internal static class EnemyMasterPresetsPanelPresetUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the left pane that hosts toolbar actions, search and preset list.
    /// </summary>
    /// <param name="panel">Owning panel that stores preset list state and callbacks.</param>
    /// <returns>Returns the constructed left pane visual element.</returns>
    public static VisualElement BuildLeftPane(EnemyMasterPresetsPanel panel)
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

        Button duplicateButton = new Button(() => panel.DuplicatePreset(panel.SelectedPreset));
        duplicateButton.text = "Duplicate";
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(() => panel.DeletePreset(panel.SelectedPreset));
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
    /// Builds the split-view main content that combines preset list and detail panes.
    /// </summary>
    /// <param name="panel">Owning panel that provides left and right pane builders.</param>
    /// <param name="leftPaneWidth">Initial split width for the preset list pane.</param>
    /// <returns>Returns the constructed main content root.</returns>
    public static VisualElement BuildMainContent(EnemyMasterPresetsPanel panel, float leftPaneWidth)
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
    /// Builds the right pane that hosts the preset detail scroll view.
    /// </summary>
    /// <param name="panel">Owning panel that stores the detail root reference.</param>
    /// <returns>Returns the constructed right pane visual element.</returns>
    public static VisualElement BuildRightPane(EnemyMasterPresetsPanel panel)
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
    /// Creates one preset list item visual element with contextual menu actions.
    /// </summary>
    /// <param name="panel">Owning panel that provides preset actions.</param>
    /// <returns>Returns the reusable preset item visual element.</returns>
    public static VisualElement MakePresetItem(EnemyMasterPresetsPanel panel)
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            EnemyMasterPreset preset = label.userData as EnemyMasterPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => panel.DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => panel.DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => panel.ShowRenamePopup(label, preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds one preset list item at the requested filtered index.
    /// </summary>
    /// <param name="panel">Owning panel that provides filtered preset data.</param>
    /// <param name="element">List item visual element to bind.</param>
    /// <param name="index">Filtered preset index.</param>

    public static void BindPresetItem(EnemyMasterPresetsPanel panel, VisualElement element, int index)
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

        EnemyMasterPreset preset = panel.FilteredPresets[index];

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

    public static void OnPresetSelectionChanged(EnemyMasterPresetsPanel panel, IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            EnemyMasterPreset preset = item as EnemyMasterPreset;

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

    public static void RefreshPresetList(EnemyMasterPresetsPanel panel)
    {
        panel.FilteredPresets.Clear();

        if (panel.Library != null)
        {
            string searchText = panel.PresetSearchField != null ? panel.PresetSearchField.value : string.Empty;

            for (int index = 0; index < panel.Library.Presets.Count; index++)
            {
                EnemyMasterPreset preset = panel.Library.Presets[index];

                if (preset == null)
                    continue;

                if (EnemyManagementDraftSession.IsAssetStagedForDeletion(preset))
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
    /// Creates one new enemy master preset asset, registers it in the library and selects it.
    /// </summary>
    /// <param name="panel">Owning panel that stores library and selection state.</param>

    public static void CreatePreset(EnemyMasterPresetsPanel panel)
    {
        EnemyMasterPreset newPreset = EnemyMasterPresetLibraryUtility.CreatePresetAsset("EnemyMasterPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Master Preset Asset");
        Undo.RecordObject(panel.Library, "Add Enemy Master Preset");
        panel.Library.AddPreset(newPreset);
        EditorUtility.SetDirty(panel.Library);
        EnemyManagementDraftSession.MarkDirty();

        panel.RefreshPresetList();
        panel.SelectPreset(newPreset);

        int index = panel.FilteredPresets.IndexOf(newPreset);

        if (index >= 0 && panel.PresetListView != null)
            panel.PresetListView.SetSelection(index);
    }

    /// <summary>
    /// Duplicates one enemy master preset asset and selects the duplicated copy.
    /// </summary>
    /// <param name="panel">Owning panel that stores library and selection state.</param>
    /// <param name="preset">Preset asset to duplicate.</param>

    public static void DuplicatePreset(EnemyMasterPresetsPanel panel, EnemyMasterPreset preset)
    {
        if (preset == null)
            return;

        EnemyMasterPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyMasterPreset>();
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
            duplicateBaseName = "EnemyMasterPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Master Preset Asset");
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

        Undo.RecordObject(panel.Library, "Duplicate Enemy Master Preset");
        panel.Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(panel.Library);
        EnemyManagementDraftSession.MarkDirty();

        panel.RefreshPresetList();
        panel.SelectPreset(duplicatedPreset);

        int index = panel.FilteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0 && panel.PresetListView != null)
            panel.PresetListView.SetSelection(index);
    }

    /// <summary>
    /// Deletes one enemy master preset after confirmation and refreshes the preset list.
    /// </summary>
    /// <param name="panel">Owning panel that stores library and selection state.</param>
    /// <param name="preset">Preset asset to delete.</param>

    public static void DeletePreset(EnemyMasterPresetsPanel panel, EnemyMasterPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Master Preset", "Delete the selected enemy master preset asset?", "Delete", "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(panel.Library, "Delete Enemy Master Preset");
        panel.Library.RemovePreset(preset);
        EditorUtility.SetDirty(panel.Library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);

        panel.RefreshPresetList();
    }

    /// <summary>
    /// Renames one enemy master preset asset and refreshes the visible preset list.
    /// </summary>
    /// <param name="panel">Owning panel that refreshes the visible preset list.</param>
    /// <param name="preset">Preset asset to rename.</param>
    /// <param name="newName">Requested new asset and preset name.</param>

    public static void RenamePreset(EnemyMasterPresetsPanel panel, EnemyMasterPreset preset, string newName)
    {
        if (panel == null || preset == null)
            return;

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(newName);

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
        EnemyManagementDraftSession.MarkDirty();
        panel.RefreshPresetList();
    }

    /// <summary>
    /// Opens the rename popup anchored to one preset list item.
    /// </summary>
    /// <param name="panel">Owning panel that receives the rename callback.</param>
    /// <param name="anchor">Anchor visual element used for popup placement.</param>
    /// <param name="preset">Preset asset being renamed.</param>

    public static void ShowRenamePopup(EnemyMasterPresetsPanel panel, VisualElement anchor, EnemyMasterPreset preset)
    {
        if (panel == null)
            return;

        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Enemy Master Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(panel, preset, newName));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether the preset display name matches the current search string.
    /// </summary>
    /// <param name="preset">Preset to test.</param>
    /// <param name="searchText">Search string entered by the user.</param>
    /// <returns>Returns true when the preset matches or when the search string is empty.</returns>
    private static bool IsMatchingSearch(EnemyMasterPreset preset, string searchText)
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
