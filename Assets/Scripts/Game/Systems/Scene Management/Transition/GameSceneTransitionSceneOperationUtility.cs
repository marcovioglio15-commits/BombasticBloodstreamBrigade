using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Provides managed Unity scene load and unload steps used by the ECS transition executor.
/// </summary>
internal static class GameSceneTransitionSceneOperationUtility
{
    #region Methods

    #region Load
    /// <summary>
    /// Advances one scene-load step and optionally activates the loaded scene when complete.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition being loaded.</param>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="forceReload">True when an already loaded scene should be loaded again.</param>
    /// <param name="setActiveScene">True when this loaded scene should become Unity's active scene.</param>
    /// <param name="activeOperation">Active Unity async operation shared by the transition executor.</param>
    /// <param name="loadComplete">Mutable flag set when the scene is loaded or skipped.</param>
    /// <returns>True while an asynchronous load operation is still running.</returns>
    public static bool TickLoadStep(GameSceneDefinitionElement sceneDefinition,
                                    GameSceneManagerConfig config,
                                    bool forceReload,
                                    bool setActiveScene,
                                    ref GameSceneSceneOperationState activeOperation,
                                    ref bool loadComplete)
    {
        if (loadComplete)
            return false;

        if (!activeOperation.IsRunning)
        {
            if (!TryStartLoad(sceneDefinition, config, forceReload, setActiveScene, ref activeOperation))
            {
                loadComplete = true;
                return false;
            }
        }

        if (!activeOperation.IsDone)
            return true;

        if (TryCompleteLoad(sceneDefinition, setActiveScene, ref activeOperation))
        {
            loadComplete = true;
            return false;
        }

        Debug.LogWarning("[GameSceneManager] Falling back to Build Settings after Addressables load failure: " + sceneDefinition.SceneId.ToString() + ".");

        if (TryStartSceneManagerLoad(sceneDefinition, ref activeOperation))
            return true;

        loadComplete = true;
        return false;
    }

    /// <summary>
    /// Starts loading the target scene with the active backend.
    /// </summary>
    /// <param name="sceneDefinition">Target scene definition.</param>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="forceReload">True when an already loaded scene should be loaded again.</param>
    /// <param name="setActiveScene">True when an already loaded scene should become active immediately.</param>
    /// <param name="activeOperation">Active Unity async operation shared by the transition executor.</param>
    /// <returns>True when an asynchronous load operation was started.</returns>
    private static bool TryStartLoad(GameSceneDefinitionElement sceneDefinition,
                                     GameSceneManagerConfig config,
                                     bool forceReload,
                                     bool setActiveScene,
                                     ref GameSceneSceneOperationState activeOperation)
    {
        Scene existingScene = GameSceneLoadBackendUtility.ResolveLoadedScene(sceneDefinition);

        if (existingScene.IsValid() && existingScene.isLoaded && !forceReload)
        {
            if (setActiveScene)
                SceneManager.SetActiveScene(existingScene);

            return false;
        }

        if (ShouldUseAddressables(config, sceneDefinition) &&
            GameSceneAddressablesRuntimeUtility.TryStartLoad(sceneDefinition, ref activeOperation))
        {
            return true;
        }

        return TryStartSceneManagerLoad(sceneDefinition, ref activeOperation);
    }

    /// <summary>
    /// Starts loading a scene through Unity Build Settings metadata, using scene path/name only as an editor-safe fallback key.
    /// </summary>
    /// <param name="sceneDefinition">Target scene definition.</param>
    /// <param name="activeOperation">Active Unity async operation shared by the transition executor.</param>
    /// <returns>True when a SceneManager async load operation was started.</returns>
    private static bool TryStartSceneManagerLoad(GameSceneDefinitionElement sceneDefinition,
                                                 ref GameSceneSceneOperationState activeOperation)
    {
#if UNITY_EDITOR
        if (Application.isPlaying && TryStartEditorSceneManagerLoad(sceneDefinition, ref activeOperation))
            return true;
#endif

        if (sceneDefinition.BuildIndex >= 0)
        {
            AsyncOperation sceneManagerOperation = SceneManager.LoadSceneAsync(sceneDefinition.BuildIndex, LoadSceneMode.Additive);
            activeOperation = GameSceneSceneOperationState.FromSceneManager(sceneManagerOperation);
            return sceneManagerOperation != null;
        }

        string scenePath = sceneDefinition.ScenePath.ToString();
        string sceneName = sceneDefinition.SceneName.ToString();
        string loadKey = !string.IsNullOrWhiteSpace(scenePath) ? scenePath : sceneName;

        if (string.IsNullOrWhiteSpace(loadKey))
        {
            Debug.LogWarning("[GameSceneManager] Target scene has no load key: " + sceneDefinition.SceneId.ToString() + ".");
            return false;
        }

        try
        {
            AsyncOperation fallbackOperation = SceneManager.LoadSceneAsync(loadKey, LoadSceneMode.Additive);
            activeOperation = GameSceneSceneOperationState.FromSceneManager(fallbackOperation);
            return fallbackOperation != null;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[GameSceneManager] SceneManager failed to load scene " + sceneDefinition.SceneId.ToString() + ": " + exception.Message);
            return false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Starts an Editor play-mode scene load by asset path, even when the scene is intentionally absent from Build Settings.
    /// </summary>
    /// <param name="sceneDefinition">Target scene definition.</param>
    /// <param name="activeOperation">Active Unity async operation shared by the transition executor.</param>
    /// <returns>True when an Editor scene load operation was started.</returns>
    private static bool TryStartEditorSceneManagerLoad(GameSceneDefinitionElement sceneDefinition,
                                                       ref GameSceneSceneOperationState activeOperation)
    {
        string scenePath = sceneDefinition.ScenePath.ToString();

        if (string.IsNullOrWhiteSpace(scenePath))
            return false;

        try
        {
            LoadSceneParameters loadParameters = new LoadSceneParameters(LoadSceneMode.Additive);
            AsyncOperation sceneManagerOperation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, loadParameters);

            if (sceneManagerOperation == null)
                return false;

            activeOperation = GameSceneSceneOperationState.FromSceneManager(sceneManagerOperation);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[GameSceneManager] Editor play-mode scene load failed for " +
                             sceneDefinition.SceneId.ToString() +
                             ": " +
                             exception.Message);
            return false;
        }
    }
#endif
    #endregion

    #region Unload
    /// <summary>
    /// Advances one scene-unload step and marks the step complete when the scene is not loaded or the operation finishes.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition being unloaded.</param>
    /// <param name="hasBootstrapScene">True when a persistent bootstrap scene is available as a safe active scene.</param>
    /// <param name="bootstrapScene">Bootstrap scene definition used as an unload safety target.</param>
    /// <param name="targetScene">Target scene definition used as an unload safety target.</param>
    /// <param name="activeOperation">Active Unity async operation shared by the transition executor.</param>
    /// <param name="unloadComplete">Mutable flag set when unload has completed or was skipped.</param>
    /// <returns>True while an asynchronous unload operation is still running.</returns>
    public static bool TickUnloadStep(GameSceneDefinitionElement sceneDefinition,
                                      bool hasBootstrapScene,
                                      GameSceneDefinitionElement bootstrapScene,
                                      GameSceneDefinitionElement targetScene,
                                      GameSceneManagerConfig config,
                                      ref GameSceneSceneOperationState activeOperation,
                                      ref bool unloadComplete)
    {
        if (unloadComplete)
            return false;

        if (GameProceduralRoomStreamingRuntimeUtility.TryTickExternalUnload(sceneDefinition.SceneId,
                                                                            out bool transactionalUnloadComplete))
        {
            unloadComplete = transactionalUnloadComplete;
            return !transactionalUnloadComplete;
        }

        if (!activeOperation.IsRunning)
        {
            if (!TryStartUnload(sceneDefinition, hasBootstrapScene, bootstrapScene, targetScene, config, ref activeOperation))
            {
                unloadComplete = true;
                return false;
            }
        }

        if (!activeOperation.IsDone)
            return true;

        CompleteUnload(ref activeOperation);
        unloadComplete = true;
        return false;
    }

    /// <summary>
    /// Starts unloading one loaded source scene when its policy allows automatic unload.
    /// </summary>
    /// <param name="sceneDefinition">Source scene definition.</param>
    /// <param name="hasBootstrapScene">True when a persistent bootstrap scene is available as a safe active scene.</param>
    /// <param name="bootstrapScene">Bootstrap scene definition used as an unload safety target.</param>
    /// <param name="targetScene">Target scene definition used as an unload safety target.</param>
    /// <param name="activeOperation">Active Unity async operation shared by the transition executor.</param>
    /// <returns>True when an asynchronous unload operation was started.</returns>
    private static bool TryStartUnload(GameSceneDefinitionElement sceneDefinition,
                                       bool hasBootstrapScene,
                                       GameSceneDefinitionElement bootstrapScene,
                                       GameSceneDefinitionElement targetScene,
                                       GameSceneManagerConfig config,
                                       ref GameSceneSceneOperationState activeOperation)
    {
        if (sceneDefinition.UnloadPolicy != GameSceneUnloadPolicy.UnloadOnTransition)
            return false;

        Scene scene = GameSceneLoadBackendUtility.ResolveLoadedScene(sceneDefinition);

        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameSceneTransitionExecutionUtility.TrySetSafeActiveSceneBeforeUnload(sceneDefinition, hasBootstrapScene, bootstrapScene, targetScene);

        if (ShouldUseAddressables(config, sceneDefinition) &&
            GameSceneAddressablesRuntimeUtility.TryStartUnload(sceneDefinition, ref activeOperation))
        {
            return true;
        }

        AsyncOperation sceneManagerOperation = SceneManager.UnloadSceneAsync(scene);
        activeOperation = GameSceneSceneOperationState.FromSceneManager(sceneManagerOperation);
        return sceneManagerOperation != null;
    }
    #endregion

    #region Scene State
    /// <summary>
    /// Sets one loaded scene as active when it resolves successfully.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition that should become active.</param>
    private static void TrySetSceneActive(GameSceneDefinitionElement sceneDefinition)
    {
        Scene loadedScene = GameSceneLoadBackendUtility.ResolveLoadedScene(sceneDefinition);

        if (loadedScene.IsValid() && loadedScene.isLoaded)
            SceneManager.SetActiveScene(loadedScene);
    }
    #endregion

    #region Completion
    /// <summary>
    /// Completes a load operation and applies backend-specific active scene/handle ownership.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition that finished loading.</param>
    /// <param name="setActiveScene">True when the loaded scene should become Unity's active scene.</param>
    /// <param name="activeOperation">Active operation state to complete and clear.</param>
    /// <returns>True when the operation produced a loaded scene.</returns>
    private static bool TryCompleteLoad(GameSceneDefinitionElement sceneDefinition,
                                        bool setActiveScene,
                                        ref GameSceneSceneOperationState activeOperation)
    {
        if (activeOperation.OperationKind == GameSceneSceneOperationKind.AddressablesLoad)
        {
            bool succeeded = GameSceneAddressablesRuntimeUtility.CompleteLoad(sceneDefinition,
                                                                              activeOperation.AddressablesOperation,
                                                                              setActiveScene);
            activeOperation.Clear();
            return succeeded;
        }

        activeOperation.Clear();

        if (setActiveScene)
            TrySetSceneActive(sceneDefinition);

        return true;
    }

    /// <summary>
    /// Completes an unload operation and releases Addressables ownership when required.
    /// </summary>
    /// <param name="activeOperation">Active operation state to complete and clear.</param>
    private static void CompleteUnload(ref GameSceneSceneOperationState activeOperation)
    {
        if (activeOperation.OperationKind == GameSceneSceneOperationKind.AddressablesUnload)
        {
            GameSceneAddressablesRuntimeUtility.CompleteUnload(activeOperation.AddressablesSceneId,
                                                              activeOperation.AddressablesOperation);
        }

        activeOperation.Clear();
    }
    #endregion

    #region Backend
    /// <summary>
    /// Resolves whether one scene should be loaded through Addressables instead of Build Settings.
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="sceneDefinition">Scene definition being processed.</param>
    /// <returns>True when the Addressables backend should own the scene.</returns>
    private static bool ShouldUseAddressables(GameSceneManagerConfig config, GameSceneDefinitionElement sceneDefinition)
    {
#if UNITY_EDITOR
        // Editor play mode must load local scenes so uncommitted SubScene bake changes are visible immediately.
        if (Application.isPlaying)
            return false;
#endif

        if (config.LoadBackend != GameSceneLoadBackend.Addressables)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.Bootstrap)
            return false;

        return sceneDefinition.AddressableKey.Length > 0;
    }
    #endregion

    #endregion
}
