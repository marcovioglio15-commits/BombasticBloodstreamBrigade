using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Validates exact-cell export, deterministic column sizing, preview, scalar/reference apply and stale-layout blocking.
/// </summary>
public static class ExcelDataGridExactImportSmokeTest
{
    #region Constants
    private const string WorkbookRelativePath = "Logs/ExcelDataGridExactImportSmoke.xlsx";
    private const string DataSheetName = "Round Trip";
    private const string FormulaCellAddress = "A3";
    private const string FormulaExpression = "70+3";
    private const string FormulaCachedValue = "73";
    private const string LongLiteral = "Grid-authoritative workbook values remain fully readable without a manual Excel column resize.";
    #endregion

    #region Fields
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs an isolated editor-only round trip against temporary ScriptableObject assets.
    /// </summary>
    public static void Run()
    {
        string temporaryFolder = CreateTemporaryAssetFolder();

        try
        {
            SmokeAssets assets = CreateSmokeAssets(temporaryFolder);
            ConfigureExpectedValues(assets);
            AssetDatabase.SaveAssets();
            ExcelDataExportResult exportResult =
                ExcelDataExportService.ExportWorkbook(assets.TransferMaster, WorkbookRelativePath);
            ValidateAutoSizedColumn(exportResult.WorkbookPath);
            WriteFormulaCell(exportResult.WorkbookPath,
                             FormulaExpression,
                             FormulaCachedValue,
                             string.Empty);
            ConfigureChangedValues(assets);
            AssetDatabase.SaveAssets();
            ExcelDataImportPreviewResult preview =
                ExcelDataImportPreviewService.PreviewWorkbook(assets.TransferMaster, WorkbookRelativePath);
            ValidatePreview(preview);
            ExcelDataImportApplyResult applyResult =
                ExcelDataImportApplyService.ApplyWorkbook(assets.TransferMaster, WorkbookRelativePath, preview);
            ValidateApplyResult(applyResult);
            ValidateRestoredValues(assets);
            ValidateLayoutHashMutationIsBlocked(assets, preview);
            ValidateBlockedFormulaStates(assets, exportResult.WorkbookPath);
            Debug.Log("ExcelDataGridExactImportSmokeTest PASS: exact scalar, enum, bool, reference, cached formula validation and auto-sized column round trip validated.");
        }
        finally
        {
            AssetDatabase.DeleteAsset(temporaryFolder);
            AssetDatabase.Refresh();
        }
    }
    #endregion

    #region Asset Setup
    /// <summary>
    /// Creates a unique project folder so smoke assets never overwrite authored content.
    /// </summary>
    /// <returns>Project-relative temporary asset folder.</returns>
    private static string CreateTemporaryAssetFolder()
    {
        string folderName = "ExcelDataGridExactImportSmoke_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folderName);
        return "Assets/" + folderName;
    }

    /// <summary>
    /// Creates all persistent temporary assets needed for binding resolution through AssetDatabase GUIDs.
    /// </summary>
    /// <param name="folderPath">Temporary project folder.</param>
    /// <returns>Created smoke asset graph.</returns>
    private static SmokeAssets CreateSmokeAssets(string folderPath)
    {
        SmokeAssets assets = new SmokeAssets();
        assets.TransferLayout = CreateAsset<ExcelDataWorkbookLayoutPreset>(folderPath, "TransferLayout.asset");
        assets.ValueLayout = CreateAsset<ExcelDataWorkbookLayoutPreset>(folderPath, "ValueLayout.asset");
        assets.ReferenceLayout = CreateAsset<ExcelDataWorkbookLayoutPreset>(folderPath, "ReferenceLayout.asset");
        assets.AlternateLayout = CreateAsset<ExcelDataWorkbookLayoutPreset>(folderPath, "AlternateLayout.asset");
        assets.ImportPreset = CreateAsset<ExcelDataImportPreset>(folderPath, "ImportPreset.asset");
        assets.ExportPreset = CreateAsset<ExcelDataExportPreset>(folderPath, "ExportPreset.asset");
        assets.ReferenceOwner = CreateAsset<ExcelDataTransferMasterPreset>(folderPath, "ReferenceOwner.asset");
        assets.TransferMaster = CreateAsset<ExcelDataTransferMasterPreset>(folderPath, "TransferMaster.asset");
        assets.ReferenceOwner.AssignLinkedPresets(assets.ReferenceLayout, null, null, null);
        assets.TransferMaster.AssignLinkedPresets(assets.TransferLayout, null, assets.ImportPreset, assets.ExportPreset);
        ConfigureTransferLayout(assets, folderPath);
        assets.TransferMaster.ValidateValues();
        return assets;
    }

    /// <summary>
    /// Creates one ScriptableObject asset at a deterministic name inside the unique smoke folder.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to create.</typeparam>
    /// <param name="folderPath">Temporary project folder.</param>
    /// <param name="fileName">Asset file name.</param>
    /// <returns>Created persistent asset.</returns>
    private static T CreateAsset<T>(string folderPath, string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, folderPath + "/" + fileName);
        return asset;
    }

    /// <summary>
    /// Configures one exact worksheet containing literal, bool, enum, string, integer and reference cells.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    /// <param name="folderPath">Temporary project folder used to build readable owner paths.</param>
    private static void ConfigureTransferLayout(SmokeAssets assets, string folderPath)
    {
        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure(DataSheetName, 8, 8, 40, 24, true, true, ExcelDataWorkbookSheetVisibility.Visible);
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 1, 1, LongLiteral));
        sheet.Cells.Add(CreateDataCell(sheet.SheetId,
                                       2,
                                       1,
                                       assets.ExportPreset,
                                       folderPath + "/ExportPreset.asset",
                                       "includePlayerData",
                                       ExcelDataBrushDataKind.Boolean,
                                       "Smoke:Bool"));
        sheet.Cells.Add(CreateDataCell(sheet.SheetId,
                                       2,
                                       2,
                                       assets.ExportPreset,
                                       folderPath + "/ExportPreset.asset",
                                       "targetWorkbookProfile",
                                       ExcelDataBrushDataKind.Enum,
                                       "Smoke:Enum"));
        sheet.Cells.Add(CreateDataCell(sheet.SheetId,
                                       2,
                                       3,
                                       assets.ExportPreset,
                                       folderPath + "/ExportPreset.asset",
                                       "targetWorkbookPath",
                                       ExcelDataBrushDataKind.String,
                                       "Smoke:String"));
        sheet.Cells.Add(CreateDataCell(sheet.SheetId,
                                       3,
                                       1,
                                       assets.ValueLayout,
                                       folderPath + "/ValueLayout.asset",
                                       "defaultGridRows",
                                       ExcelDataBrushDataKind.Number,
                                       "Smoke:Integer"));
        sheet.Cells.Add(CreateDataCell(sheet.SheetId,
                                       3,
                                       2,
                                       assets.ReferenceOwner,
                                       folderPath + "/ReferenceOwner.asset",
                                       "layoutPreset",
                                       ExcelDataBrushDataKind.ObjectReference,
                                       "Smoke:Reference"));
        assets.TransferLayout.SheetDefinitions.Add(sheet);
        assets.TransferLayout.ValidateValues();
        EditorUtility.SetDirty(assets.TransferLayout);
    }

    /// <summary>
    /// Creates one export-only literal used to validate content-based column sizing.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet ID.</param>
    /// <param name="rowIndex">One-based row.</param>
    /// <param name="columnIndex">One-based column.</param>
    /// <param name="text">Exact literal text.</param>
    /// <returns>Configured literal cell.</returns>
    private static ExcelDataWorkbookCellDefinition CreateLiteralCell(string sheetId,
                                                                      int rowIndex,
                                                                      int columnIndex,
                                                                      string text)
    {
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureLiteralText(sheetId,
                                  rowIndex,
                                  columnIndex,
                                  text,
                                  ExcelDataTransferDirection.Export,
                                  "Smoke:Literal",
                                  false);
        return cell;
    }

    /// <summary>
    /// Creates one bidirectional Data Field cell bound directly to a persistent smoke asset.
    /// </summary>
    /// <param name="sheetId">Stable owner worksheet ID.</param>
    /// <param name="rowIndex">One-based row.</param>
    /// <param name="columnIndex">One-based column.</param>
    /// <param name="ownerAsset">Persistent target asset.</param>
    /// <param name="ownerPath">Project-relative target asset path.</param>
    /// <param name="serializedPath">Concrete SerializedProperty path.</param>
    /// <param name="dataKind">Expected workbook value family.</param>
    /// <param name="fieldId">Stable smoke field identity.</param>
    /// <returns>Configured Data Field cell.</returns>
    private static ExcelDataWorkbookCellDefinition CreateDataCell(string sheetId,
                                                                   int rowIndex,
                                                                   int columnIndex,
                                                                   Object ownerAsset,
                                                                   string ownerPath,
                                                                   string serializedPath,
                                                                   ExcelDataBrushDataKind dataKind,
                                                                   string fieldId)
    {
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.Configure(fieldId,
                          ExcelDataTransferDomain.Game,
                          AssetDatabase.AssetPathToGUID(ownerPath),
                          ownerAsset.GetType().Name,
                          ownerPath,
                          serializedPath,
                          serializedPath,
                          dataKind);
        ExcelDataWorkbookCellDefinition cell = new ExcelDataWorkbookCellDefinition();
        cell.ConfigureDataField(sheetId,
                                rowIndex,
                                columnIndex,
                                binding,
                                ExcelDataTransferDirection.Both,
                                "Smoke:Data",
                                string.Empty);
        return cell;
    }
    #endregion

    #region Value Setup
    /// <summary>
    /// Writes source values that export must persist into the workbook.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    private static void ConfigureExpectedValues(SmokeAssets assets)
    {
        SetSerializedValue(assets.ExportPreset, "includePlayerData", property => property.boolValue = true);
        SetSerializedValue(assets.ExportPreset,
                           "targetWorkbookProfile",
                           property => property.enumValueIndex = (int)ExcelDataWorkbookPathProfile.AssetsExportWorkbook);
        SetSerializedValue(assets.ExportPreset,
                           "targetWorkbookPath",
                           property => property.stringValue = "Assets/Exports/ConfiguredWorkbookWithLongReadableName.xlsx");
        SetSerializedValue(assets.ValueLayout, "defaultGridRows", property => property.intValue = 73);
        SetSerializedValue(assets.ReferenceOwner,
                           "layoutPreset",
                           property => property.objectReferenceValue = assets.ReferenceLayout);
    }

    /// <summary>
    /// Replaces every source value after export so apply must restore the workbook values.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    private static void ConfigureChangedValues(SmokeAssets assets)
    {
        SetSerializedValue(assets.ExportPreset, "includePlayerData", property => property.boolValue = false);
        SetSerializedValue(assets.ExportPreset,
                           "targetWorkbookProfile",
                           property => property.enumValueIndex = (int)ExcelDataWorkbookPathProfile.LogImportWorkbook);
        SetSerializedValue(assets.ExportPreset, "targetWorkbookPath", property => property.stringValue = "Changed.xlsx");
        SetSerializedValue(assets.ValueLayout, "defaultGridRows", property => property.intValue = 5);
        SetSerializedValue(assets.ReferenceOwner,
                           "layoutPreset",
                           property => property.objectReferenceValue = assets.AlternateLayout);
    }

    /// <summary>
    /// Applies one strongly scoped mutation through SerializedProperty and marks the temporary asset dirty.
    /// </summary>
    /// <param name="asset">Target temporary asset.</param>
    /// <param name="propertyPath">Concrete serialized property path.</param>
    /// <param name="setter">Property mutation callback.</param>
    private static void SetSerializedValue(Object asset,
                                           string propertyPath,
                                           Action<SerializedProperty> setter)
    {
        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty property = serializedObject.FindProperty(propertyPath);

        if (property == null)
            throw new InvalidOperationException("Smoke setup property not found: " + propertyPath);

        setter(property);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
    #endregion

    #region Assertions
    /// <summary>
    /// Verifies that the post-processed worksheet contains a width larger than the authored 40-pixel minimum.
    /// </summary>
    /// <param name="workbookPath">Exported workbook path.</param>
    private static void ValidateAutoSizedColumn(string workbookPath)
    {
        using (FileStream stream = new FileStream(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
        {
            ZipArchiveEntry worksheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");

            if (worksheetEntry == null)
                throw new InvalidOperationException("Exported workbook is missing the first user worksheet XML entry.");

            using (Stream worksheetStream = worksheetEntry.Open())
            {
                XDocument worksheet = XDocument.Load(worksheetStream);
                XElement columns = worksheet.Root.Element(SpreadsheetNamespace + "cols");
                XElement firstColumn = columns == null ? null : columns.Element(SpreadsheetNamespace + "col");
                double width;

                if (firstColumn == null ||
                    !double.TryParse(firstColumn.Attribute("width")?.Value,
                                     NumberStyles.Float,
                                     CultureInfo.InvariantCulture,
                                     out width) ||
                    width < 60d)
                    throw new InvalidOperationException("Long exported content did not produce a fitted Excel column width.");
            }
        }
    }

    /// <summary>
    /// Verifies schema/hash compatibility and all five exact Data Field candidates.
    /// </summary>
    /// <param name="preview">Coordinate-exact import preview.</param>
    private static void ValidatePreview(ExcelDataImportPreviewResult preview)
    {
        if (!preview.CanApply || !preview.LayoutHashMatches)
            throw new InvalidOperationException("Grid-exact preview was unexpectedly blocked: " + preview.ValidationMessage);

        if (preview.TotalRowCount != 5 || preview.ImportableRowCount != 5)
            throw new InvalidOperationException("Grid-exact preview did not contain exactly five importable cells.");

        for (int rowIndex = 0; rowIndex < preview.Rows.Count; rowIndex++)
        {
            if (!preview.Rows[rowIndex].CanApply)
                throw new InvalidOperationException("Preview cell was not applicable: " + preview.Rows[rowIndex].Address);
        }

        ExcelDataImportPreviewRow formulaRow = FindFormulaRow(preview);

        if (!formulaRow.IsFormula ||
            formulaRow.FormulaExpression != "=" + FormulaExpression ||
            formulaRow.FormulaState != ExcelDataFormulaImportState.CachedResult ||
            formulaRow.Value != FormulaCachedValue)
            throw new InvalidOperationException("Valid cached formula metadata was not preserved by Preview Import.");
    }

    /// <summary>
    /// Verifies that apply committed every mapped cell once.
    /// </summary>
    /// <param name="result">Import apply result.</param>
    private static void ValidateApplyResult(ExcelDataImportApplyResult result)
    {
        if (result.AppliedRowCount != 5 || result.SkippedRowCount != 0)
            throw new InvalidOperationException("Grid-exact apply count mismatch.");
    }

    /// <summary>
    /// Verifies bool, enum, string, integer and object-reference values restored from exact workbook coordinates.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    private static void ValidateRestoredValues(SmokeAssets assets)
    {
        if (!assets.ExportPreset.IncludePlayerData)
            throw new InvalidOperationException("Boolean value was not restored.");

        if (assets.ExportPreset.TargetWorkbookProfile != ExcelDataWorkbookPathProfile.AssetsExportWorkbook)
            throw new InvalidOperationException("Enum value was not restored.");

        if (assets.ExportPreset.TargetWorkbookPath != "Assets/Exports/ConfiguredWorkbookWithLongReadableName.xlsx")
            throw new InvalidOperationException("String value was not restored.");

        if (assets.ValueLayout.DefaultGridRows != 73)
            throw new InvalidOperationException("Integer value was not restored.");

        if (assets.ReferenceOwner.LayoutPreset != assets.ReferenceLayout)
            throw new InvalidOperationException("Object reference was not restored.");
    }

    /// <summary>
    /// Verifies that changing the active layout after preview blocks apply before any mutation.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    /// <param name="approvedPreview">Previously approved preview.</param>
    private static void ValidateLayoutHashMutationIsBlocked(SmokeAssets assets,
                                                            ExcelDataImportPreviewResult approvedPreview)
    {
        ExcelDataWorkbookSheetDefinition sheet = assets.TransferLayout.SheetDefinitions[0];
        int originalCellCount = sheet.Cells.Count;
        sheet.Cells.Add(CreateLiteralCell(sheet.SheetId, 8, 8, "Hash Mutation"));
        bool blocked = false;

        try
        {
            try
            {
                ExcelDataImportApplyService.ApplyWorkbook(assets.TransferMaster,
                                                          WorkbookRelativePath,
                                                          approvedPreview);
            }
            catch (InvalidOperationException exception)
            {
                blocked = exception.Message.Contains("layout changed");
            }
        }
        finally
        {
            while (sheet.Cells.Count > originalCellCount)
                sheet.Cells.RemoveAt(sheet.Cells.Count - 1);
        }

        if (!blocked)
            throw new InvalidOperationException("Apply did not block a layout change made after preview.");
    }

    /// <summary>
    /// Verifies missing, erroneous, stale and policy-rejected formula caches block the complete preview.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    /// <param name="workbookPath">Exported workbook path mutated through OpenXML.</param>
    private static void ValidateBlockedFormulaStates(SmokeAssets assets, string workbookPath)
    {
        WriteFormulaCell(workbookPath, FormulaExpression, null, string.Empty);
        AssertBlockedFormulaState(assets,
                                  ExcelDataFormulaImportState.MissingCachedResult,
                                  "missing cached result");

        WriteFormulaCell(workbookPath, "1/0", "#DIV/0!", "e");
        AssertBlockedFormulaState(assets,
                                  ExcelDataFormulaImportState.CachedError,
                                  "cached Excel error");

        WriteFormulaCell(workbookPath,
                         FormulaExpression,
                         FormulaCachedValue,
                         string.Empty);
        SetManualCalculation(workbookPath, true);
        AssertBlockedFormulaState(assets,
                                  ExcelDataFormulaImportState.UntrustedCachedResult,
                                  "manual calculation mode");

        SetManualCalculation(workbookPath, false);
        SetSerializedValue(assets.ImportPreset,
                           "formulaImportPolicy",
                           property => property.enumValueIndex =
                               (int)ExcelDataFormulaImportPolicy.RejectFormulas);
        AssertBlockedFormulaState(assets,
                                  ExcelDataFormulaImportState.RejectedByPolicy,
                                  "formula rejection policy");
    }

    /// <summary>
    /// Runs Preview Import and asserts one exact formula blocking state.
    /// </summary>
    /// <param name="assets">Smoke asset graph.</param>
    /// <param name="expectedState">Expected formula resolution state.</param>
    /// <param name="scenario">Readable assertion scenario.</param>
    private static void AssertBlockedFormulaState(SmokeAssets assets,
                                                  ExcelDataFormulaImportState expectedState,
                                                  string scenario)
    {
        ExcelDataImportPreviewResult preview =
            ExcelDataImportPreviewService.PreviewWorkbook(assets.TransferMaster, WorkbookRelativePath);
        ExcelDataImportPreviewRow formulaRow = FindFormulaRow(preview);

        if (preview.CanApply || formulaRow.CanApply || formulaRow.FormulaState != expectedState)
            throw new InvalidOperationException("Preview did not block the " + scenario + ".");
    }

    /// <summary>
    /// Finds the mapped numeric formula row by its authoritative worksheet address.
    /// </summary>
    /// <param name="preview">Formula-aware import preview.</param>
    /// <returns>Mapped formula row.</returns>
    private static ExcelDataImportPreviewRow FindFormulaRow(ExcelDataImportPreviewResult preview)
    {
        for (int rowIndex = 0; rowIndex < preview.Rows.Count; rowIndex++)
        {
            if (preview.Rows[rowIndex].Address == FormulaCellAddress)
                return preview.Rows[rowIndex];
        }

        throw new InvalidOperationException("Formula smoke preview is missing " + FormulaCellAddress + ".");
    }
    #endregion

    #region Workbook Formula Mutation
    /// <summary>
    /// Replaces one mapped scalar with a formula and an optional persisted OpenXML result.
    /// </summary>
    /// <param name="workbookPath">Workbook path to update.</param>
    /// <param name="formulaExpression">Formula text without the leading equals sign.</param>
    /// <param name="cachedValue">Persisted result, or null to omit the cache element.</param>
    /// <param name="cachedDataType">Optional OpenXML cached-result type such as e for errors.</param>
    private static void WriteFormulaCell(string workbookPath,
                                         string formulaExpression,
                                         string cachedValue,
                                         string cachedDataType)
    {
        using (FileStream stream = new FileStream(workbookPath,
                                                  FileMode.Open,
                                                  FileAccess.ReadWrite,
                                                  FileShare.None))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, false))
        {
            string worksheetEntryPath =
                ExcelDataOpenXmlPackageUtility.BuildWorksheetEntryLookup(archive)[DataSheetName];
            XDocument worksheet =
                ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, worksheetEntryPath);
            XElement formulaCell = FindWorksheetCell(worksheet, FormulaCellAddress);
            RemoveCellPayload(formulaCell);
            formulaCell.SetAttributeValue("t",
                                          string.IsNullOrWhiteSpace(cachedDataType)
                                              ? null
                                              : cachedDataType);
            formulaCell.Add(new XElement(SpreadsheetNamespace + "f", formulaExpression));

            if (cachedValue != null)
                formulaCell.Add(new XElement(SpreadsheetNamespace + "v", cachedValue));

            ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive,
                                                            worksheetEntryPath,
                                                            worksheet);
        }
    }

    /// <summary>
    /// Sets or removes Manual calculation mode in workbook.xml.
    /// </summary>
    /// <param name="workbookPath">Workbook path to update.</param>
    /// <param name="manualCalculation">True to store Manual calculation; false to restore default automatic mode.</param>
    private static void SetManualCalculation(string workbookPath, bool manualCalculation)
    {
        const string workbookEntryPath = "xl/workbook.xml";

        using (FileStream stream = new FileStream(workbookPath,
                                                  FileMode.Open,
                                                  FileAccess.ReadWrite,
                                                  FileShare.None))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, false))
        {
            XDocument workbook =
                ExcelDataOpenXmlPackageUtility.LoadXmlEntry(archive, workbookEntryPath);
            XElement calculationProperties = workbook.Root.Element(SpreadsheetNamespace + "calcPr");

            if (manualCalculation)
            {
                if (calculationProperties == null)
                {
                    calculationProperties = new XElement(SpreadsheetNamespace + "calcPr");
                    workbook.Root.Add(calculationProperties);
                }

                calculationProperties.SetAttributeValue("calcMode", "manual");
            }
            else if (calculationProperties != null)
                calculationProperties.Remove();

            ExcelDataOpenXmlPackageUtility.ReplaceXmlEntry(archive,
                                                            workbookEntryPath,
                                                            workbook);
        }
    }

    /// <summary>
    /// Finds one materialized worksheet cell by exact A1 address.
    /// </summary>
    /// <param name="worksheet">Worksheet XML document.</param>
    /// <param name="address">Exact A1 address.</param>
    /// <returns>Existing worksheet cell element.</returns>
    private static XElement FindWorksheetCell(XDocument worksheet, string address)
    {
        foreach (XElement cell in worksheet.Descendants(SpreadsheetNamespace + "c"))
        {
            XAttribute addressAttribute = cell.Attribute("r");

            if (addressAttribute != null && addressAttribute.Value == address)
                return cell;
        }

        throw new InvalidOperationException("Workbook formula smoke cell was not found: " + address + ".");
    }

    /// <summary>
    /// Removes scalar, formula and inline-string payload nodes while retaining style and address attributes.
    /// </summary>
    /// <param name="cell">Worksheet cell to clear.</param>
    private static void RemoveCellPayload(XElement cell)
    {
        XElement formula = cell.Element(SpreadsheetNamespace + "f");
        XElement value = cell.Element(SpreadsheetNamespace + "v");
        XElement inlineString = cell.Element(SpreadsheetNamespace + "is");

        if (formula != null)
            formula.Remove();

        if (value != null)
            value.Remove();

        if (inlineString != null)
            inlineString.Remove();
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Holds the isolated temporary preset graph used by the smoke round trip.
    /// </summary>
    private sealed class SmokeAssets
    {
        #region Fields
        public ExcelDataTransferMasterPreset TransferMaster;
        public ExcelDataTransferMasterPreset ReferenceOwner;
        public ExcelDataWorkbookLayoutPreset TransferLayout;
        public ExcelDataWorkbookLayoutPreset ValueLayout;
        public ExcelDataWorkbookLayoutPreset ReferenceLayout;
        public ExcelDataWorkbookLayoutPreset AlternateLayout;
        public ExcelDataImportPreset ImportPreset;
        public ExcelDataExportPreset ExportPreset;
        #endregion
    }
    #endregion
}
