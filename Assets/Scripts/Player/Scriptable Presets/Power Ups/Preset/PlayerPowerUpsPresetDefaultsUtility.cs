using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds default modular power-up content for empty presets.
/// </summary>
internal static class PlayerPowerUpsPresetDefaultsUtility
{
    #region Constants
    private const PlayerChargeAnimationClipSlot DefaultChargeAnimationClipSlot = PlayerChargeAnimationClipSlot.Primary;
    private const PlayerReleaseAnimationClipSlot DefaultReleaseAnimationClipSlot = PlayerReleaseAnimationClipSlot.Primary;

    internal const string ModuleIdTriggerPress = "Module_TriggerPress";
    internal const string ModuleIdTriggerRelease = "Module_TriggerRelease";
    internal const string ModuleIdTriggerHoldCharge = "Module_TriggerHoldCharge";
    internal const string ModuleIdTriggerEvent = "Module_TriggerEvent";
    internal const string ModuleIdGateResource = "Module_GateResource";
    internal const string ModuleIdStateSuppressShooting = "Module_StateSuppressShooting";
    internal const string ModuleIdProjectilesPatternCone = "Module_ProjectilesPatternCone";
    internal const string ModuleIdCharacterTuning = "Module_CharacterTuning";
    internal const string ModuleIdProjectilesTuning = ModuleIdCharacterTuning;
    internal const string ModuleIdSpawnObject = "Module_SpawnObject";
    internal const string ModuleIdDash = "Module_Dash";
    internal const string ModuleIdTimeDilationEnemies = "Module_TimeDilationEnemies";
    internal const string ModuleIdImpactFrame = "Module_ImpactFrame";
    internal const string ModuleIdGhostTrail = "Module_GhostTrail";
    internal const string ModuleIdHeal = "Module_Heal";
    internal const string ModuleIdSpawnTrailSegment = "Module_SpawnTrailSegment";
    internal const string ModuleIdAreaTickApplyElement = "Module_AreaTickApplyElement";
    internal const string ModuleIdDeathExplosion = "Module_DeathExplosion";
    internal const string ModuleIdOrbitalProjectiles = "Module_OrbitalProjectiles";
    internal const string ModuleIdOrbitalProjections = "Module_OrbitalProjections";
    internal const string ModuleIdBouncingProjectiles = "Module_BouncingProjectiles";
    internal const string ModuleIdProjectileSplit = "Module_ProjectileSplit";
    internal const string ModuleIdStackable = "Module_Stackable";
    internal const string ModuleIdLaserBeam = "Module_LaserBeam";
    internal const string ModuleIdSwitchWeapon = "Module_SwitchWeapon";
    internal const string ModuleIdAttractDrops = "Module_AttractDrops";
    internal const string ModuleIdReturningProjectiles = PlayerReturningProjectilesPresetDefaultsUtility.ModuleId;
    internal const string ModuleIdDelayedShootApplication = "Module_DelayedShootApplication";
    internal const string ModuleIdSuddenStrike = "Module_SuddenStrike";
    internal const string ModuleIdSelfPreservationInstinct = "Module_SelfPreservationInstinct";
    internal const string ModuleIdRandomStatGrowth = "Module_RandomStatGrowth";

    internal const string ActivePowerUpIdShotgun = "ActiveShotgun";
    internal const string ActivePowerUpIdChargeShot = "ActiveChargeShot";
    internal const string ActivePowerUpIdGigaBomb = "ActiveGigaBomb";
    internal const string ActivePowerUpIdBasicDash = "ActiveBasicDash";
    internal const string ActivePowerUpIdPortableHealthPack = "ActivePortableHealthPack";
    internal const string ActivePowerUpIdBulletTime = "ActiveBulletTime";
    internal const string ActivePowerUpIdBoomerang = PlayerReturningProjectilesPresetDefaultsUtility.BoomerangPowerUpId;
    internal const string ActivePowerUpIdEngineeredGrowth = PlayerRandomStatGrowthPresetDefaultsUtility.PowerUpId;

    internal const string PassivePowerUpIdElementalTrail = "PassiveElementalTrail";
    internal const string PassivePowerUpIdEnemiesExplodeOnDeath = "PassiveEnemiesExplodeOnDeath";
    internal const string PassivePowerUpIdOrbitalProjectiles = "PassiveOrbitalProjectiles";
    internal const string PassivePowerUpIdBouncingProjectiles = "PassiveBouncingProjectiles";
    internal const string PassivePowerUpIdSplittingProjectiles = "PassiveSplittingProjectiles";
    internal const string PassivePowerUpIdTwoStepTreatment = PlayerReturningProjectilesPresetDefaultsUtility.TwoStepTreatmentPowerUpId;
    #endregion

    #region Methods
    public static void GenerateDefaultModularSetupIfEmpty(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        if (preset.ModuleDefinitionsMutable.Count > 0)
            return;

        if (preset.ActivePowerUpsMutable.Count > 0)
            return;

        if (preset.PassivePowerUpsMutable.Count > 0)
            return;

        List<string> defaultDropPools = BuildDefaultDropPools(preset);
        preset.ModuleDefinitionsMutable = BuildDefaultModuleDefinitions();
        preset.ActivePowerUpsMutable = BuildDefaultActivePowerUps(defaultDropPools);
        preset.PassivePowerUpsMutable = BuildDefaultPassivePowerUps(defaultDropPools);
        preset.PrimaryActivePowerUpIdMutable = ActivePowerUpIdShotgun;
        preset.SecondaryActivePowerUpIdMutable = ActivePowerUpIdBasicDash;

        if (preset.EquippedPassivePowerUpIdsMutable == null)
            preset.EquippedPassivePowerUpIdsMutable = new List<string>();

        preset.EquippedPassivePowerUpIdsMutable.Clear();
        preset.EquippedPassivePowerUpIdsMutable.Add(PassivePowerUpIdElementalTrail);
    }

    public static List<string> BuildDropPoolCopy(List<string> sourceDropPools)
    {
        List<string> copy = new List<string>();

        if (sourceDropPools == null)
            return copy;

        for (int index = 0; index < sourceDropPools.Count; index++)
        {
            string poolId = sourceDropPools[index];

            if (string.IsNullOrWhiteSpace(poolId))
                continue;

            copy.Add(poolId);
        }

        return copy;
    }

    public static List<string> BuildDefaultDropPools(PlayerPowerUpsPreset preset)
    {
        List<string> defaultDropPools = BuildDropPoolIdsCopy(preset != null ? preset.DropPoolsMutable : null);

        if (defaultDropPools.Count > 0)
            return defaultDropPools;

        defaultDropPools = BuildDropPoolCopy(preset != null ? preset.DropPoolCatalogMutable : null);

        if (defaultDropPools.Count > 0)
            return defaultDropPools;

        defaultDropPools.Add("Milestone");
        defaultDropPools.Add("Shop");
        defaultDropPools.Add("Boss");
        return defaultDropPools;
    }

    public static List<PowerUpDropPoolDefinition> BuildDefaultDropPoolDefinitions(PlayerPowerUpsPreset preset)
    {
        List<PowerUpDropPoolDefinition> definitions = new List<PowerUpDropPoolDefinition>();
        string fallbackTierId = ResolveDefaultTierId(preset);
        List<string> defaultPoolIds = BuildDefaultDropPools(preset);

        for (int poolIndex = 0; poolIndex < defaultPoolIds.Count; poolIndex++)
        {
            string poolId = defaultPoolIds[poolIndex];
            List<PowerUpDropPoolTierDefinition> tierRolls = new List<PowerUpDropPoolTierDefinition>();

            if (!string.IsNullOrWhiteSpace(fallbackTierId))
            {
                PowerUpDropPoolTierDefinition tierRoll = new PowerUpDropPoolTierDefinition();
                tierRoll.Configure(fallbackTierId, 100f);
                tierRolls.Add(tierRoll);
            }

            PowerUpDropPoolDefinition dropPool = new PowerUpDropPoolDefinition();
            dropPool.Configure(poolId, tierRolls);
            dropPool.Validate(poolId);
            definitions.Add(dropPool);
        }

        return definitions;
    }

    public static PowerUpModuleDefinition CreateModuleDefinition(string moduleId,
                                                                 string displayName,
                                                                 PowerUpModuleKind moduleKind,
                                                                 PowerUpModuleStage defaultStage,
                                                                 string notes)
    {
        PowerUpModuleData payload = CreateDefaultPayloadForModuleKind(moduleKind);
        PowerUpModuleDefinition moduleDefinition = new PowerUpModuleDefinition();
        moduleDefinition.Configure(moduleId, displayName, moduleKind, defaultStage, notes, payload);
        moduleDefinition.Validate();
        return moduleDefinition;
    }

    private static List<PowerUpModuleDefinition> BuildDefaultModuleDefinitions()
    {
        List<PowerUpModuleDefinition> definitions = new List<PowerUpModuleDefinition>();
        definitions.Add(CreateModuleDefinition(ModuleIdTriggerPress, "Trigger Press", PowerUpModuleKind.TriggerPress, PowerUpModuleStage.Trigger, "Fires when the input is initially pressed."));
        definitions.Add(CreateModuleDefinition(ModuleIdTriggerRelease, "Trigger Release", PowerUpModuleKind.TriggerRelease, PowerUpModuleStage.Trigger, "Fires when the input is released."));
        definitions.Add(CreateModuleDefinition(ModuleIdTriggerHoldCharge, "Trigger Hold Charge", PowerUpModuleKind.TriggerHoldCharge, PowerUpModuleStage.Trigger, "Accumulates charge while the input stays pressed."));
        definitions.Add(CreateModuleDefinition(ModuleIdTriggerEvent, "Trigger Event", PowerUpModuleKind.TriggerEvent, PowerUpModuleStage.Hook, "Fires from a runtime event selected in payload."));
        definitions.Add(CreateModuleDefinition(ModuleIdGateResource, "Resource Gate", PowerUpModuleKind.GateResource, PowerUpModuleStage.Gate, "Checks resource costs, recharge and cooldown."));
        definitions.Add(CreateModuleDefinition(ModuleIdStateSuppressShooting, "Suppress Shooting", PowerUpModuleKind.StateSuppressShooting, PowerUpModuleStage.StateEnter, "Disables base shooting while active."));
        definitions.Add(CreateModuleDefinition(ModuleIdProjectilesPatternCone, "Projectiles Pattern Cone", PowerUpModuleKind.ProjectilesPatternCone, PowerUpModuleStage.Execute, "Shoots a cone of multiple projectiles."));
        definitions.Add(CreateModuleDefinition(ModuleIdCharacterTuning, "Character Tuning", PowerUpModuleKind.CharacterTuning, PowerUpModuleStage.PostExecute, "Applies scalable-stat assignments on acquisition for standard actives, while owned for passives, temporarily during charge with Trigger Hold Charge, or only while active with toggleable Resource Gate."));
        definitions.Add(CreateModuleDefinition(ModuleIdSpawnObject, "Spawn Object", PowerUpModuleKind.SpawnObject, PowerUpModuleStage.Execute, "Spawns a configured object with optional damage payload."));
        definitions.Add(CreateModuleDefinition(ModuleIdDash, "Dash", PowerUpModuleKind.Dash, PowerUpModuleStage.Execute, "Moves player rapidly with optional invulnerability."));
        definitions.Add(CreateModuleDefinition(ModuleIdTimeDilationEnemies, "Time Dilation Enemies", PowerUpModuleKind.TimeDilationEnemies, PowerUpModuleStage.Execute, "Slows enemy simulation for a short duration."));
        definitions.Add(CreateModuleDefinition(ModuleIdImpactFrame, "Impact Frame", PowerUpModuleKind.ImpactFrame, PowerUpModuleStage.Execute, "Runs a global time impact and fullscreen filter when an active power-up activation succeeds."));
        definitions.Add(CreateModuleDefinition(ModuleIdGhostTrail, "Ghost Trail", PowerUpModuleKind.GhostTrail, PowerUpModuleStage.Execute, "Emits pooled residual images and optional screen feedback after a successful active power-up activation."));
        definitions.Add(CreateModuleDefinition(ModuleIdHeal, "Heal", PowerUpModuleKind.Heal, PowerUpModuleStage.Execute, "Applies instant heal or heal-over-time."));
        definitions.Add(CreateModuleDefinition(ModuleIdSpawnTrailSegment, "Spawn Trail Segment", PowerUpModuleKind.SpawnTrailSegment, PowerUpModuleStage.Hook, "Spawns trail segments while moving."));
        definitions.Add(CreateModuleDefinition(ModuleIdAreaTickApplyElement, "Area Tick Apply Element", PowerUpModuleKind.AreaTickApplyElement, PowerUpModuleStage.Hook, "Applies elemental stacks in area over time."));
        definitions.Add(CreateModuleDefinition(ModuleIdDeathExplosion, "Death Explosion", PowerUpModuleKind.DeathExplosion, PowerUpModuleStage.Hook, "Triggers explosions from configured events."));
        definitions.Add(CreateModuleDefinition(ModuleIdOrbitalProjectiles, "Orbital Projectiles", PowerUpModuleKind.OrbitalProjectiles, PowerUpModuleStage.Hook, "Overrides projectile trajectory to orbital behavior."));
        definitions.Add(CreateModuleDefinition(ModuleIdOrbitalProjections, "Orbital Projections", PowerUpModuleKind.OrbitalProjections, PowerUpModuleStage.Hook, "Spawns player-owned orbiting objects with contact damage and interception effects."));
        definitions.Add(CreateModuleDefinition(ModuleIdBouncingProjectiles, "Bouncing Projectiles", PowerUpModuleKind.BouncingProjectiles, PowerUpModuleStage.Hook, "Adds wall bounce behavior to projectiles."));
        definitions.Add(CreateModuleDefinition(ModuleIdProjectileSplit, "Projectile Split", PowerUpModuleKind.ProjectileSplit, PowerUpModuleStage.Hook, "Splits projectiles based on configured trigger mode."));
        definitions.Add(CreateModuleDefinition(ModuleIdStackable, "Stackable", PowerUpModuleKind.Stackable, PowerUpModuleStage.PostExecute, "Allows milestone reacquisition up to a configured total count."));
        definitions.Add(CreateModuleDefinition(ModuleIdLaserBeam, "Laser Beam", PowerUpModuleKind.LaserBeam, PowerUpModuleStage.Hook, "Overrides base projectile spawning with one or more continuous liquid beam lanes."));
        definitions.Add(CreateModuleDefinition(ModuleIdSwitchWeapon, "Switch Weapon", PowerUpModuleKind.SwitchWeapon, PowerUpModuleStage.Hook, "Keeps Base Gun visible and replaces the Player Visual Preset optional attachment with the mountable mesh identified by a defined Weapon Id while the owning power-up is equipped."));
        definitions.Add(CreateModuleDefinition(ModuleIdAttractDrops, "Attract Drops", PowerUpModuleKind.AttractDrops, PowerUpModuleStage.Execute, "Attracts enemy drops inside a configurable player-centered radius and can optionally consume rewards that cannot currently be used."));
        definitions.Add(CreateModuleDefinition(ModuleIdReturningProjectiles, "Returning Projectiles", PowerUpModuleKind.ReturningProjectiles, PowerUpModuleStage.Execute, "Converts projectile termination into retraced or player-seeking return travel with configurable hit and interaction rules."));
        definitions.Add(CreateModuleDefinition(ModuleIdDelayedShootApplication, "Delayed Shoot Application", PowerUpModuleKind.DelayedShootApplication, PowerUpModuleStage.Trigger, "Applies sibling discrete-projectile modules only to every configured base shot."));
        definitions.Add(CreateModuleDefinition(ModuleIdSuddenStrike, "Sudden Strike", PowerUpModuleKind.SuddenStrike, PowerUpModuleStage.Trigger, "Charges a sibling Trigger Hold Charge automatically while its condition is satisfied, then applies its full-charge payload and any sibling projectile or object-spawn effects to the next base shot."));
        definitions.Add(CreateModuleDefinition(ModuleIdSelfPreservationInstinct, "Self-Preservation Instinct", PowerUpModuleKind.SelfPreservationInstinct, PowerUpModuleStage.Trigger, "Executes sibling active-effect modules when player health crosses the configured threshold from above."));
        definitions.Add(CreateModuleDefinition(ModuleIdRandomStatGrowth, "Random Stat Growth", PowerUpModuleKind.RandomStatGrowth, PowerUpModuleStage.Execute, "Permanently increases one random native or numeric custom player statistic after a successful active activation."));
        return definitions;
    }

    private static List<string> BuildDropPoolIdsCopy(List<PowerUpDropPoolDefinition> sourceDropPools)
    {
        List<string> copy = new List<string>();

        if (sourceDropPools == null)
            return copy;

        for (int poolIndex = 0; poolIndex < sourceDropPools.Count; poolIndex++)
        {
            PowerUpDropPoolDefinition dropPool = sourceDropPools[poolIndex];

            if (dropPool == null || string.IsNullOrWhiteSpace(dropPool.PoolId))
                continue;

            copy.Add(dropPool.PoolId.Trim());
        }

        return copy;
    }

    private static string ResolveDefaultTierId(PlayerPowerUpsPreset preset)
    {
        if (preset == null || preset.TierLevelsMutable == null)
            return "Tier1";

        for (int tierIndex = 0; tierIndex < preset.TierLevelsMutable.Count; tierIndex++)
        {
            PowerUpTierLevelDefinition tierLevel = preset.TierLevelsMutable[tierIndex];

            if (tierLevel == null || string.IsNullOrWhiteSpace(tierLevel.TierId))
                continue;

            return tierLevel.TierId.Trim();
        }

        return "Tier1";
    }

    private static List<ModularPowerUpDefinition> BuildDefaultActivePowerUps(List<string> defaultDropPools)
    {
        List<ModularPowerUpDefinition> definitions = new List<ModularPowerUpDefinition>();

        definitions.Add(CreatePowerUpDefinition(ActivePowerUpIdShotgun,
                                                "Shotgun",
                                                "Fires a cone spread of projectiles.",
                                                defaultDropPools,
                                                1,
                                                90,
                                                false,
                                                CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 30f, 25f, 0f, PowerUpChargeType.Time, 0.7f)),
                                                CreateBinding(ModuleIdProjectilesPatternCone, PowerUpModuleStage.Execute, CreateProjectilePatternPayload(6, 45f))));

        definitions.Add(CreatePowerUpDefinition(ActivePowerUpIdChargeShot,
                                                "Charge Shot",
                                                "Builds and releases a charged empowered shot.",
                                                defaultDropPools,
                                                2,
                                                120,
                                                false,
                                                CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdTriggerHoldCharge, PowerUpModuleStage.Trigger, CreateHoldChargePayload(80f, 120f, 140f)),
                                                CreateBinding(ModuleIdTriggerRelease, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 35f, 20f, 0f, PowerUpChargeType.Time, 0.35f)),
                                                CreateBinding(ModuleIdStateSuppressShooting, PowerUpModuleStage.StateEnter, CreateSuppressShootingPayload(true))));

        definitions.Add(CreatePowerUpDefinition(ActivePowerUpIdGigaBomb,
                                                "Giga Bomb",
                                                "Deploys a high-damage area bomb.",
                                                defaultDropPools,
                                                3,
                                                160,
                                                false,
                                                CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 100f, 35f, 100f, PowerUpChargeType.EnemiesDestroyed, 1.8f)),
                                                CreateBinding(ModuleIdSpawnObject, PowerUpModuleStage.Execute, null)));

        definitions.Add(CreatePowerUpDefinition(ActivePowerUpIdBasicDash,
                                                "Basic Dash",
                                                "Quick reposition dash with optional i-frames.",
                                                defaultDropPools,
                                                1,
                                                100,
                                                false,
                                                CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 30f, 25f, 0f, PowerUpChargeType.Time, 0.9f)),
                                                CreateBinding(ModuleIdDash, PowerUpModuleStage.Execute, null)));

        definitions.Add(CreatePowerUpDefinition(ActivePowerUpIdPortableHealthPack,
                                                "Portable Health Pack",
                                                "Instant heal with an energy cost.",
                                                defaultDropPools,
                                                2,
                                                130,
                                                false,
                                                CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 45f, 20f, 0f, PowerUpChargeType.Time, 7f)),
                                                CreateBinding(ModuleIdHeal, PowerUpModuleStage.Execute, CreateHealPayload(35f))));

        definitions.Add(CreatePowerUpDefinition(ActivePowerUpIdBulletTime,
                                                "Bullet Time",
                                                "Slows enemies for a tactical time window.",
                                                defaultDropPools,
                                                3,
                                                170,
                                                false,
                                                CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                                CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 80f, 20f, 0f, PowerUpChargeType.EnemiesDestroyed, 8f)),
                                                CreateBinding(ModuleIdTimeDilationEnemies, PowerUpModuleStage.Execute, null)));

        definitions.Add(CreateDefaultBoomerang(defaultDropPools));
        definitions.Add(PlayerRandomStatGrowthPresetDefaultsUtility.CreateEngineeredGrowth(defaultDropPools, null));

        return definitions;
    }

    private static List<ModularPowerUpDefinition> BuildDefaultPassivePowerUps(List<string> defaultDropPools)
    {
        List<ModularPowerUpDefinition> definitions = new List<ModularPowerUpDefinition>();

        definitions.Add(CreatePowerUpDefinition(PassivePowerUpIdElementalTrail,
                                                "Elemental Trail",
                                                "Leaves an elemental trail that applies area stacks.",
                                                defaultDropPools,
                                                2,
                                                140,
                                                false,
                                                CreateBinding(ModuleIdTriggerEvent, PowerUpModuleStage.Hook, CreateTriggerEventPayload(PowerUpTriggerEventType.OnPlayerMovementStep)),
                                                CreateBinding(ModuleIdSpawnTrailSegment, PowerUpModuleStage.Hook, null),
                                                CreateBinding(ModuleIdAreaTickApplyElement, PowerUpModuleStage.Hook, null)));

        definitions.Add(CreatePowerUpDefinition(PassivePowerUpIdEnemiesExplodeOnDeath,
                                                "Enemies Explode On Death",
                                                "Killed enemies explode and damage nearby targets.",
                                                defaultDropPools,
                                                3,
                                                160,
                                                false,
                                                CreateBinding(ModuleIdTriggerEvent, PowerUpModuleStage.Hook, CreateTriggerEventPayload(PowerUpTriggerEventType.OnEnemyKilled)),
                                                CreateBinding(ModuleIdDeathExplosion, PowerUpModuleStage.Hook, null)));

        definitions.Add(CreatePowerUpDefinition(PassivePowerUpIdOrbitalProjectiles,
                                                "Orbital Projectiles",
                                                "Projectiles switch to an orbital movement pattern.",
                                                defaultDropPools,
                                                2,
                                                150,
                                                false,
                                                CreateBinding(ModuleIdTriggerEvent, PowerUpModuleStage.Hook, CreateTriggerEventPayload(PowerUpTriggerEventType.OnProjectileSpawned)),
                                                CreateBinding(ModuleIdOrbitalProjectiles, PowerUpModuleStage.Hook, null)));

        definitions.Add(CreatePowerUpDefinition(PassivePowerUpIdBouncingProjectiles,
                                                "Bouncing Projectiles",
                                                "Projectiles bounce on walls.",
                                                defaultDropPools,
                                                2,
                                                140,
                                                false,
                                                CreateBinding(ModuleIdTriggerEvent, PowerUpModuleStage.Hook, CreateTriggerEventPayload(PowerUpTriggerEventType.OnProjectileWallHit)),
                                                CreateBinding(ModuleIdBouncingProjectiles, PowerUpModuleStage.Hook, null)));

        definitions.Add(CreatePowerUpDefinition(PassivePowerUpIdSplittingProjectiles,
                                                "Splitting Projectiles",
                                                "Projectiles split on hit/kill or despawn based on trigger mode.",
                                                defaultDropPools,
                                                3,
                                                180,
                                                false,
                                                CreateBinding(ModuleIdTriggerEvent, PowerUpModuleStage.Hook, CreateTriggerEventPayload(PowerUpTriggerEventType.OnProjectileDespawned)),
                                                CreateBinding(ModuleIdProjectileSplit, PowerUpModuleStage.Hook, null)));

        definitions.Add(CreateDefaultTwoStepTreatment(defaultDropPools));

        return definitions;
    }

    /// <summary>
    /// Creates the baseline non-toggleable energy active that emits one player-seeking returning projectile.
    /// </summary>
    /// <param name="defaultDropPools">Drop-pool identifiers copied into the common power-up data.</param>
    /// <returns>Configured Boomerang modular definition.</returns>
    public static ModularPowerUpDefinition CreateDefaultBoomerang(List<string> defaultDropPools)
    {
        return CreatePowerUpDefinition(ActivePowerUpIdBoomerang,
                                       "Boomerang",
                                       "Throws one player-seeking returning projectile that pierces every enemy until it rejoins the player.",
                                       defaultDropPools,
                                       2,
                                       150,
                                       false,
                                       CreateBinding(ModuleIdTriggerPress, PowerUpModuleStage.Trigger, null),
                                       CreateBinding(ModuleIdGateResource, PowerUpModuleStage.Gate, CreateResourceGatePayload(100f, 35f, 25f, 0f, PowerUpChargeType.Time, 0.6f)),
                                       CreateBinding(ModuleIdReturningProjectiles, PowerUpModuleStage.Execute, CreateReturningProjectilesPayload(ProjectileReturnPathMode.SeekPlayer,
                                                                                                                                            ProjectileReturnHitPolicy.CompleteReturn,
                                                                                                                                            true,
                                                                                                                                            false)));
    }

    /// <summary>
    /// Creates the baseline passive that makes normal projectiles retrace their complete outbound path.
    /// </summary>
    /// <param name="defaultDropPools">Drop-pool identifiers copied into the common power-up data.</param>
    /// <returns>Configured Two-Step Treatment modular definition.</returns>
    public static ModularPowerUpDefinition CreateDefaultTwoStepTreatment(List<string> defaultDropPools)
    {
        return CreatePowerUpDefinition(PassivePowerUpIdTwoStepTreatment,
                                       "Two-Step Treatment",
                                       "Normal player projectiles retrace their outbound route after reaching their terminal point.",
                                       defaultDropPools,
                                       2,
                                       155,
                                       false,
                                       CreateBinding(ModuleIdReturningProjectiles, PowerUpModuleStage.Execute, CreateReturningProjectilesPayload(ProjectileReturnPathMode.RetraceOutboundPath,
                                                                                                                                            ProjectileReturnHitPolicy.LimitedAdditionalHits,
                                                                                                                                            true,
                                                                                                                                            false)));
    }

    private static PowerUpModuleData CreateDefaultPayloadForModuleKind(PowerUpModuleKind moduleKind)
    {
        PowerUpModuleData payload = new PowerUpModuleData();

        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerHoldCharge:
                payload.HoldCharge.Configure(80f, 120f, 140f);
                payload.HoldCharge.ConfigureAnimations(DefaultChargeAnimationClipSlot,
                                                       DefaultReleaseAnimationClipSlot);
                break;
            case PowerUpModuleKind.TriggerEvent:
                payload.TriggerEvent.Configure(PowerUpTriggerEventType.OnEnemyKilled);
                break;
            case PowerUpModuleKind.DelayedShootApplication:
                payload.DelayedShootApplication.Configure(3);
                break;
            case PowerUpModuleKind.SuddenStrike:
                payload.SuddenStrike.Configure(SuddenStrikeChargeConditionMode.Stationary,
                                               false,
                                               0.05f,
                                               1f,
                                               false,
                                               0.25f);
                break;
            case PowerUpModuleKind.SelfPreservationInstinct:
                payload.SelfPreservationInstinct.Configure(SelfPreservationHealthThresholdMode.MaximumHealthPercent, 25f);
                break;
            case PowerUpModuleKind.RandomStatGrowth:
                PlayerRandomStatGrowthEntryData randomGrowthEntry = new PlayerRandomStatGrowthEntryData();
                randomGrowthEntry.Configure(PlayerRandomStatGrowthTarget.ProjectileDamage,
                                            string.Empty,
                                            1f,
                                            2f,
                                            1f,
                                            false,
                                            Color.white);
                payload.RandomStatGrowth.Configure(new[] { randomGrowthEntry });
                break;
            case PowerUpModuleKind.GateResource:
                payload.ResourceGate.Configure(PowerUpResourceType.Energy,
                                               PowerUpResourceType.Energy,
                                               100f,
                                               30f,
                                               0f,
                                               0f,
                                               PowerUpChargeType.Time,
                                               25f,
                                               1f);
                break;
            case PowerUpModuleKind.StateSuppressShooting:
                payload.SuppressShooting.Configure(true);
                break;
            case PowerUpModuleKind.ProjectilesPatternCone:
                payload.ProjectilePatternCone.Configure(6, 45f);
                break;
            case PowerUpModuleKind.CharacterTuning:
                payload.CharacterTuning.Configure(new List<PowerUpCharacterTuningFormulaData>());
                break;
            case PowerUpModuleKind.Heal:
                payload.HealMissingHealth.Configure(PowerUpHealApplicationMode.Instant, 35f, 0f, 0.2f, PowerUpHealStackPolicy.Refresh);
                break;
            case PowerUpModuleKind.ImpactFrame:
                payload.ImpactFrame.Configure(ImpactFrameDurationMode.UseEarliestLimit,
                                              6,
                                              60f,
                                              0.12f,
                                              0.02f,
                                              0.08f,
                                              ImpactFrameEasingMode.EaseOutCubic,
                                              95f,
                                              false,
                                              1f,
                                              new UnityEngine.Color(0.96f, 0.78f, 0.55f, 0.45f),
                                               0.65f,
                                               0.55f,
                                               0.6f,
                                               0.35f,
                                               UnityEngine.Color.black,
                                               0.012f,
                                              0.18f,
                                              320f,
                                              0.35f,
                                              0.22f,
                                              0.35f,
                                              0.65f,
                                              0.12f,
                                              0.18f,
                                              0f,
                                              0f,
                                              6f,
                                              0.2f,
                                              0f,
                                              24f,
                                              0.25f,
                                              new UnityEngine.Color(1f, 0.9f, 0.45f, 0.7f));
                break;
            case PowerUpModuleKind.GhostTrail:
                payload.GhostTrail.Configure(1.5f, 0.06f, 0.35f, 18);
                break;
            case PowerUpModuleKind.Stackable:
                payload.Stackable.Configure(2);
                break;
            case PowerUpModuleKind.OrbitalProjections:
                payload.OrbitalProjections.Configure(new List<PowerUpOrbitalProjectionDefinitionData>
                {
                    new PowerUpOrbitalProjectionDefinitionData()
                });
                break;
            case PowerUpModuleKind.SwitchWeapon:
                payload.SwitchWeapon.Configure(string.Empty);
                break;
            case PowerUpModuleKind.AttractDrops:
                payload.DropAttraction.Configure(18f, false);
                break;
            case PowerUpModuleKind.ReturningProjectiles:
                payload = CreateReturningProjectilesPayload(ProjectileReturnPathMode.RetraceOutboundPath,
                                                            ProjectileReturnHitPolicy.LimitedAdditionalHits,
                                                            true,
                                                            false);
                break;
            default:
                break;
        }

        payload.Validate();
        return payload;
    }

    internal static ModularPowerUpDefinition CreatePowerUpDefinition(string powerUpId,
                                                                     string displayName,
                                                                     string descriptionValue,
                                                                     List<string> dropPools,
                                                                     int dropTier,
                                                                     int purchaseCost,
                                                                     bool stealProtected,
                                                                     params PowerUpModuleBinding[] bindings)
    {
        ModularPowerUpDefinition powerUpDefinition = new ModularPowerUpDefinition();
        powerUpDefinition.Configure(CreateCommonData(powerUpId, displayName, descriptionValue, dropPools, dropTier, purchaseCost), stealProtected);
        powerUpDefinition.ClearBindings();

        if (bindings != null)
        {
            for (int index = 0; index < bindings.Length; index++)
                powerUpDefinition.AddBinding(bindings[index]);
        }

        powerUpDefinition.Validate();
        return powerUpDefinition;
    }

    private static PowerUpCommonData CreateCommonData(string powerUpId,
                                                      string displayName,
                                                      string descriptionValue,
                                                      List<string> dropPools,
                                                      int dropTier,
                                                      int purchaseCost)
    {
        PowerUpCommonData commonData = new PowerUpCommonData();
        commonData.Configure(powerUpId,
                             displayName,
                             descriptionValue,
                             null,
                             BuildDropPoolCopy(dropPools),
                             dropTier,
                             purchaseCost);
        commonData.Validate();
        return commonData;
    }

    internal static PowerUpModuleBinding CreateBinding(string moduleId, PowerUpModuleStage stage, PowerUpModuleData overridePayload)
    {
        PowerUpModuleBinding binding = new PowerUpModuleBinding();
        binding.Configure(moduleId, stage, true);

        if (overridePayload != null)
            binding.ConfigureOverride(true, overridePayload);
        else
            binding.ConfigureOverride(false, new PowerUpModuleData());

        binding.Validate();
        return binding;
    }

    private static PowerUpModuleData CreateResourceGatePayload(float maximumEnergy,
                                                               float activationCost,
                                                               float chargePerTrigger,
                                                               float minimumActivationEnergyPercent,
                                                               PowerUpChargeType chargeType,
                                                               float cooldownSeconds)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.ResourceGate.Configure(PowerUpResourceType.Energy,
                                       PowerUpResourceType.Energy,
                                       maximumEnergy,
                                       activationCost,
                                       0f,
                                       minimumActivationEnergyPercent,
                                       chargeType,
                                       chargePerTrigger,
                                       cooldownSeconds);
        payload.Validate();
        return payload;
    }

    /// <summary>
    /// Creates a validated Returning Projectiles payload used by defaults and the two baseline power-ups.
    /// </summary>
    /// <param name="pathMode">Return path strategy.</param>
    /// <param name="hitPolicy">Return hit policy.</param>
    /// <param name="spinDuringFlight">Whether the projectile continuously spins.</param>
    /// <param name="allowConcurrentActiveProjectiles">Whether a non-toggleable active can overlap live projectiles.</param>
    /// <returns>Validated modular payload containing Returning Projectiles data.</returns>
    private static PowerUpModuleData CreateReturningProjectilesPayload(ProjectileReturnPathMode pathMode,
                                                                       ProjectileReturnHitPolicy hitPolicy,
                                                                       bool spinDuringFlight,
                                                                       bool allowConcurrentActiveProjectiles)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.ReturningProjectiles.Configure(null,
                                               true,
                                               true,
                                               true,
                                               true,
                                               pathMode,
                                               1.25f,
                                               1f,
                                               1f,
                                               ProjectileOutboundHitPolicy.NaturalPenetration,
                                               1,
                                               ProjectileReturnStartMode.AutomaticDelay,
                                               0f,
                                               false,
                                               false,
                                               0.5f,
                                               0.5f,
                                               1f,
                                               1f,
                                               spinDuringFlight,
                                               540f,
                                               ProjectileReturnRotationAxis.Vertical,
                                               720f,
                                               ProjectileReturnRotationAxis.Vertical,
                                               hitPolicy,
                                               1,
                                               false,
                                               1f,
                                               0.5f,
                                               0.25f,
                                               0.2f,
                                               true,
                                               true,
                                               true,
                                               true,
                                               true,
                                               true,
                                               false,
                                               allowConcurrentActiveProjectiles);
        payload.Validate();
        return payload;
    }

    /// <summary>
    /// Creates the default charge-shot trigger payload with visible upper-body charge and release presentation.
    /// </summary>
    /// <param name="requiredCharge">Charge threshold required for a valid release.</param>
    /// <param name="maximumCharge">Maximum charge retained by the trigger.</param>
    /// <param name="chargeRatePerSecond">Charge accumulated per second while held.</param>
    /// <returns>Validated hold-charge module payload used by generated base configurations.</returns>
    private static PowerUpModuleData CreateHoldChargePayload(float requiredCharge,
                                                             float maximumCharge,
                                                             float chargeRatePerSecond)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.HoldCharge.Configure(requiredCharge, maximumCharge, chargeRatePerSecond);
        payload.HoldCharge.ConfigureAnimations(DefaultChargeAnimationClipSlot,
                                               DefaultReleaseAnimationClipSlot);
        payload.Validate();
        return payload;
    }

    private static PowerUpModuleData CreateSuppressShootingPayload(bool suppressBaseShootingWhileActive)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.SuppressShooting.Configure(suppressBaseShootingWhileActive);
        payload.Validate();
        return payload;
    }

    private static PowerUpModuleData CreateProjectilePatternPayload(int projectileCount, float coneAngleDegrees)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.ProjectilePatternCone.Configure(projectileCount, coneAngleDegrees);
        payload.Validate();
        return payload;
    }

    private static PowerUpModuleData CreateTriggerEventPayload(PowerUpTriggerEventType eventType)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.TriggerEvent.Configure(eventType);
        payload.Validate();
        return payload;
    }

    private static PowerUpModuleData CreateHealPayload(float healAmount)
    {
        PowerUpModuleData payload = new PowerUpModuleData();
        payload.HealMissingHealth.Configure(healAmount);
        payload.Validate();
        return payload;
    }
    #endregion
}
