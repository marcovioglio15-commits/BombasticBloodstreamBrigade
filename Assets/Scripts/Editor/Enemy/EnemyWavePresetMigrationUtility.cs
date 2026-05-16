using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Migrates legacy inline enemy-spawner wave data into dedicated EnemyWavePreset assets.
/// None.
/// </summary>
public static class EnemyWavePresetMigrationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Migrates every prefab-based EnemySpawnerAuthoring that still uses inline serialized waves and has no assigned preset.
    /// None.
    /// </summary>
    //[MenuItem("Tools/NashCore/Enemy/Migrate Enemy Wave Presets")]
    public static void MigrateAllMissingWavePresets()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int migratedCount = 0;

        // Scan every prefab once and migrate only the spawners still missing a preset.
        for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                EnemySpawnerAuthoring spawnerAuthoring = prefabRoot.GetComponent<EnemySpawnerAuthoring>();

                if (spawnerAuthoring == null)
                    continue;

                if (spawnerAuthoring.WavePreset != null)
                    continue;

                SerializedObject serializedSpawner = new SerializedObject(spawnerAuthoring);
                SerializedProperty legacyWavesProperty = serializedSpawner.FindProperty("waves");

                if (legacyWavesProperty == null || legacyWavesProperty.arraySize <= 0)
                    continue;

                EnemyWavePreset newPreset = ScriptableObject.CreateInstance<EnemyWavePreset>();
                EditorUtility.CopySerializedManagedFieldsOnly(spawnerAuthoring, newPreset);
                string presetPath = ResolvePresetAssetPath(prefabPath);
                AssetDatabase.CreateAsset(newPreset, presetPath);
                serializedSpawner.Update();
                serializedSpawner.FindProperty("wavePreset").objectReferenceValue = newPreset;
                legacyWavesProperty.ClearArray();
                serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                migratedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(string.Format("[EnemyWavePresetMigrationUtility] Migrated {0} spawner prefab(s) to EnemyWavePreset assets.", migratedCount));
    }

    /// <summary>
    /// Batch entry point used by command-line Unity automation.
    /// None.
    /// </summary>
    public static void ExecuteBatchMigration()
    {
        MigrateAllMissingWavePresets();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves a unique preset asset path inside the canonical wave-preset folder.
    /// </summary>
    /// <param name="prefabPath">Source spawner prefab path.</param>
    /// <returns>Unique asset path where the migrated EnemyWavePreset should be saved.</returns>
    private static string ResolvePresetAssetPath(string prefabPath)
    {
        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        return EnemyWavePresetAssetUtility.CreateUniquePresetAssetPath(prefabName + "_WavePreset");
    }
    #endregion

    #endregion
}
