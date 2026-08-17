using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Builds pooled projectile runtime state and presentation requests shared by the spawn system.
/// </summary>
public static class ProjectileSpawnInitializationUtility
{
    #region Constants
    private const float MinimumVfxScale = 0.01f;
    private const float MinimumVfxLifetimeSeconds = 0.05f;
    private const float VisualShootingPulseDuration = 0.12f;
    #endregion

    #region Methods

    #region Reuse State
    /// <summary>
    /// Resets and enables projectile offscreen-warning state when the shooter has an enemy warning config.
    /// </summary>
    /// <param name="projectileEntity">Pooled projectile being reactivated.</param>
    /// <param name="shooterEntity">Shooter that owns the projectile spawn request.</param>
    /// <param name="enemyProjectileOffscreenWarningLookup">Read-only enemy warning config lookup.</param>
    /// <param name="projectileOffscreenWarningLookup">Mutable projectile warning-state lookup.</param>
    public static void ConfigureProjectileOffscreenWarning(Entity projectileEntity,
                                                           Entity shooterEntity,
                                                           in ComponentLookup<EnemyProjectileOffscreenWarningConfig> enemyProjectileOffscreenWarningLookup,
                                                           ref ComponentLookup<ProjectileOffscreenWarningState> projectileOffscreenWarningLookup)
    {
        if (!projectileOffscreenWarningLookup.HasComponent(projectileEntity))
            return;

        ProjectileOffscreenWarningState warningState = default;

        if (enemyProjectileOffscreenWarningLookup.HasComponent(shooterEntity) &&
            enemyProjectileOffscreenWarningLookup[shooterEntity].Enabled != 0)
        {
            warningState.Enabled = 1;
        }

        projectileOffscreenWarningLookup[projectileEntity] = warningState;
    }

    /// <summary>
    /// Clears per-projectile enemy hit memory before a pooled projectile is reused for a new shot.
    /// </summary>
    /// <param name="projectileEntity">Projectile entity being reactivated from the pool.</param>
    /// <param name="projectileHitHistoryLookup">Lookup used to resolve the projectile hit-history buffer.</param>
    public static void ResetProjectileHitHistory(Entity projectileEntity,
                                                 ref BufferLookup<ProjectileHitHistoryElement> projectileHitHistoryLookup)
    {
        if (!projectileHitHistoryLookup.HasBuffer(projectileEntity))
            return;

        DynamicBuffer<ProjectileHitHistoryElement> hitHistory = projectileHitHistoryLookup[projectileEntity];
        hitHistory.Clear();
    }
    #endregion

    #region Shooter Feedback
    /// <summary>
    /// Marks the shooter camera state so the follow system adds one fire-shake trauma pulse for the volley.
    /// </summary>
    /// <param name="shooterEntity">Shooter entity that emitted at least one primary projectile this frame.</param>
    /// <param name="cameraShakeStateLookup">Mutable lookup used to flag the pending fire request.</param>
    public static void EnqueueFireShakeRequest(Entity shooterEntity,
                                               ref ComponentLookup<PlayerCameraShakeState> cameraShakeStateLookup)
    {
        if (!cameraShakeStateLookup.HasComponent(shooterEntity))
            return;

        PlayerCameraShakeState shakeState = cameraShakeStateLookup[shooterEntity];
        shakeState.FireRequestPending = 1;
        cameraShakeStateLookup[shooterEntity] = shakeState;
    }

    /// <summary>
    /// Records a real projectile spawn as a short shoot pulse for managed animation synchronization.
    /// </summary>
    /// <param name="shooterEntity">Shooter entity whose animation state should be pulsed.</param>
    /// <param name="elapsedTime">Current elapsed world time used to hold the shooting visual state briefly.</param>
    /// <param name="shootingStateLookup">Mutable lookup used to update shooter shooting state.</param>
    public static void RegisterShooterShotPulse(Entity shooterEntity,
                                                float elapsedTime,
                                                ref ComponentLookup<PlayerShootingState> shootingStateLookup)
    {
        if (!shootingStateLookup.HasComponent(shooterEntity))
            return;

        PlayerShootingState shootingState = shootingStateLookup[shooterEntity];
        shootingState.ShotPulseVersion = shootingState.ShotPulseVersion == uint.MaxValue
            ? 1u
            : shootingState.ShotPulseVersion + 1u;
        shootingState.VisualShootingActive = 1;
        shootingState.VisualShootingUntilTime = math.max(shootingState.VisualShootingUntilTime,
                                                         elapsedTime + VisualShootingPulseDuration);
        shootingStateLookup[shooterEntity] = shootingState;
    }
    #endregion

    #region Projectile Behavior
    /// <summary>
    /// Builds a deterministic perfect-circle entry state for one projectile in a volley.
    /// </summary>
    /// <param name="perfectCircleConfig">Aggregated passive orbit configuration.</param>
    /// <param name="requestIndex">Request index used to distribute initial orbit angles.</param>
    /// <param name="shooterEntity">Shooter used to decorrelate angles across entities.</param>
    /// <param name="spawnPosition">Projectile world-space spawn point.</param>
    /// <param name="direction">Normalized initial shot direction.</param>
    /// <param name="entryVelocity">Initial projectile velocity retained for orbit entry.</param>
    /// <param name="orbitLayerIndex">Requested concentric orbit layer.</param>
    /// <param name="orbitLayerCount">Total concentric orbit layer count.</param>
    /// <param name="isEnabled">Whether perfect-circle behavior applies.</param>
    /// <returns>Initialized orbit state, or default when disabled.</returns>
    public static ProjectilePerfectCircleState BuildPerfectCircleState(in PerfectCirclePassiveConfig perfectCircleConfig,
                                                                       int requestIndex,
                                                                       Entity shooterEntity,
                                                                       float3 spawnPosition,
                                                                       float3 direction,
                                                                       float3 entryVelocity,
                                                                       int orbitLayerIndex,
                                                                       int orbitLayerCount,
                                                                       bool isEnabled)
    {
        if (!isEnabled)
            return default;

        int safeOrbitLayerCount = math.max(1, orbitLayerCount);
        float seed = requestIndex + shooterEntity.Index * 13f;
        float angleRadians = math.radians(math.max(0f, perfectCircleConfig.GoldenAngleDegrees) * seed);
        float3 radialDirection = direction;

        if (math.lengthsq(radialDirection) <= 1e-6f)
            radialDirection = new float3(math.cos(angleRadians), 0f, math.sin(angleRadians));

        radialDirection = math.normalizesafe(radialDirection, new float3(0f, 0f, 1f));

        return new ProjectilePerfectCircleState
        {
            Enabled = 1,
            HasEnteredOrbit = 0,
            CompletedFullOrbit = 0,
            HasOrbitPlaneHeight = 0,
            EntryOrigin = spawnPosition,
            OrbitAngle = angleRadians,
            OrbitBlendProgress = 0f,
            CurrentRadius = 0f,
            AccumulatedOrbitRadians = 0f,
            RadialDirection = radialDirection,
            EntryVelocity = entryVelocity,
            OrbitPlaneHeight = 0f,
            OrbitLayerIndex = math.clamp(orbitLayerIndex, 0, safeOrbitLayerCount - 1),
            OrbitLayerCount = safeOrbitLayerCount
        };
    }

    /// <summary>
    /// Builds wall-bounce runtime state from the aggregated passive configuration.
    /// </summary>
    /// <param name="bouncingProjectilesConfig">Aggregated bounce settings.</param>
    /// <param name="isEnabled">Whether bounce behavior applies.</param>
    /// <returns>Initialized bounce state, or default when disabled.</returns>
    public static ProjectileBounceState BuildBounceState(in BouncingProjectilesPassiveConfig bouncingProjectilesConfig,
                                                         bool isEnabled)
    {
        if (!isEnabled || bouncingProjectilesConfig.MaxBounces <= 0)
            return default;

        float minimumSpeedMultiplier = math.max(0f, bouncingProjectilesConfig.MinimumSpeedMultiplierAfterBounce);
        float maximumSpeedMultiplier = math.max(minimumSpeedMultiplier, bouncingProjectilesConfig.MaximumSpeedMultiplierAfterBounce);

        return new ProjectileBounceState
        {
            RemainingBounces = math.max(0, bouncingProjectilesConfig.MaxBounces),
            SpeedPercentChangePerBounce = bouncingProjectilesConfig.SpeedPercentChangePerBounce,
            MinimumSpeedMultiplierAfterBounce = minimumSpeedMultiplier,
            MaximumSpeedMultiplierAfterBounce = maximumSpeedMultiplier,
            CurrentSpeedMultiplier = 1f
        };
    }

    /// <summary>
    /// Builds one-use projectile split state while preventing recursive split-child activation.
    /// </summary>
    /// <param name="splittingProjectilesConfig">Aggregated split settings.</param>
    /// <param name="isEnabled">Whether split behavior applies.</param>
    /// <param name="isSplitChild">Whether this projectile already came from a split.</param>
    /// <param name="hasReturningProjectiles">Whether the projectile uses Returning Projectiles.</param>
    /// <param name="returningProjectilesConfig">Resolved return config used to filter split interoperability.</param>
    /// <returns>Initialized split state, or default when disabled or recursive.</returns>
    public static ProjectileSplitState BuildSplitState(in SplittingProjectilesPassiveConfig splittingProjectilesConfig,
                                                       bool isEnabled,
                                                       bool isSplitChild,
                                                       bool hasReturningProjectiles,
                                                       in ReturningProjectilesConfig returningProjectilesConfig)
    {
        if (!isEnabled ||
            isSplitChild ||
            hasReturningProjectiles &&
            !ProjectileReturnPowerUpInteractionUtility.AllowsProjectileSplitting(in returningProjectilesConfig))
            return default;

        return new ProjectileSplitState
        {
            CanSplit = 1,
            TriggerMode = splittingProjectilesConfig.TriggerMode,
            DirectionMode = splittingProjectilesConfig.DirectionMode,
            SplitProjectileCount = math.max(1, splittingProjectilesConfig.SplitProjectileCount),
            SplitOffsetDegrees = splittingProjectilesConfig.SplitOffsetDegrees,
            CustomAnglesDegrees = splittingProjectilesConfig.CustomAnglesDegrees,
            SplitDamageMultiplier = math.max(0f, splittingProjectilesConfig.SplitDamageMultiplier),
            SplitSizeMultiplier = math.max(0f, splittingProjectilesConfig.SplitSizeMultiplier),
            SplitSpeedMultiplier = math.max(0f, splittingProjectilesConfig.SplitSpeedMultiplier),
            SplitLifetimeMultiplier = math.max(0f, splittingProjectilesConfig.SplitLifetimeMultiplier)
        };
    }

    /// <summary>
    /// Merges request-specific and passive elemental payloads for a newly activated projectile.
    /// </summary>
    /// <param name="request">Shoot request carrying an optional explicit elemental payload.</param>
    /// <param name="passiveElementalProjectilesConfig">Aggregated passive elemental settings.</param>
    /// <param name="hasPassiveElementalPayload">Whether passive elemental settings apply.</param>
    /// <returns>Merged fixed-capacity elemental payload.</returns>
    public static ProjectileElementalPayload ResolveElementalPayload(in ShootRequest request,
                                                                     in ElementalProjectilesPassiveConfig passiveElementalProjectilesConfig,
                                                                     bool hasPassiveElementalPayload)
    {
        ProjectileElementalPayload resolvedPayload = request.ElementalPayloadOverride;

        if (!hasPassiveElementalPayload || passiveElementalProjectilesConfig.StacksPerHit <= 0f)
            return resolvedPayload;

        ProjectileElementalPayload passivePayload = ProjectileElementalPayloadUtility.BuildSingle(in passiveElementalProjectilesConfig.Effect,
                                                                                                    math.max(0f, passiveElementalProjectilesConfig.StacksPerHit));
        ProjectileElementalPayloadUtility.MergePayload(ref resolvedPayload, in passivePayload);
        return resolvedPayload;
    }
    #endregion

    #region Presentation Requests
    /// <summary>
    /// Queues an attached managed VFX request for a newly activated projectile when configured.
    /// </summary>
    /// <param name="shooterEntity">Player entity that owns the projectile and VFX request buffer.</param>
    /// <param name="projectileEntity">Projectile entity followed by the VFX until despawn.</param>
    /// <param name="projectileTransform">Initial projectile transform used for request placement.</param>
    /// <param name="projectileScaleMultiplier">Projectile size multiplier already applied to the spawned transform.</param>
    /// <param name="projectileAttachedVfxConfigLookup">Read-only lookup for optional projectile VFX config.</param>
    /// <param name="powerUpVfxRequestLookup">Writable lookup for player-owned VFX request buffers.</param>
    public static void TryEnqueueProjectileAttachedVfx(Entity shooterEntity,
                                                       Entity projectileEntity,
                                                       in LocalTransform projectileTransform,
                                                       float projectileScaleMultiplier,
                                                       in ComponentLookup<PlayerProjectileAttachedVfxConfig> projectileAttachedVfxConfigLookup,
                                                       ref BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup)
    {
        if (!projectileAttachedVfxConfigLookup.HasComponent(shooterEntity) ||
            !powerUpVfxRequestLookup.HasBuffer(shooterEntity))
        {
            return;
        }

        PlayerProjectileAttachedVfxConfig config = projectileAttachedVfxConfigLookup[shooterEntity];

        if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
            return;

        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(projectileTransform.Rotation);
        float resolvedProjectileScaleMultiplier = math.max(MinimumVfxScale, projectileScaleMultiplier);
        float3 scaledSpawnOffset = config.SpawnOffset * resolvedProjectileScaleMultiplier;
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = powerUpVfxRequestLookup[shooterEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.SourcePrefab,
            Position = projectileTransform.Position + math.rotate(rotation, scaledSpawnOffset),
            Rotation = rotation,
            UniformScale = math.max(MinimumVfxScale, config.UniformScale * resolvedProjectileScaleMultiplier),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, config.LifetimeSeconds),
            FollowTargetEntity = projectileEntity,
            FollowPositionOffset = scaledSpawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            KeepAliveWhileFollowTargetValid = 1,
            FollowMuzzlePose = 1
        });
    }

    /// <summary>
    /// Queues one muzzle-flash request at the primary shot origin for a whole volley.
    /// </summary>
    /// <param name="shooterEntity">Player entity that owns the muzzle-flash config and request buffer.</param>
    /// <param name="muzzleOrigin">World-space projectile origin captured from the first primary shot.</param>
    /// <param name="muzzleRotation">World-space shot rotation captured from the first primary shot.</param>
    /// <param name="muzzleFlashVfxConfigLookup">Read-only lookup for optional muzzle-flash config.</param>
    /// <param name="powerUpVfxRequestLookup">Writable lookup for player-owned VFX request buffers.</param>
    public static void TryEnqueueMuzzleFlashVfx(Entity shooterEntity,
                                                float3 muzzleOrigin,
                                                quaternion muzzleRotation,
                                                in ComponentLookup<PlayerMuzzleFlashVfxConfig> muzzleFlashVfxConfigLookup,
                                                ref BufferLookup<PlayerPowerUpVfxSpawnRequest> powerUpVfxRequestLookup)
    {
        if (!muzzleFlashVfxConfigLookup.HasComponent(shooterEntity) ||
            !powerUpVfxRequestLookup.HasBuffer(shooterEntity))
        {
            return;
        }

        PlayerMuzzleFlashVfxConfig config = muzzleFlashVfxConfigLookup[shooterEntity];

        if (config.PrefabEntity == Entity.Null && config.SourcePrefab.Value == null)
            return;

        quaternion rotation = PlayerMuzzleVfxPoseUtility.ResolveWorldUpRotation(muzzleRotation);
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = powerUpVfxRequestLookup[shooterEntity];
        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = config.PrefabEntity,
            SourcePrefab = config.SourcePrefab,
            Position = muzzleOrigin + math.rotate(rotation, config.SpawnOffset),
            Rotation = rotation,
            UniformScale = math.max(MinimumVfxScale, config.UniformScale),
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = math.max(MinimumVfxLifetimeSeconds, config.LifetimeSeconds),
            FollowTargetEntity = shooterEntity,
            FollowPositionOffset = config.SpawnOffset,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero,
            FollowMuzzlePose = 1
        });
    }
    #endregion

    #endregion
}
