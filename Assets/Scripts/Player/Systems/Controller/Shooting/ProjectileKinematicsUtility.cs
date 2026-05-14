using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared projectile motion helpers so simulation, collision and debug rendering use the same inherited-velocity rules.
/// /params None.
/// /returns None.
/// </summary>
public static class ProjectileKinematicsUtility
{
    #region Constants
    private const float VelocityEpsilonSquared = 1e-6f;
    #endregion

    #region Methods

    #region Velocity
    /// <summary>
    /// Resolves the current world-space velocity used by a linear projectile this frame.
    /// /params projectile Projectile data containing the authored propulsion velocity and inheritance flag.
    /// /params owner Projectile owner used to locate the shooter movement state.
    /// /params movementStateLookup Read-only movement lookup used by Burst systems.
    /// /returns Projectile propulsion velocity plus full shooter velocity when inheritance is enabled.
    /// </summary>
    public static float3 ResolveLinearVelocity(in Projectile projectile,
                                               in ProjectileOwner owner,
                                               in ComponentLookup<PlayerMovementState> movementStateLookup)
    {
        float3 velocity = projectile.Velocity;

        if (projectile.InheritPlayerSpeed != 0)
            velocity += ResolveInheritedVelocity(owner.ShooterEntity, in movementStateLookup);

        return velocity;
    }

    /// <summary>
    /// Resolves the current world-space velocity used by debug-only projectile rendering.
    /// /params projectile Projectile data containing the authored propulsion velocity and inheritance flag.
    /// /params owner Projectile owner used to locate the shooter movement state.
    /// /params entityManager Entity manager used by managed debug rendering.
    /// /returns Projectile propulsion velocity plus full shooter velocity when inheritance is enabled.
    /// </summary>
    public static float3 ResolveLinearVelocity(in Projectile projectile,
                                               in ProjectileOwner owner,
                                               EntityManager entityManager)
    {
        float3 velocity = projectile.Velocity;

        if (projectile.InheritPlayerSpeed != 0)
            velocity += ResolveInheritedVelocity(owner.ShooterEntity, entityManager);

        return velocity;
    }

    /// <summary>
    /// Resolves the full shooter velocity that should be inherited by projectile world motion.
    /// /params shooterEntity Shooter entity that may own PlayerMovementState.
    /// /params movementStateLookup Read-only movement lookup used by Burst systems.
    /// /returns Shooter velocity, or zero when the shooter is not readable.
    /// </summary>
    public static float3 ResolveInheritedVelocity(Entity shooterEntity,
                                                  in ComponentLookup<PlayerMovementState> movementStateLookup)
    {
        if (!IsEntityUsable(shooterEntity))
            return float3.zero;

        if (!movementStateLookup.HasComponent(shooterEntity))
            return float3.zero;

        return movementStateLookup[shooterEntity].Velocity;
    }

    /// <summary>
    /// Resolves the full shooter velocity that should be inherited by managed debug rendering.
    /// /params shooterEntity Shooter entity that may own PlayerMovementState.
    /// /params entityManager Entity manager used to inspect the live world.
    /// /returns Shooter velocity, or zero when the shooter is not readable.
    /// </summary>
    public static float3 ResolveInheritedVelocity(Entity shooterEntity,
                                                  EntityManager entityManager)
    {
        if (!IsEntityUsable(shooterEntity))
            return float3.zero;

        if (!entityManager.Exists(shooterEntity))
            return float3.zero;

        if (!entityManager.HasComponent<PlayerMovementState>(shooterEntity))
            return float3.zero;

        return entityManager.GetComponentData<PlayerMovementState>(shooterEntity).Velocity;
    }
    #endregion

    #region Displacement
    /// <summary>
    /// Resolves the world-space displacement applied by one linear simulation frame.
    /// /params projectile Projectile data containing propulsion velocity and inheritance settings.
    /// /params owner Projectile owner used to locate shooter velocity.
    /// /params movementStateLookup Read-only movement lookup used by Burst systems.
    /// /params deltaTime Frame delta time in seconds.
    /// /returns World-space displacement for this frame.
    /// </summary>
    public static float3 ResolveLinearDisplacement(in Projectile projectile,
                                                   in ProjectileOwner owner,
                                                   in ComponentLookup<PlayerMovementState> movementStateLookup,
                                                   float deltaTime)
    {
        return ResolveLinearVelocity(in projectile, in owner, in movementStateLookup) * deltaTime;
    }

    /// <summary>
    /// Resolves the distance that should consume projectile range during one linear simulation frame.
    /// /params projectile Projectile data containing the authored propulsion velocity.
    /// /params deltaTime Frame delta time in seconds.
    /// /returns Propulsion-only travel distance, excluding inherited shooter drift.
    /// </summary>
    public static float ResolveLinearRangeStepDistance(in Projectile projectile,
                                                       float deltaTime)
    {
        return math.length(projectile.Velocity * deltaTime);
    }
    #endregion

    #region Range
    /// <summary>
    /// Resolves how many seconds remain before a linear projectile exhausts its configured range.
    /// /params projectile Projectile data containing max range and propulsion speed.
    /// /params traveledDistance Propulsion-only distance already consumed.
    /// /returns Remaining range time, or positive infinity when range is disabled.
    /// </summary>
    public static float ResolveRemainingRangeSeconds(in Projectile projectile,
                                                     float traveledDistance)
    {
        if (projectile.MaxRange <= 0f)
            return float.PositiveInfinity;

        float propulsionSpeed = math.length(projectile.Velocity);

        if (propulsionSpeed * propulsionSpeed <= VelocityEpsilonSquared)
            return 0f;

        float remainingRange = math.max(0f, projectile.MaxRange - traveledDistance);
        return remainingRange / propulsionSpeed;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Checks whether an ECS entity value can be used for lookup access.
    /// /params entity Entity value to validate.
    /// /returns True when the entity is non-null and not deferred.
    /// </summary>
    private static bool IsEntityUsable(Entity entity)
    {
        if (entity == Entity.Null)
            return false;

        return entity.Index >= 0;
    }
    #endregion

    #endregion
}
