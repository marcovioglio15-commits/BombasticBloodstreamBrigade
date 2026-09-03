using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cycles scene-authored recorder viewpoints from a configurable Input Action and redirects the persistent gameplay
/// camera to the selected static pivot while rotating it toward the authoritative player.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerRecorderCameraSystem : ISystem
{
    #region Constants
    private const float MinimumProjectionValue = 0.0001f;
    #endregion

    #region Fields
    private Entity runtimeStateEntity;
    private EntityQuery recorderCameraQuery;
    private RecorderCameraRenderSnapshot gameplayCameraSnapshot;
    private Entity lastAppliedRecorderEntity;
    private int snapshotCameraInstanceId;
    private bool hasGameplayCameraSnapshot;
    #endregion

    #region Methods

    #region Lifecycle Methods
    /// <summary>
    /// Creates the singleton selection state and caches the recorder-camera query used only when cycling is requested.
    /// </summary>
    /// <param name="state">System state used to create runtime state and the immutable viewpoint query.</param>
    public void OnCreate(ref SystemState state)
    {
        runtimeStateEntity = state.EntityManager.CreateEntity(typeof(GameRecorderCameraRuntimeState));
        recorderCameraQuery = state.GetEntityQuery(ComponentType.ReadOnly<GameRecorderCamera>());
    }

    /// <summary>
    /// Restores the gameplay camera if the world is disposed while a recorder viewpoint still owns presentation.
    /// </summary>
    /// <param name="state">System state whose presentation world is shutting down.</param>
    public void OnDestroy(ref SystemState state)
    {
        RestoreGameplayCamera();
    }

    /// <summary>
    /// Processes edge-triggered cycling, restores invalid selections and applies the selected static recorder pose.
    /// </summary>
    /// <param name="state">Current ECS state providing recorder viewpoints and the authoritative player transform.</param>
    public void OnUpdate(ref SystemState state)
    {
        GameRecorderCameraRuntimeState recorderState =
            state.EntityManager.GetComponentData<GameRecorderCameraRuntimeState>(runtimeStateEntity);
        bool selectionChanged = ClearInvalidSelection(ref recorderState, state.EntityManager);

        // Allocate and inspect the recorder list only on an accepted cheat edge.
        if (ShouldCycleRecorderCamera())
            selectionChanged |= CycleSelection(ref recorderState);

        if (selectionChanged)
            state.EntityManager.SetComponentData(runtimeStateEntity, recorderState);

        // Leaving recorder mode restores the clean gameplay pose before normal camera-follow systems execute.
        if (recorderState.ActiveCameraEntity == Entity.Null)
        {
            RestoreGameplayCamera();
            return;
        }

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera gameplayCamera))
            return;

        GameRecorderCamera recorderCamera =
            state.EntityManager.GetComponentData<GameRecorderCamera>(recorderState.ActiveCameraEntity);
        int gameplayCameraInstanceId = gameplayCamera.GetInstanceID();
        bool gameplayCameraChanged = !hasGameplayCameraSnapshot ||
                                     gameplayCameraInstanceId != snapshotCameraInstanceId;
        bool viewpointChanged = recorderState.ActiveCameraEntity != lastAppliedRecorderEntity;

        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerCameraShakeState> shakeStateLookup =
            SystemAPI.GetComponentLookup<PlayerCameraShakeState>(false);
        bool hasFastPlayPlayer = SystemAPI.TryGetSingletonEntity<GameCameraBoundaryFastPlayPlayer>(
            out Entity fastPlayPlayerEntity);
        float3 targetPosition = recorderCamera.WorldPosition + recorderCamera.WorldForward;
        PlayerCameraShakeState shakeState = default;
        Entity playerEntity = Entity.Null;

        // Resolve the same local player target used by the standard camera stack and capture its current shake output.
        foreach ((RefRO<LocalTransform> localTransform, Entity entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                          .WithAll<PlayerRuntimeCameraConfig>()
                          .WithEntityAccess())
        {
            if (hasFastPlayPlayer && entity != fastPlayPlayerEntity)
                continue;

            playerEntity = entity;
            targetPosition = localToWorldLookup.HasComponent(entity)
                ? localToWorldLookup[entity].Position
                : localTransform.ValueRO.Position;

            if (shakeStateLookup.HasComponent(entity))
                shakeState = shakeStateLookup[entity];

            break;
        }

        if (gameplayCameraChanged)
        {
            gameplayCameraSnapshot = RecorderCameraRenderSnapshot.Capture(gameplayCamera, in shakeState);
            snapshotCameraInstanceId = gameplayCameraInstanceId;
            hasGameplayCameraSnapshot = true;
        }

        // Recorder mode intentionally suppresses positional, roll and zoom output while trauma continues to evolve.
        if (playerEntity != Entity.Null && shakeStateLookup.HasComponent(playerEntity))
        {
            PlayerCameraShakePresentationUtility.ClearOutput(ref shakeState);
            shakeStateLookup[playerEntity] = shakeState;
        }

        if (gameplayCameraChanged || viewpointChanged)
            ApplyProjectionToCameraStack(gameplayCamera, in recorderCamera);

        quaternion lookRotation = GameRecorderCameraUtility.ResolveLookRotation(in recorderCamera, targetPosition);
        gameplayCamera.transform.SetPositionAndRotation(recorderCamera.WorldPosition, lookRotation);
        lastAppliedRecorderEntity = recorderState.ActiveCameraEntity;

        // Complete the existing transition handshake only after recorder framing has reached the destination frame.
        if (SystemAPI.TryGetSingleton(out GameSceneTransitionState transitionState) &&
            GameSceneTransitionCameraReadinessUtility.IsPreparationPending(in transitionState))
        {
            RefRW<GameSceneTransitionState> transitionStateReference =
                SystemAPI.GetSingletonRW<GameSceneTransitionState>();
            GameSceneTransitionCameraReadinessUtility.MarkPrepared(ref transitionStateReference.ValueRW);
        }
    }
    #endregion

    #region Selection Methods
    /// <summary>
    /// Clears a recorder selection whose owning scene or SubScene has unloaded.
    /// </summary>
    /// <param name="recorderState">Mutable recorder-camera singleton state.</param>
    /// <param name="entityManager">Entity manager used to validate the selected viewpoint.</param>
    /// <returns>True when an invalid active selection was cleared.</returns>
    private static bool ClearInvalidSelection(ref GameRecorderCameraRuntimeState recorderState,
                                              EntityManager entityManager)
    {
        Entity activeCameraEntity = recorderState.ActiveCameraEntity;

        if (activeCameraEntity == Entity.Null)
            return false;

        if (entityManager.Exists(activeCameraEntity) &&
            entityManager.HasComponent<GameRecorderCamera>(activeCameraEntity))
            return false;

        recorderState.ActiveCameraEntity = Entity.Null;
        return true;
    }

    /// <summary>
    /// Advances to the next ordered recorder viewpoint, or returns control to the gameplay camera after the last one.
    /// </summary>
    /// <param name="recorderState">Mutable recorder-camera singleton receiving the next selection.</param>
    /// <returns>True when the active selection changed.</returns>
    private bool CycleSelection(ref GameRecorderCameraRuntimeState recorderState)
    {
        NativeArray<Entity> entities = recorderCameraQuery.ToEntityArray(Allocator.Temp);
        NativeArray<GameRecorderCamera> cameras =
            recorderCameraQuery.ToComponentDataArray<GameRecorderCamera>(Allocator.Temp);

        try
        {
            Entity previousEntity = recorderState.ActiveCameraEntity;

            if (GameRecorderCameraUtility.TryResolveNext(entities,
                                                         cameras,
                                                         previousEntity,
                                                         out Entity nextEntity))
                recorderState.ActiveCameraEntity = nextEntity;
            else
                recorderState.ActiveCameraEntity = Entity.Null;

            return recorderState.ActiveCameraEntity != previousEntity;
        }
        finally
        {
            cameras.Dispose();
            entities.Dispose();
        }
    }

    /// <summary>
    /// Checks the configured recorder-camera action without polling individual keyboard or gamepad controls.
    /// </summary>
    /// <returns>True on the enabled action's pressed edge while hard gameplay pause is inactive.</returns>
    private static bool ShouldCycleRecorderCamera()
    {
        if (PlayerGameplayPauseUtility.IsHardGameplayPauseActive())
            return false;

        InputAction cycleAction = PlayerInputRuntime.RecorderCameraCycleAction;

        if (cycleAction == null || !cycleAction.enabled)
            return false;

        return cycleAction.WasPressedThisFrame();
    }
    #endregion

    #region Presentation Methods
    /// <summary>
    /// Restores the pre-cheat gameplay pose and projection, then clears transient ownership state.
    /// </summary>
    private void RestoreGameplayCamera()
    {
        if (!hasGameplayCameraSnapshot)
            return;

        if (PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera gameplayCamera) &&
            gameplayCamera.GetInstanceID() == snapshotCameraInstanceId)
            gameplayCameraSnapshot.Apply(gameplayCamera);

        gameplayCameraSnapshot = default;
        lastAppliedRecorderEntity = Entity.Null;
        snapshotCameraInstanceId = 0;
        hasGameplayCameraSnapshot = false;
    }

    /// <summary>
    /// Copies recorder projection values to the base camera and every live URP overlay in its stack.
    /// </summary>
    /// <param name="gameplayCamera">Persistent base camera that remains the render owner.</param>
    /// <param name="recorderCamera">Selected recorder viewpoint supplying projection values.</param>
    private static void ApplyProjectionToCameraStack(Camera gameplayCamera, in GameRecorderCamera recorderCamera)
    {
        ApplyProjection(gameplayCamera,
                        recorderCamera.Orthographic != 0,
                        recorderCamera.FieldOfView,
                        recorderCamera.OrthographicSize,
                        recorderCamera.NearClipPlane,
                        recorderCamera.FarClipPlane);

        UniversalAdditionalCameraData cameraData = gameplayCamera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null || cameraData.renderType != CameraRenderType.Base)
            return;

        List<Camera> cameraStack = cameraData.cameraStack;

        // Keep gameplay, transition and UI overlays projection-compatible with the persistent base camera.
        for (int cameraIndex = 0; cameraIndex < cameraStack.Count; cameraIndex++)
        {
            Camera overlayCamera = cameraStack[cameraIndex];

            if (overlayCamera == null)
                continue;

            ApplyProjection(overlayCamera,
                            recorderCamera.Orthographic != 0,
                            recorderCamera.FieldOfView,
                            recorderCamera.OrthographicSize,
                            recorderCamera.NearClipPlane,
                            recorderCamera.FarClipPlane);
        }
    }

    /// <summary>
    /// Applies one validated projection payload without changing culling, render type, depth or output routing.
    /// </summary>
    /// <param name="cameraComponent">Camera receiving projection values.</param>
    /// <param name="orthographic">Whether the resulting camera uses orthographic projection.</param>
    /// <param name="fieldOfView">Perspective vertical field of view in degrees.</param>
    /// <param name="orthographicSize">Orthographic half-height in world units.</param>
    /// <param name="nearClipPlane">Near clipping distance.</param>
    /// <param name="farClipPlane">Far clipping distance.</param>
    private static void ApplyProjection(Camera cameraComponent,
                                        bool orthographic,
                                        float fieldOfView,
                                        float orthographicSize,
                                        float nearClipPlane,
                                        float farClipPlane)
    {
        if (cameraComponent == null)
            return;

        cameraComponent.orthographic = orthographic;
        cameraComponent.fieldOfView = math.max(MinimumProjectionValue, fieldOfView);
        cameraComponent.orthographicSize = math.max(MinimumProjectionValue, orthographicSize);
        cameraComponent.nearClipPlane = math.max(MinimumProjectionValue, nearClipPlane);
        cameraComponent.farClipPlane = math.max(cameraComponent.nearClipPlane + MinimumProjectionValue,
                                                farClipPlane);
    }

    #endregion

    #endregion

    #region Types
    /// <summary>
    /// Stores the clean gameplay pose and projection that must be restored when recorder mode ends.
    /// </summary>
    private struct RecorderCameraRenderSnapshot
    {
        #region Fields
        private float3 position;
        private quaternion rotation;
        private float fieldOfView;
        private float orthographicSize;
        private float nearClipPlane;
        private float farClipPlane;
        private byte orthographic;
        #endregion

        #region Methods
        /// <summary>
        /// Captures the gameplay camera after removing the shake output currently layered onto its base pose.
        /// </summary>
        /// <param name="cameraComponent">Persistent gameplay camera being redirected.</param>
        /// <param name="shakeState">Player shake state whose applied output must not leak into the restored pose.</param>
        /// <returns>A self-contained clean camera snapshot.</returns>
        public static RecorderCameraRenderSnapshot Capture(Camera cameraComponent,
                                                           in PlayerCameraShakeState shakeState)
        {
            quaternion cleanRotation = math.mul((quaternion)cameraComponent.transform.rotation,
                                                math.inverse(quaternion.RotateZ(shakeState.RollRadians)));
            return new RecorderCameraRenderSnapshot
            {
                position = (float3)cameraComponent.transform.position - shakeState.PositionOffset,
                rotation = cleanRotation,
                fieldOfView = cameraComponent.fieldOfView - shakeState.FovDelta,
                orthographicSize = cameraComponent.orthographicSize,
                nearClipPlane = cameraComponent.nearClipPlane,
                farClipPlane = cameraComponent.farClipPlane,
                orthographic = cameraComponent.orthographic ? (byte)1 : (byte)0
            };
        }

        /// <summary>
        /// Restores the clean pose and mirrors its projection to the current URP overlay stack.
        /// </summary>
        /// <param name="cameraComponent">Persistent gameplay camera returning to normal follow ownership.</param>
        public void Apply(Camera cameraComponent)
        {
            if (cameraComponent == null)
                return;

            cameraComponent.transform.SetPositionAndRotation(position, rotation);
            GameRecorderCamera restoredProjection = new GameRecorderCamera
            {
                Orthographic = orthographic,
                FieldOfView = fieldOfView,
                OrthographicSize = orthographicSize,
                NearClipPlane = nearClipPlane,
                FarClipPlane = farClipPlane
            };
            ApplyProjectionToCameraStack(cameraComponent, in restoredProjection);
        }
        #endregion
    }
    #endregion
}

/// <summary>
/// Provides deterministic recorder-camera ordering and stable look-rotation math shared by runtime and smoke tests.
/// </summary>
public static class GameRecorderCameraUtility
{
    #region Constants
    private const float ParallelDirectionThreshold = 0.999f;
    #endregion

    #region Methods

    #region Selection Methods
    /// <summary>
    /// Resolves the next ordered viewpoint. Passing Entity.Null selects the first; reaching the last returns false so
    /// the caller can hand control back to the normal gameplay camera before the following cycle starts again.
    /// </summary>
    /// <param name="entities">Entities aligned by index with <paramref name="cameras"/>.</param>
    /// <param name="cameras">Immutable recorder-camera configurations to order.</param>
    /// <param name="activeEntity">Currently selected viewpoint, or Entity.Null while gameplay camera control is active.</param>
    /// <param name="nextEntity">Next ordered recorder entity when one exists.</param>
    /// <returns>True when a next recorder viewpoint exists; false when cycling should return to gameplay control.</returns>
    public static bool TryResolveNext(NativeArray<Entity> entities,
                                      NativeArray<GameRecorderCamera> cameras,
                                      Entity activeEntity,
                                      out Entity nextEntity)
    {
        nextEntity = Entity.Null;

        if (entities.Length == 0 || entities.Length != cameras.Length)
            return false;

        bool hasActiveCamera = TryResolveCamera(entities, cameras, activeEntity, out GameRecorderCamera activeCamera);
        bool hasCandidate = false;
        GameRecorderCamera selectedCamera = default;

        // Find the smallest key after the active key, or the global minimum while gameplay control is active.
        for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
        {
            Entity candidateEntity = entities[cameraIndex];
            GameRecorderCamera candidateCamera = cameras[cameraIndex];

            if (hasActiveCamera &&
                Compare(candidateCamera, candidateEntity, activeCamera, activeEntity) <= 0)
                continue;

            if (hasCandidate &&
                Compare(candidateCamera, candidateEntity, selectedCamera, nextEntity) >= 0)
                continue;

            nextEntity = candidateEntity;
            selectedCamera = candidateCamera;
            hasCandidate = true;
        }

        return hasCandidate;
    }
    #endregion

    #region Rotation Methods
    /// <summary>
    /// Builds a stable look rotation toward the player while preserving the authored up direction whenever possible.
    /// </summary>
    /// <param name="recorderCamera">Selected recorder viewpoint supplying pivot and fallback axes.</param>
    /// <param name="targetPosition">Authoritative player world position.</param>
    /// <returns>World rotation that points the camera at the player without producing a singular up vector.</returns>
    public static quaternion ResolveLookRotation(in GameRecorderCamera recorderCamera, float3 targetPosition)
    {
        float3 direction = math.normalizesafe(targetPosition - recorderCamera.WorldPosition,
                                              recorderCamera.WorldForward);
        float3 up = math.normalizesafe(recorderCamera.WorldUp, math.up());

        if (math.abs(math.dot(direction, up)) >= ParallelDirectionThreshold)
            up = math.abs(direction.y) < ParallelDirectionThreshold ? math.up() : math.forward();

        return quaternion.LookRotationSafe(direction, up);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Resolves the configuration aligned with one entity in the query snapshots.
    /// </summary>
    /// <param name="entities">Entity snapshot aligned with <paramref name="cameras"/>.</param>
    /// <param name="cameras">Recorder configuration snapshot.</param>
    /// <param name="targetEntity">Entity whose configuration is requested.</param>
    /// <param name="camera">Resolved configuration when the entity is present.</param>
    /// <returns>True when the entity exists in the aligned snapshots.</returns>
    private static bool TryResolveCamera(NativeArray<Entity> entities,
                                         NativeArray<GameRecorderCamera> cameras,
                                         Entity targetEntity,
                                         out GameRecorderCamera camera)
    {
        camera = default;

        if (targetEntity == Entity.Null)
            return false;

        for (int cameraIndex = 0; cameraIndex < entities.Length; cameraIndex++)
        {
            if (entities[cameraIndex] != targetEntity)
                continue;

            camera = cameras[cameraIndex];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Compares two recorder keys by authored order, then ECS index and version for a total deterministic order.
    /// </summary>
    /// <param name="leftCamera">Left recorder configuration.</param>
    /// <param name="leftEntity">Entity owning the left configuration.</param>
    /// <param name="rightCamera">Right recorder configuration.</param>
    /// <param name="rightEntity">Entity owning the right configuration.</param>
    /// <returns>A negative value when left sorts first, zero for identity, or a positive value when right sorts first.</returns>
    private static int Compare(GameRecorderCamera leftCamera,
                               Entity leftEntity,
                               GameRecorderCamera rightCamera,
                               Entity rightEntity)
    {
        int orderComparison = leftCamera.CycleOrder.CompareTo(rightCamera.CycleOrder);

        if (orderComparison != 0)
            return orderComparison;

        int indexComparison = leftEntity.Index.CompareTo(rightEntity.Index);

        if (indexComparison != 0)
            return indexComparison;

        return leftEntity.Version.CompareTo(rightEntity.Version);
    }
    #endregion

    #endregion
}
