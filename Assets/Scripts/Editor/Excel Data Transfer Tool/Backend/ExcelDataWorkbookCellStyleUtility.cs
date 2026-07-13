using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

/// <summary>
/// Applies complete layout-grid borders and optional brush background or text colors to Open XML worksheets.
/// </summary>
internal static class ExcelDataWorkbookCellStyleUtility
{
    #region Constants
    private const string StylesEntryPath = "xl/styles.xml";
    private const string GridBorderColor = "FF808080";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Formats every coordinate in user worksheets whose document requests complete layout-grid styling.
    /// </summary>
    /// <param name="workbookPath">Absolute exported .xlsx path.</param>
    /// <param name="document">Source workbook document containing dimensions and optional brush colors.</param>
    public static void Apply(string workbookPath, ExcelDataWorkbookDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (!ContainsFormattedSheet(document))
            return;

        using (FileStream stream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, false))
        {
            Dictionary<string, string> worksheetEntries =
                ExcelDataOpenXmlPackageUtility.BuildWorksheetEntryLookup(archive);
            XDocument styles = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, StylesEntryPath);
            StyleRegistry styleRegistry = new StyleRegistry(styles);

            // Apply one shared style registry so equal brush colors reuse the same Open XML records.
            for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
            {
                ExcelDataWorkbookSheetDocument sheet = document.Sheets[sheetIndex];

                if (!sheet.FormatLayoutGrid)
                    continue;

                string sanitizedSheetName =
                    ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName,
                                                                   "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));
                string worksheetEntryPath;

                if (!worksheetEntries.TryGetValue(sanitizedSheetName, out worksheetEntryPath))
                    throw new InvalidDataException("Open XML workbook is missing worksheet relationship for: " + sanitizedSheetName);

                FormatWorksheet(archive, worksheetEntryPath, sheet, styleRegistry);
            }

            ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive, StylesEntryPath, styles);
        }
    }
    #endregion

    #region Worksheet Formatting
    /// <summary>
    /// Materializes and styles the full rectangular layout range of one worksheet.
    /// </summary>
    /// <param name="archive">Open update-mode workbook package.</param>
    /// <param name="entryPath">Worksheet ZIP entry path.</param>
    /// <param name="sheet">Source dimensions and authored colors.</param>
    /// <param name="styleRegistry">Shared workbook style registry.</param>
    private static void FormatWorksheet(ZipArchive archive,
                                        string entryPath,
                                        ExcelDataWorkbookSheetDocument sheet,
                                        StyleRegistry styleRegistry)
    {
        XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
        XDocument worksheet = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, entryPath);
        XElement worksheetRoot = worksheet.Root;
        XElement sheetData = worksheetRoot == null
            ? null
            : worksheetRoot.Element(spreadsheetNamespace + "sheetData");

        if (worksheetRoot == null || sheetData == null)
            throw new InvalidDataException("Open XML worksheet has no sheetData element: " + entryPath);

        Dictionary<int, XElement> rowsByIndex = BuildRowLookup(sheetData, spreadsheetNamespace);

        // Create and style every cell, including empty coordinates between or after authored payloads.
        for (int rowIndex = 1; rowIndex <= sheet.RowCount; rowIndex++)
        {
            XElement row = GetOrCreateRow(sheetData, rowsByIndex, rowIndex, spreadsheetNamespace);
            Dictionary<int, XElement> cellsByColumn = BuildCellLookup(row, spreadsheetNamespace);

            for (int columnIndex = 1; columnIndex <= sheet.ColumnCount; columnIndex++)
            {
                XElement cell = GetOrCreateCell(row,
                                                cellsByColumn,
                                                rowIndex,
                                                columnIndex,
                                                spreadsheetNamespace);
                int baseStyleIndex = ReadIntAttribute(cell, "s", 0);
                Color32 brushColor;
                bool hasBrushColor = sheet.TryGetBackgroundColor(rowIndex, columnIndex, out brushColor);
                Color32 textColor;
                bool hasTextColor = sheet.TryGetTextColor(rowIndex, columnIndex, out textColor);
                int styleIndex = styleRegistry.ResolveCellStyle(baseStyleIndex,
                                                                hasBrushColor ? BuildRgbColor(brushColor) : string.Empty,
                                                                hasTextColor ? BuildRgbColor(textColor) : string.Empty);
                cell.SetAttributeValue("s", styleIndex);
            }
        }

        UpdateDimension(worksheetRoot, sheet.RowCount, sheet.ColumnCount, spreadsheetNamespace);
        ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive, entryPath, worksheet);
    }

    /// <summary>
    /// Indexes worksheet rows by their one-based row number.
    /// </summary>
    /// <param name="sheetData">Worksheet sheetData element.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>Rows keyed by one-based index.</returns>
    private static Dictionary<int, XElement> BuildRowLookup(XElement sheetData,
                                                             XNamespace spreadsheetNamespace)
    {
        Dictionary<int, XElement> rowsByIndex = new Dictionary<int, XElement>();

        // Ignore malformed rows here; a valid row for each required index is created below.
        foreach (XElement row in sheetData.Elements(spreadsheetNamespace + "row"))
        {
            int rowIndex = ReadIntAttribute(row, "r", 0);

            if (rowIndex > 0 && !rowsByIndex.ContainsKey(rowIndex))
                rowsByIndex.Add(rowIndex, row);
        }

        return rowsByIndex;
    }

    /// <summary>
    /// Returns an existing worksheet row or inserts a new row in numeric order.
    /// </summary>
    /// <param name="sheetData">Worksheet sheetData element.</param>
    /// <param name="rowsByIndex">Current row lookup.</param>
    /// <param name="rowIndex">Required one-based row index.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>Existing or newly inserted row.</returns>
    private static XElement GetOrCreateRow(XElement sheetData,
                                           IDictionary<int, XElement> rowsByIndex,
                                           int rowIndex,
                                           XNamespace spreadsheetNamespace)
    {
        XElement row;

        if (rowsByIndex.TryGetValue(rowIndex, out row))
            return row;

        row = new XElement(spreadsheetNamespace + "row", new XAttribute("r", rowIndex));
        XElement followingRow = sheetData.Elements(spreadsheetNamespace + "row")
                                           .FirstOrDefault(candidate => ReadIntAttribute(candidate, "r", 0) > rowIndex);

        if (followingRow == null)
            sheetData.Add(row);
        else
            followingRow.AddBeforeSelf(row);

        rowsByIndex.Add(rowIndex, row);
        return row;
    }

    /// <summary>
    /// Indexes existing cells in one row by their one-based column index.
    /// </summary>
    /// <param name="row">Worksheet row to inspect.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>Cells keyed by one-based column index.</returns>
    private static Dictionary<int, XElement> BuildCellLookup(XElement row,
                                                              XNamespace spreadsheetNamespace)
    {
        Dictionary<int, XElement> cellsByColumn = new Dictionary<int, XElement>();

        // Parse cell references rather than relying on XML order or omitted empty cells.
        foreach (XElement cell in row.Elements(spreadsheetNamespace + "c"))
        {
            int columnIndex = ResolveColumnIndex(ReadStringAttribute(cell, "r"));

            if (columnIndex > 0 && !cellsByColumn.ContainsKey(columnIndex))
                cellsByColumn.Add(columnIndex, cell);
        }

        return cellsByColumn;
    }

    /// <summary>
    /// Returns an existing worksheet cell or inserts an empty styled cell in column order.
    /// </summary>
    /// <param name="row">Worksheet row receiving the cell.</param>
    /// <param name="cellsByColumn">Current cell lookup for the row.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>Existing or newly inserted cell.</returns>
    private static XElement GetOrCreateCell(XElement row,
                                            IDictionary<int, XElement> cellsByColumn,
                                            int rowIndex,
                                            int columnIndex,
                                            XNamespace spreadsheetNamespace)
    {
        XElement cell;

        if (cellsByColumn.TryGetValue(columnIndex, out cell))
            return cell;

        string address = ExcelDataWorkbookCoordinateUtility.BuildAddress(rowIndex, columnIndex);
        cell = new XElement(spreadsheetNamespace + "c", new XAttribute("r", address));
        XElement followingCell = row.Elements(spreadsheetNamespace + "c")
                                    .FirstOrDefault(candidate => ResolveColumnIndex(ReadStringAttribute(candidate, "r")) > columnIndex);

        if (followingCell == null)
            row.Add(cell);
        else
            followingCell.AddBeforeSelf(cell);

        cellsByColumn.Add(columnIndex, cell);
        return cell;
    }

    /// <summary>
    /// Updates the worksheet used-range marker to the complete authored layout rectangle.
    /// </summary>
    /// <param name="worksheetRoot">Worksheet root element.</param>
    /// <param name="rowCount">Complete layout row count.</param>
    /// <param name="columnCount">Complete layout column count.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    private static void UpdateDimension(XElement worksheetRoot,
                                        int rowCount,
                                        int columnCount,
                                        XNamespace spreadsheetNamespace)
    {
        XElement dimension = worksheetRoot.Element(spreadsheetNamespace + "dimension");

        if (dimension == null)
        {
            dimension = new XElement(spreadsheetNamespace + "dimension");
            worksheetRoot.AddFirst(dimension);
        }

        dimension.SetAttributeValue("ref",
                                    "A1:" + ExcelDataWorkbookCoordinateUtility.BuildAddress(rowCount, columnCount));
    }
    #endregion

    #region Value Helpers
    /// <summary>
    /// Converts an opaque Unity color to the ARGB string required by Open XML fills.
    /// </summary>
    /// <param name="color">Brush color whose RGB channels must be preserved.</param>
    /// <returns>Opaque eight-digit ARGB text.</returns>
    private static string BuildRgbColor(Color32 color)
    {
        return "FF" + color.r.ToString("X2", CultureInfo.InvariantCulture) +
               color.g.ToString("X2", CultureInfo.InvariantCulture) +
               color.b.ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Resolves a one-based column index from an A1-style cell reference.
    /// </summary>
    /// <param name="address">Cell reference such as A1 or BC17.</param>
    /// <returns>One-based column index, or zero for malformed references.</returns>
    private static int ResolveColumnIndex(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return 0;

        int columnIndex = 0;

        for (int characterIndex = 0; characterIndex < address.Length; characterIndex++)
        {
            char character = char.ToUpperInvariant(address[characterIndex]);

            if (character < 'A' || character > 'Z')
                break;

            columnIndex = columnIndex * 26 + character - 'A' + 1;
        }

        return columnIndex;
    }

    /// <summary>
    /// Reads one integer XML attribute with an explicit fallback.
    /// </summary>
    /// <param name="element">Element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <param name="fallback">Value returned when parsing fails.</param>
    /// <returns>Parsed integer or fallback.</returns>
    private static int ReadIntAttribute(XElement element, string attributeName, int fallback)
    {
        int parsedValue;
        return int.TryParse(ReadStringAttribute(element, attributeName),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out parsedValue)
            ? parsedValue
            : fallback;
    }

    /// <summary>
    /// Reads one unqualified XML attribute as text.
    /// </summary>
    /// <param name="element">Element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>Attribute text, or an empty string.</returns>
    private static string ReadStringAttribute(XElement element, string attributeName)
    {
        XAttribute attribute = element == null ? null : element.Attribute(attributeName);
        return attribute == null ? string.Empty : attribute.Value;
    }

    /// <summary>
    /// Reports whether at least one worksheet requests complete grid formatting.
    /// </summary>
    /// <param name="document">Workbook document to inspect.</param>
    /// <returns>True when a user worksheet requires style post-processing.</returns>
    private static bool ContainsFormattedSheet(ExcelDataWorkbookDocument document)
    {
        for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
        {
            if (document.Sheets[sheetIndex].FormatLayoutGrid)
                return true;
        }

        return false;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Deduplicates workbook border, fill and cell-format records across every formatted user sheet.
    /// </summary>
    private sealed class StyleRegistry
    {
        #region Fields
        private readonly XElement borders;
        private readonly XElement cellFormats;
        private readonly XElement fills;
        private readonly XElement fonts;
        private readonly XNamespace spreadsheetNamespace;
        private readonly Dictionary<string, int> fillIdsByColor = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> fontIdsByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> styleIdsByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly int gridBorderId;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Resolves required style collections and appends one shared neutral grid border.
        /// </summary>
        /// <param name="styles">Workbook styles document generated by MiniExcel.</param>
        public StyleRegistry(XDocument styles)
        {
            spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
            XElement root = styles == null ? null : styles.Root;

            if (root == null)
                throw new InvalidDataException("Open XML styles document has no root element.");

            fills = root.Element(spreadsheetNamespace + "fills");
            fonts = root.Element(spreadsheetNamespace + "fonts");
            borders = root.Element(spreadsheetNamespace + "borders");
            cellFormats = root.Element(spreadsheetNamespace + "cellXfs");

            if (fills == null || fonts == null || borders == null || cellFormats == null)
                throw new InvalidDataException("Open XML styles document is missing fills, fonts, borders or cellXfs.");

            gridBorderId = AppendGridBorder();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Resolves one derived cell format while preserving the MiniExcel base style.
        /// </summary>
        /// <param name="baseStyleIndex">Existing worksheet cell style index.</param>
        /// <param name="brushColor">Optional opaque ARGB brush fill.</param>
        /// <param name="textColor">Optional opaque ARGB brush text color.</param>
        /// <returns>Cell-format index containing the grid border and optional fill or font color.</returns>
        public int ResolveCellStyle(int baseStyleIndex, string brushColor, string textColor)
        {
            int fillId = string.IsNullOrWhiteSpace(brushColor) ? -1 : ResolveFillId(brushColor);
            List<XElement> existingFormats = cellFormats.Elements(spreadsheetNamespace + "xf").ToList();

            if (baseStyleIndex < 0 || baseStyleIndex >= existingFormats.Count)
                baseStyleIndex = 0;

            XElement derivedFormat = new XElement(existingFormats[baseStyleIndex]);
            int baseFontId = ReadIntAttribute(derivedFormat, "fontId", 0);
            int fontId = string.IsNullOrWhiteSpace(textColor) ? -1 : ResolveFontId(baseFontId, textColor);
            string styleKey = baseStyleIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                              fillId.ToString(CultureInfo.InvariantCulture) + ":" +
                              fontId.ToString(CultureInfo.InvariantCulture);
            int styleIndex;

            if (styleIdsByKey.TryGetValue(styleKey, out styleIndex))
                return styleIndex;

            derivedFormat.SetAttributeValue("borderId", gridBorderId);
            derivedFormat.SetAttributeValue("applyBorder", 1);

            if (fillId >= 0)
            {
                derivedFormat.SetAttributeValue("fillId", fillId);
                derivedFormat.SetAttributeValue("applyFill", 1);
            }

            if (fontId >= 0)
            {
                derivedFormat.SetAttributeValue("fontId", fontId);
                derivedFormat.SetAttributeValue("applyFont", 1);
            }

            styleIndex = existingFormats.Count;
            cellFormats.Add(derivedFormat);
            cellFormats.SetAttributeValue("count", styleIndex + 1);
            styleIdsByKey.Add(styleKey, styleIndex);
            return styleIndex;
        }
        #endregion

        #region Style Records
        /// <summary>
        /// Appends the thin neutral border used by every coordinate in a layout worksheet.
        /// </summary>
        /// <returns>Zero-based border index.</returns>
        private int AppendGridBorder()
        {
            int borderIndex = borders.Elements(spreadsheetNamespace + "border").Count();
            XElement border = new XElement(spreadsheetNamespace + "border");
            border.Add(CreateBorderSide("left"));
            border.Add(CreateBorderSide("right"));
            border.Add(CreateBorderSide("top"));
            border.Add(CreateBorderSide("bottom"));
            border.Add(new XElement(spreadsheetNamespace + "diagonal"));
            borders.Add(border);
            borders.SetAttributeValue("count", borderIndex + 1);
            return borderIndex;
        }

        /// <summary>
        /// Creates one thin side of the complete layout-grid border.
        /// </summary>
        /// <param name="sideName">SpreadsheetML side element name.</param>
        /// <returns>Configured border side.</returns>
        private XElement CreateBorderSide(string sideName)
        {
            return new XElement(spreadsheetNamespace + sideName,
                                new XAttribute("style", "thin"),
                                new XElement(spreadsheetNamespace + "color",
                                             new XAttribute("rgb", GridBorderColor)));
        }

        /// <summary>
        /// Resolves or creates one solid fill for an opaque brush color.
        /// </summary>
        /// <param name="brushColor">Eight-digit ARGB brush color.</param>
        /// <returns>Zero-based fill index.</returns>
        private int ResolveFillId(string brushColor)
        {
            int fillId;

            if (fillIdsByColor.TryGetValue(brushColor, out fillId))
                return fillId;

            fillId = fills.Elements(spreadsheetNamespace + "fill").Count();
            XElement patternFill = new XElement(spreadsheetNamespace + "patternFill",
                                                new XAttribute("patternType", "solid"),
                                                new XElement(spreadsheetNamespace + "fgColor",
                                                             new XAttribute("rgb", brushColor)),
                                                new XElement(spreadsheetNamespace + "bgColor",
                                                             new XAttribute("indexed", 64)));
            fills.Add(new XElement(spreadsheetNamespace + "fill", patternFill));
            fills.SetAttributeValue("count", fillId + 1);
            fillIdsByColor.Add(brushColor, fillId);
            return fillId;
        }

        /// <summary>
        /// Resolves or creates one font derived from the base style with an exact brush text color.
        /// </summary>
        /// <param name="baseFontId">Existing font index referenced by the MiniExcel base style.</param>
        /// <param name="textColor">Eight-digit ARGB brush text color.</param>
        /// <returns>Zero-based derived font index.</returns>
        private int ResolveFontId(int baseFontId, string textColor)
        {
            List<XElement> existingFonts = fonts.Elements(spreadsheetNamespace + "font").ToList();

            if (existingFonts.Count <= 0)
                throw new InvalidDataException("Open XML styles document contains no base font records.");

            if (baseFontId < 0 || baseFontId >= existingFonts.Count)
                baseFontId = 0;

            string fontKey = baseFontId.ToString(CultureInfo.InvariantCulture) + ":" + textColor;
            int fontId;

            if (fontIdsByKey.TryGetValue(fontKey, out fontId))
                return fontId;

            XElement derivedFont = new XElement(existingFonts[baseFontId]);
            XElement existingColor = derivedFont.Element(spreadsheetNamespace + "color");
            XElement color = new XElement(spreadsheetNamespace + "color",
                                          new XAttribute("rgb", textColor));

            if (existingColor == null)
            {
                XElement size = derivedFont.Element(spreadsheetNamespace + "sz");

                if (size == null)
                    derivedFont.AddFirst(color);
                else
                    size.AddAfterSelf(color);
            }
            else
                existingColor.ReplaceWith(color);

            fontId = existingFonts.Count;
            fonts.Add(derivedFont);
            fonts.SetAttributeValue("count", fontId + 1);
            fontIdsByKey.Add(fontKey, fontId);
            return fontId;
        }
        #endregion

        #endregion
    }
    #endregion
}
