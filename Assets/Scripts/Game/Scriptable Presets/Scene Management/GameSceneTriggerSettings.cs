using UnityEngine;

/// <summary>
/// Stores shared trigger authoring defaults used by Scene Manager transition volumes.
/// </summary>
[System.Serializable]
public sealed class GameSceneTriggerSettings
{
    #region Constants
    public const string DefaultTransitionLayerName = "SceneTransition";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Trigger Defaults")]
    [Tooltip("Unity layer name expected on physical objects that represent scene transition trigger volumes.")]
    [SerializeField] private string transitionLayerName = DefaultTransitionLayerName;

    [Tooltip("Default cooldown in seconds after a trigger submits a transition request.")]
    [SerializeField] private float defaultCooldownSeconds = 0.75f;

    [Tooltip("When enabled, trigger volumes require a player entity before they can submit a transition request.")]
    [SerializeField] private bool requirePlayer = true;

    [Tooltip("When enabled, trigger volumes deactivate themselves after the first successful transition request.")]
    [SerializeField] private bool oneShotByDefault = true;

    [Tooltip("Color used by editor gizmos for valid scene transition volumes.")]
    [SerializeField] private Color gizmoColor = new Color(0.1f, 0.55f, 1f, 0.28f);
    #endregion

    #endregion

    #region Properties
    public string TransitionLayerName
    {
        get
        {
            return transitionLayerName;
        }
    }

    public float DefaultCooldownSeconds
    {
        get
        {
            return defaultCooldownSeconds;
        }
    }

    public bool RequirePlayer
    {
        get
        {
            return requirePlayer;
        }
    }

    public bool OneShotByDefault
    {
        get
        {
            return oneShotByDefault;
        }
    }

    public Color GizmoColor
    {
        get
        {
            return gizmoColor;
        }
    }
    #endregion
}
