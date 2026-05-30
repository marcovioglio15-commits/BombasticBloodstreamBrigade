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
    private float3 followVelocity;
    private EntityQuery runOutcomeQuery;
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
        runOutcomeQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                               ComponentType.ReadOnly<PlayerRunOutcomeState>());
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

        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery))
            return;

        if (PlayerGameplayPauseUtility.IsTimeScaleHardPaused() && !isSceneTransitioning)
            return;

        if (!PlayerRuntimeCameraUtility.TryResolveGameplayCamera(out Camera camera))
            return;

        float deltaTime = ResolvePresentationDeltaTime(SystemAPI.Time.DeltaTime, isSceneTransitioning);
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        // Damage-shake inputs: shake state is rewritten here (single owner), survivability and grace are read-only.
        ComponentLookup<PlayerCameraShakeState> shakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(true);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(true);
        ComponentLookup<PlayerDamageGraceState> damageGraceLookup = SystemAPI.GetComponentLookup<PlayerDamageGraceState>(true);
        float shakeNoiseTime = (float)SystemAPI.Time.ElapsedTime;
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

            // Evolve the damage shake once for the player before behavior branching so the room-fixed camera
            // (driven by PlayerCameraRoomAnchorSystem) reads the same offset without recomputing trauma.
            PlayerCameraShakeState shakeState = default;
            bool hasShakeState = shakeStateLookup.HasComponent(entity);

            if (hasShakeState)
            {
                shakeState = shakeStateLookup[entity];
                float currentDamageDeadline = damageGraceLookup.HasComponent(entity) ? damageGraceLookup[entity].IgnoreDamageUntilTime : 0f;
                float currentSurvivability = (healthLookup.HasComponent(entity) ? healthLookup[entity].Current : 0f)
                                           + (shieldLookup.HasComponent(entity) ? shieldLookup[entity].Current : 0f);
                PlayerCameraShakeRuntimeUtility.UpdateState(ref shakeState,
                                                           in cameraConfig.Shake,
                                                           in cameraConfig.FireShake,
                                                           currentDamageDeadline,
                                                           currentSurvivability,
                                                           deltaTime,
                                                           shakeNoiseTime,
                                                           camera.transform.right,
                                                           camera.transform.up);
                shakeStateLookup[entity] = shakeState;
            }

            if (cameraConfig.Behavior == CameraBehavior.RoomFixed)
                continue;

            if (cameraConfig.Behavior != lastBehavior)
            {
                ResetCachedOffsets();
                lastBehavior = cameraConfig.Behavior;
            }

            float3 offset = cameraConfig.FollowOffset;

            // For FollowWithAutoOffset, capture the initial camera-to-player offset once. The previously applied
            // shake offset is removed first so a shake active during reacquisition cannot bias the captured offset.
            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.FollowWithAutoOffset:
                    if (!hasAutoOffset)
                    {
                        autoOffset = (float3)camera.transform.position - shakeState.PreviousAppliedPositionOffset - playerPosition;
                        hasAutoOffset = true;
                    }

                    offset = autoOffset;
                    break;
                case CameraBehavior.ChildOfPlayer:
                    if (!hasChildOffset)
                    {
                        float3 worldOffset = (float3)camera.transform.position - shakeState.PreviousAppliedPositionOffset - playerPosition;
                        quaternion inverseRotation = math.inverse(localTransform.ValueRO.Rotation);
                        childLocalOffset = math.rotate(inverseRotation, worldOffset);
                        hasChildOffset = true;
                    }

                    offset = childLocalOffset;
                    break;
            }

            float3 targetPosition = playerPosition + offset;

            // Resolve the un-shaken base position per behavior, then layer the shake offset and roll once.
            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.ChildOfPlayer:
                    float3 rotatedOffset = math.rotate(localTransform.ValueRO.Rotation, offset);
                    PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform,
                                                                 playerPosition + rotatedOffset,
                                                                 in shakeState,
                                                                 true,
                                                                 localTransform.ValueRO.Rotation);
                    break;
                default:
                    float3 smoothingSource = PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position, in shakeState);
                    float3 newPosition = PlayerControllerMath.SmoothCameraPosition(smoothingSource, targetPosition, cameraConfig.Values, ref followVelocity, deltaTime);
                    PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform, newPosition, in shakeState, false, quaternion.identity);
                    break;
            }

            break;
        }
    }

    /// <summary>
    /// Clears camera offset caches and the follow spring velocity when camera ownership or behavior changes,
    /// preventing a stale velocity from lurching the camera when a fresh target is reacquired.
    /// </summary>
    private void ResetCachedOffsets()
    {
        hasAutoOffset = false;
        hasChildOffset = false;
        followVelocity = float3.zero;
    }

    /// <summary>
    /// Resolves a camera presentation delta that can settle during transition-owned time-scale pauses.
    /// </summary>
    /// <param name="scaledDeltaTime">DOTS scaled delta time for the current frame.</param>
    /// <param name="isSceneTransitioning">True while the scene manager is loading or fading between scenes.</param>
    /// <returns>Delta time suitable for presentation-only camera smoothing.</returns>
    private static float ResolvePresentationDeltaTime(float scaledDeltaTime, bool isSceneTransitioning)
    {
        if (!isSceneTransitioning || scaledDeltaTime > 0f)
            return scaledDeltaTime;

        return Time.unscaledDeltaTime;
    }
    #endregion


}
