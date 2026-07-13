using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using UnityEngine;

/// <summary>
/// Validates Tranche 1 grid-authoritative models, legacy conversion and MiniExcel matrix persistence.
/// </summary>
public static class ExcelDataGridAuthoritativeTrancheOneSmokeTest
{
    #region Constants
    private const string DataSheetName = "Grid Exact";
    private const string TechnicalSheetName = "_NashCoreTransfer";
    private const string NumberFieldId = "Smoke:Number";
    private const string BooleanFieldId = "Smoke:Boolean";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs isolated in-memory, migration and real `.xlsx` assertions for the first rework tranche.
    /// </summary>
    public static void Run()
    {
        ValidateInMemoryCoordinates();
        ValidateLegacyConversion();
        string workbookPath = ValidateMiniExcelRoundTrip();
        Debug.Log("[ExcelDataGridAuthoritativeTrancheOneSmokeTest] PASS - workbook: " + workbookPath);
    }
    #endregion

    #region In-Memory Validation
    /// <summary>
    /// Verifies that sparse authored cells retain their exact A1-equivalent matrix coordinates.
    /// </summary>
    private static void ValidateInMemoryCoordinates()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = CreateSmokeLayout();

        try
        {
            ExcelDataWorkbookExportBuildResult result =
                ExcelDataWorkbookDocumentBuilder.BuildExportDocument(layoutPreset, ResolveSmokeValue);
            ValidateSmokeDocument(result.Document);
        }
        finally
        {
            ScriptableObject.DestroyImmediate(layoutPreset);
        }
    }

    /// <summary>
    /// Validates exact dimensions, values, empty cells and hidden-sheet state in one document.
    /// </summary>
    /// <param name="document">Workbook document built from the smoke layout.</param>
    private static void ValidateSmokeDocument(ExcelDataWorkbookDocument document)
    {
        if (document == null || document.Sheets.Count != 2)
            throw new InvalidOperationException("Grid-authoritative document did not create the expected two sheets.");

        ExcelDataWorkbookSheetDocument dataSheet = document.FindSheet(DataSheetName);

        if (dataSheet == null)
            throw new InvalidOperationException("Grid-authoritative document is missing the visible data sheet.");

        if (dataSheet.RowCount != 4 || dataSheet.ColumnCount != 6)
            throw new InvalidOperationException("Visible data sheet dimensions do not match maximum authored coordinate F4.");

        AssertValue(dataSheet.GetValue(1, 1), "Player Preset", "A1 literal text");
        AssertValue(dataSheet.GetValue(1, 2), null, "B1 empty cell");
        AssertNumericValue(dataSheet.GetValue(2, 3), 12.5d, "C2 numeric data field");
        AssertValue(dataSheet.GetValue(3, 2), true, "B3 boolean data field");
        AssertValue(dataSheet.GetValue(4, 6), "Visual Preset", "F4 literal text");

        ExcelDataWorkbookSheetDocument technicalSheet = document.FindSheet(TechnicalSheetName);

        if (technicalSheet == null || technicalSheet.Visibility != ExcelDataWorkbookSheetVisibility.VeryHidden)
            throw new InvalidOperationException("Technical sheet is missing or is not configured as VeryHidden.");
    }
    #endregion

    #region Migration Validation
    /// <summary>
    /// Verifies that unresolved legacy mappings retain coordinate, direction, format and field ID.
    /// </summary>
    private static void ValidateLegacyConversion()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();
        ExcelDataCellBrushMapping legacyMapping = new ExcelDataCellBrushMapping();
        legacyMapping.Configure(layoutPreset.ObjectsSheetName,
                                7,
                                5,
                                "Legacy:Missing:Field",
                                ExcelDataTransferDirection.Import,
                                "legacy.path",
                                "0.00");
        layoutPreset.CellMappings.Add(legacyMapping);

        try
        {
            Dictionary<string, ExcelDataFieldCatalogEntry> entriesById =
                new Dictionary<string, ExcelDataFieldCatalogEntry>(StringComparer.Ordinal);
            ExcelDataGridAuthoritativeMigrationResult result =
                ExcelDataGridAuthoritativeLayoutMigrationUtility.ConvertPreset(layoutPreset, entriesById, true);

            if (result.WasSkipped || result.ConvertedCellCount != 1 || result.UnresolvedCellCount != 1)
                throw new InvalidOperationException("Legacy layout conversion diagnostics are incorrect.");

            if (layoutPreset.SheetDefinitions.Count != 1)
                throw new InvalidOperationException("Legacy layout conversion did not create one worksheet definition.");

            ExcelDataWorkbookCellDefinition convertedCell = layoutPreset.SheetDefinitions[0].FindCell(7, 5);

            if (convertedCell == null)
                throw new InvalidOperationException("Legacy layout conversion did not preserve coordinate E7.");

            if (convertedCell.Direction != ExcelDataTransferDirection.Import || convertedCell.NumberFormat != "0.00")
                throw new InvalidOperationException("Legacy layout conversion did not preserve direction or number format.");

            if (convertedCell.FieldBinding.FieldId != legacyMapping.FieldId)
                throw new InvalidOperationException("Legacy layout conversion discarded an unresolved field identifier.");
        }
        finally
        {
            ScriptableObject.DestroyImmediate(layoutPreset);
        }
    }
    #endregion

    #region Workbook Validation
    /// <summary>
    /// Writes and reads a real workbook to verify MiniExcel preserves sparse cells and typed values.
    /// </summary>
    /// <returns>Absolute path of the verified smoke workbook.</returns>
    private static string ValidateMiniExcelRoundTrip()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = CreateSmokeLayout();
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(),
                                         "Logs",
                                         "ExcelDataGridAuthoritativeTrancheOneSmokeTest.xlsx");

        try
        {
            ExcelDataWorkbookExportBuildResult result =
                ExcelDataWorkbookDocumentBuilder.BuildExportDocument(layoutPreset, ResolveSmokeValue);
            IExcelDataWorkbookAdapter adapter = new MiniExcelDataWorkbookAdapter();
            string workbookPath = adapter.SaveWorkbook(outputPath, result.Document);
            FileInfo outputFile = new FileInfo(workbookPath);

            if (!outputFile.Exists || outputFile.Length <= 0)
                throw new InvalidOperationException("MiniExcel adapter did not create a valid workbook file.");

            ValidatePersistedCells(workbookPath);
            ValidatePersistedVisibility(workbookPath);
            return workbookPath;
        }
        finally
        {
            ScriptableObject.DestroyImmediate(layoutPreset);
        }
    }

    /// <summary>
    /// Reads the visible smoke sheet without headers and validates exact persisted coordinates.
    /// </summary>
    /// <param name="workbookPath">Absolute workbook path written by the adapter.</param>
    private static void ValidatePersistedCells(string workbookPath)
    {
        IEnumerable<object> queriedRows =
            MiniExcel.Query(workbookPath, false, DataSheetName, ExcelType.XLSX, "A1", null);
        List<IDictionary<string, object>> rows = new List<IDictionary<string, object>>();

        // Materialize raw MiniExcel rows so exact column-letter assertions stay deterministic.
        foreach (object queriedRow in queriedRows)
        {
            IDictionary<string, object> row = queriedRow as IDictionary<string, object>;

            if (row != null)
                rows.Add(row);
        }

        if (rows.Count != 4)
            throw new InvalidOperationException("Persisted visible sheet does not end at row 4.");

        AssertValue(ReadColumn(rows[0], "A"), "Player Preset", "persisted A1 literal text");
        AssertValue(ReadColumn(rows[0], "B"), null, "persisted B1 empty cell");
        AssertNumericValue(ReadColumn(rows[1], "C"), 12.5d, "persisted C2 numeric data field");
        AssertValue(ReadColumn(rows[2], "B"), true, "persisted B3 boolean data field");
        AssertValue(ReadColumn(rows[3], "F"), "Visual Preset", "persisted F4 literal text");
    }

    /// <summary>
    /// Validates that MiniExcel persisted the technical worksheet as VeryHidden.
    /// </summary>
    /// <param name="workbookPath">Absolute workbook path written by the adapter.</param>
    private static void ValidatePersistedVisibility(string workbookPath)
    {
        List<SheetInfo> sheetInformations = MiniExcel.GetSheetInformations(workbookPath, new OpenXmlConfiguration());

        // Find the technical sheet by name without relying on workbook ordering.
        for (int sheetIndex = 0; sheetIndex < sheetInformations.Count; sheetIndex++)
        {
            SheetInfo sheetInformation = sheetInformations[sheetIndex];

            if (sheetInformation.Name != TechnicalSheetName)
                continue;

            if (sheetInformation.State != SheetState.VeryHidden)
                throw new InvalidOperationException("Technical worksheet was not persisted as VeryHidden.");

            return;
        }

        throw new InvalidOperationException("Persisted workbook is missing the technical worksheet.");
    }
    #endregion

    #region Smoke Layout
    /// <summary>
    /// Creates an isolated layout with sparse literal, numeric, boolean and technical cells.
    /// </summary>
    /// <returns>Transient layout preset used only by smoke assertions.</returns>
    private static ExcelDataWorkbookLayoutPreset CreateSmokeLayout()
    {
        ExcelDataWorkbookLayoutPreset layoutPreset = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();
        ExcelDataWorkbookSheetDefinition dataSheet = new ExcelDataWorkbookSheetDefinition();
        dataSheet.Configure(DataSheetName, 8, 8, 120, 28, true, true, ExcelDataWorkbookSheetVisibility.Visible);
        dataSheet.Cells.Add(CreateLiteralCell(dataSheet.SheetId, 1, 1, "Player Preset"));
        dataSheet.Cells.Add(CreateDataCell(dataSheet.SheetId, 2, 3, NumberFieldId, ExcelDataBrushDataKind.Number));
        dataSheet.Cells.Add(CreateDataCell(dataSheet.SheetId, 3, 2, BooleanFieldId, ExcelDataBrushDataKind.Boolean));
        dataSheet.Cells.Add(CreateLiteralCell(dataSheet.SheetId, 4, 6, "Visual Preset"));
        layoutPreset.SheetDefinitions.Add(dataSheet);

        ExcelDataWorkbookSheetDefinition technicalSheet = new ExcelDataWorkbookSheetDefinition();
        technicalSheet.Configure(TechnicalSheetName, 2, 2, 120, 28, false, true, ExcelDataWorkbookSheetVisibility.VeryHidden);
        technicalSheet.Cells.Add(CreateLiteralCell(technicalSheet.SheetId, 1, 1, "SchemaVersion"));
        technicalSheet.Cells.Add(CreateLiteralCell(technicalSheet.SheetId, 1, 2, "2"));
        layoutPreset.SheetDefinitions.Add(technicalSheet);
        return layoutPreset;
    }

    /// <summary>
    /// Creates one export-only literal cell for a smoke layout.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet identifier.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    /// <param name="text">Exact literal text.</param>
    /// <returns>Configured literal cell definition.</returns>
    private static ExcelDataWorkbookCellDefinition CreateLiteralCell(string sheetId,
                                                                     int rowIndex,
                                                                     int columnIndex,
                                                                     string text)
    {
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureLiteralText(sheetId,
                                  rowIndex,
                                  columnIndex,
                                  text,
                                  ExcelDataTransferDirection.Export,
                                  "SmokeLiteral",
                                  false);
        return cell;
    }

    /// <summary>
    /// Creates one export-only data cell with a synthetic stable binding.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet identifier.</param>
    /// <param name="rowIndex">One-based row index.</param>
    /// <param name="columnIndex">One-based column index.</param>
    /// <param name="fieldId">Synthetic field identifier resolved by the smoke callback.</param>
    /// <param name="dataKind">Expected typed workbook value family.</param>
    /// <returns>Configured data field cell definition.</returns>
    private static ExcelDataWorkbookCellDefinition CreateDataCell(string sheetId,
                                                                  int rowIndex,
                                                                  int columnIndex,
                                                                  string fieldId,
                                                                  ExcelDataBrushDataKind dataKind)
    {
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.Configure(fieldId,
                          ExcelDataTransferDomain.Player,
                          "smoke-guid",
                          "SmokePreset",
                          "Assets/SmokePreset.asset",
                          "smoke.value",
                          "smoke.value",
                          dataKind);
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureDataField(sheetId,
                                rowIndex,
                                columnIndex,
                                binding,
                                ExcelDataTransferDirection.Export,
                                "SmokeData",
                                string.Empty);
        return cell;
    }

    /// <summary>
    /// Resolves deterministic typed values for synthetic smoke field bindings.
    /// </summary>
    /// <param name="binding">Synthetic field binding requested by the document builder.</param>
    /// <returns>Typed number/boolean snapshot or a warning snapshot for an unknown smoke field.</returns>
    private static ExcelDataSerializedValueSnapshot ResolveSmokeValue(ExcelDataFieldBinding binding)
    {
        if (binding == null)
            return ExcelDataSerializedValueSnapshot.CreateWarning("Missing smoke binding.", string.Empty);

        switch (binding.FieldId)
        {
            case NumberFieldId:
                return ExcelDataSerializedValueSnapshot.CreateValue(12.5d, string.Empty);
            case BooleanFieldId:
                return ExcelDataSerializedValueSnapshot.CreateValue(true, string.Empty);
            default:
                return ExcelDataSerializedValueSnapshot.CreateWarning("Unknown smoke binding.", string.Empty);
        }
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Reads a raw MiniExcel column value without assuming every empty key is materialized.
    /// </summary>
    /// <param name="row">Raw workbook row keyed by Excel column letter.</param>
    /// <param name="columnName">Excel column letter to read.</param>
    /// <returns>Stored value or null when the column is absent or empty.</returns>
    private static object ReadColumn(IDictionary<string, object> row, string columnName)
    {
        if (row == null)
            return null;

        object value;
        return row.TryGetValue(columnName, out value) ? value : null;
    }

    /// <summary>
    /// Compares one workbook value with an expected object value.
    /// </summary>
    /// <param name="actual">Actual workbook value.</param>
    /// <param name="expected">Expected workbook value.</param>
    /// <param name="label">Diagnostic label used by a failed assertion.</param>
    private static void AssertValue(object actual, object expected, string label)
    {
        if (Equals(actual, expected))
            return;

        throw new InvalidOperationException(label + " mismatch. Expected: " + expected + ", actual: " + actual + ".");
    }

    /// <summary>
    /// Compares one typed workbook number with an expected invariant double value.
    /// </summary>
    /// <param name="actual">Actual workbook numeric value.</param>
    /// <param name="expected">Expected numeric value.</param>
    /// <param name="label">Diagnostic label used by a failed assertion.</param>
    private static void AssertNumericValue(object actual, double expected, string label)
    {
        if (actual == null)
            throw new InvalidOperationException(label + " is null.");

        double actualNumber = Convert.ToDouble(actual, CultureInfo.InvariantCulture);

        if (Math.Abs(actualNumber - expected) <= 0.000001d)
            return;

        throw new InvalidOperationException(label + " mismatch. Expected: " + expected + ", actual: " + actualNumber + ".");
    }
    #endregion

    #endregion
}
