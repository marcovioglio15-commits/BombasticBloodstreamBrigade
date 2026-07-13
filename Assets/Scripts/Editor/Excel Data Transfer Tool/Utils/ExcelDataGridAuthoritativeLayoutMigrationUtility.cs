using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporarily converts legacy brush mappings into grid-authoritative sheet and cell definitions.
/// Remove this utility after every required project and external workbook layout has been migrated.
/// </summary>
public static class ExcelDataGridAuthoritativeLayoutMigrationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts every project layout preset that still has legacy mappings and no new sheet definitions.
    /// </summary>
    public static void ExecuteProjectMigration()
    {
        List<ExcelDataWorkbookLayoutPreset> layoutPresets = LoadProjectLayoutPresets();
        bool requiresCatalog = false;

        // Avoid the expensive one-time catalog scan when all project layouts are already converted.
        for (int layoutIndex = 0; layoutIndex < layoutPresets.Count; layoutIndex++)
        {
            ExcelDataWorkbookLayoutPreset layoutPreset = layoutPresets[layoutIndex];

            if (layoutPreset == null)
                continue;

            if (layoutPreset.SheetDefinitions.Count <= 0 && layoutPreset.CellMappings.Count > 0)
            {
                requiresCatalog = true;
                break;
            }
        }

        if (!requiresCatalog)
        {
            Debug.Log("[ExcelDataGridAuthoritativeLayoutMigration] No project layout preset requires conversion.");
            return;
        }

        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById =
            BuildEntryLookup(ExcelDataFieldCatalogBuilder.BuildCatalog());
        int convertedPresetCount = 0;
        int convertedCellCount = 0;
        int unresolvedCellCount = 0;
        int duplicateCellCount = 0;

        // Convert each layout once and aggregate diagnostics for the batch log.
        for (int layoutIndex = 0; layoutIndex < layoutPresets.Count; layoutIndex++)
        {
            ExcelDataGridAuthoritativeMigrationResult result =
                ConvertPreset(layoutPresets[layoutIndex], entriesById, false);

            if (result.WasSkipped)
                continue;

            convertedPresetCount++;
            convertedCellCount += result.ConvertedCellCount;
            unresolvedCellCount += result.UnresolvedCellCount;
            duplicateCellCount += result.DuplicateCellCount;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ExcelDataGridAuthoritativeLayoutMigration] PASS - presets: " + convertedPresetCount +
                  ", cells: " + convertedCellCount +
                  ", unresolved: " + unresolvedCellCount +
                  ", duplicates: " + duplicateCellCount + ".");
    }
    #endregion

    #region Internal Methods
    /// <summary>
    /// Converts one legacy layout preset while preserving every usable field identifier and coordinate.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving grid-authoritative definitions.</param>
    /// <param name="entriesById">Current field catalog entries keyed by stable field ID.</param>
    /// <param name="overwriteExisting">True only for isolated tests that intentionally replace new definitions.</param>
    /// <returns>Conversion diagnostics for the processed preset.</returns>
    internal static ExcelDataGridAuthoritativeMigrationResult ConvertPreset(
        ExcelDataWorkbookLayoutPreset layoutPreset,
        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById,
        bool overwriteExisting)
    {
        if (layoutPreset == null)
            return new ExcelDataGridAuthoritativeMigrationResult(true, 0, 0, 0);

        if (layoutPreset.SheetDefinitions.Count > 0 && !overwriteExisting)
            return new ExcelDataGridAuthoritativeMigrationResult(true, 0, 0, 0);

        bool persistentAsset = EditorUtility.IsPersistent(layoutPreset);

        if (persistentAsset)
            Undo.RecordObject(layoutPreset, "Convert Excel Workbook Layout");

        layoutPreset.SheetDefinitions.Clear();
        Dictionary<string, ExcelDataWorkbookSheetDefinition> sheetsByName =
            new Dictionary<string, ExcelDataWorkbookSheetDefinition>(StringComparer.Ordinal);
        int convertedCellCount = 0;
        int unresolvedCellCount = 0;
        int duplicateCellCount = 0;
        List<ExcelDataCellBrushMapping> legacyMappings = layoutPreset.CellMappings;

        // Convert sparse mappings without changing their one-based Excel coordinates.
        for (int mappingIndex = 0; mappingIndex < legacyMappings.Count; mappingIndex++)
        {
            ExcelDataCellBrushMapping legacyMapping = legacyMappings[mappingIndex];

            if (legacyMapping == null || !legacyMapping.IsUsable())
                continue;

            ExcelDataWorkbookSheetDefinition sheet =
                ResolveOrCreateSheet(layoutPreset, legacyMapping.SheetName, sheetsByName);

            if (sheet.FindCell(legacyMapping.RowIndex, legacyMapping.ColumnIndex) != null)
            {
                duplicateCellCount++;
                continue;
            }

            ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
            ExcelDataFieldCatalogEntry entry;

            if (entriesById != null && entriesById.TryGetValue(legacyMapping.FieldId, out entry))
                binding.ConfigureFromEntry(entry);
            else
            {
                binding.ConfigureUnresolved(legacyMapping.FieldId);
                unresolvedCellCount++;
            }

            ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
            cell.ConfigureDataField(sheet.SheetId,
                                    legacyMapping.RowIndex,
                                    legacyMapping.ColumnIndex,
                                    binding,
                                    legacyMapping.Direction,
                                    string.Empty,
                                    legacyMapping.CustomNumberFormat);
            sheet.Cells.Add(cell);
            convertedCellCount++;
        }

        if (persistentAsset)
            EditorUtility.SetDirty(layoutPreset);

        return new ExcelDataGridAuthoritativeMigrationResult(false,
                                                             convertedCellCount,
                                                             unresolvedCellCount,
                                                             duplicateCellCount);
    }
    #endregion

    #region Project Discovery
    /// <summary>
    /// Loads every workbook layout preset stored in the project AssetDatabase.
    /// </summary>
    /// <returns>Project layout presets in AssetDatabase search order.</returns>
    private static List<ExcelDataWorkbookLayoutPreset> LoadProjectLayoutPresets()
    {
        string[] guids = AssetDatabase.FindAssets("t:ExcelDataWorkbookLayoutPreset");
        List<ExcelDataWorkbookLayoutPreset> layoutPresets = new List<ExcelDataWorkbookLayoutPreset>();

        // Resolve each matching GUID while ignoring stale AssetDatabase paths.
        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            ExcelDataWorkbookLayoutPreset layoutPreset =
                AssetDatabase.LoadAssetAtPath<ExcelDataWorkbookLayoutPreset>(assetPath);

            if (layoutPreset != null)
                layoutPresets.Add(layoutPreset);
        }

        return layoutPresets;
    }

    /// <summary>
    /// Indexes current field catalog entries once for the temporary project migration.
    /// </summary>
    /// <param name="entries">Current project field catalog.</param>
    /// <returns>Dictionary keyed by stable field identifier.</returns>
    internal static Dictionary<string, ExcelDataFieldCatalogEntry> BuildEntryLookup(
        List<ExcelDataFieldCatalogEntry> entries)
    {
        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById =
            new Dictionary<string, ExcelDataFieldCatalogEntry>(StringComparer.Ordinal);

        if (entries == null)
            return entriesById;

        // Keep the latest catalog entry for a duplicate ID, matching existing lookup behavior.
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

    #region Sheet Conversion
    /// <summary>
    /// Resolves or creates one new worksheet definition for a legacy mapping sheet name.
    /// </summary>
    /// <param name="layoutPreset">Legacy layout providing preview dimensions.</param>
    /// <param name="legacySheetName">Legacy mapping worksheet name.</param>
    /// <param name="sheetsByName">Sheets already created during this conversion.</param>
    /// <returns>Existing or newly created worksheet definition.</returns>
    private static ExcelDataWorkbookSheetDefinition ResolveOrCreateSheet(
        ExcelDataWorkbookLayoutPreset layoutPreset,
        string legacySheetName,
        Dictionary<string, ExcelDataWorkbookSheetDefinition> sheetsByName)
    {
        string sheetName = string.IsNullOrWhiteSpace(legacySheetName) ? layoutPreset.ObjectsSheetName : legacySheetName;
        ExcelDataWorkbookSheetDefinition existingSheet;

        if (sheetsByName.TryGetValue(sheetName, out existingSheet))
            return existingSheet;

        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure(sheetName,
                        layoutPreset.DefaultGridRows,
                        layoutPreset.DefaultGridColumns,
                        layoutPreset.DefaultCellWidth,
                        layoutPreset.DefaultCellHeight,
                        true,
                        true,
                        ExcelDataWorkbookSheetVisibility.Visible);
        layoutPreset.SheetDefinitions.Add(sheet);
        sheetsByName.Add(sheetName, sheet);
        return sheet;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores immutable diagnostics for one temporary legacy layout conversion.
/// </summary>
internal readonly struct ExcelDataGridAuthoritativeMigrationResult
{
    #region Fields
    public readonly bool WasSkipped;
    public readonly int ConvertedCellCount;
    public readonly int UnresolvedCellCount;
    public readonly int DuplicateCellCount;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable migration result.
    /// </summary>
    /// <param name="wasSkipped">True when no conversion was required.</param>
    /// <param name="convertedCellCount">Legacy mappings converted into new cells.</param>
    /// <param name="unresolvedCellCount">Mappings retained without current catalog metadata.</param>
    /// <param name="duplicateCellCount">Mappings skipped because a coordinate was already occupied.</param>
    public ExcelDataGridAuthoritativeMigrationResult(bool wasSkipped,
                                                     int convertedCellCount,
                                                     int unresolvedCellCount,
                                                     int duplicateCellCount)
    {
        WasSkipped = wasSkipped;
        ConvertedCellCount = convertedCellCount;
        UnresolvedCellCount = unresolvedCellCount;
        DuplicateCellCount = duplicateCellCount;
    }
    #endregion

    #endregion
}
