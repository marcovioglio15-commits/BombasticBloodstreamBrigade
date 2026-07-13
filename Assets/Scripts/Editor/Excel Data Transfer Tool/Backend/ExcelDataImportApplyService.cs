using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Applies importable workbook rows to Unity ScriptableObject assets through SerializedProperty APIs.
/// </summary>
internal static class ExcelDataImportApplyService
{
    #region Constants
    private const string ObjectSection = "Object";
    private const string BrushGridSection = "BrushGrid";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies workbook values to mapped serialized fields after optional preview validation.
    /// </summary>
    /// <param name="masterPreset">Master preset that links import and layout settings.</param>
    /// <param name="overrideWorkbookPath">Optional workbook path used by tests or manual commands.</param>
    /// <param name="previewResult">Latest preview result, required when the import preset requires preview.</param>
    /// <returns>Import apply result with applied/skipped/warning row counts.</returns>
    public static ExcelDataImportApplyResult ApplyWorkbook(ExcelDataTransferMasterPreset masterPreset,
                                                           string overrideWorkbookPath,
                                                           ExcelDataImportPreviewResult previewResult)
    {
        if (masterPreset == null)
            throw new ArgumentNullException(nameof(masterPreset));

        masterPreset.ValidateValues();

        if (masterPreset.ImportPreset == null)
            throw new InvalidOperationException("Missing Excel import preset.");

        if (masterPreset.LayoutPreset == null)
            throw new InvalidOperationException("Missing Excel workbook layout preset.");

        if (masterPreset.ImportPreset.ConflictPolicy == ExcelDataImportConflictPolicy.PreviewOnly)
            throw new InvalidOperationException("Import preset conflict policy is Preview Only.");

        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveImportWorkbookPath(masterPreset.ImportPreset, overrideWorkbookPath);
        ValidatePreviewRequirement(masterPreset.ImportPreset, previewResult, resolvedPath);

        List<ExcelDataWorkbookRow> workbookRows = ExcelDataWorkbookReader.LoadWorkbookRows(resolvedPath, masterPreset.LayoutPreset.ObjectsSheetName);
        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById = BuildEntryLookup(ExcelDataFieldCatalogBuilder.BuildCatalog());
        HashSet<string> selectedFieldIds = BuildSelectedFieldSet(masterPreset.ImportPreset);
        HashSet<Object> recordedAssets = new HashSet<Object>();
        int appliedRows = 0;
        int skippedRows = 0;
        int warningRows = 0;

        for (int rowIndex = 0; rowIndex < workbookRows.Count; rowIndex++)
        {
            string warning;

            if (TryApplyWorkbookRow(workbookRows[rowIndex],
                                    masterPreset.ImportPreset,
                                    entriesById,
                                    selectedFieldIds,
                                    recordedAssets,
                                    out warning))
            {
                appliedRows++;
                continue;
            }

            skippedRows++;

            if (!string.IsNullOrWhiteSpace(warning))
                warningRows++;
        }

        AssetDatabase.SaveAssets();
        return new ExcelDataImportApplyResult(resolvedPath, appliedRows, skippedRows, warningRows);
    }
    #endregion

    #region Row Application
    /// <summary>
    /// Applies one workbook row when it maps to an allowed catalog field.
    /// </summary>
    /// <param name="workbookRow">Workbook row read from disk.</param>
    /// <param name="importPreset">Import preset controlling filters and policies.</param>
    /// <param name="entriesById">Catalog entries keyed by stable field id.</param>
    /// <param name="selectedFieldIds">Explicit import selection set.</param>
    /// <param name="recordedAssets">Assets already recorded for Undo in this operation.</param>
    /// <param name="warning">Warning generated for skipped rows.</param>
    /// <returns>True when the row was applied.</returns>
    private static bool TryApplyWorkbookRow(ExcelDataWorkbookRow workbookRow,
                                            ExcelDataImportPreset importPreset,
                                            Dictionary<string, ExcelDataFieldCatalogEntry> entriesById,
                                            HashSet<string> selectedFieldIds,
                                            HashSet<Object> recordedAssets,
                                            out string warning)
    {
        warning = string.Empty;

        if (workbookRow == null || !IsImportDataSection(workbookRow.Section))
            return false;

        if (string.IsNullOrWhiteSpace(workbookRow.FieldId))
        {
            warning = "Workbook row has no field id.";
            return false;
        }

        ExcelDataFieldCatalogEntry entry = null;

        if (!entriesById.TryGetValue(workbookRow.FieldId, out entry))
        {
            warning = "Field id is not present in the current Unity catalog.";
            return false;
        }

        if (!AllowsDomain(entry, importPreset))
            return false;

        if (selectedFieldIds.Count > 0 && !selectedFieldIds.Contains(workbookRow.FieldId))
            return false;

        Object asset = AssetDatabase.LoadAssetAtPath<Object>(entry.AssetPath);

        if (asset == null)
        {
            warning = "Missing target asset: " + entry.AssetPath;
            return false;
        }

        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty property = serializedObject.FindProperty(entry.SerializedPath);

        if (property == null)
        {
            warning = "Missing serialized property: " + entry.SerializedPath;
            return false;
        }

        if (!ExcelDataImportPropertyWriterUtility.TryWriteProperty(property, workbookRow, importPreset, out warning))
            return false;

        if (!recordedAssets.Contains(asset))
        {
            Undo.RecordObject(asset, "Apply Excel Data Import");
            recordedAssets.Add(asset);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        return true;
    }
    #endregion

    #region Filtering
    /// <summary>
    /// Validates that a recent preview exists when required by the preset.
    /// </summary>
    /// <param name="importPreset">Import preset controlling preview requirement.</param>
    /// <param name="previewResult">Latest preview result from the UI.</param>
    /// <param name="resolvedPath">Resolved workbook path about to be applied.</param>
    private static void ValidatePreviewRequirement(ExcelDataImportPreset importPreset,
                                                   ExcelDataImportPreviewResult previewResult,
                                                   string resolvedPath)
    {
        if (!importPreset.RequirePreviewBeforeApply)
            return;

        if (previewResult == null)
            throw new InvalidOperationException("Run Preview Import before Apply Import.");

        if (!string.Equals(previewResult.WorkbookPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Preview result does not match the configured workbook path.");
    }

    /// <summary>
    /// Checks whether one normalized workbook section can participate in import apply.
    /// </summary>
    /// <param name="section">Workbook section value.</param>
    /// <returns>True when the section can contain imported object data.</returns>
    private static bool IsImportDataSection(string section)
    {
        return string.Equals(section, ObjectSection, StringComparison.Ordinal) ||
               string.Equals(section, BrushGridSection, StringComparison.Ordinal);
    }

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

    #endregion
}
