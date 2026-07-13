using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Appends round-trip metadata to a reserved VeryHidden worksheet without polluting user sheets.
/// </summary>
internal static class ExcelDataWorkbookTechnicalSheetBuilder
{
    #region Constants
    public const string TechnicalSheetName = "_NashCoreTransfer";
    public const string SchemaVersion = "2";

    private const string WorkbookRecordType = "Workbook";
    private const string SheetRecordType = "Sheet";
    private const string CellRecordType = "Cell";
    #endregion

    #region Fields
    private static readonly string[] Headers = new string[]
    {
        "RecordType",
        "SchemaVersion",
        "ExportedUtc",
        "MasterPresetId",
        "MasterPresetVersion",
        "LayoutPresetId",
        "ImportPresetId",
        "ExportPresetId",
        "LayoutHash",
        "SheetId",
        "AuthoredSheetName",
        "WorkbookSheetName",
        "PreviewRows",
        "PreviewColumns",
        "PreviewCellWidth",
        "PreviewCellHeight",
        "FreezeRows",
        "FreezeColumns",
        "SheetVisibility",
        "SheetImportEnabled",
        "SheetExportEnabled",
        "Row",
        "Column",
        "ContentKind",
        "Direction",
        "FieldId",
        "Domain",
        "OwnerAssetGuid",
        "OwnerAssetType",
        "OwnerAssetPath",
        "ResolvedOwnerAssetPath",
        "SerializedPath",
        "PathTemplate",
        "DataKind",
        "BrushId",
        "NumberFormat",
        "ValidateLiteralOnImport",
        "LiteralText",
        "ListIndices",
        "ListKeys",
        "ReferenceName",
        "ReferenceGuid",
        "ReferencePath",
        "Warning"
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds the reserved technical worksheet after validating workbook visibility and name collisions.
    /// </summary>
    /// <param name="masterPreset">Active transfer preset graph written into workbook metadata.</param>
    /// <param name="buildResult">Grid-authoritative user sheets and exact exported cell records.</param>
    /// <param name="layoutHash">Deterministic hash of the complete authored layout.</param>
    /// <param name="exportedUtc">Invariant UTC export timestamp.</param>
    /// <returns>Number of technical rows including the header row.</returns>
    public static int AppendTechnicalSheet(ExcelDataTransferMasterPreset masterPreset,
                                           ExcelDataWorkbookExportBuildResult buildResult,
                                           string layoutHash,
                                           string exportedUtc)
    {
        if (masterPreset == null)
            throw new ArgumentNullException(nameof(masterPreset));

        if (buildResult == null)
            throw new ArgumentNullException(nameof(buildResult));

        ValidateUserSheets(buildResult);
        int rowCount = 2 + buildResult.Sheets.Count + buildResult.Cells.Count;
        ExcelDataWorkbookSheetDocument technicalSheet =
            buildResult.Document.AddSheet(TechnicalSheetName,
                                          rowCount,
                                          Headers.Length,
                                          ExcelDataWorkbookSheetVisibility.VeryHidden);
        WriteHeaders(technicalSheet);
        WriteWorkbookRecord(technicalSheet, masterPreset, layoutHash, exportedUtc);
        int nextRowIndex = 3;

        // Record every materialized user sheet before its cells for deterministic round-trip parsing.
        for (int sheetIndex = 0; sheetIndex < buildResult.Sheets.Count; sheetIndex++)
        {
            WriteSheetRecord(technicalSheet, nextRowIndex, buildResult.Sheets[sheetIndex].Definition);
            nextRowIndex++;
        }

        // Record every exact cell, including blank warning cells and reference metadata.
        for (int cellIndex = 0; cellIndex < buildResult.Cells.Count; cellIndex++)
        {
            WriteCellRecord(technicalSheet, nextRowIndex, buildResult.Cells[cellIndex]);
            nextRowIndex++;
        }

        return rowCount;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures the workbook has a visible user sheet and no sanitized collision with the reserved name.
    /// </summary>
    /// <param name="buildResult">User workbook build result to validate.</param>
    private static void ValidateUserSheets(ExcelDataWorkbookExportBuildResult buildResult)
    {
        if (buildResult.Sheets.Count <= 0)
            throw new InvalidOperationException("The active layout has no export-enabled user sheet containing export cells.");

        bool hasVisibleSheet = false;
        string reservedName = ExcelDataWorkbookPathUtility.SanitizeSheetName(TechnicalSheetName, TechnicalSheetName);

        // Excel requires at least one visible sheet and case-insensitive unique sanitized names.
        for (int sheetIndex = 0; sheetIndex < buildResult.Sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = buildResult.Sheets[sheetIndex].Definition;

            if (sheet.Visibility == ExcelDataWorkbookSheetVisibility.Visible)
                hasVisibleSheet = true;

            string workbookName = ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet" + (sheetIndex + 1).ToString(CultureInfo.InvariantCulture));

            if (string.Equals(workbookName, reservedName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("User worksheet name collides with reserved technical sheet: " + TechnicalSheetName);
        }

        if (!hasVisibleSheet)
            throw new InvalidOperationException("At least one export-enabled user worksheet must be visible.");
    }
    #endregion

    #region Workbook Record
    /// <summary>
    /// Writes technical column names to the first hidden worksheet row.
    /// </summary>
    /// <param name="sheet">Technical worksheet receiving headers.</param>
    private static void WriteHeaders(ExcelDataWorkbookSheetDocument sheet)
    {
        for (int headerIndex = 0; headerIndex < Headers.Length; headerIndex++)
            sheet.SetValue(1, headerIndex + 1, Headers[headerIndex]);
    }

    /// <summary>
    /// Writes schema, preset identity and deterministic layout hash metadata.
    /// </summary>
    /// <param name="sheet">Technical worksheet receiving the workbook record.</param>
    /// <param name="masterPreset">Active transfer preset graph.</param>
    /// <param name="layoutHash">Deterministic layout hash.</param>
    /// <param name="exportedUtc">Invariant UTC timestamp.</param>
    private static void WriteWorkbookRecord(ExcelDataWorkbookSheetDocument sheet,
                                            ExcelDataTransferMasterPreset masterPreset,
                                            string layoutHash,
                                            string exportedUtc)
    {
        sheet.SetValue(2, 1, WorkbookRecordType);
        sheet.SetValue(2, 2, SchemaVersion);
        sheet.SetValue(2, 3, exportedUtc);
        sheet.SetValue(2, 4, masterPreset.PresetId);
        sheet.SetValue(2, 5, masterPreset.Version);
        sheet.SetValue(2, 6, masterPreset.LayoutPreset == null ? string.Empty : masterPreset.LayoutPreset.PresetId);
        sheet.SetValue(2, 7, masterPreset.ImportPreset == null ? string.Empty : masterPreset.ImportPreset.PresetId);
        sheet.SetValue(2, 8, masterPreset.ExportPreset == null ? string.Empty : masterPreset.ExportPreset.PresetId);
        sheet.SetValue(2, 9, layoutHash);
    }
    #endregion

    #region Sheet Records
    /// <summary>
    /// Writes one complete sheet-definition record for future layout round trips.
    /// </summary>
    /// <param name="technicalSheet">Technical worksheet receiving the record.</param>
    /// <param name="rowIndex">One-based technical row index.</param>
    /// <param name="sheet">Authored user worksheet definition.</param>
    private static void WriteSheetRecord(ExcelDataWorkbookSheetDocument technicalSheet,
                                         int rowIndex,
                                         ExcelDataWorkbookSheetDefinition sheet)
    {
        technicalSheet.SetValue(rowIndex, 1, SheetRecordType);
        technicalSheet.SetValue(rowIndex, 10, sheet.SheetId);
        technicalSheet.SetValue(rowIndex, 11, sheet.SheetName);
        technicalSheet.SetValue(rowIndex, 12, ExcelDataWorkbookPathUtility.SanitizeSheetName(sheet.SheetName, "Sheet"));
        technicalSheet.SetValue(rowIndex, 13, sheet.PreviewRowCount);
        technicalSheet.SetValue(rowIndex, 14, sheet.PreviewColumnCount);
        technicalSheet.SetValue(rowIndex, 15, sheet.PreviewCellWidth);
        technicalSheet.SetValue(rowIndex, 16, sheet.PreviewCellHeight);
        technicalSheet.SetValue(rowIndex, 17, sheet.FreezeRowCount);
        technicalSheet.SetValue(rowIndex, 18, sheet.FreezeColumnCount);
        technicalSheet.SetValue(rowIndex, 19, sheet.Visibility.ToString());
        technicalSheet.SetValue(rowIndex, 20, sheet.ImportEnabled);
        technicalSheet.SetValue(rowIndex, 21, sheet.ExportEnabled);
    }
    #endregion

    #region Cell Records
    /// <summary>
    /// Writes one exact cell binding and its optional reference or warning metadata.
    /// </summary>
    /// <param name="technicalSheet">Technical worksheet receiving the record.</param>
    /// <param name="rowIndex">One-based technical row index.</param>
    /// <param name="record">Exported cell record.</param>
    private static void WriteCellRecord(ExcelDataWorkbookSheetDocument technicalSheet,
                                        int rowIndex,
                                        ExcelDataWorkbookExportCellRecord record)
    {
        ExcelDataWorkbookCellDefinition cell = record.CellDefinition;
        ExcelDataFieldBinding binding = cell.FieldBinding;
        ExcelDataSerializedValueSnapshot snapshot = record.Snapshot;
        technicalSheet.SetValue(rowIndex, 1, CellRecordType);
        technicalSheet.SetValue(rowIndex, 10, record.SheetDefinition.SheetId);
        technicalSheet.SetValue(rowIndex, 11, record.SheetDefinition.SheetName);
        technicalSheet.SetValue(rowIndex, 12, ExcelDataWorkbookPathUtility.SanitizeSheetName(record.SheetDefinition.SheetName, "Sheet"));
        technicalSheet.SetValue(rowIndex, 22, cell.RowIndex);
        technicalSheet.SetValue(rowIndex, 23, cell.ColumnIndex);
        technicalSheet.SetValue(rowIndex, 24, cell.ContentKind.ToString());
        technicalSheet.SetValue(rowIndex, 25, cell.Direction.ToString());
        technicalSheet.SetValue(rowIndex, 35, cell.BrushId);
        technicalSheet.SetValue(rowIndex, 36, cell.NumberFormat);
        technicalSheet.SetValue(rowIndex, 37, cell.ValidateLiteralDuringImport);
        technicalSheet.SetValue(rowIndex, 38, cell.LiteralText);

        if (binding != null && cell.ContentKind == ExcelDataWorkbookCellContentKind.DataField)
            WriteBindingMetadata(technicalSheet, rowIndex, binding);

        if (snapshot != null)
            WriteSnapshotMetadata(technicalSheet, rowIndex, snapshot);
    }

    /// <summary>
    /// Writes stable field and concrete list identity for one Data Field cell.
    /// </summary>
    /// <param name="technicalSheet">Technical worksheet receiving the record.</param>
    /// <param name="rowIndex">One-based technical row index.</param>
    /// <param name="binding">Stable field binding.</param>
    private static void WriteBindingMetadata(ExcelDataWorkbookSheetDocument technicalSheet,
                                             int rowIndex,
                                             ExcelDataFieldBinding binding)
    {
        technicalSheet.SetValue(rowIndex, 26, binding.FieldId);
        technicalSheet.SetValue(rowIndex, 27, binding.Domain.ToString());
        technicalSheet.SetValue(rowIndex, 28, binding.OwnerAssetGuid);
        technicalSheet.SetValue(rowIndex, 29, binding.OwnerAssetTypeName);
        technicalSheet.SetValue(rowIndex, 30, binding.OwnerAssetPath);
        technicalSheet.SetValue(rowIndex, 32, binding.SerializedPath);
        technicalSheet.SetValue(rowIndex, 33, binding.PathTemplate);
        technicalSheet.SetValue(rowIndex, 34, binding.ExpectedDataKind.ToString());
        technicalSheet.SetValue(rowIndex, 39, EncodeIndices(binding.ConcreteListIndices));
        technicalSheet.SetValue(rowIndex, 40, EncodeKeys(binding.StableListKeys));
    }

    /// <summary>
    /// Writes current owner resolution, reference identity and cell-local warning metadata.
    /// </summary>
    /// <param name="technicalSheet">Technical worksheet receiving the record.</param>
    /// <param name="rowIndex">One-based technical row index.</param>
    /// <param name="snapshot">Resolved value snapshot.</param>
    private static void WriteSnapshotMetadata(ExcelDataWorkbookSheetDocument technicalSheet,
                                              int rowIndex,
                                              ExcelDataSerializedValueSnapshot snapshot)
    {
        technicalSheet.SetValue(rowIndex, 31, snapshot.ResolvedOwnerAssetPath);
        technicalSheet.SetValue(rowIndex, 41, snapshot.ReferenceName);
        technicalSheet.SetValue(rowIndex, 42, snapshot.ReferenceGuid);
        technicalSheet.SetValue(rowIndex, 43, snapshot.ReferencePath);
        technicalSheet.SetValue(rowIndex, 44, snapshot.Warning);
    }
    #endregion

    #region List Encoding
    /// <summary>
    /// Encodes concrete list indexes as invariant comma-separated integers.
    /// </summary>
    /// <param name="indices">Concrete zero-based indexes in nesting order.</param>
    /// <returns>Compact invariant list-index text.</returns>
    private static string EncodeIndices(IReadOnlyList<int> indices)
    {
        if (indices == null || indices.Count <= 0)
            return string.Empty;

        StringBuilder encoded = new StringBuilder(indices.Count * 3);

        for (int indexPosition = 0; indexPosition < indices.Count; indexPosition++)
        {
            if (indexPosition > 0)
                encoded.Append(',');

            encoded.Append(indices[indexPosition].ToString(CultureInfo.InvariantCulture));
        }

        return encoded.ToString();
    }

    /// <summary>
    /// Encodes stable list keys with length prefixes so arbitrary key characters remain reversible.
    /// </summary>
    /// <param name="keys">Stable list keys in nesting order.</param>
    /// <returns>Length-prefixed key sequence.</returns>
    private static string EncodeKeys(IReadOnlyList<string> keys)
    {
        if (keys == null || keys.Count <= 0)
            return string.Empty;

        StringBuilder encoded = new StringBuilder(keys.Count * 8);

        for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
        {
            string key = keys[keyIndex] ?? string.Empty;
            encoded.Append(key.Length.ToString(CultureInfo.InvariantCulture));
            encoded.Append(':');
            encoded.Append(key);
        }

        return encoded.ToString();
    }
    #endregion

    #endregion
}
