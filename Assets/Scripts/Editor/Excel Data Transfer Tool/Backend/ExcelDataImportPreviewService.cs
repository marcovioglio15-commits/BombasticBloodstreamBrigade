using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Builds read-only coordinate-exact import previews from the active grid-authoritative workbook layout.
/// </summary>
internal static class ExcelDataImportPreviewService
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads exact workbook cells, validates technical compatibility and preflights supported Unity writes.
    /// </summary>
    /// <param name="masterPreset">Master preset linking import policy and workbook layout.</param>
    /// <param name="overrideWorkbookPath">Optional workbook path used by tests and direct commands.</param>
    /// <returns>Cell-based preview diagnostics without mutating Unity assets.</returns>
    public static ExcelDataImportPreviewResult PreviewWorkbook(ExcelDataTransferMasterPreset masterPreset,
                                                               string overrideWorkbookPath)
    {
        ValidatePresetGraph(masterPreset);
        ExcelDataImportPreset importPreset = masterPreset.ImportPreset;
        ExcelDataWorkbookLayoutPreset layoutPreset = masterPreset.LayoutPreset;
        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveImportWorkbookPath(importPreset, overrideWorkbookPath);
        ExcelDataGridWorkbookReadResult readResult = ExcelDataGridWorkbookReader.ReadWorkbook(resolvedPath, layoutPreset);
        string currentLayoutHash = ExcelDataWorkbookLayoutHashUtility.Calculate(layoutPreset);
        List<string> blockingReasons = new List<string>();
        ValidateWorkbookCompatibility(readResult, layoutPreset, currentLayoutHash, blockingReasons);
        List<PreviewCandidate> candidates = BuildCandidates(layoutPreset, importPreset, readResult, blockingReasons);
        ValidateDuplicateFieldMappings(candidates, blockingReasons);

        if (importPreset.ConflictPolicy == ExcelDataImportConflictPolicy.PreviewOnly)
            AddUniqueReason(blockingReasons, "Import preset conflict policy is Preview Only.");

        List<ExcelDataImportPreviewRow> previewRows = BuildPreviewRows(candidates);
        int importableCellCount = CountImportableCells(candidates);

        if (importableCellCount <= 0)
            AddUniqueReason(blockingReasons, "No import-enabled Data Field cell passed preflight.");

        int warningCount = CountWarnings(candidates) + blockingReasons.Count;
        ExcelDataWorkbookTechnicalMetadata technicalMetadata = readResult.TechnicalMetadata;
        bool layoutHashMatches =
            technicalMetadata.WorkbookRecordFound &&
            string.Equals(technicalMetadata.LayoutHash, currentLayoutHash, StringComparison.Ordinal);
        return new ExcelDataImportPreviewResult(resolvedPath,
                                                previewRows.Count,
                                                importableCellCount,
                                                previewRows.Count - importableCellCount,
                                                warningCount,
                                                previewRows,
                                                blockingReasons.Count <= 0,
                                                layoutHashMatches,
                                                technicalMetadata.LayoutHash,
                                                currentLayoutHash,
                                                BuildValidationMessage(blockingReasons),
                                                readResult.WorkbookLastWriteUtcTicks);
    }
    #endregion

    #region Preset Validation
    /// <summary>
    /// Validates the minimum preset graph required by grid-exact import preview.
    /// </summary>
    /// <param name="masterPreset">Master preset graph to validate.</param>
    private static void ValidatePresetGraph(ExcelDataTransferMasterPreset masterPreset)
    {
        if (masterPreset == null)
            throw new ArgumentNullException(nameof(masterPreset));

        masterPreset.ValidateValues();

        if (masterPreset.ImportPreset == null)
            throw new InvalidOperationException("Missing Excel import preset.");

        if (masterPreset.LayoutPreset == null)
            throw new InvalidOperationException("Missing Excel workbook layout preset.");

        if (masterPreset.LayoutPreset.SheetDefinitions.Count <= 0)
            throw new InvalidOperationException("The linked layout has no grid-authoritative worksheet definitions.");
    }

    /// <summary>
    /// Validates schema, layout hash, required worksheets and sanitized sheet-name uniqueness.
    /// </summary>
    /// <param name="readResult">Raw workbook read result.</param>
    /// <param name="layoutPreset">Active layout preset.</param>
    /// <param name="currentLayoutHash">Current deterministic layout hash.</param>
    /// <param name="blockingReasons">Workbook-level blocking diagnostics.</param>
    private static void ValidateWorkbookCompatibility(ExcelDataGridWorkbookReadResult readResult,
                                                      ExcelDataWorkbookLayoutPreset layoutPreset,
                                                      string currentLayoutHash,
                                                      List<string> blockingReasons)
    {
        ExcelDataWorkbookTechnicalMetadata metadata = readResult.TechnicalMetadata;

        if (!metadata.SheetFound)
            AddUniqueReason(blockingReasons, "Workbook is missing the reserved _NashCoreTransfer worksheet required by schema v2 import.");
        else if (!metadata.WorkbookRecordFound)
            AddUniqueReason(blockingReasons, "Workbook technical worksheet has no Workbook record.");
        else if (!string.Equals(metadata.SchemaVersion, ExcelDataWorkbookTechnicalSheetBuilder.SchemaVersion, StringComparison.Ordinal))
            AddUniqueReason(blockingReasons, "Workbook schema version " + metadata.SchemaVersion +
                                               " does not match supported version " + ExcelDataWorkbookTechnicalSheetBuilder.SchemaVersion + ".");

        if (metadata.WorkbookRecordFound && !string.Equals(metadata.LayoutHash, currentLayoutHash, StringComparison.Ordinal))
            AddUniqueReason(blockingReasons, "Workbook layout hash does not match the active layout preset. Run Preview after selecting the exported layout or export a fresh workbook.");

        for (int missingIndex = 0; missingIndex < readResult.MissingSheetNames.Count; missingIndex++)
            AddUniqueReason(blockingReasons, "Workbook is missing import worksheet: " + readResult.MissingSheetNames[missingIndex] + ".");

        ValidateUniqueSheetNames(layoutPreset, blockingReasons);
    }

    /// <summary>
    /// Rejects import-enabled sheets whose visible names collide after Excel sanitization.
    /// </summary>
    /// <param name="layoutPreset">Active layout preset.</param>
    /// <param name="blockingReasons">Workbook-level blocking diagnostics.</param>
    private static void ValidateUniqueSheetNames(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                 List<string> blockingReasons)
    {
        HashSet<string> sheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet == null || !sheet.ImportEnabled)
                continue;

            string workbookSheetName =
                ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));

            if (!sheetNames.Add(workbookSheetName))
                AddUniqueReason(blockingReasons, "Import worksheet names collide after Excel sanitization: " + workbookSheetName + ".");
        }
    }
    #endregion

    #region Candidate Building
    /// <summary>
    /// Builds preflight candidates for every import-enabled Data Field and validated literal cell.
    /// </summary>
    /// <param name="layoutPreset">Active grid-authoritative layout.</param>
    /// <param name="importPreset">Import policy and domain guardrails.</param>
    /// <param name="readResult">Raw workbook values and technical metadata.</param>
    /// <param name="blockingReasons">Workbook-level blocking diagnostics.</param>
    /// <returns>Mutable candidates used for duplicate analysis before immutable UI rows are created.</returns>
    private static List<PreviewCandidate> BuildCandidates(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                          ExcelDataImportPreset importPreset,
                                                          ExcelDataGridWorkbookReadResult readResult,
                                                          List<string> blockingReasons)
    {
        List<PreviewCandidate> candidates = new List<PreviewCandidate>();
        Dictionary<string, PreviewCandidate> candidatesByCoordinate =
            new Dictionary<string, PreviewCandidate>(StringComparer.Ordinal);
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        // Preserve authored sheet and sparse-cell order so preview output matches the layout designer.
        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet == null || !sheet.ImportEnabled)
                continue;

            string workbookSheetName =
                ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));
            List<ExcelDataWorkbookCellDefinition> cells = sheet.Cells;

            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

                if (!IncludesImportPreview(cell))
                    continue;

                PreviewCandidate candidate = BuildCandidate(sheet,
                                                            workbookSheetName,
                                                            cell,
                                                            importPreset,
                                                            readResult);
                candidates.Add(candidate);
                ValidateCoordinateOwnership(sheet, candidate, candidatesByCoordinate, blockingReasons);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Builds one literal or Data Field candidate and performs non-persistent SerializedProperty preflight.
    /// </summary>
    /// <param name="sheet">Authored owner worksheet.</param>
    /// <param name="workbookSheetName">Sanitized visible worksheet name.</param>
    /// <param name="cell">Exact authored cell definition.</param>
    /// <param name="importPreset">Import policy and domain guardrails.</param>
    /// <param name="readResult">Raw workbook values and technical metadata.</param>
    /// <returns>Preflight candidate.</returns>
    private static PreviewCandidate BuildCandidate(ExcelDataWorkbookSheetDefinition sheet,
                                                   string workbookSheetName,
                                                   ExcelDataWorkbookCellDefinition cell,
                                                   ExcelDataImportPreset importPreset,
                                                   ExcelDataGridWorkbookReadResult readResult)
    {
        object rawValue = readResult.GetValue(sheet.SheetId, cell.RowIndex, cell.ColumnIndex);
        ExcelDataWorkbookTechnicalCellMetadata technicalCell =
            readResult.TechnicalMetadata.FindCell(workbookSheetName, cell.RowIndex, cell.ColumnIndex);
        ExcelDataImportCellValue incomingValue =
            new ExcelDataImportCellValue(rawValue,
                                         technicalCell == null ? string.Empty : technicalCell.ReferenceName,
                                         technicalCell == null ? string.Empty : technicalCell.ReferenceGuid,
                                         technicalCell == null ? string.Empty : technicalCell.ReferencePath);
        PreviewCandidate candidate = new PreviewCandidate(sheet.SheetName, cell, incomingValue);

        if (cell.ContentKind == ExcelDataWorkbookCellContentKind.LiteralText)
        {
            candidate.BindingResolved = true;
            candidate.IncludedByPreset = true;
            candidate.CurrentValue = cell.LiteralText ?? string.Empty;

            if (!string.Equals(candidate.CurrentValue, incomingValue.ValueText, StringComparison.Ordinal))
                candidate.AddWarning("Literal text differs from the authored layout value.");

            return candidate;
        }

        ExcelDataFieldBinding binding = cell.FieldBinding;

        if (binding == null || !binding.IsUsable())
        {
            candidate.AddWarning("Cell has no usable Data Field binding.");
            return candidate;
        }

        candidate.IncludedByPreset = AllowsDomain(binding.Domain, importPreset);

        if (!candidate.IncludedByPreset)
        {
            candidate.AddWarning("Domain is disabled by the import preset: " + binding.Domain + ".");
            return candidate;
        }

        if (!importPreset.IncludeConcreteListElements && binding.ConcreteListIndices.Count > 0)
        {
            candidate.AddWarning("Concrete list elements are disabled by the import preset.");
            return candidate;
        }

        Object asset;
        SerializedObject serializedObject;
        SerializedProperty property;
        string warning;

        if (!ExcelDataFieldBindingAssetUtility.TryResolveTarget(binding,
                                                                out asset,
                                                                out serializedObject,
                                                                out property,
                                                                out warning))
        {
            candidate.AddWarning(warning);
            return candidate;
        }

        candidate.BindingResolved = true;
        candidate.AssetName = asset.name;
        ExcelDataSerializedValueSnapshot currentSnapshot =
            ExcelDataSerializedValueReader.ReadValue(binding, true, true, true);
        candidate.CurrentValue = ConvertToInvariantText(currentSnapshot.Value);

        if (!string.IsNullOrWhiteSpace(currentSnapshot.Warning))
            candidate.AddWarning(currentSnapshot.Warning);

        // Write only to the temporary SerializedObject state, then discard it through Update.
        candidate.CanApply = ExcelDataImportPropertyWriterUtility.TryWriteProperty(property,
                                                                                   incomingValue,
                                                                                   importPreset,
                                                                                   out warning);
        serializedObject.Update();

        if (!candidate.CanApply)
            candidate.AddWarning(warning);

        return candidate;
    }

    /// <summary>
    /// Rejects cells owned by another sheet and duplicate import coordinates within one worksheet.
    /// </summary>
    /// <param name="sheet">Expected owner worksheet.</param>
    /// <param name="candidate">Candidate being registered.</param>
    /// <param name="candidatesByCoordinate">Previously registered candidates by sheet and address.</param>
    /// <param name="blockingReasons">Workbook-level blocking diagnostics.</param>
    private static void ValidateCoordinateOwnership(ExcelDataWorkbookSheetDefinition sheet,
                                                    PreviewCandidate candidate,
                                                    Dictionary<string, PreviewCandidate> candidatesByCoordinate,
                                                    List<string> blockingReasons)
    {
        ExcelDataWorkbookCellDefinition cell = candidate.CellDefinition;

        if (!string.Equals(cell.SheetId, sheet.SheetId, StringComparison.Ordinal))
        {
            candidate.CanApply = false;
            candidate.AddWarning("Cell owner Sheet ID does not match its containing worksheet.");
            AddUniqueReason(blockingReasons, "Layout contains cells assigned to the wrong worksheet.");
        }

        string coordinateKey = sheet.SheetId + ":" + ExcelDataWorkbookCoordinateUtility.BuildAddress(cell.RowIndex, cell.ColumnIndex);
        PreviewCandidate existingCandidate;

        if (!candidatesByCoordinate.TryGetValue(coordinateKey, out existingCandidate))
        {
            candidatesByCoordinate.Add(coordinateKey, candidate);
            return;
        }

        candidate.CanApply = false;
        existingCandidate.CanApply = false;
        candidate.AddWarning("Duplicate import mapping at the same worksheet coordinate.");
        existingCandidate.AddWarning("Duplicate import mapping at the same worksheet coordinate.");
        AddUniqueReason(blockingReasons, "Layout contains duplicate import coordinate " +
                                         sheet.SheetName + "!" + candidate.Address + ".");
    }

    /// <summary>
    /// Reports whether one cell participates in data import or explicit literal validation.
    /// </summary>
    /// <param name="cell">Cell definition to inspect.</param>
    /// <returns>True when preview must read the exact coordinate.</returns>
    private static bool IncludesImportPreview(ExcelDataWorkbookCellDefinition cell)
    {
        if (cell == null || !cell.IncludesImport() || cell.RowIndex < 1 || cell.ColumnIndex < 1)
            return false;

        return cell.ContentKind == ExcelDataWorkbookCellContentKind.DataField || cell.ValidateLiteralDuringImport;
    }
    #endregion

    #region Duplicate Field Validation
    /// <summary>
    /// Applies one-value-per-field semantics across multiple import-enabled coordinates.
    /// </summary>
    /// <param name="candidates">Preflight candidates to compare.</param>
    /// <param name="blockingReasons">Workbook-level blocking diagnostics.</param>
    private static void ValidateDuplicateFieldMappings(List<PreviewCandidate> candidates,
                                                       List<string> blockingReasons)
    {
        Dictionary<string, PreviewCandidate> candidatesByField =
            new Dictionary<string, PreviewCandidate>(StringComparer.Ordinal);

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            PreviewCandidate candidate = candidates[candidateIndex];

            if (candidate.CellDefinition.ContentKind != ExcelDataWorkbookCellContentKind.DataField ||
                !candidate.BindingResolved ||
                !candidate.IncludedByPreset)
                continue;

            string fieldIdentity = BuildFieldIdentity(candidate.CellDefinition.FieldBinding);
            PreviewCandidate existingCandidate;

            if (!candidatesByField.TryGetValue(fieldIdentity, out existingCandidate))
            {
                candidatesByField.Add(fieldIdentity, candidate);
                continue;
            }

            if (string.Equals(existingCandidate.IncomingValue.ComparisonToken,
                              candidate.IncomingValue.ComparisonToken,
                              StringComparison.Ordinal))
            {
                candidate.CanApply = false;
                candidate.AddWarning("Duplicate field mapping has the same value and will be applied only from " +
                                     existingCandidate.SheetName + "!" + existingCandidate.Address + ".");
                continue;
            }

            existingCandidate.CanApply = false;
            candidate.CanApply = false;
            existingCandidate.AddWarning("Duplicate field mapping contains conflicting workbook values.");
            candidate.AddWarning("Duplicate field mapping contains conflicting workbook values.");
            AddUniqueReason(blockingReasons, "Field " + fieldIdentity + " has conflicting values at multiple import coordinates.");
        }
    }

    /// <summary>
    /// Builds a stable duplicate-detection identity from field ID or owner and property fallbacks.
    /// </summary>
    /// <param name="binding">Field binding to identify.</param>
    /// <returns>Stable field identity.</returns>
    private static string BuildFieldIdentity(ExcelDataFieldBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.FieldId))
            return binding.FieldId;

        return binding.OwnerAssetGuid + ":" + binding.SerializedPath;
    }
    #endregion

    #region Result Building
    /// <summary>
    /// Converts mutable validation candidates into immutable rows consumed by editor UI and apply.
    /// </summary>
    /// <param name="candidates">Validated preview candidates.</param>
    /// <returns>Immutable preview rows preserving authored order.</returns>
    private static List<ExcelDataImportPreviewRow> BuildPreviewRows(List<PreviewCandidate> candidates)
    {
        List<ExcelDataImportPreviewRow> rows = new List<ExcelDataImportPreviewRow>(candidates.Count);

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            PreviewCandidate candidate = candidates[candidateIndex];
            rows.Add(new ExcelDataImportPreviewRow(candidate.SheetName,
                                                   candidate.CellDefinition,
                                                   candidate.IncomingValue,
                                                   candidate.AssetName,
                                                   candidate.CurrentValue,
                                                   candidate.BindingResolved,
                                                   candidate.IncludedByPreset,
                                                   candidate.CanApply,
                                                   candidate.Warning));
        }

        return rows;
    }

    /// <summary>
    /// Counts Data Field candidates approved by local preflight.
    /// </summary>
    /// <param name="candidates">Validated preview candidates.</param>
    /// <returns>Locally applicable Data Field cell count.</returns>
    private static int CountImportableCells(List<PreviewCandidate> candidates)
    {
        int count = 0;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            if (candidates[candidateIndex].CanApply)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Counts cell-local warnings after duplicate validation.
    /// </summary>
    /// <param name="candidates">Validated preview candidates.</param>
    /// <returns>Number of cells carrying warnings.</returns>
    private static int CountWarnings(List<PreviewCandidate> candidates)
    {
        int count = 0;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            if (!string.IsNullOrWhiteSpace(candidates[candidateIndex].Warning))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Builds a concise workbook-level validation message for the editor UI.
    /// </summary>
    /// <param name="blockingReasons">Blocking diagnostics in validation order.</param>
    /// <returns>Compatibility status or concatenated blocking diagnostics.</returns>
    private static string BuildValidationMessage(List<string> blockingReasons)
    {
        return blockingReasons.Count <= 0
            ? "Workbook schema and layout hash match the active grid-authoritative layout."
            : string.Join(" ", blockingReasons);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one binding domain is enabled by import guardrails.
    /// </summary>
    /// <param name="domain">Management domain stored by the binding.</param>
    /// <param name="importPreset">Import preset containing domain toggles.</param>
    /// <returns>True when the exact mapped cell may participate in import.</returns>
    private static bool AllowsDomain(ExcelDataTransferDomain domain, ExcelDataImportPreset importPreset)
    {
        switch (domain)
        {
            case ExcelDataTransferDomain.Player:
                return importPreset.IncludePlayerData;
            case ExcelDataTransferDomain.Enemy:
                return importPreset.IncludeEnemyData;
            case ExcelDataTransferDomain.Game:
                return importPreset.IncludeGameData;
            case ExcelDataTransferDomain.Waves:
            case ExcelDataTransferDomain.SpawnerAuthoring:
                return importPreset.IncludeWaveData;
            default:
                return true;
        }
    }

    /// <summary>
    /// Converts a current typed Unity value into invariant preview text.
    /// </summary>
    /// <param name="value">Typed serialized value.</param>
    /// <returns>Invariant text, or an empty string.</returns>
    private static string ConvertToInvariantText(object value)
    {
        if (value == null)
            return string.Empty;

        IFormattable formattable = value as IFormattable;

        if (formattable != null)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Adds one workbook-level diagnostic only when it is not already present.
    /// </summary>
    /// <param name="blockingReasons">Blocking diagnostic collection.</param>
    /// <param name="reason">Diagnostic to add.</param>
    private static void AddUniqueReason(List<string> blockingReasons, string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason) && !blockingReasons.Contains(reason))
            blockingReasons.Add(reason);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Mutable preflight state used until duplicate-coordinate and duplicate-field validation completes.
    /// </summary>
    private sealed class PreviewCandidate
    {
        #region Properties
        public string SheetName
        {
            get;
        }

        public string Address
        {
            get;
        }

        public ExcelDataWorkbookCellDefinition CellDefinition
        {
            get;
        }

        public ExcelDataImportCellValue IncomingValue
        {
            get;
        }

        public string AssetName
        {
            get;
            set;
        }

        public string CurrentValue
        {
            get;
            set;
        }

        public bool BindingResolved
        {
            get;
            set;
        }

        public bool IncludedByPreset
        {
            get;
            set;
        }

        public bool CanApply
        {
            get;
            set;
        }

        public string Warning
        {
            get;
            private set;
        }
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one empty validation candidate for an exact authored coordinate.
        /// </summary>
        /// <param name="sheetName">Visible authored worksheet name.</param>
        /// <param name="cellDefinition">Exact authored cell definition.</param>
        /// <param name="incomingValue">Raw incoming value and reference metadata.</param>
        public PreviewCandidate(string sheetName,
                                ExcelDataWorkbookCellDefinition cellDefinition,
                                ExcelDataImportCellValue incomingValue)
        {
            SheetName = sheetName ?? string.Empty;
            CellDefinition = cellDefinition;
            IncomingValue = incomingValue;
            Address = ExcelDataWorkbookCoordinateUtility.BuildAddress(cellDefinition.RowIndex, cellDefinition.ColumnIndex);
            AssetName = string.Empty;
            CurrentValue = string.Empty;
            Warning = string.Empty;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Appends one distinct cell-local warning without discarding earlier diagnostics.
        /// </summary>
        /// <param name="warning">Warning to append.</param>
        public void AddWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning) || Warning.Contains(warning))
                return;

            Warning = string.IsNullOrWhiteSpace(Warning) ? warning : Warning + " " + warning;
        }
        #endregion

        #endregion
    }
    #endregion
}
