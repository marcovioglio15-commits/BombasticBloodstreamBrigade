using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Flattens validated difficulty presets into dependency-ordered ECS buffers for baked and fallback worlds.
/// </summary>
public static class GameDifficultyScalingBakeUtility
{
    #region Constants
    private const int CurveSampleCount = 64;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates the complete difficulty graph before any runtime data is created.
    /// </summary>
    /// <param name="preset">Difficulty preset selected by the active Game Master preset.</param>
    /// <param name="failureMessage">Combined actionable diagnostics when validation fails.</param>
    /// <returns>True when the graph is dependency-safe and can be baked.</returns>
    public static bool TryValidateRuntimeConfiguration(GameDifficultyScalingPreset preset,
                                                       out string failureMessage)
    {
        List<string> warnings = GameDifficultyScalingValidationUtility.BuildWarnings(preset);

        if (warnings.Count > 0)
        {
            failureMessage = string.Join(" | ", warnings);
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Builds immutable singleton metadata from one validated difficulty preset.
    /// </summary>
    /// <param name="preset">Difficulty preset used as the runtime source.</param>
    /// <returns>Immutable difficulty singleton configuration.</returns>
    public static GameDifficultyScalingConfig BuildConfig(GameDifficultyScalingPreset preset)
    {
        return new GameDifficultyScalingConfig
        {
            PresetId = new Unity.Collections.FixedString64Bytes(preset != null ? preset.PresetId : string.Empty),
            CoefficientCount = preset != null && preset.Coefficients != null ? preset.Coefficients.Count : 0,
            UsesElapsedRunTime = UsesElapsedRunTime(preset) ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Populates all flattened difficulty buffers in dependency-safe evaluation order.
    /// </summary>
    /// <param name="preset">Validated difficulty preset supplying authoring definitions.</param>
    /// <param name="definitionBuffer">Destination coefficient definition buffer.</param>
    /// <param name="curveBuffer">Destination sampled-curve buffer.</param>
    /// <param name="stepBuffer">Destination quantized-step buffer.</param>
    /// <param name="conditionBuffer">Destination step-condition buffer.</param>
    /// <param name="valueBuffer">Destination runtime coefficient value buffer.</param>
    public static void PopulateBuffers(GameDifficultyScalingPreset preset,
                                       DynamicBuffer<GameDifficultyCoefficientDefinitionElement> definitionBuffer,
                                       DynamicBuffer<GameDifficultyCurveSampleElement> curveBuffer,
                                       DynamicBuffer<GameDifficultyStepElement> stepBuffer,
                                       DynamicBuffer<GameDifficultyStepConditionElement> conditionBuffer,
                                       DynamicBuffer<GameDifficultyCoefficientValueElement> valueBuffer)
    {
        definitionBuffer.Clear();
        curveBuffer.Clear();
        stepBuffer.Clear();
        conditionBuffer.Clear();
        valueBuffer.Clear();

        if (!GameDifficultyScalingValidationUtility.TryBuildEvaluationOrder(
                preset,
                out List<GameDifficultyCoefficientDefinition> orderedDefinitions,
                out string ignoredError))
        {
            return;
        }

        for (int coefficientIndex = 0; coefficientIndex < orderedDefinitions.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition definition = orderedDefinitions[coefficientIndex];
            int firstCurveSampleIndex = curveBuffer.Length;
            int firstStepIndex = stepBuffer.Length;

            if (definition.ScalingMode == GameDifficultyScalingMode.Curve)
                PopulateCurve(definition.ScalingCurve, curveBuffer);

            if (definition.ScalingMode == GameDifficultyScalingMode.Steps)
                PopulateSteps(definition.Steps, stepBuffer, conditionBuffer);

            definitionBuffer.Add(new GameDifficultyCoefficientDefinitionElement
            {
                CoefficientId = new Unity.Collections.FixedString64Bytes(definition.CoefficientId),
                DisplayName = new Unity.Collections.FixedString128Bytes(definition.DisplayName ?? string.Empty),
                Formula = new Unity.Collections.FixedString512Bytes(definition.Formula ?? string.Empty),
                CurveInputVariable = new Unity.Collections.FixedString64Bytes(definition.CurveInputVariable ?? string.Empty),
                ScalingMode = definition.ScalingMode,
                DefaultValue = definition.DefaultValue,
                MinimumValue = definition.MinimumValue,
                MaximumValue = definition.MaximumValue,
                FirstCurveSampleIndex = firstCurveSampleIndex,
                CurveSampleCount = curveBuffer.Length - firstCurveSampleIndex,
                FirstStepIndex = firstStepIndex,
                StepCount = stepBuffer.Length - firstStepIndex,
                DebugInConsole = definition.DebugInConsole ? (byte)1 : (byte)0
            });
            valueBuffer.Add(new GameDifficultyCoefficientValueElement
            {
                CoefficientId = new Unity.Collections.FixedString64Bytes(definition.CoefficientId),
                Value = definition.DefaultValue
            });
        }
    }
    #endregion

    #region Curves
    /// <summary>
    /// Samples one authored curve across its exact key-time domain for allocation-free runtime interpolation.
    /// </summary>
    /// <param name="curve">Authored curve being flattened.</param>
    /// <param name="curveBuffer">Destination shared curve buffer.</param>
    private static void PopulateCurve(AnimationCurve curve,
                                      DynamicBuffer<GameDifficultyCurveSampleElement> curveBuffer)
    {
        if (curve == null || curve.length == 0)
            return;

        Keyframe[] keys = curve.keys;
        float minimumInput = keys[0].time;
        float maximumInput = keys[keys.Length - 1].time;

        if (Mathf.Approximately(minimumInput, maximumInput))
        {
            curveBuffer.Add(new GameDifficultyCurveSampleElement
            {
                Input = minimumInput,
                Output = curve.Evaluate(minimumInput)
            });
            return;
        }

        for (int sampleIndex = 0; sampleIndex < CurveSampleCount; sampleIndex++)
        {
            float interpolation = (float)sampleIndex / (CurveSampleCount - 1);
            float input = Mathf.Lerp(minimumInput, maximumInput, interpolation);
            curveBuffer.Add(new GameDifficultyCurveSampleElement
            {
                Input = input,
                Output = curve.Evaluate(input)
            });
        }
    }
    #endregion

    #region Steps
    /// <summary>
    /// Flattens ordered quantized steps and their contiguous condition slices.
    /// </summary>
    /// <param name="steps">Authored ordered step collection.</param>
    /// <param name="stepBuffer">Destination flattened step buffer.</param>
    /// <param name="conditionBuffer">Destination flattened condition buffer.</param>
    private static void PopulateSteps(IReadOnlyList<GameDifficultyStepDefinition> steps,
                                      DynamicBuffer<GameDifficultyStepElement> stepBuffer,
                                      DynamicBuffer<GameDifficultyStepConditionElement> conditionBuffer)
    {
        if (steps == null)
            return;

        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            GameDifficultyStepDefinition step = steps[stepIndex];

            if (step == null)
                continue;

            int firstConditionIndex = conditionBuffer.Length;

            if (step.Conditions != null)
            {
                for (int conditionIndex = 0; conditionIndex < step.Conditions.Count; conditionIndex++)
                {
                    GameDifficultyStepCondition condition = step.Conditions[conditionIndex];

                    if (condition == null)
                        continue;

                    conditionBuffer.Add(new GameDifficultyStepConditionElement
                    {
                        VariableName = new Unity.Collections.FixedString64Bytes(condition.VariableName ?? string.Empty),
                        Comparison = condition.Comparison,
                        Threshold = condition.Threshold
                    });
                }
            }

            stepBuffer.Add(new GameDifficultyStepElement
            {
                ConditionCombination = step.ConditionCombination,
                OutputValue = step.OutputValue,
                FirstConditionIndex = firstConditionIndex,
                ConditionCount = conditionBuffer.Length - firstConditionIndex
            });
        }
    }
    #endregion

    #region Context Requirements
    /// <summary>
    /// Determines whether any coefficient actively references the elapsed-run-time variable.
    /// </summary>
    /// <param name="preset">Difficulty preset whose graph is inspected.</param>
    /// <returns>True when runtime time changes must invalidate the coefficient context.</returns>
    private static bool UsesElapsedRunTime(GameDifficultyScalingPreset preset)
    {
        if (preset == null || preset.Coefficients == null)
            return false;

        for (int coefficientIndex = 0; coefficientIndex < preset.Coefficients.Count; coefficientIndex++)
        {
            GameDifficultyCoefficientDefinition definition = preset.Coefficients[coefficientIndex];

            if (definition == null)
                continue;

            if (definition.ScalingMode == GameDifficultyScalingMode.Curve &&
                string.Equals(definition.CurveInputVariable,
                              GameDifficultyVariableNames.RunElapsedSeconds,
                              StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (definition.ScalingMode == GameDifficultyScalingMode.Formula)
            {
                PlayerStatFormulaCompileResult result = PlayerStatFormulaEngine.Compile(definition.Formula, false);

                if (result.IsValid && result.CompiledFormula != null && ContainsVariable(result.CompiledFormula.VariableNames,
                                                                                         GameDifficultyVariableNames.RunElapsedSeconds))
                {
                    return true;
                }
            }

            if (definition.ScalingMode == GameDifficultyScalingMode.Steps && UsesElapsedRunTime(definition.Steps))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether one step collection references elapsed run time.
    /// </summary>
    /// <param name="steps">Quantized step definitions being inspected.</param>
    /// <returns>True when at least one condition consumes elapsed run time.</returns>
    private static bool UsesElapsedRunTime(IReadOnlyList<GameDifficultyStepDefinition> steps)
    {
        if (steps == null)
            return false;

        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            GameDifficultyStepDefinition step = steps[stepIndex];

            if (step == null || step.Conditions == null)
                continue;

            for (int conditionIndex = 0; conditionIndex < step.Conditions.Count; conditionIndex++)
            {
                GameDifficultyStepCondition condition = step.Conditions[conditionIndex];

                if (condition != null &&
                    string.Equals(condition.VariableName,
                                  GameDifficultyVariableNames.RunElapsedSeconds,
                                  StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks a compiled variable list for one case-insensitive identifier.
    /// </summary>
    /// <param name="variables">Compiled formula variable list.</param>
    /// <param name="targetVariable">Identifier searched in the list.</param>
    /// <returns>True when the formula references the target variable.</returns>
    private static bool ContainsVariable(IReadOnlyList<string> variables, string targetVariable)
    {
        for (int variableIndex = 0; variableIndex < variables.Count; variableIndex++)
        {
            if (string.Equals(variables[variableIndex], targetVariable, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
