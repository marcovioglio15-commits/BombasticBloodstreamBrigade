using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides shared expected-length calculations for editor smoke tests that validate procedural syringe previews.
/// </summary>
internal static class PlayerSyringeBarPreviewLengthTestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the expected syringe length using the same authored value-track rules as the runtime view.
    /// </summary>
    /// <param name="config">Baked syringe visual configuration.</param>
    /// <param name="maximumValue">Maximum value represented by the syringe preview.</param>
    /// <returns>Expected RectTransform width after preview rebuild.</returns>
    public static float ResolveExpectedLength(PlayerHealthBarVisualConfig config, float maximumValue)
    {
        float layoutIntervalCount = ResolveLayoutIntervalCount(config, maximumValue);
        float minimumLength = Mathf.Max(1f, config.MinimumLength);
        float maximumLength = Mathf.Max(minimumLength, config.MaximumLength);
        float graduationStartInset = Mathf.Max(0f, config.EndCapWidth) +
                                     Mathf.Max(0f, config.GraduationEndPadding);
        float graduationEndInset = Mathf.Max(0f, config.EndCapWidth);

        if (config.BodyStyle == PlayerSyringeBodyStyle.SimplePaintedContainer)
        {
            graduationStartInset = Mathf.Max(0f, config.EndCapWidth) * 0.5f +
                                   Mathf.Max(0f, config.GraduationEndPadding);
            graduationEndInset += Mathf.Max(0f, config.TerminationOffset);
        }

        float targetLength = graduationStartInset +
                             layoutIntervalCount * Mathf.Max(0.0001f, config.PixelsPerMajorDivision) +
                             graduationEndInset;
        return Mathf.Clamp(targetLength, minimumLength, maximumLength);
    }

    /// <summary>
    /// Resolves one controller value through progression default scalable stats for Edit Mode preview assertions.
    /// </summary>
    /// <param name="masterPreset">Player master preset supplying progression defaults.</param>
    /// <param name="controllerPreset">Controller preset containing Add Scaling rules.</param>
    /// <param name="targetStatKey">Controller stat key to resolve.</param>
    /// <param name="baseValue">Unscaled controller value used as [this] and fallback.</param>
    /// <returns>Formula-resolved value, or the base value when no matching rule succeeds.</returns>
    public static float ResolveExpectedScaledControllerValue(PlayerMasterPreset masterPreset,
                                                             PlayerControllerPreset controllerPreset,
                                                             string targetStatKey,
                                                             float baseValue)
    {
        if (masterPreset == null || controllerPreset == null || controllerPreset.ScalingRules == null)
            return baseValue;

        Dictionary<string, PlayerFormulaValue> variableContext = BuildVariableContext(masterPreset.ProgressionPreset);
        string normalizedTargetStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(targetStatKey);
        IReadOnlyList<PlayerStatScalingRule> scalingRules = controllerPreset.ScalingRules;

        for (int ruleIndex = 0; ruleIndex < scalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling || string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            string normalizedRuleStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(scalingRule.StatKey);

            if (!string.Equals(normalizedRuleStatKey, normalizedTargetStatKey, StringComparison.Ordinal))
                continue;

            if (PlayerStatFormulaEngine.TryEvaluate(scalingRule.Formula,
                                                    baseValue,
                                                    variableContext,
                                                    out float resolvedValue,
                                                    out string _))
                return resolvedValue;
        }

        return baseValue;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the value-track interval count used by expected preview-length assertions.
    /// </summary>
    /// <param name="config">Baked syringe visual configuration.</param>
    /// <param name="maximumValue">Maximum value represented by the syringe preview.</param>
    /// <returns>Non-negative interval count used by syringe length.</returns>
    private static float ResolveLayoutIntervalCount(PlayerHealthBarVisualConfig config, float maximumValue)
    {
        switch (config.GraduationMode)
        {
            case PlayerSyringeGraduationMode.UniformLabels:
                return Mathf.Max(1f, config.UniformLabelCount > 1 ? config.UniformLabelCount - 1 : 1);
            default:
                return Mathf.Max(0f, maximumValue / Mathf.Max(0.0001f, config.UnitsPerMajorDivision));
        }
    }

    /// <summary>
    /// Builds the default scalable-stat context used by preview-length assertions.
    /// </summary>
    /// <param name="progressionPreset">Progression preset containing scalable-stat defaults.</param>
    /// <returns>Formula variable context keyed by scalable-stat name.</returns>
    private static Dictionary<string, PlayerFormulaValue> BuildVariableContext(PlayerProgressionPreset progressionPreset)
    {
        Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);

        if (progressionPreset == null || progressionPreset.ScalableStats == null)
            return variableContext;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = progressionPreset.ScalableStats;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];

            if (scalableStat == null || string.IsNullOrWhiteSpace(scalableStat.StatName))
                continue;

            variableContext[scalableStat.StatName] = scalableStat.ResolveRuntimeDefaultFormulaValue();
        }

        return variableContext;
    }
    #endregion

    #endregion
}
