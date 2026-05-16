using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves runtime values for dropped power-up container interactions.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerPowerUpContainerInteractionRuntimeUtility
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext = new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current post-swap interaction cooldown from baked config and optional Add Scaling metadata.
    /// /params interactionConfig Runtime container interaction config baked on the player entity.
    /// /params scalableStats Current runtime scalable-stat buffer used by formula variables.
    /// /returns Non-negative cooldown duration in seconds.
    /// </summary>
    public static float ResolveInteractionLockDuration(in PlayerPowerUpContainerInteractionConfig interactionConfig,
                                                       DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        float defaultDuration = math.max(0f, interactionConfig.InteractionLockDuration);
        string formula = interactionConfig.InteractionLockDurationScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(formula))
            return defaultDuration;

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
            return defaultDuration;

        // Build the same typed variable context used by other runtime scaling formulas.
        variableContext.Clear();
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(formula,
                                                                                  math.max(0f, interactionConfig.BaseInteractionLockDuration),
                                                                                  false,
                                                                                  variableContext,
                                                                                  out float resolvedDuration))
        {
            return defaultDuration;
        }

        return math.max(0f, resolvedDuration);
    }

    /// <summary>
    /// Resolves the current post-swap interaction cooldown directly from a player entity.
    /// /params entityManager Entity manager used to read runtime player config and scalable stats.
    /// /params playerEntity Player entity that owns the dropped-container interaction config.
    /// /returns Non-negative cooldown duration in seconds.
    /// </summary>
    public static float ResolveInteractionLockDuration(EntityManager entityManager, Entity playerEntity)
    {
        if (playerEntity == Entity.Null)
            return 0f;

        if (!entityManager.Exists(playerEntity))
            return 0f;

        if (!entityManager.HasComponent<PlayerPowerUpContainerInteractionConfig>(playerEntity))
            return 0f;

        DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.HasBuffer<PlayerScalableStatElement>(playerEntity)
            ? entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity)
            : default;
        PlayerPowerUpContainerInteractionConfig interactionConfig = entityManager.GetComponentData<PlayerPowerUpContainerInteractionConfig>(playerEntity);
        return ResolveInteractionLockDuration(in interactionConfig, scalableStats);
    }
    #endregion

    #endregion
}
