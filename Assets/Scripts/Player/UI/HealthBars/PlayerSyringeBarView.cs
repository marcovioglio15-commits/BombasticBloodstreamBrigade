using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one preauthored procedural syringe graphic and numeric label pool from ECS-authoritative values.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSyringeBarView : MonoBehaviour
{
    #region Constants
    private const float MaximumReactiveDeltaTime = 1f / 30f;
    private const float MaximumReactiveVelocityMultiplier = 8f;
    private const float ReferenceDecorationLength = 340f;
    #endregion

    #region Shader Properties
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int BodyColorId = Shader.PropertyToID("_BodyColor");
    private static readonly int BodyShadowColorId = Shader.PropertyToID("_BodyShadowColor");
    private static readonly int ChamberColorId = Shader.PropertyToID("_ChamberColor");
    private static readonly int LiquidColorId = Shader.PropertyToID("_LiquidColor");
    private static readonly int LiquidHighlightColorId = Shader.PropertyToID("_LiquidHighlightColor");
    private static readonly int BubbleColorId = Shader.PropertyToID("_BubbleColor");
    private static readonly int GraduationColorId = Shader.PropertyToID("_GraduationColor");
    private static readonly int PlungerColorId = Shader.PropertyToID("_PlungerColor");
    private static readonly int PlungerWindowColorId = Shader.PropertyToID("_PlungerWindowColor");
    private static readonly int TerminationOutlineColorId = Shader.PropertyToID("_TerminationOutlineColor");
    private static readonly int TerminationInteriorColorId = Shader.PropertyToID("_TerminationInteriorColor");
    private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int OutlineAspectId = Shader.PropertyToID("_OutlineAspect");
    private static readonly int GraduationVerticalOffsetId = Shader.PropertyToID("_GraduationVerticalOffset");
    private static readonly int SloshAffectsBubblesOnlyId = Shader.PropertyToID("_SloshAffectsBubblesOnly");
    private static readonly int ChamberInsetId = Shader.PropertyToID("_ChamberInset");
    private static readonly int EndCapNormalizedId = Shader.PropertyToID("_EndCapNormalized");
    private static readonly int GraduationInsetNormalizedId = Shader.PropertyToID("_GraduationInsetNormalized");
    private static readonly int GraduationEndNormalizedId = Shader.PropertyToID("_GraduationEndNormalized");
    private static readonly int TerminationOffsetNormalizedId = Shader.PropertyToID("_TerminationOffsetNormalized");
    private static readonly int PlungerWidthId = Shader.PropertyToID("_PlungerWidth");
    private static readonly int BodyStyleId = Shader.PropertyToID("_BodyStyle");
    private static readonly int LabelPlacementId = Shader.PropertyToID("_LabelPlacement");
    private static readonly int TerminationStyleId = Shader.PropertyToID("_TerminationStyle");
    private static readonly int PaintDripsEnabledId = Shader.PropertyToID("_PaintDripsEnabled");
    private static readonly int PaintDripDensityId = Shader.PropertyToID("_PaintDripDensity");
    private static readonly int PaintDripLengthId = Shader.PropertyToID("_PaintDripLength");
    private static readonly int PaintDripWidthId = Shader.PropertyToID("_PaintDripWidth");
    private static readonly int PaintDripIrregularityId = Shader.PropertyToID("_PaintDripIrregularity");
    private static readonly int LengthPixelScaleId = Shader.PropertyToID("_LengthPixelScale");
    private static readonly int MajorDivisionCountId = Shader.PropertyToID("_MajorDivisionCount");
    private static readonly int MinorDivisionsPerMajorId = Shader.PropertyToID("_MinorDivisionsPerMajor");
    private static readonly int FillNormalizedId = Shader.PropertyToID("_FillNormalized");
    private static readonly int SloshId = Shader.PropertyToID("_Slosh");
    private static readonly int ValueImpulseId = Shader.PropertyToID("_ValueImpulse");
    private static readonly int SurfaceSloshStrengthId = Shader.PropertyToID("_SurfaceSloshStrength");
    private static readonly int HorizontalSloshEnabledId = Shader.PropertyToID("_HorizontalSloshEnabled");
    private static readonly int HorizontalSloshStrengthId = Shader.PropertyToID("_HorizontalSloshStrength");
    private static readonly int FlowEnabledId = Shader.PropertyToID("_FlowEnabled");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int WaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
    private static readonly int ViscosityId = Shader.PropertyToID("_Viscosity");
    private static readonly int BubblesEnabledId = Shader.PropertyToID("_BubblesEnabled");
    private static readonly int BubbleDensityId = Shader.PropertyToID("_BubbleDensity");
    private static readonly int BubbleMinimumSizeId = Shader.PropertyToID("_BubbleMinimumSize");
    private static readonly int BubbleMaximumSizeId = Shader.PropertyToID("_BubbleMaximumSize");
    private static readonly int BubbleRiseSpeedId = Shader.PropertyToID("_BubbleRiseSpeed");
    private static readonly int BubbleDriftId = Shader.PropertyToID("_BubbleDrift");
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("RectTransform rotated by optional player-movement tilt without affecting the parent layout.")]
    [SerializeField] private RectTransform motionRoot;

    [Tooltip("Single-quad UGUI graphic rendered by the procedural player syringe shader.")]
    [SerializeField] private PlayerSyringeBarGraphic graphic;

    [Tooltip("RectTransform spanning only the scalable chamber and hosting all preauthored labels.")]
    [SerializeField] private RectTransform labelsRoot;

    [Tooltip("Preauthored numeric graduation label pool.")]
    [SerializeField] private PlayerSyringeBarLabelPool labelPool;

    [Tooltip("Shared procedural syringe material template cloned once for this view.")]
    [SerializeField] private Material materialTemplate;
    #endregion

    private RectTransform root;
    private Material runtimeMaterial;
    private PlayerHealthBarVisualConfig sharedConfig;
    private PlayerSyringeChannelConfig channelConfig;
    private TMP_FontAsset activeFont;
    private float displayedNormalized;
    private float targetNormalized;
    private float previousTargetNormalized;
    private float previousMaximum = float.NaN;
    private float previousVelocityX;
    private float slosh;
    private float sloshVelocity;
    private float tilt;
    private float tiltVelocity;
    private float valueImpulse;
    private bool configured;
    private bool motionInitialized;
    #endregion

    #region Properties
    public RectTransform Root => root;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the persistent material instance required by this preauthored syringe view.
    /// </summary>
    public void Initialize()
    {
        if (root == null)
            root = transform as RectTransform;

        if (runtimeMaterial != null || graphic == null)
            return;

        Material sourceMaterial = materialTemplate;

        if (sourceMaterial == null)
        {
            Shader shader = Shader.Find("Custom/UI/PlayerSyringeBar");

            if (shader == null)
                return;

            runtimeMaterial = new Material(shader);
        }
        else
        {
            runtimeMaterial = new Material(sourceMaterial);
        }

        runtimeMaterial.name = (sourceMaterial != null ? sourceMaterial.name : "M_UI_PlayerSyringeBar") + " (Runtime " + name + ")";

        if (!Application.isPlaying)
            runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;

        graphic.material = runtimeMaterial;
    }

    /// <summary>
    /// Releases the persistent material instance owned by this view.
    /// </summary>
    public void Dispose()
    {
        if (labelPool != null)
            labelPool.DisposeRuntimeResources();

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

    /// <summary>
    /// Clears accumulated reactive motion after focus, pause, visibility, or timing discontinuities.
    /// </summary>
    public void ResetReactiveMotion()
    {
        previousVelocityX = 0f;
        slosh = 0f;
        sloshVelocity = 0f;
        tilt = 0f;
        tiltVelocity = 0f;
        valueImpulse = 0f;
        previousTargetNormalized = targetNormalized;
        motionInitialized = false;

        if (motionRoot != null)
            motionRoot.localRotation = Quaternion.identity;

        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(SloshId, 0f);
        runtimeMaterial.SetFloat(ValueImpulseId, 0f);
    }

    /// <summary>
    /// Clears reactive state whenever the preauthored view is disabled.
    /// </summary>
    private void OnDisable()
    {
        ResetReactiveMotion();
    }
    #endregion

    #region Configuration
    /// <summary>
    /// Applies a rebuilt ECS visual configuration without recreating UI objects.
    /// </summary>
    /// <param name="shared">Shared health-bar visual configuration.</param>
    /// <param name="channel">Health or shield channel configuration.</param>
    /// <param name="font">Direct font asset baked from the active visual preset.</param>
    public void ApplyConfiguration(in PlayerHealthBarVisualConfig shared,
                                   in PlayerSyringeChannelConfig channel,
                                   TMP_FontAsset font)
    {
        Initialize();
        sharedConfig = shared;
        channelConfig = channel;
        activeFont = font;
        configured = true;

        if (root != null)
            root.sizeDelta = new Vector2(root.sizeDelta.x, math.max(1f, sharedConfig.BarHeight));

        ApplyStaticMaterialProperties();
        RebuildLayout(previousMaximum);
        SetVisible(sharedConfig.Enabled != 0 && channelConfig.Enabled != 0);
        MarkParentLayoutDirty();
    }
    #endregion

    #region Runtime Updates
    /// <summary>
    /// Updates fill, dynamic length, labels, and optional movement reactions from authoritative runtime values.
    /// </summary>
    /// <param name="currentValue">Authoritative current health or shield value.</param>
    /// <param name="maximumValue">Authoritative maximum health or shield value.</param>
    /// <param name="velocityX">Current player world-X velocity used by optional inertial reactions.</param>
    /// <param name="snapImmediately">True when fill smoothing should be bypassed.</param>
    public void UpdateValue(float currentValue, float maximumValue, float velocityX, bool snapImmediately)
    {
        if (!configured || runtimeMaterial == null)
            return;

        bool shouldHide = sharedConfig.Enabled == 0 ||
                          channelConfig.Enabled == 0 ||
                          channelConfig.HideWhenMaximumUnavailable != 0 && maximumValue <= 0f;
        SetVisible(!shouldHide);

        if (shouldHide)
            return;

        if (!Mathf.Approximately(previousMaximum, maximumValue))
            RebuildLayout(maximumValue);

        targetNormalized = maximumValue > 0f ? math.saturate(currentValue / maximumValue) : 0f;
        float deltaTime = Time.unscaledDeltaTime;

        if (snapImmediately || channelConfig.SmoothingSeconds <= 0f)
            displayedNormalized = targetNormalized;
        else
            displayedNormalized = Mathf.MoveTowards(displayedNormalized,
                                                     targetNormalized,
                                                     deltaTime / math.max(0.0001f, channelConfig.SmoothingSeconds));

        UpdateValueImpulse(deltaTime);
        UpdateReactiveMotion(velocityX, deltaTime);
        runtimeMaterial.SetFloat(FillNormalizedId, displayedNormalized);
        runtimeMaterial.SetFloat(SloshId, slosh);
        runtimeMaterial.SetFloat(ValueImpulseId, valueImpulse);
        previousTargetNormalized = targetNormalized;
    }

    /// <summary>
    /// Applies the configured missing-player visibility behavior without destroying the view.
    /// </summary>
    /// <param name="hideWhenMissing">True when the complete view should be hidden.</param>
    public void HandleMissing(bool hideWhenMissing)
    {
        SetVisible(!hideWhenMissing && sharedConfig.Enabled != 0 && channelConfig.Enabled != 0);
    }
    #endregion

    #region Material
    /// <summary>
    /// Writes configuration properties that change only after bake or runtime scaling rebuilds.
    /// </summary>
    private void ApplyStaticMaterialProperties()
    {
        if (runtimeMaterial == null)
            return;

        SetColor(OutlineColorId, channelConfig.Palette.Outline);
        SetColor(BodyColorId, channelConfig.Palette.Body);
        SetColor(BodyShadowColorId, channelConfig.Palette.BodyShadow);
        SetColor(ChamberColorId, channelConfig.Palette.Chamber);
        SetColor(LiquidColorId, channelConfig.Palette.Liquid);
        SetColor(LiquidHighlightColorId, channelConfig.Palette.LiquidHighlight);
        SetColor(BubbleColorId, channelConfig.Palette.Bubbles);
        SetColor(GraduationColorId, channelConfig.Palette.Graduation);
        SetColor(PlungerColorId, channelConfig.Palette.Plunger);
        SetColor(PlungerWindowColorId, channelConfig.Palette.PlungerWindow);
        SetColor(TerminationOutlineColorId, channelConfig.Palette.TerminationOutline);
        SetColor(TerminationInteriorColorId, channelConfig.Palette.TerminationInterior);
        runtimeMaterial.SetFloat(OutlineThicknessId, math.max(0f, sharedConfig.OutlineThickness));
        runtimeMaterial.SetFloat(GraduationVerticalOffsetId, math.clamp(sharedConfig.GraduationVerticalOffset, -0.5f, 0.5f));
        runtimeMaterial.SetFloat(ChamberInsetId, math.clamp(sharedConfig.ChamberInset, 0f, 0.49f));
        runtimeMaterial.SetFloat(BodyStyleId, (float)sharedConfig.BodyStyle);
        runtimeMaterial.SetFloat(LabelPlacementId, (float)sharedConfig.LabelPlacement);
        runtimeMaterial.SetFloat(TerminationStyleId, (float)sharedConfig.TerminationStyle);
        runtimeMaterial.SetFloat(PaintDripsEnabledId, sharedConfig.PaintDrips.Enabled);
        runtimeMaterial.SetFloat(PaintDripDensityId, math.saturate(sharedConfig.PaintDrips.Density));
        runtimeMaterial.SetFloat(PaintDripLengthId, math.clamp(sharedConfig.PaintDrips.Length, 0f, 0.5f));
        runtimeMaterial.SetFloat(PaintDripIrregularityId, math.saturate(sharedConfig.PaintDrips.Irregularity));
        runtimeMaterial.SetFloat(MinorDivisionsPerMajorId, math.max(1, sharedConfig.MinorDivisionsPerMajor));
        runtimeMaterial.SetFloat(FlowEnabledId, channelConfig.Fluid.FlowEnabled);
        runtimeMaterial.SetFloat(FlowSpeedId, channelConfig.Fluid.FlowSpeed);
        runtimeMaterial.SetFloat(WaveAmplitudeId, math.max(0f, channelConfig.Fluid.WaveAmplitude));
        runtimeMaterial.SetFloat(WaveFrequencyId, math.max(0f, channelConfig.Fluid.WaveFrequency));
        runtimeMaterial.SetFloat(ViscosityId, math.saturate(channelConfig.Fluid.Viscosity));
        runtimeMaterial.SetFloat(BubblesEnabledId, channelConfig.Fluid.BubblesEnabled);
        runtimeMaterial.SetFloat(BubbleDensityId, math.saturate(channelConfig.Fluid.BubbleDensity));
        runtimeMaterial.SetFloat(BubbleMinimumSizeId, math.max(0f, channelConfig.Fluid.BubbleMinimumSize));
        runtimeMaterial.SetFloat(BubbleMaximumSizeId, math.max(0f, channelConfig.Fluid.BubbleMaximumSize));
        runtimeMaterial.SetFloat(BubbleRiseSpeedId, channelConfig.Fluid.BubbleRiseSpeed);
        runtimeMaterial.SetFloat(BubbleDriftId, channelConfig.Fluid.BubbleDrift);
        runtimeMaterial.SetFloat(SurfaceSloshStrengthId, math.max(0f, channelConfig.Motion.SurfaceSloshStrength));
        runtimeMaterial.SetFloat(HorizontalSloshEnabledId, channelConfig.Motion.HorizontalSloshEnabled);
        runtimeMaterial.SetFloat(HorizontalSloshStrengthId, math.max(0f, channelConfig.Motion.HorizontalSloshStrength));
        runtimeMaterial.SetFloat(SloshAffectsBubblesOnlyId, channelConfig.SloshAffectsBubblesOnly);
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
    #endregion

    #region Layout
    /// <summary>
    /// Rebuilds dynamic syringe length, shader graduations, and numeric labels after maximum-value changes.
    /// </summary>
    /// <param name="maximumValue">Authoritative maximum represented by this syringe.</param>
    private void RebuildLayout(float maximumValue)
    {
        previousMaximum = maximumValue;

        if (root == null || runtimeMaterial == null || !math.isfinite(maximumValue))
            return;

        float safeUnits = math.max(0.0001f, sharedConfig.UnitsPerMajorDivision);
        float intervalCount = math.max(0f, maximumValue / safeUnits);
        float minimumLength = math.max(1f, sharedConfig.MinimumLength);
        float maximumLength = math.max(minimumLength, sharedConfig.MaximumLength);
        float graduationStartInset = math.max(0f, sharedConfig.EndCapWidth) +
                                     math.max(0f, sharedConfig.GraduationEndPadding);
        float graduationEndInset = math.max(0f, sharedConfig.EndCapWidth);
        float terminationOffset = 0f;

        if (sharedConfig.BodyStyle == PlayerSyringeBodyStyle.SimplePaintedContainer)
        {
            graduationStartInset = math.max(0f, sharedConfig.EndCapWidth) * 0.5f +
                                   math.max(0f, sharedConfig.GraduationEndPadding);
            terminationOffset = math.max(0f, sharedConfig.TerminationOffset);
            graduationEndInset += terminationOffset;
        }

        float targetLength = graduationStartInset +
                             intervalCount * math.max(0.0001f, sharedConfig.PixelsPerMajorDivision);
        targetLength += graduationEndInset;
        float resolvedLength = math.clamp(targetLength, minimumLength, maximumLength);
        float resolvedGraduationStartInset = math.min(graduationStartInset, resolvedLength * 0.45f);
        float resolvedGraduationEndInset = math.min(graduationEndInset, resolvedLength * 0.45f);
        float graduationPixelWidth = math.max(1f,
                                              resolvedLength -
                                              resolvedGraduationStartInset -
                                              resolvedGraduationEndInset);
        root.sizeDelta = new Vector2(resolvedLength, math.max(1f, sharedConfig.BarHeight));
        runtimeMaterial.SetFloat(EndCapNormalizedId, math.clamp(sharedConfig.EndCapWidth / resolvedLength, 0.001f, 0.45f));
        runtimeMaterial.SetFloat(GraduationInsetNormalizedId, math.clamp(resolvedGraduationStartInset / resolvedLength, 0.001f, 0.45f));
        runtimeMaterial.SetFloat(GraduationEndNormalizedId, math.clamp(1f - resolvedGraduationEndInset / resolvedLength, 0.55f, 0.999f));
        runtimeMaterial.SetFloat(TerminationOffsetNormalizedId, math.clamp(terminationOffset / resolvedLength, 0f, 0.45f));
        runtimeMaterial.SetFloat(MajorDivisionCountId, math.max(0.0001f, intervalCount));
        runtimeMaterial.SetFloat(PlungerWidthId, ResolveReferenceScaledNormalized(sharedConfig.PlungerWidth, resolvedLength, 0.2f));
        runtimeMaterial.SetFloat(PaintDripWidthId, ResolveReferenceScaledNormalized(sharedConfig.PaintDrips.Width, resolvedLength, 0.25f));
        runtimeMaterial.SetFloat(LengthPixelScaleId, math.clamp(resolvedLength / ReferenceDecorationLength, 0.25f, 4f));
        // Aspect (height/length) lets the shader keep the horizontal outline as thick as the vertical one, so both
        // syringes show an identical outline regardless of their different resolved lengths.
        runtimeMaterial.SetFloat(OutlineAspectId, math.clamp(math.max(1f, sharedConfig.BarHeight) / resolvedLength, 0.02f, 4f));

        if (labelsRoot != null)
        {
            labelsRoot.offsetMin = new Vector2(resolvedGraduationStartInset, labelsRoot.offsetMin.y);
            labelsRoot.offsetMax = new Vector2(-resolvedGraduationEndInset, labelsRoot.offsetMax.y);
            labelsRoot.SetAsLastSibling();
        }

        if (labelPool != null)
        {
            labelPool.Rebuild(labelsRoot,
                              maximumValue,
                              safeUnits,
                              math.max(1, sharedConfig.LabelEveryMajorDivision),
                              math.clamp(sharedConfig.MaximumLabelCount, 2, PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
                              graduationPixelWidth,
                              math.max(1f, sharedConfig.LabelMinimumSpacing),
                              sharedConfig.LabelPlacement,
                              math.max(1f, sharedConfig.LabelFontSize),
                              sharedConfig.LabelOffset,
                              math.clamp(sharedConfig.GraduationVerticalOffset, -0.5f, 0.5f),
                              channelConfig.Palette.Label,
                              channelConfig.Palette.LabelOutline,
                              math.saturate(sharedConfig.LabelOutlineWidth),
                              activeFont);
        }

        MarkParentLayoutDirty();
    }

    /// <summary>
    /// Converts a reference-length normalized visual size into the normalized size needed by the current syringe length.
    /// </summary>
    /// <param name="normalizedValue">Authored normalized value tuned against the reference syringe length.</param>
    /// <param name="resolvedLength">Current resolved syringe length in pixels.</param>
    /// <param name="maximumValue">Maximum normalized value accepted by the target shader property.</param>
    /// <returns>Length-compensated normalized value preserving stable pixel size across short and long syringes.</returns>
    private static float ResolveReferenceScaledNormalized(float normalizedValue, float resolvedLength, float maximumValue)
    {
        return math.clamp(math.max(0f, normalizedValue) *
                          ReferenceDecorationLength /
                          math.max(1f, resolvedLength),
                          0f,
                          maximumValue);
    }
    #endregion

    #region Motion
    /// <summary>
    /// Updates the optional value-change impulse without affecting layout.
    /// </summary>
    /// <param name="deltaTime">Unscaled frame duration.</param>
    private void UpdateValueImpulse(float deltaTime)
    {
        if (channelConfig.Motion.ValueImpulseEnabled == 0)
        {
            valueImpulse = 0f;
            return;
        }

        if (!Mathf.Approximately(previousTargetNormalized, targetNormalized))
            valueImpulse += (targetNormalized - previousTargetNormalized) * channelConfig.Motion.ValueImpulseStrength;

        valueImpulse = Mathf.MoveTowards(valueImpulse,
                                         0f,
                                         math.max(0f, channelConfig.Motion.ValueImpulseDecay) * deltaTime);
        valueImpulse = math.clamp(valueImpulse, -1f, 1f);
    }

    /// <summary>
    /// Updates optional inertial slosh and Z-axis tilt, sleeping naturally when both return to rest.
    /// </summary>
    /// <param name="velocityX">Current player world-X velocity.</param>
    /// <param name="deltaTime">Unscaled frame duration.</param>
    private void UpdateReactiveMotion(float velocityX, float deltaTime)
    {
        if (!math.isfinite(velocityX) || !math.isfinite(deltaTime))
        {
            ResetReactiveMotion();
            return;
        }

        if (!motionInitialized)
        {
            previousVelocityX = velocityX;
            motionInitialized = true;
        }

        if (deltaTime <= 0f)
            return;

        float safeDeltaTime = math.min(deltaTime, MaximumReactiveDeltaTime);

        if (channelConfig.Motion.MovementReactionEnabled != 0)
        {
            float maximumSlosh = math.max(0f, channelConfig.Motion.MaximumSlosh);
            float accelerationX = (velocityX - previousVelocityX) / safeDeltaTime;
            sloshVelocity -= accelerationX * channelConfig.Motion.SloshStrength;
            sloshVelocity += (-slosh * math.max(0f, channelConfig.Motion.SloshSpring) -
                              sloshVelocity * math.max(0f, channelConfig.Motion.SloshDamping)) * safeDeltaTime;
            sloshVelocity = math.clamp(sloshVelocity,
                                       -maximumSlosh * MaximumReactiveVelocityMultiplier,
                                       maximumSlosh * MaximumReactiveVelocityMultiplier);
            slosh = math.clamp(slosh + sloshVelocity * safeDeltaTime, -maximumSlosh, maximumSlosh);
        }
        else
        {
            slosh = 0f;
            sloshVelocity = 0f;
        }

        if (channelConfig.Motion.TiltEnabled != 0 && motionRoot != null)
        {
            float maximumTilt = math.max(0f, channelConfig.Motion.MaximumTiltDegrees);
            float targetTilt = math.clamp(-velocityX * 0.25f, -maximumTilt, maximumTilt);
            tiltVelocity += ((targetTilt - tilt) * math.max(0f, channelConfig.Motion.TiltSpring) -
                             tiltVelocity * math.max(0f, channelConfig.Motion.TiltDamping)) * safeDeltaTime;
            tiltVelocity = math.clamp(tiltVelocity,
                                      -maximumTilt * MaximumReactiveVelocityMultiplier,
                                      maximumTilt * MaximumReactiveVelocityMultiplier);
            tilt = math.clamp(tilt + tiltVelocity * safeDeltaTime, -maximumTilt, maximumTilt);
            motionRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
        else if (motionRoot != null)
        {
            tilt = 0f;
            tiltVelocity = 0f;
            motionRoot.localRotation = Quaternion.identity;
        }

        previousVelocityX = velocityX;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Changes view visibility only when the requested state differs.
    /// </summary>
    /// <param name="visible">Requested active state.</param>
    private void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible)
            return;

        gameObject.SetActive(visible);
        MarkParentLayoutDirty();
    }

    /// <summary>
    /// Invalidates the authored parent layout only after this view changes size or visibility.
    /// </summary>
    private void MarkParentLayoutDirty()
    {
        if (transform.parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }
    #endregion

    #endregion
}
