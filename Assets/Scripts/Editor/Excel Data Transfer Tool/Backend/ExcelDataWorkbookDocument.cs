using System;
using System.Collections.Generic;

/// <summary>
/// Stores a cell-oriented workbook in memory before an editor-only adapter writes it to disk.
/// </summary>
internal sealed class ExcelDataWorkbookDocument
{
    #region Fields
    private readonly List<ExcelDataWorkbookSheetDocument> sheets = new List<ExcelDataWorkbookSheetDocument>();
    private readonly Dictionary<string, ExcelDataWorkbookSheetDocument> sheetsByName =
        new Dictionary<string, ExcelDataWorkbookSheetDocument>(StringComparer.Ordinal);
    #endregion

    #region Properties
    public IReadOnlyList<ExcelDataWorkbookSheetDocument> Sheets
    {
        get
        {
            return sheets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one uniquely named worksheet with fixed matrix dimensions.
    /// </summary>
    /// <param name="sheetName">Authored worksheet name.</param>
    /// <param name="rowCount">Number of rows allocated in the matrix.</param>
    /// <param name="columnCount">Number of columns allocated in the matrix.</param>
    /// <param name="visibility">Workbook visibility assigned by the adapter.</param>
    /// <param name="minimumColumnWidthPixels">Minimum exported column width derived from the authoring preview.</param>
    /// <param name="autoSizeColumns">True when the adapter should fit columns to their actual exported values.</param>
    /// <returns>Created worksheet document.</returns>
    public ExcelDataWorkbookSheetDocument AddSheet(string sheetName,
                                                   int rowCount,
                                                   int columnCount,
                                                   ExcelDataWorkbookSheetVisibility visibility,
                                                   int minimumColumnWidthPixels = 0,
                                                   bool autoSizeColumns = false)
    {
        // Validate identity and dimensions before allocating a potentially large matrix.
        if (string.IsNullOrWhiteSpace(sheetName))
            throw new ArgumentException("Workbook sheet name cannot be empty.", nameof(sheetName));

        if (rowCount < 1)
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "Workbook sheet row count must be positive.");

        if (columnCount < 1)
            throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "Workbook sheet column count must be positive.");

        if (sheetsByName.ContainsKey(sheetName))
            throw new InvalidOperationException("Workbook document already contains sheet: " + sheetName);

        // Register the same immutable sheet document in ordered and keyed collections.
        ExcelDataWorkbookSheetDocument sheet =
            new ExcelDataWorkbookSheetDocument(sheetName,
                                               rowCount,
                                               columnCount,
                                               visibility,
                                               minimumColumnWidthPixels,
                                               autoSizeColumns);
        sheets.Add(sheet);
        sheetsByName.Add(sheetName, sheet);
        return sheet;
    }

    /// <summary>
    /// Finds one worksheet by its authored name.
    /// </summary>
    /// <param name="sheetName">Authored worksheet name to search.</param>
    /// <returns>Matching worksheet, or null when the document does not contain it.</returns>
    public ExcelDataWorkbookSheetDocument FindSheet(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
            return null;

        ExcelDataWorkbookSheetDocument sheet;
        return sheetsByName.TryGetValue(sheetName, out sheet) ? sheet : null;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one fixed-size worksheet matrix using one-based public cell coordinates.
/// </summary>
internal sealed class ExcelDataWorkbookSheetDocument
{
    #region Fields
    private readonly object[,] values;
    #endregion

    #region Properties
    public string SheetName
    {
        get;
    }

    public int RowCount
    {
        get;
    }

    public int ColumnCount
    {
        get;
    }

    public ExcelDataWorkbookSheetVisibility Visibility
    {
        get;
    }

    public int MinimumColumnWidthPixels
    {
        get;
    }

    public bool AutoSizeColumns
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one fixed-size worksheet document.
    /// </summary>
    /// <param name="sheetName">Authored worksheet name.</param>
    /// <param name="rowCount">Positive row count.</param>
    /// <param name="columnCount">Positive column count.</param>
    /// <param name="visibility">Workbook worksheet visibility.</param>
    /// <param name="minimumColumnWidthPixels">Minimum exported column width in pixels.</param>
    /// <param name="autoSizeColumns">True when content-based widths should be written after export.</param>
    public ExcelDataWorkbookSheetDocument(string sheetName,
                                          int rowCount,
                                          int columnCount,
                                          ExcelDataWorkbookSheetVisibility visibility,
                                          int minimumColumnWidthPixels,
                                          bool autoSizeColumns)
    {
        SheetName = sheetName;
        RowCount = rowCount;
        ColumnCount = columnCount;
        Visibility = visibility;
        MinimumColumnWidthPixels = minimumColumnWidthPixels;
        AutoSizeColumns = autoSizeColumns;
        values = new object[rowCount, columnCount];
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Writes one value to an exact one-based worksheet coordinate.
    /// </summary>
    /// <param name="rowIndex">One-based Excel row index.</param>
    /// <param name="columnIndex">One-based Excel column index.</param>
    /// <param name="value">Typed workbook value or null for an empty cell.</param>
    public void SetValue(int rowIndex, int columnIndex, object value)
    {
        ValidateCoordinate(rowIndex, columnIndex);
        values[rowIndex - 1, columnIndex - 1] = value;
    }

    /// <summary>
    /// Reads one value from an exact one-based worksheet coordinate.
    /// </summary>
    /// <param name="rowIndex">One-based Excel row index.</param>
    /// <param name="columnIndex">One-based Excel column index.</param>
    /// <returns>Stored typed value, or null for an empty cell.</returns>
    public object GetValue(int rowIndex, int columnIndex)
    {
        ValidateCoordinate(rowIndex, columnIndex);
        return values[rowIndex - 1, columnIndex - 1];
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Validates a one-based coordinate against the fixed matrix dimensions.
    /// </summary>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    private void ValidateCoordinate(int rowIndex, int columnIndex)
    {
        if (rowIndex < 1 || rowIndex > RowCount)
            throw new ArgumentOutOfRangeException(nameof(rowIndex), rowIndex, "Workbook row is outside the sheet matrix.");

        if (columnIndex < 1 || columnIndex > ColumnCount)
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "Workbook column is outside the sheet matrix.");
    }
    #endregion

    #endregion
}
