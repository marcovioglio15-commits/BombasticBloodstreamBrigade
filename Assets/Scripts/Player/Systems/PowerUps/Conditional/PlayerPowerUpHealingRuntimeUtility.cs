using Unity.Mathematics;

/// <summary>
/// Starts or combines power-up heal-over-time payloads through one allocation-free runtime path.
/// </summary>
public static class PlayerPowerUpHealingRuntimeUtility
{
    #region Methods

    /// <summary>
    /// Applies a bounded heal request to the current heal-over-time state using the authored stack policy.
    /// </summary>
    /// <param name="requestedHealAmount">Total health points requested by the effect.</param>
    /// <param name="currentMissingHealth">Health currently missing from the target.</param>
    /// <param name="durationSeconds">Duration over which the bounded amount is restored.</param>
    /// <param name="tickIntervalSeconds">Minimum interval between authoritative heal ticks.</param>
    /// <param name="stackPolicy">Policy used when another heal-over-time effect is already active.</param>
    /// <param name="healOverTimeState">Mutable heal-over-time state updated in place.</param>
    /// <returns>True when the request started or changed an active heal effect.</returns>
    public static bool TryApply(float requestedHealAmount,
                                float currentMissingHealth,
                                float durationSeconds,
                                float tickIntervalSeconds,
                                PowerUpHealStackPolicy stackPolicy,
                                ref PlayerHealOverTimeState healOverTimeState)
    {
        float totalHeal = math.min(math.max(0f, requestedHealAmount),
                                   math.max(0f, currentMissingHealth));

        if (totalHeal <= 0f)
            return false;

        float resolvedDurationSeconds = math.max(0.05f, durationSeconds);
        float resolvedTickIntervalSeconds = math.max(0.01f, tickIntervalSeconds);
        float healPerSecond = totalHeal / resolvedDurationSeconds;
        bool hasActiveHeal = healOverTimeState.IsActive != 0;

        switch (stackPolicy)
        {
            case PowerUpHealStackPolicy.IgnoreIfActive:
                if (hasActiveHeal)
                    return false;

                break;
            case PowerUpHealStackPolicy.Additive:
                if (hasActiveHeal)
                {
                    healOverTimeState.RemainingTotalHeal += totalHeal;
                    healOverTimeState.RemainingDuration = math.max(healOverTimeState.RemainingDuration,
                                                                   resolvedDurationSeconds);
                    healOverTimeState.TickIntervalSeconds = math.min(healOverTimeState.TickIntervalSeconds,
                                                                     resolvedTickIntervalSeconds);
                    healOverTimeState.HealPerSecond += healPerSecond;
                    healOverTimeState.IsActive = 1;
                    return true;
                }

                break;
        }

        healOverTimeState.IsActive = 1;
        healOverTimeState.HealPerSecond = healPerSecond;
        healOverTimeState.RemainingTotalHeal = totalHeal;
        healOverTimeState.RemainingDuration = resolvedDurationSeconds;
        healOverTimeState.TickIntervalSeconds = resolvedTickIntervalSeconds;
        healOverTimeState.TickTimer = 0f;
        return true;
    }

    #endregion
}
