using System;
using UnityEngine;

/// <summary>
/// Stores visibility-related presentation settings shared by one enemy type.
/// </summary>
[Serializable]
public sealed class EnemyVisualVisibilitySettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Visual runtime path: managed companion Animator (few actors) or GPU-baked playback (crowd scale).")]
    [SerializeField] private EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

    [Tooltip("Playback speed multiplier used by both companion and GPU-baked visual paths.")]
    [SerializeField] private float visualAnimationSpeed = 1f;

    [Tooltip("Loop duration in seconds used by GPU-baked playback time wrapping.")]
    [SerializeField] private float gpuAnimationLoopDuration = 1f;

    [Tooltip("Enable distance-based visual culling while gameplay simulation remains fully active.")]
    [SerializeField] private bool enableDistanceCulling = true;

    [Tooltip("Maximum planar distance from player where visuals stay visible. Set to 0 to keep always visible.")]
    [SerializeField] private float maxVisibleDistance = 55f;

    [Tooltip("Additional distance band used to avoid visual popping when crossing the culling boundary.")]
    [SerializeField] private float visibleDistanceHysteresis = 6f;
    #endregion

    #endregion

    #region Properties
    public EnemyVisualMode VisualMode
    {
        get
        {
            return visualMode;
        }
    }

    public float VisualAnimationSpeed
    {
        get
        {
            return visualAnimationSpeed;
        }
    }

    public float GpuAnimationLoopDuration
    {
        get
        {
            return gpuAnimationLoopDuration;
        }
    }

    public bool EnableDistanceCulling
    {
        get
        {
            return enableDistanceCulling;
        }
    }

    public float MaxVisibleDistance
    {
        get
        {
            return maxVisibleDistance;
        }
    }

    public float VisibleDistanceHysteresis
    {
        get
        {
            return visibleDistanceHysteresis;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes visibility settings after asset edits.
    /// </summary>
    public void Validate()
    {
        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
            case EnemyVisualMode.GpuBaked:
                break;

            default:
                visualMode = EnemyVisualMode.GpuBaked;
                break;
        }

        if (visualAnimationSpeed < 0f)
            visualAnimationSpeed = 0f;

        if (gpuAnimationLoopDuration < 0.05f)
            gpuAnimationLoopDuration = 0.05f;

        if (maxVisibleDistance < 0f)
            maxVisibleDistance = 0f;

        if (visibleDistanceHysteresis < 0f)
            visibleDistanceHysteresis = 0f;
    }
    #endregion

    #endregion
}
