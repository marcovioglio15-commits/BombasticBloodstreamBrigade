/// <summary>
/// Summarizes one workbook import apply operation for editor UI feedback.
/// </summary>
public sealed class ExcelDataImportApplyResult
{
    #region Properties
    public string WorkbookPath
    {
        get;
    }

    public int AppliedRowCount
    {
        get;
    }

    public int SkippedRowCount
    {
        get;
    }

    public int WarningCount
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an immutable result for an import apply operation.
    /// </summary>
    /// <param name="workbookPath">Absolute workbook path that was read.</param>
    /// <param name="appliedRowCount">Rows successfully applied to Unity assets.</param>
    /// <param name="skippedRowCount">Rows skipped by filters, policies or unsupported value types.</param>
    /// <param name="warningCount">Rows that reported a warning while applying.</param>
    public ExcelDataImportApplyResult(string workbookPath,
                                      int appliedRowCount,
                                      int skippedRowCount,
                                      int warningCount)
    {
        WorkbookPath = workbookPath;
        AppliedRowCount = appliedRowCount;
        SkippedRowCount = skippedRowCount;
        WarningCount = warningCount;
    }
    #endregion

    #endregion
}
