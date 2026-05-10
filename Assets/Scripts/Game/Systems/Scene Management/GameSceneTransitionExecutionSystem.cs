using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Executes queued scene transition requests through Unity's managed scene loading API.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(GameSceneTransitionTriggerSystem))]
public partial class GameSceneTransitionExecutionSystem : SystemBase
{
    #region Constants
    private const int MinimumReadyWarmupFrames = 3;
    private const float MinimumReadyWarmupSeconds = 0.05f;
    private const float MaximumFadeStepSeconds = 1f / 30f;
    #endregion

    #region Fields
    private EntityQuery managerQuery;
    private GameSceneSceneOperationState activeOperation;
    private GameSceneDefinitionElement bootstrapScene;
    private GameSceneDefinitionElement sourceScene;
    private GameSceneDefinitionElement sourceCompanionScene;
    private GameSceneDefinitionElement targetScene;
    private GameSceneDefinitionElement targetCompanionScene;
    private FixedString64Bytes sourceSceneId;
    private FixedString64Bytes targetSceneId;
    private GameSceneTransitionPhase activePhase;
    private float phaseTimer;
    private float fadeOutSeconds;
    private float postLoadReadyExtraSeconds;
    private float fadeInSeconds;
    private float previousTimeScale = 1f;
    private readonly List<GameSceneDefinitionElement> persistentPlayerPreLoadUnloadScenes = new List<GameSceneDefinitionElement>(2);
    private readonly List<GameSceneDefinitionElement> persistentPlayerLoadScenes = new List<GameSceneDefinitionElement>(2);
    private readonly List<GameSceneDefinitionElement> persistentPlayerPostLoadUnloadScenes = new List<GameSceneDefinitionElement>(2);
    private int persistentPlayerPreLoadUnloadIndex;
    private int persistentPlayerLoadIndex;
    private int persistentPlayerPostLoadUnloadIndex;
    private int readinessWarmupFrames;
    private float readinessWarmupSeconds;
    private bool hasSourceScene;
    private bool hasSourceCompanionScene;
    private bool hasBootstrapScene;
    private bool hasTargetCompanionScene;
    private bool reloadActiveScene;
    private bool targetSceneLoaded;
    private bool targetCompanionSceneLoaded;
    private bool sourceSceneUnloadComplete;
    private bool sourceCompanionSceneUnloadComplete;
    private bool timeScaleChanged;
    private bool loggedManagerCountWarning;
    private bool preLoadRuntimeCleanupComplete;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the manager singleton query required by transition execution.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneManagerConfig),
                                      typeof(GameSceneTransitionState),
                                      typeof(GameSceneFadePresentationState),
                                      typeof(GameSceneDefinitionElement),
                                      typeof(GameSceneTransitionElement),
                                      typeof(GameSceneTransitionRequest));
    }

    /// <summary>
    /// Restores time scale if the system is destroyed during a transition.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnDestroy()
    {
        GameSceneTransitionTimeScaleUtility.Restore(ref timeScaleChanged, previousTimeScale);
    }

    /// <summary>
    /// Starts pending transitions or advances the active asynchronous scene operation.
    /// /params None.
    /// /returns None.
    /// </summary>
    protected override void OnUpdate()
    {
        int managerCount = managerQuery.CalculateEntityCount();

        if (managerCount != 1)
        {
            LogManagerCountWarning(managerCount);
            return;
        }

        loggedManagerCountWarning = false;
        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneManagerConfig config = EntityManager.GetComponentData<GameSceneManagerConfig>(managerEntity);
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameSceneFadePresentationState fadeState = EntityManager.GetComponentData<GameSceneFadePresentationState>(managerEntity);
        DynamicBuffer<GameSceneDefinitionElement> scenes = EntityManager.GetBuffer<GameSceneDefinitionElement>(managerEntity);
        DynamicBuffer<GameSceneTransitionElement> transitions = EntityManager.GetBuffer<GameSceneTransitionElement>(managerEntity);
        DynamicBuffer<GameSceneTransitionRequest> requests = EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);

        if (transitionState.IsTransitioning != 0)
        {
            TickActiveTransition(managerEntity, config, ref transitionState, ref fadeState);
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
            return;
        }

        if (transitionState.Initialized == 0)
            InitializeStateFromLoadedScene(config, scenes, ref transitionState);

        if (TryStartInitialTransition(config, scenes, transitions, ref transitionState, ref fadeState))
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
            return;
        }

        if (requests.Length <= 0)
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            return;
        }

        GameSceneTransitionRequest request = requests[0];
        requests.RemoveAt(0);

        if (TryStartTransition(config, scenes, transitions, request, ref transitionState, ref fadeState))
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
        }
    }
    #endregion

    #region Start
    /// <summary>
    /// Initializes active scene tracking from the loaded Unity active scene when possible.
    /// /params config Scene manager runtime config.
    /// /params scenes Runtime scene definitions.
    /// /params transitionState Mutable transition state component.
    /// /returns None.
    /// </summary>
    private static void InitializeStateFromLoadedScene(GameSceneManagerConfig config,
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

            if (GameSceneTransitionExecutionUtility.MatchesUnityScene(sceneDefinition, activeUnityScene))
            {
                transitionState.ActiveSceneId = sceneDefinition.SceneId;
                return;
            }
        }

        if (config.BootstrapSceneId.Length > 0)
            transitionState.ActiveSceneId = config.BootstrapSceneId;
    }

    /// <summary>
    /// Starts the configured initial scene transition after bootstrap when required.
    /// /params config Scene manager runtime config.
    /// /params scenes Runtime scene definitions.
    /// /params transitions Runtime transition definitions.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /returns True when the initial transition started.
    /// </summary>
    private bool TryStartInitialTransition(GameSceneManagerConfig config,
                                           DynamicBuffer<GameSceneDefinitionElement> scenes,
                                           DynamicBuffer<GameSceneTransitionElement> transitions,
                                           ref GameSceneTransitionState transitionState,
                                           ref GameSceneFadePresentationState fadeState)
    {
        if (config.AutoLoadInitialScene == 0)
            return false;

        if (config.InitialSceneId.Length <= 0)
            return false;

        if (transitionState.ActiveSceneId.Equals(config.InitialSceneId))
            return false;

        if (!GameSceneTransitionExecutionUtility.ShouldRunInitialTransition(config, transitionState))
            return false;

        GameSceneTransitionRequest request = new GameSceneTransitionRequest
        {
            RequestType = GameSceneTransitionRequestType.LoadScene,
            TargetSceneId = config.InitialSceneId,
            TransitionId = default
        };
        return TryStartTransition(config, scenes, transitions, request, ref transitionState, ref fadeState);
    }

    /// <summary>
    /// Resolves and starts one transition request.
    /// /params config Scene manager runtime config.
    /// /params scenes Runtime scene definitions.
    /// /params transitions Runtime transition definitions.
    /// /params request Request to start.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /returns True when the transition started.
    /// </summary>
    private bool TryStartTransition(GameSceneManagerConfig config,
                                    DynamicBuffer<GameSceneDefinitionElement> scenes,
                                    DynamicBuffer<GameSceneTransitionElement> transitions,
                                    GameSceneTransitionRequest request,
                                    ref GameSceneTransitionState transitionState,
                                    ref GameSceneFadePresentationState fadeState)
    {
        if (!GameSceneTransitionExecutionUtility.TryResolveTargetScene(config, scenes, transitions, request, transitionState, out GameSceneDefinitionElement resolvedTarget, out GameSceneTransitionElement transition))
            return false;

        bool isRestart = request.RequestType == GameSceneTransitionRequestType.RestartActiveScene;

        if (!isRestart && transitionState.ActiveSceneId.Equals(resolvedTarget.SceneId))
            return false;

        bool startBehindBlack = GameSceneTransitionExecutionUtility.ShouldStartBehindBlack(config, request, transitionState);
        sourceSceneId = transitionState.ActiveSceneId;
        targetSceneId = resolvedTarget.SceneId;
        targetScene = resolvedTarget;
        hasSourceScene = GameSceneLoadBackendUtility.TryFindScene(scenes, sourceSceneId, out sourceScene);
        hasSourceCompanionScene = hasSourceScene &&
                                  GameSceneLoadBackendUtility.TryFindCompanionScene(scenes, sourceScene, out sourceCompanionScene);
        hasBootstrapScene = GameSceneLoadBackendUtility.TryFindScene(scenes, config.BootstrapSceneId, out bootstrapScene);
        hasTargetCompanionScene = GameSceneLoadBackendUtility.TryFindCompanionScene(scenes, targetScene, out targetCompanionScene);
        reloadActiveScene = isRestart || sourceSceneId.Equals(targetSceneId);
        ResetOperationProgress();
        GameScenePersistentPlayerSceneUtility.CollectOperations(scenes,
                                                                targetScene,
                                                                isRestart && GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene),
                                                                persistentPlayerPreLoadUnloadScenes,
                                                                persistentPlayerLoadScenes,
                                                                persistentPlayerPostLoadUnloadScenes);
        ResolveFadeTimings(config, transition);
        GameSceneTransitionTimeScaleUtility.Begin(config, ref timeScaleChanged, ref previousTimeScale);
        transitionState.SourceSceneId = sourceSceneId;
        transitionState.TargetSceneId = targetSceneId;
        transitionState.IsTransitioning = 1;

        if (config.LogTransitions != 0)
            Debug.Log("[GameSceneManager] Transition started: " + sourceSceneId.ToString() + " -> " + targetSceneId.ToString() + ".");

        if (startBehindBlack)
        {
            GameSceneTransitionExecutionUtility.SetFade(ref fadeState, 1f, true, config);
            BeginPhase(GameSceneTransitionPhase.Loading, ref transitionState, ref fadeState, config);
            return true;
        }

        BeginPhase(GameSceneTransitionPhase.FadeOut, ref transitionState, ref fadeState, config);

        if (fadeOutSeconds <= 0f)
            AdvanceAfterFadeOut(ref transitionState, ref fadeState, config);

        return true;
    }
    #endregion

    #region Tick
    /// <summary>
    /// Advances the active transition state machine.
    /// /params managerEntity Scene manager singleton entity.
    /// /params config Scene manager runtime config.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /returns None.
    /// </summary>
    private void TickActiveTransition(Entity managerEntity,
                                      GameSceneManagerConfig config,
                                      ref GameSceneTransitionState transitionState,
                                      ref GameSceneFadePresentationState fadeState)
    {
        float deltaTime = ResolveFadeStepDeltaTime(UnityEngine.Time.unscaledDeltaTime);

        switch (activePhase)
        {
            case GameSceneTransitionPhase.FadeOut:
                TickFadeOut(ref transitionState, ref fadeState, config, deltaTime);
                break;
            case GameSceneTransitionPhase.PreUnload:
                TickPreUnload(ref transitionState, ref fadeState, config);
                break;
            case GameSceneTransitionPhase.Loading:
                TickLoading(ref transitionState, ref fadeState, config);
                break;
            case GameSceneTransitionPhase.PostUnload:
                TickPostUnload(ref transitionState, ref fadeState, config);
                break;
            case GameSceneTransitionPhase.HoldBlack:
                TickHoldBlack(ref transitionState, ref fadeState, config, deltaTime);
                break;
            case GameSceneTransitionPhase.FadeIn:
                TickFadeIn(managerEntity, ref transitionState, ref fadeState, deltaTime);
                break;
            default:
                CompleteTransition(managerEntity, ref transitionState, ref fadeState);
                break;
        }
    }

    /// <summary>
    /// Advances the fade-out phase until the overlay is fully black.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /params deltaTime Unscaled frame delta time.
    /// /returns None.
    /// </summary>
    private void TickFadeOut(ref GameSceneTransitionState transitionState,
                             ref GameSceneFadePresentationState fadeState,
                             GameSceneManagerConfig config,
                             float deltaTime)
    {
        phaseTimer += deltaTime;
        float alpha = fadeOutSeconds > 0f ? Mathf.Clamp01(phaseTimer / fadeOutSeconds) : 1f;
        GameSceneTransitionExecutionUtility.SetFade(ref fadeState, alpha, true, config);

        if (phaseTimer < fadeOutSeconds)
            return;

        GameSceneTransitionExecutionUtility.SetFade(ref fadeState, 1f, true, config);
        AdvanceAfterFadeOut(ref transitionState, ref fadeState, config);
    }

    /// <summary>
    /// Unloads the active scene before reload transitions load the new instance.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    private void TickPreUnload(ref GameSceneTransitionState transitionState,
                               ref GameSceneFadePresentationState fadeState,
                               GameSceneManagerConfig config)
    {
        if (GameSceneTransitionSceneOperationUtility.TickUnloadStep(sourceScene,
                                                                    hasBootstrapScene,
                                                                    bootstrapScene,
                                                                    targetScene,
                                                                    config,
                                                                    ref activeOperation,
                                                                    ref sourceSceneUnloadComplete))
            return;

        if (hasSourceCompanionScene &&
            GameSceneTransitionSceneOperationUtility.TickUnloadStep(sourceCompanionScene,
                                                                   hasBootstrapScene,
                                                                   bootstrapScene,
                                                                   targetScene,
                                                                   config,
                                                                   ref activeOperation,
                                                                   ref sourceCompanionSceneUnloadComplete))
            return;

        BeginPhase(GameSceneTransitionPhase.Loading, ref transitionState, ref fadeState, config);
    }

    /// <summary>
    /// Loads the target scene additively and activates it when the asynchronous operation completes.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    private void TickLoading(ref GameSceneTransitionState transitionState,
                             ref GameSceneFadePresentationState fadeState,
                             GameSceneManagerConfig config)
    {
        if (GameScenePersistentPlayerSceneUtility.TickUnloadSteps(persistentPlayerPreLoadUnloadScenes, ref persistentPlayerPreLoadUnloadIndex))
            return;

        RunPreLoadRuntimeCleanupIfNeeded();

        if (GameScenePersistentPlayerSceneUtility.TickLoadSteps(persistentPlayerLoadScenes, ref persistentPlayerLoadIndex))
            return;

        if (!targetSceneLoaded &&
            GameSceneTransitionSceneOperationUtility.TickLoadStep(targetScene,
                                                                 config,
                                                                 reloadActiveScene,
                                                                 true,
                                                                 ref activeOperation,
                                                                 ref targetSceneLoaded))
            return;

        if (hasTargetCompanionScene &&
            !targetCompanionSceneLoaded &&
            GameSceneTransitionSceneOperationUtility.TickLoadStep(targetCompanionScene,
                                                                 config,
                                                                 reloadActiveScene,
                                                                 false,
                                                                 ref activeOperation,
                                                                 ref targetCompanionSceneLoaded))
            return;

        bool shouldUnloadSourceScene = GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceAfterLoad(hasSourceScene,
                                                                                                        reloadActiveScene,
                                                                                                        sourceSceneId,
                                                                                                        targetSceneId,
                                                                                                        sourceScene);
        bool shouldUnloadSourceCompanionScene = GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceCompanionAfterLoad(hasSourceCompanionScene,
                                                                                                                          reloadActiveScene,
                                                                                                                          hasTargetCompanionScene,
                                                                                                                          sourceCompanionScene,
                                                                                                                          targetCompanionScene);

        if (GameSceneTransitionUnloadPolicyUtility.ShouldRunPostUnload(shouldUnloadSourceScene,
                                                                       shouldUnloadSourceCompanionScene,
                                                                       persistentPlayerPostLoadUnloadScenes.Count))
        {
            BeginPhase(GameSceneTransitionPhase.PostUnload, ref transitionState, ref fadeState, config);
            return;
        }

        if (!TryCompleteReadinessWarmup())
            return;

        BeginHoldOrFadeIn(ref transitionState, ref fadeState, config);
    }

    /// <summary>
    /// Unloads the previous non-persistent scene after the target scene is active.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    private void TickPostUnload(ref GameSceneTransitionState transitionState,
                                ref GameSceneFadePresentationState fadeState,
                                GameSceneManagerConfig config)
    {
        if (GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceAfterLoad(hasSourceScene,
                                                                              reloadActiveScene,
                                                                              sourceSceneId,
                                                                              targetSceneId,
                                                                              sourceScene) &&
            GameSceneTransitionSceneOperationUtility.TickUnloadStep(sourceScene,
                                                                   hasBootstrapScene,
                                                                   bootstrapScene,
                                                                   targetScene,
                                                                   config,
                                                                   ref activeOperation,
                                                                   ref sourceSceneUnloadComplete))
            return;

        if (GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceCompanionAfterLoad(hasSourceCompanionScene,
                                                                                      reloadActiveScene,
                                                                                      hasTargetCompanionScene,
                                                                                      sourceCompanionScene,
                                                                                      targetCompanionScene) &&
            GameSceneTransitionSceneOperationUtility.TickUnloadStep(sourceCompanionScene,
                                                                   hasBootstrapScene,
                                                                   bootstrapScene,
                                                                   targetScene,
                                                                   config,
                                                                   ref activeOperation,
                                                                   ref sourceCompanionSceneUnloadComplete))
            return;

        if (GameScenePersistentPlayerSceneUtility.TickUnloadSteps(persistentPlayerPostLoadUnloadScenes, ref persistentPlayerPostLoadUnloadIndex))
            return;

        if (!TryCompleteReadinessWarmup())
            return;

        BeginHoldOrFadeIn(ref transitionState, ref fadeState, config);
    }

    /// <summary>
    /// Holds the fade overlay fully opaque for the configured post-readiness bonus before fade-in.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /params deltaTime Unscaled frame delta time.
    /// /returns None.
    /// </summary>
    private void TickHoldBlack(ref GameSceneTransitionState transitionState,
                               ref GameSceneFadePresentationState fadeState,
                               GameSceneManagerConfig config,
                               float deltaTime)
    {
        GameSceneTransitionExecutionUtility.SetFade(ref fadeState, 1f, true, config);
        phaseTimer += deltaTime;

        if (phaseTimer < postLoadReadyExtraSeconds)
            return;

        BeginPhase(GameSceneTransitionPhase.FadeIn, ref transitionState, ref fadeState, config);

        if (fadeInSeconds <= 0f)
            CompleteTransition(Entity.Null, ref transitionState, ref fadeState);
    }

    /// <summary>
    /// Advances the fade-in phase and completes the transition at transparent alpha.
    /// /params managerEntity Scene manager singleton entity.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params deltaTime Unscaled frame delta time.
    /// /returns None.
    /// </summary>
    private void TickFadeIn(Entity managerEntity,
                            ref GameSceneTransitionState transitionState,
                            ref GameSceneFadePresentationState fadeState,
                            float deltaTime)
    {
        phaseTimer += deltaTime;
        float alpha = fadeInSeconds > 0f ? 1f - Mathf.Clamp01(phaseTimer / fadeInSeconds) : 0f;
        fadeState.Alpha = alpha;
        fadeState.Visible = alpha > 0.001f ? (byte)1 : (byte)0;

        if (phaseTimer < fadeInSeconds)
            return;

        CompleteTransition(managerEntity, ref transitionState, ref fadeState);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves active transition fade timings from transition override or preset defaults.
    /// /params config Scene manager runtime config.
    /// /params transition Transition override data.
    /// /returns None.
    /// </summary>
    private void ResolveFadeTimings(GameSceneManagerConfig config, GameSceneTransitionElement transition)
    {
        if (transition.OverrideFadeSettings != 0)
        {
            fadeOutSeconds = Mathf.Max(0f, transition.FadeOutSeconds);
            postLoadReadyExtraSeconds = Mathf.Max(0f, transition.PostLoadReadyExtraSeconds);
            fadeInSeconds = Mathf.Max(0f, transition.FadeInSeconds);
            return;
        }

        fadeOutSeconds = Mathf.Max(0f, config.FadeOutSeconds);
        postLoadReadyExtraSeconds = Mathf.Max(0f, config.PostLoadReadyExtraSeconds);
        fadeInSeconds = Mathf.Max(0f, config.FadeInSeconds);
    }

    /// <summary>
    /// Clears per-transition load and unload progress flags before a new transition starts.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ResetOperationProgress()
    {
        targetSceneLoaded = false;
        targetCompanionSceneLoaded = false;
        sourceSceneUnloadComplete = false;
        sourceCompanionSceneUnloadComplete = false;
        persistentPlayerPreLoadUnloadIndex = 0;
        persistentPlayerLoadIndex = 0;
        persistentPlayerPostLoadUnloadIndex = 0;
        preLoadRuntimeCleanupComplete = false;
        ResetReadinessWarmup();
    }

    /// <summary>
    /// Clears transient gameplay entities that are not owned by scene streaming before a restart loads the new instance.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void RunPreLoadRuntimeCleanupIfNeeded()
    {
        if (preLoadRuntimeCleanupComplete)
            return;

        preLoadRuntimeCleanupComplete = true;

        if (!reloadActiveScene)
            return;

        if (!GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene))
            return;

        GameSceneTransitionGameplayRuntimeCleanupUtility.DestroyTransientGameplayRuntimeEntities(EntityManager);
    }

    /// <summary>
    /// Waits until loaded scenes, gameplay runtime and a short hidden warm-up have completed before fade-in.
    /// /params None.
    /// /returns True when the transition can reveal the target scene.
    /// </summary>
    private bool TryCompleteReadinessWarmup()
    {
        EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

        if (!GameSceneTransitionReadinessUtility.AreTransitionScenesReady(targetScene,
                                                                         hasTargetCompanionScene,
                                                                         targetCompanionScene,
                                                                         persistentPlayerLoadScenes))
        {
            ResetReadinessWarmup();
            return false;
        }

        readinessWarmupFrames++;
        readinessWarmupSeconds += Mathf.Max(0f, UnityEngine.Time.unscaledDeltaTime);

        if (readinessWarmupFrames < MinimumReadyWarmupFrames)
            return false;

        return readinessWarmupSeconds >= MinimumReadyWarmupSeconds;
    }

    /// <summary>
    /// Clears hidden warm-up progress when a new transition starts or readiness drops.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ResetReadinessWarmup()
    {
        readinessWarmupFrames = 0;
        readinessWarmupSeconds = 0f;
    }

    /// <summary>
    /// Caps visual transition steps so a loading hitch cannot consume an entire fade-in in one frame.
    /// /params unscaledDeltaTime Raw Unity unscaled frame delta.
    /// /returns Clamped presentation delta for fade phases.
    /// </summary>
    private static float ResolveFadeStepDeltaTime(float unscaledDeltaTime)
    {
        return Mathf.Min(Mathf.Max(0f, unscaledDeltaTime), MaximumFadeStepSeconds);
    }

    /// <summary>
    /// Moves from fade out to source unload or target load depending on reload policy.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    private void AdvanceAfterFadeOut(ref GameSceneTransitionState transitionState,
                                     ref GameSceneFadePresentationState fadeState,
                                     GameSceneManagerConfig config)
    {
        if (reloadActiveScene && (hasSourceScene || hasSourceCompanionScene))
        {
            BeginPhase(GameSceneTransitionPhase.PreUnload, ref transitionState, ref fadeState, config);
            return;
        }

        BeginPhase(GameSceneTransitionPhase.Loading, ref transitionState, ref fadeState, config);
    }

    /// <summary>
    /// Starts the hold-black phase when configured, otherwise starts fade-in.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    private void BeginHoldOrFadeIn(ref GameSceneTransitionState transitionState,
                                   ref GameSceneFadePresentationState fadeState,
                                   GameSceneManagerConfig config)
    {
        if (postLoadReadyExtraSeconds > 0f)
        {
            BeginPhase(GameSceneTransitionPhase.HoldBlack, ref transitionState, ref fadeState, config);
            return;
        }

        BeginPhase(GameSceneTransitionPhase.FadeIn, ref transitionState, ref fadeState, config);

        if (fadeInSeconds <= 0f)
            CompleteTransition(Entity.Null, ref transitionState, ref fadeState);
    }

    /// <summary>
    /// Updates managed and ECS phase fields and resets the phase timer.
    /// /params phase New transition phase.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /params config Scene manager runtime config.
    /// /returns None.
    /// </summary>
    private void BeginPhase(GameSceneTransitionPhase phase,
                            ref GameSceneTransitionState transitionState,
                            ref GameSceneFadePresentationState fadeState,
                            GameSceneManagerConfig config)
    {
        activePhase = phase;
        phaseTimer = 0f;
        transitionState.Phase = phase;

        if (phase == GameSceneTransitionPhase.FadeIn)
            GameSceneTransitionTimeScaleUtility.Restore(ref timeScaleChanged, previousTimeScale);

        if (phase != GameSceneTransitionPhase.FadeIn)
            GameSceneTransitionExecutionUtility.SetFade(ref fadeState, fadeState.Alpha, true, config);
    }

    /// <summary>
    /// Completes the active transition and restores idle state.
    /// /params managerEntity Scene manager singleton entity, or Entity.Null when completion happens before entity writeback.
    /// /params transitionState Mutable transition state component.
    /// /params fadeState Mutable fade presentation component.
    /// /returns None.
    /// </summary>
    private void CompleteTransition(Entity managerEntity,
                                    ref GameSceneTransitionState transitionState,
                                    ref GameSceneFadePresentationState fadeState)
    {
        activeOperation.Clear();
        activePhase = GameSceneTransitionPhase.Idle;
        transitionState.ActiveSceneId = targetSceneId;
        transitionState.SourceSceneId = default;
        transitionState.TargetSceneId = default;
        transitionState.Phase = GameSceneTransitionPhase.Idle;
        transitionState.IsTransitioning = 0;
        transitionState.Initialized = 1;
        fadeState.Alpha = 0f;
        fadeState.Visible = 0;
        GameSceneTransitionTimeScaleUtility.Restore(ref timeScaleChanged, previousTimeScale);

        if (managerEntity != Entity.Null && EntityManager.Exists(managerEntity))
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
        }
    }

    /// <summary>
    /// Logs singleton count problems once until the manager count becomes valid again.
    /// /params managerCount Current number of manager entities.
    /// /returns None.
    /// </summary>
    private void LogManagerCountWarning(int managerCount)
    {
        if (loggedManagerCountWarning)
            return;

        if (managerCount > 1)
            Debug.LogWarning("[GameSceneManager] Expected one scene manager singleton, found " + managerCount + ".");

        loggedManagerCountWarning = true;
    }
    #endregion

    #endregion
}
