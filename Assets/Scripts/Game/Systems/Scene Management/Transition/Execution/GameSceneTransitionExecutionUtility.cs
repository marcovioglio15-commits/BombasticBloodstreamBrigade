using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides pure helpers used by the managed scene transition execution system.
/// </summary>
internal static class GameSceneTransitionExecutionUtility
{
    #region Methods

    #region Request Resolution
    /// <summary>
    /// Resolves the target scene and transition override data for a request.
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="scenes">Runtime scene definitions.</param>
    /// <param name="transitions">Runtime transition definitions.</param>
    /// <param name="request">Request to resolve.</param>
    /// <param name="transitionState">Current transition state.</param>
    /// <param name="sceneDefinition">Resolved target scene definition.</param>
    /// <param name="transition">Resolved transition override data.</param>
    /// <returns>True when a valid target scene was found.</returns>
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
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="scenes">Runtime scene definitions.</param>
    /// <param name="request">Request to inspect.</param>
    /// <param name="transitionState">Current transition state.</param>
    /// <returns>Target scene ID or an empty ID when unresolved.</returns>
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
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="scenes">Runtime scene definitions.</param>
    /// <param name="transitionState">Mutable transition state component.</param>
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
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="transitionState">Current transition state.</param>
    /// <returns>True only when the manager is still at bootstrap or has not resolved an active scene yet.</returns>
    public static bool ShouldRunInitialTransition(GameSceneManagerConfig config, GameSceneTransitionState transitionState)
    {
        if (transitionState.ActiveSceneId.Length <= 0)
            return true;

        if (config.BootstrapSceneId.Length <= 0)
            return false;

        return transitionState.ActiveSceneId.Equals(config.BootstrapSceneId);
    }

    /// <summary>
    /// Builds the configured initial load request when bootstrap state still requires automatic startup.
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="transitionState">Current initialized scene state.</param>
    /// <param name="request">Initial load request when automatic startup remains eligible.</param>
    /// <returns>True when the executor should submit the returned request.</returns>
    public static bool TryCreateInitialRequest(GameSceneManagerConfig config,
                                               GameSceneTransitionState transitionState,
                                               out GameSceneTransitionRequest request)
    {
        request = default;

        if (config.AutoLoadInitialScene == 0 ||
            config.InitialSceneId.Length <= 0 ||
            transitionState.ActiveSceneId.Equals(config.InitialSceneId) ||
            !ShouldRunInitialTransition(config, transitionState))
        {
            return false;
        }

        request = new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            TargetSceneId = config.InitialSceneId,
            TransitionId = default
        };
        return true;
    }

    /// <summary>
    /// Resolves whether a startup transition should begin already black to cover initial scene loading.
    /// </summary>
    /// <param name="config">Scene manager runtime config.</param>
    /// <param name="request">Transition request being started.</param>
    /// <param name="transitionState">Current transition state before the request starts.</param>
    /// <returns>True when the transition is the configured bootstrap-to-initial load.</returns>
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
    /// Clears transient gameplay entities that are not owned by scene streaming before entering, leaving, or reloading gameplay.
    /// </summary>
    /// <param name="entityManager">EntityManager that owns transient gameplay runtime entities.</param>
    /// <param name="cleanupComplete">True when this transition has already run the cleanup check.</param>
    /// <param name="reloadActiveScene">True when the active scene is being restarted.</param>
    /// <param name="hasSourceScene">True when the transition resolved a source scene definition.</param>
    /// <param name="sourceScene">Resolved source scene definition.</param>
    /// <param name="targetScene">Target scene definition for the active transition.</param>
    /// <param name="purpose">Purpose of the active transition.</param>
    /// <param name="forceCleanup">True when procedural room isolation requires cleanup even between gameplay scenes.</param>
    /// <returns>True once the cleanup gate has been consumed for this transition.</returns>
    public static bool RunPreLoadRuntimeCleanupIfNeeded(EntityManager entityManager,
                                                        bool cleanupComplete,
                                                        bool reloadActiveScene,
                                                        bool hasSourceScene,
                                                        GameSceneDefinitionElement sourceScene,
                                                        GameSceneDefinitionElement targetScene,
                                                        GameSceneTransitionPurpose purpose,
                                                        bool forceCleanup)
    {
        if (cleanupComplete)
            return true;

        bool sourceIsGameplay = hasSourceScene &&
                                GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(sourceScene);
        bool targetIsGameplay = GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene);
        bool preservesActiveGameplayRuntime = sourceIsGameplay &&
                                              targetIsGameplay &&
                                              !reloadActiveScene;

        if (forceCleanup || !preservesActiveGameplayRuntime)
            GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(entityManager,
                                                                                                      ShouldPreserveRoomClearAttraction(purpose));

        return true;
    }

    /// <summary>
    /// Resolves whether procedural isolation requires transient gameplay cleanup across the current room boundary.
    /// </summary>
    /// <param name="purpose">Active transition purpose.</param>
    /// <param name="transactionalRoomStreaming">True when room streaming retains transactional ownership.</param>
    /// <param name="singleSlotRoomStreaming">True when the transactional policy replaces one authored slot.</param>
    /// <returns>True when cleanup must run even though both scenes are gameplay-like.</returns>
    public static bool ShouldForceProceduralRuntimeCleanup(GameSceneTransitionPurpose purpose,
                                                           bool transactionalRoomStreaming,
                                                           bool singleSlotRoomStreaming)
    {
        if (!GameSceneTransitionPurposeUtility.IsProcedural(purpose))
            return false;

        if (!transactionalRoomStreaming || singleSlotRoomStreaming)
            return true;

        return purpose == GameSceneTransitionPurpose.ProceduralInitialRoom;
    }

    /// <summary>
    /// Resolves whether room-clear-attracted drops may cross the active procedural transition boundary.
    /// </summary>
    /// <param name="purpose">Purpose of the active transition.</param>
    /// <returns>True for room traversal and level-boundary transitions inside the same procedural run.</returns>
    public static bool ShouldPreserveRoomClearAttraction(GameSceneTransitionPurpose purpose)
    {
        switch (purpose)
        {
            case GameSceneTransitionPurpose.ProceduralRoomTraversal:
            case GameSceneTransitionPurpose.ProceduralLevelBoundary:
                return true;
            default:
                return false;
        }
    }
    #endregion

    #region Scene Activation
    /// <summary>
    /// Moves Unity's active scene away from the scene about to be unloaded when possible.
    /// </summary>
    /// <param name="sceneBeingUnloaded">Scene definition passed to SceneManager.UnloadSceneAsync.</param>
    /// <param name="hasBootstrapScene">True when bootstrapScene contains authored data.</param>
    /// <param name="bootstrapScene">Persistent bootstrap scene candidate.</param>
    /// <param name="targetScene">Active transition target scene candidate.</param>
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
    /// </summary>
    /// <param name="candidate">Candidate scene definition.</param>
    /// <param name="excludedScene">Scene definition that must not be selected.</param>
    /// <returns>True when the candidate scene became active.</returns>
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
    /// </summary>
    /// <param name="excludedScene">Scene definition that must not be selected.</param>
    /// <returns>True when any loaded scene became active.</returns>
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
    /// Resolves the destructive phase that follows fade-out from the current unload policy.
    /// </summary>
    /// <param name="unloadSourceBeforeLoad">True when source content must retire before target loading.</param>
    /// <param name="hasSourceScene">True when a source scene can be unloaded.</param>
    /// <param name="hasSourceCompanionScene">True when a source companion can be unloaded.</param>
    /// <param name="reloadTargetCompanion">True when the target companion must be replaced.</param>
    /// <returns>PreUnload when any pre-load retirement is required; otherwise Loading.</returns>
    public static GameSceneTransitionPhase ResolvePhaseAfterFadeOut(bool unloadSourceBeforeLoad,
                                                                    bool hasSourceScene,
                                                                    bool hasSourceCompanionScene,
                                                                    bool reloadTargetCompanion)
    {
        bool unloadReloadedCompanion = reloadTargetCompanion && hasSourceCompanionScene;
        return unloadSourceBeforeLoad && (hasSourceScene || hasSourceCompanionScene) || unloadReloadedCompanion
            ? GameSceneTransitionPhase.PreUnload
            : GameSceneTransitionPhase.Loading;
    }

    /// <summary>
    /// Resolves whether a ready target enters its authored black hold or begins reveal immediately.
    /// </summary>
    /// <param name="postLoadReadyExtraSeconds">Configured extra opaque hold duration.</param>
    /// <returns>HoldBlack for positive duration; otherwise FadeIn.</returns>
    public static GameSceneTransitionPhase ResolveReadyRevealPhase(float postLoadReadyExtraSeconds)
    {
        return postLoadReadyExtraSeconds > 0f
            ? GameSceneTransitionPhase.HoldBlack
            : GameSceneTransitionPhase.FadeIn;
    }

    /// <summary>
    /// Writes fade state values with the configured fade color.
    /// </summary>
    /// <param name="fadeState">Mutable fade presentation component.</param>
    /// <param name="alpha">Desired overlay alpha.</param>
    /// <param name="visible">True when the overlay should be visible.</param>
    /// <param name="config">Scene manager runtime config.</param>
    public static void SetFade(ref GameSceneFadePresentationState fadeState, float alpha, bool visible, GameSceneManagerConfig config)
    {
        fadeState.Alpha = Mathf.Clamp01(alpha);
        fadeState.Visible = visible ? (byte)1 : (byte)0;
        fadeState.Color = config.FadeColor;
        fadeState.Mode = config.FadeMode;
        fadeState.WipeDirection = config.FadeWipeDirection;
        fadeState.Operation = GameUiPaintRevealOperation.Deposit;
        fadeState.Easing = config.FadeEasing;
        fadeState.DirectionalEdgeSoftness = config.FadeDirectionalEdgeSoftness;
        fadeState.DirectionalNoiseStrength = config.FadeDirectionalNoiseStrength;
        fadeState.DirectionalNoiseScale = config.FadeDirectionalNoiseScale;
        fadeState.PaintEdgeSoftness = config.FadePaintEdgeSoftness;
        fadeState.PaintNoiseStrength = config.FadePaintNoiseStrength;
        fadeState.PaintNoiseScale = config.FadePaintNoiseScale;
        fadeState.PaintBristleStrength = config.FadePaintBristleStrength;
        fadeState.PaintBristleScale = config.FadePaintBristleScale;

        if (fadeState.Alpha < 0.9999f || !visible)
            fadeState.OpaquePresented = 0;
    }
    #endregion

    #region Matching
    /// <summary>
    /// Matches a scene definition against one loaded Unity scene.
    /// </summary>
    /// <param name="sceneDefinition">Scene definition to inspect.</param>
    /// <param name="scene">Loaded Unity scene.</param>
    /// <returns>True when path or name matches.</returns>
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
    /// </summary>
    /// <param name="managerCount">Current number of manager entities.</param>
    /// <param name="alreadyLogged">True when the current invalid count has already been reported.</param>
    /// <returns>True after an invalid manager count has been handled.</returns>
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
