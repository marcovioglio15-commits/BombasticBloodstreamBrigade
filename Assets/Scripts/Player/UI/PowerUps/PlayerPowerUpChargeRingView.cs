using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one preauthored procedural charge semiring from ECS-authoritative active power-up charge values.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerPowerUpChargeRingView : MonoBehaviour
{
    #region Shader Properties
    private static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int FillNormalizedId = Shader.PropertyToID("_FillNormalized");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int StartAngleDegreesId = Shader.PropertyToID("_StartAngleDegrees");
    private static readonly int ArcDegreesId = Shader.PropertyToID("_ArcDegrees");
    private static readonly int FillDirectionId = Shader.PropertyToID("_FillDirection");
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Single-quad UGUI graphic rendered by the procedural active power-up charge semiring shader.")]
    [SerializeField] private PlayerPowerUpChargeRingGraphic graphic;

    [Tooltip("Shared charge semiring material template cloned once for this view.")]
    [SerializeField] private Material materialTemplate;
    #endregion

    private Material runtimeMaterial;
    private bool configured;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the persistent material instance required by this preauthored charge ring.
    /// </summary>
    public void Initialize()
    {
        if (runtimeMaterial != null || graphic == null)
            return;

        Material sourceMaterial = materialTemplate;

        if (sourceMaterial == null)
        {
            Shader shader = Shader.Find("Custom/UI/PowerUpChargeSemiRing");

            if (shader == null)
                return;

            runtimeMaterial = new Material(shader);
        }
        else
        {
            runtimeMaterial = new Material(sourceMaterial);
        }

        runtimeMaterial.name = (sourceMaterial != null ? sourceMaterial.name : "M_UI_PowerUpChargeSemiRing") + " (Runtime " + name + ")";

        if (!Application.isPlaying)
            runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;

        graphic.material = runtimeMaterial;
    }

    /// <summary>
    /// Releases the persistent material instance owned by this view.
    /// </summary>
    public void Dispose()
    {
        if (runtimeMaterial == null)
            return;

        if (graphic != null && graphic.material == runtimeMaterial)
            graphic.material = materialTemplate;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies scalable charge-ring configuration without recreating UI objects.
    /// </summary>
    /// <param name="config">Charge-ring visual configuration resolved from ECS.</param>
    public void ApplyConfiguration(in PlayerPowerUpChargeRingVisualConfig config)
    {
        Initialize();
        configured = true;

        if (runtimeMaterial == null)
            return;

        SetColor(BackgroundColorId, config.BackgroundColor);
        SetColor(FillColorId, config.FillColor);
        SetColor(OutlineColorId, config.OutlineColor);
        runtimeMaterial.SetFloat(ThicknessId, math.clamp(config.Thickness, 0.02f, 0.6f));
        runtimeMaterial.SetFloat(OutlineThicknessId, math.clamp(config.OutlineThickness, 0f, 0.2f));
        runtimeMaterial.SetFloat(StartAngleDegreesId, math.clamp(config.StartAngleDegrees, -360f, 360f));
        runtimeMaterial.SetFloat(ArcDegreesId, math.clamp(config.ArcDegrees, 10f, 360f));
        runtimeMaterial.SetFloat(FillDirectionId, (float)ResolveFillDirection(config.FillDirection));
        SetVisible(config.Enabled != 0);
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Updates the visible semiring fill amount.
    /// </summary>
    /// <param name="normalizedValue">Charge progress normalized to the configured required or maximum charge.</param>
    public void UpdateValue(float normalizedValue)
    {
        if (!configured || runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(FillNormalizedId, math.saturate(normalizedValue));
    }

    /// <summary>
    /// Applies missing-module or missing-player visibility without destroying the view.
    /// </summary>
    /// <param name="hide">True when the view should be hidden.</param>
    public void HandleMissing(bool hide)
    {
        SetVisible(!hide && configured);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves unsupported runtime fill-direction enum values back to the authored default.
    /// </summary>
    /// <param name="fillDirection">Runtime fill direction that may have been changed by formulas.</param>
    /// <returns>Supported shader fill-direction index.</returns>
    private static PlayerPowerUpChargeRingFillDirection ResolveFillDirection(PlayerPowerUpChargeRingFillDirection fillDirection)
    {
        switch (fillDirection)
        {
            case PlayerPowerUpChargeRingFillDirection.TopToBottom:
            case PlayerPowerUpChargeRingFillDirection.BottomToTop:
                return fillDirection;
            default:
                return PlayerPowerUpChargeRingFillDirection.TopToBottom;
        }
    }

    /// <summary>
    /// Writes one unmanaged direct color to the runtime material.
    /// </summary>
    /// <param name="propertyId">Cached shader property identifier.</param>
    /// <param name="value">Unmanaged RGBA value.</param>
    private void SetColor(int propertyId, float4 value)
    {
        runtimeMaterial.SetColor(propertyId, new Color(value.x, value.y, value.z, value.w));
    }

    /// <summary>
    /// Changes view visibility only when the requested state differs.
    /// </summary>
    /// <param name="visible">Requested active state.</param>
    private void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible)
            return;

        gameObject.SetActive(visible);

        if (transform.parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }
    #endregion

    #endregion
}
