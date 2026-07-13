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

    public string AuthoringStatus
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
    /// <param name="authoringStatus">Explicit authoring-save and Player bake dependency refresh status.</param>
    public ExcelDataImportApplyResult(string workbookPath,
                                      int appliedRowCount,
                                      int skippedRowCount,
                                      int warningCount,
                                      string authoringStatus)
    {
        WorkbookPath = workbookPath;
        AppliedRowCount = appliedRowCount;
        SkippedRowCount = skippedRowCount;
        WarningCount = warningCount;
        AuthoringStatus = authoringStatus ?? string.Empty;
    }
    #endregion

    #endregion
}
