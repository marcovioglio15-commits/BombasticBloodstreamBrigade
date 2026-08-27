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
    [Tooltip("Screen edge from which the announcement enters before continuing through the opposite edge.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementDirection direction = GameHudWaveClearAnnouncementDirection.RightToLeft;

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

    [Tooltip("Screen edge from which the terminal Boss announcement enters.")]
    [SerializeField]
    private GameHudWaveClearAnnouncementDirection finalWaveDirection = GameHudWaveClearAnnouncementDirection.RightToLeft;

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
    public GameHudWaveClearAnnouncementDirection Direction => direction;
    public float TraversalDurationSeconds => traversalDurationSeconds;
    public GameHudWaveClearAnnouncementEasing Easing => easing;
    public bool PauseAtCenter => pauseAtCenter;
    public float CenterHoldDurationSeconds => centerHoldDurationSeconds;
    public bool UseUnscaledTime => useUnscaledTime;
    public bool UseFinalWaveOverride => useFinalWaveOverride;
    public string FinalWaveContent => finalWaveContent;
    public GameHudWaveClearAnnouncementDirection FinalWaveDirection => finalWaveDirection;
    public float FinalWaveTraversalDurationSeconds => finalWaveTraversalDurationSeconds;
    public GameHudWaveClearAnnouncementEasing FinalWaveEasing => finalWaveEasing;
    public bool FinalWavePauseAtCenter => finalWavePauseAtCenter;
    public float FinalWaveCenterHoldDurationSeconds => finalWaveCenterHoldDurationSeconds;
    public bool PlayFinalWaveAudioEvent => playFinalWaveAudioEvent;
    public GameAudioEventId FinalWaveAudioEventId => finalWaveAudioEventId;
    public float VerticalPositionNormalized => verticalPositionNormalized;
    public float HorizontalOffscreenPadding => horizontalOffscreenPadding;
    public TMP_FontAsset Font => font;
    public float FontSize => fontSize;
    public FontStyles FontStyle => fontStyle;
    public Color Color => color;
    #endregion
}
