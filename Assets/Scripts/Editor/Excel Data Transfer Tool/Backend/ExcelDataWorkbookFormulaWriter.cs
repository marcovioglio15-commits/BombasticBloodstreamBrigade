using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

/// <summary>
/// Replaces exported formula placeholders with native SpreadsheetML formula elements and requests recalculation.
/// </summary>
internal static class ExcelDataWorkbookFormulaWriter
{
    #region Constants
    private const string WorkbookEntryPath = "xl/workbook.xml";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Materializes every formula recorded by the workbook document after MiniExcel creates the package.
    /// </summary>
    /// <param name="workbookPath">Absolute exported .xlsx path.</param>
    /// <param name="document">Workbook document containing normalized formula coordinates.</param>
    public static void Apply(string workbookPath, ExcelDataWorkbookDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (!ContainsFormulas(document))
            return;

        using (FileStream stream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, false))
        {
            Dictionary<string, string> worksheetEntries =
                ExcelDataOpenXmlPackageUtility.BuildWorksheetEntryLookup(archive);

            // Replace formula placeholders only in worksheets that own explicit formula records.
            for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
            {
                ExcelDataWorkbookSheetDocument sheet = document.Sheets[sheetIndex];

                if (sheet.FormulaCells.Count <= 0)
                    continue;

                string sheetName = ExcelDataWorkbookPathUtility.SanitizeSheetName(
                    sheet.SheetName,
                    "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));
                string worksheetEntryPath;

                if (!worksheetEntries.TryGetValue(sheetName, out worksheetEntryPath))
                    throw new InvalidDataException("Open XML workbook is missing formula worksheet: " + sheetName);

                WriteWorksheetFormulas(archive, worksheetEntryPath, sheet.FormulaCells);
            }

            RequestAutomaticRecalculation(archive);
        }
    }
    #endregion

    #region Worksheet Writing
    /// <summary>
    /// Replaces the scalar payload at each recorded coordinate with one native formula element.
    /// </summary>
    /// <param name="archive">Open update-mode workbook package.</param>
    /// <param name="entryPath">Worksheet ZIP entry path.</param>
    /// <param name="formulaCells">Normalized formula coordinates owned by the worksheet.</param>
    private static void WriteWorksheetFormulas(ZipArchive archive,
                                               string entryPath,
                                               IReadOnlyList<ExcelDataWorkbookFormulaDocumentCell> formulaCells)
    {
        XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
        XDocument worksheet = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, entryPath);
        XElement sheetData = worksheet.Root == null
            ? null
            : worksheet.Root.Element(spreadsheetNamespace + "sheetData");

        if (sheetData == null)
            throw new InvalidDataException("Open XML formula worksheet has no sheetData element: " + entryPath);

        Dictionary<string, XElement> cellsByAddress = BuildCellLookup(sheetData, spreadsheetNamespace);

        // MiniExcel materializes every non-empty placeholder, so a missing address indicates package corruption.
        for (int formulaIndex = 0; formulaIndex < formulaCells.Count; formulaIndex++)
        {
            ExcelDataWorkbookFormulaDocumentCell formulaCell = formulaCells[formulaIndex];
            string address = ExcelDataWorkbookCoordinateUtility.BuildAddress(formulaCell.RowIndex,
                                                                              formulaCell.ColumnIndex);
            XElement cell;

            if (!cellsByAddress.TryGetValue(address, out cell))
                throw new InvalidDataException("Open XML formula cell is missing at " + address + ".");

            cell.SetAttributeValue("t", null);
            cell.Elements(spreadsheetNamespace + "f").Remove();
            cell.Elements(spreadsheetNamespace + "v").Remove();
            cell.Elements(spreadsheetNamespace + "is").Remove();
            cell.Add(new XElement(spreadsheetNamespace + "f", formulaCell.Expression));
        }

        ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive, entryPath, worksheet);
    }

    /// <summary>
    /// Indexes materialized worksheet cells by exact uppercase address.
    /// </summary>
    /// <param name="sheetData">Worksheet sheetData element.</param>
    /// <param name="spreadsheetNamespace">SpreadsheetML namespace.</param>
    /// <returns>Cell elements keyed by their exact address.</returns>
    private static Dictionary<string, XElement> BuildCellLookup(XElement sheetData,
                                                                 XNamespace spreadsheetNamespace)
    {
        Dictionary<string, XElement> cellsByAddress = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

        // Index explicit cell references without relying on sparse XML row order.
        foreach (XElement row in sheetData.Elements(spreadsheetNamespace + "row"))
        {
            foreach (XElement cell in row.Elements(spreadsheetNamespace + "c"))
            {
                XAttribute address = cell.Attribute("r");

                if (address != null && !string.IsNullOrWhiteSpace(address.Value))
                    cellsByAddress[address.Value] = cell;
            }
        }

        return cellsByAddress;
    }
    #endregion

    #region Calculation Policy
    /// <summary>
    /// Marks the workbook for automatic full recalculation because Unity does not evaluate Excel expressions.
    /// </summary>
    /// <param name="archive">Open update-mode workbook package.</param>
    private static void RequestAutomaticRecalculation(ZipArchive archive)
    {
        XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
        XDocument workbook = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, WorkbookEntryPath);
        XElement workbookRoot = workbook.Root;

        if (workbookRoot == null)
            throw new InvalidDataException("Open XML workbook has no root element.");

        XElement calculationProperties = workbookRoot.Element(spreadsheetNamespace + "calcPr");

        if (calculationProperties == null)
        {
            calculationProperties = new XElement(spreadsheetNamespace + "calcPr");
            XElement extensions = workbookRoot.Element(spreadsheetNamespace + "extLst");

            if (extensions == null)
                workbookRoot.Add(calculationProperties);
            else
                extensions.AddBeforeSelf(calculationProperties);
        }

        calculationProperties.SetAttributeValue("calcMode", "auto");
        calculationProperties.SetAttributeValue("fullCalcOnLoad", "1");
        calculationProperties.SetAttributeValue("forceFullCalc", "1");
        ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive, WorkbookEntryPath, workbook);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Reports whether any worksheet owns formula records, avoiding package access for scalar-only workbooks.
    /// </summary>
    /// <param name="document">Workbook document to inspect.</param>
    /// <returns>True when at least one native formula must be written.</returns>
    private static bool ContainsFormulas(ExcelDataWorkbookDocument document)
    {
        for (int sheetIndex = 0; sheetIndex < document.Sheets.Count; sheetIndex++)
        {
            if (document.Sheets[sheetIndex].FormulaCells.Count > 0)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
