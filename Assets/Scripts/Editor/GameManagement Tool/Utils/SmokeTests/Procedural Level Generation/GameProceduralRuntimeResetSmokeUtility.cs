#if UNITY_EDITOR
using System;
using Unity.Entities;

/// <summary>
/// Centralizes complete procedural reset assertions shared by scene-arbitration smoke scenarios.
/// </summary>
internal static class GameProceduralRuntimeResetSmokeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Verifies all mutable procedural graph, lifecycle and command state is neutral while authored buffers remain available.
    /// </summary>
    /// <param name="entityManager">Isolated fixture entity manager.</param>
    /// <param name="managerEntity">Procedural manager expected to be neutral.</param>
    /// <param name="contextLabel">Scenario label prefixed to an actionable failure.</param>
    public static void RequireNeutralState(EntityManager entityManager,
                                           Entity managerEntity,
                                           string contextLabel)
    {
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        GameProceduralRoomTransitionContext context = entityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);
        GameProceduralRoomClearCounter clearCounter = entityManager.GetComponentData<GameProceduralRoomClearCounter>(managerEntity);
        Require(entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true).Length == 0 &&
                entityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true).Length == 0 &&
                entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity, true).Length == 0 &&
                entityManager.GetBuffer<GameProceduralLevelRunRequest>(managerEntity, true).Length == 0,
                contextLabel + " retained graph or procedural command data.");
        Require(runtimeState.Phase == GameProceduralLevelRuntimePhase.Uninitialized &&
                runtimeState.Initialized == 0 &&
                runtimeState.GraphGenerated == 0 &&
                runtimeState.CurrentLevelIndex == -1 &&
                runtimeState.CurrentNodeIndex == -1 &&
                runtimeState.PendingNodeIndex == -1,
                contextLabel + " retained procedural lifecycle state.");
        Require(context.SourceNodeIndex == -1 &&
                context.TargetNodeIndex == -1 &&
                context.Kind == GameProceduralRoomTransitionKind.None &&
                context.RelocationPending == 0 &&
                context.CommitPending == 0,
                contextLabel + " retained room transition context.");
        Require(clearCounter.TotalCleared == 0u && clearCounter.Version == 0u,
                contextLabel + " retained room-clear counters.");
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable reset-fixture failure when an invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing retained procedural state.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralRuntimeResetSmokeUtility: " + message);
    }
    #endregion

    #endregion
}
#endif
