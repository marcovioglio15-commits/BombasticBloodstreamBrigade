using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bridges enemy ECS health and footprint state to a single shader-driven ground indicator mesh.
/// The view owns a flat quad lying on the world XZ plane whose material is configured via a
/// MaterialPropertyBlock so all per-enemy ring or arc variation is pushed without instantiating extra materials.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyGroundIndicatorView : MonoBehaviour
{
    #region Constants
    private const float MinimumOuterRadius = 0.01f;
    private const float SmoothingDenominatorEpsilon = 0.0001f;
    private const float CameraAngleFallbackRadians = -1.5707963267948966f;
    private static readonly Quaternion GroundPlaneRotation = Quaternion.Euler(90f, 0f, 0f);
    private static readonly int HitFootprintPropertyId = Shader.PropertyToID("_HitFootprint");
    private static readonly int RingParamsPropertyId = Shader.PropertyToID("_RingParams");
    private static readonly int FillPropertyId = Shader.PropertyToID("_Fill");
    private static readonly int SoftnessPropertyId = Shader.PropertyToID("_Softness");
    private static readonly int ShadowColorPropertyId = Shader.PropertyToID("_ShadowColor");
    private static readonly int HealthFillColorPropertyId = Shader.PropertyToID("_HealthFillColor");
    private static readonly int HealthBackgroundColorPropertyId = Shader.PropertyToID("_HealthBackgroundColor");
    private static readonly int ShieldFillColorPropertyId = Shader.PropertyToID("_ShieldFillColor");
    private static readonly int ShieldBackgroundColorPropertyId = Shader.PropertyToID("_ShieldBackgroundColor");
    #endregion

    #region Fields

    #region Serialized Fields

    #region References
    [Header("References")]
    [Tooltip("Mesh renderer used to draw the shader-driven shadow and fillable rings. The mesh must be a flat unit quad whose UV maps the full local extent.")]
    [SerializeField] private MeshRenderer meshRenderer;

    [Tooltip("Optional mesh filter used to resolve the authored mesh bounds. When empty, the component on the same GameObject is used.")]
    [SerializeField] private MeshFilter meshFilter;

    [Tooltip("Optional GameObject toggled to show or hide the entire indicator widget. When empty, the indicator GameObject itself is used.")]
    [SerializeField] private GameObject visibilityRoot;
    #endregion

    #region Behavior
    [Header("Behavior")]
    [Tooltip("Smoothing duration in seconds used to interpolate the displayed health fill toward the target value. Set 0 for instantaneous changes.")]
    [SerializeField] private float smoothingSeconds = 0.08f;

    [Tooltip("Optional smoothing duration in seconds used for the shield fill. Set 0 to reuse the generic smoothing value.")]
    [SerializeField] private float shieldSmoothingSeconds = 0.08f;

    [Tooltip("Hide the shield ring when the shield value reaches zero, or when the enemy has no shield capacity.")]
    [SerializeField] private bool hideShieldWhenEmpty = true;

    [Tooltip("Hide the entire indicator while the enemy is inactive in the pooling pipeline.")]
    [SerializeField] private bool hideWhenEnemyInactive = true;

    [Tooltip("Hide the indicator while the enemy is culled by visual distance culling.")]
    [SerializeField] private bool hideWhenEnemyCulled;
    #endregion

    #endregion

    #region Runtime State
    private MaterialPropertyBlock cachedPropertyBlock;
    private bool propertyBlockDirty;
    private float displayedHealthNormalized = 1f;
    private float displayedShieldNormalized;
    private float currentCameraAngleRadians = CameraAngleFallbackRadians;
    private bool footprintInitialized;
    private float cachedShadowRadiusX;
    private float cachedShadowRadiusZ;
    private float cachedOuterRadius;
    private float cachedHeightOffset;
    private float2 cachedPositionOffsetXZ;
    private bool cachedLockRingsToWorld;
    private float cachedLockedRingsAngleRadians;
    private bool cachedHasShield;
    private bool colorsInitialized;
    private float4 cachedShadowColor;
    private float4 cachedHealthFillColor;
    private float4 cachedHealthBackgroundColor;
    private float4 cachedShieldFillColor;
    private float4 cachedShieldBackgroundColor;
    private float cachedShadowAlpha;
    private float cachedShadowEdgeSoftness;
    private float cachedRingEdgeSoftness;
    private float cachedRingAngularSoftness;
    private float cachedRingArcRadians;
    private bool cachedRingsVisible;
    private bool softnessInitialized;
    private bool visibilityStateInitialized;
    private bool lastVisibilityState = true;
    private bool enemyActiveStateInitialized;
    private bool lastEnemyActiveState;
    private bool pendingImmediateValueRefresh;
    private bool suppressRings;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Resolves references and pushes a safe initial state to the renderer so the indicator is
    /// readable as soon as the enemy entity is enabled.
    /// </summary>
    private void Awake()
    {
        ResolveMissingReferences();
        EnsureGroundPlaneOrientation();
        EnsurePropertyBlock();
        propertyBlockDirty = true;
    }

    /// <summary>
    /// Re-resolves references after inspector edits without mutating runtime state.
    /// </summary>
    private void OnValidate()
    {
        ResolveMissingReferences();

        if (smoothingSeconds < 0f)
            smoothingSeconds = 0f;

        if (shieldSmoothingSeconds < 0f)
            shieldSmoothingSeconds = 0f;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Synchronizes the indicator world position so it lies on the floor under the enemy.
    /// The transform rotation is reset every frame to the ground-plane rotation so the quad
    /// always faces upward regardless of authored prefab transforms.
    /// </summary>
    /// <param name="enemyPosition">World-space enemy pivot position.</param>
    /// <param name="enemyRotation">World-space enemy pivot rotation used when the baked hit-center offset follows root orientation.</param>
    /// <param name="enemyScale">Uniform enemy pivot scale used to scale the local hit-center offset.</param>
    /// <param name="rotateHitCenterOffset">True when the local hit-center offset should rotate with the enemy root.</param>
    /// <param name="cameraTransform">Active camera transform used to update the depleting-edge anchor.</param>
    public void SyncWorldPose(Vector3 enemyPosition,
                              Quaternion enemyRotation,
                              float enemyScale,
                              bool rotateHitCenterOffset,
                              Transform cameraTransform)
    {
        Transform selfTransform = transform;
        Vector3 targetPosition = EnemyHitboxCenterUtility.ResolveWorldCenter(enemyPosition,
                                                                             enemyRotation,
                                                                             enemyScale,
                                                                             cachedPositionOffsetXZ,
                                                                             rotateHitCenterOffset,
                                                                             cachedHeightOffset);
        selfTransform.position = targetPosition;
        selfTransform.rotation = GroundPlaneRotation;

        // When the rings are locked to world, the fill anchor stays at the baked angle (camera-independent)
        // so the depleting gap reveals a fixed world-space direction regardless of camera orientation.
        if (cachedLockRingsToWorld)
        {
            currentCameraAngleRadians = cachedLockedRingsAngleRadians;
            return;
        }

        currentCameraAngleRadians = EnemyGroundIndicatorFootprintUtility.ResolveCameraFacingAngleRadians(targetPosition,
                                                                                                         cameraTransform,
                                                                                                         currentCameraAngleRadians);
    }

    /// <summary>
    /// Updates the resolved footprint layout and color set when authored values change. Called by the
    /// presentation system every frame but applies work only when the inputs deviate from the cache.
    /// </summary>
    /// <param name="contactRadiusX">Contact-damage hit half-axis along world X used to size the shadow.</param>
    /// <param name="contactRadiusZ">Contact-damage hit half-axis along world Z used to size the shadow.</param>
    /// <param name="config">Baked ground-indicator configuration sourced from the visual preset.</param>
    /// <param name="hasShieldCapacity">True when this enemy has shield capacity.</param>
    /// <param name="shieldNormalized">Current normalized shield value used to decide whether an empty shield ring still reserves footprint space.</param>
    public void SyncFootprint(float contactRadiusX,
                              float contactRadiusZ,
                              in EnemyGroundIndicatorConfig config,
                              bool hasShieldCapacity,
                              float shieldNormalized)
    {
        suppressRings = config.SuppressRings != 0;
        bool hasVisibleShieldRing = !suppressRings && ResolveVisibleShieldRing(hasShieldCapacity, shieldNormalized);

        // Always refresh the cached pose offsets so runtime edits to the visual preset propagate without
        // having to detect layout-affecting changes first.
        cachedHeightOffset = config.HeightOffset;
        cachedPositionOffsetXZ = config.PositionOffsetXZ;
        cachedLockRingsToWorld = config.LockRingsToWorld != 0;
        cachedLockedRingsAngleRadians = config.LockedRingsAngleRadians;

        bool layoutChanged = ApplyFootprintLayoutIfChanged(contactRadiusX, contactRadiusZ, in config, hasVisibleShieldRing);
        bool colorsChanged = ApplyColorsIfChanged(in config);
        bool softnessChanged = ApplySoftnessIfChanged(in config);

        if (layoutChanged || colorsChanged || softnessChanged)
            propertyBlockDirty = true;

        FlushPropertyBlockIfDirty();
    }

    /// <summary>
    /// Updates the visibility state and the displayed fill values in a single call. Used by the
    /// sync system on the frames where both blocks need to be refreshed.
    /// </summary>
    /// <param name="healthNormalized">Target normalized health value.</param>
    /// <param name="shieldNormalized">Target normalized shield value.</param>
    /// <param name="enemyActive">True when the owning enemy is currently active.</param>
    /// <param name="enemyVisible">True when the owning enemy is currently visible after culling.</param>
    /// <param name="deltaTime">Presentation delta time used by the fill smoothing.</param>
    public void SyncFromRuntime(float healthNormalized, float shieldNormalized, bool enemyActive, bool enemyVisible, float deltaTime)
    {
        SyncVisibilityState(enemyActive, enemyVisible);
        SyncDisplayedValues(healthNormalized, shieldNormalized, deltaTime);
    }

    /// <summary>
    /// Updates the visibility state of the indicator without touching displayed health and shield values.
    /// </summary>
    /// <param name="enemyActive">True when the owning enemy is currently active.</param>
    /// <param name="enemyVisible">True when the owning enemy is currently visible after culling.</param>
    public void SyncVisibilityState(bool enemyActive, bool enemyVisible)
    {
        if (ResolveImmediateRefresh(enemyActive))
            pendingImmediateValueRefresh = true;

        bool shouldDisplay = ResolveBarVisibility(enemyActive, enemyVisible);
        ApplyVisibility(shouldDisplay);
    }

    /// <summary>
    /// Updates the displayed normalized fills with smoothing and pushes the new values into the
    /// MaterialPropertyBlock together with the latest camera-facing angle resolved by SyncWorldPose.
    /// </summary>
    /// <param name="healthNormalized">Target normalized health value.</param>
    /// <param name="shieldNormalized">Target normalized shield value.</param>
    /// <param name="deltaTime">Presentation delta time used for smoothing.</param>
    public void SyncDisplayedValues(float healthNormalized, float shieldNormalized, float deltaTime)
    {
        float targetHealthNormalized = math.saturate(healthNormalized);
        float targetShieldNormalized = math.saturate(shieldNormalized);
        bool forceImmediateUpdate = pendingImmediateValueRefresh;
        pendingImmediateValueRefresh = false;
        UpdateDisplayedValues(targetHealthNormalized, targetShieldNormalized, deltaTime, forceImmediateUpdate);
        ApplyFillProperty();
        FlushPropertyBlockIfDirty();
    }
    #endregion

    #region Helpers

    #region Reference Resolution
    /// <summary>
    /// Resolves missing renderer and mesh filter references on the same GameObject so the prefab
    /// only needs to expose the EnemyGroundIndicatorView component itself.
    /// </summary>
    private void ResolveMissingReferences()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (visibilityRoot == null)
            visibilityRoot = gameObject;
    }

    /// <summary>
    /// Snaps the indicator GameObject to the ground-plane rotation in edit time so authors see the
    /// final orientation while editing the prefab.
    /// </summary>
    private void EnsureGroundPlaneOrientation()
    {
        transform.localRotation = GroundPlaneRotation;
    }

    /// <summary>
    /// Lazily allocates the shared MaterialPropertyBlock used for every renderer update.
    /// </summary>
    private void EnsurePropertyBlock()
    {
        if (cachedPropertyBlock == null)
            cachedPropertyBlock = new MaterialPropertyBlock();
    }
    #endregion

    #region Layout
    /// <summary>
    /// Resolves the visible shield ring state from authored hide-when-empty behaviour and current shield value.
    /// </summary>
    /// <param name="hasShieldCapacity">True when this enemy has shield capacity.</param>
    /// <param name="shieldNormalized">Current normalized shield value.</param>
    /// <returns>True when the shield ring should be drawn.</returns>
    private bool ResolveVisibleShieldRing(bool hasShieldCapacity, float shieldNormalized)
    {
        if (!hasShieldCapacity)
            return false;

        if (!hideShieldWhenEmpty)
            return true;

        return shieldNormalized > 0f;
    }

    /// <summary>
    /// Computes the new footprint layout and applies it to the renderer transform and property block.
    /// Returns true when any cached value changed so the caller can flag the property block dirty.
    /// </summary>
    /// <param name="contactRadiusX">Contact-damage hit half-axis along world X used to size the shadow.</param>
    /// <param name="contactRadiusZ">Contact-damage hit half-axis along world Z used to size the shadow.</param>
    /// <param name="config">Baked ground-indicator configuration.</param>
    /// <param name="hasVisibleShieldRing">True when the shield ring contributes to the displayed footprint.</param>
    /// <returns>True when the layout changed and the property block needs a refresh.</returns>
    private bool ApplyFootprintLayoutIfChanged(float contactRadiusX,
                                               float contactRadiusZ,
                                               in EnemyGroundIndicatorConfig config,
                                               bool hasVisibleShieldRing)
    {
        EnemyGroundIndicatorLayout layout = EnemyGroundIndicatorFootprintUtility.ResolveLayout(contactRadiusX,
                                                                                                contactRadiusZ,
                                                                                                in config,
                                                                                                !suppressRings,
                                                                                                hasVisibleShieldRing);
        bool changed = !footprintInitialized
                       || !Mathf.Approximately(cachedShadowRadiusX, layout.ShadowRadiusX)
                       || !Mathf.Approximately(cachedShadowRadiusZ, layout.ShadowRadiusZ)
                       || !Mathf.Approximately(cachedOuterRadius, layout.OuterRadius)
                       || cachedHasShield != hasVisibleShieldRing;

        if (!changed)
            return false;

        ApplyTransformScale(layout.OuterRadius);
        EnsurePropertyBlock();
        cachedPropertyBlock.SetVector(HitFootprintPropertyId, new Vector4(layout.ShadowRadiusX,
                                                                          layout.ShadowRadiusZ,
                                                                          math.max(0f, config.ShadowEdgeSoftness),
                                                                          math.saturate(config.ShadowAlpha)));
        cachedPropertyBlock.SetVector(RingParamsPropertyId, new Vector4(math.max(0f, config.RingDistanceFromShadow),
                                                                        math.max(0f, config.RingThickness),
                                                                        math.max(0f, config.RingSpacing),
                                                                        hasVisibleShieldRing ? 1f : 0f));
        cachedShadowRadiusX = layout.ShadowRadiusX;
        cachedShadowRadiusZ = layout.ShadowRadiusZ;
        cachedOuterRadius = layout.OuterRadius;
        cachedHasShield = hasVisibleShieldRing;
        footprintInitialized = true;
        return true;
    }

    /// <summary>
    /// Pushes the color set into the property block when any cached color changes between frames.
    /// </summary>
    /// <param name="config">Baked ground-indicator configuration.</param>
    /// <returns>True when any color changed and the property block needs a refresh.</returns>
    private bool ApplyColorsIfChanged(in EnemyGroundIndicatorConfig config)
    {
        bool changed = !colorsInitialized
                       || !AreColorsApproximatelyEqual(cachedShadowColor, config.ShadowColor)
                       || !AreColorsApproximatelyEqual(cachedHealthFillColor, config.HealthFillColor)
                       || !AreColorsApproximatelyEqual(cachedHealthBackgroundColor, config.HealthBackgroundColor)
                       || !AreColorsApproximatelyEqual(cachedShieldFillColor, config.ShieldFillColor)
                       || !AreColorsApproximatelyEqual(cachedShieldBackgroundColor, config.ShieldBackgroundColor);

        if (!changed)
            return false;

        EnsurePropertyBlock();
        cachedPropertyBlock.SetColor(ShadowColorPropertyId, ToColor(config.ShadowColor));
        cachedPropertyBlock.SetColor(HealthFillColorPropertyId, ToColor(config.HealthFillColor));
        cachedPropertyBlock.SetColor(HealthBackgroundColorPropertyId, ToColor(config.HealthBackgroundColor));
        cachedPropertyBlock.SetColor(ShieldFillColorPropertyId, ToColor(config.ShieldFillColor));
        cachedPropertyBlock.SetColor(ShieldBackgroundColorPropertyId, ToColor(config.ShieldBackgroundColor));
        cachedShadowColor = config.ShadowColor;
        cachedHealthFillColor = config.HealthFillColor;
        cachedHealthBackgroundColor = config.HealthBackgroundColor;
        cachedShieldFillColor = config.ShieldFillColor;
        cachedShieldBackgroundColor = config.ShieldBackgroundColor;
        colorsInitialized = true;
        return true;
    }

    /// <summary>
    /// Pushes the ring softness and arc controls into the property block when any cached value changes between frames.
    /// </summary>
    /// <param name="config">Baked ground-indicator configuration.</param>
    /// <returns>True when the softness inputs changed and the property block needs a refresh.</returns>
    private bool ApplySoftnessIfChanged(in EnemyGroundIndicatorConfig config)
    {
        float resolvedShadowAlpha = math.saturate(config.ShadowAlpha);
        float resolvedShadowSoftness = math.max(0f, config.ShadowEdgeSoftness);
        float resolvedRingEdge = math.max(0f, config.RingEdgeSoftness);
        float resolvedRingAngular = math.max(0f, config.RingAngularSoftness);
        float resolvedRingArcDegrees = EnemyGroundIndicatorFootprintUtility.ResolveRuntimeRingArcDegrees(config.RingArcDegrees);
        float resolvedRingArcRadians = math.radians(resolvedRingArcDegrees);
        bool ringsVisible = !suppressRings;
        bool changed = !softnessInitialized
                       || !Mathf.Approximately(cachedShadowAlpha, resolvedShadowAlpha)
                       || !Mathf.Approximately(cachedShadowEdgeSoftness, resolvedShadowSoftness)
                       || !Mathf.Approximately(cachedRingEdgeSoftness, resolvedRingEdge)
                       || !Mathf.Approximately(cachedRingAngularSoftness, resolvedRingAngular)
                       || !Mathf.Approximately(cachedRingArcRadians, resolvedRingArcRadians)
                       || cachedRingsVisible != ringsVisible;

        if (!changed)
            return false;

        EnsurePropertyBlock();
        cachedPropertyBlock.SetVector(SoftnessPropertyId, new Vector4(resolvedRingEdge,
                                                                      resolvedRingAngular,
                                                                      resolvedRingArcRadians,
                                                                      ringsVisible ? 1f : 0f));
        cachedShadowAlpha = resolvedShadowAlpha;
        cachedShadowEdgeSoftness = resolvedShadowSoftness;
        cachedRingEdgeSoftness = resolvedRingEdge;
        cachedRingAngularSoftness = resolvedRingAngular;
        cachedRingArcRadians = resolvedRingArcRadians;
        cachedRingsVisible = ringsVisible;
        softnessInitialized = true;
        return true;
    }

    /// <summary>
    /// Resizes the indicator transform so the authored mesh covers the outermost ring diameter.
    /// </summary>
    /// <param name="outerRadius">Outermost ring half-axis used to size the mesh extents.</param>
    private void ApplyTransformScale(float outerRadius)
    {
        Vector3 currentScale = transform.localScale;
        float preservedLocalZ = currentScale.z;

        if (Mathf.Approximately(preservedLocalZ, 0f))
            preservedLocalZ = 1f;

        Vector3 resolvedScale = EnemyGroundIndicatorFootprintUtility.ResolveLocalScale(meshFilter,
                                                                                        transform,
                                                                                        math.max(MinimumOuterRadius, outerRadius),
                                                                                        preservedLocalZ);
        transform.localScale = resolvedScale;
    }
    #endregion

    #region Per-Frame Fill
    /// <summary>
    /// Interpolates the displayed health and shield values toward their target with authored smoothing.
    /// </summary>
    /// <param name="targetHealthNormalized">Target normalized health value.</param>
    /// <param name="targetShieldNormalized">Target normalized shield value.</param>
    /// <param name="deltaTime">Presentation delta time used for smoothing.</param>
    /// <param name="forceImmediateUpdate">When true, skips smoothing and snaps to target values.</param>
    private void UpdateDisplayedValues(float targetHealthNormalized, float targetShieldNormalized, float deltaTime, bool forceImmediateUpdate)
    {
        if (forceImmediateUpdate || deltaTime <= 0f)
        {
            displayedHealthNormalized = targetHealthNormalized;
            displayedShieldNormalized = targetShieldNormalized;
            return;
        }

        if (smoothingSeconds <= 0f)
            displayedHealthNormalized = targetHealthNormalized;
        else
        {
            float healthStep = deltaTime / math.max(SmoothingDenominatorEpsilon, smoothingSeconds);
            displayedHealthNormalized = Mathf.MoveTowards(displayedHealthNormalized, targetHealthNormalized, healthStep);
        }

        float resolvedShieldSmoothing = shieldSmoothingSeconds;

        if (resolvedShieldSmoothing <= 0f)
            resolvedShieldSmoothing = smoothingSeconds;

        if (resolvedShieldSmoothing <= 0f)
        {
            displayedShieldNormalized = targetShieldNormalized;
            return;
        }

        float shieldStep = deltaTime / math.max(SmoothingDenominatorEpsilon, resolvedShieldSmoothing);
        displayedShieldNormalized = Mathf.MoveTowards(displayedShieldNormalized, targetShieldNormalized, shieldStep);
    }

    /// <summary>
    /// Writes the latest health and shield fills together with the cached camera angle into the property block.
    /// When the rings are suppressed (boss UI takes over), both fills are forced to zero so only the shadow draws.
    /// </summary>
    private void ApplyFillProperty()
    {
        EnsurePropertyBlock();
        float healthValue = suppressRings ? 0f : displayedHealthNormalized;
        float shieldValue = suppressRings ? 0f : displayedShieldNormalized;
        cachedPropertyBlock.SetVector(FillPropertyId, new Vector4(healthValue,
                                                                  shieldValue,
                                                                  currentCameraAngleRadians,
                                                                  math.max(MinimumOuterRadius, cachedOuterRadius)));
        propertyBlockDirty = true;
    }

    /// <summary>
    /// Flushes the cached property block to the renderer when it has pending changes.
    /// </summary>
    private void FlushPropertyBlockIfDirty()
    {
        if (!propertyBlockDirty || meshRenderer == null)
            return;

        meshRenderer.SetPropertyBlock(cachedPropertyBlock);
        propertyBlockDirty = false;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Detects the transition from inactive to active to force an immediate fill snap on the next sync.
    /// </summary>
    /// <param name="enemyActive">True when the owning enemy is currently active.</param>
    /// <returns>True when the next displayed value sync should skip smoothing.</returns>
    private bool ResolveImmediateRefresh(bool enemyActive)
    {
        if (!enemyActiveStateInitialized)
        {
            enemyActiveStateInitialized = true;
            lastEnemyActiveState = enemyActive;
            return true;
        }

        bool transitionedToActive = enemyActive && !lastEnemyActiveState;
        lastEnemyActiveState = enemyActive;
        return transitionedToActive;
    }

    /// <summary>
    /// Resolves whether the indicator should be drawn based on the active and visible flags.
    /// </summary>
    /// <param name="enemyActive">True when the owning enemy is currently active.</param>
    /// <param name="enemyVisible">True when the owning enemy is currently visible.</param>
    /// <returns>True when the indicator should be drawn.</returns>
    private bool ResolveBarVisibility(bool enemyActive, bool enemyVisible)
    {
        if (hideWhenEnemyInactive && !enemyActive)
            return false;

        if (hideWhenEnemyCulled && !enemyVisible)
            return false;

        return true;
    }

    /// <summary>
    /// Applies the resolved visibility state to the visibility root and the renderer, skipping work
    /// when the state is unchanged. The indicator is baked as inactive + renderer disabled so the
    /// Entities Graphics renderer entity is never created; here we restore both at runtime so the
    /// MeshRenderer renders via Unity's standard scene pipeline where MaterialPropertyBlock works.
    /// </summary>
    /// <param name="shouldDisplay">True when the indicator should be active.</param>
    private void ApplyVisibility(bool shouldDisplay)
    {
        if (visibilityStateInitialized && shouldDisplay == lastVisibilityState)
            return;

        if (visibilityRoot != null)
            visibilityRoot.SetActive(shouldDisplay);

        if (meshRenderer != null && meshRenderer.enabled != shouldDisplay)
            meshRenderer.enabled = shouldDisplay;

        lastVisibilityState = shouldDisplay;
        visibilityStateInitialized = true;
    }
    #endregion

    #region Math Helpers
    private static bool AreColorsApproximatelyEqual(float4 first, float4 second)
    {
        return Mathf.Approximately(first.x, second.x)
               && Mathf.Approximately(first.y, second.y)
               && Mathf.Approximately(first.z, second.z)
               && Mathf.Approximately(first.w, second.w);
    }

    private static Color ToColor(float4 packed)
    {
        return new Color(packed.x, packed.y, packed.z, packed.w);
    }
    #endregion

    #endregion

    #endregion
}
