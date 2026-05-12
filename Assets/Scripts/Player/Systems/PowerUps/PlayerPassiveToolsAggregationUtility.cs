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
    /// <returns>Aggregated passive runtime state.<returns>
    public static PlayerPassiveToolsState BuildPassiveToolsState(Entity entity, in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup)
    {
        PlayerPassiveToolsState passiveToolsState = CreateDefaultState();

        if (!equippedPassiveToolsLookup.HasBuffer(entity))
            return passiveToolsState;

        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];
        return BuildPassiveToolsState(equippedPassiveToolsBuffer);
    }

    /// <summary>
    /// Builds aggregated passive state from a direct equipped-passives dynamic buffer.
    /// </summary>
    /// <param name="equippedPassiveToolsBuffer">Runtime equipped passive entries.</param>
    /// <returns>Aggregated passive runtime state.<returns>
    public static PlayerPassiveToolsState BuildPassiveToolsState(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer)
    {
        PlayerPassiveToolsState passiveToolsState = CreateDefaultState();

        if (!equippedPassiveToolsBuffer.IsCreated)
            return passiveToolsState;

        for (int passiveToolIndex = 0; passiveToolIndex < equippedPassiveToolsBuffer.Length; passiveToolIndex++)
        {
            EquippedPassiveToolElement equippedPassiveTool = equippedPassiveToolsBuffer[passiveToolIndex];
            AccumulatePassiveTool(ref passiveToolsState, in equippedPassiveTool.Tool);
        }

        return passiveToolsState;
    }

    /// <summary>
    /// Builds the neutral passive state used when gameplay needs projectile math without equipped passive effects.
    /// </summary>
    /// <returns>Passive state with projectile multipliers initialized to 1 and all hooks disabled.<returns>
    public static PlayerPassiveToolsState CreateDefaultState()
    {
        return new PlayerPassiveToolsState
        {
            ProjectileSizeMultiplier = 1f,
            ProjectileDamageMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            ProjectileLifetimeSecondsMultiplier = 1f,
            ProjectileLifetimeRangeMultiplier = 1f,
            HasShotgun = 0,
            Shotgun = default,
            HasElementalProjectiles = 0,
            ElementalProjectiles = default,
            HasPerfectCircle = 0,
            PerfectCircle = default,
            HasBouncingProjectiles = 0,
            BouncingProjectiles = default,
            HasSplittingProjectiles = 0,
            SplittingProjectiles = default,
            HasExplosion = 0,
            Explosion = default,
            HasElementalTrail = 0,
            ElementalTrail = default,
            HasHeal = 0,
            Heal = default,
            HasBulletTime = 0,
            BulletTime = default,
            HasLaserBeam = 0,
            LaserBeam = default
        };
    }

    /// <summary>
    /// Builds a neutral passive snapshot that exposes only one Laser Beam config.
    /// laserBeamConfig: Standalone Laser Beam settings to expose to beam simulation and presentation.
    /// returns Passive state with only HasLaserBeam enabled.
    /// </summary>
    public static PlayerPassiveToolsState CreateStandaloneLaserBeamState(in LaserBeamPassiveConfig laserBeamConfig)
    {
        PlayerPassiveToolsState passiveToolsState = CreateDefaultState();
        passiveToolsState.HasLaserBeam = 1;
        passiveToolsState.LaserBeam = laserBeamConfig;
        return passiveToolsState;
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

    #endregion
}
