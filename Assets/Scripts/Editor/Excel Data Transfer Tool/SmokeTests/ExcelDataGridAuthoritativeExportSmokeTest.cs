using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates the public grid-authoritative export service against a real MiniExcel workbook.
/// </summary>
public static class ExcelDataGridAuthoritativeExportSmokeTest
{
    #region Constants
    private const string DataSheetName = "Objects";
    private const string SmokeBrushBackgroundArgb = "FF315A7D";
    private const string SmokeBrushTextArgb = "FFF1C232";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Exports typed project data to exact coordinates and validates user and technical worksheets.
    /// </summary>
    public static void Run()
    {
        List<ExcelDataFieldCatalogEntry> entries = ExcelDataFieldCatalogBuilder.BuildCatalog();
        ExcelDataFieldCatalogEntry numberEntry = FindReadableEntry(entries, ExcelDataBrushDataKind.Number, false);
        ExcelDataFieldCatalogEntry booleanEntry = FindReadableEntry(entries, ExcelDataBrushDataKind.Boolean, false);
        ExcelDataFieldCatalogEntry referenceEntry = FindReadableEntry(entries, ExcelDataBrushDataKind.ObjectReference, true);
        ExcelDataSerializedValueSnapshot expectedNumber = ReadEntry(numberEntry);
        ExcelDataSerializedValueSnapshot expectedBoolean = ReadEntry(booleanEntry);
        ExcelDataSerializedValueSnapshot expectedReference = ReadEntry(referenceEntry);
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ExcelDataGridAuthoritativeExportSmokeTest.xlsx");
        string colorlessOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ExcelDataGridAuthoritativeExportColorlessSmokeTest.xlsx");
        ExcelDataTransferMasterPreset masterPreset = ScriptableObject.CreateInstance<ExcelDataTransferMasterPreset>();
        ExcelDataWorkbookLayoutPreset layoutPreset = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();
        ExcelDataExportPreset exportPreset = ScriptableObject.CreateInstance<ExcelDataExportPreset>();
        ExcelDataImportPreset importPreset = ScriptableObject.CreateInstance<ExcelDataImportPreset>();
        ExcelDataBrushPalettePreset brushPreset = ScriptableObject.CreateInstance<ExcelDataBrushPalettePreset>();

        ConfigureLayout(layoutPreset, brushPreset, numberEntry, booleanEntry, referenceEntry);
        masterPreset.AssignLinkedPresets(layoutPreset, brushPreset, importPreset, exportPreset);

        try
        {
            ExcelDataExportResult result = ExcelDataExportService.ExportWorkbook(masterPreset, outputPath);
            ValidateResult(result);
            ValidateVisibleSheet(result.WorkbookPath, expectedNumber, expectedBoolean, expectedReference);
            ValidateLayoutPresentation(result.WorkbookPath, true, true);
            ValidateTechnicalSheet(result);
            ConfigureBrushColorExport(exportPreset, false, false);
            ExcelDataExportResult colorlessResult = ExcelDataExportService.ExportWorkbook(masterPreset, colorlessOutputPath);
            ValidateLayoutPresentation(colorlessResult.WorkbookPath, false, false);
            Debug.Log("[ExcelDataGridAuthoritativeExportSmokeTest] PASS - workbook: " + result.WorkbookPath);
        }
        finally
        {
            ScriptableObject.DestroyImmediate(masterPreset);
            ScriptableObject.DestroyImmediate(layoutPreset);
            ScriptableObject.DestroyImmediate(exportPreset);
            ScriptableObject.DestroyImmediate(importPreset);
            ScriptableObject.DestroyImmediate(brushPreset);
        }
    }
    #endregion

    #region Layout Setup
    /// <summary>
    /// Creates one sparse layout containing literal, numeric, boolean, reference and unresolved cells.
    /// </summary>
    /// <param name="layoutPreset">Transient layout receiving the user worksheet.</param>
    /// <param name="brushPreset">Transient palette receiving an exact presentation brush.</param>
    /// <param name="numberEntry">Readable numeric catalog field.</param>
    /// <param name="booleanEntry">Readable boolean catalog field.</param>
    /// <param name="referenceEntry">Readable non-null object-reference catalog field.</param>
    private static void ConfigureLayout(ExcelDataWorkbookLayoutPreset layoutPreset,
                                        ExcelDataBrushPalettePreset brushPreset,
                                        ExcelDataFieldCatalogEntry numberEntry,
                                        ExcelDataFieldCatalogEntry booleanEntry,
                                        ExcelDataFieldCatalogEntry referenceEntry)
    {
        ExcelDataBrushDefinition presentationBrush = CreatePresentationBrush();
        brushPreset.Brushes.Add(presentationBrush);
        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure(DataSheetName,
                        8,
                        8,
                        140,
                        28,
                        true,
                        true,
                        ExcelDataWorkbookSheetVisibility.Visible);
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 1, "Player Label", presentationBrush.BrushId));
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 4, "=A1", presentationBrush.BrushId));
        layoutPreset.SheetDefinitions.Add(sheet);
        ExcelDataWorkbookLayoutAuthoringUtility.PaintDataFieldCell(layoutPreset,
                                                                  layoutPreset.ObjectsSheetName,
                                                                  numberEntry,
                                                                  2,
                                                                  3,
                                                                  ExcelDataTransferDirection.Both,
                                                                  string.Empty,
                                                                  string.Empty);
        ExcelDataWorkbookLayoutAuthoringUtility.PaintDataFieldCell(layoutPreset,
                                                                  layoutPreset.ObjectsSheetName,
                                                                  booleanEntry,
                                                                  3,
                                                                  2,
                                                                  ExcelDataTransferDirection.Both,
                                                                  string.Empty,
                                                                  string.Empty);
        ExcelDataWorkbookLayoutAuthoringUtility.PaintDataFieldCell(layoutPreset,
                                                                  layoutPreset.ObjectsSheetName,
                                                                  referenceEntry,
                                                                  4,
                                                                  6,
                                                                  ExcelDataTransferDirection.Both,
                                                                  string.Empty,
                                                                  string.Empty);
        sheet.Cells.Add(CreateUnresolvedCell(sheet.SheetId, 5, 7));
    }

    /// <summary>
    /// Creates one export-only literal cell.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet identifier.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <param name="text">Exact literal text.</param>
    /// <param name="brushId">Stable exact presentation brush identifier.</param>
    /// <returns>Configured literal cell.</returns>
    private static ExcelDataWorkbookCellDefinition CreateLiteralCell(string sheetId,
                                                                     int rowIndex,
                                                                     int columnIndex,
                                                                     string text,
                                                                     string brushId)
    {
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureLiteralText(sheetId,
                                  rowIndex,
                                  columnIndex,
                                  text,
                                  ExcelDataTransferDirection.Export,
                                  brushId,
                                  false);
        return cell;
    }

    /// <summary>
    /// Creates the exact background and text colors validated in raw Open XML styles.
    /// </summary>
    /// <returns>Configured transient presentation brush.</returns>
    private static ExcelDataBrushDefinition CreatePresentationBrush()
    {
        ExcelDataBrushDefinition brush = new ExcelDataBrushDefinition();
        brush.Configure("Open XML Presentation",
                        ExcelDataTransferDomain.All,
                        ExcelDataBrushDataKind.All,
                        ExcelDataListElementFilterMode.AllBrushableFields,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        ExcelDataTransferDirection.Export,
                        new Color32(0x31, 0x5A, 0x7D, byte.MaxValue),
                        new Color32(0xF1, 0xC2, 0x32, byte.MaxValue),
                        string.Empty,
                        "Smoke brush for exact Open XML background and text colors.");
        return brush;
    }

    /// <summary>
    /// Creates one unresolved field cell to verify blank-coordinate and warning preservation.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet identifier.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <returns>Configured unresolved Data Field cell.</returns>
    private static ExcelDataWorkbookCellDefinition CreateUnresolvedCell(string sheetId,
                                                                        int rowIndex,
                                                                        int columnIndex)
    {
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.ConfigureUnresolved("Smoke:Missing:Field");
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureDataField(sheetId,
                                rowIndex,
                                columnIndex,
                                binding,
                                ExcelDataTransferDirection.Export,
                                "SmokeWarning",
                                string.Empty);
        return cell;
    }
    #endregion

    #region Catalog Selection
    /// <summary>
    /// Finds one readable catalog entry of the requested kind, optionally requiring a non-null asset reference.
    /// </summary>
    /// <param name="entries">Current project field catalog.</param>
    /// <param name="dataKind">Required data kind.</param>
    /// <param name="requireReference">True when reference name and GUID must be present.</param>
    /// <returns>First readable entry satisfying the requested value contract.</returns>
    private static ExcelDataFieldCatalogEntry FindReadableEntry(List<ExcelDataFieldCatalogEntry> entries,
                                                                ExcelDataBrushDataKind dataKind,
                                                                bool requireReference)
    {
        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = entries[entryIndex];

            if (entry == null || entry.DataKind != dataKind)
                continue;

            ExcelDataSerializedValueSnapshot snapshot = ReadEntry(entry);

            if (!string.IsNullOrWhiteSpace(snapshot.Warning) || snapshot.Value == null)
                continue;

            if (requireReference &&
                (string.IsNullOrWhiteSpace(snapshot.ReferenceName) || string.IsNullOrWhiteSpace(snapshot.ReferenceGuid)))
                continue;

            return entry;
        }

        throw new InvalidOperationException("No readable catalog entry found for data kind: " + dataKind);
    }

    /// <summary>
    /// Reads one catalog entry through the same stable binding used by public export.
    /// </summary>
    /// <param name="entry">Catalog entry to read.</param>
    /// <returns>Typed value snapshot with complete reference metadata.</returns>
    private static ExcelDataSerializedValueSnapshot ReadEntry(ExcelDataFieldCatalogEntry entry)
    {
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.ConfigureFromEntry(entry);
        return ExcelDataSerializedValueReader.ReadValue(binding, true, true, true);
    }
    #endregion

    #region Result Validation
    /// <summary>
    /// Validates exact public operation counts and detailed warning coordinates.
    /// </summary>
    /// <param name="result">Public export result.</param>
    private static void ValidateResult(ExcelDataExportResult result)
    {
        if (result == null || result.UserSheetCount != 1)
            throw new InvalidOperationException("Public export result did not report one user worksheet.");

        if (result.AuthoredCellCount != 6 || result.WrittenCellCount != 5)
            throw new InvalidOperationException("Public export result did not preserve authored and written cell counts.");

        if (result.DataFieldCellCount != 4 || result.LiteralCellCount != 2 || result.ReferenceCellCount != 1)
            throw new InvalidOperationException("Public export result reported incorrect cell-kind counts.");

        if (result.WarningCount != 1 || result.Diagnostics.Count != 1)
            throw new InvalidOperationException("Public export result did not report the unresolved field once.");

        ExcelDataExportDiagnostic diagnostic = result.Diagnostics[0];

        if (diagnostic.SheetName != DataSheetName || diagnostic.RowIndex != 5 || diagnostic.ColumnIndex != 7)
            throw new InvalidOperationException("Public export warning lost its exact G5 coordinate.");

        if (string.IsNullOrWhiteSpace(result.LayoutHash) || result.TechnicalRowCount != 9)
            throw new InvalidOperationException("Public export result is missing layout hash or technical row diagnostics.");

        FileInfo workbookFile = new FileInfo(result.WorkbookPath);

        if (!workbookFile.Exists || workbookFile.Length <= 0)
            throw new InvalidOperationException("Public export did not create a valid workbook file.");
    }
    #endregion

    #region Visible Workbook Validation
    /// <summary>
    /// Reads the user sheet without headers and verifies exact coordinates and typed values.
    /// </summary>
    /// <param name="workbookPath">Workbook written by public export.</param>
    /// <param name="expectedNumber">Expected numeric project value.</param>
    /// <param name="expectedBoolean">Expected boolean project value.</param>
    /// <param name="expectedReference">Expected readable reference value.</param>
    private static void ValidateVisibleSheet(string workbookPath,
                                             ExcelDataSerializedValueSnapshot expectedNumber,
                                             ExcelDataSerializedValueSnapshot expectedBoolean,
                                             ExcelDataSerializedValueSnapshot expectedReference)
    {
        List<IDictionary<string, object>> rows = ReadRows(workbookPath, DataSheetName);

        if (rows.Count != 8)
            throw new InvalidOperationException("Visible user sheet does not preserve the complete authored 8-row layout extent.");

        AssertValue(ReadColumn(rows[0], "A"), "Player Label", "A1 literal");
        AssertValue(ReadColumn(rows[0], "B"), null, "B1 empty");
        AssertValue(ReadColumn(rows[0], "D"), "=A1", "D1 formula-like literal");
        AssertNumericValue(ReadColumn(rows[1], "C"), expectedNumber.Value, "C2 number");
        AssertValue(ReadColumn(rows[2], "B"), expectedBoolean.Value, "B3 boolean");
        AssertValue(ReadColumn(rows[3], "F"), expectedReference.Value, "F4 reference name");
        AssertValue(ReadColumn(rows[4], "G"), null, "G5 unresolved blank");
        AssertValue(ReadColumn(rows[7], "H"), null, "H8 formatted empty layout cell");
    }
    #endregion

    #region Presentation Validation
    /// <summary>
    /// Changes the export color toggle through its serialized preset contract.
    /// </summary>
    /// <param name="exportPreset">Transient export preset to configure.</param>
    /// <param name="backgroundEnabled">True to emit brush fills.</param>
    /// <param name="textEnabled">True to emit brush font colors.</param>
    private static void ConfigureBrushColorExport(ExcelDataExportPreset exportPreset,
                                                  bool backgroundEnabled,
                                                  bool textEnabled)
    {
        SerializedObject serializedPreset = new SerializedObject(exportPreset);
        SerializedProperty backgroundToggle = serializedPreset.FindProperty("writeBrushBackgroundColors");
        SerializedProperty textToggle = serializedPreset.FindProperty("writeBrushTextColors");

        if (backgroundToggle == null || textToggle == null)
            throw new InvalidOperationException("Export preset is missing a brush presentation color toggle.");

        backgroundToggle.boolValue = backgroundEnabled;
        textToggle.boolValue = textEnabled;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Verifies complete layout dimensions, empty-cell borders and authored brush fills in raw Open XML.
    /// </summary>
    /// <param name="workbookPath">Workbook written by public export.</param>
    /// <param name="expectBrushFill">True when authored cells must use a brush fill.</param>
    /// <param name="expectBrushTextColor">True when authored cells must use the exact brush text color.</param>
    private static void ValidateLayoutPresentation(string workbookPath,
                                                   bool expectBrushFill,
                                                   bool expectBrushTextColor)
    {
        using (FileStream stream = new FileStream(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
        {
            XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
            Dictionary<string, string> worksheetEntries =
                ExcelDataOpenXmlPackageUtility.BuildWorksheetEntryLookup(archive);
            string worksheetEntryPath;

            if (!worksheetEntries.TryGetValue(DataSheetName, out worksheetEntryPath))
                throw new InvalidOperationException("Presentation validation could not resolve the user worksheet entry.");

            XDocument worksheet = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, worksheetEntryPath);
            XElement dimension = worksheet.Root.Element(spreadsheetNamespace + "dimension");

            if (dimension == null || ReadAttribute(dimension, "ref") != "A1:H8")
                throw new InvalidOperationException("User worksheet dimension is not the complete A1:H8 layout range.");

            XElement literalCell = FindCell(worksheet, "A1", spreadsheetNamespace);
            XElement emptyCell = FindCell(worksheet, "H8", spreadsheetNamespace);
            XDocument styles = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, "xl/styles.xml");
            List<XElement> cellFormats = styles.Root.Element(spreadsheetNamespace + "cellXfs")
                                                      .Elements(spreadsheetNamespace + "xf")
                                                      .ToList();
            List<XElement> fonts = styles.Root.Element(spreadsheetNamespace + "fonts")
                                                .Elements(spreadsheetNamespace + "font")
                                                .ToList();
            List<XElement> fills = styles.Root.Element(spreadsheetNamespace + "fills")
                                                .Elements(spreadsheetNamespace + "fill")
                                                .ToList();
            XElement literalFormat = ResolveCellFormat(literalCell, cellFormats);
            XElement emptyFormat = ResolveCellFormat(emptyCell, cellFormats);

            int literalFillId = ReadIntAttribute(literalFormat, "fillId");
            int literalFontId = ReadIntAttribute(literalFormat, "fontId");
            bool hasExpectedBackgroundColor = literalFillId >= 0 &&
                                              literalFillId < fills.Count &&
                                              HasRgbColor(fills[literalFillId],
                                                          SmokeBrushBackgroundArgb,
                                                          spreadsheetNamespace);

            if (expectBrushFill && (literalFillId <= 1 || !hasExpectedBackgroundColor))
                throw new InvalidOperationException("Authored literal cell did not retain its brush background fill.");

            if (!expectBrushFill && hasExpectedBackgroundColor)
                throw new InvalidOperationException("Authored literal cell retained a brush fill while color export was disabled.");

            bool hasExpectedTextColor = literalFontId >= 0 &&
                                        literalFontId < fonts.Count &&
                                        HasRgbColor(fonts[literalFontId],
                                                    SmokeBrushTextArgb,
                                                    spreadsheetNamespace);

            if (expectBrushTextColor && !hasExpectedTextColor)
                throw new InvalidOperationException("Authored literal cell did not retain its brush text color.");

            if (!expectBrushTextColor && hasExpectedTextColor)
                throw new InvalidOperationException("Authored literal cell retained its brush text color while text-color export was disabled.");

            if (ReadIntAttribute(emptyFormat, "borderId") <= 0)
                throw new InvalidOperationException("Empty H8 layout cell did not receive complete grid borders.");
        }
    }

    /// <summary>
    /// Reports whether one style record contains the exact opaque RGB color requested by a saved brush.
    /// </summary>
    /// <param name="styleRecord">Open XML font or fill record to inspect.</param>
    /// <param name="expectedArgb">Expected eight-digit ARGB color.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>True when the font owns the expected RGB color.</returns>
    private static bool HasRgbColor(XElement styleRecord,
                                    string expectedArgb,
                                    XNamespace spreadsheetNamespace)
    {
        XElement color = styleRecord == null
            ? null
            : styleRecord.Descendants().FirstOrDefault(element =>
                string.Equals(ReadAttribute(element, "rgb"), expectedArgb, StringComparison.OrdinalIgnoreCase));
        return color != null && string.Equals(ReadAttribute(color, "rgb"), expectedArgb, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds one exact cell element in a raw Open XML worksheet.
    /// </summary>
    /// <param name="worksheet">Worksheet document to search.</param>
    /// <param name="address">A1-style cell address.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>Matching cell element.</returns>
    private static XElement FindCell(XDocument worksheet,
                                     string address,
                                     XNamespace spreadsheetNamespace)
    {
        XElement cell = worksheet.Descendants(spreadsheetNamespace + "c")
                                 .FirstOrDefault(candidate => ReadAttribute(candidate, "r") == address);

        if (cell == null)
            throw new InvalidOperationException("Worksheet is missing expected cell: " + address + ".");

        return cell;
    }

    /// <summary>
    /// Resolves the cell-format record referenced by one worksheet cell.
    /// </summary>
    /// <param name="cell">Worksheet cell containing a style index.</param>
    /// <param name="cellFormats">Ordered workbook cell-format records.</param>
    /// <returns>Referenced cell-format element.</returns>
    private static XElement ResolveCellFormat(XElement cell, IReadOnlyList<XElement> cellFormats)
    {
        int styleIndex = ReadIntAttribute(cell, "s");

        if (styleIndex < 0 || styleIndex >= cellFormats.Count)
            throw new InvalidOperationException("Worksheet cell references an invalid style index: " + styleIndex + ".");

        return cellFormats[styleIndex];
    }

    /// <summary>
    /// Reads one required integer attribute using zero as the Open XML default.
    /// </summary>
    /// <param name="element">Element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>Parsed integer value or zero.</returns>
    private static int ReadIntAttribute(XElement element, string attributeName)
    {
        int parsedValue;
        return int.TryParse(ReadAttribute(element, attributeName),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out parsedValue)
            ? parsedValue
            : 0;
    }

    /// <summary>
    /// Reads one unqualified XML attribute.
    /// </summary>
    /// <param name="element">Element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>Attribute text or an empty string.</returns>
    private static string ReadAttribute(XElement element, string attributeName)
    {
        XAttribute attribute = element == null ? null : element.Attribute(attributeName);
        return attribute == null ? string.Empty : attribute.Value;
    }
    #endregion

    #region Technical Workbook Validation
    /// <summary>
    /// Verifies the reserved sheet state, schema records, hash, reference GUID and warning cell.
    /// </summary>
    /// <param name="result">Public export result containing path and expected layout hash.</param>
    private static void ValidateTechnicalSheet(ExcelDataExportResult result)
    {
        List<SheetInfo> sheetInformations = MiniExcel.GetSheetInformations(result.WorkbookPath, new OpenXmlConfiguration());

        if (sheetInformations.Count != 2)
            throw new InvalidOperationException("Public export did not produce one user sheet plus one technical sheet.");

        bool foundTechnicalSheet = false;

        // Locate the reserved sheet without relying on workbook tab order.
        for (int sheetIndex = 0; sheetIndex < sheetInformations.Count; sheetIndex++)
        {
            SheetInfo sheetInformation = sheetInformations[sheetIndex];

            if (sheetInformation.Name != ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName)
                continue;

            if (sheetInformation.State != SheetState.VeryHidden)
                throw new InvalidOperationException("Reserved technical worksheet is not VeryHidden.");

            foundTechnicalSheet = true;
            break;
        }

        if (!foundTechnicalSheet)
            throw new InvalidOperationException("Public export is missing the reserved technical worksheet.");

        List<IDictionary<string, object>> rows =
            ReadRows(result.WorkbookPath, ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName);

        if (rows.Count != result.TechnicalRowCount)
            throw new InvalidOperationException("Technical worksheet row count differs from the public export result.");

        AssertValue(ReadColumn(rows[0], "A"), "RecordType", "technical header");
        AssertValue(ReadColumn(rows[1], "A"), "Workbook", "technical workbook record");
        AssertValue(ReadColumn(rows[1], "I"), result.LayoutHash, "technical layout hash");
        AssertValue(ReadColumn(rows[2], "A"), "Sheet", "technical sheet record");

        if (string.IsNullOrWhiteSpace(Convert.ToString(ReadColumn(rows[7], "AP"), CultureInfo.InvariantCulture)))
            throw new InvalidOperationException("Reference cell technical record is missing its GUID.");

        if (string.IsNullOrWhiteSpace(Convert.ToString(ReadColumn(rows[8], "AR"), CultureInfo.InvariantCulture)))
            throw new InvalidOperationException("Unresolved G5 technical record is missing its warning.");
    }
    #endregion

    #region Workbook Helpers
    /// <summary>
    /// Materializes one raw MiniExcel worksheet as rows keyed by Excel column letters.
    /// </summary>
    /// <param name="workbookPath">Workbook path to query.</param>
    /// <param name="sheetName">Worksheet name to query.</param>
    /// <returns>Materialized raw worksheet rows.</returns>
    private static List<IDictionary<string, object>> ReadRows(string workbookPath, string sheetName)
    {
        IEnumerable<object> queriedRows = MiniExcel.Query(workbookPath, false, sheetName, ExcelType.XLSX, "A1", null);
        List<IDictionary<string, object>> rows = new List<IDictionary<string, object>>();

        foreach (object queriedRow in queriedRows)
        {
            IDictionary<string, object> row = queriedRow as IDictionary<string, object>;

            if (row != null)
                rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Reads one raw MiniExcel column value while tolerating omitted empty keys.
    /// </summary>
    /// <param name="row">Raw workbook row keyed by Excel column letter.</param>
    /// <param name="columnName">Excel column letter.</param>
    /// <returns>Stored value or null when the cell is empty.</returns>
    private static object ReadColumn(IDictionary<string, object> row, string columnName)
    {
        object value;
        return row != null && row.TryGetValue(columnName, out value) ? value : null;
    }

    /// <summary>
    /// Compares one workbook scalar with its expected value.
    /// </summary>
    /// <param name="actual">Actual workbook value.</param>
    /// <param name="expected">Expected scalar value.</param>
    /// <param name="label">Assertion label.</param>
    private static void AssertValue(object actual, object expected, string label)
    {
        if (Equals(actual, expected))
            return;

        throw new InvalidOperationException(label + " mismatch. Expected: " + expected + ", actual: " + actual + ".");
    }

    /// <summary>
    /// Compares one workbook number through invariant double conversion.
    /// </summary>
    /// <param name="actual">Actual workbook numeric value.</param>
    /// <param name="expected">Expected project numeric value.</param>
    /// <param name="label">Assertion label.</param>
    private static void AssertNumericValue(object actual, object expected, string label)
    {
        if (actual == null || expected == null)
            throw new InvalidOperationException(label + " cannot compare null numeric values.");

        double actualNumber = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
        double expectedNumber = Convert.ToDouble(expected, CultureInfo.InvariantCulture);

        if (Math.Abs(actualNumber - expectedNumber) <= 0.000001d)
            return;

        throw new InvalidOperationException(label + " mismatch. Expected: " + expectedNumber + ", actual: " + actualNumber + ".");
    }
    #endregion

    #endregion
}
