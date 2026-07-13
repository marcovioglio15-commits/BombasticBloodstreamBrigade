using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Builds a formula-aware Player scaling import plan against isolated pending SerializedObject state.
/// </summary>
internal static class ExcelDataPlayerScalingImportPlanBuilder
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves Player cells, simulates list merges, stages the combined post-state and validates every affected rule.
    /// </summary>
    /// <param name="cells">Approved workbook cells after local value and duplicate preflight.</param>
    /// <param name="importPreset">Import preset containing Player scaling list policy.</param>
    /// <returns>Direct write routes, planned appends and blocking semantic diagnostics.</returns>
    public static ExcelDataPlayerScalingImportPlan Build(IReadOnlyList<ExcelDataPlayerScalingImportCell> cells,
                                                          ExcelDataImportPreset importPreset)
    {
        ExcelDataPlayerScalingImportPlan plan = new ExcelDataPlayerScalingImportPlan();

        if (cells == null || cells.Count <= 0 || importPreset == null)
            return plan;

        Dictionary<Object, SerializedObject> serializedObjects = new Dictionary<Object, SerializedObject>();

        try
        {
            List<ExcelDataPlayerScalingResolvedCell> resolvedCells = ResolvePlayerCells(cells,
                                                                  serializedObjects,
                                                                  plan);
            StageNonScalingPlayerCells(resolvedCells, importPreset, plan);
            List<ExcelDataPlayerScalingRuleGroup> groups = BuildScalingRuleGroups(resolvedCells, plan);
            ValidateExistingRuleIdentities(groups, plan);
            ExcelDataPlayerScalingRuleGroupValidationUtility.ValidateUniqueDesiredStatKeys(groups, plan);
            List<ExcelDataPlayerScalingAffectedRule> affectedRules =
                PlanScalingRuleGroups(groups,
                                      serializedObjects,
                                      importPreset,
                                      plan);
            AddProgressionRulesAffectedByVariableChanges(resolvedCells,
                                                         serializedObjects,
                                                         affectedRules);
            ExcelDataPlayerScalingSemanticValidationUtility.Validate(serializedObjects,
                                                                      affectedRules,
                                                                      plan);
        }
        finally
        {
            DiscardPendingState(serializedObjects);
        }

        return plan;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Resolves every Player cell against one shared pending wrapper per owner before any value is staged.
    /// </summary>
    /// <param name="cells">Coordinate-aware workbook cells.</param>
    /// <param name="serializedObjects">Shared pending wrappers keyed by owner asset.</param>
    /// <param name="plan">Plan receiving resolution diagnostics.</param>
    /// <returns>Resolved Player cells in authored workbook order.</returns>
    private static List<ExcelDataPlayerScalingResolvedCell> ResolvePlayerCells(
        IReadOnlyList<ExcelDataPlayerScalingImportCell> cells,
        Dictionary<Object, SerializedObject> serializedObjects,
        ExcelDataPlayerScalingImportPlan plan)
    {
        List<ExcelDataPlayerScalingResolvedCell> resolvedCells = new List<ExcelDataPlayerScalingResolvedCell>();

        // Resolve all paths against the unmodified state so incoming identifiers cannot redirect later cells.
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            ExcelDataPlayerScalingImportCell cell = cells[cellIndex];
            ExcelDataWorkbookCellDefinition cellDefinition = cell == null ? null : cell.CellDefinition;
            ExcelDataFieldBinding binding = cellDefinition == null ? null : cellDefinition.FieldBinding;

            if (binding == null || binding.Domain != ExcelDataTransferDomain.Player ||
                cellDefinition.ContentKind != ExcelDataWorkbookCellContentKind.DataField)
                continue;

            Object asset;
            SerializedObject temporarySerializedObject;
            SerializedProperty temporaryProperty;
            string warning;

            if (!ExcelDataFieldBindingAssetUtility.TryResolveTarget(binding,
                                                                    out asset,
                                                                    out temporarySerializedObject,
                                                                    out temporaryProperty,
                                                                    out warning))
            {
                plan.AddDiagnostic(cell, warning);
                continue;
            }

            SerializedObject serializedObject = GetOrCreateSerializedObject(asset, serializedObjects);
            SerializedProperty property;
            string resolvedPath;

            if (!ExcelDataStableFieldBindingResolver.TryResolveProperty(binding,
                                                                        serializedObject,
                                                                        out property,
                                                                        out resolvedPath,
                                                                        out warning))
            {
                plan.AddDiagnostic(cell, warning);
                continue;
            }

            ExcelDataPlayerScalingRuleLocation location = default;
            bool isScalingRule = IsSupportedPlayerScalingOwner(asset) &&
                                 ExcelDataPlayerScalingRuleSerializedUtility.TryResolveLocation(binding,
                                                                                               resolvedPath,
                                                                                               out location);
            plan.RegisterAffectedAsset(asset);
            resolvedCells.Add(new ExcelDataPlayerScalingResolvedCell(cell,
                                                                      asset,
                                                                      serializedObject,
                                                                      resolvedPath,
                                                                      isScalingRule,
                                                                      location));
        }

        return resolvedCells;
    }

    /// <summary>
    /// Returns one shared pending wrapper for a Player preset asset.
    /// </summary>
    /// <param name="asset">Resolved Player authoring asset.</param>
    /// <param name="serializedObjects">Pending wrappers keyed by owner.</param>
    /// <returns>Existing or newly created SerializedObject.</returns>
    private static SerializedObject GetOrCreateSerializedObject(Object asset,
                                                                Dictionary<Object, SerializedObject> serializedObjects)
    {
        SerializedObject serializedObject;

        if (serializedObjects.TryGetValue(asset, out serializedObject))
            return serializedObject;

        serializedObject = new SerializedObject(asset);
        serializedObjects.Add(asset, serializedObject);
        return serializedObject;
    }

    /// <summary>
    /// Checks whether one asset is a Player preset type that participates in Add Scaling bake scope.
    /// </summary>
    /// <param name="asset">Resolved owner asset.</param>
    /// <returns>True for the six Player sub-preset families consumed by PlayerPresetScalingBakeUtility.</returns>
    private static bool IsSupportedPlayerScalingOwner(Object asset)
    {
        return asset is PlayerControllerPreset ||
               asset is PlayerProgressionPreset ||
               asset is PlayerPowerUpsPreset ||
               asset is PlayerVisualPreset ||
               asset is PlayerUiVisualPreset ||
               asset is PlayerAnimationBindingsPreset;
    }
    #endregion

    #region Pending State
    /// <summary>
    /// Stages non-scaling Player cells first so formula variables and target metadata reflect the combined workbook state.
    /// </summary>
    /// <param name="resolvedCells">Resolved Player cells.</param>
    /// <param name="importPreset">Import policy used by the shared property writer.</param>
    /// <param name="plan">Plan receiving parsing diagnostics.</param>
    private static void StageNonScalingPlayerCells(IReadOnlyList<ExcelDataPlayerScalingResolvedCell> resolvedCells,
                                                   ExcelDataImportPreset importPreset,
                                                   ExcelDataPlayerScalingImportPlan plan)
    {
        for (int cellIndex = 0; cellIndex < resolvedCells.Count; cellIndex++)
        {
            ExcelDataPlayerScalingResolvedCell resolvedCell = resolvedCells[cellIndex];

            if (resolvedCell.IsScalingRule)
                continue;

            SerializedProperty property = resolvedCell.SerializedObject.FindProperty(resolvedCell.ResolvedPath);
            string warning;

            if (!ExcelDataImportPropertyWriterUtility.TryWriteProperty(property,
                                                                       resolvedCell.Cell.IncomingValue,
                                                                       importPreset,
                                                                       out warning))
                plan.AddDiagnostic(resolvedCell.Cell, warning);
        }
    }

    /// <summary>
    /// Restores every pending wrapper from its source asset after preflight completes or fails.
    /// </summary>
    /// <param name="serializedObjects">Pending wrappers to discard.</param>
    private static void DiscardPendingState(Dictionary<Object, SerializedObject> serializedObjects)
    {
        foreach (KeyValuePair<Object, SerializedObject> serializedPair in serializedObjects)
            serializedPair.Value.Update();
    }
    #endregion

    #region Group Building
    /// <summary>
    /// Groups scaling cells by original owner and rule element before list-policy redirection.
    /// </summary>
    /// <param name="resolvedCells">Resolved Player cells.</param>
    /// <param name="plan">Plan receiving duplicate-member diagnostics.</param>
    /// <returns>Scaling-rule groups in first-cell workbook order.</returns>
    private static List<ExcelDataPlayerScalingRuleGroup> BuildScalingRuleGroups(
        IReadOnlyList<ExcelDataPlayerScalingResolvedCell> resolvedCells,
        ExcelDataPlayerScalingImportPlan plan)
    {
        List<ExcelDataPlayerScalingRuleGroup> groups = new List<ExcelDataPlayerScalingRuleGroup>();
        Dictionary<string, ExcelDataPlayerScalingRuleGroup> groupsByKey =
            new Dictionary<string, ExcelDataPlayerScalingRuleGroup>(StringComparer.Ordinal);

        for (int cellIndex = 0; cellIndex < resolvedCells.Count; cellIndex++)
        {
            ExcelDataPlayerScalingResolvedCell resolvedCell = resolvedCells[cellIndex];

            if (!resolvedCell.IsScalingRule)
                continue;

            string groupKey = resolvedCell.Asset.GetInstanceID() + ":" +
                              resolvedCell.ScalingLocation.RulePropertyPath;
            ExcelDataPlayerScalingRuleGroup group;

            if (!groupsByKey.TryGetValue(groupKey, out group))
            {
                group = new ExcelDataPlayerScalingRuleGroup(resolvedCell.Asset,
                                                             resolvedCell.SerializedObject,
                                                             resolvedCell.ScalingLocation.RulesPropertyPath,
                                                             resolvedCell.ScalingLocation.RulePropertyPath);
                groupsByKey.Add(groupKey, group);
                groups.Add(group);
            }

            if (!group.TryAddCell(resolvedCell))
                plan.AddDiagnostic(resolvedCell.Cell,
                                   "The same scaling-rule member is mapped more than once within one source rule group.");
        }

        return groups;
    }

    /// <summary>
    /// Blocks owners whose current scalingRules list already contains duplicate non-empty stat keys.
    /// </summary>
    /// <param name="groups">Scaling-rule groups to inspect by owner list.</param>
    /// <param name="plan">Plan receiving duplicate-key diagnostics.</param>
    private static void ValidateExistingRuleIdentities(IReadOnlyList<ExcelDataPlayerScalingRuleGroup> groups,
                                                       ExcelDataPlayerScalingImportPlan plan)
    {
        HashSet<string> inspectedLists = new HashSet<string>(StringComparer.Ordinal);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ExcelDataPlayerScalingRuleGroup group = groups[groupIndex];
            string listKey = group.Asset.GetInstanceID() + ":" + group.RulesPropertyPath;

            if (!inspectedLists.Add(listKey))
                continue;

            SerializedProperty rulesProperty = group.SerializedObject.FindProperty(group.RulesPropertyPath);
            Dictionary<string, int> countsByStatKey = BuildStatKeyCounts(rulesProperty);

            foreach (KeyValuePair<string, int> statKeyCount in countsByStatKey)
            {
                if (statKeyCount.Value <= 1)
                    continue;

                AddDiagnosticToMatchingGroups(groups,
                                              group.Asset,
                                              group.RulesPropertyPath,
                                              "Duplicate Player scaling stat key '" + statKeyCount.Key +
                                              "' exists in Unity authoring. Resolve the ambiguity before importing.",
                                              plan);
            }
        }
    }

    /// <summary>
    /// Counts non-empty stat keys in one scalingRules list without modifying authored state.
    /// </summary>
    /// <param name="rulesProperty">Serialized scalingRules list.</param>
    /// <returns>Exact stat-key occurrence counts.</returns>
    private static Dictionary<string, int> BuildStatKeyCounts(SerializedProperty rulesProperty)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (rulesProperty == null || !rulesProperty.isArray)
            return counts;

        for (int ruleIndex = 0; ruleIndex < rulesProperty.arraySize; ruleIndex++)
        {
            SerializedProperty ruleProperty = rulesProperty.GetArrayElementAtIndex(ruleIndex);
            SerializedProperty statKeyProperty = ruleProperty == null
                ? null
                : ruleProperty.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName);
            string statKey = statKeyProperty == null ? string.Empty : statKeyProperty.stringValue;

            if (string.IsNullOrWhiteSpace(statKey))
                continue;

            counts[statKey] = counts.TryGetValue(statKey, out int currentCount) ? currentCount + 1 : 1;
        }

        return counts;
    }
    #endregion

    #region Merge Planning
    /// <summary>
    /// Resolves existing-rule updates or controlled appends, stages values and records final direct routes.
    /// </summary>
    /// <param name="groups">Scaling cells grouped by original rule.</param>
    /// <param name="serializedObjects">Shared pending wrappers used by semantic validation.</param>
    /// <param name="importPreset">Import preset containing merge policy and value parsing settings.</param>
    /// <param name="plan">Plan receiving routes, creations and diagnostics.</param>
    /// <returns>Affected post-import rules requiring formula validation.</returns>
    private static List<ExcelDataPlayerScalingAffectedRule> PlanScalingRuleGroups(
        IReadOnlyList<ExcelDataPlayerScalingRuleGroup> groups,
        Dictionary<Object, SerializedObject> serializedObjects,
        ExcelDataImportPreset importPreset,
        ExcelDataPlayerScalingImportPlan plan)
    {
        List<ExcelDataPlayerScalingAffectedRule> affectedRules = new List<ExcelDataPlayerScalingAffectedRule>();
        Dictionary<string, ExcelDataPlayerScalingResolvedCell> routedCellsByTarget =
            new Dictionary<string, ExcelDataPlayerScalingResolvedCell>(StringComparer.Ordinal);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ExcelDataPlayerScalingRuleGroup group = groups[groupIndex];
            string targetRulePath;

            if (!TryResolveTargetRulePath(group, importPreset, plan, out targetRulePath))
                continue;

            bool groupStaged = StageScalingGroup(group,
                                                 targetRulePath,
                                                 routedCellsByTarget,
                                                 importPreset,
                                                 plan);

            if (!groupStaged)
                continue;

            affectedRules.Add(new ExcelDataPlayerScalingAffectedRule(group.Asset,
                                                                      group.RulesPropertyPath,
                                                                      targetRulePath,
                                                                      group.Cells));
        }

        return affectedRules;
    }

    /// <summary>
    /// Resolves one group to its source, matching existing stat key or newly appended rule.
    /// </summary>
    /// <param name="group">Source scaling-rule group.</param>
    /// <param name="importPreset">Import preset containing list policy.</param>
    /// <param name="plan">Plan receiving creation or policy diagnostics.</param>
    /// <param name="targetRulePath">Concrete final rule path when successful.</param>
    /// <returns>True when the group has a deterministic existing or appended target.</returns>
    private static bool TryResolveTargetRulePath(ExcelDataPlayerScalingRuleGroup group,
                                                 ExcelDataImportPreset importPreset,
                                                 ExcelDataPlayerScalingImportPlan plan,
                                                 out string targetRulePath)
    {
        targetRulePath = string.Empty;
        SerializedProperty sourceRule = group.SerializedObject.FindProperty(group.SourceRulePropertyPath);
        SerializedProperty currentStatKeyProperty = sourceRule == null
            ? null
            : sourceRule.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName);

        if (currentStatKeyProperty == null)
        {
            AddDiagnosticToGroup(group, "Source Player scaling rule no longer exposes statKey.", plan);
            return false;
        }

        string currentStatKey = currentStatKeyProperty.stringValue;
        string desiredStatKey = group.TryGetCell(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName,
                                                 out ExcelDataPlayerScalingResolvedCell statKeyCell)
            ? statKeyCell.Cell.IncomingValue.ValueText
            : currentStatKey;

        if (string.IsNullOrWhiteSpace(desiredStatKey))
        {
            AddDiagnosticToGroup(group, "Player scaling statKey cannot be empty.", plan);
            return false;
        }

        if (importPreset.ScalingRuleImportPolicy == ExcelDataScalingRuleImportPolicy.ExistingRulesOnly)
        {
            if (!string.Equals(desiredStatKey, currentStatKey, StringComparison.Ordinal))
            {
                AddDiagnosticToGroup(group,
                                     "Existing Rules Only cannot retarget statKey from '" + currentStatKey +
                                     "' to '" + desiredStatKey + "'. Use Merge Rules By Stat Key for a controlled append or redirect.",
                                     plan);
                return false;
            }

            targetRulePath = group.SourceRulePropertyPath;
            return true;
        }

        SerializedProperty rulesProperty = group.SerializedObject.FindProperty(group.RulesPropertyPath);
        List<int> matchingIndices =
            ExcelDataPlayerScalingRuleSerializedUtility.FindRuleIndicesByStatKey(rulesProperty,
                                                                                 desiredStatKey);

        if (matchingIndices.Count > 1)
        {
            AddDiagnosticToGroup(group,
                                 "Player scaling statKey '" + desiredStatKey + "' is ambiguous because it matches multiple rules.",
                                 plan);
            return false;
        }

        if (matchingIndices.Count == 1)
        {
            targetRulePath = ExcelDataPlayerScalingRuleSerializedUtility.BuildRulePath(group.RulesPropertyPath,
                                                                                        matchingIndices[0]);
            return true;
        }

        if (!HasCompleteCreationSet(group))
        {
            AddDiagnosticToGroup(group,
                                 "Merge Rules By Stat Key can create '" + desiredStatKey +
                                 "' only when statKey, addScaling and formula are all mapped in the same source rule group.",
                                 plan);
            return false;
        }

        int targetIndex = rulesProperty == null ? -1 : rulesProperty.arraySize;
        ExcelDataPlayerScalingRuleCreation creation =
            new ExcelDataPlayerScalingRuleCreation(group.Asset,
                                                   group.RulesPropertyPath,
                                                   targetIndex);
        string warning = "Scaling-rule list could not resolve a valid append index.";

        if (targetIndex < 0 ||
            !ExcelDataPlayerScalingRuleSerializedUtility.TryAppendInitializedRule(group.SerializedObject,
                                                                                  creation,
                                                                                  out targetRulePath,
                                                                                  out warning))
        {
            AddDiagnosticToGroup(group, warning, plan);
            return false;
        }

        plan.AddCreation(creation);
        return true;
    }

    /// <summary>
    /// Checks whether one source group contains every mandatory field for controlled creation.
    /// </summary>
    /// <param name="group">Scaling-rule group to inspect.</param>
    /// <returns>True when statKey, addScaling and formula cells are all present.</returns>
    private static bool HasCompleteCreationSet(ExcelDataPlayerScalingRuleGroup group)
    {
        return group.ContainsMember(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName) &&
               group.ContainsMember(ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName) &&
               group.ContainsMember(ExcelDataPlayerScalingRuleSerializedUtility.FormulaMemberName);
    }

    /// <summary>
    /// Stages every group member into its final route while detecting merge-target conflicts.
    /// </summary>
    /// <param name="group">Source scaling-rule group.</param>
    /// <param name="targetRulePath">Final existing or appended rule path.</param>
    /// <param name="routedCellsByTarget">Previously routed members used to detect collisions.</param>
    /// <param name="importPreset">Import value parsing policy.</param>
    /// <param name="plan">Plan receiving routes and diagnostics.</param>
    /// <returns>True when every group member was staged successfully.</returns>
    private static bool StageScalingGroup(ExcelDataPlayerScalingRuleGroup group,
                                          string targetRulePath,
                                          Dictionary<string, ExcelDataPlayerScalingResolvedCell> routedCellsByTarget,
                                          ExcelDataImportPreset importPreset,
                                          ExcelDataPlayerScalingImportPlan plan)
    {
        bool succeeded = true;

        for (int cellIndex = 0; cellIndex < group.ResolvedCells.Count; cellIndex++)
        {
            ExcelDataPlayerScalingResolvedCell resolvedCell = group.ResolvedCells[cellIndex];
            string targetPath = ExcelDataPlayerScalingRuleSerializedUtility.BuildMemberPath(targetRulePath,
                                                                                             resolvedCell.ScalingLocation.MemberName);
            string routeKey = group.Asset.GetInstanceID() + ":" + targetPath;

            if (routedCellsByTarget.TryGetValue(routeKey, out ExcelDataPlayerScalingResolvedCell existingCell) &&
                !string.Equals(existingCell.Cell.IncomingValue.ComparisonToken,
                               resolvedCell.Cell.IncomingValue.ComparisonToken,
                               StringComparison.Ordinal))
            {
                plan.AddDiagnostic(existingCell.Cell,
                                   "Multiple scaling groups write conflicting values to the same merged rule member.");
                plan.AddDiagnostic(resolvedCell.Cell,
                                   "Multiple scaling groups write conflicting values to the same merged rule member.");
                succeeded = false;
                continue;
            }

            routedCellsByTarget[routeKey] = resolvedCell;
            SerializedProperty targetProperty = group.SerializedObject.FindProperty(targetPath);
            string warning;

            if (!ExcelDataImportPropertyWriterUtility.TryWriteProperty(targetProperty,
                                                                       resolvedCell.Cell.IncomingValue,
                                                                       importPreset,
                                                                       out warning))
            {
                plan.AddDiagnostic(resolvedCell.Cell, warning);
                succeeded = false;
                continue;
            }

            plan.AddRoute(resolvedCell.Cell.CellDefinition, group.Asset, targetPath);
        }

        return succeeded;
    }
    #endregion

    #region Progression Dependencies
    /// <summary>
    /// Adds existing progression rules when imported scalable-stat definitions can invalidate their formulas or graph.
    /// </summary>
    /// <param name="resolvedCells">All staged Player cells.</param>
    /// <param name="serializedObjects">Pending wrappers containing combined workbook state.</param>
    /// <param name="affectedRules">Affected-rule collection extended for progression variable edits.</param>
    private static void AddProgressionRulesAffectedByVariableChanges(
        IReadOnlyList<ExcelDataPlayerScalingResolvedCell> resolvedCells,
        Dictionary<Object, SerializedObject> serializedObjects,
        List<ExcelDataPlayerScalingAffectedRule> affectedRules)
    {
        Dictionary<PlayerProgressionPreset, List<ExcelDataPlayerScalingImportCell>> cellsByProgression =
            new Dictionary<PlayerProgressionPreset, List<ExcelDataPlayerScalingImportCell>>();

        for (int cellIndex = 0; cellIndex < resolvedCells.Count; cellIndex++)
        {
            ExcelDataPlayerScalingResolvedCell resolvedCell = resolvedCells[cellIndex];
            PlayerProgressionPreset progressionPreset = resolvedCell.Asset as PlayerProgressionPreset;

            if (progressionPreset == null ||
                !resolvedCell.ResolvedPath.StartsWith("scalableStats.", StringComparison.Ordinal))
                continue;

            if (!cellsByProgression.TryGetValue(progressionPreset,
                                                out List<ExcelDataPlayerScalingImportCell> progressionCells))
            {
                progressionCells = new List<ExcelDataPlayerScalingImportCell>();
                cellsByProgression.Add(progressionPreset, progressionCells);
            }

            progressionCells.Add(resolvedCell.Cell);
        }

        foreach (KeyValuePair<PlayerProgressionPreset, List<ExcelDataPlayerScalingImportCell>> progressionPair in cellsByProgression)
        {
            SerializedObject serializedObject = serializedObjects[progressionPair.Key];
            SerializedProperty rulesProperty = serializedObject.FindProperty("scalingRules");

            if (rulesProperty == null || !rulesProperty.isArray)
                continue;

            for (int ruleIndex = 0; ruleIndex < rulesProperty.arraySize; ruleIndex++)
            {
                string rulePath = ExcelDataPlayerScalingRuleSerializedUtility.BuildRulePath("scalingRules", ruleIndex);

                if (ContainsAffectedRule(affectedRules, progressionPair.Key, rulePath))
                    continue;

                affectedRules.Add(new ExcelDataPlayerScalingAffectedRule(progressionPair.Key,
                                                                          "scalingRules",
                                                                          rulePath,
                                                                          progressionPair.Value));
            }
        }
    }

    /// <summary>
    /// Checks whether one owner and concrete rule path is already scheduled for semantic validation.
    /// </summary>
    /// <param name="affectedRules">Current affected-rule collection.</param>
    /// <param name="asset">Owner Player preset.</param>
    /// <param name="rulePath">Concrete rule path.</param>
    /// <returns>True when the same post-import rule is already present.</returns>
    private static bool ContainsAffectedRule(IReadOnlyList<ExcelDataPlayerScalingAffectedRule> affectedRules,
                                             Object asset,
                                             string rulePath)
    {
        for (int ruleIndex = 0; ruleIndex < affectedRules.Count; ruleIndex++)
        {
            ExcelDataPlayerScalingAffectedRule affectedRule = affectedRules[ruleIndex];

            if (affectedRule.Asset == asset &&
                string.Equals(affectedRule.RulePropertyPath, rulePath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #region Diagnostics
    /// <summary>
    /// Adds one diagnostic to every workbook cell in a source scaling group.
    /// </summary>
    /// <param name="group">Source group receiving the diagnostic.</param>
    /// <param name="message">Blocking semantic message.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void AddDiagnosticToGroup(ExcelDataPlayerScalingRuleGroup group,
                                             string message,
                                             ExcelDataPlayerScalingImportPlan plan)
    {
        for (int cellIndex = 0; cellIndex < group.Cells.Count; cellIndex++)
            plan.AddDiagnostic(group.Cells[cellIndex], message);
    }

    /// <summary>
    /// Adds one list-level diagnostic to all groups sharing the same owner and scalingRules path.
    /// </summary>
    /// <param name="groups">All current source groups.</param>
    /// <param name="asset">Owner asset whose list is ambiguous.</param>
    /// <param name="rulesPropertyPath">Ambiguous scalingRules list path.</param>
    /// <param name="message">Blocking diagnostic.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void AddDiagnosticToMatchingGroups(IReadOnlyList<ExcelDataPlayerScalingRuleGroup> groups,
                                                      Object asset,
                                                      string rulesPropertyPath,
                                                      string message,
                                                      ExcelDataPlayerScalingImportPlan plan)
    {
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ExcelDataPlayerScalingRuleGroup group = groups[groupIndex];

            if (group.Asset == asset &&
                string.Equals(group.RulesPropertyPath, rulesPropertyPath, StringComparison.Ordinal))
                AddDiagnosticToGroup(group, message, plan);
        }
    }
    #endregion

    #endregion

}
