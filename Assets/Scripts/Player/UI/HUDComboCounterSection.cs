using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the runtime combo counter, current rank label, and progress toward the next combo rank from ECS data.
/// </summary>
[Serializable]
public sealed class HUDComboCounterSection
{
    #region Constants
    private const float ProgressComparisonEpsilon = 0.0001f;
    private const float VisibilityComparisonEpsilon = 0.001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the combo HUD section and its ECS-driven presentation updates.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Optional root GameObject shown or hidden as one block for the combo counter.")]
    [SerializeField] private GameObject rootObject;

    [Tooltip("Optional badge image used to display the current combo-rank sprite.")]
    [SerializeField] private Image rankBadgeImage;

    [Tooltip("TMP text used to render the current combo-rank label.")]
    [SerializeField] private TMP_Text rankText;

    [Tooltip("TMP text used to render the current combo numeric value.")]
    [SerializeField] private TMP_Text comboValueText;

    [Tooltip("Optional fill image used as progress bar toward the next combo rank.")]
    [SerializeField] private Image progressFillImage;

    [Tooltip("Optional background image shown behind the progress fill.")]
    [SerializeField] private Image progressBackgroundImage;

    [Tooltip("Optional default badge sprite used when the active rank does not define a specific sprite.")]
    [SerializeField] private Sprite defaultBadgeSprite;

    [Tooltip("Fallback tint applied to the badge image when no rank-specific theme matches.")]
    [SerializeField] private Color defaultBadgeTint = Color.white;

    [Tooltip("Fallback color applied to the rank label when no rank-specific theme matches.")]
    [SerializeField] private Color defaultRankTextColor = Color.white;

    [Tooltip("Fallback color applied to the combo numeric label when no rank-specific theme matches.")]
    [SerializeField] private Color defaultComboValueTextColor = Color.white;

    [Tooltip("Fallback color applied to the progress fill when no rank-specific theme matches.")]
    [SerializeField] private Color defaultProgressFillColor = Color.white;

    [Tooltip("Fallback color applied to the progress background when no rank-specific theme matches.")]
    [SerializeField] private Color defaultProgressBackgroundColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("When disabled, the badge image stays hidden even if it is assigned.")]
    [SerializeField] private bool showRankBadgeImage = true;

    [Tooltip("When disabled, the progress bar stays hidden even if the images are assigned.")]
    [SerializeField] private bool showProgressBar = true;

    [Tooltip("Hides the combo HUD while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Hides the combo HUD while the current combo value is 0.")]
    [SerializeField] private bool hideWhenZeroCombo = true;

    [Tooltip("Hides the combo HUD whenever the current combo value no longer reaches any authored rank threshold.")]
    [SerializeField] private bool hideWhenNoActiveRank = true;

    [Tooltip("Seconds used to fade the combo HUD when it becomes visible.")]
    [SerializeField] private float fadeInDuration = 0.18f;

    [Tooltip("Seconds used to fade the combo HUD when it becomes hidden.")]
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Tooltip("Fallback label shown before the first combo rank is reached.")]
    [SerializeField] private string idleRankLabel = "COMBO";

    [Tooltip("Legacy per-rank visual themes kept hidden only as a backward-compatible fallback for existing scene data.")]
    [HideInInspector]
    [SerializeField] private List<HUDComboCounterRankVisualDefinition> rankVisuals = new List<HUDComboCounterRankVisualDefinition>();
    #endregion

    private int displayedComboValue = int.MinValue;
    private float displayedProgressNormalized = -1f;
    private int displayedRankVisualIndex = int.MinValue;
    private string displayedRankLabel = string.Empty;
    private bool rankThemeInitialized;
    private float currentVisibilityAlpha;
    private float targetVisibilityAlpha;
    private bool visibilityStateInitialized;
    private bool resetCachedStateWhenHidden;
    private CanvasGroup rootCanvasGroup;
    private PlayerProgressionPreset progressionPreset;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the initial authored visual state before runtime ECS data becomes available.
    /// </summary>
    public void Initialize()
    {
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Applies the initial visual state used before a valid player entity is resolved.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        EnsureFadeBindings();
        ResetCachedPresentationState();

        if (!isEnabled)
        {
            InitializeVisibility(false);
            return;
        }

        progressionPreset = HUDComboCounterPresetRuntimeUtility.ResolveProgressionPreset();

        if (hideWhenPlayerMissing)
        {
            InitializeVisibility(false);
            return;
        }

        ApplyFallbackVisibleState();
        InitializeVisibility(true);
    }

    /// <summary>
    /// Applies the missing-player visual state and clears cached values so the next resolved player rebuilds the HUD.
    /// </summary>
    public void HandleMissingPlayer()
    {
        ResetCachedPresentationState();

        if (!isEnabled)
        {
            RequestVisibility(false, false);
            AdvanceVisibilityFade(Time.unscaledDeltaTime);
            return;
        }

        if (hideWhenPlayerMissing)
        {
            RequestVisibility(false, true);
            AdvanceVisibilityFade(Time.unscaledDeltaTime);
            return;
        }

        ApplyFallbackVisibleState();
        RequestVisibility(true, false);
        AdvanceVisibilityFade(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Updates the combo HUD from ECS combo components owned by the current player entity.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read combo runtime components.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled)
        {
            RequestVisibility(false, false);
            AdvanceVisibilityFade(Time.unscaledDeltaTime);
            return;
        }

        if (!runtimeEntityManager.Exists(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerComboCounterState>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerRuntimeComboCounterConfig>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerRuntimeComboCounterConfig runtimeComboConfig = runtimeEntityManager.GetComponentData<PlayerRuntimeComboCounterConfig>(playerEntity);
        PlayerComboCounterState comboCounterState = runtimeEntityManager.GetComponentData<PlayerComboCounterState>(playerEntity);
        bool shouldBeVisible = runtimeComboConfig.Enabled != 0;

        if (hideWhenNoActiveRank && comboCounterState.CurrentRankIndex < 0)
        {
            shouldBeVisible = false;
        }

        if (hideWhenZeroCombo && comboCounterState.CurrentValue <= 0)
        {
            shouldBeVisible = false;
        }

        if (shouldBeVisible)
        {
            ApplyVisibleState(runtimeEntityManager,
                              playerEntity,
                              comboCounterState.CurrentValue,
                              comboCounterState.CurrentRankIndex,
                              comboCounterState.CurrentRankId,
                              comboCounterState.ProgressNormalized);
        }

        RequestVisibility(shouldBeVisible, false);
        AdvanceVisibilityFade(Time.unscaledDeltaTime);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies the current combo state to all authored UI bindings.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to resolve baked combo-rank visuals.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="comboValue">Current combo numeric value.</param>
    /// <param name="currentRankIndex">Current combo-rank index.</param>
    /// <param name="currentRankId">Current combo-rank identifier.</param>
    /// <param name="progressNormalized">Current normalized progress toward the next rank.</param>
    private void ApplyVisibleState(EntityManager runtimeEntityManager,
                                   Entity playerEntity,
                                   int comboValue,
                                   int currentRankIndex,
                                   FixedString64Bytes currentRankId,
                                   float progressNormalized)
    {
        ApplyRankTheme(runtimeEntityManager, playerEntity, currentRankIndex, currentRankId);
        ApplyRankLabel(currentRankId);
        ApplyComboValue(comboValue);
        ApplyProgress(progressNormalized);
    }

    /// <summary>
    /// Applies the fallback non-runtime visible state used when the HUD should remain visible without a player entity.
    /// </summary>
    private void ApplyFallbackVisibleState()
    {
        ApplyResolvedTheme(new HUDComboCounterResolvedVisualTheme(defaultBadgeSprite,
                                                                 defaultBadgeTint,
                                                                 defaultRankTextColor,
                                                                 defaultComboValueTextColor,
                                                                 defaultProgressFillColor,
                                                                 defaultProgressBackgroundColor));
        ApplyRankLabel(default);
        ApplyComboValue(0);
        ApplyProgress(0f);
    }

    /// <summary>
    /// Requests the target visible state while preserving the currently rendered visuals during fade-out.
    /// </summary>
    /// <param name="visible">True when the section should fade toward full visibility.</param>
    /// <param name="resetCachedStateAfterHide">True when the cached presentation state should be invalidated once fully hidden.</param>
    private void RequestVisibility(bool visible, bool resetCachedStateAfterHide)
    {
        EnsureFadeBindings();

        if (!visibilityStateInitialized)
        {
            InitializeVisibility(visible);
        }

        if (visible)
        {
            resetCachedStateWhenHidden = false;
            SetVisualPresence(true);
            targetVisibilityAlpha = 1f;
            return;
        }

        if (resetCachedStateAfterHide)
        {
            resetCachedStateWhenHidden = true;
        }

        targetVisibilityAlpha = 0f;
    }

    /// <summary>
    /// Initializes the current and target visibility state without performing an animated transition.
    /// </summary>
    /// <param name="visible">True when the section should start visible.</param>
    private void InitializeVisibility(bool visible)
    {
        currentVisibilityAlpha = visible ? 1f : 0f;
        targetVisibilityAlpha = currentVisibilityAlpha;
        visibilityStateInitialized = true;
        ApplyVisibilityAlpha(currentVisibilityAlpha);
        SetVisualPresence(visible);
    }

    /// <summary>
    /// Advances the visibility fade toward the requested alpha target using unscaled time.
    /// </summary>
    /// <param name="deltaTime">Unscaled delta time used by the HUD fade.</param>
    private void AdvanceVisibilityFade(float deltaTime)
    {
        EnsureFadeBindings();

        if (!visibilityStateInitialized)
        {
            InitializeVisibility(false);
        }

        float sanitizedDeltaTime = Mathf.Max(0f, deltaTime);
        float resolvedTargetAlpha = Mathf.Clamp01(targetVisibilityAlpha);
        float resolvedFadeDuration = resolvedTargetAlpha > currentVisibilityAlpha
            ? Mathf.Max(0f, fadeInDuration)
            : Mathf.Max(0f, fadeOutDuration);

        if (Mathf.Abs(currentVisibilityAlpha - resolvedTargetAlpha) > VisibilityComparisonEpsilon)
        {
            if (resolvedFadeDuration <= 0f)
            {
                currentVisibilityAlpha = resolvedTargetAlpha;
            }
            else
            {
                float alphaStep = sanitizedDeltaTime / resolvedFadeDuration;
                currentVisibilityAlpha = Mathf.MoveTowards(currentVisibilityAlpha, resolvedTargetAlpha, alphaStep);
            }

            ApplyVisibilityAlpha(currentVisibilityAlpha);
        }

        bool hasVisiblePresence = currentVisibilityAlpha > VisibilityComparisonEpsilon || resolvedTargetAlpha > VisibilityComparisonEpsilon;
        SetVisualPresence(hasVisiblePresence);

        if (currentVisibilityAlpha > VisibilityComparisonEpsilon || resolvedTargetAlpha > VisibilityComparisonEpsilon)
        {
            return;
        }

        if (resetCachedStateWhenHidden)
        {
            ResetCachedPresentationState();
            resetCachedStateWhenHidden = false;
        }
    }

    /// <summary>
    /// Ensures the root CanvasGroup used for alpha fades exists when a root object is assigned.
    /// </summary>
    private void EnsureFadeBindings()
    {
        if (rootObject == null)
        {
            rootCanvasGroup = null;
            return;
        }

        if (rootCanvasGroup != null && rootCanvasGroup.gameObject == rootObject)
        {
            return;
        }

        CanvasGroup resolvedCanvasGroup = rootObject.GetComponent<CanvasGroup>();

        if (resolvedCanvasGroup == null)
        {
            resolvedCanvasGroup = rootObject.AddComponent<CanvasGroup>();
        }

        rootCanvasGroup = resolvedCanvasGroup;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Applies the current visibility alpha either through the root CanvasGroup or directly through individual graphics.
    /// </summary>
    /// <param name="alpha">Normalized visibility alpha in the 0..1 range.</param>
    private void ApplyVisibilityAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = clampedAlpha;
            return;
        }

        ApplyGraphicAlpha(rankBadgeImage, clampedAlpha);
        ApplyGraphicAlpha(rankText, clampedAlpha);
        ApplyGraphicAlpha(comboValueText, clampedAlpha);
        ApplyGraphicAlpha(progressFillImage, clampedAlpha);
        ApplyGraphicAlpha(progressBackgroundImage, clampedAlpha);
    }

    /// <summary>
    /// Shows or hides the bound visual elements while respecting optional badge and progress toggles.
    /// </summary>
    /// <param name="visible">True when the bound UI elements must stay active for rendering or fade.</param>
    private void SetVisualPresence(bool visible)
    {
        if (rootObject != null)
        {
            rootObject.SetActive(visible);
        }

        if (rankBadgeImage != null)
        {
            rankBadgeImage.enabled = visible && showRankBadgeImage;
        }

        if (rankText != null)
        {
            rankText.enabled = visible;
        }

        if (comboValueText != null)
        {
            comboValueText.enabled = visible;
        }

        bool showProgress = visible && showProgressBar;

        if (progressFillImage != null)
        {
            progressFillImage.enabled = showProgress;
        }

        if (progressBackgroundImage != null)
        {
            progressBackgroundImage.enabled = showProgress;
        }
    }

    /// <summary>
    /// Applies the current combo-rank visual theme when the active rank changes.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to resolve baked combo-rank visuals.</param>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <param name="currentRankIndex">Current combo-rank index.</param>
    /// <param name="currentRankId">Current combo-rank identifier.</param>
    private void ApplyRankTheme(EntityManager runtimeEntityManager,
                                Entity playerEntity,
                                int currentRankIndex,
                                FixedString64Bytes currentRankId)
    {
        if (rankThemeInitialized && displayedRankVisualIndex == currentRankIndex)
        {
            return;
        }

        displayedRankVisualIndex = currentRankIndex;
        rankThemeInitialized = true;

        HUDComboCounterResolvedVisualTheme resolvedTheme;

        if (HUDComboCounterVisualThemeRuntimeUtility.TryResolveRuntimeTheme(runtimeEntityManager,
                                                                           playerEntity,
                                                                           currentRankIndex,
                                                                           defaultBadgeSprite,
                                                                           defaultBadgeTint,
                                                                           defaultRankTextColor,
                                                                           defaultComboValueTextColor,
                                                                           defaultProgressFillColor,
                                                                           defaultProgressBackgroundColor,
                                                                           out resolvedTheme))
        {
            ApplyResolvedTheme(resolvedTheme);
            return;
        }

        if (progressionPreset == null)
        {
            progressionPreset = HUDComboCounterPresetRuntimeUtility.ResolveProgressionPreset();
        }

        PlayerComboRankVisualDefinition rankVisual = HUDComboCounterVisualThemeRuntimeUtility.ResolvePresetRankVisual(progressionPreset,
                                                                                                                      currentRankIndex);
        HUDComboCounterRankVisualDefinition legacyRankVisual = rankVisual == null
            ? HUDComboCounterVisualThemeRuntimeUtility.ResolveLegacyRankVisual(rankVisuals, currentRankId)
            : null;
        resolvedTheme = HUDComboCounterVisualThemeRuntimeUtility.ResolveTheme(rankVisual,
                                                                              legacyRankVisual,
                                                                              defaultBadgeSprite,
                                                                              defaultBadgeTint,
                                                                              defaultRankTextColor,
                                                                              defaultComboValueTextColor,
                                                                              defaultProgressFillColor,
                                                                              defaultProgressBackgroundColor);
        ApplyResolvedTheme(resolvedTheme);
    }

    /// <summary>
    /// Applies one fully resolved visual theme to the bound HUD elements.
    /// </summary>
    /// <param name="resolvedTheme">Fully resolved theme to assign.</param>
    private void ApplyResolvedTheme(HUDComboCounterResolvedVisualTheme resolvedTheme)
    {
        if (rankBadgeImage != null)
        {
            rankBadgeImage.sprite = resolvedTheme.BadgeSprite;
            rankBadgeImage.color = resolvedTheme.BadgeTint;
        }

        if (rankText != null)
        {
            rankText.color = resolvedTheme.RankTextColor;
        }

        if (comboValueText != null)
        {
            comboValueText.color = resolvedTheme.ComboValueTextColor;
        }

        if (progressFillImage != null)
        {
            progressFillImage.color = resolvedTheme.ProgressFillColor;
        }

        if (progressBackgroundImage != null)
        {
            progressBackgroundImage.color = resolvedTheme.ProgressBackgroundColor;
        }
    }

    /// <summary>
    /// Applies the displayed rank label only when it actually changed.
    /// </summary>
    /// <param name="currentRankId">Current combo-rank identifier.</param>
    private void ApplyRankLabel(FixedString64Bytes currentRankId)
    {
        if (rankText == null)
        {
            return;
        }

        string resolvedLabel = currentRankId.Length > 0 ? currentRankId.ToString() : idleRankLabel;

        if (string.Equals(displayedRankLabel, resolvedLabel, StringComparison.Ordinal))
        {
            return;
        }

        displayedRankLabel = resolvedLabel;
        rankText.text = resolvedLabel;
    }

    /// <summary>
    /// Applies the displayed combo numeric label only when it actually changed.
    /// </summary>
    /// <param name="comboValue">Current combo numeric value.</param>
    private void ApplyComboValue(int comboValue)
    {
        if (comboValueText == null)
        {
            return;
        }

        int sanitizedComboValue = Mathf.Max(0, comboValue);

        if (displayedComboValue == sanitizedComboValue)
        {
            return;
        }

        displayedComboValue = sanitizedComboValue;
        comboValueText.text = string.Format("x{0}", sanitizedComboValue);
    }

    /// <summary>
    /// Applies the displayed progress fill only when it actually changed.
    /// </summary>
    /// <param name="progressNormalized">Current normalized progress toward the next rank.</param>
    private void ApplyProgress(float progressNormalized)
    {
        if (progressFillImage == null)
        {
            return;
        }

        float sanitizedProgress = Mathf.Clamp01(progressNormalized);

        if (Mathf.Abs(displayedProgressNormalized - sanitizedProgress) <= ProgressComparisonEpsilon)
        {
            return;
        }

        displayedProgressNormalized = sanitizedProgress;
        progressFillImage.fillAmount = sanitizedProgress;
    }

    /// <summary>
    /// Resets cached presentation values so the next applied state rebuilds every visual binding.
    /// </summary>
    private void ResetCachedPresentationState()
    {
        displayedComboValue = int.MinValue;
        displayedProgressNormalized = -1f;
        displayedRankVisualIndex = int.MinValue;
        displayedRankLabel = string.Empty;
        rankThemeInitialized = false;
        progressionPreset = null;
    }

    /// <summary>
    /// Applies one alpha value directly to one graphic canvas renderer.
    /// </summary>
    /// <param name="graphic">Graphic receiving the alpha.</param>
    /// <param name="alpha">Alpha value applied to the canvas renderer.</param>
    private static void ApplyGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        graphic.canvasRenderer.SetAlpha(Mathf.Clamp01(alpha));
    }
    #endregion

    #endregion
}
