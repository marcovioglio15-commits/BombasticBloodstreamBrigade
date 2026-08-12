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
            entityManager.AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
            DynamicBuffer<PlayerRuntimeComboRankElement> ranks = entityManager.GetBuffer<PlayerRuntimeComboRankElement>(entity);
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> formulas = entityManager.GetBuffer<PlayerPowerUpCharacterTuningFormulaElement>(entity);
            BuildMilestones(ranks, formulas);
            ValidateThresholdsAndPresentation(ranks);
            ValidateDamageDowngradeAndDecay(ranks);
            ValidateFormulaDistribution(ranks, formulas);
            Debug.Log("[PlayerComboSingleRankSmokeTest] Threshold, presentation, downgrade, decay, and formula checks passed.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Private Methods
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
    /// Verifies milestone formulas apply cumulatively at steps and blend across total progress in linear mode.
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
