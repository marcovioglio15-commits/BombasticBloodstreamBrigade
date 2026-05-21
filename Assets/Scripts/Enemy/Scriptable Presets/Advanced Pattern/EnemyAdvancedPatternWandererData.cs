using System;
using UnityEngine;

/// <summary>
/// Contains payload values for Wanderer Basic movement.
/// </summary>
[Serializable]
public sealed class EnemyWandererBasicPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Radius around the enemy used to sample next wander destination candidates.")]
    [Range(0.5f, 32f)]
    [SerializeField] private float searchRadius = 9f;

    [Tooltip("Minimum distance from current position required for a valid wander destination.")]
    [Range(0f, 16f)]
    [SerializeField] private float minimumTravelDistance = 2f;

    [Tooltip("Maximum distance from current position allowed for a valid wander destination.")]
    [Range(0.5f, 32f)]
    [SerializeField] private float maximumTravelDistance = 8f;

    [Tooltip("Distance threshold used to consider the destination reached.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float arrivalTolerance = 0.35f;

    [Tooltip("Seconds to wait after reaching a destination before selecting the next one.")]
    [Range(0f, 6f)]
    [SerializeField] private float waitCooldownSeconds = 0.7f;

    [Tooltip("Number of candidate positions sampled for each destination pick.")]
    [Range(1, 32)]
    [SerializeField] private int candidateSampleCount = 9;

    [Tooltip("When enabled, the picker scans the full 360° arc using Infinite Direction Step Degrees instead of Candidate Sample Count.")]
    [SerializeField] private bool useInfiniteDirectionSampling = true;

    [Tooltip("Angular resolution in degrees used by infinite direction sampling. Lower values are more exhaustive but more expensive.")]
    [Range(0.5f, 45f)]
    [SerializeField] private float infiniteDirectionStepDegrees = 8f;

    [Tooltip("Bias factor for preferring directions not recently explored. Applied only after free-trajectory checks and scoring.")]
    [Range(0f, 1f)]
    [SerializeField] private float unexploredDirectionPreference = 0.65f;

    [Tooltip("Bias factor for preferring candidates closer to the player. Applied only after free-trajectory checks and scoring.")]
    [Range(0f, 1f)]
    [SerializeField] private float towardPlayerPreference = 0.35f;

    [Tooltip("Minimum extra clearance in meters kept from neighboring enemies when selecting a destination or trajectory.")]
    [Range(0f, 3f)]
    [SerializeField] private float minimumEnemyClearance = 0.2f;

    [Tooltip("Prediction time in seconds used to avoid trajectories likely to intersect moving neighbors.")]
    [Range(0f, 2f)]
    [SerializeField] private float trajectoryPredictionTime = 0.35f;

    [Tooltip("Weight assigned to free-space and collision-safe trajectories. Higher values prioritize safer paths over biases.")]
    [Range(0f, 8f)]
    [SerializeField] private float freeTrajectoryPreference = 4f;

    [Tooltip("Cooldown in seconds before retrying destination selection after blocked candidates.")]
    [Range(0f, 2f)]
    [SerializeField] private float blockedPathRetryDelay = 0.25f;
    #endregion

    #endregion

    #region Properties
    public float SearchRadius
    {
        get
        {
            return searchRadius;
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

    public float ArrivalTolerance
    {
        get
        {
            return arrivalTolerance;
        }
    }

    public float WaitCooldownSeconds
    {
        get
        {
            return waitCooldownSeconds;
        }
    }

    public int CandidateSampleCount
    {
        get
        {
            return candidateSampleCount;
        }
    }

    public bool UseInfiniteDirectionSampling
    {
        get
        {
            return useInfiniteDirectionSampling;
        }
    }

    public float InfiniteDirectionStepDegrees
    {
        get
        {
            return infiniteDirectionStepDegrees;
        }
    }

    public float UnexploredDirectionPreference
    {
        get
        {
            return unexploredDirectionPreference;
        }
    }

    public float TowardPlayerPreference
    {
        get
        {
            return towardPlayerPreference;
        }
    }

    public float MinimumEnemyClearance
    {
        get
        {
            return minimumEnemyClearance;
        }
    }

    public float TrajectoryPredictionTime
    {
        get
        {
            return trajectoryPredictionTime;
        }
    }

    public float FreeTrajectoryPreference
    {
        get
        {
            return freeTrajectoryPreference;
        }
    }

    public float BlockedPathRetryDelay
    {
        get
        {
            return blockedPathRetryDelay;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Basic wanderer payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains payload values for Wanderer DVD movement.
/// </summary>
[Serializable]
public sealed class EnemyWandererDvdPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Speed multiplier applied over Brain movement speed when DVD movement is active.")]
    [Range(0f, 4f)]
    [SerializeField] private float speedMultiplier = 1.05f;

    [Tooltip("Bounce damping applied after wall reflection. 1 keeps full speed, 0 fully stops.")]
    [Range(0f, 1f)]
    [SerializeField] private float bounceDamping = 1f;

    [Tooltip("When enabled, DVD movement starts with a pseudo-random diagonal direction per spawn.")]
    [SerializeField] private bool randomizeInitialDirection = true;

    [Tooltip("Fallback fixed initial direction in degrees used when randomization is disabled.")]
    [Range(0f, 360f)]
    [SerializeField] private float fixedInitialDirectionDegrees = 45f;

    [Tooltip("Small displacement in meters used to resolve corner-stuck situations.")]
    [Range(0f, 1f)]
    [SerializeField] private float cornerNudgeDistance = 0.08f;

    [Tooltip("When enabled, DVD movement ignores local steering and priority rules, preserving pure bounce trajectories.")]
    [SerializeField] private bool ignoreSteeringAndPriority;
    #endregion

    #endregion

    #region Properties
    public float SpeedMultiplier
    {
        get
        {
            return speedMultiplier;
        }
    }

    public float BounceDamping
    {
        get
        {
            return bounceDamping;
        }
    }

    public bool RandomizeInitialDirection
    {
        get
        {
            return randomizeInitialDirection;
        }
    }

    public float FixedInitialDirectionDegrees
    {
        get
        {
            return fixedInitialDirectionDegrees;
        }
    }

    public float CornerNudgeDistance
    {
        get
        {
            return cornerNudgeDistance;
        }
    }

    public bool IgnoreSteeringAndPriority
    {
        get
        {
            return ignoreSteeringAndPriority;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures DVD wanderer payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains payload values for Acid Wanderer trail emission and damage.
/// </summary>
[Serializable]
public sealed class EnemyWandererAcidPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Seconds that one acid trail segment remains active after being emitted.")]
    [Range(0.05f, 12f)]
    [SerializeField] private float trailSegmentLifetimeSeconds = 3f;

    [Tooltip("Minimum planar movement distance required before emitting another acid trail segment.")]
    [Range(0f, 4f)]
    [SerializeField] private float trailSpawnDistance = 0.75f;

    [Tooltip("Minimum seconds between emitted acid trail segments.")]
    [Range(0f, 2f)]
    [SerializeField] private float trailSpawnIntervalSeconds = 0.18f;

    [Tooltip("Planar overlap radius used by each acid segment when checking the player.")]
    [Range(0f, 4f)]
    [SerializeField] private float trailRadius = 0.85f;

    [Tooltip("Maximum active acid trail segments retained by one enemy before the oldest samples are removed.")]
    [Range(0, 128)]
    [SerializeField] private int maxActiveSegmentsPerEnemy = 18;

    [Tooltip("Flat shield/health damage applied to the player by one acid tick.")]
    [Range(0f, 100f)]
    [SerializeField] private float damagePerTick = 4f;

    [Tooltip("Seconds between damage ticks while the player overlaps an acid segment.")]
    [Range(0.01f, 4f)]
    [SerializeField] private float applyIntervalSeconds = 0.55f;

    [Tooltip("Minimum planar movement speed required before this enemy emits acid segments.")]
    [Range(0f, 8f)]
    [SerializeField] private float minimumMovementSpeed = 0.1f;

    [Tooltip("Optional one-shot VFX prefab spawned once for each emitted acid trail segment. Leave empty for gameplay-only trails.")]
    [SerializeField] private GameObject trailSegmentVfxPrefab;

    [Tooltip("When enabled, the trail segment VFX uniform scale is multiplied by the segment diameter.")]
    [SerializeField] private bool scaleTrailSegmentVfxToRadius = true;

    [Tooltip("Uniform scale multiplier applied to each acid trail segment VFX instance.")]
    [Range(0.01f, 8f)]
    [SerializeField] private float trailSegmentVfxScaleMultiplier = 1f;

    [Tooltip("When enabled, runtime enemy wander-target gizmos also draw this enemy's acid trail segments.")]
    [SerializeField] private bool debugDrawSegments = true;
    #endregion

    #endregion

    #region Properties
    public float TrailSegmentLifetimeSeconds
    {
        get
        {
            return trailSegmentLifetimeSeconds;
        }
    }

    public float TrailSpawnDistance
    {
        get
        {
            return trailSpawnDistance;
        }
    }

    public float TrailSpawnIntervalSeconds
    {
        get
        {
            return trailSpawnIntervalSeconds;
        }
    }

    public float TrailRadius
    {
        get
        {
            return trailRadius;
        }
    }

    public int MaxActiveSegmentsPerEnemy
    {
        get
        {
            return maxActiveSegmentsPerEnemy;
        }
    }

    public float DamagePerTick
    {
        get
        {
            return damagePerTick;
        }
    }

    public float ApplyIntervalSeconds
    {
        get
        {
            return applyIntervalSeconds;
        }
    }

    public float MinimumMovementSpeed
    {
        get
        {
            return minimumMovementSpeed;
        }
    }

    public GameObject TrailSegmentVfxPrefab
    {
        get
        {
            return trailSegmentVfxPrefab;
        }
    }

    public bool ScaleTrailSegmentVfxToRadius
    {
        get
        {
            return scaleTrailSegmentVfxToRadius;
        }
    }

    public float TrailSegmentVfxScaleMultiplier
    {
        get
        {
            return trailSegmentVfxScaleMultiplier;
        }
    }

    public bool DebugDrawSegments
    {
        get
        {
            return debugDrawSegments;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Acid wanderer payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups Wanderer mode and mode-specific payloads.
/// </summary>
[Serializable]
public sealed class EnemyWandererModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Wanderer movement mode used at runtime.")]
    [SerializeField] private EnemyWandererMode mode = EnemyWandererMode.Basic;

    [Tooltip("Payload used when Wanderer mode is Basic.")]
    [SerializeField] private EnemyWandererBasicPayload basic = new EnemyWandererBasicPayload();

    [Tooltip("Payload used when Wanderer mode is DVD.")]
    [SerializeField] private EnemyWandererDvdPayload dvd = new EnemyWandererDvdPayload();

    [Tooltip("Payload used when Wanderer mode is Acid.")]
    [SerializeField] private EnemyWandererAcidPayload acid = new EnemyWandererAcidPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyWandererMode Mode
    {
        get
        {
            return mode;
        }
    }

    public EnemyWandererBasicPayload Basic
    {
        get
        {
            return basic;
        }
    }

    public EnemyWandererDvdPayload Dvd
    {
        get
        {
            return dvd;
        }
    }

    public EnemyWandererAcidPayload Acid
    {
        get
        {
            return acid;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures Wanderer nested payload references remain structurally valid without snapping authored settings.
    /// </summary>
    public void Validate()
    {
        if (basic == null)
            basic = new EnemyWandererBasicPayload();

        if (dvd == null)
            dvd = new EnemyWandererDvdPayload();

        if (acid == null)
            acid = new EnemyWandererAcidPayload();

        basic.Validate();
        dvd.Validate();
        acid.Validate();
    }
    #endregion

    #endregion
}
