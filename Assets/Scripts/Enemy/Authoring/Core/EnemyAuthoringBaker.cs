using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bakes EnemyAuthoring data into ECS enemy components.
/// </summary>
public sealed class EnemyAuthoringBaker : Baker<EnemyAuthoring>
{
    #region Methods

    #region Bake
    public override void Bake(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return;

        DeclarePresetDependencies(authoring);
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        float bakedBodyRadiusX = math.max(0.05f, authoring.BodyRadiusX);
        float bakedBodyRadiusZ = math.max(0.05f, authoring.BodyRadiusZ);
        float bakedBodyRadius = math.max(bakedBodyRadiusX, bakedBodyRadiusZ);
        bool rotateHitCenterOffset = ShouldRotateHitCenterOffset(authoring);
        float2 bakedHitCenterOffsetXZ = ResolveHitCenterOffsetXZ(authoring, rotateHitCenterOffset);

        AddComponent(entity, new EnemyData
        {
            MoveSpeed = math.max(0f, authoring.MoveSpeed),
            MaxSpeed = math.max(0f, authoring.MaxSpeed),
            Acceleration = math.max(0f, authoring.Acceleration),
            Deceleration = math.max(0f, authoring.Deceleration),
            SpawnInactivityTime = math.max(0f, authoring.InactivityTime),
            RotationSpeedDegreesPerSecond = authoring.RotationSpeedDegreesPerSecond,
            SeparationRadius = math.max(0.1f, authoring.SeparationRadius),
            SeparationWeight = math.max(0f, authoring.SeparationWeight),
            BodyRadius = bakedBodyRadius,
            BodyRadiusX = bakedBodyRadiusX,
            BodyRadiusZ = bakedBodyRadiusZ,
            HitCenterOffsetXZ = bakedHitCenterOffsetXZ,
            RotateHitCenterOffset = rotateHitCenterOffset ? (byte)1 : (byte)0,
            MinimumWallDistance = math.max(0f, authoring.MinimumWallDistance),
            PriorityTier = math.clamp(authoring.PriorityTier, -128, 128),
            SteeringAggressiveness = math.clamp(authoring.SteeringAggressiveness, 0f, 2.5f),
            DisablePlayerKnockback = authoring.DisablePlayerKnockback ? (byte)1 : (byte)0,
            ContactDamageEnabled = authoring.ContactDamageEnabled ? (byte)1 : (byte)0,
            ContactRadius = math.max(0f, authoring.ContactRadius),
            ContactAmountPerTick = math.max(0f, authoring.ContactAmountPerTick),
            ContactTickInterval = math.max(0.01f, authoring.ContactTickInterval),
            AreaDamageEnabled = authoring.AreaDamageEnabled ? (byte)1 : (byte)0,
            AreaRadius = math.max(0f, authoring.AreaRadius),
            AreaAmountPerTickPercent = math.max(0f, authoring.AreaAmountPerTickPercent),
            AreaTickInterval = math.max(0.01f, authoring.AreaTickInterval)
        });
        AddComponent(entity, EnemyTacticalNavigationBakeUtility.BuildConfig(authoring));
        AddComponent(entity, EnemyPatternDefaultsUtility.CreateNavigationRuntimeState());

        float bakedHealth = math.max(1f, authoring.MaxHealth);
        float bakedShield = math.max(0f, authoring.MaxShield);

        AddComponent(entity, new EnemyHealth
        {
            Current = bakedHealth,
            Max = bakedHealth,
            CurrentShield = bakedShield,
            MaxShield = bakedShield
        });

        AddComponent(entity, new EnemyRuntimeState
        {
            Velocity = float3.zero,
            ContactDamageCooldown = 0f,
            AreaDamageCooldown = 0f,
            SpawnInactivityTimer = 0f,
            LifetimeSeconds = 0f,
            FirstDamageLifetimeSeconds = 0f,
            LastDamageLifetimeSeconds = 0f,
            SpawnVersion = 0u,
            HasTakenDamage = 0
        });
        AddComponent(entity, new EnemyKnockbackState
        {
            Velocity = float3.zero,
            RemainingTime = 0f
        });
        AddComponent<EnemySpawnInactivityLock>(entity);
        SetComponentEnabled<EnemySpawnInactivityLock>(entity, false);
        AddComponent<EnemySpawnWarningState>(entity);
        SetComponentEnabled<EnemySpawnWarningState>(entity, false);

        EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.Compile(authoring.AdvancedPatternPreset);
        EnemyBossPatternPreset bossPatternPreset = authoring.BossPatternPreset;
        EnemyCompiledBossPatternBakeResult compiledBossPattern = null;

        if (bossPatternPreset != null)
        {
            compiledBossPattern = EnemyBossPatternBakeUtility.Compile(bossPatternPreset,
                                                                      authoring.OffensiveEngagementFeedbackSettings,
                                                                      minionPrefab => GetEntity(minionPrefab, TransformUsageFlags.Dynamic));

            if (compiledBossPattern != null)
                compiledPattern = compiledBossPattern.InitialPattern;
        }

        EnemyPatternConfig resolvedPatternConfig = compiledPattern.PatternConfig;
        bool shouldBakeManagedVfxRuntime = ShouldBakeEnemyManagedVfxRuntime(authoring,
                                                                            compiledPattern,
                                                                            in resolvedPatternConfig);
        DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> managedVfxPrefabBindings = default;

        if (shouldBakeManagedVfxRuntime)
            managedVfxPrefabBindings = BakeEnemyManagedVfxRuntime(entity);

        TryBakeAcidTrailVfxRuntime(authoring,
                                   compiledPattern,
                                   ref resolvedPatternConfig,
                                   managedVfxPrefabBindings,
                                   shouldBakeManagedVfxRuntime);

        AddComponent(entity, resolvedPatternConfig);
        AddComponent(entity, EnemyPatternDefaultsUtility.CreatePatternRuntimeState());
        AddBuffer<EnemyAcidTrailSegmentElement>(entity);
        AddComponent(entity, new EnemyShooterControlState
        {
            MovementLocked = 0,
            AimDirection = float3.zero,
            HasAimDirection = 0
        });

        if (compiledPattern.HasCustomMovement)
            AddComponent<EnemyCustomPatternMovementTag>(entity);

        DynamicBuffer<EnemyShooterConfigElement> shooterConfigs = AddBuffer<EnemyShooterConfigElement>(entity);
        DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = AddBuffer<EnemyShooterRuntimeElement>(entity);
        DynamicBuffer<EnemyBombardierConfigElement> bombardierConfigs = AddBuffer<EnemyBombardierConfigElement>(entity);
        DynamicBuffer<EnemyBombardierRuntimeElement> bombardierRuntime = AddBuffer<EnemyBombardierRuntimeElement>(entity);
        AddBuffer<EnemyBombardierLaunchRequest>(entity);
        DynamicBuffer<EnemyPowerUpStealerConfigElement> stealerConfigs = AddBuffer<EnemyPowerUpStealerConfigElement>(entity);
        DynamicBuffer<EnemyPowerUpStealerRuntimeElement> stealerRuntime = AddBuffer<EnemyPowerUpStealerRuntimeElement>(entity);
        DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs = AddBuffer<EnemyOffensiveEngagementConfigElement>(entity);

        for (int shooterIndex = 0; shooterIndex < compiledPattern.ShooterConfigs.Count; shooterIndex++)
        {
            shooterConfigs.Add(compiledPattern.ShooterConfigs[shooterIndex]);
            shooterRuntime.Add(new EnemyShooterRuntimeElement
            {
                NextBurstTimer = 0f,
                NextShotInBurstTimer = 0f,
                PostFireStopTimer = 0f,
                RemainingBurstShots = 0,
                ShotsFiredInCurrentBurst = 0,
                BurstWindupDurationSeconds = 0f,
                IsPlayerInRange = 0,
                LockedAimDirection = float3.zero,
                HasLockedAimDirection = 0
            });
        }

        for (int bombardierIndex = 0; bombardierIndex < compiledPattern.BombardierConfigs.Count; bombardierIndex++)
        {
            bombardierConfigs.Add(compiledPattern.BombardierConfigs[bombardierIndex]);
            bombardierRuntime.Add(CreateDefaultBombardierRuntime());
        }

        for (int stealerIndex = 0; stealerIndex < compiledPattern.PowerUpStealerConfigs.Count; stealerIndex++)
        {
            stealerConfigs.Add(compiledPattern.PowerUpStealerConfigs[stealerIndex]);
            int runtimeIndex = stealerRuntime.Length;
            stealerRuntime.ResizeUninitialized(runtimeIndex + 1);
            ref EnemyPowerUpStealerRuntimeElement runtime = ref stealerRuntime.ElementAt(runtimeIndex);
            EnemyPowerUpStealerRuntimeDefaultsUtility.InitializeDefault(ref runtime);
        }

        AddComponent(entity, new EnemyPowerUpStealerVisualState
        {
            HasStolenPowerUp = 0,
            PowerUpId = default,
            StolenKind = PlayerPowerUpUnlockKind.Active
        });

        if (ShouldBakeShooterRuntime(compiledPattern))
        {
            TryBakeShooterRuntime(authoring, entity, compiledPattern);
        }

        if (ShouldBakeBombardierRuntime(compiledPattern))
        {
            TryBakeBombardierRuntime(authoring,
                                     entity,
                                     compiledPattern,
                                     managedVfxPrefabBindings,
                                     shouldBakeManagedVfxRuntime);
        }

        TryBakeDropItemsRuntime(authoring,
                                entity,
                                compiledPattern,
                                compiledBossPattern != null && compiledBossPattern.BossDropExtractionEnabled);

        if (compiledBossPattern != null)
            AppendInitialBossOffensiveEngagementConfigs(compiledBossPattern, offensiveEngagementConfigs);
        else
            EnemyOffensiveEngagementBakeUtility.AppendConfigs(authoring, offensiveEngagementConfigs);

        TryBakeBossRuntime(authoring, entity, compiledBossPattern);

        EnemyVisualMode bakedVisualMode = ResolveBakedVisualMode(authoring, out Animator resolvedAnimatorComponent);

        AddComponent(entity, new EnemyVisualConfig
        {
            Mode = bakedVisualMode,
            AnimationSpeed = math.max(0f, authoring.VisualAnimationSpeed),
            GpuLoopDuration = math.max(0.05f, authoring.GpuAnimationLoopDuration),
            MaxVisibleDistance = math.max(0f, authoring.MaxVisibleDistance),
            VisibleDistanceHysteresis = math.max(0f, authoring.VisibleDistanceHysteresis),
            UseDistanceCulling = authoring.EnableDistanceCulling ? (byte)1 : (byte)0
        });
        AddComponent(entity, BuildGroundIndicatorConfig(authoring));
        AddComponent(entity, new OutlineVisualConfig
        {
            Enabled = authoring.EnableOutline ? (byte)1 : (byte)0,
            Thickness = math.max(0f, authoring.OutlineThickness),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.OutlineColor)
        });

        Entity hitVfxPrefabEntity = ResolveHitVfxPrefabEntity(authoring);
        Vector3 hitVfxSpawnOffset = authoring.HitVfxSpawnOffset;
        AddComponent(entity, new EnemyHitVfxConfig
        {
            PrefabEntity = hitVfxPrefabEntity,
            Prefab = authoring.HitVfxPrefab,
            SpawnOffset = new float3(hitVfxSpawnOffset.x, hitVfxSpawnOffset.y, hitVfxSpawnOffset.z),
            LifetimeSeconds = math.max(0.05f, authoring.HitVfxLifetimeSeconds),
            ScaleMultiplier = math.max(0.01f, authoring.HitVfxScaleMultiplier)
        });

        Entity spawnVfxPrefabEntity = ResolveSpawnVfxPrefabEntity(authoring);
        Vector3 spawnVfxSpawnOffset = authoring.SpawnVfxSpawnOffset;
        AddComponent(entity, new EnemySpawnVfxConfig
        {
            PrefabEntity = spawnVfxPrefabEntity,
            Prefab = authoring.SpawnVfxPrefab,
            Timing = ResolveSpawnVfxTiming(authoring.SpawnVfxTiming),
            SpawnOffset = new float3(spawnVfxSpawnOffset.x, spawnVfxSpawnOffset.y, spawnVfxSpawnOffset.z),
            LifetimeSeconds = math.max(0.05f, authoring.SpawnVfxLifetimeSeconds),
            ScaleMultiplier = math.max(0.01f, authoring.SpawnVfxScaleMultiplier)
        });
        AddComponent(entity, new EnemySpawnVfxRuntimeState
        {
            WarningVfxQueued = 0
        });
        TryBakeSpawnVfxRuntime(managedVfxPrefabBindings,
                               shouldBakeManagedVfxRuntime,
                               spawnVfxPrefabEntity,
                               authoring.SpawnVfxPrefab);

        Entity deathVfxPrefabEntity = ResolveDeathVfxPrefabEntity(authoring);
        Vector3 deathVfxSpawnOffset = authoring.DeathVfxSpawnOffset;
        EnemyDeathDebrisColorPalette deathDebrisPalette = EnemyVisualColorSamplingUtility.ResolveDeathDebrisPalette(authoring);
        AddComponent(entity, new EnemyDeathVfxConfig
        {
            PrefabEntity = deathVfxPrefabEntity,
            Prefab = deathVfxPrefabEntity != Entity.Null ? authoring.DeathVfxPrefab : null,
            SpawnOffset = new float3(deathVfxSpawnOffset.x, deathVfxSpawnOffset.y, deathVfxSpawnOffset.z),
            LifetimeSeconds = math.max(0.05f, authoring.DeathVfxLifetimeSeconds),
            ScaleMultiplier = math.max(0.01f, authoring.DeathVfxScaleMultiplier),
            HasDebrisColorOverride = deathVfxPrefabEntity != Entity.Null ? (byte)1 : (byte)0,
            DebrisColor = deathDebrisPalette.PrimaryColor,
            SecondaryDebrisColor = deathDebrisPalette.SecondaryColor,
            DebrisColorCount = deathDebrisPalette.ColorCount,
            DebrisParticleChildName = NormalizeFixedString64(authoring.DeathDebrisParticleChildName)
        });
        TryBakeDeathVfxRuntime(managedVfxPrefabBindings,
                               shouldBakeManagedVfxRuntime,
                               deathVfxPrefabEntity,
                               authoring.DeathVfxPrefab);

        TryBakeEnemyProjectileVfxRuntime(authoring,
                                         entity,
                                         managedVfxPrefabBindings,
                                         shouldBakeManagedVfxRuntime);

        Entity deathPuddlePrefabEntity = ResolveDeathPuddlePrefabEntity(authoring);
        AddComponent(entity, EnemyVisualFeedbackBakeUtility.BuildDeathPuddleConfig(authoring.DeathPuddleSettings,
                                                                                    deathPuddlePrefabEntity,
                                                                                    in deathDebrisPalette));

        AddComponent(entity, BuildProjectileOffscreenWarningConfig(authoring));
        TryBakeProjectileOffscreenWarningManagedConfig(authoring, entity);

        float4 damageFlashColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.DamageFlashColor);
        AddComponent(entity, new DamageFlashConfig
        {
            FlashColor = damageFlashColor,
            DurationSeconds = math.max(0f, authoring.DamageFlashDurationSeconds),
            MaximumBlend = math.saturate(authoring.DamageFlashMaximumBlend)
        });
        AddComponent(entity, new DamageFlashState
        {
            RemainingSeconds = 0f,
            AppliedBlend = 0f
        });
        AddComponent(entity, EnemyVisualFeedbackBakeUtility.BuildElasticHitConfig(authoring.ElasticHitSettings));
        AddComponent(entity, new EnemyElasticHitState
        {
            RemainingSeconds = 0f,
            LastTriggerTime = -1000f,
            DirectionWorld = new float3(0f, 0f, 1f)
        });
        AddComponent<EnemyElasticHitActive>(entity);
        SetComponentEnabled<EnemyElasticHitActive>(entity, false);
        AddComponent(entity, new EnemyVisualFlashPresentationState
        {
            AppliedBlend = 0f,
            AppliedColor = damageFlashColor,
            OffensiveEngagementColor = damageFlashColor,
            OffensiveEngagementBlend = 0f,
            OffensiveEngagementFadeOutSeconds = 0f,
            HasProtectedEngagementSource = 0,
            ProtectedEngagementSource = EnemyOffensiveEngagementTriggerSource.CoreMovement
        });
        AddComponent(entity, EnemyVisualFeedbackBakeUtility.BuildFaceFlipbookConfig(authoring.FaceFlipbookSettings));
        AddComponent(entity, CreateDefaultFaceFlipbookState());
        BakeDamageFlashRenderTargets(authoring, entity);

        AddComponent(entity, new EnemyVisualRuntimeState
        {
            AnimationTime = 0f,
            LastSquaredDistanceToPlayer = 0f,
            IsVisible = 1,
            CompanionInitialized = 0,
            AppliedVisibilityPriorityTier = int.MinValue
        });

        switch (bakedVisualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                AddComponentObject(entity, resolvedAnimatorComponent);
                AddComponent<EnemyVisualCompanionAnimator>(entity);
                break;

            default:
                AddComponent<EnemyVisualGpuBaked>(entity);
                break;
        }

        EnemyOffensiveEngagementBillboardView resolvedBillboardView = ResolveOffensiveEngagementBillboardView(authoring);

        if (resolvedBillboardView != null)
        {
            resolvedBillboardView.SyncPresetSources(authoring);
            AddComponentObject(entity, resolvedBillboardView);
        }

        EnemyGroundIndicatorView resolvedGroundIndicatorView = ResolveGroundIndicatorView(authoring);

        if (resolvedGroundIndicatorView != null)
            AddComponentObject(entity, resolvedGroundIndicatorView);

        AddComponent(entity, new EnemyOwnerSpawner
        {
            SpawnerEntity = Entity.Null
        });
        AddComponent(entity, new EnemyOwnerPool
        {
            PoolEntity = Entity.Null
        });
        AddComponent(entity, new EnemyWaveOwner
        {
            WaveIndex = -1
        });

        AddComponent(entity, new EnemyElementalRuntimeState
        {
            SlowPercent = 0f
        });
        AddBuffer<EnemyElementStackElement>(entity);

        Entity anchorEntity = Entity.Null;

        if (authoring.ElementalVfxAnchor != null)
        {
            anchorEntity = GetEntity(authoring.ElementalVfxAnchor, TransformUsageFlags.Dynamic);
        }

        AddComponent(entity, new EnemyElementalVfxAnchor
        {
            AnchorEntity = anchorEntity
        });

        AddComponent<EnemyActive>(entity);
        SetComponentEnabled<EnemyActive>(entity, false);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether this enemy needs projectile runtime buffers during bake.
    /// Boss pattern presets may start with an empty active shooter list while still owning weapon candidates that are applied later.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern result produced from advanced or boss pattern presets.</param>
    /// <returns>True when shooter configs or deferred shooter runtime settings require projectile ECS support.</returns>
    private static bool ShouldBakeShooterRuntime(EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (compiledPattern == null)
            return false;

        return compiledPattern.ShooterConfigs.Count > 0 || compiledPattern.HasShooterRuntimeSettings;
    }

    /// <summary>
    /// Normalizes authored shadow coverage enum values before they are written into ECS.
    /// </summary>
    /// <param name="coverageMode">Authoring enum value resolved from the visual preset.</param>
    /// <returns>Supported coverage mode used by runtime footprint presentation.</returns>
    private static EnemyShadowCoverageMode ResolveShadowCoverageMode(EnemyShadowCoverageMode coverageMode)
    {
        switch (coverageMode)
        {
            case EnemyShadowCoverageMode.ShadowOnly:
            case EnemyShadowCoverageMode.ShadowAndSpatialUi:
                return coverageMode;

            default:
                return EnemyShadowCoverageMode.ShadowOnly;
        }
    }

    /// <summary>
    /// Normalizes authored shadow projection enum values before they are written into ECS.
    /// </summary>
    /// <param name="projectionMode">Authoring enum value resolved from the visual preset.</param>
    /// <returns>Supported projection mode used by runtime ground-shadow presentation.</returns>
    private static GroundShadowProjectionMode ResolveGroundShadowProjectionMode(GroundShadowProjectionMode projectionMode)
    {
        switch (projectionMode)
        {
            case GroundShadowProjectionMode.RaisedQuad:
            case GroundShadowProjectionMode.ProjectOntoGround:
                return projectionMode;

            default:
                return GroundShadowProjectionMode.RaisedQuad;
        }
    }

    /// <summary>
    /// Resolves whether this enemy needs Bombardier runtime prefab support during bake.
    /// Boss pattern presets may start with an empty active Bombardier list while still owning weapon candidates that are applied later.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern result produced from advanced or boss pattern presets.</param>
    /// <returns>True when Bombardier configs or deferred runtime settings require bomb ECS support.</returns>
    private static bool ShouldBakeBombardierRuntime(EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (compiledPattern == null)
            return false;

        return compiledPattern.BombardierConfigs.Count > 0 || compiledPattern.HasBombardierRuntimeSettings;
    }

    /// <summary>
    /// Resolves whether this enemy needs managed VFX request buffers for enemy-authored one-shot visuals.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used to resolve visual-preset spawn VFX needs.</param>
    /// <param name="compiledPattern">Compiled pattern result produced from advanced or boss pattern presets.</param>
    /// <param name="patternConfig">Resolved pattern config before it is written to the enemy entity.</param>
    /// <returns>True when at least one enemy module has an assigned managed VFX prefab.</returns>
    private static bool ShouldBakeEnemyManagedVfxRuntime(EnemyAuthoring authoring,
                                                         EnemyCompiledPatternBakeResult compiledPattern,
                                                         in EnemyPatternConfig patternConfig)
    {
        if (authoring != null)
        {
            if (authoring.SpawnVfxPrefab != null || authoring.DeathVfxPrefab != null)
                return true;

            if (authoring.BulletHitVfxPrefab != null)
                return true;

            if (authoring.BulletDeathVfx != null && authoring.BulletDeathVfx.HasAnyPrefab)
                return true;
        }

        if (compiledPattern == null)
            return false;

        if (compiledPattern.BombardierExplosionVfxPrefab != null)
            return true;

        return patternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererAcid &&
               compiledPattern.AcidTrailSegmentVfxPrefab != null;
    }

    /// <summary>
    /// Creates a clean Bombardier runtime state for a freshly baked module.
    /// </summary>
    /// <returns>Default Bombardier runtime element.</returns>
    private static EnemyBombardierRuntimeElement CreateDefaultBombardierRuntime()
    {
        return new EnemyBombardierRuntimeElement
        {
            NextBurstTimer = 0f,
            NextBombInBurstTimer = 0f,
            PostLaunchStopTimer = 0f,
            RemainingBurstLaunches = 0,
            LaunchesCompletedInCurrentBurst = 0,
            BurstWindupDurationSeconds = 0f,
            IsPlayerInReach = 0,
            IsLaunchAllowed = 0,
            LockedTargetPosition = float3.zero,
            HasLockedTargetPosition = 0,
            RandomState = 0u
        };
    }

    /// <summary>
    /// Creates a clean face flipbook runtime state for freshly baked and pooled enemies.
    /// </summary>
    /// <returns>Default face flipbook runtime state.</returns>
    private static EnemyFaceFlipbookStateData CreateDefaultFaceFlipbookState()
    {
        return new EnemyFaceFlipbookStateData
        {
            CurrentState = EnemyFaceFlipbookState.Idle,
            AttackRemainingSeconds = 0f,
            DamageRemainingSeconds = 0f,
            IdlePlaybackPhaseSeconds = 0f,
            AttackPlaybackPhaseSeconds = 0f,
            DamagePlaybackPhaseSeconds = 0f,
            LastObservedDamageLifetimeSeconds = 0f,
            HasObservedDamage = 0,
            WasEngagementActive = 0
        };
    }

    /// <summary>
    /// Keeps the active boss offensive engagement buffer empty until the first runtime pattern extraction.
    /// </summary>
    /// <param name="compiledBossPattern">Compiled boss pattern data.</param>
    /// <param name="offensiveEngagementConfigs">Active target buffer populated during bake.</param>
    private static void AppendInitialBossOffensiveEngagementConfigs(EnemyCompiledBossPatternBakeResult compiledBossPattern,
                                                                    DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs)
    {
        if (compiledBossPattern == null)
            return;
    }

    /// <summary>
    /// Writes boss-specific ECS buffers and HUD configuration when a Boss Pattern Preset is assigned.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <param name="entity">Enemy entity being baked.</param>
    /// <param name="compiledBossPattern">Compiled boss data, or null for normal enemies.</param>
    private void TryBakeBossRuntime(EnemyAuthoring authoring,
                                    Entity entity,
                                    EnemyCompiledBossPatternBakeResult compiledBossPattern)
    {
        if (authoring == null || compiledBossPattern == null)
            return;

        AddComponent<EnemyBossTag>(entity);
        AddComponent(entity, new EnemyBossPatternExtractionConfig
        {
            HasCustomMovement = compiledBossPattern.InitialPattern.HasCustomMovement ? (byte)1 : (byte)0,
            RerollWhenCurrentPatternBecomesInvalid = compiledBossPattern.RerollWhenCurrentPatternBecomesInvalid ? (byte)1 : (byte)0,
            UseElapsedIntervalExtraction = compiledBossPattern.UseElapsedIntervalExtraction ? (byte)1 : (byte)0,
            UseMissingHealthStepExtraction = compiledBossPattern.UseMissingHealthStepExtraction ? (byte)1 : (byte)0,
            UseTravelledDistanceExtraction = compiledBossPattern.UseTravelledDistanceExtraction ? (byte)1 : (byte)0,
            UseDamageWindowExtraction = compiledBossPattern.UseDamageWindowExtraction ? (byte)1 : (byte)0,
            FirstShooterConfigIndex = 0,
            ShooterConfigCount = 0,
            FirstBombardierConfigIndex = 0,
            BombardierConfigCount = 0,
            FirstOffensiveEngagementConfigIndex = 0,
            OffensiveEngagementConfigCount = 0,
            PlayerDistanceCondition = compiledBossPattern.PlayerDistanceCondition,
            MinimumSecondsBetweenExtractions = compiledBossPattern.MinimumSecondsBetweenExtractions,
            ElapsedIntervalSeconds = compiledBossPattern.ElapsedIntervalSeconds,
            MissingHealthStepPercent = compiledBossPattern.MissingHealthStepPercent,
            TravelledDistanceSinceLastExtraction = compiledBossPattern.TravelledDistanceSinceLastExtraction,
            PlayerDistanceThreshold = compiledBossPattern.PlayerDistanceThreshold,
            PlayerDistanceHoldSeconds = compiledBossPattern.PlayerDistanceHoldSeconds,
            DamageWindowSeconds = compiledBossPattern.DamageWindowSeconds,
            DamageThreshold = compiledBossPattern.DamageThreshold,
            PatternConfig = compiledBossPattern.InitialPattern.PatternConfig
        });
        AddComponent(entity, new EnemyBossPatternRuntimeState
        {
            ActiveInteractionIndex = -2,
            ElapsedSeconds = 0f,
            ActiveInteractionElapsedSeconds = 0f,
            ExtractionElapsedSeconds = 0f,
            TravelledDistance = 0f,
            DistanceSinceLastExtraction = 0f,
            LastExtractionMissingHealthPercent = 0f,
            PlayerDistanceHoldSeconds = 0f,
            DamageWindowElapsedSeconds = 0f,
            DamageWindowAccumulated = 0f,
            PreviousObservedDurability = 0f,
            LastPosition = float3.zero,
            LastObservedDamageLifetimeSeconds = 0f,
            Initialized = 0
        });
        EnemyOffensiveEngagementFeedbackSettings patternChangeFeedbackSettings =
            EnemyAuthoringPresetResolverUtility.ResolveBossPatternChangeFeedbackSettings(authoring.MasterPreset,
                                                                                        authoring.VisualPreset);
        AddComponent(entity, EnemyOffensiveEngagementBakeUtility.CreateBossPatternChangeFeedbackConfig(patternChangeFeedbackSettings));
        AddComponent(entity, new EnemyBossPatternChangeFeedbackState
        {
            ElapsedSeconds = 0f,
            RemainingSeconds = 0f,
            DisplayedBlend = 0f,
            DisplayedColor = float4.zero,
            FadeOutSeconds = 0f
        });

        DynamicBuffer<EnemyBossPatternInteractionElement> interactionBuffer = AddBuffer<EnemyBossPatternInteractionElement>(entity);

        for (int interactionIndex = 0; interactionIndex < compiledBossPattern.Interactions.Count; interactionIndex++)
            interactionBuffer.Add(compiledBossPattern.Interactions[interactionIndex]);

        DynamicBuffer<EnemyBossPatternModuleExtractionElement> moduleExtractionBuffer = AddBuffer<EnemyBossPatternModuleExtractionElement>(entity);

        for (int extractionIndex = 0; extractionIndex < compiledBossPattern.ModuleExtractions.Count; extractionIndex++)
            moduleExtractionBuffer.Add(compiledBossPattern.ModuleExtractions[extractionIndex]);

        DynamicBuffer<EnemyBossPatternModuleCandidateElement> moduleCandidateBuffer = AddBuffer<EnemyBossPatternModuleCandidateElement>(entity);

        for (int candidateIndex = 0; candidateIndex < compiledBossPattern.ModuleCandidates.Count; candidateIndex++)
            moduleCandidateBuffer.Add(compiledBossPattern.ModuleCandidates[candidateIndex]);

        DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimeBuffer = AddBuffer<EnemyBossPatternSlotRuntimeElement>(entity);
        AppendDefaultBossSlotRuntime(slotRuntimeBuffer, EnemyBossPatternSlotKind.CoreMovement);
        AppendDefaultBossSlotRuntime(slotRuntimeBuffer, EnemyBossPatternSlotKind.ShortRangeInteraction);
        AppendDefaultBossSlotRuntime(slotRuntimeBuffer, EnemyBossPatternSlotKind.WeaponInteraction);

        DynamicBuffer<EnemyBossPatternShooterConfigElement> bossShooterBuffer = AddBuffer<EnemyBossPatternShooterConfigElement>(entity);

        for (int shooterIndex = 0; shooterIndex < compiledBossPattern.ShooterConfigs.Count; shooterIndex++)
        {
            bossShooterBuffer.Add(new EnemyBossPatternShooterConfigElement
            {
                ShooterConfig = compiledBossPattern.ShooterConfigs[shooterIndex]
            });
        }

        DynamicBuffer<EnemyBossPatternBombardierConfigElement> bossBombardierBuffer = AddBuffer<EnemyBossPatternBombardierConfigElement>(entity);

        for (int bombardierIndex = 0; bombardierIndex < compiledBossPattern.BombardierConfigs.Count; bombardierIndex++)
        {
            bossBombardierBuffer.Add(new EnemyBossPatternBombardierConfigElement
            {
                BombardierConfig = compiledBossPattern.BombardierConfigs[bombardierIndex]
            });
        }

        DynamicBuffer<EnemyBossPatternPowerUpStealerConfigElement> bossStealerBuffer = AddBuffer<EnemyBossPatternPowerUpStealerConfigElement>(entity);

        for (int stealerIndex = 0; stealerIndex < compiledBossPattern.PowerUpStealerConfigs.Count; stealerIndex++)
        {
            bossStealerBuffer.Add(new EnemyBossPatternPowerUpStealerConfigElement
            {
                StealerConfig = compiledBossPattern.PowerUpStealerConfigs[stealerIndex]
            });
        }

        DynamicBuffer<EnemyBossPatternOffensiveEngagementConfigElement> bossEngagementBuffer = AddBuffer<EnemyBossPatternOffensiveEngagementConfigElement>(entity);

        for (int configIndex = 0; configIndex < compiledBossPattern.OffensiveEngagementConfigs.Count; configIndex++)
        {
            bossEngagementBuffer.Add(new EnemyBossPatternOffensiveEngagementConfigElement
            {
                Config = compiledBossPattern.OffensiveEngagementConfigs[configIndex]
            });
        }

        TryBakeBossDropExtraction(entity, compiledBossPattern);
        DynamicBuffer<EnemyBossMinionSpawnElement> minionSpawnBuffer = AddBuffer<EnemyBossMinionSpawnElement>(entity);
        AddBuffer<EnemyBossPendingMinionSpawnElement>(entity);

        for (int minionIndex = 0; minionIndex < compiledBossPattern.MinionSpawns.Count; minionIndex++)
            minionSpawnBuffer.Add(compiledBossPattern.MinionSpawns[minionIndex]);

        AddComponent(entity, BuildBossHudConfig(authoring));
        TryBakeBossHudManagedConfig(authoring, entity);
    }

    /// <summary>
    /// Adds one default internal slot runtime entry used by boss module extraction.
    /// </summary>
    /// <param name="slotRuntimeBuffer">Runtime slot buffer receiving the entry.</param>
    /// <param name="slotKind">Slot represented by the runtime entry.</param>
    private static void AppendDefaultBossSlotRuntime(DynamicBuffer<EnemyBossPatternSlotRuntimeElement> slotRuntimeBuffer,
                                                     EnemyBossPatternSlotKind slotKind)
    {
        slotRuntimeBuffer.Add(new EnemyBossPatternSlotRuntimeElement
        {
            SlotKind = slotKind,
            ActivePatternIndex = -2,
            ActiveCandidateIndex = -2,
            ActiveCandidateElapsedSeconds = 0f,
            ExtractionElapsedSeconds = 0f,
            DistanceSinceLastExtraction = 0f,
            LastExtractionMissingHealthPercent = 0f,
            PlayerDistanceHoldSeconds = 0f,
            DamageWindowElapsedSeconds = 0f,
            DamageWindowAccumulated = 0f,
            PreviousObservedDurability = 0f
        });
    }

    /// <summary>
    /// Writes boss-specific drop extraction source buffers used to rebuild standard drop buffers on death.
    /// </summary>
    /// <param name="entity">Enemy entity being baked.</param>
    /// <param name="compiledBossPattern">Compiled boss pattern data.</param>
    private void TryBakeBossDropExtraction(Entity entity, EnemyCompiledBossPatternBakeResult compiledBossPattern)
    {
        if (compiledBossPattern == null || !compiledBossPattern.BossDropExtractionEnabled)
            return;

        AddComponent(entity, new EnemyBossDropExtractionConfig
        {
            Enabled = 1,
            ExtractionMode = compiledBossPattern.BossDropExtractionMode
        });
        AddComponent(entity, new EnemyBossDropRuntimeState
        {
            SelectionResolved = 0
        });

        DynamicBuffer<EnemyBossDropCandidateElement> candidateBuffer = AddBuffer<EnemyBossDropCandidateElement>(entity);
        DynamicBuffer<EnemyBossSelectedDropCandidateElement> selectedCandidateBuffer = AddBuffer<EnemyBossSelectedDropCandidateElement>(entity);
        DynamicBuffer<EnemyBossDropExperienceModuleElement> experienceModuleBuffer = AddBuffer<EnemyBossDropExperienceModuleElement>(entity);
        DynamicBuffer<EnemyBossDropExperienceDefinitionElement> experienceDefinitionBuffer = AddBuffer<EnemyBossDropExperienceDefinitionElement>(entity);
        DynamicBuffer<EnemyBossDropRecoveryModuleElement> recoveryModuleBuffer = AddBuffer<EnemyBossDropRecoveryModuleElement>(entity);
        DynamicBuffer<EnemyBossDropRecoveryDefinitionElement> recoveryDefinitionBuffer = AddBuffer<EnemyBossDropRecoveryDefinitionElement>(entity);
        DynamicBuffer<EnemyBossDropExtraComboPointsModuleElement> extraComboPointsModuleBuffer = AddBuffer<EnemyBossDropExtraComboPointsModuleElement>(entity);
        DynamicBuffer<EnemyBossDropExtraComboPointsConditionElement> extraComboPointsConditionBuffer = AddBuffer<EnemyBossDropExtraComboPointsConditionElement>(entity);

        selectedCandidateBuffer.Clear();

        for (int candidateIndex = 0; candidateIndex < compiledBossPattern.DropCandidates.Count; candidateIndex++)
            candidateBuffer.Add(compiledBossPattern.DropCandidates[candidateIndex]);

        for (int moduleIndex = 0; moduleIndex < compiledBossPattern.BossDropExperienceModules.Count; moduleIndex++)
        {
            experienceModuleBuffer.Add(new EnemyBossDropExperienceModuleElement
            {
                Module = compiledBossPattern.BossDropExperienceModules[moduleIndex]
            });
        }

        for (int definitionIndex = 0; definitionIndex < compiledBossPattern.BossDropExperienceDefinitions.Count; definitionIndex++)
        {
            experienceDefinitionBuffer.Add(new EnemyBossDropExperienceDefinitionElement
            {
                Definition = compiledBossPattern.BossDropExperienceDefinitions[definitionIndex]
            });
        }

        for (int moduleIndex = 0; moduleIndex < compiledBossPattern.BossDropRecoveryModules.Count; moduleIndex++)
        {
            recoveryModuleBuffer.Add(new EnemyBossDropRecoveryModuleElement
            {
                Module = compiledBossPattern.BossDropRecoveryModules[moduleIndex]
            });
        }

        for (int definitionIndex = 0; definitionIndex < compiledBossPattern.BossDropRecoveryDefinitions.Count; definitionIndex++)
        {
            recoveryDefinitionBuffer.Add(new EnemyBossDropRecoveryDefinitionElement
            {
                Definition = compiledBossPattern.BossDropRecoveryDefinitions[definitionIndex]
            });
        }

        for (int moduleIndex = 0; moduleIndex < compiledBossPattern.BossDropExtraComboPointsModules.Count; moduleIndex++)
        {
            extraComboPointsModuleBuffer.Add(new EnemyBossDropExtraComboPointsModuleElement
            {
                Module = compiledBossPattern.BossDropExtraComboPointsModules[moduleIndex]
            });
        }

        for (int conditionIndex = 0; conditionIndex < compiledBossPattern.BossDropExtraComboPointsConditions.Count; conditionIndex++)
        {
            extraComboPointsConditionBuffer.Add(new EnemyBossDropExtraComboPointsConditionElement
            {
                Condition = compiledBossPattern.BossDropExtraComboPointsConditions[conditionIndex]
            });
        }
    }

    /// <summary>
    /// Builds unmanaged boss HUD configuration from the resolved visual preset.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <returns>Baked boss HUD config component.</returns>
    private static EnemyBossHudConfig BuildBossHudConfig(EnemyAuthoring authoring)
    {
        IEnemyUiVisualPresetData uiVisualPreset = ResolveUiVisualPresetData(authoring);
        EnemyBossVisualUiSettings bossUi = uiVisualPreset != null ? uiVisualPreset.BossUi : null;
        string displayName = "Boss";

        if (bossUi != null && !string.IsNullOrWhiteSpace(bossUi.BossDisplayName))
            displayName = bossUi.BossDisplayName;
        else if (uiVisualPreset != null && !string.IsNullOrWhiteSpace(uiVisualPreset.PresetName))
            displayName = uiVisualPreset.PresetName;

        Color offscreenIndicatorColor = bossUi != null ? bossUi.OffscreenIndicatorColor : new Color(1f, 0.2f, 0.1f, 0.95f);
        Color portraitColor = bossUi != null ? bossUi.PortraitColor : Color.white;
        PlayerHealthBarsVisualSettings bossSyringeSettings = bossUi != null && bossUi.SyringeBars != null
            ? bossUi.SyringeBars
            : new PlayerHealthBarsVisualSettings(PlayerSyringePalettePreset.BossHealth,
                                                 PlayerSyringePalettePreset.BossShield);
        PlayerHealthBarVisualConfig bossBarsVisualConfig = PlayerHealthBarVisualBakeUtility.BuildConfig(bossSyringeSettings);

        return new EnemyBossHudConfig
        {
            Enabled = bossUi == null || bossUi.Enabled ? (byte)1 : (byte)0,
            ShowHealthBar = bossUi == null || bossUi.ShowHealthBar ? (byte)1 : (byte)0,
            ShowOffscreenIndicator = bossUi == null || bossUi.ShowOffscreenIndicator ? (byte)1 : (byte)0,
            ShowPortrait = bossUi == null || bossUi.ShowPortrait ? (byte)1 : (byte)0,
            DisplayName = new Unity.Collections.FixedString64Bytes(displayName),
            BarsVisualConfig = bossBarsVisualConfig,
            PortraitColor = DamageFlashRuntimeUtility.ToLinearFloat4(portraitColor),
            OffscreenIndicatorColor = DamageFlashRuntimeUtility.ToLinearFloat4(offscreenIndicatorColor),
            PortraitSizePixels = bossUi != null ? math.max(1f, bossUi.PortraitSizePixels) : 96f,
            OffscreenIndicatorSizePixels = bossUi != null ? math.max(1f, bossUi.OffscreenIndicatorSizePixels) : 56f,
            EdgePaddingPixels = bossUi != null ? math.max(0f, bossUi.EdgePaddingPixels) : 30f
        };
    }

    /// <summary>
    /// Builds unmanaged projectile offscreen-warning configuration from the resolved enemy visual preset.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <returns>Baked projectile offscreen-warning config component.</returns>
    private static EnemyProjectileOffscreenWarningConfig BuildProjectileOffscreenWarningConfig(EnemyAuthoring authoring)
    {
        IEnemyUiVisualPresetData uiVisualPreset = ResolveUiVisualPresetData(authoring);
        EnemyProjectileOffscreenWarningSettings warningSettings = uiVisualPreset != null ? uiVisualPreset.ProjectileOffscreenWarning : null;
        Color indicatorColor = warningSettings != null ? warningSettings.IndicatorColor : new Color(1f, 0.48f, 0.05f, 0.95f);

        return new EnemyProjectileOffscreenWarningConfig
        {
            Enabled = warningSettings != null && warningSettings.Enabled ? (byte)1 : (byte)0,
            IndicatorColor = DamageFlashRuntimeUtility.ToLinearFloat4(indicatorColor),
            IndicatorSizePixels = warningSettings != null ? math.max(1f, warningSettings.IndicatorSizePixels) : 42f,
            EdgePaddingPixels = warningSettings != null ? math.max(0f, warningSettings.EdgePaddingPixels) : 28f
        };
    }

    /// <summary>
    /// Adds managed projectile warning assets such as custom offscreen indicator sprites.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <param name="entity">Enemy entity receiving managed data.</param>
    private void TryBakeProjectileOffscreenWarningManagedConfig(EnemyAuthoring authoring, Entity entity)
    {
        if (authoring == null)
            return;

        IEnemyUiVisualPresetData uiVisualPreset = ResolveUiVisualPresetData(authoring);
        EnemyProjectileOffscreenWarningSettings warningSettings = uiVisualPreset != null ? uiVisualPreset.ProjectileOffscreenWarning : null;

        if (warningSettings == null)
            return;

        if (!warningSettings.Enabled)
            return;

        if (warningSettings.IndicatorSprite == null)
            return;

        AddComponentObject(entity, new EnemyProjectileOffscreenWarningManagedConfig
        {
            IndicatorSprite = warningSettings.IndicatorSprite
        });
    }

    /// <summary>
    /// Adds managed boss HUD assets such as custom off-screen indicator and portrait sprites.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <param name="entity">Enemy entity receiving managed data.</param>
    private void TryBakeBossHudManagedConfig(EnemyAuthoring authoring, Entity entity)
    {
        if (authoring == null)
            return;

        IEnemyUiVisualPresetData uiVisualPreset = ResolveUiVisualPresetData(authoring);
        EnemyBossVisualUiSettings bossUi = uiVisualPreset != null ? uiVisualPreset.BossUi : null;

        if (bossUi == null)
            return;

        if (!HasBossHudManagedConfig(bossUi))
            return;

        AddComponentObject(entity, new EnemyBossHudManagedConfig
        {
            OffscreenIndicatorSprite = bossUi.ShowOffscreenIndicator ? bossUi.OffscreenIndicatorSprite : null,
            PortraitSprite = bossUi.ShowPortrait ? bossUi.PortraitSprite : null
        });
    }

    /// <summary>
    /// Resolves enemy UI visual data for bake-time UI components, preserving legacy visual fallback for non-migrated assets.
    /// </summary>
    /// <param name="authoring">Source authoring component used to resolve master and fallback presets.</param>
    /// <returns>Resolved enemy UI visual data, or null when no compatible preset is available.</returns>
    private static IEnemyUiVisualPresetData ResolveUiVisualPresetData(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        return EnemyAuthoringPresetResolverUtility.ResolveUiVisualPresetData(authoring.MasterPreset,
                                                                            authoring.UiVisualPreset,
                                                                            authoring.VisualPreset);
    }

    /// <summary>
    /// Checks whether one boss UI settings block references managed assets required by runtime presentation.
    /// </summary>
    /// <param name="bossUi">Boss UI settings resolved from the visual preset.</param>
    /// <returns>True when at least one enabled managed sprite exists.</returns>
    private static bool HasBossHudManagedConfig(EnemyBossVisualUiSettings bossUi)
    {
        if (bossUi == null)
            return false;

        return bossUi.ShowOffscreenIndicator && bossUi.OffscreenIndicatorSprite != null ||
               bossUi.ShowPortrait && bossUi.PortraitSprite != null;
    }

    /// <summary>
    /// Declares preset dependencies consumed during enemy bake so edits on master, sub-preset and shared pattern assets trigger a rebake.
    /// </summary>
    /// <param name="authoring">Source authoring component used to resolve all preset references.</param>
    private void DeclarePresetDependencies(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return;

        EnemyMasterPreset masterPreset = authoring.MasterPreset;
        EnemyBrainPreset brainPreset = authoring.BrainPreset;
        EnemyVisualPreset visualPreset = authoring.VisualPreset;
        EnemyUiVisualPreset uiVisualPreset = authoring.UiVisualPreset;
        EnemyAdvancedPatternPreset advancedPatternPreset = authoring.AdvancedPatternPreset;
        EnemyBossPatternPreset bossPatternPreset = authoring.BossPatternPreset;

        if (masterPreset != null)
            DependsOn(masterPreset);

        if (brainPreset != null)
            DependsOn(brainPreset);

        if (visualPreset != null)
            DependsOn(visualPreset);

        if (uiVisualPreset != null)
            DependsOn(uiVisualPreset);

        if (advancedPatternPreset != null)
        {
            DependsOn(advancedPatternPreset);

            if (advancedPatternPreset.ModulesAndPatternsPreset != null)
                DependsOn(advancedPatternPreset.ModulesAndPatternsPreset);
        }

        if (bossPatternPreset != null)
        {
            DependsOn(bossPatternPreset);

            if (bossPatternPreset.SourcePatternsPreset != null)
                DependsOn(bossPatternPreset.SourcePatternsPreset);
        }
    }

    private static EnemyVisualMode ResolveBakedVisualMode(EnemyAuthoring authoring, out Animator resolvedAnimatorComponent)
    {
        resolvedAnimatorComponent = null;

        if (authoring == null)
            return EnemyVisualMode.GpuBaked;

        EnemyVisualMode requestedMode = authoring.VisualMode;

        switch (requestedMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                resolvedAnimatorComponent = ResolveAnimatorComponent(authoring);

                if (resolvedAnimatorComponent != null)
                    return EnemyVisualMode.CompanionAnimator;

#if UNITY_EDITOR
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] CompanionAnimator requested on '{0}', but no valid scene Animator was resolved. Falling back to GpuBaked mode.",
                                               authoring.name),
                                 authoring);
#endif
                return EnemyVisualMode.GpuBaked;

            case EnemyVisualMode.GpuBaked:
                return EnemyVisualMode.GpuBaked;

            default:
                return EnemyVisualMode.GpuBaked;
        }
    }

    private static Animator ResolveAnimatorComponent(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        Animator assignedAnimator = authoring.AnimatorComponent;

        if (assignedAnimator != null &&
            assignedAnimator.gameObject != null &&
            assignedAnimator.gameObject.scene.IsValid())
            return assignedAnimator;

        Animator fallbackAnimator = authoring.GetComponentInChildren<Animator>(true);

        if (fallbackAnimator != null &&
            fallbackAnimator.gameObject != null &&
            fallbackAnimator.gameObject.scene.IsValid())
            return fallbackAnimator;

        return null;
    }

    /// <summary>
    /// Resolves the ground-indicator view referenced on the authoring component or falls back to the first
    /// component found anywhere under the enemy hierarchy.
    /// </summary>
    /// <param name="authoring">Source authoring component used to resolve the view reference.</param>
    /// <returns>Resolved view component, or null when no indicator is authored on the prefab.</returns>
    private static EnemyGroundIndicatorView ResolveGroundIndicatorView(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        EnemyGroundIndicatorView assignedView = authoring.GroundIndicatorView;

        if (assignedView != null &&
            assignedView.gameObject != null)
        {
            return assignedView;
        }

        EnemyGroundIndicatorView fallbackView = authoring.GetComponentInChildren<EnemyGroundIndicatorView>(true);

        if (fallbackView != null &&
            fallbackView.gameObject != null)
        {
            return fallbackView;
        }

        return null;
    }

    /// <summary>
    /// Builds the ground-indicator ECS configuration from the authoring resolved values. The SuppressRings
    /// byte is resolved from the Footprint UI and Boss UI settings so the runtime never has to recompute it.
    /// </summary>
    /// <param name="authoring">Source authoring component used to read every footprint field.</param>
    /// <returns>Baked ground-indicator configuration component data.</returns>
    private static EnemyGroundIndicatorConfig BuildGroundIndicatorConfig(EnemyAuthoring authoring)
    {
        // Resolve all authored gates that hide rings while keeping the shadow presentation active.
        bool suppressRings = !authoring.HealthRingsEnabled || ShouldSuppressGroundIndicatorRings(authoring);
        // HeightOffset and RingDistanceFromShadow stay un-clamped: negative values are intentional
        // affordances for sinking the indicator below the pivot or overlapping rings with the shadow.
        bool rotateHitCenterOffset = ShouldRotateHitCenterOffset(authoring);
        float2 hitCenterOffsetXZ = ResolveHitCenterOffsetXZ(authoring, rotateHitCenterOffset);
        return new EnemyGroundIndicatorConfig
        {
            CoverageMode = ResolveShadowCoverageMode(authoring.ShadowCoverageMode),
            RingDistanceFromShadow = authoring.RingDistanceFromShadow,
            RingThickness = math.max(0f, authoring.SpatialUiRingThickness),
            RingSpacing = math.max(0f, authoring.SpatialUiRingSpacing),
            RingArcDegrees = EnemyGroundIndicatorFootprintUtility.ResolveRuntimeRingArcDegrees(authoring.RingArcDegrees),
            HeightOffset = authoring.SpatialUiHeightOffset,
            PositionOffsetXZ = hitCenterOffsetXZ,
            ProjectionMode = ResolveGroundShadowProjectionMode(authoring.GroundShadowProjectionMode),
            ProjectionMaxDistance = math.max(0f, authoring.GroundShadowProjectionMaxDistance),
            ShadowAlpha = math.saturate(authoring.ShadowAlpha),
            ShadowEdgeSoftness = math.max(0f, authoring.ShadowEdgeSoftness),
            RingEdgeSoftness = math.max(0f, authoring.RingEdgeSoftness),
            RingAngularSoftness = math.max(0f, authoring.RingAngularSoftness),
            ShadowColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.ShadowColor),
            HealthFillColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.HealthRingFillColor),
            HealthBackgroundColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.HealthRingBackgroundColor),
            HealthBackgroundAlpha = math.saturate(authoring.HealthRingBackgroundAlpha),
            ShieldFillColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.ShieldRingFillColor),
            ShieldBackgroundColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.ShieldRingBackgroundColor),
            ShieldBackgroundAlpha = math.saturate(authoring.ShieldRingBackgroundAlpha),
            SuppressRings = suppressRings ? (byte)1 : (byte)0,
            LockRingsToWorld = authoring.LockRingsToWorld ? (byte)1 : (byte)0,
            LockedRingsAngleRadians = ConvertWorldHeadingDegreesToPixelAngleRadians(authoring.LockedRingsWorldAngleDegrees)
        };
    }

    /// <summary>
    /// Converts an authored world-heading offset (degrees from world +Z rotating clockwise around +Y) into the
    /// pixel-angle radians expected by the ground indicator shader. The shader resolves the fill arc anchor by
    /// comparing pixel angles in world XZ space, so we pre-convert at bake time to keep the runtime cost to a
    /// single component copy.
    /// </summary>
    /// <param name="worldHeadingDegrees">Authored world-heading angle in degrees (0 = +Z, 90 = +X).</param>
    /// <returns>Equivalent pixel angle in radians used by the shader fill-anchor computation.</returns>
    private static float ConvertWorldHeadingDegreesToPixelAngleRadians(float worldHeadingDegrees)
    {
        return math.radians(90f) - math.radians(worldHeadingDegrees);
    }

    /// <summary>
    /// Resolves the baked local planar hit-center offset shared by gameplay hit checks and ground presentation.
    /// The visual bounds contribution is detected from authored body renderers when the root rotation represents body facing.
    /// Self-spinning enemies keep the preset fine-tune only so the gameplay center stays stable while the visual rotates.
    /// </summary>
    /// <param name="authoring">Source authoring component exposing visual footprint settings.</param>
    /// <param name="rotateHitCenterOffset">True when the resolved offset should rotate with the entity root.</param>
    /// <returns>Local root-space XZ offset from entity root to gameplay hit center.</returns>
    private static float2 ResolveHitCenterOffsetXZ(EnemyAuthoring authoring, bool rotateHitCenterOffset)
    {
        if (authoring == null)
            return float2.zero;

        Vector2 authoredPositionOffset = authoring.PositionOffsetXZ;
        float2 manualOffsetXZ = new float2(authoredPositionOffset.x, authoredPositionOffset.y);

        if (!rotateHitCenterOffset)
            return manualOffsetXZ;

        return EnemyHitCenterBakeUtility.ResolveLocalHitCenterOffsetXZ(authoring, manualOffsetXZ);
    }

    /// <summary>
    /// Resolves whether the hit-center offset should follow the entity root rotation.
    /// Continuous self-rotation is visual spin, not body facing, so offsets remain root-stable for those enemies.
    /// </summary>
    /// <param name="authoring">Source authoring component exposing movement settings.</param>
    /// <returns>True when the baked hit-center offset should rotate with LocalTransform.Rotation.</returns>
    private static bool ShouldRotateHitCenterOffset(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return true;

        return EnemyHitCenterBakeUtility.ShouldRotateHitCenterOffset(authoring.RotationSpeedDegreesPerSecond);
    }

    /// <summary>
    /// Resolves whether the ground-indicator rings should be hidden because the boss HUD already shows
    /// health and shield as screen-space bars. Shadow rendering is unaffected.
    /// </summary>
    /// <param name="authoring">Source authoring component used to inspect the boss UI configuration.</param>
    /// <returns>True when the rings should be suppressed for this enemy.</returns>
    private static bool ShouldSuppressGroundIndicatorRings(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return false;

        IEnemyUiVisualPresetData uiVisualPreset = ResolveUiVisualPresetData(authoring);

        if (uiVisualPreset == null)
            return false;

        EnemyBossVisualUiSettings bossUi = uiVisualPreset.BossUi;

        if (bossUi == null || !bossUi.Enabled || !bossUi.ShowHealthBar)
            return false;

        return authoring.BossPatternPreset != null;
    }

    private static EnemyOffensiveEngagementBillboardView ResolveOffensiveEngagementBillboardView(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        EnemyOffensiveEngagementBillboardView assignedBillboardView = authoring.OffensiveEngagementBillboardView;

        if (assignedBillboardView != null &&
            assignedBillboardView.gameObject != null)
        {
            return assignedBillboardView;
        }

        EnemyOffensiveEngagementBillboardView fallbackBillboardView = authoring.GetComponentInChildren<EnemyOffensiveEngagementBillboardView>(true);

        if (fallbackBillboardView != null &&
            fallbackBillboardView.gameObject != null)
        {
            return fallbackBillboardView;
        }

        return null;
    }

    private void TryBakeShooterRuntime(EnemyAuthoring authoring, Entity entity, EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        GameObject projectilePrefabObject = compiledPattern.ShooterProjectilePrefab;

        if (EnemyAuthoringValidationUtility.IsInvalidShooterProjectilePrefab(authoring, projectilePrefabObject))
        {
#if UNITY_EDITOR
            if (projectilePrefabObject == null)
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Shooter modules are active on '{0}', but Runtime Projectile prefab is not assigned in the resolved Shooter payload.", authoring.name), authoring);
            else
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid Runtime Projectile prefab '{0}' on '{1}'. Assign a dedicated projectile prefab without authoring components.", projectilePrefabObject.name, authoring.name), authoring);
#endif
            return;
        }

        Entity projectilePrefabEntity = GetEntity(projectilePrefabObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ShooterProjectilePrefab
        {
            PrefabEntity = projectilePrefabEntity
        });
        AddComponent(entity, new ProjectilePoolState
        {
            InitialCapacity = math.max(0, compiledPattern.ShooterProjectilePoolInitialCapacity),
            ExpandBatch = math.max(1, compiledPattern.ShooterProjectilePoolExpandBatch),
            Initialized = 0
        });
        AddBuffer<ShootRequest>(entity);
        AddBuffer<ProjectilePoolElement>(entity);
    }

    /// <summary>
    /// Bakes Bombardier runtime prefab binding used by enemy bomb spawn systems.
    /// </summary>
    /// <param name="authoring">Source authoring component used for prefab validation.</param>
    /// <param name="entity">Enemy entity receiving the Bombardier prefab binding.</param>
    /// <param name="compiledPattern">Compiled pattern providing Bombardier runtime prefab settings.</param>
    /// <param name="managedVfxPrefabBindings">Shared managed VFX prefab binding buffer, when available.</param>
    /// <param name="canBakeManagedVfx">True when the shared managed VFX buffers were added to this enemy entity.</param>
    private void TryBakeBombardierRuntime(EnemyAuthoring authoring,
                                          Entity entity,
                                          EnemyCompiledPatternBakeResult compiledPattern,
                                          DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> managedVfxPrefabBindings,
                                          bool canBakeManagedVfx)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        GameObject bombPrefabObject = compiledPattern.BombardierBombPrefab;

        if (EnemyAuthoringValidationUtility.IsInvalidBombardierBombPrefab(authoring, bombPrefabObject))
        {
#if UNITY_EDITOR
            if (bombPrefabObject == null)
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Bombardier modules are active on '{0}', but Runtime Bomb prefab is not assigned in the resolved Bombardier payload.", authoring.name), authoring);
            else
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid Runtime Bomb prefab '{0}' on '{1}'. Assign a dedicated bomb prefab without authoring components.", bombPrefabObject.name, authoring.name), authoring);
#endif
            AddComponent(entity, new EnemyBombardierBombPrefab
            {
                PrefabEntity = Entity.Null
            });
            return;
        }

        Entity bombPrefabEntity = GetEntity(bombPrefabObject, TransformUsageFlags.Dynamic);
        GameObject explosionVfxPrefabObject = compiledPattern.BombardierExplosionVfxPrefab;
        Entity explosionVfxPrefabEntity = ResolveBombardierExplosionVfxPrefabEntity(authoring, explosionVfxPrefabObject);

        AddComponent(entity, new EnemyBombardierBombPrefab
        {
            PrefabEntity = bombPrefabEntity,
            ExplosionVfxPrefabEntity = explosionVfxPrefabEntity,
            ExplosionVfxPrefab = explosionVfxPrefabObject,
            ScaleExplosionVfxToDamageRadius = compiledPattern.BombardierScaleExplosionVfxToDamageRadius ? (byte)1 : (byte)0,
            ExplosionVfxScaleMultiplier = math.max(0.01f, compiledPattern.BombardierExplosionVfxScaleMultiplier)
        });

        TryBakeBombardierExplosionVfxRuntime(managedVfxPrefabBindings,
                                             canBakeManagedVfx,
                                             explosionVfxPrefabEntity,
                                             explosionVfxPrefabObject);
    }

    /// <summary>
    /// Bakes standard drop runtime buffers, optionally keeping empty buffers for boss death-time extraction rewrites.
    /// </summary>
    /// <param name="authoring">Source authoring component used for prefab validation.</param>
    /// <param name="entity">Enemy entity receiving drop config and buffers.</param>
    /// <param name="compiledPattern">Compiled pattern providing normal or boss-union drop modules.</param>
    /// <param name="forceEmptyRuntimeBuffers">True when boss death extraction must rewrite drop buffers at runtime.</param>
    private void TryBakeDropItemsRuntime(EnemyAuthoring authoring,
                                         Entity entity,
                                         EnemyCompiledPatternBakeResult compiledPattern,
                                         bool forceEmptyRuntimeBuffers)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        EnemyDropItemsConfig dropItemsConfig = EnemyDropItemsBakeUtility.CreateDefaultConfig();
        dropItemsConfig.ModuleCombineMode = EnemyDropItemsBakeUtility.ResolveModuleCombineMode(compiledPattern.DropItemsConfig.ModuleCombineMode);
        dropItemsConfig.MinimumSelectedModules = math.max(0, compiledPattern.DropItemsConfig.MinimumSelectedModules);
        dropItemsConfig.MaximumSelectedModules = math.max(dropItemsConfig.MinimumSelectedModules,
                                                          compiledPattern.DropItemsConfig.MaximumSelectedModules);
        List<EnemyExperienceDropModuleElement> stagedExperienceModules = new List<EnemyExperienceDropModuleElement>(compiledPattern.ExperienceDropModules.Count);
        List<EnemyExperienceDropDefinitionElement> stagedExperienceDefinitions = new List<EnemyExperienceDropDefinitionElement>(compiledPattern.ExperienceDropDefinitions.Count);
        List<EnemyRecoveryDropModuleElement> stagedRecoveryModules = new List<EnemyRecoveryDropModuleElement>(compiledPattern.RecoveryDropModules.Count);
        List<EnemyRecoveryDropDefinitionElement> stagedRecoveryDefinitions = new List<EnemyRecoveryDropDefinitionElement>(compiledPattern.RecoveryDropDefinitions.Count);
        List<EnemyExtraComboPointsModuleElement> stagedExtraComboPointsModules = new List<EnemyExtraComboPointsModuleElement>(compiledPattern.ExtraComboPointsModules.Count);
        List<EnemyExtraComboPointsConditionElement> stagedExtraComboPointsConditions = new List<EnemyExtraComboPointsConditionElement>(compiledPattern.ExtraComboPointsConditions.Count);
        List<EnemyDropItemsModuleSelectionElement> stagedSelectionModules = new List<EnemyDropItemsModuleSelectionElement>();

        for (int moduleIndex = 0; moduleIndex < compiledPattern.ExperienceDropModules.Count; moduleIndex++)
        {
            EnemyCompiledExperienceDropModule compiledModule = compiledPattern.ExperienceDropModules[moduleIndex];

            if (compiledModule.MaximumTotalExperienceDrop <= 0f)
                continue;

            int stagedDefinitionStartIndex = stagedExperienceDefinitions.Count;
            int definitionStartIndex = math.max(0, compiledModule.DefinitionStartIndex);
            int definitionEndIndex = math.min(compiledPattern.ExperienceDropDefinitions.Count,
                                              definitionStartIndex + math.max(0, compiledModule.DefinitionCount));
            List<float> stagedDefinitionAmounts = new List<float>(definitionEndIndex - definitionStartIndex);

            for (int definitionIndex = definitionStartIndex; definitionIndex < definitionEndIndex; definitionIndex++)
            {
                EnemyCompiledExperienceDropDefinition compiledDefinition = compiledPattern.ExperienceDropDefinitions[definitionIndex];
                GameObject dropPrefab = compiledDefinition.Prefab;

                if (dropPrefab == null)
                    continue;

                if (EnemyAuthoringValidationUtility.IsInvalidExperienceDropPrefab(authoring, dropPrefab))
                {
#if UNITY_EDITOR
                    Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid experience drop prefab '{0}' on '{1}'. Assign a prefab asset without EnemyAuthoring or PlayerAuthoring components.", dropPrefab.name, authoring.name), authoring);
#endif
                    continue;
                }

                float experienceAmount = math.max(0f, compiledDefinition.ExperienceAmount);

                if (experienceAmount <= 0f)
                    continue;

                Entity dropPrefabEntity = GetEntity(dropPrefab, TransformUsageFlags.Dynamic);
                stagedExperienceDefinitions.Add(new EnemyExperienceDropDefinitionElement
                {
                    PrefabEntity = dropPrefabEntity,
                    ExperienceAmount = experienceAmount
                });
                stagedDefinitionAmounts.Add(experienceAmount);
            }

            int stagedDefinitionCount = stagedExperienceDefinitions.Count - stagedDefinitionStartIndex;

            if (stagedDefinitionCount <= 0)
                continue;

            int estimatedDropsPerDeath = math.max(0, compiledModule.EstimatedDropsPerDeath);

            if (estimatedDropsPerDeath <= 0)
            {
                estimatedDropsPerDeath = math.max(0,
                                                  EnemyExperienceDropDistributionUtility.EstimateDropsForPreview(stagedDefinitionAmounts,
                                                                                                                 compiledModule.MaximumTotalExperienceDrop,
                                                                                                                 compiledModule.Distribution,
                                                                                                                 out float _,
                                                                                                                 out float _));
            }

            int stagedModuleIndex = stagedExperienceModules.Count;
            stagedExperienceModules.Add(new EnemyExperienceDropModuleElement
            {
                MinimumTotalExperienceDrop = math.max(0f, compiledModule.MinimumTotalExperienceDrop),
                MaximumTotalExperienceDrop = math.max(math.max(0f, compiledModule.MinimumTotalExperienceDrop), compiledModule.MaximumTotalExperienceDrop),
                Distribution = math.clamp(compiledModule.Distribution, 0f, 1f),
                DropRadius = math.max(0f, compiledModule.DropRadius),
                GroundHeightOffset = EnemyDropItemsBakeUtility.ResolveGroundHeightOffset(compiledModule.GroundHeightOffset),
                AttractionSpeed = math.max(0f, compiledModule.AttractionSpeed),
                CollectDistance = math.max(0.01f, compiledModule.CollectDistance),
                CollectDistancePerPlayerSpeed = math.max(0f, compiledModule.CollectDistancePerPlayerSpeed),
                SpawnAnimationMinDuration = math.max(0f, compiledModule.SpawnAnimationMinDuration),
                SpawnAnimationMaxDuration = math.max(math.max(0f, compiledModule.SpawnAnimationMinDuration), compiledModule.SpawnAnimationMaxDuration),
                DefinitionStartIndex = stagedDefinitionStartIndex,
                DefinitionCount = stagedDefinitionCount,
                EstimatedDropsPerDeath = estimatedDropsPerDeath,
                SelectionWeight = math.max(0.0001f, compiledModule.SelectionWeight)
            });
            AddDropItemsSelectionModule(stagedSelectionModules,
                                        EnemyDropItemsPayloadKind.Experience,
                                        stagedModuleIndex,
                                        compiledModule.SelectionWeight);
            dropItemsConfig.HasExperienceDrops = 1;
            dropItemsConfig.ExperienceModuleCount = stagedExperienceModules.Count;
            dropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(dropItemsConfig.EstimatedDropsPerDeath,
                                                                                                       estimatedDropsPerDeath);
        }

        for (int moduleIndex = 0; moduleIndex < compiledPattern.RecoveryDropModules.Count; moduleIndex++)
        {
            EnemyCompiledRecoveryDropModule compiledModule = compiledPattern.RecoveryDropModules[moduleIndex];

            if (compiledModule.DropChance <= 0f)
                continue;

            int stagedDefinitionStartIndex = stagedRecoveryDefinitions.Count;
            int definitionStartIndex = math.max(0, compiledModule.DefinitionStartIndex);
            int definitionEndIndex = math.min(compiledPattern.RecoveryDropDefinitions.Count,
                                              definitionStartIndex + math.max(0, compiledModule.DefinitionCount));

            for (int definitionIndex = definitionStartIndex; definitionIndex < definitionEndIndex; definitionIndex++)
            {
                EnemyCompiledRecoveryDropDefinition compiledDefinition = compiledPattern.RecoveryDropDefinitions[definitionIndex];
                GameObject dropPrefab = compiledDefinition.Prefab;

                if (dropPrefab == null)
                    continue;

                if (EnemyAuthoringValidationUtility.IsInvalidExperienceDropPrefab(authoring, dropPrefab))
                {
#if UNITY_EDITOR
                    Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid recovery drop prefab '{0}' on '{1}'. Assign a prefab asset without EnemyAuthoring or PlayerAuthoring components.", dropPrefab.name, authoring.name), authoring);
#endif
                    continue;
                }

                float healthRestoreAmount = math.max(0f, compiledDefinition.HealthRestoreAmount);
                float shieldRestoreAmount = math.max(0f, compiledDefinition.ShieldRestoreAmount);
                int dropCount = math.max(0, compiledDefinition.Count);

                if (dropCount <= 0 || (healthRestoreAmount <= 0f && shieldRestoreAmount <= 0f))
                    continue;

                Entity dropPrefabEntity = GetEntity(dropPrefab, TransformUsageFlags.Dynamic);
                stagedRecoveryDefinitions.Add(new EnemyRecoveryDropDefinitionElement
                {
                    PrefabEntity = dropPrefabEntity,
                    HealthRestoreAmount = healthRestoreAmount,
                    ShieldRestoreAmount = shieldRestoreAmount,
                    Count = dropCount
                });
            }

            int stagedDefinitionCount = stagedRecoveryDefinitions.Count - stagedDefinitionStartIndex;

            if (stagedDefinitionCount <= 0)
                continue;

            int stagedModuleIndex = stagedRecoveryModules.Count;
            stagedRecoveryModules.Add(new EnemyRecoveryDropModuleElement
            {
                DropChance = math.clamp(compiledModule.DropChance, 0f, 1f),
                DropRadius = math.max(0f, compiledModule.DropRadius),
                GroundHeightOffset = EnemyDropItemsBakeUtility.ResolveGroundHeightOffset(compiledModule.GroundHeightOffset),
                AttractionSpeed = math.max(0f, compiledModule.AttractionSpeed),
                CollectDistance = math.max(0.01f, compiledModule.CollectDistance),
                CollectDistancePerPlayerSpeed = math.max(0f, compiledModule.CollectDistancePerPlayerSpeed),
                SpawnAnimationMinDuration = math.max(0f, compiledModule.SpawnAnimationMinDuration),
                SpawnAnimationMaxDuration = math.max(math.max(0f, compiledModule.SpawnAnimationMinDuration), compiledModule.SpawnAnimationMaxDuration),
                DefinitionStartIndex = stagedDefinitionStartIndex,
                DefinitionCount = stagedDefinitionCount,
                EstimatedDropsPerDeath = math.max(0, compiledModule.EstimatedDropsPerDeath),
                SelectionWeight = math.max(0.0001f, compiledModule.SelectionWeight)
            });
            AddDropItemsSelectionModule(stagedSelectionModules,
                                        EnemyDropItemsPayloadKind.Recovery,
                                        stagedModuleIndex,
                                        compiledModule.SelectionWeight);
            dropItemsConfig.HasRecoveryDrops = 1;
            dropItemsConfig.RecoveryModuleCount = stagedRecoveryModules.Count;
            dropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(dropItemsConfig.EstimatedDropsPerDeath,
                                                                                                       math.max(0, compiledModule.EstimatedDropsPerDeath));
        }

        for (int moduleIndex = 0; moduleIndex < compiledPattern.ExtraComboPointsModules.Count; moduleIndex++)
        {
            EnemyCompiledExtraComboPointsModule compiledModule = compiledPattern.ExtraComboPointsModules[moduleIndex];
            int stagedConditionStartIndex = stagedExtraComboPointsConditions.Count;
            int conditionStartIndex = math.max(0, compiledModule.ConditionStartIndex);
            int conditionEndIndex = math.min(compiledPattern.ExtraComboPointsConditions.Count,
                                             conditionStartIndex + math.max(0, compiledModule.ConditionCount));

            for (int conditionIndex = conditionStartIndex; conditionIndex < conditionEndIndex; conditionIndex++)
            {
                EnemyCompiledExtraComboPointsCondition compiledCondition = compiledPattern.ExtraComboPointsConditions[conditionIndex];
                stagedExtraComboPointsConditions.Add(new EnemyExtraComboPointsConditionElement
                {
                    Metric = compiledCondition.Metric,
                    MinimumValue = compiledCondition.MinimumValue,
                    UseMaximumValue = compiledCondition.UseMaximumValue,
                    MaximumValue = compiledCondition.MaximumValue,
                    MinimumMultiplier = compiledCondition.MinimumMultiplier,
                    MaximumMultiplier = compiledCondition.MaximumMultiplier,
                    NormalizedMultiplierCurveSamples = compiledCondition.NormalizedMultiplierCurveSamples
                });
            }

            int stagedModuleIndex = stagedExtraComboPointsModules.Count;
            stagedExtraComboPointsModules.Add(new EnemyExtraComboPointsModuleElement
            {
                BaseMultiplier = compiledModule.BaseMultiplier,
                MinimumFinalMultiplier = compiledModule.MinimumFinalMultiplier,
                MaximumFinalMultiplier = compiledModule.MaximumFinalMultiplier,
                ConditionCombineMode = compiledModule.ConditionCombineMode,
                ConditionStartIndex = stagedConditionStartIndex,
                ConditionCount = stagedExtraComboPointsConditions.Count - stagedConditionStartIndex,
                SelectionWeight = math.max(0.0001f, compiledModule.SelectionWeight)
            });
            AddDropItemsSelectionModule(stagedSelectionModules,
                                        EnemyDropItemsPayloadKind.ExtraComboPoints,
                                        stagedModuleIndex,
                                        compiledModule.SelectionWeight);
            dropItemsConfig.HasExtraComboPoints = 1;
            dropItemsConfig.ExtraComboPointsModuleCount = stagedExtraComboPointsModules.Count;
        }

        ApplyDropItemsSelectionModuleCounts(stagedSelectionModules.Count, ref dropItemsConfig);

        if (dropItemsConfig.HasExperienceDrops == 0 &&
            dropItemsConfig.HasExtraComboPoints == 0 &&
            dropItemsConfig.HasRecoveryDrops == 0 &&
            !forceEmptyRuntimeBuffers)
        {
            return;
        }

        AddComponent(entity, dropItemsConfig);

        if (stagedSelectionModules.Count > 0 || forceEmptyRuntimeBuffers)
        {
            DynamicBuffer<EnemyDropItemsModuleSelectionElement> selectionModulesBuffer = AddBuffer<EnemyDropItemsModuleSelectionElement>(entity);

            for (int moduleIndex = 0; moduleIndex < stagedSelectionModules.Count; moduleIndex++)
                selectionModulesBuffer.Add(stagedSelectionModules[moduleIndex]);
        }

        if (stagedExperienceModules.Count > 0 || forceEmptyRuntimeBuffers)
        {
            DynamicBuffer<EnemyExperienceDropModuleElement> experienceModulesBuffer = AddBuffer<EnemyExperienceDropModuleElement>(entity);
            DynamicBuffer<EnemyExperienceDropDefinitionElement> experienceDefinitionsBuffer = AddBuffer<EnemyExperienceDropDefinitionElement>(entity);

            for (int moduleIndex = 0; moduleIndex < stagedExperienceModules.Count; moduleIndex++)
                experienceModulesBuffer.Add(stagedExperienceModules[moduleIndex]);

            for (int definitionIndex = 0; definitionIndex < stagedExperienceDefinitions.Count; definitionIndex++)
                experienceDefinitionsBuffer.Add(stagedExperienceDefinitions[definitionIndex]);
        }

        if (stagedRecoveryModules.Count > 0 || forceEmptyRuntimeBuffers)
        {
            DynamicBuffer<EnemyRecoveryDropModuleElement> recoveryModulesBuffer = AddBuffer<EnemyRecoveryDropModuleElement>(entity);
            DynamicBuffer<EnemyRecoveryDropDefinitionElement> recoveryDefinitionsBuffer = AddBuffer<EnemyRecoveryDropDefinitionElement>(entity);

            for (int moduleIndex = 0; moduleIndex < stagedRecoveryModules.Count; moduleIndex++)
                recoveryModulesBuffer.Add(stagedRecoveryModules[moduleIndex]);

            for (int definitionIndex = 0; definitionIndex < stagedRecoveryDefinitions.Count; definitionIndex++)
                recoveryDefinitionsBuffer.Add(stagedRecoveryDefinitions[definitionIndex]);
        }

        if (stagedExtraComboPointsModules.Count > 0 || forceEmptyRuntimeBuffers)
        {
            DynamicBuffer<EnemyExtraComboPointsModuleElement> extraComboPointsModulesBuffer = AddBuffer<EnemyExtraComboPointsModuleElement>(entity);
            DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointsConditionsBuffer = AddBuffer<EnemyExtraComboPointsConditionElement>(entity);

            for (int moduleIndex = 0; moduleIndex < stagedExtraComboPointsModules.Count; moduleIndex++)
                extraComboPointsModulesBuffer.Add(stagedExtraComboPointsModules[moduleIndex]);

            for (int conditionIndex = 0; conditionIndex < stagedExtraComboPointsConditions.Count; conditionIndex++)
                extraComboPointsConditionsBuffer.Add(stagedExtraComboPointsConditions[conditionIndex]);
        }
    }

    /// <summary>
    /// Appends one death-time Drop Items module selection entry for weighted combine modes.
    /// </summary>
    /// <param name="selectionModules">Selection buffer staging list receiving the entry.</param>
    /// <param name="payloadKind">Runtime payload kind owned by the selected module.</param>
    /// <param name="moduleIndex">Type-local module index inside the matching payload buffer.</param>
    /// <param name="selectionWeight">Authored relative selection weight.</param>
    private static void AddDropItemsSelectionModule(List<EnemyDropItemsModuleSelectionElement> selectionModules,
                                                    EnemyDropItemsPayloadKind payloadKind,
                                                    int moduleIndex,
                                                    float selectionWeight)
    {
        if (selectionModules == null)
            return;

        selectionModules.Add(new EnemyDropItemsModuleSelectionElement
        {
            PayloadKind = payloadKind,
            ModuleIndex = math.max(0, moduleIndex),
            SelectionWeight = math.max(0.0001f, selectionWeight)
        });
    }

    /// <summary>
    /// Finalizes Drop Items weighted-selection counts after invalid modules and prefabs have been filtered out.
    /// </summary>
    /// <param name="selectionModuleCount">Amount of valid staged selection entries.</param>
    /// <param name="dropItemsConfig">Mutable drop summary config receiving clamped counts.</param>
    private static void ApplyDropItemsSelectionModuleCounts(int selectionModuleCount, ref EnemyDropItemsConfig dropItemsConfig)
    {
        int sanitizedSelectionModuleCount = math.max(0, selectionModuleCount);
        dropItemsConfig.SelectionModuleCount = sanitizedSelectionModuleCount;

        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.AllModules)
        {
            dropItemsConfig.MinimumSelectedModules = sanitizedSelectionModuleCount;
            dropItemsConfig.MaximumSelectedModules = sanitizedSelectionModuleCount;
            return;
        }

        if (dropItemsConfig.ModuleCombineMode == EnemyDropItemsModuleCombineMode.SingleWeightedModule)
        {
            int selectedModuleCount = sanitizedSelectionModuleCount > 0 ? 1 : 0;
            dropItemsConfig.MinimumSelectedModules = selectedModuleCount;
            dropItemsConfig.MaximumSelectedModules = selectedModuleCount;
            return;
        }

        dropItemsConfig.MinimumSelectedModules = math.clamp(dropItemsConfig.MinimumSelectedModules,
                                                            0,
                                                            sanitizedSelectionModuleCount);
        dropItemsConfig.MaximumSelectedModules = math.clamp(math.max(dropItemsConfig.MinimumSelectedModules,
                                                                     dropItemsConfig.MaximumSelectedModules),
                                                            0,
                                                            sanitizedSelectionModuleCount);
    }

    /// <summary>
    /// Resolves the optional Hit VFX prefab into an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <returns>Resolved prefab entity, or Entity.Null when no valid prefab is authored.</returns>
    private Entity ResolveHitVfxPrefabEntity(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return Entity.Null;

        return ResolveRuntimeVfxPrefabEntity(authoring, authoring.HitVfxPrefab, "enemy hit VFX");
    }

    /// <summary>
    /// Resolves the optional Spawn VFX prefab into an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <returns>Resolved prefab entity, or Entity.Null when no valid prefab is authored.</returns>
    private Entity ResolveSpawnVfxPrefabEntity(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return Entity.Null;

        return ResolveRuntimeVfxPrefabEntity(authoring, authoring.SpawnVfxPrefab, "enemy spawn VFX");
    }

    /// <summary>
    /// Resolves the optional Death VFX prefab into an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <returns>Resolved prefab entity, or Entity.Null when no valid prefab is authored.</returns>
    private Entity ResolveDeathVfxPrefabEntity(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return Entity.Null;

        return ResolveRuntimeVfxPrefabEntity(authoring, authoring.DeathVfxPrefab, "enemy death VFX");
    }

    /// <summary>
    /// Resolves the optional custom or shared standard death puddle prefab into an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <returns>Resolved puddle prefab entity, or Entity.Null when death puddles are disabled or no valid prefab exists.</returns>
    private Entity ResolveDeathPuddlePrefabEntity(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return Entity.Null;

        EnemyVisualDeathPuddleSettings settings = authoring.DeathPuddleSettings;

        if (settings == null || !settings.Enabled)
            return Entity.Null;

        GameObject puddlePrefab = settings.PuddlePrefab;

        if (puddlePrefab == null)
            puddlePrefab = Resources.Load<GameObject>("PF_EnemyDeathPuddle");

        if (puddlePrefab != null && puddlePrefab.GetComponent<EnemyDeathPuddlePrefabAuthoring>() == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid enemy death puddle prefab '{0}' on '{1}'. The prefab root must contain EnemyDeathPuddlePrefabAuthoring.",
                                           puddlePrefab.name,
                                           authoring.name),
                             authoring);
#endif
            return Entity.Null;
        }

        return ResolveRuntimeVfxPrefabEntity(authoring, puddlePrefab, "enemy death puddle");
    }

    /// <summary>
    /// Resolves a runtime VFX prefab into an ECS prefab entity while emitting a context-specific bake warning.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <param name="candidatePrefab">Candidate runtime VFX prefab.</param>
    /// <param name="contextLabel"> VFX context used by bake warnings.</param>
    /// <returns>Resolved prefab entity, or Entity.Null when no valid prefab is authored.</returns>
    private Entity ResolveRuntimeVfxPrefabEntity(EnemyAuthoring authoring,
                                                 GameObject candidatePrefab,
                                                 string contextLabel)
    {
        if (candidatePrefab == null)
            return Entity.Null;

        if (EnemyAuthoringValidationUtility.IsInvalidRuntimePrefab(authoring, candidatePrefab))
        {
#if UNITY_EDITOR
            if (authoring != null)
            {
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid {0} prefab '{1}' on '{2}'. Assign a prefab asset without EnemyAuthoring or PlayerAuthoring components.", contextLabel, candidatePrefab.name, authoring.name), authoring);
            }
#endif
            return Entity.Null;
        }

        return GetEntity(candidatePrefab, TransformUsageFlags.Dynamic);
    }

    /// <summary>
    /// Resolves invalid authored spawn-VFX timing values to the conservative spawn-time path for baking.
    /// </summary>
    /// <param name="timing">Authored spawn VFX timing value.</param>
    /// <returns>Runtime-supported spawn VFX timing.</returns>
    private static EnemySpawnVfxTiming ResolveSpawnVfxTiming(EnemySpawnVfxTiming timing)
    {
        switch (timing)
        {
            case EnemySpawnVfxTiming.OnSpawn:
            case EnemySpawnVfxTiming.WithSpawnWarning:
                return timing;

            default:
                return EnemySpawnVfxTiming.OnSpawn;
        }
    }

    /// <summary>
    /// Resolves the optional Bombardier explosion VFX prefab into an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <param name="candidatePrefab">Candidate explosion VFX prefab.</param>
    /// <returns>Resolved prefab entity, or Entity.Null when no valid prefab is authored.</returns>
    private Entity ResolveBombardierExplosionVfxPrefabEntity(EnemyAuthoring authoring, GameObject candidatePrefab)
    {
        return ResolveRuntimeVfxPrefabEntity(authoring, candidatePrefab, "Bombardier explosion VFX");
    }

    /// <summary>
    /// Adds shared managed VFX buffers used by enemy-authored one-shot visual requests.
    /// </summary>
    /// <param name="entity">Enemy entity receiving the managed VFX runtime buffers.</param>
    /// <returns>Prefab binding buffer used by module-specific bake helpers.</returns>
    private DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> BakeEnemyManagedVfxRuntime(Entity entity)
    {
        AddBuffer<PlayerPowerUpVfxSpawnRequest>(entity);
        DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings = AddBuffer<PlayerPowerUpVfxPrefabBindingElement>(entity);
        AddComponent(entity, BuildEnemyManagedVfxCapConfig());
        return prefabBindings;
    }

    /// <summary>
    /// Resolves and stores Acid Wanderer trail VFX data in the compiled pattern config.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <param name="compiledPattern">Compiled pattern providing Acid VFX authoring settings.</param>
    /// <param name="patternConfig">Mutable pattern config receiving the resolved VFX prefab entity.</param>
    /// <param name="managedVfxPrefabBindings">Shared managed VFX prefab binding buffer, when available.</param>
    /// <param name="canBakeManagedVfx">True when the shared managed VFX buffers were added to this enemy entity.</param>
    private void TryBakeAcidTrailVfxRuntime(EnemyAuthoring authoring,
                                            EnemyCompiledPatternBakeResult compiledPattern,
                                            ref EnemyPatternConfig patternConfig,
                                            DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> managedVfxPrefabBindings,
                                            bool canBakeManagedVfx)
    {
        patternConfig.AcidTrailVfxPrefabEntity = Entity.Null;

        if (!canBakeManagedVfx)
            return;

        if (compiledPattern == null)
            return;

        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererAcid)
            return;

        GameObject acidVfxPrefab = compiledPattern.AcidTrailSegmentVfxPrefab;
        Entity acidVfxPrefabEntity = ResolveAcidTrailVfxPrefabEntity(authoring, acidVfxPrefab);

        if (acidVfxPrefabEntity == Entity.Null || acidVfxPrefab == null)
            return;

        patternConfig.AcidTrailVfxPrefabEntity = acidVfxPrefabEntity;
        patternConfig.AcidTrailScaleVfxToRadius = compiledPattern.AcidTrailScaleSegmentVfxToRadius ? (byte)1 : (byte)0;
        patternConfig.AcidTrailVfxScaleMultiplier = math.max(0.01f, compiledPattern.AcidTrailSegmentVfxScaleMultiplier);
        patternConfig.AcidTrailVfxOffset = new float3(compiledPattern.AcidTrailSegmentVfxOffset.x,
                                                       compiledPattern.AcidTrailSegmentVfxOffset.y,
                                                       compiledPattern.AcidTrailSegmentVfxOffset.z);
        AppendManagedVfxPrefabBinding(managedVfxPrefabBindings, acidVfxPrefabEntity, acidVfxPrefab);
    }

    /// <summary>
    /// Resolves the optional Acid Wanderer trail VFX prefab into an ECS prefab entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <param name="candidatePrefab">Candidate trail segment VFX prefab.</param>
    /// <returns>Resolved prefab entity, or Entity.Null when no valid prefab is authored.</returns>
    private Entity ResolveAcidTrailVfxPrefabEntity(EnemyAuthoring authoring, GameObject candidatePrefab)
    {
        return ResolveRuntimeVfxPrefabEntity(authoring, candidatePrefab, "Acid Wanderer trail VFX");
    }

    /// <summary>
    /// Adds Bombardier explosion VFX prefab bindings to the shared enemy managed VFX runtime.
    /// </summary>
    /// <param name="prefabBindings">Shared managed VFX prefab binding buffer, when available.</param>
    /// <param name="canBakeManagedVfx">True when the shared managed VFX buffers were added to this enemy entity.</param>
    /// <param name="prefabEntity">Resolved explosion VFX prefab entity.</param>
    /// <param name="sourcePrefab">Source prefab asset stored for managed runtime instantiation.</param>
    private static void TryBakeBombardierExplosionVfxRuntime(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                                             bool canBakeManagedVfx,
                                                             Entity prefabEntity,
                                                             GameObject sourcePrefab)
    {
        if (!canBakeManagedVfx)
            return;

        AppendManagedVfxPrefabBinding(prefabBindings, prefabEntity, sourcePrefab);
    }

    /// <summary>
    /// Adds Spawn VFX prefab bindings to the shared enemy managed VFX runtime.
    /// </summary>
    /// <param name="prefabBindings">Shared managed VFX prefab binding buffer, when available.</param>
    /// <param name="canBakeManagedVfx">True when the shared managed VFX buffers were added to this enemy entity.</param>
    /// <param name="prefabEntity">Resolved spawn VFX prefab entity.</param>
    /// <param name="sourcePrefab">Source prefab asset stored for managed runtime instantiation.</param>
    private static void TryBakeSpawnVfxRuntime(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                               bool canBakeManagedVfx,
                                               Entity prefabEntity,
                                               GameObject sourcePrefab)
    {
        if (!canBakeManagedVfx)
            return;

        AppendManagedVfxPrefabBinding(prefabBindings, prefabEntity, sourcePrefab);
    }

    /// <summary>
    /// Adds Death VFX prefab bindings to the shared enemy managed VFX runtime.
    /// </summary>
    /// <param name="prefabBindings">Shared managed VFX prefab binding buffer, when available.</param>
    /// <param name="canBakeManagedVfx">True when the shared managed VFX buffers were added to this enemy entity.</param>
    /// <param name="prefabEntity">Resolved death VFX prefab entity.</param>
    /// <param name="sourcePrefab">Source prefab asset stored for managed runtime instantiation.</param>
    private static void TryBakeDeathVfxRuntime(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                               bool canBakeManagedVfx,
                                               Entity prefabEntity,
                                               GameObject sourcePrefab)
    {
        if (!canBakeManagedVfx)
            return;

        AppendManagedVfxPrefabBinding(prefabBindings, prefabEntity, sourcePrefab);
    }

    /// <summary>
    /// Bakes enemy-owned projectile hit and death VFX configs plus their managed prefab bindings.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for preset and warning context.</param>
    /// <param name="entity">Enemy entity receiving optional projectile VFX configs.</param>
    /// <param name="prefabBindings">Shared managed VFX prefab binding buffer, when available.</param>
    /// <param name="canBakeManagedVfx">True when the shared managed VFX buffers were added to this enemy entity.</param>
    private void TryBakeEnemyProjectileVfxRuntime(EnemyAuthoring authoring,
                                                  Entity entity,
                                                  DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                                  bool canBakeManagedVfx)
    {
        if (authoring == null || !canBakeManagedVfx)
            return;

        EnemyProjectileVfxEventConfig hitConfig = BuildEnemyProjectileVfxEventConfig(authoring,
                                                                                     authoring.BulletHitVfx,
                                                                                     null,
                                                                                     "enemy bullet hit VFX");

        if (hitConfig.PrefabEntity != Entity.Null || hitConfig.SourcePrefab.Value != null)
        {
            AddComponent(entity, new EnemyProjectileHitVfxConfig
            {
                Hit = hitConfig
            });
            AppendManagedVfxPrefabBinding(prefabBindings,
                                          hitConfig.PrefabEntity,
                                          hitConfig.SourcePrefab.Value);
        }

        EnemyProjectileDeathVfxSettings deathSettings = authoring.BulletDeathVfx;

        if (deathSettings == null || !deathSettings.HasAnyPrefab)
            return;

        EnemyProjectileVfxEventConfig rangeOrLifetimeConfig = BuildEnemyProjectileVfxEventConfig(authoring,
                                                                                                 deathSettings.RangeOrLifetime,
                                                                                                 null,
                                                                                                 "enemy bullet death range/lifetime VFX");
        GameObject rangeOrLifetimePrefab = rangeOrLifetimeConfig.SourcePrefab.Value;
        EnemyProjectileVfxEventConfig terminalWallHitConfig = BuildEnemyProjectileVfxEventConfig(authoring,
                                                                                                 deathSettings.TerminalWallHit,
                                                                                                 rangeOrLifetimePrefab,
                                                                                                 "enemy bullet death terminal wall VFX");
        AddComponent(entity, new EnemyProjectileDeathVfxConfig
        {
            RangeOrLifetime = rangeOrLifetimeConfig,
            TerminalWallHit = terminalWallHitConfig
        });
        AppendManagedVfxPrefabBinding(prefabBindings,
                                      rangeOrLifetimeConfig.PrefabEntity,
                                      rangeOrLifetimeConfig.SourcePrefab.Value);
        AppendManagedVfxPrefabBinding(prefabBindings,
                                      terminalWallHitConfig.PrefabEntity,
                                      terminalWallHitConfig.SourcePrefab.Value);
    }

    /// <summary>
    /// Builds one enemy projectile VFX event config and resolves its prefab into an ECS entity.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used for warning context.</param>
    /// <param name="settings">Authored enemy projectile VFX event settings.</param>
    /// <param name="fallbackPrefab">Optional fallback prefab used when the event has no direct assignment.</param>
    /// <param name="contextLabel"> VFX context used by bake warnings.</param>
    /// <returns>Runtime event config with safe presentation values.</returns>
    private EnemyProjectileVfxEventConfig BuildEnemyProjectileVfxEventConfig(EnemyAuthoring authoring,
                                                                             EnemyProjectileVfxEventSettings settings,
                                                                             GameObject fallbackPrefab,
                                                                             string contextLabel)
    {
        if (settings == null)
            return default;

        GameObject prefab = settings.VfxPrefab != null ? settings.VfxPrefab : fallbackPrefab;
        Entity prefabEntity = ResolveRuntimeVfxPrefabEntity(authoring, prefab, contextLabel);
        GameObject sourcePrefab = prefabEntity != Entity.Null ? prefab : null;
        Vector3 spawnOffset = settings.SpawnOffset;
        return new EnemyProjectileVfxEventConfig
        {
            PrefabEntity = prefabEntity,
            SourcePrefab = sourcePrefab,
            SpawnOffset = new float3(spawnOffset.x, spawnOffset.y, spawnOffset.z),
            UniformScale = math.max(0.01f, settings.ScaleMultiplier),
            LifetimeSeconds = math.max(0.05f, settings.LifetimeSeconds),
            Enabled = settings.Enabled ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Adds one managed VFX prefab binding when an equivalent binding does not already exist.
    /// </summary>
    /// <param name="prefabBindings">Shared managed VFX prefab binding buffer.</param>
    /// <param name="prefabEntity">Resolved VFX prefab entity.</param>
    /// <param name="sourcePrefab">Source prefab asset stored for managed runtime instantiation.</param>
    private static void AppendManagedVfxPrefabBinding(DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings,
                                                      Entity prefabEntity,
                                                      GameObject sourcePrefab)
    {
        if (prefabEntity == Entity.Null || sourcePrefab == null)
            return;

        for (int bindingIndex = 0; bindingIndex < prefabBindings.Length; bindingIndex++)
        {
            PlayerPowerUpVfxPrefabBindingElement binding = prefabBindings[bindingIndex];

            if (binding.PrefabEntity == prefabEntity)
                return;
        }

        prefabBindings.Add(new PlayerPowerUpVfxPrefabBindingElement
        {
            PrefabEntity = prefabEntity,
            Prefab = sourcePrefab
        });
    }

    /// <summary>
    /// Builds conservative one-shot VFX caps for enemy-authored visual feedback.
    /// </summary>
    /// <returns>Runtime VFX cap config shared with the managed VFX pool.</returns>
    private static PlayerPowerUpVfxCapConfig BuildEnemyManagedVfxCapConfig()
    {
        return new PlayerPowerUpVfxCapConfig
        {
            MaxSamePrefabPerCell = 10,
            CellSize = 1.75f,
            MaxAttachedSamePrefabPerTarget = 1,
            MaxActiveOneShotVfx = 700,
            RefreshAttachedLifetimeOnCapHit = 1
        };
    }

    /// <summary>
    /// Converts a authored string into a FixedString64Bytes value used by managed VFX color filtering.
    /// </summary>
    /// <param name="value">Source string from the visual preset.</param>
    /// <returns>Trimmed fixed string, or empty when no child-name filter is configured.</returns>
    private static FixedString64Bytes NormalizeFixedString64(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return default;

        string trimmedValue = value.Trim();

        if (Encoding.UTF8.GetByteCount(trimmedValue) > 61)
            return default;

        return new FixedString64Bytes(trimmedValue);
    }

    /// <summary>
    /// Registers all renderer entities that must react to enemy hit flash feedback.
    /// </summary>
    /// <param name="authoring">Source enemy authoring component used to enumerate renderers.</param>
    /// <param name="rootEntity">Root enemy entity that owns the flash config and render target buffer.</param>
    private void BakeDamageFlashRenderTargets(EnemyAuthoring authoring, Entity rootEntity)
    {
        if (authoring == null)
            return;

        DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = AddBuffer<DamageFlashRenderTargetElement>(rootEntity);
        Renderer[] renderers = authoring.GetComponentsInChildren<Renderer>(true);
        HashSet<Entity> bakedRenderEntities = new HashSet<Entity>();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            Entity renderEntity = GetEntity(renderer.gameObject, TransformUsageFlags.Renderable);

            if (!bakedRenderEntities.Add(renderEntity))
                continue;

            renderTargets.Add(new DamageFlashRenderTargetElement
            {
                Value = renderEntity,
                BaseColor = EnemyVisualColorSamplingUtility.ResolveRendererBaseColor(renderer)
            });
        }

        if (renderTargets.Length > 0)
            return;

        renderTargets.Add(new DamageFlashRenderTargetElement
        {
            Value = rootEntity,
            BaseColor = new float4(1f, 1f, 1f, 1f)
        });
    }
    #endregion

    #endregion
}
