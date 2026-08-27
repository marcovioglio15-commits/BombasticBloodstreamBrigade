using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Selects the screen edge used by the collapsible power-up summary panel.
/// </summary>
public enum GameHudSummaryPanelSide : byte
{
    Left = 0,
    Right = 1
}

/// <summary>
/// Selects which power-up category occupies the first horizontal column.
/// </summary>
public enum GameHudSummaryPowerUpOrder : byte
{
    ActiveFirst = 0,
    PassiveFirst = 1
}

/// <summary>
/// Selects which collected power-up categories occupy the upper summary area.
/// </summary>
public enum GameHudSummaryPowerUpVisibility : byte
{
    ActiveAndPassive = 0,
    ActiveOnly = 1,
    PassiveOnly = 2
}

/// <summary>
/// Selects the easing curve used by the authored summary-panel slide.
/// </summary>
public enum GameHudSummarySlideEasing : byte
{
    Linear = 0,
    SmoothStep = 1,
    EaseOutCubic = 2
}

/// <summary>
/// Identifies an ECS-authoritative player value available to the summary statistic selector.
/// </summary>
public enum GameHudPlayerStatistic : byte
{
    CurrentHealth = 0,
    MaximumHealth = 1,
    CurrentShield = 2,
    MaximumShield = 3,
    Level = 4,
    CurrentExperience = 5,
    ExperienceForNextLevel = 6,
    ExperienceProgress = 7,
    ExperiencePickupRadius = 8,
    MovementBaseSpeed = 9,
    MovementMaximumSpeed = 10,
    MovementAcceleration = 11,
    MovementDeceleration = 12,
    LookRotationSpeed = 13,
    ProjectileSpeed = 14,
    RateOfFire = 15,
    ProjectileDamage = 16,
    ProjectileRange = 17,
    ProjectileLifetime = 18,
    ProjectileSizeMultiplier = 19,
    SynchroValue = 20,
    SynchroProgress = 21,
    RunTimeSeconds = 22,
    CustomScalableStat = 23
}

/// <summary>
/// Selects how one resolved statistic value is converted into player-facing text.
/// </summary>
public enum GameHudStatisticValueFormat : byte
{
    Automatic = 0,
    Number = 1,
    Percentage = 2,
    Seconds = 3,
    Multiplier = 4,
    Boolean = 5,
    Token = 6
}

/// <summary>
/// Stores the selectable statistic and presentation style for one preauthored summary row.
/// </summary>
[Serializable]
public sealed class GameHudStatisticDisplayDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Statistic")]
    [Tooltip("ECS-authoritative player value shown by this row. Custom Scalable Stat exposes the selectable scalable-stat catalog in Game Management Tool.")]
    [SerializeField] private GameHudPlayerStatistic statistic;

    [Tooltip("Stable scalable-stat name selected from Player Progression presets. Game Management Tool edits this value through a dropdown instead of free text.")]
    [SerializeField] private string scalableStatName;

    [Tooltip("Optional label replacing the default name generated for the selected statistic.")]
    [SerializeField] private string labelOverride;

    [Header("Value Formatting")]
    [Tooltip("Presentation format applied after the ECS value is resolved.")]
    [SerializeField] private GameHudStatisticValueFormat valueFormat;

    [Tooltip("Decimal digits displayed by numeric, percentage, seconds, and multiplier formats.")]
    [SerializeField] private int decimalPlaces = 1;

    [Tooltip("Multiplier applied to the resolved numeric value before text formatting.")]
    [SerializeField] private float displayMultiplier = 1f;

    [Tooltip("Optional suffix appended after the formatted value, including any desired leading space.")]
    [SerializeField] private string suffix;

    [Tooltip("Shows the resolved or overridden statistic label before the formatted value.")]
    [SerializeField] private bool showLabel = true;

    [Tooltip("Text shown when a Boolean statistic evaluates to true.")]
    [SerializeField] private string trueText = "On";

    [Tooltip("Text shown when a Boolean statistic evaluates to false.")]
    [SerializeField] private string falseText = "Off";

    [Header("Text Style")]
    [Tooltip("Optional font asset used by this row. The preauthored row font is retained when this is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Tooltip("Font size used by this statistic row in pixels.")]
    [SerializeField] private float fontSize = 18f;

    [Tooltip("Font style applied to this statistic row.")]
    [SerializeField] private FontStyles fontStyle;

    [Tooltip("Text color applied to this statistic row.")]
    [SerializeField] private Color color = Color.white;
    #endregion

    #endregion

    #region Properties
    public GameHudPlayerStatistic Statistic => statistic;
    public string ScalableStatName => scalableStatName;
    public string LabelOverride => labelOverride;
    public GameHudStatisticValueFormat ValueFormat => valueFormat;
    public int DecimalPlaces => decimalPlaces;
    public float DisplayMultiplier => displayMultiplier;
    public string Suffix => suffix;
    public bool ShowLabel => showLabel;
    public string TrueText => trueText;
    public string FalseText => falseText;
    public TMP_FontAsset Font => font;
    public float FontSize => fontSize;
    public FontStyles FontStyle => fontStyle;
    public Color Color => color;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Configures the core selector and label values used when creating a default statistic row.
    /// </summary>
    /// <param name="statisticValue">Built-in statistic selected for the row.</param>
    /// <param name="labelValue">Optional label override shown before its value.</param>
    public void Configure(GameHudPlayerStatistic statisticValue, string labelValue)
    {
        statistic = statisticValue;
        labelOverride = labelValue;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the inline HUD configuration for the collapsible power-up inventory and player-stat summary.
/// </summary>
[Serializable]
public sealed class GameHudPowerUpSummarySettings
{
    #region Fields

    #region Constants
    public const int AuthoredActiveSlotCapacity = 24;
    public const int AuthoredPassiveSlotCapacity = 48;
    public const int AuthoredStatisticRowCapacity = 24;
    #endregion

    #region Serialized Fields
    [Header("Availability")]
    [Tooltip("Enables the preauthored power-up and statistic summary HUD section.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Keeps the panel expanded when gameplay HUD presentation starts.")]
    [SerializeField] private bool startsExpanded;

    [Tooltip("Hides the section while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Header("Panel Layout")]
    [Tooltip("Screen edge used to anchor the collapsible panel and its toggle handle.")]
    [SerializeField] private GameHudSummaryPanelSide panelSide = GameHudSummaryPanelSide.Right;

    [Tooltip("Controls whether active or passive power-ups occupy the first horizontal column.")]
    [SerializeField] private GameHudSummaryPowerUpOrder powerUpOrder;

    [Tooltip("Chooses whether the upper summary area shows both power-up categories, active power-ups only, or passive power-ups only.")]
    [SerializeField]
    private GameHudSummaryPowerUpVisibility powerUpVisibility;

    [Tooltip("Expanded panel width in pixels. Runtime presentation clamps invalid values without rewriting this preset.")]
    [SerializeField] private float expandedWidth = 520f;

    [Tooltip("Visible handle width retained when the panel is collapsed.")]
    [SerializeField] private float collapsedHandleWidth = 42f;

    [Tooltip("Outer padding applied inside the panel content in pixels.")]
    [SerializeField] private float contentPadding = 16f;

    [Tooltip("Horizontal distance separating the active and passive power-up columns.")]
    [SerializeField] private float powerUpColumnSpacing = 14f;

    [Tooltip("Vertical distance separating the power-up area from the statistic area.")]
    [SerializeField] private float sectionSpacing = 16f;

    [Tooltip("Normalized portion of the panel height reserved for the upper power-up area.")]
    [SerializeField] private float powerUpAreaHeightNormalized = 0.58f;

    [Header("Panel Motion")]
    [Tooltip("Unscaled seconds used to expand or collapse the panel.")]
    [SerializeField] private float slideDurationSeconds = 0.22f;

    [Tooltip("Curve used to shape the manual panel slide interpolation.")]
    [SerializeField] private GameHudSummarySlideEasing slideEasing = GameHudSummarySlideEasing.EaseOutCubic;

    [Tooltip("Uses unscaled time so panel motion remains responsive while gameplay is paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Allows the dedicated Input Action to expand or collapse the summary while gameplay input is active.")]
    [SerializeField] private bool enableInputToggle = true;

    [Tooltip("Stable Input Action ID used to expand or collapse the summary without depending on an action name at runtime.")]
    [SerializeField] private string toggleActionId;

    [Tooltip("Allows the preauthored panel handle to expand or collapse the summary with a pointer click.")]
    [SerializeField] private bool enableClickToggle = true;

    [Header("Power-Up Grid")]
    [Tooltip("Maximum active entries allowed to use the preauthored active-slot pool.")]
    [SerializeField] private int maximumVisibleActivePowerUps = AuthoredActiveSlotCapacity;

    [Tooltip("Maximum passive entries allowed to use the preauthored passive-slot pool.")]
    [SerializeField] private int maximumVisiblePassivePowerUps = AuthoredPassiveSlotCapacity;

    [Tooltip("Square power-up icon size in pixels.")]
    [SerializeField] private float iconSize = 52f;

    [Tooltip("Horizontal and vertical gap between adjacent power-up icons.")]
    [SerializeField] private float iconSpacing = 8f;

    [Tooltip("Tint applied to every resolved power-up icon.")]
    [SerializeField] private Color iconTint = Color.white;

    [Tooltip("Optional sprite drawn behind each power-up icon.")]
    [SerializeField] private Sprite iconBackgroundSprite;

    [Tooltip("Tint applied to the optional icon background sprite.")]
    [SerializeField] private Color iconBackgroundTint = new Color(0.08f, 0.08f, 0.1f, 0.9f);

    [Tooltip("Hides the Active title and grid when the player has collected no active power-ups.")]
    [SerializeField] private bool hideEmptyActiveColumn;

    [Tooltip("Hides the Passive title and grid when the player has collected no passive power-ups.")]
    [SerializeField] private bool hideEmptyPassiveColumn;

    [Header("Power-Up Counter")]
    [Tooltip("Optional font used by the collection count displayed along the lower edge of each icon.")]
    [SerializeField] private TMP_FontAsset counterFont;

    [Tooltip("Font size used by the collection count displayed on each icon.")]
    [SerializeField] private float counterFontSize = 16f;

    [Tooltip("Color used by the collection count displayed on each icon.")]
    [SerializeField] private Color counterColor = Color.white;

    [Tooltip("Text placed before the collected quantity, for example x or an empty string.")]
    [SerializeField] private string counterPrefix = "x";

    [Tooltip("Shows a counter even when the collected quantity equals one.")]
    [SerializeField] private bool showSingleCollectionCount = true;

    [Header("Section Titles")]
    [Tooltip("Text shown above the active power-up grid.")]
    [SerializeField] private string activeTitle = "ACTIVE";

    [Tooltip("Text shown above the passive power-up grid.")]
    [SerializeField] private string passiveTitle = "PASSIVE";

    [Tooltip("Text shown above the player statistic rows.")]
    [SerializeField] private string statisticsTitle = "PLAYER STATS";

    [Tooltip("Optional font shared by the Active, Passive, and Player Stats section titles.")]
    [SerializeField] private TMP_FontAsset titleFont;

    [Tooltip("Font size shared by summary section titles.")]
    [SerializeField] private float titleFontSize = 20f;

    [Tooltip("Color shared by summary section titles.")]
    [SerializeField] private Color titleColor = Color.white;

    [Header("Separators")]
    [Tooltip("Shows a vertical separator between active and passive columns.")]
    [SerializeField] private bool showPowerUpColumnSeparator = true;

    [Tooltip("Shows a horizontal separator between power-ups and player statistics.")]
    [SerializeField] private bool showStatisticsSeparator = true;

    [Tooltip("Color shared by the vertical and horizontal separator graphics.")]
    [SerializeField] private Color separatorColor = new Color(1f, 1f, 1f, 0.28f);

    [Tooltip("Thickness in pixels shared by the separator graphics.")]
    [SerializeField] private float separatorThickness = 1f;

    [Header("Panel Style")]
    [Tooltip("Optional sprite used by the expanded panel background.")]
    [SerializeField] private Sprite backgroundSprite;

    [Tooltip("Tint applied to the expanded panel background.")]
    [SerializeField] private Color backgroundTint = new Color(0.025f, 0.025f, 0.04f, 0.94f);

    [Tooltip("Optional sprite used by the expand and collapse handle.")]
    [SerializeField] private Sprite toggleSprite;

    [Tooltip("Tint applied to the expand and collapse handle.")]
    [SerializeField] private Color toggleTint = Color.white;

    [Header("Statistic Refresh")]
    [Tooltip("Minimum unscaled seconds between ECS statistic refreshes and hashed power-up catalog checks.")]
    [SerializeField] private float statisticRefreshIntervalSeconds = 0.1f;

    [Tooltip("Ordered player statistics rendered in the lower panel through preauthored row slots.")]
    [SerializeField] private List<GameHudStatisticDisplayDefinition> statistics = new List<GameHudStatisticDisplayDefinition>();
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public bool StartsExpanded => startsExpanded;
    public bool HideWhenPlayerMissing => hideWhenPlayerMissing;
    public GameHudSummaryPanelSide PanelSide => panelSide;
    public GameHudSummaryPowerUpOrder PowerUpOrder => powerUpOrder;
    public GameHudSummaryPowerUpVisibility PowerUpVisibility => powerUpVisibility;
    public float ExpandedWidth => expandedWidth;
    public float CollapsedHandleWidth => collapsedHandleWidth;
    public float ContentPadding => contentPadding;
    public float PowerUpColumnSpacing => powerUpColumnSpacing;
    public float SectionSpacing => sectionSpacing;
    public float PowerUpAreaHeightNormalized => powerUpAreaHeightNormalized;
    public float SlideDurationSeconds => slideDurationSeconds;
    public GameHudSummarySlideEasing SlideEasing => slideEasing;
    public bool UseUnscaledTime => useUnscaledTime;
    public bool EnableInputToggle => enableInputToggle;
    public string ToggleActionId => toggleActionId;
    public bool EnableClickToggle => enableClickToggle;
    public int MaximumVisibleActivePowerUps => maximumVisibleActivePowerUps;
    public int MaximumVisiblePassivePowerUps => maximumVisiblePassivePowerUps;
    public float IconSize => iconSize;
    public float IconSpacing => iconSpacing;
    public Color IconTint => iconTint;
    public Sprite IconBackgroundSprite => iconBackgroundSprite;
    public Color IconBackgroundTint => iconBackgroundTint;
    public bool HideEmptyActiveColumn => hideEmptyActiveColumn;
    public bool HideEmptyPassiveColumn => hideEmptyPassiveColumn;
    public TMP_FontAsset CounterFont => counterFont;
    public float CounterFontSize => counterFontSize;
    public Color CounterColor => counterColor;
    public string CounterPrefix => counterPrefix;
    public bool ShowSingleCollectionCount => showSingleCollectionCount;
    public string ActiveTitle => activeTitle;
    public string PassiveTitle => passiveTitle;
    public string StatisticsTitle => statisticsTitle;
    public TMP_FontAsset TitleFont => titleFont;
    public float TitleFontSize => titleFontSize;
    public Color TitleColor => titleColor;
    public bool ShowPowerUpColumnSeparator => showPowerUpColumnSeparator;
    public bool ShowStatisticsSeparator => showStatisticsSeparator;
    public Color SeparatorColor => separatorColor;
    public float SeparatorThickness => separatorThickness;
    public Sprite BackgroundSprite => backgroundSprite;
    public Color BackgroundTint => backgroundTint;
    public Sprite ToggleSprite => toggleSprite;
    public Color ToggleTint => toggleTint;
    public float StatisticRefreshIntervalSeconds => statisticRefreshIntervalSeconds;
    public IReadOnlyList<GameHudStatisticDisplayDefinition> Statistics => statistics;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures required statistic storage exists without rewriting authored tuning values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (statistics == null)
            statistics = new List<GameHudStatisticDisplayDefinition>();
    }
    #endregion

    #endregion
}
