using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles boss pattern presets by reusing the same module slots authored by normal enemy Pattern Assemble.
/// </summary>
internal static class EnemyBossPatternBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one boss pattern preset using a baker-provided prefab resolver for minion spawn rules.
    /// </summary>
    /// <param name="preset">Boss pattern preset to compile.</param>
    /// <param name="globalEngagementSettings">Generic offensive engagement feedback settings resolved from the visual preset.</param>
    /// <param name="minionPrefabResolver">Callback that converts a minion prefab GameObject to an entity prefab.</param>
    /// <returns>Compiled boss pattern data.</returns>
    public static EnemyCompiledBossPatternBakeResult Compile(EnemyBossPatternPreset preset,
                                                             EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                             System.Func<GameObject, Entity> minionPrefabResolver)
    {
        EnemyCompiledBossPatternBakeResult result = new EnemyCompiledBossPatternBakeResult();

        if (preset == null)
            return result;

        ConfigureExtractionSettings(preset.ExtractionSettings, result);
        EnemyModulesAndPatternsPreset sharedPreset = preset.SourcePatternsPreset;

        if (sharedPreset != null)
        {
            CompileInteractions(sharedPreset, preset.Interactions, globalEngagementSettings, result);
            EnemyBossDropExtractionBakeUtility.Compile(sharedPreset, preset.DropExtraction, minionPrefabResolver, result);
            ConfigureInitialPattern(result);
        }

        TryAppendMinionRules(preset.MinionSpawn, minionPrefabResolver, result);
        return result;
    }
    #endregion

    #region Pattern Compile
    /// <summary>
    /// Compiles all boss pattern candidates and their internal module candidate buffers.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="interactions">Ordered boss interaction definitions.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void CompileInteractions(EnemyModulesAndPatternsPreset sharedPreset,
                                            IReadOnlyList<EnemyBossPatternInteractionDefinition> interactions,
                                            EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                            EnemyCompiledBossPatternBakeResult result)
    {
        if (sharedPreset == null || interactions == null || result == null)
            return;

        for (int interactionIndex = 0; interactionIndex < interactions.Count; interactionIndex++)
        {
            EnemyBossPatternInteractionDefinition interaction = interactions[interactionIndex];

            if (interaction == null || !interaction.Enabled)
                continue;

            int patternBufferIndex = result.Interactions.Count;
            result.Interactions.Add(new EnemyBossPatternInteractionElement
            {
                InteractionIndex = math.max(0, interactionIndex),
                InteractionType = interaction.InteractionType,
                MinimumActiveSeconds = math.max(0f, interaction.MinimumActiveSeconds),
                SelectionWeight = ResolveSelectionWeight(interaction.SelectionWeight),
                MinimumMissingHealthPercent = math.saturate(interaction.MinimumMissingHealthPercent),
                MaximumMissingHealthPercent = math.saturate(interaction.MaximumMissingHealthPercent),
                MinimumElapsedSeconds = math.max(0f, interaction.MinimumElapsedSeconds),
                MaximumElapsedSeconds = math.max(0f, interaction.MaximumElapsedSeconds),
                MinimumTravelledDistance = math.max(0f, interaction.MinimumTravelledDistance),
                MaximumTravelledDistance = math.max(0f, interaction.MaximumTravelledDistance),
                MinimumPlayerDistance = math.max(0f, interaction.MinimumPlayerDistance),
                MaximumPlayerDistance = math.max(0f, interaction.MaximumPlayerDistance),
                RecentlyDamagedWindowSeconds = math.max(0f, interaction.RecentlyDamagedWindowSeconds),
                HasCustomMovement = 0,
                FirstShooterConfigIndex = 0,
                ShooterConfigCount = 0,
                FirstBombardierConfigIndex = 0,
                BombardierConfigCount = 0,
                FirstOffensiveEngagementConfigIndex = 0,
                OffensiveEngagementConfigCount = 0,
                PatternConfig = EnemyPatternDefaultsUtility.CreatePatternConfig()
            });
            EnemyBossPatternModuleBakeUtility.CompilePatternModuleCandidates(sharedPreset,
                                                                             interaction,
                                                                             patternBufferIndex,
                                                                             globalEngagementSettings,
                                                                             result);
        }
    }

    /// <summary>
    /// Copies high-level pattern extraction settings into the compiled boss result.
    /// </summary>
    /// <param name="settings">Source extraction settings from the boss preset.</param>
    /// <param name="result">Mutable boss compile result.</param>
    internal static void ConfigureExtractionSettings(EnemyBossPatternExtractionSettings settings,
                                                     EnemyCompiledBossPatternBakeResult result)
    {
        if (result == null)
            return;

        if (settings == null)
        {
            result.RerollWhenCurrentPatternBecomesInvalid = true;
            result.UseElapsedIntervalExtraction = true;
            result.ElapsedIntervalSeconds = 4f;
            result.UseMissingHealthStepExtraction = true;
            result.MissingHealthStepPercent = 0.25f;
            result.MinimumSecondsBetweenExtractions = 1f;
            return;
        }

        result.RerollWhenCurrentPatternBecomesInvalid = settings.RerollWhenCurrentPatternBecomesInvalid;
        result.MinimumSecondsBetweenExtractions = math.max(0f, settings.MinimumSecondsBetweenExtractions);
        result.UseElapsedIntervalExtraction = settings.UseElapsedIntervalExtraction;
        result.ElapsedIntervalSeconds = math.max(0f, settings.ElapsedIntervalSeconds);
        result.UseMissingHealthStepExtraction = settings.UseMissingHealthStepExtraction;
        result.MissingHealthStepPercent = math.saturate(settings.MissingHealthStepPercent);
        result.UseTravelledDistanceExtraction = settings.UseTravelledDistanceExtraction;
        result.TravelledDistanceSinceLastExtraction = math.max(0f, settings.TravelledDistanceSinceLastExtraction);
        result.PlayerDistanceCondition = settings.PlayerDistanceCondition;
        result.PlayerDistanceThreshold = math.max(0f, settings.PlayerDistanceThreshold);
        result.PlayerDistanceHoldSeconds = math.max(0f, settings.PlayerDistanceHoldSeconds);
        result.UseDamageWindowExtraction = settings.UseDamageWindowExtraction;
        result.DamageWindowSeconds = math.max(0f, settings.DamageWindowSeconds);
        result.DamageThreshold = math.max(0f, settings.DamageThreshold);
    }

    /// <summary>
    /// Resolves a safe selection weight while preserving legacy assets that predate the field.
    /// </summary>
    /// <param name="selectionWeight">Authored selection weight.</param>
    /// <returns>Positive weight used by runtime extraction.</returns>
    internal static float ResolveSelectionWeight(float selectionWeight)
    {
        if (selectionWeight > 0f)
            return selectionWeight;

        return 1f;
    }

    /// <summary>
    /// Applies one optional short-range slot to a compiled pattern config.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="shortRangeInteraction">Short-range slot to apply.</param>
    /// <param name="patternConfig">Mutable compiled pattern config.</param>
    internal static void ApplyShortRangeSlot(EnemyModulesAndPatternsPreset sharedPreset,
                                             EnemyPatternShortRangeInteractionAssembly shortRangeInteraction,
                                             ref EnemyPatternConfig patternConfig)
    {
        if (sharedPreset == null || shortRangeInteraction == null || !shortRangeInteraction.IsEnabled)
            return;

        EnemyModulesAndPatternsBakeUtility.TryApplyShortRangeInteractionModule(sharedPreset,
                                                                               shortRangeInteraction.Binding,
                                                                               shortRangeInteraction.ActivationRange,
                                                                               shortRangeInteraction.ReleaseDistanceBuffer,
                                                                               ref patternConfig);
    }

    /// <summary>
    /// Applies one optional weapon slot to a compiled pattern result.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="weaponInteraction">Weapon slot to apply.</param>
    /// <param name="result">Mutable compiled pattern result.</param>
    internal static void ApplyWeaponSlot(EnemyModulesAndPatternsPreset sharedPreset,
                                         EnemyPatternWeaponInteractionAssembly weaponInteraction,
                                         ref EnemyCompiledPatternBakeResult result)
    {
        if (sharedPreset == null || weaponInteraction == null || !weaponInteraction.IsEnabled)
            return;

        EnemyModulesAndPatternsBakeUtility.TryAddWeaponInteractionModule(sharedPreset,
                                                                         weaponInteraction.Binding,
                                                                         weaponInteraction.UseMinimumRange,
                                                                         weaponInteraction.MinimumRange,
                                                                         weaponInteraction.UseMaximumRange,
                                                                         weaponInteraction.MaximumRange,
                                                                         weaponInteraction.ExclusiveLookDirectionControl,
                                                                         weaponInteraction.ActivationGates,
                                                                         weaponInteraction.MaximumActivationSpeed,
                                                                         weaponInteraction.RecentlyDamagedWindowSeconds,
                                                                         ref result);
    }

    /// <summary>
    /// Appends one compiled pattern shooter slice to the boss-owned source buffer.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern providing shooter configs.</param>
    /// <param name="result">Mutable boss compile result.</param>
    /// <returns>First appended shooter config index.</returns>
    internal static int AppendShooterConfigs(EnemyCompiledPatternBakeResult compiledPattern, EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return 0;

        int firstShooterConfigIndex = result.ShooterConfigs.Count;

        for (int shooterIndex = 0; shooterIndex < compiledPattern.ShooterConfigs.Count; shooterIndex++)
            result.ShooterConfigs.Add(compiledPattern.ShooterConfigs[shooterIndex]);

        return firstShooterConfigIndex;
    }

    /// <summary>
    /// Appends one compiled pattern Bombardier slice to the boss-owned source buffer.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern providing Bombardier configs.</param>
    /// <param name="result">Mutable boss compile result.</param>
    /// <returns>First appended Bombardier config index.</returns>
    internal static int AppendBombardierConfigs(EnemyCompiledPatternBakeResult compiledPattern, EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return 0;

        int firstBombardierConfigIndex = result.BombardierConfigs.Count;

        for (int bombardierIndex = 0; bombardierIndex < compiledPattern.BombardierConfigs.Count; bombardierIndex++)
            result.BombardierConfigs.Add(compiledPattern.BombardierConfigs[bombardierIndex]);

        return firstBombardierConfigIndex;
    }

    /// <summary>
    /// Appends one compiled pattern Power-Up Stealer slice to the boss-owned source buffer.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern providing Power-Up Stealer configs.</param>
    /// <param name="result">Mutable boss compile result.</param>
    /// <returns>First appended Power-Up Stealer config index.</returns>
    internal static int AppendPowerUpStealerConfigs(EnemyCompiledPatternBakeResult compiledPattern, EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return 0;

        int firstStealerConfigIndex = result.PowerUpStealerConfigs.Count;

        for (int stealerIndex = 0; stealerIndex < compiledPattern.PowerUpStealerConfigs.Count; stealerIndex++)
            result.PowerUpStealerConfigs.Add(compiledPattern.PowerUpStealerConfigs[stealerIndex]);

        return firstStealerConfigIndex;
    }

    /// <summary>
    /// Appends one compiled offensive engagement slice to the boss-owned source buffer.
    /// </summary>
    /// <param name="engagementConfigs">Compiled engagement configs for one boss layer.</param>
    /// <param name="result">Mutable boss compile result.</param>
    /// <returns>First appended engagement config index.</returns>
    internal static int AppendOffensiveEngagementConfigs(IReadOnlyList<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                                         EnemyCompiledBossPatternBakeResult result)
    {
        if (engagementConfigs == null || result == null)
            return 0;

        int firstConfigIndex = result.OffensiveEngagementConfigs.Count;

        for (int configIndex = 0; configIndex < engagementConfigs.Count; configIndex++)
            result.OffensiveEngagementConfigs.Add(engagementConfigs[configIndex]);

        return firstConfigIndex;
    }

    /// <summary>
    /// Configures the normal enemy pattern output used by the authoring baker as the boss spawn baseline.
    /// </summary>
    /// <param name="result">Mutable boss compile result.</param>
    private static void ConfigureInitialPattern(EnemyCompiledBossPatternBakeResult result)
    {
        if (result == null)
            return;

        result.InitialPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
        result.InitialPattern.ShooterProjectilePrefab = result.ShooterProjectilePrefab;
        result.InitialPattern.ShooterProjectilePoolInitialCapacity = result.ShooterProjectilePoolInitialCapacity;
        result.InitialPattern.ShooterProjectilePoolExpandBatch = result.ShooterProjectilePoolExpandBatch;
        result.InitialPattern.HasShooterRuntimeSettings = result.HasShooterRuntimeSettings;
        result.InitialPattern.BombardierBombPrefab = result.BombardierBombPrefab;
        result.InitialPattern.BombardierExplosionVfxPrefab = result.BombardierExplosionVfxPrefab;
        result.InitialPattern.BombardierScaleExplosionVfxToDamageRadius = result.BombardierScaleExplosionVfxToDamageRadius;
        result.InitialPattern.BombardierExplosionVfxScaleMultiplier = result.BombardierExplosionVfxScaleMultiplier;
        result.InitialPattern.HasBombardierRuntimeSettings = result.HasBombardierRuntimeSettings;
        result.InitialPattern.HasCustomMovement = ResolveAnyModuleCandidateHasCustomMovement(result);
        EnemyBossDropExtractionBakeUtility.CopyBossDropUnionToInitialPattern(result);
    }

    /// <summary>
    /// Checks whether any compiled boss module candidate requires the custom movement system.
    /// </summary>
    /// <param name="result">Compiled boss result.</param>
    /// <returns>True when any module candidate needs custom pattern movement.</returns>
    private static bool ResolveAnyModuleCandidateHasCustomMovement(EnemyCompiledBossPatternBakeResult result)
    {
        if (result == null)
            return false;

        for (int candidateIndex = 0; candidateIndex < result.ModuleCandidates.Count; candidateIndex++)
        {
            if (result.ModuleCandidates[candidateIndex].HasCustomMovement != 0)
                return true;
        }

        return false;
    }
    #endregion

    #region Minions
    /// <summary>
    /// Converts minion authoring rules into baked spawn entries with automatic pool sizes.
    /// </summary>
    /// <param name="minionSpawn">Source minion spawn settings.</param>
    /// <param name="minionPrefabResolver">Callback used to bake prefab references.</param>
    /// <param name="result">Mutable boss result.</param>
    private static void TryAppendMinionRules(EnemyBossMinionSpawnSettings minionSpawn,
                                             System.Func<GameObject, Entity> minionPrefabResolver,
                                             EnemyCompiledBossPatternBakeResult result)
    {
        if (minionSpawn == null || result == null || !minionSpawn.Enabled)
            return;

        IReadOnlyList<EnemyBossMinionSpawnRule> rules = minionSpawn.Rules;

        if (rules == null)
            return;

        float3 spawnOffset = ResolveMinionSpawnOffset(minionSpawn.SpawnOffset);

        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            EnemyBossMinionSpawnRule rule = rules[ruleIndex];

            if (rule == null || !rule.Enabled || rule.MinionPrefab == null)
                continue;

            Entity prefabEntity = minionPrefabResolver != null
                ? minionPrefabResolver(rule.MinionPrefab)
                : Entity.Null;

            if (prefabEntity == Entity.Null)
                continue;

            float intervalSeconds = rule.IntervalSeconds > 0f
                ? rule.IntervalSeconds
                : minionSpawn.FallbackIntervalSeconds;

            result.MinionSpawns.Add(new EnemyBossMinionSpawnElement
            {
                PrefabEntity = prefabEntity,
                Trigger = rule.Trigger,
                IntervalSeconds = math.max(0.01f, intervalSeconds),
                BossHitCooldownSeconds = math.max(0f, rule.BossHitCooldownSeconds),
                HealthThresholdPercent = math.saturate(rule.HealthThresholdPercent),
                SpawnCount = math.max(0, rule.SpawnCount),
                MaxAliveMinions = math.max(0, rule.MaxAliveMinions),
                SpawnRadius = math.max(0f, rule.SpawnRadius),
                SpawnOffset = spawnOffset,
                DespawnDistance = math.max(0f, rule.DespawnDistance),
                ExperienceDropMultiplier = math.max(0f, rule.ExperienceDropMultiplier),
                ExtraComboPointsMultiplier = math.max(0f, rule.ExtraComboPointsMultiplier),
                FutureDropsMultiplier = math.max(0f, rule.FutureDropsMultiplier),
                AutomaticPoolSize = math.max(0, rule.CalculateAutomaticPoolSize()),
                PoolExpandBatch = math.max(1, minionSpawn.PoolExpandBatch),
                KillMinionsOnBossDeath = minionSpawn.KillMinionsOnBossDeath ? (byte)1 : (byte)0,
                RequireMinionsKilledForRunCompletion = !minionSpawn.KillMinionsOnBossDeath &&
                                                        minionSpawn.RequireMinionsKilledForRunCompletion
                    ? (byte)1
                    : (byte)0,
                PoolEntity = Entity.Null,
                NextSpawnTime = 0f,
                LastObservedDamageLifetimeSeconds = 0f,
                Triggered = 0,
                Initialized = 0
            });
        }
    }

    /// <summary>
    /// Resolves a finite boss-minion spawn offset while preserving the authored default for invalid components.
    /// </summary>
    /// <param name="spawnOffset">Authored shared minion spawn offset.</param>
    /// <returns>Finite offset copied into every baked minion rule.</returns>
    private static float3 ResolveMinionSpawnOffset(Vector3 spawnOffset)
    {
        Vector3 defaultSpawnOffset = EnemyBossMinionSpawnSettings.DefaultSpawnOffset;
        return new float3(ResolveFiniteFloat(spawnOffset.x, defaultSpawnOffset.x),
                          ResolveFiniteFloat(spawnOffset.y, defaultSpawnOffset.y),
                          ResolveFiniteFloat(spawnOffset.z, defaultSpawnOffset.z));
    }

    /// <summary>
    /// Resolves one finite authored float component for bake-time ECS data.
    /// </summary>
    /// <param name="value">Authored component value.</param>
    /// <param name="fallback">Fallback component used when the authored value is not finite.</param>
    /// <returns>Finite component value safe for runtime math.</returns>
    private static float ResolveFiniteFloat(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;

        return value;
    }
    #endregion

    #region Runtime Projectile
    /// <summary>
    /// Copies the first available shooter runtime projectile settings from a compiled pattern into the boss result.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern that may contain shooter runtime settings.</param>
    /// <param name="result">Mutable boss bake result.</param>
    internal static void TryAssignShooterRuntimeSettings(EnemyCompiledPatternBakeResult compiledPattern,
                                                         EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return;

        if (result.HasShooterRuntimeSettings || !compiledPattern.HasShooterRuntimeSettings)
            return;

        result.ShooterProjectilePrefab = compiledPattern.ShooterProjectilePrefab;
        result.ShooterProjectilePoolInitialCapacity = compiledPattern.ShooterProjectilePoolInitialCapacity;
        result.ShooterProjectilePoolExpandBatch = compiledPattern.ShooterProjectilePoolExpandBatch;
        result.HasShooterRuntimeSettings = true;
    }

    /// <summary>
    /// Copies the first available Bombardier runtime bomb settings from a compiled pattern into the boss result.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern that may contain Bombardier runtime settings.</param>
    /// <param name="result">Mutable boss bake result.</param>
    internal static void TryAssignBombardierRuntimeSettings(EnemyCompiledPatternBakeResult compiledPattern,
                                                            EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return;

        if (result.HasBombardierRuntimeSettings || !compiledPattern.HasBombardierRuntimeSettings)
            return;

        result.BombardierBombPrefab = compiledPattern.BombardierBombPrefab;
        result.BombardierExplosionVfxPrefab = compiledPattern.BombardierExplosionVfxPrefab;
        result.BombardierScaleExplosionVfxToDamageRadius = compiledPattern.BombardierScaleExplosionVfxToDamageRadius;
        result.BombardierExplosionVfxScaleMultiplier = compiledPattern.BombardierExplosionVfxScaleMultiplier;
        result.HasBombardierRuntimeSettings = true;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores compiled boss pattern data before it is written by EnemyAuthoringBaker.
/// </summary>
internal sealed class EnemyCompiledBossPatternBakeResult
{
    #region Fields
    public EnemyCompiledPatternBakeResult InitialPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
    public EnemyCompiledPatternBakeResult BossDropUnionPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
    public readonly List<EnemyBossPatternInteractionElement> Interactions = new List<EnemyBossPatternInteractionElement>();
    public readonly List<EnemyBossPatternModuleExtractionElement> ModuleExtractions = new List<EnemyBossPatternModuleExtractionElement>();
    public readonly List<EnemyBossPatternModuleCandidateElement> ModuleCandidates = new List<EnemyBossPatternModuleCandidateElement>();
    public readonly List<EnemyShooterConfigElement> ShooterConfigs = new List<EnemyShooterConfigElement>();
    public readonly List<EnemyBombardierConfigElement> BombardierConfigs = new List<EnemyBombardierConfigElement>();
    public readonly List<EnemyPowerUpStealerConfigElement> PowerUpStealerConfigs = new List<EnemyPowerUpStealerConfigElement>();
    public readonly List<EnemyOffensiveEngagementConfigElement> OffensiveEngagementConfigs = new List<EnemyOffensiveEngagementConfigElement>();
    public readonly List<EnemyBossMinionSpawnElement> MinionSpawns = new List<EnemyBossMinionSpawnElement>();
    public readonly List<EnemyBossDropCandidateElement> DropCandidates = new List<EnemyBossDropCandidateElement>();
    public readonly List<EnemyExperienceDropModuleElement> BossDropExperienceModules = new List<EnemyExperienceDropModuleElement>();
    public readonly List<EnemyExperienceDropDefinitionElement> BossDropExperienceDefinitions = new List<EnemyExperienceDropDefinitionElement>();
    public readonly List<EnemyRecoveryDropModuleElement> BossDropRecoveryModules = new List<EnemyRecoveryDropModuleElement>();
    public readonly List<EnemyRecoveryDropDefinitionElement> BossDropRecoveryDefinitions = new List<EnemyRecoveryDropDefinitionElement>();
    public readonly List<EnemyExtraComboPointsModuleElement> BossDropExtraComboPointsModules = new List<EnemyExtraComboPointsModuleElement>();
    public readonly List<EnemyExtraComboPointsConditionElement> BossDropExtraComboPointsConditions = new List<EnemyExtraComboPointsConditionElement>();
    public GameObject ShooterProjectilePrefab;
    public int ShooterProjectilePoolInitialCapacity;
    public int ShooterProjectilePoolExpandBatch;
    public bool HasShooterRuntimeSettings;
    public GameObject BombardierBombPrefab;
    public GameObject BombardierExplosionVfxPrefab;
    public bool BombardierScaleExplosionVfxToDamageRadius;
    public float BombardierExplosionVfxScaleMultiplier;
    public bool HasBombardierRuntimeSettings;
    public bool BossDropExtractionEnabled;
    public EnemyBossDropExtractionMode BossDropExtractionMode;
    public bool RerollWhenCurrentPatternBecomesInvalid;
    public bool UseElapsedIntervalExtraction;
    public bool UseMissingHealthStepExtraction;
    public bool UseTravelledDistanceExtraction;
    public bool UseDamageWindowExtraction;
    public EnemyBossPatternPlayerDistanceCondition PlayerDistanceCondition;
    public float MinimumSecondsBetweenExtractions;
    public float ElapsedIntervalSeconds;
    public float MissingHealthStepPercent;
    public float TravelledDistanceSinceLastExtraction;
    public float PlayerDistanceThreshold;
    public float PlayerDistanceHoldSeconds;
    public float DamageWindowSeconds;
    public float DamageThreshold;
    #endregion
}
