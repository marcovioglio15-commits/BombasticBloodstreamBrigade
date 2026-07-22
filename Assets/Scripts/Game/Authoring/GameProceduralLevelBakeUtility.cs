using System.Text;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Converts Procedural Level preset definitions and cached room metadata into flattened ECS configuration buffers.
/// </summary>
public static class GameProceduralLevelBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Verifies the complete authored configuration and the exact runtime Scene Manager catalog before any flattened
    /// fixed-string data is created. Invalid values remain unchanged and are returned as actionable diagnostics.
    /// </summary>
    /// <param name="preset">Procedural preset requested by the active Game Master.</param>
    /// <param name="runtimeSceneCatalog">Effective Scene Manager preset used for runtime loading.</param>
    /// <param name="failureMessage">First validation error suitable for a Baker or bootstrap log.</param>
    /// <returns>True when every bake constraint is valid and both systems use the same scene catalog.</returns>
    public static bool TryValidateRuntimeConfiguration(GameProceduralLevelPreset preset,
                                                       GameSceneManagerPreset runtimeSceneCatalog,
                                                       out string failureMessage)
    {
        GameProceduralLevelValidationReport report = GameProceduralLevelRuntimeValidationUtility.ValidateCompatibility(preset,
                                                                                                                        runtimeSceneCatalog);

        if (report.IsValid)
        {
            failureMessage = string.Empty;
            return true;
        }

        // Return the first error in deterministic validation order without altering the offending authoring value.
        for (int diagnosticIndex = 0; diagnosticIndex < report.Diagnostics.Count; diagnosticIndex++)
        {
            GameProceduralLevelValidationDiagnostic diagnostic = report.Diagnostics[diagnosticIndex];

            if (diagnostic.Severity != GameProceduralLevelValidationSeverity.Error)
                continue;

            failureMessage = diagnostic.Context + ": " + diagnostic.Message;
            return false;
        }

        failureMessage = "Procedural Level validation failed without an error diagnostic.";
        return false;
    }

    /// <summary>
    /// Builds immutable global runtime settings from one Procedural Level preset.
    /// </summary>
    /// <param name="preset">Source Procedural Level preset.</param>
    /// <returns>Baked procedural level configuration component.</returns>
    public static GameProceduralLevelConfig BuildConfig(GameProceduralLevelPreset preset)
    {
        GameProceduralLevelGenerationSettings generationSettings = preset != null ? preset.GenerationSettings : null;
        GameProceduralLevelTransitionSettings transitionSettings = preset != null ? preset.TransitionSettings : null;

        return new GameProceduralLevelConfig
        {
            PresetId = preset != null ? BuildFixedString64(preset.PresetId) : default,
            SeedMode = generationSettings != null ? generationSettings.SeedMode : GameProceduralLevelSeedMode.RandomPerRun,
            FixedSeed = generationSettings != null ? generationSettings.FixedSeed : 1u,
            MaximumNodeCount = generationSettings != null ? generationSettings.MaximumNodeCount : 128,
            MaximumDepth = generationSettings != null ? generationSettings.MaximumDepth : 64,
            MaximumGenerationAttempts = generationSettings != null ? generationSettings.MaximumGenerationAttempts : 128,
            RoomStreamingMode = transitionSettings != null
                ? transitionSettings.RoomStreamingMode
                : GameProceduralRoomStreamingMode.AuthoredSingleSlot,
            AdjacentPreloadPolicy = transitionSettings != null
                ? transitionSettings.AdjacentPreloadPolicy
                : GameProceduralAdjacentPreloadPolicy.Disabled,
            MaximumStagedRooms = transitionSettings != null ? transitionSettings.MaximumStagedRooms : 0,
            RequireReadyBeforePortalCommit = transitionSettings == null || transitionSettings.RequireReadyBeforePortalCommit ? (byte)1 : (byte)0,
            RetiredRoomBudget = transitionSettings != null ? transitionSettings.RetiredRoomBudget : 0,
            RetirementWorkBudgetMilliseconds = transitionSettings != null ? transitionSettings.RetirementWorkBudgetMilliseconds : 1.5f,
            KeepPlayerVisible = transitionSettings != null && transitionSettings.KeepPlayerVisible ? (byte)1 : (byte)0,
            HideLoadingProgressDuringRoomTransitions = transitionSettings != null && transitionSettings.HideLoadingProgressDuringRoomTransitions ? (byte)1 : (byte)0,
            HasPlayerTransitionAnimation = transitionSettings != null && transitionSettings.PlayerTransitionAnimation != null ? (byte)1 : (byte)0,
            PlayerTransitionAnimation = transitionSettings != null ? transitionSettings.PlayerTransitionAnimation : null,
            RelocationNormalizedTime = transitionSettings != null ? transitionSettings.RelocationNormalizedTime : 0.5f,
            ClearPlayerVelocity = transitionSettings == null || transitionSettings.ClearPlayerVelocity ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Populates flattened level and room tile buffers while preserving authored ordering and invalid values for diagnostics.
    /// </summary>
    /// <param name="preset">Source Procedural Level preset.</param>
    /// <param name="levelBuffer">Output ordered level definition buffer.</param>
    /// <param name="tileBuffer">Output flattened room tile buffer.</param>
    public static void PopulateLevelBuffers(GameProceduralLevelPreset preset,
                                            DynamicBuffer<GameProceduralLevelDefinitionElement> levelBuffer,
                                            DynamicBuffer<GameProceduralRoomTileElement> tileBuffer)
    {
        levelBuffer.Clear();
        tileBuffer.Clear();

        if (preset == null || preset.Levels == null)
            return;

        // Flatten each level's tile range once so runtime generation avoids nested managed collections.
        for (int levelIndex = 0; levelIndex < preset.Levels.Count; levelIndex++)
        {
            GameProceduralLevelDefinition level = preset.Levels[levelIndex];

            if (level == null)
                continue;

            int tileStartIndex = tileBuffer.Length;
            PopulateTiles(preset, level, levelIndex, tileBuffer);
            GameProceduralLevelRuleSettings rules = level.RuleSettings;
            levelBuffer.Add(new GameProceduralLevelDefinitionElement
            {
                TechnicalId = BuildFixedString64(level.TechnicalId),
                LevelId = BuildFixedString64(level.LevelId),
                DisplayName = BuildFixedString128(level.DisplayName),
                OrderIndex = levelIndex,
                TileStartIndex = tileStartIndex,
                TileCount = tileBuffer.Length - tileStartIndex,
                TargetNodeCountMinimum = level.TargetNodeCountRange.x,
                TargetNodeCountMaximum = level.TargetNodeCountRange.y,
                PreferredBossDepthMinimum = level.PreferredBossDepthRange.x,
                PreferredBossDepthMaximum = level.PreferredBossDepthRange.y,
                RoomDepthScore = rules != null ? rules.RoomDepthScore : 0f,
                BossDepthScore = rules != null ? rules.BossDepthScore : 0f,
                FittingScore = rules != null ? rules.FittingScore : 0f,
                Enabled = level.Enabled ? (byte)1 : (byte)0,
                RequireRoomClearBeforeExit = level.RequireRoomClearBeforeExit ? (byte)1 : (byte)0,
                UseCenterArrival = level.UseCenterArrival ? (byte)1 : (byte)0,
                RequiresLevelExit = GameProceduralLevelValidator.ResolveRequiresLevelExit(preset, level) ? (byte)1 : (byte)0
            });
        }
    }

    /// <summary>
    /// Populates deduplicated room metadata and individual portal signature buffers.
    /// </summary>
    /// <param name="preset">Source Procedural Level preset.</param>
    /// <param name="metadataBuffer">Output room scene metadata buffer.</param>
    /// <param name="portalBuffer">Output flattened portal signature buffer.</param>
    public static void PopulateMetadataBuffers(GameProceduralLevelPreset preset,
                                               DynamicBuffer<GameProceduralRoomMetadataElement> metadataBuffer,
                                               DynamicBuffer<GameProceduralRoomPortalDefinitionElement> portalBuffer)
    {
        metadataBuffer.Clear();
        portalBuffer.Clear();

        if (preset == null || preset.RoomMetadata == null)
            return;

        // Preserve every individual same-side portal so multiplicity remains available to the solver.
        for (int metadataIndex = 0; metadataIndex < preset.RoomMetadata.Count; metadataIndex++)
        {
            GameRoomSceneMetadata metadata = preset.RoomMetadata[metadataIndex];

            if (metadata == null)
            {
                metadataBuffer.Add(default);
                continue;
            }

            int portalStartIndex = portalBuffer.Length;

            for (int portalIndex = 0; portalIndex < metadata.Portals.Count; portalIndex++)
            {
                GameRoomPortalMetadata portal = metadata.Portals[portalIndex];

                if (portal == null)
                    continue;

                portalBuffer.Add(new GameProceduralRoomPortalDefinitionElement
                {
                    PortalId = BuildFixedString64(portal.PortalId),
                    Side = portal.Side,
                    Capability = portal.Capability,
                    ConnectionPolicy = portal.ConnectionPolicy,
                    MetadataIndex = metadataIndex
                });
            }

            metadataBuffer.Add(new GameProceduralRoomMetadataElement
            {
                SceneId = BuildFixedString64(metadata.SceneId),
                SceneGuid = BuildFixedString64(metadata.SceneGuid),
                DependencyHash = BuildFixedString128(metadata.DependencyHash),
                PortalStartIndex = portalStartIndex,
                PortalCount = portalBuffer.Length - portalStartIndex,
                CenterAnchorCount = metadata.CenterAnchorCount,
                CacheStale = metadata.CacheStale ? (byte)1 : (byte)0
            });
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends every non-null room tile owned by one level to the shared flattened tile buffer.
    /// </summary>
    /// <param name="preset">Preset used to resolve cached metadata indices.</param>
    /// <param name="level">Level whose room tiles are flattened.</param>
    /// <param name="levelIndex">Authored ordered level index.</param>
    /// <param name="tileBuffer">Output flattened tile buffer.</param>
    private static void PopulateTiles(GameProceduralLevelPreset preset,
                                      GameProceduralLevelDefinition level,
                                      int levelIndex,
                                      DynamicBuffer<GameProceduralRoomTileElement> tileBuffer)
    {
        for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];

            if (tile == null)
                continue;

            tileBuffer.Add(new GameProceduralRoomTileElement
            {
                TechnicalId = BuildFixedString64(tile.TechnicalId),
                TileId = BuildFixedString64(tile.TileId),
                SceneId = BuildFixedString64(tile.SceneId),
                SceneGuid = BuildFixedString64(tile.SceneGuid),
                Role = tile.Role,
                LevelIndex = levelIndex,
                MetadataIndex = ResolveMetadataIndex(preset, tile.SceneId),
                MaximumCopies = tile.MaximumCopies,
                PreferredDepthMinimum = tile.PreferredDepthRange.x,
                PreferredDepthMaximum = tile.PreferredDepthRange.y,
                UseExactDepthConstraint = tile.UseExactDepthConstraint ? (byte)1 : (byte)0,
                ExactDepth = tile.ExactDepth,
                BaseSelectionWeight = tile.BaseSelectionWeight
            });
        }
    }

    /// <summary>
    /// Resolves the authored metadata index associated with one canonical scene ID.
    /// </summary>
    /// <param name="preset">Preset containing the deduplicated room metadata list.</param>
    /// <param name="sceneId">Canonical room scene ID to resolve.</param>
    /// <returns>Metadata list index, or -1 when no snapshot exists.</returns>
    private static int ResolveMetadataIndex(GameProceduralLevelPreset preset, string sceneId)
    {
        if (preset == null || string.IsNullOrWhiteSpace(sceneId))
            return -1;

        for (int index = 0; index < preset.RoomMetadata.Count; index++)
        {
            GameRoomSceneMetadata metadata = preset.RoomMetadata[index];

            if (metadata == null)
                continue;

            if (string.Equals(metadata.SceneId, sceneId, System.StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Creates one fixed runtime identifier only when its authored UTF-8 payload fits exactly. Production call sites
    /// validate first; the empty fallback prevents an exception if a future caller bypasses that mandatory guard.
    /// </summary>
    /// <param name="value">Authored identifier or scene GUID.</param>
    /// <returns>Authored value when it fits, otherwise an empty fail-closed identifier without truncation.</returns>
    private static FixedString64Bytes BuildFixedString64(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) > FixedString64Bytes.UTF8MaxLengthInBytes)
        {
            return default;
        }

        return new FixedString64Bytes(value);
    }

    /// <summary>
    /// Creates one fixed runtime label or dependency hash only when its authored UTF-8 payload fits exactly. The
    /// fallback is empty rather than truncated so invalid content cannot silently acquire a different identity.
    /// </summary>
    /// <param name="value">Authored display label or dependency hash.</param>
    /// <returns>Authored value when it fits, otherwise an empty fail-closed value without truncation.</returns>
    private static FixedString128Bytes BuildFixedString128(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) > FixedString128Bytes.UTF8MaxLengthInBytes)
        {
            return default;
        }

        return new FixedString128Bytes(value);
    }
    #endregion

    #endregion
}
