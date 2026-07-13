using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Builds read-only import previews by comparing workbook rows with the current field catalog.
/// </summary>
internal static class ExcelDataImportPreviewService
{
    #region Constants
    private const string ObjectSection = "Object";
    private const string BrushGridSection = "BrushGrid";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reads the configured workbook and reports which rows can be imported by the active preset.
    /// </summary>
    /// <param name="masterPreset">Master preset that links import and layout settings.</param>
    /// <param name="overrideWorkbookPath">Optional workbook path used by tests and manual previews.</param>
    /// <returns>Import preview diagnostics without mutating Unity assets.</returns>
    public static ExcelDataImportPreviewResult PreviewWorkbook(ExcelDataTransferMasterPreset masterPreset,
                                                               string overrideWorkbookPath)
    {
        if (masterPreset == null)
            throw new ArgumentNullException(nameof(masterPreset));

        masterPreset.ValidateValues();

        if (masterPreset.ImportPreset == null)
            throw new InvalidOperationException("Missing Excel import preset.");

        if (masterPreset.LayoutPreset == null)
            throw new InvalidOperationException("Missing Excel workbook layout preset.");

        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveImportWorkbookPath(masterPreset.ImportPreset, overrideWorkbookPath);
        List<ExcelDataWorkbookRow> workbookRows = ExcelDataWorkbookReader.LoadWorkbookRows(resolvedPath, masterPreset.LayoutPreset.ObjectsSheetName);
        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById = BuildEntryLookup(ExcelDataFieldCatalogBuilder.BuildCatalog());
        HashSet<string> selectedFieldIds = BuildSelectedFieldSet(masterPreset.ImportPreset);
        List<ExcelDataImportPreviewRow> previewRows = new List<ExcelDataImportPreviewRow>();
        int importableRows = 0;
        int skippedRows = 0;
        int warningRows = 0;

        for (int rowIndex = 0; rowIndex < workbookRows.Count; rowIndex++)
        {
            ExcelDataWorkbookRow workbookRow = workbookRows[rowIndex];
            PreviewDecision decision = EvaluateWorkbookRow(workbookRow, masterPreset.ImportPreset, entriesById, selectedFieldIds);

            if (decision.IncludedByPreset && decision.CatalogMatched)
                importableRows++;
            else
                skippedRows++;

            if (!string.IsNullOrWhiteSpace(decision.Warning))
                warningRows++;

            previewRows.Add(new ExcelDataImportPreviewRow(rowIndex + 2,
                                                          workbookRow,
                                                          decision.CatalogMatched,
                                                          decision.IncludedByPreset,
                                                          decision.Warning));
        }

        return new ExcelDataImportPreviewResult(resolvedPath,
                                                workbookRows.Count,
                                                importableRows,
                                                skippedRows,
                                                warningRows,
                                                previewRows);
    }
    #endregion

    #region Evaluation
    /// <summary>
    /// Evaluates one workbook row against the current catalog and import preset.
    /// </summary>
    /// <param name="workbookRow">Workbook row read from MiniExcel.</param>
    /// <param name="importPreset">Import preset containing domain and field filters.</param>
    /// <param name="entriesById">Current catalog lookup by field id.</param>
    /// <param name="selectedFieldIds">Explicit import selections, or empty for layout-driven import.</param>
    /// <returns>Preview decision for the row.</returns>
    private static PreviewDecision EvaluateWorkbookRow(ExcelDataWorkbookRow workbookRow,
                                                       ExcelDataImportPreset importPreset,
                                                       Dictionary<string, ExcelDataFieldCatalogEntry> entriesById,
                                                       HashSet<string> selectedFieldIds)
    {
        if (workbookRow == null)
            return new PreviewDecision(false, false, "Empty workbook row.");

        if (!IsImportDataSection(workbookRow.Section))
            return new PreviewDecision(false, false, "Metadata row skipped.");

        if (string.IsNullOrWhiteSpace(workbookRow.FieldId))
            return new PreviewDecision(false, false, "Workbook row has no field id.");

        ExcelDataFieldCatalogEntry entry = null;

        if (!entriesById.TryGetValue(workbookRow.FieldId, out entry))
            return new PreviewDecision(false, false, "Field id is not present in the current Unity catalog.");

        if (!AllowsDomain(entry, importPreset))
            return new PreviewDecision(true, false, "Domain is disabled by the import preset.");

        if (selectedFieldIds.Count > 0 && !selectedFieldIds.Contains(workbookRow.FieldId))
            return new PreviewDecision(true, false, "Field is not in the explicit import selection.");

        string referenceWarning = BuildReferenceWarning(workbookRow, entry, importPreset);
        return new PreviewDecision(true, true, referenceWarning);
    }

    /// <summary>
    /// Checks whether one normalized workbook section can participate in import preview.
    /// </summary>
    /// <param name="section">Workbook section value.</param>
    /// <returns>True when the section can contain imported object data.</returns>
    private static bool IsImportDataSection(string section)
    {
        return string.Equals(section, ObjectSection, StringComparison.Ordinal) ||
               string.Equals(section, BrushGridSection, StringComparison.Ordinal);
    }
    #endregion

    #region Filtering
    /// <summary>
    /// Checks whether the import preset allows the catalog entry domain.
    /// </summary>
    /// <param name="entry">Catalog entry to test.</param>
    /// <param name="importPreset">Import preset containing domain toggles.</param>
    /// <returns>True when the domain is enabled.</returns>
    private static bool AllowsDomain(ExcelDataFieldCatalogEntry entry, ExcelDataImportPreset importPreset)
    {
        switch (entry.Domain)
        {
            case ExcelDataTransferDomain.Player:
                return importPreset.IncludePlayerData;
            case ExcelDataTransferDomain.Enemy:
                return importPreset.IncludeEnemyData;
            case ExcelDataTransferDomain.Game:
                return importPreset.IncludeGameData;
            case ExcelDataTransferDomain.Waves:
                return importPreset.IncludeWaveData;
            default:
                return false;
        }
    }

    /// <summary>
    /// Builds the explicit import selection set from the import preset.
    /// </summary>
    /// <param name="importPreset">Import preset that stores selected fields.</param>
    /// <returns>Set of selected field ids enabled for import.</returns>
    private static HashSet<string> BuildSelectedFieldSet(ExcelDataImportPreset importPreset)
    {
        HashSet<string> selectedFieldIds = new HashSet<string>();
        List<ExcelDataFieldSelection> selectedFields = importPreset.SelectedFields;

        for (int selectionIndex = 0; selectionIndex < selectedFields.Count; selectionIndex++)
        {
            ExcelDataFieldSelection selection = selectedFields[selectionIndex];

            if (selection == null || !selection.ImportEnabled)
                continue;

            if (string.IsNullOrWhiteSpace(selection.FieldId))
                continue;

            selectedFieldIds.Add(selection.FieldId);
        }

        return selectedFieldIds;
    }
    #endregion

    #region Lookups
    /// <summary>
    /// Builds a field-id lookup for catalog entries.
    /// </summary>
    /// <param name="entries">Catalog entries to index.</param>
    /// <returns>Dictionary keyed by field id.</returns>
    private static Dictionary<string, ExcelDataFieldCatalogEntry> BuildEntryLookup(List<ExcelDataFieldCatalogEntry> entries)
    {
        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById = new Dictionary<string, ExcelDataFieldCatalogEntry>();

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = entries[entryIndex];

            if (entry == null || string.IsNullOrWhiteSpace(entry.FieldId))
                continue;

            entriesById[entry.FieldId] = entry;
        }

        return entriesById;
    }
    #endregion

    #region Reference Diagnostics
    /// <summary>
    /// Builds a warning for object-reference rows that rely on ambiguous asset names.
    /// </summary>
    /// <param name="workbookRow">Workbook row containing reference metadata.</param>
    /// <param name="entry">Catalog entry matched by field id.</param>
    /// <param name="importPreset">Import preset controlling ambiguity policy.</param>
    /// <returns>Warning text, or empty when the reference is safe enough for preview.</returns>
    private static string BuildReferenceWarning(ExcelDataWorkbookRow workbookRow,
                                                ExcelDataFieldCatalogEntry entry,
                                                ExcelDataImportPreset importPreset)
    {
        if (entry.DataKind != ExcelDataBrushDataKind.ObjectReference)
            return string.Empty;

        if (!importPreset.BlockAmbiguousReferences)
            return string.Empty;

        string referenceName = string.IsNullOrWhiteSpace(workbookRow.ReferenceName) ? workbookRow.Value : workbookRow.ReferenceName;

        if (string.IsNullOrWhiteSpace(referenceName))
            return string.Empty;

        int exactMatches = CountExactAssetNameMatches(referenceName);

        if (exactMatches <= 1)
            return string.Empty;

        return "Ambiguous asset reference name: " + referenceName + " has " +
               exactMatches.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               " exact project matches.";
    }

    /// <summary>
    /// Counts project assets whose object name exactly matches the provided reference name.
    /// </summary>
    /// <param name="referenceName">Asset name written in the workbook.</param>
    /// <returns>Exact asset-name match count.</returns>
    private static int CountExactAssetNameMatches(string referenceName)
    {
        string[] guids = AssetDatabase.FindAssets(referenceName);
        int exactMatches = 0;

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            if (asset == null)
                continue;

            if (asset.name == referenceName)
                exactMatches++;
        }

        return exactMatches;
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Internal row evaluation result used before materializing preview rows.
    /// </summary>
    private readonly struct PreviewDecision
    {
        #region Fields
        public readonly bool CatalogMatched;
        public readonly bool IncludedByPreset;
        public readonly string Warning;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one immutable decision for an import preview row.
        /// </summary>
        /// <param name="catalogMatched">True when the field id matched the current catalog.</param>
        /// <param name="includedByPreset">True when preset filters allow this row.</param>
        /// <param name="warning">Warning text for the preview row.</param>
        public PreviewDecision(bool catalogMatched, bool includedByPreset, string warning)
        {
            CatalogMatched = catalogMatched;
            IncludedByPreset = includedByPreset;
            Warning = warning;
        }
        #endregion

        #endregion
    }
    #endregion
}
