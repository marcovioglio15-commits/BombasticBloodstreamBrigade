using System;
using System.Collections.Generic;

/// <summary>
/// Verifies completed solver output before editor or runtime consumers receive an authoritative graph.
/// </summary>
internal static class GameProceduralLevelGraphInvariantUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates rooted DAG structure, copy limits, portal ownership and universal terminal Boss reachability.
    /// </summary>
    /// <param name="input">Generation contract used to build the graph.</param>
    /// <param name="nodes">Completed logical room nodes.</param>
    /// <param name="edges">Completed directed edges.</param>
    /// <param name="diagnostic">First violated invariant when validation fails.</param>
    /// <returns>True when the graph satisfies every hard runtime invariant.</returns>
    public static bool TryValidate(GameProceduralLevelSolverInput input,
                                   IList<GameProceduralLevelGraphNode> nodes,
                                   IList<GameProceduralLevelGraphEdge> edges,
                                   out string diagnostic)
    {
        diagnostic = string.Empty;

        if (nodes.Count < input.TargetNodeCountRange.x || nodes.Count > input.TargetNodeCountRange.y)
        {
            diagnostic = "Generated node count lies outside the authored target range.";
            return false;
        }

        int startNodeId = -1;
        int bossNodeId = -1;
        int bossCount = 0;
        int maximumDepth = 0;
        int[] incomingCounts = new int[nodes.Count];
        int[] outgoingCounts = new int[nodes.Count];
        List<int>[] targets = new List<int>[nodes.Count];
        HashSet<string>[] incomingPortals = CreatePortalSets(nodes.Count);
        HashSet<string>[] outgoingPortals = CreatePortalSets(nodes.Count);
        HashSet<int>[] distinctTargets = CreateTargetSets(nodes.Count);
        Dictionary<string, int> copyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        // Validate stable node indices, role cardinality and tile copy counts.
        for (int index = 0; index < nodes.Count; index++)
        {
            GameProceduralLevelGraphNode node = nodes[index];

            if (node.NodeId != index || node.Depth < 0 || node.Depth > input.MaximumDepth)
            {
                diagnostic = "Node IDs must be contiguous and every depth must remain inside the technical limit.";
                return false;
            }

            maximumDepth = Math.Max(maximumDepth, node.Depth);
            targets[index] = new List<int>();
            copyCounts.TryGetValue(node.TileTechnicalId, out int copies);
            copyCounts[node.TileTechnicalId] = copies + 1;

            if (!ValidateExactTileDepth(input, node, out diagnostic))
                return false;

            switch (node.Role)
            {
                case GameProceduralRoomRole.Start:
                    if (startNodeId >= 0 || node.Depth != 0)
                    {
                        diagnostic = "The graph must contain exactly one Start node at depth zero.";
                        return false;
                    }

                    startNodeId = index;
                    break;

                case GameProceduralRoomRole.Boss:
                    bossCount++;
                    bossNodeId = index;
                    break;
            }
        }

        if (startNodeId != 0 || bossCount != 1)
        {
            diagnostic = "The graph requires node zero as its single Start and exactly one Boss node.";
            return false;
        }

        if (!ValidateCopyCounts(input, copyCounts, out diagnostic))
            return false;

        // Validate layered edges, exact fitting and individual portal reservation rules.
        for (int index = 0; index < edges.Count; index++)
        {
            GameProceduralLevelGraphEdge edge = edges[index];

            if (edge.EdgeId != index ||
                edge.SourceNodeId < 0 || edge.SourceNodeId >= nodes.Count ||
                edge.TargetNodeId < 0 || edge.TargetNodeId >= nodes.Count)
            {
                diagnostic = "Edge IDs and node references must remain contiguous and in range.";
                return false;
            }

            GameProceduralLevelGraphNode source = nodes[edge.SourceNodeId];
            GameProceduralLevelGraphNode target = nodes[edge.TargetNodeId];

            if (target.Depth != source.Depth + 1)
            {
                diagnostic = "Every graph edge must advance exactly one depth layer.";
                return false;
            }

            if (!distinctTargets[edge.SourceNodeId].Add(edge.TargetNodeId))
            {
                diagnostic = "Different outgoing portals on one source node must target distinct room nodes.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(edge.SourcePortalId) ||
                !outgoingPortals[edge.SourceNodeId].Add(edge.SourcePortalId))
            {
                diagnostic = "Each physical source portal may own at most one generated edge on its logical node.";
                return false;
            }

            if (input.UseCenterArrival)
            {
                if (!edge.UsesCenterArrival || !string.IsNullOrEmpty(edge.TargetPortalId))
                {
                    diagnostic = "Center-arrival edges must omit target portals and carry the center-arrival flag.";
                    return false;
                }
            }
            else
            {
                if (edge.UsesCenterArrival || string.IsNullOrWhiteSpace(edge.TargetPortalId))
                {
                    diagnostic = "Portal-arrival edges require a physical target entrance assignment.";
                    return false;
                }

                if (edge.TargetSide != GameProceduralLevelValidator.GetOppositeSide(edge.SourceSide))
                {
                    diagnostic = "Portal-arrival edges must match exact opposite room sides.";
                    return false;
                }

                if (!incomingPortals[edge.TargetNodeId].Add(edge.TargetPortalId))
                {
                    diagnostic = "A physical target entrance may receive only one generated edge on its logical node.";
                    return false;
                }
            }

            incomingCounts[edge.TargetNodeId]++;
            outgoingCounts[edge.SourceNodeId]++;
            targets[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        // Explicitly prohibit an incoming Both portal from becoming an outgoing portal on the same logical node.
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            foreach (string portalId in incomingPortals[nodeIndex])
            {
                if (!outgoingPortals[nodeIndex].Contains(portalId))
                    continue;

                diagnostic = "A portal assigned as the node entrance was reused as an exit on the same logical node.";
                return false;
            }
        }

        if (!ValidateNodeConnectivity(nodes,
                                      startNodeId,
                                      bossNodeId,
                                      maximumDepth,
                                      incomingCounts,
                                      outgoingCounts,
                                      targets,
                                      out diagnostic))
            return false;

        return true;
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies a generated node respects the hard depth constraint declared by its reusable tile.
    /// </summary>
    /// <param name="input">Input containing immutable tile placement constraints.</param>
    /// <param name="node">Generated node being inspected.</param>
    /// <param name="diagnostic">Failure text when the tile or exact depth does not match.</param>
    /// <returns>True when the node belongs to a known tile and satisfies its optional exact depth.</returns>
    private static bool ValidateExactTileDepth(GameProceduralLevelSolverInput input,
                                               GameProceduralLevelGraphNode node,
                                               out string diagnostic)
    {
        diagnostic = string.Empty;

        for (int tileIndex = 0; tileIndex < input.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileSolverInput tile = input.RoomTiles[tileIndex];

            if (!string.Equals(tile.TechnicalId, node.TileTechnicalId, StringComparison.Ordinal))
                continue;

            if (!tile.UseExactDepthConstraint || tile.ExactDepth == node.Depth)
                return true;

            diagnostic = "A generated tile occurrence violates its exact depth constraint.";
            return false;
        }

        diagnostic = "A generated node references an unknown reusable tile.";
        return false;
    }

    /// <summary>
    /// Verifies generated occurrences do not exceed their reusable tile copy budgets.
    /// </summary>
    /// <param name="input">Input containing tile budgets.</param>
    /// <param name="copyCounts">Generated occurrence counts keyed by technical tile ID.</param>
    /// <param name="diagnostic">Failure text when a budget is exceeded or unknown.</param>
    /// <returns>True when every generated tile occurrence is authorized.</returns>
    private static bool ValidateCopyCounts(GameProceduralLevelSolverInput input,
                                           Dictionary<string, int> copyCounts,
                                           out string diagnostic)
    {
        diagnostic = string.Empty;
        Dictionary<string, int> maximumCopies = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int index = 0; index < input.RoomTiles.Count; index++)
            maximumCopies[input.RoomTiles[index].TechnicalId] = input.RoomTiles[index].MaximumCopies;

        foreach (KeyValuePair<string, int> entry in copyCounts)
        {
            if (!maximumCopies.TryGetValue(entry.Key, out int maximum) || entry.Value > maximum)
            {
                diagnostic = "Generated tile occurrences exceed a configured maximum copy budget.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verifies root reachability, terminal roles and that every node has a path to the single deepest Boss.
    /// </summary>
    /// <param name="nodes">Completed graph nodes.</param>
    /// <param name="startNodeId">Root Start node ID.</param>
    /// <param name="bossNodeId">Single Boss node ID.</param>
    /// <param name="maximumDepth">Deepest generated depth.</param>
    /// <param name="incomingCounts">Incoming edge totals by node.</param>
    /// <param name="outgoingCounts">Outgoing edge totals by node.</param>
    /// <param name="targets">Outgoing target IDs by node.</param>
    /// <param name="diagnostic">First connectivity failure.</param>
    /// <returns>True when every node belongs to a path from Start to Boss.</returns>
    private static bool ValidateNodeConnectivity(IList<GameProceduralLevelGraphNode> nodes,
                                                 int startNodeId,
                                                 int bossNodeId,
                                                 int maximumDepth,
                                                 int[] incomingCounts,
                                                 int[] outgoingCounts,
                                                 List<int>[] targets,
                                                 out string diagnostic)
    {
        diagnostic = string.Empty;

        for (int index = 0; index < nodes.Count; index++)
        {
            GameProceduralLevelGraphNode node = nodes[index];

            if (index != startNodeId && incomingCounts[index] == 0)
            {
                diagnostic = "Every non-Start node must be reachable from an earlier layer.";
                return false;
            }

            if (index == bossNodeId)
            {
                if (outgoingCounts[index] > 0 || node.Depth != maximumDepth)
                {
                    diagnostic = "The Boss must be the deepest terminal node.";
                    return false;
                }

                continue;
            }

            if (outgoingCounts[index] == 0)
            {
                diagnostic = "No non-Boss room may terminate a generated branch.";
                return false;
            }
        }

        bool[] reachableFromStart = new bool[nodes.Count];
        MarkForwardReachable(startNodeId, targets, reachableFromStart);
        byte[] bossReachability = new byte[nodes.Count];

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!reachableFromStart[index])
            {
                diagnostic = "The graph contains a node that is not reachable from Start.";
                return false;
            }

            if (CanReachBoss(index, bossNodeId, targets, bossReachability))
                continue;

            diagnostic = "Every generated branch must converge into the single terminal Boss.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Marks all nodes reachable from one root using an iterative stack.
    /// </summary>
    /// <param name="startNodeId">Root node ID.</param>
    /// <param name="targets">Outgoing target IDs by node.</param>
    /// <param name="reachable">Mutable reachability flags.</param>
    private static void MarkForwardReachable(int startNodeId, List<int>[] targets, bool[] reachable)
    {
        Stack<int> pending = new Stack<int>();
        pending.Push(startNodeId);

        while (pending.Count > 0)
        {
            int nodeId = pending.Pop();

            if (reachable[nodeId])
                continue;

            reachable[nodeId] = true;

            for (int index = 0; index < targets[nodeId].Count; index++)
                pending.Push(targets[nodeId][index]);
        }
    }

    /// <summary>
    /// Resolves whether one node reaches the Boss through depth-advancing edges and memoizes the result.
    /// </summary>
    /// <param name="nodeId">Node being inspected.</param>
    /// <param name="bossNodeId">Terminal Boss node ID.</param>
    /// <param name="targets">Outgoing target IDs by node.</param>
    /// <param name="memo">Zero unknown, one cannot reach and two can reach.</param>
    /// <returns>True when at least one descendant path reaches the Boss.</returns>
    private static bool CanReachBoss(int nodeId,
                                     int bossNodeId,
                                     List<int>[] targets,
                                     byte[] memo)
    {
        if (nodeId == bossNodeId)
            return true;

        if (memo[nodeId] > 0)
            return memo[nodeId] == 2;

        for (int index = 0; index < targets[nodeId].Count; index++)
        {
            if (!CanReachBoss(targets[nodeId][index], bossNodeId, targets, memo))
                continue;

            memo[nodeId] = 2;
            return true;
        }

        memo[nodeId] = 1;
        return false;
    }
    #endregion

    #region Factory Methods
    /// <summary>
    /// Creates per-node portal-ID sets used to detect duplicate and bidirectional physical assignments.
    /// </summary>
    /// <param name="count">Number of graph nodes.</param>
    /// <returns>Initialized portal-ID set array.</returns>
    private static HashSet<string>[] CreatePortalSets(int count)
    {
        HashSet<string>[] sets = new HashSet<string>[count];

        for (int index = 0; index < count; index++)
            sets[index] = new HashSet<string>(StringComparer.Ordinal);

        return sets;
    }

    /// <summary>
    /// Creates per-source target sets used to enforce distinct targets for multiple outgoing portals.
    /// </summary>
    /// <param name="count">Number of graph nodes.</param>
    /// <returns>Initialized target-ID set array.</returns>
    private static HashSet<int>[] CreateTargetSets(int count)
    {
        HashSet<int>[] sets = new HashSet<int>[count];

        for (int index = 0; index < count; index++)
            sets[index] = new HashSet<int>();

        return sets;
    }
    #endregion

    #endregion
}
