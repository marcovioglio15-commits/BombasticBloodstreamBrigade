using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds runtime-scaling baselines and formula metadata used to resynchronize ECS configs after scalable-stat changes.
/// </summary>
internal static class PlayerRuntimeScalingBakeUtility
{
    #region Constants
    private const string ActivePowerUpsRoot = "activePowerUps.Array.data[";
    private const string PassivePowerUpsRoot = "passivePowerUps.Array.data[";
    private const string ModuleDefinitionsRoot = "moduleDefinitions.Array.data[";
    private const string StealProtectedPayloadPath = "stealProtected";
    private const int MaximumFixedString64Utf8Bytes = 61;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Populates immutable and runtime progression phase buffers from progression presets.
    /// </summary>
    /// <param name="scaledPreset">Scaled preset currently used by bake.</param>
    /// <param name="sourcePreset">Unscaled source preset used as runtime baseline.</param>
    /// <param name="basePhases">Destination immutable baseline buffer.</param>
    /// <param name="runtimePhases">Destination runtime buffer initialized from the scaled preset.</param>
    public static void PopulateProgressionPhaseBuffers(PlayerProgressionPreset scaledPreset,
                                                       PlayerProgressionPreset sourcePreset,
                                                       DynamicBuffer<PlayerBaseGamePhaseElement> basePhases,
                                                       DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimePhases)
    {
        basePhases.Clear();
        runtimePhases.Clear();
        IReadOnlyList<PlayerGamePhaseDefinition> sourcePhases = sourcePreset != null ? sourcePreset.GamePhasesDefinition : null;
        IReadOnlyList<PlayerGamePhaseDefinition> scaledPhases = scaledPreset != null ? scaledPreset.GamePhasesDefinition : null;
        int phaseCount = math.max(sourcePhases != null ? sourcePhases.Count : 0, scaledPhases != null ? scaledPhases.Count : 0);

        for (int phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            PlayerGamePhaseDefinition sourcePhase = sourcePhases != null && phaseIndex < sourcePhases.Count ? sourcePhases[phaseIndex] : null;
            PlayerGamePhaseDefinition scaledPhase = scaledPhases != null && phaseIndex < scaledPhases.Count ? scaledPhases[phaseIndex] : null;
            int baseStartsAtLevel = sourcePhase != null ? math.max(0, sourcePhase.StartsAtLevel) : 0;
            float baseStartingExp = sourcePhase != null ? math.max(1f, sourcePhase.StartingRequiredLevelUpExp) : 100f;
            float baseGrowth = sourcePhase != null ? math.max(0f, sourcePhase.RequiredExperienceGrouth) : 0f;
            int runtimeStartsAtLevel = scaledPhase != null ? math.max(0, scaledPhase.StartsAtLevel) : baseStartsAtLevel;
            float runtimeStartingExp = scaledPhase != null ? math.max(1f, scaledPhase.StartingRequiredLevelUpExp) : baseStartingExp;
            float runtimeGrowth = scaledPhase != null ? math.max(0f, scaledPhase.RequiredExperienceGrouth) : baseGrowth;

            basePhases.Add(new PlayerBaseGamePhaseElement
            {
                StartsAtLevel = baseStartsAtLevel,
                StartingRequiredLevelUpExp = baseStartingExp,
                RequiredExperienceGrouth = baseGrowth
            });
            runtimePhases.Add(new PlayerRuntimeGamePhaseElement
            {
                StartsAtLevel = runtimeStartsAtLevel,
                StartingRequiredLevelUpExp = runtimeStartingExp,
                RequiredExperienceGrouth = runtimeGrowth
            });
        }
    }

    /// <summary>
    /// Populates immutable base configs for all modular power-ups so runtime scaling can rebuild active/passive snapshots.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="sourcePreset">Unscaled power-ups preset.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="baseConfigs">Destination immutable base config buffer.</param>
    /// <param name="resolveOrbitalProjectionPrefabBindingIndex">Optional resolver that stores orbital projection prefabs in a remappable binding table.</param>
    public static void PopulatePowerUpBaseConfigs(PlayerAuthoring authoring,
                                                  PlayerPowerUpsPreset sourcePreset,
                                                  Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                  DynamicBuffer<PlayerPowerUpBaseConfigElement> baseConfigs,
                                                  Func<GameObject, int> resolveOrbitalProjectionPrefabBindingIndex = null)
    {
        baseConfigs.Clear();

        if (sourcePreset == null)
            return;

        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = sourcePreset.ActivePowerUps;
        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = sourcePreset.PassivePowerUps;
        AppendPowerUpBaseConfigs(authoring,
                                 sourcePreset,
                                 activePowerUps,
                                 PlayerPowerUpUnlockKind.Active,
                                 resolveDynamicPrefabEntity,
                                 resolveOrbitalProjectionPrefabBindingIndex,
                                 baseConfigs);
        AppendPowerUpBaseConfigs(authoring,
                                 sourcePreset,
                                 passivePowerUps,
                                 PlayerPowerUpUnlockKind.Passive,
                                 resolveDynamicPrefabEntity,
                                 resolveOrbitalProjectionPrefabBindingIndex,
                                 baseConfigs);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates power-up scaling metadata from the unscaled power-ups preset.
    /// </summary>
    /// <param name="sourcePreset">Unscaled source power-ups preset.</param>
    /// <param name="scalingBuffer">Destination scaling metadata buffer.</param>
    public static void PopulatePowerUpScalingMetadata(PlayerPowerUpsPreset sourcePreset,
                                                      DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
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

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (TryAddDirectPowerUpScalingEntry(sourcePreset, scalingRule, property, scalingBuffer))
                continue;

            TryAddSharedModuleScalingEntries(sourcePreset, scalingRule, property, scalingBuffer);
        }
    }
#endif
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends immutable active or passive base configs used by runtime scaling rebuilds.
    /// </summary>
    /// <param name="authoring">Owning player authoring component.</param>
    /// <param name="sourcePreset">Unscaled power-ups preset used as the baseline.</param>
    /// <param name="powerUps">Power-up definitions to append.</param>
    /// <param name="unlockKind">Config kind represented by the power-up list.</param>
    /// <param name="resolveDynamicPrefabEntity">Prefab-to-entity resolver provided by the baker.</param>
    /// <param name="resolveOrbitalProjectionPrefabBindingIndex">Resolver that stores orbital projection prefabs in a remappable binding table.</param>
    /// <param name="baseConfigs">Destination immutable base config buffer.</param>
    private static void AppendPowerUpBaseConfigs(PlayerAuthoring authoring,
                                                 PlayerPowerUpsPreset sourcePreset,
                                                 IReadOnlyList<ModularPowerUpDefinition> powerUps,
                                                 PlayerPowerUpUnlockKind unlockKind,
                                                 Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                 Func<GameObject, int> resolveOrbitalProjectionPrefabBindingIndex,
                                                 DynamicBuffer<PlayerPowerUpBaseConfigElement> baseConfigs)
    {
        if (powerUps == null)
            return;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
                continue;

            int configIndex = baseConfigs.Length;
            baseConfigs.ResizeUninitialized(configIndex + 1);
            ref PlayerPowerUpBaseConfigElement element = ref baseConfigs.ElementAt(configIndex);

            element.PowerUpId = new FixedString64Bytes(powerUp.CommonData.PowerUpId.Trim());
            element.UnlockKind = unlockKind;
            element.StealProtected = powerUp.StealProtected ? (byte)1 : (byte)0;
            element.ActiveSlotConfig = default;
            element.PassiveToolConfig = default;

            if (unlockKind == PlayerPowerUpUnlockKind.Active)
                PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(authoring,
                                                                                 sourcePreset,
                                                                                 powerUp,
                                                                                 resolveDynamicPrefabEntity,
                                                                                 out element.ActiveSlotConfig,
                                                                                 resolveOrbitalProjectionPrefabBindingIndex);
            else
                PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(authoring,
                                                                                         sourcePreset,
                                                                                         powerUp,
                                                                                         resolveDynamicPrefabEntity,
                                                                                         out element.PassiveToolConfig,
                                                                                         resolveOrbitalProjectionPrefabBindingIndex);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Resolves numeric, boolean, and enum baseline metadata for Add Scaling fields that do not need token storage.
    /// </summary>
    /// <param name="property">Serialized property targeted by Add Scaling.</param>
    /// <param name="valueType">Runtime formula value type.</param>
    /// <param name="baseValue">Numeric base value when applicable.</param>
    /// <param name="baseBooleanValue">Boolean base value when applicable.</param>
    /// <param name="isInteger">True when numeric values should be rounded before assignment.</param>
    /// <returns>True when the serialized property can be converted to runtime scaling metadata.</returns>
    internal static bool TryResolveScalingBaseMetadata(SerializedProperty property,
                                                       out byte valueType,
                                                       out float baseValue,
                                                       out byte baseBooleanValue,
                                                       out byte isInteger)
    {
        return TryResolveScalingBaseMetadata(property,
                                             out valueType,
                                             out baseValue,
                                             out baseBooleanValue,
                                             out isInteger,
                                             out FixedString64Bytes unusedBaseTokenValue);
    }

    /// <summary>
    /// Resolves baseline metadata for Add Scaling fields, including token-backed string properties.
    /// </summary>
    /// <param name="property">Serialized property targeted by Add Scaling.</param>
    /// <param name="valueType">Runtime formula value type.</param>
    /// <param name="baseValue">Numeric base value when applicable.</param>
    /// <param name="baseBooleanValue">Boolean base value when applicable.</param>
    /// <param name="isInteger">True when numeric values should be rounded before assignment.</param>
    /// <param name="baseTokenValue">Token base value when applicable.</param>
    /// <returns>True when the serialized property can be converted to runtime scaling metadata.</returns>
    internal static bool TryResolveScalingBaseMetadata(SerializedProperty property,
                                                       out byte valueType,
                                                       out float baseValue,
                                                       out byte baseBooleanValue,
                                                       out byte isInteger,
                                                       out FixedString64Bytes baseTokenValue)
    {
        valueType = (byte)PlayerFormulaValueType.Invalid;
        baseValue = 0f;
        baseBooleanValue = 0;
        isInteger = 0;
        baseTokenValue = default;

        if (property == null)
            return false;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                valueType = (byte)PlayerFormulaValueType.Number;
                baseValue = property.intValue;
                isInteger = 1;
                return true;
            case SerializedPropertyType.Float:
                valueType = (byte)PlayerFormulaValueType.Number;
                baseValue = property.floatValue;
                return true;
            case SerializedPropertyType.Boolean:
                valueType = (byte)PlayerFormulaValueType.Boolean;
                baseValue = property.boolValue ? 1f : 0f;
                baseBooleanValue = property.boolValue ? (byte)1 : (byte)0;
                isInteger = 1;
                return true;
            case SerializedPropertyType.Enum:
                valueType = (byte)PlayerFormulaValueType.Number;
                baseValue = property.enumValueIndex;
                isInteger = 1;
                return true;
            case SerializedPropertyType.String:
                valueType = (byte)PlayerFormulaValueType.Token;
                string tokenValue = string.IsNullOrWhiteSpace(property.stringValue)
                    ? string.Empty
                    : property.stringValue.Trim();

                if (Encoding.UTF8.GetByteCount(tokenValue) > MaximumFixedString64Utf8Bytes)
                    return false;

                baseTokenValue = new FixedString64Bytes(tokenValue);
                return true;
            default:
                return false;
        }
    }

    internal static string ResolveStoredFormula(string formula, SerializedProperty property, ISet<string> allowedVariables)
    {
        return PlayerScalingFormulaEditorUtility.NormalizeFormulaForTarget(formula, property, allowedVariables);
    }

    private static bool TryAddDirectPowerUpScalingEntry(PlayerPowerUpsPreset sourcePreset,
                                                        PlayerStatScalingRule scalingRule,
                                                        SerializedProperty property,
                                                        DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        if (!TryResolveDirectPowerUpScalingKey(sourcePreset,
                                               scalingRule.StatKey,
                                               property,
                                               out PlayerPowerUpUnlockKind unlockKind,
                                               out string powerUpId,
                                               out string payloadPath))
        {
            return false;
        }

        if (!TryResolveScalingBaseMetadata(property,
                                           out byte valueType,
                                           out float baseValue,
                                           out byte baseBooleanValue,
                                           out byte isInteger,
                                           out FixedString64Bytes baseTokenValue))
        {
            return false;
        }

        scalingBuffer.Add(new PlayerRuntimePowerUpScalingElement
        {
            PowerUpId = new FixedString64Bytes(powerUpId),
            UnlockKind = unlockKind,
            PayloadPath = new FixedString128Bytes(payloadPath),
            ValueType = valueType,
            BaseValue = baseValue,
            BaseBooleanValue = baseBooleanValue,
            IsInteger = isInteger,
            BaseTokenValue = baseTokenValue,
            Formula = new FixedString512Bytes(ResolveStoredFormula(scalingRule.Formula, property, null))
        });
        return true;
    }

    private static bool TryResolveDirectPowerUpScalingKey(PlayerPowerUpsPreset sourcePreset,
                                                          string statKey,
                                                          SerializedProperty property,
                                                          out PlayerPowerUpUnlockKind unlockKind,
                                                          out string powerUpId,
                                                          out string payloadPath)
    {
        if (TryExtractPowerUpKey(statKey, out unlockKind, out powerUpId, out payloadPath))
            return true;

        return TryResolvePowerUpKeyFromPropertyPath(sourcePreset,
                                                    property != null ? property.propertyPath : string.Empty,
                                                    out unlockKind,
                                                    out powerUpId,
                                                    out payloadPath);
    }

    private static void TryAddSharedModuleScalingEntries(PlayerPowerUpsPreset sourcePreset,
                                                         PlayerStatScalingRule scalingRule,
                                                         SerializedProperty property,
                                                         DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        if (!TryExtractSharedModuleKey(scalingRule.StatKey, out string moduleId, out string payloadPath))
            return;

        if (!TryResolveScalingBaseMetadata(property,
                                           out byte valueType,
                                           out float baseValue,
                                           out byte baseBooleanValue,
                                           out byte isInteger,
                                           out FixedString64Bytes baseTokenValue))
        {
            return;
        }

        AddSharedModuleEntriesForPowerUps(sourcePreset.ActivePowerUps,
                                          PlayerPowerUpUnlockKind.Active,
                                          moduleId,
                                          payloadPath,
                                          valueType,
                                          baseValue,
                                          baseBooleanValue,
                                          baseTokenValue,
                                          isInteger != 0,
                                          ResolveStoredFormula(scalingRule.Formula, property, null),
                                          scalingBuffer);
        AddSharedModuleEntriesForPowerUps(sourcePreset.PassivePowerUps,
                                          PlayerPowerUpUnlockKind.Passive,
                                          moduleId,
                                          payloadPath,
                                          valueType,
                                          baseValue,
                                          baseBooleanValue,
                                          baseTokenValue,
                                          isInteger != 0,
                                          ResolveStoredFormula(scalingRule.Formula, property, null),
                                          scalingBuffer);
    }

    private static void AddSharedModuleEntriesForPowerUps(IReadOnlyList<ModularPowerUpDefinition> powerUps,
                                                          PlayerPowerUpUnlockKind unlockKind,
                                                          string moduleId,
                                                          string payloadPath,
                                                          byte valueType,
                                                          float baseValue,
                                                          byte baseBooleanValue,
                                                          FixedString64Bytes baseTokenValue,
                                                          bool isInteger,
                                                          string formula,
                                                          DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        if (powerUps == null ||
            string.IsNullOrWhiteSpace(moduleId) ||
            string.IsNullOrWhiteSpace(payloadPath))
        {
            return;
        }

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
                continue;

            IReadOnlyList<PowerUpModuleBinding> moduleBindings = powerUp.ModuleBindings;

            if (moduleBindings == null)
                continue;

            for (int bindingIndex = 0; bindingIndex < moduleBindings.Count; bindingIndex++)
            {
                PowerUpModuleBinding binding = moduleBindings[bindingIndex];

                if (binding == null || !binding.IsEnabled)
                    continue;

                if (!string.Equals(binding.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (binding.UseOverridePayload)
                    continue;

                scalingBuffer.Add(new PlayerRuntimePowerUpScalingElement
                {
                    PowerUpId = new FixedString64Bytes(powerUp.CommonData.PowerUpId.Trim()),
                    UnlockKind = unlockKind,
                    PayloadPath = new FixedString128Bytes(payloadPath),
                    ValueType = valueType,
                    BaseValue = baseValue,
                    BaseBooleanValue = baseBooleanValue,
                    BaseTokenValue = baseTokenValue,
                    IsInteger = isInteger ? (byte)1 : (byte)0,
                    Formula = new FixedString512Bytes(formula)
                });
                break;
            }
        }
    }

    private static bool TryResolvePowerUpKeyFromPropertyPath(PlayerPowerUpsPreset sourcePreset,
                                                             string propertyPath,
                                                             out PlayerPowerUpUnlockKind unlockKind,
                                                             out string powerUpId,
                                                             out string payloadPath)
    {
        unlockKind = default;
        powerUpId = string.Empty;
        payloadPath = string.Empty;

        if (sourcePreset == null || string.IsNullOrWhiteSpace(propertyPath))
            return false;

        IReadOnlyList<ModularPowerUpDefinition> powerUps;
        string root;

        if (propertyPath.StartsWith(ActivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Active;
            powerUps = sourcePreset.ActivePowerUps;
            root = ActivePowerUpsRoot;
        }
        else if (propertyPath.StartsWith(PassivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Passive;
            powerUps = sourcePreset.PassivePowerUps;
            root = PassivePowerUpsRoot;
        }
        else
        {
            return false;
        }

        if (!TryParsePowerUpArrayIndex(propertyPath, root, out int powerUpIndex))
            return false;

        if (powerUps == null || powerUpIndex < 0 || powerUpIndex >= powerUps.Count)
            return false;

        ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

        if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
            return false;

        string stealProtectedSuffix = "." + StealProtectedPayloadPath;

        if (propertyPath.EndsWith(stealProtectedSuffix, StringComparison.Ordinal))
        {
            powerUpId = powerUp.CommonData.PowerUpId.Trim();
            payloadPath = StealProtectedPayloadPath;
            return true;
        }

        string overridePrefix = ".overridePayload.";
        int payloadIndex = propertyPath.IndexOf(overridePrefix, StringComparison.Ordinal);

        if (payloadIndex < 0 || payloadIndex + overridePrefix.Length >= propertyPath.Length)
            return false;

        powerUpId = powerUp.CommonData.PowerUpId.Trim();
        payloadPath = propertyPath.Substring(payloadIndex + overridePrefix.Length);
        return !string.IsNullOrWhiteSpace(payloadPath);
    }

    private static bool TryParsePowerUpArrayIndex(string propertyPath, string root, out int powerUpIndex)
    {
        powerUpIndex = -1;

        if (string.IsNullOrWhiteSpace(propertyPath) || string.IsNullOrWhiteSpace(root))
            return false;

        if (!propertyPath.StartsWith(root, StringComparison.Ordinal))
            return false;

        int indexStart = root.Length;
        int indexEnd = propertyPath.IndexOf(']', indexStart);

        if (indexEnd <= indexStart)
            return false;

        string indexText = propertyPath.Substring(indexStart, indexEnd - indexStart);
        return int.TryParse(indexText, out powerUpIndex);
    }

    private static bool TryExtractSharedModuleKey(string statKey,
                                                  out string moduleId,
                                                  out string payloadPath)
    {
        moduleId = string.Empty;
        payloadPath = string.Empty;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        if (!statKey.StartsWith(ModuleDefinitionsRoot, StringComparison.Ordinal))
            return false;

        int moduleIdTokenIndex = statKey.IndexOf("moduleId:", StringComparison.Ordinal);

        if (moduleIdTokenIndex < 0)
            return false;

        int moduleIdEndIndex = statKey.IndexOf(']', moduleIdTokenIndex);

        if (moduleIdEndIndex <= moduleIdTokenIndex)
            return false;

        moduleId = statKey.Substring(moduleIdTokenIndex + 9, moduleIdEndIndex - moduleIdTokenIndex - 9).Trim();

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        int dataPathIndex = statKey.IndexOf(".data.", StringComparison.Ordinal);

        if (dataPathIndex < 0 || dataPathIndex + 6 >= statKey.Length)
            return false;

        payloadPath = statKey.Substring(dataPathIndex + 6);
        return !string.IsNullOrWhiteSpace(payloadPath);
    }

    private static bool TryExtractPowerUpKey(string statKey,
                                             out PlayerPowerUpUnlockKind unlockKind,
                                             out string powerUpId,
                                             out string payloadPath)
    {
        unlockKind = default;
        powerUpId = string.Empty;
        payloadPath = string.Empty;

        if (statKey.StartsWith(ActivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Active;
        }
        else if (statKey.StartsWith(PassivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Passive;
        }
        else
        {
            return false;
        }

        int powerUpIdIndex = statKey.IndexOf("powerUpId:", StringComparison.Ordinal);

        if (powerUpIdIndex < 0)
            return false;

        int powerUpIdEndIndex = statKey.IndexOf(']', powerUpIdIndex);

        if (powerUpIdEndIndex <= powerUpIdIndex)
            return false;

        powerUpId = statKey.Substring(powerUpIdIndex + 10, powerUpIdEndIndex - powerUpIdIndex - 10).Trim();

        if (string.IsNullOrWhiteSpace(powerUpId))
            return false;

        string stealProtectedSuffix = "." + StealProtectedPayloadPath;

        if (statKey.EndsWith(stealProtectedSuffix, StringComparison.Ordinal))
        {
            payloadPath = StealProtectedPayloadPath;
            return true;
        }

        string overridePrefix = ".overridePayload.";
        int payloadIndex = statKey.IndexOf(overridePrefix, StringComparison.Ordinal);

        if (payloadIndex < 0 || payloadIndex + overridePrefix.Length >= statKey.Length)
            return false;

        payloadPath = statKey.Substring(payloadIndex + overridePrefix.Length);
        return true;
    }
#endif
    #endregion

    #endregion
}
