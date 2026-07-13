using System;
using System.Collections.Generic;

/// <summary>
/// Bridges mutable import preview candidates to the shared Player scaling semantic planner.
/// </summary>
internal static class ExcelDataPlayerScalingPreviewUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs combined Player scaling preflight and applies coordinate-specific failures to preview candidates.
    /// </summary>
    /// <param name="candidates">Locally parsed preview candidates after duplicate mapping validation.</param>
    /// <param name="importPreset">Import preset containing scaling list policy.</param>
    /// <param name="blockingReasons">Workbook-level diagnostics that determine preview approval.</param>
    public static void Validate(IReadOnlyList<ExcelDataImportPreviewService.PreviewCandidate> candidates,
                                ExcelDataImportPreset importPreset,
                                List<string> blockingReasons)
    {
        if (candidates == null || importPreset == null || blockingReasons == null)
            return;

        List<ExcelDataPlayerScalingImportCell> cells = new List<ExcelDataPlayerScalingImportCell>();
        Dictionary<ExcelDataWorkbookCellDefinition, ExcelDataImportPreviewService.PreviewCandidate> candidatesByCell =
            new Dictionary<ExcelDataWorkbookCellDefinition, ExcelDataImportPreviewService.PreviewCandidate>();

        // Include only locally approved Data Field cells so semantic validation cannot revive an earlier failure.
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            ExcelDataImportPreviewService.PreviewCandidate candidate = candidates[candidateIndex];

            if (candidate == null || !candidate.CanApply || candidate.CellDefinition == null ||
                candidate.CellDefinition.ContentKind != ExcelDataWorkbookCellContentKind.DataField)
                continue;

            ExcelDataPlayerScalingImportCell cell =
                new ExcelDataPlayerScalingImportCell(candidate.SheetName,
                                                     candidate.Address,
                                                     candidate.CellDefinition,
                                                     candidate.IncomingValue);
            cells.Add(cell);
            candidatesByCell[candidate.CellDefinition] = candidate;
        }

        ExcelDataPlayerScalingImportPlan plan = ExcelDataPlayerScalingImportPlanBuilder.Build(cells, importPreset);

        for (int diagnosticIndex = 0; diagnosticIndex < plan.Diagnostics.Count; diagnosticIndex++)
        {
            ExcelDataPlayerScalingImportDiagnostic diagnostic = plan.Diagnostics[diagnosticIndex];
            ExcelDataImportPreviewService.PreviewCandidate candidate;

            if (diagnostic.CellDefinition != null &&
                candidatesByCell.TryGetValue(diagnostic.CellDefinition, out candidate))
            {
                candidate.CanApply = false;
                candidate.AddWarning(diagnostic.Message);
            }

            string coordinate = string.IsNullOrWhiteSpace(diagnostic.SheetName) ||
                                string.IsNullOrWhiteSpace(diagnostic.Address)
                ? string.Empty
                : " at " + diagnostic.SheetName + "!" + diagnostic.Address;
            AddUniqueReason(blockingReasons,
                            "Player scaling semantic preflight failed" + coordinate + ": " +
                            diagnostic.Message);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds one workbook-level semantic diagnostic only once.
    /// </summary>
    /// <param name="blockingReasons">Workbook-level diagnostic collection.</param>
    /// <param name="reason">Reason to add.</param>
    private static void AddUniqueReason(List<string> blockingReasons, string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason) && !blockingReasons.Contains(reason))
            blockingReasons.Add(reason);
    }
    #endregion

    #endregion
}
