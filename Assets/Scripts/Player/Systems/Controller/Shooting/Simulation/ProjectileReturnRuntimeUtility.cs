using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Centralizes allocation-conscious projectile return state transitions, path recording, simulation, and active-slot accounting.
/// </summary>
public static class ProjectileReturnRuntimeUtility
{
    #region Constants
    public const byte NoActiveSlot = byte.MaxValue;
    private const float MovementEpsilonSquared = 0.00000001f;
    #endregion

    #region Methods

    #region Spawn and Lifetime
    /// <summary>
    /// Resets pooled return data, seeds retrace history when needed, and registers active-slot ownership when applicable.
    /// </summary>
    /// <param name="projectileEntity">Pooled projectile being activated.</param>
    /// <param name="shooterEntity">Shooter that owns the projectile.</param>
    /// <param name="request">Source shoot request.</param>
    /// <param name="config">Resolved returning-projectile configuration.</param>
    /// <param name="isEnabled">Whether return behavior applies to this request.</param>
    /// <param name="outboundSpeed">Resolved outbound projectile speed.</param>
    /// <param name="damage">Resolved projectile damage.</param>
    /// <param name="spawnPosition">World-space spawn point.</param>
    /// <param name="returnStateLookup">Mutable return-state lookup.</param>
    /// <param name="powerUpsStateLookup">Mutable player power-up state lookup used for concurrency accounting.</param>
    /// <param name="returnPathLookup">Mutable return-path buffer lookup.</param>
    public static void InitializeSpawnedProjectile(Entity projectileEntity,
                                                   Entity shooterEntity,
                                                   in ShootRequest request,
                                                   in ReturningProjectilesConfig config,
                                                   bool isEnabled,
                                                   float outboundSpeed,
                                                   float damage,
                                                   float3 spawnPosition,
                                                   ref ComponentLookup<ProjectileReturnState> returnStateLookup,
                                                   ref ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup,
                                                   ref BufferLookup<ProjectileReturnPathPoint> returnPathLookup)
    {
        if (!returnStateLookup.HasComponent(projectileEntity) || !returnPathLookup.HasBuffer(projectileEntity))
            return;

        DynamicBuffer<ProjectileReturnPathPoint> returnPath = returnPathLookup[projectileEntity];
        returnPath.Clear();

        if (!isEnabled)
        {
            returnStateLookup[projectileEntity] = default;
            return;
        }

        if (config.ReturnPathMode == ProjectileReturnPathMode.RetraceOutboundPath)
        {
            returnPath.Add(new ProjectileReturnPathPoint
            {
                Position = spawnPosition
            });
        }

        ProjectileReturnState returnState = new ProjectileReturnState
        {
            Enabled = 1,
            Phase = ProjectileReturnPhase.Outbound,
            Config = config,
            OutboundSpeed = math.max(0.01f, outboundSpeed),
            OriginalDamage = damage,
            OriginalPenetrationMode = request.PenetrationMode,
            LastTravelDirection = math.normalizesafe(request.Direction, new float3(0f, 0f, 1f)),
            AppliedProjectileSizePowerUpMultiplier = request.ProjectileSizePowerUpMultiplier > 0f
                ? request.ProjectileSizePowerUpMultiplier
                : 1f,
            AdditionalOutboundHitsRemaining = config.OutboundHitPolicy == ProjectileOutboundHitPolicy.LimitedAdditionalHits
                ? math.max(1, config.AdditionalOutboundHits)
                : 0,
            ReturnPathIndex = -1
        };

        if (request.SpawnSource == ProjectileSpawnSource.ActivePowerUp &&
            request.ActiveSlotIndex != NoActiveSlot &&
            powerUpsStateLookup.HasComponent(shooterEntity))
        {
            PlayerPowerUpsState powerUpsState = powerUpsStateLookup[shooterEntity];

            switch (request.ActiveSlotIndex)
            {
                case 0:
                    if (powerUpsState.PrimaryReturningProjectileGeneration == 0u ||
                        powerUpsState.PrimaryReturningProjectileGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
                    {
                        powerUpsState.PrimaryReturningProjectileGeneration = PlayerPowerUpLoadoutRuntimeUtility.AdvanceReturningProjectileGeneration(powerUpsState.PrimaryReturningProjectileGeneration,
                                                                                                                                                     powerUpsState.SecondaryReturningProjectileGeneration);
                    }

                    powerUpsState.PrimaryReturningProjectileCount++;
                    returnState.ConcurrencyGeneration = powerUpsState.PrimaryReturningProjectileGeneration;
                    returnState.ConcurrencyRegistered = 1;
                    break;
                case 1:
                    if (powerUpsState.SecondaryReturningProjectileGeneration == 0u ||
                        powerUpsState.SecondaryReturningProjectileGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
                    {
                        powerUpsState.SecondaryReturningProjectileGeneration = PlayerPowerUpLoadoutRuntimeUtility.AdvanceReturningProjectileGeneration(powerUpsState.SecondaryReturningProjectileGeneration,
                                                                                                                                                       powerUpsState.PrimaryReturningProjectileGeneration);
                    }

                    powerUpsState.SecondaryReturningProjectileCount++;
                    returnState.ConcurrencyGeneration = powerUpsState.SecondaryReturningProjectileGeneration;
                    returnState.ConcurrencyRegistered = 1;
                    break;
            }

            powerUpsStateLookup[shooterEntity] = powerUpsState;
        }

        returnStateLookup[projectileEntity] = returnState;
    }

    /// <summary>
    /// Begins turnaround or direct return, replaces outbound penetration with the configured return policy, and changes scale once.
    /// </summary>
    /// <param name="returnState">Mutable return state.</param>
    /// <param name="projectile">Mutable projectile behavior.</param>
    /// <param name="perfectCircleState">Mutable orbital trajectory state disabled at transition.</param>
    /// <param name="projectileTransform">Mutable projectile transform receiving return scale.</param>
    /// <param name="returnPath">Mutable recorded path receiving the exact outbound endpoint.</param>
    /// <param name="naturalCapacityExhausted">Whether an enemy impact already consumed the last natural hit.</param>
    public static void BeginReturn(ref ProjectileReturnState returnState,
                                   ref Projectile projectile,
                                   ref ProjectilePerfectCircleState perfectCircleState,
                                   ref LocalTransform projectileTransform,
                                   DynamicBuffer<ProjectileReturnPathPoint> returnPath,
                                   bool naturalCapacityExhausted)
    {
        if (returnState.Enabled == 0 || returnState.Phase != ProjectileReturnPhase.Outbound)
            return;

        if (returnState.Config.ReturnPathMode == ProjectileReturnPathMode.RetraceOutboundPath)
        {
            RecordOutboundPoint(returnPath,
                                projectileTransform.Position,
                                math.max(0.01f, returnState.Config.PathSampleDistance),
                                true);
            returnState.ReturnPathIndex = returnPath.Length - 2;
        }
        else
        {
            returnState.ReturnPathIndex = -1;
        }

        returnState.TurnaroundDegrees = 0f;
        returnState.OutboundHitCapacityExhausted = 0;
        returnState.OutboundNaturalHitCapacityExhausted = 0;
        returnState.AdditionalOutboundHitsRemaining = 0;
        returnState.ReturnFeedbackPending = 0;
        returnState.OutboundSpeed = math.max(0.01f, math.length(projectile.Velocity));
        returnState.ReturnDelayRemainingSeconds = math.max(0f, returnState.Config.ReturnDelaySeconds);
        returnState.Phase = returnState.ReturnDelayRemainingSeconds > 0f
            ? ProjectileReturnPhase.Delaying
            : ResolvePostDelayPhase(in returnState.Config);

        if (returnState.Phase == ProjectileReturnPhase.Returning)
            MarkReturnTravelStarted(ref returnState);

        perfectCircleState.Enabled = 0;
        projectile.Velocity = float3.zero;
        projectileTransform.Scale *= math.max(0.01f, returnState.Config.ReturnSizeMultiplier) /
                                     math.max(0.01f, returnState.Config.OutboundSizeMultiplier);

        if (returnState.Config.ReturnHitPolicy == ProjectileReturnHitPolicy.CompleteReturn ||
            returnState.OriginalPenetrationMode == ProjectilePenetrationMode.Infinite)
        {
            projectile.Damage = returnState.OriginalDamage;
            projectile.PenetrationMode = ProjectilePenetrationMode.Infinite;
            projectile.RemainingPenetrations = 0;
            return;
        }

        returnState.AdditionalReturnHitsRemaining = math.max(1, returnState.Config.AdditionalReturnHits);

        if (naturalCapacityExhausted)
            ActivateAdditionalReturnHits(ref returnState, ref projectile);
    }

    /// <summary>
    /// Extends outbound travel after natural penetration is exhausted without bypassing range, lifetime, or physical collisions.
    /// </summary>
    /// <param name="returnState">Mutable return state containing the outbound hit policy and unused budget.</param>
    /// <param name="projectile">Mutable projectile receiving the configured continuation penetration.</param>
    /// <returns>True when the projectile must continue outbound travel after the current enemy hit.</returns>
    public static bool TryExtendOutboundAfterNaturalHitCapacity(ref ProjectileReturnState returnState,
                                                                ref Projectile projectile)
    {
        if (returnState.Enabled == 0 || returnState.Phase != ProjectileReturnPhase.Outbound)
            return false;

        switch (returnState.Config.OutboundHitPolicy)
        {
            case ProjectileOutboundHitPolicy.CompleteOutboundTravel:
                ActivateOutboundHitCapacity(ref returnState,
                                            ref projectile,
                                            ProjectilePenetrationMode.Infinite,
                                            0);
                return true;
            case ProjectileOutboundHitPolicy.LimitedAdditionalHits:
                if (returnState.AdditionalOutboundHitsRemaining <= 0)
                    return false;

                ActivateOutboundHitCapacity(ref returnState,
                                            ref projectile,
                                            ProjectilePenetrationMode.FixedHits,
                                            returnState.AdditionalOutboundHitsRemaining);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Converts a terminal natural return hit into the configured additional flat-hit budget when available.
    /// </summary>
    /// <param name="returnState">Mutable return state containing the unused additional budget.</param>
    /// <param name="projectile">Mutable projectile penetration and damage state.</param>
    /// <returns>True when the projectile must continue return travel with its additional hit budget.</returns>
    public static bool TryActivateAdditionalReturnHits(ref ProjectileReturnState returnState,
                                                       ref Projectile projectile)
    {
        if (returnState.Enabled == 0 ||
            returnState.Phase == ProjectileReturnPhase.Outbound ||
            returnState.Config.ReturnHitPolicy != ProjectileReturnHitPolicy.LimitedAdditionalHits ||
            returnState.AdditionalReturnHitsRemaining <= 0)
        {
            return false;
        }

        ActivateAdditionalReturnHits(ref returnState, ref projectile);
        return true;
    }

    /// <summary>
    /// Releases the active-slot live-projectile registration exactly once before final pooling.
    /// </summary>
    /// <param name="shooterEntity">Shooter that owns the active slot.</param>
    /// <param name="returnState">Mutable return state whose registration is cleared.</param>
    /// <param name="powerUpsStateLookup">Mutable player power-up state lookup.</param>
    public static void ReleaseConcurrency(Entity shooterEntity,
                                          ref ProjectileReturnState returnState,
                                          ref ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup)
    {
        if (returnState.ConcurrencyRegistered == 0 || !powerUpsStateLookup.HasComponent(shooterEntity))
            return;

        PlayerPowerUpsState powerUpsState = powerUpsStateLookup[shooterEntity];

        if (returnState.ConcurrencyGeneration == powerUpsState.PrimaryReturningProjectileGeneration)
            powerUpsState.PrimaryReturningProjectileCount = math.max(0, powerUpsState.PrimaryReturningProjectileCount - 1);
        else if (returnState.ConcurrencyGeneration == powerUpsState.SecondaryReturningProjectileGeneration)
            powerUpsState.SecondaryReturningProjectileCount = math.max(0, powerUpsState.SecondaryReturningProjectileCount - 1);

        powerUpsStateLookup[shooterEntity] = powerUpsState;
        returnState.ConcurrencyRegistered = 0;
    }
    #endregion

    #region Transition Rules
    /// <summary>
    /// Reports whether the configured orbital prerequisite has completed for a terminal outbound condition.
    /// Bounce availability is deliberately resolved only by wall collision, so range, lifetime, and hit limits stay authoritative.
    /// </summary>
    /// <param name="returnState">Current return state and interaction config.</param>
    /// <param name="perfectCircleState">Current orbital trajectory state.</param>
    /// <returns>True when a terminal outbound condition may start return travel.</returns>
    public static bool CanBeginReturn(in ProjectileReturnState returnState,
                                      in ProjectilePerfectCircleState perfectCircleState)
    {
        if (returnState.Enabled == 0 || returnState.Phase != ProjectileReturnPhase.Outbound)
            return false;

        if (ProjectileReturnPowerUpInteractionUtility.CompletesOrbitalPathBeforeReturn(in returnState.Config) &&
            perfectCircleState.Enabled != 0 &&
            perfectCircleState.CompletedFullOrbit == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reports whether normal range or lifetime limits have been reached during outbound travel.
    /// </summary>
    /// <param name="projectile">Projectile limits.</param>
    /// <param name="runtimeState">Accumulated outbound range and lifetime.</param>
    /// <returns>True when the projectile would normally despawn.</returns>
    public static bool HasReachedOutboundLimit(in Projectile projectile,
                                               in ProjectileRuntimeState runtimeState)
    {
        return projectile.MaxRange > 0f && runtimeState.TraveledDistance >= projectile.MaxRange ||
               projectile.MaxLifetime > 0f && runtimeState.ElapsedLifetime >= projectile.MaxLifetime;
    }
    #endregion

    #region Path and Rotation
    /// <summary>
    /// Records one outbound path point when forced or sufficiently distant from the latest sample.
    /// </summary>
    /// <param name="returnPath">Mutable path buffer.</param>
    /// <param name="position">World-space position to consider.</param>
    /// <param name="sampleDistance">Minimum distance between non-forced samples.</param>
    /// <param name="force">Whether the point represents an exact endpoint or bounce waypoint.</param>
    public static void RecordOutboundPoint(DynamicBuffer<ProjectileReturnPathPoint> returnPath,
                                           float3 position,
                                           float sampleDistance,
                                           bool force)
    {
        if (returnPath.Length <= 0)
        {
            returnPath.Add(new ProjectileReturnPathPoint
            {
                Position = position
            });
            return;
        }

        float3 delta = position - returnPath[returnPath.Length - 1].Position;

        if (!force && math.lengthsq(delta) < sampleDistance * sampleDistance)
            return;

        if (math.lengthsq(delta) <= MovementEpsilonSquared)
            return;

        returnPath.Add(new ProjectileReturnPathPoint
        {
            Position = position
        });
    }

    /// <summary>
    /// Realigns the projectile when its planar travel segment changes and then applies optional local-axis flight spin.
    /// This preserves the authored turnaround pose while keeping retraced bounce segments visually coherent.
    /// </summary>
    /// <param name="projectileTransform">Mutable projectile transform.</param>
    /// <param name="returnState">Mutable return state retaining the last resolved travel direction.</param>
    /// <param name="travelDirection">Current world-space travel direction, or zero while positionally stationary.</param>
    /// <param name="deltaTime">Owner-scaled frame delta.</param>
    public static void AlignFlightRotation(ref LocalTransform projectileTransform,
                                           ref ProjectileReturnState returnState,
                                           float3 travelDirection,
                                           float deltaTime)
    {
        if (returnState.Enabled == 0)
            return;

        AlignToTravelSegment(ref projectileTransform, ref returnState, travelDirection);

        if (returnState.Config.SpinDuringFlight == 0 ||
            returnState.Config.SpinSpeedDegreesPerSecond <= 0f)
            return;

        float3 axis = ResolveRotationAxis(returnState.Config.SpinAxis);
        projectileTransform.Rotation = math.mul(projectileTransform.Rotation,
                                                quaternion.AxisAngle(axis,
                                                                     math.radians(returnState.Config.SpinSpeedDegreesPerSecond * deltaTime)));
    }
    #endregion

    #region Return Simulation
    /// <summary>
    /// Simulates turnaround or return travel and marks the projectile complete after it reaches its terminal target.
    /// </summary>
    /// <param name="returnState">Mutable return state.</param>
    /// <param name="projectile">Mutable projectile velocity.</param>
    /// <param name="projectileTransform">Mutable projectile transform.</param>
    /// <param name="owner">Projectile owner used by seek mode.</param>
    /// <param name="returnPath">Recorded outbound path.</param>
    /// <param name="ownerWorldTransformLookup">Read-only owner transform lookup.</param>
    /// <param name="deltaTime">Owner-scaled frame delta.</param>
    public static void SimulateReturn(ref ProjectileReturnState returnState,
                                      ref Projectile projectile,
                                      ref LocalTransform projectileTransform,
                                      in ProjectileOwner owner,
                                      DynamicBuffer<ProjectileReturnPathPoint> returnPath,
                                      in ComponentLookup<LocalToWorld> ownerWorldTransformLookup,
                                      float deltaTime)
    {
        if (returnState.Phase == ProjectileReturnPhase.Delaying)
        {
            projectile.Velocity = float3.zero;
            returnState.ReturnDelayRemainingSeconds = math.max(0f,
                                                               returnState.ReturnDelayRemainingSeconds - math.max(0f, deltaTime));
            AlignFlightRotation(ref projectileTransform,
                                ref returnState,
                                float3.zero,
                                deltaTime);

            if (returnState.ReturnDelayRemainingSeconds <= 0f)
            {
                returnState.Phase = ResolvePostDelayPhase(in returnState.Config);

                if (returnState.Phase == ProjectileReturnPhase.Returning)
                    MarkReturnTravelStarted(ref returnState);
            }

            return;
        }

        if (returnState.Phase == ProjectileReturnPhase.Turning)
        {
            float rotationStep = math.min(180f - returnState.TurnaroundDegrees,
                                          math.max(0.01f, returnState.Config.TurnaroundRotationSpeedDegreesPerSecond) * deltaTime);
            projectileTransform.Rotation = math.mul(projectileTransform.Rotation,
                                                    quaternion.AxisAngle(ResolveRotationAxis(returnState.Config.TurnaroundAxis),
                                                                         math.radians(rotationStep)));
            returnState.TurnaroundDegrees += rotationStep;
            projectile.Velocity = float3.zero;

            if (returnState.TurnaroundDegrees >= 179.999f)
            {
                returnState.LastTravelDirection = -returnState.LastTravelDirection;
                MarkReturnTravelStarted(ref returnState);
            }

            return;
        }

        if (returnState.Phase != ProjectileReturnPhase.Returning)
            return;

        float travelBudget = math.max(0.01f, returnState.OutboundSpeed * returnState.Config.ReturnSpeedMultiplier) * deltaTime;
        float3 startPosition = projectileTransform.Position;
        float3 travelDirection;

        switch (returnState.Config.ReturnPathMode)
        {
            case ProjectileReturnPathMode.SeekPlayer:
                SimulateSeekReturn(ref returnState,
                                   ref projectileTransform,
                                   in owner,
                                   in ownerWorldTransformLookup,
                                   travelBudget,
                                   out travelDirection);
                break;
            default:
                SimulateRetraceReturn(ref returnState,
                                      ref projectileTransform,
                                      returnPath,
                                      travelBudget,
                                      out travelDirection);
                break;
        }

        float3 displacement = projectileTransform.Position - startPosition;
        projectile.Velocity = deltaTime > 0.000001f ? displacement / deltaTime : float3.zero;
        AlignFlightRotation(ref projectileTransform,
                            ref returnState,
                            travelDirection,
                            deltaTime);
    }

    /// <summary>
    /// Consumes one return-start feedback event after the projectile has entered real return travel.
    /// </summary>
    /// <param name="returnState">Mutable return state carrying the single-consumption event marker.</param>
    /// <param name="cameraShakeMultiplier">Resolved non-negative multiplier relative to the player's firing shake.</param>
    /// <param name="rumbleMultiplier">Resolved non-negative multiplier relative to the player's firing rumble.</param>
    /// <returns>True when camera or haptic feedback must be delivered to the projectile owner.</returns>
    public static bool TryConsumeReturnFeedbackRequest(ref ProjectileReturnState returnState,
                                                       out float cameraShakeMultiplier,
                                                       out float rumbleMultiplier)
    {
        cameraShakeMultiplier = 0f;
        rumbleMultiplier = 0f;

        if (returnState.ReturnFeedbackPending == 0)
            return false;

        returnState.ReturnFeedbackPending = 0;
        cameraShakeMultiplier = math.max(0f, returnState.Config.ReturnCameraShakeMultiplier);
        rumbleMultiplier = math.max(0f, returnState.Config.ReturnRumbleMultiplier);
        return cameraShakeMultiplier > 0f || rumbleMultiplier > 0f;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves whether a projectile starts with a visual 180-degree turn or can immediately travel backward.
    /// </summary>
    /// <param name="config">Return configuration containing continuous-spin settings.</param>
    /// <returns>Turning when continuous spin is unavailable; otherwise Returning.</returns>
    private static ProjectileReturnPhase ResolvePostDelayPhase(in ReturningProjectilesConfig config)
    {
        return config.SpinDuringFlight != 0 && config.SpinSpeedDegreesPerSecond > 0f
            ? ProjectileReturnPhase.Returning
            : ProjectileReturnPhase.Turning;
    }

    /// <summary>
    /// Enters return travel and marks its optional camera and haptic pulse for one-time owner delivery.
    /// </summary>
    /// <param name="returnState">Mutable projectile state entering return travel.</param>
    private static void MarkReturnTravelStarted(ref ProjectileReturnState returnState)
    {
        returnState.Phase = ProjectileReturnPhase.Returning;
        returnState.ReturnFeedbackPending = returnState.Config.ReturnCameraShakeMultiplier > 0f ||
                                            returnState.Config.ReturnRumbleMultiplier > 0f
            ? (byte)1
            : (byte)0;
    }

    /// <summary>
    /// Replaces exhausted natural penetration with one outbound continuation policy while preserving full hit damage.
    /// </summary>
    /// <param name="returnState">Mutable state recording natural-capacity exhaustion and consuming the authored budget.</param>
    /// <param name="projectile">Mutable projectile receiving the continuation penetration mode.</param>
    /// <param name="penetrationMode">Infinite or fixed-hit continuation selected by the outbound policy.</param>
    /// <param name="hitCount">Total additional enemy hits available to a fixed-hit continuation.</param>
    private static void ActivateOutboundHitCapacity(ref ProjectileReturnState returnState,
                                                    ref Projectile projectile,
                                                    ProjectilePenetrationMode penetrationMode,
                                                    int hitCount)
    {
        returnState.OutboundNaturalHitCapacityExhausted = 1;
        returnState.AdditionalOutboundHitsRemaining = 0;
        projectile.Damage = returnState.OriginalDamage;
        projectile.PenetrationMode = penetrationMode;
        projectile.RemainingPenetrations = penetrationMode == ProjectilePenetrationMode.FixedHits
            ? math.max(0, hitCount - 1)
            : 0;
    }

    /// <summary>
    /// Replaces exhausted natural penetration with a fixed number of full-damage return hits.
    /// </summary>
    /// <param name="returnState">Mutable return state containing the remaining configured budget.</param>
    /// <param name="projectile">Mutable projectile receiving fixed-hit penetration.</param>
    private static void ActivateAdditionalReturnHits(ref ProjectileReturnState returnState,
                                                     ref Projectile projectile)
    {
        projectile.Damage = returnState.OriginalDamage;
        projectile.PenetrationMode = ProjectilePenetrationMode.FixedHits;
        projectile.RemainingPenetrations = math.max(0, returnState.AdditionalReturnHitsRemaining - 1);
        returnState.AdditionalReturnHitsRemaining = 0;
    }

    /// <summary>
    /// Advances a direct return toward the current owner position.
    /// </summary>
    /// <param name="returnState">Mutable return state.</param>
    /// <param name="projectileTransform">Mutable projectile transform.</param>
    /// <param name="owner">Projectile owner.</param>
    /// <param name="ownerWorldTransformLookup">Read-only owner transform lookup.</param>
    /// <param name="travelBudget">Maximum frame travel distance.</param>
    private static void SimulateSeekReturn(ref ProjectileReturnState returnState,
                                           ref LocalTransform projectileTransform,
                                           in ProjectileOwner owner,
                                           in ComponentLookup<LocalToWorld> ownerWorldTransformLookup,
                                           float travelBudget,
                                           out float3 travelDirection)
    {
        travelDirection = float3.zero;

        if (!ownerWorldTransformLookup.HasComponent(owner.ShooterEntity))
        {
            returnState.ReturnFeedbackPending = 0;
            returnState.Phase = ProjectileReturnPhase.Completed;
            return;
        }

        float3 targetPosition = ownerWorldTransformLookup[owner.ShooterEntity].Position;
        float3 targetDelta = targetPosition - projectileTransform.Position;
        float targetDistance = math.length(targetDelta);

        if (targetDistance <= math.max(0.01f, returnState.Config.ReturnCompletionDistance))
        {
            returnState.ReturnFeedbackPending = 0;
            projectileTransform.Position = targetPosition;
            returnState.Phase = ProjectileReturnPhase.Completed;
            return;
        }

        travelDirection = math.normalizesafe(targetDelta);
        projectileTransform.Position += math.normalizesafe(targetDelta) * math.min(travelBudget, targetDistance);
    }

    /// <summary>
    /// Consumes a frame travel budget across recorded path segments in reverse order.
    /// </summary>
    /// <param name="returnState">Mutable return state containing the current path index.</param>
    /// <param name="projectileTransform">Mutable projectile transform.</param>
    /// <param name="returnPath">Recorded outbound path.</param>
    /// <param name="travelBudget">Maximum frame travel distance.</param>
    private static void SimulateRetraceReturn(ref ProjectileReturnState returnState,
                                              ref LocalTransform projectileTransform,
                                              DynamicBuffer<ProjectileReturnPathPoint> returnPath,
                                              float travelBudget,
                                              out float3 travelDirection)
    {
        travelDirection = float3.zero;

        if (returnState.ReturnPathIndex < 0)
            returnState.ReturnFeedbackPending = 0;

        while (travelBudget > 0f && returnState.ReturnPathIndex >= 0)
        {
            float3 targetPosition = returnPath[returnState.ReturnPathIndex].Position;
            float3 targetDelta = targetPosition - projectileTransform.Position;
            float targetDistance = math.length(targetDelta);

            if (targetDistance <= math.max(0.01f, returnState.Config.ReturnCompletionDistance))
            {
                projectileTransform.Position = targetPosition;
                returnState.ReturnPathIndex--;
                continue;
            }

            float appliedDistance = math.min(travelBudget, targetDistance);
            travelDirection = math.normalizesafe(targetDelta);
            projectileTransform.Position += travelDirection * appliedDistance;
            travelBudget -= appliedDistance;

            if (appliedDistance >= targetDistance - 0.000001f)
                returnState.ReturnPathIndex--;
        }

        if (returnState.ReturnPathIndex < 0)
            returnState.Phase = ProjectileReturnPhase.Completed;
    }

    /// <summary>
    /// Resolves one local rotation axis without branches at call sites.
    /// </summary>
    /// <param name="axis">Configured vertical or horizontal axis.</param>
    /// <returns>Unit local axis.</returns>
    private static float3 ResolveRotationAxis(ProjectileReturnRotationAxis axis)
    {
        switch (axis)
        {
            case ProjectileReturnRotationAxis.Horizontal:
                return new float3(1f, 0f, 0f);
            default:
                return new float3(0f, 1f, 0f);
        }
    }

    /// <summary>
    /// Applies only the world-space yaw delta between consecutive travel segments, preserving local spin and turnaround roll.
    /// </summary>
    /// <param name="projectileTransform">Mutable projectile transform receiving the segment yaw.</param>
    /// <param name="returnState">Mutable return state retaining the normalized previous segment direction.</param>
    /// <param name="travelDirection">Current world-space travel direction.</param>
    private static void AlignToTravelSegment(ref LocalTransform projectileTransform,
                                             ref ProjectileReturnState returnState,
                                             float3 travelDirection)
    {
        float3 currentDirection = new float3(travelDirection.x, 0f, travelDirection.z);

        if (math.lengthsq(currentDirection) <= MovementEpsilonSquared)
            return;

        currentDirection = math.normalize(currentDirection);
        float3 previousDirection = new float3(returnState.LastTravelDirection.x,
                                              0f,
                                              returnState.LastTravelDirection.z);

        if (math.lengthsq(previousDirection) <= MovementEpsilonSquared)
        {
            returnState.LastTravelDirection = currentDirection;
            return;
        }

        previousDirection = math.normalize(previousDirection);
        float signedRadians = math.atan2(math.cross(previousDirection, currentDirection).y,
                                         math.clamp(math.dot(previousDirection, currentDirection), -1f, 1f));
        projectileTransform.Rotation = math.mul(quaternion.AxisAngle(math.up(), signedRadians),
                                                projectileTransform.Rotation);
        returnState.LastTravelDirection = currentDirection;
    }
    #endregion

    #endregion
}
