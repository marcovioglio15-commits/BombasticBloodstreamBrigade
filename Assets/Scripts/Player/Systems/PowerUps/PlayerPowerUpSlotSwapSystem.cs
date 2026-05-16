using Unity.Entities;

/// <summary>
/// Swaps the primary and secondary active power-up slots on a rising-edge input before activation logic consumes slot state.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
[UpdateAfter(typeof(PlayerPowerUpRechargeSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerPowerUpSlotSwapSystem : ISystem
{
    #region Constants
    private const float InputPressThreshold = 0.5f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to process active-slot swaps.
    /// </summary>
    /// <param name="state">Current ECS system state used to declare update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    /// <summary>
    /// Applies one slot swap per fresh input press and preserves per-tool energy and cooldown ownership across the swap.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRO<PlayerInputState> inputState,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState) in SystemAPI.Query<RefRO<PlayerInputState>,
                                                                               RefRW<PlayerPowerUpsConfig>,
                                                                               RefRW<PlayerPowerUpsState>>().WithAll<PlayerControllerConfig>())
        {
            bool swapPressed = inputState.ValueRO.SwapPowerUpSlots > InputPressThreshold;
            bool swapPressedThisFrame = swapPressed && powerUpsState.ValueRO.PreviousSwapSlotsPressed == 0;
            powerUpsState.ValueRW.PreviousSwapSlotsPressed = swapPressed ? (byte)1 : (byte)0;

            if (!swapPressedThisFrame)
                continue;

            if (!CanSwapSlots(in powerUpsConfig.ValueRO))
                continue;

            ApplySlotSwap(ref powerUpsConfig.ValueRW,
                          ref powerUpsState.ValueRW,
                          in inputState.ValueRO);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether the runtime loadout currently exposes at least one active slot worth swapping.
    /// </summary>
    /// <param name="powerUpsConfig">Runtime active-slot configuration to inspect.</param>
    /// <returns>True when at least one slot is defined; otherwise false.</returns>
    private static bool CanSwapSlots(in PlayerPowerUpsConfig powerUpsConfig)
    {
        return powerUpsConfig.PrimarySlot.IsDefined != 0 || powerUpsConfig.SecondarySlot.IsDefined != 0;
    }

    /// <summary>
    /// Swaps slot configs and runtime ownership values while clearing transient charge state that cannot survive a layout flip safely.
    /// </summary>
    /// <param name="powerUpsConfig">Runtime active-slot configuration to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to mutate.</param>
    /// <param name="inputState">Current input snapshot used to realign pressed-edge tracking after the swap.</param>
    private static void ApplySlotSwap(ref PlayerPowerUpsConfig powerUpsConfig,
                                      ref PlayerPowerUpsState powerUpsState,
                                      in PlayerInputState inputState)
    {
        PlayerPowerUpLoadoutRuntimeUtility.SwapActiveSlotRuntimeData(ref powerUpsConfig, ref powerUpsState);
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.PrimaryIsCharging = 0;
        powerUpsState.SecondaryIsCharging = 0;
        powerUpsState.IsShootingSuppressed = 0;
        powerUpsState.PreviousPrimaryPressed = inputState.PowerUpPrimary > InputPressThreshold ? (byte)1 : (byte)0;
        powerUpsState.PreviousSecondaryPressed = inputState.PowerUpSecondary > InputPressThreshold ? (byte)1 : (byte)0;
    }
    #endregion

    #endregion
}
