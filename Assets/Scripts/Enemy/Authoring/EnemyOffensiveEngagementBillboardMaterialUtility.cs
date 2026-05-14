using UnityEngine;

/// <summary>
/// Resolves a build-safe sprite material for offensive engagement billboard views whose authored material asset is missing.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyOffensiveEngagementBillboardMaterialUtility
{
    #region Constants
    private const string UrpSpriteUnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
    private const string LegacySpritesDefaultShaderName = "Sprites/Default";
    private static readonly int SurfacePropertyId = Shader.PropertyToID("_Surface");
    private static readonly int BlendPropertyId = Shader.PropertyToID("_Blend");
    private static readonly int AlphaClipPropertyId = Shader.PropertyToID("_AlphaClip");
    private static readonly int SrcBlendPropertyId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendPropertyId = Shader.PropertyToID("_DstBlend");
    private static readonly int CullPropertyId = Shader.PropertyToID("_Cull");
    private static readonly int ZWritePropertyId = Shader.PropertyToID("_ZWrite");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    #endregion

    #region Fields
    private static Material sharedRuntimeSpriteMaterial;
    private static bool missingSpriteShaderWarningIssued;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns a build-safe sprite material when the authored prefab references a missing material asset.
    /// /params spriteRenderer Renderer that owns the engagement billboard sprite.
    /// /returns None.
    /// </summary>
    public static void EnsureSpriteRendererMaterial(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
            return;

        Material currentMaterial = spriteRenderer.sharedMaterial;

        if (IsUsableSpriteMaterial(currentMaterial))
            return;

        Material runtimeMaterial = ResolveSharedRuntimeSpriteMaterial();

        if (runtimeMaterial != null)
        {
            spriteRenderer.sharedMaterial = runtimeMaterial;
            return;
        }

        spriteRenderer.sharedMaterial = null;
    }

    /// <summary>
    /// Releases the shared runtime material used to replace missing billboard prefab material references.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void DestroySharedRuntimeMaterial()
    {
        if (sharedRuntimeSpriteMaterial == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(sharedRuntimeSpriteMaterial);
        else
            Object.DestroyImmediate(sharedRuntimeSpriteMaterial);

        sharedRuntimeSpriteMaterial = null;
        missingSpriteShaderWarningIssued = false;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether a sprite renderer material can render in the current pipeline.
    /// /params material Material currently assigned to the sprite renderer.
    /// /returns True when the material and shader are valid and supported.
    /// </summary>
    private static bool IsUsableSpriteMaterial(Material material)
    {
        if (material == null)
            return false;

        Shader shader = material.shader;
        return shader != null && shader.isSupported;
    }

    /// <summary>
    /// Resolves or creates the shared runtime material used by engagement billboards with missing prefab materials.
    /// /params None.
    /// /returns Runtime sprite material, or null when no supported shader is available.
    /// </summary>
    private static Material ResolveSharedRuntimeSpriteMaterial()
    {
        if (sharedRuntimeSpriteMaterial != null)
            return sharedRuntimeSpriteMaterial;

        Shader spriteShader = ResolveSpriteShader();

        if (spriteShader == null)
        {
            LogMissingSpriteShaderWarning();
            return null;
        }

        sharedRuntimeSpriteMaterial = new Material(spriteShader);
        ConfigureRuntimeSpriteMaterial(sharedRuntimeSpriteMaterial);
        return sharedRuntimeSpriteMaterial;
    }

    /// <summary>
    /// Resolves the first supported shader that can render sprite billboard textures in player builds.
    /// /params None.
    /// /returns Supported sprite shader, or null when none can be found.
    /// </summary>
    private static Shader ResolveSpriteShader()
    {
        Shader spriteShader = Shader.Find(UrpSpriteUnlitShaderName);

        if (spriteShader != null && spriteShader.isSupported)
            return spriteShader;

        spriteShader = Shader.Find(UrpUnlitShaderName);

        if (spriteShader != null && spriteShader.isSupported)
            return spriteShader;

        spriteShader = Shader.Find(LegacySpritesDefaultShaderName);

        if (spriteShader != null && spriteShader.isSupported)
            return spriteShader;

        return null;
    }

    /// <summary>
    /// Configures the generated sprite material for transparent billboard rendering.
    /// /params material Runtime material created for engagement billboards.
    /// /returns None.
    /// </summary>
    private static void ConfigureRuntimeSpriteMaterial(Material material)
    {
        material.hideFlags = HideFlags.HideAndDontSave;
        material.name = "EnemyOffensiveEngagementBillboard_Runtime";
        SetFloatIfPresent(material, SurfacePropertyId, 1f);
        SetFloatIfPresent(material, BlendPropertyId, 0f);
        SetFloatIfPresent(material, AlphaClipPropertyId, 0f);
        SetFloatIfPresent(material, SrcBlendPropertyId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfPresent(material, DstBlendPropertyId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, CullPropertyId, (float)UnityEngine.Rendering.CullMode.Off);
        SetFloatIfPresent(material, ZWritePropertyId, 0f);
        SetColorIfPresent(material, BaseColorPropertyId, Color.white);
        SetColorIfPresent(material, ColorPropertyId, Color.white);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 40;
    }

    /// <summary>
    /// Sets one float property when the active shader exposes it.
    /// /params material Runtime material being configured.
    /// /params propertyId Shader property id.
    /// /params value Float value to assign.
    /// /returns None.
    /// </summary>
    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (!material.HasProperty(propertyId))
            return;

        material.SetFloat(propertyId, value);
    }

    /// <summary>
    /// Sets one color property when the active shader exposes it.
    /// /params material Runtime material being configured.
    /// /params propertyId Shader property id.
    /// /params value Color value to assign.
    /// /returns None.
    /// </summary>
    private static void SetColorIfPresent(Material material, int propertyId, Color value)
    {
        if (!material.HasProperty(propertyId))
            return;

        material.SetColor(propertyId, value);
    }

    /// <summary>
    /// Logs one warning when no supported shader can render offensive engagement billboards.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void LogMissingSpriteShaderWarning()
    {
        if (missingSpriteShaderWarningIssued)
            return;

        missingSpriteShaderWarningIssued = true;
        Debug.LogWarning("[EnemyOffensiveEngagementBillboardMaterialUtility] No supported sprite shader was found. Offensive engagement billboards will stay hidden until a URP Sprite-Unlit, URP Unlit, or Sprites/Default shader is available.");
    }
    #endregion

    #endregion
}
