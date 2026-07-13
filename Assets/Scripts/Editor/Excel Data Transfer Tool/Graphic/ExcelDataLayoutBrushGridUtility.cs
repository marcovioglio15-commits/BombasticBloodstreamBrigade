using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the coordinate-labelled workbook grid and structural row or column edit separators.
/// </summary>
internal static class ExcelDataLayoutBrushGridUtility
{
    #region Constants
    private const int MaxVisibleRows = 48;
    private const int MaxVisibleColumns = 24;
    private const float MinimumCellWidth = 48f;
    private const float MinimumCellHeight = 22f;
    private const float RowGutterWidth = 42f;
    private const float SeparatorSize = 6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the active worksheet with Excel headers, exact cells and right-click structural separators.
    /// </summary>
    /// <param name="gridRoot">Visual root receiving the grid.</param>
    /// <param name="sheet">Active grid-authoritative worksheet.</param>
    /// <param name="brushPalettePreset">Brush palette used to resolve exact saved brush colors.</param>
    /// <param name="allEntries">Cached catalog entries used to resolve field labels.</param>
    /// <param name="selectedRowIndex">One-based selected row index.</param>
    /// <param name="selectedColumnIndex">One-based selected column index.</param>
    /// <param name="cellClicked">Callback invoked when a grid cell is left-clicked.</param>
    /// <param name="insertRow">Callback invoked with the one-based new empty row index.</param>
    /// <param name="insertColumn">Callback invoked with the one-based new empty column index.</param>
    /// <param name="removeRow">Callback invoked with the one-based row index to remove.</param>
    /// <param name="removeColumn">Callback invoked with the one-based column index to remove.</param>
    public static void RebuildGrid(VisualElement gridRoot,
                                   ExcelDataWorkbookSheetDefinition sheet,
                                   ExcelDataBrushPalettePreset brushPalettePreset,
                                   IReadOnlyList<ExcelDataFieldCatalogEntry> allEntries,
                                   int selectedRowIndex,
                                   int selectedColumnIndex,
                                   Action<int, int> cellClicked,
                                   Action<int> insertRow,
                                   Action<int> insertColumn,
                                   Action<int> removeRow,
                                   Action<int> removeColumn)
    {
        if (gridRoot == null)
            return;

        gridRoot.Clear();

        if (sheet == null)
        {
            gridRoot.Add(new Label("Missing grid-authoritative worksheet."));
            return;
        }

        int visibleRows = Mathf.Max(1, Mathf.Min(sheet.PreviewRowCount, MaxVisibleRows));
        int visibleColumns = Mathf.Max(1, Mathf.Min(sheet.PreviewColumnCount, MaxVisibleColumns));
        float cellWidth = Mathf.Max(MinimumCellWidth, sheet.PreviewCellWidth);
        float cellHeight = Mathf.Max(MinimumCellHeight, sheet.PreviewCellHeight);
        float gridWidth = RowGutterWidth +
                          visibleColumns * cellWidth +
                          Mathf.Max(0, visibleColumns - 1) * SeparatorSize;

        AddCapWarningIfNeeded(gridRoot, sheet);
        ScrollView scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        scrollView.style.flexGrow = 1f;
        gridRoot.Add(scrollView);
        scrollView.Add(CreateColumnHeaderRow(visibleColumns,
                                             cellWidth,
                                             cellHeight,
                                             insertColumn,
                                             removeColumn));

        // Build each row with a real gutter and dedicated vertical insertion hit areas.
        for (int rowIndex = 1; rowIndex <= visibleRows; rowIndex++)
        {
            scrollView.Add(CreateDataRow(sheet,
                                         brushPalettePreset,
                                         allEntries,
                                         rowIndex,
                                         visibleColumns,
                                         cellWidth,
                                         cellHeight,
                                         selectedRowIndex,
                                         selectedColumnIndex,
                                         cellClicked,
                                         insertColumn,
                                         removeColumn));

            if (rowIndex < visibleRows)
                scrollView.Add(CreateRowSeparator(rowIndex + 1, gridWidth, insertRow, removeRow));
        }
    }
    #endregion

    #region Grid Rows
    /// <summary>
    /// Creates column headers A, B, C and vertical insertion separators.
    /// </summary>
    /// <param name="visibleColumns">Visible column count.</param>
    /// <param name="cellWidth">Stable cell width.</param>
    /// <param name="cellHeight">Stable cell height.</param>
    /// <param name="insertColumn">Structural insertion callback.</param>
    /// <param name="removeColumn">Structural removal callback.</param>
    /// <returns>Configured header row.</returns>
    private static VisualElement CreateColumnHeaderRow(int visibleColumns,
                                                       float cellWidth,
                                                       float cellHeight,
                                                       Action<int> insertColumn,
                                                       Action<int> removeColumn)
    {
        VisualElement row = CreateFixedRow(cellHeight);
        row.Add(CreateHeaderLabel(string.Empty, RowGutterWidth, cellHeight));

        for (int columnIndex = 1; columnIndex <= visibleColumns; columnIndex++)
        {
            row.Add(CreateHeaderLabel(ExcelDataWorkbookCoordinateUtility.ColumnIndexToName(columnIndex),
                                      cellWidth,
                                      cellHeight));

            if (columnIndex < visibleColumns)
                row.Add(CreateColumnSeparator(columnIndex + 1,
                                              cellHeight,
                                              insertColumn,
                                              removeColumn));
        }

        return row;
    }

    /// <summary>
    /// Creates one data row with row-number gutter, exact cells and vertical separators.
    /// </summary>
    /// <param name="sheet">Active worksheet.</param>
    /// <param name="brushPalettePreset">Brush palette used for cell colors.</param>
    /// <param name="allEntries">Cached field catalog.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="visibleColumns">Visible column count.</param>
    /// <param name="cellWidth">Stable cell width.</param>
    /// <param name="cellHeight">Stable cell height.</param>
    /// <param name="selectedRowIndex">Selected row index.</param>
    /// <param name="selectedColumnIndex">Selected column index.</param>
    /// <param name="cellClicked">Cell click callback.</param>
    /// <param name="insertColumn">Structural insertion callback.</param>
    /// <param name="removeColumn">Structural removal callback.</param>
    /// <returns>Configured data row.</returns>
    private static VisualElement CreateDataRow(ExcelDataWorkbookSheetDefinition sheet,
                                               ExcelDataBrushPalettePreset brushPalettePreset,
                                               IReadOnlyList<ExcelDataFieldCatalogEntry> allEntries,
                                               int rowIndex,
                                               int visibleColumns,
                                               float cellWidth,
                                               float cellHeight,
                                               int selectedRowIndex,
                                               int selectedColumnIndex,
                                               Action<int, int> cellClicked,
                                               Action<int> insertColumn,
                                               Action<int> removeColumn)
    {
        VisualElement row = CreateFixedRow(cellHeight);
        row.Add(CreateHeaderLabel(rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                  RowGutterWidth,
                                  cellHeight));

        for (int columnIndex = 1; columnIndex <= visibleColumns; columnIndex++)
        {
            row.Add(CreateCellButton(sheet,
                                     brushPalettePreset,
                                     allEntries,
                                     rowIndex,
                                     columnIndex,
                                     cellWidth,
                                     cellHeight,
                                     rowIndex == selectedRowIndex && columnIndex == selectedColumnIndex,
                                     cellClicked));

            if (columnIndex < visibleColumns)
                row.Add(CreateColumnSeparator(columnIndex + 1,
                                              cellHeight,
                                              insertColumn,
                                              removeColumn));
        }

        return row;
    }

    /// <summary>
    /// Creates one fixed-height horizontal row container.
    /// </summary>
    /// <param name="height">Stable row height.</param>
    /// <returns>Configured row container.</returns>
    private static VisualElement CreateFixedRow(float height)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexShrink = 0f;
        row.style.height = height;
        return row;
    }

    /// <summary>
    /// Creates one non-interactive Excel row or column header label.
    /// </summary>
    /// <param name="text">Visible header text.</param>
    /// <param name="width">Stable header width.</param>
    /// <param name="height">Stable header height.</param>
    /// <returns>Configured header label.</returns>
    private static Label CreateHeaderLabel(string text, float width, float height)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.height = height;
        label.style.flexShrink = 0f;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }
    #endregion

    #region Cell Rendering
    /// <summary>
    /// Creates one exact authored cell button with selected-state and payload-aware presentation.
    /// </summary>
    /// <param name="sheet">Active worksheet.</param>
    /// <param name="brushPalettePreset">Brush palette used for exact brush colors.</param>
    /// <param name="allEntries">Cached field catalog.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    /// <param name="cellWidth">Stable cell width.</param>
    /// <param name="cellHeight">Stable cell height.</param>
    /// <param name="selected">True when this is the current selected coordinate.</param>
    /// <param name="cellClicked">Cell click callback.</param>
    /// <returns>Configured cell button.</returns>
    private static Button CreateCellButton(ExcelDataWorkbookSheetDefinition sheet,
                                           ExcelDataBrushPalettePreset brushPalettePreset,
                                           IReadOnlyList<ExcelDataFieldCatalogEntry> allEntries,
                                           int rowIndex,
                                           int columnIndex,
                                           float cellWidth,
                                           float cellHeight,
                                           bool selected,
                                           Action<int, int> cellClicked)
    {
        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(rowIndex, columnIndex);
        Button button = new Button(() => cellClicked(rowIndex, columnIndex));
        button.AddToClassList("excel-data-workbook-cell");
        ManagementToolInteractiveElementColorUtility.ExcludeFromHierarchyColors(button);
        button.text = BuildCellText(cell, allEntries);
        button.tooltip = BuildCellTooltip(sheet, cell, allEntries, rowIndex, columnIndex);
        button.style.width = cellWidth;
        button.style.height = cellHeight;
        button.style.flexShrink = 0f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.overflow = Overflow.Hidden;

        if (cell != null)
        {
            button.style.backgroundColor = ExcelDataBrushPaletteColorUtility.ResolveCellColor(cell, brushPalettePreset);
            button.style.color = ExcelDataBrushPaletteColorUtility.ResolveCellTextColor(cell, brushPalettePreset);
        }

        if (selected)
        {
            Color selectionColor = new Color(0.3f, 0.7f, 1f, 1f);
            button.style.borderTopWidth = 2f;
            button.style.borderBottomWidth = 2f;
            button.style.borderLeftWidth = 2f;
            button.style.borderRightWidth = 2f;
            button.style.borderTopColor = selectionColor;
            button.style.borderBottomColor = selectionColor;
            button.style.borderLeftColor = selectionColor;
            button.style.borderRightColor = selectionColor;
        }

        return button;
    }

    /// <summary>
    /// Builds compact literal text or a concrete serialized-path label for one cell.
    /// </summary>
    /// <param name="cell">Authored cell definition.</param>
    /// <param name="allEntries">Cached field catalog.</param>
    /// <returns>Visible cell label.</returns>
    private static string BuildCellText(ExcelDataWorkbookCellDefinition cell,
                                        IReadOnlyList<ExcelDataFieldCatalogEntry> allEntries)
    {
        if (cell == null)
            return string.Empty;

        if (cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText)
            return cell.LiteralText ?? string.Empty;

        ExcelDataFieldCatalogEntry entry = FindEntryById(allEntries,
                                                         cell.FieldBinding == null ? string.Empty : cell.FieldBinding.FieldId);
        if (entry != null && entry.IsConcreteListElement)
            return entry.ReadablePath;

        string path = entry == null
            ? cell.FieldBinding == null ? string.Empty : cell.FieldBinding.SerializedPath
            : entry.ReadablePath;
        int splitIndex = path.LastIndexOf('.');
        return splitIndex >= 0 && splitIndex < path.Length - 1 ? path.Substring(splitIndex + 1) : path;
    }

    /// <summary>
    /// Builds a coordinate-exact tooltip describing payload, direction and data source.
    /// </summary>
    /// <param name="sheet">Active worksheet.</param>
    /// <param name="cell">Authored cell definition, or null.</param>
    /// <param name="allEntries">Cached field catalog.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    /// <returns>Detailed tooltip text.</returns>
    private static string BuildCellTooltip(ExcelDataWorkbookSheetDefinition sheet,
                                           ExcelDataWorkbookCellDefinition cell,
                                           IReadOnlyList<ExcelDataFieldCatalogEntry> allEntries,
                                           int rowIndex,
                                           int columnIndex)
    {
        string address = sheet.SheetName + "!" + ExcelDataWorkbookCoordinateUtility.BuildAddress(rowIndex, columnIndex);

        if (cell == null)
            return address + "\nEmpty workbook cell.";

        if (cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText)
            return address + "\nLiteral Text | " + cell.Direction + "\n" + cell.LiteralText;

        ExcelDataFieldCatalogEntry entry = FindEntryById(allEntries,
                                                         cell.FieldBinding == null ? string.Empty : cell.FieldBinding.FieldId);
        string fieldText = entry == null
            ? cell.FieldBinding == null ? "Unresolved Data Field" : cell.FieldBinding.SerializedPath
            : entry.DisplayName + "\n" + entry.AssetPath;
        return address + "\nData Field | " + cell.Direction + "\n" + fieldText;
    }
    #endregion

    #region Structural Separators
    /// <summary>
    /// Creates a vertical right-click target between two existing columns.
    /// </summary>
    /// <param name="insertionColumnIndex">One-based new empty column index.</param>
    /// <param name="height">Separator height matching its row.</param>
    /// <param name="insertColumn">Structural insertion callback.</param>
    /// <param name="removeColumn">Structural removal callback.</param>
    /// <returns>Configured separator.</returns>
    private static VisualElement CreateColumnSeparator(int insertionColumnIndex,
                                                       float height,
                                                       Action<int> insertColumn,
                                                       Action<int> removeColumn)
    {
        VisualElement separator = CreateSeparator(SeparatorSize, height);
        separator.AddToClassList("excel-data-column-insert-separator");
        separator.tooltip = "Right-click to insert a column here or remove the column on either side.";
        separator.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction("Insert Empty Column Here",
                                  action => insertColumn(insertionColumnIndex),
                                  DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            AppendColumnRemovalAction(evt.menu,
                                      "Remove Column Left",
                                      insertionColumnIndex - 1,
                                      removeColumn);
            AppendColumnRemovalAction(evt.menu,
                                      "Remove Column Right",
                                      insertionColumnIndex,
                                      removeColumn);
        }));
        return separator;
    }

    /// <summary>
    /// Creates a horizontal right-click target between two existing rows.
    /// </summary>
    /// <param name="insertionRowIndex">One-based new empty row index.</param>
    /// <param name="width">Separator width spanning gutter and visible cells.</param>
    /// <param name="insertRow">Structural insertion callback.</param>
    /// <param name="removeRow">Structural removal callback.</param>
    /// <returns>Configured separator.</returns>
    private static VisualElement CreateRowSeparator(int insertionRowIndex,
                                                    float width,
                                                    Action<int> insertRow,
                                                    Action<int> removeRow)
    {
        VisualElement separator = CreateSeparator(width, SeparatorSize);
        separator.AddToClassList("excel-data-row-insert-separator");
        separator.tooltip = "Right-click to insert a row here or remove the row on either side.";
        separator.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction("Insert Empty Row Here",
                                  action => insertRow(insertionRowIndex),
                                  DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            AppendRowRemovalAction(evt.menu,
                                   "Remove Row Above",
                                   insertionRowIndex - 1,
                                   removeRow);
            AppendRowRemovalAction(evt.menu,
                                   "Remove Row Below",
                                   insertionRowIndex,
                                   removeRow);
        }));
        return separator;
    }

    /// <summary>
    /// Adds one unambiguous column-removal action with its Excel column name.
    /// </summary>
    /// <param name="menu">Context menu receiving the removal action.</param>
    /// <param name="label">Relative side label shown to the user.</param>
    /// <param name="columnIndex">One-based column index removed by the action.</param>
    /// <param name="removeColumn">Structural removal callback.</param>
    private static void AppendColumnRemovalAction(DropdownMenu menu,
                                                  string label,
                                                  int columnIndex,
                                                  Action<int> removeColumn)
    {
        string columnName = ExcelDataWorkbookCoordinateUtility.ColumnIndexToName(columnIndex);
        menu.AppendAction(label + " (" + columnName + ")",
                          action => removeColumn(columnIndex),
                          DropdownMenuAction.AlwaysEnabled);
    }

    /// <summary>
    /// Adds one unambiguous row-removal action with its one-based row number.
    /// </summary>
    /// <param name="menu">Context menu receiving the removal action.</param>
    /// <param name="label">Relative side label shown to the user.</param>
    /// <param name="rowIndex">One-based row index removed by the action.</param>
    /// <param name="removeRow">Structural removal callback.</param>
    private static void AppendRowRemovalAction(DropdownMenu menu,
                                               string label,
                                               int rowIndex,
                                               Action<int> removeRow)
    {
        menu.AppendAction(label + " (" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")",
                          action => removeRow(rowIndex),
                          DropdownMenuAction.AlwaysEnabled);
    }

    /// <summary>
    /// Creates one stable insertion hit area with restrained hover feedback.
    /// </summary>
    /// <param name="width">Separator width.</param>
    /// <param name="height">Separator height.</param>
    /// <returns>Configured separator element.</returns>
    private static VisualElement CreateSeparator(float width, float height)
    {
        VisualElement separator = new VisualElement();
        separator.style.width = width;
        separator.style.height = height;
        separator.style.flexShrink = 0f;
        separator.RegisterCallback<PointerEnterEvent>(evt =>
            separator.style.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.28f));
        separator.RegisterCallback<PointerLeaveEvent>(evt =>
            separator.style.backgroundColor = Color.clear);
        return separator;
    }
    #endregion

    #region Catalog Lookup
    /// <summary>
    /// Finds a catalog entry by stable field ID.
    /// </summary>
    /// <param name="allEntries">Cached catalog entries.</param>
    /// <param name="fieldId">Stable field ID.</param>
    /// <returns>Matching entry, or null.</returns>
    private static ExcelDataFieldCatalogEntry FindEntryById(IReadOnlyList<ExcelDataFieldCatalogEntry> allEntries,
                                                             string fieldId)
    {
        if (allEntries == null || string.IsNullOrWhiteSpace(fieldId))
            return null;

        for (int entryIndex = 0; entryIndex < allEntries.Count; entryIndex++)
        {
            if (allEntries[entryIndex].FieldId == fieldId)
                return allEntries[entryIndex];
        }

        return null;
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Adds a non-destructive cap warning when authored preview dimensions exceed practical UI rendering limits.
    /// </summary>
    /// <param name="gridRoot">Grid root receiving the warning.</param>
    /// <param name="sheet">Active worksheet dimensions.</param>
    private static void AddCapWarningIfNeeded(VisualElement gridRoot, ExcelDataWorkbookSheetDefinition sheet)
    {
        if (sheet.PreviewRowCount <= MaxVisibleRows && sheet.PreviewColumnCount <= MaxVisibleColumns)
            return;

        Label capLabel = new Label("Grid preview capped at " + MaxVisibleRows + " x " + MaxVisibleColumns +
                                   ". Authored worksheet dimensions are preserved.");
        capLabel.style.whiteSpace = WhiteSpace.Normal;
        gridRoot.Add(capLabel);
    }
    #endregion

    #endregion
}
