using System;
using System.Globalization;

/// <summary>
/// Identifies the runtime type stored inside one compiled formula value.
/// </summary>
public enum PlayerFormulaValueType : byte
{
    Invalid = 0,
    Number = 1,
    Boolean = 2,
    Token = 3
}

/// <summary>
/// Stores one typed formula value used by the unified scaling and Character Tuning evaluators.
/// </summary>
public readonly struct PlayerFormulaValue
{
    #region Fields
    public readonly PlayerFormulaValueType Type;
    public readonly float NumberValue;
    public readonly bool BooleanValue;
    public readonly string TokenValue;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable formula value.
    /// </summary>
    /// <param name="typeValue">Stored runtime type.</param>
    /// <param name="numberValue">Numeric payload.</param>
    /// <param name="booleanValue">Boolean payload.</param>
    /// <param name="tokenValue">Token payload.</param>
    /// <returns>Initialized formula value.</returns>
    private PlayerFormulaValue(PlayerFormulaValueType typeValue,
                               float numberValue,
                               bool booleanValue,
                               string tokenValue)
    {
        Type = typeValue;
        NumberValue = numberValue;
        BooleanValue = booleanValue;
        TokenValue = tokenValue ?? string.Empty;
    }
    #endregion

    #region Properties
    public bool IsValid
    {
        get
        {
            return Type != PlayerFormulaValueType.Invalid;
        }
    }
    #endregion

    #region Methods

    #region Factory
    /// <summary>
    /// Creates one numeric formula value.
    /// </summary>
    /// <param name="numberValue">Numeric payload.</param>
    /// <returns>Numeric formula value.</returns>
    public static PlayerFormulaValue CreateNumber(float numberValue)
    {
        return new PlayerFormulaValue(PlayerFormulaValueType.Number,
                                      numberValue,
                                      false,
                                      string.Empty);
    }

    /// <summary>
    /// Creates one boolean formula value.
    /// </summary>
    /// <param name="booleanValue">Boolean payload.</param>
    /// <returns>Boolean formula value.</returns>
    public static PlayerFormulaValue CreateBoolean(bool booleanValue)
    {
        return new PlayerFormulaValue(PlayerFormulaValueType.Boolean,
                                      booleanValue ? 1f : 0f,
                                      booleanValue,
                                      string.Empty);
    }

    /// <summary>
    /// Creates one token formula value.
    /// </summary>
    /// <param name="tokenValue">Token payload.</param>
    /// <returns>Token formula value.</returns>
    public static PlayerFormulaValue CreateToken(string tokenValue)
    {
        return new PlayerFormulaValue(PlayerFormulaValueType.Token,
                                      0f,
                                      false,
                                      string.IsNullOrWhiteSpace(tokenValue) ? string.Empty : tokenValue.Trim());
    }

    /// <summary>
    /// Creates one invalid formula value.
    /// </summary>
    /// <returns>Invalid sentinel value.</returns>
    public static PlayerFormulaValue CreateInvalid()
    {
        return new PlayerFormulaValue(PlayerFormulaValueType.Invalid,
                                      0f,
                                      false,
                                      string.Empty);
    }
    #endregion

    #region Comparison
    /// <summary>
    /// Compares two formula values using type-safe equality semantics.
    /// </summary>
    /// <param name="leftValue">First value.</param>
    /// <param name="rightValue">Second value.</param>
    /// <returns>True when both values are equal.</returns>
    public static bool AreEqual(in PlayerFormulaValue leftValue, in PlayerFormulaValue rightValue)
    {
        if (leftValue.Type != rightValue.Type)
            return false;

        switch (leftValue.Type)
        {
            case PlayerFormulaValueType.Number:
                return Math.Abs(leftValue.NumberValue - rightValue.NumberValue) <= 0.0001f;
            case PlayerFormulaValueType.Boolean:
                return leftValue.BooleanValue == rightValue.BooleanValue;
            case PlayerFormulaValueType.Token:
                return string.Equals(leftValue.TokenValue,
                                     rightValue.TokenValue,
                                     StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }
    #endregion

    #region Formatting
    /// <summary>
    /// Formats the value using invariant culture for logs and editor diagnostics.
    /// </summary>
    /// <returns>Formatted payload text.</returns>
    public string ToDisplayString()
    {
        switch (Type)
        {
            case PlayerFormulaValueType.Number:
                return NumberValue.ToString("0.###", CultureInfo.InvariantCulture);
            case PlayerFormulaValueType.Boolean:
                return BooleanValue ? "true" : "false";
            case PlayerFormulaValueType.Token:
                return "\"" + TokenValue + "\"";
            default:
                return "<invalid>";
        }
    }
    #endregion

    #endregion
}
