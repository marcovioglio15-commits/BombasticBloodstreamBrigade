using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;

/// <summary>
/// Resolves one managed room into its unique SubScene and enemy wave asset without disturbing open scenes.
/// </summary>
internal static class GameWaveSceneEditorUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Synchronizes scene paths, GUIDs and wave preset from the mapping's editor-only main scene reference.
    /// </summary>
    /// <param name="mappingProperty">Serialized GameWaveSceneDefinition being updated.</param>
    /// <returns>Empty text on success, otherwise an actionable scene-structure warning.</returns>
    public static string SynchronizeMapping(SerializedProperty mappingProperty)
    {
        if (mappingProperty == null)
            return "Wave scene mapping is missing.";

        SerializedProperty mainSceneAssetProperty = mappingProperty.FindPropertyRelative("mainSceneAsset");
        SceneAsset mainSceneAsset = mainSceneAssetProperty == null
            ? null
            : mainSceneAssetProperty.objectReferenceValue as SceneAsset;

        if (mainSceneAsset == null)
        {
            ClearResolvedSceneData(mappingProperty);
            return "Select a managed main room scene.";
        }

        string mainScenePath = AssetDatabase.GetAssetPath(mainSceneAsset);
        mappingProperty.FindPropertyRelative("mainScenePath").stringValue = mainScenePath;
        mappingProperty.FindPropertyRelative("mainSceneGuid").stringValue = AssetDatabase.AssetPathToGUID(mainScenePath);

        if (!TryResolveSingleSubScene(mainScenePath, out string subScenePath, out string warning))
        {
            ClearSubSceneData(mappingProperty);
            return warning;
        }

        mappingProperty.FindPropertyRelative("subScenePath").stringValue = subScenePath;
        mappingProperty.FindPropertyRelative("subSceneGuid").stringValue = AssetDatabase.AssetPathToGUID(subScenePath);

        if (!TryResolveSingleSpawner(subScenePath, out EnemyWavePreset wavePreset, out warning))
            return warning;

        mappingProperty.FindPropertyRelative("wavePreset").objectReferenceValue = wavePreset;
        return string.Empty;
    }

    /// <summary>
    /// Validates that a managed scene references exactly one SubScene and returns its asset path.
    /// </summary>
    /// <param name="mainScenePath">Project-relative managed scene path.</param>
    /// <param name="subScenePath">Unique referenced SubScene path when valid.</param>
    /// <param name="warning">Actionable structure warning on failure.</param>
    /// <returns>True when exactly one readable SubScene reference exists.</returns>
    public static bool TryResolveSingleSubScene(string mainScenePath,
                                                out string subScenePath,
                                                out string warning)
    {
        subScenePath = string.Empty;
        warning = string.Empty;

        if (string.IsNullOrWhiteSpace(mainScenePath))
        {
            warning = "Managed main scene path is empty.";
            return false;
        }

        Scene scene = SceneManager.GetSceneByPath(mainScenePath);
        bool closeScene = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenPreviewScene(mainScenePath);
                closeScene = true;
            }

            List<SubScene> subScenes = CollectComponents<SubScene>(scene);

            if (subScenes.Count != 1)
            {
                warning = "Main scene '" + mainScenePath + "' contains " + subScenes.Count +
                          " SubScene components; Waves requires exactly one.";
                return false;
            }

            if (subScenes[0].SceneAsset == null)
            {
                warning = "The single SubScene component in '" + mainScenePath + "' has no scene asset.";
                return false;
            }

            subScenePath = AssetDatabase.GetAssetPath(subScenes[0].SceneAsset);
            return !string.IsNullOrWhiteSpace(subScenePath);
        }
        finally
        {
            if (closeScene && scene.IsValid())
                EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>
    /// Validates that an ECS SubScene contains one enemy spawner and resolves its wave preset.
    /// </summary>
    /// <param name="subScenePath">Project-relative ECS SubScene path.</param>
    /// <param name="wavePreset">Wave preset assigned to the unique spawner.</param>
    /// <param name="warning">Actionable structure warning on failure.</param>
    /// <returns>True when the SubScene contains exactly one configured spawner.</returns>
    public static bool TryResolveSingleSpawner(string subScenePath,
                                               out EnemyWavePreset wavePreset,
                                               out string warning)
    {
        wavePreset = null;
        warning = string.Empty;
        Scene scene = SceneManager.GetSceneByPath(subScenePath);
        bool closeScene = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenPreviewScene(subScenePath);
                closeScene = true;
            }

            List<EnemySpawnerAuthoring> spawners = CollectComponents<EnemySpawnerAuthoring>(scene);

            if (spawners.Count != 1)
            {
                warning = "SubScene '" + subScenePath + "' contains " + spawners.Count +
                          " enemy spawners; Waves requires exactly one.";
                return false;
            }

            wavePreset = spawners[0].WavePreset;

            if (wavePreset != null)
                return true;

            warning = "The single enemy spawner in '" + subScenePath + "' has no Enemy Wave preset.";
            return false;
        }
        finally
        {
            if (closeScene && scene.IsValid())
                EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>
    /// Assigns one Waves preset as the reusable brush-category source of an Enemy Wave asset.
    /// </summary>
    /// <param name="wavePreset">Enemy Wave asset receiving the category-source reference.</param>
    /// <param name="wavesPreset">Waves preset providing reusable category definitions.</param>
    public static void LinkCategorySource(EnemyWavePreset wavePreset, GameWavesPreset wavesPreset)
    {
        if (wavePreset == null)
            return;

        SerializedObject serializedWavePreset = new SerializedObject(wavePreset);
        SerializedProperty wavesPresetProperty = serializedWavePreset.FindProperty("wavesPreset");

        if (wavesPresetProperty.objectReferenceValue == wavesPreset)
            return;

        Undo.RecordObject(wavePreset, "Link Enemy Wave Category Source");
        wavesPresetProperty.objectReferenceValue = wavesPreset;
        serializedWavePreset.ApplyModifiedProperties();
        EditorUtility.SetDirty(wavePreset);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Collects all nested components of one type from a loaded preview scene.
    /// </summary>
    /// <typeparam name="TComponent">Component type requested from scene roots.</typeparam>
    /// <param name="scene">Loaded preview scene.</param>
    /// <returns>All active and inactive nested components.</returns>
    private static List<TComponent> CollectComponents<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> components = new List<TComponent>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            components.AddRange(roots[rootIndex].GetComponentsInChildren<TComponent>(true));

        return components;
    }

    /// <summary>
    /// Clears every resolved path and asset after the managed scene reference is removed.
    /// </summary>
    /// <param name="mappingProperty">Serialized mapping being cleared.</param>
    private static void ClearResolvedSceneData(SerializedProperty mappingProperty)
    {
        mappingProperty.FindPropertyRelative("mainScenePath").stringValue = string.Empty;
        mappingProperty.FindPropertyRelative("mainSceneGuid").stringValue = string.Empty;
        ClearSubSceneData(mappingProperty);
    }

    /// <summary>
    /// Clears resolved SubScene and wave data after scene-structure validation fails.
    /// </summary>
    /// <param name="mappingProperty">Serialized mapping being cleared.</param>
    private static void ClearSubSceneData(SerializedProperty mappingProperty)
    {
        mappingProperty.FindPropertyRelative("subScenePath").stringValue = string.Empty;
        mappingProperty.FindPropertyRelative("subSceneGuid").stringValue = string.Empty;
        mappingProperty.FindPropertyRelative("wavePreset").objectReferenceValue = null;
    }
    #endregion

    #endregion
}
