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
        if (bombModuleData == null)
            return;

        hasBomb = true;

        if (bombPrefab == null && bombModuleData.BombPrefab != null)
            bombPrefab = bombModuleData.BombPrefab;

        if (math.lengthsq(bombSpawnOffset) <= 0f)
            bombSpawnOffset = new float3(bombModuleData.SpawnOffset.x, bombModuleData.SpawnOffset.y, bombModuleData.SpawnOffset.z);

        bombSpawnOffsetOrientation = bombModuleData.SpawnOffsetOrientation;
        bombDeploySpeed = math.max(bombDeploySpeed, math.max(0f, bombModuleData.DeploySpeed));
        bombCollisionRadius = math.max(bombCollisionRadius, math.max(0.01f, bombModuleData.CollisionRadius));
        bombBounceOnWalls = bombBounceOnWalls || bombModuleData.BounceOnWalls;
        bombBounceDamping = math.max(bombBounceDamping, math.clamp(bombModuleData.BounceDamping, 0f, 1f));
        bombLinearDampingPerSecond = math.max(bombLinearDampingPerSecond, math.max(0f, bombModuleData.LinearDampingPerSecond));
        bombFuseSeconds = math.min(bombFuseSeconds, math.max(0.05f, bombModuleData.FuseSeconds));
        bombDamagePayloadEnabled = bombDamagePayloadEnabled || bombModuleData.EnableDamagePayload;
        bombPayloadRadius = math.max(bombPayloadRadius, math.max(0.1f, bombModuleData.Radius));
        bombPayloadDamage += math.max(0f, bombModuleData.Damage);
        bombPayloadAffectAllEnemies = bombPayloadAffectAllEnemies || bombModuleData.AffectAllEnemiesInRadius;

        if (bombExplosionVfxPrefab != null || bombModuleData.ExplosionVfxPrefab == null)
            return;

        bombExplosionVfxPrefab = bombModuleData.ExplosionVfxPrefab;
        bombScaleVfxToRadius = bombModuleData.ScaleVfxToRadius;
        bombVfxScaleMultiplier = math.max(0.01f, bombModuleData.VfxScaleMultiplier);
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
    /// powerUp: Modular active power-up definition being compiled.
    /// returns Stable power-up identifier or an empty fixed string when unavailable.
    /// </summary>
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
    #endregion

    #endregion
}
