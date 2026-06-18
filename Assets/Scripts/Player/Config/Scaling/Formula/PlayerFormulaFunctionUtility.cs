using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Centralizes eager formula function evaluation and type inference shared by Add Scaling and Character Tuning formulas.
/// </summary>
public static class PlayerFormulaFunctionUtility
{
    #region Constants
    public const string SupportedFunctionsLabel = "min, max, clamp, saturate, abs, round, floor, ceil, pow, log, sqrt, lerp, inverseLerp, remap, between, select, switch";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Evaluates one eager built-in formula function after all arguments have already been resolved.
    /// </summary>
    /// <param name="functionName">Function identifier as authored in the formula.</param>
    /// <param name="argumentValues">Ordered resolved argument values.</param>
    /// <param name="result">Resolved function result when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when the function is supported and evaluation succeeds.</returns>
    public static bool TryEvaluate(string functionName,
                                   IReadOnlyList<PlayerFormulaValue> argumentValues,
                                   out PlayerFormulaValue result,
                                   out string errorMessage)
    {
        result = PlayerFormulaValue.CreateInvalid();
        errorMessage = string.Empty;
        int argumentCount = argumentValues != null ? argumentValues.Count : 0;

        if (string.Equals(functionName, "between", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 3, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[0], functionName, out float value, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[1], functionName, out float minimumValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[2], functionName, out float maximumValue, out errorMessage))
                return false;

            float orderedMinimumValue = Mathf.Min(minimumValue, maximumValue);
            float orderedMaximumValue = Mathf.Max(minimumValue, maximumValue);
            result = PlayerFormulaValue.CreateBoolean(value >= orderedMinimumValue && value <= orderedMaximumValue);
            return true;
        }

        if (string.Equals(functionName, "min", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 2, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(argumentValues[0], argumentValues[1], functionName, out float leftValue, out float rightValue, out errorMessage))
                return false;

            result = PlayerFormulaValue.CreateNumber(Mathf.Min(leftValue, rightValue));
            return true;
        }

        if (string.Equals(functionName, "max", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 2, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(argumentValues[0], argumentValues[1], functionName, out float leftValue, out float rightValue, out errorMessage))
                return false;

            result = PlayerFormulaValue.CreateNumber(Mathf.Max(leftValue, rightValue));
            return true;
        }

        if (string.Equals(functionName, "pow", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 2, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(argumentValues[0], argumentValues[1], functionName, out float leftValue, out float rightValue, out errorMessage))
                return false;

            result = PlayerFormulaValue.CreateNumber(Mathf.Pow(leftValue, rightValue));
            return true;
        }

        if (string.Equals(functionName, "clamp", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 3, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[0], functionName, out float value, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[1], functionName, out float minimumValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[2], functionName, out float maximumValue, out errorMessage))
                return false;

            result = PlayerFormulaValue.CreateNumber(Mathf.Clamp(value, minimumValue, maximumValue));
            return true;
        }

        if (string.Equals(functionName, "lerp", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 3, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[0], functionName, out float startValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[1], functionName, out float endValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[2], functionName, out float interpolationValue, out errorMessage))
                return false;

            result = PlayerFormulaValue.CreateNumber(Mathf.Lerp(startValue, endValue, interpolationValue));
            return true;
        }

        if (string.Equals(functionName, "inverseLerp", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 3, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[0], functionName, out float startValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[1], functionName, out float endValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[2], functionName, out float value, out errorMessage))
                return false;

            result = PlayerFormulaValue.CreateNumber(Mathf.InverseLerp(startValue, endValue, value));
            return true;
        }

        if (string.Equals(functionName, "remap", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 5, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[0], functionName, out float inputMinimum, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[1], functionName, out float inputMaximum, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[2], functionName, out float outputMinimum, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[3], functionName, out float outputMaximum, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[4], functionName, out float value, out errorMessage))
                return false;

            float interpolationValue = Mathf.InverseLerp(inputMinimum, inputMaximum, value);
            result = PlayerFormulaValue.CreateNumber(Mathf.Lerp(outputMinimum, outputMaximum, interpolationValue));
            return true;
        }

        if (string.Equals(functionName, "abs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "round", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "floor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "ceil", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "log", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "sqrt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "saturate", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 1, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireNumber(argumentValues[0], functionName, out float value, out errorMessage))
                return false;

            if (string.Equals(functionName, "abs", StringComparison.OrdinalIgnoreCase))
            {
                result = PlayerFormulaValue.CreateNumber(Mathf.Abs(value));
                return true;
            }

            if (string.Equals(functionName, "round", StringComparison.OrdinalIgnoreCase))
            {
                result = PlayerFormulaValue.CreateNumber(Mathf.Round(value));
                return true;
            }

            if (string.Equals(functionName, "floor", StringComparison.OrdinalIgnoreCase))
            {
                result = PlayerFormulaValue.CreateNumber(Mathf.Floor(value));
                return true;
            }

            if (string.Equals(functionName, "ceil", StringComparison.OrdinalIgnoreCase))
            {
                result = PlayerFormulaValue.CreateNumber(Mathf.Ceil(value));
                return true;
            }

            if (string.Equals(functionName, "saturate", StringComparison.OrdinalIgnoreCase))
            {
                result = PlayerFormulaValue.CreateNumber(Mathf.Clamp01(value));
                return true;
            }

            if (string.Equals(functionName, "log", StringComparison.OrdinalIgnoreCase))
            {
                if (value <= 0f)
                {
                    errorMessage = "log() requires an operand greater than zero.";
                    return false;
                }

                result = PlayerFormulaValue.CreateNumber(Mathf.Log(value));
                return true;
            }

            if (value < 0f)
            {
                errorMessage = "sqrt() requires a non-negative operand.";
                return false;
            }

            result = PlayerFormulaValue.CreateNumber(Mathf.Sqrt(value));
            return true;
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "Unsupported function '{0}'.",
                                     functionName);
        return false;
    }

    /// <summary>
    /// Infers the output type of one eager built-in formula function from already inferred argument types.
    /// </summary>
    /// <param name="functionName">Function identifier as authored in the formula.</param>
    /// <param name="argumentTypes">Ordered inferred argument types.</param>
    /// <param name="resultType">Resolved output type when inference succeeds.</param>
    /// <param name="errorMessage">Failure reason when inference fails.</param>
    /// <returns>True when the function is supported and the type contract is valid.</returns>
    public static bool TryInferType(string functionName,
                                    IReadOnlyList<PlayerFormulaValueType> argumentTypes,
                                    out PlayerFormulaValueType resultType,
                                    out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;
        int argumentCount = argumentTypes != null ? argumentTypes.Count : 0;

        if (string.Equals(functionName, "between", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 3, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.AreAllNumbers(argumentTypes))
            {
                errorMessage = "between() requires numeric arguments.";
                return false;
            }

            resultType = PlayerFormulaValueType.Boolean;
            return true;
        }

        if (string.Equals(functionName, "min", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "max", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "pow", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 2, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.AreAllNumbers(argumentTypes))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "{0}() requires numeric arguments.",
                                             functionName);
                return false;
            }

            resultType = PlayerFormulaValueType.Number;
            return true;
        }

        if (string.Equals(functionName, "clamp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "lerp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "inverseLerp", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 3, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.AreAllNumbers(argumentTypes))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "{0}() requires numeric arguments.",
                                             functionName);
                return false;
            }

            resultType = PlayerFormulaValueType.Number;
            return true;
        }

        if (string.Equals(functionName, "remap", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 5, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.AreAllNumbers(argumentTypes))
            {
                errorMessage = "remap() requires numeric arguments.";
                return false;
            }

            resultType = PlayerFormulaValueType.Number;
            return true;
        }

        if (string.Equals(functionName, "abs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "round", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "floor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "ceil", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "log", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "sqrt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "saturate", StringComparison.OrdinalIgnoreCase))
        {
            if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentCount, 1, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.AreAllNumbers(argumentTypes))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "{0}() requires a numeric argument.",
                                             functionName);
                return false;
            }

            resultType = PlayerFormulaValueType.Number;
            return true;
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "Unsupported function '{0}'.",
                                     functionName);
        return false;
    }
    #endregion

    #endregion
}
