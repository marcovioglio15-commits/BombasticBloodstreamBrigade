using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Injects and removes the transient real-player and persistent-camera rig used to test boundaries in the current scene.
/// </summary>
[InitializeOnLoad]
internal static class GameCameraBoundaryFastPlayEditorUtility
{
    #region Constants
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string DefaultSceneManagerPresetPath =
        "Assets/Scriptable Objects/Game/Scene Management/GameSceneManagerPreset.asset";
    private const string ActiveSessionKey = "NashCore.CameraBoundaryFastPlay.Active";
    private const string TargetSceneHandleKey = "NashCore.CameraBoundaryFastPlay.TargetSceneHandle";
    private const string PreviousActiveSceneHandleKey = "NashCore.CameraBoundaryFastPlay.PreviousActiveSceneHandle";
    private const string TargetSceneWasDirtyKey = "NashCore.CameraBoundaryFastPlay.TargetSceneWasDirty";
    #endregion

    #region Static Fields
    private static readonly Action<Scene> clearSceneDirtiness = ResolveClearSceneDirtiness();
    #endregion

    #region Constructors
    /// <summary>
    /// Registers cleanup across Play Mode transitions and removes interrupted Fast Play roots after editor reloads.
    /// </summary>
    static GameCameraBoundaryFastPlayEditorUtility()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += CleanupInterruptedSession;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts Fast Play in the selected boundary's loaded scene without saving, opening or replacing scene assets.
    /// </summary>
    /// <param name="boundaryAuthoring">Selected boundary that identifies the tested scene and spawn volume.</param>
    /// <param name="failureMessage">Diagnostic returned when the real project rig cannot be prepared.</param>
    /// <returns>True when the transient rig was created and Play Mode was requested.</returns>
    public static bool Start(GameCameraBoundaryAuthoring boundaryAuthoring, out string failureMessage)
    {
        failureMessage = string.Empty;

        if (boundaryAuthoring == null || !boundaryAuthoring.gameObject.scene.IsValid() ||
            !boundaryAuthoring.gameObject.scene.isLoaded)
        {
            failureMessage = "Select a Camera Boundary in a loaded scene before starting Fast Play.";
            return false;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        CleanupFastPlayRoots();
        Scene targetScene = boundaryAuthoring.gameObject.scene;
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        PlayerAuthoring playerAuthoring = playerPrefab != null ? playerPrefab.GetComponent<PlayerAuthoring>() : null;
        PlayerMasterPreset playerMasterPreset = playerAuthoring != null ? playerAuthoring.MasterPreset : null;

        if (playerPrefab == null || playerMasterPreset == null || playerMasterPreset.ControllerPreset == null ||
            playerMasterPreset.ControllerPreset.CameraSettings == null ||
            playerMasterPreset.ControllerPreset.CameraSettings.Values == null)
        {
            failureMessage = "The project player prefab or its master/controller preset is missing at " +
                             PlayerPrefabPath + ".";
            return false;
        }

        float movementSpeed;

        try
        {
            movementSpeed = ResolveMovementSpeed(playerMasterPreset);
        }
        catch (Exception exception)
        {
            failureMessage = "The player movement Add Scaling formulas could not be resolved: " + exception.Message;
            return false;
        }

        if (movementSpeed <= 0f)
        {
            failureMessage = "The resolved player maximum movement speed must be greater than zero for Camera Boundary Fast Play.";
            return false;
        }

        GameSceneManagerPreset sceneManagerPreset = ResolveSceneManagerPreset(targetScene);

        if (sceneManagerPreset == null ||
            !sceneManagerPreset.TryFindScene(sceneManagerPreset.BootstrapSceneId,
                                             out GameSceneDefinition bootstrapDefinition) ||
            string.IsNullOrWhiteSpace(bootstrapDefinition.ScenePath))
        {
            failureMessage = "The active Scene Manager preset does not resolve a bootstrap scene with the persistent gameplay camera.";
            return false;
        }

        GameObject fastPlayRoot = null;
        bool targetSceneWasDirty = targetScene.isDirty;
        Scene previousActiveScene = SceneManager.GetActiveScene();

        try
        {
            fastPlayRoot = CreateFastPlayRoot(targetScene,
                                              boundaryAuthoring,
                                              playerPrefab,
                                               playerMasterPreset,
                                               sceneManagerPreset,
                                               bootstrapDefinition.ScenePath,
                                               movementSpeed);

            if (fastPlayRoot == null)
            {
                RestoreSceneDirtyState(targetScene, targetSceneWasDirty);
                failureMessage = "The transient player and persistent gameplay camera rig could not be created.";
                return false;
            }

            SessionState.SetBool(ActiveSessionKey, true);
            SessionState.SetInt(TargetSceneHandleKey, targetScene.handle);
            SessionState.SetInt(PreviousActiveSceneHandleKey, previousActiveScene.handle);
            SessionState.SetBool(TargetSceneWasDirtyKey, targetSceneWasDirty);
            SceneManager.SetActiveScene(targetScene);
            GameSceneManagementPlayModeSceneGuard.RequestOneShotBypass(ActiveSessionKey);
            EditorApplication.isPlaying = true;
            return true;
        }
        catch (Exception exception)
        {
            if (fastPlayRoot != null)
                Object.DestroyImmediate(fastPlayRoot);

            RestoreSceneDirtyState(targetScene, targetSceneWasDirty);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);

            ClearSessionState();
            failureMessage = "Camera Boundary Fast Play setup failed: " + exception.Message;
            return false;
        }
    }
    #endregion

    #region Creation Methods
    /// <summary>
    /// Builds one temporary root containing the project player prefab and a clone of the real bootstrap camera rig.
    /// </summary>
    /// <param name="targetScene">Currently loaded scene receiving transient objects.</param>
    /// <param name="boundaryAuthoring">Selected boundary used to resolve spawn position.</param>
    /// <param name="playerPrefab">Project player prefab asset.</param>
    /// <param name="playerMasterPreset">Actual player master preset.</param>
    /// <param name="sceneManagerPreset">Actual Scene Manager preset.</param>
    /// <param name="bootstrapScenePath">Configured bootstrap scene containing the persistent gameplay camera.</param>
    /// <param name="movementSpeed">Formula-scaled maximum movement speed used by the transient target.</param>
    /// <returns>Transient root ready for the Play Mode scene copy, or null when camera cloning fails.</returns>
    private static GameObject CreateFastPlayRoot(Scene targetScene,
                                                 GameCameraBoundaryAuthoring boundaryAuthoring,
                                                 GameObject playerPrefab,
                                                 PlayerMasterPreset playerMasterPreset,
                                                 GameSceneManagerPreset sceneManagerPreset,
                                                 string bootstrapScenePath,
                                                 float movementSpeed)
    {
        GameObject cameraRig = ClonePersistentCameraRig(bootstrapScenePath, targetScene);

        if (cameraRig == null)
            return null;

        GameObject fastPlayRoot = new GameObject("[Camera Boundary Fast Play - Temporary]");
        SceneManager.MoveGameObjectToScene(fastPlayRoot, targetScene);
        fastPlayRoot.hideFlags = HideFlags.DontSaveInBuild;
        cameraRig.transform.SetParent(fastPlayRoot.transform, true);
        cameraRig.SetActive(false);

        GameObject playerInstance = PrefabUtility.InstantiatePrefab(playerPrefab, targetScene) as GameObject;

        if (playerInstance == null)
        {
            Object.DestroyImmediate(fastPlayRoot);
            return null;
        }

        playerInstance.name = "PF_Player [Camera Boundary Fast Play]";
        playerInstance.transform.SetParent(fastPlayRoot.transform, true);
        Vector3 spawnPosition = ResolveSpawnPosition(boundaryAuthoring,
                                                     sceneManagerPreset,
                                                     playerInstance.transform.position.y);
        playerInstance.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);

        // Preserve the persistent rig's authored offset while relocating its bootstrap origin to the selected volume.
        cameraRig.transform.position += spawnPosition;
        GameCameraBoundaryFastPlayRig fastPlayRig = fastPlayRoot.AddComponent<GameCameraBoundaryFastPlayRig>();
        fastPlayRig.Configure(playerMasterPreset,
                              sceneManagerPreset,
                               playerInstance.transform,
                               cameraRig,
                               spawnPosition,
                               movementSpeed);
        return fastPlayRoot;
    }

    /// <summary>
    /// Clones the exact persistent gameplay camera hierarchy from the configured bootstrap scene through a preview scene.
    /// </summary>
    /// <param name="bootstrapScenePath">Project-relative bootstrap scene path.</param>
    /// <param name="targetScene">Loaded scene receiving the clone.</param>
    /// <returns>Inactive cloned camera root, or null when the bootstrap hierarchy is incomplete.</returns>
    private static GameObject ClonePersistentCameraRig(string bootstrapScenePath, Scene targetScene)
    {
        Scene previewScene = EditorSceneManager.OpenPreviewScene(bootstrapScenePath);

        try
        {
            GameObject[] roots = previewScene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameSceneBootstrapCameraView cameraView =
                    roots[rootIndex].GetComponentInChildren<GameSceneBootstrapCameraView>(true);

                if (cameraView == null)
                    continue;

                GameObject sourceCameraRig = cameraView.transform.root.gameObject;
                sourceCameraRig.SetActive(false);
                GameObject cameraRig = Object.Instantiate(sourceCameraRig);
                cameraRig.name = "Persistent Gameplay Camera [Camera Boundary Fast Play]";
                SceneManager.MoveGameObjectToScene(cameraRig, targetScene);
                return cameraRig;
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        return null;
    }

    /// <summary>
    /// Uses the selected collider center for containment or an exterior approach point for impassable mode.
    /// </summary>
    /// <param name="boundaryAuthoring">Selected Camera Boundary authoring object.</param>
    /// <param name="sceneManagerPreset">Preset selecting containment or impassable-volume behavior.</param>
    /// <param name="playerHeight">Authored player-prefab height preserved independently from the boundary transform.</param>
    /// <returns>World-space player spawn position suitable for the selected boundary mode.</returns>
    private static Vector3 ResolveSpawnPosition(GameCameraBoundaryAuthoring boundaryAuthoring,
                                                GameSceneManagerPreset sceneManagerPreset,
                                                float playerHeight)
    {
        if (sceneManagerPreset != null &&
            sceneManagerPreset.CameraBoundaryMode == GameCameraBoundaryMode.ImpassableVolume &&
            boundaryAuthoring.TryBuildBoundary(out GameCameraBoundary boundary))
        {
            float2 planarRight = math.normalizesafe(boundary.PlanarRight, new float2(1f, 0f));
            float exteriorDistance = boundary.HalfExtents.x +
                                     math.max(1f, sceneManagerPreset.CameraBoundarySoftZoneDistance + 1f);
            float2 planarPosition = boundary.Center - planarRight * exteriorDistance;
            return new Vector3(planarPosition.x, playerHeight, planarPosition.y);
        }

        BoxCollider boundaryCollider = boundaryAuthoring.GetComponent<BoxCollider>();

        if (boundaryCollider != null)
        {
            // Match the baked footprint exactly: collider height plus transform pitch and roll are intentionally ignored.
            Transform boundaryTransform = boundaryAuthoring.transform;
            Vector3 lossyScale = boundaryTransform.lossyScale;
            Vector3 planarCenter = new Vector3(boundaryCollider.center.x * lossyScale.x,
                                               0f,
                                               boundaryCollider.center.z * lossyScale.z);
            Vector3 spawnPosition = boundaryTransform.position +
                                    Quaternion.Euler(0f, boundaryTransform.eulerAngles.y, 0f) * planarCenter;
            spawnPosition.y = playerHeight;
            return spawnPosition;
        }

        Vector3 fallbackPosition = boundaryAuthoring.transform.position;
        fallbackPosition.y = playerHeight;
        return fallbackPosition;
    }
    #endregion

    #region Preset Resolution Methods
    /// <summary>
    /// Resolves the Fast Play speed through the same scoped Add Scaling formula pipeline used by player baking.
    /// </summary>
    /// <param name="playerMasterPreset">Master preset supplying every formula context and scalable preset source.</param>
    /// <returns>Resolved maximum movement speed, or zero when movement configuration is incomplete.</returns>
    internal static float ResolveMovementSpeed(PlayerMasterPreset playerMasterPreset)
    {
        if (playerMasterPreset == null)
            return 0f;

        using (PlayerScaledPresetScope scope = PlayerPresetScalingBakeUtility.CreateScope(
                   playerMasterPreset.ControllerPreset,
                   playerMasterPreset.ProgressionPreset,
                   playerMasterPreset.PowerUpsPreset,
                   playerMasterPreset.VisualPreset,
                   playerMasterPreset.UiVisualPreset,
                   playerMasterPreset.AnimationBindingsPreset))
        {
            MovementSettings movementSettings = scope.ControllerPreset != null
                ? scope.ControllerPreset.MovementSettings
                : null;
            return movementSettings != null && movementSettings.Values != null
                ? movementSettings.Values.MaxSpeed
                : 0f;
        }
    }

    /// <summary>
    /// Resolves a loaded scene manager first, then falls back to the project's default Scene Manager preset asset.
    /// </summary>
    /// <param name="targetScene">Scene whose local manager receives priority.</param>
    /// <returns>Effective Scene Manager preset, or null when no configured source exists.</returns>
    private static GameSceneManagerPreset ResolveSceneManagerPreset(Scene targetScene)
    {
        GameSceneManagerAuthoring[] authorings =
            Object.FindObjectsByType<GameSceneManagerAuthoring>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None);
        GameSceneManagerPreset fallbackPreset = null;

        for (int authoringIndex = 0; authoringIndex < authorings.Length; authoringIndex++)
        {
            GameSceneManagerPreset preset = authorings[authoringIndex].ResolveSceneManagerPreset();

            if (preset == null)
                continue;

            if (authorings[authoringIndex].gameObject.scene == targetScene)
                return preset;

            if (fallbackPreset == null)
                fallbackPreset = preset;
        }

        if (fallbackPreset != null)
            return fallbackPreset;

        return AssetDatabase.LoadAssetAtPath<GameSceneManagerPreset>(DefaultSceneManagerPresetPath);
    }
    #endregion

    #region Lifecycle Methods
    /// <summary>
    /// Removes the temporary edit-scene root after Play Mode returns and restores active/dirty scene state.
    /// </summary>
    /// <param name="state">Unity Play Mode lifecycle state.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveSessionKey, false))
            return;

        int targetSceneHandle = SessionState.GetInt(TargetSceneHandleKey, 0);
        int previousActiveSceneHandle = SessionState.GetInt(PreviousActiveSceneHandleKey, 0);
        bool targetSceneWasDirty = SessionState.GetBool(TargetSceneWasDirtyKey, true);
        CleanupFastPlayRoots();
        Scene targetScene = ResolveLoadedScene(targetSceneHandle);
        RestoreSceneDirtyState(targetScene, targetSceneWasDirty);
        Scene previousActiveScene = ResolveLoadedScene(previousActiveSceneHandle);

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);

        ClearSessionState();
    }

    /// <summary>
    /// Cleans roots left by an interrupted editor session only when no Play Mode transition is active.
    /// </summary>
    private static void CleanupInterruptedSession()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (!SessionState.GetBool(ActiveSessionKey, false))
        {
            // A bypass without its Fast Play owner must never affect the next normal Play action.
            CleanupFastPlayRoots();
            GameSceneManagementPlayModeSceneGuard.ClearOneShotBypass();
            return;
        }

        HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
    }

    /// <summary>
    /// Destroys every loaded temporary root without touching authored Camera Boundary objects.
    /// </summary>
    private static void CleanupFastPlayRoots()
    {
        GameCameraBoundaryFastPlayRig[] rigs =
            Resources.FindObjectsOfTypeAll<GameCameraBoundaryFastPlayRig>();

        for (int rigIndex = rigs.Length - 1; rigIndex >= 0; rigIndex--)
        {
            GameCameraBoundaryFastPlayRig rig = rigs[rigIndex];

            if (rig == null || !rig.gameObject.scene.IsValid())
                continue;

            Object.DestroyImmediate(rig.gameObject);
        }
    }

    /// <summary>
    /// Resolves one loaded scene by its runtime handle without opening any asset.
    /// </summary>
    /// <param name="sceneHandle">Scene handle captured before Play Mode.</param>
    /// <returns>Matching loaded scene, or an invalid scene when it no longer exists.</returns>
    private static Scene ResolveLoadedScene(int sceneHandle)
    {
        int loadedSceneCount = SceneManager.sceneCount;

        for (int sceneIndex = 0; sceneIndex < loadedSceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (scene.handle == sceneHandle)
                return scene;
        }

        return default;
    }

    /// <summary>
    /// Restores a previously clean scene after temporary object removal without clearing legitimate pre-existing dirtiness.
    /// </summary>
    /// <param name="scene">Scene whose temporary root was removed.</param>
    /// <param name="wasDirty">Dirty state captured before Fast Play setup.</param>
    private static void RestoreSceneDirtyState(Scene scene, bool wasDirty)
    {
        if (scene.IsValid() && scene.isLoaded && !wasDirty && clearSceneDirtiness != null)
            clearSceneDirtiness(scene);
    }

    /// <summary>
    /// Resolves Unity's editor-only scene dirtiness reset once because the installed Unity version keeps it non-public.
    /// </summary>
    /// <returns>Cached editor delegate, or null when the installed editor no longer exposes the compatibility API.</returns>
    private static Action<Scene> ResolveClearSceneDirtiness()
    {
        MethodInfo method = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                                                                 BindingFlags.NonPublic |
                                                                 BindingFlags.Static);

        if (method == null)
            return null;

        return (Action<Scene>)Delegate.CreateDelegate(typeof(Action<Scene>), method);
    }

    /// <summary>
    /// Clears one-shot editor state after cleanup or a failed Play Mode transition.
    /// </summary>
    private static void ClearSessionState()
    {
        SessionState.SetBool(ActiveSessionKey, false);
        SessionState.EraseInt(TargetSceneHandleKey);
        SessionState.EraseInt(PreviousActiveSceneHandleKey);
        SessionState.EraseBool(TargetSceneWasDirtyKey);
        GameSceneManagementPlayModeSceneGuard.ClearOneShotBypass();
    }
    #endregion

    #endregion
}
