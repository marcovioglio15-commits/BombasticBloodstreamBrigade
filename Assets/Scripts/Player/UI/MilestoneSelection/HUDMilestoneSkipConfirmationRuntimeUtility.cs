using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves milestone skip hold confirmation settings from baked ECS progression data and scalable-stat formulas.
/// </summary>
internal static class HUDMilestoneSkipConfirmationRuntimeUtility
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current milestone skip hold settings for one player entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read progression and scalable-stat runtime data.</param>
    /// <param name="playerEntity">Player entity that owns the current progression config.</param>
    /// <param name="settings">Resolved settings when progression config is available.</param>
    /// <param name="scalingHash">Current runtime scaling hash used by callers to cache the resolved settings.</param>
    /// <param name="configHash">Progression blob reference hash used by callers to invalidate cache after rebake.</param>
    /// <returns>True when settings were resolved from ECS data; otherwise false.</returns>
    public static bool TryResolveSettings(EntityManager entityManager,
                                          Entity playerEntity,
                                          out HUDMilestoneSkipConfirmationSettings settings,
                                          out uint scalingHash,
                                          out int configHash)
    {
        settings = HUDMilestoneSkipConfirmationSettings.Default;
        scalingHash = 0u;
        configHash = 0;

        if (!entityManager.Exists(playerEntity))
            return false;

        if (!entityManager.HasComponent<PlayerProgressionConfig>(playerEntity))
            return false;

        PlayerProgressionConfig progressionConfig = entityManager.GetComponentData<PlayerProgressionConfig>(playerEntity);

        if (!progressionConfig.Config.IsCreated)
            return false;

        ref PlayerProgressionConfigBlob root = ref progressionConfig.Config.Value;
        scalingHash = ResolveScalingHash(entityManager, playerEntity);
        configHash = progressionConfig.Config.GetHashCode();

        if (HasAnyScalingFormula(ref root))
            BuildVariableContext(entityManager, playerEntity);
        else
            VariableContext.Clear();

        float holdSeconds = ResolveNumericValue(root.MilestoneSkipHoldConfirmationSeconds,
                                                root.BaseMilestoneSkipHoldConfirmationSeconds,
                                                root.MilestoneSkipHoldConfirmationSecondsScalingFormula.ToString());
        Color fillColor = ResolveFillColor(ref root);
        settings = new HUDMilestoneSkipConfirmationSettings(holdSeconds, fillColor);
        return true;
    }
    #endregion

    #region Resolution
    /// <summary>
    /// Checks whether the progression blob contains any milestone skip formula that needs a variable context.
    /// </summary>
    /// <param name="root">Progression blob root being inspected.</param>
    /// <returns>True when at least one milestone skip formula is present; otherwise false.</returns>
    private static bool HasAnyScalingFormula(ref PlayerProgressionConfigBlob root)
    {
        if (!string.IsNullOrWhiteSpace(root.MilestoneSkipHoldConfirmationSecondsScalingFormula.ToString()))
            return true;

        if (!string.IsNullOrWhiteSpace(root.MilestoneSkipHoldFillColorRScalingFormula.ToString()))
            return true;

        if (!string.IsNullOrWhiteSpace(root.MilestoneSkipHoldFillColorGScalingFormula.ToString()))
            return true;

        if (!string.IsNullOrWhiteSpace(root.MilestoneSkipHoldFillColorBScalingFormula.ToString()))
            return true;

        return !string.IsNullOrWhiteSpace(root.MilestoneSkipHoldFillColorAScalingFormula.ToString());
    }

    /// <summary>
    /// Builds the effective formula variable context, including active combo-rank Character Tuning bonuses when present.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read player buffers and components.</param>
    /// <param name="playerEntity">Player entity that owns scalable stat data.</param>
    private static void BuildVariableContext(EntityManager entityManager, Entity playerEntity)
    {
        VariableContext.Clear();
        EffectiveScalableStats.Clear();

        if (!entityManager.HasBuffer<PlayerScalableStatElement>(playerEntity))
            return;

        DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
        PlayerRuntimeScalingComboApplyUtility.CopyBaseScalableStats(scalableStats, EffectiveScalableStats);

        if (entityManager.HasComponent<PlayerRuntimeComboCounterConfig>(playerEntity) &&
            entityManager.HasComponent<PlayerComboCounterState>(playerEntity) &&
            entityManager.HasBuffer<PlayerRuntimeComboRankElement>(playerEntity) &&
            entityManager.HasBuffer<PlayerPowerUpCharacterTuningFormulaElement>(playerEntity))
        {
            PlayerComboCounterState comboState = entityManager.GetComponentData<PlayerComboCounterState>(playerEntity);
            PlayerRuntimeComboCounterConfig comboConfig = entityManager.GetComponentData<PlayerRuntimeComboCounterConfig>(playerEntity);
            DynamicBuffer<PlayerRuntimeComboRankElement> comboRanks = entityManager.GetBuffer<PlayerRuntimeComboRankElement>(playerEntity);
            int activeRankIndex = PlayerComboCounterRuntimeUtility.ResolveActiveRankIndex(comboState.CurrentValue,
                                                                                          in comboConfig,
                                                                                          comboRanks);
            PlayerRuntimeScalingComboApplyUtility.ApplyActiveComboRankBonuses(activeRankIndex,
                                                                              comboState.CurrentValue,
                                                                              comboRanks,
                                                                              entityManager.GetBuffer<PlayerPowerUpCharacterTuningFormulaElement>(playerEntity),
                                                                              EffectiveScalableStats);
        }

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(EffectiveScalableStats, VariableContext);
    }

    /// <summary>
    /// Resolves a cache hash matching the runtime-scaled variable context as closely as available from managed HUD code.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read runtime scaling state.</param>
    /// <param name="playerEntity">Player entity that owns scalable stat data.</param>
    /// <returns>Runtime scaling hash when initialized, otherwise a direct scalable-stat hash.</returns>
    private static uint ResolveScalingHash(EntityManager entityManager, Entity playerEntity)
    {
        if (entityManager.HasComponent<PlayerRuntimeScalingState>(playerEntity))
        {
            PlayerRuntimeScalingState scalingState = entityManager.GetComponentData<PlayerRuntimeScalingState>(playerEntity);

            if (scalingState.Initialized != 0)
                return scalingState.LastScalableStatsHash;
        }

        if (!entityManager.HasBuffer<PlayerScalableStatElement>(playerEntity))
            return 0u;

        return PlayerScalableStatHashUtility.ComputeHash(entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity));
    }

    /// <summary>
    /// Resolves one non-negative numeric setting from its baked runtime value and optional formula.
    /// </summary>
    /// <param name="runtimeValue">Scaled value baked into the progression blob.</param>
    /// <param name="baseValue">Unscaled base value used as [this] for runtime formulas.</param>
    /// <param name="formula">Optional formula evaluated against current scalable stats.</param>
    /// <returns>Finite non-negative value used by the HUD.</returns>
    private static float ResolveNumericValue(float runtimeValue, float baseValue, string formula)
    {
        float resolvedValue = ResolveFiniteValue(runtimeValue, 0f);

        if (string.IsNullOrWhiteSpace(formula))
            return math.max(0f, resolvedValue);

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                   ResolveFiniteValue(baseValue, resolvedValue),
                                                                   VariableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return math.max(0f, resolvedValue);
        }

        return math.max(0f, ResolveFiniteValue(evaluatedValue, resolvedValue));
    }

    /// <summary>
    /// Resolves the milestone skip hold fill color, evaluating per-channel formulas when present.
    /// </summary>
    /// <param name="root">Progression blob root containing color values and formulas.</param>
    /// <returns>Presentation color with channels clamped to 0..1.</returns>
    private static Color ResolveFillColor(ref PlayerProgressionConfigBlob root)
    {
        float red = ResolveColorChannel(root.MilestoneSkipHoldFillColor.x,
                                        root.BaseMilestoneSkipHoldFillColor.x,
                                        root.MilestoneSkipHoldFillColorRScalingFormula.ToString());
        float green = ResolveColorChannel(root.MilestoneSkipHoldFillColor.y,
                                          root.BaseMilestoneSkipHoldFillColor.y,
                                          root.MilestoneSkipHoldFillColorGScalingFormula.ToString());
        float blue = ResolveColorChannel(root.MilestoneSkipHoldFillColor.z,
                                         root.BaseMilestoneSkipHoldFillColor.z,
                                         root.MilestoneSkipHoldFillColorBScalingFormula.ToString());
        float alpha = ResolveColorChannel(root.MilestoneSkipHoldFillColor.w,
                                          root.BaseMilestoneSkipHoldFillColor.w,
                                          root.MilestoneSkipHoldFillColorAScalingFormula.ToString());
        return new Color(red, green, blue, alpha);
    }

    /// <summary>
    /// Resolves one color channel from baked runtime data and optional scaling formula.
    /// </summary>
    /// <param name="runtimeValue">Scaled channel value baked into the progression blob.</param>
    /// <param name="baseValue">Unscaled channel base value used as [this].</param>
    /// <param name="formula">Optional formula evaluated against current scalable stats.</param>
    /// <returns>Finite channel value clamped to the 0..1 presentation range.</returns>
    private static float ResolveColorChannel(float runtimeValue, float baseValue, string formula)
    {
        float resolvedValue = ResolveFiniteValue(runtimeValue, 0f);

        if (string.IsNullOrWhiteSpace(formula))
            return math.saturate(resolvedValue);

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                   ResolveFiniteValue(baseValue, resolvedValue),
                                                                   VariableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return math.saturate(resolvedValue);
        }

        return math.saturate(ResolveFiniteValue(evaluatedValue, resolvedValue));
    }

    /// <summary>
    /// Replaces non-finite float values with a deterministic fallback.
    /// </summary>
    /// <param name="value">Raw value to inspect.</param>
    /// <param name="fallbackValue">Fallback returned when the value is NaN or infinite.</param>
    /// <returns>Finite value suitable for runtime math.</returns>
    private static float ResolveFiniteValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallbackValue;

        return value;
    }
    #endregion

    #endregion
}
