using Unity.Collections;
using Unity.Entities;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Bakes runtime scaling metadata for scalable fields owned by the player visual preset.
/// </summary>
internal static class PlayerRuntimeScalingVisualBakeUtility
{
    #region Methods

#if UNITY_EDITOR
    #region Public Methods
    /// <summary>
    /// Populates death-animation scaling metadata from the source visual preset Add Scaling rules.
    /// </summary>
    /// <param name="sourcePreset">Source visual preset used to resolve unscaled fields and scaling formulas.</param>
    /// <param name="scalingBuffer">Destination runtime scaling buffer.</param>
    public static void PopulateDeathAnimationScalingMetadata(PlayerVisualPreset sourcePreset,
                                                             DynamicBuffer<PlayerRuntimeDeathAnimationScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!TryMapDeathAnimationFieldId(scalingRule.StatKey, out PlayerRuntimeDeathAnimationFieldId fieldId))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (!PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                              out byte valueType,
                                                                              out float baseValue,
                                                                              out byte baseBooleanValue,
                                                                              out byte isInteger))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeDeathAnimationScalingElement
            {
                FieldId = fieldId,
                ValueType = valueType,
                BaseValue = baseValue,
                BaseBooleanValue = baseBooleanValue,
                IsInteger = isInteger,
                Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                       property,
                                                                                                       null))
            });
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Maps normalized visual preset stat keys to runtime death-animation fields.
    /// </summary>
    /// <param name="statKey">Raw Add Scaling stat key.</param>
    /// <param name="fieldId">Resolved runtime field identifier.</param>
    /// <returns>True when the key targets a death-animation field supported by runtime scaling.</returns>
    private static bool TryMapDeathAnimationFieldId(string statKey, out PlayerRuntimeDeathAnimationFieldId fieldId)
    {
        fieldId = default;
        string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(statKey);

        switch (normalizedStatKey)
        {
            case "deathAnimation.enabled":
                fieldId = PlayerRuntimeDeathAnimationFieldId.Enabled;
                return true;
            case "deathAnimation.playbackDurationSeconds":
                fieldId = PlayerRuntimeDeathAnimationFieldId.PlaybackDurationSeconds;
                return true;
            case "deathAnimation.cameraZoomEnabled":
                fieldId = PlayerRuntimeDeathAnimationFieldId.CameraZoomEnabled;
                return true;
            case "deathAnimation.cameraTargetFovDelta":
                fieldId = PlayerRuntimeDeathAnimationFieldId.CameraTargetFovDelta;
                return true;
            case "deathAnimation.cameraPositionLerpEnabled":
                fieldId = PlayerRuntimeDeathAnimationFieldId.CameraPositionLerpEnabled;
                return true;
            case "deathAnimation.cameraPositionLerpAmount":
                fieldId = PlayerRuntimeDeathAnimationFieldId.CameraPositionLerpAmount;
                return true;
            case "deathAnimation.cameraCompletionNormalizedTime":
                fieldId = PlayerRuntimeDeathAnimationFieldId.CameraCompletionNormalizedTime;
                return true;
            case "deathAnimation.easingMode":
                fieldId = PlayerRuntimeDeathAnimationFieldId.EasingMode;
                return true;
            case "deathAnimation.despawnVfxSpawnOffset.x":
                fieldId = PlayerRuntimeDeathAnimationFieldId.DespawnVfxSpawnOffsetX;
                return true;
            case "deathAnimation.despawnVfxSpawnOffset.y":
                fieldId = PlayerRuntimeDeathAnimationFieldId.DespawnVfxSpawnOffsetY;
                return true;
            case "deathAnimation.despawnVfxSpawnOffset.z":
                fieldId = PlayerRuntimeDeathAnimationFieldId.DespawnVfxSpawnOffsetZ;
                return true;
            case "deathAnimation.despawnVfxScaleMultiplier":
                fieldId = PlayerRuntimeDeathAnimationFieldId.DespawnVfxScaleMultiplier;
                return true;
            case "deathAnimation.despawnVfxSpawnNormalizedTime":
                fieldId = PlayerRuntimeDeathAnimationFieldId.DespawnVfxSpawnNormalizedTime;
                return true;
            case "deathAnimation.despawnVfxLifetimeSeconds":
                fieldId = PlayerRuntimeDeathAnimationFieldId.DespawnVfxLifetimeSeconds;
                return true;
            case "deathAnimation.hidePlayerVisualOnVfxSpawn":
                fieldId = PlayerRuntimeDeathAnimationFieldId.HidePlayerVisualOnVfxSpawn;
                return true;
            default:
                return false;
        }
    }
    #endregion
#endif

    #endregion
}
