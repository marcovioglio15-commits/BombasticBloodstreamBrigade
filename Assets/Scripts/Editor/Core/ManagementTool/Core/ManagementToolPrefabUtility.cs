using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared utility methods for finding and validating prefab assets used by management tools.
/// Used by the Player/Enemy Master Presets panels when resolving the target prefab in Active Preset sections.
/// </summary>
public static class ManagementToolPrefabUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds the first project prefab containing a component of type <typeparamref name="TComponent"/>.
    /// Used by the Active Preset "Find" button handlers in management panels.
    /// Takes in an output <see cref="GameObject"/> that receives the found prefab.
    /// </summary>
    /// <typeparam name="TComponent">Component type required on the prefab.</typeparam>
    /// <param name="prefabAsset">Output prefab reference set when a match is found.</param>
    /// <returns>Returns true when a matching prefab is found; otherwise false.</returns>
    public static bool TryFindFirstPrefabWithComponent<TComponent>(out GameObject prefabAsset) where TComponent : Component
    {
        return TryFindFirstPrefabWithComponentInHierarchy<TComponent>(out prefabAsset);
    }

    /// <summary>
    /// Finds the first project prefab containing a component of type <typeparamref name="TComponent"/>
    /// either on root or in any child object.
    /// </summary>
    /// <typeparam name="TComponent">Component type required on the prefab hierarchy.</typeparam>
    /// <param name="prefabAsset">Output prefab reference set when a match is found.</param>
    /// <returns>Returns true when a matching prefab is found; otherwise false.</returns>
    public static bool TryFindFirstPrefabWithComponentInHierarchy<TComponent>(out GameObject prefabAsset) where TComponent : Component
    {
        List<GameObject> matchingPrefabs = FindPrefabsWithComponentInHierarchy<TComponent>();

        if (matchingPrefabs.Count <= 0)
        {
            prefabAsset = null;
            return false;
        }

        prefabAsset = matchingPrefabs[0];
        return true;
    }

    /// <summary>
    /// Finds all project prefabs containing a component of type <typeparamref name="TComponent"/>
    /// either on root or in any child object.
    /// </summary>
    /// <typeparam name="TComponent">Component type required on the prefab hierarchy.</typeparam>
    /// <param name="searchFolders">Optional folder filters passed to AssetDatabase.FindAssets.</param>
    /// <returns>Returns a path-sorted list of matching prefab assets.</returns>
    public static List<GameObject> FindPrefabsWithComponentInHierarchy<TComponent>(string[] searchFolders = null) where TComponent : Component
    {
        List<GameObject> matchingPrefabs = new List<GameObject>();
        string[] prefabGuids = ResolvePrefabGuids(searchFolders);

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
            GameObject candidatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (candidatePrefab == null)
                continue;

            if (!HasComponentInHierarchy<TComponent>(candidatePrefab))
                continue;

            matchingPrefabs.Add(candidatePrefab);
        }

        matchingPrefabs.Sort(ComparePrefabByPath);
        return matchingPrefabs;
    }

    /// <summary>
    /// Checks whether a prefab contains a component of type <typeparamref name="TComponent"/>.
    /// Used by callers that need a quick validation before operating on a prefab reference.
    /// Takes in a prefab asset reference to validate.
    /// </summary>
    /// <typeparam name="TComponent">Component type expected on the prefab.</typeparam>
    /// <param name="prefabAsset">Prefab to validate.</param>
    /// <returns>Returns true if the prefab contains the component; otherwise false.</returns>
    public static bool HasComponent<TComponent>(GameObject prefabAsset) where TComponent : Component
    {
        return HasComponentInHierarchy<TComponent>(prefabAsset);
    }

    /// <summary>
    /// Checks whether a prefab contains a component of type <typeparamref name="TComponent"/>
    /// either on root or in any child object.
    /// </summary>
    /// <typeparam name="TComponent">Component type expected on the prefab hierarchy.</typeparam>
    /// <param name="prefabAsset">Prefab to validate.</param>
    /// <returns>Returns true if the prefab hierarchy contains the component; otherwise false.</returns>
    public static bool HasComponentInHierarchy<TComponent>(GameObject prefabAsset) where TComponent : Component
    {
        if (prefabAsset == null)
            return false;

        if (prefabAsset.GetComponent<TComponent>() != null)
            return true;

        return prefabAsset.GetComponentInChildren<TComponent>(true) != null;
    }
    #endregion

    #region Private Methods
    private static string[] ResolvePrefabGuids(string[] searchFolders)
    {
        if (searchFolders == null || searchFolders.Length <= 0)
            return AssetDatabase.FindAssets("t:Prefab");

        return AssetDatabase.FindAssets("t:Prefab", searchFolders);
    }

    private static int ComparePrefabByPath(GameObject leftPrefab, GameObject rightPrefab)
    {
        string leftPath = leftPrefab != null ? AssetDatabase.GetAssetPath(leftPrefab) : string.Empty;
        string rightPath = rightPrefab != null ? AssetDatabase.GetAssetPath(rightPrefab) : string.Empty;
        return string.Compare(leftPath, rightPath, StringComparison.Ordinal);
    }
    #endregion

    #endregion
}
