using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verifies deterministic graph generation, same-side multiplicity, convergence, center arrival and portal reservation.
/// </summary>
public static class GameProceduralLevelSolverSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs pure managed solver checks without creating assets, opening scenes or exposing a persistent menu item.
    /// </summary>
    public static void Run()
    {
        ValidatePortalArrivalGraph();
        ValidateCenterArrivalSkipsFitting();
        ValidateEntranceCapacityFailure();
        ValidateExactDepthConstraint();
        ValidateOptionalExitPlanBacktracking();
        ValidateBossExitBacktracking();
        ValidatePreferredDepthFallback();
        ValidateArrivalAndLevelExitGuards();
        ValidateValidatorContracts();
        Debug.Log("[GameProceduralLevelSolverSmokeTest] Deterministic solver checks passed.");
    }
    #endregion

    #region Test Methods
    /// <summary>
    /// Verifies two same-side exits target distinct rooms, merge into one Boss and never reuse incoming Both portals.
    /// </summary>
    private static void ValidatePortalArrivalGraph()
    {
        GameProceduralLevelSolverInput input = CreatePortalInput(true, false, false, null, 1);
        const uint Seed = 41729u;
        GameProceduralLevelGenerationResult first = GameProceduralLevelSolver.Generate(input, Seed);
        GameProceduralLevelGenerationResult second = GameProceduralLevelSolver.Generate(input, Seed);
        Require(first.Success, "Portal graph failed: " + first.FailureCode + " - " + first.Diagnostic);
        Require(second.Success, "Repeated portal graph failed.");
        Require(first.Nodes.Count == 4, "Portal graph did not respect the exact four-node target.");
        Require(first.Edges.Count == 4, "Portal graph should contain two branches and two converging Boss edges.");
        RequireGraphsEqual(first, second);

        HashSet<int> startTargets = new HashSet<int>();
        int bossNodeId = FindBossNode(first);
        int bossIncomingCount = 0;

        // Inspect every edge assignment independently to preserve same-side multiplicity semantics.
        for (int index = 0; index < first.Edges.Count; index++)
        {
            GameProceduralLevelGraphEdge edge = first.Edges[index];
            Require(!edge.UsesCenterArrival, "Portal graph emitted a center-arrival edge.");
            Require(edge.TargetSide == GameProceduralLevelValidator.GetOppositeSide(edge.SourceSide),
                    "Portal graph emitted non-opposite sides.");

            if (edge.SourceNodeId == 0)
            {
                Require(edge.SourceSide == GameRoomPortalSide.North,
                        "Same-side Start exits did not preserve their authored North side.");
                startTargets.Add(edge.TargetNodeId);
            }

            if (edge.TargetNodeId == bossNodeId)
                bossIncomingCount++;

            if (first.Nodes[edge.SourceNodeId].Role == GameProceduralRoomRole.Regular)
                Require(!string.Equals(edge.SourcePortalId, "REGULAR_BOTH_SOUTH", StringComparison.Ordinal),
                        "An incoming Both portal was reused as an outgoing exit.");
        }

        Require(startTargets.Count == 2, "Two outgoing portals from Start targeted the same logical room node.");
        Require(bossIncomingCount == 2, "Both branches did not merge into the single terminal Boss.");
    }

    /// <summary>
    /// Verifies center-arrival generation ignores incompatible target sides and omits every target portal assignment.
    /// </summary>
    private static void ValidateCenterArrivalSkipsFitting()
    {
        GameProceduralLevelSolverInput input = CreatePortalInput(true, true, false, null, 1);
        GameProceduralLevelGenerationResult result = GameProceduralLevelSolver.Generate(input, 9001u);
        Require(result.Success, "Center-arrival graph failed despite valid exit topology: " + result.Diagnostic);
        GameProceduralLevelGenerationResult ignoredFittingResult = GameProceduralLevelSolver.Generate(
            CreatePortalInput(true, true, false, null, 1, float.NaN),
            9001u);
        Require(ignoredFittingResult.Success,
                "Center-arrival graph evaluated the inactive Fitting Score: " + ignoredFittingResult.Diagnostic);

        for (int index = 0; index < result.Edges.Count; index++)
        {
            GameProceduralLevelGraphEdge edge = result.Edges[index];
            Require(edge.UsesCenterArrival, "Center-arrival graph emitted a fitted edge.");
            Require(string.IsNullOrEmpty(edge.TargetPortalId), "Center-arrival graph assigned a target entrance.");
        }
    }

    /// <summary>
    /// Verifies two converging branches fail explicitly when the Boss owns only one compatible physical entrance.
    /// </summary>
    private static void ValidateEntranceCapacityFailure()
    {
        GameProceduralLevelSolverInput input = CreatePortalInput(false, false, false, null, 1);
        GameProceduralLevelGenerationResult result = GameProceduralLevelSolver.Generate(input, 71u);
        Require(!result.Success, "Portal graph succeeded despite insufficient unique Boss entrance capacity.");
        Require(result.FailureCode == GameProceduralLevelGenerationFailureCode.NoBossRoomCandidate ||
                result.FailureCode == GameProceduralLevelGenerationFailureCode.TargetEntranceUnavailable,
                "Insufficient entrance capacity returned an unrelated failure code: " + result.FailureCode);
    }

    /// <summary>
    /// Verifies a hard tile depth accepts the exact layer and excludes the tile from every other candidate layer.
    /// </summary>
    private static void ValidateExactDepthConstraint()
    {
        GameProceduralLevelGenerationResult validResult = GameProceduralLevelSolver.Generate(
            CreatePortalInput(true, false, false, null, 1, 2f, 1, 2),
            173u);
        Require(validResult.Success,
                "Exact regular and Boss depth constraints rejected their valid layers: " + validResult.Diagnostic);

        for (int nodeIndex = 0; nodeIndex < validResult.Nodes.Count; nodeIndex++)
        {
            GameProceduralLevelGraphNode node = validResult.Nodes[nodeIndex];

            if (node.Role == GameProceduralRoomRole.Regular)
                Require(node.Depth == 1, "A Regular tile spawned outside its exact depth.");

            if (node.Role == GameProceduralRoomRole.Boss)
                Require(node.Depth == 2, "The Boss tile spawned outside its exact depth.");
        }

        GameProceduralLevelGenerationResult excludedResult = GameProceduralLevelSolver.Generate(
            CreatePortalInput(true, false, false, null, 1, 2f, 2, 2),
            173u);
        Require(!excludedResult.Success,
                "A Regular tile constrained to depth two incorrectly spawned in the required depth-one layer.");
    }

    /// <summary>
    /// Verifies all-Optional rooms connect at least one exit and retry every valid subset within one solver attempt.
    /// </summary>
    private static void ValidateOptionalExitPlanBacktracking()
    {
        GameProceduralLevelSolverInput input = CreateOptionalBranchInput();

        // Exercise different seeded preference orders while keeping the technical attempt limit at one.
        for (uint seed = 0u; seed < 32u; seed++)
        {
            GameProceduralLevelGenerationResult result = GameProceduralLevelSolver.Generate(input, seed);
            Require(result.Success,
                    "Optional exit planning remained seed-dependent for seed " + seed + ": " +
                    result.FailureCode + " - " + result.Diagnostic);
            Require(result.Nodes.Count == 4,
                    "Optional exit planning did not satisfy the exact four-node target.");

            for (int nodeIndex = 0; nodeIndex < result.Nodes.Count; nodeIndex++)
            {
                GameProceduralLevelGraphNode node = result.Nodes[nodeIndex];

                if (node.Role == GameProceduralRoomRole.Boss)
                    continue;

                bool hasOutgoingEdge = false;

                for (int edgeIndex = 0; edgeIndex < result.Edges.Count; edgeIndex++)
                {
                    if (result.Edges[edgeIndex].SourceNodeId == node.NodeId)
                        hasOutgoingEdge = true;
                }

                Require(hasOutgoingEdge,
                        "An all-Optional non-Boss room was left without a connected exit.");
            }
        }
    }

    /// <summary>
    /// Verifies Boss convergence retries alternate Optional source portals when only one side is compatible.
    /// </summary>
    private static void ValidateBossExitBacktracking()
    {
        GameProceduralLevelSolverInput input = CreateBossExitBacktrackingInput();

        for (uint seed = 0u; seed < 32u; seed++)
        {
            GameProceduralLevelGenerationResult result = GameProceduralLevelSolver.Generate(input, seed);
            Require(result.Success,
                    "Boss source-exit selection remained seed-dependent for seed " + seed + ": " +
                    result.FailureCode + " - " + result.Diagnostic);
        }
    }

    /// <summary>
    /// Verifies preferred depth scoring falls back to an earlier valid Boss when deeper expansion is impossible.
    /// </summary>
    private static void ValidatePreferredDepthFallback()
    {
        GameProceduralLevelSolverInput input = CreatePreferredDepthFallbackInput();

        for (uint seed = 0u; seed < 16u; seed++)
        {
            GameProceduralLevelGenerationResult result = GameProceduralLevelSolver.Generate(input, seed);
            Require(result.Success,
                    "A soft Boss-depth preference rejected a shallower valid graph for seed " + seed + ": " +
                    result.FailureCode + " - " + result.Diagnostic);
            Require(result.Nodes.Count == 2 && result.Nodes[1].Role == GameProceduralRoomRole.Boss,
                    "Preferred-depth fallback did not close directly from Start to Boss.");
        }
    }

    /// <summary>
    /// Verifies the pure runtime guard always requires the Start center anchor and conditionally requires a usable Boss LevelExit.
    /// </summary>
    private static void ValidateArrivalAndLevelExitGuards()
    {
        GameProceduralLevelGenerationResult missingAnchor = GameProceduralLevelSolver.Generate(
            CreatePortalInput(true, false, false, null, 0),
            37u);
        Require(!missingAnchor.Success &&
                missingAnchor.Diagnostic.IndexOf("Start tile", StringComparison.Ordinal) >= 0,
                "Portal-arrival input without a Start center anchor did not fail its initial-arrival guard.");

        GameProceduralLevelGenerationResult missingLevelExit = GameProceduralLevelSolver.Generate(
            CreatePortalInput(true,
                              false,
                              true,
                              GameRoomPortalCapability.Entrance,
                              1),
            41u);
        Require(!missingLevelExit.Success &&
                missingLevelExit.Diagnostic.IndexOf("LevelExit", StringComparison.Ordinal) >= 0,
                "A non-final level with an entrance-only Boss LevelExit passed pure solver validation.");

        GameProceduralLevelGenerationResult validLevelExit = GameProceduralLevelSolver.Generate(
            CreatePortalInput(true,
                              false,
                              true,
                              GameRoomPortalCapability.Exit,
                              1),
            43u);
        Require(validLevelExit.Success,
                "A non-final level with a usable Boss LevelExit failed generation: " + validLevelExit.Diagnostic);
    }

    /// <summary>
    /// Verifies stable public validation diagnostics and exact opposite-side mappings used by editor and runtime.
    /// </summary>
    private static void ValidateValidatorContracts()
    {
        GameProceduralLevelValidationReport report = GameProceduralLevelValidator.ValidatePreset(null);
        Require(!report.IsValid, "A null preset unexpectedly passed validation.");
        Require(report.Diagnostics.Count == 1 &&
                report.Diagnostics[0].Code == GameProceduralLevelValidationCode.MissingPreset,
                "A null preset did not return the stable MissingPreset diagnostic.");
        Require(GameProceduralLevelValidator.GetOppositeSide(GameRoomPortalSide.North) == GameRoomPortalSide.South,
                "North did not map to South.");
        Require(GameProceduralLevelValidator.GetOppositeSide(GameRoomPortalSide.South) == GameRoomPortalSide.North,
                "South did not map to North.");
        Require(GameProceduralLevelValidator.GetOppositeSide(GameRoomPortalSide.East) == GameRoomPortalSide.West,
                "East did not map to West.");
        Require(GameProceduralLevelValidator.GetOppositeSide(GameRoomPortalSide.West) == GameRoomPortalSide.East,
                "West did not map to East.");
    }
    #endregion

    #region Factory Methods
    /// <summary>
    /// Creates a four-node branch-and-merge input with optional side incompatibility and Boss entrance shortage.
    /// </summary>
    /// <param name="includeSecondBossEntrance">Whether the Boss can receive both converging branches.</param>
    /// <param name="useCenterArrival">Whether all target entrance fitting is skipped.</param>
    /// <param name="requiresLevelExit">Whether the Boss must expose a usable inter-level progression portal.</param>
    /// <param name="levelExitCapability">Optional capability for the Boss LevelExit fixture; null omits the portal.</param>
    /// <param name="startCenterAnchorCount">Center-anchor count exposed by the Start room metadata.</param>
    /// <param name="fittingScore">Fitting score supplied to the immutable solver request.</param>
    /// <param name="regularExactDepth">Exact Regular depth, or -1 to keep soft preferred-range placement.</param>
    /// <param name="bossExactDepth">Exact Boss depth, or -1 to keep soft preferred-range placement.</param>
    /// <returns>Immutable pure managed solver request.</returns>
    private static GameProceduralLevelSolverInput CreatePortalInput(bool includeSecondBossEntrance,
                                                                    bool useCenterArrival,
                                                                    bool requiresLevelExit,
                                                                    GameRoomPortalCapability? levelExitCapability,
                                                                    int startCenterAnchorCount,
                                                                    float fittingScore = 2f,
                                                                    int regularExactDepth = -1,
                                                                    int bossExactDepth = -1)
    {
        List<GameProceduralRoomPortalSolverInput> startPortals = new List<GameProceduralRoomPortalSolverInput>
        {
            CreatePortal("START_EXIT_NORTH_A",
                         GameRoomPortalSide.North,
                         GameRoomPortalCapability.Exit,
                         GameRoomPortalConnectionPolicy.Required),
            CreatePortal("START_EXIT_NORTH_B",
                         GameRoomPortalSide.North,
                         GameRoomPortalCapability.Exit,
                         GameRoomPortalConnectionPolicy.Required)
        };
        GameRoomPortalSide regularEntranceSide = useCenterArrival
            ? GameRoomPortalSide.North
            : GameRoomPortalSide.South;
        List<GameProceduralRoomPortalSolverInput> regularPortals = new List<GameProceduralRoomPortalSolverInput>
        {
            CreatePortal("REGULAR_BOTH_SOUTH",
                         regularEntranceSide,
                         useCenterArrival ? GameRoomPortalCapability.Entrance : GameRoomPortalCapability.Both,
                         GameRoomPortalConnectionPolicy.Required),
            CreatePortal("REGULAR_EXIT_NORTH",
                         GameRoomPortalSide.North,
                         GameRoomPortalCapability.Exit,
                         GameRoomPortalConnectionPolicy.Required)
        };
        GameRoomPortalSide bossEntranceSide = useCenterArrival
            ? GameRoomPortalSide.East
            : GameRoomPortalSide.South;
        List<GameProceduralRoomPortalSolverInput> bossPortals = new List<GameProceduralRoomPortalSolverInput>
        {
            CreatePortal("BOSS_ENTRANCE_A",
                         bossEntranceSide,
                         GameRoomPortalCapability.Entrance,
                         GameRoomPortalConnectionPolicy.Required)
        };

        if (includeSecondBossEntrance)
            bossPortals.Add(CreatePortal("BOSS_ENTRANCE_B",
                                         bossEntranceSide,
                                         GameRoomPortalCapability.Entrance,
                                         GameRoomPortalConnectionPolicy.Required));

        if (levelExitCapability.HasValue)
            bossPortals.Add(CreatePortal("BOSS_LEVEL_EXIT",
                                         GameRoomPortalSide.East,
                                         levelExitCapability.Value,
                                         GameRoomPortalConnectionPolicy.LevelExit));

        List<GameProceduralRoomTileSolverInput> tiles = new List<GameProceduralRoomTileSolverInput>
        {
            new GameProceduralRoomTileSolverInput("TILE_START_TECH",
                                                   "START_TILE",
                                                   "SCN_START",
                                                   GameProceduralRoomRole.Start,
                                                   1,
                                                   new Vector2Int(0, 0),
                                                   1f,
                                                   startCenterAnchorCount,
                                                   startPortals),
            new GameProceduralRoomTileSolverInput("TILE_REGULAR_TECH",
                                                   "REGULAR_TILE",
                                                   "SCN_REGULAR",
                                                   GameProceduralRoomRole.Regular,
                                                   2,
                                                   new Vector2Int(1, 1),
                                                   1f,
                                                   1,
                                                   regularPortals,
                                                   regularExactDepth >= 0,
                                                   regularExactDepth),
            new GameProceduralRoomTileSolverInput("TILE_BOSS_TECH",
                                                   "BOSS_TILE",
                                                   "SCN_BOSS",
                                                   GameProceduralRoomRole.Boss,
                                                   1,
                                                   new Vector2Int(2, 2),
                                                   1f,
                                                   1,
                                                   bossPortals,
                                                   bossExactDepth >= 0,
                                                   bossExactDepth)
        };
        return new GameProceduralLevelSolverInput("LEVEL_TECH",
                                                  "LEVEL_TEST",
                                                  new Vector2Int(4, 4),
                                                  new Vector2Int(2, 2),
                                                  1f,
                                                  4f,
                                                  fittingScore,
                                                  useCenterArrival,
                                                  requiresLevelExit,
                                                  16,
                                                  8,
                                                  32,
                                                  tiles);
    }

    /// <summary>
    /// Creates an exact four-node center-arrival graph that requires both Optional Start exits to be connected.
    /// </summary>
    /// <returns>Single-attempt solver input used to prove Optional subset backtracking.</returns>
    private static GameProceduralLevelSolverInput CreateOptionalBranchInput()
    {
        List<GameProceduralRoomTileSolverInput> tiles = new List<GameProceduralRoomTileSolverInput>
        {
            new GameProceduralRoomTileSolverInput(
                "OPTIONAL_START_TECH",
                "OPTIONAL_START",
                "SCN_OPTIONAL_START",
                GameProceduralRoomRole.Start,
                1,
                new Vector2Int(0, 0),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("OPTIONAL_START_NORTH",
                                 GameRoomPortalSide.North,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional),
                    CreatePortal("OPTIONAL_START_EAST",
                                 GameRoomPortalSide.East,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional)
                }),
            new GameProceduralRoomTileSolverInput(
                "OPTIONAL_REGULAR_TECH",
                "OPTIONAL_REGULAR",
                "SCN_OPTIONAL_REGULAR",
                GameProceduralRoomRole.Regular,
                2,
                new Vector2Int(1, 1),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("OPTIONAL_REGULAR_EXIT",
                                 GameRoomPortalSide.North,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional)
                },
                true,
                1),
            new GameProceduralRoomTileSolverInput(
                "OPTIONAL_BOSS_TECH",
                "OPTIONAL_BOSS",
                "SCN_OPTIONAL_BOSS",
                GameProceduralRoomRole.Boss,
                1,
                new Vector2Int(2, 2),
                1f,
                1,
                Array.Empty<GameProceduralRoomPortalSolverInput>(),
                true,
                2)
        };
        return new GameProceduralLevelSolverInput("OPTIONAL_LEVEL_TECH",
                                                  "OPTIONAL_LEVEL",
                                                  new Vector2Int(4, 4),
                                                  new Vector2Int(2, 2),
                                                  1f,
                                                  1f,
                                                  0f,
                                                  true,
                                                  false,
                                                  8,
                                                  4,
                                                  1,
                                                  tiles);
    }

    /// <summary>
    /// Creates a fitted three-node graph whose Regular room has one compatible and one incompatible Optional Boss exit.
    /// </summary>
    /// <returns>Single-attempt solver input used to prove Boss source-portal backtracking.</returns>
    private static GameProceduralLevelSolverInput CreateBossExitBacktrackingInput()
    {
        List<GameProceduralRoomTileSolverInput> tiles = new List<GameProceduralRoomTileSolverInput>
        {
            new GameProceduralRoomTileSolverInput(
                "BOSS_BACKTRACK_START_TECH",
                "BOSS_BACKTRACK_START",
                "SCN_BOSS_BACKTRACK_START",
                GameProceduralRoomRole.Start,
                1,
                new Vector2Int(0, 0),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("BOSS_BACKTRACK_START_EXIT",
                                 GameRoomPortalSide.North,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Required)
                }),
            new GameProceduralRoomTileSolverInput(
                "BOSS_BACKTRACK_REGULAR_TECH",
                "BOSS_BACKTRACK_REGULAR",
                "SCN_BOSS_BACKTRACK_REGULAR",
                GameProceduralRoomRole.Regular,
                1,
                new Vector2Int(1, 1),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("BOSS_BACKTRACK_REGULAR_ENTRANCE",
                                 GameRoomPortalSide.South,
                                 GameRoomPortalCapability.Both,
                                 GameRoomPortalConnectionPolicy.Optional),
                    CreatePortal("BOSS_BACKTRACK_REGULAR_VALID_EXIT",
                                 GameRoomPortalSide.North,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional),
                    CreatePortal("BOSS_BACKTRACK_REGULAR_INVALID_EXIT",
                                 GameRoomPortalSide.East,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional)
                }),
            new GameProceduralRoomTileSolverInput(
                "BOSS_BACKTRACK_BOSS_TECH",
                "BOSS_BACKTRACK_BOSS",
                "SCN_BOSS_BACKTRACK_BOSS",
                GameProceduralRoomRole.Boss,
                1,
                new Vector2Int(2, 2),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("BOSS_BACKTRACK_BOSS_ENTRANCE",
                                 GameRoomPortalSide.South,
                                 GameRoomPortalCapability.Entrance,
                                 GameRoomPortalConnectionPolicy.Optional)
                },
                true,
                2)
        };
        return new GameProceduralLevelSolverInput("BOSS_BACKTRACK_LEVEL_TECH",
                                                  "BOSS_BACKTRACK_LEVEL",
                                                  new Vector2Int(3, 3),
                                                  new Vector2Int(2, 2),
                                                  1f,
                                                  1f,
                                                  1f,
                                                  false,
                                                  false,
                                                  8,
                                                  4,
                                                  1,
                                                  tiles);
    }

    /// <summary>
    /// Creates a graph whose deeper preferred path has no eligible Regular candidate but whose direct Boss path is valid.
    /// </summary>
    /// <returns>Single-attempt solver input used to prove soft-depth fallback behavior.</returns>
    private static GameProceduralLevelSolverInput CreatePreferredDepthFallbackInput()
    {
        List<GameProceduralRoomTileSolverInput> tiles = new List<GameProceduralRoomTileSolverInput>
        {
            new GameProceduralRoomTileSolverInput(
                "DEPTH_FALLBACK_START_TECH",
                "DEPTH_FALLBACK_START",
                "SCN_DEPTH_FALLBACK_START",
                GameProceduralRoomRole.Start,
                1,
                new Vector2Int(0, 0),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("DEPTH_FALLBACK_START_EXIT",
                                 GameRoomPortalSide.North,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional)
                }),
            new GameProceduralRoomTileSolverInput(
                "DEPTH_FALLBACK_REGULAR_TECH",
                "DEPTH_FALLBACK_REGULAR",
                "SCN_DEPTH_FALLBACK_REGULAR",
                GameProceduralRoomRole.Regular,
                1,
                new Vector2Int(3, 3),
                1f,
                1,
                new GameProceduralRoomPortalSolverInput[]
                {
                    CreatePortal("DEPTH_FALLBACK_REGULAR_EXIT",
                                 GameRoomPortalSide.North,
                                 GameRoomPortalCapability.Exit,
                                 GameRoomPortalConnectionPolicy.Optional)
                },
                true,
                3),
            new GameProceduralRoomTileSolverInput(
                "DEPTH_FALLBACK_BOSS_TECH",
                "DEPTH_FALLBACK_BOSS",
                "SCN_DEPTH_FALLBACK_BOSS",
                GameProceduralRoomRole.Boss,
                1,
                new Vector2Int(1, 3),
                1f,
                1,
                Array.Empty<GameProceduralRoomPortalSolverInput>())
        };
        return new GameProceduralLevelSolverInput("DEPTH_FALLBACK_LEVEL_TECH",
                                                  "DEPTH_FALLBACK_LEVEL",
                                                  new Vector2Int(2, 4),
                                                  new Vector2Int(3, 3),
                                                  1f,
                                                  8f,
                                                  0f,
                                                  true,
                                                  false,
                                                  8,
                                                  4,
                                                  1,
                                                  tiles);
    }

    /// <summary>
    /// Creates one immutable portal input with concise test call sites.
    /// </summary>
    /// <param name="portalId">Stable portal ID.</param>
    /// <param name="side">Authored room side.</param>
    /// <param name="capability">Entrance, Exit or Both capability.</param>
    /// <param name="policy">Required, Optional or LevelExit policy.</param>
    /// <returns>Plain solver portal input.</returns>
    private static GameProceduralRoomPortalSolverInput CreatePortal(string portalId,
                                                                    GameRoomPortalSide side,
                                                                    GameRoomPortalCapability capability,
                                                                    GameRoomPortalConnectionPolicy policy)
    {
        return new GameProceduralRoomPortalSolverInput(portalId, side, capability, policy);
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Verifies two results produced from the same seed contain identical node and edge assignments.
    /// </summary>
    /// <param name="left">First generation result.</param>
    /// <param name="right">Repeated generation result.</param>
    private static void RequireGraphsEqual(GameProceduralLevelGenerationResult left,
                                           GameProceduralLevelGenerationResult right)
    {
        Require(left.Nodes.Count == right.Nodes.Count && left.Edges.Count == right.Edges.Count,
                "Repeated generation changed graph collection lengths.");

        for (int index = 0; index < left.Nodes.Count; index++)
        {
            GameProceduralLevelGraphNode leftNode = left.Nodes[index];
            GameProceduralLevelGraphNode rightNode = right.Nodes[index];
            Require(leftNode.NodeId == rightNode.NodeId &&
                    leftNode.Depth == rightNode.Depth &&
                    string.Equals(leftNode.TileTechnicalId, rightNode.TileTechnicalId, StringComparison.Ordinal),
                    "Repeated generation changed node " + index + ".");
        }

        for (int index = 0; index < left.Edges.Count; index++)
        {
            GameProceduralLevelGraphEdge leftEdge = left.Edges[index];
            GameProceduralLevelGraphEdge rightEdge = right.Edges[index];
            Require(leftEdge.SourceNodeId == rightEdge.SourceNodeId &&
                    leftEdge.TargetNodeId == rightEdge.TargetNodeId &&
                    string.Equals(leftEdge.SourcePortalId, rightEdge.SourcePortalId, StringComparison.Ordinal) &&
                    string.Equals(leftEdge.TargetPortalId, rightEdge.TargetPortalId, StringComparison.Ordinal),
                    "Repeated generation changed edge " + index + ".");
        }
    }

    /// <summary>
    /// Finds the single generated Boss node required by the solver contract.
    /// </summary>
    /// <param name="result">Successful generation result.</param>
    /// <returns>Boss node ID.</returns>
    private static int FindBossNode(GameProceduralLevelGenerationResult result)
    {
        for (int index = 0; index < result.Nodes.Count; index++)
        {
            if (result.Nodes[index].Role == GameProceduralRoomRole.Boss)
                return result.Nodes[index].NodeId;
        }

        throw new InvalidOperationException("GameProceduralLevelSolverSmokeTest: Generated graph has no Boss node.");
    }

    /// <summary>
    /// Throws one actionable smoke-test exception when a solver invariant is violated.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralLevelSolverSmokeTest: " + message);
    }
    #endregion

    #endregion
}
