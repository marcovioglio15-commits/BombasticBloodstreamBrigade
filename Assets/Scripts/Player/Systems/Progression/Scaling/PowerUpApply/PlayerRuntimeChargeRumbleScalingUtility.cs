using Unity.Mathematics;

/// <summary>
/// Applies unified formula results to the shared manual and Sudden Strike rumble payload.
/// </summary>
public static class PlayerRuntimeChargeRumbleScalingUtility
{
    #region Methods

    #region Formula Results
    /// <summary>
    /// Resolves numeric rumble paths without reflection; authored values remain untouched.
    /// </summary>
    /// <param name="path">Modular payload path carried by the scaling rule.</param>
    /// <param name="value">Numeric formula result.</param>
    /// <param name="config">Runtime payload rebuilt from its immutable baseline.</param>
    /// <returns>True when the path belongs to charge-completion rumble.</returns>
    public static bool TryApplyValue(string path, float value, ref PlayerChargeRumbleConfig config)
    {
        // Invalid formula results cannot propagate NaN into the input backend.
        float safeValue = math.isfinite(value) ? value : 0f;

        switch (path)
        {
            case "holdCharge.chargeCompleteRumbleDurationSeconds":
                config.DurationSeconds = math.max(0f, safeValue);
                return true;
            case "holdCharge.chargeCompleteRumbleLowFrequency":
                config.LowFrequency = math.saturate(safeValue);
                return true;
            case "holdCharge.chargeCompleteRumbleHighFrequency":
                config.HighFrequency = math.saturate(safeValue);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies the boolean result of a rumble enable formula in every supported composition.
    /// </summary>
    /// <param name="path">Modular payload path carried by the scaling rule.</param>
    /// <param name="value">Boolean formula result.</param>
    /// <param name="config">Runtime payload rebuilt from its immutable baseline.</param>
    /// <returns>True when the enable field was handled.</returns>
    public static bool TryApplyBooleanValue(string path, bool value, ref PlayerChargeRumbleConfig config)
    {
        if (path != "holdCharge.chargeCompleteRumbleEnabled")
            return false;

        config.Enabled = value ? (byte)1 : (byte)0;
        return true;
    }
    #endregion

    #endregion
}
