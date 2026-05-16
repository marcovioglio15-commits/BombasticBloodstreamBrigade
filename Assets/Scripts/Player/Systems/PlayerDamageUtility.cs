using Unity.Mathematics;

/// <summary>
/// Provides shared player damage helpers used by enemy-hit and elemental systems.
/// </summary>
public static class PlayerDamageUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether the player's post-hit grace window is still active at the provided world time.
    /// </summary>
    /// <param name="playerDamageGraceState">Runtime grace state that stores the current ignore-damage deadline.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    /// <returns>True when incoming damage should be ignored because the grace window is active.</returns>
    public static bool IsDamageGraceActive(in PlayerDamageGraceState playerDamageGraceState,
                                           float elapsedTime)
    {
        return elapsedTime < playerDamageGraceState.IgnoreDamageUntilTime;
    }

    /// <summary>
    /// Applies incoming flat damage to the player while honoring the configured post-hit grace window.
    /// The grace timer is refreshed only when positive damage is actually accepted.
    /// </summary>
    /// <param name="playerHealth">Mutable player health state that receives remaining damage after shield absorption.</param>
    /// <param name="playerShield">Mutable player shield state that absorbs damage before health.</param>
    /// <param name="playerDamageGraceState">Mutable grace state updated after one valid hit is accepted.</param>
    /// <param name="runtimeHealthConfig">Current runtime health config that provides the grace duration.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    /// <param name="incomingDamage">Raw incoming damage value. Negative values are treated as zero.</param>
    /// <returns>True when the hit was accepted and modified player survivability state.</returns>
    public static bool TryApplyFlatShieldDamage(ref PlayerHealth playerHealth,
                                                ref PlayerShield playerShield,
                                                ref PlayerDamageGraceState playerDamageGraceState,
                                                in PlayerRuntimeHealthStatisticsConfig runtimeHealthConfig,
                                                float elapsedTime,
                                                float incomingDamage)
    {
        float sanitizedIncomingDamage = math.max(0f, incomingDamage);

        if (sanitizedIncomingDamage <= 0f)
            return false;

        if (IsDamageGraceActive(in playerDamageGraceState, elapsedTime))
            return false;

        ApplyFlatShieldDamage(ref playerHealth, ref playerShield, sanitizedIncomingDamage);
        playerDamageGraceState.IgnoreDamageUntilTime = elapsedTime + math.max(0f, runtimeHealthConfig.GraceTimeSeconds);
        return true;
    }

    /// <summary>
    /// Applies incoming flat damage to player shield first and then to health.
    /// This overload intentionally ignores grace rules and is meant for callers that already resolved them.
    /// </summary>
    /// <param name="playerHealth">Mutable player health state that receives remaining damage after shield absorption.</param>
    /// <param name="playerShield">Mutable player shield state that absorbs damage before health.</param>
    /// <param name="incomingDamage">Raw incoming damage value. Negative values are treated as zero.</param>
    public static void ApplyFlatShieldDamage(ref PlayerHealth playerHealth, ref PlayerShield playerShield, float incomingDamage)
    {
        float remainingDamage = math.max(0f, incomingDamage);

        if (remainingDamage <= 0f)
            return;

        if (playerShield.Current > 0f)
        {
            float absorbedDamage = math.min(playerShield.Current, remainingDamage);
            playerShield.Current -= absorbedDamage;
            remainingDamage -= absorbedDamage;

            if (playerShield.Current < 0f)
                playerShield.Current = 0f;
        }

        if (remainingDamage <= 0f)
            return;

        playerHealth.Current -= remainingDamage;

        if (playerHealth.Current < 0f)
            playerHealth.Current = 0f;
    }
    #endregion

    #endregion
}
