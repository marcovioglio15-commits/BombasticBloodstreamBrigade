using Unity.Entities;

/// <summary>
/// Creates canonical default runtime states for enemy Power-Up Stealer buffers.
/// </summary>
internal static class EnemyPowerUpStealerRuntimeDefaultsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a clean Power-Up Stealer runtime entry for a newly baked, pooled, or activated module.
    /// </summary>
    /// <returns>Default runtime entry with no stolen payload and no recovery metrics.</returns>
    public static EnemyPowerUpStealerRuntimeElement CreateDefault()
    {
        return new EnemyPowerUpStealerRuntimeElement
        {
            HasTriggeredOnce = 0,
            HasStolenPowerUp = 0,
            StolenKind = PlayerPowerUpUnlockKind.Active,
            PowerUpId = default,
            StoredActivePowerUp = default,
            StoredPassiveTool = default,
            OriginalActiveSlotIndex = -1,
            OriginalPassiveCatalogIndex = -1,
            OriginalPassiveUnlockCount = 0,
            PlayerEntity = Entity.Null,
            UseDamageRecovery = 0,
            DamageRecoveryPercent = 0f,
            UseTimedDamageRecovery = 0,
            TimedDamageRecoveryPercent = 0f,
            TimedDamageRecoverySeconds = 0f,
            HealthAtSteal = 0f,
            LastObservedHealth = 0f,
            RecoveryWindowElapsedSeconds = 0f,
            RecoveryWindowAccumulatedPercent = 0f
        };
    }

    /// <summary>
    /// Clears a runtime entry after recovery while preserving one-shot trigger history for the same module activation.
    /// </summary>
    /// <param name="runtime">Runtime entry that just returned its stolen payload.</param>
    /// <returns>Cleared runtime entry ready to ignore held-payload checks.</returns>
    public static EnemyPowerUpStealerRuntimeElement CreateCleared(in EnemyPowerUpStealerRuntimeElement runtime)
    {
        EnemyPowerUpStealerRuntimeElement clearedRuntime = CreateDefault();
        clearedRuntime.HasTriggeredOnce = runtime.HasTriggeredOnce;
        return clearedRuntime;
    }
    #endregion

    #endregion
}
