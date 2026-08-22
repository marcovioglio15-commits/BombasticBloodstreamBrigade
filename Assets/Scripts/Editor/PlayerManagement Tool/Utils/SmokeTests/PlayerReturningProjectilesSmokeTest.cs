using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Installs baseline returning-projectile content and validates authoring, bake, scaling, filtering, and transition behavior.
/// </summary>
public static class PlayerReturningProjectilesSmokeTest
{
    #region Constants
    private const string MainPresetPath = "Assets/Scriptable Objects/Player/Power-Ups/PlayerPowerUpsPreset.asset";
    private const string BoomerangMeshPath = "Assets/3D/Meshes/PowerUpsMeshes/SM_PowerUp_Gigabomb.fbx";
    private const float PrecisionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Entry Point
    // [MenuItem("Tools/Player/Run Returning Projectiles Smoke Test")]
    /// <summary>
    /// Adds missing baseline content to the main preset and runs deterministic editor checks.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        PlayerPowerUpsPreset preset = AssetDatabase.LoadAssetAtPath<PlayerPowerUpsPreset>(MainPresetPath);

        if (preset == null)
            throw new InvalidOperationException("The main Player Power-Ups preset could not be loaded.");

        if (PlayerReturningProjectilesPresetDefaultsUtility.EnsureContent(preset))
        {
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
        }

        ValidateBaselineBake(preset);
        RunDeterministicChecks();
        Debug.Log("[PlayerReturningProjectilesSmokeTest] Baseline content and all Returning Projectiles checks passed.");
    }

    /// <summary>
    /// Runs Returning Projectiles editor and runtime checks without reading or modifying authored preset content.
    /// </summary>
    public static void RunDeterministicChecks()
    {
        ValidateAuthoringScalingTargets();
        ValidateRuntimeScalingPaths();
        ValidateSourceFiltering();
        ValidateProjectileSizeFiltering();
        ValidateReplacementProjectileFootprint();
        PlayerReturningProjectileInteractionSmokeTest.Run();
        ValidateSpecializedPoolExpansionPolicy();
        ValidatePoolStoragePartitioning();
        ValidateReturnTransition();
        PlayerReturningProjectileRecallSmokeTest.Run();
        PlayerReturningProjectileContactDamageSmokeTest.Run();
        PlayerReturnCameraShakeRuntimeSmokeTest.Run();
        GameSceneTransitionGameplayRuntimeCleanupSmokeTest.Run();
        Debug.Log("[PlayerReturningProjectilesSmokeTest] Deterministic Returning Projectiles checks passed.");
    }
    #endregion

    #region Preset and Bake
    /// <summary>
    /// Verifies both baseline definitions compile to their intended unmanaged runtime configurations.
    /// </summary>
    /// <param name="preset">Main project preset containing installed baseline definitions.</param>
    private static void ValidateBaselineBake(PlayerPowerUpsPreset preset)
    {
        ModularPowerUpDefinition boomerang = FindPowerUp(preset.ActivePowerUps,
                                                         PlayerReturningProjectilesPresetDefaultsUtility.BoomerangPowerUpId);
        ModularPowerUpDefinition twoStepTreatment = FindPowerUp(preset.PassivePowerUps,
                                                                PlayerReturningProjectilesPresetDefaultsUtility.TwoStepTreatmentPowerUpId);

        if (boomerang == null || twoStepTreatment == null)
            throw new InvalidOperationException("Boomerang or Two-Step Treatment is missing from the main preset.");

        PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(null,
                                                                         preset,
                                                                         boomerang,
                                                                         projectilePrefab => Entity.Null,
                                                                         out PlayerPowerUpSlotConfig activeConfig);
        PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(null,
                                                                                  preset,
                                                                                  twoStepTreatment,
                                                                                  projectilePrefab => Entity.Null,
                                                                                  out PlayerPassiveToolConfig passiveConfig);

        if (activeConfig.IsDefined == 0 ||
            activeConfig.ToolKind != ActiveToolKind.ReturningProjectile ||
            activeConfig.HasReturningProjectiles == 0 ||
            activeConfig.ReturningProjectiles.ReturnPathMode != ProjectileReturnPathMode.SeekPlayer ||
            activeConfig.ReturningProjectiles.ReturnHitPolicy != ProjectileReturnHitPolicy.CompleteReturn ||
            activeConfig.ReturningProjectiles.OwningPowerUpId.ToString() != PlayerReturningProjectilesPresetDefaultsUtility.BoomerangPowerUpId ||
            activeConfig.ReturningProjectiles.AllowConcurrentActiveProjectiles != 0 ||
            activeConfig.Toggleable != 0 ||
            activeConfig.ActivationResource != PowerUpResourceType.Energy)
        {
            throw new InvalidOperationException($"Boomerang baseline mismatch: Defined={activeConfig.IsDefined}, Tool={activeConfig.ToolKind}, " +
                                                $"HasReturning={activeConfig.HasReturningProjectiles}, Path={activeConfig.ReturningProjectiles.ReturnPathMode}, " +
                                                $"HitPolicy={activeConfig.ReturningProjectiles.ReturnHitPolicy}, StartMode={activeConfig.ReturningProjectiles.ReturnStartMode}, " +
                                                $"PowerUpId={activeConfig.ReturningProjectiles.OwningPowerUpId}, " +
                                                $"Concurrent={activeConfig.ReturningProjectiles.AllowConcurrentActiveProjectiles}, " +
                                                $"Toggleable={activeConfig.Toggleable}, Resource={activeConfig.ActivationResource}.");
        }

        if (passiveConfig.IsDefined == 0 ||
            passiveConfig.ToolKind != PassiveToolKind.ReturningProjectiles ||
            passiveConfig.HasReturningProjectiles == 0 ||
            passiveConfig.ReturningProjectiles.ReturnPathMode != ProjectileReturnPathMode.RetraceOutboundPath ||
            passiveConfig.ReturningProjectiles.ReturnStartMode != ProjectileReturnStartMode.AutomaticDelay ||
            passiveConfig.ReturningProjectiles.KeepProjectileVfx == 0 ||
            passiveConfig.ReturningProjectiles.AllowOtherPowerUpInteractions == 0 ||
            passiveConfig.ReturningProjectiles.EnableProjectileSplitting == 0 ||
            passiveConfig.ReturningProjectiles.ApplyToSplitProjectiles == 0 ||
            passiveConfig.ReturningProjectiles.CompleteBouncesBeforeReturn == 0 ||
            passiveConfig.ReturningProjectiles.CompleteOrbitalPathBeforeReturn == 0 ||
            passiveConfig.ReturningProjectiles.ApplyTinyMegaProjectileScaling == 0 ||
            passiveConfig.ReturningProjectiles.OwningPowerUpId.ToString() != PlayerReturningProjectilesPresetDefaultsUtility.TwoStepTreatmentPowerUpId)
        {
            throw new InvalidOperationException("Two-Step Treatment did not bake with complete split, bounce, orbit, and retrace defaults.");
        }
    }

    /// <summary>
    /// Finds a composed power-up by its stable identifier.
    /// </summary>
    /// <param name="definitions">Power-up definitions to inspect.</param>
    /// <param name="powerUpId">Stable identifier to match.</param>
    /// <returns>Matching definition, or null when no entry matches.</returns>
    private static ModularPowerUpDefinition FindPowerUp(IReadOnlyList<ModularPowerUpDefinition> definitions,
                                                        string powerUpId)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            ModularPowerUpDefinition definition = definitions[index];

            if (definition != null &&
                definition.CommonData != null &&
                string.Equals(definition.CommonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
            {
                return definition;
            }
        }

        return null;
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Verifies every numeric, Boolean, and enum payload setting is a unified Add Scaling target.
    /// </summary>
    private static void ValidateAuthoringScalingTargets()
    {
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();

        try
        {
            PlayerReturningProjectilesPresetDefaultsUtility.EnsureContent(preset);
            SerializedObject serializedPreset = new SerializedObject(preset);
            SerializedProperty moduleDefinitions = serializedPreset.FindProperty("moduleDefinitions");
            SerializedProperty payload = null;

            for (int index = 0; index < moduleDefinitions.arraySize; index++)
            {
                SerializedProperty module = moduleDefinitions.GetArrayElementAtIndex(index);

                if (module.FindPropertyRelative("moduleId").stringValue != PlayerReturningProjectilesPresetDefaultsUtility.ModuleId)
                    continue;

                payload = module.FindPropertyRelative("data").FindPropertyRelative("returningProjectiles");
                break;
            }

            if (payload == null)
                throw new InvalidOperationException("Returning Projectiles serialized payload is missing.");

            string[] scalableFields =
            {
                "keepProjectileVfx",
                "keepMuzzleFlashVfx",
                "keepHitVfx",
                "keepDeathVfx",
                "returnPathMode",
                "returnSpeedMultiplier",
                "outboundRangeMultiplier",
                "outboundLifetimeMultiplier",
                "outboundHitPolicy",
                "additionalOutboundHits",
                "returnStartMode",
                "returnDelaySeconds",
                "allowEarlyActivationRecall",
                "reapplyResourceGateCostOnRecall",
                "resourceReturnThresholdPercent",
                "stolenOwnershipPolicy",
                "returnRumbleMultiplier",
                "returnCameraShakeMultiplier",
                "outboundSizeMultiplier",
                "returnSizeMultiplier",
                "spinDuringFlight",
                "spinSpeedDegreesPerSecond",
                "spinAxis",
                "turnaroundRotationSpeedDegreesPerSecond",
                "turnaroundAxis",
                "returnHitPolicy",
                "additionalReturnHits",
                "enableRepeatedContactDamage",
                "repeatedContactDamage",
                "repeatedContactDamageIntervalSeconds",
                "pathSampleDistance",
                "returnCompletionDistance",
                "allowOtherPowerUpInteractions",
                "enableProjectileSplitting",
                "applyToSplitProjectiles",
                "completeBouncesBeforeReturn",
                "completeOrbitalPathBeforeReturn",
                "applyTinyMegaProjectileScaling",
                "applyToActivePowerUpProjectiles",
                "allowConcurrentActiveProjectiles"
            };

            for (int index = 0; index < scalableFields.Length; index++)
            {
                if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(payload.FindPropertyRelative(scalableFields[index])))
                    throw new InvalidOperationException("Returning Projectiles field is not exposed through Add Scaling: " + scalableFields[index]);
            }

            PlayerReturningProjectileRecallSmokeTest.ValidateAuthoringUi(payload, serializedPreset);

            // Cross-power-up options must disappear as a group while same-power-up composition remains baked separately.
            payload.FindPropertyRelative("allowOtherPowerUpInteractions").boolValue = false;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            VisualElement payloadContainer = new VisualElement();
            PowerUpReturningProjectilesPayloadDrawerUtility.Build(payloadContainer, payload, false);
            VisualElement externalOptions = payloadContainer.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.OtherInteractionOptionsContainerName);
            VisualElement projectileVfxOptions = payloadContainer.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.ProjectileVfxOptionsContainerName);
            VisualElement additionalOutboundHits = payloadContainer.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.AdditionalOutboundHitsContainerName);
            VisualElement repeatedContactDamageSettings = payloadContainer.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.RepeatedContactDamageSettingsContainerName);

            if (externalOptions == null || externalOptions.style.display.value != DisplayStyle.None)
                throw new InvalidOperationException("Returning Projectiles external interaction options remained visible after their master toggle was disabled.");

            if (projectileVfxOptions == null || projectileVfxOptions.style.display.value != DisplayStyle.None)
                throw new InvalidOperationException("Returning Projectiles displayed its replacement-only projectile VFX option without a replacement prefab.");

            if (additionalOutboundHits == null || additionalOutboundHits.style.display.value != DisplayStyle.None)
                throw new InvalidOperationException("Returning Projectiles displayed its additional outbound hit budget for a non-limited policy.");

            if (repeatedContactDamageSettings == null || repeatedContactDamageSettings.style.display.value != DisplayStyle.None)
                throw new InvalidOperationException("Returning Projectiles displayed repeated contact damage settings while their toggle was disabled.");

            // The additional hit budget becomes relevant only for the limited outbound policy.
            payload.FindPropertyRelative("outboundHitPolicy").enumValueIndex = (int)ProjectileOutboundHitPolicy.LimitedAdditionalHits;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            VisualElement limitedOutboundPayloadContainer = new VisualElement();
            PowerUpReturningProjectilesPayloadDrawerUtility.Build(limitedOutboundPayloadContainer, payload, false);
            additionalOutboundHits = limitedOutboundPayloadContainer.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.AdditionalOutboundHitsContainerName);

            if (additionalOutboundHits == null || additionalOutboundHits.style.display.value != DisplayStyle.Flex)
                throw new InvalidOperationException("Returning Projectiles hid its additional outbound hit budget for the limited policy.");

            // Damage amount and cadence become relevant only after their explicit runtime toggle is enabled.
            payload.FindPropertyRelative("enableRepeatedContactDamage").boolValue = true;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            VisualElement repeatedDamagePayloadContainer = new VisualElement();
            PowerUpReturningProjectilesPayloadDrawerUtility.Build(repeatedDamagePayloadContainer, payload, false);
            repeatedContactDamageSettings = repeatedDamagePayloadContainer.Q<VisualElement>(PowerUpReturningProjectilesPayloadDrawerUtility.RepeatedContactDamageSettingsContainerName);

            if (repeatedContactDamageSettings == null || repeatedContactDamageSettings.style.display.value != DisplayStyle.Flex)
                throw new InvalidOperationException("Returning Projectiles hid repeated contact damage settings after their toggle was enabled.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Verifies enum, numeric, and Boolean formula results reach active and passive unmanaged configs.
    /// </summary>
    private static void ValidateRuntimeScalingPaths()
    {
        PlayerPowerUpSlotConfig activeConfig = new PlayerPowerUpSlotConfig
        {
            HasReturningProjectiles = 1,
            HasResourceGate = 1,
            ReturningProjectiles = new ReturningProjectilesConfig
            {
                KeepProjectileVfx = 1,
                KeepMuzzleFlashVfx = 1,
                KeepHitVfx = 1,
                KeepDeathVfx = 1,
                AllowOtherPowerUpInteractions = 1,
                EnableProjectileSplitting = 1,
                ApplyTinyMegaProjectileScaling = 1
            }
        };
        PlayerPassiveToolConfig passiveConfig = new PlayerPassiveToolConfig
        {
            HasReturningProjectiles = 1
        };

        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnPathMode",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           (float)ProjectileReturnPathMode.SeekPlayer,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnSpeedMultiplier",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           2.5f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.outboundRangeMultiplier",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           1.75f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.outboundLifetimeMultiplier",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           1.5f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.outboundHitPolicy",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           (float)ProjectileOutboundHitPolicy.LimitedAdditionalHits,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.additionalOutboundHits",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           4f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnDelaySeconds",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           0.4f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnStartMode",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           (float)ProjectileReturnStartMode.AutomaticDelayOrActivationTapOrResourceDrain,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.resourceReturnThresholdPercent",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           35f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.stolenOwnershipPolicy",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           (float)ProjectileStolenOwnershipPolicy.PreserveAndReconnect,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnRumbleMultiplier",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           0.65f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnCameraShakeMultiplier",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           0.8f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.allowConcurrentActiveProjectiles",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                   ref activeConfig,
                                                                   ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.allowEarlyActivationRecall",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.reapplyResourceGateCostOnRecall",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.enableRepeatedContactDamage",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.repeatedContactDamage",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           6f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.repeatedContactDamageIntervalSeconds",
                                                           PlayerPowerUpUnlockKind.Active,
                                                           0.3f,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.allowOtherPowerUpInteractions",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.keepProjectileVfx",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.keepMuzzleFlashVfx",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.keepHitVfx",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.keepDeathVfx",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.enableProjectileSplitting",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.applyTinyMegaProjectileScaling",
                                                                  PlayerPowerUpUnlockKind.Active,
                                                                  false,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnHitPolicy",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           (float)ProjectileReturnHitPolicy.LimitedAdditionalHits,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyValue("returningProjectiles.returnStartMode",
                                                           PlayerPowerUpUnlockKind.Passive,
                                                           (float)ProjectileReturnStartMode.ResourceDrain,
                                                           ref activeConfig,
                                                           ref passiveConfig);
        PlayerRuntimePowerUpScalingPathUtility.ApplyBooleanValue("returningProjectiles.applyToSplitProjectiles",
                                                                  PlayerPowerUpUnlockKind.Passive,
                                                                  true,
                                                                  ref activeConfig,
                                                                  ref passiveConfig);

        if (activeConfig.ReturningProjectiles.ReturnPathMode != ProjectileReturnPathMode.SeekPlayer ||
            math.abs(activeConfig.ReturningProjectiles.ReturnSpeedMultiplier - 2.5f) > PrecisionEpsilon ||
            math.abs(activeConfig.ReturningProjectiles.OutboundRangeMultiplier - 1.75f) > PrecisionEpsilon ||
            math.abs(activeConfig.ReturningProjectiles.OutboundLifetimeMultiplier - 1.5f) > PrecisionEpsilon ||
            activeConfig.ReturningProjectiles.OutboundHitPolicy != ProjectileOutboundHitPolicy.LimitedAdditionalHits ||
            activeConfig.ReturningProjectiles.AdditionalOutboundHits != 4 ||
            activeConfig.ReturningProjectiles.ReturnStartMode != ProjectileReturnStartMode.AutomaticDelayOrActivationTapOrResourceDrain ||
            math.abs(activeConfig.ReturningProjectiles.ResourceReturnThresholdPercent - 35f) > PrecisionEpsilon ||
            activeConfig.ReturningProjectiles.StolenOwnershipPolicy != ProjectileStolenOwnershipPolicy.PreserveAndReconnect ||
            math.abs(activeConfig.ReturningProjectiles.ReturnDelaySeconds - 0.4f) > PrecisionEpsilon ||
            math.abs(activeConfig.ReturningProjectiles.ReturnRumbleMultiplier - 0.65f) > PrecisionEpsilon ||
            math.abs(activeConfig.ReturningProjectiles.ReturnCameraShakeMultiplier - 0.8f) > PrecisionEpsilon ||
            activeConfig.ReturningProjectiles.AllowConcurrentActiveProjectiles == 0 ||
            activeConfig.ReturningProjectiles.AllowEarlyActivationRecall == 0 ||
            activeConfig.ReturningProjectiles.ReapplyResourceGateCostOnRecall == 0 ||
            activeConfig.ReturningProjectiles.EnableRepeatedContactDamage == 0 ||
            math.abs(activeConfig.ReturningProjectiles.RepeatedContactDamage - 6f) > PrecisionEpsilon ||
            math.abs(activeConfig.ReturningProjectiles.RepeatedContactDamageIntervalSeconds - 0.3f) > PrecisionEpsilon ||
            activeConfig.ReturningProjectiles.KeepProjectileVfx != 0 ||
            activeConfig.ReturningProjectiles.KeepMuzzleFlashVfx != 0 ||
            activeConfig.ReturningProjectiles.KeepHitVfx != 0 ||
            activeConfig.ReturningProjectiles.KeepDeathVfx != 0 ||
            activeConfig.ReturningProjectiles.AllowOtherPowerUpInteractions != 0 ||
            activeConfig.ReturningProjectiles.EnableProjectileSplitting != 0 ||
            activeConfig.ReturningProjectiles.ApplyTinyMegaProjectileScaling != 0 ||
            passiveConfig.ReturningProjectiles.ReturnHitPolicy != ProjectileReturnHitPolicy.LimitedAdditionalHits ||
            passiveConfig.ReturningProjectiles.ReturnStartMode != ProjectileReturnStartMode.AutomaticDelay ||
            passiveConfig.ReturningProjectiles.ApplyToSplitProjectiles == 0)
        {
            throw new InvalidOperationException("Returning Projectiles runtime formula paths did not update typed configs.");
        }
    }

    /// <summary>
    /// Verifies Tiny/Mega-style size sources obey the dedicated gate and retain only same-power-up provenance when external interactions are disabled.
    /// </summary>
    private static void ValidateProjectileSizeFiltering()
    {
        World world = new World("ReturningProjectilesSizeInteractionSmokeTest");

        try
        {
            Entity entity = world.EntityManager.CreateEntity();
            DynamicBuffer<PlayerProjectileSizePowerUpMultiplierElement> sources = world.EntityManager.AddBuffer<PlayerProjectileSizePowerUpMultiplierElement>(entity);
            sources.Add(new PlayerProjectileSizePowerUpMultiplierElement
            {
                PowerUpId = new Unity.Collections.FixedString64Bytes("Passive_MegaProjectiles"),
                Multiplier = 2f
            });
            sources.Add(new PlayerProjectileSizePowerUpMultiplierElement
            {
                PowerUpId = new Unity.Collections.FixedString64Bytes(PlayerReturningProjectilesPresetDefaultsUtility.TwoStepTreatmentPowerUpId),
                Multiplier = 1.5f
            });
            ReturningProjectilesConfig config = new ReturningProjectilesConfig
            {
                ApplyTinyMegaProjectileScaling = 1,
                AllowOtherPowerUpInteractions = 1,
                OwningPowerUpId = new Unity.Collections.FixedString64Bytes(PlayerReturningProjectilesPresetDefaultsUtility.TwoStepTreatmentPowerUpId)
            };

            if (math.abs(ProjectileReturnPowerUpInteractionUtility.ResolveProjectileSizePowerUpMultiplier(in config,
                                                                                                            3f,
                                                                                                            sources) - 3f) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("Returning Projectiles did not retain enabled external Tiny/Mega projectile scaling.");
            }

            config.AllowOtherPowerUpInteractions = 0;

            if (math.abs(ProjectileReturnPowerUpInteractionUtility.ResolveProjectileSizePowerUpMultiplier(in config,
                                                                                                            3f,
                                                                                                            sources) - 1.5f) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("Returning Projectiles did not isolate same-power-up projectile-size tuning from external Tiny/Mega sources.");
            }

            config.ApplyTinyMegaProjectileScaling = 0;

            if (math.abs(ProjectileReturnPowerUpInteractionUtility.ResolveProjectileSizePowerUpMultiplier(in config,
                                                                                                            3f,
                                                                                                            sources) - 1f) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("Returning Projectiles retained Tiny/Mega projectile scaling after its dedicated interaction was disabled.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies the replacement mesh exposes a non-zero rotation-invariant footprint for wall sweeps.
    /// </summary>
    private static void ValidateReplacementProjectileFootprint()
    {
        GameObject boomerangMesh = AssetDatabase.LoadAssetAtPath<GameObject>(BoomerangMeshPath);

        if (boomerangMesh == null || PlayerProjectilePrefabFootprintBakeUtility.ResolvePlanarRadius(boomerangMesh) <= 0.05f)
            throw new InvalidOperationException("Boomerang replacement geometry did not produce a usable baked wall-collision footprint.");
    }
    #endregion

    #region Runtime Policies
    /// <summary>
    /// Verifies a high-throughput base pool batch can never multiply a one-shot specialized prefab request.
    /// </summary>
    private static void ValidateSpecializedPoolExpansionPolicy()
    {
        Entity basePrefabEntity = new Entity
        {
            Index = 1,
            Version = 1
        };
        Entity replacementPrefabEntity = new Entity
        {
            Index = 2,
            Version = 1
        };

        if (ProjectileSpawnPoolSelectionUtility.ResolveExpansionCount(replacementPrefabEntity,
                                                                      basePrefabEntity,
                                                                      1,
                                                                      1500) != 1)
        {
            throw new InvalidOperationException("A one-shot returning replacement inherited the base pool's 1500-projectile expansion batch.");
        }

        if (ProjectileSpawnPoolSelectionUtility.ResolveExpansionCount(basePrefabEntity,
                                                                      basePrefabEntity,
                                                                      3,
                                                                      128) != 128)
        {
            throw new InvalidOperationException("The high-throughput base projectile lost its configured pool expansion batch.");
        }
    }

    /// <summary>
    /// Verifies passive source filters and explicit active overrides resolve without per-frame discovery.
    /// </summary>
    private static void ValidateSourceFiltering()
    {
        PlayerPassiveToolsState passiveState = new PlayerPassiveToolsState
        {
            HasReturningProjectiles = 1,
            ReturningProjectiles = new ReturningProjectilesConfig
            {
                AllowOtherPowerUpInteractions = 1,
                ApplyToSplitProjectiles = 0,
                ApplyToActivePowerUpProjectiles = 0
            }
        };
        ShootRequest request = new ShootRequest
        {
            SpawnSource = ProjectileSpawnSource.BaseShot
        };

        if (!ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request, in passiveState, out ReturningProjectilesConfig _))
            throw new InvalidOperationException("Returning passive did not apply to normal player shots.");

        request.SpawnSource = ProjectileSpawnSource.SplitProjectile;

        if (ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request, in passiveState, out ReturningProjectilesConfig _))
            throw new InvalidOperationException("Projectile Split source filter was ignored.");

        request.SpawnSource = ProjectileSpawnSource.ActivePowerUp;

        if (ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request, in passiveState, out ReturningProjectilesConfig _))
            throw new InvalidOperationException("Other active-projectile source filter was ignored.");

        // Disabling the master gate must reject external sources even if their individual policies remain authored.
        passiveState.ReturningProjectiles = new ReturningProjectilesConfig
        {
            AllowOtherPowerUpInteractions = 0,
            ApplyToSplitProjectiles = 1,
            ApplyToActivePowerUpProjectiles = 1
        };
        request.SpawnSource = ProjectileSpawnSource.SplitProjectile;

        if (ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request, in passiveState, out ReturningProjectilesConfig _))
            throw new InvalidOperationException("Returning Projectiles accepted an external split source while cross-power-up interactions were disabled.");

        // Baked same-power-up provenance bypasses only the external master gate and preserves local module composition.
        passiveState.ReturningProjectiles.SamePowerUpHasProjectileSplit = 1;

        if (!ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request, in passiveState, out ReturningProjectilesConfig _))
            throw new InvalidOperationException("Returning Projectiles rejected a Projectile Split module composed inside the same power-up.");

        passiveState.ReturningProjectiles.ApplyToSplitProjectiles = 0;

        if (ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request, in passiveState, out ReturningProjectilesConfig _))
            throw new InvalidOperationException("The Projectile Split policy was ignored for a module composed inside the same power-up.");

        request.SpawnSource = ProjectileSpawnSource.ActivePowerUp;
        request.HasReturningProjectilesOverride = 1;
        request.ReturningProjectilesOverride = new ReturningProjectilesConfig
        {
            ReturnPathMode = ProjectileReturnPathMode.SeekPlayer
        };

        if (!ProjectileSpawnPoolSelectionUtility.TryResolveReturningProjectiles(in request,
                                                                                 in passiveState,
                                                                                 out ReturningProjectilesConfig overrideConfig) ||
            overrideConfig.ReturnPathMode != ProjectileReturnPathMode.SeekPlayer)
        {
            throw new InvalidOperationException("Explicit active Returning Projectiles override did not bypass passive source filtering.");
        }
    }

    /// <summary>
    /// Verifies prewarmed player pools receive return storage while enemy pools retain their smaller archetype.
    /// </summary>
    private static void ValidatePoolStoragePartitioning()
    {
        World world = new World("ReturningProjectilesPoolStorageSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity projectilePrefab = entityManager.CreateEntity(typeof(Prefab), typeof(LocalTransform));
            Entity playerShooter = entityManager.CreateEntity(typeof(PlayerControllerConfig));
            Entity enemyShooter = entityManager.CreateEntity();
            entityManager.AddBuffer<ProjectilePoolElement>(playerShooter);
            entityManager.AddBuffer<ProjectilePoolElement>(enemyShooter);

            ProjectilePoolUtility.ExpandPool(entityManager, playerShooter, projectilePrefab, 1);
            ProjectilePoolUtility.ExpandPool(entityManager, enemyShooter, projectilePrefab, 1);

            ProjectilePoolElement playerPoolElement = entityManager.GetBuffer<ProjectilePoolElement>(playerShooter)[0];
            ProjectilePoolElement enemyPoolElement = entityManager.GetBuffer<ProjectilePoolElement>(enemyShooter)[0];

            if (playerPoolElement.PrefabEntity != projectilePrefab ||
                !entityManager.HasComponent<ProjectileReturnState>(playerPoolElement.ProjectileEntity) ||
                !entityManager.HasBuffer<ProjectileReturnPathPoint>(playerPoolElement.ProjectileEntity))
            {
                throw new InvalidOperationException("Prewarmed player projectiles did not receive prefab-partitioned return storage.");
            }

            if (enemyPoolElement.PrefabEntity != projectilePrefab ||
                entityManager.HasComponent<ProjectileReturnState>(enemyPoolElement.ProjectileEntity) ||
                entityManager.HasBuffer<ProjectileReturnPathPoint>(enemyPoolElement.ProjectileEntity))
            {
                throw new InvalidOperationException("Enemy projectile pools unexpectedly received player-only return storage.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Verifies turnaround, scale, speed, natural penetration preservation, and limited additional-hit activation.
    /// </summary>
    private static void ValidateReturnTransition()
    {
        World world = new World("ReturningProjectilesTransitionSmokeTest");

        try
        {
            Entity entity = world.EntityManager.CreateEntity();
            DynamicBuffer<ProjectileReturnPathPoint> path = world.EntityManager.AddBuffer<ProjectileReturnPathPoint>(entity);
            path.Add(new ProjectileReturnPathPoint
            {
                Position = float3.zero
            });
            ProjectileReturnState returnState = new ProjectileReturnState
            {
                Enabled = 1,
                Phase = ProjectileReturnPhase.Outbound,
                OutboundSpeed = 5f,
                OriginalDamage = 12f,
                OriginalPenetrationMode = ProjectilePenetrationMode.FixedHits,
                Config = new ReturningProjectilesConfig
                {
                    ReturnSpeedMultiplier = 2f,
                    ReturnDelaySeconds = 0.5f,
                    ReturnRumbleMultiplier = 0.75f,
                    ReturnCameraShakeMultiplier = 0.6f,
                    OutboundSizeMultiplier = 2f,
                    ReturnSizeMultiplier = 3f,
                    SpinDuringFlight = 0,
                    TurnaroundRotationSpeedDegreesPerSecond = 720f,
                    ReturnHitPolicy = ProjectileReturnHitPolicy.LimitedAdditionalHits,
                    AdditionalReturnHits = 2,
                    PathSampleDistance = 0.25f
                }
            };
            Projectile projectile = new Projectile
            {
                Velocity = new float3(0f, 0f, 7f),
                Damage = 1f,
                PenetrationMode = ProjectilePenetrationMode.FixedHits
            };
            ProjectilePerfectCircleState perfectCircleState = new ProjectilePerfectCircleState
            {
                Enabled = 1
            };
            LocalTransform transform = LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 5f),
                                                                                quaternion.identity,
                                                                                2f);

            ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                        ref projectile,
                                                        ref perfectCircleState,
                                                         ref transform,
                                                         path,
                                                         true,
                                                         false);

            if (returnState.Phase != ProjectileReturnPhase.Delaying ||
                math.abs(returnState.OutboundSpeed - 7f) > PrecisionEpsilon ||
                math.abs(returnState.ReturnDelayRemainingSeconds - 0.5f) > PrecisionEpsilon ||
                math.abs(transform.Scale - 3f) > PrecisionEpsilon ||
                math.lengthsq(projectile.Velocity) > PrecisionEpsilon ||
                math.abs(projectile.Damage - 12f) > PrecisionEpsilon ||
                projectile.PenetrationMode != ProjectilePenetrationMode.FixedHits ||
                projectile.RemainingPenetrations != 1 ||
                perfectCircleState.Enabled != 0)
            {
                throw new InvalidOperationException("Returning projectile transition did not apply its configured runtime state.");
            }

            ProjectileOwner projectileOwner = default;
            ComponentLookup<LocalToWorld> ownerWorldTransformLookup = default;
            ProjectileReturnRuntimeUtility.SimulateReturn(ref returnState,
                                                           ref projectile,
                                                           ref transform,
                                                           in projectileOwner,
                                                           path,
                                                           in ownerWorldTransformLookup,
                                                           0.25f);

            if (returnState.Phase != ProjectileReturnPhase.Delaying ||
                math.abs(returnState.ReturnDelayRemainingSeconds - 0.25f) > PrecisionEpsilon ||
                math.lengthsq(projectile.Velocity) > PrecisionEpsilon)
            {
                throw new InvalidOperationException("Returning projectile latency did not preserve its stationary delay.");
            }

            ProjectileReturnRuntimeUtility.SimulateReturn(ref returnState,
                                                           ref projectile,
                                                           ref transform,
                                                           in projectileOwner,
                                                           path,
                                                           in ownerWorldTransformLookup,
                                                           0.25f);
            ProjectileReturnRuntimeUtility.SimulateReturn(ref returnState,
                                                           ref projectile,
                                                           ref transform,
                                                           in projectileOwner,
                                                           path,
                                                           in ownerWorldTransformLookup,
                                                           0.25f);

            if (returnState.Phase != ProjectileReturnPhase.Returning ||
                !ProjectileReturnRuntimeUtility.TryConsumeReturnFeedbackRequest(ref returnState,
                                                                                 out float returnCameraShakeMultiplier,
                                                                                 out float returnRumbleMultiplier) ||
                math.abs(returnCameraShakeMultiplier - 0.6f) > PrecisionEpsilon ||
                math.abs(returnRumbleMultiplier - 0.75f) > PrecisionEpsilon ||
                ProjectileReturnRuntimeUtility.TryConsumeReturnFeedbackRequest(ref returnState,
                                                                                 out float _,
                                                                                 out float _))
            {
                throw new InvalidOperationException("Returning projectile did not emit exactly one camera-and-haptic event after delay and turnaround.");
            }

            // A range-triggered return must consume the remaining natural capacity before enabling its additional budget.
            path.Clear();
            path.Add(new ProjectileReturnPathPoint
            {
                Position = float3.zero
            });
            returnState = new ProjectileReturnState
            {
                Enabled = 1,
                Phase = ProjectileReturnPhase.Outbound,
                OriginalDamage = 12f,
                OriginalPenetrationMode = ProjectilePenetrationMode.DamageBased,
                Config = new ReturningProjectilesConfig
                {
                    ReturnSpeedMultiplier = 1f,
                    OutboundSizeMultiplier = 1f,
                    ReturnSizeMultiplier = 1f,
                    SpinDuringFlight = 1,
                    SpinSpeedDegreesPerSecond = 360f,
                    ReturnHitPolicy = ProjectileReturnHitPolicy.LimitedAdditionalHits,
                    AdditionalReturnHits = 2,
                    PathSampleDistance = 0.25f
                }
            };
            projectile = new Projectile
            {
                Velocity = new float3(0f, 0f, 7f),
                Damage = 4f,
                PenetrationMode = ProjectilePenetrationMode.DamageBased,
                RemainingPenetrations = 3
            };
            perfectCircleState = default;
            transform = LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 5f),
                                                                  quaternion.identity,
                                                                  1f);
            ProjectileReturnRuntimeUtility.BeginReturn(ref returnState,
                                                        ref projectile,
                                                        ref perfectCircleState,
                                                         ref transform,
                                                         path,
                                                         false,
                                                         false);

            if (projectile.PenetrationMode != ProjectilePenetrationMode.DamageBased ||
                projectile.RemainingPenetrations != 3 ||
                math.abs(projectile.Damage - 4f) > PrecisionEpsilon ||
                returnState.AdditionalReturnHitsRemaining != 2)
            {
                throw new InvalidOperationException("Range-triggered return discarded remaining natural penetration capacity.");
            }

            if (!ProjectileReturnRuntimeUtility.TryActivateAdditionalReturnHits(ref returnState, ref projectile) ||
                projectile.PenetrationMode != ProjectilePenetrationMode.FixedHits ||
                projectile.RemainingPenetrations != 1 ||
                math.abs(projectile.Damage - 12f) > PrecisionEpsilon ||
                returnState.AdditionalReturnHitsRemaining != 0)
            {
                throw new InvalidOperationException("Limited return did not activate its full-damage additional hit budget.");
            }

            // A retraced bounce segment must rotate the entire current spin pose into its new travel heading.
            returnState.LastTravelDirection = new float3(0f, 0f, 1f);
            transform.Rotation = quaternion.AxisAngle(math.up(), math.radians(30f));
            float3 expectedFacing = math.rotate(quaternion.AxisAngle(math.up(), math.radians(90f)),
                                                math.forward(transform.Rotation));
            ProjectileReturnRuntimeUtility.AlignFlightRotation(ref transform,
                                                                ref returnState,
                                                                new float3(1f, 0f, 0f),
                                                                0f);

            if (math.lengthsq(math.forward(transform.Rotation) - expectedFacing) > PrecisionEpsilon)
                throw new InvalidOperationException("Returning projectile rotation did not realign after a retraced bounce segment change.");
        }
        finally
        {
            world.Dispose();
        }
    }

    #endregion

    #endregion
}
