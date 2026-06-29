using TMPro;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drives the screen-space boss HUD from ECS boss health, transform and visual preset data.
/// </summary>
[ExecuteAlways]
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

    [Tooltip("Rect transform hosting the mirrored boss portrait image.")]
    [SerializeField] private RectTransform portraitRoot;

    [Tooltip("Image used to render the mirrored boss portrait sprite from the Enemy Visual Preset.")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Preauthored procedural syringe view representing boss health.")]
    [SerializeField] private PlayerSyringeBarView healthSyringeBar;

    [Tooltip("Preauthored procedural syringe view representing boss shield.")]
    [SerializeField] private PlayerSyringeBarView shieldSyringeBar;

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

    #if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Enemy Visual Preset used to render the boss syringe bars outside Play Mode through the same configuration builder used by the boss baker.")]
    [SerializeField] private EnemyVisualPreset editorPreviewVisualPreset;

    [Tooltip("Current boss health shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewHealthValue = 100f;

    [Tooltip("Maximum boss health shown only by the Edit Mode preview and used to resolve syringe length and graduations.")]
    [Min(0.0001f)]
    [SerializeField] private float editorPreviewHealthMaximum = 100f;

    [Tooltip("Current boss shield shown only by the Edit Mode preview.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewShieldValue;

    [Tooltip("Maximum boss shield shown only by the Edit Mode preview. A zero value hides the shield when the selected preset enables that policy.")]
    [Min(0f)]
    [SerializeField] private float editorPreviewShieldMaximum;
    #endif
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
    private bool ecsInitialized;
    private bool visibilityInitialized;
    private string displayedBossName;
    private EnemyBossHudAggregateBaseline aggregateBaseline;
    private Entity cachedBarConfigBossEntity = Entity.Null;

    #if UNITY_EDITOR
    private bool editorPreviewQueued;
    #endif
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Validates UI references and applies a safe initial hidden state.
    /// </summary>
    private void Awake()
    {
        ValidateReferences();
        InitializeBossSyringeViews();

        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorPreview();
            return;
        }
        #endif

        ApplyVisibility(!hideWhenNoBoss);
        ApplyInitialBarVisualState();
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Queues an Edit Mode preview refresh and subscribes to referenced enemy preset changes.
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        EditorApplication.projectChanged -= HandleEditorProjectChanged;
        EditorApplication.projectChanged += HandleEditorProjectChanged;
        QueueEditorPreview();
    }

    /// <summary>
    /// Releases Edit Mode preview materials and editor callbacks without touching Play Mode ownership.
    /// </summary>
    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleEditorProjectChanged;
        EditorApplication.delayCall -= ApplyQueuedEditorPreview;
        editorPreviewQueued = false;

        if (!Application.isPlaying)
            DisposeBossSyringeViews();
    }
    #endif

    /// <summary>
    /// Releases runtime material instances created by the boss syringe bars.
    /// </summary>
    private void OnDestroy()
    {
        DisposeBossSyringeViews();
    }

    /// <summary>
    /// Keeps serialized settings safe after inspector edits.
    /// </summary>
    private void OnValidate()
    {
        ValidateReferences();

        if (healthSmoothingSeconds < 0f)
            healthSmoothingSeconds = 0f;

        if (bossResolveIntervalSeconds < 0.05f)
            bossResolveIntervalSeconds = 0.05f;

        #if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueEditorPreview();
        #endif
    }

    /// <summary>
    /// Updates the boss HUD from the cached ECS boss entity.
    /// </summary>
    private void Update()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
        #endif

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

    #if UNITY_EDITOR
    #region Editor Preview
    /// <summary>
    /// Rebuilds the Edit Mode boss syringe preview through the same visual settings path used by enemy baking.
    /// </summary>
    public void RefreshEditorPreview()
    {
        if (Application.isPlaying || !isActiveAndEnabled)
            return;

        ValidateReferences();
        InitializeBossSyringeViews();

        EnemyBossVisualUiSettings bossUi = editorPreviewVisualPreset != null ? editorPreviewVisualPreset.BossUi : null;
        bool showBars = EnemyBossHudEditorPreviewUtility.ShouldShowBars(bossUi);

        ApplyVisibility(true);
        ApplyHealthBarVisibility(showBars);
        EnemyBossHudEditorPreviewUtility.Refresh(editorPreviewVisualPreset,
                                                 bossUi,
                                                 healthSyringeBar,
                                                 shieldSyringeBar,
                                                 portraitRoot,
                                                 portraitImage,
                                                 offscreenIndicatorRoot,
                                                 bossNameText,
                                                 editorPreviewHealthValue,
                                                 editorPreviewHealthMaximum,
                                                 editorPreviewShieldValue,
                                                 editorPreviewShieldMaximum);
    }

    /// <summary>
    /// Schedules one coalesced preview rebuild after an inspector or project asset change.
    /// </summary>
    private void QueueEditorPreview()
    {
        if (editorPreviewQueued || Application.isPlaying)
            return;

        editorPreviewQueued = true;
        EditorApplication.delayCall += ApplyQueuedEditorPreview;
    }

    /// <summary>
    /// Applies the queued preview only while this scene or prefab-stage instance remains valid.
    /// </summary>
    private void ApplyQueuedEditorPreview()
    {
        EditorApplication.delayCall -= ApplyQueuedEditorPreview;
        editorPreviewQueued = false;

        if (this == null)
            return;

        RefreshEditorPreview();
    }

    /// <summary>
    /// Queues a preview rebuild after any referenced project asset is imported or modified.
    /// </summary>
    private void HandleEditorProjectChanged()
    {
        QueueEditorPreview();
    }
    #endregion
    #endif

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

        bool showHealthBar = hudConfig.ShowHealthBar != 0;
        bool showOffscreenIndicator = hudConfig.ShowOffscreenIndicator != 0;
        bool showPortrait = hudConfig.ShowPortrait != 0;

        ApplyVisibility(true);
        ApplyHealthBarVisibility(showHealthBar);

        if (showPortrait)
            SyncBossPortraitConfig(bossSnapshot.PrimaryEntity, in hudConfig);
        else
            SetBossPortraitVisible(false);

        if (showHealthBar)
        {
            SyncHealthBarConfig(in bossSnapshot);
            SyncBars(in bossSnapshot, deltaTime);
        }
        else
        {
            if (shieldSyringeBar != null)
                shieldSyringeBar.HandleMissing(true);
        }

        if (showOffscreenIndicator)
        {
            SyncOffscreenIndicatorConfig(in bossSnapshot);
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
        else
        {
            EnemyBossHudOffscreenIndicatorUtility.SetVisible(offscreenIndicatorRoot, false);
        }
    }

    /// <summary>
    /// Applies boss portrait sprite, tint, size, and mirrored orientation from ECS and managed visual data.
    /// </summary>
    /// <param name="bossEntity">Primary boss entity supplying managed portrait sprite data.</param>
    /// <param name="hudConfig">Unmanaged boss HUD configuration baked from the visual preset.</param>
    private void SyncBossPortraitConfig(Entity bossEntity, in EnemyBossHudConfig hudConfig)
    {
        if (portraitRoot == null || portraitImage == null)
            return;

        if (!entityManager.HasComponent<EnemyBossHudManagedConfig>(bossEntity))
        {
            SetBossPortraitVisible(false);
            return;
        }

        EnemyBossHudManagedConfig managedConfig = entityManager.GetComponentObject<EnemyBossHudManagedConfig>(bossEntity);

        if (managedConfig == null || managedConfig.PortraitSprite == null)
        {
            SetBossPortraitVisible(false);
            return;
        }

        ApplyPortraitMirrorTransform();
        ApplyPortraitSize(hudConfig.PortraitSizePixels);
        EnemyBossHudPresentationUtility.ApplyImageColor(portraitImage,
                                                        EnemyBossHudPresentationUtility.ToColor(hudConfig.PortraitColor));

        if (portraitImage.sprite != managedConfig.PortraitSprite)
            portraitImage.sprite = managedConfig.PortraitSprite;

        portraitImage.enabled = true;
        SetBossPortraitVisible(true);
    }

    /// <summary>
    /// Applies boss bar configuration baked from the selected visual preset.
    /// </summary>
    /// <param name="bossSnapshot">Aggregated snapshot whose primary boss supplies labels, colors and managed sprite data.</param>
    private void SyncHealthBarConfig(in EnemyBossHudSnapshot bossSnapshot)
    {
        EnemyBossHudConfig hudConfig = bossSnapshot.PrimaryConfig;
        SyncBossName(ResolveBossDisplayName(in bossSnapshot));

        if (cachedBarConfigBossEntity == bossSnapshot.PrimaryEntity)
            return;

        cachedBarConfigBossEntity = bossSnapshot.PrimaryEntity;
        TMP_FontAsset font = hudConfig.BarsVisualConfig.FontAsset.Value;

        if (healthSyringeBar != null)
            healthSyringeBar.ApplyConfiguration(in hudConfig.BarsVisualConfig, in hudConfig.BarsVisualConfig.Health, font);

        if (shieldSyringeBar != null)
            shieldSyringeBar.ApplyConfiguration(in hudConfig.BarsVisualConfig, in hudConfig.BarsVisualConfig.Shield, font);
    }

    /// <summary>
    /// Applies boss offscreen-indicator configuration baked from the selected visual preset.
    /// </summary>
    /// <param name="bossSnapshot">Aggregated snapshot whose primary boss supplies indicator sprite and tint data.</param>
    private void SyncOffscreenIndicatorConfig(in EnemyBossHudSnapshot bossSnapshot)
    {
        EnemyBossHudConfig hudConfig = bossSnapshot.PrimaryConfig;
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

        if (healthSyringeBar != null)
            healthSyringeBar.UpdateValue(displayedHealthNormalized * stableMaxHealth, stableMaxHealth, 0f, true);

        if (shieldSyringeBar == null)
            return;

        if (hasShield)
            shieldSyringeBar.UpdateValue(displayedShieldNormalized * stableMaxShield, stableMaxShield, 0f, true);
        else
            shieldSyringeBar.HandleMissing(true);
    }

    /// <summary>
    /// Applies missing-boss visibility and resets cached boss state.
    /// </summary>
    private void HandleMissingBoss()
    {
        cachedBossEntity = Entity.Null;
        cachedBarConfigBossEntity = Entity.Null;
        aggregateBaseline.Reset();

        if (healthSyringeBar != null)
            healthSyringeBar.HandleMissing(true);

        if (shieldSyringeBar != null)
            shieldSyringeBar.HandleMissing(true);

        SetBossPortraitVisible(false);
        EnemyBossHudOffscreenIndicatorUtility.SetVisible(offscreenIndicatorRoot, false);

        if (hideWhenNoBoss)
        {
            ApplyVisibility(false);
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

        if (portraitImage == null)
            portraitImage = EnemyBossHudPresentationUtility.ResolveImage(transform, "BossPortraitImage");

        if (portraitRoot == null && portraitImage != null)
        {
            portraitRoot = portraitImage.transform.parent as RectTransform;

            if (portraitRoot == null)
                portraitRoot = portraitImage.rectTransform;
        }

        if (portraitRoot == null)
            portraitRoot = transform.Find("BossPortraitContainer") as RectTransform;

        if (portraitImage == null && portraitRoot != null)
            portraitImage = portraitRoot.GetComponentInChildren<Image>(true);

        if (healthSyringeBar == null)
            healthSyringeBar = EnemyBossHudPresentationUtility.ResolveComponent<PlayerSyringeBarView>(transform, "BossHealthSyringe");

        if (shieldSyringeBar == null)
            shieldSyringeBar = EnemyBossHudPresentationUtility.ResolveComponent<PlayerSyringeBarView>(transform, "BossShieldSyringe");

        EnemyBossHudOffscreenIndicatorUtility.ResolveReferences(transform,
                                                               ref offscreenIndicatorRoot,
                                                               ref offscreenIndicatorImage,
                                                               ref indicatorParentRect,
                                                               ref rootCanvas);
    }

    /// <summary>
    /// Applies the authored initial bar states before ECS data is available.
    /// </summary>
    private void ApplyInitialBarVisualState()
    {
        displayedHealthNormalized = 1f;
        displayedShieldNormalized = 0f;

        if (healthSyringeBar != null)
            healthSyringeBar.HandleMissing(false);

        if (shieldSyringeBar != null)
            shieldSyringeBar.HandleMissing(true);

        SetBossPortraitVisible(false);
    }

    /// <summary>
    /// Initializes preauthored boss syringe views without creating UI GameObjects.
    /// </summary>
    private void InitializeBossSyringeViews()
    {
        if (healthSyringeBar != null)
            healthSyringeBar.Initialize();

        if (shieldSyringeBar != null)
            shieldSyringeBar.Initialize();
    }

    /// <summary>
    /// Releases persistent material instances owned by boss syringe views.
    /// </summary>
    private void DisposeBossSyringeViews()
    {
        if (healthSyringeBar != null)
            healthSyringeBar.Dispose();

        if (shieldSyringeBar != null)
            shieldSyringeBar.Dispose();
    }

    /// <summary>
    /// Applies the authored mirrored boss portrait transform without using negative scale.
    /// </summary>
    private void ApplyPortraitMirrorTransform()
    {
        if (portraitRoot == null)
            return;

        portraitRoot.localScale = Vector3.one;
        portraitRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    /// <summary>
    /// Applies the configured square portrait size to the preauthored RectTransform.
    /// </summary>
    /// <param name="sizePixels">Target square size in UI pixels.</param>
    private void ApplyPortraitSize(float sizePixels)
    {
        if (portraitRoot == null)
            return;

        float resolvedSize = Mathf.Max(1f, sizePixels);
        portraitRoot.sizeDelta = new Vector2(resolvedSize, resolvedSize);
    }

    /// <summary>
    /// Shows or hides the boss portrait root without touching the rest of the boss HUD.
    /// </summary>
    /// <param name="visible">Desired portrait visibility.</param>
    private void SetBossPortraitVisible(bool visible)
    {
        if (portraitRoot == null)
            return;

        GameObject portraitObject = portraitRoot.gameObject;

        if (portraitObject.activeSelf != visible)
            portraitObject.SetActive(visible);
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
    /// Toggles only the mirrored health and shield bar panel while keeping the presenter active for offscreen-only setups.
    /// </summary>
    /// <param name="visible">Desired boss bar panel visibility.</param>
    private void ApplyHealthBarVisibility(bool visible)
    {
        GameObject panelObject = ResolvePanelVisibilityTarget();

        if (panelObject == null)
            return;

        TryApplyVisibilityToTarget(panelObject, visible);
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
