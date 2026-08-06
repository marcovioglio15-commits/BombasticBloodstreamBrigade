using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Owns the workbook brush sidebar controls, virtualized catalog and saved-brush interactions.
/// </summary>
internal sealed class ExcelDataLayoutBrushPanelControls
{
    #region Constants
    private const string WorksheetFoldoutKey = "ExcelDataTransfer.LayoutBrush.Worksheet";
    private const string BrushStyleFoldoutKey = "ExcelDataTransfer.LayoutBrush.BrushStyle";
    private const string CatalogFiltersFoldoutKey = "ExcelDataTransfer.LayoutBrush.CatalogFilters";
    private const string FieldResultsFoldoutKey = "ExcelDataTransfer.LayoutBrush.FieldResults";
    private const string StatusFoldoutKey = "ExcelDataTransfer.LayoutBrush.Status";
    #endregion

    #region Fields
    private readonly List<ExcelDataFieldCatalogEntry> allEntries = new List<ExcelDataFieldCatalogEntry>();
    private readonly List<ExcelDataFieldCatalogEntry> filteredEntries = new List<ExcelDataFieldCatalogEntry>();
    private readonly Func<ExcelDataBrushPalettePreset> brushPaletteResolver;
    private readonly ExcelDataLayoutBrushInspector brushInspector;
    private readonly Action<ExcelDataTransferMasterPreset> masterChanged;
    private readonly Action<string> sheetChanged;
    private readonly Action catalogSelectionChanged;

    private readonly VisualElement root;
    private readonly Foldout brushStyleRoot;
    private readonly VisualElement fieldCatalogRoot;
    private readonly ObjectField masterPresetField;
    private readonly PopupField<string> sheetField;
    private ToolbarSearchField searchField;
    private EnumField domainField;
    private PopupField<ExcelDataBrushDataKind> dataKindField;
    private EnumField listModeField;
    private ToolbarSearchField sourceTypeSearchField;
    private ToolbarSearchField sourceAssetSearchField;
    private VisualElement sourceAssetRoot;
    private PopupField<string> savedBrushField;
    private ColorField brushColorField;
    private ColorField brushTextColorField;
    private ListView listView;
    private Label selectionLabel;
    private Label statusLabel;
    private Foldout statusFoldout;

    private ExcelDataFieldCatalogEntry selectedEntry;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }

    public IReadOnlyList<ExcelDataFieldCatalogEntry> AllEntries
    {
        get
        {
            return allEntries;
        }
    }

    public ExcelDataFieldCatalogEntry SelectedEntry
    {
        get
        {
            return selectedEntry;
        }
    }

    public ObjectField MasterPresetField
    {
        get
        {
            return masterPresetField;
        }
    }

    public PopupField<string> SheetField
    {
        get
        {
            return sheetField;
        }
    }

    public ColorField BrushColorField
    {
        get
        {
            return brushColorField;
        }
    }

    public ColorField BrushTextColorField
    {
        get
        {
            return brushTextColorField;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Builds the sidebar and wires its changes to the owning workbook panel.
    /// </summary>
    /// <param name="showMasterPresetField">True when an independent master selector is required.</param>
    /// <param name="brushInspector">Mode and selected-cell inspector displayed in the sidebar.</param>
    /// <param name="newBrushPaletteResolver">Resolver for the palette linked by the active master preset.</param>
    /// <param name="newMasterChanged">Callback invoked after selecting another master preset.</param>
    /// <param name="newSheetChanged">Callback invoked after selecting another worksheet.</param>
    /// <param name="newCatalogSelectionChanged">Callback invoked after catalog selection or filtering changes.</param>
    public ExcelDataLayoutBrushPanelControls(bool showMasterPresetField,
                                             ExcelDataLayoutBrushInspector brushInspector,
                                             Func<ExcelDataBrushPalettePreset> newBrushPaletteResolver,
                                             Action<ExcelDataTransferMasterPreset> newMasterChanged,
                                             Action<string> newSheetChanged,
                                             Action newCatalogSelectionChanged)
    {
        brushPaletteResolver = newBrushPaletteResolver;
        this.brushInspector = brushInspector;
        masterChanged = newMasterChanged;
        sheetChanged = newSheetChanged;
        catalogSelectionChanged = newCatalogSelectionChanged;

        root = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(root);
        root.style.overflow = Overflow.Hidden;
        Label titleLabel = new Label("Layout Brush");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6f;
        root.Add(titleLabel);

        Foldout worksheetFoldout = ManagementToolFoldoutStateUtility.CreateFoldout("Worksheet",
                                                                                    WorksheetFoldoutKey,
                                                                                    true);
        worksheetFoldout.tooltip = "Choose the transfer graph and grid-authoritative worksheet edited by the layout brush.";
        worksheetFoldout.style.flexShrink = 0f;

        if (showMasterPresetField)
        {
            masterPresetField = BuildMasterPresetField();
            worksheetFoldout.Add(masterPresetField);
        }

        sheetField = BuildSheetField();
        sheetField.style.flexShrink = 0f;
        worksheetFoldout.Add(sheetField);
        root.Add(worksheetFoldout);
        root.Add(brushInspector.Root);
        brushStyleRoot = BuildBrushStyleSection();
        brushStyleRoot.style.flexShrink = 0f;
        root.Add(brushStyleRoot);
        fieldCatalogRoot = BuildFieldCatalogSection();
        fieldCatalogRoot.AddToClassList("excel-data-field-catalog-root");
        fieldCatalogRoot.style.flexGrow = 1f;
        fieldCatalogRoot.style.flexShrink = 1f;
        fieldCatalogRoot.style.minHeight = 180f;
        fieldCatalogRoot.style.overflow = Overflow.Hidden;
        root.Add(fieldCatalogRoot);
        root.Add(BuildStatusSection());
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Rebuilds the project field catalog and reapplies all active filters.
    /// </summary>
    public void RefreshCatalog()
    {
        allEntries.Clear();
        allEntries.AddRange(ExcelDataFieldCatalogBuilder.BuildCatalog());
        RefreshSavedBrushOptions();
        RefreshDataKindChoices();
    }

    /// <summary>
    /// Rebuilds the Kind dropdown from current catalog capabilities and active transfer direction.
    /// </summary>
    public void RefreshDataKindChoices()
    {
        if (dataKindField == null)
            return;

        ExcelDataBrushDataKind previousKind = dataKindField.value;
        List<ExcelDataBrushDataKind> choices =
            ExcelDataBrushDataKindFilterUtility.BuildChoices(allEntries, brushInspector.Direction);
        dataKindField.choices = choices;
        dataKindField.SetValueWithoutNotify(choices.Contains(previousKind)
            ? previousKind
            : ExcelDataBrushDataKind.All);
        ApplyFilters();
    }

    /// <summary>
    /// Rebuilds saved-brush choices while preserving a valid current option.
    /// </summary>
    public void RefreshSavedBrushOptions()
    {
        ExcelDataLayoutBrushPaletteUtility.RefreshSavedBrushOptions(savedBrushField, brushPaletteResolver());
    }

    /// <summary>
    /// Updates mode-dependent style and catalog visibility.
    /// </summary>
    /// <param name="mode">Current workbook brush mode.</param>
    public void SetModeVisibility(ExcelDataLayoutBrushMode mode)
    {
        brushStyleRoot.style.display = mode == ExcelDataLayoutBrushMode.Data ||
                                       mode == ExcelDataLayoutBrushMode.Text ||
                                       mode == ExcelDataLayoutBrushMode.Formula
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        fieldCatalogRoot.style.display = mode == ExcelDataLayoutBrushMode.Data
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Updates the compact mode, field and filter-result summary.
    /// </summary>
    /// <param name="mode">Current workbook brush mode.</param>
    public void UpdateSelectionLabel(ExcelDataLayoutBrushMode mode)
    {
        string fieldText = selectedEntry == null ? "No field selected" : selectedEntry.SerializedPath;
        selectionLabel.text = "Mode: " + mode +
                              "\nSelected Field: " + fieldText +
                              "\nFiltered Fields: " + filteredEntries.Count + " / " + allEntries.Count;
    }

    /// <summary>
    /// Updates the sidebar status with one authoring result or validation warning.
    /// </summary>
    /// <param name="message">User-facing result.</param>
    public void SetStatus(string message)
    {
        statusLabel.text = string.IsNullOrWhiteSpace(message) ? "No pending layout operation." : message;

        if (!string.IsNullOrWhiteSpace(message) && statusFoldout != null)
            statusFoldout.value = true;
    }

    /// <summary>
    /// Resolves the stable ID of the currently selected saved brush.
    /// </summary>
    /// <returns>Stable brush ID, or an empty string.</returns>
    public string GetSelectedBrushId()
    {
        return ExcelDataLayoutBrushPaletteUtility.ResolveBrushId(brushPaletteResolver(), savedBrushField.value);
    }
    #endregion

    #region Layout Building
    /// <summary>
    /// Builds the optional master preset selector.
    /// </summary>
    /// <returns>Configured master preset field.</returns>
    private ObjectField BuildMasterPresetField()
    {
        ObjectField field = new ObjectField("Master");
        field.objectType = typeof(ExcelDataTransferMasterPreset);
        field.allowSceneObjects = false;
        field.tooltip = "Master preset whose linked workbook layout and brush palette are edited.";
        field.RegisterValueChangedCallback(evt => masterChanged(evt.newValue as ExcelDataTransferMasterPreset));
        return field;
    }

    /// <summary>
    /// Builds the grid-authoritative worksheet selector.
    /// </summary>
    /// <returns>Configured sheet dropdown.</returns>
    private PopupField<string> BuildSheetField()
    {
        List<string> options = new List<string> { "No Worksheet" };
        PopupField<string> field = new PopupField<string>("Sheet", options, 0);
        field.tooltip = "Choose which grid-authoritative worksheet is edited. Coordinates are local to this sheet.";
        field.RegisterValueChangedCallback(evt => sheetChanged(evt.newValue));
        return field;
    }

    /// <summary>
    /// Builds saved-brush style controls used by Data and Text modes.
    /// </summary>
    /// <returns>Configured collapsible style section.</returns>
    private Foldout BuildBrushStyleSection()
    {
        Foldout section = ManagementToolFoldoutStateUtility.CreateFoldout("Brush Style",
                                                                          BrushStyleFoldoutKey,
                                                                          true);
        section.tooltip = "Choose a retained saved brush, inspect its background and text colors, and store reusable brush configurations.";
        List<string> options = ExcelDataLayoutBrushPaletteUtility.BuildSavedBrushOptions(brushPaletteResolver());
        savedBrushField = new PopupField<string>("Brush", options, 0);
        savedBrushField.tooltip = "Apply a named saved brush and retain its stable ID on newly painted cells.";
        savedBrushField.RegisterValueChangedCallback(evt => ApplySavedBrushConfiguration(evt.newValue));
        brushColorField = ExcelDataLayoutBrushPaletteUtility.CreateBrushColorField();
        brushTextColorField = ExcelDataLayoutBrushPaletteUtility.CreateBrushTextColorField();
        section.Add(savedBrushField);
        section.Add(brushColorField);
        section.Add(brushTextColorField);
        Button saveBrushButton = new Button(SaveCurrentBrushConfiguration);
        saveBrushButton.text = "Save Brush";
        saveBrushButton.tooltip = "Save current filters, background color and text color into the linked brush palette preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(saveBrushButton, 112f);
        section.Add(saveBrushButton);
        return section;
    }

    /// <summary>
    /// Builds smart field filters and the virtualized catalog used by Data mode.
    /// </summary>
    /// <returns>Configured field catalog section.</returns>
    private VisualElement BuildFieldCatalogSection()
    {
        VisualElement section = new VisualElement();
        section.style.flexGrow = 1f;
        section.style.overflow = Overflow.Hidden;
        Foldout filtersFoldout = ManagementToolFoldoutStateUtility.CreateFoldout("Catalog Filters",
                                                                                 CatalogFiltersFoldoutKey,
                                                                                 true);
        filtersFoldout.tooltip = "Narrow fields by owner, transferable value family, list position, ScriptableObject type and concrete source asset.";
        filtersFoldout.style.flexShrink = 0f;
        searchField = new ToolbarSearchField();
        searchField.tooltip = "Search field, asset, type or aliases such as ref, list, wave, bool, enum, number and scaling.";
        searchField.RegisterValueChangedCallback(evt => ApplyFilters());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        filtersFoldout.Add(searchField);
        domainField = CreateEnumFilter("Domain", ExcelDataTransferDomain.All,
                                       "Limit fields by management owner. Example: Player or Waves.");
        dataKindField = CreateDataKindFilter();
        listModeField = CreateEnumFilter("List Entries", ExcelDataListElementFilterMode.OutsideListsOnly,
                                         "Choose all fields, fields outside lists, list values by nesting depth, or list sizes only. Example: Top Level List Values shows fields belonging to `_1`, `_2` and sibling elements.");
        sourceTypeSearchField = CreateSourceSearchFilter("Filter ScriptableObject types by partial name. Example: PlayerControllerPreset or EnemyWavePreset.");
        sourceAssetSearchField = CreateSourceSearchFilter("Filter concrete assets by partial name or path. Example: ConeVision_ForwardAndBackward.");
        filtersFoldout.Add(domainField);
        filtersFoldout.Add(dataKindField);
        filtersFoldout.Add(listModeField);
        AddSearchFilter(filtersFoldout, "Source Type", sourceTypeSearchField);
        sourceAssetRoot = new VisualElement();
        AddSearchFilter(sourceAssetRoot, "Source Asset", sourceAssetSearchField);
        filtersFoldout.Add(sourceAssetRoot);
        section.Add(filtersFoldout);

        Foldout resultsFoldout = ManagementToolFoldoutStateUtility.CreateFoldout("Field Results",
                                                                                 FieldResultsFoldoutKey,
                                                                                 true);
        resultsFoldout.tooltip = "Virtualized list of fields that pass every active catalog filter.";
        resultsFoldout.style.flexGrow = 1f;
        resultsFoldout.style.flexShrink = 1f;
        resultsFoldout.style.minHeight = 140f;
        resultsFoldout.contentContainer.style.flexGrow = 1f;
        resultsFoldout.contentContainer.style.overflow = Overflow.Hidden;
        listView = BuildListView();
        listView.style.flexGrow = 1f;
        resultsFoldout.Add(listView);
        selectionLabel = new Label();
        selectionLabel.tooltip = "Current brush mode, selected serialized field and filtered-result count.";
        selectionLabel.style.marginTop = 4f;
        selectionLabel.style.whiteSpace = WhiteSpace.Normal;
        selectionLabel.style.flexShrink = 0f;
        resultsFoldout.Add(selectionLabel);
        section.Add(resultsFoldout);
        return section;
    }

    /// <summary>
    /// Builds the persistent operation-status foldout shown below catalog results.
    /// </summary>
    /// <returns>Configured status foldout.</returns>
    private Foldout BuildStatusSection()
    {
        statusFoldout = ManagementToolFoldoutStateUtility.CreateFoldout("Status",
                                                                        StatusFoldoutKey,
                                                                        false);
        statusFoldout.tooltip = "Shows the latest paint, structural edit, brush save or validation result.";
        statusFoldout.style.flexShrink = 0f;
        statusLabel = new Label("No pending layout operation.");
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.flexShrink = 0f;
        statusFoldout.Add(statusLabel);
        return statusFoldout;
    }

    /// <summary>
    /// Creates a compact Kind dropdown backed only by current, direction-compatible catalog families.
    /// </summary>
    /// <returns>Configured data-kind dropdown.</returns>
    private PopupField<ExcelDataBrushDataKind> CreateDataKindFilter()
    {
        List<ExcelDataBrushDataKind> choices = new List<ExcelDataBrushDataKind>
        {
            ExcelDataBrushDataKind.All
        };
        PopupField<ExcelDataBrushDataKind> field =
            new PopupField<ExcelDataBrushDataKind>("Kind",
                                                   choices,
                                                   0,
                                                   ExcelDataBrushDataKindFilterUtility.BuildLabel,
                                                   ExcelDataBrushDataKindFilterUtility.BuildLabel);
        field.tooltip = "Limit fields to a value family that is actually present and supported by the selected Direction. Example: Animation Curve and List Size appear only for Export cells.";
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        return field;
    }

    /// <summary>
    /// Creates one explicitly described enum filter.
    /// </summary>
    /// <param name="label">Dropdown label.</param>
    /// <param name="initialValue">Initial enum value.</param>
    /// <param name="tooltip">Filter behavior and example.</param>
    /// <returns>Configured enum filter.</returns>
    private EnumField CreateEnumFilter(string label, Enum initialValue, string tooltip)
    {
        EnumField field = new EnumField(label, initialValue);
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        return field;
    }

    /// <summary>
    /// Creates the source-type text filter.
    /// </summary>
    /// <returns>Configured source search field.</returns>
    private ToolbarSearchField CreateSourceSearchFilter(string tooltip)
    {
        ToolbarSearchField field = new ToolbarSearchField();
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        GameManagementPanelLayoutUtility.ConfigureSearchField(field);
        return field;
    }

    /// <summary>
    /// Adds one labelled searchable filter without creating an oversized dropdown.
    /// </summary>
    /// <param name="parent">Parent receiving label and search field.</param>
    /// <param name="labelText">Visible filter label.</param>
    /// <param name="field">Search field controlled by the label.</param>
    private static void AddSearchFilter(VisualElement parent,
                                        string labelText,
                                        ToolbarSearchField field)
    {
        Label label = new Label(labelText);
        label.tooltip = field.tooltip;
        label.style.flexShrink = 0f;
        parent.Add(label);
        parent.Add(field);
    }

    /// <summary>
    /// Builds the virtualized filtered field list.
    /// </summary>
    /// <returns>Configured field list.</returns>
    private ListView BuildListView()
    {
        ListView fieldListView = new ListView();
        fieldListView.itemsSource = filteredEntries;
        fieldListView.makeItem = () =>
        {
            Label label = new Label();
            label.style.whiteSpace = WhiteSpace.NoWrap;
            GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
            return label;
        };
        fieldListView.bindItem = BindListItem;
        fieldListView.selectionChanged += OnFieldSelectionChanged;
        fieldListView.fixedItemHeight = 20f;
        fieldListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        fieldListView.style.minHeight = 120f;
        GameManagementPanelLayoutUtility.ConfigureListView(fieldListView);
        return fieldListView;
    }
    #endregion

    #region Catalog And Brushes
    /// <summary>
    /// Applies active smart filters to the field picker list.
    /// </summary>
    private void ApplyFilters()
    {
        filteredEntries.Clear();
        RefreshSourceAssetVisibility();

        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = allEntries[entryIndex];

            if (ExcelDataFieldCatalogFilterUtility.MatchesFilters(entry,
                                                                  searchField.value,
                                                                  (ExcelDataTransferDomain)domainField.value,
                                                                  dataKindField.value,
                                                                  (ExcelDataListElementFilterMode)listModeField.value,
                                                                  sourceTypeSearchField.value,
                                                                  sourceAssetSearchField.value))
                filteredEntries.Add(entry);
        }

        listView.Rebuild();
        catalogSelectionChanged();
    }

    /// <summary>
    /// Shows the dependent source-asset filter only when the current source scope contains alternatives.
    /// </summary>
    private void RefreshSourceAssetVisibility()
    {
        if (sourceAssetRoot == null || sourceAssetSearchField == null)
            return;

        HashSet<string> sourceAssets = new HashSet<string>(StringComparer.Ordinal);

        // Count only distinct assets that pass filters preceding Source Asset in the filter hierarchy.
        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = allEntries[entryIndex];

            if (!ExcelDataFieldCatalogFilterUtility.MatchesFilters(entry,
                                                                   searchField.value,
                                                                   (ExcelDataTransferDomain)domainField.value,
                                                                   dataKindField.value,
                                                                   (ExcelDataListElementFilterMode)listModeField.value,
                                                                   sourceTypeSearchField.value,
                                                                   string.Empty))
                continue;

            sourceAssets.Add(entry.AssetPath);

            if (sourceAssets.Count > 1)
                break;
        }

        bool useful = sourceAssets.Count > 1;
        sourceAssetRoot.style.display = useful ? DisplayStyle.Flex : DisplayStyle.None;

        if (!useful && !string.IsNullOrWhiteSpace(sourceAssetSearchField.value))
            sourceAssetSearchField.SetValueWithoutNotify(string.Empty);
    }

    /// <summary>
    /// Binds one field catalog row with concrete path and source diagnostics.
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
        label.text = entry.Domain + " | " + entry.DataKind + " | " + entry.AssetName + " | " + entry.ReadablePath;
        label.tooltip = entry.DisplayName +
                        "\nType: " + entry.AssetTypeName +
                        "\nAsset: " + entry.AssetPath +
                        "\nSerialized Path: " + entry.SerializedPath +
                        "\nStable List Keys: " + ExcelDataListIdentityUtility.BuildStableKeySearchText(entry.StableListKeys);
    }

    /// <summary>
    /// Stores the selected field used by Data paint mode.
    /// </summary>
    /// <param name="selection">Selected list payload.</param>
    private void OnFieldSelectionChanged(IEnumerable<object> selection)
    {
        selectedEntry = null;

        foreach (object selectedObject in selection)
        {
            selectedEntry = selectedObject as ExcelDataFieldCatalogEntry;

            if (selectedEntry != null)
                break;
        }

        catalogSelectionChanged();
    }

    /// <summary>
    /// Applies one saved brush to catalog filters and current color.
    /// </summary>
    /// <param name="optionLabel">Visible saved-brush option.</param>
    private void ApplySavedBrushConfiguration(string optionLabel)
    {
        ExcelDataTransferDirection direction;

        if (ExcelDataLayoutBrushPaletteUtility.ApplySavedBrushConfiguration(brushPaletteResolver(),
                                                                            optionLabel,
                                                                            domainField,
                                                                            dataKindField,
                                                                            listModeField,
                                                                            sourceTypeSearchField,
                                                                            sourceAssetSearchField,
                                                                            searchField,
                                                                            brushColorField,
                                                                            brushTextColorField,
                                                                            out direction))
        {
            brushInspector.SetPaintDirection(direction);
            RefreshDataKindChoices();
        }
    }

    /// <summary>
    /// Saves the current filter and color state into the linked brush palette.
    /// </summary>
    private void SaveCurrentBrushConfiguration()
    {
        string selectedOption;
        string statusMessage;
        bool saved = ExcelDataLayoutBrushPaletteUtility.SaveCurrentBrushConfiguration(brushPaletteResolver(),
                                                                                      domainField,
                                                                                      dataKindField,
                                                                                      listModeField,
                                                                                      sourceTypeSearchField,
                                                                                      sourceAssetSearchField,
                                                                                      brushColorField,
                                                                                      brushTextColorField,
                                                                                      searchField,
                                                                                      brushInspector.Direction,
                                                                                      out selectedOption,
                                                                                      out statusMessage);
        SetStatus(statusMessage);

        if (!saved)
            return;

        RefreshSavedBrushOptions();
        savedBrushField.SetValueWithoutNotify(selectedOption);
    }
    #endregion

    #endregion
}
