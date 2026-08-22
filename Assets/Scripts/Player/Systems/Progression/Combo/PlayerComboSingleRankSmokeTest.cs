#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Runs deterministic editor checks for single-rank thresholds, presentation, damage downgrade, decay, and bonus distribution.
/// </summary>
public static class PlayerComboSingleRankSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    // [UnityEditor.MenuItem("Tools/Player/Run Single Rank Combo Smoke Test")]
    /// <summary>
    /// Executes the complete single-rank combo smoke suite from Unity batch mode through -executeMethod.
    /// </summary>
    public static void Run()
    {
        World world = new World("Single Rank Combo Smoke Test");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity entity = entityManager.CreateEntity();
            entityManager.AddBuffer<PlayerRuntimeComboRankElement>(entity);
            entityManager.AddBuffer<PlayerRuntimeComboPassiveUnlockElement>(entity);
            entityManager.AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
            DynamicBuffer<PlayerRuntimeComboRankElement> ranks = entityManager.GetBuffer<PlayerRuntimeComboRankElement>(entity);
            DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> passiveUnlocks = entityManager.GetBuffer<PlayerRuntimeComboPassiveUnlockElement>(entity);
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> formulas = entityManager.GetBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
            BuildMilestones(ranks, formulas);
            ValidateScalingPipeline(ranks, passiveUnlocks);
            ValidateThresholdsAndPresentation(ranks);
            ValidateFirstMilestonePresentationGate(ranks);
            ValidateDamageDowngradeAndDecay(ranks);
            ValidateFormulaDistribution(ranks, formulas);
            ValidateMilestoneTierFormulaContext(entityManager);
            Debug.Log("[PlayerComboSingleRankSmokeTest] Threshold, presentation, scaling, downgrade, decay, formula-range, milestone tier-context, pickup-radius, and container-context checks passed.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Verifies first-milestone Boolean keys and the linear range enum accept unified formula results.
    /// </summary>
    /// <param name="ranks">Runtime milestone buffer required by the shared field-apply path.</param>
    /// <param name="passiveUnlocks">Runtime passive-unlock buffer required by the shared field-apply path.</param>
    private static void ValidateScalingPipeline(DynamicBuffer<PlayerRuntimeComboRankElement> ranks,
                                                DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> passiveUnlocks)
    {
        PlayerComboCounterMode entryMode;
        int entryIndex;
        int passiveUnlockIndex;
        PlayerRuntimeComboCounterFieldId visibilityFieldId;
        PlayerRuntimeComboCounterFieldId formulaFieldId;
        PlayerRuntimeComboCounterFieldId rangeModeFieldId;
        bool mapsVisibility = PlayerRuntimeScalingComboFieldMappingUtility.TryMapFieldId(
            "comboCounter.singleRankProgression.showMeterOnlyAfterFirstMilestone",
            out entryMode,
            out entryIndex,
            out passiveUnlockIndex,
            out visibilityFieldId);
        bool mapsFormula = PlayerRuntimeScalingComboFieldMappingUtility.TryMapFieldId(
            "comboCounter.singleRankProgression.startLinearBonusesAtFirstMilestone",
            out entryMode,
            out entryIndex,
            out passiveUnlockIndex,
            out formulaFieldId);
        bool mapsRangeMode = PlayerRuntimeScalingComboFieldMappingUtility.TryMapFieldId(
            "comboCounter.singleRankProgression.linearBonusRangeMode",
            out entryMode,
            out entryIndex,
            out passiveUnlockIndex,
            out rangeModeFieldId);
        PlayerRuntimeComboCounterConfig config = CreateConfig(PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression);
        PlayerRuntimeScalingComboFieldApplyUtility.ApplyBooleanValue(visibilityFieldId,
                                                                      entryMode,
                                                                      entryIndex,
                                                                      passiveUnlockIndex,
                                                                      true,
                                                                      ref config,
                                                                      ranks,
                                                                      passiveUnlocks);
        PlayerRuntimeScalingComboFieldApplyUtility.ApplyBooleanValue(formulaFieldId,
                                                                      entryMode,
                                                                      entryIndex,
                                                                      passiveUnlockIndex,
                                                                      true,
                                                                      ref config,
                                                                      ranks,
                                                                      passiveUnlocks);
        PlayerRuntimeScalingComboFieldApplyUtility.ApplyNumericValue(rangeModeFieldId,
                                                                      entryMode,
                                                                      entryIndex,
                                                                      1f,
                                                                      ref config,
                                                                      ranks);

        if (!mapsVisibility ||
            !mapsFormula ||
            !mapsRangeMode ||
            visibilityFieldId != PlayerRuntimeComboCounterFieldId.SingleRankShowMeterOnlyAfterFirstMilestone ||
            formulaFieldId != PlayerRuntimeComboCounterFieldId.SingleRankStartLinearBonusesAtFirstMilestone ||
            rangeModeFieldId != PlayerRuntimeComboCounterFieldId.SingleRankLinearBonusRangeMode ||
            config.SingleRankShowMeterOnlyAfterFirstMilestone == 0 ||
            config.SingleRankStartLinearBonusesAtFirstMilestone == 0 ||
            config.SingleRankLinearBonusRangeMode != PlayerComboSingleRankLinearBonusRangeMode.MilestoneToNextMilestone)
            throw new Exception("Single-rank formula options did not complete their typed Add Scaling pipeline.");
    }

    /// <summary>
    /// Verifies the optional presentation gate keeps the meter inactive until the first enabled milestone threshold.
    /// </summary>
    /// <param name="ranks">Runtime milestone buffer used to resolve the first threshold.</param>
    private static void ValidateFirstMilestonePresentationGate(DynamicBuffer<PlayerRuntimeComboRankElement> ranks)
    {
        PlayerRuntimeComboCounterConfig config = CreateConfig(PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps);
        config.SingleRankShowMeterOnlyAfterFirstMilestone = 1;
        PlayerComboCounterState state = new PlayerComboCounterState
        {
            CurrentValue = 249
        };
        PlayerComboCounterRuntimeUtility.UpdatePresentation(ref state, in config, ranks);

        if (state.CurrentRankIndex >= 0)
            throw new Exception("Single-rank presentation became active before the first enabled milestone.");

        state.CurrentValue = 250;
        PlayerComboCounterRuntimeUtility.UpdatePresentation(ref state, in config, ranks);

        if (state.CurrentRankIndex != 0 || Math.Abs(state.ProgressNormalized - 0.25f) > PrecisionEpsilon)
            throw new Exception("Single-rank presentation did not activate at the first enabled milestone.");
    }

    /// <summary>
    /// Builds three enabled milestone entries and two flattened formula ranges for deterministic checks.
    /// </summary>
    /// <param name="ranks">Runtime milestone buffer populated in place.</param>
    /// <param name="formulas">Flattened Character Tuning formula buffer populated in place.</param>
    private static void BuildMilestones(DynamicBuffer<PlayerRuntimeComboRankElement> ranks,
                                        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> formulas)
    {
        formulas.Add(new PlayerPowerUpCharacterTuningFormulaElement
        {
            Formula = new FixedString128Bytes("[Damage]=[Damage]+10")
        });
        formulas.Add(new PlayerPowerUpCharacterTuningFormulaElement
        {
            Formula = new FixedString128Bytes("[Damage]=[Damage]+20")
        });

        ranks.Add(CreateMilestone("Quarter", 250, 0, 1));
        ranks.Add(CreateMilestone("Half", 500, 1, 1));
        ranks.Add(CreateMilestone("Complete", 1000, 2, 0));
    }

    /// <summary>
    /// Creates one runtime single-rank milestone element.
    /// </summary>
    /// <param name="id">Stable milestone identifier.</param>
    /// <param name="requiredValue">Combo threshold required by the milestone.</param>
    /// <param name="formulaStartIndex">Flattened formula range start index.</param>
    /// <param name="formulaCount">Flattened formula count.</param>
    /// <returns>Configured runtime milestone element.</returns>
    private static PlayerRuntimeComboRankElement CreateMilestone(string id,
                                                                 int requiredValue,
                                                                 int formulaStartIndex,
                                                                 int formulaCount)
    {
        return new PlayerRuntimeComboRankElement
        {
            Mode = PlayerComboCounterMode.SingleRankProgression,
            RankId = new FixedString64Bytes(id),
            Enabled = 1,
            RequiredComboValue = requiredValue,
            RequiredProgressPercent = requiredValue * 0.1f,
            BonusFormulaStartIndex = formulaStartIndex,
            BonusFormulaCount = formulaCount
        };
    }

    /// <summary>
    /// Verifies percentage threshold conversion, active milestone resolution, and one-bar presentation state.
    /// </summary>
    /// <param name="ranks">Runtime milestone buffer used by combo resolution.</param>
    private static void ValidateThresholdsAndPresentation(DynamicBuffer<PlayerRuntimeComboRankElement> ranks)
    {
        PlayerRuntimeComboCounterConfig config = CreateConfig(PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps);
        int convertedThreshold = PlayerComboCounterRuntimeUtility.ResolveSingleRankMilestoneRequiredValue(1000, 25f);
        int activeIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(600, in config, ranks);
        PlayerComboCounterState state = new PlayerComboCounterState
        {
            CurrentValue = 750
        };
        PlayerComboCounterRuntimeUtility.UpdatePresentation(ref state, in config, ranks);

        if (convertedThreshold != 250 ||
            activeIndex != 1 ||
            state.CurrentRankIndex != 0 ||
            !state.CurrentRankId.Equals(config.SingleRankId) ||
            state.NextRankRequiredValue != 1000 ||
            Math.Abs(state.ProgressNormalized - 0.75f) > PrecisionEpsilon)
        {
            throw new Exception("Single-rank threshold conversion or presentation state is invalid.");
        }
    }

    /// <summary>
    /// Verifies milestone-based damage downgrade and continuous single-rank point decay.
    /// </summary>
    /// <param name="ranks">Runtime milestone buffer used by downgrade resolution.</param>
    private static void ValidateDamageDowngradeAndDecay(DynamicBuffer<PlayerRuntimeComboRankElement> ranks)
    {
        PlayerRuntimeComboCounterConfig config = CreateConfig(PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps);
        config.DamageBreakMode = PlayerComboDamageBreakMode.DowngradeToPreviousRank;
        config.SingleRankPointsDecayPerSecond = 10f;
        int downgradedValue = PlayerComboCounterRuntimeUtility.ResolveDamageBreakComboValue(600, in config, ranks);
        PlayerComboCounterState state = new PlayerComboCounterState
        {
            CurrentValue = 100
        };
        PlayerComboCounterRuntimeUtility.ApplyRankDecay(ref state, in config, ranks, 1.5f);

        if (downgradedValue != 250 || state.CurrentValue != 85)
            throw new Exception("Single-rank damage downgrade or point decay is invalid.");
    }

    /// <summary>
    /// Verifies milestone formulas apply cumulatively at steps and blend across rank-wide or milestone-local intervals.
    /// </summary>
    /// <param name="ranks">Runtime milestone buffer owning formula ranges.</param>
    /// <param name="formulas">Flattened formula buffer evaluated by combo bonuses.</param>
    private static void ValidateFormulaDistribution(DynamicBuffer<PlayerRuntimeComboRankElement> ranks,
                                                    DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> formulas)
    {
        List<PlayerScalableStatElement> stats = CreateDamageStatList();
        PlayerRuntimeComboCounterConfig config = CreateConfig(PlayerComboSingleRankFormulaDistributionMode.MilestoneSteps);
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(1, 600, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 40f) > PrecisionEpsilon)
            throw new Exception("Single-rank milestone formulas did not apply cumulatively.");

        stats = CreateDamageStatList();
        config.SingleRankFormulaDistributionMode = PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression;
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(1, 500, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 25f) > PrecisionEpsilon)
            throw new Exception("Single-rank formulas did not blend linearly across total progression.");

        stats = CreateDamageStatList();
        config.SingleRankStartLinearBonusesAtFirstMilestone = 1;
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(0, 249, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 10f) > PrecisionEpsilon)
            throw new Exception("Single-rank linear formulas became active before the first enabled milestone.");

        stats = CreateDamageStatList();
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(0, 250, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 10f) > PrecisionEpsilon)
            throw new Exception("Single-rank linear formulas should begin at zero weight on the first enabled milestone.");

        stats = CreateDamageStatList();
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(1, 625, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 25f) > PrecisionEpsilon)
            throw new Exception("Single-rank formulas did not blend linearly across progression after the first milestone.");

        stats = CreateDamageStatList();
        config.SingleRankLinearBonusRangeMode = PlayerComboSingleRankLinearBonusRangeMode.MilestoneToNextMilestone;
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(0, 375, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 15f) > PrecisionEpsilon)
            throw new Exception("The first single-rank formula did not blend inside its milestone segment.");

        stats = CreateDamageStatList();
        PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(1, 750, in config, ranks, formulas, stats);

        if (Math.Abs(stats[0].Value - 30f) > PrecisionEpsilon)
            throw new Exception("Single-rank formulas did not accumulate across consecutive milestone segments.");

        float finalMilestoneWeightBeforeCompletion = PlayerComboSingleRankLinearBonusUtility.ResolveMilestoneProgressNormalized(999,
                                                                                                                                 2,
                                                                                                                                 in config,
                                                                                                                                 ranks);
        float finalMilestoneWeightAtCompletion = PlayerComboSingleRankLinearBonusUtility.ResolveMilestoneProgressNormalized(1000,
                                                                                                                             2,
                                                                                                                             in config,
                                                                                                                             ranks);

        if (finalMilestoneWeightBeforeCompletion > PrecisionEpsilon ||
            Math.Abs(finalMilestoneWeightAtCompletion - 1f) > PrecisionEpsilon)
            throw new Exception("A final milestone at maximum progression did not activate deterministically at completion.");
    }

    /// <summary>
    /// Verifies milestone tier formulas consume the effective Luck value after linear Synchro bonuses instead of the permanent base value.
    /// </summary>
    /// <param name="entityManager">Temporary smoke-test entity manager used to allocate ECS buffers.</param>
    private static void ValidateMilestoneTierFormulaContext(EntityManager entityManager)
    {
        // Build the milestone-time ECS state used by the effective formula-context pipeline.
        Entity entity = entityManager.CreateEntity();
        entityManager.AddBuffer<PlayerScalableStatElement>(entity);
        entityManager.AddBuffer<PlayerRoomRewardTemporaryModifierElement>(entity);
        entityManager.AddBuffer<PlayerRuntimeComboRankElement>(entity);
        entityManager.AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
        DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.GetBuffer<PlayerScalableStatElement>(entity);
        DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers = entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(entity);
        DynamicBuffer<PlayerRuntimeComboRankElement> ranks = entityManager.GetBuffer<PlayerRuntimeComboRankElement>(entity);
        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> formulas = entityManager.GetBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
        scalableStats.Add(new PlayerScalableStatElement
        {
            Name = new FixedString64Bytes("Luck"),
            Type = (byte)PlayerScalableStatType.Float,
            MinimumValue = 0f,
            MaximumValue = 100f,
            Value = 1f
        });
        formulas.Add(new PlayerPowerUpCharacterTuningFormulaElement
        {
            Formula = new FixedString128Bytes("[Luck]=[Luck]+50")
        });
        ranks.Add(new PlayerRuntimeComboRankElement
        {
            Mode = PlayerComboCounterMode.SingleRankProgression,
            RankId = new FixedString64Bytes("SYNCHRO"),
            Enabled = 1,
            RequiredComboValue = 0,
            RequiredProgressPercent = 0f,
            BonusFormulaStartIndex = 0,
            BonusFormulaCount = 1
        });

        // Compose the 10% Synchro sample through the same reusable path consumed by milestone rolls.
        PlayerRuntimeComboCounterConfig config = CreateConfig(PlayerComboSingleRankFormulaDistributionMode.LinearAcrossProgression);
        PlayerComboCounterState state = new PlayerComboCounterState
        {
            CurrentValue = 100
        };
        List<PlayerScalableStatElement> effectiveStats = new List<PlayerScalableStatElement>(1);
        Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
        PlayerRuntimeScalingFormulaContextUtility.Fill(scalableStats,
                                                        temporaryModifiers,
                                                        0u,
                                                        in config,
                                                        in state,
                                                        ranks,
                                                        formulas,
                                                        effectiveStats,
                                                        variableContext);

        // Resolve the three configured drop-tier formulas against effective Luck 6.
        float tierOnePercentage = PlayerMilestonePowerUpRollFormulaUtility.ResolveTierRollPercentage(60f,
                                                                                                      60f,
                                                                                                      "([Luck]>5)?0:60",
                                                                                                      variableContext);
        float tierTwoPercentage = PlayerMilestonePowerUpRollFormulaUtility.ResolveTierRollPercentage(30f,
                                                                                                      30f,
                                                                                                      "([Luck]>5)?0:30",
                                                                                                      variableContext);
        float tierThreePercentage = PlayerMilestonePowerUpRollFormulaUtility.ResolveTierRollPercentage(10f,
                                                                                                        10f,
                                                                                                        "([Luck]>5)?100:10",
                                                                                                        variableContext);

        // Require the exact effective-stat threshold and deterministic Tier 3 distribution.
        if (!variableContext.TryGetValue("Luck", out PlayerFormulaValue luckValue) ||
            luckValue.Type != PlayerFormulaValueType.Number ||
            Math.Abs(luckValue.NumberValue - 6f) > PrecisionEpsilon ||
            tierOnePercentage > PrecisionEpsilon ||
            tierTwoPercentage > PrecisionEpsilon ||
            Math.Abs(tierThreePercentage - 100f) > PrecisionEpsilon)
            throw new Exception("Milestone tier formulas did not consume the effective Luck value produced by 10% Synchro progression.");

        // Resolve progression Add Scaling from the same effective list retained by the runtime rebuild.
        BlobAssetReference<PlayerProgressionConfigBlob> progressionBlob = default;
        BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);

        try
        {
            ref PlayerProgressionConfigBlob progressionRoot =
                ref blobBuilder.ConstructRoot<PlayerProgressionConfigBlob>();
            progressionRoot.BaseExperiencePickupRadius = 1f;
            progressionRoot.ExperiencePickupRadius = 1f;
            blobBuilder.AllocateString(ref progressionRoot.ExperiencePickupRadiusScalingFormula,
                                       "[this] * [Luck]");
            progressionBlob = blobBuilder.CreateBlobAssetReference<PlayerProgressionConfigBlob>(Allocator.Temp);
        }
        finally
        {
            blobBuilder.Dispose();
        }

        try
        {
            PlayerProgressionConfig progressionConfig = new PlayerProgressionConfig
            {
                Config = progressionBlob
            };
            float pickupRadius = PlayerExperiencePickupRadiusRuntimeUtility.ResolveCurrentPickupRadius(progressionConfig,
                                                                                                        effectiveStats,
                                                                                                        1f);

            if (Math.Abs(pickupRadius - 6f) > PrecisionEpsilon)
                throw new Exception("Experience pickup radius scaling did not consume the effective Luck value produced by 10% Synchro progression.");
        }
        finally
        {
            progressionBlob.Dispose();
        }

        // Resolve a container formula through the EntityManager entry point used by HUD and ECS swaps.
        entityManager.AddComponentData(entity, config);
        entityManager.AddComponentData(entity, state);
        entityManager.AddComponentData(entity, new PlayerPowerUpContainerInteractionConfig
        {
            BaseInteractionLockDuration = 1f,
            InteractionLockDuration = 1f,
            InteractionLockDurationScalingFormula = new FixedString512Bytes("[this] * [Luck]")
        });
        float interactionLockDuration =
            PlayerPowerUpContainerInteractionRuntimeUtility.ResolveInteractionLockDuration(entityManager,
                                                                                            entity);

        if (Math.Abs(interactionLockDuration - 6f) > PrecisionEpsilon)
            throw new Exception("Container interaction scaling did not consume the effective Luck value produced by 10% Synchro progression.");
    }

    /// <summary>
    /// Creates the single-rank runtime config shared by deterministic checks.
    /// </summary>
    /// <param name="distributionMode">Formula distribution behavior assigned to the config.</param>
    /// <returns>Configured single-rank runtime data.</returns>
    private static PlayerRuntimeComboCounterConfig CreateConfig(PlayerComboSingleRankFormulaDistributionMode distributionMode)
    {
        return new PlayerRuntimeComboCounterConfig
        {
            Enabled = 1,
            Mode = PlayerComboCounterMode.SingleRankProgression,
            ComboGainPerKill = 10,
            SingleRankId = new FixedString64Bytes("SYNCHRO"),
            SingleRankMaximumComboValue = 1000,
            SingleRankFormulaDistributionMode = distributionMode
        };
    }

    /// <summary>
    /// Creates one numeric Damage stat used to verify Character Tuning formula application.
    /// </summary>
    /// <returns>Mutable scalable-stat list initialized with Damage at 10.</returns>
    private static List<PlayerScalableStatElement> CreateDamageStatList()
    {
        return new List<PlayerScalableStatElement>
        {
            new PlayerScalableStatElement
            {
                Name = new FixedString64Bytes("Damage"),
                Type = (byte)PlayerScalableStatType.Float,
                MinimumValue = -1000f,
                MaximumValue = 1000f,
                Value = 10f
            }
        };
    }
    #endregion

    #endregion
}
#endif
