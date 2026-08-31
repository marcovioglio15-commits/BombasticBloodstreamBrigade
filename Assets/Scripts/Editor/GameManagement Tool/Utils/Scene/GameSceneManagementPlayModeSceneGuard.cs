using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configures Unity's Play Mode start scene as the project bootstrap while preserving the open editor scene setup.
/// </summary>
[InitializeOnLoad]
public static class GameSceneManagementPlayModeSceneGuard
{
    #region Constants
    private const string BypassSessionKey = "NashCore.GameSceneManagement.BypassForcedBootstrapPlayMode";
    private const string BypassOwnerSessionKey =
        "NashCore.GameSceneManagement.BypassForcedBootstrapPlayMode.OwnerSessionKey";
    private const string BypassRequestTicksKey =
        "NashCore.GameSceneManagement.BypassForcedBootstrapPlayMode.RequestTicks";
    private const long BypassRequestValidityTicks = 10L * TimeSpan.TicksPerSecond;
    #endregion

    #region Constructors
    /// <summary>
    /// Registers Play Mode callbacks and arms the bootstrap start scene after editor assemblies load.
    /// </summary>
    static GameSceneManagementPlayModeSceneGuard()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            ScheduleBootstrapPlayModeStartScene();
    }
    #endregion

    #region Methods

    #region Play Mode Events
    /// <summary>
    /// Validates normal Play Mode before entry and rearms bootstrap behavior after returning to Edit Mode.
    /// </summary>
    /// <param name="state">Current Play Mode state transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                PrepareBootstrapPlayMode();
                break;
            case PlayModeStateChange.EnteredEditMode:
                ScheduleBootstrapPlayModeStartScene();
                break;
        }
    }
    #endregion

    #region Preparation Methods
    /// <summary>
    /// Validates generated metadata and bootstrap authoring before Unity enters its configured Play Mode start scene.
    /// </summary>
    private static void PrepareBootstrapPlayMode()
    {
        if (ConsumeBypassRequest())
            return;

        if (!ConfigureBootstrapPlayModeStartScene())
        {
            EditorApplication.isPlaying = false;
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorApplication.isPlaying = false;
            return;
        }

        // Complete deferred room work before Play Mode reads generated procedural metadata.
        GameRoomMetadataRefreshReport metadataReport = GameRoomMetadataAutomaticRefreshUtility.RefreshAllStaleReferencedRooms();

        if (!metadataReport.Succeeded)
        {
            Debug.LogError(
                "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because procedural room metadata could not be refreshed: " +
                string.Join(" | ", metadataReport.Errors));
            EditorApplication.isPlaying = false;
            return;
        }

        if (!ValidateBootstrapRuntimeConfiguration())
            EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Schedules bootstrap start-scene configuration after the current editor callback has completed.
    /// </summary>
    private static void ScheduleBootstrapPlayModeStartScene()
    {
        EditorApplication.delayCall -= ApplyScheduledBootstrapPlayModeStartScene;
        EditorApplication.delayCall += ApplyScheduledBootstrapPlayModeStartScene;
    }

    /// <summary>
    /// Applies one deferred bootstrap start-scene request and removes its one-shot editor callback.
    /// </summary>
    private static void ApplyScheduledBootstrapPlayModeStartScene()
    {
        EditorApplication.delayCall -= ApplyScheduledBootstrapPlayModeStartScene;
        ConfigureBootstrapPlayModeStartScene();
    }

    /// <summary>
    /// Assigns the authored bootstrap SceneAsset to Unity's native Play Mode start-scene mechanism.
    /// </summary>
    /// <returns>True when the configured bootstrap scene exists and was assigned.</returns>
    private static bool ConfigureBootstrapPlayModeStartScene()
    {
        string bootstrapPath = GameSceneManagementProjectSetupUtility.BootstrapScenePath;
        SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapPath);

        if (bootstrapScene == null)
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.LogWarning(
                "[GameSceneManagementPlayModeSceneGuard] Bootstrap scene is missing at path: " + bootstrapPath + ".");
            return false;
        }

        EditorSceneManager.playModeStartScene = bootstrapScene;
        return true;
    }
    #endregion

    #region Validation Methods
    /// <summary>
    /// Validates bootstrap presets from an isolated preview scene without replacing the open editor scene setup.
    /// </summary>
    /// <returns>True when the bootstrap contains one valid Scene Manager authoring configuration.</returns>
    private static bool ValidateBootstrapRuntimeConfiguration()
    {
        Scene bootstrapScene = EditorSceneManager.OpenPreviewScene(
            GameSceneManagementProjectSetupUtility.BootstrapScenePath);

        if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
        {
            Debug.LogError(
                "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because the bootstrap preview scene could not be opened.");
            return false;
        }

        try
        {
            if (!TryResolveBootstrapAuthoring(bootstrapScene, out GameSceneManagerAuthoring authoring))
                return false;

            GameProceduralLevelPreset proceduralPreset = authoring.ResolveProceduralLevelPreset();

            if (proceduralPreset == null)
                return true;

            GameSceneManagerPreset scenePreset = authoring.ResolveSceneManagerPreset();

            if (!GameProceduralLevelBakeUtility.TryValidateRuntimeConfiguration(
                    proceduralPreset,
                    scenePreset,
                    out string proceduralFailure))
            {
                Debug.LogError(
                    "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because procedural generation is invalid. " +
                    proceduralFailure,
                    authoring);
                return false;
            }

            GameRoomClearRewardsPreset rewardPreset = authoring.ResolveRoomClearRewardsPreset();

            if (rewardPreset == null)
                return true;

            if (GameRoomRewardBakeUtility.TryValidateRuntimeConfiguration(
                    rewardPreset,
                    proceduralPreset,
                    out string rewardFailure))
            {
                return true;
            }

            Debug.LogError(
                "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because Room Clear Rewards are invalid. " +
                rewardFailure,
                authoring);
            return false;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(bootstrapScene);
        }
    }

    /// <summary>
    /// Resolves the single Scene Manager authoring component contained by the bootstrap preview scene.
    /// </summary>
    /// <param name="bootstrapScene">Loaded preview scene whose hierarchy should be inspected.</param>
    /// <param name="authoring">Single resolved authoring component when validation succeeds.</param>
    /// <returns>True when the preview scene contains exactly one Scene Manager authoring component.</returns>
    private static bool TryResolveBootstrapAuthoring(Scene bootstrapScene,
                                                     out GameSceneManagerAuthoring authoring)
    {
        authoring = null;
        int authoringCount = 0;
        GameObject[] roots = bootstrapScene.GetRootGameObjects();

        // Count inactive and nested components so the validation matches the baker's complete authored input.
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameSceneManagerAuthoring[] rootAuthorings =
                roots[rootIndex].GetComponentsInChildren<GameSceneManagerAuthoring>(true);

            for (int authoringIndex = 0; authoringIndex < rootAuthorings.Length; authoringIndex++)
            {
                authoring = rootAuthorings[authoringIndex];
                authoringCount++;
            }
        }

        if (authoringCount == 1)
            return true;

        Debug.LogError(
            "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because the bootstrap scene must contain exactly one GameSceneManagerAuthoring component.");
        return false;
    }
    #endregion

    #region Bypass Methods
    /// <summary>
    /// Requests a short-lived bypass owned by a focused editor runner and clears Unity's configured start scene.
    /// </summary>
    /// <param name="ownerSessionKey">SessionState boolean key that remains true only while the requesting runner is active.</param>
    internal static void RequestOneShotBypass(string ownerSessionKey)
    {
        if (string.IsNullOrWhiteSpace(ownerSessionKey))
            throw new ArgumentException("A Play Mode bypass requires an owner SessionState key.", nameof(ownerSessionKey));

        SessionState.SetString(BypassOwnerSessionKey, ownerSessionKey);
        SessionState.SetString(BypassRequestTicksKey, DateTime.UtcNow.Ticks.ToString());
        SessionState.SetBool(BypassSessionKey, true);
        EditorSceneManager.playModeStartScene = null;
    }

    /// <summary>
    /// Clears a pending one-shot bypass and rearms bootstrap entry when the editor is idle.
    /// </summary>
    internal static void ClearOneShotBypass()
    {
        ClearBypassState();

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            ScheduleBootstrapPlayModeStartScene();
    }

    /// <summary>
    /// Consumes a bypass only when its timestamp and owning editor runner are both still valid.
    /// </summary>
    /// <returns>True when the current Play Mode entry should preserve its existing scene setup.</returns>
    private static bool ConsumeBypassRequest()
    {
        if (!SessionState.GetBool(BypassSessionKey, false))
            return false;

        string ownerSessionKey = SessionState.GetString(BypassOwnerSessionKey, string.Empty);
        string requestTicksText = SessionState.GetString(BypassRequestTicksKey, string.Empty);
        bool ownerActive = !string.IsNullOrWhiteSpace(ownerSessionKey) &&
                           SessionState.GetBool(ownerSessionKey, false);
        ClearBypassState();
        return IsBypassRequestValid(requestTicksText, DateTime.UtcNow.Ticks, ownerActive);
    }

    /// <summary>
    /// Validates that a bypass is owned, parseable, non-future, and inside its short request window.
    /// </summary>
    /// <param name="requestTicksText">Serialized UTC tick count stored when the editor runner requested bypass.</param>
    /// <param name="currentTicks">Current UTC tick count used for deterministic validation.</param>
    /// <param name="ownerActive">True when the requesting editor runner still owns an active session.</param>
    /// <returns>True only while the one-shot request belongs to the current active runner and Play action.</returns>
    internal static bool IsBypassRequestValid(string requestTicksText,
                                              long currentTicks,
                                              bool ownerActive)
    {
        if (!ownerActive ||
            !long.TryParse(requestTicksText, out long requestTicks) ||
            requestTicks <= 0 ||
            currentTicks < requestTicks)
        {
            return false;
        }

        return currentTicks - requestTicks <= BypassRequestValidityTicks;
    }

    /// <summary>
    /// Clears every SessionState value used by the one-shot Play Mode bypass.
    /// </summary>
    private static void ClearBypassState()
    {
        SessionState.SetBool(BypassSessionKey, false);
        SessionState.SetString(BypassOwnerSessionKey, string.Empty);
        SessionState.SetString(BypassRequestTicksKey, string.Empty);
    }
    #endregion

    #endregion
}
