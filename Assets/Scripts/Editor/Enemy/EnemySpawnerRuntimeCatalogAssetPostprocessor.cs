using System;
using UnityEditor;

/// <summary>
/// Keeps the runtime spawner catalog synchronized when scene assets, EnemyWavePreset assets or spawner bake scripts change in the editor.
/// </summary>
public sealed class EnemySpawnerRuntimeCatalogAssetPostprocessor : AssetPostprocessor
{
    #region Constants
    private const string EnemyAuthoringScriptDirectory = "Assets/Scripts/Enemy/Authoring/";
    private const string EnemyComponentScriptDirectory = "Assets/Scripts/Enemy/Components/";
    private const string EnemyConfigScriptDirectory = "Assets/Scripts/Enemy/Config/";
    private const string EnemyEditorScriptDirectory = "Assets/Scripts/Editor/Enemy/";
    private const string EnemySpawnerRuntimeOverrideSystemPath = "Assets/Scripts/Enemy/Systems/Pool/EnemySpawnerRuntimeOverrideSystem.cs";
    #endregion

    #region Fields
    private static bool refreshQueued;
    private static bool isRefreshing;
    private static bool pendingOwnerSceneReimport;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Schedules a catalog rebuild after relevant project assets are imported, deleted or moved.
    /// </summary>
    /// <param name="importedAssets">Assets imported by the editor refresh.</param>
    /// <param name="deletedAssets">Assets deleted by the editor refresh.</param>
    /// <param name="movedAssets">New asset paths after move operations.</param>
    /// <param name="movedFromAssetPaths">Previous asset paths before move operations.</param>
    private static void OnPostprocessAllAssets(string[] importedAssets,
                                               string[] deletedAssets,
                                               string[] movedAssets,
                                               string[] movedFromAssetPaths)
    {
        if (isRefreshing || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        bool hasRelevantCatalogChange = ContainsRelevantCatalogChange(importedAssets, true) ||
                                        ContainsRelevantCatalogChange(deletedAssets, false) ||
                                        ContainsRelevantCatalogChange(movedAssets, true) ||
                                        ContainsRelevantCatalogChange(movedFromAssetPaths, false);

        if (!hasRelevantCatalogChange)
            return;

        pendingOwnerSceneReimport = true;
        QueueCatalogRefresh();
    }
    #endregion

    #region Refresh
    /// <summary>
    /// Queues one delayed rebuild so bursts of asset imports collapse into a single catalog update.
    /// </summary>
    private static void QueueCatalogRefresh()
    {
        if (refreshQueued)
            return;

        refreshQueued = true;
        EditorApplication.delayCall += RefreshCatalog;
    }

    /// <summary>
    /// Rebuilds the runtime catalog outside the asset import callback.
    /// </summary>
    private static void RefreshCatalog()
    {
        EditorApplication.delayCall -= RefreshCatalog;
        refreshQueued = false;

        if (isRefreshing || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        isRefreshing = true;
        bool shouldReimportOwnerScenes = pendingOwnerSceneReimport;
        pendingOwnerSceneReimport = false;

        try
        {
            EnemySpawnerRuntimeBakeMetadataUtility.ClearRuntimeWavePresetCandidateCache();
            EnemySpawnerRuntimeCatalog catalog = EnemySpawnerRuntimeCatalogBuildUtility.RebuildCatalogAsset();

            if (shouldReimportOwnerScenes)
                EnemySpawnerRuntimeCatalogOwnerSceneImportUtility.ReimportOwnerScenes(catalog);
        }
        finally
        {
            isRefreshing = false;
        }
    }
    #endregion

    #region Filtering
    /// <summary>
    /// Checks whether an asset batch contains scene, EnemyWavePreset or bake-script changes relevant to the runtime catalog.
    /// </summary>
    /// <param name="assetPaths">Asset paths from one postprocessor batch category.</param>
    /// <param name="canInspectAssetType">True when the asset still exists and its imported type can be queried.</param>
    /// <returns>True when at least one path requires a catalog rebuild.</returns>
    private static bool ContainsRelevantCatalogChange(string[] assetPaths, bool canInspectAssetType)
    {
        if (assetPaths == null)
            return false;

        for (int assetIndex = 0; assetIndex < assetPaths.Length; assetIndex++)
        {
            if (IsCatalogRelevantAsset(assetPaths[assetIndex], canInspectAssetType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks one asset path for scene, wave-preset or bake-script relevance.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <param name="canInspectAssetType">True when AssetDatabase type inspection is available.</param>
    /// <returns>True when the asset should trigger a catalog rebuild.</returns>
    private static bool IsCatalogRelevantAsset(string assetPath, bool canInspectAssetType)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');

        if (normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsWavePresetAsset(normalizedPath, canInspectAssetType))
            return true;

        if (IsRuntimeSpawnerScriptAsset(normalizedPath))
            return true;

        return false;
    }

    /// <summary>
    /// Checks one asset path for EnemyWavePreset relevance.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <param name="canInspectAssetType">True when AssetDatabase type inspection is available.</param>
    /// <returns>True when the path points to a wave preset asset.</returns>
    private static bool IsWavePresetAsset(string assetPath, bool canInspectAssetType)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');

        if (canInspectAssetType && AssetDatabase.GetMainAssetTypeAtPath(normalizedPath) == typeof(EnemyWavePreset))
            return true;

        return normalizedPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
               normalizedPath.IndexOf("/Wave Preset/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Checks whether a script change can alter runtime spawner catalog entries, identities or baked variant buffers.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <returns>True when the script affects runtime spawner baking or catalog synchronization.</returns>
    private static bool IsRuntimeSpawnerScriptAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string normalizedPath = assetPath.Replace('\\', '/');

        if (!normalizedPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return false;

        if (normalizedPath.StartsWith(EnemyAuthoringScriptDirectory, StringComparison.Ordinal))
            return normalizedPath.IndexOf("EnemySpawner", StringComparison.Ordinal) >= 0 ||
                   normalizedPath.IndexOf("EnemyWavePreset", StringComparison.Ordinal) >= 0;

        if (normalizedPath.StartsWith(EnemyComponentScriptDirectory, StringComparison.Ordinal))
            return normalizedPath.IndexOf("EnemySpawner", StringComparison.Ordinal) >= 0;

        if (normalizedPath.StartsWith(EnemyConfigScriptDirectory, StringComparison.Ordinal))
            return normalizedPath.IndexOf("EnemySpawnerRuntime", StringComparison.Ordinal) >= 0;

        if (normalizedPath.StartsWith(EnemyEditorScriptDirectory, StringComparison.Ordinal))
            return normalizedPath.IndexOf("EnemySpawnerRuntimeCatalog", StringComparison.Ordinal) >= 0;

        return string.Equals(normalizedPath, EnemySpawnerRuntimeOverrideSystemPath, StringComparison.Ordinal);
    }
    #endregion

    #endregion
}
