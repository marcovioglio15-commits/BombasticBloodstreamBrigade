#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Unity.Transforms;

/// <summary>
/// Runs deterministic editor checks for projectile-death VFX authoring, scaling rebuilds, and despawn gating.
/// </summary>
public static class PlayerProjectileDeathVfxSmokeTest
{
    #region Constants
    private const string LevelStatName = "Level";
    #endregion

    #region Methods

    #region Public Methods
    // [MenuItem("Tools/Player/Run Projectile Death VFX Smoke Test")]
    /// <summary>
    /// Executes the projectile-death VFX smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    public static void Run()
    {
        ValidateAuthoringAndBakeScaling();
        ValidateRuntimeScalingRebuild();
        ValidateNaturalExpiryGating();
        ValidateManagedCapRestart();
        Debug.Log("[PlayerProjectileDeathVfxSmokeTest] All projectile-death VFX checks passed.");
    }
    #endregion

    #region Authoring And Bake Scaling
    /// <summary>
    /// Validates serialized Add Scaling targets, wall-prefab fallback, and bake-time formula application.
    /// </summary>
    private static void ValidateAuthoringAndBakeScaling()
    {
        PlayerVisualPreset visualPreset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        GameObject rangePrefab = new GameObject("RangeLifetimeDeathVfx");

        try
        {
            SerializedObject serializedPreset = new SerializedObject(visualPreset);
            SerializedProperty rangeEvent = serializedPreset.FindProperty("projectileDeathVfx.rangeOrLifetime");
            SerializedProperty wallEvent = serializedPreset.FindProperty("projectileDeathVfx.terminalWallHit");

            if (rangeEvent == null || wallEvent == null)
                throw new Exception("Projectile-death VFX serialized event settings are missing.");

            ConfigureEvent(rangeEvent, true, rangePrefab, new Vector3(1f, 2f, 3f), 2f, 0.6f);
            ConfigureEvent(wallEvent, true, null, new Vector3(4f, 5f, 6f), 4f, 0.8f);
            ValidateScalableEventFields(rangeEvent);
            ValidateScalableEventFields(wallEvent);

            SerializedProperty scalingRules = serializedPreset.FindProperty("scalingRules");
            scalingRules.arraySize = 1;
            ConfigureScalingRule(scalingRules.GetArrayElementAtIndex(0),
                                 PlayerScalingStatKeyUtility.BuildStatKey(wallEvent.FindPropertyRelative("scaleMultiplier")),
                                 "[this] * 2");
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();

            if (!PlayerVisualVfxBakeUtility.TryBuildProjectileDeathVfxConfig(visualPreset,
                                                                              null,
                                                                              out PlayerProjectileDeathVfxConfig config))
                throw new Exception("Projectile-death VFX config did not build from an assigned range/lifetime prefab.");

            if (config.RangeOrLifetime.SourcePrefab.Value != rangePrefab ||
                config.TerminalWallHit.SourcePrefab.Value != rangePrefab)
                throw new Exception("Terminal wall-hit VFX did not reuse the range/lifetime prefab when its override was empty.");

            using (PlayerScaledPresetScope scaledScope = PlayerPresetScalingBakeUtility.CreateScope(null,
                                                                                                     null,
                                                                                                     null,
                                                                                                     visualPreset,
                                                                                                     null))
            {
                float scaledWallScale = scaledScope.VisualPreset.ProjectileDeathVfx.TerminalWallHit.ScaleMultiplier;

                if (!Mathf.Approximately(scaledWallScale, 8f))
                    throw new Exception("Projectile-death VFX bake-time Add Scaling did not update terminal wall-hit scale.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rangePrefab);
            UnityEngine.Object.DestroyImmediate(visualPreset);
        }
    }

    /// <summary>
    /// Writes one temporary projectile-death event used by smoke checks.
    /// </summary>
    /// <param name="eventProperty">Serialized projectile-death event property.</param>
    /// <param name="enabled">Authored event-enabled value.</param>
    /// <param name="prefab">Optional event prefab assignment.</param>
    /// <param name="offset">Authored local spawn offset.</param>
    /// <param name="scale">Authored uniform scale multiplier.</param>
    /// <param name="lifetime">Authored one-shot lifetime.</param>
    private static void ConfigureEvent(SerializedProperty eventProperty,
                                       bool enabled,
                                       GameObject prefab,
                                       Vector3 offset,
                                       float scale,
                                       float lifetime)
    {
        eventProperty.FindPropertyRelative("enabled").boolValue = enabled;
        eventProperty.FindPropertyRelative("vfxPrefab").objectReferenceValue = prefab;
        eventProperty.FindPropertyRelative("spawnOffset").vector3Value = offset;
        eventProperty.FindPropertyRelative("scaleMultiplier").floatValue = scale;
        eventProperty.FindPropertyRelative("lifetimeSeconds").floatValue = lifetime;
    }

    /// <summary>
    /// Asserts every behavioral event field exposed by the tool is compatible with unified Add Scaling formulas.
    /// </summary>
    /// <param name="eventProperty">Serialized projectile-death event property.</param>
    private static void ValidateScalableEventFields(SerializedProperty eventProperty)
    {
        SerializedProperty enabled = eventProperty.FindPropertyRelative("enabled");
        SerializedProperty offset = eventProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty scale = eventProperty.FindPropertyRelative("scaleMultiplier");
        SerializedProperty lifetime = eventProperty.FindPropertyRelative("lifetimeSeconds");

        if (!PlayerScalingFormulaEditorUtility.SupportsScalingTarget(enabled) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(offset.FindPropertyRelative("x")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(offset.FindPropertyRelative("y")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(offset.FindPropertyRelative("z")) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(scale) ||
            !PlayerScalingFormulaEditorUtility.SupportsScalingTarget(lifetime))
            throw new Exception("Projectile-death VFX behavioral fields are not fully supported by Add Scaling.");
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
    /// Validates hash-gated runtime baseline rebuild and boolean/numeric formula application.
    /// </summary>
    private static void ValidateRuntimeScalingRebuild()
    {
        World world = new World("PlayerProjectileDeathVfxScalingSmokeTest");
        EntityManager entityManager = world.EntityManager;
        Entity playerEntity = entityManager.CreateEntity();

        try
        {
            PlayerProjectileDeathVfxConfig baseConfig = BuildRuntimeConfig();
            entityManager.AddComponentData(playerEntity, new PlayerBaseProjectileDeathVfxConfig
            {
                Config = baseConfig
            });
            entityManager.AddComponentData(playerEntity, baseConfig);
            entityManager.AddComponentData(playerEntity, new PlayerProjectileDeathVfxScalingState());
            entityManager.AddComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 1u
            });
            entityManager.AddBuffer<PlayerRuntimeProjectileDeathVfxScalingElement>(playerEntity);
            entityManager.AddBuffer<PlayerScalableStatElement>(playerEntity);

            // Acquire writable buffers after all structural changes have completed.
            DynamicBuffer<PlayerRuntimeProjectileDeathVfxScalingElement> scaling =
                entityManager.GetBuffer<PlayerRuntimeProjectileDeathVfxScalingElement>(playerEntity);
            DynamicBuffer<PlayerScalableStatElement> scalableStats =
                entityManager.GetBuffer<PlayerScalableStatElement>(playerEntity);
            scaling.Add(BuildBooleanScalingElement("rangeOrLifetime.enabled", "[Level] > 0"));
            scaling.Add(BuildNumericScalingElement("terminalWallHit.scaleMultiplier", 3f, "[this] + [Level]"));
            scalableStats.Add(new PlayerScalableStatElement
            {
                Name = new FixedString64Bytes(LevelStatName),
                Type = (byte)PlayerScalableStatType.Integer,
                MinimumValue = 0f,
                MaximumValue = 100f,
                Value = 2f
            });

            SystemHandle scalingSystem = world.GetOrCreateSystem<PlayerRuntimeProjectileDeathVfxScalingSystem>();
            scalingSystem.Update(world.Unmanaged);
            AssertRuntimeScaling(entityManager.GetComponentData<PlayerProjectileDeathVfxConfig>(playerEntity), true, 5f);

            PlayerScalableStatElement levelStat = scalableStats[0];
            levelStat.Value = 4f;
            scalableStats[0] = levelStat;
            entityManager.SetComponentData(playerEntity, new PlayerRuntimeScalingState
            {
                Initialized = 1,
                LastScalableStatsHash = 2u
            });
            scalingSystem.Update(world.Unmanaged);
            AssertRuntimeScaling(entityManager.GetComponentData<PlayerProjectileDeathVfxConfig>(playerEntity), true, 7f);
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Builds deterministic runtime projectile-death VFX settings for scaling and despawn tests.
    /// </summary>
    /// <returns>Runtime config with both occasions enabled.</returns>
    private static PlayerProjectileDeathVfxConfig BuildRuntimeConfig()
    {
        return new PlayerProjectileDeathVfxConfig
        {
            RangeOrLifetime = new PlayerProjectileDeathVfxEventConfig
            {
                Enabled = 0,
                SpawnOffset = new float3(1f, 0f, 0f),
                UniformScale = 3f,
                LifetimeSeconds = 0.5f
            },
            TerminalWallHit = new PlayerProjectileDeathVfxEventConfig
            {
                Enabled = 1,
                UniformScale = 3f,
                LifetimeSeconds = 0.5f
            }
        };
    }

    /// <summary>
    /// Builds one boolean runtime scaling metadata entry.
    /// </summary>
    /// <param name="path">Runtime payload path.</param>
    /// <param name="formula">Unified boolean formula.</param>
    /// <returns>Configured boolean scaling metadata.</returns>
    private static PlayerRuntimeProjectileDeathVfxScalingElement BuildBooleanScalingElement(string path, string formula)
    {
        return new PlayerRuntimeProjectileDeathVfxScalingElement
        {
            PayloadPath = new FixedString128Bytes(path),
            ValueType = (byte)PlayerFormulaValueType.Boolean,
            BaseBooleanValue = 0,
            IsInteger = 1,
            Formula = new FixedString512Bytes(formula)
        };
    }

    /// <summary>
    /// Builds one numeric runtime scaling metadata entry.
    /// </summary>
    /// <param name="path">Runtime payload path.</param>
    /// <param name="baseValue">Immutable baseline value.</param>
    /// <param name="formula">Unified numeric formula.</param>
    /// <returns>Configured numeric scaling metadata.</returns>
    private static PlayerRuntimeProjectileDeathVfxScalingElement BuildNumericScalingElement(string path,
                                                                                             float baseValue,
                                                                                             string formula)
    {
        return new PlayerRuntimeProjectileDeathVfxScalingElement
        {
            PayloadPath = new FixedString128Bytes(path),
            ValueType = (byte)PlayerFormulaValueType.Number,
            BaseValue = baseValue,
            Formula = new FixedString512Bytes(formula)
        };
    }

    /// <summary>
    /// Asserts one runtime scaling result after a hash-triggered rebuild.
    /// </summary>
    /// <param name="config">Runtime config to inspect.</param>
    /// <param name="expectedRangeEnabled">Expected range/lifetime enabled state.</param>
    /// <param name="expectedWallScale">Expected terminal wall-hit scale.</param>
    private static void AssertRuntimeScaling(PlayerProjectileDeathVfxConfig config,
                                             bool expectedRangeEnabled,
                                             float expectedWallScale)
    {
        if ((config.RangeOrLifetime.Enabled != 0) != expectedRangeEnabled ||
            !Mathf.Approximately(config.TerminalWallHit.UniformScale, expectedWallScale))
            throw new Exception("Projectile-death VFX runtime scaling did not rebuild from immutable baseline metadata.");
    }
    #endregion

    #region Natural Expiry
    /// <summary>
    /// Validates natural-expiry VFX enqueue, pooled return, and suppression after a previous valid target hit.
    /// </summary>
    private static void ValidateNaturalExpiryGating()
    {
        World world = new World("PlayerProjectileDeathVfxExpirySmokeTest");
        EntityManager entityManager = world.EntityManager;
        Entity vfxPrefabEntity = entityManager.CreateEntity();
        Entity playerEntity = entityManager.CreateEntity();
        Entity projectileEntity = entityManager.CreateEntity(typeof(Projectile),
                                                              typeof(ProjectileRuntimeState),
                                                              typeof(ProjectileContactState),
                                                              typeof(ProjectileOwner),
                                                              typeof(LocalTransform),
                                                              typeof(ProjectileActive));

        try
        {
            entityManager.AddBuffer<ProjectilePoolElement>(playerEntity);
            entityManager.AddBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity);
            PlayerProjectileDeathVfxConfig config = BuildRuntimeConfig();
            config.RangeOrLifetime.Enabled = 1;
            config.RangeOrLifetime.PrefabEntity = vfxPrefabEntity;
            entityManager.AddComponentData(playerEntity, config);
            entityManager.SetComponentData(projectileEntity, new Projectile
            {
                MaxRange = 1f
            });
            entityManager.SetComponentData(projectileEntity, new ProjectileRuntimeState
            {
                TraveledDistance = 1f
            });
            entityManager.SetComponentData(projectileEntity, new ProjectileOwner
            {
                ShooterEntity = playerEntity
            });
            entityManager.SetComponentData(projectileEntity, LocalTransform.FromPositionRotationScale(float3.zero,
                                                                                                       quaternion.identity,
                                                                                                       2f));

            SystemHandle despawnSystem = world.GetOrCreateSystem<ProjectileDespawnSystem>();
            despawnSystem.Update(world.Unmanaged);

            DynamicBuffer<ProjectilePoolElement> pool = entityManager.GetBuffer<ProjectilePoolElement>(playerEntity);
            DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests =
                entityManager.GetBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity);
            if (vfxRequests.Length != 1 ||
                pool.Length != 1 ||
                entityManager.IsComponentEnabled<ProjectileActive>(projectileEntity) ||
                vfxRequests[0].RestartOldestOnCap == 0 ||
                !Mathf.Approximately(vfxRequests[0].UniformScale, 6f) ||
                !math.all(vfxRequests[0].Position == new float3(2f, 0f, 0f)))
                throw new Exception("Natural projectile expiry did not enqueue the expected death VFX and return the projectile to its pool.");

            vfxRequests.Clear();
            pool.Clear();
            entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, true);
            entityManager.SetComponentData(projectileEntity, new ProjectileContactState
            {
                HasHitTarget = 1
            });
            entityManager.SetComponentData(projectileEntity, new ProjectileRuntimeState
            {
                TraveledDistance = 1f
            });
            despawnSystem.Update(world.Unmanaged);

            pool = entityManager.GetBuffer<ProjectilePoolElement>(playerEntity);
            vfxRequests = entityManager.GetBuffer<PlayerPowerUpVfxSpawnRequest>(playerEntity);
            if (vfxRequests.Length != 0 || pool.Length != 1)
                throw new Exception("Natural projectile expiry was not suppressed after a previous valid target hit.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Managed Cap Restart
    /// <summary>
    /// Validates that long-lived bullet-death VFX restart from the beginning when area or global caps are full.
    /// </summary>
    private static void ValidateManagedCapRestart()
    {
        World world = new World("PlayerProjectileDeathVfxManagedCapSmokeTest");
        EntityManager entityManager = world.EntityManager;
        Entity ownerEntity = entityManager.CreateEntity();
        DynamicBuffer<PlayerPowerUpVfxPrefabBindingElement> prefabBindings =
            entityManager.AddBuffer<PlayerPowerUpVfxPrefabBindingElement>(ownerEntity);
        GameObject sourcePrefab = new GameObject("ProjectileDeathManagedCapVfx");
        ParticleSystem sourceParticleSystem = sourcePrefab.AddComponent<ParticleSystem>();
        GameObject alternateSourcePrefab = new GameObject("ProjectileDeathManagedCapAlternateVfx");
        GameObject instanceObject = null;
        GameObject alternateInstanceObject = null;

        try
        {
            sourceParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule mainModule = sourceParticleSystem.main;
            mainModule.playOnAwake = false;
            mainModule.duration = 5f;

            PlayerPowerUpVfxSpawnRequest request = BuildManagedCapRestartRequest(sourcePrefab, float3.zero);
            PlayerPowerUpVfxCapConfig areaCapConfig = new PlayerPowerUpVfxCapConfig
            {
                MaxSamePrefabPerCell = 1,
                CellSize = 100f,
                MaxActiveOneShotVfx = 8
            };

            if (!PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                 prefabBindings,
                                                                 in request,
                                                                 in areaCapConfig))
            {
                throw new Exception("Managed bullet-death VFX initial spawn was rejected unexpectedly.");
            }

            instanceObject = GameObject.Find(sourcePrefab.name + "_PowerUpVfx");

            if (instanceObject == null)
                throw new Exception("Managed bullet-death VFX instance was not created.");

            ParticleSystem instanceParticleSystem = instanceObject.GetComponent<ParticleSystem>();
            instanceParticleSystem.Simulate(2f, true, true, true);
            request.Position = new float3(1f, 0f, 0f);

            if (!PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                 prefabBindings,
                                                                 in request,
                                                                 in areaCapConfig))
            {
                throw new Exception("Managed bullet-death VFX did not restart when the area cap was full.");
            }

            AssertManagedCapRestart(instanceObject, instanceParticleSystem, request.Position, "area");
            instanceParticleSystem.Simulate(2f, true, true, true);
            request.Position = new float3(200f, 0f, 0f);
            PlayerPowerUpVfxCapConfig globalCapConfig = new PlayerPowerUpVfxCapConfig
            {
                MaxActiveOneShotVfx = 1
            };

            if (!PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                 prefabBindings,
                                                                 in request,
                                                                 in globalCapConfig))
            {
                throw new Exception("Managed bullet-death VFX did not restart when the global cap was full.");
            }

            AssertManagedCapRestart(instanceObject, instanceParticleSystem, request.Position, "global");
            PlayerPowerUpVfxSpawnRequest alternateRequest = BuildManagedCapRestartRequest(alternateSourcePrefab,
                                                                                           new float3(400f, 0f, 0f));

            if (!PlayerPowerUpManagedVfxRuntimeUtility.TrySpawn(entityManager,
                                                                 prefabBindings,
                                                                 in alternateRequest,
                                                                 in globalCapConfig))
            {
                throw new Exception("Managed bullet-death VFX did not replace a different prefab when the global cap was full.");
            }

            alternateInstanceObject = GameObject.Find(alternateSourcePrefab.name + "_PowerUpVfx");

            if (alternateInstanceObject == null ||
                PlayerPowerUpManagedVfxRuntimeUtility.ActiveInstanceCount != 1 ||
                Vector3.Distance(alternateInstanceObject.transform.position, new Vector3(400f, 0f, 0f)) > 0.001f)
            {
                throw new Exception("Managed bullet-death VFX cross-prefab global-cap replacement failed.");
            }
        }
        finally
        {
            PlayerPowerUpManagedVfxRuntimeUtility.DestroyAll();

            if (instanceObject != null)
                UnityEngine.Object.DestroyImmediate(instanceObject);

            if (alternateInstanceObject != null)
                UnityEngine.Object.DestroyImmediate(alternateInstanceObject);

            UnityEngine.Object.DestroyImmediate(sourcePrefab);
            UnityEngine.Object.DestroyImmediate(alternateSourcePrefab);
            world.Dispose();
        }
    }

    /// <summary>
    /// Builds one long-lived managed VFX request that opts into bounded restart-on-cap behavior.
    /// </summary>
    /// <param name="sourcePrefab">Direct managed source prefab.</param>
    /// <param name="position">Initial world spawn position.</param>
    /// <returns>Configured long-lived one-shot request.</returns>
    private static PlayerPowerUpVfxSpawnRequest BuildManagedCapRestartRequest(GameObject sourcePrefab, float3 position)
    {
        return new PlayerPowerUpVfxSpawnRequest
        {
            SourcePrefab = sourcePrefab,
            Position = position,
            Rotation = quaternion.identity,
            UniformScale = 1f,
            ParticleSimulationSpeedMultiplier = 1f,
            LifetimeSeconds = 20f,
            RestartOldestOnCap = 1
        };
    }

    /// <summary>
    /// Asserts that one capped managed VFX reuse moved and restarted the existing instance.
    /// </summary>
    /// <param name="instanceObject">Managed VFX instance reused under the cap.</param>
    /// <param name="particleSystem">Particle system expected to restart from time zero.</param>
    /// <param name="expectedPosition">Expected replacement world position.</param>
    /// <param name="capLabel">Cap path label included in failure messages.</param>
    private static void AssertManagedCapRestart(GameObject instanceObject,
                                                ParticleSystem particleSystem,
                                                float3 expectedPosition,
                                                string capLabel)
    {
        Vector3 managedExpectedPosition = new Vector3(expectedPosition.x, expectedPosition.y, expectedPosition.z);

        if (PlayerPowerUpManagedVfxRuntimeUtility.ActiveInstanceCount != 1 ||
            Vector3.Distance(instanceObject.transform.position, managedExpectedPosition) > 0.001f ||
            particleSystem.time > 0.1f)
        {
            throw new Exception(string.Format("Managed bullet-death VFX did not restart cleanly under the {0} cap.", capLabel));
        }
    }
    #endregion

    #endregion
}
#endif
