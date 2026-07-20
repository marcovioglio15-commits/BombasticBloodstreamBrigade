using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Marks affected room metadata snapshots stale after scene imports without rescanning or rewriting authoring scenes.
/// </summary>
public static class GameRoomMetadataCacheInvalidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Marks snapshots stale when any cached root or nested scene path matches a changed asset path.
    /// </summary>
    /// <param name="changedAssetPaths">Imported, deleted, moved or saved project-relative scene paths.</param>
    public static void MarkStaleForAssetPaths(IReadOnlyCollection<string> changedAssetPaths)
    {
        if (changedAssetPaths == null || changedAssetPaths.Count == 0)
            return;

        HashSet<string> normalizedPaths = BuildNormalizedPathSet(changedAssetPaths);

        if (normalizedPaths.Count == 0)
            return;

        VisitPresets((metadataProperty) => MetadataReferencesAnyPath(metadataProperty, normalizedPaths));
    }

    /// <summary>
    /// Marks every room snapshot stale after authoring schema scripts change.
    /// </summary>
    public static void MarkAllStale()
    {
        VisitPresets((metadataProperty) => true);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Visits every procedural preset and marks matching serialized cache entries stale.
    /// </summary>
    /// <param name="shouldMark">Predicate selecting metadata entries affected by the change.</param>
    private static void VisitPresets(Func<SerializedProperty, bool> shouldMark)
    {
        string[] presetGuids = AssetDatabase.FindAssets("t:GameProceduralLevelPreset", new[] { "Assets" });

        // Touch only matching cache flags; hashes and cached recap remain visible for stale diagnostics.
        for (int presetIndex = 0; presetIndex < presetGuids.Length; presetIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[presetIndex]);
            GameProceduralLevelPreset preset = AssetDatabase.LoadAssetAtPath<GameProceduralLevelPreset>(presetPath);

            if (preset == null)
                continue;

            SerializedObject serializedPreset = new SerializedObject(preset);
            serializedPreset.Update();
            SerializedProperty metadataArray = serializedPreset.FindProperty("roomMetadata");

            if (metadataArray == null || !metadataArray.isArray)
                continue;

            bool changed = false;

            for (int metadataIndex = 0; metadataIndex < metadataArray.arraySize; metadataIndex++)
            {
                SerializedProperty metadataProperty = metadataArray.GetArrayElementAtIndex(metadataIndex);

                if (!shouldMark(metadataProperty))
                    continue;

                SerializedProperty staleProperty = metadataProperty.FindPropertyRelative("cacheStale");

                if (staleProperty == null || staleProperty.boolValue)
                    continue;

                staleProperty.boolValue = true;
                changed = true;
            }

            if (!changed)
                continue;

            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
        }
    }

    /// <summary>
    /// Checks whether one metadata snapshot contains any changed source scene path.
    /// </summary>
    /// <param name="metadataProperty">Serialized metadata entry.</param>
    /// <param name="changedPaths">Normalized changed path set.</param>
    /// <returns>True when the root scene or a nested SubScene changed.</returns>
    private static bool MetadataReferencesAnyPath(SerializedProperty metadataProperty, HashSet<string> changedPaths)
    {
        SerializedProperty pathArray = metadataProperty.FindPropertyRelative("sourceScenePaths");

        if (pathArray == null || !pathArray.isArray)
            return false;

        for (int pathIndex = 0; pathIndex < pathArray.arraySize; pathIndex++)
        {
            string path = GameRoomMetadataDependencyUtility.NormalizeAssetPath(pathArray.GetArrayElementAtIndex(pathIndex).stringValue);

            if (changedPaths.Contains(path))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes changed scene paths into an ordinal lookup set.
    /// </summary>
    /// <param name="changedAssetPaths">Raw asset paths supplied by editor events.</param>
    /// <returns>Normalized non-empty scene path set.</returns>
    private static HashSet<string> BuildNormalizedPathSet(IReadOnlyCollection<string> changedAssetPaths)
    {
        HashSet<string> normalizedPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (string changedAssetPath in changedAssetPaths)
        {
            string normalizedPath = GameRoomMetadataDependencyUtility.NormalizeAssetPath(changedAssetPath);

            if (!string.IsNullOrWhiteSpace(normalizedPath) && normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                normalizedPaths.Add(normalizedPath);
        }

        return normalizedPaths;
    }
    #endregion

    #endregion
}
