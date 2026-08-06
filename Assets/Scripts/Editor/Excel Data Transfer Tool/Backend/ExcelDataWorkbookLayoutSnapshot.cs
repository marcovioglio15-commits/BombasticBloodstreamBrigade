using System.Collections.Generic;

/// <summary>
/// Stores the complete grid-authoritative layout snapshot parsed from the reserved workbook worksheet.
/// </summary>
internal sealed class ExcelDataWorkbookLayoutSnapshot
{
    #region Fields
    private readonly List<ExcelDataWorkbookLayoutSheetSnapshot> sheets = new List<ExcelDataWorkbookLayoutSheetSnapshot>();
    private readonly List<ExcelDataWorkbookLayoutCellSnapshot> cells = new List<ExcelDataWorkbookLayoutCellSnapshot>();
    #endregion

    #region Properties
    public bool TechnicalSheetFound
    {
        get;
        private set;
    }

    public bool WorkbookRecordFound
    {
        get;
        private set;
    }

    public string SchemaVersion
    {
        get;
        private set;
    } = string.Empty;

    public string LayoutPresetId
    {
        get;
        private set;
    } = string.Empty;

    public string LayoutHash
    {
        get;
        private set;
    } = string.Empty;

    public IReadOnlyList<ExcelDataWorkbookLayoutSheetSnapshot> Sheets
    {
        get
        {
            return sheets;
        }
    }

    public IReadOnlyList<ExcelDataWorkbookLayoutCellSnapshot> Cells
    {
        get
        {
            return cells;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Marks that the reserved technical worksheet exists in the source workbook.
    /// </summary>
    public void MarkTechnicalSheetFound()
    {
        TechnicalSheetFound = true;
    }

    /// <summary>
    /// Stores workbook-level schema and layout identity exactly once.
    /// </summary>
    /// <param name="schemaVersion">Technical schema version.</param>
    /// <param name="layoutPresetId">Layout preset identifier captured during export.</param>
    /// <param name="layoutHash">Deterministic exported layout hash.</param>
    public void ConfigureWorkbookRecord(string schemaVersion, string layoutPresetId, string layoutHash)
    {
        if (WorkbookRecordFound)
            return;

        WorkbookRecordFound = true;
        SchemaVersion = schemaVersion ?? string.Empty;
        LayoutPresetId = layoutPresetId ?? string.Empty;
        LayoutHash = layoutHash ?? string.Empty;
    }

    /// <summary>
    /// Appends one authored worksheet record in workbook tab order.
    /// </summary>
    /// <param name="sheet">Parsed worksheet snapshot.</param>
    public void AddSheet(ExcelDataWorkbookLayoutSheetSnapshot sheet)
    {
        if (sheet != null)
            sheets.Add(sheet);
    }

    /// <summary>
    /// Appends one exact authored cell record.
    /// </summary>
    /// <param name="cell">Parsed cell snapshot.</param>
    public void AddCell(ExcelDataWorkbookLayoutCellSnapshot cell)
    {
        if (cell != null)
            cells.Add(cell);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one complete worksheet definition read from technical metadata.
/// </summary>
internal sealed class ExcelDataWorkbookLayoutSheetSnapshot
{
    #region Properties
    public string SheetId { get; }
    public string SheetName { get; }
    public string WorkbookSheetName { get; }
    public int PreviewRowCount { get; }
    public int PreviewColumnCount { get; }
    public int PreviewCellWidth { get; }
    public int PreviewCellHeight { get; }
    public int FreezeRowCount { get; }
    public int FreezeColumnCount { get; }
    public ExcelDataWorkbookSheetVisibility Visibility { get; }
    public bool ImportEnabled { get; }
    public bool ExportEnabled { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an immutable worksheet snapshot from one technical Sheet record.
    /// </summary>
    /// <param name="sheetId">Stable authored worksheet identifier.</param>
    /// <param name="sheetName">Authored visible worksheet name.</param>
    /// <param name="workbookSheetName">Sanitized workbook worksheet name.</param>
    /// <param name="previewRowCount">Authored preview row count.</param>
    /// <param name="previewColumnCount">Authored preview column count.</param>
    /// <param name="previewCellWidth">Authored preview cell width.</param>
    /// <param name="previewCellHeight">Authored preview cell height.</param>
    /// <param name="freezeRowCount">Leading frozen row count.</param>
    /// <param name="freezeColumnCount">Leading frozen column count.</param>
    /// <param name="visibility">Authored workbook visibility.</param>
    /// <param name="importEnabled">True when the sheet participates in import.</param>
    /// <param name="exportEnabled">True when the sheet participates in export.</param>
    public ExcelDataWorkbookLayoutSheetSnapshot(string sheetId,
                                                string sheetName,
                                                string workbookSheetName,
                                                int previewRowCount,
                                                int previewColumnCount,
                                                int previewCellWidth,
                                                int previewCellHeight,
                                                int freezeRowCount,
                                                int freezeColumnCount,
                                                ExcelDataWorkbookSheetVisibility visibility,
                                                bool importEnabled,
                                                bool exportEnabled)
    {
        SheetId = sheetId;
        SheetName = sheetName;
        WorkbookSheetName = workbookSheetName;
        PreviewRowCount = previewRowCount;
        PreviewColumnCount = previewColumnCount;
        PreviewCellWidth = previewCellWidth;
        PreviewCellHeight = previewCellHeight;
        FreezeRowCount = freezeRowCount;
        FreezeColumnCount = freezeColumnCount;
        Visibility = visibility;
        ImportEnabled = importEnabled;
        ExportEnabled = exportEnabled;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one exact Data Field, Literal Text or Formula cell read from technical metadata.
/// </summary>
internal sealed class ExcelDataWorkbookLayoutCellSnapshot
{
    #region Properties
    public string SheetId { get; }
    public int RowIndex { get; }
    public int ColumnIndex { get; }
    public ExcelDataWorkbookCellContentKind ContentKind { get; }
    public ExcelDataTransferDirection Direction { get; }
    public string FieldId { get; }
    public ExcelDataTransferDomain Domain { get; }
    public string OwnerAssetGuid { get; }
    public string OwnerAssetTypeName { get; }
    public string OwnerAssetPath { get; }
    public string SerializedPath { get; }
    public string PathTemplate { get; }
    public ExcelDataBrushDataKind DataKind { get; }
    public string BrushId { get; }
    public string NumberFormat { get; }
    public bool ValidateLiteralDuringImport { get; }
    public string LiteralText { get; }
    public string FormulaExpression { get; }
    public IReadOnlyList<int> ConcreteListIndices { get; }
    public IReadOnlyList<string> StableListKeys { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an immutable cell snapshot containing complete binding and presentation identity.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet identifier.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="contentKind">Data Field, Literal Text or Formula payload kind.</param>
    /// <param name="direction">Allowed transfer directions.</param>
    /// <param name="fieldId">Stable catalog field identifier.</param>
    /// <param name="domain">Management domain owning the field.</param>
    /// <param name="ownerAssetGuid">Owner asset GUID.</param>
    /// <param name="ownerAssetTypeName">Expected owner asset type.</param>
    /// <param name="ownerAssetPath">Authored owner asset path.</param>
    /// <param name="serializedPath">Concrete SerializedProperty path.</param>
    /// <param name="pathTemplate">Reusable list-tokenized path.</param>
    /// <param name="dataKind">Expected workbook value family.</param>
    /// <param name="brushId">Stable saved brush identifier.</param>
    /// <param name="numberFormat">Optional Excel number format.</param>
    /// <param name="validateLiteralDuringImport">True when import validates literal text.</param>
    /// <param name="literalText">Exact authored literal text.</param>
    /// <param name="formulaExpression">Exact authored Excel formula expression.</param>
    /// <param name="concreteListIndices">Concrete zero-based list indexes.</param>
    /// <param name="stableListKeys">Stable list element keys.</param>
    public ExcelDataWorkbookLayoutCellSnapshot(string sheetId,
                                               int rowIndex,
                                               int columnIndex,
                                               ExcelDataWorkbookCellContentKind contentKind,
                                               ExcelDataTransferDirection direction,
                                               string fieldId,
                                               ExcelDataTransferDomain domain,
                                               string ownerAssetGuid,
                                               string ownerAssetTypeName,
                                               string ownerAssetPath,
                                               string serializedPath,
                                               string pathTemplate,
                                               ExcelDataBrushDataKind dataKind,
                                               string brushId,
                                               string numberFormat,
                                               bool validateLiteralDuringImport,
                                               string literalText,
                                               string formulaExpression,
                                               IReadOnlyList<int> concreteListIndices,
                                               IReadOnlyList<string> stableListKeys)
    {
        SheetId = sheetId;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        ContentKind = contentKind;
        Direction = direction;
        FieldId = fieldId;
        Domain = domain;
        OwnerAssetGuid = ownerAssetGuid;
        OwnerAssetTypeName = ownerAssetTypeName;
        OwnerAssetPath = ownerAssetPath;
        SerializedPath = serializedPath;
        PathTemplate = pathTemplate;
        DataKind = dataKind;
        BrushId = brushId;
        NumberFormat = numberFormat;
        ValidateLiteralDuringImport = validateLiteralDuringImport;
        LiteralText = literalText;
        FormulaExpression = formulaExpression;
        ConcreteListIndices = concreteListIndices ?? new List<int>();
        StableListKeys = stableListKeys ?? new List<string>();
    }
    #endregion

    #endregion
}
