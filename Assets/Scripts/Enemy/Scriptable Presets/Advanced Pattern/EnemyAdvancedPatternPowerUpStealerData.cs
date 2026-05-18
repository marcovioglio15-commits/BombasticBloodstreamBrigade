using System;
using UnityEngine;

/// <summary>
/// Contains target and trigger settings for enemy Power-Up Stealer modules.
/// </summary>
[Serializable]
public sealed class EnemyPowerUpStealerModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When this module attempts to steal one eligible player power-up. Module Activation fires when the module becomes active, including boss extraction re-activation.")]
    [SerializeField] private EnemyPowerUpStealTriggerMode triggerMode = EnemyPowerUpStealTriggerMode.OnFirstPlayerHit;

    [Tooltip("Which player power-up category can be stolen by this module.")]
    [SerializeField] private EnemyPowerUpStealTargetKind targetKind = EnemyPowerUpStealTargetKind.ActiveOrPassive;

    [Tooltip("Selects which eligible power-up inside the resolved category is stolen. First/Last use active equip order and passive acquisition order, while Random samples only valid entries.")]
    [SerializeField] private EnemyPowerUpStealSelectionMode selectionMode = EnemyPowerUpStealSelectionMode.FirstObtained;

    [Tooltip("Percentage chance to try an active power-up before a passive one when Target Kind is Active Or Passive. Runtime falls back to the other category if the preferred one is unavailable.")]
    [Range(0f, 100f)]
    [SerializeField] private float activeTargetBiasPercent = 50f;

    [Header("Recovery")]
    [Tooltip("When enabled, the stolen power-up is returned after the enemy loses the configured percentage of max health after the steal.")]
    [SerializeField] private bool recoverAfterDamageTakenPercent;

    [Tooltip("Max-health percentage that must be lost after the steal before returning the stolen power-up.")]
    [Min(0f)]
    [SerializeField] private float recoveryDamageTakenPercent = 25f;

    [Tooltip("When enabled, the stolen power-up is returned after the enemy loses the configured max-health percentage inside the configured time window.")]
    [SerializeField] private bool recoverAfterDamageWindow;

    [Tooltip("Max-health percentage that must be lost inside the recovery time window before returning the stolen power-up.")]
    [Min(0f)]
    [SerializeField] private float recoveryDamageWindowPercent = 20f;

    [Tooltip("Seconds used by the timed damage recovery window. The accumulated damage resets when the window elapses.")]
    [Min(0f)]
    [SerializeField] private float recoveryDamageWindowSeconds = 5f;
    #endregion

    #endregion

    #region Properties
    public EnemyPowerUpStealTriggerMode TriggerMode
    {
        get
        {
            return triggerMode;
        }
    }

    public EnemyPowerUpStealTargetKind TargetKind
    {
        get
        {
            return targetKind;
        }
    }

    public EnemyPowerUpStealSelectionMode SelectionMode
    {
        get
        {
            return selectionMode;
        }
    }

    public float ActiveTargetBiasPercent
    {
        get
        {
            return activeTargetBiasPercent;
        }
    }

    public bool RecoverAfterDamageTakenPercent
    {
        get
        {
            return recoverAfterDamageTakenPercent;
        }
    }

    public float RecoveryDamageTakenPercent
    {
        get
        {
            return recoveryDamageTakenPercent;
        }
    }

    public bool RecoverAfterDamageWindow
    {
        get
        {
            return recoverAfterDamageWindow;
        }
    }

    public float RecoveryDamageWindowPercent
    {
        get
        {
            return recoveryDamageWindowPercent;
        }
    }

    public float RecoveryDamageWindowSeconds
    {
        get
        {
            return recoveryDamageWindowSeconds;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Preserves authored Power-Up Stealer values while keeping a validation hook consistent with other module payloads.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
