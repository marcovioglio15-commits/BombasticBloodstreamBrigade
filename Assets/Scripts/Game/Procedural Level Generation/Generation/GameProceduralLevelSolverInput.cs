using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores one immutable portal signature consumed by the pure managed solver core.
/// </summary>
public readonly struct GameProceduralRoomPortalSolverInput
{
    #region Fields

    #region Readonly Fields
    private readonly string portalId;
    private readonly GameRoomPortalSide side;
    private readonly GameRoomPortalCapability capability;
    private readonly GameRoomPortalConnectionPolicy connectionPolicy;
    #endregion

    #endregion

    #region Properties
    public string PortalId
    {
        get
        {
            return portalId;
        }
    }

    public GameRoomPortalSide Side
    {
        get
        {
            return side;
        }
    }

    public GameRoomPortalCapability Capability
    {
        get
        {
            return capability;
        }
    }

    public GameRoomPortalConnectionPolicy ConnectionPolicy
    {
        get
        {
            return connectionPolicy;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates a plain portal signature from authored or baked configuration data.
    /// </summary>
    /// <param name="portalId">Stable physical portal ID.</param>
    /// <param name="side">Logical room side.</param>
    /// <param name="capability">Entrance, Exit or Both capability.</param>
    /// <param name="connectionPolicy">Required, Optional or LevelExit policy.</param>
    public GameProceduralRoomPortalSolverInput(string portalId,
                                               GameRoomPortalSide side,
                                               GameRoomPortalCapability capability,
                                               GameRoomPortalConnectionPolicy connectionPolicy)
    {
        this.portalId = portalId ?? string.Empty;
        this.side = side;
        this.capability = capability;
        this.connectionPolicy = connectionPolicy;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one immutable reusable room tile and its flattened room signature for pure graph generation.
/// </summary>
public sealed class GameProceduralRoomTileSolverInput
{
    #region Fields

    #region Readonly Fields
    private readonly string technicalId;
    private readonly string tileId;
    private readonly string sceneId;
    private readonly GameProceduralRoomRole role;
    private readonly int maximumCopies;
    private readonly Vector2Int preferredDepthRange;
    private readonly float baseSelectionWeight;
    private readonly int centerAnchorCount;
    private readonly GameProceduralRoomPortalSolverInput[] portals;
    private readonly bool useExactDepthConstraint;
    private readonly int exactDepth;
    #endregion

    #endregion

    #region Properties
    public string TechnicalId
    {
        get
        {
            return technicalId;
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

    public int MaximumCopies
    {
        get
        {
            return maximumCopies;
        }
    }

    public Vector2Int PreferredDepthRange
    {
        get
        {
            return preferredDepthRange;
        }
    }

    public float BaseSelectionWeight
    {
        get
        {
            return baseSelectionWeight;
        }
    }

    public int CenterAnchorCount
    {
        get
        {
            return centerAnchorCount;
        }
    }

    public IReadOnlyList<GameProceduralRoomPortalSolverInput> Portals
    {
        get
        {
            return portals;
        }
    }

    public bool UseExactDepthConstraint
    {
        get
        {
            return useExactDepthConstraint;
        }
    }

    public int ExactDepth
    {
        get
        {
            return exactDepth;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one pure solver tile from editor objects or flattened runtime configuration buffers.
    /// </summary>
    /// <param name="technicalId">Stable reusable tile technical ID.</param>
    /// <param name="tileId">-facing tile ID.</param>
    /// <param name="sceneId">Canonical room scene ID.</param>
    /// <param name="role">Structural tile role.</param>
    /// <param name="maximumCopies">Maximum logical nodes using this tile.</param>
    /// <param name="preferredDepthRange">Inclusive preferred depth range.</param>
    /// <param name="baseSelectionWeight">Positive base candidate weight.</param>
    /// <param name="centerAnchorCount">Number of cached center anchors.</param>
    /// <param name="portals">Flattened individual portal signatures.</param>
    /// <param name="useExactDepthConstraint">Whether this tile is valid at one hard graph depth only.</param>
    /// <param name="exactDepth">Required graph depth when the hard constraint is enabled.</param>
    public GameProceduralRoomTileSolverInput(string technicalId,
                                             string tileId,
                                             string sceneId,
                                             GameProceduralRoomRole role,
                                             int maximumCopies,
                                             Vector2Int preferredDepthRange,
                                             float baseSelectionWeight,
                                             int centerAnchorCount,
                                             IList<GameProceduralRoomPortalSolverInput> portals,
                                             bool useExactDepthConstraint = false,
                                             int exactDepth = 0)
    {
        this.technicalId = technicalId ?? string.Empty;
        this.tileId = tileId ?? string.Empty;
        this.sceneId = sceneId ?? string.Empty;
        this.role = role;
        this.maximumCopies = maximumCopies;
        this.preferredDepthRange = preferredDepthRange;
        this.baseSelectionWeight = baseSelectionWeight;
        this.centerAnchorCount = centerAnchorCount;
        this.portals = CopyPortals(portals);
        this.useExactDepthConstraint = useExactDepthConstraint;
        this.exactDepth = exactDepth;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Copies portal inputs so callers cannot mutate solver configuration during bounded backtracking.
    /// </summary>
    /// <param name="source">Source portal collection.</param>
    /// <returns>Independent portal array.</returns>
    private static GameProceduralRoomPortalSolverInput[] CopyPortals(IList<GameProceduralRoomPortalSolverInput> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<GameProceduralRoomPortalSolverInput>();

        GameProceduralRoomPortalSolverInput[] copy = new GameProceduralRoomPortalSolverInput[source.Count];

        for (int index = 0; index < source.Count; index++)
            copy[index] = source[index];

        return copy;
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines a complete editor-independent generation request consumable from ScriptableObjects or ECS buffer copies.
/// </summary>
public sealed class GameProceduralLevelSolverInput
{
    #region Fields

    #region Readonly Fields
    private readonly string levelTechnicalId;
    private readonly string levelId;
    private readonly Vector2Int targetNodeCountRange;
    private readonly Vector2Int preferredBossDepthRange;
    private readonly float roomDepthScore;
    private readonly float bossDepthScore;
    private readonly float fittingScore;
    private readonly bool useCenterArrival;
    private readonly bool requiresLevelExit;
    private readonly int maximumNodeCount;
    private readonly int maximumDepth;
    private readonly int maximumGenerationAttempts;
    private readonly GameProceduralRoomTileSolverInput[] roomTiles;
    #endregion

    #endregion

    #region Properties
    public string LevelTechnicalId
    {
        get
        {
            return levelTechnicalId;
        }
    }

    public string LevelId
    {
        get
        {
            return levelId;
        }
    }

    public Vector2Int TargetNodeCountRange
    {
        get
        {
            return targetNodeCountRange;
        }
    }

    public Vector2Int PreferredBossDepthRange
    {
        get
        {
            return preferredBossDepthRange;
        }
    }

    public float RoomDepthScore
    {
        get
        {
            return roomDepthScore;
        }
    }

    public float BossDepthScore
    {
        get
        {
            return bossDepthScore;
        }
    }

    public float FittingScore
    {
        get
        {
            return fittingScore;
        }
    }

    public bool UseCenterArrival
    {
        get
        {
            return useCenterArrival;
        }
    }

    public bool RequiresLevelExit
    {
        get
        {
            return requiresLevelExit;
        }
    }

    public int MaximumNodeCount
    {
        get
        {
            return maximumNodeCount;
        }
    }

    public int MaximumDepth
    {
        get
        {
            return maximumDepth;
        }
    }

    public int MaximumGenerationAttempts
    {
        get
        {
            return maximumGenerationAttempts;
        }
    }

    public IReadOnlyList<GameProceduralRoomTileSolverInput> RoomTiles
    {
        get
        {
            return roomTiles;
        }
    }
    #endregion

    #region Methods

    #region Constructors
    /// <summary>
    /// Creates one immutable pure managed request shared by editor preview and authoritative runtime generation.
    /// </summary>
    /// <param name="levelTechnicalId">Immutable level identity used for seed derivation.</param>
    /// <param name="levelId">-facing level identity used by diagnostics.</param>
    /// <param name="targetNodeCountRange">Inclusive acceptable total node range.</param>
    /// <param name="preferredBossDepthRange">Inclusive preferred terminal depth range.</param>
    /// <param name="roomDepthScore">Non-negative room depth rule weight.</param>
    /// <param name="bossDepthScore">Non-negative Boss depth rule weight.</param>
    /// <param name="fittingScore">Non-negative fitting quality rule weight.</param>
    /// <param name="useCenterArrival">Whether target entrance and side fitting are skipped.</param>
    /// <param name="requiresLevelExit">Whether the terminal Boss must expose a usable authored LevelExit portal.</param>
    /// <param name="maximumNodeCount">Hard technical node limit.</param>
    /// <param name="maximumDepth">Hard technical depth limit.</param>
    /// <param name="maximumGenerationAttempts">Bounded deterministic restart count.</param>
    /// <param name="roomTiles">Reusable room tile inputs.</param>
    public GameProceduralLevelSolverInput(string levelTechnicalId,
                                          string levelId,
                                          Vector2Int targetNodeCountRange,
                                          Vector2Int preferredBossDepthRange,
                                          float roomDepthScore,
                                          float bossDepthScore,
                                          float fittingScore,
                                          bool useCenterArrival,
                                          bool requiresLevelExit,
                                          int maximumNodeCount,
                                          int maximumDepth,
                                          int maximumGenerationAttempts,
                                          IList<GameProceduralRoomTileSolverInput> roomTiles)
    {
        this.levelTechnicalId = levelTechnicalId ?? string.Empty;
        this.levelId = levelId ?? string.Empty;
        this.targetNodeCountRange = targetNodeCountRange;
        this.preferredBossDepthRange = preferredBossDepthRange;
        this.roomDepthScore = roomDepthScore;
        this.bossDepthScore = bossDepthScore;
        this.fittingScore = fittingScore;
        this.useCenterArrival = useCenterArrival;
        this.requiresLevelExit = requiresLevelExit;
        this.maximumNodeCount = maximumNodeCount;
        this.maximumDepth = maximumDepth;
        this.maximumGenerationAttempts = maximumGenerationAttempts;
        this.roomTiles = CopyTiles(roomTiles);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Copies tile inputs so the generation request remains immutable across retries.
    /// </summary>
    /// <param name="source">Source tile collection.</param>
    /// <returns>Independent tile array.</returns>
    private static GameProceduralRoomTileSolverInput[] CopyTiles(IList<GameProceduralRoomTileSolverInput> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<GameProceduralRoomTileSolverInput>();

        GameProceduralRoomTileSolverInput[] copy = new GameProceduralRoomTileSolverInput[source.Count];

        for (int index = 0; index < source.Count; index++)
            copy[index] = source[index];

        return copy;
    }
    #endregion

    #endregion
}
