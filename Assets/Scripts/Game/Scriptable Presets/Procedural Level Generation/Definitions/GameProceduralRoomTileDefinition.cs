using System;
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

    [Tooltip("Designer-facing identifier displayed in the level tile list and graph preview.")]
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

    [Tooltip("Base weighted-selection value applied before level rule scores rank valid candidates.")]
    [SerializeField]
    private float baseSelectionWeight = 1f;
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

    public float BaseSelectionWeight
    {
        get
        {
            return baseSelectionWeight;
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
