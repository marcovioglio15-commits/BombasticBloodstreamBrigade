using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Samples mesh-authored enemy visual colors for death debris palette generation.
/// </summary>
internal static class EnemyVisualMeshColorSamplingUtility
{
    #region Constants
    private const int MaximumMeshSamples = 512;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Samples mesh vertex colors and material textures through UVs so atlas averages do not pollute debris colors.
    /// </summary>
    /// <param name="renderer">Mesh renderer or skinned mesh renderer inspected for visual color data.</param>
    /// <param name="paletteColors">Mutable palette color buckets.</param>
    /// <param name="paletteWeights">Mutable palette sample weights matching paletteColors.</param>
    /// <param name="bucketCount">Current number of active palette buckets.</param>
    /// <returns>True when at least one mesh-derived color was appended.</returns>
    public static bool TryAppendMeshVisualColors(Renderer renderer,
                                                 float4[] paletteColors,
                                                 int[] paletteWeights,
                                                 ref int bucketCount)
    {
        if (!TryResolveRendererMesh(renderer, out Mesh mesh))
            return false;

        Mesh.MeshDataArray meshDataArray;

        try
        {
            meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
        }
        catch (UnityException)
        {
            return false;
        }

        try
        {
            if (meshDataArray.Length <= 0)
                return false;

            Mesh.MeshData meshData = meshDataArray[0];
            return TryAppendMeshDataVisualColors(meshData,
                                                 renderer,
                                                 paletteColors,
                                                 paletteWeights,
                                                 ref bucketCount);
        }
        finally
        {
            meshDataArray.Dispose();
        }
    }
    #endregion

    #region Mesh Resolution
    /// <summary>
    /// Resolves the mesh used by a MeshRenderer or SkinnedMeshRenderer.
    /// </summary>
    /// <param name="renderer">Renderer inspected for mesh data.</param>
    /// <param name="mesh">Resolved shared mesh.</param>
    /// <returns>True when a shared mesh is available.</returns>
    private static bool TryResolveRendererMesh(Renderer renderer, out Mesh mesh)
    {
        mesh = null;

        SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;

        if (skinnedMeshRenderer != null)
        {
            mesh = skinnedMeshRenderer.sharedMesh;
            return mesh != null;
        }

        MeshRenderer meshRenderer = renderer as MeshRenderer;

        if (meshRenderer == null)
            return false;

        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();

        if (meshFilter == null)
            return false;

        mesh = meshFilter.sharedMesh;
        return mesh != null;
    }

    /// <summary>
    /// Resolves the material assigned to a submesh, falling back to the first material when the submesh index is out of range.
    /// </summary>
    /// <param name="materials">Renderer material array.</param>
    /// <param name="subMeshIndex">Submesh index requesting a material.</param>
    /// <returns>Resolved material, or null when no material is assigned.</returns>
    private static Material ResolveSubMeshMaterial(Material[] materials, int subMeshIndex)
    {
        if (materials == null || materials.Length <= 0)
            return null;

        if (subMeshIndex >= 0 && subMeshIndex < materials.Length)
            return materials[subMeshIndex];

        return materials[0];
    }
    #endregion

    #region Mesh Data Sampling
    /// <summary>
    /// Samples one mesh data snapshot using UV-mapped texture pixels and optional vertex colors.
    /// </summary>
    /// <param name="meshData">Read-only mesh data snapshot.</param>
    /// <param name="renderer">Renderer providing submesh materials.</param>
    /// <param name="paletteColors">Mutable palette color buckets.</param>
    /// <param name="paletteWeights">Mutable palette sample weights matching paletteColors.</param>
    /// <param name="bucketCount">Current number of active palette buckets.</param>
    /// <returns>True when at least one mesh-derived color was appended.</returns>
    private static bool TryAppendMeshDataVisualColors(Mesh.MeshData meshData,
                                                      Renderer renderer,
                                                      float4[] paletteColors,
                                                      int[] paletteWeights,
                                                      ref int bucketCount)
    {
        int vertexCount = meshData.vertexCount;

        if (vertexCount <= 0)
            return false;

        NativeArray<Color32> vertexColors = default;
        NativeArray<Vector2> uvs = default;
        bool hasVertexColors = TryReadVertexColors(meshData, vertexCount, out vertexColors);
        bool hasUvs = TryReadUvs(meshData, vertexCount, out uvs);

        try
        {
            Material[] sharedMaterials = renderer.sharedMaterials;
            bool appendedAnyColor = false;
            int subMeshCount = math.max(1, meshData.subMeshCount);

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = ResolveSubMeshMaterial(sharedMaterials, subMeshIndex);
                Color materialTint = EnemyVisualColorSamplingUtility.TryResolveMaterialTint(material, out Color tintColor) ? tintColor : Color.white;
                Texture2D texture = null;
                bool ownsTexture = false;

                if (hasUvs)
                {
                    Texture sourceTexture = material != null ? EnemyVisualColorSamplingUtility.ResolveMaterialTexture(material) : null;
                    EnemyVisualReadableTextureUtility.TryResolveReadableTexture(sourceTexture, out texture, out ownsTexture);
                }

                try
                {
                    if (!hasVertexColors && texture == null)
                        continue;

                    if (TryAppendSubMeshVisualColors(meshData,
                                                     subMeshIndex,
                                                     vertexColors,
                                                     uvs,
                                                     hasVertexColors,
                                                     hasUvs,
                                                     texture,
                                                     materialTint,
                                                     paletteColors,
                                                     paletteWeights,
                                                     ref bucketCount))
                        appendedAnyColor = true;
                }
                finally
                {
                    EnemyVisualReadableTextureUtility.ReleaseReadableTexture(texture, ownsTexture);
                }
            }

            return appendedAnyColor;
        }
        finally
        {
            if (vertexColors.IsCreated)
                vertexColors.Dispose();

            if (uvs.IsCreated)
                uvs.Dispose();
        }
    }

    /// <summary>
    /// Attempts to copy mesh vertex colors into a temporary native array.
    /// </summary>
    /// <param name="meshData">Read-only mesh data snapshot.</param>
    /// <param name="vertexCount">Expected vertex count.</param>
    /// <param name="vertexColors">Temporary color array populated on success.</param>
    /// <returns>True when vertex colors were available and copied.</returns>
    private static bool TryReadVertexColors(Mesh.MeshData meshData, int vertexCount, out NativeArray<Color32> vertexColors)
    {
        vertexColors = default;

        if (!meshData.HasVertexAttribute(VertexAttribute.Color))
            return false;

        try
        {
            vertexColors = new NativeArray<Color32>(vertexCount, Allocator.Temp);
            meshData.GetColors(vertexColors);
            return true;
        }
        catch (UnityException)
        {
            if (vertexColors.IsCreated)
                vertexColors.Dispose();

            vertexColors = default;
            return false;
        }
    }

    /// <summary>
    /// Attempts to copy mesh UV0 data into a temporary native array.
    /// </summary>
    /// <param name="meshData">Read-only mesh data snapshot.</param>
    /// <param name="vertexCount">Expected vertex count.</param>
    /// <param name="uvs">Temporary UV array populated on success.</param>
    /// <returns>True when UV0 data was available and copied.</returns>
    private static bool TryReadUvs(Mesh.MeshData meshData, int vertexCount, out NativeArray<Vector2> uvs)
    {
        uvs = default;

        if (!meshData.HasVertexAttribute(VertexAttribute.TexCoord0))
            return false;

        try
        {
            uvs = new NativeArray<Vector2>(vertexCount, Allocator.Temp);
            meshData.GetUVs(0, uvs);
            return true;
        }
        catch (UnityException)
        {
            if (uvs.IsCreated)
                uvs.Dispose();

            uvs = default;
            return false;
        }
    }
    #endregion

    #region Submesh Sampling
    /// <summary>
    /// Samples one submesh through vertex colors and UV texture lookups.
    /// </summary>
    /// <param name="meshData">Read-only mesh data snapshot.</param>
    /// <param name="subMeshIndex">Submesh index being inspected.</param>
    /// <param name="vertexColors">Mesh vertex colors, when authored.</param>
    /// <param name="uvs">Mesh UVs, when authored.</param>
    /// <param name="hasVertexColors">True when vertexColors matches the mesh vertex count.</param>
    /// <param name="hasUvs">True when uvs matches the mesh vertex count.</param>
    /// <param name="texture">Readable material texture, or null when unavailable.</param>
    /// <param name="materialTint">Material tint multiplied into every sample.</param>
    /// <param name="paletteColors">Mutable palette color buckets.</param>
    /// <param name="paletteWeights">Mutable palette sample weights matching paletteColors.</param>
    /// <param name="bucketCount">Current number of active palette buckets.</param>
    /// <returns>True when at least one color sample was appended.</returns>
    private static bool TryAppendSubMeshVisualColors(Mesh.MeshData meshData,
                                                     int subMeshIndex,
                                                     NativeArray<Color32> vertexColors,
                                                     NativeArray<Vector2> uvs,
                                                     bool hasVertexColors,
                                                     bool hasUvs,
                                                     Texture2D texture,
                                                     Color materialTint,
                                                     float4[] paletteColors,
                                                     int[] paletteWeights,
                                                     ref int bucketCount)
    {
        if (subMeshIndex < 0 || subMeshIndex >= meshData.subMeshCount)
            return TryAppendVertexRangeVisualColors(meshData.vertexCount,
                                                    vertexColors,
                                                    uvs,
                                                    hasVertexColors,
                                                    hasUvs,
                                                    texture,
                                                    materialTint,
                                                    paletteColors,
                                                    paletteWeights,
                                                    ref bucketCount);

        SubMeshDescriptor subMeshDescriptor = meshData.GetSubMesh(subMeshIndex);

        if (subMeshDescriptor.indexCount <= 0)
            return TryAppendVertexRangeVisualColors(meshData.vertexCount,
                                                    vertexColors,
                                                    uvs,
                                                    hasVertexColors,
                                                    hasUvs,
                                                    texture,
                                                    materialTint,
                                                    paletteColors,
                                                    paletteWeights,
                                                    ref bucketCount);

        int stride = math.max(1, subMeshDescriptor.indexCount / MaximumMeshSamples);
        bool appendedAnyColor = false;

        for (int indexOffset = 0; indexOffset < subMeshDescriptor.indexCount; indexOffset += stride)
        {
            int indexPosition = subMeshDescriptor.indexStart + indexOffset;

            if (!TryReadSubMeshVertexIndex(meshData, indexPosition, subMeshDescriptor.baseVertex, out int vertexIndex))
                continue;

            if (vertexIndex < 0 || vertexIndex >= meshData.vertexCount)
                continue;

            AppendMeshVertexVisualColor(vertexIndex,
                                        vertexColors,
                                        uvs,
                                        hasVertexColors,
                                        hasUvs,
                                        texture,
                                        materialTint,
                                        paletteColors,
                                        paletteWeights,
                                        ref bucketCount);
            appendedAnyColor = true;
        }

        return appendedAnyColor;
    }

    /// <summary>
    /// Samples mesh vertices directly when submesh index data is unavailable.
    /// </summary>
    /// <param name="vertexCount">Mesh vertex count.</param>
    /// <param name="vertexColors">Mesh vertex colors, when authored.</param>
    /// <param name="uvs">Mesh UVs, when authored.</param>
    /// <param name="hasVertexColors">True when vertexColors matches the mesh vertex count.</param>
    /// <param name="hasUvs">True when uvs matches the mesh vertex count.</param>
    /// <param name="texture">Readable material texture, or null when unavailable.</param>
    /// <param name="materialTint">Material tint multiplied into every sample.</param>
    /// <param name="paletteColors">Mutable palette color buckets.</param>
    /// <param name="paletteWeights">Mutable palette sample weights matching paletteColors.</param>
    /// <param name="bucketCount">Current number of active palette buckets.</param>
    /// <returns>True when at least one color sample was appended.</returns>
    private static bool TryAppendVertexRangeVisualColors(int vertexCount,
                                                         NativeArray<Color32> vertexColors,
                                                         NativeArray<Vector2> uvs,
                                                         bool hasVertexColors,
                                                         bool hasUvs,
                                                         Texture2D texture,
                                                         Color materialTint,
                                                         float4[] paletteColors,
                                                         int[] paletteWeights,
                                                         ref int bucketCount)
    {
        int stride = math.max(1, vertexCount / MaximumMeshSamples);
        bool appendedAnyColor = false;

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex += stride)
        {
            AppendMeshVertexVisualColor(vertexIndex,
                                        vertexColors,
                                        uvs,
                                        hasVertexColors,
                                        hasUvs,
                                        texture,
                                        materialTint,
                                        paletteColors,
                                        paletteWeights,
                                        ref bucketCount);
            appendedAnyColor = true;
        }

        return appendedAnyColor;
    }

    /// <summary>
    /// Reads one submesh vertex index while respecting the mesh index buffer format.
    /// </summary>
    /// <param name="meshData">Read-only mesh data snapshot.</param>
    /// <param name="indexPosition">Absolute index buffer position.</param>
    /// <param name="baseVertex">Base vertex offset stored on the submesh descriptor.</param>
    /// <param name="vertexIndex">Resolved vertex index.</param>
    /// <returns>True when a valid index value was read.</returns>
    private static bool TryReadSubMeshVertexIndex(Mesh.MeshData meshData, int indexPosition, int baseVertex, out int vertexIndex)
    {
        vertexIndex = 0;

        try
        {
            switch (meshData.indexFormat)
            {
                case IndexFormat.UInt16:
                    NativeArray<ushort> indexData16 = meshData.GetIndexData<ushort>();

                    if (indexPosition < 0 || indexPosition >= indexData16.Length)
                        return false;

                    vertexIndex = indexData16[indexPosition] + baseVertex;
                    return true;

                default:
                    NativeArray<int> indexData32 = meshData.GetIndexData<int>();

                    if (indexPosition < 0 || indexPosition >= indexData32.Length)
                        return false;

                    vertexIndex = indexData32[indexPosition] + baseVertex;
                    return true;
            }
        }
        catch (UnityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Appends one mesh vertex visual color after multiplying vertex color, texture color and material tint.
    /// </summary>
    /// <param name="vertexIndex">Vertex index to sample.</param>
    /// <param name="vertexColors">Mesh vertex colors, when authored.</param>
    /// <param name="uvs">Mesh UVs, when authored.</param>
    /// <param name="hasVertexColors">True when vertexColors matches the mesh vertex count.</param>
    /// <param name="hasUvs">True when uvs matches the mesh vertex count.</param>
    /// <param name="texture">Readable material texture, or null when unavailable.</param>
    /// <param name="materialTint">Material tint multiplied into the sample.</param>
    /// <param name="paletteColors">Mutable palette color buckets.</param>
    /// <param name="paletteWeights">Mutable palette sample weights matching paletteColors.</param>
    /// <param name="bucketCount">Current number of active palette buckets.</param>
    private static void AppendMeshVertexVisualColor(int vertexIndex,
                                                    NativeArray<Color32> vertexColors,
                                                    NativeArray<Vector2> uvs,
                                                    bool hasVertexColors,
                                                    bool hasUvs,
                                                    Texture2D texture,
                                                    Color materialTint,
                                                    float4[] paletteColors,
                                                    int[] paletteWeights,
                                                    ref int bucketCount)
    {
        Color sampledColor = hasVertexColors ? ToColor(vertexColors[vertexIndex]) : Color.white;

        if (texture != null && hasUvs && vertexIndex < uvs.Length)
            sampledColor = EnemyVisualColorSamplingUtility.Multiply(sampledColor,
                                                                    texture.GetPixelBilinear(WrapUv(uvs[vertexIndex].x),
                                                                                             WrapUv(uvs[vertexIndex].y)));

        sampledColor = EnemyVisualColorSamplingUtility.Multiply(sampledColor, materialTint);
        EnemyVisualColorSamplingUtility.AppendPaletteSample(paletteColors,
                                                            paletteWeights,
                                                            ref bucketCount,
                                                            EnemyVisualColorSamplingUtility.ToFloat4(sampledColor),
                                                            1);
    }
    #endregion

    #region Color Helpers
    /// <summary>
    /// Converts a Color32 sample to normalized managed Color.
    /// </summary>
    /// <param name="color">Source byte color.</param>
    /// <returns>Managed color with normalized channels.</returns>
    private static Color ToColor(Color32 color)
    {
        return new Color(color.r / 255f,
                         color.g / 255f,
                         color.b / 255f,
                         color.a / 255f);
    }

    /// <summary>
    /// Wraps a UV coordinate to the 0-1 range used by repeated textures.
    /// </summary>
    /// <param name="value">Source UV component.</param>
    /// <returns>Wrapped UV component.</returns>
    private static float WrapUv(float value)
    {
        return value - math.floor(value);
    }
    #endregion

    #endregion
}
