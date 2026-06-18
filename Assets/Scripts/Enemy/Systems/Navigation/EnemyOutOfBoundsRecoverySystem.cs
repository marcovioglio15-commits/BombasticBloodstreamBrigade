using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Safety net that pulls active enemies which end up outside the walkable navigation area — whether spawned out of
/// bounds or pushed through a wall by movement/knockback — back to the nearest walkable cell. This keeps every enemy
/// reachable by player fire and prevents a single unreachable enemy from deadlocking wave progression (which only
/// advances once a wave's alive count returns to zero via despawn/kill).
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
[BurstCompile]
public partial struct EnemyOutOfBoundsRecoverySystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires at least one active enemy and a ready navigation grid before running.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyActive>();
        state.RequireForUpdate<EnemyNavigationGridState>();
    }

    /// <summary>
    /// Teleports out-of-bounds enemies to the nearest walkable cell and cancels their residual velocity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out EnemyNavigationGridState navigationGridState))
            return;

        if (navigationGridState.FlowReady == 0)
            return;

        if (!SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out DynamicBuffer<EnemyNavigationCellElement> navigationCellsBuffer))
            return;

        if (navigationCellsBuffer.Length == 0)
            return;

        NativeArray<EnemyNavigationCellElement> navigationCells = navigationCellsBuffer.AsNativeArray();

        foreach ((RefRW<LocalTransform> enemyTransform,
                  RefRW<EnemyRuntimeState> enemyRuntimeState)
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<EnemyRuntimeState>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>())
        {
            if (!EnemyNavigationFlowFieldUtility.TryResolveOutOfBoundsRecovery(enemyTransform.ValueRO.Position,
                                                                              in navigationGridState,
                                                                              navigationCells,
                                                                              out float3 recoveredPosition))
                continue;

            LocalTransform recoveredTransform = enemyTransform.ValueRO;
            recoveredTransform.Position = recoveredPosition;
            enemyTransform.ValueRW = recoveredTransform;

            // Cancel residual motion so a LOD-gated enemy does not immediately drive back out before steering re-aims it.
            EnemyRuntimeState runtimeState = enemyRuntimeState.ValueRO;
            runtimeState.Velocity = float3.zero;
            enemyRuntimeState.ValueRW = runtimeState;
        }
    }
    #endregion

    #endregion
}
