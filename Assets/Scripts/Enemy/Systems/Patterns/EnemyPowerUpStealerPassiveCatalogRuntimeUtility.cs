using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Handles Power-Up Stealer mutations for owned passive power-ups that do not create an equipped passive tool.
/// </summary>
internal static class EnemyPowerUpStealerPassiveCatalogRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Removes one catalog-only passive ownership stack from the player and stores it on the Stealer runtime.
    /// </summary>
    /// <param name="playerEntity">Player entity being stolen from.</param>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <param name="config">Stealer config containing the within-category selection mode.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used by acquisition anti-steal cooldowns.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to avoid stealing tool-backed passives through the catalog fallback.</param>
    /// <param name="runtime">Mutable Stealer runtime entry receiving the stolen payload.</param>
    /// <param name="playerAccess">Mutable player passive accessors.</param>
    /// <returns>True when one catalog-only passive ownership entry was stolen.</returns>
    public static bool TryStealCatalogOnlyPassivePowerUp(Entity playerEntity,
                                                         Entity enemyEntity,
                                                         in EnemyRuntimeState enemyRuntimeState,
                                                         int stealerIndex,
                                                         in EnemyPowerUpStealerConfigElement config,
                                                         float elapsedTime,
                                                         DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                         ref EnemyPowerUpStealerRuntimeElement runtime,
                                                         ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = playerAccess.UnlockCatalogLookup[playerEntity];
        int catalogIndex = EnemyPowerUpStealerSelectionUtility.ResolvePassiveCatalogIndexToSteal(unlockCatalog,
                                                                                                 equippedPassiveTools,
                                                                                                 config.AcquisitionStealCooldownSeconds,
                                                                                                 elapsedTime,
                                                                                                 enemyEntity,
                                                                                                 in enemyRuntimeState,
                                                                                                 stealerIndex,
                                                                                                 config.SelectionMode);

        if (catalogIndex < 0)
            return false;

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];
        int originalUnlockCount = math.max(1, catalogEntry.CurrentUnlockCount);

        // Clear catalog ownership so runtime-scoped Character Tuning and milestone duplicate rules see the stolen state.
        catalogEntry.CurrentUnlockCount = 0;
        catalogEntry.IsUnlocked = 0;
        catalogEntry.PendingInitialCharacterTuningApply = 0;
        unlockCatalog[catalogIndex] = catalogEntry;

        StoreCatalogOnlyPassivePayload(playerEntity,
                                       catalogIndex,
                                       originalUnlockCount,
                                       in catalogEntry,
                                       ref runtime);
        return true;
    }
    #endregion

    #region Runtime Payload
    /// <summary>
    /// Writes the stolen catalog-only passive payload into the Stealer runtime entry.
    /// </summary>
    /// <param name="playerEntity">Player entity that owned the stolen passive.</param>
    /// <param name="catalogIndex">Original unlock catalog index used by recovery when possible.</param>
    /// <param name="originalUnlockCount">Ownership count captured before the Stealer removed the passive.</param>
    /// <param name="catalogEntry">Catalog entry that supplied the stolen PowerUpId.</param>
    /// <param name="runtime">Mutable Stealer runtime entry receiving passive payload metadata.</param>
    private static void StoreCatalogOnlyPassivePayload(Entity playerEntity,
                                                       int catalogIndex,
                                                       int originalUnlockCount,
                                                       in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                                       ref EnemyPowerUpStealerRuntimeElement runtime)
    {
        runtime.HasStolenPowerUp = 1;
        runtime.StolenKind = PlayerPowerUpUnlockKind.Passive;
        runtime.PowerUpId = catalogEntry.PowerUpId;
        runtime.StoredActivePowerUp = default;
        runtime.StoredPassiveTool = catalogEntry.PassiveToolConfig;
        runtime.OriginalActiveSlotIndex = -1;
        runtime.OriginalActiveEquipOrder = 0;
        runtime.OriginalPassiveCatalogIndex = catalogIndex;
        runtime.OriginalPassiveBufferIndex = -1;
        runtime.OriginalPassiveUnlockCount = originalUnlockCount;
        runtime.PlayerEntity = playerEntity;
    }
    #endregion

    #endregion
}
