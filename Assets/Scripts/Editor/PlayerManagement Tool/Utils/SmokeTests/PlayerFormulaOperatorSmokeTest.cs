#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Runs a deterministic smoke suite that validates operator precedence and associativity for player scaling formulas.
/// </summary>
public static class PlayerFormulaOperatorSmokeTest
{
    #region Methods
    /// <summary>
    /// Executes the smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        Dictionary<string, float> variables = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        variables["FireRate"] = 8f;
        variables["Speed"] = 3f;
        variables["Damage"] = 20f;

        // Exponent precedence and parenthesized exponent base.
        Validate("[this] + [FireRate]^2", 2f, variables, 66f);
        Validate("([this] + [FireRate])^2", 2f, variables, 100f);
        Validate("[this] * [FireRate]^2", 2f, variables, 128f);
        Validate("[this]^[Speed]^[this]", 2f, variables, 512f);

        // Multiplicative vs additive precedence.
        Validate("[Damage] + [FireRate] * [Speed]", 0f, variables, 44f);
        Validate("([Damage] + [FireRate]) * [Speed]", 0f, variables, 84f);

        // Left associativity for subtraction and division.
        Validate("[Damage] - [FireRate] - [Speed]", 0f, variables, 9f);
        Validate("[Damage] / [FireRate] / [Speed]", 0f, variables, 0.8333333f);

        // Function precedence and nested grouping.
        Validate("log([FireRate]^2)", 0f, variables, Mathf.Log(64f));
        Validate("[this] + log([FireRate]) * [Speed]", 2f, variables, 2f + Mathf.Log(8f) * 3f);
        Validate("saturate([this] - 0.25)", 0.25f, variables, 0f);
        Validate("saturate([this] + 1.5)", 0f, variables, 1f);
        Validate("inverseLerp(2, 10, [FireRate])", 0f, variables, 0.75f);
        Validate("remap(0, 10, 100, 200, [Speed])", 0f, variables, 130f);
        Validate("switch([Speed], 1:10, 3:20, 99)", 0f, variables, 20f);
        Validate("switch([Speed], 1:10, 2:20, 99)", 0f, variables, 99f);

        Debug.Log("[PlayerFormulaOperatorSmokeTest] All operator checks passed.");
    }

    /// <summary>
    /// Evaluates one formula and asserts that the result matches the expected value.
    /// </summary>
    /// <param name="formula">Formula text using [this] and named scalable variables.</param>
    /// <param name="thisValue">Numeric value mapped to [this].</param>
    /// <param name="variables">Available variable context for formula evaluation.</param>
    /// <param name="expected">Expected deterministic result.</param>
    private static void Validate(string formula,
                                 float thisValue,
                                 IReadOnlyDictionary<string, float> variables,
                                 float expected)
    {
        if (!PlayerStatFormulaEngine.TryEvaluate(formula,
                                                 thisValue,
                                                 variables,
                                                 out float result,
                                                 out string errorMessage))
        {
            throw new Exception(string.Format(CultureInfo.InvariantCulture,
                                              "Formula failed: '{0}'. Error: {1}",
                                              formula,
                                              errorMessage));
        }

        if (Mathf.Abs(result - expected) > 0.0001f)
        {
            throw new Exception(string.Format(CultureInfo.InvariantCulture,
                                              "Formula mismatch: '{0}'. Expected: {1}, Actual: {2}",
                                              formula,
                                              expected,
                                              result));
        }
    }
    #endregion
}
#endif
