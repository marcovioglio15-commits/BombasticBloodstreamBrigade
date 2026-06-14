#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Logs one ordered debug line per scaling rule, including formula, resolved math expression, and final value.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerScalingDebugConsoleSystem : ISystem
{
    #region Constants
    private const float ValueChangeEpsilon = 0.0001f;
    private const double MinimumLogIntervalSeconds = 0.25d;
    #endregion

    #region Fields
    private EntityQuery ruleDebugQuery;
    private NativeParallelHashMap<ulong, float> lastLoggedRuleValues;
    private NativeParallelHashMap<ulong, uint> lastEntityVariableHashes;
    private double nextAllowedLogTime;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the query used to detect entities with scaling debug snapshots and scalable stat values.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        ruleDebugQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerScalingDebugRuleElement, PlayerScalableStatElement>()
            .Build();
        state.RequireForUpdate(ruleDebugQuery);
        lastLoggedRuleValues = new NativeParallelHashMap<ulong, float>(256, Allocator.Persistent);
        lastEntityVariableHashes = new NativeParallelHashMap<ulong, uint>(32, Allocator.Persistent);
        nextAllowedLogTime = 0d;
    }

    /// <summary>
    /// Resets cached change-tracking maps when the system starts running.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnStartRunning(ref SystemState state)
    {
        if (lastLoggedRuleValues.IsCreated)
            lastLoggedRuleValues.Clear();

        if (lastEntityVariableHashes.IsCreated)
            lastEntityVariableHashes.Clear();

        nextAllowedLogTime = 0d;
    }

    /// <summary>
    /// Releases native allocations owned by this editor-only debug system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnDestroy(ref SystemState state)
    {
        if (lastLoggedRuleValues.IsCreated)
            lastLoggedRuleValues.Dispose();

        if (lastEntityVariableHashes.IsCreated)
            lastEntityVariableHashes.Dispose();
    }

    /// <summary>
    /// Emits colorized scaling-debug logs at a throttled cadence and only for rules whose evaluated value changed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        if (!Application.isPlaying)
            return;

        if (ruleDebugQuery.IsEmptyIgnoreFilter)
            return;

        double elapsedTime = SystemAPI.Time.ElapsedTime;

        if (elapsedTime < nextAllowedLogTime)
            return;

        nextAllowedLogTime = elapsedTime + MinimumLogIntervalSeconds;

        Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
        StringBuilder batchedLogBuilder = new StringBuilder(1024);
        int batchedRuleCount = 0;

        foreach ((DynamicBuffer<PlayerScalingDebugRuleElement> debugRules,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats,
                  Entity entity) in SystemAPI.Query<DynamicBuffer<PlayerScalingDebugRuleElement>,
                                                    DynamicBuffer<PlayerScalableStatElement>>()
                                              .WithEntityAccess())
        {
            if (debugRules.Length == 0)
                continue;

            ulong entityKey = ComposeEntityKey(entity);
            uint variableContextHash = PlayerScalableStatHashUtility.ComputeHash(scalableStats);

            if (!HasEntityVariableHashChanged(entityKey, variableContextHash))
                continue;

            PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

            for (int ruleIndex = 0; ruleIndex < debugRules.Length; ruleIndex++)
            {
                PlayerScalingDebugRuleElement debugRule = debugRules[ruleIndex];
                string targetDisplayName = ResolveTargetDisplayName(debugRule);
                string formulaText = debugRule.Formula.ToString();
                string translatedFormula = PlayerScalingRuntimeFormulaUtility.TranslateFormula(formulaText,
                                                                                               variableContext,
                                                                                               PlayerFormulaValue.CreateNumber(debugRule.ThisValue));
                float evaluatedValue = debugRule.FinalValue;
                Color debugColor = ResolveDebugColor(debugRule);
                string debugColorHex = ColorUtility.ToHtmlStringRGBA(debugColor);

                if (PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formulaText,
                                                                          debugRule.ThisValue,
                                                                          variableContext,
                                                                          out float runtimeEvaluatedValue,
                                                                          out string _))
                {
                    evaluatedValue = runtimeEvaluatedValue;
                }

                ulong ruleKey = ComposeRuleKey(entity, ruleIndex);

                if (!HasRuleValueChanged(ruleKey, evaluatedValue))
                    continue;

                AppendRuleLine(batchedLogBuilder,
                               targetDisplayName,
                               formulaText,
                               translatedFormula,
                               evaluatedValue,
                               debugColorHex);
                batchedRuleCount++;
            }
        }

        if (batchedRuleCount == 0)
            return;

        string batchedLog = string.Format(CultureInfo.InvariantCulture,
                                          "[PlayerScalingDebugConsoleSystem] Updated rules: {0}\n{1}",
                                          batchedRuleCount,
                                          batchedLogBuilder.ToString());
        Debug.Log(batchedLog);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates a stable 64-bit key for one entity using index/version tuple.
    /// </summary>
    /// <param name="entity">Entity used as key source.</param>
    /// <returns>Packed key used by native hash maps.</returns>
    private static ulong ComposeEntityKey(Entity entity)
    {
        ulong versionPart = (uint)entity.Version;
        ulong indexPart = (uint)entity.Index;
        return (versionPart << 32) | indexPart;
    }

    /// <summary>
    /// Creates a stable 64-bit key for one debug rule entry bound to an entity.
    /// </summary>
    /// <param name="entity">Entity owning the debug-rule buffer.</param>
    /// <param name="ruleIndex">Rule index inside the debug-rule buffer.</param>
    /// <returns>Combined key for rule-value change tracking.</returns>
    private static ulong ComposeRuleKey(Entity entity, int ruleIndex)
    {
        ulong entityKey = ComposeEntityKey(entity);
        ulong rulePart = (uint)ruleIndex;
        return (entityKey * 1099511628211UL) ^ rulePart;
    }

    /// Updates the entity-variable hash cache and reports whether the context changed since the previous logged sample.
    /// </summary>
    /// <param name="entityKey">Packed entity key.</param>
    /// <param name="currentHash">Current variable-context hash.</param>
    /// <returns>True when values changed and rule evaluation should run, otherwise false.</returns>
    private bool HasEntityVariableHashChanged(ulong entityKey, uint currentHash)
    {
        if (lastEntityVariableHashes.TryGetValue(entityKey, out uint previousHash))
        {
            if (previousHash == currentHash)
                return false;

            lastEntityVariableHashes.Remove(entityKey);
            lastEntityVariableHashes.TryAdd(entityKey, currentHash);
            return true;
        }

        EnsureEntityHashCapacity(lastEntityVariableHashes.Count() + 1);
        lastEntityVariableHashes.TryAdd(entityKey, currentHash);
        return true;
    }

    /// <summary>
    /// Updates the rule-value cache and reports whether a meaningful value delta occurred.
    /// </summary>
    /// <param name="ruleKey">Packed key for one rule entry.</param>
    /// <param name="currentValue">Current evaluated value.</param>
    /// <returns>True when value changed beyond epsilon and should be logged, otherwise false.</returns>
    private bool HasRuleValueChanged(ulong ruleKey, float currentValue)
    {
        if (lastLoggedRuleValues.TryGetValue(ruleKey, out float previousValue))
        {
            float delta = Mathf.Abs(currentValue - previousValue);

            if (delta <= ValueChangeEpsilon)
                return false;

            lastLoggedRuleValues.Remove(ruleKey);
            lastLoggedRuleValues.TryAdd(ruleKey, currentValue);
            return true;
        }

        EnsureRuleValueCapacity(lastLoggedRuleValues.Count() + 1);
        lastLoggedRuleValues.TryAdd(ruleKey, currentValue);
        return true;
    }

    /// <summary>
    /// Grows the rule-value cache capacity when the next insertion would overflow.
    /// </summary>
    /// <param name="requiredCapacity">Minimum capacity required by the next insertion.</param>

    private void EnsureRuleValueCapacity(int requiredCapacity)
    {
        if (lastLoggedRuleValues.Capacity >= requiredCapacity)
            return;

        int targetCapacity = requiredCapacity * 2;
        lastLoggedRuleValues.Capacity = targetCapacity;
    }

    /// <summary>
    /// Grows the entity-hash cache capacity when the next insertion would overflow.
    /// </summary>
    /// <param name="requiredCapacity">Minimum capacity required by the next insertion.</param>

    private void EnsureEntityHashCapacity(int requiredCapacity)
    {
        if (lastEntityVariableHashes.Capacity >= requiredCapacity)
            return;

        int targetCapacity = requiredCapacity * 2;
        lastEntityVariableHashes.Capacity = targetCapacity;
    }

    /// <summary>
    /// Appends one formatted colorized rule line to the shared batched output buffer.
    /// </summary>
    /// <param name="batchedLogBuilder">Mutable frame-level string builder.</param>
    /// <param name="targetDisplayName">Display label for the scaled stat.</param>
    /// <param name="formulaText">Original formula text.</param>
    /// <param name="translatedFormula">Formula with resolved variable values.</param>
    /// <param name="evaluatedValue">Final evaluated numeric result.</param>
    /// <param name="debugColorHex">Color used by this debug line as HTML hex RGBA.</param>

    private static void AppendRuleLine(StringBuilder batchedLogBuilder,
                                       string targetDisplayName,
                                       string formulaText,
                                       string translatedFormula,
                                       float evaluatedValue,
                                       string debugColorHex)
    {
        batchedLogBuilder.Append("<color=#");
        batchedLogBuilder.Append(debugColorHex);
        batchedLogBuilder.Append('>');
        batchedLogBuilder.Append(targetDisplayName);
        batchedLogBuilder.Append(" = ");
        batchedLogBuilder.Append(formulaText);
        batchedLogBuilder.Append(" = ");
        batchedLogBuilder.Append(translatedFormula);
        batchedLogBuilder.Append(" = ");
        batchedLogBuilder.Append(PlayerScalingRuntimeFormulaUtility.FormatNumber(evaluatedValue));
        batchedLogBuilder.AppendLine("</color>");
    }

    /// <summary>
    /// Resolves target display name with fallback to stat key.
    /// </summary>
    /// <param name="debugRule">Debug-rule snapshot entry.</param>
    /// <returns>Resolved display name used in the output message.</returns>
    private static string ResolveTargetDisplayName(in PlayerScalingDebugRuleElement debugRule)
    {
        string targetDisplayName = debugRule.TargetDisplayName.ToString();

        if (!string.IsNullOrWhiteSpace(targetDisplayName))
            return targetDisplayName;

        string statKey = debugRule.StatKey.ToString();

        if (!string.IsNullOrWhiteSpace(statKey))
            return statKey;

        return "Scaled Stat";
    }


    /// <summary>
    /// Resolves the per-rule debug color with safe clamping and fallback to default yellow for invalid/legacy data.
    /// </summary>
    /// <param name="debugRule">Current debug-rule snapshot.</param>
    /// <returns>Resolved color used for this log line.</returns>
    private static Color ResolveDebugColor(in PlayerScalingDebugRuleElement debugRule)
    {
        float red = Mathf.Clamp01(debugRule.DebugColor.x);
        float green = Mathf.Clamp01(debugRule.DebugColor.y);
        float blue = Mathf.Clamp01(debugRule.DebugColor.z);
        float alpha = Mathf.Clamp01(debugRule.DebugColor.w);

        if (alpha <= 0.0001f)
            return PlayerStatScalingRule.GetDefaultDebugColor();

        return new Color(red, green, blue, alpha);
    }

    #endregion

    #endregion
}
#endif
