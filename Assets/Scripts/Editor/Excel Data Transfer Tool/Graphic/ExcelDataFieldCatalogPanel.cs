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
    private EnumField dataKindField;
    private EnumField listModeField;
    private ToolbarSearchField sourceTypeSearchField;
    private ToolbarSearchField sourceAssetSearchField;
    private VisualElement sourceAssetRoot;
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
        dataKindField = CreateEnumFilter("Kind", ExcelDataBrushDataKind.All);
        listModeField = CreateEnumFilter("List Entries", ExcelDataListElementFilterMode.OutsideListsOnly);
        sourceTypeSearchField = CreateSearchFilter("Filter ScriptableObject types by partial name. Example: PlayerControllerPreset.");
        sourceAssetSearchField = CreateSearchFilter("Filter concrete source assets by partial name or path. Example: ConeVision_ForwardAndBackward.");

        pane.Add(domainField);
        pane.Add(dataKindField);
        pane.Add(listModeField);
        AddSearchFilter(pane, "Source Type", sourceTypeSearchField);
        sourceAssetRoot = new VisualElement();
        AddSearchFilter(sourceAssetRoot, "Source Asset", sourceAssetSearchField);
        pane.Add(sourceAssetRoot);

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
    /// Creates one searchable source filter that cannot expand into an oversized dropdown.
    /// </summary>
    /// <param name="tooltip">Explicit filter behavior and example.</param>
    /// <returns>Configured source search field.</returns>
    private ToolbarSearchField CreateSearchFilter(string tooltip)
    {
        ToolbarSearchField field = new ToolbarSearchField();
        field.tooltip = tooltip;
        field.RegisterValueChangedCallback(evt => ApplyFilters());
        GameManagementPanelLayoutUtility.ConfigureSearchField(field);
        return field;
    }

    /// <summary>
    /// Adds one labelled source search filter to the filter pane.
    /// </summary>
    /// <param name="parent">Parent receiving label and field.</param>
    /// <param name="labelText">Visible filter label.</param>
    /// <param name="field">Search field controlled by the label.</param>
    private static void AddSearchFilter(VisualElement parent,
                                        string labelText,
                                        ToolbarSearchField field)
    {
        Label label = new Label(labelText);
        label.tooltip = field.tooltip;
        parent.Add(label);
        parent.Add(field);
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
        label.text = entry.Domain + " | " + entry.AssetTypeName + " | " + entry.AssetName + " | " + entry.ReadablePath;
        label.tooltip = entry.DisplayName + "\n" + entry.AssetPath + "\n" + entry.SerializedPath;
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
        ApplyFilters();
    }

    /// <summary>
    /// Applies search and dropdown filters to the cached catalog entries.
    /// </summary>
    private void ApplyFilters()
    {
        filteredEntries.Clear();
        RefreshSourceAssetVisibility();
        string searchText = searchField == null ? string.Empty : searchField.value;
        ExcelDataTransferDomain domainFilter = domainField == null ? ExcelDataTransferDomain.All : (ExcelDataTransferDomain)domainField.value;
        ExcelDataBrushDataKind dataKindFilter = dataKindField == null ? ExcelDataBrushDataKind.All : (ExcelDataBrushDataKind)dataKindField.value;
        ExcelDataListElementFilterMode listFilter = listModeField == null ? ExcelDataListElementFilterMode.AllBrushableFields : (ExcelDataListElementFilterMode)listModeField.value;
        string sourceTypeFilter = sourceTypeSearchField == null ? string.Empty : sourceTypeSearchField.value;
        string sourceAssetFilter = sourceAssetSearchField == null ? string.Empty : sourceAssetSearchField.value;

        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = allEntries[entryIndex];

            if (!ExcelDataFieldCatalogFilterUtility.MatchesFilters(entry,
                                                                   searchText,
                                                                   domainFilter,
                                                                   dataKindFilter,
                                                                   listFilter,
                                                                   sourceTypeFilter,
                                                                   sourceAssetFilter))
                continue;

            filteredEntries.Add(entry);
        }

        if (listView != null)
            listView.Rebuild();

        UpdateCountLabel();
    }

    /// <summary>
    /// Shows Source Asset only when preceding filters leave multiple concrete assets.
    /// </summary>
    private void RefreshSourceAssetVisibility()
    {
        if (sourceAssetRoot == null || sourceAssetSearchField == null)
            return;

        HashSet<string> sourceAssets = new HashSet<string>(StringComparer.Ordinal);

        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = allEntries[entryIndex];

            if (!ExcelDataFieldCatalogFilterUtility.MatchesFilters(entry,
                                                                   searchField.value,
                                                                   (ExcelDataTransferDomain)domainField.value,
                                                                   (ExcelDataBrushDataKind)dataKindField.value,
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
                "Readable Path: " + entry.ReadablePath + "\n" +
                "Path: " + entry.SerializedPath + "\n" +
                "Template: " + entry.PathTemplate + "\n" +
                "Kind: " + entry.DataKind + "\n" +
                "List Depth: " + entry.ListDepth + "\n" +
                "Stable List Keys: " + ExcelDataListIdentityUtility.BuildStableKeySearchText(entry.StableListKeys);
            return;
        }

        detailsLabel.text = "Select a catalog field.";
    }
    #endregion

    #endregion
}
