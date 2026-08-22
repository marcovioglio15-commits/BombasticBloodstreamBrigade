using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Resolves runtime values for dropped power-up container interactions.
/// </summary>
internal static class PlayerPowerUpContainerInteractionRuntimeUtility
{
    #region Fields
    private static readonly List<PlayerScalableStatElement> effectiveScalableStats =
        new List<PlayerScalableStatElement>(64);
    private static readonly Dictionary<string, PlayerFormulaValue> variableContext =
        new Dictionary<string, PlayerFormulaValue>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current post-swap interaction cooldown from baked config and optional Add Scaling metadata.
    /// </summary>
    /// <param name="interactionConfig">Runtime container interaction config baked on the player entity.</param>
    /// <param name="formulaContext">Effective scalable-stat context used by the unified formula.</param>
    /// <returns>Non-negative cooldown duration in seconds.</returns>
    public static float ResolveInteractionLockDuration(in PlayerPowerUpContainerInteractionConfig interactionConfig,
                                                       IReadOnlyDictionary<string, PlayerFormulaValue> formulaContext)
    {
        float defaultDuration = math.max(0f, interactionConfig.InteractionLockDuration);
        string formula = interactionConfig.InteractionLockDurationScalingFormula.ToString();

        if (string.IsNullOrWhiteSpace(formula))
            return defaultDuration;

        if (formulaContext == null || formulaContext.Count <= 0)
            return defaultDuration;

        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(formula,
                                                                                  math.max(0f, interactionConfig.BaseInteractionLockDuration),
                                                                                  false,
                                                                                  formulaContext,
                                                                                  out float resolvedDuration))
        {
            return defaultDuration;
        }

        return math.max(0f, resolvedDuration);
    }

    /// <summary>
    /// Resolves the current post-swap interaction cooldown directly from a player entity.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read runtime player config and scalable stats.</param>
    /// <param name="playerEntity">Player entity that owns the dropped-container interaction config.</param>
    /// <returns>Non-negative cooldown duration in seconds.</returns>
    public static float ResolveInteractionLockDuration(EntityManager entityManager, Entity playerEntity)
    {
        if (playerEntity == Entity.Null)
            return 0f;

        if (!entityManager.Exists(playerEntity))
            return 0f;

        if (!entityManager.HasComponent<PlayerPowerUpContainerInteractionConfig>(playerEntity))
            return 0f;

        PlayerPowerUpContainerInteractionConfig interactionConfig = entityManager.GetComponentData<PlayerPowerUpContainerInteractionConfig>(playerEntity);
        PlayerRuntimeScalingFormulaContextUtility.Fill(entityManager,
                                                        playerEntity,
                                                        effectiveScalableStats,
                                                        variableContext);
        return ResolveInteractionLockDuration(in interactionConfig, variableContext);
    }
    #endregion

    #endregion
}
