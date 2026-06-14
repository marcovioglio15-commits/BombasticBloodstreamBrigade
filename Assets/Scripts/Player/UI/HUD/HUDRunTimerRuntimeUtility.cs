using TMPro;
using UnityEngine;

/// <summary>
/// Provides shared helpers for configuring and formatting the managed HUD run timer.
/// none.
/// </summary>
public static class HUDRunTimerRuntimeUtility
{
    #region Constants
    private const float DisplayRoundingEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one run timer config component from HUD-authored values.
    /// </summary>
    /// <param name="direction">Authored timer direction.</param>
    /// <param name="initialSeconds">Authored initial seconds.</param>
    /// <returns>New timer config component.</returns>
    public static PlayerRunTimerConfig CreateConfig(PlayerRunTimerDirection direction, float initialSeconds)
    {
        return new PlayerRunTimerConfig
        {
            Direction = direction,
            InitialSeconds = Mathf.Max(0f, initialSeconds)
        };
    }

    /// <summary>
    /// Builds the initial timer state that matches the authored HUD configuration.
    /// </summary>
    /// <param name="direction">Authored timer direction.</param>
    /// <param name="initialSeconds">Authored initial seconds.</param>
    /// <returns>New timer state component.</returns>
    public static PlayerRunTimerState CreateState(PlayerRunTimerDirection direction, float initialSeconds)
    {
        float clampedInitialSeconds = Mathf.Max(0f, initialSeconds);
        float currentSeconds = direction == PlayerRunTimerDirection.Backward ? clampedInitialSeconds : 0f;
        byte expired = direction == PlayerRunTimerDirection.Backward && clampedInitialSeconds <= 0f ? (byte)1 : (byte)0;
        return new PlayerRunTimerState
        {
            CurrentSeconds = currentSeconds,
            Expired = expired
        };
    }

    /// <summary>
    /// Resolves the integer value displayed by the HUD for the current authoritative timer state.
    /// </summary>
    /// <param name="timerConfig">Current timer config.</param>
    /// <param name="timerState">Current timer state.</param>
    /// <returns>Whole seconds value used by the clock label.</returns>
    public static int ResolveDisplaySeconds(in PlayerRunTimerConfig timerConfig, in PlayerRunTimerState timerState)
    {
        float clampedSeconds = Mathf.Max(0f, timerState.CurrentSeconds);

        switch (timerConfig.Direction)
        {
            case PlayerRunTimerDirection.Backward:
                if (clampedSeconds <= 0f)
                    return 0;

                return Mathf.CeilToInt(clampedSeconds - DisplayRoundingEpsilon);

            default:
                return Mathf.FloorToInt(clampedSeconds + DisplayRoundingEpsilon);
        }
    }

    /// <summary>
    /// Resolves the authored initial display value before ECS runtime data is available.
    /// </summary>
    /// <param name="direction">Authored timer direction.</param>
    /// <param name="initialSeconds">Authored initial seconds.</param>
    /// <returns>Whole seconds value used by the initial clock label.</returns>
    public static int ResolveInitialDisplaySeconds(PlayerRunTimerDirection direction, float initialSeconds)
    {
        PlayerRunTimerConfig timerConfig = CreateConfig(direction, initialSeconds);
        PlayerRunTimerState timerState = CreateState(direction, initialSeconds);
        return ResolveDisplaySeconds(in timerConfig, in timerState);
    }

    /// <summary>
    /// Applies a `00:00` formatted value to the target TMP text.
    /// </summary>
    /// <param name="timerText">Target TMP text.</param>
    /// <param name="totalSeconds">Whole seconds value to format.</param>
    public static void ApplyClockText(TMP_Text timerText, int totalSeconds)
    {
        if (timerText == null)
            return;

        int clampedSeconds = Mathf.Max(0, totalSeconds);
        int minutes = clampedSeconds / 60;
        int seconds = clampedSeconds % 60;
        timerText.SetText("{0:00}:{1:00}", minutes, seconds);
        timerText.enabled = true;
    }
    #endregion

    #endregion
}
