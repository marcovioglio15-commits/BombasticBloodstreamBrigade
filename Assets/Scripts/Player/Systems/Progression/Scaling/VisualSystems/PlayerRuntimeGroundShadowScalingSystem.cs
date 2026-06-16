using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds scalable player ground-shadow settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeGroundShadowScalingSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable player ground-shadow settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerGroundShadowScalingState>();
        state.RequireForUpdate<PlayerBaseGroundShadowConfig>();
        state.RequireForUpdate<PlayerGroundShadowConfig>();
        state.RequireForUpdate<PlayerRuntimeGroundShadowScalingElement>();
    }

    /// <summary>
    /// Restores the immutable baseline and applies all player ground-shadow formulas when scalable stats change.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeGroundShadowScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeGroundShadowScalingElement>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerRuntimeScalingState> runtimeScalingState,
                  RefRW<PlayerGroundShadowScalingState> groundShadowScalingState,
                  RefRO<PlayerBaseGroundShadowConfig> baseConfig,
                  RefRW<PlayerGroundShadowConfig> runtimeConfig,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRW<PlayerGroundShadowScalingState>,
                                    RefRO<PlayerBaseGroundShadowConfig>,
                                    RefRW<PlayerGroundShadowConfig>>()
                             .WithAll<PlayerRuntimeGroundShadowScalingElement>()
                             .WithEntityAccess())
        {
            if (runtimeScalingState.ValueRO.Initialized == 0)
                continue;

            if (groundShadowScalingState.ValueRO.Initialized != 0 &&
                groundShadowScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.ValueRO.LastScalableStatsHash)
            {
                continue;
            }

            runtimeConfig.ValueRW = baseConfig.ValueRO.Config;
            PlayerRuntimeScalingFormulaContextUtility.Fill(entity,
                                                           in scalableStatsLookup,
                                                           in comboConfigLookup,
                                                           in comboStateLookup,
                                                           in comboRanksLookup,
                                                           in characterTuningLookup,
                                                           EffectiveScalableStats,
                                                           VariableContext);
            ApplyScaling(scalingLookup[entity], ref runtimeConfig.ValueRW);
            Sanitize(ref runtimeConfig.ValueRW);
            groundShadowScalingState.ValueRW.Initialized = 1;
            groundShadowScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.ValueRO.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Applies all ground-shadow formulas to a freshly restored runtime configuration.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime player ground-shadow configuration.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeGroundShadowScalingElement> scalingBuffer,
                                     ref PlayerGroundShadowConfig runtimeConfig)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeGroundShadowScalingElement scalingElement = scalingBuffer[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          VariableContext,
                                                                                          out bool resolvedBoolean))
                    ApplyBooleanValue(scalingElement.PayloadPath.ToString(), resolvedBoolean, ref runtimeConfig);

                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      VariableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            ApplyNumericValue(scalingElement.PayloadPath.ToString(), resolvedValue, ref runtimeConfig);
        }
    }

    /// <summary>
    /// Applies one boolean formula result to the player ground-shadow toggle.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to groundShadow.</param>
    /// <param name="resolvedValue">Resolved boolean result.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerGroundShadowConfig runtimeConfig)
    {
        if (payloadPath == "enabled")
            runtimeConfig.Enabled = resolvedValue ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Applies one numeric formula result to player ground-shadow geometry, projection or color fields.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to groundShadow.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyNumericValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerGroundShadowConfig runtimeConfig)
    {
        switch (payloadPath)
        {
            case "hitAreaMultiplier":
                runtimeConfig.HitAreaMultiplier = resolvedValue;
                break;
            case "positionOffsetXZ.x":
                runtimeConfig.PositionOffsetXZ.x = resolvedValue;
                break;
            case "positionOffsetXZ.y":
                runtimeConfig.PositionOffsetXZ.y = resolvedValue;
                break;
            case "heightOffset":
                runtimeConfig.HeightOffset = resolvedValue;
                break;
            case "projectionMode":
                runtimeConfig.ProjectionMode = (GroundShadowProjectionMode)math.clamp((int)math.round(resolvedValue),
                                                                                      (int)GroundShadowProjectionMode.RaisedQuad,
                                                                                      (int)GroundShadowProjectionMode.ProjectOntoGround);
                break;
            case "projectionMaxDistance":
                runtimeConfig.ProjectionMaxDistance = resolvedValue;
                break;
            case "shadowColor.r":
                runtimeConfig.ShadowColor.x = resolvedValue;
                break;
            case "shadowColor.g":
                runtimeConfig.ShadowColor.y = resolvedValue;
                break;
            case "shadowColor.b":
                runtimeConfig.ShadowColor.z = resolvedValue;
                break;
            case "shadowColor.a":
                runtimeConfig.ShadowColor.w = resolvedValue;
                break;
            case "shadowAlpha":
                runtimeConfig.ShadowAlpha = resolvedValue;
                break;
            case "shadowEdgeSoftness":
                runtimeConfig.ShadowEdgeSoftness = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Clamps runtime-only player ground-shadow copies to safe presentation values.
    /// </summary>
    /// <param name="config">Mutable runtime config.</param>
    private static void Sanitize(ref PlayerGroundShadowConfig config)
    {
        config.HitAreaMultiplier = math.max(PlayerHitAreaUtility.MinimumShadowMultiplier, config.HitAreaMultiplier);
        config.ProjectionMaxDistance = math.max(0f, config.ProjectionMaxDistance);
        config.ShadowAlpha = math.saturate(config.ShadowAlpha);
        config.ShadowEdgeSoftness = math.saturate(config.ShadowEdgeSoftness);
    }
    #endregion

    #endregion
}
