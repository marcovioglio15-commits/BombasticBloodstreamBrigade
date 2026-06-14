using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Counts down post-spawn inactivity timers and keeps locked enemies completely idle until release.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
[UpdateBefore(typeof(EnemyShooterRequestSystem))]
[UpdateBefore(typeof(EnemySteeringSystem))]
[UpdateBefore(typeof(EnemyPatternMovementSystem))]
public partial struct EnemySpawnInactivitySystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires the inactivity lock and runtime state before the system starts ticking.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemySpawnInactivityLock>();
        state.RequireForUpdate<EnemyRuntimeState>();
    }

    /// <summary>
    /// Decrements the spawn inactivity timer and releases the lock when the authored delay expires.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        foreach ((RefRW<EnemyRuntimeState> runtimeState,
                  Entity enemyEntity) in SystemAPI.Query<RefRW<EnemyRuntimeState>>()
                                                 .WithAll<EnemyActive, EnemySpawnInactivityLock>()
                                                 .WithNone<EnemyDespawnRequest>()
                                                 .WithEntityAccess())
        {
            EnemyRuntimeState nextRuntimeState = runtimeState.ValueRO;
            nextRuntimeState.Velocity = float3.zero;

            if (deltaTime > 0f)
                nextRuntimeState.SpawnInactivityTimer = math.max(0f, nextRuntimeState.SpawnInactivityTimer - deltaTime);

            runtimeState.ValueRW = nextRuntimeState;

            if (nextRuntimeState.SpawnInactivityTimer <= 0f)
                state.EntityManager.SetComponentEnabled<EnemySpawnInactivityLock>(enemyEntity, false);
        }
    }
    #endregion

    #endregion
}
