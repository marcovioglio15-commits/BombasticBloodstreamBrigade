using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores transition fade timing and presentation values authored by the Scene Manager preset.
/// /params None.
/// /returns None.
/// </summary>
[System.Serializable]
public sealed class GameSceneFadeSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Fade")]
    [Tooltip("Color used by the full-screen transition overlay.")]
    [SerializeField] private Color fadeColor = Color.black;

    [Tooltip("Seconds used to fade from transparent to fully opaque before loading or unloading scenes.")]
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Tooltip("Extra seconds spent at full opacity after Unity scene loading, DOTS SubScene streaming and presentation readiness have completed.")]
    [FormerlySerializedAs("holdBlackSeconds")]
    [SerializeField] private float postLoadReadyExtraSeconds = 0.08f;

    [Tooltip("Seconds used to fade from fully opaque back to transparent after scene activation.")]
    [SerializeField] private float fadeInSeconds = 0.35f;

    [Tooltip("When enabled, gameplay input is blocked during transitions through the time-scale pause path.")]
    [SerializeField] private bool lockGameplayInput = true;

    [Tooltip("When enabled, Time.timeScale is set to zero while a transition is active and restored afterwards.")]
    [SerializeField] private bool setTimeScaleDuringTransition = true;
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
