/// <summary>
/// Stores one row written by the normalized editor-only workbook export.
/// </summary>
public sealed class ExcelDataWorkbookRow
{
    #region Properties
    public string Section
    {
        get;
        set;
    }

    public string Domain
    {
        get;
        set;
    }

    public string Category
    {
        get;
        set;
    }

    public string DataKind
    {
        get;
        set;
    }

    public string AssetType
    {
        get;
        set;
    }

    public string AssetName
    {
        get;
        set;
    }

    public string AssetPath
    {
        get;
        set;
    }

    public string SerializedPath
    {
        get;
        set;
    }

    public string PathTemplate
    {
        get;
        set;
    }

    public string FieldId
    {
        get;
        set;
    }

    public string Value
    {
        get;
        set;
    }

    public string ReferenceName
    {
        get;
        set;
    }

    public string ReferenceGuid
    {
        get;
        set;
    }

    public string ReferencePath
    {
        get;
        set;
    }

    public string WorkbookSheet
    {
        get;
        set;
    }

    public int WorkbookRow
    {
        get;
        set;
    }

    public int WorkbookColumn
    {
        get;
        set;
    }

    public bool ConcreteListElement
    {
        get;
        set;
    }

    public int ListDepth
    {
        get;
        set;
    }

    public string Warning
    {
        get;
        set;
    }
    #endregion
}

/// <summary>
/// Stores one diagnostic row produced by an import preview without mutating Unity assets.
/// </summary>
public sealed class ExcelDataImportPreviewRow
{
    #region Properties
    public int RowIndex
    {
        get;
    }

    public string Section
    {
        get;
    }

    public string FieldId
    {
        get;
    }

    public string AssetName
    {
        get;
    }

    public string SerializedPath
    {
        get;
    }

    public string Value
    {
        get;
    }

    public bool CatalogMatched
    {
        get;
    }

    public bool IncludedByPreset
    {
        get;
    }

    public string Warning
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable import preview row for UI lists and smoke assertions.
    /// </summary>
    /// <param name="rowIndex">One-based workbook row index including the header row offset.</param>
    /// <param name="sourceRow">Workbook row read from MiniExcel.</param>
    /// <param name="catalogMatched">True when the row field id exists in the current catalog.</param>
    /// <param name="includedByPreset">True when import preset filters allow this row.</param>
    /// <param name="warning">Warning text explaining skipped or risky rows.</param>
    public ExcelDataImportPreviewRow(int rowIndex,
                                     ExcelDataWorkbookRow sourceRow,
                                     bool catalogMatched,
                                     bool includedByPreset,
                                     string warning)
    {
        RowIndex = rowIndex;
        Section = sourceRow == null ? string.Empty : sourceRow.Section;
        FieldId = sourceRow == null ? string.Empty : sourceRow.FieldId;
        AssetName = sourceRow == null ? string.Empty : sourceRow.AssetName;
        SerializedPath = sourceRow == null ? string.Empty : sourceRow.SerializedPath;
        Value = sourceRow == null ? string.Empty : sourceRow.Value;
        CatalogMatched = catalogMatched;
        IncludedByPreset = includedByPreset;
        Warning = warning;
    }
    #endregion

    #endregion
}

/// <summary>
/// Summarizes one import preview operation for editor UI feedback and smoke tests.
/// </summary>
public sealed class ExcelDataImportPreviewResult
{
    #region Properties
    public string WorkbookPath
    {
        get;
    }

    public int TotalRowCount
    {
        get;
    }

    public int ImportableRowCount
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

    public System.Collections.Generic.List<ExcelDataImportPreviewRow> Rows
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an immutable import preview result after the workbook has been inspected.
    /// </summary>
    /// <param name="workbookPath">Absolute workbook path read by the preview.</param>
    /// <param name="totalRowCount">Total normalized workbook rows inspected.</param>
    /// <param name="importableRowCount">Rows that match the current catalog and import preset filters.</param>
    /// <param name="skippedRowCount">Rows skipped because they are metadata, disabled, or unmatched.</param>
    /// <param name="warningCount">Rows carrying warnings.</param>
    /// <param name="rows">Detailed preview rows.</param>
    public ExcelDataImportPreviewResult(string workbookPath,
                                        int totalRowCount,
                                        int importableRowCount,
                                        int skippedRowCount,
                                        int warningCount,
                                        System.Collections.Generic.List<ExcelDataImportPreviewRow> rows)
    {
        WorkbookPath = workbookPath;
        TotalRowCount = totalRowCount;
        ImportableRowCount = importableRowCount;
        SkippedRowCount = skippedRowCount;
        WarningCount = warningCount;
        Rows = rows == null ? new System.Collections.Generic.List<ExcelDataImportPreviewRow>() : rows;
    }
    #endregion

    #endregion
}
