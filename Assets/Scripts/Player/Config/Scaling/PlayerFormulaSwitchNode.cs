using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Implements lazy switch-style branching for formulas using switch(condition, case:value, ..., fallback).
/// </summary>
internal sealed class PlayerFormulaSwitchNode : PlayerFormulaNode
{
    #region Fields
    private readonly PlayerFormulaNode conditionNode;
    private readonly PlayerFormulaNode[] caseNodes;
    private readonly PlayerFormulaNode[] resultNodes;
    private readonly PlayerFormulaNode fallbackNode;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one switch-style formula node.
    /// </summary>
    /// <param name="conditionNodeValue">Condition expression evaluated once and compared against each case expression.</param>
    /// <param name="caseNodesValue">Ordered case expressions.</param>
    /// <param name="resultNodesValue">Ordered result expressions paired with caseNodesValue.</param>
    /// <param name="fallbackNodeValue">Optional fallback expression evaluated only when no case matches.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaSwitchNode(PlayerFormulaNode conditionNodeValue,
                                   PlayerFormulaNode[] caseNodesValue,
                                   PlayerFormulaNode[] resultNodesValue,
                                   PlayerFormulaNode fallbackNodeValue)
    {
        conditionNode = conditionNodeValue;
        caseNodes = caseNodesValue ?? Array.Empty<PlayerFormulaNode>();
        resultNodes = resultNodesValue ?? Array.Empty<PlayerFormulaNode>();
        fallbackNode = fallbackNodeValue;
    }
    #endregion

    #region Methods
    public override bool TryEvaluate(PlayerFormulaValue thisValue,
                                     IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                                     out PlayerFormulaValue result,
                                     out string errorMessage)
    {
        result = PlayerFormulaValue.CreateInvalid();
        errorMessage = string.Empty;

        if (!IsStructureValid(out errorMessage))
            return false;

        if (!conditionNode.TryEvaluate(thisValue, variableValues, out PlayerFormulaValue conditionValue, out errorMessage))
            return false;

        for (int caseIndex = 0; caseIndex < caseNodes.Length; caseIndex++)
        {
            if (!caseNodes[caseIndex].TryEvaluate(thisValue, variableValues, out PlayerFormulaValue caseValue, out errorMessage))
                return false;

            if (!PlayerFormulaNodeUtility.TryRequireComparableValues(conditionValue, caseValue, "switch()", out errorMessage))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "switch() case #{0}: {1}",
                                             caseIndex + 1,
                                             errorMessage);
                return false;
            }

            if (!PlayerFormulaValue.AreEqual(in conditionValue, in caseValue))
                continue;

            return resultNodes[caseIndex].TryEvaluate(thisValue, variableValues, out result, out errorMessage);
        }

        if (fallbackNode != null)
            return fallbackNode.TryEvaluate(thisValue, variableValues, out result, out errorMessage);

        errorMessage = "switch() did not match any case and has no fallback branch.";
        return false;
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (!IsStructureValid(out errorMessage))
            return false;

        if (!conditionNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType conditionType, out errorMessage))
            return false;

        if (conditionType == PlayerFormulaValueType.Invalid)
        {
            errorMessage = "switch() condition resolved to an invalid type.";
            return false;
        }

        PlayerFormulaValueType resolvedBranchType = PlayerFormulaValueType.Invalid;

        for (int caseIndex = 0; caseIndex < caseNodes.Length; caseIndex++)
        {
            if (!caseNodes[caseIndex].TryInferType(thisType, variableTypes, out PlayerFormulaValueType caseType, out errorMessage))
                return false;

            if (caseType != conditionType)
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "switch() case #{0} resolves to {1} but the condition resolves to {2}.",
                                             caseIndex + 1,
                                             caseType,
                                             conditionType);
                return false;
            }

            if (!resultNodes[caseIndex].TryInferType(thisType, variableTypes, out PlayerFormulaValueType branchType, out errorMessage))
                return false;

            if (!TryResolveBranchType(branchType,
                                      "switch()",
                                      caseIndex + 1,
                                      ref resolvedBranchType,
                                      out errorMessage))
            {
                return false;
            }
        }

        if (fallbackNode != null)
        {
            if (!fallbackNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType fallbackType, out errorMessage))
                return false;

            if (!TryResolveBranchType(fallbackType,
                                      "switch() fallback",
                                      0,
                                      ref resolvedBranchType,
                                      out errorMessage))
            {
                return false;
            }
        }

        if (resolvedBranchType == PlayerFormulaValueType.Invalid)
        {
            errorMessage = "switch() requires at least one valid result branch.";
            return false;
        }

        resultType = resolvedBranchType;
        return true;
    }

    private bool IsStructureValid(out string errorMessage)
    {
        errorMessage = string.Empty;

        if (conditionNode == null)
        {
            errorMessage = "switch() is missing its condition expression.";
            return false;
        }

        if (caseNodes == null || resultNodes == null || caseNodes.Length <= 0 || caseNodes.Length != resultNodes.Length)
        {
            errorMessage = "switch() requires one or more case:value branches.";
            return false;
        }

        return true;
    }

    private static bool TryResolveBranchType(PlayerFormulaValueType branchType,
                                             string branchLabel,
                                             int branchIndex,
                                             ref PlayerFormulaValueType resolvedBranchType,
                                             out string errorMessage)
    {
        errorMessage = string.Empty;

        if (branchType == PlayerFormulaValueType.Invalid)
        {
            errorMessage = branchIndex > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} branch #{1} resolves to an invalid type.", branchLabel, branchIndex)
                : string.Format(CultureInfo.InvariantCulture, "{0} resolves to an invalid type.", branchLabel);
            return false;
        }

        if (resolvedBranchType == PlayerFormulaValueType.Invalid)
        {
            resolvedBranchType = branchType;
            return true;
        }

        if (resolvedBranchType == branchType)
            return true;

        errorMessage = branchIndex > 0
            ? string.Format(CultureInfo.InvariantCulture,
                            "switch() branch #{0} resolves to {1} but previous branches resolve to {2}.",
                            branchIndex,
                            branchType,
                            resolvedBranchType)
            : string.Format(CultureInfo.InvariantCulture,
                            "switch() fallback resolves to {0} but previous branches resolve to {1}.",
                            branchType,
                            resolvedBranchType);
        return false;
    }
    #endregion
}
