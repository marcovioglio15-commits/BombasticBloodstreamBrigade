#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Discovers managed portal scenes, accumulates every root anchor and resolves unique authoritative portal frames.
/// </summary>
internal static class GameRoomPortalSceneDiscoveryUtility
{
    #region Methods

    #region Scene Paths
    /// <summary>
    /// Collects all saved project scenes that can contain portal anchors and guarantees inclusion of the source scene.
    /// </summary>
    /// <param name="sourceAnchor">Selected source anchor whose scene must always be processed.</param>
    /// <returns>Distinct project-relative scene paths in deterministic ordinal order.</returns>
    internal static List<string> CollectCandidateScenePaths(
        GameRoomPortalRewardLogAnchor sourceAnchor)
    {
        HashSet<string> excludedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> discoveredPaths =
            GameRoomPortalEditorSceneDependencyUtility.FindCandidateScenePaths(excludedPaths);
        HashSet<string> uniquePaths =
            new HashSet<string>(discoveredPaths, StringComparer.OrdinalIgnoreCase);

        uniquePaths.Add(sourceAnchor.gameObject.scene.path);

        // Include loaded saved scenes from their current unsaved state even if their dependency cache is stale.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrWhiteSpace(scene.path) ||
                !SceneContainsAnchor(scene))
            {
                continue;
            }

            uniquePaths.Add(scene.path);
        }

        List<string> scenePaths = new List<string>(uniquePaths);
        scenePaths.Sort(StringComparer.OrdinalIgnoreCase);
        return scenePaths;
    }

    /// <summary>
    /// Reports whether one loaded scene currently contains any portal reward anchor.
    /// </summary>
    /// <param name="scene">Loaded scene inspected through its current hierarchy state.</param>
    /// <returns>True when at least one root hierarchy contains a portal reward anchor.</returns>
    private static bool SceneContainsAnchor(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            if (roots[rootIndex]
                    .GetComponentInChildren<GameRoomPortalRewardLogAnchor>(true) != null)
                return true;

        return false;
    }
    #endregion

    #region Anchors
    /// <summary>
    /// Collects portal anchors from every root of one loaded scene without including additive neighbor scenes.
    /// </summary>
    /// <param name="scene">Loaded managed room scene to scan.</param>
    /// <returns>All portal reward anchors found below every root object.</returns>
    internal static List<GameRoomPortalRewardLogAnchor> CollectAnchors(Scene scene)
    {
        List<GameRoomPortalRewardLogAnchor> anchors =
            new List<GameRoomPortalRewardLogAnchor>(8);
        List<GameRoomPortalRewardLogAnchor> rootAnchors =
            new List<GameRoomPortalRewardLogAnchor>(4);
        GameObject[] roots = scene.GetRootGameObjects();

        // Unity replaces the result list on every hierarchy query, so append each root result before continuing.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            rootAnchors.Clear();
            roots[rootIndex].GetComponentsInChildren(true, rootAnchors);
            anchors.AddRange(rootAnchors);
        }

        return anchors;
    }
    #endregion

    #region Portal Frames
    /// <summary>
    /// Builds an exact portal identity lookup and removes duplicated identities from synchronization eligibility.
    /// </summary>
    /// <param name="scene">Loaded managed room scene containing referenced portal SubScenes.</param>
    /// <param name="report">Aggregate report receiving duplicate identity failures.</param>
    /// <returns>Unique portal identities mapped to authoritative reference frames.</returns>
    internal static Dictionary<string, GameRoomPortalReferencePose> BuildUniquePortalPoseLookup(
        Scene scene,
        GameRoomPortalSynchronizationReport report)
    {
        List<GameRoomPortalReferencePose> poses =
            GameRoomRewardPortalManagedSceneSetupUtility.CollectPortalReferencePoses(scene);
        Dictionary<string, GameRoomPortalReferencePose> uniquePoses =
            new Dictionary<string, GameRoomPortalReferencePose>(StringComparer.Ordinal);
        HashSet<string> duplicateIds = new HashSet<string>(StringComparer.Ordinal);

        // Retain only identities that occur exactly once across all referenced SubScenes.
        for (int poseIndex = 0; poseIndex < poses.Count; poseIndex++)
        {
            GameRoomPortalReferencePose pose = poses[poseIndex];

            if (duplicateIds.Contains(pose.PortalId))
                continue;

            if (uniquePoses.ContainsKey(pose.PortalId))
            {
                uniquePoses.Remove(pose.PortalId);
                duplicateIds.Add(pose.PortalId);
                report.AddFailure(scene.path,
                                  null,
                                  "Portal Id '" + pose.PortalId +
                                  "' is duplicated across referenced SubScenes.");
                continue;
            }

            uniquePoses.Add(pose.PortalId, pose);
        }

        return uniquePoses;
    }
    #endregion

    #endregion
}
#endif
