using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves within-category Power-Up Stealer targets without allocating temporary runtime collections.
/// </summary>
internal static class EnemyPowerUpStealerSelectionUtility
{
    #region Methods

    #region Active Selection
    /// <summary>
    /// Resolves the active slot selected for stealing according to the configured within-category mode.
    /// </summary>
    /// <param name="powerUpsConfig">Current player active loadout.</param>
    /// <param name="powerUpsState">Current player active runtime state containing equip-order markers.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <param name="selectionMode">Within-category selection mode configured by the module.</param>
    /// <returns>0 for primary, 1 for secondary, or -1 when no active slot exists.</returns>
    public static int ResolveActiveSlotToSteal(in PlayerPowerUpsConfig powerUpsConfig,
                                               in PlayerPowerUpsState powerUpsState,
                                               DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                               float acquisitionStealCooldownSeconds,
                                               float elapsedTime,
                                               Entity enemyEntity,
                                               in EnemyRuntimeState enemyRuntimeState,
                                               int stealerIndex,
                                               EnemyPowerUpStealSelectionMode selectionMode)
    {
        return ResolveActiveSlotToSteal(in powerUpsConfig.PrimarySlot,
                                        in powerUpsConfig.SecondarySlot,
                                        in powerUpsState,
                                        unlockCatalog,
                                        acquisitionStealCooldownSeconds,
                                        elapsedTime,
                                        enemyEntity,
                                        in enemyRuntimeState,
                                        stealerIndex,
                                        selectionMode);
    }

    /// <summary>
    /// Resolves the active slot selected for stealing from direct slot payloads.
    /// </summary>
    /// <param name="primarySlotConfig">Current primary active slot payload.</param>
    /// <param name="secondarySlotConfig">Current secondary active slot payload.</param>
    /// <param name="powerUpsState">Current player active runtime state containing equip-order markers.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <param name="selectionMode">Within-category selection mode configured by the module.</param>
    /// <returns>0 for primary, 1 for secondary, or -1 when no active slot exists.</returns>
    public static int ResolveActiveSlotToSteal(in PlayerPowerUpSlotConfig primarySlotConfig,
                                               in PlayerPowerUpSlotConfig secondarySlotConfig,
                                               in PlayerPowerUpsState powerUpsState,
                                               DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                               float acquisitionStealCooldownSeconds,
                                               float elapsedTime,
                                               Entity enemyEntity,
                                               in EnemyRuntimeState enemyRuntimeState,
                                               int stealerIndex,
                                               EnemyPowerUpStealSelectionMode selectionMode)
    {
        bool hasPrimary = IsActiveSlotEligibleForSteal(in primarySlotConfig,
                                                       unlockCatalog,
                                                       acquisitionStealCooldownSeconds,
                                                       elapsedTime);
        bool hasSecondary = IsActiveSlotEligibleForSteal(in secondarySlotConfig,
                                                         unlockCatalog,
                                                         acquisitionStealCooldownSeconds,
                                                         elapsedTime);

        if (!hasPrimary && !hasSecondary)
            return -1;

        switch (selectionMode)
        {
            case EnemyPowerUpStealSelectionMode.LastObtained:
                return ResolveOrderedActiveSlot(hasPrimary, hasSecondary, in powerUpsState, false);

            case EnemyPowerUpStealSelectionMode.Random:
                return ResolveRandomActiveSlot(hasPrimary, hasSecondary, enemyEntity, in enemyRuntimeState, stealerIndex);

            default:
                return ResolveOrderedActiveSlot(hasPrimary, hasSecondary, in powerUpsState, true);
        }
    }

    /// <summary>
    /// Reads the equip-order marker currently assigned to one active slot.
    /// </summary>
    /// <param name="slotIndex">Slot index to inspect. 0 is primary and 1 is secondary.</param>
    /// <param name="powerUpsState">Runtime active state containing equip-order markers.</param>
    /// <returns>Positive equip order when available; otherwise 0.</returns>
    public static int ResolveActiveSlotEquipOrder(int slotIndex, in PlayerPowerUpsState powerUpsState)
    {
        switch (slotIndex)
        {
            case 0:
                return math.max(0, powerUpsState.PrimaryEquipOrder);

            case 1:
                return math.max(0, powerUpsState.SecondaryEquipOrder);

            default:
                return 0;
        }
    }

    /// <summary>
    /// Resolves the first or last active slot using equip-order metadata.
    /// </summary>
    /// <param name="hasPrimary">True when the primary slot currently contains a defined power-up.</param>
    /// <param name="hasSecondary">True when the secondary slot currently contains a defined power-up.</param>
    /// <param name="powerUpsState">Runtime active slot state containing order metadata.</param>
    /// <param name="preferFirst">True to select the oldest active; false to select the newest active.</param>
    /// <returns>0 for primary, 1 for secondary, or -1 when no active slot exists.</returns>
    private static int ResolveOrderedActiveSlot(bool hasPrimary,
                                                bool hasSecondary,
                                                in PlayerPowerUpsState powerUpsState,
                                                bool preferFirst)
    {
        if (hasPrimary && !hasSecondary)
            return 0;

        if (!hasPrimary && hasSecondary)
            return 1;

        if (!hasPrimary && !hasSecondary)
            return -1;

        int primaryOrder = powerUpsState.PrimaryEquipOrder > 0 ? powerUpsState.PrimaryEquipOrder : 1;
        int secondaryOrder = powerUpsState.SecondaryEquipOrder > 0 ? powerUpsState.SecondaryEquipOrder : 2;

        if (preferFirst)
            return primaryOrder <= secondaryOrder ? 0 : 1;

        return primaryOrder > secondaryOrder ? 0 : 1;
    }

    /// <summary>
    /// Resolves one active slot by deterministic random sampling across currently defined active slots.
    /// </summary>
    /// <param name="hasPrimary">True when the primary slot currently contains a defined power-up.</param>
    /// <param name="hasSecondary">True when the secondary slot currently contains a defined power-up.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <returns>0 for primary, 1 for secondary, or -1 when no active slot exists.</returns>
    private static int ResolveRandomActiveSlot(bool hasPrimary,
                                               bool hasSecondary,
                                               Entity enemyEntity,
                                               in EnemyRuntimeState enemyRuntimeState,
                                               int stealerIndex)
    {
        if (hasPrimary && hasSecondary)
        {
            uint seed = BuildSelectionSeed(enemyEntity, in enemyRuntimeState, stealerIndex, 0xA31723u);
            return (seed & 1u) == 0u ? 0 : 1;
        }

        if (hasPrimary)
            return 0;

        if (hasSecondary)
            return 1;

        return -1;
    }
    #endregion

    #region Passive Selection
    /// <summary>
    /// Resolves the passive buffer index selected for stealing according to the configured within-category mode.
    /// </summary>
    /// <param name="equippedPassiveTools">Equipped passive buffer scanned for eligible entries.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <param name="selectionMode">Within-category selection mode configured by the module.</param>
    /// <returns>Selected passive buffer index, or -1 when no valid passive can be stolen.</returns>
    public static int ResolvePassiveIndexToSteal(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                 DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                 float acquisitionStealCooldownSeconds,
                                                 float elapsedTime,
                                                 Entity enemyEntity,
                                                 in EnemyRuntimeState enemyRuntimeState,
                                                 int stealerIndex,
                                                 EnemyPowerUpStealSelectionMode selectionMode)
    {
        switch (selectionMode)
        {
            case EnemyPowerUpStealSelectionMode.LastObtained:
                return ResolveLastEligiblePassiveIndex(equippedPassiveTools,
                                                       unlockCatalog,
                                                       acquisitionStealCooldownSeconds,
                                                       elapsedTime);

            case EnemyPowerUpStealSelectionMode.Random:
                return ResolveRandomEligiblePassiveIndex(equippedPassiveTools,
                                                         unlockCatalog,
                                                         acquisitionStealCooldownSeconds,
                                                         elapsedTime,
                                                         enemyEntity,
                                                         in enemyRuntimeState,
                                                         stealerIndex);

            default:
                return ResolveFirstEligiblePassiveIndex(equippedPassiveTools,
                                                        unlockCatalog,
                                                        acquisitionStealCooldownSeconds,
                                                        elapsedTime);
        }
    }

    /// <summary>
    /// Resolves a catalog-only passive selected for stealing when no equipped passive tool represents it.
    /// </summary>
    /// <param name="unlockCatalog">Player unlock catalog scanned for owned passive entries.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to exclude tool-backed passives already handled by the primary path.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <param name="selectionMode">Within-category selection mode configured by the module.</param>
    /// <returns>Selected unlock catalog index, or -1 when no catalog-only passive can be stolen.</returns>
    public static int ResolvePassiveCatalogIndexToSteal(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                        float acquisitionStealCooldownSeconds,
                                                        float elapsedTime,
                                                        Entity enemyEntity,
                                                        in EnemyRuntimeState enemyRuntimeState,
                                                        int stealerIndex,
                                                        EnemyPowerUpStealSelectionMode selectionMode)
    {
        switch (selectionMode)
        {
            case EnemyPowerUpStealSelectionMode.LastObtained:
                return ResolveLastEligiblePassiveCatalogIndex(unlockCatalog,
                                                              equippedPassiveTools,
                                                              acquisitionStealCooldownSeconds,
                                                              elapsedTime);

            case EnemyPowerUpStealSelectionMode.Random:
                return ResolveRandomEligiblePassiveCatalogIndex(unlockCatalog,
                                                                equippedPassiveTools,
                                                                acquisitionStealCooldownSeconds,
                                                                elapsedTime,
                                                                enemyEntity,
                                                                in enemyRuntimeState,
                                                                stealerIndex);

            default:
                return ResolveFirstEligiblePassiveCatalogIndex(unlockCatalog,
                                                               equippedPassiveTools,
                                                               acquisitionStealCooldownSeconds,
                                                               elapsedTime);
        }
    }

    /// <summary>
    /// Finds the oldest eligible passive in acquisition-buffer order.
    /// </summary>
    /// <param name="equippedPassiveTools">Equipped passive buffer scanned from front to back.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>First eligible passive index, or -1 when none exists.</returns>
    private static int ResolveFirstEligiblePassiveIndex(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                        float acquisitionStealCooldownSeconds,
                                                        float elapsedTime)
    {
        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

            if (!IsPassiveEligibleForSteal(in passiveTool,
                                           unlockCatalog,
                                           acquisitionStealCooldownSeconds,
                                           elapsedTime))
                continue;

            return passiveIndex;
        }

        return -1;
    }

    /// <summary>
    /// Finds the oldest catalog-only passive in catalog acquisition order.
    /// </summary>
    /// <param name="unlockCatalog">Unlock catalog scanned from front to back.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to exclude tool-backed passives.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>First eligible catalog index, or -1 when none exists.</returns>
    private static int ResolveFirstEligiblePassiveCatalogIndex(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                               DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                               float acquisitionStealCooldownSeconds,
                                                               float elapsedTime)
    {
        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (!IsPassiveCatalogEntryEligibleForSteal(in catalogEntry,
                                                       equippedPassiveTools,
                                                       acquisitionStealCooldownSeconds,
                                                       elapsedTime))
                continue;

            return catalogIndex;
        }

        return -1;
    }

    /// <summary>
    /// Finds the newest eligible passive in acquisition-buffer order.
    /// </summary>
    /// <param name="equippedPassiveTools">Equipped passive buffer scanned from back to front.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>Last eligible passive index, or -1 when none exists.</returns>
    private static int ResolveLastEligiblePassiveIndex(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                       DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                       float acquisitionStealCooldownSeconds,
                                                       float elapsedTime)
    {
        for (int passiveIndex = equippedPassiveTools.Length - 1; passiveIndex >= 0; passiveIndex--)
        {
            ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

            if (!IsPassiveEligibleForSteal(in passiveTool,
                                           unlockCatalog,
                                           acquisitionStealCooldownSeconds,
                                           elapsedTime))
                continue;

            return passiveIndex;
        }

        return -1;
    }

    /// <summary>
    /// Finds the newest catalog-only passive in catalog acquisition order.
    /// </summary>
    /// <param name="unlockCatalog">Unlock catalog scanned from back to front.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to exclude tool-backed passives.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>Last eligible catalog index, or -1 when none exists.</returns>
    private static int ResolveLastEligiblePassiveCatalogIndex(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                              float acquisitionStealCooldownSeconds,
                                                              float elapsedTime)
    {
        for (int catalogIndex = unlockCatalog.Length - 1; catalogIndex >= 0; catalogIndex--)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (!IsPassiveCatalogEntryEligibleForSteal(in catalogEntry,
                                                       equippedPassiveTools,
                                                       acquisitionStealCooldownSeconds,
                                                       elapsedTime))
                continue;

            return catalogIndex;
        }

        return -1;
    }

    /// <summary>
    /// Samples one eligible passive by deterministic random index without allocating temporary collections.
    /// </summary>
    /// <param name="equippedPassiveTools">Equipped passive buffer scanned for eligible entries.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <returns>Random eligible passive index, or -1 when none exists.</returns>
    private static int ResolveRandomEligiblePassiveIndex(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                         DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                         float acquisitionStealCooldownSeconds,
                                                         float elapsedTime,
                                                         Entity enemyEntity,
                                                         in EnemyRuntimeState enemyRuntimeState,
                                                         int stealerIndex)
    {
        int eligibleCount = CountEligiblePassives(equippedPassiveTools,
                                                  unlockCatalog,
                                                  acquisitionStealCooldownSeconds,
                                                  elapsedTime);

        if (eligibleCount <= 0)
            return -1;

        uint seed = BuildSelectionSeed(enemyEntity, in enemyRuntimeState, stealerIndex, 0xC77131u);
        int targetEligibleIndex = (int)(seed % (uint)eligibleCount);
        int currentEligibleIndex = 0;

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

            if (!IsPassiveEligibleForSteal(in passiveTool,
                                           unlockCatalog,
                                           acquisitionStealCooldownSeconds,
                                           elapsedTime))
                continue;

            if (currentEligibleIndex == targetEligibleIndex)
                return passiveIndex;

            currentEligibleIndex += 1;
        }

        return -1;
    }

    /// <summary>
    /// Samples one eligible catalog-only passive by deterministic random index without allocating temporary collections.
    /// </summary>
    /// <param name="unlockCatalog">Unlock catalog scanned for eligible passive ownership.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to exclude tool-backed passives.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <param name="enemyEntity">Enemy entity used by deterministic random selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic random selection by activation time.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <returns>Random eligible catalog index, or -1 when none exists.</returns>
    private static int ResolveRandomEligiblePassiveCatalogIndex(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                                DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                                float acquisitionStealCooldownSeconds,
                                                                float elapsedTime,
                                                                Entity enemyEntity,
                                                                in EnemyRuntimeState enemyRuntimeState,
                                                                int stealerIndex)
    {
        int eligibleCount = CountEligiblePassiveCatalogEntries(unlockCatalog,
                                                               equippedPassiveTools,
                                                               acquisitionStealCooldownSeconds,
                                                               elapsedTime);

        if (eligibleCount <= 0)
            return -1;

        uint seed = BuildSelectionSeed(enemyEntity, in enemyRuntimeState, stealerIndex, 0xD1B723u);
        int targetEligibleIndex = (int)(seed % (uint)eligibleCount);
        int currentEligibleIndex = 0;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (!IsPassiveCatalogEntryEligibleForSteal(in catalogEntry,
                                                       equippedPassiveTools,
                                                       acquisitionStealCooldownSeconds,
                                                       elapsedTime))
                continue;

            if (currentEligibleIndex == targetEligibleIndex)
                return catalogIndex;

            currentEligibleIndex += 1;
        }

        return -1;
    }

    /// <summary>
    /// Counts valid passives available for Stealer selection.
    /// </summary>
    /// <param name="equippedPassiveTools">Equipped passive buffer to scan.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>Number of eligible passive entries.</returns>
    private static int CountEligiblePassives(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                             DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                             float acquisitionStealCooldownSeconds,
                                             float elapsedTime)
    {
        int eligibleCount = 0;

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            ref EquippedPassiveToolElement passiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);

            if (!IsPassiveEligibleForSteal(in passiveTool,
                                           unlockCatalog,
                                           acquisitionStealCooldownSeconds,
                                           elapsedTime))
                continue;

            eligibleCount += 1;
        }

        return eligibleCount;
    }

    /// <summary>
    /// Counts owned passive catalog entries that are not represented by an equipped passive tool.
    /// </summary>
    /// <param name="unlockCatalog">Unlock catalog to scan.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to exclude already handled passives.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>Number of eligible catalog-only passive entries.</returns>
    private static int CountEligiblePassiveCatalogEntries(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                          DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                          float acquisitionStealCooldownSeconds,
                                                          float elapsedTime)
    {
        int eligibleCount = 0;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (!IsPassiveCatalogEntryEligibleForSteal(in catalogEntry,
                                                       equippedPassiveTools,
                                                       acquisitionStealCooldownSeconds,
                                                       elapsedTime))
                continue;

            eligibleCount += 1;
        }

        return eligibleCount;
    }

    /// <summary>
    /// Checks whether a passive entry has a stable PowerUpId, including catalog-driven passives without a runtime tool.
    /// </summary>
    /// <param name="passive">Passive entry being inspected.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to ignore protected recent acquisitions.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>True when the passive can be identified, stolen, and restored.</returns>
    private static bool IsPassiveEligibleForSteal(in EquippedPassiveToolElement passive,
                                                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                  float acquisitionStealCooldownSeconds,
                                                  float elapsedTime)
    {
        if (passive.PowerUpId.Length <= 0)
            return false;

        return !PlayerPowerUpStealCooldownRuntimeUtility.IsPowerUpProtectedFromSteal(passive.PowerUpId,
                                                                                     PlayerPowerUpUnlockKind.Passive,
                                                                                     unlockCatalog,
                                                                                     acquisitionStealCooldownSeconds,
                                                                                     elapsedTime);
    }

    /// <summary>
    /// Checks whether a passive catalog entry is owned and needs the catalog-only Stealer path.
    /// </summary>
    /// <param name="catalogEntry">Unlock catalog entry inspected for ownership and payload kind.</param>
    /// <param name="equippedPassiveTools">Equipped passive buffer used to exclude passives already handled by tool removal.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>True when the entry can be stolen through catalog ownership mutation.</returns>
    private static bool IsPassiveCatalogEntryEligibleForSteal(in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                              float acquisitionStealCooldownSeconds,
                                                              float elapsedTime)
    {
        if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
            return false;

        if (catalogEntry.PowerUpId.Length <= 0)
            return false;

        if (catalogEntry.CurrentUnlockCount <= 0)
            return false;

        if (PlayerPowerUpStealCooldownRuntimeUtility.IsPowerUpProtectedFromSteal(in catalogEntry,
                                                                                 acquisitionStealCooldownSeconds,
                                                                                 elapsedTime))
            return false;

        return !EnemyPowerUpStealerRuntimeUtility.ContainsPassivePowerUp(catalogEntry.PowerUpId, equippedPassiveTools);
    }
    #endregion

    #region Protection
    /// <summary>
    /// Checks whether one active slot is defined and outside the anti-steal acquisition cooldown.
    /// </summary>
    /// <param name="slotConfig">Active slot config to inspect.</param>
    /// <param name="unlockCatalog">Player unlock catalog used to resolve acquisition time.</param>
    /// <param name="acquisitionStealCooldownSeconds">Cooldown duration applied per recently acquired power-up.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time.</param>
    /// <returns>True when the active slot can be stolen.</returns>
    private static bool IsActiveSlotEligibleForSteal(in PlayerPowerUpSlotConfig slotConfig,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     float acquisitionStealCooldownSeconds,
                                                     float elapsedTime)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.PowerUpId.Length <= 0)
            return true;

        return !PlayerPowerUpStealCooldownRuntimeUtility.IsPowerUpProtectedFromSteal(slotConfig.PowerUpId,
                                                                                     PlayerPowerUpUnlockKind.Active,
                                                                                     unlockCatalog,
                                                                                     acquisitionStealCooldownSeconds,
                                                                                     elapsedTime);
    }
    #endregion

    #region Seed
    /// <summary>
    /// Builds a deterministic seed for within-category random Stealer choices.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity owning the Stealer module.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary the seed by activation timing.</param>
    /// <param name="stealerIndex">Stealer module index used to decorrelate sibling modules.</param>
    /// <param name="salt">Call-site salt separating active and passive selection streams.</param>
    /// <returns>Deterministic unsigned seed.</returns>
    private static uint BuildSelectionSeed(Entity enemyEntity,
                                           in EnemyRuntimeState enemyRuntimeState,
                                           int stealerIndex,
                                           uint salt)
    {
        return math.hash(new uint4((uint)enemyEntity.Index,
                                   (uint)enemyEntity.Version,
                                   math.asuint(enemyRuntimeState.LifetimeSeconds),
                                   (uint)math.max(0, stealerIndex) ^ salt));
    }
    #endregion

    #endregion
}
