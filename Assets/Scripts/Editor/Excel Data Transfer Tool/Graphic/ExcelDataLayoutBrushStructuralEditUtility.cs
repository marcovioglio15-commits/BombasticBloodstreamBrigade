using System.Globalization;
using UnityEditor;

/// <summary>
/// Identifies one explicit structural operation requested from a grid separator.
/// </summary>
internal enum ExcelDataLayoutStructuralEditKind
{
    InsertRow = 0,
    InsertColumn = 1,
    RemoveRow = 2,
    RemoveColumn = 3
}

/// <summary>
/// Executes reversible worksheet row and column edits while preserving selection and confirmation rules.
/// </summary>
internal static class ExcelDataLayoutBrushStructuralEditUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Routes one structural request through its insertion or confirmed-removal implementation.
    /// </summary>
    /// <param name="editKind">Requested row or column operation.</param>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheet">Active worksheet receiving the structural edit.</param>
    /// <param name="coordinateIndex">One-based row or column index used by the operation.</param>
    /// <param name="selectedRowIndex">Selected row adjusted when coordinates shift.</param>
    /// <param name="selectedColumnIndex">Selected column adjusted when coordinates shift.</param>
    /// <param name="status">User-facing operation result or validation failure.</param>
    /// <returns>True when the requested structural edit was applied.</returns>
    public static bool TryExecute(ExcelDataLayoutStructuralEditKind editKind,
                                  ExcelDataWorkbookLayoutPreset layoutPreset,
                                  ExcelDataWorkbookSheetDefinition sheet,
                                  int coordinateIndex,
                                  ref int selectedRowIndex,
                                  ref int selectedColumnIndex,
                                  out string status)
    {
        switch (editKind)
        {
            case ExcelDataLayoutStructuralEditKind.InsertRow:
                return TryInsertRow(layoutPreset,
                                    sheet,
                                    coordinateIndex,
                                    ref selectedRowIndex,
                                    out status);
            case ExcelDataLayoutStructuralEditKind.InsertColumn:
                return TryInsertColumn(layoutPreset,
                                       sheet,
                                       coordinateIndex,
                                       ref selectedColumnIndex,
                                       out status);
            case ExcelDataLayoutStructuralEditKind.RemoveRow:
                return TryRemoveRow(layoutPreset,
                                    sheet,
                                    coordinateIndex,
                                    ref selectedRowIndex,
                                    out status);
            case ExcelDataLayoutStructuralEditKind.RemoveColumn:
                return TryRemoveColumn(layoutPreset,
                                       sheet,
                                       coordinateIndex,
                                       ref selectedColumnIndex,
                                       out status);
            default:
                status = "Unsupported workbook structural edit: " + editKind + ".";
                return false;
        }
    }
    #endregion

    #region Insert Operations
    /// <summary>
    /// Inserts one empty row and keeps the selected payload coordinate stable after the shift.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheet">Active worksheet receiving the row.</param>
    /// <param name="insertionRowIndex">One-based row that becomes empty.</param>
    /// <param name="selectedRowIndex">Selected row updated when its payload shifts.</param>
    /// <param name="status">User-facing operation result.</param>
    /// <returns>True when the row was inserted.</returns>
    public static bool TryInsertRow(ExcelDataWorkbookLayoutPreset layoutPreset,
                                    ExcelDataWorkbookSheetDefinition sheet,
                                    int insertionRowIndex,
                                    ref int selectedRowIndex,
                                    out string status)
    {
        if (layoutPreset == null || sheet == null)
        {
            status = "Cannot insert row: missing workbook layout or active worksheet.";
            return false;
        }

        Undo.RecordObject(layoutPreset, "Insert Excel Workbook Row");
        ExcelDataWorkbookLayoutAuthoringUtility.InsertEmptyRow(layoutPreset,
                                                               sheet.SheetName,
                                                               insertionRowIndex);

        if (selectedRowIndex >= insertionRowIndex)
            selectedRowIndex++;

        status = "Inserted empty row " + insertionRowIndex.ToString(CultureInfo.InvariantCulture) +
                 " in " + sheet.SheetName + ".";
        return true;
    }

    /// <summary>
    /// Inserts one empty column and keeps the selected payload coordinate stable after the shift.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheet">Active worksheet receiving the column.</param>
    /// <param name="insertionColumnIndex">One-based column that becomes empty.</param>
    /// <param name="selectedColumnIndex">Selected column updated when its payload shifts.</param>
    /// <param name="status">User-facing operation result.</param>
    /// <returns>True when the column was inserted.</returns>
    public static bool TryInsertColumn(ExcelDataWorkbookLayoutPreset layoutPreset,
                                       ExcelDataWorkbookSheetDefinition sheet,
                                       int insertionColumnIndex,
                                       ref int selectedColumnIndex,
                                       out string status)
    {
        if (layoutPreset == null || sheet == null)
        {
            status = "Cannot insert column: missing workbook layout or active worksheet.";
            return false;
        }

        Undo.RecordObject(layoutPreset, "Insert Excel Workbook Column");
        ExcelDataWorkbookLayoutAuthoringUtility.InsertEmptyColumn(layoutPreset,
                                                                  sheet.SheetName,
                                                                  insertionColumnIndex);

        if (selectedColumnIndex >= insertionColumnIndex)
            selectedColumnIndex++;

        status = "Inserted empty column " +
                 ExcelDataWorkbookCoordinateUtility.ColumnIndexToName(insertionColumnIndex) +
                 " in " + sheet.SheetName + ".";
        return true;
    }
    #endregion

    #region Remove Operations
    /// <summary>
    /// Removes one row after warning for authored payloads and keeps selection inside the new bounds.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheet">Active worksheet losing the row.</param>
    /// <param name="removalRowIndex">One-based row removed from the layout.</param>
    /// <param name="selectedRowIndex">Selected row adjusted after the shift.</param>
    /// <param name="status">User-facing operation result or cancellation reason.</param>
    /// <returns>True when the row was removed.</returns>
    public static bool TryRemoveRow(ExcelDataWorkbookLayoutPreset layoutPreset,
                                    ExcelDataWorkbookSheetDefinition sheet,
                                    int removalRowIndex,
                                    ref int selectedRowIndex,
                                    out string status)
    {
        if (!CanRemoveRow(layoutPreset, sheet, removalRowIndex, out status))
            return false;

        int authoredCellCount = sheet.CountAuthoredCellsInRow(removalRowIndex);

        if (!ConfirmPopulatedRemoval(sheet.SheetName,
                                     "row " + removalRowIndex.ToString(CultureInfo.InvariantCulture),
                                     authoredCellCount))
        {
            status = "Row removal cancelled.";
            return false;
        }

        Undo.RecordObject(layoutPreset, "Remove Excel Workbook Row");
        int removedCellCount = ExcelDataWorkbookLayoutAuthoringUtility.RemoveRow(layoutPreset,
                                                                                 sheet.SheetName,
                                                                                 removalRowIndex);

        if (selectedRowIndex > removalRowIndex)
            selectedRowIndex--;

        if (selectedRowIndex > sheet.PreviewRowCount)
            selectedRowIndex = sheet.PreviewRowCount;

        status = BuildRemovalStatus("row " + removalRowIndex.ToString(CultureInfo.InvariantCulture),
                                    sheet.SheetName,
                                    removedCellCount);
        return true;
    }

    /// <summary>
    /// Removes one column after warning for authored payloads and keeps selection inside the new bounds.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the structural edit.</param>
    /// <param name="sheet">Active worksheet losing the column.</param>
    /// <param name="removalColumnIndex">One-based column removed from the layout.</param>
    /// <param name="selectedColumnIndex">Selected column adjusted after the shift.</param>
    /// <param name="status">User-facing operation result or cancellation reason.</param>
    /// <returns>True when the column was removed.</returns>
    public static bool TryRemoveColumn(ExcelDataWorkbookLayoutPreset layoutPreset,
                                       ExcelDataWorkbookSheetDefinition sheet,
                                       int removalColumnIndex,
                                       ref int selectedColumnIndex,
                                       out string status)
    {
        if (!CanRemoveColumn(layoutPreset, sheet, removalColumnIndex, out status))
            return false;

        int authoredCellCount = sheet.CountAuthoredCellsInColumn(removalColumnIndex);
        string columnName = ExcelDataWorkbookCoordinateUtility.ColumnIndexToName(removalColumnIndex);

        if (!ConfirmPopulatedRemoval(sheet.SheetName,
                                     "column " + columnName,
                                     authoredCellCount))
        {
            status = "Column removal cancelled.";
            return false;
        }

        Undo.RecordObject(layoutPreset, "Remove Excel Workbook Column");
        int removedCellCount = ExcelDataWorkbookLayoutAuthoringUtility.RemoveColumn(layoutPreset,
                                                                                    sheet.SheetName,
                                                                                    removalColumnIndex);

        if (selectedColumnIndex > removalColumnIndex)
            selectedColumnIndex--;

        if (selectedColumnIndex > sheet.PreviewColumnCount)
            selectedColumnIndex = sheet.PreviewColumnCount;

        status = BuildRemovalStatus("column " + columnName,
                                    sheet.SheetName,
                                    removedCellCount);
        return true;
    }
    #endregion

    #region Validation And Confirmation
    /// <summary>
    /// Validates one requested row removal without modifying authored dimensions.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the edit.</param>
    /// <param name="sheet">Active worksheet losing the row.</param>
    /// <param name="removalRowIndex">One-based row requested for removal.</param>
    /// <param name="status">Validation failure shown to the user.</param>
    /// <returns>True when the requested row may be removed.</returns>
    private static bool CanRemoveRow(ExcelDataWorkbookLayoutPreset layoutPreset,
                                     ExcelDataWorkbookSheetDefinition sheet,
                                     int removalRowIndex,
                                     out string status)
    {
        if (layoutPreset == null || sheet == null)
        {
            status = "Cannot remove row: missing workbook layout or active worksheet.";
            return false;
        }

        if (sheet.PreviewRowCount <= 1)
        {
            status = "Cannot remove the final worksheet row.";
            return false;
        }

        if (removalRowIndex < 1 || removalRowIndex > sheet.PreviewRowCount)
        {
            status = "Cannot remove row: coordinate is outside the worksheet preview.";
            return false;
        }

        status = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates one requested column removal without modifying authored dimensions.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the edit.</param>
    /// <param name="sheet">Active worksheet losing the column.</param>
    /// <param name="removalColumnIndex">One-based column requested for removal.</param>
    /// <param name="status">Validation failure shown to the user.</param>
    /// <returns>True when the requested column may be removed.</returns>
    private static bool CanRemoveColumn(ExcelDataWorkbookLayoutPreset layoutPreset,
                                        ExcelDataWorkbookSheetDefinition sheet,
                                        int removalColumnIndex,
                                        out string status)
    {
        if (layoutPreset == null || sheet == null)
        {
            status = "Cannot remove column: missing workbook layout or active worksheet.";
            return false;
        }

        if (sheet.PreviewColumnCount <= 1)
        {
            status = "Cannot remove the final worksheet column.";
            return false;
        }

        if (removalColumnIndex < 1 || removalColumnIndex > sheet.PreviewColumnCount)
        {
            status = "Cannot remove column: coordinate is outside the worksheet preview.";
            return false;
        }

        status = string.Empty;
        return true;
    }

    /// <summary>
    /// Requests explicit confirmation only when a structural deletion would discard authored payloads.
    /// </summary>
    /// <param name="sheetName">Visible worksheet containing the payloads.</param>
    /// <param name="coordinateLabel">Readable row or column label.</param>
    /// <param name="authoredCellCount">Number of payloads that would be deleted.</param>
    /// <returns>True for empty structures or after explicit user confirmation.</returns>
    private static bool ConfirmPopulatedRemoval(string sheetName,
                                                string coordinateLabel,
                                                int authoredCellCount)
    {
        if (authoredCellCount <= 0)
            return true;

        string payloadLabel = authoredCellCount == 1 ? "authored cell" : "authored cells";
        return EditorUtility.DisplayDialog("Remove Populated Workbook Structure",
                                           coordinateLabel + " in " + sheetName + " contains " +
                                           authoredCellCount.ToString(CultureInfo.InvariantCulture) + " " +
                                           payloadLabel + ". Removing it deletes those payloads and shifts the following coordinates. This operation supports Undo.",
                                           "Remove",
                                           "Cancel");
    }

    /// <summary>
    /// Builds a concise structural removal result including discarded payload count when applicable.
    /// </summary>
    /// <param name="coordinateLabel">Readable removed row or column label.</param>
    /// <param name="sheetName">Visible worksheet name.</param>
    /// <param name="removedCellCount">Number of authored payloads deleted by the operation.</param>
    /// <returns>User-facing operation result.</returns>
    private static string BuildRemovalStatus(string coordinateLabel,
                                             string sheetName,
                                             int removedCellCount)
    {
        if (removedCellCount <= 0)
            return "Removed empty " + coordinateLabel + " from " + sheetName + ".";

        return "Removed " + coordinateLabel + " from " + sheetName + " with " +
               removedCellCount.ToString(CultureInfo.InvariantCulture) + " authored cell" +
               (removedCellCount == 1 ? "." : "s.");
    }
    #endregion

    #endregion
}
