using System.Text;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the ECS-authoritative combo state as a seamless two-wave Synchro Meter.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDComboCounterSection : MonoBehaviour
{
    #region Constants
    private const float VisibilityComparisonEpsilon = 0.001f;
    private const float ProgressComparisonEpsilon = 0.0001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("Enables the Synchro Meter and its ECS-driven presentation updates.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Root GameObject shown or hidden as one block for the Synchro Meter.")]
    [SerializeField] private GameObject rootObject;

    [Header("Authored Layers")]
    [Tooltip("RectTransform defining the clipped wave display area and the reference width for diagnostics.")]
    [SerializeField] private RectTransform waveViewport;

    [Tooltip("Oscilloscope background image rendered behind both waves.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Scanline cover image rendered above both waves.")]
    [SerializeField] private Image coverImage;

    [Tooltip("Leading image of the seamless primary-wave pair.")]
    [SerializeField] private Image primaryWaveLeadingImage;

    [Tooltip("Trailing image of the seamless primary-wave pair.")]
    [SerializeField] private Image primaryWaveTrailingImage;

    [Tooltip("Leading image of the seamless secondary-wave pair.")]
    [SerializeField] private Image secondaryWaveLeadingImage;

    [Tooltip("Trailing image of the seamless secondary-wave pair.")]
    [SerializeField] private Image secondaryWaveTrailingImage;

    [Tooltip("TMP text used to render the current synchro rank identifier.")]
    [SerializeField] private TMP_Text rankText;

    [Tooltip("TMP text used to render the current numeric synchro value.")]
    [SerializeField] private TMP_Text valueText;

    [Tooltip("Horizontal filled image that renders normalized progression toward the next synchro rank.")]
    [SerializeField] private Image progressFillImage;

    [Tooltip("Track image rendered behind normalized synchro progression.")]
    [SerializeField] private Image progressBackgroundImage;

    [Tooltip("Authored TMP label shown at the progress-bar position when Progression Text mode is selected.")]
    [SerializeField] private TMP_Text progressionText;

    [Header("Presentation Mode")]
    [Tooltip("Fallback overlay composition used before the baked HUD Manager preset becomes available.")]
    [SerializeField] private GameHudSynchroMeterVisualMode visualMode;

    [Tooltip("Fallback tokenized label format used by Progression Text mode.")]
    [SerializeField] private string progressionTextFormat = GameHudSynchroMeterSettings.DefaultProgressionTextFormat;

    [Tooltip("Fallback font size in pixels used by the optional progression label.")]
    [SerializeField] private float progressionTextFontSize = GameHudSynchroMeterSettings.DefaultProgressionTextFontSize;

    [Tooltip("Fallback horizontal alignment used by the optional progression label.")]
    [SerializeField] private GameHudSynchroMeterTextAlignment progressionTextAlignment = GameHudSynchroMeterTextAlignment.Center;

    [Tooltip("Fallback vertical distance in pixels between the wave reticle and the optional progression label.")]
    [SerializeField] private float progressionTextWaveDistance = GameHudSynchroMeterSettings.DefaultProgressionTextWaveDistance;

    [Header("Theme Fallback")]
    [Tooltip("Fallback tint applied to the oscilloscope background image.")]
    [SerializeField] private Color backgroundTint = Color.white;

    [Tooltip("Fallback tint applied to the scanline cover image.")]
    [SerializeField] private Color coverTint = Color.white;

    [Tooltip("Fallback tint applied to both primary-wave images.")]
    [SerializeField] private Color primaryWaveTint = Color.white;

    [Tooltip("Fallback tint applied to both secondary-wave images.")]
    [SerializeField] private Color secondaryWaveTint = Color.white;

    [Tooltip("Fallback color applied to the current rank label.")]
    [SerializeField] private Color rankTextColor = Color.white;

    [Tooltip("Fallback color applied to the current numeric value.")]
    [SerializeField] private Color valueTextColor = Color.white;

    [Tooltip("Fallback color applied to the optional progression label below the waves.")]
    [SerializeField] private Color progressionTextColor = Color.white;

    [Tooltip("Fallback tint applied to the progression fill below the wave display.")]
    [SerializeField] private Color progressFillTint = new Color(0f, 0.85f, 1f, 1f);

    [Tooltip("Fallback tint applied to the progression track below the wave display.")]
    [SerializeField] private Color progressBackgroundTint = new Color(0f, 0f, 0f, 0.65f);

    [Header("Layer Visibility")]
    [Tooltip("Shows the background layer when its image is assigned.")]
    [SerializeField] private bool showBackground = true;

    [Tooltip("Shows the scanline cover layer when its image is assigned.")]
    [SerializeField] private bool showCover;

    [Tooltip("Shows the current rank label over the wave display.")]
    [SerializeField] private bool showRankText = true;

    [Tooltip("Shows the current numeric value over the wave display.")]
    [SerializeField] private bool showValueText = true;

    [Tooltip("Shows normalized progression toward the next synchro rank below the wave display.")]
    [SerializeField] private bool showProgressBar = true;

    [Header("Wave Animation")]
    [Tooltip("Number of complete wave-image tile cycles scrolled per second.")]
    [SerializeField] private float waveScrollCyclesPerSecond = 0.12f;

    [Tooltip("Normalized separation between waves at the first rank, measured over one complete image tile.")]
    [SerializeField] private float lowestRankPhaseOffsetNormalized = 0.25f;

    [Tooltip("Normalized separation between waves at the maximum rank. Use 0 for complete overlap.")]
    [SerializeField] private float highestRankPhaseOffsetNormalized;

    [Tooltip("Exponent shaping wave convergence across rank indices.")]
    [SerializeField] private float phaseOffsetResponseExponent = 1f;

    [Header("Single Rank Wave Animation")]
    [Tooltip("Increases both wave scroll rates linearly while Single Rank Progression advances.")]
    [SerializeField] private bool singleRankAccelerateWavesWithProgress = true;

    [Tooltip("Wave-image tile cycles per second reached at full Single Rank Progression when acceleration is enabled.")]
    [SerializeField] private float singleRankMaximumWaveScrollCyclesPerSecond = 0.3f;

    [Tooltip("Controls whether Single Rank Progression converges the two waves continuously or through equal progression steps.")]
    [SerializeField] private GameHudSynchroSingleRankConvergenceMode singleRankConvergenceMode;

    [Tooltip("Normalized wave separation used before Single Rank Progression convergence starts.")]
    [SerializeField] private float singleRankInitialPhaseOffsetNormalized = 0.25f;

    [Tooltip("Normalized wave separation reached after Single Rank Progression convergence ends. Use 0 for complete overlap.")]
    [SerializeField] private float singleRankFinalPhaseOffsetNormalized;

    [Tooltip("Single Rank Progression percentage at which wave convergence starts.")]
    [SerializeField] private float singleRankConvergenceStartProgressPercent;

    [Tooltip("Single Rank Progression percentage at which wave convergence ends.")]
    [SerializeField] private float singleRankConvergenceEndProgressPercent = 100f;

    [Tooltip("Number of equal convergence intervals used when Single Rank Progression selects Steps mode.")]
    [SerializeField] private int singleRankConvergenceStepCount = 5;

    [Header("Shared Wave Animation")]
    [Tooltip("Seconds used to blend the secondary wave toward the phase required by a new rank.")]
    [SerializeField] private float phaseTransitionDuration = 0.3f;

    [Tooltip("Uses unscaled time so wave animation remains independent from gameplay time scale.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Seconds used to smooth progression fill changes. Use 0 for immediate authoritative updates.")]
    [SerializeField] private float progressSmoothingSeconds = 0.08f;

    [Header("Visibility")]
    [Tooltip("Hides the Synchro Meter while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Hides the Synchro Meter while the current synchro value is 0.")]
    [SerializeField] private bool hideWhenZeroValue = true;

    [Tooltip("Hides the Synchro Meter whenever no authored rank threshold is active.")]
    [SerializeField] private bool hideWhenNoActiveRank = true;

    [Tooltip("Seconds used to fade the Synchro Meter when it becomes visible.")]
    [SerializeField] private float fadeInDuration = 0.18f;

    [Tooltip("Seconds used to fade the Synchro Meter when it becomes hidden.")]
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Tooltip("Fallback label shown before the first synchro rank is reached.")]
    [SerializeField] private string idleRankLabel = "SYNCHRO";
    #endregion

    private HUDComboTextPresentationState textPresentationState;
    private float scrollPhaseNormalized;
    private float currentWaveScrollCyclesPerSecond;
    private float currentWaveOffsetNormalized;
    private float targetWaveOffsetNormalized;
    private float currentVisibilityAlpha;
    private float targetVisibilityAlpha;
    private bool wavePhaseInitialized;
    private bool visibilityStateInitialized;
    private bool resetCachedStateWhenHidden;
    private CanvasGroup rootCanvasGroup;
    private float displayedProgressNormalized = float.MinValue;
    private float targetProgressNormalized;
    private readonly StringBuilder progressionTextBuilder = new StringBuilder(512);
    private int displayedProgressPercentage = int.MinValue;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the baked HUD Manager settings used by the managed Synchro Meter presentation.
    /// </summary>
    /// <param name="config">Runtime HUD config resolved from the ECS singleton.</param>
    public void ApplySettings(in GameHudRuntimeConfig config)
    {
        isEnabled = config.SynchroMeterEnabled != 0;
        visualMode = config.SynchroVisualMode;
        backgroundTint = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroBackgroundTint);
        coverTint = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroCoverTint);
        primaryWaveTint = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroPrimaryWaveTint);
        secondaryWaveTint = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroSecondaryWaveTint);
        rankTextColor = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroRankTextColor);
        valueTextColor = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroValueTextColor);
        progressionTextColor = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroProgressionTextColor);
        progressFillTint = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroProgressFillTint);
        progressBackgroundTint = HUDSynchroMeterPresentationUtility.ToColor(config.SynchroProgressBackgroundTint);
        showBackground = config.SynchroShowBackground != 0;
        showCover = config.SynchroShowCover != 0;
        showRankText = config.SynchroShowRankText != 0;
        showValueText = config.SynchroShowValueText != 0;
        showProgressBar = config.SynchroShowProgressBar != 0;
        progressionTextFormat = config.SynchroProgressionTextFormat.ToString();
        progressionTextFontSize = config.SynchroProgressionTextFontSize;
        progressionTextAlignment = config.SynchroProgressionTextAlignment;
        progressionTextWaveDistance = config.SynchroProgressionTextWaveDistance;
        waveScrollCyclesPerSecond = config.SynchroWaveScrollCyclesPerSecond;
        lowestRankPhaseOffsetNormalized = config.SynchroLowestRankPhaseOffsetNormalized;
        highestRankPhaseOffsetNormalized = config.SynchroHighestRankPhaseOffsetNormalized;
        phaseOffsetResponseExponent = config.SynchroPhaseOffsetResponseExponent;
        singleRankAccelerateWavesWithProgress = config.SynchroSingleRankAccelerateWavesWithProgress != 0;
        singleRankMaximumWaveScrollCyclesPerSecond = config.SynchroSingleRankMaximumWaveScrollCyclesPerSecond;
        singleRankConvergenceMode = config.SynchroSingleRankConvergenceMode;
        singleRankInitialPhaseOffsetNormalized = config.SynchroSingleRankInitialPhaseOffsetNormalized;
        singleRankFinalPhaseOffsetNormalized = config.SynchroSingleRankFinalPhaseOffsetNormalized;
        singleRankConvergenceStartProgressPercent = config.SynchroSingleRankConvergenceStartProgressPercent;
        singleRankConvergenceEndProgressPercent = config.SynchroSingleRankConvergenceEndProgressPercent;
        singleRankConvergenceStepCount = config.SynchroSingleRankConvergenceStepCount;
        phaseTransitionDuration = config.SynchroPhaseTransitionDuration;
        useUnscaledTime = config.SynchroUseUnscaledTime != 0;
        progressSmoothingSeconds = config.SynchroProgressSmoothingSeconds;
        hideWhenPlayerMissing = config.SynchroHideWhenPlayerMissing != 0;
        hideWhenZeroValue = config.SynchroHideWhenZeroValue != 0;
        hideWhenNoActiveRank = config.SynchroHideWhenNoActiveRank != 0;
        fadeInDuration = config.SynchroFadeInDuration;
        fadeOutDuration = config.SynchroFadeOutDuration;
        idleRankLabel = config.SynchroIdleRankLabel.ToString();
        currentWaveScrollCyclesPerSecond = waveScrollCyclesPerSecond;
        ApplyTheme();
        wavePhaseInitialized = false;
    }

    /// <summary>
    /// Applies the authored initial state after HUD settings and scene bindings are available.
    /// </summary>
    public void Initialize()
    {
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Resets cached text, phase, and visibility before a valid player entity is resolved.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        EnsureBindings();
        ResetCachedPresentationState();
        ApplyTheme();
        targetWaveOffsetNormalized = HUDSynchroMeterWaveUtility.SanitizeNormalizedPhase(lowestRankPhaseOffsetNormalized, 0.25f);
        currentWaveOffsetNormalized = targetWaveOffsetNormalized;
        currentWaveScrollCyclesPerSecond = HUDSynchroMeterWaveUtility.SanitizeNonNegative(waveScrollCyclesPerSecond, 0.12f);
        wavePhaseInitialized = true;
        ApplyWaveTransforms();

        if (!isEnabled || hideWhenPlayerMissing)
        {
            InitializeVisibility(false);
            return;
        }

        ApplyFallbackVisibleState();
        InitializeVisibility(true);
    }

    /// <summary>
    /// Applies missing-player visibility while keeping the seamless animation valid for the next resolved player.
    /// </summary>
    public void HandleMissingPlayer()
    {
        ResetCachedPresentationState();

        if (!isEnabled || hideWhenPlayerMissing)
        {
            RequestVisibility(false, true);
            AdvancePresentation(ResolveDeltaTime());
            return;
        }

        ApplyFallbackVisibleState();
        RequestVisibility(true, false);
        AdvancePresentation(ResolveDeltaTime());
    }

    /// <summary>
    /// Updates labels, visibility, rank-derived wave separation, and seamless scrolling from ECS combo state.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read authoritative combo state and rank count.</param>
    /// <param name="playerEntity">Player entity currently driving the Synchro Meter.</param>
    public void UpdateSection(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled)
        {
            RequestVisibility(false, false);
            AdvancePresentation(ResolveDeltaTime());
            return;
        }

        if (!runtimeEntityManager.Exists(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerComboCounterState>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerRuntimeComboCounterConfig>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        // Resolve authoritative visibility and rank data before changing any visual state.
        PlayerRuntimeComboCounterConfig runtimeComboConfig = runtimeEntityManager.GetComponentData<PlayerRuntimeComboCounterConfig>(playerEntity);
        PlayerComboCounterState comboCounterState = runtimeEntityManager.GetComponentData<PlayerComboCounterState>(playerEntity);
        bool shouldBeVisible = runtimeComboConfig.Enabled != 0;

        if (runtimeComboConfig.Mode == PlayerComboCounterMode.SingleRankProgression &&
            runtimeComboConfig.SingleRankShowMeterOnlyAfterFirstMilestone != 0 &&
            comboCounterState.CurrentRankIndex < 0)
            shouldBeVisible = false;

        if (hideWhenNoActiveRank && comboCounterState.CurrentRankIndex < 0)
            shouldBeVisible = false;

        if (hideWhenZeroValue && comboCounterState.CurrentValue <= 0)
            shouldBeVisible = false;

        targetProgressNormalized = Mathf.Clamp01(comboCounterState.ProgressNormalized);

        // Select topology-specific phase and scroll behavior from authoritative combo configuration.
        if (runtimeComboConfig.Mode == PlayerComboCounterMode.SingleRankProgression)
        {
            targetWaveOffsetNormalized = HUDSynchroMeterWaveUtility.ResolveSingleRankPhaseOffset(targetProgressNormalized,
                                                                                                  singleRankInitialPhaseOffsetNormalized,
                                                                                                  singleRankFinalPhaseOffsetNormalized,
                                                                                                  singleRankConvergenceStartProgressPercent,
                                                                                                  singleRankConvergenceEndProgressPercent,
                                                                                                  singleRankConvergenceMode,
                                                                                                  singleRankConvergenceStepCount);
            currentWaveScrollCyclesPerSecond = HUDSynchroMeterWaveUtility.ResolveSingleRankScrollCycles(waveScrollCyclesPerSecond,
                                                                                                        singleRankMaximumWaveScrollCyclesPerSecond,
                                                                                                        targetProgressNormalized,
                                                                                                        singleRankAccelerateWavesWithProgress);
        }
        else
        {
            int rankCount = HUDComboCounterRuntimePresentationUtility.ResolveRankCount(runtimeEntityManager,
                                                                                        playerEntity,
                                                                                        comboCounterState.CurrentRankIndex,
                                                                                        runtimeComboConfig.Mode);
            targetWaveOffsetNormalized = HUDSynchroMeterWaveUtility.ResolveRankPhaseOffset(comboCounterState.CurrentRankIndex,
                                                                                          rankCount,
                                                                                          lowestRankPhaseOffsetNormalized,
                                                                                          highestRankPhaseOffsetNormalized,
                                                                                          phaseOffsetResponseExponent);
            currentWaveScrollCyclesPerSecond = HUDSynchroMeterWaveUtility.SanitizeNonNegative(waveScrollCyclesPerSecond, 0.12f);
        }

        if (shouldBeVisible && visualMode != GameHudSynchroMeterVisualMode.ProgressionText)
            HUDComboCounterRuntimePresentationUtility.ApplyVisibleText(rankText,
                                                                      valueText,
                                                                      idleRankLabel,
                                                                      comboCounterState.CurrentValue,
                                                                      comboCounterState.CurrentRankId,
                                                                      runtimeComboConfig.SingleRankMaximumComboValue,
                                                                      runtimeComboConfig.Mode == PlayerComboCounterMode.SingleRankProgression
                                                                          ? runtimeComboConfig.SingleRankValueDisplayMode
                                                                          : PlayerComboSingleRankValueDisplayMode.CurrentValue,
                                                                      ref textPresentationState);

        RequestVisibility(shouldBeVisible, false);
        AdvancePresentation(ResolveDeltaTime());
    }
    #endregion

    #region Presentation
    /// <summary>
    /// Applies the visible fallback state used when player absence is configured not to hide the meter.
    /// </summary>
    private void ApplyFallbackVisibleState()
    {
        if (visualMode != GameHudSynchroMeterVisualMode.ProgressionText)
            HUDComboCounterRuntimePresentationUtility.ApplyVisibleText(rankText,
                                                                      valueText,
                                                                      idleRankLabel,
                                                                      0,
                                                                      default,
                                                                      0,
                                                                      PlayerComboSingleRankValueDisplayMode.CurrentValue,
                                                                      ref textPresentationState);

        targetWaveOffsetNormalized = HUDSynchroMeterWaveUtility.SanitizeNormalizedPhase(lowestRankPhaseOffsetNormalized, 0.25f);
        currentWaveScrollCyclesPerSecond = HUDSynchroMeterWaveUtility.SanitizeNonNegative(waveScrollCyclesPerSecond, 0.12f);
    }

    /// <summary>
    /// Advances visibility, wave phase convergence, and seamless scrolling using one shared delta time.
    /// </summary>
    /// <param name="deltaTime">Frame delta time selected by the configured time domain.</param>
    private void AdvancePresentation(float deltaTime)
    {
        AdvanceVisibilityFade(deltaTime);
        AdvanceProgress(deltaTime);

        if (currentVisibilityAlpha <= VisibilityComparisonEpsilon &&
            targetVisibilityAlpha <= VisibilityComparisonEpsilon)
        {
            return;
        }

        if (!wavePhaseInitialized)
        {
            currentWaveOffsetNormalized = targetWaveOffsetNormalized;
            wavePhaseInitialized = true;
        }

        currentWaveOffsetNormalized = HUDSynchroMeterWaveUtility.AdvancePhase(currentWaveOffsetNormalized,
                                                                              targetWaveOffsetNormalized,
                                                                              phaseTransitionDuration,
                                                                              deltaTime);
        scrollPhaseNormalized = HUDSynchroMeterWaveUtility.AdvanceScroll(scrollPhaseNormalized,
                                                                         currentWaveScrollCyclesPerSecond,
                                                                         deltaTime);
        ApplyWaveTransforms();
    }

    /// <summary>
    /// Positions both authored image pairs so each wave scrolls continuously and retains the requested relative phase.
    /// </summary>
    private void ApplyWaveTransforms()
    {
        HUDSynchroMeterWaveUtility.ApplySeamlessPair(primaryWaveLeadingImage,
                                                     primaryWaveTrailingImage,
                                                     scrollPhaseNormalized);
        HUDSynchroMeterWaveUtility.ApplySeamlessPair(secondaryWaveLeadingImage,
                                                     secondaryWaveTrailingImage,
                                                     scrollPhaseNormalized + currentWaveOffsetNormalized);
    }

    /// <summary>
    /// Applies configured colors to all authored image and TMP bindings.
    /// </summary>
    private void ApplyTheme()
    {
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(backgroundImage, backgroundTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(coverImage, coverTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(primaryWaveLeadingImage, primaryWaveTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(primaryWaveTrailingImage, primaryWaveTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(secondaryWaveLeadingImage, secondaryWaveTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(secondaryWaveTrailingImage, secondaryWaveTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(rankText, rankTextColor);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(valueText, valueTextColor);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(progressionText, progressionTextColor);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(progressFillImage, progressFillTint);
        HUDSynchroMeterPresentationUtility.ApplyGraphicColor(progressBackgroundImage, progressBackgroundTint);
        HUDSynchroMeterPresentationUtility.ApplyProgressionTextLayout(progressionText,
                                                                     progressionTextFontSize,
                                                                     progressionTextAlignment,
                                                                     progressionTextWaveDistance);
    }

    /// <summary>
    /// Advances the progression fill toward the authoritative normalized value without redundant Image writes.
    /// </summary>
    /// <param name="deltaTime">Frame delta time selected by the configured time domain.</param>
    private void AdvanceProgress(float deltaTime)
    {
        float nextProgress = HUDSynchroMeterPresentationUtility.AdvanceProgress(displayedProgressNormalized,
                                                                                targetProgressNormalized,
                                                                                progressSmoothingSeconds,
                                                                                deltaTime);

        if (displayedProgressNormalized != float.MinValue &&
            Mathf.Abs(displayedProgressNormalized - nextProgress) <= ProgressComparisonEpsilon)
            return;

        displayedProgressNormalized = nextProgress;

        if (visualMode != GameHudSynchroMeterVisualMode.ProgressionText &&
            showProgressBar &&
            progressFillImage != null)
        {
            progressFillImage.fillAmount = displayedProgressNormalized;
            return;
        }

        if (visualMode != GameHudSynchroMeterVisualMode.ProgressionText)
            return;

        int progressionPercentage = Mathf.RoundToInt(displayedProgressNormalized * 100f);

        if (progressionPercentage == displayedProgressPercentage)
            return;

        displayedProgressPercentage = progressionPercentage;
        HUDSynchroMeterPresentationUtility.ApplyProgressionText(progressionText,
                                                               progressionTextBuilder,
                                                               progressionTextFormat,
                                                               progressionPercentage);
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Requests a target visible state while preserving authored visuals during fade-out.
    /// </summary>
    /// <param name="visible">True when the section should fade toward full visibility.</param>
    /// <param name="resetCachedStateAfterHide">True when cached text should be invalidated after reaching zero alpha.</param>
    private void RequestVisibility(bool visible, bool resetCachedStateAfterHide)
    {
        EnsureBindings();

        if (!visibilityStateInitialized)
            InitializeVisibility(visible);

        if (visible)
        {
            resetCachedStateWhenHidden = false;
            SetVisualPresence(true);
            targetVisibilityAlpha = 1f;
            return;
        }

        if (resetCachedStateAfterHide)
            resetCachedStateWhenHidden = true;

        targetVisibilityAlpha = 0f;
    }

    /// <summary>
    /// Initializes current and target visibility without an animated transition.
    /// </summary>
    /// <param name="visible">True when the meter should start visible.</param>
    private void InitializeVisibility(bool visible)
    {
        currentVisibilityAlpha = visible ? 1f : 0f;
        targetVisibilityAlpha = currentVisibilityAlpha;
        visibilityStateInitialized = true;
        ApplyVisibilityAlpha(currentVisibilityAlpha);
        SetVisualPresence(visible);
    }

    /// <summary>
    /// Advances the root alpha toward its target and disables the authored hierarchy after fade-out completes.
    /// </summary>
    /// <param name="deltaTime">Frame delta time used for the visibility transition.</param>
    private void AdvanceVisibilityFade(float deltaTime)
    {
        EnsureBindings();

        if (!visibilityStateInitialized)
            InitializeVisibility(false);

        float targetAlpha = Mathf.Clamp01(targetVisibilityAlpha);
        float fadeDuration = targetAlpha > currentVisibilityAlpha
            ? HUDSynchroMeterWaveUtility.SanitizeNonNegative(fadeInDuration, 0f)
            : HUDSynchroMeterWaveUtility.SanitizeNonNegative(fadeOutDuration, 0f);

        if (Mathf.Abs(currentVisibilityAlpha - targetAlpha) > VisibilityComparisonEpsilon)
        {
            float alphaStep = fadeDuration <= 0f
                ? 1f
                : HUDSynchroMeterWaveUtility.SanitizeNonNegative(deltaTime, 0f) / fadeDuration;
            currentVisibilityAlpha = Mathf.MoveTowards(currentVisibilityAlpha, targetAlpha, alphaStep);
            ApplyVisibilityAlpha(currentVisibilityAlpha);
        }

        bool hasVisualPresence = currentVisibilityAlpha > VisibilityComparisonEpsilon ||
                                 targetAlpha > VisibilityComparisonEpsilon;
        SetVisualPresence(hasVisualPresence);

        if (hasVisualPresence || !resetCachedStateWhenHidden)
            return;

        ResetCachedPresentationState();
        resetCachedStateWhenHidden = false;
    }

    /// <summary>
    /// Shows or hides bound layers while respecting the independent layer toggles.
    /// </summary>
    /// <param name="visible">True when the authored meter hierarchy must remain renderable.</param>
    private void SetVisualPresence(bool visible)
    {
        bool usesProgressionText = visualMode == GameHudSynchroMeterVisualMode.ProgressionText;

        if (rootObject != null)
            rootObject.SetActive(visible);

        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(backgroundImage, visible && showBackground);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(coverImage, visible && showCover);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(primaryWaveLeadingImage, visible);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(primaryWaveTrailingImage, visible);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(secondaryWaveLeadingImage, visible);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(secondaryWaveTrailingImage, visible);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(rankText, visible && !usesProgressionText && showRankText);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(valueText, visible && !usesProgressionText && showValueText);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(progressFillImage, visible && !usesProgressionText && showProgressBar);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(progressBackgroundImage, visible && !usesProgressionText && showProgressBar);
        HUDSynchroMeterPresentationUtility.SetGraphicEnabled(progressionText, visible && usesProgressionText);
    }

    /// <summary>
    /// Applies visibility alpha through the authored CanvasGroup or through individual graphics as a safe fallback.
    /// </summary>
    /// <param name="alpha">Normalized visibility alpha.</param>
    private void ApplyVisibilityAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = clampedAlpha;
            return;
        }

        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(backgroundImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(coverImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(primaryWaveLeadingImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(primaryWaveTrailingImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(secondaryWaveLeadingImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(secondaryWaveTrailingImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(rankText, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(valueText, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(progressFillImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(progressBackgroundImage, clampedAlpha);
        HUDSynchroMeterPresentationUtility.ApplyGraphicAlpha(progressionText, clampedAlpha);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves authored scene bindings without creating UI components at runtime.
    /// </summary>
    private void EnsureBindings()
    {
        rootCanvasGroup = rootObject != null ? rootObject.GetComponent<CanvasGroup>() : null;

        if (rootCanvasGroup == null)
            return;

        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Selects scaled or unscaled frame time according to the baked HUD setting.
    /// </summary>
    /// <returns>Selected Unity frame delta time.</returns>
    private float ResolveDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    /// <summary>
    /// Invalidates cached TMP values so the next visible update reapplies authoritative content.
    /// </summary>
    private void ResetCachedPresentationState()
    {
        textPresentationState.Reset();
        displayedProgressNormalized = float.MinValue;
        targetProgressNormalized = 0f;
        displayedProgressPercentage = int.MinValue;
    }

    #endregion

    #endregion
}
