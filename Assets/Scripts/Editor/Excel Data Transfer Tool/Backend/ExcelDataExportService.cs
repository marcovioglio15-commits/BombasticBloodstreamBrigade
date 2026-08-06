using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>
/// Exports only grid-authoritative layout cells to exact workbook coordinates and hidden technical metadata.
/// </summary>
internal static class ExcelDataExportService
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Exports the selected master preset through the cell-oriented MiniExcel adapter.
    /// </summary>
    /// <param name="masterPreset">Master preset that links export and grid-authoritative layout settings.</param>
    /// <param name="overrideWorkbookPath">Optional absolute or project-relative output path used by tests and direct commands.</param>
    /// <returns>Detailed export summary with exact-cell counts, warnings, hash and final path.</returns>
    public static ExcelDataExportResult ExportWorkbook(ExcelDataTransferMasterPreset masterPreset,
                                                       string overrideWorkbookPath)
    {
        ValidatePresetGraph(masterPreset);
        ExcelDataExportPreset exportPreset = masterPreset.ExportPreset;
        ExcelDataWorkbookLayoutPreset layoutPreset = masterPreset.LayoutPreset;
        ExcelDataWorkbookExportBuildResult buildResult =
            ExcelDataWorkbookDocumentBuilder.BuildExportDocument(
                layoutPreset,
                binding => ResolveFieldValue(binding, exportPreset),
                masterPreset.BrushPalettePreset,
                exportPreset.WriteBrushBackgroundColors,
                exportPreset.WriteBrushTextColors);
        string layoutHash = ExcelDataWorkbookLayoutHashUtility.Calculate(layoutPreset);
        string exportedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        int technicalRowCount =
            ExcelDataWorkbookTechnicalSheetBuilder.AppendTechnicalSheet(masterPreset,
                                                                        buildResult,
                                                                        layoutHash,
                                                                        exportedUtc);
        string targetWorkbookPath = ExcelDataWorkbookPathUtility.ResolveExportWorkbookPath(exportPreset, overrideWorkbookPath);
        IExcelDataWorkbookAdapter adapter = new MiniExcelDataWorkbookAdapter();
        string workbookPath = adapter.SaveWorkbook(targetWorkbookPath, buildResult.Document);

        if (!File.Exists(workbookPath))
            throw new IOException("Workbook adapter finished but the file was not found: " + workbookPath);

        List<ExcelDataExportDiagnostic> diagnostics = BuildDiagnostics(buildResult.Cells);
        return new ExcelDataExportResult(workbookPath,
                                         buildResult.Sheets.Count,
                                         buildResult.Cells.Count,
                                         buildResult.WrittenCellCount,
                                         buildResult.DataFieldCellCount,
                                         buildResult.LiteralCellCount,
                                         buildResult.FormulaCellCount,
                                         buildResult.ReferenceCellCount,
                                         buildResult.WarningCount,
                                         technicalRowCount,
                                         layoutHash,
                                         diagnostics);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates the minimum preset graph required by the grid-authoritative export pipeline.
    /// </summary>
    /// <param name="masterPreset">Master preset graph to validate.</param>
    private static void ValidatePresetGraph(ExcelDataTransferMasterPreset masterPreset)
    {
        if (masterPreset == null)
            throw new ArgumentNullException(nameof(masterPreset));

        masterPreset.ValidateValues();

        if (masterPreset.ExportPreset == null)
            throw new InvalidOperationException("Missing Excel export preset.");

        if (masterPreset.LayoutPreset == null)
            throw new InvalidOperationException("Missing Excel workbook layout preset.");

        if (masterPreset.LayoutPreset.SheetDefinitions.Count <= 0)
            throw new InvalidOperationException("The linked layout has no grid-authoritative worksheet definitions.");
    }
    #endregion

    #region Value Resolution
    /// <summary>
    /// Applies export-domain guardrails before reading one typed SerializedProperty value directly from its binding.
    /// </summary>
    /// <param name="binding">Stable cell field binding.</param>
    /// <param name="exportPreset">Export preset containing domain and reference metadata policies.</param>
    /// <returns>Typed value snapshot or a cell-local warning when the domain is disabled.</returns>
    private static ExcelDataSerializedValueSnapshot ResolveFieldValue(ExcelDataFieldBinding binding,
                                                                      ExcelDataExportPreset exportPreset)
    {
        if (!AllowsDomain(binding.Domain, exportPreset))
            return ExcelDataSerializedValueSnapshot.CreateWarning("Export preset disables domain: " + binding.Domain + ".", string.Empty);

        return ExcelDataSerializedValueReader.ReadValue(binding,
                                                        exportPreset.WriteAssetNames,
                                                        exportPreset.WriteReferenceGuids,
                                                        exportPreset.WriteReferencePaths);
    }

    /// <summary>
    /// Checks whether one grid binding domain is allowed by the export guardrails.
    /// </summary>
    /// <param name="domain">Management domain stored by the field binding.</param>
    /// <param name="exportPreset">Export preset containing domain toggles.</param>
    /// <returns>True when the grid cell may read its Unity authoring value.</returns>
    private static bool AllowsDomain(ExcelDataTransferDomain domain, ExcelDataExportPreset exportPreset)
    {
        switch (domain)
        {
            case ExcelDataTransferDomain.Player:
                return exportPreset.IncludePlayerData;
            case ExcelDataTransferDomain.Enemy:
                return exportPreset.IncludeEnemyData;
            case ExcelDataTransferDomain.Game:
                return exportPreset.IncludeGameData;
            case ExcelDataTransferDomain.Waves:
            case ExcelDataTransferDomain.SpawnerAuthoring:
                return exportPreset.IncludeWaveData;
            default:
                return true;
        }
    }
    #endregion

    #region Diagnostics
    /// <summary>
    /// Converts cell-local snapshot warnings into a stable public operation result.
    /// </summary>
    /// <param name="cellRecords">Exact exported cell records to inspect.</param>
    /// <returns>Detailed warning list keyed by workbook coordinate.</returns>
    private static List<ExcelDataExportDiagnostic> BuildDiagnostics(
        IReadOnlyList<ExcelDataWorkbookExportCellRecord> cellRecords)
    {
        List<ExcelDataExportDiagnostic> diagnostics = new List<ExcelDataExportDiagnostic>();

        // Preserve worksheet and cell iteration order so UI/log diagnostics remain deterministic.
        for (int cellIndex = 0; cellIndex < cellRecords.Count; cellIndex++)
        {
            ExcelDataWorkbookExportCellRecord record = cellRecords[cellIndex];

            if (record.Snapshot == null || string.IsNullOrWhiteSpace(record.Snapshot.Warning))
                continue;

            ExcelDataFieldBinding binding = record.CellDefinition.FieldBinding;
            diagnostics.Add(new ExcelDataExportDiagnostic(record.SheetDefinition.SheetName,
                                                          record.CellDefinition.RowIndex,
                                                          record.CellDefinition.ColumnIndex,
                                                          binding == null ? string.Empty : binding.FieldId,
                                                          record.Snapshot.Warning));
        }

        return diagnostics;
    }
    #endregion

    #endregion
}
