#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verifies stable seed derivation and byte-for-byte equivalent graph decisions for repeated solver inputs.
/// </summary>
public static class GameProceduralLevelSolverDeterminismSmokeTest
{
    #region Constants
    private const uint FixtureRunSeed = 0x10203040u;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes deterministic pure-solver checks from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        GameProceduralLevelSolverInput input = CreateLinearCenterArrivalInput();
        GameProceduralLevelGenerationResult first = GameProceduralLevelSolver.Generate(input, FixtureRunSeed);
        GameProceduralLevelGenerationResult second = GameProceduralLevelSolver.Generate(input, FixtureRunSeed);

        Require(first.Success, "The first deterministic fixture generation failed: " + first.Diagnostic);
        Require(second.Success, "The repeated deterministic fixture generation failed: " + second.Diagnostic);
        Require(first.RunSeed == FixtureRunSeed && second.RunSeed == FixtureRunSeed,
                "The generation result did not preserve the authoritative run seed.");
        Require(first.LevelSeed == second.LevelSeed,
                "Repeated generation derived different per-level seeds.");
        Require(first.LevelSeed == GameProceduralLevelSolver.DeriveLevelSeed(FixtureRunSeed, input.LevelTechnicalId),
                "The result level seed differs from the public deterministic derivation API.");
        Require(first.AttemptsUsed == second.AttemptsUsed,
                "Repeated generation consumed a different number of bounded attempts.");

        CompareNodes(first, second);
        CompareEdges(first, second);
        ValidateSeedIsolation(input.LevelTechnicalId, first.LevelSeed);
        Debug.Log("[GameProceduralLevelSolverDeterminismSmokeTest] All deterministic solver checks passed.");
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates a four-node linear graph fixture that intentionally uses center arrival while retaining physical exits.
    /// </summary>
    /// <returns>Immutable valid solver input.</returns>
    private static GameProceduralLevelSolverInput CreateLinearCenterArrivalInput()
    {
        List<GameProceduralRoomPortalSolverInput> exits = new List<GameProceduralRoomPortalSolverInput>
        {
            new GameProceduralRoomPortalSolverInput("EAST_EXIT",
                                                    GameRoomPortalSide.East,
                                                    GameRoomPortalCapability.Exit,
                                                    GameRoomPortalConnectionPolicy.Required)
        };
        List<GameProceduralRoomTileSolverInput> tiles = new List<GameProceduralRoomTileSolverInput>
        {
            new GameProceduralRoomTileSolverInput("START_TECH",
                                                  "START",
                                                  "SCN_START",
                                                  GameProceduralRoomRole.Start,
                                                  1,
                                                  new Vector2Int(0, 0),
                                                  1f,
                                                  1,
                                                  exits),
            new GameProceduralRoomTileSolverInput("REGULAR_TECH",
                                                  "REGULAR",
                                                  "SCN_REGULAR",
                                                  GameProceduralRoomRole.Regular,
                                                  2,
                                                  new Vector2Int(1, 2),
                                                  1f,
                                                  1,
                                                  exits),
            new GameProceduralRoomTileSolverInput("BOSS_TECH",
                                                  "BOSS",
                                                  "SCN_BOSS",
                                                  GameProceduralRoomRole.Boss,
                                                  1,
                                                  new Vector2Int(3, 3),
                                                  1f,
                                                  1,
                                                  Array.Empty<GameProceduralRoomPortalSolverInput>())
        };

        return new GameProceduralLevelSolverInput("LEVEL_TECH_DETERMINISM",
                                                   "LEVEL_DETERMINISM",
                                                   new Vector2Int(4, 4),
                                                   new Vector2Int(3, 3),
                                                   1f,
                                                   1f,
                                                   1f,
                                                   true,
                                                   false,
                                                   4,
                                                   3,
                                                   8,
                                                   tiles);
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Compares every immutable node decision produced by two equal solver runs.
    /// </summary>
    /// <param name="first">First generation result.</param>
    /// <param name="second">Repeated generation result.</param>
    private static void CompareNodes(GameProceduralLevelGenerationResult first,
                                     GameProceduralLevelGenerationResult second)
    {
        Require(first.Nodes.Count == 4, "The linear fixture did not produce exactly four nodes.");
        Require(first.Nodes.Count == second.Nodes.Count, "Repeated generation produced a different node count.");

        // Compare every identity and placement field persisted into runtime graph buffers.
        for (int index = 0; index < first.Nodes.Count; index++)
        {
            GameProceduralLevelGraphNode left = first.Nodes[index];
            GameProceduralLevelGraphNode right = second.Nodes[index];
            Require(left.NodeId == right.NodeId &&
                    left.TileTechnicalId == right.TileTechnicalId &&
                    left.TileId == right.TileId &&
                    left.SceneId == right.SceneId &&
                    left.Role == right.Role &&
                    left.Depth == right.Depth &&
                    left.CopyOrdinal == right.CopyOrdinal,
                    "Node " + index + " differs between repeated runs.");
        }

        Require(first.Nodes[0].Role == GameProceduralRoomRole.Start && first.Nodes[0].Depth == 0,
                "The generated root is not the Start node at depth zero.");
        Require(first.Nodes[first.Nodes.Count - 1].Role == GameProceduralRoomRole.Boss &&
                first.Nodes[first.Nodes.Count - 1].Depth == 3,
                "The generated terminal node is not the Boss at the requested depth.");
    }

    /// <summary>
    /// Compares every directed edge and verifies center-arrival portal semantics.
    /// </summary>
    /// <param name="first">First generation result.</param>
    /// <param name="second">Repeated generation result.</param>
    private static void CompareEdges(GameProceduralLevelGenerationResult first,
                                     GameProceduralLevelGenerationResult second)
    {
        Require(first.Edges.Count == 3, "The four-node linear fixture did not produce exactly three edges.");
        Require(first.Edges.Count == second.Edges.Count, "Repeated generation produced a different edge count.");

        // Compare graph connectivity and exact physical source assignments retained in center-arrival mode.
        for (int index = 0; index < first.Edges.Count; index++)
        {
            GameProceduralLevelGraphEdge left = first.Edges[index];
            GameProceduralLevelGraphEdge right = second.Edges[index];
            Require(left.EdgeId == right.EdgeId &&
                    left.SourceNodeId == right.SourceNodeId &&
                    left.TargetNodeId == right.TargetNodeId &&
                    left.SourcePortalId == right.SourcePortalId &&
                    left.TargetPortalId == right.TargetPortalId &&
                    left.SourceSide == right.SourceSide &&
                    left.TargetSide == right.TargetSide &&
                    left.UsesCenterArrival == right.UsesCenterArrival,
                    "Edge " + index + " differs between repeated runs.");
            Require(left.UsesCenterArrival && string.IsNullOrEmpty(left.TargetPortalId),
                    "Center-arrival generation unexpectedly reserved a target entrance.");
        }
    }

    /// <summary>
    /// Verifies run and level identity independently contribute to the derived random stream.
    /// </summary>
    /// <param name="levelTechnicalId">Fixture level identity.</param>
    /// <param name="fixtureLevelSeed">Seed derived for the fixture run and level.</param>
    private static void ValidateSeedIsolation(string levelTechnicalId, uint fixtureLevelSeed)
    {
        uint differentRunSeed = GameProceduralLevelSolver.DeriveLevelSeed(FixtureRunSeed + 1u, levelTechnicalId);
        uint differentLevelSeed = GameProceduralLevelSolver.DeriveLevelSeed(FixtureRunSeed, levelTechnicalId + "_OTHER");
        Require(fixtureLevelSeed != 0u, "The deterministic derivation returned the reserved zero seed.");
        Require(differentRunSeed != fixtureLevelSeed,
                "Changing the run seed did not isolate the per-level random stream.");
        Require(differentLevelSeed != fixtureLevelSeed,
                "Changing the stable level identity did not isolate the per-level random stream.");
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when an invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralLevelSolverDeterminismSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
