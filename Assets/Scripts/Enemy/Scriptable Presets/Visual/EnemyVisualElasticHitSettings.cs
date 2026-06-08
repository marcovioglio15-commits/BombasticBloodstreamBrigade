using System;
using UnityEngine;

/// <summary>
/// Selects which accepted enemy damage events can trigger elastic hit deformation.
/// </summary>
public enum EnemyElasticHitTriggerMode : byte
{
    DirectImpactsOnly = 0,
    AllNonLethalDamage = 1
}

/// <summary>
/// Selects how a new elastic hit competes with an already active deformation.
/// </summary>
public enum EnemyElasticHitRetriggerMode : byte
{
    StrongestWins = 0,
    Restart = 1
}

/// <summary>
/// Stores shader-driven non-lethal elastic hit feedback settings.
/// </summary>
[Serializable]
public sealed class EnemyVisualElasticHitSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables shader-driven elastic deformation after valid non-lethal enemy damage.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Controls whether only spatially directed impacts or every non-lethal damage event can trigger deformation.")]
    [SerializeField] private EnemyElasticHitTriggerMode triggerMode = EnemyElasticHitTriggerMode.DirectImpactsOnly;

    [Tooltip("Total elastic response duration in seconds.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float durationSeconds = 0.16f;

    [Tooltip("Maximum compression ratio applied along the resolved impact axis.")]
    [Range(0f, 0.75f)]
    [SerializeField] private float maximumCompression = 0.18f;

    [Tooltip("Amount of perpendicular stretch used to visually compensate compressed volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeCompensation = 0.65f;

    [Tooltip("Number of squash and rebound oscillations completed during one response.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float oscillationCount = 1.25f;

    [Tooltip("Exponential damping applied to the elastic response over time.")]
    [Range(0f, 20f)]
    [SerializeField] private float damping = 5.5f;

    [Tooltip("Blend between a broad squash response and a deformation aligned to the incoming hit direction.")]
    [Range(0f, 1f)]
    [SerializeField] private float directionality = 0.85f;

    [Tooltip("Keeps vertices at the object-space ground pivot stable while the enemy stretches vertically.")]
    [SerializeField] private bool anchorToGround = true;

    [Tooltip("Minimum interval in seconds between accepted elastic deformation triggers.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float minimumRetriggerInterval = 0.035f;

    [Tooltip("Controls how a new accepted hit interacts with an already active deformation.")]
    [SerializeField] private EnemyElasticHitRetriggerMode retriggerMode = EnemyElasticHitRetriggerMode.StrongestWins;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public EnemyElasticHitTriggerMode TriggerMode => triggerMode;
    public float DurationSeconds => durationSeconds;
    public float MaximumCompression => maximumCompression;
    public float VolumeCompensation => volumeCompensation;
    public float OscillationCount => oscillationCount;
    public float Damping => damping;
    public float Directionality => directionality;
    public bool AnchorToGround => anchorToGround;
    public float MinimumRetriggerInterval => minimumRetriggerInterval;
    public EnemyElasticHitRetriggerMode RetriggerMode => retriggerMode;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Preserves authored values so the management tool can report inconsistencies without silently snapping data.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
