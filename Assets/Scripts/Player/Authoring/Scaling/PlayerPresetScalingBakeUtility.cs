#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stores one debug snapshot for a scaled stat, including input [this] value and final evaluated value.
/// </summary>
public readonly struct PlayerScalingDebugRuleSnapshot
{
    #region Fields
    public readonly string PresetTypeLabel;
    public readonly string TargetDisplayName;
    public readonly string StatKey;
    public readonly string Formula;
    public readonly float ThisValue;
    public readonly float FinalValue;
    public readonly Color DebugColor;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable debug snapshot for a successful scaling-rule evaluation.
    /// </summary>
    /// <param name="presetTypeLabelValue">Preset category label owning the scaling rule.</param>
    /// <param name="targetDisplayNameValue">User-facing target stat name shown in debug logs.</param>
    /// <param name="statKeyValue">Stable stat key targeted by the scaling rule.</param>
    /// <param name="formulaValue">Raw scaling formula text.</param>
    /// <param name="thisValueValue">Input [this] value before scaling is applied.</param>
    /// <param name="finalValueValue">Final evaluated value after scaling logic is applied.</param>
    /// <param name="debugColorValue">Editor-only color used by runtime console logs for this rule.</param>
    /// <returns>Initialized immutable debug snapshot.</returns>
    public PlayerScalingDebugRuleSnapshot(string presetTypeLabelValue,
                                          string targetDisplayNameValue,
                                          string statKeyValue,
                                          string formulaValue,
                                          float thisValueValue,
                                          float finalValueValue,
                                          Color debugColorValue)
    {
        PresetTypeLabel = presetTypeLabelValue;
        TargetDisplayName = targetDisplayNameValue;
        StatKey = statKeyValue;
        Formula = formulaValue;
        ThisValue = thisValueValue;
        FinalValue = finalValueValue;
        DebugColor = debugColorValue;
    }
    #endregion
}

/// <summary>
/// Lifetime scope containing scaled bake-time preset clones.
/// </summary>
public sealed class PlayerScaledPresetScope : IDisposable
{
    #region Fields
    private readonly List<UnityEngine.Object> instantiatedPresets;
    #endregion

    #region Properties
    public PlayerControllerPreset ControllerPreset { get; private set; }
    public PlayerProgressionPreset ProgressionPreset { get; private set; }
    public PlayerPowerUpsPreset PowerUpsPreset { get; private set; }
    public PlayerVisualPreset VisualPreset { get; private set; }
    public PlayerUiVisualPreset UiVisualPreset { get; private set; }
    public PlayerAnimationBindingsPreset AnimationBindingsPreset { get; private set; }
    public IReadOnlyList<PlayerScalingDebugRuleSnapshot> DebugRuleSnapshots { get; private set; }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates a scoped container for scaled preset references.
    /// </summary>
    /// <param name="controllerPreset">Resolved controller preset used by bake.</param>
    /// <param name="progressionPreset">Resolved progression preset used by bake.</param>
    /// <param name="powerUpsPreset">Resolved power-ups preset used by bake.</param>
    /// <param name="visualPreset">Resolved visual preset used by bake.</param>
    /// <param name="uiVisualPreset">Resolved UI visual preset used by bake.</param>
    /// <param name="animationBindingsPreset">Resolved animation preset used by bake.</param>
    /// <param name="instantiatedPresetsValue">Owned clone instances to destroy on dispose.</param>
    /// <param name="debugRuleSnapshotsValue">Collected debug snapshots for rules with Debug in Console enabled.</param>
    /// <returns>Initialized scope instance.</returns>
    public PlayerScaledPresetScope(PlayerControllerPreset controllerPreset,
                                   PlayerProgressionPreset progressionPreset,
                                   PlayerPowerUpsPreset powerUpsPreset,
                                   PlayerVisualPreset visualPreset,
                                   PlayerUiVisualPreset uiVisualPreset,
                                   PlayerAnimationBindingsPreset animationBindingsPreset,
                                   List<UnityEngine.Object> instantiatedPresetsValue,
                                   List<PlayerScalingDebugRuleSnapshot> debugRuleSnapshotsValue)
    {
        ControllerPreset = controllerPreset;
        ProgressionPreset = progressionPreset;
        PowerUpsPreset = powerUpsPreset;
        VisualPreset = visualPreset;
        UiVisualPreset = uiVisualPreset;
        AnimationBindingsPreset = animationBindingsPreset;
        instantiatedPresets = instantiatedPresetsValue;
        DebugRuleSnapshots = debugRuleSnapshotsValue;
    }
    #endregion

    #region Methods

    #region Lifetime
    /// <summary>
    /// Destroys all internally owned cloned presets.
    /// </summary>

    public void Dispose()
    {
        if (instantiatedPresets == null)
            return;

        for (int index = instantiatedPresets.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object clonedPreset = instantiatedPresets[index];

            if (clonedPreset == null)
                continue;

            UnityEngine.Object.DestroyImmediate(clonedPreset);
        }

        instantiatedPresets.Clear();
    }
    #endregion

    #endregion
}

/// <summary>
/// Applies formula scaling rules to cloned presets during baking without mutating source assets.
/// </summary>
public static class PlayerPresetScalingBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates scaled preset clones and applies Add Scaling rules using progression scalable stat defaults as variables.
    /// </summary>
    /// <param name="controllerPreset">Original controller preset reference.</param>
    /// <param name="progressionPreset">Original progression preset reference.</param>
    /// <param name="powerUpsPreset">Original power-ups preset reference.</param>
    /// <param name="visualPreset">Original visual preset reference.</param>
    /// <param name="uiVisualPreset">Original UI visual preset reference.</param>
    /// <param name="animationBindingsPreset">Original animation preset reference.</param>
    /// <returns>Disposable scope containing scaled or original preset references.</returns>
    public static PlayerScaledPresetScope CreateScope(PlayerControllerPreset controllerPreset,
                                                      PlayerProgressionPreset progressionPreset,
                                                      PlayerPowerUpsPreset powerUpsPreset,
                                                      PlayerVisualPreset visualPreset,
                                                      PlayerUiVisualPreset uiVisualPreset,
                                                      PlayerAnimationBindingsPreset animationBindingsPreset)
    {
        List<UnityEngine.Object> instantiatedPresets = new List<UnityEngine.Object>();
        List<PlayerScalingDebugRuleSnapshot> debugRuleSnapshots = new List<PlayerScalingDebugRuleSnapshot>();
        PlayerProgressionPreset resolvedProgressionPreset = progressionPreset;
        PlayerControllerPreset resolvedControllerPreset = controllerPreset;
        PlayerPowerUpsPreset resolvedPowerUpsPreset = powerUpsPreset;
        PlayerVisualPreset resolvedVisualPreset = visualPreset;
        PlayerUiVisualPreset resolvedUiVisualPreset = uiVisualPreset;
        PlayerAnimationBindingsPreset resolvedAnimationBindingsPreset = animationBindingsPreset;

        bool progressionHasScaling = HasEnabledScalingRules(progressionPreset != null ? progressionPreset.ScalingRules : null);
        bool controllerHasScaling = HasEnabledScalingRules(controllerPreset != null ? controllerPreset.ScalingRules : null);
        bool powerUpsHasScaling = HasEnabledScalingRules(powerUpsPreset != null ? powerUpsPreset.ScalingRules : null);
        bool visualHasScaling = HasEnabledScalingRules(visualPreset != null ? visualPreset.ScalingRules : null);
        bool uiVisualHasScaling = HasEnabledScalingRules(uiVisualPreset != null ? uiVisualPreset.ScalingRules : null);
        bool animationHasScaling = HasEnabledScalingRules(animationBindingsPreset != null ? animationBindingsPreset.ScalingRules : null);
        bool hasAnyScaling = progressionHasScaling ||
                             controllerHasScaling ||
                             powerUpsHasScaling ||
                             visualHasScaling ||
                             uiVisualHasScaling ||
                             animationHasScaling;

        if (!hasAnyScaling)
            return new PlayerScaledPresetScope(resolvedControllerPreset,
                                               resolvedProgressionPreset,
                                               resolvedPowerUpsPreset,
                                               resolvedVisualPreset,
                                               resolvedUiVisualPreset,
                                               resolvedAnimationBindingsPreset,
                                               instantiatedPresets,
                                               debugRuleSnapshots);

        if (progressionHasScaling)
            resolvedProgressionPreset = CreateClone(progressionPreset, instantiatedPresets);

        Dictionary<string, PlayerFormulaValue> variableContext = BuildScalableVariableContext(resolvedProgressionPreset);
        HashSet<string> allowedVariables = new HashSet<string>(variableContext.Keys, StringComparer.OrdinalIgnoreCase);

        if (progressionHasScaling && resolvedProgressionPreset != null)
        {
            ApplyScalingRules(resolvedProgressionPreset,
                              resolvedProgressionPreset.ScalingRules,
                              variableContext,
                              allowedVariables,
                              "PlayerProgressionPreset",
                              debugRuleSnapshots);

            variableContext = BuildScalableVariableContext(resolvedProgressionPreset);
            allowedVariables = new HashSet<string>(variableContext.Keys, StringComparer.OrdinalIgnoreCase);
        }

        if (controllerHasScaling)
        {
            resolvedControllerPreset = CreateClone(controllerPreset, instantiatedPresets);
            ApplyScalingRules(resolvedControllerPreset,
                              resolvedControllerPreset.ScalingRules,
                              variableContext,
                              allowedVariables,
                              "PlayerControllerPreset",
                              debugRuleSnapshots);
        }

        if (powerUpsHasScaling)
        {
            resolvedPowerUpsPreset = CreateClone(powerUpsPreset, instantiatedPresets);
            ApplyScalingRules(resolvedPowerUpsPreset,
                              resolvedPowerUpsPreset.ScalingRules,
                              variableContext,
                              allowedVariables,
                              "PlayerPowerUpsPreset",
                              debugRuleSnapshots);
        }

        if (visualHasScaling)
        {
            resolvedVisualPreset = CreateClone(visualPreset, instantiatedPresets);
            ApplyScalingRules(resolvedVisualPreset,
                              resolvedVisualPreset.ScalingRules,
                              variableContext,
                              allowedVariables,
                              "PlayerVisualPreset",
                              debugRuleSnapshots);
        }

        if (uiVisualHasScaling)
        {
            resolvedUiVisualPreset = CreateClone(uiVisualPreset, instantiatedPresets);
            ApplyScalingRules(resolvedUiVisualPreset,
                              resolvedUiVisualPreset.ScalingRules,
                              variableContext,
                              allowedVariables,
                              "PlayerUiVisualPreset",
                              debugRuleSnapshots);
        }

        if (animationHasScaling)
        {
            resolvedAnimationBindingsPreset = CreateClone(animationBindingsPreset, instantiatedPresets);
            ApplyScalingRules(resolvedAnimationBindingsPreset,
                              resolvedAnimationBindingsPreset.ScalingRules,
                              variableContext,
                              allowedVariables,
                              "PlayerAnimationBindingsPreset",
                              debugRuleSnapshots);
        }

        return new PlayerScaledPresetScope(resolvedControllerPreset,
                                           resolvedProgressionPreset,
                                           resolvedPowerUpsPreset,
                                           resolvedVisualPreset,
                                           resolvedUiVisualPreset,
                                           resolvedAnimationBindingsPreset,
                                           instantiatedPresets,
                                           debugRuleSnapshots);
    }

    #endregion

    #region Scaling
    private static void ApplyScalingRules(UnityEngine.Object presetObject,
                                          IReadOnlyList<PlayerStatScalingRule> scalingRules,
                                          IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                          ISet<string> allowedVariables,
                                          string presetTypeLabel,
                                          List<PlayerScalingDebugRuleSnapshot> debugRuleSnapshots)
    {
        if (presetObject == null)
            return;

        if (scalingRules == null || scalingRules.Count == 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(presetObject);
        serializedPreset.Update();
        bool hasAppliedAtLeastOneRule = false;

        for (int ruleIndex = 0; ruleIndex < scalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[ruleIndex];

            if (IsEnabledScalingRule(scalingRule) == false)
                continue;

            if (PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty targetProperty) == false)
            {
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, "Target stat key could not be resolved.");
                continue;
            }

            if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(targetProperty))
            {
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, "Target stat type is not supported by Add Scaling.");
                continue;
            }

            string normalizedFormula = PlayerScalingFormulaEditorUtility.NormalizeFormulaForTarget(scalingRule.Formula,
                                                                                                   targetProperty,
                                                                                                   allowedVariables);
            PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaEngine.Compile(normalizedFormula, true);

            if (compileResult.IsValid == false || compileResult.CompiledFormula == null)
            {
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, compileResult.ErrorMessage);
                continue;
            }

            if (HasUnknownVariables(compileResult.CompiledFormula.VariableNames, allowedVariables, out string unknownVariableName))
            {
                string warning = string.Format("Unknown variable [{0}] for available scalable stats context.", unknownVariableName);
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, warning);
                continue;
            }

            if (!PlayerScalingSerializedPropertyUtility.TryReadFormulaInput(targetProperty,
                                                                           out PlayerFormulaValue currentValue,
                                                                           out bool isIntegerLike))
            {
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, "Target stat type is not supported by Add Scaling.");
                continue;
            }

            if (!compileResult.CompiledFormula.TryEvaluate(currentValue,
                                                           variableContext,
                                                           out PlayerFormulaValue scaledValue,
                                                           out string evaluationError))
            {
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, evaluationError);
                continue;
            }

            if (scalingRule.DebugInConsole &&
                debugRuleSnapshots != null &&
                (targetProperty.propertyType == SerializedPropertyType.Integer || targetProperty.propertyType == SerializedPropertyType.Float))
            {
                string targetDisplayName = string.IsNullOrWhiteSpace(targetProperty.displayName)
                    ? scalingRule.StatKey
                    : targetProperty.displayName;
                float currentRawValue = currentValue.Type == PlayerFormulaValueType.Number
                    ? currentValue.NumberValue
                    : currentValue.BooleanValue ? 1f : 0f;
                float finalAppliedValue = scaledValue.Type == PlayerFormulaValueType.Number
                    ? isIntegerLike ? Mathf.RoundToInt(scaledValue.NumberValue) : scaledValue.NumberValue
                    : scaledValue.BooleanValue ? 1f : 0f;
                debugRuleSnapshots.Add(new PlayerScalingDebugRuleSnapshot(presetTypeLabel,
                                                                          targetDisplayName,
                                                                          scalingRule.StatKey,
                                                                          scalingRule.Formula,
                                                                          currentRawValue,
                                                                          finalAppliedValue,
                                                                          scalingRule.DebugColor));
            }

            if (!PlayerScalingSerializedPropertyUtility.TryApplyFormulaResult(targetProperty,
                                                                              scaledValue,
                                                                              out bool changed,
                                                                              out string applyError))
            {
                LogScalingWarning(presetObject, presetTypeLabel, scalingRule, applyError);
                continue;
            }

            if (changed)
                hasAppliedAtLeastOneRule = true;
        }

        if (hasAppliedAtLeastOneRule)
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool HasUnknownVariables(IReadOnlyList<string> variableNames,
                                            ISet<string> allowedVariables,
                                            out string unknownVariableName)
    {
        unknownVariableName = string.Empty;

        if (variableNames == null || variableNames.Count == 0)
            return false;

        for (int index = 0; index < variableNames.Count; index++)
        {
            string variableName = variableNames[index];

            if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (allowedVariables != null && allowedVariables.Contains(variableName))
                continue;

            unknownVariableName = variableName;
            return true;
        }

        return false;
    }

    private static void LogScalingWarning(UnityEngine.Object presetObject,
                                          string presetTypeLabel,
                                          PlayerStatScalingRule scalingRule,
                                          string reason)
    {
        string presetName = presetObject != null ? presetObject.name : "<null>";
        string statKey = scalingRule != null ? scalingRule.StatKey : string.Empty;
        string formula = scalingRule != null ? scalingRule.Formula : string.Empty;
        Debug.LogWarning(string.Format("[PlayerScalingBake] {0} '{1}' - scaling rule failed. StatKey: '{2}', Formula: '{3}', Reason: {4}. Fallback: raw value preserved.",
                                       presetTypeLabel,
                                       presetName,
                                       statKey,
                                       formula,
                                       reason),
                         presetObject);
    }
    #endregion

    #region Helpers
    private static Dictionary<string, PlayerFormulaValue> BuildScalableVariableContext(PlayerProgressionPreset progressionPreset)
    {
        Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);

        if (progressionPreset == null)
            return variableContext;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = progressionPreset.ScalableStats;

        if (scalableStats == null)
            return variableContext;

        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[index];

            if (scalableStat == null)
                continue;

            if (string.IsNullOrWhiteSpace(scalableStat.StatName))
                continue;

            PlayerFormulaValue value = scalableStat.ResolveRuntimeDefaultFormulaValue();
            variableContext[scalableStat.StatName] = value;
        }

        return variableContext;
    }

    private static bool HasEnabledScalingRules(IReadOnlyList<PlayerStatScalingRule> scalingRules)
    {
        if (scalingRules == null)
            return false;

        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (IsEnabledScalingRule(scalingRule) == false)
                continue;

            return true;
        }

        return false;
    }

    private static bool IsEnabledScalingRule(PlayerStatScalingRule scalingRule)
    {
        if (scalingRule == null)
            return false;

        if (scalingRule.AddScaling == false)
            return false;

        if (string.IsNullOrWhiteSpace(scalingRule.StatKey))
            return false;

        if (string.IsNullOrWhiteSpace(scalingRule.Formula))
            return false;

        return true;
    }

    private static T CreateClone<T>(T sourcePreset, List<UnityEngine.Object> instantiatedPresets) where T : UnityEngine.Object
    {
        if (sourcePreset == null)
            return null;

        T clonedPreset = UnityEngine.Object.Instantiate(sourcePreset);
        clonedPreset.hideFlags = HideFlags.HideAndDontSave;

        if (instantiatedPresets != null)
            instantiatedPresets.Add(clonedPreset);

        return clonedPreset;
    }
    #endregion

    #endregion
}
#endif
