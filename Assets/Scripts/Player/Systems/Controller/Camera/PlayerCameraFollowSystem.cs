using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// This system is responsible for updating the main camera's position to follow the 
/// player based on the configuration specified in the PlayerControllerConfig component.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PlayerCameraFollowSystem : ISystem
{
    #region Fields
    private bool hasAutoOffset;
    private float3 autoOffset;
    private bool hasChildOffset;
    private float3 childLocalOffset;
    private CameraBehavior lastBehavior;
    private int lastCameraInstanceId;
    #endregion

    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates 
    /// for entities that have the PlayerControllerConfig component, which contains
    /// a reference to the camera configuration that determines how the camera should follow the player.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
    }

    /// <summary>
    /// Updates the main camera's position based on the player's position and the specified camera behavior 
    /// in the PlayerControllerConfig. In detail it calculates the desired camera position using 
    /// the player's position and the configured follow offset,
    /// then smoothly moves the camera towards that position using the SmoothCameraPosition method from 
    /// PlayerControllerMath. It also handles different camera behaviors, such as maintaining
    /// a fixed offset or being a child of the player, 
    /// and ensures that the camera's position is updated accordingly.
    /// </summary>
    /// <param name="state"></param>
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
        int cameraInstanceId = camera.GetInstanceID();

        if (cameraInstanceId != lastCameraInstanceId)
        {
            ResetCachedOffsets();
            lastCameraInstanceId = cameraInstanceId;
        }

        // Only support one player camera config at a time, so breaks after the first iteration.
        foreach ((RefRO<LocalTransform> localTransform,
                  RefRO<PlayerRuntimeCameraConfig> runtimeCameraConfig,
                  Entity entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerRuntimeCameraConfig>>().WithEntityAccess())
        {
            PlayerRuntimeCameraConfig cameraConfig = runtimeCameraConfig.ValueRO;
            float3 playerPosition = localTransform.ValueRO.Position;

            if (localToWorldLookup.HasComponent(entity))
                playerPosition = localToWorldLookup[entity].Position;

            if (cameraConfig.Behavior == CameraBehavior.RoomFixed)
                continue;

            if (cameraConfig.Behavior != lastBehavior)
            {
                ResetCachedOffsets();
                lastBehavior = cameraConfig.Behavior;
            }

            float3 offset = cameraConfig.FollowOffset;

            // For FollowWithAutoOffset, we calculate the initial offset from the camera to the player
            // and maintain that offset.
            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.FollowWithAutoOffset:
                    if (!hasAutoOffset)
                    {
                        autoOffset = (float3)camera.transform.position - playerPosition;
                        hasAutoOffset = true;
                    }

                    offset = autoOffset;
                    break;
                case CameraBehavior.ChildOfPlayer:
                    if (!hasChildOffset)
                    {
                        float3 worldOffset = (float3)camera.transform.position - playerPosition;
                        quaternion inverseRotation = math.inverse(localTransform.ValueRO.Rotation);
                        childLocalOffset = math.rotate(inverseRotation, worldOffset);
                        hasChildOffset = true;
                    }

                    offset = childLocalOffset;
                    break;
            }

            float3 targetPosition = playerPosition + offset;

            // Handle camera behavior modes. For ChildOfPlayer, directly sets the camera's position and rotation
            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.ChildOfPlayer:
                    float3 rotatedOffset = math.rotate(localTransform.ValueRO.Rotation, offset);
                    camera.transform.position = playerPosition + rotatedOffset;
                    camera.transform.rotation = localTransform.ValueRO.Rotation;
                    break;
                default:
                    float3 newPosition = PlayerControllerMath.SmoothCameraPosition(camera.transform.position, targetPosition, cameraConfig.Values, deltaTime);
                    camera.transform.position = newPosition;
                    break;
            }

            break;
        }
    }

    /// <summary>
    /// Clears camera offset caches when camera ownership or behavior changes.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ResetCachedOffsets()
    {
        hasAutoOffset = false;
        hasChildOffset = false;
    }

    /// <summary>
    /// Resolves a camera presentation delta that can settle during transition-owned time-scale pauses.
    /// /params scaledDeltaTime DOTS scaled delta time for the current frame.
    /// /params isSceneTransitioning True while the scene manager is loading or fading between scenes.
    /// /returns Delta time suitable for presentation-only camera smoothing.
    /// </summary>
    private static float ResolvePresentationDeltaTime(float scaledDeltaTime, bool isSceneTransitioning)
    {
        if (!isSceneTransitioning || scaledDeltaTime > 0f)
            return scaledDeltaTime;

        return Time.unscaledDeltaTime;
    }
    #endregion


}
