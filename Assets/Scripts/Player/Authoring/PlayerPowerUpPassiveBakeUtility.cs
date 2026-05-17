using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles passive power-up loadouts from modular definitions and legacy passive tools.
/// </summary>
public static class PlayerPowerUpPassiveBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves equipped passive entries from the preset into runtime configs.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="outputPassiveToolConfigs">Destination list receiving compiled passive configs.</param>
    public static void CollectEquippedPassiveToolConfigs(PlayerAuthoring authoring,
                                                         PlayerPowerUpsPreset preset,
                                                         Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                         List<PlayerPassiveToolConfig> outputPassiveToolConfigs,
                                                         List<FixedString64Bytes> outputPowerUpIds = null)
    {
        if (preset == null || outputPassiveToolConfigs == null)
            return;

        outputPassiveToolConfigs.Clear();

        if (outputPowerUpIds != null)
            outputPowerUpIds.Clear();

        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = preset.PassivePowerUps;

        if (passivePowerUps != null && passivePowerUps.Count > 0)
        {
            IReadOnlyList<string> equippedPassivePowerUpIds = preset.EquippedPassivePowerUpIds;

            if (equippedPassivePowerUpIds == null || equippedPassivePowerUpIds.Count == 0)
                return;

            HashSet<string> visitedPassivePowerUpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < equippedPassivePowerUpIds.Count; index++)
            {
                string passivePowerUpId = equippedPassivePowerUpIds[index];

                if (string.IsNullOrWhiteSpace(passivePowerUpId))
                    continue;

                if (!visitedPassivePowerUpIds.Add(passivePowerUpId))
                    continue;

                ModularPowerUpDefinition passivePowerUp = PlayerPowerUpBakeSharedUtility.ResolveLoadoutPassivePowerUp(preset, passivePowerUpId);

                if (passivePowerUp == null)
                    continue;

                PlayerPassiveToolConfig passiveToolConfig = BuildPassiveToolConfigFromModularPowerUp(authoring,
                                                                                                     preset,
                                                                                                     passivePowerUp,
                                                                                                     resolveDynamicPrefabEntity);

                if (passiveToolConfig.IsDefined == 0)
                    continue;

                outputPassiveToolConfigs.Add(passiveToolConfig);

                if (outputPowerUpIds != null)
                    outputPowerUpIds.Add(new FixedString64Bytes(passivePowerUp.CommonData.PowerUpId.Trim()));
            }

            return;
        }

        IReadOnlyList<string> equippedPassiveToolIds = preset.EquippedPassiveToolIds;

        if (equippedPassiveToolIds == null || equippedPassiveToolIds.Count == 0)
            return;

        HashSet<string> visitedPassiveToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < equippedPassiveToolIds.Count; index++)
        {
            string passiveToolId = equippedPassiveToolIds[index];

            if (string.IsNullOrWhiteSpace(passiveToolId))
                continue;

            if (!visitedPassiveToolIds.Add(passiveToolId))
                continue;

            PassiveToolDefinition passiveTool = PlayerPowerUpBakeSharedUtility.ResolveLoadoutPassiveTool(preset, passiveToolId);

            if (passiveTool == null)
                continue;

            PlayerPassiveToolConfig passiveToolConfig = BuildPassiveToolConfig(authoring, passiveTool, resolveDynamicPrefabEntity);

            if (passiveToolConfig.IsDefined == 0)
                continue;

            outputPassiveToolConfigs.Add(passiveToolConfig);

            if (outputPowerUpIds != null)
                outputPowerUpIds.Add(default);
        }
    }

    /// <summary>
    /// Compiles one modular passive power-up into a runtime passive config.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <param name="powerUp">Modular passive power-up definition.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Runtime passive config or default.</returns>
    public static PlayerPassiveToolConfig BuildPassiveToolConfigFromModularPowerUp(PlayerAuthoring authoring,
                                                                                   PlayerPowerUpsPreset preset,
                                                                                   ModularPowerUpDefinition powerUp,
                                                                                   Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (powerUp == null)
            return default;

        bool hasTriggerEvent = false;
        PowerUpTriggerEventType triggerEventType = PowerUpTriggerEventType.OnEnemyKilled;
        bool hasSharedCooldown = false;
        float sharedCooldownSeconds = 0f;
        bool hasTrailSpawn = false;
        bool hasAreaTick = false;
        float trailSegmentLifetimeSeconds = 1.5f;
        float trailSpawnDistance = 0.6f;
        float trailSpawnIntervalSeconds = 0.1f;
        float trailRadius = 1.2f;
        int maxTrailSegments = 30;
        float3 trailAttachedVfxOffset = new float3(0f, 0.08f, 0f);
        ElementalEffectConfig trailEffect = default;
        float trailStacksPerTick = 0f;
        float trailApplyIntervalSeconds = 0.2f;
        bool hasExplosion = false;
        ExplosionPassiveConfig explosionConfig = default;
        bool hasOrbit = false;
        PerfectCirclePassiveConfig orbitConfig = default;
        bool hasBounce = false;
        BouncingProjectilesPassiveConfig bounceConfig = default;
        bool hasSplit = false;
        SplittingProjectilesPassiveConfig splitConfig = default;
        bool hasShotgunPattern = false;
        float projectileSizeMultiplier = 1f;
        float projectileDamageMultiplier = 1f;
        float projectileSpeedMultiplier = 1f;
        float projectileRangeMultiplier = 1f;
        float projectileLifetimeMultiplier = 1f;
        ProjectilePenetrationMode projectilePenetrationMode = ProjectilePenetrationMode.None;
        int projectileMaxPenetrations = 0;
        int shotgunProjectileCount = 0;
        float shotgunConeAngleDegrees = 0f;
        bool hasElementalProjectiles = false;
        ElementalEffectConfig elementalProjectilesEffect = default;
        float elementalProjectilesStacksPerHit = 0f;
        bool hasHeal = false;
        float healAmount = 0f;
        float healDurationSeconds = 0.5f;
        float healTickIntervalSeconds = 0.2f;
        PowerUpHealStackPolicy healStackPolicy = PowerUpHealStackPolicy.Refresh;
        bool hasBulletTime = false;
        float bulletTimeDurationSeconds = 0.05f;
        float bulletTimeEnemySlowPercent = 0f;
        float bulletTimeTransitionTimeSeconds = 0f;
        bool hasLaserBeam = false;
        LaserBeamPassiveConfig laserBeamConfig = default;
        IReadOnlyList<PowerUpModuleBinding> moduleBindings = powerUp.ModuleBindings;

        if (moduleBindings == null || moduleBindings.Count == 0)
            return default;

        for (int index = 0; index < moduleBindings.Count; index++)
        {
            PowerUpModuleBinding binding = moduleBindings[index];

            if (binding == null || !binding.IsEnabled)
                continue;

            PowerUpModuleDefinition moduleDefinition = PlayerPowerUpBakeSharedUtility.ResolveModuleDefinitionById(preset, binding.ModuleId);

            if (moduleDefinition == null)
                continue;

            PowerUpModuleData payload = binding.ResolvePayload(moduleDefinition);

            if (payload == null)
                continue;

            switch (moduleDefinition.ModuleKind)
            {
                case PowerUpModuleKind.TriggerEvent:
                    hasTriggerEvent = true;
                    triggerEventType = payload.TriggerEvent.EventType;
                    break;
                case PowerUpModuleKind.GateResource:
                    float candidateCooldownSeconds = math.max(0f, payload.ResourceGate.CooldownSeconds);

                    if (candidateCooldownSeconds <= 0f)
                        break;

                    if (!hasSharedCooldown)
                    {
                        hasSharedCooldown = true;
                        sharedCooldownSeconds = candidateCooldownSeconds;
                    }
                    else
                    {
                        sharedCooldownSeconds = math.min(sharedCooldownSeconds, candidateCooldownSeconds);
                    }

                    break;
                case PowerUpModuleKind.SpawnTrailSegment:
                    PowerUpTrailSpawnModuleData trailSpawnData = payload.TrailSpawn;

                    if (trailSpawnData == null)
                        break;

                    hasTrailSpawn = true;
                    trailSegmentLifetimeSeconds = math.max(trailSegmentLifetimeSeconds, math.max(0.05f, trailSpawnData.TrailSegmentLifetimeSeconds));
                    trailSpawnDistance = math.max(trailSpawnDistance, math.max(0f, trailSpawnData.TrailSpawnDistance));
                    trailSpawnIntervalSeconds = math.min(trailSpawnIntervalSeconds, math.max(0.01f, trailSpawnData.TrailSpawnIntervalSeconds));
                    trailRadius = math.max(trailRadius, math.max(0f, trailSpawnData.TrailRadius));
                    maxTrailSegments = math.max(maxTrailSegments, math.max(1, trailSpawnData.MaxActiveSegmentsPerPlayer));
                    trailAttachedVfxOffset = new float3(trailSpawnData.TrailAttachedVfxOffset.x,
                                                        trailSpawnData.TrailAttachedVfxOffset.y,
                                                        trailSpawnData.TrailAttachedVfxOffset.z);
                    break;
                case PowerUpModuleKind.AreaTickApplyElement:
                    PowerUpElementalAreaTickModuleData areaTickData = payload.ElementalAreaTick;

                    if (areaTickData == null)
                        break;

                    hasAreaTick = true;
                    trailEffect = PlayerPowerUpBakeSharedUtility.BuildElementalEffectConfig(areaTickData.EffectData);
                    trailStacksPerTick += math.max(0f, areaTickData.StacksPerTick);
                    trailApplyIntervalSeconds = math.min(trailApplyIntervalSeconds, math.max(0.01f, areaTickData.ApplyIntervalSeconds));
                    break;
                case PowerUpModuleKind.DeathExplosion:
                    ExplosionPassiveConfig candidateExplosionConfig = PlayerPowerUpPassiveConfigBuildUtility.BuildExplosionPassiveConfig(authoring,
                                                                                                                                    payload.DeathExplosion,
                                                                                                                                    resolveDynamicPrefabEntity);

                    if (candidateExplosionConfig.Radius <= 0f && candidateExplosionConfig.Damage <= 0f)
                        break;

                    if (!hasExplosion)
                    {
                        hasExplosion = true;
                        explosionConfig = candidateExplosionConfig;
                        break;
                    }

                    explosionConfig.CooldownSeconds = math.min(explosionConfig.CooldownSeconds, candidateExplosionConfig.CooldownSeconds);
                    explosionConfig.Radius = math.max(explosionConfig.Radius, candidateExplosionConfig.Radius);
                    explosionConfig.Damage += candidateExplosionConfig.Damage;
                    explosionConfig.AffectAllEnemiesInRadius = explosionConfig.AffectAllEnemiesInRadius != 0 || candidateExplosionConfig.AffectAllEnemiesInRadius != 0 ? (byte)1 : (byte)0;

                    if (explosionConfig.ExplosionVfxPrefabEntity == Entity.Null && candidateExplosionConfig.ExplosionVfxPrefabEntity != Entity.Null)
                    {
                        explosionConfig.ExplosionVfxPrefabEntity = candidateExplosionConfig.ExplosionVfxPrefabEntity;
                        explosionConfig.ScaleVfxToRadius = candidateExplosionConfig.ScaleVfxToRadius;
                        explosionConfig.VfxScaleMultiplier = candidateExplosionConfig.VfxScaleMultiplier;
                    }

                    break;
                case PowerUpModuleKind.OrbitalProjectiles:
                    PerfectCirclePassiveConfig candidateOrbitConfig = PlayerPowerUpPassiveConfigBuildUtility.BuildPerfectCirclePassiveConfig(payload.ProjectileOrbitOverride);
                    PlayerPowerUpPassiveConfigBuildUtility.AccumulatePerfectCirclePassiveConfig(ref orbitConfig,
                                                                                                in candidateOrbitConfig,
                                                                                                ref hasOrbit);
                    break;
                case PowerUpModuleKind.BouncingProjectiles:
                    BouncingProjectilesPassiveConfig candidateBounceConfig = PlayerPowerUpPassiveConfigBuildUtility.BuildBouncingProjectilesPassiveConfig(payload.ProjectileBounceOnWalls);

                    if (!hasBounce)
                    {
                        hasBounce = true;
                        bounceConfig = candidateBounceConfig;
                        break;
                    }

                    bounceConfig.MaxBounces += math.max(0, candidateBounceConfig.MaxBounces);
                    bounceConfig.SpeedPercentChangePerBounce += candidateBounceConfig.SpeedPercentChangePerBounce;
                    bounceConfig.MinimumSpeedMultiplierAfterBounce = bounceConfig.MinimumSpeedMultiplierAfterBounce <= 0f
                        ? math.max(0f, candidateBounceConfig.MinimumSpeedMultiplierAfterBounce)
                        : math.min(bounceConfig.MinimumSpeedMultiplierAfterBounce, math.max(0f, candidateBounceConfig.MinimumSpeedMultiplierAfterBounce));
                    bounceConfig.MaximumSpeedMultiplierAfterBounce = math.max(bounceConfig.MaximumSpeedMultiplierAfterBounce,
                                                                              math.max(0f, candidateBounceConfig.MaximumSpeedMultiplierAfterBounce));
                    break;
                case PowerUpModuleKind.ProjectileSplit:
                    SplittingProjectilesPassiveConfig candidateSplitConfig = PlayerPowerUpPassiveConfigBuildUtility.BuildSplittingProjectilesPassiveConfig(payload.ProjectileSplit);

                    if (!hasSplit)
                    {
                        hasSplit = true;
                        splitConfig = candidateSplitConfig;
                        break;
                    }

                    splitConfig.SplitProjectileCount = math.max(splitConfig.SplitProjectileCount, candidateSplitConfig.SplitProjectileCount);
                    splitConfig.SplitOffsetDegrees = math.max(splitConfig.SplitOffsetDegrees, candidateSplitConfig.SplitOffsetDegrees);
                    splitConfig.SplitDamageMultiplier = math.max(splitConfig.SplitDamageMultiplier, candidateSplitConfig.SplitDamageMultiplier);
                    splitConfig.SplitSizeMultiplier = math.max(splitConfig.SplitSizeMultiplier, candidateSplitConfig.SplitSizeMultiplier);
                    splitConfig.SplitSpeedMultiplier = math.max(splitConfig.SplitSpeedMultiplier, candidateSplitConfig.SplitSpeedMultiplier);
                    splitConfig.SplitLifetimeMultiplier = math.max(splitConfig.SplitLifetimeMultiplier, candidateSplitConfig.SplitLifetimeMultiplier);

                    if (splitConfig.CustomAnglesDegrees.Length <= 0 && candidateSplitConfig.CustomAnglesDegrees.Length > 0)
                        splitConfig.CustomAnglesDegrees = candidateSplitConfig.CustomAnglesDegrees;

                    splitConfig.TriggerMode = candidateSplitConfig.TriggerMode;
                    splitConfig.DirectionMode = candidateSplitConfig.DirectionMode;
                    break;
                case PowerUpModuleKind.ProjectilesPatternCone:
                    PowerUpProjectilePatternConeModuleData shotgunPatternData = payload.ProjectilePatternCone;

                    if (shotgunPatternData == null)
                        break;

                    hasShotgunPattern = true;
                    shotgunProjectileCount += math.max(1, shotgunPatternData.ProjectileCount);
                    shotgunConeAngleDegrees = math.max(shotgunConeAngleDegrees, math.max(0f, shotgunPatternData.ConeAngleDegrees));
                    break;
                case PowerUpModuleKind.CharacterTuning:
                case PowerUpModuleKind.Stackable:
                    break;
                case PowerUpModuleKind.Heal:
                    PowerUpHealMissingHealthModuleData healData = payload.HealMissingHealth;

                    if (healData == null)
                        break;

                    hasHeal = true;
                    healAmount += math.max(0f, healData.HealAmount);
                    healDurationSeconds = math.max(healDurationSeconds,
                                                   PlayerPowerUpPassiveConfigBuildUtility.ResolvePassiveHealDurationSeconds(healData.ApplyMode,
                                                                                                                           healData.HealAmount,
                                                                                                                           healData.DurationSeconds));
                    healTickIntervalSeconds = math.min(healTickIntervalSeconds, math.max(0.01f, healData.TickIntervalSeconds));
                    healStackPolicy = healData.StackPolicy;
                    break;
                case PowerUpModuleKind.TimeDilationEnemies:
                    BulletTimeToolData bulletTimeData = payload.BulletTime;

                    if (bulletTimeData == null)
                        break;

                    hasBulletTime = true;
                    bulletTimeDurationSeconds = math.max(bulletTimeDurationSeconds, math.max(0.05f, bulletTimeData.Duration));
                    bulletTimeEnemySlowPercent = math.max(bulletTimeEnemySlowPercent,
                                                          math.clamp(bulletTimeData.EnemySlowPercent, 0f, 100f));
                    bulletTimeTransitionTimeSeconds = math.max(bulletTimeTransitionTimeSeconds,
                                                               math.max(0f, bulletTimeData.TransitionTimeSeconds));
                    break;
                case PowerUpModuleKind.LaserBeam:
                    LaserBeamPassiveConfig candidateLaserBeamConfig = PlayerPowerUpPassiveConfigBuildUtility.BuildLaserBeamPassiveConfig(payload.LaserBeam);

                    if (!hasLaserBeam)
                    {
                        hasLaserBeam = true;
                        laserBeamConfig = candidateLaserBeamConfig;
                        break;
                    }

                    laserBeamConfig.DamageMultiplier *= math.max(0f, candidateLaserBeamConfig.DamageMultiplier);
                    laserBeamConfig.ContinuousDamagePerSecondMultiplier *= math.max(0f, candidateLaserBeamConfig.ContinuousDamagePerSecondMultiplier);
                    laserBeamConfig.VirtualProjectileSpeedMultiplier *= math.max(0f, candidateLaserBeamConfig.VirtualProjectileSpeedMultiplier);
                    laserBeamConfig.DamageTickIntervalSeconds = math.min(laserBeamConfig.DamageTickIntervalSeconds,
                                                                        math.max(0.0001f, candidateLaserBeamConfig.DamageTickIntervalSeconds));
                    laserBeamConfig.MaximumContinuousActiveSeconds = math.max(laserBeamConfig.MaximumContinuousActiveSeconds,
                                                                             candidateLaserBeamConfig.MaximumContinuousActiveSeconds);
                    laserBeamConfig.CooldownSeconds = laserBeamConfig.CooldownSeconds <= 0f ||
                                                      candidateLaserBeamConfig.CooldownSeconds <= 0f
                        ? 0f
                        : math.min(laserBeamConfig.CooldownSeconds, candidateLaserBeamConfig.CooldownSeconds);
                    laserBeamConfig.MaximumBounceSegments = math.max(laserBeamConfig.MaximumBounceSegments,
                                                                    candidateLaserBeamConfig.MaximumBounceSegments);
                    laserBeamConfig.ApplyPlayerHandlingNerfWhileFiring =
                        laserBeamConfig.ApplyPlayerHandlingNerfWhileFiring != 0 ||
                        candidateLaserBeamConfig.ApplyPlayerHandlingNerfWhileFiring != 0
                            ? (byte)1
                            : (byte)0;
                    laserBeamConfig.FiringMoveSpeedMultiplier *= math.max(0f, candidateLaserBeamConfig.FiringMoveSpeedMultiplier);
                    laserBeamConfig.FiringRotationSpeedMultiplier *= math.max(0f, candidateLaserBeamConfig.FiringRotationSpeedMultiplier);
                    laserBeamConfig.BodyWidthMultiplier = math.max(laserBeamConfig.BodyWidthMultiplier,
                                                                  candidateLaserBeamConfig.BodyWidthMultiplier);
                    laserBeamConfig.CollisionWidthMultiplier = math.max(laserBeamConfig.CollisionWidthMultiplier,
                                                                       candidateLaserBeamConfig.CollisionWidthMultiplier);
                    laserBeamConfig.SourceScaleMultiplier = math.max(laserBeamConfig.SourceScaleMultiplier,
                                                                    candidateLaserBeamConfig.SourceScaleMultiplier);
                    laserBeamConfig.TerminalCapScaleMultiplier = math.max(laserBeamConfig.TerminalCapScaleMultiplier,
                                                                         candidateLaserBeamConfig.TerminalCapScaleMultiplier);
                    laserBeamConfig.ContactFlareScaleMultiplier = math.max(laserBeamConfig.ContactFlareScaleMultiplier,
                                                                          candidateLaserBeamConfig.ContactFlareScaleMultiplier);
                    laserBeamConfig.BodyOpacity = math.max(laserBeamConfig.BodyOpacity,
                                                          candidateLaserBeamConfig.BodyOpacity);
                    laserBeamConfig.CoreWidthMultiplier = math.max(laserBeamConfig.CoreWidthMultiplier,
                                                                   candidateLaserBeamConfig.CoreWidthMultiplier);
                    laserBeamConfig.CoreBrightness = math.max(laserBeamConfig.CoreBrightness,
                                                             candidateLaserBeamConfig.CoreBrightness);
                    laserBeamConfig.RimBrightness = math.max(laserBeamConfig.RimBrightness,
                                                            candidateLaserBeamConfig.RimBrightness);
                    laserBeamConfig.FlowScrollSpeed = math.max(laserBeamConfig.FlowScrollSpeed,
                                                              candidateLaserBeamConfig.FlowScrollSpeed);
                    laserBeamConfig.FlowPulseFrequency = math.max(laserBeamConfig.FlowPulseFrequency,
                                                                 candidateLaserBeamConfig.FlowPulseFrequency);
                    laserBeamConfig.StormTwistSpeed = math.max(laserBeamConfig.StormTwistSpeed,
                                                               candidateLaserBeamConfig.StormTwistSpeed);
                    laserBeamConfig.StormTickPostTravelHoldSeconds = math.max(laserBeamConfig.StormTickPostTravelHoldSeconds,
                                                                              candidateLaserBeamConfig.StormTickPostTravelHoldSeconds);
                    laserBeamConfig.StormIdleIntensity = math.max(laserBeamConfig.StormIdleIntensity,
                                                                  candidateLaserBeamConfig.StormIdleIntensity);
                    laserBeamConfig.StormBurstIntensity = math.max(laserBeamConfig.StormBurstIntensity,
                                                                   candidateLaserBeamConfig.StormBurstIntensity);
                    laserBeamConfig.SourceOffset = math.max(laserBeamConfig.SourceOffset,
                                                            candidateLaserBeamConfig.SourceOffset);
                    laserBeamConfig.SourceDischargeIntensity = math.max(laserBeamConfig.SourceDischargeIntensity,
                                                                       candidateLaserBeamConfig.SourceDischargeIntensity);
                    laserBeamConfig.StormShellWidthMultiplier = math.max(laserBeamConfig.StormShellWidthMultiplier,
                                                                        candidateLaserBeamConfig.StormShellWidthMultiplier);
                    laserBeamConfig.StormShellSeparation = math.max(laserBeamConfig.StormShellSeparation,
                                                                    candidateLaserBeamConfig.StormShellSeparation);
                    laserBeamConfig.StormRingFrequency = math.max(laserBeamConfig.StormRingFrequency,
                                                                  candidateLaserBeamConfig.StormRingFrequency);
                    laserBeamConfig.StormRingThickness = math.max(laserBeamConfig.StormRingThickness,
                                                                  candidateLaserBeamConfig.StormRingThickness);
                    laserBeamConfig.StormTickTravelSpeed = math.max(laserBeamConfig.StormTickTravelSpeed,
                                                                    candidateLaserBeamConfig.StormTickTravelSpeed);
                    laserBeamConfig.StormTickDamageLengthTolerance = math.max(laserBeamConfig.StormTickDamageLengthTolerance,
                                                                               candidateLaserBeamConfig.StormTickDamageLengthTolerance);
                    laserBeamConfig.TerminalCapIntensity = math.max(laserBeamConfig.TerminalCapIntensity,
                                                                    candidateLaserBeamConfig.TerminalCapIntensity);
                    laserBeamConfig.ContactFlareIntensity = math.max(laserBeamConfig.ContactFlareIntensity,
                                                                     candidateLaserBeamConfig.ContactFlareIntensity);
                    laserBeamConfig.WobbleAmplitude = math.max(laserBeamConfig.WobbleAmplitude,
                                                              candidateLaserBeamConfig.WobbleAmplitude);
                    laserBeamConfig.BubbleDriftSpeed = math.max(laserBeamConfig.BubbleDriftSpeed,
                                                                candidateLaserBeamConfig.BubbleDriftSpeed);
                    laserBeamConfig.VisualPresetId = candidateLaserBeamConfig.VisualPresetId;
                    laserBeamConfig.BodyProfile = candidateLaserBeamConfig.BodyProfile;
                    laserBeamConfig.SourceShape = candidateLaserBeamConfig.SourceShape;
                    laserBeamConfig.TerminalCapShape = candidateLaserBeamConfig.TerminalCapShape;
                    break;
            }
        }

        if (hasExplosion)
            explosionConfig.TriggerMode = PlayerPowerUpPassiveConfigBuildUtility.ResolveExplosionTriggerMode(hasTriggerEvent,
                                                                                                             triggerEventType,
                                                                                                             explosionConfig.TriggerMode);

        if (hasSplit)
            splitConfig.TriggerMode = PlayerPowerUpPassiveConfigBuildUtility.ResolveSplitTriggerMode(hasTriggerEvent,
                                                                                                     triggerEventType,
                                                                                                     splitConfig.TriggerMode);

        float trailAttachedVfxScaleMultiplier = authoring != null ? math.max(0.01f, authoring.ElementalTrailAttachedVfxScaleMultiplier) : 1f;
        PlayerPassiveToolConfig config = new PlayerPassiveToolConfig
        {
            IsDefined = 0,
            ToolKind = PassiveToolKind.Custom,
            HasProjectileSize = 0,
            HasShotgun = hasShotgunPattern ? (byte)1 : (byte)0,
            HasElementalProjectiles = hasElementalProjectiles ? (byte)1 : (byte)0,
            HasPerfectCircle = hasOrbit ? (byte)1 : (byte)0,
            HasBouncingProjectiles = hasBounce ? (byte)1 : (byte)0,
            HasSplittingProjectiles = hasSplit ? (byte)1 : (byte)0,
            HasExplosion = hasExplosion ? (byte)1 : (byte)0,
            HasElementalTrail = hasTrailSpawn || hasAreaTick ? (byte)1 : (byte)0,
            HasHeal = hasHeal && healAmount > 0f ? (byte)1 : (byte)0,
            HasBulletTime = hasBulletTime && bulletTimeEnemySlowPercent > 0f ? (byte)1 : (byte)0,
            HasLaserBeam = hasLaserBeam ? (byte)1 : (byte)0,
            ProjectileSize = new ProjectileSizePassiveConfig
            {
                SizeMultiplier = math.max(0.01f, projectileSizeMultiplier),
                DamageMultiplier = math.max(0f, projectileDamageMultiplier),
                SpeedMultiplier = math.max(0f, projectileSpeedMultiplier),
                LifetimeRangeMultiplier = math.max(0f, projectileRangeMultiplier),
                LifetimeSecondsMultiplier = math.max(0f, projectileLifetimeMultiplier)
            },
            Shotgun = new ShotgunPowerUpConfig
            {
                ProjectileCount = math.max(0, shotgunProjectileCount),
                ConeAngleDegrees = math.max(0f, shotgunConeAngleDegrees),
                SizeMultiplier = math.max(0.01f, projectileSizeMultiplier),
                DamageMultiplier = math.max(0f, projectileDamageMultiplier),
                SpeedMultiplier = math.max(0f, projectileSpeedMultiplier),
                RangeMultiplier = math.max(0f, projectileRangeMultiplier),
                LifetimeMultiplier = math.max(0f, projectileLifetimeMultiplier),
                PenetrationMode = projectilePenetrationMode,
                MaxPenetrations = math.max(0, projectileMaxPenetrations),
                HasElementalPayload = hasElementalProjectiles ? (byte)1 : (byte)0,
                ElementalEffect = elementalProjectilesEffect,
                ElementalStacksPerHit = math.max(0f, elementalProjectilesStacksPerHit)
            },
            ElementalProjectiles = new ElementalProjectilesPassiveConfig
            {
                Effect = elementalProjectilesEffect,
                StacksPerHit = math.max(0f, elementalProjectilesStacksPerHit)
            },
            PerfectCircle = orbitConfig,
            BouncingProjectiles = bounceConfig,
            SplittingProjectiles = splitConfig,
            Explosion = explosionConfig,
            ElementalTrail = new ElementalTrailPassiveConfig
            {
                Effect = trailEffect,
                TrailSegmentLifetimeSeconds = math.max(0.05f, trailSegmentLifetimeSeconds),
                TrailSpawnDistance = math.max(0f, trailSpawnDistance),
                TrailSpawnIntervalSeconds = math.max(0.01f, trailSpawnIntervalSeconds),
                TrailRadius = math.max(0f, trailRadius),
                MaxActiveSegmentsPerPlayer = math.max(1, maxTrailSegments),
                StacksPerTick = math.max(0f, trailStacksPerTick),
                ApplyIntervalSeconds = math.max(0.01f, trailApplyIntervalSeconds),
                TrailAttachedVfxPrefabEntity = Entity.Null,
                TrailAttachedVfxScaleMultiplier = trailAttachedVfxScaleMultiplier,
                TrailAttachedVfxOffset = trailAttachedVfxOffset
            },
            Heal = new PassiveHealConfig
            {
                TriggerMode = PlayerPowerUpPassiveConfigBuildUtility.ResolvePassiveHealTriggerMode(hasTriggerEvent,
                                                                                                   triggerEventType,
                                                                                                   PassiveHealTriggerMode.Periodic),
                CooldownSeconds = hasSharedCooldown ? math.max(0f, sharedCooldownSeconds) : 0f,
                HealAmount = math.max(0f, healAmount),
                DurationSeconds = math.max(0.05f, healDurationSeconds),
                TickIntervalSeconds = math.max(0.01f, healTickIntervalSeconds),
                StackPolicy = healStackPolicy
            },
            BulletTime = new PassiveBulletTimeConfig
            {
                TriggerMode = PlayerPowerUpPassiveConfigBuildUtility.ResolvePassiveBulletTimeTriggerMode(hasTriggerEvent,
                                                                                                         triggerEventType,
                                                                                                         PassiveBulletTimeTriggerMode.Periodic),
                CooldownSeconds = hasSharedCooldown ? math.max(0f, sharedCooldownSeconds) : 0f,
                DurationSeconds = math.max(0.05f, bulletTimeDurationSeconds),
                EnemySlowPercent = math.clamp(bulletTimeEnemySlowPercent, 0f, 100f),
                TransitionTimeSeconds = math.max(0f, bulletTimeTransitionTimeSeconds)
            },
            LaserBeam = laserBeamConfig
        };

        if (!PlayerPowerUpPassiveConfigBuildUtility.HasAnyPayload(config))
            return default;

        config.IsDefined = 1;
        config.ToolKind = PlayerPowerUpPassiveConfigBuildUtility.ResolvePassiveToolKind(config);
        return config;
    }

    /// <summary>
    /// Compiles a legacy passive tool into a runtime passive config.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="passiveTool">Legacy passive tool definition.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Runtime passive config or default.</returns>
    public static PlayerPassiveToolConfig BuildPassiveToolConfig(PlayerAuthoring authoring,
                                                                 PassiveToolDefinition passiveTool,
                                                                 Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (passiveTool == null)
            return default;

        PassiveToolKind toolKind = passiveTool.ToolKind == PassiveToolKind.Custom
            ? PassiveToolKind.ProjectileSize
            : passiveTool.ToolKind;

        byte hasProjectileSize = 0;
        byte hasElementalProjectiles = 0;
        byte hasPerfectCircle = 0;
        byte hasBouncingProjectiles = 0;
        byte hasSplittingProjectiles = 0;
        byte hasExplosion = 0;
        byte hasElementalTrail = 0;

        switch (toolKind)
        {
            case PassiveToolKind.ProjectileSize:
                hasProjectileSize = 1;
                break;
            case PassiveToolKind.ElementalProjectiles:
                hasElementalProjectiles = 1;
                break;
            case PassiveToolKind.PerfectCircle:
                hasPerfectCircle = 1;
                break;
            case PassiveToolKind.BouncingProjectiles:
                hasBouncingProjectiles = 1;
                break;
            case PassiveToolKind.SplittingProjectiles:
                hasSplittingProjectiles = 1;
                break;
            case PassiveToolKind.Explosion:
                hasExplosion = 1;
                break;
            case PassiveToolKind.ElementalTrail:
                hasElementalTrail = 1;
                break;
        }

        return new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            ToolKind = toolKind,
            HasProjectileSize = hasProjectileSize,
            HasShotgun = 0,
            HasElementalProjectiles = hasElementalProjectiles,
            HasPerfectCircle = hasPerfectCircle,
            HasBouncingProjectiles = hasBouncingProjectiles,
            HasSplittingProjectiles = hasSplittingProjectiles,
            HasExplosion = hasExplosion,
            HasElementalTrail = hasElementalTrail,
            HasHeal = 0,
            ProjectileSize = PlayerPowerUpPassiveConfigBuildUtility.BuildProjectileSizePassiveConfig(passiveTool.ProjectileSizeData),
            ElementalProjectiles = PlayerPowerUpPassiveConfigBuildUtility.BuildElementalProjectilesPassiveConfig(passiveTool.ElementalProjectilesData),
            PerfectCircle = PlayerPowerUpPassiveConfigBuildUtility.BuildPerfectCirclePassiveConfig(passiveTool.PerfectCircleData),
            BouncingProjectiles = PlayerPowerUpPassiveConfigBuildUtility.BuildBouncingProjectilesPassiveConfig(passiveTool.BouncingProjectilesData),
            SplittingProjectiles = PlayerPowerUpPassiveConfigBuildUtility.BuildSplittingProjectilesPassiveConfig(passiveTool.SplittingProjectilesData),
            Explosion = PlayerPowerUpPassiveConfigBuildUtility.BuildExplosionPassiveConfig(authoring, passiveTool.ExplosionData, resolveDynamicPrefabEntity),
            ElementalTrail = PlayerPowerUpPassiveConfigBuildUtility.BuildElementalTrailPassiveConfig(authoring, passiveTool.ElementalTrailData)
        };
    }
    #endregion
    #endregion
}
