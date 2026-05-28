using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Synchronizes runtime spawner catalog data and owner SubScene bake artifacts before editor play mode starts.
/// </summary>
[InitializeOnLoad]
public static class EnemySpawnerRuntimeCatalogPlayModeSyncUtility
{
    #region Constants
    private const string SignatureEditorPrefsKey = "NashCore.EnemySpawnerRuntimeCatalog.PlayModeSyncSignature.v2";
    private const string SignatureVersion = "RuntimeSpawnerCatalogPlayModeSync.v2";
    private const string AssetSearchRoot = "Assets";
    private const string GameSceneManagerPresetSearchFilter = "t:GameSceneManagerPreset";
    private const string EnemyWavePresetSearchFilter = "t:EnemyWavePreset";
    private static readonly string[] RuntimeBakeScriptPaths =
    {
        "Assets/Scripts/Enemy/Authoring/EnemySpawnerAuthoringBaker.cs",
        "Assets/Scripts/Enemy/Authoring/EnemySpawnerRuntimeBakeMetadataUtility.cs",
        "Assets/Scripts/Enemy/Components/EnemySpawnerComponents.cs",
        "Assets/Scripts/Enemy/Config/EnemySpawnerRuntimeCatalog.cs",
        "Assets/Scripts/Enemy/Config/EnemySpawnerRuntimeOverrideStore.cs",
        "Assets/Scripts/Enemy/Config/EnemySpawnerRuntimeSceneOverrideUtility.cs",
        "Assets/Scripts/Enemy/Systems/Pool/EnemySpawnerRuntimeOverrideSystem.cs",
        "Assets/Scripts/Editor/Enemy/EnemySpawnerRuntimeCatalogPlayModeSyncUtility.cs"
    };
    #endregion

    #region Fields
    private static bool isSynchronizing;
    #endregion

    #region Methods

    #region Initialization
    /// <summary>
    /// Registers the editor play-mode hook after each domain reload.
    /// </summary>
    static EnemySpawnerRuntimeCatalogPlayModeSyncUtility()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Starts a pre-play synchronization pass before Unity loads play-mode scenes.
    /// </summary>
    /// <param name="state">Play-mode transition state reported by Unity.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        SynchronizeBeforePlayMode();
    }
    #endregion

    #region Synchronization
    /// <summary>
    /// Rebuilds the runtime catalog and reimports owner scenes when editor asset hashes changed since the last play-mode sync.
    /// </summary>
    private static void SynchronizeBeforePlayMode()
    {
        if (isSynchronizing)
            return;

        isSynchronizing = true;

        try
        {
            EnemySpawnerRuntimeCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemySpawnerRuntimeCatalog>(EnemySpawnerRuntimeCatalogBuildUtility.CatalogAssetPath);
            string currentSignature = BuildSynchronizationSignature(catalog);
            string storedSignature = EditorPrefs.GetString(SignatureEditorPrefsKey, string.Empty);

            if (!RequiresSynchronization(catalog, currentSignature, storedSignature))
                return;

            EnemySpawnerRuntimeBakeMetadataUtility.ClearRuntimeWavePresetCandidateCache();
            EnemySpawnerRuntimeCatalog rebuiltCatalog = EnemySpawnerRuntimeCatalogBuildUtility.RebuildCatalogAsset();
            EnemySpawnerRuntimeCatalogOwnerSceneImportUtility.ReimportOwnerScenes(rebuiltCatalog);
            EditorPrefs.SetString(SignatureEditorPrefsKey, BuildSynchronizationSignature(rebuiltCatalog));
        }
        finally
        {
            isSynchronizing = false;
        }
    }

    /// <summary>
    /// Resolves whether the catalog and owner scene artifacts need a pre-play synchronization pass.
    /// </summary>
    /// <param name="catalog">Catalog currently available in the project.</param>
    /// <param name="currentSignature">Fresh dependency signature built from editor assets.</param>
    /// <param name="storedSignature">Signature stored after the last successful synchronization.</param>
    /// <returns>True when a rebuild and owner-scene reimport should run.</returns>
    private static bool RequiresSynchronization(EnemySpawnerRuntimeCatalog catalog,
                                                string currentSignature,
                                                string storedSignature)
    {
        if (catalog == null)
            return true;

        if (string.IsNullOrWhiteSpace(storedSignature))
            return true;

        return !string.Equals(currentSignature, storedSignature, StringComparison.Ordinal);
    }
    #endregion

    #region Signature
    /// <summary>
    /// Builds a compact signature from catalog, scene, preset and bake-script dependency hashes.
    /// </summary>
    /// <param name="catalog">Catalog whose owner scenes should participate in the signature.</param>
    /// <returns>Stable hash string representing the relevant editor state.</returns>
    private static string BuildSynchronizationSignature(EnemySpawnerRuntimeCatalog catalog)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.Append(SignatureVersion).Append('\n');
        AppendAssetDependencyHash(builder, EnemySpawnerRuntimeCatalogBuildUtility.CatalogAssetPath);
        AppendAssetsBySearch(builder, GameSceneManagerPresetSearchFilter);
        AppendAssetsBySearch(builder, EnemyWavePresetSearchFilter);
        AppendRuntimeBakeScriptHashes(builder);
        AppendCatalogOwnerSceneHashes(builder, catalog);
        return Hash128.Compute(builder.ToString()).ToString();
    }

    /// <summary>
    /// Appends dependency hashes for every asset matched by one AssetDatabase search filter.
    /// </summary>
    /// <param name="builder">Signature builder receiving normalized asset hash lines.</param>
    /// <param name="searchFilter">AssetDatabase search filter to evaluate under Assets.</param>
    private static void AppendAssetsBySearch(StringBuilder builder, string searchFilter)
    {
        if (builder == null || string.IsNullOrWhiteSpace(searchFilter))
            return;

        List<string> assetPaths = new List<string>();
        string[] assetGuids = AssetDatabase.FindAssets(searchFilter, new[] { AssetSearchRoot });

        for (int assetIndex = 0; assetIndex < assetGuids.Length; assetIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[assetIndex]);

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            assetPaths.Add(assetPath.Replace('\\', '/'));
        }

        assetPaths.Sort(StringComparer.Ordinal);

        for (int assetIndex = 0; assetIndex < assetPaths.Count; assetIndex++)
            AppendAssetDependencyHash(builder, assetPaths[assetIndex]);
    }

    /// <summary>
    /// Appends hashes for the scripts that define runtime spawner variant baking and application.
    /// </summary>
    /// <param name="builder">Signature builder receiving script hash lines.</param>
    private static void AppendRuntimeBakeScriptHashes(StringBuilder builder)
    {
        if (builder == null)
            return;

        for (int scriptIndex = 0; scriptIndex < RuntimeBakeScriptPaths.Length; scriptIndex++)
            AppendAssetDependencyHash(builder, RuntimeBakeScriptPaths[scriptIndex]);
    }

    /// <summary>
    /// Appends dependency hashes for every scene or SubScene that owns cataloged enemy spawners.
    /// </summary>
    /// <param name="builder">Signature builder receiving scene hash lines.</param>
    /// <param name="catalog">Catalog used to resolve owner scene paths.</param>
    private static void AppendCatalogOwnerSceneHashes(StringBuilder builder, EnemySpawnerRuntimeCatalog catalog)
    {
        if (builder == null || catalog == null)
            return;

        HashSet<string> ownerScenePaths = new HashSet<string>();
        IReadOnlyList<EnemySpawnerRuntimeSceneEntry> sceneEntries = catalog.Scenes;

        for (int sceneIndex = 0; sceneIndex < sceneEntries.Count; sceneIndex++)
            CollectOwnerScenePaths(sceneEntries[sceneIndex], ownerScenePaths);

        List<string> sortedOwnerScenePaths = new List<string>(ownerScenePaths);
        sortedOwnerScenePaths.Sort(StringComparer.Ordinal);

        for (int sceneIndex = 0; sceneIndex < sortedOwnerScenePaths.Count; sceneIndex++)
            AppendAssetDependencyHash(builder, sortedOwnerScenePaths[sceneIndex]);
    }

    /// <summary>
    /// Collects the selected catalog scene path and the actual owner scene paths used by its spawners.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry to inspect.</param>
    /// <param name="ownerScenePaths">Target set receiving project-relative Unity scene paths.</param>
    private static void CollectOwnerScenePaths(EnemySpawnerRuntimeSceneEntry sceneEntry, HashSet<string> ownerScenePaths)
    {
        if (sceneEntry == null || ownerScenePaths == null)
            return;

        AddScenePath(sceneEntry.ScenePath, ownerScenePaths);
        IReadOnlyList<EnemySpawnerRuntimeSpawnerEntry> spawners = sceneEntry.Spawners;

        for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
        {
            EnemySpawnerRuntimeSpawnerEntry spawnerEntry = spawners[spawnerIndex];

            if (spawnerEntry == null)
                continue;

            string ownerSceneGuid = EnemySpawnerRuntimeSceneOverrideUtility.ResolveOverrideSceneGuid(sceneEntry, spawnerEntry);
            AddScenePath(AssetDatabase.GUIDToAssetPath(ownerSceneGuid), ownerScenePaths);
        }
    }

    /// <summary>
    /// Adds one normalized Unity scene path to the owner scene set.
    /// </summary>
    /// <param name="scenePath">Project-relative scene path candidate.</param>
    /// <param name="ownerScenePaths">Target set receiving valid scene paths.</param>
    private static void AddScenePath(string scenePath, HashSet<string> ownerScenePaths)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || ownerScenePaths == null)
            return;

        string normalizedPath = scenePath.Replace('\\', '/');

        if (!normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return;

        ownerScenePaths.Add(normalizedPath);
    }

    /// <summary>
    /// Appends one normalized dependency hash line for an asset path.
    /// </summary>
    /// <param name="builder">Signature builder receiving the asset hash line.</param>
    /// <param name="assetPath">Project-relative asset path to hash.</param>
    private static void AppendAssetDependencyHash(StringBuilder builder, string assetPath)
    {
        if (builder == null || string.IsNullOrWhiteSpace(assetPath))
            return;

        string normalizedPath = assetPath.Replace('\\', '/');
        Hash128 dependencyHash = AssetDatabase.GetAssetDependencyHash(normalizedPath);
        builder.Append(normalizedPath).Append('=').Append(dependencyHash.ToString()).Append('\n');
    }
    #endregion

    #endregion
}
