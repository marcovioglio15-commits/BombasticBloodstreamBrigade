using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies runtime Add Scaling formulas to the player death-animation visual config.
/// </summary>
internal static class PlayerRuntimeScalingDeathAnimationApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Copies the immutable baseline death-animation config into a runtime config.
    /// </summary>
    /// <param name="baseConfig">Baseline config baked from the active visual preset.</param>
    /// <returns>Sanitized runtime config ready for formula application.</returns>
    public static PlayerDeathAnimationConfig CopyDeathAnimation(in PlayerBaseDeathAnimationConfig baseConfig)
    {
        PlayerDeathAnimationConfig config = baseConfig.Config;
        SanitizeConfig(ref config);
        return config;
    }

    /// <summary>
    /// Applies all death-animation scaling formulas against the current scalable-stat variable context.
    /// </summary>
    /// <param name="deathAnimationScaling">Runtime scaling metadata baked from visual preset Add Scaling rules.</param>
    /// <param name="variableContext">Current typed scalable-stat context.</param>
    /// <param name="runtimeDeathAnimation">Mutable runtime death-animation config.</param>
    public static void ApplyScaling(DynamicBuffer<PlayerRuntimeDeathAnimationScalingElement> deathAnimationScaling,
                                    IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                    ref PlayerDeathAnimationConfig runtimeDeathAnimation)
    {
        if (!deathAnimationScaling.IsCreated)
            return;

        for (int scalingIndex = 0; scalingIndex < deathAnimationScaling.Length; scalingIndex++)
        {
            PlayerRuntimeDeathAnimationScalingElement scalingElement = deathAnimationScaling[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    continue;
                }

                ApplyBooleanValue(scalingElement.PayloadPath.ToString(), resolvedBoolean, ref runtimeDeathAnimation);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            ApplyValue(scalingElement.PayloadPath.ToString(), resolvedValue, ref runtimeDeathAnimation);
        }

        SanitizeConfig(ref runtimeDeathAnimation);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one boolean scaling result to the runtime death-animation config.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to deathAnimation.</param>
    /// <param name="resolvedValue">Resolved boolean value.</param>
    /// <param name="runtimeDeathAnimation">Mutable runtime config.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerDeathAnimationConfig runtimeDeathAnimation)
    {
        byte value = resolvedValue ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "enabled":
                runtimeDeathAnimation.Enabled = value;
                break;
            case "cameraZoomEnabled":
                runtimeDeathAnimation.CameraZoomEnabled = value;
                break;
            case "cameraPositionLerpEnabled":
                runtimeDeathAnimation.CameraPositionLerpEnabled = value;
                break;
            case "hidePlayerVisualOnVfxSpawn":
                runtimeDeathAnimation.HidePlayerVisualOnVfxSpawn = value;
                break;
            case "impactFrameEnabled":
                runtimeDeathAnimation.ImpactFrameEnabled = value;
                break;
            default:
                PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyBooleanValue(payloadPath,
                                                                                        resolvedValue,
                                                                                        ref runtimeDeathAnimation.ImpactFrame);
                break;
        }
    }

    /// <summary>
    /// Applies one numeric scaling result to the runtime death-animation config.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to deathAnimation.</param>
    /// <param name="resolvedValue">Resolved numeric value.</param>
    /// <param name="runtimeDeathAnimation">Mutable runtime config.</param>
    private static void ApplyValue(string payloadPath,
                                   float resolvedValue,
                                   ref PlayerDeathAnimationConfig runtimeDeathAnimation)
    {
        switch (payloadPath)
        {
            case "playbackDurationSeconds":
                runtimeDeathAnimation.PlaybackDurationSeconds = resolvedValue;
                break;
            case "cameraTargetFovDelta":
                runtimeDeathAnimation.CameraTargetFovDelta = resolvedValue;
                break;
            case "cameraPositionLerpAmount":
                runtimeDeathAnimation.CameraPositionLerpAmount = resolvedValue;
                break;
            case "cameraCompletionNormalizedTime":
                runtimeDeathAnimation.CameraCompletionNormalizedTime = resolvedValue;
                break;
            case "easingMode":
                runtimeDeathAnimation.EasingMode = PlayerRuntimeScalingEnumUtility.ResolvePlayerDeathAnimationEasing(resolvedValue);
                break;
            case "despawnVfxSpawnOffset.x":
                runtimeDeathAnimation.DespawnVfxSpawnOffset.x = resolvedValue;
                break;
            case "despawnVfxSpawnOffset.y":
                runtimeDeathAnimation.DespawnVfxSpawnOffset.y = resolvedValue;
                break;
            case "despawnVfxSpawnOffset.z":
                runtimeDeathAnimation.DespawnVfxSpawnOffset.z = resolvedValue;
                break;
            case "despawnVfxScaleMultiplier":
                runtimeDeathAnimation.DespawnVfxScaleMultiplier = resolvedValue;
                break;
            case "despawnVfxSpawnNormalizedTime":
                runtimeDeathAnimation.DespawnVfxSpawnNormalizedTime = resolvedValue;
                break;
            case "despawnVfxLifetimeSeconds":
                runtimeDeathAnimation.DespawnVfxLifetimeSeconds = resolvedValue;
                break;
            case "impactFrameBuildInStartNormalizedTime":
                runtimeDeathAnimation.ImpactFrameBuildInStartNormalizedTime = resolvedValue;
                break;
            case "impactFrameApplyNormalizedTime":
                runtimeDeathAnimation.ImpactFrameApplyNormalizedTime = resolvedValue;
                break;
            case "impactFrameEndNormalizedTime":
                runtimeDeathAnimation.ImpactFrameEndNormalizedTime = resolvedValue;
                break;
            default:
                PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyValue(payloadPath,
                                                                                 resolvedValue,
                                                                                 ref runtimeDeathAnimation.ImpactFrame);
                break;
        }
    }

    /// <summary>
    /// Clamps runtime-only death-animation copies to safe presentation ranges without mutating authored presets.
    /// </summary>
    /// <param name="runtimeDeathAnimation">Mutable runtime config.</param>
    private static void SanitizeConfig(ref PlayerDeathAnimationConfig runtimeDeathAnimation)
    {
        runtimeDeathAnimation.PlaybackDurationSeconds = math.max(0f, runtimeDeathAnimation.PlaybackDurationSeconds);
        runtimeDeathAnimation.CameraPositionLerpAmount = math.saturate(runtimeDeathAnimation.CameraPositionLerpAmount);
        runtimeDeathAnimation.CameraCompletionNormalizedTime = math.saturate(runtimeDeathAnimation.CameraCompletionNormalizedTime);
        runtimeDeathAnimation.DespawnVfxScaleMultiplier = math.max(0f, runtimeDeathAnimation.DespawnVfxScaleMultiplier);
        runtimeDeathAnimation.DespawnVfxSpawnNormalizedTime = math.saturate(runtimeDeathAnimation.DespawnVfxSpawnNormalizedTime);
        runtimeDeathAnimation.DespawnVfxLifetimeSeconds = math.max(0f, runtimeDeathAnimation.DespawnVfxLifetimeSeconds);
        runtimeDeathAnimation.ImpactFrameBuildInStartNormalizedTime = math.saturate(runtimeDeathAnimation.ImpactFrameBuildInStartNormalizedTime);
        runtimeDeathAnimation.ImpactFrameApplyNormalizedTime = math.saturate(runtimeDeathAnimation.ImpactFrameApplyNormalizedTime);
        runtimeDeathAnimation.ImpactFrameEndNormalizedTime = math.saturate(runtimeDeathAnimation.ImpactFrameEndNormalizedTime);

        if (runtimeDeathAnimation.Enabled != 0)
            return;

        runtimeDeathAnimation.PlaybackDurationSeconds = 0f;
    }
    #endregion

    #endregion
}
