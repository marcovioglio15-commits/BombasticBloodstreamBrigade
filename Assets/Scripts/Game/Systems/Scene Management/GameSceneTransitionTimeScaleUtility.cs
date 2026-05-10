using UnityEngine;

/// <summary>
/// Applies and restores transition-owned Unity time-scale locks.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneTransitionTimeScaleUtility
{
    #region Constants
    private const float PausedTimeScaleThreshold = 0.0001f;
    private const float DefaultRestoredTimeScale = 1f;
    #endregion

    #region Methods

    #region Lock
    /// <summary>
    /// Starts a transition-owned time-scale lock when configured.
    /// /params config Scene manager runtime config.
    /// /params timeScaleChanged True when a previous lock is active.
    /// /params previousTimeScale Previous time-scale value captured before locking.
    /// /returns None.
    /// </summary>
    public static void Begin(GameSceneManagerConfig config, ref bool timeScaleChanged, ref float previousTimeScale)
    {
        if (config.SetTimeScaleDuringTransition == 0 && config.LockGameplayInput == 0)
            return;

        if (timeScaleChanged)
            return;

        previousTimeScale = ResolveRestoredTimeScale(Time.timeScale);
        Time.timeScale = 0f;
        timeScaleChanged = true;
    }
    #endregion

    #region Restore
    /// <summary>
    /// Restores a transition-owned time-scale lock.
    /// /params timeScaleChanged True when a previous lock is active.
    /// /params previousTimeScale Time-scale value captured before locking.
    /// /returns None.
    /// </summary>
    public static void Restore(ref bool timeScaleChanged, float previousTimeScale)
    {
        if (!timeScaleChanged)
            return;

        Time.timeScale = previousTimeScale;
        timeScaleChanged = false;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the time scale that should be restored when a transition starts from a UI hard pause.
    /// /params currentTimeScale Time.timeScale at transition start.
    /// /returns Previous non-paused scale, or normal gameplay scale when the request came from a pause menu.
    /// </summary>
    private static float ResolveRestoredTimeScale(float currentTimeScale)
    {
        if (currentTimeScale <= PausedTimeScaleThreshold)
            return DefaultRestoredTimeScale;

        return currentTimeScale;
    }
    #endregion

    #endregion
}
