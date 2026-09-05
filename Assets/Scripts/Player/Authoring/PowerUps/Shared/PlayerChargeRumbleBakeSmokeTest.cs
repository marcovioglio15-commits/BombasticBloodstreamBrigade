#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Verifies charge rumble through modular bake, formula metadata, baseline rebuild and impulse consumption.
/// </summary>
public static class PlayerChargeRumbleBakeSmokeTest
{
    #region Fields
    public static readonly string[] FieldNames =
    {
        "chargeCompleteRumbleEnabled", "chargeCompleteRumbleDurationSeconds",
        "chargeCompleteRumbleLowFrequency", "chargeCompleteRumbleHighFrequency"
    };
    #endregion

    #region Methods

    #region Entry Point
    /// <summary>
    /// Runs deterministic checks against temporary presets and a disposable ECS world.
    /// </summary>
    public static void Run()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            preset.EnsureDefaultModularSetup();
            ConfigurePreset(preset);
            ValidateBakeAndScaling(preset);
            ValidateImpulse();
            Debug.Log("[PlayerChargeRumbleBakeSmokeTest] Bake, formula, baseline and impulse checks passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }
    #endregion

    #region Authoring
    /// <summary>
    /// Creates manual, passive and toggle compositions sharing one scalable hold-charge module.
    /// </summary>
    /// <param name="preset">Temporary preset owned by the smoke test.</param>
    private static void ConfigurePreset(PlayerPowerUpsPreset preset)
    {
        preset.ActivePowerUpsMutable.Clear();
        preset.PassivePowerUpsMutable.Clear();
        preset.ScalingRulesMutable.Clear();
        preset.ActivePowerUpsMutable.Add(CreatePowerUp("SmokeCharge", "Module_TriggerHoldCharge"));
        preset.PassivePowerUpsMutable.Add(CreatePowerUp("SmokePassiveCharge", "Module_SuddenStrike", "Module_TriggerHoldCharge"));
        ModularPowerUpDefinition toggle = CreatePowerUp("SmokeToggleCharge", "Module_GateResource", "Module_SuddenStrike", "Module_TriggerHoldCharge");
        PowerUpModuleData gate = new PowerUpModuleData();
        gate.ResourceGate.Configure(PowerUpResourceType.Energy, PowerUpResourceType.Energy,
                                     100f, 0f, 1f, 0f, PowerUpChargeType.Time, 1f, 0f, true, 4f, false, 2f);
        toggle.ModuleBindings[0].ConfigureOverride(true, gate);
        preset.ActivePowerUpsMutable.Add(toggle);
        SerializedObject serialized = new SerializedObject(preset);
        SerializedProperty modules = serialized.FindProperty("moduleDefinitions");

        // Use production stable keys so the metadata builder exercises shared module propagation.
        for (int index = 0; index < modules.arraySize; index++)
        {
            SerializedProperty module = modules.GetArrayElementAtIndex(index);

            if (module.FindPropertyRelative("moduleId").stringValue != "Module_TriggerHoldCharge")
                continue;

            SerializedProperty payload = module.FindPropertyRelative("data").FindPropertyRelative("holdCharge");
            payload.FindPropertyRelative(FieldNames[0]).boolValue = true;
            payload.FindPropertyRelative(FieldNames[1]).floatValue = 0.2f;
            payload.FindPropertyRelative(FieldNames[2]).floatValue = 0.25f;
            payload.FindPropertyRelative(FieldNames[3]).floatValue = 0.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            for (int fieldIndex = 0; fieldIndex < FieldNames.Length; fieldIndex++)
            {
                SerializedProperty field = payload.FindPropertyRelative(FieldNames[fieldIndex]);
                PlayerStatScalingRule rule = new PlayerStatScalingRule();
                rule.Configure(PlayerScalingStatKeyUtility.BuildStatKey(field), true, fieldIndex == 0 ? "![this]" : "[this] * 2");
                preset.ScalingRulesMutable.Add(rule);
            }

            return;
        }

        throw new InvalidOperationException("Default hold-charge module is missing.");
    }

    /// <summary>
    /// Builds a small test composition using production binding APIs.
    /// </summary>
    /// <param name="id">Stable power-up identifier.</param>
    /// <param name="moduleIds">Modules used in the composition.</param>
    /// <returns>Enabled modular power-up definition.</returns>
    private static ModularPowerUpDefinition CreatePowerUp(string id, params string[] moduleIds)
    {
        PowerUpCommonData common = new PowerUpCommonData();
        common.Configure(id, id, "Charge rumble verification.", null, new List<string>(), 1, 0);
        ModularPowerUpDefinition powerUp = new ModularPowerUpDefinition();
        powerUp.Configure(common, false);
        powerUp.ClearBindings();

        // Preserve module order to match normal preset compilation.
        for (int index = 0; index < moduleIds.Length; index++)
        {
            PowerUpModuleBinding binding = new PowerUpModuleBinding();
            binding.Configure(moduleIds[index], true);
            powerUp.AddBinding(binding);
        }

        return powerUp;
    }
    #endregion

    #region Bake And Rebuild
    /// <summary>
    /// Confirms formulas reach all supported compositions and repeated rebuilding starts from immutable values.
    /// </summary>
    /// <param name="preset">Temporary configured preset.</param>
    private static void ValidateBakeAndScaling(PlayerPowerUpsPreset preset)
    {
        using (World world = new World("Charge rumble verification"))
        {
            Entity entity = world.EntityManager.CreateEntity();
            DynamicBuffer<PlayerPowerUpBaseConfigElement> baselines = world.EntityManager.AddBuffer<PlayerPowerUpBaseConfigElement>(entity);
            world.EntityManager.AddBuffer<PlayerRuntimePowerUpScalingElement>(entity);
            baselines = world.EntityManager.GetBuffer<PlayerPowerUpBaseConfigElement>(entity);
            DynamicBuffer<PlayerRuntimePowerUpScalingElement> metadata = world.EntityManager.GetBuffer<PlayerRuntimePowerUpScalingElement>(entity);
            PlayerRuntimeScalingBakeUtility.PopulatePowerUpBaseConfigs(null, preset, prefab => Entity.Null, baselines);
            PlayerRuntimeScalingBakeUtility.PopulatePowerUpScalingMetadata(preset, metadata);

            if (baselines.Length != 3 || metadata.Length != 12)
                throw new InvalidOperationException("Charge rumble did not propagate to every baseline and formula metadata entry.");

            // Repeat the same rebuild to detect accidental compounding or mutation of source payloads.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int index = 0; index < baselines.Length; index++)
                {
                    PlayerPowerUpBaseConfigElement baseline = baselines[index];
                    PlayerPowerUpSlotConfig active = baseline.ActiveSlotConfig;
                    PlayerPassiveToolConfig passive = baseline.PassiveToolConfig;
                    AssertConfig(ResolveRumble(in baseline), 1, 0.2f, 0.25f, 0.4f);

                    for (int ruleIndex = 0; ruleIndex < metadata.Length; ruleIndex++)
                    {
                        PlayerRuntimePowerUpScalingElement rule = metadata[ruleIndex];

                        if (rule.PowerUpId != baseline.PowerUpId || rule.UnlockKind != baseline.UnlockKind)
                            continue;

                        PlayerFormulaValue input = (PlayerFormulaValueType)rule.ValueType == PlayerFormulaValueType.Boolean
                            ? PlayerFormulaValue.CreateBoolean(rule.BaseBooleanValue != 0)
                            : PlayerFormulaValue.CreateNumber(rule.BaseValue);

                        if (!PlayerStatFormulaEngine.TryEvaluate(rule.Formula.ToString(), input, null,
                                                                  out PlayerFormulaValue result, out string error))
                            throw new InvalidOperationException(error);

                        if (result.Type == PlayerFormulaValueType.Boolean)
                            PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue(rule.PayloadPath.ToString(), rule.UnlockKind,
                                                                                      result.BooleanValue, ref active, ref passive);
                        else
                            PlayerRuntimePowerUpScalingPathUtility.ApplyValue(rule.PayloadPath.ToString(), rule.UnlockKind,
                                                                               result.NumberValue, ref active, ref passive);
                    }

                    PlayerPowerUpBaseConfigElement rebuilt = baseline;
                    rebuilt.ActiveSlotConfig = active;
                    rebuilt.PassiveToolConfig = passive;
                    AssertConfig(ResolveRumble(in rebuilt), 0, 0.4f, 0.5f, 0.8f);
                    AssertConfig(ResolveRumble(in baseline), 1, 0.2f, 0.25f, 0.4f);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the payload actually consumed by each charge composition.
    /// </summary>
    /// <param name="config">Baked or rebuilt test entry.</param>
    /// <returns>Manual or conditional charge-completion rumble settings.</returns>
    private static PlayerChargeRumbleConfig ResolveRumble(in PlayerPowerUpBaseConfigElement config)
    {
        if (config.UnlockKind == PlayerPowerUpUnlockKind.Passive)
            return config.PassiveToolConfig.ConditionalApplication.HoldCharge.ChargeCompleteRumble;

        if (config.ActiveSlotConfig.Toggleable != 0)
            return config.ActiveSlotConfig.TogglePassiveTool.ConditionalApplication.HoldCharge.ChargeCompleteRumble;

        return config.ActiveSlotConfig.ChargeShot.ChargeCompleteRumble;
    }

    /// <summary>
    /// Compares the complete feedback payload after bake or formula application.
    /// </summary>
    /// <param name="config">Actual compiled payload.</param>
    /// <param name="enabled">Expected enable flag.</param>
    /// <param name="duration">Expected impulse duration.</param>
    /// <param name="low">Expected low-frequency motor strength.</param>
    /// <param name="high">Expected high-frequency motor strength.</param>
    private static void AssertConfig(PlayerChargeRumbleConfig config, byte enabled, float duration, float low, float high)
    {
        if (config.Enabled != enabled || math.abs(config.DurationSeconds - duration) > 0.0001f ||
            math.abs(config.LowFrequency - low) > 0.0001f || math.abs(config.HighFrequency - high) > 0.0001f)
            throw new InvalidOperationException("Charge rumble values diverged between authoring, bake and runtime scaling.");
    }
    #endregion

    #region Runtime Impulses
    /// <summary>
    /// Checks threshold edges, held-full deduplication, expiry, rearming and disabled feedback.
    /// </summary>
    private static void ValidateImpulse()
    {
        ChargeShotPowerUpConfig config = new ChargeShotPowerUpConfig
        {
            RequiredCharge = 100f,
            ChargeCompleteRumble = new PlayerChargeRumbleConfig { Enabled = 1, DurationSeconds = 0.2f, LowFrequency = 0.3f, HighFrequency = 0.7f }
        };
        PlayerChargeRumbleState state = default;
        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 80f, 99f, ref state);

        if (state.RemainingSeconds != 0f)
            throw new InvalidOperationException("Incomplete charge emitted rumble.");

        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 99f, 100f, ref state);
        float2 speeds = PlayerChargeRumbleRuntimeUtility.Advance(ref state, 0.1f);
        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 100f, 100f, ref state);

        if (math.abs(state.RemainingSeconds - 0.1f) > 0.0001f || speeds.x != 0.3f || speeds.y != 0.7f)
            throw new InvalidOperationException("Full charge retriggered or failed to reach the motor mixer.");

        PlayerChargeRumbleRuntimeUtility.Advance(ref state, 0.2f);

        if (state.RemainingSeconds != 0f || state.LowFrequency != 0f || state.HighFrequency != 0f)
            throw new InvalidOperationException("Expired charge rumble retained a motor speed.");

        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 0f, 100f, ref state);

        if (state.RemainingSeconds <= 0f)
            throw new InvalidOperationException("A new charge cycle did not rearm feedback.");

        state = default;
        config.ChargeCompleteRumble.Enabled = 0;
        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 0f, 100f, ref state);

        if (state.RemainingSeconds != 0f)
            throw new InvalidOperationException("Disabled charge feedback emitted an impulse.");

        // Manual charge follows the full HUD cap; Sudden Strike instead completes when it becomes armed.
        config.ChargeCompleteRumble.Enabled = 1;
        config.MaximumCharge = 150f;
        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 99f, 100f, ref state);

        if (state.RemainingSeconds != 0f)
            throw new InvalidOperationException("Manual charge emitted its full-charge impulse at the minimum release threshold.");

        PlayerChargeRumbleRuntimeUtility.QueueCompletion(in config, 99f, 100f, ref state, true);

        if (state.RemainingSeconds <= 0f)
            throw new InvalidOperationException("Sudden Strike did not emit feedback on arming.");

        PlayerPowerUpsState powerUpsState = new PlayerPowerUpsState { ChargeRumble = state };
        PlayerPowerUpsConfig powerUpsConfig = default;
        PlayerPowerUpLoadoutRuntimeUtility.ResetRuntimeState(ref powerUpsState, in powerUpsConfig);

        if (powerUpsState.ChargeRumble.RemainingSeconds != 0f)
            throw new InvalidOperationException("Loadout reset retained stale charge rumble.");
    }
    #endregion

    #endregion
}
#endif
