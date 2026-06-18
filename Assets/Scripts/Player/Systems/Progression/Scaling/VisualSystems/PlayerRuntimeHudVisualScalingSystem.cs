using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds scalable player HUD portrait settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimePortraitHudVisualScalingSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable portrait HUD settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerPortraitHudVisualOwner>();
        state.RequireForUpdate<PlayerPortraitHudVisualScalingState>();
        state.RequireForUpdate<PlayerBasePortraitHudVisualConfig>();
        state.RequireForUpdate<PlayerPortraitHudVisualConfig>();
        state.RequireForUpdate<PlayerRuntimePortraitHudVisualScalingElement>();
    }

    /// <summary>
    /// Restores portrait HUD baselines and applies all formulas when scalable stats change.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimePortraitHudVisualScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimePortraitHudVisualScalingElement>(true);
        BufferLookup<PlayerBasePortraitHudAnimationElement> baseAnimationLookup = SystemAPI.GetBufferLookup<PlayerBasePortraitHudAnimationElement>(true);
        BufferLookup<PlayerPortraitHudAnimationElement> animationLookup = SystemAPI.GetBufferLookup<PlayerPortraitHudAnimationElement>(false);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerPortraitHudVisualOwner> owner,
                  RefRW<PlayerPortraitHudVisualScalingState> visualScalingState,
                  RefRO<PlayerBasePortraitHudVisualConfig> baseConfig,
                  RefRW<PlayerPortraitHudVisualConfig> runtimeConfig,
                  Entity configEntity)
                 in SystemAPI.Query<RefRO<PlayerPortraitHudVisualOwner>,
                                    RefRW<PlayerPortraitHudVisualScalingState>,
                                    RefRO<PlayerBasePortraitHudVisualConfig>,
                                    RefRW<PlayerPortraitHudVisualConfig>>()
                             .WithAll<PlayerRuntimePortraitHudVisualScalingElement>()
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
            CopyBaseAnimations(baseAnimationLookup[configEntity], animationLookup[configEntity]);
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
                         animationLookup[configEntity]);
            visualScalingState.ValueRW.Initialized = 1;
            visualScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Copies immutable portrait animation baselines into the mutable runtime buffer.
    /// </summary>
    /// <param name="baseAnimations">Immutable baseline animation entries.</param>
    /// <param name="runtimeAnimations">Mutable runtime animation entries.</param>
    private static void CopyBaseAnimations(DynamicBuffer<PlayerBasePortraitHudAnimationElement> baseAnimations,
                                           DynamicBuffer<PlayerPortraitHudAnimationElement> runtimeAnimations)
    {
        runtimeAnimations.Clear();

        for (int animationIndex = 0; animationIndex < baseAnimations.Length; animationIndex++)
        {
            PlayerBasePortraitHudAnimationElement animation = baseAnimations[animationIndex];
            runtimeAnimations.Add(new PlayerPortraitHudAnimationElement
            {
                AnimationId = animation.AnimationId,
                Role = animation.Role,
                TriggerKey = animation.TriggerKey,
                FrameStartIndex = animation.FrameStartIndex,
                FrameCount = animation.FrameCount,
                SecondsPerFrame = animation.SecondsPerFrame,
                PlaybackSpeedMultiplier = animation.PlaybackSpeedMultiplier,
                PlaybackMode = animation.PlaybackMode,
                Priority = animation.Priority,
                RestartWhenReentered = animation.RestartWhenReentered
            });
        }
    }

    /// <summary>
    /// Applies all portrait HUD formulas to freshly restored runtime settings.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable portrait HUD visual configuration.</param>
    /// <param name="runtimeAnimations">Mutable portrait animation buffer.</param>
    private static void ApplyScaling(DynamicBuffer<PlayerRuntimePortraitHudVisualScalingElement> scalingBuffer,
                                     ref PlayerPortraitHudVisualConfig runtimeConfig,
                                     DynamicBuffer<PlayerPortraitHudAnimationElement> runtimeAnimations)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimePortraitHudVisualScalingElement scalingElement = scalingBuffer[scalingIndex];
            string payloadPath = scalingElement.PayloadPath.ToString();

            switch ((PlayerFormulaValueType)scalingElement.ValueType)
            {
                case PlayerFormulaValueType.Boolean:
                    if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                              scalingElement.BaseBooleanValue != 0,
                                                                                              VariableContext,
                                                                                              out bool resolvedBoolean))
                    {
                        ApplyBooleanValue(payloadPath, resolvedBoolean, ref runtimeConfig, runtimeAnimations);
                    }
                    break;
                case PlayerFormulaValueType.Number:
                    if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                              scalingElement.BaseValue,
                                                                                              scalingElement.IsInteger != 0,
                                                                                              VariableContext,
                                                                                              out float resolvedNumber))
                    {
                        ApplyNumericValue(payloadPath, resolvedNumber, ref runtimeConfig, runtimeAnimations);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Applies one boolean formula result to portrait HUD config or animation toggles.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Portrait.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="runtimeConfig">Mutable portrait HUD visual configuration.</param>
    /// <param name="runtimeAnimations">Mutable portrait animation buffer.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerPortraitHudVisualConfig runtimeConfig,
                                          DynamicBuffer<PlayerPortraitHudAnimationElement> runtimeAnimations)
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

        if (TryResolveAnimationTarget(payloadPath,
                                      out PlayerPortraitHudAnimationRole role,
                                      out FixedString64Bytes triggerKey,
                                      out string animationPath))
        {
            ApplyAnimationBooleanValue(role, triggerKey, animationPath, byteValue, runtimeAnimations);
        }
    }

    /// <summary>
    /// Applies one numeric formula result to portrait HUD config or animation fields.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Portrait.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="runtimeConfig">Mutable portrait HUD visual configuration.</param>
    /// <param name="runtimeAnimations">Mutable portrait animation buffer.</param>
    private static void ApplyNumericValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerPortraitHudVisualConfig runtimeConfig,
                                          DynamicBuffer<PlayerPortraitHudAnimationElement> runtimeAnimations)
    {
        if (TryResolveAnimationTarget(payloadPath,
                                      out PlayerPortraitHudAnimationRole role,
                                      out FixedString64Bytes triggerKey,
                                      out string animationPath))
        {
            ApplyAnimationNumericValue(role, triggerKey, animationPath, resolvedValue, runtimeAnimations);
        }
    }

    /// <summary>
    /// Applies a boolean value to matching portrait animations.
    /// </summary>
    /// <param name="role">Animation role to match.</param>
    /// <param name="triggerKey">Optional trigger key to match.</param>
    /// <param name="animationPath">Target path relative to the animation definition.</param>
    /// <param name="byteValue">Resolved byte boolean value.</param>
    /// <param name="runtimeAnimations">Mutable portrait animation buffer.</param>
    private static void ApplyAnimationBooleanValue(PlayerPortraitHudAnimationRole role,
                                                   FixedString64Bytes triggerKey,
                                                   string animationPath,
                                                   byte byteValue,
                                                   DynamicBuffer<PlayerPortraitHudAnimationElement> runtimeAnimations)
    {
        if (!string.Equals(animationPath, "restartWhenReentered", StringComparison.Ordinal))
            return;

        for (int animationIndex = 0; animationIndex < runtimeAnimations.Length; animationIndex++)
        {
            PlayerPortraitHudAnimationElement animation = runtimeAnimations[animationIndex];

            if (!IsMatchingAnimation(animation, role, triggerKey))
                continue;

            animation.RestartWhenReentered = byteValue;
            runtimeAnimations[animationIndex] = animation;
        }
    }

    /// <summary>
    /// Applies a numeric value to matching portrait animations.
    /// </summary>
    /// <param name="role">Animation role to match.</param>
    /// <param name="triggerKey">Optional trigger key to match.</param>
    /// <param name="animationPath">Target path relative to the animation definition.</param>
    /// <param name="resolvedValue">Formula result.</param>
    /// <param name="runtimeAnimations">Mutable portrait animation buffer.</param>
    private static void ApplyAnimationNumericValue(PlayerPortraitHudAnimationRole role,
                                                   FixedString64Bytes triggerKey,
                                                   string animationPath,
                                                   float resolvedValue,
                                                   DynamicBuffer<PlayerPortraitHudAnimationElement> runtimeAnimations)
    {
        for (int animationIndex = 0; animationIndex < runtimeAnimations.Length; animationIndex++)
        {
            PlayerPortraitHudAnimationElement animation = runtimeAnimations[animationIndex];

            if (!IsMatchingAnimation(animation, role, triggerKey))
                continue;

            switch (animationPath)
            {
                case "secondsPerFrame":
                    animation.SecondsPerFrame = math.max(0.0001f, resolvedValue);
                    break;
                case "playbackSpeedMultiplier":
                    animation.PlaybackSpeedMultiplier = math.max(0.0001f, resolvedValue);
                    break;
                case "priority":
                    animation.Priority = (int)math.round(resolvedValue);
                    break;
                case "playbackMode":
                    animation.PlaybackMode = (PlayerPortraitHudPlaybackMode)math.clamp((int)math.round(resolvedValue), 0, 2);
                    break;
                default:
                    break;
            }

            runtimeAnimations[animationIndex] = animation;
        }
    }

    /// <summary>
    /// Resolves a portrait payload path into role, trigger key, and animation-local path.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Portrait.</param>
    /// <param name="role">Resolved animation role.</param>
    /// <param name="triggerKey">Resolved optional trigger key.</param>
    /// <param name="animationPath">Resolved path relative to the animation definition.</param>
    /// <returns>True when the path targets an animation definition.</returns>
    private static bool TryResolveAnimationTarget(string payloadPath,
                                                  out PlayerPortraitHudAnimationRole role,
                                                  out FixedString64Bytes triggerKey,
                                                  out string animationPath)
    {
        role = PlayerPortraitHudAnimationRole.Idle;
        triggerKey = default;
        animationPath = string.Empty;

        if (TryStripPrefix(payloadPath, "idleAnimation.", out animationPath))
            return true;

        if (TryStripPrefix(payloadPath, "damageAnimation.", out animationPath))
        {
            role = PlayerPortraitHudAnimationRole.Damage;
            return true;
        }

        if (TryStripPrefix(payloadPath, "deathAnimation.", out animationPath))
        {
            role = PlayerPortraitHudAnimationRole.Death;
            return true;
        }

        if (TryStripNestedAnimationPath(payloadPath, "comboRankAnimations.", out animationPath))
        {
            role = PlayerPortraitHudAnimationRole.ComboRankIdle;

            if (TryExtractStableToken(payloadPath, "rankId", out string rankId))
                triggerKey = new FixedString64Bytes(rankId);

            return true;
        }

        if (TryStripNestedAnimationPath(payloadPath, "powerUpAnimations.", out animationPath))
        {
            role = PlayerPortraitHudAnimationRole.PowerUpAcquired;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether one runtime animation matches a role and optional trigger key.
    /// </summary>
    /// <param name="animation">Animation candidate to inspect.</param>
    /// <param name="role">Required animation role.</param>
    /// <param name="triggerKey">Optional trigger key; empty matches every key for the role.</param>
    /// <returns>True when the animation should receive the scaling value.</returns>
    private static bool IsMatchingAnimation(PlayerPortraitHudAnimationElement animation,
                                            PlayerPortraitHudAnimationRole role,
                                            FixedString64Bytes triggerKey)
    {
        if (animation.Role != role)
            return false;

        if (triggerKey.IsEmpty)
            return true;

        return animation.TriggerKey.Equals(triggerKey);
    }

    /// <summary>
    /// Strips an animation property path nested under an array element.
    /// </summary>
    /// <param name="payloadPath">Target path relative to Portrait.</param>
    /// <param name="prefix">Collection prefix to match.</param>
    /// <param name="animationPath">Resolved path relative to the animation definition.</param>
    /// <returns>True when a nested animation path was found.</returns>
    private static bool TryStripNestedAnimationPath(string payloadPath, string prefix, out string animationPath)
    {
        animationPath = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadPath) || !payloadPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        const string Marker = ".animation.";
        int markerIndex = payloadPath.IndexOf(Marker, StringComparison.Ordinal);

        if (markerIndex < 0)
            return false;

        animationPath = payloadPath.Substring(markerIndex + Marker.Length);
        return !string.IsNullOrWhiteSpace(animationPath);
    }

    /// <summary>
    /// Strips a fixed prefix from a path.
    /// </summary>
    /// <param name="payloadPath">Incoming payload path.</param>
    /// <param name="prefix">Prefix to remove.</param>
    /// <param name="suffix">Resolved suffix.</param>
    /// <returns>True when the prefix matched.</returns>
    private static bool TryStripPrefix(string payloadPath, string prefix, out string suffix)
    {
        suffix = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadPath) || !payloadPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        suffix = payloadPath.Substring(prefix.Length);
        return !string.IsNullOrWhiteSpace(suffix);
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

