using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Centralizes enemy knockback application, decay and movement composition for projectile hits.
/// </summary>
public static class EnemyKnockbackRuntimeUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumRemainingTime = 1e-4f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one projectile knockback payload onto the target enemy state.
    /// </summary>
    /// <param name="projectile">Projectile data carrying the authored knockback payload.</param>
    /// <param name="projectileTransform">Current projectile transform used to derive hit direction when requested.</param>
    /// <param name="enemyPosition">Current enemy position used to derive hit-to-target knockback direction.</param>
    /// <param name="knockbackState">Mutable target knockback state.</param>
    /// <returns>True when a valid knockback payload was applied.</returns>
    public static bool TryApplyFromProjectile(in Projectile projectile,
                                              in LocalTransform projectileTransform,
                                              float3 enemyPosition,
                                              ref EnemyKnockbackState knockbackState)
    {
        if (projectile.KnockbackEnabled == 0)
            return false;

        float strength = math.max(0f, projectile.KnockbackStrength);
        float durationSeconds = math.max(0f, projectile.KnockbackDurationSeconds);

        if (strength <= 0f || durationSeconds <= 0f)
            return false;

        float3 direction = ResolveKnockbackDirection(in projectile, in projectileTransform, enemyPosition);

        if (math.lengthsq(direction) <= DirectionEpsilon)
            return false;

        float3 desiredVelocity = direction * strength;

        switch (projectile.KnockbackStackingMode)
        {
            case ProjectileKnockbackStackingMode.Add:
                knockbackState.Velocity += desiredVelocity;
                knockbackState.Velocity.y = 0f;
                knockbackState.RemainingTime = math.max(knockbackState.RemainingTime, durationSeconds);
                return true;
            case ProjectileKnockbackStackingMode.KeepStrongest:
                if (!IsActive(in knockbackState) ||
                    math.lengthsq(desiredVelocity) > math.lengthsq(knockbackState.Velocity))
                {
                    knockbackState.Velocity = desiredVelocity;
                    knockbackState.RemainingTime = durationSeconds;
                    return true;
                }

                return false;
            default:
                knockbackState.Velocity = desiredVelocity;
                knockbackState.RemainingTime = durationSeconds;
                return true;
        }
    }

    /// <summary>
    /// Advances the active knockback state, resolves wall collisions and applies the resulting displacement.
    /// </summary>
    /// <param name="knockbackState">Mutable knockback state to decay and integrate.</param>
    /// <param name="position">Mutable enemy position that receives the knockback displacement.</param>
    /// <param name="collisionRadius">Effective enemy collision radius used for wall blocking checks.</param>
    /// <param name="physicsWorldSingleton">Physics world used for wall collision resolution.</param>
    /// <param name="wallsLayerMask">Resolved wall layer mask.</param>
    /// <param name="wallsEnabled">True when wall blocking should be evaluated.</param>
    /// <param name="deltaTime">Simulation delta time for the current frame.</param>
    public static void ApplyDisplacement(ref EnemyKnockbackState knockbackState,
                                         ref float3 position,
                                         float collisionRadius,
                                         in PhysicsWorldSingleton physicsWorldSingleton,
                                         int wallsLayerMask,
                                         bool wallsEnabled,
                                         float deltaTime)
    {
        if (!IsActive(in knockbackState))
        {
            Clear(ref knockbackState);
            return;
        }

        if (deltaTime <= 0f)
            return;

        float3 planarVelocity = knockbackState.Velocity;
        planarVelocity.y = 0f;
        float3 desiredDisplacement = planarVelocity * deltaTime;

        if (wallsEnabled && math.lengthsq(desiredDisplacement) > DirectionEpsilon)
        {
            bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                   position,
                                                                                   desiredDisplacement,
                                                                                   collisionRadius,
                                                                                   wallsLayerMask,
                                                                                   out float3 allowedDisplacement,
                                                                                   out float3 hitNormal);
            position += allowedDisplacement;

            if (hitWall)
                planarVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(planarVelocity, hitNormal);
        }
        else
        {
            position += desiredDisplacement;
        }

        float previousRemainingTime = math.max(MinimumRemainingTime, knockbackState.RemainingTime);
        float nextRemainingTime = math.max(0f, knockbackState.RemainingTime - deltaTime);

        if (nextRemainingTime <= 0f || math.lengthsq(planarVelocity) <= DirectionEpsilon)
        {
            Clear(ref knockbackState);
            return;
        }

        planarVelocity *= nextRemainingTime / previousRemainingTime;
        planarVelocity.y = 0f;
        knockbackState.Velocity = planarVelocity;
        knockbackState.RemainingTime = nextRemainingTime;
    }

    /// <summary>
    /// Combines authored movement velocity and current knockback velocity for look and occupancy calculations.
    /// </summary>
    /// <param name="baseVelocity">Current non-knockback enemy velocity.</param>
    /// <param name="knockbackState">Current knockback state.</param>
    /// <returns>Combined planar velocity.</returns>
    public static float3 ResolveCombinedVelocity(float3 baseVelocity, in EnemyKnockbackState knockbackState)
    {
        if (!IsActive(in knockbackState))
            return baseVelocity;

        float3 combinedVelocity = baseVelocity + knockbackState.Velocity;
        combinedVelocity.y = 0f;
        return combinedVelocity;
    }

    /// <summary>
    /// Resolves whether the current knockback state should still influence movement.
    /// </summary>
    /// <param name="knockbackState">Knockback state to inspect.</param>
    /// <returns>True when the state still has time and meaningful planar velocity.</returns>
    public static bool IsActive(in EnemyKnockbackState knockbackState)
    {
        return knockbackState.RemainingTime > MinimumRemainingTime &&
               math.lengthsq(knockbackState.Velocity) > DirectionEpsilon;
    }

    /// <summary>
    /// Clears the knockback state immediately.
    /// </summary>
    /// <param name="knockbackState">Mutable knockback state to reset.</param>
    public static void Clear(ref EnemyKnockbackState knockbackState)
    {
        knockbackState.Velocity = float3.zero;
        knockbackState.RemainingTime = 0f;
    }
    #endregion

    #region Private Methods
    private static float3 ResolveKnockbackDirection(in Projectile projectile,
                                                    in LocalTransform projectileTransform,
                                                    float3 enemyPosition)
    {
        float3 preferredDirection;

        switch (projectile.KnockbackDirectionMode)
        {
            case ProjectileKnockbackDirectionMode.HitToTarget:
                preferredDirection = enemyPosition - projectileTransform.Position;
                break;
            default:
                preferredDirection = projectile.Velocity;
                break;
        }

        preferredDirection.y = 0f;

        if (math.lengthsq(preferredDirection) > DirectionEpsilon)
            return math.normalizesafe(preferredDirection, float3.zero);

        float3 fallbackDirection = projectile.Velocity;
        fallbackDirection.y = 0f;
        return math.normalizesafe(fallbackDirection, float3.zero);
    }
    #endregion

    #endregion
}
