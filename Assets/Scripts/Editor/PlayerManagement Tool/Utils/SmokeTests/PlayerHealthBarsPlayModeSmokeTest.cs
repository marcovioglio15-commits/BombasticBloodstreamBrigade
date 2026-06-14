using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, true);
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayScenePath, OpenSceneMode.Single);
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath, OpenSceneMode.Additive);
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
        {
            SessionState.SetBool(EnteredPlayKey, true);
            SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());
        }
    }

    /// <summary>
    /// Captures the targeted ECS chunk-capacity exception emitted during player initialization.
    /// </summary>
    /// <param name="condition">Logged condition text.</param>
    /// <param name="stackTrace">Logged stack trace.</param>
    /// <param name="type">Logged severity.</param>
    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        if (!ContainsArchetypeOverflow(condition) && !ContainsArchetypeOverflow(stackTrace))
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
    /// Checks whether one log fragment contains the targeted ECS chunk-capacity exception.
    /// </summary>
    /// <param name="value">Log fragment to inspect.</param>
    /// <returns>True when the archetype-overflow signature is present.</returns>
    private static bool ContainsArchetypeOverflow(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf("Entity archetype component data is too large", StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Resolves elapsed wall-clock seconds across editor domain reloads.
    /// </summary>
    /// <returns>Elapsed seconds since the smoke test started.</returns>
    private static double ResolveElapsedSeconds()
    {
        string startTicksText = SessionState.GetString(StartTicksKey, "0");

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
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, false);

        if (passed)
            Debug.Log("[PlayerHealthBarsPlayModeSmokeTest] Passed gameplay Play Mode player-archetype validation.");
        else
            Debug.LogError("[PlayerHealthBarsPlayModeSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }

    #endregion

    #endregion
}
