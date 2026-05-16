using System;

/// <summary>
/// Parses assignment-style scalable-stat formulas used by Character Tuning power-up modules.
/// </summary>
public static class PlayerCharacterTuningFormulaUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Splits one Character Tuning assignment into its target scalable stat and right-hand expression.
    /// </summary>
    /// <param name="formula">Raw assignment string entered by designers.</param>
    /// <param name="targetStatName">Parsed left-hand scalable stat name.</param>
    /// <param name="expression">Parsed right-hand mathematical expression.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when the assignment syntax is valid.</returns>
    public static bool TryParseAssignmentFormula(string formula,
                                                 out string targetStatName,
                                                 out string expression,
                                                 out string errorMessage)
    {
        targetStatName = string.Empty;
        expression = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(formula))
        {
            errorMessage = "Assignment formula is empty.";
            return false;
        }

        if (!TryFindAssignmentOperator(formula, out int assignmentIndex, out errorMessage))
            return false;

        string leftHandSide = formula.Substring(0, assignmentIndex).Trim();
        string rightHandSide = formula.Substring(assignmentIndex + 1).Trim();

        if (!TryParseAssignmentTarget(leftHandSide, out targetStatName, out errorMessage))
            return false;

        if (string.IsNullOrWhiteSpace(rightHandSide))
        {
            errorMessage = "Assignment formula is missing the right-hand expression.";
            return false;
        }

        expression = rightHandSide;
        return true;
    }

    /// <summary>
    /// Parses one left-hand assignment token and validates that it targets a named scalable stat.
    /// </summary>
    /// <param name="targetToken">Raw left-hand text before '='.</param>
    /// <param name="targetStatName">Parsed scalable stat name.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when the target token is valid.</returns>
    public static bool TryParseAssignmentTarget(string targetToken,
                                                out string targetStatName,
                                                out string errorMessage)
    {
        targetStatName = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(targetToken))
        {
            errorMessage = "Assignment target is empty.";
            return false;
        }

        string trimmedTargetToken = targetToken.Trim();

        if (!trimmedTargetToken.StartsWith("[", StringComparison.Ordinal) ||
            !trimmedTargetToken.EndsWith("]", StringComparison.Ordinal))
        {
            errorMessage = "Assignment target must use [ScalableStatName] syntax.";
            return false;
        }

        int closingBracketIndex = trimmedTargetToken.IndexOf(']');

        if (closingBracketIndex != trimmedTargetToken.Length - 1)
        {
            errorMessage = "Assignment target must contain exactly one bracketed scalable stat token.";
            return false;
        }

        targetStatName = trimmedTargetToken.Substring(1, trimmedTargetToken.Length - 2).Trim();

        if (string.IsNullOrWhiteSpace(targetStatName))
        {
            errorMessage = "Assignment target cannot be empty.";
            return false;
        }

        if (string.Equals(targetStatName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Assignment target cannot use [this].";
            return false;
        }

        if (!PlayerScalableStatNameUtility.IsValid(targetStatName))
        {
            errorMessage = string.Format("Assignment target [{0}] is not a valid scalable stat name.", targetStatName);
            return false;
        }

        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Finds the assignment operator while ignoring comparison operators and quoted string content.
    /// </summary>
    /// <param name="formula">Raw Character Tuning assignment string.</param>
    /// <param name="assignmentIndex">Zero-based index of the assignment operator.</param>
    /// <param name="errorMessage">Failure reason when no valid assignment operator is found.</param>
    /// <returns>True when exactly one valid assignment operator exists.</returns>
    private static bool TryFindAssignmentOperator(string formula, out int assignmentIndex, out string errorMessage)
    {
        assignmentIndex = -1;
        errorMessage = string.Empty;
        bool insideStringLiteral = false;

        for (int characterIndex = 0; characterIndex < formula.Length; characterIndex++)
        {
            char currentCharacter = formula[characterIndex];

            if (currentCharacter == '"' && !IsEscapedCharacter(formula, characterIndex))
            {
                insideStringLiteral = !insideStringLiteral;
                continue;
            }

            if (insideStringLiteral)
                continue;

            if (currentCharacter != '=')
                continue;

            char previousCharacter = characterIndex > 0 ? formula[characterIndex - 1] : '\0';
            char nextCharacter = characterIndex + 1 < formula.Length ? formula[characterIndex + 1] : '\0';

            if (previousCharacter == '=' ||
                previousCharacter == '!' ||
                previousCharacter == '<' ||
                previousCharacter == '>' ||
                nextCharacter == '=')
            {
                continue;
            }

            if (assignmentIndex >= 0)
            {
                errorMessage = "Assignment formula can contain only one assignment '='.";
                return false;
            }

            assignmentIndex = characterIndex;
        }

        if (insideStringLiteral)
        {
            errorMessage = "Assignment formula contains an unterminated string literal.";
            return false;
        }

        if (assignmentIndex < 0)
        {
            errorMessage = "Assignment formula is missing '='.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves whether the current character is escaped by an odd number of backslashes.
    /// </summary>
    /// <param name="text">Source string being scanned.</param>
    /// <param name="characterIndex">Character index to inspect.</param>
    /// <returns>True when the character is escaped.</returns>
    private static bool IsEscapedCharacter(string text, int characterIndex)
    {
        int backslashCount = 0;

        for (int index = characterIndex - 1; index >= 0; index--)
        {
            if (text[index] != '\\')
                break;

            backslashCount++;
        }

        return (backslashCount & 1) != 0;
    }
    #endregion

    #endregion
}
