using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one ordered procedural level, its room tile set and generation preferences.
/// </summary>
[Serializable]
public sealed class GameProceduralLevelDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Immutable technical identifier used to preserve this level across reorder and display-ID changes.")]
    [SerializeField]
    private string technicalId;

    [Tooltip("-authored stable level identifier used by runtime progression and diagnostics.")]
    [SerializeField]
    private string levelId = "LEVEL_01";

    [Tooltip("Human-readable level name displayed by the Game Management Tool and graph preview.")]
    [SerializeField]
    private string displayName = "Level 01";

    [Tooltip("Includes this level in ordered run progression and procedural graph generation.")]
    [SerializeField]
    private bool enabled = true;

    [Tooltip("Inclusive target range for the total number of logical nodes generated for this level.")]
    [SerializeField]
    private Vector2Int targetNodeCountRange = new Vector2Int(8, 14);

    [Tooltip("Inclusive preferred depth range used when the terminal Boss layer is selected.")]
    [SerializeField]
    private Vector2Int preferredBossDepthRange = new Vector2Int(5, 8);

    [Tooltip("-authored weights used to rank valid room and Boss placement candidates.")]
    [SerializeField]
    private GameProceduralLevelRuleSettings ruleSettings = new GameProceduralLevelRuleSettings();

    [Tooltip("Keeps required and optional exits locked until the active room reports its one-shot completion event.")]
    [SerializeField]
    private bool requireRoomClearBeforeExit = true;

    [Tooltip("Places the player at each target room's center anchor and skips all exit-to-entrance compatibility checks.")]
    [SerializeField]
    private bool useCenterArrival;

    [Tooltip("Reusable room scene tiles available to the generator for this level only.")]
    [SerializeField]
    private List<GameProceduralRoomTileDefinition> roomTiles = new List<GameProceduralRoomTileDefinition>();
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

    public string LevelId
    {
        get
        {
            return levelId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public bool Enabled
    {
        get
        {
            return enabled;
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

    public GameProceduralLevelRuleSettings RuleSettings
    {
        get
        {
            return ruleSettings;
        }
    }

    public bool RequireRoomClearBeforeExit
    {
        get
        {
            return requireRoomClearBeforeExit;
        }
    }

    public bool UseCenterArrival
    {
        get
        {
            return useCenterArrival;
        }
    }

    public IReadOnlyList<GameProceduralRoomTileDefinition> RoomTiles
    {
        get
        {
            return roomTiles;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures stable nested identifiers and required collection objects exist without correcting authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        // Initialize required identity and reference data without sanitizing  values.
        if (string.IsNullOrWhiteSpace(technicalId))
            technicalId = Guid.NewGuid().ToString("N");

        if (ruleSettings == null)
            ruleSettings = new GameProceduralLevelRuleSettings();

        if (roomTiles == null)
            roomTiles = new List<GameProceduralRoomTileDefinition>();

        // Preserve null entries for validation while initializing every existing tile.
        for (int index = 0; index < roomTiles.Count; index++)
        {
            GameProceduralRoomTileDefinition roomTile = roomTiles[index];

            if (roomTile == null)
                continue;

            roomTile.EnsureInitialized();
        }
    }

    /// <summary>
    /// Generates new technical identifiers for this level and every nested room tile after duplication.
    /// </summary>
    public void RegenerateTechnicalIds()
    {
        technicalId = Guid.NewGuid().ToString("N");

        if (roomTiles == null)
            return;

        // Detach all duplicated tile identities while preserving -facing IDs and authored settings.
        for (int index = 0; index < roomTiles.Count; index++)
        {
            GameProceduralRoomTileDefinition roomTile = roomTiles[index];

            if (roomTile == null)
                continue;

            roomTile.RegenerateTechnicalId();
        }
    }
    #endregion

    #endregion
}
