using System.Collections.Generic;

/// <summary>
/// Centralizes the exact authored-cell selection used by value and formula workbook readers.
/// </summary>
internal static class ExcelDataWorkbookImportCellUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Groups import-enabled authored columns by their one-based worksheet row.
    /// </summary>
    /// <param name="sheetDefinition">Worksheet whose sparse cells are inspected.</param>
    /// <returns>Requested column indexes grouped by row.</returns>
    public static Dictionary<int, List<int>> BuildRequestedColumnsByRow(ExcelDataWorkbookSheetDefinition sheetDefinition)
    {
        Dictionary<int, List<int>> columnsByRow = new Dictionary<int, List<int>>();
        List<ExcelDataWorkbookCellDefinition> cells = sheetDefinition.Cells;

        // Preserve authored order while deduplicating repeated coordinate requests.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (!IncludesImportRead(cell))
                continue;

            List<int> columns;

            if (!columnsByRow.TryGetValue(cell.RowIndex, out columns))
            {
                columns = new List<int>();
                columnsByRow.Add(cell.RowIndex, columns);
            }

            if (!columns.Contains(cell.ColumnIndex))
                columns.Add(cell.ColumnIndex);
        }

        return columnsByRow;
    }

    /// <summary>
    /// Builds packed coordinate keys for every import-enabled Data Field or validated literal.
    /// </summary>
    /// <param name="sheetDefinition">Worksheet whose sparse cells are inspected.</param>
    /// <returns>Exact coordinate keys consumed by OpenXML metadata readers.</returns>
    public static HashSet<long> BuildRequestedCoordinateKeys(ExcelDataWorkbookSheetDefinition sheetDefinition)
    {
        HashSet<long> coordinateKeys = new HashSet<long>();
        Dictionary<int, List<int>> columnsByRow = BuildRequestedColumnsByRow(sheetDefinition);

        // Flatten the shared row lookup without repeating authored-cell filtering rules.
        foreach (KeyValuePair<int, List<int>> requestedRow in columnsByRow)
        {
            for (int columnIndex = 0; columnIndex < requestedRow.Value.Count; columnIndex++)
                coordinateKeys.Add(ExcelDataWorkbookCoordinateUtility.BuildKey(requestedRow.Key,
                                                                                requestedRow.Value[columnIndex]));
        }

        return coordinateKeys;
    }

    /// <summary>
    /// Reports whether a sheet contains any coordinate needed by import preview.
    /// </summary>
    /// <param name="sheetDefinition">Worksheet definition to inspect.</param>
    /// <returns>True when at least one Data Field or validated literal participates in import.</returns>
    public static bool ContainsImportCells(ExcelDataWorkbookSheetDefinition sheetDefinition)
    {
        List<ExcelDataWorkbookCellDefinition> cells = sheetDefinition.Cells;

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            if (IncludesImportRead(cells[cellIndex]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reports whether one cell must be read for data import or literal validation.
    /// </summary>
    /// <param name="cell">Authored cell definition.</param>
    /// <returns>True when preview needs the exact worksheet value.</returns>
    public static bool IncludesImportRead(ExcelDataWorkbookCellDefinition cell)
    {
        if (cell == null || !cell.IncludesImport() || cell.RowIndex < 1 || cell.ColumnIndex < 1)
            return false;

        return cell.ContentKind == ExcelDataWorkbookCellContentKind.DataField || cell.ValidateLiteralDuringImport;
    }
    #endregion

    #endregion
}
