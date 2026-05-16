using Unity.Mathematics;

/// <summary>
/// Centralizes runtime state mutations for Bullet Time timed, toggle, and transition behavior.
/// </summary>
public static class PlayerBulletTimeRuntimeUtility
{
    #region Constants
    private const float ComparisonEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts or refreshes one timed Bullet Time effect on the provided state.
    /// </summary>
    /// <param name="bulletTimeState">Mutable runtime state updated in place.</param>
    /// <param name="durationSeconds">Timed-effect duration.</param>
    /// <param name="slowPercent">Target enemy slow percentage.</param>
    /// <param name="transitionTimeSeconds">Blend duration used when the effect activates or expires.</param>
    public static void ActivateTimedEffect(ref PlayerBulletTimeState bulletTimeState,
                                           float durationSeconds,
                                           float slowPercent,
                                           float transitionTimeSeconds)
    {
        float resolvedDuration = math.max(0f, durationSeconds);
        float resolvedSlowPercent = math.clamp(slowPercent, 0f, 100f);

        if (resolvedDuration <= ComparisonEpsilon || resolvedSlowPercent <= ComparisonEpsilon)
            return;

        if (resolvedSlowPercent > bulletTimeState.TimedSlowPercent + ComparisonEpsilon)
        {
            bulletTimeState.TimedSlowPercent = resolvedSlowPercent;
            bulletTimeState.TimedTransitionTimeSeconds = math.max(0f, transitionTimeSeconds);
            bulletTimeState.TimedRemainingDuration = resolvedDuration;
            return;
        }

        bulletTimeState.TimedRemainingDuration = math.max(bulletTimeState.TimedRemainingDuration, resolvedDuration);
        bulletTimeState.TimedTransitionTimeSeconds = math.max(bulletTimeState.TimedTransitionTimeSeconds,
                                                              math.max(0f, transitionTimeSeconds));

        if (resolvedSlowPercent > bulletTimeState.TimedSlowPercent)
            bulletTimeState.TimedSlowPercent = resolvedSlowPercent;
    }

    /// <summary>
    /// Clears all Bullet Time state immediately without preserving any current transition.
    /// </summary>
    /// <param name="bulletTimeState">Mutable runtime state reset in place.</param>
    public static void Clear(ref PlayerBulletTimeState bulletTimeState)
    {
        bulletTimeState = default;
    }

    /// <summary>
    /// Advances timed duration and transition progress, then returns the resolved current slow percentage.
    /// </summary>
    /// <param name="bulletTimeState">Mutable runtime state updated in place.</param>
    /// <param name="deltaTime">Frame delta time.</param>
    /// <returns>Current enemy slow percentage after this tick.</returns>
    public static float Tick(ref PlayerBulletTimeState bulletTimeState, float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);

        if (bulletTimeState.TimedRemainingDuration > 0f)
        {
            bulletTimeState.TimedRemainingDuration = math.max(0f, bulletTimeState.TimedRemainingDuration - safeDeltaTime);

            if (bulletTimeState.TimedRemainingDuration <= ComparisonEpsilon)
            {
                bulletTimeState.TimedRemainingDuration = 0f;
                bulletTimeState.TimedSlowPercent = 0f;
            }
        }
        else
        {
            bulletTimeState.TimedSlowPercent = 0f;
        }

        float targetSlowPercent = ResolveTargetSlowPercent(in bulletTimeState, out float targetTransitionTimeSeconds);

        if (math.abs(targetSlowPercent - bulletTimeState.TransitionTargetSlowPercent) > ComparisonEpsilon)
        {
            bulletTimeState.TransitionStartSlowPercent = bulletTimeState.CurrentSlowPercent;
            bulletTimeState.TransitionTargetSlowPercent = targetSlowPercent;
            bulletTimeState.TransitionDurationSeconds = math.max(0f, targetTransitionTimeSeconds);
            bulletTimeState.TransitionElapsedSeconds = 0f;
        }

        if (bulletTimeState.TransitionDurationSeconds <= ComparisonEpsilon)
        {
            bulletTimeState.CurrentSlowPercent = targetSlowPercent;
            bulletTimeState.TransitionStartSlowPercent = targetSlowPercent;
            bulletTimeState.TransitionTargetSlowPercent = targetSlowPercent;
            bulletTimeState.TransitionElapsedSeconds = bulletTimeState.TransitionDurationSeconds;
            return math.clamp(bulletTimeState.CurrentSlowPercent, 0f, 100f);
        }

        bulletTimeState.TransitionElapsedSeconds = math.min(bulletTimeState.TransitionDurationSeconds,
                                                            bulletTimeState.TransitionElapsedSeconds + safeDeltaTime);
        float normalizedTransition = math.saturate(bulletTimeState.TransitionElapsedSeconds / bulletTimeState.TransitionDurationSeconds);
        bulletTimeState.CurrentSlowPercent = math.lerp(bulletTimeState.TransitionStartSlowPercent,
                                                       bulletTimeState.TransitionTargetSlowPercent,
                                                       normalizedTransition);
        return math.clamp(bulletTimeState.CurrentSlowPercent, 0f, 100f);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the strongest requested slow target and its associated transition duration.
    /// </summary>
    /// <param name="bulletTimeState">Current runtime state.</param>
    /// <param name="transitionTimeSeconds">Transition duration associated with the selected target.</param>
    /// <returns>Target slow percentage requested this frame.</returns>
    private static float ResolveTargetSlowPercent(in PlayerBulletTimeState bulletTimeState, out float transitionTimeSeconds)
    {
        float timedSlowPercent = bulletTimeState.TimedRemainingDuration > ComparisonEpsilon
            ? math.clamp(bulletTimeState.TimedSlowPercent, 0f, 100f)
            : 0f;
        float toggleSlowPercent = math.clamp(bulletTimeState.ToggleSlowPercent, 0f, 100f);

        if (timedSlowPercent >= toggleSlowPercent)
        {
            transitionTimeSeconds = timedSlowPercent > ComparisonEpsilon
                ? math.max(0f, bulletTimeState.TimedTransitionTimeSeconds)
                : math.max(math.max(0f, bulletTimeState.TimedTransitionTimeSeconds),
                           math.max(0f, bulletTimeState.ToggleTransitionTimeSeconds));
            return timedSlowPercent;
        }

        transitionTimeSeconds = math.max(0f, bulletTimeState.ToggleTransitionTimeSeconds);
        return toggleSlowPercent;
    }
    #endregion

    #endregion
}
