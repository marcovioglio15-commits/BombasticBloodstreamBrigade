using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Shared helper methods used to resolve split-projectile generation across hit and despawn paths.
/// </summary>
public static class ProjectileSplitUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public API
    public static bool ShouldSplitOnHitEvent(in ProjectileSplitState splitState, bool enemyKilledByProjectile)
    {
        if (splitState.CanSplit == 0)
            return false;

        switch (splitState.TriggerMode)
        {
            case ProjectileSplitTriggerMode.OnEnemyKilled:
                return enemyKilledByProjectile;
            case ProjectileSplitTriggerMode.OnEnemyHit:
                return true;
            default:
                return false;
        }
    }

    public static bool ShouldSplitOnDespawn(in ProjectileSplitState splitState)
    {
        if (splitState.CanSplit == 0)
            return false;

        return splitState.TriggerMode == ProjectileSplitTriggerMode.OnProjectileDespawn;
    }

    public static void TryEnqueueSplitRequests(in Projectile projectileData,
                                               in ProjectileSplitState splitState,
                                               in LocalTransform projectileTransform,
                                               float currentScaleMultiplier,
                                               in ProjectileElementalPayload elementalPayload,
                                               in ProjectileOwner projectileOwner,
                                               ref BufferLookup<ShootRequest> shootRequestLookup)
    {
        if (splitState.CanSplit == 0)
            return;

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (!shootRequestLookup.HasBuffer(shooterEntity))
            return;

        DynamicBuffer<ShootRequest> shootRequests = shootRequestLookup[shooterEntity];
        float3 baseDirection = projectileData.Velocity;
        baseDirection.y = 0f;

        if (math.lengthsq(baseDirection) <= DirectionEpsilon)
            baseDirection = math.forward(projectileTransform.Rotation);

        baseDirection.y = 0f;
        baseDirection = math.normalizesafe(baseDirection, new float3(0f, 0f, 1f));

        float projectileSpeed = math.length(projectileData.Velocity);
        float splitSpeed = math.max(0f, projectileSpeed * math.max(0f, splitState.SplitSpeedMultiplier));
        float splitRange = math.max(0f, projectileData.MaxRange * math.max(0f, splitState.SplitLifetimeMultiplier));
        float splitLifetime = math.max(0f, projectileData.MaxLifetime * math.max(0f, splitState.SplitLifetimeMultiplier));
        float splitDamage = math.max(0f, projectileData.Damage * math.max(0f, splitState.SplitDamageMultiplier));
        float splitExplosionRadius = math.max(0f, projectileData.ExplosionRadius * math.max(0f, splitState.SplitSizeMultiplier));
        float splitScaleMultiplier = math.max(0.01f, currentScaleMultiplier * math.max(0f, splitState.SplitSizeMultiplier));

        switch (splitState.DirectionMode)
        {
            case ProjectileSplitDirectionMode.Uniform:
                AddUniformSplitRequests(ref shootRequests,
                                        in splitState,
                                        projectileTransform.Position,
                                        baseDirection,
                                        splitSpeed,
                                        splitRange,
                                        splitLifetime,
                                        splitDamage,
                                        splitExplosionRadius,
                                        splitScaleMultiplier,
                                        in projectileData,
                                        in elementalPayload,
                                        projectileData.InheritPlayerSpeed);
                return;
            case ProjectileSplitDirectionMode.CustomAngles:
                if (splitState.CustomAnglesDegrees.Length <= 0)
                {
                    AddUniformSplitRequests(ref shootRequests,
                                            in splitState,
                                            projectileTransform.Position,
                                            baseDirection,
                                            splitSpeed,
                                            splitRange,
                                            splitLifetime,
                                            splitDamage,
                                            splitExplosionRadius,
                                            splitScaleMultiplier,
                                            in projectileData,
                                            in elementalPayload,
                                            projectileData.InheritPlayerSpeed);
                    return;
                }

                AddCustomAngleSplitRequests(ref shootRequests,
                                            in splitState,
                                            projectileTransform.Position,
                                            baseDirection,
                                            splitSpeed,
                                            splitRange,
                                            splitLifetime,
                                            splitDamage,
                                            splitExplosionRadius,
                                            splitScaleMultiplier,
                                            in projectileData,
                                            in elementalPayload,
                                            projectileData.InheritPlayerSpeed);
                return;
        }
    }
    #endregion

    #region Private Helpers
    private static void AddUniformSplitRequests(ref DynamicBuffer<ShootRequest> shootRequests,
                                                in ProjectileSplitState splitState,
                                                float3 spawnPosition,
                                                float3 baseDirection,
                                                float splitSpeed,
                                                float splitRange,
                                                float splitLifetime,
                                                float splitDamage,
                                                float splitExplosionRadius,
                                                float splitScaleMultiplier,
                                                in Projectile sourceProjectile,
                                                in ProjectileElementalPayload elementalPayload,
                                                byte inheritPlayerSpeed)
    {
        int splitCount = math.max(1, splitState.SplitProjectileCount);
        float stepDegrees = 360f / splitCount;
        float baseAngleDegrees = ResolveDirectionAngleDegrees(baseDirection);

        for (int splitIndex = 0; splitIndex < splitCount; splitIndex++)
        {
            float angleDegrees = baseAngleDegrees + splitState.SplitOffsetDegrees + stepDegrees * splitIndex;
            float3 direction = ResolvePlanarDirectionFromAngleDegrees(angleDegrees);
            AddSplitShootRequest(ref shootRequests,
                                 spawnPosition,
                                 direction,
                                 splitSpeed,
                                 splitRange,
                                 splitLifetime,
                                 splitDamage,
                                 splitExplosionRadius,
                                 splitScaleMultiplier,
                                 in sourceProjectile,
                                 in elementalPayload,
                                 inheritPlayerSpeed,
                                 splitIndex,
                                 splitCount);
        }
    }

    private static void AddCustomAngleSplitRequests(ref DynamicBuffer<ShootRequest> shootRequests,
                                                    in ProjectileSplitState splitState,
                                                    float3 spawnPosition,
                                                    float3 baseDirection,
                                                    float splitSpeed,
                                                    float splitRange,
                                                    float splitLifetime,
                                                    float splitDamage,
                                                    float splitExplosionRadius,
                                                    float splitScaleMultiplier,
                                                    in Projectile sourceProjectile,
                                                    in ProjectileElementalPayload elementalPayload,
                                                    byte inheritPlayerSpeed)
    {
        float baseAngleDegrees = ResolveDirectionAngleDegrees(baseDirection);
        int splitCount = math.max(1, splitState.CustomAnglesDegrees.Length);

        for (int splitIndex = 0; splitIndex < splitState.CustomAnglesDegrees.Length; splitIndex++)
        {
            float angleDegrees = baseAngleDegrees + splitState.CustomAnglesDegrees[splitIndex] + splitState.SplitOffsetDegrees;
            float3 direction = ResolvePlanarDirectionFromAngleDegrees(angleDegrees);
            AddSplitShootRequest(ref shootRequests,
                                 spawnPosition,
                                 direction,
                                 splitSpeed,
                                 splitRange,
                                 splitLifetime,
                                 splitDamage,
                                 splitExplosionRadius,
                                 splitScaleMultiplier,
                                 in sourceProjectile,
                                 in elementalPayload,
                                 inheritPlayerSpeed,
                                 splitIndex,
                                 splitCount);
        }
    }

    private static void AddSplitShootRequest(ref DynamicBuffer<ShootRequest> shootRequests,
                                             float3 spawnPosition,
                                             float3 direction,
                                             float splitSpeed,
                                             float splitRange,
                                             float splitLifetime,
                                             float splitDamage,
                                             float splitExplosionRadius,
                                             float splitScaleMultiplier,
                                             in Projectile sourceProjectile,
                                             in ProjectileElementalPayload elementalPayload,
                                             byte inheritPlayerSpeed,
                                             int orbitLayerIndex,
                                             int orbitLayerCount)
    {
        int safeOrbitLayerCount = math.max(1, orbitLayerCount);
        shootRequests.Add(new ShootRequest
        {
            Position = spawnPosition,
            Direction = direction,
            Speed = splitSpeed,
            ExplosionRadius = splitExplosionRadius,
            Range = splitRange,
            Lifetime = splitLifetime,
            Damage = splitDamage,
            ProjectileScaleMultiplier = splitScaleMultiplier,
            PenetrationMode = ProjectilePenetrationMode.None,
            MaxPenetrations = 0,
            KnockbackEnabled = sourceProjectile.KnockbackEnabled,
            KnockbackStrength = math.max(0f, sourceProjectile.KnockbackStrength),
            KnockbackDurationSeconds = math.max(0f, sourceProjectile.KnockbackDurationSeconds),
            KnockbackDirectionMode = sourceProjectile.KnockbackDirectionMode,
            KnockbackStackingMode = sourceProjectile.KnockbackStackingMode,
            InheritPlayerSpeed = inheritPlayerSpeed,
            IsSplitChild = 1,
            OrbitLayerIndex = math.clamp(orbitLayerIndex, 0, safeOrbitLayerCount - 1),
            OrbitLayerCount = safeOrbitLayerCount,
            ElementalPayloadOverride = elementalPayload
        });
    }

    private static float ResolveDirectionAngleDegrees(float3 direction)
    {
        float3 normalizedDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));
        return math.degrees(math.atan2(normalizedDirection.x, normalizedDirection.z));
    }

    private static float3 ResolvePlanarDirectionFromAngleDegrees(float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        return new float3(math.sin(radians), 0f, math.cos(radians));
    }
    #endregion

    #endregion
}
