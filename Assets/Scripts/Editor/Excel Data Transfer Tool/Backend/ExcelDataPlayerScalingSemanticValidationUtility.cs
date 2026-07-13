using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
/// Validates Player scaling-rule post-state with the same formula contracts used by the Player Management Tool.
/// </summary>
internal static class ExcelDataPlayerScalingSemanticValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates stat-key uniqueness, target types, formulas, scoped variables and progression dependency cycles.
    /// </summary>
    /// <param name="serializedObjects">Pending owner wrappers containing the combined workbook post-state.</param>
    /// <param name="affectedRules">Rules affected directly or through scalable-stat definition edits.</param>
    /// <param name="plan">Plan receiving coordinate-specific blocking diagnostics.</param>
    public static void Validate(Dictionary<Object, SerializedObject> serializedObjects,
                                IReadOnlyList<ExcelDataPlayerScalingAffectedRule> affectedRules,
                                ExcelDataPlayerScalingImportPlan plan)
    {
        if (serializedObjects == null || affectedRules == null || affectedRules.Count <= 0 || plan == null)
            return;

        List<ValidationTarget> validationTargets = BuildValidationTargets(affectedRules);
        ValidatePostStateStatKeyUniqueness(serializedObjects, validationTargets, plan);
        Dictionary<Object, FormulaContext> contextsByOwner = new Dictionary<Object, FormulaContext>();

        for (int targetIndex = 0; targetIndex < validationTargets.Count; targetIndex++)
            ValidateRule(serializedObjects,
                         validationTargets[targetIndex],
                         contextsByOwner,
                         plan);

        ValidateProgressionDependencyGraphs(serializedObjects, validationTargets, plan);
    }
    #endregion

    #region Target Collection
    /// <summary>
    /// Combines repeated affected-rule records while preserving all workbook cells responsible for diagnostics.
    /// </summary>
    /// <param name="affectedRules">Raw affected rules from direct mappings and scalable-stat edits.</param>
    /// <returns>Unique validation targets in deterministic first-seen order.</returns>
    private static List<ValidationTarget> BuildValidationTargets(
        IReadOnlyList<ExcelDataPlayerScalingAffectedRule> affectedRules)
    {
        List<ValidationTarget> targets = new List<ValidationTarget>();
        Dictionary<string, ValidationTarget> targetsByKey = new Dictionary<string, ValidationTarget>(StringComparer.Ordinal);

        for (int ruleIndex = 0; ruleIndex < affectedRules.Count; ruleIndex++)
        {
            ExcelDataPlayerScalingAffectedRule affectedRule = affectedRules[ruleIndex];

            if (affectedRule == null || affectedRule.Asset == null)
                continue;

            string key = affectedRule.Asset.GetInstanceID() + ":" + affectedRule.RulePropertyPath;
            ValidationTarget target;

            if (!targetsByKey.TryGetValue(key, out target))
            {
                target = new ValidationTarget(affectedRule.Asset,
                                              affectedRule.RulesPropertyPath,
                                              affectedRule.RulePropertyPath);
                targetsByKey.Add(key, target);
                targets.Add(target);
            }

            target.AddDiagnosticCells(affectedRule.DiagnosticCells);
        }

        return targets;
    }
    #endregion

    #region Rule Validation
    /// <summary>
    /// Validates one affected rule using its final combined pending state.
    /// </summary>
    /// <param name="serializedObjects">Pending wrappers keyed by Player preset.</param>
    /// <param name="target">Unique affected rule.</param>
    /// <param name="contextsByOwner">Formula contexts cached for this preflight only.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void ValidateRule(Dictionary<Object, SerializedObject> serializedObjects,
                                     ValidationTarget target,
                                     Dictionary<Object, FormulaContext> contextsByOwner,
                                     ExcelDataPlayerScalingImportPlan plan)
    {
        if (!serializedObjects.TryGetValue(target.Asset, out SerializedObject serializedObject))
        {
            AddDiagnostic(target, "Pending Player preset state could not be resolved for formula validation.", plan);
            return;
        }

        SerializedProperty ruleProperty = serializedObject.FindProperty(target.RulePropertyPath);
        SerializedProperty statKeyProperty = ruleProperty == null
            ? null
            : ruleProperty.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName);
        SerializedProperty addScalingProperty = ruleProperty == null
            ? null
            : ruleProperty.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.AddScalingMemberName);
        SerializedProperty formulaProperty = ruleProperty == null
            ? null
            : ruleProperty.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.FormulaMemberName);

        if (statKeyProperty == null || addScalingProperty == null || formulaProperty == null)
        {
            AddDiagnostic(target, "PlayerStatScalingRule serialized members no longer match the formula contract.", plan);
            return;
        }

        string statKey = statKeyProperty.stringValue;

        if (string.IsNullOrWhiteSpace(statKey))
        {
            AddDiagnostic(target, "Player scaling statKey is required.", plan);
            return;
        }

        SerializedProperty targetProperty;

        if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedObject,
                                                                  statKey,
                                                                  out targetProperty) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(targetProperty))
        {
            AddDiagnostic(target,
                          "Player scaling statKey '" + statKey +
                          "' does not resolve to a supported numeric, Boolean, enum or token target on " +
                          target.Asset.name + ".",
                          plan);
            return;
        }

        if (!addScalingProperty.boolValue)
            return;

        string formula = formulaProperty.stringValue;

        if (string.IsNullOrWhiteSpace(formula))
        {
            AddDiagnostic(target, "Formula is required when Add Scaling is enabled.", plan);
            return;
        }

        FormulaContext context = ResolveFormulaContext(target.Asset,
                                                       serializedObjects,
                                                       contextsByOwner);

        if (!string.IsNullOrWhiteSpace(context.Warning))
        {
            AddDiagnostic(target, context.Warning, plan);
            return;
        }

        string normalizedFormula = PlayerScalingFormulaEditorUtility.NormalizeFormulaForTarget(formula,
                                                                                                targetProperty,
                                                                                                context.AllowedVariables);
        PlayerFormulaValueType requiredResultType =
            PlayerScalingFormulaEditorUtility.ResolveRequiredResultType(targetProperty);
        string warning;

        if (!PlayerScalingFormulaValidationUtility.TryValidateFormula(normalizedFormula,
                                                                      context.AllowedVariables,
                                                                      context.VariableTypes,
                                                                      requiredResultType,
                                                                      requiredResultType,
                                                                      out warning))
            AddDiagnostic(target, warning, plan);
    }
    #endregion

    #region Formula Context
    /// <summary>
    /// Resolves and caches the progression variable scope associated with one Player preset owner.
    /// </summary>
    /// <param name="owner">Player sub-preset that owns scaling rules.</param>
    /// <param name="serializedObjects">Pending wrappers containing imported progression edits.</param>
    /// <param name="contextsByOwner">Per-preflight context cache.</param>
    /// <returns>Resolved variable set, typed map and optional ambiguity warning.</returns>
    private static FormulaContext ResolveFormulaContext(Object owner,
                                                        Dictionary<Object, SerializedObject> serializedObjects,
                                                        Dictionary<Object, FormulaContext> contextsByOwner)
    {
        if (contextsByOwner.TryGetValue(owner, out FormulaContext cachedContext))
            return cachedContext;

        PlayerProgressionPreset progressionPreset = owner as PlayerProgressionPreset;
        string warning = string.Empty;

        if (progressionPreset == null)
            progressionPreset = ResolveLinkedProgressionPreset(owner, out warning);

        if (!string.IsNullOrWhiteSpace(warning))
        {
            FormulaContext ambiguousContext = new FormulaContext(new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                                                                 new Dictionary<string, PlayerFormulaValueType>(StringComparer.OrdinalIgnoreCase),
                                                                 warning);
            contextsByOwner.Add(owner, ambiguousContext);
            return ambiguousContext;
        }

        if (progressionPreset == null)
        {
            FormulaContext localOnlyContext = new FormulaContext(new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                                                                 new Dictionary<string, PlayerFormulaValueType>(StringComparer.OrdinalIgnoreCase),
                                                                 string.Empty);
            contextsByOwner.Add(owner, localOnlyContext);
            return localOnlyContext;
        }

        SerializedObject progressionSerializedObject;

        if (!serializedObjects.TryGetValue(progressionPreset, out progressionSerializedObject))
            progressionSerializedObject = new SerializedObject(progressionPreset);

        SerializedProperty scalableStatsProperty = progressionSerializedObject.FindProperty("scalableStats");
        FormulaContext context = new FormulaContext(
            PlayerScalingFormulaValidationUtility.BuildVariableSet(scalableStatsProperty),
            PlayerScalingFormulaValidationUtility.BuildVariableTypeMap(scalableStatsProperty),
            string.Empty);
        contextsByOwner.Add(owner, context);
        return context;
    }

    /// <summary>
    /// Finds the unique progression preset that supplies formula variables to one non-progression Player preset.
    /// </summary>
    /// <param name="owner">Player sub-preset referenced by one or more master presets.</param>
    /// <param name="warning">Blocking warning when linked masters expose divergent progression contexts.</param>
    /// <returns>Unique linked progression preset, or null for a [this]-only unlinked scope.</returns>
    private static PlayerProgressionPreset ResolveLinkedProgressionPreset(Object owner, out string warning)
    {
        warning = string.Empty;
        string[] searchFolders = new string[] { "Assets" };
        string[] masterGuids = AssetDatabase.FindAssets("t:PlayerMasterPreset", searchFolders);
        string ownerPath = AssetDatabase.GetAssetPath(owner);
        HashSet<PlayerProgressionPreset> progressionPresets = new HashSet<PlayerProgressionPreset>();
        bool foundReferencedMaster = false;
        bool foundMissingProgression = false;

        // Filter through dependency metadata before loading a master so unrelated OnValidate paths never execute.
        for (int guidIndex = 0; guidIndex < masterGuids.Length; guidIndex++)
        {
            string masterPath = AssetDatabase.GUIDToAssetPath(masterGuids[guidIndex]);

            if (!ContainsDependencyPath(masterPath, ownerPath))
                continue;

            PlayerMasterPreset masterPreset = AssetDatabase.LoadAssetAtPath<PlayerMasterPreset>(masterPath);

            if (!DoesMasterReferenceOwner(masterPreset, owner))
                continue;

            foundReferencedMaster = true;

            if (masterPreset.ProgressionPreset == null)
                foundMissingProgression = true;
            else
                progressionPresets.Add(masterPreset.ProgressionPreset);
        }

        if (!foundReferencedMaster)
            return null;

        if (progressionPresets.Count == 1 && !foundMissingProgression)
        {
            foreach (PlayerProgressionPreset progressionPreset in progressionPresets)
                return progressionPreset;
        }

        if (progressionPresets.Count <= 0 && foundMissingProgression)
            return null;

        warning = "Player scaling formula scope is ambiguous because " + owner.name +
                  " is referenced by Player master presets with different or missing progression presets.";
        return null;
    }

    /// <summary>
    /// Checks direct AssetDatabase dependencies without loading the candidate Player master asset.
    /// </summary>
    /// <param name="masterPath">Project-relative Player master asset path.</param>
    /// <param name="ownerPath">Project-relative scaling-rule owner path.</param>
    /// <returns>True when the master dependency metadata contains the owner asset.</returns>
    private static bool ContainsDependencyPath(string masterPath, string ownerPath)
    {
        if (string.IsNullOrWhiteSpace(masterPath) || string.IsNullOrWhiteSpace(ownerPath))
            return false;

        string[] dependencies = AssetDatabase.GetDependencies(masterPath, false);

        for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
        {
            if (string.Equals(dependencies[dependencyIndex], ownerPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks all six Player master sub-preset references without runtime reflection.
    /// </summary>
    /// <param name="masterPreset">Player master preset to inspect.</param>
    /// <param name="owner">Candidate scaling-rule owner.</param>
    /// <returns>True when the master directly references the owner.</returns>
    private static bool DoesMasterReferenceOwner(PlayerMasterPreset masterPreset, Object owner)
    {
        if (masterPreset == null || owner == null)
            return false;

        switch (owner)
        {
            case PlayerControllerPreset controllerPreset:
                return masterPreset.ControllerPreset == controllerPreset;
            case PlayerPowerUpsPreset powerUpsPreset:
                return masterPreset.PowerUpsPreset == powerUpsPreset;
            case PlayerVisualPreset visualPreset:
                return masterPreset.VisualPreset == visualPreset;
            case PlayerUiVisualPreset uiVisualPreset:
                return masterPreset.UiVisualPreset == uiVisualPreset;
            case PlayerAnimationBindingsPreset animationPreset:
                return masterPreset.AnimationBindingsPreset == animationPreset;
            default:
                return false;
        }
    }
    #endregion

    #region List Validation
    /// <summary>
    /// Rejects duplicate non-empty stat keys in every affected post-import scalingRules list.
    /// </summary>
    /// <param name="serializedObjects">Pending owner wrappers.</param>
    /// <param name="targets">Unique affected rules.</param>
    /// <param name="plan">Plan receiving list-level diagnostics.</param>
    private static void ValidatePostStateStatKeyUniqueness(
        Dictionary<Object, SerializedObject> serializedObjects,
        IReadOnlyList<ValidationTarget> targets,
        ExcelDataPlayerScalingImportPlan plan)
    {
        HashSet<string> inspectedLists = new HashSet<string>(StringComparer.Ordinal);

        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            ValidationTarget target = targets[targetIndex];
            string listKey = target.Asset.GetInstanceID() + ":" + target.RulesPropertyPath;

            if (!inspectedLists.Add(listKey) ||
                !serializedObjects.TryGetValue(target.Asset, out SerializedObject serializedObject))
                continue;

            SerializedProperty rulesProperty = serializedObject.FindProperty(target.RulesPropertyPath);
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

            if (rulesProperty == null || !rulesProperty.isArray)
                continue;

            for (int ruleIndex = 0; ruleIndex < rulesProperty.arraySize; ruleIndex++)
            {
                SerializedProperty ruleProperty = rulesProperty.GetArrayElementAtIndex(ruleIndex);
                SerializedProperty statKeyProperty = ruleProperty == null
                    ? null
                    : ruleProperty.FindPropertyRelative(ExcelDataPlayerScalingRuleSerializedUtility.StatKeyMemberName);
                string statKey = statKeyProperty == null ? string.Empty : statKeyProperty.stringValue;

                if (string.IsNullOrWhiteSpace(statKey))
                    continue;

                counts[statKey] = counts.TryGetValue(statKey, out int count) ? count + 1 : 1;
            }

            foreach (KeyValuePair<string, int> statKeyCount in counts)
            {
                if (statKeyCount.Value <= 1)
                    continue;

                AddDiagnosticToList(targets,
                                    target.Asset,
                                    target.RulesPropertyPath,
                                    "Post-import Player scaling statKey '" + statKeyCount.Key +
                                    "' is duplicated. Scaling rules must remain uniquely addressable.",
                                    plan);
            }
        }
    }
    #endregion

    #region Dependency Validation
    /// <summary>
    /// Runs the shared Player Management Tool dependency graph against affected progression post-state.
    /// </summary>
    /// <param name="serializedObjects">Pending owner wrappers.</param>
    /// <param name="targets">Unique affected rules.</param>
    /// <param name="plan">Plan receiving circular-dependency diagnostics.</param>
    private static void ValidateProgressionDependencyGraphs(
        Dictionary<Object, SerializedObject> serializedObjects,
        IReadOnlyList<ValidationTarget> targets,
        ExcelDataPlayerScalingImportPlan plan)
    {
        HashSet<PlayerProgressionPreset> inspectedPresets = new HashSet<PlayerProgressionPreset>();

        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            ValidationTarget target = targets[targetIndex];
            PlayerProgressionPreset progressionPreset = target.Asset as PlayerProgressionPreset;

            if (progressionPreset == null || !inspectedPresets.Add(progressionPreset) ||
                !serializedObjects.TryGetValue(progressionPreset, out SerializedObject serializedObject))
                continue;

            SerializedProperty scalableStatsProperty = serializedObject.FindProperty("scalableStats");
            SerializedProperty scalingRulesProperty = serializedObject.FindProperty(target.RulesPropertyPath);
            List<string> warnings =
                PlayerScalingDependencyValidationUtility.BuildScalableStatsDependencyWarnings(scalableStatsProperty,
                                                                                                scalingRulesProperty);

            for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
                AddDiagnosticToAsset(targets, progressionPreset, warnings[warningIndex], plan);
        }
    }
    #endregion

    #region Diagnostics
    /// <summary>
    /// Adds one rule-level diagnostic to the formula cell when mapped, otherwise to the first responsible cell.
    /// </summary>
    /// <param name="target">Affected rule.</param>
    /// <param name="message">Blocking formula diagnostic.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void AddDiagnostic(ValidationTarget target,
                                      string message,
                                      ExcelDataPlayerScalingImportPlan plan)
    {
        plan.AddDiagnostic(target.ResolvePreferredDiagnosticCell(), message);
    }

    /// <summary>
    /// Adds one diagnostic to every affected rule sharing an owner and scalingRules list.
    /// </summary>
    /// <param name="targets">Unique affected rules.</param>
    /// <param name="asset">Owner asset.</param>
    /// <param name="rulesPropertyPath">Affected scalingRules list path.</param>
    /// <param name="message">Blocking list diagnostic.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void AddDiagnosticToList(IReadOnlyList<ValidationTarget> targets,
                                            Object asset,
                                            string rulesPropertyPath,
                                            string message,
                                            ExcelDataPlayerScalingImportPlan plan)
    {
        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            ValidationTarget target = targets[targetIndex];

            if (target.Asset == asset &&
                string.Equals(target.RulesPropertyPath, rulesPropertyPath, StringComparison.Ordinal))
                AddDiagnostic(target, message, plan);
        }
    }

    /// <summary>
    /// Adds one dependency diagnostic to every affected rule owned by a progression preset.
    /// </summary>
    /// <param name="targets">Unique affected rules.</param>
    /// <param name="asset">Affected progression preset.</param>
    /// <param name="message">Dependency graph diagnostic.</param>
    /// <param name="plan">Plan receiving diagnostics.</param>
    private static void AddDiagnosticToAsset(IReadOnlyList<ValidationTarget> targets,
                                             Object asset,
                                             string message,
                                             ExcelDataPlayerScalingImportPlan plan)
    {
        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            if (targets[targetIndex].Asset == asset)
                AddDiagnostic(targets[targetIndex], message, plan);
        }
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores a scoped formula variable set and type map for one Player preset owner.
    /// </summary>
    private sealed class FormulaContext
    {
        #region Properties
        public HashSet<string> AllowedVariables { get; }
        public Dictionary<string, PlayerFormulaValueType> VariableTypes { get; }
        public string Warning { get; }
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one immutable per-preflight formula context.
        /// </summary>
        /// <param name="allowedVariables">Scoped scalable stat names.</param>
        /// <param name="variableTypes">Scoped scalable stat formula types.</param>
        /// <param name="warning">Blocking context ambiguity warning.</param>
        public FormulaContext(HashSet<string> allowedVariables,
                              Dictionary<string, PlayerFormulaValueType> variableTypes,
                              string warning)
        {
            AllowedVariables = allowedVariables;
            VariableTypes = variableTypes;
            Warning = warning ?? string.Empty;
        }
        #endregion

        #endregion
    }

    /// <summary>
    /// Stores one unique affected rule and all workbook cells responsible for validating it.
    /// </summary>
    private sealed class ValidationTarget
    {
        #region Fields
        private readonly List<ExcelDataPlayerScalingImportCell> diagnosticCells =
            new List<ExcelDataPlayerScalingImportCell>();
        private readonly HashSet<ExcelDataWorkbookCellDefinition> diagnosticCellSet =
            new HashSet<ExcelDataWorkbookCellDefinition>();
        #endregion

        #region Properties
        public Object Asset { get; }
        public string RulesPropertyPath { get; }
        public string RulePropertyPath { get; }
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one unique post-state validation target.
        /// </summary>
        /// <param name="asset">Owner Player preset.</param>
        /// <param name="rulesPropertyPath">Serialized scalingRules list path.</param>
        /// <param name="rulePropertyPath">Concrete rule element path.</param>
        public ValidationTarget(Object asset, string rulesPropertyPath, string rulePropertyPath)
        {
            Asset = asset;
            RulesPropertyPath = rulesPropertyPath ?? string.Empty;
            RulePropertyPath = rulePropertyPath ?? string.Empty;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Adds distinct responsible cells from another affected-rule record.
        /// </summary>
        /// <param name="cells">Coordinate-aware workbook cells.</param>
        public void AddDiagnosticCells(IReadOnlyList<ExcelDataPlayerScalingImportCell> cells)
        {
            if (cells == null)
                return;

            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                ExcelDataPlayerScalingImportCell cell = cells[cellIndex];

                if (cell != null && diagnosticCellSet.Add(cell.CellDefinition))
                    diagnosticCells.Add(cell);
            }
        }

        /// <summary>
        /// Selects the mapped formula cell for PMT-equivalent warnings, or the first responsible cell.
        /// </summary>
        /// <returns>Preferred coordinate-aware diagnostic cell, or null.</returns>
        public ExcelDataPlayerScalingImportCell ResolvePreferredDiagnosticCell()
        {
            for (int cellIndex = 0; cellIndex < diagnosticCells.Count; cellIndex++)
            {
                ExcelDataFieldBinding binding = diagnosticCells[cellIndex].CellDefinition.FieldBinding;

                if (binding != null &&
                    binding.PathTemplate.EndsWith("." + ExcelDataPlayerScalingRuleSerializedUtility.FormulaMemberName,
                                                  StringComparison.Ordinal))
                    return diagnosticCells[cellIndex];
            }

            return diagnosticCells.Count > 0 ? diagnosticCells[0] : null;
        }
        #endregion

        #endregion
    }
    #endregion
}
