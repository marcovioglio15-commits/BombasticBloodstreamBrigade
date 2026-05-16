using System.Collections.Generic;

/// <summary>
/// Provides editor-side validation helpers for Character Tuning assignment formulas.
/// </summary>
public static class PlayerCharacterTuningFormulaValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one Character Tuning assignment formula using the scalable-stat parser and formula engine.
    /// </summary>
    /// <param name="assignmentFormula">Assignment string entered by designers.</param>
    /// <param name="allowedVariables">Optional scalable-stat whitelist for the current preset scope.</param>
    /// <param name="warningMessage">Failure reason when validation fails.</param>
    /// <returns>True when the assignment is valid.</returns>
    public static bool TryValidateAssignmentFormula(string assignmentFormula,
                                                    ISet<string> allowedVariables,
                                                    IReadOnlyDictionary<string, PlayerFormulaValueType> variableTypes,
                                                    out string warningMessage)
    {
        warningMessage = string.Empty;

        if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(assignmentFormula,
                                                                           out string targetStatName,
                                                                           out string expression,
                                                                           out warningMessage))
        {
            return false;
        }

        if (allowedVariables != null && !allowedVariables.Contains(targetStatName))
        {
            warningMessage = string.Format("Unknown assignment target scalable stat [{0}].", targetStatName);
            return false;
        }

        PlayerFormulaValueType targetType = PlayerFormulaValueType.Invalid;

        if (variableTypes != null && variableTypes.TryGetValue(targetStatName, out PlayerFormulaValueType resolvedTargetType))
            targetType = resolvedTargetType;

        if (targetType == PlayerFormulaValueType.Invalid)
        {
            warningMessage = string.Format("Unknown assignment target type for scalable stat [{0}].", targetStatName);
            return false;
        }

        return PlayerScalingFormulaValidationUtility.TryValidateFormula(expression,
                                                                        allowedVariables,
                                                                        variableTypes,
                                                                        targetType,
                                                                        targetType,
                                                                        out warningMessage,
                                                                        false);
    }
    #endregion

    #endregion
}
