using System;
using System.Collections.Generic;

/// <summary>
/// Applies exact grid-authoritative cell, preview and structural edits from workbook-layout interactions.
/// </summary>
internal static class ExcelDataWorkbookLayoutAuthoringUtility
{
    #region Methods

    #region Cell Authoring
    /// <summary>
    /// Paints one Data Field directly into the authoritative worksheet at an exact coordinate.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the painted field.</param>
    /// <param name="sheetName">Visible worksheet receiving the cell.</param>
    /// <param name="entry">Selected catalog field.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <param name="direction">Allowed transfer directions.</param>
    /// <param name="brushId">Stable selected brush identifier.</param>
    /// <param name="numberFormat">Optional invariant Excel number format.</param>
    public static void PaintDataFieldCell(ExcelDataWorkbookLayoutPreset layoutPreset,
                                          string sheetName,
                                          ExcelDataFieldCatalogEntry entry,
                                          int rowIndex,
                                          int columnIndex,
                                          ExcelDataTransferDirection direction,
                                          string brushId,
                                          string numberFormat)
    {
        UpsertDataFieldCell(layoutPreset,
                            sheetName,
                            rowIndex,
                            columnIndex,
                            entry,
                            direction,
                            brushId,
                            numberFormat);
    }

    /// <summary>
    /// Creates or replaces one Data Field cell while preserving exact workbook coordinates.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the authored cell.</param>
    /// <param name="sheetName">Authored user worksheet name.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <param name="entry">Current catalog entry bound to the cell.</param>
    /// <param name="direction">Allowed transfer directions.</param>
    /// <param name="brushId">Stable brush identifier used by the grid palette.</param>
    /// <param name="numberFormat">Optional invariant Excel number format.</param>
    public static void UpsertDataFieldCell(ExcelDataWorkbookLayoutPreset layoutPreset,
                                           string sheetName,
                                           int rowIndex,
                                           int columnIndex,
                                           ExcelDataFieldCatalogEntry entry,
                                           ExcelDataTransferDirection direction,
                                           string brushId,
                                           string numberFormat)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        ExcelDataWorkbookSheetDefinition sheet = ResolveOrCreateSheet(layoutPreset, sheetName);
        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(rowIndex, columnIndex);

        if (cell == null)
        {
            cell = new ExcelDataWorkbookCellDefinition();
            sheet.Cells.Add(cell);
        }

        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.ConfigureFromEntry(entry);
        cell.ConfigureDataField(sheet.SheetId,
                                rowIndex,
                                columnIndex,
                                binding,
                                direction,
                                brushId,
                                numberFormat);
    }

    /// <summary>
    /// Paints exact literal text used to organize the visible workbook without targeting Unity data.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the literal cell.</param>
    /// <param name="sheetName">Visible worksheet receiving the cell.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <param name="literalText">Exact text written to the workbook.</param>
    /// <param name="direction">Allowed transfer directions.</param>
    /// <param name="brushId">Stable selected brush identifier.</param>
    /// <param name="validateDuringImport">True when preview should report changed literal text.</param>
    public static void PaintLiteralCell(ExcelDataWorkbookLayoutPreset layoutPreset,
                                        string sheetName,
                                        int rowIndex,
                                        int columnIndex,
                                        string literalText,
                                        ExcelDataTransferDirection direction,
                                        string brushId,
                                        bool validateDuringImport)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        ExcelDataWorkbookSheetDefinition sheet = ResolveOrCreateSheet(layoutPreset, sheetName);
        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(rowIndex, columnIndex);

        if (cell == null)
        {
            cell = new ExcelDataWorkbookCellDefinition();
            sheet.Cells.Add(cell);
        }

        cell.ConfigureLiteralText(sheet.SheetId,
                                  rowIndex,
                                  columnIndex,
                                  literalText,
                                  direction,
                                  brushId,
                                  validateDuringImport);
    }

    /// <summary>
    /// Erases one exact cell from the authoritative worksheet.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing the cell.</param>
    /// <param name="sheetName">Visible worksheet containing the cell.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <returns>True when an authored cell was removed.</returns>
    public static bool EraseCell(ExcelDataWorkbookLayoutPreset layoutPreset,
                                 string sheetName,
                                 int rowIndex,
                                 int columnIndex)
    {
        ExcelDataWorkbookSheetDefinition sheet = FindSheet(layoutPreset, ResolveSheetName(layoutPreset, sheetName));

        if (sheet == null)
            return false;

        bool removed = false;

        for (int cellIndex = sheet.Cells.Count - 1; cellIndex >= 0; cellIndex--)
        {
            ExcelDataWorkbookCellDefinition cell = sheet.Cells[cellIndex];

            if (cell != null && cell.MatchesCell(sheet.SheetId, rowIndex, columnIndex))
            {
                sheet.Cells.RemoveAt(cellIndex);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>
    /// Updates editable settings for one selected cell while preserving its payload identity.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing the selected cell.</param>
    /// <param name="sheetName">Visible worksheet containing the selected cell.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <param name="direction">Updated transfer direction.</param>
    /// <param name="literalText">Updated literal text when the cell is Literal Text.</param>
    /// <param name="validateLiteralDuringImport">Updated literal validation toggle.</param>
    /// <param name="numberFormat">Updated number format when the cell is a Data Field.</param>
    /// <returns>True when a selected cell was found and updated.</returns>
    public static bool UpdateCellSettings(ExcelDataWorkbookLayoutPreset layoutPreset,
                                          string sheetName,
                                          int rowIndex,
                                          int columnIndex,
                                          ExcelDataTransferDirection direction,
                                          string literalText,
                                          bool validateLiteralDuringImport,
                                          string numberFormat)
    {
        ExcelDataWorkbookSheetDefinition sheet = FindSheet(layoutPreset, ResolveSheetName(layoutPreset, sheetName));

        if (sheet == null)
            return false;

        ExcelDataWorkbookCellDefinition cell = sheet.FindCell(rowIndex, columnIndex);

        if (cell == null)
            return false;

        switch (cell.ContentKind)
        {
            case ExcelDataWorkbookCellContentKind.LiteralText:
                cell.ConfigureLiteralText(sheet.SheetId,
                                          rowIndex,
                                          columnIndex,
                                          literalText,
                                          direction,
                                          cell.BrushId,
                                          validateLiteralDuringImport);
                break;
            default:
                cell.ConfigureDataField(sheet.SheetId,
                                        rowIndex,
                                        columnIndex,
                                        cell.FieldBinding,
                                        direction,
                                        cell.BrushId,
                                        numberFormat);
                break;
        }

        return true;
    }
    #endregion

    #region Structural Authoring
    /// <summary>
    /// Inserts an empty row and shifts authoritative coordinates at or below it.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheetName">Visible worksheet receiving the empty row.</param>
    /// <param name="insertionRowIndex">One-based row that becomes empty.</param>
    public static void InsertEmptyRow(ExcelDataWorkbookLayoutPreset layoutPreset,
                                      string sheetName,
                                      int insertionRowIndex)
    {
        ExcelDataWorkbookSheetDefinition sheet = ResolveOrCreateSheet(layoutPreset, sheetName);
        sheet.InsertEmptyRow(insertionRowIndex);
        SynchronizePrimaryGridDefaults(layoutPreset, sheet);
    }

    /// <summary>
    /// Inserts an empty column and shifts authoritative coordinates at or right of it.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheetName">Visible worksheet receiving the empty column.</param>
    /// <param name="insertionColumnIndex">One-based column that becomes empty.</param>
    public static void InsertEmptyColumn(ExcelDataWorkbookLayoutPreset layoutPreset,
                                         string sheetName,
                                         int insertionColumnIndex)
    {
        ExcelDataWorkbookSheetDefinition sheet = ResolveOrCreateSheet(layoutPreset, sheetName);
        sheet.InsertEmptyColumn(insertionColumnIndex);
        SynchronizePrimaryGridDefaults(layoutPreset, sheet);
    }

    /// <summary>
    /// Updates active-sheet preview dimensions after an explicit toolbar edit.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing the sheet.</param>
    /// <param name="sheet">Active worksheet definition.</param>
    /// <param name="rowCount">New preview row count.</param>
    /// <param name="columnCount">New preview column count.</param>
    /// <param name="cellWidth">New preview cell width.</param>
    /// <param name="cellHeight">New preview cell height.</param>
    public static void ConfigureSheetPreview(ExcelDataWorkbookLayoutPreset layoutPreset,
                                             ExcelDataWorkbookSheetDefinition sheet,
                                             int rowCount,
                                             int columnCount,
                                             int cellWidth,
                                             int cellHeight)
    {
        if (layoutPreset == null || sheet == null)
            return;

        sheet.ConfigurePreview(rowCount, columnCount, cellWidth, cellHeight);
        SynchronizePrimaryGridDefaults(layoutPreset, sheet);
    }
    #endregion

    #region Sheet Resolution
    /// <summary>
    /// Resolves one authored worksheet by name or creates it from layout preview defaults.
    /// </summary>
    /// <param name="layoutPreset">Layout preset that owns the user worksheet.</param>
    /// <param name="requestedSheetName">Requested user worksheet name.</param>
    /// <returns>Existing or newly created grid-authoritative worksheet definition.</returns>
    public static ExcelDataWorkbookSheetDefinition ResolveOrCreateSheet(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                                         string requestedSheetName)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        string sheetName = ResolveSheetName(layoutPreset, requestedSheetName);
        ExcelDataWorkbookSheetDefinition sheet = FindSheet(layoutPreset, sheetName);

        if (sheet != null)
            return sheet;

        ExcelDataWorkbookSheetDefinition createdSheet = new ExcelDataWorkbookSheetDefinition();
        createdSheet.Configure(sheetName,
                               layoutPreset.DefaultGridRows,
                               layoutPreset.DefaultGridColumns,
                               layoutPreset.DefaultCellWidth,
                               layoutPreset.DefaultCellHeight,
                               true,
                               true,
                               ExcelDataWorkbookSheetVisibility.Visible);
        layoutPreset.SheetDefinitions.Add(createdSheet);
        return createdSheet;
    }

    /// <summary>
    /// Finds one authored worksheet by its visible name.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to search.</param>
    /// <param name="sheetName">Visible worksheet name.</param>
    /// <returns>Matching worksheet, or null.</returns>
    public static ExcelDataWorkbookSheetDefinition FindSheet(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                              string sheetName)
    {
        if (layoutPreset == null)
            return null;

        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet != null && string.Equals(sheet.SheetName, sheetName, StringComparison.Ordinal))
                return sheet;
        }

        return null;
    }

    /// <summary>
    /// Resolves an empty sheet request to the layout's current default worksheet name.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing the fallback name.</param>
    /// <param name="requestedSheetName">Requested worksheet name.</param>
    /// <returns>Non-empty visible worksheet name.</returns>
    private static string ResolveSheetName(ExcelDataWorkbookLayoutPreset layoutPreset, string requestedSheetName)
    {
        if (!string.IsNullOrWhiteSpace(requestedSheetName))
            return requestedSheetName;

        return layoutPreset == null || string.IsNullOrWhiteSpace(layoutPreset.ObjectsSheetName)
            ? "Objects"
            : layoutPreset.ObjectsSheetName;
    }

    /// <summary>
    /// Keeps new-sheet defaults aligned with the primary worksheet after explicit preview edits.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing shared defaults.</param>
    /// <param name="sheet">Authoritative worksheet whose dimensions changed.</param>
    private static void SynchronizePrimaryGridDefaults(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                       ExcelDataWorkbookSheetDefinition sheet)
    {
        if (layoutPreset == null || sheet == null)
            return;

        if (layoutPreset.SheetDefinitions.Count <= 0 || layoutPreset.SheetDefinitions[0] != sheet)
            return;

        layoutPreset.ConfigureGridDefaults(sheet.PreviewRowCount,
                                           sheet.PreviewColumnCount,
                                           sheet.PreviewCellWidth,
                                           sheet.PreviewCellHeight);
    }
    #endregion

    #endregion
}
