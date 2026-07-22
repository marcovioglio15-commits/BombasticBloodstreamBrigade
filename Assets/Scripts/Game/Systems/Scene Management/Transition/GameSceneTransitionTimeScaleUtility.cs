using UnityEngine;

/// <summary>
/// Applies and restores transition-owned Unity time-scale locks.
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
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="purpose">Transition purpose selecting pause-free room traversal.</param>
    /// <param name="timeScaleChanged">True when a previous lock is active.</param>
    /// <param name="previousTimeScale">Previous time-scale value captured before locking.</param>
    public static void Begin(GameSceneManagerConfig config,
                             GameSceneTransitionPurpose purpose,
                             ref bool timeScaleChanged,
                             ref float previousTimeScale)
    {
        if (purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal ||
            config.SetTimeScaleDuringTransition == 0)
        {
            return;
        }

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
    /// </summary>
    /// <param name="timeScaleChanged">True when a previous lock is active.</param>
    /// <param name="previousTimeScale">Time-scale value captured before locking.</param>
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
    /// </summary>
    /// <param name="currentTimeScale">Time.timeScale at transition start.</param>
    /// <returns>Previous non-paused scale, or normal gameplay scale when the request came from a pause menu.</returns>
    private static float ResolveRestoredTimeScale(float currentTimeScale)
    {
        if (currentTimeScale <= PausedTimeScaleThreshold)
            return DefaultRestoredTimeScale;

        return currentTimeScale;
    }
    #endregion

    #endregion
}
