#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws baked entity meshes through the regular Unity renderer when WebGL cannot start Entities Graphics.
/// ECS remains authoritative; compatible entities are grouped by mesh, material and material properties.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class WebGLEntitiesGraphicsFallbackSystem : SystemBase
{
    #region Constants
    private const int MaxInstancesPerDraw = 1023;
    private const string ToonDiffuseEcsShaderName = "Cel Shader/Toon Diffuse ECS";
    private const string ToonDiffuseShaderName = "Cel Shader/Toon Diffuse";
    private const string ToonDiffuseEcsHitFlashShaderName = "Cel Shader/Toon Diffuse ECS Hit Flash";
    private const string ToonDiffuseHitFlashShaderName = "Cel Shader/Toon Diffuse Hit Flash";
    private const string ToonDiffuseEcsBlurShaderName = "Cel Shader/Toon Diffuse ECS Blur";
    private const string ToonDiffuseBlurShaderName = "Cel Shader/Toon Diffuse Blur";
    private const string ToonOutlineEcsShaderName = "Cel Shader/Toon Outline ECS";
    private const string ToonOutlineShaderName = "BombasticBloodstreamBrigade/Toon Outline WebGL";
    private const string EnemyFacesEcsShaderName = "BombasticBloodstreamBrigade/Enemy Faces Flipbook ECS";
    private const string EnemyFacesWebGlShaderName = "BombasticBloodstreamBrigade/Enemy Faces Flipbook WebGL";
    #endregion

    #region Property Identifiers
    private static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int colorId = Shader.PropertyToID("_Color");
    private static readonly int hitFlashColorId = Shader.PropertyToID("_HitFlashColor");
    private static readonly int hitFlashBlendId = Shader.PropertyToID("_HitFlashBlend");
    private static readonly int outlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int outlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int faceFlipbookEnabledId = Shader.PropertyToID("_FaceFlipbookEnabled");
    private static readonly int faceFlipbookStateId = Shader.PropertyToID("_FaceFlipbookState");
    private static readonly int faceFlipbookPlaybackId = Shader.PropertyToID("_FaceFlipbookPlayback");
    private static readonly int faceIdleGridId = Shader.PropertyToID("_FaceIdleGrid");
    private static readonly int faceAttackGridId = Shader.PropertyToID("_FaceAttackGrid");
    private static readonly int faceDamageGridId = Shader.PropertyToID("_FaceDamageGrid");
    private static readonly int elasticHitDirectionId = Shader.PropertyToID("_ElasticHitDirection");
    private static readonly int elasticHitTimingId = Shader.PropertyToID("_ElasticHitTiming");
    private static readonly int elasticHitMotionId = Shader.PropertyToID("_ElasticHitMotion");
    private static readonly int puddlePrimaryColorId = Shader.PropertyToID("_PuddlePrimaryColor");
    private static readonly int puddleSecondaryColorId = Shader.PropertyToID("_PuddleSecondaryColor");
    private static readonly int puddleTimingId = Shader.PropertyToID("_PuddleTiming");
    private static readonly int puddleShapeId = Shader.PropertyToID("_PuddleShape");
    private static readonly int puddleStyleId = Shader.PropertyToID("_PuddleStyle");
    private static readonly int puddleFluidId = Shader.PropertyToID("_PuddleFluid");
    #endregion

    #region Fields
    private readonly Dictionary<Material, Material> webGlMaterials = new Dictionary<Material, Material>(64);
    private readonly Dictionary<DrawBatchKey, DrawBatch> drawBatches = new Dictionary<DrawBatchKey, DrawBatch>(128);
    private readonly Matrix4x4[] instanceMatrices = new Matrix4x4[MaxInstancesPerDraw];
    private EntityQuery renderEntityQuery;
    private MaterialPropertyBlock propertyBlock;
    #endregion

    #region Lifecycle
    protected override void OnCreate()
    {
        renderEntityQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<MaterialMeshInfo>(),
                ComponentType.ReadOnly<RenderMeshArray>()
            },
            None = new ComponentType[]
            {
                ComponentType.ReadOnly<DisableRendering>(),
                ComponentType.ReadOnly<Prefab>(),
                ComponentType.ReadOnly<Disabled>()
            }
        });

        propertyBlock = new MaterialPropertyBlock();
        RequireForUpdate(renderEntityQuery);
    }

    protected override void OnDestroy()
    {
        foreach (KeyValuePair<Material, Material> materialPair in webGlMaterials)
        {
            if (materialPair.Value != null && materialPair.Value != materialPair.Key)
                UnityEngine.Object.Destroy(materialPair.Value);
        }

        webGlMaterials.Clear();
        drawBatches.Clear();
        propertyBlock = null;
    }

    protected override void OnUpdate()
    {
        if (SystemInfo.supportsComputeShaders)
            return;

        Dependency.Complete();
        ResetBatches();
        NativeArray<Entity> renderEntities = renderEntityQuery.ToEntityArray(Allocator.Temp);

        try
        {
            for (int entityIndex = 0; entityIndex < renderEntities.Length; entityIndex++)
                QueueEntity(renderEntities[entityIndex]);
        }
        finally
        {
            renderEntities.Dispose();
        }

        RenderQueuedBatches();
    }
    #endregion

    #region Queueing
    private void ResetBatches()
    {
        foreach (DrawBatch batch in drawBatches.Values)
            batch.Reset();
    }

    private void QueueEntity(Entity renderEntity)
    {
        MaterialMeshInfo materialMeshInfo = EntityManager.GetComponentData<MaterialMeshInfo>(renderEntity);
        RenderMeshArray renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(renderEntity);
        Matrix4x4 matrix = ToMatrix4x4(EntityManager.GetComponentData<LocalToWorld>(renderEntity).Value);
        MaterialProperties properties = CaptureProperties(renderEntity);

        if (materialMeshInfo.HasMaterialMeshIndexRange)
        {
            RangeInt range = materialMeshInfo.MaterialMeshIndexRange;

            for (int rangeIndex = range.start; rangeIndex < range.end; rangeIndex++)
            {
                MaterialMeshIndex materialMeshIndex = renderMeshArray.MaterialMeshIndices[rangeIndex];
                QueueMesh(renderMeshArray.MeshReferences[materialMeshIndex.MeshIndex],
                          renderMeshArray.MaterialReferences[materialMeshIndex.MaterialIndex],
                          materialMeshIndex.SubMeshIndex,
                          matrix,
                          properties);
            }

            return;
        }

        QueueMesh(renderMeshArray.GetMesh(materialMeshInfo),
                  renderMeshArray.GetMaterial(materialMeshInfo),
                  materialMeshInfo.SubMesh,
                  matrix,
                  properties);
    }

    private void QueueMesh(Mesh mesh,
                           Material sourceMaterial,
                           int subMeshIndex,
                           Matrix4x4 matrix,
                           MaterialProperties properties)
    {
        if (mesh == null || sourceMaterial == null || subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
            return;

        Material material = ResolveWebGlMaterial(sourceMaterial);

        if (material == null || material.shader == null || !material.shader.isSupported)
            return;

        Bounds worldBounds = TransformBounds(mesh.bounds, matrix);
        DrawBatchKey key = new DrawBatchKey(mesh, material, subMeshIndex, properties);

        if (!drawBatches.TryGetValue(key, out DrawBatch batch))
        {
            batch = new DrawBatch();
            drawBatches.Add(key, batch);
        }

        batch.Add(matrix, worldBounds);
    }
    #endregion

    #region Rendering
    private void RenderQueuedBatches()
    {
        foreach (KeyValuePair<DrawBatchKey, DrawBatch> batchPair in drawBatches)
        {
            DrawBatch batch = batchPair.Value;

            if (batch.Matrices.Count == 0)
                continue;

            DrawBatchKey key = batchPair.Key;
            ApplyProperties(key.Properties);
            RenderParams renderParams = new RenderParams(key.Material)
            {
                layer = 0,
                matProps = propertyBlock,
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.On,
                worldBounds = batch.WorldBounds
            };

            if (!key.Material.enableInstancing || batch.Matrices.Count == 1)
            {
                for (int matrixIndex = 0; matrixIndex < batch.Matrices.Count; matrixIndex++)
                {
                    Matrix4x4 matrix = batch.Matrices[matrixIndex];
                    Graphics.RenderMesh(in renderParams, key.Mesh, key.SubMeshIndex, matrix);
                }

                continue;
            }

            for (int startInstance = 0; startInstance < batch.Matrices.Count; startInstance += MaxInstancesPerDraw)
            {
                int instanceCount = Mathf.Min(MaxInstancesPerDraw, batch.Matrices.Count - startInstance);
                batch.Matrices.CopyTo(startInstance, instanceMatrices, 0, instanceCount);
                Graphics.RenderMeshInstanced<Matrix4x4>(renderParams,
                                                        key.Mesh,
                                                        key.SubMeshIndex,
                                                        instanceMatrices,
                                                        instanceCount);
            }
        }
    }
    #endregion

    #region Material Properties
    private MaterialProperties CaptureProperties(Entity renderEntity)
    {
        MaterialProperties properties = default;

        if (EntityManager.HasComponent<URPMaterialPropertyBaseColor>(renderEntity))
            properties.Set(PropertyFlags.BaseColor, EntityManager.GetComponentData<URPMaterialPropertyBaseColor>(renderEntity).Value);
        else if (EntityManager.HasComponent<MaterialColor>(renderEntity))
            properties.Set(PropertyFlags.Color, EntityManager.GetComponentData<MaterialColor>(renderEntity).Value);

        if (EntityManager.HasComponent<MaterialHitFlashColor>(renderEntity))
            properties.Set(PropertyFlags.HitFlashColor, EntityManager.GetComponentData<MaterialHitFlashColor>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialHitFlashBlend>(renderEntity))
            properties.Set(PropertyFlags.HitFlashBlend, new float4(EntityManager.GetComponentData<MaterialHitFlashBlend>(renderEntity).Value));
        if (EntityManager.HasComponent<MaterialOutlineColor>(renderEntity))
            properties.Set(PropertyFlags.OutlineColor, EntityManager.GetComponentData<MaterialOutlineColor>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialOutlineThickness>(renderEntity))
            properties.Set(PropertyFlags.OutlineThickness, new float4(EntityManager.GetComponentData<MaterialOutlineThickness>(renderEntity).Value));
        if (EntityManager.HasComponent<MaterialFaceFlipbookEnabled>(renderEntity))
            properties.Set(PropertyFlags.FaceEnabled, new float4(EntityManager.GetComponentData<MaterialFaceFlipbookEnabled>(renderEntity).Value));
        if (EntityManager.HasComponent<MaterialFaceFlipbookState>(renderEntity))
            properties.Set(PropertyFlags.FaceState, new float4(EntityManager.GetComponentData<MaterialFaceFlipbookState>(renderEntity).Value));
        if (EntityManager.HasComponent<MaterialFaceFlipbookPlayback>(renderEntity))
            properties.Set(PropertyFlags.FacePlayback, EntityManager.GetComponentData<MaterialFaceFlipbookPlayback>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialFaceIdleGrid>(renderEntity))
            properties.Set(PropertyFlags.FaceIdleGrid, EntityManager.GetComponentData<MaterialFaceIdleGrid>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialFaceAttackGrid>(renderEntity))
            properties.Set(PropertyFlags.FaceAttackGrid, EntityManager.GetComponentData<MaterialFaceAttackGrid>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialFaceDamageGrid>(renderEntity))
            properties.Set(PropertyFlags.FaceDamageGrid, EntityManager.GetComponentData<MaterialFaceDamageGrid>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialElasticHitDirection>(renderEntity))
            properties.Set(PropertyFlags.ElasticDirection, EntityManager.GetComponentData<MaterialElasticHitDirection>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialElasticHitTiming>(renderEntity))
            properties.Set(PropertyFlags.ElasticTiming, EntityManager.GetComponentData<MaterialElasticHitTiming>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialElasticHitMotion>(renderEntity))
            properties.Set(PropertyFlags.ElasticMotion, EntityManager.GetComponentData<MaterialElasticHitMotion>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialDeathPuddlePrimaryColor>(renderEntity))
            properties.Set(PropertyFlags.PuddlePrimaryColor, EntityManager.GetComponentData<MaterialDeathPuddlePrimaryColor>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialDeathPuddleSecondaryColor>(renderEntity))
            properties.Set(PropertyFlags.PuddleSecondaryColor, EntityManager.GetComponentData<MaterialDeathPuddleSecondaryColor>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialDeathPuddleTiming>(renderEntity))
            properties.Set(PropertyFlags.PuddleTiming, EntityManager.GetComponentData<MaterialDeathPuddleTiming>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialDeathPuddleShape>(renderEntity))
            properties.Set(PropertyFlags.PuddleShape, EntityManager.GetComponentData<MaterialDeathPuddleShape>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialDeathPuddleStyle>(renderEntity))
            properties.Set(PropertyFlags.PuddleStyle, EntityManager.GetComponentData<MaterialDeathPuddleStyle>(renderEntity).Value);
        if (EntityManager.HasComponent<MaterialDeathPuddleFluid>(renderEntity))
            properties.Set(PropertyFlags.PuddleFluid, EntityManager.GetComponentData<MaterialDeathPuddleFluid>(renderEntity).Value);
        return properties;
    }

    private void ApplyProperties(MaterialProperties properties)
    {
        propertyBlock.Clear();
        ApplyColor(properties, PropertyFlags.BaseColor, baseColorId);
        ApplyColor(properties, PropertyFlags.Color, colorId);
        ApplyColor(properties, PropertyFlags.HitFlashColor, hitFlashColorId);
        ApplyFloat(properties, PropertyFlags.HitFlashBlend, hitFlashBlendId);
        ApplyColor(properties, PropertyFlags.OutlineColor, outlineColorId);
        ApplyFloat(properties, PropertyFlags.OutlineThickness, outlineThicknessId);
        ApplyFloat(properties, PropertyFlags.FaceEnabled, faceFlipbookEnabledId);
        ApplyFloat(properties, PropertyFlags.FaceState, faceFlipbookStateId);
        ApplyVector(properties, PropertyFlags.FacePlayback, faceFlipbookPlaybackId);
        ApplyVector(properties, PropertyFlags.FaceIdleGrid, faceIdleGridId);
        ApplyVector(properties, PropertyFlags.FaceAttackGrid, faceAttackGridId);
        ApplyVector(properties, PropertyFlags.FaceDamageGrid, faceDamageGridId);
        ApplyVector(properties, PropertyFlags.ElasticDirection, elasticHitDirectionId);
        ApplyVector(properties, PropertyFlags.ElasticTiming, elasticHitTimingId);
        ApplyVector(properties, PropertyFlags.ElasticMotion, elasticHitMotionId);
        ApplyColor(properties, PropertyFlags.PuddlePrimaryColor, puddlePrimaryColorId);
        ApplyColor(properties, PropertyFlags.PuddleSecondaryColor, puddleSecondaryColorId);
        ApplyVector(properties, PropertyFlags.PuddleTiming, puddleTimingId);
        ApplyVector(properties, PropertyFlags.PuddleShape, puddleShapeId);
        ApplyVector(properties, PropertyFlags.PuddleStyle, puddleStyleId);
        ApplyVector(properties, PropertyFlags.PuddleFluid, puddleFluidId);
    }

    private void ApplyColor(MaterialProperties properties, PropertyFlags flag, int propertyId)
    {
        if (properties.TryGet(flag, out Vector4 value))
            propertyBlock.SetColor(propertyId, new Color(value.x, value.y, value.z, value.w));
    }

    private void ApplyFloat(MaterialProperties properties, PropertyFlags flag, int propertyId)
    {
        if (properties.TryGet(flag, out Vector4 value))
            propertyBlock.SetFloat(propertyId, value.x);
    }

    private void ApplyVector(MaterialProperties properties, PropertyFlags flag, int propertyId)
    {
        if (properties.TryGet(flag, out Vector4 value))
            propertyBlock.SetVector(propertyId, value);
    }
    #endregion

    #region Material Resolution
    private Material ResolveWebGlMaterial(Material sourceMaterial)
    {
        if (webGlMaterials.TryGetValue(sourceMaterial, out Material resolvedMaterial))
            return resolvedMaterial;

        Shader fallbackShader = ResolveFallbackShader(sourceMaterial.shader);
        resolvedMaterial = new Material(sourceMaterial)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        if (fallbackShader != null)
            resolvedMaterial.shader = fallbackShader;

        resolvedMaterial.enableInstancing = sourceMaterial.enableInstancing || fallbackShader != null;
        webGlMaterials[sourceMaterial] = resolvedMaterial;
        return resolvedMaterial;
    }

    private static Shader ResolveFallbackShader(Shader sourceShader)
    {
        if (sourceShader == null)
            return null;

        string fallbackShaderName;

        switch (sourceShader.name)
        {
            case ToonDiffuseEcsShaderName:
                fallbackShaderName = ToonDiffuseShaderName;
                break;
            case ToonDiffuseEcsHitFlashShaderName:
                fallbackShaderName = ToonDiffuseHitFlashShaderName;
                break;
            case ToonDiffuseEcsBlurShaderName:
                fallbackShaderName = ToonDiffuseBlurShaderName;
                break;
            case ToonOutlineEcsShaderName:
                fallbackShaderName = ToonOutlineShaderName;
                break;
            case EnemyFacesEcsShaderName:
                fallbackShaderName = EnemyFacesWebGlShaderName;
                break;
            default:
                return null;
        }

        Shader fallbackShader = Shader.Find(fallbackShaderName);
        return fallbackShader != null && fallbackShader.isSupported ? fallbackShader : null;
    }
    #endregion

    #region Geometry
    private static Matrix4x4 ToMatrix4x4(float4x4 value)
    {
        return new Matrix4x4(new Vector4(value.c0.x, value.c0.y, value.c0.z, value.c0.w),
                             new Vector4(value.c1.x, value.c1.y, value.c1.z, value.c1.w),
                             new Vector4(value.c2.x, value.c2.y, value.c2.z, value.c2.w),
                             new Vector4(value.c3.x, value.c3.y, value.c3.z, value.c3.w));
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
        Vector3 worldExtents = new Vector3(Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                                           Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                                           Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, worldExtents * 2f);
    }
    #endregion

    #region Batch Types
    [Flags]
    private enum PropertyFlags : uint
    {
        BaseColor = 1u << 0,
        Color = 1u << 1,
        HitFlashColor = 1u << 2,
        HitFlashBlend = 1u << 3,
        OutlineColor = 1u << 4,
        OutlineThickness = 1u << 5,
        FaceEnabled = 1u << 6,
        FaceState = 1u << 7,
        FacePlayback = 1u << 8,
        FaceIdleGrid = 1u << 9,
        FaceAttackGrid = 1u << 10,
        FaceDamageGrid = 1u << 11,
        ElasticDirection = 1u << 12,
        ElasticTiming = 1u << 13,
        ElasticMotion = 1u << 14,
        PuddlePrimaryColor = 1u << 15,
        PuddleSecondaryColor = 1u << 16,
        PuddleTiming = 1u << 17,
        PuddleShape = 1u << 18,
        PuddleStyle = 1u << 19,
        PuddleFluid = 1u << 20
    }

    private struct MaterialProperties : IEquatable<MaterialProperties>
    {
        public PropertyFlags Flags;
        private Vector4 value0;
        private Vector4 value1;
        private Vector4 value2;
        private Vector4 value3;
        private Vector4 value4;
        private Vector4 value5;
        private Vector4 value6;
        private Vector4 value7;
        private Vector4 value8;
        private Vector4 value9;
        private Vector4 value10;
        private Vector4 value11;
        private Vector4 value12;
        private Vector4 value13;
        private Vector4 value14;
        private Vector4 value15;
        private Vector4 value16;
        private Vector4 value17;
        private Vector4 value18;
        private Vector4 value19;
        private Vector4 value20;

        public void Set(PropertyFlags flag, float4 value)
        {
            Set(flag, new Vector4(value.x, value.y, value.z, value.w));
        }

        public void Set(PropertyFlags flag, Vector4 value)
        {
            Flags |= flag;
            int index = FlagIndex(flag);

            switch (index)
            {
                case 0: value0 = value; break;
                case 1: value1 = value; break;
                case 2: value2 = value; break;
                case 3: value3 = value; break;
                case 4: value4 = value; break;
                case 5: value5 = value; break;
                case 6: value6 = value; break;
                case 7: value7 = value; break;
                case 8: value8 = value; break;
                case 9: value9 = value; break;
                case 10: value10 = value; break;
                case 11: value11 = value; break;
                case 12: value12 = value; break;
                case 13: value13 = value; break;
                case 14: value14 = value; break;
                case 15: value15 = value; break;
                case 16: value16 = value; break;
                case 17: value17 = value; break;
                case 18: value18 = value; break;
                case 19: value19 = value; break;
                case 20: value20 = value; break;
            }
        }

        public bool TryGet(PropertyFlags flag, out Vector4 value)
        {
            if ((Flags & flag) == 0)
            {
                value = default;
                return false;
            }

            switch (FlagIndex(flag))
            {
                case 0: value = value0; return true;
                case 1: value = value1; return true;
                case 2: value = value2; return true;
                case 3: value = value3; return true;
                case 4: value = value4; return true;
                case 5: value = value5; return true;
                case 6: value = value6; return true;
                case 7: value = value7; return true;
                case 8: value = value8; return true;
                case 9: value = value9; return true;
                case 10: value = value10; return true;
                case 11: value = value11; return true;
                case 12: value = value12; return true;
                case 13: value = value13; return true;
                case 14: value = value14; return true;
                case 15: value = value15; return true;
                case 16: value = value16; return true;
                case 17: value = value17; return true;
                case 18: value = value18; return true;
                case 19: value = value19; return true;
                case 20: value = value20; return true;
                default: value = default; return false;
            }
        }

        public bool Equals(MaterialProperties other)
        {
            return Flags == other.Flags &&
                   value0 == other.value0 && value1 == other.value1 && value2 == other.value2 &&
                   value3 == other.value3 && value4 == other.value4 && value5 == other.value5 &&
                   value6 == other.value6 && value7 == other.value7 && value8 == other.value8 &&
                   value9 == other.value9 && value10 == other.value10 && value11 == other.value11 &&
                   value12 == other.value12 && value13 == other.value13 && value14 == other.value14 &&
                   value15 == other.value15 && value16 == other.value16 && value17 == other.value17 &&
                   value18 == other.value18 && value19 == other.value19 && value20 == other.value20;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialProperties other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Flags;
                hash = hash * 31 + value0.GetHashCode();
                hash = hash * 31 + value1.GetHashCode();
                hash = hash * 31 + value2.GetHashCode();
                hash = hash * 31 + value3.GetHashCode();
                hash = hash * 31 + value4.GetHashCode();
                hash = hash * 31 + value5.GetHashCode();
                hash = hash * 31 + value6.GetHashCode();
                hash = hash * 31 + value7.GetHashCode();
                hash = hash * 31 + value8.GetHashCode();
                hash = hash * 31 + value9.GetHashCode();
                hash = hash * 31 + value10.GetHashCode();
                hash = hash * 31 + value11.GetHashCode();
                hash = hash * 31 + value12.GetHashCode();
                hash = hash * 31 + value13.GetHashCode();
                hash = hash * 31 + value14.GetHashCode();
                hash = hash * 31 + value15.GetHashCode();
                hash = hash * 31 + value16.GetHashCode();
                hash = hash * 31 + value17.GetHashCode();
                hash = hash * 31 + value18.GetHashCode();
                hash = hash * 31 + value19.GetHashCode();
                hash = hash * 31 + value20.GetHashCode();
                return hash;
            }
        }

        private static int FlagIndex(PropertyFlags flag)
        {
            uint value = (uint)flag;
            int index = 0;

            while (value > 1u)
            {
                value >>= 1;
                index++;
            }

            return index;
        }
    }

    private readonly struct DrawBatchKey : IEquatable<DrawBatchKey>
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly int SubMeshIndex;
        public readonly MaterialProperties Properties;

        public DrawBatchKey(Mesh mesh, Material material, int subMeshIndex, MaterialProperties properties)
        {
            Mesh = mesh;
            Material = material;
            SubMeshIndex = subMeshIndex;
            Properties = properties;
        }

        public bool Equals(DrawBatchKey other)
        {
            return ReferenceEquals(Mesh, other.Mesh) &&
                   ReferenceEquals(Material, other.Material) &&
                   SubMeshIndex == other.SubMeshIndex &&
                   Properties.Equals(other.Properties);
        }

        public override bool Equals(object obj)
        {
            return obj is DrawBatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Mesh != null ? Mesh.GetInstanceID() : 0;
                hash = hash * 31 + (Material != null ? Material.GetInstanceID() : 0);
                hash = hash * 31 + SubMeshIndex;
                hash = hash * 31 + Properties.GetHashCode();
                return hash;
            }
        }
    }

    private sealed class DrawBatch
    {
        public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
        public Bounds WorldBounds;
        private bool hasBounds;

        public void Add(Matrix4x4 matrix, Bounds bounds)
        {
            Matrices.Add(matrix);

            if (!hasBounds)
            {
                WorldBounds = bounds;
                hasBounds = true;
                return;
            }

            WorldBounds.Encapsulate(bounds);
        }

        public void Reset()
        {
            Matrices.Clear();
            WorldBounds = default;
            hasBounds = false;
        }
    }
    #endregion
}
#endif
