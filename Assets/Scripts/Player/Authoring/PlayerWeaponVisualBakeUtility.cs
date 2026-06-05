using System.Text;
using Unity.Collections;
using Unity.Entities;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds immutable and runtime ECS weapon visual configuration from the resolved player visual preset.
/// </summary>
internal static class PlayerWeaponVisualBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the immutable weapon visual baseline used by runtime Add Scaling rebuilds.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Immutable weapon visual baseline.</returns>
    public static PlayerBaseWeaponVisualConfig BuildBaseConfig(PlayerVisualPreset visualPreset)
    {
        PlayerWeaponVisualSettings settings = visualPreset != null ? visualPreset.WeaponVisuals : null;
        return new PlayerBaseWeaponVisualConfig
        {
            BaseGunReference = BuildReference(settings != null ? settings.BaseGunReference : PlayerWeaponVisualSettings.DefaultBaseGunSelector,
                                              PlayerWeaponVisualSettings.DefaultBaseGunSelector),
            CannonReference = BuildReference(settings != null ? settings.CannonReference : PlayerWeaponVisualSettings.DefaultCannonSelector,
                                             PlayerWeaponVisualSettings.DefaultCannonSelector),
            GatlingReference = BuildReference(settings != null ? settings.GatlingReference : PlayerWeaponVisualSettings.DefaultGatlingSelector,
                                              PlayerWeaponVisualSettings.DefaultGatlingSelector),
            RailgunReference = BuildReference(settings != null ? settings.RailgunReference : PlayerWeaponVisualSettings.DefaultRailgunSelector,
                                              PlayerWeaponVisualSettings.DefaultRailgunSelector),
            DefaultAdditionalWeaponVisual = settings != null
                ? ResolveDefaultAdditionalWeaponVisual(settings.DefaultAdditionalWeaponVisual)
                : PlayerWeaponVisualSlot.None
        };
    }

    /// <summary>
    /// Writes scaled weapon visual values into the runtime visual bridge configuration.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="runtimeConfig">Runtime visual bridge configuration updated in place.</param>
    public static void ApplyRuntimeConfig(PlayerVisualPreset visualPreset, ref PlayerVisualRuntimeBridgeConfig runtimeConfig)
    {
        PlayerBaseWeaponVisualConfig resolvedConfig = BuildBaseConfig(visualPreset);
        runtimeConfig.BaseGunReference = resolvedConfig.BaseGunReference;
        runtimeConfig.CannonReference = resolvedConfig.CannonReference;
        runtimeConfig.GatlingReference = resolvedConfig.GatlingReference;
        runtimeConfig.RailgunReference = resolvedConfig.RailgunReference;
        runtimeConfig.DefaultAdditionalWeaponVisual = resolvedConfig.DefaultAdditionalWeaponVisual;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates runtime weapon visual scaling metadata from the unscaled visual preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled visual preset used to resolve baseline values and formulas.</param>
    /// <param name="scalingBuffer">Destination runtime weapon visual scaling buffer.</param>
    public static void PopulateScalingMetadata(PlayerVisualPreset sourcePreset,
                                               DynamicBuffer<PlayerRuntimeWeaponVisualScalingElement> scalingBuffer)
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

            if (!TryMapFieldId(scalingRule.StatKey, out PlayerRuntimeWeaponVisualFieldId fieldId))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (!TryBuildScalingElement(fieldId, scalingRule, property, out PlayerRuntimeWeaponVisualScalingElement scalingElement))
                continue;

            scalingBuffer.Add(scalingElement);
        }
    }
#endif
    #endregion

    #region Helpers
    /// <summary>
    /// Builds one fixed-size ECS reference selector while preserving a deterministic fallback.
    /// </summary>
    /// <param name="selector">Authored reference selector.</param>
    /// <param name="fallbackSelector">Fallback selector used when the authored value is empty or oversized.</param>
    /// <returns>Runtime-safe fixed-size reference selector.</returns>
    private static FixedString128Bytes BuildReference(string selector, string fallbackSelector)
    {
        string resolvedSelector = string.IsNullOrWhiteSpace(selector) ? fallbackSelector : selector.Trim();

        if (Encoding.UTF8.GetByteCount(resolvedSelector) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            resolvedSelector = fallbackSelector;

        return new FixedString128Bytes(resolvedSelector);
    }

    /// <summary>
    /// Resolves a valid default optional weapon attachment without changing the source preset.
    /// </summary>
    /// <param name="weaponVisual">Authored default optional weapon attachment.</param>
    /// <returns>Supported default attachment, falling back to None.</returns>
    private static PlayerWeaponVisualSlot ResolveDefaultAdditionalWeaponVisual(PlayerWeaponVisualSlot weaponVisual)
    {
        int weaponVisualValue = (int)weaponVisual;

        if (weaponVisualValue < (int)PlayerWeaponVisualSlot.None ||
            weaponVisualValue > (int)PlayerWeaponVisualSlot.Railgun)
        {
            return PlayerWeaponVisualSlot.None;
        }

        return weaponVisual;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Maps normalized visual preset stat keys to runtime weapon visual fields.
    /// </summary>
    /// <param name="statKey">Raw Add Scaling stat key.</param>
    /// <param name="fieldId">Resolved runtime field identifier.</param>
    /// <returns>True when the key targets a supported weapon visual field.</returns>
    private static bool TryMapFieldId(string statKey, out PlayerRuntimeWeaponVisualFieldId fieldId)
    {
        fieldId = default;

        switch (PlayerScalingStatKeyUtility.NormalizeStatKey(statKey))
        {
            case "weaponVisuals.baseGunReference":
                fieldId = PlayerRuntimeWeaponVisualFieldId.BaseGunReference;
                return true;
            case "weaponVisuals.cannonReference":
                fieldId = PlayerRuntimeWeaponVisualFieldId.CannonReference;
                return true;
            case "weaponVisuals.gatlingReference":
                fieldId = PlayerRuntimeWeaponVisualFieldId.GatlingReference;
                return true;
            case "weaponVisuals.railgunReference":
                fieldId = PlayerRuntimeWeaponVisualFieldId.RailgunReference;
                return true;
            case "weaponVisuals.defaultAdditionalWeaponVisual":
                fieldId = PlayerRuntimeWeaponVisualFieldId.DefaultAdditionalWeaponVisual;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Builds one runtime scaling element from a supported weapon visual property.
    /// </summary>
    /// <param name="fieldId">Target runtime field identifier.</param>
    /// <param name="scalingRule">Source Add Scaling rule.</param>
    /// <param name="property">Serialized source property targeted by the rule.</param>
    /// <param name="scalingElement">Built runtime scaling element.</param>
    /// <returns>True when the property can be represented by weapon visual runtime metadata.</returns>
    private static bool TryBuildScalingElement(PlayerRuntimeWeaponVisualFieldId fieldId,
                                               PlayerStatScalingRule scalingRule,
                                               SerializedProperty property,
                                               out PlayerRuntimeWeaponVisualScalingElement scalingElement)
    {
        scalingElement = default;

        if (property.propertyType == SerializedPropertyType.Enum)
        {
            scalingElement = new PlayerRuntimeWeaponVisualScalingElement
            {
                FieldId = fieldId,
                ValueType = (byte)PlayerFormulaValueType.Number,
                BaseValue = property.enumValueIndex,
                IsInteger = 1,
                Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                        property,
                                                                                                        null))
            };
            return true;
        }

        if (property.propertyType != SerializedPropertyType.String)
            return false;

        string tokenValue = string.IsNullOrWhiteSpace(property.stringValue) ? string.Empty : property.stringValue.Trim();

        if (Encoding.UTF8.GetByteCount(tokenValue) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            return false;

        scalingElement = new PlayerRuntimeWeaponVisualScalingElement
        {
            FieldId = fieldId,
            ValueType = (byte)PlayerFormulaValueType.Token,
            BaseTokenValue = new FixedString128Bytes(tokenValue),
            Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                    property,
                                                                                                    null))
        };
        return true;
    }
#endif
    #endregion

    #endregion
}
