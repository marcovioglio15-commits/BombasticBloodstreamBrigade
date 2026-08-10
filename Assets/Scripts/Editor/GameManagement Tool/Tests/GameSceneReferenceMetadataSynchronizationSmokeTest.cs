using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies that stable scene identities repair Waves mapping paths after main-scene and SubScene renames.
/// </summary>
public static class GameSceneReferenceMetadataSynchronizationSmokeTest
{
    #region Constants

    private const string TemporaryFolder = "Assets/__GameSceneReferenceMetadataSmoke";
    private const string InitialMainScenePath = TemporaryFolder + "/SCN_TEMP_INITIAL.unity";
    private const string RenamedMainScenePath = TemporaryFolder + "/SCN_TEMP_RENAMED.unity";
    private const string InitialSubScenePath = TemporaryFolder + "/SUB_TEMP_INITIAL.unity";
    private const string RenamedSubScenePath = TemporaryFolder + "/SUB_TEMP_RENAMED.unity";
    private const string TemporaryPresetPath = TemporaryFolder + "/GameWavesPreset_Temporary.asset";
    private const string TemporarySceneManagerPresetPath = TemporaryFolder + "/GameSceneManagerPreset_Temporary.asset";

    #endregion

    #region Methods

    #region Entry Point

    // [MenuItem("Tools/Tests/Game/Scene Reference Metadata Synchronization Smoke Test")]
    /// <summary>
    /// Creates isolated scene assets, renames them and verifies GUID-backed mapping recovery before cleanup.
    /// </summary>
    public static void Run()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            EnsureTemporaryFolder();
            CreateEmptySceneAsset(InitialMainScenePath);
            CreateEmptySceneAsset(InitialSubScenePath);
            GameWavesPreset preset = CreateTemporaryPreset();
            GameSceneManagerPreset sceneManagerPreset = CreateTemporarySceneManagerPreset();
            MoveAsset(InitialSubScenePath, RenamedSubScenePath);
            GameSceneReferenceMetadataSynchronizer.SynchronizeWavesPreset(preset);
            AssertMappingPaths(preset, InitialMainScenePath, RenamedSubScenePath);
            MoveAsset(InitialMainScenePath, RenamedMainScenePath);
            GameSceneReferenceMetadataSynchronizer.SynchronizeAllStableReferences();
            AssertMappingPaths(preset, RenamedMainScenePath, RenamedSubScenePath);
            AssertSceneManagerPath(sceneManagerPreset, RenamedMainScenePath);
            Debug.Log("Game Scene reference metadata synchronization smoke test passed.");
        }
        finally
        {
            if (CanRestoreSetup(originalSetup))
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AssetDatabase.DeleteAsset(TemporaryFolder);
            AssetDatabase.Refresh();
        }
    }

    #endregion

    #region Asset Setup Methods

    /// <summary>
    /// Creates the isolated project folder used by the rename smoke test.
    /// </summary>
    private static void EnsureTemporaryFolder()
    {
        if (!AssetDatabase.IsValidFolder(TemporaryFolder))
            AssetDatabase.CreateFolder("Assets", "__GameSceneReferenceMetadataSmoke");
    }

    /// <summary>
    /// Creates and saves one empty scene in the isolated batch-mode scene setup.
    /// </summary>
    /// <param name="scenePath">Project-relative path receiving the empty scene asset.</param>
    private static void CreateEmptySceneAsset(string scenePath)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new InvalidOperationException("Unable to create temporary scene '" + scenePath + "'.");
    }

    /// <summary>
    /// Creates one temporary Waves preset containing a fully initialized main-scene and SubScene mapping.
    /// </summary>
    /// <returns>Created temporary Waves preset asset.</returns>
    private static GameWavesPreset CreateTemporaryPreset()
    {
        GameWavesPreset preset = ScriptableObject.CreateInstance<GameWavesPreset>();
        AssetDatabase.CreateAsset(preset, TemporaryPresetPath);
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty mappings = serializedPreset.FindProperty("sceneMappings");
        mappings.arraySize = 1;
        SerializedProperty mapping = mappings.GetArrayElementAtIndex(0);
        SceneAsset mainSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(InitialMainScenePath);
        mapping.FindPropertyRelative("displayName").stringValue = "Temporary Mapping";
        mapping.FindPropertyRelative("mainScenePath").stringValue = InitialMainScenePath;
        mapping.FindPropertyRelative("mainSceneGuid").stringValue = AssetDatabase.AssetPathToGUID(InitialMainScenePath);
        mapping.FindPropertyRelative("subScenePath").stringValue = InitialSubScenePath;
        mapping.FindPropertyRelative("subSceneGuid").stringValue = AssetDatabase.AssetPathToGUID(InitialSubScenePath);
        mapping.FindPropertyRelative("wavePreset").objectReferenceValue = null;
        mapping.FindPropertyRelative("mainSceneAsset").objectReferenceValue = mainSceneAsset;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(preset);
        return preset;
    }

    /// <summary>
    /// Creates one temporary Scene Manager preset whose generated path metadata follows the main scene asset.
    /// </summary>
    /// <returns>Created temporary Scene Manager preset asset.</returns>
    private static GameSceneManagerPreset CreateTemporarySceneManagerPreset()
    {
        GameSceneManagerPreset preset = ScriptableObject.CreateInstance<GameSceneManagerPreset>();
        AssetDatabase.CreateAsset(preset, TemporarySceneManagerPresetPath);
        SerializedObject serializedPreset = new SerializedObject(preset);
        serializedPreset.Update();
        SerializedProperty sceneDefinitions = serializedPreset.FindProperty("sceneDefinitions");
        sceneDefinitions.arraySize = 1;
        SerializedProperty definition = sceneDefinitions.GetArrayElementAtIndex(0);
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(InitialMainScenePath);
        definition.FindPropertyRelative("sceneId").stringValue = "SCN_TEMP_STABLE_ID";
        definition.FindPropertyRelative("sceneName").stringValue = "SCN_TEMP_INITIAL";
        definition.FindPropertyRelative("scenePath").stringValue = InitialMainScenePath;
        definition.FindPropertyRelative("sceneGuid").stringValue = AssetDatabase.AssetPathToGUID(InitialMainScenePath);
        definition.FindPropertyRelative("buildIndex").intValue = -1;
        definition.FindPropertyRelative("sceneAsset").objectReferenceValue = sceneAsset;
        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(preset);
        return preset;
    }

    /// <summary>
    /// Moves one temporary scene asset and fails with Unity's actionable AssetDatabase error text.
    /// </summary>
    /// <param name="sourcePath">Existing scene asset path.</param>
    /// <param name="destinationPath">Requested renamed scene asset path.</param>
    private static void MoveAsset(string sourcePath, string destinationPath)
    {
        string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);

        if (!string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException("Unable to rename temporary scene: " + error);
    }

    #endregion

    #region Assertion Methods

    /// <summary>
    /// Determines whether every captured scene has a persistent path that Unity can restore safely.
    /// </summary>
    /// <param name="sceneSetup">Editor scene setup captured before the smoke test.</param>
    /// <returns>True when the complete setup can be restored after temporary scene creation.</returns>
    private static bool CanRestoreSetup(SceneSetup[] sceneSetup)
    {
        if (sceneSetup == null || sceneSetup.Length == 0)
            return false;

        for (int sceneIndex = 0; sceneIndex < sceneSetup.Length; sceneIndex++)
        {
            if (string.IsNullOrWhiteSpace(sceneSetup[sceneIndex].path))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Verifies that one mapping path and GUID pair resolves to the renamed scene assets.
    /// </summary>
    /// <param name="preset">Temporary Waves preset refreshed by the synchronizer.</param>
    /// <param name="expectedMainScenePath">Expected current managed scene path.</param>
    /// <param name="expectedSubScenePath">Expected current SubScene path.</param>
    private static void AssertMappingPaths(GameWavesPreset preset,
                                           string expectedMainScenePath,
                                           string expectedSubScenePath)
    {
        if (preset.SceneMappings.Count != 1 || preset.SceneMappings[0] == null)
            throw new InvalidOperationException("Temporary Waves preset lost its scene mapping during synchronization.");

        GameWaveSceneDefinition mapping = preset.SceneMappings[0];

        if (!string.Equals(mapping.MainScenePath, expectedMainScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Main scene rename was not propagated to the Waves mapping.");

        if (!string.Equals(mapping.SubScenePath, expectedSubScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("SubScene rename was not propagated to the Waves mapping.");

        if (!string.Equals(mapping.MainSceneGuid,
                           AssetDatabase.AssetPathToGUID(expectedMainScenePath),
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Main scene GUID changed or became stale after rename synchronization.");

        if (!string.Equals(mapping.SubSceneGuid,
                           AssetDatabase.AssetPathToGUID(expectedSubScenePath),
                           StringComparison.Ordinal))
            throw new InvalidOperationException("SubScene GUID changed or became stale after rename synchronization.");
    }

    /// <summary>
    /// Verifies that Scene Manager runtime path, name and GUID metadata follow a renamed scene asset.
    /// </summary>
    /// <param name="preset">Temporary Scene Manager preset refreshed by the synchronizer.</param>
    /// <param name="expectedScenePath">Expected current managed scene path.</param>
    private static void AssertSceneManagerPath(GameSceneManagerPreset preset, string expectedScenePath)
    {
        if (preset.SceneDefinitions.Count != 1 || preset.SceneDefinitions[0] == null)
            throw new InvalidOperationException("Temporary Scene Manager preset lost its scene definition during synchronization.");

        GameSceneDefinition definition = preset.SceneDefinitions[0];

        if (!string.Equals(definition.ScenePath, expectedScenePath, StringComparison.Ordinal) ||
            !string.Equals(definition.SceneName, "SCN_TEMP_RENAMED", StringComparison.Ordinal) ||
            !string.Equals(definition.SceneGuid,
                           AssetDatabase.AssetPathToGUID(expectedScenePath),
                           StringComparison.Ordinal))
            throw new InvalidOperationException("Scene Manager runtime metadata did not follow the renamed main scene asset.");
    }

    #endregion

    #endregion
}
