using Unity.Mathematics;

/// <summary>
/// Compiles charge-completion feedback for all modular compositions.
/// </summary>
public static class PlayerChargeRumbleBakeUtility
{
    #region Methods

    #region Compilation
    /// <summary>
    /// Retains tuning when disabled so a runtime boolean formula can enable the baked impulse.
    /// </summary>
    /// <param name="payload">Hold-charge module defaults or a binding override.</param>
    /// <param name="config">Accumulated rumble payload for this power-up.</param>
    public static void Merge(PowerUpHoldChargeModuleData payload, ref PlayerChargeRumbleConfig config)
    {
        if (payload == null)
            return;

        // Duplicate triggers combine into one impulse using the strongest authored values.
        config.Enabled = payload.ChargeCompleteRumbleEnabled ? (byte)1 : config.Enabled;
        config.DurationSeconds = math.max(config.DurationSeconds, ResolveFinitePositive(payload.ChargeCompleteRumbleDurationSeconds));
        config.LowFrequency = math.max(config.LowFrequency, math.saturate(ResolveFinitePositive(payload.ChargeCompleteRumbleLowFrequency)));
        config.HighFrequency = math.max(config.HighFrequency, math.saturate(ResolveFinitePositive(payload.ChargeCompleteRumbleHighFrequency)));
    }

    /// <summary>
    /// Protects runtime motor data without changing preset values reported by editor warnings.
    /// </summary>
    /// <param name="value">Authored duration or motor strength.</param>
    /// <returns>Finite nonnegative runtime value.</returns>
    private static float ResolveFinitePositive(float value)
    {
        return math.isfinite(value) ? math.max(0f, value) : 0f;
    }
    #endregion

    #endregion
}
