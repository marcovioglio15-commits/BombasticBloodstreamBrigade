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
            .WithAll<GameProceduralLevelDefinitionElement, GameRoomCombatCompletionState>()
            .Build(ref state);
    }

    /// <summary>
    /// Marks the active room complete once, unlocks regular exits and exposes terminal Boss completion to run outcome.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (managerQuery.CalculateEntityCount() != 1)
            return;

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameProceduralLevelRuntimeState runtimeState = state.EntityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (runtimeState.Initialized == 0 ||
            runtimeState.GraphGenerated == 0 ||
            runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentRoomCleared != 0)
        {
            return;
        }

        DynamicBuffer<GameProceduralRoomNodeElement> nodes = state.EntityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity);

        if (runtimeState.CurrentNodeIndex < 0 || runtimeState.CurrentNodeIndex >= nodes.Length)
            return;

        GameRoomCombatCompletionState completionState = state.EntityManager.GetComponentData<GameRoomCombatCompletionState>(managerEntity);

        if (completionState.IsComplete == 0)
            return;

        CompleteCurrentRoom(state.EntityManager, managerEntity, ref runtimeState, nodes);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes the one-shot node and global clear state, then advances Boss rooms to the appropriate terminal phase.
    /// </summary>
    /// <param name="entityManager">Entity manager owning procedural runtime data.</param>
    /// <param name="managerEntity">Unique procedural manager entity.</param>
    /// <param name="runtimeState">Mutable active run state.</param>
    /// <param name="nodes">Generated node buffer containing the current logical room.</param>
    private static void CompleteCurrentRoom(EntityManager entityManager,
                                            Entity managerEntity,
                                            ref GameProceduralLevelRuntimeState runtimeState,
                                            DynamicBuffer<GameProceduralRoomNodeElement> nodes)
    {
        GameProceduralRoomNodeElement currentNode = nodes[runtimeState.CurrentNodeIndex];
        currentNode.Cleared = 1;
        nodes[runtimeState.CurrentNodeIndex] = currentNode;
        runtimeState.CurrentRoomCleared = 1;

        GameProceduralRoomClearCounter counter = entityManager.GetComponentData<GameProceduralRoomClearCounter>(managerEntity);
        counter.TotalCleared++;
        counter.Version++;

        if (currentNode.Role == GameProceduralRoomRole.Boss)
        {
            DynamicBuffer<GameProceduralLevelDefinitionElement> levels = entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true);
            runtimeState.Phase = HasNextEnabledLevel(levels, runtimeState.CurrentLevelIndex)
                ? GameProceduralLevelRuntimePhase.LevelComplete
                : GameProceduralLevelRuntimePhase.RunComplete;
        }

        entityManager.SetComponentData(managerEntity, counter);
        entityManager.SetComponentData(managerEntity, runtimeState);
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager, managerEntity);
    }

    /// <summary>
    /// Finds whether ordered progression contains another enabled level after the current one.
    /// </summary>
    /// <param name="levels">Ordered flattened procedural level definitions.</param>
    /// <param name="currentLevelIndex">Current level buffer index.</param>
    /// <returns>True when a later enabled level exists.</returns>
    private static bool HasNextEnabledLevel(DynamicBuffer<GameProceduralLevelDefinitionElement> levels, int currentLevelIndex)
    {
        for (int levelIndex = currentLevelIndex + 1; levelIndex < levels.Length; levelIndex++)
        {
            if (levels[levelIndex].Enabled != 0)
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
