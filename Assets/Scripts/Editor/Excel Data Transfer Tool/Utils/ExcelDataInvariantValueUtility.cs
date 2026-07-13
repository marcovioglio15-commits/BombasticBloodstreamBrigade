using System;
using System.Globalization;

/// <summary>
/// Formats workbook and Unity scalar values consistently across preview, comparison and editor presentation.
/// </summary>
internal static class ExcelDataInvariantValueUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts one scalar into culture-independent text while treating null database values as empty cells.
    /// </summary>
    /// <param name="value">Raw workbook or typed Unity scalar.</param>
    /// <returns>Invariant text, or an empty string when the value is null.</returns>
    public static string ToText(object value)
    {
        if (value == null || value == DBNull.Value)
            return string.Empty;

        IFormattable formattable = value as IFormattable;

        if (formattable != null)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }
    #endregion

    #endregion
}
