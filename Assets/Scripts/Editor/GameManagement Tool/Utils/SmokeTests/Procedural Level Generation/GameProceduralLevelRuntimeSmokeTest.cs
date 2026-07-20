#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs procedural-level runtime coverage and the legacy run-outcome compatibility regression check.
/// </summary>
public static class GameProceduralLevelRuntimeSmokeTest
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the complete procedural runtime smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        GameProceduralLevelBakeSmokeTest.Run();
        GameProceduralLevelRunRequestSmokeTest.Run();
        GameProceduralLevelSolverDeterminismSmokeTest.Run();
        GameProceduralSceneTransitionPurposeSmokeTest.Run();
        GameProceduralRoomTraversalSmokeTest.Run();
        GameProceduralRoomPortalBlockingSmokeTest.Run();
        GameProceduralRoomClearSmokeTest.Run();
        GameLegacyRoomVictorySmokeTest.Run();
        ValidateSharedRendererLayerRestoration();
        Debug.Log("[GameProceduralLevelRuntimeSmokeTest] Procedural runtime and legacy compatibility smoke suites passed.");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Verifies multiple renderer components on one player object retain one original layer snapshot through teardown.
    /// </summary>
    private static void ValidateSharedRendererLayerRestoration()
    {
        GameObject playerObject = null;

        try
        {
            int transitionLayer = LayerMask.NameToLayer(GameSceneCameraLayerUtility.PlayerTransitionLayerName);
            int originalLayer = LayerMask.NameToLayer("Default");
            Require(transitionLayer >= 0,
                    "The PlayerTransition layer required by renderer-isolation coverage is missing.");
            Require(originalLayer >= 0 && originalLayer != transitionLayer,
                    "The renderer-isolation fixture could not resolve a distinct original layer.");

            playerObject = new GameObject("GameProceduralPlayerRenderer_Smoke");
            playerObject.layer = originalLayer;
            Animator animator = playerObject.AddComponent<Animator>();
            playerObject.AddComponent<MeshRenderer>();
            playerObject.AddComponent<LineRenderer>();
            Renderer[] renderers = playerObject.GetComponents<Renderer>();
            Require(renderers.Length >= 2,
                    "The renderer-isolation fixture did not create two renderer components on one GameObject.");
            Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
            int movedCount = GameSceneCameraLayerUtility.MoveRendererObjectsToLayer(animator,
                                                                                    transitionLayer,
                                                                                    originalLayers);
            Require(movedCount == 1 && originalLayers.Count == 1,
                    "Renderer isolation did not deduplicate multiple components on one GameObject.");
            Require(playerObject.layer == transitionLayer,
                    "Player renderer isolation did not move the shared GameObject to PlayerTransition.");

            // Destroy the first renderer snapshot key to make per-component restoration fail deterministically.
            UnityEngine.Object.DestroyImmediate(renderers[0]);
            int restoredCount = GameSceneCameraLayerUtility.RestoreRendererObjectLayers(originalLayers);
            Require(restoredCount == 1 && originalLayers.Count == 0,
                    "Renderer isolation did not consume exactly one surviving GameObject snapshot.");
            Require(playerObject.layer == originalLayer,
                    "Player renderer isolation did not restore the shared GameObject's original layer exactly once.");
        }
        finally
        {
            if (playerObject != null)
                UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    /// <summary>
    /// Throws one actionable smoke-test failure when a runtime invariant is violated.
    /// </summary>
    /// <param name="condition">Invariant result.</param>
    /// <param name="message">Failure diagnostic.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralLevelRuntimeSmokeTest: " + message);
    }
    #endregion

    #endregion
}
#endif
