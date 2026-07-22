#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Captures and verifies persistent gameplay camera identity and player framing across streamed room traversal.
/// </summary>
public static class GameProceduralCameraContinuitySmokeUtility
{
    #region Constants
    private const string SourceCameraInstanceKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourceCameraInstance";
    private const string SourceCameraOffsetXKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourceCameraOffsetX";
    private const string SourceCameraOffsetYKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourceCameraOffsetY";
    private const string SourceCameraOffsetZKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourceCameraOffsetZ";
    private const string SourcePlayerPositionXKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerPositionX";
    private const string SourcePlayerPositionYKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerPositionY";
    private const string SourcePlayerPositionZKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerPositionZ";
    private const string SourcePlayerRotationXKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerRotationX";
    private const string SourcePlayerRotationYKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerRotationY";
    private const string SourcePlayerRotationZKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerRotationZ";
    private const string SourcePlayerRotationWKey = "NashCore.GameProceduralCameraContinuitySmokeUtility.SourcePlayerRotationW";
    private const float CameraOffsetTolerance = 0.25f;
    private const float PlayerPositionTolerance = 0.001f;
    private const float PlayerRotationToleranceDegrees = 0.1f;
    private const float PortalAlignmentTolerance = 0.001f;
    private const float ViewportTolerance = 0.05f;
    private const float SpatialStagingHeightThreshold = -50000f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Clears the cross-frame camera snapshot before a new Play Mode smoke-test session starts.
    /// </summary>
    public static void Reset()
    {
        SessionState.SetInt(SourceCameraInstanceKey, 0);
        SessionState.SetFloat(SourceCameraOffsetXKey, 0f);
        SessionState.SetFloat(SourceCameraOffsetYKey, 0f);
        SessionState.SetFloat(SourceCameraOffsetZKey, 0f);
        SessionState.SetFloat(SourcePlayerPositionXKey, 0f);
        SessionState.SetFloat(SourcePlayerPositionYKey, 0f);
        SessionState.SetFloat(SourcePlayerPositionZKey, 0f);
        SessionState.SetFloat(SourcePlayerRotationXKey, 0f);
        SessionState.SetFloat(SourcePlayerRotationYKey, 0f);
        SessionState.SetFloat(SourcePlayerRotationZKey, 0f);
        SessionState.SetFloat(SourcePlayerRotationWKey, 1f);
    }

    /// <summary>
    /// Captures the persistent camera instance and its player-relative position before room traversal.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the persistent player transform.</param>
    /// <param name="failure">Diagnostic message when a unique player or persistent camera cannot be resolved.</param>
    /// <returns>True when the source camera snapshot was stored.</returns>
    public static bool CaptureAndStore(EntityManager entityManager, out string failure)
    {
        if (!TryCapture(entityManager,
                        out Camera gameplayCamera,
                        out Vector3 cameraOffset,
                        out Vector3 playerPosition,
                        out Quaternion playerRotation,
                        out failure))
        {
            return false;
        }

        SessionState.SetInt(SourceCameraInstanceKey, gameplayCamera.GetInstanceID());
        SessionState.SetFloat(SourceCameraOffsetXKey, cameraOffset.x);
        SessionState.SetFloat(SourceCameraOffsetYKey, cameraOffset.y);
        SessionState.SetFloat(SourceCameraOffsetZKey, cameraOffset.z);
        SessionState.SetFloat(SourcePlayerPositionXKey, playerPosition.x);
        SessionState.SetFloat(SourcePlayerPositionYKey, playerPosition.y);
        SessionState.SetFloat(SourcePlayerPositionZKey, playerPosition.z);
        SessionState.SetFloat(SourcePlayerRotationXKey, playerRotation.x);
        SessionState.SetFloat(SourcePlayerRotationYKey, playerRotation.y);
        SessionState.SetFloat(SourcePlayerRotationZKey, playerRotation.z);
        SessionState.SetFloat(SourcePlayerRotationWKey, playerRotation.w);
        return true;
    }

    /// <summary>
    /// Verifies the loaded gameplay UI overlay is enabled and attached to the persistent URP base camera.
    /// </summary>
    /// <param name="failure">Diagnostic message when the UI scene bridge or camera-stack ownership is invalid.</param>
    /// <returns>True when exactly one active gameplay UI camera is stacked on the persistent camera.</returns>
    public static bool ValidateGameplayUi(out string failure)
    {
        failure = string.Empty;
        Camera gameplayCamera = Camera.main;

        if (gameplayCamera == null || !GameSceneBootstrapCameraView.IsPersistentGameplayCamera(gameplayCamera))
        {
            failure = "Gameplay UI validation requires the persistent gameplay camera to own MainCamera.";
            return false;
        }

        UniversalAdditionalCameraData baseCameraData = gameplayCamera.GetComponent<UniversalAdditionalCameraData>();

        if (baseCameraData == null)
        {
            failure = "The persistent gameplay camera has no URP camera data for UI stacking.";
            return false;
        }

        int activeBridgeCount = 0;
        Camera resolvedUiCamera = null;

        // Inspect loaded scene roots explicitly so the smoke test does not rely on global object searches.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameSceneUiCameraStackBridge[] bridges = roots[rootIndex].GetComponentsInChildren<GameSceneUiCameraStackBridge>(true);

                for (int bridgeIndex = 0; bridgeIndex < bridges.Length; bridgeIndex++)
                {
                    if (!bridges[bridgeIndex].isActiveAndEnabled)
                        continue;

                    activeBridgeCount++;
                    resolvedUiCamera = bridges[bridgeIndex].GetComponent<Camera>();
                }
            }
        }

        if (activeBridgeCount != 1 || resolvedUiCamera == null || !resolvedUiCamera.isActiveAndEnabled)
        {
            failure = "Gameplay UI validation expected one active UI camera bridge, found " + activeBridgeCount + ".";
            return false;
        }

        if (baseCameraData.cameraStack.Contains(resolvedUiCamera))
            return true;

        failure = "The gameplay UI camera is not attached to the persistent URP base camera stack.";
        return false;
    }

    /// <summary>
    /// Verifies that exactly one managed scene referenced by the generated room graph is loaded.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing generated nodes and the scene catalog.</param>
    /// <param name="managerEntity">Unique procedural and scene manager entity.</param>
    /// <param name="failure">Diagnostic message when no room or multiple managed room scenes are resident.</param>
    /// <returns>True when the active generated room is the only loaded managed room scene.</returns>
    public static bool ValidateSingleManagedRoom(EntityManager entityManager,
                                                 Entity managerEntity,
                                                 out string failure)
    {
        failure = string.Empty;
        DynamicBuffer<GameProceduralRoomNodeElement> nodes = entityManager.GetBuffer<GameProceduralRoomNodeElement>(managerEntity, true);
        DynamicBuffer<GameSceneDefinitionElement> scenes = entityManager.GetBuffer<GameSceneDefinitionElement>(managerEntity, true);
        HashSet<string> roomScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Resolve reusable room paths once; duplicate graph nodes still represent the same managed scene asset.
        for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
        {
            for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
            {
                if (!scenes[sceneIndex].SceneId.Equals(nodes[nodeIndex].SceneId))
                    continue;

                roomScenePaths.Add(scenes[sceneIndex].ScenePath.ToString());
                break;
            }
        }

        int loadedRoomCount = 0;
        string loadedRooms = string.Empty;

        // Count exact managed scene handles so duplicate loads of one reusable asset cannot pass the assertion.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.IsValid() || !scene.isLoaded || !roomScenePaths.Contains(scene.path))
                continue;

            loadedRoomCount++;
            loadedRooms = string.IsNullOrEmpty(loadedRooms) ? scene.name : loadedRooms + ", " + scene.name;
        }

        if (loadedRoomCount == 1)
            return true;

        failure = "Single-slot streaming expected exactly one managed generated room, found " +
                  loadedRoomCount + " [" + loadedRooms + "].";
        return false;
    }

    /// <summary>
    /// Verifies that authored single-slot ownership never applies the dual-slot off-world translation to managed
    /// roots or active ECS roots.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the active exact room instance.</param>
    /// <param name="failure">Diagnostic message when any active room surface remains spatially staged.</param>
    /// <returns>True when the active single-slot room remains entirely in authored coordinates.</returns>
    public static bool ValidateAuthoredRoomPlacement(EntityManager entityManager, out string failure)
    {
        failure = string.Empty;

        EntityQuery configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameProceduralLevelConfig>());

        try
        {
            if (configQuery.CalculateEntityCount() != 1 ||
                configQuery.GetSingleton<GameProceduralLevelConfig>().RoomStreamingMode != GameProceduralRoomStreamingMode.AuthoredSingleSlot)
            {
                return true;
            }
        }
        finally
        {
            configQuery.Dispose();
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] managedRoots = activeScene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < managedRoots.Length; rootIndex++)
        {
            if (managedRoots[rootIndex].transform.position.y > SpatialStagingHeightThreshold)
                continue;

            failure = "Managed room root '" + managedRoots[rootIndex].name +
                      "' remained in the dual-slot staging range at " + managedRoots[rootIndex].transform.position + ".";
            return false;
        }

        EntityQuery rootQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<SceneTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<Parent>()
            },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        try
        {
            using NativeArray<Entity> entityRoots = rootQuery.ToEntityArray(Allocator.Temp);

            for (int rootIndex = 0; rootIndex < entityRoots.Length; rootIndex++)
            {
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entityRoots[rootIndex]);

                if (transform.Position.y > SpatialStagingHeightThreshold)
                    continue;

                failure = "Active ECS room root " + entityRoots[rootIndex] +
                          " remained in the dual-slot staging range at " + transform.Position + ".";
                return false;
            }

            return true;
        }
        finally
        {
            rootQuery.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the graph-selected target portal belongs to the expected side and exactly owns the player's authored arrival.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing active room portals and the persistent player.</param>
    /// <param name="targetPortalId">Exact target portal ID stored on the traversed graph edge.</param>
    /// <param name="targetSide">Target side stored on the traversed graph edge.</param>
    /// <param name="failure">Diagnostic message when portal identity, side or authored arrival alignment is invalid.</param>
    /// <returns>True when one matching target portal arrival coincides with the persistent player position.</returns>
    public static bool ValidateTargetPortalAlignment(EntityManager entityManager,
                                                     string targetPortalId,
                                                     GameRoomPortalSide targetSide,
                                                     out string failure)
    {
        failure = string.Empty;
        EntityQuery portalQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameRoomPortal>(),
                                                                  ComponentType.ReadOnly<SceneTag>());
        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                  ComponentType.ReadOnly<LocalTransform>());

        try
        {
            if (playerQuery.CalculateEntityCount() != 1)
            {
                failure = "Portal-alignment validation requires exactly one persistent player.";
                return false;
            }

            FixedString64Bytes expectedPortalId = new FixedString64Bytes(targetPortalId);
            NativeArray<GameRoomPortal> portals = portalQuery.ToComponentDataArray<GameRoomPortal>(Allocator.Temp);
            int matchCount = 0;
            GameRoomPortal targetPortal = default;

            try
            {
                // Resolve the exact graph-selected portal rather than inferring an entrance from its cardinal side.
                for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
                {
                    if (!portals[portalIndex].PortalId.Equals(expectedPortalId))
                        continue;

                    targetPortal = portals[portalIndex];
                    matchCount++;
                }
            }
            finally
            {
                portals.Dispose();
            }

            if (matchCount != 1)
            {
                failure = "Expected one active target portal '" + targetPortalId + "', found " + matchCount + ".";
                return false;
            }

            if (targetPortal.Side != targetSide)
            {
                failure = "Target portal '" + targetPortalId + "' has side " + targetPortal.Side +
                          " instead of generated edge side " + targetSide + ".";
                return false;
            }

            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(playerQuery.GetSingletonEntity());
            float alignmentDelta = Vector3.Distance(playerTransform.Position, targetPortal.ArrivalPosition);

            if (alignmentDelta <= PortalAlignmentTolerance)
                return true;

            failure = "Target portal '" + targetPortalId + "' arrival differs from the relocated player position by " +
                      alignmentDelta.ToString("0.######") + " units.";
            return false;
        }
        finally
        {
            portalQuery.Dispose();
            playerQuery.Dispose();
        }
    }

    /// <summary>
    /// Verifies traversal retained the exact camera object, its player-relative framing and a visible player viewport position.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the relocated persistent player transform.</param>
    /// <param name="failure">Diagnostic message describing camera identity, framing or viewport regression.</param>
    /// <returns>True when the persistent camera remained seamless across the room transition.</returns>
    public static bool Validate(EntityManager entityManager, out string failure)
    {
        if (!TryCapture(entityManager,
                        out Camera gameplayCamera,
                        out Vector3 targetCameraOffset,
                        out Vector3 targetPlayerPosition,
                        out Quaternion targetPlayerRotation,
                        out failure))
        {
            return false;
        }

        if (gameplayCamera.GetInstanceID() != SessionState.GetInt(SourceCameraInstanceKey, 0))
        {
            failure = "Room traversal replaced the persistent gameplay camera instance.";
            return false;
        }

        Vector3 sourceCameraOffset = new Vector3(SessionState.GetFloat(SourceCameraOffsetXKey, 0f),
                                                 SessionState.GetFloat(SourceCameraOffsetYKey, 0f),
                                                 SessionState.GetFloat(SourceCameraOffsetZKey, 0f));
        float offsetDelta = Vector3.Distance(sourceCameraOffset, targetCameraOffset);

        if (offsetDelta > CameraOffsetTolerance)
        {
            failure = "Room traversal changed the persistent camera player-relative offset by " +
                      offsetDelta.ToString("0.###") + " units. Source=" + sourceCameraOffset +
                      ", target=" + targetCameraOffset + ".";
            return false;
        }

        if (UsesSpatiallyAlignedStreaming(entityManager))
        {
            Vector3 sourcePlayerPosition = new Vector3(SessionState.GetFloat(SourcePlayerPositionXKey, 0f),
                                                       SessionState.GetFloat(SourcePlayerPositionYKey, 0f),
                                                       SessionState.GetFloat(SourcePlayerPositionZKey, 0f));
            float positionDelta = Vector3.Distance(sourcePlayerPosition, targetPlayerPosition);

            if (positionDelta > PlayerPositionTolerance)
            {
                failure = "Spatial dual-slot traversal displaced the persistent player position by " +
                          positionDelta.ToString("0.######") + " units. Source=" + sourcePlayerPosition +
                          ", target=" + targetPlayerPosition + ".";
                return false;
            }

            Quaternion sourcePlayerRotation = new Quaternion(SessionState.GetFloat(SourcePlayerRotationXKey, 0f),
                                                             SessionState.GetFloat(SourcePlayerRotationYKey, 0f),
                                                             SessionState.GetFloat(SourcePlayerRotationZKey, 0f),
                                                             SessionState.GetFloat(SourcePlayerRotationWKey, 1f));
            float rotationDelta = Quaternion.Angle(sourcePlayerRotation, targetPlayerRotation);

            if (rotationDelta > PlayerRotationToleranceDegrees)
            {
                failure = "Spatial dual-slot traversal displaced the persistent player rotation by " +
                          rotationDelta.ToString("0.######") + " degrees. Source=" + sourcePlayerRotation.eulerAngles +
                          ", target=" + targetPlayerRotation.eulerAngles + ".";
                return false;
            }
        }

        Vector3 playerPosition = gameplayCamera.transform.position - targetCameraOffset;
        Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(playerPosition);
        bool playerIsVisible = viewportPosition.z > 0f &&
                               viewportPosition.x >= -ViewportTolerance &&
                               viewportPosition.x <= 1f + ViewportTolerance &&
                               viewportPosition.y >= -ViewportTolerance &&
                               viewportPosition.y <= 1f + ViewportTolerance;

        if (playerIsVisible)
            return true;

        failure = "The relocated player is outside the persistent camera viewport at " + viewportPosition + ".";
        return false;
    }
    #endregion

    #region Capture Methods
    /// <summary>
    /// Resolves whether the active procedural configuration intentionally preserves world-space player pose.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the procedural level configuration.</param>
    /// <returns>True only for the optional spatial dual-slot mode.</returns>
    private static bool UsesSpatiallyAlignedStreaming(EntityManager entityManager)
    {
        EntityQuery configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameProceduralLevelConfig>());

        try
        {
            return configQuery.CalculateEntityCount() == 1 &&
                   configQuery.GetSingleton<GameProceduralLevelConfig>().RoomStreamingMode ==
                   GameProceduralRoomStreamingMode.TransactionalDualSlot;
        }
        finally
        {
            configQuery.Dispose();
        }
    }

    /// <summary>
    /// Resolves the active persistent gameplay camera and computes its offset from the unique player ECS transform.
    /// </summary>
    /// <param name="entityManager">Default-world entity manager containing the player transform.</param>
    /// <param name="gameplayCamera">Resolved persistent gameplay camera.</param>
    /// <param name="cameraOffset">Resolved player-relative gameplay camera offset.</param>
    /// <param name="playerPosition">Resolved persistent-player local position used by the no-relocation assertion.</param>
    /// <param name="playerRotation">Resolved persistent-player local rotation used by the no-relocation assertion.</param>
    /// <param name="failure">Diagnostic message when a unique player or persistent camera cannot be resolved.</param>
    /// <returns>True when both camera and player transform were available.</returns>
    private static bool TryCapture(EntityManager entityManager,
                                   out Camera gameplayCamera,
                                   out Vector3 cameraOffset,
                                   out Vector3 playerPosition,
                                   out Quaternion playerRotation,
                                   out string failure)
    {
        gameplayCamera = Camera.main;
        cameraOffset = Vector3.zero;
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        failure = string.Empty;

        if (gameplayCamera == null || !GameSceneBootstrapCameraView.IsPersistentGameplayCamera(gameplayCamera))
        {
            failure = "The persistent gameplay MainCamera was not available for camera-continuity validation.";
            return false;
        }

        EntityQuery playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                                                  ComponentType.ReadOnly<LocalToWorld>(),
                                                                  ComponentType.ReadOnly<LocalTransform>());

        try
        {
            if (playerQuery.CalculateEntityCount() != 1)
            {
                failure = "Camera-continuity validation requires exactly one persistent player LocalToWorld.";
                return false;
            }

            LocalToWorld playerTransform = entityManager.GetComponentData<LocalToWorld>(playerQuery.GetSingletonEntity());
            LocalTransform playerLocalTransform = entityManager.GetComponentData<LocalTransform>(playerQuery.GetSingletonEntity());
            cameraOffset = gameplayCamera.transform.position - (Vector3)playerTransform.Position;
            playerPosition = playerLocalTransform.Position;
            playerRotation = playerLocalTransform.Rotation;
            return true;
        }
        finally
        {
            playerQuery.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
