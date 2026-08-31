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
    private const string BypassExpectedKey =
        "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.BypassExpected";
    private const string EnteredPlayKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.EnteredPlay";
    private const string FailureKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.Failure";
    private const string OrphanOwnerKey =
        "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.OrphanOwner";
    private const string SourceScenePathKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.SourceScenePath";
    private const string StartTicksKey = "NashCore.GameSceneManagementPlayModeSceneGuardSmokeTest.StartTicks";
    private const string StartRoomScenePath =
        "Assets/Scenes/LevelGenerationSceneSetTest/MetroConcourse/SCN_MAIN_METRO_START.unity";
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
        Start(GameSceneManagementProjectSetupUtility.GameplayUiScenePath, false, string.Empty);
    }

    /// <summary>
    /// Opens the procedural start room and validates normal Play Mode bootstrap entry and scene restoration.
    /// </summary>
    public static void RunFromStartRoom()
    {
        Start(StartRoomScenePath, false, string.Empty);
    }

    /// <summary>
    /// Verifies that an owned one-shot bypass keeps the procedural start room for one Play Mode entry only.
    /// </summary>
    public static void RunOwnedBypass()
    {
        Start(StartRoomScenePath, true, ActiveKey);
    }

    /// <summary>
    /// Verifies that a bypass without an active owner cannot prevent normal bootstrap Play Mode entry.
    /// </summary>
    public static void RunOrphanedBypass()
    {
        SessionState.SetBool(OrphanOwnerKey, false);
        Start(StartRoomScenePath, false, OrphanOwnerKey);
    }
    #endregion

    #region Execution Methods
    /// <summary>
    /// Starts the complete forced-bootstrap Play Mode restore validation from one source scene.
    /// </summary>
    /// <param name="sourceScenePath">Scene path that must be restored after Play Mode exits.</param>
    /// <param name="bypassExpected">True to request one owned Play Mode entry without bootstrap replacement.</param>
    /// <param name="bypassOwnerKey">Optional active or orphan SessionState owner used to request a bypass.</param>
    private static void Start(string sourceScenePath,
                              bool bypassExpected,
                              string bypassOwnerKey)
    {
        ValidateBypassLifetime();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(BypassExpectedKey, bypassExpected);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(SourceScenePathKey, sourceScenePath);
        SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());
        GameSceneManagementPlayModeSceneGuard.ClearOneShotBypass();
        EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);

        if (!string.IsNullOrWhiteSpace(bypassOwnerKey))
            GameSceneManagementPlayModeSceneGuard.RequestOneShotBypass(bypassOwnerKey);

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

        string expectedScenePath = SessionState.GetBool(BypassExpectedKey, false)
            ? SessionState.GetString(SourceScenePathKey, string.Empty)
            : GameSceneManagementProjectSetupUtility.BootstrapScenePath;

        if (!string.Equals(SceneManager.GetActiveScene().path, expectedScenePath, StringComparison.Ordinal))
        {
            SessionState.SetString(
                FailureKey,
                "Play Mode did not enter from the scene required by the normal or owned-bypass path.");
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

        string sourceScenePath = SessionState.GetString(SourceScenePathKey, string.Empty);

        SceneAsset configuredStartScene = EditorSceneManager.playModeStartScene;
        string configuredStartScenePath = AssetDatabase.GetAssetPath(configuredStartScene);

        if (string.Equals(SceneManager.GetActiveScene().path, sourceScenePath, StringComparison.Ordinal) &&
            string.Equals(configuredStartScenePath,
                          GameSceneManagementProjectSetupUtility.BootstrapScenePath,
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
    /// Verifies that leaked or malformed bypass requests cannot affect a later normal Play action.
    /// </summary>
    private static void ValidateBypassLifetime()
    {
        long currentTicks = DateTime.UtcNow.Ticks;
        string currentRequest = currentTicks.ToString();
        string expiredRequest = (currentTicks - 11L * TimeSpan.TicksPerSecond).ToString();

        if (!GameSceneManagementPlayModeSceneGuard.IsBypassRequestValid(currentRequest, currentTicks, true) ||
            GameSceneManagementPlayModeSceneGuard.IsBypassRequestValid(expiredRequest, currentTicks, true) ||
            GameSceneManagementPlayModeSceneGuard.IsBypassRequestValid(currentRequest, currentTicks, false) ||
            GameSceneManagementPlayModeSceneGuard.IsBypassRequestValid(string.Empty, currentTicks, true))
        {
            throw new InvalidOperationException("Play Mode bypass lifetime validation failed.");
        }
    }

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
        SessionState.SetBool(BypassExpectedKey, false);
        SessionState.SetBool(OrphanOwnerKey, false);
        SessionState.SetBool(EnteredPlayKey, false);
        SessionState.SetString(FailureKey, string.Empty);
        SessionState.SetString(SourceScenePathKey, string.Empty);
        SessionState.SetString(StartTicksKey, string.Empty);

        if (passed)
            Debug.Log("[GameSceneManagementPlayModeSceneGuardSmokeTest] Passed Play Mode scene selection, restoration and bootstrap rearming.");
        else
            Debug.LogError("[GameSceneManagementPlayModeSceneGuardSmokeTest] Failed: " + failure);

        EditorApplication.Exit(passed ? 0 : 1);
    }
    #endregion

    #endregion
}
