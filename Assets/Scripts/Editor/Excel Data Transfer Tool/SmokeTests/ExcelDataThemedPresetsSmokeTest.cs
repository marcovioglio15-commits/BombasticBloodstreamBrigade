using System;
using System.Collections.Generic;
using System.IO;
using MiniExcelLibs;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates every shipped thematic transfer graph through real export and complete layout-snapshot restoration.
/// </summary>
public static class ExcelDataThemedPresetsSmokeTest
{
    #region Constants
    private const string Root = "Assets/Scriptable Objects/Editor/Excel Data Transfer/";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Exports all thematic workbooks and verifies their literal organization, domain bindings and round-trip layout hashes.
    /// </summary>
    public static void Run()
    {
        List<ThemeExpectation> expectations = BuildExpectations();

        // Validate each independent master graph through the same public services used by the editor tool.
        for (int expectationIndex = 0; expectationIndex < expectations.Count; expectationIndex++)
            ValidateTheme(expectations[expectationIndex]);

        AssetDatabase.SaveAssets();
        Debug.Log("[ExcelDataThemedPresetsSmokeTest] PASS - themes: " + expectations.Count + ".");
    }
    #endregion

    #region Theme Validation
    /// <summary>
    /// Validates one thematic master graph, writes its workbook and restores its complete technical layout snapshot.
    /// </summary>
    /// <param name="expectation">Fixed theme contract expected from the shipped test asset.</param>
    private static void ValidateTheme(ThemeExpectation expectation)
    {
        ExcelDataTransferMasterPreset masterPreset =
            AssetDatabase.LoadAssetAtPath<ExcelDataTransferMasterPreset>(Root + expectation.MasterAssetName + ".asset");

        if (masterPreset == null)
            throw new InvalidOperationException("Missing thematic master preset: " + expectation.MasterAssetName + ".");

        ValidatePresetGraph(masterPreset, expectation);
        ExcelDataExportResult exportResult = ExcelDataExportService.ExportWorkbook(masterPreset, string.Empty);
        ValidateExportResult(exportResult, masterPreset.LayoutPreset, expectation);
        ValidateVisibleWorkbook(exportResult.WorkbookPath,
                                masterPreset.LayoutPreset.SheetDefinitions[0],
                                expectation);
        ValidateSnapshotRoundTrip(masterPreset.LayoutPreset, exportResult, expectation);
    }

    /// <summary>
    /// Checks graph ownership, independent import/export paths, domain guardrails and the authored visible worksheet structure.
    /// </summary>
    /// <param name="masterPreset">Thematic master graph to inspect.</param>
    /// <param name="expectation">Expected theme identity and content contract.</param>
    private static void ValidatePresetGraph(ExcelDataTransferMasterPreset masterPreset,
                                            ThemeExpectation expectation)
    {
        if (masterPreset.LayoutPreset == null || masterPreset.BrushPalettePreset == null ||
            masterPreset.ImportPreset == null || masterPreset.ExportPreset == null)
            throw new InvalidOperationException(expectation.DisplayName + " does not own a complete sub-preset graph.");

        if (masterPreset.LayoutPreset.SheetDefinitions.Count != 1)
            throw new InvalidOperationException(expectation.DisplayName + " must contain exactly one focused user worksheet.");

        ExcelDataWorkbookPathState importPathState =
            ExcelDataWorkbookPathUtility.EvaluateImportWorkbookPath(masterPreset.ImportPreset, string.Empty);
        ExcelDataWorkbookPathState exportPathState =
            ExcelDataWorkbookPathUtility.EvaluateExportWorkbookPath(masterPreset.ExportPreset, string.Empty);

        if (!importPathState.HasValidExtension || !exportPathState.HasValidExtension)
            throw new InvalidOperationException(expectation.DisplayName +
                                                " import and export paths must independently target .xlsx workbooks.");

        ValidateDomainFlags(masterPreset.ImportPreset, expectation);
        ValidateDomainFlags(masterPreset.ExportPreset, expectation);
        ValidateSheet(masterPreset.LayoutPreset.SheetDefinitions[0], expectation);
    }

    /// <summary>
    /// Verifies one authored user sheet contains readable literals and only theme-appropriate data bindings.
    /// </summary>
    /// <param name="sheet">Authoritative thematic worksheet.</param>
    /// <param name="expectation">Expected title, domain and minimum content counts.</param>
    private static void ValidateSheet(ExcelDataWorkbookSheetDefinition sheet,
                                      ThemeExpectation expectation)
    {
        int dataFieldCount = 0;
        int literalCount = 0;
        bool titleFound = false;

        // Count every sparse cell while validating that data never leaks across thematic domains.
        for (int cellIndex = 0; cellIndex < sheet.Cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = sheet.Cells[cellIndex];

            if (cell == null)
                continue;

            switch (cell.ContentKind)
            {
                case ExcelDataWorkbookCellContentKind.DataField:
                    dataFieldCount++;

                    if (!IsExpectedDomain(cell.FieldBinding.Domain, expectation.Domain))
                        throw new InvalidOperationException(expectation.DisplayName +
                                                            " contains an out-of-theme field: " +
                                                            cell.FieldBinding.FieldId + ".");
                    break;
                case ExcelDataWorkbookCellContentKind.LiteralText:
                    literalCount++;

                    if (cell.RowIndex == 1 && cell.ColumnIndex == 1 &&
                        string.Equals(cell.LiteralText, expectation.Title, StringComparison.Ordinal))
                        titleFound = true;
                    break;
            }
        }

        if (!titleFound || dataFieldCount < expectation.MinimumDataFields ||
            literalCount < expectation.MinimumLiterals)
            throw new InvalidOperationException(expectation.DisplayName +
                                                " is missing its title or required organized cell content.");

        if (sheet.FreezeRowCount != 4 || sheet.FreezeColumnCount != 1)
            throw new InvalidOperationException(expectation.DisplayName + " does not preserve the themed navigation freeze panes.");
    }

    /// <summary>
    /// Verifies the concrete workbook was created without unresolved cells and reports the authored grid counts.
    /// </summary>
    /// <param name="result">Completed export operation.</param>
    /// <param name="layoutPreset">Source authoritative layout.</param>
    /// <param name="expectation">Theme used for actionable diagnostics.</param>
    private static void ValidateExportResult(ExcelDataExportResult result,
                                             ExcelDataWorkbookLayoutPreset layoutPreset,
                                             ThemeExpectation expectation)
    {
        int expectedCellCount = layoutPreset.SheetDefinitions[0].Cells.Count;

        if (result == null || result.UserSheetCount != 1 || result.AuthoredCellCount != expectedCellCount ||
            result.WrittenCellCount != expectedCellCount || result.WarningCount != 0)
            throw new InvalidOperationException(expectation.DisplayName + " export did not write every authored cell cleanly.");

        FileInfo workbook = new FileInfo(result.WorkbookPath);

        if (!workbook.Exists || workbook.Length <= 0)
            throw new InvalidOperationException(expectation.DisplayName + " export did not create a non-empty workbook.");
    }

    /// <summary>
    /// Reads the physical user worksheet and verifies that its visible organization matches the authored grid.
    /// </summary>
    /// <param name="workbookPath">Exported workbook path.</param>
    /// <param name="sheet">Source authoritative worksheet.</param>
    /// <param name="expectation">Theme title and diagnostic identity.</param>
    private static void ValidateVisibleWorkbook(string workbookPath,
                                                ExcelDataWorkbookSheetDefinition sheet,
                                                ThemeExpectation expectation)
    {
        string sheetName = ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet");
        IEnumerable<object> queriedRows = MiniExcel.Query(workbookPath,
                                                          false,
                                                          sheetName,
                                                          ExcelType.XLSX,
                                                          "A1",
                                                          null);
        List<IDictionary<string, object>> rows = new List<IDictionary<string, object>>();

        // Materialize only the focused user worksheet so exact row and column letters remain available.
        foreach (object queriedRow in queriedRows)
        {
            IDictionary<string, object> row = queriedRow as IDictionary<string, object>;

            if (row != null)
                rows.Add(row);
        }

        if (rows.Count < 4 ||
            !string.Equals(ReadText(rows[0], "A"), expectation.Title, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(ReadText(rows[1], "A")) ||
            !MatchesAuthoredLiteral(rows[3], sheet, 4, "SETTING") ||
            !MatchesAuthoredLiteral(rows[3], sheet, 4, "VALUE") ||
            !MatchesAuthoredLiteral(rows[3], sheet, 4, "SOURCE ASSET") ||
            !MatchesAuthoredLiteral(rows[3], sheet, 4, "SERIALIZED PATH"))
            throw new InvalidOperationException(expectation.DisplayName +
                                                " physical workbook does not match its visible authored organization.");
    }

    /// <summary>
    /// Verifies one exported literal at the column currently authored by the workbook layout.
    /// </summary>
    /// <param name="row">Physical workbook row keyed by Excel column letter.</param>
    /// <param name="sheet">Authoritative layout that owns the expected coordinate.</param>
    /// <param name="rowIndex">One-based row containing the expected literal.</param>
    /// <param name="literal">Exact literal text to locate and validate.</param>
    /// <returns>True when the authored literal exists at its current exported coordinate.</returns>
    private static bool MatchesAuthoredLiteral(IDictionary<string, object> row,
                                               ExcelDataWorkbookSheetDefinition sheet,
                                               int rowIndex,
                                               string literal)
    {
        // Resolve the current authored column so structural grid edits remain valid test inputs.
        for (int cellIndex = 0; cellIndex < sheet.Cells.Count; cellIndex++)
        {
            ExcelDataWorkbookCellDefinition cell = sheet.Cells[cellIndex];

            if (cell == null || cell.RowIndex != rowIndex ||
                !string.Equals(cell.LiteralText, literal, StringComparison.Ordinal))
                continue;

            return string.Equals(ReadText(row,
                                          ExcelDataWorkbookCoordinateUtility.ColumnIndexToName(cell.ColumnIndex)),
                                 literal,
                                 StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Reads one raw MiniExcel cell as text while tolerating omitted empty keys.
    /// </summary>
    /// <param name="row">Raw workbook row keyed by Excel column letter.</param>
    /// <param name="columnName">Excel column letter.</param>
    /// <returns>Invariant text value, or an empty string when the cell is absent.</returns>
    private static string ReadText(IDictionary<string, object> row, string columnName)
    {
        object value;

        if (row == null || !row.TryGetValue(columnName, out value) || value == null)
            return string.Empty;

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Restores a workbook snapshot into a transient preset and verifies the full deterministic layout identity.
    /// </summary>
    /// <param name="sourceLayout">Original thematic layout.</param>
    /// <param name="exportResult">Export result containing workbook path and source hash.</param>
    /// <param name="expectation">Theme used for actionable diagnostics.</param>
    private static void ValidateSnapshotRoundTrip(ExcelDataWorkbookLayoutPreset sourceLayout,
                                                  ExcelDataExportResult exportResult,
                                                  ThemeExpectation expectation)
    {
        ExcelDataWorkbookLayoutPreset restoredLayout = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();

        try
        {
            ExcelDataWorkbookLayoutImportResult importResult =
                ExcelDataWorkbookLayoutImportService.ImportLayoutSnapshot(restoredLayout, exportResult.WorkbookPath);
            string sourceHash = ExcelDataWorkbookLayoutHashUtility.Calculate(sourceLayout);
            string restoredHash = ExcelDataWorkbookLayoutHashUtility.Calculate(restoredLayout);

            if (!importResult.LayoutHashMatches || importResult.ImportedSheetCount != sourceLayout.SheetDefinitions.Count ||
                !string.Equals(sourceHash, exportResult.LayoutHash, StringComparison.Ordinal) ||
                !string.Equals(sourceHash, restoredHash, StringComparison.Ordinal))
                throw new InvalidOperationException(expectation.DisplayName + " layout snapshot did not round-trip exactly.");

            ValidateSheet(restoredLayout.SheetDefinitions[0], expectation);
        }
        finally
        {
            restoredLayout.SheetDefinitions.Clear();
            ScriptableObject.DestroyImmediate(restoredLayout);
        }
    }
    #endregion

    #region Domain Validation
    /// <summary>
    /// Verifies import domain toggles expose only the theme represented by the preset.
    /// </summary>
    /// <param name="preset">Thematic import preset.</param>
    /// <param name="expectation">Expected enabled domain.</param>
    private static void ValidateDomainFlags(ExcelDataImportPreset preset, ThemeExpectation expectation)
    {
        ValidateDomainFlags(preset.IncludePlayerData,
                            preset.IncludeEnemyData,
                            preset.IncludeGameData,
                            preset.IncludeWaveData,
                            expectation);
    }

    /// <summary>
    /// Verifies export domain toggles expose only the theme represented by the preset.
    /// </summary>
    /// <param name="preset">Thematic export preset.</param>
    /// <param name="expectation">Expected enabled domain.</param>
    private static void ValidateDomainFlags(ExcelDataExportPreset preset, ThemeExpectation expectation)
    {
        ValidateDomainFlags(preset.IncludePlayerData,
                            preset.IncludeEnemyData,
                            preset.IncludeGameData,
                            preset.IncludeWaveData,
                            expectation);
    }

    /// <summary>
    /// Compares four operation guardrails against one exclusive thematic domain.
    /// </summary>
    /// <param name="playerEnabled">Player-domain flag.</param>
    /// <param name="enemyEnabled">Enemy-domain flag.</param>
    /// <param name="gameEnabled">Game-domain flag.</param>
    /// <param name="wavesEnabled">Waves-domain flag.</param>
    /// <param name="expectation">Expected enabled domain.</param>
    private static void ValidateDomainFlags(bool playerEnabled,
                                            bool enemyEnabled,
                                            bool gameEnabled,
                                            bool wavesEnabled,
                                            ThemeExpectation expectation)
    {
        bool valid;

        switch (expectation.Domain)
        {
            case ExcelDataTransferDomain.Player:
                valid = playerEnabled && !enemyEnabled && !gameEnabled && !wavesEnabled;
                break;
            case ExcelDataTransferDomain.Enemy:
                valid = !playerEnabled && enemyEnabled && !gameEnabled && !wavesEnabled;
                break;
            case ExcelDataTransferDomain.Game:
                valid = !playerEnabled && !enemyEnabled && gameEnabled && !wavesEnabled;
                break;
            case ExcelDataTransferDomain.Waves:
                valid = !playerEnabled && !enemyEnabled && !gameEnabled && wavesEnabled;
                break;
            default:
                valid = false;
                break;
        }

        if (!valid)
            throw new InvalidOperationException(expectation.DisplayName + " has incorrect import/export domain guardrails.");
    }

    /// <summary>
    /// Accepts the dedicated spawner-authoring domain as part of a waves workbook.
    /// </summary>
    /// <param name="actualDomain">Domain carried by one field binding.</param>
    /// <param name="expectedDomain">Primary theme domain.</param>
    /// <returns>True when the binding belongs to the thematic workbook.</returns>
    private static bool IsExpectedDomain(ExcelDataTransferDomain actualDomain,
                                         ExcelDataTransferDomain expectedDomain)
    {
        if (expectedDomain == ExcelDataTransferDomain.Waves)
            return actualDomain == ExcelDataTransferDomain.Waves ||
                   actualDomain == ExcelDataTransferDomain.SpawnerAuthoring;

        return actualDomain == expectedDomain;
    }
    #endregion

    #region Expectations
    /// <summary>
    /// Creates the fixed contracts for the four shipped thematic preset graphs.
    /// </summary>
    /// <returns>Ordered theme expectations used by batch validation.</returns>
    private static List<ThemeExpectation> BuildExpectations()
    {
        return new List<ThemeExpectation>
        {
            new ThemeExpectation("Player Tuning",
                                 "PlayerTuningExcelDataTransferMasterPreset",
                                 "PLAYER TUNING WORKBOOK",
                                 ExcelDataTransferDomain.Player,
                                 8,
                                 30),
            new ThemeExpectation("Enemy Balance",
                                 "EnemyBalanceExcelDataTransferMasterPreset",
                                 "ENEMY BALANCE WORKBOOK",
                                 ExcelDataTransferDomain.Enemy,
                                 8,
                                 30),
            new ThemeExpectation("Wave Encounters",
                                 "WaveEncountersExcelDataTransferMasterPreset",
                                 "WAVE ENCOUNTERS WORKBOOK",
                                 ExcelDataTransferDomain.Waves,
                                 7,
                                 26),
            new ThemeExpectation("Game Flow",
                                 "GameFlowExcelDataTransferMasterPreset",
                                 "GAME FLOW WORKBOOK",
                                 ExcelDataTransferDomain.Game,
                                 7,
                                 27)
        };
    }
    #endregion

    #endregion

    #region Types
    /// <summary>
    /// Stores the immutable validation contract for one thematic transfer graph.
    /// </summary>
    private readonly struct ThemeExpectation
    {
        #region Properties
        public string DisplayName { get; }
        public string MasterAssetName { get; }
        public string Title { get; }
        public ExcelDataTransferDomain Domain { get; }
        public int MinimumDataFields { get; }
        public int MinimumLiterals { get; }
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one thematic graph expectation.
        /// </summary>
        /// <param name="displayName">Readable diagnostic name.</param>
        /// <param name="masterAssetName">Fixed master asset filename without extension.</param>
        /// <param name="title">Exact literal title stored in A1.</param>
        /// <param name="domain">Exclusive transfer domain.</param>
        /// <param name="minimumDataFields">Minimum expected Data Field cells.</param>
        /// <param name="minimumLiterals">Minimum expected Literal Text cells.</param>
        public ThemeExpectation(string displayName,
                                string masterAssetName,
                                string title,
                                ExcelDataTransferDomain domain,
                                int minimumDataFields,
                                int minimumLiterals)
        {
            DisplayName = displayName;
            MasterAssetName = masterAssetName;
            Title = title;
            Domain = domain;
            MinimumDataFields = minimumDataFields;
            MinimumLiterals = minimumLiterals;
        }
        #endregion

        #endregion
    }
    #endregion
}
