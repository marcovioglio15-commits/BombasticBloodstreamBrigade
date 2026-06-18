using System;
using UnityEngine;

/// <summary>
/// Centralizes scalable-stat clamp and integer normalization rules shared by bake and runtime systems.
/// </summary>
public static class PlayerScalableStatClampUtility
{
    #region Constants
    public const float DefaultMinimumValue = -1000000f;
    public const float DefaultMaximumValue = 1000000f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one normalized numeric scalable-stat value using the provided clamp range and stat type.
    /// </summary>
    /// <param name="statType">Runtime scalable stat type.</param>
    /// <param name="minimumValue">Raw minimum clamp value configured for the stat.</param>
    /// <param name="maximumValue">Raw maximum clamp value configured for the stat.</param>
    /// <param name="rawValue">Unnormalized numeric value to sanitize.</param>
    /// <returns>Clamped numeric value normalized for the scalable-stat type.</returns>
    public static float ResolveNormalizedValue(PlayerScalableStatType statType,
                                               float minimumValue,
                                               float maximumValue,
                                               float rawValue)
    {
        if (statType == PlayerScalableStatType.Boolean)
            return rawValue >= 0.5f ? 1f : 0f;

        if (statType == PlayerScalableStatType.Token)
            return 0f;

        ResolveOrderedRange(minimumValue, maximumValue, out float resolvedMinimumValue, out float resolvedMaximumValue);

        if (statType == PlayerScalableStatType.Unsigned)
        {
            resolvedMinimumValue = Mathf.Max(0f, resolvedMinimumValue);
            resolvedMaximumValue = Mathf.Max(resolvedMinimumValue, resolvedMaximumValue);
        }

        float clampedValue = Mathf.Clamp(rawValue, resolvedMinimumValue, resolvedMaximumValue);

        if (statType == PlayerScalableStatType.Integer || statType == PlayerScalableStatType.Unsigned)
            clampedValue = (float)Math.Round(clampedValue, MidpointRounding.AwayFromZero);

        return clampedValue;
    }

    /// <summary>
    /// Resolves one normalized scalable-stat value using a baked blob entry as clamp metadata source.
    /// </summary>
    /// <param name="scalableStat">Baked scalable-stat metadata.</param>
    /// <param name="rawValue">Unnormalized value to sanitize for runtime usage.</param>
    /// <returns>Clamped value normalized for the scalable-stat type.</returns>
    public static float ResolveNormalizedValue(ref PlayerScalableStatBlob scalableStat, float rawValue)
    {
        return ResolveNormalizedValue((PlayerScalableStatType)scalableStat.Type,
                                      scalableStat.MinimumValue,
                                      scalableStat.MaximumValue,
                                      rawValue);
    }

    /// <summary>
    /// Resolves one normalized scalable-stat value using a runtime buffer element as clamp metadata source.
    /// </summary>
    /// <param name="scalableStat">Runtime scalable-stat entry.</param>
    /// <param name="rawValue">Unnormalized value to sanitize for runtime usage.</param>
    /// <returns>Clamped value normalized for the scalable-stat type.</returns>
    public static float ResolveNormalizedValue(in PlayerScalableStatElement scalableStat, float rawValue)
    {
        return ResolveNormalizedValue((PlayerScalableStatType)scalableStat.Type,
                                      scalableStat.MinimumValue,
                                      scalableStat.MaximumValue,
                                      rawValue);
    }

    /// <summary>
    /// Resolves one numeric projection from the current runtime scalable stat entry.
    /// </summary>
    /// <param name="scalableStat">Runtime scalable stat entry.</param>
    /// <returns>Numeric projection of the typed scalable stat.</returns>
    public static float ResolveNumericProjection(in PlayerScalableStatElement scalableStat)
    {
        PlayerScalableStatType statType = (PlayerScalableStatType)scalableStat.Type;

        switch (statType)
        {
            case PlayerScalableStatType.Boolean:
                return scalableStat.BooleanValue != 0 ? 1f : 0f;
            case PlayerScalableStatType.Token:
                return 0f;
            default:
                return scalableStat.Value;
        }
    }

    /// <summary>
    /// Resolves one ordered clamp range from raw serialized minimum and maximum values.
    /// </summary>
    /// <param name="minimumValue">Raw serialized minimum value.</param>
    /// <param name="maximumValue">Raw serialized maximum value.</param>
    /// <param name="resolvedMinimumValue">Ordered lower bound used by runtime normalization.</param>
    /// <param name="resolvedMaximumValue">Ordered upper bound used by runtime normalization.</param>
    public static void ResolveOrderedRange(float minimumValue,
                                           float maximumValue,
                                           out float resolvedMinimumValue,
                                           out float resolvedMaximumValue)
    {
        resolvedMinimumValue = Mathf.Min(minimumValue, maximumValue);
        resolvedMaximumValue = Mathf.Max(minimumValue, maximumValue);
    }
    #endregion

    #endregion
}
