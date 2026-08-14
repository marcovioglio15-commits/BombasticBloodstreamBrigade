using System;
using UnityEngine;

/// <summary>
/// Scriptable preset that owns non-scene-reference runtime settings for the gameplay HUD.
/// </summary>
[CreateAssetMenu(fileName = "GameHudManagerPreset", menuName = "Game/HUD Manager Preset", order = 27)]
public sealed class GameHudManagerPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this HUD manager preset, used for stable editor references.")]
    [SerializeField] private string presetId;

    [Tooltip("HUD manager preset name displayed in Game Management Tool.")]
    [SerializeField] private string presetName = "New HUD Manager Preset";

    [Tooltip("Short description of this gameplay HUD configuration.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this HUD preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("HUD Sections")]
    [Tooltip("Level label and legacy experience bar runtime behavior.")]
    [SerializeField] private GameHudLevelExperienceSettings levelExperienceSettings = new GameHudLevelExperienceSettings();

    [Tooltip("Active power-up icon, energy and charge bar fallback behavior used when player visual config is unavailable.")]
    [SerializeField] private GameHudActivePowerUpSettings activePowerUpSettings = new GameHudActivePowerUpSettings();

    [Tooltip("Run timer ECS setup and fallback visibility behavior.")]
    [SerializeField] private GameHudRunTimerSettings runTimerSettings = new GameHudRunTimerSettings();

    [Tooltip("Synchro Meter wave animation, theme, text, and visibility behavior.")]
    [SerializeField] private GameHudSynchroMeterSettings synchroMeterSettings = new GameHudSynchroMeterSettings();

    [Tooltip("Milestone selection navigation and interaction behavior.")]
    [SerializeField] private GameHudMilestoneSelectionSettings milestoneSelectionSettings = new GameHudMilestoneSelectionSettings();

    [Tooltip("Damage vignette section toggles.")]
    [SerializeField] private GameHudDamageVignetteSettings damageVignetteSettings = new GameHudDamageVignetteSettings();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public GameHudLevelExperienceSettings LevelExperienceSettings
    {
        get
        {
            return levelExperienceSettings;
        }
    }

    public GameHudActivePowerUpSettings ActivePowerUpSettings
    {
        get
        {
            return activePowerUpSettings;
        }
    }

    public GameHudRunTimerSettings RunTimerSettings
    {
        get
        {
            return runTimerSettings;
        }
    }

    public GameHudSynchroMeterSettings SynchroMeterSettings
    {
        get
        {
            return synchroMeterSettings;
        }
    }

    public GameHudMilestoneSelectionSettings MilestoneSelectionSettings
    {
        get
        {
            return milestoneSelectionSettings;
        }
    }

    public GameHudDamageVignetteSettings DamageVignetteSettings
    {
        get
        {
            return damageVignetteSettings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested settings containers and stable metadata exist without clamping authored values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (levelExperienceSettings == null)
            levelExperienceSettings = new GameHudLevelExperienceSettings();

        if (activePowerUpSettings == null)
            activePowerUpSettings = new GameHudActivePowerUpSettings();

        if (runTimerSettings == null)
            runTimerSettings = new GameHudRunTimerSettings();

        if (synchroMeterSettings == null)
            synchroMeterSettings = new GameHudSynchroMeterSettings();

        if (milestoneSelectionSettings == null)
            milestoneSelectionSettings = new GameHudMilestoneSelectionSettings();

        if (damageVignetteSettings == null)
            damageVignetteSettings = new GameHudDamageVignetteSettings();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps required settings containers alive when the preset is edited.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}

/// <summary>
/// Level label and legacy experience bar runtime behavior.
/// </summary>
[Serializable]
public sealed class GameHudLevelExperienceSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Hide player level text when no player entity with PlayerLevel is available.")]
    [SerializeField] private bool hideLevelTextWhenPlayerMissing = true;

    [Tooltip("Seconds used to smooth visual experience fill transitions. Set 0 for immediate updates.")]
    [Min(0f)]
    [SerializeField] private float experienceBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide experience bar image when no player entity with progression runtime data is available.")]
    [SerializeField] private bool hideExperienceBarWhenPlayerMissing = true;

    [Tooltip("Enables the liquid shader layer for the legacy experience bar.")]
    [SerializeField] private bool enableLegacyExperienceLiquidShader = true;

    [Tooltip("Enables a plunger transform for the legacy experience bar when a scene reference is assigned.")]
    [SerializeField] private bool enableLegacyExperiencePiston;

    [Tooltip("Additional local X offset applied to the legacy experience plunger after fill positioning.")]
    [SerializeField] private float legacyExperiencePistonLocalOffsetX;

    [Tooltip("Additional local Y offset applied to the legacy experience plunger after fill positioning.")]
    [SerializeField] private float legacyExperiencePistonLocalOffsetY;

    [Tooltip("Enables a temporary liquid slosh pulse when the legacy experience target value changes.")]
    [SerializeField] private bool enableLegacyExperienceValueDeltaMotion = true;

    [Tooltip("Minimum normalized delta required before the legacy experience slosh pulse retriggers.")]
    [Min(0f)]
    [SerializeField] private float legacyExperienceDeltaTriggerThreshold = 0.0125f;

    [Tooltip("Multiplier applied to the legacy experience slosh pulse intensity generated by value changes.")]
    [Min(0f)]
    [SerializeField] private float legacyExperienceDeltaMotionStrength = 0.9f;

    [Tooltip("Seconds used to decay the legacy experience slosh pulse back to rest.")]
    [Min(0f)]
    [SerializeField] private float legacyExperienceDeltaMotionDecaySeconds = 0.3f;
    #endregion

    #endregion

    #region Properties
    public bool HideLevelTextWhenPlayerMissing => hideLevelTextWhenPlayerMissing;
    public float ExperienceBarSmoothingSeconds => experienceBarSmoothingSeconds;
    public bool HideExperienceBarWhenPlayerMissing => hideExperienceBarWhenPlayerMissing;
    public bool EnableLegacyExperienceLiquidShader => enableLegacyExperienceLiquidShader;
    public bool EnableLegacyExperiencePiston => enableLegacyExperiencePiston;
    public float LegacyExperiencePistonLocalOffsetX => legacyExperiencePistonLocalOffsetX;
    public float LegacyExperiencePistonLocalOffsetY => legacyExperiencePistonLocalOffsetY;
    public bool EnableLegacyExperienceValueDeltaMotion => enableLegacyExperienceValueDeltaMotion;
    public float LegacyExperienceDeltaTriggerThreshold => legacyExperienceDeltaTriggerThreshold;
    public float LegacyExperienceDeltaMotionStrength => legacyExperienceDeltaMotionStrength;
    public float LegacyExperienceDeltaMotionDecaySeconds => legacyExperienceDeltaMotionDecaySeconds;
    #endregion
}

/// <summary>
/// Active power-up fallback behavior used when ECS visual config is not available yet.
/// </summary>
[Serializable]
public sealed class GameHudActivePowerUpSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Seconds used to smooth energy fill transitions. Set 0 for immediate updates.")]
    [Min(0f)]
    [SerializeField] private float energyBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide energy bars when no player entity is available.")]
    [SerializeField] private bool hideEnergyBarsWhenPlayerMissing = true;

    [Tooltip("Hide energy bars when the corresponding slot has no energy module.")]
    [SerializeField] private bool hideEnergyBarsWhenModuleMissing = true;

    [Tooltip("Seconds used to smooth charge fill transitions. Set 0 for immediate updates.")]
    [Min(0f)]
    [SerializeField] private float chargeBarSmoothingSeconds = 0.05f;

    [Tooltip("Hide charge bars when no player entity is available.")]
    [SerializeField] private bool hideChargeBarsWhenPlayerMissing = true;

    [Tooltip("Hide charge bars when the corresponding slot has no charge module.")]
    [SerializeField] private bool hideChargeBarsWhenModuleMissing = true;
    #endregion

    #endregion

    #region Properties
    public float EnergyBarSmoothingSeconds => energyBarSmoothingSeconds;
    public bool HideEnergyBarsWhenPlayerMissing => hideEnergyBarsWhenPlayerMissing;
    public bool HideEnergyBarsWhenModuleMissing => hideEnergyBarsWhenModuleMissing;
    public float ChargeBarSmoothingSeconds => chargeBarSmoothingSeconds;
    public bool HideChargeBarsWhenPlayerMissing => hideChargeBarsWhenPlayerMissing;
    public bool HideChargeBarsWhenModuleMissing => hideChargeBarsWhenModuleMissing;
    #endregion
}

/// <summary>
/// Run timer ECS setup and fallback visibility behavior.
/// </summary>
[Serializable]
public sealed class GameHudRunTimerSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the run timer section and its authoritative ECS timer setup.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Direction used by the run timer. Backward counts down toward a defeat at zero.")]
    [SerializeField] private PlayerRunTimerDirection direction = PlayerRunTimerDirection.Backward;

    [Tooltip("Initial value in seconds used only when Direction is set to Backward.")]
    [Min(0f)]
    [SerializeField] private float initialSeconds = 450f;

    [Tooltip("Hides the timer text while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public PlayerRunTimerDirection Direction => direction;
    public float InitialSeconds => initialSeconds;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    #endregion
}

/// <summary>
/// Selects how single-rank Synchro Meter wave convergence advances through the progression window.
/// </summary>
public enum GameHudSynchroSingleRankConvergenceMode : byte
{
    Linear = 0,
    Steps = 1
}

/// <summary>
/// Selects the authored Synchro Meter overlay composition without changing its ECS-authoritative wave behavior.
/// </summary>
public enum GameHudSynchroMeterVisualMode : byte
{
    Standard = 0,
    ProgressionText = 1
}

/// <summary>
/// Selects the horizontal alignment of the optional Synchro Meter progression label.
/// </summary>
public enum GameHudSynchroMeterTextAlignment : byte
{
    Left = 0,
    Center = 1,
    Right = 2
}

/// <summary>
/// Stores Synchro Meter wave animation, theme, text, and visibility behavior.
/// </summary>
[Serializable]
public sealed class GameHudSynchroMeterSettings
{
    #region Constants
    public const string ProgressionPercentageToken = "[ProgressionPercentage]";
    public const string DefaultProgressionTextFormat = "Synchro Meter : [ProgressionPercentage]%";
    public const float DefaultProgressionTextFontSize = 14f;
    public const float DefaultProgressionTextWaveDistance = 7f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the Synchro Meter and its ECS-driven presentation updates.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Selects the standard rank, value, and progress-bar overlays or a single progression label below the waves.")]
    [SerializeField] private GameHudSynchroMeterVisualMode visualMode;

    [Tooltip("Tint applied to the oscilloscope background image.")]
    [SerializeField] private Color backgroundTint = Color.white;

    [Tooltip("Tint applied to the scanline cover rendered above both waves.")]
    [SerializeField] private Color coverTint = Color.white;

    [Tooltip("Tint applied to both seamless images composing the primary wave.")]
    [SerializeField] private Color primaryWaveTint = Color.white;

    [Tooltip("Tint applied to both seamless images composing the secondary wave.")]
    [SerializeField] private Color secondaryWaveTint = Color.white;

    [Tooltip("Color applied to the current synchro rank label.")]
    [SerializeField] private Color rankTextColor = Color.white;

    [Tooltip("Color applied to the current numeric synchro value.")]
    [SerializeField] private Color valueTextColor = Color.white;

    [Tooltip("Color applied to the optional progression label shown at the authored progress-bar position.")]
    [SerializeField] private Color progressionTextColor = Color.white;

    [Tooltip("Tint applied to the progression fill shown below the wave display.")]
    [SerializeField] private Color progressFillTint = new Color(0f, 0.85f, 1f, 1f);

    [Tooltip("Tint applied to the progression track shown below the wave display.")]
    [SerializeField] private Color progressBackgroundTint = new Color(0f, 0f, 0f, 0.65f);

    [Tooltip("Shows the oscilloscope background layer when its image is assigned.")]
    [SerializeField] private bool showBackground = true;

    [Tooltip("Shows the scanline cover layer when its image is assigned.")]
    [SerializeField] private bool showCover;

    [Tooltip("Shows the current rank label over the wave display.")]
    [SerializeField] private bool showRankText = true;

    [Tooltip("Shows the current numeric synchro value over the wave display.")]
    [SerializeField] private bool showValueText = true;

    [Tooltip("Shows rank progression below the wave display using the authoritative normalized combo progress.")]
    [SerializeField] private bool showProgressBar = true;

    [Tooltip("Text shown by Progression Text mode. Use [ProgressionPercentage] wherever the current numeric percentage must appear; add % explicitly when required.")]
    [SerializeField] private string progressionTextFormat = DefaultProgressionTextFormat;

    [Tooltip("Font size in pixels used by the optional progression label.")]
    [SerializeField] private float progressionTextFontSize = DefaultProgressionTextFontSize;

    [Tooltip("Horizontal alignment used by the optional progression label across its authored progress-bar width.")]
    [SerializeField] private GameHudSynchroMeterTextAlignment progressionTextAlignment = GameHudSynchroMeterTextAlignment.Center;

    [Tooltip("Vertical distance in pixels between the bottom of the wave reticle and the top of the optional progression label.")]
    [SerializeField] private float progressionTextWaveDistance = DefaultProgressionTextWaveDistance;

    [Header("Wave Motion")]
    [Tooltip("Number of complete wave-image tile cycles scrolled per second. Both waves share this rate so their relative phase remains stable.")]
    [SerializeField] private float waveScrollCyclesPerSecond = 0.12f;

    [Header("Rank Convergence")]
    [Tooltip("Normalized horizontal separation between the two waves at the first rank. A value of 1 represents one complete image tile.")]
    [SerializeField] private float lowestRankPhaseOffsetNormalized = 0.25f;

    [Tooltip("Normalized horizontal separation between the two waves at the maximum rank. Use 0 for complete overlap.")]
    [SerializeField] private float highestRankPhaseOffsetNormalized;

    [Tooltip("Exponent shaping phase convergence across rank indices. Values above 1 preserve separation longer; values below 1 synchronize earlier.")]
    [SerializeField] private float phaseOffsetResponseExponent = 1f;

    [Header("Single Rank Progression")]
    [Tooltip("Increases both wave scroll rates linearly from the base speed to the authored maximum while Single Rank Progression advances.")]
    [SerializeField] private bool singleRankAccelerateWavesWithProgress = true;

    [Tooltip("Wave-image tile cycles per second reached at full Single Rank Progression when acceleration is enabled.")]
    [SerializeField] private float singleRankMaximumWaveScrollCyclesPerSecond = 0.3f;

    [Tooltip("Controls whether Single Rank Progression converges the two waves continuously or through equally spaced progression steps.")]
    [SerializeField] private GameHudSynchroSingleRankConvergenceMode singleRankConvergenceMode;

    [Tooltip("Normalized horizontal separation between the two waves before Single Rank Progression convergence starts. A value of 1 represents one complete image tile.")]
    [SerializeField] private float singleRankInitialPhaseOffsetNormalized = 0.25f;

    [Tooltip("Normalized horizontal separation between the two waves after Single Rank Progression convergence ends. Use 0 for complete overlap.")]
    [SerializeField] private float singleRankFinalPhaseOffsetNormalized;

    [Tooltip("Single Rank Progression percentage at which the waves begin moving toward their final separation.")]
    [SerializeField] private float singleRankConvergenceStartProgressPercent;

    [Tooltip("Single Rank Progression percentage at which the waves reach their final separation.")]
    [SerializeField] private float singleRankConvergenceEndProgressPercent = 100f;

    [Tooltip("Number of equal convergence intervals used by Steps mode across the configured progression window.")]
    [SerializeField] private int singleRankConvergenceStepCount = 5;

    [Header("Shared Wave Transition")]
    [Tooltip("Seconds used to blend the secondary wave toward its new phase after a rank change.")]
    [SerializeField] private float phaseTransitionDuration = 0.3f;

    [Tooltip("Uses unscaled time for wave scrolling and phase blending so UI motion remains independent from gameplay time scale.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Seconds used to smooth the progression fill after authoritative combo progress changes. Use 0 for immediate updates.")]
    [SerializeField] private float progressSmoothingSeconds = 0.08f;

    [Tooltip("Hides the Synchro Meter while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Hides the Synchro Meter while the current synchro value is 0.")]
    [SerializeField] private bool hideWhenZeroValue = true;

    [Tooltip("Hides the Synchro Meter whenever the current value no longer reaches an authored rank threshold.")]
    [SerializeField] private bool hideWhenNoActiveRank = true;

    [Tooltip("Seconds used to fade the Synchro Meter when it becomes visible.")]
    [SerializeField] private float fadeInDuration = 0.18f;

    [Tooltip("Seconds used to fade the Synchro Meter when it becomes hidden.")]
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Tooltip("Fallback label shown before the first synchro rank is reached.")]
    [SerializeField] private string idleRankLabel = "SYNCHRO";
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public GameHudSynchroMeterVisualMode VisualMode => visualMode;
    public Color BackgroundTint => backgroundTint;
    public Color CoverTint => coverTint;
    public Color PrimaryWaveTint => primaryWaveTint;
    public Color SecondaryWaveTint => secondaryWaveTint;
    public Color RankTextColor => rankTextColor;
    public Color ValueTextColor => valueTextColor;
    public Color ProgressionTextColor => progressionTextColor;
    public Color ProgressFillTint => progressFillTint;
    public Color ProgressBackgroundTint => progressBackgroundTint;
    public bool ShowBackground => showBackground;
    public bool ShowCover => showCover;
    public bool ShowRankText => showRankText;
    public bool ShowValueText => showValueText;
    public bool ShowProgressBar => showProgressBar;
    public string ProgressionTextFormat => progressionTextFormat;
    public float ProgressionTextFontSize => progressionTextFontSize;
    public GameHudSynchroMeterTextAlignment ProgressionTextAlignment => progressionTextAlignment;
    public float ProgressionTextWaveDistance => progressionTextWaveDistance;
    public float WaveScrollCyclesPerSecond => waveScrollCyclesPerSecond;
    public float LowestRankPhaseOffsetNormalized => lowestRankPhaseOffsetNormalized;
    public float HighestRankPhaseOffsetNormalized => highestRankPhaseOffsetNormalized;
    public float PhaseOffsetResponseExponent => phaseOffsetResponseExponent;
    public bool SingleRankAccelerateWavesWithProgress => singleRankAccelerateWavesWithProgress;
    public float SingleRankMaximumWaveScrollCyclesPerSecond => singleRankMaximumWaveScrollCyclesPerSecond;
    public GameHudSynchroSingleRankConvergenceMode SingleRankConvergenceMode => singleRankConvergenceMode;
    public float SingleRankInitialPhaseOffsetNormalized => singleRankInitialPhaseOffsetNormalized;
    public float SingleRankFinalPhaseOffsetNormalized => singleRankFinalPhaseOffsetNormalized;
    public float SingleRankConvergenceStartProgressPercent => singleRankConvergenceStartProgressPercent;
    public float SingleRankConvergenceEndProgressPercent => singleRankConvergenceEndProgressPercent;
    public int SingleRankConvergenceStepCount => singleRankConvergenceStepCount;
    public float PhaseTransitionDuration => phaseTransitionDuration;
    public bool UseUnscaledTime => useUnscaledTime;
    public float ProgressSmoothingSeconds => progressSmoothingSeconds;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    public bool HideWhenZeroValue => hideWhenZeroValue;
    public bool HideWhenNoActiveRank => hideWhenNoActiveRank;
    public float FadeInDuration => fadeInDuration;
    public float FadeOutDuration => fadeOutDuration;
    public string IdleRankLabel => idleRankLabel;
    #endregion
}

/// <summary>
/// Milestone selection navigation and interaction behavior.
/// </summary>
[Serializable]
public sealed class GameHudMilestoneSelectionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, milestone option titles are shown without the generated numeric prefix.")]
    [SerializeField] private bool hideOptionTitleNumbers = true;

    [Tooltip("Child object name used to auto-discover the skip hold-confirmation fill image.")]
    [SerializeField] private string skipHoldFillImageName = "SkipHoldFill";

    [Tooltip("Configures the skip hold fill Image as a horizontal left-to-right fill at runtime.")]
    [SerializeField] private bool configureSkipHoldFillImage = true;

    [Tooltip("Automatically discovers card views under PowerUpsPanel/PowerUpList and uses them for image-style selection.")]
    [SerializeField] private bool autoDiscoverOptionViewsFromPanelRoot = true;

    [Tooltip("Minimum Navigate axis magnitude required before a custom card-navigation step is accepted.")]
    [Range(0f, 1f)]
    [SerializeField] private float navigationInputDeadzone = 0.5f;

    [Tooltip("Minimum unscaled time required between two accepted custom navigation steps.")]
    [Min(0f)]
    [SerializeField] private float navigationRepeatCooldownSeconds = 0.15f;

    [Tooltip("Loops the current selection from last card to first card and vice versa.")]
    [SerializeField] private bool wrapNavigation = true;

    [Tooltip("Moves the current keyboard or gamepad selection to the card under the mouse pointer.")]
    [SerializeField] private bool followPointerHoverSelection = true;

    [Tooltip("Disables default EventSystem navigation while the milestone panel is open to avoid duplicate Submit/Navigate processing.")]
    [SerializeField] private bool suspendEventSystemNavigationWhileSelectionActive = true;

    [Tooltip("Automatically queues the first rolled offer when no selection UI and no skip button are configured.")]
    [SerializeField] private bool autoSelectFirstOfferWhenUiMissing = true;

    [Tooltip("Blocks further card and skip interactions immediately after a command is queued.")]
    [SerializeField] private bool lockButtonsAfterSelectionClick = true;
    #endregion

    #endregion

    #region Properties
    public bool HideOptionTitleNumbers => hideOptionTitleNumbers;
    public string SkipHoldFillImageName => skipHoldFillImageName;
    public bool ConfigureSkipHoldFillImage => configureSkipHoldFillImage;
    public bool AutoDiscoverOptionViewsFromPanelRoot => autoDiscoverOptionViewsFromPanelRoot;
    public float NavigationInputDeadzone => navigationInputDeadzone;
    public float NavigationRepeatCooldownSeconds => navigationRepeatCooldownSeconds;
    public bool WrapNavigation => wrapNavigation;
    public bool FollowPointerHoverSelection => followPointerHoverSelection;
    public bool SuspendEventSystemNavigationWhileSelectionActive => suspendEventSystemNavigationWhileSelectionActive;
    public bool AutoSelectFirstOfferWhenUiMissing => autoSelectFirstOfferWhenUiMissing;
    public bool LockButtonsAfterSelectionClick => lockButtonsAfterSelectionClick;
    #endregion
}

/// <summary>
/// Damage vignette section toggles.
/// </summary>
[Serializable]
public sealed class GameHudDamageVignetteSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Master toggle for the damage vignette overlays.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Hide both vignettes when no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    #endregion
}
