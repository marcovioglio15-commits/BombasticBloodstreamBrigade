using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes runtime cheat commands and replaces the player's whole power-up loadout with a baked preset snapshot.
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
        state.RequireForUpdate<PlayerPowerUpCheatCommand>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetEntry>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetPassiveElement>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
    }

    /// <summary>
    /// Applies queued cheat commands for each player, replacing runtime config and passives when a preset swap is requested.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands,
                  DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                  DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  RefRW<PlayerPassiveToolsState> passiveToolsState) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatCommand>,
                                                                                       DynamicBuffer<PlayerPowerUpCheatPresetEntry>,
                                                                                       DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement>,
                                                                                       RefRW<PlayerPowerUpsConfig>,
                                                                                       RefRW<PlayerPowerUpsState>,
                                                                                       DynamicBuffer<EquippedPassiveToolElement>,
                                                                                       RefRW<PlayerPassiveToolsState>>())
        {
            int commandCount = cheatCommands.Length;

            if (commandCount <= 0)
                continue;

            bool passivesChanged = false;

            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                PlayerPowerUpCheatCommand cheatCommand = cheatCommands[commandIndex];
                bool changed = ProcessCheatCommand(in cheatCommand,
                                                   cheatPresetEntries,
                                                   cheatPresetPassives,
                                                   ref powerUpsConfig.ValueRW,
                                                   ref powerUpsState.ValueRW,
                                                   equippedPassiveTools);

                if (changed)
                    passivesChanged = true;
            }

            if (passivesChanged)
                passiveToolsState.ValueRW = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);

            cheatCommands.Clear();
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Routes one command to the matching cheat action.
    /// </summary>
    /// <param name="cheatCommand">Command payload to process.</param>
    /// <param name="cheatPresetEntries">Baked preset metadata buffer.</param>
    /// <param name="cheatPresetPassives">Flattened baked passives buffer.</param>
    /// <param name="powerUpsConfig">Runtime power-up config to mutate.</param>
    /// <param name="powerUpsState">Runtime power-up state to reset.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer to replace.</param>
    /// <returns>True when runtime loadout was changed, otherwise false.</returns>
    private static bool ProcessCheatCommand(in PlayerPowerUpCheatCommand cheatCommand,
                                            DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                                            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        switch (cheatCommand.CommandType)
        {
            case PlayerPowerUpCheatCommandType.ApplyPresetByIndex:
                return TryApplyPresetByIndex(cheatCommand.PresetIndex,
                                             cheatPresetEntries,
                                             cheatPresetPassives,
                                             ref powerUpsConfig,
                                             ref powerUpsState,
                                             equippedPassiveTools);
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies a full preset snapshot by index when the entry exists.
    /// </summary>
    /// <param name="presetIndex">Requested snapshot index.</param>
    /// <param name="cheatPresetEntries">Baked preset metadata buffer.</param>
    /// <param name="cheatPresetPassives">Flattened baked passives buffer.</param>
    /// <param name="powerUpsConfig">Runtime power-up config to mutate.</param>
    /// <param name="powerUpsState">Runtime state to reset after replacement.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer to replace.</param>
    /// <returns>True when the preset was found and applied, otherwise false.</returns>
    private static bool TryApplyPresetByIndex(int presetIndex,
                                              DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
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

        powerUpsConfig = cheatPresetEntry.PowerUpsConfig;
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
            PlayerPassiveToolConfig passiveToolConfig = cheatPresetPassives[safeStartIndex + passiveOffset].Tool;

            if (passiveToolConfig.IsDefined == 0)
                continue;

            equippedPassiveTools.Add(new EquippedPassiveToolElement
            {
                PowerUpId = default,
                Tool = passiveToolConfig
            });
        }
    }
    #endregion

    #endregion
}
