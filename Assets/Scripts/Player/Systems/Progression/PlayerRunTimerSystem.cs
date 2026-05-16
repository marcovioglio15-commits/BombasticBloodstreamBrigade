using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Advances the authoritative player run timer and marks countdown expiry before run-outcome evaluation.
/// none.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PlayerRunOutcomeSystem))]
public partial struct PlayerRunTimerSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum runtime data required by the run timer.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerRunTimerConfig>();
        state.RequireForUpdate<PlayerRunTimerState>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
    }

    /// <summary>
    /// Advances the local run timer using the active direction and freezes it once the run outcome is finalized.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        if (deltaTime <= 0f)
            return;

        foreach ((RefRO<PlayerRunTimerConfig> timerConfig,
                  RefRW<PlayerRunTimerState> timerState,
                  RefRO<PlayerRunOutcomeState> runOutcomeState)
                 in SystemAPI.Query<RefRO<PlayerRunTimerConfig>,
                                    RefRW<PlayerRunTimerState>,
                                    RefRO<PlayerRunOutcomeState>>()
                             .WithAll<PlayerControllerConfig>())
        {
            if (runOutcomeState.ValueRO.IsFinalized != 0)
                continue;

            switch (timerConfig.ValueRO.Direction)
            {
                case PlayerRunTimerDirection.Backward:
                    UpdateBackwardTimer(deltaTime, ref timerState.ValueRW);
                    break;

                default:
                    UpdateForwardTimer(deltaTime, ref timerState.ValueRW);
                    break;
            }
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Advances a forward timer.
    /// </summary>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <param name="timerState">Mutable timer state.</param>
    private static void UpdateForwardTimer(float deltaTime, ref PlayerRunTimerState timerState)
    {
        timerState.CurrentSeconds = math.max(0f, timerState.CurrentSeconds) + deltaTime;
        timerState.Expired = 0;
    }

    /// <summary>
    /// Advances a countdown timer and latches expiry when it reaches zero.
    /// </summary>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <param name="timerState">Mutable timer state.</param>
    private static void UpdateBackwardTimer(float deltaTime, ref PlayerRunTimerState timerState)
    {
        if (timerState.Expired != 0)
        {
            timerState.CurrentSeconds = 0f;
            return;
        }

        timerState.CurrentSeconds = math.max(0f, timerState.CurrentSeconds - deltaTime);

        if (timerState.CurrentSeconds > 0f)
            return;

        timerState.CurrentSeconds = 0f;
        timerState.Expired = 1;
    }
    #endregion

    #endregion
}
