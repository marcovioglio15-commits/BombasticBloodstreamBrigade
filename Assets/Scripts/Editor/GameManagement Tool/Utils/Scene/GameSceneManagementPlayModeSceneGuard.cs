using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Forces Play Mode to start from the bootstrap scene in editor, then restores the previous scene setup afterward.
/// </summary>
[InitializeOnLoad]
public static class GameSceneManagementPlayModeSceneGuard
{
    #region Constants
    private const string EnabledPreferenceKey = "NashCore.GameSceneManagement.ForceBootstrapPlayMode.V3";
    private const string PendingRestoreKey = "NashCore.GameSceneManagement.PlayModeRestorePending";
    private const string SerializedSetupKey = "NashCore.GameSceneManagement.SerializedPlayModeSceneSetup";
    private const string MenuPath = "Tools/Game/Scene Manager/Force Bootstrap Play Mode";
    internal const string BypassSessionKey = "NashCore.GameSceneManagement.BypassForcedBootstrapPlayMode";
    #endregion

    #region Constructors
    /// <summary>
    /// Registers Play Mode callbacks once when editor assemblies load.
    /// </summary>
    static GameSceneManagementPlayModeSceneGuard()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

        if (!EditorApplication.isPlayingOrWillChangePlaymode &&
            SessionState.GetBool(PendingRestoreKey, false))
        {
            EditorApplication.delayCall += RestorePreviousSceneSetup;
        }
    }
    #endregion

    #region Methods

    #region Menu
    /// <summary>
    /// Toggles forced bootstrap Play Mode scene setup for local editor sessions.
    /// </summary>
    //[MenuItem(MenuPath)]
    private static void ToggleForcedBootstrapPlayMode()
    {
        SetEnabled(!IsEnabled());
    }

    /// <summary>
    /// Validates the Play Mode guard menu item and keeps its check mark synchronized.
    /// </summary>
    /// <returns>True because the menu item is always available in editor.</returns>
    //[MenuItem(MenuPath, true)]
    private static bool ValidateForcedBootstrapPlayMode()
    {
        Menu.SetChecked(MenuPath, IsEnabled());
        return true;
    }
    #endregion

    #region Play Mode Events
    /// <summary>
    /// Applies bootstrap setup before entering Play Mode and restores the previous scene setup after exit.
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
                EditorApplication.delayCall += RestorePreviousSceneSetup;
                break;
        }
    }
    #endregion

    #region Preparation
    /// <summary>
    /// Captures the current scene setup and opens only the bootstrap scene before Play Mode starts.
    /// </summary>
    private static void PrepareBootstrapPlayMode()
    {
        if (ConsumeBypassRequest())
            return;

        if (!IsEnabled())
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorApplication.isPlaying = false;
            return;
        }

        // A scene save queues metadata work, but Play Mode can begin before that delayed callback runs.
        // Complete and persist the generated room snapshot now so runtime bootstrap never reads a stale asset.
        GameRoomMetadataRefreshReport metadataReport = GameRoomMetadataAutomaticRefreshUtility.RefreshAllStaleReferencedRooms();

        if (!metadataReport.Succeeded)
        {
            Debug.LogError("[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because procedural room metadata could not be refreshed: " +
                           string.Join(" | ", metadataReport.Errors));
            EditorApplication.isPlaying = false;
            return;
        }

        if (!CanCaptureCurrentSceneSetup())
        {
            EditorApplication.isPlaying = false;
            return;
        }

        SceneSetup[] currentSetup = EditorSceneManager.GetSceneManagerSetup();
        StoreSceneSetup(currentSetup);

        if (!OpenBootstrapScene() || !ValidateBootstrapRuntimeConfiguration())
        {
            RestorePreviousSceneSetup();
            EditorApplication.isPlaying = false;
        }
    }

    /// <summary>
    /// Verifies that all open scenes can be restored after Play Mode.
    /// </summary>
    /// <returns>True when every loaded scene has a persistent asset path.</returns>
    private static bool CanCaptureCurrentSceneSetup()
    {
        SceneSetup[] currentSetup = EditorSceneManager.GetSceneManagerSetup();

        for (int index = 0; index < currentSetup.Length; index++)
        {
            SceneSetup setup = currentSetup[index];

            if (!setup.isLoaded)
                continue;

            if (!string.IsNullOrWhiteSpace(setup.path))
                continue;

            EditorUtility.DisplayDialog("Scene Manager Play Mode",
                                        "The current scene setup contains an unsaved scene. Save it before entering Play Mode so the editor can restore your workspace after testing.",
                                        "OK");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Opens the authored bootstrap scene as the only loaded scene for Play Mode.
    /// </summary>
    /// <returns>True when the bootstrap scene was opened successfully.</returns>
    private static bool OpenBootstrapScene()
    {
        string bootstrapPath = GameSceneManagementProjectSetupUtility.BootstrapScenePath;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapPath) == null)
        {
            Debug.LogWarning("[GameSceneManagementPlayModeSceneGuard] Bootstrap scene is missing at path: " + bootstrapPath + ".");
            return false;
        }

        EditorSceneManager.OpenScene(bootstrapPath, OpenSceneMode.Single);
        return true;
    }

    /// <summary>
    /// Blocks Play Mode when the bootstrap's procedural graph or room reward configuration cannot bake safely.
    /// </summary>
    /// <returns>True when the active bootstrap authoring configuration passes bake-equivalent validation.</returns>
    private static bool ValidateBootstrapRuntimeConfiguration()
    {
        GameSceneManagerAuthoring[] authorings =
            UnityEngine.Object.FindObjectsByType<GameSceneManagerAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (authorings.Length != 1)
        {
            Debug.LogError(
                "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because the bootstrap scene must contain exactly one GameSceneManagerAuthoring component.");
            return false;
        }

        GameSceneManagerAuthoring authoring = authorings[0];
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
            return true;

        Debug.LogError(
            "[GameSceneManagementPlayModeSceneGuard] Play Mode was cancelled because Room Clear Rewards are invalid. " +
            rewardFailure,
            authoring);
        return false;
    }
    #endregion

    #region Restoration
    /// <summary>
    /// Restores the scene setup captured before Play Mode, when a restore is pending.
    /// </summary>
    private static void RestorePreviousSceneSetup()
    {
        EditorApplication.delayCall -= RestorePreviousSceneSetup;

        if (!SessionState.GetBool(PendingRestoreKey, false))
            return;

        SceneSetup[] restoredSetup = LoadSceneSetup();

        if (restoredSetup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(restoredSetup);

        SessionState.SetBool(PendingRestoreKey, false);
        SessionState.SetString(SerializedSetupKey, string.Empty);
    }
    #endregion

    #region Persistence
    /// <summary>
    /// Stores a serializable copy of the current editor scene setup in SessionState.
    /// </summary>
    /// <param name="sceneSetup">Scene setup captured before Play Mode.</param>
    private static void StoreSceneSetup(SceneSetup[] sceneSetup)
    {
        StoredSceneSetupCollection collection = new StoredSceneSetupCollection();
        collection.Scenes = new List<StoredSceneSetup>(sceneSetup.Length);

        for (int index = 0; index < sceneSetup.Length; index++)
        {
            collection.Scenes.Add(new StoredSceneSetup(sceneSetup[index]));
        }

        SessionState.SetString(SerializedSetupKey, JsonUtility.ToJson(collection));
        SessionState.SetBool(PendingRestoreKey, true);
    }

    /// <summary>
    /// Loads the previously stored editor scene setup from SessionState.
    /// </summary>
    /// <returns>Scene setup array ready for EditorSceneManager.RestoreSceneManagerSetup.</returns>
    private static SceneSetup[] LoadSceneSetup()
    {
        string serializedSetup = SessionState.GetString(SerializedSetupKey, string.Empty);

        if (string.IsNullOrWhiteSpace(serializedSetup))
            return Array.Empty<SceneSetup>();

        StoredSceneSetupCollection collection = JsonUtility.FromJson<StoredSceneSetupCollection>(serializedSetup);

        if (collection == null || collection.Scenes == null)
            return Array.Empty<SceneSetup>();

        List<SceneSetup> sceneSetups = new List<SceneSetup>(collection.Scenes.Count);

        for (int index = 0; index < collection.Scenes.Count; index++)
        {
            StoredSceneSetup storedScene = collection.Scenes[index];

            if (storedScene == null || string.IsNullOrWhiteSpace(storedScene.Path))
                continue;

            sceneSetups.Add(storedScene.ToSceneSetup());
        }

        return sceneSetups.ToArray();
    }
    #endregion

    #region Preferences
    /// <summary>
    /// Resolves whether forced bootstrap Play Mode is enabled for the local editor.
    /// </summary>
    /// <returns>True when Play Mode should open SCN_Bootstrap automatically.</returns>
    private static bool IsEnabled()
    {
        return EditorPrefs.GetBool(EnabledPreferenceKey, true);
    }

    /// <summary>
    /// Consumes the one-shot bypass requested by automated Play Mode tests without disabling future  sessions.
    /// </summary>
    /// <returns>True when the current Play Mode entry alone should keep its existing scene setup.</returns>
    private static bool ConsumeBypassRequest()
    {
        if (!SessionState.GetBool(BypassSessionKey, false))
            return false;

        SessionState.SetBool(BypassSessionKey, false);
        return true;
    }

    /// <summary>
    /// Stores the local editor preference that controls forced bootstrap Play Mode.
    /// </summary>
    /// <param name="enabled">True to force bootstrap scene setup before Play Mode.</param>
    private static void SetEnabled(bool enabled)
    {
        EditorPrefs.SetBool(EnabledPreferenceKey, enabled);
    }
    #endregion

    #endregion

    #region Types
    /// <summary>
    /// Serializable collection wrapper used by SessionState JSON storage.
    /// </summary>
    [Serializable]
    private sealed class StoredSceneSetupCollection
    {
        public List<StoredSceneSetup> Scenes;
    }

    /// <summary>
    /// Serializable scene setup entry used to survive Play Mode domain reloads.
    /// </summary>
    [Serializable]
    private sealed class StoredSceneSetup
    {
        public string Path;
        public bool IsLoaded;
        public bool IsActive;

        /// <summary>
        /// Creates a JSON-serializable scene setup entry.
        /// </summary>
        /// <param name="sceneSetup">Source editor scene setup.</param>
        public StoredSceneSetup(SceneSetup sceneSetup)
        {
            Path = sceneSetup.path;
            IsLoaded = sceneSetup.isLoaded;
            IsActive = sceneSetup.isActive;
        }

        /// <summary>
        /// Converts this stored entry back into Unity's editor scene setup type.
        /// </summary>
        /// <returns>SceneSetup value compatible with EditorSceneManager.RestoreSceneManagerSetup.</returns>
        public SceneSetup ToSceneSetup()
        {
            return new SceneSetup
            {
                path = Path,
                isLoaded = IsLoaded,
                isActive = IsActive
            };
        }
    }
    #endregion
}
