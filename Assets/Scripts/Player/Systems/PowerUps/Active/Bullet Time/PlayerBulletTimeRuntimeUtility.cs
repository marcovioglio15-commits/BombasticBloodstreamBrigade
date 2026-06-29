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
    /// <param name="enemySlowPercent">Target enemy slow percentage.</param>
    /// <param name="playerProjectileSlowPercent">Target player projectile slow percentage.</param>
    /// <param name="transitionTimeSeconds">Blend duration used when the effect activates or expires.</param>
    public static void ActivateTimedEffect(ref PlayerBulletTimeState bulletTimeState,
                                           float durationSeconds,
                                           float enemySlowPercent,
                                           float playerProjectileSlowPercent,
                                           float transitionTimeSeconds)
    {
        float resolvedDuration = math.max(0f, durationSeconds);
        float resolvedEnemySlowPercent = math.clamp(enemySlowPercent, 0f, 100f);
        float resolvedPlayerProjectileSlowPercent = math.clamp(playerProjectileSlowPercent, 0f, 100f);
        float resolvedTransitionTimeSeconds = math.max(0f, transitionTimeSeconds);

        if (resolvedDuration <= ComparisonEpsilon ||
            (resolvedEnemySlowPercent <= ComparisonEpsilon && resolvedPlayerProjectileSlowPercent <= ComparisonEpsilon))
            return;

        RefreshTimedChannel(ref bulletTimeState.TimedRemainingDuration,
                            ref bulletTimeState.TimedSlowPercent,
                            resolvedDuration,
                            resolvedEnemySlowPercent);
        RefreshTimedChannel(ref bulletTimeState.TimedPlayerProjectileRemainingDuration,
                            ref bulletTimeState.TimedPlayerProjectileSlowPercent,
                            resolvedDuration,
                            resolvedPlayerProjectileSlowPercent);

        bulletTimeState.TimedTransitionTimeSeconds = math.max(bulletTimeState.TimedTransitionTimeSeconds,
                                                              resolvedTransitionTimeSeconds);
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
    /// <param name="playerProjectileSlowPercent">Current player projectile slow percentage after this tick.</param>
    /// <returns>Current enemy slow percentage after this tick.</returns>
    public static float Tick(ref PlayerBulletTimeState bulletTimeState,
                             float deltaTime,
                             out float playerProjectileSlowPercent)
    {
        float safeDeltaTime = math.max(0f, deltaTime);

        TickTimedChannel(ref bulletTimeState.TimedRemainingDuration,
                         ref bulletTimeState.TimedSlowPercent,
                         safeDeltaTime);
        TickTimedChannel(ref bulletTimeState.TimedPlayerProjectileRemainingDuration,
                         ref bulletTimeState.TimedPlayerProjectileSlowPercent,
                         safeDeltaTime);

        float targetSlowPercent = ResolveTargetSlowPercent(in bulletTimeState,
                                                           bulletTimeState.TimedRemainingDuration,
                                                           bulletTimeState.TimedSlowPercent,
                                                           bulletTimeState.ToggleSlowPercent,
                                                           out float targetTransitionTimeSeconds);
        float targetPlayerProjectileSlowPercent = ResolveTargetSlowPercent(in bulletTimeState,
                                                                           bulletTimeState.TimedPlayerProjectileRemainingDuration,
                                                                           bulletTimeState.TimedPlayerProjectileSlowPercent,
                                                                           bulletTimeState.TogglePlayerProjectileSlowPercent,
                                                                           out float targetPlayerProjectileTransitionTimeSeconds);

        UpdateTransitionChannel(ref bulletTimeState.CurrentSlowPercent,
                                ref bulletTimeState.TransitionStartSlowPercent,
                                ref bulletTimeState.TransitionTargetSlowPercent,
                                ref bulletTimeState.TransitionDurationSeconds,
                                ref bulletTimeState.TransitionElapsedSeconds,
                                targetSlowPercent,
                                targetTransitionTimeSeconds,
                                safeDeltaTime);
        UpdateTransitionChannel(ref bulletTimeState.CurrentPlayerProjectileSlowPercent,
                                ref bulletTimeState.PlayerProjectileTransitionStartSlowPercent,
                                ref bulletTimeState.PlayerProjectileTransitionTargetSlowPercent,
                                ref bulletTimeState.PlayerProjectileTransitionDurationSeconds,
                                ref bulletTimeState.PlayerProjectileTransitionElapsedSeconds,
                                targetPlayerProjectileSlowPercent,
                                targetPlayerProjectileTransitionTimeSeconds,
                                safeDeltaTime);

        playerProjectileSlowPercent = math.clamp(bulletTimeState.CurrentPlayerProjectileSlowPercent, 0f, 100f);
        return math.clamp(bulletTimeState.CurrentSlowPercent, 0f, 100f);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes one timed Bullet Time channel without extending unrelated channels.
    /// </summary>
    /// <param name="remainingDuration">Mutable channel duration.</param>
    /// <param name="slowPercent">Mutable channel slow percentage.</param>
    /// <param name="resolvedDuration">Sanitized duration requested by the activation.</param>
    /// <param name="resolvedSlowPercent">Sanitized channel slow requested by the activation.</param>
    private static void RefreshTimedChannel(ref float remainingDuration,
                                            ref float slowPercent,
                                            float resolvedDuration,
                                            float resolvedSlowPercent)
    {
        if (resolvedSlowPercent <= ComparisonEpsilon)
            return;

        if (resolvedSlowPercent > slowPercent + ComparisonEpsilon)
            slowPercent = resolvedSlowPercent;

        remainingDuration = math.max(remainingDuration, resolvedDuration);
    }

    /// <summary>
    /// Advances one timed channel and clears its target once the channel expires.
    /// </summary>
    /// <param name="remainingDuration">Mutable channel duration.</param>
    /// <param name="slowPercent">Mutable channel slow percentage.</param>
    /// <param name="deltaTime">Sanitized frame delta time.</param>
    private static void TickTimedChannel(ref float remainingDuration, ref float slowPercent, float deltaTime)
    {
        if (remainingDuration > 0f)
        {
            remainingDuration = math.max(0f, remainingDuration - deltaTime);

            if (remainingDuration <= ComparisonEpsilon)
            {
                remainingDuration = 0f;
                slowPercent = 0f;
            }

            return;
        }

        slowPercent = 0f;
    }

    /// <summary>
    /// Resolves the strongest requested slow target and its associated transition duration.
    /// </summary>
    /// <param name="bulletTimeState">Current runtime state.</param>
    /// <param name="timedRemainingDuration">Timed duration for the channel being resolved.</param>
    /// <param name="timedSlowPercent">Timed slow target for the channel being resolved.</param>
    /// <param name="toggleSlowPercent">Toggle slow target for the channel being resolved.</param>
    /// <param name="transitionTimeSeconds">Transition duration associated with the selected target.</param>
    /// <returns>Target slow percentage requested this frame.</returns>
    private static float ResolveTargetSlowPercent(in PlayerBulletTimeState bulletTimeState,
                                                  float timedRemainingDuration,
                                                  float timedSlowPercent,
                                                  float toggleSlowPercent,
                                                  out float transitionTimeSeconds)
    {
        float resolvedTimedSlowPercent = timedRemainingDuration > ComparisonEpsilon
            ? math.clamp(timedSlowPercent, 0f, 100f)
            : 0f;
        float resolvedToggleSlowPercent = math.clamp(toggleSlowPercent, 0f, 100f);

        if (resolvedTimedSlowPercent >= resolvedToggleSlowPercent)
        {
            transitionTimeSeconds = resolvedTimedSlowPercent > ComparisonEpsilon
                ? math.max(0f, bulletTimeState.TimedTransitionTimeSeconds)
                : math.max(math.max(0f, bulletTimeState.TimedTransitionTimeSeconds),
                           math.max(0f, bulletTimeState.ToggleTransitionTimeSeconds));
            return resolvedTimedSlowPercent;
        }

        transitionTimeSeconds = math.max(0f, bulletTimeState.ToggleTransitionTimeSeconds);
        return resolvedToggleSlowPercent;
    }

    /// <summary>
    /// Updates one smooth transition channel toward its current target slow value.
    /// </summary>
    /// <param name="currentSlowPercent">Mutable current slow value for the channel.</param>
    /// <param name="transitionStartSlowPercent">Mutable transition start value.</param>
    /// <param name="transitionTargetSlowPercent">Mutable transition target value.</param>
    /// <param name="transitionDurationSeconds">Mutable transition duration.</param>
    /// <param name="transitionElapsedSeconds">Mutable transition elapsed time.</param>
    /// <param name="targetSlowPercent">Resolved target slow value for this tick.</param>
    /// <param name="targetTransitionTimeSeconds">Transition time associated with the selected target.</param>
    /// <param name="deltaTime">Sanitized frame delta time.</param>
    private static void UpdateTransitionChannel(ref float currentSlowPercent,
                                                ref float transitionStartSlowPercent,
                                                ref float transitionTargetSlowPercent,
                                                ref float transitionDurationSeconds,
                                                ref float transitionElapsedSeconds,
                                                float targetSlowPercent,
                                                float targetTransitionTimeSeconds,
                                                float deltaTime)
    {
        if (math.abs(targetSlowPercent - transitionTargetSlowPercent) > ComparisonEpsilon)
        {
            transitionStartSlowPercent = currentSlowPercent;
            transitionTargetSlowPercent = targetSlowPercent;
            transitionDurationSeconds = math.max(0f, targetTransitionTimeSeconds);
            transitionElapsedSeconds = 0f;
        }

        if (transitionDurationSeconds <= ComparisonEpsilon)
        {
            currentSlowPercent = targetSlowPercent;
            transitionStartSlowPercent = targetSlowPercent;
            transitionTargetSlowPercent = targetSlowPercent;
            transitionElapsedSeconds = transitionDurationSeconds;
            return;
        }

        transitionElapsedSeconds = math.min(transitionDurationSeconds, transitionElapsedSeconds + deltaTime);
        float normalizedTransition = math.saturate(transitionElapsedSeconds / transitionDurationSeconds);
        currentSlowPercent = math.lerp(transitionStartSlowPercent, transitionTargetSlowPercent, normalizedTransition);
    }
    #endregion

    #endregion
}
