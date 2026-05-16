using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Computes the authoritative terminal outcome for the current player run without reloading the gameplay scene immediately.
/// Defeat is triggered by zero health, while victory requires every authored enemy wave to complete.
/// None.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemySystemGroup))]
public partial struct PlayerRunOutcomeSystem : ISystem
{
    #region Fields
    private EntityQuery activeBossMinionQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime state required by run-outcome evaluation.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        activeBossMinionQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemyBossMinionOwner, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build(ref state);

        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
    }

    /// <summary>
    /// Finalizes defeat or victory once the corresponding gameplay condition is satisfied.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        DynamicBuffer<GameAudioEventRequest> audioRequests = default;
        bool canEnqueueAudioRequests = SystemAPI.TryGetSingletonBuffer<GameAudioEventRequest>(out audioRequests);

        foreach ((RefRW<PlayerRunOutcomeState> runOutcomeState,
                  RefRO<PlayerHealth> playerHealth,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRW<PlayerRunOutcomeState>,
                                    RefRO<PlayerHealth>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            if (runOutcomeState.ValueRO.IsFinalized != 0)
                continue;

            if (entityManager.HasComponent<PlayerRunTimerConfig>(playerEntity) &&
                entityManager.HasComponent<PlayerRunTimerState>(playerEntity))
            {
                PlayerRunTimerConfig timerConfig = entityManager.GetComponentData<PlayerRunTimerConfig>(playerEntity);
                PlayerRunTimerState timerState = entityManager.GetComponentData<PlayerRunTimerState>(playerEntity);

                if (timerConfig.Direction == PlayerRunTimerDirection.Backward && timerState.Expired != 0)
                {
                    FinalizeOutcome(ref runOutcomeState.ValueRW, PlayerRunOutcome.Defeat);

                    if (canEnqueueAudioRequests)
                        GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.PlayerDeath);

                    continue;
                }
            }

            if (playerHealth.ValueRO.Current <= 0f)
            {
                FinalizeOutcome(ref runOutcomeState.ValueRW, PlayerRunOutcome.Defeat);

                if (canEnqueueAudioRequests)
                    GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.PlayerDeath);

                continue;
            }

            bool anySpawnerFound = false;
            bool allSpawnersCompleted = true;
            bool anyWaveFound = false;

            foreach ((RefRO<EnemySpawner> _,
                      RefRO<EnemySpawnerState> spawnerState,
                      DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntimeBuffer)
                     in SystemAPI.Query<RefRO<EnemySpawner>,
                                        RefRO<EnemySpawnerState>,
                                        DynamicBuffer<EnemySpawnerWaveRuntimeElement>>())
            {
                anySpawnerFound = true;

                if (spawnerState.ValueRO.Initialized == 0)
                {
                    allSpawnersCompleted = false;
                    break;
                }

                if (spawnerState.ValueRO.AliveCount > 0)
                {
                    allSpawnersCompleted = false;
                    break;
                }

                if (waveRuntimeBuffer.Length <= 0)
                {
                    allSpawnersCompleted = false;
                    break;
                }

                for (int waveIndex = 0; waveIndex < waveRuntimeBuffer.Length; waveIndex++)
                {
                    anyWaveFound = true;

                    if (waveRuntimeBuffer[waveIndex].Completed != 0)
                        continue;

                    allSpawnersCompleted = false;
                    break;
                }

                if (!allSpawnersCompleted)
                    break;
            }

            if (!anySpawnerFound)
                continue;

            if (!anyWaveFound)
                continue;

            if (!allSpawnersCompleted)
                continue;

            if (HasCompletionBlockingBossMinions(activeBossMinionQuery))
                continue;

            FinalizeOutcome(ref runOutcomeState.ValueRW, PlayerRunOutcome.Victory);

            if (canEnqueueAudioRequests)
                GameAudioEventRequestUtility.EnqueueGlobal(audioRequests, GameAudioEventId.PlayerVictory);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes the resolved terminal run outcome once and marks the state as finalized.
    /// </summary>
    /// <param name="runOutcomeState">Mutable runtime state stored on the local player entity.</param>
    /// <param name="outcome">Terminal outcome that should be exposed to UI.</param>
    private static void FinalizeOutcome(ref PlayerRunOutcomeState runOutcomeState, PlayerRunOutcome outcome)
    {
        runOutcomeState.Outcome = outcome;
        runOutcomeState.IsFinalized = 1;
        runOutcomeState.RuntimeFreezeApplied = 0;
    }

    /// <summary>
    /// Resolves whether any active boss minion is configured to delay run completion after its boss dies.
    /// </summary>
    /// <param name="activeBossMinionQuery">Query matching active boss-owned minions without despawn requests.</param>
    /// <returns>True when at least one active minion blocks victory.</returns>
    private static bool HasCompletionBlockingBossMinions(EntityQuery activeBossMinionQuery)
    {
        if (activeBossMinionQuery.IsEmptyIgnoreFilter)
            return false;

        NativeArray<EnemyBossMinionOwner> minionOwners = activeBossMinionQuery.ToComponentDataArray<EnemyBossMinionOwner>(Allocator.Temp);

        try
        {
            for (int index = 0; index < minionOwners.Length; index++)
            {
                if (minionOwners[index].BlocksRunCompletion != 0)
                    return true;
            }
        }
        finally
        {
            if (minionOwners.IsCreated)
                minionOwners.Dispose();
        }

        return false;
    }
    #endregion

    #endregion
}
