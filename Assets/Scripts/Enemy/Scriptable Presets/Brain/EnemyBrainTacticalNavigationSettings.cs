using System;
using UnityEngine;

/// <summary>
/// Stores tactical pathfinding settings used by enemy candidate scoring.
/// </summary>
[Serializable]
public sealed class EnemyBrainTacticalNavigationSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Candidate budget used before LOD clamps the tactical scorer. Low is cheapest, High evaluates extra side and prediction lanes near the player.")]
    [SerializeField] private EnemyTacticalCandidateBudget candidateBudget = EnemyTacticalCandidateBudget.Balanced;

    [Tooltip("Weight applied to shared flow-field directions when direct movement is blocked or tactically worse.")]
    [Range(0f, 1f)]
    [SerializeField] private float navigationInfluence = 0.72f;

    [Tooltip("Seconds used to predict player and neighbor positions while scoring movement candidates.")]
    [Range(0f, 2f)]
    [SerializeField] private float predictionHorizonSeconds = 0.42f;

    [Tooltip("Weight for trajectories that approach the player and pass beside them instead of only chasing their current position.")]
    [Range(0f, 1f)]
    [SerializeField] private float sidePassPreference = 0.48f;

    [Tooltip("Weight for deterministic crowd lanes that reduce enemy-to-enemy indecision and pileups.")]
    [Range(0f, 1f)]
    [SerializeField] private float crowdLanePreference = 0.58f;

    [Tooltip("Weight for wall-tangent candidates when movement is blocked or stuck recovery is active.")]
    [Range(0f, 1f)]
    [SerializeField] private float wallTangentPreference = 0.64f;

    [Tooltip("Penalty applied to candidates that reverse the last committed movement direction.")]
    [Range(0f, 1f)]
    [SerializeField] private float oscillationDamping = 0.7f;

    [Tooltip("Seconds of poor displacement before stuck recovery gives stronger weight to tangent and flow-field alternatives.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float stuckRecoverySeconds = 0.42f;
    #endregion

    #endregion

    #region Properties
    public EnemyTacticalCandidateBudget CandidateBudget
    {
        get
        {
            return candidateBudget;
        }
    }

    public float NavigationInfluence
    {
        get
        {
            return navigationInfluence;
        }
    }

    public float PredictionHorizonSeconds
    {
        get
        {
            return predictionHorizonSeconds;
        }
    }

    public float SidePassPreference
    {
        get
        {
            return sidePassPreference;
        }
    }

    public float CrowdLanePreference
    {
        get
        {
            return crowdLanePreference;
        }
    }

    public float WallTangentPreference
    {
        get
        {
            return wallTangentPreference;
        }
    }

    public float OscillationDamping
    {
        get
        {
            return oscillationDamping;
        }
    }

    public float StuckRecoverySeconds
    {
        get
        {
            return stuckRecoverySeconds;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Keeps the tactical navigation settings object valid without snapping authored tuning values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
