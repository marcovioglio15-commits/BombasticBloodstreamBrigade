using System.Collections.Generic;

/// <summary>
/// Stores one grid-authoritative workbook document and the exact authored records used to build it.
/// </summary>
internal sealed class ExcelDataWorkbookExportBuildResult
{
    #region Fields
    private readonly List<ExcelDataWorkbookExportSheetRecord> sheets = new List<ExcelDataWorkbookExportSheetRecord>();
    private readonly List<ExcelDataWorkbookExportCellRecord> cells = new List<ExcelDataWorkbookExportCellRecord>();
    #endregion

    #region Properties
    public ExcelDataWorkbookDocument Document
    {
        get;
    }

    public IReadOnlyList<ExcelDataWorkbookExportSheetRecord> Sheets
    {
        get
        {
            return sheets;
        }
    }

    public IReadOnlyList<ExcelDataWorkbookExportCellRecord> Cells
    {
        get
        {
            return cells;
        }
    }

    public int DataFieldCellCount
    {
        get;
        private set;
    }

    public int LiteralCellCount
    {
        get;
        private set;
    }

    public int FormulaCellCount
    {
        get;
        private set;
    }

    public int ReferenceCellCount
    {
        get;
        private set;
    }

    public int WrittenCellCount
    {
        get;
        private set;
    }

    public int WarningCount
    {
        get;
        private set;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one empty export build result around a new workbook document.
    /// </summary>
    public ExcelDataWorkbookExportBuildResult()
    {
        Document = new ExcelDataWorkbookDocument();
    }
    #endregion

    #region Registration
    /// <summary>
    /// Registers one user worksheet that was materialized into the workbook document.
    /// </summary>
    /// <param name="sheetDefinition">Authored sheet definition represented by the document sheet.</param>
    public void RegisterSheet(ExcelDataWorkbookSheetDefinition sheetDefinition)
    {
        sheets.Add(new ExcelDataWorkbookExportSheetRecord(sheetDefinition));
    }

    /// <summary>
    /// Registers one exact exported cell and updates operation counters without rereading its value.
    /// </summary>
    /// <param name="sheetDefinition">Authored owner worksheet.</param>
    /// <param name="cellDefinition">Authored cell definition.</param>
    /// <param name="snapshot">Resolved typed value and technical metadata.</param>
    public void RegisterCell(ExcelDataWorkbookSheetDefinition sheetDefinition,
                             ExcelDataWorkbookCellDefinition cellDefinition,
                             ExcelDataSerializedValueSnapshot snapshot)
    {
        cells.Add(new ExcelDataWorkbookExportCellRecord(sheetDefinition, cellDefinition, snapshot));

        switch (cellDefinition.ContentKind)
        {
            case ExcelDataWorkbookCellContentKind.DataField:
                DataFieldCellCount++;

                if (cellDefinition.FieldBinding != null &&
                    cellDefinition.FieldBinding.ExpectedDataKind == ExcelDataBrushDataKind.ObjectReference)
                    ReferenceCellCount++;

                break;
            case ExcelDataWorkbookCellContentKind.LiteralText:
                LiteralCellCount++;
                break;
            case ExcelDataWorkbookCellContentKind.Formula:
                FormulaCellCount++;
                break;
        }

        if (snapshot != null && snapshot.Value != null)
            WrittenCellCount++;

        if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.Warning))
            WarningCount++;
    }
    #endregion

    #endregion
}

/// <summary>
/// Associates one materialized user worksheet with its authored definition.
/// </summary>
internal sealed class ExcelDataWorkbookExportSheetRecord
{
    #region Properties
    public ExcelDataWorkbookSheetDefinition Definition
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one exported worksheet record.
    /// </summary>
    /// <param name="definition">Authored worksheet definition.</param>
    public ExcelDataWorkbookExportSheetRecord(ExcelDataWorkbookSheetDefinition definition)
    {
        Definition = definition;
    }
    #endregion

    #endregion
}

/// <summary>
/// Associates one exact workbook coordinate with its authored definition and resolved value snapshot.
/// </summary>
internal sealed class ExcelDataWorkbookExportCellRecord
{
    #region Properties
    public ExcelDataWorkbookSheetDefinition SheetDefinition
    {
        get;
    }

    public ExcelDataWorkbookCellDefinition CellDefinition
    {
        get;
    }

    public ExcelDataSerializedValueSnapshot Snapshot
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable exported cell record used by diagnostics and technical metadata.
    /// </summary>
    /// <param name="sheetDefinition">Authored owner worksheet.</param>
    /// <param name="cellDefinition">Authored exact cell definition.</param>
    /// <param name="snapshot">Resolved value and asset metadata.</param>
    public ExcelDataWorkbookExportCellRecord(ExcelDataWorkbookSheetDefinition sheetDefinition,
                                             ExcelDataWorkbookCellDefinition cellDefinition,
                                             ExcelDataSerializedValueSnapshot snapshot)
    {
        SheetDefinition = sheetDefinition;
        CellDefinition = cellDefinition;
        Snapshot = snapshot;
    }
    #endregion

    #endregion
}
