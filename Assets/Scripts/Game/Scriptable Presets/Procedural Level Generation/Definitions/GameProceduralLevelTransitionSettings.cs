using System;
using UnityEngine;

/// <summary>
/// Stores presentation and relocation settings used only for room-to-room transitions inside one level.
/// </summary>
[Serializable]
public sealed class GameProceduralLevelTransitionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Keeps the persistent player presentation visible above the black environment fade during intra-level room transitions.")]
    [SerializeField]
    private bool keepPlayerVisible = true;

    [Tooltip("Optional one-shot animation played by the persistent player presentation during an intra-level transition.")]
    [SerializeField]
    private AnimationClip playerTransitionAnimation;

    [Tooltip("Normalized animation time at which the player is relocated after the destination room becomes ready.")]
    [SerializeField]
    private float relocationNormalizedTime = 0.5f;

    [Tooltip("Clears player linear and angular velocity when the destination arrival pose is applied.")]
    [SerializeField]
    private bool clearPlayerVelocity = true;
    #endregion

    #endregion

    #region Properties
    public bool KeepPlayerVisible
    {
        get
        {
            return keepPlayerVisible;
        }
    }

    public AnimationClip PlayerTransitionAnimation
    {
        get
        {
            return playerTransitionAnimation;
        }
    }

    public float RelocationNormalizedTime
    {
        get
        {
            return relocationNormalizedTime;
        }
    }

    public bool ClearPlayerVelocity
    {
        get
        {
            return clearPlayerVelocity;
        }
    }

    #endregion
}
