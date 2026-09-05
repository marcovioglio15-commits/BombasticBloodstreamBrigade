using Unity.Mathematics;

/// <summary>
/// Stores the scalable motor strengths and duration of a charge-completion impulse.
/// </summary>
public struct PlayerChargeRumbleConfig
{
    #region Fields
    public byte Enabled;
    public float DurationSeconds;
    public float LowFrequency;
    public float HighFrequency;
    #endregion
}

/// <summary>
/// Carries a simulation-triggered impulse to the existing controller rumble mixer.
/// </summary>
public struct PlayerChargeRumbleState
{
    #region Fields
    public float RemainingSeconds;
    public float LowFrequency;
    public float HighFrequency;
    #endregion
}

/// <summary>
/// Detects charge completion and mixes overlapping impulses without allocating or writing to input devices.
/// </summary>
public static class PlayerChargeRumbleRuntimeUtility
{
    #region Methods

    #region Requests
    /// <summary>
    /// Emits one impulse at maximum manual charge or when Sudden Strike becomes armed.
    /// </summary>
    /// <param name="config">Runtime charge payload after scaling.</param>
    /// <param name="previousCharge">Charge before this simulation step.</param>
    /// <param name="charge">Charge after this simulation step.</param>
    /// <param name="state">Player-owned impulse shared by both slots and conditional passives.</param>
    /// <param name="useRequiredCharge">True for Sudden Strike, which stops charging at its arming threshold.</param>
    public static void QueueCompletion(in ChargeShotPowerUpConfig config,
                                       float previousCharge,
                                       float charge,
                                       ref PlayerChargeRumbleState state,
                                       bool useRequiredCharge = false)
    {
        float threshold = useRequiredCharge ? config.RequiredCharge : math.max(config.RequiredCharge, config.MaximumCharge);

        // A held full charge cannot retrigger until it has fallen below the threshold.
        if (config.ChargeCompleteRumble.Enabled == 0 || threshold <= 0f ||
            previousCharge >= threshold || charge < threshold)
            return;

        state.RemainingSeconds = math.max(state.RemainingSeconds, config.ChargeCompleteRumble.DurationSeconds);
        state.LowFrequency = math.max(state.LowFrequency, config.ChargeCompleteRumble.LowFrequency);
        state.HighFrequency = math.max(state.HighFrequency, config.ChargeCompleteRumble.HighFrequency);
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Consumes real time only while an impulse exists and returns normalized motor speeds for this frame.
    /// </summary>
    /// <param name="state">Mutable impulse cleared on expiry.</param>
    /// <param name="deltaTime">Unscaled frame duration, keeping tactile timing independent of bullet time.</param>
    /// <returns>Low-frequency and high-frequency motor amplitudes.</returns>
    public static float2 Advance(ref PlayerChargeRumbleState state, float deltaTime)
    {
        if (state.RemainingSeconds <= 0f)
        {
            state = default;
            return float2.zero;
        }

        // Sample before decrementing so a short impulse still reaches the device once.
        float2 speeds = math.saturate(new float2(state.LowFrequency, state.HighFrequency));
        state.RemainingSeconds = math.max(0f, state.RemainingSeconds - math.max(0f, deltaTime));

        if (state.RemainingSeconds <= 0f)
            state = default;

        return speeds;
    }
    #endregion

    #endregion
}
