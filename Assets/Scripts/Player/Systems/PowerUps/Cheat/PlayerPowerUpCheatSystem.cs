using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes pending runtime cheat commands and replaces the player's whole power-up loadout with a baked preset snapshot.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerPowerUpRechargeSystem))]
public partial struct PlayerPowerUpCheatSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers all component requirements needed by runtime cheat preset application.
    /// </summary>
    /// <param name="state">System state used to declare update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpCheatPresetEntry>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetSlotElement>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetPassiveElement>();
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPassiveToolsStateElement>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
    }

    /// <summary>
    /// Applies pending cheat commands for each player, replacing runtime config and passives when a preset swap is requested.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                  DynamicBuffer<PlayerPowerUpCheatPresetSlotElement> cheatPresetSlots,
                  DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                  DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  DynamicBuffer<PlayerPassiveToolsStateElement> passiveToolsStateBuffer) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatPresetEntry>,
                                                                                       DynamicBuffer<PlayerPowerUpCheatPresetSlotElement>,
                                                                                       DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement>,
                                                                                       DynamicBuffer<PlayerPowerUpsConfigElement>,
                                                                                       RefRW<PlayerPowerUpsState>,
                                                                                       DynamicBuffer<EquippedPassiveToolElement>,
                                                                                       DynamicBuffer<PlayerPassiveToolsStateElement>>())
        {
            if (!TryConsumePendingCommand(ref powerUpsState.ValueRW,
                                          out PlayerPowerUpCheatCommandType commandType,
                                          out int presetIndex))
                continue;

            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer,
                                                   out powerUpsConfig);
            bool passivesChanged = ProcessCheatCommand(commandType,
                                                       presetIndex,
                                                       cheatPresetEntries,
                                                       cheatPresetSlots,
                                                       cheatPresetPassives,
                                                       ref powerUpsConfig,
                                                       ref powerUpsState.ValueRW,
                                                       equippedPassiveTools);

            if (passivesChanged)
            {
                PlayerPowerUpsConfigBufferUtility.Write(powerUpsConfigBuffer, in powerUpsConfig);
                ref PlayerPassiveToolsState passiveToolsState = ref PlayerPassiveToolsStateBufferUtility.GetStateRef(passiveToolsStateBuffer);
                PlayerPassiveToolsAggregationUtility.RebuildPassiveToolsState(equippedPassiveTools,
                                                                              ref passiveToolsState);
            }
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Routes one command to the matching cheat action.
    /// </summary>
    /// <param name="commandType">Pending command kind to process.</param>
    /// <param name="presetIndex">Pending preset index used by apply-preset commands.</param>
    /// <param name="cheatPresetEntries">Baked preset metadata buffer.</param>
    /// <param name="cheatPresetSlots">Flattened baked active-slot buffer.</param>
    /// <param name="cheatPresetPassives">Flattened baked passives buffer.</param>
    /// <param name="powerUpsConfig">Runtime power-up config to mutate.</param>
    /// <param name="powerUpsState">Runtime power-up state to reset.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer to replace.</param>
    /// <returns>True when runtime loadout was changed, otherwise false.</returns>
    private static bool ProcessCheatCommand(PlayerPowerUpCheatCommandType commandType,
                                            int presetIndex,
                                            DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                                            DynamicBuffer<PlayerPowerUpCheatPresetSlotElement> cheatPresetSlots,
                                            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        switch (commandType)
        {
            case PlayerPowerUpCheatCommandType.ApplyPresetByIndex:
                return TryApplyPresetByIndex(presetIndex,
                                             cheatPresetEntries,
                                             cheatPresetSlots,
                                             cheatPresetPassives,
                                             ref powerUpsConfig,
                                             ref powerUpsState,
                                             equippedPassiveTools);
            default:
                return false;
        }
    }

    /// <summary>
    /// Reads and clears the single pending cheat command stored in runtime power-up state.
    /// </summary>
    /// <param name="powerUpsState">Runtime power-up state that owns pending cheat input.</param>
    /// <param name="commandType">Resolved pending command type.</param>
    /// <param name="presetIndex">Resolved pending preset index.</param>
    /// <returns>True when a pending command was present.</returns>
    private static bool TryConsumePendingCommand(ref PlayerPowerUpsState powerUpsState,
                                                 out PlayerPowerUpCheatCommandType commandType,
                                                 out int presetIndex)
    {
        commandType = powerUpsState.PendingCheatCommandType;
        presetIndex = powerUpsState.PendingCheatPresetIndex;

        if (powerUpsState.HasPendingCheatCommand == 0)
            return false;

        powerUpsState.HasPendingCheatCommand = 0;
        powerUpsState.PendingCheatCommandType = PlayerPowerUpCheatCommandType.None;
        powerUpsState.PendingCheatPresetIndex = -1;
        return true;
    }

    /// <summary>
    /// Applies a full preset snapshot by index when the entry exists.
    /// </summary>
    /// <param name="presetIndex">Requested snapshot index.</param>
    /// <param name="cheatPresetEntries">Baked preset metadata buffer.</param>
    /// <param name="cheatPresetSlots">Flattened baked active-slot buffer.</param>
    /// <param name="cheatPresetPassives">Flattened baked passives buffer.</param>
    /// <param name="powerUpsConfig">Runtime power-up config to mutate.</param>
    /// <param name="powerUpsState">Runtime state to reset after replacement.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer to replace.</param>
    /// <returns>True when the preset was found and applied, otherwise false.</returns>
    private static bool TryApplyPresetByIndex(int presetIndex,
                                              DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                                              DynamicBuffer<PlayerPowerUpCheatPresetSlotElement> cheatPresetSlots,
                                              DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                              ref PlayerPowerUpsConfig powerUpsConfig,
                                              ref PlayerPowerUpsState powerUpsState,
                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (presetIndex < 0)
            return false;

        if (presetIndex >= cheatPresetEntries.Length)
            return false;

        PlayerPowerUpCheatPresetEntry cheatPresetEntry = cheatPresetEntries[presetIndex];

        if (cheatPresetEntry.IsDefined == 0)
            return false;

        PlayerPowerUpCheatPresetSlotBufferUtility.Read(in cheatPresetEntry,
                                                       cheatPresetSlots,
                                                       out powerUpsConfig);
        ReplaceEquippedPassivesFromSnapshot(cheatPresetEntry, cheatPresetPassives, equippedPassiveTools);
        PlayerPowerUpLoadoutRuntimeUtility.ResetRuntimeState(ref powerUpsState, in powerUpsConfig);
        return true;
    }

    /// <summary>
    /// Replaces equipped passive tools with the passive range referenced by one baked preset entry.
    /// </summary>
    /// <param name="cheatPresetEntry">Preset metadata containing passive range indices.</param>
    /// <param name="cheatPresetPassives">Flattened source passive payloads.</param>
    /// <param name="equippedPassiveTools">Runtime destination buffer to overwrite.</param>
    private static void ReplaceEquippedPassivesFromSnapshot(in PlayerPowerUpCheatPresetEntry cheatPresetEntry,
                                                            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        equippedPassiveTools.Clear();

        if (cheatPresetPassives.Length <= 0)
            return;

        int safeStartIndex = math.clamp(cheatPresetEntry.PassiveStartIndex, 0, cheatPresetPassives.Length);
        int availableCount = cheatPresetPassives.Length - safeStartIndex;
        int safeCount = math.clamp(cheatPresetEntry.PassiveCount, 0, availableCount);

        for (int passiveOffset = 0; passiveOffset < safeCount; passiveOffset++)
        {
            PlayerPowerUpCheatPresetPassiveElement cheatPresetPassive = cheatPresetPassives[safeStartIndex + passiveOffset];
            int passiveIndex = equippedPassiveTools.Length;
            equippedPassiveTools.ResizeUninitialized(passiveIndex + 1);
            ref EquippedPassiveToolElement equippedPassiveTool = ref equippedPassiveTools.ElementAt(passiveIndex);
            equippedPassiveTool.PowerUpId = cheatPresetPassive.PowerUpId;
            equippedPassiveTool.Tool = cheatPresetPassive.Tool;
        }
    }
    #endregion

    #endregion
}
