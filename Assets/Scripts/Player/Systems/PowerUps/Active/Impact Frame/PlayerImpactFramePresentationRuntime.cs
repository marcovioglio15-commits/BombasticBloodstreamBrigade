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
                                                 float radialDistortion)
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

        ConfigureMaterial();
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
    private static void ConfigureMaterial()
    {
        float blend = math.saturate(snapshot.Blend);
        float overlayIntensity = math.saturate(snapshot.OverlayIntensity);
        float4 tint = math.saturate(snapshot.FilterTintRgba);
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
