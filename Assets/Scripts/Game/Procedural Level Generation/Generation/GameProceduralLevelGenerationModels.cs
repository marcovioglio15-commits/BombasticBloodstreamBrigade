using System;
using System.Collections.Generic;

/// <summary>
/// Identifies the stable reason why one bounded procedural graph generation request failed.
/// </summary>
public enum GameProceduralLevelGenerationFailureCode : byte
{
    None = 0,
    InvalidInput = 1,
    ValidationFailed = 2,
    MissingStartTile = 3,
    MissingBossTile = 4,
    NodeBudgetExceeded = 5,
    DepthBudgetExceeded = 6,
    RequiredExitUnresolved = 7,
    TargetEntranceUnavailable = 8,
    NoRegularRoomCandidate = 9,
    NoBossRoomCandidate = 10,
    SearchBudgetExceeded = 11,
    AttemptLimitReached = 12,
    GraphInvariantViolation = 13
}

/// <summary>
/// Describes one immutable logical room node produced by the shared procedural level solver.
/// </summary>
public readonly struct GameProceduralLevelGraphNode
{
    #region Fields

    #region Readonly Fields
    private readonly int nodeId;
    private readonly string tileTechnicalId;
    private readonly string tileId;
    private readonly string sceneId;
    private readonly GameProceduralRoomRole role;
    private readonly int depth;
    private readonly int copyOrdinal;
    #endregion

    #endregion

    #region Properties
    public int NodeId
    {
        get
        {
            return nodeId;
        }
    }

    public string TileTechnicalId
    {
        get
        {
            return tileTechnicalId;
        }
    }

    public string TileId
    {
        get
        {
            return tileId;
        }
    }

    public string SceneId
    {
        get
        {
            return sceneId;
        }
    }

    public GameProceduralRoomRole Role
    {
        get
        {
            return role;
        }
    }

    public int Depth
    {
        get
        {
            return depth;
        }
    }

    public int CopyOrdinal
    {
        get
        {
            return copyOrdinal;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable graph node whose numeric ID is stable for the lifetime of its generation result.
    /// </summary>
    /// <param name="nodeId">Zero-based node index stored by runtime graph buffers.</param>
    /// <param name="tileTechnicalId">Stable technical ID of the reusable source tile.</param>
    /// <param name="tileId">Designer-facing tile label used by diagnostics and preview.</param>
    /// <param name="sceneId">Canonical Scene Manager scene ID loaded for this node.</param>
    /// <param name="role">Structural Start, Regular or Boss role.</param>
    /// <param name="depth">Zero-based graph depth.</param>
    /// <param name="copyOrdinal">One-based occurrence number of this tile in the generated graph.</param>
    public GameProceduralLevelGraphNode(int nodeId,
                                        string tileTechnicalId,
                                        string tileId,
                                        string sceneId,
                                        GameProceduralRoomRole role,
                                        int depth,
                                        int copyOrdinal)
    {
        this.nodeId = nodeId;
        this.tileTechnicalId = tileTechnicalId ?? string.Empty;
        this.tileId = tileId ?? string.Empty;
        this.sceneId = sceneId ?? string.Empty;
        this.role = role;
        this.depth = depth;
        this.copyOrdinal = copyOrdinal;
    }
    #endregion

    #endregion
}

/// <summary>
/// Describes one immutable forward edge and its optional physical portal assignment.
/// </summary>
public readonly struct GameProceduralLevelGraphEdge
{
    #region Fields

    #region Readonly Fields
    private readonly int edgeId;
    private readonly int sourceNodeId;
    private readonly int targetNodeId;
    private readonly string sourcePortalId;
    private readonly string targetPortalId;
    private readonly GameRoomPortalSide sourceSide;
    private readonly GameRoomPortalSide targetSide;
    private readonly bool usesCenterArrival;
    #endregion

    #endregion

    #region Properties
    public int EdgeId
    {
        get
        {
            return edgeId;
        }
    }

    public int SourceNodeId
    {
        get
        {
            return sourceNodeId;
        }
    }

    public int TargetNodeId
    {
        get
        {
            return targetNodeId;
        }
    }

    public string SourcePortalId
    {
        get
        {
            return sourcePortalId;
        }
    }

    public string TargetPortalId
    {
        get
        {
            return targetPortalId;
        }
    }

    public GameRoomPortalSide SourceSide
    {
        get
        {
            return sourceSide;
        }
    }

    public GameRoomPortalSide TargetSide
    {
        get
        {
            return targetSide;
        }
    }

    public bool UsesCenterArrival
    {
        get
        {
            return usesCenterArrival;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one depth-advancing edge used identically by editor preview and runtime graph storage.
    /// </summary>
    /// <param name="edgeId">Zero-based edge index stable within the generation result.</param>
    /// <param name="sourceNodeId">Source node index.</param>
    /// <param name="targetNodeId">Target node index at the next depth.</param>
    /// <param name="sourcePortalId">Physical source exit ID, or an empty value when no authored exit exists.</param>
    /// <param name="targetPortalId">Selected target entrance ID, empty in center-arrival mode.</param>
    /// <param name="sourceSide">Authored source side retained for traversal and diagnostics.</param>
    /// <param name="targetSide">Opposite target side, or the default enum value in center-arrival mode.</param>
    /// <param name="usesCenterArrival">Whether target portal fitting was intentionally skipped.</param>
    public GameProceduralLevelGraphEdge(int edgeId,
                                        int sourceNodeId,
                                        int targetNodeId,
                                        string sourcePortalId,
                                        string targetPortalId,
                                        GameRoomPortalSide sourceSide,
                                        GameRoomPortalSide targetSide,
                                        bool usesCenterArrival)
    {
        this.edgeId = edgeId;
        this.sourceNodeId = sourceNodeId;
        this.targetNodeId = targetNodeId;
        this.sourcePortalId = sourcePortalId ?? string.Empty;
        this.targetPortalId = targetPortalId ?? string.Empty;
        this.sourceSide = sourceSide;
        this.targetSide = targetSide;
        this.usesCenterArrival = usesCenterArrival;
    }
    #endregion

    #endregion
}

/// <summary>
/// Returns an immutable graph or an explicit stable failure without exposing solver working state.
/// </summary>
public sealed class GameProceduralLevelGenerationResult
{
    #region Fields

    #region Readonly Fields
    private readonly bool success;
    private readonly GameProceduralLevelGenerationFailureCode failureCode;
    private readonly string diagnostic;
    private readonly uint runSeed;
    private readonly uint levelSeed;
    private readonly int attemptsUsed;
    private readonly IReadOnlyList<GameProceduralLevelGraphNode> nodes;
    private readonly IReadOnlyList<GameProceduralLevelGraphEdge> edges;
    #endregion

    #endregion

    #region Properties
    public bool Success
    {
        get
        {
            return success;
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

    public uint RunSeed
    {
        get
        {
            return runSeed;
        }
    }

    public uint LevelSeed
    {
        get
        {
            return levelSeed;
        }
    }

    public int AttemptsUsed
    {
        get
        {
            return attemptsUsed;
        }
    }

    public IReadOnlyList<GameProceduralLevelGraphNode> Nodes
    {
        get
        {
            return nodes;
        }
    }

    public IReadOnlyList<GameProceduralLevelGraphEdge> Edges
    {
        get
        {
            return edges;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates a successful immutable generation result from solver-owned node and edge collections.
    /// </summary>
    /// <param name="runSeed">Authoritative run seed supplied to generation.</param>
    /// <param name="levelSeed">Stable seed derived for this level.</param>
    /// <param name="attemptsUsed">Number of bounded attempts consumed.</param>
    /// <param name="nodes">Completed graph nodes.</param>
    /// <param name="edges">Completed graph edges.</param>
    /// <returns>Successful result containing defensive collection copies.</returns>
    public static GameProceduralLevelGenerationResult CreateSuccess(uint runSeed,
                                                                     uint levelSeed,
                                                                     int attemptsUsed,
                                                                     IList<GameProceduralLevelGraphNode> nodes,
                                                                     IList<GameProceduralLevelGraphEdge> edges)
    {
        return new GameProceduralLevelGenerationResult(true,
                                                        GameProceduralLevelGenerationFailureCode.None,
                                                        string.Empty,
                                                        runSeed,
                                                        levelSeed,
                                                        attemptsUsed,
                                                        CopyNodes(nodes),
                                                        CopyEdges(edges));
    }

    /// <summary>
    /// Creates an explicit failed generation result without a partially usable graph.
    /// </summary>
    /// <param name="failureCode">Stable failure category.</param>
    /// <param name="diagnostic">Actionable diagnostic text.</param>
    /// <param name="runSeed">Authoritative run seed supplied to generation.</param>
    /// <param name="levelSeed">Derived level seed when available.</param>
    /// <param name="attemptsUsed">Number of bounded attempts consumed.</param>
    /// <returns>Failed result with empty node and edge collections.</returns>
    public static GameProceduralLevelGenerationResult CreateFailure(GameProceduralLevelGenerationFailureCode failureCode,
                                                                     string diagnostic,
                                                                     uint runSeed,
                                                                     uint levelSeed,
                                                                     int attemptsUsed)
    {
        return new GameProceduralLevelGenerationResult(false,
                                                        failureCode,
                                                        diagnostic,
                                                        runSeed,
                                                        levelSeed,
                                                        attemptsUsed,
                                                        Array.Empty<GameProceduralLevelGraphNode>(),
                                                        Array.Empty<GameProceduralLevelGraphEdge>());
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes one immutable generation result created through the public success and failure factories.
    /// </summary>
    /// <param name="success">Whether generation completed successfully.</param>
    /// <param name="failureCode">Stable failure code when generation failed.</param>
    /// <param name="diagnostic">Actionable failure description.</param>
    /// <param name="runSeed">Authoritative run seed.</param>
    /// <param name="levelSeed">Derived per-level seed.</param>
    /// <param name="attemptsUsed">Bounded attempt count consumed.</param>
    /// <param name="nodes">Immutable node array.</param>
    /// <param name="edges">Immutable edge array.</param>
    private GameProceduralLevelGenerationResult(bool success,
                                                GameProceduralLevelGenerationFailureCode failureCode,
                                                string diagnostic,
                                                uint runSeed,
                                                uint levelSeed,
                                                int attemptsUsed,
                                                GameProceduralLevelGraphNode[] nodes,
                                                GameProceduralLevelGraphEdge[] edges)
    {
        this.success = success;
        this.failureCode = failureCode;
        this.diagnostic = diagnostic ?? string.Empty;
        this.runSeed = runSeed;
        this.levelSeed = levelSeed;
        this.attemptsUsed = attemptsUsed;
        this.nodes = Array.AsReadOnly(nodes);
        this.edges = Array.AsReadOnly(edges);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Copies solver-owned nodes so later backtracking cannot mutate an emitted result.
    /// </summary>
    /// <param name="source">Source node collection.</param>
    /// <returns>Independent node array.</returns>
    private static GameProceduralLevelGraphNode[] CopyNodes(IList<GameProceduralLevelGraphNode> source)
    {
        GameProceduralLevelGraphNode[] copy = new GameProceduralLevelGraphNode[source.Count];

        for (int index = 0; index < source.Count; index++)
            copy[index] = source[index];

        return copy;
    }

    /// <summary>
    /// Copies solver-owned edges so later backtracking cannot mutate an emitted result.
    /// </summary>
    /// <param name="source">Source edge collection.</param>
    /// <returns>Independent edge array.</returns>
    private static GameProceduralLevelGraphEdge[] CopyEdges(IList<GameProceduralLevelGraphEdge> source)
    {
        GameProceduralLevelGraphEdge[] copy = new GameProceduralLevelGraphEdge[source.Count];

        for (int index = 0; index < source.Count; index++)
            copy[index] = source[index];

        return copy;
    }
    #endregion

    #endregion
}
