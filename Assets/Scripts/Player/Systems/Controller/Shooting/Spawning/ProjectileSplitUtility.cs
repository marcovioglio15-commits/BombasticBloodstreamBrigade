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
    /// <summary>
    /// Reports whether the configured one-use split trigger matches the resolved enemy-hit outcome.
    /// </summary>
    /// <param name="splitState">Current projectile split state.</param>
    /// <param name="enemyKilledByProjectile">Whether this projectile killed at least one enemy in the resolved hit batch.</param>
    /// <returns>True when split requests should be emitted for the hit event.</returns>
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

    /// <summary>
    /// Reports whether a projectile's natural terminal event must emit its configured split children.
    /// </summary>
    /// <param name="splitState">Current projectile split state.</param>
    /// <returns>True when the one-use despawn split trigger is ready.</returns>
    public static bool ShouldSplitOnDespawn(in ProjectileSplitState splitState)
    {
        if (splitState.CanSplit == 0)
            return false;

        return splitState.TriggerMode == ProjectileSplitTriggerMode.OnProjectileDespawn;
    }

    /// <summary>
    /// Builds split-child requests at the source pose while preserving elemental data and optional return inheritance.
    /// </summary>
    /// <param name="projectileData">Source projectile behavior and current damage state.</param>
    /// <param name="splitState">Authored one-use split settings.</param>
    /// <param name="projectileTransform">Source world pose and scale.</param>
    /// <param name="currentScaleMultiplier">Source scale relative to its prefab base scale.</param>
    /// <param name="elementalPayload">Elemental payload inherited by each child.</param>
    /// <param name="projectileOwner">Source owner that receives child shoot requests.</param>
    /// <param name="returnState">Source return state used to filter and scale inherited return behavior.</param>
    /// <param name="shootRequestLookup">Mutable shooter request-buffer lookup.</param>
    public static void TryEnqueueSplitRequests(in Projectile projectileData,
                                               in ProjectileSplitState splitState,
                                               in LocalTransform projectileTransform,
                                               float currentScaleMultiplier,
                                               in ProjectileElementalPayload elementalPayload,
                                               in ProjectileOwner projectileOwner,
                                               in ProjectileReturnState returnState,
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
        float inheritedReturnRange = returnState.Enabled != 0
            ? math.max(0.01f, returnState.Config.OutboundRangeMultiplier)
            : 1f;
        float inheritedReturnLifetime = returnState.Enabled != 0
            ? math.max(0.01f, returnState.Config.OutboundLifetimeMultiplier)
            : 1f;
        float splitRange = math.max(0f,
                                    projectileData.MaxRange * math.max(0f, splitState.SplitLifetimeMultiplier) /
                                    inheritedReturnRange);
        float splitLifetime = math.max(0f,
                                       projectileData.MaxLifetime * math.max(0f, splitState.SplitLifetimeMultiplier) /
                                       inheritedReturnLifetime);
        float splitDamage = math.max(0f, projectileData.Damage * math.max(0f, splitState.SplitDamageMultiplier));
        float splitExplosionRadius = math.max(0f, projectileData.ExplosionRadius * math.max(0f, splitState.SplitSizeMultiplier));
        float inheritedReturnScale = returnState.Enabled != 0
            ? returnState.Phase == ProjectileReturnPhase.Outbound
                ? math.max(0.01f, returnState.Config.OutboundSizeMultiplier)
                : math.max(0.01f, returnState.Config.ReturnSizeMultiplier)
            : 1f;
        float splitScaleMultiplier = math.max(0.01f,
                                              currentScaleMultiplier * math.max(0f, splitState.SplitSizeMultiplier) / inheritedReturnScale);

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
                                        projectileData.InheritPlayerSpeed,
                                        projectileData.IgnoreInheritedPlayerVelocityX,
                                        projectileData.IgnoreInheritedPlayerVelocityZ,
                                        in returnState);
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
                                            projectileData.InheritPlayerSpeed,
                                            projectileData.IgnoreInheritedPlayerVelocityX,
                                            projectileData.IgnoreInheritedPlayerVelocityZ,
                                            in returnState);
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
                                            projectileData.InheritPlayerSpeed,
                                            projectileData.IgnoreInheritedPlayerVelocityX,
                                            projectileData.IgnoreInheritedPlayerVelocityZ,
                                            in returnState);
                return;
        }
    }
    #endregion

    #region Private Helpers
    /// <summary>
    /// Emits evenly distributed child directions around the source travel direction.
    /// </summary>
    /// <param name="shootRequests">Mutable destination request buffer.</param>
    /// <param name="splitState">Split count, offset, and multipliers.</param>
    /// <param name="spawnPosition">World-space child origin.</param>
    /// <param name="baseDirection">Normalized source travel direction.</param>
    /// <param name="splitSpeed">Resolved child speed.</param>
    /// <param name="splitRange">Resolved child range.</param>
    /// <param name="splitLifetime">Resolved child lifetime.</param>
    /// <param name="splitDamage">Resolved child damage.</param>
    /// <param name="splitExplosionRadius">Resolved child explosion radius.</param>
    /// <param name="splitScaleMultiplier">Resolved child scale before optional return scaling.</param>
    /// <param name="sourceProjectile">Source knockback and inheritance settings.</param>
    /// <param name="elementalPayload">Elemental payload inherited by each child.</param>
    /// <param name="inheritPlayerSpeed">Source inherited-speed flag.</param>
    /// <param name="ignoreInheritedPlayerVelocityX">Source X-axis inheritance mask.</param>
    /// <param name="ignoreInheritedPlayerVelocityZ">Source Z-axis inheritance mask.</param>
    /// <param name="returnState">Source return state inherited only when configured.</param>
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
                                                byte inheritPlayerSpeed,
                                                byte ignoreInheritedPlayerVelocityX,
                                                byte ignoreInheritedPlayerVelocityZ,
                                                in ProjectileReturnState returnState)
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
                                 ignoreInheritedPlayerVelocityX,
                                 ignoreInheritedPlayerVelocityZ,
                                 in returnState,
                                 splitIndex,
                                 splitCount);
        }
    }

    /// <summary>
    /// Emits one child for each authored angle relative to the source travel direction.
    /// </summary>
    /// <param name="shootRequests">Mutable destination request buffer.</param>
    /// <param name="splitState">Custom angles, offset, and multipliers.</param>
    /// <param name="spawnPosition">World-space child origin.</param>
    /// <param name="baseDirection">Normalized source travel direction.</param>
    /// <param name="splitSpeed">Resolved child speed.</param>
    /// <param name="splitRange">Resolved child range.</param>
    /// <param name="splitLifetime">Resolved child lifetime.</param>
    /// <param name="splitDamage">Resolved child damage.</param>
    /// <param name="splitExplosionRadius">Resolved child explosion radius.</param>
    /// <param name="splitScaleMultiplier">Resolved child scale before optional return scaling.</param>
    /// <param name="sourceProjectile">Source knockback and inheritance settings.</param>
    /// <param name="elementalPayload">Elemental payload inherited by each child.</param>
    /// <param name="inheritPlayerSpeed">Source inherited-speed flag.</param>
    /// <param name="ignoreInheritedPlayerVelocityX">Source X-axis inheritance mask.</param>
    /// <param name="ignoreInheritedPlayerVelocityZ">Source Z-axis inheritance mask.</param>
    /// <param name="returnState">Source return state inherited only when configured.</param>
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
                                                    byte inheritPlayerSpeed,
                                                    byte ignoreInheritedPlayerVelocityX,
                                                    byte ignoreInheritedPlayerVelocityZ,
                                                    in ProjectileReturnState returnState)
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
                                 ignoreInheritedPlayerVelocityX,
                                 ignoreInheritedPlayerVelocityZ,
                                 in returnState,
                                 splitIndex,
                                 splitCount);
        }
    }

    /// <summary>
    /// Appends one fully initialized split-child request with non-recursive split and filtered return behavior.
    /// </summary>
    /// <param name="shootRequests">Mutable destination request buffer.</param>
    /// <param name="spawnPosition">World-space child origin.</param>
    /// <param name="direction">Normalized child direction.</param>
    /// <param name="splitSpeed">Resolved child speed.</param>
    /// <param name="splitRange">Resolved child range.</param>
    /// <param name="splitLifetime">Resolved child lifetime.</param>
    /// <param name="splitDamage">Resolved child damage.</param>
    /// <param name="splitExplosionRadius">Resolved child explosion radius.</param>
    /// <param name="splitScaleMultiplier">Resolved child scale before optional return scaling.</param>
    /// <param name="sourceProjectile">Source knockback settings.</param>
    /// <param name="elementalPayload">Elemental payload inherited by the child.</param>
    /// <param name="inheritPlayerSpeed">Source inherited-speed flag.</param>
    /// <param name="ignoreInheritedPlayerVelocityX">Source X-axis inheritance mask.</param>
    /// <param name="ignoreInheritedPlayerVelocityZ">Source Z-axis inheritance mask.</param>
    /// <param name="returnState">Source return state inherited only when configured.</param>
    /// <param name="orbitLayerIndex">Stable child orbit layer index.</param>
    /// <param name="orbitLayerCount">Total child orbit layer count.</param>
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
                                             byte ignoreInheritedPlayerVelocityX,
                                             byte ignoreInheritedPlayerVelocityZ,
                                             in ProjectileReturnState returnState,
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
            ProjectileSizePowerUpMultiplier = returnState.AppliedProjectileSizePowerUpMultiplier > 0f
                ? returnState.AppliedProjectileSizePowerUpMultiplier
                : 1f,
            PenetrationMode = ProjectilePenetrationMode.None,
            MaxPenetrations = 0,
            KnockbackEnabled = sourceProjectile.KnockbackEnabled,
            KnockbackStrength = math.max(0f, sourceProjectile.KnockbackStrength),
            KnockbackDurationSeconds = math.max(0f, sourceProjectile.KnockbackDurationSeconds),
            KnockbackDirectionMode = sourceProjectile.KnockbackDirectionMode,
            KnockbackStackingMode = sourceProjectile.KnockbackStackingMode,
            InheritPlayerSpeed = inheritPlayerSpeed,
            IgnoreInheritedPlayerVelocityX = ignoreInheritedPlayerVelocityX,
            IgnoreInheritedPlayerVelocityZ = ignoreInheritedPlayerVelocityZ,
            IsSplitChild = 1,
            SpawnSource = ProjectileSpawnSource.SplitProjectile,
            ActiveSlotIndex = returnState.ConcurrencyRegistered != 0
                ? returnState.ActiveSlotIndex
                : ProjectileReturnRuntimeUtility.NoActiveSlot,
            HasReturningProjectilesOverride = returnState.Enabled != 0 &&
                                                ProjectileReturnPowerUpInteractionUtility.AllowsSplitChildren(in returnState.Config)
                ? (byte)1
                : (byte)0,
            OrbitLayerIndex = math.clamp(orbitLayerIndex, 0, safeOrbitLayerCount - 1),
            OrbitLayerCount = safeOrbitLayerCount,
            ReturningProjectilesOverride = returnState.Config,
            ElementalPayloadOverride = elementalPayload
        });
    }

    /// <summary>
    /// Resolves the planar heading angle used as the base for split offsets.
    /// </summary>
    /// <param name="direction">Candidate planar direction.</param>
    /// <returns>Heading angle in degrees around world up.</returns>
    private static float ResolveDirectionAngleDegrees(float3 direction)
    {
        float3 normalizedDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));
        return math.degrees(math.atan2(normalizedDirection.x, normalizedDirection.z));
    }

    /// <summary>
    /// Converts one heading angle into a normalized XZ direction.
    /// </summary>
    /// <param name="angleDegrees">Heading angle in degrees around world up.</param>
    /// <returns>Normalized planar direction.</returns>
    private static float3 ResolvePlanarDirectionFromAngleDegrees(float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        return new float3(math.sin(radians), 0f, math.cos(radians));
    }
    #endregion

    #endregion
}
