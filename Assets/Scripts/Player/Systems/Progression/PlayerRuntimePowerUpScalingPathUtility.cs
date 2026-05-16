using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies runtime power-up scaling values to active and passive configs using modular payload paths.
/// </summary>
internal static class PlayerRuntimePowerUpScalingPathUtility
{
    #region Constants
    private const string PassiveLaserBeamPayloadPrefix = "laserBeam.";
    private const string HoldChargeChargedLaserBeamPayloadPrefix = "holdCharge.chargedLaserBeam.";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one resolved Add Scaling value to the runtime config field represented by the provided payload path.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="unlockKind">Active or passive target kind owning the runtime config.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    public static void ApplyValue(string payloadPath,
                                  PlayerPowerUpUnlockKind unlockKind,
                                  float resolvedValue,
                                  ref PlayerPowerUpSlotConfig activeSlotConfig,
                                  ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath))
            return;

        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                ApplyActiveValue(payloadPath, resolvedValue, ref activeSlotConfig);
                ApplyTriggeredProjectilePassiveValue(payloadPath, resolvedValue, ref activeSlotConfig);
                ApplyEmbeddedTogglePassiveValue(payloadPath, resolvedValue, ref activeSlotConfig);
                return;
            case PlayerPowerUpUnlockKind.Passive:
                ApplyPassiveValue(payloadPath, resolvedValue, ref passiveToolConfig);
                return;
        }
    }

    /// <summary>
    /// Applies one resolved boolean Add Scaling value to the runtime config field represented by the provided payload path.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="unlockKind">Active or passive target kind owning the runtime config.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    public static void ApplyBooleanValue(string payloadPath,
                                         PlayerPowerUpUnlockKind unlockKind,
                                         bool resolvedValue,
                                         ref PlayerPowerUpSlotConfig activeSlotConfig,
                                         ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath))
            return;

        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                ApplyActiveBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig);
                ApplyTriggeredProjectilePassiveBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig);
                ApplyEmbeddedTogglePassiveBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig);
                return;
            case PlayerPowerUpUnlockKind.Passive:
                ApplyPassiveBooleanValue(payloadPath, resolvedValue, ref passiveToolConfig);
                return;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one resolved Add Scaling value to an active-slot runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyActiveValue(string payloadPath,
                                         float resolvedValue,
                                         ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (TryApplyLaserBeamValue(payloadPath,
                                   HoldChargeChargedLaserBeamPayloadPrefix,
                                   resolvedValue,
                                   ref activeSlotConfig.ChargeShot.ChargedLaserBeam))
            return;

        if (PlayerRuntimePowerUpBombScalingApplyUtility.TryApplyValue(payloadPath, resolvedValue, ref activeSlotConfig.Bomb))
            return;

        switch (payloadPath)
        {
            case "resourceGate.activationResource":
                activeSlotConfig.ActivationResource = PlayerRuntimeScalingEnumUtility.ResolvePowerUpResourceType(resolvedValue);
                return;
            case "resourceGate.maintenanceResource":
                activeSlotConfig.MaintenanceResource = PlayerRuntimeScalingEnumUtility.ResolvePowerUpResourceType(resolvedValue);
                return;
            case "resourceGate.maximumEnergy":
                activeSlotConfig.MaximumEnergy = math.max(0f, resolvedValue);
                return;
            case "resourceGate.activationCost":
                activeSlotConfig.ActivationCost = math.max(0f, resolvedValue);
                return;
            case "resourceGate.maintenanceCostPerSecond":
                activeSlotConfig.MaintenanceCostPerSecond = math.max(0f, resolvedValue);
                return;
            case "resourceGate.maintenanceTicksPerSecond":
                activeSlotConfig.MaintenanceTicksPerSecond = math.max(0f, resolvedValue);
                return;
            case "resourceGate.chargePerTrigger":
                activeSlotConfig.ChargePerTrigger = math.max(0f, resolvedValue);
                return;
            case "resourceGate.cooldownSeconds":
                activeSlotConfig.CooldownSeconds = math.max(0f, resolvedValue);
                return;
            case "resourceGate.minimumActivationEnergyPercent":
                activeSlotConfig.MinimumActivationEnergyPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "resourceGate.chargeType":
                activeSlotConfig.ChargeType = PlayerRuntimeScalingEnumUtility.ResolvePowerUpChargeType(resolvedValue);
                return;
            case "holdCharge.requiredCharge":
                activeSlotConfig.ChargeShot.RequiredCharge = math.max(0f, resolvedValue);

                if (activeSlotConfig.ChargeShot.MaximumCharge < activeSlotConfig.ChargeShot.RequiredCharge)
                    activeSlotConfig.ChargeShot.MaximumCharge = activeSlotConfig.ChargeShot.RequiredCharge;

                return;
            case "holdCharge.maximumCharge":
                activeSlotConfig.ChargeShot.MaximumCharge = math.max(activeSlotConfig.ChargeShot.RequiredCharge, resolvedValue);
                return;
            case "holdCharge.chargeRatePerSecond":
                activeSlotConfig.ChargeShot.ChargeRatePerSecond = math.max(0f, resolvedValue);
                return;
            case "holdCharge.decayAfterReleasePercentPerSecond":
                activeSlotConfig.ChargeShot.DecayAfterReleasePercentPerSecond = math.max(0f, resolvedValue);
                return;
            case "holdCharge.passiveChargeGainPercentPerSecond":
                activeSlotConfig.ChargeShot.PassiveChargeGainPercentPerSecond = math.max(0f, resolvedValue);
                return;
            case "holdCharge.laserDurationSeconds":
                activeSlotConfig.ChargeShot.LaserDurationSeconds = math.max(0f, resolvedValue);
                return;
            case "holdCharge.chargedLaserDurationSeconds":
                activeSlotConfig.ChargeShot.ChargedLaserDurationSeconds = math.max(0f, resolvedValue);
                return;
            case "holdCharge.maximumPlayerSlowPercent":
                activeSlotConfig.ChargeShot.MaximumPlayerSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "projectilePatternCone.projectileCount":
                activeSlotConfig.Shotgun.ProjectileCount = math.max(1, (int)resolvedValue);
                return;
            case "projectilePatternCone.coneAngleDegrees":
                activeSlotConfig.Shotgun.ConeAngleDegrees = math.max(0f, resolvedValue);
                return;
            case "projectilePatternCone.laserDurationSeconds":
                activeSlotConfig.Shotgun.LaserDurationSeconds = math.max(0f, resolvedValue);
                return;
            case "dash.distance":
                activeSlotConfig.Dash.Distance = math.max(0f, resolvedValue);
                return;
            case "dash.directionMode":
                activeSlotConfig.Dash.DirectionMode = PlayerRuntimeScalingEnumUtility.ResolveDashDirectionMode(resolvedValue);
                return;
            case "dash.duration":
                activeSlotConfig.Dash.Duration = math.max(0.01f, resolvedValue);
                return;
            case "dash.speedTransitionInSeconds":
                activeSlotConfig.Dash.SpeedTransitionInSeconds = math.max(0f, resolvedValue);
                return;
            case "dash.speedTransitionOutSeconds":
                activeSlotConfig.Dash.SpeedTransitionOutSeconds = math.max(0f, resolvedValue);
                return;
            case "dash.wallBounceIntensity":
                activeSlotConfig.Dash.WallBounceIntensity = math.clamp(resolvedValue, 0f, 1f);
                return;
            case "dash.invulnerabilityExtraTime":
                activeSlotConfig.Dash.InvulnerabilityExtraTime = math.max(0f, resolvedValue);
                return;
            case "bulletTime.duration":
                activeSlotConfig.BulletTime.Duration = math.max(0.05f, resolvedValue);
                return;
            case "bulletTime.enemySlowPercent":
                activeSlotConfig.BulletTime.EnemySlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "bulletTime.transitionTimeSeconds":
                activeSlotConfig.BulletTime.TransitionTimeSeconds = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.healAmount":
                activeSlotConfig.PortableHealthPack.HealAmount = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.durationSeconds":
                activeSlotConfig.PortableHealthPack.DurationSeconds = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.tickIntervalSeconds":
                activeSlotConfig.PortableHealthPack.TickIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "healMissingHealth.applyMode":
                activeSlotConfig.PortableHealthPack.ApplyMode = PlayerRuntimeScalingEnumUtility.ResolvePowerUpHealApplicationMode(resolvedValue);
                return;
        }
    }

    /// <summary>
    /// Applies one resolved Add Scaling value to the embedded passive payload owned by an active toggleable power-up.
    /// This keeps runtime scaling generic for active power-ups that expose passive module payloads through TogglePassiveTool.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyEmbeddedTogglePassiveValue(string payloadPath,
                                                        float resolvedValue,
                                                        ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.ToolKind != ActiveToolKind.PassiveToggle)
            return;

        if (activeSlotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = activeSlotConfig.TogglePassiveTool;
        ApplyPassiveValue(payloadPath, resolvedValue, ref togglePassiveTool);
        activeSlotConfig.TogglePassiveTool = togglePassiveTool;
    }

    /// <summary>
    /// Applies one resolved Add Scaling value to the transient passive snapshot owned by projectile-shooting actives.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyTriggeredProjectilePassiveValue(string payloadPath,
                                                            float resolvedValue,
                                                            ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.TriggeredProjectilePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig triggeredProjectilePassiveTool = activeSlotConfig.TriggeredProjectilePassiveTool;
        ApplyPassiveValue(payloadPath, resolvedValue, ref triggeredProjectilePassiveTool);
        activeSlotConfig.TriggeredProjectilePassiveTool = triggeredProjectilePassiveTool;
    }

    /// <summary>
    /// Applies one resolved boolean Add Scaling value to the embedded passive payload owned by an active toggleable power-up.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyEmbeddedTogglePassiveBooleanValue(string payloadPath,
                                                               bool resolvedValue,
                                                               ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.ToolKind != ActiveToolKind.PassiveToggle)
            return;

        if (activeSlotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = activeSlotConfig.TogglePassiveTool;
        ApplyPassiveBooleanValue(payloadPath, resolvedValue, ref togglePassiveTool);
        activeSlotConfig.TogglePassiveTool = togglePassiveTool;
    }

    /// <summary>
    /// Applies one resolved boolean Add Scaling value to the transient passive snapshot owned by projectile-shooting actives.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyTriggeredProjectilePassiveBooleanValue(string payloadPath,
                                                                   bool resolvedValue,
                                                                   ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.TriggeredProjectilePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig triggeredProjectilePassiveTool = activeSlotConfig.TriggeredProjectilePassiveTool;
        ApplyPassiveBooleanValue(payloadPath, resolvedValue, ref triggeredProjectilePassiveTool);
        activeSlotConfig.TriggeredProjectilePassiveTool = triggeredProjectilePassiveTool;
    }

    /// <summary>
    /// Applies one resolved Add Scaling value to a passive-tool runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    private static void ApplyPassiveValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (TryApplyLaserBeamValue(payloadPath,
                                   PassiveLaserBeamPayloadPrefix,
                                   resolvedValue,
                                   ref passiveToolConfig.LaserBeam))
            return;

        switch (payloadPath)
        {
            case "projectileOrbitOverride.pathMode":
                passiveToolConfig.PerfectCircle.PathMode = PlayerRuntimeScalingEnumUtility.ResolveProjectileOrbitPathMode(resolvedValue);
                return;
            case "resourceGate.cooldownSeconds":
                passiveToolConfig.Heal.CooldownSeconds = math.max(0f, resolvedValue);
                passiveToolConfig.BulletTime.CooldownSeconds = math.max(0f, resolvedValue);
                return;
            case "projectilePatternCone.projectileCount":
                passiveToolConfig.Shotgun.ProjectileCount = math.max(1, (int)resolvedValue);
                return;
            case "projectilePatternCone.coneAngleDegrees":
                passiveToolConfig.Shotgun.ConeAngleDegrees = math.max(0f, resolvedValue);
                return;
            case "trailSpawn.trailSegmentLifetimeSeconds":
                passiveToolConfig.ElementalTrail.TrailSegmentLifetimeSeconds = math.max(0.05f, resolvedValue);
                return;
            case "trailSpawn.trailSpawnDistance":
                passiveToolConfig.ElementalTrail.TrailSpawnDistance = math.max(0f, resolvedValue);
                return;
            case "trailSpawn.trailSpawnIntervalSeconds":
                passiveToolConfig.ElementalTrail.TrailSpawnIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "trailSpawn.trailRadius":
                passiveToolConfig.ElementalTrail.TrailRadius = math.max(0f, resolvedValue);
                return;
            case "trailSpawn.maxActiveSegmentsPerPlayer":
                passiveToolConfig.ElementalTrail.MaxActiveSegmentsPerPlayer = math.max(1, (int)resolvedValue);
                return;
            case "trailSpawn.trailAttachedVfxOffset.x":
                passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset.x = resolvedValue;
                return;
            case "trailSpawn.trailAttachedVfxOffset.y":
                passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset.y = resolvedValue;
                return;
            case "trailSpawn.trailAttachedVfxOffset.z":
                passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset.z = resolvedValue;
                return;
            case "elementalAreaTick.stacksPerTick":
                passiveToolConfig.ElementalTrail.StacksPerTick = math.max(0f, resolvedValue);
                return;
            case "elementalAreaTick.applyIntervalSeconds":
                passiveToolConfig.ElementalTrail.ApplyIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "deathExplosion.cooldownSeconds":
                passiveToolConfig.Explosion.CooldownSeconds = math.max(0f, resolvedValue);
                return;
            case "deathExplosion.radius":
                passiveToolConfig.Explosion.Radius = math.max(0f, resolvedValue);
                return;
            case "deathExplosion.damage":
                passiveToolConfig.Explosion.Damage = math.max(0f, resolvedValue);
                return;
            case "deathExplosion.triggerOffset.x":
                passiveToolConfig.Explosion.TriggerOffset.x = resolvedValue;
                return;
            case "deathExplosion.triggerOffset.y":
                passiveToolConfig.Explosion.TriggerOffset.y = resolvedValue;
                return;
            case "deathExplosion.triggerOffset.z":
                passiveToolConfig.Explosion.TriggerOffset.z = resolvedValue;
                return;
            case "deathExplosion.vfxScaleMultiplier":
                passiveToolConfig.Explosion.VfxScaleMultiplier = math.max(0.01f, resolvedValue);
                return;
            case "projectileOrbitOverride.radialEntrySpeed":
                passiveToolConfig.PerfectCircle.RadialEntrySpeed = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitalSpeed":
                passiveToolConfig.PerfectCircle.OrbitalSpeed = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitRadiusMin":
                passiveToolConfig.PerfectCircle.OrbitRadiusMin = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitRadiusMax":
                passiveToolConfig.PerfectCircle.OrbitRadiusMax = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitPulseFrequency":
                passiveToolConfig.PerfectCircle.OrbitPulseFrequency = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitEntryRatio":
                passiveToolConfig.PerfectCircle.OrbitEntryRatio = math.clamp(resolvedValue, 0f, 1f);
                return;
            case "projectileOrbitOverride.orbitBlendDuration":
                passiveToolConfig.PerfectCircle.OrbitBlendDuration = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.heightOffset":
                passiveToolConfig.PerfectCircle.HeightOffset = resolvedValue;
                return;
            case "projectileOrbitOverride.goldenAngleDegrees":
                passiveToolConfig.PerfectCircle.GoldenAngleDegrees = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralStartRadius":
                passiveToolConfig.PerfectCircle.SpiralStartRadius = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralMaximumRadius":
                passiveToolConfig.PerfectCircle.SpiralMaximumRadius = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralAngularSpeedDegreesPerSecond":
                passiveToolConfig.PerfectCircle.SpiralAngularSpeedDegreesPerSecond = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralGrowthMultiplier":
                passiveToolConfig.PerfectCircle.SpiralGrowthMultiplier = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralTurnsBeforeDespawn":
                passiveToolConfig.PerfectCircle.SpiralTurnsBeforeDespawn = math.max(0.1f, resolvedValue);
                return;
            case "projectileBounceOnWalls.maxBounces":
                passiveToolConfig.BouncingProjectiles.MaxBounces = math.max(0, (int)resolvedValue);
                return;
            case "projectileBounceOnWalls.speedPercentChangePerBounce":
                passiveToolConfig.BouncingProjectiles.SpeedPercentChangePerBounce = resolvedValue;
                return;
            case "projectileBounceOnWalls.minimumSpeedMultiplierAfterBounce":
                passiveToolConfig.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce = math.max(0f, resolvedValue);
                return;
            case "projectileBounceOnWalls.maximumSpeedMultiplierAfterBounce":
                passiveToolConfig.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce = math.max(0f, resolvedValue);
                return;
            case "projectileSplit.splitProjectileCount":
                passiveToolConfig.SplittingProjectiles.SplitProjectileCount = math.max(1, (int)resolvedValue);
                return;
            case "projectileSplit.splitOffsetDegrees":
                passiveToolConfig.SplittingProjectiles.SplitOffsetDegrees = resolvedValue;
                return;
            case "projectileSplit.splitDamagePercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitDamageMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "projectileSplit.splitSizePercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitSizeMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "projectileSplit.splitSpeedPercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitSpeedMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "projectileSplit.splitLifetimePercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitLifetimeMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "healMissingHealth.healAmount":
                passiveToolConfig.Heal.HealAmount = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.durationSeconds":
                passiveToolConfig.Heal.DurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case "healMissingHealth.tickIntervalSeconds":
                passiveToolConfig.Heal.TickIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "bulletTime.duration":
                passiveToolConfig.BulletTime.DurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case "bulletTime.enemySlowPercent":
                passiveToolConfig.BulletTime.EnemySlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "bulletTime.transitionTimeSeconds":
                passiveToolConfig.BulletTime.TransitionTimeSeconds = math.max(0f, resolvedValue);
                return;
        }
    }

    /// <summary>
    /// Applies a resolved Add Scaling value to a Laser Beam config using a payload-prefix namespace.
    /// This is shared by passive Laser Beam payloads and the standalone charged-shot beam payload.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="prefix">Path prefix that identifies the owning payload namespace.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="laserBeamConfig">Mutable Laser Beam config being rebuilt from immutable baseline data.</param>
    /// <returns>True when the path matched a Laser Beam field and was applied.</returns>
    private static bool TryApplyLaserBeamValue(string payloadPath,
                                               string prefix,
                                               float resolvedValue,
                                               ref LaserBeamPassiveConfig laserBeamConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath) || string.IsNullOrWhiteSpace(prefix))
            return false;

        if (!payloadPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string laserBeamFieldPath = payloadPath.Substring(prefix.Length);

        switch (laserBeamFieldPath)
        {
            case "damageMultiplier":
                laserBeamConfig.DamageMultiplier = math.max(0f, resolvedValue);
                return true;
            case "continuousDamagePerSecondMultiplier":
                laserBeamConfig.ContinuousDamagePerSecondMultiplier = math.max(0f, resolvedValue);
                return true;
            case "virtualProjectileSpeedMultiplier":
                laserBeamConfig.VirtualProjectileSpeedMultiplier = math.max(0f, resolvedValue);
                return true;
            case "damageTickIntervalSeconds":
                laserBeamConfig.DamageTickIntervalSeconds = math.max(0.0001f, resolvedValue);
                return true;
            case "maximumContinuousActiveSeconds":
                laserBeamConfig.MaximumContinuousActiveSeconds = math.max(0f, resolvedValue);
                return true;
            case "cooldownSeconds":
                laserBeamConfig.CooldownSeconds = math.max(0f, resolvedValue);
                return true;
            case "maximumBounceSegments":
                laserBeamConfig.MaximumBounceSegments = math.max(0, (int)resolvedValue);
                return true;
            case "visualPresetId":
                laserBeamConfig.VisualPresetId = math.max(0, (int)math.round(resolvedValue));
                return true;
            case "bodyProfile":
                laserBeamConfig.BodyProfile = PlayerRuntimeScalingEnumUtility.ResolveLaserBeamBodyProfile(resolvedValue);
                return true;
            case "sourceShape":
                laserBeamConfig.SourceShape = PlayerRuntimeScalingEnumUtility.ResolveLaserBeamCapShape(resolvedValue);
                return true;
            case "terminalCapShape":
                laserBeamConfig.TerminalCapShape = PlayerRuntimeScalingEnumUtility.ResolveLaserBeamCapShape(resolvedValue);
                return true;
            case "bodyWidthMultiplier":
                laserBeamConfig.BodyWidthMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "collisionWidthMultiplier":
                laserBeamConfig.CollisionWidthMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "sourceScaleMultiplier":
                laserBeamConfig.SourceScaleMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "terminalCapScaleMultiplier":
                laserBeamConfig.TerminalCapScaleMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "contactFlareScaleMultiplier":
                laserBeamConfig.ContactFlareScaleMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "bodyOpacity":
                laserBeamConfig.BodyOpacity = math.max(0.01f, resolvedValue);
                return true;
            case "coreWidthMultiplier":
                laserBeamConfig.CoreWidthMultiplier = math.max(0.05f, resolvedValue);
                return true;
            case "coreBrightness":
                laserBeamConfig.CoreBrightness = math.max(0f, resolvedValue);
                return true;
            case "rimBrightness":
                laserBeamConfig.RimBrightness = math.max(0f, resolvedValue);
                return true;
            case "flowScrollSpeed":
                laserBeamConfig.FlowScrollSpeed = math.max(0f, resolvedValue);
                return true;
            case "flowPulseFrequency":
                laserBeamConfig.FlowPulseFrequency = math.max(0f, resolvedValue);
                return true;
            case "stormTwistSpeed":
                laserBeamConfig.StormTwistSpeed = math.max(0f, resolvedValue);
                return true;
            case "stormTickPostTravelHoldSeconds":
                laserBeamConfig.StormTickPostTravelHoldSeconds = math.max(0f, resolvedValue);
                return true;
            case "stormIdleIntensity":
                laserBeamConfig.StormIdleIntensity = math.max(0f, resolvedValue);
                return true;
            case "stormBurstIntensity":
                laserBeamConfig.StormBurstIntensity = math.max(0f, resolvedValue);
                return true;
            case "sourceOffset":
                laserBeamConfig.SourceOffset = math.max(0f, resolvedValue);
                return true;
            case "sourceDischargeIntensity":
                laserBeamConfig.SourceDischargeIntensity = math.max(0f, resolvedValue);
                return true;
            case "stormShellWidthMultiplier":
                laserBeamConfig.StormShellWidthMultiplier = math.max(0.01f, resolvedValue);
                return true;
            case "stormShellSeparation":
                laserBeamConfig.StormShellSeparation = math.max(0f, resolvedValue);
                return true;
            case "stormRingFrequency":
                laserBeamConfig.StormRingFrequency = math.max(0f, resolvedValue);
                return true;
            case "stormRingThickness":
                laserBeamConfig.StormRingThickness = math.max(0.01f, resolvedValue);
                return true;
            case "stormTickTravelSpeed":
                laserBeamConfig.StormTickTravelSpeed = math.max(0f, resolvedValue);
                return true;
            case "stormTickDamageLengthTolerance":
                laserBeamConfig.StormTickDamageLengthTolerance = math.max(0f, resolvedValue);
                return true;
            case "terminalCapIntensity":
                laserBeamConfig.TerminalCapIntensity = math.max(0f, resolvedValue);
                return true;
            case "contactFlareIntensity":
                laserBeamConfig.ContactFlareIntensity = math.max(0f, resolvedValue);
                return true;
            case "wobbleAmplitude":
                laserBeamConfig.WobbleAmplitude = math.max(0f, resolvedValue);
                return true;
            case "bubbleDriftSpeed":
                laserBeamConfig.BubbleDriftSpeed = math.max(0f, resolvedValue);
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies one resolved boolean Add Scaling value to an active-slot runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyActiveBooleanValue(string payloadPath,
                                                bool resolvedValue,
                                                ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (PlayerRuntimePowerUpBombScalingApplyUtility.TryApplyBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig.Bomb))
            return;

        switch (payloadPath)
        {
            case "resourceGate.isToggleable":
                activeSlotConfig.Toggleable = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "resourceGate.allowRechargeDuringToggleStartupLock":
                activeSlotConfig.AllowRechargeDuringToggleStartupLock = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "holdCharge.decayAfterRelease":
                activeSlotConfig.ChargeShot.DecayAfterRelease = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "holdCharge.passiveChargeGainWhileReleased":
                activeSlotConfig.ChargeShot.PassiveChargeGainWhileReleased = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "holdCharge.useChargedLaserBeam":
                activeSlotConfig.ChargeShot.UseChargedLaserBeam = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "holdCharge.slowPlayerWhileCharging":
                activeSlotConfig.ChargeShot.SlowPlayerWhileCharging = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "dash.grantsInvulnerability":
                activeSlotConfig.Dash.GrantsInvulnerability = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "suppressShooting.suppressBaseShootingWhileActive":
                activeSlotConfig.SuppressBaseShootingWhileActive = resolvedValue ? (byte)1 : (byte)0;
                return;
        }
    }

    /// <summary>
    /// Applies one resolved boolean Add Scaling value to a passive-tool runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    private static void ApplyPassiveBooleanValue(string payloadPath,
                                                 bool resolvedValue,
                                                 ref PlayerPassiveToolConfig passiveToolConfig)
    {
        switch (payloadPath)
        {
            case "projectileOrbitOverride.spiralClockwise":
                passiveToolConfig.PerfectCircle.SpiralClockwise = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "deathExplosion.affectAllEnemiesInRadius":
                passiveToolConfig.Explosion.AffectAllEnemiesInRadius = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "deathExplosion.scaleVfxToRadius":
                passiveToolConfig.Explosion.ScaleVfxToRadius = resolvedValue ? (byte)1 : (byte)0;
                return;
        }
    }
    #endregion

    #endregion
}
