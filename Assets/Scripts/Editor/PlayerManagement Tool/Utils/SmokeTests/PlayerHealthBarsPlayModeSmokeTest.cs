using System;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;
using Hash128 = Unity.Entities.Hash128;

/// <summary>
/// Runs a short bootstrap Play Mode session and fails when the player archetype exceeds the ECS chunk capacity.
/// </summary>
[InitializeOnLoad]
public static class PlayerHealthBarsPlayModeSmokeTest
{
    #region Constants
    private const string ActiveKey = "NashCore.PlayerHealthBarsPlayModeSmokeTest.Active";
    private const string EnteredPlayKey = "NashCore.PlayerHealthBarsPlayModeSmokeTest.EnteredPlay";
    private const string FailureKey = "NashCore.PlayerHealthBarsPlayModeSmokeTest.Failure";
    private const string StartTicksKey = "NashCore.PlayerHealthBarsPlayModeSmokeTest.StartTicks";
    private const string LaunchTicksKey = "NashCore.PlayerHealthBarsPlayModeSmokeTest.LaunchTicks";
    private const string PlayerSubSceneGuid = "da7ade6fe92d5ba4cba3257fa8bbb3b8";
    private const string ArchetypeOverflowSignature = "Entity archetype component data is too large";
    private const string StructuralChangeDuringIterationSignature = "Structural changes are not allowed while iterating over entities";
    private const string NorthExitBakeFailureSignature = "[GameRoomPortalAuthoringBaker] Portal 'NorthExit' was not baked";
    private const double BootstrapTimeoutSeconds = 60d;
    private const double RuntimeSeconds = 20d;
    #endregion

    #region Constructors
    /// <summary>
    /// Registers callbacks after every editor or Play Mode domain reload.
    /// </summary>
    static PlayerHealthBarsPlayModeSmokeTest()
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
    /// Opens the authored gameplay and UI scenes and starts the time-bounded Play Mode archetype validation.
    /// </summary>
    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(StartTicksKey, string.Empty);
        SessionState.SetString(LaunchTicksKey, DateTime.UtcNow.Ticks.ToString());
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayScenePath, OpenSceneMode.Single);
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath, OpenSceneMode.Additive);
        GameSceneManagementPlayModeSceneGuard.RequestOneShotBypass(ActiveKey);
        EditorApplication.isPlaying = true;
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Tracks Play Mode entry so completion cannot race the editor transition.
    /// </summary>
    /// <param name="state">Current editor Play Mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetBool(EnteredPlayKey, true);
    }

    /// <summary>
    /// Captures targeted ECS initialization failures and the invalid NorthExit portal bake warning.
    /// </summary>
    /// <param name="condition">Logged condition text.</param>
    /// <param name="stackTrace">Logged stack trace.</param>
    /// <param name="type">Logged severity.</param>
    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (!ContainsTargetedFailure(condition) &&
            !ContainsTargetedFailure(stackTrace))
            return;

        SessionState.SetString(FailureKey, condition + Environment.NewLine + stackTrace);
    }

    /// <summary>
    /// Stops Play Mode after the validation window and exits batch mode with the resolved result.
    /// </summary>
    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        string failure = SessionState.GetString(FailureKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(failure))
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            else
                Finish(false, failure);

            return;
        }

        if (!SessionState.GetBool(EnteredPlayKey, false))
            return;

        if (EditorApplication.isPlaying &&
            !EnsurePersistentPlayerReady())
        {
            if (ResolveElapsedSeconds(LaunchTicksKey) >= BootstrapTimeoutSeconds)
                Finish(false, "Persistent Player SubScene did not produce one PlayerControllerConfig entity before the bootstrap timeout.");

            return;
        }

        if (string.IsNullOrWhiteSpace(SessionState.GetString(StartTicksKey, string.Empty)))
            SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());

        if (EditorApplication.isPlaying && ResolveElapsedSeconds() < RuntimeSeconds)
            return;

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        Finish(true, string.Empty);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Loads the authored persistent Player SubScene directly and waits for its authoritative entity.
    /// </summary>
    /// <returns>True when exactly one player entity exists in the Default World.</returns>
    private static bool EnsurePersistentPlayerReady()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        Hash128 sceneGuid = new Hash128(PlayerSubSceneGuid);
        Entity sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, sceneGuid);

        if (sceneEntity == Entity.Null)
        {
            SceneSystem.LoadSceneAsync(world.Unmanaged,
                                       sceneGuid,
                                       BuildPlayerSceneLoadParameters());
        }
        else if (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity) &&
                 !world.EntityManager.HasComponent<RequestSceneLoaded>(sceneEntity))
        {
            SceneSystem.LoadSceneAsync(world.Unmanaged,
                                       sceneEntity,
                                       BuildPlayerSceneLoadParameters());
        }

        EntityQuery playerQuery = world.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerControllerConfig>());

        try
        {
            return playerQuery.CalculateEntityCount() == 1;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }

    /// <summary>
    /// Builds blocking-import load parameters matching the production persistent-player loader.
    /// </summary>
    /// <returns>Load parameters used only by the Play Mode smoke test.</returns>
    private static SceneSystem.LoadParameters BuildPlayerSceneLoadParameters()
    {
        return new SceneSystem.LoadParameters
        {
            Flags = SceneLoadFlags.BlockOnImport
        };
    }

    /// <summary>
    /// Checks whether one log fragment contains a targeted ECS initialization or portal-bake failure.
    /// </summary>
    /// <param name="value">Log fragment to inspect.</param>
    /// <returns>True when a regression signature covered by this Play Mode test is present.</returns>
    private static bool ContainsTargetedFailure(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.IndexOf(ArchetypeOverflowSignature, StringComparison.Ordinal) >= 0 ||
               value.IndexOf(StructuralChangeDuringIterationSignature, StringComparison.Ordinal) >= 0 ||
               value.IndexOf(NorthExitBakeFailureSignature, StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Resolves elapsed wall-clock seconds across editor domain reloads.
    /// </summary>
    /// <returns>Elapsed seconds since the smoke test started.</returns>
    private static double ResolveElapsedSeconds()
    {
        return ResolveElapsedSeconds(StartTicksKey);
    }

    /// <summary>
    /// Resolves elapsed wall-clock seconds from one persisted tick key.
    /// </summary>
    /// <param name="ticksKey">SessionState key containing UTC start ticks.</param>
    /// <returns>Elapsed seconds, or the runtime window when the key is invalid.</returns>
    private static double ResolveElapsedSeconds(string ticksKey)
    {
        string startTicksText = SessionState.GetString(ticksKey, "0");

        if (!long.TryParse(startTicksText, out long startTicks) || startTicks <= 0)
            return RuntimeSeconds;

        return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTicks).TotalSeconds;
    }

    /// <summary>
    /// Clears persistent smoke state, reports the result, and exits the batch editor.
    /// </summary>
    /// <param name="passed">True when the runtime window completed without archetype overflow.</param>
    /// <param name="failure">Captured failure details.</param>
    private static void Finish(bool passed, string failure)
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(StartTicksKey, string.Empty);
        SessionState.SetString(LaunchTicksKey, string.Empty);
        GameSceneManagementPlayModeSceneGuard.ClearOneShotBypass();

        if (passed)
            Debug.Log("[PlayerHealthBarsPlayModeSmokeTest] Passed gameplay Play Mode player-archetype validation.");
        else
            Debug.LogError("[PlayerHealthBarsPlayModeSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }

    #endregion

    #endregion
}
