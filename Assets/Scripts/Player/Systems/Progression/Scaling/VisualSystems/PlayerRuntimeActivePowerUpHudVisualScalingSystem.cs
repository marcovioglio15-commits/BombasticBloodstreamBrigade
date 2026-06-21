using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds scalable active power-up HUD visual settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeActivePowerUpHudVisualScalingSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable active power-up HUD visual settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerActivePowerUpHudVisualOwner>();
        state.RequireForUpdate<PlayerActivePowerUpHudVisualScalingState>();
        state.RequireForUpdate<PlayerBaseActivePowerUpHudVisualConfig>();
        state.RequireForUpdate<PlayerActivePowerUpHudVisualConfig>();
        state.RequireForUpdate<PlayerRuntimeActivePowerUpHudVisualScalingElement>();
    }

    /// <summary>
    /// Restores the immutable baseline and applies all active-HUD visual formulas when scalable stats change.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeActivePowerUpHudVisualScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeActivePowerUpHudVisualScalingElement>(true);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerActivePowerUpHudVisualOwner> owner,
                  RefRW<PlayerActivePowerUpHudVisualScalingState> visualScalingState,
                  RefRO<PlayerBaseActivePowerUpHudVisualConfig> baseConfig,
                  RefRW<PlayerActivePowerUpHudVisualConfig> runtimeConfig,
                  Entity configEntity)
                 in SystemAPI.Query<RefRO<PlayerActivePowerUpHudVisualOwner>,
                                    RefRW<PlayerActivePowerUpHudVisualScalingState>,
                                    RefRO<PlayerBaseActivePowerUpHudVisualConfig>,
                                    RefRW<PlayerActivePowerUpHudVisualConfig>>()
                             .WithAll<PlayerRuntimeActivePowerUpHudVisualScalingElement>()
                             .WithEntityAccess())
        {
            Entity playerEntity = owner.ValueRO.PlayerEntity;

            if (!runtimeScalingStateLookup.HasComponent(playerEntity))
                continue;

            PlayerRuntimeScalingState runtimeScalingState = runtimeScalingStateLookup[playerEntity];

            if (runtimeScalingState.Initialized == 0)
                continue;

            if (visualScalingState.ValueRO.Initialized != 0 &&
                visualScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.LastScalableStatsHash)
            {
                continue;
            }

            runtimeConfig.ValueRW = baseConfig.ValueRO.Config;
            PlayerRuntimeScalingFormulaContextUtility.Fill(playerEntity,
                                                           in scalableStatsLookup,
                                                           in comboConfigLookup,
                                                           in comboStateLookup,
                                                           in comboRanksLookup,
                                                           in characterTuningLookup,
                                                           EffectiveScalableStats,
                                                           VariableContext);
            ApplyScaling(scalingLookup[configEntity], ref runtimeConfig.ValueRW);
            visualScalingState.ValueRW.Initialized = 1;
            visualScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Applies all active power-up HUD formulas to a freshly restored runtime configuration.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime active power-up HUD visual configuration.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeActivePowerUpHudVisualScalingElement> scalingBuffer,
                                     ref PlayerActivePowerUpHudVisualConfig runtimeConfig)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeActivePowerUpHudVisualScalingElement scalingElement = scalingBuffer[scalingIndex];
            string payloadPath = scalingElement.PayloadPath.ToString();

            if (payloadPath.StartsWith("energySyringe.", StringComparison.Ordinal))
            {
                PlayerRuntimeHealthBarVisualScalingElement syringeElement = ToSyringeScalingElement(scalingElement,
                                                                                                     payloadPath.Substring("energySyringe.".Length));
                PlayerRuntimeHealthBarVisualScalingSystem.ApplyScalingElement(syringeElement,
                                                                               ref runtimeConfig.EnergySyringe,
                                                                               VariableContext);
                continue;
            }

            switch ((PlayerFormulaValueType)scalingElement.ValueType)
            {
                case PlayerFormulaValueType.Boolean:
                    ApplyBooleanScaling(scalingElement, payloadPath, ref runtimeConfig);
                    break;
                case PlayerFormulaValueType.Number:
                    ApplyNumericScaling(scalingElement, payloadPath, ref runtimeConfig);
                    break;
            }
        }
    }

    /// <summary>
    /// Applies one boolean formula result to an active power-up HUD visual field.
    /// </summary>
    /// <param name="scalingElement">Runtime scaling metadata element.</param>
    /// <param name="payloadPath">Target field path relative to activePowerUpHud.</param>
    /// <param name="runtimeConfig">Mutable runtime active power-up HUD visual configuration.</param>
    private static void ApplyBooleanScaling(PlayerRuntimeActivePowerUpHudVisualScalingElement scalingElement,
                                            string payloadPath,
                                            ref PlayerActivePowerUpHudVisualConfig runtimeConfig)
    {
        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                  scalingElement.BaseBooleanValue != 0,
                                                                                  VariableContext,
                                                                                  out bool resolvedBoolean))
        {
            return;
        }

        byte byteValue = resolvedBoolean ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "enabled":
                runtimeConfig.Enabled = byteValue;
                return;
            case "hideWhenPlayerMissing":
                runtimeConfig.HideWhenPlayerMissing = byteValue;
                return;
            case "hideEnergyWhenModuleMissing":
                runtimeConfig.HideEnergyWhenModuleMissing = byteValue;
                return;
            case "hideChargeWhenModuleMissing":
                runtimeConfig.HideChargeWhenModuleMissing = byteValue;
                return;
            case "requirementMarker.enabled":
                runtimeConfig.RequirementMarker.Enabled = byteValue;
                return;
            case "chargeRing.enabled":
                runtimeConfig.ChargeRing.Enabled = byteValue;
                return;
            case "iconCooldown.enabled":
                runtimeConfig.IconCooldown.Enabled = byteValue;
                return;
        }
    }

    /// <summary>
    /// Applies one numeric or enum-like formula result to an active power-up HUD visual field.
    /// </summary>
    /// <param name="scalingElement">Runtime scaling metadata element.</param>
    /// <param name="payloadPath">Target field path relative to activePowerUpHud.</param>
    /// <param name="runtimeConfig">Mutable runtime active power-up HUD visual configuration.</param>
    private static void ApplyNumericScaling(PlayerRuntimeActivePowerUpHudVisualScalingElement scalingElement,
                                            string payloadPath,
                                            ref PlayerActivePowerUpHudVisualConfig runtimeConfig)
    {
        if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                  scalingElement.BaseValue,
                                                                                  scalingElement.IsInteger != 0,
                                                                                  VariableContext,
                                                                                  out float resolvedValue))
        {
            return;
        }

        if (TryApplyColorChannel(payloadPath, resolvedValue, ref runtimeConfig))
            return;

        switch (payloadPath)
        {
            case "chargeSmoothingSeconds":
                runtimeConfig.ChargeSmoothingSeconds = resolvedValue;
                return;
            case "requirementMarker.width":
                runtimeConfig.RequirementMarker.Width = resolvedValue;
                return;
            case "requirementMarker.height":
                runtimeConfig.RequirementMarker.Height = resolvedValue;
                return;
            case "requirementMarker.verticalOffset":
                runtimeConfig.RequirementMarker.VerticalOffset = resolvedValue;
                return;
            case "chargeRing.thickness":
                runtimeConfig.ChargeRing.Thickness = resolvedValue;
                return;
            case "chargeRing.outlineThickness":
                runtimeConfig.ChargeRing.OutlineThickness = resolvedValue;
                return;
            case "chargeRing.startAngleDegrees":
                runtimeConfig.ChargeRing.StartAngleDegrees = resolvedValue;
                return;
            case "chargeRing.arcDegrees":
                runtimeConfig.ChargeRing.ArcDegrees = resolvedValue;
                return;
            case "iconCooldown.desaturationStrength":
                runtimeConfig.IconCooldown.DesaturationStrength = resolvedValue;
                return;
            case "iconCooldown.revealFeather":
                runtimeConfig.IconCooldown.RevealFeather = resolvedValue;
                return;
            case "iconCooldown.fillDirection":
                runtimeConfig.IconCooldown.FillDirection = (PlayerPowerUpIconCooldownFillDirection)math.clamp((int)math.round(resolvedValue),
                                                                                                               (int)PlayerPowerUpIconCooldownFillDirection.BottomToTop,
                                                                                                               (int)PlayerPowerUpIconCooldownFillDirection.TopToBottom);
                return;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Converts one active-HUD scaling element into the syringe scaling element expected by the shared applicator.
    /// </summary>
    /// <param name="source">Source active-HUD scaling metadata.</param>
    /// <param name="syringePayloadPath">Path relative to the nested energy syringe root.</param>
    /// <returns>Equivalent syringe scaling metadata element.</returns>
    private static PlayerRuntimeHealthBarVisualScalingElement ToSyringeScalingElement(PlayerRuntimeActivePowerUpHudVisualScalingElement source,
                                                                                       string syringePayloadPath)
    {
        return new PlayerRuntimeHealthBarVisualScalingElement
        {
            PayloadPath = new FixedString128Bytes(syringePayloadPath),
            ValueType = source.ValueType,
            BaseValue = source.BaseValue,
            BaseBooleanValue = source.BaseBooleanValue,
            IsInteger = source.IsInteger,
            BaseTokenValue = source.BaseTokenValue,
            Formula = source.Formula
        };
    }

    /// <summary>
    /// Applies one numeric formula result to a direct active-HUD color channel.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to activePowerUpHud.</param>
    /// <param name="resolvedValue">Resolved numeric color-channel value.</param>
    /// <param name="runtimeConfig">Mutable runtime active power-up HUD visual configuration.</param>
    /// <returns>True when the path targeted a supported color channel.</returns>
    private static bool TryApplyColorChannel(string payloadPath,
                                             float resolvedValue,
                                             ref PlayerActivePowerUpHudVisualConfig runtimeConfig)
    {
        int channelSeparatorIndex = payloadPath.LastIndexOf('.');

        if (channelSeparatorIndex <= 0 || channelSeparatorIndex >= payloadPath.Length - 1)
            return false;

        char channelName = payloadPath[channelSeparatorIndex + 1];
        string colorPath = payloadPath.Substring(0, channelSeparatorIndex);

        switch (colorPath)
        {
            case "requirementMarker.color":
                return TryWriteColorChannel(channelName, resolvedValue, ref runtimeConfig.RequirementMarker.Color);
            case "chargeRing.backgroundColor":
                return TryWriteColorChannel(channelName, resolvedValue, ref runtimeConfig.ChargeRing.BackgroundColor);
            case "chargeRing.fillColor":
                return TryWriteColorChannel(channelName, resolvedValue, ref runtimeConfig.ChargeRing.FillColor);
            case "chargeRing.outlineColor":
                return TryWriteColorChannel(channelName, resolvedValue, ref runtimeConfig.ChargeRing.OutlineColor);
            case "iconCooldown.lockedTint":
                return TryWriteColorChannel(channelName, resolvedValue, ref runtimeConfig.IconCooldown.LockedTint);
            default:
                return false;
        }
    }

    /// <summary>
    /// Writes one resolved formula value into an unmanaged RGBA channel.
    /// </summary>
    /// <param name="channelName">Serialized channel name.</param>
    /// <param name="resolvedValue">Resolved numeric channel value.</param>
    /// <param name="color">Mutable unmanaged color.</param>
    /// <returns>True when the channel name is supported.</returns>
    private static bool TryWriteColorChannel(char channelName, float resolvedValue, ref float4 color)
    {
        switch (channelName)
        {
            case 'r':
                color.x = resolvedValue;
                return true;
            case 'g':
                color.y = resolvedValue;
                return true;
            case 'b':
                color.z = resolvedValue;
                return true;
            case 'a':
                color.w = resolvedValue;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
