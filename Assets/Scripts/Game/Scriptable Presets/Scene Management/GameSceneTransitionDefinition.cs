using UnityEngine;

/// <summary>
/// Defines a directed transition between two managed scenes.
/// /params None.
/// /returns None.
/// </summary>
[System.Serializable]
public sealed class GameSceneTransitionDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable transition ID used by menu commands, scripted requests and trigger volumes.")]
    [SerializeField] private string transitionId;

    [Tooltip("Scene ID that owns or starts this transition. Leave empty for global menu/script commands.")]
    [SerializeField] private string fromSceneId;

    [Tooltip("Target scene ID loaded by this transition.")]
    [SerializeField] private string toSceneId;

    [Tooltip("Priority used when several transitions could match the same request context.")]
    [SerializeField] private int priority;

    [Tooltip("Source that is expected to request this transition.")]
    [SerializeField] private GameSceneTransitionMode transitionMode = GameSceneTransitionMode.MenuCommand;

    [Header("Trigger")]
    [Tooltip("Trigger ID expected from GameSceneTransitionTriggerAuthoring when this transition is trigger-based.")]
    [SerializeField] private string triggerId;

    [Tooltip("Cooldown override in seconds for this transition trigger. Negative values mean the trigger default is used.")]
    [SerializeField] private float triggerCooldownOverrideSeconds = -1f;

    [Tooltip("When enabled, trigger volumes using this transition submit only one successful request.")]
    [SerializeField] private bool oneShotTrigger = true;

    [Header("Fade Override")]
    [Tooltip("When enabled, this transition uses the override fade timings instead of the preset defaults.")]
    [SerializeField] private bool overrideFadeSettings;

    [Tooltip("Override fade-out duration in seconds when Override Fade Settings is enabled.")]
    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Tooltip("Override black-hold duration in seconds when Override Fade Settings is enabled.")]
    [SerializeField] private float holdBlackSeconds = 0.08f;

    [Tooltip("Override fade-in duration in seconds when Override Fade Settings is enabled.")]
    [SerializeField] private float fadeInSeconds = 0.35f;

    [Header("Rules")]
    [Tooltip("When enabled, this transition can run while gameplay is paused.")]
    [SerializeField] private bool allowDuringPause = true;

    [Tooltip("When enabled, this transition can run after the ECS run outcome has finalized.")]
    [SerializeField] private bool allowWhenRunFinalized = true;
    #endregion

    #endregion

    #region Properties
    public string TransitionId
    {
        get
        {
            return transitionId;
        }
    }

    public string FromSceneId
    {
        get
        {
            return fromSceneId;
        }
    }

    public string ToSceneId
    {
        get
        {
            return toSceneId;
        }
    }

    public int Priority
    {
        get
        {
            return priority;
        }
    }

    public GameSceneTransitionMode TransitionMode
    {
        get
        {
            return transitionMode;
        }
    }

    public string TriggerId
    {
        get
        {
            return triggerId;
        }
    }

    public float TriggerCooldownOverrideSeconds
    {
        get
        {
            return triggerCooldownOverrideSeconds;
        }
    }

    public bool OneShotTrigger
    {
        get
        {
            return oneShotTrigger;
        }
    }

    public bool OverrideFadeSettings
    {
        get
        {
            return overrideFadeSettings;
        }
    }

    public float FadeOutSeconds
    {
        get
        {
            return fadeOutSeconds;
        }
    }

    public float HoldBlackSeconds
    {
        get
        {
            return holdBlackSeconds;
        }
    }

    public float FadeInSeconds
    {
        get
        {
            return fadeInSeconds;
        }
    }

    public bool AllowDuringPause
    {
        get
        {
            return allowDuringPause;
        }
    }

    public bool AllowWhenRunFinalized
    {
        get
        {
            return allowWhenRunFinalized;
        }
    }
    #endregion
}
