using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds Jetpack VFX settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeJetpackVfxScalingSystem : ISystem
{
    #region Constants
    private const float MinimumScale = 0.01f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, System.StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable Jetpack VFX settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerJetpackVfxScalingState>();
        state.RequireForUpdate<PlayerBaseJetpackVfxConfig>();
        state.RequireForUpdate<PlayerJetpackVfxConfig>();
        state.RequireForUpdate<PlayerRuntimeJetpackVfxScalingElement>();
    }

    /// <summary>
    /// Rebuilds Jetpack VFX runtime settings when the shared scalable-stat hash changes.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeJetpackVfxScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeJetpackVfxScalingElement>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerRuntimeScalingState> runtimeScalingState,
                  RefRW<PlayerJetpackVfxScalingState> vfxScalingState,
                  RefRO<PlayerBaseJetpackVfxConfig> baseConfig,
                  RefRW<PlayerJetpackVfxConfig> runtimeConfig,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeScalingState>,
                                    RefRW<PlayerJetpackVfxScalingState>,
                                    RefRO<PlayerBaseJetpackVfxConfig>,
                                    RefRW<PlayerJetpackVfxConfig>>()
                             .WithAll<PlayerRuntimeJetpackVfxScalingElement>()
                             .WithEntityAccess())
        {
            if (runtimeScalingState.ValueRO.Initialized == 0)
                continue;

            if (vfxScalingState.ValueRO.Initialized != 0 &&
                vfxScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.ValueRO.LastScalableStatsHash)
                continue;

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
            vfxScalingState.ValueRW.Initialized = 1;
            vfxScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.ValueRO.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Applies all Jetpack VFX formulas to a freshly restored runtime config.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime Jetpack VFX config.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeJetpackVfxScalingElement> scalingBuffer,
                                     ref PlayerJetpackVfxConfig runtimeConfig)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeJetpackVfxScalingElement scalingElement = scalingBuffer[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
                continue;

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
    /// Applies one numeric formula result to a Jetpack VFX behavior field.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to playerJetpackVfx.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyNumericValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerJetpackVfxConfig runtimeConfig)
    {
        switch (payloadPath)
        {
            case "activationMode":
                runtimeConfig.ActivationMode = (PlayerJetpackVfxActivationMode)math.clamp((int)math.round(resolvedValue),
                                                                                         (int)PlayerJetpackVfxActivationMode.Always,
                                                                                         (int)PlayerJetpackVfxActivationMode.WhileMovingOrRotating);
                break;
            case "spawnOffset.x":
                runtimeConfig.SpawnOffset.x = resolvedValue;
                break;
            case "spawnOffset.y":
                runtimeConfig.SpawnOffset.y = resolvedValue;
                break;
            case "spawnOffset.z":
                runtimeConfig.SpawnOffset.z = resolvedValue;
                break;
            case "scaleMultiplier":
                runtimeConfig.UniformScale = resolvedValue;
                break;
            case "movementSpeedThreshold":
                runtimeConfig.MovementSpeedThreshold = resolvedValue;
                break;
            case "rotationSpeedThresholdDegrees":
                runtimeConfig.RotationSpeedThresholdDegrees = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Clamps the runtime-only Jetpack VFX copy to safe presentation values.
    /// </summary>
    /// <param name="config">Mutable runtime config.</param>
    private static void Sanitize(ref PlayerJetpackVfxConfig config)
    {
        config.UniformScale = math.max(MinimumScale, config.UniformScale);
        config.MovementSpeedThreshold = math.max(0f, config.MovementSpeedThreshold);
        config.RotationSpeedThresholdDegrees = math.max(0f, config.RotationSpeedThresholdDegrees);
    }
    #endregion

    #endregion
}
