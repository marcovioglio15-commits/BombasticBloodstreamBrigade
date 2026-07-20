using System;
using System.Collections.Generic;

/// <summary>
/// Owns one bounded weighted backtracking attempt and rolls failed branches back to immutable collection snapshots.
/// </summary>
internal sealed class GameProceduralLevelSolverContext
{
    #region Fields

    #region Readonly Fields
    private readonly GameProceduralLevelSolverInput input;
    private readonly List<GameProceduralLevelGraphNode> nodes = new List<GameProceduralLevelGraphNode>();
    private readonly List<GameProceduralLevelGraphEdge> edges = new List<GameProceduralLevelGraphEdge>();
    private readonly List<GameProceduralLevelSolverNodeState> nodeStates = new List<GameProceduralLevelSolverNodeState>();
    private readonly List<GameProceduralRoomTileSolverInput> regularTiles = new List<GameProceduralRoomTileSolverInput>();
    private readonly Dictionary<string, int> copyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly GameProceduralRoomTileSolverInput startTile;
    private readonly GameProceduralRoomTileSolverInput bossTile;
    private readonly int targetNodeCount;
    private readonly int targetBossDepth;
    private readonly int maximumSearchSteps;
    #endregion

    #region Runtime Fields
    private GameProceduralLevelSolverRandom random;
    private int searchSteps;
    private GameProceduralLevelGenerationFailureCode failureCode;
    private string diagnostic = string.Empty;
    #endregion

    #endregion

    #region Properties
    public IList<GameProceduralLevelGraphNode> Nodes
    {
        get
        {
            return nodes;
        }
    }

    public IList<GameProceduralLevelGraphEdge> Edges
    {
        get
        {
            return edges;
        }
    }

    public GameProceduralLevelGenerationFailureCode FailureCode
    {
        get
        {
            return failureCode;
        }
    }

    public string Diagnostic
    {
        get
        {
            return diagnostic;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Initializes one independent deterministic search attempt and resolves its soft node and Boss-depth targets.
    /// </summary>
    /// <param name="input">Validated pure solver input.</param>
    /// <param name="attemptSeed">Derived deterministic attempt seed.</param>
    public GameProceduralLevelSolverContext(GameProceduralLevelSolverInput input, uint attemptSeed)
    {
        this.input = input;
        random = new GameProceduralLevelSolverRandom(attemptSeed);

        // Resolve role-specific tile collections once for the complete search attempt.
        for (int index = 0; index < input.RoomTiles.Count; index++)
        {
            GameProceduralRoomTileSolverInput tile = input.RoomTiles[index];

            switch (tile.Role)
            {
                case GameProceduralRoomRole.Start:
                    startTile = tile;
                    break;

                case GameProceduralRoomRole.Regular:
                    regularTiles.Add(tile);
                    break;

                case GameProceduralRoomRole.Boss:
                    bossTile = tile;
                    break;
            }
        }

        int nodeRangeLength = input.TargetNodeCountRange.y - input.TargetNodeCountRange.x + 1;
        targetNodeCount = input.TargetNodeCountRange.x + random.NextInt(nodeRangeLength);
        targetBossDepth = GameProceduralLevelSolverSearchUtility.SelectTargetBossDepth(input, ref random);
        long searchBudget = (long)input.MaximumNodeCount * input.MaximumDepth * 32L;
        maximumSearchSteps = (int)Math.Min(1000000L, Math.Max(128L, searchBudget));
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Builds a rooted layered DAG and returns only after every branch reaches one terminal Boss.
    /// </summary>
    /// <returns>True when this bounded attempt produced a complete graph.</returns>
    public bool TryGenerate()
    {
        int startNodeId = AddNode(startTile, 0);
        List<int> frontier = new List<int>(1)
        {
            startNodeId
        };

        if (TryExpandFrontier(frontier, 0))
            return true;

        if (failureCode == GameProceduralLevelGenerationFailureCode.None)
            SetFailure(GameProceduralLevelGenerationFailureCode.AttemptLimitReached,
                       "The deterministic search attempt exhausted all weighted graph branches.");

        return false;
    }
    #endregion

    #region Graph Expansion Methods
    /// <summary>
    /// Either converges the current frontier into the Boss or recursively assigns a new Regular depth layer.
    /// </summary>
    /// <param name="frontier">Reachable nodes at one common depth.</param>
    /// <param name="depth">Current frontier depth.</param>
    /// <returns>True when a complete descendant graph reaches the terminal Boss.</returns>
    private bool TryExpandFrontier(List<int> frontier, int depth)
    {
        if (!ConsumeSearchStep())
            return false;

        int bossDepth = depth + 1;
        int finalNodeCount = nodes.Count + 1;
        bool nodeCountAccepted = finalNodeCount >= input.TargetNodeCountRange.x &&
                                 finalNodeCount <= input.TargetNodeCountRange.y;
        bool mustClose = finalNodeCount == input.TargetNodeCountRange.y || bossDepth >= input.MaximumDepth;
        bool reachedSoftTargets = finalNodeCount >= targetNodeCount && bossDepth >= targetBossDepth;

        // Prefer the authored soft targets, while technical limits can force an earlier valid convergence attempt.
        if (nodeCountAccepted && (reachedSoftTargets || mustClose))
        {
            GameProceduralLevelSolverSnapshot bossSnapshot = CaptureSnapshot();

            if (TryAttachBoss(frontier, bossDepth))
                return true;

            Rollback(bossSnapshot);

            if (mustClose)
                return false;
        }

        if (bossDepth >= input.MaximumDepth)
        {
            SetFailure(GameProceduralLevelGenerationFailureCode.DepthBudgetExceeded,
                       "The current frontier cannot reach the Boss before Maximum Depth.");
            return false;
        }

        if (nodes.Count >= input.TargetNodeCountRange.y - 1 || nodes.Count >= input.MaximumNodeCount - 1)
        {
            SetFailure(GameProceduralLevelGenerationFailureCode.NodeBudgetExceeded,
                       "No Regular node capacity remains before the reserved terminal Boss node.");
            return false;
        }

        GameProceduralLevelSolverSnapshot layerSnapshot = CaptureSnapshot();

        if (!TryCreatePendingExits(frontier, out List<GameProceduralLevelPendingExit> pendingExits))
        {
            Rollback(layerSnapshot);
            return false;
        }

        List<int> nextLayer = new List<int>();
        Dictionary<int, HashSet<int>> targetNodesBySource = new Dictionary<int, HashSet<int>>();

        for (int index = 0; index < frontier.Count; index++)
            targetNodesBySource.Add(frontier[index], new HashSet<int>());

        if (TryAssignPendingExit(pendingExits,
                                 0,
                                 nextLayer,
                                 targetNodesBySource,
                                 bossDepth))
            return true;

        Rollback(layerSnapshot);
        return false;
    }

    /// <summary>
    /// Recursively assigns every source exit to a distinct-per-source next-layer target and then expands that layer.
    /// </summary>
    /// <param name="pendingExits">Physical source exits requiring edges.</param>
    /// <param name="pendingIndex">Current pending exit index.</param>
    /// <param name="nextLayer">Unique target nodes created or reused at the next depth.</param>
    /// <param name="targetNodesBySource">Target IDs already used by each source node.</param>
    /// <param name="nextDepth">Depth assigned to every target node.</param>
    /// <returns>True when this assignment branch reaches a complete graph.</returns>
    private bool TryAssignPendingExit(List<GameProceduralLevelPendingExit> pendingExits,
                                      int pendingIndex,
                                      List<int> nextLayer,
                                      Dictionary<int, HashSet<int>> targetNodesBySource,
                                      int nextDepth)
    {
        if (!ConsumeSearchStep())
            return false;

        if (pendingIndex >= pendingExits.Count)
            return nextLayer.Count > 0 && TryExpandFrontier(nextLayer, nextDepth);

        GameProceduralLevelPendingExit pendingExit = pendingExits[pendingIndex];
        HashSet<int> sourceTargets = targetNodesBySource[pendingExit.SourceNodeId];
        List<GameProceduralLevelTargetCandidate> candidates = GameProceduralLevelSolverSearchUtility.BuildTargetCandidates(input,
                                                                                                                            nodeStates,
                                                                                                                            regularTiles,
                                                                                                                            copyCounts,
                                                                                                                            nodes.Count,
                                                                                                                            targetNodeCount,
                                                                                                                            pendingExit,
                                                                                                                            nextLayer,
                                                                                                                            sourceTargets,
                                                                                                                            nextDepth);

        // Consume candidates in deterministic weighted order and roll each failed assignment back immediately.
        while (candidates.Count > 0)
        {
            int candidateIndex = GameProceduralLevelSolverSearchUtility.SelectWeightedCandidateIndex(candidates, ref random);
            GameProceduralLevelTargetCandidate candidate = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);
            GameProceduralLevelSolverSnapshot candidateSnapshot = CaptureSnapshot();
            int targetNodeId = candidate.ExistingNodeId;
            bool createdNode = candidate.CreatesNode;

            if (createdNode)
            {
                targetNodeId = AddNode(candidate.NewTile, nextDepth);
                nextLayer.Add(targetNodeId);
            }

            if (candidate.HasTargetPortal)
                nodeStates[targetNodeId].UsedIncomingPortalIds.Add(candidate.TargetPortal.PortalId);

            sourceTargets.Add(targetNodeId);
            AddEdge(pendingExit, targetNodeId, candidate);

            if (TryAssignPendingExit(pendingExits,
                                     pendingIndex + 1,
                                     nextLayer,
                                     targetNodesBySource,
                                     nextDepth))
                return true;

            sourceTargets.Remove(targetNodeId);

            if (createdNode)
                nextLayer.RemoveAt(nextLayer.Count - 1);

            Rollback(candidateSnapshot);
        }

        SetFailure(GameProceduralLevelGenerationFailureCode.TargetEntranceUnavailable,
                   "A required source exit could not acquire a distinct compatible next-layer target.");
        return false;
    }
    #endregion

    #region Boss Methods
    /// <summary>
    /// Adds one Boss node and assigns exactly one converging edge from every frontier node.
    /// </summary>
    /// <param name="frontier">Current deepest Regular or Start nodes.</param>
    /// <param name="bossDepth">Depth assigned to the terminal Boss.</param>
    /// <returns>True when every branch can converge through unique Boss entrances when fitting is active.</returns>
    private bool TryAttachBoss(List<int> frontier, int bossDepth)
    {
        if (!CanUseTile(bossTile))
        {
            SetFailure(GameProceduralLevelGenerationFailureCode.NoBossRoomCandidate,
                       "The Boss tile copy budget is exhausted.");
            return false;
        }

        List<GameProceduralLevelPendingExit> bossExits = new List<GameProceduralLevelPendingExit>(frontier.Count);

        // A single Boss target requires exactly one active outgoing portal from each source node.
        for (int index = 0; index < frontier.Count; index++)
        {
            if (!TrySelectBossExit(frontier[index], out GameProceduralRoomPortalSolverInput sourcePortal))
                return false;

            bossExits.Add(new GameProceduralLevelPendingExit(frontier[index], sourcePortal));
        }

        int bossNodeId = AddNode(bossTile, bossDepth);

        if (TryAssignBossEntrance(bossExits, 0, bossNodeId))
            return true;

        SetFailure(GameProceduralLevelGenerationFailureCode.NoBossRoomCandidate,
                   "The Boss room lacks enough compatible unused entrances for all converging branches.");
        return false;
    }

    /// <summary>
    /// Selects the only required exit or one optional exit that can converge a source node into the Boss.
    /// </summary>
    /// <param name="sourceNodeId">Frontier source node.</param>
    /// <param name="sourcePortal">Selected physical source portal.</param>
    /// <returns>True when the source can emit exactly one Boss edge.</returns>
    private bool TrySelectBossExit(int sourceNodeId, out GameProceduralRoomPortalSolverInput sourcePortal)
    {
        GameProceduralLevelSolverNodeState sourceState = nodeStates[sourceNodeId];
        List<GameProceduralRoomPortalSolverInput> required = new List<GameProceduralRoomPortalSolverInput>();
        List<GameProceduralRoomPortalSolverInput> optional = new List<GameProceduralRoomPortalSolverInput>();
        CollectAvailableExits(sourceState, required, optional);

        if (required.Count > 1)
        {
            sourcePortal = default;
            SetFailure(GameProceduralLevelGenerationFailureCode.RequiredExitUnresolved,
                       "A frontier room owns multiple Required exits, so its edges cannot target one distinct Boss node.");
            return false;
        }

        if (required.Count == 1)
        {
            sourcePortal = required[0];
            return true;
        }

        if (optional.Count == 0)
        {
            sourcePortal = default;
            SetFailure(GameProceduralLevelGenerationFailureCode.RequiredExitUnresolved,
                       "A frontier room has no available exit that can reach the Boss.");
            return false;
        }

        sourcePortal = optional[random.NextInt(optional.Count)];
        return true;
    }

    /// <summary>
    /// Recursively assigns distinct compatible Boss entrances, or empty targets in center-arrival mode.
    /// </summary>
    /// <param name="bossExits">One converging exit for each frontier source.</param>
    /// <param name="exitIndex">Current Boss edge index.</param>
    /// <param name="bossNodeId">Terminal Boss node ID.</param>
    /// <returns>True when all Boss edges have valid arrival assignments.</returns>
    private bool TryAssignBossEntrance(List<GameProceduralLevelPendingExit> bossExits,
                                       int exitIndex,
                                       int bossNodeId)
    {
        if (exitIndex >= bossExits.Count)
            return true;

        GameProceduralLevelPendingExit pendingExit = bossExits[exitIndex];

        if (input.UseCenterArrival)
        {
            AddEdge(pendingExit,
                    bossNodeId,
                    new GameProceduralLevelTargetCandidate(bossNodeId, null, default, false, 1f));
            return TryAssignBossEntrance(bossExits, exitIndex + 1, bossNodeId);
        }

        List<GameProceduralRoomPortalSolverInput> entrances = GameProceduralLevelSolverSearchUtility.GetCompatibleEntrances(nodeStates[bossNodeId],
                                                                                                                             pendingExit.Portal.Side);
        GameProceduralLevelSolverSearchUtility.Shuffle(entrances, ref random);

        for (int index = 0; index < entrances.Count; index++)
        {
            GameProceduralLevelSolverSnapshot snapshot = CaptureSnapshot();
            GameProceduralRoomPortalSolverInput entrance = entrances[index];
            nodeStates[bossNodeId].UsedIncomingPortalIds.Add(entrance.PortalId);
            AddEdge(pendingExit,
                    bossNodeId,
                    new GameProceduralLevelTargetCandidate(bossNodeId, null, entrance, true, 1f));

            if (TryAssignBossEntrance(bossExits, exitIndex + 1, bossNodeId))
                return true;

            Rollback(snapshot);
        }

        return false;
    }
    #endregion

    #region Exit Planning Methods
    /// <summary>
    /// Selects all Required exits and a bounded deterministic subset of Optional exits for one new layer.
    /// </summary>
    /// <param name="frontier">Current source nodes.</param>
    /// <param name="pendingExits">Combined individual exits requiring target assignments.</param>
    /// <returns>True when every source can emit at least one forward edge.</returns>
    private bool TryCreatePendingExits(List<int> frontier,
                                       out List<GameProceduralLevelPendingExit> pendingExits)
    {
        pendingExits = new List<GameProceduralLevelPendingExit>();
        int remainingNodeCapacity = Math.Min(input.TargetNodeCountRange.y, input.MaximumNodeCount) - nodes.Count - 1;

        for (int sourceIndex = 0; sourceIndex < frontier.Count; sourceIndex++)
        {
            int sourceNodeId = frontier[sourceIndex];
            List<GameProceduralRoomPortalSolverInput> required = new List<GameProceduralRoomPortalSolverInput>();
            List<GameProceduralRoomPortalSolverInput> optional = new List<GameProceduralRoomPortalSolverInput>();
            CollectAvailableExits(nodeStates[sourceNodeId], required, optional);

            if (required.Count > remainingNodeCapacity)
            {
                SetFailure(GameProceduralLevelGenerationFailureCode.NodeBudgetExceeded,
                           "One room owns more Required exits than the remaining distinct-target node capacity.");
                return false;
            }

            int minimumOptionalCount = required.Count == 0 ? 1 : 0;

            if (optional.Count < minimumOptionalCount)
            {
                SetFailure(GameProceduralLevelGenerationFailureCode.RequiredExitUnresolved,
                           "Every non-Boss room must expose at least one available Required or Optional exit.");
                return false;
            }

            GameProceduralLevelSolverSearchUtility.Shuffle(optional, ref random);
            int maximumOptionalCount = Math.Min(optional.Count,
                                                Math.Max(minimumOptionalCount,
                                                         remainingNodeCapacity - required.Count));
            int optionalCount = minimumOptionalCount;

            if (maximumOptionalCount > minimumOptionalCount)
                optionalCount += random.NextInt(maximumOptionalCount - minimumOptionalCount + 1);

            for (int index = 0; index < required.Count; index++)
                pendingExits.Add(new GameProceduralLevelPendingExit(sourceNodeId, required[index]));

            for (int index = 0; index < optionalCount; index++)
                pendingExits.Add(new GameProceduralLevelPendingExit(sourceNodeId, optional[index]));
        }

        GameProceduralLevelSolverSearchUtility.Shuffle(pendingExits, ref random);
        return pendingExits.Count > 0;
    }

    /// <summary>
    /// Collects individual exit-capable portals while excluding the node's already assigned incoming entrances.
    /// </summary>
    /// <param name="nodeState">Source node working state.</param>
    /// <param name="required">Destination list for Required exits.</param>
    /// <param name="optional">Destination list for Optional exits.</param>
    private static void CollectAvailableExits(GameProceduralLevelSolverNodeState nodeState,
                                              List<GameProceduralRoomPortalSolverInput> required,
                                              List<GameProceduralRoomPortalSolverInput> optional)
    {
        for (int index = 0; index < nodeState.Tile.Portals.Count; index++)
        {
            GameProceduralRoomPortalSolverInput portal = nodeState.Tile.Portals[index];

            // An incoming Both portal is permanently reserved and can never become a source exit on this node.
            if (nodeState.UsedIncomingPortalIds.Contains(portal.PortalId))
                continue;

            if (portal.Capability == GameRoomPortalCapability.Entrance ||
                portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
                continue;

            switch (portal.ConnectionPolicy)
            {
                case GameRoomPortalConnectionPolicy.Required:
                    required.Add(portal);
                    break;

                case GameRoomPortalConnectionPolicy.Optional:
                    optional.Add(portal);
                    break;
            }
        }
    }
    #endregion

    #region Mutation Methods
    /// <summary>
    /// Appends one logical node and increments its reusable tile occurrence count.
    /// </summary>
    /// <param name="tile">Reusable tile selected for the node.</param>
    /// <param name="depth">Node graph depth.</param>
    /// <returns>Stable zero-based node ID.</returns>
    private int AddNode(GameProceduralRoomTileSolverInput tile, int depth)
    {
        copyCounts.TryGetValue(tile.TechnicalId, out int currentCopies);
        int nodeId = nodes.Count;
        GameProceduralLevelGraphNode graphNode = new GameProceduralLevelGraphNode(nodeId,
                                                                                 tile.TechnicalId,
                                                                                 tile.TileId,
                                                                                 tile.SceneId,
                                                                                 tile.Role,
                                                                                 depth,
                                                                                 currentCopies + 1);
        nodes.Add(graphNode);
        nodeStates.Add(new GameProceduralLevelSolverNodeState(tile, graphNode));
        copyCounts[tile.TechnicalId] = currentCopies + 1;
        return nodeId;
    }

    /// <summary>
    /// Appends one edge with exact physical assignments or an empty center-arrival target portal.
    /// </summary>
    /// <param name="pendingExit">Source node and exit assignment.</param>
    /// <param name="targetNodeId">Target node ID.</param>
    /// <param name="candidate">Target node or tile and entrance assignment.</param>
    private void AddEdge(GameProceduralLevelPendingExit pendingExit,
                         int targetNodeId,
                         GameProceduralLevelTargetCandidate candidate)
    {
        GameRoomPortalSide targetSide = candidate.HasTargetPortal
            ? candidate.TargetPortal.Side
            : default;
        edges.Add(new GameProceduralLevelGraphEdge(edges.Count,
                                                   pendingExit.SourceNodeId,
                                                   targetNodeId,
                                                   pendingExit.Portal.PortalId,
                                                   candidate.HasTargetPortal ? candidate.TargetPortal.PortalId : string.Empty,
                                                   pendingExit.Portal.Side,
                                                   targetSide,
                                                   input.UseCenterArrival));
    }

    /// <summary>
    /// Rolls edges and nodes back to a captured branch snapshot, including entrance reservations and copy counts.
    /// </summary>
    /// <param name="snapshot">Collection lengths captured before the failed branch.</param>
    private void Rollback(GameProceduralLevelSolverSnapshot snapshot)
    {
        // Remove edges first so target nodes still exist while their reserved entrances are released.
        while (edges.Count > snapshot.EdgeCount)
        {
            GameProceduralLevelGraphEdge edge = edges[edges.Count - 1];

            if (!edge.UsesCenterArrival && edge.TargetNodeId < nodeStates.Count)
                nodeStates[edge.TargetNodeId].UsedIncomingPortalIds.Remove(edge.TargetPortalId);

            edges.RemoveAt(edges.Count - 1);
        }

        while (nodes.Count > snapshot.NodeCount)
        {
            GameProceduralLevelSolverNodeState nodeState = nodeStates[nodeStates.Count - 1];
            string technicalId = nodeState.Tile.TechnicalId;
            int updatedCount = copyCounts[technicalId] - 1;

            if (updatedCount > 0)
                copyCounts[technicalId] = updatedCount;
            else
                copyCounts.Remove(technicalId);

            nodeStates.RemoveAt(nodeStates.Count - 1);
            nodes.RemoveAt(nodes.Count - 1);
        }
    }

    /// <summary>
    /// Captures current graph collection lengths for efficient branch rollback.
    /// </summary>
    /// <returns>Immutable node and edge length snapshot.</returns>
    private GameProceduralLevelSolverSnapshot CaptureSnapshot()
    {
        return new GameProceduralLevelSolverSnapshot(nodes.Count, edges.Count);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Checks whether another logical node may use one tile without exceeding its maximum copies.
    /// </summary>
    /// <param name="tile">Reusable tile to inspect.</param>
    /// <returns>True when copy capacity remains.</returns>
    private bool CanUseTile(GameProceduralRoomTileSolverInput tile)
    {
        return !copyCounts.TryGetValue(tile.TechnicalId, out int count) || count < tile.MaximumCopies;
    }

    /// <summary>
    /// Consumes one bounded recursive decision step and records an explicit budget failure when exhausted.
    /// </summary>
    /// <returns>True while the attempt remains inside its hard search budget.</returns>
    private bool ConsumeSearchStep()
    {
        searchSteps++;

        if (searchSteps <= maximumSearchSteps)
            return true;

        SetFailure(GameProceduralLevelGenerationFailureCode.SearchBudgetExceeded,
                   "The weighted backtracking tree exceeded its deterministic technical search-step budget.");
        return false;
    }

    /// <summary>
    /// Stores the latest actionable failure encountered by the bounded search tree.
    /// </summary>
    /// <param name="code">Stable failure category.</param>
    /// <param name="message">Actionable failure description.</param>
    private void SetFailure(GameProceduralLevelGenerationFailureCode code, string message)
    {
        failureCode = code;
        diagnostic = message;
    }
    #endregion

    #endregion
}
