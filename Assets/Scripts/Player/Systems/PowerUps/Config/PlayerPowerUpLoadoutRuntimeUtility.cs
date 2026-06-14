using Unity.Mathematics;

/// <summary>
/// Centralizes runtime loadout mutations for active-slot state initialization, replacement, and swapping.
/// </summary>
internal static class PlayerPowerUpLoadoutRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the initial runtime state for one player power-up loadout.
    /// </summary>
    /// <param name="powerUpsConfig">Current runtime power-up config used to seed slot values.</param>
    /// <param name="currentKillCount">Current global kill count tracked by power-up charge systems.</param>
    /// <returns>Initialized runtime state ready to add as PlayerPowerUpsState.</returns>
    public static PlayerPowerUpsState CreateInitialState(in PlayerPowerUpsConfig powerUpsConfig, uint currentKillCount)
    {
        PlayerPowerUpsState powerUpsState = new PlayerPowerUpsState
        {
            LastObservedGlobalKillCount = currentKillCount,
            LastValidMovementDirection = float3.zero
        };
        ResetRuntimeState(ref powerUpsState, in powerUpsConfig);
        return powerUpsState;
    }

    /// <summary>
    /// Resets transient runtime slot state to match the currently equipped loadout.
    /// </summary>
    /// <param name="powerUpsState">Runtime power-up state to reset.</param>
    /// <param name="powerUpsConfig">Current runtime config used to initialize slot values.</param>
    public static void ResetRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpsConfig powerUpsConfig)
    {
        powerUpsState.PrimaryEnergy = math.max(0f, powerUpsConfig.PrimarySlot.MaximumEnergy);
        powerUpsState.SecondaryEnergy = math.max(0f, powerUpsConfig.SecondarySlot.MaximumEnergy);
        powerUpsState.PrimaryCooldownRemaining = 0f;
        powerUpsState.SecondaryCooldownRemaining = 0f;
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.PrimaryMaintenanceTickTimer = 0f;
        powerUpsState.SecondaryMaintenanceTickTimer = 0f;
        powerUpsState.PrimaryIsCharging = 0;
        powerUpsState.SecondaryIsCharging = 0;
        powerUpsState.PrimaryIsActive = 0;
        powerUpsState.SecondaryIsActive = 0;
        powerUpsState.IsShootingSuppressed = 0;
        powerUpsState.PreviousPrimaryPressed = 0;
        powerUpsState.PreviousSecondaryPressed = 0;
        powerUpsState.PreviousSwapSlotsPressed = 0;
        powerUpsState.HasPendingCheatCommand = 0;
        powerUpsState.PendingCheatCommandType = PlayerPowerUpCheatCommandType.None;
        powerUpsState.PendingCheatPresetIndex = -1;
        AssignInitialEquipOrders(ref powerUpsState, in powerUpsConfig);
    }

    /// <summary>
    /// Equips a newly acquired active power-up into the first vacant slot, or the oldest occupied slot when both are filled.
    /// </summary>
    /// <param name="activeSlotConfig">Unlocked active-slot payload to equip.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to update.</param>
    /// <param name="targetSlotIndex">Resolved slot index receiving the new active power-up.</param>
    /// <returns>True when the active power-up was equipped; otherwise false.</returns>
    public static bool TryEquipIntoOldestSlot(in PlayerPowerUpSlotConfig activeSlotConfig,
                                              ref PlayerPowerUpsConfig powerUpsConfig,
                                              ref PlayerPowerUpsState powerUpsState,
                                              out int targetSlotIndex)
    {
        PlayerStoredActivePowerUpData replacedPowerUp = default;
        return TryEquipIntoOldestSlot(in activeSlotConfig,
                                      PlayerPowerUpContainerStoredStateMode.PreserveEnergyAndCooldown,
                                      ref powerUpsConfig,
                                      ref powerUpsState,
                                      out targetSlotIndex,
                                      out replacedPowerUp);
    }

    /// <summary>
    /// Equips a newly acquired active power-up into the first vacant slot, or the oldest occupied slot when both are filled, and snapshots the replaced one for container storage.
    /// </summary>
    /// <param name="activeSlotConfig">Unlocked active-slot payload to equip.</param>
    /// <param name="storedStateMode">Storage policy applied to the replaced slot before it is dropped to the world.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to update.</param>
    /// <param name="targetSlotIndex">Resolved slot index receiving the new active power-up.</param>
    /// <param name="replacedPowerUp">Snapshot of the replaced slot ready to store in a dropped container.</param>
    /// <returns>True when the active power-up was equipped; otherwise false.</returns>
    public static bool TryEquipIntoOldestSlot(in PlayerPowerUpSlotConfig activeSlotConfig,
                                              PlayerPowerUpContainerStoredStateMode storedStateMode,
                                              ref PlayerPowerUpsConfig powerUpsConfig,
                                              ref PlayerPowerUpsState powerUpsState,
                                              out int targetSlotIndex,
                                              out PlayerStoredActivePowerUpData replacedPowerUp)
    {
        targetSlotIndex = -1;
        replacedPowerUp = default;

        if (activeSlotConfig.IsDefined == 0)
            return false;

        targetSlotIndex = ResolveOldestActiveSlotIndex(in powerUpsConfig, in powerUpsState);
        replacedPowerUp = CaptureStoredPowerUp(targetSlotIndex,
                                               storedStateMode,
                                               in powerUpsConfig,
                                               in powerUpsState);

        switch (targetSlotIndex)
        {
            case 0:
                powerUpsConfig.PrimarySlot = activeSlotConfig;
                ResetPrimaryRuntimeState(ref powerUpsState, in activeSlotConfig);
                powerUpsState.PrimaryEquipOrder = ConsumeNextEquipOrder(ref powerUpsState);
                return true;
            case 1:
                powerUpsConfig.SecondarySlot = activeSlotConfig;
                ResetSecondaryRuntimeState(ref powerUpsState, in activeSlotConfig);
                powerUpsState.SecondaryEquipOrder = ConsumeNextEquipOrder(ref powerUpsState);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Swaps the active power-up stored in a world container with one runtime slot.
    /// </summary>
    /// <param name="storedPowerUp">Container payload swapped in place with the selected slot.</param>
    /// <param name="targetSlotIndex">Slot index selected by the player. 0 is primary and 1 is secondary.</param>
    /// <param name="storedStateMode">Storage policy applied to the outgoing slot before it replaces the container payload.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <param name="storedPowerUpConsumed">True when the target slot was empty and the container should be destroyed after the equip.</param>
    /// <returns>True when the swap succeeded; otherwise false.</returns>
    public static bool TrySwapStoredPowerUpWithSlot(ref PlayerStoredActivePowerUpData storedPowerUp,
                                                    int targetSlotIndex,
                                                    PlayerPowerUpContainerStoredStateMode storedStateMode,
                                                    ref PlayerPowerUpsConfig powerUpsConfig,
                                                    ref PlayerPowerUpsState powerUpsState,
                                                    out bool storedPowerUpConsumed)
    {
        storedPowerUpConsumed = false;

        if (storedPowerUp.SlotConfig.IsDefined == 0)
            return false;

        targetSlotIndex = ResolveContainerPickupSlotIndex(targetSlotIndex, in powerUpsConfig);

        if (targetSlotIndex < 0)
            return false;

        PlayerStoredActivePowerUpData replacedPowerUp = CaptureStoredPowerUp(targetSlotIndex,
                                                                             storedStateMode,
                                                                             in powerUpsConfig,
                                                                             in powerUpsState);
        ApplyStoredPowerUpToSlot(in storedPowerUp,
                                 targetSlotIndex,
                                 0,
                                 ref powerUpsConfig,
                                 ref powerUpsState);
        storedPowerUp = replacedPowerUp;
        storedPowerUpConsumed = replacedPowerUp.SlotConfig.IsDefined == 0;
        return true;
    }

    /// <summary>
    /// Removes one active slot from the runtime loadout and captures its state for delayed restoration.
    /// </summary>
    /// <param name="targetSlotIndex">Slot index to remove. 0 is primary and 1 is secondary.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <param name="removedPowerUp">Captured active-slot payload ready for restoration or dropped-container storage.</param>
    /// <returns>True when a defined active slot was removed; otherwise false.</returns>
    public static bool TryRemoveActiveSlot(int targetSlotIndex,
                                           ref PlayerPowerUpsConfig powerUpsConfig,
                                           ref PlayerPowerUpsState powerUpsState,
                                           out PlayerStoredActivePowerUpData removedPowerUp)
    {
        removedPowerUp = default;
        return TryRemoveActiveSlot(targetSlotIndex,
                                   ref powerUpsConfig.PrimarySlot,
                                   ref powerUpsConfig.SecondarySlot,
                                   ref powerUpsState,
                                   ref removedPowerUp);
    }

    /// <summary>
    /// Removes one active slot from direct slot payloads and captures its state for delayed restoration.
    /// </summary>
    /// <param name="targetSlotIndex">Slot index to remove. 0 is primary and 1 is secondary.</param>
    /// <param name="primarySlotConfig">Mutable primary active slot payload.</param>
    /// <param name="secondarySlotConfig">Mutable secondary active slot payload.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <param name="removedPowerUp">Captured active-slot payload ready for restoration or dropped-container storage.</param>
    /// <returns>True when a defined active slot was removed; otherwise false.</returns>
    public static bool TryRemoveActiveSlot(int targetSlotIndex,
                                           ref PlayerPowerUpSlotConfig primarySlotConfig,
                                           ref PlayerPowerUpSlotConfig secondarySlotConfig,
                                           ref PlayerPowerUpsState powerUpsState,
                                           ref PlayerStoredActivePowerUpData removedPowerUp)
    {
        CaptureStoredPowerUp(targetSlotIndex,
                             PlayerPowerUpContainerStoredStateMode.PreserveEnergyAndCooldown,
                             in primarySlotConfig,
                             in secondarySlotConfig,
                             in powerUpsState,
                             ref removedPowerUp);

        if (removedPowerUp.SlotConfig.IsDefined == 0)
            return false;

        switch (targetSlotIndex)
        {
            case 0:
                primarySlotConfig = default;
                ResetPrimaryRuntimeState(ref powerUpsState, default);
                powerUpsState.PrimaryEquipOrder = 0;
                return true;

            case 1:
                secondarySlotConfig = default;
                ResetSecondaryRuntimeState(ref powerUpsState, default);
                powerUpsState.SecondaryEquipOrder = 0;
                return true;

            default:
                removedPowerUp = default;
                return false;
        }
    }

    /// <summary>
    /// Restores one stored active power-up only when the requested destination slot is still vacant.
    /// </summary>
    /// <param name="storedPowerUp">Stored active payload to restore.</param>
    /// <param name="targetSlotIndex">Slot index to restore into. 0 is primary and 1 is secondary.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <returns>True when the stored power-up was restored directly; otherwise false.</returns>
    public static bool TryRestoreStoredPowerUpToVacantSlot(in PlayerStoredActivePowerUpData storedPowerUp,
                                                           int targetSlotIndex,
                                                           ref PlayerPowerUpsConfig powerUpsConfig,
                                                           ref PlayerPowerUpsState powerUpsState)
    {
        return TryRestoreStoredPowerUpToVacantSlot(in storedPowerUp,
                                                   targetSlotIndex,
                                                   0,
                                                   ref powerUpsConfig,
                                                   ref powerUpsState);
    }

    /// <summary>
    /// Restores one stored active power-up while preserving a known equip order when the requested destination slot is still vacant.
    /// </summary>
    /// <param name="storedPowerUp">Stored active payload to restore.</param>
    /// <param name="targetSlotIndex">Slot index to restore into. 0 is primary and 1 is secondary.</param>
    /// <param name="restoredEquipOrder">Original positive equip-order marker to preserve, or 0 to assign a new order.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <returns>True when the stored power-up was restored directly; otherwise false.</returns>
    public static bool TryRestoreStoredPowerUpToVacantSlot(in PlayerStoredActivePowerUpData storedPowerUp,
                                                           int targetSlotIndex,
                                                           int restoredEquipOrder,
                                                           ref PlayerPowerUpsConfig powerUpsConfig,
                                                           ref PlayerPowerUpsState powerUpsState)
    {
        return TryRestoreStoredPowerUpToVacantSlot(in storedPowerUp,
                                                   targetSlotIndex,
                                                   restoredEquipOrder,
                                                   ref powerUpsConfig.PrimarySlot,
                                                   ref powerUpsConfig.SecondarySlot,
                                                   ref powerUpsState);
    }

    /// <summary>
    /// Restores one stored active power-up into direct slot payloads when the requested destination slot is still vacant.
    /// </summary>
    /// <param name="storedPowerUp">Stored active payload to restore.</param>
    /// <param name="targetSlotIndex">Slot index to restore into. 0 is primary and 1 is secondary.</param>
    /// <param name="restoredEquipOrder">Original positive equip-order marker to preserve, or 0 to assign a new order.</param>
    /// <param name="primarySlotConfig">Mutable primary active slot payload.</param>
    /// <param name="secondarySlotConfig">Mutable secondary active slot payload.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <returns>True when the stored power-up was restored directly; otherwise false.</returns>
    public static bool TryRestoreStoredPowerUpToVacantSlot(in PlayerStoredActivePowerUpData storedPowerUp,
                                                           int targetSlotIndex,
                                                           int restoredEquipOrder,
                                                           ref PlayerPowerUpSlotConfig primarySlotConfig,
                                                           ref PlayerPowerUpSlotConfig secondarySlotConfig,
                                                           ref PlayerPowerUpsState powerUpsState)
    {
        if (storedPowerUp.SlotConfig.IsDefined == 0)
            return false;

        switch (targetSlotIndex)
        {
            case 0:
                if (primarySlotConfig.IsDefined != 0)
                    return false;

                ApplyStoredPowerUpToSlot(in storedPowerUp,
                                         targetSlotIndex,
                                         restoredEquipOrder,
                                         ref primarySlotConfig,
                                         ref secondarySlotConfig,
                                         ref powerUpsState);
                return true;

            case 1:
                if (secondarySlotConfig.IsDefined != 0)
                    return false;

                ApplyStoredPowerUpToSlot(in storedPowerUp,
                                         targetSlotIndex,
                                         restoredEquipOrder,
                                         ref primarySlotConfig,
                                         ref secondarySlotConfig,
                                         ref powerUpsState);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Swaps active-slot configs and the runtime data that belongs to each equipped power-up.
    /// </summary>
    /// <param name="powerUpsConfig">Runtime active-slot config to mutate.</param>
    /// <param name="powerUpsState">Runtime active-slot state to mutate.</param>
    public static void SwapActiveSlotRuntimeData(ref PlayerPowerUpsConfig powerUpsConfig, ref PlayerPowerUpsState powerUpsState)
    {
        SwapValues(ref powerUpsConfig.PrimarySlot, ref powerUpsConfig.SecondarySlot);
        SwapValues(ref powerUpsState.PrimaryEnergy, ref powerUpsState.SecondaryEnergy);
        SwapValues(ref powerUpsState.PrimaryCooldownRemaining, ref powerUpsState.SecondaryCooldownRemaining);
        SwapValues(ref powerUpsState.PrimaryCharge, ref powerUpsState.SecondaryCharge);
        SwapValues(ref powerUpsState.PrimaryMaintenanceTickTimer, ref powerUpsState.SecondaryMaintenanceTickTimer);
        SwapValues(ref powerUpsState.PrimaryIsCharging, ref powerUpsState.SecondaryIsCharging);
        SwapValues(ref powerUpsState.PrimaryIsActive, ref powerUpsState.SecondaryIsActive);
        SwapValues(ref powerUpsState.PrimaryEquipOrder, ref powerUpsState.SecondaryEquipOrder);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the slot that should receive a picked-up container payload.
    /// </summary>
    /// <param name="requestedSlotIndex">Slot index requested by UI input. 0 is primary and 1 is secondary.</param>
    /// <param name="powerUpsConfig">Current runtime loadout config used to prefer vacant slots.</param>
    /// <returns>Resolved slot index, or -1 when the requested slot is invalid.</returns>
    private static int ResolveContainerPickupSlotIndex(int requestedSlotIndex, in PlayerPowerUpsConfig powerUpsConfig)
    {
        if (requestedSlotIndex < 0 || requestedSlotIndex > 1)
            return -1;

        if (powerUpsConfig.PrimarySlot.IsDefined == 0)
            return 0;

        if (powerUpsConfig.SecondarySlot.IsDefined == 0)
            return 1;

        return requestedSlotIndex;
    }

    /// <summary>
    /// Seeds deterministic age ordering for the active slots currently present in the runtime loadout.
    /// </summary>
    /// <param name="powerUpsState">Runtime state receiving the initial order markers.</param>
    /// <param name="powerUpsConfig">Runtime config inspected for defined slots.</param>
    private static void AssignInitialEquipOrders(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpsConfig powerUpsConfig)
    {
        int nextEquipOrder = 1;
        powerUpsState.PrimaryEquipOrder = 0;
        powerUpsState.SecondaryEquipOrder = 0;

        if (powerUpsConfig.PrimarySlot.IsDefined != 0)
        {
            powerUpsState.PrimaryEquipOrder = nextEquipOrder;
            nextEquipOrder += 1;
        }

        if (powerUpsConfig.SecondarySlot.IsDefined != 0)
        {
            powerUpsState.SecondaryEquipOrder = nextEquipOrder;
            nextEquipOrder += 1;
        }

        powerUpsState.NextEquipOrder = nextEquipOrder;
    }

    /// <summary>
    /// Resolves which active slot should receive the next equipped power-up.
    /// </summary>
    /// <param name="powerUpsConfig">Current runtime loadout config.</param>
    /// <param name="powerUpsState">Current runtime slot state containing equip-order metadata.</param>
    /// <returns>0 for primary or 1 for secondary.</returns>
    private static int ResolveOldestActiveSlotIndex(in PlayerPowerUpsConfig powerUpsConfig, in PlayerPowerUpsState powerUpsState)
    {
        bool hasPrimary = powerUpsConfig.PrimarySlot.IsDefined != 0;
        bool hasSecondary = powerUpsConfig.SecondarySlot.IsDefined != 0;

        // Fill vacant slots first so intentionally empty startup loadout slots stay available until the player acquires something.
        if (!hasPrimary)
            return 0;

        if (!hasSecondary)
            return 1;

        int primaryOrder = powerUpsState.PrimaryEquipOrder > 0 ? powerUpsState.PrimaryEquipOrder : 1;
        int secondaryOrder = powerUpsState.SecondaryEquipOrder > 0 ? powerUpsState.SecondaryEquipOrder : 2;
        return primaryOrder <= secondaryOrder ? 0 : 1;
    }

    /// <summary>
    /// Captures one runtime active slot as container-ready stored data.
    /// </summary>
    /// <param name="slotIndex">Slot index to snapshot. 0 is primary and 1 is secondary.</param>
    /// <param name="storedStateMode">Storage policy applied while building the snapshot.</param>
    /// <param name="powerUpsConfig">Runtime loadout config inspected for slot data.</param>
    /// <param name="powerUpsState">Runtime slot state inspected for energy and cooldown values.</param>
    /// <returns>Stored slot snapshot ready to serialize into a dropped container.</returns>
    private static PlayerStoredActivePowerUpData CaptureStoredPowerUp(int slotIndex,
                                                                      PlayerPowerUpContainerStoredStateMode storedStateMode,
                                                                      in PlayerPowerUpsConfig powerUpsConfig,
                                                                      in PlayerPowerUpsState powerUpsState)
    {
        PlayerStoredActivePowerUpData storedPowerUp = default;
        CaptureStoredPowerUp(slotIndex,
                             storedStateMode,
                             in powerUpsConfig.PrimarySlot,
                             in powerUpsConfig.SecondarySlot,
                             in powerUpsState,
                             ref storedPowerUp);
        return storedPowerUp;
    }

    /// <summary>
    /// Captures one runtime active slot from direct slot payloads into an existing stored-data target.
    /// </summary>
    /// <param name="slotIndex">Slot index to snapshot. 0 is primary and 1 is secondary.</param>
    /// <param name="storedStateMode">Storage policy applied while building the snapshot.</param>
    /// <param name="primarySlotConfig">Current primary active slot payload.</param>
    /// <param name="secondarySlotConfig">Current secondary active slot payload.</param>
    /// <param name="powerUpsState">Runtime slot state inspected for energy and cooldown values.</param>
    /// <param name="storedPowerUp">Stored slot snapshot target reused by callers that already own storage.</param>
    private static void CaptureStoredPowerUp(int slotIndex,
                                             PlayerPowerUpContainerStoredStateMode storedStateMode,
                                             in PlayerPowerUpSlotConfig primarySlotConfig,
                                             in PlayerPowerUpSlotConfig secondarySlotConfig,
                                             in PlayerPowerUpsState powerUpsState,
                                             ref PlayerStoredActivePowerUpData storedPowerUp)
    {
        storedPowerUp = default;

        switch (slotIndex)
        {
            case 0:
                WriteStoredPowerUpSnapshot(in primarySlotConfig,
                                           powerUpsState.PrimaryEnergy,
                                           powerUpsState.PrimaryCooldownRemaining,
                                           storedStateMode,
                                           ref storedPowerUp);
                return;

            case 1:
                WriteStoredPowerUpSnapshot(in secondarySlotConfig,
                                           powerUpsState.SecondaryEnergy,
                                           powerUpsState.SecondaryCooldownRemaining,
                                           storedStateMode,
                                           ref storedPowerUp);
                return;
        }
    }

    /// <summary>
    /// Writes one active slot and its persisted runtime resources into stored active data.
    /// </summary>
    /// <param name="slotConfig">Slot payload to store.</param>
    /// <param name="currentEnergy">Current energy value owned by the slot.</param>
    /// <param name="currentCooldownRemaining">Current cooldown value owned by the slot.</param>
    /// <param name="storedStateMode">Storage policy applied while building the snapshot.</param>
    /// <param name="storedPowerUp">Stored slot snapshot receiving the payload.</param>
    private static void WriteStoredPowerUpSnapshot(in PlayerPowerUpSlotConfig slotConfig,
                                                   float currentEnergy,
                                                   float currentCooldownRemaining,
                                                   PlayerPowerUpContainerStoredStateMode storedStateMode,
                                                   ref PlayerStoredActivePowerUpData storedPowerUp)
    {
        if (slotConfig.IsDefined == 0)
            return;

        float storedEnergy = currentEnergy;
        float storedCooldownRemaining = currentCooldownRemaining;

        if (storedStateMode == PlayerPowerUpContainerStoredStateMode.ResetEnergyAndCooldown)
        {
            storedEnergy = math.max(0f, slotConfig.MaximumEnergy);
            storedCooldownRemaining = 0f;
        }

        storedPowerUp.SlotConfig = slotConfig;
        storedPowerUp.StoredEnergy = math.clamp(storedEnergy, 0f, math.max(0f, slotConfig.MaximumEnergy));
        storedPowerUp.StoredCooldownRemaining = math.clamp(storedCooldownRemaining, 0f, math.max(0f, slotConfig.CooldownSeconds));
    }

    /// <summary>
    /// Applies one stored container payload into the selected active slot while resetting non-persisted transient state.
    /// </summary>
    /// <param name="storedPowerUp">Stored payload restored from a dropped container.</param>
    /// <param name="targetSlotIndex">Slot index receiving the payload. 0 is primary and 1 is secondary.</param>
    /// <param name="restoredEquipOrder">Original positive equip-order marker to preserve, or 0 to assign a new order.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    private static void ApplyStoredPowerUpToSlot(in PlayerStoredActivePowerUpData storedPowerUp,
                                                 int targetSlotIndex,
                                                 int restoredEquipOrder,
                                                 ref PlayerPowerUpsConfig powerUpsConfig,
                                                 ref PlayerPowerUpsState powerUpsState)
    {
        ApplyStoredPowerUpToSlot(in storedPowerUp,
                                 targetSlotIndex,
                                 restoredEquipOrder,
                                 ref powerUpsConfig.PrimarySlot,
                                 ref powerUpsConfig.SecondarySlot,
                                 ref powerUpsState);
    }

    /// <summary>
    /// Applies one stored payload into direct slot fields while resetting non-persisted transient state.
    /// </summary>
    /// <param name="storedPowerUp">Stored payload restored from a dropped container.</param>
    /// <param name="targetSlotIndex">Slot index receiving the payload. 0 is primary and 1 is secondary.</param>
    /// <param name="restoredEquipOrder">Original positive equip-order marker to preserve, or 0 to assign a new order.</param>
    /// <param name="primarySlotConfig">Mutable primary active slot payload.</param>
    /// <param name="secondarySlotConfig">Mutable secondary active slot payload.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    private static void ApplyStoredPowerUpToSlot(in PlayerStoredActivePowerUpData storedPowerUp,
                                                 int targetSlotIndex,
                                                 int restoredEquipOrder,
                                                 ref PlayerPowerUpSlotConfig primarySlotConfig,
                                                 ref PlayerPowerUpSlotConfig secondarySlotConfig,
                                                 ref PlayerPowerUpsState powerUpsState)
    {
        switch (targetSlotIndex)
        {
            case 0:
                primarySlotConfig = storedPowerUp.SlotConfig;
                powerUpsState.PrimaryEnergy = math.clamp(storedPowerUp.StoredEnergy, 0f, math.max(0f, storedPowerUp.SlotConfig.MaximumEnergy));
                powerUpsState.PrimaryCooldownRemaining = math.clamp(storedPowerUp.StoredCooldownRemaining, 0f, math.max(0f, storedPowerUp.SlotConfig.CooldownSeconds));
                powerUpsState.PrimaryCharge = 0f;
                powerUpsState.PrimaryMaintenanceTickTimer = 0f;
                powerUpsState.PrimaryIsCharging = 0;
                powerUpsState.PrimaryIsActive = 0;
                powerUpsState.PrimaryEquipOrder = ResolveRestoredEquipOrder(ref powerUpsState, restoredEquipOrder);
                return;
            case 1:
                secondarySlotConfig = storedPowerUp.SlotConfig;
                powerUpsState.SecondaryEnergy = math.clamp(storedPowerUp.StoredEnergy, 0f, math.max(0f, storedPowerUp.SlotConfig.MaximumEnergy));
                powerUpsState.SecondaryCooldownRemaining = math.clamp(storedPowerUp.StoredCooldownRemaining, 0f, math.max(0f, storedPowerUp.SlotConfig.CooldownSeconds));
                powerUpsState.SecondaryCharge = 0f;
                powerUpsState.SecondaryMaintenanceTickTimer = 0f;
                powerUpsState.SecondaryIsCharging = 0;
                powerUpsState.SecondaryIsActive = 0;
                powerUpsState.SecondaryEquipOrder = ResolveRestoredEquipOrder(ref powerUpsState, restoredEquipOrder);
                return;
        }
    }

    /// <summary>
    /// Resets the runtime resource state owned by the primary active slot.
    /// </summary>
    /// <param name="powerUpsState">Runtime state to mutate.</param>
    /// <param name="slotConfig">Slot config that now owns the primary slot.</param>
    private static void ResetPrimaryRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpSlotConfig slotConfig)
    {
        powerUpsState.PrimaryEnergy = math.max(0f, slotConfig.MaximumEnergy);
        powerUpsState.PrimaryCooldownRemaining = 0f;
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.PrimaryMaintenanceTickTimer = 0f;
        powerUpsState.PrimaryIsCharging = 0;
        powerUpsState.PrimaryIsActive = 0;
    }

    /// <summary>
    /// Resets the runtime resource state owned by the secondary active slot.
    /// </summary>
    /// <param name="powerUpsState">Runtime state to mutate.</param>
    /// <param name="slotConfig">Slot config that now owns the secondary slot.</param>
    private static void ResetSecondaryRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpSlotConfig slotConfig)
    {
        powerUpsState.SecondaryEnergy = math.max(0f, slotConfig.MaximumEnergy);
        powerUpsState.SecondaryCooldownRemaining = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.SecondaryMaintenanceTickTimer = 0f;
        powerUpsState.SecondaryIsCharging = 0;
        powerUpsState.SecondaryIsActive = 0;
    }

    /// <summary>
    /// Preserves a restored active's original order marker while keeping the next-order cursor monotonic.
    /// </summary>
    /// <param name="powerUpsState">Runtime active state that stores the next available order value.</param>
    /// <param name="restoredEquipOrder">Original positive order marker to preserve, or 0 to consume a new order.</param>
    /// <returns>Equip-order marker assigned to the restored active slot.</returns>
    private static int ResolveRestoredEquipOrder(ref PlayerPowerUpsState powerUpsState, int restoredEquipOrder)
    {
        if (restoredEquipOrder <= 0)
            return ConsumeNextEquipOrder(ref powerUpsState);

        if (powerUpsState.NextEquipOrder <= restoredEquipOrder && restoredEquipOrder < int.MaxValue)
            powerUpsState.NextEquipOrder = restoredEquipOrder + 1;

        return restoredEquipOrder;
    }

    /// <summary>
    /// Consumes the next monotonically increasing equip-order value.
    /// </summary>
    /// <param name="powerUpsState">Runtime state that stores the next available order value.</param>
    /// <returns>Equip-order value assigned to the newly equipped active power-up.</returns>
    private static int ConsumeNextEquipOrder(ref PlayerPowerUpsState powerUpsState)
    {
        int nextEquipOrder = powerUpsState.NextEquipOrder > 0 ? powerUpsState.NextEquipOrder : 1;

        if (nextEquipOrder < int.MaxValue)
            powerUpsState.NextEquipOrder = nextEquipOrder + 1;

        return nextEquipOrder;
    }

    /// <summary>
    /// Swaps two values in place.
    /// </summary>
    /// <typeparam name="T">Value type being exchanged.</typeparam>
    /// <param name="leftValue">First value to swap.</param>
    /// <param name="rightValue">Second value to swap.</param>
    private static void SwapValues<T>(ref T leftValue, ref T rightValue)
    {
        T temporaryValue = leftValue;
        leftValue = rightValue;
        rightValue = temporaryValue;
    }
    #endregion

    #endregion
}
