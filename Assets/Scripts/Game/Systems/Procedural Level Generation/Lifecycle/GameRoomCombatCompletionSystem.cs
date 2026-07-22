using Unity.Entities;

/// <summary>
/// Aggregates spawner-wave and Boss-minion completion without temporary arrays or per-frame entity copies.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemySystemGroup))]
[UpdateBefore(typeof(GameProceduralRoomCompletionSystem))]
[UpdateBefore(typeof(PlayerRunOutcomeSystem))]
public partial struct GameRoomCombatCompletionSystem : ISystem
{
    #region Fields
    private EntityQuery completionStateQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Requires the unique scene-manager aggregate before combat completion can be evaluated.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        completionStateQuery = new EntityQueryBuilder(Unity.Collections.Allocator.Temp)
            .WithAllRW<GameRoomCombatCompletionState>()
            .Build(ref state);
        state.RequireForUpdate(completionStateQuery);
    }

    /// <summary>
    /// Scans compact spawner wave buffers and only inspects Boss minions when all waves have completed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (completionStateQuery.CalculateEntityCount() != 1)
            return;

        bool anyWaveFound = false;
        bool allWavesComplete = true;

        // Spawner count is small and stable; direct generated iteration avoids NativeArray materialization.
        foreach ((RefRO<EnemySpawnerState> spawnerState,
                  DynamicBuffer<EnemySpawnerWaveRuntimeElement> waves,
                  Entity spawnerEntity)
                 in SystemAPI.Query<RefRO<EnemySpawnerState>, DynamicBuffer<EnemySpawnerWaveRuntimeElement>>()
                             .WithAll<EnemySpawner>()
                             .WithEntityAccess())
        {
            if (!GameProceduralRoomInstanceQueryUtility.IsEntityInActiveRoom(state.EntityManager, spawnerEntity))
                continue;

            if (!GameRoomCombatCompletionUtility.IsSpawnerComplete(spawnerState.ValueRO,
                                                                   waves,
                                                                   out bool spawnerHasWaves))
            {
                allWavesComplete = false;
            }

            anyWaveFound |= spawnerHasWaves;
        }

        bool hasBlockingBossMinion = false;

        // Boss-minion inspection is unnecessary until the authored waves themselves are complete.
        if (anyWaveFound && allWavesComplete)
        {
            foreach (RefRO<EnemyBossMinionOwner> owner
                     in SystemAPI.Query<RefRO<EnemyBossMinionOwner>>()
                                 .WithAll<EnemyActive>()
                                 .WithNone<EnemyDespawnRequest>())
            {
                if (!GameRoomCombatCompletionUtility.BlocksCompletion(owner.ValueRO))
                    continue;

                hasBlockingBossMinion = true;
                break;
            }
        }

        byte completionValue = anyWaveFound && allWavesComplete && !hasBlockingBossMinion ? (byte)1 : (byte)0;
        Entity completionEntity = completionStateQuery.GetSingletonEntity();
        GameRoomCombatCompletionState completionState = state.EntityManager.GetComponentData<GameRoomCombatCompletionState>(completionEntity);

        if (completionState.IsComplete == completionValue)
            return;

        completionState.IsComplete = completionValue;
        state.EntityManager.SetComponentData(completionEntity, completionState);
    }
    #endregion

    #endregion
}
