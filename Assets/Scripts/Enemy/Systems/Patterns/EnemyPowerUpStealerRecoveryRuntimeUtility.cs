using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Restores stolen Power-Up Stealer payloads on death, despawn, or configured damage-recovery thresholds.
/// </summary>
internal static class EnemyPowerUpStealerRecoveryRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores or drops every power-up currently held by one despawning enemy.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity being returned to the pool.</param>
    /// <param name="dropPosition">World position used for active container drops.</param>
    /// <param name="physicsWorldSingleton">Physics world used to ground dropped active containers.</param>
    /// <param name="hasPhysicsWorld">True when physicsWorldSingleton is valid.</param>
    /// <param name="forceActiveContainerDrop">True when stolen active power-ups must be dropped as containers instead of restored directly.</param>
    /// <param name="stealerRuntime">Mutable Stealer runtime buffer on the enemy.</param>
    /// <param name="visualStateLookup">Enemy visual state lookup used for icon cleanup.</param>
    /// <param name="playerAccess">Mutable player loadout and passive accessors.</param>
    /// <param name="commandBuffer">ECB used to spawn dropped active containers.</param>
    /// <returns>True when at least one stolen power-up was recovered or dropped.</returns>
    public static bool TryRecoverStolenPowerUps(Entity enemyEntity,
                                                float3 dropPosition,
                                                in PhysicsWorldSingleton physicsWorldSingleton,
                                                bool hasPhysicsWorld,
                                                bool forceActiveContainerDrop,
                                                DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                ref ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup,
                                                ref EnemyPowerUpStealerPlayerAccess playerAccess,
                                                ref EntityCommandBuffer commandBuffer)
    {
        bool recoveredAny = false;

        for (int stealerIndex = 0; stealerIndex < stealerRuntime.Length; stealerIndex++)
        {
            EnemyPowerUpStealerRuntimeElement runtime = stealerRuntime[stealerIndex];

            if (runtime.HasStolenPowerUp == 0)
                continue;

            if (!TryRecoverRuntime(in runtime,
                                   dropPosition,
                                   in physicsWorldSingleton,
                                   hasPhysicsWorld,
                                   forceActiveContainerDrop,
                                   ref playerAccess,
                                   ref commandBuffer))
            {
                continue;
            }

            stealerRuntime[stealerIndex] = EnemyPowerUpStealerRuntimeDefaultsUtility.CreateCleared(in runtime);
            recoveredAny = true;
        }

        if (recoveredAny && !EnemyPowerUpStealerRuntimeUtility.HasAnyStolenPowerUp(stealerRuntime))
            EnemyPowerUpStealerRuntimeUtility.ClearVisualState(enemyEntity, ref visualStateLookup);

        return recoveredAny;
    }

    /// <summary>
    /// Updates damage-recovery metrics and returns stolen payloads whose configured thresholds are reached.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity that owns the Stealer runtime.</param>
    /// <param name="dropPosition">World position used if active recovery has to fall back to a dropped container.</param>
    /// <param name="enemyHealth">Current enemy health used to compute post-steal damage percentages.</param>
    /// <param name="deltaTime">Scaled delta time used by timed damage windows.</param>
    /// <param name="physicsWorldSingleton">Physics world used to ground dropped active containers.</param>
    /// <param name="hasPhysicsWorld">True when physicsWorldSingleton is valid.</param>
    /// <param name="stealerRuntime">Mutable Stealer runtime buffer on the enemy.</param>
    /// <param name="visualStateLookup">Enemy visual state lookup used for icon cleanup.</param>
    /// <param name="playerAccess">Mutable player loadout and passive accessors.</param>
    /// <param name="commandBuffer">ECB used to spawn dropped active containers.</param>
    /// <returns>True when at least one stolen power-up was recovered.</returns>
    public static bool TryRecoverStolenPowerUpsAfterDamage(Entity enemyEntity,
                                                           float3 dropPosition,
                                                           in EnemyHealth enemyHealth,
                                                           float deltaTime,
                                                           in PhysicsWorldSingleton physicsWorldSingleton,
                                                           bool hasPhysicsWorld,
                                                           DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime,
                                                           ref ComponentLookup<EnemyPowerUpStealerVisualState> visualStateLookup,
                                                           ref EnemyPowerUpStealerPlayerAccess playerAccess,
                                                           ref EntityCommandBuffer commandBuffer)
    {
        bool recoveredAny = false;

        for (int stealerIndex = 0; stealerIndex < stealerRuntime.Length; stealerIndex++)
        {
            EnemyPowerUpStealerRuntimeElement runtime = stealerRuntime[stealerIndex];

            if (runtime.HasStolenPowerUp == 0)
                continue;

            bool shouldRecover = UpdateDamageRecoveryMetrics(ref runtime, in enemyHealth, deltaTime);

            if (!shouldRecover)
            {
                stealerRuntime[stealerIndex] = runtime;
                continue;
            }

            if (!TryRecoverRuntime(in runtime,
                                   dropPosition,
                                   in physicsWorldSingleton,
                                   hasPhysicsWorld,
                                   false,
                                   ref playerAccess,
                                   ref commandBuffer))
            {
                stealerRuntime[stealerIndex] = runtime;
                continue;
            }

            stealerRuntime[stealerIndex] = EnemyPowerUpStealerRuntimeDefaultsUtility.CreateCleared(in runtime);
            recoveredAny = true;
        }

        if (recoveredAny && !EnemyPowerUpStealerRuntimeUtility.HasAnyStolenPowerUp(stealerRuntime))
            EnemyPowerUpStealerRuntimeUtility.ClearVisualState(enemyEntity, ref visualStateLookup);

        return recoveredAny;
    }
    #endregion

    #region Recovery Metrics
    /// <summary>
    /// Advances total and timed damage thresholds for one stolen payload.
    /// </summary>
    /// <param name="runtime">Runtime entry holding the stolen payload and mutable recovery metrics.</param>
    /// <param name="enemyHealth">Current enemy health used to compute post-steal damage.</param>
    /// <param name="deltaTime">Scaled delta time used by timed damage windows.</param>
    /// <returns>True when any configured recovery threshold is reached.</returns>
    private static bool UpdateDamageRecoveryMetrics(ref EnemyPowerUpStealerRuntimeElement runtime,
                                                    in EnemyHealth enemyHealth,
                                                    float deltaTime)
    {
        float maxHealth = math.max(0f, enemyHealth.Max);
        float currentHealth = math.max(0f, enemyHealth.Current);

        if (maxHealth <= 0f)
        {
            runtime.LastObservedHealth = currentHealth;
            return false;
        }

        if (runtime.HealthAtSteal <= 0f && runtime.LastObservedHealth <= 0f)
        {
            runtime.HealthAtSteal = currentHealth;
            runtime.LastObservedHealth = currentHealth;
            return false;
        }

        float previousObservedHealth = runtime.LastObservedHealth > 0f
            ? runtime.LastObservedHealth
            : runtime.HealthAtSteal;
        float damagePercentThisFrame = math.max(0f, previousObservedHealth - currentHealth) * 100f / maxHealth;
        runtime.LastObservedHealth = currentHealth;
        bool totalDamageRecoveryReached = IsTotalDamageRecoveryReached(in runtime, currentHealth, maxHealth);
        bool timedDamageRecoveryReached = UpdateTimedDamageRecovery(ref runtime, damagePercentThisFrame, deltaTime);
        return totalDamageRecoveryReached || timedDamageRecoveryReached;
    }

    /// <summary>
    /// Checks the cumulative post-steal damage threshold.
    /// </summary>
    /// <param name="runtime">Runtime entry containing the total-damage recovery threshold.</param>
    /// <param name="currentHealth">Current enemy health after latest damage application.</param>
    /// <param name="maxHealth">Enemy max health used as percentage baseline.</param>
    /// <returns>True when cumulative post-steal health loss reaches the configured threshold.</returns>
    private static bool IsTotalDamageRecoveryReached(in EnemyPowerUpStealerRuntimeElement runtime,
                                                     float currentHealth,
                                                     float maxHealth)
    {
        if (runtime.UseDamageRecovery == 0 || runtime.DamageRecoveryPercent <= 0f)
            return false;

        float damageSinceStealPercent = math.max(0f, runtime.HealthAtSteal - currentHealth) * 100f / maxHealth;
        return damageSinceStealPercent >= runtime.DamageRecoveryPercent;
    }

    /// <summary>
    /// Advances the timed damage window and checks whether its threshold was reached.
    /// </summary>
    /// <param name="runtime">Runtime entry containing timed recovery metrics.</param>
    /// <param name="damagePercentThisFrame">Health damage percentage observed since the previous update.</param>
    /// <param name="deltaTime">Scaled delta time used by the timed window.</param>
    /// <returns>True when the timed damage window reaches the configured threshold.</returns>
    private static bool UpdateTimedDamageRecovery(ref EnemyPowerUpStealerRuntimeElement runtime,
                                                  float damagePercentThisFrame,
                                                  float deltaTime)
    {
        if (runtime.UseTimedDamageRecovery == 0 ||
            runtime.TimedDamageRecoveryPercent <= 0f ||
            runtime.TimedDamageRecoverySeconds <= 0f)
        {
            runtime.RecoveryWindowElapsedSeconds = 0f;
            runtime.RecoveryWindowAccumulatedPercent = 0f;
            return false;
        }

        runtime.RecoveryWindowElapsedSeconds += math.max(0f, deltaTime);

        if (runtime.RecoveryWindowElapsedSeconds > runtime.TimedDamageRecoverySeconds)
        {
            runtime.RecoveryWindowElapsedSeconds = 0f;
            runtime.RecoveryWindowAccumulatedPercent = 0f;
        }

        runtime.RecoveryWindowAccumulatedPercent += math.max(0f, damagePercentThisFrame);
        return runtime.RecoveryWindowAccumulatedPercent >= runtime.TimedDamageRecoveryPercent;
    }
    #endregion

    #region Restore Helpers
    /// <summary>
    /// Recovers one stolen payload according to its active or passive kind.
    /// </summary>
    /// <param name="runtime">Runtime entry holding the stolen payload.</param>
    /// <param name="dropPosition">World position used for active container drops.</param>
    /// <param name="physicsWorldSingleton">Physics world used to ground dropped containers.</param>
    /// <param name="hasPhysicsWorld">True when physicsWorldSingleton is valid.</param>
    /// <param name="forceContainerDrop">True when active payloads must always become a dropped container.</param>
    /// <param name="playerAccess">Mutable player loadout and passive accessors.</param>
    /// <param name="commandBuffer">ECB used to spawn dropped active containers.</param>
    /// <returns>True when the payload was restored or dropped.</returns>
    private static bool TryRecoverRuntime(in EnemyPowerUpStealerRuntimeElement runtime,
                                          float3 dropPosition,
                                          in PhysicsWorldSingleton physicsWorldSingleton,
                                          bool hasPhysicsWorld,
                                          bool forceContainerDrop,
                                          ref EnemyPowerUpStealerPlayerAccess playerAccess,
                                          ref EntityCommandBuffer commandBuffer)
    {
        switch (runtime.StolenKind)
        {
            case PlayerPowerUpUnlockKind.Passive:
                return TryRestorePassivePowerUp(in runtime, ref playerAccess);

            default:
                return TryRestoreActivePowerUp(in runtime,
                                               dropPosition,
                                               in physicsWorldSingleton,
                                               hasPhysicsWorld,
                                               forceContainerDrop,
                                               ref playerAccess,
                                               ref commandBuffer);
        }
    }

    /// <summary>
    /// Recovers a stolen active power-up through direct restoration or a dropped container.
    /// </summary>
    /// <param name="runtime">Stealer runtime holding the active payload.</param>
    /// <param name="dropPosition">World position used for active container drops.</param>
    /// <param name="physicsWorldSingleton">Physics world used to ground dropped containers.</param>
    /// <param name="hasPhysicsWorld">True when physicsWorldSingleton is valid.</param>
    /// <param name="forceContainerDrop">True when the active must always become a world container.</param>
    /// <param name="playerAccess">Mutable player loadout accessors.</param>
    /// <param name="commandBuffer">ECB used to spawn dropped active containers.</param>
    /// <returns>True when the active payload was recovered or dropped.</returns>
    private static bool TryRestoreActivePowerUp(in EnemyPowerUpStealerRuntimeElement runtime,
                                                float3 dropPosition,
                                                in PhysicsWorldSingleton physicsWorldSingleton,
                                                bool hasPhysicsWorld,
                                                bool forceContainerDrop,
                                                ref EnemyPowerUpStealerPlayerAccess playerAccess,
                                                ref EntityCommandBuffer commandBuffer)
    {
        Entity playerEntity = runtime.PlayerEntity;

        if (!EnemyPowerUpStealerRuntimeUtility.CanAccessPlayer(playerEntity, ref playerAccess))
            return false;

        if (forceContainerDrop)
            return TryDropActivePowerUpContainer(playerEntity,
                                                 dropPosition,
                                                 in runtime.StoredActivePowerUp,
                                                 in physicsWorldSingleton,
                                                 hasPhysicsWorld,
                                                 ref playerAccess,
                                                 ref commandBuffer);

        PlayerPowerUpsConfig powerUpsConfig = playerAccess.PowerUpsConfigLookup[playerEntity];
        PlayerPowerUpsState powerUpsState = playerAccess.PowerUpsStateLookup[playerEntity];

        if (PlayerPowerUpLoadoutRuntimeUtility.TryRestoreStoredPowerUpToVacantSlot(in runtime.StoredActivePowerUp,
                                                                                   runtime.OriginalActiveSlotIndex,
                                                                                   runtime.OriginalActiveEquipOrder,
                                                                                   ref powerUpsConfig,
                                                                                   ref powerUpsState))
        {
            playerAccess.PowerUpsConfigLookup[playerEntity] = powerUpsConfig;
            playerAccess.PowerUpsStateLookup[playerEntity] = powerUpsState;
            return true;
        }

        if (TryDropActivePowerUpContainer(playerEntity,
                                          dropPosition,
                                          in runtime.StoredActivePowerUp,
                                          in physicsWorldSingleton,
                                          hasPhysicsWorld,
                                          ref playerAccess,
                                          ref commandBuffer))
            return true;

        if (PlayerPowerUpLoadoutRuntimeUtility.TryRestoreStoredPowerUpToVacantSlot(in runtime.StoredActivePowerUp,
                                                                                   0,
                                                                                   runtime.OriginalActiveEquipOrder,
                                                                                   ref powerUpsConfig,
                                                                                   ref powerUpsState) ||
            PlayerPowerUpLoadoutRuntimeUtility.TryRestoreStoredPowerUpToVacantSlot(in runtime.StoredActivePowerUp,
                                                                                   1,
                                                                                   runtime.OriginalActiveEquipOrder,
                                                                                   ref powerUpsConfig,
                                                                                   ref powerUpsState))
        {
            playerAccess.PowerUpsConfigLookup[playerEntity] = powerUpsConfig;
            playerAccess.PowerUpsStateLookup[playerEntity] = powerUpsState;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Restores a stolen passive power-up to the player's passive buffer and catalog ownership state.
    /// </summary>
    /// <param name="runtime">Stealer runtime holding the passive payload.</param>
    /// <param name="playerAccess">Mutable player passive accessors.</param>
    /// <returns>True when the passive payload was restored.</returns>
    private static bool TryRestorePassivePowerUp(in EnemyPowerUpStealerRuntimeElement runtime,
                                                 ref EnemyPowerUpStealerPlayerAccess playerAccess)
    {
        Entity playerEntity = runtime.PlayerEntity;

        if (!EnemyPowerUpStealerRuntimeUtility.CanAccessPlayer(playerEntity, ref playerAccess))
            return false;

        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = playerAccess.EquippedPassiveToolsLookup[playerEntity];

        if (runtime.OriginalPassiveBufferIndex >= 0 &&
            !EnemyPowerUpStealerRuntimeUtility.ContainsPassivePowerUp(runtime.PowerUpId, equippedPassiveTools) &&
            runtime.PowerUpId.Length > 0)
        {
            InsertPassiveAtRestoredIndex(equippedPassiveTools,
                                         runtime.OriginalPassiveBufferIndex,
                                         new EquippedPassiveToolElement
                                         {
                                             PowerUpId = runtime.PowerUpId,
                                             Tool = runtime.StoredPassiveTool
                                         });
        }

        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = playerAccess.UnlockCatalogLookup[playerEntity];
        int catalogIndex = runtime.OriginalPassiveCatalogIndex;

        if (catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
            catalogIndex = EnemyPowerUpStealerRuntimeUtility.FindCatalogIndex(runtime.PowerUpId,
                                                                              PlayerPowerUpUnlockKind.Passive,
                                                                              unlockCatalog);

        if (catalogIndex >= 0)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];
            catalogEntry.CurrentUnlockCount = math.max(catalogEntry.CurrentUnlockCount,
                                                       math.max(1, runtime.OriginalPassiveUnlockCount));
            catalogEntry.IsUnlocked = 1;
            catalogEntry.PendingInitialCharacterTuningApply = 0;
            unlockCatalog[catalogIndex] = catalogEntry;
        }

        playerAccess.PassiveToolsStateLookup[playerEntity] = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        return true;
    }

    /// <summary>
    /// Restores a passive payload at its original buffer index when possible to preserve acquisition order.
    /// </summary>
    /// <param name="equippedPassiveTools">Mutable passive buffer receiving the restored entry.</param>
    /// <param name="restoredIndex">Original passive buffer index captured when the Stealer removed the entry.</param>
    /// <param name="restoredPassive">Passive entry to restore.</param>
    private static void InsertPassiveAtRestoredIndex(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     int restoredIndex,
                                                     EquippedPassiveToolElement restoredPassive)
    {
        int insertionIndex = restoredIndex >= 0
            ? math.min(restoredIndex, equippedPassiveTools.Length)
            : equippedPassiveTools.Length;
        equippedPassiveTools.Add(restoredPassive);

        // Shift the newly appended entry into its original acquisition slot.
        for (int passiveIndex = equippedPassiveTools.Length - 1; passiveIndex > insertionIndex; passiveIndex--)
        {
            equippedPassiveTools[passiveIndex] = equippedPassiveTools[passiveIndex - 1];
        }

        equippedPassiveTools[insertionIndex] = restoredPassive;
    }

    /// <summary>
    /// Drops a stolen active power-up as a world container when direct restoration is blocked.
    /// </summary>
    /// <param name="playerEntity">Player entity receiving the recovery drop.</param>
    /// <param name="dropPosition">World position used for the dropped container.</param>
    /// <param name="storedPowerUp">Stored active payload to put inside the container.</param>
    /// <param name="physicsWorldSingleton">Physics world used to ground the container.</param>
    /// <param name="hasPhysicsWorld">True when physicsWorldSingleton is valid.</param>
    /// <param name="playerAccess">Mutable player container accessors.</param>
    /// <param name="commandBuffer">ECB used to spawn the container.</param>
    /// <returns>True when a container was spawned.</returns>
    private static bool TryDropActivePowerUpContainer(Entity playerEntity,
                                                      float3 dropPosition,
                                                      in PlayerStoredActivePowerUpData storedPowerUp,
                                                      in PhysicsWorldSingleton physicsWorldSingleton,
                                                      bool hasPhysicsWorld,
                                                      ref EnemyPowerUpStealerPlayerAccess playerAccess,
                                                      ref EntityCommandBuffer commandBuffer)
    {
        if (!hasPhysicsWorld)
            return false;

        if (!playerAccess.ContainerConfigLookup.HasComponent(playerEntity))
            return false;

        PlayerPowerUpContainerInteractionConfig interactionConfig = playerAccess.ContainerConfigLookup[playerEntity];
        return PlayerPowerUpContainerSpawnUtility.TrySpawnDroppedContainerAtPosition(in physicsWorldSingleton,
                                                                                     dropPosition,
                                                                                     in interactionConfig,
                                                                                     in storedPowerUp,
                                                                                     ref commandBuffer);
    }
    #endregion

    #endregion
}
