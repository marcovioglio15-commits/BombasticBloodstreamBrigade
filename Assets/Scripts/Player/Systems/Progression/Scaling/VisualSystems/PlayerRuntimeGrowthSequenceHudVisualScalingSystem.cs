using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds scalable player HUD growth-sequence settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeGrowthSequenceHudVisualScalingSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable growth-sequence HUD settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerGrowthSequenceHudVisualOwner>();
        state.RequireForUpdate<PlayerGrowthSequenceHudVisualScalingState>();
        state.RequireForUpdate<PlayerBaseGrowthSequenceHudVisualConfig>();
        state.RequireForUpdate<PlayerGrowthSequenceHudVisualConfig>();
        state.RequireForUpdate<PlayerRuntimeGrowthSequenceHudVisualScalingElement>();
    }

    /// <summary>
    /// Restores growth-sequence HUD baselines and applies all formulas when scalable stats change.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeGrowthSequenceHudVisualScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeGrowthSequenceHudVisualScalingElement>(true);
        BufferLookup<PlayerBaseGrowthSequenceHudStepVisualElement> baseStepLookup = SystemAPI.GetBufferLookup<PlayerBaseGrowthSequenceHudStepVisualElement>(true);
        BufferLookup<PlayerGrowthSequenceHudStepVisualElement> stepLookup = SystemAPI.GetBufferLookup<PlayerGrowthSequenceHudStepVisualElement>(false);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerGrowthSequenceHudVisualOwner> owner,
                  RefRW<PlayerGrowthSequenceHudVisualScalingState> visualScalingState,
                  RefRO<PlayerBaseGrowthSequenceHudVisualConfig> baseConfig,
                  RefRW<PlayerGrowthSequenceHudVisualConfig> runtimeConfig,
                  Entity configEntity)
                 in SystemAPI.Query<RefRO<PlayerGrowthSequenceHudVisualOwner>,
                                    RefRW<PlayerGrowthSequenceHudVisualScalingState>,
                                    RefRO<PlayerBaseGrowthSequenceHudVisualConfig>,
                                    RefRW<PlayerGrowthSequenceHudVisualConfig>>()
                             .WithAll<PlayerRuntimeGrowthSequenceHudVisualScalingElement>()
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
            CopyBaseSteps(baseStepLookup[configEntity], stepLookup[configEntity]);
            PlayerRuntimeScalingFormulaContextUtility.Fill(playerEntity,
                                                           in scalableStatsLookup,
                                                           in comboConfigLookup,
                                                           in comboStateLookup,
                                                           in comboRanksLookup,
                                                           in characterTuningLookup,
                                                           EffectiveScalableStats,
                                                           VariableContext);
            ApplyScaling(scalingLookup[configEntity],
                         ref runtimeConfig.ValueRW,
                         stepLookup[configEntity]);
            visualScalingState.ValueRW.Initialized = 1;
            visualScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Copies immutable growth step baselines into the mutable runtime buffer.
    /// </summary>
    /// <param name="baseSteps">Immutable baseline growth steps.</param>
    /// <param name="runtimeSteps">Mutable runtime growth steps.</param>
    private static void CopyBaseSteps(DynamicBuffer<PlayerBaseGrowthSequenceHudStepVisualElement> baseSteps,
                                      DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> runtimeSteps)
    {
        runtimeSteps.Clear();

        for (int stepIndex = 0; stepIndex < baseSteps.Length; stepIndex++)
        {
            PlayerBaseGrowthSequenceHudStepVisualElement step = baseSteps[stepIndex];
            runtimeSteps.Add(new PlayerGrowthSequenceHudStepVisualElement
            {
                ScheduleId = step.ScheduleId,
                StepIndex = step.StepIndex,
                StatName = step.StatName,
                Text = step.Text,
                PresentationMode = step.PresentationMode,
                NextSprite = step.NextSprite,
                NormalSprite = step.NormalSprite,
                NextFontAsset = step.NextFontAsset,
                NormalFontAsset = step.NormalFontAsset,
                NextFontSize = step.NextFontSize,
                NormalFontSize = step.NormalFontSize,
                NextColor = step.NextColor,
                NormalColor = step.NormalColor,
                NextOutlineColor = step.NextOutlineColor,
                NormalOutlineColor = step.NormalOutlineColor,
                NextOutlineWidth = step.NextOutlineWidth,
                NormalOutlineWidth = step.NormalOutlineWidth
            });
        }
    }

    /// <summary>
    /// Applies all growth-sequence HUD formulas to freshly restored runtime settings.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable growth-sequence HUD visual configuration.</param>
    /// <param name="runtimeSteps">Mutable runtime growth step buffer.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimeGrowthSequenceHudVisualScalingElement> scalingBuffer,
                                     ref PlayerGrowthSequenceHudVisualConfig runtimeConfig,
                                     DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> runtimeSteps)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeGrowthSequenceHudVisualScalingElement scalingElement = scalingBuffer[scalingIndex];
            string payloadPath = scalingElement.PayloadPath.ToString();

            switch ((PlayerFormulaValueType)scalingElement.ValueType)
            {
                case PlayerFormulaValueType.Boolean:
                    if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                              scalingElement.BaseBooleanValue != 0,
                                                                                              VariableContext,
                                                                                              out bool resolvedBoolean))
                    {
                        ApplyBooleanValue(payloadPath, resolvedBoolean, ref runtimeConfig);
                    }
                    break;
                case PlayerFormulaValueType.Number:
                    if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                              scalingElement.BaseValue,
                                                                                              scalingElement.IsInteger != 0,
                                                                                              VariableContext,
                                                                                              out float resolvedNumber))
                    {
                        ApplyNumericValue(payloadPath, resolvedNumber, ref runtimeConfig, runtimeSteps);
                    }
                    break;
                case PlayerFormulaValueType.Token:
                    if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateTokenValue(scalingElement.Formula.ToString(),
                                                                                           scalingElement.BaseTokenValue.ToString(),
                                                                                           VariableContext,
                                                                                           out string resolvedToken))
                    {
                        ApplyTokenValue(payloadPath, resolvedToken, runtimeSteps);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Applies one boolean formula result to growth-sequence global config.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Growth Sequence.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="runtimeConfig">Mutable growth-sequence HUD visual configuration.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerGrowthSequenceHudVisualConfig runtimeConfig)
    {
        byte byteValue = resolvedValue ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "enabled":
                runtimeConfig.Enabled = byteValue;
                return;
            case "hideWhenPlayerMissing":
                runtimeConfig.HideWhenPlayerMissing = byteValue;
                return;
        }
    }

    /// <summary>
    /// Applies one numeric formula result to growth-sequence config or matching steps.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Growth Sequence.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="runtimeConfig">Mutable growth-sequence HUD visual configuration.</param>
    /// <param name="runtimeSteps">Mutable runtime growth step buffer.</param>
    private static void ApplyNumericValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerGrowthSequenceHudVisualConfig runtimeConfig,
                                          DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> runtimeSteps)
    {
        if (string.Equals(payloadPath, "maximumVisibleSteps", StringComparison.Ordinal))
        {
            runtimeConfig.MaximumVisibleSteps = math.max(0, (int)math.round(resolvedValue));
            return;
        }

        if (!TryResolveStepTarget(payloadPath, out FixedString64Bytes scheduleId, out int stepIndex, out string stepPath))
            return;

        for (int runtimeStepIndex = 0; runtimeStepIndex < runtimeSteps.Length; runtimeStepIndex++)
        {
            PlayerGrowthSequenceHudStepVisualElement step = runtimeSteps[runtimeStepIndex];

            if (!IsMatchingStep(step, scheduleId, stepIndex))
                continue;

            ApplyStepNumericValue(stepPath, resolvedValue, ref step);
            runtimeSteps[runtimeStepIndex] = step;
        }
    }

    /// <summary>
    /// Applies one token formula result to matching growth steps.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Growth Sequence.</param>
    /// <param name="resolvedToken">Formula token result.</param>
    /// <param name="runtimeSteps">Mutable runtime growth step buffer.</param>
    private static void ApplyTokenValue(string payloadPath,
                                        string resolvedToken,
                                        DynamicBuffer<PlayerGrowthSequenceHudStepVisualElement> runtimeSteps)
    {
        if (!TryResolveStepTarget(payloadPath, out FixedString64Bytes scheduleId, out int stepIndex, out string stepPath))
            return;

        if (!string.Equals(stepPath, "textOverride", StringComparison.Ordinal))
            return;

        for (int runtimeStepIndex = 0; runtimeStepIndex < runtimeSteps.Length; runtimeStepIndex++)
        {
            PlayerGrowthSequenceHudStepVisualElement step = runtimeSteps[runtimeStepIndex];

            if (!IsMatchingStep(step, scheduleId, stepIndex))
                continue;

            step.Text = new FixedString128Bytes(string.IsNullOrWhiteSpace(resolvedToken) ? string.Empty : resolvedToken.Trim());
            runtimeSteps[runtimeStepIndex] = step;
        }
    }

    /// <summary>
    /// Applies a numeric formula result to one growth step.
    /// </summary>
    /// <param name="stepPath">Target path relative to the step visual definition.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="step">Mutable growth step.</param>
    private static void ApplyStepNumericValue(string stepPath,
                                              float resolvedValue,
                                              ref PlayerGrowthSequenceHudStepVisualElement step)
    {
        switch (stepPath)
        {
            case "presentationMode":
                step.PresentationMode = (PlayerGrowthSequenceHudPresentationMode)math.clamp((int)math.round(resolvedValue), 0, 1);
                return;
            case "nextText.fontSize":
                step.NextFontSize = math.max(0f, resolvedValue);
                return;
            case "normalText.fontSize":
                step.NormalFontSize = math.max(0f, resolvedValue);
                return;
            case "nextText.outlineWidth":
                step.NextOutlineWidth = math.max(0f, resolvedValue);
                return;
            case "normalText.outlineWidth":
                step.NormalOutlineWidth = math.max(0f, resolvedValue);
                return;
        }

        ApplyStepColorValue(stepPath, resolvedValue, ref step);
    }

    /// <summary>
    /// Applies one color-channel formula result to one growth step.
    /// </summary>
    /// <param name="stepPath">Target path relative to the step visual definition.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="step">Mutable growth step.</param>
    private static void ApplyStepColorValue(string stepPath,
                                            float resolvedValue,
                                            ref PlayerGrowthSequenceHudStepVisualElement step)
    {
        float colorValue = math.saturate(resolvedValue);

        switch (stepPath)
        {
            case "nextText.color.r":
                step.NextColor.x = colorValue;
                return;
            case "nextText.color.g":
                step.NextColor.y = colorValue;
                return;
            case "nextText.color.b":
                step.NextColor.z = colorValue;
                return;
            case "nextText.color.a":
                step.NextColor.w = colorValue;
                return;
            case "normalText.color.r":
                step.NormalColor.x = colorValue;
                return;
            case "normalText.color.g":
                step.NormalColor.y = colorValue;
                return;
            case "normalText.color.b":
                step.NormalColor.z = colorValue;
                return;
            case "normalText.color.a":
                step.NormalColor.w = colorValue;
                return;
            case "nextText.outlineColor.r":
                step.NextOutlineColor.x = colorValue;
                return;
            case "nextText.outlineColor.g":
                step.NextOutlineColor.y = colorValue;
                return;
            case "nextText.outlineColor.b":
                step.NextOutlineColor.z = colorValue;
                return;
            case "nextText.outlineColor.a":
                step.NextOutlineColor.w = colorValue;
                return;
            case "normalText.outlineColor.r":
                step.NormalOutlineColor.x = colorValue;
                return;
            case "normalText.outlineColor.g":
                step.NormalOutlineColor.y = colorValue;
                return;
            case "normalText.outlineColor.b":
                step.NormalOutlineColor.z = colorValue;
                return;
            case "normalText.outlineColor.a":
                step.NormalOutlineColor.w = colorValue;
                return;
        }
    }

    /// <summary>
    /// Resolves a growth-sequence payload path into schedule, step index, and step-local path.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Growth Sequence.</param>
    /// <param name="scheduleId">Resolved optional schedule ID.</param>
    /// <param name="stepIndex">Resolved optional step index.</param>
    /// <param name="stepPath">Resolved path relative to the step visual definition.</param>
    /// <returns>True when the path targets a step definition.</returns>
    private static bool TryResolveStepTarget(string payloadPath,
                                             out FixedString64Bytes scheduleId,
                                             out int stepIndex,
                                             out string stepPath)
    {
        scheduleId = default;
        stepIndex = -1;
        stepPath = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadPath) ||
            !payloadPath.StartsWith("schedules.", StringComparison.Ordinal))
        {
            return false;
        }

        if (TryExtractStableToken(payloadPath, "scheduleId", out string scheduleIdText))
            scheduleId = new FixedString64Bytes(scheduleIdText);

        if (TryExtractStableToken(payloadPath, "stepIndex", out string stepIndexText) &&
            int.TryParse(stepIndexText, out int parsedStepIndex))
        {
            stepIndex = parsedStepIndex;
        }

        const string Marker = ".steps.Array.";
        int markerIndex = payloadPath.IndexOf(Marker, StringComparison.Ordinal);

        if (markerIndex < 0)
            return false;

        int dataEndIndex = payloadPath.IndexOf("].", markerIndex, StringComparison.Ordinal);

        if (dataEndIndex < 0 || dataEndIndex + 2 >= payloadPath.Length)
            return false;

        stepPath = payloadPath.Substring(dataEndIndex + 2);
        return !string.IsNullOrWhiteSpace(stepPath);
    }

    /// <summary>
    /// Checks whether one growth step matches optional schedule and step filters.
    /// </summary>
    /// <param name="step">Growth step candidate.</param>
    /// <param name="scheduleId">Optional schedule ID; empty matches any schedule.</param>
    /// <param name="stepIndex">Optional step index; negative matches any step.</param>
    /// <returns>True when the step should receive the scaling value.</returns>
    private static bool IsMatchingStep(PlayerGrowthSequenceHudStepVisualElement step,
                                       FixedString64Bytes scheduleId,
                                       int stepIndex)
    {
        if (!scheduleId.IsEmpty && !step.ScheduleId.Equals(scheduleId))
            return false;

        if (stepIndex >= 0 && step.StepIndex != stepIndex)
            return false;

        return true;
    }

    /// <summary>
    /// Extracts a stable token value from a normalized scaling path.
    /// </summary>
    /// <param name="payloadPath">Normalized payload path.</param>
    /// <param name="tokenName">Stable token name to find.</param>
    /// <param name="tokenValue">Resolved token value.</param>
    /// <returns>True when the token exists in the path.</returns>
    private static bool TryExtractStableToken(string payloadPath, string tokenName, out string tokenValue)
    {
        tokenValue = string.Empty;
        string marker = tokenName + ":";
        int markerIndex = payloadPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
            return false;

        int valueStartIndex = markerIndex + marker.Length;
        int valueEndIndex = valueStartIndex;

        while (valueEndIndex < payloadPath.Length &&
               payloadPath[valueEndIndex] != ']' &&
               payloadPath[valueEndIndex] != '|')
        {
            valueEndIndex++;
        }

        if (valueEndIndex <= valueStartIndex)
            return false;

        tokenValue = payloadPath.Substring(valueStartIndex, valueEndIndex - valueStartIndex);
        return !string.IsNullOrWhiteSpace(tokenValue);
    }
    #endregion

    #endregion
}
