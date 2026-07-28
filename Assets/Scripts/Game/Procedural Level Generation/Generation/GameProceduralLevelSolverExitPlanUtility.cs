using System;
using System.Collections.Generic;

/// <summary>
/// Enumerates deterministic Required and Optional source-exit plans for solver layer backtracking.
/// </summary>
internal static class GameProceduralLevelSolverExitPlanUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Visits every legal exit subset until one plan can be assigned by the owning solver context.
    /// </summary>
    /// <param name="frontier">Current source-node IDs.</param>
    /// <param name="nodeStates">Mutable solver states used to resolve available physical exits.</param>
    /// <param name="remainingNodeCapacity">Maximum distinct next-layer targets available to one source.</param>
    /// <param name="random">Deterministic stream used only to prioritize equivalent plans.</param>
    /// <param name="consumeSearchStep">Bounded-search callback invoked by every recursive decision.</param>
    /// <param name="reportFailure">Callback receiving actionable structural planning failures.</param>
    /// <param name="tryAssignPlan">Callback that assigns one complete physical exit plan.</param>
    /// <returns>True when one enumerated exit plan reaches a complete generated graph.</returns>
    public static bool TryPlan(List<int> frontier,
                               IList<GameProceduralLevelSolverNodeState> nodeStates,
                               int remainingNodeCapacity,
                               ref GameProceduralLevelSolverRandom random,
                               Func<bool> consumeSearchStep,
                               Action<GameProceduralLevelGenerationFailureCode, string> reportFailure,
                               Func<List<GameProceduralLevelPendingExit>, bool> tryAssignPlan)
    {
        List<GameProceduralLevelPendingExit> pendingExits =
            new List<GameProceduralLevelPendingExit>();
        return TryPlanSourceExits(frontier,
                                  nodeStates,
                                  0,
                                  remainingNodeCapacity,
                                  pendingExits,
                                  ref random,
                                  consumeSearchStep,
                                  reportFailure,
                                  tryAssignPlan);
    }
    #endregion

    #region Planning Methods
    /// <summary>
    /// Recursively selects an exit subset for each source so Optional policy participates in backtracking.
    /// </summary>
    /// <param name="frontier">Current source-node IDs.</param>
    /// <param name="nodeStates">Mutable solver states used to resolve available physical exits.</param>
    /// <param name="sourceIndex">Frontier source currently being planned.</param>
    /// <param name="remainingNodeCapacity">Maximum distinct next-layer targets available to one source.</param>
    /// <param name="pendingExits">Working combined source-exit plan.</param>
    /// <param name="random">Deterministic stream used to prioritize equivalent plans.</param>
    /// <param name="consumeSearchStep">Bounded-search callback invoked by every recursive decision.</param>
    /// <param name="reportFailure">Callback receiving actionable structural planning failures.</param>
    /// <param name="tryAssignPlan">Callback that assigns one complete physical exit plan.</param>
    /// <returns>True when one complete source combination can be assigned by the solver.</returns>
    private static bool TryPlanSourceExits(
        List<int> frontier,
        IList<GameProceduralLevelSolverNodeState> nodeStates,
        int sourceIndex,
        int remainingNodeCapacity,
        List<GameProceduralLevelPendingExit> pendingExits,
        ref GameProceduralLevelSolverRandom random,
        Func<bool> consumeSearchStep,
        Action<GameProceduralLevelGenerationFailureCode, string> reportFailure,
        Func<List<GameProceduralLevelPendingExit>, bool> tryAssignPlan)
    {
        if (!consumeSearchStep())
            return false;

        if (sourceIndex >= frontier.Count)
        {
            List<GameProceduralLevelPendingExit> orderedExits =
                new List<GameProceduralLevelPendingExit>(pendingExits);
            GameProceduralLevelSolverSearchUtility.Shuffle(orderedExits, ref random);
            return orderedExits.Count > 0 && tryAssignPlan(orderedExits);
        }

        int sourceNodeId = frontier[sourceIndex];
        List<GameProceduralRoomPortalSolverInput> required =
            new List<GameProceduralRoomPortalSolverInput>();
        List<GameProceduralRoomPortalSolverInput> optional =
            new List<GameProceduralRoomPortalSolverInput>();
        GameProceduralLevelSolverSearchUtility.CollectAvailableExits(nodeStates[sourceNodeId],
                                                                    required,
                                                                    optional);

        if (required.Count > remainingNodeCapacity)
        {
            reportFailure(GameProceduralLevelGenerationFailureCode.NodeBudgetExceeded,
                          "One room owns more Required exits than the remaining distinct-target node capacity.");
            return false;
        }

        int minimumOptionalCount = required.Count == 0 ? 1 : 0;

        if (optional.Count < minimumOptionalCount)
        {
            reportFailure(GameProceduralLevelGenerationFailureCode.RequiredExitUnresolved,
                          "Every non-Boss room must connect at least one available Required or Optional exit.");
            return false;
        }

        int maximumOptionalCount = Math.Min(optional.Count,
                                            remainingNodeCapacity - required.Count);

        if (maximumOptionalCount < minimumOptionalCount)
        {
            reportFailure(GameProceduralLevelGenerationFailureCode.NodeBudgetExceeded,
                          "No distinct-target node capacity remains for the room's mandatory connected Optional exit.");
            return false;
        }

        int initialCount = pendingExits.Count;

        for (int index = 0; index < required.Count; index++)
            pendingExits.Add(new GameProceduralLevelPendingExit(sourceNodeId, required[index]));

        GameProceduralLevelSolverSearchUtility.Shuffle(optional, ref random);
        int countRange = maximumOptionalCount - minimumOptionalCount + 1;

        // Prefer the smallest safe branch factor, then expand only when downstream node or depth constraints require it.
        for (int offset = 0; offset < countRange; offset++)
        {
            int optionalCount = minimumOptionalCount + offset;

            if (TryPlanOptionalCombination(frontier,
                                           nodeStates,
                                           sourceIndex,
                                           sourceNodeId,
                                           optional,
                                           0,
                                           optionalCount,
                                           remainingNodeCapacity,
                                           pendingExits,
                                           ref random,
                                           consumeSearchStep,
                                           reportFailure,
                                           tryAssignPlan))
                return true;

            pendingExits.RemoveRange(initialCount + required.Count,
                                     pendingExits.Count - initialCount - required.Count);
        }

        pendingExits.RemoveRange(initialCount, pendingExits.Count - initialCount);
        return false;
    }

    /// <summary>
    /// Enumerates combinations of one source's Optional portals without duplicating permutations.
    /// </summary>
    /// <param name="frontier">Current source-node IDs.</param>
    /// <param name="nodeStates">Mutable solver states used by subsequent source planning.</param>
    /// <param name="sourceIndex">Frontier source currently being planned.</param>
    /// <param name="sourceNodeId">Stable node ID owning the Optional portals.</param>
    /// <param name="optional">Shuffled Optional portal candidates.</param>
    /// <param name="candidateIndex">First candidate index still available.</param>
    /// <param name="remainingChoices">Number of Optional portals still required by this combination.</param>
    /// <param name="remainingNodeCapacity">Maximum distinct next-layer targets available to one source.</param>
    /// <param name="pendingExits">Working combined source-exit plan.</param>
    /// <param name="random">Deterministic stream used by subsequent source planning.</param>
    /// <param name="consumeSearchStep">Bounded-search callback invoked by every recursive decision.</param>
    /// <param name="reportFailure">Callback receiving actionable structural planning failures.</param>
    /// <param name="tryAssignPlan">Callback that assigns one complete physical exit plan.</param>
    /// <returns>True when one Optional combination can complete the source plan.</returns>
    private static bool TryPlanOptionalCombination(
        List<int> frontier,
        IList<GameProceduralLevelSolverNodeState> nodeStates,
        int sourceIndex,
        int sourceNodeId,
        List<GameProceduralRoomPortalSolverInput> optional,
        int candidateIndex,
        int remainingChoices,
        int remainingNodeCapacity,
        List<GameProceduralLevelPendingExit> pendingExits,
        ref GameProceduralLevelSolverRandom random,
        Func<bool> consumeSearchStep,
        Action<GameProceduralLevelGenerationFailureCode, string> reportFailure,
        Func<List<GameProceduralLevelPendingExit>, bool> tryAssignPlan)
    {
        if (!consumeSearchStep())
            return false;

        if (remainingChoices == 0)
            return TryPlanSourceExits(frontier,
                                      nodeStates,
                                      sourceIndex + 1,
                                      remainingNodeCapacity,
                                      pendingExits,
                                      ref random,
                                      consumeSearchStep,
                                      reportFailure,
                                      tryAssignPlan);

        int finalCandidateIndex = optional.Count - remainingChoices;

        for (int index = candidateIndex; index <= finalCandidateIndex; index++)
        {
            pendingExits.Add(new GameProceduralLevelPendingExit(sourceNodeId, optional[index]));

            if (TryPlanOptionalCombination(frontier,
                                           nodeStates,
                                           sourceIndex,
                                           sourceNodeId,
                                           optional,
                                           index + 1,
                                           remainingChoices - 1,
                                           remainingNodeCapacity,
                                           pendingExits,
                                           ref random,
                                           consumeSearchStep,
                                           reportFailure,
                                           tryAssignPlan))
                return true;

            pendingExits.RemoveAt(pendingExits.Count - 1);
        }

        return false;
    }
    #endregion

    #endregion
}
