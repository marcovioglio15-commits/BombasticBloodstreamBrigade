using System;
using UnityEngine;

/// <summary>
/// Contains telegraph settings used before a short-range dash starts.
/// </summary>
[Serializable]
public sealed class EnemyShortRangeDashAimPayload
{
    #region Fields

    #region Serialized Fields
    [Header("1. Aim")]
    [Tooltip("Seconds spent tracking the player before the dash starts. Use 0 for an instant release.")]
    [Min(0f)]
    [SerializeField] private float aimDurationSeconds = 0.4f;

    [Tooltip("Movement speed multiplier applied while the enemy is taking aim. Use 0 to fully stop during the telegraph.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float moveSpeedMultiplierWhileAiming = 0.1f;
    #endregion

    #endregion

    #region Properties
    public float AimDurationSeconds
    {
        get
        {
            return aimDurationSeconds;
        }
    }

    public float MoveSpeedMultiplierWhileAiming
    {
        get
        {
            return moveSpeedMultiplierWhileAiming;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps nested references structurally valid without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains recovery settings used after one committed short-range dash finishes.
/// </summary>
[Serializable]
public sealed class EnemyShortRangeDashRecoveryPayload
{
    #region Fields

    #region Serialized Fields
    [Header("2. Recovery")]
    [Tooltip("Seconds spent back on the core movement module after a committed dash before the enemy can start aiming a new dash.")]
    [Min(0f)]
    [SerializeField] private float cooldownSeconds = 1f;
    #endregion

    #endregion

    #region Properties
    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps nested references structurally valid without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains distance-resolution settings used to convert current player distance into one dash travel distance.
/// </summary>
[Serializable]
public sealed class EnemyShortRangeDashDistancePayload
{
    #region Fields

    #region Serialized Fields
    [Header("3. Distance")]
    [Tooltip("Selects whether dash travel distance is derived from the current player distance or from one fixed authored value.")]
    [SerializeField] private EnemyShortRangeDashDistanceSource distanceSource = EnemyShortRangeDashDistanceSource.PlayerDistance;

    [Tooltip("Multiplier applied to current player distance when Distance Source uses Player Distance.")]
    [Min(0f)]
    [SerializeField] private float playerDistanceMultiplier = 1f;

    [Tooltip("Flat distance added after Player Distance Multiplier when Distance Source uses Player Distance.")]
    [SerializeField] private float distanceOffset;

    [Tooltip("Fixed travel distance used when Distance Source is set to Fixed Distance.")]
    [Min(0f)]
    [SerializeField] private float fixedDistance = 5f;

    [Tooltip("Lower clamp applied to the resolved dash travel distance.")]
    [Min(0f)]
    [SerializeField] private float minimumTravelDistance = 2f;

    [Tooltip("Upper clamp applied to the resolved dash travel distance.")]
    [Min(0f)]
    [SerializeField] private float maximumTravelDistance = 7f;
    #endregion

    #endregion

    #region Properties
    public EnemyShortRangeDashDistanceSource DistanceSource
    {
        get
        {
            return distanceSource;
        }
    }

    public float PlayerDistanceMultiplier
    {
        get
        {
            return playerDistanceMultiplier;
        }
    }

    public float DistanceOffset
    {
        get
        {
            return distanceOffset;
        }
    }

    public float FixedDistance
    {
        get
        {
            return fixedDistance;
        }
    }

    public float MinimumTravelDistance
    {
        get
        {
            return minimumTravelDistance;
        }
    }

    public float MaximumTravelDistance
    {
        get
        {
            return maximumTravelDistance;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps nested references structurally valid without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains authored path-shaping settings used to sample one ECS-friendly dash path.
/// </summary>
[Serializable]
public sealed class EnemyShortRangeDashPathPayload
{
    #region Fields

    #region Serialized Fields
    [Header("4. Path")]
    [Tooltip("Seconds used to travel across the sampled dash path once the telegraph finishes.")]
    [Min(0.01f)]
    [SerializeField] private float dashDurationSeconds = 0.28f;

    [Tooltip("Horizontal width in meters applied over the normalized lateral path curve.")]
    [Min(0f)]
    [SerializeField] private float lateralAmplitude = 1.2f;

    [Tooltip("Controls how the dash path chooses its lateral side relative to the locked aim direction.")]
    [SerializeField] private EnemyShortRangeDashMirrorMode mirrorMode = EnemyShortRangeDashMirrorMode.Alternate;

    [Tooltip("Normalized forward progression sampled from 0 to 1 across dash time. This curve controls how quickly the dash advances toward its travel distance.")]
    [SerializeField] private AnimationCurve forwardProgressCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

    [Tooltip("Normalized lateral offset sampled from 0 to 1 across dash time. Use this curve to create arcs, feints, S-shapes and side sweeps around the locked aim line.")]
    [SerializeField] private AnimationCurve lateralOffsetCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
    #endregion

    #endregion

    #region Properties
    public float DashDurationSeconds
    {
        get
        {
            return dashDurationSeconds;
        }
    }

    public float LateralAmplitude
    {
        get
        {
            return lateralAmplitude;
        }
    }

    public EnemyShortRangeDashMirrorMode MirrorMode
    {
        get
        {
            return mirrorMode;
        }
    }

    public AnimationCurve ForwardProgressCurve
    {
        get
        {
            return forwardProgressCurve;
        }
    }

    public AnimationCurve LateralOffsetCurve
    {
        get
        {
            return lateralOffsetCurve;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the path curves always exist without snapping authored keys.
    /// </summary>
    public void Validate()
    {
        if (forwardProgressCurve == null)
            forwardProgressCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        if (lateralOffsetCurve == null)
            lateralOffsetCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups all authoring blocks used by the short-range dash module.
/// </summary>
[Serializable]
public sealed class EnemyShortRangeDashModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Telegraph settings applied before the dash starts.")]
    [SerializeField] private EnemyShortRangeDashAimPayload aim = new EnemyShortRangeDashAimPayload();

    [Tooltip("Recovery settings applied after the dash finishes before a new dash can start.")]
    [SerializeField] private EnemyShortRangeDashRecoveryPayload recovery = new EnemyShortRangeDashRecoveryPayload();

    [Tooltip("Distance settings used to resolve how far the dash travels from the current enemy position.")]
    [SerializeField] private EnemyShortRangeDashDistancePayload distance = new EnemyShortRangeDashDistancePayload();

    [Tooltip("Path settings used to shape the committed dash motion.")]
    [SerializeField] private EnemyShortRangeDashPathPayload path = new EnemyShortRangeDashPathPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyShortRangeDashAimPayload Aim
    {
        get
        {
            return aim;
        }
    }

    public EnemyShortRangeDashDistancePayload Distance
    {
        get
        {
            return distance;
        }
    }

    public EnemyShortRangeDashRecoveryPayload Recovery
    {
        get
        {
            return recovery;
        }
    }

    public EnemyShortRangeDashPathPayload Path
    {
        get
        {
            return path;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested authoring blocks always exist without snapping authored values.
    /// </summary>
    public void Validate()
    {
        if (aim == null)
            aim = new EnemyShortRangeDashAimPayload();

        if (distance == null)
            distance = new EnemyShortRangeDashDistancePayload();

        if (recovery == null)
            recovery = new EnemyShortRangeDashRecoveryPayload();

        if (path == null)
            path = new EnemyShortRangeDashPathPayload();

        aim.Validate();
        recovery.Validate();
        distance.Validate();
        path.Validate();
    }
    #endregion

    #endregion
}
