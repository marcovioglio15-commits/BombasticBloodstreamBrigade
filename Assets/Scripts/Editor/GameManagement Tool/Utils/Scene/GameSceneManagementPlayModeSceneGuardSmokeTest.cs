using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Verifies that editor Play Mode starts from bootstrap and restores the previously active scene afterward.
/// </summary>
[InitializeOnLoad]
public static class GameSceneManagementPlayModeSceneGuardSmokeTest
{
    #region Constants
    private const string ActiveKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.Active";
    private const string EnteredPlayKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.EnteredPlay";
    private const string FailureKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.Failure";
    private const string StartTicksKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.StartTicks";
    private const double TimeoutSeconds = 40d;
    #endregion

    #region Constructors
    /// <summary>
    /// Registers state and update callbacks after editor domain reloads.
    /// </summary>
    static GameSceneManagementPlayModeSceneGuardSmokeTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens a non-bootstrap scene and starts the complete forced-bootstrap Play Mode restore validation.
    /// </summary>
    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());
        SessionState.SetBool(GameSceneManagementPlayModeSceneGuard.BypassSessionKey, false);
        EditorSceneManager.OpenScene(GameSceneManagementProjectSetupUtility.GameplayUiScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Validates the active scene on Play Mode entry and requests exit after bootstrap is confirmed.
    /// </summary>
    /// <param name="state">Current editor Play Mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredPlayMode)
            return;

        SessionState.SetBool(EnteredPlayKey, true);

        if (!string.Equals(SceneManager.GetActiveScene().path,
                           GameSceneManagementProjectSetupUtility.BootstrapScenePath,
                           StringComparison.Ordinal))
        {
            SessionState.SetString(FailureKey, "Play Mode did not enter from the configured bootstrap scene.");
        }

        EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Waits for edit-mode restoration, then exits batch mode with the resolved result.
    /// </summary>
    private static void Update()
    {
        if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string failure = SessionState.GetString(FailureKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(failure))
        {
            Finish(false, failure);
            return;
        }

        if (!SessionState.GetBool(EnteredPlayKey, false))
            return;

        if (string.Equals(SceneManager.GetActiveScene().path,
                          GameSceneManagementProjectSetupUtility.GameplayUiScenePath,
                          StringComparison.Ordinal))
        {
            Finish(true, string.Empty);
            return;
        }

        if (ResolveElapsedSeconds() >= TimeoutSeconds)
            Finish(false, "The previously active editor scene was not restored before timeout.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves elapsed wall-clock seconds across editor domain reloads.
    /// </summary>
    /// <returns>Elapsed seconds since the smoke test started.</returns>
    private static double ResolveElapsedSeconds()
    {
        string startTicksText = SessionState.GetString(StartTicksKey, "0");

        if (!long.TryParse(startTicksText, out long startTicks) || startTicks <= 0)
            return TimeoutSeconds;

        return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTicks).TotalSeconds;
    }

    /// <summary>
    /// Clears smoke state, reports the result, and exits the batch editor.
    /// </summary>
    /// <param name="passed">True when bootstrap entry and source-scene restoration both succeeded.</param>
    /// <param name="failure">Failure description when validation did not complete.</param>
    private static void Finish(bool passed, string failure)
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(StartTicksKey, string.Empty);

        if (passed)
            Debug.Log("[GameSceneManagementPlayModeSceneGuardSmokeTest] Passed bootstrap entry and editor scene restoration.");
        else
            Debug.LogError("[GameSceneManagementPlayModeSceneGuardSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }
    #endregion

    #endregion
}
