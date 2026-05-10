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
            PassiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true),
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
        #region Fields
        public float DeltaTime;
        public float GlobalTime;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;
        [ReadOnly] public ComponentLookup<PlayerPassiveToolsState> PassiveToolsLookup;
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

            float3 velocity = projectile.Velocity;

            // If the projectile is set to inherit player speed,
            // calculate the inherited velocity and add it to the projectile's velocity.
            if (projectile.InheritPlayerSpeed != 0)
                velocity += ResolveInheritedVelocity(projectile, owner);

            // Calculate the displacement based on the velocity and delta time,
            // then update the projectile's position.
            float3 displacement = velocity * DeltaTime;
            projectileTransform.Position += displacement;
            runtimeState.TraveledDistance += math.length(displacement);
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

            if (!PassiveToolsLookup.HasComponent(shooterEntity))
                return false;

            PlayerPassiveToolsState passiveToolsState = PassiveToolsLookup[shooterEntity];

            if (passiveToolsState.HasPerfectCircle == 0)
                return false;

            if (!PlayerWorldTransformLookup.HasComponent(shooterEntity))
                return false;

            PerfectCirclePassiveConfig perfectCircleConfig = passiveToolsState.PerfectCircle;
            float3 shooterPosition = PlayerWorldTransformLookup[shooterEntity].Position;
            float3 perfectCircleInheritedVelocity = ResolveFullInheritedVelocity(owner);
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
        /// Calculates the Velocity inherited by a projectile from its shooter, excluding any component in the direction
        /// of the projectile's own velocity.
        /// </summary>
        /// <param name="projectile">The projectile whose inherited velocity is being resolved.</param>
        /// <param name="owner">The owner of the projectile (shooter entity).</param>
        /// <returns>The inherited velocity vector for the projectile.<returns>
        private float3 ResolveInheritedVelocity(in Projectile projectile, in ProjectileOwner owner)
        {
            // If the shooter entity does not have a PlayerMovementState component, return zero velocity.
            if (MovementStateLookup.HasComponent(owner.ShooterEntity) == false)
                return float3.zero;

            // Get the shooter's full movement velocity (including vertical component).
            float3 inheritedVelocity = MovementStateLookup[owner.ShooterEntity].Velocity;

            // Remove any component of the inherited velocity that is in the direction
            // of the projectile's own velocity.
            float speedSquared = math.lengthsq(projectile.Velocity); // Calculate the squared speed of the projectile to use for projection.
            // Only perform the projection if the projectile has a non-negligible speed to avoid division by zero.
            if (speedSquared > 1e-6f)
            {
                // Calculate the projection of the inherited velocity onto the projectile's velocity and subtract it from the inherited velocity.
                float projectionScale = math.dot(inheritedVelocity, projectile.Velocity) / speedSquared;
                inheritedVelocity -= projectile.Velocity * projectionScale;
            }

            return inheritedVelocity; // Return the final inherited velocity after removing the component in the direction of the projectile's velocity.
        }

        private float3 ResolveFullInheritedVelocity(in ProjectileOwner owner)
        {
            // If the shooter entity does not have a PlayerMovementState component, return zero velocity.
            if (MovementStateLookup.HasComponent(owner.ShooterEntity) == false)
                return float3.zero;

            return MovementStateLookup[owner.ShooterEntity].Velocity;
        }
        #endregion
        #endregion
    }
    #endregion
}
#endregion
