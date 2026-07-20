using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Computes stable aggregate hashes for root room scenes and all recursively scanned SubScenes.
/// </summary>
public static class GameRoomMetadataDependencyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Computes a deterministic dependency hash from the supplied project-relative scene paths.
    /// </summary>
    /// <param name="sourceScenePaths">Root and nested scene asset paths included by a metadata scan.</param>
    /// <returns>Aggregate dependency hash, or an empty string when no valid source path exists.</returns>
    public static string ComputeCombinedDependencyHash(IReadOnlyList<string> sourceScenePaths)
    {
        if (sourceScenePaths == null || sourceScenePaths.Count == 0)
            return string.Empty;

        List<string> sortedPaths = new List<string>(sourceScenePaths.Count);
        HashSet<string> uniquePaths = new HashSet<string>(StringComparer.Ordinal);

        // Normalize and deduplicate source paths before hashing so scan order cannot change cache identity.
        for (int index = 0; index < sourceScenePaths.Count; index++)
        {
            string path = NormalizeAssetPath(sourceScenePaths[index]);

            if (string.IsNullOrWhiteSpace(path) || !uniquePaths.Add(path))
                continue;

            sortedPaths.Add(path);
        }

        if (sortedPaths.Count == 0)
            return string.Empty;

        sortedPaths.Sort(StringComparer.Ordinal);
        StringBuilder hashInput = new StringBuilder(sortedPaths.Count * 96);

        // Include paths, GUIDs and Unity dependency hashes so moves and nested content changes invalidate the snapshot.
        for (int index = 0; index < sortedPaths.Count; index++)
        {
            string path = sortedPaths[index];
            hashInput.Append(path);
            hashInput.Append('|');
            hashInput.Append(AssetDatabase.AssetPathToGUID(path));
            hashInput.Append('|');
            hashInput.Append(AssetDatabase.GetAssetDependencyHash(path));
            hashInput.Append('\n');
        }

        return Hash128.Compute(hashInput.ToString()).ToString();
    }

    /// <summary>
    /// Normalizes one Unity asset path for ordinal cache comparisons.
    /// </summary>
    /// <param name="assetPath">Project-relative asset path.</param>
    /// <returns>Forward-slash path, or an empty string when unavailable.</returns>
    public static string NormalizeAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        return assetPath.Replace('\\', '/');
    }
    #endregion

    #endregion
}
