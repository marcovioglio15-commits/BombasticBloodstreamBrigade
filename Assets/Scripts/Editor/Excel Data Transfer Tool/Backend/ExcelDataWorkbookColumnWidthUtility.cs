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
    private const string WorkbookEntryPath = "xl/workbook.xml";
    private const string WorkbookRelationshipsEntryPath = "xl/_rels/workbook.xml.rels";
    private const double MaximumExcelColumnWidth = 255d;
    private const double DefaultCharacterPixelWidth = 7d;
    private const double CellPaddingPixels = 10d;
    #endregion

    #region Fields
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
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
            Dictionary<string, string> worksheetEntries = BuildWorksheetEntryLookup(archive);

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

    #region Package Mapping
    /// <summary>
    /// Maps workbook-visible sheet names to their ZIP worksheet entry paths through relationship IDs.
    /// </summary>
    /// <param name="archive">Open .xlsx ZIP package.</param>
    /// <returns>Case-insensitive worksheet entry lookup by visible sheet name.</returns>
    private static Dictionary<string, string> BuildWorksheetEntryLookup(ZipArchive archive)
    {
        XDocument workbook = LoadXmlEntry(archive, WorkbookEntryPath);
        XDocument relationships = LoadXmlEntry(archive, WorkbookRelationshipsEntryPath);
        Dictionary<string, string> targetsByRelationshipId = new Dictionary<string, string>(StringComparer.Ordinal);

        // Index package relationships before joining them with workbook sheet records.
        foreach (XElement relationship in relationships.Root.Elements(PackageRelationshipsNamespace + "Relationship"))
        {
            string relationshipId = ReadAttribute(relationship, "Id");
            string target = ReadAttribute(relationship, "Target");

            if (!string.IsNullOrWhiteSpace(relationshipId) && !string.IsNullOrWhiteSpace(target))
                targetsByRelationshipId[relationshipId] = NormalizeWorksheetEntryPath(target);
        }

        Dictionary<string, string> entriesBySheetName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XElement sheets = workbook.Root.Element(SpreadsheetNamespace + "sheets");

        if (sheets == null)
            throw new InvalidDataException("Open XML workbook has no sheets collection.");

        // Resolve each visible sheet through its office-document relationship attribute.
        foreach (XElement sheet in sheets.Elements(SpreadsheetNamespace + "sheet"))
        {
            string sheetName = ReadAttribute(sheet, "name");
            XAttribute relationshipAttribute = sheet.Attribute(OfficeRelationshipsNamespace + "id");
            string target;

            if (relationshipAttribute == null ||
                !targetsByRelationshipId.TryGetValue(relationshipAttribute.Value, out target))
                continue;

            entriesBySheetName[sheetName] = target;
        }

        return entriesBySheetName;
    }

    /// <summary>
    /// Loads one required XML entry from an Open XML ZIP package.
    /// </summary>
    /// <param name="archive">Open .xlsx ZIP package.</param>
    /// <param name="entryPath">Forward-slash ZIP entry path.</param>
    /// <returns>Parsed XML document.</returns>
    private static XDocument LoadXmlEntry(ZipArchive archive, string entryPath)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryPath);

        if (entry == null)
            throw new InvalidDataException("Open XML package is missing required entry: " + entryPath);

        using (Stream entryStream = entry.Open())
            return XDocument.Load(entryStream, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// Normalizes a workbook relationship target into a root-relative ZIP entry path.
    /// </summary>
    /// <param name="target">Relationship target from workbook.xml.rels.</param>
    /// <returns>Normalized worksheet ZIP entry path.</returns>
    private static string NormalizeWorksheetEntryPath(string target)
    {
        string normalizedTarget = target.Replace('\\', '/').TrimStart('/');

        if (normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            return normalizedTarget;

        while (normalizedTarget.StartsWith("../", StringComparison.Ordinal))
            normalizedTarget = normalizedTarget.Substring(3);

        return "xl/" + normalizedTarget;
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
        XDocument worksheet = LoadXmlEntry(archive, entryPath);
        XElement root = worksheet.Root;
        XElement existingColumns = root.Element(SpreadsheetNamespace + "cols");
        existingColumns?.Remove();
        XElement columns = new XElement(SpreadsheetNamespace + "cols");
        double[] widths = CalculateColumnWidths(sheet);

        // Emit one explicit column record so every width remains deterministic across spreadsheet viewers.
        for (int columnIndex = 1; columnIndex <= widths.Length; columnIndex++)
        {
            columns.Add(new XElement(SpreadsheetNamespace + "col",
                                     new XAttribute("min", columnIndex),
                                     new XAttribute("max", columnIndex),
                                     new XAttribute("width", widths[columnIndex - 1].ToString("0.###", CultureInfo.InvariantCulture)),
                                     new XAttribute("bestFit", 1),
                                     new XAttribute("customWidth", 1)));
        }

        XElement sheetData = root.Element(SpreadsheetNamespace + "sheetData");

        if (sheetData == null)
            throw new InvalidDataException("Open XML worksheet has no sheetData element: " + entryPath);

        sheetData.AddBeforeSelf(columns);
        ReplaceXmlEntry(archive, entryPath, worksheet);
    }

    /// <summary>
    /// Replaces one XML ZIP entry after its previous stream has been closed.
    /// </summary>
    /// <param name="archive">Open update-mode ZIP package.</param>
    /// <param name="entryPath">Entry path to replace.</param>
    /// <param name="document">Updated XML document.</param>
    private static void ReplaceXmlEntry(ZipArchive archive, string entryPath, XDocument document)
    {
        ZipArchiveEntry existingEntry = archive.GetEntry(entryPath);

        if (existingEntry == null)
            throw new InvalidDataException("Cannot replace missing Open XML entry: " + entryPath);

        existingEntry.Delete();
        ZipArchiveEntry replacementEntry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);

        using (Stream replacementStream = replacementEntry.Open())
            document.Save(replacementStream, SaveOptions.DisableFormatting);
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

    #region XML Helpers
    /// <summary>
    /// Reads one unqualified XML attribute as text.
    /// </summary>
    /// <param name="element">XML element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>Attribute text, or an empty string.</returns>
    private static string ReadAttribute(XElement element, string attributeName)
    {
        XAttribute attribute = element.Attribute(attributeName);
        return attribute == null ? string.Empty : attribute.Value;
    }
    #endregion

    #endregion
}
