using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reimports scene assets that own runtime-editable enemy spawners so baked variant buffers stay synchronized.
/// </summary>
public static class EnemySpawnerRuntimeCatalogOwnerSceneImportUtility
{
    #region Constants
    private const string RebuildMenuPath = "Tools/Enemy/Runtime Spawner Tool/Rebuild Catalog And Owner Scenes";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the runtime spawner catalog and immediately reimports every owner scene represented by it.
    /// </summary>
    //[MenuItem(RebuildMenuPath)]
    public static void RebuildCatalogAndReimportOwnerScenes()
    {
        EnemySpawnerRuntimeCatalog catalog = EnemySpawnerRuntimeCatalogBuildUtility.RebuildCatalogAsset();
        ReimportOwnerScenes(catalog);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Reimports every unique scene or SubScene asset that owns spawners represented by the catalog.
    /// </summary>
    /// <param name="catalog">Runtime spawner catalog to inspect.</param>
    public static void ReimportOwnerScenes(EnemySpawnerRuntimeCatalog catalog)
    {
        if (catalog == null)
            return;

        HashSet<string> ownerScenePaths = new HashSet<string>();
        IReadOnlyList<EnemySpawnerRuntimeSceneEntry> sceneEntries = catalog.Scenes;

        // Collect paths first so each owner scene is imported only once per refresh.
        for (int sceneIndex = 0; sceneIndex < sceneEntries.Count; sceneIndex++)
            CollectSpawnerOwnerScenePaths(sceneEntries[sceneIndex], ownerScenePaths);

        // Force the Entities importer to refresh baked runtime wave-preset variants.
        foreach (string ownerScenePath in ownerScenePaths)
            AssetDatabase.ImportAsset(ownerScenePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        ForceReimportReferencedSubScenes(catalog, ownerScenePaths);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds every owner scene path used by spawners in one catalog scene entry.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry to inspect.</param>
    /// <param name="ownerScenePaths">Target path set receiving Unity scene assets.</param>
    private static void CollectSpawnerOwnerScenePaths(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                                      HashSet<string> ownerScenePaths)
    {
        if (sceneEntry == null || ownerScenePaths == null)
            return;

        IReadOnlyList<EnemySpawnerRuntimeSpawnerEntry> spawners = sceneEntry.Spawners;

        // Resolve each row against the actual owner scene GUID baked into ECS identities.
        for (int spawnerIndex = 0; spawnerIndex < spawners.Count; spawnerIndex++)
            AddOwnerScenePath(sceneEntry, spawners[spawnerIndex], ownerScenePaths);
    }

    /// <summary>
    /// Resolves and adds the owner scene path for one spawner entry when it points to a Unity scene asset.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry currently being scanned.</param>
    /// <param name="spawnerEntry">Spawner entry that owns the runtime identity.</param>
    /// <param name="ownerScenePaths">Target path set receiving Unity scene assets.</param>
    private static void AddOwnerScenePath(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                          EnemySpawnerRuntimeSpawnerEntry spawnerEntry,
                                          HashSet<string> ownerScenePaths)
    {
        if (spawnerEntry == null)
            return;

        string ownerSceneGuid = EnemySpawnerRuntimeSceneOverrideUtility.ResolveOverrideSceneGuid(sceneEntry, spawnerEntry);
        string ownerScenePath = AssetDatabase.GUIDToAssetPath(ownerSceneGuid);

        if (string.IsNullOrWhiteSpace(ownerScenePath))
            return;

        if (!ownerScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return;

        ownerScenePaths.Add(ownerScenePath);
    }

    /// <summary>
    /// Forces Entities to dirty and rebuild EntityScene artifacts for SubScene components that reference owner scene assets.
    /// </summary>
    /// <param name="catalog">Runtime spawner catalog whose parent scenes can contain SubScene references.</param>
    /// <param name="ownerScenePaths">Scene assets that own baked spawner entities.</param>
    private static void ForceReimportReferencedSubScenes(EnemySpawnerRuntimeCatalog catalog,
                                                         HashSet<string> ownerScenePaths)
    {
        if (catalog == null || ownerScenePaths == null || ownerScenePaths.Count <= 0)
            return;

        List<SubScene> subScenes = new List<SubScene>();
        HashSet<string> collectedSubSceneGuids = new HashSet<string>();
        IReadOnlyList<EnemySpawnerRuntimeSceneEntry> sceneEntries = catalog.Scenes;
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int sceneIndex = 0; sceneIndex < sceneEntries.Count; sceneIndex++)
                CollectReferencedSubScenes(sceneEntries[sceneIndex],
                                           ownerScenePaths,
                                           collectedSubSceneGuids,
                                           subScenes);
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        if (subScenes.Count <= 0)
            return;

        DefaultWorldInitialization.DefaultLazyEditModeInitialize();
        SubSceneInspectorUtility.ForceReimport(subScenes.ToArray());
    }

    /// <summary>
    /// Opens one catalog parent scene and collects SubScene components whose scene assets own runtime-editable spawners.
    /// </summary>
    /// <param name="sceneEntry">Catalog scene entry that may reference owner SubScenes.</param>
    /// <param name="ownerScenePaths">Scene asset paths that must have EntityScene artifacts refreshed.</param>
    /// <param name="collectedSubSceneGuids">Uniqueness guard for SubScene assets already collected.</param>
    /// <param name="subScenes">Target SubScene component list passed to the Entities editor reimport utility.</param>
    private static void CollectReferencedSubScenes(EnemySpawnerRuntimeSceneEntry sceneEntry,
                                                   HashSet<string> ownerScenePaths,
                                                   HashSet<string> collectedSubSceneGuids,
                                                   List<SubScene> subScenes)
    {
        if (sceneEntry == null || ownerScenePaths == null || collectedSubSceneGuids == null || subScenes == null)
            return;

        string scenePath = sceneEntry.ScenePath;

        if (string.IsNullOrWhiteSpace(scenePath))
            return;

        if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return;

        Scene openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        if (!openedScene.IsValid())
            return;

        CollectReferencedSubScenes(openedScene, ownerScenePaths, collectedSubSceneGuids, subScenes);
    }

    /// <summary>
    /// Scans loaded scene roots for matching SubScene components.
    /// </summary>
    /// <param name="scene">Loaded parent scene to inspect.</param>
    /// <param name="ownerScenePaths">Scene asset paths that must have EntityScene artifacts refreshed.</param>
    /// <param name="collectedSubSceneGuids">Uniqueness guard for SubScene assets already collected.</param>
    /// <param name="subScenes">Target SubScene component list passed to the Entities editor reimport utility.</param>
    private static void CollectReferencedSubScenes(Scene scene,
                                                   HashSet<string> ownerScenePaths,
                                                   HashSet<string> collectedSubSceneGuids,
                                                   List<SubScene> subScenes)
    {
        if (!scene.IsValid())
            return;

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            SubScene[] foundSubScenes = rootObjects[rootIndex].GetComponentsInChildren<SubScene>(true);

            for (int subSceneIndex = 0; subSceneIndex < foundSubScenes.Length; subSceneIndex++)
                AddReferencedSubScene(foundSubScenes[subSceneIndex],
                                      ownerScenePaths,
                                      collectedSubSceneGuids,
                                      subScenes);
        }
    }

    /// <summary>
    /// Adds one SubScene component when it points to a scene asset that owns runtime-editable spawners.
    /// </summary>
    /// <param name="subScene">SubScene component found inside a catalog parent scene.</param>
    /// <param name="ownerScenePaths">Scene asset paths that must have EntityScene artifacts refreshed.</param>
    /// <param name="collectedSubSceneGuids">Uniqueness guard for SubScene assets already collected.</param>
    /// <param name="subScenes">Target SubScene component list passed to the Entities editor reimport utility.</param>
    private static void AddReferencedSubScene(SubScene subScene,
                                              HashSet<string> ownerScenePaths,
                                              HashSet<string> collectedSubSceneGuids,
                                              List<SubScene> subScenes)
    {
        if (subScene == null)
            return;

        string subScenePath = ResolveSubSceneAssetPath(subScene);

        if (string.IsNullOrWhiteSpace(subScenePath))
            return;

        if (!ownerScenePaths.Contains(subScenePath))
            return;

        string subSceneGuid = AssetDatabase.AssetPathToGUID(subScenePath);

        if (string.IsNullOrWhiteSpace(subSceneGuid))
            return;

        if (!collectedSubSceneGuids.Add(subSceneGuid))
            return;

        subScenes.Add(subScene);
    }

    /// <summary>
    /// Resolves the project-relative scene path referenced by one DOTS SubScene component.
    /// </summary>
    /// <param name="subScene">SubScene component to inspect.</param>
    /// <returns>Project-relative scene path, or an empty string when the reference is missing.</returns>
    private static string ResolveSubSceneAssetPath(SubScene subScene)
    {
        if (subScene == null)
            return string.Empty;

        SerializedObject serializedObject = new SerializedObject(subScene);
        SerializedProperty sceneAssetProperty = serializedObject.FindProperty("_SceneAsset");
        SceneAsset sceneAsset = sceneAssetProperty != null ? sceneAssetProperty.objectReferenceValue as SceneAsset : null;

        if (sceneAsset == null)
            return string.Empty;

        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);

        if (string.IsNullOrWhiteSpace(scenePath))
            return string.Empty;

        return scenePath.Replace('\\', '/');
    }
    #endregion

    #endregion
}
