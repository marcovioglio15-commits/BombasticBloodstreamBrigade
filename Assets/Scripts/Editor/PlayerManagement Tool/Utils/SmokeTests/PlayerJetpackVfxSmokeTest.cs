#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Unity.Transforms;

/// <summary>
/// Runs deterministic editor checks for Jetpack VFX authoring, scaling, activity gating, and Visual Player toggling.
/// </summary>
public static class PlayerJetpackVfxSmokeTest
{
    #region Constants
    private const string LevelStatName = "Level";
    private const string JetpackReferenceA = "Visuals/JetpackA";
    private const string JetpackReferenceB = "Visuals/JetpackB";
    #endregion

    #region Methods

    #region Public Methods
    // [MenuItem("Tools/Player/Run Jetpack VFX Smoke Test")]
    /// <summary>
    /// Executes the Jetpack VFX smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateAuthoringAndBakeScaling();
        ValidateRuntimeScalingRebuild();
        ValidateActivityGating();
        ValidateManagedVisualPlayerToggle();
        Debug.Log("[PlayerJetpackVfxSmokeTest] All Jetpack VFX checks passed.");
    }
    #endregion

    #region Authoring And Bake Scaling
    /// <summary>
    /// Validates scalable serialized fields, Visual Player reference baking, metadata, and bake-time formula application.
    /// </summary>
    private static void ValidateAuthoringAndBakeScaling()
    {
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        GameObject visualPrefab = BuildVisualPlayerPrefab("JetpackAuthoringVisual");
        World metadataWorld = new World("PlayerJetpackVfxMetadataSmokeTest");

        try
        {
            SerializedObject serializedPreset = new SerializedObject(visualPreset);
            serializedPreset.FindProperty("runtimeVisualBridgePrefab").objectReferenceValue = visualPrefab;
            SerializedProperty settings = serializedPreset.FindProperty("playerJetpackVfx");

            if (settings == null)
                throw new Exception("Player Jetpack VFX serialized settings are missing.");

            settings.FindPropertyRelative("runtimeReference").stringValue = JetpackReferenceA;
            settings.FindPropertyRelative("activationMode").enumValueIndex = (int)PlayerJetpackVfxActivationMode.WhileMoving;
            settings.FindPropertyRelative("movementSpeedThreshold").floatValue = 0.25f;
            settings.FindPropertyRelative("rotationSpeedThresholdDegrees").floatValue = 4f;
            settings.FindPropertyRelative("scaleWithMovementSpeed").boolValue = true;
            settings.FindPropertyRelative("speedForMaximumScale").floatValue = 8f;
            settings.FindPropertyRelative("normalScaleSpeedPercent").floatValue = 40f;
            settings.FindPropertyRelative("scaleVariationPercent").floatValue = 50f;
            ValidateScalableFields(settings);

            SerializedProperty scalingRules = serializedPreset.FindProperty("scalingRules");
            scalingRules.arraySize = 6;
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(0),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("runtimeReference")),
                                 "switch([this], \"Visuals/JetpackA\":\"Visuals/JetpackB\", [this])");
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(1),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("activationMode")),
                                 "[this] + 2");
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(2),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("scaleWithMovementSpeed")),
                                 "[this]");
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(3),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("speedForMaximumScale")),
                                 "[this] + 2");
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(4),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("normalScaleSpeedPercent")),
                                 "[this] + 2");
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(5),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("scaleVariationPercent")),
                                 "[this] + 25");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            PlayerJetpackVfxConfig config = PlayerVisualVfxBakeUtility.BuildJetpackVfxConfig(visualPreset);

            if (!config.RuntimeReference.Equals(new FixedString128Bytes(JetpackReferenceA)) ||
                config.ActivationMode != PlayerJetpackVfxActivationMode.WhileMoving ||
                config.ScaleWithMovementSpeed == 0 ||
                !Mathf.Approximately(config.SpeedForMaximumScale, 8f) ||
                !Mathf.Approximately(config.NormalScaleSpeedPercent, 40f) ||
                !Mathf.Approximately(config.ScaleVariationPercent, 50f))
                throw new Exception("Jetpack VFX bake did not preserve the -authored Visual Player reference.");

            Entity metadataEntity = metadataWorld.EntityManager.CreateEntity();
            DynamicBuffer<PlayerRuntimeJetpackVfxScalingElement> metadata =
                metadataWorld.EntityManager.AddBuffer<PlayerRuntimeJetpackVfxScalingElement>(metadataEntity);
            PlayerRuntimeScalingVisualBakeUtility.PopulateJetpackVfxScalingMetadata(visualPreset, metadata);

            if (metadata.Length != 6 ||
                (PlayerFormulaValueType)metadata[0].ValueType != PlayerFormulaValueType.Token ||
                !metadata[0].BaseTokenValue.Equals(new FixedString128Bytes(JetpackReferenceA)) ||
                (PlayerFormulaValueType)metadata[2].ValueType != PlayerFormulaValueType.Boolean ||
                metadata[2].BaseBooleanValue == 0)
                throw new Exception("Jetpack VFX runtime Add Scaling metadata did not preserve token, enum, boolean, and numeric fields.");

            using (PlayerScaledPresetScope scaledScope = PlayerPresetScalingBakeUtility.CreateScope(null,
                                                                                                     null,
                                                                                                     null,
                                                                                                     visualPreset,
                                                                                                     null,
                                                                                                     null))
            {
                PlayerJetpackVfxSettings scaledSettings = scaledScope.VisualPreset.PlayerJetpackVfx;

                if (!string.Equals(scaledSettings.RuntimeReference, JetpackReferenceB, StringComparison.Ordinal) ||
                    scaledSettings.ActivationMode != PlayerJetpackVfxActivationMode.WhileMovingOrRotating ||
                    !scaledSettings.ScaleWithMovementSpeed ||
                    !Mathf.Approximately(scaledSettings.SpeedForMaximumScale, 10f) ||
                    !Mathf.Approximately(scaledSettings.NormalScaleSpeedPercent, 42f) ||
                    !Mathf.Approximately(scaledSettings.ScaleVariationPercent, 75f))
                    throw new Exception("Jetpack VFX bake-time Add Scaling did not update token, enum, boolean, and numeric fields.");
            }
        }
        finally
        {
            metadataWorld.Dispose();
            UnityEngine.Object.DestroyImmediate(visualPrefab);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Asserts every behavioral Jetpack field exposed by the tool is compatible with unified Add Scaling formulas.
    /// </summary>
    /// <param name="settings">Serialized Jetpack VFX settings property.</param>
    private static void ValidateScalableFields(SerializedProperty settings)
    {
        if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("runtimeReference")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("activationMode")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("movementSpeedThreshold")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("rotationSpeedThresholdDegrees")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("scaleWithMovementSpeed")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("speedForMaximumScale")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("normalScaleSpeedPercent")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("scaleVariationPercent")))
            throw new Exception("Jetpack VFX fields are not fully supported by Add Scaling.");
    }

    /// <summary>
    /// Writes one enabled unified scaling formula into a temporary Visual Preset rule.
    /// </summary>
    /// <param name="ruleProperty">Serialized scaling rule property.</param>
    /// <param name="statKey">Stable target stat key.</param>
    /// <param name="formula">Unified formula expression.</param>
    private static void ConfigureScalingRule(SerializedProperty ruleProperty, string statKey, string formula)
    {
        ruleProperty.FindPropertyRelative("statKey").stringValue = statKey;
        ruleProperty.FindPropertyRelative("addScaling").boolValue = true;
        ruleProperty.FindPropertyRelative("formula").stringValue = formula;
    }
    #endregion

    #region Runtime Scaling
    /// <summary>
    /// Validates hash-gated baseline rebuild and token, enum, and numeric runtime formula application.
    /// </summary>
    private static void ValidateRuntimeScalingRebuild()
    {
        World world = new World("PlayerJetpackVfxScalingSmokeTest");
        EntityManager entityManager = world.EntityManager;
        Entity playerEntity = entityManager.CreateEntity();
        Entity visualRuntimeEntity = entityManager.CreateEntity();

        try
        {
            PlayerJetpackVfxConfig baseConfig = BuildRuntimeConfig();
            entityManager.AddComponentData(visualRuntimeEntity, new PlayerVisualRuntimeDataOwner
            {
                PlayerEntity = playerEntity
            });
            entityManager.AddComponentData(visualRuntimeEntity, new PlayerBaseJetpackVfxConfig
            {
                Config = baseConfig
            });
            entityManager.AddComponentData(visualRuntimeEntity, baseConfig);
            entityManager.AddComponentData(visualRuntimeEntity, new PlayerJetpackVfxScalingState());
            entityManager.AddComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 1u
            });
            entityManager.AddBuffer<PlayerRuntimeJetpackVfxScalingElement>(visualRuntimeEntity);
            entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);

            DynamicBuffer<PlayerRuntimeJetpackVfxScalingElement> scaling =
                entityManager.GetBuffer<PlayerRuntimeJetpackVfxScalingElement>(visualRuntimeEntity);
            DynamicBuffer<PlayerScalableStatElement> scalableStats =
                entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
            scaling.Add(BuildTokenScalingElement("runtimeReference",
                                                 JetpackReferenceA,
                                                 "switch([Level], 2:\"Visuals/JetpackB\", [this])"));
            scaling.Add(BuildNumericScalingElement("activationMode", 0f, true, "[this] + [Level]"));
            scaling.Add(BuildNumericScalingElement("movementSpeedThreshold", 1f, false, "[this] + [Level]"));
            scaling.Add(BuildBooleanScalingElement("scaleWithMovementSpeed", false, "[Level] > 0"));
            scaling.Add(BuildNumericScalingElement("speedForMaximumScale", 8f, false, "[this] + [Level]"));
            scaling.Add(BuildNumericScalingElement("normalScaleSpeedPercent", 40f, false, "[this] + [Level]"));
            scaling.Add(BuildNumericScalingElement("scaleVariationPercent", 50f, false, "[this] + [Level]"));
            scalableStats.Add(new PlayerScalableStatElement
            {
                Name = new FixedString64Bytes(LevelStatName),
                Type = (byte)PlayerScalableStatType.Integer,
                MinimumValue = 0f,
                MaximumValue = 100f,
                Value = 2f
            });

            SystemHandle scalingSystem = world.GetOrCreateSystem<PlayerRuntimeJetpackVfxScalingSystem>();
            scalingSystem.Update(world.Unmanaged);
            AssertRuntimeScaling(entityManager.GetComponentData<PlayerJetpackVfxConfig>(visualRuntimeEntity),
                                 JetpackReferenceB,
                                 PlayerJetpackVfxActivationMode.WhileRotating,
                                 3f,
                                 true,
                                 10f,
                                 42f,
                                 52f);

            PlayerScalableStatElement levelStat = scalableStats[0];
            levelStat.Value = 3f;
            scalableStats[0] = levelStat;
            entityManager.SetComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 2u
            });
            scalingSystem.Update(world.Unmanaged);
            AssertRuntimeScaling(entityManager.GetComponentData<PlayerJetpackVfxConfig>(visualRuntimeEntity),
                                 JetpackReferenceA,
                                 PlayerJetpackVfxActivationMode.WhileMovingOrRotating,
                                 4f,
                                 true,
                                 11f,
                                 43f,
                                 53f);
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Builds deterministic runtime Jetpack VFX settings for scaling and activity tests.
    /// </summary>
    /// <returns>Runtime config using movement-based activation.</returns>
    private static PlayerJetpackVfxConfig BuildRuntimeConfig()
    {
        return new PlayerJetpackVfxConfig
        {
            RuntimeReference = new FixedString128Bytes(JetpackReferenceA),
            MovementSpeedThreshold = 1f,
            RotationSpeedThresholdDegrees = 1f,
            SpeedForMaximumScale = 4f,
            NormalScaleSpeedPercent = 50f,
            ScaleVariationPercent = 100f,
            ActivationMode = PlayerJetpackVfxActivationMode.WhileMoving,
            ScaleWithMovementSpeed = 1
        };
    }

    /// <summary>
    /// Builds one boolean runtime scaling metadata entry.
    /// </summary>
    /// <param name="path">Runtime payload path.</param>
    /// <param name="baseValue">Immutable baseline boolean.</param>
    /// <param name="formula">Unified boolean formula.</param>
    /// <returns>Configured boolean scaling metadata.</returns>
    private static PlayerRuntimeJetpackVfxScalingElement BuildBooleanScalingElement(string path,
                                                                                    bool baseValue,
                                                                                    string formula)
    {
        return new PlayerRuntimeJetpackVfxScalingElement
        {
            PayloadPath = new FixedString128Bytes(path),
            ValueType = (byte)PlayerFormulaValueType.Boolean,
            BaseBooleanValue = baseValue ? (byte)1 : (byte)0,
            IsInteger = 1,
            Formula = new FixedString512Bytes(formula)
        };
    }

    /// <summary>
    /// Builds one token runtime scaling metadata entry.
    /// </summary>
    /// <param name="path">Runtime payload path.</param>
    /// <param name="baseValue">Immutable baseline token.</param>
    /// <param name="formula">Unified token formula.</param>
    /// <returns>Configured token scaling metadata.</returns>
    private static PlayerRuntimeJetpackVfxScalingElement BuildTokenScalingElement(string path,
                                                                                  string baseValue,
                                                                                  string formula)
    {
        return new PlayerRuntimeJetpackVfxScalingElement
        {
            PayloadPath = new FixedString128Bytes(path),
            ValueType = (byte)PlayerFormulaValueType.Token,
            BaseTokenValue = new FixedString128Bytes(baseValue),
            Formula = new FixedString512Bytes(formula)
        };
    }

    /// <summary>
    /// Builds one numeric runtime scaling metadata entry.
    /// </summary>
    /// <param name="path">Runtime payload path.</param>
    /// <param name="baseValue">Immutable baseline value.</param>
    /// <param name="isInteger">True when formula output must resolve as an integer-like value.</param>
    /// <param name="formula">Unified numeric formula.</param>
    /// <returns>Configured numeric scaling metadata.</returns>
    private static PlayerRuntimeJetpackVfxScalingElement BuildNumericScalingElement(string path,
                                                                                    float baseValue,
                                                                                    bool isInteger,
                                                                                    string formula)
    {
        return new PlayerRuntimeJetpackVfxScalingElement
        {
            PayloadPath = new FixedString128Bytes(path),
            ValueType = (byte)PlayerFormulaValueType.Number,
            BaseValue = baseValue,
            IsInteger = isInteger ? (byte)1 : (byte)0,
            Formula = new FixedString512Bytes(formula)
        };
    }

    /// <summary>
    /// Asserts one runtime scaling result after a hash-triggered rebuild.
    /// </summary>
    /// <param name="config">Runtime config to inspect.</param>
    /// <param name="expectedReference">Expected Visual Player runtime reference.</param>
    /// <param name="expectedActivationMode">Expected runtime activation mode.</param>
    /// <param name="expectedMovementThreshold">Expected runtime movement threshold.</param>
    /// <param name="expectedScaleWithMovementSpeed">Expected movement-speed scaling toggle.</param>
    /// <param name="expectedSpeedForMaximumScale">Expected custom speed at which the VFX reaches maximum configured size.</param>
    /// <param name="expectedNormalScaleSpeedPercent">Expected reference-speed percentage preserving authored scale.</param>
    /// <param name="expectedScaleVariationPercent">Expected total scale variation percentage.</param>
    private static void AssertRuntimeScaling(PlayerJetpackVfxConfig config,
                                             string expectedReference,
                                             PlayerJetpackVfxActivationMode expectedActivationMode,
                                             float expectedMovementThreshold,
                                             bool expectedScaleWithMovementSpeed,
                                             float expectedSpeedForMaximumScale,
                                             float expectedNormalScaleSpeedPercent,
                                             float expectedScaleVariationPercent)
    {
        if (!config.RuntimeReference.Equals(new FixedString128Bytes(expectedReference)) ||
            config.ActivationMode != expectedActivationMode ||
            !Mathf.Approximately(config.MovementSpeedThreshold, expectedMovementThreshold) ||
            (config.ScaleWithMovementSpeed != 0) != expectedScaleWithMovementSpeed ||
            !Mathf.Approximately(config.SpeedForMaximumScale, expectedSpeedForMaximumScale) ||
            !Mathf.Approximately(config.NormalScaleSpeedPercent, expectedNormalScaleSpeedPercent) ||
            !Mathf.Approximately(config.ScaleVariationPercent, expectedScaleVariationPercent))
            throw new Exception("Jetpack VFX runtime scaling did not rebuild from immutable baseline metadata.");
    }
    #endregion

    #region Activity
    /// <summary>
    /// Validates movement, rotation, and constant modes publish the expected desired visibility without spawn requests.
    /// </summary>
    private static void ValidateActivityGating()
    {
        World world = new World("PlayerJetpackVfxActivitySmokeTest");
        world.SetTime(new TimeData(1d / 60d, 1f / 60f));
        EntityManager entityManager = world.EntityManager;
        Entity playerEntity = entityManager.CreateEntity(typeof(PlayerMovementState),
                                                          typeof(LocalTransform));
        Entity visualRuntimeEntity = entityManager.CreateEntity(typeof(PlayerVisualRuntimeDataOwner),
                                                                 typeof(PlayerJetpackVfxConfig),
                                                                 typeof(PlayerJetpackVfxRuntimeState));

        try
        {
            PlayerJetpackVfxConfig config = BuildRuntimeConfig();
            entityManager.SetComponentData(visualRuntimeEntity, new PlayerVisualRuntimeDataOwner
            {
                PlayerEntity = playerEntity
            });
            entityManager.SetComponentData(visualRuntimeEntity, config);
            entityManager.SetComponentData(playerEntity, new PlayerMovementState());
            entityManager.SetComponentData(playerEntity, LocalTransform.Identity);
            SystemHandle jetpackSystem = world.GetOrCreateSystem<PlayerJetpackVfxSystem>();
            jetpackSystem.Update(world.Unmanaged);
            AssertDesiredVisibility(entityManager, visualRuntimeEntity, false, "stationary movement-only");
            AssertDesiredScaleMultiplier(entityManager, visualRuntimeEntity, 0.5f, "stationary shrink");

            entityManager.SetComponentData(playerEntity, new PlayerMovementState
            {
                Velocity = new float3(2f, 0f, 0f)
            });
            jetpackSystem.Update(world.Unmanaged);
            AssertDesiredVisibility(entityManager, visualRuntimeEntity, true, "moving movement-only");
            AssertDesiredScaleMultiplier(entityManager, visualRuntimeEntity, 1f, "normal scale speed");

            entityManager.SetComponentData(playerEntity, new PlayerMovementState
            {
                Velocity = new float3(4f, 0f, 0f)
            });
            jetpackSystem.Update(world.Unmanaged);
            AssertDesiredScaleMultiplier(entityManager, visualRuntimeEntity, 1.5f, "maximum-size speed growth");

            config.ActivationMode = PlayerJetpackVfxActivationMode.WhileRotating;
            entityManager.SetComponentData(visualRuntimeEntity, config);
            entityManager.SetComponentData(playerEntity, new PlayerMovementState());
            jetpackSystem.Update(world.Unmanaged);
            AssertDesiredVisibility(entityManager, visualRuntimeEntity, false, "rotation baseline");

            entityManager.SetComponentData(playerEntity,
                                           LocalTransform.FromPositionRotationScale(float3.zero,
                                                                                    quaternion.RotateY(math.radians(90f)),
                                                                                    1f));
            jetpackSystem.Update(world.Unmanaged);
            AssertDesiredVisibility(entityManager, visualRuntimeEntity, true, "active rotation");

            config.ActivationMode = PlayerJetpackVfxActivationMode.Always;
            entityManager.SetComponentData(visualRuntimeEntity, config);
            jetpackSystem.Update(world.Unmanaged);
            AssertDesiredVisibility(entityManager, visualRuntimeEntity, true, "always-visible");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Asserts the Jetpack activity system published the expected -scale multiplier.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player state.</param>
    /// <param name="playerEntity">Visual runtime entity to inspect.</param>
    /// <param name="expectedScaleMultiplier">Expected multiplier over the -authored local scale.</param>
    /// <param name="caseLabel">Scale case label included in failure messages.</param>
    private static void AssertDesiredScaleMultiplier(EntityManager entityManager,
                                                     Entity playerEntity,
                                                     float expectedScaleMultiplier,
                                                     string caseLabel)
    {
        float scaleMultiplier = entityManager.GetComponentData<PlayerJetpackVfxRuntimeState>(playerEntity).DesiredScaleMultiplier;

        if (!Mathf.Approximately(scaleMultiplier, expectedScaleMultiplier))
            throw new Exception(string.Format("Jetpack VFX desired scale multiplier failed for {0}.", caseLabel));
    }

    /// <summary>
    /// Asserts the Jetpack activity system published the expected visibility state.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player state.</param>
    /// <param name="playerEntity">Visual runtime entity to inspect.</param>
    /// <param name="expectedVisible">Expected desired visibility.</param>
    /// <param name="caseLabel">Activity case label included in failure messages.</param>
    private static void AssertDesiredVisibility(EntityManager entityManager,
                                                Entity playerEntity,
                                                bool expectedVisible,
                                                string caseLabel)
    {
        bool visible = entityManager.GetComponentData<PlayerJetpackVfxRuntimeState>(playerEntity).DesiredVisible != 0;

        if (visible != expectedVisible)
            throw new Exception(string.Format("Jetpack VFX desired visibility failed for {0}.", caseLabel));
    }
    #endregion

    #region Visual Player Presentation
    /// <summary>
    /// Validates the runtime Visual Player child is toggled in place without modifying its -authored local pose.
    /// </summary>
    private static void ValidateManagedVisualPlayerToggle()
    {
        World world = new World("PlayerJetpackVfxVisualPlayerSmokeTest");
        EntityManager entityManager = world.EntityManager;
        entityManager.CreateEntity(typeof(GameSceneManagerConfig));
        GameObject visualPrefab = BuildVisualPlayerPrefab("JetpackRuntimeVisual");
        GameObject companionVfx = new GameObject("BakedCompanionJetpackVfx");
        ParticleSystem companionParticleSystem = companionVfx.AddComponent<ParticleSystem>();
        GameObject runtimeVisual = null;
        Entity playerEntity = entityManager.CreateEntity(typeof(PlayerControllerConfig),
                                                          typeof(PlayerPresentationRuntimeReferences),
                                                          typeof(LocalTransform));
        Entity visualRuntimeEntity = entityManager.CreateEntity(typeof(PlayerVisualRuntimeDataOwner),
                                                                 typeof(PlayerVisualRuntimeBridgeConfig),
                                                                 typeof(PlayerJetpackVfxConfig),
                                                                 typeof(PlayerJetpackVfxRuntimeState));
        Entity companionEntity = entityManager.CreateEntity();

        try
        {
            entityManager.AddComponentObject(companionEntity, companionParticleSystem);
            entityManager.AddBuffer<Child>(playerEntity).Add(new Child
            {
                Value = companionEntity
            });
            entityManager.SetComponentData(playerEntity, new PlayerPresentationRuntimeReferences
            {
                VisualRuntimeEntity = visualRuntimeEntity
            });
            entityManager.SetComponentData(visualRuntimeEntity, new PlayerVisualRuntimeBridgeConfig
            {
                VisualPrefab = visualPrefab,
                SyncRotation = 1,
                SpawnWhenAnimatorMissing = 1
            });
            entityManager.SetComponentData(visualRuntimeEntity, new PlayerVisualRuntimeDataOwner
            {
                PlayerEntity = playerEntity
            });
            entityManager.SetComponentData(visualRuntimeEntity, new PlayerJetpackVfxConfig
            {
                RuntimeReference = new FixedString128Bytes(JetpackReferenceA)
            });
            entityManager.SetComponentData(playerEntity, LocalTransform.Identity);

            SystemHandle bridgeSystem = world.GetOrCreateSystem<PlayerManagedVisualAnimatorBridgeSystem>();
            SystemHandle presentationSystem = world.GetOrCreateSystem<PlayerJetpackVfxPresentationSystem>();
            bridgeSystem.Update(world.Unmanaged);
            presentationSystem.Update(world.Unmanaged);
            runtimeVisual = GameObject.Find(visualPrefab.name + "_RuntimeVisual");
            Transform jetpackTransform = ResolveRequiredJetpackTransform(runtimeVisual);
            Vector3 authoredLocalPosition = jetpackTransform.localPosition;
            Vector3 authoredLocalScale = jetpackTransform.localScale;

            if (companionVfx.activeSelf)
                throw new Exception("Runtime Visual Player bridge did not suspend the duplicate baked companion Jetpack VFX.");

            if (jetpackTransform.gameObject.activeSelf)
                throw new Exception("-authored Jetpack VFX was not disabled for a false desired state.");

            entityManager.SetComponentData(visualRuntimeEntity, new PlayerJetpackVfxRuntimeState
            {
                DesiredVisible = 1,
                DesiredScaleMultiplier = 0.5f
            });
            presentationSystem.Update(world.Unmanaged);

            if (!jetpackTransform.gameObject.activeSelf)
                throw new Exception("-authored Jetpack VFX was not enabled for a true desired state.");

            if (Vector3.Distance(jetpackTransform.localScale, authoredLocalScale * 0.5f) > 0.0001f)
                throw new Exception("-authored Jetpack VFX did not apply the requested shrink multiplier.");

            entityManager.SetComponentData(visualRuntimeEntity, new PlayerJetpackVfxRuntimeState
            {
                DesiredVisible = 1,
                DesiredScaleMultiplier = 1.5f
            });
            presentationSystem.Update(world.Unmanaged);

            if (Vector3.Distance(jetpackTransform.localScale, authoredLocalScale * 1.5f) > 0.0001f)
                throw new Exception("-authored Jetpack VFX did not apply the requested growth multiplier.");

            entityManager.SetComponentData(playerEntity,
                                           LocalTransform.FromPositionRotationScale(new float3(10f, 0f, 0f),
                                                                                    quaternion.RotateY(math.radians(45f)),
                                                                                    1f));
            bridgeSystem.Update(world.Unmanaged);
            presentationSystem.Update(world.Unmanaged);

            if (Vector3.Distance(jetpackTransform.localPosition, authoredLocalPosition) > 0.0001f)
                throw new Exception("Jetpack VFX local pose changed while the Visual Player moved.");

            entityManager.SetComponentData(visualRuntimeEntity, new PlayerJetpackVfxConfig());
            presentationSystem.Update(world.Unmanaged);

            if (Vector3.Distance(jetpackTransform.localScale, authoredLocalScale) > 0.0001f)
                throw new Exception("Jetpack VFX did not restore its -authored local scale when its binding was removed.");

            entityManager.SetComponentData(visualRuntimeEntity, new PlayerVisualRuntimeBridgeConfig());
            bridgeSystem.Update(world.Unmanaged);

            if (!companionVfx.activeSelf)
                throw new Exception("Baked companion Jetpack VFX did not restore its authored active state after runtime bridge removal.");
        }
        finally
        {
            if (runtimeVisual != null)
                UnityEngine.Object.DestroyImmediate(runtimeVisual);

            world.Dispose();
            UnityEngine.Object.DestroyImmediate(companionVfx);
            UnityEngine.Object.DestroyImmediate(visualPrefab);
        }
    }

    /// <summary>
    /// Builds one Visual Player prefab-like hierarchy containing an Animator and two -authored Jetpack VFX objects.
    /// </summary>
    /// <param name="name">Root object name.</param>
    /// <returns>Configured Visual Player hierarchy.</returns>
    private static GameObject BuildVisualPlayerPrefab(string name)
    {
        GameObject visualRoot = new GameObject(name);
        visualRoot.AddComponent<Animator>();
        visualRoot.AddComponent<PlayerWeaponVisualSet>();
        GameObject baseGun = new GameObject("base gun");
        baseGun.transform.SetParent(visualRoot.transform, false);
        GameObject visualsRoot = new GameObject("Visuals");
        visualsRoot.transform.SetParent(visualRoot.transform, false);
        GameObject jetpackA = new GameObject("JetpackA");
        jetpackA.transform.SetParent(visualsRoot.transform, false);
        jetpackA.transform.localPosition = new Vector3(0.75f, 1.25f, -0.5f);
        jetpackA.transform.localScale = new Vector3(0.25f, 0.5f, 0.75f);
        GameObject jetpackB = new GameObject("JetpackB");
        jetpackB.transform.SetParent(visualsRoot.transform, false);
        return visualRoot;
    }

    /// <summary>
    /// Resolves the required JetpackA object from a runtime Visual Player hierarchy.
    /// </summary>
    /// <param name="runtimeVisual">Runtime Visual Player root.</param>
    /// <returns>Resolved -authored Jetpack transform.</returns>
    private static Transform ResolveRequiredJetpackTransform(GameObject runtimeVisual)
    {
        if (runtimeVisual == null ||
            !PlayerWeaponVisualReferenceUtility.TryResolve(runtimeVisual.transform,
                                                           JetpackReferenceA,
                                                           out Transform jetpackTransform))
            throw new Exception("Runtime Visual Player did not contain the -authored Jetpack VFX.");

        return jetpackTransform;
    }
    #endregion

    #endregion
}
#endif
