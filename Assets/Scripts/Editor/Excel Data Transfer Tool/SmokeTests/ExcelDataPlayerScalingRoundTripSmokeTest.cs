using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies Player scaling formulas through real grid-authoritative export, preview, atomic apply, merge and ECS baking.
/// </summary>
public static class ExcelDataPlayerScalingRoundTripSmokeTest
{
    #region Constants
    private const int MappedCellCount = 15;
    private const int NumericRuleIndex = 0;
    private const int BooleanRuleIndex = 1;
    private const int TokenRuleIndex = 2;
    private const int ColorRuleIndex = 3;
    private const int EnumRuleIndex = 0;
    private const string MergeStatKeyCellAddress = "A3";
    private const string MergeFormulaCellAddress = "C3";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs the complete temporary workbook round trip without exposing a permanent editor menu command.
    /// </summary>
    public static void Run()
    {
        ExcelDataPlayerScalingSmokeAssets assets = null;

        try
        {
            assets = ExcelDataPlayerScalingSmokeAssetUtility.Create();
            ExcelDataPlayerScalingWorkbookSmokeContext workbookContext =
                ExcelDataPlayerScalingWorkbookSmokeUtility.Create(assets);
            ConfigureBakeBaseValues(assets);
            AssetDatabase.SaveAssets();

            // Export valid authoring before deliberately changing every mapped rule member.
            ExcelDataExportResult exportResult = ExcelDataExportService.ExportWorkbook(
                workbookContext.TransferMasterPreset,
                workbookContext.WorkbookPath);
            ValidateExport(exportResult);
            DisableMappedRules(assets);
            AssetDatabase.SaveAssets();

            ExcelDataImportPreviewResult preview = ExcelDataImportPreviewService.PreviewWorkbook(
                workbookContext.TransferMasterPreset,
                workbookContext.WorkbookPath);
            ValidateApplicablePreview(preview, "existing rules round trip");
            ExcelDataImportApplyResult applyResult = ExcelDataImportApplyService.ApplyWorkbook(
                workbookContext.TransferMasterPreset,
                workbookContext.WorkbookPath,
                preview);
            ValidateApplyResult(applyResult, "existing rules round trip");
            ValidateRestoredRules(assets);
            ValidateReimportAndBake(assets, false);

            // Exercise an invalid and then valid stat-key merge through the actual visible workbook cells.
            ValidateControlledMerge(workbookContext, assets, exportResult.WorkbookPath);
            Debug.Log("[ExcelDataPlayerScalingRoundTripSmokeTest] PASS: formula-aware xlsx round trip, atomic merge, reimport persistence and production ECS blobs validated.");
        }
        finally
        {
            ExcelDataPlayerScalingSmokeAssetUtility.Delete(assets);
        }
    }
    #endregion

    #region Setup
    /// <summary>
    /// Configures deterministic source values and two valid token schedules used by production bake assertions.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ConfigureBakeBaseValues(ExcelDataPlayerScalingSmokeAssets assets)
    {
        SerializedObject progression = new SerializedObject(assets.ProgressionPreset);
        progression.FindProperty("experiencePickupRadius").floatValue = 5f;
        progression.FindProperty("milestoneTimeScaleResumeDurationSeconds").floatValue = 0.2f;
        progression.FindProperty("milestoneSkipOnlyFromExitInput").boolValue = false;
        progression.FindProperty("milestoneSkipHoldFillColor").colorValue = new Color(0.8f, 0.4f, 0.2f, 1f);
        progression.FindProperty("equippedScheduleId").stringValue = "Base";
        SerializedProperty schedules = progression.FindProperty("schedules");
        schedules.arraySize = 2;
        ConfigureSchedule(schedules.GetArrayElementAtIndex(0), "Base");
        ConfigureSchedule(schedules.GetArrayElementAtIndex(1), "Scaled");
        progression.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(assets.ProgressionPreset);

        SerializedObject controller = new SerializedObject(assets.ControllerPreset);
        controller.FindProperty("movementSettings.directionsMode").enumValueIndex =
            (int)MovementDirectionsMode.AllDirections;
        controller.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(assets.ControllerPreset);
    }

    /// <summary>
    /// Configures one token schedule with an empty sequence so its ID remains a valid baked target.
    /// </summary>
    /// <param name="scheduleProperty">Serialized schedule list element.</param>
    /// <param name="scheduleId">Stable schedule identifier.</param>
    private static void ConfigureSchedule(SerializedProperty scheduleProperty, string scheduleId)
    {
        scheduleProperty.FindPropertyRelative("scheduleId").stringValue = scheduleId;
        scheduleProperty.FindPropertyRelative("sequence").arraySize = 0;
    }

    /// <summary>
    /// Replaces every mapped formula with a no-op and disables Add Scaling so import must restore workbook state.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void DisableMappedRules(ExcelDataPlayerScalingSmokeAssets assets)
    {
        for (int ruleIndex = NumericRuleIndex; ruleIndex <= ColorRuleIndex; ruleIndex++)
            SetRuleState(assets.ProgressionPreset, ruleIndex, false, "[this]");

        SetRuleState(assets.ControllerPreset, EnumRuleIndex, false, "[this]");
    }

    /// <summary>
    /// Updates direct rule members without changing statKey or list structure.
    /// </summary>
    /// <param name="owner">Player preset containing scalingRules.</param>
    /// <param name="ruleIndex">Zero-based rule index.</param>
    /// <param name="addScaling">Replacement Add Scaling state.</param>
    /// <param name="formula">Replacement unified formula.</param>
    private static void SetRuleState(Object owner,
                                     int ruleIndex,
                                     bool addScaling,
                                     string formula)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty rules = serializedObject.FindProperty("scalingRules");

        if (rules == null || ruleIndex < 0 || ruleIndex >= rules.arraySize)
            throw new InvalidOperationException("Scaling smoke rule index is unavailable on " + owner.name + ".");

        SerializedProperty rule = rules.GetArrayElementAtIndex(ruleIndex);
        rule.FindPropertyRelative("addScaling").boolValue = addScaling;
        rule.FindPropertyRelative("formula").stringValue = formula;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }
    #endregion

    #region Round Trip Assertions
    /// <summary>
    /// Verifies that export wrote the expected workbook and all mapped Data Field values.
    /// </summary>
    /// <param name="result">Export operation result.</param>
    private static void ValidateExport(ExcelDataExportResult result)
    {
        if (result == null || !System.IO.File.Exists(result.WorkbookPath))
            throw new InvalidOperationException("Player scaling export did not create its workbook.");

        if (result.DataFieldCellCount != MappedCellCount)
            throw new InvalidOperationException("Player scaling export count mismatch.");
    }

    /// <summary>
    /// Verifies one formula-aware preview is layout-compatible and fully applicable.
    /// </summary>
    /// <param name="preview">Import preview to inspect.</param>
    /// <param name="scenario">Readable scenario label.</param>
    private static void ValidateApplicablePreview(ExcelDataImportPreviewResult preview, string scenario)
    {
        if (preview == null || !preview.CanApply || !preview.LayoutHashMatches)
            throw new InvalidOperationException("Formula-aware preview blocked " + scenario + ": " +
                                                (preview == null ? "missing result" : preview.ValidationMessage));

        if (preview.ImportableRowCount != MappedCellCount || preview.Rows.Count != MappedCellCount)
            throw new InvalidOperationException("Formula-aware preview count mismatch for " + scenario + ".");

        for (int rowIndex = 0; rowIndex < preview.Rows.Count; rowIndex++)
        {
            if (!preview.Rows[rowIndex].CanApply)
                throw new InvalidOperationException("Formula-aware preview rejected " +
                                                    preview.Rows[rowIndex].Address + " during " + scenario + ".");
        }
    }

    /// <summary>
    /// Verifies atomic apply counts and the explicit automatic Player bake refresh status.
    /// </summary>
    /// <param name="result">Import apply result.</param>
    /// <param name="scenario">Readable scenario label.</param>
    private static void ValidateApplyResult(ExcelDataImportApplyResult result, string scenario)
    {
        if (result == null || result.AppliedRowCount != MappedCellCount || result.SkippedRowCount != 0)
            throw new InvalidOperationException("Formula-aware apply count mismatch for " + scenario + ".");

        if (!result.AuthoringStatus.Contains("Player Bake Queued", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Formula-aware apply omitted its automatic Player bake status.");
    }

    /// <summary>
    /// Verifies that every typed rule was restored exactly from workbook cells.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateRestoredRules(ExcelDataPlayerScalingSmokeAssets assets)
    {
        AssertRule(assets.ProgressionPreset,
                   NumericRuleIndex,
                   assets.NumericStatKey,
                   "[this] + [Level]");
        AssertRule(assets.ProgressionPreset,
                   BooleanRuleIndex,
                   assets.BooleanStatKey,
                   "[Level] > 0");
        AssertRule(assets.ProgressionPreset,
                   TokenRuleIndex,
                   assets.TokenStatKey,
                   "[ModeToken]");
        AssertRule(assets.ProgressionPreset,
                   ColorRuleIndex,
                   assets.ColorChannelStatKey,
                   "[this] * 0.5");
        AssertRule(assets.ControllerPreset,
                   EnumRuleIndex,
                   assets.EnumStatKey,
                   "[this] + 1");
    }

    /// <summary>
    /// Verifies one existing rule still owns its stable key, enabled state and exact formula.
    /// </summary>
    /// <param name="owner">Player preset containing scalingRules.</param>
    /// <param name="ruleIndex">Zero-based rule index.</param>
    /// <param name="expectedStatKey">Expected stable stat key.</param>
    /// <param name="expectedFormula">Expected imported formula.</param>
    private static void AssertRule(Object owner,
                                   int ruleIndex,
                                   string expectedStatKey,
                                   string expectedFormula)
    {
        SerializedObject serializedObject = new SerializedObject(owner);
        SerializedProperty rule = serializedObject.FindProperty("scalingRules").GetArrayElementAtIndex(ruleIndex);

        if (!string.Equals(rule.FindPropertyRelative("statKey").stringValue,
                           expectedStatKey,
                           StringComparison.Ordinal) ||
            !rule.FindPropertyRelative("addScaling").boolValue ||
            !string.Equals(rule.FindPropertyRelative("formula").stringValue,
                           expectedFormula,
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Imported scaling rule did not match workbook state on " + owner.name + ".");
    }
    #endregion

    #region Merge Assertions
    /// <summary>
    /// Verifies invalid merge rollback followed by one complete stat-key append through real workbook import.
    /// </summary>
    /// <param name="workbookContext">Temporary transfer graph.</param>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="workbookPath">Absolute exported workbook path.</param>
    private static void ValidateControlledMerge(ExcelDataPlayerScalingWorkbookSmokeContext workbookContext,
                                                ExcelDataPlayerScalingSmokeAssets assets,
                                                string workbookPath)
    {
        ExcelDataPlayerScalingSmokeAssetUtility.SetImportPolicy(
            assets.ImportPreset,
            ExcelDataScalingRuleImportPolicy.MergeRulesByStatKey);
        AssetDatabase.SaveAssets();
        int originalRuleCount = ExcelDataPlayerScalingSmokeAssetUtility.ReadRuleCount(assets.ProgressionPreset);

        // Retarget the complete first rule group, then prove a semantic failure cannot mutate the source list.
        ExcelDataPlayerScalingWorkbookSmokeUtility.ReplaceVisibleCellString(workbookPath,
                                                                            MergeStatKeyCellAddress,
                                                                            assets.MergeTargetStatKey);
        ExcelDataPlayerScalingWorkbookSmokeUtility.ReplaceVisibleCellString(workbookPath,
                                                                            MergeFormulaCellAddress,
                                                                            "[UnknownStat] + [this]");
        ExcelDataImportPreviewResult invalidPreview = ExcelDataImportPreviewService.PreviewWorkbook(
            workbookContext.TransferMasterPreset,
            workbookPath);
        ValidateInvalidMergeRollback(workbookContext, assets, invalidPreview, workbookPath, originalRuleCount);

        // Correcting the same cell yields one complete, unique append while preserving the source rule.
        ExcelDataPlayerScalingWorkbookSmokeUtility.ReplaceVisibleCellString(workbookPath,
                                                                            MergeFormulaCellAddress,
                                                                            "[this] + [Level]");
        ExcelDataImportPreviewResult mergePreview = ExcelDataImportPreviewService.PreviewWorkbook(
            workbookContext.TransferMasterPreset,
            workbookPath);
        ValidateApplicablePreview(mergePreview, "controlled scaling-rule merge");
        ExcelDataImportApplyResult mergeResult = ExcelDataImportApplyService.ApplyWorkbook(
            workbookContext.TransferMasterPreset,
            workbookPath,
            mergePreview);
        ValidateApplyResult(mergeResult, "controlled scaling-rule merge");
        ValidateMergedRule(assets, originalRuleCount);
        ValidateReimportAndBake(assets, true);
    }

    /// <summary>
    /// Verifies an invalid semantic preview blocks apply and leaves the source scalingRules list untouched.
    /// </summary>
    /// <param name="workbookContext">Temporary transfer graph.</param>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="invalidPreview">Preview containing the intentionally unknown variable.</param>
    /// <param name="workbookPath">Absolute workbook path.</param>
    /// <param name="originalRuleCount">Rule count before the attempted merge.</param>
    private static void ValidateInvalidMergeRollback(ExcelDataPlayerScalingWorkbookSmokeContext workbookContext,
                                                     ExcelDataPlayerScalingSmokeAssets assets,
                                                     ExcelDataImportPreviewResult invalidPreview,
                                                     string workbookPath,
                                                     int originalRuleCount)
    {
        if (invalidPreview == null || invalidPreview.CanApply)
            throw new InvalidOperationException("Unknown-variable merge preview was unexpectedly applicable.");

        bool applyBlocked = false;

        try
        {
            ExcelDataImportApplyService.ApplyWorkbook(workbookContext.TransferMasterPreset,
                                                       workbookPath,
                                                       invalidPreview);
        }
        catch (InvalidOperationException)
        {
            applyBlocked = true;
        }

        if (!applyBlocked ||
            ExcelDataPlayerScalingSmokeAssetUtility.ReadRuleCount(assets.ProgressionPreset) != originalRuleCount)
            throw new InvalidOperationException("Invalid scaling-rule merge did not roll back atomically.");
    }

    /// <summary>
    /// Verifies exactly one initialized rule was appended and the original stable-key rule was preserved.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="originalRuleCount">Rule count before merge.</param>
    private static void ValidateMergedRule(ExcelDataPlayerScalingSmokeAssets assets, int originalRuleCount)
    {
        SerializedObject serializedObject = new SerializedObject(assets.ProgressionPreset);
        SerializedProperty rules = serializedObject.FindProperty("scalingRules");

        if (rules.arraySize != originalRuleCount + 1)
            throw new InvalidOperationException("Controlled scaling merge did not append exactly one rule.");

        AssertRule(assets.ProgressionPreset,
                   NumericRuleIndex,
                   assets.NumericStatKey,
                   "[this] + [Level]");
        AssertRule(assets.ProgressionPreset,
                   rules.arraySize - 1,
                   assets.MergeTargetStatKey,
                   "[this] + [Level]");
    }
    #endregion

    #region Bake Assertions
    /// <summary>
    /// Force-reimports authoring to prove persistence, then validates production scaled blobs for every typed family.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="expectMergedRule">True when the merged numeric rule must also affect baking.</param>
    private static void ValidateReimportAndBake(ExcelDataPlayerScalingSmokeAssets assets,
                                                bool expectMergedRule)
    {
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(assets.ProgressionPreset),
                                  ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(assets.ControllerPreset),
                                  ImportAssetOptions.ForceUpdate);
        ValidateRestoredRules(assets);
        PlayerScalingExcelBakeSmokeAssertionUtility.AssertImportedScaling(assets.ControllerPreset,
                                                                          assets.ProgressionPreset,
                                                                          expectMergedRule);
    }
    #endregion

    #endregion
}
