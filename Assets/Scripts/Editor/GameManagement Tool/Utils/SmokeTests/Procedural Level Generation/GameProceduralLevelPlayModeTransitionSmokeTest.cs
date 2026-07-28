#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies direct procedural Play startup, one streamed room traversal and one Play Again restart in real Play Mode.
/// </summary>
[InitializeOnLoad]
public static class GameProceduralLevelPlayModeTransitionSmokeTest
{
    #region Constants
    private const string ActiveKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.Active";
    private const string EnteredPlayKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.EnteredPlay";
    private const string FailureKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.Failure";
    private const string PhaseKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.Phase";
    private const string StepTicksKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.StepTicks";
    private const string SourceNodeKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.SourceNode";
    private const string RestartPlayerIndexKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.RestartPlayerIndex";
    private const string RestartPlayerVersionKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.RestartPlayerVersion";
    private const string TargetPortalIdKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.TargetPortalId";
    private const string TargetPortalSideKey = "NashCore.GameProceduralLevelPlayModeTransitionSmokeTest.TargetPortalSide";
    private const string MainMenuSceneId = "SCN_MainMenu";
    private const string FallbackGameplaySceneId = "SCN_MainScene";
    private const string WaitingForMainMenuPhase = "WaitingForMainMenu";
    private const string WaitingForInitialRoomPhase = "WaitingForInitialRoom";
    private const string WaitingForTraversalPhase = "WaitingForTraversal";
    private const string WaitingForRestartStartPhase = "WaitingForRestartStart";
    private const string WaitingForRestartCompletionPhase = "WaitingForRestartCompletion";
    private const string CompletedPhase = "Completed";
    private const string initialRoomControlCycle = "Initial room control release";
    private const string roomTraversalControlCycle = "Room traversal control release";
    private const string restartControlCycle = "Restart control release";
    private const double StepTimeoutSeconds = 180d;
    #endregion

    #region Constructors
    /// <summary>
    /// Restores editor, Play Mode and log callbacks after every domain reload.
    /// </summary>
    static GameProceduralLevelPlayModeTransitionSmokeTest()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes generated room metadata, opens bootstrap and starts the time-bounded Play Mode transition regression test.
    /// </summary>
    public static void Run()
    {
        // Discard any recovered dirty editor backup before scanning, otherwise a room left open by an interrupted
        // test correctly keeps its metadata stale and prevents this independent runtime regression from starting.
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.BootstrapScenePath,
                                     OpenSceneMode.Single);
        GameRoomMetadataRefreshReport metadataReport = GameRoomMetadataAutomaticRefreshUtility.RefreshAllStaleReferencedRooms();

        if (!metadataReport.Succeeded)
            throw new InvalidOperationException("Procedural room metadata refresh failed before Play Mode: " +
                                                string.Join(" | ", metadataReport.Errors));

        AssetDatabase.SaveAssets();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(PhaseKey, WaitingForMainMenuPhase);
        SessionState.SetString(StepTicksKey, DateTime.UtcNow.Ticks.ToString());
        SessionState.SetInt(SourceNodeKey, -1);
        GameProceduralCameraContinuitySmokeUtility.Reset();
        GameProceduralPlayerControlReleaseSmokeUtility.Reset();
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, true);
        EditorApplication.isPlaying = true;
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Records Play Mode entry so the update loop cannot complete during editor state changes.
    /// </summary>
    /// <param name="state">Current editor Play Mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredPlayMode)
            return;

        SessionState.SetBool(EnteredPlayKey, true);
        ResetStepTimeout();
    }

    /// <summary>
    /// Captures targeted runtime failures that invalidate procedural streaming or arrival readiness.
    /// </summary>
    /// <param name="condition">Logged condition text.</param>
    /// <param name="stackTrace">Logged stack trace.</param>
    /// <param name="type">Logged severity.</param>
    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false) || type != LogType.Exception && type != LogType.Error)
            return;

        if (!GameProceduralLevelTransitionSmokeDiagnosticUtility.ContainsTargetedFailure(condition) &&
            !GameProceduralLevelTransitionSmokeDiagnosticUtility.ContainsTargetedFailure(stackTrace))
            return;

        SessionState.SetString(FailureKey, condition + Environment.NewLine + stackTrace);
    }

    /// <summary>
    /// Drives menu startup, initial-room readiness and one graph-selected room traversal across domain reloads.
    /// </summary>
    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        string failure = SessionState.GetString(FailureKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(failure))
        {
            StopOrFinish(false, failure);
            return;
        }

        if (!SessionState.GetBool(EnteredPlayKey, false))
            return;

        if (!EditorApplication.isPlaying)
        {
            bool completed = string.Equals(SessionState.GetString(PhaseKey, string.Empty),
                                           CompletedPhase,
                                           StringComparison.Ordinal);
            Finish(completed,
                   completed ? string.Empty : "Play Mode exited before the procedural traversal completed.");
            return;
        }

        if (IsFallbackGameplaySceneLoaded())
        {
            StopOrFinish(false, "SCN_MainScene was loaded before the generated Start room.");
            return;
        }

        bool hasManager = TryResolveManager(out EntityManager entityManager, out Entity managerEntity);

        if (ResolveElapsedStepSeconds() >= StepTimeoutSeconds)
        {
            string diagnostic = hasManager
                ? GameProceduralLevelTransitionSmokeDiagnosticUtility.BuildRuntimeDiagnostic(entityManager,
                                                                                              managerEntity)
                : "The scene manager singleton was unavailable.";
            StopOrFinish(false,
                         "Timed out during phase '" + SessionState.GetString(PhaseKey, string.Empty) + "'. " +
                         diagnostic);
            return;
        }

        if (!hasManager)
            return;

        switch (SessionState.GetString(PhaseKey, WaitingForMainMenuPhase))
        {
            case WaitingForMainMenuPhase:
                TryStartProceduralRun(entityManager, managerEntity);
                break;
            case WaitingForInitialRoomPhase:
                TryStartTraversal(entityManager, managerEntity);
                break;
            case WaitingForTraversalPhase:
                TryCompleteTraversal(entityManager, managerEntity);
                break;
            case WaitingForRestartStartPhase:
                TryObserveRestartStart(entityManager, managerEntity);
                break;
            case WaitingForRestartCompletionPhase:
                TryCompleteRestart(entityManager, managerEntity);
                break;
        }
    }
    #endregion

    #region Runtime Steps
    /// <summary>
    /// Submits the same default-gameplay command used by the main-menu Play button after menu readiness.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    private static void TryStartProceduralRun(EntityManager entityManager, Entity managerEntity)
    {
        GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);

        if (transitionState.IsTransitioning != 0 ||
            transitionState.ActiveSceneId.ToString() != MainMenuSceneId)
        {
            return;
        }

        if (!GameSceneTransitionRequestUtility.EnqueueLoadDefaultGameplay())
        {
            SessionState.SetString(FailureKey, "The default-gameplay request could not be queued from the loaded main menu.");
            return;
        }

        SessionState.SetString(PhaseKey, WaitingForInitialRoomPhase);
        ResetStepTimeout();
    }

    /// <summary>
    /// Waits for the generated Start room, then submits one of its authoritative outgoing graph edges.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    private static void TryStartTraversal(EntityManager entityManager, Entity managerEntity)
    {
        GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (transitionState.IsTransitioning != 0)
        {
            if (!GameProceduralPlayerControlReleaseSmokeUtility.Tick(entityManager,
                                                                      transitionState,
                                                                      initialRoomControlCycle,
                                                                      out string releaseFailure))
            {
                SessionState.SetString(FailureKey, releaseFailure);
            }

            return;
        }

        if (runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentNodeIndex < 0)
        {
            return;
        }

        if (!GameRoomRewardPresentationPlayModeSmokeUtility.TryValidate(entityManager,
                                                                        managerEntity,
                                                                        out bool presentationReady,
                                                                        out string presentationFailure))
        {
            SessionState.SetString(FailureKey, presentationFailure);
            return;
        }

        if (!presentationReady)
            return;

        if (!GameProceduralPlayerControlReleaseSmokeUtility.TryComplete(entityManager,
                                                                        initialRoomControlCycle,
                                                                        out bool releaseReady,
                                                                        out string releaseCompletionFailure))
        {
            SessionState.SetString(FailureKey, releaseCompletionFailure);
            return;
        }

        if (!releaseReady)
            return;

        if (!GameProceduralCameraContinuitySmokeUtility.CaptureAndStore(entityManager, out string cameraFailure))
        {
            SessionState.SetString(FailureKey, cameraFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateGameplayUi(out string uiFailure))
        {
            SessionState.SetString(FailureKey, uiFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateSingleManagedRoom(entityManager,
                                                                                  managerEntity,
                                                                                  out string roomFailure))
        {
            SessionState.SetString(FailureKey, roomFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateAuthoredRoomPlacement(entityManager,
                                                                                       out string placementFailure))
        {
            SessionState.SetString(FailureKey, placementFailure);
            return;
        }

        DynamicBuffer<GameProceduralRoomEdgeElement> edges = entityManager.GetBuffer<GameProceduralRoomEdgeElement>(managerEntity, true);

        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            GameProceduralRoomEdgeElement edge = edges[edgeIndex];

            if (edge.SourceNodeIndex != runtimeState.CurrentNodeIndex)
                continue;

            DynamicBuffer<GameProceduralRoomTraversalRequest> requests = entityManager.GetBuffer<GameProceduralRoomTraversalRequest>(managerEntity);
            requests.Add(new GameProceduralRoomTraversalRequest
            {
                SourcePortalId = edge.SourcePortalId,
                SourceNodeIndex = runtimeState.CurrentNodeIndex,
                AssignedEdgeIndex = edge.EdgeIndex
            });
            SessionState.SetInt(SourceNodeKey, runtimeState.CurrentNodeIndex);
            SessionState.SetString(TargetPortalIdKey, edge.TargetPortalId.ToString());
            SessionState.SetInt(TargetPortalSideKey, (int)edge.TargetSide);
            SessionState.SetString(PhaseKey, WaitingForTraversalPhase);
            ResetStepTimeout();
            return;
        }

        SessionState.SetString(FailureKey, "The generated Start node contains no outgoing edge to test.");
    }

    /// <summary>
    /// Completes after the second generated room is active and its pending arrival has committed.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    private static void TryCompleteTraversal(EntityManager entityManager, Entity managerEntity)
    {
        GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (transitionState.IsTransitioning != 0)
        {
            if (transitionState.Purpose != GameSceneTransitionPurpose.ProceduralRoomTraversal)
            {
                SessionState.SetString(FailureKey,
                                       "Room traversal started with unexpected transition purpose " + transitionState.Purpose + ".");
                return;
            }

            if (Time.timeScale <= 0.0001f)
                SessionState.SetString(FailureKey, "Transactional room traversal paused global Unity time scale.");

            if (!GameProceduralPlayerControlReleaseSmokeUtility.Tick(entityManager,
                                                                      transitionState,
                                                                      roomTraversalControlCycle,
                                                                      out string releaseFailure))
            {
                SessionState.SetString(FailureKey, releaseFailure);
            }

            return;
        }

        if (runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentNodeIndex == SessionState.GetInt(SourceNodeKey, -1))
        {
            return;
        }

        if (!GameProceduralPlayerControlReleaseSmokeUtility.TryComplete(entityManager,
                                                                        roomTraversalControlCycle,
                                                                        out bool releaseReady,
                                                                        out string releaseCompletionFailure))
        {
            SessionState.SetString(FailureKey, releaseCompletionFailure);
            return;
        }

        if (!releaseReady)
            return;

        if (!GameProceduralCameraContinuitySmokeUtility.Validate(entityManager, out string cameraFailure))
        {
            SessionState.SetString(FailureKey, cameraFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateGameplayUi(out string uiFailure))
        {
            SessionState.SetString(FailureKey, uiFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateSingleManagedRoom(entityManager,
                                                                                  managerEntity,
                                                                                  out string roomFailure))
        {
            SessionState.SetString(FailureKey, roomFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateAuthoredRoomPlacement(entityManager,
                                                                                       out string placementFailure))
        {
            SessionState.SetString(FailureKey, placementFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateTargetPortalAlignment(
                entityManager,
                SessionState.GetString(TargetPortalIdKey, string.Empty),
                (GameRoomPortalSide)SessionState.GetInt(TargetPortalSideKey, -1),
                out string portalFailure))
        {
            SessionState.SetString(FailureKey, portalFailure);
            return;
        }

        if (!GameProceduralRuntimeReadinessSmokeUtility.TryValidateEnemySpawnersReady(entityManager,
                                                                                      out bool enemySpawnersReady,
                                                                                      out string enemyFailure))
        {
            SessionState.SetString(FailureKey, enemyFailure);
            return;
        }

        if (!enemySpawnersReady)
            return;

        if (!GameProceduralRuntimeReadinessSmokeUtility.ValidatePlayerProjectilePoolReady(entityManager,
                                                                                           out string poolFailure))
        {
            SessionState.SetString(FailureKey, poolFailure);
            return;
        }

        if (!GameProceduralRuntimeReadinessSmokeUtility.TryResolvePlayerEntity(entityManager, out Entity playerEntity))
        {
            SessionState.SetString(FailureKey, "The persistent player was unavailable before procedural restart validation.");
            return;
        }

        // Seed a finalized outcome so the restart must recreate both player runtime and its companion gameplay UI.
        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);
        runOutcomeState.Outcome = PlayerRunOutcome.Defeat;
        runOutcomeState.IsFinalized = 1;
        entityManager.SetComponentData(playerEntity, runOutcomeState);
        SessionState.SetInt(RestartPlayerIndexKey, playerEntity.Index);
        SessionState.SetInt(RestartPlayerVersionKey, playerEntity.Version);

        if (!GameProceduralLevelRunRequestUtility.TryRestartActiveRun())
        {
            SessionState.SetString(FailureKey, "The active procedural run rejected the Play Again restart request.");
            return;
        }

        SessionState.SetString(PhaseKey, WaitingForRestartStartPhase);
        ResetStepTimeout();
    }

    /// <summary>
    /// Waits until procedural generation has replaced the run with a direct initial-room transition.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    private static void TryObserveRestartStart(EntityManager entityManager, Entity managerEntity)
    {
        GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);

        if (transitionState.IsTransitioning == 0)
            return;

        if (!GameProceduralPlayerControlReleaseSmokeUtility.Tick(entityManager,
                                                                  transitionState,
                                                                  restartControlCycle,
                                                                  out string releaseFailure))
        {
            SessionState.SetString(FailureKey, releaseFailure);
            return;
        }

        if (transitionState.Purpose != GameSceneTransitionPurpose.ProceduralInitialRoom)
        {
            SessionState.SetString(FailureKey,
                                   "Play Again started an unexpected " + transitionState.Purpose + " transition instead of a direct procedural initial-room load.");
            return;
        }

        SessionState.SetString(PhaseKey, WaitingForRestartCompletionPhase);
        ResetStepTimeout();
    }

    /// <summary>
    /// Completes after Play Again has rebuilt player runtime, companion UI and a ready projectile pool in the new Start room.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    private static void TryCompleteRestart(EntityManager entityManager, Entity managerEntity)
    {
        GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);

        if (transitionState.IsTransitioning != 0)
        {
            if (!GameProceduralPlayerControlReleaseSmokeUtility.Tick(entityManager,
                                                                      transitionState,
                                                                      restartControlCycle,
                                                                      out string releaseFailure))
            {
                SessionState.SetString(FailureKey, releaseFailure);
            }

            return;
        }

        if (runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentNodeIndex < 0)
        {
            return;
        }

        if (!GameProceduralPlayerControlReleaseSmokeUtility.TryComplete(entityManager,
                                                                        restartControlCycle,
                                                                        out bool releaseReady,
                                                                        out string releaseCompletionFailure))
        {
            SessionState.SetString(FailureKey, releaseCompletionFailure);
            return;
        }

        if (!releaseReady)
            return;

        if (!GameProceduralRuntimeReadinessSmokeUtility.TryResolvePlayerEntity(entityManager, out Entity playerEntity))
        {
            SessionState.SetString(FailureKey, "Play Again completed without one persistent player entity.");
            return;
        }

        if (playerEntity.Index == SessionState.GetInt(RestartPlayerIndexKey, -1) &&
            playerEntity.Version == SessionState.GetInt(RestartPlayerVersionKey, -1))
        {
            SessionState.SetString(FailureKey, "Play Again retained the finalized persistent player instead of recreating its runtime scene.");
            return;
        }

        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);

        if (runOutcomeState.Outcome != PlayerRunOutcome.None ||
            runOutcomeState.IsDying != 0 ||
            runOutcomeState.IsFinalized != 0)
        {
            SessionState.SetString(FailureKey, "Play Again retained finalized run-outcome state on the recreated player.");
            return;
        }

        if (!GameProceduralRuntimeReadinessSmokeUtility.ValidatePlayerProjectilePoolReady(entityManager,
                                                                                           out string poolFailure))
        {
            SessionState.SetString(FailureKey, poolFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.ValidateGameplayUi(out string uiFailure))
        {
            SessionState.SetString(FailureKey, uiFailure);
            return;
        }

        if (!GameProceduralCameraContinuitySmokeUtility.Validate(entityManager, out string restartCameraFailure))
        {
            SessionState.SetString(FailureKey, "Play Again camera reset failed: " + restartCameraFailure);
            return;
        }

        SessionState.SetString(PhaseKey, CompletedPhase);
        EditorApplication.isPlaying = false;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the unique scene and procedural manager from the default ECS world.
    /// </summary>
    /// <param name="entityManager">Resolved default-world entity manager.</param>
    /// <param name="managerEntity">Resolved singleton entity.</param>
    /// <returns>True when the required runtime singleton is available.</returns>
    private static bool TryResolveManager(out EntityManager entityManager, out Entity managerEntity)
    {
        entityManager = default;
        managerEntity = Entity.Null;
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSceneTransitionState>(),
                                                            ComponentType.ReadOnly<GameProceduralLevelRuntimeState>(),
                                                            ComponentType.ReadOnly<GameProceduralRoomEdgeElement>(),
                                                            ComponentType.ReadWrite<GameProceduralRoomTraversalRequest>());

        try
        {
            if (query.CalculateEntityCount() != 1)
                return false;

            managerEntity = query.GetSingletonEntity();
            return true;
        }
        finally
        {
            query.Dispose();
        }
    }

    /// <summary>
    /// Checks whether the fallback gameplay scene ever became loaded during direct procedural startup.
    /// </summary>
    /// <returns>True when SCN_MainScene is currently loaded.</returns>
    private static bool IsFallbackGameplaySceneLoaded()
    {
        Scene scene = SceneManager.GetSceneByName(FallbackGameplaySceneId);
        return scene.IsValid() && scene.isLoaded;
    }

    /// <summary>
    /// Restarts the wall-clock timeout for the current asynchronous transition step.
    /// </summary>
    private static void ResetStepTimeout()
    {
        SessionState.SetString(StepTicksKey, DateTime.UtcNow.Ticks.ToString());
    }

    /// <summary>
    /// Resolves elapsed wall-clock seconds across editor and Play Mode domain reloads.
    /// </summary>
    /// <returns>Elapsed seconds since the current transition step began.</returns>
    private static double ResolveElapsedStepSeconds()
    {
        string ticksText = SessionState.GetString(StepTicksKey, "0");

        if (!long.TryParse(ticksText, out long ticks) || ticks <= 0)
            return StepTimeoutSeconds;

        return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ticks).TotalSeconds;
    }

    /// <summary>
    /// Leaves Play Mode before reporting a result, or reports immediately when already back in edit mode.
    /// </summary>
    /// <param name="passed">True when the complete transition sequence succeeded.</param>
    /// <param name="failure">Failure description when the sequence did not complete.</param>
    private static void StopOrFinish(bool passed, string failure)
    {
        SessionState.SetString(FailureKey, failure);

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        Finish(passed, failure);
    }

    /// <summary>
    /// Clears persistent smoke state, reports the result and exits the batch editor.
    /// </summary>
    /// <param name="passed">True when both generated rooms completed their transitions.</param>
    /// <param name="failure">Failure description when validation did not complete.</param>
    private static void Finish(bool passed, string failure)
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(PhaseKey, string.Empty);
        SessionState.SetString(StepTicksKey, string.Empty);
        SessionState.SetInt(SourceNodeKey, -1);
        SessionState.SetInt(RestartPlayerIndexKey, -1);
        SessionState.SetInt(RestartPlayerVersionKey, -1);
        SessionState.SetString(TargetPortalIdKey, string.Empty);
        SessionState.SetInt(TargetPortalSideKey, -1);
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, false);
        GameProceduralPlayerControlReleaseSmokeUtility.Reset();

        if (passed)
            Debug.Log("[GameProceduralLevelPlayModeTransitionSmokeTest] Direct Start-room load, player and portal reward presentation, streamed traversal and Play Again restart passed.");
        else
            Debug.LogError("[GameProceduralLevelPlayModeTransitionSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }
    #endregion

    #endregion
}
#endif
