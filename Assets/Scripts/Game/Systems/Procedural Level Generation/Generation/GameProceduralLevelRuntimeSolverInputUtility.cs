using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Adapts flattened baked ECS configuration into the immutable managed request shared with editor preview generation.
/// </summary>
internal static class GameProceduralLevelRuntimeSolverInputUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one solver input from a level's contiguous tile range and deduplicated metadata buffers.
    /// </summary>
    /// <param name="config">Global generation limits baked from the active preset.</param>
    /// <param name="level">Ordered level definition to generate.</param>
    /// <param name="tiles">Flattened room tile buffer.</param>
    /// <param name="metadata">Deduplicated room scene metadata buffer.</param>
    /// <param name="portals">Flattened physical portal signature buffer.</param>
    /// <param name="input">Created immutable solver input when all ranges are valid.</param>
    /// <param name="diagnostic">Actionable failure message when baked ranges are inconsistent.</param>
    /// <returns>True when runtime buffers produced a complete solver input.</returns>
    public static bool TryBuild(GameProceduralLevelConfig config,
                                GameProceduralLevelDefinitionElement level,
                                DynamicBuffer<GameProceduralRoomTileElement> tiles,
                                DynamicBuffer<GameProceduralRoomMetadataElement> metadata,
                                DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portals,
                                out GameProceduralLevelSolverInput input,
                                out string diagnostic)
    {
        input = null;
        diagnostic = string.Empty;

        if (level.TileStartIndex < 0 ||
            level.TileCount <= 0 ||
            level.TileStartIndex + level.TileCount > tiles.Length)
        {
            diagnostic = "The baked room tile range is invalid for level '" + level.LevelId.ToString() + "'.";
            return false;
        }

        List<GameProceduralRoomTileSolverInput> tileInputs = new List<GameProceduralRoomTileSolverInput>(level.TileCount);

        // Expand shared metadata only during bounded graph generation, never during per-frame traversal.
        for (int tileOffset = 0; tileOffset < level.TileCount; tileOffset++)
        {
            GameProceduralRoomTileElement tile = tiles[level.TileStartIndex + tileOffset];

            if (!TryBuildTile(tile, metadata, portals, out GameProceduralRoomTileSolverInput tileInput, out diagnostic))
                return false;

            tileInputs.Add(tileInput);
        }

        input = new GameProceduralLevelSolverInput(level.TechnicalId.ToString(),
                                                   level.LevelId.ToString(),
                                                   new Vector2Int(level.TargetNodeCountMinimum, level.TargetNodeCountMaximum),
                                                   new Vector2Int(level.PreferredBossDepthMinimum, level.PreferredBossDepthMaximum),
                                                   level.RoomDepthScore,
                                                   level.BossDepthScore,
                                                   level.FittingScore,
                                                   level.UseCenterArrival != 0,
                                                   level.RequiresLevelExit != 0,
                                                   config.MaximumNodeCount,
                                                   config.MaximumDepth,
                                                   config.MaximumGenerationAttempts,
                                                   tileInputs);
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one tile solver input and copies its contiguous individual portal signatures.
    /// </summary>
    /// <param name="tile">Flattened reusable room tile.</param>
    /// <param name="metadata">Deduplicated metadata buffer.</param>
    /// <param name="portals">Flattened portal signature buffer.</param>
    /// <param name="tileInput">Created immutable tile input.</param>
    /// <param name="diagnostic">Actionable range failure message.</param>
    /// <returns>True when the referenced metadata and portal ranges are valid.</returns>
    private static bool TryBuildTile(GameProceduralRoomTileElement tile,
                                     DynamicBuffer<GameProceduralRoomMetadataElement> metadata,
                                     DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portals,
                                     out GameProceduralRoomTileSolverInput tileInput,
                                     out string diagnostic)
    {
        tileInput = null;
        diagnostic = string.Empty;

        if (tile.MetadataIndex < 0 || tile.MetadataIndex >= metadata.Length)
        {
            diagnostic = "Room tile '" + tile.TileId.ToString() + "' has no valid baked metadata reference.";
            return false;
        }

        GameProceduralRoomMetadataElement roomMetadata = metadata[tile.MetadataIndex];

        if (!roomMetadata.SceneId.Equals(tile.SceneId))
        {
            diagnostic = "Room tile '" + tile.TileId.ToString() + "' references missing or mismatched baked scene metadata.";
            return false;
        }

        if (roomMetadata.CacheStale != 0)
        {
            diagnostic = "Room tile '" + tile.TileId.ToString() + "' references a stale metadata cache. Refresh the room from the Game Management Tool.";
            return false;
        }

        if (roomMetadata.PortalStartIndex < 0 ||
            roomMetadata.PortalCount < 0 ||
            roomMetadata.PortalStartIndex + roomMetadata.PortalCount > portals.Length)
        {
            diagnostic = "Room tile '" + tile.TileId.ToString() + "' references an invalid baked portal range.";
            return false;
        }

        List<GameProceduralRoomPortalSolverInput> portalInputs = new List<GameProceduralRoomPortalSolverInput>(roomMetadata.PortalCount);

        for (int portalOffset = 0; portalOffset < roomMetadata.PortalCount; portalOffset++)
        {
            GameProceduralRoomPortalDefinitionElement portal = portals[roomMetadata.PortalStartIndex + portalOffset];
            portalInputs.Add(new GameProceduralRoomPortalSolverInput(portal.PortalId.ToString(),
                                                                     portal.Side,
                                                                     portal.Capability,
                                                                     portal.ConnectionPolicy));
        }

        tileInput = new GameProceduralRoomTileSolverInput(tile.TechnicalId.ToString(),
                                                          tile.TileId.ToString(),
                                                          tile.SceneId.ToString(),
                                                          tile.Role,
                                                          tile.MaximumCopies,
                                                          new Vector2Int(tile.PreferredDepthMinimum, tile.PreferredDepthMaximum),
                                                          tile.BaseSelectionWeight,
                                                          roomMetadata.CenterAnchorCount,
                                                          portalInputs,
                                                          tile.UseExactDepthConstraint != 0,
                                                          tile.ExactDepth);
        return true;
    }
    #endregion

    #endregion
}
