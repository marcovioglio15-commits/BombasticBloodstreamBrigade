using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Reads formula expressions, persisted results and workbook calculation flags directly from OpenXML parts.
/// </summary>
internal static class ExcelDataWorkbookFormulaReader
{
    #region Constants
    private const string WorkbookEntryPath = "xl/workbook.xml";
    private const string SharedStringsEntryPath = "xl/sharedStrings.xml";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads formula metadata only for exact import-enabled layout coordinates.
    /// </summary>
    /// <param name="workbookPath">Absolute source workbook path.</param>
    /// <param name="layoutPreset">Grid-authoritative layout defining requested coordinates.</param>
    /// <returns>Formula lookup and workbook calculation metadata without evaluating expressions.</returns>
    public static ExcelDataWorkbookFormulaReadResult Read(string workbookPath,
                                                          ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        using (FileStream stream = new FileStream(workbookPath,
                                                  FileMode.Open,
                                                  FileAccess.Read,
                                                  FileShare.ReadWrite))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
        {
            Dictionary<string, string> worksheetEntries =
                ExcelDataOpenXmlPackageUtility.BuildWorksheetEntryLookup(archive);
            List<string> sharedStrings = ReadSharedStrings(archive);
            ExcelDataWorkbookFormulaReadResult result =
                new ExcelDataWorkbookFormulaReadResult(ReadCalculationMetadata(archive));
            List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

            // Read only worksheets and coordinates that can participate in import preview.
            for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
            {
                ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

                if (sheet == null || !sheet.ImportEnabled ||
                    !ExcelDataWorkbookImportCellUtility.ContainsImportCells(sheet))
                    continue;

                string sheetName = ExcelDataWorkbookPathUtility.SanitizeSheetName(
                    sheet.SheetName,
                    "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));
                string worksheetEntryPath;

                if (!worksheetEntries.TryGetValue(sheetName, out worksheetEntryPath))
                    continue;

                ReadWorksheetFormulas(archive,
                                      worksheetEntryPath,
                                      sheet,
                                      sharedStrings,
                                      result);
            }

            return result;
        }
    }
    #endregion

    #region Workbook Metadata
    /// <summary>
    /// Reads calculation mode and pending full-calculation flags from workbook.xml.
    /// </summary>
    /// <param name="archive">Open workbook package.</param>
    /// <returns>Calculation metadata used to assess cached-result freshness.</returns>
    private static ExcelDataWorkbookCalculationMetadata ReadCalculationMetadata(ZipArchive archive)
    {
        XDocument workbook = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, WorkbookEntryPath);
        XElement calculationProperties =
            workbook.Root == null
                ? null
                : workbook.Root.Element(ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace + "calcPr");

        if (calculationProperties == null)
            return new ExcelDataWorkbookCalculationMetadata("auto", false, false);

        string calculationMode = ReadAttribute(calculationProperties, "calcMode");
        bool manualCalculation = string.Equals(calculationMode, "manual", StringComparison.OrdinalIgnoreCase);
        bool fullCalculationRequired = ReadBooleanAttribute(calculationProperties, "fullCalcOnLoad") ||
                                       ReadBooleanAttribute(calculationProperties, "forceFullCalc");
        return new ExcelDataWorkbookCalculationMetadata(
            string.IsNullOrWhiteSpace(calculationMode) ? "auto" : calculationMode,
            manualCalculation,
            fullCalculationRequired);
    }

    /// <summary>
    /// Reads one OpenXML Boolean attribute accepting numeric and textual representations.
    /// </summary>
    /// <param name="element">Element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>True only when the attribute explicitly stores a true value.</returns>
    private static bool ReadBooleanAttribute(XElement element, string attributeName)
    {
        string value = ReadAttribute(element, attributeName);
        return string.Equals(value, "1", StringComparison.Ordinal) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region Worksheet Reading
    /// <summary>
    /// Reads mapped formula cells from one worksheet XML part.
    /// </summary>
    /// <param name="archive">Open workbook package.</param>
    /// <param name="worksheetEntryPath">Resolved worksheet ZIP path.</param>
    /// <param name="sheetDefinition">Authoritative sheet definition.</param>
    /// <param name="sharedStrings">Workbook shared-string table.</param>
    /// <param name="result">Formula result receiving mapped records.</param>
    private static void ReadWorksheetFormulas(ZipArchive archive,
                                              string worksheetEntryPath,
                                              ExcelDataWorkbookSheetDefinition sheetDefinition,
                                              List<string> sharedStrings,
                                              ExcelDataWorkbookFormulaReadResult result)
    {
        XDocument worksheet = ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, worksheetEntryPath);
        HashSet<long> requestedCoordinates =
            ExcelDataWorkbookImportCellUtility.BuildRequestedCoordinateKeys(sheetDefinition);
        XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;

        // Formula nodes are sparse, so scan materialized cells and retain only requested coordinates.
        foreach (XElement cell in worksheet.Descendants(spreadsheetNamespace + "c"))
        {
            XElement formula = cell.Element(spreadsheetNamespace + "f");

            if (formula == null)
                continue;

            int rowIndex;
            int columnIndex;

            if (!ExcelDataWorkbookCoordinateUtility.TryParseAddress(ReadAttribute(cell, "r"),
                                                                    out rowIndex,
                                                                    out columnIndex) ||
                !requestedCoordinates.Contains(ExcelDataWorkbookCoordinateUtility.BuildKey(rowIndex,
                                                                                            columnIndex)))
                continue;

            result.RegisterFormula(sheetDefinition.SheetId,
                                   rowIndex,
                                   columnIndex,
                                   BuildFormulaCell(cell, formula, sharedStrings));
        }
    }

    /// <summary>
    /// Builds one immutable formula record and parses its persisted scalar result.
    /// </summary>
    /// <param name="cell">OpenXML worksheet cell.</param>
    /// <param name="formula">Formula child element.</param>
    /// <param name="sharedStrings">Workbook shared-string table.</param>
    /// <returns>Formula expression and cached-result metadata.</returns>
    private static ExcelDataWorkbookFormulaCell BuildFormulaCell(XElement cell,
                                                                  XElement formula,
                                                                  List<string> sharedStrings)
    {
        XNamespace spreadsheetNamespace = ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace;
        string dataType = ReadAttribute(cell, "t");
        XElement valueElement = cell.Element(spreadsheetNamespace + "v");
        XElement inlineStringElement = cell.Element(spreadsheetNamespace + "is");
        bool hasCachedResult = valueElement != null || inlineStringElement != null;
        string rawCachedValue = valueElement == null
            ? ReadInlineString(inlineStringElement)
            : valueElement.Value;
        bool cachedError = string.Equals(dataType, "e", StringComparison.OrdinalIgnoreCase);
        object cachedValue = null;
        string unsupportedReason = string.Empty;
        bool cachedResultSupported = hasCachedResult &&
                                     !cachedError &&
                                     TryParseCachedValue(dataType,
                                                         rawCachedValue,
                                                         sharedStrings,
                                                         out cachedValue,
                                                         out unsupportedReason);

        if (!cachedResultSupported)
            cachedValue = cachedError ? rawCachedValue : null;

        return new ExcelDataWorkbookFormulaCell(formula.Value,
                                                ReadAttribute(formula, "t"),
                                                ReadAttribute(formula, "si"),
                                                dataType,
                                                hasCachedResult,
                                                cachedResultSupported,
                                                cachedError,
                                                rawCachedValue,
                                                cachedValue,
                                                unsupportedReason);
    }

    /// <summary>
    /// Converts one persisted formula result into an invariant scalar supported by Unity property writers.
    /// </summary>
    /// <param name="dataType">OpenXML cell data type token.</param>
    /// <param name="rawValue">Raw cached value text.</param>
    /// <param name="sharedStrings">Workbook shared-string table.</param>
    /// <param name="cachedValue">Parsed scalar value.</param>
    /// <param name="unsupportedReason">Diagnostic when the cached representation cannot be parsed.</param>
    /// <returns>True when the cached result is a supported scalar.</returns>
    private static bool TryParseCachedValue(string dataType,
                                            string rawValue,
                                            List<string> sharedStrings,
                                            out object cachedValue,
                                            out string unsupportedReason)
    {
        cachedValue = null;
        unsupportedReason = string.Empty;

        switch (dataType)
        {
            case "b":
                if (rawValue == "1" || string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    cachedValue = true;
                    return true;
                }

                if (rawValue == "0" || string.Equals(rawValue, "false", StringComparison.OrdinalIgnoreCase))
                {
                    cachedValue = false;
                    return true;
                }

                unsupportedReason = "Cached Boolean result is not 0, 1, true or false.";
                return false;
            case "str":
            case "inlineStr":
                cachedValue = rawValue;
                return true;
            case "s":
                int sharedStringIndex;

                if (int.TryParse(rawValue,
                                 NumberStyles.Integer,
                                 CultureInfo.InvariantCulture,
                                 out sharedStringIndex) &&
                    sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
                {
                    cachedValue = sharedStrings[sharedStringIndex];
                    return true;
                }

                unsupportedReason = "Cached shared-string result references an invalid string-table index.";
                return false;
            case "d":
                DateTime dateValue;

                if (DateTime.TryParse(rawValue,
                                      CultureInfo.InvariantCulture,
                                      DateTimeStyles.RoundtripKind,
                                      out dateValue))
                {
                    cachedValue = dateValue;
                    return true;
                }

                unsupportedReason = "Cached date result is not an ISO-8601 value.";
                return false;
            default:
                double numericValue;

                if (double.TryParse(rawValue,
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out numericValue))
                {
                    cachedValue = numericValue;
                    return true;
                }

                unsupportedReason = string.IsNullOrEmpty(rawValue)
                    ? "Formula cell has no persisted scalar result."
                    : "Cached numeric result is not a valid invariant number.";
                return false;
        }
    }
    #endregion

    #region Shared Strings
    /// <summary>
    /// Reads the optional shared-string table used by uncommon formula-result encodings.
    /// </summary>
    /// <param name="archive">Open workbook package.</param>
    /// <returns>Shared strings in workbook index order.</returns>
    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        List<string> sharedStrings = new List<string>();
        ZipArchiveEntry entry = archive.GetEntry(SharedStringsEntryPath);

        if (entry == null)
            return sharedStrings;

        using (Stream stream = entry.Open())
        {
            XDocument document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);

            foreach (XElement item in document.Descendants(ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace + "si"))
                sharedStrings.Add(ReadInlineString(item));
        }

        return sharedStrings;
    }

    /// <summary>
    /// Concatenates plain and rich-text runs from one inline or shared string element.
    /// </summary>
    /// <param name="stringElement">Inline-string or shared-string container.</param>
    /// <returns>Combined visible text.</returns>
    private static string ReadInlineString(XElement stringElement)
    {
        if (stringElement == null)
            return string.Empty;

        StringBuilder text = new StringBuilder();

        foreach (XElement textElement in stringElement.Descendants(ExcelDataOpenXmlPackageUtility.SpreadsheetNamespace + "t"))
            text.Append(textElement.Value);

        return text.ToString();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Reads one unqualified XML attribute as text.
    /// </summary>
    /// <param name="element">Element containing the attribute.</param>
    /// <param name="attributeName">Unqualified attribute name.</param>
    /// <returns>Attribute text, or an empty string.</returns>
    private static string ReadAttribute(XElement element, string attributeName)
    {
        XAttribute attribute = element == null ? null : element.Attribute(attributeName);
        return attribute == null ? string.Empty : attribute.Value;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores workbook-level flags that can make persisted formula results stale.
/// </summary>
internal sealed class ExcelDataWorkbookCalculationMetadata
{
    #region Properties
    public string CalculationMode { get; }
    public bool ManualCalculation { get; }
    public bool FullCalculationRequired { get; }
    public bool PotentiallyStale
    {
        get
        {
            return ManualCalculation || FullCalculationRequired;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates immutable workbook calculation metadata.
    /// </summary>
    /// <param name="calculationMode">OpenXML calculation mode token.</param>
    /// <param name="manualCalculation">True when automatic recalculation is disabled.</param>
    /// <param name="fullCalculationRequired">True when the workbook requests a full recalculation.</param>
    public ExcelDataWorkbookCalculationMetadata(string calculationMode,
                                                bool manualCalculation,
                                                bool fullCalculationRequired)
    {
        CalculationMode = calculationMode ?? string.Empty;
        ManualCalculation = manualCalculation;
        FullCalculationRequired = fullCalculationRequired;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Builds a concrete freshness warning for cached formula results.
    /// </summary>
    /// <returns>Empty text for trusted calculation settings, otherwise the stale-cache reason.</returns>
    public string BuildStaleReason()
    {
        if (ManualCalculation && FullCalculationRequired)
            return "Workbook calculation is Manual and requests a full recalculation.";

        if (ManualCalculation)
            return "Workbook calculation mode is Manual.";

        return FullCalculationRequired
            ? "Workbook requests a full recalculation before cached formula results are trusted."
            : string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one mapped formula expression and its persisted OpenXML result.
/// </summary>
internal sealed class ExcelDataWorkbookFormulaCell
{
    #region Properties
    public string Expression { get; }
    public string FormulaType { get; }
    public string SharedIndex { get; }
    public string CachedDataType { get; }
    public bool HasCachedResult { get; }
    public bool CachedResultSupported { get; }
    public bool CachedError { get; }
    public string RawCachedValue { get; }
    public object CachedValue { get; }
    public string UnsupportedReason { get; }
    public string DisplayExpression
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Expression))
                return "=" + Expression;

            return string.IsNullOrWhiteSpace(SharedIndex)
                ? "[formula result]"
                : "[shared formula " + SharedIndex + " result]";
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates immutable formula and cached-result metadata for one cell.
    /// </summary>
    /// <param name="expression">OpenXML formula expression without a leading equals sign.</param>
    /// <param name="formulaType">OpenXML formula type such as shared or array.</param>
    /// <param name="sharedIndex">Optional shared-formula identity.</param>
    /// <param name="cachedDataType">OpenXML cached-result data type.</param>
    /// <param name="hasCachedResult">True when a persisted result element exists.</param>
    /// <param name="cachedResultSupported">True when the persisted result was parsed into a supported scalar.</param>
    /// <param name="cachedError">True when the persisted result is an Excel error.</param>
    /// <param name="rawCachedValue">Raw persisted result text.</param>
    /// <param name="cachedValue">Parsed persisted scalar.</param>
    /// <param name="unsupportedReason">Diagnostic for unsupported cached representations.</param>
    public ExcelDataWorkbookFormulaCell(string expression,
                                        string formulaType,
                                        string sharedIndex,
                                        string cachedDataType,
                                        bool hasCachedResult,
                                        bool cachedResultSupported,
                                        bool cachedError,
                                        string rawCachedValue,
                                        object cachedValue,
                                        string unsupportedReason)
    {
        Expression = expression ?? string.Empty;
        FormulaType = formulaType ?? string.Empty;
        SharedIndex = sharedIndex ?? string.Empty;
        CachedDataType = cachedDataType ?? string.Empty;
        HasCachedResult = hasCachedResult;
        CachedResultSupported = cachedResultSupported;
        CachedError = cachedError;
        RawCachedValue = rawCachedValue ?? string.Empty;
        CachedValue = cachedValue;
        UnsupportedReason = unsupportedReason ?? string.Empty;
    }
    #endregion

    #endregion
}

/// <summary>
/// Provides coordinate lookup for mapped formulas and workbook calculation metadata.
/// </summary>
internal sealed class ExcelDataWorkbookFormulaReadResult
{
    #region Fields
    private readonly Dictionary<string, Dictionary<long, ExcelDataWorkbookFormulaCell>> formulasBySheetId =
        new Dictionary<string, Dictionary<long, ExcelDataWorkbookFormulaCell>>(StringComparer.Ordinal);
    #endregion

    #region Properties
    public ExcelDataWorkbookCalculationMetadata CalculationMetadata { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an empty formula lookup for one workbook calculation state.
    /// </summary>
    /// <param name="calculationMetadata">Workbook calculation flags.</param>
    public ExcelDataWorkbookFormulaReadResult(ExcelDataWorkbookCalculationMetadata calculationMetadata)
    {
        CalculationMetadata = calculationMetadata;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Registers one mapped formula by stable sheet identity and exact coordinate.
    /// </summary>
    /// <param name="sheetId">Stable layout worksheet identity.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="formula">Parsed formula metadata.</param>
    public void RegisterFormula(string sheetId,
                                int rowIndex,
                                int columnIndex,
                                ExcelDataWorkbookFormulaCell formula)
    {
        Dictionary<long, ExcelDataWorkbookFormulaCell> formulas;

        if (!formulasBySheetId.TryGetValue(sheetId, out formulas))
        {
            formulas = new Dictionary<long, ExcelDataWorkbookFormulaCell>();
            formulasBySheetId.Add(sheetId, formulas);
        }

        formulas[ExcelDataWorkbookCoordinateUtility.BuildKey(rowIndex, columnIndex)] = formula;
    }

    /// <summary>
    /// Finds mapped formula metadata at one exact layout coordinate.
    /// </summary>
    /// <param name="sheetId">Stable layout worksheet identity.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <returns>Formula metadata, or null when the cell is a literal scalar.</returns>
    public ExcelDataWorkbookFormulaCell FindFormula(string sheetId, int rowIndex, int columnIndex)
    {
        Dictionary<long, ExcelDataWorkbookFormulaCell> formulas;
        ExcelDataWorkbookFormulaCell formula;

        if (!formulasBySheetId.TryGetValue(sheetId, out formulas))
            return null;

        return formulas.TryGetValue(ExcelDataWorkbookCoordinateUtility.BuildKey(rowIndex, columnIndex), out formula)
            ? formula
            : null;
    }
    #endregion

    #endregion
}
