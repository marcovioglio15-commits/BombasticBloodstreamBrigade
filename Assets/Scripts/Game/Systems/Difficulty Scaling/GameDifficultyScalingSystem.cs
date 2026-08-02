using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Evaluates the authoritative difficulty graph only when its built-in or player input context changes.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateBefore(typeof(EnemySystemGroup))]
public partial class GameDifficultyScalingSystem : SystemBase
{
    #region Constants
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;
    private const float ComparisonTolerance = 0.0001f;
    #endregion

    #region Fields
    private readonly Dictionary<string, PlayerFormulaValue> variableContext =
        new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> resolvedValues =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private EntityQuery managerQuery;
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates stable singleton and player queries used by event-driven context evaluation.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameDifficultyScalingConfig),
                                      typeof(GameDifficultyRuntimeState),
                                      typeof(GameDifficultyCoefficientDefinitionElement),
                                      typeof(GameDifficultyCurveSampleElement),
                                      typeof(GameDifficultyStepElement),
                                      typeof(GameDifficultyStepConditionElement),
                                      typeof(GameDifficultyCoefficientValueElement));
        playerQuery = GetEntityQuery(typeof(PlayerScalableStatElement));
        RequireForUpdate(managerQuery);
    }

    /// <summary>
    /// Rebuilds the context, early-outs on a stable hash and commits changed coefficient values atomically.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameDifficultyScalingConfig config = EntityManager.GetComponentData<GameDifficultyScalingConfig>(managerEntity);
        GameDifficultyRuntimeState runtimeState = EntityManager.GetComponentData<GameDifficultyRuntimeState>(managerEntity);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        if (runtimeState.Initialized == 0)
            runtimeState.RunStartTime = elapsedTime;

        BuildVariableContext(managerEntity, config, runtimeState, elapsedTime);
        uint sourceHash = ComputeContextHash(variableContext);

        if (runtimeState.Initialized != 0 && runtimeState.SourceHash == sourceHash)
            return;

        DynamicBuffer<GameDifficultyCoefficientDefinitionElement> definitions =
            EntityManager.GetBuffer<GameDifficultyCoefficientDefinitionElement>(managerEntity, true);
        DynamicBuffer<GameDifficultyCurveSampleElement> curveSamples =
            EntityManager.GetBuffer<GameDifficultyCurveSampleElement>(managerEntity, true);
        DynamicBuffer<GameDifficultyStepElement> steps =
            EntityManager.GetBuffer<GameDifficultyStepElement>(managerEntity, true);
        DynamicBuffer<GameDifficultyStepConditionElement> conditions =
            EntityManager.GetBuffer<GameDifficultyStepConditionElement>(managerEntity, true);
        DynamicBuffer<GameDifficultyCoefficientValueElement> coefficientValues =
            EntityManager.GetBuffer<GameDifficultyCoefficientValueElement>(managerEntity);
        bool changed = EvaluateDefinitions(definitions,
                                           curveSamples,
                                           steps,
                                           conditions,
                                           coefficientValues);
        runtimeState.SourceHash = sourceHash;
        runtimeState.Initialized = 1;

        if (changed || runtimeState.Version == 0u)
            runtimeState.Version = runtimeState.Version == uint.MaxValue ? 1u : runtimeState.Version + 1u;

        EntityManager.SetComponentData(managerEntity, runtimeState);
        GameDifficultyRuntimeValueStore.Replace(resolvedValues, runtimeState.Version);
    }

    /// <summary>
    /// Clears the managed read projection when this world's authoritative system is destroyed.
    /// </summary>
    protected override void OnDestroy()
    {
        GameDifficultyRuntimeValueStore.Clear();
    }
    #endregion

    #region Context
    /// <summary>
    /// Rebuilds built-in run variables and the current typed player scalable-stat projection.
    /// </summary>
    /// <param name="managerEntity">Difficulty singleton entity that may also own procedural state.</param>
    /// <param name="config">Immutable difficulty configuration describing optional time usage.</param>
    /// <param name="runtimeState">Current difficulty runtime state containing the run time origin.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    private void BuildVariableContext(Entity managerEntity,
                                      GameDifficultyScalingConfig config,
                                      GameDifficultyRuntimeState runtimeState,
                                      float elapsedTime)
    {
        variableContext.Clear();
        GameProceduralLevelRuntimeState levelState = EntityManager.HasComponent<GameProceduralLevelRuntimeState>(managerEntity)
            ? EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity)
            : default;
        GameProceduralRoomClearCounter roomCounter = EntityManager.HasComponent<GameProceduralRoomClearCounter>(managerEntity)
            ? EntityManager.GetComponentData<GameProceduralRoomClearCounter>(managerEntity)
            : default;
        AddNumber(GameDifficultyVariableNames.RoomsCleared, roomCounter.TotalCleared);
        AddNumber(GameDifficultyVariableNames.CurrentDepth, levelState.CurrentDepth);
        AddNumber(GameDifficultyVariableNames.LevelIndex, levelState.CurrentLevelIndex);
        AddNumber(GameDifficultyVariableNames.VisitOrdinal, levelState.VisitOrdinal);
        AddNumber(GameDifficultyVariableNames.RunSeed, levelState.RunSeed);
        AddNumber(GameDifficultyVariableNames.GenerationVersion, levelState.GenerationVersion);
        float runElapsedSeconds = config.UsesElapsedRunTime != 0
            ? math.floor(math.max(0f, elapsedTime - runtimeState.RunStartTime) * 4f) * 0.25f
            : 0f;
        AddNumber(GameDifficultyVariableNames.RunElapsedSeconds, runElapsedSeconds);

        if (playerQuery.CalculateEntityCount() != 1)
        {
            AddDefaultPlayerContext();
            return;
        }

        Entity playerEntity = playerQuery.GetSingletonEntity();
        DynamicBuffer<PlayerScalableStatElement> scalableStats =
            EntityManager.GetBuffer<PlayerScalableStatElement>(playerEntity, true);

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length > 0)
                variableContext[scalableStat.Name.ToString()] = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
        }
        PlayerLevel playerLevel = EntityManager.HasComponent<PlayerLevel>(playerEntity)
            ? EntityManager.GetComponentData<PlayerLevel>(playerEntity)
            : default;
        PlayerExperience playerExperience = EntityManager.HasComponent<PlayerExperience>(playerEntity)
            ? EntityManager.GetComponentData<PlayerExperience>(playerEntity)
            : default;
        PlayerHealth playerHealth = EntityManager.HasComponent<PlayerHealth>(playerEntity)
            ? EntityManager.GetComponentData<PlayerHealth>(playerEntity)
            : default;
        PlayerShield playerShield = EntityManager.HasComponent<PlayerShield>(playerEntity)
            ? EntityManager.GetComponentData<PlayerShield>(playerEntity)
            : default;
        AddNumber(GameDifficultyVariableNames.PlayerLevel, playerLevel.Current);
        AddNumber(GameDifficultyVariableNames.PlayerExperience, playerExperience.Current);
        AddNumber(GameDifficultyVariableNames.PlayerHealth, playerHealth.Current);
        AddNumber(GameDifficultyVariableNames.PlayerHealthRatio,
                  playerHealth.Max > 0f ? math.saturate(playerHealth.Current / playerHealth.Max) : 0f);
        AddNumber(GameDifficultyVariableNames.PlayerShield, playerShield.Current);
        AddNumber(GameDifficultyVariableNames.PlayerShieldRatio,
                  playerShield.Max > 0f ? math.saturate(playerShield.Current / playerShield.Max) : 0f);
    }

    /// <summary>
    /// Adds stable zero-valued player built-ins when no unique player entity is available.
    /// </summary>
    private void AddDefaultPlayerContext()
    {
        AddNumber(GameDifficultyVariableNames.PlayerLevel, 0f);
        AddNumber(GameDifficultyVariableNames.PlayerExperience, 0f);
        AddNumber(GameDifficultyVariableNames.PlayerHealth, 0f);
        AddNumber(GameDifficultyVariableNames.PlayerHealthRatio, 0f);
        AddNumber(GameDifficultyVariableNames.PlayerShield, 0f);
        AddNumber(GameDifficultyVariableNames.PlayerShieldRatio, 0f);
    }

    /// <summary>
    /// Writes one numeric value into the reusable typed variable context.
    /// </summary>
    /// <param name="variableName">Stable formula variable identifier.</param>
    /// <param name="value">Numeric value exposed to evaluation.</param>
    private void AddNumber(string variableName, float value)
    {
        variableContext[variableName] = PlayerFormulaValue.CreateNumber(value);
    }
    #endregion

    #region Evaluation
    /// <summary>
    /// Evaluates all dependency-ordered definitions and writes the authoritative value buffer.
    /// </summary>
    /// <param name="definitions">Dependency-ordered coefficient definitions.</param>
    /// <param name="curveSamples">Shared sampled curve storage.</param>
    /// <param name="steps">Shared flattened quantized steps.</param>
    /// <param name="conditions">Shared flattened step conditions.</param>
    /// <param name="coefficientValues">Mutable authoritative coefficient values.</param>
    /// <returns>True when at least one coefficient changed.</returns>
    private bool EvaluateDefinitions(DynamicBuffer<GameDifficultyCoefficientDefinitionElement> definitions,
                                     DynamicBuffer<GameDifficultyCurveSampleElement> curveSamples,
                                     DynamicBuffer<GameDifficultyStepElement> steps,
                                     DynamicBuffer<GameDifficultyStepConditionElement> conditions,
                                     DynamicBuffer<GameDifficultyCoefficientValueElement> coefficientValues)
    {
        resolvedValues.Clear();
        bool changed = coefficientValues.Length != definitions.Length;

        while (coefficientValues.Length < definitions.Length)
            coefficientValues.Add(default);

        if (coefficientValues.Length > definitions.Length)
            coefficientValues.ResizeUninitialized(definitions.Length);

        for (int coefficientIndex = 0; coefficientIndex < definitions.Length; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinitionElement definition = definitions[coefficientIndex];
            float resolvedValue = ResolveValue(in definition, curveSamples, steps, conditions);
            resolvedValue = math.clamp(resolvedValue, definition.MinimumValue, definition.MaximumValue);
            GameDifficultyCoefficientValueElement previousValue = coefficientValues[coefficientIndex];

            if (!previousValue.CoefficientId.Equals(definition.CoefficientId) ||
                math.abs(previousValue.Value - resolvedValue) > ComparisonTolerance)
            {
                changed = true;
            }

            coefficientValues[coefficientIndex] = new GameDifficultyCoefficientValueElement
            {
                CoefficientId = definition.CoefficientId,
                Value = resolvedValue
            };
            string coefficientId = definition.CoefficientId.ToString();
            resolvedValues[coefficientId] = resolvedValue;
            variableContext[coefficientId] = PlayerFormulaValue.CreateNumber(resolvedValue);
#if UNITY_EDITOR
            if (definition.DebugInConsole != 0 && changed)
                Debug.Log("[Difficulty Scaling] " + coefficientId + " = " + resolvedValue.ToString("0.###"));
#endif
        }

        return changed;
    }

    /// <summary>
    /// Resolves one coefficient through its selected authoring strategy with finite fallback handling.
    /// </summary>
    /// <param name="definition">Coefficient definition being evaluated.</param>
    /// <param name="curveSamples">Shared sampled curve storage.</param>
    /// <param name="steps">Shared flattened quantized steps.</param>
    /// <param name="conditions">Shared flattened step conditions.</param>
    /// <returns>Finite resolved value or the authored default when evaluation fails.</returns>
    private float ResolveValue(in GameDifficultyCoefficientDefinitionElement definition,
                               DynamicBuffer<GameDifficultyCurveSampleElement> curveSamples,
                               DynamicBuffer<GameDifficultyStepElement> steps,
                               DynamicBuffer<GameDifficultyStepConditionElement> conditions)
    {
        float resolvedValue;

        switch (definition.ScalingMode)
        {
            case GameDifficultyScalingMode.Curve:
                resolvedValue = ResolveCurveValue(in definition, curveSamples);
                break;
            case GameDifficultyScalingMode.Steps:
                resolvedValue = ResolveStepValue(in definition, steps, conditions);
                break;
            default:
                resolvedValue = ResolveFormulaValue(in definition);
                break;
        }

        if (float.IsNaN(resolvedValue) || float.IsInfinity(resolvedValue))
            return definition.DefaultValue;

        return resolvedValue;
    }

    /// <summary>
    /// Evaluates one unified numeric formula against built-ins, player stats and prior coefficients.
    /// </summary>
    /// <param name="definition">Formula-backed coefficient definition.</param>
    /// <returns>Formula result or authored default when parsing or evaluation fails.</returns>
    private float ResolveFormulaValue(in GameDifficultyCoefficientDefinitionElement definition)
    {
        if (PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(definition.Formula.ToString(),
                                                                  definition.DefaultValue,
                                                                  variableContext,
                                                                  out float resolvedValue,
                                                                  out string ignoredError,
                                                                  true))
        {
            return resolvedValue;
        }

        return definition.DefaultValue;
    }

    /// <summary>
    /// Interpolates one sampled curve using its selected numeric input variable.
    /// </summary>
    /// <param name="definition">Curve-backed coefficient definition.</param>
    /// <param name="curveSamples">Shared sampled curve storage.</param>
    /// <returns>Interpolated curve output or authored default when input is unavailable.</returns>
    private float ResolveCurveValue(in GameDifficultyCoefficientDefinitionElement definition,
                                    DynamicBuffer<GameDifficultyCurveSampleElement> curveSamples)
    {
        if (!TryResolveNumber(definition.CurveInputVariable.ToString(), out float input) ||
            definition.CurveSampleCount <= 0)
        {
            return definition.DefaultValue;
        }

        int firstIndex = definition.FirstCurveSampleIndex;
        int lastIndex = firstIndex + definition.CurveSampleCount - 1;

        if (firstIndex < 0 || lastIndex >= curveSamples.Length)
            return definition.DefaultValue;

        if (input <= curveSamples[firstIndex].Input)
            return curveSamples[firstIndex].Output;

        for (int sampleIndex = firstIndex + 1; sampleIndex <= lastIndex; sampleIndex++)
        {
            GameDifficultyCurveSampleElement current = curveSamples[sampleIndex];

            if (input > current.Input)
                continue;

            GameDifficultyCurveSampleElement previous = curveSamples[sampleIndex - 1];
            float interpolation = math.unlerp(previous.Input, current.Input, input);
            return math.lerp(previous.Output, current.Output, interpolation);
        }

        return curveSamples[lastIndex].Output;
    }

    /// <summary>
    /// Returns the first matching ordered quantized step output.
    /// </summary>
    /// <param name="definition">Step-backed coefficient definition.</param>
    /// <param name="steps">Shared flattened step storage.</param>
    /// <param name="conditions">Shared flattened condition storage.</param>
    /// <returns>First matching step output or authored default when none match.</returns>
    private float ResolveStepValue(in GameDifficultyCoefficientDefinitionElement definition,
                                   DynamicBuffer<GameDifficultyStepElement> steps,
                                   DynamicBuffer<GameDifficultyStepConditionElement> conditions)
    {
        int lastStepIndex = math.min(steps.Length, definition.FirstStepIndex + definition.StepCount);

        for (int stepIndex = math.max(0, definition.FirstStepIndex); stepIndex < lastStepIndex; stepIndex++)
        {
            GameDifficultyStepElement step = steps[stepIndex];

            if (MatchesStep(in step, conditions))
                return step.OutputValue;
        }

        return definition.DefaultValue;
    }

    /// <summary>
    /// Evaluates one flattened step using its authored All or Any condition policy.
    /// </summary>
    /// <param name="step">Quantized step being tested.</param>
    /// <param name="conditions">Shared flattened condition storage.</param>
    /// <returns>True when the condition slice satisfies the selected combination policy.</returns>
    private bool MatchesStep(in GameDifficultyStepElement step,
                             DynamicBuffer<GameDifficultyStepConditionElement> conditions)
    {
        if (step.ConditionCount <= 0)
            return true;

        int lastConditionIndex = math.min(conditions.Length, step.FirstConditionIndex + step.ConditionCount);
        bool anyMatched = false;

        for (int conditionIndex = math.max(0, step.FirstConditionIndex);
             conditionIndex < lastConditionIndex;
             conditionIndex++)
        {
            bool matched = MatchesCondition(conditions[conditionIndex]);

            if (step.ConditionCombination == GameDifficultyConditionCombination.All && !matched)
                return false;

            if (matched)
                anyMatched = true;
        }

        return step.ConditionCombination == GameDifficultyConditionCombination.All || anyMatched;
    }

    /// <summary>
    /// Evaluates one numeric comparison against the current typed variable context.
    /// </summary>
    /// <param name="condition">Flattened comparison being evaluated.</param>
    /// <returns>True when the current variable value satisfies the comparison.</returns>
    private bool MatchesCondition(GameDifficultyStepConditionElement condition)
    {
        if (!TryResolveNumber(condition.VariableName.ToString(), out float value))
            return false;

        switch (condition.Comparison)
        {
            case GameDifficultyComparison.Less:
                return value < condition.Threshold;
            case GameDifficultyComparison.LessOrEqual:
                return value <= condition.Threshold;
            case GameDifficultyComparison.Equal:
                return math.abs(value - condition.Threshold) <= ComparisonTolerance;
            case GameDifficultyComparison.Greater:
                return value > condition.Threshold;
            case GameDifficultyComparison.NotEqual:
                return math.abs(value - condition.Threshold) > ComparisonTolerance;
            default:
                return value >= condition.Threshold;
        }
    }

    /// <summary>
    /// Resolves one numeric variable from the current typed formula context.
    /// </summary>
    /// <param name="variableName">Variable identifier requested by a curve or step.</param>
    /// <param name="value">Numeric value when the variable exists and is numeric.</param>
    /// <returns>True when a numeric variable was resolved.</returns>
    private bool TryResolveNumber(string variableName, out float value)
    {
        value = 0f;

        if (string.IsNullOrWhiteSpace(variableName) ||
            !variableContext.TryGetValue(variableName, out PlayerFormulaValue formulaValue) ||
            formulaValue.Type != PlayerFormulaValueType.Number)
        {
            return false;
        }

        value = formulaValue.NumberValue;
        return true;
    }
    #endregion

    #region Hashing
    /// <summary>
    /// Computes a stable FNV-1a hash for the complete typed source context.
    /// </summary>
    /// <param name="context">Current built-in and player variable context.</param>
    /// <returns>Stable hash used to skip unchanged evaluations.</returns>
    private static uint ComputeContextHash(IReadOnlyDictionary<string, PlayerFormulaValue> context)
    {
        uint rollingHash = FnvOffsetBasis;

        foreach (KeyValuePair<string, PlayerFormulaValue> entry in context)
        {
            rollingHash = (rollingHash ^ (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(entry.Key)) * FnvPrime;
            rollingHash = (rollingHash ^ (uint)entry.Value.Type) * FnvPrime;

            switch (entry.Value.Type)
            {
                case PlayerFormulaValueType.Boolean:
                    rollingHash = (rollingHash ^ (entry.Value.BooleanValue ? 1u : 0u)) * FnvPrime;
                    break;
                case PlayerFormulaValueType.Token:
                    rollingHash = (rollingHash ^ (uint)StringComparer.Ordinal.GetHashCode(entry.Value.TokenValue ?? string.Empty)) * FnvPrime;
                    break;
                default:
                    rollingHash = (rollingHash ^ math.asuint(entry.Value.NumberValue)) * FnvPrime;
                    break;
            }
        }

        return rollingHash;
    }
    #endregion

    #endregion
}
