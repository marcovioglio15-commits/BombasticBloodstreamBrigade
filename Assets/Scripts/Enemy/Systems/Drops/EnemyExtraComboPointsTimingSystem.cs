using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Advances enemy lifetime timers used by Extra Combo Points conditions.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
[UpdateBefore(typeof(EnemyProjectileHitSystem))]
public partial struct EnemyExtraComboPointsTimingSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime state required by the timing update.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyRuntimeState>();
    }

    /// <summary>
    /// Advances enemy lifetime while the enemy stays active in simulation.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime * enemyTimeScale);

        if (deltaTime <= 0f)
            return;

        foreach (RefRW<EnemyRuntimeState> enemyRuntimeState
                 in SystemAPI.Query<RefRW<EnemyRuntimeState>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>())
        {
            EnemyRuntimeState runtimeState = enemyRuntimeState.ValueRO;
            runtimeState.LifetimeSeconds = math.max(0f, runtimeState.LifetimeSeconds + deltaTime);
            enemyRuntimeState.ValueRW = runtimeState;
        }
    }
    #endregion

    #endregion
}
