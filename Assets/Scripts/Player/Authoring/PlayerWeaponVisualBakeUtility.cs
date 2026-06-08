using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds immutable and runtime ECS weapon visual configuration from the resolved player visual preset. Walks
/// the mountable weapons array to populate the runtime bridge configuration, the per-weapon buffer, the implicit
/// Base Gun shooting clip, and the Add Scaling metadata without using runtime reflection.
/// </summary>
public static class PlayerWeaponVisualBakeUtility
{
    #region Constants
    private const string AdditionalWeaponsPathSegment = "weaponVisuals.additionalWeapons.Array.data[";
    private const string AdditionalRuntimeReferenceFieldSuffix = "].runtimeReference";
    private const string AdditionalWeaponIdFieldSuffix = "].weaponId";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the immutable weapon visual baseline consumed by runtime Add Scaling rebuilds. The mountable
    /// weapons baseline is populated separately into the supplied buffer; this overload only emits the bridge
    /// scalar fields so callers can split the two writes.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Immutable weapon visual baseline scalar fields.</returns>
    public static PlayerBaseWeaponVisualConfig BuildBaseConfig(PlayerVisualPreset visualPreset)
    {
        PlayerWeaponVisualSettings settings = visualPreset != null ? visualPreset.WeaponVisuals : null;
        return new PlayerBaseWeaponVisualConfig
        {
            BaseGunReference = BuildReference(settings != null ? settings.BaseGunReference : PlayerWeaponVisualSettings.DefaultBaseGunSelector,
                                              PlayerWeaponVisualSettings.DefaultBaseGunSelector),
            DefaultAdditionalWeaponId = BuildWeaponId(settings != null ? settings.DefaultAdditionalWeaponId : string.Empty)
        };
    }

    /// <summary>
    /// Writes scaled weapon visual scalar values into the runtime visual bridge configuration. Called once at
    /// bake after the visual preset has been cloned and any Add Scaling formulas have been applied to the clone.
    /// The mountable weapons buffer is populated separately via <see cref="PopulateAdditionalWeaponsBuffer"/>.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="runtimeConfig">Runtime visual bridge configuration updated in place.</param>
    public static void ApplyRuntimeConfig(PlayerVisualPreset visualPreset, ref PlayerVisualRuntimeBridgeConfig runtimeConfig)
    {
        PlayerBaseWeaponVisualConfig resolvedConfig = BuildBaseConfig(visualPreset);
        runtimeConfig.BaseGunReference = resolvedConfig.BaseGunReference;
        runtimeConfig.DefaultAdditionalWeaponId = resolvedConfig.DefaultAdditionalWeaponId;
    }

    /// <summary>
    /// Fills the runtime mountable-weapons buffer with one element per authored entry, preserving the entry
    /// order. Empty Weapon Ids and oversized strings are sanitized into fixed-size tokens so the buffer never
    /// holds invalid FixedString values.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="buffer">Runtime additional-weapons buffer rebuilt in place.</param>
    public static void PopulateAdditionalWeaponsBuffer(PlayerVisualPreset visualPreset,
                                                        DynamicBuffer<PlayerAdditionalWeaponVisualElement> buffer)
    {
        buffer.Clear();
        PlayerWeaponVisualSettings settings = visualPreset != null ? visualPreset.WeaponVisuals : null;

        if (settings == null || settings.AdditionalWeapons == null)
            return;

        for (int entryIndex = 0; entryIndex < settings.AdditionalWeapons.Count; entryIndex++)
        {
            PlayerAdditionalWeaponVisualEntry entry = settings.AdditionalWeapons[entryIndex];

            if (entry == null)
            {
                buffer.Add(default);
                continue;
            }

            buffer.Add(new PlayerAdditionalWeaponVisualElement
            {
                WeaponId = BuildWeaponId(entry.WeaponId),
                RuntimeReference = BuildReference(entry.RuntimeReference, string.Empty),
                ShootAnimationClip = entry.ShootAnimationClip
            });
        }
    }

    /// <summary>
    /// Fills the baseline mountable-weapons buffer used by the runtime scaling system to rebuild the live
    /// runtime buffer when the scalable-stat hash changes. Mirrors <see cref="PopulateAdditionalWeaponsBuffer"/>
    /// against the immutable source preset.
    /// </summary>
    /// <param name="sourceVisualPreset">Unscaled source visual preset providing baseline values.</param>
    /// <param name="buffer">Baseline additional-weapons buffer rebuilt in place.</param>
    public static void PopulateBaseAdditionalWeaponsBuffer(PlayerVisualPreset sourceVisualPreset,
                                                            DynamicBuffer<PlayerBaseAdditionalWeaponVisualElement> buffer)
    {
        buffer.Clear();
        PlayerWeaponVisualSettings settings = sourceVisualPreset != null ? sourceVisualPreset.WeaponVisuals : null;

        if (settings == null || settings.AdditionalWeapons == null)
            return;

        for (int entryIndex = 0; entryIndex < settings.AdditionalWeapons.Count; entryIndex++)
        {
            PlayerAdditionalWeaponVisualEntry entry = settings.AdditionalWeapons[entryIndex];

            if (entry == null)
            {
                buffer.Add(default);
                continue;
            }

            buffer.Add(new PlayerBaseAdditionalWeaponVisualElement
            {
                WeaponId = BuildWeaponId(entry.WeaponId),
                RuntimeReference = BuildReference(entry.RuntimeReference, string.Empty),
                ShootAnimationClip = entry.ShootAnimationClip
            });
        }
    }

    /// <summary>
    /// Resolves the implicit Base Gun shooting clip from the entry matching the visual preset Default
    /// Additional Weapon Id. Returns null when no default is set or the matching entry is missing/empty so the
    /// upper-body presentation utility can fall back to its no-clip path safely.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset whose default attachment drives the implicit clip.</param>
    /// <returns>Matching shooting clip, or null when no default entry is authored.</returns>
    public static AnimationClip ResolveDefaultShootClip(PlayerVisualPreset visualPreset)
    {
        PlayerWeaponVisualSettings settings = visualPreset != null ? visualPreset.WeaponVisuals : null;
        return settings != null ? settings.ResolveShootClip(settings.DefaultAdditionalWeaponId) : null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates runtime weapon visual scaling metadata from the unscaled visual preset. Walks each Add Scaling
    /// rule, resolves its current serialized target, and captures the target array index so the runtime scaling
    /// buffer can locate the matching mountable entry even when the authored array is reordered.
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

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (!TryMapField(sourcePreset,
                             scalingRule.StatKey,
                             property,
                             out PlayerRuntimeWeaponVisualFieldId fieldId,
                             out int targetEntryIndex))
                continue;

            if (!TryBuildScalingElement(fieldId,
                                         targetEntryIndex,
                                         scalingRule,
                                         property,
                                         out PlayerRuntimeWeaponVisualScalingElement scalingElement))
                continue;

            scalingBuffer.Add(scalingElement);
        }
    }
#endif
    #endregion

    #region Helpers
    /// <summary>
    /// Builds one fixed-size ECS reference selector while preserving a deterministic fallback. Empty or
    /// oversized selectors are replaced by the fallback so the runtime bridge never sees an invalid FixedString.
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
    /// Builds one runtime-safe Weapon Id from the authored value. Empty or oversized strings collapse to the
    /// empty FixedString sentinel used by runtime systems to detect "no attachment". Public wrapper used by the
    /// power-up bake utilities so the same normalisation rules apply to Switch Weapon payload IDs.
    /// </summary>
    /// <param name="weaponId">Authored Weapon Id.</param>
    /// <returns>Runtime-safe fixed-size Weapon Id.</returns>
    public static FixedString64Bytes BuildWeaponIdFixedString(string weaponId)
    {
        return BuildWeaponId(weaponId);
    }

    /// <summary>
    /// Internal Weapon Id normalisation used by both bridge-config baking and the public Switch Weapon wrapper.
    /// </summary>
    /// <param name="weaponId">Authored Weapon Id.</param>
    /// <returns>Runtime-safe fixed-size Weapon Id.</returns>
    private static FixedString64Bytes BuildWeaponId(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return default;

        string normalizedId = weaponId.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedId) > PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
            return default;

        return new FixedString64Bytes(normalizedId);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Maps normalized visual preset stat keys to runtime weapon visual fields. Handles the flat Base Gun
    /// reference, the Default Additional Weapon Id, and array-indexed mountable entry fields whose target ID
    /// is resolved from the authored array index.
    /// </summary>
    /// <param name="sourcePreset">Unscaled visual preset providing the mountable-weapons array.</param>
    /// <param name="statKey">Raw Add Scaling stat key.</param>
    /// <param name="property">Resolved target property whose current array index overrides stale key fallbacks.</param>
    /// <param name="fieldId">Resolved runtime field identifier.</param>
    /// <param name="targetEntryIndex">Resolved target array index when the field targets a mountable entry.</param>
    /// <returns>True when the key targets a supported weapon visual field.</returns>
    private static bool TryMapField(PlayerVisualPreset sourcePreset,
                                     string statKey,
                                     SerializedProperty property,
                                     out PlayerRuntimeWeaponVisualFieldId fieldId,
                                     out int targetEntryIndex)
    {
        fieldId = default;
        targetEntryIndex = -1;
        string normalizedStatKey = PlayerScalingStatKeyUtility.NormalizeStatKey(statKey);

        switch (normalizedStatKey)
        {
            case "weaponVisuals.baseGunReference":
                fieldId = PlayerRuntimeWeaponVisualFieldId.BaseGunReference;
                return true;
            case "weaponVisuals.defaultAdditionalWeaponId":
                fieldId = PlayerRuntimeWeaponVisualFieldId.DefaultAdditionalWeaponId;
                return true;
        }

        string normalizedPropertyPath = property != null
            ? PlayerScalingStatKeyUtility.NormalizeStatKey(property.propertyPath)
            : string.Empty;

        if (!TryExtractAdditionalWeaponEntryIndex(normalizedPropertyPath, out int entryIndex, out bool targetsReference))
            return false;

        PlayerWeaponVisualSettings settings = sourcePreset != null ? sourcePreset.WeaponVisuals : null;

        if (settings == null || settings.AdditionalWeapons == null)
            return false;

        if (entryIndex < 0 || entryIndex >= settings.AdditionalWeapons.Count)
            return false;

        PlayerAdditionalWeaponVisualEntry entry = settings.AdditionalWeapons[entryIndex];

        if (entry == null)
            return false;

        targetEntryIndex = entryIndex;
        fieldId = targetsReference
            ? PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponRuntimeReference
            : PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponWeaponId;
        return true;
    }

    /// <summary>
    /// Parses one normalized property path that targets the mountable weapons array and reports the array index plus
    /// whether the field is the per-entry runtime reference. Returns false for unrelated keys so the caller can
    /// short-circuit cleanly.
    /// </summary>
    /// <param name="normalizedPropertyPath">Property path already normalized by <see cref="PlayerScalingStatKeyUtility"/>.</param>
    /// <param name="entryIndex">Resolved array index when parsing succeeds.</param>
    /// <param name="targetsReference">True when the key targets the per-entry runtimeReference field.</param>
    /// <returns>True when the key targets one mountable weapons array element.</returns>
    private static bool TryExtractAdditionalWeaponEntryIndex(string normalizedPropertyPath,
                                                             out int entryIndex,
                                                             out bool targetsReference)
    {
        entryIndex = -1;
        targetsReference = false;

        if (string.IsNullOrWhiteSpace(normalizedPropertyPath))
            return false;

        if (!normalizedPropertyPath.StartsWith(AdditionalWeaponsPathSegment, System.StringComparison.Ordinal))
            return false;

        int closingBracketIndex = normalizedPropertyPath.IndexOf(']', AdditionalWeaponsPathSegment.Length);

        if (closingBracketIndex < 0)
            return false;

        string indexText = normalizedPropertyPath.Substring(AdditionalWeaponsPathSegment.Length,
                                                            closingBracketIndex - AdditionalWeaponsPathSegment.Length);
        int stableTokenSeparatorIndex = indexText.IndexOf('|');

        if (stableTokenSeparatorIndex > 0)
            indexText = indexText.Substring(0, stableTokenSeparatorIndex);

        if (!int.TryParse(indexText, out entryIndex))
            return false;

        string suffix = normalizedPropertyPath.Substring(closingBracketIndex);

        if (string.Equals(suffix, AdditionalRuntimeReferenceFieldSuffix, System.StringComparison.Ordinal))
        {
            targetsReference = true;
            return true;
        }

        return string.Equals(suffix, AdditionalWeaponIdFieldSuffix, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds one runtime scaling element from a supported weapon visual property. Only string token properties
    /// are supported, since every authored field on the new weapon visual surface is a string identifier.
    /// </summary>
    /// <param name="fieldId">Target runtime field identifier.</param>
    /// <param name="targetEntryIndex">Target array index when the rule targets a mountable entry.</param>
    /// <param name="scalingRule">Source Add Scaling rule.</param>
    /// <param name="property">Serialized source property targeted by the rule.</param>
    /// <param name="scalingElement">Built runtime scaling element.</param>
    /// <returns>True when the property can be represented by weapon visual runtime metadata.</returns>
    private static bool TryBuildScalingElement(PlayerRuntimeWeaponVisualFieldId fieldId,
                                                int targetEntryIndex,
                                                PlayerStatScalingRule scalingRule,
                                                SerializedProperty property,
                                                out PlayerRuntimeWeaponVisualScalingElement scalingElement)
    {
        scalingElement = default;

        if (property.propertyType != SerializedPropertyType.String)
            return false;

        string tokenValue = string.IsNullOrWhiteSpace(property.stringValue) ? string.Empty : property.stringValue.Trim();
        int maxBytes = fieldId == PlayerRuntimeWeaponVisualFieldId.BaseGunReference ||
                       fieldId == PlayerRuntimeWeaponVisualFieldId.AdditionalWeaponRuntimeReference
            ? PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes
            : PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes;

        if (Encoding.UTF8.GetByteCount(tokenValue) > maxBytes)
            return false;

        scalingElement = new PlayerRuntimeWeaponVisualScalingElement
        {
            FieldId = fieldId,
            TargetEntryIndex = targetEntryIndex,
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
