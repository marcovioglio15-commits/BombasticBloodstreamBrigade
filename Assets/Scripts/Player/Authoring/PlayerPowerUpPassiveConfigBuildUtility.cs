using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds passive power-up config fragments and trigger-mode resolutions shared by passive bake paths.
/// </summary>
public static class PlayerPowerUpPassiveConfigBuildUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether a passive config contains at least one runtime payload block.
    /// </summary>
    /// <param name="config">Passive config inspected by reference to avoid copying orbital projection payloads.</param>
    /// <returns>True when any passive payload flag is enabled.</returns>
    public static bool HasAnyPayload(in PlayerPassiveToolConfig config)
    {
        return config.HasProjectileSize != 0 ||
               config.HasShotgun != 0 ||
               config.HasElementalProjectiles != 0 ||
               config.HasPerfectCircle != 0 ||
               config.HasBouncingProjectiles != 0 ||
               config.HasSplittingProjectiles != 0 ||
               config.HasExplosion != 0 ||
               config.HasElementalTrail != 0 ||
               config.HasHeal != 0 ||
               config.HasBulletTime != 0 ||
               config.HasLaserBeam != 0 ||
               config.HasOrbitalProjections != 0 ||
               config.HasWeaponSwitch != 0;
    }

    /// <summary>
    /// Resolves the representative passive kind used by systems that need a single category for a multi-payload config.
    /// </summary>
    /// <param name="config">Passive config inspected by reference to avoid copying orbital projection payloads.</param>
    /// <returns>Representative passive tool kind for the authored payload mix.</returns>
    public static PassiveToolKind ResolvePassiveToolKind(in PlayerPassiveToolConfig config)
    {
        if (config.HasElementalTrail != 0)
            return PassiveToolKind.ElementalTrail;

        if (config.HasExplosion != 0)
            return PassiveToolKind.Explosion;

        if (config.HasPerfectCircle != 0)
            return PassiveToolKind.PerfectCircle;

        if (config.HasBouncingProjectiles != 0)
            return PassiveToolKind.BouncingProjectiles;

        if (config.HasSplittingProjectiles != 0)
            return PassiveToolKind.SplittingProjectiles;

        if (config.HasElementalProjectiles != 0)
            return PassiveToolKind.ElementalProjectiles;

        if (config.HasBulletTime != 0)
            return PassiveToolKind.BulletTime;

        if (config.HasLaserBeam != 0)
            return PassiveToolKind.LaserBeam;

        if (config.HasOrbitalProjections != 0)
            return PassiveToolKind.Custom;

        if (config.HasHeal != 0)
            return PassiveToolKind.Custom;

        if (config.HasWeaponSwitch != 0)
            return PassiveToolKind.Custom;

        return PassiveToolKind.ProjectileSize;
    }

    public static PassiveExplosionTriggerMode ResolveExplosionTriggerMode(bool hasTriggerEvent, PowerUpTriggerEventType triggerEventType, PassiveExplosionTriggerMode fallback)
    {
        if (!hasTriggerEvent)
            return fallback;

        switch (triggerEventType)
        {
            case PowerUpTriggerEventType.OnEnemyKilled:
                return PassiveExplosionTriggerMode.OnEnemyKilled;
            case PowerUpTriggerEventType.OnPlayerDamaged:
                return PassiveExplosionTriggerMode.OnPlayerDamaged;
            default:
                return fallback;
        }
    }

    public static ProjectileSplitTriggerMode ResolveSplitTriggerMode(bool hasTriggerEvent, PowerUpTriggerEventType triggerEventType, ProjectileSplitTriggerMode fallback)
    {
        if (!hasTriggerEvent)
            return fallback;

        switch (triggerEventType)
        {
            case PowerUpTriggerEventType.OnEnemyKilled:
                return ProjectileSplitTriggerMode.OnEnemyKilled;
            case PowerUpTriggerEventType.OnProjectileDespawned:
                return ProjectileSplitTriggerMode.OnProjectileDespawn;
            default:
                return fallback;
        }
    }

    public static PassiveHealTriggerMode ResolvePassiveHealTriggerMode(bool hasTriggerEvent, PowerUpTriggerEventType triggerEventType, PassiveHealTriggerMode fallback)
    {
        if (!hasTriggerEvent)
            return fallback;

        switch (triggerEventType)
        {
            case PowerUpTriggerEventType.OnEnemyKilled:
                return PassiveHealTriggerMode.OnEnemyKilled;
            case PowerUpTriggerEventType.OnPlayerDamaged:
                return PassiveHealTriggerMode.OnPlayerDamaged;
            default:
                return PassiveHealTriggerMode.Periodic;
        }
    }

    public static PassiveBulletTimeTriggerMode ResolvePassiveBulletTimeTriggerMode(bool hasTriggerEvent,
                                                                                   PowerUpTriggerEventType triggerEventType,
                                                                                   PassiveBulletTimeTriggerMode fallback)
    {
        if (!hasTriggerEvent)
            return fallback;

        switch (triggerEventType)
        {
            case PowerUpTriggerEventType.OnEnemyKilled:
                return PassiveBulletTimeTriggerMode.OnEnemyKilled;
            case PowerUpTriggerEventType.OnPlayerDamaged:
                return PassiveBulletTimeTriggerMode.OnPlayerDamaged;
            default:
                return PassiveBulletTimeTriggerMode.Periodic;
        }
    }

    public static float ResolvePassiveHealDurationSeconds(PowerUpHealApplicationMode applyMode, float healAmount, float durationSeconds)
    {
        if (applyMode == PowerUpHealApplicationMode.OverTime)
            return math.max(0.05f, durationSeconds);

        float fallbackDurationSeconds = 0.5f + math.max(0f, healAmount) * 0.01f;
        return math.clamp(fallbackDurationSeconds, 0.5f, 4f);
    }

    public static ProjectileSizePassiveConfig BuildProjectileSizePassiveConfig(ProjectileSizePassiveToolData projectileSizeData)
    {
        return new ProjectileSizePassiveConfig
        {
            SizeMultiplier = projectileSizeData != null ? math.max(0.01f, projectileSizeData.ProjectileSizeMultiplier) : 1f,
            DamageMultiplier = projectileSizeData != null ? math.max(0f, projectileSizeData.DamageMultiplier) : 1f,
            SpeedMultiplier = projectileSizeData != null ? math.max(0f, projectileSizeData.SpeedMultiplier) : 1f,
            LifetimeSecondsMultiplier = projectileSizeData != null ? math.max(0f, projectileSizeData.LifetimeSecondsMultiplier) : 1f,
            LifetimeRangeMultiplier = projectileSizeData != null ? math.max(0f, projectileSizeData.LifetimeRangeMultiplier) : 1f
        };
    }

    public static ElementalProjectilesPassiveConfig BuildElementalProjectilesPassiveConfig(ElementalProjectilesPassiveToolData elementalProjectilesData)
    {
        return new ElementalProjectilesPassiveConfig
        {
            Effect = PlayerPowerUpBakeSharedUtility.BuildElementalEffectConfig(elementalProjectilesData != null ? elementalProjectilesData.EffectData : null),
            StacksPerHit = elementalProjectilesData != null ? math.max(0f, elementalProjectilesData.StacksPerHit) : 0f
        };
    }

    public static PerfectCirclePassiveConfig BuildPerfectCirclePassiveConfig(PerfectCirclePassiveToolData perfectCircleData)
    {
        return new PerfectCirclePassiveConfig
        {
            PathMode = perfectCircleData != null ? perfectCircleData.PathMode : ProjectileOrbitPathMode.Circle,
            RadialEntrySpeed = perfectCircleData != null ? math.max(0f, perfectCircleData.RadialEntrySpeed) : 0f,
            OrbitalSpeed = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitalSpeed) : 0f,
            OrbitRadiusMin = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitRadiusMin) : 0f,
            OrbitRadiusMax = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitRadiusMax) : 0f,
            OrbitPulseFrequency = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitPulseFrequency) : 0f,
            OrbitEntryRatio = perfectCircleData != null ? math.clamp(perfectCircleData.OrbitEntryRatio, 0f, 1f) : 0f,
            OrbitBlendDuration = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitBlendDuration) : 0f,
            HeightOffset = perfectCircleData != null ? perfectCircleData.HeightOffset : 0f,
            GoldenAngleDegrees = perfectCircleData != null ? math.max(0f, perfectCircleData.GoldenAngleDegrees) : 137.5f,
            SpiralStartRadius = perfectCircleData != null ? math.max(0f, perfectCircleData.SpiralStartRadius) : 0f,
            SpiralMaximumRadius = perfectCircleData != null ? math.max(0f, perfectCircleData.SpiralMaximumRadius) : 0f,
            SpiralAngularSpeedDegreesPerSecond = perfectCircleData != null ? math.max(0f, perfectCircleData.SpiralAngularSpeedDegreesPerSecond) : 0f,
            SpiralGrowthMultiplier = perfectCircleData != null ? math.max(0f, perfectCircleData.SpiralGrowthMultiplier) : 1f,
            SpiralTurnsBeforeDespawn = perfectCircleData != null ? math.max(0.1f, perfectCircleData.SpiralTurnsBeforeDespawn) : 1f,
            SpiralClockwise = perfectCircleData != null && perfectCircleData.SpiralClockwise ? (byte)1 : (byte)0
        };
    }

    public static void AccumulatePerfectCirclePassiveConfig(ref PerfectCirclePassiveConfig targetConfig,
                                                            in PerfectCirclePassiveConfig candidateConfig,
                                                            ref bool hasTargetConfig)
    {
        if (!hasTargetConfig)
        {
            hasTargetConfig = true;
            targetConfig = candidateConfig;
            return;
        }

        if (candidateConfig.PathMode == ProjectileOrbitPathMode.GoldenSpiral)
            targetConfig.PathMode = ProjectileOrbitPathMode.GoldenSpiral;

        targetConfig.RadialEntrySpeed = math.max(targetConfig.RadialEntrySpeed, candidateConfig.RadialEntrySpeed);
        targetConfig.HeightOffset = math.max(targetConfig.HeightOffset, candidateConfig.HeightOffset);
        targetConfig.GoldenAngleDegrees = math.max(targetConfig.GoldenAngleDegrees, candidateConfig.GoldenAngleDegrees);

        switch (targetConfig.PathMode)
        {
            case ProjectileOrbitPathMode.GoldenSpiral:
                targetConfig.SpiralStartRadius = math.max(targetConfig.SpiralStartRadius, candidateConfig.SpiralStartRadius);
                targetConfig.SpiralMaximumRadius = math.max(targetConfig.SpiralMaximumRadius, candidateConfig.SpiralMaximumRadius);
                targetConfig.SpiralAngularSpeedDegreesPerSecond = math.max(targetConfig.SpiralAngularSpeedDegreesPerSecond,
                                                                           candidateConfig.SpiralAngularSpeedDegreesPerSecond);
                targetConfig.SpiralGrowthMultiplier = math.max(targetConfig.SpiralGrowthMultiplier, candidateConfig.SpiralGrowthMultiplier);
                targetConfig.SpiralTurnsBeforeDespawn = math.max(targetConfig.SpiralTurnsBeforeDespawn, candidateConfig.SpiralTurnsBeforeDespawn);
                targetConfig.SpiralClockwise = targetConfig.SpiralClockwise != 0 || candidateConfig.SpiralClockwise != 0 ? (byte)1 : (byte)0;
                return;
            default:
                targetConfig.OrbitalSpeed = math.max(targetConfig.OrbitalSpeed, candidateConfig.OrbitalSpeed);
                targetConfig.OrbitRadiusMin = math.max(targetConfig.OrbitRadiusMin, candidateConfig.OrbitRadiusMin);
                targetConfig.OrbitRadiusMax = math.max(targetConfig.OrbitRadiusMax, candidateConfig.OrbitRadiusMax);
                targetConfig.OrbitPulseFrequency = math.max(targetConfig.OrbitPulseFrequency, candidateConfig.OrbitPulseFrequency);
                targetConfig.OrbitEntryRatio = math.max(targetConfig.OrbitEntryRatio, candidateConfig.OrbitEntryRatio);
                targetConfig.OrbitBlendDuration = math.max(targetConfig.OrbitBlendDuration, candidateConfig.OrbitBlendDuration);
                return;
        }
    }

    public static BouncingProjectilesPassiveConfig BuildBouncingProjectilesPassiveConfig(BouncingProjectilesPassiveToolData bouncingProjectilesData)
    {
        return new BouncingProjectilesPassiveConfig
        {
            MaxBounces = bouncingProjectilesData != null ? math.max(0, bouncingProjectilesData.MaxBounces) : 0,
            SpeedPercentChangePerBounce = bouncingProjectilesData != null ? bouncingProjectilesData.SpeedPercentChangePerBounce : 0f,
            MinimumSpeedMultiplierAfterBounce = bouncingProjectilesData != null ? math.max(0f, bouncingProjectilesData.MinimumSpeedMultiplierAfterBounce) : 0f,
            MaximumSpeedMultiplierAfterBounce = bouncingProjectilesData != null ? math.max(0f, bouncingProjectilesData.MaximumSpeedMultiplierAfterBounce) : 0f
        };
    }

    public static LaserBeamPassiveConfig BuildLaserBeamPassiveConfig(PowerUpLaserBeamModuleData laserBeamData)
    {
        if (laserBeamData == null)
            return default;

        return new LaserBeamPassiveConfig
        {
            DamageMultiplier = math.max(0f, laserBeamData.DamageMultiplier),
            ContinuousDamagePerSecondMultiplier = math.max(0f, laserBeamData.ContinuousDamagePerSecondMultiplier),
            VirtualProjectileSpeedMultiplier = math.max(0f, laserBeamData.VirtualProjectileSpeedMultiplier),
            DamageTickIntervalSeconds = math.max(0.0001f, laserBeamData.DamageTickIntervalSeconds),
            MaximumContinuousActiveSeconds = math.max(0f, laserBeamData.MaximumContinuousActiveSeconds),
            CooldownSeconds = math.max(0f, laserBeamData.CooldownSeconds),
            MaximumBounceSegments = math.max(0, laserBeamData.MaximumBounceSegments),
            ApplyPlayerHandlingNerfWhileFiring = laserBeamData.ApplyPlayerHandlingNerfWhileFiring ? (byte)1 : (byte)0,
            FiringMoveSpeedMultiplier = math.max(0f, laserBeamData.FiringMoveSpeedMultiplier),
            FiringRotationSpeedMultiplier = math.max(0f, laserBeamData.FiringRotationSpeedMultiplier),
            VisualPresetId = math.max(0, laserBeamData.VisualPresetId),
            BodyProfile = laserBeamData.BodyProfile,
            SourceShape = laserBeamData.SourceShape,
            TerminalCapShape = laserBeamData.TerminalCapShape,
            BodyWidthMultiplier = math.max(0.01f, laserBeamData.BodyWidthMultiplier),
            CollisionWidthMultiplier = math.max(0.01f, laserBeamData.CollisionWidthMultiplier),
            SourceScaleMultiplier = math.max(0.01f, laserBeamData.SourceScaleMultiplier),
            TerminalCapScaleMultiplier = math.max(0.01f, laserBeamData.TerminalCapScaleMultiplier),
            ContactFlareScaleMultiplier = math.max(0.01f, laserBeamData.ContactFlareScaleMultiplier),
            BodyOpacity = math.max(0.01f, laserBeamData.BodyOpacity),
            CoreWidthMultiplier = math.max(0.05f, laserBeamData.CoreWidthMultiplier),
            CoreBrightness = math.max(0f, laserBeamData.CoreBrightness),
            RimBrightness = math.max(0f, laserBeamData.RimBrightness),
            FlowScrollSpeed = math.max(0f, laserBeamData.FlowScrollSpeed),
            FlowPulseFrequency = math.max(0f, laserBeamData.FlowPulseFrequency),
            StormTwistSpeed = math.max(0f, laserBeamData.StormTwistSpeed),
            StormTickPostTravelHoldSeconds = math.max(0f, laserBeamData.StormTickPostTravelHoldSeconds),
            StormIdleIntensity = math.max(0f, laserBeamData.StormIdleIntensity),
            StormBurstIntensity = math.max(0f, laserBeamData.StormBurstIntensity),
            SourceOffset = math.max(0f, laserBeamData.SourceOffset),
            SourceDischargeIntensity = math.max(0f, laserBeamData.SourceDischargeIntensity),
            StormShellWidthMultiplier = math.max(0.01f, laserBeamData.StormShellWidthMultiplier),
            StormShellSeparation = math.max(0f, laserBeamData.StormShellSeparation),
            StormRingFrequency = math.max(0f, laserBeamData.StormRingFrequency),
            StormRingThickness = math.max(0.01f, laserBeamData.StormRingThickness),
            StormTickTravelSpeed = math.max(0f, laserBeamData.StormTickTravelSpeed),
            StormTickDamageLengthTolerance = math.max(0f, laserBeamData.StormTickDamageLengthTolerance),
            TerminalCapIntensity = math.max(0f, laserBeamData.TerminalCapIntensity),
            ContactFlareIntensity = math.max(0f, laserBeamData.ContactFlareIntensity),
            WobbleAmplitude = math.max(0f, laserBeamData.WobbleAmplitude),
            BubbleDriftSpeed = math.max(0f, laserBeamData.BubbleDriftSpeed)
        };
    }

    public static SplittingProjectilesPassiveConfig BuildSplittingProjectilesPassiveConfig(SplittingProjectilesPassiveToolData splittingProjectilesData)
    {
        SplittingProjectilesPassiveConfig config = default;

        if (splittingProjectilesData == null)
            return config;

        FixedList128Bytes<float> customAngles = default;
        IReadOnlyList<float> customAnglesSource = splittingProjectilesData.CustomAnglesDegrees;

        if (customAnglesSource != null)
        {
            for (int index = 0; index < customAnglesSource.Count; index++)
            {
                if (customAngles.Length >= customAngles.Capacity)
                    break;

                customAngles.Add(customAnglesSource[index]);
            }
        }

        config.DirectionMode = splittingProjectilesData.DirectionMode;
        config.TriggerMode = splittingProjectilesData.TriggerMode;
        config.SplitProjectileCount = math.max(1, splittingProjectilesData.SplitProjectileCount);
        config.SplitOffsetDegrees = splittingProjectilesData.SplitOffsetDegrees;
        config.CustomAnglesDegrees = customAngles;
        config.SplitDamageMultiplier = math.max(0f, splittingProjectilesData.SplitDamagePercentFromOriginal * 0.01f);
        config.SplitSizeMultiplier = math.max(0f, splittingProjectilesData.SplitSizePercentFromOriginal * 0.01f);
        config.SplitSpeedMultiplier = math.max(0f, splittingProjectilesData.SplitSpeedPercentFromOriginal * 0.01f);
        config.SplitLifetimeMultiplier = math.max(0f, splittingProjectilesData.SplitLifetimePercentFromOriginal * 0.01f);
        return config;
    }

    public static ExplosionPassiveConfig BuildExplosionPassiveConfig(PlayerAuthoring authoring,
                                                                     ExplosionPassiveToolData explosionData,
                                                                     Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (explosionData == null)
            return default;

        Entity explosionVfxPrefabEntity = Entity.Null;

        if (authoring != null && authoring.BakePowerUpVfxEntityPrefabs)
            explosionVfxPrefabEntity = PlayerPowerUpBakeSharedUtility.ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                                                         explosionData.ExplosionVfxPrefab,
                                                                                                         "Passive Explosion VFX",
                                                                                                         resolveDynamicPrefabEntity);
#if UNITY_EDITOR
        else if (authoring != null && explosionData.ExplosionVfxPrefab != null)
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Passive explosion VFX prefab is assigned on '{0}', but BakePowerUpVfxEntityPrefabs is disabled. Death-explosion VFX will not spawn at runtime.",
                                           authoring.name),
                             authoring);
#endif

        return new ExplosionPassiveConfig
        {
            TriggerMode = explosionData.TriggerMode,
            CooldownSeconds = math.max(0f, explosionData.CooldownSeconds),
            Radius = math.max(0f, explosionData.Radius),
            Damage = math.max(0f, explosionData.Damage),
            AffectAllEnemiesInRadius = explosionData.AffectAllEnemiesInRadius ? (byte)1 : (byte)0,
            TriggerOffset = new float3(explosionData.TriggerOffset.x, explosionData.TriggerOffset.y, explosionData.TriggerOffset.z),
            ExplosionVfxPrefabEntity = explosionVfxPrefabEntity,
            ScaleVfxToRadius = explosionData.ScaleVfxToRadius ? (byte)1 : (byte)0,
            VfxScaleMultiplier = math.max(0.01f, explosionData.VfxScaleMultiplier)
        };
    }

    public static ElementalTrailPassiveConfig BuildElementalTrailPassiveConfig(PlayerAuthoring authoring, ElementalTrailPassiveToolData elementalTrailData)
    {
        if (elementalTrailData == null)
            return default;

        return new ElementalTrailPassiveConfig
        {
            Effect = PlayerPowerUpBakeSharedUtility.BuildElementalEffectConfig(elementalTrailData.EffectData),
            TrailSegmentLifetimeSeconds = math.max(0.05f, elementalTrailData.TrailSegmentLifetimeSeconds),
            TrailSpawnDistance = math.max(0f, elementalTrailData.TrailSpawnDistance),
            TrailSpawnIntervalSeconds = math.max(0.01f, elementalTrailData.TrailSpawnIntervalSeconds),
            TrailRadius = math.max(0f, elementalTrailData.TrailRadius),
            MaxActiveSegmentsPerPlayer = math.max(1, elementalTrailData.MaxActiveSegmentsPerPlayer),
            StacksPerTick = math.max(0f, elementalTrailData.StacksPerTick),
            ApplyIntervalSeconds = math.max(0.01f, elementalTrailData.ApplyIntervalSeconds),
            TrailAttachedVfxPrefabEntity = Entity.Null,
            TrailAttachedVfxScaleMultiplier = authoring != null ? math.max(0.01f, authoring.ElementalTrailAttachedVfxScaleMultiplier) : 1f,
            TrailAttachedVfxOffset = new float3(elementalTrailData.TrailAttachedVfxOffset.x,
                                                elementalTrailData.TrailAttachedVfxOffset.y,
                                                elementalTrailData.TrailAttachedVfxOffset.z)
        };
    }
    #endregion

    #endregion
}
