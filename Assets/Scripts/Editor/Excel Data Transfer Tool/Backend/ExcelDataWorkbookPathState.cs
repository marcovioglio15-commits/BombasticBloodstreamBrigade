/// <summary>
/// Describes one resolved workbook path and the non-destructive validation performed for its intended operation.
/// </summary>
internal sealed class ExcelDataWorkbookPathState
{
    #region Fields

    #region Readonly Fields
    public readonly ExcelDataWorkbookPathProfile Profile;
    public readonly ExcelDataWorkbookPathAccess Access;
    public readonly string AuthoredPath;
    public readonly string ProjectRelativePath;
    public readonly string AbsolutePath;
    public readonly string ValidationMessage;
    public readonly bool IsCustom;
    public readonly bool IsInsideProject;
    public readonly bool IsInsideAssets;
    public readonly bool Exists;
    public readonly bool ParentDirectoryExists;
    public readonly bool HasValidExtension;
    public readonly bool IsAccessible;
    public readonly bool IsValid;
    public readonly ExcelDataWorkbookPathValidationSeverity Severity;
    #endregion

    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates a read-only path snapshot used by UI diagnostics and import/export guardrails.
    /// </summary>
    /// <param name="profile">Serialized workbook profile that produced the path.</param>
    /// <param name="access">Operation that intends to read or write the workbook.</param>
    /// <param name="authoredPath">Raw custom or profile path before absolute resolution.</param>
    /// <param name="projectRelativePath">Readable project-relative path, or an external-path marker.</param>
    /// <param name="absolutePath">Absolute filesystem path resolved without changing its extension.</param>
    /// <param name="validationMessage">Diagnostic message shown by the editor tool.</param>
    /// <param name="isCustom">True when the Custom Path profile is selected.</param>
    /// <param name="isInsideProject">True when the workbook is under the current Unity project.</param>
    /// <param name="isInsideAssets">True when the workbook is under the project Assets folder.</param>
    /// <param name="exists">True when the workbook file currently exists.</param>
    /// <param name="parentDirectoryExists">True when the immediate output directory exists.</param>
    /// <param name="hasValidExtension">True when the authored target ends exactly in .xlsx.</param>
    /// <param name="isAccessible">True when the file can be read for import or written for export.</param>
    /// <param name="isValid">True when the operation may safely use the path.</param>
    /// <param name="severity">Highest diagnostic severity produced by validation.</param>
    public ExcelDataWorkbookPathState(ExcelDataWorkbookPathProfile profile,
                                      ExcelDataWorkbookPathAccess access,
                                      string authoredPath,
                                      string projectRelativePath,
                                      string absolutePath,
                                      string validationMessage,
                                      bool isCustom,
                                      bool isInsideProject,
                                      bool isInsideAssets,
                                      bool exists,
                                      bool parentDirectoryExists,
                                      bool hasValidExtension,
                                      bool isAccessible,
                                      bool isValid,
                                      ExcelDataWorkbookPathValidationSeverity severity)
    {
        Profile = profile;
        Access = access;
        AuthoredPath = authoredPath;
        ProjectRelativePath = projectRelativePath;
        AbsolutePath = absolutePath;
        ValidationMessage = validationMessage;
        IsCustom = isCustom;
        IsInsideProject = isInsideProject;
        IsInsideAssets = isInsideAssets;
        Exists = exists;
        ParentDirectoryExists = parentDirectoryExists;
        HasValidExtension = hasValidExtension;
        IsAccessible = isAccessible;
        IsValid = isValid;
        Severity = severity;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies whether a workbook path is validated for reading or writing.
/// </summary>
internal enum ExcelDataWorkbookPathAccess
{
    Import = 0,
    Export = 1
}

/// <summary>
/// Classifies workbook path diagnostics without coupling backend validation to UI Toolkit types.
/// </summary>
internal enum ExcelDataWorkbookPathValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}
