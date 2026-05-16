using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Represents a compiled typed formula that can be reused across AddScaling and Character Tuning execution paths.
/// </summary>
public sealed class PlayerCompiledStatFormula
{
    #region Fields
    private readonly PlayerFormulaNode rootNode;
    private readonly string[] variableNames;
    #endregion

    #region Properties
    public IReadOnlyList<string> VariableNames
    {
        get
        {
            return variableNames;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one compiled formula wrapper from the parser output.
    /// </summary>
    /// <param name="rootNodeValue">Root expression node.</param>
    /// <param name="variableNamesValue">Distinct variable names referenced by the formula.</param>
    /// <returns>Initialized compiled formula.</returns>
    internal PlayerCompiledStatFormula(PlayerFormulaNode rootNodeValue, string[] variableNamesValue)
    {
        rootNode = rootNodeValue;
        variableNames = variableNamesValue ?? Array.Empty<string>();
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Evaluates the compiled typed formula using the provided typed context.
    /// </summary>
    /// <param name="thisValue">Typed value mapped to the reserved [this] token.</param>
    /// <param name="variableValues">Current typed variable context.</param>
    /// <param name="result">Resolved typed value when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeds.</returns>
    public bool TryEvaluate(PlayerFormulaValue thisValue,
                            IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                            out PlayerFormulaValue result,
                            out string errorMessage)
    {
        result = PlayerFormulaValue.CreateInvalid();
        errorMessage = string.Empty;

        if (rootNode == null)
        {
            errorMessage = "Formula has no executable root node.";
            return false;
        }

        return rootNode.TryEvaluate(thisValue,
                                    variableValues,
                                    out result,
                                    out errorMessage);
    }

    /// <summary>
    /// Evaluates the compiled formula expecting a numeric result.
    /// </summary>
    /// <param name="thisValue">Numeric value mapped to [this].</param>
    /// <param name="variableValues">Typed variable context.</param>
    /// <param name="result">Resolved numeric result when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeds with a numeric result.</returns>
    public bool TryEvaluate(float thisValue,
                            IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                            out float result,
                            out string errorMessage)
    {
        result = thisValue;

        if (!TryEvaluate(PlayerFormulaValue.CreateNumber(thisValue),
                         variableValues,
                         out PlayerFormulaValue evaluatedValue,
                         out errorMessage))
        {
            return false;
        }

        if (evaluatedValue.Type != PlayerFormulaValueType.Number)
        {
            errorMessage = string.Format(CultureInfo.InvariantCulture,
                                         "Formula resolved to {0} but a numeric result was required.",
                                         evaluatedValue.Type);
            return false;
        }

        result = evaluatedValue.NumberValue;
        return true;
    }

    /// <summary>
    /// Evaluates the compiled formula against the legacy numeric-only variable dictionary.
    /// </summary>
    /// <param name="thisValue">Numeric value mapped to [this].</param>
    /// <param name="variableValues">Legacy numeric variable context.</param>
    /// <param name="result">Resolved numeric result when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeds.</returns>
    public bool TryEvaluate(float thisValue,
                            IReadOnlyDictionary<string, float> variableValues,
                            out float result,
                            out string errorMessage)
    {
        Dictionary<string, PlayerFormulaValue> typedContext = null;

        if (variableValues != null)
        {
            typedContext = new Dictionary<string, PlayerFormulaValue>(variableValues.Count, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, float> entry in variableValues)
                typedContext[entry.Key] = PlayerFormulaValue.CreateNumber(entry.Value);
        }

        return TryEvaluate(thisValue, typedContext, out result, out errorMessage);
    }

    /// <summary>
    /// Infers the result type of the compiled formula using the known variable types for the current authoring scope.
    /// </summary>
    /// <param name="thisType">Type bound to the reserved [this] token.</param>
    /// <param name="variableTypes">Known types for scoped variables.</param>
    /// <param name="resultType">Resolved result type when inference succeeds.</param>
    /// <param name="errorMessage">Failure reason when type inference fails.</param>
    /// <returns>True when type inference succeeds.</returns>
    public bool TryInferResultType(PlayerFormulaValueType thisType,
                                   IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                   out PlayerFormulaValueType resultType,
                                   out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (rootNode == null)
        {
            errorMessage = "Formula has no executable root node.";
            return false;
        }

        return rootNode.TryInferType(thisType,
                                     variableTypes,
                                     out resultType,
                                     out errorMessage);
    }
    #endregion

    #endregion
}

/// <summary>
/// Base node for the unified compiled formula tree.
/// </summary>
internal abstract class PlayerFormulaNode
{
    #region Methods
    public abstract bool TryEvaluate(PlayerFormulaValue thisValue,
                                     IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                                     out PlayerFormulaValue result,
                                     out string errorMessage);

    public abstract bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage);
    #endregion
}

/// <summary>
/// Literal AST node.
/// </summary>
internal sealed class PlayerFormulaLiteralNode : PlayerFormulaNode
{
    #region Fields
    private readonly PlayerFormulaValue value;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one literal node.
    /// </summary>
    /// <param name="valueValue">Literal payload.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaLiteralNode(PlayerFormulaValue valueValue)
    {
        value = valueValue;
    }
    #endregion

    #region Methods
    public override bool TryEvaluate(PlayerFormulaValue thisValue,
                                     IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                                     out PlayerFormulaValue result,
                                     out string errorMessage)
    {
        result = value;
        errorMessage = string.Empty;
        return true;
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = value.Type;
        errorMessage = string.Empty;
        return value.IsValid;
    }
    #endregion
}

/// <summary>
/// Variable AST node.
/// </summary>
internal sealed class PlayerFormulaVariableNode : PlayerFormulaNode
{
    #region Fields
    private readonly string variableName;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one variable node.
    /// </summary>
    /// <param name="variableNameValue">Referenced variable name.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaVariableNode(string variableNameValue)
    {
        variableName = string.IsNullOrWhiteSpace(variableNameValue)
            ? string.Empty
            : variableNameValue.Trim();
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

        if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
        {
            result = thisValue;
            return true;
        }

        if (variableValues == null)
        {
            errorMessage = string.Format(CultureInfo.InvariantCulture,
                                         "Unknown variable [{0}].",
                                         variableName);
            return false;
        }

        if (variableValues.TryGetValue(variableName, out PlayerFormulaValue variableValue))
        {
            result = variableValue;
            return true;
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "Unknown variable [{0}].",
                                     variableName);
        return false;
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
        {
            resultType = thisType;
            return thisType != PlayerFormulaValueType.Invalid;
        }

        if (variableTypes != null && variableTypes.TryGetValue(variableName, out PlayerFormulaValueType variableType))
        {
            resultType = variableType;
            return variableType != PlayerFormulaValueType.Invalid;
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "Unknown variable [{0}].",
                                     variableName);
        return false;
    }
    #endregion
}

/// <summary>
/// Unary-operation AST node.
/// </summary>
internal sealed class PlayerFormulaUnaryNode : PlayerFormulaNode
{
    #region Fields
    private readonly string operatorText;
    private readonly PlayerFormulaNode operandNode;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one unary-operation node.
    /// </summary>
    /// <param name="operatorTextValue">Unary operator text.</param>
    /// <param name="operandNodeValue">Operand expression node.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaUnaryNode(string operatorTextValue, PlayerFormulaNode operandNodeValue)
    {
        operatorText = operatorTextValue ?? string.Empty;
        operandNode = operandNodeValue;
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

        if (operandNode == null)
        {
            errorMessage = "Unary operator is missing its operand.";
            return false;
        }

        if (!operandNode.TryEvaluate(thisValue, variableValues, out PlayerFormulaValue operandValue, out errorMessage))
            return false;

        switch (operatorText)
        {
            case "+":
                if (!PlayerFormulaNodeUtility.TryRequireNumber(operandValue, operatorText, out float positiveOperand, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateNumber(positiveOperand);
                return true;
            case "-":
                if (!PlayerFormulaNodeUtility.TryRequireNumber(operandValue, operatorText, out float negativeOperand, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateNumber(-negativeOperand);
                return true;
            case "!":
                if (!PlayerFormulaNodeUtility.TryRequireBoolean(operandValue, operatorText, out bool negatedOperand, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(!negatedOperand);
                return true;
            default:
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "Unsupported unary operator '{0}'.",
                                             operatorText);
                return false;
        }
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (operandNode == null)
        {
            errorMessage = "Unary operator is missing its operand.";
            return false;
        }

        if (!operandNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType operandType, out errorMessage))
            return false;

        switch (operatorText)
        {
            case "+":
            case "-":
                if (operandType != PlayerFormulaValueType.Number)
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Unary operator '{0}' requires a numeric operand.",
                                                 operatorText);
                    return false;
                }

                resultType = PlayerFormulaValueType.Number;
                return true;
            case "!":
                if (operandType != PlayerFormulaValueType.Boolean)
                {
                    errorMessage = "Logical not requires a boolean operand.";
                    return false;
                }

                resultType = PlayerFormulaValueType.Boolean;
                return true;
            default:
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "Unsupported unary operator '{0}'.",
                                             operatorText);
                return false;
        }
    }
    #endregion
}

/// <summary>
/// Binary-operation AST node.
/// </summary>
internal sealed class PlayerFormulaBinaryNode : PlayerFormulaNode
{
    #region Fields
    private readonly string operatorText;
    private readonly PlayerFormulaNode leftNode;
    private readonly PlayerFormulaNode rightNode;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one binary-operation node.
    /// </summary>
    /// <param name="operatorTextValue">Binary operator text.</param>
    /// <param name="leftNodeValue">Left operand node.</param>
    /// <param name="rightNodeValue">Right operand node.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaBinaryNode(string operatorTextValue,
                                   PlayerFormulaNode leftNodeValue,
                                   PlayerFormulaNode rightNodeValue)
    {
        operatorText = operatorTextValue ?? string.Empty;
        leftNode = leftNodeValue;
        rightNode = rightNodeValue;
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

        if (leftNode == null || rightNode == null)
        {
            errorMessage = "Binary operator is missing one operand.";
            return false;
        }

        if (!leftNode.TryEvaluate(thisValue, variableValues, out PlayerFormulaValue leftValue, out errorMessage))
            return false;

        if (!rightNode.TryEvaluate(thisValue, variableValues, out PlayerFormulaValue rightValue, out errorMessage))
            return false;

        switch (operatorText)
        {
            case "+":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float addLeft, out float addRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateNumber(addLeft + addRight);
                return true;
            case "-":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float subtractLeft, out float subtractRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateNumber(subtractLeft - subtractRight);
                return true;
            case "*":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float multiplyLeft, out float multiplyRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateNumber(multiplyLeft * multiplyRight);
                return true;
            case "/":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float divideLeft, out float divideRight, out errorMessage))
                    return false;

                if (Mathf.Abs(divideRight) <= 0.000001f)
                {
                    errorMessage = "Division by zero is not allowed.";
                    return false;
                }

                result = PlayerFormulaValue.CreateNumber(divideLeft / divideRight);
                return true;
            case "^":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float powLeft, out float powRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateNumber(Mathf.Pow(powLeft, powRight));
                return true;
            case "<":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float lessLeft, out float lessRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(lessLeft < lessRight);
                return true;
            case "<=":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float lessEqualLeft, out float lessEqualRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(lessEqualLeft <= lessEqualRight);
                return true;
            case ">":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float greaterLeft, out float greaterRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(greaterLeft > greaterRight);
                return true;
            case ">=":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryNumbers(leftValue, rightValue, operatorText, out float greaterEqualLeft, out float greaterEqualRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(greaterEqualLeft >= greaterEqualRight);
                return true;
            case "==":
                if (!PlayerFormulaNodeUtility.TryRequireComparableValues(leftValue, rightValue, operatorText, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(PlayerFormulaValue.AreEqual(in leftValue, in rightValue));
                return true;
            case "!=":
                if (!PlayerFormulaNodeUtility.TryRequireComparableValues(leftValue, rightValue, operatorText, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(!PlayerFormulaValue.AreEqual(in leftValue, in rightValue));
                return true;
            case "&&":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryBooleans(leftValue, rightValue, operatorText, out bool andLeft, out bool andRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(andLeft && andRight);
                return true;
            case "||":
                if (!PlayerFormulaNodeUtility.TryRequireBinaryBooleans(leftValue, rightValue, operatorText, out bool orLeft, out bool orRight, out errorMessage))
                    return false;

                result = PlayerFormulaValue.CreateBoolean(orLeft || orRight);
                return true;
            default:
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "Unsupported binary operator '{0}'.",
                                             operatorText);
                return false;
        }
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (leftNode == null || rightNode == null)
        {
            errorMessage = "Binary operator is missing one operand.";
            return false;
        }

        if (!leftNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType leftType, out errorMessage))
            return false;

        if (!rightNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType rightType, out errorMessage))
            return false;

        switch (operatorText)
        {
            case "+":
            case "-":
            case "*":
            case "/":
            case "^":
                if (!PlayerFormulaNodeUtility.AreBothNumbers(leftType, rightType))
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Operator '{0}' requires numeric operands.",
                                                 operatorText);
                    return false;
                }

                resultType = PlayerFormulaValueType.Number;
                return true;
            case "<":
            case "<=":
            case ">":
            case ">=":
                if (!PlayerFormulaNodeUtility.AreBothNumbers(leftType, rightType))
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Comparison operator '{0}' requires numeric operands.",
                                                 operatorText);
                    return false;
                }

                resultType = PlayerFormulaValueType.Boolean;
                return true;
            case "==":
            case "!=":
                if (leftType == PlayerFormulaValueType.Invalid || rightType == PlayerFormulaValueType.Invalid)
                {
                    errorMessage = "Equality comparison requires valid operand types.";
                    return false;
                }

                if (leftType != rightType)
                {
                    errorMessage = "Equality comparison requires operands of the same type.";
                    return false;
                }

                resultType = PlayerFormulaValueType.Boolean;
                return true;
            case "&&":
            case "||":
                if (leftType != PlayerFormulaValueType.Boolean || rightType != PlayerFormulaValueType.Boolean)
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Logical operator '{0}' requires boolean operands.",
                                                 operatorText);
                    return false;
                }

                resultType = PlayerFormulaValueType.Boolean;
                return true;
            default:
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "Unsupported binary operator '{0}'.",
                                             operatorText);
                return false;
        }
    }
    #endregion
}

/// <summary>
/// Function-call AST node.
/// </summary>
internal sealed class PlayerFormulaFunctionNode : PlayerFormulaNode
{
    #region Fields
    private readonly string functionName;
    private readonly PlayerFormulaNode[] argumentNodes;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one function-call node.
    /// </summary>
    /// <param name="functionNameValue">Function identifier.</param>
    /// <param name="argumentNodesValue">Ordered argument nodes.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaFunctionNode(string functionNameValue, PlayerFormulaNode[] argumentNodesValue)
    {
        functionName = string.IsNullOrWhiteSpace(functionNameValue)
            ? string.Empty
            : functionNameValue.Trim();
        argumentNodes = argumentNodesValue ?? Array.Empty<PlayerFormulaNode>();
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

        if (string.Equals(functionName, "select", StringComparison.OrdinalIgnoreCase))
            return EvaluateSelect(thisValue, variableValues, out result, out errorMessage);

        PlayerFormulaValue[] argumentValues = new PlayerFormulaValue[argumentNodes.Length];

        for (int argumentIndex = 0; argumentIndex < argumentNodes.Length; argumentIndex++)
        {
            if (!argumentNodes[argumentIndex].TryEvaluate(thisValue,
                                                          variableValues,
                                                          out argumentValues[argumentIndex],
                                                          out errorMessage))
            {
                return false;
            }
        }

        return PlayerFormulaFunctionUtility.TryEvaluate(functionName,
                                                       argumentValues,
                                                       out result,
                                                       out errorMessage);
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (string.Equals(functionName, "select", StringComparison.OrdinalIgnoreCase))
            return InferSelectType(thisType, variableTypes, out resultType, out errorMessage);

        PlayerFormulaValueType[] argumentTypes = new PlayerFormulaValueType[argumentNodes.Length];

        for (int argumentIndex = 0; argumentIndex < argumentNodes.Length; argumentIndex++)
        {
            if (!argumentNodes[argumentIndex].TryInferType(thisType,
                                                           variableTypes,
                                                           out argumentTypes[argumentIndex],
                                                           out errorMessage))
            {
                return false;
            }
        }

        return PlayerFormulaFunctionUtility.TryInferType(functionName,
                                                        argumentTypes,
                                                        out resultType,
                                                        out errorMessage);
    }

    /// <summary>
    /// Evaluates the special lazy select() function.
    /// </summary>
    /// <param name="thisValue">Typed [this] value.</param>
    /// <param name="variableValues">Current typed variable context.</param>
    /// <param name="result">Resolved selected branch result.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeds.</returns>
    private bool EvaluateSelect(PlayerFormulaValue thisValue,
                                IReadOnlyDictionary<string, PlayerFormulaValue> variableValues,
                                out PlayerFormulaValue result,
                                out string errorMessage)
    {
        result = PlayerFormulaValue.CreateInvalid();
        errorMessage = string.Empty;

        if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentNodes.Length, 3, out errorMessage))
            return false;

        if (!argumentNodes[0].TryEvaluate(thisValue, variableValues, out PlayerFormulaValue conditionValue, out errorMessage))
            return false;

        if (!PlayerFormulaNodeUtility.TryRequireBoolean(conditionValue, functionName, out bool condition, out errorMessage))
            return false;

        return condition
            ? argumentNodes[1].TryEvaluate(thisValue, variableValues, out result, out errorMessage)
            : argumentNodes[2].TryEvaluate(thisValue, variableValues, out result, out errorMessage);
    }

    /// <summary>
    /// Infers the return type of the special lazy select() function.
    /// </summary>
    /// <param name="thisType">Type bound to [this].</param>
    /// <param name="variableTypes">Known scoped variable types.</param>
    /// <param name="resultType">Resolved result type.</param>
    /// <param name="errorMessage">Failure reason when inference fails.</param>
    /// <returns>True when inference succeeds.</returns>
    private bool InferSelectType(PlayerFormulaValueType thisType,
                                 IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                 out PlayerFormulaValueType resultType,
                                 out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (!PlayerFormulaNodeUtility.TryRequireFunctionArgumentCount(functionName, argumentNodes.Length, 3, out errorMessage))
            return false;

        if (!argumentNodes[0].TryInferType(thisType, variableTypes, out PlayerFormulaValueType conditionType, out errorMessage))
            return false;

        if (conditionType != PlayerFormulaValueType.Boolean)
        {
            errorMessage = "select() requires a boolean condition.";
            return false;
        }

        if (!argumentNodes[1].TryInferType(thisType, variableTypes, out PlayerFormulaValueType whenTrueType, out errorMessage))
            return false;

        if (!argumentNodes[2].TryInferType(thisType, variableTypes, out PlayerFormulaValueType whenFalseType, out errorMessage))
            return false;

        if (whenTrueType != whenFalseType)
        {
            errorMessage = "select() requires matching branch result types.";
            return false;
        }

        resultType = whenTrueType;
        return true;
    }
    #endregion
}

/// <summary>
/// Conditional AST node.
/// </summary>
internal sealed class PlayerFormulaConditionalNode : PlayerFormulaNode
{
    #region Fields
    private readonly PlayerFormulaNode conditionNode;
    private readonly PlayerFormulaNode whenTrueNode;
    private readonly PlayerFormulaNode whenFalseNode;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one conditional node.
    /// </summary>
    /// <param name="conditionNodeValue">Condition expression node.</param>
    /// <param name="whenTrueNodeValue">True branch expression node.</param>
    /// <param name="whenFalseNodeValue">False branch expression node.</param>
    /// <returns>Initialized node.</returns>
    public PlayerFormulaConditionalNode(PlayerFormulaNode conditionNodeValue,
                                        PlayerFormulaNode whenTrueNodeValue,
                                        PlayerFormulaNode whenFalseNodeValue)
    {
        conditionNode = conditionNodeValue;
        whenTrueNode = whenTrueNodeValue;
        whenFalseNode = whenFalseNodeValue;
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

        if (conditionNode == null || whenTrueNode == null || whenFalseNode == null)
        {
            errorMessage = "Conditional operator is missing one branch.";
            return false;
        }

        if (!conditionNode.TryEvaluate(thisValue, variableValues, out PlayerFormulaValue conditionValue, out errorMessage))
            return false;

        if (!PlayerFormulaNodeUtility.TryRequireBoolean(conditionValue, "?:", out bool condition, out errorMessage))
            return false;

        return condition
            ? whenTrueNode.TryEvaluate(thisValue, variableValues, out result, out errorMessage)
            : whenFalseNode.TryEvaluate(thisValue, variableValues, out result, out errorMessage);
    }

    public override bool TryInferType(PlayerFormulaValueType thisType,
                                      IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                      out PlayerFormulaValueType resultType,
                                      out string errorMessage)
    {
        resultType = PlayerFormulaValueType.Invalid;
        errorMessage = string.Empty;

        if (conditionNode == null || whenTrueNode == null || whenFalseNode == null)
        {
            errorMessage = "Conditional operator is missing one branch.";
            return false;
        }

        if (!conditionNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType conditionType, out errorMessage))
            return false;

        if (conditionType != PlayerFormulaValueType.Boolean)
        {
            errorMessage = "Conditional operator requires a boolean condition.";
            return false;
        }

        if (!whenTrueNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType whenTrueType, out errorMessage))
            return false;

        if (!whenFalseNode.TryInferType(thisType, variableTypes, out PlayerFormulaValueType whenFalseType, out errorMessage))
            return false;

        if (whenTrueType != whenFalseType)
        {
            errorMessage = "Conditional operator requires matching branch result types.";
            return false;
        }

        resultType = whenTrueType;
        return true;
    }
    #endregion
}

/// <summary>
/// Shared evaluation and type-check helpers for formula AST nodes.
/// </summary>
internal static class PlayerFormulaNodeUtility
{
    #region Methods

    #region Type Checks
    /// <summary>
    /// Checks whether both operand types are numeric.
    /// </summary>
    /// <param name="leftType">Left operand type.</param>
    /// <param name="rightType">Right operand type.</param>
    /// <returns>True when both types are numeric.</returns>
    public static bool AreBothNumbers(PlayerFormulaValueType leftType, PlayerFormulaValueType rightType)
    {
        return leftType == PlayerFormulaValueType.Number && rightType == PlayerFormulaValueType.Number;
    }

    /// <summary>
    /// Checks whether all provided types are numeric.
    /// </summary>
    /// <param name="types">Types to inspect.</param>
    /// <returns>True when every type is numeric.</returns>
    public static bool AreAllNumbers(IReadOnlyList<PlayerFormulaValueType> types)
    {
        if (types == null)
            return false;

        for (int index = 0; index < types.Count; index++)
        {
            if (types[index] != PlayerFormulaValueType.Number)
                return false;
        }

        return true;
    }
    #endregion

    #region Runtime Validation
    /// <summary>
    /// Validates that one function call received the expected argument count.
    /// </summary>
    /// <param name="functionName">Function identifier.</param>
    /// <param name="actualCount">Actual number of arguments.</param>
    /// <param name="expectedCount">Required number of arguments.</param>
    /// <param name="errorMessage">Failure reason when validation fails.</param>
    /// <returns>True when the count is valid.</returns>
    public static bool TryRequireFunctionArgumentCount(string functionName,
                                                       int actualCount,
                                                       int expectedCount,
                                                       out string errorMessage)
    {
        errorMessage = string.Empty;

        if (actualCount == expectedCount)
            return true;

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "{0}() requires {1} argument(s) but received {2}.",
                                     functionName,
                                     expectedCount,
                                     actualCount);
        return false;
    }

    /// <summary>
    /// Validates that one value is numeric and returns the numeric payload.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <param name="contextLabel">Operator or function name used in the error message.</param>
    /// <param name="numberValue">Resolved numeric payload.</param>
    /// <param name="errorMessage">Failure reason when validation fails.</param>
    /// <returns>True when the value is numeric.</returns>
    public static bool TryRequireNumber(PlayerFormulaValue value,
                                        string contextLabel,
                                        out float numberValue,
                                        out string errorMessage)
    {
        numberValue = 0f;
        errorMessage = string.Empty;

        if (value.Type == PlayerFormulaValueType.Number)
        {
            numberValue = value.NumberValue;
            return true;
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "{0} requires a numeric operand but received {1}.",
                                     contextLabel,
                                     value.Type);
        return false;
    }

    /// <summary>
    /// Validates that one value is boolean and returns the boolean payload.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <param name="contextLabel">Operator or function name used in the error message.</param>
    /// <param name="booleanValue">Resolved boolean payload.</param>
    /// <param name="errorMessage">Failure reason when validation fails.</param>
    /// <returns>True when the value is boolean.</returns>
    public static bool TryRequireBoolean(PlayerFormulaValue value,
                                         string contextLabel,
                                         out bool booleanValue,
                                         out string errorMessage)
    {
        booleanValue = false;
        errorMessage = string.Empty;

        if (value.Type == PlayerFormulaValueType.Boolean)
        {
            booleanValue = value.BooleanValue;
            return true;
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "{0} requires a boolean operand but received {1}.",
                                     contextLabel,
                                     value.Type);
        return false;
    }

    /// <summary>
    /// Validates that two values are numeric and returns both payloads.
    /// </summary>
    /// <param name="leftValue">Left operand value.</param>
    /// <param name="rightValue">Right operand value.</param>
    /// <param name="contextLabel">Operator or function name used in the error message.</param>
    /// <param name="resolvedLeftValue">Resolved left numeric payload.</param>
    /// <param name="resolvedRightValue">Resolved right numeric payload.</param>
    /// <param name="errorMessage">Failure reason when validation fails.</param>
    /// <returns>True when both values are numeric.</returns>
    public static bool TryRequireBinaryNumbers(PlayerFormulaValue leftValue,
                                               PlayerFormulaValue rightValue,
                                               string contextLabel,
                                               out float resolvedLeftValue,
                                               out float resolvedRightValue,
                                               out string errorMessage)
    {
        resolvedLeftValue = 0f;
        resolvedRightValue = 0f;
        errorMessage = string.Empty;

        if (!TryRequireNumber(leftValue, contextLabel, out resolvedLeftValue, out errorMessage))
            return false;

        if (!TryRequireNumber(rightValue, contextLabel, out resolvedRightValue, out errorMessage))
            return false;

        return true;
    }

    /// <summary>
    /// Validates that two values are boolean and returns both payloads.
    /// </summary>
    /// <param name="leftValue">Left operand value.</param>
    /// <param name="rightValue">Right operand value.</param>
    /// <param name="contextLabel">Operator or function name used in the error message.</param>
    /// <param name="resolvedLeftValue">Resolved left boolean payload.</param>
    /// <param name="resolvedRightValue">Resolved right boolean payload.</param>
    /// <param name="errorMessage">Failure reason when validation fails.</param>
    /// <returns>True when both values are boolean.</returns>
    public static bool TryRequireBinaryBooleans(PlayerFormulaValue leftValue,
                                                PlayerFormulaValue rightValue,
                                                string contextLabel,
                                                out bool resolvedLeftValue,
                                                out bool resolvedRightValue,
                                                out string errorMessage)
    {
        resolvedLeftValue = false;
        resolvedRightValue = false;
        errorMessage = string.Empty;

        if (!TryRequireBoolean(leftValue, contextLabel, out resolvedLeftValue, out errorMessage))
            return false;

        if (!TryRequireBoolean(rightValue, contextLabel, out resolvedRightValue, out errorMessage))
            return false;

        return true;
    }

    /// <summary>
    /// Validates that two values can be compared for equality.
    /// </summary>
    /// <param name="leftValue">Left operand value.</param>
    /// <param name="rightValue">Right operand value.</param>
    /// <param name="contextLabel">Operator name used in the error message.</param>
    /// <param name="errorMessage">Failure reason when validation fails.</param>
    /// <returns>True when both values have the same valid type.</returns>
    public static bool TryRequireComparableValues(PlayerFormulaValue leftValue,
                                                  PlayerFormulaValue rightValue,
                                                  string contextLabel,
                                                  out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!leftValue.IsValid || !rightValue.IsValid)
        {
            errorMessage = string.Format(CultureInfo.InvariantCulture,
                                         "{0} requires valid operands.",
                                         contextLabel);
            return false;
        }

        if (leftValue.Type == rightValue.Type)
            return true;

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "{0} requires operands of the same type but received {1} and {2}.",
                                     contextLabel,
                                     leftValue.Type,
                                     rightValue.Type);
        return false;
    }
    #endregion

    #endregion
}

/// <summary>
/// Represents formula compilation output, including either a compiled formula or a failure reason.
/// </summary>
public readonly struct PlayerStatFormulaCompileResult
{
    #region Fields
    public readonly bool IsValid;
    public readonly PlayerCompiledStatFormula CompiledFormula;
    public readonly string ErrorMessage;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one compile result value.
    /// </summary>
    /// <param name="isValidValue">Compilation success flag.</param>
    /// <param name="compiledFormulaValue">Compiled formula when successful.</param>
    /// <param name="errorMessageValue">Failure reason when compilation fails.</param>
    /// <returns>Initialized compile result.</returns>
    public PlayerStatFormulaCompileResult(bool isValidValue,
                                          PlayerCompiledStatFormula compiledFormulaValue,
                                          string errorMessageValue)
    {
        IsValid = isValidValue;
        CompiledFormula = compiledFormulaValue;
        ErrorMessage = errorMessageValue ?? string.Empty;
    }
    #endregion
}
