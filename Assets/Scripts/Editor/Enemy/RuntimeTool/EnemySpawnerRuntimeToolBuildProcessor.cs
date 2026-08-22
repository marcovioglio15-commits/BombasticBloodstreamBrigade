using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates runtime-spawner compilation and removes its authored main-menu objects from excluded player scenes.
/// </summary>
internal sealed class EnemySpawnerRuntimeToolBuildProcessor : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    #region Properties
    public int callbackOrder
    {
        get
        {
            return -2000;
        }
    }
    #endregion

    #region Methods

    #region Build Callbacks
    /// <summary>
    /// Prevents a build from continuing with stale conditional-compilation state.
    /// </summary>
    /// <param name="report">Build report identifying the target group being validated.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        if (EnemySpawnerRuntimeToolBuildFeatureUtility.IsBuildTargetGroupSynchronized(report.summary.platformGroup))
            return;

        EnemySpawnerRuntimeToolBuildFeatureUtility.SynchronizeAllBuildTargetGroups();
        throw new BuildFailedException("Runtime Enemy Spawner Tool build symbols were stale and have been synchronized. Allow Unity to finish compiling, then start the build again.");
    }

    /// <summary>
    /// Removes the test-tool button, controller, panel hierarchy, and catalog reference from each excluded build scene copy.
    /// </summary>
    /// <param name="scene">Temporary scene copy currently processed for the player build.</param>
    /// <param name="report">Build report for the active player build, or null while Addressables process a scene.</param>
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (!EnemySpawnerRuntimeToolBuildFeatureUtility.IsExcludedFromPlayerBuilds)
            return;

        HashSet<GameObject> removalRoots = new HashSet<GameObject>();
        GameObject[] sceneRoots = scene.GetRootGameObjects();

        // Resolve authored references before destroying anything so nested tool objects are removed only once.
        for (int rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
        {
            CollectMainMenuToolButtons(sceneRoots[rootIndex], removalRoots);
            CollectRuntimeToolPanels(sceneRoots[rootIndex], removalRoots);
        }

        foreach (GameObject removalRoot in removalRoots)
        {
            if (removalRoot != null)
                Object.DestroyImmediate(removalRoot);
        }
    }
    #endregion

    #region Collection
    /// <summary>
    /// Collects runtime-spawner buttons referenced by main-menu controllers under one scene root.
    /// </summary>
    /// <param name="sceneRoot">Scene root searched for main-menu controllers.</param>
    /// <param name="removalRoots">Unique GameObjects scheduled for removal.</param>
    private static void CollectMainMenuToolButtons(GameObject sceneRoot, HashSet<GameObject> removalRoots)
    {
        MainMenuController[] menuControllers = sceneRoot.GetComponentsInChildren<MainMenuController>(true);

        for (int controllerIndex = 0; controllerIndex < menuControllers.Length; controllerIndex++)
        {
            SerializedObject serializedController = new SerializedObject(menuControllers[controllerIndex]);
            SerializedProperty buttonProperty = serializedController.FindProperty("enemySpawnerToolButton");
            Button toolButton = buttonProperty != null ? buttonProperty.objectReferenceValue as Button : null;

            if (toolButton != null)
                removalRoots.Add(toolButton.gameObject);
        }
    }

    /// <summary>
    /// Collects runtime-spawner controllers and their detached panel hierarchies under one scene root.
    /// </summary>
    /// <param name="sceneRoot">Scene root searched for runtime tool controllers.</param>
    /// <param name="removalRoots">Unique GameObjects scheduled for removal.</param>
    private static void CollectRuntimeToolPanels(GameObject sceneRoot, HashSet<GameObject> removalRoots)
    {
        EnemySpawnerRuntimeToolPanelController[] toolControllers =
            sceneRoot.GetComponentsInChildren<EnemySpawnerRuntimeToolPanelController>(true);

        for (int controllerIndex = 0; controllerIndex < toolControllers.Length; controllerIndex++)
        {
            EnemySpawnerRuntimeToolPanelController toolController = toolControllers[controllerIndex];
            SerializedObject serializedController = new SerializedObject(toolController);
            SerializedProperty panelRootProperty = serializedController.FindProperty("panelRoot");
            GameObject panelRoot = panelRootProperty != null ? panelRootProperty.objectReferenceValue as GameObject : null;

            if (panelRoot != null)
                removalRoots.Add(panelRoot);

            removalRoots.Add(toolController.gameObject);
        }
    }
    #endregion

    #endregion
}
