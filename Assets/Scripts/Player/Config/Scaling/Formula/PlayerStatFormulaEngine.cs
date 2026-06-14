using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Provides parsing, compilation, and cached lookup for player scaling formulas.
/// </summary>
public static class PlayerStatFormulaEngine
{
    #region Fields
    private static readonly Dictionary<string, PlayerStatFormulaCompileResult> compileCache = new Dictionary<string, PlayerStatFormulaCompileResult>(StringComparer.Ordinal);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles a formula string to an executable token graph.
    /// </summary>
    /// <param name="formula">Formula string using [variables], operators, parentheses and log().</param>
    /// <param name="requireAtLeastOneVariable">When true, formulas with no variable tokens are rejected.</param>
    /// <returns>Compilation result containing either a compiled formula or an error.</returns>
    public static PlayerStatFormulaCompileResult Compile(string formula, bool requireAtLeastOneVariable)
    {
        string normalizedFormula = string.IsNullOrWhiteSpace(formula) ? string.Empty : formula.Trim();
        string cacheKey = string.Format(CultureInfo.InvariantCulture, "{0}|{1}", requireAtLeastOneVariable ? "1" : "0", normalizedFormula);

        if (compileCache.TryGetValue(cacheKey, out PlayerStatFormulaCompileResult cachedResult))
            return cachedResult;

        PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaCompilationUtility.Compile(normalizedFormula, requireAtLeastOneVariable);
        compileCache[cacheKey] = compileResult;
        return compileResult;
    }

    /// <summary>
    /// Evaluates a raw formula string directly, compiling it on first use, using the typed formula context.
    /// </summary>
    /// <param name="formula">Formula expression to evaluate.</param>
    /// <param name="thisValue">Typed value mapped to [this].</param>
    /// <param name="variableValues">Typed variable dictionary for named scalable stats.</param>
    /// <param name="result">Computed typed result when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded, otherwise false.</returns>
    public static bool TryEvaluate(string formula,
                                   PlayerFormulaValue thisValue,
                                   IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                                   out PlayerFormulaValue result,
                                   out string errorMessage,
                                   bool requireAtLeastOneVariable = true)
    {
        PlayerStatFormulaCompileResult compileResult = Compile(formula, requireAtLeastOneVariable);

        if (!compileResult.IsValid || compileResult.CompiledFormula == null)
        {
            result = thisValue;
            errorMessage = compileResult.ErrorMessage;
            return false;
        }

        return compileResult.CompiledFormula.TryEvaluate(thisValue, variableValues, out result, out errorMessage);
    }

    /// <summary>
    /// Evaluates a raw formula string directly, compiling it on first use, expecting a numeric result.
    /// </summary>
    /// <param name="formula">Formula expression to evaluate.</param>
    /// <param name="thisValue">Numeric value mapped to [this].</param>
    /// <param name="variableValues">Typed variable dictionary for named scalable stats.</param>
    /// <param name="result">Computed numeric result when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded, otherwise false.</returns>
    public static bool TryEvaluate(string formula,
                                   float thisValue,
                                   IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                                   out float result,
                                   out string errorMessage,
                                   bool requireAtLeastOneVariable = true)
    {
        PlayerStatFormulaCompileResult compileResult = Compile(formula, requireAtLeastOneVariable);

        if (!compileResult.IsValid || compileResult.CompiledFormula == null)
        {
            result = thisValue;
            errorMessage = compileResult.ErrorMessage;
            return false;
        }

        return compileResult.CompiledFormula.TryEvaluate(thisValue, variableValues, out result, out errorMessage);
    }

    /// <summary>
    /// Evaluates a raw formula string directly against the legacy numeric-only variable dictionary.
    /// </summary>
    /// <param name="formula">Formula expression to evaluate.</param>
    /// <param name="thisValue">Numeric value mapped to [this].</param>
    /// <param name="variableValues">Legacy numeric variable dictionary.</param>
    /// <param name="result">Computed numeric result when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded, otherwise false.</returns>
    public static bool TryEvaluate(string formula,
                                   float thisValue,
                                   IReadOnlyDictionary<string, float> variableValues,
                                   out float result,
                                   out string errorMessage,
                                   bool requireAtLeastOneVariable = true)
    {
        PlayerStatFormulaCompileResult compileResult = Compile(formula, requireAtLeastOneVariable);

        if (!compileResult.IsValid || compileResult.CompiledFormula == null)
        {
            result = thisValue;
            errorMessage = compileResult.ErrorMessage;
            return false;
        }

        return compileResult.CompiledFormula.TryEvaluate(thisValue, variableValues, out result, out errorMessage);
    }
    #endregion

    #endregion
}
