using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Seeds practical Excel Data Transfer defaults without overwriting authored user choices.
/// </summary>
internal static class ExcelDataTransferDefaultPresetUtility
{
    #region Constants
    private const int MinimumPracticalLayoutMappings = 8;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures a full transfer preset graph has usable import, export and layout defaults.
    /// </summary>
    /// <param name="layoutPreset">Workbook layout preset to seed with practical mappings.</param>
    /// <param name="importPreset">Import preset to seed with practical selected fields.</param>
    /// <param name="exportPreset">Export preset to seed with practical selected fields.</param>
    public static void EnsureTransferGraphDefaults(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                   ExcelDataImportPreset importPreset,
                                                   ExcelDataExportPreset exportPreset)
    {
        if (!NeedsCatalog(layoutPreset, importPreset, exportPreset))
            return;

        List<ExcelDataFieldCatalogEntry> catalogEntries = ExcelDataFieldCatalogBuilder.BuildCatalog();
        EnsureLayoutPresetDefaults(layoutPreset, catalogEntries);
        EnsureImportPresetDefaults(importPreset, catalogEntries);
        EnsureExportPresetDefaults(exportPreset, catalogEntries);
    }

    /// <summary>
    /// Ensures a standalone layout preset has practical cell mappings when it is empty or nearly empty.
    /// </summary>
    /// <param name="layoutPreset">Workbook layout preset to seed.</param>
    public static void EnsureLayoutPresetDefaults(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPreset == null ||
            layoutPreset.CellMappings.Count >= MinimumPracticalLayoutMappings &&
            layoutPreset.SheetDefinitions.Count > 0)
            return;

        EnsureLayoutPresetDefaults(layoutPreset, ExcelDataFieldCatalogBuilder.BuildCatalog());
    }

    /// <summary>
    /// Ensures a standalone import preset has practical selected fields when the selection is empty.
    /// </summary>
    /// <param name="importPreset">Import preset to seed.</param>
    public static void EnsureImportPresetDefaults(ExcelDataImportPreset importPreset)
    {
        if (importPreset == null || importPreset.SelectedFields.Count > 0)
            return;

        EnsureImportPresetDefaults(importPreset, ExcelDataFieldCatalogBuilder.BuildCatalog());
    }

    /// <summary>
    /// Ensures a standalone export preset has practical selected fields when the selection is empty.
    /// </summary>
    /// <param name="exportPreset">Export preset to seed.</param>
    public static void EnsureExportPresetDefaults(ExcelDataExportPreset exportPreset)
    {
        if (exportPreset == null || exportPreset.SelectedFields.Count > 0)
            return;

        EnsureExportPresetDefaults(exportPreset, ExcelDataFieldCatalogBuilder.BuildCatalog());
    }
    #endregion

    #region Graph Defaults
    /// <summary>
    /// Checks whether any preset still needs catalog-backed default data.
    /// </summary>
    /// <param name="layoutPreset">Workbook layout preset to inspect.</param>
    /// <param name="importPreset">Import preset to inspect.</param>
    /// <param name="exportPreset">Export preset to inspect.</param>
    /// <returns>True when a catalog pass is useful.</returns>
    private static bool NeedsCatalog(ExcelDataWorkbookLayoutPreset layoutPreset,
                                     ExcelDataImportPreset importPreset,
                                     ExcelDataExportPreset exportPreset)
    {
        return layoutPreset != null &&
               (layoutPreset.CellMappings.Count < MinimumPracticalLayoutMappings || layoutPreset.SheetDefinitions.Count <= 0) ||
               importPreset != null && importPreset.SelectedFields.Count <= 0 ||
               exportPreset != null && exportPreset.SelectedFields.Count <= 0;
    }

    /// <summary>
    /// Adds practical export field selections while preserving authored selections.
    /// </summary>
    /// <param name="exportPreset">Export preset to seed.</param>
    /// <param name="catalogEntries">Current field catalog entries.</param>
    private static void EnsureExportPresetDefaults(ExcelDataExportPreset exportPreset,
                                                   List<ExcelDataFieldCatalogEntry> catalogEntries)
    {
        if (exportPreset == null || exportPreset.SelectedFields.Count > 0)
            return;

        AddExportSelection(exportPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.String, false, "preset", "name"));
        AddExportSelection(exportPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Number, false, "max", "speed"));
        AddExportSelection(exportPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Number, false, "acceleration"));
        AddExportSelection(exportPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Enum, false, "mode"));
        AddExportSelection(exportPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Enemy, ExcelDataBrushDataKind.ObjectReference, false, "visual"));
        AddExportSelection(exportPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Waves, ExcelDataBrushDataKind.All, true, "wave"));
        EditorUtility.SetDirty(exportPreset);
    }

    /// <summary>
    /// Adds practical import field selections while preserving authored selections.
    /// </summary>
    /// <param name="importPreset">Import preset to seed.</param>
    /// <param name="catalogEntries">Current field catalog entries.</param>
    private static void EnsureImportPresetDefaults(ExcelDataImportPreset importPreset,
                                                   List<ExcelDataFieldCatalogEntry> catalogEntries)
    {
        if (importPreset == null || importPreset.SelectedFields.Count > 0)
            return;

        AddImportSelection(importPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.All, ExcelDataBrushDataKind.ObjectReference, false, "preset"));
        AddImportSelection(importPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.All, ExcelDataBrushDataKind.ListElement, true, "array"));
        AddImportSelection(importPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Number, false, "speed"));
        AddImportSelection(importPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Boolean, false, "enabled"));
        AddImportSelection(importPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Enum, false, "mode"));
        EditorUtility.SetDirty(importPreset);
    }

    /// <summary>
    /// Adds practical layout mappings while preserving existing mapped cells and fields.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to seed.</param>
    /// <param name="catalogEntries">Current field catalog entries.</param>
    private static void EnsureLayoutPresetDefaults(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                   List<ExcelDataFieldCatalogEntry> catalogEntries)
    {
        if (layoutPreset == null ||
            layoutPreset.CellMappings.Count >= MinimumPracticalLayoutMappings &&
            layoutPreset.SheetDefinitions.Count > 0)
            return;

        if (layoutPreset.SheetDefinitions.Count <= 0 && layoutPreset.CellMappings.Count > 0)
        {
            Dictionary<string, ExcelDataFieldCatalogEntry> entriesById =
                ExcelDataGridAuthoritativeLayoutMigrationUtility.BuildEntryLookup(catalogEntries);
            ExcelDataGridAuthoritativeLayoutMigrationUtility.ConvertPreset(layoutPreset, entriesById, false);
        }

        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.String, false, "preset", "name"), 1, 1);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.String, false, "version"), 1, 2);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Number, false, "max", "speed"), 2, 1);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Number, false, "acceleration"), 2, 2);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Enum, false, "mode"), 3, 1);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Boolean, false, "enabled"), 3, 2);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Enemy, ExcelDataBrushDataKind.ObjectReference, false, "visual"), 4, 1);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.Waves, ExcelDataBrushDataKind.All, true, "wave"), 5, 1);
        AddMapping(layoutPreset, FindEntry(catalogEntries, ExcelDataTransferDomain.All, ExcelDataBrushDataKind.ListElement, true, "array"), 6, 1);
        EditorUtility.SetDirty(layoutPreset);
    }
    #endregion

    #region Selection Helpers
    /// <summary>
    /// Adds one export selection when the catalog entry exists.
    /// </summary>
    /// <param name="exportPreset">Export preset receiving the selection.</param>
    /// <param name="entry">Catalog entry to add.</param>
    private static void AddExportSelection(ExcelDataExportPreset exportPreset,
                                           ExcelDataFieldCatalogEntry entry)
    {
        if (entry != null)
            exportPreset.AddOrUpdateSelectedField(entry);
    }

    /// <summary>
    /// Adds one import selection when the catalog entry exists.
    /// </summary>
    /// <param name="importPreset">Import preset receiving the selection.</param>
    /// <param name="entry">Catalog entry to add.</param>
    private static void AddImportSelection(ExcelDataImportPreset importPreset,
                                           ExcelDataFieldCatalogEntry entry)
    {
        if (entry != null)
            importPreset.AddOrUpdateSelectedField(entry);
    }
    #endregion

    #region Mapping Helpers
    /// <summary>
    /// Adds a mapping when both the target cell and field are not already mapped.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the mapping.</param>
    /// <param name="entry">Catalog entry represented by the cell.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    private static void AddMapping(ExcelDataWorkbookLayoutPreset layoutPreset,
                                   ExcelDataFieldCatalogEntry entry,
                                   int rowIndex,
                                   int columnIndex)
    {
        if (entry == null || HasFieldMapping(layoutPreset, entry.FieldId) || HasCellMapping(layoutPreset, rowIndex, columnIndex))
            return;

        ExcelDataCellBrushMapping mapping = new ExcelDataCellBrushMapping();
        mapping.Configure(layoutPreset.ObjectsSheetName,
                          rowIndex,
                          columnIndex,
                          entry.FieldId,
                          ExcelDataTransferDirection.Both,
                          entry.PathTemplate,
                          string.Empty);
        layoutPreset.CellMappings.Add(mapping);
        ExcelDataWorkbookLayoutAuthoringUtility.UpsertDataFieldCell(layoutPreset,
                                                                   layoutPreset.ObjectsSheetName,
                                                                   rowIndex,
                                                                   columnIndex,
                                                                   entry,
                                                                   ExcelDataTransferDirection.Both,
                                                                   string.Empty,
                                                                   string.Empty);
    }

    /// <summary>
    /// Checks whether a field id is already mapped by the layout.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to inspect.</param>
    /// <param name="fieldId">Field id to search.</param>
    /// <returns>True when a mapping already targets the field.</returns>
    private static bool HasFieldMapping(ExcelDataWorkbookLayoutPreset layoutPreset, string fieldId)
    {
        List<ExcelDataCellBrushMapping> mappings = layoutPreset.CellMappings;

        for (int mappingIndex = 0; mappingIndex < mappings.Count; mappingIndex++)
        {
            ExcelDataCellBrushMapping mapping = mappings[mappingIndex];

            if (mapping != null && mapping.FieldId == fieldId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a workbook cell is already mapped by the layout.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to inspect.</param>
    /// <param name="rowIndex">One-based workbook row.</param>
    /// <param name="columnIndex">One-based workbook column.</param>
    /// <returns>True when the cell is already mapped.</returns>
    private static bool HasCellMapping(ExcelDataWorkbookLayoutPreset layoutPreset, int rowIndex, int columnIndex)
    {
        return ExcelDataLayoutBrushGridUtility.FindMapping(layoutPreset, layoutPreset.ObjectsSheetName, rowIndex, columnIndex) != null;
    }
    #endregion

    #region Catalog Helpers
    /// <summary>
    /// Finds the first catalog entry matching domain, kind, list policy and all required search terms.
    /// </summary>
    /// <param name="catalogEntries">Catalog entries to search.</param>
    /// <param name="domain">Required domain, or All for any domain.</param>
    /// <param name="dataKind">Required data kind, or All for any kind.</param>
    /// <param name="allowConcreteListElements">True when concrete list elements are allowed.</param>
    /// <param name="requiredTerms">Lower-priority search terms that all need to match.</param>
    /// <returns>First matching catalog entry, or null.</returns>
    private static ExcelDataFieldCatalogEntry FindEntry(List<ExcelDataFieldCatalogEntry> catalogEntries,
                                                        ExcelDataTransferDomain domain,
                                                        ExcelDataBrushDataKind dataKind,
                                                        bool allowConcreteListElements,
                                                        params string[] requiredTerms)
    {
        for (int entryIndex = 0; entryIndex < catalogEntries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = catalogEntries[entryIndex];

            if (!MatchesDomainAndKind(entry, domain, dataKind, allowConcreteListElements))
                continue;

            if (MatchesTerms(entry, requiredTerms))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Checks fixed catalog filters before text matching.
    /// </summary>
    /// <param name="entry">Catalog entry to inspect.</param>
    /// <param name="domain">Required domain, or All for any domain.</param>
    /// <param name="dataKind">Required data kind, or All for any kind.</param>
    /// <param name="allowConcreteListElements">True when concrete list elements are allowed.</param>
    /// <returns>True when the entry passes fixed filters.</returns>
    private static bool MatchesDomainAndKind(ExcelDataFieldCatalogEntry entry,
                                             ExcelDataTransferDomain domain,
                                             ExcelDataBrushDataKind dataKind,
                                             bool allowConcreteListElements)
    {
        if (entry == null)
            return false;

        if (domain != ExcelDataTransferDomain.All && entry.Domain != domain)
            return false;

        if (dataKind != ExcelDataBrushDataKind.All && entry.DataKind != dataKind)
            return false;

        return allowConcreteListElements || !entry.IsConcreteListElement;
    }

    /// <summary>
    /// Checks whether every required term exists in the entry search text.
    /// </summary>
    /// <param name="entry">Catalog entry to inspect.</param>
    /// <param name="requiredTerms">Search terms that must all match.</param>
    /// <returns>True when every term matches.</returns>
    private static bool MatchesTerms(ExcelDataFieldCatalogEntry entry, string[] requiredTerms)
    {
        string searchableText = (entry.SearchText + " " + entry.PathTemplate + " " + entry.DisplayName).ToLowerInvariant();

        for (int termIndex = 0; termIndex < requiredTerms.Length; termIndex++)
        {
            string requiredTerm = requiredTerms[termIndex];

            if (string.IsNullOrWhiteSpace(requiredTerm))
                continue;

            if (!searchableText.Contains(requiredTerm.ToLowerInvariant()))
                return false;
        }

        return true;
    }
    #endregion

    #endregion
}
