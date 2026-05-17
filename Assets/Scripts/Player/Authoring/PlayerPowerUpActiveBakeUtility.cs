using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles active power-up loadouts from legacy tools and modular power-up definitions.
/// </summary>
public static class PlayerPowerUpActiveBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime active loadout config from one power-ups preset.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Primary and secondary active slot config.</returns>
    public static PlayerPowerUpsConfig BuildPowerUpsConfig(PlayerAuthoring authoring,
                                                           PlayerPowerUpsPreset preset,
                                                           Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (preset == null)
            return default;

        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = preset.ActivePowerUps;

        if (activePowerUps == null || activePowerUps.Count <= 0)
            return BuildLegacyLoadoutPowerUpsConfig(authoring,
                                                    preset,
                                                    resolveDynamicPrefabEntity);

        ModularPowerUpDefinition primaryPowerUp = PlayerPowerUpBakeSharedUtility.ResolveLoadoutActivePowerUp(preset,
                                                                                                              preset.PrimaryActivePowerUpId,
                                                                                                              0,
                                                                                                              false);
        ModularPowerUpDefinition secondaryPowerUp = PlayerPowerUpBakeSharedUtility.ResolveLoadoutActivePowerUp(preset,
                                                                                                                preset.SecondaryActivePowerUpId,
                                                                                                                1,
                                                                                                                false);

        PlayerPowerUpSlotConfig primaryCompiledSlot = BuildSlotConfigFromModularPowerUp(authoring,
                                                                                        preset,
                                                                                        primaryPowerUp,
                                                                                        resolveDynamicPrefabEntity);
        PlayerPowerUpSlotConfig secondaryCompiledSlot = BuildSlotConfigFromModularPowerUp(authoring,
                                                                                          preset,
                                                                                          secondaryPowerUp,
                                                                                          resolveDynamicPrefabEntity);

        return new PlayerPowerUpsConfig
        {
            PrimarySlot = primaryCompiledSlot,
            SecondarySlot = secondaryCompiledSlot
        };
    }

    /// <summary>
    /// Builds the runtime active loadout config from legacy active tool definitions only.
    /// Used as fallback when modular active power-ups are missing entirely.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Primary and secondary legacy slot config.</returns>
    public static PlayerPowerUpsConfig BuildLegacyLoadoutPowerUpsConfig(PlayerAuthoring authoring,
                                                                        PlayerPowerUpsPreset preset,
                                                                        Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (preset == null)
            return default;

        IReadOnlyList<ActiveToolDefinition> activeTools = preset.ActiveTools;

        if (activeTools == null || activeTools.Count <= 0)
            return default;

        int secondaryFallbackIndex = activeTools.Count > 1 ? 1 : 0;
        ActiveToolDefinition primaryTool = PlayerPowerUpBakeSharedUtility.ResolveLoadoutTool(preset, preset.PrimaryActiveToolId, 0);
        ActiveToolDefinition secondaryTool = PlayerPowerUpBakeSharedUtility.ResolveLoadoutTool(preset,
                                                                                               preset.SecondaryActiveToolId,
                                                                                               secondaryFallbackIndex);

        if (activeTools.Count > 1 && ReferenceEquals(primaryTool, secondaryTool))
            secondaryTool = PlayerPowerUpBakeSharedUtility.ResolveLoadoutTool(preset, string.Empty, 1);

        return new PlayerPowerUpsConfig
        {
            PrimarySlot = BuildSlotConfig(authoring, primaryTool, resolveDynamicPrefabEntity),
            SecondarySlot = BuildSlotConfig(authoring, secondaryTool, resolveDynamicPrefabEntity)
        };
    }

    /// <summary>
    /// Compiles one modular active power-up into a runtime slot config.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <param name="powerUp">Modular active power-up definition.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Runtime slot config or default.</returns>
    public static PlayerPowerUpSlotConfig BuildSlotConfigFromModularPowerUp(PlayerAuthoring authoring,
                                                                            PlayerPowerUpsPreset preset,
                                                                            ModularPowerUpDefinition powerUp,
                                                                            Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (powerUp == null)
            return default;

        bool hasGateResource = false;
        PowerUpResourceType activationResource = PowerUpResourceType.None;
        PowerUpResourceType maintenanceResource = PowerUpResourceType.None;
        PowerUpChargeType chargeType = PowerUpChargeType.Time;
        bool isToggleable = false;
        float maximumEnergy = 0f;
        float activationCost = 0f;
        float maintenanceCostPerSecond = 0f;
        float maintenanceTicksPerSecond = 0f;
        float chargePerTrigger = 0f;
        float cooldownSeconds = 0f;
        bool hasCooldownSeconds = false;
        bool allowRechargeDuringToggleStartupLock = false;
        float minimumActivationEnergyPercent = 0f;
        bool suppressBaseShootingWhileActive = false;
        bool interruptOtherSlotOnEnter = false;
        bool interruptOtherSlotChargingOnly = true;
        bool hasShotgun = false;
        bool hasHoldCharge = false;
        float holdChargeRequired = 0f;
        float holdChargeMaximum = 0f;
        float holdChargeRatePerSecond = 0f;
        bool decayAfterRelease = false;
        float decayAfterReleasePercentPerSecond = 0f;
        bool passiveChargeGainWhileReleased = false;
        float passiveChargeGainPercentPerSecond = 0f;
        bool useChargedLaserBeam = false;
        float chargedLaserDurationSeconds = 0f;
        LaserBeamPassiveConfig chargedLaserBeamConfig = default;
        bool slowPlayerWhileCharging = false;
        float maximumPlayerSlowPercent = 0f;
        FixedList128Bytes<float> playerSlowCurveSamples = default;
        bool hasBomb = false;
        GameObject bombPrefab = null;
        float3 bombSpawnOffset = float3.zero;
        SpawnOffsetOrientationMode bombSpawnOffsetOrientation = SpawnOffsetOrientationMode.PlayerForward;
        float bombDeploySpeed = 0f;
        BombVelocityDirectionMode bombVelocityDirection = BombVelocityDirectionMode.AwayFromPlayer;
        float bombCollisionRadius = 0.1f;
        bool bombBounceOnWalls = false;
        float bombBounceDamping = 0f;
        float bombLinearDampingPerSecond = 0f;
        float bombFuseSeconds = float.MaxValue;
        bool bombDamagePayloadEnabled = false;
        float bombPayloadRadius = 0.1f;
        float bombPayloadDamage = 0f;
        bool bombPayloadAffectAllEnemies = false;
        GameObject bombExplosionVfxPrefab = null;
        bool bombScaleVfxToRadius = true;
        float bombVfxScaleMultiplier = 1f;
        bool hasDash = false;
        float dashDistance = 0f;
        DashDirectionMode dashDirectionMode = DashDirectionMode.PlayerMovement;
        float dashDuration = 0.01f;
        float dashSpeedTransitionInSeconds = 0f;
        float dashSpeedTransitionOutSeconds = 0f;
        float dashWallBounceIntensity = 0f;
        bool dashGrantsInvulnerability = false;
        float dashInvulnerabilityExtraTime = 0f;
        bool hasBulletTime = false;
        float bulletTimeDuration = 0.05f;
        float bulletTimeEnemySlowPercent = 0f;
        float bulletTimeTransitionTimeSeconds = 0f;
        bool hasHealthPack = false;
        bool hasHealthPackOverTime = false;
        float healthPackHealAmount = 0f;
        float healthPackDurationSeconds = 0f;
        float healthPackTickIntervalSeconds = 0.2f;
        PowerUpHealStackPolicy healthPackStackPolicy = PowerUpHealStackPolicy.Refresh;
        bool hasTriggerPress = false;
        bool hasTriggerRelease = false;
        bool suppressBaseShootingWhileCharging = false;
        int shotgunProjectileCount = 0;
        float shotgunConeAngleDegrees = 0f;
        float shotgunLaserDurationSeconds = 0f;
        float chargeShotLaserDurationSeconds = 0f;
        float projectileSizeMultiplier = 1f;
        float projectileDamageMultiplier = 1f;
        float projectileSpeedMultiplier = 1f;
        float projectileRangeMultiplier = 1f;
        float projectileLifetimeMultiplier = 1f;
        ProjectilePenetrationMode projectilePenetrationMode = ProjectilePenetrationMode.None;
        int projectileMaxPenetrations = 0;
        bool hasProjectileElementalPayload = false;
        ElementalEffectConfig projectileElementalEffect = default;
        float projectileElementalStacksPerHit = 0f;
        float explosionRadius = 0f;
        float explosionDamage = 0f;
        bool explosionAffectAllEnemies = false;
        bool hasExplosionData = false;
        bool hasCharacterTuning = false;
        bool applyCharacterTuningOnActiveTrigger = false;
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
                case PowerUpModuleKind.GateResource:
                    PlayerPowerUpActiveSlotSynthesisUtility.AccumulateResourceGate(payload.ResourceGate,
                                                                                    ref hasGateResource,
                                                                                    ref activationResource,
                                                                                    ref maintenanceResource,
                                                                                    ref chargeType,
                                                                                    ref isToggleable,
                                                                                    ref maintenanceTicksPerSecond,
                                                                                    ref allowRechargeDuringToggleStartupLock,
                                                                                    ref maximumEnergy,
                                                                                    ref activationCost,
                                                                                    ref maintenanceCostPerSecond,
                                                                                    ref chargePerTrigger,
                                                                                    ref cooldownSeconds,
                                                                                    ref hasCooldownSeconds,
                                                                                    ref minimumActivationEnergyPercent);
                    break;
                case PowerUpModuleKind.TriggerHoldCharge:
                    PowerUpHoldChargeModuleData holdChargeData = payload.HoldCharge;

                    if (holdChargeData == null)
                        break;

                    hasHoldCharge = true;
                    holdChargeRequired = math.max(holdChargeRequired, math.max(0f, holdChargeData.RequiredCharge));
                    holdChargeMaximum = math.max(math.max(holdChargeMaximum, holdChargeRequired), math.max(0f, holdChargeData.MaximumCharge));
                    holdChargeRatePerSecond += math.max(0f, holdChargeData.ChargeRatePerSecond);
                    decayAfterRelease = decayAfterRelease || holdChargeData.DecayAfterRelease;
                    decayAfterReleasePercentPerSecond = math.max(decayAfterReleasePercentPerSecond,
                                                                 math.max(0f, holdChargeData.DecayAfterReleasePercentPerSecond));
                    passiveChargeGainWhileReleased = passiveChargeGainWhileReleased || holdChargeData.PassiveChargeGainWhileReleased;
                    passiveChargeGainPercentPerSecond = math.max(passiveChargeGainPercentPerSecond,
                                                                 math.max(0f, holdChargeData.PassiveChargeGainPercentPerSecond));
                    chargeShotLaserDurationSeconds = math.max(chargeShotLaserDurationSeconds,
                                                              math.max(0f, holdChargeData.LaserDurationSeconds));
                    useChargedLaserBeam = useChargedLaserBeam || holdChargeData.UseChargedLaserBeam;

                    if (holdChargeData.UseChargedLaserBeam)
                    {
                        chargedLaserDurationSeconds = math.max(chargedLaserDurationSeconds,
                                                               math.max(0f, holdChargeData.ChargedLaserDurationSeconds));
                        chargedLaserBeamConfig = PlayerPowerUpPassiveConfigBuildUtility.BuildLaserBeamPassiveConfig(holdChargeData.ChargedLaserBeam);
                    }

                    slowPlayerWhileCharging = slowPlayerWhileCharging || holdChargeData.SlowPlayerWhileCharging;
                    maximumPlayerSlowPercent = math.max(maximumPlayerSlowPercent,
                                                        math.max(0f, holdChargeData.MaximumPlayerSlowPercent));

                    if (holdChargeData.SlowPlayerWhileCharging)
                    {
                        FixedList128Bytes<float> holdChargeSlowCurveSamples = PlayerPowerUpSlowCurveBakeUtility.BuildNormalizedSamples(holdChargeData.PlayerSlowCurve);
                        PlayerPowerUpSlowCurveBakeUtility.AccumulateMaximumSamples(ref playerSlowCurveSamples, in holdChargeSlowCurveSamples);
                    }

                    break;
                case PowerUpModuleKind.TriggerPress:
                    hasTriggerPress = true;
                    break;
                case PowerUpModuleKind.TriggerRelease:
                    hasTriggerRelease = true;
                    break;
                case PowerUpModuleKind.StateSuppressShooting:
                    PowerUpSuppressShootingModuleData suppressShootingData = payload.SuppressShooting;

                    if (suppressShootingData == null)
                        break;

                    suppressBaseShootingWhileCharging = suppressBaseShootingWhileCharging || suppressShootingData.SuppressBaseShootingWhileActive;
                    suppressBaseShootingWhileActive = suppressBaseShootingWhileActive || suppressShootingData.SuppressBaseShootingWhileActive;
                    interruptOtherSlotOnEnter = interruptOtherSlotOnEnter || suppressShootingData.InterruptOtherSlotOnEnter;
                    interruptOtherSlotChargingOnly = interruptOtherSlotChargingOnly && suppressShootingData.InterruptOtherSlotChargingOnly;
                    break;
                case PowerUpModuleKind.ProjectilesPatternCone:
                    PowerUpProjectilePatternConeModuleData shotgunPatternData = payload.ProjectilePatternCone;

                    if (shotgunPatternData == null)
                        break;

                    hasShotgun = true;
                    shotgunProjectileCount += math.max(1, shotgunPatternData.ProjectileCount);
                    shotgunConeAngleDegrees = math.max(shotgunConeAngleDegrees, math.max(0f, shotgunPatternData.ConeAngleDegrees));
                    shotgunLaserDurationSeconds = math.max(shotgunLaserDurationSeconds,
                                                           math.max(0f, shotgunPatternData.LaserDurationSeconds));
                    break;
                case PowerUpModuleKind.CharacterTuning:
                    bool hasCharacterTuningFormulas = HasCharacterTuningFormulas(payload);
                    hasCharacterTuning = hasCharacterTuning || hasCharacterTuningFormulas;

                    if (hasCharacterTuningFormulas && payload.CharacterTuning != null)
                        applyCharacterTuningOnActiveTrigger = applyCharacterTuningOnActiveTrigger ||
                                                              payload.CharacterTuning.ApplyFormulasOnlyOnActiveTrigger;

                    break;
                case PowerUpModuleKind.Stackable:
                    break;
                case PowerUpModuleKind.SpawnObject:
                    BombToolData fallbackBombData = ResolveFallbackBombData(binding, moduleDefinition, payload);
                    PlayerPowerUpActiveSlotSynthesisUtility.AccumulateBombData(payload.Bomb,
                                                                               fallbackBombData,
                                                                               ref hasBomb,
                                                                               ref bombPrefab,
                                                                               ref bombSpawnOffset,
                                                                               ref bombSpawnOffsetOrientation,
                                                                               ref bombDeploySpeed,
                                                                               ref bombVelocityDirection,
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
                    break;
                case PowerUpModuleKind.DeathExplosion:
                    ExplosionPassiveToolData explosionModuleData = payload.DeathExplosion;

                    if (explosionModuleData == null)
                        break;

                    hasExplosionData = true;
                    explosionRadius = math.max(explosionRadius, math.max(0f, explosionModuleData.Radius));
                    explosionDamage += math.max(0f, explosionModuleData.Damage);
                    explosionAffectAllEnemies = explosionAffectAllEnemies || explosionModuleData.AffectAllEnemiesInRadius;
                    break;
                case PowerUpModuleKind.Dash:
                    DashToolData dashModuleData = payload.Dash;

                    if (dashModuleData == null)
                        break;

                    hasDash = true;
                    dashDistance = math.max(dashDistance, math.max(0f, dashModuleData.Distance));
                    dashDirectionMode = dashModuleData.DirectionMode;
                    dashDuration = math.max(dashDuration, math.max(0.01f, dashModuleData.Duration));
                    dashSpeedTransitionInSeconds = math.min(dashSpeedTransitionInSeconds <= 0f ? float.MaxValue : dashSpeedTransitionInSeconds,
                                                            math.max(0f, dashModuleData.SpeedTransitionInSeconds));
                    dashSpeedTransitionOutSeconds = math.min(dashSpeedTransitionOutSeconds <= 0f ? float.MaxValue : dashSpeedTransitionOutSeconds,
                                                             math.max(0f, dashModuleData.SpeedTransitionOutSeconds));
                    dashWallBounceIntensity = math.max(dashWallBounceIntensity, math.max(0f, dashModuleData.WallBounceIntensity));
                    dashGrantsInvulnerability = dashGrantsInvulnerability || dashModuleData.GrantsInvulnerability;
                    dashInvulnerabilityExtraTime = math.max(dashInvulnerabilityExtraTime, math.max(0f, dashModuleData.InvulnerabilityExtraTime));
                    break;
                case PowerUpModuleKind.TimeDilationEnemies:
                    BulletTimeToolData bulletTimeModuleData = payload.BulletTime;

                    if (bulletTimeModuleData == null)
                        break;

                    hasBulletTime = true;
                    bulletTimeDuration = math.max(bulletTimeDuration, math.max(0.05f, bulletTimeModuleData.Duration));
                    bulletTimeEnemySlowPercent = math.max(bulletTimeEnemySlowPercent, math.clamp(bulletTimeModuleData.EnemySlowPercent, 0f, 100f));
                    bulletTimeTransitionTimeSeconds = math.max(bulletTimeTransitionTimeSeconds,
                                                               math.max(0f, bulletTimeModuleData.TransitionTimeSeconds));
                    break;
                case PowerUpModuleKind.Heal:
                    PowerUpHealMissingHealthModuleData healModuleData = payload.HealMissingHealth;

                    if (healModuleData == null)
                        break;

                    hasHealthPack = true;
                    healthPackHealAmount += math.max(0f, healModuleData.HealAmount);
                    healthPackStackPolicy = healModuleData.StackPolicy;

                    if (healModuleData.ApplyMode == PowerUpHealApplicationMode.OverTime)
                    {
                        hasHealthPackOverTime = true;
                        healthPackDurationSeconds = math.max(healthPackDurationSeconds, math.max(0f, healModuleData.DurationSeconds));
                        healthPackTickIntervalSeconds = math.min(healthPackTickIntervalSeconds, math.max(0.01f, healModuleData.TickIntervalSeconds));
                    }

                    break;
            }
        }

        PlayerPassiveToolConfig togglePassiveTool = default;
        PlayerPassiveToolConfig triggeredProjectilePassiveTool = default;
        ActiveToolKind resolvedToolKind = ActiveToolKind.Custom;

        if (isToggleable)
        {
            togglePassiveTool = PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(authoring,
                                                                                                         preset,
                                                                                                         powerUp,
                                                                                                         resolveDynamicPrefabEntity);

            if (togglePassiveTool.IsDefined == 0 && !hasCharacterTuning)
                return default;

            resolvedToolKind = ActiveToolKind.PassiveToggle;
        }
        else
        {
            resolvedToolKind = PlayerPowerUpActiveSlotSynthesisUtility.ResolveModularToolKind(hasHoldCharge,
                                                                                              hasShotgun,
                                                                                              hasBomb,
                                                                                              hasDash,
                                                                                              hasBulletTime,
                                                                                              hasHealthPack);

            if (resolvedToolKind == ActiveToolKind.ChargeShot || resolvedToolKind == ActiveToolKind.Shotgun)
            {
                triggeredProjectilePassiveTool = PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(authoring,
                                                                                                                           preset,
                                                                                                                           powerUp,
                                                                                                                           resolveDynamicPrefabEntity);
            }
        }

        if (resolvedToolKind == ActiveToolKind.Custom)
            return default;

        Entity bombPrefabEntity = Entity.Null;
        Entity bombExplosionVfxPrefabEntity = Entity.Null;

        if (resolvedToolKind == ActiveToolKind.Bomb && bombPrefab != null)
        {
            if (!PlayerPowerUpBakeSharedUtility.IsInvalidBombPrefab(authoring, bombPrefab))
                bombPrefabEntity = PlayerPowerUpBakeSharedUtility.ResolvePrefabEntity(resolveDynamicPrefabEntity, bombPrefab);
            else
            {
#if UNITY_EDITOR
                if (authoring != null)
                    Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid bomb prefab '{0}' on '{1}'. Assign a dedicated bomb prefab without PlayerAuthoring.", bombPrefab.name, authoring.name), authoring);
#endif
            }
        }

        if (resolvedToolKind == ActiveToolKind.Bomb)
            bombExplosionVfxPrefabEntity = PlayerPowerUpActiveSlotSynthesisUtility.ResolveBombExplosionVfx(authoring,
                                                                                                            bombExplosionVfxPrefab,
                                                                                                            resolveDynamicPrefabEntity);

        float bombRadius = math.max(0.1f, bombPayloadRadius);
        float bombDamage = math.max(0f, bombPayloadDamage);
        byte bombAffectAll = bombPayloadAffectAllEnemies ? (byte)1 : (byte)0;
        byte bombEnableDamagePayload = bombDamagePayloadEnabled ? (byte)1 : (byte)0;

        if (hasExplosionData)
        {
            bombRadius = math.max(0.1f, explosionRadius);
            bombDamage += math.max(0f, explosionDamage);
            bombAffectAll = explosionAffectAllEnemies ? (byte)1 : (byte)0;
            bombEnableDamagePayload = 1;
        }

        if (bombEnableDamagePayload == 0)
        {
            bombRadius = 0f;
            bombDamage = 0f;
            bombAffectAll = 0;
        }

        return PlayerPowerUpActiveSlotSynthesisUtility.BuildModularSlotConfig(powerUp,
                                                                              activationResource,
                                                                              maintenanceResource,
                                                                              chargeType,
                                                                              isToggleable,
                                                                              maximumEnergy,
                                                                              activationCost,
                                                                              maintenanceCostPerSecond,
                                                                              maintenanceTicksPerSecond,
                                                                              chargePerTrigger,
                                                                              cooldownSeconds,
                                                                              allowRechargeDuringToggleStartupLock,
                                                                              minimumActivationEnergyPercent,
                                                                              suppressBaseShootingWhileActive,
                                                                              interruptOtherSlotOnEnter,
                                                                              interruptOtherSlotChargingOnly,
                                                                              bombPrefabEntity,
                                                                              bombSpawnOffset,
                                                                              bombSpawnOffsetOrientation,
                                                                              bombDeploySpeed,
                                                                              bombVelocityDirection,
                                                                              bombCollisionRadius,
                                                                              bombBounceOnWalls,
                                                                              bombBounceDamping,
                                                                              bombLinearDampingPerSecond,
                                                                              bombFuseSeconds,
                                                                              bombEnableDamagePayload,
                                                                              bombRadius,
                                                                              bombDamage,
                                                                              bombAffectAll,
                                                                              bombExplosionVfxPrefabEntity,
                                                                              bombScaleVfxToRadius,
                                                                              bombVfxScaleMultiplier,
                                                                              dashDistance,
                                                                              dashDirectionMode,
                                                                              dashDuration,
                                                                              dashSpeedTransitionInSeconds,
                                                                              dashSpeedTransitionOutSeconds,
                                                                              dashWallBounceIntensity,
                                                                              dashGrantsInvulnerability,
                                                                              dashInvulnerabilityExtraTime,
                                                                              bulletTimeDuration,
                                                                              bulletTimeEnemySlowPercent,
                                                                              bulletTimeTransitionTimeSeconds,
                                                                              hasTriggerPress,
                                                                              hasTriggerRelease,
                                                                              hasHoldCharge,
                                                                              holdChargeRequired,
                                                                              holdChargeMaximum,
                                                                              holdChargeRatePerSecond,
                                                                              decayAfterRelease,
                                                                              decayAfterReleasePercentPerSecond,
                                                                              passiveChargeGainWhileReleased,
                                                                              passiveChargeGainPercentPerSecond,
                                                                              useChargedLaserBeam,
                                                                              chargedLaserDurationSeconds,
                                                                              in chargedLaserBeamConfig,
                                                                              slowPlayerWhileCharging,
                                                                              maximumPlayerSlowPercent,
                                                                              in playerSlowCurveSamples,
                                                                              suppressBaseShootingWhileCharging,
                                                                              shotgunProjectileCount,
                                                                              shotgunConeAngleDegrees,
                                                                              shotgunLaserDurationSeconds,
                                                                              chargeShotLaserDurationSeconds,
                                                                              projectileSizeMultiplier,
                                                                              projectileDamageMultiplier,
                                                                              projectileSpeedMultiplier,
                                                                              projectileRangeMultiplier,
                                                                              projectileLifetimeMultiplier,
                                                                              projectilePenetrationMode,
                                                                              projectileMaxPenetrations,
                                                                              hasProjectileElementalPayload,
                                                                              projectileElementalEffect,
                                                                              projectileElementalStacksPerHit,
                                                                              hasHealthPackOverTime,
                                                                              healthPackHealAmount,
                                                                              healthPackDurationSeconds,
                                                                              healthPackTickIntervalSeconds,
                                                                              healthPackStackPolicy,
                                                                              applyCharacterTuningOnActiveTrigger,
                                                                              in triggeredProjectilePassiveTool,
                                                                              in togglePassiveTool,
                                                                              resolvedToolKind);
    }

    /// <summary>
    /// Detects active-only character tuning formulas so toggle slots can be baked even when they do not contain a traditional passive payload.
    /// </summary>
    /// <param name="payload">Resolved module payload being inspected during active-slot synthesis.</param>
    /// <returns>True when the payload contains at least one character tuning formula.</returns>
    private static bool HasCharacterTuningFormulas(PowerUpModuleData payload)
    {
        if (payload == null || payload.CharacterTuning == null)
            return false;

        IReadOnlyList<PowerUpCharacterTuningFormulaData> formulas = payload.CharacterTuning.Formulas;
        return formulas != null && formulas.Count > 0;
    }

    /// <summary>
    /// Compiles a legacy active tool definition into a runtime slot config.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="activeTool">Legacy active tool definition.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <returns>Runtime slot config or default.</returns>
    public static PlayerPowerUpSlotConfig BuildSlotConfig(PlayerAuthoring authoring,
                                                          ActiveToolDefinition activeTool,
                                                          Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (activeTool == null)
            return default;

        BombToolData bombData = activeTool.BombData;
        DashToolData dashData = activeTool.DashData;
        BulletTimeToolData bulletTimeData = activeTool.BulletTimeData;
        Entity bombPrefabEntity = Entity.Null;
        Entity bombExplosionVfxPrefabEntity = Entity.Null;

        if (activeTool.ToolKind == ActiveToolKind.Bomb && bombData != null && bombData.BombPrefab != null)
        {
            GameObject bombPrefab = bombData.BombPrefab;

            if (!PlayerPowerUpBakeSharedUtility.IsInvalidBombPrefab(authoring, bombPrefab))
                bombPrefabEntity = PlayerPowerUpBakeSharedUtility.ResolvePrefabEntity(resolveDynamicPrefabEntity, bombPrefab);
            else
            {
#if UNITY_EDITOR
                if (authoring != null)
                    Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid bomb prefab '{0}' on '{1}'. Assign a dedicated bomb prefab without PlayerAuthoring.", bombPrefab.name, authoring.name), authoring);
#endif
            }
        }

        if (activeTool.ToolKind == ActiveToolKind.Bomb && bombData != null)
            bombExplosionVfxPrefabEntity = PlayerPowerUpActiveSlotSynthesisUtility.ResolveBombExplosionVfx(authoring,
                                                                                                            bombData.ExplosionVfxPrefab,
                                                                                                            resolveDynamicPrefabEntity);

        ActiveToolKind toolKind = activeTool.ToolKind == ActiveToolKind.Custom ? ActiveToolKind.Bomb : activeTool.ToolKind;

        return new PlayerPowerUpSlotConfig
        {
            IsDefined = 1,
            PowerUpId = ResolveLegacyPowerUpId(activeTool),
            ToolKind = toolKind,
            ActivationResource = activeTool.ActivationResource,
            MaintenanceResource = activeTool.MaintenanceResource,
            ChargeType = activeTool.ChargeType,
            MaximumEnergy = math.max(0f, activeTool.MaximumEnergy),
            ActivationCost = math.max(0f, activeTool.ActivationCost),
            MaintenanceCostPerSecond = math.max(0f, activeTool.MaintenanceCostPerSecond),
            MaintenanceTicksPerSecond = 0f,
            ChargePerTrigger = math.max(0f, activeTool.ChargePerTrigger),
            ActivationInputMode = PowerUpActivationInputMode.OnPress,
            Toggleable = activeTool.Toggleable ? (byte)1 : (byte)0,
            ApplyCharacterTuningOnActiveTrigger = 0,
            AllowRechargeDuringToggleStartupLock = 0,
            MinimumActivationEnergyPercent = math.clamp(activeTool.MinimumActivationEnergyPercent, 0f, 100f),
            Unreplaceable = activeTool.Unreplaceable ? (byte)1 : (byte)0,
            SuppressBaseShootingWhileActive = 0,
            InterruptOtherSlotOnEnter = 0,
            InterruptOtherSlotChargingOnly = 1,
            BombPrefabEntity = bombPrefabEntity,
            Bomb = new BombPowerUpConfig
            {
                SpawnOffset = bombData != null ? new float3(bombData.SpawnOffset.x, bombData.SpawnOffset.y, bombData.SpawnOffset.z) : float3.zero,
                SpawnOffsetOrientation = bombData != null ? bombData.SpawnOffsetOrientation : SpawnOffsetOrientationMode.PlayerForward,
                DeploySpeed = bombData != null ? math.max(0f, bombData.DeploySpeed) : 0f,
                VelocityDirection = bombData != null ? bombData.VelocityDirection : BombVelocityDirectionMode.AwayFromPlayer,
                CollisionRadius = bombData != null ? math.max(0.01f, bombData.CollisionRadius) : 0.1f,
                BounceOnWalls = bombData != null && bombData.BounceOnWalls ? (byte)1 : (byte)0,
                BounceDamping = bombData != null ? math.clamp(bombData.BounceDamping, 0f, 1f) : 0f,
                LinearDampingPerSecond = bombData != null ? math.max(0f, bombData.LinearDampingPerSecond) : 0f,
                FuseSeconds = bombData != null ? math.max(0.05f, bombData.FuseSeconds) : 0.05f,
                EnableDamagePayload = bombData != null && bombData.EnableDamagePayload ? (byte)1 : (byte)0,
                Radius = bombData != null ? math.max(0.1f, bombData.Radius) : 0.1f,
                Damage = bombData != null ? math.max(0f, bombData.Damage) : 0f,
                AffectAllEnemiesInRadius = bombData != null && bombData.AffectAllEnemiesInRadius ? (byte)1 : (byte)0,
                ExplosionVfxPrefabEntity = bombExplosionVfxPrefabEntity,
                ScaleVfxToRadius = bombData != null && bombData.ScaleVfxToRadius ? (byte)1 : (byte)0,
                VfxScaleMultiplier = bombData != null ? math.max(0.01f, bombData.VfxScaleMultiplier) : 1f
            },
            Dash = new DashPowerUpConfig
            {
                Distance = dashData != null ? math.max(0f, dashData.Distance) : 0f,
                DirectionMode = dashData != null ? dashData.DirectionMode : DashDirectionMode.PlayerMovement,
                Duration = dashData != null ? math.max(0.01f, dashData.Duration) : 0.01f,
                SpeedTransitionInSeconds = dashData != null ? math.max(0f, dashData.SpeedTransitionInSeconds) : 0f,
                SpeedTransitionOutSeconds = dashData != null ? math.max(0f, dashData.SpeedTransitionOutSeconds) : 0f,
                WallBounceIntensity = dashData != null ? math.clamp(dashData.WallBounceIntensity, 0f, 1f) : 0f,
                GrantsInvulnerability = dashData != null && dashData.GrantsInvulnerability ? (byte)1 : (byte)0,
                InvulnerabilityExtraTime = dashData != null ? math.max(0f, dashData.InvulnerabilityExtraTime) : 0f
            },
            BulletTime = new BulletTimePowerUpConfig
            {
                Duration = bulletTimeData != null ? math.max(0.05f, bulletTimeData.Duration) : 0.05f,
                EnemySlowPercent = bulletTimeData != null ? math.clamp(bulletTimeData.EnemySlowPercent, 0f, 100f) : 0f
            },
            ChargeShot = default,
            PortableHealthPack = default,
            TogglePassiveTool = default
        };
    }

    /// <summary>
    /// Resolves module-default bomb data used only for object-reference fallback when a SpawnObject binding override is active.
    /// </summary>
    /// <param name="binding">Module binding currently being compiled.</param>
    /// <param name="moduleDefinition">Resolved module definition referenced by the binding.</param>
    /// <param name="resolvedPayload">Payload selected for this binding before fallback is applied.</param>
    /// <returns>Default bomb payload used as fallback, or null when no fallback is needed.</returns>
    private static BombToolData ResolveFallbackBombData(PowerUpModuleBinding binding,
                                                        PowerUpModuleDefinition moduleDefinition,
                                                        PowerUpModuleData resolvedPayload)
    {
        if (binding == null || !binding.UseOverridePayload)
            return null;

        if (moduleDefinition == null || moduleDefinition.Data == null || ReferenceEquals(moduleDefinition.Data, resolvedPayload))
            return null;

        return moduleDefinition.Data.Bomb;
    }

    /// <summary>
    /// Resolves the stable identifier stored by one legacy active tool definition.
    /// </summary>
    /// <param name="activeTool">Legacy active tool definition being compiled.</param>
    /// <returns>Stable power-up identifier or an empty fixed string when unavailable.</returns>
    private static FixedString64Bytes ResolveLegacyPowerUpId(ActiveToolDefinition activeTool)
    {
        if (activeTool == null || activeTool.CommonData == null || string.IsNullOrWhiteSpace(activeTool.CommonData.PowerUpId))
            return default;

        return new FixedString64Bytes(activeTool.CommonData.PowerUpId.Trim());
    }
    #endregion

    #endregion
}
