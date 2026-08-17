using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
/// <summary>
/// Simulates the movement and state updates of active projectiles, handling velocity, player speed inheritance, and
/// lifetime tracking within the player controller system group.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSpawnSystem))]
public partial struct ProjectileSimulationSystem : ISystem
{
    #region Constants
    private const float MinimumFacingDirectionSquared = 1e-8f;
    private const float FacingSmoothingRate = 16f;
    #endregion

    #region Fields
    private NativeQueue<ReturnFeedbackRequest> returnFeedbackRequests;
    #endregion

    #region Methods

    #region Lifecycle

    /// <summary>
    /// Configures the system state to require updates for projectile-related components 
    /// (Projectile for velocity and inheritance settings, 
    /// ProjectileRuntimeState to tracks the projectile's traveled distance and elapsed lifetime, 
    /// ProjectileOwner to identify the shooter entity for velocity inheritance, 
    /// and ProjectileActive to filter for active projectiles that should be simulated). 
    /// This ensures that the system will only run when there are relevant entities to process, 
    /// optimizing performance by avoiding unnecessary updates when no projectiles are present or active. 
    /// </summary>
    /// <param name="state">Reference to the system state to configure.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileRuntimeState>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectileActive>();
        returnFeedbackRequests = new NativeQueue<ReturnFeedbackRequest>(Allocator.Persistent);
    }

    /// <summary>
    /// Completes pending simulation work and releases the persistent return-rumble event queue.
    /// </summary>
    /// <param name="state">System state owning scheduled projectile jobs.</param>
    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();

        if (returnFeedbackRequests.IsCreated)
            returnFeedbackRequests.Dispose();
    }

    /// <summary>
    /// Schedules the projectile simulation job to update projectile movement in parallel.
    /// </summary>
    /// <param name="state">The current system state used to manage dependencies.</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        float enemyTimeScale = 1f;
        float playerProjectileTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
        {
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);
            playerProjectileTimeScale = math.clamp(enemyGlobalTimeScale.PlayerProjectileScale, 0f, 1f);
        }

        // Create the projectile simulation job,
        // passing in delta time and component lookups.
        ReturningProjectileSimulationJob returningSimulationJob = new ReturningProjectileSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            GlobalTime = (float)SystemAPI.Time.ElapsedTime,
            EnemyTimeScale = enemyTimeScale,
            PlayerProjectileTimeScale = playerProjectileTimeScale,
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true),
            EnemyDataLookup = SystemAPI.GetComponentLookup<EnemyData>(true),
            PassiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true),
            PlayerWorldTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            BounceStateLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(true),
            ReturnFeedbackWriter = returnFeedbackRequests.AsParallelWriter()
        };
        StandardProjectileSimulationJob standardSimulationJob = new StandardProjectileSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            EnemyTimeScale = enemyTimeScale,
            PlayerProjectileTimeScale = playerProjectileTimeScale,
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true),
            EnemyDataLookup = SystemAPI.GetComponentLookup<EnemyData>(true),
            BounceStateLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(true)
        };

        // Player-capable and lightweight standard projectile archetypes are disjoint and retain dependency-safe scheduling.
        state.Dependency = returningSimulationJob.ScheduleParallel(state.Dependency);
        state.Dependency = standardSimulationJob.ScheduleParallel(state.Dependency);
        ApplyReturnFeedbackRequestsJob applyReturnFeedbackRequestsJob = new ApplyReturnFeedbackRequestsJob
        {
            Requests = returnFeedbackRequests,
            CameraShakeStateLookup = SystemAPI.GetComponentLookup<PlayerCameraShakeState>(false)
        };
        state.Dependency = applyReturnFeedbackRequestsJob.Schedule(state.Dependency);
    }

    #endregion


    #region Jobs

    /// <summary>
    /// This job Simulates the movement of active projectiles by updating their positions based on their velocities, inherited
    /// player speed, and elapsed time.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(ProjectileActive))]
    private partial struct ReturningProjectileSimulationJob : IJobEntity
    {
        #region Fields
        public float DeltaTime;
        public float GlobalTime;
        public float EnemyTimeScale;
        public float PlayerProjectileTimeScale;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;
        [ReadOnly] public ComponentLookup<EnemyData> EnemyDataLookup;
        [ReadOnly] public BufferLookup<PlayerPassiveToolsStateElement> PassiveToolsLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> PlayerWorldTransformLookup;
        [ReadOnly] public ComponentLookup<ProjectileBounceState> BounceStateLookup;
        public NativeQueue<ReturnFeedbackRequest>.ParallelWriter ReturnFeedbackWriter;
        #endregion

        #region Methods
        #region Execute
        /// <summary>
        /// Advances one player-capable projectile through outbound, turnaround, or return travel.
        /// </summary>
        /// <param name="projectileEntity">Projectile entity used by optional behavior lookups.</param>
        /// <param name="projectileTransform">Mutable projectile transform.</param>
        /// <param name="runtimeState">Mutable outbound range and lifetime state.</param>
        /// <param name="projectile">Mutable projectile behavior and velocity.</param>
        /// <param name="perfectCircleState">Mutable optional orbital trajectory state.</param>
        /// <param name="returnState">Mutable optional return state.</param>
        /// <param name="returnPath">Mutable sampled outbound path.</param>
        /// <param name="owner">Projectile owner used by time scale, inheritance, and seeking.</param>
        private void Execute(Entity projectileEntity,
                             ref LocalTransform projectileTransform,
                             ref ProjectileRuntimeState runtimeState,
                             ref Projectile projectile,
                             ref ProjectilePerfectCircleState perfectCircleState,
                             ref ProjectileReturnState returnState,
                             DynamicBuffer<ProjectileReturnPathPoint> returnPath,
                             in ProjectileOwner owner)
        {
            float projectileDeltaTime = ProjectileKinematicsUtility.ResolveOwnerScaledDeltaTime(in owner,
                                                                                                in EnemyDataLookup,
                                                                                                DeltaTime,
                                                                                                EnemyTimeScale,
                                                                                                PlayerProjectileTimeScale);

            // Return phases bypass outbound lifetime accumulation and wall-oriented facing updates.
            if (returnState.Enabled != 0 && returnState.Phase != ProjectileReturnPhase.Outbound)
            {
                ProjectileReturnRuntimeUtility.SimulateReturn(ref returnState,
                                                              ref projectile,
                                                              ref projectileTransform,
                                                              in owner,
                                                              returnPath,
                                                              in PlayerWorldTransformLookup,
                                                              projectileDeltaTime);

                if (projectileDeltaTime > 0f &&
                    ProjectileReturnRuntimeUtility.TryConsumeReturnFeedbackRequest(ref returnState,
                                                                                    out float cameraShakeMultiplier,
                                                                                    out float rumbleMultiplier))
                {
                    ReturnFeedbackWriter.Enqueue(new ReturnFeedbackRequest(owner.ShooterEntity,
                                                                           cameraShakeMultiplier,
                                                                           rumbleMultiplier));
                }

                return;
            }

            // Flight spin owns visual rotation; otherwise curved and bounced trajectories retain direction-facing smoothing.
            bool appliesFlightSpin = returnState.Enabled != 0 &&
                                     returnState.Config.SpinDuringFlight != 0 &&
                                     returnState.Config.SpinSpeedDegreesPerSecond > 0f;
            float3 outboundStartPosition = projectileTransform.Position;
            bool simulatedPerfectCircle = TrySimulatePerfectCircle(ref projectileTransform,
                                                                   ref runtimeState,
                                                                   ref projectile,
                                                                   ref perfectCircleState,
                                                                   in owner,
                                                                   !appliesFlightSpin,
                                                                   projectileDeltaTime);

            if (!simulatedPerfectCircle)
                ProjectileSimulationSystem.SimulateLinearOutbound(projectileEntity,
                                                                  ref projectileTransform,
                                                                  ref runtimeState,
                                                                  in projectile,
                                                                  in owner,
                                                                  in MovementStateLookup,
                                                                  in BounceStateLookup,
                                                                  !appliesFlightSpin,
                                                                  projectileDeltaTime);

            if (returnState.Enabled == 0)
                return;

            // Curved orbit and inherited-velocity paths need samples; straight segments only need spawn, bounce, and terminal waypoints.
            if (returnState.Config.ReturnPathMode == ProjectileReturnPathMode.RetraceOutboundPath &&
                (simulatedPerfectCircle || projectile.InheritPlayerSpeed != 0))
            {
                ProjectileReturnRuntimeUtility.RecordOutboundPoint(returnPath,
                                                                   projectileTransform.Position,
                                                                   math.max(0.01f, returnState.Config.PathSampleDistance),
                                                                   false);
            }

            ProjectileReturnRuntimeUtility.AlignFlightRotation(ref projectileTransform,
                                                                ref returnState,
                                                                projectileTransform.Position - outboundStartPosition,
                                                                projectileDeltaTime);
        }
        #endregion

        #region Perfect Circle
        /// <summary>
        /// Advances an enabled player orbital trajectory when its owner and passive snapshot remain valid.
        /// </summary>
        /// <param name="projectileTransform">Mutable projectile transform.</param>
        /// <param name="runtimeState">Mutable outbound range and lifetime state.</param>
        /// <param name="projectile">Mutable projectile behavior and frame velocity.</param>
        /// <param name="perfectCircleState">Mutable orbital trajectory state.</param>
        /// <param name="owner">Projectile owner that defines the orbit center.</param>
        /// <param name="easeFacing">Whether orbital travel may align visual facing.</param>
        /// <param name="projectileDeltaTime">Owner-scaled frame delta.</param>
        /// <returns>True when orbital movement was applied.</returns>
        private bool TrySimulatePerfectCircle(ref LocalTransform projectileTransform,
                                              ref ProjectileRuntimeState runtimeState,
                                              ref Projectile projectile,
                                              ref ProjectilePerfectCircleState perfectCircleState,
                                              in ProjectileOwner owner,
                                              bool easeFacing,
                                              float projectileDeltaTime)
        {
            if (perfectCircleState.Enabled == 0)
                return false;

            Entity shooterEntity = owner.ShooterEntity;

            if (!PassiveToolsLookup.HasBuffer(shooterEntity))
                return false;

            PlayerPassiveToolsState passiveToolsState;
            PlayerPassiveToolsStateBufferUtility.Read(shooterEntity,
                                                      in PassiveToolsLookup,
                                                      out passiveToolsState);

            if (passiveToolsState.HasPerfectCircle == 0)
                return false;

            if (!PlayerWorldTransformLookup.HasComponent(shooterEntity))
                return false;

            PerfectCirclePassiveConfig perfectCircleConfig = passiveToolsState.PerfectCircle;
            float3 shooterPosition = PlayerWorldTransformLookup[shooterEntity].Position;
            float3 perfectCircleInheritedVelocity = ResolveInheritedVelocity(in owner, in projectile);
            float3 targetPosition = ProjectilePerfectCircleTrajectoryUtility.ResolveNextPosition(ref perfectCircleState,
                                                                                                shooterPosition,
                                                                                                perfectCircleInheritedVelocity,
                                                                                                projectileTransform.Position,
                                                                                                projectileDeltaTime,
                                                                                                GlobalTime,
                                                                                                1f,
                                                                                                in perfectCircleConfig);
            ApplyResolvedStep(ref projectileTransform,
                              ref runtimeState,
                              ref projectile,
                              targetPosition,
                              easeFacing,
                              projectileDeltaTime);
            return true;
        }
        #endregion

        #region Step Resolution
        /// <summary>
        /// Applies a resolved projectile position using the exact frame displacement as the authoritative source for
        /// distance, lifetime and velocity updates. This keeps collision reconstruction aligned with non-linear paths.
        /// Used by orbital (Perfect Circle) projectiles, so it also eases the projectile facing toward its curved travel
        /// direction; otherwise the spawn-time facing would stay fixed and the attached VFX would not rotate with the orbit.
        /// </summary>
        /// <param name="projectileTransform">Projectile transform to update.</param>
        /// <param name="runtimeState">Projectile runtime state that tracks range and lifetime.</param>
        /// <param name="projectile">Projectile data that stores the authoritative frame velocity.</param>
        /// <param name="targetPosition">Final position reached by the projectile in the current frame.</param>
        /// <param name="easeFacing">Whether trajectory-facing updates may alter projectile rotation.</param>
        /// <param name="deltaTime">Owner-scaled delta time consumed by the resolved step.</param>
        private void ApplyResolvedStep(ref LocalTransform projectileTransform,
                                       ref ProjectileRuntimeState runtimeState,
                                       ref Projectile projectile,
                                       float3 targetPosition,
                                       bool easeFacing,
                                       float deltaTime)
        {
            float3 displacement = targetPosition - projectileTransform.Position;
            projectileTransform.Position = targetPosition;
            runtimeState.TraveledDistance += math.length(displacement);
            runtimeState.ElapsedLifetime += deltaTime;

            // Continuous boomerang spin owns rotation; other orbital projectiles ease toward their curved travel direction.
            if (easeFacing)
                ProjectileSimulationSystem.EaseFacingTowardDirection(ref projectileTransform, displacement, deltaTime);

            if (deltaTime > 1e-6f)
            {
                projectile.Velocity = displacement / deltaTime;
                return;
            }

            projectile.Velocity = float3.zero;
        }
        #endregion

        #region Inherited Velocity
        /// <summary>
        /// Resolves the full shooter velocity used by non-linear projectile trajectories.
        /// </summary>
        /// <param name="owner">Projectile owner containing the shooter entity reference.</param>
        /// <param name="projectile">Projectile inheritance-axis settings.</param>
        /// <returns>Shooter velocity, or zero when the shooter has no movement state.</returns>
        private float3 ResolveInheritedVelocity(in ProjectileOwner owner, in Projectile projectile)
        {
            return ProjectileKinematicsUtility.ApplyInheritedVelocityAxisMask(ProjectileKinematicsUtility.ResolveInheritedVelocity(owner.ShooterEntity, in MovementStateLookup),
                                                                             in projectile);
        }
        #endregion
        #endregion
    }

    /// <summary>
    /// Simulates projectiles whose shooters cannot own returning-projectile power-ups without attaching the larger return config or path buffer.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(ProjectileActive))]
    [WithNone(typeof(ProjectileReturnState))]
    private partial struct StandardProjectileSimulationJob : IJobEntity
    {
        #region Fields
        public float DeltaTime;
        public float EnemyTimeScale;
        public float PlayerProjectileTimeScale;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;
        [ReadOnly] public ComponentLookup<EnemyData> EnemyDataLookup;
        [ReadOnly] public ComponentLookup<ProjectileBounceState> BounceStateLookup;
        #endregion

        #region Methods
        /// <summary>
        /// Advances one lightweight projectile through its ordinary linear outbound phase.
        /// </summary>
        /// <param name="projectileEntity">Projectile entity used for optional bounce-facing lookup.</param>
        /// <param name="projectileTransform">Mutable projectile transform.</param>
        /// <param name="runtimeState">Mutable traveled-distance and lifetime state.</param>
        /// <param name="projectile">Projectile velocity and inheritance settings.</param>
        /// <param name="owner">Projectile owner used for time scale and inherited velocity.</param>
        private void Execute(Entity projectileEntity,
                             ref LocalTransform projectileTransform,
                             ref ProjectileRuntimeState runtimeState,
                             in Projectile projectile,
                             in ProjectileOwner owner)
        {
            float projectileDeltaTime = ProjectileKinematicsUtility.ResolveOwnerScaledDeltaTime(in owner,
                                                                                                in EnemyDataLookup,
                                                                                                DeltaTime,
                                                                                                EnemyTimeScale,
                                                                                                PlayerProjectileTimeScale);
            ProjectileSimulationSystem.SimulateLinearOutbound(projectileEntity,
                                                              ref projectileTransform,
                                                              ref runtimeState,
                                                              in projectile,
                                                              in owner,
                                                              in MovementStateLookup,
                                                              in BounceStateLookup,
                                                              true,
                                                              projectileDeltaTime);
        }
        #endregion
    }

    /// <summary>
    /// Coalesces parallel projectile return events into the owning player's presentation-facing feedback request.
    /// </summary>
    [BurstCompile]
    private struct ApplyReturnFeedbackRequestsJob : IJob
    {
        #region Fields
        public NativeQueue<ReturnFeedbackRequest> Requests;
        public ComponentLookup<PlayerCameraShakeState> CameraShakeStateLookup;
        #endregion

        #region Methods
        /// <summary>
        /// Drains every event after projectile simulation and retains the strongest request per player for this frame.
        /// </summary>
        public void Execute()
        {
            while (Requests.TryDequeue(out ReturnFeedbackRequest request))
            {
                if (!CameraShakeStateLookup.HasComponent(request.PlayerEntity))
                    continue;

                PlayerCameraShakeState shakeState = CameraShakeStateLookup[request.PlayerEntity];
                shakeState.ReturnCameraShakeRequestMultiplier = math.max(shakeState.ReturnCameraShakeRequestMultiplier,
                                                                          request.CameraShakeMultiplier);
                shakeState.ReturnRumbleRequestMultiplier = math.max(shakeState.ReturnRumbleRequestMultiplier,
                                                                     request.RumbleMultiplier);
                CameraShakeStateLookup[request.PlayerEntity] = shakeState;
            }
        }
        #endregion
    }
    #endregion

    #region Internal Data
    /// <summary>
    /// Carries one allocation-free return-start camera-and-haptic request from parallel simulation to its player owner.
    /// </summary>
    private readonly struct ReturnFeedbackRequest
    {
        public readonly Entity PlayerEntity;
        public readonly float CameraShakeMultiplier;
        public readonly float RumbleMultiplier;

        /// <summary>
        /// Creates one immutable player feedback request for sequential aggregation after parallel simulation.
        /// </summary>
        /// <param name="playerEntity">Player entity receiving the camera and haptic pulse.</param>
        /// <param name="cameraShakeMultiplier">Non-negative pulse strength relative to firing camera shake.</param>
        /// <param name="rumbleMultiplier">Non-negative pulse strength relative to firing rumble.</param>
        public ReturnFeedbackRequest(Entity playerEntity,
                                     float cameraShakeMultiplier,
                                     float rumbleMultiplier)
        {
            PlayerEntity = playerEntity;
            CameraShakeMultiplier = cameraShakeMultiplier;
            RumbleMultiplier = rumbleMultiplier;
        }
    }
    #endregion

    #region Shared Simulation
    /// <summary>
    /// Applies ordinary linear movement, range/lifetime accumulation, and optional bounce-facing easing.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity used for optional bounce-state lookup.</param>
    /// <param name="projectileTransform">Mutable projectile transform.</param>
    /// <param name="runtimeState">Mutable traveled-distance and lifetime state.</param>
    /// <param name="projectile">Projectile velocity and inherited-motion settings.</param>
    /// <param name="owner">Projectile owner used for inherited motion.</param>
    /// <param name="movementStateLookup">Read-only owner movement lookup.</param>
    /// <param name="bounceStateLookup">Read-only bounce-state lookup.</param>
    /// <param name="easeFacing">Whether bounce-facing updates may alter projectile rotation.</param>
    /// <param name="deltaTime">Owner-scaled frame delta.</param>
    private static void SimulateLinearOutbound(Entity projectileEntity,
                                               ref LocalTransform projectileTransform,
                                               ref ProjectileRuntimeState runtimeState,
                                               in Projectile projectile,
                                               in ProjectileOwner owner,
                                               in ComponentLookup<PlayerMovementState> movementStateLookup,
                                               in ComponentLookup<ProjectileBounceState> bounceStateLookup,
                                               bool easeFacing,
                                               float deltaTime)
    {
        float3 displacement = ProjectileKinematicsUtility.ResolveLinearDisplacement(in projectile,
                                                                                    in owner,
                                                                                    in movementStateLookup,
                                                                                    deltaTime);
        projectileTransform.Position += displacement;
        runtimeState.TraveledDistance += ProjectileKinematicsUtility.ResolveLinearRangeStepDistance(in projectile, deltaTime);
        runtimeState.ElapsedLifetime += deltaTime;

        if (easeFacing && bounceStateLookup.HasComponent(projectileEntity))
            EaseFacingTowardDirection(ref projectileTransform, projectile.Velocity, deltaTime);
    }

    /// <summary>
    /// Eases projectile facing toward horizontal travel using frame-rate-independent exponential smoothing.
    /// </summary>
    /// <param name="projectileTransform">Mutable projectile transform.</param>
    /// <param name="travelDirection">Current travel direction.</param>
    /// <param name="deltaTime">Owner-scaled frame delta.</param>
    private static void EaseFacingTowardDirection(ref LocalTransform projectileTransform,
                                                  float3 travelDirection,
                                                  float deltaTime)
    {
        float3 horizontalDirection = new float3(travelDirection.x, 0f, travelDirection.z);

        if (math.lengthsq(horizontalDirection) <= MinimumFacingDirectionSquared)
            return;

        quaternion targetRotation = quaternion.LookRotationSafe(horizontalDirection, math.up());
        projectileTransform.Rotation = math.slerp(projectileTransform.Rotation,
                                                  targetRotation,
                                                  1f - math.exp(-FacingSmoothingRate * deltaTime));
    }
    #endregion

    #endregion
}
#endregion
