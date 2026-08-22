using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies enemy Power-Up Stealer runtime mutations that remove active or passive player power-ups.
/// </summary>
internal static class EnemyPowerUpStealerRuntimeUtility
{
    #region Methods
    #region Steal
    /// <summary>
    /// Attempts to run one configured Power-Up Stealer trigger for an enemy.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity owning the Stealer module.</param>
    /// <param name="playerEntity">Player entity targeted by the steal.</param>
    /// <param name="enemyPosition">Current enemy world position used by range gates.</param>
    /// <param name="playerPosition">Current player world position used by range gates.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used by activation gates.</param>
    /// <param name="patternRuntimeState">Pattern runtime state used by activation gates.</param>
    /// <param name="enemyHealth">Enemy health used to seed damage-based recovery thresholds after a successful steal.</param>
    /// <param name="triggerMode">Trigger being evaluated this frame.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used by acquisition anti-steal cooldowns.</param>
    /// <param name="stealerConfigs">Compiled Stealer configs on the enemy.</param>
    /// <param name="stealerRuntime">Mutable Stealer runtime buffer on the enemy.</param>
    /// <param name="visualStateLookup">Enemy visual state lookup used for stolen icon presentation.</param>
    /// <param name="playerAccess">Mutable player loadout and passive accessors.</param>
    /// <returns>True when a power-up was stolen.</returns>
    public static bool TryStealForTrigger(Entity enemyEntity,
                                          Entity playerEntity,
                                          float3 enemyPosition,
                                          float3 playerPosition,
                                          in EnemyRuntimeState enemyRuntimeState,
                                          in EnemyPatternRuntimeState patternRuntimeState,
                                          in EnemyHealth enemyHealth,
                                          EnemyPowerUpStealTriggerMode triggerMode,
                                          float elapsedTime,
                                          DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                          DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                          ref ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup,
                                          ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        if (!CanAccessPlayer(playerEntity, ref playerAccess))
        {
            ConsumeModuleActivationAttempts(triggerMode, stealerConfigs, stealerRuntime);
            return false;
        }

        if (HasAnyStolenPowerUp(stealerRuntime))
        {
            ConsumeModuleActivationAttempts(triggerMode, stealerConfigs, stealerRuntime);
            return false;
        }

        int stealerCount = math.min(stealerConfigs.Length, stealerRuntime.Length);
        for (int stealerIndex = 0; stealerIndex < stealerCount; stealerIndex++)
        {
            EnemyPowerUpStealerConfigElement config = stealerConfigs[stealerIndex];
            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(stealerIndex);

            if (!CanEvaluateTrigger(in config, in runtime, triggerMode))
                continue;

            bool consumeModuleActivationAttempt = ShouldConsumeModuleActivationAttempt(in config, triggerMode);

            if (!AreActivationGatesValid(in config,
                                         in enemyRuntimeState,
                                         in patternRuntimeState,
                                         enemyPosition,
                                         playerPosition))
            {
                if (consumeModuleActivationAttempt)
                    runtime.HasTriggeredOnce = 1;

                continue;
            }

            bool stolen = TryStealPowerUp(playerEntity,
                                          enemyEntity,
                                          in enemyRuntimeState,
                                          stealerIndex,
                                          in config,
                                          elapsedTime,
                                          ref runtime,
                                          ref playerAccess);

            if ((stolen && config.TriggerMode != EnemyPowerUpStealTriggerMode.OnEveryPlayerHit) ||
                (consumeModuleActivationAttempt && !stolen))
            {
                runtime.HasTriggeredOnce = 1;
            }

            if (stolen)
            {
                InitializeRecoveryTracking(in config, in enemyHealth, ref runtime);
                ApplyVisualState(enemyEntity, in runtime, in config, ref visualStateLookup);
            }

            if (stolen)
            {
                ConsumeModuleActivationAttempts(triggerMode, stealerConfigs, stealerRuntime);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Marks every spawn-only module-activation Stealer entry as consumed when the activation attempt cannot continue.
    /// </summary>
    /// <param name="triggerMode">Trigger currently being evaluated by the caller.</param>
    /// <param name="stealerConfigs">Compiled Stealer configs on the enemy.</param>
    /// <param name="stealerRuntime">Mutable Stealer runtime entries updated in place.</param>
    private static void ConsumeModuleActivationAttempts(EnemyPowerUpStealTriggerMode triggerMode,
                                                        DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs,
                                                        DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime)
    {
        int stealerCount = math.min(stealerConfigs.Length, stealerRuntime.Length);

        for (int stealerIndex = 0; stealerIndex < stealerCount; stealerIndex++)
        {
            EnemyPowerUpStealerConfigElement config = stealerConfigs[stealerIndex];

            if (!ShouldConsumeModuleActivationAttempt(in config, triggerMode))
                continue;

            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(stealerIndex);

            if (runtime.HasTriggeredOnce != 0)
                continue;

            runtime.HasTriggeredOnce = 1;
        }
    }

    /// <summary>
    /// Resolves whether one module-activation Stealer config consumes its trigger even when no target is stolen.
    /// </summary>
    /// <param name="config">Stealer config being evaluated.</param>
    /// <param name="triggerMode">Trigger requested by the caller.</param>
    /// <returns>True when the current activation attempt should be treated as one-shot.</returns>
    private static bool ShouldConsumeModuleActivationAttempt(in EnemyPowerUpStealerConfigElement config,
                                                            EnemyPowerUpStealTriggerMode triggerMode)
    {
        if (triggerMode != EnemyPowerUpStealTriggerMode.OnModuleActivation)
            return false;

        if (config.TriggerMode != EnemyPowerUpStealTriggerMode.OnModuleActivation)
            return false;

        return config.ConsumeModuleActivationAttemptOnSpawnOnly != 0;
    }

    /// <summary>
    /// Checks whether the enemy already holds any stolen power-up across all Stealer runtime entries.
    /// </summary>
    /// <param name="stealerRuntime">Runtime Stealer buffer to scan.</param>
    /// <returns>True when at least one runtime entry is currently holding a stolen power-up.</returns>
    public static bool HasAnyStolenPowerUp(DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime)
    {
        for (int runtimeIndex = 0; runtimeIndex < stealerRuntime.Length; runtimeIndex++)
        {
            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(runtimeIndex);

            if (runtime.HasStolenPowerUp == 0)
                continue;

            return true;
        }

        return false;
    }

    #endregion

    #region Steal Helpers
    /// <summary>
    /// Checks whether the selected trigger can be evaluated for the current runtime entry.
    /// </summary>
    /// <param name="config">Stealer config being evaluated.</param>
    /// <param name="runtime">Current Stealer runtime entry.</param>
    /// <param name="triggerMode">Trigger requested by the caller.</param>
    /// <returns>True when this config can attempt a steal.</returns>
    private static bool CanEvaluateTrigger(in EnemyPowerUpStealerConfigElement config,
                                           in EnemyPowerUpStealerRuntimeElement runtime,
                                           EnemyPowerUpStealTriggerMode triggerMode)
    {
        if (config.TriggerMode != triggerMode)
            return false;

        if (runtime.HasStolenPowerUp != 0)
            return false;

        if (config.TriggerMode == EnemyPowerUpStealTriggerMode.OnEveryPlayerHit)
            return true;

        return runtime.HasTriggeredOnce == 0;
    }

    /// <summary>
    /// Seeds recovery metrics on the runtime entry immediately after a successful steal.
    /// </summary>
    /// <param name="config">Stealer config that produced the stolen payload.</param>
    /// <param name="enemyHealth">Current enemy health at the moment of the steal.</param>
    /// <param name="runtime">Runtime entry receiving recovery configuration and baselines.</param>
    private static void InitializeRecoveryTracking(in EnemyPowerUpStealerConfigElement config,
                                                   in EnemyHealth enemyHealth,
                                                   ref EnemyPowerUpStealerRuntimeElement runtime)
    {
        runtime.UseDamageRecovery = config.UseDamageRecovery;
        runtime.DamageRecoveryPercent = math.max(0f, config.DamageRecoveryPercent);
        runtime.UseTimedDamageRecovery = config.UseTimedDamageRecovery;
        runtime.TimedDamageRecoveryPercent = math.max(0f, config.TimedDamageRecoveryPercent);
        runtime.TimedDamageRecoverySeconds = math.max(0f, config.TimedDamageRecoverySeconds);
        runtime.HealthAtSteal = math.max(0f, enemyHealth.Current);
        runtime.LastObservedHealth = runtime.HealthAtSteal;
        runtime.RecoveryWindowElapsedSeconds = 0f;
        runtime.RecoveryWindowAccumulatedPercent = 0f;
    }

    /// <summary>
    /// Resolves whether active power-ups should be attempted before passive power-ups for one biased steal attempt.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic target-bias selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary the seed by activation timing.</param>
    /// <param name="stealerIndex">Index of the Stealer module being evaluated.</param>
    /// <param name="config">Stealer config containing the active-target bias percentage.</param>
    /// <returns>True when active should be attempted before passive.</returns>
    private static bool ShouldTryActiveFirst(Entity enemyEntity,
                                             in EnemyRuntimeState enemyRuntimeState,
                                             int stealerIndex,
                                             in EnemyPowerUpStealerConfigElement config)
    {
        float activeBiasPercent = math.clamp(config.ActiveTargetBiasPercent, 0f, 100f);

        if (activeBiasPercent >= 100f)
            return true;

        if (activeBiasPercent <= 0f)
            return false;

        uint seed = math.hash(new uint4((uint)enemyEntity.Index,
                                        (uint)enemyEntity.Version,
                                        math.asuint(enemyRuntimeState.LifetimeSeconds),
                                        (uint)math.max(0, stealerIndex)));
        float sampledPercent = (seed & 0x00FFFFFFu) * (100f / 16777215f);
        return sampledPercent <= activeBiasPercent;
    }

    /// <summary>
    /// Applies the target policy and removes one eligible active or passive power-up from the player.
    /// </summary>
    /// <param name="playerEntity">Player entity being stolen from.</param>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic target-bias selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary target-bias selection per activation.</param>
    /// <param name="stealerIndex">Index of the Stealer module being evaluated.</param>
    /// <param name="config">Stealer config being evaluated.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used by acquisition anti-steal cooldowns.</param>
    /// <param name="runtime">Mutable Stealer runtime entry receiving the stolen payload.</param>
    /// <param name="playerAccess">Mutable player loadout and passive accessors.</param>
    /// <returns>True when one power-up was stolen.</returns>
    private static bool TryStealPowerUp(Entity playerEntity,
                                        Entity enemyEntity,
                                        in EnemyRuntimeState enemyRuntimeState,
                                        int stealerIndex,
                                        in EnemyPowerUpStealerConfigElement config,
                                        float elapsedTime,
                                        ref EnemyPowerUpStealerRuntimeElement runtime,
                                        ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        switch (config.TargetKind)
        {
            case EnemyPowerUpStealTargetKind.Active:
                return TryStealActivePowerUp(playerEntity,
                                             enemyEntity,
                                             in enemyRuntimeState,
                                             stealerIndex,
                                             in config,
                                             elapsedTime,
                                             ref runtime,
                                             ref playerAccess);

            case EnemyPowerUpStealTargetKind.Passive:
                return TryStealPassivePowerUp(playerEntity,
                                              enemyEntity,
                                              in enemyRuntimeState,
                                              stealerIndex,
                                              in config,
                                              elapsedTime,
                                              ref runtime,
                                              ref playerAccess);

            default:
                return TryStealByBias(playerEntity,
                                      enemyEntity,
                                      in enemyRuntimeState,
                                      stealerIndex,
                                      in config,
                                      elapsedTime,
                                      ref runtime,
                                      ref playerAccess);
        }
    }

    /// <summary>
    /// Attempts active and passive steal paths using the configured active-target bias percentage.
    /// </summary>
    /// <param name="playerEntity">Player entity being stolen from.</param>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic target-bias selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary target-bias selection per activation.</param>
    /// <param name="stealerIndex">Index of the Stealer module being evaluated.</param>
    /// <param name="config">Stealer config containing the active-target bias percentage.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used by acquisition anti-steal cooldowns.</param>
    /// <param name="runtime">Mutable Stealer runtime entry receiving the stolen payload.</param>
    /// <param name="playerAccess">Mutable player loadout and passive accessors.</param>
    /// <returns>True when either biased path steals one power-up.</returns>
    private static bool TryStealByBias(Entity playerEntity,
                                       Entity enemyEntity,
                                       in EnemyRuntimeState enemyRuntimeState,
                                       int stealerIndex,
                                       in EnemyPowerUpStealerConfigElement config,
                                       float elapsedTime,
                                       ref EnemyPowerUpStealerRuntimeElement runtime,
                                       ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        if (ShouldTryActiveFirst(enemyEntity, in enemyRuntimeState, stealerIndex, in config))
        {
            if (TryStealActivePowerUp(playerEntity,
                                      enemyEntity,
                                      in enemyRuntimeState,
                                      stealerIndex,
                                      in config,
                                      elapsedTime,
                                      ref runtime,
                                      ref playerAccess))
                return true;

            return TryStealPassivePowerUp(playerEntity,
                                          enemyEntity,
                                          in enemyRuntimeState,
                                          stealerIndex,
                                          in config,
                                          elapsedTime,
                                          ref runtime,
                                          ref playerAccess);
        }

        if (TryStealPassivePowerUp(playerEntity,
                                   enemyEntity,
                                   in enemyRuntimeState,
                                   stealerIndex,
                                   in config,
                                   elapsedTime,
                                   ref runtime,
                                   ref playerAccess))
            return true;

        return TryStealActivePowerUp(playerEntity,
                                     enemyEntity,
                                     in enemyRuntimeState,
                                     stealerIndex,
                                     in config,
                                     elapsedTime,
                                     ref runtime,
                                     ref playerAccess);
    }

    /// <summary>
    /// Removes one active slot from the player and stores it on the Stealer runtime.
    /// </summary>
    /// <param name="playerEntity">Player entity being stolen from.</param>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic active selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic selection by activation time.</param>
    /// <param name="stealerIndex">Index of the Stealer module being evaluated.</param>
    /// <param name="config">Stealer config containing selection and anti-steal cooldown settings.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used by acquisition anti-steal cooldowns.</param>
    /// <param name="runtime">Mutable Stealer runtime entry receiving the active payload.</param>
    /// <param name="playerAccess">Mutable player loadout accessors.</param>
    /// <returns>True when an active slot was stolen.</returns>
    private static bool TryStealActivePowerUp(Entity playerEntity,
                                              Entity enemyEntity,
                                              in EnemyRuntimeState enemyRuntimeState,
                                              int stealerIndex,
                                              in EnemyPowerUpStealerConfigElement config,
                                              float elapsedTime,
                                              ref EnemyPowerUpStealerRuntimeElement runtime,
                                              ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        PlayerPowerUpSlotConfig primarySlotConfig;
        PlayerPowerUpSlotConfig secondarySlotConfig;
        PlayerPowerUpsConfigBufferUtility.ReadSlots(playerEntity,
                                                    in playerAccess.PowerUpsConfigLookup,
                                                    out primarySlotConfig,
                                                    out secondarySlotConfig);
        PlayerPowerUpsState powerUpsState = playerAccess.PowerUpsStateLookup[playerEntity];
        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = playerAccess.UnlockCatalogLookup[playerEntity];
        int slotIndex = EnemyPowerUpStealerSelectionUtility.ResolveActiveSlotToSteal(in primarySlotConfig,
                                                                                     in secondarySlotConfig,
                                                                                     in powerUpsState,
                                                                                     unlockCatalog,
                                                                                     config.AcquisitionStealCooldownSeconds,
                                                                                     elapsedTime,
                                                                                     enemyEntity,
                                                                                     in enemyRuntimeState,
                                                                                     stealerIndex,
                                                                                     config.SelectionMode);

        if (slotIndex < 0)
            return false;

        int originalEquipOrder = EnemyPowerUpStealerSelectionUtility.ResolveActiveSlotEquipOrder(slotIndex, in powerUpsState);

        if (!PlayerPowerUpLoadoutRuntimeUtility.TryRemoveActiveSlot(slotIndex,
                                                                    ref primarySlotConfig,
                                                                    ref secondarySlotConfig,
                                                                    ref powerUpsState,
                                                                    ref runtime.StoredActivePowerUp))
            return false;

        PlayerReturningProjectileLoadoutRuntimeUtility.ApplyStolenOwnershipPolicy(slotIndex,
                                                                                   ref runtime.StoredActivePowerUp,
                                                                                   ref powerUpsState);

        powerUpsState.IsShootingSuppressed = 0;
        powerUpsState.PreviousPrimaryPressed = 0;
        powerUpsState.PreviousSecondaryPressed = 0;
        PlayerPowerUpsConfigBufferUtility.WriteSlots(playerAccess.PowerUpsConfigLookup[playerEntity],
                                                     in primarySlotConfig,
                                                     in secondarySlotConfig);
        playerAccess.PowerUpsStateLookup[playerEntity] = powerUpsState;
        runtime.HasStolenPowerUp = 1;
        runtime.StolenKind = PlayerPowerUpUnlockKind.Active;
        runtime.PowerUpId = runtime.StoredActivePowerUp.SlotConfig.PowerUpId;
        runtime.StoredPassiveTool = default;
        runtime.OriginalActiveSlotIndex = slotIndex;
        runtime.OriginalActiveEquipOrder = originalEquipOrder;
        runtime.OriginalPassiveCatalogIndex = -1;
        runtime.OriginalPassiveBufferIndex = -1;
        runtime.OriginalPassiveUnlockCount = 0;
        runtime.PlayerEntity = playerEntity;
        return true;
    }

    /// <summary>
    /// Removes one equipped passive power-up from the player and stores it on the Stealer runtime.
    /// </summary>
    /// <param name="playerEntity">Player entity being stolen from.</param>
    /// <param name="enemyEntity">Enemy entity used to seed deterministic passive selection.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used to vary deterministic selection by activation time.</param>
    /// <param name="stealerIndex">Index of the Stealer module being evaluated.</param>
    /// <param name="config">Stealer config containing selection and anti-steal cooldown settings.</param>
    /// <param name="elapsedTime">Current gameplay elapsed time used by acquisition anti-steal cooldowns.</param>
    /// <param name="runtime">Mutable Stealer runtime entry receiving the passive payload.</param>
    /// <param name="playerAccess">Mutable player passive accessors.</param>
    /// <returns>True when a passive power-up was stolen.</returns>
    private static bool TryStealPassivePowerUp(Entity playerEntity,
                                               Entity enemyEntity,
                                               in EnemyRuntimeState enemyRuntimeState,
                                               int stealerIndex,
                                               in EnemyPowerUpStealerConfigElement config,
                                               float elapsedTime,
                                               ref EnemyPowerUpStealerRuntimeElement runtime,
                                               ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = playerAccess.EquippedPassiveToolsLookup[playerEntity];
        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = playerAccess.UnlockCatalogLookup[playerEntity];
        DynamicBuffer<PlayerOrbitalProjectionLostElement> lostProjections = playerAccess.OrbitalProjectionLostLookup.HasBuffer(playerEntity)
            ? playerAccess.OrbitalProjectionLostLookup[playerEntity]
            : default;

        int passiveIndex = equippedPassiveTools.Length > 0
            ? EnemyPowerUpStealerSelectionUtility.ResolvePassiveIndexToSteal(equippedPassiveTools,
                                                                             unlockCatalog,
                                                                             lostProjections,
                                                                             config.AcquisitionStealCooldownSeconds,
                                                                             elapsedTime,
                                                                             enemyEntity,
                                                                             in enemyRuntimeState,
                                                                             stealerIndex,
                                                                             config.SelectionMode)
            : -1;

        if (passiveIndex < 0)
            return EnemyPowerUpStealerPassiveCatalogRuntimeUtility.TryStealCatalogOnlyPassivePowerUp(playerEntity,
                                                                                                     enemyEntity,
                                                                                                     in enemyRuntimeState,
                                                                                                     stealerIndex,
                                                                                                     in config,
                                                                                                     elapsedTime,
                                                                                                     equippedPassiveTools,
                                                                                                     ref runtime,
                                                                                                     ref playerAccess);

        ref EquippedPassiveToolElement stolenPassive = ref equippedPassiveTools.ElementAt(passiveIndex);
        FixedString64Bytes stolenPowerUpId = stolenPassive.PowerUpId;
        int catalogIndex = FindCatalogIndex(stolenPowerUpId, PlayerPowerUpUnlockKind.Passive, unlockCatalog);
        int originalUnlockCount = 0;
        runtime.StoredPassiveTool = stolenPassive.Tool;
        PlayerOrbitalProjectionLossRuntimeUtility.ShiftAfterPassiveRemoval(lostProjections,
                                                                           stolenPowerUpId,
                                                                           passiveIndex);
        equippedPassiveTools.RemoveAt(passiveIndex);
        DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer = playerAccess.PassiveToolsStateLookup[playerEntity];
        ref PlayerPassiveToolsState passiveToolsState = ref PlayerPassiveToolsStateBufferUtility.GetStateRef(passiveToolsStateBuffer);
        PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                      ref passiveToolsState);

        if (catalogIndex >= 0)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);
            originalUnlockCount = math.max(0, catalogEntry.CurrentUnlockCount);
            catalogEntry.CurrentUnlockCount = 0;
            catalogEntry.IsUnlocked = 0;
            catalogEntry.PendingInitialCharacterTuningApply = 0;
        }

        runtime.HasStolenPowerUp = 1;
        runtime.StolenKind = PlayerPowerUpUnlockKind.Passive;
        runtime.PowerUpId = stolenPowerUpId;
        runtime.StoredActivePowerUp = default;
        runtime.OriginalActiveSlotIndex = -1;
        runtime.OriginalActiveEquipOrder = 0;
        runtime.OriginalPassiveCatalogIndex = catalogIndex;
        runtime.OriginalPassiveBufferIndex = passiveIndex;
        runtime.OriginalPassiveUnlockCount = originalUnlockCount;
        runtime.PlayerEntity = playerEntity;
        return true;
    }
    #endregion

    #region Gates
    /// <summary>
    /// Evaluates range and optional Weapon Interaction activation gates for one Stealer config.
    /// </summary>
    /// <param name="config">Stealer config containing gate flags.</param>
    /// <param name="enemyRuntimeState">Enemy runtime state used for speed and damage checks.</param>
    /// <param name="patternRuntimeState">Pattern runtime state used for Wanderer wait checks.</param>
    /// <param name="enemyPosition">Current enemy world position.</param>
    /// <param name="playerPosition">Current player world position.</param>
    /// <returns>True when every configured gate is satisfied.</returns>
    private static bool AreActivationGatesValid(in EnemyPowerUpStealerConfigElement config,
                                                in EnemyRuntimeState enemyRuntimeState,
                                                in EnemyPatternRuntimeState patternRuntimeState,
                                                float3 enemyPosition,
                                                float3 playerPosition)
    {
        float3 delta = playerPosition - enemyPosition;
        delta.y = 0f;
        float distance = math.length(delta);

        if (config.UseMinimumRange != 0 && distance < math.max(0f, config.MinimumRange))
            return false;

        if (config.UseMaximumRange != 0 && distance > math.max(config.MinimumRange, config.MaximumRange))
            return false;

        EnemyWeaponInteractionActivationGate gates = config.ActivationGates;

        if (gates == EnemyWeaponInteractionActivationGate.Always)
            return true;

        if ((gates & EnemyWeaponInteractionActivationGate.RequireBelowSpeed) != 0)
        {
            float3 planarVelocity = enemyRuntimeState.Velocity;
            planarVelocity.y = 0f;

            if (math.length(planarVelocity) > math.max(0f, config.MaximumActivationSpeed))
                return false;
        }

        if ((gates & EnemyWeaponInteractionActivationGate.RequireRecentlyDamaged) != 0)
        {
            float damageAge = enemyRuntimeState.LifetimeSeconds - enemyRuntimeState.LastDamageLifetimeSeconds;

            if (enemyRuntimeState.HasTakenDamage == 0 ||
                damageAge > math.max(0f, config.RecentlyDamagedWindowSeconds))
            {
                return false;
            }
        }

        if ((gates & EnemyWeaponInteractionActivationGate.RequireWandererWait) != 0 &&
            patternRuntimeState.WanderWaitTimer <= 0f)
        {
            return false;
        }

        return true;
    }
    #endregion

    #region Lookups
    /// <summary>
    /// Checks whether all player data required by stealing and recovery is available.
    /// </summary>
    /// <param name="playerEntity">Player entity to inspect.</param>
    /// <param name="playerAccess">Player accessors that must contain the requested entity.</param>
    /// <returns>True when the required player components and buffers are available.</returns>
    internal static bool CanAccessPlayer(Entity playerEntity, ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        if (playerEntity == Entity.Null)
            return false;

        if (!playerAccess.PowerUpsConfigLookup.HasBuffer(playerEntity))
            return false;

        if (!playerAccess.PowerUpsStateLookup.HasComponent(playerEntity))
            return false;

        if (!playerAccess.EquippedPassiveToolsLookup.HasBuffer(playerEntity))
            return false;

        if (!playerAccess.PassiveToolsStateLookup.HasBuffer(playerEntity))
            return false;

        return playerAccess.UnlockCatalogLookup.HasBuffer(playerEntity);
    }

    /// <summary>
    /// Finds one unlock catalog entry by id and kind.
    /// </summary>
    /// <param name="powerUpId">Power-up id to find.</param>
    /// <param name="unlockKind">Expected unlock kind.</param>
    /// <param name="unlockCatalog">Catalog buffer to scan.</param>
    /// <returns>Catalog index, or -1 when no entry matches.</returns>
    internal static int FindCatalogIndex(FixedString64Bytes powerUpId,
                                         PlayerPowerUpUnlockKind unlockKind,
                                         DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog)
    {
        if (powerUpId.Length <= 0)
            return -1;

        for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
        {
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(catalogIndex);

            if (catalogEntry.UnlockKind != unlockKind)
                continue;

            if (catalogEntry.PowerUpId != powerUpId)
                continue;

            return catalogIndex;
        }

        return -1;
    }

    #endregion

    #region Visuals
    /// <summary>
    /// Writes the enemy icon state for a stolen power-up.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity that owns the visual state.</param>
    /// <param name="runtime">Stealer runtime entry holding the stolen payload.</param>
    /// <param name="config">Stealer config carrying the icon scale multiplier.</param>
    /// <param name="visualStateLookup">Mutable visual state lookup.</param>
    private static void ApplyVisualState(Entity enemyEntity,
                                         in EnemyPowerUpStealerRuntimeElement runtime,
                                         in EnemyPowerUpStealerConfigElement config,
                                         ref ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup)
    {
        if (!visualStateLookup.HasComponent(enemyEntity))
            return;

        visualStateLookup[enemyEntity] = new EnemyPowerUpStealerVisualState
        {
            HasStolenPowerUp = 1,
            PowerUpId = runtime.PowerUpId,
            StolenKind = runtime.StolenKind,
            IconScaleMultiplier = math.max(0.01f, config.StolenPowerUpIconScaleMultiplier)
        };
    }

    /// <summary>
    /// Clears the enemy icon state after every held power-up has been recovered.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity that owns the visual state.</param>
    /// <param name="visualStateLookup">Mutable visual state lookup.</param>
    internal static void ClearVisualState(Entity enemyEntity,
                                          ref ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup)
    {
        if (!visualStateLookup.HasComponent(enemyEntity))
            return;

        visualStateLookup[enemyEntity] = new EnemyPowerUpStealerVisualState
        {
            HasStolenPowerUp = 0,
            PowerUpId = default,
            StolenKind = PlayerPowerUpUnlockKind.Active,
            IconScaleMultiplier = 1f
        };
    }
    #endregion

    #endregion
}
