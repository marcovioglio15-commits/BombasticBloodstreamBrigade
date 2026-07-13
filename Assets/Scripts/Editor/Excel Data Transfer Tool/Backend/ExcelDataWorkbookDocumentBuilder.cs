using System;
using System.Collections.Generic;

/// <summary>
/// Converts grid-authoritative layout definitions into exact in-memory workbook matrices.
/// </summary>
internal static class ExcelDataWorkbookDocumentBuilder
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds export-visible worksheet matrices and captures every resolved cell exactly once.
    /// </summary>
    /// <param name="layoutPreset">Layout preset containing sheet and cell definitions.</param>
    /// <param name="fieldValueResolver">Callback that resolves typed snapshots for Data Field cells.</param>
    /// <returns>Workbook document plus sheet, cell, count and warning records.</returns>
    public static ExcelDataWorkbookExportBuildResult BuildExportDocument(
        ExcelDataWorkbookLayoutPreset layoutPreset,
        Func<ExcelDataFieldBinding, ExcelDataSerializedValueSnapshot> fieldValueResolver)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        ExcelDataWorkbookExportBuildResult result = new ExcelDataWorkbookExportBuildResult();
        List<ExcelDataWorkbookSheetDefinition> sheetDefinitions = layoutPreset.SheetDefinitions;

        // Build sheets independently so sparse coordinates never shift neighboring values.
        for (int sheetIndex = 0; sheetIndex < sheetDefinitions.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheetDefinition = sheetDefinitions[sheetIndex];

            if (sheetDefinition == null || !sheetDefinition.ExportEnabled)
                continue;

            AddExportSheet(result, sheetDefinition, fieldValueResolver);
        }

        return result;
    }
    #endregion

    #region Sheet Building
    /// <summary>
    /// Adds one export-enabled sheet when it contains at least one authored export cell.
    /// </summary>
    /// <param name="result">Build result receiving the worksheet and exported cell records.</param>
    /// <param name="sheetDefinition">Authored worksheet definition.</param>
    /// <param name="fieldValueResolver">Typed data-field snapshot resolver.</param>
    private static void AddExportSheet(ExcelDataWorkbookExportBuildResult result,
                                       ExcelDataWorkbookSheetDefinition sheetDefinition,
                                       Func<ExcelDataFieldBinding, ExcelDataSerializedValueSnapshot> fieldValueResolver)
    {
        List<ExcelDataWorkbookCellDefinition> cells = sheetDefinition.Cells;
        HashSet<long> coordinates = new HashSet<long>();
        int maximumRowIndex = 0;
        int maximumColumnIndex = 0;

        // Validate ownership, reject duplicate export coordinates and determine exact used dimensions.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (!IncludesExportCell(cell))
                continue;

            if (!string.Equals(cell.SheetId, sheetDefinition.SheetId, StringComparison.Ordinal))
                throw new InvalidOperationException("Workbook cell references a different owner sheet: " + cell.SheetId);

            long coordinate = ExcelDataWorkbookCoordinateUtility.BuildKey(cell.RowIndex, cell.ColumnIndex);

            if (!coordinates.Add(coordinate))
                throw new InvalidOperationException("Duplicate export cell at " + sheetDefinition.SheetName + "!" + cell.RowIndex + "," + cell.ColumnIndex + ".");

            maximumRowIndex = Math.Max(maximumRowIndex, cell.RowIndex);
            maximumColumnIndex = Math.Max(maximumColumnIndex, cell.ColumnIndex);
        }

        if (maximumRowIndex <= 0 || maximumColumnIndex <= 0)
            return;

        ExcelDataWorkbookSheetDocument sheet =
            result.Document.AddSheet(sheetDefinition.SheetName,
                                     maximumRowIndex,
                                     maximumColumnIndex,
                                     sheetDefinition.Visibility,
                                     sheetDefinition.PreviewCellWidth,
                                     true);
        result.RegisterSheet(sheetDefinition);

        // Resolve each data field once and write its typed value at the exact one-based coordinate.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (!IncludesExportCell(cell))
                continue;

            ExcelDataSerializedValueSnapshot snapshot = ResolveExportSnapshot(cell, fieldValueResolver);
            sheet.SetValue(cell.RowIndex, cell.ColumnIndex, snapshot.Value);
            result.RegisterCell(sheetDefinition, cell, snapshot);
        }
    }

    /// <summary>
    /// Reports whether one authored definition has valid coordinates and participates in export.
    /// </summary>
    /// <param name="cell">Cell definition to inspect.</param>
    /// <returns>True when export must preserve the cell coordinate even if its field cannot be resolved.</returns>
    private static bool IncludesExportCell(ExcelDataWorkbookCellDefinition cell)
    {
        return cell != null &&
               cell.IncludesExport() &&
               cell.RowIndex > 0 &&
               cell.ColumnIndex > 0 &&
               !string.IsNullOrWhiteSpace(cell.SheetId);
    }

    /// <summary>
    /// Resolves one literal or Data Field cell into a typed value snapshot with isolated diagnostics.
    /// </summary>
    /// <param name="cell">Cell definition being materialized.</param>
    /// <param name="fieldValueResolver">Callback used by Data Field cells.</param>
    /// <returns>Typed value snapshot; failures retain a null value and warning instead of shifting the grid.</returns>
    private static ExcelDataSerializedValueSnapshot ResolveExportSnapshot(
        ExcelDataWorkbookCellDefinition cell,
        Func<ExcelDataFieldBinding, ExcelDataSerializedValueSnapshot> fieldValueResolver)
    {
        switch (cell.ContentKind)
        {
            case ExcelDataWorkbookCellContentKind.LiteralText:
                return ExcelDataSerializedValueSnapshot.CreateValue(cell.LiteralText ?? string.Empty, string.Empty);
            case ExcelDataWorkbookCellContentKind.DataField:
                if (cell.FieldBinding == null || !cell.FieldBinding.IsUsable())
                    return ExcelDataSerializedValueSnapshot.CreateWarning("Cell has no usable Data Field binding.", string.Empty);

                if (fieldValueResolver == null)
                    return ExcelDataSerializedValueSnapshot.CreateWarning("No Data Field resolver was supplied.", string.Empty);

                try
                {
                    return fieldValueResolver(cell.FieldBinding) ??
                           ExcelDataSerializedValueSnapshot.CreateWarning("Data Field resolver returned no snapshot.", string.Empty);
                }
                catch (Exception exception)
                {
                    return ExcelDataSerializedValueSnapshot.CreateWarning("Data Field read failed: " + exception.Message, string.Empty);
                }
            default:
                return ExcelDataSerializedValueSnapshot.CreateWarning("Unsupported workbook cell content kind: " + cell.ContentKind, string.Empty);
        }
    }
    #endregion

    #endregion
}
