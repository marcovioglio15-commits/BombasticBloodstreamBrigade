using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the screen-space boss HUD from ECS boss health, transform and visual preset data.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyBossHudPresentation : MonoBehaviour
{
    #region Constants
    private const float DefaultResolveIntervalSeconds = 0.25f;
    private const float CameraResolveIntervalSeconds = 0.5f;
    private const float Epsilon = 0.0001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("References")]
    [Tooltip("UI content root toggled when a valid boss is available. If this is the presenter object, the panel root is toggled instead so runtime updates keep running.")]
    [SerializeField] private GameObject visibilityRoot;

    [Tooltip("Rect transform containing the mirrored top-right boss name, health bar and shield bar.")]
    [SerializeField] private RectTransform panelRoot;

    [Tooltip("Text label that displays the active boss name.")]
    [SerializeField] private TMP_Text bossNameText;

    [Tooltip("Fill image used for the active boss health percentage.")]
    [SerializeField] private Image healthFillImage;

    [Tooltip("Background image behind the boss health fill.")]
    [SerializeField] private Image healthBackgroundImage;

    [Tooltip("Fill image used for the active boss shield percentage.")]
    [SerializeField] private Image shieldFillImage;

    [Tooltip("Background image behind the boss shield fill.")]
    [SerializeField] private Image shieldBackgroundImage;

    [Header("Bar Presentation")]
    [Tooltip("Liquid shader and piston behavior used by the mirrored boss health syringe bar.")]
    [SerializeField] private HUDLiquidBarPresentationSettings healthBarPresentation = HUDLiquidBarPresentationSettings.CreateHealthDefaults();

    [Tooltip("Liquid shader and piston behavior used by the mirrored boss shield syringe bar.")]
    [SerializeField] private HUDLiquidBarPresentationSettings shieldBarPresentation = HUDLiquidBarPresentationSettings.CreateShieldDefaults();

    [Tooltip("Rect transform moved along screen borders when the boss is outside camera view.")]
    [SerializeField] private RectTransform offscreenIndicatorRoot;

    [Tooltip("Image used as the offscreen boss direction indicator.")]
    [SerializeField] private Image offscreenIndicatorImage;

    [Tooltip("Optional camera used for boss screen projection. When empty, the active main camera is resolved periodically.")]
    [SerializeField] private Camera targetCamera;

    [Header("Behavior")]
    [Tooltip("Seconds used to smooth boss health fill transitions. Set to zero for immediate updates.")]
    [SerializeField] private float healthSmoothingSeconds = 0.08f;

    [Tooltip("Seconds between boss entity lookup attempts when no cached boss is available.")]
    [SerializeField] private float bossResolveIntervalSeconds = DefaultResolveIntervalSeconds;

    [Tooltip("Hide the whole boss HUD when no active boss entity is available.")]
    [SerializeField] private bool hideWhenNoBoss = true;
    #endregion

    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery bossQuery;
    private Entity cachedBossEntity = Entity.Null;
    private Canvas rootCanvas;
    private RectTransform indicatorParentRect;
    private Camera cachedCamera;
    private float nextBossResolveTime;
    private float nextCameraResolveTime;
    private float displayedHealthNormalized = 1f;
    private float displayedShieldNormalized;
    private HUDLiquidBarRuntime healthBarRuntime;
    private HUDLiquidBarRuntime shieldBarRuntime;
    private bool ecsInitialized;
    private bool visibilityInitialized;
    private string displayedBossName;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Validates UI references and applies a safe initial hidden state.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void Awake()
    {
        ValidateReferences();
        EnsureBossBarVisualsInitialized();
        ApplyVisibility(!hideWhenNoBoss);
        ApplyInitialBarVisualState();
    }

    /// <summary>
    /// Releases runtime material instances created by the liquid boss bars.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnDestroy()
    {
        DisposeBossBarVisuals();
    }

    /// <summary>
    /// Keeps serialized settings safe after inspector edits.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void OnValidate()
    {
        ValidateReferences();
        EnsureBossBarPresentationSettings();

        if (healthSmoothingSeconds < 0f)
            healthSmoothingSeconds = 0f;

        if (bossResolveIntervalSeconds < 0.05f)
            bossResolveIntervalSeconds = 0.05f;
    }

    /// <summary>
    /// Updates the boss HUD from the cached ECS boss entity.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void Update()
    {
        EnsureBossBarVisualsInitialized();

        if (!TryInitializeEcsBindings())
        {
            HandleMissingBoss();
            return;
        }

        if (!TryResolveBossEntity(Time.unscaledTime, out Entity bossEntity))
        {
            HandleMissingBoss();
            return;
        }

        SyncBossHud(bossEntity, Time.unscaledDeltaTime);
    }
    #endregion

    #region ECS
    /// <summary>
    /// Initializes or refreshes cached ECS world, entity manager and boss query references.
    /// /params None.
    /// /returns True when ECS bindings are ready.
    /// </summary>
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || !currentWorld.IsCreated)
        {
            ClearEcsBindings();
            return false;
        }

        if (!ReferenceEquals(defaultWorld, currentWorld))
        {
            defaultWorld = currentWorld;
            entityManager = defaultWorld.EntityManager;
            cachedBossEntity = Entity.Null;
            ecsInitialized = false;
        }

        if (ecsInitialized)
            return true;

        EntityQueryDesc queryDescription = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<EnemyBossTag>(),
                ComponentType.ReadOnly<EnemyBossHudConfig>(),
                ComponentType.ReadOnly<EnemyHealth>(),
                ComponentType.ReadOnly<LocalTransform>()
            }
        };

        bossQuery = entityManager.CreateEntityQuery(queryDescription);
        ecsInitialized = true;
        return true;
    }

    /// <summary>
    /// Resolves the active boss entity, reusing a cached entity while it stays valid.
    /// /params currentTime Current unscaled time used to throttle lookup attempts.
    /// /params bossEntity Resolved boss entity.
    /// /returns True when a valid boss entity is available.
    /// </summary>
    private bool TryResolveBossEntity(float currentTime, out Entity bossEntity)
    {
        if (cachedBossEntity != Entity.Null &&
            entityManager.Exists(cachedBossEntity) &&
            entityManager.HasComponent<EnemyBossTag>(cachedBossEntity) &&
            entityManager.HasComponent<EnemyActive>(cachedBossEntity) &&
            entityManager.IsComponentEnabled<EnemyActive>(cachedBossEntity))
        {
            bossEntity = cachedBossEntity;
            return true;
        }

        cachedBossEntity = Entity.Null;

        if (currentTime < nextBossResolveTime)
        {
            bossEntity = Entity.Null;
            return false;
        }

        nextBossResolveTime = currentTime + bossResolveIntervalSeconds;

        if (bossQuery.IsEmptyIgnoreFilter)
        {
            bossEntity = Entity.Null;
            return false;
        }

        NativeArray<Entity> bossEntities = bossQuery.ToEntityArray(Allocator.Temp);
        Entity resolvedBoss = Entity.Null;

        for (int index = 0; index < bossEntities.Length; index++)
        {
            Entity candidateEntity = bossEntities[index];

            if (!entityManager.Exists(candidateEntity))
                continue;

            if (!entityManager.HasComponent<EnemyActive>(candidateEntity))
                continue;

            if (!entityManager.IsComponentEnabled<EnemyActive>(candidateEntity))
                continue;

            EnemyBossHudConfig hudConfig = entityManager.GetComponentData<EnemyBossHudConfig>(candidateEntity);

            if (hudConfig.Enabled == 0)
                continue;

            resolvedBoss = candidateEntity;
            break;
        }

        bossEntities.Dispose();
        cachedBossEntity = resolvedBoss;
        bossEntity = resolvedBoss;
        return bossEntity != Entity.Null;
    }

    /// <summary>
    /// Clears cached ECS references after the default world becomes unavailable.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ClearEcsBindings()
    {
        defaultWorld = null;
        entityManager = default;
        cachedBossEntity = Entity.Null;
        ecsInitialized = false;
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Synchronizes health, text, colors and offscreen indicator for the active boss.
    /// /params bossEntity Active boss entity.
    /// /params deltaTime Unscaled frame delta used for smoothing.
    /// /returns None.
    /// </summary>
    private void SyncBossHud(Entity bossEntity, float deltaTime)
    {
        EnemyBossHudConfig hudConfig = entityManager.GetComponentData<EnemyBossHudConfig>(bossEntity);

        if (hudConfig.Enabled == 0)
        {
            HandleMissingBoss();
            return;
        }

        ApplyVisibility(true);
        SyncConfig(in hudConfig, bossEntity);
        SyncBars(bossEntity, deltaTime);
        SyncOffscreenIndicator(bossEntity, in hudConfig);
    }

    /// <summary>
    /// Applies boss HUD configuration baked from the selected visual preset.
    /// /params hudConfig Baked boss HUD config.
    /// /params bossEntity Active boss entity used to resolve managed sprite data.
    /// /returns None.
    /// </summary>
    private void SyncConfig(in EnemyBossHudConfig hudConfig, Entity bossEntity)
    {
        SyncBossName(hudConfig.DisplayName.ToString());
        EnemyBossHudPresentationUtility.ApplyImageColor(healthFillImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.HealthFillColor));
        EnemyBossHudPresentationUtility.ApplyImageColor(healthBackgroundImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.HealthBackgroundColor));
        EnemyBossHudPresentationUtility.ApplyImageColor(shieldFillImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.ShieldFillColor));
        EnemyBossHudPresentationUtility.ApplyImageColor(shieldBackgroundImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.ShieldBackgroundColor));
        ApplyOffscreenIndicatorConfig(bossEntity,
                                      EnemyBossHudPresentationUtility.ToColor(hudConfig.OffscreenIndicatorColor),
                                      hudConfig.OffscreenIndicatorSizePixels);
    }

    /// <summary>
    /// Updates the current health and shield fill values from ECS health data.
    /// /params bossEntity Active boss entity.
    /// /params deltaTime Unscaled frame delta used for smoothing.
    /// /returns None.
    /// </summary>
    private void SyncBars(Entity bossEntity, float deltaTime)
    {
        EnemyHealth health = entityManager.GetComponentData<EnemyHealth>(bossEntity);
        float targetHealthNormalized = 0f;
        float targetShieldNormalized = 0f;

        if (health.Max > 0f)
            targetHealthNormalized = Mathf.Clamp01(health.Current / health.Max);

        if (health.MaxShield > 0f)
            targetShieldNormalized = Mathf.Clamp01(health.CurrentShield / health.MaxShield);

        if (healthSmoothingSeconds <= 0f || deltaTime <= 0f)
        {
            displayedHealthNormalized = targetHealthNormalized;
            displayedShieldNormalized = targetShieldNormalized;
        }
        else
        {
            displayedHealthNormalized = Mathf.MoveTowards(displayedHealthNormalized, targetHealthNormalized, deltaTime / Mathf.Max(Epsilon, healthSmoothingSeconds));
            displayedShieldNormalized = Mathf.MoveTowards(displayedShieldNormalized, targetShieldNormalized, deltaTime / Mathf.Max(Epsilon, healthSmoothingSeconds));
        }

        ApplyBarFill(healthBarRuntime, healthFillImage, displayedHealthNormalized, targetHealthNormalized);
        ApplyBarFill(shieldBarRuntime, shieldFillImage, displayedShieldNormalized, targetShieldNormalized);
    }

    /// <summary>
    /// Updates the offscreen indicator placement and visibility.
    /// /params bossEntity Active boss entity.
    /// /params hudConfig Baked HUD config containing edge padding.
    /// /returns None.
    /// </summary>
    private void SyncOffscreenIndicator(Entity bossEntity, in EnemyBossHudConfig hudConfig)
    {
        if (offscreenIndicatorRoot == null)
            return;

        Camera camera = ResolveCamera(Time.unscaledTime);

        if (camera == null)
        {
            SetOffscreenIndicatorVisible(false);
            return;
        }

        LocalTransform bossTransform = entityManager.GetComponentData<LocalTransform>(bossEntity);
        Vector3 bossPosition = new Vector3(bossTransform.Position.x, bossTransform.Position.y, bossTransform.Position.z);
        Vector3 viewportPosition = camera.WorldToViewportPoint(bossPosition);
        bool bossIsVisible = viewportPosition.z > 0f &&
                             viewportPosition.x >= 0f &&
                             viewportPosition.x <= 1f &&
                             viewportPosition.y >= 0f &&
                             viewportPosition.y <= 1f;

        if (bossIsVisible)
        {
            SetOffscreenIndicatorVisible(false);
            return;
        }

        float indicatorHalfSizePixels = Mathf.Max(0f, hudConfig.OffscreenIndicatorSizePixels) * 0.5f;
        Vector2 edgePosition = EnemyBossHudPresentationUtility.ResolveEdgePosition(viewportPosition,
                                                                                   Mathf.Max(0f, hudConfig.EdgePaddingPixels) + indicatorHalfSizePixels);
        if (!TryApplyOffscreenIndicatorPosition(edgePosition, camera))
        {
            SetOffscreenIndicatorVisible(false);
            return;
        }

        ApplyIndicatorRotation(edgePosition);
        SetOffscreenIndicatorVisible(true);
    }

    /// <summary>
    /// Applies missing-boss visibility and resets cached boss state.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void HandleMissingBoss()
    {
        cachedBossEntity = Entity.Null;

        if (hideWhenNoBoss)
        {
            ApplyVisibility(false);
            SetOffscreenIndicatorVisible(false);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves missing serialized references from child hierarchy.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ValidateReferences()
    {
        if (visibilityRoot == null)
            visibilityRoot = gameObject;

        if (panelRoot == null)
            panelRoot = transform.Find("Panel") as RectTransform;

        if (bossNameText == null)
            bossNameText = GetComponentInChildren<TMP_Text>(true);

        if (healthFillImage == null)
            healthFillImage = EnemyBossHudPresentationUtility.ResolveImage(transform, "HealthFill");

        if (healthBackgroundImage == null)
            healthBackgroundImage = EnemyBossHudPresentationUtility.ResolveImage(transform, "HealthBackground");

        if (shieldFillImage == null)
            shieldFillImage = EnemyBossHudPresentationUtility.ResolveImage(transform, "ShieldFill");

        if (shieldBackgroundImage == null)
            shieldBackgroundImage = EnemyBossHudPresentationUtility.ResolveImage(transform, "ShieldBackground");

        if (offscreenIndicatorRoot == null)
            offscreenIndicatorRoot = transform.Find("OffscreenIndicator") as RectTransform;

        indicatorParentRect = offscreenIndicatorRoot != null ? offscreenIndicatorRoot.parent as RectTransform : null;
        rootCanvas = offscreenIndicatorRoot != null ? offscreenIndicatorRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();

        if (offscreenIndicatorImage == null && offscreenIndicatorRoot != null)
            offscreenIndicatorImage = offscreenIndicatorRoot.GetComponentInChildren<Image>(true);

        EnemyBossHudPresentationUtility.ConfigureFillImage(healthFillImage);
        EnemyBossHudPresentationUtility.ConfigureFillImage(shieldFillImage);
    }

    /// <summary>
    /// Ensures boss bar presentation settings exist on scenes authored before this HUD mirrored the player bars.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void EnsureBossBarPresentationSettings()
    {
        if (healthBarPresentation == null)
            healthBarPresentation = HUDLiquidBarPresentationSettings.CreateHealthDefaults();

        if (shieldBarPresentation == null)
            shieldBarPresentation = HUDLiquidBarPresentationSettings.CreateShieldDefaults();
    }

    /// <summary>
    /// Builds reusable liquid-bar runtimes once the prefab image bindings are available.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void EnsureBossBarVisualsInitialized()
    {
        EnsureBossBarPresentationSettings();

        if (healthBarRuntime == null && healthFillImage != null)
            healthBarRuntime = HUDLiquidBarRuntime.CreateHealth(healthFillImage, healthBarPresentation);

        if (shieldBarRuntime == null && shieldFillImage != null)
            shieldBarRuntime = HUDLiquidBarRuntime.CreateShield(shieldFillImage, shieldBarPresentation);
    }

    /// <summary>
    /// Applies the authored initial bar states before ECS data is available.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ApplyInitialBarVisualState()
    {
        displayedHealthNormalized = 1f;
        displayedShieldNormalized = 0f;
        ApplyBarFill(healthBarRuntime, healthFillImage, displayedHealthNormalized, displayedHealthNormalized);
        ApplyBarFill(shieldBarRuntime, shieldFillImage, displayedShieldNormalized, displayedShieldNormalized);
    }

    /// <summary>
    /// Releases runtime liquid materials created for boss health and shield bars.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void DisposeBossBarVisuals()
    {
        if (healthBarRuntime != null)
            healthBarRuntime.Dispose();

        if (shieldBarRuntime != null)
            shieldBarRuntime.Dispose();
    }

    /// <summary>
    /// Applies boss name text only when it changed.
    /// /params bossName Boss name to display.
    /// /returns None.
    /// </summary>
    private void SyncBossName(string bossName)
    {
        if (bossNameText == null)
            return;

        string resolvedName = string.IsNullOrWhiteSpace(bossName) ? "Boss" : bossName;

        if (string.Equals(displayedBossName, resolvedName, System.StringComparison.Ordinal))
            return;

        displayedBossName = resolvedName;
        bossNameText.text = resolvedName;
    }

    /// <summary>
    /// Applies sprite and tint for the offscreen indicator.
    /// /params bossEntity Active boss entity.
    /// /params indicatorColor Color resolved from visual preset.
    /// /params sizePixels Square indicator size in screen pixels.
    /// /returns None.
    /// </summary>
    private void ApplyOffscreenIndicatorConfig(Entity bossEntity, Color indicatorColor, float sizePixels)
    {
        if (offscreenIndicatorImage == null)
            return;

        EnemyBossHudPresentationUtility.ApplyImageColor(offscreenIndicatorImage, indicatorColor);
        ApplyOffscreenIndicatorSize(sizePixels);

        if (!entityManager.HasComponent<EnemyBossHudManagedConfig>(bossEntity))
            return;

        EnemyBossHudManagedConfig managedConfig = entityManager.GetComponentObject<EnemyBossHudManagedConfig>(bossEntity);

        if (managedConfig == null || managedConfig.OffscreenIndicatorSprite == null)
            return;

        if (offscreenIndicatorImage.sprite != managedConfig.OffscreenIndicatorSprite)
            offscreenIndicatorImage.sprite = managedConfig.OffscreenIndicatorSprite;
    }

    /// <summary>
    /// Applies square dimensions to the offscreen indicator root and image rect only when needed.
    /// /params sizePixels Requested square indicator size in pixels.
    /// /returns None.
    /// </summary>
    private void ApplyOffscreenIndicatorSize(float sizePixels)
    {
        float resolvedSize = Mathf.Max(1f, sizePixels);
        Vector2 size = new Vector2(resolvedSize, resolvedSize);

        if (offscreenIndicatorRoot != null &&
            Vector2.SqrMagnitude(offscreenIndicatorRoot.sizeDelta - size) > Epsilon)
        {
            offscreenIndicatorRoot.sizeDelta = size;
        }

        if (offscreenIndicatorImage == null)
            return;

        RectTransform imageTransform = offscreenIndicatorImage.rectTransform;

        if (imageTransform == null)
            return;

        if (Vector2.SqrMagnitude(imageTransform.sizeDelta - size) <= Epsilon)
            return;

        imageTransform.sizeDelta = size;
    }

    /// <summary>
    /// Resolves a camera for boss projection without calling Camera.main every frame.
    /// /params currentTime Current unscaled time used to throttle camera lookup.
    /// /returns Active projection camera, or null when unavailable.
    /// </summary>
    private Camera ResolveCamera(float currentTime)
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            return cachedCamera;

        if (currentTime < nextCameraResolveTime)
            return null;

        nextCameraResolveTime = currentTime + CameraResolveIntervalSeconds;
        cachedCamera = Camera.main;

        if (cachedCamera != null)
            return cachedCamera;

        Camera[] cameras = Camera.allCameras;

        for (int index = 0; index < cameras.Length; index++)
        {
            Camera camera = cameras[index];

            if (camera == null || !camera.isActiveAndEnabled)
                continue;

            cachedCamera = camera;
            return cachedCamera;
        }

        return null;
    }

    /// <summary>
    /// Applies the screen-edge position in the correct coordinate space for overlay, camera-space and world-space canvases.
    /// /params screenPosition Screen-space indicator position in pixels.
    /// /params projectionCamera Camera used to project the boss into viewport space.
    /// /returns True when the indicator position could be applied.
    /// </summary>
    private bool TryApplyOffscreenIndicatorPosition(Vector2 screenPosition, Camera projectionCamera)
    {
        if (offscreenIndicatorRoot == null)
            return false;

        RectTransform parentRect = ResolveIndicatorParentRect();

        if (parentRect == null)
        {
            offscreenIndicatorRoot.position = screenPosition;
            return true;
        }

        Camera eventCamera = ResolveCanvasEventCamera(projectionCamera);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        offscreenIndicatorRoot.anchoredPosition = ResolveAnchoredPosition(parentRect, localPoint);
        return true;
    }

    /// <summary>
    /// Converts a parent-local screen point into the anchored position expected by the indicator RectTransform.
    /// /params parentRect Parent rect used as the coordinate frame.
    /// /params localPoint Local point returned by RectTransformUtility.
    /// /returns Anchored position corrected for the indicator anchor reference.
    /// </summary>
    private Vector2 ResolveAnchoredPosition(RectTransform parentRect, Vector2 localPoint)
    {
        Vector2 anchorCenter = (offscreenIndicatorRoot.anchorMin + offscreenIndicatorRoot.anchorMax) * 0.5f;
        Vector2 anchorReference = new Vector2(Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, anchorCenter.x),
                                              Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, anchorCenter.y));
        return localPoint - anchorReference;
    }

    /// <summary>
    /// Resolves and caches the parent RectTransform used as the indicator coordinate space.
    /// /params None.
    /// /returns Parent RectTransform when available.
    /// </summary>
    private RectTransform ResolveIndicatorParentRect()
    {
        if (indicatorParentRect != null)
            return indicatorParentRect;

        if (offscreenIndicatorRoot == null || offscreenIndicatorRoot.parent == null)
            return null;

        indicatorParentRect = offscreenIndicatorRoot.parent as RectTransform;
        return indicatorParentRect;
    }

    /// <summary>
    /// Resolves the event camera required by RectTransformUtility for the active canvas render mode.
    /// /params projectionCamera Camera used as a fallback when the canvas has no explicit world camera.
    /// /returns Null for overlay canvas, otherwise the canvas world camera or projection fallback.
    /// </summary>
    private Camera ResolveCanvasEventCamera(Camera projectionCamera)
    {
        Canvas canvas = ResolveRootCanvas();

        if (canvas == null)
            return projectionCamera;

        switch (canvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                return null;
            case RenderMode.ScreenSpaceCamera:
            case RenderMode.WorldSpace:
                if (canvas.worldCamera != null)
                    return canvas.worldCamera;

                return projectionCamera;
            default:
                return projectionCamera;
        }
    }

    /// <summary>
    /// Resolves and caches the root canvas that owns the boss HUD presentation.
    /// /params None.
    /// /returns Canvas owning this presenter, or null when unavailable.
    /// </summary>
    private Canvas ResolveRootCanvas()
    {
        if (rootCanvas != null)
            return rootCanvas;

        if (offscreenIndicatorRoot != null)
        {
            rootCanvas = offscreenIndicatorRoot.GetComponentInParent<Canvas>();

            if (rootCanvas != null)
                return rootCanvas;
        }

        rootCanvas = GetComponentInParent<Canvas>();
        return rootCanvas;
    }

    /// <summary>
    /// Rotates the offscreen indicator toward the clamped edge direction.
    /// /params edgePosition Current indicator screen position.
    /// /returns None.
    /// </summary>
    private void ApplyIndicatorRotation(Vector2 edgePosition)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 direction = edgePosition - screenCenter;

        if (direction.sqrMagnitude <= Epsilon)
            return;

        float angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        offscreenIndicatorRoot.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
    }

    /// <summary>
    /// Applies one boss bar value through the liquid runtime when available, falling back to a direct fill amount.
    /// /params barRuntime Optional liquid-bar runtime that drives shader and piston state.
    /// /params fallbackFillImage Fill image used when the runtime has not been created yet.
    /// /params displayedNormalizedValue Smoothed normalized value shown by the bar.
    /// /params targetNormalizedValue Raw normalized target used for liquid delta motion.
    /// /returns None.
    /// </summary>
    private void ApplyBarFill(HUDLiquidBarRuntime barRuntime,
                              Image fallbackFillImage,
                              float displayedNormalizedValue,
                              float targetNormalizedValue)
    {
        if (barRuntime != null && barRuntime.IsBound)
        {
            barRuntime.Apply(displayedNormalizedValue, targetNormalizedValue);
            return;
        }

        if (fallbackFillImage == null)
            return;

        fallbackFillImage.fillAmount = Mathf.Clamp01(displayedNormalizedValue);
    }

    /// <summary>
    /// Toggles the boss HUD content without disabling the presenter host.
    /// /params visible Desired visibility state.
    /// /returns None.
    /// </summary>
    private void ApplyVisibility(bool visible)
    {
        GameObject targetObject = ResolveVisibilityTarget();

        if (TryApplyVisibilityToTarget(targetObject, visible))
        {
            return;
        }

        GameObject fallbackObject = ResolvePanelVisibilityTarget();

        if (ReferenceEquals(fallbackObject, targetObject))
        {
            return;
        }

        TryApplyVisibilityToTarget(fallbackObject, visible);
    }

    /// <summary>
    /// Resolves the object that can be safely toggled without disabling this presenter.
    /// /params None.
    /// /returns Content GameObject to toggle, or null when no safe target exists.
    /// </summary>
    private GameObject ResolveVisibilityTarget()
    {
        if (visibilityRoot != null && visibilityRoot != gameObject)
        {
            return visibilityRoot;
        }

        return ResolvePanelVisibilityTarget();
    }

    /// <summary>
    /// Resolves the panel GameObject used as a fallback visibility target.
    /// /params None.
    /// /returns Panel GameObject, or null when the panel reference is unavailable.
    /// </summary>
    private GameObject ResolvePanelVisibilityTarget()
    {
        if (panelRoot != null)
        {
            return panelRoot.gameObject;
        }

        return null;
    }

    /// <summary>
    /// Applies active state to one target while tolerating Unity Missing references from stale prefab overrides.
    /// /params targetObject Candidate object to toggle.
    /// /params visible Desired visibility state.
    /// /returns True when a valid target was handled.
    /// </summary>
    private bool TryApplyVisibilityToTarget(GameObject targetObject, bool visible)
    {
        if (targetObject == null)
        {
            return false;
        }

        try
        {
            if (visibilityInitialized && targetObject.activeSelf == visible)
            {
                return true;
            }

            targetObject.SetActive(visible);
            visibilityInitialized = true;
            return true;
        }
        catch (MissingReferenceException)
        {
            if (ReferenceEquals(targetObject, visibilityRoot))
            {
                visibilityRoot = null;
            }

            return false;
        }
    }

    /// <summary>
    /// Toggles the offscreen indicator root.
    /// /params visible Desired indicator visibility.
    /// /returns None.
    /// </summary>
    private void SetOffscreenIndicatorVisible(bool visible)
    {
        if (offscreenIndicatorRoot == null)
            return;

        GameObject indicatorObject = offscreenIndicatorRoot.gameObject;

        if (indicatorObject.activeSelf == visible)
            return;

        indicatorObject.SetActive(visible);
    }

    #endregion

    #endregion
}
