using System;
using UnityEngine;

#region Ghost Trail Payload
/// <summary>
/// Authors the pooled residual-image trail emitted after a successful active power-up activation.
/// </summary>
[Serializable]
public sealed class PowerUpGhostTrailModuleData
{
    #region Fields

    #region Serialized Fields - Timing
    [Header("Timing")]
    [Tooltip("Unscaled seconds during which new residual images are emitted after activation.")]
    [SerializeField] private float durationSeconds = 1.5f;

    [Tooltip("When enabled on a toggleable active power-up, emission remains active exactly while the toggle is active.")]
    [SerializeField] private bool matchToggleActivationDuration;

    [Tooltip("Unscaled seconds blended into full trail intensity after activation.")]
    [SerializeField] private float easeInUnscaledSeconds = 0.08f;

    [Tooltip("Unscaled seconds blended out after emission duration ends or a matched toggle switches off.")]
    [SerializeField] private float easeOutUnscaledSeconds = 0.18f;

    [Tooltip("Easing curve used by trail opacity and optional screen feedback transitions.")]
    [SerializeField] private ImpactFrameEasingMode easingMode = ImpactFrameEasingMode.EaseOutCubic;

    [Tooltip("Minimum unscaled seconds between two residual-image captures.")]
    [SerializeField] private float emissionIntervalSeconds = 0.06f;

    [Tooltip("Unscaled lifetime of each frozen residual image after capture.")]
    [SerializeField] private float snapshotLifetimeSeconds = 0.35f;
    #endregion

    #region Serialized Fields - Capture
    [Header("Capture")]
    [Tooltip("Selects whether residual images include only the player body, active orbital projections, or every visual attached to the player.")]
    [SerializeField] private GhostTrailCaptureScope captureScope = GhostTrailCaptureScope.PlayerOnly;

    [Tooltip("Minimum player movement distance required to emit a new residual image before the next interval.")]
    [SerializeField] private float movementDistanceThreshold = 0.04f;

    [Tooltip("Minimum player rotation delta in degrees required to emit a new residual image before the next interval.")]
    [SerializeField] private float rotationAngleThresholdDegrees = 3f;

    [Tooltip("Maximum live residual images retained per player. Oldest images are recycled first.")]
    [SerializeField] private int maximumActiveSnapshots = 18;
    #endregion

    #region Serialized Fields - Appearance
    [Header("Appearance")]
    [Tooltip("RGBA residual-image tint. Every channel supports Add Scaling and alpha controls peak opacity.")]
    [SerializeField] private Vector4 tint = new Vector4(0.25f, 0.8f, 1f, 0.45f);
    #endregion

    #region Serialized Fields - Screen Feedback
    [Header("Screen Feedback")]
    [Tooltip("Enables the reusable Impact Frame screen and camera effect profile while Ghost Trail emission is active.")]
    [SerializeField] private bool screenFeedbackEnabled;

    [Tooltip("Optional fullscreen, time-slowdown and camera-motion profile blended with Ghost Trail intensity.")]
    [SerializeField] private PowerUpImpactFrameEffectData screenFeedback = new PowerUpImpactFrameEffectData();
    #endregion

    #endregion

    #region Properties
    public float DurationSeconds => durationSeconds;
    public bool MatchToggleActivationDuration => matchToggleActivationDuration;
    public float EaseInUnscaledSeconds => easeInUnscaledSeconds;
    public float EaseOutUnscaledSeconds => easeOutUnscaledSeconds;
    public ImpactFrameEasingMode EasingMode => easingMode;
    public float EmissionIntervalSeconds => emissionIntervalSeconds;
    public float SnapshotLifetimeSeconds => snapshotLifetimeSeconds;
    public GhostTrailCaptureScope CaptureScope => captureScope;
    public float MovementDistanceThreshold => movementDistanceThreshold;
    public float RotationAngleThresholdDegrees => rotationAngleThresholdDegrees;
    public int MaximumActiveSnapshots => maximumActiveSnapshots;
    public Vector4 Tint => tint;
    public bool ScreenFeedbackEnabled => screenFeedbackEnabled;
    public PowerUpImpactFrameEffectData ScreenFeedback => screenFeedback;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Replaces the baseline values used when the standard Ghost Trail module definition is rebuilt.
    /// </summary>
    /// <param name="durationSecondsValue">Finite emission duration.</param>
    /// <param name="emissionIntervalSecondsValue">Delay between captures.</param>
    /// <param name="snapshotLifetimeSecondsValue">Lifetime of each captured image.</param>
    /// <param name="maximumActiveSnapshotsValue">Maximum retained images per player.</param>
    public void Configure(float durationSecondsValue,
                          float emissionIntervalSecondsValue,
                          float snapshotLifetimeSecondsValue,
                          int maximumActiveSnapshotsValue)
    {
        durationSeconds = durationSecondsValue;
        emissionIntervalSeconds = emissionIntervalSecondsValue;
        snapshotLifetimeSeconds = snapshotLifetimeSecondsValue;
        maximumActiveSnapshots = maximumActiveSnapshotsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures nested reference payloads exist without snapping authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (screenFeedback == null)
            screenFeedback = new PowerUpImpactFrameEffectData();

        screenFeedback.Validate();
    }
    #endregion

    #endregion
}
#endregion
