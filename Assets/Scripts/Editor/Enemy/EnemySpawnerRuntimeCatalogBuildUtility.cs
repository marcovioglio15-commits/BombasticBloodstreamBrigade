using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        EnsureFolder(Path.GetDirectoryName(CatalogAssetPath));
        EnemySpawnerRuntimeCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemySpawnerRuntimeCatalog>(CatalogAssetPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EnemySpawnerRuntimeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        List<EnemySpawnerRuntimeSceneEntry> sceneEntries = BuildSceneEntries();
        List<EnemySpawnerRuntimeWavePresetFolderEntry> folderEntries = BuildWavePresetFolderEntries();
        catalog.Assign(sceneEntries, folderEntries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return catalog;
    }
    #endregion

    #region Scene Catalog
    /// <summary>
    /// Builds scene entries by opening each project scene additively and normalizing runtime-selectable spawners.
    /// </summary>
    /// <returns>Generated scene entries that contain at least one enemy spawner.</returns>
    private static List<EnemySpawnerRuntimeSceneEntry> BuildSceneEntries()
    {
        List<EnemySpawnerRuntimeSceneEntry> sceneEntries = new List<EnemySpawnerRuntimeSceneEntry>();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            for (int sceneIndex = 0; sceneIndex < sceneGuids.Length; sceneIndex++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[sceneIndex]);

                if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.EndsWith(".unity"))
                    continue;

                Scene openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                bool sceneChanged;
                List<EnemySpawnerRuntimeSpawnerEntry> spawnerEntries = BuildSpawnerEntries(openedScene, out sceneChanged);

                if (spawnerEntries.Count > 0)
                {
                    EnemySpawnerRuntimeSceneEntry sceneEntry = new EnemySpawnerRuntimeSceneEntry();
                    sceneEntry.Assign(Path.GetFileNameWithoutExtension(scenePath),
                                      scenePath,
                                      AssetDatabase.AssetPathToGUID(scenePath),
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

        sceneEntries.Sort(CompareSceneEntries);
        return sceneEntries;
    }

    /// <summary>
    /// Builds spawner entries from one opened scene.
    /// </summary>
    /// <param name="scene">Opened scene to scan.</param>
    /// <param name="sceneChanged">True when inactive spawner instances were converted into bakeable disabled defaults.</param>
    /// <returns>Generated spawner entries.</returns>
    private static List<EnemySpawnerRuntimeSpawnerEntry> BuildSpawnerEntries(Scene scene, out bool sceneChanged)
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
                spawnerEntry.Assign(GlobalObjectId.GetGlobalObjectIdSlow(spawner).ToString(),
                                    spawner.name,
                                    BuildHierarchyPath(spawner.transform),
                                    spawner.RuntimeEnabledByDefault,
                                    ResolveAssetGuid(spawner.WavePreset));
                spawnerEntries.Add(spawnerEntry);
            }
        }

        spawnerEntries.Sort(CompareSpawnerEntries);
        return spawnerEntries;
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
            string assetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            EnemyWavePreset preset = AssetDatabase.LoadAssetAtPath<EnemyWavePreset>(assetPath);

            if (preset == null)
                continue;

            string folderPath = Path.GetDirectoryName(assetPath);

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
    /// Builds a readable scene hierarchy path for one transform.
    /// </summary>
    /// <param name="transform">Transform to describe.</param>
    /// <returns>Slash-separated hierarchy path.</returns>
    private static string BuildHierarchyPath(Transform transform)
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

        return path;
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
