#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Filters project scenes by direct portal-anchor and portal-prefab dependencies without opening unrelated assets.
/// </summary>
internal static class GameRoomPortalEditorSceneDependencyUtility
{
    #region Constants
    private const string PrefabExtension = ".prefab";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds unloaded scene assets that can contain portal reward anchors through direct scene or prefab references.
    /// </summary>
    /// <param name="excludedScenePaths">Loaded scene paths already inspected from their current unsaved state.</param>
    /// <returns>Project-relative scene paths requiring additive catalog inspection.</returns>
    public static List<string> FindCandidateScenePaths(HashSet<string> excludedScenePaths)
    {
        List<string> candidateScenePaths = new List<string>();
        string anchorScriptPath = ResolveAnchorScriptPath();

        if (string.IsNullOrWhiteSpace(anchorScriptPath))
            return candidateScenePaths;

        Dictionary<string, bool> prefabResults =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

        // Direct scene dependencies exclude unrelated assets reached through global configuration graphs.
        for (int sceneIndex = 0; sceneIndex < sceneGuids.Length; sceneIndex++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[sceneIndex]);

            if (excludedScenePaths.Contains(scenePath) ||
                !SceneCanContainAnchor(scenePath, anchorScriptPath, prefabResults))
            {
                continue;
            }

            candidateScenePaths.Add(scenePath);
        }

        return candidateScenePaths;
    }
    #endregion

    #region Dependency Resolution
    /// <summary>
    /// Reports whether a scene directly owns an anchor component or references a prefab containing one.
    /// </summary>
    /// <param name="scenePath">Project-relative scene asset path.</param>
    /// <param name="anchorScriptPath">Resolved project-relative anchor script path.</param>
    /// <param name="prefabResults">Per-build prefab dependency cache shared across scene checks.</param>
    /// <returns>True when opening the scene can contribute portal binding entries.</returns>
    private static bool SceneCanContainAnchor(string scenePath,
                                              string anchorScriptPath,
                                              Dictionary<string, bool> prefabResults)
    {
        string[] directDependencies = AssetDatabase.GetDependencies(scenePath, false);

        for (int dependencyIndex = 0;
             dependencyIndex < directDependencies.Length;
             dependencyIndex++)
        {
            string dependencyPath = directDependencies[dependencyIndex];

            if (string.Equals(dependencyPath, anchorScriptPath, StringComparison.Ordinal))
                return true;

            if (!dependencyPath.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!prefabResults.TryGetValue(dependencyPath, out bool containsAnchor))
            {
                containsAnchor = AssetDependsOnScript(dependencyPath, anchorScriptPath);
                prefabResults.Add(dependencyPath, containsAnchor);
            }

            if (containsAnchor)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks one prefab and its nested prefab graph for the portal anchor script.
    /// </summary>
    /// <param name="assetPath">Project-relative prefab asset path.</param>
    /// <param name="anchorScriptPath">Resolved project-relative anchor script path.</param>
    /// <returns>True when the dependency graph contains the portal anchor component script.</returns>
    private static bool AssetDependsOnScript(string assetPath, string anchorScriptPath)
    {
        string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);

        for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
        {
            if (string.Equals(dependencies[dependencyIndex],
                              anchorScriptPath,
                              StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the portal anchor script asset without coupling the catalog to its current folder.
    /// </summary>
    /// <returns>Project-relative script path, or an empty value when the script cannot be found.</returns>
    private static string ResolveAnchorScriptPath()
    {
        string anchorTypeName = nameof(GameRoomPortalRewardLogAnchor);
        string expectedFileName = anchorTypeName + ".cs";
        string[] scriptGuids = AssetDatabase.FindAssets(anchorTypeName + " t:MonoScript");

        for (int scriptIndex = 0; scriptIndex < scriptGuids.Length; scriptIndex++)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[scriptIndex]);

            if (string.Equals(System.IO.Path.GetFileName(scriptPath),
                              expectedFileName,
                              StringComparison.Ordinal))
            {
                return scriptPath;
            }
        }

        return string.Empty;
    }
    #endregion

    #endregion
}
#endif
