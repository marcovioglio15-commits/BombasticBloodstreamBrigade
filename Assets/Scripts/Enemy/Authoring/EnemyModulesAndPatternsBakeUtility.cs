using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Compiles the shared Modules & Patterns preset referenced by one enemy advanced-pattern preset.
/// </summary>
internal static class EnemyModulesAndPatternsBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles the active shared pattern loadout referenced by one enemy advanced-pattern preset.
    /// </summary>
    /// <param name="preset">Enemy-specific advanced-pattern preset that owns the shared preset reference and loadout.</param>
    /// <returns>The compiled bake result ready for ECS authoring.</returns>
    public static EnemyCompiledPatternBakeResult Compile(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(preset);

        if (preset == null)
            return result;

        EnemyModulesAndPatternsPreset sharedPreset = preset.ModulesAndPatternsPreset;

        if (sharedPreset == null)
            return result;

        EnemyModulesPatternDefinition selectedPattern = EnemyModulesAndPatternsSelectionUtility.ResolveSelectedPattern(preset);

        if (selectedPattern == null)
            return result;

        return CompilePattern(sharedPreset, selectedPattern, result);
    }

    /// <summary>
    /// Compiles one explicit shared pattern by ID so boss presets can reuse normal enemy pattern assemblies.
    /// </summary>
    /// <param name="sharedPreset">Source normal enemy Modules & Patterns preset.</param>
    /// <param name="patternId">Pattern ID to compile.</param>
    /// <returns>The compiled bake result, or a default result when the pattern is unavailable.</returns>
    public static EnemyCompiledPatternBakeResult CompilePatternById(EnemyModulesAndPatternsPreset sharedPreset, string patternId)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);

        if (sharedPreset == null)
            return result;

        EnemyModulesPatternDefinition selectedPattern = sharedPreset.ResolvePatternById(patternId);

        if (selectedPattern == null)
            return result;

        return CompilePattern(sharedPreset, selectedPattern, result);
    }

    /// <summary>
    /// Applies one core movement module binding to a compiled pattern result.
    /// </summary>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="binding">Module binding being compiled.</param>
    /// <param name="result">Mutable compiled result.</param>
    /// <returns>True when a legal core movement module was applied.</returns>
    internal static bool TryApplyCoreMovementModule(EnemyModulesAndPatternsPreset sharedPreset,
                                                    EnemyPatternModuleBinding binding,
                                                    ref EnemyCompiledPatternBakeResult result)
    {
        if (sharedPreset == null || binding == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind);

        if (moduleKind != EnemyPatternModuleKind.Stationary &&
            moduleKind != EnemyPatternModuleKind.Grunt &&
            moduleKind != EnemyPatternModuleKind.Wanderer)
        {
            return false;
        }

        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
        int selectedPriority = 0;
        bool hasCustomMovement = result.HasCustomMovement;
        EnemyAdvancedPatternBakeUtility.TryApplyMovementModule(moduleKind,
                                                               resolvedPayload,
                                                               ref result.PatternConfig,
                                                               ref selectedPriority,
                                                               ref hasCustomMovement);
        result.HasCustomMovement = hasCustomMovement;
        return true;
    }

    /// <summary>
    /// Applies one short-range interaction module binding to a compiled pattern config.
    /// </summary>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="binding">Module binding being compiled.</param>
    /// <param name="activationRange">Player distance that activates the interaction.</param>
    /// <param name="releaseDistanceBuffer">Extra release buffer added after activation.</param>
    /// <param name="patternConfig">Mutable compiled pattern config.</param>
    /// <returns>True when a legal short-range interaction module was applied.</returns>
    internal static bool TryApplyShortRangeInteractionModule(EnemyModulesAndPatternsPreset sharedPreset,
                                                             EnemyPatternModuleBinding binding,
                                                             float activationRange,
                                                             float releaseDistanceBuffer,
                                                             ref EnemyPatternConfig patternConfig)
    {
        if (sharedPreset == null || binding == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind);

        if (moduleKind != EnemyPatternModuleKind.Grunt &&
            moduleKind != EnemyPatternModuleKind.Coward &&
            moduleKind != EnemyPatternModuleKind.ShortRangeDash)
        {
            return false;
        }

        patternConfig.HasShortRangeInteraction = 1;
        patternConfig.ShortRangeActivationRange = math.max(0f, activationRange);
        patternConfig.ShortRangeReleaseDistanceBuffer = math.max(0f, releaseDistanceBuffer);

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Coward:
                patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Coward;
                break;

            case EnemyPatternModuleKind.ShortRangeDash:
                patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.ShortRangeDash;
                break;

            default:
                patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Grunt;
                break;
        }

        if (moduleKind == EnemyPatternModuleKind.Grunt)
            return true;

        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Coward:
                ApplyShortRangeCowardPayload(resolvedPayload, ref patternConfig);
                break;

            case EnemyPatternModuleKind.ShortRangeDash:
                EnemyAdvancedPatternBakeUtility.ApplyShortRangeDashPayload(resolvedPayload, ref patternConfig);
                break;
        }

        return true;
    }

    /// <summary>
    /// Adds one weapon interaction module binding to a compiled pattern result.
    /// </summary>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="binding">Module binding being compiled.</param>
    /// <param name="useMinimumRange">True when minimum range gating should be applied.</param>
    /// <param name="minimumRange">Authored minimum player range.</param>
    /// <param name="useMaximumRange">True when maximum range gating should be applied.</param>
    /// <param name="maximumRange">Authored maximum player range.</param>
    /// <param name="exclusiveLookDirectionControl">True when this weapon controls look direction while active.</param>
    /// <param name="activationGates">Additional non-range activation gates.</param>
    /// <param name="maximumActivationSpeed">Maximum enemy speed allowed by speed gating.</param>
    /// <param name="recentlyDamagedWindowSeconds">Recent damage window used by damage gating.</param>
    /// <param name="result">Mutable compiled result.</param>
    /// <returns>True when a legal weapon interaction module was applied.</returns>
    internal static bool TryAddWeaponInteractionModule(EnemyModulesAndPatternsPreset sharedPreset,
                                                       EnemyPatternModuleBinding binding,
                                                       bool useMinimumRange,
                                                       float minimumRange,
                                                       bool useMaximumRange,
                                                       float maximumRange,
                                                       bool exclusiveLookDirectionControl,
                                                       EnemyWeaponInteractionActivationGate activationGates,
                                                       float maximumActivationSpeed,
                                                       float recentlyDamagedWindowSeconds,
                                                       ref EnemyCompiledPatternBakeResult result)
    {
        if (sharedPreset == null || binding == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind);
        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Shooter:
                return TryAddShooterWeaponModule(resolvedPayload,
                                                 useMinimumRange,
                                                 minimumRange,
                                                 useMaximumRange,
                                                 maximumRange,
                                                 exclusiveLookDirectionControl,
                                                 activationGates,
                                                 maximumActivationSpeed,
                                                 recentlyDamagedWindowSeconds,
                                                 ref result);

            case EnemyPatternModuleKind.PowerUpStealer:
                return TryAddPowerUpStealerWeaponModule(resolvedPayload,
                                                        useMinimumRange,
                                                        minimumRange,
                                                        useMaximumRange,
                                                        maximumRange,
                                                        exclusiveLookDirectionControl,
                                                        activationGates,
                                                        maximumActivationSpeed,
                                                        recentlyDamagedWindowSeconds,
                                                        ref result);

            default:
                return false;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends one Shooter module and applies shared Weapon Interaction gating.
    /// </summary>
    /// <param name="resolvedPayload">Resolved Shooter payload.</param>
    /// <param name="useMinimumRange">True when minimum range gating should be applied.</param>
    /// <param name="minimumRange">Authored minimum player range.</param>
    /// <param name="useMaximumRange">True when maximum range gating should be applied.</param>
    /// <param name="maximumRange">Authored maximum player range.</param>
    /// <param name="exclusiveLookDirectionControl">True when this weapon controls look direction while active.</param>
    /// <param name="activationGates">Additional non-range activation gates.</param>
    /// <param name="maximumActivationSpeed">Maximum enemy speed allowed by speed gating.</param>
    /// <param name="recentlyDamagedWindowSeconds">Recent damage window used by damage gating.</param>
    /// <param name="result">Mutable compiled result.</param>
    /// <returns>True when a Shooter config was appended.</returns>
    private static bool TryAddShooterWeaponModule(EnemyPatternModulePayloadData resolvedPayload,
                                                  bool useMinimumRange,
                                                  float minimumRange,
                                                  bool useMaximumRange,
                                                  float maximumRange,
                                                  bool exclusiveLookDirectionControl,
                                                  EnemyWeaponInteractionActivationGate activationGates,
                                                  float maximumActivationSpeed,
                                                  float recentlyDamagedWindowSeconds,
                                                  ref EnemyCompiledPatternBakeResult result)
    {
        int previousConfigCount = result.ShooterConfigs.Count;
        EnemyAdvancedPatternBakeUtility.TryAddShooterModule(resolvedPayload, result.ShooterConfigs, ref result);

        for (int shooterIndex = previousConfigCount; shooterIndex < result.ShooterConfigs.Count; shooterIndex++)
        {
            EnemyShooterConfigElement shooterConfig = result.ShooterConfigs[shooterIndex];
            ApplyWeaponGates(ref shooterConfig,
                             useMinimumRange,
                             minimumRange,
                             useMaximumRange,
                             maximumRange,
                             exclusiveLookDirectionControl,
                             activationGates,
                             maximumActivationSpeed,
                             recentlyDamagedWindowSeconds);
            result.ShooterConfigs[shooterIndex] = shooterConfig;
        }

        return result.ShooterConfigs.Count > previousConfigCount;
    }

    /// <summary>
    /// Appends one Power-Up Stealer module and applies shared Weapon Interaction gating.
    /// </summary>
    /// <param name="resolvedPayload">Resolved Power-Up Stealer payload.</param>
    /// <param name="useMinimumRange">True when minimum range gating should be applied.</param>
    /// <param name="minimumRange">Authored minimum player range.</param>
    /// <param name="useMaximumRange">True when maximum range gating should be applied.</param>
    /// <param name="maximumRange">Authored maximum player range.</param>
    /// <param name="exclusiveLookDirectionControl">True when this weapon controls look direction while active.</param>
    /// <param name="activationGates">Additional non-range activation gates.</param>
    /// <param name="maximumActivationSpeed">Maximum enemy speed allowed by speed gating.</param>
    /// <param name="recentlyDamagedWindowSeconds">Recent damage window used by damage gating.</param>
    /// <param name="result">Mutable compiled result.</param>
    /// <returns>True when a Power-Up Stealer config was appended.</returns>
    private static bool TryAddPowerUpStealerWeaponModule(EnemyPatternModulePayloadData resolvedPayload,
                                                         bool useMinimumRange,
                                                         float minimumRange,
                                                         bool useMaximumRange,
                                                         float maximumRange,
                                                         bool exclusiveLookDirectionControl,
                                                         EnemyWeaponInteractionActivationGate activationGates,
                                                         float maximumActivationSpeed,
                                                         float recentlyDamagedWindowSeconds,
                                                         ref EnemyCompiledPatternBakeResult result)
    {
        int previousConfigCount = result.PowerUpStealerConfigs.Count;
        EnemyAdvancedPatternBakeUtility.TryAddPowerUpStealerModule(resolvedPayload, result.PowerUpStealerConfigs);

        for (int stealerIndex = previousConfigCount; stealerIndex < result.PowerUpStealerConfigs.Count; stealerIndex++)
        {
            EnemyPowerUpStealerConfigElement stealerConfig = result.PowerUpStealerConfigs[stealerIndex];
            ApplyWeaponGates(ref stealerConfig,
                             useMinimumRange,
                             minimumRange,
                             useMaximumRange,
                             maximumRange,
                             exclusiveLookDirectionControl,
                             activationGates,
                             maximumActivationSpeed,
                             recentlyDamagedWindowSeconds);
            result.PowerUpStealerConfigs[stealerIndex] = stealerConfig;
        }

        return result.PowerUpStealerConfigs.Count > previousConfigCount;
    }

    /// <summary>
    /// Applies shared Weapon Interaction gates to one Shooter config.
    /// </summary>
    /// <param name="config">Mutable Shooter config.</param>
    /// <param name="useMinimumRange">True when minimum range gating should be applied.</param>
    /// <param name="minimumRange">Authored minimum player range.</param>
    /// <param name="useMaximumRange">True when maximum range gating should be applied.</param>
    /// <param name="maximumRange">Authored maximum player range.</param>
    /// <param name="exclusiveLookDirectionControl">True when this weapon controls look direction while active.</param>
    /// <param name="activationGates">Additional non-range activation gates.</param>
    /// <param name="maximumActivationSpeed">Maximum enemy speed allowed by speed gating.</param>
    /// <param name="recentlyDamagedWindowSeconds">Recent damage window used by damage gating.</param>
    private static void ApplyWeaponGates(ref EnemyShooterConfigElement config,
                                         bool useMinimumRange,
                                         float minimumRange,
                                         bool useMaximumRange,
                                         float maximumRange,
                                         bool exclusiveLookDirectionControl,
                                         EnemyWeaponInteractionActivationGate activationGates,
                                         float maximumActivationSpeed,
                                         float recentlyDamagedWindowSeconds)
    {
        config.UseMinimumRange = useMinimumRange ? (byte)1 : (byte)0;
        config.MinimumRange = math.max(0f, minimumRange);
        config.UseMaximumRange = useMaximumRange ? (byte)1 : (byte)0;
        config.MaximumRange = math.max(config.MinimumRange, maximumRange);
        config.ExclusiveLookDirectionControl = exclusiveLookDirectionControl ? (byte)1 : (byte)0;
        config.ActivationGates = ResolveWeaponActivationGates(activationGates);
        config.MaximumActivationSpeed = math.max(0f, maximumActivationSpeed);
        config.RecentlyDamagedWindowSeconds = math.max(0f, recentlyDamagedWindowSeconds);
    }

    /// <summary>
    /// Applies shared Weapon Interaction gates to one Power-Up Stealer config.
    /// </summary>
    /// <param name="config">Mutable Power-Up Stealer config.</param>
    /// <param name="useMinimumRange">True when minimum range gating should be applied.</param>
    /// <param name="minimumRange">Authored minimum player range.</param>
    /// <param name="useMaximumRange">True when maximum range gating should be applied.</param>
    /// <param name="maximumRange">Authored maximum player range.</param>
    /// <param name="exclusiveLookDirectionControl">True when this weapon controls look direction while active.</param>
    /// <param name="activationGates">Additional non-range activation gates.</param>
    /// <param name="maximumActivationSpeed">Maximum enemy speed allowed by speed gating.</param>
    /// <param name="recentlyDamagedWindowSeconds">Recent damage window used by damage gating.</param>
    private static void ApplyWeaponGates(ref EnemyPowerUpStealerConfigElement config,
                                         bool useMinimumRange,
                                         float minimumRange,
                                         bool useMaximumRange,
                                         float maximumRange,
                                         bool exclusiveLookDirectionControl,
                                         EnemyWeaponInteractionActivationGate activationGates,
                                         float maximumActivationSpeed,
                                         float recentlyDamagedWindowSeconds)
    {
        config.UseMinimumRange = useMinimumRange ? (byte)1 : (byte)0;
        config.MinimumRange = math.max(0f, minimumRange);
        config.UseMaximumRange = useMaximumRange ? (byte)1 : (byte)0;
        config.MaximumRange = math.max(config.MinimumRange, maximumRange);
        config.ExclusiveLookDirectionControl = exclusiveLookDirectionControl ? (byte)1 : (byte)0;
        config.ActivationGates = ResolveWeaponActivationGates(activationGates);
        config.MaximumActivationSpeed = math.max(0f, maximumActivationSpeed);
        config.RecentlyDamagedWindowSeconds = math.max(0f, recentlyDamagedWindowSeconds);
    }

    /// <summary>
    /// Compiles one shared pattern definition into movement, shooter and drop buffers.
    /// </summary>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="selectedPattern">Shared assembled pattern to compile.</param>
    /// <param name="result">Existing result object that receives compiled values.</param>
    /// <returns>Compiled bake result.</returns>
    private static EnemyCompiledPatternBakeResult CompilePattern(EnemyModulesAndPatternsPreset sharedPreset,
                                                                 EnemyModulesPatternDefinition selectedPattern,
                                                                 EnemyCompiledPatternBakeResult result)
    {
        ApplyCoreMovement(selectedPattern, sharedPreset, ref result);
        ApplyShortRangeInteraction(selectedPattern, sharedPreset, ref result.PatternConfig);
        ApplyWeaponInteraction(selectedPattern, sharedPreset, ref result);
        ApplyDropItems(selectedPattern, sharedPreset, ref result);
        result.HasCustomMovement = ResolveHasCustomMovement(result.PatternConfig);
        return result;
    }

    /// <summary>
    /// Applies the core movement selection to the compiled result.
    /// </summary>
    /// <param name="pattern">Shared pattern definition currently being compiled.</param>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="result">Mutable compiled result.</param>
    private static void ApplyCoreMovement(EnemyModulesPatternDefinition pattern,
                                          EnemyModulesAndPatternsPreset sharedPreset,
                                          ref EnemyCompiledPatternBakeResult result)
    {
        if (pattern == null)
            return;

        EnemyPatternCoreMovementAssembly coreMovement = pattern.CoreMovement;

        if (coreMovement == null)
            return;

        TryApplyCoreMovementModule(sharedPreset, coreMovement.Binding, ref result);
    }

    /// <summary>
    /// Applies the optional short-range interaction selection to the compiled pattern config.
    /// </summary>
    /// <param name="pattern">Shared pattern definition currently being compiled.</param>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="patternConfig">Mutable compiled pattern config.</param>
    private static void ApplyShortRangeInteraction(EnemyModulesPatternDefinition pattern,
                                                   EnemyModulesAndPatternsPreset sharedPreset,
                                                   ref EnemyPatternConfig patternConfig)
    {
        if (pattern == null)
            return;

        EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = pattern.ShortRangeInteraction;

        if (shortRangeInteraction == null || !shortRangeInteraction.IsEnabled)
            return;

        TryApplyShortRangeInteractionModule(sharedPreset,
                                            shortRangeInteraction.Binding,
                                            shortRangeInteraction.ActivationRange,
                                            shortRangeInteraction.ReleaseDistanceBuffer,
                                            ref patternConfig);
    }

    /// <summary>
    /// Applies one optional weapon interaction to the compiled result.
    /// </summary>
    /// <param name="pattern">Shared pattern definition currently being compiled.</param>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="result">Mutable compiled result.</param>
    private static void ApplyWeaponInteraction(EnemyModulesPatternDefinition pattern,
                                               EnemyModulesAndPatternsPreset sharedPreset,
                                               ref EnemyCompiledPatternBakeResult result)
    {
        if (pattern == null)
            return;

        EnemyPatternWeaponInteractionAssembly weaponInteraction = pattern.WeaponInteraction;

        if (weaponInteraction == null || !weaponInteraction.IsEnabled)
            return;

        TryAddWeaponInteractionModule(sharedPreset,
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
    /// Resolves legal Weapon Interaction activation gate flags authored by the shared pattern assembly.
    /// </summary>
    /// <param name="gates">Authored gate flags.</param>
    /// <returns>Sanitized gate flags.</returns>
    internal static EnemyWeaponInteractionActivationGate ResolveWeaponActivationGates(EnemyWeaponInteractionActivationGate gates)
    {
        EnemyWeaponInteractionActivationGate legalMask = EnemyWeaponInteractionActivationGate.RequireBelowSpeed |
                                                         EnemyWeaponInteractionActivationGate.RequireRecentlyDamaged |
                                                         EnemyWeaponInteractionActivationGate.RequireWandererWait;
        return gates & legalMask;
    }

    /// <summary>
    /// Applies the optional drop-items selection to the compiled result.
    /// </summary>
    /// <param name="pattern">Shared pattern definition currently being compiled.</param>
    /// <param name="sharedPreset">Shared preset used to resolve module definitions.</param>
    /// <param name="result">Mutable compiled result.</param>
    private static void ApplyDropItems(EnemyModulesPatternDefinition pattern,
                                       EnemyModulesAndPatternsPreset sharedPreset,
                                       ref EnemyCompiledPatternBakeResult result)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternDropItemsAssembly dropItems = pattern.DropItems;

        if (dropItems == null || !dropItems.IsEnabled || dropItems.Modules == null)
            return;

        IReadOnlyList<EnemyPatternModuleBinding> moduleBindings = dropItems.Modules;

        for (int moduleIndex = 0; moduleIndex < moduleBindings.Count; moduleIndex++)
        {
            EnemyPatternModuleBinding binding = moduleBindings[moduleIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

            if (moduleDefinition == null)
                continue;

            if (EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind) != EnemyPatternModuleKind.DropItems)
                continue;

            EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
            EnemyDropItemsBakeUtility.TryAppendModule(resolvedPayload, ref result);
        }
    }

    /// <summary>
    /// Copies the short-range coward payload into the short-range section of the compiled pattern config.
    /// </summary>
    /// <param name="payload">Resolved module payload for the short-range coward module.</param>
    /// <param name="patternConfig">Mutable compiled pattern config.</param>
    private static void ApplyShortRangeCowardPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        if (payload == null || payload.Coward == null)
            return;

        EnemyCowardModuleData coward = payload.Coward;
        patternConfig.ShortRangeSearchRadius = math.max(0.5f, coward.SearchRadius);
        patternConfig.ShortRangeMinimumTravelDistance = math.max(0f, coward.MinimumRetreatDistance);
        patternConfig.ShortRangeMaximumTravelDistance = math.max(patternConfig.ShortRangeMinimumTravelDistance, coward.MaximumRetreatDistance);
        patternConfig.ShortRangeArrivalTolerance = math.max(0.05f, coward.ArrivalTolerance);
        patternConfig.ShortRangeCandidateSampleCount = math.clamp(math.max(1, coward.CandidateSampleCount), 1, 64);
        patternConfig.ShortRangeUseInfiniteDirectionSampling = coward.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
        patternConfig.ShortRangeInfiniteDirectionStepDegrees = math.clamp(coward.InfiniteDirectionStepDegrees, 0.5f, 90f);
        patternConfig.ShortRangeMinimumEnemyClearance = math.max(0f, coward.MinimumEnemyClearance);
        patternConfig.ShortRangeTrajectoryPredictionTime = math.max(0f, coward.TrajectoryPredictionTime);
        patternConfig.ShortRangeFreeTrajectoryPreference = math.lerp(1f, 5f, math.saturate(coward.FreeTrajectoryPreference));
        patternConfig.ShortRangeBlockedPathRetryDelay = math.max(0f, coward.BlockedPathRetryDelay);
        patternConfig.ShortRangeRetreatDirectionPreference = math.saturate(coward.RetreatDirectionPreference);
        patternConfig.ShortRangeOpenSpacePreference = math.saturate(coward.OpenSpacePreference);
        patternConfig.ShortRangeNavigationPreference = math.saturate(coward.NavigationRetreatPreference);
        patternConfig.ShortRangeRetreatSpeedMultiplierFar = math.max(0f, coward.RetreatSpeedMultiplierFar);
        patternConfig.ShortRangeRetreatSpeedMultiplierNear = math.max(patternConfig.ShortRangeRetreatSpeedMultiplierFar,
                                                                      coward.RetreatSpeedMultiplierNear);
    }

    /// <summary>
    /// Resolves whether the compiled pattern still requires the custom pattern movement system after the new category split.
    /// </summary>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <returns>True when the pattern should keep the custom movement tag.</returns>
    private static bool ResolveHasCustomMovement(EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.Grunt)
            return true;

        if (patternConfig.HasShortRangeInteraction == 0)
            return false;

        return patternConfig.ShortRangeMovementKind != EnemyCompiledMovementPatternKind.Grunt;
    }
    #endregion

    #endregion
}
