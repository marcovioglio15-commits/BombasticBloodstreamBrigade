using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies runtime power-up scaling values to active and passive configs using modular payload paths.
/// </summary>
public static class PlayerRuntimePowerUpScalingPathUtility
{
    #region Constants
    private const string PassiveLaserBeamPayloadPrefix = "laserBeam.";
    private const string HoldChargeChargedLaserBeamPayloadPrefix = "holdCharge.chargedLaserBeam.";
    private const string ElementalAreaTickEffectPrefix = "elementalAreaTick.effectData.";
    private const string OrbitalProjectionEntryPrefix = "orbitalProjections.projections.Array.data[";
    private const int MaximumFixedString64Utf8Bytes = 61;
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

    /// <summary>
    /// Applies one resolved token Add Scaling value to the runtime config field represented by the provided payload path.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="unlockKind">Active or passive target kind owning the runtime config.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    public static void ApplyTokenValue(string payloadPath,
                                       PlayerPowerUpUnlockKind unlockKind,
                                       string resolvedValue,
                                       ref PlayerPowerUpSlotConfig activeSlotConfig,
                                       ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath))
            return;

        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                if (ApplyActiveTokenValue(payloadPath, resolvedValue, ref activeSlotConfig))
                    return;
                ApplyTriggeredProjectilePassiveTokenValue(payloadPath, resolvedValue, ref activeSlotConfig);
                ApplyEmbeddedTogglePassiveTokenValue(payloadPath, resolvedValue, ref activeSlotConfig);
                return;
            case PlayerPowerUpUnlockKind.Passive:
                if (ApplyPassiveSwitchWeaponTokenValue(payloadPath, resolvedValue, ref passiveToolConfig))
                    return;
                ApplyPassiveTokenValue(payloadPath, resolvedValue, ref passiveToolConfig);
                return;
        }
    }

    /// <summary>
    /// Applies token Add Scaling values to active-slot fields that store defined string IDs. Returns
    /// true when the path matched so callers can short-circuit downstream token application stages.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted an active-slot string field.</returns>
    private static bool ApplyActiveTokenValue(string payloadPath,
                                               string resolvedValue,
                                               ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (!string.Equals(payloadPath, "switchWeapon.weaponId", System.StringComparison.Ordinal))
            return false;

        if (TryBuildFixedString64(resolvedValue, out FixedString64Bytes resolvedId))
            activeSlotConfig.ActiveWeaponId = resolvedId;

        return true;
    }

    /// <summary>
    /// Applies token Add Scaling values to passive-tool fields that store defined string IDs.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted a passive-tool string field.</returns>
    private static bool ApplyPassiveSwitchWeaponTokenValue(string payloadPath,
                                                            string resolvedValue,
                                                            ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (!string.Equals(payloadPath, "switchWeapon.weaponId", System.StringComparison.Ordinal))
            return false;

        if (TryBuildFixedString64(resolvedValue, out FixedString64Bytes resolvedId))
            passiveToolConfig.WeaponId = resolvedId;

        return true;
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

        if (PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyValue(payloadPath, resolvedValue, ref activeSlotConfig.ImpactFrame))
            return;

        if (PlayerRuntimePowerUpGhostTrailScalingApplyUtility.TryApplyValue(payloadPath, resolvedValue, ref activeSlotConfig.GhostTrail))
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
            case "holdCharge.chargeAnimationClipSlot":
                activeSlotConfig.ChargeShot.ChargeAnimationClipSlot = PlayerRuntimeScalingEnumUtility.ResolvePlayerChargeAnimationClipSlot(resolvedValue);
                return;
            case "holdCharge.releaseAnimationClipSlot":
                activeSlotConfig.ChargeShot.ReleaseAnimationClipSlot = PlayerRuntimeScalingEnumUtility.ResolvePlayerReleaseAnimationClipSlot(resolvedValue);
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
    /// Applies one resolved token Add Scaling value to the embedded passive payload owned by an active toggleable power-up.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyEmbeddedTogglePassiveTokenValue(string payloadPath,
                                                            string resolvedValue,
                                                            ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.ToolKind != ActiveToolKind.PassiveToggle)
            return;

        if (activeSlotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = activeSlotConfig.TogglePassiveTool;
        ApplyPassiveTokenValue(payloadPath, resolvedValue, ref togglePassiveTool);
        activeSlotConfig.TogglePassiveTool = togglePassiveTool;
    }

    /// <summary>
    /// Applies one resolved token Add Scaling value to the transient passive snapshot owned by projectile-shooting actives.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="activeSlotConfig">Mutable active slot config rebuilt from immutable baselines.</param>
    private static void ApplyTriggeredProjectilePassiveTokenValue(string payloadPath,
                                                                  string resolvedValue,
                                                                  ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.TriggeredProjectilePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig triggeredProjectilePassiveTool = activeSlotConfig.TriggeredProjectilePassiveTool;
        ApplyPassiveTokenValue(payloadPath, resolvedValue, ref triggeredProjectilePassiveTool);
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

        ElementalEffectConfig elementalAreaTickEffect = passiveToolConfig.ElementalTrail.Effect;

        if (TryApplyElementalEffectValue(payloadPath,
                                         ElementalAreaTickEffectPrefix,
                                         resolvedValue,
                                         ref elementalAreaTickEffect))
        {
            NormalizeElementalEffectStackBounds(ref elementalAreaTickEffect);
            passiveToolConfig.ElementalTrail.Effect = elementalAreaTickEffect;
            return;
        }

        if (TryApplyOrbitalProjectionValue(payloadPath, resolvedValue, ref passiveToolConfig))
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
    /// Applies one resolved token Add Scaling value to a passive-tool runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config rebuilt from immutable baselines.</param>
    private static void ApplyPassiveTokenValue(string payloadPath,
                                               string resolvedValue,
                                               ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (TryApplyOrbitalProjectionTokenValue(payloadPath, resolvedValue, ref passiveToolConfig))
            return;
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
            case "firingMoveSpeedMultiplier":
                laserBeamConfig.FiringMoveSpeedMultiplier = math.max(0f, resolvedValue);
                return true;
            case "firingRotationSpeedMultiplier":
                laserBeamConfig.FiringRotationSpeedMultiplier = math.max(0f, resolvedValue);
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
    /// Applies a resolved boolean Add Scaling value to a Laser Beam config using a payload-prefix namespace.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="prefix">Path prefix that identifies the owning payload namespace.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="laserBeamConfig">Mutable Laser Beam config being rebuilt from immutable baseline data.</param>
    /// <returns>True when the path matched a Laser Beam boolean field and was applied.</returns>
    private static bool TryApplyLaserBeamBooleanValue(string payloadPath,
                                                      string prefix,
                                                      bool resolvedValue,
                                                      ref LaserBeamPassiveConfig laserBeamConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath) || string.IsNullOrWhiteSpace(prefix))
            return false;

        if (!payloadPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string laserBeamFieldPath = payloadPath.Substring(prefix.Length);

        switch (laserBeamFieldPath)
        {
            case "applyPlayerHandlingNerfWhileFiring":
                laserBeamConfig.ApplyPlayerHandlingNerfWhileFiring = resolvedValue ? (byte)1 : (byte)0;
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies one numeric Add Scaling value to a shared elemental effect payload.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="prefix">Path prefix that identifies the owning effect payload namespace.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="effectConfig">Mutable elemental effect config being rebuilt from immutable baseline data.</param>
    /// <returns>True when the path matched an elemental effect field and was applied.</returns>
    private static bool TryApplyElementalEffectValue(string payloadPath,
                                                     string prefix,
                                                     float resolvedValue,
                                                     ref ElementalEffectConfig effectConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath) || string.IsNullOrWhiteSpace(prefix))
            return false;

        if (!payloadPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string effectFieldPath = payloadPath.Substring(prefix.Length);

        switch (effectFieldPath)
        {
            case "elementType":
                effectConfig.ElementType = PlayerRuntimeScalingEnumUtility.ResolveElementType(resolvedValue);
                return true;
            case "effectKind":
                effectConfig.EffectKind = PlayerRuntimeScalingEnumUtility.ResolveElementalEffectKind(resolvedValue);
                return true;
            case "procMode":
                effectConfig.ProcMode = PlayerRuntimeScalingEnumUtility.ResolveElementalProcMode(resolvedValue);
                return true;
            case "reapplyMode":
                effectConfig.ReapplyMode = PlayerRuntimeScalingEnumUtility.ResolveElementalProcReapplyMode(resolvedValue);
                return true;
            case "procThresholdStacks":
                effectConfig.ProcThresholdStacks = math.max(0.1f, resolvedValue);
                return true;
            case "maximumStacks":
                effectConfig.MaximumStacks = math.max(0.1f, resolvedValue);
                return true;
            case "stackDecayPerSecond":
                effectConfig.StackDecayPerSecond = math.max(0f, resolvedValue);
                return true;
            case "dotDamagePerTick":
                effectConfig.DotDamagePerTick = math.max(0f, resolvedValue);
                return true;
            case "dotTickInterval":
                effectConfig.DotTickInterval = math.max(0.01f, resolvedValue);
                return true;
            case "dotDurationSeconds":
                effectConfig.DotDurationSeconds = math.max(0.05f, resolvedValue);
                return true;
            case "impedimentSlowPercentPerStack":
                effectConfig.ImpedimentSlowPercentPerStack = math.clamp(resolvedValue, 0f, 100f);
                return true;
            case "impedimentProcSlowPercent":
                effectConfig.ImpedimentProcSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return true;
            case "impedimentMaxSlowPercent":
                effectConfig.ImpedimentMaxSlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return true;
            case "impedimentDurationSeconds":
                effectConfig.ImpedimentDurationSeconds = math.max(0.05f, resolvedValue);
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies one boolean Add Scaling value to a shared elemental effect payload.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="prefix">Path prefix that identifies the owning effect payload namespace.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="effectConfig">Mutable elemental effect config being rebuilt from immutable baseline data.</param>
    /// <returns>True when the path matched an elemental effect boolean field and was applied.</returns>
    private static bool TryApplyElementalEffectBooleanValue(string payloadPath,
                                                            string prefix,
                                                            bool resolvedValue,
                                                            ref ElementalEffectConfig effectConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath) || string.IsNullOrWhiteSpace(prefix))
            return false;

        if (!payloadPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string effectFieldPath = payloadPath.Substring(prefix.Length);

        switch (effectFieldPath)
        {
            case "consumeStacksOnProc":
                effectConfig.ConsumeStacksOnProc = resolvedValue ? (byte)1 : (byte)0;
                return true;
        }

        return false;
    }

    /// <summary>
    /// Keeps elemental stack threshold and maximum values coherent after independent runtime scaling rewrites.
    /// </summary>
    /// <param name="effectConfig">Mutable elemental effect config receiving stack bounds normalization.</param>
    private static void NormalizeElementalEffectStackBounds(ref ElementalEffectConfig effectConfig)
    {
        effectConfig.ProcThresholdStacks = math.max(0.1f, effectConfig.ProcThresholdStacks);
        effectConfig.MaximumStacks = math.max(effectConfig.ProcThresholdStacks, math.max(0.1f, effectConfig.MaximumStacks));
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
        if (TryApplyLaserBeamBooleanValue(payloadPath,
                                          HoldChargeChargedLaserBeamPayloadPrefix,
                                          resolvedValue,
                                          ref activeSlotConfig.ChargeShot.ChargedLaserBeam))
            return;

        if (PlayerRuntimePowerUpBombScalingApplyUtility.TryApplyBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig.Bomb))
            return;

        if (PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig.ImpactFrame))
            return;

        if (PlayerRuntimePowerUpGhostTrailScalingApplyUtility.TryApplyBooleanValue(payloadPath, resolvedValue, ref activeSlotConfig.GhostTrail))
            return;

        switch (payloadPath)
        {
            case "resourceGate.isToggleable":
                activeSlotConfig.Toggleable = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "characterTuning.applyFormulasOnlyOnActiveTrigger":
                activeSlotConfig.ApplyCharacterTuningOnActiveTrigger = resolvedValue ? (byte)1 : (byte)0;
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
            case "holdCharge.ignoreInheritedPlayerVelocityX":
                activeSlotConfig.ChargeShot.IgnoreInheritedPlayerVelocityX = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "holdCharge.ignoreInheritedPlayerVelocityZ":
                activeSlotConfig.ChargeShot.IgnoreInheritedPlayerVelocityZ = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "projectilePatternCone.ignoreInheritedPlayerVelocityX":
                activeSlotConfig.Shotgun.IgnoreInheritedPlayerVelocityX = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "projectilePatternCone.ignoreInheritedPlayerVelocityZ":
                activeSlotConfig.Shotgun.IgnoreInheritedPlayerVelocityZ = resolvedValue ? (byte)1 : (byte)0;
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
        if (TryApplyLaserBeamBooleanValue(payloadPath,
                                          PassiveLaserBeamPayloadPrefix,
                                          resolvedValue,
                                          ref passiveToolConfig.LaserBeam))
            return;

        ElementalEffectConfig elementalAreaTickEffect = passiveToolConfig.ElementalTrail.Effect;

        if (TryApplyElementalEffectBooleanValue(payloadPath,
                                                ElementalAreaTickEffectPrefix,
                                                resolvedValue,
                                                ref elementalAreaTickEffect))
        {
            NormalizeElementalEffectStackBounds(ref elementalAreaTickEffect);
            passiveToolConfig.ElementalTrail.Effect = elementalAreaTickEffect;
            return;
        }

        if (TryApplyOrbitalProjectionBooleanValue(payloadPath, resolvedValue, ref passiveToolConfig))
            return;

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
            case "projectilePatternCone.ignoreInheritedPlayerVelocityX":
                passiveToolConfig.Shotgun.IgnoreInheritedPlayerVelocityX = resolvedValue ? (byte)1 : (byte)0;
                return;
            case "projectilePatternCone.ignoreInheritedPlayerVelocityZ":
                passiveToolConfig.Shotgun.IgnoreInheritedPlayerVelocityZ = resolvedValue ? (byte)1 : (byte)0;
                return;
        }
    }

    /// <summary>
    /// Applies one resolved numeric Add Scaling value to an orbital projection payload.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config containing projection entries.</param>
    /// <returns>True when the path matched an orbital projection field and was applied.</returns>
    private static bool TryApplyOrbitalProjectionValue(string payloadPath,
                                                       float resolvedValue,
                                                       ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath))
            return false;

        switch (payloadPath)
        {
            case "orbitalProjections.acquisitionPolicy":
                ApplyToAllOrbitalProjectionConfigs(payloadPath, resolvedValue, ref passiveToolConfig);
                return true;
            case "orbitalProjections.activeDurationSeconds":
                ApplyToAllOrbitalProjectionConfigs(payloadPath, resolvedValue, ref passiveToolConfig);
                return true;
            case "orbitalProjections.spawnAnimationSeconds":
                ApplyToAllOrbitalProjectionConfigs(payloadPath, resolvedValue, ref passiveToolConfig);
                return true;
            case "orbitalProjections.despawnAnimationSeconds":
                ApplyToAllOrbitalProjectionConfigs(payloadPath, resolvedValue, ref passiveToolConfig);
                return true;
        }

        if (!TryResolveOrbitalProjectionEntryPath(payloadPath, out int projectionIndex, out string fieldPath))
            return false;

        if (projectionIndex < 0 || projectionIndex >= passiveToolConfig.OrbitalProjections.Length)
            return true;

        OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[projectionIndex];

        switch (fieldPath)
        {
            case "motionMode":
                projectionConfig.MotionMode = PlayerRuntimeScalingEnumUtility.ResolveOrbitalProjectionMotionMode(resolvedValue);
                break;
            case "orbitDistance":
                projectionConfig.OrbitDistance = math.max(0f, resolvedValue);
                break;
            case "heightOffset":
                projectionConfig.HeightOffset = resolvedValue;
                break;
            case "angleOffsetDegrees":
                projectionConfig.AngleOffsetDegrees = resolvedValue;
                break;
            case "orbitSpeedDegreesPerSecond":
                projectionConfig.OrbitSpeedDegreesPerSecond = resolvedValue;
                break;
            case "orbitConeCenterAngleDegrees":
                projectionConfig.OrbitConeCenterAngleDegrees = resolvedValue;
                break;
            case "orbitConeAngleDegrees":
                projectionConfig.OrbitConeAngleDegrees = math.clamp(resolvedValue, 0f, 360f);
                break;
            case "fullOrbitConeResponse":
                projectionConfig.FullOrbitConeResponse = PlayerRuntimeScalingEnumUtility.ResolveOrbitalProjectionFullOrbitConeResponse(resolvedValue);
                break;
            case "lookFollowDelaySeconds":
                projectionConfig.LookFollowDelaySeconds = math.max(0f, resolvedValue);
                break;
            case "collisionRadius":
                projectionConfig.CollisionRadius = math.max(0.01f, resolvedValue);
                break;
            case "contactDamage":
                projectionConfig.ContactDamage = math.max(0f, resolvedValue);
                break;
            case "damageTickIntervalSeconds":
                projectionConfig.DamageTickIntervalSeconds = math.max(0.01f, resolvedValue);
                break;
            case "maximumHealth":
                projectionConfig.MaximumHealth = math.max(0f, resolvedValue);
                break;
            case "enemyContactHealthDamage":
                projectionConfig.EnemyContactHealthDamage = math.max(0f, resolvedValue);
                break;
            case "projectileBlockHealthDamage":
                projectionConfig.ProjectileBlockHealthDamage = math.max(0f, resolvedValue);
                break;
            case "bombBlockHealthDamage":
                projectionConfig.BombBlockHealthDamage = math.max(0f, resolvedValue);
                break;
            default:
                return false;
        }

        passiveToolConfig.OrbitalProjections[projectionIndex] = projectionConfig;
        passiveToolConfig.HasOrbitalProjections = passiveToolConfig.OrbitalProjections.Length > 0 ? (byte)1 : (byte)0;
        return true;
    }

    /// <summary>
    /// Applies one resolved token Add Scaling value to an orbital projection payload.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="resolvedValue">Formula token result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config containing projection entries.</param>
    /// <returns>True when the path matched an orbital projection token field and was applied.</returns>
    private static bool TryApplyOrbitalProjectionTokenValue(string payloadPath,
                                                           string resolvedValue,
                                                           ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (!TryResolveOrbitalProjectionEntryPath(payloadPath, out int projectionIndex, out string fieldPath))
            return false;

        if (!string.Equals(fieldPath, "categoryId", StringComparison.Ordinal))
            return false;

        if (projectionIndex < 0 || projectionIndex >= passiveToolConfig.OrbitalProjections.Length)
            return true;

        if (!TryBuildFixedString64(resolvedValue, out FixedString64Bytes categoryId))
            return true;

        OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[projectionIndex];
        projectionConfig.CategoryId = categoryId;
        passiveToolConfig.OrbitalProjections[projectionIndex] = projectionConfig;
        passiveToolConfig.HasOrbitalProjections = passiveToolConfig.OrbitalProjections.Length > 0 ? (byte)1 : (byte)0;
        return true;
    }

    /// <summary>
    /// Applies one resolved boolean Add Scaling value to an orbital projection payload.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config containing projection entries.</param>
    /// <returns>True when the path matched an orbital projection boolean field and was applied.</returns>
    private static bool TryApplyOrbitalProjectionBooleanValue(string payloadPath,
                                                              bool resolvedValue,
                                                              ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (string.Equals(payloadPath, "orbitalProjections.useCategoryIdAsExclusionFilter", StringComparison.Ordinal))
        {
            ApplyCategoryFilterFlagToAllOrbitalProjectionConfigs(resolvedValue, ref passiveToolConfig);
            return true;
        }

        if (!TryResolveOrbitalProjectionEntryPath(payloadPath, out int projectionIndex, out string fieldPath))
            return false;

        if (projectionIndex < 0 || projectionIndex >= passiveToolConfig.OrbitalProjections.Length)
            return true;

        OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[projectionIndex];
        byte value = resolvedValue ? (byte)1 : (byte)0;

        switch (fieldPath)
        {
            case "damageEnemies":
                projectionConfig.DamageEnemies = value;
                break;
            case "bounceInsideOrbitCone":
                projectionConfig.BounceInsideOrbitCone = value;
                break;
            case "blockEnemyProjectiles":
                projectionConfig.BlockEnemyProjectiles = value;
                break;
            case "blockEnemyBombs":
                projectionConfig.BlockEnemyBombs = value;
                break;
            case "adaptCollisionToModel":
                projectionConfig.AdaptCollisionToModel = value;
                break;
            case "faceOrbitOutward":
                projectionConfig.FaceOrbitOutward = value;
                break;
            case "hasHealth":
                projectionConfig.HasHealth = value;
                break;
            default:
                return false;
        }

        passiveToolConfig.OrbitalProjections[projectionIndex] = projectionConfig;
        passiveToolConfig.HasOrbitalProjections = passiveToolConfig.OrbitalProjections.Length > 0 ? (byte)1 : (byte)0;
        return true;
    }

    /// <summary>
    /// Applies the module-level category filter flag to every baked projection config.
    /// </summary>
    /// <param name="resolvedValue">Formula-resolved filter flag.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config containing projection entries.</param>
    private static void ApplyCategoryFilterFlagToAllOrbitalProjectionConfigs(bool resolvedValue,
                                                                             ref PlayerPassiveToolConfig passiveToolConfig)
    {
        byte filterValue = resolvedValue ? (byte)1 : (byte)0;

        for (int configIndex = 0; configIndex < passiveToolConfig.OrbitalProjections.Length; configIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[configIndex];
            projectionConfig.UseCategoryIdAsExclusionFilter = filterValue;
            passiveToolConfig.OrbitalProjections[configIndex] = projectionConfig;
        }

        passiveToolConfig.HasOrbitalProjections = passiveToolConfig.OrbitalProjections.Length > 0 ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Applies one module-level value to every projection config in a passive payload.
    /// </summary>
    /// <param name="payloadPath">Module-level payload path that should affect each projection entry.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="passiveToolConfig">Mutable passive tool config containing projection entries.</param>
    private static void ApplyToAllOrbitalProjectionConfigs(string payloadPath,
                                                           float resolvedValue,
                                                           ref PlayerPassiveToolConfig passiveToolConfig)
    {
        for (int configIndex = 0; configIndex < passiveToolConfig.OrbitalProjections.Length; configIndex++)
        {
            OrbitalProjectionConfig projectionConfig = passiveToolConfig.OrbitalProjections[configIndex];

            switch (payloadPath)
            {
                case "orbitalProjections.acquisitionPolicy":
                    projectionConfig.AcquisitionPolicy = PlayerRuntimeScalingEnumUtility.ResolveOrbitalProjectionAcquisitionPolicy(resolvedValue);
                    break;
                case "orbitalProjections.activeDurationSeconds":
                    projectionConfig.ActiveDurationSeconds = math.max(0f, resolvedValue);
                    break;
                case "orbitalProjections.spawnAnimationSeconds":
                    projectionConfig.SpawnAnimationSeconds = math.max(0f, resolvedValue);
                    break;
                case "orbitalProjections.despawnAnimationSeconds":
                    projectionConfig.DespawnAnimationSeconds = math.max(0f, resolvedValue);
                    break;
            }

            passiveToolConfig.OrbitalProjections[configIndex] = projectionConfig;
        }

        passiveToolConfig.HasOrbitalProjections = passiveToolConfig.OrbitalProjections.Length > 0 ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Resolves a serialized list entry path for one orbital projection field.
    /// </summary>
    /// <param name="payloadPath">Full modular payload path carried by the scaling rule.</param>
    /// <param name="projectionIndex">Resolved projection list index.</param>
    /// <param name="fieldPath">Resolved field path inside the projection entry.</param>
    /// <returns>True when the path points to an orbital projection list entry.</returns>
    private static bool TryResolveOrbitalProjectionEntryPath(string payloadPath,
                                                            out int projectionIndex,
                                                            out string fieldPath)
    {
        projectionIndex = -1;
        fieldPath = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadPath))
            return false;

        if (!payloadPath.StartsWith(OrbitalProjectionEntryPrefix, StringComparison.Ordinal))
            return false;

        int indexStart = OrbitalProjectionEntryPrefix.Length;
        int indexEnd = payloadPath.IndexOf("].", indexStart, StringComparison.Ordinal);

        if (indexEnd < 0)
            return false;

        string indexText = payloadPath.Substring(indexStart, indexEnd - indexStart);

        if (!int.TryParse(indexText, out projectionIndex))
            return false;

        fieldPath = payloadPath.Substring(indexEnd + 2);
        return !string.IsNullOrWhiteSpace(fieldPath);
    }

    /// <summary>
    /// Converts one managed token into the FixedString64Bytes storage used by runtime power-up configs.
    /// </summary>
    /// <param name="tokenValue">Formula-resolved token text.</param>
    /// <param name="fixedToken">Fixed token result when the value fits in runtime storage.</param>
    /// <returns>True when the token can be represented safely in FixedString64Bytes.</returns>
    private static bool TryBuildFixedString64(string tokenValue,
                                              out FixedString64Bytes fixedToken)
    {
        fixedToken = default;
        string trimmedToken = string.IsNullOrWhiteSpace(tokenValue)
            ? string.Empty
            : tokenValue.Trim();

        if (Encoding.UTF8.GetByteCount(trimmedToken) > MaximumFixedString64Utf8Bytes)
            return false;

        fixedToken = new FixedString64Bytes(trimmedToken);
        return true;
    }
    #endregion

    #endregion
}
