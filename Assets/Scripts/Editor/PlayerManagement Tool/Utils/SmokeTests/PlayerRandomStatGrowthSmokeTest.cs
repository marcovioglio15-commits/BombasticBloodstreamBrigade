using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates Engineered Growth authoring, bake, unified scaling, initialization, and permanent runtime rewards.
/// </summary>
public static class PlayerRandomStatGrowthSmokeTest
{
    #region Constants
    private const string MainPresetPath = "Assets/Scriptable Objects/Player/Power-Ups/PlayerPowerUpsPreset.asset";
    private const string ProgressionPresetPath = "Assets/Scriptable Objects/Player/Progression/PlayerProgressionPreset.asset";
    private const string UiVisualPresetPath = "Assets/Scriptable Objects/Player/UI Visual/PlayerUiVisualPreset_A.asset";
    private const string EngineeredGrowthPowerUpId = "ActiveEngineeredGrowth";
    private const string ResourceGateModuleId = "Module_GateResource";
    private const string RandomStatGrowthModuleId = "Module_RandomStatGrowth";
    private const float PrecisionEpsilon = 0.0001f;
    private const int NativeCandidateCount = 14;
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Player/Run Random Stat Growth Smoke Test")]
    /// <summary>
    /// Runs deterministic checks against the authored Engineered Growth content and its runtime utilities.
    /// </summary>
    public static void Run()
    {
        PlayerPowerUpsPreset powerUpsPreset = AssetDatabase.LoadAssetAtPath<PlayerPowerUpsPreset>(MainPresetPath);
        PlayerProgressionPreset progressionPreset = AssetDatabase.LoadAssetAtPath<PlayerProgressionPreset>(ProgressionPresetPath);
        PlayerUiVisualPreset uiVisualPreset = AssetDatabase.LoadAssetAtPath<PlayerUiVisualPreset>(UiVisualPresetPath);

        if (powerUpsPreset == null || progressionPreset == null || uiVisualPreset == null)
            throw new InvalidOperationException("Random Stat Growth smoke testing requires the main power-up, progression, and UI visual presets.");

        ValidateEngineeredGrowthBake(powerUpsPreset, progressionPreset);
        ValidateAuthoringScalingTargets(powerUpsPreset);
        ValidateGrowthSequencePresentationOption(uiVisualPreset);
        ValidateRuntimeScalingPaths();
        ValidatePermanentRuntimeRewards();
        Debug.Log("[PlayerRandomStatGrowthSmokeTest] All Random Stat Growth checks passed.");
    }
    #endregion

    #region Preset and Bake
    /// <summary>
    /// Confirms that Engineered Growth contains the maximum meaningful pool and exact requested resource settings.
    /// </summary>
    /// <param name="powerUpsPreset">Main power-up preset containing Engineered Growth.</param>
    /// <param name="progressionPreset">Progression preset supplying numeric custom stats.</param>
    private static void ValidateEngineeredGrowthBake(PlayerPowerUpsPreset powerUpsPreset,
                                                     PlayerProgressionPreset progressionPreset)
    {
        ModularPowerUpDefinition powerUp = FindPowerUp(powerUpsPreset.ActivePowerUps,
                                                       EngineeredGrowthPowerUpId);

        if (powerUp == null)
            throw new InvalidOperationException("Engineered Growth is missing from the main power-up preset.");

        PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(null,
                                                                         powerUpsPreset,
                                                                         powerUp,
                                                                         prefab => Entity.Null,
                                                                         out PlayerPowerUpSlotConfig slotConfig);
        int expectedCandidateCount = NativeCandidateCount + CountNumericStats(progressionPreset.ScalableStats);

        if (slotConfig.IsDefined == 0 ||
            slotConfig.ToolKind != ActiveToolKind.RandomStatGrowth ||
            slotConfig.RandomStatGrowthEntries.Length != expectedCandidateCount ||
            slotConfig.UseWeightedRandomStatGrowthSelection == 0 ||
            slotConfig.HasResourceGate == 0 ||
            slotConfig.Toggleable != 0 ||
            slotConfig.ChargeType != PowerUpChargeType.EnemiesDestroyed ||
            math.abs(slotConfig.InitialEnergy) > PrecisionEpsilon ||
            math.abs(slotConfig.MaximumEnergy - 100f) > PrecisionEpsilon ||
            math.abs(slotConfig.ActivationCost - 50f) > PrecisionEpsilon ||
            math.abs(slotConfig.ChargePerTrigger - 10f) > PrecisionEpsilon)
        {
            throw new InvalidOperationException("Engineered Growth did not bake with its complete statistic pool and 0/100/50/+10 kill-energy contract.");
        }

        for (int entryIndex = 0; entryIndex < slotConfig.RandomStatGrowthEntries.Length; entryIndex++)
        {
            PlayerRandomStatGrowthEntryConfig entry = slotConfig.RandomStatGrowthEntries[entryIndex];

            if (math.abs(entry.SelectionWeight - 1f) > PrecisionEpsilon ||
                entry.UseCustomPresentationColor == 0 ||
                !math.all(math.isfinite(entry.PresentationColor)))
            {
                throw new InvalidOperationException("Engineered Growth candidates are missing their unit weights or custom presentation colors.");
            }
        }

    }

    /// <summary>
    /// Counts Float, Integer, and Unsigned progression stats eligible for additive permanent growth.
    /// </summary>
    /// <param name="scalableStats">Progression stat catalog to inspect.</param>
    /// <returns>Number of numeric scalable stats.</returns>
    private static int CountNumericStats(IReadOnlyList<PlayerScalableStatDefinition> scalableStats)
    {
        int count = 0;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];

            if (scalableStat == null)
                continue;

            switch (scalableStat.StatType)
            {
                case PlayerScalableStatType.Float:
                case PlayerScalableStatType.Integer:
                case PlayerScalableStatType.Unsigned:
                    count++;
                    break;
            }
        }

        return count;
    }

    /// <summary>
    /// Finds an active power-up by stable identifier.
    /// </summary>
    /// <param name="powerUps">Active power-up catalog to inspect.</param>
    /// <param name="powerUpId">Stable identifier to match.</param>
    /// <returns>The matching power-up, or null when absent.</returns>
    private static ModularPowerUpDefinition FindPowerUp(IReadOnlyList<ModularPowerUpDefinition> powerUps,
                                                        string powerUpId)
    {
        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp != null &&
                powerUp.CommonData != null &&
                string.Equals(powerUp.CommonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
            {
                return powerUp;
            }
        }

        return null;
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Verifies Add Scaling support for the resource gate and every Random Stat Growth entry field type.
    /// </summary>
    /// <param name="powerUpsPreset">Main preset used to inspect the Engineered Growth override payload.</param>
    private static void ValidateAuthoringScalingTargets(PlayerPowerUpsPreset powerUpsPreset)
    {
        SerializedObject serializedPreset = new SerializedObject(powerUpsPreset);
        SerializedProperty activePowerUps = serializedPreset.FindProperty("activePowerUps");
        SerializedProperty resourceGate = null;
        SerializedProperty randomStatGrowth = null;

        // Locate both override payloads without relying on their current list positions.
        for (int powerUpIndex = 0; powerUpIndex < activePowerUps.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUp = activePowerUps.GetArrayElementAtIndex(powerUpIndex);

            if (powerUp.FindPropertyRelative("commonData").FindPropertyRelative("powerUpId").stringValue !=
                EngineeredGrowthPowerUpId)
            {
                continue;
            }

            SerializedProperty bindings = powerUp.FindPropertyRelative("moduleBindings");

            for (int bindingIndex = 0; bindingIndex < bindings.arraySize; bindingIndex++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(bindingIndex);
                string moduleId = binding.FindPropertyRelative("moduleId").stringValue;
                SerializedProperty payload = binding.FindPropertyRelative("overridePayload");

                switch (moduleId)
                {
                    case ResourceGateModuleId:
                        resourceGate = payload.FindPropertyRelative("resourceGate");
                        break;
                    case RandomStatGrowthModuleId:
                        randomStatGrowth = payload.FindPropertyRelative("randomStatGrowth");
                        break;
                }
            }

            break;
        }

        if (resourceGate == null || randomStatGrowth == null)
            throw new InvalidOperationException("Engineered Growth override payloads are missing from serialized authoring data.");

        SerializedProperty entries = randomStatGrowth.FindPropertyRelative("entries");
        SerializedProperty useWeightedSelection = randomStatGrowth.FindPropertyRelative("useWeightedSelection");
        SerializedProperty firstEntry = entries.arraySize > 0 ? entries.GetArrayElementAtIndex(0) : null;
        SerializedProperty presentationColor = firstEntry != null
            ? firstEntry.FindPropertyRelative("presentationColor")
            : null;

        if (!SupportsScaling(resourceGate.FindPropertyRelative("initialEnergy")) ||
            !SupportsScaling(useWeightedSelection) ||
            firstEntry == null ||
            !SupportsScaling(firstEntry.FindPropertyRelative("target")) ||
            !SupportsScaling(firstEntry.FindPropertyRelative("customScalableStatName")) ||
            !SupportsScaling(firstEntry.FindPropertyRelative("minimumIncrease")) ||
            !SupportsScaling(firstEntry.FindPropertyRelative("maximumIncrease")) ||
            !SupportsScaling(firstEntry.FindPropertyRelative("selectionWeight")) ||
            !SupportsScaling(firstEntry.FindPropertyRelative("useCustomPresentationColor")) ||
            presentationColor == null ||
            !SupportsScaling(presentationColor.FindPropertyRelative("r")))
        {
            throw new InvalidOperationException("Random Stat Growth or initial-energy authoring fields are missing unified Add Scaling support.");
        }
    }

    /// <summary>
    /// Checks whether one serialized property is eligible for unified Add Scaling controls.
    /// </summary>
    /// <param name="property">Serialized property to validate.</param>
    /// <returns>True when the unified scaling editor accepts the field type.</returns>
    private static bool SupportsScaling(SerializedProperty property)
    {
        return property != null && PlayerScalingFormulaEditorUtility.SupportsScalingTarget(property);
    }

    /// <summary>
    /// Verifies that the level-up overhead presentation option is scalable and reaches both mutable and baseline ECS configs.
    /// </summary>
    /// <param name="uiVisualPreset">UI visual preset containing the Growth Sequence settings.</param>
    private static void ValidateGrowthSequencePresentationOption(PlayerUiVisualPreset uiVisualPreset)
    {
        SerializedObject serializedPreset = new SerializedObject(uiVisualPreset);
        SerializedProperty growthSequence = serializedPreset.FindProperty("growthSequence");
        SerializedProperty presentationOption = growthSequence != null
            ? growthSequence.FindPropertyRelative("showLevelUpStatGrowthAbovePlayer")
            : null;
        SerializedProperty presentationColor = growthSequence != null
            ? growthSequence.FindPropertyRelative("levelUpStatGrowthColor")
            : null;
        SerializedProperty perStatColors = growthSequence != null
            ? growthSequence.FindPropertyRelative("usePerStatLevelUpGrowthColors")
            : null;
        SerializedProperty schedules = growthSequence != null
            ? growthSequence.FindPropertyRelative("schedules")
            : null;
        SerializedProperty firstSteps = schedules != null && schedules.arraySize > 0
            ? schedules.GetArrayElementAtIndex(0).FindPropertyRelative("steps")
            : null;
        SerializedProperty firstStep = firstSteps != null && firstSteps.arraySize > 0
            ? firstSteps.GetArrayElementAtIndex(0)
            : null;
        SerializedProperty stepColor = firstStep != null
            ? firstStep.FindPropertyRelative("levelUpGrowthColor")
            : null;
        PlayerGrowthSequenceHudVisualConfig runtimeConfig = PlayerHudGrowthSequenceVisualBakeUtility.BuildGrowthSequenceConfig(uiVisualPreset);
        PlayerBaseGrowthSequenceHudVisualConfig baseConfig = PlayerHudGrowthSequenceVisualBakeUtility.BuildBaseGrowthSequenceConfig(uiVisualPreset);

        if (!SupportsScaling(presentationOption) ||
            presentationColor == null ||
            !SupportsScaling(presentationColor.FindPropertyRelative("r")) ||
            !SupportsScaling(perStatColors) ||
            firstStep == null ||
            !SupportsScaling(firstStep.FindPropertyRelative("useLevelUpGrowthColorOverride")) ||
            stepColor == null ||
            !SupportsScaling(stepColor.FindPropertyRelative("r")) ||
            runtimeConfig.ShowLevelUpStatGrowthAbovePlayer != baseConfig.Config.ShowLevelUpStatGrowthAbovePlayer ||
            runtimeConfig.UsePerStatLevelUpGrowthColors != baseConfig.Config.UsePerStatLevelUpGrowthColors ||
            math.any(math.abs(runtimeConfig.LevelUpStatGrowthColor - baseConfig.Config.LevelUpStatGrowthColor) > PrecisionEpsilon) ||
            runtimeConfig.ShowLevelUpStatGrowthAbovePlayer != (uiVisualPreset.GrowthSequence.ShowLevelUpStatGrowthAbovePlayer ? (byte)1 : (byte)0))
        {
            throw new InvalidOperationException("The Growth Sequence level-up presentation option is not fully scalable or preserved by ECS bake paths.");
        }
    }

    /// <summary>
    /// Confirms numeric, enum, and token formula results reach baked Random Stat Growth and resource fields.
    /// </summary>
    private static void ValidateRuntimeScalingPaths()
    {
        PlayerPowerUpSlotConfig activeConfig = new PlayerPowerUpSlotConfig();
        PlayerPassiveToolConfig passiveConfig = new PlayerPassiveToolConfig();
        activeConfig.RandomStatGrowthEntries.Add(new PlayerRandomStatGrowthEntryConfig
        {
            Target = PlayerRandomStatGrowthTarget.MaximumHealth,
            MinimumIncrease = 1f,
            MaximumIncrease = 2f
        });
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("resourceGate.initialEnergy",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           37f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("randomStatGrowth.entries.Array.data[0|smoke-entry].target",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           (float)PlayerRandomStatGrowthTarget.CustomScalableStat,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("randomStatGrowth.entries.Array.data[0|smoke-entry].minimumIncrease",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           3f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("randomStatGrowth.entries.Array.data[0|smoke-entry].maximumIncrease",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           4f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("randomStatGrowth.entries.Array.data[0|smoke-entry].selectionWeight",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           7f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("randomStatGrowth.entries.Array.data[0|smoke-entry].presentationColor.g",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           0.35f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("randomStatGrowth.useWeightedSelection",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("randomStatGrowth.entries.Array.data[0|smoke-entry].useCustomPresentationColor",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyTokenValue("randomStatGrowth.entries.Array.data[0|smoke-entry].customScalableStatName",
                                                                PlayerPowerUpUnlockKind.Active,
                                                                "Damage",
                                                                ref activeConfig,
                                                                ref passiveConfig);
        PlayerRandomStatGrowthEntryConfig entry = activeConfig.RandomStatGrowthEntries[0];

        if (math.abs(activeConfig.InitialEnergy - 37f) > PrecisionEpsilon ||
            entry.Target != PlayerRandomStatGrowthTarget.CustomScalableStat ||
            entry.CustomScalableStatName.ToString() != "Damage" ||
            math.abs(entry.MinimumIncrease - 3f) > PrecisionEpsilon ||
            math.abs(entry.MaximumIncrease - 4f) > PrecisionEpsilon ||
            math.abs(entry.SelectionWeight - 7f) > PrecisionEpsilon ||
            math.abs(entry.PresentationColor.y - 0.35f) > PrecisionEpsilon ||
            entry.UseCustomPresentationColor == 0 ||
            activeConfig.UseWeightedRandomStatGrowthSelection == 0)
        {
            throw new InvalidOperationException("Unified runtime scaling did not propagate every Random Stat Growth field type.");
        }
    }
    #endregion

    #region Runtime Rewards
    /// <summary>
    /// Applies deterministic native and custom rewards to ECS buffers and checks shared overhead presentation events.
    /// </summary>
    private static void ValidatePermanentRuntimeRewards()
    {
        World world = new World("RandomStatGrowthSmokeTest");
        Entity entity = world.EntityManager.CreateEntity();

        try
        {
            world.EntityManager.AddBuffer<PlayerScalableStatElement>(entity);
            world.EntityManager.AddBuffer<PlayerRandomStatGrowthModifierElement>(entity);
            world.EntityManager.AddBuffer<PlayerRoomRewardPresentationEvent>(entity);
            DynamicBuffer<PlayerScalableStatElement> scalableStats = world.EntityManager.GetBuffer<PlayerScalableStatElement>(entity);
            DynamicBuffer<PlayerRandomStatGrowthModifierElement> modifiers = world.EntityManager.GetBuffer<PlayerRandomStatGrowthModifierElement>(entity);
            DynamicBuffer<PlayerRoomRewardPresentationEvent> presentationEvents = world.EntityManager.GetBuffer<PlayerRoomRewardPresentationEvent>(entity);
            PlayerRandomStatGrowthState growthState = default;
            PlayerRuntimeScalingState runtimeScalingState = new PlayerRuntimeScalingState
            {
                Initialized = 1
            };
            PlayerPowerUpSlotConfig nativeSlot = BuildSingleCandidateSlot(PlayerRandomStatGrowthTarget.ProjectileDamage,
                                                                          string.Empty,
                                                                          2f);

            if (!PlayerRandomStatGrowthRuntimeUtility.TryApply(in nativeSlot,
                                                               scalableStats,
                                                               modifiers,
                                                               ref growthState,
                                                               ref runtimeScalingState,
                                                               presentationEvents) ||
                modifiers.Length != 1 ||
                math.abs(modifiers[0].TotalIncrease - 2f) > PrecisionEpsilon ||
                presentationEvents.Length != 1 ||
                presentationEvents[0].TargetStatName.ToString() != "Projectile Damage" ||
                presentationEvents[0].HasTextColorOverride == 0 ||
                math.abs(presentationEvents[0].TextColorOverride.x - 0.2f) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("A native Random Stat Growth reward was not accumulated and presented correctly.");
            }

            scalableStats.Add(new PlayerScalableStatElement
            {
                Name = new FixedString64Bytes("Damage"),
                Type = (byte)PlayerScalableStatType.Float,
                MinimumValue = 0f,
                MaximumValue = 100f,
                Value = 10f
            });
            PlayerPowerUpSlotConfig customSlot = BuildSingleCandidateSlot(PlayerRandomStatGrowthTarget.CustomScalableStat,
                                                                          "Damage",
                                                                          2.5f);

            if (!PlayerRandomStatGrowthRuntimeUtility.TryApply(in customSlot,
                                                               scalableStats,
                                                               modifiers,
                                                               ref growthState,
                                                               ref runtimeScalingState,
                                                               presentationEvents) ||
                math.abs(scalableStats[0].Value - 12.5f) > PrecisionEpsilon ||
                runtimeScalingState.Initialized != 0 ||
                presentationEvents.Length != 2 ||
                presentationEvents[1].TargetStatName.ToString() != "Damage" ||
                presentationEvents[1].HasTextColorOverride == 0)
            {
                throw new InvalidOperationException("A custom Random Stat Growth reward was not persisted, invalidated, and presented correctly.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Builds one exact-value candidate so runtime reward checks remain deterministic.
    /// </summary>
    /// <param name="target">Native or custom target to exercise.</param>
    /// <param name="customStatName">Custom scalable-stat name, or an empty string for native targets.</param>
    /// <param name="increase">Exact permanent increase applied by the candidate.</param>
    /// <returns>A defined active slot containing one Random Stat Growth entry.</returns>
    private static PlayerPowerUpSlotConfig BuildSingleCandidateSlot(PlayerRandomStatGrowthTarget target,
                                                                    string customStatName,
                                                                    float increase)
    {
        PlayerPowerUpSlotConfig slotConfig = new PlayerPowerUpSlotConfig
        {
            IsDefined = 1,
            PowerUpId = new FixedString64Bytes("RandomGrowthSmokeTest"),
            ToolKind = ActiveToolKind.RandomStatGrowth,
            UseWeightedRandomStatGrowthSelection = 1
        };
        slotConfig.RandomStatGrowthEntries.Add(new PlayerRandomStatGrowthEntryConfig
        {
            Target = target,
            CustomScalableStatName = new FixedString64Bytes(customStatName),
            MinimumIncrease = increase,
            MaximumIncrease = increase,
            SelectionWeight = 1f,
            UseCustomPresentationColor = 1,
            PresentationColor = new float4(0.2f, 0.7f, 0.9f, 1f)
        });
        return slotConfig;
    }
    #endregion

    #endregion
}
