using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Provides editor helpers for validating and synchronizing Scene Manager presets with Build Settings.
/// /params None.
/// /returns None.
/// </summary>
public static class GameSceneManagementBuildSettingsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the enabled Build Settings index for one scene path.
    /// /params scenePath Project-relative scene path.
    /// /returns Build index when enabled, otherwise -1.
    /// </summary>
    public static int ResolveBuildIndex(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            return -1;

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int enabledIndex = 0;

        for (int index = 0; index < scenes.Length; index++)
        {
            EditorBuildSettingsScene buildScene = scenes[index];

            if (!buildScene.enabled)
                continue;

            if (string.Equals(buildScene.path, scenePath, System.StringComparison.Ordinal))
                return enabledIndex;

            enabledIndex++;
        }

        return -1;
    }

    /// <summary>
    /// Adds warnings for scene definitions whose build settings metadata is missing or out of date.
    /// /params preset Scene manager preset to inspect.
    /// /params warnings Mutable warning output list.
    /// /returns None.
    /// </summary>
    public static void CollectBuildSettingsWarnings(GameSceneManagerPreset preset, List<string> warnings)
    {
        if (preset == null || warnings == null || preset.SceneDefinitions == null)
            return;

        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (sceneDefinition == null)
                continue;

            int buildIndex = ResolveBuildIndex(sceneDefinition.ScenePath);

            if (ShouldSkipBuildSettings(sceneDefinition))
                continue;

            if (ShouldSkipBuildSettingsForAddressables(preset, sceneDefinition))
                continue;

            if (buildIndex < 0)
            {
                warnings.Add(sceneDefinition.SceneId + " is not enabled in Build Settings.");
                continue;
            }

            if (buildIndex != sceneDefinition.BuildIndex)
                warnings.Add(sceneDefinition.SceneId + " has stale Build Index metadata. Expected " + buildIndex + ".");
        }
    }

    /// <summary>
    /// Adds every non-SubScene scene definition to Build Settings in authored order.
    /// /params preset Scene manager preset containing ordered scene definitions.
    /// /returns True when Build Settings changed.
    /// </summary>
    public static bool ApplySceneOrderToBuildSettings(GameSceneManagerPreset preset)
    {
        if (preset == null || preset.SceneDefinitions == null)
            return false;

        List<EditorBuildSettingsScene> orderedScenes = new List<EditorBuildSettingsScene>();

        for (int index = 0; index < preset.SceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = preset.SceneDefinitions[index];

            if (sceneDefinition == null)
                continue;

            if (ShouldSkipBuildSettings(sceneDefinition))
                continue;

            if (ShouldSkipBuildSettingsForAddressables(preset, sceneDefinition))
                continue;

            if (string.IsNullOrWhiteSpace(sceneDefinition.ScenePath))
                continue;

            orderedScenes.Add(new EditorBuildSettingsScene(sceneDefinition.ScenePath, true));
        }

        if (orderedScenes.Count <= 0)
            return false;

        EditorBuildSettings.scenes = orderedScenes.ToArray();
        return true;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether a non-bootstrap scene is intentionally omitted from Build Settings because Addressables owns it.
    /// /params preset Scene manager preset containing the active backend.
    /// /params sceneDefinition Scene definition being inspected.
    /// /returns True when Build Settings validation should skip the scene.
    /// </summary>
    private static bool ShouldSkipBuildSettingsForAddressables(GameSceneManagerPreset preset, GameSceneDefinition sceneDefinition)
    {
        if (preset.LoadBackend != GameSceneLoadBackend.Addressables)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.Bootstrap)
            return false;

        return !string.IsNullOrWhiteSpace(sceneDefinition.AddressableKey);
    }

    /// <summary>
    /// Resolves whether one scene definition is not loaded by Unity Build Settings.
    /// /params sceneDefinition Scene definition being inspected.
    /// /returns True when Build Settings should ignore the entry.
    /// </summary>
    private static bool ShouldSkipBuildSettings(GameSceneDefinition sceneDefinition)
    {
        switch (sceneDefinition.SceneKind)
        {
            case GameSceneKind.SubScene:
            case GameSceneKind.PersistentPlayer:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
