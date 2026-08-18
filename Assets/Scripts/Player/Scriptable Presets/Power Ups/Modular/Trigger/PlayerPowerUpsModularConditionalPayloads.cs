using System;
using UnityEngine;

#region Conditional Trigger Payloads
/// <summary>
/// Defines the discrete base-shot cadence that enables the sibling shooting modules of one passive or toggleable power-up.
/// </summary>
[Serializable]
public sealed class PowerUpDelayedShootApplicationModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Number of base shots required between applications. The sibling shooting modules affect the Xth shot, then the counter restarts.")]
    [SerializeField]
    private int shotInterval = 3;
    #endregion

    #endregion

    #region Properties
    public int ShotInterval => shotInterval;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the authored shot interval without normalizing invalid values so the management tool can report them.
    /// </summary>
    /// <param name="shotIntervalValue">Authored number of base shots between applications.</param>
    public void Configure(int shotIntervalValue)
    {
        shotInterval = shotIntervalValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored values because non-positive intervals are reported by non-mutating validation warnings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines the automatic charge condition used to arm sibling projectile or object-spawn modules for the next base shot.
/// </summary>
[Serializable]
public sealed class PowerUpSuddenStrikeModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Condition that must remain satisfied while the sibling Trigger Hold Charge payload accumulates charge automatically.")]
    [SerializeField]
    private SuddenStrikeChargeConditionMode conditionMode = SuddenStrikeChargeConditionMode.Stationary;

    [Tooltip("When enabled in Stationary mode, player look rotation also interrupts automatic charge accumulation.")]
    [SerializeField]
    private bool countRotationAsMovement;

    [Tooltip("Maximum planar movement speed that is still considered stationary.")]
    [SerializeField]
    private float stationarySpeedTolerance = 0.05f;

    [Tooltip("Maximum angular speed in degrees per second that is still considered stationary when rotation is counted.")]
    [SerializeField]
    private float stationaryRotationToleranceDegrees = 1f;

    [Tooltip("When enabled, the movement slow authored by Trigger Hold Charge is applied while the selected charge condition remains satisfied.")]
    [SerializeField]
    private bool applyChargeMovementSlow;

    [Tooltip("Seconds used to remove the applied charge movement slow linearly after the selected condition stops being satisfied. Zero removes it immediately.")]
    [SerializeField]
    private float movementSlowRecoverySeconds = 0.25f;
    #endregion

    #endregion

    #region Properties
    public SuddenStrikeChargeConditionMode ConditionMode => conditionMode;
    public bool CountRotationAsMovement => countRotationAsMovement;
    public float StationarySpeedTolerance => stationarySpeedTolerance;
    public float StationaryRotationToleranceDegrees => stationaryRotationToleranceDegrees;
    public bool ApplyChargeMovementSlow => applyChargeMovementSlow;
    public float MovementSlowRecoverySeconds => movementSlowRecoverySeconds;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns automatic charge-condition settings while retaining authored values for warning-driven validation.
    /// </summary>
    /// <param name="conditionModeValue">Condition used to accumulate charge.</param>
    /// <param name="countRotationAsMovementValue">Whether look rotation interrupts Stationary mode.</param>
    /// <param name="stationarySpeedToleranceValue">Allowed planar speed while stationary.</param>
    /// <param name="stationaryRotationToleranceDegreesValue">Allowed angular speed while stationary.</param>
    /// <param name="applyChargeMovementSlowValue">Whether Trigger Hold Charge movement slow is applied.</param>
    /// <param name="movementSlowRecoverySecondsValue">Linear movement-slow recovery duration.</param>
    public void Configure(SuddenStrikeChargeConditionMode conditionModeValue,
                          bool countRotationAsMovementValue,
                          float stationarySpeedToleranceValue,
                          float stationaryRotationToleranceDegreesValue,
                          bool applyChargeMovementSlowValue,
                          float movementSlowRecoverySecondsValue)
    {
        conditionMode = conditionModeValue;
        countRotationAsMovement = countRotationAsMovementValue;
        stationarySpeedTolerance = stationarySpeedToleranceValue;
        stationaryRotationToleranceDegrees = stationaryRotationToleranceDegreesValue;
        applyChargeMovementSlow = applyChargeMovementSlowValue;
        movementSlowRecoverySeconds = movementSlowRecoverySecondsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored values because invalid tolerances and recovery durations are surfaced by the management tool.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines the health threshold that automatically executes sibling active-effect modules on a downward threshold crossing.
/// </summary>
[Serializable]
public sealed class PowerUpSelfPreservationInstinctModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Determines whether the activation threshold is a percentage of maximum health or a direct current-health value.")]
    [SerializeField]
    private SelfPreservationHealthThresholdMode thresholdMode = SelfPreservationHealthThresholdMode.MaximumHealthPercent;

    [Tooltip("Health threshold that triggers sibling active effects when reached from above. Percentage mode expects a value from 0 to 100.")]
    [SerializeField]
    private float healthThreshold = 25f;
    #endregion

    #endregion

    #region Properties
    public SelfPreservationHealthThresholdMode ThresholdMode => thresholdMode;
    public float HealthThreshold => healthThreshold;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the threshold interpretation and authored value without snapping invalid input.
    /// </summary>
    /// <param name="thresholdModeValue">Interpretation applied to the threshold value.</param>
    /// <param name="healthThresholdValue">Authored percentage or direct health threshold.</param>
    public void Configure(SelfPreservationHealthThresholdMode thresholdModeValue, float healthThresholdValue)
    {
        thresholdMode = thresholdModeValue;
        healthThreshold = healthThresholdValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Preserves authored values because threshold inconsistencies are reported by non-mutating validation warnings.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
#endregion
