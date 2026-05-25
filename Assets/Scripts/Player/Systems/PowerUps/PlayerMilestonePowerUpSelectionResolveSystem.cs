using System.Globalization;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Consumes milestone power-up selection commands and applies either the chosen unlock or the configured skip compensations.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLevelUpSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerMilestonePowerUpSelectionResolveSystem : ISystem
{
    #region Nested Types
    private struct ResolvedMilestoneSelectionCommand
    {
        public PlayerMilestoneSelectionCommandType CommandType;
        public int OfferIndex;
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers all runtime data required to resolve milestone selection commands.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionOfferElement>();
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionState>();
        state.RequireForUpdate<PlayerMilestoneTimeScaleResumeState>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerPowerUpCharacterTuningFormulaElement>();
        state.RequireForUpdate<PlayerProgressionConfig>();
        state.RequireForUpdate<PlayerRuntimeGamePhaseElement>();
        state.RequireForUpdate<PlayerLevel>();
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerExperienceCollection>();
        state.RequireForUpdate<PlayerScalableStatElement>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Processes queued HUD selection commands and resolves the active milestone outcome immediately.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        BufferLookup<PlayerPassiveToolsStateElement> passiveToolsStateLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(false);
        ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneTimeScaleResumeStateLookup = SystemAPI.GetComponentLookup<PlayerMilestoneTimeScaleResumeState>(false);
        ComponentLookup<PlayerProgressionConfig> progressionConfigLookup = SystemAPI.GetComponentLookup<PlayerProgressionConfig>(true);
        ComponentLookup<PlayerLevel> playerLevelLookup = SystemAPI.GetComponentLookup<PlayerLevel>(false);
        ComponentLookup<PlayerExperience> playerExperienceLookup = SystemAPI.GetComponentLookup<PlayerExperience>(false);
        ComponentLookup<PlayerExperienceCollection> playerExperienceCollectionLookup = SystemAPI.GetComponentLookup<PlayerExperienceCollection>(false);
        ComponentLookup<PlayerHealth> playerHealthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> playerShieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        BufferLookup<PlayerPowerUpsConfigElement> powerUpsConfigLookup = SystemAPI.GetBufferLookup<PlayerPowerUpsConfigElement>(false);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerPowerUpContainerInteractionConfig> powerUpContainerConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequestsLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionSpawnRequest>(false);
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                  DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats,
                  DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                  RefRW<PlayerMilestonePowerUpSelectionState> selectionState,
                  Entity entity)
                 in SystemAPI.Query<DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement>,
                                    DynamicBuffer<PlayerPowerUpUnlockCatalogElement>,
                                    DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement>,
                                    DynamicBuffer<PlayerScalableStatElement>,
                                    DynamicBuffer<PlayerRuntimeGamePhaseElement>,
                                    RefRW<PlayerMilestonePowerUpSelectionState>>().WithEntityAccess())
        {
            if (!passiveToolsStateLookup.HasBuffer(entity) ||
                !milestoneTimeScaleResumeStateLookup.HasComponent(entity) ||
                !progressionConfigLookup.HasComponent(entity) ||
                !playerLevelLookup.HasComponent(entity) ||
                !playerExperienceLookup.HasComponent(entity) ||
                !playerExperienceCollectionLookup.HasComponent(entity) ||
                !playerHealthLookup.HasComponent(entity) ||
                !playerShieldLookup.HasComponent(entity) ||
                !powerUpsConfigLookup.HasBuffer(entity) ||
                !powerUpsStateLookup.HasComponent(entity) ||
                !equippedPassiveToolsLookup.HasBuffer(entity))
                continue;

            DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffersBuffer = selectionOffers;
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalogBuffer = unlockCatalog;
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer = characterTuningFormulas;
            DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer = scalableStats;
            DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhaseBuffer = runtimeGamePhases;
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];

            PlayerMilestonePowerUpSelectionState selectionStateValue = selectionState.ValueRO;

            if (selectionStateValue.HasPendingCommand == 0)
                continue;

            if (!TryResolveRequestedCommand(in selectionStateValue, out ResolvedMilestoneSelectionCommand resolvedCommand))
            {
                ClearPendingCommand(ref selectionStateValue);
                selectionState.ValueRW = selectionStateValue;
                continue;
            }

            ClearPendingCommand(ref selectionStateValue);
            selectionState.ValueRW = selectionStateValue;

            if (selectionStateValue.IsSelectionActive == 0)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Ignored stale selection command on entity {0}: no active milestone selection.",
                                               entity.Index));
                continue;
            }

            PlayerMilestoneTimeScaleResumeState milestoneTimeScaleResumeStateValue = milestoneTimeScaleResumeStateLookup[entity];
            PlayerProgressionConfig progressionConfigValue = progressionConfigLookup[entity];
            PlayerLevel playerLevelValue = playerLevelLookup[entity];
            PlayerExperienceCollection playerExperienceCollectionValue = playerExperienceCollectionLookup[entity];
            PlayerPowerUpsConfig powerUpsConfigValue;
            PlayerPowerUpsConfigBufferUtility.Read(entity,
                                                   in powerUpsConfigLookup,
                                                   out powerUpsConfigValue);
            PlayerPowerUpsState powerUpsStateValue = powerUpsStateLookup[entity];
            PlayerExperience playerExperienceValue = playerExperienceLookup[entity];
            PlayerHealth playerHealthValue = playerHealthLookup[entity];
            PlayerShield playerShieldValue = playerShieldLookup[entity];

            if (resolvedCommand.CommandType == PlayerMilestoneSelectionCommandType.Skip)
            {
                int skippedMilestoneLevel = selectionStateValue.MilestoneLevel;
                PlayerHealth previousHealthValue = playerHealthValue;
                PlayerShield previousShieldValue = playerShieldValue;
                int appliedCompensationCount = PlayerMilestoneSelectionOutcomeUtility.ApplySkipCompensations(progressionConfigValue,
                                                                                                              runtimeGamePhaseBuffer,
                                                                                                              in selectionStateValue,
                                                                                                              ref playerHealthValue,
                                                                                                              ref playerShieldValue,
                                                                                                              ref playerExperienceValue,
                                                                                                              in playerLevelValue,
                                                                                                              in powerUpsConfigValue,
                                                                                                              ref powerUpsStateValue);

                playerHealthLookup[entity] = playerHealthValue;
                playerShieldLookup[entity] = playerShieldValue;
                playerExperienceLookup[entity] = playerExperienceValue;
                powerUpsStateLookup[entity] = powerUpsStateValue;

                if (canEnqueueAudioRequests)
                    EnqueueSkipCompensationAudio(entity,
                                                 previousHealthValue,
                                                 playerHealthValue,
                                                 previousShieldValue,
                                                 playerShieldValue,
                                                 in localTransformLookup,
                                                 audioRequests);

                FinalizeSelection(progressionConfigValue,
                                  selectionOffersBuffer,
                                  ref selectionStateValue,
                                  ref milestoneTimeScaleResumeStateValue);
                selectionState.ValueRW = selectionStateValue;
                milestoneTimeScaleResumeStateLookup[entity] = milestoneTimeScaleResumeStateValue;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                        "[PlayerMilestonePowerUpSelectionResolveSystem] Skip executed for milestone {0}. Applied compensation entries: {1}.",
                                        skippedMilestoneLevel,
                                        appliedCompensationCount));
                continue;
            }

            int selectedOfferIndex = resolvedCommand.OfferIndex;

            if (selectedOfferIndex < 0 || selectedOfferIndex >= selectionOffersBuffer.Length)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Invalid offer index {0}. Active offers: {1}.",
                                               selectedOfferIndex,
                                               selectionOffersBuffer.Length));
                continue;
            }

            PlayerMilestonePowerUpSelectionOfferElement selectedOffer = selectionOffersBuffer[selectedOfferIndex];
            int selectedCatalogIndex = selectedOffer.CatalogIndex;

            if (selectedCatalogIndex < 0 || selectedCatalogIndex >= unlockCatalogBuffer.Length)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Invalid catalog index {0} for selected offer.",
                                               selectedCatalogIndex));
                continue;
            }

            DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer = passiveToolsStateLookup[entity];
            ref PlayerPassiveToolsState passiveToolsState = ref PlayerPassiveToolsStateBufferUtility.GetStateRef(passiveToolsStateBuffer);
            ref PlayerPowerUpUnlockCatalogElement selectedCatalogEntry = ref unlockCatalogBuffer.ElementAt(selectedCatalogIndex);
            int maximumUnlockCount = math.max(1, selectedCatalogEntry.MaximumUnlockCount);

            if (selectedCatalogEntry.CurrentUnlockCount >= maximumUnlockCount)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Ignored selected offer '{0}' because the unlock already reached its acquisition cap ({1}/{2}).",
                                               selectedCatalogEntry.PowerUpId.ToString(),
                                               selectedCatalogEntry.CurrentUnlockCount,
                                               maximumUnlockCount));
                continue;
            }

            bool isFirstAcquisition = selectedCatalogEntry.CurrentUnlockCount <= 0;
            bool wasAlreadyUnlocked = selectedCatalogEntry.IsUnlocked != 0;
            bool runtimeApplied = false;
            string runtimeApplyTarget = "NoRuntimeEffect";

            if (isFirstAcquisition)
            {
                runtimeApplied = ApplySelectedUnlock(selectedOffer.UnlockKind,
                                                    in selectedCatalogEntry,
                                                    in physicsWorldSingleton,
                                                    ref powerUpsConfigValue,
                                                    ref powerUpsStateValue,
                                                    equippedPassiveToolsBuffer,
                                                    ref passiveToolsState,
                                                    entity,
                                                    in localTransformLookup,
                                                    in powerUpContainerConfigLookup,
                                                    ref commandBuffer,
                                                    out runtimeApplyTarget);
            }
            else
            {
                runtimeApplied = ApplyStackedUnlock(selectedOffer.UnlockKind,
                                                    in selectedCatalogEntry,
                                                    equippedPassiveToolsBuffer,
                                                    ref passiveToolsState,
                                                    entity,
                                                    orbitalProjectionRequestsLookup,
                                                    out runtimeApplyTarget);
            }

            bool characterTuningApplied = false;
            int appliedCharacterTuningCount = 0;

            if (PlayerPowerUpCharacterTuningRuntimeUtility.ShouldApplyOnAcquisition(in selectedCatalogEntry))
            {
                characterTuningApplied = PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuning(in selectedCatalogEntry,
                                                                                                             characterTuningFormulaBuffer,
                                                                                                             scalableStatsBuffer,
                                                                                                             progressionConfigValue,
                                                                                                             runtimeGamePhaseBuffer,
                                                                                                             ref playerExperienceValue,
                                                                                                             ref playerLevelValue,
                                                                                                             ref playerExperienceCollectionValue,
                                                                                                             out appliedCharacterTuningCount);
            }

            selectedCatalogEntry.CurrentUnlockCount = math.min(maximumUnlockCount, selectedCatalogEntry.CurrentUnlockCount + 1);
            selectedCatalogEntry.IsUnlocked = 1;
            selectedCatalogEntry.PendingInitialCharacterTuningApply = 0;
            PlayerPowerUpStealCooldownRuntimeUtility.MarkCatalogEntryAcquired(selectedCatalogIndex,
                                                                              unlockCatalogBuffer,
                                                                              elapsedTime);

            PlayerPowerUpsConfigBufferUtility.Write(powerUpsConfigLookup[entity], in powerUpsConfigValue);
            powerUpsStateLookup[entity] = powerUpsStateValue;
            playerExperienceLookup[entity] = playerExperienceValue;
            playerLevelLookup[entity] = playerLevelValue;
            playerExperienceCollectionLookup[entity] = playerExperienceCollectionValue;
            FinalizeSelection(progressionConfigValue,
                              selectionOffersBuffer,
                              ref selectionStateValue,
                              ref milestoneTimeScaleResumeStateValue);
            selectionState.ValueRW = selectionStateValue;
            milestoneTimeScaleResumeStateLookup[entity] = milestoneTimeScaleResumeStateValue;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerMilestonePowerUpSelectionResolveSystem] Selected offer {0}: Power-Up '{1}' ({2}). Already Unlocked: {3}. Runtime Applied: {4} ({5}). Character Tuning Applied: {6} ({7} formulas). Unlock Count: {8}/{9}.",
                                    selectedOfferIndex,
                                    selectedOffer.PowerUpId.ToString(),
                                    selectedOffer.UnlockKind,
                                    wasAlreadyUnlocked ? "Yes" : "No",
                                    runtimeApplied ? "Yes" : "No",
                                    runtimeApplyTarget,
                                    characterTuningApplied ? "Yes" : "No",
                                    appliedCharacterTuningCount,
                                    selectedCatalogEntry.CurrentUnlockCount,
                                    maximumUnlockCount));
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Commands
    /// <summary>
    /// Resolves the actionable milestone-selection command stored on the selection state.
    /// </summary>
    /// <param name="selectionState">Selection state carrying one pending HUD command.</param>
    /// <param name="resolvedCommand">Resolved command payload when found.</param>
    /// <returns>True when one actionable command exists; otherwise false.</returns>
    private static bool TryResolveRequestedCommand(in PlayerMilestonePowerUpSelectionState selectionState,
                                                   out ResolvedMilestoneSelectionCommand resolvedCommand)
    {
        resolvedCommand = default;

        if (selectionState.HasPendingCommand == 0)
            return false;

        switch (selectionState.PendingCommandType)
        {
            case PlayerMilestoneSelectionCommandType.Skip:
                resolvedCommand = new ResolvedMilestoneSelectionCommand
                {
                    CommandType = PlayerMilestoneSelectionCommandType.Skip,
                    OfferIndex = -1
                };
                return true;
            case PlayerMilestoneSelectionCommandType.SelectOffer:
                if (selectionState.PendingOfferIndex < 0)
                    return false;

                resolvedCommand = new ResolvedMilestoneSelectionCommand
                {
                    CommandType = PlayerMilestoneSelectionCommandType.SelectOffer,
                    OfferIndex = selectionState.PendingOfferIndex
                };
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Clears the HUD command slot after the resolver has observed it.
    /// </summary>
    /// <param name="selectionState">Selection state whose pending command fields are reset.</param>
    private static void ClearPendingCommand(ref PlayerMilestonePowerUpSelectionState selectionState)
    {
        selectionState.HasPendingCommand = 0;
        selectionState.PendingCommandType = PlayerMilestoneSelectionCommandType.SelectOffer;
        selectionState.PendingOfferIndex = -1;
    }

    /// <summary>
    /// Closes the active milestone selection and starts the smooth Time.timeScale resume.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config containing the resume duration.</param>
    /// <param name="selectionOffers">Offer buffer cleared when the selection closes.</param>
    /// <param name="selectionState">Selection state reset to inactive defaults.</param>
    /// <param name="resumeState">Time.timeScale resume state configured in place.</param>

    private static void FinalizeSelection(PlayerProgressionConfig progressionConfig,
                                          DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                                          ref PlayerMilestonePowerUpSelectionState selectionState,
                                          ref PlayerMilestoneTimeScaleResumeState resumeState)
    {
        PlayerMilestoneSelectionOutcomeUtility.ResetSelection(selectionOffers, ref selectionState);
        PlayerMilestoneSelectionOutcomeUtility.BeginTimeScaleResume(progressionConfig, ref resumeState);
    }

    /// <summary>
    /// Applies the selected unlock payload to runtime active/passive player data.
    /// </summary>
    /// <param name="unlockKind">Selected unlock payload type.</param>
    /// <param name="selectedCatalogEntry">Selected catalog entry data.</param>
    /// <param name="powerUpsConfig">Runtime active-slot config to mutate.</param>
    /// <param name="powerUpsState">Runtime active-slot state to mutate.</param>
    /// <param name="equippedPassiveTools">Runtime passive-tools buffer.</param>
    /// <param name="passiveToolsState">Aggregated runtime passive state.</param>
    /// <param name="applyTarget">Debug-friendly destination where the unlock was applied.</param>
    /// <returns>True when runtime state changed; otherwise false.</returns>
    private static bool ApplySelectedUnlock(PlayerPowerUpUnlockKind unlockKind,
                                            in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                            in PhysicsWorldSingleton physicsWorldSingleton,
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                            ref PlayerPassiveToolsState passiveToolsState,
                                            Entity playerEntity,
                                            in ComponentLookup<LocalTransform> localTransformLookup,
                                            in ComponentLookup<PlayerPowerUpContainerInteractionConfig> powerUpContainerConfigLookup,
                                            ref EntityCommandBuffer commandBuffer,
                                            out string applyTarget)
    {
        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                return TryEquipActiveUnlock(in selectedCatalogEntry.ActiveSlotConfig,
                                            in physicsWorldSingleton,
                                            ref powerUpsConfig,
                                            ref powerUpsState,
                                            playerEntity,
                                            in localTransformLookup,
                                            in powerUpContainerConfigLookup,
                                            ref commandBuffer,
                                            out applyTarget);
            case PlayerPowerUpUnlockKind.Passive:
                return TryEquipPassiveUnlock(in selectedCatalogEntry,
                                             equippedPassiveTools,
                                             ref passiveToolsState,
                                             out applyTarget);
            default:
                applyTarget = "UnknownUnlockKind";
                return false;
        }
    }

    /// <summary>
    /// Applies runtime effects that are meaningful on repeated Stackable acquisitions without re-equipping the base power-up.
    /// </summary>
    /// <param name="unlockKind">Selected unlock payload type.</param>
    /// <param name="selectedCatalogEntry">Selected catalog entry data.</param>
    /// <param name="equippedPassiveTools">Runtime passive-tools buffer used by passive orbital stacks.</param>
    /// <param name="passiveToolsState">Aggregated runtime passive state rebuilt when a passive orbital source is added.</param>
    /// <param name="playerEntity">Player entity receiving the stacked runtime effect.</param>
    /// <param name="orbitalProjectionRequestsLookup">Writable orbital projection request buffers.</param>
    /// <param name="applyTarget">Debug-friendly destination where the stacked effect was applied.</param>
    /// <returns>True when a repeated acquisition produced an orbital runtime effect.</returns>
    private static bool ApplyStackedUnlock(PlayerPowerUpUnlockKind unlockKind,
                                           in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                           DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                           ref PlayerPassiveToolsState passiveToolsState,
                                           Entity playerEntity,
                                           BufferLookup<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequestsLookup,
                                           out string applyTarget)
    {
        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                return TryEnqueueStackedActiveOrbitalProjections(in selectedCatalogEntry,
                                                                 playerEntity,
                                                                 orbitalProjectionRequestsLookup,
                                                                 out applyTarget);
            case PlayerPowerUpUnlockKind.Passive:
                return PlayerPowerUpPassiveUnlockRuntimeUtility.TryStackOrbitalProjectionTool(in selectedCatalogEntry,
                                                                                              equippedPassiveTools,
                                                                                              ref passiveToolsState,
                                                                                              out applyTarget);
            default:
                applyTarget = "UnknownStackedUnlockKind";
                return false;
        }
    }

    /// <summary>
    /// Enqueues active-tool orbital projections for repeated Stackable acquisitions that cannot equip a second active slot.
    /// </summary>
    /// <param name="selectedCatalogEntry">Active catalog entry being acquired again.</param>
    /// <param name="playerEntity">Player entity that owns the orbital request buffer.</param>
    /// <param name="orbitalProjectionRequestsLookup">Writable request buffers used by the orbital spawn system.</param>
    /// <param name="applyTarget">Debug label describing the active stacked result.</param>
    /// <returns>True when a timed orbital projection request was enqueued.</returns>
    private static bool TryEnqueueStackedActiveOrbitalProjections(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                                                  Entity playerEntity,
                                                                  BufferLookup<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequestsLookup,
                                                                  out string applyTarget)
    {
        applyTarget = "NoActiveOrbitalProjectionStack";

        if (!orbitalProjectionRequestsLookup.HasBuffer(playerEntity))
            return false;

        PlayerPassiveToolConfig triggeredPassiveTool = selectedCatalogEntry.ActiveSlotConfig.TriggeredProjectilePassiveTool;

        if (triggeredPassiveTool.IsDefined == 0 ||
            triggeredPassiveTool.HasOrbitalProjections == 0 ||
            triggeredPassiveTool.OrbitalProjections.Length <= 0)
        {
            return false;
        }

        DynamicBuffer<PlayerOrbitalProjectionSpawnRequest> orbitalProjectionRequests = orbitalProjectionRequestsLookup[playerEntity];
        orbitalProjectionRequests.Add(new PlayerOrbitalProjectionSpawnRequest
        {
            OwnerEntity = playerEntity,
            PowerUpId = selectedCatalogEntry.PowerUpId,
            Persistent = 0,
            SourceInstanceId = math.max(0, selectedCatalogEntry.CurrentUnlockCount),
            Projections = triggeredPassiveTool.OrbitalProjections
        });
        applyTarget = "ActiveOrbitalProjectionStacked";
        return true;
    }
    #endregion

    #region Active Unlock
    /// <summary>
    /// Equips one active unlock into the first available or replaceable runtime slot.
    /// </summary>
    /// <param name="activeSlotConfig">Unlocked active-slot config payload.</param>
    /// <param name="powerUpsConfig">Runtime active-slot config to mutate.</param>
    /// <param name="powerUpsState">Runtime active-slot state to reset.</param>
    /// <param name="playerEntity">Player entity receiving the new active unlock.</param>
    /// <param name="localTransformLookup">Read-only player transform lookup used to position dropped containers.</param>
    /// <param name="powerUpContainerConfigLookup">Read-only lookup of player-side dropped-container settings.</param>
    /// <param name="commandBuffer">ECB used to spawn dropped containers when one active is replaced.</param>
    /// <param name="applyTarget">Debug label of the slot where the unlock was applied.</param>
    /// <returns>True when the active slot was applied; otherwise false.</returns>
    private static bool TryEquipActiveUnlock(in PlayerPowerUpSlotConfig activeSlotConfig,
                                             in PhysicsWorldSingleton physicsWorldSingleton,
                                             ref PlayerPowerUpsConfig powerUpsConfig,
                                             ref PlayerPowerUpsState powerUpsState,
                                             Entity playerEntity,
                                             in ComponentLookup<LocalTransform> localTransformLookup,
                                             in ComponentLookup<PlayerPowerUpContainerInteractionConfig> powerUpContainerConfigLookup,
                                             ref EntityCommandBuffer commandBuffer,
                                             out string applyTarget)
    {
        applyTarget = "ActiveSlot";

        if (activeSlotConfig.IsDefined == 0)
        {
            applyTarget = "InvalidActiveConfig";
            return false;
        }

        PlayerPowerUpContainerStoredStateMode storedStateMode = PlayerPowerUpContainerStoredStateMode.PreserveEnergyAndCooldown;

        if (powerUpContainerConfigLookup.HasComponent(playerEntity))
            storedStateMode = powerUpContainerConfigLookup[playerEntity].StoredStateMode;

        if (!PlayerPowerUpLoadoutRuntimeUtility.TryEquipIntoOldestSlot(in activeSlotConfig,
                                                                       storedStateMode,
                                                                       ref powerUpsConfig,
                                                                       ref powerUpsState,
                                                                       out int targetSlotIndex,
                                                                       out PlayerStoredActivePowerUpData replacedPowerUp))
        {
            applyTarget = "NoReplaceableSlot";
            return false;
        }

        bool spawnedDroppedContainer = false;

        if (replacedPowerUp.SlotConfig.IsDefined != 0 &&
            powerUpContainerConfigLookup.HasComponent(playerEntity) &&
            localTransformLookup.HasComponent(playerEntity))
        {
            PlayerPowerUpContainerInteractionConfig interactionConfig = powerUpContainerConfigLookup[playerEntity];
            LocalTransform playerTransform = localTransformLookup[playerEntity];
            spawnedDroppedContainer = PlayerPowerUpContainerSpawnUtility.TrySpawnDroppedContainer(in physicsWorldSingleton,
                                                                                                  in playerTransform,
                                                                                                  in interactionConfig,
                                                                                                  in replacedPowerUp,
                                                                                                  ref commandBuffer);
        }

        switch (targetSlotIndex)
        {
            case 0:
                applyTarget = spawnedDroppedContainer ? "PrimaryOldestReplacedAndDropped" : "PrimaryOldestReplaced";
                return true;
            case 1:
                applyTarget = spawnedDroppedContainer ? "SecondaryOldestReplacedAndDropped" : "SecondaryOldestReplaced";
                return true;
            default:
                applyTarget = "UnknownTargetSlot";
                return false;
        }
    }
    #endregion

    #region Audio
    /// <summary>
    /// Enqueues recharge sounds produced by skip-compensation resources.
    /// </summary>
    /// <param name="entity">Player entity receiving compensation.</param>
    /// <param name="previousHealth">Health before compensation.</param>
    /// <param name="currentHealth">Health after compensation.</param>
    /// <param name="previousShield">Shield before compensation.</param>
    /// <param name="currentShield">Shield after compensation.</param>
    /// <param name="localTransformLookup">Lookup used to resolve positioned audio.</param>
    /// <param name="audioRequests">Audio request buffer on the game audio singleton.</param>
    private static void EnqueueSkipCompensationAudio(Entity entity,
                                                     PlayerHealth previousHealth,
                                                     PlayerHealth currentHealth,
                                                     PlayerShield previousShield,
                                                     PlayerShield currentShield,
                                                     in ComponentLookup<LocalTransform> localTransformLookup,
                                                     DynamicBuffer<GameAudioEventRequest> audioRequests)
    {
        if (currentHealth.Current > previousHealth.Current)
            EnqueuePlayerAudio(entity, GameAudioEventId.PlayerHealthRecharge, in localTransformLookup, audioRequests);

        if (currentShield.Current > previousShield.Current)
            EnqueuePlayerAudio(entity, GameAudioEventId.PlayerShieldRecharge, in localTransformLookup, audioRequests);
    }

    /// <summary>
    /// Enqueues a player-scoped audio event with position when a transform is available.
    /// </summary>
    /// <param name="entity">Player entity used for optional position lookup.</param>
    /// <param name="eventId">Audio event to request.</param>
    /// <param name="localTransformLookup">Lookup used to resolve player position.</param>
    /// <param name="audioRequests">Audio request buffer on the game audio singleton.</param>
    private static void EnqueuePlayerAudio(Entity entity,
                                           GameAudioEventId eventId,
                                           in ComponentLookup<LocalTransform> localTransformLookup,
                                           DynamicBuffer<GameAudioEventRequest> audioRequests)
    {
        if (localTransformLookup.HasComponent(entity))
        {
            GameAudioEventRequestUtility.EnqueuePositioned(audioRequests, eventId, localTransformLookup[entity].Position);
            return;
        }

        GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, eventId);
    }
    #endregion

    #region Passive Unlock
    /// <summary>
    /// Equips one passive unlock and rebuilds aggregated passive runtime state.
    /// </summary>
    /// <param name="selectedCatalogEntry">Selected passive catalog entry.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer.</param>
    /// <param name="passiveToolsState">Aggregated passive runtime state to update.</param>
    /// <param name="applyTarget">Debug label describing the passive-apply result.</param>
    /// <returns>True when a passive tool was added; otherwise false.</returns>
    private static bool TryEquipPassiveUnlock(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                              ref PlayerPassiveToolsState passiveToolsState,
                                              out string applyTarget)
    {
        return PlayerPowerUpPassiveUnlockRuntimeUtility.TryEquipPassiveTool(in selectedCatalogEntry,
                                                                            equippedPassiveTools,
                                                                            ref passiveToolsState,
                                                                            out applyTarget);
    }
    #endregion

    #endregion
}
