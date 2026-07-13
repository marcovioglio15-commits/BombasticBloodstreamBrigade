using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

/// <summary>
/// Reads grid-authoritative workbook cells and reserved round-trip metadata without assuming header rows.
/// </summary>
internal static class ExcelDataGridWorkbookReader
{
    #region Constants
    private const string WorkbookRecordType = "Workbook";
    private const string CellRecordType = "Cell";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads every import-enabled layout coordinate and the reserved technical worksheet from one workbook.
    /// </summary>
    /// <param name="workbookPath">Resolved workbook path.</param>
    /// <param name="layoutPreset">Active grid-authoritative layout defining exact coordinates.</param>
    /// <returns>Raw cell values, technical metadata, missing sheets and the source timestamp.</returns>
    public static ExcelDataGridWorkbookReadResult ReadWorkbook(string workbookPath,
                                                               ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        if (!File.Exists(workbookPath))
            throw new FileNotFoundException("Import workbook was not found.", workbookPath);

        List<SheetInfo> sheetInformations = MiniExcel.GetSheetInformations(workbookPath, new OpenXmlConfiguration());
        HashSet<string> workbookSheetNames = BuildSheetNameSet(sheetInformations);
        ExcelDataWorkbookTechnicalMetadata technicalMetadata = ReadTechnicalMetadata(workbookPath, workbookSheetNames);
        ExcelDataGridWorkbookReadResult result =
            new ExcelDataGridWorkbookReadResult(technicalMetadata, File.GetLastWriteTimeUtc(workbookPath).Ticks);
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        // Read each authored user sheet once while retaining only requested import coordinates.
        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet == null || !sheet.ImportEnabled || !ContainsImportCells(sheet))
                continue;

            string workbookSheetName =
                ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));

            if (!workbookSheetNames.Contains(workbookSheetName))
            {
                result.RegisterMissingSheet(workbookSheetName);
                continue;
            }

            ReadSheetValues(workbookPath, workbookSheetName, sheet, result);
        }

        return result;
    }
    #endregion

    #region Sheet Reading
    /// <summary>
    /// Reads one raw worksheet and records values only for the exact authored import coordinates.
    /// </summary>
    /// <param name="workbookPath">Workbook path passed to MiniExcel.</param>
    /// <param name="workbookSheetName">Sanitized visible worksheet name.</param>
    /// <param name="sheetDefinition">Authored worksheet definition.</param>
    /// <param name="result">Read result receiving raw values.</param>
    private static void ReadSheetValues(string workbookPath,
                                        string workbookSheetName,
                                        ExcelDataWorkbookSheetDefinition sheetDefinition,
                                        ExcelDataGridWorkbookReadResult result)
    {
        Dictionary<int, List<int>> requestedColumnsByRow = BuildRequestedColumnsByRow(sheetDefinition);
        OpenXmlConfiguration configuration = new OpenXmlConfiguration();
        configuration.IgnoreEmptyRows = false;
        IEnumerable<object> queriedRows =
            MiniExcel.Query(workbookPath, false, workbookSheetName, ExcelType.XLSX, "A1", configuration);
        int rowIndex = 0;

        // MiniExcel keeps empty rows when requested, allowing enumeration index to remain the Excel row index.
        foreach (object queriedRow in queriedRows)
        {
            rowIndex++;
            List<int> requestedColumns;

            if (!requestedColumnsByRow.TryGetValue(rowIndex, out requestedColumns))
                continue;

            IDictionary<string, object> row = queriedRow as IDictionary<string, object>;

            for (int columnPosition = 0; columnPosition < requestedColumns.Count; columnPosition++)
            {
                int columnIndex = requestedColumns[columnPosition];
                object value = ReadColumn(row, ExcelDataWorkbookCoordinateUtility.ColumnIndexToName(columnIndex));
                result.RegisterValue(sheetDefinition.SheetId, rowIndex, columnIndex, value);
            }
        }

        // Explicitly register trailing or physically omitted empty cells as null values.
        foreach (KeyValuePair<int, List<int>> requestedRow in requestedColumnsByRow)
        {
            for (int columnPosition = 0; columnPosition < requestedRow.Value.Count; columnPosition++)
            {
                int columnIndex = requestedRow.Value[columnPosition];

                if (!result.ContainsValue(sheetDefinition.SheetId, requestedRow.Key, columnIndex))
                    result.RegisterValue(sheetDefinition.SheetId, requestedRow.Key, columnIndex, null);
            }
        }
    }

    /// <summary>
    /// Groups import-enabled authored columns by their one-based worksheet row.
    /// </summary>
    /// <param name="sheetDefinition">Worksheet whose sparse cells are inspected.</param>
    /// <returns>Requested column indexes grouped by row.</returns>
    private static Dictionary<int, List<int>> BuildRequestedColumnsByRow(ExcelDataWorkbookSheetDefinition sheetDefinition)
    {
        Dictionary<int, List<int>> columnsByRow = new Dictionary<int, List<int>>();
        List<ExcelDataWorkbookCellDefinition> cells = sheetDefinition.Cells;

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

            if (!IncludesImportRead(cell))
                continue;

            List<int> columns;

            if (!columnsByRow.TryGetValue(cell.RowIndex, out columns))
            {
                columns = new List<int>();
                columnsByRow.Add(cell.RowIndex, columns);
            }

            if (!columns.Contains(cell.ColumnIndex))
                columns.Add(cell.ColumnIndex);
        }

        return columnsByRow;
    }

    /// <summary>
    /// Reports whether a sheet contains any coordinate needed by import preview.
    /// </summary>
    /// <param name="sheetDefinition">Worksheet definition to inspect.</param>
    /// <returns>True when at least one Data Field or validated literal participates in import.</returns>
    private static bool ContainsImportCells(ExcelDataWorkbookSheetDefinition sheetDefinition)
    {
        List<ExcelDataWorkbookCellDefinition> cells = sheetDefinition.Cells;

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            if (IncludesImportRead(cells[cellIndex]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reports whether one cell must be read for data import or literal validation.
    /// </summary>
    /// <param name="cell">Authored cell definition.</param>
    /// <returns>True when preview needs the exact worksheet value.</returns>
    private static bool IncludesImportRead(ExcelDataWorkbookCellDefinition cell)
    {
        if (cell == null || !cell.IncludesImport() || cell.RowIndex < 1 || cell.ColumnIndex < 1)
            return false;

        return cell.ContentKind == ExcelDataWorkbookCellContentKind.DataField || cell.ValidateLiteralDuringImport;
    }
    #endregion

    #region Technical Metadata
    /// <summary>
    /// Reads schema, layout identity and per-cell reference metadata from the reserved worksheet.
    /// </summary>
    /// <param name="workbookPath">Workbook path passed to MiniExcel.</param>
    /// <param name="workbookSheetNames">Case-insensitive workbook sheet-name set.</param>
    /// <returns>Parsed technical metadata, or an empty result when the sheet is absent.</returns>
    private static ExcelDataWorkbookTechnicalMetadata ReadTechnicalMetadata(string workbookPath,
                                                                            HashSet<string> workbookSheetNames)
    {
        ExcelDataWorkbookTechnicalMetadata metadata = new ExcelDataWorkbookTechnicalMetadata();

        if (!workbookSheetNames.Contains(ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName))
            return metadata;

        metadata.MarkSheetFound();
        OpenXmlConfiguration configuration = new OpenXmlConfiguration();
        configuration.IgnoreEmptyRows = false;
        IEnumerable<object> queriedRows =
            MiniExcel.Query(workbookPath,
                            false,
                            ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName,
                            ExcelType.XLSX,
                            "A1",
                            configuration);

        // Parse records by the fixed v2 column contract without relying on worksheet row order.
        foreach (object queriedRow in queriedRows)
        {
            IDictionary<string, object> row = queriedRow as IDictionary<string, object>;
            string recordType = ReadText(row, "A");

            switch (recordType)
            {
                case WorkbookRecordType:
                    metadata.SetWorkbookRecord(ReadText(row, "B"),
                                               ReadText(row, "F"),
                                               ReadText(row, "I"));
                    break;
                case CellRecordType:
                    RegisterTechnicalCell(metadata, row);
                    break;
            }
        }

        return metadata;
    }

    /// <summary>
    /// Parses one technical Cell record and registers its optional reference identity.
    /// </summary>
    /// <param name="metadata">Metadata result receiving the cell record.</param>
    /// <param name="row">Raw technical worksheet row.</param>
    private static void RegisterTechnicalCell(ExcelDataWorkbookTechnicalMetadata metadata,
                                              IDictionary<string, object> row)
    {
        int rowIndex;
        int columnIndex;

        if (!TryReadPositiveInt(row, "V", out rowIndex) || !TryReadPositiveInt(row, "W", out columnIndex))
            return;

        metadata.RegisterCell(ReadText(row, "L"),
                              rowIndex,
                              columnIndex,
                              ReadText(row, "AO"),
                              ReadText(row, "AP"),
                              ReadText(row, "AQ"));
    }
    #endregion

    #region Raw Helpers
    /// <summary>
    /// Builds a case-insensitive set of all worksheet names in the source workbook.
    /// </summary>
    /// <param name="sheetInformations">MiniExcel worksheet information records.</param>
    /// <returns>Case-insensitive worksheet-name set.</returns>
    private static HashSet<string> BuildSheetNameSet(List<SheetInfo> sheetInformations)
    {
        HashSet<string> sheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int sheetIndex = 0; sheetIndex < sheetInformations.Count; sheetIndex++)
            sheetNames.Add(sheetInformations[sheetIndex].Name);

        return sheetNames;
    }

    /// <summary>
    /// Reads one raw MiniExcel column value while tolerating omitted empty keys.
    /// </summary>
    /// <param name="row">Raw workbook row keyed by Excel column name.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <returns>Stored value, or null for an empty cell.</returns>
    private static object ReadColumn(IDictionary<string, object> row, string columnName)
    {
        object value;
        return row != null && row.TryGetValue(columnName, out value) ? value : null;
    }

    /// <summary>
    /// Reads one raw cell as invariant text.
    /// </summary>
    /// <param name="row">Raw workbook row.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <returns>Invariant text, or an empty string.</returns>
    private static string ReadText(IDictionary<string, object> row, string columnName)
    {
        object value = ReadColumn(row, columnName);
        return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads one positive integer from a raw technical worksheet cell.
    /// </summary>
    /// <param name="row">Raw workbook row.</param>
    /// <param name="columnName">Excel column name.</param>
    /// <param name="value">Parsed positive integer.</param>
    /// <returns>True when the cell contains a positive integer.</returns>
    private static bool TryReadPositiveInt(IDictionary<string, object> row, string columnName, out int value)
    {
        return int.TryParse(ReadText(row, columnName), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores raw coordinate values and workbook-level diagnostics from one grid-authoritative read.
/// </summary>
internal sealed class ExcelDataGridWorkbookReadResult
{
    #region Fields
    private readonly Dictionary<string, Dictionary<long, object>> valuesBySheetId =
        new Dictionary<string, Dictionary<long, object>>(StringComparer.Ordinal);
    private readonly List<string> missingSheetNames = new List<string>();
    #endregion

    #region Properties
    public ExcelDataWorkbookTechnicalMetadata TechnicalMetadata
    {
        get;
    }

    public long WorkbookLastWriteUtcTicks
    {
        get;
    }

    public IReadOnlyList<string> MissingSheetNames
    {
        get
        {
            return missingSheetNames;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an empty raw workbook result for one source file timestamp.
    /// </summary>
    /// <param name="technicalMetadata">Parsed reserved technical metadata.</param>
    /// <param name="workbookLastWriteUtcTicks">Source file timestamp used for stale-preview detection.</param>
    public ExcelDataGridWorkbookReadResult(ExcelDataWorkbookTechnicalMetadata technicalMetadata,
                                           long workbookLastWriteUtcTicks)
    {
        TechnicalMetadata = technicalMetadata;
        WorkbookLastWriteUtcTicks = workbookLastWriteUtcTicks;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Registers one raw value at an exact authored sheet coordinate.
    /// </summary>
    /// <param name="sheetId">Stable authored worksheet identifier.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="value">Raw MiniExcel value.</param>
    public void RegisterValue(string sheetId, int rowIndex, int columnIndex, object value)
    {
        Dictionary<long, object> values;

        if (!valuesBySheetId.TryGetValue(sheetId, out values))
        {
            values = new Dictionary<long, object>();
            valuesBySheetId.Add(sheetId, values);
        }

        values[ExcelDataWorkbookCoordinateUtility.BuildKey(rowIndex, columnIndex)] = value;
    }

    /// <summary>
    /// Reads one previously requested coordinate.
    /// </summary>
    /// <param name="sheetId">Stable authored worksheet identifier.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <returns>Raw cell value, or null for an empty or unavailable cell.</returns>
    public object GetValue(string sheetId, int rowIndex, int columnIndex)
    {
        Dictionary<long, object> values;
        object value;

        if (!valuesBySheetId.TryGetValue(sheetId, out values))
            return null;

        return values.TryGetValue(ExcelDataWorkbookCoordinateUtility.BuildKey(rowIndex, columnIndex), out value) ? value : null;
    }

    /// <summary>
    /// Reports whether a requested coordinate has been materialized, including explicit empty cells.
    /// </summary>
    /// <param name="sheetId">Stable authored worksheet identifier.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <returns>True when the coordinate was registered.</returns>
    public bool ContainsValue(string sheetId, int rowIndex, int columnIndex)
    {
        Dictionary<long, object> values;

        return valuesBySheetId.TryGetValue(sheetId, out values) &&
               values.ContainsKey(ExcelDataWorkbookCoordinateUtility.BuildKey(rowIndex, columnIndex));
    }

    /// <summary>
    /// Registers a required user worksheet that is absent from the workbook.
    /// </summary>
    /// <param name="sheetName">Missing sanitized worksheet name.</param>
    public void RegisterMissingSheet(string sheetName)
    {
        if (!missingSheetNames.Contains(sheetName))
            missingSheetNames.Add(sheetName);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores schema identity and reference metadata parsed from the reserved workbook worksheet.
/// </summary>
internal sealed class ExcelDataWorkbookTechnicalMetadata
{
    #region Fields
    private readonly Dictionary<string, ExcelDataWorkbookTechnicalCellMetadata> cellsByAddress =
        new Dictionary<string, ExcelDataWorkbookTechnicalCellMetadata>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Properties
    public bool SheetFound
    {
        get;
        private set;
    }

    public bool WorkbookRecordFound
    {
        get;
        private set;
    }

    public string SchemaVersion
    {
        get;
        private set;
    }

    public string LayoutPresetId
    {
        get;
        private set;
    }

    public string LayoutHash
    {
        get;
        private set;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates empty metadata for a workbook that may not contain the reserved worksheet.
    /// </summary>
    public ExcelDataWorkbookTechnicalMetadata()
    {
        SchemaVersion = string.Empty;
        LayoutPresetId = string.Empty;
        LayoutHash = string.Empty;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Marks that the reserved technical worksheet exists in the workbook package.
    /// </summary>
    public void MarkSheetFound()
    {
        SheetFound = true;
    }

    /// <summary>
    /// Stores the single workbook identity record used for compatibility validation.
    /// </summary>
    /// <param name="schemaVersion">Technical schema version.</param>
    /// <param name="layoutPresetId">Layout preset ID captured during export.</param>
    /// <param name="layoutHash">Deterministic exported layout hash.</param>
    public void SetWorkbookRecord(string schemaVersion, string layoutPresetId, string layoutHash)
    {
        if (WorkbookRecordFound)
            return;

        WorkbookRecordFound = true;
        SchemaVersion = schemaVersion ?? string.Empty;
        LayoutPresetId = layoutPresetId ?? string.Empty;
        LayoutHash = layoutHash ?? string.Empty;
    }

    /// <summary>
    /// Registers optional object-reference metadata for one exact visible worksheet coordinate.
    /// </summary>
    /// <param name="workbookSheetName">Sanitized visible worksheet name.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="referenceName">Exported readable asset name.</param>
    /// <param name="referenceGuid">Exported asset GUID.</param>
    /// <param name="referencePath">Exported project-relative asset path.</param>
    public void RegisterCell(string workbookSheetName,
                             int rowIndex,
                             int columnIndex,
                             string referenceName,
                             string referenceGuid,
                             string referencePath)
    {
        cellsByAddress[BuildCellKey(workbookSheetName, rowIndex, columnIndex)] =
            new ExcelDataWorkbookTechnicalCellMetadata(referenceName, referenceGuid, referencePath);
    }

    /// <summary>
    /// Finds optional reference metadata for one exact visible worksheet coordinate.
    /// </summary>
    /// <param name="workbookSheetName">Sanitized visible worksheet name.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <returns>Matching technical cell metadata, or null.</returns>
    public ExcelDataWorkbookTechnicalCellMetadata FindCell(string workbookSheetName,
                                                            int rowIndex,
                                                            int columnIndex)
    {
        ExcelDataWorkbookTechnicalCellMetadata cellMetadata;
        return cellsByAddress.TryGetValue(BuildCellKey(workbookSheetName, rowIndex, columnIndex), out cellMetadata)
            ? cellMetadata
            : null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds one case-insensitive technical-cell lookup key.
    /// </summary>
    /// <param name="workbookSheetName">Sanitized visible worksheet name.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <returns>Stable worksheet and coordinate key.</returns>
    private static string BuildCellKey(string workbookSheetName, int rowIndex, int columnIndex)
    {
        return (workbookSheetName ?? string.Empty) + "!" + ExcelDataWorkbookCoordinateUtility.BuildAddress(rowIndex, columnIndex);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores optional hidden object-reference identity for one visible workbook cell.
/// </summary>
internal sealed class ExcelDataWorkbookTechnicalCellMetadata
{
    #region Properties
    public string ReferenceName
    {
        get;
    }

    public string ReferenceGuid
    {
        get;
    }

    public string ReferencePath
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates immutable reference metadata captured during export.
    /// </summary>
    /// <param name="referenceName">Readable asset name.</param>
    /// <param name="referenceGuid">Project asset GUID.</param>
    /// <param name="referencePath">Project-relative asset path.</param>
    public ExcelDataWorkbookTechnicalCellMetadata(string referenceName,
                                                  string referenceGuid,
                                                  string referencePath)
    {
        ReferenceName = referenceName ?? string.Empty;
        ReferenceGuid = referenceGuid ?? string.Empty;
        ReferencePath = referencePath ?? string.Empty;
    }
    #endregion

    #endregion
}
