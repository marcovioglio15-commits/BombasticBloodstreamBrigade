using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Updates orbital projection positions, lifetime timers, and spawn/despawn animation phases.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerOrbitalProjectionSpawnSystem))]
[UpdateAfter(typeof(PlayerLookRotationSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerOrbitalProjectionTransformSystem : ISystem
{
    #region Constants
    private const float DegreesToRadians = math.PI / 180f;
    private const float DefaultMaximumLookFollowSpeedDegreesPerSecond = 540f;
    private const float FollowAlignmentToleranceDegrees = 0.01f;
    private const float FullCircleDegrees = 360f;
    private const float IndependentOrbitLayoutBlendSeconds = 0.35f;
    private const float MinimumBounceSectorDegrees = 0.01f;
    #endregion

    #region Fields
    private EntityQuery projectionQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the projection component required by this transform update system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        projectionQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerOrbitalProjectionInstance>()
            .Build();

        state.RequireForUpdate<PlayerOrbitalProjectionInstance>();
    }

    /// <summary>
    /// Moves projections around their owner and destroys entities whose despawn animation has completed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        ComponentLookup<LocalTransform> ownerTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerLookState> lookStateLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        BufferLookup<PlayerOrbitalProjectionLostElement> lostProjectionLookup = SystemAPI.GetBufferLookup<PlayerOrbitalProjectionLostElement>(false);
        NativeArray<Entity> projectionEntities = projectionQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerOrbitalProjectionInstance> projectionInstances = projectionQuery.ToComponentDataArray<PlayerOrbitalProjectionInstance>(Allocator.Temp);

        foreach ((RefRW<PlayerOrbitalProjectionInstance> projection,
                  RefRW<LocalTransform> transform,
                  Entity projectionEntity)
                 in SystemAPI.Query<RefRW<PlayerOrbitalProjectionInstance>, RefRW<LocalTransform>>()
                             .WithEntityAccess())
        {
            PlayerOrbitalProjectionInstance instance = projection.ValueRO;

            if (!ownerTransformLookup.HasComponent(instance.OwnerEntity))
            {
                commandBuffer.DestroyEntity(projectionEntity);
                continue;
            }

            LocalTransform ownerTransform = ownerTransformLookup[instance.OwnerEntity];
            PlayerLookState lookState = lookStateLookup.HasComponent(instance.OwnerEntity)
                ? lookStateLookup[instance.OwnerEntity]
                : default;

            if (instance.CurrentHealth <= 0f && instance.Phase != PlayerOrbitalProjectionPhase.Despawning)
            {
                PlayerOrbitalProjectionLossRuntimeUtility.TryRecordPermanentLoss(ref lostProjectionLookup,
                                                                                 in instance);
                BeginDespawn(ref instance, transform.ValueRO.Position);
            }

            TickMotion(ref instance,
                       projectionEntity,
                       projectionEntities,
                       projectionInstances,
                       in lookState,
                       deltaTime);
            TickLifetime(ref instance, transform.ValueRO.Position, deltaTime);

            if (instance.Phase == PlayerOrbitalProjectionPhase.Despawning)
            {
                if (TickDespawn(ref instance, ref transform.ValueRW, ownerTransform.Position, deltaTime))
                {
                    commandBuffer.DestroyEntity(projectionEntity);
                    continue;
                }
            }
            else
            {
                TickSpawnOrActive(ref instance, ref transform.ValueRW, ownerTransform.Position, deltaTime);
            }

            // Face Orbit Outward keeps the projection's forward axis pointing away from the player;
            // the same yaw also rotates the model collision silhouette in the contact systems.
            if (instance.Config.FaceOrbitOutward != 0)
                transform.ValueRW.Rotation = quaternion.RotateY(instance.AngleDegrees * DegreesToRadians);

            projection.ValueRW = instance;
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
        projectionEntities.Dispose();
        projectionInstances.Dispose();
    }
    #endregion

    #region Motion
    /// <summary>
    /// Updates the angular state used by the projection's configured motion mode.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="projectionEntity">Projection entity being updated.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="lookState">Owner look state used by Follow Player Look mode.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickMotion(ref PlayerOrbitalProjectionInstance instance,
                                   Entity projectionEntity,
                                   NativeArray<Entity> projectionEntities,
                                   NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                   in PlayerLookState lookState,
                                   float deltaTime)
    {
        switch (instance.Config.MotionMode)
        {
            case OrbitalProjectionMotionMode.IndependentOrbit:
                TickIndependentOrbit(ref instance,
                                     projectionEntity,
                                     projectionEntities,
                                     projectionInstances,
                                     deltaTime);
                break;
            case OrbitalProjectionMotionMode.FollowPlayerLook:
                TickFollowPlayerLook(ref instance,
                                     projectionInstances,
                                     in lookState,
                                     deltaTime);
                break;
            default:
                instance.AngleDegrees = instance.Config.AngleOffsetDegrees;
                break;
        }
    }

    /// <summary>
    /// Advances one Follow Player Look projection, applying ring slot distribution when more than
    /// one Follow Player Look projection shares the same orbit so they spread evenly around the
    /// player's look direction instead of overlapping.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data captured before this update.</param>
    /// <param name="lookState">Owner look state providing the live look angle.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickFollowPlayerLook(ref PlayerOrbitalProjectionInstance instance,
                                             NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                             in PlayerLookState lookState,
                                             float deltaTime)
    {
        float currentLookAngleDegrees = PlayerOrbitalProjectionStableLayoutUtility.ResolveLookAngleDegrees(in lookState);
        float lookRotationDeltaDegrees = ResolveSignedAngleDelta(instance.FollowLookAngleDegrees,
                                                                 currentLookAngleDegrees);
        instance.FollowLookAngleDegrees += lookRotationDeltaDegrees;
        float targetAngle;

        // Resolve targets from the continuous look angle rather than the lagging visible angle.
        // This prevents shortest-path direction flips while the player spins faster than the follow spring.
        if (!PlayerOrbitalProjectionStableLayoutUtility.TryResolveLiveRingTargetAngle(in instance,
                                                                                       instance.FollowLookAngleDegrees,
                                                                                       projectionInstances,
                                                                                       out targetAngle))
        {
            targetAngle = instance.FollowLookAngleDegrees + instance.Config.AngleOffsetDegrees;
        }

        float followDelay = math.max(0f, instance.Config.LookFollowDelaySeconds);

        if (followDelay <= 0f)
        {
            instance.FollowAngleDegrees = targetAngle;
            instance.FollowAngularVelocityDegrees = 0f;
        }
        else
        {
            targetAngle = DiscardCompletedFollowTurns(ref instance, targetAngle);
            AdvanceLocalFollow(ref instance,
                               targetAngle,
                               lookRotationDeltaDegrees,
                               followDelay,
                               ResolveMaximumLookFollowSpeedDegreesPerSecond(in instance.Config),
                               deltaTime);
        }

        instance.AngleDegrees = instance.FollowAngleDegrees;
    }

    /// <summary>
    /// Advances independent-orbit motion while blending same-distance projections into an even 360-degree layout.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="projectionEntity">Projection entity being updated.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickIndependentOrbit(ref PlayerOrbitalProjectionInstance instance,
                                             Entity projectionEntity,
                                             NativeArray<Entity> projectionEntities,
                                             NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                             float deltaTime)
    {
        if (PlayerOrbitalProjectionConeLayoutUtility.TryResolveBounceSector(in instance,
                                                                            projectionEntity,
                                                                            projectionEntities,
                                                                            projectionInstances,
                                                                            out float sectorStartDegrees,
                                                                            out float sectorEndDegrees))
        {
            TickBounceOrbit(ref instance, sectorStartDegrees, sectorEndDegrees, deltaTime);
            return;
        }

        instance.AngleDegrees += instance.Config.OrbitSpeedDegreesPerSecond * deltaTime;

        if (!TryResolveIndependentOrbitLayoutAngle(in instance,
                                                   projectionEntity,
                                                   projectionEntities,
                                                   projectionInstances,
                                                   deltaTime,
                                                   out float layoutAngleDegrees))
        {
            return;
        }

        float blend = math.saturate(deltaTime / IndependentOrbitLayoutBlendSeconds);
        instance.AngleDegrees += ResolveSignedAngleDelta(instance.AngleDegrees, layoutAngleDegrees) * blend;
    }

    /// <summary>
    /// Advances one independent projection inside its assigned bounce sector while preserving motion continuity across layout changes.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="sectorStartDegrees">Inclusive assigned sector start in unwrapped degrees.</param>
    /// <param name="sectorEndDegrees">Inclusive assigned sector end in unwrapped degrees.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickBounceOrbit(ref PlayerOrbitalProjectionInstance instance,
                                        float sectorStartDegrees,
                                        float sectorEndDegrees,
                                        float deltaTime)
    {
        float sectorWidthDegrees = math.max(0f, sectorEndDegrees - sectorStartDegrees);
        float sectorCenterDegrees = sectorStartDegrees + sectorWidthDegrees * 0.5f;
        float currentDegrees = UnwrapNear(instance.AngleDegrees, sectorCenterDegrees);

        if (sectorWidthDegrees <= MinimumBounceSectorDegrees)
        {
            BlendTowardBounceSector(ref instance, sectorCenterDegrees, deltaTime);
            return;
        }

        if (currentDegrees < sectorStartDegrees || currentDegrees > sectorEndDegrees)
        {
            BlendTowardBounceSector(ref instance,
                                    math.clamp(currentDegrees, sectorStartDegrees, sectorEndDegrees),
                                    deltaTime);
            return;
        }

        float speedDegreesPerSecond = math.abs(instance.Config.OrbitSpeedDegreesPerSecond);

        if (speedDegreesPerSecond <= 0f)
            return;

        sbyte bounceDirection = instance.OrbitBounceDirection < 0 ? (sbyte)-1 : (sbyte)1;
        float nextDegrees = currentDegrees + speedDegreesPerSecond * deltaTime * bounceDirection;

        // Reflect short and long frame steps so bursty frame time still keeps the orbit inside the sector.
        for (int reflectionIndex = 0; reflectionIndex < 4; reflectionIndex++)
        {
            if (nextDegrees > sectorEndDegrees)
            {
                nextDegrees = sectorEndDegrees - (nextDegrees - sectorEndDegrees);
                bounceDirection = -1;
                continue;
            }

            if (nextDegrees < sectorStartDegrees)
            {
                nextDegrees = sectorStartDegrees + (sectorStartDegrees - nextDegrees);
                bounceDirection = 1;
                continue;
            }

            break;
        }

        instance.AngleDegrees = math.clamp(nextDegrees, sectorStartDegrees, sectorEndDegrees);
        instance.OrbitBounceDirection = bounceDirection;
    }

    /// <summary>
    /// Blends a cone projection toward its newly assigned sector boundary when live subdivisions change.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="targetDegrees">Nearest valid sector angle.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void BlendTowardBounceSector(ref PlayerOrbitalProjectionInstance instance,
                                                float targetDegrees,
                                                float deltaTime)
    {
        float blend = math.saturate(deltaTime / IndependentOrbitLayoutBlendSeconds);
        instance.AngleDegrees += ResolveSignedAngleDelta(instance.AngleDegrees, targetDegrees) * blend;
    }

    /// <summary>
    /// Resolves the target angle for one independent-orbit projection inside its same-distance layout group.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="projectionEntity">Projection entity being updated.</param>
    /// <param name="projectionEntities">Snapshot of projection entities alive before this update.</param>
    /// <param name="projectionInstances">Snapshot of projection instance data aligned with projectionEntities.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <param name="layoutAngleDegrees">Resolved target angle for this frame.</param>
    /// <returns>True when the projection belongs to a multi-projection layout group.</returns>
    private static bool TryResolveIndependentOrbitLayoutAngle(in PlayerOrbitalProjectionInstance instance,
                                                              Entity projectionEntity,
                                                              NativeArray<Entity> projectionEntities,
                                                              NativeArray<PlayerOrbitalProjectionInstance> projectionInstances,
                                                              float deltaTime,
                                                              out float layoutAngleDegrees)
    {
        layoutAngleDegrees = instance.AngleDegrees;
        int currentSortKey = PlayerOrbitalProjectionConeLayoutUtility.ResolveSortKey(projectionEntity, in instance);
        int anchorSortKey = int.MaxValue;
        float anchorAngleDegrees = instance.AngleDegrees;
        float anchorSpeedDegreesPerSecond = instance.Config.OrbitSpeedDegreesPerSecond;
        int layoutCount = 0;
        int layoutSlot = 0;
        bool hasAuthoredConeOnRing = PlayerOrbitalProjectionConeLayoutUtility.HasAuthoredConeOnRing(in instance, projectionInstances);

        for (int candidateIndex = 0; candidateIndex < projectionInstances.Length; candidateIndex++)
        {
            PlayerOrbitalProjectionInstance candidate = projectionInstances[candidateIndex];

            if (!IsIndependentOrbitLayoutMatch(in instance, in candidate, hasAuthoredConeOnRing))
                continue;

            int candidateSortKey = PlayerOrbitalProjectionConeLayoutUtility.ResolveSortKey(projectionEntities[candidateIndex], in candidate);
            layoutCount++;

            if (candidateSortKey < currentSortKey)
                layoutSlot++;

            if (candidateSortKey >= anchorSortKey)
                continue;

            anchorSortKey = candidateSortKey;
            anchorAngleDegrees = candidate.AngleDegrees;
            anchorSpeedDegreesPerSecond = candidate.Config.OrbitSpeedDegreesPerSecond;
        }

        if (layoutCount <= 1)
            return false;

        float angleStep = 360f / layoutCount;
        layoutAngleDegrees = anchorAngleDegrees + anchorSpeedDegreesPerSecond * deltaTime + angleStep * layoutSlot;
        return true;
    }

    /// <summary>
    /// Checks whether two projections should share the same independent-orbit layout ring.
    /// </summary>
    /// <param name="reference">Reference projection being updated.</param>
    /// <param name="candidate">Candidate projection from the frame snapshot.</param>
    /// <param name="hasAuthoredConeOnRing">Whether authored bounce cones reserve this orbit ring.</param>
    /// <returns>True when both projections belong to the same owner and same-distance independent orbit group.</returns>
    private static bool IsIndependentOrbitLayoutMatch(in PlayerOrbitalProjectionInstance reference,
                                                      in PlayerOrbitalProjectionInstance candidate,
                                                      bool hasAuthoredConeOnRing)
    {
        if (!PlayerOrbitalProjectionConeLayoutUtility.IsSameRingIndependentProjection(in reference, in candidate))
            return false;

        if (candidate.Config.BounceInsideOrbitCone != 0)
            return false;

        if (hasAuthoredConeOnRing &&
            candidate.Config.FullOrbitConeResponse == OrbitalProjectionFullOrbitConeResponse.BounceInComplementaryCone)
            return false;

        return reference.Config.BounceInsideOrbitCone == 0;
    }

    /// <summary>
    /// Updates non-persistent lifetime and starts despawn when the timer ends.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="currentPosition">Current projection world position.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickLifetime(ref PlayerOrbitalProjectionInstance instance, float3 currentPosition, float deltaTime)
    {
        if (instance.Persistent != 0 || instance.Phase != PlayerOrbitalProjectionPhase.Active)
            return;

        instance.RemainingLifetimeSeconds -= deltaTime;

        if (instance.RemainingLifetimeSeconds > 0f)
            return;

        BeginDespawn(ref instance, currentPosition);
    }

    /// <summary>
    /// Applies spawn interpolation or active orbit position to a live projection.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="transform">Projection transform updated in place.</param>
    /// <param name="ownerPosition">Current player world position.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void TickSpawnOrActive(ref PlayerOrbitalProjectionInstance instance,
                                          ref LocalTransform transform,
                                          float3 ownerPosition,
                                          float deltaTime)
    {
        float3 targetPosition = ResolveTargetPosition(in instance, ownerPosition);

        if (instance.Phase == PlayerOrbitalProjectionPhase.Spawning)
        {
            float duration = math.max(0f, instance.Config.SpawnAnimationSeconds);

            if (duration <= 0f)
            {
                instance.Phase = PlayerOrbitalProjectionPhase.Active;
                instance.PhaseElapsedSeconds = 0f;
                transform.Position = targetPosition;
                return;
            }

            instance.PhaseElapsedSeconds += deltaTime;
            float normalizedTime = math.saturate(instance.PhaseElapsedSeconds / duration);
            transform.Position = math.lerp(ownerPosition, targetPosition, Smooth01(normalizedTime));

            if (normalizedTime >= 1f)
            {
                instance.Phase = PlayerOrbitalProjectionPhase.Active;
                instance.PhaseElapsedSeconds = 0f;
            }

            return;
        }

        transform.Position = targetPosition;
    }

    /// <summary>
    /// Applies despawn interpolation back to the owner and reports when the entity can be destroyed.
    /// </summary>
    /// <param name="instance">Projection instance being updated.</param>
    /// <param name="transform">Projection transform updated in place.</param>
    /// <param name="ownerPosition">Current player world position.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    /// <returns>True when the despawn animation has completed.</returns>
    private static bool TickDespawn(ref PlayerOrbitalProjectionInstance instance,
                                    ref LocalTransform transform,
                                    float3 ownerPosition,
                                    float deltaTime)
    {
        float duration = math.max(0f, instance.Config.DespawnAnimationSeconds);

        if (duration <= 0f)
        {
            transform.Position = ownerPosition;
            return true;
        }

        instance.PhaseElapsedSeconds += deltaTime;
        float normalizedTime = math.saturate(instance.PhaseElapsedSeconds / duration);
        transform.Position = math.lerp(instance.DespawnStartPosition, ownerPosition, Smooth01(normalizedTime));
        return normalizedTime >= 1f;
    }

    /// <summary>
    /// Resolves the target orbit position for one projection around its owner.
    /// </summary>
    /// <param name="instance">Projection instance containing orbit settings.</param>
    /// <param name="ownerPosition">Current player world position.</param>
    /// <returns>World-space target orbit position.</returns>
    private static float3 ResolveTargetPosition(in PlayerOrbitalProjectionInstance instance, float3 ownerPosition)
    {
        float angleRadians = instance.AngleDegrees * DegreesToRadians;
        float distance = math.max(0f, instance.Config.OrbitDistance);
        float3 offset = new float3(math.sin(angleRadians) * distance,
                                   instance.Config.HeightOffset,
                                   math.cos(angleRadians) * distance);
        return ownerPosition + offset;
    }

    /// <summary>
    /// Starts despawn animation from the current position.
    /// </summary>
    /// <param name="instance">Projection instance moved into despawn phase.</param>
    /// <param name="currentPosition">Current projection world position used as animation start.</param>
    private static void BeginDespawn(ref PlayerOrbitalProjectionInstance instance, float3 currentPosition)
    {
        instance.Phase = PlayerOrbitalProjectionPhase.Despawning;
        instance.PhaseElapsedSeconds = 0f;
        instance.DespawnStartPosition = currentPosition;
    }
    #endregion

    #region Angle Helpers
    /// <summary>
    /// Removes completed full-turn backlog while preserving the remaining signed lag and physical
    /// target orientation, preventing long delayed spins without changing catch-up direction.
    /// </summary>
    /// <param name="instance">Projection whose continuous look domain is rebased in place.</param>
    /// <param name="targetDegrees">Current continuously unwrapped follow target.</param>
    /// <returns>Physically equivalent target with less than one full turn of signed backlog.</returns>
    private static float DiscardCompletedFollowTurns(ref PlayerOrbitalProjectionInstance instance,
                                                     float targetDegrees)
    {
        float targetLagDegrees = targetDegrees - instance.FollowAngleDegrees;

        if (math.abs(targetLagDegrees) < FullCircleDegrees)
            return targetDegrees;

        float discardedDegrees = math.trunc(targetLagDegrees / FullCircleDegrees) * FullCircleDegrees;
        instance.FollowLookAngleDegrees -= discardedDegrees;
        return targetDegrees - discardedDegrees;
    }

    /// <summary>
    /// Advances visible Follow Player Look motion using uncapped player-turn inheritance plus
    /// independently limited exponential catch-up. Contradictory catch-up is removed without
    /// suppressing the player's current rotation, keeping sustained turns and reversals responsive.
    /// </summary>
    /// <param name="instance">Projection visible follow state updated in place.</param>
    /// <param name="targetDegrees">Continuous target with completed full-turn backlog removed.</param>
    /// <param name="lookRotationDeltaDegrees">Signed player look rotation observed this frame.</param>
    /// <param name="followDelaySeconds">Authored exponential follow response time.</param>
    /// <param name="maximumCatchUpSpeedDegreesPerSecond">Maximum autonomous catch-up angular speed.</param>
    /// <param name="deltaTime">Current frame delta time.</param>
    private static void AdvanceLocalFollow(ref PlayerOrbitalProjectionInstance instance,
                                           float targetDegrees,
                                           float lookRotationDeltaDegrees,
                                           float followDelaySeconds,
                                           float maximumCatchUpSpeedDegreesPerSecond,
                                           float deltaTime)
    {
        float remainingDegrees = targetDegrees - instance.FollowAngleDegrees;

        if (math.abs(remainingDegrees) <= FollowAlignmentToleranceDegrees)
        {
            instance.FollowAngleDegrees = targetDegrees;
            instance.FollowAngularVelocityDegrees = 0f;
            return;
        }

        float response = 1f - math.exp(-deltaTime / math.max(0.0001f, followDelaySeconds));
        float maximumCatchUpStepDegrees = math.max(0f, maximumCatchUpSpeedDegreesPerSecond) * deltaTime;
        float catchUpStepDegrees = math.clamp(remainingDegrees * response,
                                              -maximumCatchUpStepDegrees,
                                              maximumCatchUpStepDegrees);

        // Discard contradictory catch-up during active input so it cannot attenuate or reverse the
        // player's current visible turn response.
        if (math.abs(lookRotationDeltaDegrees) > FollowAlignmentToleranceDegrees &&
            catchUpStepDegrees * lookRotationDeltaDegrees < 0f)
            catchUpStepDegrees = 0f;

        float signedStepDegrees = lookRotationDeltaDegrees + catchUpStepDegrees;

        // Do not overshoot a target that lies along the selected step direction.
        if (signedStepDegrees * remainingDegrees > 0f &&
            math.abs(signedStepDegrees) > math.abs(remainingDegrees))
        {
            signedStepDegrees = remainingDegrees;
        }

        instance.FollowAngleDegrees += signedStepDegrees;
        instance.FollowAngularVelocityDegrees = deltaTime > 0f
            ? signedStepDegrees / deltaTime
            : 0f;
    }

    /// <summary>
    /// Resolves the authored Follow Player Look catch-up speed cap, using a safe fallback for
    /// existing presets and formula results that do not provide a positive value.
    /// </summary>
    /// <param name="config">Projection runtime configuration containing the authored speed cap.</param>
    /// <returns>Positive maximum autonomous catch-up speed in degrees per second.</returns>
    private static float ResolveMaximumLookFollowSpeedDegreesPerSecond(in OrbitalProjectionConfig config)
    {
        return config.MaximumLookFollowSpeedDegreesPerSecond > 0f
            ? config.MaximumLookFollowSpeedDegreesPerSecond
            : DefaultMaximumLookFollowSpeedDegreesPerSecond;
    }

    /// <summary>
    /// Resolves the shortest signed delta between two angles.
    /// </summary>
    /// <param name="currentDegrees">Current angle in degrees.</param>
    /// <param name="targetDegrees">Target angle in degrees.</param>
    /// <returns>Signed delta in the -180 to 180 range.</returns>
    private static float ResolveSignedAngleDelta(float currentDegrees, float targetDegrees)
    {
        float deltaDegrees = math.fmod(targetDegrees - currentDegrees, FullCircleDegrees);

        if (deltaDegrees > 180f)
            deltaDegrees -= FullCircleDegrees;
        else if (deltaDegrees < -180f)
            deltaDegrees += FullCircleDegrees;

        return deltaDegrees;
    }

    /// <summary>
    /// Unwraps one angle to the nearest equivalent representation around a reference angle.
    /// </summary>
    /// <param name="angleDegrees">Angle to unwrap.</param>
    /// <param name="referenceDegrees">Reference angle that anchors the unwrapped result.</param>
    /// <returns>Equivalent angle nearest to the reference.</returns>
    private static float UnwrapNear(float angleDegrees, float referenceDegrees)
    {
        return referenceDegrees + ResolveSignedAngleDelta(referenceDegrees, angleDegrees);
    }

    /// <summary>
    /// Applies cubic smoothing to a normalized interpolation value.
    /// </summary>
    /// <param name="value">Raw interpolation value in any numeric range.</param>
    /// <returns>Smoothed value clamped to 0-1.</returns>
    private static float Smooth01(float value)
    {
        float saturated = math.saturate(value);
        return saturated * saturated * (3f - 2f * saturated);
    }
    #endregion

    #endregion
}
