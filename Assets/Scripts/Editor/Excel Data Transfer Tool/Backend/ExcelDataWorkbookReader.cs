using System.Collections.Generic;
using System.IO;
using MiniExcelLibs;

/// <summary>
/// Editor-only MiniExcel adapter that reads normalized workbook rows from an .xlsx file.
/// </summary>
internal static class ExcelDataWorkbookReader
{
    #region Constants
    private const string DefaultImportRelativePath = ExcelDataWorkbookPathUtility.LogExportRelativePath;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads normalized workbook rows from the requested worksheet.
    /// </summary>
    /// <param name="sourceWorkbookPath">Absolute or project-relative workbook path. Empty uses the default export path.</param>
    /// <param name="sheetName">Worksheet name expected by the active layout preset.</param>
    /// <returns>Workbook rows read from disk.</returns>
    public static List<ExcelDataWorkbookRow> LoadWorkbookRows(string sourceWorkbookPath, string sheetName)
    {
        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveWorkbookPath(sourceWorkbookPath, DefaultImportRelativePath);

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException("Import workbook was not found.", resolvedPath);

        string sanitizedSheetName = ExcelDataWorkbookPathUtility.SanitizeSheetName(sheetName, "Objects");
        IEnumerable<ExcelDataWorkbookRow> queriedRows =
            MiniExcel.Query<ExcelDataWorkbookRow>(resolvedPath, sanitizedSheetName, ExcelType.XLSX, "A1", null, true);
        List<ExcelDataWorkbookRow> rows = new List<ExcelDataWorkbookRow>();

        foreach (ExcelDataWorkbookRow row in queriedRows)
        {
            if (row == null)
                continue;

            rows.Add(row);
        }

        return rows;
    }
    #endregion

    #endregion
}
