using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
/// <summary>Executes queued scene transition requests through Unity's managed scene loading API.</summary>
[UpdateInGroup(typeof(GameSceneManagementSystemGroup))]
[UpdateAfter(typeof(GameSceneTransitionTriggerSystem))]
public partial class GameSceneTransitionExecutionSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    private EntityQuery physicsStepStateQuery;
    private GameSceneSceneOperationState activeOperation;
    private GameSceneDefinitionElement bootstrapScene, sourceScene, sourceCompanionScene, targetScene, targetCompanionScene;
    private FixedString64Bytes sourceSceneId, targetSceneId;
    private GameSceneTransitionPhase activePhase;
    private GameSceneTransitionPurpose activePurpose;
    private float phaseTimer, fadeOutSeconds, postLoadReadyExtraSeconds, fadeInSeconds;
    private float previousTimeScale = 1f;
    private readonly List<GameSceneDefinitionElement> persistentPlayerPreLoadUnloadScenes = new List<GameSceneDefinitionElement>(2);
    private readonly List<GameSceneDefinitionElement> persistentPlayerLoadScenes = new List<GameSceneDefinitionElement>(2);
    private readonly List<GameSceneDefinitionElement> persistentPlayerPostLoadUnloadScenes = new List<GameSceneDefinitionElement>(2);
    private int persistentPlayerPreLoadUnloadIndex, persistentPlayerLoadIndex, persistentPlayerPostLoadUnloadIndex;
    private int loadingProgressTotalSteps = 1;
    private int readinessWarmupFrames;
    private ulong requiredPhysicsStepVersion;
    private float readinessWarmupSeconds;
    private bool hasSourceScene, hasSourceCompanionScene, hasBootstrapScene, hasTargetCompanionScene, reloadActiveScene;
    private bool unloadSourceBeforeLoad, reloadTargetCompanion;
    private bool targetSceneLoaded, targetCompanionSceneLoaded, sourceSceneUnloadComplete, sourceCompanionSceneUnloadComplete;
    private bool timeScaleChanged, loggedManagerCountWarning, preLoadRuntimeCleanupComplete, suppressLoadingProgress;
    private bool transactionalRoomStreaming, singleSlotRoomStreaming, transactionalSourceOwned;
    private bool physicsReadinessRequested;
    #endregion
    #region Methods
    #region Lifecycle
    /// <summary>Creates the manager singleton query required by transition execution.</summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneManagerConfig),
                                      typeof(GameSceneTransitionState),
                                      typeof(GameSceneFadePresentationState),
                                      typeof(GameSceneLoadingProgressPresentationState),
                                      typeof(GameSceneDefinitionElement),
                                      typeof(GameSceneTransitionElement),
                                      typeof(GameSceneTransitionRequest));
        physicsStepStateQuery = GetEntityQuery(ComponentType.ReadOnly<GameSceneTransitionPhysicsStepState>());
    }

    /// <summary>Restores time scale if the system is destroyed during a transition.</summary>
    protected override void OnDestroy()
    {
        GameSceneTransitionTimeScaleUtility.Restore(ref timeScaleChanged, previousTimeScale);
    }

    /// <summary>Starts pending transitions or advances the active asynchronous scene operation.</summary>
    protected override void OnUpdate()
    {
        int managerCount = managerQuery.CalculateEntityCount();

        if (managerCount != 1)
        {
            loggedManagerCountWarning = GameSceneTransitionExecutionUtility.LogManagerCountWarning(managerCount, loggedManagerCountWarning);
            return;
        }

        loggedManagerCountWarning = false;
        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneManagerConfig config = EntityManager.GetComponentData<GameSceneManagerConfig>(managerEntity);
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameSceneFadePresentationState fadeState = EntityManager.GetComponentData<GameSceneFadePresentationState>(managerEntity);
        GameSceneLoadingProgressPresentationState loadingProgressState = EntityManager.GetComponentData<GameSceneLoadingProgressPresentationState>(managerEntity);
        DynamicBuffer<GameSceneDefinitionElement> scenes = EntityManager.GetBuffer<GameSceneDefinitionElement>(managerEntity);
        DynamicBuffer<GameSceneTransitionElement> transitions = EntityManager.GetBuffer<GameSceneTransitionElement>(managerEntity);
        DynamicBuffer<GameSceneTransitionRequest> requests = EntityManager.GetBuffer<GameSceneTransitionRequest>(managerEntity);

        if (transitionState.IsTransitioning != 0)
        {
            TickActiveTransition(managerEntity, config, ref transitionState, ref fadeState, ref loadingProgressState);
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
            EntityManager.SetComponentData(managerEntity, loadingProgressState);
            return;
        }

        if (transitionState.Initialized == 0)
            GameSceneTransitionExecutionUtility.InitializeStateFromLoadedScene(config, scenes, ref transitionState);

        if (TryStartInitialTransition(managerEntity, config, scenes, transitions, ref transitionState, ref fadeState, ref loadingProgressState))
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
            EntityManager.SetComponentData(managerEntity, loadingProgressState);
            return;
        }

        if (requests.Length <= 0)
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            return;
        }

        GameSceneTransitionRequest request = requests[0];
        requests.RemoveAt(0);

        if (TryStartTransition(managerEntity, config, scenes, transitions, request, ref transitionState, ref fadeState, ref loadingProgressState))
        {
            EntityManager.SetComponentData(managerEntity, transitionState);
            EntityManager.SetComponentData(managerEntity, fadeState);
            EntityManager.SetComponentData(managerEntity, loadingProgressState);
        }
    }
    #endregion

    #region Start
    /// <summary>Starts the configured initial scene transition after bootstrap when required.</summary>
    /// <param name="managerEntity">Scene manager entity supplying purpose-specific presentation policy.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    /// <param name="scenes">Canonical scene definitions used to resolve the initial target.</param>
    /// <param name="transitions">Authored transition definitions used to resolve optional overrides.</param>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <returns>True when the initial transition started.</returns>
    private bool TryStartInitialTransition(Entity managerEntity,
                                           GameSceneManagerConfig config,
                                           DynamicBuffer<GameSceneDefinitionElement> scenes,
                                           DynamicBuffer<GameSceneTransitionElement> transitions,
                                           ref GameSceneTransitionState transitionState,
                                           ref GameSceneFadePresentationState fadeState,
                                           ref GameSceneLoadingProgressPresentationState loadingProgressState)
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
        return TryStartTransition(managerEntity, config, scenes, transitions, request, ref transitionState, ref fadeState, ref loadingProgressState);
    }

    /// <summary>Resolves and starts one transition request.</summary>
    /// <param name="managerEntity">Scene manager entity supplying purpose-specific presentation policy.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    /// <param name="scenes">Canonical scene definitions used to resolve the requested target.</param>
    /// <param name="transitions">Authored transition definitions used to resolve optional overrides.</param>
    /// <param name="request">Transition request to start.</param>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <returns>True when the transition started.</returns>
    private bool TryStartTransition(Entity managerEntity,
                                    GameSceneManagerConfig config,
                                    DynamicBuffer<GameSceneDefinitionElement> scenes,
                                    DynamicBuffer<GameSceneTransitionElement> transitions,
                                    GameSceneTransitionRequest request,
                                    ref GameSceneTransitionState transitionState,
                                    ref GameSceneFadePresentationState fadeState,
                                    ref GameSceneLoadingProgressPresentationState loadingProgressState)
    {
        if (!GameSceneTransitionExecutionUtility.TryResolveTargetScene(config, scenes, transitions, request, transitionState, out GameSceneDefinitionElement resolvedTarget, out GameSceneTransitionElement transition))
            return false;

        bool isRestart = request.RequestType == GameSceneTransitionRequestType.RestartActiveScene;
        bool reloadPersistentPlayer = isRestart || request.ReloadPersistentPlayer != 0;

        if (!isRestart &&
            transitionState.ActiveSceneId.Equals(resolvedTarget.SceneId) &&
            !GameSceneTransitionPurposeUtility.AllowsSameSceneReload(request.Purpose))
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
        activePurpose = request.Purpose;
        transactionalRoomStreaming = GameProceduralRoomTransitionTransactionUtility.UsesTransactionalStreaming(EntityManager,
                                                                                                                  managerEntity,
                                                                                                                  activePurpose);
        singleSlotRoomStreaming = GameProceduralRoomTransitionTransactionUtility.UsesSingleSlotStreaming(EntityManager,
                                                                                                           managerEntity,
                                                                                                           activePurpose);
        suppressLoadingProgress = GameSceneTransitionPurposeUtility.ShouldHideDetailedLoadingProgress(EntityManager,
                                                                                                       managerEntity,
                                                                                                       activePurpose);
        unloadSourceBeforeLoad = (!transactionalRoomStreaming || singleSlotRoomStreaming) &&
                                 (reloadActiveScene || GameSceneTransitionPurposeUtility.IsProcedural(activePurpose));
        reloadTargetCompanion = reloadPersistentPlayer;
        ResetOperationProgress();
        GameScenePersistentPlayerSceneUtility.CollectOperations(scenes,
                                                                targetScene,
                                                                reloadPersistentPlayer && GameScenePersistentPlayerSceneUtility.IsGameplayLikeScene(targetScene),
                                                                persistentPlayerPreLoadUnloadScenes,
                                                                persistentPlayerLoadScenes,
                                                                persistentPlayerPostLoadUnloadScenes);
        loadingProgressTotalSteps = suppressLoadingProgress
            ? 1
            : GameSceneLoadingProgressRuntimeUtility.CountTransitionSteps(reloadActiveScene,
                                                                          hasSourceScene,
                                                                          sourceScene,
                                                                          hasSourceCompanionScene,
                                                                          sourceCompanionScene,
                                                                          sourceSceneId,
                                                                          targetSceneId,
                                                                          hasTargetCompanionScene,
                                                                          targetCompanionScene,
                                                                          persistentPlayerPreLoadUnloadScenes,
                                                                          persistentPlayerLoadScenes,
                                                                          persistentPlayerPostLoadUnloadScenes);
        GameSceneTransitionFadeTimings fadeTimings = GameSceneTransitionExecutionTimingUtility.ResolveFadeTimings(config, transition);
        fadeOutSeconds = fadeTimings.FadeOutSeconds;
        postLoadReadyExtraSeconds = fadeTimings.PostLoadReadyExtraSeconds;
        fadeInSeconds = fadeTimings.FadeInSeconds;
        GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
        GameSceneTransitionTimeScaleUtility.Begin(config,
                                                  activePurpose,
                                                  ref timeScaleChanged,
                                                  ref previousTimeScale);
        transitionState.SourceSceneId = sourceSceneId;
        transitionState.TargetSceneId = targetSceneId;
        transitionState.Purpose = activePurpose;
        transitionState.IsTransitioning = 1;

        if (startBehindBlack)
        {
            GameSceneTransitionExecutionUtility.SetFade(ref fadeState, 1f, true, config);
            AdvanceAfterFadeOut(ref transitionState, ref fadeState, ref loadingProgressState, config);
            return true;
        }

        BeginPhase(GameSceneTransitionPhase.FadeOut, ref transitionState, ref fadeState, ref loadingProgressState, config);

        if (fadeOutSeconds <= 0f)
            AdvanceAfterFadeOut(ref transitionState, ref fadeState, ref loadingProgressState, config);

        return true;
    }
    #endregion

    #region Tick
    /// <summary>Advances the active transition state machine.</summary>
    /// <param name="managerEntity">Scene manager entity that owns the transition state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    private void TickActiveTransition(Entity managerEntity,
                                      GameSceneManagerConfig config,
                                      ref GameSceneTransitionState transitionState,
                                      ref GameSceneFadePresentationState fadeState,
                                      ref GameSceneLoadingProgressPresentationState loadingProgressState)
    {
        float deltaTime = GameSceneTransitionExecutionTimingUtility.ResolveFadeStepDeltaTime(UnityEngine.Time.unscaledDeltaTime);

        switch (activePhase)
        {
            case GameSceneTransitionPhase.FadeOut:
                if (GameSceneTransitionFadePhaseUtility.TickFadeOut(ref phaseTimer,
                                                                    fadeOutSeconds,
                                                                    deltaTime,
                                                                    ref fadeState,
                                                                    config))
                    AdvanceAfterFadeOut(ref transitionState, ref fadeState, ref loadingProgressState, config);

                break;
            case GameSceneTransitionPhase.PreUnload:
                TickPreUnload(ref transitionState, ref fadeState, ref loadingProgressState, config);
                break;
            case GameSceneTransitionPhase.Loading:
                TickLoading(ref transitionState, ref fadeState, ref loadingProgressState, config);
                break;
            case GameSceneTransitionPhase.PostUnload:
                TickPostUnload(ref transitionState, ref fadeState, ref loadingProgressState, config);
                break;
            case GameSceneTransitionPhase.HoldBlack:
                if (!GameSceneTransitionFadePhaseUtility.TickHoldBlack(ref phaseTimer,
                                                                       postLoadReadyExtraSeconds,
                                                                       deltaTime,
                                                                       ref fadeState,
                                                                       ref loadingProgressState,
                                                                       config))
                    break;

                BeginPhase(GameSceneTransitionPhase.FadeIn, ref transitionState, ref fadeState, ref loadingProgressState, config);

                if (fadeInSeconds <= 0f)
                    CompleteTransition(Entity.Null, config, ref transitionState, ref fadeState, ref loadingProgressState);

                break;
            case GameSceneTransitionPhase.FadeIn:
                if (GameSceneTransitionFadePhaseUtility.TickFadeIn(ref phaseTimer,
                                                                   fadeInSeconds,
                                                                   deltaTime,
                                                                   ref fadeState))
                    CompleteTransition(managerEntity, config, ref transitionState, ref fadeState, ref loadingProgressState);

                break;
            default:
                CompleteTransition(managerEntity, config, ref transitionState, ref fadeState, ref loadingProgressState);
                break;
        }

        if (suppressLoadingProgress)
            GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
    }

    /// <summary>Unloads the active scene before reload transitions load the new instance.</summary>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void TickPreUnload(ref GameSceneTransitionState transitionState,
                               ref GameSceneFadePresentationState fadeState,
                               ref GameSceneLoadingProgressPresentationState loadingProgressState,
                               GameSceneManagerConfig config)
    {
        preLoadRuntimeCleanupComplete = GameSceneTransitionExecutionUtility.RunPreLoadRuntimeCleanupIfNeeded(EntityManager,
                                                                                                            preLoadRuntimeCleanupComplete,
                                                                                                            reloadActiveScene,
                                                                                                            hasSourceScene,
                                                                                                            sourceScene,
                                                                                                            targetScene,
                                                                                                            ShouldForceProceduralRuntimeCleanup());

        if (GameSceneTransitionPurposeUtility.ShouldUnloadSourceBeforeLoad(unloadSourceBeforeLoad,
                                                                          hasSourceScene,
                                                                          sourceScene) &&
            GameSceneTransitionSceneOperationUtility.TickUnloadStep(sourceScene,
                                                                    hasBootstrapScene,
                                                                    bootstrapScene,
                                                                    targetScene,
                                                                    config,
                                                                    ref activeOperation,
                                                                    ref sourceSceneUnloadComplete))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Unloading, sourceScene);
            return;
        }

        if (GameSceneTransitionPurposeUtility.ShouldUnloadSourceCompanionBeforeLoad(unloadSourceBeforeLoad,
                                                                                   hasSourceCompanionScene,
                                                                                   hasTargetCompanionScene,
                                                                                   reloadTargetCompanion,
                                                                                   sourceCompanionScene,
                                                                                   targetCompanionScene) &&
            GameSceneTransitionSceneOperationUtility.TickUnloadStep(sourceCompanionScene,
                                                                   hasBootstrapScene,
                                                                   bootstrapScene,
                                                                   targetScene,
                                                                   config,
                                                                   ref activeOperation,
                                                                   ref sourceCompanionSceneUnloadComplete))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Unloading, sourceCompanionScene);
            return;
        }

        BeginPhase(GameSceneTransitionPhase.Loading, ref transitionState, ref fadeState, ref loadingProgressState, config);
    }

    /// <summary>Loads the target scene additively and activates it when the asynchronous operation completes.</summary>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void TickLoading(ref GameSceneTransitionState transitionState,
                             ref GameSceneFadePresentationState fadeState,
                             ref GameSceneLoadingProgressPresentationState loadingProgressState,
                             GameSceneManagerConfig config)
    {
        if (GameScenePersistentPlayerSceneUtility.TickUnloadSteps(persistentPlayerPreLoadUnloadScenes, ref persistentPlayerPreLoadUnloadIndex))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState,
                                        config,
                                        GameSceneLoadingProgressOperationKind.Unloading,
                                        GameSceneTransitionExecutionProgressUtility.ResolveCurrentListScene(persistentPlayerPreLoadUnloadScenes,
                                                                                                           persistentPlayerPreLoadUnloadIndex,
                                                                                                           targetScene));
            return;
        }

        preLoadRuntimeCleanupComplete = GameSceneTransitionExecutionUtility.RunPreLoadRuntimeCleanupIfNeeded(EntityManager,
                                                                                                            preLoadRuntimeCleanupComplete,
                                                                                                            reloadActiveScene,
                                                                                                            hasSourceScene,
                                                                                                            sourceScene,
                                                                                                            targetScene,
                                                                                                            ShouldForceProceduralRuntimeCleanup());

        if (GameScenePersistentPlayerSceneUtility.TickLoadSteps(persistentPlayerLoadScenes, ref persistentPlayerLoadIndex))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState,
                                        config,
                                        GameSceneLoadingProgressOperationKind.Loading,
                                        GameSceneTransitionExecutionProgressUtility.ResolveCurrentListScene(persistentPlayerLoadScenes,
                                                                                                           persistentPlayerLoadIndex,
                                                                                                           targetScene));
            return;
        }

        if (transactionalRoomStreaming &&
            !targetSceneLoaded &&
            !GameProceduralRoomTransitionTransactionUtility.TryCommitPendingTarget(EntityManager,
                                                                                    managerQuery.GetSingletonEntity(),
                                                                                    targetScene,
                                                                                    config.LoadBackend,
                                                                                    out transactionalSourceOwned))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Loading, targetScene);
            return;
        }

        if (transactionalRoomStreaming)
        {
            targetSceneLoaded = true;

            if (transactionalSourceOwned)
                sourceSceneUnloadComplete = true;
        }
        else if (!targetSceneLoaded &&
                 GameSceneTransitionSceneOperationUtility.TickLoadStep(targetScene,
                                                                      config,
                                                                      reloadActiveScene,
                                                                      true,
                                                                      ref activeOperation,
                                                                      ref targetSceneLoaded))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Loading, targetScene);
            return;
        }

        if (hasTargetCompanionScene &&
            !targetCompanionSceneLoaded &&
            GameSceneTransitionSceneOperationUtility.TickLoadStep(targetCompanionScene,
                                                                 config,
                                                                 reloadTargetCompanion,
                                                                 false,
                                                                 ref activeOperation,
                                                                 ref targetCompanionSceneLoaded))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Loading, targetCompanionScene);
            return;
        }

        bool shouldUnloadSourceScene = !sourceSceneUnloadComplete &&
                                       !unloadSourceBeforeLoad &&
                                       GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceAfterLoad(hasSourceScene,
                                                                                                        reloadActiveScene,
                                                                                                        sourceSceneId,
                                                                                                        targetSceneId,
                                                                                                        sourceScene);
        bool shouldUnloadSourceCompanionScene = !sourceCompanionSceneUnloadComplete &&
                                                !unloadSourceBeforeLoad &&
                                                GameSceneTransitionUnloadPolicyUtility.ShouldUnloadSourceCompanionAfterLoad(hasSourceCompanionScene,
                                                                                                                          reloadActiveScene,
                                                                                                                          hasTargetCompanionScene,
                                                                                                                          sourceCompanionScene,
                                                                                                                          targetCompanionScene);

        if (GameSceneTransitionUnloadPolicyUtility.ShouldRunPostUnload(shouldUnloadSourceScene,
                                                                       shouldUnloadSourceCompanionScene,
                                                                       persistentPlayerPostLoadUnloadScenes.Count))
        {
            BeginPhase(GameSceneTransitionPhase.PostUnload, ref transitionState, ref fadeState, ref loadingProgressState, config);
            return;
        }

        if (transactionalRoomStreaming &&
            activePurpose == GameSceneTransitionPurpose.ProceduralRoomTraversal)
        {
            if (!GameProceduralRoomArrivalUtility.TryPreparePendingArrival(EntityManager))
            {
                ApplyCurrentLoadingProgress(ref loadingProgressState,
                                            config,
                                            GameSceneLoadingProgressOperationKind.Readiness,
                                            targetScene);
                return;
            }

            BeginHoldOrFadeIn(ref transitionState, ref fadeState, ref loadingProgressState, config);
            return;
        }

        if (!GameSceneTransitionExecutionTimingUtility.TryCompleteReadinessWarmup(EntityManager,
                                                                                 targetScene,
                                                                                 hasTargetCompanionScene,
                                                                                 targetCompanionScene,
                                                                                 persistentPlayerLoadScenes,
                                                                                 activePurpose,
                                                                                 ref readinessWarmupFrames,
                                                                                 ref readinessWarmupSeconds,
                                                                                 physicsStepStateQuery,
                                                                                 ref timeScaleChanged,
                                                                                 previousTimeScale,
                                                                                 ref physicsReadinessRequested,
                                                                                 ref requiredPhysicsStepVersion))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Readiness, targetScene);
            return;
        }

        BeginHoldOrFadeIn(ref transitionState, ref fadeState, ref loadingProgressState, config);
    }
    #endregion

    #region Cleanup Policy
    /// <summary>
    /// Forces transient room-runtime cleanup for initial loads, compatibility replacement and single-slot traversal.
    /// Dual-slot traversal alone retains both room-owned runtime surfaces until its deferred retirement path completes.
    /// </summary>
    /// <returns>True when the active procedural transition crosses a runtime lifetime boundary.</returns>
    private bool ShouldForceProceduralRuntimeCleanup()
    {
        if (!GameSceneTransitionPurposeUtility.IsProcedural(activePurpose))
            return false;

        if (!transactionalRoomStreaming || singleSlotRoomStreaming)
            return true;

        return activePurpose == GameSceneTransitionPurpose.ProceduralInitialRoom;
    }
    #endregion

    #region Post Unload

    /// <summary>Unloads the previous non-persistent scene after the target scene is active.</summary>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void TickPostUnload(ref GameSceneTransitionState transitionState,
                                ref GameSceneFadePresentationState fadeState,
                                ref GameSceneLoadingProgressPresentationState loadingProgressState,
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
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Unloading, sourceScene);
            return;
        }

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
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Unloading, sourceCompanionScene);
            return;
        }

        if (GameScenePersistentPlayerSceneUtility.TickUnloadSteps(persistentPlayerPostLoadUnloadScenes, ref persistentPlayerPostLoadUnloadIndex))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState,
                                        config,
                                        GameSceneLoadingProgressOperationKind.Unloading,
                                        GameSceneTransitionExecutionProgressUtility.ResolveCurrentListScene(persistentPlayerPostLoadUnloadScenes,
                                                                                                           persistentPlayerPostLoadUnloadIndex,
                                                                                                           targetScene));
            return;
        }

        if (!GameSceneTransitionExecutionTimingUtility.TryCompleteReadinessWarmup(EntityManager,
                                                                                 targetScene,
                                                                                 hasTargetCompanionScene,
                                                                                 targetCompanionScene,
                                                                                 persistentPlayerLoadScenes,
                                                                                 activePurpose,
                                                                                 ref readinessWarmupFrames,
                                                                                 ref readinessWarmupSeconds,
                                                                                 physicsStepStateQuery,
                                                                                 ref timeScaleChanged,
                                                                                 previousTimeScale,
                                                                                 ref physicsReadinessRequested,
                                                                                 ref requiredPhysicsStepVersion))
        {
            ApplyCurrentLoadingProgress(ref loadingProgressState, config, GameSceneLoadingProgressOperationKind.Readiness, targetScene);
            return;
        }

        BeginHoldOrFadeIn(ref transitionState, ref fadeState, ref loadingProgressState, config);
    }

    #endregion
    #region Helpers
    /// <summary>Clears per-transition load and unload progress flags before a new transition starts.</summary>
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
        transactionalSourceOwned = false;
        GameSceneTransitionExecutionTimingUtility.ResetReadiness(ref readinessWarmupFrames,
                                                                 ref readinessWarmupSeconds,
                                                                 ref physicsReadinessRequested,
                                                                 ref requiredPhysicsStepVersion);
    }

    /// <summary>Moves from fade out to required source or companion unload work, otherwise directly to target loading.</summary>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void AdvanceAfterFadeOut(ref GameSceneTransitionState transitionState,
                                     ref GameSceneFadePresentationState fadeState,
                                     ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                     GameSceneManagerConfig config)
    {
        bool unloadReloadedCompanion = reloadTargetCompanion && hasSourceCompanionScene;

        if ((unloadSourceBeforeLoad && (hasSourceScene || hasSourceCompanionScene)) ||
            unloadReloadedCompanion)
        {
            BeginPhase(GameSceneTransitionPhase.PreUnload, ref transitionState, ref fadeState, ref loadingProgressState, config);
            return;
        }

        BeginPhase(GameSceneTransitionPhase.Loading, ref transitionState, ref fadeState, ref loadingProgressState, config);
    }

    /// <summary>Starts the hold-black phase when configured, otherwise starts fade-in.</summary>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void BeginHoldOrFadeIn(ref GameSceneTransitionState transitionState,
                                   ref GameSceneFadePresentationState fadeState,
                                   ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                   GameSceneManagerConfig config)
    {
        if (postLoadReadyExtraSeconds > 0f)
        {
            BeginPhase(GameSceneTransitionPhase.HoldBlack, ref transitionState, ref fadeState, ref loadingProgressState, config);
            return;
        }

        BeginPhase(GameSceneTransitionPhase.FadeIn, ref transitionState, ref fadeState, ref loadingProgressState, config);

        if (fadeInSeconds <= 0f)
            CompleteTransition(Entity.Null, config, ref transitionState, ref fadeState, ref loadingProgressState);
    }

    /// <summary>Applies loading-progress visibility and status when a transition phase starts.</summary>
    /// <param name="phase">Transition phase whose presentation state is being initialized.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation component.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void ApplyLoadingProgressForPhase(GameSceneTransitionPhase phase,
                                              ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                              GameSceneManagerConfig config)
    {
        if (suppressLoadingProgress)
        {
            GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
            return;
        }

        GameSceneTransitionExecutionProgressUtility.ApplyForPhase(phase,
                                                                  ref loadingProgressState,
                                                                  config,
                                                                  BuildLoadingProgressSnapshot());
    }

    /// <summary>Applies aggregate loading progress for the current operation step.</summary>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    /// <param name="operationKind">Operation type used to select the displayed loading status.</param>
    /// <param name="sceneDefinition">Scene definition processed by the current operation.</param>
    private void ApplyCurrentLoadingProgress(ref GameSceneLoadingProgressPresentationState loadingProgressState,
                                             GameSceneManagerConfig config,
                                             GameSceneLoadingProgressOperationKind operationKind,
                                             GameSceneDefinitionElement sceneDefinition)
    {
        if (suppressLoadingProgress)
        {
            GameSceneLoadingProgressRuntimeUtility.Hide(ref loadingProgressState, config);
            return;
        }

        GameSceneTransitionExecutionProgressUtility.ApplyCurrent(ref loadingProgressState,
                                                                 config,
                                                                 operationKind,
                                                                 sceneDefinition,
                                                                 BuildLoadingProgressSnapshot());
    }

    /// <summary>Builds an immutable snapshot of the counters used by loading-progress calculations.</summary>
    /// <returns>Current transition progress snapshot.</returns>
    private GameSceneTransitionProgressSnapshot BuildLoadingProgressSnapshot()
    {
        return new GameSceneTransitionProgressSnapshot(reloadActiveScene, hasSourceScene,
                                                       sourceScene, hasSourceCompanionScene,
                                                       sourceCompanionScene, targetScene,
                                                       hasTargetCompanionScene, targetSceneLoaded,
                                                       targetCompanionSceneLoaded, sourceSceneUnloadComplete,
                                                       sourceCompanionSceneUnloadComplete, persistentPlayerPreLoadUnloadScenes,
                                                       persistentPlayerLoadScenes, persistentPlayerPostLoadUnloadScenes,
                                                       persistentPlayerPreLoadUnloadIndex, persistentPlayerLoadIndex,
                                                       persistentPlayerPostLoadUnloadIndex, loadingProgressTotalSteps,
                                                       activeOperation);
    }

    /// <summary>Updates managed and ECS phase fields and resets the phase timer.</summary>
    /// <param name="phase">New transition phase.</param>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    private void BeginPhase(GameSceneTransitionPhase phase,
                            ref GameSceneTransitionState transitionState,
                            ref GameSceneFadePresentationState fadeState,
                            ref GameSceneLoadingProgressPresentationState loadingProgressState,
                            GameSceneManagerConfig config)
    {
        activePhase = phase;
        phaseTimer = 0f;
        transitionState.Phase = phase;

        if (phase == GameSceneTransitionPhase.FadeIn)
            GameSceneTransitionTimeScaleUtility.Restore(ref timeScaleChanged, previousTimeScale);

        if (phase != GameSceneTransitionPhase.FadeIn)
            GameSceneTransitionExecutionUtility.SetFade(ref fadeState, fadeState.Alpha, true, config);

        ApplyLoadingProgressForPhase(phase, ref loadingProgressState, config);
    }

    /// <summary>Completes the active transition and restores idle state.</summary>
    /// <param name="managerEntity">Scene manager entity that owns the transition state.</param>
    /// <param name="config">Runtime scene manager configuration.</param>
    /// <param name="transitionState">Mutable scene transition state.</param>
    /// <param name="fadeState">Mutable fade presentation state.</param>
    /// <param name="loadingProgressState">Mutable loading-progress presentation state.</param>
    private void CompleteTransition(Entity managerEntity,
                                    GameSceneManagerConfig config,
                                    ref GameSceneTransitionState transitionState,
                                    ref GameSceneFadePresentationState fadeState,
                                    ref GameSceneLoadingProgressPresentationState loadingProgressState)
    {
        GameSceneTransitionExecutionCompletionUtility.Complete(EntityManager, managerEntity,
                                                               ref activeOperation, ref activePhase, ref activePurpose,
                                                               ref suppressLoadingProgress, targetSceneId, ref transitionState,
                                                               ref fadeState, ref loadingProgressState, config,
                                                               ref timeScaleChanged, previousTimeScale);
    }

    #endregion

    #endregion
}
