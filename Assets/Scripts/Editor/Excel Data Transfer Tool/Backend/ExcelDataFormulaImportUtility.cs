using System;

/// <summary>
/// Resolves persisted Excel formula results according to import-preset trust policies.
/// </summary>
internal static class ExcelDataFormulaImportUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one import value from a literal MiniExcel scalar or an OpenXML formula cache.
    /// </summary>
    /// <param name="rawValue">Literal scalar read by MiniExcel.</param>
    /// <param name="technicalCell">Optional reference metadata for the same coordinate.</param>
    /// <param name="formulaCell">Optional formula and persisted-result metadata.</param>
    /// <param name="calculationMetadata">Workbook calculation flags used for freshness checks.</param>
    /// <param name="importPreset">Formula and reference import policies.</param>
    /// <returns>Effective typed value and formula diagnostics consumed by Preview and Apply.</returns>
    public static ExcelDataImportCellValue CreateImportValue(
        object rawValue,
        ExcelDataWorkbookTechnicalCellMetadata technicalCell,
        ExcelDataWorkbookFormulaCell formulaCell,
        ExcelDataWorkbookCalculationMetadata calculationMetadata,
        ExcelDataImportPreset importPreset)
    {
        string referenceName = technicalCell == null ? string.Empty : technicalCell.ReferenceName;
        string referenceGuid = technicalCell == null ? string.Empty : technicalCell.ReferenceGuid;
        string referencePath = technicalCell == null ? string.Empty : technicalCell.ReferencePath;

        if (formulaCell == null)
            return new ExcelDataImportCellValue(rawValue,
                                                referenceName,
                                                referenceGuid,
                                                referencePath);

        object effectiveValue = formulaCell.CachedResultSupported
            ? formulaCell.CachedValue
            : formulaCell.CachedError ? formulaCell.RawCachedValue : null;
        ExcelDataFormulaImportState state;
        bool canImport;
        string warning;
        ResolveFormulaState(formulaCell,
                            calculationMetadata,
                            importPreset,
                            out state,
                            out canImport,
                            out warning);
        return new ExcelDataImportCellValue(effectiveValue,
                                            referenceName,
                                            referenceGuid,
                                            referencePath,
                                            true,
                                            formulaCell.DisplayExpression,
                                            state,
                                            canImport,
                                            warning);
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves formula trust state without evaluating the expression inside Unity.
    /// </summary>
    /// <param name="formulaCell">Formula and persisted-result metadata.</param>
    /// <param name="calculationMetadata">Workbook calculation flags.</param>
    /// <param name="importPreset">Formula import policy.</param>
    /// <param name="state">Resolved formula state.</param>
    /// <param name="canImport">True when the cached result is eligible for typed preflight.</param>
    /// <param name="warning">Blocking or caution diagnostic.</param>
    private static void ResolveFormulaState(ExcelDataWorkbookFormulaCell formulaCell,
                                            ExcelDataWorkbookCalculationMetadata calculationMetadata,
                                            ExcelDataImportPreset importPreset,
                                            out ExcelDataFormulaImportState state,
                                            out bool canImport,
                                            out string warning)
    {
        if (importPreset.FormulaImportPolicy == ExcelDataFormulaImportPolicy.RejectFormulas)
        {
            state = ExcelDataFormulaImportState.RejectedByPolicy;
            canImport = false;
            warning = "Formula cells are rejected by the active Import Preset.";
            return;
        }

        if (!formulaCell.HasCachedResult)
        {
            state = ExcelDataFormulaImportState.MissingCachedResult;
            canImport = false;
            warning = "Formula has no persisted result. Recalculate and save the workbook in Excel before Preview Import.";
            return;
        }

        if (formulaCell.CachedError)
        {
            state = ExcelDataFormulaImportState.CachedError;
            canImport = false;
            warning = "Formula cached an Excel error: " + formulaCell.RawCachedValue + ".";
            return;
        }

        if (!formulaCell.CachedResultSupported)
        {
            state = ExcelDataFormulaImportState.UnsupportedCachedResult;
            canImport = false;
            warning = string.IsNullOrWhiteSpace(formulaCell.UnsupportedReason)
                ? "Formula cached result uses an unsupported OpenXML scalar representation."
                : formulaCell.UnsupportedReason;
            return;
        }

        if (calculationMetadata != null && calculationMetadata.PotentiallyStale)
        {
            warning = calculationMetadata.BuildStaleReason();

            if (importPreset.BlockPotentiallyStaleFormulaCaches)
            {
                state = ExcelDataFormulaImportState.UntrustedCachedResult;
                canImport = false;
                warning += " Recalculate and save the workbook, or disable strict stale-cache blocking deliberately.";
                return;
            }

            state = ExcelDataFormulaImportState.CachedResultWithWarning;
            canImport = true;
            warning += " The persisted result will be imported because strict stale-cache blocking is disabled.";
            return;
        }

        state = ExcelDataFormulaImportState.CachedResult;
        canImport = true;
        warning = string.Empty;
    }
    #endregion

    #endregion
}
