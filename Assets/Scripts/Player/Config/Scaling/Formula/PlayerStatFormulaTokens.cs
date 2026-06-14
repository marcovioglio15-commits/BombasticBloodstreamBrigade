/// <summary>
/// Enumerates lexical token categories used by the unified player formula parser.
/// </summary>
internal enum PlayerStatFormulaTokenType
{
    End = 0,
    Number = 1,
    Variable = 2,
    Identifier = 3,
    StringLiteral = 4,
    BooleanLiteral = 5,
    LeftParenthesis = 6,
    RightParenthesis = 7,
    Comma = 8,
    Question = 9,
    Colon = 10,
    Operator = 11
}

/// <summary>
/// Stores one lexical token produced by the player formula lexer.
/// </summary>
internal readonly struct PlayerStatFormulaToken
{
    #region Fields
    public readonly PlayerStatFormulaTokenType Type;
    public readonly string TextValue;
    public readonly float NumberValue;
    public readonly bool BooleanValue;
    public readonly int StartIndex;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one token value with its parsed payload.
    /// </summary>
    /// <param name="typeValue">Lexical token category.</param>
    /// <param name="textValue">String payload when applicable.</param>
    /// <param name="numberValue">Numeric payload when applicable.</param>
    /// <param name="booleanValue">Boolean payload when applicable.</param>
    /// <param name="startIndexValue">Character index where the token starts in the source expression.</param>
    /// <returns>Initialized token value.</returns>
    public PlayerStatFormulaToken(PlayerStatFormulaTokenType typeValue,
                                  string textValue,
                                  float numberValue,
                                  bool booleanValue,
                                  int startIndexValue)
    {
        Type = typeValue;
        TextValue = textValue ?? string.Empty;
        NumberValue = numberValue;
        BooleanValue = booleanValue;
        StartIndex = startIndexValue;
    }
    #endregion
}
