using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates the runtime enemy spawner catalog consumed by the main-menu spawner tool.
/// </summary>
public static class EnemySpawnerRuntimeCatalogBuildUtility
{
    #region Constants
    public const string CatalogAssetPath = "Assets/Resources/EnemySpawnerRuntimeCatalog.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the runtime catalog asset from project scenes, subscenes and EnemyWavePreset folders.
    /// </summary>
    /// <returns>Generated runtime catalog asset.</returns>
    public static EnemySpawnerRuntimeCatalog RebuildCatalogAsset()
    {
        EnemySpawnerRuntimeBakeMetadataUtility.ClearRuntimeWavePresetCandidateCache();
        List<EnemySpawnerRuntimeSceneEntry> sceneEntries = BuildSceneEntries();
        List<EnemySpawnerRuntimeWavePresetFolderEntry> folderEntries = BuildWavePresetFolderEntries();
        EnsureFolder(Path.GetDirectoryName(CatalogAssetPath));
        EnemySpawnerRuntimeCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemySpawnerRuntimeCatalog>(CatalogAssetPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EnemySpawnerRuntimeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.Assign(sceneEntries, folderEntries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }
    #endregion

    #region Scene Catalog
    /// <summary>
    /// Builds scene entries by opening project scenes and aggregating direct spawners plus referenced SubScene spawners.
    /// </summary>
    /// <returns>Generated scene entries that contain at least one enemy spawner.</returns>
    private static List<EnemySpawnerRuntimeSceneEntry> BuildSceneEntries()
    {
        List<EnemySpawnerRuntimeSceneEntry> sceneEntries = new List<EnemySpawnerRuntimeSceneEntry>();
        HashSet<string> referencedSubScenePaths = new HashSet<string>();
        List<string> scenePaths = BuildManagedScenePaths();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int sceneIndex = 0; sceneIndex < scenePaths.Count; sceneIndex++)
            {
                string scenePath = scenePaths[sceneIndex];

                if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.EndsWith(".unity"))
                    continue;

                Scene openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                bool sceneChanged;
                List<EnemySpawnerRuntimeSpawnerEntry> spawnerEntries = new List<EnemySpawnerRuntimeSpawnerEntry>();
                HashSet<string> uniqueSpawnerKeys = new HashSet<string>();
                List<EnemySpawnerRuntimeSpawnerEntry> directSpawnerEntries = BuildSpawnerEntries(openedScene,
                                                                                                 sceneGuid,
                                                                                                 string.Empty,
                                                                                                 out sceneChanged);
                AddUniqueSpawnerEntries(spawnerEntries, directSpawnerEntries, uniqueSpawnerKeys);
                AppendReferencedSubSceneSpawners(openedScene,
                                                 spawnerEntries,
                                                 uniqueSpawnerKeys,
                                                 referencedSubScenePaths);

                if (spawnerEntries.Count > 0)
                {
                    EnemySpawnerRuntimeSceneEntry sceneEntry = new EnemySpawnerRuntimeSceneEntry();
                    sceneEntry.Assign(Path.GetFileNameWithoutExtension(scenePath),
                                      scenePath,
                                      sceneGuid,
                                      spawnerEntries);
                    sceneEntries.Add(sceneEntry);
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(openedScene);
                    EditorSceneManager.SaveScene(openedScene);
                }

                EditorSceneManager.CloseScene(openedScene, true);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        RemoveReferencedSubSceneEntries(sceneEntries, referencedSubScenePaths);
        sceneEntries.Sort(CompareSceneEntries);
        return sceneEntries;
    }

    /// <summary>
    /// Builds the scene path list that can actually participate in the runtime scene flow.
    /// </summary>
    /// <returns>Project-relative managed scene paths sorted for deterministic catalog output.</returns>
    private static List<string> BuildManagedScenePaths()
    {
        List<string> scenePaths = new List<string>();
        HashSet<string> uniqueScenePaths = new HashSet<string>();
        string[] presetGuids = AssetDatabase.FindAssets("t:GameSceneManagerPreset", new[] { "Assets" });

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            GameSceneManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(presetPath);
            AddManagedScenePaths(preset, scenePaths, uniqueScenePaths);
        }

        scenePaths.Sort(System.StringComparer.Ordinal);
        return scenePaths;
    }

    /// <summary>
    /// Adds scene paths from one Game Scene Manager preset without touching unrelated test scenes.
    /// </summary>
    /// <param name="preset">Scene manager preset to inspect.</param>
    /// <param name="scenePaths">Target path list.</param>
    /// <param name="uniqueScenePaths">Uniqueness guard for project-relative scene paths.</param>
    private static void AddManagedScenePaths(GameSceneManagerPreset preset,
                                             List<string> scenePaths,
                                             HashSet<string> uniqueScenePaths)
    {
        if (preset == null || preset.SceneDefinitions == null)
            return;

        for (int sceneIndex = 0; sceneIndex < preset.SceneDefinitions.Count; sceneIndex++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[sceneIndex];

            if (sceneDefinition == null)
                continue;

            string scenePath = NormalizeAssetPath(sceneDefinition.ScenePath);

            if (string.IsNullOrWhiteSpace(scenePath))
                continue;

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
                continue;

            if (!uniqueScenePaths.Add(scenePath))
                continue;

            scenePaths.Add(scenePath);
        }
    }

    /// <summary>
    /// Builds spawner entries from one opened scene.
    /// </summary>
    /// <param name="scene">Opened scene to scan.</param>
    /// <param name="ownerSceneGuid">Unity scene or SubScene asset GUID that owns the scanned authoring objects.</param>
    /// <param name="hierarchyPathPrefix">Optional path prefix used when spawners are displayed under a parent scene SubScene.</param>
    /// <param name="sceneChanged">True when inactive spawner instances were converted into bakeable disabled defaults.</param>
    /// <returns>Generated spawner entries.</returns>
    private static List<EnemySpawnerRuntimeSpawnerEntry> BuildSpawnerEntries(Scene scene,
                                                                             string ownerSceneGuid,
                                                                             string hierarchyPathPrefix,
                                                                             out bool sceneChanged)
    {
        sceneChanged = false;
        List<EnemySpawnerRuntimeSpawnerEntry> spawnerEntries = new List<EnemySpawnerRuntimeSpawnerEntry>();

        if (!scene.IsValid())
            return spawnerEntries;

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            EnemySpawnerAuthoring[] spawners = rootObjects[rootIndex].GetComponentsInChildren<EnemySpawnerAuthoring>(true);

            for (int spawnerIndex = 0; spawnerIndex < spawners.Length; spawnerIndex++)
            {
                EnemySpawnerAuthoring spawner = spawners[spawnerIndex];

                if (spawner == null)
                    continue;

                if (NormalizeSpawnerForRuntimeTool(spawner))
                    sceneChanged = true;

                if (!spawner.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning("[EnemySpawnerRuntimeCatalogBuildUtility] Skipped runtime spawner '" +
                                     spawner.name +
                                     "' in scene '" +
                                     scene.path +
                                     "' because an inactive parent prevents DOTS baking. Keep the spawner hierarchy active and use Spawner Enabled to control gameplay defaults.");
                    continue;
                }

                EnemySpawnerRuntimeSpawnerEntry spawnerEntry = new EnemySpawnerRuntimeSpawnerEntry();
                spawnerEntry.Assign(ownerSceneGuid,
                                    EnemySpawnerRuntimeBakeMetadataUtility.ResolveAuthoringSpawnerGuid(spawner),
                                    spawner.name,
                                    BuildHierarchyPath(spawner.transform, hierarchyPathPrefix),
                                    spawner.RuntimeEnabledByDefault,
                                    ResolveAssetGuid(spawner.WavePreset));
                spawnerEntries.Add(spawnerEntry);
            }
        }

        spawnerEntries.Sort(CompareSpawnerEntries);
        return spawnerEntries;
    }

    /// <summary>
    /// Appends spawners authored inside SubScenes referenced by an opened parent scene.
    /// </summary>
    /// <param name="parentScene">Opened parent scene that owns SubScene components.</param>
    /// <param name="spawnerEntries">Target spawner list for the parent scene catalog entry.</param>
    /// <param name="uniqueSpawnerKeys">Uniqueness guard shared by direct and SubScene spawners.</param>
    /// <param name="referencedSubScenePaths">Project-wide set that tracks SubScene assets referenced by root scenes.</param>
    private static void AppendReferencedSubSceneSpawners(Scene parentScene,
                                                         List<EnemySpawnerRuntimeSpawnerEntry> spawnerEntries,
                                                         HashSet<string> uniqueSpawnerKeys,
                                                         HashSet<string> referencedSubScenePaths)
    {
        if (!parentScene.IsValid())
            return;

        HashSet<string> localSubScenePaths = new HashSet<string>();
        GameObject[] rootObjects = parentScene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            SubScene[] subScenes = rootObjects[rootIndex].GetComponentsInChildren<SubScene>(true);

            for (int subSceneIndex = 0; subSceneIndex < subScenes.Length; subSceneIndex++)
                AppendReferencedSubSceneSpawners(subScenes[subSceneIndex],
                                                 spawnerEntries,
                                                 uniqueSpawnerKeys,
                                                 referencedSubScenePaths,
                                                 localSubScenePaths);
        }
    }

    /// <summary>
    /// Appends spawners from one referenced SubScene asset to the selected parent scene entry.
    /// </summary>
    /// <param name="subScene">SubScene component found inside the parent scene.</param>
    /// <param name="spawnerEntries">Target spawner list for the parent scene catalog entry.</param>
    /// <param name="uniqueSpawnerKeys">Uniqueness guard shared by direct and SubScene spawners.</param>
    /// <param name="referencedSubScenePaths">Project-wide set that tracks SubScene assets referenced by root scenes.</param>
    /// <param name="localSubScenePaths">Parent-scene guard that prevents scanning the same SubScene asset twice.</param>
    private static void AppendReferencedSubSceneSpawners(SubScene subScene,
                                                         List<EnemySpawnerRuntimeSpawnerEntry> spawnerEntries,
                                                         HashSet<string> uniqueSpawnerKeys,
                                                         HashSet<string> referencedSubScenePaths,
                                                         HashSet<string> localSubScenePaths)
    {
        string subScenePath = ResolveSubSceneAssetPath(subScene);

        if (string.IsNullOrWhiteSpace(subScenePath))
            return;

        referencedSubScenePaths.Add(subScenePath);

        if (!localSubScenePaths.Add(subScenePath))
            return;

        Scene openedSubScene = EditorSceneManager.OpenScene(subScenePath, OpenSceneMode.Additive);

        try
        {
            bool subSceneChanged;
            List<EnemySpawnerRuntimeSpawnerEntry> subSceneSpawnerEntries = BuildSpawnerEntries(openedSubScene,
                                                                                              AssetDatabase.AssetPathToGUID(subScenePath),
                                                                                              BuildHierarchyPath(subScene.transform, string.Empty),
                                                                                              out subSceneChanged);
            AddUniqueSpawnerEntries(spawnerEntries, subSceneSpawnerEntries, uniqueSpawnerKeys);

            if (subSceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(openedSubScene);
                EditorSceneManager.SaveScene(openedSubScene);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(openedSubScene, true);
        }
    }

    /// <summary>
    /// Adds generated spawner entries while preserving one row per baked runtime identity.
    /// </summary>
    /// <param name="targetEntries">Catalog list receiving unique entries.</param>
    /// <param name="sourceEntries">Generated entries from one opened scene or SubScene.</param>
    /// <param name="uniqueSpawnerKeys">Set of owner-scene/spawner identity keys already added.</param>
    private static void AddUniqueSpawnerEntries(List<EnemySpawnerRuntimeSpawnerEntry> targetEntries,
                                                List<EnemySpawnerRuntimeSpawnerEntry> sourceEntries,
                                                HashSet<string> uniqueSpawnerKeys)
    {
        if (targetEntries == null || sourceEntries == null || uniqueSpawnerKeys == null)
            return;

        for (int spawnerIndex = 0; spawnerIndex < sourceEntries.Count; spawnerIndex++)
        {
            EnemySpawnerRuntimeSpawnerEntry spawnerEntry = sourceEntries[spawnerIndex];

            if (spawnerEntry == null)
                continue;

            string key = BuildSpawnerIdentityKey(spawnerEntry);

            if (!uniqueSpawnerKeys.Add(key))
                continue;

            targetEntries.Add(spawnerEntry);
        }
    }

    /// <summary>
    /// Removes standalone SubScene catalog entries when their spawners are already exposed through parent scenes.
    /// </summary>
    /// <param name="sceneEntries">Generated scene entries to filter.</param>
    /// <param name="referencedSubScenePaths">SubScene asset paths found inside parent scenes.</param>
    private static void RemoveReferencedSubSceneEntries(List<EnemySpawnerRuntimeSceneEntry> sceneEntries,
                                                        HashSet<string> referencedSubScenePaths)
    {
        if (sceneEntries == null || referencedSubScenePaths == null || referencedSubScenePaths.Count <= 0)
            return;

        for (int sceneIndex = sceneEntries.Count - 1; sceneIndex >= 0; sceneIndex--)
        {
            EnemySpawnerRuntimeSceneEntry sceneEntry = sceneEntries[sceneIndex];

            if (sceneEntry == null || !referencedSubScenePaths.Contains(sceneEntry.ScenePath))
                continue;

            sceneEntries.RemoveAt(sceneIndex);
        }
    }
    #endregion

    #region Preset Catalog
    /// <summary>
    /// Groups EnemyWavePreset assets by containing folder.
    /// </summary>
    /// <returns>Generated folder entries that contain at least one preset.</returns>
    private static List<EnemySpawnerRuntimeWavePresetFolderEntry> BuildWavePresetFolderEntries()
    {
        SortedDictionary<string, List<EnemySpawnerRuntimeWavePresetEntry>> presetsByFolder = new SortedDictionary<string, List<EnemySpawnerRuntimeWavePresetEntry>>();
        string[] presetGuids = AssetDatabase.FindAssets("t:EnemyWavePreset", new[] { "Assets" });

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string assetPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]));
            EnemyWavePreset preset = AssetDatabase.LoadAssetAtPath<EnemyWavePreset>(assetPath);

            if (preset == null)
                continue;

            string folderPath = NormalizeAssetPath(Path.GetDirectoryName(assetPath));

            if (string.IsNullOrWhiteSpace(folderPath))
                continue;

            List<EnemySpawnerRuntimeWavePresetEntry> presetEntries;

            if (!presetsByFolder.TryGetValue(folderPath, out presetEntries))
            {
                presetEntries = new List<EnemySpawnerRuntimeWavePresetEntry>();
                presetsByFolder.Add(folderPath, presetEntries);
            }

            EnemySpawnerRuntimeWavePresetEntry presetEntry = new EnemySpawnerRuntimeWavePresetEntry();
            presetEntry.Assign(preset.name, assetPath, presetGuids[presetIndex], preset);
            presetEntries.Add(presetEntry);
        }

        return BuildFolderEntries(presetsByFolder);
    }

    /// <summary>
    /// Converts grouped preset entries into serialized catalog folder entries.
    /// </summary>
    /// <param name="presetsByFolder">Grouped preset entries keyed by folder path.</param>
    /// <returns>Serialized folder entries.</returns>
    private static List<EnemySpawnerRuntimeWavePresetFolderEntry> BuildFolderEntries(SortedDictionary<string, List<EnemySpawnerRuntimeWavePresetEntry>> presetsByFolder)
    {
        List<EnemySpawnerRuntimeWavePresetFolderEntry> folderEntries = new List<EnemySpawnerRuntimeWavePresetFolderEntry>();

        foreach (KeyValuePair<string, List<EnemySpawnerRuntimeWavePresetEntry>> pair in presetsByFolder)
        {
            pair.Value.Sort(ComparePresetEntries);
            EnemySpawnerRuntimeWavePresetFolderEntry folderEntry = new EnemySpawnerRuntimeWavePresetFolderEntry();
            folderEntry.Assign(pair.Key, BuildFolderDisplayName(pair.Key), pair.Value);
            folderEntries.Add(folderEntry);
        }

        return folderEntries;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds a readable scene hierarchy path for one transform, optionally under a parent scene prefix.
    /// </summary>
    /// <param name="transform">Transform to describe.</param>
    /// <param name="pathPrefix">Optional prefix used when the transform belongs to a referenced SubScene.</param>
    /// <returns>Slash-separated hierarchy path.</returns>
    private static string BuildHierarchyPath(Transform transform, string pathPrefix)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        if (!string.IsNullOrWhiteSpace(pathPrefix))
            path = pathPrefix + "/" + path;

        return path;
    }

    /// <summary>
    /// Resolves the project-relative scene asset path referenced by one DOTS SubScene component.
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
        {
            Debug.LogWarning("[EnemySpawnerRuntimeCatalogBuildUtility] Skipped SubScene '" +
                             subScene.name +
                             "' because it does not reference a scene asset.",
                             subScene);
            return string.Empty;
        }

        string scenePath = AssetDatabase.GetAssetPath(sceneAsset);

        if (!string.IsNullOrWhiteSpace(scenePath))
            return NormalizeAssetPath(scenePath);

        Debug.LogWarning("[EnemySpawnerRuntimeCatalogBuildUtility] Skipped SubScene '" +
                         subScene.name +
                         "' because its scene asset path could not be resolved.",
                         subScene);
        return string.Empty;
    }

    /// <summary>
    /// Builds the unique runtime identity key for one generated spawner entry.
    /// </summary>
    /// <param name="spawnerEntry">Spawner entry to identify.</param>
    /// <returns>Composite owner scene and spawner key.</returns>
    private static string BuildSpawnerIdentityKey(EnemySpawnerRuntimeSpawnerEntry spawnerEntry)
    {
        if (spawnerEntry == null)
            return string.Empty;

        return spawnerEntry.OwnerSceneGuid + "|" + spawnerEntry.SpawnerGuid;
    }

    /// <summary>
    /// Normalizes editor asset paths so catalog folder grouping stays stable across platforms.
    /// </summary>
    /// <param name="assetPath">Unity project-relative asset path.</param>
    /// <returns>Asset path using forward slashes, or an empty string when unavailable.</returns>
    private static string NormalizeAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        return assetPath.Replace('\\', '/');
    }

    /// <summary>
    /// Resolves an asset GUID for one Unity object.
    /// </summary>
    /// <param name="asset">Asset to inspect.</param>
    /// <returns>Asset GUID, or empty string when unavailable.</returns>
    private static string ResolveAssetGuid(Object asset)
    {
        if (asset == null)
            return string.Empty;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        return AssetDatabase.AssetPathToGUID(assetPath);
    }

    /// <summary>
    /// Converts inactive spawner GameObjects into active bakeable instances while preserving disabled gameplay defaults.
    /// </summary>
    /// <param name="spawner">Spawner authoring component to normalize.</param>
    /// <returns>True when scene or prefab-instance state changed.</returns>
    private static bool NormalizeSpawnerForRuntimeTool(EnemySpawnerAuthoring spawner)
    {
        if (spawner == null)
            return false;

        if (spawner.gameObject.activeSelf)
            return false;

        bool changed = SetSpawnerDisabledDefault(spawner);

        if (!spawner.gameObject.activeSelf)
        {
            spawner.gameObject.SetActive(true);
            changed = true;
        }

        if (!changed)
            return false;

        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(spawner.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner.gameObject);
        return true;
    }

    /// <summary>
    /// Writes the serialized default-enabled flag without requiring runtime-facing migration APIs.
    /// </summary>
    /// <param name="spawner">Spawner authoring component to edit.</param>
    /// <returns>True when the serialized field changed.</returns>
    private static bool SetSpawnerDisabledDefault(EnemySpawnerAuthoring spawner)
    {
        SerializedObject serializedObject = new SerializedObject(spawner);
        serializedObject.Update();
        SerializedProperty enabledProperty = serializedObject.FindProperty("spawnerEnabled");

        if (enabledProperty == null)
            return false;

        if (!enabledProperty.boolValue)
            return false;

        enabledProperty.boolValue = false;
        serializedObject.ApplyModifiedProperties();
        return true;
    }

    /// <summary>
    /// Creates a compact display name for one preset folder.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path.</param>
    /// <returns>Readable folder display name.</returns>
    private static string BuildFolderDisplayName(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return "Unknown Folder";

        return folderPath.Replace("Assets/Scriptable Objects/Enemy/", string.Empty);
    }

    /// <summary>
    /// Ensures a project folder exists.
    /// </summary>
    /// <param name="folderPath">Project-relative folder path.</param>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion

    #region Comparers
    /// <summary>
    /// Compares two scene entries by scene path.
    /// </summary>
    /// <param name="left">Left scene entry.</param>
    /// <param name="right">Right scene entry.</param>
    /// <returns>Standard comparison result.</returns>
    private static int CompareSceneEntries(EnemySpawnerRuntimeSceneEntry left, EnemySpawnerRuntimeSceneEntry right)
    {
        return string.Compare(left != null ? left.ScenePath : string.Empty,
                              right != null ? right.ScenePath : string.Empty,
                              System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Compares two spawner entries by hierarchy path.
    /// </summary>
    /// <param name="left">Left spawner entry.</param>
    /// <param name="right">Right spawner entry.</param>
    /// <returns>Standard comparison result.</returns>
    private static int CompareSpawnerEntries(EnemySpawnerRuntimeSpawnerEntry left, EnemySpawnerRuntimeSpawnerEntry right)
    {
        return string.Compare(left != null ? left.HierarchyPath : string.Empty,
                              right != null ? right.HierarchyPath : string.Empty,
                              System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Compares two preset entries by preset name.
    /// </summary>
    /// <param name="left">Left preset entry.</param>
    /// <param name="right">Right preset entry.</param>
    /// <returns>Standard comparison result.</returns>
    private static int ComparePresetEntries(EnemySpawnerRuntimeWavePresetEntry left, EnemySpawnerRuntimeWavePresetEntry right)
    {
        return string.Compare(left != null ? left.PresetName : string.Empty,
                              right != null ? right.PresetName : string.Empty,
                              System.StringComparison.Ordinal);
    }
    #endregion

    #endregion
}
