using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the progression blob used by runtime level-up, milestone, and scalable-stat systems.
/// </summary>
internal static class PlayerProgressionBlobBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the baked progression blob from the selected progression preset.
    /// </summary>
    /// <param name="preset">Scaled progression preset used during baking.</param>
    /// <param name="powerUpsPreset">Scaled scoped power-ups preset used to resolve milestone drop pools into tier rolls.</param>
    /// <param name="sourcePreset">Unscaled source progression preset used to extract runtime scaling metadata.</param>
    /// <param name="sourcePowerUpsPreset">Unscaled scoped power-ups preset used to extract runtime scaling metadata.</param>
    /// <returns>Persistent blob asset reference ready to assign to PlayerProgressionConfig.</returns>
    public static BlobAssetReference<PlayerProgressionConfigBlob> BuildProgressionConfigBlob(PlayerProgressionPreset preset,
                                                                                             PlayerPowerUpsPreset powerUpsPreset,
                                                                                             PlayerProgressionPreset sourcePreset,
                                                                                             PlayerPowerUpsPreset sourcePowerUpsPreset)
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);
        ref PlayerProgressionConfigBlob root = ref builder.ConstructRoot<PlayerProgressionConfigBlob>();
        int levelCap = preset != null ? math.max(1, preset.LevelCap) : 100;
        float experiencePickupRadius = preset != null ? math.max(0f, preset.ExperiencePickupRadius) : 0f;
        float baseExperiencePickupRadius = sourcePreset != null ? math.max(0f, sourcePreset.ExperiencePickupRadius) : experiencePickupRadius;
        string experiencePickupRadiusScalingFormula = string.Empty;
        float milestoneSkipHoldConfirmationSeconds = ResolveNonNegativeFinite(preset != null
            ? preset.MilestoneSkipHoldConfirmationSeconds
            : PlayerProgressionPreset.DefaultMilestoneSkipHoldConfirmationSeconds,
            PlayerProgressionPreset.DefaultMilestoneSkipHoldConfirmationSeconds);
        float baseMilestoneSkipHoldConfirmationSeconds = ResolveNonNegativeFinite(sourcePreset != null
            ? sourcePreset.MilestoneSkipHoldConfirmationSeconds
            : milestoneSkipHoldConfirmationSeconds,
            milestoneSkipHoldConfirmationSeconds);
        string milestoneSkipHoldConfirmationScalingFormula = string.Empty;
        float4 milestoneSkipHoldFillColor = ResolveColorVector(preset != null
            ? preset.MilestoneSkipHoldFillColor
            : PlayerProgressionPreset.DefaultMilestoneSkipHoldFillColor);
        float4 baseMilestoneSkipHoldFillColor = ResolveColorVector(sourcePreset != null
            ? sourcePreset.MilestoneSkipHoldFillColor
            : preset != null ? preset.MilestoneSkipHoldFillColor : PlayerProgressionPreset.DefaultMilestoneSkipHoldFillColor);
        string milestoneSkipHoldFillColorRScalingFormula = string.Empty;
        string milestoneSkipHoldFillColorGScalingFormula = string.Empty;
        string milestoneSkipHoldFillColorBScalingFormula = string.Empty;
        string milestoneSkipHoldFillColorAScalingFormula = string.Empty;

        if (PlayerRuntimeScalingBakeMetadataUtility.TryResolveExperiencePickupRadiusScalingData(sourcePreset,
                                                                                                out float resolvedBaseExperiencePickupRadius,
                                                                                                out string resolvedExperiencePickupRadiusScalingFormula))
        {
            baseExperiencePickupRadius = math.max(0f, resolvedBaseExperiencePickupRadius);
            experiencePickupRadiusScalingFormula = resolvedExperiencePickupRadiusScalingFormula;
        }

        if (PlayerRuntimeScalingBakeMetadataUtility.TryResolveMilestoneSkipHoldConfirmationScalingData(sourcePreset,
                                                                                                      out float resolvedBaseMilestoneSkipHoldConfirmationSeconds,
                                                                                                      out string resolvedMilestoneSkipHoldConfirmationScalingFormula))
        {
            baseMilestoneSkipHoldConfirmationSeconds = ResolveNonNegativeFinite(resolvedBaseMilestoneSkipHoldConfirmationSeconds,
                                                                                baseMilestoneSkipHoldConfirmationSeconds);
            milestoneSkipHoldConfirmationScalingFormula = resolvedMilestoneSkipHoldConfirmationScalingFormula;
        }

        ResolveMilestoneSkipHoldColorChannelScalingData(sourcePreset,
                                                        "r",
                                                        ref baseMilestoneSkipHoldFillColor.x,
                                                        ref milestoneSkipHoldFillColorRScalingFormula);
        ResolveMilestoneSkipHoldColorChannelScalingData(sourcePreset,
                                                        "g",
                                                        ref baseMilestoneSkipHoldFillColor.y,
                                                        ref milestoneSkipHoldFillColorGScalingFormula);
        ResolveMilestoneSkipHoldColorChannelScalingData(sourcePreset,
                                                        "b",
                                                        ref baseMilestoneSkipHoldFillColor.z,
                                                        ref milestoneSkipHoldFillColorBScalingFormula);
        ResolveMilestoneSkipHoldColorChannelScalingData(sourcePreset,
                                                        "a",
                                                        ref baseMilestoneSkipHoldFillColor.w,
                                                        ref milestoneSkipHoldFillColorAScalingFormula);

        root.LevelCap = levelCap;
        root.ExperiencePickupRadius = experiencePickupRadius;
        root.BaseExperiencePickupRadius = baseExperiencePickupRadius;
        root.MilestoneTimeScaleResumeDurationSeconds = preset != null ? math.max(0f, preset.MilestoneTimeScaleResumeDurationSeconds) : 0f;
        root.MilestoneSkipHoldConfirmationSeconds = milestoneSkipHoldConfirmationSeconds;
        root.BaseMilestoneSkipHoldConfirmationSeconds = baseMilestoneSkipHoldConfirmationSeconds;
        root.MilestoneSkipHoldFillColor = milestoneSkipHoldFillColor;
        root.BaseMilestoneSkipHoldFillColor = baseMilestoneSkipHoldFillColor;
        root.EquippedScheduleIndex = -1;
        builder.AllocateString(ref root.ExperiencePickupRadiusScalingFormula,
                               string.IsNullOrWhiteSpace(experiencePickupRadiusScalingFormula) ? string.Empty : experiencePickupRadiusScalingFormula);
        builder.AllocateString(ref root.MilestoneSkipHoldConfirmationSecondsScalingFormula,
                               string.IsNullOrWhiteSpace(milestoneSkipHoldConfirmationScalingFormula) ? string.Empty : milestoneSkipHoldConfirmationScalingFormula);
        builder.AllocateString(ref root.MilestoneSkipHoldFillColorRScalingFormula,
                               string.IsNullOrWhiteSpace(milestoneSkipHoldFillColorRScalingFormula) ? string.Empty : milestoneSkipHoldFillColorRScalingFormula);
        builder.AllocateString(ref root.MilestoneSkipHoldFillColorGScalingFormula,
                               string.IsNullOrWhiteSpace(milestoneSkipHoldFillColorGScalingFormula) ? string.Empty : milestoneSkipHoldFillColorGScalingFormula);
        builder.AllocateString(ref root.MilestoneSkipHoldFillColorBScalingFormula,
                               string.IsNullOrWhiteSpace(milestoneSkipHoldFillColorBScalingFormula) ? string.Empty : milestoneSkipHoldFillColorBScalingFormula);
        builder.AllocateString(ref root.MilestoneSkipHoldFillColorAScalingFormula,
                               string.IsNullOrWhiteSpace(milestoneSkipHoldFillColorAScalingFormula) ? string.Empty : milestoneSkipHoldFillColorAScalingFormula);

        BakeProgressionGamePhases(builder,
                                  ref root,
                                  preset,
                                  sourcePreset,
                                  powerUpsPreset,
                                  sourcePowerUpsPreset);
        BakeProgressionScalableStats(builder, ref root, preset);
        BakeProgressionSchedules(builder, ref root, preset, sourcePreset);

        BlobAssetReference<PlayerProgressionConfigBlob> blob = builder.CreateBlobAssetReference<PlayerProgressionConfigBlob>(Allocator.Persistent);
        builder.Dispose();
        return blob;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one finite non-negative value used by the progression blob without mutating the source preset.
    /// </summary>
    /// <param name="value">Authored value to sanitize for runtime storage.</param>
    /// <param name="fallbackValue">Fallback used when the authored value is not finite.</param>
    /// <returns>Finite non-negative value safe for runtime consumption.</returns>
    private static float ResolveNonNegativeFinite(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return math.max(0f, fallbackValue);

        return math.max(0f, value);
    }

    /// <summary>
    /// Converts an authored color to a runtime-safe float4 used by blob data.
    /// </summary>
    /// <param name="color">Authored color value.</param>
    /// <returns>Color channels clamped to the presentation-safe 0..1 range.</returns>
    private static float4 ResolveColorVector(Color color)
    {
        return new float4(ResolveColorChannel(color.r),
                          ResolveColorChannel(color.g),
                          ResolveColorChannel(color.b),
                          ResolveColorChannel(color.a));
    }

    /// <summary>
    /// Converts one authored color channel to a finite clamped value for runtime presentation.
    /// </summary>
    /// <param name="value">Raw color channel.</param>
    /// <returns>Finite channel value in the 0..1 range.</returns>
    private static float ResolveColorChannel(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;

        return math.saturate(value);
    }

    /// <summary>
    /// Resolves the source base value and formula for one milestone skip hold fill color channel.
    /// </summary>
    /// <param name="sourcePreset">Unscaled progression preset used as formula baseline.</param>
    /// <param name="channelName">Unity color channel name: r, g, b, or a.</param>
    /// <param name="baseChannelValue">Mutable base color channel value.</param>
    /// <param name="scalingFormula">Mutable formula string for the channel.</param>
    private static void ResolveMilestoneSkipHoldColorChannelScalingData(PlayerProgressionPreset sourcePreset,
                                                                        string channelName,
                                                                        ref float baseChannelValue,
                                                                        ref string scalingFormula)
    {
        if (!PlayerRuntimeScalingBakeMetadataUtility.TryResolveMilestoneSkipHoldFillColorChannelScalingData(sourcePreset,
                                                                                                           channelName,
                                                                                                           out float resolvedBaseChannelValue,
                                                                                                           out string resolvedScalingFormula))
        {
            return;
        }

        baseChannelValue = ResolveColorChannel(resolvedBaseChannelValue);
        scalingFormula = resolvedScalingFormula;
    }

    /// <summary>
    /// Bakes game phases, milestone requirements, milestone offer rolls, and skip compensations.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="root">Progression blob root being populated.</param>
    /// <param name="preset">Source progression preset.</param>

    private static void BakeProgressionGamePhases(BlobBuilder builder,
                                                  ref PlayerProgressionConfigBlob root,
                                                  PlayerProgressionPreset preset,
                                                  PlayerProgressionPreset sourcePreset,
                                                  PlayerPowerUpsPreset powerUpsPreset,
                                                  PlayerPowerUpsPreset sourcePowerUpsPreset)
    {
        IReadOnlyList<PlayerGamePhaseDefinition> gamePhases = preset != null ? preset.GamePhasesDefinition : null;
        int gamePhasesCount = gamePhases != null && gamePhases.Count > 0 ? gamePhases.Count : 1;
        BlobBuilderArray<PlayerGamePhaseBlob> gamePhasesArray = builder.Allocate(ref root.GamePhases, gamePhasesCount);

        // Bake each phase with safe defaults when authoring data is missing.
        for (int phaseIndex = 0; phaseIndex < gamePhasesCount; phaseIndex++)
        {
            PlayerGamePhaseDefinition gamePhase = gamePhases != null && phaseIndex < gamePhases.Count ? gamePhases[phaseIndex] : null;
            string phaseID = gamePhase != null ? gamePhase.PhaseID : string.Format("Phase{0}", phaseIndex);
            int startsAtLevel = gamePhase != null ? math.max(0, gamePhase.StartsAtLevel) : 0;
            float startingRequiredLevelUpExp = gamePhase != null ? math.max(1f, gamePhase.StartingRequiredLevelUpExp) : 100f;
            float requiredExperienceGrouth = gamePhase != null ? math.max(0f, gamePhase.RequiredExperienceGrouth) : 0f;

            if (string.IsNullOrWhiteSpace(phaseID))
                phaseID = string.Format("Phase{0}", phaseIndex);

            gamePhasesArray[phaseIndex] = new PlayerGamePhaseBlob
            {
                StartsAtLevel = startsAtLevel,
                StartingRequiredLevelUpExp = startingRequiredLevelUpExp,
                RequiredExperienceGrouth = requiredExperienceGrouth
            };
            builder.AllocateString(ref gamePhasesArray[phaseIndex].PhaseID, phaseID);

            IReadOnlyList<PlayerLevelUpMilestoneDefinition> milestones = gamePhase != null ? gamePhase.Milestones : null;
            int milestonesCount = milestones != null ? milestones.Count : 0;
            BlobBuilderArray<PlayerLevelUpMilestoneBlob> milestoneArray = builder.Allocate(ref gamePhasesArray[phaseIndex].Milestones, milestonesCount);

            // Bake each milestone together with its nested custom unlock rolls.
            for (int milestoneIndex = 0; milestoneIndex < milestonesCount; milestoneIndex++)
            {
                PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];
                int milestoneLevel = milestone != null ? math.max(startsAtLevel, milestone.MilestoneLevel) : startsAtLevel;
                float specialExpRequirement = milestone != null ? math.max(1f, milestone.SpecialExpRequirement) : startingRequiredLevelUpExp;

                milestoneArray[milestoneIndex] = new PlayerLevelUpMilestoneBlob
                {
                    MilestoneLevel = milestoneLevel,
                    SpecialExpRequirement = specialExpRequirement,
                    IsRecurring = milestone != null && milestone.Recurring ? (byte)1 : (byte)0,
                    RecurrenceIntervalLevels = milestone != null ? math.max(1, milestone.RecurrenceIntervalLevels) : 1
                };

                BakeMilestonePowerUpUnlocks(builder,
                                            ref milestoneArray[milestoneIndex],
                                            milestone,
                                            phaseIndex,
                                            milestoneIndex,
                                            sourcePreset,
                                            powerUpsPreset,
                                            sourcePowerUpsPreset);
                BakeMilestoneSkipCompensations(builder, ref milestoneArray[milestoneIndex], milestone);
            }
        }
    }

    /// <summary>
    /// Bakes milestone power-up extractions, each with its own tier-roll list.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="milestoneBlob">Destination milestone blob.</param>
    /// <param name="milestone">Source milestone definition.</param>

    private static void BakeMilestonePowerUpUnlocks(BlobBuilder builder,
                                                    ref PlayerLevelUpMilestoneBlob milestoneBlob,
                                                    PlayerLevelUpMilestoneDefinition milestone,
                                                    int phaseIndex,
                                                    int milestoneIndex,
                                                    PlayerProgressionPreset sourcePreset,
                                                    PlayerPowerUpsPreset powerUpsPreset,
                                                    PlayerPowerUpsPreset sourcePowerUpsPreset)
    {
        IReadOnlyList<PlayerMilestonePowerUpUnlockDefinition> powerUpUnlocks = milestone != null ? milestone.PowerUpUnlocks : null;
        int powerUpUnlockCount = powerUpUnlocks != null ? math.min(PlayerLevelUpMilestoneDefinition.MaxPowerUpUnlockCount, powerUpUnlocks.Count) : 0;
        BlobBuilderArray<PlayerMilestonePowerUpUnlockBlob> powerUpUnlockArray = builder.Allocate(ref milestoneBlob.PowerUpUnlocks, powerUpUnlockCount);

        for (int powerUpUnlockIndex = 0; powerUpUnlockIndex < powerUpUnlockCount; powerUpUnlockIndex++)
        {
            PlayerMilestonePowerUpUnlockDefinition powerUpUnlock = powerUpUnlocks[powerUpUnlockIndex];
            string requestedDropPoolId = powerUpUnlock != null && !string.IsNullOrWhiteSpace(powerUpUnlock.DropPoolId)
                ? powerUpUnlock.DropPoolId.Trim()
                : string.Empty;
            IReadOnlyList<PowerUpDropPoolTierDefinition> resolvedDropPoolTierRolls = ResolveDropPoolTierRolls(powerUpsPreset,
                                                                                                               requestedDropPoolId,
                                                                                                               out int resolvedDropPoolIndex);
            IReadOnlyList<PlayerMilestoneTierRollDefinition> legacyTierRolls = powerUpUnlock != null ? powerUpUnlock.LegacyTierRolls : null;
            bool useResolvedDropPool = resolvedDropPoolTierRolls != null && resolvedDropPoolTierRolls.Count > 0;
            int tierRollCount = useResolvedDropPool
                ? resolvedDropPoolTierRolls.Count
                : legacyTierRolls != null ? legacyTierRolls.Count : 0;
            BlobBuilderArray<PlayerMilestoneTierRollBlob> tierRollArray = builder.Allocate(ref powerUpUnlockArray[powerUpUnlockIndex].TierRolls, tierRollCount);
            builder.AllocateString(ref powerUpUnlockArray[powerUpUnlockIndex].DropPoolId, requestedDropPoolId);

            if (useResolvedDropPool)
            {
                // Copy drop-pool tier candidates into the contiguous blob array used at runtime.
                for (int tierRollIndex = 0; tierRollIndex < tierRollCount; tierRollIndex++)
                {
                    PowerUpDropPoolTierDefinition tierRoll = resolvedDropPoolTierRolls[tierRollIndex];
                    string tierId = tierRoll != null && !string.IsNullOrWhiteSpace(tierRoll.TierId)
                        ? tierRoll.TierId.Trim()
                        : string.Empty;
                    float selectionPercentage = tierRoll != null ? math.max(0f, tierRoll.SelectionPercentage) : 0f;
                    float baseSelectionPercentage = selectionPercentage;
                    string scalingFormula = string.Empty;

                    if (PlayerRuntimeScalingBakeMetadataUtility.TryResolveDropPoolTierRollScalingData(sourcePowerUpsPreset,
                                                                                                      resolvedDropPoolIndex,
                                                                                                      tierRollIndex,
                                                                                                      out float resolvedBaseSelectionPercentage,
                                                                                                      out string resolvedScalingFormula))
                    {
                        baseSelectionPercentage = math.max(0f, resolvedBaseSelectionPercentage);
                        scalingFormula = resolvedScalingFormula;
                    }

                    tierRollArray[tierRollIndex] = new PlayerMilestoneTierRollBlob
                    {
                        SelectionPercentage = selectionPercentage,
                        BaseSelectionPercentage = baseSelectionPercentage
                    };
                    builder.AllocateString(ref tierRollArray[tierRollIndex].TierId, tierId);
                    builder.AllocateString(ref tierRollArray[tierRollIndex].ScalingFormula, string.IsNullOrWhiteSpace(scalingFormula) ? string.Empty : scalingFormula);
                }

                continue;
            }

            // Preserve legacy inline tier-roll data when no valid drop pool is selected yet.
            for (int tierRollIndex = 0; tierRollIndex < tierRollCount; tierRollIndex++)
            {
                PlayerMilestoneTierRollDefinition tierRoll = legacyTierRolls[tierRollIndex];
                string tierId = tierRoll != null && !string.IsNullOrWhiteSpace(tierRoll.TierId)
                    ? tierRoll.TierId.Trim()
                    : string.Empty;
                float selectionPercentage = tierRoll != null ? math.max(0f, tierRoll.SelectionPercentage) : 0f;
                float baseSelectionPercentage = selectionPercentage;
                string scalingFormula = string.Empty;

                if (PlayerRuntimeScalingBakeMetadataUtility.TryResolveLegacyMilestoneTierRollScalingData(sourcePreset,
                                                                                                         phaseIndex,
                                                                                                         milestoneIndex,
                                                                                                         powerUpUnlockIndex,
                                                                                                         tierRollIndex,
                                                                                                         out float resolvedBaseSelectionPercentage,
                                                                                                         out string resolvedScalingFormula))
                {
                    baseSelectionPercentage = math.max(0f, resolvedBaseSelectionPercentage);
                    scalingFormula = resolvedScalingFormula;
                }

                tierRollArray[tierRollIndex] = new PlayerMilestoneTierRollBlob
                {
                    SelectionPercentage = selectionPercentage,
                    BaseSelectionPercentage = baseSelectionPercentage
                };
                builder.AllocateString(ref tierRollArray[tierRollIndex].TierId, tierId);
                builder.AllocateString(ref tierRollArray[tierRollIndex].ScalingFormula, string.IsNullOrWhiteSpace(scalingFormula) ? string.Empty : scalingFormula);
            }
        }
    }

    /// <summary>
    /// Bakes skip-compensation resource entries for one milestone.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays.</param>
    /// <param name="milestoneBlob">Destination milestone blob.</param>
    /// <param name="milestone">Source milestone definition.</param>

    private static void BakeMilestoneSkipCompensations(BlobBuilder builder,
                                                       ref PlayerLevelUpMilestoneBlob milestoneBlob,
                                                       PlayerLevelUpMilestoneDefinition milestone)
    {
        IReadOnlyList<PlayerMilestoneSkipCompensationDefinition> skipCompensationResources = milestone != null ? milestone.SkipCompensationResources : null;
        int compensationCount = skipCompensationResources != null ? skipCompensationResources.Count : 0;
        BlobBuilderArray<PlayerMilestoneSkipCompensationBlob> compensationArray = builder.Allocate(ref milestoneBlob.SkipCompensationResources, compensationCount);

        for (int compensationIndex = 0; compensationIndex < compensationCount; compensationIndex++)
        {
            PlayerMilestoneSkipCompensationDefinition compensation = skipCompensationResources[compensationIndex];
            PlayerMilestoneSkipCompensationResourceType resourceType = compensation != null
                ? compensation.ResourceType
                : PlayerMilestoneSkipCompensationResourceType.Health;
            PlayerMilestoneCompensationApplyMode applyMode = compensation != null
                ? compensation.ApplyMode
                : PlayerMilestoneCompensationApplyMode.Flat;
            float value = compensation != null ? math.max(0f, compensation.Value) : 0f;

            compensationArray[compensationIndex] = new PlayerMilestoneSkipCompensationBlob
            {
                ResourceType = (byte)resourceType,
                ApplyMode = (byte)applyMode,
                Value = value
            };
        }
    }

    /// <summary>
    /// Bakes default scalable-stat values into the progression blob.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="root">Progression blob root being populated.</param>
    /// <param name="preset">Source progression preset.</param>

    private static void BakeProgressionScalableStats(BlobBuilder builder,
                                                     ref PlayerProgressionConfigBlob root,
                                                     PlayerProgressionPreset preset)
    {
        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = preset != null ? preset.ScalableStats : null;
        int scalableStatsCount = scalableStats != null ? scalableStats.Count : 0;
        BlobBuilderArray<PlayerScalableStatBlob> scalableStatsArray = builder.Allocate(ref root.ScalableStats, scalableStatsCount);

        for (int statIndex = 0; statIndex < scalableStatsCount; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];
            string statName = scalableStat != null ? scalableStat.StatName : string.Format("stat{0}", statIndex + 1);
            PlayerScalableStatType statType = scalableStat != null ? scalableStat.StatType : PlayerScalableStatType.Float;
            float defaultValue = scalableStat != null ? scalableStat.ResolveRuntimeDefaultValue() : 0f;
            float minimumValue = scalableStat != null ? scalableStat.MinimumValue : PlayerScalableStatClampUtility.DefaultMinimumValue;
            float maximumValue = scalableStat != null ? scalableStat.MaximumValue : PlayerScalableStatClampUtility.DefaultMaximumValue;
            bool defaultBooleanValue = scalableStat != null && scalableStat.DefaultBooleanValue;
            string defaultTokenValue = scalableStat != null ? scalableStat.DefaultTokenValue : string.Empty;

            if (string.IsNullOrWhiteSpace(statName))
                statName = string.Format("stat{0}", statIndex + 1);

            scalableStatsArray[statIndex] = new PlayerScalableStatBlob
            {
                Type = (byte)statType,
                DefaultValue = defaultValue,
                MinimumValue = minimumValue,
                MaximumValue = maximumValue,
                DefaultBooleanValue = defaultBooleanValue ? (byte)1 : (byte)0
            };
            builder.AllocateString(ref scalableStatsArray[statIndex].Name, statName);
            builder.AllocateString(ref scalableStatsArray[statIndex].DefaultTokenValue,
                                   string.IsNullOrWhiteSpace(defaultTokenValue) ? string.Empty : defaultTokenValue.Trim());
        }
    }

    /// <summary>
    /// Bakes repeating level-up schedules and resolves the equipped schedule index.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="root">Progression blob root being populated.</param>
    /// <param name="preset">Source progression preset.</param>
    /// <param name="sourcePreset">Unscaled source progression preset used to bake runtime scaling metadata.</param>

    private static void BakeProgressionSchedules(BlobBuilder builder,
                                                 ref PlayerProgressionConfigBlob root,
                                                 PlayerProgressionPreset preset,
                                                 PlayerProgressionPreset sourcePreset)
    {
        IReadOnlyList<PlayerLevelUpScheduleDefinition> schedules = preset != null ? preset.Schedules : null;
        int schedulesCount = schedules != null ? schedules.Count : 0;
        BlobBuilderArray<PlayerLevelUpScheduleBlob> schedulesArray = builder.Allocate(ref root.Schedules, schedulesCount);
        root.EquippedScheduleIndex = -1;
        string equippedScheduleId = preset != null ? preset.EquippedScheduleId : string.Empty;

        for (int scheduleIndex = 0; scheduleIndex < schedulesCount; scheduleIndex++)
        {
            PlayerLevelUpScheduleDefinition schedule = schedules[scheduleIndex];
            string scheduleId = schedule != null && !string.IsNullOrWhiteSpace(schedule.ScheduleId)
                ? schedule.ScheduleId.Trim()
                : string.Format("Schedule{0}", scheduleIndex + 1);

            schedulesArray[scheduleIndex] = new PlayerLevelUpScheduleBlob();
            builder.AllocateString(ref schedulesArray[scheduleIndex].ScheduleId, scheduleId);

            IReadOnlyList<PlayerLevelUpScheduleStepDefinition> sequence = schedule != null ? schedule.Sequence : null;
            int stepCount = sequence != null ? sequence.Count : 0;
            BlobBuilderArray<PlayerLevelUpScheduleStepBlob> stepArray = builder.Allocate(ref schedulesArray[scheduleIndex].Steps, stepCount);

            // Serialize schedule steps in authoring order for deterministic runtime cycling.
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                PlayerLevelUpScheduleStepDefinition step = sequence[stepIndex];
                string statName = step != null && !string.IsNullOrWhiteSpace(step.StatName)
                    ? step.StatName.Trim()
                    : string.Empty;
                PlayerLevelUpScheduleApplyMode applyMode = step != null ? step.ApplyMode : PlayerLevelUpScheduleApplyMode.Flat;
                float value = step != null ? step.Value : 0f;
                float baseValue = value;
                string scalingFormula = string.Empty;

                if (PlayerRuntimeScalingBakeMetadataUtility.TryResolveScheduleStepValueScalingData(sourcePreset,
                                                                                                   scheduleIndex,
                                                                                                   stepIndex,
                                                                                                   out float resolvedBaseValue,
                                                                                                   out string resolvedScalingFormula))
                {
                    baseValue = resolvedBaseValue;
                    scalingFormula = resolvedScalingFormula;
                }

                stepArray[stepIndex] = new PlayerLevelUpScheduleStepBlob
                {
                    ApplyMode = (byte)applyMode,
                    Value = value,
                    BaseValue = baseValue
                };
                builder.AllocateString(ref stepArray[stepIndex].StatName, statName);
                builder.AllocateString(ref stepArray[stepIndex].ScalingFormula,
                                       string.IsNullOrWhiteSpace(scalingFormula) ? string.Empty : scalingFormula);
            }

            if (root.EquippedScheduleIndex >= 0 || string.IsNullOrWhiteSpace(equippedScheduleId))
                continue;

            if (!string.Equals(scheduleId, equippedScheduleId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            root.EquippedScheduleIndex = scheduleIndex;
        }

        if (root.EquippedScheduleIndex >= 0 || schedulesCount <= 0)
            return;

        root.EquippedScheduleIndex = 0;
    }

    private static IReadOnlyList<PowerUpDropPoolTierDefinition> ResolveDropPoolTierRolls(PlayerPowerUpsPreset powerUpsPreset,
                                                                                          string dropPoolId,
                                                                                          out int dropPoolIndex)
    {
        dropPoolIndex = -1;

        if (powerUpsPreset == null || powerUpsPreset.DropPools == null || string.IsNullOrWhiteSpace(dropPoolId))
            return null;

        for (int dropPoolIndexValue = 0; dropPoolIndexValue < powerUpsPreset.DropPools.Count; dropPoolIndexValue++)
        {
            PowerUpDropPoolDefinition dropPool = powerUpsPreset.DropPools[dropPoolIndexValue];

            if (dropPool == null || string.IsNullOrWhiteSpace(dropPool.PoolId))
                continue;

            if (!string.Equals(dropPool.PoolId, dropPoolId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            dropPoolIndex = dropPoolIndexValue;
            return dropPool.TierRolls;
        }

        return null;
    }
    #endregion

    #endregion
}
