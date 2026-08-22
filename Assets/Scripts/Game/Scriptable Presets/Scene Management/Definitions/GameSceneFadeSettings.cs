using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores transition fade timing and presentation values authored by the Scene Manager preset.
/// </summary>
[System.Serializable]
public sealed class GameSceneFadeSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Fade")]
    [Tooltip("Color used by the full-screen transition overlay.")]
    [SerializeField]
    private Color fadeColor = Color.black;

    [Tooltip("Selects a uniform opacity fade or a shader-driven directional gradient that progressively covers and reveals the scene.")]
    [SerializeField]
    private GameSceneFadeMode fadeMode = GameSceneFadeMode.DirectionalGradient;

    [Tooltip("Direction followed by the darkness when Directional Gradient is selected. Fade-in reveals the target scene in the reverse progression.")]
    [InspectorName("Direction")]
    [SerializeField]
    private GameSceneFadeWipeDirection wipeDirection = GameSceneFadeWipeDirection.LeftToRight;

    [Tooltip("Half-width of the soft transition boundary in normalized screen space. Larger values spread darkness across a broader part of the screen.")]
    [SerializeField]
    private float directionalEdgeSoftness = 0.16f;

    [Tooltip("Maximum normalized displacement applied to the directional boundary by the procedural shader noise. Zero produces a straight gradient.")]
    [SerializeField]
    private float directionalNoiseStrength = 0.035f;

    [Tooltip("Spatial frequency of the procedural boundary noise. Larger values create smaller variations along the gradient edge.")]
    [SerializeField]
    private float directionalNoiseScale = 5.5f;

    [Tooltip("Interpolation applied before shader evaluation. Smooth Step softens acceleration at the beginning and end of the transition.")]
    [SerializeField]
    private GameSceneFadeEasing easing = GameSceneFadeEasing.SmoothStep;

    [Tooltip("Seconds used to fade from transparent to fully opaque before loading or unloading scenes.")]
    [SerializeField]
    private float fadeOutSeconds = 0.35f;

    [Tooltip("Extra seconds spent at full opacity after Unity scene loading, DOTS SubScene streaming and presentation readiness have completed.")]
    [FormerlySerializedAs("holdBlackSeconds")]
    [SerializeField]
    private float postLoadReadyExtraSeconds = 0.08f;

    [Tooltip("Seconds used to fade from fully opaque back to transparent after scene activation.")]
    [SerializeField]
    private float fadeInSeconds = 0.35f;

    [Tooltip("When enabled, gameplay input is blocked during transitions through the time-scale pause path.")]
    [SerializeField]
    private bool lockGameplayInput = true;

    [Tooltip("When enabled, Time.timeScale is set to zero while a transition is active and restored afterwards.")]
    [SerializeField]
    private bool setTimeScaleDuringTransition = true;
    #endregion

    #endregion

    #region Properties
    public Color FadeColor
    {
        get
        {
            return fadeColor;
        }
    }

    public GameSceneFadeMode FadeMode
    {
        get
        {
            return fadeMode;
        }
    }

    public GameSceneFadeWipeDirection WipeDirection
    {
        get
        {
            return wipeDirection;
        }
    }

    public float DirectionalEdgeSoftness
    {
        get
        {
            return directionalEdgeSoftness;
        }
    }

    public float DirectionalNoiseStrength
    {
        get
        {
            return directionalNoiseStrength;
        }
    }

    public float DirectionalNoiseScale
    {
        get
        {
            return directionalNoiseScale;
        }
    }

    public GameSceneFadeEasing Easing
    {
        get
        {
            return easing;
        }
    }

    public float FadeOutSeconds
    {
        get
        {
            return fadeOutSeconds;
        }
    }

    public float PostLoadReadyExtraSeconds
    {
        get
        {
            return postLoadReadyExtraSeconds;
        }
    }

    public float FadeInSeconds
    {
        get
        {
            return fadeInSeconds;
        }
    }

    public bool LockGameplayInput
    {
        get
        {
            return lockGameplayInput;
        }
    }

    public bool SetTimeScaleDuringTransition
    {
        get
        {
            return setTimeScaleDuringTransition;
        }
    }
    #endregion
}
