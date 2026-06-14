using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Provides tokenization and recursive-descent parsing for unified player formulas.
/// </summary>
internal static class PlayerStatFormulaCompilationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one raw formula string into a reusable typed expression tree.
    /// </summary>
    /// <param name="formula">Raw expression string.</param>
    /// <param name="requireAtLeastOneVariable">True when formulas without [this] or named variables must be rejected.</param>
    /// <returns>Compilation result containing either a compiled formula or a failure reason.</returns>
    internal static PlayerStatFormulaCompileResult Compile(string formula, bool requireAtLeastOneVariable)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return new PlayerStatFormulaCompileResult(false, null, "Formula is empty.");

        if (!Tokenize(formula, out List<PlayerStatFormulaToken> tokens, out string tokenizationError))
            return new PlayerStatFormulaCompileResult(false, null, tokenizationError);

        PlayerFormulaParser parser = new PlayerFormulaParser(tokens);

        if (!parser.TryParse(out PlayerFormulaNode rootNode, out string parseError))
            return new PlayerStatFormulaCompileResult(false, null, parseError);

        if (requireAtLeastOneVariable && parser.VariableNames.Count == 0)
        {
            return new PlayerStatFormulaCompileResult(false,
                                                      null,
                                                      "Formula must reference at least one variable ([this] or [StatName]).");
        }

        string[] variableNames = new string[parser.VariableNames.Count];
        parser.VariableNames.CopyTo(variableNames);
        Array.Sort(variableNames, StringComparer.OrdinalIgnoreCase);
        PlayerCompiledStatFormula compiledFormula = new PlayerCompiledStatFormula(rootNode, variableNames);
        return new PlayerStatFormulaCompileResult(true, compiledFormula, string.Empty);
    }
    #endregion

    #region Tokenization
    /// <summary>
    /// Converts one raw formula string into lexical tokens.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="tokens">Produced token list.</param>
    /// <param name="errorMessage">Failure reason when tokenization fails.</param>
    /// <returns>True when tokenization succeeds.</returns>
    private static bool Tokenize(string formula,
                                 out List<PlayerStatFormulaToken> tokens,
                                 out string errorMessage)
    {
        tokens = new List<PlayerStatFormulaToken>(16);
        errorMessage = string.Empty;
        int index = 0;

        while (index < formula.Length)
        {
            char currentCharacter = formula[index];

            if (char.IsWhiteSpace(currentCharacter))
            {
                index += 1;
                continue;
            }

            if (currentCharacter == '[')
            {
                if (!TryReadVariableToken(formula, ref index, tokens, out errorMessage))
                    return false;

                continue;
            }

            if (currentCharacter == '"')
            {
                if (!TryReadStringLiteralToken(formula, ref index, tokens, out errorMessage))
                    return false;

                continue;
            }

            if (IsNumberStart(formula, index))
            {
                int tokenStartIndex = index;

                if (!TryReadNumberToken(formula, ref index, out float numberValue))
                {
                    errorMessage = "Invalid number literal in formula.";
                    return false;
                }

                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Number,
                                                      string.Empty,
                                                      numberValue,
                                                      false,
                                                      tokenStartIndex));
                continue;
            }

            if (IsIdentifierStart(currentCharacter))
            {
                TryReadIdentifierToken(formula, ref index, tokens);
                continue;
            }

            if (TryReadOperatorOrPunctuationToken(formula, ref index, tokens))
                continue;

            errorMessage = string.Format(CultureInfo.InvariantCulture,
                                         "Unsupported character '{0}' in formula.",
                                         currentCharacter);
            return false;
        }

        tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.End,
                                              string.Empty,
                                              0f,
                                              false,
                                              formula.Length));
        return true;
    }

    /// <summary>
    /// Reads one bracketed variable token.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="index">Current lexer index, advanced after the token.</param>
    /// <param name="tokens">Destination token list.</param>
    /// <param name="errorMessage">Failure reason when tokenization fails.</param>
    /// <returns>True when the variable token is valid.</returns>
    private static bool TryReadVariableToken(string formula,
                                             ref int index,
                                             List<PlayerStatFormulaToken> tokens,
                                             out string errorMessage)
    {
        errorMessage = string.Empty;
        int startIndex = index;
        int closingBracketIndex = formula.IndexOf(']', index + 1);

        if (closingBracketIndex < 0)
        {
            errorMessage = "Variable token is missing closing ']'.";
            return false;
        }

        string variableName = formula.Substring(index + 1, closingBracketIndex - index - 1).Trim();

        if (string.IsNullOrWhiteSpace(variableName))
        {
            errorMessage = "Variable token cannot be empty.";
            return false;
        }

        tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Variable,
                                              variableName,
                                              0f,
                                              false,
                                              startIndex));
        index = closingBracketIndex + 1;
        return true;
    }

    /// <summary>
    /// Reads one string literal token using double quotes and basic escape sequences.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="index">Current lexer index, advanced after the token.</param>
    /// <param name="tokens">Destination token list.</param>
    /// <param name="errorMessage">Failure reason when tokenization fails.</param>
    /// <returns>True when the string literal is valid.</returns>
    private static bool TryReadStringLiteralToken(string formula,
                                                  ref int index,
                                                  List<PlayerStatFormulaToken> tokens,
                                                  out string errorMessage)
    {
        errorMessage = string.Empty;
        int startIndex = index;
        index += 1;
        StringBuilder literalBuilder = new StringBuilder();

        while (index < formula.Length)
        {
            char currentCharacter = formula[index];

            if (currentCharacter == '"')
            {
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.StringLiteral,
                                                      literalBuilder.ToString(),
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            }

            if (currentCharacter == '\\')
            {
                index += 1;

                if (index >= formula.Length)
                {
                    errorMessage = "String literal ends with an incomplete escape sequence.";
                    return false;
                }

                char escapedCharacter = formula[index];

                switch (escapedCharacter)
                {
                    case '\\':
                        literalBuilder.Append('\\');
                        break;
                    case '"':
                        literalBuilder.Append('"');
                        break;
                    case 'n':
                        literalBuilder.Append('\n');
                        break;
                    case 't':
                        literalBuilder.Append('\t');
                        break;
                    default:
                        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                     "Unsupported escape sequence '\\{0}'.",
                                                     escapedCharacter);
                        return false;
                }

                index += 1;
                continue;
            }

            literalBuilder.Append(currentCharacter);
            index += 1;
        }

        errorMessage = "String literal is missing its closing quote.";
        return false;
    }

    /// <summary>
    /// Reads one identifier or boolean literal token.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="index">Current lexer index, advanced after the token.</param>
    /// <param name="tokens">Destination token list.</param>
    /// <returns>Void.</returns>
    private static void TryReadIdentifierToken(string formula, ref int index, List<PlayerStatFormulaToken> tokens)
    {
        int startIndex = index;

        while (index < formula.Length && IsIdentifierPart(formula[index]))
            index += 1;

        string identifier = formula.Substring(startIndex, index - startIndex);

        if (string.Equals(identifier, "true", StringComparison.OrdinalIgnoreCase))
        {
            tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.BooleanLiteral,
                                                  string.Empty,
                                                  0f,
                                                  true,
                                                  startIndex));
            return;
        }

        if (string.Equals(identifier, "false", StringComparison.OrdinalIgnoreCase))
        {
            tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.BooleanLiteral,
                                                  string.Empty,
                                                  0f,
                                                  false,
                                                  startIndex));
            return;
        }

        tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Identifier,
                                              identifier,
                                              0f,
                                              false,
                                              startIndex));
    }

    /// <summary>
    /// Reads one operator or punctuation token.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="index">Current lexer index, advanced after the token.</param>
    /// <param name="tokens">Destination token list.</param>
    /// <returns>True when a token was read from the current position.</returns>
    private static bool TryReadOperatorOrPunctuationToken(string formula,
                                                          ref int index,
                                                          List<PlayerStatFormulaToken> tokens)
    {
        int startIndex = index;
        char currentCharacter = formula[index];

        if (index + 1 < formula.Length)
        {
            string pair = formula.Substring(index, 2);

            switch (pair)
            {
                case "&&":
                case "||":
                case "==":
                case "!=":
                case "<=":
                case ">=":
                    tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Operator,
                                                          pair,
                                                          0f,
                                                          false,
                                                          startIndex));
                    index += 2;
                    return true;
            }
        }

        switch (currentCharacter)
        {
            case '(':
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.LeftParenthesis,
                                                      string.Empty,
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            case ')':
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.RightParenthesis,
                                                      string.Empty,
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            case ',':
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Comma,
                                                      string.Empty,
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            case '?':
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Question,
                                                      string.Empty,
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            case ':':
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Colon,
                                                      string.Empty,
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            case '+':
            case '-':
            case '*':
            case '/':
            case '^':
            case '<':
            case '>':
            case '!':
                tokens.Add(new PlayerStatFormulaToken(PlayerStatFormulaTokenType.Operator,
                                                      currentCharacter.ToString(),
                                                      0f,
                                                      false,
                                                      startIndex));
                index += 1;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether the current position starts a number literal.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="index">Character index being inspected.</param>
    /// <returns>True when the current position starts a number literal.</returns>
    private static bool IsNumberStart(string formula, int index)
    {
        if (index < 0 || index >= formula.Length)
            return false;

        char currentCharacter = formula[index];

        if (char.IsDigit(currentCharacter))
            return true;

        if (currentCharacter == '.')
        {
            int nextIndex = index + 1;
            return nextIndex < formula.Length && char.IsDigit(formula[nextIndex]);
        }

        return false;
    }

    /// <summary>
    /// Reads one number literal.
    /// </summary>
    /// <param name="formula">Source expression string.</param>
    /// <param name="index">Current lexer index, advanced after the token.</param>
    /// <param name="numberValue">Parsed numeric value.</param>
    /// <returns>True when the literal is valid.</returns>
    private static bool TryReadNumberToken(string formula, ref int index, out float numberValue)
    {
        numberValue = 0f;
        int startIndex = index;
        bool hasDecimalSeparator = false;

        while (index < formula.Length)
        {
            char currentCharacter = formula[index];

            if (char.IsDigit(currentCharacter))
            {
                index += 1;
                continue;
            }

            if (currentCharacter == '.')
            {
                if (hasDecimalSeparator)
                    return false;

                hasDecimalSeparator = true;
                index += 1;
                continue;
            }

            break;
        }

        string numberToken = formula.Substring(startIndex, index - startIndex);
        return float.TryParse(numberToken,
                              NumberStyles.Float,
                              CultureInfo.InvariantCulture,
                              out numberValue);
    }

    /// <summary>
    /// Checks whether the character can start an identifier.
    /// </summary>
    /// <param name="character">Character to inspect.</param>
    /// <returns>True when the character starts an identifier.</returns>
    private static bool IsIdentifierStart(char character)
    {
        return char.IsLetter(character) || character == '_';
    }

    /// <summary>
    /// Checks whether the character can continue an identifier.
    /// </summary>
    /// <param name="character">Character to inspect.</param>
    /// <returns>True when the character continues an identifier.</returns>
    private static bool IsIdentifierPart(char character)
    {
        return char.IsLetterOrDigit(character) || character == '_';
    }
    #endregion

    #endregion
}

/// <summary>
/// Recursive-descent parser for unified player formulas.
/// </summary>
internal sealed class PlayerFormulaParser
{
    #region Fields
    private readonly List<PlayerStatFormulaToken> tokens;
    private int currentIndex;
    #endregion

    #region Properties
    public HashSet<string> VariableNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one parser instance for the provided token list.
    /// </summary>
    /// <param name="tokensValue">Lexical token list.</param>
    /// <returns>Initialized parser.</returns>
    public PlayerFormulaParser(List<PlayerStatFormulaToken> tokensValue)
    {
        tokens = tokensValue ?? new List<PlayerStatFormulaToken>();
        currentIndex = 0;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Parses the full expression and validates that no trailing tokens remain.
    /// </summary>
    /// <param name="rootNode">Parsed root node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    public bool TryParse(out PlayerFormulaNode rootNode, out string errorMessage)
    {
        rootNode = null;
        errorMessage = string.Empty;

        if (!TryParseConditional(out rootNode, out errorMessage))
            return false;

        if (CurrentToken.Type == PlayerStatFormulaTokenType.End)
            return true;

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "Unexpected token '{0}' at position {1}.",
                                     DescribeToken(CurrentToken),
                                     CurrentToken.StartIndex);
        return false;
    }
    #endregion

    #region Grammar
    /// <summary>
    /// Parses the ternary conditional production.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseConditional(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseLogicalOr(out node, out errorMessage))
            return false;

        if (!Match(PlayerStatFormulaTokenType.Question))
            return true;

        if (!TryParseConditional(out PlayerFormulaNode whenTrueNode, out errorMessage))
            return false;

        if (!Consume(PlayerStatFormulaTokenType.Colon, "Conditional operator is missing ':' after the true branch.", out errorMessage))
            return false;

        if (!TryParseConditional(out PlayerFormulaNode whenFalseNode, out errorMessage))
            return false;

        node = new PlayerFormulaConditionalNode(node, whenTrueNode, whenFalseNode);
        return true;
    }

    /// <summary>
    /// Parses logical-or expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseLogicalOr(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseLogicalAnd(out node, out errorMessage))
            return false;

        while (MatchOperator("||"))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParseLogicalAnd(out PlayerFormulaNode rightNode, out errorMessage))
                return false;

            node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        }

        return true;
    }

    /// <summary>
    /// Parses logical-and expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseLogicalAnd(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseEquality(out node, out errorMessage))
            return false;

        while (MatchOperator("&&"))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParseEquality(out PlayerFormulaNode rightNode, out errorMessage))
                return false;

            node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        }

        return true;
    }

    /// <summary>
    /// Parses equality expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseEquality(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseComparison(out node, out errorMessage))
            return false;

        while (MatchOperator("==") || MatchOperator("!="))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParseComparison(out PlayerFormulaNode rightNode, out errorMessage))
                return false;

            node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        }

        return true;
    }

    /// <summary>
    /// Parses comparison expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseComparison(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseAddition(out node, out errorMessage))
            return false;

        while (MatchOperator("<") || MatchOperator("<=") || MatchOperator(">") || MatchOperator(">="))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParseAddition(out PlayerFormulaNode rightNode, out errorMessage))
                return false;

            node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        }

        return true;
    }

    /// <summary>
    /// Parses additive expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseAddition(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseMultiplication(out node, out errorMessage))
            return false;

        while (MatchOperator("+") || MatchOperator("-"))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParseMultiplication(out PlayerFormulaNode rightNode, out errorMessage))
                return false;

            node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        }

        return true;
    }

    /// <summary>
    /// Parses multiplicative expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseMultiplication(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParsePower(out node, out errorMessage))
            return false;

        while (MatchOperator("*") || MatchOperator("/"))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParsePower(out PlayerFormulaNode rightNode, out errorMessage))
                return false;

            node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        }

        return true;
    }

    /// <summary>
    /// Parses right-associative exponentiation expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParsePower(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!TryParseUnary(out node, out errorMessage))
            return false;

        if (!MatchOperator("^"))
            return true;

        string operatorText = PreviousToken.TextValue;

        if (!TryParsePower(out PlayerFormulaNode rightNode, out errorMessage))
            return false;

        node = new PlayerFormulaBinaryNode(operatorText, node, rightNode);
        return true;
    }

    /// <summary>
    /// Parses unary expressions.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseUnary(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (MatchOperator("+") || MatchOperator("-") || MatchOperator("!"))
        {
            string operatorText = PreviousToken.TextValue;

            if (!TryParseUnary(out PlayerFormulaNode operandNode, out errorMessage))
                return false;

            node = new PlayerFormulaUnaryNode(operatorText, operandNode);
            return true;
        }

        return TryParsePrimary(out node, out errorMessage);
    }

    /// <summary>
    /// Parses primary expressions and function calls.
    /// </summary>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParsePrimary(out PlayerFormulaNode node, out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (Match(PlayerStatFormulaTokenType.Number))
        {
            node = new PlayerFormulaLiteralNode(PlayerFormulaValue.CreateNumber(PreviousToken.NumberValue));
            return true;
        }

        if (Match(PlayerStatFormulaTokenType.BooleanLiteral))
        {
            node = new PlayerFormulaLiteralNode(PlayerFormulaValue.CreateBoolean(PreviousToken.BooleanValue));
            return true;
        }

        if (Match(PlayerStatFormulaTokenType.StringLiteral))
        {
            node = new PlayerFormulaLiteralNode(PlayerFormulaValue.CreateToken(PreviousToken.TextValue));
            return true;
        }

        if (Match(PlayerStatFormulaTokenType.Variable))
        {
            string variableName = PreviousToken.TextValue;
            VariableNames.Add(variableName);
            node = new PlayerFormulaVariableNode(variableName);
            return true;
        }

        if (Match(PlayerStatFormulaTokenType.Identifier))
            return TryParseFunctionCall(PreviousToken.TextValue, out node, out errorMessage);

        if (Match(PlayerStatFormulaTokenType.LeftParenthesis))
        {
            if (!TryParseConditional(out node, out errorMessage))
                return false;

            return Consume(PlayerStatFormulaTokenType.RightParenthesis,
                           "Missing closing ')' in formula.",
                           out errorMessage);
        }

        errorMessage = string.Format(CultureInfo.InvariantCulture,
                                     "Unexpected token '{0}' at position {1}.",
                                     DescribeToken(CurrentToken),
                                     CurrentToken.StartIndex);
        return false;
    }

    /// <summary>
    /// Parses one function call.
    /// </summary>
    /// <param name="functionName">Function identifier.</param>
    /// <param name="node">Parsed node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseFunctionCall(string functionName,
                                      out PlayerFormulaNode node,
                                      out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (string.Equals(functionName, "switch", StringComparison.OrdinalIgnoreCase))
            return TryParseSwitchFunctionCall(functionName, out node, out errorMessage);

        if (!Consume(PlayerStatFormulaTokenType.LeftParenthesis,
                     string.Format(CultureInfo.InvariantCulture,
                                   "Function '{0}' must be followed by parentheses.",
                                   functionName),
                     out errorMessage))
        {
            return false;
        }

        List<PlayerFormulaNode> arguments = new List<PlayerFormulaNode>();

        if (!Check(PlayerStatFormulaTokenType.RightParenthesis))
        {
            do
            {
                if (!TryParseConditional(out PlayerFormulaNode argumentNode, out errorMessage))
                    return false;

                arguments.Add(argumentNode);
            }
            while (Match(PlayerStatFormulaTokenType.Comma));
        }

        if (!Consume(PlayerStatFormulaTokenType.RightParenthesis,
                     string.Format(CultureInfo.InvariantCulture,
                                   "Function '{0}' is missing its closing ')'.",
                                   functionName),
                     out errorMessage))
        {
            return false;
        }

        node = new PlayerFormulaFunctionNode(functionName, arguments.ToArray());
        return true;
    }

    /// <summary>
    /// Parses one lazy switch() call using switch(condition, case:value, ..., fallback) syntax.
    /// </summary>
    /// <param name="functionName">Function identifier.</param>
    /// <param name="node">Parsed switch node when successful.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private bool TryParseSwitchFunctionCall(string functionName,
                                            out PlayerFormulaNode node,
                                            out string errorMessage)
    {
        node = null;
        errorMessage = string.Empty;

        if (!Consume(PlayerStatFormulaTokenType.LeftParenthesis,
                     string.Format(CultureInfo.InvariantCulture,
                                   "Function '{0}' must be followed by parentheses.",
                                   functionName),
                     out errorMessage))
        {
            return false;
        }

        if (!TryParseConditional(out PlayerFormulaNode conditionNode, out errorMessage))
            return false;

        if (!Consume(PlayerStatFormulaTokenType.Comma,
                     "switch() requires at least one case:value branch after the condition.",
                     out errorMessage))
        {
            return false;
        }

        List<PlayerFormulaNode> caseNodes = new List<PlayerFormulaNode>();
        List<PlayerFormulaNode> resultNodes = new List<PlayerFormulaNode>();
        PlayerFormulaNode fallbackNode = null;

        while (!Check(PlayerStatFormulaTokenType.RightParenthesis))
        {
            if (!TryParseConditional(out PlayerFormulaNode branchSelectorNode, out errorMessage))
                return false;

            if (Match(PlayerStatFormulaTokenType.Colon))
            {
                if (!TryParseConditional(out PlayerFormulaNode branchResultNode, out errorMessage))
                    return false;

                caseNodes.Add(branchSelectorNode);
                resultNodes.Add(branchResultNode);
            }
            else
            {
                if (fallbackNode != null)
                {
                    errorMessage = "switch() can contain only one fallback branch and it must be last.";
                    return false;
                }

                fallbackNode = branchSelectorNode;

                if (Match(PlayerStatFormulaTokenType.Comma))
                {
                    errorMessage = "switch() fallback branch must be the last argument.";
                    return false;
                }

                break;
            }

            if (!Match(PlayerStatFormulaTokenType.Comma))
                break;

            if (Check(PlayerStatFormulaTokenType.RightParenthesis))
            {
                errorMessage = "switch() is missing a branch after the trailing comma.";
                return false;
            }
        }

        if (caseNodes.Count <= 0)
        {
            errorMessage = "switch() requires at least one case:value branch.";
            return false;
        }

        if (!Consume(PlayerStatFormulaTokenType.RightParenthesis,
                     string.Format(CultureInfo.InvariantCulture,
                                   "Function '{0}' is missing its closing ')'.",
                                   functionName),
                     out errorMessage))
        {
            return false;
        }

        node = new PlayerFormulaSwitchNode(conditionNode,
                                           caseNodes.ToArray(),
                                           resultNodes.ToArray(),
                                           fallbackNode);
        return true;
    }
    #endregion

    #region Token Helpers
    /// <summary>
    /// Checks whether the current token matches the requested type.
    /// </summary>
    /// <param name="tokenType">Requested token type.</param>
    /// <returns>True when the current token matches.</returns>
    private bool Check(PlayerStatFormulaTokenType tokenType)
    {
        return CurrentToken.Type == tokenType;
    }

    /// <summary>
    /// Consumes one token of the requested type.
    /// </summary>
    /// <param name="tokenType">Requested token type.</param>
    /// <returns>True when the current token matches and is consumed.</returns>
    private bool Match(PlayerStatFormulaTokenType tokenType)
    {
        if (!Check(tokenType))
            return false;

        Advance();
        return true;
    }

    /// <summary>
    /// Consumes one operator token with the requested text.
    /// </summary>
    /// <param name="operatorText">Requested operator text.</param>
    /// <returns>True when the current operator matches and is consumed.</returns>
    private bool MatchOperator(string operatorText)
    {
        if (CurrentToken.Type != PlayerStatFormulaTokenType.Operator)
            return false;

        if (!string.Equals(CurrentToken.TextValue, operatorText, StringComparison.Ordinal))
            return false;

        Advance();
        return true;
    }

    /// <summary>
    /// Consumes the requested token type or emits the provided parser error.
    /// </summary>
    /// <param name="tokenType">Requested token type.</param>
    /// <param name="failureMessage">Failure reason when the token is missing.</param>
    /// <param name="errorMessage">Failure reason when parsing fails.</param>
    /// <returns>True when the token is consumed.</returns>
    private bool Consume(PlayerStatFormulaTokenType tokenType,
                         string failureMessage,
                         out string errorMessage)
    {
        errorMessage = string.Empty;

        if (Match(tokenType))
            return true;

        errorMessage = failureMessage;
        return false;
    }

    /// <summary>
    /// Advances the parser by one token.
    /// </summary>
    /// <returns>Consumed token.</returns>
    private PlayerStatFormulaToken Advance()
    {
        if (currentIndex < tokens.Count - 1)
            currentIndex += 1;

        return PreviousToken;
    }

    /// <summary>
    /// Formats one token for user-facing error messages.
    /// </summary>
    /// <param name="token">Token to describe.</param>
    /// <returns>Readable token label.</returns>
    private static string DescribeToken(PlayerStatFormulaToken token)
    {
        switch (token.Type)
        {
            case PlayerStatFormulaTokenType.End:
                return "<end>";
            case PlayerStatFormulaTokenType.Number:
                return token.NumberValue.ToString(CultureInfo.InvariantCulture);
            case PlayerStatFormulaTokenType.Variable:
            case PlayerStatFormulaTokenType.Identifier:
            case PlayerStatFormulaTokenType.Operator:
            case PlayerStatFormulaTokenType.StringLiteral:
                return token.TextValue;
            case PlayerStatFormulaTokenType.BooleanLiteral:
                return token.BooleanValue ? "true" : "false";
            case PlayerStatFormulaTokenType.LeftParenthesis:
                return "(";
            case PlayerStatFormulaTokenType.RightParenthesis:
                return ")";
            case PlayerStatFormulaTokenType.Comma:
                return ",";
            case PlayerStatFormulaTokenType.Question:
                return "?";
            case PlayerStatFormulaTokenType.Colon:
                return ":";
            default:
                return token.Type.ToString();
        }
    }
    #endregion

    #region Accessors
    private PlayerStatFormulaToken CurrentToken
    {
        get
        {
            if (tokens == null || tokens.Count == 0)
            {
                return new PlayerStatFormulaToken(PlayerStatFormulaTokenType.End,
                                                  string.Empty,
                                                  0f,
                                                  false,
                                                  0);
            }

            return tokens[currentIndex];
        }
    }

    private PlayerStatFormulaToken PreviousToken
    {
        get
        {
            if (tokens == null || tokens.Count == 0)
            {
                return new PlayerStatFormulaToken(PlayerStatFormulaTokenType.End,
                                                  string.Empty,
                                                  0f,
                                                  false,
                                                  0);
            }

            int previousIndex = currentIndex - 1;

            if (previousIndex < 0)
                previousIndex = 0;

            return tokens[previousIndex];
        }
    }
    #endregion

    #endregion
}
