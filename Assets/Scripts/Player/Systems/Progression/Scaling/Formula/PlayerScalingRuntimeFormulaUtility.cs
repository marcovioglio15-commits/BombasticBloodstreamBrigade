using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Shares runtime helpers for scalable-stat formula evaluation and translated debug output.
/// </summary>
internal static class PlayerScalingRuntimeFormulaUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds a case-insensitive typed variable dictionary from the runtime scalable-stat buffer.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="variableContext">Destination dictionary receiving the current typed stat values.</param>
    /// <returns>Void.</returns>
    public static void FillVariableContext(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                           Dictionary<string, PlayerFormulaValue> variableContext)
    {
        if (variableContext == null)
            return;

        variableContext.Clear();

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            string variableName = scalableStat.Name.ToString();

            if (string.IsNullOrWhiteSpace(variableName))
                continue;

            variableContext[variableName] = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
        }
    }

    /// <summary>
    /// Rebuilds a case-insensitive typed variable dictionary from a managed scalable-stat collection.
    /// </summary>
    /// <param name="scalableStats">Managed scalable-stat list used by temporary runtime overlays.</param>
    /// <param name="variableContext">Destination dictionary receiving the current typed stat values.</param>
    /// <returns>Void.</returns>
    public static void FillVariableContext(IReadOnlyList<PlayerScalableStatElement> scalableStats,
                                           Dictionary<string, PlayerFormulaValue> variableContext)
    {
        if (variableContext == null)
            return;

        variableContext.Clear();

        if (scalableStats == null)
            return;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            string variableName = scalableStat.Name.ToString();

            if (string.IsNullOrWhiteSpace(variableName))
                continue;

            variableContext[variableName] = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
        }
    }

    /// <summary>
    /// Rebuilds a case-insensitive numeric variable dictionary from the runtime scalable-stat buffer.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="variableContext">Destination dictionary receiving numeric projections of the current stat values.</param>
    /// <returns>Void.</returns>
    public static void FillVariableContext(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                           Dictionary<string, float> variableContext)
    {
        if (variableContext == null)
            return;

        variableContext.Clear();

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            string variableName = scalableStat.Name.ToString();

            if (string.IsNullOrWhiteSpace(variableName))
                continue;

            variableContext[variableName] = PlayerScalableStatClampUtility.ResolveNumericProjection(in scalableStat);
        }
    }

    /// <summary>
    /// Evaluates one runtime formula against the current typed scalable-stat context.
    /// </summary>
    /// <param name="formula">Formula string to evaluate.</param>
    /// <param name="thisValue">Typed base value mapped to the reserved [this] token.</param>
    /// <param name="variableContext">Current typed scalable-stat dictionary.</param>
    /// <param name="evaluatedValue">Resolved typed value when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded; otherwise false.</returns>
    public static bool TryEvaluateFormula(string formula,
                                          PlayerFormulaValue thisValue,
                                          IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                          out PlayerFormulaValue evaluatedValue,
                                          out string errorMessage,
                                          bool requireAtLeastOneVariable = true)
    {
        evaluatedValue = thisValue;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(formula))
            return false;

        return PlayerStatFormulaEngine.TryEvaluate(formula,
                                                   thisValue,
                                                   variableContext,
                                                   out evaluatedValue,
                                                   out errorMessage,
                                                   requireAtLeastOneVariable);
    }

    /// <summary>
    /// Evaluates one runtime formula against the current typed scalable-stat context, requiring a numeric result.
    /// </summary>
    /// <param name="formula">Formula string to evaluate.</param>
    /// <param name="thisValue">Numeric base value mapped to the reserved [this] token.</param>
    /// <param name="variableContext">Current typed scalable-stat dictionary.</param>
    /// <param name="evaluatedValue">Resolved numeric value when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded; otherwise false.</returns>
    public static bool TryEvaluateFormula(string formula,
                                          float thisValue,
                                          IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                          out float evaluatedValue,
                                          out string errorMessage,
                                          bool requireAtLeastOneVariable = true)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            evaluatedValue = thisValue;
            errorMessage = string.Empty;
            return false;
        }

        return PlayerStatFormulaEngine.TryEvaluate(formula,
                                                   thisValue,
                                                   variableContext,
                                                   out evaluatedValue,
                                                   out errorMessage,
                                                   requireAtLeastOneVariable);
    }

    /// <summary>
    /// Evaluates one runtime formula against the legacy numeric-only scalable-stat context.
    /// </summary>
    /// <param name="formula">Formula string to evaluate.</param>
    /// <param name="thisValue">Numeric base value mapped to the reserved [this] token.</param>
    /// <param name="variableContext">Current numeric scalable-stat dictionary.</param>
    /// <param name="evaluatedValue">Resolved numeric value when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded; otherwise false.</returns>
    public static bool TryEvaluateFormula(string formula,
                                          float thisValue,
                                          IReadOnlyDictionary<string, float> variableContext,
                                          out float evaluatedValue,
                                          out string errorMessage,
                                          bool requireAtLeastOneVariable = true)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            evaluatedValue = thisValue;
            errorMessage = string.Empty;
            return false;
        }

        return PlayerStatFormulaEngine.TryEvaluate(formula,
                                                   thisValue,
                                                   variableContext,
                                                   out evaluatedValue,
                                                   out errorMessage,
                                                   requireAtLeastOneVariable);
    }

    /// <summary>
    /// Replaces [this] and named variables with their resolved typed values for readable debug output.
    /// </summary>
    /// <param name="formula">Raw formula string.</param>
    /// <param name="variableContext">Current typed scalable-stat dictionary.</param>
    /// <param name="thisValue">Typed base value mapped to [this].</param>
    /// <returns>Readable translated expression suitable for logs.</returns>
    public static string TranslateFormula(string formula,
                                          IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                          PlayerFormulaValue thisValue)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return string.Empty;

        StringBuilder translatedFormulaBuilder = new StringBuilder(formula.Length + 32);
        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
            {
                translatedFormulaBuilder.Append(formula.Substring(parseIndex));
                break;
            }

            if (openBracketIndex > parseIndex)
                translatedFormulaBuilder.Append(formula.Substring(parseIndex, openBracketIndex - parseIndex));

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                translatedFormulaBuilder.Append(formula.Substring(openBracketIndex));
                break;
            }

            string variableToken = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.Equals(variableToken, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
            {
                translatedFormulaBuilder.Append(FormatValue(thisValue));
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            if (TryResolveVariableValue(variableContext, variableToken, out PlayerFormulaValue resolvedValue))
            {
                translatedFormulaBuilder.Append(FormatValue(resolvedValue));
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            translatedFormulaBuilder.Append('[');
            translatedFormulaBuilder.Append(variableToken);
            translatedFormulaBuilder.Append(']');
            parseIndex = closeBracketIndex + 1;
        }

        return translatedFormulaBuilder.ToString();
    }

    /// <summary>
    /// Replaces [this] and named variables with their resolved numeric values for readable debug output.
    /// </summary>
    /// <param name="formula">Raw formula string.</param>
    /// <param name="variableContext">Current numeric scalable-stat dictionary.</param>
    /// <param name="thisValue">Numeric base value mapped to [this].</param>
    /// <returns>Readable translated expression suitable for logs.</returns>
    public static string TranslateFormula(string formula,
                                          IReadOnlyDictionary<string, float> variableContext,
                                          float thisValue)
    {
        return TranslateFormula(formula,
                                BuildTypedContext(variableContext),
                                PlayerFormulaValue.CreateNumber(thisValue));
    }

    /// <summary>
    /// Formats one typed value for concise debug output.
    /// </summary>
    /// <param name="value">Typed value to format.</param>
    /// <returns>Compact invariant string representation.</returns>
    public static string FormatValue(PlayerFormulaValue value)
    {
        switch (value.Type)
        {
            case PlayerFormulaValueType.Number:
                return FormatNumber(value.NumberValue);
            case PlayerFormulaValueType.Boolean:
                return value.BooleanValue ? "true" : "false";
            case PlayerFormulaValueType.Token:
                return "\"" + value.TokenValue + "\"";
            default:
                return "<invalid>";
        }
    }

    /// <summary>
    /// Formats numeric values for concise debug output.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <returns>Compact invariant string representation.</returns>
    public static string FormatNumber(float value)
    {
        float roundedInteger = Mathf.Round(value);

        if (Mathf.Abs(value - roundedInteger) <= 0.0001f)
            return ((int)roundedInteger).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts a numeric-only runtime dictionary into a typed dictionary used by shared translation helpers.
    /// </summary>
    /// <param name="variableContext">Numeric variable dictionary.</param>
    /// <returns>Typed dictionary mirroring the numeric values.</returns>
    private static Dictionary<string, PlayerFormulaValue> BuildTypedContext(IReadOnlyDictionary<string, float> variableContext)
    {
        Dictionary<string, PlayerFormulaValue> typedContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);

        if (variableContext == null)
            return typedContext;

        foreach (KeyValuePair<string, float> entry in variableContext)
            typedContext[entry.Key] = PlayerFormulaValue.CreateNumber(entry.Value);

        return typedContext;
    }

    /// <summary>
    /// Resolves one named scalable-stat variable from the current typed runtime dictionary.
    /// </summary>
    /// <param name="variableContext">Current typed scalable-stat dictionary.</param>
    /// <param name="variableName">Variable to resolve.</param>
    /// <param name="resolvedValue">Resolved variable value when found.</param>
    /// <returns>True when the variable exists; otherwise false.</returns>
    private static bool TryResolveVariableValue(IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                                string variableName,
                                                out PlayerFormulaValue resolvedValue)
    {
        resolvedValue = PlayerFormulaValue.CreateInvalid();

        if (variableContext == null || string.IsNullOrWhiteSpace(variableName))
            return false;

        return variableContext.TryGetValue(variableName, out resolvedValue);
    }

    /// <summary>
    /// Resolves one named scalable-stat variable from the current numeric runtime dictionary.
    /// </summary>
    /// <param name="variableContext">Current numeric scalable-stat dictionary.</param>
    /// <param name="variableName">Variable to resolve.</param>
    /// <param name="resolvedValue">Resolved variable value when found.</param>
    /// <returns>True when the variable exists; otherwise false.</returns>
    private static bool TryResolveVariableValue(IReadOnlyDictionary<string, float> variableContext,
                                                string variableName,
                                                out float resolvedValue)
    {
        resolvedValue = 0f;

        if (variableContext == null || string.IsNullOrWhiteSpace(variableName))
            return false;

        return variableContext.TryGetValue(variableName, out resolvedValue);
    }
    #endregion

    #endregion
}
