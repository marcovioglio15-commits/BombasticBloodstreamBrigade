using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

/// <summary>
/// Writes deterministic content-fitted column widths into exported Open XML worksheets.
/// </summary>
internal static class ExcelDataWorkbookColumnWidthUtility
{
    #region Constants
    private const double MaximumExcelColumnWidth = 255d;
    private const double DefaultCharacterPixelWidth = 7d;
    private const double CellPaddingPixels = 10d;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies precomputed content widths to user worksheets after MiniExcel closes the output file.
    /// </summary>
    /// <param name="workbookPath">Absolute exported .xlsx path.</param>
    /// <param name="document">Source workbook document containing typed values and width policy.</param>
    public static void Apply(string workbookPath, ExcelDataWorkbookDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (!ContainsAutoSizedSheet(document))
            return;

        using (FileStream stream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, false))
        {
            Dictionary<string, string> worksheetEntries =
                ExcelDataOpenXmlPackageUtility.BuildWorksheetEntryLookup(archive);

            // Update only user sheets that explicitly request content fitting.
            for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
            {
                ExcelDataWorkbookSheetDocument sheet = document.Sheets[sheetIndex];

                if (!sheet.AutoSizeColumns)
                    continue;

                string sanitizedSheetName =
                    ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));
                string worksheetEntryPath;

                if (!worksheetEntries.TryGetValue(sanitizedSheetName, out worksheetEntryPath))
                    throw new InvalidDataException("Open XML workbook is missing worksheet relationship for: " + sanitizedSheetName);

                WriteColumnWidths(archive, worksheetEntryPath, sheet);
            }
        }
    }
    #endregion

    #region Width Writing
    /// <summary>
    /// Replaces one worksheet cols collection with deterministic content-fitted widths.
    /// </summary>
    /// <param name="archive">Open .xlsx ZIP package.</param>
    /// <param name="entryPath">Worksheet ZIP entry path.</param>
    /// <param name="sheet">Source worksheet values and width policy.</param>
    private static void WriteColumnWidths(ZipArchive archive,
                                          string entryPath,
                                          ExcelDataWorkbookSheetDocument sheet)
    {
        XDocument worksheet = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, entryPath);
        XElement root = worksheet.Root;
        XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
        XElement existingColumns = root.Element(spreadsheetNamespace + "cols");
        existingColumns?.Remove();
        XElement columns = new XElement(spreadsheetNamespace + "cols");
        double[] widths = CalculateColumnWidths(sheet);

        // Emit one explicit column record so every width remains deterministic across spreadsheet viewers.
        for (int columnIndex = 1; columnIndex <= widths.Length; columnIndex++)
        {
            columns.Add(new XElement(spreadsheetNamespace + "col",
                                     new XAttribute("min", columnIndex),
                                     new XAttribute("max", columnIndex),
                                     new XAttribute("width", widths[columnIndex - 1].ToString("0.###", CultureInfo.InvariantCulture)),
                                     new XAttribute("bestFit", 1),
                                     new XAttribute("customWidth", 1)));
        }

        XElement sheetData = root.Element(spreadsheetNamespace + "sheetData");

        if (sheetData == null)
            throw new InvalidDataException("Open XML worksheet has no sheetData element: " + entryPath);

        sheetData.AddBeforeSelf(columns);
        ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive, entryPath, worksheet);
    }
    #endregion

    #region Width Calculation
    /// <summary>
    /// Calculates conservative per-column Excel widths from actual exported values and preview minimums.
    /// </summary>
    /// <param name="sheet">Source worksheet document.</param>
    /// <returns>Excel width value for every materialized column.</returns>
    private static double[] CalculateColumnWidths(ExcelDataWorkbookSheetDocument sheet)
    {
        double[] widths = new double[sheet.ColumnCount];
        double minimumWidth = ConvertPixelsToExcelWidth(sheet.MinimumColumnWidthPixels);

        for (int columnIndex = 1; columnIndex <= sheet.ColumnCount; columnIndex++)
        {
            double contentWidth = 0d;

            for (int rowIndex = 1; rowIndex <= sheet.RowCount; rowIndex++)
                contentWidth = Math.Max(contentWidth, MeasureValue(sheet.GetValue(rowIndex, columnIndex)));

            widths[columnIndex - 1] = Math.Min(MaximumExcelColumnWidth,
                                              Math.Max(minimumWidth, Math.Ceiling(contentWidth + 2d)));
        }

        return widths;
    }

    /// <summary>
    /// Converts a Unity preview pixel width into Excel's approximate default-font character width unit.
    /// </summary>
    /// <param name="pixelWidth">Authored preview width in pixels.</param>
    /// <returns>Equivalent conservative Excel column width.</returns>
    private static double ConvertPixelsToExcelWidth(int pixelWidth)
    {
        if (pixelWidth <= 0)
            return 0d;

        return Math.Max(1d, (pixelWidth - CellPaddingPixels) / DefaultCharacterPixelWidth);
    }

    /// <summary>
    /// Estimates the longest visual line using conservative glyph weights for the default Excel font.
    /// </summary>
    /// <param name="value">Typed exported cell value.</param>
    /// <returns>Approximate character width of the longest displayed line.</returns>
    private static double MeasureValue(object value)
    {
        if (value == null || value == DBNull.Value)
            return 0d;

        string text = ConvertToDisplayText(value);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        double maximumWidth = 0d;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            double lineWidth = 0d;

            for (int characterIndex = 0; characterIndex < lines[lineIndex].Length; characterIndex++)
                lineWidth += MeasureCharacter(lines[lineIndex][characterIndex]);

            maximumWidth = Math.Max(maximumWidth, lineWidth);
        }

        return maximumWidth;
    }

    /// <summary>
    /// Converts one typed cell value into deterministic display text for width measurement.
    /// </summary>
    /// <param name="value">Typed exported cell value.</param>
    /// <returns>Invariant display text.</returns>
    private static string ConvertToDisplayText(object value)
    {
        if (value is bool)
            return (bool)value ? "TRUE" : "FALSE";

        IFormattable formattable = value as IFormattable;

        if (formattable != null)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Assigns a conservative width weight to one character without platform font APIs.
    /// </summary>
    /// <param name="character">Character to measure.</param>
    /// <returns>Approximate default-font character width.</returns>
    private static double MeasureCharacter(char character)
    {
        if (character == '\t')
            return 4d;

        if (character > 255)
            return 1.7d;

        if ("MW@#%&".IndexOf(character) >= 0)
            return 1.5d;

        if ("ilI.,'`:;|! ".IndexOf(character) >= 0)
            return 0.6d;

        return char.IsUpper(character) ? 1.15d : 1d;
    }

    /// <summary>
    /// Reports whether at least one worksheet requests deterministic content fitting.
    /// </summary>
    /// <param name="document">Workbook document to inspect.</param>
    /// <returns>True when the Open XML package requires width post-processing.</returns>
    private static bool ContainsAutoSizedSheet(ExcelDataWorkbookDocument document)
    {
        for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
        {
            if (document.Sheets[sheetIndex].AutoSizeColumns)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
