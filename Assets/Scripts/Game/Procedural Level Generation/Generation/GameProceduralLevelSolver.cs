using System;
using System.Collections.Generic;

/// <summary>
/// Generates deterministic layered room graphs through one bounded core shared by editor and runtime callers.
/// </summary>
public static class GameProceduralLevelSolver
{
    #region Constants
    private const uint SeedOffset = 0x9E3779B9u;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates and adapts authored ScriptableObject data before invoking the editor-independent solver core.
    /// </summary>
    /// <param name="preset">Preset supplying level, metadata, scene and global solver configuration.</param>
    /// <param name="level">Level definition to generate.</param>
    /// <param name="runSeed">Authoritative run or preview seed.</param>
    /// <returns>Immutable generated graph or an explicit validation failure.</returns>
    public static GameProceduralLevelGenerationResult Generate(GameProceduralLevelPreset preset,
                                                                GameProceduralLevelDefinition level,
                                                                uint runSeed)
    {
        if (!TryCreateInput(preset, level, out GameProceduralLevelSolverInput input, out string diagnostic))
            return GameProceduralLevelGenerationResult.CreateFailure(GameProceduralLevelGenerationFailureCode.ValidationFailed,
                                                                     diagnostic,
                                                                     runSeed,
                                                                     0u,
                                                                     0);

        return Generate(input, runSeed);
    }

    /// <summary>
    /// Runs bounded deterministic weighted backtracking from plain managed data suitable for ECS buffer adaptation.
    /// </summary>
    /// <param name="input">Editor-independent immutable generation request.</param>
    /// <param name="runSeed">Authoritative run or preview seed.</param>
    /// <returns>Immutable generated graph or an explicit failure without a partial graph.</returns>
    public static GameProceduralLevelGenerationResult Generate(GameProceduralLevelSolverInput input, uint runSeed)
    {
        if (!TryValidateInput(input,
                              out GameProceduralLevelGenerationFailureCode validationFailure,
                              out string validationDiagnostic))
            return GameProceduralLevelGenerationResult.CreateFailure(validationFailure,
                                                                     validationDiagnostic,
                                                                     runSeed,
                                                                     0u,
                                                                     0);

        uint levelSeed = DeriveLevelSeed(runSeed, input.LevelTechnicalId);
        GameProceduralLevelGenerationFailureCode lastFailure = GameProceduralLevelGenerationFailureCode.AttemptLimitReached;
        string lastDiagnostic = "No valid graph was found within the configured bounded attempt limit.";

        // Restart from a deterministic derived stream after each exhausted backtracking tree.
        for (int attempt = 0; attempt < input.MaximumGenerationAttempts; attempt++)
        {
            uint attemptSeed = MixSeed(levelSeed, unchecked(SeedOffset + (uint)attempt));
            GameProceduralLevelSolverContext context = new GameProceduralLevelSolverContext(input, attemptSeed);

            if (context.TryGenerate())
            {
                if (!GameProceduralLevelGraphInvariantUtility.TryValidate(input,
                                                                          context.Nodes,
                                                                          context.Edges,
                                                                          out string invariantDiagnostic))
                    return GameProceduralLevelGenerationResult.CreateFailure(GameProceduralLevelGenerationFailureCode.GraphInvariantViolation,
                                                                             invariantDiagnostic,
                                                                             runSeed,
                                                                             levelSeed,
                                                                             attempt + 1);

                return GameProceduralLevelGenerationResult.CreateSuccess(runSeed,
                                                                         levelSeed,
                                                                         attempt + 1,
                                                                         context.Nodes,
                                                                         context.Edges);
            }

            lastFailure = context.FailureCode;
            lastDiagnostic = context.Diagnostic;
        }

        return GameProceduralLevelGenerationResult.CreateFailure(lastFailure == GameProceduralLevelGenerationFailureCode.None
                                                                     ? GameProceduralLevelGenerationFailureCode.AttemptLimitReached
                                                                     : lastFailure,
                                                                 lastDiagnostic,
                                                                 runSeed,
                                                                 levelSeed,
                                                                 input.MaximumGenerationAttempts);
    }

    /// <summary>
    /// Converts one validated authored level into the plain immutable input accepted by runtime generation.
    /// </summary>
    /// <param name="preset">Preset supplying global limits and room metadata.</param>
    /// <param name="level">Level whose tile set is adapted.</param>
    /// <param name="input">Created pure solver input when validation succeeds.</param>
    /// <param name="diagnostic">First actionable validation error when adaptation fails.</param>
    /// <returns>True when a complete solver input was created.</returns>
    public static bool TryCreateInput(GameProceduralLevelPreset preset,
                                      GameProceduralLevelDefinition level,
                                      out GameProceduralLevelSolverInput input,
                                      out string diagnostic)
    {
        input = null;
        diagnostic = string.Empty;
        GameProceduralLevelValidationReport report = GameProceduralLevelValidator.ValidateLevel(preset, level);

        if (!report.IsValid)
        {
            diagnostic = ResolveFirstError(report);
            return false;
        }

        List<GameProceduralRoomTileSolverInput> tileInputs = new List<GameProceduralRoomTileSolverInput>(level.RoomTiles.Count);

        // Flatten deduplicated room metadata into each reusable tile for a self-contained runtime request.
        for (int tileIndex = 0; tileIndex < level.RoomTiles.Count; tileIndex++)
        {
            GameProceduralRoomTileDefinition tile = level.RoomTiles[tileIndex];
            preset.TryFindRoomMetadata(tile.SceneId, out GameRoomSceneMetadata metadata);
            List<GameProceduralRoomPortalSolverInput> portalInputs = new List<GameProceduralRoomPortalSolverInput>(metadata.Portals.Count);

            for (int portalIndex = 0; portalIndex < metadata.Portals.Count; portalIndex++)
            {
                GameRoomPortalMetadata portal = metadata.Portals[portalIndex];

                if (portal == null)
                    continue;

                portalInputs.Add(new GameProceduralRoomPortalSolverInput(portal.PortalId,
                                                                         portal.Side,
                                                                         portal.Capability,
                                                                         portal.ConnectionPolicy));
            }

            tileInputs.Add(new GameProceduralRoomTileSolverInput(tile.TechnicalId,
                                                                  tile.TileId,
                                                                  tile.SceneId,
                                                                  tile.Role,
                                                                  tile.MaximumCopies,
                                                                  tile.PreferredDepthRange,
                                                                  tile.BaseSelectionWeight,
                                                                  metadata.CenterAnchorCount,
                                                                  portalInputs,
                                                                  tile.UseExactDepthConstraint,
                                                                  tile.ExactDepth));
        }

        GameProceduralLevelGenerationSettings generationSettings = preset.GenerationSettings;
        GameProceduralLevelRuleSettings ruleSettings = level.RuleSettings;
        input = new GameProceduralLevelSolverInput(level.TechnicalId,
                                                   level.LevelId,
                                                   level.TargetNodeCountRange,
                                                   level.PreferredBossDepthRange,
                                                   ruleSettings.RoomDepthScore,
                                                   ruleSettings.BossDepthScore,
                                                   ruleSettings.FittingScore,
                                                   level.UseCenterArrival,
                                                   GameProceduralLevelValidator.ResolveRequiresLevelExit(preset, level),
                                                   generationSettings.MaximumNodeCount,
                                                   generationSettings.MaximumDepth,
                                                   generationSettings.MaximumGenerationAttempts,
                                                   tileInputs);
        return true;
    }

    /// <summary>
    /// Derives the stable per-level stream seed from one run seed and immutable level identity.
    /// </summary>
    /// <param name="runSeed">Authoritative run or preview seed.</param>
    /// <param name="levelTechnicalId">Immutable level identity.</param>
    /// <returns>Non-zero deterministic per-level seed.</returns>
    public static uint DeriveLevelSeed(uint runSeed, string levelTechnicalId)
    {
        uint hash = 2166136261u;
        string identity = levelTechnicalId ?? string.Empty;

        // Hash UTF-16 code units deterministically without depending on platform string hash randomization.
        for (int index = 0; index < identity.Length; index++)
        {
            char character = identity[index];
            hash ^= (byte)character;
            hash *= 16777619u;
            hash ^= (byte)(character >> 8);
            hash *= 16777619u;
        }

        return MixSeed(hash, runSeed == 0u ? SeedOffset : runSeed);
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Validates plain runtime input before any random stream or mutable search state is allocated.
    /// </summary>
    /// <param name="input">Pure generation request.</param>
    /// <param name="failureCode">Stable failure category when validation fails.</param>
    /// <param name="diagnostic">Actionable failure description.</param>
    /// <returns>True when the request satisfies all core structural prerequisites.</returns>
    private static bool TryValidateInput(GameProceduralLevelSolverInput input,
                                         out GameProceduralLevelGenerationFailureCode failureCode,
                                         out string diagnostic)
    {
        failureCode = GameProceduralLevelGenerationFailureCode.InvalidInput;
        diagnostic = string.Empty;

        if (input == null)
        {
            diagnostic = "The solver input is null.";
            return false;
        }

        if (input.MaximumNodeCount < 2 ||
            input.MaximumDepth < 1 ||
            input.MaximumGenerationAttempts < 1)
        {
            diagnostic = "Solver node, depth and attempt limits must be positive and allow separate Start and Boss nodes.";
            return false;
        }

        if (input.TargetNodeCountRange.x < 2 ||
            input.TargetNodeCountRange.y < input.TargetNodeCountRange.x ||
            input.TargetNodeCountRange.y > input.MaximumNodeCount)
        {
            diagnostic = "The target node range is inverted, below two nodes or exceeds the hard node limit.";
            return false;
        }

        if (input.PreferredBossDepthRange.x < 1 ||
            input.PreferredBossDepthRange.y < input.PreferredBossDepthRange.x ||
            input.PreferredBossDepthRange.y > input.MaximumDepth)
        {
            diagnostic = "The preferred Boss depth range is invalid or exceeds the hard depth limit.";
            return false;
        }

        if (!IsFiniteNonNegative(input.RoomDepthScore) ||
            !IsFiniteNonNegative(input.BossDepthScore) ||
            (!input.UseCenterArrival && !IsFiniteNonNegative(input.FittingScore)))
        {
            diagnostic = "Every active solver score must be finite and non-negative.";
            return false;
        }

        int startCount = 0;
        int bossCount = 0;
        HashSet<string> tileTechnicalIds = new HashSet<string>(StringComparer.Ordinal);

        // Validate immutable tile inputs and exact root/terminal role counts.
        for (int index = 0; index < input.RoomTiles.Count; index++)
        {
            GameProceduralRoomTileSolverInput tile = input.RoomTiles[index];

            if (tile == null ||
                string.IsNullOrWhiteSpace(tile.TechnicalId) ||
                string.IsNullOrWhiteSpace(tile.SceneId) ||
                tile.MaximumCopies <= 0 ||
                tile.PreferredDepthRange.x < 0 ||
                tile.PreferredDepthRange.y < tile.PreferredDepthRange.x ||
                tile.UseExactDepthConstraint && (tile.ExactDepth < 0 || tile.ExactDepth > input.MaximumDepth) ||
                tile.Role == GameProceduralRoomRole.Start && tile.UseExactDepthConstraint && tile.ExactDepth != 0 ||
                tile.Role != GameProceduralRoomRole.Start && tile.UseExactDepthConstraint && tile.ExactDepth < 1 ||
                tile.BaseSelectionWeight <= 0f ||
                float.IsNaN(tile.BaseSelectionWeight) ||
                float.IsInfinity(tile.BaseSelectionWeight))
            {
                diagnostic = "Every solver tile requires identity, scene, valid preferred or exact depth data, positive copy budget and a finite positive base weight.";
                return false;
            }

            if (!tileTechnicalIds.Add(tile.TechnicalId))
            {
                diagnostic = "Every pure solver tile requires a unique technical ID.";
                return false;
            }

            HashSet<string> portalIds = new HashSet<string>(StringComparer.Ordinal);
            bool hasUsableLevelExit = false;

            for (int portalIndex = 0; portalIndex < tile.Portals.Count; portalIndex++)
            {
                GameProceduralRoomPortalSolverInput portal = tile.Portals[portalIndex];
                string portalId = portal.PortalId;

                if (!string.IsNullOrWhiteSpace(portalId) && portalIds.Add(portalId))
                {
                    if (portal.ConnectionPolicy == GameRoomPortalConnectionPolicy.LevelExit &&
                        (portal.Capability == GameRoomPortalCapability.Exit ||
                         portal.Capability == GameRoomPortalCapability.Both))
                        hasUsableLevelExit = true;

                    continue;
                }

                diagnostic = "Every physical portal requires a non-empty ID unique inside its reusable room tile.";
                return false;
            }

            switch (tile.Role)
            {
                case GameProceduralRoomRole.Start:
                    startCount++;
                    break;

                case GameProceduralRoomRole.Boss:
                    bossCount++;
                    break;
            }

            if ((input.UseCenterArrival || tile.Role == GameProceduralRoomRole.Start) &&
                tile.CenterAnchorCount != 1)
            {
                diagnostic = tile.Role == GameProceduralRoomRole.Start
                    ? "The Start tile requires exactly one center anchor because initial and level-boundary arrival always uses it."
                    : "Center-arrival mode requires exactly one center anchor on every reusable tile.";
                return false;
            }

            if (input.RequiresLevelExit &&
                tile.Role == GameProceduralRoomRole.Boss &&
                !hasUsableLevelExit)
            {
                diagnostic = "A level followed by another enabled level requires its Boss tile to expose at least one LevelExit portal with Exit or Both capability.";
                return false;
            }
        }

        if (startCount != 1)
        {
            failureCode = GameProceduralLevelGenerationFailureCode.MissingStartTile;
            diagnostic = "Exactly one Start tile is required.";
            return false;
        }

        if (bossCount != 1)
        {
            failureCode = GameProceduralLevelGenerationFailureCode.MissingBossTile;
            diagnostic = "Exactly one Boss tile is required.";
            return false;
        }

        failureCode = GameProceduralLevelGenerationFailureCode.None;
        return true;
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Resolves the first error into one concise generation guard message.
    /// </summary>
    /// <param name="report">Validation report containing one or more errors.</param>
    /// <returns>Stable code, context and actionable validation message.</returns>
    private static string ResolveFirstError(GameProceduralLevelValidationReport report)
    {
        for (int index = 0; index < report.Diagnostics.Count; index++)
        {
            GameProceduralLevelValidationDiagnostic diagnostic = report.Diagnostics[index];

            if (diagnostic.Severity != GameProceduralLevelValidationSeverity.Error)
                continue;

            return "[" + diagnostic.Code + "] " + diagnostic.Context + ": " + diagnostic.Message;
        }

        return "Procedural level validation failed without a reported error.";
    }

    /// <summary>
    /// Mixes two deterministic seed values with an integer avalanche function.
    /// </summary>
    /// <param name="left">First seed value.</param>
    /// <param name="right">Second seed value.</param>
    /// <returns>Non-zero mixed seed.</returns>
    private static uint MixSeed(uint left, uint right)
    {
        uint value = unchecked(left ^ right ^ SeedOffset);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value == 0u ? SeedOffset : value;
    }

    /// <summary>
    /// Checks whether one solver score is finite and non-negative.
    /// </summary>
    /// <param name="value">Score to inspect.</param>
    /// <returns>True when the score is valid.</returns>
    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
    #endregion

    #endregion
}
