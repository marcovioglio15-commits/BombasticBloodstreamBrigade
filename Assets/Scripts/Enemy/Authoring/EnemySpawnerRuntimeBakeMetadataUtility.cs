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
    /// Resolves a stable global object identifier for the authoring object.
    /// </summary>
    /// <param name="authoring">Spawner authoring source.</param>
    /// <returns>Stable authoring object identifier used by runtime overrides.</returns>
    public static string ResolveAuthoringSpawnerGuid(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return string.Empty;

#if UNITY_EDITOR
        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(authoring);
        return globalObjectId.ToString();
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
    #endregion

    #region Helpers
#if UNITY_EDITOR
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
    #endregion

    #endregion
}
