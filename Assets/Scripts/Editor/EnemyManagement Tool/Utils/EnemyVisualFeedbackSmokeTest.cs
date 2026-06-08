using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Verifies the standard death puddle assets and elastic hit shader contract used by enemy visual presets.
/// </summary>
public static class EnemyVisualFeedbackSmokeTest
{
    #region Constants
    private const string ElasticDirectionProperty = "_ElasticHitDirection";
    private const string ElasticTimingProperty = "_ElasticHitTiming";
    private const string ElasticMotionProperty = "_ElasticHitMotion";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the enemy visual feedback smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    [MenuItem("Tools/Enemy Management/Run Enemy Visual Feedback Smoke Test")]
    public static void Run()
    {
        ValidateStandardPuddleAssets();
        ValidateElasticShaders();
        ValidatePresetDefaultsAndBakeConfig();
        ValidateDeathPuddleSpawnRuntime();
        ValidateElasticTriggerPolicy();
        Debug.Log("[EnemyVisualFeedbackSmokeTest] All enemy visual feedback checks passed.");
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Validates the shared material, prefab, mesh and authoring marker required by automatic puddle fallback.
    /// </summary>
    private static void ValidateStandardPuddleAssets()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(EnemyDeathPuddleStandardAssetUtility.MaterialAssetPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDeathPuddleStandardAssetUtility.PrefabAssetPath);

        if (material == null || material.shader == null ||
            !material.HasProperty("_PuddleFluid") ||
            !string.Equals(material.shader.name, "BombasticBloodstreamBrigade/Enemy Death Puddle ECS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The standard death puddle material is missing or uses the wrong shader.");
        }

        if (prefab == null || Resources.Load<GameObject>("PF_EnemyDeathPuddle") != prefab)
            throw new InvalidOperationException("The standard death puddle prefab is missing from Resources.");

        if (prefab.GetComponent<EnemyDeathPuddlePrefabAuthoring>() == null)
            throw new InvalidOperationException("The standard death puddle prefab lacks EnemyDeathPuddlePrefabAuthoring.");

        MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = prefab.GetComponent<MeshRenderer>();

        if (meshFilter == null || meshFilter.sharedMesh == null ||
            meshRenderer == null || meshRenderer.sharedMaterial != material)
        {
            throw new InvalidOperationException("The standard death puddle prefab render contract is incomplete.");
        }
    }

    /// <summary>
    /// Validates all supported enemy shaders expose the three elastic hit material properties.
    /// </summary>
    private static void ValidateElasticShaders()
    {
        ValidateElasticShader("Cel Shader/Toon Diffuse ECS Hit Flash");
        ValidateElasticShader("Cel Shader/Toon Outline ECS");
        ValidateElasticShader("Cel Shader/Toon Diffuse Hit Flash");
    }

    /// <summary>
    /// Validates one shader exposes the complete elastic hit material-property contract.
    /// </summary>
    /// <param name="shaderName">Shader name resolved through Unity's shader registry.</param>
    private static void ValidateElasticShader(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
            throw new InvalidOperationException("Missing elastic-compatible shader: " + shaderName);

        Material material = new Material(shader);

        try
        {
            if (!material.HasProperty(ElasticDirectionProperty) ||
                !material.HasProperty(ElasticTimingProperty) ||
                !material.HasProperty(ElasticMotionProperty))
            {
                throw new InvalidOperationException("Incomplete elastic property contract on shader: " + shaderName);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    /// <summary>
    /// Validates new visual presets initialize both settings blocks and produce finite runtime-safe configs.
    /// </summary>
    private static void ValidatePresetDefaultsAndBakeConfig()
    {
        EnemyVisualPreset preset = ScriptableObject.CreateInstance<EnemyVisualPreset>();

        try
        {
            preset.ValidateValues();

            if (preset.DeathPuddle == null || preset.ElasticHit == null)
                throw new InvalidOperationException("Enemy visual preset feedback settings are not initialized.");

            EnemyDeathDebrisColorPalette palette = new EnemyDeathDebrisColorPalette
            {
                PrimaryColor = new float4(1f, 0f, 0f, 1f),
                SecondaryColor = new float4(0.5f, 0f, 0f, 1f)
            };
            EnemyDeathPuddleConfig puddleConfig = EnemyVisualFeedbackBakeUtility.BuildDeathPuddleConfig(preset.DeathPuddle,
                                                                                                         Entity.Null,
                                                                                                         in palette);
            EnemyElasticHitConfig elasticConfig = EnemyVisualFeedbackBakeUtility.BuildElasticHitConfig(preset.ElasticHit);

            if (!math.isfinite(puddleConfig.LifetimeSeconds) ||
                !math.isfinite(puddleConfig.FlowSpeed) ||
                !math.isfinite(puddleConfig.Viscosity) ||
                !math.isfinite(puddleConfig.SurfaceDistortion) ||
                !math.isfinite(puddleConfig.HighlightStrength) ||
                !math.isfinite(elasticConfig.DurationSeconds) ||
                elasticConfig.Enabled == 0)
            {
                throw new InvalidOperationException("Default enemy visual feedback configs are invalid.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    /// <summary>
    /// Validates one detached puddle request instantiates and activates through the dedicated ECS pool system.
    /// </summary>
    private static void ValidateDeathPuddleSpawnRuntime()
    {
        World world = new World("EnemyDeathPuddleSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity prefabEntity = entityManager.CreateEntity(typeof(Prefab),
                                                              typeof(LocalTransform),
                                                              typeof(EnemyDeathPuddleRuntimeState),
                                                              typeof(EnemyDeathPuddleActive),
                                                              typeof(MaterialDeathPuddlePrimaryColor),
                                                              typeof(MaterialDeathPuddleSecondaryColor),
                                                              typeof(MaterialDeathPuddleTiming),
                                                              typeof(MaterialDeathPuddleShape),
                                                              typeof(MaterialDeathPuddleStyle));
            entityManager.SetComponentEnabled<EnemyDeathPuddleActive>(prefabEntity, false);
            Entity requestEntity = entityManager.CreateEntity();
            DynamicBuffer<EnemyDeathPuddleSpawnRequest> requests = entityManager.AddBuffer<EnemyDeathPuddleSpawnRequest>(requestEntity);
            requests.Add(new EnemyDeathPuddleSpawnRequest
            {
                PrefabEntity = prefabEntity,
                Position = new float3(2f, 0.02f, 3f),
                WorldSize = new float2(1.5f, 1f),
                LifetimeSeconds = 4f,
                StableFraction = 0.2f,
                FinalScaleRatio = 0.08f,
                EdgeIrregularity = 0.25f,
                BorderWidth = 0.1f,
                EdgeFeather = 0.04f,
                SecondaryPaletteBlend = 0.5f,
                FlowSpeed = 0.35f,
                Viscosity = 0.7f,
                SurfaceDistortion = 0.08f,
                HighlightStrength = 0.18f,
                PrimaryColor = new float4(1f, 0f, 0f, 1f),
                SecondaryColor = new float4(0.5f, 0f, 0f, 1f),
                Seed = 17u,
                EvaporationCurve = EnemyDeathPuddleEvaporationCurve.SmoothStep,
                RandomRotation = 1
            });

            SystemHandle spawnSystem = world.GetOrCreateSystem<EnemyDeathPuddleSpawnSystem>();
            spawnSystem.Update(world.Unmanaged);
            EntityQuery activePuddles = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EnemyDeathPuddleRuntimeState>(),
                    ComponentType.ReadOnly<EnemyDeathPuddleActive>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Prefab>()
                }
            });
            DynamicBuffer<EnemyDeathPuddleSpawnRequest> remainingRequests = entityManager.GetBuffer<EnemyDeathPuddleSpawnRequest>(requestEntity);

            if (remainingRequests.Length != 0 || activePuddles.CalculateEntityCount() != 1)
                throw new InvalidOperationException("The death puddle ECS pool did not consume and activate one request.");

            NativeArray<Entity> activePuddleEntities = activePuddles.ToEntityArray(Allocator.Temp);
            Entity activePuddle = activePuddleEntities[0];
            activePuddleEntities.Dispose();

            if (!entityManager.HasComponent<PostTransformMatrix>(activePuddle))
                throw new InvalidOperationException("The death puddle ECS pool did not recover a missing PostTransformMatrix.");

            if (!entityManager.HasComponent<MaterialDeathPuddleFluid>(activePuddle))
                throw new InvalidOperationException("The death puddle ECS pool did not recover a missing fluid material property.");
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Validates direct-only, all-damage and lethal-hit rejection behavior against an isolated ECS entity.
    /// </summary>
    private static void ValidateElasticTriggerPolicy()
    {
        World world = new World("EnemyVisualFeedbackSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity enemyEntity = entityManager.CreateEntity(typeof(EnemyElasticHitConfig),
                                                             typeof(EnemyElasticHitState),
                                                             typeof(EnemyElasticHitActive));
            EnemyHealth survivingHealth = new EnemyHealth
            {
                Current = 10f
            };
            EnemyHealth lethalHealth = new EnemyHealth
            {
                Current = 0f
            };
            EnemyElasticHitConfig config = EnemyVisualFeedbackBakeUtility.BuildElasticHitConfig(new EnemyVisualElasticHitSettings());
            entityManager.SetComponentData(enemyEntity, config);
            entityManager.SetComponentData(enemyEntity, new EnemyElasticHitState
            {
                LastTriggerTime = -1000f,
                DirectionWorld = new float3(0f, 0f, 1f)
            });
            entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, false);

            EnemyElasticHitRuntimeUtility.Trigger(entityManager,
                                                  enemyEntity,
                                                  in survivingHealth,
                                                  float3.zero,
                                                  false);

            if (entityManager.IsComponentEnabled<EnemyElasticHitActive>(enemyEntity))
                throw new InvalidOperationException("Direct-only elastic policy accepted non-spatial damage.");

            EnemyElasticHitRuntimeUtility.Trigger(entityManager,
                                                  enemyEntity,
                                                  in survivingHealth,
                                                  new float3(1f, 0f, 0f),
                                                  true);

            if (!entityManager.IsComponentEnabled<EnemyElasticHitActive>(enemyEntity))
                throw new InvalidOperationException("Direct-only elastic policy rejected a valid non-lethal impact.");

            entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, false);
            config.TriggerMode = EnemyElasticHitTriggerMode.AllNonLethalDamage;
            entityManager.SetComponentData(enemyEntity, config);
            entityManager.SetComponentData(enemyEntity, new EnemyElasticHitState
            {
                LastTriggerTime = -1000f,
                DirectionWorld = new float3(0f, 0f, 1f)
            });
            EnemyElasticHitRuntimeUtility.Trigger(entityManager,
                                                  enemyEntity,
                                                  in survivingHealth,
                                                  float3.zero,
                                                  false);

            if (!entityManager.IsComponentEnabled<EnemyElasticHitActive>(enemyEntity))
                throw new InvalidOperationException("All-damage elastic policy rejected valid non-lethal damage.");

            entityManager.SetComponentEnabled<EnemyElasticHitActive>(enemyEntity, false);
            entityManager.SetComponentData(enemyEntity, new EnemyElasticHitState
            {
                LastTriggerTime = -1000f,
                DirectionWorld = new float3(0f, 0f, 1f)
            });
            EnemyElasticHitRuntimeUtility.Trigger(entityManager,
                                                  enemyEntity,
                                                  in lethalHealth,
                                                  new float3(1f, 0f, 0f),
                                                  true);

            if (entityManager.IsComponentEnabled<EnemyElasticHitActive>(enemyEntity))
                throw new InvalidOperationException("Elastic policy accepted a lethal hit.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #endregion
}
