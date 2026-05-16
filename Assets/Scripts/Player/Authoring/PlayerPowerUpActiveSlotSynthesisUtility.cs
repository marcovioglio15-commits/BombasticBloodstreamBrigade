using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Assembles active power-up slot payloads after modular data aggregation.
/// </summary>
public static class PlayerPowerUpActiveSlotSynthesisUtility
{
    #region Methods

    #region Public Methods
    public static void AccumulateResourceGate(PowerUpResourceGateModuleData resourceGateData,
                                              ref bool hasGateResource,
                                              ref PowerUpResourceType activationResource,
                                              ref PowerUpResourceType maintenanceResource,
                                              ref PowerUpChargeType chargeType,
                                              ref bool isToggleable,
                                              ref float maintenanceTicksPerSecond,
                                              ref bool allowRechargeDuringToggleStartupLock,
                                              ref float maximumEnergy,
                                              ref float activationCost,
                                              ref float maintenanceCostPerSecond,
                                              ref float chargePerTrigger,
                                              ref float cooldownSeconds,
                                              ref bool hasCooldownSeconds,
                                              ref float minimumActivationEnergyPercent)
    {
        if (resourceGateData == null)
            return;

        if (!hasGateResource)
        {
            hasGateResource = true;
            activationResource = resourceGateData.ActivationResource;
            maintenanceResource = resourceGateData.MaintenanceResource;
            chargeType = resourceGateData.ChargeType;
            isToggleable = resourceGateData.IsToggleable;
            maintenanceTicksPerSecond = resourceGateData.IsToggleable ? math.max(0.01f, resourceGateData.MaintenanceTicksPerSecond) : 0f;
            allowRechargeDuringToggleStartupLock = resourceGateData.AllowRechargeDuringToggleStartupLock;
        }
        else
        {
            if (activationResource == PowerUpResourceType.None && resourceGateData.ActivationResource != PowerUpResourceType.None)
                activationResource = resourceGateData.ActivationResource;

            if (maintenanceResource == PowerUpResourceType.None && resourceGateData.MaintenanceResource != PowerUpResourceType.None)
                maintenanceResource = resourceGateData.MaintenanceResource;

            isToggleable = isToggleable || resourceGateData.IsToggleable;

            if (resourceGateData.IsToggleable)
                maintenanceTicksPerSecond = math.max(maintenanceTicksPerSecond, math.max(0.01f, resourceGateData.MaintenanceTicksPerSecond));

            allowRechargeDuringToggleStartupLock = allowRechargeDuringToggleStartupLock || resourceGateData.AllowRechargeDuringToggleStartupLock;
        }

        maximumEnergy = math.max(maximumEnergy, math.max(0f, resourceGateData.MaximumEnergy));
        activationCost += math.max(0f, resourceGateData.ActivationCost);
        maintenanceCostPerSecond += math.max(0f, resourceGateData.MaintenanceCostPerSecond);
        minimumActivationEnergyPercent = math.max(minimumActivationEnergyPercent,
                                                  math.clamp(resourceGateData.MinimumActivationEnergyPercent, 0f, 100f));

        if (chargeType == PowerUpChargeType.Time && resourceGateData.ChargeType != PowerUpChargeType.Time)
            chargeType = resourceGateData.ChargeType;

        chargePerTrigger += math.max(0f, resourceGateData.ChargePerTrigger);

        float candidateCooldownSeconds = math.max(0f, resourceGateData.CooldownSeconds);

        if (candidateCooldownSeconds <= 0f)
            return;

        if (!hasCooldownSeconds)
        {
            hasCooldownSeconds = true;
            cooldownSeconds = candidateCooldownSeconds;
            return;
        }

        cooldownSeconds = math.min(cooldownSeconds, candidateCooldownSeconds);
    }

    /// <summary>
    /// Accumulates SpawnObject bomb payload values into the active slot aggregation state.
    /// </summary>
    /// <param name="bombModuleData">Bomb payload selected for the current module binding.</param>
    /// <param name="hasBomb">Aggregation flag set when a bomb payload contributes to the slot.</param>
    /// <param name="bombPrefab">Accumulated bomb prefab reference.</param>
    /// <param name="bombSpawnOffset">Accumulated bomb spawn offset.</param>
    /// <param name="bombSpawnOffsetOrientation">Accumulated spawn-offset orientation mode.</param>
    /// <param name="bombDeploySpeed">Accumulated bomb deploy speed.</param>
    /// <param name="bombCollisionRadius">Accumulated bomb collision radius.</param>
    /// <param name="bombBounceOnWalls">Accumulated wall-bounce flag.</param>
    /// <param name="bombBounceDamping">Accumulated bounce damping.</param>
    /// <param name="bombLinearDampingPerSecond">Accumulated movement damping.</param>
    /// <param name="bombFuseSeconds">Accumulated fuse duration.</param>
    /// <param name="bombDamagePayloadEnabled">Accumulated damage payload flag.</param>
    /// <param name="bombPayloadRadius">Accumulated explosion radius.</param>
    /// <param name="bombPayloadDamage">Accumulated explosion damage.</param>
    /// <param name="bombPayloadAffectAllEnemies">Accumulated radius target selection flag.</param>
    /// <param name="bombExplosionVfxPrefab">Accumulated explosion VFX prefab.</param>
    /// <param name="bombScaleVfxToRadius">Accumulated explosion VFX radius-scaling flag.</param>
    /// <param name="bombVfxScaleMultiplier">Accumulated explosion VFX scale multiplier.</param>
    public static void AccumulateBombData(BombToolData bombModuleData,
                                          ref bool hasBomb,
                                          ref GameObject bombPrefab,
                                          ref float3 bombSpawnOffset,
                                          ref SpawnOffsetOrientationMode bombSpawnOffsetOrientation,
                                          ref float bombDeploySpeed,
                                          ref float bombCollisionRadius,
                                          ref bool bombBounceOnWalls,
                                          ref float bombBounceDamping,
                                          ref float bombLinearDampingPerSecond,
                                          ref float bombFuseSeconds,
                                          ref bool bombDamagePayloadEnabled,
                                          ref float bombPayloadRadius,
                                          ref float bombPayloadDamage,
                                          ref bool bombPayloadAffectAllEnemies,
                                          ref GameObject bombExplosionVfxPrefab,
                                          ref bool bombScaleVfxToRadius,
                                          ref float bombVfxScaleMultiplier)
    {
        AccumulateBombData(bombModuleData,
                           null,
                           ref hasBomb,
                           ref bombPrefab,
                           ref bombSpawnOffset,
                           ref bombSpawnOffsetOrientation,
                           ref bombDeploySpeed,
                           ref bombCollisionRadius,
                           ref bombBounceOnWalls,
                           ref bombBounceDamping,
                           ref bombLinearDampingPerSecond,
                           ref bombFuseSeconds,
                           ref bombDamagePayloadEnabled,
                           ref bombPayloadRadius,
                           ref bombPayloadDamage,
                           ref bombPayloadAffectAllEnemies,
                           ref bombExplosionVfxPrefab,
                           ref bombScaleVfxToRadius,
                           ref bombVfxScaleMultiplier);
    }

    /// <summary>
    /// Accumulates SpawnObject bomb payload values while inheriting required object references from module defaults.
    /// </summary>
    /// <param name="bombModuleData">Override or default bomb payload selected for the current module binding.</param>
    /// <param name="fallbackBombModuleData">Module-default bomb payload used for missing prefab and VFX references.</param>
    /// <param name="hasBomb">Aggregation flag set when a bomb payload contributes to the slot.</param>
    /// <param name="bombPrefab">Accumulated bomb prefab reference.</param>
    /// <param name="bombSpawnOffset">Accumulated bomb spawn offset.</param>
    /// <param name="bombSpawnOffsetOrientation">Accumulated spawn-offset orientation mode.</param>
    /// <param name="bombDeploySpeed">Accumulated bomb deploy speed.</param>
    /// <param name="bombCollisionRadius">Accumulated bomb collision radius.</param>
    /// <param name="bombBounceOnWalls">Accumulated wall-bounce flag.</param>
    /// <param name="bombBounceDamping">Accumulated bounce damping.</param>
    /// <param name="bombLinearDampingPerSecond">Accumulated movement damping.</param>
    /// <param name="bombFuseSeconds">Accumulated fuse duration.</param>
    /// <param name="bombDamagePayloadEnabled">Accumulated damage payload flag.</param>
    /// <param name="bombPayloadRadius">Accumulated explosion radius.</param>
    /// <param name="bombPayloadDamage">Accumulated explosion damage.</param>
    /// <param name="bombPayloadAffectAllEnemies">Accumulated radius target selection flag.</param>
    /// <param name="bombExplosionVfxPrefab">Accumulated explosion VFX prefab.</param>
    /// <param name="bombScaleVfxToRadius">Accumulated explosion VFX radius-scaling flag.</param>
    /// <param name="bombVfxScaleMultiplier">Accumulated explosion VFX scale multiplier.</param>
    public static void AccumulateBombData(BombToolData bombModuleData,
                                          BombToolData fallbackBombModuleData,
                                          ref bool hasBomb,
                                          ref GameObject bombPrefab,
                                          ref float3 bombSpawnOffset,
                                          ref SpawnOffsetOrientationMode bombSpawnOffsetOrientation,
                                          ref float bombDeploySpeed,
                                          ref float bombCollisionRadius,
                                          ref bool bombBounceOnWalls,
                                          ref float bombBounceDamping,
                                          ref float bombLinearDampingPerSecond,
                                          ref float bombFuseSeconds,
                                          ref bool bombDamagePayloadEnabled,
                                          ref float bombPayloadRadius,
                                          ref float bombPayloadDamage,
                                          ref bool bombPayloadAffectAllEnemies,
                                          ref GameObject bombExplosionVfxPrefab,
                                          ref bool bombScaleVfxToRadius,
                                          ref float bombVfxScaleMultiplier)
    {
        BombToolData resolvedBombModuleData = ResolveBombData(bombModuleData, fallbackBombModuleData);

        if (resolvedBombModuleData == null)
            return;

        hasBomb = true;

        GameObject resolvedBombPrefab = ResolvePrefab(resolvedBombModuleData.BombPrefab,
                                                      fallbackBombModuleData != null ? fallbackBombModuleData.BombPrefab : null);

        if (bombPrefab == null && resolvedBombPrefab != null)
            bombPrefab = resolvedBombPrefab;

        if (math.lengthsq(bombSpawnOffset) <= 0f)
            bombSpawnOffset = new float3(resolvedBombModuleData.SpawnOffset.x, resolvedBombModuleData.SpawnOffset.y, resolvedBombModuleData.SpawnOffset.z);

        bombSpawnOffsetOrientation = resolvedBombModuleData.SpawnOffsetOrientation;
        bombDeploySpeed = math.max(bombDeploySpeed, math.max(0f, resolvedBombModuleData.DeploySpeed));
        bombCollisionRadius = math.max(bombCollisionRadius, math.max(0.01f, resolvedBombModuleData.CollisionRadius));
        bombBounceOnWalls = bombBounceOnWalls || resolvedBombModuleData.BounceOnWalls;
        bombBounceDamping = math.max(bombBounceDamping, math.clamp(resolvedBombModuleData.BounceDamping, 0f, 1f));
        bombLinearDampingPerSecond = math.max(bombLinearDampingPerSecond, math.max(0f, resolvedBombModuleData.LinearDampingPerSecond));
        bombFuseSeconds = math.min(bombFuseSeconds, math.max(0.05f, resolvedBombModuleData.FuseSeconds));
        bombDamagePayloadEnabled = bombDamagePayloadEnabled || resolvedBombModuleData.EnableDamagePayload;
        bombPayloadRadius = math.max(bombPayloadRadius, math.max(0.1f, resolvedBombModuleData.Radius));
        bombPayloadDamage += math.max(0f, resolvedBombModuleData.Damage);
        bombPayloadAffectAllEnemies = bombPayloadAffectAllEnemies || resolvedBombModuleData.AffectAllEnemiesInRadius;

        BombToolData resolvedVfxModuleData = ResolveVfxData(resolvedBombModuleData, fallbackBombModuleData);

        if (bombExplosionVfxPrefab != null || resolvedVfxModuleData == null || resolvedVfxModuleData.ExplosionVfxPrefab == null)
            return;

        bombExplosionVfxPrefab = resolvedVfxModuleData.ExplosionVfxPrefab;
        bombScaleVfxToRadius = resolvedVfxModuleData.ScaleVfxToRadius;
        bombVfxScaleMultiplier = math.max(0.01f, resolvedVfxModuleData.VfxScaleMultiplier);
    }

    public static ActiveToolKind ResolveModularToolKind(bool hasHoldCharge,
                                                        bool hasShotgun,
                                                        bool hasBomb,
                                                        bool hasDash,
                                                        bool hasBulletTime,
                                                        bool hasHealthPack)
    {
        if (hasHoldCharge)
            return ActiveToolKind.ChargeShot;

        if (hasShotgun)
            return ActiveToolKind.Shotgun;

        if (hasBomb)
            return ActiveToolKind.Bomb;

        if (hasDash)
            return ActiveToolKind.Dash;

        if (hasBulletTime)
            return ActiveToolKind.BulletTime;

        if (hasHealthPack)
            return ActiveToolKind.PortableHealthPack;

        return ActiveToolKind.Custom;
    }

    public static Entity ResolveBombExplosionVfx(PlayerAuthoring authoring,
                                                 GameObject bombExplosionVfxPrefab,
                                                 Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (authoring != null && authoring.BakePowerUpVfxEntityPrefabs)
            return PlayerPowerUpBakeSharedUtility.ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                                     bombExplosionVfxPrefab,
                                                                                     "Bomb Explosion VFX",
                                                                                     resolveDynamicPrefabEntity);
#if UNITY_EDITOR
        if (authoring != null && bombExplosionVfxPrefab != null)
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Bomb explosion VFX prefab is assigned on '{0}', but BakePowerUpVfxEntityPrefabs is disabled. SpawnObject explosion VFX will not spawn at runtime.",
                                           authoring.name),
                             authoring);
#endif
        return Entity.Null;
    }

    public static PlayerPowerUpSlotConfig BuildModularSlotConfig(ModularPowerUpDefinition powerUp,
                                                                 PowerUpResourceType activationResource,
                                                                 PowerUpResourceType maintenanceResource,
                                                                 PowerUpChargeType chargeType,
                                                                 bool isToggleable,
                                                                 float maximumEnergy,
                                                                 float activationCost,
                                                                 float maintenanceCostPerSecond,
                                                                 float maintenanceTicksPerSecond,
                                                                 float chargePerTrigger,
                                                                 float cooldownSeconds,
                                                                 bool allowRechargeDuringToggleStartupLock,
                                                                 float minimumActivationEnergyPercent,
                                                                 bool suppressBaseShootingWhileActive,
                                                                 bool interruptOtherSlotOnEnter,
                                                                 bool interruptOtherSlotChargingOnly,
                                                                 Entity bombPrefabEntity,
                                                                 float3 bombSpawnOffset,
                                                                 SpawnOffsetOrientationMode bombSpawnOffsetOrientation,
                                                                 float bombDeploySpeed,
                                                                 float bombCollisionRadius,
                                                                 bool bombBounceOnWalls,
                                                                 float bombBounceDamping,
                                                                 float bombLinearDampingPerSecond,
                                                                 float bombFuseSeconds,
                                                                 byte bombEnableDamagePayload,
                                                                 float bombRadius,
                                                                 float bombDamage,
                                                                 byte bombAffectAll,
                                                                 Entity bombExplosionVfxPrefabEntity,
                                                                 bool bombScaleVfxToRadius,
                                                                 float bombVfxScaleMultiplier,
                                                                 float dashDistance,
                                                                 DashDirectionMode dashDirectionMode,
                                                                 float dashDuration,
                                                                 float dashSpeedTransitionInSeconds,
                                                                 float dashSpeedTransitionOutSeconds,
                                                                 float dashWallBounceIntensity,
                                                                 bool dashGrantsInvulnerability,
                                                                 float dashInvulnerabilityExtraTime,
                                                                 float bulletTimeDuration,
                                                                 float bulletTimeEnemySlowPercent,
                                                                 float bulletTimeTransitionTimeSeconds,
                                                                 bool hasTriggerPress,
                                                                 bool hasTriggerRelease,
                                                                 bool hasHoldCharge,
                                                                 float holdChargeRequired,
                                                                 float holdChargeMaximum,
                                                                 float holdChargeRatePerSecond,
                                                                 bool decayAfterRelease,
                                                                 float decayAfterReleasePercentPerSecond,
                                                                 bool passiveChargeGainWhileReleased,
                                                                 float passiveChargeGainPercentPerSecond,
                                                                 bool useChargedLaserBeam,
                                                                 float chargedLaserDurationSeconds,
                                                                 in LaserBeamPassiveConfig chargedLaserBeamConfig,
                                                                 bool slowPlayerWhileCharging,
                                                                 float maximumPlayerSlowPercent,
                                                                 in FixedList128Bytes<float> playerSlowCurveSamples,
                                                                 bool suppressBaseShootingWhileCharging,
                                                                 int shotgunProjectileCount,
                                                                 float shotgunConeAngleDegrees,
                                                                 float shotgunLaserDurationSeconds,
                                                                 float chargeShotLaserDurationSeconds,
                                                                 float projectileSizeMultiplier,
                                                                 float projectileDamageMultiplier,
                                                                 float projectileSpeedMultiplier,
                                                                 float projectileRangeMultiplier,
                                                                 float projectileLifetimeMultiplier,
                                                                 ProjectilePenetrationMode projectilePenetrationMode,
                                                                 int projectileMaxPenetrations,
                                                                 bool hasProjectileElementalPayload,
                                                                 ElementalEffectConfig projectileElementalEffect,
                                                                 float projectileElementalStacksPerHit,
                                                                 bool hasHealthPackOverTime,
                                                                 float healthPackHealAmount,
                                                                 float healthPackDurationSeconds,
                                                                 float healthPackTickIntervalSeconds,
                                                                 PowerUpHealStackPolicy healthPackStackPolicy,
                                                                 in PlayerPassiveToolConfig triggeredProjectilePassiveTool,
                                                                 in PlayerPassiveToolConfig togglePassiveTool,
                                                                 ActiveToolKind resolvedToolKind)
    {
        float shotgunSizeMultiplier = math.max(0.01f, projectileSizeMultiplier);
        float shotgunDamageMultiplier = math.max(0f, projectileDamageMultiplier);
        float shotgunSpeedMultiplier = math.max(0f, projectileSpeedMultiplier);
        float shotgunRangeMultiplier = math.max(0f, projectileRangeMultiplier);
        float shotgunLifetimeMultiplier = math.max(0f, projectileLifetimeMultiplier);
        int maxPenetrations = math.max(0, projectileMaxPenetrations);
        bool hasElementalPayload = hasProjectileElementalPayload && projectileElementalStacksPerHit > 0f;
        float elementalStacksPerHit = math.max(0f, projectileElementalStacksPerHit);
        float chargeShotRequired = math.max(0f, holdChargeRequired);
        float chargeShotMaximum = math.max(chargeShotRequired, holdChargeMaximum);
        float chargeShotRate = math.max(0f, holdChargeRatePerSecond);
        FixedList128Bytes<float> normalizedPlayerSlowCurveSamples = playerSlowCurveSamples;
        PlayerPowerUpSlowCurveBakeUtility.EnsureSampleCount(ref normalizedPlayerSlowCurveSamples);
        PowerUpActivationInputMode activationInputMode = ResolveActivationInputMode(hasTriggerPress,
                                                                                    hasTriggerRelease,
                                                                                    hasHoldCharge,
                                                                                    resolvedToolKind);

        return new PlayerPowerUpSlotConfig
        {
            IsDefined = 1,
            PowerUpId = ResolvePowerUpId(powerUp),
            ToolKind = resolvedToolKind,
            ActivationResource = activationResource,
            MaintenanceResource = maintenanceResource,
            ChargeType = chargeType,
            MaximumEnergy = maximumEnergy,
            ActivationCost = activationCost,
            MaintenanceCostPerSecond = maintenanceCostPerSecond,
            MaintenanceTicksPerSecond = isToggleable ? math.max(0.01f, maintenanceTicksPerSecond) : 0f,
            ChargePerTrigger = chargePerTrigger,
            CooldownSeconds = cooldownSeconds,
            ActivationInputMode = activationInputMode,
            Toggleable = isToggleable ? (byte)1 : (byte)0,
            AllowRechargeDuringToggleStartupLock = allowRechargeDuringToggleStartupLock ? (byte)1 : (byte)0,
            MinimumActivationEnergyPercent = math.clamp(minimumActivationEnergyPercent, 0f, 100f),
            Unreplaceable = powerUp.Unreplaceable ? (byte)1 : (byte)0,
            SuppressBaseShootingWhileActive = suppressBaseShootingWhileActive ? (byte)1 : (byte)0,
            InterruptOtherSlotOnEnter = interruptOtherSlotOnEnter ? (byte)1 : (byte)0,
            InterruptOtherSlotChargingOnly = interruptOtherSlotChargingOnly ? (byte)1 : (byte)0,
            BombPrefabEntity = bombPrefabEntity,
            Bomb = new BombPowerUpConfig
            {
                SpawnOffset = bombSpawnOffset,
                SpawnOffsetOrientation = bombSpawnOffsetOrientation,
                DeploySpeed = math.max(0f, bombDeploySpeed),
                CollisionRadius = math.max(0.01f, bombCollisionRadius),
                BounceOnWalls = bombBounceOnWalls ? (byte)1 : (byte)0,
                BounceDamping = math.clamp(bombBounceDamping, 0f, 1f),
                LinearDampingPerSecond = math.max(0f, bombLinearDampingPerSecond),
                FuseSeconds = math.max(0.05f, bombFuseSeconds == float.MaxValue ? 0.05f : bombFuseSeconds),
                EnableDamagePayload = bombEnableDamagePayload,
                Radius = bombRadius,
                Damage = bombDamage,
                AffectAllEnemiesInRadius = bombAffectAll,
                ExplosionVfxPrefabEntity = bombExplosionVfxPrefabEntity,
                ScaleVfxToRadius = bombScaleVfxToRadius ? (byte)1 : (byte)0,
                VfxScaleMultiplier = math.max(0.01f, bombVfxScaleMultiplier)
            },
            Dash = new DashPowerUpConfig
            {
                Distance = math.max(0f, dashDistance),
                DirectionMode = dashDirectionMode,
                Duration = math.max(0.01f, dashDuration),
                SpeedTransitionInSeconds = math.max(0f, dashSpeedTransitionInSeconds == float.MaxValue ? 0f : dashSpeedTransitionInSeconds),
                SpeedTransitionOutSeconds = math.max(0f, dashSpeedTransitionOutSeconds == float.MaxValue ? 0f : dashSpeedTransitionOutSeconds),
                WallBounceIntensity = math.clamp(dashWallBounceIntensity, 0f, 1f),
                GrantsInvulnerability = dashGrantsInvulnerability ? (byte)1 : (byte)0,
                InvulnerabilityExtraTime = math.max(0f, dashInvulnerabilityExtraTime)
            },
            BulletTime = new BulletTimePowerUpConfig
            {
                Duration = math.max(0.05f, bulletTimeDuration),
                EnemySlowPercent = math.clamp(bulletTimeEnemySlowPercent, 0f, 100f),
                TransitionTimeSeconds = math.max(0f, bulletTimeTransitionTimeSeconds)
            },
            Shotgun = new ShotgunPowerUpConfig
            {
                ProjectileCount = math.max(1, shotgunProjectileCount),
                ConeAngleDegrees = math.max(0f, shotgunConeAngleDegrees),
                LaserDurationSeconds = math.max(0f, shotgunLaserDurationSeconds),
                SizeMultiplier = shotgunSizeMultiplier,
                DamageMultiplier = shotgunDamageMultiplier,
                SpeedMultiplier = shotgunSpeedMultiplier,
                RangeMultiplier = shotgunRangeMultiplier,
                LifetimeMultiplier = shotgunLifetimeMultiplier,
                PenetrationMode = projectilePenetrationMode,
                MaxPenetrations = maxPenetrations,
                HasElementalPayload = hasElementalPayload ? (byte)1 : (byte)0,
                ElementalEffect = projectileElementalEffect,
                ElementalStacksPerHit = elementalStacksPerHit
            },
            ChargeShot = new ChargeShotPowerUpConfig
            {
                RequiredCharge = chargeShotRequired,
                MaximumCharge = chargeShotMaximum,
                ChargeRatePerSecond = chargeShotRate,
                LaserDurationSeconds = math.max(0f, chargeShotLaserDurationSeconds),
                UseChargedLaserBeam = useChargedLaserBeam ? (byte)1 : (byte)0,
                ChargedLaserDurationSeconds = math.max(0f, chargedLaserDurationSeconds),
                ChargedLaserBeam = chargedLaserBeamConfig,
                DecayAfterRelease = decayAfterRelease ? (byte)1 : (byte)0,
                DecayAfterReleasePercentPerSecond = math.max(0f, decayAfterReleasePercentPerSecond),
                PassiveChargeGainWhileReleased = passiveChargeGainWhileReleased ? (byte)1 : (byte)0,
                PassiveChargeGainPercentPerSecond = math.max(0f, passiveChargeGainPercentPerSecond),
                SuppressBaseShootingWhileCharging = suppressBaseShootingWhileCharging ? (byte)1 : (byte)0,
                SlowPlayerWhileCharging = slowPlayerWhileCharging ? (byte)1 : (byte)0,
                MaximumPlayerSlowPercent = math.clamp(maximumPlayerSlowPercent, 0f, 100f),
                PlayerSlowCurveSamples = normalizedPlayerSlowCurveSamples,
                SizeMultiplier = shotgunSizeMultiplier,
                DamageMultiplier = shotgunDamageMultiplier,
                SpeedMultiplier = shotgunSpeedMultiplier,
                RangeMultiplier = shotgunRangeMultiplier,
                LifetimeMultiplier = shotgunLifetimeMultiplier,
                PenetrationMode = projectilePenetrationMode,
                MaxPenetrations = maxPenetrations,
                HasElementalPayload = hasElementalPayload ? (byte)1 : (byte)0,
                ElementalEffect = projectileElementalEffect,
                ElementalStacksPerHit = elementalStacksPerHit
            },
            PortableHealthPack = new PortableHealthPackPowerUpConfig
            {
                ApplyMode = hasHealthPackOverTime ? PowerUpHealApplicationMode.OverTime : PowerUpHealApplicationMode.Instant,
                HealAmount = math.max(0f, healthPackHealAmount),
                DurationSeconds = hasHealthPackOverTime ? math.max(0f, healthPackDurationSeconds) : 0f,
                TickIntervalSeconds = math.max(0.01f, healthPackTickIntervalSeconds),
                StackPolicy = healthPackStackPolicy
            },
            TriggeredProjectilePassiveTool = triggeredProjectilePassiveTool,
            TogglePassiveTool = togglePassiveTool
        };
    }

    /// <summary>
    /// Resolves the stable power-up identifier embedded in one modular active definition.
    /// </summary>
    /// <param name="powerUp">Modular active power-up definition being compiled.</param>
    /// <returns>Stable power-up identifier or an empty fixed string when unavailable.</returns>
    private static FixedString64Bytes ResolvePowerUpId(ModularPowerUpDefinition powerUp)
    {
        if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
            return default;

        return new FixedString64Bytes(powerUp.CommonData.PowerUpId.Trim());
    }

    private static PowerUpActivationInputMode ResolveActivationInputMode(bool hasTriggerPress,
                                                                         bool hasTriggerRelease,
                                                                         bool hasHoldCharge,
                                                                         ActiveToolKind resolvedToolKind)
    {
        if (resolvedToolKind == ActiveToolKind.PassiveToggle)
            return PowerUpActivationInputMode.OnPress;

        if (hasTriggerRelease && !hasTriggerPress && !hasHoldCharge)
            return PowerUpActivationInputMode.OnRelease;

        return PowerUpActivationInputMode.OnPress;
    }

    /// <summary>
    /// Resolves the bomb payload that contributes numeric SpawnObject values.
    /// </summary>
    /// <param name="bombModuleData">Primary payload selected for the binding.</param>
    /// <param name="fallbackBombModuleData">Fallback module-default payload.</param>
    /// <returns>Primary payload when available; otherwise the fallback payload.</returns>
    private static BombToolData ResolveBombData(BombToolData bombModuleData, BombToolData fallbackBombModuleData)
    {
        if (bombModuleData != null)
            return bombModuleData;

        return fallbackBombModuleData;
    }

    /// <summary>
    /// Resolves the payload that owns the effective explosion VFX reference and scale settings.
    /// </summary>
    /// <param name="bombModuleData">Primary payload selected for the binding.</param>
    /// <param name="fallbackBombModuleData">Fallback module-default payload.</param>
    /// <returns>Payload with an explosion VFX prefab, or null when neither payload has one.</returns>
    private static BombToolData ResolveVfxData(BombToolData bombModuleData, BombToolData fallbackBombModuleData)
    {
        if (bombModuleData != null && bombModuleData.ExplosionVfxPrefab != null)
            return bombModuleData;

        if (fallbackBombModuleData != null && fallbackBombModuleData.ExplosionVfxPrefab != null)
            return fallbackBombModuleData;

        return null;
    }

    /// <summary>
    /// Resolves an object reference with a module-default fallback.
    /// </summary>
    /// <param name="primaryPrefab">Primary prefab reference.</param>
    /// <param name="fallbackPrefab">Fallback prefab reference.</param>
    /// <returns>Primary prefab when assigned; otherwise fallback prefab.</returns>
    private static GameObject ResolvePrefab(GameObject primaryPrefab, GameObject fallbackPrefab)
    {
        if (primaryPrefab != null)
            return primaryPrefab;

        return fallbackPrefab;
    }
    #endregion

    #endregion
}
