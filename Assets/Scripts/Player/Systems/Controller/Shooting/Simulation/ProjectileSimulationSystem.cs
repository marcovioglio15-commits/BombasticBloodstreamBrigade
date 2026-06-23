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
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        // Create the projectile simulation job,
        // passing in delta time and component lookups.
        ProjectileSimulationJob simulationJob = new ProjectileSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            GlobalTime = (float)SystemAPI.Time.ElapsedTime,
            EnemyTimeScale = enemyTimeScale,
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true),
            EnemyDataLookup = SystemAPI.GetComponentLookup<EnemyData>(true),
            PassiveToolsLookup = SystemAPI.GetBufferLookup<PlayerPassiveToolsStateElement>(true),
            PlayerWorldTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            BounceStateLookup = SystemAPI.GetComponentLookup<ProjectileBounceState>(true)
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
        // Below this squared horizontal direction magnitude the facing is left untouched to avoid jitter when a projectile barely moves.
        private const float MinimumFacingDirectionSquared = 1e-8f;
        // Exponential facing-follow rate (per second): higher tracks the travel direction tighter, lower eases direction changes (orbit entry, wall bounce) more.
        private const float FacingSmoothingRate = 16f;
        #endregion

        #region Fields
        public float DeltaTime;
        public float GlobalTime;
        public float EnemyTimeScale;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;
        [ReadOnly] public ComponentLookup<EnemyData> EnemyDataLookup;
        [ReadOnly] public BufferLookup<PlayerPassiveToolsStateElement> PassiveToolsLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> PlayerWorldTransformLookup;
        [ReadOnly] public ComponentLookup<ProjectileBounceState> BounceStateLookup;
        #endregion

        #region Methods
        #region Execute
        private void Execute(Entity projectileEntity,
                             ref LocalTransform projectileTransform,
                             ref ProjectileRuntimeState runtimeState,
                             ref Projectile projectile,
                             ref ProjectilePerfectCircleState perfectCircleState,
                             in ProjectileOwner owner)
        {
            float projectileDeltaTime = ProjectileKinematicsUtility.ResolveOwnerScaledDeltaTime(in owner,
                                                                                                in EnemyDataLookup,
                                                                                                DeltaTime,
                                                                                                EnemyTimeScale);

            // If the projectile has the perfect circle behavior enabled
            // and the shooter has the perfect circle passive tool,
            // then attempt to simulate the perfect circle behavior for this projectile
            //(if successful quit).
            if (TrySimulatePerfectCircle(ref projectileTransform,
                                         ref runtimeState,
                                         ref projectile,
                                         ref perfectCircleState,
                                         in owner,
                                         projectileDeltaTime))
            {
                return;
            }

            // Calculate the displacement based on the velocity and delta time,
            // then update the projectile's position.
            float3 displacement = ProjectileKinematicsUtility.ResolveLinearDisplacement(in projectile,
                                                                                        in owner,
                                                                                        in MovementStateLookup,
                                                                                        projectileDeltaTime);
            projectileTransform.Position += displacement;
            runtimeState.TraveledDistance += ProjectileKinematicsUtility.ResolveLinearRangeStepDistance(in projectile, projectileDeltaTime);
            runtimeState.ElapsedLifetime += projectileDeltaTime;

            // Bouncing projectiles change direction on wall impact; ease the facing toward the new velocity so the attached VFX turns smoothly instead of snapping.
            if (BounceStateLookup.HasComponent(projectileEntity))
                EaseFacingTowardDirection(ref projectileTransform, projectile.Velocity, projectileDeltaTime);
        }
        #endregion

        #region Perfect Circle
        private bool TrySimulatePerfectCircle(ref LocalTransform projectileTransform,
                                              ref ProjectileRuntimeState runtimeState,
                                              ref Projectile projectile,
                                              ref ProjectilePerfectCircleState perfectCircleState,
                                              in ProjectileOwner owner,
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
        /// <param name="deltaTime">Owner-scaled delta time consumed by the resolved step.</param>
        private void ApplyResolvedStep(ref LocalTransform projectileTransform,
                                       ref ProjectileRuntimeState runtimeState,
                                       ref Projectile projectile,
                                       float3 targetPosition,
                                       float deltaTime)
        {
            float3 displacement = targetPosition - projectileTransform.Position;
            projectileTransform.Position = targetPosition;
            runtimeState.TraveledDistance += math.length(displacement);
            runtimeState.ElapsedLifetime += deltaTime;

            // Ease facing toward the curved travel direction so the radial-to-orbit turn is smooth instead of snapping.
            EaseFacingTowardDirection(ref projectileTransform, displacement, deltaTime);

            if (deltaTime > 1e-6f)
            {
                projectile.Velocity = displacement / deltaTime;
                return;
            }

            projectile.Velocity = float3.zero;
        }
        #endregion

        #region Facing
        /// <summary>
        /// Eases the projectile rotation toward a horizontal travel direction (spawn convention: +Z along motion), frame-rate independent.
        /// Shared by non-linear direction changes (orbit entry and wall bounce) so the attached VFX turns smoothly instead of snapping.
        /// </summary>
        /// <param name="projectileTransform">Projectile transform whose rotation is eased in place.</param>
        /// <param name="travelDirection">Current travel direction; only its horizontal component drives the facing.</param>
        /// <param name="deltaTime">Current frame delta time used for frame-rate-independent smoothing.</param>
        private static void EaseFacingTowardDirection(ref LocalTransform projectileTransform,
                                                      float3 travelDirection,
                                                      float deltaTime)
        {
            // Keep facing on the horizontal plane to match the top-down spawn orientation.
            float3 horizontalDirection = new float3(travelDirection.x, 0f, travelDirection.z);

            if (math.lengthsq(horizontalDirection) <= MinimumFacingDirectionSquared)
                return;

            quaternion targetRotation = quaternion.LookRotationSafe(horizontalDirection, math.up());
            projectileTransform.Rotation = math.slerp(projectileTransform.Rotation,
                                                      targetRotation,
                                                      1f - math.exp(-FacingSmoothingRate * deltaTime));
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
