using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Produces non-mutating warnings for room metadata freshness, catalog identity and arrival-mode requirements.
/// </summary>
public static class GameRoomMetadataCacheValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds designer-facing warnings without initializing, sanitizing or rewriting any preset value.
    /// </summary>
    /// <param name="preset">Procedural preset to validate.</param>
    /// <returns>Ordered warning list suitable for a tool HelpBox or build validation output.</returns>
    public static List<string> BuildWarnings(GameProceduralLevelPreset preset)
    {
        List<string> warnings = new List<string>();

        if (preset == null)
        {
            warnings.Add("A Procedural Level preset is required for room metadata validation.");
            return warnings;
        }

        if (preset.SceneCatalogPreset == null)
        {
            warnings.Add("The Procedural Level preset has no Scene Manager catalog reference.");
            return warnings;
        }

        ValidateMetadataEntries(preset, warnings);
        ValidateTileRequirements(preset, warnings);
        return warnings;
    }
    #endregion

    #region Metadata Validation
    /// <summary>
    /// Validates cached scene identity, source dependencies and scanner authoring warnings.
    /// </summary>
    /// <param name="preset">Procedural preset owning metadata entries.</param>
    /// <param name="warnings">Target warning list.</param>
    private static void ValidateMetadataEntries(GameProceduralLevelPreset preset, List<string> warnings)
    {
        HashSet<string> sceneIds = new HashSet<string>(StringComparer.Ordinal);

        if (preset.RoomMetadata == null)
            return;

        // Inspect every cached snapshot independently so duplicate and orphaned entries remain visible.
        for (int metadataIndex = 0; metadataIndex < preset.RoomMetadata.Count; metadataIndex++)
        {
            GameRoomSceneMetadata metadata = preset.RoomMetadata[metadataIndex];

            if (metadata == null)
            {
                warnings.Add("Room metadata cache contains a null entry at index " + metadataIndex + ".");
                continue;
            }

            string label = string.IsNullOrWhiteSpace(metadata.SceneId) ? "Room metadata [" + metadataIndex + "]" : "Room metadata '" + metadata.SceneId + "'";

            if (string.IsNullOrWhiteSpace(metadata.SceneId))
                warnings.Add(label + " has no canonical Scene ID.");
            else if (!sceneIds.Add(metadata.SceneId))
                warnings.Add(label + " duplicates another cached Scene ID.");

            ValidateCatalogIdentity(preset, metadata, label, warnings);
            ValidateFreshness(metadata, label, warnings);
            AppendAuthoringWarnings(metadata, label, warnings);
        }
    }

    /// <summary>
    /// Compares cached scene GUID identity with the active Scene Manager catalog entry.
    /// </summary>
    /// <param name="preset">Procedural preset supplying the scene catalog.</param>
    /// <param name="metadata">Cached room metadata entry.</param>
    /// <param name="label">Readable cache label.</param>
    /// <param name="warnings">Target warning list.</param>
    private static void ValidateCatalogIdentity(GameProceduralLevelPreset preset,
                                                GameRoomSceneMetadata metadata,
                                                string label,
                                                List<string> warnings)
    {
        GameSceneDefinition sceneDefinition;

        if (!preset.SceneCatalogPreset.TryFindScene(metadata.SceneId, out sceneDefinition) || sceneDefinition == null)
        {
            warnings.Add(label + " is orphaned because its Scene ID is missing from the Scene Manager catalog.");
            return;
        }

        string currentGuid = AssetDatabase.AssetPathToGUID(sceneDefinition.ScenePath);

        if (!string.Equals(sceneDefinition.SceneGuid, currentGuid, StringComparison.Ordinal))
            warnings.Add(label + " uses a Scene Manager catalog entry whose cached GUID does not match its scene path.");

        if (!string.Equals(metadata.SceneGuid, currentGuid, StringComparison.Ordinal))
            warnings.Add(label + " references a different scene GUID than the current Scene Manager catalog entry.");
    }

    /// <summary>
    /// Checks the explicit stale marker and recomputes the aggregate dependency hash without opening scenes.
    /// </summary>
    /// <param name="metadata">Cached room metadata entry.</param>
    /// <param name="label">Readable cache label.</param>
    /// <param name="warnings">Target warning list.</param>
    private static void ValidateFreshness(GameRoomSceneMetadata metadata,
                                          string label,
                                          List<string> warnings)
    {
        if (metadata.CacheStale)
            warnings.Add(label + " is stale and queued for automatic refresh. Use Refresh Room Metadata only to retry immediately.");

        if (metadata.SourceScenePaths == null || metadata.SourceScenePaths.Count == 0)
        {
            warnings.Add(label + " has no root or nested source scene paths.");
            return;
        }

        if (metadata.CacheStale)
            return;

        string currentHash = GameRoomMetadataDependencyUtility.ComputeCombinedDependencyHash(metadata.SourceScenePaths);

        if (string.IsNullOrWhiteSpace(metadata.DependencyHash))
            warnings.Add(label + " has no dependency hash and must be refreshed.");
        else if (!string.Equals(metadata.DependencyHash, currentHash, StringComparison.Ordinal))
            warnings.Add(label + " dependency hash no longer matches its root scene or nested SubScenes.");
    }

    /// <summary>
    /// Appends warnings captured while authoring components were last scanned.
    /// </summary>
    /// <param name="metadata">Cached room metadata entry.</param>
    /// <param name="label">Readable cache label.</param>
    /// <param name="warnings">Target warning list.</param>
    private static void AppendAuthoringWarnings(GameRoomSceneMetadata metadata,
                                                string label,
                                                List<string> warnings)
    {
        if (metadata.AuthoringWarnings == null)
            return;

        for (int warningIndex = 0; warningIndex < metadata.AuthoringWarnings.Count; warningIndex++)
        {
            string warning = metadata.AuthoringWarnings[warningIndex];

            if (!string.IsNullOrWhiteSpace(warning))
                warnings.Add(label + ": " + warning);
        }
    }
    #endregion

    #region Tile Validation
    /// <summary>
    /// Validates center-arrival and portal-mode metadata requirements for every referenced room tile.
    /// </summary>
    /// <param name="preset">Procedural preset to inspect.</param>
    /// <param name="warnings">Target warning list.</param>
    private static void ValidateTileRequirements(GameProceduralLevelPreset preset, List<string> warnings)
    {
        if (preset.Levels == null)
            return;

        // Evaluate requirements per level because the same room may be reused with different arrival modes.
        for (int levelIndex = 0; levelIndex < preset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = preset.Levels[levelIndex];

            if (level == null || level.RoomTiles == null)
                continue;

            for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
                ValidateTileRequirement(preset, level, level.RoomTiles[tileIndex], warnings);
        }
    }

    /// <summary>
    /// Checks one tile against the arrival mode selected by its containing level.
    /// </summary>
    /// <param name="preset">Procedural preset owning cached metadata.</param>
    /// <param name="level">Containing level definition.</param>
    /// <param name="tile">Room tile to validate.</param>
    /// <param name="warnings">Target warning list.</param>
    private static void ValidateTileRequirement(GameProceduralLevelPreset preset,
                                                GameProceduralLevelDefinition level,
                                                GameProceduralRoomTileDefinition tile,
                                                List<string> warnings)
    {
        if (tile == null || string.IsNullOrWhiteSpace(tile.SceneId))
            return;

        GameRoomSceneMetadata metadata;
        string tileLabel = "Level '" + level.LevelId + "', tile '" + tile.TileId + "'";

        if (!preset.TryFindRoomMetadata(tile.SceneId, out metadata) || metadata == null)
        {
            warnings.Add(tileLabel + " has no cached room metadata for Scene ID '" + tile.SceneId + "'.");
            return;
        }

        if (level.UseCenterArrival)
        {
            if (metadata.CenterAnchorCount != 1)
                warnings.Add(tileLabel + " uses center arrival but its room contains " + metadata.CenterAnchorCount + " center anchors; exactly one is required.");

            return;
        }

        if (metadata.Portals == null || metadata.Portals.Count == 0)
            warnings.Add(tileLabel + " uses portal arrival but its room metadata contains no portals.");
    }
    #endregion

    #endregion
}
