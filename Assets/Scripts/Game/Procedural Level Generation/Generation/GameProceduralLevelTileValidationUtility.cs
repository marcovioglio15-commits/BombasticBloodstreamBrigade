using System;
using System.Collections.Generic;

/// <summary>
/// Validates reusable room tiles, scene references and cached portal signatures for the public preset validator.
/// </summary>
internal static class GameProceduralLevelTileValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one tile's identity, tuning, scene reference and cached room signature.
    /// </summary>
    /// <param name="preset">Owning preset.</param>
    /// <param name="level">Owning level.</param>
    /// <param name="tile">Tile to inspect.</param>
    /// <param name="technicalIds">Tile technical IDs already encountered.</param>
    /// <param name="tileIds">Designer-facing tile IDs already encountered.</param>
    /// <param name="validatedMetadataScenes">Scene metadata records already structurally inspected.</param>
    /// <param name="requiresLevelExit">Whether this level's Boss must expose progression to a later enabled level.</param>
    /// <param name="report">Destination report.</param>
    public static void ValidateTile(GameProceduralLevelPreset preset,
                                    GameProceduralLevelDefinition level,
                                    GameProceduralRoomTileDefinition tile,
                                    HashSet<string> technicalIds,
                                    HashSet<string> tileIds,
                                    HashSet<string> validatedMetadataScenes,
                                    bool requiresLevelExit,
                                    GameProceduralLevelValidationReport report)
    {
        string context = GameProceduralLevelValidator.ResolveLevelContext(level) + "/" + ResolveTileContext(tile);
        ValidateTileIdentity(tile, context, technicalIds, tileIds, report);

        if (tile.MaximumCopies <= 0)
            report.Add(GameProceduralLevelValidationCode.InvalidMaximumCopies,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Maximum Copies must be greater than zero.");

        if (tile.PreferredDepthRange.x < 0 || tile.PreferredDepthRange.y < tile.PreferredDepthRange.x)
            report.Add(GameProceduralLevelValidationCode.InvalidPreferredDepthRange,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Preferred Depth Range must be ordered and non-negative.");

        ValidateExactDepth(preset, tile, context, report);

        if (float.IsNaN(tile.BaseSelectionWeight) ||
            float.IsInfinity(tile.BaseSelectionWeight) ||
            tile.BaseSelectionWeight <= 0f)
            report.Add(GameProceduralLevelValidationCode.InvalidBaseSelectionWeight,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Base Selection Weight must be finite and greater than zero.");

        ValidateSceneReference(preset, tile, context, report);

        if (!preset.TryFindRoomMetadata(tile.SceneId, out GameRoomSceneMetadata metadata))
        {
            report.Add(GameProceduralLevelValidationCode.MissingRoomMetadata,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Refresh the room metadata cache for the selected scene.");
            return;
        }

        ValidateMetadataState(level, tile, metadata, context, report);

        if (validatedMetadataScenes.Add(metadata.SceneId))
            ValidatePortalMetadata(metadata, context, report);

        ValidateTilePortalRole(level, tile, metadata, context, requiresLevelExit, report);
    }

    /// <summary>
    /// Verifies every required source side has at least one opposite-side Regular or Boss entrance candidate.
    /// </summary>
    /// <param name="preset">Preset containing cached room signatures.</param>
    /// <param name="level">Level whose fitting feasibility is inspected.</param>
    /// <param name="report">Destination report.</param>
    public static void ValidateRequiredExitCompatibility(GameProceduralLevelPreset preset,
                                                         GameProceduralLevelDefinition level,
                                                         GameProceduralLevelValidationReport report)
    {
        for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileDefinition sourceTile = level.RoomTiles[tileIndex];

            if (sourceTile == null || sourceTile.Role == GameProceduralRoomRole.Boss)
                continue;

            if (!preset.TryFindRoomMetadata(sourceTile.SceneId, out GameRoomSceneMetadata sourceMetadata))
                continue;

            for (int portalIndex = 0; portalIndex < sourceMetadata.Portals.Count; portalIndex++)
            {
                GameRoomPortalMetadata portal = sourceMetadata.Portals[portalIndex];

                if (portal == null || portal.ConnectionPolicy != GameRoomPortalConnectionPolicy.Required)
                    continue;

                if (portal.Capability == GameRoomPortalCapability.Entrance)
                    continue;

                if (HasCompatibleTarget(preset, level, portal.Side))
                    continue;

                report.Add(GameProceduralLevelValidationCode.RequiredExitHasNoCompatibleTile,
                           GameProceduralLevelValidationSeverity.Error,
                           GameProceduralLevelValidator.ResolveLevelContext(level) + "/" +
                           ResolveTileContext(sourceTile) + "/" + portal.PortalId,
                           "No Regular or Boss tile provides an unused-capable entrance on the opposite side.");
            }
        }
    }
    #endregion

    #region Tile Methods
    /// <summary>
    /// Validates the optional hard depth constraint without rewriting the authored depth or preferred scoring range.
    /// </summary>
    /// <param name="preset">Owning preset supplying the global technical depth limit.</param>
    /// <param name="tile">Tile whose exact placement constraint is inspected.</param>
    /// <param name="context">Designer-facing tile context.</param>
    /// <param name="report">Destination validation report.</param>
    private static void ValidateExactDepth(GameProceduralLevelPreset preset,
                                           GameProceduralRoomTileDefinition tile,
                                           string context,
                                           GameProceduralLevelValidationReport report)
    {
        if (!tile.UseExactDepthConstraint)
            return;

        if (tile.ExactDepth < 0)
        {
            report.Add(GameProceduralLevelValidationCode.InvalidExactDepth,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Exact Depth must be non-negative when the hard constraint is enabled.");
            return;
        }

        GameProceduralLevelGenerationSettings generationSettings = preset != null ? preset.GenerationSettings : null;

        if (generationSettings != null && tile.ExactDepth > generationSettings.MaximumDepth)
            report.Add(GameProceduralLevelValidationCode.ExactDepthExceedsLimit,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Exact Depth exceeds the global Maximum Depth and can never produce a node.");

        if (tile.Role == GameProceduralRoomRole.Start && tile.ExactDepth != 0)
            report.Add(GameProceduralLevelValidationCode.StartExactDepthMismatch,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The Start tile is structurally fixed at depth zero, so its Exact Depth must also be zero.");

        if (tile.Role != GameProceduralRoomRole.Start && tile.ExactDepth < 1)
            report.Add(GameProceduralLevelValidationCode.NonStartExactDepthMismatch,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Regular and Boss tiles cannot occupy the Start node at depth zero, so their Exact Depth must be at least one.");

        if (tile.ExactDepth < tile.PreferredDepthRange.x || tile.ExactDepth > tile.PreferredDepthRange.y)
            report.Add(GameProceduralLevelValidationCode.ExactDepthOutsidePreferredRange,
                       GameProceduralLevelValidationSeverity.Warning,
                       context,
                       "Exact Depth lies outside Preferred Depth Range. The hard constraint wins, so the preferred range is ignored while enabled.");
    }

    /// <summary>
    /// Validates tile IDs stored in fixed runtime buffers and uniqueness within the level.
    /// </summary>
    /// <param name="tile">Tile whose identity is inspected.</param>
    /// <param name="context">Designer-facing tile context.</param>
    /// <param name="technicalIds">Technical IDs already encountered.</param>
    /// <param name="tileIds">Designer-facing IDs already encountered.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateTileIdentity(GameProceduralRoomTileDefinition tile,
                                             string context,
                                             HashSet<string> technicalIds,
                                             HashSet<string> tileIds,
                                             GameProceduralLevelValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(tile.TechnicalId))
            report.Add(GameProceduralLevelValidationCode.MissingTileTechnicalId,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The tile requires an immutable technical ID.");
        else
        {
            GameProceduralLevelValidator.ValidateFixedString64(tile.TechnicalId, context + " technical ID", report);

            if (!technicalIds.Add(tile.TechnicalId))
                report.Add(GameProceduralLevelValidationCode.DuplicateTileTechnicalId,
                           GameProceduralLevelValidationSeverity.Error,
                           context,
                           "Another tile in this level uses the same technical ID.");
        }

        if (string.IsNullOrWhiteSpace(tile.TileId))
            report.Add(GameProceduralLevelValidationCode.MissingTileId,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Enter a designer-facing Tile ID.");
        else
        {
            GameProceduralLevelValidator.ValidateFixedString64(tile.TileId, context + " Tile ID", report);

            if (!tileIds.Add(tile.TileId))
                report.Add(GameProceduralLevelValidationCode.DuplicateTileId,
                           GameProceduralLevelValidationSeverity.Error,
                           context,
                           "Another tile in this level uses the same Tile ID.");
        }

        if (string.IsNullOrWhiteSpace(tile.SceneId))
            report.Add(GameProceduralLevelValidationCode.MissingTileSceneId,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Select a room scene from the Scene Manager catalog.");
        else
            GameProceduralLevelValidator.ValidateFixedString64(tile.SceneId, context + " Scene ID", report);

        GameProceduralLevelValidator.ValidateFixedString64(tile.SceneGuid,
                                                           context + " Scene GUID",
                                                           report);
    }

    /// <summary>
    /// Validates that a tile references a gameplay scene loadable by the configured Scene Manager backend.
    /// </summary>
    /// <param name="preset">Owning preset.</param>
    /// <param name="tile">Tile whose scene is resolved.</param>
    /// <param name="context">Designer-facing tile context.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateSceneReference(GameProceduralLevelPreset preset,
                                               GameProceduralRoomTileDefinition tile,
                                               string context,
                                               GameProceduralLevelValidationReport report)
    {
        GameSceneManagerPreset catalog = preset.SceneCatalogPreset;

        if (catalog == null || string.IsNullOrWhiteSpace(tile.SceneId))
            return;

        if (!catalog.TryFindScene(tile.SceneId, out GameSceneDefinition sceneDefinition))
        {
            report.Add(GameProceduralLevelValidationCode.MissingSceneDefinition,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The selected Scene ID is not present in the assigned Scene Manager preset.");
            return;
        }

        if (sceneDefinition.SceneKind != GameSceneKind.Gameplay)
            report.Add(GameProceduralLevelValidationCode.SceneIsNotGameplay,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Procedural room tiles must reference scenes with Gameplay kind.");

        if (sceneDefinition.UnloadPolicy != GameSceneUnloadPolicy.UnloadOnTransition)
            report.Add(GameProceduralLevelValidationCode.SceneUnloadPolicyInvalid,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Procedural room scenes must use Unload On Transition because traversal unloads each source room before loading its target.");

        bool loadable = catalog.LoadBackend == GameSceneLoadBackend.BuildSettings
            ? sceneDefinition.BuildIndex >= 0 && !string.IsNullOrWhiteSpace(sceneDefinition.ScenePath)
            : !string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey);

        if (!loadable)
            report.Add(GameProceduralLevelValidationCode.SceneIsNotLoadable,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The selected room is not loadable by the Scene Manager's active backend.");

        if (!string.IsNullOrWhiteSpace(tile.SceneGuid) &&
            !string.Equals(tile.SceneGuid, sceneDefinition.SceneGuid, StringComparison.Ordinal))
            report.Add(GameProceduralLevelValidationCode.SceneGuidMismatch,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The tile's cached scene GUID no longer matches the Scene Manager catalog.");
    }

    /// <summary>
    /// Validates cache freshness, scanner warnings and center-anchor requirements for one room scene.
    /// </summary>
    /// <param name="level">Owning level.</param>
    /// <param name="tile">Tile referencing the metadata.</param>
    /// <param name="metadata">Cached room metadata.</param>
    /// <param name="context">Designer-facing tile context.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateMetadataState(GameProceduralLevelDefinition level,
                                              GameProceduralRoomTileDefinition tile,
                                              GameRoomSceneMetadata metadata,
                                              string context,
                                              GameProceduralLevelValidationReport report)
    {
        if (!string.IsNullOrWhiteSpace(tile.SceneGuid) &&
            !string.Equals(tile.SceneGuid, metadata.SceneGuid, StringComparison.Ordinal))
            report.Add(GameProceduralLevelValidationCode.RoomMetadataGuidMismatch,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The cached room metadata belongs to a different scene GUID; refresh it.");

        if (metadata.CacheStale)
            report.Add(GameProceduralLevelValidationCode.RoomMetadataCacheStale,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The room or one of its nested SubScenes changed after scanning; refresh room metadata before preview or bake.");

        for (int warningIndex = 0; warningIndex < metadata.AuthoringWarnings.Count; warningIndex++)
        {
            string warning = metadata.AuthoringWarnings[warningIndex];

            if (!string.IsNullOrWhiteSpace(warning))
                report.Add(GameProceduralLevelValidationCode.RoomAuthoringWarning,
                           GameProceduralLevelValidationSeverity.Warning,
                           context,
                           warning);
        }

        if (!level.UseCenterArrival && tile.Role != GameProceduralRoomRole.Start)
            return;

        if (metadata.CenterAnchorCount < 1)
            report.Add(GameProceduralLevelValidationCode.MissingCenterAnchor,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       tile.Role == GameProceduralRoomRole.Start
                           ? "The Start room requires exactly one GameRoomCenterAnchorAuthoring because initial and level-boundary arrival always uses it."
                           : "Center-arrival mode requires exactly one GameRoomCenterAnchorAuthoring in this room.");
        else if (metadata.CenterAnchorCount > 1)
            report.Add(GameProceduralLevelValidationCode.DuplicateCenterAnchor,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       tile.Role == GameProceduralRoomRole.Start
                           ? "The Start room has multiple center anchors; retain exactly one for deterministic initial and level-boundary arrival."
                           : "Center-arrival mode found multiple center anchors; retain exactly one.");
    }
    #endregion

    #region Portal Methods
    /// <summary>
    /// Validates individual portal IDs inside one cached room signature.
    /// </summary>
    /// <param name="metadata">Room metadata to inspect.</param>
    /// <param name="context">Designer-facing room context.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidatePortalMetadata(GameRoomSceneMetadata metadata,
                                               string context,
                                               GameProceduralLevelValidationReport report)
    {
        HashSet<string> portalIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < metadata.Portals.Count; index++)
        {
            GameRoomPortalMetadata portal = metadata.Portals[index];

            if (portal == null)
            {
                report.Add(GameProceduralLevelValidationCode.NullPortal,
                           GameProceduralLevelValidationSeverity.Error,
                           context + "/Portal[" + index + "]",
                           "Refresh or remove the null cached portal entry.");
                continue;
            }

            string portalContext = context + "/" +
                                   (string.IsNullOrWhiteSpace(portal.PortalId) ? "Portal[" + index + "]" : portal.PortalId);

            if (string.IsNullOrWhiteSpace(portal.PortalId))
                report.Add(GameProceduralLevelValidationCode.MissingPortalId,
                           GameProceduralLevelValidationSeverity.Error,
                           portalContext,
                           "The authored portal requires a stable Portal ID.");
            else
            {
                GameProceduralLevelValidator.ValidateFixedString64(portal.PortalId, portalContext, report);

                if (!portalIds.Add(portal.PortalId))
                    report.Add(GameProceduralLevelValidationCode.DuplicatePortalId,
                               GameProceduralLevelValidationSeverity.Error,
                               portalContext,
                               "Another physical portal in this room uses the same Portal ID.");
            }
        }
    }

    /// <summary>
    /// Validates role-specific entrance, exit and LevelExit requirements for one reusable tile.
    /// </summary>
    /// <param name="level">Owning level.</param>
    /// <param name="tile">Tile being inspected.</param>
    /// <param name="metadata">Cached portal signature for the tile scene.</param>
    /// <param name="context">Designer-facing tile context.</param>
    /// <param name="requiresLevelExit">Whether this level's Boss must expose progression to a later enabled level.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateTilePortalRole(GameProceduralLevelDefinition level,
                                               GameProceduralRoomTileDefinition tile,
                                               GameRoomSceneMetadata metadata,
                                               string context,
                                               bool requiresLevelExit,
                                               GameProceduralLevelValidationReport report)
    {
        int usableExitCount = 0;
        int entranceCount = 0;
        int requiredRoomExitCount = 0;
        int usableLevelExitCount = 0;

        for (int index = 0; index < metadata.Portals.Count; index++)
        {
            GameRoomPortalMetadata portal = metadata.Portals[index];

            if (portal == null)
                continue;

            bool canExit = portal.Capability == GameRoomPortalCapability.Exit ||
                           portal.Capability == GameRoomPortalCapability.Both;
            bool canEnter = portal.Capability == GameRoomPortalCapability.Entrance ||
                            portal.Capability == GameRoomPortalCapability.Both;

            if (portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit &&
                tile.Role != GameProceduralRoomRole.Boss)
                report.Add(GameProceduralLevelValidationCode.InvalidLevelExitOwner,
                           GameProceduralLevelValidationSeverity.Error,
                           context + "/" + portal.PortalId,
                           "LevelExit portals are valid only on Boss room tiles.");

            if (canExit && portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
                usableLevelExitCount++;

            if (canExit && portal.ConnectionPolicy != GameRoomPortalConnectionPolicy.LevelExit)
            {
                usableExitCount++;

                if (portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.Required)
                    requiredRoomExitCount++;
            }

            if (canEnter && portal.ConnectionPolicy != GameRoomPortalConnectionPolicy.LevelExit)
                entranceCount++;
        }

        if (tile.Role != GameProceduralRoomRole.Boss && usableExitCount == 0)
            report.Add(GameProceduralLevelValidationCode.RoomHasNoUsableExit,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Start and Regular rooms require at least one Required or Optional exit portal.");

        if (!level.UseCenterArrival && tile.Role != GameProceduralRoomRole.Start && entranceCount == 0)
            report.Add(GameProceduralLevelValidationCode.RoomHasNoUsableEntrance,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Portal-arrival mode requires at least one Entrance or Both portal on every non-Start tile.");

        if (tile.Role == GameProceduralRoomRole.Boss && requiredRoomExitCount > 0)
            report.Add(GameProceduralLevelValidationCode.BossHasRequiredRoomExit,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The terminal Boss cannot own Required intra-level exits; use LevelExit for progression.");

        if (tile.Role == GameProceduralRoomRole.Boss &&
            requiresLevelExit &&
            usableLevelExitCount == 0)
            report.Add(GameProceduralLevelValidationCode.BossMissingLevelExit,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "This level precedes another enabled level, so its Boss requires at least one LevelExit portal with Exit or Both capability.");
    }

    /// <summary>
    /// Checks whether any non-Start tile can receive an exit emitted from one source side.
    /// </summary>
    /// <param name="preset">Preset containing room metadata.</param>
    /// <param name="level">Level containing candidate target tiles.</param>
    /// <param name="sourceSide">Source exit side requiring an opposite entrance.</param>
    /// <returns>True when at least one compatible target entrance exists.</returns>
    private static bool HasCompatibleTarget(GameProceduralLevelPreset preset,
                                            GameProceduralLevelDefinition level,
                                            GameRoomPortalSide sourceSide)
    {
        GameRoomPortalSide targetSide = GameProceduralLevelValidator.GetOppositeSide(sourceSide);

        for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileDefinition targetTile = level.RoomTiles[tileIndex];

            if (targetTile == null || targetTile.Role == GameProceduralRoomRole.Start)
                continue;

            if (!preset.TryFindRoomMetadata(targetTile.SceneId, out GameRoomSceneMetadata metadata))
                continue;

            for (int portalIndex = 0; portalIndex < metadata.Portals.Count; portalIndex++)
            {
                GameRoomPortalMetadata portal = metadata.Portals[portalIndex];

                if (portal == null || portal.Side != targetSide ||
                    portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit)
                    continue;

                if (portal.Capability != GameRoomPortalCapability.Exit)
                    return true;
            }
        }

        return false;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Resolves a stable diagnostic label for a room tile with incomplete identity fields.
    /// </summary>
    /// <param name="tile">Tile whose label is required.</param>
    /// <returns>Best available tile label.</returns>
    private static string ResolveTileContext(GameProceduralRoomTileDefinition tile)
    {
        if (!string.IsNullOrWhiteSpace(tile.TileId))
            return tile.TileId;

        if (!string.IsNullOrWhiteSpace(tile.SceneId))
            return tile.SceneId;

        return "Unnamed Tile";
    }
    #endregion

    #endregion
}
