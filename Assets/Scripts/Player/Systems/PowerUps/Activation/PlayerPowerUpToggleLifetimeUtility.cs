using Unity.Mathematics;

/// <summary>
/// Advances the optional finite lifetime shared by all toggleable active power-ups.
/// </summary>
public static class PlayerPowerUpToggleLifetimeUtility
{
    #region Methods

    #region State Update
    /// <summary>
    /// Advances a configured finite toggle lifetime without retaining per-frame state for unlimited toggles.
    /// </summary>
    /// <param name="maximumActiveDurationSeconds">Maximum active lifetime, or zero when no time limit is configured.</param>
    /// <param name="deltaTime">Current frame duration.</param>
    /// <param name="runtimeState">Mutable slot state retaining elapsed active time.</param>
    /// <returns>True when the configured finite lifetime has expired during this update.</returns>
    public static bool Tick(float maximumActiveDurationSeconds,
                            float deltaTime,
                            ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        if (maximumActiveDurationSeconds <= 0f)
        {
            runtimeState.ToggleActiveElapsedSeconds = 0f;
            return false;
        }

        runtimeState.ToggleActiveElapsedSeconds += math.max(0f, deltaTime);

        if (runtimeState.ToggleActiveElapsedSeconds < maximumActiveDurationSeconds)
            return false;

        runtimeState.ToggleActiveElapsedSeconds = 0f;
        return true;
    }

    /// <summary>
    /// Clears elapsed toggle lifetime when a slot is inactive, replaced, or interrupted.
    /// </summary>
    /// <param name="runtimeState">Mutable slot state reset in place.</param>
    public static void Reset(ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        runtimeState.ToggleActiveElapsedSeconds = 0f;
    }
    #endregion

    #endregion
}
