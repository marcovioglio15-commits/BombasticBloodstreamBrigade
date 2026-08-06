using System;

/// <summary>
/// Validates authored Excel expressions and normalizes them for SpreadsheetML formula elements.
/// </summary>
internal static class ExcelDataFormulaExpressionUtility
{
    #region Constants
    private const int MaximumFormulaLength = 8192;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one authored expression without evaluating or rewriting its formula semantics.
    /// </summary>
    /// <param name="formulaExpression">Authored expression with an optional leading equals sign.</param>
    /// <param name="normalizedExpression">Trimmed SpreadsheetML expression without a leading equals sign.</param>
    /// <param name="warning">Actionable validation warning when the expression cannot be exported.</param>
    /// <returns>True when the expression can be stored in a native Excel formula cell.</returns>
    public static bool TryNormalize(string formulaExpression,
                                    out string normalizedExpression,
                                    out string warning)
    {
        normalizedExpression = formulaExpression == null ? string.Empty : formulaExpression.Trim();

        if (normalizedExpression.StartsWith("=", StringComparison.Ordinal))
            normalizedExpression = normalizedExpression.Substring(1).TrimStart();

        if (string.IsNullOrWhiteSpace(normalizedExpression))
        {
            warning = "Formula cells require a non-empty Excel expression.";
            return false;
        }

        if (normalizedExpression.Length > MaximumFormulaLength)
        {
            warning = "Formula exceeds Excel's 8,192-character expression limit.";
            return false;
        }

        warning = string.Empty;
        return true;
    }

    /// <summary>
    /// Builds the conventional equals-prefixed formula shown by layout previews and workbook sizing.
    /// </summary>
    /// <param name="formulaExpression">Authored expression with an optional leading equals sign.</param>
    /// <returns>Equals-prefixed display text, or the unchanged invalid expression for diagnostics.</returns>
    public static string BuildDisplayExpression(string formulaExpression)
    {
        if (!TryNormalize(formulaExpression, out string normalizedExpression, out string _))
            return formulaExpression ?? string.Empty;

        return "=" + normalizedExpression;
    }
    #endregion

    #endregion
}
