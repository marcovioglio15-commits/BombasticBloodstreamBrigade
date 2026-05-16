using UnityEngine;

/// <summary>
/// Stores authored loading-progress presentation values used by Scene Manager transitions.
/// </summary>
[System.Serializable]
public sealed class GameSceneLoadingProgressSettings
{
    #region Constants
    public const int DefaultSegmentCount = 32;
    public const float DefaultSegmentGapDegrees = 3f;
    public const float DefaultRingThickness = 12f;
    public const float DefaultSpinnerRotationDegreesPerSecond = 180f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Loading Progress")]
    [Tooltip("When enabled, the fade overlay can display a circular loading-progress indicator while scenes and companion content are loading.")]
    [SerializeField] private bool showLoadingProgress = true;

    [Tooltip("When enabled, a percentage label is shown at the center of the circular loading indicator.")]
    [SerializeField] private bool showPercentage = true;

    [Tooltip("When enabled, a status text is shown next to the circular loading indicator with the current scene or Addressables key being processed.")]
    [SerializeField] private bool showStatusText = true;

    [Header("Status Text")]
    [Tooltip("Prefix used when the transition is loading a scene, companion UI scene or direct DOTS scene content.")]
    [SerializeField] private string loadingStatusPrefix = "Loading";

    [Tooltip("Prefix used when the transition is unloading an old scene, companion UI scene or direct DOTS scene content.")]
    [SerializeField] private string unloadingStatusPrefix = "Unloading";

    [Tooltip("Text shown while loaded content is waiting for DOTS transform and presentation readiness before fade-in.")]
    [SerializeField] private string readinessStatusText = "Preparing scene";

    [Tooltip("Text shown after loading has finished and the transition is about to fade back in.")]
    [SerializeField] private string readyStatusText = "Ready";

    [Header("Ring Visuals")]
    [Tooltip("Color applied to the filled segmented ring that represents current loading progress.")]
    [SerializeField] private Color ringColor = new Color(0.55f, 0.82f, 1f, 1f);

    [Tooltip("Color applied to the background segmented ring behind the progress fill.")]
    [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.18f);

    [Tooltip("Color applied to the percentage and status labels.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Number of visual segments used by the circular loading ring. Values lower than 3 are reported by validation and clamped only at runtime rendering.")]
    [SerializeField] private int ringSegmentCount = DefaultSegmentCount;

    [Tooltip("Angular gap in degrees between ring segments. Negative values are reported by validation and clamped only at runtime rendering.")]
    [SerializeField] private float ringSegmentGapDegrees = DefaultSegmentGapDegrees;

    [Tooltip("Ring thickness in UI pixels. Non-positive values are reported by validation and clamped only at runtime rendering.")]
    [SerializeField] private float ringThickness = DefaultRingThickness;

    [Tooltip("Unscaled rotation speed in degrees per second used by the authored loading spinner root.")]
    [SerializeField] private float spinnerRotationDegreesPerSecond = DefaultSpinnerRotationDegreesPerSecond;
    #endregion

    #endregion

    #region Properties
    public bool ShowLoadingProgress
    {
        get
        {
            return showLoadingProgress;
        }
    }

    public bool ShowPercentage
    {
        get
        {
            return showPercentage;
        }
    }

    public bool ShowStatusText
    {
        get
        {
            return showStatusText;
        }
    }

    public string LoadingStatusPrefix
    {
        get
        {
            return loadingStatusPrefix;
        }
    }

    public string UnloadingStatusPrefix
    {
        get
        {
            return unloadingStatusPrefix;
        }
    }

    public string ReadinessStatusText
    {
        get
        {
            return readinessStatusText;
        }
    }

    public string ReadyStatusText
    {
        get
        {
            return readyStatusText;
        }
    }

    public Color RingColor
    {
        get
        {
            return ringColor;
        }
    }

    public Color TrackColor
    {
        get
        {
            return trackColor;
        }
    }

    public Color TextColor
    {
        get
        {
            return textColor;
        }
    }

    public int RingSegmentCount
    {
        get
        {
            return ringSegmentCount;
        }
    }

    public float RingSegmentGapDegrees
    {
        get
        {
            return ringSegmentGapDegrees;
        }
    }

    public float RingThickness
    {
        get
        {
            return ringThickness;
        }
    }

    public float SpinnerRotationDegreesPerSecond
    {
        get
        {
            return spinnerRotationDegreesPerSecond;
        }
    }
    #endregion
}
