using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides shared projectile motion helpers so simulation, collision and debug rendering use the same inherited-velocity rules.
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
    /// </summary>
    /// <param name="projectile">Projectile data containing the authored propulsion velocity and inheritance flag.</param>
    /// <param name="owner">Projectile owner used to locate the shooter movement state.</param>
    /// <param name="movementStateLookup">Read-only movement lookup used by Burst systems.</param>
    /// <returns>Projectile propulsion velocity plus full shooter velocity when inheritance is enabled.</returns>
    public static float3 ResolveLinearVelocity(in Projectile projectile,
                                               in ProjectileOwner owner,
                                               in ComponentLookup<PlayerMovementState> movementStateLookup)
    {
        float3 velocity = projectile.Velocity;

        if (projectile.InheritPlayerSpeed != 0)
            velocity += ApplyInheritedVelocityAxisMask(ResolveInheritedVelocity(owner.ShooterEntity, in movementStateLookup), in projectile);

        return velocity;
    }

    /// <summary>
    /// Resolves the current world-space velocity used by debug-only projectile rendering.
    /// </summary>
    /// <param name="projectile">Projectile data containing the authored propulsion velocity and inheritance flag.</param>
    /// <param name="owner">Projectile owner used to locate the shooter movement state.</param>
    /// <param name="entityManager">Entity manager used by managed debug rendering.</param>
    /// <returns>Projectile propulsion velocity plus full shooter velocity when inheritance is enabled.</returns>
    public static float3 ResolveLinearVelocity(in Projectile projectile,
                                               in ProjectileOwner owner,
                                               EntityManager entityManager)
    {
        float3 velocity = projectile.Velocity;

        if (projectile.InheritPlayerSpeed != 0)
            velocity += ApplyInheritedVelocityAxisMask(ResolveInheritedVelocity(owner.ShooterEntity, entityManager), in projectile);

        return velocity;
    }

    /// <summary>
    /// Resolves the full shooter velocity that should be inherited by projectile world motion.
    /// </summary>
    /// <param name="shooterEntity">Shooter entity that may own PlayerMovementState.</param>
    /// <param name="movementStateLookup">Read-only movement lookup used by Burst systems.</param>
    /// <returns>Shooter velocity, or zero when the shooter is not readable.</returns>
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
    /// </summary>
    /// <param name="shooterEntity">Shooter entity that may own PlayerMovementState.</param>
    /// <param name="entityManager">Entity manager used to inspect the live world.</param>
    /// <returns>Shooter velocity, or zero when the shooter is not readable.</returns>
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

    /// <summary>
    /// Applies per-projectile inherited velocity axis masks after reading the shooter movement velocity.
    /// </summary>
    /// <param name="inheritedVelocity">Full shooter velocity before projectile-specific masking.</param>
    /// <param name="projectile">Projectile data carrying inherited velocity mask flags.</param>
    /// <returns>Masked inherited velocity.</returns>
    public static float3 ApplyInheritedVelocityAxisMask(float3 inheritedVelocity, in Projectile projectile)
    {
        if (projectile.IgnoreInheritedPlayerVelocityX != 0)
            inheritedVelocity.x = 0f;

        if (projectile.IgnoreInheritedPlayerVelocityZ != 0)
            inheritedVelocity.z = 0f;

        return inheritedVelocity;
    }
    #endregion

    #region Displacement
    /// <summary>
    /// Resolves the frame delta time for a projectile, applying enemy Bullet Time only to enemy-owned shots.
    /// Player projectiles keep the unscaled controller delta so active power-ups never slow the player weapon stream.
    /// </summary>
    /// <param name="owner">Projectile owner used to identify the shooter entity.</param>
    /// <param name="enemyDataLookup">Read-only lookup that marks live enemy shooter entities.</param>
    /// <param name="deltaTime">Unscaled frame delta time from the owning system.</param>
    /// <param name="enemyTimeScale">Current enemy global time scale resolved from Bullet Time.</param>
    /// <returns>Delta time to use for this projectile simulation step.</returns>
    public static float ResolveOwnerScaledDeltaTime(in ProjectileOwner owner,
                                                    in ComponentLookup<EnemyData> enemyDataLookup,
                                                    float deltaTime,
                                                    float enemyTimeScale)
    {
        float safeDeltaTime = math.max(0f, deltaTime);

        if (!IsEnemyOwnedProjectile(owner.ShooterEntity, in enemyDataLookup))
            return safeDeltaTime;

        return safeDeltaTime * math.clamp(enemyTimeScale, 0f, 1f);
    }

    /// <summary>
    /// Checks whether the projectile shooter is an enemy entity that should inherit enemy global time scaling.
    /// </summary>
    /// <param name="shooterEntity">Shooter entity stored on the projectile owner.</param>
    /// <param name="enemyDataLookup">Read-only lookup that marks entities baked as enemies.</param>
    /// <returns>True when the projectile belongs to an enemy shooter.</returns>
    public static bool IsEnemyOwnedProjectile(Entity shooterEntity,
                                             in ComponentLookup<EnemyData> enemyDataLookup)
    {
        if (!IsEntityUsable(shooterEntity))
            return false;

        return enemyDataLookup.HasComponent(shooterEntity);
    }

    /// <summary>
    /// Resolves the world-space displacement applied by one linear simulation frame.
    /// </summary>
    /// <param name="projectile">Projectile data containing propulsion velocity and inheritance settings.</param>
    /// <param name="owner">Projectile owner used to locate shooter velocity.</param>
    /// <param name="movementStateLookup">Read-only movement lookup used by Burst systems.</param>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <returns>World-space displacement for this frame.</returns>
    public static float3 ResolveLinearDisplacement(in Projectile projectile,
                                                   in ProjectileOwner owner,
                                                   in ComponentLookup<PlayerMovementState> movementStateLookup,
                                                   float deltaTime)
    {
        return ResolveLinearVelocity(in projectile, in owner, in movementStateLookup) * deltaTime;
    }

    /// <summary>
    /// Resolves the distance that should consume projectile range during one linear simulation frame.
    /// </summary>
    /// <param name="projectile">Projectile data containing the authored propulsion velocity.</param>
    /// <param name="deltaTime">Frame delta time in seconds.</param>
    /// <returns>Propulsion-only travel distance, excluding inherited shooter drift.</returns>
    public static float ResolveLinearRangeStepDistance(in Projectile projectile,
                                                       float deltaTime)
    {
        return math.length(projectile.Velocity * deltaTime);
    }
    #endregion

    #region Range
    /// <summary>
    /// Resolves how many seconds remain before a linear projectile exhausts its configured range.
    /// </summary>
    /// <param name="projectile">Projectile data containing max range and propulsion speed.</param>
    /// <param name="traveledDistance">Propulsion-only distance already consumed.</param>
    /// <returns>Remaining range time, or positive infinity when range is disabled.</returns>
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
    /// </summary>
    /// <param name="entity">Entity value to validate.</param>
    /// <returns>True when the entity is non-null and not deferred.</returns>
    private static bool IsEntityUsable(Entity entity)
    {
        if (entity == Entity.Null)
            return false;

        return entity.Index >= 0;
    }
    #endregion

    #endregion
}
