using System;
using UnityEngine;

/// <summary>
/// Contains payload values for Coward flee movement.
/// none.
/// </summary>
[Serializable]
public sealed class EnemyCowardModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("1. Detection")]
    [Tooltip("Radius around the enemy used to detect the player and start a retreat.")]
    [Min(0f)]
    [SerializeField] private float detectionRadius = 8f;

    [Tooltip("Extra distance added over Detection Radius before the enemy stops retreating.")]
    [Min(0f)]
    [SerializeField] private float releaseDistanceBuffer = 1.5f;

    [Header("2. Retreat Distances")]
    [Tooltip("Radius around the enemy used to search for retreat destinations.")]
    [Min(0.5f)]
    [SerializeField] private float searchRadius = 10f;

    [Tooltip("Minimum distance from the current position required for a valid retreat destination.")]
    [Min(0f)]
    [SerializeField] private float minimumRetreatDistance = 2.5f;

    [Tooltip("Maximum distance from the current position allowed for a valid retreat destination.")]
    [Min(0f)]
    [SerializeField] private float maximumRetreatDistance = 8.5f;

    [Tooltip("Distance threshold used to consider the current retreat destination reached.")]
    [Range(0.05f, 1.5f)]
    [SerializeField] private float arrivalTolerance = 0.35f;

    [Tooltip("Number of candidate positions sampled for each retreat destination pick.")]
    [Range(6, 24)]
    [SerializeField] private int candidateSampleCount = 12;

    [Tooltip("When enabled, the picker scans the full 360° arc using Infinite Direction Step Degrees instead of Candidate Sample Count.")]
    [SerializeField] private bool useInfiniteDirectionSampling = true;

    [Tooltip("Angular resolution in degrees used by infinite direction sampling. Lower values are more exhaustive but more expensive.")]
    [Range(4f, 30f)]
    [SerializeField] private float infiniteDirectionStepDegrees = 8f;

    [Header("3. Retreat Steering")]
    [Tooltip("Minimum extra clearance in meters kept from neighboring enemies while retreating.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float minimumEnemyClearance = 0.25f;

    [Tooltip("Prediction time in seconds used to avoid retreat trajectories likely to intersect moving neighbors.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float trajectoryPredictionTime = 0.35f;

    [Tooltip("0 = permissive trajectories, 1 = strongly favor safer and cleaner retreat paths.")]
    [Range(0f, 1f)]
    [SerializeField] private float freeTrajectoryPreference = 0.85f;

    [Tooltip("0 = loose retreat direction, 1 = strongly favor moving directly away from the player.")]
    [Range(0f, 1f)]
    [SerializeField] private float retreatDirectionPreference = 0.65f;

    [Tooltip("0 = neutral, 1 = strongly favor destinations that feel open and less corner-prone.")]
    [Range(0f, 1f)]
    [SerializeField] private float openSpacePreference = 0.55f;

    [Tooltip("0 = mostly direct flee, 1 = stronger support from pathfinding when walls start interfering.")]
    [Range(0f, 1f)]
    [SerializeField] private float navigationRetreatPreference = 0.6f;

    [Header("4. Patrol")]
    [Tooltip("Local patrol radius used when the player is outside Detection Radius. The enemy keeps dancing inside this area instead of standing still.")]
    [Min(0.5f)]
    [SerializeField] private float patrolRadius = 3.5f;

    [Tooltip("Pause applied after reaching a patrol point before choosing the next one inside the patrol area.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float patrolWaitSeconds = 0.55f;

    [Tooltip("Speed multiplier applied while patrolling outside the fear area.")]
    [Range(0.25f, 1.25f)]
    [SerializeField] private float patrolSpeedMultiplier = 0.82f;

    [Header("5. Speed")]
    [Tooltip("Speed multiplier applied when the player is near the outer edge of the fear area.")]
    [Range(0.5f, 2f)]
    [SerializeField] private float retreatSpeedMultiplierFar = 1f;

    [Tooltip("Speed multiplier applied when the player is very close to the enemy.")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float retreatSpeedMultiplierNear = 1.4f;

    [Header("6. Recovery")]
    [Tooltip("Cooldown in seconds before retrying destination selection after blocked retreat candidates.")]
    [Range(0.02f, 0.35f)]
    [SerializeField] private float blockedPathRetryDelay = 0.2f;
    #endregion

    #endregion

    #region Properties
    public float DetectionRadius
    {
        get
        {
            return detectionRadius;
        }
    }

    public float ReleaseDistanceBuffer
    {
        get
        {
            return releaseDistanceBuffer;
        }
    }

    public float SearchRadius
    {
        get
        {
            return searchRadius;
        }
    }

    public float MinimumRetreatDistance
    {
        get
        {
            return minimumRetreatDistance;
        }
    }

    public float MaximumRetreatDistance
    {
        get
        {
            return maximumRetreatDistance;
        }
    }

    public float ArrivalTolerance
    {
        get
        {
            return arrivalTolerance;
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

    public float RetreatDirectionPreference
    {
        get
        {
            return retreatDirectionPreference;
        }
    }

    public float OpenSpacePreference
    {
        get
        {
            return openSpacePreference;
        }
    }

    public float NavigationRetreatPreference
    {
        get
        {
            return navigationRetreatPreference;
        }
    }

    public float PatrolRadius
    {
        get
        {
            return patrolRadius;
        }
    }

    public float PatrolWaitSeconds
    {
        get
        {
            return patrolWaitSeconds;
        }
    }

    public float PatrolSpeedMultiplier
    {
        get
        {
            return patrolSpeedMultiplier;
        }
    }

    public float RetreatSpeedMultiplierFar
    {
        get
        {
            return retreatSpeedMultiplierFar;
        }
    }

    public float RetreatSpeedMultiplierNear
    {
        get
        {
            return retreatSpeedMultiplierNear;
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
    /// Ensures Coward payload references remain structurally valid without snapping authored settings.
    /// none.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
