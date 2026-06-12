using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Bakes runtime scaling metadata for scalable fields owned by the player visual preset.
/// </summary>
public static class PlayerRuntimeScalingVisualBakeUtility
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

            string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(scalingRule.StatKey);

            if (!normalizedStatKey.StartsWith("deathAnimation.", StringComparison.Ordinal))
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
                PayloadPath = new FixedString128Bytes(normalizedStatKey.Substring("deathAnimation.".Length)),
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

    /// <summary>
    /// Populates projectile-death VFX scaling metadata from the source visual preset Add Scaling rules.
    /// </summary>
    /// <param name="sourcePreset">Source visual preset used to resolve unscaled fields and scaling formulas.</param>
    /// <param name="scalingBuffer">Destination runtime scaling buffer.</param>
    public static void PopulateProjectileDeathVfxScalingMetadata(PlayerVisualPreset sourcePreset,
                                                                 DynamicBuffer<PlayerRuntimeProjectileDeathVfxScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling || string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(scalingRule.StatKey);

            if (!normalizedStatKey.StartsWith("projectileDeathVfx.", StringComparison.Ordinal))
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

            scalingBuffer.Add(new PlayerRuntimeProjectileDeathVfxScalingElement
            {
                PayloadPath = new FixedString128Bytes(normalizedStatKey.Substring("projectileDeathVfx.".Length)),
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

    /// <summary>
    /// Populates Jetpack VFX scaling metadata from the source visual preset Add Scaling rules.
    /// </summary>
    /// <param name="sourcePreset">Source visual preset used to resolve unscaled fields and scaling formulas.</param>
    /// <param name="scalingBuffer">Destination runtime scaling buffer.</param>
    public static void PopulateJetpackVfxScalingMetadata(PlayerVisualPreset sourcePreset,
                                                         DynamicBuffer<PlayerRuntimeJetpackVfxScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling || string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(scalingRule.StatKey);

            if (!normalizedStatKey.StartsWith("playerJetpackVfx.", StringComparison.Ordinal))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (property.propertyType == SerializedPropertyType.String)
            {
                string tokenValue = string.IsNullOrWhiteSpace(property.stringValue)
                    ? string.Empty
                    : property.stringValue.Trim();

                if (Encoding.UTF8.GetByteCount(tokenValue) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
                    continue;

                scalingBuffer.Add(new PlayerRuntimeJetpackVfxScalingElement
                {
                    PayloadPath = new FixedString128Bytes(normalizedStatKey.Substring("playerJetpackVfx.".Length)),
                    ValueType = (byte)PlayerFormulaValueType.Token,
                    BaseTokenValue = new FixedString128Bytes(tokenValue),
                    Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                           property,
                                                                                                           null))
                });
                continue;
            }

            if (!PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                              out byte valueType,
                                                                              out float baseValue,
                                                                              out byte baseBooleanValue,
                                                                              out byte isInteger))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeJetpackVfxScalingElement
            {
                PayloadPath = new FixedString128Bytes(normalizedStatKey.Substring("playerJetpackVfx.".Length)),
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

#endif

    #endregion
}
