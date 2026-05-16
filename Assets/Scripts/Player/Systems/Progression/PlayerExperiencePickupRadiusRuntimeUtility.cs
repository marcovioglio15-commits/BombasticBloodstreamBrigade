using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves and synchronizes the runtime experience pickup radius from progression scaling data and current scalable stats.
/// </summary>
internal static class PlayerExperiencePickupRadiusRuntimeUtility
{
    #region Constants
    private const float ComparisonEpsilon = 0.0001f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current runtime pickup radius using the baked base value and optional scaling formula.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config containing pickup radius metadata.</param>
    /// <param name="scalableStats">Current scalable-stat buffer used as formula variables.</param>
    /// <param name="fallbackValue">Fallback value returned when progression config is unavailable.</param>
    /// <returns>Resolved non-negative pickup radius.</returns>
    public static float ResolveCurrentPickupRadius(PlayerProgressionConfig progressionConfig,
                                                   DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                   float fallbackValue)
    {
        if (!progressionConfig.Config.IsCreated)
            return Mathf.Max(0f, fallbackValue);

        ref PlayerProgressionConfigBlob root = ref progressionConfig.Config.Value;
        float defaultPickupRadius = Mathf.Max(0f, root.ExperiencePickupRadius);
        string scalingFormula = root.ExperiencePickupRadiusScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return defaultPickupRadius;

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
            return defaultPickupRadius;

        variableContext.Clear();
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(scalingFormula,
                                                                   Mathf.Max(0f, root.BaseExperiencePickupRadius),
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return defaultPickupRadius;
        }

        return Mathf.Max(0f, evaluatedValue);
    }

    /// <summary>
    /// Resolves the current runtime pickup radius using the baked base value and optional scaling formula.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config containing pickup radius metadata.</param>
    /// <param name="scalableStats">Managed scalable-stat list used as formula variables.</param>
    /// <param name="fallbackValue">Fallback value returned when progression config is unavailable.</param>
    /// <returns>Resolved non-negative pickup radius.</returns>
    public static float ResolveCurrentPickupRadius(PlayerProgressionConfig progressionConfig,
                                                   IReadOnlyList<PlayerScalableStatElement> scalableStats,
                                                   float fallbackValue)
    {
        if (!progressionConfig.Config.IsCreated)
            return Mathf.Max(0f, fallbackValue);

        ref PlayerProgressionConfigBlob root = ref progressionConfig.Config.Value;
        float defaultPickupRadius = Mathf.Max(0f, root.ExperiencePickupRadius);
        string scalingFormula = root.ExperiencePickupRadiusScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(scalingFormula))
            return defaultPickupRadius;

        if (scalableStats == null || scalableStats.Count <= 0)
            return defaultPickupRadius;

        variableContext.Clear();
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(scalingFormula,
                                                                   Mathf.Max(0f, root.BaseExperiencePickupRadius),
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return defaultPickupRadius;
        }

        return Mathf.Max(0f, evaluatedValue);
    }

    /// <summary>
    /// Updates the runtime collection component only when the resolved pickup radius actually changed.
    /// </summary>
    /// <param name="experienceCollection">Mutable runtime collection component.</param>
    /// <param name="progressionConfig">Runtime progression config containing pickup radius metadata.</param>
    /// <param name="scalableStats">Current scalable-stat buffer used as formula variables.</param>
    /// <returns>Void.</returns>
    public static void SyncRuntimeComponent(ref PlayerExperienceCollection experienceCollection,
                                            PlayerProgressionConfig progressionConfig,
                                            DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        float resolvedPickupRadius = ResolveCurrentPickupRadius(progressionConfig,
                                                                scalableStats,
                                                                experienceCollection.PickupRadius);

        if (Mathf.Abs(experienceCollection.PickupRadius - resolvedPickupRadius) <= ComparisonEpsilon)
            return;

        experienceCollection = new PlayerExperienceCollection
        {
            PickupRadius = resolvedPickupRadius
        };
    }

    /// <summary>
    /// Updates the runtime collection component only when the resolved pickup radius actually changed.
    /// </summary>
    /// <param name="experienceCollection">Mutable runtime collection component.</param>
    /// <param name="progressionConfig">Runtime progression config containing pickup radius metadata.</param>
    /// <param name="scalableStats">Managed scalable-stat list used as formula variables.</param>
    /// <returns>Void.</returns>
    public static void SyncRuntimeComponent(ref PlayerExperienceCollection experienceCollection,
                                            PlayerProgressionConfig progressionConfig,
                                            IReadOnlyList<PlayerScalableStatElement> scalableStats)
    {
        float resolvedPickupRadius = ResolveCurrentPickupRadius(progressionConfig,
                                                                scalableStats,
                                                                experienceCollection.PickupRadius);

        if (Mathf.Abs(experienceCollection.PickupRadius - resolvedPickupRadius) <= ComparisonEpsilon)
            return;

        experienceCollection = new PlayerExperienceCollection
        {
            PickupRadius = resolvedPickupRadius
        };
    }
    #endregion

    #endregion
}
