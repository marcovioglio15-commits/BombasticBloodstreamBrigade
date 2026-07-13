using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Converts one-based worksheet coordinates into stable keys and readable Excel addresses.
/// </summary>
internal static class ExcelDataWorkbookCoordinateUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Packs a positive row and column into one allocation-free dictionary key.
    /// </summary>
    /// <param name="rowIndex">One-based worksheet row index.</param>
    /// <param name="columnIndex">One-based worksheet column index.</param>
    /// <returns>Unique key for valid Int32 worksheet coordinates.</returns>
    public static long BuildKey(int rowIndex, int columnIndex)
    {
        return ((long)rowIndex << 32) | (uint)columnIndex;
    }

    /// <summary>
    /// Builds a readable Excel address from one-based worksheet coordinates.
    /// </summary>
    /// <param name="rowIndex">One-based worksheet row index.</param>
    /// <param name="columnIndex">One-based worksheet column index.</param>
    /// <returns>Excel address such as A1 or AA12.</returns>
    public static string BuildAddress(int rowIndex, int columnIndex)
    {
        return ColumnIndexToName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts a positive one-based column index into its Excel column name.
    /// </summary>
    /// <param name="columnIndex">One-based worksheet column index.</param>
    /// <returns>Excel column name such as A, Z or AA.</returns>
    public static string ColumnIndexToName(int columnIndex)
    {
        if (columnIndex < 1)
            throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "Workbook column index must be positive.");

        StringBuilder columnName = new StringBuilder(4);

        // Convert the one-based index through repeated base-26 divisions.
        while (columnIndex > 0)
        {
            columnIndex--;
            columnName.Insert(0, (char)('A' + columnIndex % 26));
            columnIndex /= 26;
        }

        return columnName.ToString();
    }
    #endregion

    #endregion
}
