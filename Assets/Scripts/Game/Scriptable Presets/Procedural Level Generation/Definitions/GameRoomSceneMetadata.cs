using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores an editor-generated portal and center-anchor snapshot for one reusable room scene.
/// </summary>
[Serializable]
public sealed class GameRoomSceneMetadata
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable Scene Manager scene ID owning this cached room metadata snapshot.")]
    [SerializeField]
    private string sceneId;

    [Tooltip("Unity scene GUID captured when this room metadata snapshot was refreshed.")]
    [SerializeField]
    private string sceneGuid;

    [Tooltip("Unity dependency hash captured during the last explicit or import-triggered metadata refresh.")]
    [SerializeField]
    private string dependencyHash;

    [Tooltip("Indicates that one source scene or nested SubScene changed after this metadata snapshot was refreshed.")]
    [SerializeField]
    private bool cacheStale = true;

    [Tooltip("Root room scene and recursively referenced SubScene asset paths included in this metadata snapshot.")]
    [SerializeField]
    private List<string> sourceScenePaths = new List<string>();

    [Tooltip("Number of authored center anchors discovered in this room; exactly one is required when center arrival is used.")]
    [SerializeField]
    private int centerAnchorCount;

    [Tooltip("Number of enabled enemy spawners discovered in bakeable SubScenes during the last metadata scan.")]
    [SerializeField]
    private int activeSpawnerCount;

    [Tooltip("Number of enabled bakeable enemy spawners containing at least one wave with one or more enemies.")]
    [SerializeField]
    private int activeSpawnerWithWavesCount;

    [Tooltip("Individual portal signatures discovered in the room authoring SubScene.")]
    [SerializeField]
    private List<GameRoomPortalMetadata> portals = new List<GameRoomPortalMetadata>();

    [Tooltip("Non-mutating authoring warnings captured during the last metadata scan for display in  tooling.")]
    [SerializeField]
    private List<string> authoringWarnings = new List<string>();
    #endregion

    #endregion

    #region Properties
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

    public string DependencyHash
    {
        get
        {
            return dependencyHash;
        }
    }

    public bool CacheStale
    {
        get
        {
            return cacheStale;
        }
    }

    public IReadOnlyList<string> SourceScenePaths
    {
        get
        {
            return sourceScenePaths;
        }
    }

    public int CenterAnchorCount
    {
        get
        {
            return centerAnchorCount;
        }
    }

    public int ActiveSpawnerCount
    {
        get
        {
            return activeSpawnerCount;
        }
    }

    public int ActiveSpawnerWithWavesCount
    {
        get
        {
            return activeSpawnerWithWavesCount;
        }
    }

    public bool IsRoomClearRewardEligible
    {
        get
        {
            return !cacheStale && activeSpawnerWithWavesCount > 0;
        }
    }

    public IReadOnlyList<GameRoomPortalMetadata> Portals
    {
        get
        {
            return portals;
        }
    }

    public IReadOnlyList<string> AuthoringWarnings
    {
        get
        {
            return authoringWarnings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Counts cached portals matching one side and capability for editor recap and feasibility validation.
    /// </summary>
    /// <param name="side">Room side to inspect.</param>
    /// <param name="capability">Portal capability to count.</param>
    /// <returns>Number of matching individual portal signatures.</returns>
    public int CountPortals(GameRoomPortalSide side, GameRoomPortalCapability capability)
    {
        if (portals == null)
            return 0;

        int count = 0;

        // Count each physical portal independently so same-side multiplicity remains authoritative.
        for (int index = 0; index < portals.Count; index++)
        {
            GameRoomPortalMetadata portal = portals[index];

            if (portal == null)
                continue;

            if (portal.Side != side || portal.Capability != capability)
                continue;

            count++;
        }

        return count;
    }
    #endregion

    #endregion
}
