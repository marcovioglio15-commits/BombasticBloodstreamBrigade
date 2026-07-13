/// <summary>
/// Defines the editor-only persistence boundary for a cell-oriented workbook document.
/// </summary>
internal interface IExcelDataWorkbookAdapter
{
    #region Methods

    /// <summary>
    /// Writes one complete cell-oriented workbook document to an `.xlsx` file.
    /// </summary>
    /// <param name="targetWorkbookPath">Absolute or project-relative target workbook path.</param>
    /// <param name="document">Workbook document containing exact worksheet matrices.</param>
    /// <returns>Absolute path written by the adapter.</returns>
    string SaveWorkbook(string targetWorkbookPath, ExcelDataWorkbookDocument document);
    #endregion
}
