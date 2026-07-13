using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Editor-only smoke test for the complete grid-authoritative Excel Data Transfer Tool pipeline.
/// </summary>
public static class ExcelDataTransferToolSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates default preset creation and field catalog coverage from Unity batchmode.
    /// </summary>
    public static void Run()
    {
        ExcelDataTransferMasterPreset masterPreset = ExcelDataTransferAssetUtility.GetOrCreateDefaultMasterPreset();

        if (masterPreset == null)
            throw new InvalidOperationException("Default Excel data transfer master preset was not created.");

        ValidateLinkedPresets(masterPreset);
        ValidateManualPolicyFields();

        List<ExcelDataFieldCatalogEntry> entries = ExcelDataFieldCatalogBuilder.BuildCatalog();

        if (entries.Count <= 0)
            throw new InvalidOperationException("Excel data transfer field catalog did not find any fields.");

        ValidateCatalogCoverage(entries);
        ValidateDefaultWorkbookExport(masterPreset);
        ValidateWorkbookExport(entries);
        Debug.Log("[ExcelDataTransferToolSmokeTest] PASS - entries: " + entries.Count);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Validates that the default master preset owns every required sub-preset reference.
    /// </summary>
    /// <param name="masterPreset">Master preset created by the asset utility.</param>
    private static void ValidateLinkedPresets(ExcelDataTransferMasterPreset masterPreset)
    {
        if (masterPreset.ImportPreset == null)
            throw new InvalidOperationException("Default Excel data transfer import preset is missing.");

        if (masterPreset.ExportPreset == null)
            throw new InvalidOperationException("Default Excel data transfer export preset is missing.");

        if (masterPreset.LayoutPreset == null)
            throw new InvalidOperationException("Default Excel data transfer layout preset is missing.");

        if (masterPreset.BrushPalettePreset == null)
            throw new InvalidOperationException("Default Excel data transfer brush palette preset is missing.");

        if (masterPreset.BrushPalettePreset.Brushes.Count <= 0)
            throw new InvalidOperationException("Default Excel data transfer brush palette has no brushes.");

        if (masterPreset.LayoutPreset.SheetDefinitions.Count <= 0)
            throw new InvalidOperationException("Default Excel data transfer layout preset has no grid-authoritative worksheets.");

        int authoritativeCellCount = 0;

        // Count exact cells across sheets instead of relying on removed parallel selection or mapping lists.
        for (int sheetIndex = 0; sheetIndex < masterPreset.LayoutPreset.SheetDefinitions.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = masterPreset.LayoutPreset.SheetDefinitions[sheetIndex];

            if (sheet != null)
                authoritativeCellCount += sheet.Cells.Count;
        }

        if (authoritativeCellCount <= 0)
            throw new InvalidOperationException("Default Excel data transfer layout preset has no authoritative cells.");
    }

    /// <summary>
    /// Verifies manual enum and Boolean policy controls persist once without SerializedProperty binding recursion.
    /// </summary>
    private static void ValidateManualPolicyFields()
    {
        ExcelDataImportPreset importPreset = ScriptableObject.CreateInstance<ExcelDataImportPreset>();

        try
        {
            SerializedObject serializedObject = new SerializedObject(importPreset);
            SerializedProperty enumProperty = serializedObject.FindProperty("conflictPolicy");
            SerializedProperty toggleProperty = serializedObject.FindProperty("requirePreviewBeforeApply");
            VisualElement controlsRoot = new VisualElement();
            PopupField<string> enumField = ExcelDataLinkedSubPresetPanelFieldUtility.AddEnumPopupField(
                controlsRoot,
                serializedObject,
                "conflictPolicy",
                "Conflict Policy",
                "Smoke-test enum policy.",
                null);
            Toggle toggleField = ExcelDataLinkedSubPresetPanelFieldUtility.AddToggleField(
                controlsRoot,
                serializedObject,
                "requirePreviewBeforeApply",
                "Require Preview Before Apply",
                "Smoke-test Boolean policy.",
                null);

            if (enumProperty == null || toggleProperty == null || enumField == null || toggleField == null ||
                enumField.choices.Count <= 1)
                throw new InvalidOperationException("Manual policy controls could not be constructed.");

            int targetEnumIndex = (enumProperty.enumValueIndex + 1) % enumField.choices.Count;
            bool targetToggleValue = !toggleProperty.boolValue;
            bool enumChanged = ExcelDataLinkedSubPresetPanelFieldUtility.SetEnumPropertyValue(serializedObject,
                                                                                              "conflictPolicy",
                                                                                              targetEnumIndex);
            bool toggleChanged = ExcelDataLinkedSubPresetPanelFieldUtility.SetBooleanPropertyValue(serializedObject,
                                                                                                    "requirePreviewBeforeApply",
                                                                                                    targetToggleValue);
            serializedObject.Update();
            enumProperty = serializedObject.FindProperty("conflictPolicy");
            toggleProperty = serializedObject.FindProperty("requirePreviewBeforeApply");

            if (!enumChanged || !toggleChanged || enumProperty.enumValueIndex != targetEnumIndex ||
                toggleProperty.boolValue != targetToggleValue)
                throw new InvalidOperationException("Manual policy controls did not persist their selected values.");

            if (controlsRoot.Q<PropertyField>() != null)
                throw new InvalidOperationException("Manual scalar policy controls unexpectedly created an auto-bound PropertyField.");
        }
        finally
        {
            ScriptableObject.DestroyImmediate(importPreset);
        }
    }

    /// <summary>
    /// Validates that the first field catalog tranche exposes the required data families.
    /// </summary>
    /// <param name="entries">Catalog entries generated from current project assets.</param>
    private static void ValidateCatalogCoverage(List<ExcelDataFieldCatalogEntry> entries)
    {
        bool hasWaveEntry = false;
        bool hasConcreteListElement = false;
        bool hasReadableListIdentifier = false;
        bool hasObjectReference = false;
        bool hasScalingStatKey = false;
        bool hasScalingToggle = false;
        bool hasScalingFormula = false;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = entries[entryIndex];

            if (entry == null)
                continue;

            if (entry.Domain == ExcelDataTransferDomain.Waves)
                hasWaveEntry = true;

            if (entry.IsConcreteListElement)
            {
                hasConcreteListElement = true;

                if (entry.ReadablePath.Contains("_1", StringComparison.Ordinal) ||
                    entry.ReadablePath.Contains("_2", StringComparison.Ordinal))
                    hasReadableListIdentifier = true;
            }

            if (entry.DataKind == ExcelDataBrushDataKind.ListContainer ||
                entry.DataKind == ExcelDataBrushDataKind.ListElement)
                throw new InvalidOperationException("Field catalog still exposes a Generic/Complex list entry: " + entry.SerializedPath);

            if (entry.DataKind == ExcelDataBrushDataKind.ObjectReference)
                hasObjectReference = true;

            if (entry.Domain != ExcelDataTransferDomain.Player ||
                entry.SerializedPath.IndexOf("scalingRules.Array.data[", StringComparison.Ordinal) < 0)
                continue;

            if (entry.SerializedPath.EndsWith(".statKey", StringComparison.Ordinal) &&
                entry.DataKind == ExcelDataBrushDataKind.String)
                hasScalingStatKey = true;

            if (entry.SerializedPath.EndsWith(".addScaling", StringComparison.Ordinal) &&
                entry.DataKind == ExcelDataBrushDataKind.Boolean)
                hasScalingToggle = true;

            if (entry.SerializedPath.EndsWith(".formula", StringComparison.Ordinal) &&
                entry.DataKind == ExcelDataBrushDataKind.String)
                hasScalingFormula = true;
        }

        if (!hasWaveEntry)
            throw new InvalidOperationException("Field catalog does not expose EnemyWavePreset data.");

        if (!hasConcreteListElement)
            throw new InvalidOperationException("Field catalog does not expose concrete list element paths.");

        if (!hasReadableListIdentifier)
            throw new InvalidOperationException("Concrete list elements do not expose readable one-based identifiers.");

        if (!hasObjectReference)
            throw new InvalidOperationException("Field catalog does not expose object reference fields.");

        if (!hasScalingStatKey || !hasScalingToggle || !hasScalingFormula)
            throw new InvalidOperationException("Field catalog does not expose complete Player Add Scaling rule fields.");
    }

    /// <summary>
    /// Validates that the persistent default master exports only its exact grid-authoritative cells.
    /// </summary>
    /// <param name="masterPreset">Persistent default master preset graph.</param>
    private static void ValidateDefaultWorkbookExport(ExcelDataTransferMasterPreset masterPreset)
    {
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ExcelDataTransferDefaultGridExportSmokeTest.xlsx");
        int expectedCellCount = CountExportCells(masterPreset.LayoutPreset);
        ExcelDataExportResult result = ExcelDataExportService.ExportWorkbook(masterPreset, outputPath);

        if (expectedCellCount <= 0 || result.AuthoredCellCount != expectedCellCount)
            throw new InvalidOperationException("Default master export count does not match its exact layout cells.");

        if (result.UserSheetCount <= 0 || result.TechnicalRowCount <= expectedCellCount)
            throw new InvalidOperationException("Default master export is missing user or technical worksheet records.");

        FileInfo outputFile = new FileInfo(result.WorkbookPath);

        if (!outputFile.Exists || outputFile.Length <= 0)
            throw new InvalidOperationException("Default master export did not create a valid grid-authoritative workbook.");
    }

    /// <summary>
    /// Counts every coordinate-preserving export cell in one layout preset.
    /// </summary>
    /// <param name="layoutPreset">Grid-authoritative layout preset to inspect.</param>
    /// <returns>Number of cells included by export-enabled user worksheets.</returns>
    private static int CountExportCells(ExcelDataWorkbookLayoutPreset layoutPreset)
    {
        int cellCount = 0;
        List<ExcelDataWorkbookSheetDefinition> sheets = layoutPreset.SheetDefinitions;

        for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
        {
            ExcelDataWorkbookSheetDefinition sheet = sheets[sheetIndex];

            if (sheet == null || !sheet.ExportEnabled)
                continue;

            List<ExcelDataWorkbookCellDefinition> cells = sheet.Cells;

            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                ExcelDataWorkbookCellDefinition cell = cells[cellIndex];

                if (cell != null && cell.IncludesExport() && cell.RowIndex > 0 && cell.ColumnIndex > 0)
                    cellCount++;
            }
        }

        return cellCount;
    }

    /// <summary>
    /// Validates the MiniExcel backend by writing a real .xlsx workbook through the export service.
    /// </summary>
    /// <param name="entries">Catalog entries generated from current project assets.</param>
    private static void ValidateWorkbookExport(List<ExcelDataFieldCatalogEntry> entries)
    {
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ExcelDataTransferToolSmokeTest.xlsx");
        ExcelDataTransferMasterPreset smokeMasterPreset = ScriptableObject.CreateInstance<ExcelDataTransferMasterPreset>();
        ExcelDataWorkbookLayoutPreset smokeLayoutPreset = ScriptableObject.CreateInstance<ExcelDataWorkbookLayoutPreset>();
        ExcelDataExportPreset smokeExportPreset = ScriptableObject.CreateInstance<ExcelDataExportPreset>();
        ExcelDataImportPreset smokeImportPreset = ScriptableObject.CreateInstance<ExcelDataImportPreset>();
        ExcelDataBrushPalettePreset smokeBrushPalettePreset = ScriptableObject.CreateInstance<ExcelDataBrushPalettePreset>();
        ExcelDataFieldCatalogEntry mappedEntry = FindFirstExportableEntry(entries);
        ExcelDataWorkbookSheetDefinition sheet = new ExcelDataWorkbookSheetDefinition();
        sheet.Configure("Grid Export",
                        8,
                        8,
                        120,
                        28,
                        true,
                        true,
                        ExcelDataWorkbookSheetVisibility.Visible);
        ExcelDataWorkbookCellDefinition literalCell = new ExcelDataWorkbookCellDefinition();
        literalCell.ConfigureLiteralText(sheet.SheetId,
                                         1,
                                         1,
                                         "Smoke Export",
                                         ExcelDataTransferDirection.Export,
                                         "SmokeLiteral",
                                         false);
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.ConfigureFromEntry(mappedEntry);
        ExcelDataWorkbookCellDefinition dataCell = new ExcelDataWorkbookCellDefinition();
        dataCell.ConfigureDataField(sheet.SheetId,
                                    2,
                                    1,
                                    binding,
                                    ExcelDataTransferDirection.Export,
                                    "SmokeData",
                                    string.Empty);
        sheet.Cells.Add(literalCell);
        sheet.Cells.Add(dataCell);
        smokeLayoutPreset.SheetDefinitions.Add(sheet);
        smokeMasterPreset.AssignLinkedPresets(smokeLayoutPreset, smokeBrushPalettePreset, smokeImportPreset, smokeExportPreset);

        try
        {
            ExcelDataExportResult result = ExcelDataExportService.ExportWorkbook(smokeMasterPreset, outputPath);

            if (result.UserSheetCount != 1)
                throw new InvalidOperationException("Workbook export did not produce the expected user worksheet.");

            if (result.AuthoredCellCount != 2 || result.WrittenCellCount != 2)
                throw new InvalidOperationException("Workbook export did not preserve both authored cells.");

            if (result.DataFieldCellCount != 1 || result.LiteralCellCount != 1)
                throw new InvalidOperationException("Workbook export cell-kind counters are incorrect.");

            if (result.TechnicalRowCount < 5 || string.IsNullOrWhiteSpace(result.LayoutHash))
                throw new InvalidOperationException("Workbook export did not produce technical metadata and a layout hash.");

            FileInfo outputFile = new FileInfo(result.WorkbookPath);

            if (!outputFile.Exists || outputFile.Length <= 0)
                throw new InvalidOperationException("Workbook export did not create a valid .xlsx file.");

        }
        finally
        {
            ScriptableObject.DestroyImmediate(smokeMasterPreset);
            ScriptableObject.DestroyImmediate(smokeLayoutPreset);
            ScriptableObject.DestroyImmediate(smokeExportPreset);
            ScriptableObject.DestroyImmediate(smokeImportPreset);
            ScriptableObject.DestroyImmediate(smokeBrushPalettePreset);
        }
    }

    /// <summary>
    /// Finds one concrete catalog field suitable for a smoke-test brush mapping.
    /// </summary>
    /// <param name="entries">Catalog entries generated from current project assets.</param>
    /// <returns>First entry with a stable field id.</returns>
    private static ExcelDataFieldCatalogEntry FindFirstExportableEntry(List<ExcelDataFieldCatalogEntry> entries)
    {
        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ExcelDataFieldCatalogEntry entry = entries[entryIndex];

            if (entry == null)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.FieldId))
                return entry;
        }

        throw new InvalidOperationException("No exportable catalog entry found.");
    }
    #endregion

    #endregion
}
