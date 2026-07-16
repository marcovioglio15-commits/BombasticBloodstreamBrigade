using Unity.Entities;

/// <summary>
/// Resolves stable ownership for behaviour warnings that cannot be interrupted by other module slots.
/// </summary>
internal static class EnemyOffensiveEngagementInterruptionUtility
{
    #region Methods

    #region Internal Methods
    /// <summary>
    /// Retains the currently protected module source while its warning remains active, or acquires the first active protected source in stable buffer order.
    /// </summary>
    /// <param name="configs">Baked offensive engagement configs for the current enemy.</param>
    /// <param name="shooterRuntime">Current shooter runtime buffer used by weapon timing evaluation.</param>
    /// <param name="bombardierRuntime">Current Bombardier runtime buffer used by weapon timing evaluation.</param>
    /// <param name="hasBossSlotRuntimes">Whether boss slot runtime data is available for activation feedback.</param>
    /// <param name="bossSlotRuntimes">Boss slot runtime buffer used by module activation timing.</param>
    /// <param name="patternConfig">Current compiled pattern config used by short-range timing evaluation.</param>
    /// <param name="patternRuntimeState">Current mutable pattern runtime state used by short-range timing evaluation.</param>
    /// <param name="hasCurrentProtectedSource">Whether presentation state currently owns a protected source.</param>
    /// <param name="currentProtectedSource">Previously protected module source retained when its window is still active.</param>
    /// <param name="protectedSource">Resolved protected module source for the current frame.</param>
    /// <returns>True when one protected warning source owns presentation for the current frame.</returns>
    internal static bool TryResolveProtectedSource(DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs,
                                                   DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                   DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime,
                                                   bool hasBossSlotRuntimes,
                                                   DynamicBuffer<EnemyBossPatternSlotRuntimeElement> bossSlotRuntimes,
                                                   in EnemyPatternConfig patternConfig,
                                                   in EnemyPatternRuntimeState patternRuntimeState,
                                                   bool hasCurrentProtectedSource,
                                                   EnemyOffensiveEngagementTriggerSource currentProtectedSource,
                                                   out EnemyOffensiveEngagementTriggerSource protectedSource)
    {
        int configCount = configs.Length;

        // Preserve the first protected owner until its own color and billboard windows have both closed.
        if (hasCurrentProtectedSource)
        {
            for (int configIndex = 0; configIndex < configCount; configIndex++)
            {
                EnemyOffensiveEngagementConfigElement config = configs[configIndex];

                if (config.Source != currentProtectedSource || config.PreventWarningInterruption == 0)
                    continue;

                if (!EnemyOffensiveEngagementPresentationUtility.IsConfigWarningActive(in config,
                                                                                        shooterRuntime,
                                                                                        bombardierRuntime,
                                                                                        hasBossSlotRuntimes,
                                                                                        bossSlotRuntimes,
                                                                                        in patternConfig,
                                                                                        in patternRuntimeState))
                    continue;

                protectedSource = currentProtectedSource;
                return true;
            }
        }

        // Acquire the first newly active protected source using deterministic Core, Short-Range and Weapon buffer order.
        for (int configIndex = 0; configIndex < configCount; configIndex++)
        {
            EnemyOffensiveEngagementConfigElement config = configs[configIndex];

            if (config.PreventWarningInterruption == 0 ||
                !EnemyOffensiveEngagementPresentationUtility.IsConfigWarningActive(in config,
                                                                                    shooterRuntime,
                                                                                    bombardierRuntime,
                                                                                    hasBossSlotRuntimes,
                                                                                    bossSlotRuntimes,
                                                                                    in patternConfig,
                                                                                    in patternRuntimeState))
                continue;

            protectedSource = config.Source;
            return true;
        }

        protectedSource = default(EnemyOffensiveEngagementTriggerSource);
        return false;
    }
    #endregion

    #endregion
}
