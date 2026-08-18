using Unity.Entities;

/// <summary>
/// Advances Sudden Strike automatic charge for equipped passives and active toggle slots before movement penalties are applied.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpTogglePassiveSystem))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
[UpdateBefore(typeof(PlayerPowerUpChargeMovementSlowSystem))]
[UpdateBefore(typeof(PlayerShootingIntentSystem))]
public partial struct PlayerSuddenStrikeChargeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the compact runtime data required by automatic conditional charge.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerLookState>();
    }

    /// <summary>
    /// Updates only conditional entries and active toggle slots, leaving ordinary passives outside the per-frame charge path.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  RefRO<PlayerShootingState> shootingState,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<PlayerLookState> lookState,
                  RefRW<PlayerPowerUpsState> powerUpsState)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    DynamicBuffer<EquippedPassiveToolElement>,
                                    RefRO<PlayerShootingState>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<PlayerLookState>,
                                    RefRW<PlayerPowerUpsState>>())
        {
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer, out powerUpsConfig);

            // Equipped passive counts remain small, and ordinary entries exit before any condition math.
            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                ref EquippedPassiveToolElement passive = ref equippedPassiveTools.ElementAt(passiveIndex);

                if (passive.Tool.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.SuddenStrike)
                    continue;

                PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in passive.Tool.ConditionalApplication,
                                                                           deltaTime,
                                                                           in movementState.ValueRO,
                                                                           in lookState.ValueRO,
                                                                           shootingState.ValueRO.ShotPulseVersion,
                                                                           ref passive.ConditionalApplicationState);
            }

            UpdateToggleSlot(in powerUpsConfig.PrimarySlot,
                             powerUpsState.ValueRO.PrimaryIsActive,
                             deltaTime,
                             in movementState.ValueRO,
                             in lookState.ValueRO,
                             shootingState.ValueRO.ShotPulseVersion,
                             ref powerUpsState.ValueRW.PrimaryConditionalApplication);
            UpdateToggleSlot(in powerUpsConfig.SecondarySlot,
                             powerUpsState.ValueRO.SecondaryIsActive,
                             deltaTime,
                             in movementState.ValueRO,
                             in lookState.ValueRO,
                             shootingState.ValueRO.ShotPulseVersion,
                             ref powerUpsState.ValueRW.SecondaryConditionalApplication);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances one active toggle's conditional state or clears it when the slot no longer owns an active Sudden Strike.
    /// </summary>
    /// <param name="slotConfig">Active slot containing the optional toggle passive payload.</param>
    /// <param name="isActive">Whether the toggle is currently active.</param>
    /// <param name="deltaTime">Current frame duration.</param>
    /// <param name="movementState">Current player movement state.</param>
    /// <param name="lookState">Current player look state.</param>
    /// <param name="shotPulseVersion">Current real-shot pulse version.</param>
    /// <param name="runtimeState">Mutable slot-owned conditional state.</param>
    private static void UpdateToggleSlot(in PlayerPowerUpSlotConfig slotConfig,
                                         byte isActive,
                                         float deltaTime,
                                         in PlayerMovementState movementState,
                                         in PlayerLookState lookState,
                                         uint shotPulseVersion,
                                         ref PowerUpConditionalApplicationRuntimeState runtimeState)
    {
        if (isActive == 0 || slotConfig.Toggleable == 0)
        {
            PlayerConditionalPowerUpRuntimeUtility.Reset(ref runtimeState);
            return;
        }

        switch (slotConfig.TogglePassiveTool.ConditionalApplication.Mode)
        {
            case PowerUpConditionalApplicationMode.SuddenStrike:
                PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in slotConfig.TogglePassiveTool.ConditionalApplication,
                                                                           deltaTime,
                                                                           in movementState,
                                                                           in lookState,
                                                                           shotPulseVersion,
                                                                           ref runtimeState);
                return;
            case PowerUpConditionalApplicationMode.DelayedShootApplication:
                return;
            default:
                PlayerConditionalPowerUpRuntimeUtility.Reset(ref runtimeState);
                return;
        }
    }
    #endregion

    #endregion
}
