using Unity.Entities;

/// <summary>
/// Commits one authoritative procedural room-clear transaction for runtime systems and deterministic smoke coverage.
/// </summary>
public static class GameProceduralRoomCompletionTransactionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Marks the active node complete, emits its one-shot clear event and synchronizes portal traversal availability.
    /// The caller remains responsible for deciding whether the room completion predicate has been satisfied.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the procedural runtime singleton and graph buffers.</param>
    /// <param name="managerEntity">Unique procedural manager receiving the committed transaction.</param>
    /// <returns>True when an active uncleared node was committed; false when current runtime state was ineligible.</returns>
    public static bool TryCommit(EntityManager entityManager, Entity managerEntity)
    {
        if (!entityManager.Exists(managerEntity) ||
            !entityManager.HasComponent<GameProceduralLevelRuntimeState>(managerEntity) ||
            !entityManager.HasBuffer<GameProceduralRoomNodeElement>(managerEntity) ||
            !entityManager.HasComponent<GameProceduralRoomClearCounter>(managerEntity) ||
            !entityManager.HasBuffer<GameProceduralRoomClearedEvent>(managerEntity))
        {
            return false;
        }

        GameProceduralLevelRuntimeState runtimeState =
            entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        DynamicBuffer<GameProceduralRoomNodeElement> nodes =
            entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity);

        if (runtimeState.Initialized == 0 ||
            runtimeState.GraphGenerated == 0 ||
            runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentRoomCleared != 0 ||
            runtimeState.CurrentNodeIndex < 0 ||
            runtimeState.CurrentNodeIndex >= nodes.Length)
        {
            return false;
        }

        // Commit node and aggregate progress before publishing the clear event consumed by reward systems.
        GameProceduralRoomNodeElement currentNode = nodes[runtimeState.CurrentNodeIndex];
        currentNode.Cleared = 1;
        nodes[runtimeState.CurrentNodeIndex] = currentNode;
        runtimeState.CurrentRoomCleared = 1;
        GameProceduralRoomClearCounter counter =
            entityManager.GetComponentData<GameProceduralRoomClearCounter>(managerEntity);
        counter.TotalCleared++;
        counter.Version++;

        if (currentNode.Role == GameProceduralRoomRole.Boss)
        {
            DynamicBuffer<GameProceduralLevelDefinitionElement> levels =
                entityManager.GetBuffer<GameProceduralLevelDefinitionElement>(managerEntity, true);
            runtimeState.Phase = HasNextEnabledLevel(levels, runtimeState.CurrentLevelIndex)
                ? GameProceduralLevelRuntimePhase.LevelComplete
                : GameProceduralLevelRuntimePhase.RunComplete;
        }

        entityManager.SetComponentData(managerEntity, counter);
        entityManager.SetComponentData(managerEntity, runtimeState);
        DynamicBuffer<GameProceduralRoomClearedEvent> clearedEvents =
            entityManager.GetBuffer<GameProceduralRoomClearedEvent>(managerEntity);
        clearedEvents.Clear();
        clearedEvents.Add(new GameProceduralRoomClearedEvent
        {
            RunSeed = runtimeState.RunSeed,
            GenerationVersion = runtimeState.GenerationVersion,
            ClearVersion = counter.Version,
            LevelIndex = runtimeState.CurrentLevelIndex,
            NodeIndex = currentNode.NodeIndex,
            TileIndex = currentNode.TileIndex
        });
        GameProceduralRoomPortalBlockingUtility.SynchronizeTraversalAvailability(entityManager,
                                                                                 managerEntity);
        return true;
    }
    #endregion

    #region Progression
    /// <summary>
    /// Finds whether ordered progression contains another enabled level after the current one.
    /// </summary>
    /// <param name="levels">Ordered flattened procedural level definitions.</param>
    /// <param name="currentLevelIndex">Current level buffer index.</param>
    /// <returns>True when a later enabled level exists.</returns>
    private static bool HasNextEnabledLevel(
        DynamicBuffer<GameProceduralLevelDefinitionElement> levels,
        int currentLevelIndex)
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
