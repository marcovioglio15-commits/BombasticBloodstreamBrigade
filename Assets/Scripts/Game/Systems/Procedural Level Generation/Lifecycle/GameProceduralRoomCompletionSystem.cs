using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Converts the shared combat-completion predicate into one-shot procedural room progression and room-clear events.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemySystemGroup))]
[UpdateBefore(typeof(PlayerRunOutcomeSystem))]
public partial struct GameProceduralRoomCompletionSystem : ISystem
{
    #region Fields
    private EntityQuery managerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates cached queries for the procedural manager and shared combat-completion predicate.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        managerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAllRW<GameProceduralLevelRuntimeState>()
            .WithAllRW<GameProceduralRoomNodeElement>()
            .WithAllRW<GameProceduralRoomClearCounter>()
            .WithAllRW<GameProceduralRoomClearedEvent>()
            .WithAll<GameProceduralLevelDefinitionElement, GameRoomCombatCompletionState>()
            .Build(ref state);
    }

    /// <summary>
    /// Marks the active room complete once, starts exit activation and exposes terminal Boss completion to run outcome.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameRoomCombatCompletionState completionState = state.EntityManager.GetComponentData<GameRoomCombatCompletionState>(managerEntity);

        if (completionState.IsComplete == 0)
            return;

        GameProceduralRoomCompletionTransactionUtility.TryCommit(state.EntityManager,
                                                                 managerEntity);
    }
    #endregion

    #endregion
}
