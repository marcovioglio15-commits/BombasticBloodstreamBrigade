using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one grid-authoritative workbook sheet and its non-empty authored cells.
/// </summary>
[Serializable]
public sealed class ExcelDataWorkbookSheetDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable worksheet identifier used by cell definitions independently from the visible sheet name.")]
    [SerializeField] private string sheetId;

    [Tooltip("Visible Excel worksheet name before invalid-character sanitization by the workbook adapter.")]
    [SerializeField] private string sheetName = "Objects";

    [Tooltip("Rows displayed by the layout designer preview; export uses the maximum authored cell coordinate.")]
    [Min(1)]
    [SerializeField] private int previewRowCount = 32;

    [Tooltip("Columns displayed by the layout designer preview; export uses the maximum authored cell coordinate.")]
    [Min(1)]
    [SerializeField] private int previewColumnCount = 16;

    [Tooltip("Visible cell width in pixels used only by the Unity layout designer preview.")]
    [Min(24)]
    [SerializeField] private int previewCellWidth = 112;

    [Tooltip("Visible cell height in pixels used only by the Unity layout designer preview.")]
    [Min(18)]
    [SerializeField] private int previewCellHeight = 28;

    [Tooltip("Number of leading worksheet rows frozen by formatting-capable workbook adapters.")]
    [Min(0)]
    [SerializeField] private int freezeRowCount;

    [Tooltip("Number of leading worksheet columns frozen by formatting-capable workbook adapters.")]
    [Min(0)]
    [SerializeField] private int freezeColumnCount;

    [Tooltip("Controls whether this worksheet is visible, hidden or very hidden in Excel.")]
    [SerializeField] private ExcelDataWorkbookSheetVisibility visibility;

    [Tooltip("Allow this worksheet to participate in import operations.")]
    [SerializeField] private bool importEnabled = true;

    [Tooltip("Allow this worksheet to participate in export operations.")]
    [SerializeField] private bool exportEnabled = true;

    [Tooltip("Non-empty workbook cells authored for this worksheet.")]
    [SerializeField] private List<ExcelDataWorkbookCellDefinition> cells = new List<ExcelDataWorkbookCellDefinition>();
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

    public string SheetName
    {
        get
        {
            return sheetName;
        }
    }

    public int PreviewRowCount
    {
        get
        {
            return previewRowCount;
        }
    }

    public int PreviewColumnCount
    {
        get
        {
            return previewColumnCount;
        }
    }

    public int PreviewCellWidth
    {
        get
        {
            return previewCellWidth;
        }
    }

    public int PreviewCellHeight
    {
        get
        {
            return previewCellHeight;
        }
    }

    public int FreezeRowCount
    {
        get
        {
            return freezeRowCount;
        }
    }

    public int FreezeColumnCount
    {
        get
        {
            return freezeColumnCount;
        }
    }

    public ExcelDataWorkbookSheetVisibility Visibility
    {
        get
        {
            return visibility;
        }
    }

    public bool ImportEnabled
    {
        get
        {
            return importEnabled;
        }
    }

    public bool ExportEnabled
    {
        get
        {
            return exportEnabled;
        }
    }

    public List<ExcelDataWorkbookCellDefinition> Cells
    {
        get
        {
            EnsureCollections();
            return cells;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures worksheet identity, preview dimensions and operation availability.
    /// </summary>
    /// <param name="newSheetName">Visible worksheet name.</param>
    /// <param name="newPreviewRowCount">Rows displayed by the Unity preview.</param>
    /// <param name="newPreviewColumnCount">Columns displayed by the Unity preview.</param>
    /// <param name="newPreviewCellWidth">Preview cell width in pixels.</param>
    /// <param name="newPreviewCellHeight">Preview cell height in pixels.</param>
    /// <param name="newImportEnabled">True when this sheet participates in import.</param>
    /// <param name="newExportEnabled">True when this sheet participates in export.</param>
    /// <param name="newVisibility">Workbook worksheet visibility.</param>
    public void Configure(string newSheetName,
                          int newPreviewRowCount,
                          int newPreviewColumnCount,
                          int newPreviewCellWidth,
                          int newPreviewCellHeight,
                          bool newImportEnabled,
                          bool newExportEnabled,
                          ExcelDataWorkbookSheetVisibility newVisibility)
    {
        if (string.IsNullOrWhiteSpace(sheetId))
            sheetId = Guid.NewGuid().ToString("N");

        sheetName = newSheetName;
        previewRowCount = newPreviewRowCount;
        previewColumnCount = newPreviewColumnCount;
        previewCellWidth = newPreviewCellWidth;
        previewCellHeight = newPreviewCellHeight;
        importEnabled = newImportEnabled;
        exportEnabled = newExportEnabled;
        visibility = newVisibility;
        EnsureCollections();
    }

    /// <summary>
    /// Restores complete worksheet identity and behavior from a trusted grid-authoritative workbook snapshot.
    /// </summary>
    /// <param name="newSheetId">Stable worksheet identifier stored in the technical sheet.</param>
    /// <param name="newSheetName">Authored visible worksheet name.</param>
    /// <param name="newPreviewRowCount">Rows displayed by the Unity preview.</param>
    /// <param name="newPreviewColumnCount">Columns displayed by the Unity preview.</param>
    /// <param name="newPreviewCellWidth">Preview cell width in pixels.</param>
    /// <param name="newPreviewCellHeight">Preview cell height in pixels.</param>
    /// <param name="newFreezeRowCount">Leading rows frozen by formatting-capable adapters.</param>
    /// <param name="newFreezeColumnCount">Leading columns frozen by formatting-capable adapters.</param>
    /// <param name="newImportEnabled">True when this sheet participates in import.</param>
    /// <param name="newExportEnabled">True when this sheet participates in export.</param>
    /// <param name="newVisibility">Workbook worksheet visibility.</param>
    public void ConfigureFromSnapshot(string newSheetId,
                                      string newSheetName,
                                      int newPreviewRowCount,
                                      int newPreviewColumnCount,
                                      int newPreviewCellWidth,
                                      int newPreviewCellHeight,
                                      int newFreezeRowCount,
                                      int newFreezeColumnCount,
                                      bool newImportEnabled,
                                      bool newExportEnabled,
                                      ExcelDataWorkbookSheetVisibility newVisibility)
    {
        sheetId = newSheetId;
        sheetName = newSheetName;
        previewRowCount = newPreviewRowCount;
        previewColumnCount = newPreviewColumnCount;
        previewCellWidth = newPreviewCellWidth;
        previewCellHeight = newPreviewCellHeight;
        freezeRowCount = newFreezeRowCount;
        freezeColumnCount = newFreezeColumnCount;
        importEnabled = newImportEnabled;
        exportEnabled = newExportEnabled;
        visibility = newVisibility;
        EnsureCollections();
    }

    /// <summary>
    /// Updates freeze-pane counts used by workbook formatting without changing worksheet dimensions.
    /// </summary>
    /// <param name="newFreezeRowCount">Leading rows frozen in the workbook.</param>
    /// <param name="newFreezeColumnCount">Leading columns frozen in the workbook.</param>
    public void ConfigureFreezePanes(int newFreezeRowCount, int newFreezeColumnCount)
    {
        freezeRowCount = newFreezeRowCount;
        freezeColumnCount = newFreezeColumnCount;
    }

    /// <summary>
    /// Ensures stable identity and serialized collections without snapping authored dimensions.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(sheetId))
            sheetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(sheetName))
            sheetName = "Sheet";

        EnsureCollections();
    }

    /// <summary>
    /// Finds one authored cell by its one-based worksheet coordinate.
    /// </summary>
    /// <param name="rowIndex">One-based row index to search.</param>
    /// <param name="columnIndex">One-based column index to search.</param>
    /// <returns>Matching cell definition, or null when the coordinate is empty.</returns>
    public ExcelDataWorkbookCellDefinition FindCell(int rowIndex, int columnIndex)
    {
        EnsureCollections();

        // Search the sparse authored cell list without allocating a coordinate lookup.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (cell == null)
                continue;

            if (cell.MatchesCell(sheetId, rowIndex, columnIndex))
                return cell;
        }

        return null;
    }

    /// <summary>
    /// Updates preview dimensions after an explicit toolbar edit without modifying authored cell coordinates.
    /// </summary>
    /// <param name="newPreviewRowCount">New preview row count.</param>
    /// <param name="newPreviewColumnCount">New preview column count.</param>
    /// <param name="newPreviewCellWidth">New preview cell width in pixels.</param>
    /// <param name="newPreviewCellHeight">New preview cell height in pixels.</param>
    public void ConfigurePreview(int newPreviewRowCount,
                                 int newPreviewColumnCount,
                                 int newPreviewCellWidth,
                                 int newPreviewCellHeight)
    {
        previewRowCount = newPreviewRowCount;
        previewColumnCount = newPreviewColumnCount;
        previewCellWidth = newPreviewCellWidth;
        previewCellHeight = newPreviewCellHeight;
    }

    /// <summary>
    /// Inserts one empty row and shifts every authored cell at or below that coordinate down by one.
    /// </summary>
    /// <param name="insertionRowIndex">One-based row that becomes the new empty row.</param>
    public void InsertEmptyRow(int insertionRowIndex)
    {
        if (insertionRowIndex < 1 || insertionRowIndex > previewRowCount + 1)
            throw new ArgumentOutOfRangeException(nameof(insertionRowIndex), insertionRowIndex, "Inserted row is outside the preview bounds.");

        EnsureCollections();

        // Shift every sparse cell independently so authored payload and style remain intact.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (cell != null && cell.RowIndex >= insertionRowIndex)
                cell.MoveTo(sheetId, cell.RowIndex + 1, cell.ColumnIndex);
        }

        previewRowCount++;
    }

    /// <summary>
    /// Inserts one empty column and shifts every authored cell at or right of that coordinate by one.
    /// </summary>
    /// <param name="insertionColumnIndex">One-based column that becomes the new empty column.</param>
    public void InsertEmptyColumn(int insertionColumnIndex)
    {
        if (insertionColumnIndex < 1 || insertionColumnIndex > previewColumnCount + 1)
            throw new ArgumentOutOfRangeException(nameof(insertionColumnIndex), insertionColumnIndex, "Inserted column is outside the preview bounds.");

        EnsureCollections();

        // Shift every sparse cell independently so authored payload and style remain intact.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (cell != null && cell.ColumnIndex >= insertionColumnIndex)
                cell.MoveTo(sheetId, cell.RowIndex, cell.ColumnIndex + 1);
        }

        previewColumnCount++;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Recreates the serialized cell collection when Unity deserializes it as null.
    /// </summary>
    private void EnsureCollections()
    {
        if (cells == null)
            cells = new List<ExcelDataWorkbookCellDefinition>();
    }
    #endregion

    #endregion
}
