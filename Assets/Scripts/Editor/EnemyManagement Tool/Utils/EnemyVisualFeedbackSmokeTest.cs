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
    private const string FaceFlipbookMaterialPath = "Assets/3D/Materials/M_EnemiesFaces.mat";
    private const string FaceFlipbookTexturePath = "Assets/3D/Textures/textures-enemiesFaces/T_EnemiesFaces_Flipbook.png";
    private const string FaceFlipbookShaderName = "BombasticBloodstreamBrigade/Enemy Faces Flipbook ECS";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Executes the enemy visual feedback smoke suite from Unity batch mode via -executeMethod.
    /// </summary>
    //[MenuItem("Tools/Enemy Management/Run Enemy Visual Feedback Smoke Test")]
    public static void Run()
    {
        ValidateStandardPuddleAssets();
        ValidateFaceFlipbookAssets();
        ValidateElasticShaders();
        ValidatePresetDefaultsAndBakeConfig();
        ValidateDeathPuddleSpawnRuntime();
        ValidateElasticTriggerPolicy();
        //Debug.Log("[EnemyVisualFeedbackSmokeTest] All enemy visual feedback checks passed.");
    }

    /// <summary>
    /// Executes only the shared enemy-face flipbook asset and shader contract checks from Unity batch mode.
    /// </summary>
    //[MenuItem("Tools/Enemy Management/Run Enemy Face Flipbook Smoke Test")]
    public static void RunFaceFlipbook()
    {
        ValidateFaceFlipbookAssets();
        ValidateElasticShader(FaceFlipbookShaderName);
        //Debug.Log("[EnemyVisualFeedbackSmokeTest] Enemy face flipbook checks passed.");
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
        ValidateElasticShader(FaceFlipbookShaderName);
    }

    /// <summary>
    /// Validates the shared enemy-face material, source flipbook and shader playback contract used by enemy meshes.
    /// </summary>
    private static void ValidateFaceFlipbookAssets()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FaceFlipbookMaterialPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FaceFlipbookTexturePath);

        if (material == null ||
            material.shader == null ||
            !string.Equals(material.shader.name, FaceFlipbookShaderName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The shared enemy-face material is missing or uses the wrong flipbook shader.");
        }

        if (texture == null ||
            material.GetTexture("_MainTex") != texture ||
            texture.width != 8192 ||
            texture.height != 4096)
        {
            throw new InvalidOperationException("The shared enemy-face flipbook texture is missing, downscaled or not assigned to the material.");
        }

        if (!material.HasProperty("_FaceFlipbookGrid") ||
            !material.HasProperty("_FaceFlipbookPlayback") ||
            !material.HasProperty("_FaceFlipbookEnabled") ||
            !material.HasProperty("_AlphaClipThreshold"))
        {
            throw new InvalidOperationException("The shared enemy-face material does not expose the complete flipbook property contract.");
        }

        Vector4 grid = material.GetVector("_FaceFlipbookGrid");
        Vector4 playback = material.GetVector("_FaceFlipbookPlayback");

        if (!Mathf.Approximately(grid.x, 4f) ||
            !Mathf.Approximately(grid.y, 2f) ||
            !Mathf.Approximately(grid.z, 8f) ||
            playback.x <= 0f)
        {
            throw new InvalidOperationException("The shared enemy-face flipbook must use four columns, two rows, eight frames and positive FPS.");
        }
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
    /// Validates one detached puddle request instantiates and activates through the dedicated ECS pool system,
    /// and that each per-instance material override is recovered when missing from the source prefab so the
    /// shader OVERRIDE_SUPPORTED fallback can never silently apply material defaults to authored values.
    /// </summary>
    private static void ValidateDeathPuddleSpawnRuntime()
    {
        World world = new World("EnemyDeathPuddleSmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            // Deliberately omit every per-instance material override so the spawn system has to recover them all.
            Entity prefabEntity = entityManager.CreateEntity(typeof(Prefab),
                                                              typeof(LocalTransform),
                                                              typeof(EnemyDeathPuddleRuntimeState),
                                                              typeof(EnemyDeathPuddleActive));
            entityManager.SetComponentEnabled<EnemyDeathPuddleActive>(prefabEntity, false);
            float4 expectedPrimaryColor = new float4(1f, 0f, 0f, 1f);
            float4 expectedSecondaryColor = new float4(0.5f, 0f, 0f, 1f);
            const float expectedEdgeIrregularity = 0.81f;
            const float expectedBorderWidth = 0.27f;
            const float expectedEdgeFeather = 0.13f;
            const float expectedSecondaryPaletteBlend = 0.42f;
            const float expectedFlowSpeed = 1.7f;
            const float expectedViscosity = 0.33f;
            const float expectedSurfaceDistortion = 0.21f;
            const float expectedHighlightStrength = 0.62f;
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
                EdgeIrregularity = expectedEdgeIrregularity,
                BorderWidth = expectedBorderWidth,
                EdgeFeather = expectedEdgeFeather,
                SecondaryPaletteBlend = expectedSecondaryPaletteBlend,
                FlowSpeed = expectedFlowSpeed,
                Viscosity = expectedViscosity,
                SurfaceDistortion = expectedSurfaceDistortion,
                HighlightStrength = expectedHighlightStrength,
                PrimaryColor = expectedPrimaryColor,
                SecondaryColor = expectedSecondaryColor,
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
            RequireRecoveredComponent<PostTransformMatrix>(entityManager, activePuddle, nameof(PostTransformMatrix));
            RequireRecoveredComponent<MaterialDeathPuddlePrimaryColor>(entityManager, activePuddle, nameof(MaterialDeathPuddlePrimaryColor));
            RequireRecoveredComponent<MaterialDeathPuddleSecondaryColor>(entityManager, activePuddle, nameof(MaterialDeathPuddleSecondaryColor));
            RequireRecoveredComponent<MaterialDeathPuddleTiming>(entityManager, activePuddle, nameof(MaterialDeathPuddleTiming));
            RequireRecoveredComponent<MaterialDeathPuddleShape>(entityManager, activePuddle, nameof(MaterialDeathPuddleShape));
            RequireRecoveredComponent<MaterialDeathPuddleStyle>(entityManager, activePuddle, nameof(MaterialDeathPuddleStyle));
            RequireRecoveredComponent<MaterialDeathPuddleFluid>(entityManager, activePuddle, nameof(MaterialDeathPuddleFluid));

            // Confirm authored shape values land on the per-instance component instead of leaking the material default.
            float4 appliedShape = entityManager.GetComponentData<MaterialDeathPuddleShape>(activePuddle).Value;

            if (!math.all(appliedShape.xyz == new float3(expectedEdgeIrregularity, expectedBorderWidth, expectedEdgeFeather)))
                throw new InvalidOperationException("The death puddle ECS pool did not propagate authored shape values to the per-instance material override.");

            float4 appliedFluid = entityManager.GetComponentData<MaterialDeathPuddleFluid>(activePuddle).Value;

            if (!math.all(appliedFluid == new float4(expectedFlowSpeed, expectedViscosity, expectedSurfaceDistortion, expectedHighlightStrength)))
                throw new InvalidOperationException("The death puddle ECS pool did not propagate authored fluid values to the per-instance material override.");

            // Force the same entity through the inactive pool and confirm a later preset cannot inherit stale shape data.
            entityManager.SetComponentEnabled<EnemyDeathPuddleActive>(activePuddle, false);
            const float reusedEdgeIrregularity = 0.07f;
            const float reusedBorderWidth = 0.04f;
            const float reusedEdgeFeather = 0.02f;
            DynamicBuffer<EnemyDeathPuddleSpawnRequest> reuseRequests = entityManager.GetBuffer<EnemyDeathPuddleSpawnRequest>(requestEntity);
            EnemyDeathPuddleSpawnRequest reuseRequest = new EnemyDeathPuddleSpawnRequest
            {
                PrefabEntity = prefabEntity,
                Position = new float3(4f, 0.02f, 5f),
                WorldSize = new float2(0.75f, 0.9f),
                LifetimeSeconds = 2f,
                StableFraction = 0.1f,
                FinalScaleRatio = 0.03f,
                EdgeIrregularity = reusedEdgeIrregularity,
                BorderWidth = reusedBorderWidth,
                EdgeFeather = reusedEdgeFeather,
                SecondaryPaletteBlend = 0.2f,
                FlowSpeed = 0.1f,
                Viscosity = 0.9f,
                SurfaceDistortion = 0.03f,
                HighlightStrength = 0.1f,
                PrimaryColor = expectedPrimaryColor,
                SecondaryColor = expectedSecondaryColor,
                Seed = 41u,
                EvaporationCurve = EnemyDeathPuddleEvaporationCurve.Linear,
                RandomRotation = 0
            };
            reuseRequests.Add(reuseRequest);
            spawnSystem.Update(world.Unmanaged);

            if (activePuddles.CalculateEntityCount() != 1)
                throw new InvalidOperationException("The death puddle ECS pool did not reactivate exactly one reused entity.");

            NativeArray<Entity> reusedPuddleEntities = activePuddles.ToEntityArray(Allocator.Temp);
            Entity reusedPuddle = reusedPuddleEntities[0];
            reusedPuddleEntities.Dispose();

            if (reusedPuddle != activePuddle)
                throw new InvalidOperationException("The death puddle ECS pool did not reuse the expected inactive entity.");

            float4 reusedShape = entityManager.GetComponentData<MaterialDeathPuddleShape>(reusedPuddle).Value;

            if (!math.all(reusedShape.xyz == new float3(reusedEdgeIrregularity, reusedBorderWidth, reusedEdgeFeather)) ||
                reusedShape.w < 0f ||
                reusedShape.w > 1f)
            {
                throw new InvalidOperationException("The death puddle ECS pool leaked stale shape data or an imprecise raw seed during reuse.");
            }
        }
        finally
        {
            world.Dispose();
        }
    }

    /// <summary>
    /// Asserts one per-instance material override has been recovered on the activated puddle so the shader's
    /// override-supported fallback never has to use the material default in production.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the activated puddle.</param>
    /// <param name="puddleEntity">Activated puddle entity being inspected.</param>
    /// <param name="componentLabel"> component label used in the failure message.</param>
    private static void RequireRecoveredComponent<TComponent>(EntityManager entityManager,
                                                              Entity puddleEntity,
                                                              string componentLabel) where TComponent : unmanaged, IComponentData
    {
        if (!entityManager.HasComponent<TComponent>(puddleEntity))
            throw new InvalidOperationException("The death puddle ECS pool did not recover a missing " + componentLabel + " material override.");
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
