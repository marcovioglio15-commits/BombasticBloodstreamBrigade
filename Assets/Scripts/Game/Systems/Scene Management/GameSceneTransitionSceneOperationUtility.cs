using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides managed Unity scene load and unload steps used by the ECS transition executor.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneTransitionSceneOperationUtility
{
    #region Methods

    #region Load
    /// <summary>
    /// Advances one scene-load step and optionally activates the loaded scene when complete.
    /// /params sceneDefinition Scene definition being loaded.
    /// /params config Scene manager runtime config.
    /// /params forceReload True when an already loaded scene should be loaded again.
    /// /params setActiveScene True when this loaded scene should become Unity's active scene.
    /// /params activeOperation Active Unity async operation shared by the transition executor.
    /// /params loadComplete Mutable flag set when the scene is loaded or skipped.
    /// /returns True while an asynchronous load operation is still running.
    /// </summary>
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
    /// /params sceneDefinition Target scene definition.
    /// /params config Scene manager runtime config.
    /// /params forceReload True when an already loaded scene should be loaded again.
    /// /params setActiveScene True when an already loaded scene should become active immediately.
    /// /params activeOperation Active Unity async operation shared by the transition executor.
    /// /returns True when an asynchronous load operation was started.
    /// </summary>
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
    /// /params sceneDefinition Target scene definition.
    /// /params activeOperation Active Unity async operation shared by the transition executor.
    /// /returns True when a SceneManager async load operation was started.
    /// </summary>
    private static bool TryStartSceneManagerLoad(GameSceneDefinitionElement sceneDefinition,
                                                 ref GameSceneSceneOperationState activeOperation)
    {
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
    #endregion

    #region Unload
    /// <summary>
    /// Advances one scene-unload step and marks the step complete when the scene is not loaded or the operation finishes.
    /// /params sceneDefinition Scene definition being unloaded.
    /// /params hasBootstrapScene True when a persistent bootstrap scene is available as a safe active scene.
    /// /params bootstrapScene Bootstrap scene definition used as an unload safety target.
    /// /params targetScene Target scene definition used as an unload safety target.
    /// /params activeOperation Active Unity async operation shared by the transition executor.
    /// /params unloadComplete Mutable flag set when unload has completed or was skipped.
    /// /returns True while an asynchronous unload operation is still running.
    /// </summary>
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
    /// /params sceneDefinition Source scene definition.
    /// /params hasBootstrapScene True when a persistent bootstrap scene is available as a safe active scene.
    /// /params bootstrapScene Bootstrap scene definition used as an unload safety target.
    /// /params targetScene Target scene definition used as an unload safety target.
    /// /params activeOperation Active Unity async operation shared by the transition executor.
    /// /returns True when an asynchronous unload operation was started.
    /// </summary>
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
    /// /params sceneDefinition Scene definition that should become active.
    /// /returns None.
    /// </summary>
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
    /// /params sceneDefinition Scene definition that finished loading.
    /// /params setActiveScene True when the loaded scene should become Unity's active scene.
    /// /params activeOperation Active operation state to complete and clear.
    /// /returns True when the operation produced a loaded scene.
    /// </summary>
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
    /// /params activeOperation Active operation state to complete and clear.
    /// /returns None.
    /// </summary>
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
    /// /params config Scene manager runtime config.
    /// /params sceneDefinition Scene definition being processed.
    /// /returns True when the Addressables backend should own the scene.
    /// </summary>
    private static bool ShouldUseAddressables(GameSceneManagerConfig config, GameSceneDefinitionElement sceneDefinition)
    {
        if (config.LoadBackend != GameSceneLoadBackend.Addressables)
            return false;

        if (sceneDefinition.SceneKind == GameSceneKind.Bootstrap)
            return false;

        return sceneDefinition.AddressableKey.Length > 0;
    }
    #endregion

    #endregion
}
