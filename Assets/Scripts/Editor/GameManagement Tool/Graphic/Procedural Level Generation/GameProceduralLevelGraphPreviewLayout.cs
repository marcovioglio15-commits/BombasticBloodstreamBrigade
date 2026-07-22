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
/// Arranges generated nodes into stable depth columns without mutating or reordering solver output.
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
        return new GameProceduralLevelGraphPreviewLayout(layouts, bounds);
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
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable preview layout from precomputed node rectangles and bounds.
    /// </summary>
    /// <param name="nodes">Node layout array indexed by Node ID.</param>
    /// <param name="graphBounds">Complete world-coordinate graph bounds.</param>
    private GameProceduralLevelGraphPreviewLayout(GameProceduralLevelGraphPreviewNodeLayout[] nodes,
                                                  Rect graphBounds)
    {
        this.nodes = nodes;
        this.graphBounds = graphBounds;
    }
    #endregion

    #endregion
}
