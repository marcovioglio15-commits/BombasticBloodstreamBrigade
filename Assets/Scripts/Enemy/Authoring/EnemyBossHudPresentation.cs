using TMPro;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the screen-space boss HUD from ECS boss health, transform and visual preset data.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyBossHudPresentation : MonoBehaviour
{
    #region Constants
    private const float DefaultResolveIntervalSeconds = 0.25f;
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
    private GameObject shieldBarRootObject;
    private bool ecsInitialized;
    private bool visibilityInitialized;
    private string displayedBossName;
    private EnemyBossHudAggregateBaseline aggregateBaseline;
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Validates UI references and applies a safe initial hidden state.
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
    /// </summary>
    private void OnDestroy()
    {
        DisposeBossBarVisuals();
    }

    /// <summary>
    /// Keeps serialized settings safe after inspector edits.
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
    /// </summary>
    private void Update()
    {
        EnsureBossBarVisualsInitialized();

        if (!TryInitializeEcsBindings())
        {
            HandleMissingBoss();
            return;
        }

        if (!EnemyBossHudSnapshotUtility.TryResolveSnapshot(entityManager,
                                                            bossQuery,
                                                            ref cachedBossEntity,
                                                            ref nextBossResolveTime,
                                                            Time.unscaledTime,
                                                            bossResolveIntervalSeconds,
                                                            out EnemyBossHudSnapshot bossSnapshot))
        {
            HandleMissingBoss();
            return;
        }

        SyncBossHud(in bossSnapshot, Time.unscaledDeltaTime);
    }
    #endregion

    #region ECS
    /// <summary>
    /// Initializes or refreshes cached ECS world, entity manager and boss query references.
    /// </summary>
    /// <returns>True when ECS bindings are ready.</returns>
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
    /// Clears cached ECS references after the default world becomes unavailable.
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
    /// Synchronizes health, text, colors and offscreen indicator for the current boss aggregate.
    /// </summary>
    /// <param name="bossSnapshot">Aggregated active boss HUD snapshot.</param>
    /// <param name="deltaTime">Unscaled frame delta used for smoothing.</param>
    private void SyncBossHud(in EnemyBossHudSnapshot bossSnapshot, float deltaTime)
    {
        EnemyBossHudConfig hudConfig = bossSnapshot.PrimaryConfig;

        if (hudConfig.Enabled == 0)
        {
            HandleMissingBoss();
            return;
        }

        ApplyVisibility(true);
        SyncConfig(in bossSnapshot);
        SyncBars(in bossSnapshot, deltaTime);
        EnemyBossHudOffscreenIndicatorUtility.Sync(entityManager,
                                                   bossSnapshot.PrimaryEntity,
                                                   in hudConfig,
                                                   targetCamera,
                                                   offscreenIndicatorRoot,
                                                   ref cachedCamera,
                                                   ref nextCameraResolveTime,
                                                   ref indicatorParentRect,
                                                   ref rootCanvas);
    }

    /// <summary>
    /// Applies boss HUD configuration baked from the selected visual preset.
    /// </summary>
    /// <param name="bossSnapshot">Aggregated snapshot whose primary boss supplies labels, colors and managed sprite data.</param>
    private void SyncConfig(in EnemyBossHudSnapshot bossSnapshot)
    {
        EnemyBossHudConfig hudConfig = bossSnapshot.PrimaryConfig;
        SyncBossName(ResolveBossDisplayName(in bossSnapshot));
        EnemyBossHudPresentationUtility.ApplyImageColor(healthFillImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.HealthFillColor));
        EnemyBossHudPresentationUtility.ApplyImageColor(healthBackgroundImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.HealthBackgroundColor));
        EnemyBossHudPresentationUtility.ApplyImageColor(shieldFillImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.ShieldFillColor));
        EnemyBossHudPresentationUtility.ApplyImageColor(shieldBackgroundImage, EnemyBossHudPresentationUtility.ToColor(hudConfig.ShieldBackgroundColor));
        EnemyBossHudOffscreenIndicatorUtility.ApplyConfig(entityManager,
                                                          bossSnapshot.PrimaryEntity,
                                                          offscreenIndicatorRoot,
                                                          offscreenIndicatorImage,
                                                          EnemyBossHudPresentationUtility.ToColor(hudConfig.OffscreenIndicatorColor),
                                                          hudConfig.OffscreenIndicatorSizePixels);
    }

    /// <summary>
    /// Updates the health and shield fill values from summed active boss ECS health data.
    /// </summary>
    /// <param name="bossSnapshot">Aggregated boss health and shield values.</param>
    /// <param name="deltaTime">Unscaled frame delta used for smoothing.</param>
    private void SyncBars(in EnemyBossHudSnapshot bossSnapshot, float deltaTime)
    {
        float targetHealthNormalized = 0f;
        float targetShieldNormalized = 0f;
        float stableMaxHealth = aggregateBaseline.ResolveHealthMax(bossSnapshot.MaxHealth);
        float stableMaxShield = aggregateBaseline.ResolveShieldMax(bossSnapshot.MaxShield);
        bool hasShield = bossSnapshot.MaxShield > Epsilon && stableMaxShield > Epsilon;

        if (stableMaxHealth > Epsilon)
            targetHealthNormalized = Mathf.Clamp01(bossSnapshot.CurrentHealth / stableMaxHealth);

        if (hasShield)
            targetShieldNormalized = Mathf.Clamp01(bossSnapshot.CurrentShield / stableMaxShield);

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
        HUDBarVisibilityUtility.SetVisible(shieldBarRootObject, hasShield);

        if (hasShield)
            ApplyBarFill(shieldBarRuntime, shieldFillImage, displayedShieldNormalized, targetShieldNormalized);
        else
            ApplyBarFill(shieldBarRuntime, shieldFillImage, 0f, 0f);
    }

    /// <summary>
    /// Applies missing-boss visibility and resets cached boss state.
    /// </summary>
    private void HandleMissingBoss()
    {
        cachedBossEntity = Entity.Null;
        aggregateBaseline.Reset();
        HUDBarVisibilityUtility.SetVisible(shieldBarRootObject, false);

        if (hideWhenNoBoss)
        {
            ApplyVisibility(false);
            EnemyBossHudOffscreenIndicatorUtility.SetVisible(offscreenIndicatorRoot, false);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves missing serialized references from child hierarchy.
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

        shieldBarRootObject = HUDBarVisibilityUtility.ResolveRootObject(shieldBackgroundImage, shieldFillImage);
        EnemyBossHudOffscreenIndicatorUtility.ResolveReferences(transform,
                                                               ref offscreenIndicatorRoot,
                                                               ref offscreenIndicatorImage,
                                                               ref indicatorParentRect,
                                                               ref rootCanvas);

        EnemyBossHudPresentationUtility.ConfigureFillImage(healthFillImage);
        EnemyBossHudPresentationUtility.ConfigureFillImage(shieldFillImage);
    }

    /// <summary>
    /// Ensures boss bar presentation settings exist on scenes authored before this HUD mirrored the player bars.
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
    /// </summary>
    private void ApplyInitialBarVisualState()
    {
        displayedHealthNormalized = 1f;
        displayedShieldNormalized = 0f;
        HUDBarVisibilityUtility.SetVisible(shieldBarRootObject, false);
        ApplyBarFill(healthBarRuntime, healthFillImage, displayedHealthNormalized, displayedHealthNormalized);
        ApplyBarFill(shieldBarRuntime, shieldFillImage, displayedShieldNormalized, displayedShieldNormalized);
    }

    /// <summary>
    /// Releases runtime liquid materials created for boss health and shield bars.
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
    /// </summary>
    /// <param name="bossName">Boss name to display.</param>
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
    /// Resolves the label shown by the boss HUD, including a compact count suffix when multiple bosses contribute to the bars.
    /// </summary>
    /// <param name="bossSnapshot">Aggregated boss HUD snapshot.</param>
    /// <returns>Display name for the active boss aggregate.</returns>
    private static string ResolveBossDisplayName(in EnemyBossHudSnapshot bossSnapshot)
    {
        string primaryName = bossSnapshot.PrimaryConfig.DisplayName.ToString();
        string resolvedName = string.IsNullOrWhiteSpace(primaryName) ? "Boss" : primaryName;

        if (bossSnapshot.BossCount <= 1)
            return resolvedName;

        return string.Format("{0} x{1}", resolvedName, bossSnapshot.BossCount);
    }

    /// <summary>
    /// Applies one boss bar value through the liquid runtime when available, falling back to a direct fill amount.
    /// </summary>
    /// <param name="barRuntime">Optional liquid-bar runtime that drives shader and piston state.</param>
    /// <param name="fallbackFillImage">Fill image used when the runtime has not been created yet.</param>
    /// <param name="displayedNormalizedValue">Smoothed normalized value shown by the bar.</param>
    /// <param name="targetNormalizedValue">Raw normalized target used for liquid delta motion.</param>
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
    /// </summary>
    /// <param name="visible">Desired visibility state.</param>
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
    /// </summary>
    /// <returns>Content GameObject to toggle, or null when no safe target exists.</returns>
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
    /// </summary>
    /// <returns>Panel GameObject, or null when the panel reference is unavailable.</returns>
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
    /// </summary>
    /// <param name="targetObject">Candidate object to toggle.</param>
    /// <param name="visible">Desired visibility state.</param>
    /// <returns>True when a valid target was handled.</returns>
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

    #endregion

    #endregion
}
