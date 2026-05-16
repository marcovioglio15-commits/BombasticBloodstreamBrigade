using System.Globalization;
using System.Text;
using Unity.Collections;

/// <summary>
/// Centralizes typed scalable-stat conversions shared by bake, runtime scaling, and Character Tuning systems.
/// </summary>
public static class PlayerScalableStatValueUtility
{
    #region Constants
    private const int MaximumFixedTokenUtf8Bytes = 61;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the typed default value stored in one baked scalable-stat blob.
    /// </summary>
    /// <param name="scalableStat">Baked scalable-stat blob.</param>
    /// <returns>Typed default value ready for runtime initialization.</returns>
    public static PlayerFormulaValue ResolveDefaultValue(ref PlayerScalableStatBlob scalableStat)
    {
        PlayerScalableStatType statType = (PlayerScalableStatType)scalableStat.Type;

        switch (statType)
        {
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValue.CreateBoolean(scalableStat.DefaultBooleanValue != 0);
            case PlayerScalableStatType.Token:
                return PlayerFormulaValue.CreateToken(scalableStat.DefaultTokenValue.ToString());
            default:
                return PlayerFormulaValue.CreateNumber(PlayerScalableStatClampUtility.ResolveNormalizedValue(ref scalableStat,
                                                                                                            scalableStat.DefaultValue));
        }
    }

    /// <summary>
    /// Resolves the current typed runtime value stored inside one scalable-stat buffer element.
    /// </summary>
    /// <param name="scalableStat">Runtime scalable-stat buffer element.</param>
    /// <returns>Typed runtime value exposed to the formula engine.</returns>
    public static PlayerFormulaValue ResolveRuntimeValue(in PlayerScalableStatElement scalableStat)
    {
        PlayerScalableStatType statType = (PlayerScalableStatType)scalableStat.Type;

        switch (statType)
        {
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValue.CreateBoolean(scalableStat.BooleanValue != 0);
            case PlayerScalableStatType.Token:
                return PlayerFormulaValue.CreateToken(scalableStat.TokenValue.ToString());
            default:
                return PlayerFormulaValue.CreateNumber(PlayerScalableStatClampUtility.ResolveNormalizedValue(in scalableStat,
                                                                                                            scalableStat.Value));
        }
    }

    /// <summary>
    /// Writes one typed formula result into the provided runtime scalable-stat buffer element.
    /// </summary>
    /// <param name="scalableStat">Mutable runtime scalable-stat buffer element.</param>
    /// <param name="value">Typed formula result to persist.</param>
    /// <param name="errorMessage">Failure reason when the value type is not compatible.</param>
    /// <returns>True when the value is successfully written.</returns>
    public static bool TryWriteRuntimeValue(ref PlayerScalableStatElement scalableStat,
                                            PlayerFormulaValue value,
                                            out string errorMessage)
    {
        errorMessage = string.Empty;
        PlayerScalableStatType statType = (PlayerScalableStatType)scalableStat.Type;

        switch (statType)
        {
            case PlayerScalableStatType.Float:
            case PlayerScalableStatType.Integer:
            case PlayerScalableStatType.Unsigned:
                if (value.Type != PlayerFormulaValueType.Number)
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Stat '{0}' requires a numeric result but formula resolved to {1}.",
                                                 scalableStat.Name.ToString(),
                                                 value.Type);
                    return false;
                }

                scalableStat.Value = PlayerScalableStatClampUtility.ResolveNormalizedValue(in scalableStat, value.NumberValue);
                scalableStat.BooleanValue = 0;
                scalableStat.TokenValue = default;
                return true;
            case PlayerScalableStatType.Boolean:
                if (value.Type != PlayerFormulaValueType.Boolean)
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Stat '{0}' requires a boolean result but formula resolved to {1}.",
                                                 scalableStat.Name.ToString(),
                                                 value.Type);
                    return false;
                }

                scalableStat.Value = value.BooleanValue ? 1f : 0f;
                scalableStat.BooleanValue = value.BooleanValue ? (byte)1 : (byte)0;
                scalableStat.TokenValue = default;
                return true;
            case PlayerScalableStatType.Token:
                if (value.Type != PlayerFormulaValueType.Token)
                {
                    errorMessage = string.Format(CultureInfo.InvariantCulture,
                                                 "Stat '{0}' requires a token result but formula resolved to {1}.",
                                                 scalableStat.Name.ToString(),
                                                 value.Type);
                    return false;
                }

                if (!TryCreateFixedToken(value.TokenValue, out FixedString64Bytes tokenValue, out errorMessage))
                    return false;

                scalableStat.Value = 0f;
                scalableStat.BooleanValue = 0;
                scalableStat.TokenValue = tokenValue;
                return true;
            default:
                errorMessage = string.Format(CultureInfo.InvariantCulture,
                                             "Stat '{0}' has an unsupported scalable-stat type.",
                                             scalableStat.Name.ToString());
                return false;
        }
    }

    /// <summary>
    /// Checks whether the provided runtime scalable-stat currently stores the same logical typed value.
    /// </summary>
    /// <param name="scalableStat">Runtime scalable-stat buffer element.</param>
    /// <param name="value">Comparison value.</param>
    /// <returns>True when both values are logically equivalent.</returns>
    public static bool HasSameRuntimeValue(in PlayerScalableStatElement scalableStat, PlayerFormulaValue value)
    {
        PlayerFormulaValue currentValue = ResolveRuntimeValue(in scalableStat);
        return PlayerFormulaValue.AreEqual(in currentValue, in value);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts one managed token string into the fixed-size runtime storage used by ECS buffers.
    /// </summary>
    /// <param name="tokenText">Managed token text.</param>
    /// <param name="fixedToken">Resolved fixed-size token value when conversion succeeds.</param>
    /// <param name="errorMessage">Failure reason when the token exceeds runtime storage capacity.</param>
    /// <returns>True when conversion succeeds.</returns>
    private static bool TryCreateFixedToken(string tokenText,
                                            out FixedString64Bytes fixedToken,
                                            out string errorMessage)
    {
        fixedToken = default;
        errorMessage = string.Empty;
        string resolvedTokenText = string.IsNullOrWhiteSpace(tokenText)
            ? string.Empty
            : tokenText.Trim();
        int utf8ByteCount = Encoding.UTF8.GetByteCount(resolvedTokenText);

        if (utf8ByteCount > MaximumFixedTokenUtf8Bytes)
        {
            errorMessage = string.Format(CultureInfo.InvariantCulture,
                                         "Token value '{0}' exceeds the runtime FixedString64Bytes capacity.",
                                         resolvedTokenText);
            return false;
        }

        fixedToken = new FixedString64Bytes(resolvedTokenText);
        return true;
    }
    #endregion

    #endregion
}
