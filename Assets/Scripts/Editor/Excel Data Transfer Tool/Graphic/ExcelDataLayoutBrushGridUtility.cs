using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds and colors the visible Excel layout brush grid for the layout panel.
/// </summary>
internal static class ExcelDataLayoutBrushGridUtility
{
    #region Constants
    private const int MaxVisibleRows = 48;
    private const int MaxVisibleColumns = 24;
    private const float MinimumCellWidth = 48f;
    private const float MinimumCellHeight = 22f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the visible brush grid from layout mappings.
    /// </summary>
    /// <param name="gridRoot">Visual root receiving the grid.</param>
    /// <param name="layoutPreset">Layout preset storing dimensions and cell mappings.</param>
    /// <param name="brushPalettePreset">Brush palette used to color painted cells.</param>
    /// <param name="allEntries">Cached catalog entries used to resolve mapped field labels.</param>
    /// <param name="cellClicked">Callback invoked when a grid cell is clicked.</param>
    public static void RebuildGrid(VisualElement gridRoot,
                                   ExcelDataWorkbookLayoutPreset layoutPreset,
                                   ExcelDataBrushPalettePreset brushPalettePreset,
                                   List<ExcelDataFieldCatalogEntry> allEntries,
                                   Action<int, int> cellClicked)
    {
        if (gridRoot == null)
            return;

        gridRoot.Clear();

        if (layoutPreset == null)
        {
            gridRoot.Add(new Label("Missing layout preset."));
            return;
        }

        int visibleRows = Mathf.Max(1, Mathf.Min(layoutPreset.DefaultGridRows, MaxVisibleRows));
        int visibleColumns = Mathf.Max(1, Mathf.Min(layoutPreset.DefaultGridColumns, MaxVisibleColumns));
        float cellWidth = Mathf.Max(MinimumCellWidth, layoutPreset.DefaultCellWidth);
        float cellHeight = Mathf.Max(MinimumCellHeight, layoutPreset.DefaultCellHeight);

        AddCapWarningIfNeeded(gridRoot, layoutPreset);

        ScrollView scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        scrollView.style.flexGrow = 1f;
        gridRoot.Add(scrollView);

        for (int rowIndex = 1; rowIndex <= visibleRows; rowIndex++)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0f;
            scrollView.Add(row);

            for (int columnIndex = 1; columnIndex <= visibleColumns; columnIndex++)
                row.Add(CreateCellButton(layoutPreset, brushPalettePreset, allEntries, rowIndex, columnIndex, cellWidth, cellHeight, cellClicked));
        }
    }

    /// <summary>
    /// Finds an existing cell mapping by sheet and coordinate.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to search.</param>
    /// <param name="sheetName">Worksheet name.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    /// <returns>Matching mapping, or null when the cell is empty.</returns>
    public static ExcelDataCellBrushMapping FindMapping(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                        string sheetName,
                                                        int rowIndex,
                                                        int columnIndex)
    {
        if (layoutPreset == null)
            return null;

        List<ExcelDataCellBrushMapping> mappings = layoutPreset.CellMappings;

        for (int mappingIndex = 0; mappingIndex < mappings.Count; mappingIndex++)
        {
            ExcelDataCellBrushMapping mapping = mappings[mappingIndex];

            if (mapping == null)
                continue;

            if (mapping.MatchesCell(sheetName, rowIndex, columnIndex))
                return mapping;
        }

        return null;
    }
    #endregion

    #region Grid Rendering
    /// <summary>
    /// Creates one clickable grid cell button.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing current mappings.</param>
    /// <param name="brushPalettePreset">Brush palette used to color painted cells.</param>
    /// <param name="allEntries">Cached catalog entries used to resolve mapped field labels.</param>
    /// <param name="rowIndex">One-based grid row index.</param>
    /// <param name="columnIndex">One-based grid column index.</param>
    /// <param name="cellWidth">Resolved visible cell width.</param>
    /// <param name="cellHeight">Resolved visible cell height.</param>
    /// <param name="cellClicked">Callback invoked when the button is clicked.</param>
    /// <returns>Configured cell button.</returns>
    private static Button CreateCellButton(ExcelDataWorkbookLayoutPreset layoutPreset,
                                           ExcelDataBrushPalettePreset brushPalettePreset,
                                           List<ExcelDataFieldCatalogEntry> allEntries,
                                           int rowIndex,
                                           int columnIndex,
                                           float cellWidth,
                                           float cellHeight,
                                           Action<int, int> cellClicked)
    {
        ExcelDataCellBrushMapping mapping = FindMapping(layoutPreset, layoutPreset.ObjectsSheetName, rowIndex, columnIndex);
        Button button = new Button(() => cellClicked(rowIndex, columnIndex));
        button.text = BuildCellText(mapping, allEntries);
        button.tooltip = BuildCellTooltip(mapping, allEntries, rowIndex, columnIndex);
        button.style.width = cellWidth;
        button.style.height = cellHeight;
        button.style.flexShrink = 0f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.overflow = Overflow.Hidden;

        if (mapping != null)
            button.style.backgroundColor = ResolveMappingColor(mapping, brushPalettePreset, allEntries);

        return button;
    }

    /// <summary>
    /// Adds a non-destructive preview cap warning when authored dimensions are very large.
    /// </summary>
    /// <param name="gridRoot">Grid visual root receiving the warning.</param>
    /// <param name="layoutPreset">Layout preset containing authored dimensions.</param>
    private static void AddCapWarningIfNeeded(VisualElement gridRoot, ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPreset.DefaultGridRows <= MaxVisibleRows && layoutPreset.DefaultGridColumns <= MaxVisibleColumns)
            return;

        Label capLabel = new Label("Grid preview capped at " + MaxVisibleRows + " x " + MaxVisibleColumns + ". Authored dimensions are preserved.");
        capLabel.style.whiteSpace = WhiteSpace.Normal;
        gridRoot.Add(capLabel);
    }
    #endregion

    #region Label Helpers
    /// <summary>
    /// Builds a compact visible label for a painted cell.
    /// </summary>
    /// <param name="mapping">Cell mapping to render.</param>
    /// <param name="allEntries">Cached catalog entries used to resolve labels.</param>
    /// <returns>Short cell text.</returns>
    private static string BuildCellText(ExcelDataCellBrushMapping mapping,
                                        List<ExcelDataFieldCatalogEntry> allEntries)
    {
        if (mapping == null)
            return string.Empty;

        ExcelDataFieldCatalogEntry entry = FindEntryById(allEntries, mapping.FieldId);

        if (entry == null)
            return "?";

        string path = entry.PathTemplate;
        int splitIndex = path.LastIndexOf('.');
        return splitIndex >= 0 && splitIndex < path.Length - 1 ? path.Substring(splitIndex + 1) : path;
    }

    /// <summary>
    /// Builds a detailed tooltip for a painted or empty grid cell.
    /// </summary>
    /// <param name="mapping">Cell mapping to describe.</param>
    /// <param name="allEntries">Cached catalog entries used to resolve labels.</param>
    /// <param name="rowIndex">One-based row coordinate.</param>
    /// <param name="columnIndex">One-based column coordinate.</param>
    /// <returns>Tooltip text.</returns>
    private static string BuildCellTooltip(ExcelDataCellBrushMapping mapping,
                                           List<ExcelDataFieldCatalogEntry> allEntries,
                                           int rowIndex,
                                           int columnIndex)
    {
        if (mapping == null)
            return "Cell " + rowIndex + "," + columnIndex + ". Click to paint the selected field.";

        ExcelDataFieldCatalogEntry entry = FindEntryById(allEntries, mapping.FieldId);
        string fieldText = entry == null ? mapping.FieldId : entry.DisplayName;
        return "Cell " + rowIndex + "," + columnIndex + "\n" + fieldText;
    }

    /// <summary>
    /// Resolves a color for a painted cell from its mapped field domain.
    /// </summary>
    /// <param name="mapping">Cell mapping to color.</param>
    /// <param name="brushPalettePreset">Brush palette used to color painted cells.</param>
    /// <param name="allEntries">Cached catalog entries used to resolve field domains.</param>
    /// <returns>Cell background color.</returns>
    private static Color ResolveMappingColor(ExcelDataCellBrushMapping mapping,
                                             ExcelDataBrushPalettePreset brushPalettePreset,
                                             List<ExcelDataFieldCatalogEntry> allEntries)
    {
        ExcelDataFieldCatalogEntry entry = FindEntryById(allEntries, mapping.FieldId);

        if (entry == null)
            return new Color(0.6f, 0.35f, 0.35f, 1f);

        Color paletteColor = ResolvePaletteColor(brushPalettePreset, entry);

        if (paletteColor.a > 0f)
            return paletteColor;

        switch (entry.Domain)
        {
            case ExcelDataTransferDomain.Player:
                return new Color(0.36f, 0.64f, 0.95f, 1f);
            case ExcelDataTransferDomain.Enemy:
                return new Color(0.95f, 0.45f, 0.45f, 1f);
            case ExcelDataTransferDomain.Waves:
                return new Color(0.95f, 0.72f, 0.36f, 1f);
            case ExcelDataTransferDomain.Game:
                return new Color(0.48f, 0.82f, 0.5f, 1f);
            default:
                return new Color(0.72f, 0.72f, 0.72f, 1f);
        }
    }

    /// <summary>
    /// Resolves the best matching authored brush color for one catalog entry.
    /// </summary>
    /// <param name="brushPalettePreset">Brush palette to inspect.</param>
    /// <param name="entry">Catalog entry represented by the painted cell.</param>
    /// <returns>Authored brush color, or transparent when no brush matches.</returns>
    private static Color ResolvePaletteColor(ExcelDataBrushPalettePreset brushPalettePreset,
                                             ExcelDataFieldCatalogEntry entry)
    {
        if (brushPalettePreset == null || entry == null)
            return Color.clear;

        List<ExcelDataBrushDefinition> brushes = brushPalettePreset.Brushes;
        ExcelDataBrushDefinition fallbackBrush = null;

        for (int brushIndex = 0; brushIndex < brushes.Count; brushIndex++)
        {
            ExcelDataBrushDefinition brush = brushes[brushIndex];

            if (brush == null)
                continue;

            if (!BrushMatchesEntry(brush, entry))
                continue;

            if (brush.DataKind == entry.DataKind && brush.Domain == entry.Domain)
                return brush.Color;

            if (fallbackBrush == null)
                fallbackBrush = brush;
        }

        return fallbackBrush == null ? Color.clear : fallbackBrush.Color;
    }

    /// <summary>
    /// Checks whether a saved brush can color one catalog entry.
    /// </summary>
    /// <param name="brush">Brush configuration to evaluate.</param>
    /// <param name="entry">Catalog entry represented by a cell mapping.</param>
    /// <returns>True when the brush filters include the entry.</returns>
    private static bool BrushMatchesEntry(ExcelDataBrushDefinition brush,
                                          ExcelDataFieldCatalogEntry entry)
    {
        if (brush.Domain != ExcelDataTransferDomain.All && brush.Domain != entry.Domain)
            return false;

        if (brush.Category != ExcelDataFieldCategory.All && brush.Category != entry.Category)
            return false;

        if (brush.DataKind != ExcelDataBrushDataKind.All && brush.DataKind != entry.DataKind)
            return false;

        return ExcelDataFieldCatalogFilterUtility.MatchesSourceFilter(entry, brush.SourceFilter);
    }

    /// <summary>
    /// Finds a catalog entry by stable field id in the cached catalog.
    /// </summary>
    /// <param name="allEntries">Cached catalog entries to search.</param>
    /// <param name="fieldId">Field id to search.</param>
    /// <returns>Matching entry or null.</returns>
    private static ExcelDataFieldCatalogEntry FindEntryById(List<ExcelDataFieldCatalogEntry> allEntries, string fieldId)
    {
        if (allEntries == null || string.IsNullOrWhiteSpace(fieldId))
            return null;

        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = allEntries[entryIndex];

            if (entry.FieldId == fieldId)
                return entry;
        }

        return null;
    }
    #endregion

    #endregion
}
