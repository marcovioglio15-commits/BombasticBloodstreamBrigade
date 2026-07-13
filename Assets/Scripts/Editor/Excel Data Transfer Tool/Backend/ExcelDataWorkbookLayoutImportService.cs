using System;
using System.Collections.Generic;
using System.IO;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using UnityEditor;

/// <summary>
/// Rebuilds workbook layout preset mappings from BrushGrid rows saved inside exported workbooks.
/// </summary>
internal static class ExcelDataWorkbookLayoutImportService
{
    #region Constants
    private const string BrushGridSection = "BrushGrid";
    private const string DefaultObjectsSheetName = "Objects";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Imports painted brush-grid mappings from one workbook into the selected layout preset.
    /// </summary>
    /// <param name="layoutPreset">Layout preset that receives imported brush-grid mappings.</param>
    /// <param name="workbookPath">Absolute or project-relative workbook path to read.</param>
    /// <param name="preferredSheetName">Worksheet name to read before falling back to the default Objects sheet.</param>
    /// <returns>Summary of imported, skipped and warning rows.</returns>
    public static ExcelDataWorkbookLayoutImportResult ImportBrushGridMappings(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                                              string workbookPath,
                                                                              string preferredSheetName)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveWorkbookPath(workbookPath, ExcelDataWorkbookPathUtility.LogExportRelativePath);

        if (ContainsGridAuthoritativeTechnicalSheet(resolvedPath))
            throw new InvalidOperationException("Grid-authoritative workbook layout restoration is not supported by the current loader. The layout preset was not modified.");

        List<ExcelDataWorkbookRow> workbookRows = LoadWorkbookRowsWithFallback(resolvedPath, preferredSheetName);
        List<ExcelDataCellBrushMapping> importedMappings = new List<ExcelDataCellBrushMapping>();
        int importedRows = 0;
        int skippedRows = 0;
        int warningRows = 0;
        int maxRowIndex = layoutPreset.DefaultGridRows;
        int maxColumnIndex = layoutPreset.DefaultGridColumns;

        // Parse every legacy row into a temporary collection before mutating the selected preset.
        for (int rowIndex = 0; rowIndex < workbookRows.Count; rowIndex++)
        {
            ExcelDataWorkbookRow workbookRow = workbookRows[rowIndex];

            if (!IsBrushGridRow(workbookRow))
                continue;

            if (!CanImportMapping(workbookRow))
            {
                skippedRows++;
                continue;
            }

            ExcelDataCellBrushMapping mapping = new ExcelDataCellBrushMapping();
            mapping.Configure(ResolveSheetName(workbookRow, layoutPreset),
                              workbookRow.WorkbookRow,
                              workbookRow.WorkbookColumn,
                              workbookRow.FieldId,
                              ExcelDataTransferDirection.Both,
                              workbookRow.PathTemplate,
                              string.Empty);
            importedMappings.Add(mapping);
            importedRows++;
            maxRowIndex = Math.Max(maxRowIndex, workbookRow.WorkbookRow);
            maxColumnIndex = Math.Max(maxColumnIndex, workbookRow.WorkbookColumn);

            if (!string.IsNullOrWhiteSpace(workbookRow.Warning))
                warningRows++;
        }

        if (importedRows <= 0)
            throw new InvalidOperationException("Workbook contains no usable legacy BrushGrid mappings. The layout preset was not modified.");

        Undo.RecordObject(layoutPreset, "Load Excel Workbook Layout");
        layoutPreset.CellMappings.Clear();
        layoutPreset.CellMappings.AddRange(importedMappings);
        Dictionary<string, ExcelDataFieldCatalogEntry> entriesById =
            ExcelDataGridAuthoritativeLayoutMigrationUtility.BuildEntryLookup(ExcelDataFieldCatalogBuilder.BuildCatalog());
        ExcelDataGridAuthoritativeLayoutMigrationUtility.ConvertPreset(layoutPreset, entriesById, true);

        ApplyGridPreviewSize(layoutPreset, maxRowIndex, maxColumnIndex);
        EditorUtility.SetDirty(layoutPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        return new ExcelDataWorkbookLayoutImportResult(resolvedPath, workbookRows.Count, importedRows, skippedRows, warningRows);
    }
    #endregion

    #region Row Loading
    /// <summary>
    /// Detects the reserved v2 technical worksheet before the legacy loader can mutate layout assets.
    /// </summary>
    /// <param name="resolvedPath">Absolute workbook path to inspect.</param>
    /// <returns>True when the workbook uses the grid-authoritative schema.</returns>
    private static bool ContainsGridAuthoritativeTechnicalSheet(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
            return false;

        List<SheetInfo> sheets = MiniExcel.GetSheetInformations(resolvedPath, new OpenXmlConfiguration());

        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            if (string.Equals(sheets[sheetIndex].Name,
                              ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName,
                              StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reads workbook rows from the preferred sheet and falls back to Objects for older exported files.
    /// </summary>
    /// <param name="resolvedPath">Resolved workbook path.</param>
    /// <param name="preferredSheetName">Preferred worksheet name.</param>
    /// <returns>Rows read from the workbook.</returns>
    private static List<ExcelDataWorkbookRow> LoadWorkbookRowsWithFallback(string resolvedPath, string preferredSheetName)
    {
        string safePreferredSheetName = string.IsNullOrWhiteSpace(preferredSheetName) ? DefaultObjectsSheetName : preferredSheetName;

        try
        {
            return ExcelDataWorkbookReader.LoadWorkbookRows(resolvedPath, safePreferredSheetName);
        }
        catch
        {
            if (string.Equals(safePreferredSheetName, DefaultObjectsSheetName, StringComparison.Ordinal))
                throw;

            return ExcelDataWorkbookReader.LoadWorkbookRows(resolvedPath, DefaultObjectsSheetName);
        }
    }
    #endregion

    #region Mapping Conversion
    /// <summary>
    /// Checks whether one workbook row represents a brush-grid mapping row.
    /// </summary>
    /// <param name="workbookRow">Workbook row to inspect.</param>
    /// <returns>True when the row belongs to the BrushGrid section.</returns>
    private static bool IsBrushGridRow(ExcelDataWorkbookRow workbookRow)
    {
        return workbookRow != null &&
               string.Equals(workbookRow.Section, BrushGridSection, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether a BrushGrid row has the minimum data required to recreate a mapping.
    /// </summary>
    /// <param name="workbookRow">Workbook row to inspect.</param>
    /// <returns>True when field id and cell coordinates are usable.</returns>
    private static bool CanImportMapping(ExcelDataWorkbookRow workbookRow)
    {
        if (workbookRow.WorkbookRow < 1 || workbookRow.WorkbookColumn < 1)
            return false;

        return !string.IsNullOrWhiteSpace(workbookRow.FieldId);
    }

    /// <summary>
    /// Resolves the sheet name for one imported mapping.
    /// </summary>
    /// <param name="workbookRow">Workbook row being converted.</param>
    /// <param name="layoutPreset">Layout preset receiving the mapping.</param>
    /// <returns>Workbook sheet name used by the mapping.</returns>
    private static string ResolveSheetName(ExcelDataWorkbookRow workbookRow,
                                           ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        if (!string.IsNullOrWhiteSpace(workbookRow.WorkbookSheet))
            return workbookRow.WorkbookSheet;

        return string.IsNullOrWhiteSpace(layoutPreset.ObjectsSheetName) ? DefaultObjectsSheetName : layoutPreset.ObjectsSheetName;
    }
    #endregion

    #region Serialized Updates
    /// <summary>
    /// Expands the visible grid preview so imported mappings are immediately visible.
    /// </summary>
    /// <param name="layoutPreset">Layout preset receiving the preview dimensions.</param>
    /// <param name="rowCount">Minimum row count needed by imported mappings.</param>
    /// <param name="columnCount">Minimum column count needed by imported mappings.</param>
    private static void ApplyGridPreviewSize(ExcelDataWorkbookLayoutPreset layoutPreset,
                                             int rowCount,
                                             int columnCount)
    {
        SerializedObject serializedObject = new SerializedObject(layoutPreset);
        SerializedProperty rowsProperty = serializedObject.FindProperty("defaultGridRows");
        SerializedProperty columnsProperty = serializedObject.FindProperty("defaultGridColumns");

        if (rowsProperty != null && rowsProperty.intValue < rowCount)
            rowsProperty.intValue = rowCount;

        if (columnsProperty != null && columnsProperty.intValue < columnCount)
            columnsProperty.intValue = columnCount;

        serializedObject.ApplyModifiedProperties();
    }
    #endregion

    #endregion
}

/// <summary>
/// Summarizes one layout import operation from workbook BrushGrid rows.
/// </summary>
internal sealed class ExcelDataWorkbookLayoutImportResult
{
    #region Properties
    public string WorkbookPath
    {
        get;
    }

    public int TotalRowCount
    {
        get;
    }

    public int ImportedMappingCount
    {
        get;
    }

    public int SkippedMappingCount
    {
        get;
    }

    public int WarningCount
    {
        get;
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an immutable result for layout-import UI feedback.
    /// </summary>
    /// <param name="workbookPath">Workbook path read during layout import.</param>
    /// <param name="totalRowCount">Total workbook rows scanned.</param>
    /// <param name="importedMappingCount">Brush-grid mappings imported into the layout preset.</param>
    /// <param name="skippedMappingCount">Brush-grid rows skipped because they were incomplete.</param>
    /// <param name="warningCount">Imported rows that carried workbook warnings.</param>
    public ExcelDataWorkbookLayoutImportResult(string workbookPath,
                                               int totalRowCount,
                                               int importedMappingCount,
                                               int skippedMappingCount,
                                               int warningCount)
    {
        WorkbookPath = workbookPath;
        TotalRowCount = totalRowCount;
        ImportedMappingCount = importedMappingCount;
        SkippedMappingCount = skippedMappingCount;
        WarningCount = warningCount;
    }
    #endregion

    #endregion
}
