using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Summarizes one grid-authoritative workbook export for editor UI feedback and smoke assertions.
/// </summary>
public sealed class ExcelDataExportResult
{
    #region Properties
    public string WorkbookPath
    {
        get;
    }

    public int UserSheetCount
    {
        get;
    }

    public int AuthoredCellCount
    {
        get;
    }

    public int WrittenCellCount
    {
        get;
    }

    public int DataFieldCellCount
    {
        get;
    }

    public int LiteralCellCount
    {
        get;
    }

    public int FormulaCellCount
    {
        get;
    }

    public int ReferenceCellCount
    {
        get;
    }

    public int WarningCount
    {
        get;
    }

    public int TechnicalRowCount
    {
        get;
    }

    public string LayoutHash
    {
        get;
    }

    public IReadOnlyList<ExcelDataExportDiagnostic> Diagnostics
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable export result after the exact workbook has been persisted.
    /// </summary>
    /// <param name="workbookPath">Absolute workbook path written by the adapter.</param>
    /// <param name="userSheetCount">Number of user-authored worksheets written before the technical sheet.</param>
    /// <param name="authoredCellCount">Number of export-enabled authored cells preserved in the workbook layout.</param>
    /// <param name="writtenCellCount">Number of authored cells that produced non-null workbook values.</param>
    /// <param name="dataFieldCellCount">Number of authored Data Field cells.</param>
    /// <param name="literalCellCount">Number of authored Literal Text cells.</param>
    /// <param name="formulaCellCount">Number of authored native Excel Formula cells.</param>
    /// <param name="referenceCellCount">Number of Data Field cells targeting object references.</param>
    /// <param name="warningCount">Number of cell-local export warnings.</param>
    /// <param name="technicalRowCount">Technical worksheet rows including its header.</param>
    /// <param name="layoutHash">Deterministic hash written to technical metadata.</param>
    /// <param name="diagnostics">Detailed warning records keyed by user sheet and exact coordinate.</param>
    public ExcelDataExportResult(string workbookPath,
                                 int userSheetCount,
                                 int authoredCellCount,
                                 int writtenCellCount,
                                 int dataFieldCellCount,
                                 int literalCellCount,
                                 int formulaCellCount,
                                 int referenceCellCount,
                                 int warningCount,
                                 int technicalRowCount,
                                 string layoutHash,
                                 List<ExcelDataExportDiagnostic> diagnostics)
    {
        WorkbookPath = workbookPath;
        UserSheetCount = userSheetCount;
        AuthoredCellCount = authoredCellCount;
        WrittenCellCount = writtenCellCount;
        DataFieldCellCount = dataFieldCellCount;
        LiteralCellCount = literalCellCount;
        FormulaCellCount = formulaCellCount;
        ReferenceCellCount = referenceCellCount;
        WarningCount = warningCount;
        TechnicalRowCount = technicalRowCount;
        LayoutHash = layoutHash;
        Diagnostics = diagnostics == null ? new List<ExcelDataExportDiagnostic>() : diagnostics.AsReadOnly();
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Builds one consistent concise operation message for every tool surface that exposes export.
    /// </summary>
    /// <returns>Readable summary containing cells, sheets, warnings and final workbook path.</returns>
    public string BuildSummary()
    {
        return "Exported " + WrittenCellCount.ToString(CultureInfo.InvariantCulture) +
               "/" + AuthoredCellCount.ToString(CultureInfo.InvariantCulture) +
               " cells across " + UserSheetCount.ToString(CultureInfo.InvariantCulture) +
               " user sheets. Data: " + DataFieldCellCount.ToString(CultureInfo.InvariantCulture) +
               ", literals: " + LiteralCellCount.ToString(CultureInfo.InvariantCulture) +
               ", formulas: " + FormulaCellCount.ToString(CultureInfo.InvariantCulture) +
               ", references: " + ReferenceCellCount.ToString(CultureInfo.InvariantCulture) +
               ", warnings: " + WarningCount.ToString(CultureInfo.InvariantCulture) +
               ". Path: " + WorkbookPath;
    }
    #endregion

    #endregion
}

/// <summary>
/// Describes one non-fatal export warning at an exact user workbook coordinate.
/// </summary>
public sealed class ExcelDataExportDiagnostic
{
    #region Properties
    public string SheetName
    {
        get;
    }

    public int RowIndex
    {
        get;
    }

    public int ColumnIndex
    {
        get;
    }

    public string FieldId
    {
        get;
    }

    public string Message
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable cell-local export warning.
    /// </summary>
    /// <param name="sheetName">Authored user worksheet name.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <param name="fieldId">Stable field identifier, or an empty string for non-field cells.</param>
    /// <param name="message">Actionable warning text.</param>
    public ExcelDataExportDiagnostic(string sheetName,
                                     int rowIndex,
                                     int columnIndex,
                                     string fieldId,
                                     string message)
    {
        SheetName = sheetName;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        FieldId = fieldId;
        Message = message;
    }
    #endregion

    #endregion
}
