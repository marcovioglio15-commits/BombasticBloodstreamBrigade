#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Provides editor-backed metadata used while baking runtime-overridable enemy spawners.
/// </summary>
public static class EnemySpawnerRuntimeBakeMetadataUtility
{
    #region Constants
    private const char SpawnerIdentitySeparator = '|';
    private const char HierarchySeparator = '/';
    private const char SiblingIndexOpen = '[';
    private const char SiblingIndexClose = ']';
    #endregion

    #region Fields
#if UNITY_EDITOR
    private static readonly List<EnemyWavePreset> cachedRuntimeWavePresets = new List<EnemyWavePreset>();
    private static bool hasCachedRuntimeWavePresets;
#endif
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects all EnemyWavePreset assets that should be selectable for runtime overrides.
    /// </summary>
    /// <param name="defaultPreset">Preset authored directly on the spawner.</param>
    /// <returns>Unique candidate preset list.</returns>
    public static List<EnemyWavePreset> CollectRuntimeWavePresetCandidates(EnemyWavePreset defaultPreset)
    {
        List<EnemyWavePreset> candidatePresets = new List<EnemyWavePreset>();
        HashSet<EnemyWavePreset> uniquePresets = new HashSet<EnemyWavePreset>();
        AddPresetIfUnique(defaultPreset, candidatePresets, uniquePresets);

#if UNITY_EDITOR
        AddRuntimeCatalogWavePresetCandidates(candidatePresets, uniquePresets);
        EnsureRuntimeWavePresetCache();

        for (int presetIndex = 0; presetIndex < cachedRuntimeWavePresets.Count; presetIndex++)
        {
            EnemyWavePreset preset = cachedRuntimeWavePresets[presetIndex];
            AddPresetIfUnique(preset, candidatePresets, uniquePresets);
        }
#endif

        return candidatePresets;
    }

    /// <summary>
    /// Resolves the Unity asset GUID for a project asset.
    /// </summary>
    /// <param name="asset">Asset to inspect.</param>
    /// <returns>Unity asset GUID, or an empty string when unavailable.</returns>
    public static string ResolveAssetGuid(Object asset)
    {
        if (asset == null)
            return string.Empty;

#if UNITY_EDITOR
        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        return AssetDatabase.AssetPathToGUID(assetPath);
#else
        return string.Empty;
#endif
    }

    /// <summary>
    /// Resolves the Unity asset GUID of the scene that owns the authoring object.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <returns>Unity scene asset GUID, or an empty string when unavailable.</returns>
    public static string ResolveAuthoringSceneGuid(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return string.Empty;

#if UNITY_EDITOR
        string scenePath = authoring.gameObject.scene.path;

        if (string.IsNullOrWhiteSpace(scenePath))
            return string.Empty;

        return AssetDatabase.AssetPathToGUID(scenePath);
#else
        return string.Empty;
#endif
    }

    /// <summary>
    /// Resolves a deterministic runtime identifier shared by the catalog builder and the baker.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <returns>Stable authoring object identifier used by runtime overrides.</returns>
    public static string ResolveAuthoringSpawnerGuid(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return string.Empty;

#if UNITY_EDITOR
        string sceneGuid = ResolveAuthoringSceneGuid(authoring);
        string hierarchyPath = BuildIndexedHierarchyPath(authoring.transform);

        if (string.IsNullOrWhiteSpace(sceneGuid) || string.IsNullOrWhiteSpace(hierarchyPath))
            return string.Empty;

        string identitySource = sceneGuid + SpawnerIdentitySeparator + hierarchyPath;
        return Hash128.Compute(identitySource).ToString();
#else
        return authoring.name;
#endif
    }

    /// <summary>
    /// Clears cached wave-preset candidates before editor tools perform an explicit catalog rebuild.
    /// </summary>
    public static void ClearRuntimeWavePresetCandidateCache()
    {
#if UNITY_EDITOR
        cachedRuntimeWavePresets.Clear();
        hasCachedRuntimeWavePresets = false;
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Resolves the runtime spawner catalog asset that mirrors the dropdown source used by the main-menu tool.
    /// </summary>
    /// <returns>Runtime spawner catalog asset, or null when it has not been generated yet.</returns>
    public static EnemySpawnerRuntimeCatalog ResolveRuntimeCatalogAsset()
    {
        return AssetDatabase.LoadAssetAtPath<EnemySpawnerRuntimeCatalog>(EnemySpawnerRuntimeCatalog.DefaultAssetPath);
    }
#endif
    #endregion

    #region Helpers
#if UNITY_EDITOR
    /// <summary>
    /// Adds catalog-backed wave presets so SubScene baking uses the same selectable set exposed by the runtime tool.
    /// </summary>
    /// <param name="candidatePresets">Ordered candidate list receiving catalog presets.</param>
    /// <param name="uniquePresets">Uniqueness guard shared by all candidate sources.</param>
    private static void AddRuntimeCatalogWavePresetCandidates(List<EnemyWavePreset> candidatePresets,
                                                              HashSet<EnemyWavePreset> uniquePresets)
    {
        EnemySpawnerRuntimeCatalog catalog = ResolveRuntimeCatalogAsset();

        if (catalog == null)
            return;

        IReadOnlyList<EnemySpawnerRuntimeWavePresetFolderEntry> folderEntries = catalog.WavePresetFolders;

        for (int folderIndex = 0; folderIndex < folderEntries.Count; folderIndex++)
            AddRuntimeCatalogFolderWavePresetCandidates(folderEntries[folderIndex], candidatePresets, uniquePresets);
    }

    /// <summary>
    /// Adds all valid wave presets from one catalog folder entry.
    /// </summary>
    /// <param name="folderEntry">Catalog folder entry to inspect.</param>
    /// <param name="candidatePresets">Ordered candidate list receiving presets.</param>
    /// <param name="uniquePresets">Uniqueness guard shared by all candidate sources.</param>
    private static void AddRuntimeCatalogFolderWavePresetCandidates(EnemySpawnerRuntimeWavePresetFolderEntry folderEntry,
                                                                    List<EnemyWavePreset> candidatePresets,
                                                                    HashSet<EnemyWavePreset> uniquePresets)
    {
        if (folderEntry == null)
            return;

        IReadOnlyList<EnemySpawnerRuntimeWavePresetEntry> presetEntries = folderEntry.WavePresets;

        for (int presetIndex = 0; presetIndex < presetEntries.Count; presetIndex++)
        {
            EnemySpawnerRuntimeWavePresetEntry presetEntry = presetEntries[presetIndex];

            if (presetEntry == null)
                continue;

            EnemyWavePreset preset = ResolveCatalogPreset(presetEntry);
            AddPresetIfUnique(preset, candidatePresets, uniquePresets);
        }
    }

    /// <summary>
    /// Resolves the wave preset referenced by one catalog entry, falling back to its asset path when the object reference is stale.
    /// </summary>
    /// <param name="presetEntry">Catalog preset entry to resolve.</param>
    /// <returns>Resolved EnemyWavePreset asset, or null when unavailable.</returns>
    private static EnemyWavePreset ResolveCatalogPreset(EnemySpawnerRuntimeWavePresetEntry presetEntry)
    {
        if (presetEntry == null)
            return null;

        if (presetEntry.Preset != null)
            return presetEntry.Preset;

        if (string.IsNullOrWhiteSpace(presetEntry.AssetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<EnemyWavePreset>(presetEntry.AssetPath);
    }

    /// <summary>
    /// Builds the editor wave-preset cache once per domain so each spawner baker does not rescan the AssetDatabase.
    /// </summary>
    private static void EnsureRuntimeWavePresetCache()
    {
        if (hasCachedRuntimeWavePresets)
            return;

        cachedRuntimeWavePresets.Clear();
        string[] presetGuids = AssetDatabase.FindAssets("t:EnemyWavePreset", new[] { "Assets" });

        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            EnemyWavePreset preset = AssetDatabase.LoadAssetAtPath<EnemyWavePreset>(assetPath);

            if (preset == null)
                continue;

            cachedRuntimeWavePresets.Add(preset);
        }

        hasCachedRuntimeWavePresets = true;
    }
#endif

    /// <summary>
    /// Adds one preset to a unique ordered list when it is valid and not already present.
    /// </summary>
    /// <param name="preset">Preset candidate to add.</param>
    /// <param name="candidatePresets">Ordered candidate list.</param>
    /// <param name="uniquePresets">Uniqueness guard set.</param>
    private static void AddPresetIfUnique(EnemyWavePreset preset,
                                          List<EnemyWavePreset> candidatePresets,
                                          HashSet<EnemyWavePreset> uniquePresets)
    {
        if (preset == null || candidatePresets == null || uniquePresets == null)
            return;

        if (!uniquePresets.Add(preset))
            return;

        candidatePresets.Add(preset);
    }

    /// <summary>
    /// Builds an indexed hierarchy path so duplicated sibling names still resolve to different runtime identities.
    /// </summary>
    /// <param name="transform">Authoring transform to describe.</param>
    /// <returns>Slash-separated hierarchy path with sibling indices.</returns>
    private static string BuildIndexedHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string path = BuildIndexedHierarchySegment(transform);
        Transform parent = transform.parent;

        // Walk to the scene root so catalog scans and SubScene baking use the same identity source.
        while (parent != null)
        {
            path = BuildIndexedHierarchySegment(parent) + HierarchySeparator + path;
            parent = parent.parent;
        }

        return path;
    }

    /// <summary>
    /// Builds one hierarchy segment from a transform name and sibling index.
    /// </summary>
    /// <param name="transform">Transform represented by this segment.</param>
    /// <returns>Stable segment text for the current scene hierarchy.</returns>
    private static string BuildIndexedHierarchySegment(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        return transform.name + SiblingIndexOpen + transform.GetSiblingIndex() + SiblingIndexClose;
    }
    #endregion

    #endregion
}
#endif
