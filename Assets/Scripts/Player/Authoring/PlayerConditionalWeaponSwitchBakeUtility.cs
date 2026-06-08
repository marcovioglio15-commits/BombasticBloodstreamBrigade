using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds the baked conditional weapon switch entry and condition buffers from one authored
/// <see cref="PlayerConditionalWeaponSwitchSettings"/> instance. Every authored entry is preserved in order so
/// runtime tie-breaks remain deterministic; per-entry conditions are flattened into a single shared buffer to
/// avoid runtime indirection lookups against nested arrays.
/// </summary>
public static class PlayerConditionalWeaponSwitchBakeUtility
{
    #region Constants
    private const byte MaximumEntryCount = 64;
    private const int MaximumStatNameUtf8Bytes = 60;
#if UNITY_EDITOR
    private const string EntriesPathPrefix = "shootingSettings.conditionalWeaponSwitches.entries.Array.data[";
    private const string ConditionsPathSegment = ".conditions.Array.data[";
#endif
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the conditional weapon switch config component built from authored entries. The summary count
    /// gates the runtime evaluation so a player with no authored entries skips the evaluator entirely.
    /// </summary>
    /// <param name="settings">Authored conditional weapon switch settings, or null when the controller preset has none.</param>
    /// <returns>Runtime-safe config component.</returns>
    public static PlayerConditionalWeaponSwitchConfig BuildConfig(PlayerConditionalWeaponSwitchSettings settings)
    {
        return new PlayerConditionalWeaponSwitchConfig
        {
            EntryCount = ResolveEntryCount(settings)
        };
    }

    /// <summary>
    /// Populates the runtime entry and condition buffers from the authored settings. Both buffers are cleared
    /// first so re-bake never duplicates rows. The flattened condition buffer holds entries in the same order
    /// as the entry buffer, with each entry pointing at its slice via Condition Start Index and Condition Count.
    /// </summary>
    /// <param name="settings">Authored conditional weapon switch settings, or null when the controller preset has none.</param>
    /// <param name="entryBuffer">Destination ECS entry buffer.</param>
    /// <param name="conditionBuffer">Destination ECS condition buffer.</param>
    public static void PopulateBuffers(PlayerConditionalWeaponSwitchSettings settings,
                                       DynamicBuffer<PlayerConditionalWeaponSwitchEntryElement> entryBuffer,
                                       DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditionBuffer)
    {
        entryBuffer.Clear();
        conditionBuffer.Clear();

        if (settings == null || settings.Entries == null || settings.Entries.Count <= 0)
            return;

        IReadOnlyList<PlayerConditionalWeaponSwitchEntry> entries = settings.Entries;
        int authoredEntryCount = math.min(entries.Count, MaximumEntryCount);

        for (int entryIndex = 0; entryIndex < authoredEntryCount; entryIndex++)
        {
            PlayerConditionalWeaponSwitchEntry entry = entries[entryIndex];

            if (entry == null)
            {
                entryBuffer.Add(default);
                continue;
            }

            int conditionStartIndex = conditionBuffer.Length;
            byte sufficientGroupCount = AppendConditions(entry.Conditions, conditionBuffer);
            int conditionCount = conditionBuffer.Length - conditionStartIndex;
            entryBuffer.Add(new PlayerConditionalWeaponSwitchEntryElement
            {
                WeaponId = PlayerWeaponVisualBakeUtility.BuildWeaponIdFixedString(entry.WeaponId),
                Priority = entry.Priority,
                ConditionStartIndex = conditionStartIndex,
                ConditionCount = conditionCount,
                OverridePowerUpSwitch = entry.OverridePowerUpSwitch ? (byte)1 : (byte)0,
                SufficientGroupCount = sufficientGroupCount
            });
        }
    }

    /// <summary>
    /// Populates immutable conditional weapon switch baseline buffers from the unscaled source preset settings.
    /// </summary>
    /// <param name="settings">Unscaled authored conditional weapon switch settings.</param>
    /// <param name="entryBuffer">Destination immutable entry buffer.</param>
    /// <param name="conditionBuffer">Destination immutable flattened condition buffer.</param>
    public static void PopulateBaseBuffers(PlayerConditionalWeaponSwitchSettings settings,
                                           DynamicBuffer<PlayerBaseConditionalWeaponSwitchEntryElement> entryBuffer,
                                           DynamicBuffer<PlayerBaseConditionalWeaponSwitchConditionElement> conditionBuffer)
    {
        entryBuffer.Clear();
        conditionBuffer.Clear();

        if (settings == null || settings.Entries == null || settings.Entries.Count <= 0)
            return;

        IReadOnlyList<PlayerConditionalWeaponSwitchEntry> entries = settings.Entries;
        int authoredEntryCount = math.min(entries.Count, MaximumEntryCount);

        for (int entryIndex = 0; entryIndex < authoredEntryCount; entryIndex++)
        {
            PlayerConditionalWeaponSwitchEntry entry = entries[entryIndex];

            if (entry == null)
            {
                entryBuffer.Add(default);
                continue;
            }

            int conditionStartIndex = conditionBuffer.Length;
            byte sufficientGroupCount = AppendBaseConditions(entry.Conditions, conditionBuffer);
            entryBuffer.Add(new PlayerBaseConditionalWeaponSwitchEntryElement
            {
                WeaponId = PlayerWeaponVisualBakeUtility.BuildWeaponIdFixedString(entry.WeaponId),
                Priority = entry.Priority,
                ConditionStartIndex = conditionStartIndex,
                ConditionCount = conditionBuffer.Length - conditionStartIndex,
                OverridePowerUpSwitch = entry.OverridePowerUpSwitch ? (byte)1 : (byte)0,
                SufficientGroupCount = sufficientGroupCount
            });
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates conditional weapon switch runtime scaling metadata from nested Add Scaling rules.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source controller preset.</param>
    /// <param name="scalingBuffer">Destination conditional weapon switch scaling metadata buffer.</param>
    public static void PopulateScalingMetadata(PlayerControllerPreset sourcePreset,
                                               DynamicBuffer<PlayerRuntimeConditionalWeaponSwitchScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        PlayerConditionalWeaponSwitchSettings settings = sourcePreset.ShootingSettings != null
            ? sourcePreset.ShootingSettings.ConditionalWeaponSwitches
            : null;

        if (settings == null)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling || string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset,
                                                                      scalingRule.StatKey,
                                                                      out SerializedProperty property))
            {
                continue;
            }

            if (!TryMapScalingTarget(property.propertyPath,
                                     settings,
                                     out PlayerRuntimeConditionalWeaponSwitchFieldId fieldId,
                                     out int entryIndex,
                                     out int conditionIndex))
            {
                continue;
            }

            if (!PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                               out byte valueType,
                                                                               out float baseValue,
                                                                               out byte baseBooleanValue,
                                                                               out byte isInteger,
                                                                               out FixedString64Bytes baseTokenValue))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeConditionalWeaponSwitchScalingElement
            {
                FieldId = fieldId,
                TargetEntryIndex = entryIndex,
                TargetConditionIndex = conditionIndex,
                ValueType = valueType,
                BaseValue = baseValue,
                BaseBooleanValue = baseBooleanValue,
                IsInteger = isInteger,
                BaseTokenValue = baseTokenValue,
                Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                        property,
                                                                                                        null))
            });
        }
    }
#endif

    /// <summary>
    /// Builds the neutral conditional weapon switch state used at bake time so the animator presentation
    /// utility never has to special-case a missing state component.
    /// </summary>
    /// <returns>Neutral conditional weapon switch state.</returns>
    public static PlayerConditionalWeaponSwitchState BuildInitialState()
    {
        return new PlayerConditionalWeaponSwitchState
        {
            WeaponId = default,
            MatchedPriority = int.MinValue,
            LastEvaluatedScalableStatsHash = 0u,
            HasMatch = 0,
            OverridesPowerUpSwitch = 0,
            Initialized = 0
        };
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Appends one entry condition slice into the shared condition buffer and reports how many conditions
    /// belong to the Sufficient or Necessary And Sufficient classes so the evaluator can short-circuit entries
    /// whose sufficiency group is empty.
    /// </summary>
    /// <param name="conditions">Authored entry conditions, or null when the entry has no gates.</param>
    /// <param name="conditionBuffer">Destination flattened ECS condition buffer.</param>
    /// <returns>Number of sufficient-class conditions written for this entry, clamped to byte for compact storage.</returns>
    private static byte AppendConditions(IReadOnlyList<PlayerConditionalWeaponSwitchCondition> conditions,
                                         DynamicBuffer<PlayerConditionalWeaponSwitchConditionElement> conditionBuffer)
    {
        if (conditions == null || conditions.Count <= 0)
            return 0;

        int sufficientGroupCount = 0;

        for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
        {
            PlayerConditionalWeaponSwitchCondition condition = conditions[conditionIndex];

            if (condition == null)
            {
                conditionBuffer.Add(default);
                continue;
            }

            ResolveConditionValues(condition,
                                   out FixedString64Bytes statName,
                                   out float minimumValue,
                                   out float maximumValue,
                                   out byte requirement);

            if (requirement != (byte)PlayerConditionalWeaponSwitchConditionRequirement.Necessary)
                sufficientGroupCount++;

            conditionBuffer.Add(new PlayerConditionalWeaponSwitchConditionElement
            {
                StatName = statName,
                MinimumValue = minimumValue,
                MaximumValue = maximumValue,
                Requirement = requirement
            });
        }

        return (byte)math.min(byte.MaxValue, sufficientGroupCount);
    }

    /// <summary>
    /// Appends one authored condition slice to the immutable baseline condition buffer.
    /// </summary>
    /// <param name="conditions">Authored entry conditions.</param>
    /// <param name="conditionBuffer">Destination immutable flattened condition buffer.</param>
    /// <returns>Number of sufficient-class conditions written for the entry.</returns>
    private static byte AppendBaseConditions(IReadOnlyList<PlayerConditionalWeaponSwitchCondition> conditions,
                                             DynamicBuffer<PlayerBaseConditionalWeaponSwitchConditionElement> conditionBuffer)
    {
        if (conditions == null || conditions.Count <= 0)
            return 0;

        int sufficientGroupCount = 0;

        for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
        {
            PlayerConditionalWeaponSwitchCondition condition = conditions[conditionIndex];

            if (condition == null)
            {
                conditionBuffer.Add(default);
                continue;
            }

            ResolveConditionValues(condition,
                                   out FixedString64Bytes statName,
                                   out float minimumValue,
                                   out float maximumValue,
                                   out byte requirement);

            if (requirement != (byte)PlayerConditionalWeaponSwitchConditionRequirement.Necessary)
                sufficientGroupCount++;

            conditionBuffer.Add(new PlayerBaseConditionalWeaponSwitchConditionElement
            {
                StatName = statName,
                MinimumValue = minimumValue,
                MaximumValue = maximumValue,
                Requirement = requirement
            });
        }

        return (byte)math.min(byte.MaxValue, sufficientGroupCount);
    }

    /// <summary>
    /// Resolves one authored condition into finite, runtime-safe values shared by baseline and runtime buffers.
    /// </summary>
    /// <param name="condition">Authored condition to resolve.</param>
    /// <param name="statName">Runtime-safe scalable stat name.</param>
    /// <param name="minimumValue">Finite inclusive minimum.</param>
    /// <param name="maximumValue">Finite inclusive maximum.</param>
    /// <param name="requirement">Supported runtime requirement value.</param>
    private static void ResolveConditionValues(PlayerConditionalWeaponSwitchCondition condition,
                                               out FixedString64Bytes statName,
                                               out float minimumValue,
                                               out float maximumValue,
                                               out byte requirement)
    {
        string normalizedStatName = string.IsNullOrWhiteSpace(condition.StatName)
            ? string.Empty
            : condition.StatName.Trim();
        statName = BuildStatNameFixedString(normalizedStatName);
        minimumValue = math.isfinite(condition.MinimumValue) ? condition.MinimumValue : 0f;
        maximumValue = math.isfinite(condition.MaximumValue) ? condition.MaximumValue : 0f;
        requirement = (byte)ResolveRequirement(condition.Requirement);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Maps one nested serialized property path to its runtime field and stable authored array indices.
    /// </summary>
    /// <param name="propertyPath">Raw serialized property path.</param>
    /// <param name="settings">Source settings used to validate resolved indices.</param>
    /// <param name="fieldId">Resolved runtime field identifier.</param>
    /// <param name="entryIndex">Resolved entry array index.</param>
    /// <param name="conditionIndex">Resolved condition-local array index, or -1 for entry fields.</param>
    /// <returns>True when the path targets a supported nested scalable field.</returns>
    private static bool TryMapScalingTarget(string propertyPath,
                                            PlayerConditionalWeaponSwitchSettings settings,
                                            out PlayerRuntimeConditionalWeaponSwitchFieldId fieldId,
                                            out int entryIndex,
                                            out int conditionIndex)
    {
        fieldId = default;
        entryIndex = -1;
        conditionIndex = -1;

        if (!TryExtractArrayIndex(propertyPath, EntriesPathPrefix, 0, out entryIndex, out int entryClosingBracketIndex))
            return false;

        if (settings.Entries == null || entryIndex < 0 || entryIndex >= settings.Entries.Count)
            return false;

        string entrySuffix = propertyPath.Substring(entryClosingBracketIndex + 1);

        switch (entrySuffix)
        {
            case ".weaponId":
                fieldId = PlayerRuntimeConditionalWeaponSwitchFieldId.EntryWeaponId;
                return true;
            case ".priority":
                fieldId = PlayerRuntimeConditionalWeaponSwitchFieldId.EntryPriority;
                return true;
            case ".overridePowerUpSwitch":
                fieldId = PlayerRuntimeConditionalWeaponSwitchFieldId.EntryOverridePowerUpSwitch;
                return true;
        }

        if (!TryExtractArrayIndex(propertyPath,
                                  ConditionsPathSegment,
                                  entryClosingBracketIndex + 1,
                                  out conditionIndex,
                                  out int conditionClosingBracketIndex))
        {
            return false;
        }

        PlayerConditionalWeaponSwitchEntry entry = settings.Entries[entryIndex];

        if (entry == null || entry.Conditions == null || conditionIndex < 0 || conditionIndex >= entry.Conditions.Count)
            return false;

        string conditionSuffix = propertyPath.Substring(conditionClosingBracketIndex + 1);

        switch (conditionSuffix)
        {
            case ".minimumValue":
                fieldId = PlayerRuntimeConditionalWeaponSwitchFieldId.ConditionMinimumValue;
                return true;
            case ".maximumValue":
                fieldId = PlayerRuntimeConditionalWeaponSwitchFieldId.ConditionMaximumValue;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Extracts one Unity serialized array index following the supplied path segment.
    /// </summary>
    /// <param name="propertyPath">Raw serialized property path.</param>
    /// <param name="pathSegment">Array path segment ending immediately before the numeric index.</param>
    /// <param name="searchStartIndex">First path character eligible for matching.</param>
    /// <param name="arrayIndex">Parsed array index.</param>
    /// <param name="closingBracketIndex">Index of the parsed closing bracket.</param>
    /// <returns>True when a non-negative array index was parsed.</returns>
    private static bool TryExtractArrayIndex(string propertyPath,
                                             string pathSegment,
                                             int searchStartIndex,
                                             out int arrayIndex,
                                             out int closingBracketIndex)
    {
        arrayIndex = -1;
        closingBracketIndex = -1;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return false;

        int segmentIndex = propertyPath.IndexOf(pathSegment, searchStartIndex, System.StringComparison.Ordinal);

        if (segmentIndex < 0)
            return false;

        int indexStart = segmentIndex + pathSegment.Length;
        closingBracketIndex = propertyPath.IndexOf(']', indexStart);

        if (closingBracketIndex <= indexStart)
            return false;

        return int.TryParse(propertyPath.Substring(indexStart, closingBracketIndex - indexStart), out arrayIndex) &&
               arrayIndex >= 0;
    }
#endif

    /// <summary>
    /// Reports how many authored entries the bake will actually emit, clamped to the runtime ceiling so the
    /// config component stays within a single byte without truncating real authored data unexpectedly.
    /// </summary>
    /// <param name="settings">Authored conditional weapon switch settings, or null.</param>
    /// <returns>Clamped entry count.</returns>
    private static byte ResolveEntryCount(PlayerConditionalWeaponSwitchSettings settings)
    {
        if (settings == null || settings.Entries == null)
            return 0;

        return (byte)math.min(settings.Entries.Count, MaximumEntryCount);
    }

    /// <summary>
    /// Maps the authored requirement enum to one supported runtime value. Unknown enum values fall back to
    /// Necessary And Sufficient so a misconfigured asset produces deterministic behavior at runtime.
    /// </summary>
    /// <param name="requirement">Authored requirement value.</param>
    /// <returns>Runtime-supported requirement value.</returns>
    private static PlayerConditionalWeaponSwitchConditionRequirement ResolveRequirement(PlayerConditionalWeaponSwitchConditionRequirement requirement)
    {
        switch (requirement)
        {
            case PlayerConditionalWeaponSwitchConditionRequirement.Sufficient:
            case PlayerConditionalWeaponSwitchConditionRequirement.Necessary:
            case PlayerConditionalWeaponSwitchConditionRequirement.NecessaryAndSufficient:
                return requirement;

            default:
                return PlayerConditionalWeaponSwitchConditionRequirement.NecessaryAndSufficient;
        }
    }

    /// <summary>
    /// Builds one runtime-safe scalable stat reference. Empty and oversized names collapse to the empty
    /// FixedString sentinel so the runtime can short-circuit cleanly without throwing fixed-string overflows.
    /// </summary>
    /// <param name="statName">Authored stat name.</param>
    /// <returns>Runtime-safe fixed-size stat name.</returns>
    private static FixedString64Bytes BuildStatNameFixedString(string statName)
    {
        if (string.IsNullOrWhiteSpace(statName))
            return default;

        if (Encoding.UTF8.GetByteCount(statName) > MaximumStatNameUtf8Bytes)
            return default;

        return new FixedString64Bytes(statName);
    }
    #endregion

    #endregion
}
