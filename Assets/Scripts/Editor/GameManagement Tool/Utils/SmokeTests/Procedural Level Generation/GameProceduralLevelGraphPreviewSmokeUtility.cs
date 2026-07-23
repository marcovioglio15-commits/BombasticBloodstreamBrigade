#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verifies converging-edge anchor separation and finalized-viewport graph fitting without opening an editor window.
/// </summary>
internal static class GameProceduralLevelGraphPreviewSmokeUtility
{
    #region Constants
    private const float ComparisonTolerance = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes deterministic graph preview layout and viewport checks inside the procedural bake smoke suite.
    /// </summary>
    public static void Validate()
    {
        GameProceduralLevelGraphPreviewLayout layout = CreateConvergingLayout();
        ValidateSeparatedConvergingAnchors(layout);
        ValidateFinalizedViewportFit(layout);
    }
    #endregion

    #region Fixture Methods
    /// <summary>
    /// Creates a branch-and-merge graph whose two differently colored source nodes share one target node.
    /// </summary>
    /// <returns>Immutable preview layout containing two converging incoming edges.</returns>
    private static GameProceduralLevelGraphPreviewLayout CreateConvergingLayout()
    {
        List<GameProceduralLevelGraphNode> nodes = new List<GameProceduralLevelGraphNode>
        {
            new GameProceduralLevelGraphNode(0,
                                             "START_TECH",
                                             "START",
                                             "SCN_START",
                                             GameProceduralRoomRole.Start,
                                             0,
                                             1),
            new GameProceduralLevelGraphNode(1,
                                             "UPPER_TECH",
                                             "UPPER",
                                             "SCN_UPPER",
                                             GameProceduralRoomRole.Regular,
                                             1,
                                             1),
            new GameProceduralLevelGraphNode(2,
                                             "LOWER_TECH",
                                             "LOWER",
                                             "SCN_LOWER",
                                             GameProceduralRoomRole.Regular,
                                             1,
                                             1),
            new GameProceduralLevelGraphNode(3,
                                             "MERGE_TECH",
                                             "MERGE",
                                             "SCN_MERGE",
                                             GameProceduralRoomRole.Regular,
                                             2,
                                             1)
        };
        List<GameProceduralLevelGraphEdge> edges = new List<GameProceduralLevelGraphEdge>
        {
            CreateEdge(0, 0, 1),
            CreateEdge(1, 0, 2),
            CreateEdge(2, 1, 3),
            CreateEdge(3, 2, 3)
        };
        GameProceduralLevelGenerationResult result =
            GameProceduralLevelGenerationResult.CreateSuccess(1u, 2u, 1, nodes, edges);
        return GameProceduralLevelGraphPreviewLayout.Build(result);
    }

    /// <summary>
    /// Creates one physically labelled east-to-west fixture edge.
    /// </summary>
    /// <param name="edgeId">Stable edge ID.</param>
    /// <param name="sourceNodeId">Source fixture node ID.</param>
    /// <param name="targetNodeId">Target fixture node ID.</param>
    /// <returns>Immutable graph edge used by the preview layout fixture.</returns>
    private static GameProceduralLevelGraphEdge CreateEdge(int edgeId, int sourceNodeId, int targetNodeId)
    {
        return new GameProceduralLevelGraphEdge(edgeId,
                                                sourceNodeId,
                                                targetNodeId,
                                                "EAST_EXIT",
                                                "WEST_ENTRANCE",
                                                GameRoomPortalSide.East,
                                                GameRoomPortalSide.West,
                                                false);
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Verifies that differently colored incoming edges cannot overwrite one shared target arrowhead.
    /// </summary>
    /// <param name="layout">Converging preview fixture layout.</param>
    private static void ValidateSeparatedConvergingAnchors(GameProceduralLevelGraphPreviewLayout layout)
    {
        Require(layout.TryGetEdge(2, out GameProceduralLevelGraphPreviewEdgeLayout upperEdge),
                "The preview layout omitted the upper converging edge.");
        Require(layout.TryGetEdge(3, out GameProceduralLevelGraphPreviewEdgeLayout lowerEdge),
                "The preview layout omitted the lower converging edge.");
        Require(upperEdge.TargetCount == 2 && lowerEdge.TargetCount == 2,
                "The preview layout did not retain the shared target edge count.");
        Require(upperEdge.TargetOrdinal != lowerEdge.TargetOrdinal,
                "Converging edges received the same target anchor ordinal.");
        Require(layout.TryGetNode(3, out GameProceduralLevelGraphPreviewNodeLayout targetNode),
                "The preview layout omitted the converging target node.");
        Vector3 upperPoint = GameProceduralLevelGraphPreviewViewportUtility.ResolveConnectionPoint(targetNode.Rect,
                                                                                                   false,
                                                                                                   upperEdge.TargetOrdinal,
                                                                                                   upperEdge.TargetCount);
        Vector3 lowerPoint = GameProceduralLevelGraphPreviewViewportUtility.ResolveConnectionPoint(targetNode.Rect,
                                                                                                   false,
                                                                                                   lowerEdge.TargetOrdinal,
                                                                                                   lowerEdge.TargetCount);
        Require(Mathf.Abs(upperPoint.y - lowerPoint.y) > ComparisonTolerance,
                "Converging edge arrowheads still overlap at the target node.");
    }

    /// <summary>
    /// Verifies that fitting waits for usable dimensions and centers the complete graph in a finalized canvas.
    /// </summary>
    /// <param name="layout">Converging preview fixture layout.</param>
    private static void ValidateFinalizedViewportFit(GameProceduralLevelGraphPreviewLayout layout)
    {
        bool placeholderFit = GameProceduralLevelGraphPreviewViewportUtility.TryResolveFit(layout.GraphBounds,
                                                                                           Vector2.one,
                                                                                           0f,
                                                                                           24f,
                                                                                           0.2f,
                                                                                           2.25f,
                                                                                           out float placeholderZoom,
                                                                                           out Vector2 placeholderOffset);
        Require(!placeholderFit,
                "Fit Graph accepted placeholder IMGUI layout dimensions.");

        Vector2 canvasSize = new Vector2(1900f, 1000f);
        bool finalFit = GameProceduralLevelGraphPreviewViewportUtility.TryResolveFit(layout.GraphBounds,
                                                                                     canvasSize,
                                                                                     0f,
                                                                                     24f,
                                                                                     0.2f,
                                                                                     2.25f,
                                                                                     out float zoom,
                                                                                     out Vector2 panOffset);
        Require(finalFit,
                "Fit Graph rejected a finalized graph canvas.");
        Rect fittedBounds = GameProceduralLevelGraphPreviewViewportUtility.TransformRect(layout.GraphBounds,
                                                                                         panOffset,
                                                                                         zoom);
        Require(fittedBounds.xMin >= 24f - ComparisonTolerance &&
                fittedBounds.xMax <= canvasSize.x - 24f + ComparisonTolerance &&
                fittedBounds.yMin >= 24f - ComparisonTolerance &&
                fittedBounds.yMax <= canvasSize.y - 24f + ComparisonTolerance,
                "Fit Graph placed complete graph bounds outside the padded canvas.");
        Require(Mathf.Abs(fittedBounds.center.x - canvasSize.x * 0.5f) <= ComparisonTolerance &&
                Mathf.Abs(fittedBounds.center.y - canvasSize.y * 0.5f) <= ComparisonTolerance,
                "Fit Graph did not center the graph in the available viewport.");

        // Preserve explicit out variables in the rejected case as part of the API contract check.
        Require(Mathf.Approximately(placeholderZoom, 1f) && placeholderOffset == Vector2.zero,
                "A rejected placeholder fit returned a partially applied viewport.");
    }

    /// <summary>
    /// Throws one actionable smoke failure when a preview invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result.</param>
    /// <param name="message">Failure diagnostic.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    #endregion

    #endregion
}
#endif
