using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Converts final hold-charge slot state into a charge-proportional Impact Frame build-in request.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateBefore(typeof(PlayerImpactFrameUpdateSystem))]
public partial struct PlayerImpactFrameChargeBuildInSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the power-up configuration, slot state, and build-in output required by the system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfigElement>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerImpactFrameBuildInState>();
    }

    /// <summary>
    /// Requests the strongest enabled build-in profile from the currently charging active slots.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpsConfigElement> powerUpsConfigBuffer,
                  RefRO<PlayerPowerUpsState> powerUpsState,
                  RefRW<PlayerImpactFrameBuildInState> buildInState)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpsConfigElement>,
                                    RefRO<PlayerPowerUpsState>,
                                    RefRW<PlayerImpactFrameBuildInState>>())
        {
            PlayerPowerUpsConfig powerUpsConfig;
            PlayerPowerUpsConfigBufferUtility.Read(powerUpsConfigBuffer, out powerUpsConfig);

            RequestSlotBuildIn(in powerUpsConfig.PrimarySlot,
                               powerUpsState.ValueRO.PrimaryCharge,
                               powerUpsState.ValueRO.PrimaryIsCharging,
                               ref buildInState.ValueRW);
            RequestSlotBuildIn(in powerUpsConfig.SecondarySlot,
                               powerUpsState.ValueRO.SecondaryCharge,
                               powerUpsState.ValueRO.SecondaryIsCharging,
                               ref buildInState.ValueRW);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Requests one build-in profile when the inspected slot is an active hold-charge tool with Impact Frame support.
    /// </summary>
    /// <param name="slotConfig">Resolved active-slot configuration.</param>
    /// <param name="charge">Current stored charge for the slot.</param>
    /// <param name="isCharging">One while the slot is actively charging.</param>
    /// <param name="buildInState">Mutable shared build-in state receiving the request.</param>
    private static void RequestSlotBuildIn(in PlayerPowerUpSlotConfig slotConfig,
                                           float charge,
                                           byte isCharging,
                                           ref PlayerImpactFrameBuildInState buildInState)
    {
        if (isCharging == 0 ||
            slotConfig.IsDefined == 0 ||
            slotConfig.ToolKind != ActiveToolKind.ChargeShot ||
            slotConfig.HasImpactFrame == 0 ||
            slotConfig.ImpactFrame.BuildIn.Enabled == 0)
            return;

        float maximumCharge = math.max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);

        if (maximumCharge <= 0f)
            return;

        PlayerImpactFrameBuildInRuntimeUtility.Request(ref buildInState,
                                                       in slotConfig.ImpactFrame.BuildIn,
                                                       math.saturate(math.max(0f, charge) / maximumCharge));
    }
    #endregion

    #endregion
}
