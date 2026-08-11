using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Discovers and serializes the asset surface tracked by Player Management draft sessions.
/// </summary>
internal static class PlayerManagementDraftAssetUtility
{
    #region Constants
    internal const string PlayerAssetsRoot = "Assets/Scriptable Objects/Player";
    private const string ProjectRoot = "Assets";
    private const string PlayerPrefabRoot = "Assets/Prefabs/Player";
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Builds compact serialized state for every tracked Player preset, Input Actions asset, and PlayerAuthoring prefab.
    /// </summary>
    /// <returns>Path-keyed serialized state used for baseline comparisons and discard.</returns>
    internal static Dictionary<string, string> BuildStateDictionary()
    {
        Dictionary<string, string> stateByPath = new Dictionary<string, string>();
        List<string> assetPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
        {
            string assetPath = assetPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (assetObject != null)
                stateByPath[assetPath] = EditorJsonUtility.ToJson(assetObject);
        }

        return stateByPath;
    }

    /// <summary>
    /// Collects all asset paths participating in Player draft apply and discard operations.
    /// </summary>
    /// <returns>Ordinally sorted, duplicate-free project-relative asset paths.</returns>
    internal static List<string> CollectTrackedAssetPaths()
    {
        HashSet<string> uniquePaths = new HashSet<string>();
        AddTrackedPlayerAssetPaths(uniquePaths);
        AddAssetPathsOfType<InputActionAsset>(uniquePaths, ProjectRoot);
        AddPlayerPrefabPaths(uniquePaths);
        List<string> paths = new List<string>(uniquePaths);
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }
    #endregion

    #region Discovery Methods
    /// <summary>
    /// Collects every supported Player preset type with one indexed folder query instead of one query per type.
    /// </summary>
    /// <param name="uniquePaths">Output set receiving tracked project-relative asset paths.</param>
    private static void AddTrackedPlayerAssetPaths(HashSet<string> uniquePaths)
    {
        string[] guids = AssetDatabase.FindAssets(string.Empty, new string[] { PlayerAssetsRoot });

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (IsTrackedPlayerAssetType(AssetDatabase.GetMainAssetTypeAtPath(path)))
                uniquePaths.Add(path);
        }
    }

    /// <summary>
    /// Checks whether one main asset type belongs to the Player Management draft surface.
    /// </summary>
    /// <param name="assetType">Main asset type returned by the AssetDatabase.</param>
    /// <returns>True when the type participates in Player draft apply and discard.</returns>
    private static bool IsTrackedPlayerAssetType(Type assetType)
    {
        return assetType == typeof(PlayerMasterPresetLibrary) ||
               assetType == typeof(PlayerMasterPreset) ||
               assetType == typeof(PlayerControllerPresetLibrary) ||
               assetType == typeof(PlayerControllerPreset) ||
               assetType == typeof(PlayerProgressionPresetLibrary) ||
               assetType == typeof(PlayerProgressionPreset) ||
               assetType == typeof(PlayerPowerUpsPresetLibrary) ||
               assetType == typeof(PlayerPowerUpsPreset) ||
               assetType == typeof(PlayerVisualPresetLibrary) ||
               assetType == typeof(PlayerVisualPreset) ||
               assetType == typeof(PlayerUiVisualPresetLibrary) ||
               assetType == typeof(PlayerUiVisualPreset) ||
               assetType == typeof(PlayerAnimationBindingsPreset);
    }

    /// <summary>
    /// Adds all assets of one Unity type found below the provided search root.
    /// </summary>
    /// <typeparam name="TAsset">Unity asset type included in the draft surface.</typeparam>
    /// <param name="uniquePaths">Output set receiving asset paths.</param>
    /// <param name="searchRoot">Project-relative folder searched by the AssetDatabase.</param>
    private static void AddAssetPathsOfType<TAsset>(HashSet<string> uniquePaths, string searchRoot)
        where TAsset : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(searchRoot) || !AssetDatabase.IsValidFolder(searchRoot))
            return;

        string[] guids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, new string[] { searchRoot });

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);

            if (!string.IsNullOrWhiteSpace(path))
                uniquePaths.Add(path);
        }
    }

    /// <summary>
    /// Adds PlayerAuthoring prefab paths from the dedicated player prefab folder without scanning unrelated prefabs.
    /// </summary>
    /// <param name="uniquePaths">Output set receiving matching prefab paths.</param>
    private static void AddPlayerPrefabPaths(HashSet<string> uniquePaths)
    {
        if (!AssetDatabase.IsValidFolder(PlayerPrefabRoot))
            return;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { PlayerPrefabRoot });

        for (int guidIndex = 0; guidIndex < prefabGuids.Length; guidIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[guidIndex]);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefabAsset != null && prefabAsset.GetComponent<PlayerAuthoring>() != null)
                uniquePaths.Add(prefabPath);
        }
    }
    #endregion

    #endregion
}
