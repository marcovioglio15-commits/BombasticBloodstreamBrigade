#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies direct procedural Play startup and one complete streamed room traversal in a real Play Mode session.
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
    private const string MainMenuSceneId = "SCN_MainMenu";
    private const string FallbackGameplaySceneId = "SCN_MainScene";
    private const string WaitingForMainMenuPhase = "WaitingForMainMenu";
    private const string WaitingForInitialRoomPhase = "WaitingForInitialRoom";
    private const string WaitingForTraversalPhase = "WaitingForTraversal";
    private const string CompletedPhase = "Completed";
    private const double StepTimeoutSeconds = 75d;
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
    /// Opens bootstrap and starts the time-bounded Play Mode transition regression test.
    /// </summary>
    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(PhaseKey, WaitingForMainMenuPhase);
        SessionState.SetString(StepTicksKey, DateTime.UtcNow.Ticks.ToString());
        SessionState.SetInt(SourceNodeKey, -1);
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, true);
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.BootstrapScenePath, OpenSceneMode.Single);
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

        if (!ContainsTargetedFailure(condition) && !ContainsTargetedFailure(stackTrace))
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
                ? BuildRuntimeDiagnostic(entityManager, managerEntity)
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

        if (transitionState.IsTransitioning != 0 ||
            runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentNodeIndex < 0)
        {
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

        if (transitionState.IsTransitioning != 0 ||
            runtimeState.Phase != GameProceduralLevelRuntimePhase.Active ||
            runtimeState.CurrentNodeIndex == SessionState.GetInt(SourceNodeKey, -1))
        {
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
    /// Checks one log fragment for failures relevant to scene streaming, ECS buffer lifetime or procedural generation.
    /// </summary>
    /// <param name="value">Log fragment to inspect.</param>
    /// <returns>True when the fragment contains a targeted runtime failure signature.</returns>
    private static bool ContainsTargetedFailure(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.IndexOf("ObjectDisposedException", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("Attempted to access BufferTypeHandle", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("BlobAssetReference is not valid", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("[GameProceduralLevel]", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Builds a compact state snapshot when an asynchronous Play Mode step exceeds its timeout.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager.</param>
    /// <param name="managerEntity">Scene and procedural manager singleton.</param>
    /// <returns>Transition, procedural context and target-portal state useful for identifying the blocked readiness condition.</returns>
    private static string BuildRuntimeDiagnostic(EntityManager entityManager, Entity managerEntity)
    {
        GameSceneTransitionState transitionState = entityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelRuntimeState runtimeState = entityManager.GetComponentData<GameProceduralLevelRuntimeState>(managerEntity);
        GameProceduralRoomTransitionContext context = entityManager.GetComponentData<GameProceduralRoomTransitionContext>(managerEntity);
        EntityQuery portalQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>());
        int matchingPortalCount = 0;

        try
        {
            using NativeArray<GameRoomPortal> portals = portalQuery.ToComponentDataArray<GameRoomPortal>(Allocator.Temp);

            // Count only the graph-selected arrival ID so stale or missing room authoring is immediately visible.
            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                if (portals[portalIndex].PortalId.Equals(context.TargetPortalId))
                    matchingPortalCount++;
            }

            return "Transition=" + transitionState.Phase +
                   ", IsTransitioning=" + transitionState.IsTransitioning +
                   ", Active='" + transitionState.ActiveSceneId +
                   "', Target='" + transitionState.TargetSceneId +
                   "', RuntimePhase=" + runtimeState.Phase +
                   ", CurrentNode=" + runtimeState.CurrentNodeIndex +
                   ", PendingNode=" + runtimeState.PendingNodeIndex +
                   ", RelocationPending=" + context.RelocationPending +
                   ", CommitPending=" + context.CommitPending +
                   ", TargetPortal='" + context.TargetPortalId +
                   "', MatchingPortals=" + matchingPortalCount +
                   ", TotalPortals=" + portals.Length + ".";
        }
        finally
        {
            portalQuery.Dispose();
        }
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
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, false);

        if (passed)
            Debug.Log("[GameProceduralLevelPlayModeTransitionSmokeTest] Direct Start-room load and streamed room traversal passed.");
        else
            Debug.LogError("[GameProceduralLevelPlayModeTransitionSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }
    #endregion

    #endregion
}
#endif
