using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Immutable screen-filter snapshot selected by the Impact Frame update system for the current rendered frame.
/// </summary>
public readonly struct PlayerImpactFramePresentationSnapshot
{
    #region Fields
    public readonly float Blend;
    public readonly float OverlayIntensity;
    public readonly float4 FilterTintRgba;
    public readonly float DesaturationAmount;
    public readonly float VignetteIntensity;
    public readonly float VignetteSoftness;
    public readonly float ChromaticAberration;
    public readonly float ScanlineIntensity;
    public readonly float ScanlineFrequency;
    public readonly float FlashIntensity;
    public readonly float RadialDistortion;
    public readonly float ShockwaveIntensity;
    public readonly float ShockwaveRadius;
    public readonly float ShockwaveThickness;
    public readonly float ZoomPunchIntensity;
    public readonly float InvertIntensity;
    public readonly float PosterizeIntensity;
    public readonly float PosterizeSteps;
    public readonly float EdgeInkIntensity;
    public readonly float ScreenTearIntensity;
    public readonly float ScreenTearFrequency;
    public readonly float PaletteFlashIntensity;
    public readonly float4 PaletteFlashTintRgba;
    public readonly float LifetimeProgress;
    public readonly float3 EffectOriginWorldPosition;
    public readonly byte HasWorldOrigin;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one presentation snapshot from the active ECS Impact Frame state.
    /// </summary>
    /// <param name="blend">Current normalized effect blend.</param>
    /// <param name="overlayIntensity">Master overlay intensity.</param>
    /// <param name="filterTintRgba">Tint color and tint alpha.</param>
    /// <param name="desaturationAmount">Screen desaturation amount.</param>
    /// <param name="vignetteIntensity">Vignette intensity.</param>
    /// <param name="vignetteSoftness">Vignette softness.</param>
    /// <param name="chromaticAberration">Chromatic aberration offset.</param>
    /// <param name="scanlineIntensity">Scanline opacity.</param>
    /// <param name="scanlineFrequency">Scanline frequency in vertical lines.</param>
    /// <param name="flashIntensity">White flash additive intensity.</param>
    /// <param name="radialDistortion">Radial screen distortion strength.</param>
    /// <param name="shockwaveIntensity">Expanding shockwave ring intensity.</param>
    /// <param name="shockwaveRadius">Maximum normalized shockwave radius.</param>
    /// <param name="shockwaveThickness">Normalized shockwave ring thickness.</param>
    /// <param name="zoomPunchIntensity">Radial zoom punch intensity.</param>
    /// <param name="invertIntensity">Color inversion intensity.</param>
    /// <param name="posterizeIntensity">Posterization blend intensity.</param>
    /// <param name="posterizeSteps">Posterization color-step count.</param>
    /// <param name="edgeInkIntensity">Local edge darkening intensity.</param>
    /// <param name="screenTearIntensity">Horizontal tear intensity.</param>
    /// <param name="screenTearFrequency">Horizontal tear frequency.</param>
    /// <param name="paletteFlashIntensity">Palette flash blend intensity.</param>
    /// <param name="paletteFlashTintRgba">Palette flash tint and alpha.</param>
    /// <param name="lifetimeProgress">Normalized progress across the whole visible effect.</param>
    /// <param name="effectOriginWorldPosition">World position used by spatial effects.</param>
    /// <param name="hasWorldOrigin">One when effectOriginWorldPosition should be projected by the camera.</param>
    public PlayerImpactFramePresentationSnapshot(float blend,
                                                 float overlayIntensity,
                                                 float4 filterTintRgba,
                                                 float desaturationAmount,
                                                 float vignetteIntensity,
                                                 float vignetteSoftness,
                                                 float chromaticAberration,
                                                 float scanlineIntensity,
                                                 float scanlineFrequency,
                                                 float flashIntensity,
                                                 float radialDistortion,
                                                 float shockwaveIntensity,
                                                 float shockwaveRadius,
                                                 float shockwaveThickness,
                                                 float zoomPunchIntensity,
                                                 float invertIntensity,
                                                 float posterizeIntensity,
                                                 float posterizeSteps,
                                                 float edgeInkIntensity,
                                                 float screenTearIntensity,
                                                 float screenTearFrequency,
                                                 float paletteFlashIntensity,
                                                 float4 paletteFlashTintRgba,
                                                 float lifetimeProgress,
                                                 float3 effectOriginWorldPosition,
                                                 byte hasWorldOrigin)
    {
        Blend = blend;
        OverlayIntensity = overlayIntensity;
        FilterTintRgba = filterTintRgba;
        DesaturationAmount = desaturationAmount;
        VignetteIntensity = vignetteIntensity;
        VignetteSoftness = vignetteSoftness;
        ChromaticAberration = chromaticAberration;
        ScanlineIntensity = scanlineIntensity;
        ScanlineFrequency = scanlineFrequency;
        FlashIntensity = flashIntensity;
        RadialDistortion = radialDistortion;
        ShockwaveIntensity = shockwaveIntensity;
        ShockwaveRadius = shockwaveRadius;
        ShockwaveThickness = shockwaveThickness;
        ZoomPunchIntensity = zoomPunchIntensity;
        InvertIntensity = invertIntensity;
        PosterizeIntensity = posterizeIntensity;
        PosterizeSteps = posterizeSteps;
        EdgeInkIntensity = edgeInkIntensity;
        ScreenTearIntensity = screenTearIntensity;
        ScreenTearFrequency = screenTearFrequency;
        PaletteFlashIntensity = paletteFlashIntensity;
        PaletteFlashTintRgba = paletteFlashTintRgba;
        LifetimeProgress = lifetimeProgress;
        EffectOriginWorldPosition = effectOriginWorldPosition;
        HasWorldOrigin = hasWorldOrigin;
    }
    #endregion
}

/// <summary>
/// Stores the active Impact Frame presentation snapshot and configures the fullscreen material consumed by URP.
/// </summary>
internal static class PlayerImpactFramePresentationRuntime
{
    #region Constants
    private const string ShaderName = "Hidden/NashCore/PlayerImpactFrame";
    private const float ComparisonEpsilon = 0.0001f;
    #endregion

    #region Fields
    private static readonly int blendId = Shader.PropertyToID("_ImpactBlend");
    private static readonly int overlayIntensityId = Shader.PropertyToID("_OverlayIntensity");
    private static readonly int filterTintId = Shader.PropertyToID("_FilterTint");
    private static readonly int desaturationAmountId = Shader.PropertyToID("_DesaturationAmount");
    private static readonly int vignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
    private static readonly int vignetteSoftnessId = Shader.PropertyToID("_VignetteSoftness");
    private static readonly int chromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int scanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
    private static readonly int scanlineFrequencyId = Shader.PropertyToID("_ScanlineFrequency");
    private static readonly int flashIntensityId = Shader.PropertyToID("_FlashIntensity");
    private static readonly int radialDistortionId = Shader.PropertyToID("_RadialDistortion");
    private static readonly int shockwaveIntensityId = Shader.PropertyToID("_ShockwaveIntensity");
    private static readonly int shockwaveRadiusId = Shader.PropertyToID("_ShockwaveRadius");
    private static readonly int shockwaveThicknessId = Shader.PropertyToID("_ShockwaveThickness");
    private static readonly int zoomPunchIntensityId = Shader.PropertyToID("_ZoomPunchIntensity");
    private static readonly int invertIntensityId = Shader.PropertyToID("_InvertIntensity");
    private static readonly int posterizeIntensityId = Shader.PropertyToID("_PosterizeIntensity");
    private static readonly int posterizeStepsId = Shader.PropertyToID("_PosterizeSteps");
    private static readonly int edgeInkIntensityId = Shader.PropertyToID("_EdgeInkIntensity");
    private static readonly int screenTearIntensityId = Shader.PropertyToID("_ScreenTearIntensity");
    private static readonly int screenTearFrequencyId = Shader.PropertyToID("_ScreenTearFrequency");
    private static readonly int paletteFlashIntensityId = Shader.PropertyToID("_PaletteFlashIntensity");
    private static readonly int paletteFlashTintId = Shader.PropertyToID("_PaletteFlashTint");
    private static readonly int lifetimeProgressId = Shader.PropertyToID("_LifetimeProgress");
    private static readonly int effectCenterId = Shader.PropertyToID("_EffectCenter");
    private static Material material;
    private static PlayerImpactFramePresentationSnapshot snapshot;
    private static bool isActive;
    private static bool shaderWarningIssued;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Publishes the current Impact Frame presentation snapshot for the URP renderer feature.
    /// </summary>
    /// <param name="newSnapshot">Snapshot selected by the Impact Frame update system.</param>
    public static void SetSnapshot(in PlayerImpactFramePresentationSnapshot newSnapshot)
    {
        snapshot = newSnapshot;
        isActive = snapshot.Blend * snapshot.OverlayIntensity > ComparisonEpsilon;
    }

    /// <summary>
    /// Clears the current presentation snapshot while keeping the material ready for later activations.
    /// </summary>
    public static void ClearSnapshot()
    {
        isActive = false;
    }

    /// <summary>
    /// Resolves and configures the material used by the Impact Frame URP renderer pass.
    /// </summary>
    /// <param name="camera">Camera currently evaluated by URP.</param>
    /// <param name="impactFrameShader">Renderer feature shader reference used to keep the shader included in builds.</param>
    /// <param name="configuredMaterial">Configured material instance when the pass should render.</param>
    /// <returns>True when the material is ready and the camera should receive the effect.</returns>
    public static bool TryConfigureMaterialForCamera(Camera camera, Shader impactFrameShader, out Material configuredMaterial)
    {
        configuredMaterial = null;

        if (!ShouldRenderForCamera(camera))
            return false;

        if (!EnsureMaterial(impactFrameShader))
            return false;

        ConfigureMaterial(camera);
        configuredMaterial = material;
        return true;
    }

    /// <summary>
    /// Resets static state when Unity reloads the runtime domain.
    /// </summary>
    public static void Reset()
    {
        isActive = false;
        shaderWarningIssued = false;
        snapshot = default;

        if (material != null)
            Object.Destroy(material);

        material = null;
    }
    #endregion

    #region Camera Filtering
    /// <summary>
    /// Resolves whether the current camera should receive the Impact Frame filter.
    /// </summary>
    /// <param name="camera">Camera being evaluated by the URP renderer pass.</param>
    /// <returns>True when the camera is an active game camera with a valid target size.</returns>
    private static bool ShouldRenderForCamera(Camera camera)
    {
        if (!Application.isPlaying)
            return false;

        if (!isActive)
            return false;

        if (camera == null)
            return false;

        if (camera.cameraType != CameraType.Game)
            return false;

        if (!camera.isActiveAndEnabled)
            return false;

        if (camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            return false;

        return true;
    }
    #endregion

    #region Material
    /// <summary>
    /// Lazily creates the material used by the fullscreen Impact Frame pass.
    /// </summary>
    /// <param name="impactFrameShader">Renderer feature shader reference used before falling back to Shader.Find.</param>
    /// <returns>True when the material is available.</returns>
    private static bool EnsureMaterial(Shader impactFrameShader)
    {
        if (material != null && impactFrameShader == null)
            return true;

        if (material != null && material.shader == impactFrameShader)
            return true;

        if (material != null)
        {
            Object.Destroy(material);
            material = null;
        }

        Shader shader = impactFrameShader != null ? impactFrameShader : Shader.Find(ShaderName);

        if (shader == null)
        {
            if (!shaderWarningIssued)
            {
                Debug.LogWarning("Impact Frame shader could not be found. Fullscreen Impact Frame presentation is disabled.");
                shaderWarningIssued = true;
            }

            return false;
        }

        material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return true;
    }

    /// <summary>
    /// Writes the current snapshot values into the fullscreen material.
    /// </summary>
    private static void ConfigureMaterial(Camera camera)
    {
        float blend = math.saturate(snapshot.Blend);
        float overlayIntensity = math.saturate(snapshot.OverlayIntensity);
        float4 tint = math.saturate(snapshot.FilterTintRgba);
        float4 paletteTint = math.saturate(snapshot.PaletteFlashTintRgba);
        Vector2 effectCenter = ResolveEffectCenter(camera);
        material.SetFloat(blendId, blend);
        material.SetFloat(overlayIntensityId, overlayIntensity);
        material.SetVector(filterTintId, new Vector4(tint.x, tint.y, tint.z, tint.w));
        material.SetFloat(desaturationAmountId, math.saturate(snapshot.DesaturationAmount));
        material.SetFloat(vignetteIntensityId, math.saturate(snapshot.VignetteIntensity));
        material.SetFloat(vignetteSoftnessId, math.saturate(snapshot.VignetteSoftness));
        material.SetFloat(chromaticAberrationId, math.max(0f, snapshot.ChromaticAberration));
        material.SetFloat(scanlineIntensityId, math.saturate(snapshot.ScanlineIntensity));
        material.SetFloat(scanlineFrequencyId, math.max(0f, snapshot.ScanlineFrequency));
        material.SetFloat(flashIntensityId, math.saturate(snapshot.FlashIntensity));
        material.SetFloat(radialDistortionId, math.saturate(snapshot.RadialDistortion));
        material.SetFloat(shockwaveIntensityId, math.saturate(snapshot.ShockwaveIntensity));
        material.SetFloat(shockwaveRadiusId, math.saturate(snapshot.ShockwaveRadius));
        material.SetFloat(shockwaveThicknessId, math.clamp(snapshot.ShockwaveThickness, 0.001f, 1f));
        material.SetFloat(zoomPunchIntensityId, math.saturate(snapshot.ZoomPunchIntensity));
        material.SetFloat(invertIntensityId, math.saturate(snapshot.InvertIntensity));
        material.SetFloat(posterizeIntensityId, math.saturate(snapshot.PosterizeIntensity));
        material.SetFloat(posterizeStepsId, math.max(2f, snapshot.PosterizeSteps));
        material.SetFloat(edgeInkIntensityId, math.saturate(snapshot.EdgeInkIntensity));
        material.SetFloat(screenTearIntensityId, math.saturate(snapshot.ScreenTearIntensity));
        material.SetFloat(screenTearFrequencyId, math.max(0f, snapshot.ScreenTearFrequency));
        material.SetFloat(paletteFlashIntensityId, math.saturate(snapshot.PaletteFlashIntensity));
        material.SetVector(paletteFlashTintId, new Vector4(paletteTint.x, paletteTint.y, paletteTint.z, paletteTint.w));
        material.SetFloat(lifetimeProgressId, math.saturate(snapshot.LifetimeProgress));
        material.SetVector(effectCenterId, effectCenter);
    }

    /// <summary>
    /// Resolves the screen-space origin used by shockwave and zoom effects.
    /// </summary>
    /// <param name="camera">Camera currently rendering the fullscreen pass.</param>
    /// <returns>Viewport-space origin clamped to the visible screen.</returns>
    private static Vector2 ResolveEffectCenter(Camera camera)
    {
        if (snapshot.HasWorldOrigin == 0 || camera == null)
            return new Vector2(0.5f, 0.5f);

        Vector3 viewportPosition = camera.WorldToViewportPoint(new Vector3(snapshot.EffectOriginWorldPosition.x,
                                                                           snapshot.EffectOriginWorldPosition.y,
                                                                           snapshot.EffectOriginWorldPosition.z));

        if (viewportPosition.z <= 0f)
            return new Vector2(0.5f, 0.5f);

        return new Vector2(math.saturate(viewportPosition.x), math.saturate(viewportPosition.y));
    }
    #endregion

    #region Runtime Reset
    /// <summary>
    /// Clears static render and time-scale state before a new play-mode runtime starts.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnRuntimeLoad()
    {
        Reset();
        PlayerImpactFrameTimeScaleUtility.Reset();
    }
    #endregion

    #endregion
}
