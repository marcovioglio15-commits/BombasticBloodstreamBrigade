using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Seeds one organized grid-authoritative starter layout without overwriting authored sheets or cells.
/// </summary>
internal static class ExcelDataTransferDefaultPresetUtility
{
    #region Constants
    private const string DefaultSheetName = "Objects";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures a complete transfer graph has a practical authoritative layout and valid sub-preset metadata.
    /// </summary>
    /// <param name="layoutPreset">Workbook layout preset to seed only when empty.</param>
    /// <param name="importPreset">Import preset to validate.</param>
    /// <param name="exportPreset">Export preset to validate.</param>
    public static void EnsureTransferGraphDefaults(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                   ExcelDataImportPreset importPreset,
                                                   ExcelDataExportPreset exportPreset)
    {
        EnsureLayoutPresetDefaults(layoutPreset);
        EnsureImportPresetDefaults(importPreset);
        EnsureExportPresetDefaults(exportPreset);
    }

    /// <summary>
    /// Adds an organized mixed-domain starter sheet only when the layout has no authored cells.
    /// </summary>
    /// <param name="layoutPreset">Workbook layout preset to seed.</param>
    public static void EnsureLayoutPresetDefaults(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (layoutPreset == null || HasAuthoredCells(layoutPreset))
            return;

        List<ExcelDataFieldCatalogEntry> catalogEntries = ExcelDataFieldCatalogBuilder.BuildCatalog();
        string sheetName = string.IsNullOrWhiteSpace(layoutPreset.ObjectsSheetName)
            ? DefaultSheetName
            : layoutPreset.ObjectsSheetName;
        ExcelDataWorkbookSheetDefinition sheet =
            ExcelDataWorkbookLayoutAuthoringUtility.ResolveOrCreateSheet(layoutPreset, sheetName);
        sheet.ConfigurePreview(18, 4, 180, 30);
        sheet.ConfigureFreezePanes(3, 1);
        AddLiteral(layoutPreset, sheetName, 1, 1, "NASHCORE DATA WORKBOOK");
        AddLiteral(layoutPreset, sheetName, 3, 1, "FIELD");
        AddLiteral(layoutPreset, sheetName, 3, 2, "VALUE");
        AddSection(layoutPreset, sheetName, 5, "PLAYER");
        AddLabeledEntry(layoutPreset,
                        sheetName,
                        6,
                        "Player Preset Name",
                        FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.String, false, "preset", "name"));
        AddLabeledEntry(layoutPreset,
                        sheetName,
                        7,
                        "Player Max Speed",
                        FindEntry(catalogEntries, ExcelDataTransferDomain.Player, ExcelDataBrushDataKind.Number, false, "max", "speed"));
        AddSection(layoutPreset, sheetName, 9, "ENEMIES");
        AddLabeledEntry(layoutPreset,
                        sheetName,
                        10,
                        "Enemy Visual Preset",
                        FindEntry(catalogEntries, ExcelDataTransferDomain.Enemy, ExcelDataBrushDataKind.ObjectReference, false, "visual"));
        AddSection(layoutPreset, sheetName, 12, "WAVES");
        AddLabeledEntry(layoutPreset,
                        sheetName,
                        13,
                        "Wave Setting",
                        FindEntry(catalogEntries, ExcelDataTransferDomain.Waves, ExcelDataBrushDataKind.All, true, "wave"));
        EditorUtility.SetDirty(layoutPreset);
    }

    /// <summary>
    /// Ensures a standalone import preset owns valid metadata without creating a parallel field-selection list.
    /// </summary>
    /// <param name="importPreset">Import preset to validate.</param>
    public static void EnsureImportPresetDefaults(ExcelDataImportPreset importPreset)
    {
        if (importPreset != null)
            importPreset.ValidateValues();
    }

    /// <summary>
    /// Ensures a standalone export preset owns valid metadata without creating a parallel field-selection list.
    /// </summary>
    /// <param name="exportPreset">Export preset to validate.</param>
    public static void EnsureExportPresetDefaults(ExcelDataExportPreset exportPreset)
    {
        if (exportPreset != null)
            exportPreset.ValidateValues();
    }
    #endregion

    #region Layout Helpers
    /// <summary>
    /// Reports whether any authoritative sheet already contains user-authored cells.
    /// </summary>
    /// <param name="layoutPreset">Layout preset to inspect.</param>
    /// <returns>True when at least one non-null cell exists.</returns>
    private static bool HasAuthoredCells(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        // Preserve any authored layout regardless of its size or completeness.
        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet != null && sheet.Cells.Count > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds one export-only literal used as a workbook title, header or field label.
    /// </summary>
    /// <param name="layoutPreset">Layout receiving the literal.</param>
    /// <param name="sheetName">Visible worksheet name.</param>
    /// <param name="rowIndex">One-based worksheet row.</param>
    /// <param name="columnIndex">One-based worksheet column.</param>
    /// <param name="text">Exact literal text.</param>
    private static void AddLiteral(ExcelDataWorkbookLayoutPreset layoutPreset,
                                   string sheetName,
                                   int rowIndex,
                                   int columnIndex,
                                   string text)
    {
        ExcelDataWorkbookLayoutAuthoringUtility.PaintLiteralCell(layoutPreset,
                                                                 sheetName,
                                                                 rowIndex,
                                                                 columnIndex,
                                                                 text,
                                                                 ExcelDataTransferDirection.Export,
                                                                 string.Empty,
                                                                 false);
    }

    /// <summary>
    /// Adds one uppercase thematic section divider in the first column.
    /// </summary>
    /// <param name="layoutPreset">Layout receiving the divider.</param>
    /// <param name="sheetName">Visible worksheet name.</param>
    /// <param name="rowIndex">One-based divider row.</param>
    /// <param name="sectionName">Readable section name.</param>
    private static void AddSection(ExcelDataWorkbookLayoutPreset layoutPreset,
                                   string sheetName,
                                   int rowIndex,
                                   string sectionName)
    {
        AddLiteral(layoutPreset, sheetName, rowIndex, 1, sectionName);
    }

    /// <summary>
    /// Adds a readable label and its matching Data Field value on the same row.
    /// </summary>
    /// <param name="layoutPreset">Layout receiving the row.</param>
    /// <param name="sheetName">Visible worksheet name.</param>
    /// <param name="rowIndex">One-based row.</param>
    /// <param name="label">Literal field label.</param>
    /// <param name="entry">Catalog entry written in column B.</param>
    private static void AddLabeledEntry(ExcelDataWorkbookLayoutPreset layoutPreset,
                                        string sheetName,
                                        int rowIndex,
                                        string label,
                                        ExcelDataFieldCatalogEntry entry)
    {
        AddLiteral(layoutPreset, sheetName, rowIndex, 1, label);

        if (entry == null)
            return;

        ExcelDataWorkbookLayoutAuthoringUtility.PaintDataFieldCell(layoutPreset,
                                                                   sheetName,
                                                                   entry,
                                                                   rowIndex,
                                                                   2,
                                                                   ExcelDataTransferDirection.Both,
                                                                   string.Empty,
                                                                   string.Empty);
    }
    #endregion

    #region Catalog Helpers
    /// <summary>
    /// Finds the first brushable catalog entry matching domain, kind, list policy and every search term.
    /// </summary>
    /// <param name="entries">Catalog entries to search.</param>
    /// <param name="domain">Required management domain.</param>
    /// <param name="dataKind">Required data family, or All.</param>
    /// <param name="allowConcreteListElements">True when concrete list values may match.</param>
    /// <param name="requiredTerms">Terms that must exist in the searchable entry text.</param>
    /// <returns>First matching entry, or null.</returns>
    private static ExcelDataFieldCatalogEntry FindEntry(List<ExcelDataFieldCatalogEntry> entries,
                                                        ExcelDataTransferDomain domain,
                                                        ExcelDataBrushDataKind dataKind,
                                                        bool allowConcreteListElements,
                                                        params string[] requiredTerms)
    {
        // Stop at the first deterministic catalog match to keep generated presets stable.
        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = entries[entryIndex];

            if (entry == null || entry.Domain != domain)
                continue;

            if (dataKind != ExcelDataBrushDataKind.All && entry.DataKind != dataKind)
                continue;

            if (!allowConcreteListElements && entry.IsConcreteListElement)
                continue;

            string searchableText = (entry.SearchText + " " + entry.ReadablePath).ToLowerInvariant();
            bool matches = true;

            // Require every requested term so generated labels bind to predictable gameplay concepts.
            for (int termIndex = 0; termIndex < requiredTerms.Length; termIndex++)
            {
                if (!searchableText.Contains(requiredTerms[termIndex].ToLowerInvariant()))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return entry;
        }

        return null;
    }
    #endregion

    #endregion
}
