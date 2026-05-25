using Unity.Entities;

/// <summary>
/// Creates canonical default runtime states for enemy Power-Up Stealer buffers.
/// </summary>
internal static class EnemyPowerUpStealerRuntimeDefaultsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Initializes a clean Power-Up Stealer runtime entry for a newly baked, pooled, or activated module.
    /// </summary>
    /// <param name="runtime">Runtime entry receiving the default no-payload state.</param>
    public static void InitializeDefault(ref EnemyPowerUpStealerRuntimeElement runtime)
    {
        runtime.HasTriggeredOnce = 0;
        runtime.HasStolenPowerUp = 0;
        runtime.StolenKind = PlayerPowerUpUnlockKind.Active;
        runtime.PowerUpId = default;
        runtime.StoredActivePowerUp = default;
        runtime.StoredPassiveTool = default;
        runtime.OriginalActiveSlotIndex = -1;
        runtime.OriginalActiveEquipOrder = 0;
        runtime.OriginalPassiveCatalogIndex = -1;
        runtime.OriginalPassiveBufferIndex = -1;
        runtime.OriginalPassiveUnlockCount = 0;
        runtime.PlayerEntity = Entity.Null;
        runtime.UseDamageRecovery = 0;
        runtime.DamageRecoveryPercent = 0f;
        runtime.UseTimedDamageRecovery = 0;
        runtime.TimedDamageRecoveryPercent = 0f;
        runtime.TimedDamageRecoverySeconds = 0f;
        runtime.HealthAtSteal = 0f;
        runtime.LastObservedHealth = 0f;
        runtime.RecoveryWindowElapsedSeconds = 0f;
        runtime.RecoveryWindowAccumulatedPercent = 0f;
    }

    /// <summary>
    /// Clears a runtime entry after recovery while preserving one-shot trigger history for the same module activation.
    /// </summary>
    /// <param name="runtime">Runtime entry that just returned its stolen payload.</param>
    public static void ClearAfterRecovery(ref EnemyPowerUpStealerRuntimeElement runtime)
    {
        byte hasTriggeredOnce = runtime.HasTriggeredOnce;
        InitializeDefault(ref runtime);
        runtime.HasTriggeredOnce = hasTriggeredOnce;
    }
    #endregion

    #endregion
}
