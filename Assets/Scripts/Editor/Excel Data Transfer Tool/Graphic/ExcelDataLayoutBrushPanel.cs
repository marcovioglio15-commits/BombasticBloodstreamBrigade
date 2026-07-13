using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Hosts the workbook layout brush grid, field picker and first selective export actions.
/// </summary>
public sealed class ExcelDataLayoutBrushPanel
{
    #region Fields
    private readonly VisualElement root;
    private readonly VisualElement gridRoot;
    private readonly bool showMasterPresetField;
    private readonly List<ExcelDataFieldCatalogEntry> allEntries = new List<ExcelDataFieldCatalogEntry>();
    private readonly List<ExcelDataFieldCatalogEntry> filteredEntries = new List<ExcelDataFieldCatalogEntry>();

    private ObjectField masterPresetField;
    private ToolbarSearchField searchField;
    private EnumField domainField;
    private EnumField categoryField;
    private EnumField dataKindField;
    private EnumField listModeField;
    private ToolbarSearchField sourceSearchField;
    private PopupField<string> savedBrushField;
    private ColorField brushColorField;
    private IntegerField rowCountField;
    private IntegerField columnCountField;
    private IntegerField cellWidthField;
    private IntegerField cellHeightField;
    private ListView listView;
    private Label statusLabel;
    private Label selectionLabel;

    private ExcelDataTransferMasterPreset selectedMasterPreset;
    private ExcelDataWorkbookLayoutPreset layoutPresetOverride;
    private ExcelDataFieldCatalogEntry selectedEntry;
    private int selectedRowIndex = 1;
    private int selectedColumnIndex = 1;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    internal ExcelDataTransferMasterPreset SelectedMasterPreset
    {
        get
        {
            return selectedMasterPreset;
        }
    }

    internal ExcelDataFieldCatalogEntry SelectedEntry
    {
        get
        {
            return selectedEntry;
        }
    }

    internal List<ExcelDataFieldCatalogEntry> FilteredEntries
    {
        get
        {
            return filteredEntries;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Builds the brush layout panel and shows its own master preset selector.
    /// </summary>
    public ExcelDataLayoutBrushPanel()
        : this(true)
    {
    }

    /// <summary>
    /// Builds the brush layout panel and loads the selected master preset.
    /// </summary>
    /// <param name="newShowMasterPresetField">True when the panel should show its own master preset field.</param>
    public ExcelDataLayoutBrushPanel(bool newShowMasterPresetField)
    {
        showMasterPresetField = newShowMasterPresetField;
        selectedMasterPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();

        root = new VisualElement();
        root.style.flexGrow = 1f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(380f);
        root.Add(splitView);

        splitView.Add(BuildBrushPane());

        VisualElement gridPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(gridPane);
        gridPane.Add(ExcelDataLayoutBrushPanelToolbarUtility.BuildGridToolbar(UpdateLayoutInt,
                                                                              RebuildGrid,
                                                                              out rowCountField,
                                                                              out columnCountField,
                                                                              out cellWidthField,
                                                                              out cellHeightField));

        gridRoot = new VisualElement();
        gridRoot.style.flexGrow = 1f;
        gridPane.Add(gridRoot);
        splitView.Add(gridPane);

        RefreshCatalog();
        RefreshPresetFields();
        RebuildGrid();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Refreshes master bindings and the field catalog after draft session changes.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        selectedMasterPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();
        if (masterPresetField != null)
            masterPresetField.SetValueWithoutNotify(selectedMasterPreset);
        RefreshCatalog();
        RefreshPresetFields();
        RebuildGrid();
    }

    /// <summary>
    /// Assigns the master preset provided by the parent transfer panel.
    /// </summary>
    /// <param name="masterPreset">Master preset whose layout and import/export selections should be edited.</param>
    public void SetMasterPreset(ExcelDataTransferMasterPreset masterPreset)
    {
        selectedMasterPreset = masterPreset;
        if (masterPresetField != null)
            masterPresetField.SetValueWithoutNotify(selectedMasterPreset);
        RefreshPresetFields();
        RebuildGrid();
    }

    /// <summary>
    /// Assigns a layout preset selected by the parent layout browser without changing the active master preset.
    /// </summary>
    /// <param name="layoutPreset">Layout preset edited by the brush grid.</param>
    public void SetLayoutPresetOverride(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        layoutPresetOverride = layoutPreset;
        RefreshPresetFields();
        RebuildGrid();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the left field picker and export action pane.
    /// </summary>
    /// <returns>Configured brush pane.</returns>
    private VisualElement BuildBrushPane()
    {
        VisualElement pane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(pane);

        Label titleLabel = new Label("Layout Brush");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6f;
        pane.Add(titleLabel);

        if (showMasterPresetField)
        {
            masterPresetField = new ObjectField("Master");
            masterPresetField.objectType = typeof(ExcelDataTransferMasterPreset);
            masterPresetField.allowSceneObjects = false;
            masterPresetField.tooltip = "Master preset whose layout/export settings are edited by this brush grid.";
            masterPresetField.RegisterValueChangedCallback(evt =>
            {
                selectedMasterPreset = evt.newValue as ExcelDataTransferMasterPreset;
                ExcelDataTransferAssetUtility.SaveSelectedMasterPreset(selectedMasterPreset);
                RefreshPresetFields();
                RebuildGrid();
            });
            pane.Add(masterPresetField);
        }

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Search by path, asset, type or aliases such as ref, list, wave, bool, enum, number, scaling.";
        searchField.RegisterValueChangedCallback(evt => ApplyFilters());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        pane.Add(searchField);

        domainField = CreateEnumFilter("Domain", ExcelDataTransferDomain.All);
        categoryField = CreateEnumFilter("Category", ExcelDataFieldCategory.All);
        dataKindField = CreateEnumFilter("Kind", ExcelDataBrushDataKind.All);
        listModeField = CreateEnumFilter("Lists", ExcelDataListElementFilterMode.HideConcreteListElements);
        sourceSearchField = CreateSourceSearchFilter();
        savedBrushField = CreateSavedBrushField();
        brushColorField = ExcelDataLayoutBrushPaletteUtility.CreateBrushColorField();
        pane.Add(domainField);
        pane.Add(categoryField);
        pane.Add(dataKindField);
        pane.Add(listModeField);
        Label sourceSearchLabel = new Label("Source Search");
        sourceSearchLabel.tooltip = "Text filter for source asset types. This replaces the previous oversized source dropdown.";
        pane.Add(sourceSearchLabel);
        pane.Add(sourceSearchField);
        pane.Add(savedBrushField);
        pane.Add(brushColorField);

        Button saveBrushButton = new Button(SaveCurrentBrushConfiguration);
        saveBrushButton.text = "Save Brush";
        saveBrushButton.tooltip = "Save the current filters and color into the linked brush palette preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(saveBrushButton, 112f);
        pane.Add(saveBrushButton);

        listView = BuildListView();
        pane.Add(listView);

        Button addSelectedButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.AddSelectedFieldToExport(this));
        addSelectedButton.text = "Add Selected";
        addSelectedButton.tooltip = "Add the selected catalog field to the export preset selection.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(addSelectedButton, 112f);
        pane.Add(addSelectedButton);

        Button addFilteredButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.AddFilteredFieldsToExport(this));
        addFilteredButton.text = "Add Filtered";
        addFilteredButton.tooltip = "Add all currently filtered catalog fields to the export preset selection.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(addFilteredButton, 112f);
        pane.Add(addFilteredButton);

        Button addSelectedImportButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.AddSelectedFieldToImport(this));
        addSelectedImportButton.text = "Add Import";
        addSelectedImportButton.tooltip = "Add the selected catalog field to the import preset selection.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(addSelectedImportButton, 112f);
        pane.Add(addSelectedImportButton);

        Button addFilteredImportButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.AddFilteredFieldsToImport(this));
        addFilteredImportButton.text = "Import Filter";
        addFilteredImportButton.tooltip = "Add all currently filtered catalog fields to the import preset selection.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(addFilteredImportButton, 112f);
        pane.Add(addFilteredImportButton);

        Button clearButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.ClearExportSelection(this));
        clearButton.text = "Clear Export";
        clearButton.tooltip = "Clear explicit export selections. Empty selection exports all fields allowed by preset filters.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(clearButton, 112f);
        pane.Add(clearButton);

        Button clearImportButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.ClearImportSelection(this));
        clearImportButton.text = "Clear Import";
        clearImportButton.tooltip = "Clear explicit import selections. Empty selection imports mapped fields allowed by preset filters.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(clearImportButton, 112f);
        pane.Add(clearImportButton);

        Button exportButton = new Button(() => ExcelDataLayoutBrushPanelActionsUtility.ExportWorkbook(this));
        exportButton.text = "Export .xlsx";
        exportButton.tooltip = "Export the selected preset to the configured .xlsx workbook path.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(exportButton, 112f);
        pane.Add(exportButton);

        selectionLabel = new Label();
        selectionLabel.style.marginTop = 8f;
        selectionLabel.style.whiteSpace = WhiteSpace.Normal;
        pane.Add(selectionLabel);

        statusLabel = new Label();
        statusLabel.style.marginTop = 8f;
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        pane.Add(statusLabel);
        return pane;
    }

    /// <summary>
    /// Creates one enum filter dropdown.
    /// </summary>
    /// <param name="label">Dropdown label.</param>
    /// <param name="initialValue">Initial enum value.</param>
    /// <returns>Configured enum field.</returns>
    private EnumField CreateEnumFilter(string label, Enum initialValue)
    {
        EnumField field = new EnumField(label, initialValue);
        field.tooltip = "Filter fields before painting them into workbook cells.";
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        return field;
    }

    /// <summary>
    /// Creates the source type text filter used instead of a large source dropdown.
    /// </summary>
    /// <returns>Configured source search field.</returns>
    private ToolbarSearchField CreateSourceSearchFilter()
    {
        ToolbarSearchField field = new ToolbarSearchField();
        field.tooltip = "Filter source asset types by text, for example PlayerControllerPreset or EnemyWavePreset.";
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        GameManagementPanelLayoutUtility.ConfigureSearchField(field);
        return field;
    }

    /// <summary>
    /// Creates the saved brush selector backed by the linked brush palette preset.
    /// </summary>
    /// <returns>Configured saved brush dropdown.</returns>
    private PopupField<string> CreateSavedBrushField()
    {
        List<string> options = ExcelDataLayoutBrushPaletteUtility.BuildSavedBrushOptions(GetBrushPalettePreset());
        PopupField<string> field = new PopupField<string>("Brush", options, 0);
        field.tooltip = "Apply a saved brush configuration from the linked brush palette preset.";
        field.RegisterValueChangedCallback(evt => ApplySavedBrushConfiguration(evt.newValue));
        return field;
    }

    /// <summary>
    /// Builds the filtered field ListView used as a brush source.
    /// </summary>
    /// <returns>Configured ListView.</returns>
    private ListView BuildListView()
    {
        ListView createdListView = new ListView();
        createdListView.itemsSource = filteredEntries;
        createdListView.makeItem = MakeListItem;
        createdListView.bindItem = BindListItem;
        createdListView.selectionChanged += OnFieldSelectionChanged;
        createdListView.fixedItemHeight = 20f;
        createdListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        GameManagementPanelLayoutUtility.ConfigureListView(createdListView);
        return createdListView;
    }

    /// <summary>
    /// Creates one field picker row visual.
    /// </summary>
    /// <returns>Row label visual element.</returns>
    private VisualElement MakeListItem()
    {
        Label label = new Label();
        label.style.whiteSpace = WhiteSpace.NoWrap;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        return label;
    }

    /// <summary>
    /// Binds one field picker row.
    /// </summary>
    /// <param name="element">Row element to bind.</param>
    /// <param name="index">Filtered entry index.</param>
    private void BindListItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= filteredEntries.Count)
        {
            label.text = string.Empty;
            return;
        }

        ExcelDataFieldCatalogEntry entry = filteredEntries[index];
        label.text = entry.Domain + " | " + entry.DataKind + " | " + entry.PathTemplate;
        label.tooltip = entry.DisplayName + "\n" + entry.AssetPath;
    }
    #endregion

    #region Catalog
    /// <summary>
    /// Rebuilds the field catalog from current project assets.
    /// </summary>
    private void RefreshCatalog()
    {
        allEntries.Clear();
        allEntries.AddRange(ExcelDataFieldCatalogBuilder.BuildCatalog());
        RefreshSavedBrushOptions();
        ApplyFilters();
    }

    /// <summary>
    /// Applies active smart filters to the field picker list.
    /// </summary>
    private void ApplyFilters()
    {
        filteredEntries.Clear();
        string searchText = searchField == null ? string.Empty : searchField.value;
        ExcelDataTransferDomain domainFilter = domainField == null ? ExcelDataTransferDomain.All : (ExcelDataTransferDomain)domainField.value;
        ExcelDataFieldCategory categoryFilter = categoryField == null ? ExcelDataFieldCategory.All : (ExcelDataFieldCategory)categoryField.value;
        ExcelDataBrushDataKind dataKindFilter = dataKindField == null ? ExcelDataBrushDataKind.All : (ExcelDataBrushDataKind)dataKindField.value;
        ExcelDataListElementFilterMode listFilter = listModeField == null ? ExcelDataListElementFilterMode.All : (ExcelDataListElementFilterMode)listModeField.value;
        string sourceFilter = sourceSearchField == null ? string.Empty : sourceSearchField.value;

        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = allEntries[entryIndex];

            if (!ExcelDataFieldCatalogFilterUtility.MatchesFilters(entry,
                                                                   searchText,
                                                                   domainFilter,
                                                                   categoryFilter,
                                                                   dataKindFilter,
                                                                   listFilter,
                                                                   sourceFilter))
                continue;

            filteredEntries.Add(entry);
        }

        if (listView != null)
            listView.Rebuild();

        UpdateSelectionLabel();
    }

    /// <summary>
    /// Rebuilds saved brush dropdown options while preserving the selected brush when still valid.
    /// </summary>
    private void RefreshSavedBrushOptions()
    {
        ExcelDataLayoutBrushPaletteUtility.RefreshSavedBrushOptions(savedBrushField, GetBrushPalettePreset());
    }

    /// <summary>
    /// Stores the selected field entry used by paint and export-selection actions.
    /// </summary>
    /// <param name="selection">Selected ListView payload.</param>
    private void OnFieldSelectionChanged(IEnumerable<object> selection)
    {
        selectedEntry = null;

        foreach (object selectedObject in selection)
        {
            selectedEntry = selectedObject as ExcelDataFieldCatalogEntry;

            if (selectedEntry != null)
                break;
        }

        UpdateSelectionLabel();
    }
    #endregion

    #region Grid
    /// <summary>
    /// Rebuilds the visible brush grid from the selected layout preset mappings.
    /// </summary>
    private void RebuildGrid()
    {
        ExcelDataLayoutBrushGridUtility.RebuildGrid(gridRoot, GetLayoutPreset(), GetBrushPalettePreset(), allEntries, PaintOrSelectCell);
    }

    /// <summary>
    /// Paints the selected field into the clicked cell or only selects the cell when no field is selected.
    /// </summary>
    /// <param name="rowIndex">One-based grid row index.</param>
    /// <param name="columnIndex">One-based grid column index.</param>
    private void PaintOrSelectCell(int rowIndex, int columnIndex)
    {
        selectedRowIndex = rowIndex;
        selectedColumnIndex = columnIndex;

        if (selectedEntry == null)
        {
            UpdateSelectionLabel();
            return;
        }

        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();

        if (layoutPreset == null)
            return;

        ExcelDataWorkbookLayoutAuthoringUtility.PaintDataFieldCell(layoutPreset,
                                                                  selectedEntry,
                                                                  rowIndex,
                                                                  columnIndex);
        EditorUtility.SetDirty(layoutPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        RebuildGrid();
        UpdateSelectionLabel();
    }

    #endregion

    #region Preset Helpers
    /// <summary>
    /// Refreshes object fields and dimension controls from the selected master preset.
    /// </summary>
    private void RefreshPresetFields()
    {
        if (selectedMasterPreset == null)
            selectedMasterPreset = ExcelDataTransferAssetUtility.LoadSelectedOrDefaultMasterPreset();

        if (masterPresetField != null)
            masterPresetField.SetValueWithoutNotify(selectedMasterPreset);

        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();

        if (layoutPreset != null)
        {
            rowCountField.SetValueWithoutNotify(layoutPreset.DefaultGridRows);
            columnCountField.SetValueWithoutNotify(layoutPreset.DefaultGridColumns);
            cellWidthField.SetValueWithoutNotify(layoutPreset.DefaultCellWidth);
            cellHeightField.SetValueWithoutNotify(layoutPreset.DefaultCellHeight);
        }

        RefreshSavedBrushOptions();
        UpdateSelectionLabel();
    }

    /// <summary>
    /// Updates an integer field on the layout preset through SerializedObject so authored data remains Unity-serialized.
    /// </summary>
    /// <param name="propertyName">Serialized layout preset property name.</param>
    /// <param name="newValue">New value entered by the user.</param>
    private void UpdateLayoutInt(string propertyName, int newValue)
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = GetLayoutPreset();

        if (layoutPreset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(layoutPreset);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        property.intValue = newValue;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(layoutPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        RebuildGrid();
        UpdateSelectionLabel();
    }

    /// <summary>
    /// Gets the layout preset linked by the selected master preset.
    /// </summary>
    /// <returns>Layout preset, or null when the master graph is incomplete.</returns>
    private ExcelDataWorkbookLayoutPreset GetLayoutPreset()
    {
        if (layoutPresetOverride != null)
            return layoutPresetOverride;

        if (selectedMasterPreset == null)
            return null;

        return selectedMasterPreset.LayoutPreset;
    }

    /// <summary>
    /// Gets the export preset linked by the selected master preset.
    /// </summary>
    /// <returns>Export preset, or null when the master graph is incomplete.</returns>
    internal ExcelDataExportPreset GetExportPreset()
    {
        if (selectedMasterPreset == null)
            return null;

        return selectedMasterPreset.ExportPreset;
    }

    /// <summary>
    /// Gets the import preset linked by the selected master preset.
    /// </summary>
    /// <returns>Import preset, or null when the master graph is incomplete.</returns>
    internal ExcelDataImportPreset GetImportPreset()
    {
        if (selectedMasterPreset == null)
            return null;

        return selectedMasterPreset.ImportPreset;
    }

    /// <summary>
    /// Gets the brush palette preset linked by the selected master preset.
    /// </summary>
    /// <returns>Brush palette preset, or null when the graph is incomplete.</returns>
    internal ExcelDataBrushPalettePreset GetBrushPalettePreset()
    {
        if (selectedMasterPreset == null)
            return null;

        return selectedMasterPreset.BrushPalettePreset;
    }
    #endregion

    #region Saved Brushes
    /// <summary>
    /// Applies a saved brush configuration to all filter controls.
    /// </summary>
    /// <param name="optionLabel">Visible brush option selected by the user.</param>
    private void ApplySavedBrushConfiguration(string optionLabel)
    {
        if (ExcelDataLayoutBrushPaletteUtility.ApplySavedBrushConfiguration(GetBrushPalettePreset(),
                                                                            optionLabel,
                                                                            domainField,
                                                                            categoryField,
                                                                            dataKindField,
                                                                            listModeField,
                                                                            sourceSearchField,
                                                                            brushColorField))
            ApplyFilters();
    }

    /// <summary>
    /// Saves the current filter and color state into the linked brush palette preset.
    /// </summary>
    private void SaveCurrentBrushConfiguration()
    {
        string selectedOption;
        string statusMessage;
        bool saved = ExcelDataLayoutBrushPaletteUtility.SaveCurrentBrushConfiguration(GetBrushPalettePreset(),
                                                                                      domainField,
                                                                                      categoryField,
                                                                                      dataKindField,
                                                                                      listModeField,
                                                                                      sourceSearchField,
                                                                                      brushColorField,
                                                                                      searchField,
                                                                                      out selectedOption,
                                                                                      out statusMessage);
        SetStatus(statusMessage);

        if (!saved)
            return;

        RefreshSavedBrushOptions();

        if (savedBrushField != null)
            savedBrushField.SetValueWithoutNotify(selectedOption);
    }
    #endregion

    #region UI Helpers
    /// <summary>
    /// Updates the selection summary label.
    /// </summary>
    internal void UpdateSelectionLabel()
    {
        if (selectionLabel == null)
            return;

        ExcelDataExportPreset exportPreset = GetExportPreset();
        ExcelDataImportPreset importPreset = GetImportPreset();
        int exportSelectionCount = exportPreset == null ? 0 : exportPreset.SelectedFields.Count;
        int importSelectionCount = importPreset == null ? 0 : importPreset.SelectedFields.Count;
        string fieldText = selectedEntry == null ? "No field selected" : selectedEntry.PathTemplate;
        selectionLabel.text = "Selected Cell: " + selectedRowIndex + "," + selectedColumnIndex +
                              "\nSelected Field: " + fieldText +
                              "\nFiltered Fields: " + filteredEntries.Count + " / " + allEntries.Count +
                              "\nImport Selections: " + importSelectionCount +
                              "\nExport Selections: " + exportSelectionCount;
    }

    /// <summary>
    /// Updates the status label with a short user-facing message.
    /// </summary>
    /// <param name="message">Status message to show.</param>
    internal void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;
    }

    /// <summary>
    /// Checks whether workbook operations would use the layout currently edited by this brush panel.
    /// </summary>
    /// <returns>True when the active layout is the layout linked by the selected master preset.</returns>
    internal bool IsEditingLinkedLayoutPreset()
    {
        return layoutPresetOverride == null ||
               selectedMasterPreset != null && selectedMasterPreset.LayoutPreset == layoutPresetOverride;
    }
    #endregion

    #endregion
}
