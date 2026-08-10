using Unity.Mathematics;

/// <summary>
/// Applies unified-formula Add Scaling results to the reusable Drop Attraction runtime config.
/// </summary>
internal static class PlayerRuntimePowerUpDropAttractionScalingApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies a numeric Drop Attraction scaling result to the matching runtime field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="config">Mutable Drop Attraction config rebuilt from its immutable baseline.</param>
    /// <returns>True when the payload path targeted a numeric Drop Attraction field.</returns>
    public static bool TryApplyValue(string payloadPath,
                                     float resolvedValue,
                                     ref DropAttractionPowerUpConfig config)
    {
        if (payloadPath != "dropAttraction.attractionRadius")
            return false;

        config.AttractionRadius = math.max(0f, resolvedValue);
        return true;
    }

    /// <summary>
    /// Applies a boolean Drop Attraction scaling result to the matching runtime field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula boolean result.</param>
    /// <param name="config">Mutable Drop Attraction config rebuilt from its immutable baseline.</param>
    /// <returns>True when the payload path targeted a boolean Drop Attraction field.</returns>
    public static bool TryApplyBooleanValue(string payloadPath,
                                            bool resolvedValue,
                                            ref DropAttractionPowerUpConfig config)
    {
        if (payloadPath != "dropAttraction.consumeUnusableDrops")
            return false;

        config.ConsumeUnusableDrops = resolvedValue ? (byte)1 : (byte)0;
        return true;
    }
    #endregion

    #endregion
}
