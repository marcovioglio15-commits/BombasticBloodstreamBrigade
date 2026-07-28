using System.Collections.Generic;
using System.Text;
using Unity.Collections;
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
    private const string RuntimeReferencePath = "runtimeReference";
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
        state.RequireForUpdate<PlayerVisualRuntimeDataOwner>();
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
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(true);
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRoomRewardTemporaryModifierElement> temporaryModifiersLookup = SystemAPI.GetBufferLookup<PlayerRoomRewardTemporaryModifierElement>(true);
        ComponentLookup<PlayerRoomRewardTemporaryState> temporaryStateLookup = SystemAPI.GetComponentLookup<PlayerRoomRewardTemporaryState>(true);
        BufferLookup<PlayerRuntimeJetpackVfxScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeJetpackVfxScalingElement>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerVisualRuntimeDataOwner> visualRuntimeOwner,
                  RefRW<PlayerJetpackVfxScalingState> vfxScalingState,
                  RefRO<PlayerBaseJetpackVfxConfig> baseConfig,
                  RefRW<PlayerJetpackVfxConfig> runtimeConfig,
                  Entity visualRuntimeEntity)
                 in SystemAPI.Query<RefRO<PlayerVisualRuntimeDataOwner>,
                                    RefRW<PlayerJetpackVfxScalingState>,
                                    RefRO<PlayerBaseJetpackVfxConfig>,
                                    RefRW<PlayerJetpackVfxConfig>>()
                             .WithAll<PlayerRuntimeJetpackVfxScalingElement>()
                             .WithEntityAccess())
        {
            Entity playerEntity = visualRuntimeOwner.ValueRO.PlayerEntity;

            if (!runtimeScalingStateLookup.TryGetComponent(playerEntity, out PlayerRuntimeScalingState runtimeScalingState) ||
                runtimeScalingState.Initialized == 0)
                continue;

            if (vfxScalingState.ValueRO.Initialized != 0 &&
                vfxScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.LastScalableStatsHash)
                continue;

            runtimeConfig.ValueRW = baseConfig.ValueRO.Config;
            PlayerRuntimeScalingFormulaContextUtility.Fill(playerEntity,
                                                            in scalableStatsLookup,
                                                            in temporaryModifiersLookup,
                                                            in temporaryStateLookup,
                                                            in comboConfigLookup,
                                                           in comboStateLookup,
                                                           in comboRanksLookup,
                                                           in characterTuningLookup,
                                                           EffectiveScalableStats,
                                                           VariableContext);
            ApplyScaling(scalingLookup[visualRuntimeEntity], ref runtimeConfig.ValueRW);
            vfxScalingState.ValueRW.Initialized = 1;
            vfxScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.LastScalableStatsHash;
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

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Token)
            {
                if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                        scalingElement.BaseTokenValue.ToString(),
                                                                                        VariableContext,
                                                                                        out string resolvedToken))
                    ApplyTokenValue(scalingElement.PayloadPath.ToString(), resolvedToken, ref runtimeConfig);

                continue;
            }

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
    /// Applies one boolean formula result to a Jetpack VFX behavior toggle.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to playerJetpackVfx.</param>
    /// <param name="resolvedValue">Resolved boolean result.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerJetpackVfxConfig runtimeConfig)
    {
        if (string.Equals(payloadPath, "scaleWithMovementSpeed", System.StringComparison.Ordinal))
            runtimeConfig.ScaleWithMovementSpeed = resolvedValue ? (byte)1 : (byte)0;
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
            case "movementSpeedThreshold":
                runtimeConfig.MovementSpeedThreshold = resolvedValue;
                break;
            case "rotationSpeedThresholdDegrees":
                runtimeConfig.RotationSpeedThresholdDegrees = resolvedValue;
                break;
            case "speedForMaximumScale":
                runtimeConfig.SpeedForMaximumScale = resolvedValue;
                break;
            case "normalScaleSpeedPercent":
                runtimeConfig.NormalScaleSpeedPercent = resolvedValue;
                break;
            case "scaleVariationPercent":
                runtimeConfig.ScaleVariationPercent = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Applies one token formula result to the prefab-relative Visual Player reference.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to playerJetpackVfx.</param>
    /// <param name="resolvedToken">Resolved token formula output.</param>
    /// <param name="runtimeConfig">Mutable runtime config.</param>
    private static void ApplyTokenValue(string payloadPath,
                                        string resolvedToken,
                                        ref PlayerJetpackVfxConfig runtimeConfig)
    {
        if (!string.Equals(payloadPath, RuntimeReferencePath, System.StringComparison.Ordinal))
            return;

        string normalizedToken = string.IsNullOrWhiteSpace(resolvedToken) ? string.Empty : resolvedToken.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedToken) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            return;

        runtimeConfig.RuntimeReference = new FixedString128Bytes(normalizedToken);
    }
    #endregion

    #endregion
}
