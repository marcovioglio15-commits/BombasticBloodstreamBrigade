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

    [Tooltip("Combo counter fallback theme and visibility behavior.")]
    [SerializeField] private GameHudComboCounterSettings comboCounterSettings = new GameHudComboCounterSettings();

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

    public GameHudComboCounterSettings ComboCounterSettings
    {
        get
        {
            return comboCounterSettings;
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

        if (comboCounterSettings == null)
            comboCounterSettings = new GameHudComboCounterSettings();

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
/// Combo counter fallback theme and visibility behavior.
/// </summary>
[Serializable]
public sealed class GameHudComboCounterSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the combo HUD section and its ECS-driven presentation updates.")]
    [SerializeField] private bool isEnabled = true;

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
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 0.18f;

    [Tooltip("Seconds used to fade the combo HUD when it becomes hidden.")]
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Tooltip("Fallback label shown before the first combo rank is reached.")]
    [SerializeField] private string idleRankLabel = "COMBO";
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public Color DefaultBadgeTint => defaultBadgeTint;
    public Color DefaultRankTextColor => defaultRankTextColor;
    public Color DefaultComboValueTextColor => defaultComboValueTextColor;
    public Color DefaultProgressFillColor => defaultProgressFillColor;
    public Color DefaultProgressBackgroundColor => defaultProgressBackgroundColor;
    public bool ShowRankBadgeImage => showRankBadgeImage;
    public bool ShowProgressBar => showProgressBar;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    public bool HideWhenZeroCombo => hideWhenZeroCombo;
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
