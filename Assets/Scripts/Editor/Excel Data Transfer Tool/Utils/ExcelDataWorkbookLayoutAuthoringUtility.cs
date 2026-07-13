using System;
using System.Collections.Generic;

/// <summary>
/// Applies exact grid-authoritative sheet and Data Field cell edits from editor tool interactions.
/// </summary>
internal static class ExcelDataWorkbookLayoutAuthoringUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Paints one Data Field through the current grid UI while keeping both staged layout stores synchronized.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the painted field.</param>
    /// <param name="entry">Selected catalog field.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    public static void PaintDataFieldCell(ExcelDataWorkbookLayoutPreset layoutPreset,
                                          ExcelDataFieldCatalogEntry entry,
                                          int rowIndex,
                                          int columnIndex)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        ExcelDataCellBrushMapping mapping =
            ExcelDataLayoutBrushGridUtility.FindMapping(layoutPreset,
                                                        layoutPreset.ObjectsSheetName,
                                                        rowIndex,
                                                        columnIndex);

        if (mapping == null)
        {
            mapping = new ExcelDataCellBrushMapping();
            layoutPreset.CellMappings.Add(mapping);
        }

        mapping.Configure(layoutPreset.ObjectsSheetName,
                          rowIndex,
                          columnIndex,
                          entry.FieldId,
                          ExcelDataTransferDirection.Both,
                          entry.PathTemplate,
                          string.Empty);
        UpsertDataFieldCell(layoutPreset,
                            layoutPreset.ObjectsSheetName,
                            rowIndex,
                            columnIndex,
                            entry,
                            ExcelDataTransferDirection.Both,
                            string.Empty,
                            string.Empty);
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
    #endregion

    #region Sheet Resolution
    /// <summary>
    /// Resolves one authored worksheet by name or creates it from layout preview defaults.
    /// </summary>
    /// <param name="layoutPreset">Layout preset that owns the user worksheet.</param>
    /// <param name="requestedSheetName">Requested user worksheet name.</param>
    /// <returns>Existing or newly created grid-authoritative worksheet definition.</returns>
    private static ExcelDataWorkbookSheetDefinition ResolveOrCreateSheet(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                                         string requestedSheetName)
    {
        string sheetName = string.IsNullOrWhiteSpace(requestedSheetName) ? layoutPreset.ObjectsSheetName : requestedSheetName;
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet != null && string.Equals(sheet.SheetName, sheetName, StringComparison.Ordinal))
                return sheet;
        }

        ExcelDataWorkbookSheetDefinition createdSheet = new ExcelDataWorkbookSheetDefinition();
        createdSheet.Configure(sheetName,
                               layoutPreset.DefaultGridRows,
                               layoutPreset.DefaultGridColumns,
                               layoutPreset.DefaultCellWidth,
                               layoutPreset.DefaultCellHeight,
                               true,
                               true,
                               ExcelDataWorkbookSheetVisibility.Visible);
        sheets.Add(createdSheet);
        return createdSheet;
    }
    #endregion

    #endregion
}
