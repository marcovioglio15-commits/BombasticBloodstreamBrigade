using System.Collections;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the preauthored collapsible power-up inventory and player-stat rows from ECS-authoritative data.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDPowerUpSummarySection : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Panel")]
    [Tooltip("RectTransform moved between expanded and collapsed screen-edge positions.")]
    [SerializeField] private RectTransform panelRoot;

    [Tooltip("RectTransform containing all panel content except the persistent toggle handle.")]
    [SerializeField] private RectTransform contentRoot;

    [Tooltip("Upper content area whose normalized height is controlled by the summary preset.")]
    [SerializeField] private RectTransform powerUpAreaRoot;

    [Tooltip("Lower content area occupying the remaining height below the power-up area.")]
    [SerializeField] private RectTransform statisticsAreaRoot;

    [Tooltip("Background image receiving the sprite and tint from the inline HUD summary settings.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Preauthored button that expands or collapses the summary panel.")]
    [SerializeField] private Button toggleButton;

    [Tooltip("Image rendered by the expand and collapse button.")]
    [SerializeField] private Image toggleImage;

    [Header("Power-Up Columns")]
    [Tooltip("Root containing the Active title and its preauthored icon grid.")]
    [SerializeField] private RectTransform activeColumnRoot;

    [Tooltip("Root containing the Passive title and its preauthored icon grid.")]
    [SerializeField] private RectTransform passiveColumnRoot;

    [Tooltip("Grid layout owning the preauthored active power-up slots.")]
    [SerializeField] private GridLayoutGroup activeGrid;

    [Tooltip("Grid layout owning the preauthored passive power-up slots.")]
    [SerializeField] private GridLayoutGroup passiveGrid;

    [Tooltip("Horizontal layout controlling active/passive column spacing and separator placement.")]
    [SerializeField] private HorizontalLayoutGroup powerUpColumnsLayout;

    [Tooltip("Text displayed above the active power-up grid.")]
    [SerializeField] private TMP_Text activeTitleText;

    [Tooltip("Text displayed above the passive power-up grid.")]
    [SerializeField] private TMP_Text passiveTitleText;

    [Tooltip("Vertical graphic separating the active and passive power-up columns.")]
    [SerializeField] private Image powerUpColumnSeparator;

    [Tooltip("Preauthored active power-up icon slots; runtime never instantiates additional UI.")]
    [SerializeField] private HUDPowerUpSummaryIconView[] activeIconViews;

    [Tooltip("Preauthored passive power-up icon slots; runtime never instantiates additional UI.")]
    [SerializeField] private HUDPowerUpSummaryIconView[] passiveIconViews;

    [Header("Statistics")]
    [Tooltip("Text displayed above the ordered player-stat rows.")]
    [SerializeField] private TMP_Text statisticsTitleText;

    [Tooltip("Horizontal graphic separating the power-up area from player-stat rows.")]
    [SerializeField] private Image statisticsSeparator;

    [Tooltip("Preauthored statistic rows; unused entries remain disabled without runtime UI creation.")]
    [SerializeField] private HUDPowerUpSummaryStatisticRowView[] statisticRows;
    #endregion

    #region Runtime
    private Coroutine slideCoroutine;
    private InputAction toggleAction;
    private Entity configEntity;
    private GamePowerUpSummaryRuntimeConfig config;
    private bool configApplied;
    private bool iconsInitialized;
    private bool isExpanded;
    private uint powerUpCatalogHash;
    private float nextRefreshTime;
    private bool inputLifecycleSubscribed;
    #endregion

    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Clears unused slots before ECS data and its configured toggle paths become available.
    /// </summary>
    public void Initialize()
    {
        SubscribeInputLifecycle();
        HUDPowerUpSummaryViewPoolUtility.HideAllIconViews(activeIconViews);
        HUDPowerUpSummaryViewPoolUtility.HideAllIconViews(passiveIconViews);
        HUDPowerUpSummaryViewPoolUtility.HideUnusedStatisticRows(statisticRows, 0);
    }

    /// <summary>
    /// Releases callbacks and any in-progress panel transition.
    /// </summary>
    public void Dispose()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleExpanded);

        ReleaseToggleAction();
        UnsubscribeInputLifecycle();
        StopSlide();
    }

    /// <summary>
    /// Updates visible summary data at the authored interval while preserving responsive manual panel motion.
    /// </summary>
    /// <param name="entityManager">Entity manager owning HUD config and player state.</param>
    /// <param name="playerEntity">Current authoritative player entity.</param>
    public void UpdateSection(EntityManager entityManager, Entity playerEntity)
    {
        if (!configApplied && !TryApplyConfig(entityManager))
            return;

        if (config.Enabled == 0)
        {
            SetPanelActive(false);
            return;
        }

        SetPanelActive(true);
        float currentTime = Time.unscaledTime;

        if (currentTime < nextRefreshTime)
            return;

        nextRefreshTime = currentTime + math.max(0.02f, config.StatisticRefreshIntervalSeconds);
        RefreshPowerUps(entityManager, playerEntity);
        RefreshStatistics(entityManager, playerEntity);
    }

    /// <summary>
    /// Applies the authored missing-player policy without performing entity queries from this section.
    /// </summary>
    public void HandleMissingPlayer()
    {
        if (!configApplied || config.HideWhenPlayerMissing != 0)
            SetPanelActive(false);
    }
    #endregion

    #region Config
    /// <summary>
    /// Resolves the baked summary singleton once and applies all static presentation values to preauthored UI.
    /// </summary>
    /// <param name="entityManager">Entity manager expected to contain one summary config singleton.</param>
    /// <returns>True when the summary config and buffers were found.</returns>
    private bool TryApplyConfig(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GamePowerUpSummaryRuntimeConfig>(),
                                                             ComponentType.ReadOnly<GamePowerUpSummaryStatisticElement>());

        if (query.CalculateEntityCount() != 1)
        {
            query.Dispose();
            return false;
        }

        configEntity = query.GetSingletonEntity();
        query.Dispose();
        config = entityManager.GetComponentData<GamePowerUpSummaryRuntimeConfig>(configEntity);
        DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticDefinitions =
            entityManager.GetBuffer<GamePowerUpSummaryStatisticElement>(configEntity, true);
        ApplyStaticPresentation(statisticDefinitions);
        ConfigureTogglePaths();
        configApplied = true;
        isExpanded = config.StartsExpanded != 0;
        ApplyPanelPosition(isExpanded);
        SetPanelActive(config.Enabled != 0);
        return true;
    }

    /// <summary>
    /// Applies layout, shared styles, titles, separator state, and statistic-row styles once per baked config.
    /// </summary>
    /// <param name="statisticDefinitions">Ordered baked statistic definitions.</param>
    private void ApplyStaticPresentation(DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticDefinitions)
    {
        ApplyPanelLayout();
        ApplyPanelStyle();
        ApplyGridStyle(activeGrid);
        ApplyGridStyle(passiveGrid);
        ApplyTitleStyle(activeTitleText, config.ActiveTitle.ToString());
        ApplyTitleStyle(passiveTitleText, config.PassiveTitle.ToString());
        ApplyTitleStyle(statisticsTitleText, config.StatisticsTitle.ToString());
        ApplySeparatorStyle(powerUpColumnSeparator,
                            ShowsActivePowerUps() &&
                            ShowsPassivePowerUps() &&
                            config.ShowPowerUpColumnSeparator != 0,
                            false);
        ApplySeparatorStyle(statisticsSeparator, config.ShowStatisticsSeparator != 0, true);
        HUDPowerUpSummaryViewPoolUtility.ApplyIconStyles(activeIconViews, in config);
        HUDPowerUpSummaryViewPoolUtility.ApplyIconStyles(passiveIconViews, in config);
        ApplyPowerUpColumnVisibility(0, 0);

        int visibleStatisticCount = math.min(statisticDefinitions.Length, statisticRows != null ? statisticRows.Length : 0);

        for (int statisticIndex = 0; statisticIndex < visibleStatisticCount; statisticIndex++)
        {
            HUDPowerUpSummaryStatisticRowView row = statisticRows[statisticIndex];

            if (row != null)
                row.ApplyStyle(statisticDefinitions[statisticIndex]);
        }

        HUDPowerUpSummaryViewPoolUtility.HideUnusedStatisticRows(statisticRows, visibleStatisticCount);
    }

    /// <summary>
    /// Applies edge anchoring, width, padding, column order, and section spacing from the baked preset.
    /// </summary>
    private void ApplyPanelLayout()
    {
        if (panelRoot != null)
        {
            bool useRightEdge = config.PanelSide == GameHudSummaryPanelSide.Right;
            panelRoot.anchorMin = new Vector2(useRightEdge ? 1f : 0f, 0.5f);
            panelRoot.anchorMax = panelRoot.anchorMin;
            panelRoot.pivot = new Vector2(useRightEdge ? 1f : 0f, 0.5f);
            panelRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, config.ExpandedWidth);
        }

        if (contentRoot != null)
        {
            contentRoot.offsetMin = new Vector2(config.ContentPadding, config.ContentPadding);
            contentRoot.offsetMax = new Vector2(-config.ContentPadding, -config.ContentPadding);
        }

        ApplyContentAreaLayout();

        bool showsBothColumns = ShowsActivePowerUps() && ShowsPassivePowerUps();

        if (powerUpColumnsLayout != null)
            powerUpColumnsLayout.spacing = showsBothColumns ? config.PowerUpColumnSpacing : 0f;

        if (showsBothColumns && activeColumnRoot != null && passiveColumnRoot != null)
        {
            bool activeFirst = config.PowerUpOrder == GameHudSummaryPowerUpOrder.ActiveFirst;
            activeColumnRoot.SetSiblingIndex(activeFirst ? 0 : 2);
            passiveColumnRoot.SetSiblingIndex(activeFirst ? 2 : 0);
        }

        ApplyToggleLayout();
    }

    /// <summary>
    /// Splits the content into upper power-up and lower statistic areas using the baked ratio and spacing.
    /// </summary>
    private void ApplyContentAreaLayout()
    {
        float split = math.saturate(1f - config.PowerUpAreaHeightNormalized);
        float halfSpacing = config.SectionSpacing * 0.5f;

        if (powerUpAreaRoot != null)
        {
            powerUpAreaRoot.anchorMin = new Vector2(0f, split);
            powerUpAreaRoot.anchorMax = Vector2.one;
            powerUpAreaRoot.offsetMin = new Vector2(0f, halfSpacing);
            powerUpAreaRoot.offsetMax = Vector2.zero;
        }

        if (statisticsAreaRoot != null)
        {
            statisticsAreaRoot.anchorMin = Vector2.zero;
            statisticsAreaRoot.anchorMax = new Vector2(1f, split);
            statisticsAreaRoot.offsetMin = Vector2.zero;
            statisticsAreaRoot.offsetMax = new Vector2(0f, -halfSpacing);
        }
    }

    /// <summary>
    /// Keeps the authored handle on the inward edge so it remains visible after the panel collapses.
    /// </summary>
    private void ApplyToggleLayout()
    {
        if (toggleButton == null)
            return;

        RectTransform toggleRect = toggleButton.transform as RectTransform;

        if (toggleRect == null)
            return;

        bool useRightEdge = config.PanelSide == GameHudSummaryPanelSide.Right;
        float anchor = useRightEdge ? 0f : 1f;
        toggleRect.anchorMin = new Vector2(anchor, 0.5f);
        toggleRect.anchorMax = toggleRect.anchorMin;
        toggleRect.pivot = new Vector2(anchor, 0.5f);
        toggleRect.anchoredPosition = Vector2.zero;
        toggleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, config.CollapsedHandleWidth);
    }

    /// <summary>
    /// Applies background and toggle assets plus their authored tints.
    /// </summary>
    private void ApplyPanelStyle()
    {
        if (backgroundImage != null)
        {
            backgroundImage.sprite = config.BackgroundSprite.Value;
            backgroundImage.color = HUDPowerUpSummaryPresentationUtility.ToColor(config.BackgroundTint);
        }

        if (toggleImage != null)
        {
            toggleImage.sprite = config.ToggleSprite.Value;
            toggleImage.color = HUDPowerUpSummaryPresentationUtility.ToColor(config.ToggleTint);
        }
    }

    /// <summary>
    /// Applies square cell dimensions and spacing to one preauthored power-up grid.
    /// </summary>
    /// <param name="grid">Grid layout to update.</param>
    private void ApplyGridStyle(GridLayoutGroup grid)
    {
        if (grid == null)
            return;

        grid.cellSize = new Vector2(config.IconSize, config.IconSize);
        grid.spacing = new Vector2(config.IconSpacing, config.IconSpacing);
    }

    /// <summary>
    /// Applies shared text and font styling to one summary section title.
    /// </summary>
    /// <param name="titleText">Preauthored title text to update.</param>
    /// <param name="text">Baked title content.</param>
    private void ApplyTitleStyle(TMP_Text titleText, string text)
    {
        if (titleText == null)
            return;

        titleText.text = text;
        titleText.fontSize = config.TitleFontSize;
        titleText.color = HUDPowerUpSummaryPresentationUtility.ToColor(config.TitleColor);

        if (config.TitleFont.Value != null)
            titleText.font = config.TitleFont.Value;
    }

    /// <summary>
    /// Applies authored visibility, color, and thickness to one separator graphic.
    /// </summary>
    /// <param name="separator">Separator image to update.</param>
    /// <param name="visible">True when the separator should render.</param>
    /// <param name="horizontal">True for a horizontal separator, false for vertical.</param>
    private void ApplySeparatorStyle(Image separator, bool visible, bool horizontal)
    {
        if (separator == null)
            return;

        separator.gameObject.SetActive(visible);
        separator.color = HUDPowerUpSummaryPresentationUtility.ToColor(config.SeparatorColor);
        RectTransform separatorRect = separator.rectTransform;

        if (horizontal)
            separatorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, config.SeparatorThickness);
        else
            separatorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, config.SeparatorThickness);
    }
    #endregion

    #region Input
    /// <summary>
    /// Configures the independent pointer and Input Action toggle paths from the baked HUD settings.
    /// </summary>
    private void ConfigureTogglePaths()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleExpanded);
            bool clickToggleEnabled = config.EnableClickToggle != 0;
            toggleButton.gameObject.SetActive(clickToggleEnabled);

            if (clickToggleEnabled)
                toggleButton.onClick.AddListener(ToggleExpanded);
        }

        BindToggleAction();
    }

    /// <summary>
    /// Rebinds the configured summary toggle after the shared player Input Action asset is replaced between rooms.
    /// </summary>
    private void BindToggleAction()
    {
        ReleaseToggleAction();

        if (config.EnableInputToggle == 0)
            return;

        toggleAction = PlayerInputRuntime.ResolveRuntimeAction(config.ToggleActionId.ToString(),
                                                               "Player/PowerUpSummaryToggle");

        if (toggleAction != null)
            toggleAction.performed += HandleToggleActionPerformed;
    }

    /// <summary>
    /// Registers one-shot lifecycle callbacks so room transitions can replace input assets without per-frame polling.
    /// </summary>
    private void SubscribeInputLifecycle()
    {
        if (inputLifecycleSubscribed)
            return;

        PlayerInputRuntime.RuntimeInitialized += BindToggleAction;
        PlayerInputRuntime.RuntimeShutdown += ReleaseToggleAction;
        inputLifecycleSubscribed = true;
    }

    /// <summary>
    /// Releases shared input lifecycle callbacks when the HUD section is disposed.
    /// </summary>
    private void UnsubscribeInputLifecycle()
    {
        if (!inputLifecycleSubscribed)
            return;

        PlayerInputRuntime.RuntimeInitialized -= BindToggleAction;
        PlayerInputRuntime.RuntimeShutdown -= ReleaseToggleAction;
        inputLifecycleSubscribed = false;
    }

    /// <summary>
    /// Toggles the panel in response to the configured gameplay Input Action.
    /// </summary>
    /// <param name="context">Performed callback emitted by the shared runtime action.</param>
    private void HandleToggleActionPerformed(InputAction.CallbackContext context)
    {
        ToggleExpanded();
    }

    /// <summary>
    /// Releases the configured Input Action callback before rebinding or disposal.
    /// </summary>
    private void ReleaseToggleAction()
    {
        if (toggleAction == null)
            return;

        toggleAction.performed -= HandleToggleActionPerformed;
        toggleAction = null;
    }
    #endregion

    #region Data Refresh
    /// <summary>
    /// Refreshes icon pools only when the collected catalog hash changed.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player catalog.</param>
    /// <param name="playerEntity">Player entity whose catalog is displayed.</param>
    private void RefreshPowerUps(EntityManager entityManager, Entity playerEntity)
    {
        if (!entityManager.HasBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity))
        {
            HUDPowerUpSummaryViewPoolUtility.HideAllIconViews(activeIconViews);
            HUDPowerUpSummaryViewPoolUtility.HideAllIconViews(passiveIconViews);
            ApplyPowerUpColumnVisibility(0, 0);
            iconsInitialized = false;
            return;
        }

        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> catalog =
            entityManager.GetBuffer<PlayerPowerUpUnlockCatalogElement>(playerEntity, true);
        uint resolvedHash = HUDPowerUpSummaryRuntimeUtility.ComputePowerUpCatalogHash(catalog);

        if (iconsInitialized && resolvedHash == powerUpCatalogHash)
            return;

        powerUpCatalogHash = resolvedHash;
        iconsInitialized = true;
        int activeCount = 0;

        if (ShowsActivePowerUps())
            activeCount = HUDPowerUpSummaryViewPoolUtility.FillIconViews(catalog,
                                                                         PlayerPowerUpUnlockKind.Active,
                                                                         activeIconViews,
                                                                         config.MaximumVisibleActivePowerUps,
                                                                         in config);
        else
            HUDPowerUpSummaryViewPoolUtility.HideAllIconViews(activeIconViews);

        int passiveCount = 0;

        if (ShowsPassivePowerUps())
            passiveCount = HUDPowerUpSummaryViewPoolUtility.FillIconViews(catalog,
                                                                          PlayerPowerUpUnlockKind.Passive,
                                                                          passiveIconViews,
                                                                          config.MaximumVisiblePassivePowerUps,
                                                                          in config);
        else
            HUDPowerUpSummaryViewPoolUtility.HideAllIconViews(passiveIconViews);

        ApplyPowerUpColumnVisibility(activeCount, passiveCount);
    }

    /// <summary>
    /// Applies authored category and empty-column policies without changing either fixed icon pool.
    /// </summary>
    /// <param name="activeCount">Number of populated active icon slots.</param>
    /// <param name="passiveCount">Number of populated passive icon slots.</param>
    private void ApplyPowerUpColumnVisibility(int activeCount, int passiveCount)
    {
        if (activeColumnRoot != null)
            activeColumnRoot.gameObject.SetActive(ShowsActivePowerUps() &&
                                                  (activeCount > 0 || config.HideEmptyActiveColumn == 0));

        if (passiveColumnRoot != null)
            passiveColumnRoot.gameObject.SetActive(ShowsPassivePowerUps() &&
                                                   (passiveCount > 0 || config.HideEmptyPassiveColumn == 0));
    }

    /// <summary>
    /// Reports whether the baked upper summary includes the active power-up category.
    /// </summary>
    /// <returns>True when active power-ups may use their preauthored column.</returns>
    private bool ShowsActivePowerUps()
    {
        return config.PowerUpVisibility != GameHudSummaryPowerUpVisibility.PassiveOnly;
    }

    /// <summary>
    /// Reports whether the baked upper summary includes the passive power-up category.
    /// </summary>
    /// <returns>True when passive power-ups may use their preauthored column.</returns>
    private bool ShowsPassivePowerUps()
    {
        return config.PowerUpVisibility != GameHudSummaryPowerUpVisibility.ActiveOnly;
    }

    /// <summary>
    /// Refreshes every configured statistic row from current ECS components at the shared throttled cadence.
    /// </summary>
    /// <param name="entityManager">Entity manager owning the player and summary definitions.</param>
    /// <param name="playerEntity">Player entity whose values are displayed.</param>
    private void RefreshStatistics(EntityManager entityManager, Entity playerEntity)
    {
        if (configEntity == Entity.Null || !entityManager.Exists(configEntity) || statisticRows == null)
            return;

        DynamicBuffer<GamePowerUpSummaryStatisticElement> definitions =
            entityManager.GetBuffer<GamePowerUpSummaryStatisticElement>(configEntity, true);
        int visibleCount = math.min(definitions.Length, statisticRows.Length);

        for (int statisticIndex = 0; statisticIndex < visibleCount; statisticIndex++)
        {
            HUDPowerUpSummaryStatisticRowView row = statisticRows[statisticIndex];

            if (row == null)
                continue;

            GamePowerUpSummaryStatisticElement definition = definitions[statisticIndex];

            if (HUDPowerUpSummaryRuntimeUtility.TryResolveStatistic(entityManager,
                                                                    playerEntity,
                                                                    in definition,
                                                                    out HUDPowerUpSummaryStatisticValue value))
                row.SetText(HUDPowerUpSummaryRuntimeUtility.FormatStatistic(in definition, in value));
            else
                row.SetText(definition.ShowLabel != 0 ? definition.Label.ToString() + ": —" : "—");
        }
    }
    #endregion

    #region Panel Motion
    /// <summary>
    /// Toggles the target panel state and starts one interruptible transition.
    /// </summary>
    private void ToggleExpanded()
    {
        if (!configApplied || config.Enabled == 0 || panelRoot == null)
            return;

        isExpanded = !isExpanded;
        StopSlide();

        if (config.SlideDurationSeconds <= 0f)
        {
            ApplyPanelPosition(isExpanded);
            return;
        }

        slideCoroutine = StartCoroutine(SlidePanel(panelRoot.anchoredPosition,
                                                   ResolvePanelPosition(isExpanded),
                                                   config.SlideDurationSeconds));
    }

    /// <summary>
    /// Moves the panel between its current and target anchored positions using the selected authored easing.
    /// </summary>
    /// <param name="startPosition">Anchored position at transition start.</param>
    /// <param name="targetPosition">Anchored position requested by the new expanded state.</param>
    /// <param name="durationSeconds">Transition duration in seconds.</param>
    /// <returns>Coroutine enumerator scheduled by Unity.</returns>
    private IEnumerator SlidePanel(Vector2 startPosition, Vector2 targetPosition, float durationSeconds)
    {
        float elapsedSeconds = 0f;

        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += config.UseUnscaledTime != 0 ? Time.unscaledDeltaTime : Time.deltaTime;
            float normalizedTime = math.saturate(elapsedSeconds / durationSeconds);
            panelRoot.anchoredPosition = Vector2.LerpUnclamped(startPosition,
                                                               targetPosition,
                                                               HUDPowerUpSummaryPresentationUtility.EvaluateSlide(
                                                                   config.SlideEasing,
                                                                   normalizedTime));
            yield return null;
        }

        panelRoot.anchoredPosition = targetPosition;
        slideCoroutine = null;
    }

    /// <summary>
    /// Stops the active slide coroutine before a new target state is requested.
    /// </summary>
    private void StopSlide()
    {
        if (slideCoroutine == null)
            return;

        StopCoroutine(slideCoroutine);
        slideCoroutine = null;
    }

    /// <summary>
    /// Applies an expanded or collapsed position immediately.
    /// </summary>
    /// <param name="expanded">True to show the complete panel.</param>
    private void ApplyPanelPosition(bool expanded)
    {
        if (panelRoot != null)
            panelRoot.anchoredPosition = ResolvePanelPosition(expanded);
    }

    /// <summary>
    /// Resolves the anchored position that retains only the toggle handle when collapsed.
    /// </summary>
    /// <param name="expanded">True to resolve the fully visible position.</param>
    /// <returns>Screen-edge anchored panel position.</returns>
    private Vector2 ResolvePanelPosition(bool expanded)
    {
        if (expanded)
            return Vector2.zero;

        float visibleHandleWidth = config.EnableClickToggle != 0 ? config.CollapsedHandleWidth : 0f;
        float hiddenDistance = math.max(0f, config.ExpandedWidth - visibleHandleWidth);
        float horizontalPosition = config.PanelSide == GameHudSummaryPanelSide.Right ? hiddenDistance : -hiddenDistance;
        return new Vector2(horizontalPosition, 0f);
    }

    #endregion

    #region Helpers
    /// <summary>
    /// Enables or disables the complete preauthored panel only when its current state differs.
    /// </summary>
    /// <param name="active">Requested panel activation state.</param>
    private void SetPanelActive(bool active)
    {
        if (panelRoot == null || panelRoot.gameObject.activeSelf == active)
            return;

        panelRoot.gameObject.SetActive(active);
    }

    #endregion

    #endregion
}
