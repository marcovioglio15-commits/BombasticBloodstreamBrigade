using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bake-time compiler that resolves EnemyAdvancedPatternPreset into ECS-friendly runtime data.
/// </summary>
public static class EnemyAdvancedPatternBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one advanced pattern preset into movement config and shooter module list.
    /// </summary>
    /// <param name="preset">Preset to compile. Null returns default grunt config.</param>
    /// <returns>Compiled bake result.</returns>
    public static EnemyCompiledPatternBakeResult Compile(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = CreateDefaultResult(preset);

        if (preset == null)
            return result;

        if (preset.ModulesAndPatternsPreset != null)
            return EnemyModulesAndPatternsBakeUtility.Compile(preset);

        int selectedMovementPriority = 0;

        for (int loadoutIndex = 0; loadoutIndex < preset.ActivePatternIds.Count; loadoutIndex++)
        {
            string patternId = preset.ActivePatternIds[loadoutIndex];
            EnemyPatternDefinition pattern = preset.ResolvePatternById(patternId);

            if (pattern == null)
                continue;

            IReadOnlyList<EnemyPatternModuleBinding> bindings = pattern.ModuleBindings;

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                EnemyPatternModuleBinding binding = bindings[bindingIndex];

                if (binding == null)
                    continue;

                if (!binding.IsEnabled)
                    continue;

                EnemyPatternModuleDefinition moduleDefinition = preset.ResolveModuleDefinitionById(binding.ModuleId);

                if (moduleDefinition == null)
                    continue;

                EnemyPatternModulePayloadData resolvedPayload = ResolveBindingPayload(moduleDefinition, binding);

                EnemyPatternModuleKind resolvedModuleKind = ResolveModuleKind(moduleDefinition.ModuleKind);

                switch (resolvedModuleKind)
                {
                    case EnemyPatternModuleKind.Stationary:
                    case EnemyPatternModuleKind.Grunt:
                    case EnemyPatternModuleKind.Wanderer:
                    case EnemyPatternModuleKind.Coward:
                        TryApplyMovementModule(resolvedModuleKind,
                                               resolvedPayload,
                                               ref result.PatternConfig,
                                               ref selectedMovementPriority,
                                               ref result.HasCustomMovement);
                        break;

                    case EnemyPatternModuleKind.Shooter:
                        TryAddShooterModule(resolvedPayload, result.ShooterConfigs, ref result);
                        break;

                    case EnemyPatternModuleKind.PowerUpStealer:
                        TryAddPowerUpStealerModule(resolvedPayload, result.PowerUpStealerConfigs);
                        break;

                    case EnemyPatternModuleKind.DropItems:
                        EnemyDropItemsBakeUtility.TryAppendModule(resolvedPayload, ref result);
                        break;
                }
            }
        }

        return result;
    }
    #endregion

    #region Helpers
    internal static EnemyCompiledPatternBakeResult CreateDefaultResult(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = new EnemyCompiledPatternBakeResult();
        result.PatternConfig = BuildDefaultPatternConfig();
        result.HasCustomMovement = false;
        result.ShooterProjectilePrefab = preset != null ? preset.LegacyShooterProjectilePrefab : null;
        result.ShooterProjectilePoolInitialCapacity = preset != null ? math.max(0, preset.LegacyShooterProjectilePoolInitialCapacity) : 0;
        result.ShooterProjectilePoolExpandBatch = preset != null ? math.max(1, preset.LegacyShooterProjectilePoolExpandBatch) : 1;
        result.HasShooterRuntimeSettings = false;
        result.DropItemsConfig = EnemyDropItemsBakeUtility.CreateDefaultConfig();
        return result;
    }

    internal static EnemyPatternConfig BuildDefaultPatternConfig()
    {
        return EnemyPatternDefaultsUtility.CreatePatternConfig();
    }

    internal static EnemyPatternModulePayloadData ResolveBindingPayload(EnemyPatternModuleDefinition moduleDefinition,
                                                                        EnemyPatternModuleBinding binding)
    {
        if (binding != null && binding.UseOverridePayload && binding.OverridePayload != null)
            return binding.OverridePayload;

        if (moduleDefinition != null && moduleDefinition.Data != null)
            return moduleDefinition.Data;

        return new EnemyPatternModulePayloadData();
    }

    internal static void TryApplyMovementModule(EnemyPatternModuleKind moduleKind,
                                                EnemyPatternModulePayloadData payload,
                                                ref EnemyPatternConfig patternConfig,
                                                ref int selectedPriority,
                                                ref bool hasCustomMovement)
    {
        int candidatePriority = ResolveMovementPriority(moduleKind);

        if (candidatePriority < selectedPriority)
            return;

        selectedPriority = candidatePriority;

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                ApplyStationaryPayload(payload, ref patternConfig);
                hasCustomMovement = true;
                return;

            case EnemyPatternModuleKind.Wanderer:
                ApplyWandererPayload(payload, ref patternConfig);
                hasCustomMovement = true;
                return;

            case EnemyPatternModuleKind.Coward:
                ApplyCowardPayload(payload, ref patternConfig);
                hasCustomMovement = true;
                return;

            default:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Grunt;
                hasCustomMovement = false;
                return;
        }
    }

    internal static int ResolveMovementPriority(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return 3;

            case EnemyPatternModuleKind.Wanderer:
            case EnemyPatternModuleKind.Coward:
                return 2;

            case EnemyPatternModuleKind.Grunt:
                return 1;

            default:
                return 0;
        }
    }

    internal static void ApplyWandererPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        if (payload == null || payload.Wanderer == null)
        {
            patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererBasic;
            return;
        }

        EnemyWandererModuleData wanderer = payload.Wanderer;
        EnemyWandererBasicPayload basic = wanderer.Basic;
        EnemyWandererDvdPayload dvd = wanderer.Dvd;
        EnemyWandererMode resolvedMode = ResolveWandererMode(wanderer.Mode);

        if (basic != null)
        {
            patternConfig.BasicSearchRadius = math.max(0.5f, basic.SearchRadius);
            patternConfig.BasicMinimumTravelDistance = math.max(0f, basic.MinimumTravelDistance);
            patternConfig.BasicMaximumTravelDistance = math.max(patternConfig.BasicMinimumTravelDistance, basic.MaximumTravelDistance);
            patternConfig.BasicArrivalTolerance = math.max(0.05f, basic.ArrivalTolerance);
            patternConfig.BasicWaitCooldownSeconds = math.max(0f, basic.WaitCooldownSeconds);
            patternConfig.BasicCandidateSampleCount = math.clamp(math.max(1, basic.CandidateSampleCount), 1, 32);
            patternConfig.BasicUseInfiniteDirectionSampling = basic.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
            patternConfig.BasicInfiniteDirectionStepDegrees = math.clamp(basic.InfiniteDirectionStepDegrees, 0.5f, 90f);
            patternConfig.BasicUnexploredDirectionPreference = math.max(0f, basic.UnexploredDirectionPreference);
            patternConfig.BasicTowardPlayerPreference = math.max(0f, basic.TowardPlayerPreference);
            patternConfig.BasicMinimumEnemyClearance = math.max(0f, basic.MinimumEnemyClearance);
            patternConfig.BasicTrajectoryPredictionTime = math.max(0f, basic.TrajectoryPredictionTime);
            patternConfig.BasicFreeTrajectoryPreference = math.max(0f, basic.FreeTrajectoryPreference);
            patternConfig.BasicBlockedPathRetryDelay = math.max(0f, basic.BlockedPathRetryDelay);
        }

        if (dvd != null)
        {
            patternConfig.DvdSpeedMultiplier = math.max(0f, dvd.SpeedMultiplier);
            patternConfig.DvdBounceDamping = math.clamp(dvd.BounceDamping, 0f, 1f);
            patternConfig.DvdRandomizeInitialDirection = dvd.RandomizeInitialDirection ? (byte)1 : (byte)0;
            patternConfig.DvdFixedInitialDirectionDegrees = dvd.FixedInitialDirectionDegrees;
            patternConfig.DvdCornerNudgeDistance = math.max(0f, dvd.CornerNudgeDistance);
            patternConfig.DvdIgnoreSteeringAndPriority = dvd.IgnoreSteeringAndPriority ? (byte)1 : (byte)0;
        }

        switch (resolvedMode)
        {
            case EnemyWandererMode.Dvd:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererDvd;
                return;

            default:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererBasic;
                return;
        }
    }

    internal static void ApplyStationaryPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Stationary;
        patternConfig.StationaryFreezeRotation = 1;

        if (payload == null || payload.Stationary == null)
            return;

        patternConfig.StationaryFreezeRotation = payload.Stationary.FreezeRotation ? (byte)1 : (byte)0;
    }

    internal static void ApplyCowardPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Coward;

        if (payload == null || payload.Coward == null)
            return;

        EnemyCowardModuleData coward = payload.Coward;
        patternConfig.BasicSearchRadius = math.max(0.5f, coward.SearchRadius);
        patternConfig.BasicMinimumTravelDistance = math.max(0f, coward.MinimumRetreatDistance);
        patternConfig.BasicMaximumTravelDistance = math.max(patternConfig.BasicMinimumTravelDistance, coward.MaximumRetreatDistance);
        patternConfig.BasicArrivalTolerance = math.max(0.05f, coward.ArrivalTolerance);
        patternConfig.BasicWaitCooldownSeconds = 0f;
        patternConfig.BasicCandidateSampleCount = math.clamp(math.max(1, coward.CandidateSampleCount), 1, 64);
        patternConfig.BasicUseInfiniteDirectionSampling = coward.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
        patternConfig.BasicInfiniteDirectionStepDegrees = math.clamp(coward.InfiniteDirectionStepDegrees, 0.5f, 90f);
        patternConfig.BasicUnexploredDirectionPreference = 0f;
        patternConfig.BasicTowardPlayerPreference = 0f;
        patternConfig.BasicMinimumEnemyClearance = math.max(0f, coward.MinimumEnemyClearance);
        patternConfig.BasicTrajectoryPredictionTime = math.max(0f, coward.TrajectoryPredictionTime);
        patternConfig.BasicFreeTrajectoryPreference = math.lerp(1f, 5f, math.saturate(coward.FreeTrajectoryPreference));
        patternConfig.BasicWaitCooldownSeconds = 0f;
        patternConfig.BasicBlockedPathRetryDelay = math.max(0f, coward.BlockedPathRetryDelay);
        patternConfig.CowardDetectionRadius = math.max(0f, coward.DetectionRadius);
        patternConfig.CowardReleaseDistanceBuffer = math.max(0f, coward.ReleaseDistanceBuffer);
        patternConfig.CowardRetreatDirectionPreference = math.saturate(coward.RetreatDirectionPreference);
        patternConfig.CowardOpenSpacePreference = math.saturate(coward.OpenSpacePreference);
        patternConfig.CowardNavigationPreference = math.saturate(coward.NavigationRetreatPreference);
        patternConfig.CowardPatrolRadius = math.max(0f, coward.PatrolRadius);
        patternConfig.CowardPatrolWaitSeconds = math.max(0f, coward.PatrolWaitSeconds);
        patternConfig.CowardPatrolSpeedMultiplier = math.max(0f, coward.PatrolSpeedMultiplier);
        patternConfig.CowardRetreatSpeedMultiplierFar = math.max(0f, coward.RetreatSpeedMultiplierFar);
        patternConfig.CowardRetreatSpeedMultiplierNear = math.max(patternConfig.CowardRetreatSpeedMultiplierFar,
                                                                  coward.RetreatSpeedMultiplierNear);
    }

    /// <summary>
    /// Copies the short-range dash payload into the short-range dash section of the compiled pattern config.
    /// </summary>
    /// <param name="payload">Resolved module payload for the short-range dash module.</param>
    /// <param name="patternConfig">Mutable compiled pattern config.</param>
    internal static void ApplyShortRangeDashPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.ShortRangeDash;

        if (payload == null || payload.ShortRangeDash == null)
            return;

        EnemyShortRangeDashModuleData shortRangeDash = payload.ShortRangeDash;
        EnemyShortRangeDashAimPayload aim = shortRangeDash.Aim;
        EnemyShortRangeDashRecoveryPayload recovery = shortRangeDash.Recovery;
        EnemyShortRangeDashDistancePayload distance = shortRangeDash.Distance;
        EnemyShortRangeDashPathPayload path = shortRangeDash.Path;

        if (aim != null)
        {
            patternConfig.ShortRangeDashAimDuration = math.max(0f, aim.AimDurationSeconds);
            patternConfig.ShortRangeDashAimMoveSpeedMultiplier = math.max(0f, aim.MoveSpeedMultiplierWhileAiming);
        }

        if (distance != null)
        {
            patternConfig.ShortRangeDashDistanceSource = ResolveShortRangeDashDistanceSource(distance.DistanceSource);
            patternConfig.ShortRangeDashDistanceMultiplier = math.max(0f, distance.PlayerDistanceMultiplier);
            patternConfig.ShortRangeDashDistanceOffset = distance.DistanceOffset;
            patternConfig.ShortRangeDashFixedDistance = math.max(0f, distance.FixedDistance);
            patternConfig.ShortRangeDashMinimumTravelDistance = math.max(0f, distance.MinimumTravelDistance);
            patternConfig.ShortRangeDashMaximumTravelDistance = math.max(patternConfig.ShortRangeDashMinimumTravelDistance,
                                                                        distance.MaximumTravelDistance);
        }

        if (recovery != null)
            patternConfig.ShortRangeDashCooldownSeconds = math.max(0f, recovery.CooldownSeconds);

        if (path == null)
            return;

        patternConfig.ShortRangeDashDuration = math.max(0.01f, path.DashDurationSeconds);
        patternConfig.ShortRangeDashLateralAmplitude = math.max(0f, path.LateralAmplitude);
        patternConfig.ShortRangeDashMirrorMode = ResolveShortRangeDashMirrorMode(path.MirrorMode);
        patternConfig.ShortRangeDashPathSamples = BuildShortRangeDashPathSamples(path.ForwardProgressCurve,
                                                                                 path.LateralOffsetCurve);
    }

    internal static void TryAddShooterModule(EnemyPatternModulePayloadData payload,
                                             List<EnemyShooterConfigElement> shooterConfigs,
                                             ref EnemyCompiledPatternBakeResult result)
    {
        if (shooterConfigs == null)
            return;

        if (payload == null)
            return;

        if (payload.Shooter == null)
            return;

        EnemyShooterModuleData shooterData = payload.Shooter;
        TryAssignShooterRuntimeSettings(shooterData, ref result);
        EnemyShooterProjectilePayload projectilePayload = shooterData.Projectile;

        if (projectilePayload == null)
            return;

        float minimumRange = math.max(0f, shooterData.MinimumRange);
        float maximumRange = math.max(minimumRange, shooterData.MaximumRange);

        EnemyShooterConfigElement shooterConfig = new EnemyShooterConfigElement
        {
            AimPolicy = ResolveShooterAimPolicy(shooterData.AimPolicy),
            MovementPolicy = ResolveShooterMovementPolicy(shooterData.MovementPolicy),
            ShotPattern = ResolveShooterShotPattern(projectilePayload.ShotPattern),
            FireInterval = math.max(0.01f, shooterData.FireInterval),
            BurstCount = math.clamp(math.max(1, shooterData.BurstCount), 1, 64),
            AimWindupSeconds = math.max(0f, shooterData.AimWindupSeconds),
            PreFireStopSeconds = math.max(0f, shooterData.PreFireStopSeconds),
            PostFireStopSeconds = math.max(0f, shooterData.PostFireStopSeconds),
            IntraBurstDelay = math.max(0f, shooterData.IntraBurstDelay),
            UseMinimumRange = shooterData.UseMinimumRange ? (byte)1 : (byte)0,
            MinimumRange = minimumRange,
            UseMaximumRange = shooterData.UseMaximumRange ? (byte)1 : (byte)0,
            MaximumRange = maximumRange,
            ActivationGates = EnemyWeaponInteractionActivationGate.Always,
            MaximumActivationSpeed = 0f,
            RecentlyDamagedWindowSeconds = 0f,
            ProjectilesPerShot = math.clamp(math.max(1, projectilePayload.ProjectilesPerShot), 1, 64),
            SpreadAngleDegrees = math.max(0f, projectilePayload.SpreadAngleDegrees),
            ProjectileSpeed = math.max(0f, projectilePayload.ProjectileSpeed),
            ProjectileDamage = math.max(0f, projectilePayload.ProjectileDamage),
            ProjectileRange = math.max(0f, projectilePayload.ProjectileRange),
            ProjectileLifetime = math.max(0f, projectilePayload.ProjectileLifetime),
            ProjectileExplosionRadius = math.max(0f, projectilePayload.ProjectileExplosionRadius),
            ProjectileScaleMultiplier = math.max(0.01f, projectilePayload.ProjectileScaleMultiplier),
            PenetrationMode = ResolveProjectilePenetrationMode(projectilePayload.PenetrationMode),
            MaxPenetrations = math.max(0, projectilePayload.MaxPenetrations),
            InheritShooterSpeed = projectilePayload.InheritShooterSpeed ? (byte)1 : (byte)0,
            HasElementalPayload = 0,
            ElementalEffect = default,
            ElementalStacksPerHit = 0f
        };

        EnemyShooterElementalPayload elementalPayload = shooterData.Elemental;

        if (elementalPayload != null && elementalPayload.EnableElementalDamage && elementalPayload.StacksPerHit > 0f)
        {
            shooterConfig.HasElementalPayload = 1;
            shooterConfig.ElementalEffect = BuildElementalEffectConfig(elementalPayload.EffectData);
            shooterConfig.ElementalStacksPerHit = math.max(0f, elementalPayload.StacksPerHit);
        }

        shooterConfigs.Add(shooterConfig);
    }

    internal static void TryAssignShooterRuntimeSettings(EnemyShooterModuleData shooterData, ref EnemyCompiledPatternBakeResult result)
    {
        if (result.HasShooterRuntimeSettings)
            return;

        if (shooterData == null)
            return;

        EnemyShooterRuntimeProjectilePayload runtimePayload = shooterData.RuntimeProjectile;

        if (runtimePayload == null)
            return;

        GameObject projectilePrefab = runtimePayload.ProjectilePrefab;

        if (projectilePrefab == null)
            return;

        result.ShooterProjectilePrefab = projectilePrefab;
        result.ShooterProjectilePoolInitialCapacity = math.max(0, runtimePayload.PoolInitialCapacity);
        result.ShooterProjectilePoolExpandBatch = math.max(1, runtimePayload.PoolExpandBatch);
        result.HasShooterRuntimeSettings = true;
    }

    internal static EnemyPatternModuleKind ResolveModuleKind(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
            case EnemyPatternModuleKind.Grunt:
            case EnemyPatternModuleKind.Wanderer:
            case EnemyPatternModuleKind.Coward:
            case EnemyPatternModuleKind.ShortRangeDash:
            case EnemyPatternModuleKind.Shooter:
            case EnemyPatternModuleKind.PowerUpStealer:
            case EnemyPatternModuleKind.DropItems:
                return moduleKind;

            default:
                return EnemyPatternModuleKind.Grunt;
        }
    }

    internal static EnemyWandererMode ResolveWandererMode(EnemyWandererMode mode)
    {
        switch (mode)
        {
            case EnemyWandererMode.Basic:
            case EnemyWandererMode.Dvd:
                return mode;

            default:
                return EnemyWandererMode.Basic;
        }
    }

    internal static EnemyShooterAimPolicy ResolveShooterAimPolicy(EnemyShooterAimPolicy aimPolicy)
    {
        switch (aimPolicy)
        {
            case EnemyShooterAimPolicy.LockOnFireStart:
            case EnemyShooterAimPolicy.ContinuousTracking:
                return aimPolicy;

            default:
                return EnemyShooterAimPolicy.LockOnFireStart;
        }
    }

    internal static EnemyShooterMovementPolicy ResolveShooterMovementPolicy(EnemyShooterMovementPolicy movementPolicy)
    {
        switch (movementPolicy)
        {
            case EnemyShooterMovementPolicy.KeepMoving:
            case EnemyShooterMovementPolicy.StopWhileAiming:
                return movementPolicy;

            default:
                return EnemyShooterMovementPolicy.KeepMoving;
        }
    }

    /// <summary>
    /// Resolves authored Shooter projectile distribution to a supported runtime value.
    /// </summary>
    /// <param name="shotPattern">Authored projectile distribution mode.</param>
    /// <returns>Valid Shooter shot pattern.</returns>
    internal static EnemyShooterShotPattern ResolveShooterShotPattern(EnemyShooterShotPattern shotPattern)
    {
        switch (shotPattern)
        {
            case EnemyShooterShotPattern.ForwardSpread:
            case EnemyShooterShotPattern.RadialBurst:
                return shotPattern;

            default:
                return EnemyShooterShotPattern.ForwardSpread;
        }
    }

    internal static ProjectilePenetrationMode ResolveProjectilePenetrationMode(ProjectilePenetrationMode penetrationMode)
    {
        switch (penetrationMode)
        {
            case ProjectilePenetrationMode.None:
            case ProjectilePenetrationMode.FixedHits:
            case ProjectilePenetrationMode.Infinite:
            case ProjectilePenetrationMode.DamageBased:
                return penetrationMode;

            default:
                return ProjectilePenetrationMode.None;
        }
    }

    /// <summary>
    /// Resolves one legal short-range dash distance source enum value.
    /// </summary>
    /// <param name="distanceSource">Authored distance source candidate.</param>
    /// <returns>Sanitized short-range dash distance source.</returns>
    internal static EnemyShortRangeDashDistanceSource ResolveShortRangeDashDistanceSource(EnemyShortRangeDashDistanceSource distanceSource)
    {
        switch (distanceSource)
        {
            case EnemyShortRangeDashDistanceSource.PlayerDistance:
            case EnemyShortRangeDashDistanceSource.FixedDistance:
                return distanceSource;

            default:
                return EnemyShortRangeDashDistanceSource.PlayerDistance;
        }
    }

    /// <summary>
    /// Resolves one legal short-range dash mirror mode enum value.
    /// </summary>
    /// <param name="mirrorMode">Authored mirror mode candidate.</param>
    /// <returns>Sanitized short-range dash mirror mode.</returns>
    internal static EnemyShortRangeDashMirrorMode ResolveShortRangeDashMirrorMode(EnemyShortRangeDashMirrorMode mirrorMode)
    {
        switch (mirrorMode)
        {
            case EnemyShortRangeDashMirrorMode.Right:
            case EnemyShortRangeDashMirrorMode.Left:
            case EnemyShortRangeDashMirrorMode.Alternate:
            case EnemyShortRangeDashMirrorMode.Random:
                return mirrorMode;

            default:
                return EnemyShortRangeDashMirrorMode.Right;
        }
    }

    internal static ElementalEffectConfig BuildElementalEffectConfig(ElementalEffectDefinitionData definitionData)
    {
        if (definitionData == null)
            return default;

        return new ElementalEffectConfig
        {
            ElementType = definitionData.ElementType,
            EffectKind = definitionData.EffectKind,
            ProcMode = definitionData.ProcMode,
            ReapplyMode = definitionData.ReapplyMode,
            ProcThresholdStacks = math.max(0.1f, definitionData.ProcThresholdStacks),
            MaximumStacks = math.max(0.1f, definitionData.MaximumStacks),
            StackDecayPerSecond = math.max(0f, definitionData.StackDecayPerSecond),
            ConsumeStacksOnProc = definitionData.ConsumeStacksOnProc ? (byte)1 : (byte)0,
            DotDamagePerTick = math.max(0f, definitionData.DotDamagePerTick),
            DotTickInterval = math.max(0.01f, definitionData.DotTickInterval),
            DotDurationSeconds = math.max(0.05f, definitionData.DotDurationSeconds),
            ImpedimentSlowPercentPerStack = math.clamp(definitionData.ImpedimentSlowPercentPerStack, 0f, 100f),
            ImpedimentProcSlowPercent = math.clamp(definitionData.ImpedimentProcSlowPercent, 0f, 100f),
            ImpedimentMaxSlowPercent = math.clamp(definitionData.ImpedimentMaxSlowPercent, 0f, 100f),
            ImpedimentDurationSeconds = math.max(0.05f, definitionData.ImpedimentDurationSeconds)
        };
    }

    /// <summary>
    /// Appends one Power-Up Stealer module config from a resolved payload.
    /// </summary>
    /// <param name="payload">Resolved module payload.</param>
    /// <param name="stealerConfigs">Target config list.</param>
    internal static void TryAddPowerUpStealerModule(EnemyPatternModulePayloadData payload,
                                                    List<EnemyPowerUpStealerConfigElement> stealerConfigs)
    {
        if (stealerConfigs == null)
            return;

        if (payload == null || payload.PowerUpStealer == null)
            return;

        EnemyPowerUpStealerModuleData stealerData = payload.PowerUpStealer;
        stealerConfigs.Add(new EnemyPowerUpStealerConfigElement
        {
            TriggerMode = ResolvePowerUpStealTriggerMode(stealerData.TriggerMode),
            TargetKind = ResolvePowerUpStealTargetKind(stealerData.TargetKind),
            ActiveTargetBiasPercent = math.clamp(stealerData.ActiveTargetBiasPercent, 0f, 100f),
            UseMinimumRange = 0,
            MinimumRange = 0f,
            UseMaximumRange = 0,
            MaximumRange = 0f,
            ExclusiveLookDirectionControl = 0,
            ActivationGates = EnemyWeaponInteractionActivationGate.Always,
            MaximumActivationSpeed = 0f,
            RecentlyDamagedWindowSeconds = 0f,
            UseDamageRecovery = stealerData.RecoverAfterDamageTakenPercent ? (byte)1 : (byte)0,
            DamageRecoveryPercent = math.max(0f, stealerData.RecoveryDamageTakenPercent),
            UseTimedDamageRecovery = stealerData.RecoverAfterDamageWindow ? (byte)1 : (byte)0,
            TimedDamageRecoveryPercent = math.max(0f, stealerData.RecoveryDamageWindowPercent),
            TimedDamageRecoverySeconds = math.max(0f, stealerData.RecoveryDamageWindowSeconds)
        });
    }

    /// <summary>
    /// Resolves one legal Power-Up Stealer trigger enum value.
    /// </summary>
    /// <param name="triggerMode">Authored trigger mode.</param>
    /// <returns>Supported trigger mode.</returns>
    internal static EnemyPowerUpStealTriggerMode ResolvePowerUpStealTriggerMode(EnemyPowerUpStealTriggerMode triggerMode)
    {
        switch (triggerMode)
        {
            case EnemyPowerUpStealTriggerMode.OnModuleActivation:
            case EnemyPowerUpStealTriggerMode.OnFirstPlayerHit:
            case EnemyPowerUpStealTriggerMode.OnEveryPlayerHit:
                return triggerMode;

            default:
                return EnemyPowerUpStealTriggerMode.OnFirstPlayerHit;
        }
    }

    /// <summary>
    /// Resolves one legal Power-Up Stealer target enum value.
    /// </summary>
    /// <param name="targetKind">Authored target kind.</param>
    /// <returns>Supported target kind.</returns>
    internal static EnemyPowerUpStealTargetKind ResolvePowerUpStealTargetKind(EnemyPowerUpStealTargetKind targetKind)
    {
        switch (targetKind)
        {
            case EnemyPowerUpStealTargetKind.Active:
            case EnemyPowerUpStealTargetKind.Passive:
            case EnemyPowerUpStealTargetKind.ActiveOrPassive:
                return targetKind;

            default:
                return EnemyPowerUpStealTargetKind.ActiveOrPassive;
        }
    }

    /// <summary>
    /// Samples authored dash curves into one compact local path stored directly inside the runtime config.
    /// </summary>
    /// <param name="forwardProgressCurve">Authored forward-progression curve.</param>
    /// <param name="lateralOffsetCurve">Authored lateral-offset curve.</param>
    /// <returns>Fixed-size sampled path where x is normalized lateral offset and y is normalized forward progress.</returns>
    private static FixedList128Bytes<float2> BuildShortRangeDashPathSamples(AnimationCurve forwardProgressCurve,
                                                                            AnimationCurve lateralOffsetCurve)
    {
        FixedList128Bytes<float2> pathSamples = default;
        AnimationCurve resolvedForwardCurve = forwardProgressCurve ?? new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
        AnimationCurve resolvedLateralCurve = lateralOffsetCurve ?? new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        int sampleCount = 12;

        if (sampleCount <= 1)
            return EnemyPatternDefaultsUtility.BuildDefaultShortRangeDashPathSamples();

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float normalizedTime = sampleCount <= 1
                ? 0f
                : (float)sampleIndex / (sampleCount - 1);
            float normalizedForward = math.saturate(resolvedForwardCurve.Evaluate(normalizedTime));
            float normalizedLateral = resolvedLateralCurve.Evaluate(normalizedTime);
            pathSamples.Add(new float2(normalizedLateral, normalizedForward));
        }

        if (pathSamples.Length > 0)
            pathSamples[0] = new float2(0f, 0f);

        if (pathSamples.Length > 1)
        {
            int lastIndex = pathSamples.Length - 1;
            float2 lastPoint = pathSamples[lastIndex];
            lastPoint.y = 1f;
            pathSamples[lastIndex] = lastPoint;
        }

        return pathSamples;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores bake-time compiled pattern outputs before converting them to ECS components.
/// </summary>
public sealed class EnemyCompiledPatternBakeResult
{
    #region Fields
    public EnemyPatternConfig PatternConfig;
    public bool HasCustomMovement;
    public GameObject ShooterProjectilePrefab;
    public int ShooterProjectilePoolInitialCapacity;
    public int ShooterProjectilePoolExpandBatch;
    public bool HasShooterRuntimeSettings;
    public EnemyDropItemsConfig DropItemsConfig;
    public readonly List<EnemyShooterConfigElement> ShooterConfigs = new List<EnemyShooterConfigElement>();
    public readonly List<EnemyPowerUpStealerConfigElement> PowerUpStealerConfigs = new List<EnemyPowerUpStealerConfigElement>();
    public readonly List<EnemyCompiledExperienceDropModule> ExperienceDropModules = new List<EnemyCompiledExperienceDropModule>();
    public readonly List<EnemyCompiledExperienceDropDefinition> ExperienceDropDefinitions = new List<EnemyCompiledExperienceDropDefinition>();
    public readonly List<EnemyCompiledExtraComboPointsModule> ExtraComboPointsModules = new List<EnemyCompiledExtraComboPointsModule>();
    public readonly List<EnemyCompiledExtraComboPointsCondition> ExtraComboPointsConditions = new List<EnemyCompiledExtraComboPointsCondition>();
    #endregion
}
