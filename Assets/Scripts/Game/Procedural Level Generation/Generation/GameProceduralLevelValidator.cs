using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Performs non-mutating validation of procedural presets and individual levels for editor, bake and runtime guards.
/// </summary>
public static class GameProceduralLevelValidator
{
    #region Constants
    private const int FixedString64MaximumUtf8Bytes = 61;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates global settings, identity uniqueness and every authored level without changing serialized values.
    /// </summary>
    /// <param name="preset">Procedural preset to inspect.</param>
    /// <returns>Complete validation report ordered by preset, level, tile and portal scope.</returns>
    public static GameProceduralLevelValidationReport ValidatePreset(GameProceduralLevelPreset preset)
    {
        GameProceduralLevelValidationReport report = new GameProceduralLevelValidationReport();

        if (!ValidatePresetPrerequisites(preset, report))
            return report;

        HashSet<string> technicalIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> levelIds = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<GameProceduralLevelDefinition> levels = preset.Levels;

        if (levels == null || levels.Count == 0)
        {
            AddMissingEnabledLevelDiagnostic(preset, report);
            return report;
        }

        bool hasEnabledLevel = false;

        // Validate every authored entry so disabled levels cannot hide data that later becomes active.
        for (int index = 0; index < levels.Count; index++)
        {
            GameProceduralLevelDefinition level = levels[index];

            if (level == null)
            {
                report.Add(GameProceduralLevelValidationCode.NullLevel,
                           GameProceduralLevelValidationSeverity.Error,
                           "Level[" + index + "]",
                           "Remove the null level entry or replace it with a valid level definition.");
                continue;
            }

            if (level.Enabled)
                hasEnabledLevel = true;

            ValidateLevelIdentity(level, technicalIds, levelIds, report);
            ValidateLevelInternal(preset,
                                  level,
                                  ResolveRequiresLevelExit(preset, level),
                                  report);
        }

        if (!hasEnabledLevel)
            AddMissingEnabledLevelDiagnostic(preset, report);

        return report;
    }

    /// <summary>
    /// Validates one level with the same structural rules used by the shared solver and graph preview.
    /// </summary>
    /// <param name="preset">Preset supplying global limits, room metadata and scene catalog data.</param>
    /// <param name="level">Specific level definition to inspect.</param>
    /// <returns>Validation report scoped to global prerequisites and the selected level.</returns>
    public static GameProceduralLevelValidationReport ValidateLevel(GameProceduralLevelPreset preset,
                                                                    GameProceduralLevelDefinition level)
    {
        GameProceduralLevelValidationReport report = new GameProceduralLevelValidationReport();

        if (!ValidatePresetPrerequisites(preset, report))
            return report;

        if (level == null)
        {
            report.Add(GameProceduralLevelValidationCode.NullLevel,
                       GameProceduralLevelValidationSeverity.Error,
                       "Level",
                       "Select a valid level definition before generation.");
            return report;
        }

        ValidateLevelIdentity(level,
                              new HashSet<string>(StringComparer.Ordinal),
                              new HashSet<string>(StringComparer.Ordinal),
                              report);
        ValidateLevelInternal(preset,
                              level,
                              ResolveRequiresLevelExit(preset, level),
                              report);
        return report;
    }

    /// <summary>
    /// Returns the opposite logical portal side used by exact side fitting.
    /// </summary>
    /// <param name="side">Source side.</param>
    /// <returns>Opposite target side.</returns>
    public static GameRoomPortalSide GetOppositeSide(GameRoomPortalSide side)
    {
        switch (side)
        {
            case GameRoomPortalSide.North:
                return GameRoomPortalSide.South;

            case GameRoomPortalSide.South:
                return GameRoomPortalSide.North;

            case GameRoomPortalSide.East:
                return GameRoomPortalSide.West;

            default:
                return GameRoomPortalSide.East;
        }
    }
    #endregion

    #region Shared Internal Methods
    /// <summary>
    /// Validates a string against the exact UTF-8 payload available to FixedString64Bytes.
    /// </summary>
    /// <param name="value">String stored at runtime.</param>
    /// <param name="context">Field context included in diagnostics.</param>
    /// <param name="report">Destination report.</param>
    internal static void ValidateFixedString64(string value,
                                               string context,
                                               GameProceduralLevelValidationReport report)
    {
        if (string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) <= FixedString64MaximumUtf8Bytes)
            return;

        report.Add(GameProceduralLevelValidationCode.IdentifierTooLong,
                   GameProceduralLevelValidationSeverity.Error,
                   context,
                   "The UTF-8 value exceeds the 61-byte FixedString64 runtime capacity.");
    }

    /// <summary>
    /// Validates a -facing label against the exact UTF-8 payload available to FixedString128Bytes.
    /// </summary>
    /// <param name="value">Display label stored in the flattened runtime level buffer.</param>
    /// <param name="context">Field context included in diagnostics.</param>
    /// <param name="report">Destination report.</param>
    internal static void ValidateFixedString128(string value,
                                                string context,
                                                GameProceduralLevelValidationReport report)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) <= FixedString128Bytes.UTF8MaxLengthInBytes)
        {
            return;
        }

        report.Add(GameProceduralLevelValidationCode.LevelDisplayNameTooLong,
                   GameProceduralLevelValidationSeverity.Error,
                   context,
                   "The UTF-8 display name exceeds the FixedString128 runtime capacity of " +
                   FixedString128Bytes.UTF8MaxLengthInBytes + " bytes.");
    }

    /// <summary>
    /// Validates non-display runtime text against the exact UTF-8 payload available to FixedString128Bytes.
    /// </summary>
    /// <param name="value">Authored metadata value stored in a flattened runtime buffer.</param>
    /// <param name="context">Field context included in diagnostics.</param>
    /// <param name="report">Destination report.</param>
    internal static void ValidateRuntimeFixedString128(string value,
                                                       string context,
                                                       GameProceduralLevelValidationReport report)
    {
        if (string.IsNullOrEmpty(value) ||
            Encoding.UTF8.GetByteCount(value) <= FixedString128Bytes.UTF8MaxLengthInBytes)
        {
            return;
        }

        report.Add(GameProceduralLevelValidationCode.RuntimeTextTooLong,
                   GameProceduralLevelValidationSeverity.Error,
                   context,
                   "The UTF-8 value exceeds the FixedString128 runtime capacity of " +
                   FixedString128Bytes.UTF8MaxLengthInBytes + " bytes.");
    }

    /// <summary>
    /// Derives whether one authored level must hand progression to a later enabled level when active.
    /// </summary>
    /// <param name="preset">Preset owning the ordered level collection.</param>
    /// <param name="level">Level whose progression requirement is requested.</param>
    /// <returns>True when this level precedes another enabled level in authored order.</returns>
    internal static bool ResolveRequiresLevelExit(GameProceduralLevelPreset preset,
                                                  GameProceduralLevelDefinition level)
    {
        if (preset == null || level == null)
            return false;

        bool foundLevel = false;

        // Find the exact nested definition, then inspect only later enabled entries in authored order.
        for (int index = 0; index < preset.Levels.Count; index++)
        {
            GameProceduralLevelDefinition candidate = preset.Levels[index];

            if (!foundLevel)
            {
                if (ReferenceEquals(candidate, level))
                    foundLevel = true;

                continue;
            }

            if (candidate != null && candidate.Enabled)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a stable diagnostic label for a level with incomplete identity fields.
    /// </summary>
    /// <param name="level">Level whose label is required.</param>
    /// <returns>Best available level label.</returns>
    internal static string ResolveLevelContext(GameProceduralLevelDefinition level)
    {
        if (!string.IsNullOrWhiteSpace(level.LevelId))
            return level.LevelId;

        if (!string.IsNullOrWhiteSpace(level.DisplayName))
            return level.DisplayName;

        return "Unnamed Level";
    }
    #endregion

    #region Preset Methods
    /// <summary>
    /// Validates the preset reference, runtime identity, scene catalog and global solver limits.
    /// </summary>
    /// <param name="preset">Preset to inspect.</param>
    /// <param name="report">Destination report.</param>
    /// <returns>True when the preset exists and level iteration may continue safely.</returns>
    private static bool ValidatePresetPrerequisites(GameProceduralLevelPreset preset,
                                                    GameProceduralLevelValidationReport report)
    {
        if (preset == null)
        {
            report.Add(GameProceduralLevelValidationCode.MissingPreset,
                       GameProceduralLevelValidationSeverity.Error,
                       "Preset",
                       "Assign a Procedural Level preset before validation or generation.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(preset.PresetId))
            report.Add(GameProceduralLevelValidationCode.MissingPresetId,
                       GameProceduralLevelValidationSeverity.Error,
                       preset.PresetName,
                       "The preset requires a stable technical ID before it can be baked.");
        else
            ValidateFixedString64(preset.PresetId, "Preset ID", report);

        ValidateGenerationSettings(preset.GenerationSettings, report);
        ValidateTransitionSettings(preset.TransitionSettings, report);

        if (preset.SceneCatalogPreset == null)
            report.Add(GameProceduralLevelValidationCode.MissingSceneCatalog,
                       GameProceduralLevelValidationSeverity.Error,
                       preset.PresetName,
                       "Assign the Scene Manager preset that owns all room scene IDs.");

        ValidateMetadataIdentities(preset, report);

        return true;
    }

    /// <summary>
    /// Validates the deduplicated room metadata keys used by tile lookup and flattened bake indices.
    /// </summary>
    /// <param name="preset">Preset containing shared room metadata snapshots.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateMetadataIdentities(GameProceduralLevelPreset preset,
                                                   GameProceduralLevelValidationReport report)
    {
        HashSet<string> sceneIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < preset.RoomMetadata.Count; index++)
        {
            GameRoomSceneMetadata metadata = preset.RoomMetadata[index];

            if (metadata == null)
            {
                report.Add(GameProceduralLevelValidationCode.NullRoomMetadata,
                           GameProceduralLevelValidationSeverity.Error,
                           "Room Metadata[" + index + "]",
                           "Remove the null metadata entry and refresh the affected room scene.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(metadata.SceneId))
            {
                report.Add(GameProceduralLevelValidationCode.MissingRoomMetadataSceneId,
                           GameProceduralLevelValidationSeverity.Error,
                           "Room Metadata[" + index + "]",
                           "The cached room metadata requires its canonical Scene ID.");
                continue;
            }

            ValidateFixedString64(metadata.SceneId,
                                  "Room Metadata[" + index + "] Scene ID",
                                  report);
            ValidateFixedString64(metadata.SceneGuid,
                                  metadata.SceneId + " Scene GUID",
                                  report);
            ValidateRuntimeFixedString128(metadata.DependencyHash,
                                          metadata.SceneId + " Dependency Hash",
                                          report);

            if (!sceneIds.Add(metadata.SceneId))
                report.Add(GameProceduralLevelValidationCode.DuplicateRoomMetadataSceneId,
                           GameProceduralLevelValidationSeverity.Error,
                           metadata.SceneId,
                           "Only one deduplicated room metadata snapshot may exist for a Scene ID.");
        }
    }

    /// <summary>
    /// Validates solver limits without applying runtime safety clamps to authored values.
    /// </summary>
    /// <param name="settings">Global generation settings.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateGenerationSettings(GameProceduralLevelGenerationSettings settings,
                                                   GameProceduralLevelValidationReport report)
    {
        if (settings == null)
        {
            report.Add(GameProceduralLevelValidationCode.MissingGenerationSettings,
                       GameProceduralLevelValidationSeverity.Error,
                       "Generation Settings",
                       "Restore the required generation settings object.");
            return;
        }

        if (settings.MaximumNodeCount < 2)
            report.Add(GameProceduralLevelValidationCode.InvalidMaximumNodeCount,
                       GameProceduralLevelValidationSeverity.Error,
                       "Generation Settings",
                       "Maximum Node Count must allow at least separate Start and Boss nodes.");

        if (settings.MaximumDepth < 1)
            report.Add(GameProceduralLevelValidationCode.InvalidMaximumDepth,
                       GameProceduralLevelValidationSeverity.Error,
                       "Generation Settings",
                       "Maximum Depth must be at least one so the Boss can follow the Start room.");

        if (settings.MaximumGenerationAttempts < 1)
            report.Add(GameProceduralLevelValidationCode.InvalidAttemptLimit,
                       GameProceduralLevelValidationSeverity.Error,
                       "Generation Settings",
                       "Maximum Generation Attempts must be greater than zero.");
    }

    /// <summary>
    /// Validates only transition fields that participate in the currently enabled presentation path.
    /// </summary>
    /// <param name="settings">Global intra-level transition settings.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateTransitionSettings(GameProceduralLevelTransitionSettings settings,
                                                   GameProceduralLevelValidationReport report)
    {
        if (settings == null)
        {
            report.Add(GameProceduralLevelValidationCode.MissingTransitionSettings,
                       GameProceduralLevelValidationSeverity.Error,
                       "Transition Settings",
                       "Restore the required transition settings object.");
            return;
        }

        if (settings.RoomStreamingMode == GameProceduralRoomStreamingMode.TransactionalDualSlot &&
            settings.AdjacentPreloadPolicy != GameProceduralAdjacentPreloadPolicy.Disabled &&
            settings.MaximumStagedRooms < 1)
        {
            report.Add(GameProceduralLevelValidationCode.InvalidMaximumStagedRooms,
                       GameProceduralLevelValidationSeverity.Error,
                       "Transition Settings",
                       "Maximum Staged Rooms must be at least one while adjacent transactional preloading is enabled.");
        }

        if (settings.RoomStreamingMode == GameProceduralRoomStreamingMode.TransactionalDualSlot &&
            settings.RetiredRoomBudget < 0)
            report.Add(GameProceduralLevelValidationCode.InvalidRetiredRoomBudget,
                       GameProceduralLevelValidationSeverity.Error,
                       "Transition Settings",
                       "Retired Room Budget cannot be negative.");

        if (settings.RoomStreamingMode == GameProceduralRoomStreamingMode.TransactionalDualSlot &&
            (float.IsNaN(settings.RetirementWorkBudgetMilliseconds) ||
             float.IsInfinity(settings.RetirementWorkBudgetMilliseconds) ||
             settings.RetirementWorkBudgetMilliseconds <= 0f))
        {
            report.Add(GameProceduralLevelValidationCode.InvalidRetirementWorkBudget,
                       GameProceduralLevelValidationSeverity.Error,
                       "Transition Settings",
                       "Retirement Work Budget must be finite and greater than zero milliseconds.");
        }

        if (!settings.KeepPlayerVisible || settings.PlayerTransitionAnimation == null)
            return;

        if (settings.PlayerTransitionAnimation.hasRootCurves)
            report.Add(GameProceduralLevelValidationCode.TransitionAnimationContainsRootCurves,
                       GameProceduralLevelValidationSeverity.Error,
                       "Transition Settings",
                       "Player Transition Animation must be in-place and contain no root transform curves, otherwise it can change player presentation position or rotation during traversal.");

        float relocationTime = settings.RelocationNormalizedTime;

        if (!float.IsNaN(relocationTime) &&
            !float.IsInfinity(relocationTime) &&
            relocationTime >= 0f &&
            relocationTime <= 1f)
        {
            return;
        }

        report.Add(GameProceduralLevelValidationCode.InvalidRelocationNormalizedTime,
                   GameProceduralLevelValidationSeverity.Error,
                   "Transition Settings",
                   "Room Commit Normalized Time must be finite and remain inside the inclusive 0..1 range.");
    }
    #endregion

    #region Level Methods
    /// <summary>
    /// Validates stable and -facing level IDs, including preset-wide uniqueness.
    /// </summary>
    /// <param name="level">Level whose IDs are inspected.</param>
    /// <param name="technicalIds">Technical IDs already encountered in the preset.</param>
    /// <param name="levelIds">-facing IDs already encountered in the preset.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateLevelIdentity(GameProceduralLevelDefinition level,
                                              HashSet<string> technicalIds,
                                              HashSet<string> levelIds,
                                              GameProceduralLevelValidationReport report)
    {
        string context = ResolveLevelContext(level);
        ValidateRequiredUniqueId(level.TechnicalId,
                                 technicalIds,
                                 GameProceduralLevelValidationCode.MissingLevelTechnicalId,
                                 GameProceduralLevelValidationCode.DuplicateLevelTechnicalId,
                                 context,
                                 "technical ID",
                                 report);
        ValidateRequiredUniqueId(level.LevelId,
                                 levelIds,
                                 GameProceduralLevelValidationCode.MissingLevelId,
                                 GameProceduralLevelValidationCode.DuplicateLevelId,
                                 context,
                                 "Level ID",
                                 report);
        ValidateFixedString128(level.DisplayName, context + " Display Name", report);
    }

    /// <summary>
    /// Validates one level's ranges, scores, tile budget, scene references and portal feasibility.
    /// </summary>
    /// <param name="preset">Owning preset.</param>
    /// <param name="level">Level to inspect.</param>
    /// <param name="requiresLevelExit">Whether the level Boss must expose progression to a later enabled level.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateLevelInternal(GameProceduralLevelPreset preset,
                                              GameProceduralLevelDefinition level,
                                              bool requiresLevelExit,
                                              GameProceduralLevelValidationReport report)
    {
        string context = ResolveLevelContext(level);
        ValidateLevelRanges(level, preset.GenerationSettings, context, report);
        GameProceduralLevelRuleValidationUtility.Validate(level, context, report);
        HashSet<string> tileTechnicalIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> tileIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> validatedMetadataScenes = new HashSet<string>(StringComparer.Ordinal);
        int startCount = 0;
        int bossCount = 0;
        int regularCount = 0;
        long copyBudget = 0;

        // Validate every tile independently while accumulating structural role and copy budgets.
        for (int index = 0; index < level.RoomTiles.Count; index++)
        {
            GameProceduralRoomTileDefinition tile = level.RoomTiles[index];

            if (tile == null)
            {
                report.Add(GameProceduralLevelValidationCode.NullTile,
                           GameProceduralLevelValidationSeverity.Error,
                           context + "/Tile[" + index + "]",
                           "Remove the null tile entry or replace it with a room definition.");
                continue;
            }

            switch (tile.Role)
            {
                case GameProceduralRoomRole.Start:
                    startCount++;
                    break;

                case GameProceduralRoomRole.Regular:
                    regularCount++;
                    break;

                case GameProceduralRoomRole.Boss:
                    bossCount++;
                    break;
            }

            if (tile.MaximumCopies > 0)
                copyBudget += tile.MaximumCopies;

            GameProceduralLevelTileValidationUtility.ValidateTile(preset,
                                                                   level,
                                                                   tile,
                                                                   tileTechnicalIds,
                                                                   tileIds,
                                                                   validatedMetadataScenes,
                                                                   requiresLevelExit,
                                                                   report);
        }

        ValidateRoleCounts(context, startCount, regularCount, bossCount, report);

        if (copyBudget < level.TargetNodeCountRange.x)
            report.Add(GameProceduralLevelValidationCode.InsufficientCopyBudget,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The combined tile copy budget is smaller than the minimum target node count.");

        if (!level.UseCenterArrival)
            GameProceduralLevelTileValidationUtility.ValidateRequiredExitCompatibility(preset, level, report);
    }

    /// <summary>
    /// Validates authored node and Boss depth ranges against global technical limits.
    /// </summary>
    /// <param name="level">Level containing the ranges.</param>
    /// <param name="settings">Global solver limits.</param>
    /// <param name="context">-facing level context.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateLevelRanges(GameProceduralLevelDefinition level,
                                            GameProceduralLevelGenerationSettings settings,
                                            string context,
                                            GameProceduralLevelValidationReport report)
    {
        Vector2Int nodeRange = level.TargetNodeCountRange;
        Vector2Int bossRange = level.PreferredBossDepthRange;

        if (nodeRange.x < 2 || nodeRange.y < nodeRange.x)
            report.Add(GameProceduralLevelValidationCode.InvalidTargetNodeRange,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Target Node Count Range must be ordered and contain at least Start and Boss nodes.");

        if (settings != null && nodeRange.y > settings.MaximumNodeCount)
            report.Add(GameProceduralLevelValidationCode.TargetNodeRangeExceedsLimit,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Target Node Count Range exceeds the global Maximum Node Count.");

        if (bossRange.x < 1 || bossRange.y < bossRange.x)
            report.Add(GameProceduralLevelValidationCode.InvalidBossDepthRange,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Preferred Boss Depth Range must be ordered and start at depth one or later.");

        if (settings != null && bossRange.y > settings.MaximumDepth)
            report.Add(GameProceduralLevelValidationCode.BossDepthRangeExceedsLimit,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Preferred Boss Depth Range exceeds the global Maximum Depth.");
    }

    #endregion

    #region Helper Methods
    /// <summary>
    /// Records the blocking preset diagnostic used when no authored level can begin a runtime run.
    /// </summary>
    /// <param name="preset">Preset whose enabled-level collection is empty.</param>
    /// <param name="report">Destination validation report.</param>
    private static void AddMissingEnabledLevelDiagnostic(GameProceduralLevelPreset preset,
                                                         GameProceduralLevelValidationReport report)
    {
        report.Add(GameProceduralLevelValidationCode.MissingEnabledLevel,
                   GameProceduralLevelValidationSeverity.Error,
                   preset.PresetName,
                   "Enable at least one procedural level before preview, bake or runtime generation.");
    }

    /// <summary>
    /// Validates one required unique level identity and records missing, overlong or duplicate values.
    /// </summary>
    /// <param name="value">Identity value.</param>
    /// <param name="encountered">Values already encountered.</param>
    /// <param name="missingCode">Missing-value diagnostic code.</param>
    /// <param name="duplicateCode">Duplicate-value diagnostic code.</param>
    /// <param name="context">Owning level context.</param>
    /// <param name="label">-facing field label.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateRequiredUniqueId(string value,
                                                 HashSet<string> encountered,
                                                 GameProceduralLevelValidationCode missingCode,
                                                 GameProceduralLevelValidationCode duplicateCode,
                                                 string context,
                                                 string label,
                                                 GameProceduralLevelValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            report.Add(missingCode,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "The level requires a stable " + label + ".");
            return;
        }

        ValidateFixedString64(value, context + " " + label, report);

        if (!encountered.Add(value))
            report.Add(duplicateCode,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Another level uses the same " + label + ".");
    }

    /// <summary>
    /// Emits role-count diagnostics required by the rooted graph contract.
    /// </summary>
    /// <param name="context">Owning level context.</param>
    /// <param name="startCount">Number of Start tiles.</param>
    /// <param name="regularCount">Number of Regular tiles.</param>
    /// <param name="bossCount">Number of Boss tiles.</param>
    /// <param name="report">Destination report.</param>
    private static void ValidateRoleCounts(string context,
                                           int startCount,
                                           int regularCount,
                                           int bossCount,
                                           GameProceduralLevelValidationReport report)
    {
        if (startCount != 1)
            report.Add(startCount == 0
                           ? GameProceduralLevelValidationCode.MissingStartTile
                           : GameProceduralLevelValidationCode.DuplicateStartTile,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Add exactly one Start room tile.");

        if (bossCount != 1)
            report.Add(bossCount == 0
                           ? GameProceduralLevelValidationCode.MissingBossTile
                           : GameProceduralLevelValidationCode.DuplicateBossTile,
                       GameProceduralLevelValidationSeverity.Error,
                       context,
                       "Add exactly one Boss room tile.");

        if (regularCount == 0)
            report.Add(GameProceduralLevelValidationCode.MissingRegularTile,
                       GameProceduralLevelValidationSeverity.Warning,
                       context,
                       "No Regular tile is available; only a direct Start-to-Boss graph can be generated.");
    }
    #endregion

    #endregion
}
