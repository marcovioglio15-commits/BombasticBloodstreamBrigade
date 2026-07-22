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

    #region Static Fields
    private static int traversalOverrideCameraInstanceId;
    private static int traversalOverrideFrame = -1;
    #endregion

    #region Runtime Fields
    private bool hasAutoOffset;
    private float3 autoOffset;
    private bool hasChildOffset;
    private float3 childLocalOffset;
    private bool hasTraversalCameraOffset;
    private float3 traversalCameraOffset;
    private bool wasProceduralRoomTraversal;
    private int traversalCameraInstanceId;
    private CameraBehavior lastBehavior;
    private int lastCameraInstanceId;
    private int canonicalAutoOffsetCameraInstanceId;
    private int canonicalChildOffsetCameraInstanceId;
    private float3 followVelocity;
    private float3 canonicalAutoOffset;
    private float3 canonicalChildLocalOffset;
    private Entity lastPlayerEntity;
    private EntityQuery runOutcomeQuery;
    private bool hasCanonicalAutoOffset;
    private bool hasCanonicalChildOffset;
    private bool hasTrackedPlayer;
    #endregion

    #endregion

    #region Methods

    #region Lifecycle Methods
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
        bool isProceduralRoomTraversal = SystemAPI.TryGetSingleton(out GameSceneTransitionState transitionState) &&
                                         transitionState.IsTransitioning != 0 &&
                                         transitionState.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal;

        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery))
            return;

        // Dying bypasses the hard-pause gate: the freeze system pinned Time.timeScale to zero on the lethal hit but the
        // camera shake feedback must keep evolving (it switches to unscaled time below) so the player feels the final beat.
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

        // Damage-shake inputs: shake state is rewritten here (single owner), survivability and grace are read-only.
        ComponentLookup<PlayerCameraShakeState> shakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(true);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(true);
        ComponentLookup<PlayerDamageGraceState> damageGraceLookup = SystemAPI.GetComponentLookup<PlayerDamageGraceState>(true);
        ComponentLookup<PlayerImpactFrameState> impactFrameLookup = SystemAPI.GetComponentLookup<PlayerImpactFrameState>(true);
        ComponentLookup<PlayerImpactFrameBuildInState> impactFrameBuildInLookup = SystemAPI.GetComponentLookup<PlayerImpactFrameBuildInState>(true);
        float shakeNoiseTime = (float)SystemAPI.Time.ElapsedTime;
        int cameraInstanceId = camera.GetInstanceID();
        bool cameraChanged = cameraInstanceId != lastCameraInstanceId;

        // Only support one player camera config at a time, so breaks after the first iteration.
        foreach ((RefRO<LocalTransform> localTransform,
                  RefRO<PlayerRuntimeCameraConfig> runtimeCameraConfig,
                  Entity entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerRuntimeCameraConfig>>().WithEntityAccess())
        {
            PlayerRuntimeCameraConfig cameraConfig = runtimeCameraConfig.ValueRO;
            float3 playerPosition = localTransform.ValueRO.Position;

            if (localToWorldLookup.HasComponent(entity))
                playerPosition = localToWorldLookup[entity].Position;

            bool playerChanged = !hasTrackedPlayer || entity != lastPlayerEntity;

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
                                                           camera.transform.up,
                                                           camera.transform.forward);

                if (impactFrameLookup.HasComponent(entity) && impactFrameLookup[entity].IsActive != 0)
                {
                    PlayerImpactFrameState impactFrameState = impactFrameLookup[entity];
                    PlayerCameraShakeRuntimeUtility.AddImpactFrameOutput(ref shakeState,
                                                                        in impactFrameState.Effect.CameraFeedback,
                                                                        impactFrameState.CurrentBlend,
                                                                        shakeNoiseTime,
                                                                        camera.transform.right,
                                                                        camera.transform.up,
                                                                        camera.transform.forward);
                }

                if (impactFrameBuildInLookup.HasComponent(entity) && impactFrameBuildInLookup[entity].IsActive != 0)
                {
                    PlayerImpactFrameBuildInState buildInState = impactFrameBuildInLookup[entity];
                    PlayerCameraShakeRuntimeUtility.AddImpactFrameOutput(ref shakeState,
                                                                        in buildInState.Effect.CameraFeedback,
                                                                        buildInState.CurrentBlend,
                                                                        shakeNoiseTime,
                                                                        camera.transform.right,
                                                                        camera.transform.up,
                                                                        camera.transform.forward);
                }

                shakeStateLookup[entity] = shakeState;
                PlayerCameraShakeRuntimeUtility.ApplyFovToCamera(camera, in shakeState);
            }

            bool behaviorChanged = cameraConfig.Behavior != lastBehavior;

            // Camera or behavior replacement starts a fresh authored offset and discards stale spring state.
            if (behaviorChanged || cameraChanged || playerChanged)
            {
                ResetCachedOffsets();
                lastBehavior = cameraConfig.Behavior;
            }

            if (cameraChanged)
            {
                followVelocity = float3.zero;
                lastCameraInstanceId = cameraInstanceId;
            }

            // A new run receives a deterministic baseline from the persistent rig. Never recapture the previous
            // run's final camera-to-player relation as the next run's automatic offset.
            if (playerChanged)
            {
                ResetForNewPlayer(camera,
                                  entity,
                                  playerPosition,
                                  localTransform.ValueRO.Rotation,
                                  in cameraConfig,
                                  in shakeState);
            }

            if (cameraConfig.Behavior == CameraBehavior.RoomFixed)
                continue;

            // Preserve the exact source framing throughout traversal and its release frame. The player-only overlay
            // applies the same relocation delta, so neither camera smoothing nor room coordinates expose the commit.
            if (TryApplyTraversalFraming(camera,
                                         playerPosition,
                                         in shakeState,
                                         isProceduralRoomTraversal))
            {
                break;
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
                    float3 newPosition = targetPosition;
                    float3 smoothingSource = PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position, in shakeState);
                    newPosition = PlayerControllerMath.SmoothCameraPosition(smoothingSource,
                                                                           targetPosition,
                                                                           cameraConfig.Values,
                                                                           ref followVelocity,
                                                                           deltaTime);

                    PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform, newPosition, in shakeState, false, quaternion.identity);
                    break;
            }

            break;
        }
    }
    #endregion

    #region Traversal Continuity Methods
    /// <summary>
    /// Checks whether the follow system already wrote authoritative traversal framing for the supplied camera this frame.
    /// The room-anchor system uses this ownership marker to avoid a second competing camera transform write.
    /// </summary>
    /// <param name="camera">Gameplay camera whose transform ownership is being queried.</param>
    /// <returns>True when traversal continuity owns this camera for the current rendered frame.</returns>
    internal static bool OwnsTraversalFraming(Camera camera)
    {
        return camera != null &&
               traversalOverrideFrame == Time.frameCount &&
               traversalOverrideCameraInstanceId == camera.GetInstanceID();
    }

    /// <summary>
    /// Captures the unshaken source camera-to-player offset once, reapplies it while room coordinates change under
    /// black, and retains it for the first released frame so no spring step becomes visible at fade completion.
    /// </summary>
    /// <param name="camera">Persistent gameplay camera receiving the continuity pose.</param>
    /// <param name="playerPosition">Current world-space position of the persistent ECS player.</param>
    /// <param name="shakeState">Current feedback state layered above the preserved base framing.</param>
    /// <param name="isProceduralRoomTraversal">True while the transactional room traversal remains active.</param>
    /// <returns>True when this method wrote the authoritative camera transform for the current frame.</returns>
    private bool TryApplyTraversalFraming(Camera camera,
                                          float3 playerPosition,
                                          in PlayerCameraShakeState shakeState,
                                          bool isProceduralRoomTraversal)
    {
        if (!isProceduralRoomTraversal && !wasProceduralRoomTraversal)
            return false;

        int cameraInstanceId = camera.GetInstanceID();

        // Capture the real source view instead of the configured target offset, which may differ while the spring settles.
        if (!hasTraversalCameraOffset || traversalCameraInstanceId != cameraInstanceId)
        {
            traversalCameraOffset = PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position,
                                                                                           in shakeState) -
                                    playerPosition;
            traversalCameraInstanceId = cameraInstanceId;
            hasTraversalCameraOffset = true;
            followVelocity = float3.zero;
        }

        PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform,
                                                      playerPosition + traversalCameraOffset,
                                                      in shakeState,
                                                      false,
                                                      quaternion.identity);
        traversalOverrideCameraInstanceId = cameraInstanceId;
        traversalOverrideFrame = Time.frameCount;
        wasProceduralRoomTraversal = isProceduralRoomTraversal;

        // The release frame remains exact; normal follow smoothing resumes from this pose on the following frame.
        if (!isProceduralRoomTraversal)
        {
            hasTraversalCameraOffset = false;
            traversalCameraInstanceId = 0;
        }

        return true;
    }
    #endregion

    #region Cache Methods

    /// <summary>
    /// Rebinds the persistent camera to a newly created player without inheriting the previous run's spring,
    /// traversal offset or final world position. Automatic behaviors retain the first valid offset captured for
    /// the same persistent camera instance and reuse it on later runs.
    /// </summary>
    /// <param name="camera">Persistent gameplay camera being rebound.</param>
    /// <param name="playerEntity">New authoritative player entity.</param>
    /// <param name="playerPosition">Current player world position after start-room arrival.</param>
    /// <param name="playerRotation">Current authored player rotation.</param>
    /// <param name="cameraConfig">Resolved runtime camera behavior and fixed offset.</param>
    /// <param name="shakeState">New player's feedback state used to avoid baking shake into the baseline.</param>
    private void ResetForNewPlayer(Camera camera,
                                   Entity playerEntity,
                                   float3 playerPosition,
                                   quaternion playerRotation,
                                   in PlayerRuntimeCameraConfig cameraConfig,
                                   in PlayerCameraShakeState shakeState)
    {
        float3 currentBasePosition = PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position,
                                                                                            in shakeState);
        float3 resolvedPosition = currentBasePosition;

        // Resolve and cache one stable baseline for each automatic behavior on this persistent camera instance.
        switch (cameraConfig.Behavior)
        {
            case CameraBehavior.FollowWithAutoOffset:
                if (!hasCanonicalAutoOffset || canonicalAutoOffsetCameraInstanceId != camera.GetInstanceID())
                {
                    canonicalAutoOffset = currentBasePosition - playerPosition;
                    canonicalAutoOffsetCameraInstanceId = camera.GetInstanceID();
                    hasCanonicalAutoOffset = true;
                }

                autoOffset = canonicalAutoOffset;
                hasAutoOffset = true;
                resolvedPosition = playerPosition + autoOffset;
                break;

            case CameraBehavior.ChildOfPlayer:
                if (!hasCanonicalChildOffset || canonicalChildOffsetCameraInstanceId != camera.GetInstanceID())
                {
                    canonicalChildLocalOffset = math.rotate(math.inverse(playerRotation),
                                                            currentBasePosition - playerPosition);
                    canonicalChildOffsetCameraInstanceId = camera.GetInstanceID();
                    hasCanonicalChildOffset = true;
                }

                childLocalOffset = canonicalChildLocalOffset;
                hasChildOffset = true;
                resolvedPosition = playerPosition + math.rotate(playerRotation, childLocalOffset);
                break;

            case CameraBehavior.FollowWithOffset:
                resolvedPosition = playerPosition + cameraConfig.FollowOffset;
                break;
        }

        if (cameraConfig.Behavior != CameraBehavior.RoomFixed)
        {
            PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform,
                                                          resolvedPosition,
                                                          in shakeState,
                                                          cameraConfig.Behavior == CameraBehavior.ChildOfPlayer,
                                                          playerRotation);
        }

        lastPlayerEntity = playerEntity;
        hasTrackedPlayer = true;
        hasTraversalCameraOffset = false;
        wasProceduralRoomTraversal = false;
        traversalCameraInstanceId = 0;
        followVelocity = float3.zero;
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
    #endregion

    #endregion

}
