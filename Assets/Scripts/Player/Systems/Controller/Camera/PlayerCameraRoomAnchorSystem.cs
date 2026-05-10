using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerCameraRoomAnchorSystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCameraAnchor>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool isSceneTransitioning = GameSceneTransitionRuntimeGuardUtility.IsDefaultWorldTransitioning();

        if (PlayerGameplayPauseUtility.IsTimeScaleHardPaused() && !isSceneTransitioning)
            return;

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera))
            return;

        float deltaTime = ResolvePresentationDeltaTime(SystemAPI.Time.DeltaTime, isSceneTransitioning);
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach ((RefRO<PlayerCameraAnchor> cameraAnchor, RefRO<PlayerRuntimeCameraConfig> runtimeCameraConfig) in SystemAPI.Query<RefRO<PlayerCameraAnchor>, RefRO<PlayerRuntimeCameraConfig>>())
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

            float3 newPosition = PlayerControllerMath.SmoothCameraPosition(camera.transform.position, anchorPosition, cameraConfig.Values, deltaTime);
            camera.transform.position = newPosition;
            break;
        }
    }

    #endregion

    #region Helpers
    private static float ResolvePresentationDeltaTime(float scaledDeltaTime, bool isSceneTransitioning)
    {
        if (!isSceneTransitioning || scaledDeltaTime > 0f)
            return scaledDeltaTime;

        return Time.unscaledDeltaTime;
    }
    #endregion

}
