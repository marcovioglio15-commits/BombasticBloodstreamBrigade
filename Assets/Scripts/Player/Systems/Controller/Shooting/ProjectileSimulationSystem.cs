using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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
    }

    /// <summary>
    /// Schedules the projectile simulation job to update projectile movement in parallel.
    /// </summary>
    /// <param name="state">The current system state used to manage dependencies.</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

        // Create the projectile simulation job,
        // passing in delta time and component lookups.
        ProjectileSimulationJob simulationJob = new ProjectileSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            GlobalTime = (float)SystemAPI.Time.ElapsedTime,
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true),
            PassiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true),
            PlayerWorldTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
        };

        // Schedule the job to run in parallel across all entities matching the query,
        state.Dependency = simulationJob.ScheduleParallel(state.Dependency);
    }

    #endregion


    #region Jobs

    /// <summary>
    /// This job Simulates the movement of active projectiles by updating their positions based on their velocities, inherited
    /// player speed, and elapsed time.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(ProjectileActive))]
    private partial struct ProjectileSimulationJob : IJobEntity
    {
        #region Constants
        // Below this squared horizontal step the facing is left untouched to avoid jitter when an orbital projectile barely moves.
        private const float MinimumFacingDisplacementSquared = 1e-8f;
        // Exponential facing-follow rate (per second): higher tracks the travel direction tighter, lower eases the radial-to-orbit turn more.
        private const float OrbitFacingSmoothingRate = 16f;
        #endregion

        #region Fields
        public float DeltaTime;
        public float GlobalTime;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;
        [ReadOnly] public BufferLookup<PlayerPassiveToolsStateElement> PassiveToolsLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> PlayerWorldTransformLookup;
        #endregion

        #region Methods
        #region Execute
        private void Execute(ref LocalTransform projectileTransform,
                             ref ProjectileRuntimeState runtimeState,
                             ref Projectile projectile,
                             ref ProjectilePerfectCircleState perfectCircleState,
                             in ProjectileOwner owner)
        {
            // If the projectile has the perfect circle behavior enabled
            // and the shooter has the perfect circle passive tool,
            // then attempt to simulate the perfect circle behavior for this projectile
            //(if successful quit).
            if (TrySimulatePerfectCircle(ref projectileTransform,
                                         ref runtimeState,
                                         ref projectile,
                                         ref perfectCircleState,
                                         in owner))
            {
                return;
            }

            // Calculate the displacement based on the velocity and delta time,
            // then update the projectile's position.
            float3 displacement = ProjectileKinematicsUtility.ResolveLinearDisplacement(in projectile,
                                                                                        in owner,
                                                                                        in MovementStateLookup,
                                                                                        DeltaTime);
            projectileTransform.Position += displacement;
            runtimeState.TraveledDistance += ProjectileKinematicsUtility.ResolveLinearRangeStepDistance(in projectile, DeltaTime);
            runtimeState.ElapsedLifetime += DeltaTime;
        }
        #endregion

        #region Perfect Circle
        private bool TrySimulatePerfectCircle(ref LocalTransform projectileTransform,
                                              ref ProjectileRuntimeState runtimeState,
                                              ref Projectile projectile,
                                              ref ProjectilePerfectCircleState perfectCircleState,
                                              in ProjectileOwner owner)
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
                                                                                                DeltaTime,
                                                                                                GlobalTime,
                                                                                                1f,
                                                                                                in perfectCircleConfig);
            ApplyResolvedStep(ref projectileTransform,
                              ref runtimeState,
                              ref projectile,
                              targetPosition);
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
        private void ApplyResolvedStep(ref LocalTransform projectileTransform,
                                       ref ProjectileRuntimeState runtimeState,
                                       ref Projectile projectile,
                                       float3 targetPosition)
        {
            float3 displacement = targetPosition - projectileTransform.Position;
            projectileTransform.Position = targetPosition;
            runtimeState.TraveledDistance += math.length(displacement);
            runtimeState.ElapsedLifetime += DeltaTime;

            // Ease facing toward the horizontal travel direction (spawn convention: +Z along motion) so the radial-to-orbit turn is smooth instead of snapping; frame-rate independent.
            float3 facingDisplacement = new float3(displacement.x, 0f, displacement.z);

            if (math.lengthsq(facingDisplacement) > MinimumFacingDisplacementSquared)
            {
                quaternion targetRotation = quaternion.LookRotationSafe(facingDisplacement, math.up());
                projectileTransform.Rotation = math.slerp(projectileTransform.Rotation,
                                                          targetRotation,
                                                          1f - math.exp(-OrbitFacingSmoothingRate * DeltaTime));
            }

            if (DeltaTime > 1e-6f)
            {
                projectile.Velocity = displacement / DeltaTime;
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
        /// <returns>Shooter velocity, or zero when the shooter has no movement state.</returns>
        private float3 ResolveInheritedVelocity(in ProjectileOwner owner, in Projectile projectile)
        {
            return ProjectileKinematicsUtility.ApplyInheritedVelocityAxisMask(ProjectileKinematicsUtility.ResolveInheritedVelocity(owner.ShooterEntity, in MovementStateLookup),
                                                                             in projectile);
        }
        #endregion
        #endregion
    }
    #endregion
}
#endregion
