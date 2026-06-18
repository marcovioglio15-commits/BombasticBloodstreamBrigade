using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameSceneManagementProjectSetupSceneUtility;

/// <summary>
/// Applies the loading-progress view upgrade to configured bootstrap scenes without rewriting Scene Manager presets.
/// </summary>
public static class GameSceneManagementLoadingProgressSceneUpgradeUtility
{
    #region Constants
    private const string SceneManagerPresetFilter = "t:GameSceneManagerPreset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Batch entry point used by Unity -executeMethod to upgrade bootstrap fade canvases.
    /// </summary>
    public static void ExecuteBatchUpgrade()
    {
        UpgradeConfiguredBootstrapScenes(true);
    }

    /// <summary>
    /// Adds the loading-progress view to every bootstrap fade canvas referenced by Scene Manager presets.
    /// </summary>
    /// <param name="logToConsole">True when upgrade progress should be logged.</param>
    public static void UpgradeConfiguredBootstrapScenes(bool logToConsole)
    {
        HashSet<string> upgradedScenePaths = new HashSet<string>();
        string[] presetGuids = AssetDatabase.FindAssets(SceneManagerPresetFilter);

        for (int index = 0; index < presetGuids.Length; index++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[index]);
            GameSceneManagerPreset preset = AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(presetPath);

            if (!TryResolveBootstrapScenePath(preset, out string scenePath))
                continue;

            if (!upgradedScenePaths.Add(scenePath))
                continue;

            UpgradeBootstrapScene(scenePath, logToConsole);
        }

        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the bootstrap scene path from one Scene Manager preset.
    /// </summary>
    /// <param name="preset">Scene Manager preset to inspect.</param>
    /// <param name="scenePath">Resolved bootstrap scene path.</param>
    /// <returns>True when a valid scene path was resolved.</returns>
    private static bool TryResolveBootstrapScenePath(GameSceneManagerPreset preset, out string scenePath)
    {
        scenePath = string.Empty;

        if (preset == null)
            return false;

        if (preset.TryFindScene(preset.BootstrapSceneId, out GameSceneDefinition bootstrapScene) &&
            bootstrapScene != null &&
            !string.IsNullOrWhiteSpace(bootstrapScene.ScenePath))
        {
            scenePath = bootstrapScene.ScenePath;
            return true;
        }

        IReadOnlyList<GameSceneDefinition> sceneDefinitions = preset.SceneDefinitions;

        for (int index = 0; index < sceneDefinitions.Count; index++)
        {
            GameSceneDefinition sceneDefinition = sceneDefinitions[index];

            if (sceneDefinition == null || sceneDefinition.SceneKind != GameSceneKind.Bootstrap)
                continue;

            if (string.IsNullOrWhiteSpace(sceneDefinition.ScenePath))
                continue;

            scenePath = sceneDefinition.ScenePath;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Opens one bootstrap scene, upgrades its fade canvas and saves it when a view exists.
    /// </summary>
    /// <param name="scenePath">Project-relative bootstrap scene path.</param>
    /// <param name="logToConsole">True when the upgrade should log scene-level status.</param>
    private static void UpgradeBootstrapScene(string scenePath, bool logToConsole)
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

        if (sceneAsset == null)
        {
            if (logToConsole)
                Debug.LogWarning("[GameSceneManagementLoadingProgressSceneUpgradeUtility] Bootstrap scene path is not a scene asset: " + scenePath + ".");

            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        if (!scene.IsValid())
            return;

        GameSceneFadeCanvasView fadeView = FindFirstComponentInScene<GameSceneFadeCanvasView>(scene);

        if (fadeView == null)
        {
            if (logToConsole)
                Debug.LogWarning("[GameSceneManagementLoadingProgressSceneUpgradeUtility] No GameSceneFadeCanvasView found in " + scenePath + ".");

            return;
        }

        GameSceneManagementProjectSetupLoadingProgressUtility.EnsureLoadingProgressView(fadeView.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (logToConsole)
            Debug.Log("[GameSceneManagementLoadingProgressSceneUpgradeUtility] Upgraded loading progress view in " + scenePath + ".");
    }
    #endregion

    #endregion
}
