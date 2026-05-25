using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Resolves authored enemy renderer colors for bake-time visual feedback such as hit flash and death debris.
/// </summary>
public static class EnemyVisualColorSamplingUtility
{
    #region Constants
    private const int MaximumTextureSamples = 256;
    private const float DistinctColorDistanceThreshold = 0.08f;
    private const byte SingleColorCount = 1;
    private const byte PaletteColorCount = 2;

    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int TintColorPropertyId = Shader.PropertyToID("_TintColor");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the death debris palette from the enemy's actual authored renderers or the preset fallback.
    /// </summary>
    /// <param name="authoring">Enemy authoring component whose visual hierarchy is inspected during baking.</param>
    /// <returns>Palette used by managed death VFX requests.</returns>
    public static EnemyDeathDebrisColorPalette ResolveDeathDebrisPalette(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return CreateSingleColorPalette(Color.white);

        Color fallbackColor = authoring.DeathDebrisFallbackColor;

        if (!authoring.UseEnemyBaseColorForDeathDebris)
            return CreateSingleColorPalette(fallbackColor);

        Renderer[] renderers = authoring.GetComponentsInChildren<Renderer>(true);

        if (TryBuildRendererPalette(renderers, out EnemyDeathDebrisColorPalette palette))
            return palette;

        return CreateSingleColorPalette(fallbackColor);
    }

    /// <summary>
    /// Resolves one renderer's visual color with texture, tint and sprite color considered.
    /// </summary>
    /// <param name="renderer">Renderer inspected for authored visual color data.</param>
    /// <returns>Resolved renderer color, or white when no compatible color source exists.</returns>
    public static float4 ResolveRendererBaseColor(Renderer renderer)
    {
        if (TryResolveRendererColor(renderer, out float4 color))
            return color;

        return new float4(1f, 1f, 1f, 1f);
    }
    #endregion

    #region Palette
    /// <summary>
    /// Builds a compact two-color palette from renderers that belong to the enemy body.
    /// </summary>
    /// <param name="renderers">Renderer list sampled from the enemy prefab hierarchy.</param>
    /// <param name="palette">Resolved palette when at least one valid visual color is found.</param>
    /// <returns>True when a palette was resolved.</returns>
    private static bool TryBuildRendererPalette(Renderer[] renderers, out EnemyDeathDebrisColorPalette palette)
    {
        palette = default;

        if (renderers == null || renderers.Length <= 0)
            return false;

        float4 primaryColor = float4.zero;
        float4 secondaryColor = float4.zero;
        int colorCount = 0;

        // Sample only enemy body renderers so status bars, warnings and VFX helpers do not pollute debris colors.
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (!ShouldSampleRendererForDeathDebris(renderer))
                continue;

            if (!TryResolveRendererColor(renderer, out float4 rendererColor))
                continue;

            if (!math.all(math.isfinite(rendererColor)) || rendererColor.w <= 0.01f)
                continue;

            if (TryAppendDistinctColor(ref primaryColor, ref secondaryColor, ref colorCount, rendererColor))
            {
                if (colorCount >= PaletteColorCount)
                    break;
            }
        }

        if (colorCount <= 0)
            return false;

        if (colorCount == 1)
            secondaryColor = primaryColor;

        palette = new EnemyDeathDebrisColorPalette
        {
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            ColorCount = colorCount > 1 ? PaletteColorCount : SingleColorCount
        };
        return true;
    }

    /// <summary>
    /// Appends one renderer color when it is visually distinct from colors already in the palette.
    /// </summary>
    /// <param name="primaryColor">First palette color.</param>
    /// <param name="secondaryColor">Second palette color.</param>
    /// <param name="colorCount">Current number of accepted colors.</param>
    /// <param name="candidateColor">Candidate color sampled from a renderer.</param>
    /// <returns>True when the candidate changed the palette.</returns>
    private static bool TryAppendDistinctColor(ref float4 primaryColor,
                                               ref float4 secondaryColor,
                                               ref int colorCount,
                                               float4 candidateColor)
    {
        if (colorCount <= 0)
        {
            primaryColor = candidateColor;
            colorCount = 1;
            return true;
        }

        if (ColorDistance(primaryColor, candidateColor) < DistinctColorDistanceThreshold)
            return false;

        secondaryColor = candidateColor;
        colorCount = 2;
        return true;
    }

    /// <summary>
    /// Creates a single-color debris palette from a managed Color value.
    /// </summary>
    /// <param name="color">Source managed color.</param>
    /// <returns>Single-color palette with primary and secondary slots matching.</returns>
    private static EnemyDeathDebrisColorPalette CreateSingleColorPalette(Color color)
    {
        float4 resolvedColor = ToFloat4(color);

        return new EnemyDeathDebrisColorPalette
        {
            PrimaryColor = resolvedColor,
            SecondaryColor = resolvedColor,
            ColorCount = SingleColorCount
        };
    }
    #endregion

    #region Renderer Sampling
    /// <summary>
    /// Resolves one renderer color from SpriteRenderer tint, material textures, or material color properties.
    /// </summary>
    /// <param name="renderer">Renderer inspected for visual color data.</param>
    /// <param name="color">Resolved color when available.</param>
    /// <returns>True when the renderer exposes usable visual color data.</returns>
    private static bool TryResolveRendererColor(Renderer renderer, out float4 color)
    {
        color = default;

        if (renderer == null)
            return false;

        SpriteRenderer spriteRenderer = renderer as SpriteRenderer;

        if (spriteRenderer != null)
        {
            color = ToFloat4(spriteRenderer.color);
            return true;
        }

        Material[] sharedMaterials = renderer.sharedMaterials;

        if (sharedMaterials == null)
            return false;

        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
        {
            Material sharedMaterial = sharedMaterials[materialIndex];

            if (TryResolveMaterialColor(sharedMaterial, out color))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one material's visible color from base texture average multiplied by tint, or from tint alone.
    /// </summary>
    /// <param name="material">Material inspected for texture and color properties.</param>
    /// <param name="color">Resolved material color when available.</param>
    /// <returns>True when a usable material color was found.</returns>
    private static bool TryResolveMaterialColor(Material material, out float4 color)
    {
        color = default;

        if (material == null)
            return false;

        bool hasTintColor = TryResolveMaterialTint(material, out Color tintColor);

        if (TryResolveMaterialTextureAverage(material, out Color textureColor))
        {
            color = ToFloat4(Multiply(textureColor, tintColor));
            return true;
        }

        if (!hasTintColor)
            return false;

        color = ToFloat4(tintColor);
        return true;
    }

    /// <summary>
    /// Returns whether this renderer belongs to the enemy body instead of editor/runtime helper visuals.
    /// </summary>
    /// <param name="renderer">Renderer candidate from the enemy prefab hierarchy.</param>
    /// <returns>True when the renderer should contribute to death debris colors.</returns>
    private static bool ShouldSampleRendererForDeathDebris(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;

        return !HasExcludedHelperName(renderer.transform);
    }

    /// <summary>
    /// Checks the renderer transform chain for helper names that should not influence enemy debris colors.
    /// </summary>
    /// <param name="transform">Renderer transform to inspect.</param>
    /// <returns>True when the transform belongs to status, warning, billboard, or VFX helper hierarchy.</returns>
    private static bool HasExcludedHelperName(Transform transform)
    {
        Transform currentTransform = transform;

        while (currentTransform != null)
        {
            string objectName = currentTransform.name;

            if (ContainsOrdinalIgnoreCase(objectName, "Status") ||
                ContainsOrdinalIgnoreCase(objectName, "Health") ||
                ContainsOrdinalIgnoreCase(objectName, "Shield") ||
                ContainsOrdinalIgnoreCase(objectName, "Billboard") ||
                ContainsOrdinalIgnoreCase(objectName, "Warning") ||
                ContainsOrdinalIgnoreCase(objectName, "VFX"))
            {
                return true;
            }

            currentTransform = currentTransform.parent;
        }

        return false;
    }
    #endregion

    #region Material Sampling
    /// <summary>
    /// Resolves the material tint property used by URP and custom toon shaders.
    /// </summary>
    /// <param name="material">Material inspected for supported tint properties.</param>
    /// <param name="color">Resolved tint when a supported property exists.</param>
    /// <returns>True when a supported tint property exists.</returns>
    private static bool TryResolveMaterialTint(Material material, out Color color)
    {
        color = Color.white;

        if (material.HasProperty(BaseColorPropertyId))
        {
            color = material.GetColor(BaseColorPropertyId);
            return true;
        }

        if (material.HasProperty(ColorPropertyId))
        {
            color = material.GetColor(ColorPropertyId);
            return true;
        }

        if (material.HasProperty(TintColorPropertyId))
        {
            color = material.GetColor(TintColorPropertyId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the average visible color of a readable material base texture.
    /// </summary>
    /// <param name="material">Material inspected for supported texture properties.</param>
    /// <param name="color">Average texture color when a readable texture is available.</param>
    /// <returns>True when a readable texture was sampled.</returns>
    private static bool TryResolveMaterialTextureAverage(Material material, out Color color)
    {
        color = Color.white;
        Texture texture = ResolveMaterialTexture(material);
        Texture2D texture2D = texture as Texture2D;

        if (texture2D == null || !texture2D.isReadable)
            return false;

        return TryAverageReadableTexture(texture2D, out color);
    }

    /// <summary>
    /// Resolves the first supported base texture property from a material.
    /// </summary>
    /// <param name="material">Material inspected for supported texture properties.</param>
    /// <returns>Texture assigned to the material, or null when none exists.</returns>
    private static Texture ResolveMaterialTexture(Material material)
    {
        if (material.HasProperty(BaseMapPropertyId))
            return material.GetTexture(BaseMapPropertyId);

        if (material.HasProperty(MainTexturePropertyId))
            return material.GetTexture(MainTexturePropertyId);

        return null;
    }

    /// <summary>
    /// Samples a readable texture sparsely and averages non-transparent pixels.
    /// </summary>
    /// <param name="texture">Readable texture to sample.</param>
    /// <param name="color">Average color of sampled non-transparent pixels.</param>
    /// <returns>True when at least one visible pixel was sampled.</returns>
    private static bool TryAverageReadableTexture(Texture2D texture, out Color color)
    {
        color = Color.white;

        try
        {
            Color32[] pixels = texture.GetPixels32();

            if (pixels == null || pixels.Length <= 0)
                return false;

            int stride = math.max(1, (int)math.sqrt(math.max(1, pixels.Length / MaximumTextureSamples)));
            float4 accumulatedColor = float4.zero;
            int sampledPixelCount = 0;

            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex += stride)
            {
                Color32 pixel = pixels[pixelIndex];

                if (pixel.a <= 8)
                    continue;

                accumulatedColor += new float4(pixel.r / 255f, pixel.g / 255f, pixel.b / 255f, pixel.a / 255f);
                sampledPixelCount++;
            }

            if (sampledPixelCount <= 0)
                return false;

            float4 averageColor = accumulatedColor / sampledPixelCount;
            color = new Color(averageColor.x, averageColor.y, averageColor.z, averageColor.w);
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }
    #endregion

    #region Color Helpers
    /// <summary>
    /// Multiplies two managed colors component-wise.
    /// </summary>
    /// <param name="left">First color.</param>
    /// <param name="right">Second color.</param>
    /// <returns>Component-wise color product.</returns>
    private static Color Multiply(Color left, Color right)
    {
        return new Color(left.r * right.r,
                         left.g * right.g,
                         left.b * right.b,
                         left.a * right.a);
    }

    /// <summary>
    /// Converts a managed Color to a math float4 without color-space conversion.
    /// </summary>
    /// <param name="color">Managed color value.</param>
    /// <returns>Float4 color with matching components.</returns>
    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    /// <summary>
    /// Computes perceptual distance between two RGB colors for palette deduplication.
    /// </summary>
    /// <param name="left">First color.</param>
    /// <param name="right">Second color.</param>
    /// <returns>Euclidean RGB distance.</returns>
    private static float ColorDistance(float4 left, float4 right)
    {
        return math.distance(new float3(left.x, left.y, left.z), new float3(right.x, right.y, right.z));
    }

    /// <summary>
    /// Performs ordinal case-insensitive containment without allocating normalized strings.
    /// </summary>
    /// <param name="value">Source string to inspect.</param>
    /// <param name="search">Search fragment.</param>
    /// <returns>True when the fragment appears in the source string.</returns>
    private static bool ContainsOrdinalIgnoreCase(string value, string search)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(search))
            return false;

        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #endregion
}
