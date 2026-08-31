using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Selects the horizontal direction followed by the room-clear announcement.
/// </summary>
public enum GameHudWaveClearAnnouncementDirection : byte
{
    LeftToRight = 0,
    RightToLeft = 1
}

/// <summary>
/// Selects the velocity profile used before and after the announcement reaches screen center.
/// </summary>
public enum GameHudWaveClearAnnouncementEasing : byte
{
    Linear = 0,
    SmoothStep = 1,
    DecelerateAtCenter = 2
}

/// <summary>
/// Selects the traversal or paint-reveal presentation used by a room-clear announcement.
/// </summary>
public enum GameHudWaveClearAnnouncementPresentationMode : byte
{
    Traversal = 0,
    PaintReveal = 1
}

/// <summary>
/// Stores standard and terminal-Boss content, motion, audio, placement, and style for the preauthored room-clear announcement.
/// </summary>
[Serializable]
public sealed class GameHudWaveClearAnnouncementSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Availability")]
    [Tooltip("Shows the preauthored announcement whenever the authoritative active room is cleared.")]
    [SerializeField]
    private bool isEnabled = true;

    [Tooltip("Text that crosses the gameplay HUD after a standard room is cleared.")]
    [SerializeField]
    private string content = "ROOM CLEARED";

    [Tooltip("Requests the selected Audio Manager event when the standard room-clear message starts.")]
    [SerializeField]
    private bool playAudioEvent;

    [Tooltip("Audio Manager event requested with the standard room-clear message.")]
    [SerializeField]
    private GameAudioEventId audioEventId = GameAudioEventId.WaveClear;

    [Header("Motion")]
    [Tooltip("Selects an edge-to-edge traversal or a stationary aerosol-paint reveal at screen center.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementPresentationMode presentationMode =
        GameHudWaveClearAnnouncementPresentationMode.PaintReveal;

    [Tooltip("Screen edge from which the announcement enters before continuing through the opposite edge.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementDirection direction = GameHudWaveClearAnnouncementDirection.RightToLeft;

    [Tooltip("Direction followed by the atomizing removal front after the standard paint announcement hold.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementDirection paintExitDirection =
        GameHudWaveClearAnnouncementDirection.RightToLeft;

    [Tooltip("Seconds used for the complete edge-to-edge traversal, excluding the optional center hold.")]
    [SerializeField]
    private float traversalDurationSeconds = 1.4f;

    [Tooltip("Velocity profile applied independently to the incoming and outgoing halves of the traversal.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementEasing easing = GameHudWaveClearAnnouncementEasing.DecelerateAtCenter;

    [Tooltip("Pauses the announcement at screen center before it continues toward the opposite edge.")]
    [SerializeField]
    private bool pauseAtCenter = true;

    [Tooltip("Seconds the announcement remains stationary at screen center when Center Pause is enabled.")]
    [SerializeField]
    private float centerHoldDurationSeconds = 0.7f;

    [Tooltip("Seconds used to reveal the standard message through the paint mask.")]
    [SerializeField]
    private float paintRevealDurationSeconds = 0.65f;

    [Tooltip("Seconds the fully revealed standard paint announcement remains visible.")]
    [SerializeField]
    private float paintHoldDurationSeconds = 0.85f;

    [Tooltip("Seconds used by the moving aerosol-removal front after the standard paint hold phase.")]
    [InspectorName("Paint Removal Duration Seconds")]
    [SerializeField]
    private float paintFadeOutDurationSeconds = 0.25f;

    [Tooltip("Uses unscaled time so the announcement remains deterministic while gameplay time scale is reduced.")]
    [SerializeField]
    private bool useUnscaledTime = true;

    [Header("Terminal Boss Room")]
    [Tooltip("Uses dedicated content, motion timing, and audio when the final Boss room is cleared.")]
    [SerializeField]
    private bool useFinalWaveOverride = true;

    [Tooltip("Text that crosses the gameplay HUD after the terminal Boss room is cleared.")]
    [SerializeField]
    private string finalWaveContent = "AREA CLEARED";

    [Tooltip("Selects traversal or paint reveal for the terminal Boss announcement.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementPresentationMode finalWavePresentationMode =
        GameHudWaveClearAnnouncementPresentationMode.PaintReveal;

    [Tooltip("Screen edge from which the terminal Boss announcement enters.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementDirection finalWaveDirection = GameHudWaveClearAnnouncementDirection.RightToLeft;

    [Tooltip("Direction followed by the terminal Boss announcement removal front after its paint hold.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementDirection finalWavePaintExitDirection =
        GameHudWaveClearAnnouncementDirection.RightToLeft;

    [Tooltip("Seconds used for the terminal Boss announcement traversal, excluding its optional center hold.")]
    [SerializeField]
    private float finalWaveTraversalDurationSeconds = 2.4f;

    [Tooltip("Velocity profile applied to the terminal Boss announcement.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementEasing finalWaveEasing = GameHudWaveClearAnnouncementEasing.DecelerateAtCenter;

    [Tooltip("Pauses the terminal Boss announcement at screen center before it exits.")]
    [SerializeField]
    private bool finalWavePauseAtCenter = true;

    [Tooltip("Seconds the terminal Boss announcement remains at screen center when its pause is enabled.")]
    [SerializeField]
    private float finalWaveCenterHoldDurationSeconds = 1.5f;

    [Tooltip("Seconds used to reveal the terminal Boss message through the paint mask.")]
    [SerializeField]
    private float finalWavePaintRevealDurationSeconds = 0.9f;

    [Tooltip("Seconds the fully revealed terminal Boss paint announcement remains visible.")]
    [SerializeField]
    private float finalWavePaintHoldDurationSeconds = 1.35f;

    [Tooltip("Seconds used by the moving aerosol-removal front after the terminal Boss paint hold phase.")]
    [InspectorName("Paint Removal Duration Seconds")]
    [SerializeField]
    private float finalWavePaintFadeOutDurationSeconds = 0.35f;

    [Tooltip("Requests the selected Audio Manager event when the terminal Boss message starts.")]
    [SerializeField]
    private bool playFinalWaveAudioEvent;

    [Tooltip("Audio Manager event requested with the terminal Boss room-clear message.")]
    [SerializeField]
    private GameAudioEventId finalWaveAudioEventId = GameAudioEventId.FinalWaveClear;

    [Header("Placement")]
    [Tooltip("Normalized vertical screen position used by the announcement, where zero is the bottom and one is the top.")]
    [SerializeField]
    private float verticalPositionNormalized = 0.62f;

    [Tooltip("Additional horizontal distance beyond the text bounds used to keep its start and end positions fully off-screen.")]
    [SerializeField]
    private float horizontalOffscreenPadding = 48f;

    [Header("Paint Reveal")]
    [Tooltip("Aerosol stain silhouette rendered behind the text and used by the animated paint mask.")]
    [SerializeField]
    private Sprite paintBackgroundSprite;

    [Tooltip("Color applied to the paint background sprite.")]
    [SerializeField]
    private Color paintBackgroundColor = new Color(0.95f, 0.015f, 0.32f, 0.97f);

    [Tooltip("Horizontal and vertical canvas padding added around the measured text bounds.")]
    [SerializeField]
    private Vector2 paintBackgroundPadding = new Vector2(112f, 46f);

    [Tooltip("Normalized antialiasing width applied around newly deposited pigment without softening the final sprite silhouette.")]
    [InspectorName("Deposit Softness")]
    [SerializeField]
    private float paintEdgeSoftness = 0.025f;

    [Tooltip("Maximum local arrival-time variation used to keep aerosol deposits separated while they accumulate.")]
    [InspectorName("Deposit Variation")]
    [SerializeField]
    private float paintNoiseStrength = 0.22f;

    [Tooltip("Scale of the overlapping deposit clusters sampled across the announcement background.")]
    [InspectorName("Deposit Scale")]
    [SerializeField]
    private float paintNoiseScale = 2.4f;

    [Tooltip("Strength of fine aerosol breakup and sparse droplets around active deposit edges.")]
    [InspectorName("Mist Strength")]
    [SerializeField]
    private float paintBristleStrength = 0.075f;

    [Tooltip("Spatial density of fine aerosol mist sampled around active deposit edges.")]
    [InspectorName("Mist Density")]
    [SerializeField]
    private float paintBristleScale = 48f;

    [Header("Style")]
    [Tooltip("Optional font asset applied to the announcement. The preauthored font remains in use when this is empty.")]
    [SerializeField]
    private TMP_FontAsset font;

    [Tooltip("Announcement font size in canvas pixels.")]
    [SerializeField]
    private float fontSize = 72f;

    [Tooltip("Font style applied to the announcement text.")]
    [SerializeField]
    private FontStyles fontStyle = FontStyles.Bold;

    [Tooltip("Color applied to the announcement text.")]
    [SerializeField]
    private Color color = Color.white;
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled => isEnabled;
    public string Content => content;
    public bool PlayAudioEvent => playAudioEvent;
    public GameAudioEventId AudioEventId => audioEventId;
    public GameHudWaveClearAnnouncementPresentationMode PresentationMode => presentationMode;
    public GameHudWaveClearAnnouncementDirection Direction => direction;
    public GameHudWaveClearAnnouncementDirection PaintExitDirection => paintExitDirection;
    public float TraversalDurationSeconds => traversalDurationSeconds;
    public GameHudWaveClearAnnouncementEasing Easing => easing;
    public bool PauseAtCenter => pauseAtCenter;
    public float CenterHoldDurationSeconds => centerHoldDurationSeconds;
    public float PaintRevealDurationSeconds => paintRevealDurationSeconds;
    public float PaintHoldDurationSeconds => paintHoldDurationSeconds;
    public float PaintFadeOutDurationSeconds => paintFadeOutDurationSeconds;
    public bool UseUnscaledTime => useUnscaledTime;
    public bool UseFinalWaveOverride => useFinalWaveOverride;
    public string FinalWaveContent => finalWaveContent;
    public GameHudWaveClearAnnouncementPresentationMode FinalWavePresentationMode => finalWavePresentationMode;
    public GameHudWaveClearAnnouncementDirection FinalWaveDirection => finalWaveDirection;
    public GameHudWaveClearAnnouncementDirection FinalWavePaintExitDirection => finalWavePaintExitDirection;
    public float FinalWaveTraversalDurationSeconds => finalWaveTraversalDurationSeconds;
    public GameHudWaveClearAnnouncementEasing FinalWaveEasing => finalWaveEasing;
    public bool FinalWavePauseAtCenter => finalWavePauseAtCenter;
    public float FinalWaveCenterHoldDurationSeconds => finalWaveCenterHoldDurationSeconds;
    public float FinalWavePaintRevealDurationSeconds => finalWavePaintRevealDurationSeconds;
    public float FinalWavePaintHoldDurationSeconds => finalWavePaintHoldDurationSeconds;
    public float FinalWavePaintFadeOutDurationSeconds => finalWavePaintFadeOutDurationSeconds;
    public bool PlayFinalWaveAudioEvent => playFinalWaveAudioEvent;
    public GameAudioEventId FinalWaveAudioEventId => finalWaveAudioEventId;
    public float VerticalPositionNormalized => verticalPositionNormalized;
    public float HorizontalOffscreenPadding => horizontalOffscreenPadding;
    public Sprite PaintBackgroundSprite => paintBackgroundSprite;
    public Color PaintBackgroundColor => paintBackgroundColor;
    public Vector2 PaintBackgroundPadding => paintBackgroundPadding;
    public float PaintEdgeSoftness => paintEdgeSoftness;
    public float PaintNoiseStrength => paintNoiseStrength;
    public float PaintNoiseScale => paintNoiseScale;
    public float PaintBristleStrength => paintBristleStrength;
    public float PaintBristleScale => paintBristleScale;
    public TMP_FontAsset Font => font;
    public float FontSize => fontSize;
    public FontStyles FontStyle => fontStyle;
    public Color Color => color;
    #endregion
}
