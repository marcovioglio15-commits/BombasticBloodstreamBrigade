using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates runtime materials for Elemental Trail ribbons and preserves authored texture/color data when possible.
/// </summary>
internal static class PlayerElementalTrailRibbonMaterialUtility
{
    #region Constants
    private const string ElementalTrailShaderName = "NashCore/VFX/Elemental Trail Ribbon Unlit";
    private const int UnderGameplayActorsRenderQueue = 1990;
    private const float AlwaysDepthTest = (float)CompareFunction.Always;
    private static readonly int AlphaPropertyId = Shader.PropertyToID("_Alpha");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int CullPropertyId = Shader.PropertyToID("_Cull");
    private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int ZTestPropertyId = Shader.PropertyToID("_ZTest");
    private static readonly int ZWritePropertyId = Shader.PropertyToID("_ZWrite");
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates an independent transparent material configured to remain visible across the preserved environment depth pass.
    /// </summary>
    /// <param name="sourceMaterial">Authored template material read from the TrailRenderer prefab.</param>
    /// <returns>Runtime material instance, or null when no source material is available.</returns>
    public static Material CreateRuntimeMaterial(Material sourceMaterial)
    {
        if (sourceMaterial == null)
            return null;

        // Prefer the dedicated shader so preserved environment depth cannot clip the ground ribbon.
        Shader elementalTrailShader = Shader.Find(ElementalTrailShaderName);
        Material material = elementalTrailShader != null ? new Material(elementalTrailShader) : new Material(sourceMaterial);
        material.name = string.Format("{0}_ElementalTrailRuntime", sourceMaterial.name);

        // Preserve authored visual data while normalizing the render state for the gameplay overlay.
        CopyTexture(sourceMaterial, material);
        CopyBaseColor(sourceMaterial, material);
        ConfigureRenderState(material);
        return material;
    }
    #endregion

    #region Copy
    /// <summary>
    /// Copies the authored trail texture from either URP base-map or legacy main-texture slots.
    /// </summary>
    /// <param name="sourceMaterial">Authored material that may hold the texture reference.</param>
    /// <param name="targetMaterial">Runtime material receiving the texture.</param>
    private static void CopyTexture(Material sourceMaterial, Material targetMaterial)
    {
        // Read from the common URP and legacy slots because older particle materials store the mask in _MainTex.
        Texture sourceTexture = null;
        int sourceTexturePropertyId = BaseMapPropertyId;

        if (sourceMaterial.HasProperty(BaseMapPropertyId))
            sourceTexture = sourceMaterial.GetTexture(BaseMapPropertyId);

        if (sourceTexture == null && sourceMaterial.HasProperty(MainTexturePropertyId))
        {
            sourceTexture = sourceMaterial.GetTexture(MainTexturePropertyId);
            sourceTexturePropertyId = MainTexturePropertyId;
        }

        if (sourceTexture == null || !targetMaterial.HasProperty(BaseMapPropertyId))
            return;

        // Apply texture data to the dedicated runtime shader slot.
        targetMaterial.SetTexture(BaseMapPropertyId, sourceTexture);
        targetMaterial.SetTextureScale(BaseMapPropertyId, sourceMaterial.GetTextureScale(sourceTexturePropertyId));
        targetMaterial.SetTextureOffset(BaseMapPropertyId, sourceMaterial.GetTextureOffset(sourceTexturePropertyId));
    }

    /// <summary>
    /// Copies the authored tint into the runtime shader when both materials expose a base color.
    /// </summary>
    /// <param name="sourceMaterial">Authored material that may hold a base color.</param>
    /// <param name="targetMaterial">Runtime material receiving the tint.</param>
    private static void CopyBaseColor(Material sourceMaterial, Material targetMaterial)
    {
        if (!sourceMaterial.HasProperty(BaseColorPropertyId) || !targetMaterial.HasProperty(BaseColorPropertyId))
            return;

        // Keep the authored tint separate from the per-point gradient baked into vertex colors.
        targetMaterial.SetColor(BaseColorPropertyId, sourceMaterial.GetColor(BaseColorPropertyId));
    }
    #endregion

    #region Render State
    /// <summary>
    /// Applies the render state required by a flat ground ribbon inside the gameplay overlay camera.
    /// </summary>
    /// <param name="material">Runtime material being configured.</param>
    private static void ConfigureRenderState(Material material)
    {
        material.renderQueue = UnderGameplayActorsRenderQueue;

        // Draw before gameplay opaque actors so player/enemies visually sit above the ground ribbon.
        SetFloatIfPresent(material, AlphaPropertyId, 1f);
        SetFloatIfPresent(material, CullPropertyId, (float)CullMode.Off);
        SetFloatIfPresent(material, ZTestPropertyId, AlwaysDepthTest);
        SetFloatIfPresent(material, ZWritePropertyId, 0f);
    }

    /// <summary>
    /// Writes a float shader property only when the material exposes it.
    /// </summary>
    /// <param name="material">Runtime material instance being configured.</param>
    /// <param name="propertyId">Shader property identifier.</param>
    /// <param name="value">Float value to assign.</param>
    private static void SetFloatIfPresent(Material material,
                                          int propertyId,
                                          float value)
    {
        if (material.HasProperty(propertyId))
            material.SetFloat(propertyId, value);
    }
    #endregion

    #endregion
}
