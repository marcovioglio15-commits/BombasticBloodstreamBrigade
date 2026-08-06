using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Restores complete grid-authoritative workbook layouts from supported reserved technical worksheet schemas.
/// </summary>
internal static class ExcelDataWorkbookLayoutImportService
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Replaces one selected layout only after a complete workbook snapshot passes structural and hash validation.
    /// </summary>
    /// <param name="layoutPreset">Layout preset that receives the restored sheets and cells.</param>
    /// <param name="workbookPath">Absolute or project-relative workbook path to read.</param>
    /// <returns>Summary of restored worksheets, cells and deterministic hash validation.</returns>
    public static ExcelDataWorkbookLayoutImportResult ImportLayoutSnapshot(ExcelDataWorkbookLayoutPreset layoutPreset,
                                                                           string workbookPath)
    {
        if (layoutPreset == null)
            throw new ArgumentNullException(nameof(layoutPreset));

        string resolvedPath = ExcelDataWorkbookPathUtility.ResolveWorkbookPath(workbookPath,
                                                                               ExcelDataWorkbookPathUtility.LogExportRelativePath);
        ExcelDataWorkbookLayoutSnapshot snapshot = ExcelDataWorkbookLayoutSnapshotReader.Read(resolvedPath);
        ValidateSnapshot(snapshot);
        List<ExcelDataWorkbookSheetDefinition> restoredSheets = BuildRestoredSheets(snapshot);
        string restoredHash = CalculateRestoredHash(restoredSheets, snapshot.SchemaVersion);

        if (!string.Equals(restoredHash, snapshot.LayoutHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Workbook layout snapshot hash does not match its reconstructed content. The layout preset was not modified.");

        Undo.RecordObject(layoutPreset, "Load Excel Workbook Layout Snapshot");
        layoutPreset.SheetDefinitions.Clear();
        layoutPreset.SheetDefinitions.AddRange(restoredSheets);

        if (restoredSheets.Count > 0)
        {
            ExcelDataWorkbookSheetDefinition primarySheet = restoredSheets[0];
            layoutPreset.ConfigureGridDefaults(primarySheet.PreviewRowCount,
                                               primarySheet.PreviewColumnCount,
                                               primarySheet.PreviewCellWidth,
                                               primarySheet.PreviewCellHeight);
        }

        EditorUtility.SetDirty(layoutPreset);
        ExcelDataTransferDraftSession.MarkDirty();
        return new ExcelDataWorkbookLayoutImportResult(resolvedPath,
                                                       restoredSheets.Count,
                                                       snapshot.Cells.Count,
                                                       true,
                                                       restoredHash);
    }
    #endregion

    #region Snapshot Validation
    /// <summary>
    /// Validates workbook-level snapshot identity before constructing serialized definitions.
    /// </summary>
    /// <param name="snapshot">Parsed technical worksheet snapshot.</param>
    private static void ValidateSnapshot(ExcelDataWorkbookLayoutSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.TechnicalSheetFound)
            throw new InvalidOperationException("Workbook does not contain the required " +
                                                ExcelDataWorkbookTechnicalSheetBuilder.TechnicalSheetName +
                                                " technical worksheet.");

        if (!snapshot.WorkbookRecordFound)
            throw new InvalidOperationException("Workbook technical worksheet has no Workbook identity record.");

        if (!IsSupportedSchemaVersion(snapshot.SchemaVersion))
            throw new InvalidOperationException("Workbook layout schema " + snapshot.SchemaVersion +
                                                 " is not supported. Expected " +
                                                 ExcelDataWorkbookTechnicalSheetBuilder.LegacySchemaVersion + " or " +
                                                 ExcelDataWorkbookTechnicalSheetBuilder.SchemaVersion + ".");

        if (snapshot.Sheets.Count <= 0)
            throw new InvalidOperationException("Workbook technical worksheet contains no authored Sheet records.");

        if (snapshot.Cells.Count <= 0)
            throw new InvalidOperationException("Workbook technical worksheet contains no authored Cell records.");

        if (string.IsNullOrWhiteSpace(snapshot.LayoutHash))
            throw new InvalidOperationException("Workbook technical worksheet contains no deterministic layout hash.");
    }

    /// <summary>
    /// Reports whether one workbook schema can be restored without losing authored layout semantics.
    /// </summary>
    /// <param name="schemaVersion">Technical workbook schema identifier.</param>
    /// <returns>True for legacy v3 scalar layouts and current v4 formula-aware layouts.</returns>
    private static bool IsSupportedSchemaVersion(string schemaVersion)
    {
        return string.Equals(schemaVersion,
                             ExcelDataWorkbookTechnicalSheetBuilder.LegacySchemaVersion,
                             StringComparison.Ordinal) ||
               string.Equals(schemaVersion,
                             ExcelDataWorkbookTechnicalSheetBuilder.SchemaVersion,
                             StringComparison.Ordinal);
    }
    #endregion

    #region Restoration
    /// <summary>
    /// Builds a detached authoritative sheet graph so malformed workbooks cannot partially mutate project assets.
    /// </summary>
    /// <param name="snapshot">Validated workbook layout snapshot.</param>
    /// <returns>Detached worksheet definitions ready for one atomic assignment.</returns>
    private static List<ExcelDataWorkbookSheetDefinition> BuildRestoredSheets(ExcelDataWorkbookLayoutSnapshot snapshot)
    {
        List<ExcelDataWorkbookSheetDefinition> sheets = new List<ExcelDataWorkbookSheetDefinition>();
        Dictionary<string, ExcelDataWorkbookSheetDefinition> sheetsById =
            new Dictionary<string, ExcelDataWorkbookSheetDefinition>(StringComparer.Ordinal);
        HashSet<string> sanitizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Restore worksheets first so every subsequent cell resolves a stable owner identity.
        for (int sheetIndex = 0; sheetIndex < snapshot.Sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookLayoutSheetSnapshot sourceSheet = snapshot.Sheets[sheetIndex];
            ValidateSheet(sourceSheet, sheetsById, sanitizedNames);
            ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
            sheet.ConfigureFromSnapshot(sourceSheet.SheetId,
                                        sourceSheet.SheetName,
                                        sourceSheet.PreviewRowCount,
                                        sourceSheet.PreviewColumnCount,
                                        sourceSheet.PreviewCellWidth,
                                        sourceSheet.PreviewCellHeight,
                                        sourceSheet.FreezeRowCount,
                                        sourceSheet.FreezeColumnCount,
                                        sourceSheet.ImportEnabled,
                                        sourceSheet.ExportEnabled,
                                        sourceSheet.Visibility);
            sheets.Add(sheet);
            sheetsById.Add(sourceSheet.SheetId, sheet);
        }

        Dictionary<string, HashSet<long>> coordinatesBySheet = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);

        // Restore sparse cells only after every referenced sheet has passed validation.
        for (int cellIndex = 0; cellIndex < snapshot.Cells.Count; cellIndex++)
        {
            ExcelDataWorkbookLayoutCellSnapshot sourceCell = snapshot.Cells[cellIndex];
            ExcelDataWorkbookSheetDefinition sheet;

            if (sourceCell == null || !sheetsById.TryGetValue(sourceCell.SheetId, out sheet))
                throw new InvalidOperationException("Technical Cell record references an unknown worksheet ID.");

            ValidateCellCoordinate(sourceCell, coordinatesBySheet);
            sheet.Cells.Add(BuildRestoredCell(sourceCell, sheet.SheetId));
        }

        return sheets;
    }

    /// <summary>
    /// Recreates one Data Field, Literal Text or Formula cell from complete technical metadata.
    /// </summary>
    /// <param name="sourceCell">Parsed cell snapshot.</param>
    /// <param name="sheetId">Validated owner worksheet identifier.</param>
    /// <returns>Restored authoritative cell definition.</returns>
    private static ExcelDataWorkbookCellDefinition BuildRestoredCell(ExcelDataWorkbookLayoutCellSnapshot sourceCell,
                                                                     string sheetId)
    {
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();

        switch (sourceCell.ContentKind)
        {
            case ExcelDataWorkbookCellContentKind.LiteralText:
                cell.ConfigureLiteralText(sheetId,
                                          sourceCell.RowIndex,
                                          sourceCell.ColumnIndex,
                                          sourceCell.LiteralText,
                                          sourceCell.Direction,
                                          sourceCell.BrushId,
                                          sourceCell.ValidateLiteralDuringImport);
                return cell;
            case ExcelDataWorkbookCellContentKind.Formula:
                if (!ExcelDataFormulaExpressionUtility.TryNormalize(sourceCell.FormulaExpression,
                                                                    out string _,
                                                                    out string warning))
                {
                    throw new InvalidOperationException("Technical Formula cell is invalid: " + warning);
                }

                cell.ConfigureFormula(sheetId,
                                      sourceCell.RowIndex,
                                      sourceCell.ColumnIndex,
                                      sourceCell.FormulaExpression,
                                      sourceCell.BrushId);
                return cell;
            case ExcelDataWorkbookCellContentKind.DataField:
                if (string.IsNullOrWhiteSpace(sourceCell.FieldId))
                    throw new InvalidOperationException("Technical Data Field cell has no stable FieldId.");

                ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
                binding.Configure(sourceCell.FieldId,
                                  sourceCell.Domain,
                                  sourceCell.OwnerAssetGuid,
                                  sourceCell.OwnerAssetTypeName,
                                  sourceCell.OwnerAssetPath,
                                  sourceCell.SerializedPath,
                                  sourceCell.PathTemplate,
                                  sourceCell.DataKind);
                binding.ConfigureListIdentity(sourceCell.ConcreteListIndices, sourceCell.StableListKeys);
                cell.ConfigureDataField(sheetId,
                                        sourceCell.RowIndex,
                                        sourceCell.ColumnIndex,
                                        binding,
                                        sourceCell.Direction,
                                        sourceCell.BrushId,
                                        sourceCell.NumberFormat);
                return cell;
            default:
                throw new InvalidOperationException("Unsupported technical cell content kind: " + sourceCell.ContentKind + ".");
        }
    }
    #endregion

    #region Record Validation
    /// <summary>
    /// Validates one sheet record and reserves its stable and visible identities.
    /// </summary>
    /// <param name="sheet">Sheet snapshot to validate.</param>
    /// <param name="sheetsById">Already reserved stable sheet IDs.</param>
    /// <param name="sanitizedNames">Already reserved workbook-visible names.</param>
    private static void ValidateSheet(ExcelDataWorkbookLayoutSheetSnapshot sheet,
                                      Dictionary<string, ExcelDataWorkbookSheetDefinition> sheetsById,
                                      HashSet<string> sanitizedNames)
    {
        if (sheet == null || string.IsNullOrWhiteSpace(sheet.SheetId) || string.IsNullOrWhiteSpace(sheet.SheetName))
            throw new InvalidOperationException("Technical Sheet record has missing stable or visible identity.");

        if (sheet.PreviewRowCount < 1 || sheet.PreviewColumnCount < 1 ||
            sheet.PreviewCellWidth < 24 || sheet.PreviewCellHeight < 18 ||
            sheet.FreezeRowCount < 0 || sheet.FreezeColumnCount < 0)
            throw new InvalidOperationException("Technical Sheet record contains invalid preview or freeze-pane dimensions: " + sheet.SheetName + ".");

        if (sheetsById.ContainsKey(sheet.SheetId))
            throw new InvalidOperationException("Duplicate technical worksheet ID: " + sheet.SheetId + ".");

        string sanitizedName = ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet");

        if (!sanitizedNames.Add(sanitizedName))
            throw new InvalidOperationException("Technical worksheet names collide after Excel sanitization: " + sanitizedName + ".");
    }

    /// <summary>
    /// Validates one exact coordinate and rejects duplicate payloads before asset mutation.
    /// </summary>
    /// <param name="cell">Cell snapshot to validate.</param>
    /// <param name="coordinatesBySheet">Reserved coordinate keys grouped by sheet ID.</param>
    private static void ValidateCellCoordinate(ExcelDataWorkbookLayoutCellSnapshot cell,
                                               Dictionary<string, HashSet<long>> coordinatesBySheet)
    {
        if (cell.RowIndex < 1 || cell.ColumnIndex < 1)
            throw new InvalidOperationException("Technical Cell record contains an invalid one-based coordinate.");

        HashSet<long> coordinates;

        if (!coordinatesBySheet.TryGetValue(cell.SheetId, out coordinates))
        {
            coordinates = new HashSet<long>();
            coordinatesBySheet.Add(cell.SheetId, coordinates);
        }

        if (!coordinates.Add(ExcelDataWorkbookCoordinateUtility.BuildKey(cell.RowIndex, cell.ColumnIndex)))
            throw new InvalidOperationException("Duplicate technical Cell coordinate at " +
                                                ExcelDataWorkbookCoordinateUtility.BuildAddress(cell.RowIndex, cell.ColumnIndex) + ".");
    }
    #endregion

    #region Hash Validation
    /// <summary>
    /// Calculates the reconstructed hash on a transient owner without touching the selected project asset.
    /// </summary>
    /// <param name="restoredSheets">Detached restored worksheet definitions.</param>
    /// <param name="snapshotSchemaVersion">Technical schema that authored the stored layout hash.</param>
    /// <returns>Deterministic reconstructed layout hash.</returns>
    private static string CalculateRestoredHash(List<ExcelDataWorkbookSheetDefinition> restoredSheets,
                                                string snapshotSchemaVersion)
    {
        ExcelDataWorkbookLayoutPreset transientLayout = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();

        try
        {
            transientLayout.SheetDefinitions.AddRange(restoredSheets);
            bool includeFormulaExpression = !string.Equals(snapshotSchemaVersion,
                                                           ExcelDataWorkbookTechnicalSheetBuilder.LegacySchemaVersion,
                                                           StringComparison.Ordinal);
            return ExcelDataWorkbookLayoutHashUtility.Calculate(transientLayout, includeFormulaExpression);
        }
        finally
        {
            transientLayout.SheetDefinitions.Clear();
            ScriptableObject.DestroyImmediate(transientLayout);
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Summarizes one complete layout restoration from grid-authoritative workbook metadata.
/// </summary>
internal sealed class ExcelDataWorkbookLayoutImportResult
{
    #region Properties
    public string WorkbookPath { get; }
    public int ImportedSheetCount { get; }
    public int ImportedCellCount { get; }
    public bool LayoutHashMatches { get; }
    public string LayoutHash { get; }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates an immutable result for layout-restoration UI feedback.
    /// </summary>
    /// <param name="workbookPath">Workbook path read during restoration.</param>
    /// <param name="importedSheetCount">Authoritative worksheets restored.</param>
    /// <param name="importedCellCount">Exact Data Field, Literal Text and Formula cells restored.</param>
    /// <param name="layoutHashMatches">True when reconstructed and workbook hashes match.</param>
    /// <param name="layoutHash">Validated deterministic layout hash.</param>
    public ExcelDataWorkbookLayoutImportResult(string workbookPath,
                                               int importedSheetCount,
                                               int importedCellCount,
                                               bool layoutHashMatches,
                                               string layoutHash)
    {
        WorkbookPath = workbookPath;
        ImportedSheetCount = importedSheetCount;
        ImportedCellCount = importedCellCount;
        LayoutHashMatches = layoutHashMatches;
        LayoutHash = layoutHash;
    }
    #endregion

    #endregion
}
