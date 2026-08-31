using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Updates the persistent gameplay camera from the authoritative player configuration, transition policy and boundaries.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PlayerCameraFollowSystem : ISystem
{
    #region Constants
    private const float BoundaryReacquisitionToleranceSquared = 0.0001f;
    #endregion
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
    private Entity lastBoundaryEntity;
    private EntityQuery runOutcomeQuery;
    private int boundaryCameraInstanceId;
    private bool hasCanonicalAutoOffset;
    private bool hasCanonicalChildOffset;
    private bool hasTrackedPlayer;
    private bool hasTrackedBoundary;
    private bool smoothBoundaryReacquisition;
    #endregion
    #endregion
    #region Methods
    #region Lifecycle Methods
    /// <summary>
    /// Requires an authoritative runtime camera target and caches the run-outcome query used by pause policy.
    /// </summary>
    /// <param name="state">System state used to register requirements and cache queries.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        runOutcomeQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>(),
                                               ComponentType.ReadOnly<PlayerRunOutcomeState>());
    }
    /// <summary>
    /// Resolves the configured follow behavior, applies camera feedback and constraints, and acknowledges hidden
    /// destination containment before a scene transition starts revealing the loaded scene.
    /// </summary>
    /// <param name="state">System state providing player transforms, camera runtime data and boundary state.</param>
    public void OnUpdate(ref SystemState state)
    {
        // Resolve transition presentation once so camera continuity and reveal acknowledgment share one state snapshot.
        bool hasTransitionState = SystemAPI.TryGetSingleton(out GameSceneTransitionState transitionState);
        bool isSceneTransitioning = hasTransitionState && transitionState.IsTransitioning != 0;
        bool isProceduralRoomTraversal = isSceneTransitioning &&
                                          transitionState.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal;
        bool isRevealPreparationPending = hasTransitionState &&
                                           GameSceneTransitionCameraReadinessUtility.IsPreparationPending(
                                               in transitionState);
        bool usesPreparedFraming = hasTransitionState &&
                                   GameSceneTransitionCameraReadinessUtility.UsesPreparedFraming(in transitionState);

        if (PlayerGameplayPauseUtility.IsFinalizedRunOutcomeActive(runOutcomeQuery) &&
            !isRevealPreparationPending)
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
        bool hasBoundaryRuntimeState =
            SystemAPI.TryGetSingleton(out GameCameraBoundaryRuntimeState cameraBoundaryState);
        DynamicBuffer<GameCameraBoundaryContainmentElement> containmentBoundaries = default;
        bool hasContainmentBuffer =
            SystemAPI.TryGetSingletonBuffer<GameCameraBoundaryContainmentElement>(out containmentBoundaries, true);
        bool hasContainmentBoundary = hasBoundaryRuntimeState &&
                                      cameraBoundaryState.Enabled != 0 &&
                                      cameraBoundaryState.Mode == GameCameraBoundaryMode.ContainmentVolume &&
                                      cameraBoundaryState.HasBoundary != 0 &&
                                      hasContainmentBuffer &&
                                      containmentBoundaries.Length > 0;
        bool hasImpassableBoundaries = hasBoundaryRuntimeState &&
                                       cameraBoundaryState.Enabled != 0 &&
                                       cameraBoundaryState.Mode == GameCameraBoundaryMode.ImpassableVolume;
        bool hasFastPlayPlayer = SystemAPI.TryGetSingletonEntity<GameCameraBoundaryFastPlayPlayer>(
            out Entity fastPlayPlayerEntity);

        // Fast Play owns camera focus when present; regular gameplay still supports one camera config at a time.
        foreach ((RefRO<LocalTransform> localTransform,
                  RefRO<PlayerRuntimeCameraConfig> runtimeCameraConfig,
                  Entity entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerRuntimeCameraConfig>>().WithEntityAccess())
        {
            if (hasFastPlayPlayer && entity != fastPlayPlayerEntity)
                continue;

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

            RefreshBoundaryReacquisition(camera,
                                         in shakeState,
                                         in cameraBoundaryState,
                                         containmentBoundaries,
                                         hasContainmentBoundary,
                                         cameraChanged || playerChanged);

            // A new run receives a deterministic baseline from the persistent rig. Never recapture the previous
            // run's final camera-to-player relation as the next run's automatic offset.
            if (playerChanged)
            {
                ResetForNewPlayer(camera,
                                  entity,
                                  playerPosition,
                                  localTransform.ValueRO.Rotation,
                                  in cameraConfig,
                                  in shakeState,
                                  in cameraBoundaryState,
                                  containmentBoundaries,
                                  hasContainmentBoundary);
            }

            if (cameraConfig.Behavior == CameraBehavior.RoomFixed)
                continue;

            // Preserve the exact source framing throughout traversal and its release frame. The player-only overlay
            // applies the same relocation delta, so neither camera smoothing nor room coordinates expose the commit.
            if (TryApplyTraversalFraming(camera,
                                         playerPosition,
                                         in shakeState,
                                         isProceduralRoomTraversal,
                                         usesPreparedFraming))
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
                    float3 childTargetPosition = playerPosition + rotatedOffset;
                    float3 childSmoothingSource =
                        PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position,
                                                                               in shakeState);

                    if (hasContainmentBoundary)
                    {
                        childTargetPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(
                            containmentBoundaries,
                            childTargetPosition,
                            cameraBoundaryState.SoftZoneDistance);
                    }
                    else if (hasImpassableBoundaries)
                    {
                        childTargetPosition = ResolveImpassableSoftConstraints(ref state,
                                                                                childSmoothingSource,
                                                                                childTargetPosition,
                                                                                cameraBoundaryState.SoftZoneDistance);
                    }

                    float3 childDesiredPosition = childTargetPosition;

                    if (isRevealPreparationPending && hasContainmentBoundary)
                    {
                        followVelocity = float3.zero;
                        smoothBoundaryReacquisition = false;
                    }

                    if (smoothBoundaryReacquisition)
                    {
                        childTargetPosition = PlayerControllerMath.SmoothCameraPosition(
                            childSmoothingSource,
                            childTargetPosition,
                            cameraConfig.Values,
                            ref followVelocity,
                            deltaTime);

                        if (hasContainmentBoundary)
                        {
                            GameCameraBoundaryUtility.ApplyReachableHardConstraint(
                                containmentBoundaries,
                                childSmoothingSource,
                                ref childTargetPosition,
                                ref followVelocity);
                        }

                        if (math.distancesq(childTargetPosition, childDesiredPosition) <=
                            BoundaryReacquisitionToleranceSquared)
                        {
                            smoothBoundaryReacquisition = false;
                        }
                    }

                    if (hasImpassableBoundaries)
                    {
                        ApplyImpassableHardConstraints(ref state,
                                                       childSmoothingSource,
                                                       ref childTargetPosition,
                                                       ref followVelocity);
                    }

                    PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform,
                                                                 childTargetPosition,
                                                                 in shakeState,
                                                                 true,
                                                                 localTransform.ValueRO.Rotation);
                    break;
                default:
                    float3 smoothingSource = PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position, in shakeState);

                    if (hasContainmentBoundary)
                    {
                        targetPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(
                            containmentBoundaries,
                            targetPosition,
                            cameraBoundaryState.SoftZoneDistance);
                    }
                    else if (hasImpassableBoundaries)
                    {
                        targetPosition = ResolveImpassableSoftConstraints(ref state,
                                                                          smoothingSource,
                                                                          targetPosition,
                                                                          cameraBoundaryState.SoftZoneDistance);
                    }

                    float3 newPosition = targetPosition;
                    bool applyPreparedContainment = isRevealPreparationPending && hasContainmentBoundary;

                    if (applyPreparedContainment)
                    {
                        followVelocity = float3.zero;
                        smoothBoundaryReacquisition = false;
                    }
                    else
                    {
                        newPosition = PlayerControllerMath.SmoothCameraPosition(smoothingSource,
                                                                               targetPosition,
                                                                               cameraConfig.Values,
                                                                               ref followVelocity,
                                                                               deltaTime);
                    }

                    if (hasContainmentBoundary && !applyPreparedContainment)
                    {
                        GameCameraBoundaryUtility.ApplyReachableHardConstraint(
                            containmentBoundaries,
                            smoothingSource,
                            ref newPosition,
                            ref followVelocity);
                    }
                    else if (hasImpassableBoundaries)
                    {
                        ApplyImpassableHardConstraints(ref state,
                                                       smoothingSource,
                                                       ref newPosition,
                                                       ref followVelocity);
                    }

                    if (smoothBoundaryReacquisition &&
                        math.distancesq(smoothingSource, targetPosition) <=
                        BoundaryReacquisitionToleranceSquared)
                    {
                        smoothBoundaryReacquisition = false;
                    }

                    PlayerCameraShakeRuntimeUtility.ApplyToCamera(camera.transform, newPosition, in shakeState, false, quaternion.identity);
                    break;
            }

            // Acknowledge only after the authoritative non-room-fixed writer committed its hidden destination pose.
            if (isRevealPreparationPending)
            {
                RefRW<GameSceneTransitionState> transitionStateReference =
                    SystemAPI.GetSingletonRW<GameSceneTransitionState>();
                GameSceneTransitionCameraReadinessUtility.MarkPrepared(ref transitionStateReference.ValueRW);
            }

            break;
        }
    }
    #endregion
    #region Impassable Boundary Methods
    /// <summary>
    /// Applies every static impassable footprint to a desired camera target without allocating a runtime collection.
    /// </summary>
    /// <param name="state">System state used by the generated boundary query.</param>
    /// <param name="sourcePosition">Current unshaken camera position.</param>
    /// <param name="desiredPosition">Unconstrained camera target.</param>
    /// <param name="softZoneDistance">World-space braking distance outside each footprint.</param>
    /// <returns>Target progressively blocked by every approached footprint.</returns>
    private float3 ResolveImpassableSoftConstraints(ref SystemState state,
                                                    float3 sourcePosition,
                                                    float3 desiredPosition,
                                                    float softZoneDistance)
    {
        foreach (RefRO<GameCameraBoundary> boundaryReference in
                 SystemAPI.Query<RefRO<GameCameraBoundary>>())
        {
            desiredPosition = GameCameraBoundaryImpassableUtility.ResolveSoftBlockedPosition(
                in boundaryReference.ValueRO,
                sourcePosition,
                desiredPosition,
                softZoneDistance);
        }

        return desiredPosition;
    }

    /// <summary>
    /// Stops an integrated camera step against every crossed impassable footprint without temporary allocations.
    /// </summary>
    /// <param name="state">System state used by the generated boundary query.</param>
    /// <param name="sourcePosition">Camera position before spring integration.</param>
    /// <param name="candidatePosition">Integrated camera position constrained in place.</param>
    /// <param name="velocity">Persistent spring velocity stabilized at crossed faces.</param>
    private void ApplyImpassableHardConstraints(ref SystemState state,
                                                float3 sourcePosition,
                                                ref float3 candidatePosition,
                                                ref float3 velocity)
    {
        foreach (RefRO<GameCameraBoundary> boundaryReference in
                 SystemAPI.Query<RefRO<GameCameraBoundary>>())
        {
            GameCameraBoundaryImpassableUtility.ApplyHardConstraint(
                in boundaryReference.ValueRO,
                sourcePosition,
                ref candidatePosition,
                ref velocity);
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
    /// <param name="usesPreparedFraming">True when destination containment must replace traversal framing before reveal.</param>
    /// <returns>True when this method wrote the authoritative camera transform for the current frame.</returns>
    private bool TryApplyTraversalFraming(Camera camera,
                                          float3 playerPosition,
                                          in PlayerCameraShakeState shakeState,
                                          bool isProceduralRoomTraversal,
                                          bool usesPreparedFraming)
    {
        // Release source framing while fully covered so destination containment can become authoritative before reveal.
        if (usesPreparedFraming)
        {
            hasTraversalCameraOffset = false;
            traversalCameraInstanceId = 0;
            wasProceduralRoomTraversal = false;
            return false;
        }

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
    /// Detects boundary ownership changes and enables a spring-driven entrance when direct hard containment would snap
    /// the current camera into a newly selected footprint.
    /// </summary>
    /// <param name="camera">Persistent gameplay camera being constrained.</param>
    /// <param name="shakeState">Feedback state used to recover the unshaken camera position.</param>
    /// <param name="cameraBoundaryState">Current boundary selection published before camera presentation.</param>
    /// <param name="containmentBoundaries">Active compound containment group used for continuity checks.</param>
    /// <param name="hasCameraBoundary">True when the selection contains an active boundary.</param>
    /// <param name="forceReacquisition">True when camera or player ownership changed without a boundary entity change.</param>
    private void RefreshBoundaryReacquisition(Camera camera,
                                              in PlayerCameraShakeState shakeState,
                                              in GameCameraBoundaryRuntimeState cameraBoundaryState,
                                              DynamicBuffer<GameCameraBoundaryContainmentElement> containmentBoundaries,
                                              bool hasCameraBoundary,
                                              bool forceReacquisition)
    {
        if (!hasCameraBoundary)
        {
            lastBoundaryEntity = Entity.Null;
            boundaryCameraInstanceId = 0;
            hasTrackedBoundary = false;
            smoothBoundaryReacquisition = false;
            return;
        }

        int cameraInstanceId = camera.GetInstanceID();
        bool boundaryChanged = hasTrackedBoundary &&
                               cameraBoundaryState.BoundaryEntity != lastBoundaryEntity;

        if (!forceReacquisition && !boundaryChanged &&
            hasTrackedBoundary && cameraInstanceId == boundaryCameraInstanceId)
        {
            return;
        }

        // Every real boundary hand-off is smoothed; first acquisition only needs it when the camera starts outside.
        float3 currentBasePosition =
            PlayerCameraShakeRuntimeUtility.ResolveSmoothingSource(camera.transform.position, in shakeState);
        smoothBoundaryReacquisition = boundaryChanged ||
                                      !GameCameraBoundaryUtility.Contains(containmentBoundaries,
                                                                          currentBasePosition);
        lastBoundaryEntity = cameraBoundaryState.BoundaryEntity;
        boundaryCameraInstanceId = cameraInstanceId;
        hasTrackedBoundary = true;

        if (smoothBoundaryReacquisition && !boundaryChanged)
            followVelocity = float3.zero;
    }

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
    /// <param name="cameraBoundaryState">Selected boundary data available for the new player.</param>
    /// <param name="containmentBoundaries">Active compound containment group available for the new player.</param>
    /// <param name="hasCameraBoundary">True when the selected boundary must constrain the reset pose.</param>
    private void ResetForNewPlayer(Camera camera,
                                   Entity playerEntity,
                                   float3 playerPosition,
                                   quaternion playerRotation,
                                   in PlayerRuntimeCameraConfig cameraConfig,
                                   in PlayerCameraShakeState shakeState,
                                   in GameCameraBoundaryRuntimeState cameraBoundaryState,
                                   DynamicBuffer<GameCameraBoundaryContainmentElement> containmentBoundaries,
                                   bool hasCameraBoundary)
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

        if (hasCameraBoundary && cameraConfig.Behavior != CameraBehavior.RoomFixed)
        {
            resolvedPosition = GameCameraBoundaryUtility.ResolveSoftConstrainedPosition(
                containmentBoundaries,
                resolvedPosition,
                cameraBoundaryState.SoftZoneDistance);
        }

        bool mayApplyImmediately = !hasCameraBoundary ||
                                   !smoothBoundaryReacquisition &&
                                   GameCameraBoundaryUtility.Contains(containmentBoundaries,
                                                                       currentBasePosition);

        if (cameraConfig.Behavior != CameraBehavior.RoomFixed && mayApplyImmediately)
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
