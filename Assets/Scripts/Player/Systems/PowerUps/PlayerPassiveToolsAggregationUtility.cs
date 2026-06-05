using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Aggregates equipped passive-tool payloads into one runtime <see cref="PlayerPassiveToolsState"/> snapshot.
/// </summary>
public static class PlayerPassiveToolsAggregationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds aggregated passive state from one entity passive buffer lookup.
    /// </summary>
    /// <param name="entity">Player entity being aggregated.</param>
    /// <param name="equippedPassiveToolsLookup">Buffer lookup containing equipped passive entries.</param>
    /// <param name="passiveToolsState">Aggregated passive runtime state.</param>
    public static void BuildPassiveToolsState(Entity entity,
                                              in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup,
                                              out PlayerPassiveToolsState passiveToolsState)
    {
        CreateDefaultState(out passiveToolsState);

        if (!equippedPassiveToolsLookup.HasBuffer(entity))
            return;

        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];
        RebuildPassiveToolsState(equippedPassiveToolsBuffer, ref passiveToolsState);
    }

    /// <summary>
    /// Builds aggregated passive state from a direct equipped-passives dynamic buffer.
    /// </summary>
    /// <param name="equippedPassiveToolsBuffer">Runtime equipped passive entries.</param>
    /// <param name="passiveToolsState">Aggregated passive runtime state.</param>
    public static void BuildPassiveToolsState(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer,
                                              out PlayerPassiveToolsState passiveToolsState)
    {
        CreateDefaultState(out passiveToolsState);
        RebuildPassiveToolsState(equippedPassiveToolsBuffer, ref passiveToolsState);
    }

    /// <summary>
    /// Rebuilds aggregated passive state in place so Burst callers do not pass the large state payload by value.
    /// </summary>
    /// <param name="entity">Player entity being aggregated.</param>
    /// <param name="equippedPassiveToolsLookup">Buffer lookup containing equipped passive entries.</param>
    /// <param name="passiveToolsState">Aggregate snapshot reset and rebuilt in place.</param>
    public static void RebuildPassiveToolsState(Entity entity,
                                                in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup,
                                                ref PlayerPassiveToolsState passiveToolsState)
    {
        ResetToDefault(ref passiveToolsState);

        if (!equippedPassiveToolsLookup.HasBuffer(entity))
            return;

        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];
        RebuildPassiveToolsState(equippedPassiveToolsBuffer, ref passiveToolsState);
    }

    /// <summary>
    /// Rebuilds aggregated passive state from a direct equipped-passives buffer without returning the large state payload.
    /// </summary>
    /// <param name="equippedPassiveToolsBuffer">Runtime equipped passive entries.</param>
    /// <param name="passiveToolsState">Aggregate snapshot reset and rebuilt in place.</param>
    public static void RebuildPassiveToolsState(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer,
                                                ref PlayerPassiveToolsState passiveToolsState)
    {
        ResetToDefault(ref passiveToolsState);

        if (!equippedPassiveToolsBuffer.IsCreated)
            return;

        for (int passiveToolIndex = 0; passiveToolIndex < equippedPassiveToolsBuffer.Length; passiveToolIndex++)
        {
            ref EquippedPassiveToolElement equippedPassiveTool = ref equippedPassiveToolsBuffer.ElementAt(passiveToolIndex);
            AccumulatePassiveTool(ref passiveToolsState, in equippedPassiveTool.Tool);
        }
    }

    /// <summary>
    /// Builds the neutral passive state used when gameplay needs projectile math without equipped passive effects.
    /// </summary>
    /// <param name="passiveToolsState">Passive state with projectile multipliers initialized to 1 and all hooks disabled.</param>
    public static void CreateDefaultState(out PlayerPassiveToolsState passiveToolsState)
    {
        passiveToolsState = default;
        ResetToDefault(ref passiveToolsState);
    }

    /// <summary>
    /// Resets an aggregate passive snapshot to its neutral multiplier state.
    /// </summary>
    /// <param name="passiveToolsState">Aggregate snapshot reset in place.</param>
    public static void ResetToDefault(ref PlayerPassiveToolsState passiveToolsState)
    {
        passiveToolsState = default;
        passiveToolsState.ProjectileSizeMultiplier = 1f;
        passiveToolsState.ProjectileDamageMultiplier = 1f;
        passiveToolsState.ProjectileSpeedMultiplier = 1f;
        passiveToolsState.ProjectileLifetimeSecondsMultiplier = 1f;
        passiveToolsState.ProjectileLifetimeRangeMultiplier = 1f;
        passiveToolsState.WeaponVisualSlot = PlayerWeaponVisualSlot.BaseGun;
    }

    /// <summary>
    /// Builds a neutral passive snapshot that exposes only one Laser Beam config.
    /// </summary>
    /// <param name="laserBeamConfig">Standalone Laser Beam settings to expose to beam simulation and presentation.</param>
    /// <param name="passiveToolsState">Passive state with only HasLaserBeam enabled.</param>
    public static void CreateStandaloneLaserBeamState(in LaserBeamPassiveConfig laserBeamConfig,
                                                      out PlayerPassiveToolsState passiveToolsState)
    {
        CreateDefaultState(out passiveToolsState);
        passiveToolsState.HasLaserBeam = 1;
        passiveToolsState.LaserBeam = laserBeamConfig;
    }

    /// <summary>
    /// Merges one passive-tool payload into an aggregated passive runtime snapshot.
    /// </summary>
    /// <param name="passiveToolsState">Aggregated passive state updated in place.</param>
    /// <param name="passiveToolConfig">Passive-tool payload being merged.</param>

    public static void AccumulatePassiveTool(ref PlayerPassiveToolsState passiveToolsState, in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.IsDefined == 0)
            return;

        if (passiveToolConfig.HasWeaponSwitch != 0)
        {
            passiveToolsState.HasWeaponSwitch = 1;
            passiveToolsState.WeaponVisualSlot = passiveToolConfig.WeaponVisualSlot;
        }

        if (passiveToolConfig.HasProjectileSize != 0)
        {
            passiveToolsState.ProjectileSizeMultiplier *= math.max(0.01f, passiveToolConfig.ProjectileSize.SizeMultiplier);
            passiveToolsState.ProjectileDamageMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.DamageMultiplier);
            passiveToolsState.ProjectileSpeedMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.SpeedMultiplier);
            passiveToolsState.ProjectileLifetimeSecondsMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeSecondsMultiplier);
            passiveToolsState.ProjectileLifetimeRangeMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeRangeMultiplier);
        }

        if (passiveToolConfig.HasShotgun != 0)
        {
            passiveToolsState.HasShotgun = 1;
            passiveToolsState.Shotgun.ProjectileCount += math.max(0, passiveToolConfig.Shotgun.ProjectileCount);
            passiveToolsState.Shotgun.ConeAngleDegrees = math.max(passiveToolsState.Shotgun.ConeAngleDegrees,
                                                                  math.max(0f, passiveToolConfig.Shotgun.ConeAngleDegrees));
            passiveToolsState.Shotgun.PenetrationMode = (ProjectilePenetrationMode)math.max((int)passiveToolsState.Shotgun.PenetrationMode,
                                                                                             (int)passiveToolConfig.Shotgun.PenetrationMode);
            passiveToolsState.Shotgun.MaxPenetrations += math.max(0, passiveToolConfig.Shotgun.MaxPenetrations);
            passiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityX = passiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityX != 0 || passiveToolConfig.Shotgun.IgnoreInheritedPlayerVelocityX != 0 ? (byte)1 : (byte)0;
            passiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityZ = passiveToolsState.Shotgun.IgnoreInheritedPlayerVelocityZ != 0 || passiveToolConfig.Shotgun.IgnoreInheritedPlayerVelocityZ != 0 ? (byte)1 : (byte)0;
        }

        if (passiveToolConfig.HasElementalProjectiles != 0 && passiveToolConfig.ElementalProjectiles.StacksPerHit > 0f)
        {
            float candidateStacksPerHit = math.max(0f, passiveToolConfig.ElementalProjectiles.StacksPerHit);

            if (candidateStacksPerHit > 0f)
            {
                if (passiveToolsState.HasElementalProjectiles == 0)
                {
                    passiveToolsState.HasElementalProjectiles = 1;
                    passiveToolsState.ElementalProjectiles = passiveToolConfig.ElementalProjectiles;
                }
                else
                {
                    passiveToolsState.ElementalProjectiles.Effect = passiveToolConfig.ElementalProjectiles.Effect;
                    passiveToolsState.ElementalProjectiles.StacksPerHit += candidateStacksPerHit;
                }
            }
        }

        if (passiveToolConfig.HasPerfectCircle != 0)
        {
            passiveToolsState.HasPerfectCircle = 1;
            bool hasPerfectCircleConfig = passiveToolsState.PerfectCircle.PathMode == ProjectileOrbitPathMode.GoldenSpiral ||
                                          passiveToolsState.PerfectCircle.OrbitRadiusMax > 0f ||
                                          passiveToolsState.PerfectCircle.SpiralMaximumRadius > 0f;
            PlayerPowerUpPassiveConfigBuildUtility.AccumulatePerfectCirclePassiveConfig(ref passiveToolsState.PerfectCircle,
                                                                                        in passiveToolConfig.PerfectCircle,
                                                                                        ref hasPerfectCircleConfig);
        }

        if (passiveToolConfig.HasBouncingProjectiles != 0)
        {
            passiveToolsState.HasBouncingProjectiles = 1;
            passiveToolsState.BouncingProjectiles.MaxBounces += math.max(0, passiveToolConfig.BouncingProjectiles.MaxBounces);
            passiveToolsState.BouncingProjectiles.SpeedPercentChangePerBounce += passiveToolConfig.BouncingProjectiles.SpeedPercentChangePerBounce;

            if (passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce <= 0f)
                passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce = math.max(0f, passiveToolConfig.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce);
            else
                passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce = math.min(passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce,
                                                                                                    math.max(0f, passiveToolConfig.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce));

            passiveToolsState.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce = math.max(passiveToolsState.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce,
                                                                                                math.max(0f, passiveToolConfig.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce));
        }

        if (passiveToolConfig.HasSplittingProjectiles != 0)
        {
            passiveToolsState.HasSplittingProjectiles = 1;

            if (passiveToolsState.SplittingProjectiles.SplitProjectileCount <= 0)
            {
                passiveToolsState.SplittingProjectiles = passiveToolConfig.SplittingProjectiles;
            }
            else
            {
                passiveToolsState.SplittingProjectiles.SplitProjectileCount = math.max(passiveToolsState.SplittingProjectiles.SplitProjectileCount,
                                                                                       passiveToolConfig.SplittingProjectiles.SplitProjectileCount);
                passiveToolsState.SplittingProjectiles.SplitOffsetDegrees = math.max(passiveToolsState.SplittingProjectiles.SplitOffsetDegrees,
                                                                                     passiveToolConfig.SplittingProjectiles.SplitOffsetDegrees);
                passiveToolsState.SplittingProjectiles.SplitDamageMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitDamageMultiplier,
                                                                                        passiveToolConfig.SplittingProjectiles.SplitDamageMultiplier);
                passiveToolsState.SplittingProjectiles.SplitSizeMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitSizeMultiplier,
                                                                                      passiveToolConfig.SplittingProjectiles.SplitSizeMultiplier);
                passiveToolsState.SplittingProjectiles.SplitSpeedMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitSpeedMultiplier,
                                                                                       passiveToolConfig.SplittingProjectiles.SplitSpeedMultiplier);
                passiveToolsState.SplittingProjectiles.SplitLifetimeMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitLifetimeMultiplier,
                                                                                          passiveToolConfig.SplittingProjectiles.SplitLifetimeMultiplier);

                if (passiveToolsState.SplittingProjectiles.CustomAnglesDegrees.Length <= 0 &&
                    passiveToolConfig.SplittingProjectiles.CustomAnglesDegrees.Length > 0)
                {
                    passiveToolsState.SplittingProjectiles.CustomAnglesDegrees = passiveToolConfig.SplittingProjectiles.CustomAnglesDegrees;
                }

                passiveToolsState.SplittingProjectiles.TriggerMode = passiveToolConfig.SplittingProjectiles.TriggerMode;
                passiveToolsState.SplittingProjectiles.DirectionMode = passiveToolConfig.SplittingProjectiles.DirectionMode;
            }
        }

        if (passiveToolConfig.HasExplosion != 0)
        {
            passiveToolsState.HasExplosion = 1;

            if (passiveToolsState.Explosion.Radius <= 0f)
            {
                passiveToolsState.Explosion = passiveToolConfig.Explosion;
            }
            else
            {
                passiveToolsState.Explosion.CooldownSeconds = math.min(passiveToolsState.Explosion.CooldownSeconds, passiveToolConfig.Explosion.CooldownSeconds);
                passiveToolsState.Explosion.Radius = math.max(passiveToolsState.Explosion.Radius, passiveToolConfig.Explosion.Radius);
                passiveToolsState.Explosion.Damage += passiveToolConfig.Explosion.Damage;
                passiveToolsState.Explosion.AffectAllEnemiesInRadius = passiveToolsState.Explosion.AffectAllEnemiesInRadius != 0 || passiveToolConfig.Explosion.AffectAllEnemiesInRadius != 0 ? (byte)1 : (byte)0;

                if (passiveToolsState.Explosion.ExplosionVfxPrefabEntity == Entity.Null && passiveToolConfig.Explosion.ExplosionVfxPrefabEntity != Entity.Null)
                {
                    passiveToolsState.Explosion.ExplosionVfxPrefabEntity = passiveToolConfig.Explosion.ExplosionVfxPrefabEntity;
                    passiveToolsState.Explosion.ScaleVfxToRadius = passiveToolConfig.Explosion.ScaleVfxToRadius;
                    passiveToolsState.Explosion.VfxScaleMultiplier = passiveToolConfig.Explosion.VfxScaleMultiplier;
                }
            }
        }

        if (passiveToolConfig.HasElementalTrail != 0)
        {
            passiveToolsState.HasElementalTrail = 1;

            if (passiveToolsState.ElementalTrail.TrailSegmentLifetimeSeconds <= 0f)
            {
                passiveToolsState.ElementalTrail = passiveToolConfig.ElementalTrail;
            }
            else
            {
                passiveToolsState.ElementalTrail.Effect = passiveToolConfig.ElementalTrail.Effect;
                passiveToolsState.ElementalTrail.TrailSegmentLifetimeSeconds = math.max(passiveToolsState.ElementalTrail.TrailSegmentLifetimeSeconds,
                                                                                        passiveToolConfig.ElementalTrail.TrailSegmentLifetimeSeconds);
                passiveToolsState.ElementalTrail.TrailSpawnDistance = math.max(passiveToolsState.ElementalTrail.TrailSpawnDistance,
                                                                               passiveToolConfig.ElementalTrail.TrailSpawnDistance);
                passiveToolsState.ElementalTrail.TrailSpawnIntervalSeconds = math.min(passiveToolsState.ElementalTrail.TrailSpawnIntervalSeconds,
                                                                                      passiveToolConfig.ElementalTrail.TrailSpawnIntervalSeconds);
                passiveToolsState.ElementalTrail.TrailRadius = math.max(passiveToolsState.ElementalTrail.TrailRadius,
                                                                        passiveToolConfig.ElementalTrail.TrailRadius);
                passiveToolsState.ElementalTrail.MaxActiveSegmentsPerPlayer = math.max(passiveToolsState.ElementalTrail.MaxActiveSegmentsPerPlayer,
                                                                                        passiveToolConfig.ElementalTrail.MaxActiveSegmentsPerPlayer);
                passiveToolsState.ElementalTrail.StacksPerTick += math.max(0f, passiveToolConfig.ElementalTrail.StacksPerTick);
                passiveToolsState.ElementalTrail.ApplyIntervalSeconds = math.min(passiveToolsState.ElementalTrail.ApplyIntervalSeconds,
                                                                                 passiveToolConfig.ElementalTrail.ApplyIntervalSeconds);

                if (passiveToolsState.ElementalTrail.TrailAttachedVfxPrefabEntity == Entity.Null &&
                    passiveToolConfig.ElementalTrail.TrailAttachedVfxPrefabEntity != Entity.Null)
                {
                    passiveToolsState.ElementalTrail.TrailAttachedVfxPrefabEntity = passiveToolConfig.ElementalTrail.TrailAttachedVfxPrefabEntity;
                    passiveToolsState.ElementalTrail.TrailAttachedVfxScaleMultiplier = passiveToolConfig.ElementalTrail.TrailAttachedVfxScaleMultiplier;
                    passiveToolsState.ElementalTrail.TrailAttachedVfxOffset = passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset;
                }
            }
        }

        AccumulateOrbitalProjections(ref passiveToolsState, in passiveToolConfig);

        if (passiveToolConfig.HasHeal == 0 &&
            passiveToolConfig.HasBulletTime == 0 &&
            passiveToolConfig.HasLaserBeam == 0)
        {
            return;
        }

        if (passiveToolConfig.HasHeal != 0)
        {
            passiveToolsState.HasHeal = 1;

            if (passiveToolsState.Heal.HealAmount <= 0f)
            {
                passiveToolsState.Heal = passiveToolConfig.Heal;
            }
            else
            {
                passiveToolsState.Heal.HealAmount += math.max(0f, passiveToolConfig.Heal.HealAmount);
                passiveToolsState.Heal.CooldownSeconds = math.min(passiveToolsState.Heal.CooldownSeconds, passiveToolConfig.Heal.CooldownSeconds);
                passiveToolsState.Heal.DurationSeconds = math.max(passiveToolsState.Heal.DurationSeconds, passiveToolConfig.Heal.DurationSeconds);
                passiveToolsState.Heal.TickIntervalSeconds = math.min(passiveToolsState.Heal.TickIntervalSeconds, passiveToolConfig.Heal.TickIntervalSeconds);
                passiveToolsState.Heal.StackPolicy = passiveToolConfig.Heal.StackPolicy;
                passiveToolsState.Heal.TriggerMode = passiveToolConfig.Heal.TriggerMode;
            }
        }

        if (passiveToolConfig.HasBulletTime != 0)
        {
            passiveToolsState.HasBulletTime = 1;

            if (passiveToolsState.BulletTime.EnemySlowPercent <= 0f)
            {
                passiveToolsState.BulletTime = passiveToolConfig.BulletTime;
            }
            else
            {
                passiveToolsState.BulletTime.TriggerMode = passiveToolConfig.BulletTime.TriggerMode;
                passiveToolsState.BulletTime.CooldownSeconds = math.min(passiveToolsState.BulletTime.CooldownSeconds,
                                                                        passiveToolConfig.BulletTime.CooldownSeconds);
                passiveToolsState.BulletTime.DurationSeconds = math.max(passiveToolsState.BulletTime.DurationSeconds,
                                                                        passiveToolConfig.BulletTime.DurationSeconds);
                passiveToolsState.BulletTime.EnemySlowPercent = math.max(passiveToolsState.BulletTime.EnemySlowPercent,
                                                                         passiveToolConfig.BulletTime.EnemySlowPercent);
                passiveToolsState.BulletTime.TransitionTimeSeconds = math.max(passiveToolsState.BulletTime.TransitionTimeSeconds,
                                                                              passiveToolConfig.BulletTime.TransitionTimeSeconds);
            }
        }

        if (passiveToolConfig.HasLaserBeam == 0)
            return;

        passiveToolsState.HasLaserBeam = 1;

        if (passiveToolsState.LaserBeam.DamageTickIntervalSeconds <= 0f)
        {
            passiveToolsState.LaserBeam = passiveToolConfig.LaserBeam;
            return;
        }

        passiveToolsState.LaserBeam.DamageMultiplier *= math.max(0f, passiveToolConfig.LaserBeam.DamageMultiplier);
        passiveToolsState.LaserBeam.ContinuousDamagePerSecondMultiplier *= math.max(0f, passiveToolConfig.LaserBeam.ContinuousDamagePerSecondMultiplier);
        passiveToolsState.LaserBeam.VirtualProjectileSpeedMultiplier *= math.max(0f, passiveToolConfig.LaserBeam.VirtualProjectileSpeedMultiplier);
        passiveToolsState.LaserBeam.DamageTickIntervalSeconds = math.min(passiveToolsState.LaserBeam.DamageTickIntervalSeconds,
                                                                         math.max(0.0001f, passiveToolConfig.LaserBeam.DamageTickIntervalSeconds));
        passiveToolsState.LaserBeam.MaximumContinuousActiveSeconds = math.max(passiveToolsState.LaserBeam.MaximumContinuousActiveSeconds,
                                                                              passiveToolConfig.LaserBeam.MaximumContinuousActiveSeconds);
        passiveToolsState.LaserBeam.CooldownSeconds = passiveToolsState.LaserBeam.CooldownSeconds <= 0f ||
                                                      passiveToolConfig.LaserBeam.CooldownSeconds <= 0f
            ? 0f
            : math.min(passiveToolsState.LaserBeam.CooldownSeconds, passiveToolConfig.LaserBeam.CooldownSeconds);
        passiveToolsState.LaserBeam.MaximumBounceSegments = math.max(passiveToolsState.LaserBeam.MaximumBounceSegments,
                                                                     passiveToolConfig.LaserBeam.MaximumBounceSegments);
        passiveToolsState.LaserBeam.ApplyPlayerHandlingNerfWhileFiring =
            passiveToolsState.LaserBeam.ApplyPlayerHandlingNerfWhileFiring != 0 ||
            passiveToolConfig.LaserBeam.ApplyPlayerHandlingNerfWhileFiring != 0
                ? (byte)1
                : (byte)0;
        passiveToolsState.LaserBeam.FiringMoveSpeedMultiplier *= math.max(0f, passiveToolConfig.LaserBeam.FiringMoveSpeedMultiplier);
        passiveToolsState.LaserBeam.FiringRotationSpeedMultiplier *= math.max(0f, passiveToolConfig.LaserBeam.FiringRotationSpeedMultiplier);
        passiveToolsState.LaserBeam.BodyWidthMultiplier = math.max(passiveToolsState.LaserBeam.BodyWidthMultiplier,
                                                                   passiveToolConfig.LaserBeam.BodyWidthMultiplier);
        passiveToolsState.LaserBeam.CollisionWidthMultiplier = math.max(passiveToolsState.LaserBeam.CollisionWidthMultiplier,
                                                                        passiveToolConfig.LaserBeam.CollisionWidthMultiplier);
        passiveToolsState.LaserBeam.SourceScaleMultiplier = math.max(passiveToolsState.LaserBeam.SourceScaleMultiplier,
                                                                     passiveToolConfig.LaserBeam.SourceScaleMultiplier);
        passiveToolsState.LaserBeam.TerminalCapScaleMultiplier = math.max(passiveToolsState.LaserBeam.TerminalCapScaleMultiplier,
                                                                          passiveToolConfig.LaserBeam.TerminalCapScaleMultiplier);
        passiveToolsState.LaserBeam.ContactFlareScaleMultiplier = math.max(passiveToolsState.LaserBeam.ContactFlareScaleMultiplier,
                                                                           passiveToolConfig.LaserBeam.ContactFlareScaleMultiplier);
        passiveToolsState.LaserBeam.BodyOpacity = math.max(passiveToolsState.LaserBeam.BodyOpacity,
                                                           passiveToolConfig.LaserBeam.BodyOpacity);
        passiveToolsState.LaserBeam.CoreWidthMultiplier = math.max(passiveToolsState.LaserBeam.CoreWidthMultiplier,
                                                                   passiveToolConfig.LaserBeam.CoreWidthMultiplier);
        passiveToolsState.LaserBeam.CoreBrightness = math.max(passiveToolsState.LaserBeam.CoreBrightness,
                                                              passiveToolConfig.LaserBeam.CoreBrightness);
        passiveToolsState.LaserBeam.RimBrightness = math.max(passiveToolsState.LaserBeam.RimBrightness,
                                                             passiveToolConfig.LaserBeam.RimBrightness);
        passiveToolsState.LaserBeam.FlowScrollSpeed = math.max(passiveToolsState.LaserBeam.FlowScrollSpeed,
                                                               passiveToolConfig.LaserBeam.FlowScrollSpeed);
        passiveToolsState.LaserBeam.FlowPulseFrequency = math.max(passiveToolsState.LaserBeam.FlowPulseFrequency,
                                                                  passiveToolConfig.LaserBeam.FlowPulseFrequency);
        passiveToolsState.LaserBeam.StormTwistSpeed = math.max(passiveToolsState.LaserBeam.StormTwistSpeed,
                                                               passiveToolConfig.LaserBeam.StormTwistSpeed);
        passiveToolsState.LaserBeam.StormTickPostTravelHoldSeconds = math.max(passiveToolsState.LaserBeam.StormTickPostTravelHoldSeconds,
                                                                               passiveToolConfig.LaserBeam.StormTickPostTravelHoldSeconds);
        passiveToolsState.LaserBeam.StormIdleIntensity = math.max(passiveToolsState.LaserBeam.StormIdleIntensity,
                                                                  passiveToolConfig.LaserBeam.StormIdleIntensity);
        passiveToolsState.LaserBeam.StormBurstIntensity = math.max(passiveToolsState.LaserBeam.StormBurstIntensity,
                                                                   passiveToolConfig.LaserBeam.StormBurstIntensity);
        passiveToolsState.LaserBeam.SourceOffset = math.max(passiveToolsState.LaserBeam.SourceOffset,
                                                            passiveToolConfig.LaserBeam.SourceOffset);
        passiveToolsState.LaserBeam.SourceDischargeIntensity = math.max(passiveToolsState.LaserBeam.SourceDischargeIntensity,
                                                                        passiveToolConfig.LaserBeam.SourceDischargeIntensity);
        passiveToolsState.LaserBeam.StormShellWidthMultiplier = math.max(passiveToolsState.LaserBeam.StormShellWidthMultiplier,
                                                                         passiveToolConfig.LaserBeam.StormShellWidthMultiplier);
        passiveToolsState.LaserBeam.StormShellSeparation = math.max(passiveToolsState.LaserBeam.StormShellSeparation,
                                                                    passiveToolConfig.LaserBeam.StormShellSeparation);
        passiveToolsState.LaserBeam.StormRingFrequency = math.max(passiveToolsState.LaserBeam.StormRingFrequency,
                                                                  passiveToolConfig.LaserBeam.StormRingFrequency);
        passiveToolsState.LaserBeam.StormRingThickness = math.max(passiveToolsState.LaserBeam.StormRingThickness,
                                                                  passiveToolConfig.LaserBeam.StormRingThickness);
        passiveToolsState.LaserBeam.StormTickTravelSpeed = math.max(passiveToolsState.LaserBeam.StormTickTravelSpeed,
                                                                    passiveToolConfig.LaserBeam.StormTickTravelSpeed);
        passiveToolsState.LaserBeam.StormTickDamageLengthTolerance = math.max(passiveToolsState.LaserBeam.StormTickDamageLengthTolerance,
                                                                               passiveToolConfig.LaserBeam.StormTickDamageLengthTolerance);
        passiveToolsState.LaserBeam.TerminalCapIntensity = math.max(passiveToolsState.LaserBeam.TerminalCapIntensity,
                                                                    passiveToolConfig.LaserBeam.TerminalCapIntensity);
        passiveToolsState.LaserBeam.ContactFlareIntensity = math.max(passiveToolsState.LaserBeam.ContactFlareIntensity,
                                                                     passiveToolConfig.LaserBeam.ContactFlareIntensity);
        passiveToolsState.LaserBeam.WobbleAmplitude = math.max(passiveToolsState.LaserBeam.WobbleAmplitude,
                                                               passiveToolConfig.LaserBeam.WobbleAmplitude);
        passiveToolsState.LaserBeam.BubbleDriftSpeed = math.max(passiveToolsState.LaserBeam.BubbleDriftSpeed,
                                                                passiveToolConfig.LaserBeam.BubbleDriftSpeed);
        passiveToolsState.LaserBeam.VisualPresetId = passiveToolConfig.LaserBeam.VisualPresetId;
        passiveToolsState.LaserBeam.BodyProfile = passiveToolConfig.LaserBeam.BodyProfile;
        passiveToolsState.LaserBeam.SourceShape = passiveToolConfig.LaserBeam.SourceShape;
        passiveToolsState.LaserBeam.TerminalCapShape = passiveToolConfig.LaserBeam.TerminalCapShape;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends orbital projection configs from one passive payload into the aggregate passive snapshot.
    /// </summary>
    /// <param name="passiveToolsState">Aggregate passive state updated in place for systems that consume rebuilt passive snapshots.</param>
    /// <param name="passiveToolConfig">Passive payload that may contain one or more orbital projection entries.</param>
    private static void AccumulateOrbitalProjections(ref PlayerPassiveToolsState passiveToolsState,
                                                     in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.HasOrbitalProjections == 0 ||
            passiveToolConfig.OrbitalProjections.Length <= 0)
            return;

        for (int projectionIndex = 0; projectionIndex < passiveToolConfig.OrbitalProjections.Length; projectionIndex++)
        {
            if (passiveToolsState.OrbitalProjections.Length >= passiveToolsState.OrbitalProjections.Capacity)
                break;

            passiveToolsState.OrbitalProjections.Add(passiveToolConfig.OrbitalProjections[projectionIndex]);
        }

        passiveToolsState.HasOrbitalProjections = passiveToolsState.OrbitalProjections.Length > 0 ? (byte)1 : (byte)0;
    }
    #endregion

    #endregion
}
