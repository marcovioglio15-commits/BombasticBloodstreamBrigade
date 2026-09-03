using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Smooths the persistent gameplay camera toward the active room anchor when the player preset selects room-fixed framing.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerCameraRoomAnchorSystem : ISystem
{
    #region Fields
    private float3 anchorFollowVelocity;
    private Entity lastAnchorEntity;
    private Entity lastPlayerEntity;
    private int lastCameraInstanceId;
    private bool hasTrackedTarget;
    private bool wasRoomFixedActive;
    private EntityQuery runOutcomeQuery;
    #endregion

    #region Methods

    #region Lifecycle Methods
    /// <summary>
    /// Requires room-anchor and runtime camera data, then caches the run-outcome query used by presentation pause policy.
    /// </summary>
    /// <param name="state">System state used to register required ECS components.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCameraAnchor>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        runOutcomeQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                               ComponentType.ReadOnly<PlayerRunOutcomeState>());
    }

    /// <summary>
    /// Applies the active room anchor through the configured follow spring while respecting transition continuity,
    /// finalized outcomes, hard pause policy and camera shake ownership from <see cref="PlayerCameraFollowSystem"/>.
    /// </summary>
    /// <param name="state">System state providing camera-anchor and player feedback components.</param>
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<GameCameraBoundaryFastPlayPlayer>())
            return;

        if (SystemAPI.TryGetSingleton(out GameRecorderCameraRuntimeState recorderCameraState) &&
            recorderCameraState.ActiveCameraEntity != Entity.Null)
            return;

        // Resolve transition presentation once so room-fixed framing can close the same reveal handshake.
        bool hasTransitionState = SystemAPI.TryGetSingleton(out GameSceneTransitionState transitionState);
        bool isSceneTransitioning = hasTransitionState && transitionState.IsTransitioning != 0;
        bool isRevealPreparationPending = hasTransitionState &&
                                           GameSceneTransitionCameraReadinessUtility.IsPreparationPending(
                                               in transitionState);

        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery) &&
            !isRevealPreparationPending)
            return;

        // Dying bypasses the hard-pause gate: room-fixed cameras must still receive the shake offset and roll while the
        // freeze system pins gameplay time to zero on the lethal hit.
        bool isDying = PlayerGameplayPauseUtility.IsDyingRunOutcomeActive(runOutcomeQuery);

        if (PlayerGameplayPauseUtility.IsTimeScaleHardPaused() && !isSceneTransitioning && !isDying)
            return;

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera))
            return;

        if (PlayerCameraTraversalFramingUtility.Owns(camera))
            return;

        float deltaTime = PlayerGameplayPauseUtility.ResolveFeedbackDeltaTime(SystemAPI.Time.DeltaTime,
                                                                              runOutcomeQuery,
                                                                              isSceneTransitioning);
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerCameraShakeState> shakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(true);
        bool hasBoundaryRuntimeState =
            SystemAPI.TryGetSingleton(out GameCameraBoundaryRuntimeState cameraBoundaryState);
        DynamicBuffer<GameCameraBoundaryContainmentElement> containmentBoundaries = default;
        bool hasContainmentBuffer =
            SystemAPI.TryGetSingletonBuffer<GameCameraBoundaryContainmentElement>(out containmentBoundaries, true);
        bool hasCameraBoundary = hasBoundaryRuntimeState &&
                                 cameraBoundaryState.Enabled != 0 &&
                                 cameraBoundaryState.Mode == GameCameraBoundaryMode.ContainmentVolume &&
                                 cameraBoundaryState.HasBoundary != 0 &&
                                 hasContainmentBuffer &&
                                 containmentBoundaries.Length > 0;
        bool appliedRoomFixed = false;

        foreach ((RefRO<PlayerCameraAnchor> cameraAnchor, RefRO<PlayerRuntimeCameraConfig> runtimeCameraConfig, Entity entity) in SystemAPI.Query<RefRO<PlayerCameraAnchor>, RefRO<PlayerRuntimeCameraConfig>>().WithEntityAccess())
        {
            PlayerRuntimeCameraConfig cameraConfig = runtimeCameraConfig.ValueRO;

            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.RoomFixed:
                    break;
                default:
                    continue;
            }

            Entity anchorEntity = cameraAnchor.ValueRO.AnchorEntity;

            if (!state.EntityManager.Exists(anchorEntity))
                continue;

            float3 anchorPosition;

            if (localToWorldLookup.HasComponent(anchorEntity))
                anchorPosition = localToWorldLookup[anchorEntity].Position;
            else if (localTransformLookup.HasComponent(anchorEntity))
                anchorPosition = localTransformLookup[anchorEntity].Position;
            else
                continue;

            if (hasCameraBoundary)
            {
                anchorPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(
                    containmentBoundaries,
                    anchorPosition,
                    cameraBoundaryState.SoftZoneDistance);
            }

            appliedRoomFixed = true;

            // Apply the damage shake already evolved this frame by PlayerCameraFollowSystem (the single trauma owner).
            // Removing the previously applied offset before smoothing keeps the shake from feeding the follow spring.
            PlayerCameraShakeState shakeState = shakeStateLookup.HasComponent(entity) ? shakeStateLookup[entity] : default;
            float3 smoothingSource =
                PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position, in shakeState);
            bool isFirstTarget = !hasTrackedTarget;
            bool targetChanged = !hasTrackedTarget ||
                                 !wasRoomFixedActive ||
                                 entity != lastPlayerEntity ||
                                 anchorEntity != lastAnchorEntity ||
                                 camera.GetInstanceID() != lastCameraInstanceId;

            if (targetChanged)
            {
                anchorFollowVelocity = float3.zero;
                lastPlayerEntity = entity;
                lastAnchorEntity = anchorEntity;
                lastCameraInstanceId = camera.GetInstanceID();
                hasTrackedTarget = true;
                wasRoomFixedActive = true;
            }

            // Initial valid targets and hidden destination containment can be applied without exposing a camera jump.
            bool applyInitialTarget = targetChanged &&
                                      isFirstTarget &&
                                      (!hasCameraBoundary ||
                                       GameCameraBoundaryUtility.Contains(containmentBoundaries, smoothingSource));
            bool applyPreparedTarget = isRevealPreparationPending && hasCameraBoundary;

            if (applyInitialTarget || applyPreparedTarget)
            {
                anchorFollowVelocity = float3.zero;
                PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform,
                                                              anchorPosition,
                                                              in shakeState,
                                                              false,
                                                              quaternion.identity);
                break;
            }

            float3 newPosition = PlayerControllerMath.SmoothCameraPosition(smoothingSource, anchorPosition, cameraConfig.Values, ref anchorFollowVelocity, deltaTime);

            if (hasCameraBoundary)
            {
                GameCameraBoundaryUtility.ApplyReachableHardConstraint(
                    containmentBoundaries,
                    smoothingSource,
                    ref newPosition,
                    ref anchorFollowVelocity);
            }

            PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform, newPosition, in shakeState, false, quaternion.identity);
            // FOV is intentionally not applied here: PlayerCameraFollowSystem (the single trauma owner) already wrote it
            // once per frame, so re-applying would double-count the delta against PreviousAppliedFovDelta.
            break;
        }

        // Room-fixed behavior owns the acknowledgment, including safe fallback when no valid anchor was resolved.
        if (isRevealPreparationPending)
        {
            RefRW<GameSceneTransitionState> transitionStateReference =
                SystemAPI.GetSingletonRW<GameSceneTransitionState>();
            GameSceneTransitionCameraReadinessUtility.MarkPrepared(ref transitionStateReference.ValueRW);
        }

        wasRoomFixedActive = appliedRoomFixed;
    }

    #endregion

    #endregion

}
