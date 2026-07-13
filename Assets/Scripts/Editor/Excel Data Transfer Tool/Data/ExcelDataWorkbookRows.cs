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

    public int ColumnIndex
    {
        get;
    }

    public string SheetName
    {
        get;
    }

    public string Address
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

    public string CurrentValue
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

    public bool CanApply
    {
        get;
    }

    public string Warning
    {
        get;
    }

    internal ExcelDataWorkbookCellDefinition CellDefinition
    {
        get;
    }

    internal ExcelDataImportCellValue IncomingValue
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable import preview row for UI lists and smoke assertions.
    /// </summary>
    /// <param name="sheetName">Visible worksheet name containing the incoming value.</param>
    /// <param name="cellDefinition">Grid-authoritative cell definition read at its exact coordinate.</param>
    /// <param name="incomingValue">Raw incoming value and hidden reference metadata.</param>
    /// <param name="assetName">Resolved target asset name for readable preview output.</param>
    /// <param name="currentValue">Current Unity serialized value before import.</param>
    /// <param name="bindingResolved">True when the target asset and property were resolved.</param>
    /// <param name="includedByPreset">True when domain guardrails allow this cell.</param>
    /// <param name="canApply">True when this cell passed preflight and can mutate its target after approval.</param>
    /// <param name="warning">Warning text explaining skipped, duplicated or risky cells.</param>
    internal ExcelDataImportPreviewRow(string sheetName,
                                       ExcelDataWorkbookCellDefinition cellDefinition,
                                       ExcelDataImportCellValue incomingValue,
                                       string assetName,
                                       string currentValue,
                                       bool bindingResolved,
                                       bool includedByPreset,
                                       bool canApply,
                                       string warning)
    {
        ExcelDataFieldBinding binding = cellDefinition == null ? null : cellDefinition.FieldBinding;
        RowIndex = cellDefinition == null ? 0 : cellDefinition.RowIndex;
        ColumnIndex = cellDefinition == null ? 0 : cellDefinition.ColumnIndex;
        SheetName = sheetName ?? string.Empty;
        Address = RowIndex > 0 && ColumnIndex > 0
            ? ExcelDataWorkbookCoordinateUtility.BuildAddress(RowIndex, ColumnIndex)
            : string.Empty;
        Section = SheetName;
        FieldId = binding == null ? string.Empty : binding.FieldId;
        AssetName = assetName ?? string.Empty;
        SerializedPath = binding == null ? string.Empty : binding.SerializedPath;
        Value = incomingValue == null ? string.Empty : incomingValue.ValueText;
        CurrentValue = currentValue ?? string.Empty;
        CatalogMatched = bindingResolved;
        IncludedByPreset = includedByPreset;
        CanApply = canApply;
        Warning = warning ?? string.Empty;
        CellDefinition = cellDefinition;
        IncomingValue = incomingValue;
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

    public bool CanApply
    {
        get;
    }

    public bool LayoutHashMatches
    {
        get;
    }

    public string WorkbookLayoutHash
    {
        get;
    }

    public string CurrentLayoutHash
    {
        get;
    }

    public string ValidationMessage
    {
        get;
    }

    public long WorkbookLastWriteUtcTicks
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
    /// <param name="rows">Detailed coordinate-exact preview cells.</param>
    /// <param name="canApply">True when workbook-level validation allows apply.</param>
    /// <param name="layoutHashMatches">True when the workbook and active layout hashes match.</param>
    /// <param name="workbookLayoutHash">Layout hash stored in the workbook technical sheet.</param>
    /// <param name="currentLayoutHash">Current active layout hash.</param>
    /// <param name="validationMessage">Workbook-level validation summary.</param>
    /// <param name="workbookLastWriteUtcTicks">File timestamp used to reject stale previews.</param>
    public ExcelDataImportPreviewResult(string workbookPath,
                                        int totalRowCount,
                                        int importableRowCount,
                                        int skippedRowCount,
                                        int warningCount,
                                        System.Collections.Generic.List<ExcelDataImportPreviewRow> rows,
                                        bool canApply,
                                        bool layoutHashMatches,
                                        string workbookLayoutHash,
                                        string currentLayoutHash,
                                        string validationMessage,
                                        long workbookLastWriteUtcTicks)
    {
        WorkbookPath = workbookPath;
        TotalRowCount = totalRowCount;
        ImportableRowCount = importableRowCount;
        SkippedRowCount = skippedRowCount;
        WarningCount = warningCount;
        Rows = rows == null ? new System.Collections.Generic.List<ExcelDataImportPreviewRow>() : rows;
        CanApply = canApply;
        LayoutHashMatches = layoutHashMatches;
        WorkbookLayoutHash = workbookLayoutHash ?? string.Empty;
        CurrentLayoutHash = currentLayoutHash ?? string.Empty;
        ValidationMessage = validationMessage ?? string.Empty;
        WorkbookLastWriteUtcTicks = workbookLastWriteUtcTicks;
    }
    #endregion

    #endregion
}
