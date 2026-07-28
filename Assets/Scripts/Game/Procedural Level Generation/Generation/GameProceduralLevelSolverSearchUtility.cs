using System;
using System.Collections.Generic;

/// <summary>
/// Builds and scores solver candidates while keeping the recursive context focused on graph state and rollback.
/// </summary>
internal static class GameProceduralLevelSolverSearchUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds weighted merge and new-room candidates compatible with one pending physical source exit.
    /// </summary>
    /// <param name="input">Immutable solver request.</param>
    /// <param name="nodeStates">Current mutable node assignments.</param>
    /// <param name="regularTiles">Reusable Regular tile candidates.</param>
    /// <param name="copyCounts">Current tile occurrence counts.</param>
    /// <param name="nodeCount">Current total node count.</param>
    /// <param name="targetNodeCount">Selected soft node target.</param>
    /// <param name="pendingExit">Source exit requiring a target.</param>
    /// <param name="nextLayer">Nodes already created at the target depth.</param>
    /// <param name="sourceTargets">Targets already used by this source node.</param>
    /// <param name="nextDepth">Target graph depth.</param>
    /// <returns>All valid existing and new-node assignments before weighted ordering.</returns>
    public static List<GameProceduralLevelTargetCandidate> BuildTargetCandidates(GameProceduralLevelSolverInput input,
                                                                                 IList<GameProceduralLevelSolverNodeState> nodeStates,
                                                                                 IList<GameProceduralRoomTileSolverInput> regularTiles,
                                                                                 Dictionary<string, int> copyCounts,
                                                                                 int nodeCount,
                                                                                 int targetNodeCount,
                                                                                 GameProceduralLevelPendingExit pendingExit,
                                                                                 List<int> nextLayer,
                                                                                 HashSet<int> sourceTargets,
                                                                                 int nextDepth)
    {
        List<GameProceduralLevelTargetCandidate> candidates = new List<GameProceduralLevelTargetCandidate>();

        // Existing next-layer nodes allow branches from different sources to merge through unused entrances.
        for (int index = 0; index < nextLayer.Count; index++)
        {
            int targetNodeId = nextLayer[index];

            if (sourceTargets.Contains(targetNodeId))
                continue;

            GameProceduralLevelSolverNodeState targetState = nodeStates[targetNodeId];
            float weight = CalculateTileWeight(input, targetState.Tile, nextDepth, string.Empty) *
                           CalculateMergePressure(nodeCount, targetNodeCount);

            if (input.UseCenterArrival)
                candidates.Add(new GameProceduralLevelTargetCandidate(targetNodeId, null, default, false, weight));
            else
                AddPortalTargetCandidates(candidates,
                                          targetNodeId,
                                          targetState,
                                          pendingExit.Portal.Side,
                                          weight);
        }

        if (!CanAddRegularNode(input, nodeCount))
            return candidates;

        // New nodes expand graph breadth while respecting per-tile copy budgets.
        for (int index = 0; index < regularTiles.Count; index++)
        {
            GameProceduralRoomTileSolverInput tile = regularTiles[index];

            if (!CanUseTileAtDepth(tile, copyCounts, nextDepth))
                continue;

            if (input.UseCenterArrival)
            {
                float weight = CalculateTileWeight(input, tile, nextDepth, string.Empty);
                candidates.Add(new GameProceduralLevelTargetCandidate(-1, tile, default, false, weight));
            }
            else
                AddNewTilePortalCandidates(input,
                                           candidates,
                                           tile,
                                           pendingExit.Portal.Side,
                                           nextDepth);
        }

        return candidates;
    }

    /// <summary>
    /// Collects exit-capable portals while excluding already reserved incoming entrances and level exits.
    /// </summary>
    /// <param name="nodeState">Source node working state.</param>
    /// <param name="required">Destination list receiving every available Required exit.</param>
    /// <param name="optional">Destination list receiving every available Optional exit.</param>
    public static void CollectAvailableExits(GameProceduralLevelSolverNodeState nodeState,
                                             List<GameProceduralRoomPortalSolverInput> required,
                                             List<GameProceduralRoomPortalSolverInput> optional)
    {
        for (int index = 0; index < nodeState.Tile.Portals.Count; index++)
        {
            GameProceduralRoomPortalSolverInput portal = nodeState.Tile.Portals[index];

            // An incoming Both portal remains reserved and cannot become an outgoing edge on this node.
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

    /// <summary>
    /// Returns every compatible unused target entrance on an existing logical node.
    /// </summary>
    /// <param name="targetState">Target node working state.</param>
    /// <param name="sourceSide">Source exit side.</param>
    /// <returns>Compatible individual entrances with no ordinal same-side pairing.</returns>
    public static List<GameProceduralRoomPortalSolverInput> GetCompatibleEntrances(GameProceduralLevelSolverNodeState targetState,
                                                                                   GameRoomPortalSide sourceSide)
    {
        List<GameProceduralRoomPortalSolverInput> entrances = new List<GameProceduralRoomPortalSolverInput>();
        GameRoomPortalSide requiredSide = GameProceduralLevelValidator.GetOppositeSide(sourceSide);

        for (int index = 0; index < targetState.Tile.Portals.Count; index++)
        {
            GameProceduralRoomPortalSolverInput portal = targetState.Tile.Portals[index];

            if (targetState.UsedIncomingPortalIds.Contains(portal.PortalId) || !CanReceive(portal, requiredSide))
                continue;

            entrances.Add(portal);
        }

        return entrances;
    }

    /// <summary>
    /// Selects a soft Boss depth using the authored range score across all technically valid depths.
    /// </summary>
    /// <param name="input">Immutable solver request.</param>
    /// <param name="random">Mutable deterministic attempt stream.</param>
    /// <returns>Deterministically weighted target Boss depth.</returns>
    public static int SelectTargetBossDepth(GameProceduralLevelSolverInput input,
                                            ref GameProceduralLevelSolverRandom random)
    {
        for (int tileIndex = 0; tileIndex < input.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileSolverInput tile = input.RoomTiles[tileIndex];

            if (tile.Role == GameProceduralRoomRole.Boss && tile.UseExactDepthConstraint)
                return tile.ExactDepth;
        }

        List<float> weights = new List<float>(input.MaximumDepth);

        for (int depth = 1; depth <= input.MaximumDepth; depth++)
        {
            float fit = CalculateRangeFit(depth,
                                          input.PreferredBossDepthRange.x,
                                          input.PreferredBossDepthRange.y);
            weights.Add(1f + input.BossDepthScore * fit);
        }

        return SelectWeightedIndex(weights, ref random) + 1;
    }

    /// <summary>
    /// Selects one weighted target candidate index using the deterministic attempt stream.
    /// </summary>
    /// <param name="candidates">Positive weighted target candidates.</param>
    /// <param name="random">Mutable deterministic attempt stream.</param>
    /// <returns>Selected candidate index.</returns>
    public static int SelectWeightedCandidateIndex(List<GameProceduralLevelTargetCandidate> candidates,
                                                   ref GameProceduralLevelSolverRandom random)
    {
        double total = 0d;

        for (int index = 0; index < candidates.Count; index++)
            total += candidates[index].Weight;

        double threshold = random.NextFloat() * total;

        for (int index = 0; index < candidates.Count; index++)
        {
            threshold -= candidates[index].Weight;

            if (threshold <= 0d)
                return index;
        }

        return candidates.Count - 1;
    }

    /// <summary>
    /// Applies an in-place deterministic Fisher-Yates shuffle to one working list.
    /// </summary>
    /// <typeparam name="T">List element type.</typeparam>
    /// <param name="items">Mutable list to shuffle.</param>
    /// <param name="random">Mutable deterministic attempt stream.</param>
    public static void Shuffle<T>(List<T> items, ref GameProceduralLevelSolverRandom random)
    {
        for (int index = items.Count - 1; index > 0; index--)
        {
            int swapIndex = random.NextInt(index + 1);
            T item = items[index];
            items[index] = items[swapIndex];
            items[swapIndex] = item;
        }
    }
    #endregion

    #region Candidate Methods
    /// <summary>
    /// Adds compatible unused entrances for an existing next-layer node.
    /// </summary>
    /// <param name="candidates">Destination candidate list.</param>
    /// <param name="targetNodeId">Existing target node ID.</param>
    /// <param name="targetState">Existing target working state.</param>
    /// <param name="sourceSide">Source exit side.</param>
    /// <param name="weight">Base weighted score.</param>
    private static void AddPortalTargetCandidates(List<GameProceduralLevelTargetCandidate> candidates,
                                                  int targetNodeId,
                                                  GameProceduralLevelSolverNodeState targetState,
                                                  GameRoomPortalSide sourceSide,
                                                  float weight)
    {
        List<GameProceduralRoomPortalSolverInput> entrances = GetCompatibleEntrances(targetState, sourceSide);

        for (int index = 0; index < entrances.Count; index++)
            candidates.Add(new GameProceduralLevelTargetCandidate(targetNodeId,
                                                                  null,
                                                                  entrances[index],
                                                                  true,
                                                                  weight));
    }

    /// <summary>
    /// Adds one new-node candidate for every compatible entrance on a reusable Regular tile.
    /// </summary>
    /// <param name="input">Immutable solver request.</param>
    /// <param name="candidates">Destination candidate list.</param>
    /// <param name="tile">Reusable tile that would create a node.</param>
    /// <param name="sourceSide">Source exit side.</param>
    /// <param name="depth">Target node depth used by scoring.</param>
    private static void AddNewTilePortalCandidates(GameProceduralLevelSolverInput input,
                                                   List<GameProceduralLevelTargetCandidate> candidates,
                                                   GameProceduralRoomTileSolverInput tile,
                                                   GameRoomPortalSide sourceSide,
                                                   int depth)
    {
        GameRoomPortalSide requiredSide = GameProceduralLevelValidator.GetOppositeSide(sourceSide);

        for (int index = 0; index < tile.Portals.Count; index++)
        {
            GameProceduralRoomPortalSolverInput portal = tile.Portals[index];

            if (!CanReceive(portal, requiredSide))
                continue;

            float weight = CalculateTileWeight(input, tile, depth, portal.PortalId);
            candidates.Add(new GameProceduralLevelTargetCandidate(-1, tile, portal, true, weight));
        }
    }

    /// <summary>
    /// Checks whether one portal can receive an edge on the exact opposite source side.
    /// </summary>
    /// <param name="portal">Candidate target portal.</param>
    /// <param name="requiredSide">Required opposite target side.</param>
    /// <returns>True when side, capability and policy permit an incoming assignment.</returns>
    private static bool CanReceive(GameProceduralRoomPortalSolverInput portal, GameRoomPortalSide requiredSide)
    {
        if (portal.Side != requiredSide || portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
            return false;

        return portal.Capability == GameRoomPortalCapability.Entrance ||
               portal.Capability == GameRoomPortalCapability.Both;
    }
    #endregion

    #region Scoring Methods
    /// <summary>
    /// Calculates the approved multiplicative base, depth and fitting score for one valid tile candidate.
    /// </summary>
    /// <param name="input">Immutable solver request.</param>
    /// <param name="tile">Candidate tile.</param>
    /// <param name="depth">Candidate graph depth.</param>
    /// <param name="incomingPortalId">Entrance reserved by the candidate, if any.</param>
    /// <returns>Finite positive candidate weight.</returns>
    private static float CalculateTileWeight(GameProceduralLevelSolverInput input,
                                             GameProceduralRoomTileSolverInput tile,
                                             int depth,
                                             string incomingPortalId)
    {
        float depthFit = CalculateRangeFit(depth, tile.PreferredDepthRange.x, tile.PreferredDepthRange.y);
        double multiplier = 1d + input.RoomDepthScore * depthFit;

        if (!input.UseCenterArrival)
            multiplier += input.FittingScore * CalculateFittingFit(tile, incomingPortalId);

        double weight = tile.BaseSelectionWeight * multiplier;

        if (double.IsNaN(weight) || weight <= 0d)
            return 0.0001f;

        if (double.IsInfinity(weight) || weight > float.MaxValue)
            return float.MaxValue;

        return (float)weight;
    }

    /// <summary>
    /// Scores future frontier quality from remaining exits after reserving an entrance.
    /// </summary>
    /// <param name="tile">Candidate target tile.</param>
    /// <param name="incomingPortalId">Portal removed from outgoing consideration.</param>
    /// <returns>Normalized fitting quality from zero to one.</returns>
    private static float CalculateFittingFit(GameProceduralRoomTileSolverInput tile, string incomingPortalId)
    {
        int usableExits = 0;

        for (int index = 0; index < tile.Portals.Count; index++)
        {
            GameProceduralRoomPortalSolverInput portal = tile.Portals[index];

            if (string.Equals(portal.PortalId, incomingPortalId, StringComparison.Ordinal) ||
                portal.Capability == GameRoomPortalCapability.Entrance ||
                portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
                continue;

            usableExits++;
        }

        return Math.Min(1f, usableExits * 0.5f);
    }

    /// <summary>
    /// Calculates a normalized score equal to one inside a range and decaying with outside distance.
    /// </summary>
    /// <param name="value">Value being scored.</param>
    /// <param name="minimum">Inclusive preferred minimum.</param>
    /// <param name="maximum">Inclusive preferred maximum.</param>
    /// <returns>Normalized positive range fit.</returns>
    private static float CalculateRangeFit(int value, int minimum, int maximum)
    {
        if (value >= minimum && value <= maximum)
            return 1f;

        int distance = value < minimum ? minimum - value : value - maximum;
        return 1f / (1f + distance);
    }

    /// <summary>
    /// Increases merge preference as the graph approaches its selected soft node target.
    /// </summary>
    /// <param name="nodeCount">Current total node count.</param>
    /// <param name="targetNodeCount">Selected soft node target.</param>
    /// <returns>Positive merge pressure multiplier.</returns>
    private static float CalculateMergePressure(int nodeCount, int targetNodeCount)
    {
        return nodeCount < targetNodeCount ? 0.65f : 1.75f;
    }

    /// <summary>
    /// Selects one index from a positive weight list using the deterministic attempt stream.
    /// </summary>
    /// <param name="weights">Positive candidate weights.</param>
    /// <param name="random">Mutable deterministic attempt stream.</param>
    /// <returns>Selected zero-based index.</returns>
    private static int SelectWeightedIndex(List<float> weights,
                                           ref GameProceduralLevelSolverRandom random)
    {
        double total = 0d;

        for (int index = 0; index < weights.Count; index++)
            total += weights[index];

        double threshold = random.NextFloat() * total;

        for (int index = 0; index < weights.Count; index++)
        {
            threshold -= weights[index];

            if (threshold <= 0d)
                return index;
        }

        return weights.Count - 1;
    }

    /// <summary>
    /// Checks whether another logical node may use one tile at the requested depth without exceeding its maximum copies.
    /// </summary>
    /// <param name="tile">Reusable tile to inspect.</param>
    /// <param name="copyCounts">Current copy counts keyed by tile technical ID.</param>
    /// <param name="depth">Absolute graph depth requested for the candidate node.</param>
    /// <returns>True when the hard depth constraint matches and copy capacity remains.</returns>
    private static bool CanUseTileAtDepth(GameProceduralRoomTileSolverInput tile,
                                          Dictionary<string, int> copyCounts,
                                          int depth)
    {
        if (tile.UseExactDepthConstraint && tile.ExactDepth != depth)
            return false;

        return !copyCounts.TryGetValue(tile.TechnicalId, out int count) || count < tile.MaximumCopies;
    }

    /// <summary>
    /// Checks technical and authored total-node capacity while reserving one terminal Boss node.
    /// </summary>
    /// <param name="input">Immutable solver request.</param>
    /// <param name="nodeCount">Current total node count.</param>
    /// <returns>True when one more Regular node can be created.</returns>
    private static bool CanAddRegularNode(GameProceduralLevelSolverInput input, int nodeCount)
    {
        return nodeCount < input.TargetNodeCountRange.y - 1 && nodeCount < input.MaximumNodeCount - 1;
    }
    #endregion

    #endregion
}
