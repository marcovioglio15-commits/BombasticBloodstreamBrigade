using System;
using UnityEngine;

/// <summary>
/// Stores one non-empty grid-authoritative workbook cell and its import/export behavior.
/// </summary>
[Serializable]
public sealed class ExcelDataWorkbookCellDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier of the worksheet that owns this cell.")]
    [SerializeField] private string sheetId;

    [Tooltip("One-based Excel row index where this cell is written or read.")]
    [Min(1)]
    [SerializeField] private int rowIndex = 1;

    [Tooltip("One-based Excel column index where this cell is written or read.")]
    [Min(1)]
    [SerializeField] private int columnIndex = 1;

    [Tooltip("Payload family stored by this cell: a Unity data field or literal workbook text.")]
    [SerializeField] private ExcelDataWorkbookCellContentKind contentKind;

    [Tooltip("Transfer directions that are allowed to consume this cell.")]
    [SerializeField] private ExcelDataTransferDirection direction;

    [Tooltip("Stable Unity field binding used when Content Kind is Data Field.")]
    [SerializeField] private ExcelDataFieldBinding fieldBinding = new ExcelDataFieldBinding();

    [Tooltip("Exact workbook text used when Content Kind is Literal Text.")]
    [SerializeField] private string literalText;

    [Tooltip("Excel formula expression written as an executable workbook formula when Content Kind is Formula. The leading equals sign is optional.")]
    [SerializeField] private string formulaExpression;

    [Tooltip("Stable brush preset identifier used to preserve the authored grid color and behavior.")]
    [SerializeField] private string brushId;

    [Tooltip("Optional invariant Excel number format applied by formatting-capable adapters.")]
    [SerializeField] private string numberFormat;

    [Tooltip("Report a preview warning when imported literal text differs from the authored value.")]
    [SerializeField] private bool validateLiteralDuringImport;
    #endregion

    #endregion

    #region Properties
    public string SheetId
    {
        get
        {
            return sheetId;
        }
    }

    public int RowIndex
    {
        get
        {
            return rowIndex;
        }
    }

    public int ColumnIndex
    {
        get
        {
            return columnIndex;
        }
    }

    public ExcelDataWorkbookCellContentKind ContentKind
    {
        get
        {
            return contentKind;
        }
    }

    public ExcelDataTransferDirection Direction
    {
        get
        {
            return direction;
        }
    }

    public ExcelDataFieldBinding FieldBinding
    {
        get
        {
            return fieldBinding;
        }
    }

    public string LiteralText
    {
        get
        {
            return literalText;
        }
    }

    public string FormulaExpression
    {
        get
        {
            return formulaExpression;
        }
    }

    public string BrushId
    {
        get
        {
            return brushId;
        }
    }

    public string NumberFormat
    {
        get
        {
            return numberFormat;
        }
    }

    public bool ValidateLiteralDuringImport
    {
        get
        {
            return validateLiteralDuringImport;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures a cell that reads or writes one Unity serialized data field.
    /// </summary>
    /// <param name="newSheetId">Stable owner worksheet identifier.</param>
    /// <param name="newRowIndex">One-based Excel row index.</param>
    /// <param name="newColumnIndex">One-based Excel column index.</param>
    /// <param name="newFieldBinding">Stable Unity field binding.</param>
    /// <param name="newDirection">Allowed transfer directions.</param>
    /// <param name="newBrushId">Stable brush preset identifier.</param>
    /// <param name="newNumberFormat">Optional invariant Excel number format.</param>
    public void ConfigureDataField(string newSheetId,
                                   int newRowIndex,
                                   int newColumnIndex,
                                   ExcelDataFieldBinding newFieldBinding,
                                   ExcelDataTransferDirection newDirection,
                                   string newBrushId,
                                   string newNumberFormat)
    {
        sheetId = newSheetId;
        rowIndex = newRowIndex;
        columnIndex = newColumnIndex;
        contentKind = ExcelDataWorkbookCellContentKind.DataField;
        fieldBinding = newFieldBinding ?? new ExcelDataFieldBinding();
        literalText = string.Empty;
        formulaExpression = string.Empty;
        direction = newDirection;
        brushId = newBrushId;
        numberFormat = newNumberFormat;
        validateLiteralDuringImport = false;
    }

    /// <summary>
    /// Configures a cell that writes authored literal text without targeting Unity data.
    /// </summary>
    /// <param name="newSheetId">Stable owner worksheet identifier.</param>
    /// <param name="newRowIndex">One-based Excel row index.</param>
    /// <param name="newColumnIndex">One-based Excel column index.</param>
    /// <param name="newLiteralText">Exact workbook text.</param>
    /// <param name="newDirection">Allowed transfer directions.</param>
    /// <param name="newBrushId">Stable brush preset identifier.</param>
    /// <param name="newValidateLiteralDuringImport">True when import preview should validate this text.</param>
    public void ConfigureLiteralText(string newSheetId,
                                     int newRowIndex,
                                     int newColumnIndex,
                                     string newLiteralText,
                                     ExcelDataTransferDirection newDirection,
                                     string newBrushId,
                                     bool newValidateLiteralDuringImport)
    {
        sheetId = newSheetId;
        rowIndex = newRowIndex;
        columnIndex = newColumnIndex;
        contentKind = ExcelDataWorkbookCellContentKind.LiteralText;
        fieldBinding = new ExcelDataFieldBinding();
        literalText = newLiteralText;
        formulaExpression = string.Empty;
        direction = newDirection;
        brushId = newBrushId;
        numberFormat = string.Empty;
        validateLiteralDuringImport = newValidateLiteralDuringImport;
    }

    /// <summary>
    /// Configures an export-only cell whose expression is emitted as a native Excel formula.
    /// </summary>
    /// <param name="newSheetId">Stable owner worksheet identifier.</param>
    /// <param name="newRowIndex">One-based Excel row index.</param>
    /// <param name="newColumnIndex">One-based Excel column index.</param>
    /// <param name="newFormulaExpression">Authored Excel expression with an optional leading equals sign.</param>
    /// <param name="newBrushId">Stable brush preset identifier.</param>
    public void ConfigureFormula(string newSheetId,
                                 int newRowIndex,
                                 int newColumnIndex,
                                 string newFormulaExpression,
                                 string newBrushId)
    {
        sheetId = newSheetId;
        rowIndex = newRowIndex;
        columnIndex = newColumnIndex;
        contentKind = ExcelDataWorkbookCellContentKind.Formula;
        fieldBinding = new ExcelDataFieldBinding();
        literalText = string.Empty;
        formulaExpression = newFormulaExpression;
        direction = ExcelDataTransferDirection.Export;
        brushId = newBrushId;
        numberFormat = string.Empty;
        validateLiteralDuringImport = false;
    }

    /// <summary>
    /// Checks whether this definition owns the requested worksheet coordinate.
    /// </summary>
    /// <param name="targetSheetId">Stable worksheet identifier to compare.</param>
    /// <param name="targetRowIndex">One-based row index to compare.</param>
    /// <param name="targetColumnIndex">One-based column index to compare.</param>
    /// <returns>True when sheet and coordinates match.</returns>
    public bool MatchesCell(string targetSheetId, int targetRowIndex, int targetColumnIndex)
    {
        return string.Equals(sheetId, targetSheetId, StringComparison.Ordinal) &&
               rowIndex == targetRowIndex &&
               columnIndex == targetColumnIndex;
    }

    /// <summary>
    /// Reports whether this definition has valid coordinates and content identity.
    /// </summary>
    /// <returns>True when the cell can participate in a workbook document.</returns>
    public bool IsUsable()
    {
        if (string.IsNullOrWhiteSpace(sheetId) || rowIndex < 1 || columnIndex < 1)
            return false;

        switch (contentKind)
        {
            case ExcelDataWorkbookCellContentKind.DataField:
                return fieldBinding != null && fieldBinding.IsUsable();
            case ExcelDataWorkbookCellContentKind.LiteralText:
                return true;
            case ExcelDataWorkbookCellContentKind.Formula:
                return ExcelDataFormulaExpressionUtility.TryNormalize(formulaExpression,
                                                                      out string _,
                                                                      out string _);
            default:
                return false;
        }
    }

    /// <summary>
    /// Reports whether the cell participates in export operations.
    /// </summary>
    /// <returns>True for Export and Both directions.</returns>
    public bool IncludesExport()
    {
        return direction != ExcelDataTransferDirection.Import;
    }

    /// <summary>
    /// Reports whether the cell participates in import operations.
    /// </summary>
    /// <returns>True for Import and Both directions.</returns>
    public bool IncludesImport()
    {
        return direction != ExcelDataTransferDirection.Export;
    }

    /// <summary>
    /// Moves this authored payload to another exact coordinate without changing its binding or style.
    /// </summary>
    /// <param name="newSheetId">Stable owner worksheet identifier.</param>
    /// <param name="newRowIndex">New one-based Excel row index.</param>
    /// <param name="newColumnIndex">New one-based Excel column index.</param>
    public void MoveTo(string newSheetId, int newRowIndex, int newColumnIndex)
    {
        sheetId = newSheetId;
        rowIndex = newRowIndex;
        columnIndex = newColumnIndex;
    }
    #endregion

    #endregion
}
