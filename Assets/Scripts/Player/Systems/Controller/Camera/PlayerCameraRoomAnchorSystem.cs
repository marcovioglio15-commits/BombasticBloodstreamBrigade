using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerCameraRoomAnchorSystem : ISystem
{
    #region Fields
    private float3 anchorFollowVelocity;
    private EntityQuery runOutcomeQuery;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCameraAnchor>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        runOutcomeQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                               ComponentType.ReadOnly<PlayerRunOutcomeState>());
    }

    public void OnUpdate(ref SystemState state)
    {
        bool isSceneTransitioning = GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning();

        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery))
            return;

        // Dying bypasses the hard-pause gate: room-fixed cameras must still receive the shake offset and roll while the
        // freeze system pins gameplay time to zero on the lethal hit.
        bool isDying = PlayerGameplayPauseUtility.IsDyingRunOutcomeActive(runOutcomeQuery);

        if (PlayerGameplayPauseUtility.IsTimeScaleHardPaused() && !isSceneTransitioning && !isDying)
            return;

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera))
            return;

        float deltaTime = PlayerGameplayPauseUtility.ResolveFeedbackDeltaTime(SystemAPI.Time.DeltaTime,
                                                                              runOutcomeQuery,
                                                                              isSceneTransitioning);
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerCameraShakeState> shakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(true);

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

            // Apply the damage shake already evolved this frame by PlayerCameraFollowSystem (the single trauma owner).
            // Removing the previously applied offset before smoothing keeps the shake from feeding the follow spring.
            PlayerCameraShakeState shakeState = shakeStateLookup.HasComponent(entity) ? shakeStateLookup[entity] : default;
            float3 smoothingSource = PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position, in shakeState);
            float3 newPosition = PlayerControllerMath.SmoothCameraPosition(smoothingSource, anchorPosition, cameraConfig.Values, ref anchorFollowVelocity, deltaTime);
            PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform, newPosition, in shakeState, false, quaternion.identity);
            // FOV is intentionally not applied here: PlayerCameraFollowSystem (the single trauma owner) already wrote it
            // once per frame, so re-applying would double-count the delta against PreviousAppliedFovDelta.
            break;
        }
    }

    #endregion

}
