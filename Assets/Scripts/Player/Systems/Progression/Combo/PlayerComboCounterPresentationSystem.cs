using Unity.Entities;

/// <summary>
/// Resolves combo rank HUD data after runtime combo thresholds have been rebuilt.
/// none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
[UpdateBefore(typeof(PlayerPowerUpRechargeSystem))]
public partial struct PlayerComboCounterPresentationSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to publish combo presentation data.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComboCounterState>();
        state.RequireForUpdate<PlayerRuntimeComboCounterConfig>();
        state.RequireForUpdate<PlayerRuntimeComboRankElement>();
    }

    /// <summary>
    /// Refreshes rank identifiers, thresholds, and progress normalized values for HUD consumers.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<PlayerComboCounterState> comboCounterState,
                  RefRO<PlayerRuntimeComboCounterConfig> runtimeComboConfig,
                  DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks)
                 in SystemAPI.Query<RefRW<PlayerComboCounterState>,
                                    RefRO<PlayerRuntimeComboCounterConfig>,
                                    DynamicBuffer<PlayerRuntimeComboRankElement>>())
        {
            PlayerComboCounterState mutableComboCounterState = comboCounterState.ValueRO;
            PlayerComboCounterRuntimeUtility.UpdatePresentation(ref mutableComboCounterState,
                                                               in runtimeComboConfig.ValueRO,
                                                               runtimeComboRanks);
            comboCounterState.ValueRW = mutableComboCounterState;
        }
    }
    #endregion

    #endregion
}
