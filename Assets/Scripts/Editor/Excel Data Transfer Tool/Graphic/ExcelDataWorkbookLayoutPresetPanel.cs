using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Edits workbook layout presets with the same browser and section structure used by the other Excel sub-preset panels.
/// </summary>
internal sealed class ExcelDataWorkbookLayoutPresetPanel
{
    #region Constants
    private const float LeftPaneWidth = 340f;
    #endregion

    #region Fields
    private readonly ExcelDataTransferMasterPanel parentPanel;
    private readonly VisualElement root;
    private readonly List<ExcelDataWorkbookLayoutPreset> filteredPresets = new List<ExcelDataWorkbookLayoutPreset>();

    private ToolbarSearchField searchField;
    private ListView presetListView;
    private Label browserStatusLabel;
    private VisualElement detailsRoot;
    private VisualElement sectionContentRoot;
    private ExcelDataTransferMasterPreset selectedMasterPreset;
    private ExcelDataWorkbookLayoutPreset selectedLayoutPreset;
    private ExcelDataLayoutBrushPanel brushPanel;
    private LayoutSectionType activeSection = LayoutSectionType.BrushGrid;
    private string operationStatus;
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

    #region Constructors
    /// <summary>
    /// Builds the workbook layout sub-preset panel.
    /// </summary>
    /// <param name="newParentPanel">Master panel that owns the active transfer preset.</param>
    public ExcelDataWorkbookLayoutPresetPanel(ExcelDataTransferMasterPanel newParentPanel)
    {
        parentPanel = newParentPanel;
        selectedMasterPreset = parentPanel == null ? null : parentPanel.SelectedMasterPreset;

        root = new VisualElement();
        root.style.flexGrow = 1f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(LeftPaneWidth);
        root.Add(splitView);
        splitView.Add(BuildBrowserPane());
        splitView.Add(BuildDetailsPane());

        RefreshFromSessionChange();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Rebinds the panel after master selection, draft session or linked layout changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        selectedMasterPreset = parentPanel == null ? null : parentPanel.SelectedMasterPreset;
        RefreshPresetList();
        SelectLinkedPreset();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the left layout preset browser pane.
    /// </summary>
    /// <returns>Configured browser pane.</returns>
    private VisualElement BuildBrowserPane()
    {
        VisualElement pane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(pane);

        Label titleLabel = new Label("Workbook Layout Presets");
        titleLabel.tooltip = "Workbook layout preset assets available to the Excel Data Transfer Tool.";
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(titleLabel, "NashCore.ExcelDataTransfer.Layout.BrowserTitle");
        pane.Add(titleLabel);
        pane.Add(BuildBrowserToolbar());

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Search workbook layout presets by asset name or authored preset name.";
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        pane.Add(searchField);
        GameManagementPanelLayoutUtility.BindSearchFieldToBrowserPane(pane, searchField);

        presetListView = BuildPresetListView();
        pane.Add(presetListView);
        browserStatusLabel = BuildBrowserStatusLabel();
        pane.Add(browserStatusLabel);
        return pane;
    }

    /// <summary>
    /// Builds create, duplicate and delete actions for the layout preset browser.
    /// </summary>
    /// <returns>Configured toolbar.</returns>
    private Toolbar BuildBrowserToolbar()
    {
        Toolbar toolbar = new Toolbar();
        GameManagementPanelLayoutUtility.ConfigureWrappingToolbar(toolbar);

        Button createButton = new Button(CreateLayoutPreset);
        createButton.text = "Create";
        createButton.tooltip = "Create a new standalone workbook layout preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(createButton, 58f);
        toolbar.Add(createButton);

        Button duplicateButton = new Button(DuplicateLayoutPreset);
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate the selected workbook layout preset without changing master links.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(duplicateButton, 78f);
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(DeleteLayoutPreset);
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete the selected workbook layout preset when no transfer master references it.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(deleteButton, 58f);
        toolbar.Add(deleteButton);
        return toolbar;
    }

    /// <summary>
    /// Builds the browser status label shown under the layout list.
    /// </summary>
    /// <returns>Configured status label.</returns>
    private Label BuildBrowserStatusLabel()
    {
        Label label = new Label();
        label.tooltip = "Latest create, duplicate or delete result for this layout preset browser.";
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 6f;
        return label;
    }

    /// <summary>
    /// Builds the right details pane that hosts layout section tabs and content.
    /// </summary>
    /// <returns>Configured details root.</returns>
    private VisualElement BuildDetailsPane()
    {
        detailsRoot = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(detailsRoot);
        return detailsRoot;
    }

    /// <summary>
    /// Builds the layout preset list view.
    /// </summary>
    /// <returns>Configured ListView.</returns>
    private ListView BuildPresetListView()
    {
        ListView listView = new ListView();
        listView.itemsSource = filteredPresets;
        listView.makeItem = MakePresetListItem;
        listView.bindItem = BindPresetListItem;
        listView.selectionChanged += OnPresetSelectionChanged;
        listView.fixedItemHeight = 22f;
        listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        GameManagementPanelLayoutUtility.ConfigureListView(listView);
        return listView;
    }
    #endregion

    #region List Binding
    /// <summary>
    /// Creates one layout preset browser row.
    /// </summary>
    /// <returns>Row label visual element.</returns>
    private VisualElement MakePresetListItem()
    {
        Label label = new Label();
        label.style.whiteSpace = WhiteSpace.NoWrap;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        return label;
    }

    /// <summary>
    /// Binds one layout preset browser row.
    /// </summary>
    /// <param name="element">Row visual element.</param>
    /// <param name="index">Filtered layout preset index.</param>
    private void BindPresetListItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            return;
        }

        ExcelDataWorkbookLayoutPreset preset = filteredPresets[index];
        label.text = ExcelDataLinkedSubPresetPanelContextUtility.ResolvePresetDisplayName(preset);
        label.tooltip = AssetDatabase.GetAssetPath(preset);
    }

    /// <summary>
    /// Receives layout preset selection changes from the browser list.
    /// </summary>
    /// <param name="selection">Selected ListView payload.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            selectedLayoutPreset = selectedObject as ExcelDataWorkbookLayoutPreset;
            BuildDetailsShell();
            return;
        }
    }
    #endregion

    #region Preset List
    /// <summary>
    /// Refreshes the layout preset browser list from project assets.
    /// </summary>
    private void RefreshPresetList()
    {
        filteredPresets.Clear();
        string searchText = searchField == null ? string.Empty : searchField.value;
        List<ExcelDataWorkbookLayoutPreset> presets = ExcelDataTransferAssetUtility.LoadSubPresets<ExcelDataWorkbookLayoutPreset>();

        for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
        {
            ExcelDataWorkbookLayoutPreset preset = presets[presetIndex];

            if (!MatchesSearch(preset, searchText))
                continue;

            filteredPresets.Add(preset);
        }

        if (presetListView != null)
            presetListView.Rebuild();
    }

    /// <summary>
    /// Selects the layout preset currently linked by the active master.
    /// </summary>
    private void SelectLinkedPreset()
    {
        ExcelDataWorkbookLayoutPreset linkedPreset = selectedMasterPreset == null ? null : selectedMasterPreset.LayoutPreset;

        if (linkedPreset != null && filteredPresets.Contains(linkedPreset))
            selectedLayoutPreset = linkedPreset;
        else if (filteredPresets.Count > 0 && selectedLayoutPreset == null)
            selectedLayoutPreset = filteredPresets[0];

        if (presetListView != null && selectedLayoutPreset != null)
        {
            int selectedIndex = filteredPresets.IndexOf(selectedLayoutPreset);

            if (selectedIndex >= 0)
                presetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        BuildDetailsShell();
    }
    #endregion

    #region Details
    /// <summary>
    /// Rebuilds section buttons and content for the selected layout preset.
    /// </summary>
    private void BuildDetailsShell()
    {
        detailsRoot.Clear();

        if (selectedLayoutPreset == null)
        {
            detailsRoot.Add(new Label("Select a workbook layout preset to edit."));
            return;
        }

        detailsRoot.Add(BuildSectionButtons());
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(sectionContentRoot);
        BuildActiveDetailsSection();
    }

    /// <summary>
    /// Builds the currently active layout section.
    /// </summary>
    private void BuildActiveDetailsSection()
    {
        if (sectionContentRoot == null)
            return;

        sectionContentRoot.Clear();

        switch (activeSection)
        {
            case LayoutSectionType.Metadata:
                BuildMetadataSection();
                break;
            case LayoutSectionType.Sheets:
                BuildSheetsSection();
                break;
            case LayoutSectionType.Mappings:
                BuildMappingsSection();
                break;
            case LayoutSectionType.Workbook:
                BuildWorkbookSection();
                break;
            default:
                BuildBrushGridSection();
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(sectionContentRoot);
    }

    /// <summary>
    /// Builds internal layout section buttons.
    /// </summary>
    /// <returns>Section button row.</returns>
    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, LayoutSectionType.Metadata, "Metadata", 84f);
        AddSectionButton(buttonsRoot, LayoutSectionType.Sheets, "Sheets", 68f);
        AddSectionButton(buttonsRoot, LayoutSectionType.BrushGrid, "Brush Grid", 92f);
        AddSectionButton(buttonsRoot, LayoutSectionType.Mappings, "Mappings", 88f);
        AddSectionButton(buttonsRoot, LayoutSectionType.Workbook, "Workbook", 88f);
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one internal section selector button.
    /// </summary>
    /// <param name="parent">Parent button row.</param>
    /// <param name="sectionType">Section activated by the button.</param>
    /// <param name="label">Visible section label.</param>
    /// <param name="width">Button width in pixels.</param>
    private void AddSectionButton(VisualElement parent,
                                  LayoutSectionType sectionType,
                                  string label,
                                  float width)
    {
        Button button = new Button(() =>
        {
            activeSection = sectionType;
            BuildActiveDetailsSection();
        });
        button.text = label;
        button.tooltip = "Show " + label + " for this workbook layout preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(button, width);
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds metadata fields for the selected layout preset.
    /// </summary>
    private void BuildMetadataSection()
    {
        ScrollView scrollView = BuildSectionScrollView();
        SerializedObject serializedObject = new SerializedObject(selectedLayoutPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(scrollView, "Metadata");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "presetName", "Preset Name", "Readable workbook layout preset name.");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddDisabledPropertyField(section, serializedObject, "presetId", "Preset ID", "Stable editor identifier.");
        AddLinkedStatus(section);
    }

    /// <summary>
    /// Builds default worksheet and grid sizing fields for the selected layout preset.
    /// </summary>
    private void BuildSheetsSection()
    {
        ScrollView scrollView = BuildSectionScrollView();
        SerializedObject serializedObject = new SerializedObject(selectedLayoutPreset);
        VisualElement sheetsSection = ExcelDataTransferMasterPanelSectionUtility.CreateSection(scrollView, "Sheet Defaults");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(sheetsSection, serializedObject, "objectsSheetName", "Default Sheet Name", "Visible name used only when a new layout creates its first authoritative worksheet. Existing worksheet names live in Authoritative Sheets.");

        VisualElement gridSection = ExcelDataTransferMasterPanelSectionUtility.CreateSection(scrollView, "Grid Preview");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(gridSection, serializedObject, "defaultGridRows", "Rows", "Visible row count for the brush grid preview.");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(gridSection, serializedObject, "defaultGridColumns", "Columns", "Visible column count for the brush grid preview.");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(gridSection, serializedObject, "defaultCellWidth", "Cell Width", "Brush grid cell width in pixels.");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(gridSection, serializedObject, "defaultCellHeight", "Cell Height", "Brush grid cell height in pixels.");
    }

    /// <summary>
    /// Builds the embedded brush-grid editor for the selected layout preset.
    /// </summary>
    private void BuildBrushGridSection()
    {
        if (!IsSelectedLayoutLinked())
            sectionContentRoot.Add(new HelpBox("This layout preset is not linked to the active master. Link it from Transfer Master Presets > Sub Presets before export/import operations.", HelpBoxMessageType.Info));

        if (brushPanel == null)
            brushPanel = new ExcelDataLayoutBrushPanel(false);

        brushPanel.SetMasterPreset(selectedMasterPreset);
        brushPanel.SetLayoutPresetOverride(selectedLayoutPreset);
        sectionContentRoot.Add(brushPanel.Root);
    }

    /// <summary>
    /// Builds the authoritative sheet and sparse cell list for detailed inspection.
    /// </summary>
    private void BuildMappingsSection()
    {
        ScrollView scrollView = BuildSectionScrollView();
        SerializedObject serializedObject = new SerializedObject(selectedLayoutPreset);
        VisualElement section = ExcelDataTransferMasterPanelSectionUtility.CreateSection(scrollView, "Authoritative Sheets and Cells");
        ExcelDataLinkedSubPresetPanelFieldUtility.AddPropertyField(section, serializedObject, "sheetDefinitions", "Authoritative Sheets", "Ordered workbook sheets and every exact Data Field or Literal Text cell. This is the only import/export layout source.");
    }

    /// <summary>
    /// Builds workbook path controls and import/export operations that affect the selected layout.
    /// </summary>
    private void BuildWorkbookSection()
    {
        ExcelDataWorkbookLayoutPresetPanelWorkbookUtility.BuildWorkbookSection(sectionContentRoot,
                                                                               selectedMasterPreset,
                                                                               selectedLayoutPreset,
                                                                               IsSelectedLayoutLinked(),
                                                                               operationStatus,
                                                                               SetOperationStatus,
                                                                               BuildActiveDetailsSection);
    }
    #endregion

    #region Actions
    /// <summary>
    /// Creates one workbook layout preset and selects it.
    /// </summary>
    private void CreateLayoutPreset()
    {
        ExcelDataWorkbookLayoutPreset createdPreset =
            ExcelDataTransferSubPresetAssetUtility.CreateSubPreset<ExcelDataWorkbookLayoutPreset>("ExcelDataWorkbookLayoutPreset",
                                                                                                  "Excel Workbook Layout");
        ExcelDataTransferDraftSession.MarkDirty();
        SelectLayoutAfterListRefresh(createdPreset);
        SetBrowserStatus("Created layout preset " + createdPreset.name + ".");
    }

    /// <summary>
    /// Duplicates the selected workbook layout preset and selects the copy.
    /// </summary>
    private void DuplicateLayoutPreset()
    {
        if (selectedLayoutPreset == null)
        {
            SetBrowserStatus("Select a layout preset before duplicating.");
            return;
        }

        ExcelDataWorkbookLayoutPreset duplicatedPreset =
            ExcelDataTransferSubPresetAssetUtility.DuplicateSubPreset(selectedLayoutPreset);

        if (duplicatedPreset == null)
        {
            SetBrowserStatus("Could not duplicate the selected layout preset.");
            return;
        }

        ExcelDataTransferDraftSession.MarkDirty();
        SelectLayoutAfterListRefresh(duplicatedPreset);
        SetBrowserStatus("Duplicated layout preset " + duplicatedPreset.name + ".");
    }

    /// <summary>
    /// Deletes the selected workbook layout preset when it is not linked by a transfer master.
    /// </summary>
    private void DeleteLayoutPreset()
    {
        if (selectedLayoutPreset == null)
        {
            SetBrowserStatus("Select a layout preset before deleting.");
            return;
        }

        string deletedPresetName = selectedLayoutPreset.name;
        string blockingMasterName;

        if (!ExcelDataTransferSubPresetAssetUtility.DeleteSubPresetIfUnreferenced(selectedLayoutPreset, out blockingMasterName))
        {
            SetBrowserStatus(string.IsNullOrWhiteSpace(blockingMasterName)
                ? "Could not delete the selected layout preset."
                : "Cannot delete: still referenced by " + blockingMasterName + ".");
            return;
        }

        selectedLayoutPreset = null;
        ExcelDataTransferDraftSession.MarkDirty();
        RefreshPresetList();
        SelectLinkedPreset();
        SetBrowserStatus("Deleted layout preset " + deletedPresetName + ".");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears the search field, refreshes the browser and selects one layout preset.
    /// </summary>
    /// <param name="preset">Layout preset asset to select after refresh.</param>
    private void SelectLayoutAfterListRefresh(ExcelDataWorkbookLayoutPreset preset)
    {
        if (searchField != null)
            searchField.SetValueWithoutNotify(string.Empty);

        selectedLayoutPreset = preset;
        RefreshPresetList();
        BuildDetailsShell();

        if (presetListView == null || selectedLayoutPreset == null)
            return;

        int selectedIndex = filteredPresets.IndexOf(selectedLayoutPreset);

        if (selectedIndex >= 0)
            presetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
    }

    /// <summary>
    /// Updates the browser status label when it exists.
    /// </summary>
    /// <param name="message">Status text to show.</param>
    private void SetBrowserStatus(string message)
    {
        if (browserStatusLabel != null)
            browserStatusLabel.text = message;
    }

    /// <summary>
    /// Builds a scroll view for non-grid sections.
    /// </summary>
    /// <returns>Configured scroll view.</returns>
    private ScrollView BuildSectionScrollView()
    {
        ScrollView scrollView = new ScrollView();
        scrollView.style.flexGrow = 1f;
        sectionContentRoot.Add(scrollView);
        return scrollView;
    }

    /// <summary>
    /// Checks whether the selected layout preset is linked to the active master.
    /// </summary>
    /// <returns>True when selected and linked layout assets match.</returns>
    private bool IsSelectedLayoutLinked()
    {
        return selectedLayoutPreset != null &&
               selectedMasterPreset != null &&
               selectedMasterPreset.LayoutPreset == selectedLayoutPreset;
    }

    /// <summary>
    /// Adds a compact linked/unlinked status label to a section.
    /// </summary>
    /// <param name="parent">Section receiving the status label.</param>
    private void AddLinkedStatus(VisualElement parent)
    {
        Label label = new Label(IsSelectedLayoutLinked() ? "Linked to active master preset." : "Not linked to active master preset.");
        label.tooltip = "Shows whether the selected layout preset is the one currently referenced by the active master preset.";
        label.style.marginTop = 6f;
        parent.Add(label);
    }

    /// <summary>
    /// Stores operation status so the workbook section can redraw the latest result after tab changes.
    /// </summary>
    /// <param name="message">Status text to persist.</param>
    private void SetOperationStatus(string message)
    {
        operationStatus = message;
    }

    /// <summary>
    /// Checks whether one layout preset matches the current browser search text.
    /// </summary>
    /// <param name="preset">Layout preset to test.</param>
    /// <param name="searchText">Search text entered by the user.</param>
    /// <returns>True when the layout preset remains visible.</returns>
    private bool MatchesSearch(ExcelDataWorkbookLayoutPreset preset, string searchText)
    {
        if (preset == null)
            return false;

        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string normalizedSearch = searchText.ToLowerInvariant();
        string displayName = ExcelDataLinkedSubPresetPanelContextUtility.ResolvePresetDisplayName(preset).ToLowerInvariant();
        return displayName.Contains(normalizedSearch) || preset.name.ToLowerInvariant().Contains(normalizedSearch);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Internal section tabs used by the workbook layout preset panel.
    /// </summary>
    private enum LayoutSectionType
    {
        Metadata = 0,
        Sheets = 1,
        BrushGrid = 2,
        Mappings = 3,
        Workbook = 4
    }
    #endregion
}
