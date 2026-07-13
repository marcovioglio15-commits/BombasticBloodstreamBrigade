using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shows searchable field catalog entries that can later be brushed into workbook layout cells.
/// </summary>
public sealed class ExcelDataFieldCatalogPanel
{
    #region Fields
    private readonly VisualElement root;
    private readonly List<ExcelDataFieldCatalogEntry> allEntries = new List<ExcelDataFieldCatalogEntry>();
    private readonly List<ExcelDataFieldCatalogEntry> filteredEntries = new List<ExcelDataFieldCatalogEntry>();
    private Label countLabel;
    private Label detailsLabel;
    private ListView listView;
    private ToolbarSearchField searchField;
    private EnumField domainField;
    private EnumField categoryField;
    private EnumField dataKindField;
    private EnumField listModeField;
    private PopupField<string> sourceField;
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
    /// Builds the field catalog panel and performs the initial asset scan.
    /// </summary>
    public ExcelDataFieldCatalogPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        TwoPaneSplitView splitView = GameManagementPanelLayoutUtility.CreateHorizontalSplitView(360f);
        root.Add(splitView);

        VisualElement filterPane = BuildFilterPane();
        VisualElement detailsPane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureDetailsPane(detailsPane);

        listView = BuildListView();
        detailsLabel = new Label("Select a catalog field.");
        detailsLabel.style.whiteSpace = WhiteSpace.Normal;
        detailsPane.Add(listView);
        detailsPane.Add(detailsLabel);

        splitView.Add(filterPane);
        splitView.Add(detailsPane);

        RefreshCatalog();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Rebuilds the catalog after draft session changes may have altered source assets.
    /// </summary>
    public void RefreshFromSessionChange()
    {
        RefreshCatalog();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Builds the filter pane with smart search and dropdown filters.
    /// </summary>
    /// <returns>Configured filter pane.</returns>
    private VisualElement BuildFilterPane()
    {
        VisualElement pane = new VisualElement();
        GameManagementPanelLayoutUtility.ConfigureBrowserPane(pane);

        Label titleLabel = new Label("Field Catalog");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6f;
        pane.Add(titleLabel);

        searchField = new ToolbarSearchField();
        searchField.tooltip = "Search by path, asset, type or aliases such as ref, list, wave, bool, enum, number, scaling.";
        searchField.RegisterValueChangedCallback(evt => ApplyFilters());
        GameManagementPanelLayoutUtility.ConfigureSearchField(searchField);
        pane.Add(searchField);

        domainField = CreateEnumFilter("Domain", ExcelDataTransferDomain.All);
        categoryField = CreateEnumFilter("Category", ExcelDataFieldCategory.All);
        dataKindField = CreateEnumFilter("Kind", ExcelDataBrushDataKind.All);
        listModeField = CreateEnumFilter("Lists", ExcelDataListElementFilterMode.HideConcreteListElements);
        sourceField = CreateSourceFilter();

        pane.Add(domainField);
        pane.Add(categoryField);
        pane.Add(dataKindField);
        pane.Add(listModeField);
        pane.Add(sourceField);

        Button refreshButton = new Button(RefreshCatalog);
        refreshButton.text = "Refresh";
        refreshButton.tooltip = "Rebuild the field catalog from current Player, Enemy, Game and Wave assets.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(refreshButton, 80f);
        pane.Add(refreshButton);

        countLabel = new Label();
        countLabel.style.marginTop = 8f;
        countLabel.style.whiteSpace = WhiteSpace.Normal;
        pane.Add(countLabel);

        return pane;
    }

    /// <summary>
    /// Creates one enum dropdown that refreshes filters when changed.
    /// </summary>
    /// <param name="label">Dropdown label.</param>
    /// <param name="initialValue">Initial enum value.</param>
    /// <returns>Configured enum field.</returns>
    private EnumField CreateEnumFilter(string label, Enum initialValue)
    {
        EnumField field = new EnumField(label, initialValue);
        field.tooltip = "Filter the field catalog before brushing data into workbook cells.";
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        return field;
    }

    /// <summary>
    /// Creates the dynamic source-type dropdown used to reduce large catalog searches.
    /// </summary>
    /// <returns>Configured source filter dropdown.</returns>
    private PopupField<string> CreateSourceFilter()
    {
        List<string> options = new List<string>();
        options.Add(ExcelDataFieldCatalogSourceFilterUtility.AllSourcesOption);
        PopupField<string> field = new PopupField<string>("Source", options, 0);
        field.tooltip = "Limit the catalog to one ScriptableObject source type before searching individual fields.";
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        return field;
    }

    /// <summary>
    /// Builds the catalog list view with compact rows.
    /// </summary>
    /// <returns>Configured list view.</returns>
    private ListView BuildListView()
    {
        ListView createdListView = new ListView();
        createdListView.itemsSource = filteredEntries;
        createdListView.makeItem = MakeListItem;
        createdListView.bindItem = BindListItem;
        createdListView.selectionChanged += OnSelectionChanged;
        createdListView.fixedItemHeight = 20f;
        createdListView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        GameManagementPanelLayoutUtility.ConfigureListView(createdListView);
        return createdListView;
    }

    /// <summary>
    /// Creates one reusable catalog row visual.
    /// </summary>
    /// <returns>Row root visual element.</returns>
    private VisualElement MakeListItem()
    {
        Label label = new Label();
        label.style.whiteSpace = WhiteSpace.NoWrap;
        GameManagementPanelLayoutUtility.ConfigureListRowLabel(label);
        return label;
    }

    /// <summary>
    /// Binds one visible catalog row.
    /// </summary>
    /// <param name="element">Row visual element.</param>
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
        label.text = entry.Domain + " | " + entry.AssetTypeName + " | " + entry.PathTemplate;
        label.tooltip = entry.DisplayName + "\n" + entry.AssetPath;
    }
    #endregion

    #region Catalog
    /// <summary>
    /// Rebuilds the full catalog from project assets and reapplies active filters.
    /// </summary>
    private void RefreshCatalog()
    {
        allEntries.Clear();
        filteredEntries.Clear();
        allEntries.AddRange(ExcelDataFieldCatalogBuilder.BuildCatalog());
        RefreshSourceOptions();
        ApplyFilters();
    }

    /// <summary>
    /// Applies search and dropdown filters to the cached catalog entries.
    /// </summary>
    private void ApplyFilters()
    {
        filteredEntries.Clear();
        string searchText = searchField == null ? string.Empty : searchField.value;
        ExcelDataTransferDomain domainFilter = domainField == null ? ExcelDataTransferDomain.All : (ExcelDataTransferDomain)domainField.value;
        ExcelDataFieldCategory categoryFilter = categoryField == null ? ExcelDataFieldCategory.All : (ExcelDataFieldCategory)categoryField.value;
        ExcelDataBrushDataKind dataKindFilter = dataKindField == null ? ExcelDataBrushDataKind.All : (ExcelDataBrushDataKind)dataKindField.value;
        ExcelDataListElementFilterMode listFilter = listModeField == null ? ExcelDataListElementFilterMode.All : (ExcelDataListElementFilterMode)listModeField.value;
        string sourceFilter = sourceField == null ? ExcelDataFieldCatalogSourceFilterUtility.AllSourcesOption : sourceField.value;

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

        UpdateCountLabel();
    }

    /// <summary>
    /// Rebuilds the source dropdown from catalog asset types while preserving the previous selection when possible.
    /// </summary>
    private void RefreshSourceOptions()
    {
        if (sourceField == null)
            return;

        string previousValue = string.IsNullOrWhiteSpace(sourceField.value) ?
                               ExcelDataFieldCatalogSourceFilterUtility.AllSourcesOption :
                               sourceField.value;
        List<string> options = ExcelDataFieldCatalogSourceFilterUtility.BuildSourceOptions(allEntries);

        sourceField.choices = options;

        if (options.Contains(previousValue))
            sourceField.SetValueWithoutNotify(previousValue);
        else
            sourceField.SetValueWithoutNotify(ExcelDataFieldCatalogSourceFilterUtility.AllSourcesOption);
    }

    /// <summary>
    /// Updates the compact catalog count label.
    /// </summary>
    private void UpdateCountLabel()
    {
        if (countLabel == null)
            return;

        countLabel.text = filteredEntries.Count + " / " + allEntries.Count + " fields";
    }
    #endregion

    #region Selection
    /// <summary>
    /// Shows detailed metadata for the selected catalog row.
    /// </summary>
    /// <param name="selection">Selected row objects from the list view.</param>
    private void OnSelectionChanged(IEnumerable<object> selection)
    {
        if (detailsLabel == null)
            return;

        foreach (object selectedObject in selection)
        {
            ExcelDataFieldCatalogEntry entry = selectedObject as ExcelDataFieldCatalogEntry;

            if (entry == null)
                continue;

            detailsLabel.text =
                "Field ID: " + entry.FieldId + "\n" +
                "Asset: " + entry.AssetName + "\n" +
                "Path: " + entry.SerializedPath + "\n" +
                "Template: " + entry.PathTemplate + "\n" +
                "Kind: " + entry.DataKind + "\n" +
                "Category: " + entry.Category + "\n" +
                "List Depth: " + entry.ListDepth;
            return;
        }

        detailsLabel.text = "Select a catalog field.";
    }
    #endregion

    #endregion
}
