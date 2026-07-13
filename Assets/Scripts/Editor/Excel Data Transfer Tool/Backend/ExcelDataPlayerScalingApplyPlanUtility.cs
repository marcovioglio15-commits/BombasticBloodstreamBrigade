using System;
using System.Collections.Generic;

/// <summary>
/// Rebuilds Player scaling semantic routes from an approved preview immediately before apply.
/// </summary>
internal static class ExcelDataPlayerScalingApplyPlanUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Revalidates approved Player cells against current authoring state and rejects any semantic drift.
    /// </summary>
    /// <param name="previewResult">Approved coordinate-exact preview.</param>
    /// <param name="importPreset">Import preset containing scaling list policy.</param>
    /// <returns>Fresh atomic route and creation plan.</returns>
    public static ExcelDataPlayerScalingImportPlan Build(ExcelDataImportPreviewResult previewResult,
                                                         ExcelDataImportPreset importPreset)
    {
        List<ExcelDataPlayerScalingImportCell> cells = new List<ExcelDataPlayerScalingImportCell>();

        for (int rowIndex = 0; rowIndex < previewResult.Rows.Count; rowIndex++)
        {
            ExcelDataImportPreviewRow row = previewResult.Rows[rowIndex];

            if (!row.CanApply || row.CellDefinition == null ||
                row.CellDefinition.ContentKind != ExcelDataWorkbookCellContentKind.DataField)
                continue;

            cells.Add(new ExcelDataPlayerScalingImportCell(row.SheetName,
                                                           row.Address,
                                                           row.CellDefinition,
                                                           row.IncomingValue));
        }

        ExcelDataPlayerScalingImportPlan plan = ExcelDataPlayerScalingImportPlanBuilder.Build(cells, importPreset);

        if (!plan.IsValid)
            throw BuildPreflightException(plan.Diagnostics[0]);

        return plan;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds one coordinate-specific exception for semantic drift detected after preview.
    /// </summary>
    /// <param name="diagnostic">First blocking Player scaling diagnostic.</param>
    /// <returns>Invalid operation exception suitable for the import status panel.</returns>
    private static InvalidOperationException BuildPreflightException(
        ExcelDataPlayerScalingImportDiagnostic diagnostic)
    {
        string coordinate = diagnostic == null || string.IsNullOrWhiteSpace(diagnostic.SheetName) ||
                            string.IsNullOrWhiteSpace(diagnostic.Address)
            ? string.Empty
            : " at " + diagnostic.SheetName + "!" + diagnostic.Address;
        string message = diagnostic == null ? "Unknown Player scaling semantic failure." : diagnostic.Message;
        return new InvalidOperationException("Player scaling apply preflight failed" + coordinate + ": " + message);
    }
    #endregion

    #endregion
}
