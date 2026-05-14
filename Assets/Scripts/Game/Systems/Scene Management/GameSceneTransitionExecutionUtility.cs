using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides pure helpers used by the managed scene transition execution system.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneTransitionExecutionUtility
{
    #region Methods

    #region Request Resolution
    /// <summary>
    /// Resolves the target scene and transition override data for a request.
    /// /params config Scene manager runtime config.
    /// /params scenes Runtime scene definitions.
    /// /params transitions Runtime transition definitions.
    /// /params request Request to resolve.
    /// /params transitionState Current transition state.
    /// /params sceneDefinition Resolved target scene definition.
    /// /params transition Resolved transition override data.
    /// /returns True when a valid target scene was found.
    /// </summary>
    public static bool TryResolveTargetScene(GameSceneManagerConfig config,
                                             DynamicBuffer<GameSceneDefinitionElement> scenes,
                                             DynamicBuffer<GameSceneTransitionElement> transitions,
                                             GameSceneTransitionRequest request,
                                             GameSceneTransitionState transitionState,
                                             out GameSceneDefinitionElement sceneDefinition,
                                             out GameSceneTransitionElement transition)
    {
        sceneDefinition = default;
        transition = default;
        FixedString64Bytes targetId = request.TargetSceneId;

        if (request.TransitionId.Length > 0 &&
            GameSceneLoadBackendUtility.TryFindTransition(transitions, request.TransitionId, out transition))
        {
            targetId = transition.ToSceneId;
        }
        else
        {
            targetId = ResolveTargetSceneId(config, scenes, request, transitionState);
        }

        if (targetId.Length <= 0)
        {
            Debug.LogWarning("[GameSceneManager] Scene transition request has no target scene.");
            return false;
        }

        if (GameSceneLoadBackendUtility.TryFindScene(scenes, targetId, out sceneDefinition))
        {
            if (sceneDefinition.SceneKind != GameSceneKind.PersistentPlayer)
                return true;

            Debug.LogWarning("[GameSceneManager] Persistent player scenes are loaded automatically by gameplay transitions and cannot be targeted directly: " + targetId.ToString() + ".");
            return false;
        }

        Debug.LogWarning("[GameSceneManager] Target scene is not defined: " + targetId.ToString() + ".");
        return false;
    }

    /// <summary>
    /// Resolves the target scene ID for command-style transition requests.
    /// /params config Scene manager runtime config.
    /// /params scenes Runtime scene definitions.
    /// /params request Request to inspect.
    /// /params transitionState Current transition state.
    /// /returns Target scene ID or an empty ID when unresolved.
    /// </summary>
    private static FixedString64Bytes ResolveTargetSceneId(GameSceneManagerConfig config,
                                                           DynamicBuffer<GameSceneDefinitionElement> scenes,
                                                           GameSceneTransitionRequest request,
                                                           GameSceneTransitionState transitionState)
    {
        switch (request.RequestType)
        {
            case GameSceneTransitionRequestType.LoadDefaultGameplay:
                return config.DefaultGameplaySceneId;
            case GameSceneTransitionRequestType.LoadMainMenu:
                return config.MainMenuSceneId;
            case GameSceneTransitionRequestType.RestartActiveScene:
                return transitionState.ActiveSceneId;
            case GameSceneTransitionRequestType.LoadNextScene:
                if (GameSceneLoadBackendUtility.TryFindNextScene(scenes, transitionState.ActiveSceneId, out GameSceneDefinitionElement nextScene))
                    return nextScene.SceneId;

                return default;
            default:
                return request.TargetSceneId;
        }
    }
    #endregion

    #region Startup
    /// <summary>
    /// Initializes active scene tracking from the loaded Unity active scene when possible.
    /// /params config Scene manager runtime config.
    /// /params scenes Runtime scene definitions.
    /// /params transitionState Mutable transition state component.
    /// /returns None.
    /// </summary>
    public static void InitializeStateFromLoadedScene(GameSceneManagerConfig config,
                                                      DynamicBuffer<GameSceneDefinitionElement> scenes,
                                                      ref GameSceneTransitionState transitionState)
    {
        Scene activeUnityScene = SceneManager.GetActiveScene();
        transitionState.Initialized = 1;

        if (!activeUnityScene.IsValid())
            return;

        for (int index = 0; index < scenes.Length; index++)
        {
            GameSceneDefinitionElement sceneDefinition = scenes[index];

            if (MatchesUnityScene(sceneDefinition, activeUnityScene))
            {
                transitionState.ActiveSceneId = sceneDefinition.SceneId;
                return;
            }
        }

        if (config.BootstrapSceneId.Length > 0)
            transitionState.ActiveSceneId = config.BootstrapSceneId;
    }

    /// <summary>
    /// Resolves whether the startup transition should run from the current active scene state.
    /// /params config Scene manager runtime config.
    /// /params transitionState Current transition state.
    /// /returns True only when the manager is still at bootstrap or has not resolved an active scene yet.
    /// </summary>
    public static bool ShouldRunInitialTransition(GameSceneManagerConfig config, GameSceneTransitionState transitionState)
    {
        if (transitionState.ActiveSceneId.Length <= 0)
            return true;

        if (config.BootstrapSceneId.Length <= 0)
            return false;

        return transitionState.ActiveSceneId.Equals(config.BootstrapSceneId);
    }

    /// <summary>
    /// Resolves whether a startup transition should begin already black to cover initial scene loading.
    /// /params config Scene manager runtime config.
    /// /params request Transition request being started.
    /// /params transitionState Current transition state before the request starts.
    /// /returns True when the transition is the configured bootstrap-to-initial load.
    /// </summary>
    public static bool ShouldStartBehindBlack(GameSceneManagerConfig config,
                                              GameSceneTransitionRequest request,
                                              GameSceneTransitionState transitionState)
    {
        if (config.InitialSceneId.Length <= 0)
            return false;

        if (request.RequestType != GameSceneTransitionRequestType.LoadScene)
            return false;

        if (!request.TargetSceneId.Equals(config.InitialSceneId))
            return false;

        return ShouldRunInitialTransition(config, transitionState);
    }
    #endregion

    #region Runtime Cleanup
    /// <summary>
    /// Clears transient gameplay entities that are not owned by scene streaming before a restart loads the new instance.
    /// /params entityManager EntityManager that owns transient gameplay runtime entities.
    /// /params cleanupComplete True when this transition has already run the cleanup check.
    /// /params reloadActiveScene True when the active scene is being restarted.
    /// /params targetScene Target scene definition for the active transition.
    /// /returns True once the cleanup gate has been consumed for this transition.
    /// </summary>
    public static bool RunPreLoadRuntimeCleanupIfNeeded(EntityManager entityManager,
                                                        bool cleanupComplete,
                                                        bool reloadActiveScene,
                                                        GameSceneDefinitionElement targetScene)
    {
        if (cleanupComplete)
            return true;

        if (reloadActiveScene && GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene))
            GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(entityManager);

        return true;
    }
    #endregion

    #region Scene Activation
    /// <summary>
    /// Moves Unity's active scene away from the scene about to be unloaded when possible.
    /// /params sceneBeingUnloaded Scene definition passed to SceneManager.UnloadSceneAsync.
    /// /params hasBootstrapScene True when bootstrapScene contains authored data.
    /// /params bootstrapScene Persistent bootstrap scene candidate.
    /// /params targetScene Active transition target scene candidate.
    /// /returns None.
    /// </summary>
    public static void TrySetSafeActiveSceneBeforeUnload(GameSceneDefinitionElement sceneBeingUnloaded,
                                                         bool hasBootstrapScene,
                                                         GameSceneDefinitionElement bootstrapScene,
                                                         GameSceneDefinitionElement targetScene)
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid())
            return;

        if (!MatchesUnityScene(sceneBeingUnloaded, activeScene))
            return;

        if (hasBootstrapScene && TrySetLoadedSceneActive(bootstrapScene, sceneBeingUnloaded))
            return;

        if (TrySetLoadedSceneActive(targetScene, sceneBeingUnloaded))
            return;

        TrySetAnyLoadedSceneActive(sceneBeingUnloaded);
    }

    /// <summary>
    /// Sets a loaded scene active when it does not match the scene currently being unloaded.
    /// /params candidate Candidate scene definition.
    /// /params excludedScene Scene definition that must not be selected.
    /// /returns True when the candidate scene became active.
    /// </summary>
    private static bool TrySetLoadedSceneActive(GameSceneDefinitionElement candidate, GameSceneDefinitionElement excludedScene)
    {
        Scene candidateScene = GameSceneLoadBackendUtility.ResolveLoadedScene(candidate);

        if (!candidateScene.IsValid() || !candidateScene.isLoaded)
            return false;

        if (MatchesUnityScene(excludedScene, candidateScene))
            return false;

        SceneManager.SetActiveScene(candidateScene);
        return true;
    }

    /// <summary>
    /// Sets any loaded Unity scene active when no authored bootstrap or target scene can be used.
    /// /params excludedScene Scene definition that must not be selected.
    /// /returns True when any loaded scene became active.
    /// </summary>
    private static bool TrySetAnyLoadedSceneActive(GameSceneDefinitionElement excludedScene)
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene candidateScene = SceneManager.GetSceneAt(index);

            if (!candidateScene.IsValid() || !candidateScene.isLoaded)
                continue;

            if (MatchesUnityScene(excludedScene, candidateScene))
                continue;

            SceneManager.SetActiveScene(candidateScene);
            return true;
        }

        return false;
    }
    #endregion

    #region Fade
    /// <summary>
    /// Writes fade state values with the configured fade color.
    /// /params fadeState Mutable fade presentation component.
    /// /params alpha Desired overlay alpha.
    /// /params visible True when the overlay should be visible.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    public static void SetFade(ref GameSceneFadePresentationState fadeState, float alpha, bool visible, GameSceneManagerConfig config)
    {
        fadeState.Alpha = Mathf.Clamp01(alpha);
        fadeState.Visible = visible ? (byte)1 : (byte)0;
        fadeState.Color = config.FadeColor;
    }
    #endregion

    #region Matching
    /// <summary>
    /// Matches a scene definition against one loaded Unity scene.
    /// /params sceneDefinition Scene definition to inspect.
    /// /params scene Loaded Unity scene.
    /// /returns True when path or name matches.
    /// </summary>
    public static bool MatchesUnityScene(GameSceneDefinitionElement sceneDefinition, Scene scene)
    {
        string scenePath = sceneDefinition.ScenePath.ToString();

        if (!string.IsNullOrWhiteSpace(scenePath) && string.Equals(scenePath, scene.path, System.StringComparison.Ordinal))
            return true;

        string sceneName = sceneDefinition.SceneName.ToString();
        return !string.IsNullOrWhiteSpace(sceneName) && string.Equals(sceneName, scene.name, System.StringComparison.Ordinal);
    }
    #endregion

    #region Logging
    /// <summary>
    /// Logs singleton count problems once until the manager count becomes valid again.
    /// /params managerCount Current number of manager entities.
    /// /params alreadyLogged True when the current invalid count has already been reported.
    /// /returns True after an invalid manager count has been handled.
    /// </summary>
    public static bool LogManagerCountWarning(int managerCount, bool alreadyLogged)
    {
        if (alreadyLogged)
            return true;

        if (managerCount > 1)
            Debug.LogWarning("[GameSceneManager] Expected one scene manager singleton, found " + managerCount + ".");

        return true;
    }
    #endregion

    #endregion
}
