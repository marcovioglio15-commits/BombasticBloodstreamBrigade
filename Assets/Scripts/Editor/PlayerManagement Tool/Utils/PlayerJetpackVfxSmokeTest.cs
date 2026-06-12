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
/// Runs deterministic editor checks for Jetpack VFX authoring, scaling, activity gating, and managed follow rotation.
/// </summary>
public static class PlayerJetpackVfxSmokeTest
{
    #region Constants
    private const string LevelStatName = "Level";
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
        ValidateActivityGatingAndManagedFollow();
        Debug.Log("[PlayerJetpackVfxSmokeTest] All Jetpack VFX checks passed.");
    }
    #endregion

    #region Authoring And Bake Scaling
    /// <summary>
    /// Validates serialized Add Scaling targets, config bake, and bake-time formula application.
    /// </summary>
    private static void ValidateAuthoringAndBakeScaling()
    {
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        GameObject vfxPrefab = new GameObject("JetpackVfxAuthoringPrefab");

        try
        {
            SerializedObject serializedPreset = new SerializedObject(visualPreset);
            SerializedProperty settings = serializedPreset.FindProperty("playerJetpackVfx");

            if (settings == null)
                throw new Exception("Player Jetpack VFX serialized settings are missing.");

            settings.FindPropertyRelative("vfxPrefab").objectReferenceValue = vfxPrefab;
            settings.FindPropertyRelative("activationMode").enumValueIndex = (int)PlayerJetpackVfxActivationMode.WhileMoving;
            settings.FindPropertyRelative("spawnOffset").vector3Value = new Vector3(1f, 2f, 3f);
            settings.FindPropertyRelative("scaleMultiplier").floatValue = 2f;
            settings.FindPropertyRelative("movementSpeedThreshold").floatValue = 0.25f;
            settings.FindPropertyRelative("rotationSpeedThresholdDegrees").floatValue = 4f;
            ValidateScalableFields(settings);

            SerializedProperty scalingRules = serializedPreset.FindProperty("scalingRules");
            scalingRules.arraySize = 2;
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(0),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("activationMode")),
                                 "[this] + 2");
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(1),
                                 PlayerScalingStatKeyUtility.BuildStatKey(settings.FindPropertyRelative("scaleMultiplier")),
                                 "[this] * 2");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            if (!PlayerVisualVfxBakeUtility.TryBuildJetpackVfxConfig(visualPreset,
                                                                     null,
                                                                     out PlayerJetpackVfxConfig config))
                throw new Exception("Jetpack VFX config did not build from an assigned prefab.");

            if (config.SourcePrefab.Value != vfxPrefab ||
                config.ActivationMode != PlayerJetpackVfxActivationMode.WhileMoving ||
                !math.all(config.SpawnOffset == new float3(1f, 2f, 3f)) ||
                !Mathf.Approximately(config.UniformScale, 2f))
                throw new Exception("Jetpack VFX bake did not preserve authored settings.");

            using (PlayerScaledPresetScope scaledScope = PlayerPresetScalingBakeUtility.CreateScope(null,
                                                                                                     null,
                                                                                                     null,
                                                                                                     visualPreset,
                                                                                                     null))
            {
                PlayerJetpackVfxSettings scaledSettings = scaledScope.VisualPreset.PlayerJetpackVfx;

                if (scaledSettings.ActivationMode != PlayerJetpackVfxActivationMode.WhileMovingOrRotating ||
                    !Mathf.Approximately(scaledSettings.ScaleMultiplier, 4f))
                    throw new Exception("Jetpack VFX bake-time Add Scaling did not update enum and numeric fields.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vfxPrefab);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Asserts every behavioral Jetpack field exposed by the tool is compatible with unified Add Scaling formulas.
    /// </summary>
    /// <param name="settings">Serialized Jetpack VFX settings property.</param>
    private static void ValidateScalableFields(SerializedProperty settings)
    {
        SerializedProperty offset = settings.FindPropertyRelative("spawnOffset");

        if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("activationMode")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(offset.FindPropertyRelative("x")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(offset.FindPropertyRelative("y")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(offset.FindPropertyRelative("z")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("scaleMultiplier")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("movementSpeedThreshold")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(settings.FindPropertyRelative("rotationSpeedThresholdDegrees")))
            throw new Exception("Jetpack VFX behavioral fields are not fully supported by Add Scaling.");
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
    /// Validates hash-gated runtime baseline rebuild and enum/numeric formula application.
    /// </summary>
    private static void ValidateRuntimeScalingRebuild()
    {
        World world = new World("PlayerJetpackVfxScalingSmokeTest");
        EntityManager entityManager = world.EntityManager;
        Entity playerEntity = entityManager.CreateEntity();

        try
        {
            PlayerJetpackVfxConfig baseConfig = BuildRuntimeConfig();
            entityManager.AddComponentData(playerEntity, new PlayerBaseJetpackVfxConfig
            {
                Config = baseConfig
            });
            entityManager.AddComponentData(playerEntity, baseConfig);
            entityManager.AddComponentData(playerEntity, new PlayerJetpackVfxScalingState());
            entityManager.AddComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 1u
            });
            entityManager.AddBuffer<PlayerRuntimeJetpackVfxScalingElement>(playerEntity);
            entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);

            DynamicBuffer<PlayerRuntimeJetpackVfxScalingElement> scaling =
                entityManager.GetBuffer<PlayerRuntimeJetpackVfxScalingElement>(playerEntity);
            DynamicBuffer<PlayerScalableStatElement> scalableStats =
                entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
            scaling.Add(BuildNumericScalingElement("activationMode", 0f, true, "[this] + [Level]"));
            scaling.Add(BuildNumericScalingElement("movementSpeedThreshold", 1f, false, "[this] + [Level]"));
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
            AssertRuntimeScaling(entityManager.GetComponentData<PlayerJetpackVfxConfig>(playerEntity),
                                 PlayerJetpackVfxActivationMode.WhileRotating,
                                 3f);

            PlayerScalableStatElement levelStat = scalableStats[0];
            levelStat.Value = 3f;
            scalableStats[0] = levelStat;
            entityManager.SetComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 2u
            });
            scalingSystem.Update(world.Unmanaged);
            AssertRuntimeScaling(entityManager.GetComponentData<PlayerJetpackVfxConfig>(playerEntity),
                                 PlayerJetpackVfxActivationMode.WhileMovingOrRotating,
                                 4f);
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
            SpawnOffset = new float3(1f, 0f, 0f),
            UniformScale = 2f,
            MovementSpeedThreshold = 1f,
            RotationSpeedThresholdDegrees = 1f,
            ActivationMode = PlayerJetpackVfxActivationMode.WhileMoving
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
    /// <param name="expectedActivationMode">Expected runtime activation mode.</param>
    /// <param name="expectedMovementThreshold">Expected runtime movement threshold.</param>
    private static void AssertRuntimeScaling(PlayerJetpackVfxConfig config,
                                             PlayerJetpackVfxActivationMode expectedActivationMode,
                                             float expectedMovementThreshold)
    {
        if (config.ActivationMode != expectedActivationMode ||
            !Mathf.Approximately(config.MovementSpeedThreshold, expectedMovementThreshold))
            throw new Exception("Jetpack VFX runtime scaling did not rebuild from immutable baseline metadata.");
    }
    #endregion

    #region Runtime Presentation
    /// <summary>
    /// Validates movement, rotation and constant activity modes plus managed player-local rotation following.
    /// </summary>
    private static void ValidateActivityGatingAndManagedFollow()
    {
        World world = new World("PlayerJetpackVfxPresentationSmokeTest");
        world.SetTime(new TimeData(1d / 60d, 1f / 60f));
        EntityManager entityManager = world.EntityManager;
        Entity playerEntity = entityManager.CreateEntity(typeof(PlayerJetpackVfxConfig),
                                                          typeof(PlayerJetpackVfxRuntimeState),
                                                          typeof(PlayerMovementState),
                                                          typeof(LocalTransform));
        entityManager.AddBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity);
        DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings =
            entityManager.AddBuffer<PlayerPowerUpVfxPrefabBindingElement>(playerEntity);
        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> requests =
            entityManager.GetBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity);
        GameObject vfxPrefab = new GameObject("JetpackVfxRuntimePrefab");
        GameObject instanceObject = null;

        try
        {
            PlayerJetpackVfxConfig config = BuildRuntimeConfig();
            config.SourcePrefab = vfxPrefab;
            entityManager.SetComponentData(playerEntity, config);
            entityManager.SetComponentData(playerEntity, LocalTransform.FromPositionRotationScale(float3.zero,
                                                                                                   quaternion.identity,
                                                                                                   1f));
            SystemHandle jetpackSystem = world.GetOrCreateSystem<PlayerJetpackVfxSystem>();
            jetpackSystem.Update(world.Unmanaged);

            if (requests.Length != 0)
                throw new Exception("Movement-only Jetpack VFX activated while the player was stationary.");

            entityManager.SetComponentData(playerEntity, new PlayerMovementState
            {
                Velocity = new float3(2f, 0f, 0f)
            });
            jetpackSystem.Update(world.Unmanaged);

            if (requests.Length != 1 ||
                requests[0].ForceLooping == 0 ||
                requests[0].FollowTargetRotation == 0 ||
                requests[0].RefreshKey == 0)
                throw new Exception("Movement-only Jetpack VFX did not enqueue a stable looping rotation-follow request.");

            requests.Clear();
            config.ActivationMode = PlayerJetpackVfxActivationMode.WhileRotating;
            entityManager.SetComponentData(playerEntity, config);
            entityManager.SetComponentData(playerEntity, new PlayerMovementState());
            jetpackSystem.Update(world.Unmanaged);

            if (requests.Length != 0)
                throw new Exception("Rotation-only Jetpack VFX activated before a previous rotation snapshot existed.");

            quaternion rotatedPlayer = quaternion.RotateY(math.radians(90f));
            entityManager.SetComponentData(playerEntity,
                                           LocalTransform.FromPositionRotationScale(float3.zero,
                                                                                    rotatedPlayer,
                                                                                    1f));
            jetpackSystem.Update(world.Unmanaged);

            if (requests.Length != 1)
                throw new Exception("Rotation-only Jetpack VFX did not activate after a meaningful player rotation.");

            PlayerPowerUpVfxSpawnRequest request = requests[0];
            requests.Clear();
            config.ActivationMode = PlayerJetpackVfxActivationMode.Always;
            entityManager.SetComponentData(playerEntity, config);
            jetpackSystem.Update(world.Unmanaged);

            if (requests.Length != 1)
                throw new Exception("Always-visible Jetpack VFX did not activate while the player was stationary.");

            if (!PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                 prefabBindings,
                                                                 in request,
                                                                 new PlayerPowerUpVfxCapConfig
                                                                 {
                                                                     MaxActiveOneShotVfx = 8
                                                                 }))
                throw new Exception("Managed Jetpack VFX request was rejected unexpectedly.");

            PlayerPowerUpManagedVfxRuntimeUtility.UpdateActiveInstances(entityManager, 0.01f);
            instanceObject = GameObject.Find(vfxPrefab.name + "_PowerUpVfx");
            float3 expectedPosition = math.rotate(rotatedPlayer, config.SpawnOffset);

            if (instanceObject == null ||
                Vector3.Distance(instanceObject.transform.position,
                                 new Vector3(expectedPosition.x, expectedPosition.y, expectedPosition.z)) > 0.001f ||
                Quaternion.Angle(instanceObject.transform.rotation,
                                 new Quaternion(rotatedPlayer.value.x,
                                                rotatedPlayer.value.y,
                                                rotatedPlayer.value.z,
                                                rotatedPlayer.value.w)) > 0.1f)
            {
                throw new Exception("Managed Jetpack VFX did not follow the player-local offset and rotation.");
            }

            request.UniformScale = 3f;

            if (!PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                 prefabBindings,
                                                                 in request,
                                                                 new PlayerPowerUpVfxCapConfig
                                                                 {
                                                                     MaxActiveOneShotVfx = 8
                                                                 }) ||
                Vector3.Distance(instanceObject.transform.localScale, Vector3.one * 3f) > 0.001f)
            {
                throw new Exception("Keyed managed Jetpack VFX refresh did not apply runtime-scaled root scale.");
            }
        }
        finally
        {
            PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();

            if (instanceObject != null)
                UnityEngine.Object.DestroyImmediate(instanceObject);

            UnityEngine.Object.DestroyImmediate(vfxPrefab);
            world.Dispose();
        }
    }
    #endregion

    #endregion
}
#endif
