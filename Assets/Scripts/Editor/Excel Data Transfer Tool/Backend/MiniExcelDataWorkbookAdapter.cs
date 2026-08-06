using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;

/// <summary>
/// Writes cell-oriented workbook documents through the public editor-only MiniExcel dependency.
/// </summary>
internal sealed class MiniExcelDataWorkbookAdapter : IExcelDataWorkbookAdapter
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Writes exact worksheet matrices without generated headers or normalized metadata columns.
    /// </summary>
    /// <param name="targetWorkbookPath">Absolute or project-relative target workbook path.</param>
    /// <param name="document">Workbook document containing exact worksheet matrices.</param>
    /// <returns>Absolute path written by MiniExcel.</returns>
    public string SaveWorkbook(string targetWorkbookPath, ExcelDataWorkbookDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (document.Sheets.Count <= 0)
            throw new InvalidOperationException("Cannot write a workbook document without worksheets.");

        string resolvedPath =
            ExcelDataWorkbookPathUtility.ResolveWorkbookPath(targetWorkbookPath, ExcelDataWorkbookPathUtility.LogExportRelativePath);
        string directoryPath = Path.GetDirectoryName(resolvedPath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
            Directory.CreateDirectory(directoryPath);

        Dictionary<string, object> sheetTables = new Dictionary<string, object>(StringComparer.Ordinal);
        DynamicExcelSheet[] dynamicSheets = new DynamicExcelSheet[document.Sheets.Count];
        HashSet<string> sanitizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string firstSheetName = string.Empty;

        // Convert each fixed matrix into an ordered DataTable supported natively by MiniExcel.
        for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDocument sheet = document.Sheets[sheetIndex];
            string sanitizedName = ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));

            if (!sanitizedNames.Add(sanitizedName))
                throw new InvalidOperationException("Worksheet names collide after Excel sanitization: " + sanitizedName);

            DataTable table = BuildDataTable(sheet, sanitizedName);
            DynamicExcelSheet dynamicSheet = new DynamicExcelSheet(sanitizedName);
            dynamicSheet.Name = sanitizedName;
            dynamicSheet.State = ResolveSheetState(sheet.Visibility);
            sheetTables.Add(sanitizedName, table);
            dynamicSheets[sheetIndex] = dynamicSheet;

            if (sheetIndex == 0)
                firstSheetName = sanitizedName;
        }

        OpenXmlConfiguration configuration = new OpenXmlConfiguration();
        configuration.AutoFilter = false;
        configuration.EnableAutoWidth = false;
        configuration.EnableWriteNullValueCell = true;
        configuration.IgnoreEmptyRows = false;
        configuration.WriteEmptyStringAsNull = false;
        configuration.DynamicSheets = dynamicSheets;

        MiniExcel.SaveAs(resolvedPath,
                         sheetTables,
                         printHeader: false,
                         sheetName: firstSheetName,
                         excelType: ExcelType.XLSX,
                         configuration: configuration,
                         overwriteFile: true);

        ExcelDataWorkbookFormulaWriter.Apply(resolvedPath, document);
        ExcelDataWorkbookColumnWidthUtility.Apply(resolvedPath, document);
        ExcelDataWorkbookCellStyleUtility.Apply(resolvedPath, document);

        ExcelDataWorkbookPathUtility.RefreshAssetDatabaseIfNeeded(resolvedPath);
        return resolvedPath;
    }
    #endregion

    #region Conversion
    /// <summary>
    /// Converts one exact workbook matrix into an ordered DataTable without adding a header row.
    /// </summary>
    /// <param name="sheet">Worksheet document to convert.</param>
    /// <param name="sanitizedSheetName">Excel-compatible worksheet name.</param>
    /// <returns>DataTable preserving typed values and explicit null cells.</returns>
    private static DataTable BuildDataTable(ExcelDataWorkbookSheetDocument sheet, string sanitizedSheetName)
    {
        DataTable table = new DataTable(sanitizedSheetName);

        // Create deterministic ordered columns because object arrays are treated as POCOs by MiniExcel.
        for (int columnIndex = 1; columnIndex <= sheet.ColumnCount; columnIndex++)
            table.Columns.Add("Column" + columnIndex.ToString(CultureInfo.InvariantCulture), typeof(object));

        // Copy each exact matrix row while representing empty cells as database null values.
        for (int rowIndex = 1; rowIndex <= sheet.RowCount; rowIndex++)
        {
            DataRow row = table.NewRow();

            for (int columnIndex = 1; columnIndex <= sheet.ColumnCount; columnIndex++)
                row[columnIndex - 1] = sheet.GetValue(rowIndex, columnIndex) ?? DBNull.Value;

            table.Rows.Add(row);
        }

        return table;
    }

    /// <summary>
    /// Maps tool worksheet visibility to MiniExcel Open XML sheet state.
    /// </summary>
    /// <param name="visibility">Tool-authored worksheet visibility.</param>
    /// <returns>MiniExcel sheet state written to the workbook package.</returns>
    private static SheetState ResolveSheetState(ExcelDataWorkbookSheetVisibility visibility)
    {
        switch (visibility)
        {
            case ExcelDataWorkbookSheetVisibility.Hidden:
                return SheetState.Hidden;
            case ExcelDataWorkbookSheetVisibility.VeryHidden:
                return SheetState.VeryHidden;
            default:
                return SheetState.Visible;
        }
    }
    #endregion

    #endregion
}
