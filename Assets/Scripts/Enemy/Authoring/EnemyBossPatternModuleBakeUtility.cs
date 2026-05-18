using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Compiles internal boss pattern module extraction candidates into ECS buffers.
/// </summary>
internal static class EnemyBossPatternModuleBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles all internal module candidate lists owned by one top-level boss pattern candidate.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="pattern">Top-level boss pattern candidate being compiled.</param>
    /// <param name="patternIndex">Runtime pattern buffer index that owns the module candidates.</param>
    /// <param name="globalEngagementSettings">Generic offensive engagement feedback settings resolved from the visual preset.</param>
    /// <param name="result">Mutable boss compile result.</param>
    public static void CompilePatternModuleCandidates(EnemyModulesAndPatternsPreset sharedPreset,
                                                      EnemyBossPatternInteractionDefinition pattern,
                                                      int patternIndex,
                                                      EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                      EnemyCompiledBossPatternBakeResult result)
    {
        if (sharedPreset == null || pattern == null || result == null)
            return;

        CompileCoreMovementCandidates(sharedPreset, pattern.CoreMovementExtraction, patternIndex, globalEngagementSettings, result);
        CompileShortRangeCandidates(sharedPreset, pattern.ShortRangeExtraction, patternIndex, globalEngagementSettings, result);
        CompileWeaponCandidates(sharedPreset, pattern.WeaponExtraction, patternIndex, globalEngagementSettings, result);
    }
    #endregion

    #region Core Movement
    /// <summary>
    /// Compiles Core Movement internal extraction settings and candidates for one pattern.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="extraction">Authored Core Movement extraction definition.</param>
    /// <param name="patternIndex">Runtime pattern buffer index that owns the slot.</param>
    /// <param name="globalEngagementSettings">Generic offensive engagement feedback settings resolved from the visual preset.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void CompileCoreMovementCandidates(EnemyModulesAndPatternsPreset sharedPreset,
                                                      EnemyBossPatternCoreMovementExtractionDefinition extraction,
                                                      int patternIndex,
                                                      EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                      EnemyCompiledBossPatternBakeResult result)
    {
        result.ModuleExtractions.Add(BuildExtractionElement(patternIndex,
                                                            EnemyBossPatternSlotKind.CoreMovement,
                                                            extraction != null ? extraction.ExtractionSettings : null));

        IReadOnlyList<EnemyBossPatternCoreMovementModuleCandidateDefinition> candidates = extraction != null
            ? extraction.Candidates
            : null;

        if (candidates == null)
            return;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossPatternCoreMovementModuleCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null || candidate.Eligibility == null || !candidate.Eligibility.Enabled)
                continue;

            EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
            byte isNullModule = candidate.ModuleMode == EnemyBossPatternModuleMode.NullModule ? (byte)1 : (byte)0;
            List<EnemyOffensiveEngagementConfigElement> engagementConfigs = new List<EnemyOffensiveEngagementConfigElement>(1);

            if (isNullModule != 0)
            {
                ConfigureNullCoreMovement(ref compiledPattern);
            }
            else
            {
                EnemyModulesAndPatternsBakeUtility.TryApplyCoreMovementModule(sharedPreset, candidate.Binding, ref compiledPattern);

                if (EnemyOffensiveEngagementBakeUtility.TryBuildCoreMovementConfig(candidate,
                                                                                   sharedPreset,
                                                                                   globalEngagementSettings,
                                                                                   out EnemyOffensiveEngagementConfigElement config))
                {
                    config.VisualSettingsKey = result.ModuleCandidates.Count;
                    engagementConfigs.Add(config);
                }
            }

            compiledPattern.HasCustomMovement = EnemyBossPatternConfigUtility.RequiresCustomMovement(in compiledPattern.PatternConfig);
            int firstEngagementConfigIndex = EnemyBossPatternBakeUtility.AppendOffensiveEngagementConfigs(engagementConfigs, result);
            result.ModuleCandidates.Add(BuildCandidateElement(patternIndex,
                                                              EnemyBossPatternSlotKind.CoreMovement,
                                                              candidateIndex,
                                                              isNullModule,
                                                              candidate.Eligibility,
                                                              compiledPattern,
                                                              0,
                                                              0,
                                                              0,
                                                              0,
                                                              firstEngagementConfigIndex,
                                                              engagementConfigs.Count));
        }
    }
    #endregion

    #region Short Range
    /// <summary>
    /// Compiles Short-Range internal extraction settings and candidates for one pattern.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="extraction">Authored Short-Range extraction definition.</param>
    /// <param name="patternIndex">Runtime pattern buffer index that owns the slot.</param>
    /// <param name="globalEngagementSettings">Generic offensive engagement feedback settings resolved from the visual preset.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void CompileShortRangeCandidates(EnemyModulesAndPatternsPreset sharedPreset,
                                                    EnemyBossPatternShortRangeExtractionDefinition extraction,
                                                    int patternIndex,
                                                    EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                    EnemyCompiledBossPatternBakeResult result)
    {
        result.ModuleExtractions.Add(BuildExtractionElement(patternIndex,
                                                            EnemyBossPatternSlotKind.ShortRangeInteraction,
                                                            extraction != null ? extraction.ExtractionSettings : null));

        IReadOnlyList<EnemyBossPatternShortRangeModuleCandidateDefinition> candidates = extraction != null
            ? extraction.Candidates
            : null;

        if (candidates == null)
            return;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossPatternShortRangeModuleCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null || candidate.Eligibility == null || !candidate.Eligibility.Enabled)
                continue;

            EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
            byte isNullModule = candidate.ModuleMode == EnemyBossPatternModuleMode.NullModule ? (byte)1 : (byte)0;
            List<EnemyOffensiveEngagementConfigElement> engagementConfigs = new List<EnemyOffensiveEngagementConfigElement>(1);

            if (isNullModule == 0)
            {
                EnemyBossPatternBakeUtility.ApplyShortRangeSlot(sharedPreset,
                                                                 candidate.Interaction,
                                                                 ref compiledPattern.PatternConfig);

                if (EnemyOffensiveEngagementBakeUtility.TryBuildShortRangeConfig(candidate.Interaction,
                                                                                 sharedPreset,
                                                                                 globalEngagementSettings,
                                                                                 out EnemyOffensiveEngagementConfigElement config))
                {
                    config.VisualSettingsKey = result.ModuleCandidates.Count;
                    engagementConfigs.Add(config);
                }
            }

            compiledPattern.HasCustomMovement = EnemyBossPatternConfigUtility.RequiresCustomMovement(in compiledPattern.PatternConfig);
            int firstEngagementConfigIndex = EnemyBossPatternBakeUtility.AppendOffensiveEngagementConfigs(engagementConfigs, result);
            result.ModuleCandidates.Add(BuildCandidateElement(patternIndex,
                                                              EnemyBossPatternSlotKind.ShortRangeInteraction,
                                                              candidateIndex,
                                                              isNullModule,
                                                              candidate.Eligibility,
                                                              compiledPattern,
                                                              0,
                                                              0,
                                                              0,
                                                              0,
                                                              firstEngagementConfigIndex,
                                                              engagementConfigs.Count));
        }
    }
    #endregion

    #region Weapon
    /// <summary>
    /// Compiles Weapon internal extraction settings and candidates for one pattern.
    /// </summary>
    /// <param name="sharedPreset">Source shared preset containing module definitions.</param>
    /// <param name="extraction">Authored Weapon extraction definition.</param>
    /// <param name="patternIndex">Runtime pattern buffer index that owns the slot.</param>
    /// <param name="globalEngagementSettings">Generic offensive engagement feedback settings resolved from the visual preset.</param>
    /// <param name="result">Mutable boss compile result.</param>
    private static void CompileWeaponCandidates(EnemyModulesAndPatternsPreset sharedPreset,
                                                EnemyBossPatternWeaponExtractionDefinition extraction,
                                                int patternIndex,
                                                EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                EnemyCompiledBossPatternBakeResult result)
    {
        result.ModuleExtractions.Add(BuildExtractionElement(patternIndex,
                                                            EnemyBossPatternSlotKind.WeaponInteraction,
                                                            extraction != null ? extraction.ExtractionSettings : null));

        IReadOnlyList<EnemyBossPatternWeaponModuleCandidateDefinition> candidates = extraction != null
            ? extraction.Candidates
            : null;

        if (candidates == null)
            return;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            EnemyBossPatternWeaponModuleCandidateDefinition candidate = candidates[candidateIndex];

            if (candidate == null || candidate.Eligibility == null || !candidate.Eligibility.Enabled)
                continue;

            EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
            byte isNullModule = candidate.ModuleMode == EnemyBossPatternModuleMode.NullModule ? (byte)1 : (byte)0;
            List<EnemyOffensiveEngagementConfigElement> engagementConfigs = new List<EnemyOffensiveEngagementConfigElement>(1);

            if (isNullModule == 0)
            {
                EnemyBossPatternBakeUtility.ApplyWeaponSlot(sharedPreset, candidate.Interaction, ref compiledPattern);
                EnemyBossPatternBakeUtility.TryAssignShooterRuntimeSettings(compiledPattern, result);

                if (EnemyOffensiveEngagementBakeUtility.TryBuildWeaponConfig(candidate.Interaction,
                                                                             sharedPreset,
                                                                             globalEngagementSettings,
                                                                             out EnemyOffensiveEngagementConfigElement config))
                {
                    config.VisualSettingsKey = result.ModuleCandidates.Count;
                    engagementConfigs.Add(config);
                }
            }

            int firstShooterConfigIndex = EnemyBossPatternBakeUtility.AppendShooterConfigs(compiledPattern, result);
            int firstStealerConfigIndex = EnemyBossPatternBakeUtility.AppendPowerUpStealerConfigs(compiledPattern, result);
            int firstEngagementConfigIndex = EnemyBossPatternBakeUtility.AppendOffensiveEngagementConfigs(engagementConfigs, result);
            result.ModuleCandidates.Add(BuildCandidateElement(patternIndex,
                                                              EnemyBossPatternSlotKind.WeaponInteraction,
                                                              candidateIndex,
                                                              isNullModule,
                                                              candidate.Eligibility,
                                                              compiledPattern,
                                                              firstShooterConfigIndex,
                                                              compiledPattern.ShooterConfigs.Count,
                                                              firstStealerConfigIndex,
                                                              compiledPattern.PowerUpStealerConfigs.Count,
                                                              firstEngagementConfigIndex,
                                                              engagementConfigs.Count));
        }
    }
    #endregion

    #region Element Builders
    /// <summary>
    /// Configures a null Core Movement candidate as an explicit stationary boss state instead of falling back to Grunt.
    /// </summary>
    /// <param name="compiledPattern">Compiled pattern candidate receiving the stationary config.</param>
    private static void ConfigureNullCoreMovement(ref EnemyCompiledPatternBakeResult compiledPattern)
    {
        compiledPattern.PatternConfig = EnemyPatternDefaultsUtility.CreatePatternConfig();
        compiledPattern.PatternConfig.MovementKind = EnemyCompiledMovementPatternKind.Stationary;
        compiledPattern.PatternConfig.StationaryFreezeRotation = 1;
        compiledPattern.HasCustomMovement = true;
    }

    /// <summary>
    /// Copies authored extraction settings into one runtime slot extraction entry.
    /// </summary>
    /// <param name="patternIndex">Runtime pattern buffer index that owns the slot.</param>
    /// <param name="slotKind">Slot controlled by the extraction entry.</param>
    /// <param name="settings">Authored extraction settings.</param>
    /// <returns>Compiled slot extraction entry.</returns>
    private static EnemyBossPatternModuleExtractionElement BuildExtractionElement(int patternIndex,
                                                                                  EnemyBossPatternSlotKind slotKind,
                                                                                  EnemyBossPatternExtractionSettings settings)
    {
        if (settings == null)
        {
            return new EnemyBossPatternModuleExtractionElement
            {
                PatternIndex = patternIndex,
                SlotKind = slotKind,
                RerollWhenCurrentPatternBecomesInvalid = 1,
                UseElapsedIntervalExtraction = 1,
                UseMissingHealthStepExtraction = 0,
                UseTravelledDistanceExtraction = 0,
                UseDamageWindowExtraction = 0,
                PlayerDistanceCondition = EnemyBossPatternPlayerDistanceCondition.Disabled,
                MinimumSecondsBetweenExtractions = 0f,
                ElapsedIntervalSeconds = 2f,
                MissingHealthStepPercent = 0f,
                TravelledDistanceSinceLastExtraction = 0f,
                PlayerDistanceThreshold = 0f,
                PlayerDistanceHoldSeconds = 0f,
                DamageWindowSeconds = 0f,
                DamageThreshold = 0f
            };
        }

        return new EnemyBossPatternModuleExtractionElement
        {
            PatternIndex = patternIndex,
            SlotKind = slotKind,
            RerollWhenCurrentPatternBecomesInvalid = settings.RerollWhenCurrentPatternBecomesInvalid ? (byte)1 : (byte)0,
            UseElapsedIntervalExtraction = settings.UseElapsedIntervalExtraction ? (byte)1 : (byte)0,
            UseMissingHealthStepExtraction = settings.UseMissingHealthStepExtraction ? (byte)1 : (byte)0,
            UseTravelledDistanceExtraction = settings.UseTravelledDistanceExtraction ? (byte)1 : (byte)0,
            UseDamageWindowExtraction = settings.UseDamageWindowExtraction ? (byte)1 : (byte)0,
            PlayerDistanceCondition = settings.PlayerDistanceCondition,
            MinimumSecondsBetweenExtractions = math.max(0f, settings.MinimumSecondsBetweenExtractions),
            ElapsedIntervalSeconds = math.max(0f, settings.ElapsedIntervalSeconds),
            MissingHealthStepPercent = math.saturate(settings.MissingHealthStepPercent),
            TravelledDistanceSinceLastExtraction = math.max(0f, settings.TravelledDistanceSinceLastExtraction),
            PlayerDistanceThreshold = math.max(0f, settings.PlayerDistanceThreshold),
            PlayerDistanceHoldSeconds = math.max(0f, settings.PlayerDistanceHoldSeconds),
            DamageWindowSeconds = math.max(0f, settings.DamageWindowSeconds),
            DamageThreshold = math.max(0f, settings.DamageThreshold)
        };
    }

    /// <summary>
    /// Builds one compiled module candidate buffer entry from authored eligibility and compiled module data.
    /// </summary>
    /// <param name="patternIndex">Runtime pattern buffer index that owns the candidate.</param>
    /// <param name="slotKind">Slot controlled by the candidate.</param>
    /// <param name="candidateIndex">Authored candidate index inside the slot list.</param>
    /// <param name="isNullModule">One when this candidate clears the slot.</param>
    /// <param name="eligibility">Authored eligibility settings.</param>
    /// <param name="compiledPattern">Compiled module pattern data.</param>
    /// <param name="firstShooterConfigIndex">First boss-owned shooter config index.</param>
    /// <param name="shooterConfigCount">Number of shooter configs owned by the candidate.</param>
    /// <param name="firstStealerConfigIndex">First boss-owned Power-Up Stealer config index.</param>
    /// <param name="stealerConfigCount">Number of Power-Up Stealer configs owned by the candidate.</param>
    /// <param name="firstEngagementConfigIndex">First boss-owned engagement config index.</param>
    /// <param name="engagementConfigCount">Number of engagement configs owned by the candidate.</param>
    /// <returns>Compiled module candidate entry.</returns>
    private static EnemyBossPatternModuleCandidateElement BuildCandidateElement(int patternIndex,
                                                                                EnemyBossPatternSlotKind slotKind,
                                                                                int candidateIndex,
                                                                                byte isNullModule,
                                                                                EnemyBossPatternModuleCandidateEligibilityDefinition eligibility,
                                                                                EnemyCompiledPatternBakeResult compiledPattern,
                                                                                int firstShooterConfigIndex,
                                                                                int shooterConfigCount,
                                                                                int firstStealerConfigIndex,
                                                                                int stealerConfigCount,
                                                                                int firstEngagementConfigIndex,
                                                                                int engagementConfigCount)
    {
        return new EnemyBossPatternModuleCandidateElement
        {
            PatternIndex = patternIndex,
            SlotKind = slotKind,
            CandidateIndex = math.max(0, candidateIndex),
            EligibilityType = eligibility.EligibilityType,
            IsNullModule = isNullModule,
            HasCustomMovement = compiledPattern.HasCustomMovement ? (byte)1 : (byte)0,
            MinimumActiveSeconds = math.max(0f, eligibility.MinimumActiveSeconds),
            SelectionWeight = EnemyBossPatternBakeUtility.ResolveSelectionWeight(eligibility.SelectionWeight),
            MinimumMissingHealthPercent = math.saturate(eligibility.MinimumMissingHealthPercent),
            MaximumMissingHealthPercent = math.saturate(eligibility.MaximumMissingHealthPercent),
            MinimumElapsedSeconds = math.max(0f, eligibility.MinimumElapsedSeconds),
            MaximumElapsedSeconds = math.max(0f, eligibility.MaximumElapsedSeconds),
            MinimumTravelledDistance = math.max(0f, eligibility.MinimumTravelledDistance),
            MaximumTravelledDistance = math.max(0f, eligibility.MaximumTravelledDistance),
            MinimumPlayerDistance = math.max(0f, eligibility.MinimumPlayerDistance),
            MaximumPlayerDistance = math.max(0f, eligibility.MaximumPlayerDistance),
            RecentlyDamagedWindowSeconds = math.max(0f, eligibility.RecentlyDamagedWindowSeconds),
            FirstShooterConfigIndex = firstShooterConfigIndex,
            ShooterConfigCount = math.max(0, shooterConfigCount),
            FirstPowerUpStealerConfigIndex = firstStealerConfigIndex,
            PowerUpStealerConfigCount = math.max(0, stealerConfigCount),
            FirstOffensiveEngagementConfigIndex = firstEngagementConfigIndex,
            OffensiveEngagementConfigCount = math.max(0, engagementConfigCount),
            PatternConfig = compiledPattern.PatternConfig
        };
    }
    #endregion

    #endregion
}
