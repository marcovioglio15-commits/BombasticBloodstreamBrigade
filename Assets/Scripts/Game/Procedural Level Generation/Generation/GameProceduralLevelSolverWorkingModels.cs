using System;
using System.Collections.Generic;

/// <summary>
/// Provides a deterministic platform-independent random stream for weighted solver ordering.
/// </summary>
internal struct GameProceduralLevelSolverRandom
{
    #region Fields
    private uint state;
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Initializes one non-zero xorshift stream from a derived attempt seed.
    /// </summary>
    /// <param name="seed">Deterministic attempt seed.</param>
    public GameProceduralLevelSolverRandom(uint seed)
    {
        state = seed == 0u ? 0xA341316Cu : seed;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Returns one uniformly distributed unsigned value and advances the stream once.
    /// </summary>
    /// <returns>Next deterministic unsigned value.</returns>
    public uint NextUInt()
    {
        uint value = state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }

    /// <summary>
    /// Returns one deterministic integer inside a non-empty zero-based range.
    /// </summary>
    /// <param name="maximumExclusive">Exclusive positive upper bound.</param>
    /// <returns>Integer greater than or equal to zero and below the upper bound.</returns>
    public int NextInt(int maximumExclusive)
    {
        if (maximumExclusive <= 1)
            return 0;

        return (int)(NextUInt() % (uint)maximumExclusive);
    }

    /// <summary>
    /// Returns one deterministic normalized value in the half-open interval zero to one.
    /// </summary>
    /// <returns>Normalized deterministic value.</returns>
    public float NextFloat()
    {
        return (NextUInt() & 0x00FFFFFFu) / 16777216f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores mutable per-node assignments required only while the solver backtracks.
/// </summary>
internal sealed class GameProceduralLevelSolverNodeState
{
    #region Fields

    #region Readonly Fields
    private readonly GameProceduralRoomTileSolverInput tile;
    private readonly GameProceduralLevelGraphNode graphNode;
    private readonly HashSet<string> usedIncomingPortalIds = new HashSet<string>(StringComparer.Ordinal);
    #endregion

    #endregion

    #region Properties
    public GameProceduralRoomTileSolverInput Tile
    {
        get
        {
            return tile;
        }
    }

    public GameProceduralLevelGraphNode GraphNode
    {
        get
        {
            return graphNode;
        }
    }

    public HashSet<string> UsedIncomingPortalIds
    {
        get
        {
            return usedIncomingPortalIds;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates mutable working state around one immutable emitted graph node.
    /// </summary>
    /// <param name="tile">Reusable tile selected for the node.</param>
    /// <param name="graphNode">Immutable result node representation.</param>
    public GameProceduralLevelSolverNodeState(GameProceduralRoomTileSolverInput tile,
                                              GameProceduralLevelGraphNode graphNode)
    {
        this.tile = tile;
        this.graphNode = graphNode;
    }
    #endregion

    #endregion
}

/// <summary>
/// Associates one physical source exit with the logical node that must consume it.
/// </summary>
internal readonly struct GameProceduralLevelPendingExit
{
    #region Fields

    #region Readonly Fields
    private readonly int sourceNodeId;
    private readonly GameProceduralRoomPortalSolverInput portal;
    #endregion

    #endregion

    #region Properties
    public int SourceNodeId
    {
        get
        {
            return sourceNodeId;
        }
    }

    public GameProceduralRoomPortalSolverInput Portal
    {
        get
        {
            return portal;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one pending source exit assignment for the next graph depth.
    /// </summary>
    /// <param name="sourceNodeId">Node owning the physical exit.</param>
    /// <param name="portal">Individual source portal signature.</param>
    public GameProceduralLevelPendingExit(int sourceNodeId,
                                          GameProceduralRoomPortalSolverInput portal)
    {
        this.sourceNodeId = sourceNodeId;
        this.portal = portal;
    }
    #endregion

    #endregion
}

/// <summary>
/// Represents one weighted existing-node or new-tile assignment for a pending edge.
/// </summary>
internal sealed class GameProceduralLevelTargetCandidate
{
    #region Fields

    #region Readonly Fields
    private readonly int existingNodeId;
    private readonly GameProceduralRoomTileSolverInput newTile;
    private readonly GameProceduralRoomPortalSolverInput targetPortal;
    private readonly bool hasTargetPortal;
    private readonly float weight;
    #endregion

    #endregion

    #region Properties
    public int ExistingNodeId
    {
        get
        {
            return existingNodeId;
        }
    }

    public GameProceduralRoomTileSolverInput NewTile
    {
        get
        {
            return newTile;
        }
    }

    public GameProceduralRoomPortalSolverInput TargetPortal
    {
        get
        {
            return targetPortal;
        }
    }

    public bool HasTargetPortal
    {
        get
        {
            return hasTargetPortal;
        }
    }

    public float Weight
    {
        get
        {
            return weight;
        }
    }

    public bool CreatesNode
    {
        get
        {
            return newTile != null;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one target assignment candidate used by weighted ordering and recursive rollback.
    /// </summary>
    /// <param name="existingNodeId">Existing next-layer target ID, or -1 for a new node.</param>
    /// <param name="newTile">Tile instantiated when this candidate creates a node.</param>
    /// <param name="targetPortal">Target entrance assigned in portal-arrival mode.</param>
    /// <param name="hasTargetPortal">Whether a target entrance assignment is present.</param>
    /// <param name="weight">Positive candidate ordering weight.</param>
    public GameProceduralLevelTargetCandidate(int existingNodeId,
                                              GameProceduralRoomTileSolverInput newTile,
                                              GameProceduralRoomPortalSolverInput targetPortal,
                                              bool hasTargetPortal,
                                              float weight)
    {
        this.existingNodeId = existingNodeId;
        this.newTile = newTile;
        this.targetPortal = targetPortal;
        this.hasTargetPortal = hasTargetPortal;
        this.weight = weight;
    }
    #endregion

    #endregion
}

/// <summary>
/// Captures mutable collection lengths so one failed branch can be rolled back without copying the complete graph.
/// </summary>
internal readonly struct GameProceduralLevelSolverSnapshot
{
    #region Fields

    #region Readonly Fields
    private readonly int nodeCount;
    private readonly int edgeCount;
    #endregion

    #endregion

    #region Properties
    public int NodeCount
    {
        get
        {
            return nodeCount;
        }
    }

    public int EdgeCount
    {
        get
        {
            return edgeCount;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Captures node and edge lengths before one recursive solver branch mutates working state.
    /// </summary>
    /// <param name="nodeCount">Current node count.</param>
    /// <param name="edgeCount">Current edge count.</param>
    public GameProceduralLevelSolverSnapshot(int nodeCount, int edgeCount)
    {
        this.nodeCount = nodeCount;
        this.edgeCount = edgeCount;
    }
    #endregion

    #endregion
}
