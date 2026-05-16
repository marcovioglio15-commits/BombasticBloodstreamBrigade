using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Reconciles Character Tuning applications owned by runtime-scoped active power-ups and by currently owned passive power-ups.
/// </summary>
internal static class PlayerPowerUpChargeCharacterTuningRuntimeUtility
{
    #region Constants
    private const uint PassiveSignatureSeed = 2166136261u;
    private const uint PassiveSignaturePrime = 16777619u;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Activates, refreshes, or restores temporary Character Tuning overlays based on the current runtime-scoped ownership state.
    /// </summary>
    /// <param name="primarySlotConfig">Primary active-slot config inspected for runtime-scoped Character Tuning.</param>
    /// <param name="secondarySlotConfig">Secondary active-slot config inspected for runtime-scoped Character Tuning.</param>
    /// <param name="primaryShouldBeActive">True when the primary slot should keep its temporary Character Tuning applied.</param>
    /// <param name="secondaryShouldBeActive">True when the secondary slot should keep its temporary Character Tuning applied.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog used to resolve Character Tuning formulas by PowerUpId.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer referenced by the unlock catalog.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer receiving temporary runtime-scoped overrides.</param>
    /// <param name="progressionConfig">Runtime progression config used to resynchronize dependent progression state.</param>
    /// <param name="chargeCharacterTuningState">Mutable slot-ownership state for temporary Character Tuning.</param>
    /// <param name="baseStats">Mutable snapshot buffer storing baseline values for stats touched by temporary runtime-scoped overrides.</param>
    /// <param name="playerExperience">Mutable runtime experience component synchronized after reconciliation.</param>
    /// <param name="playerLevel">Mutable runtime level component synchronized after reconciliation.</param>
    /// <param name="playerExperienceCollection">Mutable runtime pickup-radius component synchronized after reconciliation.</param>
    /// <returns>True when the reconciliation changed at least one scalable stat; otherwise false.</returns>
    public static bool ReconcileScopedCharacterTuning(in PlayerPowerUpSlotConfig primarySlotConfig,
                                                      in PlayerPowerUpSlotConfig secondarySlotConfig,
                                                      bool primaryShouldBeActive,
                                                      bool secondaryShouldBeActive,
                                                      DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                      DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                      DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                      PlayerProgressionConfig progressionConfig,
                                                      DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                      ref PlayerChargeCharacterTuningState chargeCharacterTuningState,
                                                      DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats,
                                                      ref PlayerExperience playerExperience,
                                                      ref PlayerLevel playerLevel,
                                                      ref PlayerExperienceCollection playerExperienceCollection)
    {
        bool primaryWasApplied = chargeCharacterTuningState.PrimaryIsApplied != 0;
        bool secondaryWasApplied = chargeCharacterTuningState.SecondaryIsApplied != 0;
        uint previousPrimaryOwnershipSignature = chargeCharacterTuningState.PrimaryOwnershipSignature;
        uint previousSecondaryOwnershipSignature = chargeCharacterTuningState.SecondaryOwnershipSignature;
        uint previousPassiveOwnershipSignature = chargeCharacterTuningState.PassiveOwnershipSignature;
        uint passiveOwnershipSignature = BuildPassiveOwnershipSignature(unlockCatalog);

        if (!primaryWasApplied &&
            !secondaryWasApplied &&
            !primaryShouldBeActive &&
            !secondaryShouldBeActive &&
            previousPassiveOwnershipSignature == 0u &&
            passiveOwnershipSignature == 0u)
        {
            if (baseStats.IsCreated && baseStats.Length > 0)
                baseStats.Clear();

            return false;
        }

        PlayerPowerUpUnlockCatalogElement primaryCatalogEntry = default;
        PlayerPowerUpUnlockCatalogElement secondaryCatalogEntry = default;
        bool primaryCanBeApplied = primaryShouldBeActive &&
                                   TryResolveScopedCatalogEntry(in primarySlotConfig,
                                                                unlockCatalog,
                                                                out primaryCatalogEntry);
        bool secondaryCanBeApplied = secondaryShouldBeActive &&
                                     TryResolveScopedCatalogEntry(in secondarySlotConfig,
                                                                  unlockCatalog,
                                                                  out secondaryCatalogEntry);
        uint primaryOwnershipSignature = BuildScopedOwnershipSignature(primaryCanBeApplied, in primaryCatalogEntry);
        uint secondaryOwnershipSignature = BuildScopedOwnershipSignature(secondaryCanBeApplied, in secondaryCatalogEntry);
        bool primaryOwnershipChanged = previousPrimaryOwnershipSignature != primaryOwnershipSignature;
        bool secondaryOwnershipChanged = previousSecondaryOwnershipSignature != secondaryOwnershipSignature;
        bool passiveOwnershipChanged = previousPassiveOwnershipSignature != passiveOwnershipSignature;

        if (primaryWasApplied == primaryCanBeApplied &&
            secondaryWasApplied == secondaryCanBeApplied &&
            !primaryOwnershipChanged &&
            !secondaryOwnershipChanged &&
            !passiveOwnershipChanged)
        {
            return false;
        }

        if (primaryCanBeApplied && !primaryWasApplied)
            CaptureMissingBaseStats(in primaryCatalogEntry, characterTuningFormulas, scalableStats, baseStats);

        if (secondaryCanBeApplied && !secondaryWasApplied)
            CaptureMissingBaseStats(in secondaryCatalogEntry, characterTuningFormulas, scalableStats, baseStats);

        if (passiveOwnershipSignature != 0u)
            CaptureMissingPassiveBaseStats(unlockCatalog, characterTuningFormulas, scalableStats, baseStats);

        bool anyScalableStatChanged = RestoreBaseStats(baseStats, scalableStats);
        chargeCharacterTuningState.PrimaryIsApplied = primaryCanBeApplied ? (byte)1 : (byte)0;
        chargeCharacterTuningState.SecondaryIsApplied = secondaryCanBeApplied ? (byte)1 : (byte)0;
        chargeCharacterTuningState.PrimaryOwnershipSignature = primaryOwnershipSignature;
        chargeCharacterTuningState.SecondaryOwnershipSignature = secondaryOwnershipSignature;
        chargeCharacterTuningState.PassiveOwnershipSignature = passiveOwnershipSignature;

        if (ApplyOwnedPassiveCharacterTuning(unlockCatalog, characterTuningFormulas, scalableStats))
            anyScalableStatChanged = true;

        if (ApplyScopedCharacterTuning(in primaryCatalogEntry,
                                       primaryCanBeApplied,
                                       characterTuningFormulas,
                                       scalableStats))
        {
            anyScalableStatChanged = true;
        }

        if (ApplyScopedCharacterTuning(in secondaryCatalogEntry,
                                       secondaryCanBeApplied,
                                       characterTuningFormulas,
                                       scalableStats))
        {
            anyScalableStatChanged = true;
        }

        if (primaryCanBeApplied || secondaryCanBeApplied || passiveOwnershipSignature != 0u)
            PruneUnusedBaseStats(baseStats,
                                 in primaryCatalogEntry,
                                 primaryCanBeApplied,
                                 in secondaryCatalogEntry,
                                 secondaryCanBeApplied,
                                 unlockCatalog,
                                 characterTuningFormulas);
        else
            baseStats.Clear();

        if (!anyScalableStatChanged)
            return false;

        PlayerPowerUpCharacterTuningRuntimeUtility.SyncProgressionRuntimeState(scalableStats,
                                                                               progressionConfig,
                                                                               runtimeGamePhases,
                                                                               ref playerExperience,
                                                                               ref playerLevel,
                                                                               ref playerExperienceCollection);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the unlock-catalog entry backing one runtime-scoped active slot when it owns temporary Character Tuning.
    /// </summary>
    /// <param name="slotConfig">Active-slot config inspected by PowerUpId.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for the matching entry.</param>
    /// <param name="catalogEntry">Matching runtime-scoped Character Tuning entry when found.</param>
    /// <returns>True when the slot maps to a runtime-scoped Character Tuning entry.</returns>
    private static bool TryResolveScopedCatalogEntry(in PlayerPowerUpSlotConfig slotConfig,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     out PlayerPowerUpUnlockCatalogElement catalogEntry)
    {
        catalogEntry = default;

        if (slotConfig.IsDefined == 0)
            return false;

        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0 || slotConfig.PowerUpId.Length <= 0)
            return false;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement candidate = unlockCatalog[catalogIndex];

            if (candidate.PowerUpId != slotConfig.PowerUpId)
                continue;

            if (!PlayerPowerUpCharacterTuningRuntimeUtility.IsRuntimeScopedCharacterTuning(in candidate))
                return false;

            catalogEntry = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Captures baseline values for every target stat touched by one runtime-scoped Character Tuning entry.
    /// </summary>
    /// <param name="catalogEntry">Runtime-scoped Character Tuning entry whose target stats need a baseline snapshot.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Current scalable-stat buffer used as snapshot source.</param>
    /// <param name="baseStats">Snapshot buffer that receives any still-missing target stat values.</param>
    private static void CaptureMissingBaseStats(in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                                DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats)
    {
        int startIndex = Math.Max(0, catalogEntry.CharacterTuningFormulaStartIndex);
        int endIndex = Math.Min(characterTuningFormulas.Length, startIndex + Math.Max(0, catalogEntry.CharacterTuningFormulaCount));

        for (int formulaIndex = startIndex; formulaIndex < endIndex; formulaIndex++)
        {
            string formula = characterTuningFormulas[formulaIndex].Formula.ToString();

            if (!PlayerPowerUpCharacterTuningRuntimeUtility.TryResolveTargetStatName(formula, out string targetStatName))
                continue;

            if (HasBaseStatSnapshot(baseStats, targetStatName))
                continue;

            int scalableStatIndex = PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(scalableStats, targetStatName);

            if (scalableStatIndex < 0)
                continue;

            PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
            baseStats.Add(new PlayerChargeCharacterTuningBaseStatElement
            {
                Name = scalableStat.Name,
                Type = scalableStat.Type,
                Value = scalableStat.Value,
                BooleanValue = scalableStat.BooleanValue,
                TokenValue = scalableStat.TokenValue
            });
        }
    }

    /// <summary>
    /// Captures baseline values for every stat targeted by currently owned passive Character Tuning entries.
    /// </summary>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for owned passive Character Tuning entries.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Current scalable-stat buffer used as snapshot source.</param>
    /// <param name="baseStats">Snapshot buffer that receives any still-missing target stat values.</param>
    private static void CaptureMissingPassiveBaseStats(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                       DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                       DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                       DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats)
    {
        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0)
            return;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

            if (!IsPassiveScopedCharacterTuningOwned(in catalogEntry))
                continue;

            CaptureMissingBaseStats(in catalogEntry, characterTuningFormulas, scalableStats, baseStats);
        }
    }

    /// <summary>
    /// Restores every captured baseline stat value before active runtime-scoped Character Tuning overlays are reapplied.
    /// </summary>
    /// <param name="baseStats">Snapshot buffer storing baseline values.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer restored in place.</param>
    /// <returns>True when at least one scalable stat is restored.</returns>
    private static bool RestoreBaseStats(DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats,
                                         DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        if (!baseStats.IsCreated || baseStats.Length <= 0)
            return false;

        bool anyChanged = false;

        for (int baseStatIndex = 0; baseStatIndex < baseStats.Length; baseStatIndex++)
        {
            PlayerChargeCharacterTuningBaseStatElement baseStat = baseStats[baseStatIndex];
            int scalableStatIndex = PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(scalableStats, baseStat.Name.ToString());

            if (scalableStatIndex < 0)
                continue;

            PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
            PlayerFormulaValue currentValue = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
            PlayerFormulaValue baseValue = ResolveBaseStatValue(in baseStat);

            if (PlayerFormulaValue.AreEqual(in currentValue, in baseValue))
                continue;

            if (!PlayerScalableStatValueUtility.TryWriteRuntimeValue(ref scalableStat, baseValue, out string _))
                continue;

            scalableStats[scalableStatIndex] = scalableStat;
            anyChanged = true;
        }

        return anyChanged;
    }

    /// <summary>
    /// Removes baseline snapshots that are no longer needed by any still-active runtime-scoped Character Tuning overlay.
    /// </summary>
    /// <param name="baseStats">Snapshot buffer pruned in place.</param>
    /// <param name="primaryCatalogEntry">Primary runtime-scoped Character Tuning entry when active.</param>
    /// <param name="primaryIsActive">True when the primary runtime-scoped Character Tuning overlay remains active.</param>
    /// <param name="secondaryCatalogEntry">Secondary runtime-scoped Character Tuning entry when active.</param>
    /// <param name="secondaryIsActive">True when the secondary runtime-scoped Character Tuning overlay remains active.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    private static void PruneUnusedBaseStats(DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats,
                                             in PlayerPowerUpUnlockCatalogElement primaryCatalogEntry,
                                             bool primaryIsActive,
                                             in PlayerPowerUpUnlockCatalogElement secondaryCatalogEntry,
                                             bool secondaryIsActive,
                                             DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                             DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas)
    {
        for (int baseStatIndex = 0; baseStatIndex < baseStats.Length; baseStatIndex++)
        {
            string statName = baseStats[baseStatIndex].Name.ToString();
            bool statStillNeeded = false;

            if (primaryIsActive)
                statStillNeeded = IsStatTargetedByEntry(statName, in primaryCatalogEntry, characterTuningFormulas);

            if (!statStillNeeded && secondaryIsActive)
                statStillNeeded = IsStatTargetedByEntry(statName, in secondaryCatalogEntry, characterTuningFormulas);

            if (!statStillNeeded)
                statStillNeeded = IsStatTargetedByOwnedPassiveEntries(statName, unlockCatalog, characterTuningFormulas);

            if (statStillNeeded)
                continue;

            baseStats.RemoveAt(baseStatIndex);
            baseStatIndex--;
        }
    }

    /// <summary>
    /// Checks whether one stat name already has a captured baseline snapshot.
    /// </summary>
    /// <param name="baseStats">Snapshot buffer inspected for the requested stat.</param>
    /// <param name="statName">Scalable-stat name to resolve.</param>
    /// <returns>True when a snapshot already exists for the stat.</returns>
    private static bool HasBaseStatSnapshot(DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats, string statName)
    {
        for (int baseStatIndex = 0; baseStatIndex < baseStats.Length; baseStatIndex++)
        {
            if (!string.Equals(baseStats[baseStatIndex].Name.ToString(), statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one stable signature describing the currently applied runtime-scoped active Character Tuning ownership.
    /// </summary>
    /// <param name="canBeApplied">True when the active runtime-scoped Character Tuning is currently active.</param>
    /// <param name="catalogEntry">Unlock catalog entry backing the active runtime-scoped Character Tuning.</param>
    /// <returns>Stable non-zero signature while active, or zero when the scoped Character Tuning is inactive.</returns>
    private static uint BuildScopedOwnershipSignature(bool canBeApplied, in PlayerPowerUpUnlockCatalogElement catalogEntry)
    {
        if (!canBeApplied)
            return 0u;

        uint signature = PassiveSignatureSeed;
        FixedString64Bytes powerUpId = catalogEntry.PowerUpId;

        for (int characterIndex = 0; characterIndex < powerUpId.Length; characterIndex++)
            signature = (signature ^ powerUpId[characterIndex]) * PassiveSignaturePrime;

        signature = (signature ^ (uint)ResolveScopedApplicationCount(in catalogEntry)) * PassiveSignaturePrime;
        return signature;
    }

    /// <summary>
    /// Resolves the ownership signature of all passive Character Tuning entries currently applied through unlock counts.
    /// </summary>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for owned passive Character Tuning entries.</param>
    /// <returns>Stable signature for the currently owned passive Character Tuning set, or zero when none are owned.</returns>
    private static uint BuildPassiveOwnershipSignature(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog)
    {
        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0)
            return 0u;

        uint signature = PassiveSignatureSeed;
        bool hasAnyOwnedPassive = false;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

            if (!IsPassiveScopedCharacterTuningOwned(in catalogEntry))
                continue;

            hasAnyOwnedPassive = true;
            signature = (signature ^ (uint)(catalogIndex + 1)) * PassiveSignaturePrime;
            signature = (signature ^ (uint)Math.Max(0, catalogEntry.CurrentUnlockCount)) * PassiveSignaturePrime;
        }

        if (!hasAnyOwnedPassive)
            return 0u;

        return signature;
    }

    /// <summary>
    /// Applies currently owned passive Character Tuning entries as many times as their unlock count indicates.
    /// </summary>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for owned passive Character Tuning entries.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer receiving passive Character Tuning overlays.</param>
    /// <returns>True when at least one passive Character Tuning formula changed runtime scalable stats.</returns>
    private static bool ApplyOwnedPassiveCharacterTuning(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                         DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                         DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0)
            return false;

        bool anyChanged = false;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

            if (!IsPassiveScopedCharacterTuningOwned(in catalogEntry))
                continue;

            int applicationCount = Math.Max(0, catalogEntry.CurrentUnlockCount);

            for (int applicationIndex = 0; applicationIndex < applicationCount; applicationIndex++)
            {
                if (!PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningFormulas(in catalogEntry,
                                                                                               characterTuningFormulas,
                                                                                               scalableStats,
                                                                                               out int appliedFormulaCount))
                {
                    continue;
                }

                anyChanged = anyChanged || appliedFormulaCount > 0;
            }
        }

        return anyChanged;
    }

    /// <summary>
    /// Applies one runtime-scoped active Character Tuning entry as many times as its current unlock count indicates.
    /// </summary>
    /// <param name="catalogEntry">Runtime-scoped active Character Tuning entry currently applied.</param>
    /// <param name="canBeApplied">True when the runtime-scoped Character Tuning is currently active.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer receiving the scoped runtime overlay.</param>
    /// <returns>True when at least one formula changed runtime scalable stats.</returns>
    private static bool ApplyScopedCharacterTuning(in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                                   bool canBeApplied,
                                                   DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                   DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        if (!canBeApplied)
            return false;

        bool anyChanged = false;
        int applicationCount = ResolveScopedApplicationCount(in catalogEntry);

        for (int applicationIndex = 0; applicationIndex < applicationCount; applicationIndex++)
        {
            if (!PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningFormulas(in catalogEntry,
                                                                                           characterTuningFormulas,
                                                                                           scalableStats,
                                                                                           out int appliedFormulaCount))
            {
                continue;
            }

            anyChanged = anyChanged || appliedFormulaCount > 0;
        }

        return anyChanged;
    }

    /// <summary>
    /// Resolves how many times one runtime-scoped active Character Tuning entry must be applied while active.
    /// </summary>
    /// <param name="catalogEntry">Runtime-scoped active Character Tuning entry inspected for stack count.</param>
    /// <returns>Positive application count matching current ownership.</returns>
    private static int ResolveScopedApplicationCount(in PlayerPowerUpUnlockCatalogElement catalogEntry)
    {
        return Math.Max(1, catalogEntry.CurrentUnlockCount);
    }

    /// <summary>
    /// Resolves whether one unlock-catalog entry represents an owned passive Character Tuning application.
    /// </summary>
    /// <param name="catalogEntry">Unlock catalog entry inspected for passive Character Tuning ownership.</param>
    /// <returns>True when the passive entry currently contributes runtime Character Tuning; otherwise false.</returns>
    private static bool IsPassiveScopedCharacterTuningOwned(in PlayerPowerUpUnlockCatalogElement catalogEntry)
    {
        if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
            return false;

        if (catalogEntry.CharacterTuningFormulaCount <= 0)
            return false;

        return catalogEntry.CurrentUnlockCount > 0;
    }

    /// <summary>
    /// Checks whether one stat is still targeted by any currently owned passive Character Tuning entry.
    /// </summary>
    /// <param name="statName">Requested scalable-stat name.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for owned passive Character Tuning entries.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <returns>True when at least one owned passive Character Tuning entry targets the stat.</returns>
    private static bool IsStatTargetedByOwnedPassiveEntries(string statName,
                                                            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas)
    {
        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0)
            return false;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

            if (!IsPassiveScopedCharacterTuningOwned(in catalogEntry))
                continue;

            if (!IsStatTargetedByEntry(statName, in catalogEntry, characterTuningFormulas))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether one stat is targeted by any assignment inside the provided Character Tuning entry.
    /// </summary>
    /// <param name="statName">Requested scalable-stat name.</param>
    /// <param name="catalogEntry">Character Tuning catalog entry whose assignments are scanned.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <returns>True when the stat is targeted by at least one assignment.</returns>
    private static bool IsStatTargetedByEntry(string statName,
                                              in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                              DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas)
    {
        int startIndex = Math.Max(0, catalogEntry.CharacterTuningFormulaStartIndex);
        int endIndex = Math.Min(characterTuningFormulas.Length, startIndex + Math.Max(0, catalogEntry.CharacterTuningFormulaCount));

        for (int formulaIndex = startIndex; formulaIndex < endIndex; formulaIndex++)
        {
            string formula = characterTuningFormulas[formulaIndex].Formula.ToString();

            if (!PlayerPowerUpCharacterTuningRuntimeUtility.TryResolveTargetStatName(formula, out string targetStatName))
                continue;

            if (!string.Equals(targetStatName, statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the typed baseline value stored inside one temporary Character Tuning snapshot entry.
    /// </summary>
    /// <param name="baseStat">Snapshot entry to convert.</param>
    /// <returns>Typed baseline value used during restore.</returns>
    private static PlayerFormulaValue ResolveBaseStatValue(in PlayerChargeCharacterTuningBaseStatElement baseStat)
    {
        switch ((PlayerScalableStatType)baseStat.Type)
        {
            case PlayerScalableStatType.Boolean:
                return PlayerFormulaValue.CreateBoolean(baseStat.BooleanValue != 0);
            case PlayerScalableStatType.Token:
                return PlayerFormulaValue.CreateToken(baseStat.TokenValue.ToString());
            default:
                return PlayerFormulaValue.CreateNumber(baseStat.Value);
        }
    }
    #endregion

    #endregion
}
