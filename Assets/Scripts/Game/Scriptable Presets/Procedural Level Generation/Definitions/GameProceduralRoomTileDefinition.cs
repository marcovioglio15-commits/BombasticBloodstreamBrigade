using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one reusable room scene tile and the constraints applied to its generated graph copies.
/// </summary>
[Serializable]
public sealed class GameProceduralRoomTileDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Immutable technical identifier used to preserve this tile across editor reorder and rename operations.")]
    [SerializeField]
    private string technicalId;

    [Tooltip("-facing identifier displayed in the level tile list and graph preview.")]
    [SerializeField]
    private string tileId = "ROOM_TILE";

    [Tooltip("Stable Scene Manager scene ID selected through the catalog-backed room scene popup.")]
    [SerializeField]
    private string sceneId;

    [Tooltip("Cached Unity scene GUID used to detect stale scene ID references after catalog or asset changes.")]
    [SerializeField]
    private string sceneGuid;

    [Tooltip("Structural role assigned to this tile within the generated level graph.")]
    [SerializeField]
    private GameProceduralRoomRole role = GameProceduralRoomRole.Regular;

    [Tooltip("Maximum number of logical graph nodes that may reference this room scene in one generated level.")]
    [SerializeField]
    private int maximumCopies = 1;

    [Tooltip("Inclusive preferred depth range used by the room-depth scoring rule.")]
    [SerializeField]
    private Vector2Int preferredDepthRange = new Vector2Int(1, 8);

    [Tooltip("Restricts every generated occurrence of this tile to one exact graph depth instead of using the preferred depth range for placement scoring.")]
    [SerializeField]
    private bool useExactDepthConstraint;

    [Tooltip("Required graph depth for this tile when Exact Depth Constraint is enabled; the tile is excluded from every other depth.")]
    [SerializeField]
    private int exactDepth = 1;

    [Tooltip("Base weighted-selection value applied before level rule scores rank valid candidates.")]
    [SerializeField]
    private float baseSelectionWeight = 1f;

    [Tooltip("Ordered Room Clear Reward references granted after every wave and remaining enemy in this tile have been cleared.")]
    [SerializeField]
    private List<GameRoomRewardTileAssignment> roomRewards = new List<GameRoomRewardTileAssignment>();
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

    public string SceneGuid
    {
        get
        {
            return sceneGuid;
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

    public float BaseSelectionWeight
    {
        get
        {
            return baseSelectionWeight;
        }
    }

    public IReadOnlyList<GameRoomRewardTileAssignment> RoomRewards
    {
        get
        {
            return roomRewards;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures this tile owns the stable technical identifier required by editor selection and graph references.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(technicalId))
            technicalId = Guid.NewGuid().ToString("N");

        if (roomRewards == null)
            roomRewards = new List<GameRoomRewardTileAssignment>();
    }

    /// <summary>
    /// Generates a new technical identifier after a containing preset or level is duplicated.
    /// </summary>
    public void RegenerateTechnicalId()
    {
        technicalId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #endregion
}
