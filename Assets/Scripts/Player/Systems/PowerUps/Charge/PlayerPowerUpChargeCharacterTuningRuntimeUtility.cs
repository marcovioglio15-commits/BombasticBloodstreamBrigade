using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Reconciles Character Tuning applications owned by runtime-scoped active power-ups and by currently owned passive power-ups.
/// </summary>
internal static class PlayerPowerUpChargeCharacterTuningRuntimeUtility
{
    #region Constants
    private const uint PassiveSignatureSeed = 2166136261u;
    private const uint PassiveSignaturePrime = 16777619u;
    private const string ProjectileSizeStatName = "BulletSizeMultiplier";
    private const float MinimumProjectileSizeMultiplier = 0.01f;
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
    /// <param name="projectileSizeMultipliers">Mutable per-power-up projectile-size provenance rebuilt with the tuning overlays.</param>
    /// <param name="passiveToolsState">Mutable passive snapshot receiving the combined embedded projectile-size multiplier.</param>
    /// <param name="playerExperience">Mutable runtime experience component synchronized after reconciliation.</param>
    /// <param name="playerLevel">Mutable runtime level component synchronized after reconciliation.</param>
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
                                                      DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> projectileSizeMultipliers,
                                                      ref PlayerPassiveToolsState passiveToolsState,
                                                      ref PlayerExperience playerExperience,
                                                      ref PlayerLevel playerLevel)
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

            if (projectileSizeMultipliers.IsCreated && projectileSizeMultipliers.Length > 0)
                projectileSizeMultipliers.Clear();

            passiveToolsState.ProjectileSizePowerUpMultiplier = 1f;

            return false;
        }

        int primaryCatalogIndex = -1;
        int secondaryCatalogIndex = -1;
        bool primaryCanBeApplied = primaryShouldBeActive &&
                                   TryResolveScopedCatalogIndex(in primarySlotConfig,
                                                                unlockCatalog,
                                                                out primaryCatalogIndex);
        bool secondaryCanBeApplied = secondaryShouldBeActive &&
                                     TryResolveScopedCatalogIndex(in secondarySlotConfig,
                                                                  unlockCatalog,
                                                                  out secondaryCatalogIndex);
        uint primaryOwnershipSignature = BuildScopedOwnershipSignature(primaryCanBeApplied,
                                                                       unlockCatalog,
                                                                       primaryCatalogIndex);
        uint secondaryOwnershipSignature = BuildScopedOwnershipSignature(secondaryCanBeApplied,
                                                                         unlockCatalog,
                                                                         secondaryCatalogIndex);
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
            CaptureMissingBaseStats(unlockCatalog, primaryCatalogIndex, characterTuningFormulas, scalableStats, baseStats);

        if (secondaryCanBeApplied && !secondaryWasApplied)
            CaptureMissingBaseStats(unlockCatalog, secondaryCatalogIndex, characterTuningFormulas, scalableStats, baseStats);

        if (passiveOwnershipSignature != 0u)
            CaptureMissingPassiveBaseStats(unlockCatalog, characterTuningFormulas, scalableStats, baseStats);

        bool anyScalableStatChanged = RestoreBaseStats(baseStats, scalableStats);
        float projectileSizePowerUpMultiplier = 1f;

        if (projectileSizeMultipliers.IsCreated)
            projectileSizeMultipliers.Clear();

        chargeCharacterTuningState.PrimaryIsApplied = primaryCanBeApplied ? (byte)1 : (byte)0;
        chargeCharacterTuningState.SecondaryIsApplied = secondaryCanBeApplied ? (byte)1 : (byte)0;
        chargeCharacterTuningState.PrimaryOwnershipSignature = primaryOwnershipSignature;
        chargeCharacterTuningState.SecondaryOwnershipSignature = secondaryOwnershipSignature;
        chargeCharacterTuningState.PassiveOwnershipSignature = passiveOwnershipSignature;

        if (ApplyOwnedPassiveCharacterTuning(unlockCatalog,
                                             characterTuningFormulas,
                                             scalableStats,
                                             projectileSizeMultipliers,
                                             ref projectileSizePowerUpMultiplier))
            anyScalableStatChanged = true;

        if (ApplyScopedCharacterTuning(unlockCatalog,
                                       primaryCatalogIndex,
                                       primaryCanBeApplied,
                                       characterTuningFormulas,
                                       scalableStats,
                                       projectileSizeMultipliers,
                                       ref projectileSizePowerUpMultiplier))
        {
            anyScalableStatChanged = true;
        }

        if (ApplyScopedCharacterTuning(unlockCatalog,
                                       secondaryCatalogIndex,
                                       secondaryCanBeApplied,
                                       characterTuningFormulas,
                                       scalableStats,
                                       projectileSizeMultipliers,
                                       ref projectileSizePowerUpMultiplier))
        {
            anyScalableStatChanged = true;
        }

        if (primaryCanBeApplied || secondaryCanBeApplied || passiveOwnershipSignature != 0u)
            PruneUnusedBaseStats(baseStats,
                                 primaryCatalogIndex,
                                 primaryCanBeApplied,
                                 secondaryCatalogIndex,
                                 secondaryCanBeApplied,
                                 unlockCatalog,
                                 characterTuningFormulas);
        else
            baseStats.Clear();

        passiveToolsState.ProjectileSizePowerUpMultiplier = math.max(MinimumProjectileSizeMultiplier,
                                                                      projectileSizePowerUpMultiplier);

        if (!anyScalableStatChanged)
            return false;

        PlayerPowerUpCharacterTuningRuntimeUtility.SyncProgressionRuntimeState(scalableStats,
                                                                               progressionConfig,
                                                                               runtimeGamePhases,
                                                                               ref playerExperience,
                                                                               ref playerLevel);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the unlock-catalog entry backing one runtime-scoped active slot when it owns temporary Character Tuning.
    /// </summary>
    /// <param name="slotConfig">Active-slot config inspected by PowerUpId.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog scanned for the matching entry.</param>
    /// <param name="catalogIndex">Matching runtime-scoped Character Tuning index when found.</param>
    /// <returns>True when the slot maps to a runtime-scoped Character Tuning entry.</returns>
    private static bool TryResolveScopedCatalogIndex(in PlayerPowerUpSlotConfig slotConfig,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     out int catalogIndex)
    {
        catalogIndex = -1;

        if (slotConfig.IsDefined == 0)
            return false;

        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0 || slotConfig.PowerUpId.Length <= 0)
            return false;

        for (int candidateIndex = 0; candidateIndex < unlockCatalog.Length; candidateIndex++)
        {
            ref PlayerPowerUpUnlockCatalogElement candidate = ref unlockCatalog.ElementAt(candidateIndex);

            if (candidate.PowerUpId != slotConfig.PowerUpId)
                continue;

            if (!PlayerPowerUpCharacterTuningRuntimeUtility.IsRuntimeScopedCharacterTuning(in candidate))
                return false;

            catalogIndex = candidateIndex;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Captures baseline values for every target stat touched by one runtime-scoped Character Tuning entry.
    /// </summary>
    /// <param name="unlockCatalog">Runtime unlock catalog containing the scoped Character Tuning entry.</param>
    /// <param name="catalogIndex">Catalog index for the scoped Character Tuning entry.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Current scalable-stat buffer used as snapshot source.</param>
    /// <param name="baseStats">Snapshot buffer that receives any still-missing target stat values.</param>
    private static void CaptureMissingBaseStats(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                int catalogIndex,
                                                DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats)
    {
        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
            return;

        ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);
        CaptureMissingBaseStats(in catalogEntry,
                                characterTuningFormulas,
                                scalableStats,
                                baseStats);
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
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

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
    /// <param name="primaryCatalogIndex">Primary runtime-scoped Character Tuning catalog index when active.</param>
    /// <param name="primaryIsActive">True when the primary runtime-scoped Character Tuning overlay remains active.</param>
    /// <param name="secondaryCatalogIndex">Secondary runtime-scoped Character Tuning catalog index when active.</param>
    /// <param name="secondaryIsActive">True when the secondary runtime-scoped Character Tuning overlay remains active.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog used to resolve active indices and passive ownership.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    private static void PruneUnusedBaseStats(DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> baseStats,
                                             int primaryCatalogIndex,
                                             bool primaryIsActive,
                                             int secondaryCatalogIndex,
                                             bool secondaryIsActive,
                                             DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                             DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas)
    {
        for (int baseStatIndex = 0; baseStatIndex < baseStats.Length; baseStatIndex++)
        {
            string statName = baseStats[baseStatIndex].Name.ToString();
            bool statStillNeeded = false;

            if (primaryIsActive)
                statStillNeeded = IsStatTargetedByCatalogIndex(statName,
                                                               unlockCatalog,
                                                               primaryCatalogIndex,
                                                               characterTuningFormulas);

            if (!statStillNeeded && secondaryIsActive)
                statStillNeeded = IsStatTargetedByCatalogIndex(statName,
                                                               unlockCatalog,
                                                               secondaryCatalogIndex,
                                                               characterTuningFormulas);

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
    /// <param name="unlockCatalog">Runtime unlock catalog containing the scoped Character Tuning entry.</param>
    /// <param name="catalogIndex">Catalog index backing the active runtime-scoped Character Tuning.</param>
    /// <returns>Stable non-zero signature while active, or zero when the scoped Character Tuning is inactive.</returns>
    private static uint BuildScopedOwnershipSignature(bool canBeApplied,
                                                      DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                      int catalogIndex)
    {
        if (!canBeApplied)
            return 0u;

        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
            return 0u;

        ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);
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
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

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
    /// <param name="projectileSizeMultipliers">Mutable per-source projectile-size multiplier buffer.</param>
    /// <param name="totalProjectileSizeMultiplier">Mutable product of every applied size-tuning source.</param>
    /// <returns>True when at least one passive Character Tuning formula changed runtime scalable stats.</returns>
    private static bool ApplyOwnedPassiveCharacterTuning(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                         DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                         DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                         DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> projectileSizeMultipliers,
                                                         ref float totalProjectileSizeMultiplier)
    {
        if (!unlockCatalog.IsCreated || unlockCatalog.Length <= 0)
            return false;

        bool anyChanged = false;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (!IsPassiveScopedCharacterTuningOwned(in catalogEntry))
                continue;

            int applicationCount = Math.Max(0, catalogEntry.CurrentUnlockCount);
            float sizeBefore = ResolveProjectileSizeMultiplier(scalableStats);

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

            TrackProjectileSizeSource(in catalogEntry,
                                      sizeBefore,
                                      ResolveProjectileSizeMultiplier(scalableStats),
                                      projectileSizeMultipliers,
                                      ref totalProjectileSizeMultiplier);
        }

        return anyChanged;
    }

    /// <summary>
    /// Applies one runtime-scoped active Character Tuning entry as many times as its current unlock count indicates.
    /// </summary>
    /// <param name="unlockCatalog">Runtime unlock catalog containing the scoped active Character Tuning entry.</param>
    /// <param name="catalogIndex">Catalog index for the scoped active Character Tuning entry.</param>
    /// <param name="canBeApplied">True when the runtime-scoped Character Tuning is currently active.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <param name="scalableStats">Mutable scalable-stat buffer receiving the scoped runtime overlay.</param>
    /// <param name="projectileSizeMultipliers">Mutable per-source projectile-size multiplier buffer.</param>
    /// <param name="totalProjectileSizeMultiplier">Mutable product of every applied size-tuning source.</param>
    /// <returns>True when at least one formula changed runtime scalable stats.</returns>
    private static bool ApplyScopedCharacterTuning(DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                   int catalogIndex,
                                                   bool canBeApplied,
                                                   DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                   DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                   DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> projectileSizeMultipliers,
                                                   ref float totalProjectileSizeMultiplier)
    {
        if (!canBeApplied)
            return false;

        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
            return false;

        ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);
        bool anyChanged = false;
        int applicationCount = ResolveScopedApplicationCount(in catalogEntry);
        float sizeBefore = ResolveProjectileSizeMultiplier(scalableStats);

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

        TrackProjectileSizeSource(in catalogEntry,
                                  sizeBefore,
                                  ResolveProjectileSizeMultiplier(scalableStats),
                                  projectileSizeMultipliers,
                                  ref totalProjectileSizeMultiplier);

        return anyChanged;
    }

    /// <summary>
    /// Records one exact size ratio after a power-up's formulas have been evaluated against the current scalable-stat context.
    /// </summary>
    /// <param name="catalogEntry">Power-up source whose formula range was applied.</param>
    /// <param name="sizeBefore">Projectile-size stat before this source.</param>
    /// <param name="sizeAfter">Projectile-size stat after this source.</param>
    /// <param name="projectileSizeMultipliers">Mutable source buffer receiving non-neutral ratios.</param>
    /// <param name="totalProjectileSizeMultiplier">Mutable combined ratio updated in place.</param>
    private static void TrackProjectileSizeSource(in PlayerPowerUpUnlockCatalogElement catalogEntry,
                                                  float sizeBefore,
                                                  float sizeAfter,
                                                  DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> projectileSizeMultipliers,
                                                  ref float totalProjectileSizeMultiplier)
    {
        float sourceMultiplier = math.max(MinimumProjectileSizeMultiplier, sizeAfter) /
                                 math.max(MinimumProjectileSizeMultiplier, sizeBefore);

        if (math.abs(sourceMultiplier - 1f) <= 0.000001f)
            return;

        totalProjectileSizeMultiplier *= sourceMultiplier;

        if (!projectileSizeMultipliers.IsCreated || catalogEntry.PowerUpId.Length <= 0)
            return;

        projectileSizeMultipliers.Add(new PlayerProjectileSizePowerUpMultiplierElement
        {
            PowerUpId = catalogEntry.PowerUpId,
            Multiplier = sourceMultiplier
        });
    }

    /// <summary>
    /// Resolves the current numeric projectile-size scalable stat used before and after each source formula range.
    /// </summary>
    /// <param name="scalableStats">Current runtime scalable-stat buffer.</param>
    /// <returns>Positive projectile-size multiplier, or one when the stat is unavailable or non-numeric.</returns>
    private static float ResolveProjectileSizeMultiplier(DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        int scalableStatIndex = PlayerPowerUpCharacterTuningRuntimeUtility.FindScalableStatIndex(scalableStats,
                                                                                                 ProjectileSizeStatName);

        if (scalableStatIndex < 0)
            return 1f;

        PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
        PlayerFormulaValue value = PlayerScalableStatValueUtility.ResolveRuntimeValue(in scalableStat);
        return value.Type == PlayerFormulaValueType.Number
            ? math.max(MinimumProjectileSizeMultiplier, value.NumberValue)
            : 1f;
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

        switch (catalogEntry.PassiveToolConfig.ConditionalApplication.Mode)
        {
            case PowerUpConditionalApplicationMode.DelayedShootApplication:
            case PowerUpConditionalApplicationMode.SuddenStrike:
                return false;
        }

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
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (!IsPassiveScopedCharacterTuningOwned(in catalogEntry))
                continue;

            if (!IsStatTargetedByEntry(statName, in catalogEntry, characterTuningFormulas))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether one stat is targeted by the Character Tuning entry stored at a catalog index.
    /// </summary>
    /// <param name="statName">Requested scalable-stat name.</param>
    /// <param name="unlockCatalog">Runtime unlock catalog containing the candidate entry.</param>
    /// <param name="catalogIndex">Catalog index to inspect.</param>
    /// <param name="characterTuningFormulas">Flattened Character Tuning formula buffer.</param>
    /// <returns>True when the indexed entry targets the stat.</returns>
    private static bool IsStatTargetedByCatalogIndex(string statName,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     int catalogIndex,
                                                     DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas)
    {
        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
            return false;

        ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);
        return IsStatTargetedByEntry(statName, in catalogEntry, characterTuningFormulas);
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
