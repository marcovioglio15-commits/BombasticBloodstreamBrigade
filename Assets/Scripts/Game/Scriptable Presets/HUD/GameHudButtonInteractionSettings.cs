using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Identifies independently configurable runtime menu groups.
/// </summary>
public enum GameUiMenuKind : byte
{
    MainMenu = 0,
    PauseMenu = 1,
    SettingsMenu = 2,
    EndingMenu = 3,
    MilestoneSelection = 4,
    PowerUpContainer = 5,
    PowerUpSummary = 6,
    RuntimeTools = 7
}

/// <summary>
/// Selects which authored transition paths are applied to menu buttons.
/// </summary>
public enum GameUiButtonMotionMode : byte
{
    None = 0,
    ManualTransform = 1,
    AnimationClips = 2,
    ManualTransformAndClips = 3
}

/// <summary>
/// Selects whether hover transform feedback holds its target or completes pulse cycles.
/// </summary>
public enum GameUiButtonHoverTransformMode : byte
{
    HoldTarget = 0,
    Pulse = 1
}

/// <summary>
/// Stores independent hover, focus, press, sprite, and text behavior for one menu group.
/// </summary>
[Serializable]
public sealed class GameUiMenuButtonInteractionDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Target Menu")]
    [Tooltip("Menu group receiving this button interaction profile.")]
    [SerializeField] private GameUiMenuKind menuKind = GameUiMenuKind.MainMenu;

    [Tooltip("Enables this interaction profile without disabling the underlying Button controls.")]
    [SerializeField] private bool isEnabled = true;

    [Header("Motion")]
    [Tooltip("Selects manual RectTransform motion, authored clips, both paths, or no motion.")]
    [SerializeField] private GameUiButtonMotionMode motionMode = GameUiButtonMotionMode.ManualTransform;

    [Tooltip("Seconds used to blend manual transform states and sample transition clips through the selected time source.")]
    [SerializeField] private float transitionDurationSeconds = 0.12f;

    [Tooltip("Uses unscaled time so menu feedback remains responsive while gameplay is paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Selects a held hover transform or a complete baseline-to-peak-to-baseline pulse.")]
    [SerializeField] private GameUiButtonHoverTransformMode hoverTransformMode;

    [Tooltip("Seconds required for one complete hover pulse cycle.")]
    [SerializeField] private float hoverPulseCycleSeconds = 0.34f;

    [Tooltip("Number of complete hover pulse cycles played after pointer entry when looping is disabled.")]
    [SerializeField] private int hoverPulseCycles = 1;

    [Tooltip("Repeats complete hover pulse cycles until the pointer exits or another interaction state takes priority.")]
    [SerializeField] private bool loopHoverPulse;

    [Tooltip("Local scale applied while a pointer hovers the button or it owns keyboard or gamepad focus.")]
    [SerializeField] private Vector3 hoverScale = new Vector3(1.04f, 1.04f, 1f);

    [Tooltip("Local position offset applied while a pointer hovers the button or it owns keyboard or gamepad focus.")]
    [SerializeField] private Vector3 hoverPositionOffset;

    [Tooltip("Local Euler rotation offset applied while a pointer hovers the button or it owns keyboard or gamepad focus.")]
    [SerializeField] private Vector3 hoverRotationOffset;

    [Tooltip("Local scale applied while the button is pressed.")]
    [SerializeField] private Vector3 pressedScale = new Vector3(0.97f, 0.97f, 1f);

    [Tooltip("Local position offset applied while the button is pressed.")]
    [SerializeField] private Vector3 pressedPositionOffset;

    [Tooltip("Local Euler rotation offset applied while the button is pressed.")]
    [SerializeField] private Vector3 pressedRotationOffset;

    [Header("Animation Clips")]
    [Tooltip("Optional clip sampled when a button returns to its normal state.")]
    [SerializeField] private AnimationClip normalClip;

    [Tooltip("Optional clip sampled when a button is hovered or receives keyboard or gamepad focus.")]
    [SerializeField] private AnimationClip hoverClip;

    [Tooltip("Optional clip sampled when a button is pressed.")]
    [SerializeField] private AnimationClip pressedClip;

    [Tooltip("Optional clip sampled when a button becomes non-interactable.")]
    [SerializeField] private AnimationClip disabledClip;

    [Header("Sprite Overrides")]
    [Tooltip("Overrides the target Button image sprite for each interaction state.")]
    [SerializeField] private bool overrideSprites;

    [Tooltip("Keeps empty sprite overrides as None and hides the Button image while retaining its pointer hit area, leaving only the label visible.")]
    [SerializeField]
    private bool allowEmptySprites;

    [Tooltip("Optional sprite used by the normal button state.")]
    [SerializeField] private Sprite normalSprite;

    [Tooltip("Optional sprite used by pointer hover and keyboard or gamepad focus.")]
    [SerializeField] private Sprite hoverSprite;

    [Tooltip("Optional sprite used while the button is pressed.")]
    [SerializeField] private Sprite pressedSprite;

    [Tooltip("Optional sprite used while the button is non-interactable.")]
    [SerializeField] private Sprite disabledSprite;

    [Header("Graphic Colors")]
    [Tooltip("Overrides target Button graphic colors for every interaction state.")]
    [SerializeField] private bool overrideGraphicColors;

    [Tooltip("Target graphic color used by the normal state.")]
    [SerializeField] private Color normalGraphicColor = Color.white;

    [Tooltip("Target graphic color used by pointer hover and keyboard or gamepad focus.")]
    [SerializeField] private Color hoverGraphicColor = Color.white;

    [Tooltip("Target graphic color used while the button is pressed.")]
    [SerializeField] private Color pressedGraphicColor = Color.white;

    [Tooltip("Target graphic color used while the button is non-interactable.")]
    [SerializeField] private Color disabledGraphicColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Text Style")]
    [Tooltip("Overrides child TMP text presentation for every button in this menu group.")]
    [SerializeField] private bool overrideTextStyle;

    [Tooltip("Optional font used while buttons are in their normal state.")]
    [SerializeField] private TMP_FontAsset normalFont;

    [Tooltip("Optional font used while buttons are hovered, selected, or pressed.")]
    [SerializeField] private TMP_FontAsset emphasizedFont;

    [Tooltip("Normal-state button label size in pixels.")]
    [SerializeField] private float normalFontSize = 24f;

    [Tooltip("Hovered, selected, and pressed button label size in pixels.")]
    [SerializeField] private float emphasizedFontSize = 26f;

    [Tooltip("Normal-state button label style.")]
    [SerializeField] private FontStyles normalFontStyle;

    [Tooltip("Hovered, selected, and pressed button label style.")]
    [SerializeField] private FontStyles emphasizedFontStyle = FontStyles.Bold;

    [Tooltip("Normal-state button label color.")]
    [SerializeField] private Color normalTextColor = Color.white;

    [Tooltip("Button label color used by pointer hover and keyboard or gamepad focus.")]
    [SerializeField] private Color hoverTextColor = Color.white;

    [Tooltip("Pressed button label color.")]
    [SerializeField] private Color pressedTextColor = Color.white;

    [Tooltip("Non-interactable button label color.")]
    [SerializeField] private Color disabledTextColor = new Color(1f, 1f, 1f, 0.45f);
    #endregion

    #endregion

    #region Properties
    public GameUiMenuKind MenuKind => menuKind;
    public bool IsEnabled => isEnabled;
    public GameUiButtonMotionMode MotionMode => motionMode;
    public float TransitionDurationSeconds => transitionDurationSeconds;
    public bool UseUnscaledTime => useUnscaledTime;
    public GameUiButtonHoverTransformMode HoverTransformMode => hoverTransformMode;
    public float HoverPulseCycleSeconds => hoverPulseCycleSeconds;
    public int HoverPulseCycles => hoverPulseCycles;
    public bool LoopHoverPulse => loopHoverPulse;
    public Vector3 HoverScale => hoverScale;
    public Vector3 HoverPositionOffset => hoverPositionOffset;
    public Vector3 HoverRotationOffset => hoverRotationOffset;
    public Vector3 PressedScale => pressedScale;
    public Vector3 PressedPositionOffset => pressedPositionOffset;
    public Vector3 PressedRotationOffset => pressedRotationOffset;
    public AnimationClip NormalClip => normalClip;
    public AnimationClip HoverClip => hoverClip;
    public AnimationClip PressedClip => pressedClip;
    public AnimationClip DisabledClip => disabledClip;
    public bool OverrideSprites => overrideSprites;
    public bool AllowEmptySprites => allowEmptySprites;
    public Sprite NormalSprite => normalSprite;
    public Sprite HoverSprite => hoverSprite;
    public Sprite PressedSprite => pressedSprite;
    public Sprite DisabledSprite => disabledSprite;
    public bool OverrideGraphicColors => overrideGraphicColors;
    public Color NormalGraphicColor => normalGraphicColor;
    public Color HoverGraphicColor => hoverGraphicColor;
    public Color PressedGraphicColor => pressedGraphicColor;
    public Color DisabledGraphicColor => disabledGraphicColor;
    public bool OverrideTextStyle => overrideTextStyle;
    public TMP_FontAsset NormalFont => normalFont;
    public TMP_FontAsset EmphasizedFont => emphasizedFont;
    public float NormalFontSize => normalFontSize;
    public float EmphasizedFontSize => emphasizedFontSize;
    public FontStyles NormalFontStyle => normalFontStyle;
    public FontStyles EmphasizedFontStyle => emphasizedFontStyle;
    public Color NormalTextColor => normalTextColor;
    public Color HoverTextColor => hoverTextColor;
    public Color PressedTextColor => pressedTextColor;
    public Color DisabledTextColor => disabledTextColor;
    #endregion
}

/// <summary>
/// Stores independently selectable button interaction profiles for every runtime menu group.
/// </summary>
[Serializable]
public sealed class GameHudButtonInteractionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Independent interaction profiles matched to preauthored runtime menu groups.")]
    [SerializeField] private List<GameUiMenuButtonInteractionDefinition> menuProfiles = new List<GameUiMenuButtonInteractionDefinition>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameUiMenuButtonInteractionDefinition> MenuProfiles => menuProfiles;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Restores the required profile collection without changing authored entries.
    /// </summary>
    public void EnsureInitialized()
    {
        if (menuProfiles == null)
            menuProfiles = new List<GameUiMenuButtonInteractionDefinition>();
    }
    #endregion

    #endregion
}
