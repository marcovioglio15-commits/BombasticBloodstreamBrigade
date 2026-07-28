using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines ordered procedural levels, reusable room tiles, cached portal metadata and generation settings.
/// </summary>
[CreateAssetMenu(fileName = "GameProceduralLevelPreset", menuName = "Game/Procedural Level Preset", order = 24)]
public sealed class GameProceduralLevelPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this Procedural Level preset, used for stable editor references.")]
    [SerializeField]
    private string presetId;

    [Tooltip("Procedural Level preset name displayed in Game Management Tool.")]
    [SerializeField]
    private string presetName = "New Procedural Level Preset";

    [Tooltip("Short description of this ordered procedural level configuration.")]
    [SerializeField]
    private string description;

    [Tooltip("Optional semantic version string for this procedural level preset.")]
    [SerializeField]
    private string version = "1.0.0";

    [Header("Scene Catalog")]
    [Tooltip("Scene Manager preset supplying canonical room scene IDs, GUIDs and load backend metadata.")]
    [SerializeField]
    private GameSceneManagerPreset sceneCatalogPreset;

    [Header("Generation")]
    [Tooltip("Deterministic seed policy and hard technical limits shared by every level in this preset.")]
    [SerializeField]
    private GameProceduralLevelGenerationSettings generationSettings = new GameProceduralLevelGenerationSettings();

    [Header("Transition Presentation")]
    [Tooltip("Player visibility, animation and relocation settings used for intra-level room transitions.")]
    [SerializeField]
    private GameProceduralLevelTransitionSettings transitionSettings = new GameProceduralLevelTransitionSettings();

    [Header("Levels")]
    [Tooltip("Ordered procedural levels traversed by a run; each level owns its independent room tile set and rule scores.")]
    [SerializeField]
    private List<GameProceduralLevelDefinition> levels = new List<GameProceduralLevelDefinition>();

    [Header("Room Metadata Cache")]
    [Tooltip("Editor-generated portal and center-anchor snapshots shared by every tile referencing the same room scene.")]
    [SerializeField]
    private List<GameRoomSceneMetadata> roomMetadata = new List<GameRoomSceneMetadata>();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public GameSceneManagerPreset SceneCatalogPreset
    {
        get
        {
            return sceneCatalogPreset;
        }
    }

    public GameProceduralLevelGenerationSettings GenerationSettings
    {
        get
        {
            return generationSettings;
        }
    }

    public GameProceduralLevelTransitionSettings TransitionSettings
    {
        get
        {
            return transitionSettings;
        }
    }

    public IReadOnlyList<GameProceduralLevelDefinition> Levels
    {
        get
        {
            return levels;
        }
    }

    public IReadOnlyList<GameRoomSceneMetadata> RoomMetadata
    {
        get
        {
            return roomMetadata;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures required reference objects and stable nested identifiers exist without correcting authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        // Initialize required identity, settings and collections while preserving invalid authored values for validation.
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (generationSettings == null)
            generationSettings = new GameProceduralLevelGenerationSettings();

        if (transitionSettings == null)
            transitionSettings = new GameProceduralLevelTransitionSettings();

        if (levels == null)
            levels = new List<GameProceduralLevelDefinition>();

        if (roomMetadata == null)
            roomMetadata = new List<GameRoomSceneMetadata>();

        // Keep null level entries available to the validator while initializing valid definitions.
        for (int index = 0; index < levels.Count; index++)
        {
            GameProceduralLevelDefinition level = levels[index];

            if (level == null)
                continue;

            level.EnsureInitialized();
        }
    }

    /// <summary>
    /// Generates new preset, level and tile technical identifiers after this asset is duplicated.
    /// </summary>
    public void RegenerateTechnicalIds()
    {
        presetId = Guid.NewGuid().ToString("N");

        if (levels == null)
            return;

        // Detach all duplicated editor identities while preserving -facing IDs and scene metadata.
        for (int index = 0; index < levels.Count; index++)
        {
            GameProceduralLevelDefinition level = levels[index];

            if (level == null)
                continue;

            level.RegenerateTechnicalIds();
        }
    }

    /// <summary>
    /// Finds one level by its stable -authored level ID.
    /// </summary>
    /// <param name="levelId">Level ID to find.</param>
    /// <param name="level">Matching level definition when available.</param>
    /// <returns>True when a matching level definition exists.</returns>
    public bool TryFindLevel(string levelId, out GameProceduralLevelDefinition level)
    {
        level = null;

        if (string.IsNullOrWhiteSpace(levelId) || levels == null)
            return false;

        // Resolve by exact ordinal ID so editor and runtime use identical reference semantics.
        for (int index = 0; index < levels.Count; index++)
        {
            GameProceduralLevelDefinition candidate = levels[index];

            if (candidate == null)
                continue;

            if (!string.Equals(candidate.LevelId, levelId, StringComparison.Ordinal))
                continue;

            level = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds cached room metadata by the canonical Scene Manager scene ID.
    /// </summary>
    /// <param name="sceneId">Scene ID whose cached room metadata is required.</param>
    /// <param name="metadata">Matching metadata snapshot when available.</param>
    /// <returns>True when a matching room metadata snapshot exists.</returns>
    public bool TryFindRoomMetadata(string sceneId, out GameRoomSceneMetadata metadata)
    {
        metadata = null;

        if (string.IsNullOrWhiteSpace(sceneId) || roomMetadata == null)
            return false;

        // Resolve the deduplicated scene snapshot shared by every tile referencing this scene ID.
        for (int index = 0; index < roomMetadata.Count; index++)
        {
            GameRoomSceneMetadata candidate = roomMetadata[index];

            if (candidate == null)
                continue;

            if (!string.Equals(candidate.SceneId, sceneId, StringComparison.Ordinal))
                continue;

            metadata = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
