using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies typed formula preflight, dependency diagnostics and controlled Player scaling-rule list semantics.
/// </summary>
public static class ExcelDataPlayerScalingSemanticSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs isolated planner validation without adding permanent editor menu commands.
    /// </summary>
    public static void Run()
    {
        ExcelDataPlayerScalingSmokeAssets assets = null;

        try
        {
            assets = ExcelDataPlayerScalingSmokeAssetUtility.Create();
            ValidateTypedFormulaFamilies(assets);
            ValidateFormulaFailures(assets);
            ValidateDependencyCycle(assets);
            ValidateNonScalingPlayerRefreshRegistration(assets);
            ValidateExistingRulesOnlyPolicy(assets);
            ValidateMergeRuleCreation(assets);
            ValidateIncompleteMergeRule(assets);
            ValidateDuplicateMergeTarget(assets);
            Debug.Log("[ExcelDataPlayerScalingSemanticSmokeTest] PASS: typed formulas, failures, cycle detection, general Player bake refresh and atomic merge planning validated.");
        }
        finally
        {
            ExcelDataPlayerScalingSmokeAssetUtility.Delete(assets);
        }
    }
    #endregion

    #region Bake Refresh Validation
    /// <summary>
    /// Verifies a regular Player data cell registers its owner for automatic bake refresh without scaling routing.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateNonScalingPlayerRefreshRegistration(ExcelDataPlayerScalingSmokeAssets assets)
    {
        ExcelDataPlayerScalingImportPlan plan = BuildPlan(
            assets,
            CreatePlayerDataCell(assets.ControllerPreset,
                                 "movementSettings.values.baseSpeed",
                                 "5",
                                 25));
        AssertPlanValid(plan, "non-scaling Player bake registration");

        for (int assetIndex = 0; assetIndex < plan.AffectedAssets.Count; assetIndex++)
        {
            if (plan.AffectedAssets[assetIndex] == assets.ControllerPreset)
                return;
        }

        throw new InvalidOperationException("A non-scaling Player import did not register its owner for bake refresh.");
    }
    #endregion

    #region Typed Formula Validation
    /// <summary>
    /// Verifies numeric, Boolean, token, Color-channel and enum formulas against PMT result-type contracts.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateTypedFormulaFamilies(ExcelDataPlayerScalingSmokeAssets assets)
    {
        AssertPlanValid(BuildPlan(assets,
                                  CreateFormulaCell(assets.ProgressionPreset,
                                                    0,
                                                    assets.NumericStatKey,
                                                    "[this] + [Level]",
                                                    1)),
                        "numeric formula");
        AssertPlanValid(BuildPlan(assets,
                                  CreateFormulaCell(assets.ProgressionPreset,
                                                    1,
                                                    assets.BooleanStatKey,
                                                    "[Level] > 0",
                                                    2)),
                        "Boolean formula");
        AssertPlanValid(BuildPlan(assets,
                                  CreateFormulaCell(assets.ProgressionPreset,
                                                    2,
                                                    assets.TokenStatKey,
                                                    "[ModeToken]",
                                                    3)),
                        "token formula");
        AssertPlanValid(BuildPlan(assets,
                                  CreateFormulaCell(assets.ProgressionPreset,
                                                    3,
                                                    assets.ColorChannelStatKey,
                                                    "[this] * 0.5",
                                                    4)),
                        "Color channel formula");
        AssertPlanValid(BuildPlan(assets,
                                  CreateFormulaCell(assets.ControllerPreset,
                                                    0,
                                                    assets.EnumStatKey,
                                                    "[this] + 1",
                                                    5)),
                        "enum formula");
    }
    #endregion

    #region Failure Validation
    /// <summary>
    /// Verifies unknown variables, malformed syntax, result-type mismatch and empty stat keys block preflight.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateFormulaFailures(ExcelDataPlayerScalingSmokeAssets assets)
    {
        AssertPlanInvalid(BuildPlan(assets,
                                    CreateFormulaCell(assets.ProgressionPreset,
                                                      0,
                                                      assets.NumericStatKey,
                                                      "[UnknownStat] + [this]",
                                                      10)),
                          "Unknown scalable stat variable",
                          "unknown variable");
        AssertPlanInvalid(BuildPlan(assets,
                                    CreateFormulaCell(assets.ProgressionPreset,
                                                      0,
                                                      assets.NumericStatKey,
                                                      "[this] + (",
                                                      11)),
                          string.Empty,
                          "invalid syntax");
        AssertPlanInvalid(BuildPlan(assets,
                                    CreateFormulaCell(assets.ProgressionPreset,
                                                      1,
                                                      assets.BooleanStatKey,
                                                      "[Level] + 1",
                                                      12)),
                          "Number",
                          "Boolean result type");
        ExcelDataPlayerScalingImportCell missingKeyCell =
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                0,
                assets.NumericStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                string.Empty,
                13);
        AssertPlanInvalid(BuildPlan(assets, missingKeyCell),
                          "statKey",
                          "missing stat key");
    }

    /// <summary>
    /// Verifies the shared PMT dependency graph catches a two-stat circular formula import.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateDependencyCycle(ExcelDataPlayerScalingSmokeAssets assets)
    {
        List<ExcelDataPlayerScalingImportCell> cells = new List<ExcelDataPlayerScalingImportCell>
        {
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                4,
                assets.LevelDefaultStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName,
                "True",
                20),
            CreateFormulaCell(assets.ProgressionPreset,
                              4,
                              assets.LevelDefaultStatKey,
                              "[Bonus]",
                              21),
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                5,
                assets.BonusDefaultStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName,
                "True",
                22),
            CreateFormulaCell(assets.ProgressionPreset,
                              5,
                              assets.BonusDefaultStatKey,
                              "[Level]",
                              23)
        };
        AssertPlanInvalid(BuildPlan(assets, cells),
                          "Circular dependency",
                          "scalable stat cycle");
    }
    #endregion

    #region List Policy Validation
    /// <summary>
    /// Verifies Existing Rules Only rejects stat-key retargeting and preserves list structure.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateExistingRulesOnlyPolicy(ExcelDataPlayerScalingSmokeAssets assets)
    {
        ExcelDataPlayerScalingSmokeAssetUtility.SetImportPolicy(assets.ImportPreset,
                                                                ExcelDataScalingRuleImportPolicy.ExistingRulesOnly);
        ExcelDataPlayerScalingImportCell retargetCell =
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                0,
                assets.NumericStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                assets.MergeTargetStatKey,
                30);
        AssertPlanInvalid(BuildPlan(assets, retargetCell),
                          "Existing Rules Only",
                          "existing-rule retarget");
    }

    /// <summary>
    /// Verifies a complete unique merge group plans one append without mutating source authoring during preview.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateMergeRuleCreation(ExcelDataPlayerScalingSmokeAssets assets)
    {
        ExcelDataPlayerScalingSmokeAssetUtility.SetImportPolicy(assets.ImportPreset,
                                                                ExcelDataScalingRuleImportPolicy.MergeRulesByStatKey);
        int originalRuleCount = ExcelDataPlayerScalingSmokeAssetUtility.ReadRuleCount(assets.ProgressionPreset);
        List<ExcelDataPlayerScalingImportCell> cells = BuildCompleteMergeGroup(assets,
                                                                              0,
                                                                              assets.NumericStatKey,
                                                                              assets.MergeTargetStatKey,
                                                                              40);
        ExcelDataPlayerScalingImportPlan plan = BuildPlan(assets, cells);
        AssertPlanValid(plan, "complete merge creation");

        if (plan.Creations.Count != 1)
            throw new InvalidOperationException("Complete merge group did not plan exactly one scaling-rule append.");

        if (ExcelDataPlayerScalingSmokeAssetUtility.ReadRuleCount(assets.ProgressionPreset) != originalRuleCount)
            throw new InvalidOperationException("Semantic preview mutated the source scalingRules list.");

        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            if (!plan.TryGetRoute(cells[cellIndex].CellDefinition, out ExcelDataPlayerScalingWriteRoute _))
                throw new InvalidOperationException("Complete merge group did not route every mandatory member.");
        }
    }

    /// <summary>
    /// Verifies creation is blocked when the workbook omits one mandatory rule member.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateIncompleteMergeRule(ExcelDataPlayerScalingSmokeAssets assets)
    {
        List<ExcelDataPlayerScalingImportCell> cells = new List<ExcelDataPlayerScalingImportCell>
        {
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                0,
                assets.NumericStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                assets.MergeTargetStatKey,
                50),
            CreateFormulaCell(assets.ProgressionPreset,
                              0,
                              assets.NumericStatKey,
                              "[this] + [Level]",
                              51)
        };
        AssertPlanInvalid(BuildPlan(assets, cells),
                          "statKey, addScaling and formula",
                          "incomplete merge group");
    }

    /// <summary>
    /// Verifies two source groups cannot silently converge on the same new stat key.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    private static void ValidateDuplicateMergeTarget(ExcelDataPlayerScalingSmokeAssets assets)
    {
        List<ExcelDataPlayerScalingImportCell> cells = BuildCompleteMergeGroup(assets,
                                                                              0,
                                                                              assets.NumericStatKey,
                                                                              assets.MergeTargetStatKey,
                                                                              60);
        cells.AddRange(BuildCompleteMergeGroup(assets,
                                               1,
                                               assets.BooleanStatKey,
                                               assets.MergeTargetStatKey,
                                               70));
        AssertPlanInvalid(BuildPlan(assets, cells),
                          "source groups",
                          "duplicate merge target");
        ExcelDataPlayerScalingSmokeAssetUtility.SetImportPolicy(assets.ImportPreset,
                                                                ExcelDataScalingRuleImportPolicy.ExistingRulesOnly);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates one direct non-list Player data cell used to verify general authoring refresh coverage.
    /// </summary>
    /// <param name="owner">Persistent Player preset owner.</param>
    /// <param name="serializedPath">Concrete serialized property path.</param>
    /// <param name="incomingValue">Workbook value staged during semantic planning.</param>
    /// <param name="rowIndex">One-based smoke worksheet row.</param>
    /// <returns>Coordinate-aware Player data cell.</returns>
    private static ExcelDataPlayerScalingImportCell CreatePlayerDataCell(UnityEngine.Object owner,
                                                                         string serializedPath,
                                                                         string incomingValue,
                                                                         int rowIndex)
    {
        string ownerPath = AssetDatabase.GetAssetPath(owner);
        ExcelDataFieldBinding binding = new ExcelDataFieldBinding();
        binding.Configure("PlayerDataSmoke:" + Guid.NewGuid().ToString("N"),
                          ExcelDataTransferDomain.Player,
                          AssetDatabase.AssetPathToGUID(ownerPath),
                          owner.GetType().Name,
                          ownerPath,
                          serializedPath,
                          serializedPath,
                          ExcelDataBrushDataKind.Number);
        ExcelDataWorkbookCellDefinition cellDefinition = new ExcelDataWorkbookCellDefinition();
        cellDefinition.ConfigureDataField("Player Scaling",
                                          rowIndex,
                                          1,
                                          binding,
                                          ExcelDataTransferDirection.Both,
                                          "PlayerDataSmoke",
                                          string.Empty);
        ExcelDataImportCellValue cellValue = new ExcelDataImportCellValue(incomingValue,
                                                                          string.Empty,
                                                                          string.Empty,
                                                                          string.Empty);
        return new ExcelDataPlayerScalingImportCell("Player Scaling",
                                                    ExcelDataWorkbookCoordinateUtility.BuildAddress(rowIndex, 1),
                                                    cellDefinition,
                                                    cellValue);
    }

    /// <summary>
    /// Creates one incoming formula cell for an existing scaling rule.
    /// </summary>
    /// <param name="owner">Player preset owner.</param>
    /// <param name="ruleIndex">Zero-based source rule index.</param>
    /// <param name="sourceStatKey">Current source rule key.</param>
    /// <param name="formula">Incoming formula text.</param>
    /// <param name="rowIndex">One-based smoke worksheet row.</param>
    /// <returns>Coordinate-aware formula cell.</returns>
    private static ExcelDataPlayerScalingImportCell CreateFormulaCell(UnityEngine.Object owner,
                                                                      int ruleIndex,
                                                                      string sourceStatKey,
                                                                      string formula,
                                                                      int rowIndex)
    {
        return ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
            owner,
            ruleIndex,
            sourceStatKey,
            ExcelDataPlayerScalingRuleSerializedUtility.FormulaMemberName,
            formula,
            rowIndex);
    }

    /// <summary>
    /// Builds all mandatory incoming cells for one controlled merge creation.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="sourceRuleIndex">Zero-based source rule index.</param>
    /// <param name="sourceStatKey">Current source rule key.</param>
    /// <param name="desiredStatKey">Unique desired target key.</param>
    /// <param name="firstRowIndex">First one-based row used by the group.</param>
    /// <returns>Complete statKey, addScaling and formula cell group.</returns>
    private static List<ExcelDataPlayerScalingImportCell> BuildCompleteMergeGroup(
        ExcelDataPlayerScalingSmokeAssets assets,
        int sourceRuleIndex,
        string sourceStatKey,
        string desiredStatKey,
        int firstRowIndex)
    {
        return new List<ExcelDataPlayerScalingImportCell>
        {
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                sourceRuleIndex,
                sourceStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                desiredStatKey,
                firstRowIndex),
            ExcelDataPlayerScalingSmokeAssetUtility.CreateScalingCell(
                assets.ProgressionPreset,
                sourceRuleIndex,
                sourceStatKey,
                ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName,
                "True",
                firstRowIndex + 1),
            CreateFormulaCell(assets.ProgressionPreset,
                              sourceRuleIndex,
                              sourceStatKey,
                              "[this] + [Level]",
                              firstRowIndex + 2)
        };
    }

    /// <summary>
    /// Builds one semantic plan from a single incoming cell.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="cell">Single incoming cell.</param>
    /// <returns>Formula-aware import plan.</returns>
    private static ExcelDataPlayerScalingImportPlan BuildPlan(ExcelDataPlayerScalingSmokeAssets assets,
                                                               ExcelDataPlayerScalingImportCell cell)
    {
        return BuildPlan(assets, new List<ExcelDataPlayerScalingImportCell> { cell });
    }

    /// <summary>
    /// Builds one semantic plan from a combined incoming cell collection.
    /// </summary>
    /// <param name="assets">Temporary Player authoring graph.</param>
    /// <param name="cells">Incoming workbook cells.</param>
    /// <returns>Formula-aware import plan.</returns>
    private static ExcelDataPlayerScalingImportPlan BuildPlan(ExcelDataPlayerScalingSmokeAssets assets,
                                                               IReadOnlyList<ExcelDataPlayerScalingImportCell> cells)
    {
        return ExcelDataPlayerScalingImportPlanBuilder.Build(cells, assets.ImportPreset);
    }

    /// <summary>
    /// Throws when one expected-valid semantic plan contains diagnostics.
    /// </summary>
    /// <param name="plan">Plan to inspect.</param>
    /// <param name="scenario">Readable smoke scenario.</param>
    private static void AssertPlanValid(ExcelDataPlayerScalingImportPlan plan, string scenario)
    {
        if (plan.IsValid)
            return;

        throw new InvalidOperationException("Expected valid " + scenario + " but received: " +
                                            plan.Diagnostics[0].Message);
    }

    /// <summary>
    /// Throws when one expected-invalid semantic plan is accepted or omits an expected diagnostic fragment.
    /// </summary>
    /// <param name="plan">Plan to inspect.</param>
    /// <param name="expectedMessageFragment">Optional diagnostic text fragment.</param>
    /// <param name="scenario">Readable smoke scenario.</param>
    private static void AssertPlanInvalid(ExcelDataPlayerScalingImportPlan plan,
                                          string expectedMessageFragment,
                                          string scenario)
    {
        if (plan.IsValid || plan.Diagnostics.Count <= 0)
            throw new InvalidOperationException("Expected invalid " + scenario + " but semantic preflight accepted it.");

        if (string.IsNullOrWhiteSpace(expectedMessageFragment))
            return;

        for (int diagnosticIndex = 0; diagnosticIndex < plan.Diagnostics.Count; diagnosticIndex++)
        {
            if (plan.Diagnostics[diagnosticIndex].Message.Contains(expectedMessageFragment,
                                                                   StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new InvalidOperationException("Invalid " + scenario +
                                            " did not report expected diagnostic fragment '" +
                                            expectedMessageFragment + "'. First diagnostic: " +
                                            plan.Diagnostics[0].Message);
    }
    #endregion

    #endregion
}
