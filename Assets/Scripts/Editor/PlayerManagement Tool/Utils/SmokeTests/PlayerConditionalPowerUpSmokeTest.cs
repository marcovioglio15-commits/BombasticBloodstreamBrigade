#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs deterministic editor checks for conditional power-up authoring, scaling, cadence, charge, and aggregation isolation.
/// </summary>
public static class PlayerConditionalPowerUpSmokeTest
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Player/Run Conditional Power-Up Smoke Test")]
    /// <summary>
    /// Executes the conditional power-up smoke suite from Unity batch mode through -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateAuthoringScalingTargets();
        ValidateBakePaths();
        ValidateRuntimeScalingPaths();
        ValidateDelayedShotCadence();
        ValidateSuddenStrikeCharge();
        ValidateConditionalCharacterTuning();
        ValidateSelfPreservationThreshold();
        ValidateFiniteToggleLifetime();
        ValidateConditionalAggregationIsolation();
        ValidatePayloadClipboard();
        PlayerProjectileConePatternSmokeTest.Run();
        Debug.Log("[PlayerConditionalPowerUpSmokeTest] All conditional power-up checks passed.");
    }
    #endregion

    #region Bake
    /// <summary>
    /// Verifies passive and toggleable-active compositions compile without recursion and retain their conditional payloads.
    /// </summary>
    private static void ValidateBakePaths()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            preset.EnsureDefaultModularSetup();
            ModularPowerUpDefinition suddenStrike = CreatePowerUp("SmokeSuddenStrike",
                                                                  CreateBinding("Module_SuddenStrike"),
                                                                  CreateBinding("Module_TriggerHoldCharge"),
                                                                  CreateBinding("Module_ProjectilesPatternCone"));
            PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(null,
                                                                                      preset,
                                                                                      suddenStrike,
                                                                                      prefab => Entity.Null,
                                                                                      out PlayerPassiveToolConfig suddenStrikeConfig);

            if (suddenStrikeConfig.IsDefined == 0 ||
                suddenStrikeConfig.HasShotgun == 0 ||
                suddenStrikeConfig.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.SuddenStrike ||
                suddenStrikeConfig.ConditionalApplication.HoldCharge.RequiredCharge <= 0f)
            {
                throw new Exception("Sudden Strike did not bake its hold-charge and projectile payloads.");
            }

            ModularPowerUpDefinition standaloneSuddenStrike = CreatePowerUp("SmokeStandaloneSuddenStrike",
                                                                            CreateBinding("Module_SuddenStrike"),
                                                                            CreateBinding("Module_TriggerHoldCharge"),
                                                                            CreateBinding("Module_Character Tuning"));
            PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(null,
                                                                                      preset,
                                                                                      standaloneSuddenStrike,
                                                                                      prefab => Entity.Null,
                                                                                      out PlayerPassiveToolConfig standaloneSuddenStrikeConfig);

            if (standaloneSuddenStrikeConfig.IsDefined == 0 ||
                standaloneSuddenStrikeConfig.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.SuddenStrike ||
                standaloneSuddenStrikeConfig.ConditionalApplication.HoldCharge.RequiredCharge <= 0f)
            {
                throw new Exception("Sudden Strike did not accept Trigger Hold Charge with conditional Character Tuning.");
            }

            ModularPowerUpDefinition delayed = CreatePowerUp("SmokeDelayed",
                                                              CreateBinding("Module_DelayedShootApplication"),
                                                              CreateBinding("Module_BouncingProjectiles"));
            PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(null,
                                                                                      preset,
                                                                                      delayed,
                                                                                      prefab => Entity.Null,
                                                                                      out PlayerPassiveToolConfig delayedConfig);

            if (delayedConfig.IsDefined == 0 ||
                delayedConfig.HasBouncingProjectiles == 0 ||
                delayedConfig.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.DelayedShootApplication ||
                delayedConfig.ConditionalApplication.DelayedShotInterval != 3)
            {
                throw new Exception("Delayed Shoot Application did not bake its cadence and projectile hook.");
            }

            ModularPowerUpDefinition selfPreservation = CreatePowerUp("SmokeSelfPreservation",
                                                                       CreateBinding("Module_SelfPreservationInstinct"),
                                                                       CreateBinding("Module_TimeDilationEnemies"));
            PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(null,
                                                                                      preset,
                                                                                      selfPreservation,
                                                                                      prefab => Entity.Null,
                                                                                      out PlayerPassiveToolConfig selfPreservationConfig);

            if (selfPreservationConfig.IsDefined == 0 ||
                selfPreservationConfig.HasBulletTime == 0 ||
                selfPreservationConfig.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.SelfPreservationInstinct ||
                selfPreservationConfig.BulletTime.DurationSeconds <= 0f ||
                selfPreservationConfig.BulletTime.TransitionTimeSeconds <= 0f)
            {
                throw new Exception("Self-Preservation Instinct did not bake its health trigger and active effect.");
            }

            ModularPowerUpDefinition invalidSuddenStrike = CreatePowerUp("SmokeInvalidSuddenStrike",
                                                                         CreateBinding("Module_SuddenStrike"),
                                                                         CreateBinding("Module_ProjectilesPatternCone"));
            PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(null,
                                                                                      preset,
                                                                                      invalidSuddenStrike,
                                                                                      prefab => Entity.Null,
                                                                                      out PlayerPassiveToolConfig invalidSuddenStrikeConfig);
            PlayerPassiveToolsState invalidAggregate = new PlayerPassiveToolsState
            {
                ProjectileSizeMultiplier = 1f,
                ProjectileDamageMultiplier = 1f,
                ProjectileSpeedMultiplier = 1f,
                ProjectileLifetimeSecondsMultiplier = 1f,
                ProjectileLifetimeRangeMultiplier = 1f
            };
            PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref invalidAggregate,
                                                                       in invalidSuddenStrikeConfig);

            if (invalidSuddenStrikeConfig.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.InvalidComposition ||
                invalidAggregate.HasShotgun != 0)
            {
                throw new Exception("An invalid Sudden Strike composition leaked its sibling projectile effect into normal passive aggregation.");
            }

            PowerUpModuleData toggleGatePayload = new PowerUpModuleData();
            toggleGatePayload.ResourceGate.Configure(PowerUpResourceType.Energy,
                                                      PowerUpResourceType.Energy,
                                                      100f,
                                                      0f,
                                                      1f,
                                                      0f,
                                                      PowerUpChargeType.Time,
                                                      1f,
                                                      0f,
                                                      true,
                                                      4f,
                                                      false,
                                                      2f);
            ModularPowerUpDefinition toggleDelayed = CreatePowerUp("SmokeToggleDelayed",
                                                                    CreateBinding("Module_GateResource", toggleGatePayload),
                                                                    CreateBinding("Module_DelayedShootApplication"),
                                                                    CreateBinding("Module_ProjectilesPatternCone"));
            PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(null,
                                                                              preset,
                                                                              toggleDelayed,
                                                                              prefab => Entity.Null,
                                                                              out PlayerPowerUpSlotConfig toggleConfig);

            if (toggleConfig.IsDefined == 0 ||
                toggleConfig.ToolKind != ActiveToolKind.PassiveToggle ||
                math.abs(toggleConfig.MaximumToggleActiveDurationSeconds - 2f) > PrecisionEpsilon ||
                toggleConfig.TogglePassiveTool.IsDefined == 0 ||
                toggleConfig.TogglePassiveTool.ConditionalApplication.Mode != PowerUpConditionalApplicationMode.DelayedShootApplication)
            {
                throw new Exception("Toggleable Active did not retain its embedded conditional passive config.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Creates one temporary modular composition from already configured bindings.
    /// </summary>
    /// <param name="powerUpId">Stable identifier assigned to the temporary composition.</param>
    /// <param name="bindings">Module bindings appended in execution order.</param>
    /// <returns>Validated modular power-up definition used only by the smoke test.</returns>
    private static ModularPowerUpDefinition CreatePowerUp(string powerUpId, params PowerUpModuleBinding[] bindings)
    {
        PowerUpCommonData commonData = new PowerUpCommonData();
        commonData.Configure(powerUpId,
                             powerUpId,
                             "Conditional bake smoke test.",
                             null,
                             new List<string>(),
                             1,
                             0);
        ModularPowerUpDefinition powerUp = new ModularPowerUpDefinition();
        powerUp.Configure(commonData, false);
        powerUp.ClearBindings();

        // Preserve the authored order used by the production modular compiler.
        for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            powerUp.AddBinding(bindings[bindingIndex]);

        powerUp.Validate();
        return powerUp;
    }

    /// <summary>
    /// Creates one enabled module binding with an optional isolated override payload.
    /// </summary>
    /// <param name="moduleId">Stable module identifier resolved from the temporary preset.</param>
    /// <param name="overridePayload">Optional payload used instead of the preset module default.</param>
    /// <returns>Configured binding ready to append to a temporary composition.</returns>
    private static PowerUpModuleBinding CreateBinding(string moduleId, PowerUpModuleData overridePayload = null)
    {
        PowerUpModuleBinding binding = new PowerUpModuleBinding();
        binding.Configure(moduleId, true);

        if (overridePayload != null)
            binding.ConfigureOverride(true, overridePayload);

        return binding;
    }
    #endregion

    #region Authoring
    /// <summary>
    /// Verifies every new serialized field is exposed to the unified Add Scaling workflow, including enums and booleans.
    /// </summary>
    private static void ValidateAuthoringScalingTargets()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            SerializedObject serializedPreset = new SerializedObject(preset);
            SerializedProperty moduleDefinitions = serializedPreset.FindProperty("moduleDefinitions");
            moduleDefinitions.arraySize = 1;
            SerializedProperty moduleData = moduleDefinitions.GetArrayElementAtIndex(0)
                                                             .FindPropertyRelative("data");
            ValidateScalingFields(moduleData.FindPropertyRelative("delayedShootApplication"),
                                  "shotInterval");
            ValidateScalingFields(moduleData.FindPropertyRelative("suddenStrike"),
                                  "conditionMode",
                                  "countRotationAsMovement",
                                  "stationarySpeedTolerance",
                                  "stationaryRotationToleranceDegrees",
                                  "applyChargeMovementSlow",
                                  "movementSlowRecoverySeconds");
            ValidateScalingFields(moduleData.FindPropertyRelative("selfPreservationInstinct"),
                                  "thresholdMode",
                                  "healthThreshold");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Verifies a list of child fields exists and is accepted by the shared scaling-target utility.
    /// </summary>
    /// <param name="payloadProperty">Serialized module payload containing the inspected fields.</param>
    /// <param name="fieldNames">Relative serialized field names expected to support Add Scaling.</param>
    private static void ValidateScalingFields(SerializedProperty payloadProperty, params string[] fieldNames)
    {
        if (payloadProperty == null)
            throw new Exception("A conditional module payload is missing from serialized modular data.");

        // Inspect every declared field through the same eligibility path used by Add Scaling buttons.
        for (int fieldIndex = 0; fieldIndex < fieldNames.Length; fieldIndex++)
        {
            SerializedProperty fieldProperty = payloadProperty.FindPropertyRelative(fieldNames[fieldIndex]);

            if (fieldProperty == null || !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(fieldProperty))
                throw new Exception("Conditional Add Scaling target is unavailable: " + fieldNames[fieldIndex]);
        }
    }
    #endregion

    #region Runtime Scaling
    /// <summary>
    /// Verifies numeric, enum, and boolean formula results reach the baked conditional runtime fields used by systems.
    /// </summary>
    private static void ValidateRuntimeScalingPaths()
    {
        PlayerPowerUpSlotConfig activeConfig = default;
        PlayerPassiveToolConfig passiveConfig = new PlayerPassiveToolConfig
        {
            ConditionalApplication = new PowerUpConditionalApplicationConfig
            {
                Mode = PowerUpConditionalApplicationMode.SuddenStrike
            }
        };

        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("suddenStrike.conditionMode",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           (float)SuddenStrikeChargeConditionMode.NotShooting,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("suddenStrike.stationarySpeedTolerance",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           0.35f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("suddenStrike.movementSlowRecoverySeconds",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           0.75f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("suddenStrike.countRotationAsMovement",
                                                                  PlayerPowerUpUnlockKind.Passive,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("suddenStrike.applyChargeMovementSlow",
                                                                  PlayerPowerUpUnlockKind.Passive,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("holdCharge.chargedLaserBeam.damageMultiplier",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           2.5f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("holdCharge.chargedLaserBeam.applyPlayerHandlingNerfWhileFiring",
                                                                  PlayerPowerUpUnlockKind.Passive,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);

        PowerUpConditionalApplicationConfig scaledConfig = passiveConfig.ConditionalApplication;

        if (scaledConfig.SuddenStrikeConditionMode != SuddenStrikeChargeConditionMode.NotShooting ||
            math.abs(scaledConfig.StationarySpeedTolerance - 0.35f) > PrecisionEpsilon ||
            math.abs(scaledConfig.MovementSlowRecoverySeconds - 0.75f) > PrecisionEpsilon ||
            scaledConfig.CountRotationAsMovement == 0 ||
            scaledConfig.ApplyChargeMovementSlow == 0 ||
            math.abs(scaledConfig.HoldCharge.ChargedLaserBeam.DamageMultiplier - 2.5f) > PrecisionEpsilon ||
            scaledConfig.HoldCharge.ChargedLaserBeam.ApplyPlayerHandlingNerfWhileFiring == 0)
        {
            throw new Exception("Conditional runtime scaling paths did not update the ECS config.");
        }

        scaledConfig.Mode = PowerUpConditionalApplicationMode.DelayedShootApplication;
        passiveConfig.ConditionalApplication = scaledConfig;
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("delayedShootApplication.shotInterval",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           5f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        scaledConfig = passiveConfig.ConditionalApplication;

        if (scaledConfig.DelayedShotInterval != 5)
            throw new Exception("Delayed Shoot Application scaling did not reach its ECS cadence config.");

        scaledConfig.Mode = PowerUpConditionalApplicationMode.SelfPreservationInstinct;
        passiveConfig.ConditionalApplication = scaledConfig;
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("selfPreservationInstinct.thresholdMode",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           (float)SelfPreservationHealthThresholdMode.CurrentHealthValue,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("selfPreservationInstinct.healthThreshold",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           12f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        scaledConfig = passiveConfig.ConditionalApplication;

        if (scaledConfig.HealthThresholdMode != SelfPreservationHealthThresholdMode.CurrentHealthValue ||
            math.abs(scaledConfig.HealthThreshold - 12f) > PrecisionEpsilon)
        {
            throw new Exception("Self-Preservation Instinct scaling did not reach its ECS threshold config.");
        }

        activeConfig = new PlayerPowerUpSlotConfig
        {
            ToolKind = ActiveToolKind.PassiveToggle,
            TogglePassiveTool = new PlayerPassiveToolConfig
            {
                IsDefined = 1,
                ConditionalApplication = new PowerUpConditionalApplicationConfig
                {
                    Mode = PowerUpConditionalApplicationMode.DelayedShootApplication
                }
            }
        };
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("delayedShootApplication.shotInterval",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           7f,
                                                           ref activeConfig,
                                                           ref passiveConfig);

        if (activeConfig.TogglePassiveTool.ConditionalApplication.DelayedShotInterval != 7)
            throw new Exception("Toggleable Active scaling did not reach its embedded conditional ECS config.");
    }
    #endregion

    #region Runtime Conditions
    /// <summary>
    /// Verifies Delayed Shoot Application qualifies exactly the configured base-shot cadence.
    /// </summary>
    private static void ValidateDelayedShotCadence()
    {
        PowerUpConditionalApplicationConfig config = new PowerUpConditionalApplicationConfig
        {
            Mode = PowerUpConditionalApplicationMode.DelayedShootApplication,
            DelayedShotInterval = 3
        };
        PowerUpConditionalApplicationRuntimeState runtimeState = default;

        if (PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState) ||
            PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState) ||
            !PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState) ||
            PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState))
        {
            throw new Exception("Delayed Shoot Application did not qualify every third base shot.");
        }
    }

    /// <summary>
    /// Verifies Sudden Strike arms after the required stationary duration and is consumed by one shot only.
    /// </summary>
    private static void ValidateSuddenStrikeCharge()
    {
        PowerUpConditionalApplicationConfig config = new PowerUpConditionalApplicationConfig
        {
            Mode = PowerUpConditionalApplicationMode.SuddenStrike,
            SuddenStrikeConditionMode = SuddenStrikeChargeConditionMode.Stationary,
            StationarySpeedTolerance = 0.05f,
            HoldCharge = new ChargeShotPowerUpConfig
            {
                RequiredCharge = 1f,
                MaximumCharge = 1f,
                ChargeRatePerSecond = 2f
            }
        };
        PowerUpConditionalApplicationRuntimeState runtimeState = default;
        PlayerMovementState movementState = default;
        PlayerLookState lookState = default;
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  0.25f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);

        if (runtimeState.Armed != 0)
            throw new Exception("Sudden Strike armed before its required stationary duration.");

        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  0.25f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);

        if (runtimeState.Armed == 0 ||
            !PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState) ||
            PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState))
        {
            throw new Exception("Sudden Strike did not arm and consume exactly one automatic charged shot.");
        }

        config.ApplyChargeMovementSlow = 1;
        config.MovementSlowRecoverySeconds = 0.5f;
        config.HoldCharge.SlowPlayerWhileCharging = 1;
        config.HoldCharge.MaximumPlayerSlowPercent = 40f;
        runtimeState = default;
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  0.25f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);

        if (math.abs(runtimeState.MovementSlowPercent - 20f) > PrecisionEpsilon)
            throw new Exception("Sudden Strike did not apply the normalized Trigger Hold Charge movement slow.");

        movementState.Velocity = new float3(1f, 0f, 0f);
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  0.125f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);

        if (math.abs(runtimeState.MovementSlowPercent - 10f) > PrecisionEpsilon)
            throw new Exception("Sudden Strike did not remove its movement slow linearly after interruption.");

        movementState = default;
        lookState.AngularSpeed = 2f;
        config.CountRotationAsMovement = 1;
        config.StationaryRotationToleranceDegrees = 1f;
        runtimeState = default;
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  0.25f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);

        if (runtimeState.Charge > PrecisionEpsilon)
            throw new Exception("Sudden Strike ignored look rotation while Count Rotation As Movement was enabled.");

        config.SuddenStrikeConditionMode = SuddenStrikeChargeConditionMode.NotShooting;
        config.HoldCharge.RequiredCharge = 50f;
        config.HoldCharge.MaximumCharge = 50f;
        config.HoldCharge.ChargeRatePerSecond = 25f;
        config.ApplyChargeMovementSlow = 0;
        runtimeState = default;
        lookState = default;
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  1f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  1f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  0u,
                                                                  ref runtimeState);

        if (runtimeState.Armed == 0)
            throw new Exception("Sudden Strike did not arm after two seconds without a shot.");

        PlayerConditionalPowerUpRuntimeUtility.TryConsumeQualifiedShot(in config, ref runtimeState);
        PlayerConditionalPowerUpRuntimeUtility.UpdateSuddenStrike(in config,
                                                                  0.1f,
                                                                  in movementState,
                                                                  in lookState,
                                                                  1u,
                                                                  ref runtimeState);

        if (runtimeState.Charge > PrecisionEpsilon || runtimeState.Armed != 0)
            throw new Exception("A real shot did not restart the Sudden Strike no-shooting charge window.");
    }

    /// <summary>
    /// Verifies qualified Character Tuning rebuilds only the current projectile configuration and leaves persistent scalable stats unchanged.
    /// </summary>
    private static void ValidateConditionalCharacterTuning()
    {
        World world = new World("ConditionalCharacterTuningSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity playerEntity = entityManager.CreateEntity();
            entityManager.AddBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity);
            entityManager.AddBuffer<PlayerPowerUpCharacterTuningFormulaElement>(playerEntity);
            entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);
            entityManager.AddBuffer<PlayerRuntimeControllerScalingElement>(playerEntity);
            entityManager.AddBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
            entityManager.AddBuffer<PlayerRuntimeComboRankElement>(playerEntity);

            // Acquire all handles after structural changes so their safety versions remain valid throughout the test.
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = entityManager.GetBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity);
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = entityManager.GetBuffer<PlayerPowerUpCharacterTuningFormulaElement>(playerEntity);
            DynamicBuffer<PlayerScalableStatElement> scalableStats = entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
            DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling = entityManager.GetBuffer<PlayerRuntimeControllerScalingElement>(playerEntity);
            DynamicBuffer<PlayerRoomRewardTemporaryModifierElement> temporaryModifiers = entityManager.GetBuffer<PlayerRoomRewardTemporaryModifierElement>(playerEntity);
            DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks = entityManager.GetBuffer<PlayerRuntimeComboRankElement>(playerEntity);
            characterTuningFormulas.Add(new PlayerPowerUpCharacterTuningFormulaElement
            {
                Formula = new FixedString128Bytes("[BulletSizeMultiplier]=[BulletSizeMultiplier]*3")
            });
            characterTuningFormulas.Add(new PlayerPowerUpCharacterTuningFormulaElement
            {
                Formula = new FixedString128Bytes("[ShotRange]=[ShotRange]+1")
            });
            scalableStats.Add(new PlayerScalableStatElement
            {
                Name = new FixedString64Bytes("BulletSizeMultiplier"),
                Type = (byte)PlayerScalableStatType.Float,
                MinimumValue = 0.01f,
                MaximumValue = 100f,
                Value = 1f
            });
            scalableStats.Add(new PlayerScalableStatElement
            {
                Name = new FixedString64Bytes("ShotRange"),
                Type = (byte)PlayerScalableStatType.Float,
                MinimumValue = 0f,
                MaximumValue = 10000f,
                Value = 1f
            });
            controllerScaling.Add(new PlayerRuntimeControllerScalingElement
            {
                FieldId = PlayerRuntimeControllerFieldId.ShootingProjectileSizeMultiplier,
                ValueType = (byte)PlayerFormulaValueType.Number,
                BaseValue = 1f,
                Formula = new FixedString512Bytes("[this]*[BulletSizeMultiplier]")
            });
            controllerScaling.Add(new PlayerRuntimeControllerScalingElement
            {
                FieldId = PlayerRuntimeControllerFieldId.ShootingRange,
                ValueType = (byte)PlayerFormulaValueType.Number,
                BaseValue = 10f,
                Formula = new FixedString512Bytes("[this]*[ShotRange]")
            });
            unlockCatalog.ResizeUninitialized(1);
            ref PlayerPowerUpUnlockCatalogElement catalogEntry = ref unlockCatalog.ElementAt(0);
            catalogEntry = default;
            catalogEntry.PowerUpId = new FixedString64Bytes("SmokeConditionalTuning");
            catalogEntry.UnlockKind = PlayerPowerUpUnlockKind.Passive;
            catalogEntry.CurrentUnlockCount = 1;
            catalogEntry.CharacterTuningFormulaStartIndex = 0;
            catalogEntry.CharacterTuningFormulaCount = 2;
            catalogEntry.PassiveToolConfig.ConditionalApplication.Mode = PowerUpConditionalApplicationMode.SuddenStrike;
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> readOnlyUnlockCatalog = entityManager.GetBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity,
                                                                                                                                                true);
            PlayerRoomRewardTemporaryState temporaryState = default;
            PlayerRuntimeComboCounterConfig runtimeComboConfig = default;
            PlayerComboCounterState comboState = default;
            PlayerConditionalCharacterTuningContext context = new PlayerConditionalCharacterTuningContext(readOnlyUnlockCatalog,
                                                                                                             characterTuningFormulas,
                                                                                                             scalableStats,
                                                                                                             controllerScaling,
                                                                                                             temporaryModifiers,
                                                                                                             in temporaryState,
                                                                                                             runtimeComboRanks,
                                                                                                             in runtimeComboConfig,
                                                                                                             in comboState);
            bool shotContextInitialized = false;

            if (!PlayerConditionalCharacterTuningRuntimeUtility.TryAccumulate(new FixedString64Bytes("SmokeConditionalTuning"),
                                                                              in context,
                                                                              ref shotContextInitialized))
            {
                throw new Exception("Conditional Character Tuning did not apply its qualified formula range.");
            }

            PlayerRuntimeShootingConfig baselineShootingConfig = new PlayerRuntimeShootingConfig
            {
                Values = new ShootingValuesBlob
                {
                    ProjectileSizeMultiplier = 1f,
                    Range = 10f
                }
            };
            PlayerConditionalCharacterTuningRuntimeUtility.RebuildShootingConfig(in baselineShootingConfig,
                                                                                 in context,
                                                                                 out PlayerRuntimeShootingConfig shotShootingConfig);

            if (math.abs(shotShootingConfig.Values.ProjectileSizeMultiplier - 3f) > PrecisionEpsilon ||
                math.abs(shotShootingConfig.Values.Range - 20f) > PrecisionEpsilon ||
                math.abs(scalableStats[0].Value - 1f) > PrecisionEpsilon ||
                math.abs(scalableStats[1].Value - 1f) > PrecisionEpsilon)
            {
                throw new Exception("Conditional Character Tuning did not remain isolated to the qualified projectile configuration.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies percentage and direct Self-Preservation Instinct thresholds use authoritative health values.
    /// </summary>
    private static void ValidateSelfPreservationThreshold()
    {
        PowerUpConditionalApplicationConfig config = new PowerUpConditionalApplicationConfig
        {
            Mode = PowerUpConditionalApplicationMode.SelfPreservationInstinct,
            HealthThresholdMode = SelfPreservationHealthThresholdMode.MaximumHealthPercent,
            HealthThreshold = 25f
        };

        if (!PlayerConditionalPowerUpRuntimeUtility.HasReachedSelfPreservationThreshold(in config, 25f, 100f) ||
            PlayerConditionalPowerUpRuntimeUtility.HasReachedSelfPreservationThreshold(in config, 26f, 100f))
        {
            throw new Exception("Self-Preservation Instinct percentage threshold was resolved incorrectly.");
        }

        config.HealthThresholdMode = SelfPreservationHealthThresholdMode.CurrentHealthValue;
        config.HealthThreshold = 12f;

        if (!PlayerConditionalPowerUpRuntimeUtility.HasReachedSelfPreservationThreshold(in config, 12f, 250f) ||
            PlayerConditionalPowerUpRuntimeUtility.HasReachedSelfPreservationThreshold(in config, 13f, 250f))
        {
            throw new Exception("Self-Preservation Instinct direct-health threshold was resolved incorrectly.");
        }
    }

    /// <summary>
    /// Verifies finite toggle lifetimes expire exactly once and unlimited toggles avoid retaining elapsed state.
    /// </summary>
    private static void ValidateFiniteToggleLifetime()
    {
        PowerUpConditionalApplicationRuntimeState runtimeState = default;

        if (PlayerPowerUpToggleLifetimeUtility.Tick(2f, 1f, ref runtimeState) ||
            !PlayerPowerUpToggleLifetimeUtility.Tick(2f, 1f, ref runtimeState) ||
            runtimeState.ToggleActiveElapsedSeconds > PrecisionEpsilon)
        {
            throw new Exception("Finite toggle lifetime did not expire and reset at its configured duration.");
        }

        runtimeState.ToggleActiveElapsedSeconds = 1f;

        if (PlayerPowerUpToggleLifetimeUtility.Tick(0f, 1f, ref runtimeState) ||
            runtimeState.ToggleActiveElapsedSeconds > PrecisionEpsilon)
        {
            throw new Exception("Unlimited toggle lifetime retained unnecessary elapsed state.");
        }
    }

    /// <summary>
    /// Verifies conditional passives stay out of the shared aggregate and merge only after explicit qualification.
    /// </summary>
    private static void ValidateConditionalAggregationIsolation()
    {
        PlayerPassiveToolConfig passiveTool = new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            HasShotgun = 1,
            Shotgun = new ShotgunPowerUpConfig
            {
                ProjectileCount = 3,
                ConeAngleDegrees = 25f
            },
            ConditionalApplication = new PowerUpConditionalApplicationConfig
            {
                Mode = PowerUpConditionalApplicationMode.DelayedShootApplication,
                DelayedShotInterval = 2
            }
        };
        PlayerPassiveToolsState passiveState = new PlayerPassiveToolsState
        {
            ProjectileSizeMultiplier = 1f,
            ProjectileDamageMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            ProjectileLifetimeSecondsMultiplier = 1f,
            ProjectileLifetimeRangeMultiplier = 1f
        };
        PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref passiveState, in passiveTool);

        if (passiveState.HasShotgun != 0)
            throw new Exception("Conditional projectile effects leaked into the shared passive aggregate.");

        PlayerPassiveToolsAggregationUtility.AccumulateConditionalPassiveTool(ref passiveState, in passiveTool);

        if (passiveState.HasShotgun == 0 || passiveState.Shotgun.ProjectileCount != 3)
            throw new Exception("A qualified conditional projectile effect was not merged into its shot snapshot.");
    }

    /// <summary>
    /// Verifies module payload copy and paste restore values only for a compatible runtime module kind.
    /// </summary>
    private static void ValidatePayloadClipboard()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            preset.EnsureDefaultModularSetup();
            int suddenStrikeModuleIndex = -1;

            // Resolve the default Sudden Strike definition by runtime kind so catalog ordering can evolve safely.
            for (int moduleIndex = 0; moduleIndex < preset.ModuleDefinitions.Count; moduleIndex++)
            {
                PowerUpModuleDefinition definition = preset.ModuleDefinitions[moduleIndex];

                if (definition != null && definition.ModuleKind == PowerUpModuleKind.SuddenStrike)
                {
                    suddenStrikeModuleIndex = moduleIndex;
                    break;
                }
            }

            if (suddenStrikeModuleIndex < 0)
                throw new Exception("Default Sudden Strike module was unavailable for clipboard validation.");

            PowerUpSuddenStrikeModuleData payload = preset.ModuleDefinitions[suddenStrikeModuleIndex].Data.SuddenStrike;
            payload.Configure(SuddenStrikeChargeConditionMode.NotShooting,
                              false,
                              0.15f,
                              2f,
                              false,
                              0.4f);

            if (!PowerUpModulePayloadClipboardUtility.CopyDefinitionPayload(preset, suddenStrikeModuleIndex))
                throw new Exception("Module payload clipboard did not copy a valid definition payload.");

            payload.Configure(SuddenStrikeChargeConditionMode.Stationary,
                              true,
                              1f,
                              10f,
                              true,
                              2f);

            if (!PowerUpModulePayloadClipboardUtility.PasteDefinitionPayload(preset, suddenStrikeModuleIndex))
                throw new Exception("Module payload clipboard did not paste a compatible definition payload.");

            payload = preset.ModuleDefinitions[suddenStrikeModuleIndex].Data.SuddenStrike;

            if (payload.ConditionMode != SuddenStrikeChargeConditionMode.NotShooting ||
                math.abs(payload.StationarySpeedTolerance - 0.15f) > PrecisionEpsilon ||
                math.abs(payload.MovementSlowRecoverySeconds - 0.4f) > PrecisionEpsilon)
            {
                throw new Exception("Module payload clipboard did not restore the copied values.");
            }

            if (PowerUpModulePayloadClipboardUtility.CanPaste(PowerUpModuleKind.SelfPreservationInstinct))
                throw new Exception("Module payload clipboard enabled paste for a different module kind.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }
    #endregion

    #endregion
}
#endif
