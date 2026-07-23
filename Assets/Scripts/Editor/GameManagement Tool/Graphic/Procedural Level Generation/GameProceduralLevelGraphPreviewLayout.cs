using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one graph node and its deterministic depth-column rectangle in preview world coordinates.
/// </summary>
internal readonly struct GameProceduralLevelGraphPreviewNodeLayout
{
    #region Fields

    #region Readonly Fields
    private readonly GameProceduralLevelGraphNode node;
    private readonly Rect rect;
    private readonly int depthOrdinal;
    private readonly int depthNodeCount;
    #endregion

    #endregion

    #region Properties
    public GameProceduralLevelGraphNode Node
    {
        get
        {
            return node;
        }
    }

    public Rect Rect
    {
        get
        {
            return rect;
        }
    }

    public int DepthOrdinal
    {
        get
        {
            return depthOrdinal;
        }
    }

    public int DepthNodeCount
    {
        get
        {
            return depthNodeCount;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable preview placement for a generated logical room node.
    /// </summary>
    /// <param name="node">Generated graph node.</param>
    /// <param name="rect">World-space preview rectangle.</param>
    /// <param name="depthOrdinal">Stable row ordinal inside the node's depth column.</param>
    /// <param name="depthNodeCount">Total nodes sharing the same graph depth.</param>
    public GameProceduralLevelGraphPreviewNodeLayout(GameProceduralLevelGraphNode node,
                                                     Rect rect,
                                                     int depthOrdinal,
                                                     int depthNodeCount)
    {
        this.node = node;
        this.rect = rect;
        this.depthOrdinal = depthOrdinal;
        this.depthNodeCount = depthNodeCount;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores deterministic source and target anchor slots for one rendered graph edge.
/// </summary>
internal readonly struct GameProceduralLevelGraphPreviewEdgeLayout
{
    #region Fields

    #region Readonly Fields
    private readonly int sourceOrdinal;
    private readonly int sourceCount;
    private readonly int targetOrdinal;
    private readonly int targetCount;
    #endregion

    #endregion

    #region Properties
    public int SourceOrdinal
    {
        get
        {
            return sourceOrdinal;
        }
    }

    public int SourceCount
    {
        get
        {
            return sourceCount;
        }
    }

    public int TargetOrdinal
    {
        get
        {
            return targetOrdinal;
        }
    }

    public int TargetCount
    {
        get
        {
            return targetCount;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates immutable side-anchor assignments that keep converging edges visually independent.
    /// </summary>
    /// <param name="sourceOrdinal">Stable outgoing slot ordinal on the source node.</param>
    /// <param name="sourceCount">Total outgoing edges sharing the source node side.</param>
    /// <param name="targetOrdinal">Stable incoming slot ordinal on the target node.</param>
    /// <param name="targetCount">Total incoming edges sharing the target node side.</param>
    public GameProceduralLevelGraphPreviewEdgeLayout(int sourceOrdinal,
                                                     int sourceCount,
                                                     int targetOrdinal,
                                                     int targetCount)
    {
        this.sourceOrdinal = sourceOrdinal;
        this.sourceCount = sourceCount;
        this.targetOrdinal = targetOrdinal;
        this.targetCount = targetCount;
    }
    #endregion

    #endregion
}

/// <summary>
/// Arranges generated nodes and non-overlapping edge anchors without mutating or reordering solver output.
/// </summary>
internal sealed class GameProceduralLevelGraphPreviewLayout
{
    #region Constants
    public const float NodeWidth = 210f;
    public const float NodeHeight = 92f;
    private const float ColumnSpacing = 100f;
    private const float RowSpacing = 46f;
    private const float OuterPadding = 80f;
    #endregion

    #region Fields

    #region Readonly Fields
    private readonly GameProceduralLevelGraphPreviewNodeLayout[] nodes;
    private readonly GameProceduralLevelGraphPreviewEdgeLayout[] edges;
    private readonly Rect graphBounds;
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameProceduralLevelGraphPreviewNodeLayout> Nodes
    {
        get
        {
            return nodes;
        }
    }

    public Rect GraphBounds
    {
        get
        {
            return graphBounds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a depth-aligned layout whose vertical ordering follows stable node IDs.
    /// </summary>
    /// <param name="result">Successful generation result.</param>
    /// <returns>Immutable graph layout ready for zoomable rendering.</returns>
    public static GameProceduralLevelGraphPreviewLayout Build(GameProceduralLevelGenerationResult result)
    {
        if (result == null || !result.Success || result.Nodes.Count == 0)
            return new GameProceduralLevelGraphPreviewLayout(Array.Empty<GameProceduralLevelGraphPreviewNodeLayout>(),
                                                             Array.Empty<GameProceduralLevelGraphPreviewEdgeLayout>(),
                                                             new Rect(0f, 0f, 1f, 1f));

        int maximumDepth = 0;
        Dictionary<int, List<GameProceduralLevelGraphNode>> nodesByDepth = new Dictionary<int, List<GameProceduralLevelGraphNode>>();

        // Preserve node-ID order inside each deterministic depth column.
        for (int index = 0; index < result.Nodes.Count; index++)
        {
            GameProceduralLevelGraphNode node = result.Nodes[index];
            maximumDepth = Math.Max(maximumDepth, node.Depth);

            if (!nodesByDepth.TryGetValue(node.Depth, out List<GameProceduralLevelGraphNode> depthNodes))
            {
                depthNodes = new List<GameProceduralLevelGraphNode>();
                nodesByDepth.Add(node.Depth, depthNodes);
            }

            depthNodes.Add(node);
        }

        int maximumRows = 1;

        foreach (KeyValuePair<int, List<GameProceduralLevelGraphNode>> entry in nodesByDepth)
            maximumRows = Math.Max(maximumRows, entry.Value.Count);

        float graphHeight = maximumRows * NodeHeight + (maximumRows - 1) * RowSpacing;
        GameProceduralLevelGraphPreviewNodeLayout[] layouts = new GameProceduralLevelGraphPreviewNodeLayout[result.Nodes.Count];

        // Center shorter columns vertically so merge and branch curves remain easy to read.
        for (int depth = 0; depth <= maximumDepth; depth++)
        {
            if (!nodesByDepth.TryGetValue(depth, out List<GameProceduralLevelGraphNode> depthNodes))
                continue;

            float columnHeight = depthNodes.Count * NodeHeight + (depthNodes.Count - 1) * RowSpacing;
            float startY = OuterPadding + (graphHeight - columnHeight) * 0.5f;
            float x = OuterPadding + depth * (NodeWidth + ColumnSpacing);

            for (int row = 0; row < depthNodes.Count; row++)
            {
                GameProceduralLevelGraphNode node = depthNodes[row];
                Rect rect = new Rect(x,
                                     startY + row * (NodeHeight + RowSpacing),
                                     NodeWidth,
                                     NodeHeight);
                layouts[node.NodeId] = new GameProceduralLevelGraphPreviewNodeLayout(node,
                                                                                     rect,
                                                                                     row,
                                                                                     depthNodes.Count);
            }
        }

        float graphWidth = (maximumDepth + 1) * NodeWidth + maximumDepth * ColumnSpacing;
        Rect bounds = new Rect(0f,
                               0f,
                               graphWidth + OuterPadding * 2f,
                               graphHeight + OuterPadding * 2f);
        return new GameProceduralLevelGraphPreviewLayout(layouts,
                                                         BuildEdgeLayouts(result.Edges, layouts),
                                                         bounds);
    }

    /// <summary>
    /// Resolves one layout by the stable node ID used directly as its array index.
    /// </summary>
    /// <param name="nodeId">Stable generated node ID.</param>
    /// <param name="layout">Matching layout when present.</param>
    /// <returns>True when the node ID lies inside the layout.</returns>
    public bool TryGetNode(int nodeId, out GameProceduralLevelGraphPreviewNodeLayout layout)
    {
        if (nodeId < 0 || nodeId >= nodes.Length)
        {
            layout = default;
            return false;
        }

        layout = nodes[nodeId];
        return true;
    }

    /// <summary>
    /// Resolves precomputed side-anchor ordinals by the edge's stable generation-list index.
    /// </summary>
    /// <param name="edgeIndex">Zero-based edge index in the generation result.</param>
    /// <param name="layout">Matching source and target anchor layout when present.</param>
    /// <returns>True when the edge index lies inside the layout.</returns>
    public bool TryGetEdge(int edgeIndex, out GameProceduralLevelGraphPreviewEdgeLayout layout)
    {
        if (edgeIndex < 0 || edgeIndex >= edges.Length)
        {
            layout = default;
            return false;
        }

        layout = edges[edgeIndex];
        return true;
    }
    #endregion

    #region Edge Layout Methods
    /// <summary>
    /// Builds stable outgoing and incoming side slots so individual edge colors cannot overwrite each other.
    /// </summary>
    /// <param name="graphEdges">Generated edges in stable solver order.</param>
    /// <param name="nodeLayouts">Node rectangles indexed by stable node ID.</param>
    /// <returns>Edge layouts indexed identically to the generated edge collection.</returns>
    private static GameProceduralLevelGraphPreviewEdgeLayout[] BuildEdgeLayouts(
        IReadOnlyList<GameProceduralLevelGraphEdge> graphEdges,
        GameProceduralLevelGraphPreviewNodeLayout[] nodeLayouts)
    {
        int[] sourceOrdinals = new int[graphEdges.Count];
        int[] sourceCounts = new int[graphEdges.Count];
        int[] targetOrdinals = new int[graphEdges.Count];
        int[] targetCounts = new int[graphEdges.Count];
        PopulateEdgeAnchorSlots(graphEdges,
                                nodeLayouts,
                                true,
                                sourceOrdinals,
                                sourceCounts);
        PopulateEdgeAnchorSlots(graphEdges,
                                nodeLayouts,
                                false,
                                targetOrdinals,
                                targetCounts);
        GameProceduralLevelGraphPreviewEdgeLayout[] layouts =
            new GameProceduralLevelGraphPreviewEdgeLayout[graphEdges.Count];

        // Combine the independently ordered source and target slots by stable edge index.
        for (int edgeIndex = 0; edgeIndex < layouts.Length; edgeIndex++)
            layouts[edgeIndex] = new GameProceduralLevelGraphPreviewEdgeLayout(sourceOrdinals[edgeIndex],
                                                                               sourceCounts[edgeIndex],
                                                                               targetOrdinals[edgeIndex],
                                                                               targetCounts[edgeIndex]);

        return layouts;
    }

    /// <summary>
    /// Assigns side slots ordered by the opposite node's vertical placement to reduce edge crossings.
    /// </summary>
    /// <param name="graphEdges">Generated edges in stable solver order.</param>
    /// <param name="nodeLayouts">Node rectangles indexed by stable node ID.</param>
    /// <param name="groupBySource">True to build outgoing slots; false to build incoming slots.</param>
    /// <param name="ordinals">Destination array receiving each edge's side ordinal.</param>
    /// <param name="counts">Destination array receiving each edge's shared-side edge count.</param>
    private static void PopulateEdgeAnchorSlots(IReadOnlyList<GameProceduralLevelGraphEdge> graphEdges,
                                                GameProceduralLevelGraphPreviewNodeLayout[] nodeLayouts,
                                                bool groupBySource,
                                                int[] ordinals,
                                                int[] counts)
    {
        Dictionary<int, List<int>> edgeIndicesByNode = new Dictionary<int, List<int>>();

        // Group stable edge indices by the side-owning node.
        for (int edgeIndex = 0; edgeIndex < graphEdges.Count; edgeIndex++)
        {
            GameProceduralLevelGraphEdge edge = graphEdges[edgeIndex];
            int nodeId = groupBySource ? edge.SourceNodeId : edge.TargetNodeId;

            if (!edgeIndicesByNode.TryGetValue(nodeId, out List<int> edgeIndices))
            {
                edgeIndices = new List<int>();
                edgeIndicesByNode.Add(nodeId, edgeIndices);
            }

            edgeIndices.Add(edgeIndex);
        }

        // Match vertical slots to opposite-node order for deterministic, minimally crossed curves.
        foreach (KeyValuePair<int, List<int>> entry in edgeIndicesByNode)
        {
            List<int> edgeIndices = entry.Value;
            SortEdgeIndices(graphEdges, nodeLayouts, groupBySource, edgeIndices);

            for (int ordinal = 0; ordinal < edgeIndices.Count; ordinal++)
            {
                int edgeIndex = edgeIndices[ordinal];
                ordinals[edgeIndex] = ordinal;
                counts[edgeIndex] = edgeIndices.Count;
            }
        }
    }

    /// <summary>
    /// Sorts one usually small node-edge group in place without allocating comparison delegates.
    /// </summary>
    /// <param name="graphEdges">Generated edges in stable solver order.</param>
    /// <param name="nodeLayouts">Node rectangles indexed by stable node ID.</param>
    /// <param name="groupBySource">True when target nodes drive ordering; false when source nodes drive it.</param>
    /// <param name="edgeIndices">Mutable edge-index group to order.</param>
    private static void SortEdgeIndices(IReadOnlyList<GameProceduralLevelGraphEdge> graphEdges,
                                        GameProceduralLevelGraphPreviewNodeLayout[] nodeLayouts,
                                        bool groupBySource,
                                        List<int> edgeIndices)
    {
        // Insertion sort avoids delegate allocations for the low edge degrees used by room graphs.
        for (int index = 1; index < edgeIndices.Count; index++)
        {
            int edgeIndex = edgeIndices[index];
            int insertionIndex = index;

            while (insertionIndex > 0 &&
                   CompareOppositeNodes(graphEdges,
                                        nodeLayouts,
                                        groupBySource,
                                        edgeIndices[insertionIndex - 1],
                                        edgeIndex) > 0)
            {
                edgeIndices[insertionIndex] = edgeIndices[insertionIndex - 1];
                insertionIndex--;
            }

            edgeIndices[insertionIndex] = edgeIndex;
        }
    }

    /// <summary>
    /// Compares two edges by opposite-node height, node ID and edge index for fully stable slot ordering.
    /// </summary>
    /// <param name="graphEdges">Generated edges in stable solver order.</param>
    /// <param name="nodeLayouts">Node rectangles indexed by stable node ID.</param>
    /// <param name="groupBySource">True when the target is the opposite node; false when the source is opposite.</param>
    /// <param name="leftIndex">First edge index.</param>
    /// <param name="rightIndex">Second edge index.</param>
    /// <returns>Negative, zero or positive according to stable visual ordering.</returns>
    private static int CompareOppositeNodes(IReadOnlyList<GameProceduralLevelGraphEdge> graphEdges,
                                            GameProceduralLevelGraphPreviewNodeLayout[] nodeLayouts,
                                            bool groupBySource,
                                            int leftIndex,
                                            int rightIndex)
    {
        GameProceduralLevelGraphEdge leftEdge = graphEdges[leftIndex];
        GameProceduralLevelGraphEdge rightEdge = graphEdges[rightIndex];
        int leftNodeId = groupBySource ? leftEdge.TargetNodeId : leftEdge.SourceNodeId;
        int rightNodeId = groupBySource ? rightEdge.TargetNodeId : rightEdge.SourceNodeId;
        int heightComparison = nodeLayouts[leftNodeId].Rect.center.y.CompareTo(nodeLayouts[rightNodeId].Rect.center.y);

        if (heightComparison != 0)
            return heightComparison;

        int nodeComparison = leftNodeId.CompareTo(rightNodeId);
        return nodeComparison != 0 ? nodeComparison : leftIndex.CompareTo(rightIndex);
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable preview layout from precomputed node rectangles, edge slots and bounds.
    /// </summary>
    /// <param name="nodes">Node layout array indexed by Node ID.</param>
    /// <param name="edges">Edge layout array indexed by generation-list position.</param>
    /// <param name="graphBounds">Complete world-coordinate graph bounds.</param>
    private GameProceduralLevelGraphPreviewLayout(GameProceduralLevelGraphPreviewNodeLayout[] nodes,
                                                  GameProceduralLevelGraphPreviewEdgeLayout[] edges,
                                                  Rect graphBounds)
    {
        this.nodes = nodes;
        this.edges = edges;
        this.graphBounds = graphBounds;
    }
    #endregion

    #endregion
}
