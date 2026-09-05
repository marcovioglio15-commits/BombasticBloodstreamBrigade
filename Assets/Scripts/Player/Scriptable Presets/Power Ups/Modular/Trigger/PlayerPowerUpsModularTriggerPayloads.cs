using System;
using UnityEngine;

#region Module Payloads
[Serializable]
public sealed class PowerUpResourceGateModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Resource used to pay activation.")]
    [SerializeField] private PowerUpResourceType activationResource = PowerUpResourceType.Energy;

    [Tooltip("Resource consumed by toggle maintenance and by Returning Projectiles resource-drain return modes while a projectile remains outside.")]
    [SerializeField] private PowerUpResourceType maintenanceResource = PowerUpResourceType.Energy;

    [Tooltip("Maximum energy capacity for this active power up.")]
    [SerializeField] private float maximumEnergy = 100f;

    [Tooltip("Energy available immediately when this active power up is obtained or its runtime slot is reset.")]
    [SerializeField]
    private float initialEnergy = 100f;

    [Tooltip("Resource amount consumed on activation.")]
    [SerializeField] private float activationCost = 100f;

    [Tooltip("Resource consumed per second by an enabled toggle or a Returning Projectiles resource-drain return mode.")]
    [SerializeField] private float maintenanceCostPerSecond;

    [Tooltip("When enabled, this gate turns the power-up into a press-to-toggle active that keeps passive-compatible effects enabled while runtime maintenance is paid.")]
    [SerializeField] private bool isToggleable;

    [Tooltip("How many maintenance ticks are applied every second while the toggleable power-up remains active after the startup interval.")]
    [SerializeField] private float maintenanceTicksPerSecond = 4f;

    [Tooltip("Minimum energy percentage required to activate. 0 disables this gate.")]
    [SerializeField] private float minimumActivationEnergyPercent;

    [Tooltip("Charge source used to refill energy over time/events.")]
    [SerializeField] private PowerUpChargeType chargeType = PowerUpChargeType.Time;

    [Tooltip("Recharge amount gained per trigger unit of the selected charge source.")]
    [SerializeField] private float chargePerTrigger = 100f;

    [Tooltip("Cooldown in seconds applied after a successful activation.")]
    [SerializeField] private float cooldownSeconds = 1f;

    [Tooltip("When enabled, the toggleable power-up can recharge energy during the startup interval defined by Cooldown Seconds.")]
    [SerializeField] private bool allowRechargeDuringToggleStartupLock;

    [Tooltip("Maximum seconds a toggleable power-up may remain active before it switches off automatically. Zero keeps the toggle active until input, interruption, or resource failure deactivates it.")]
    [SerializeField]
    private float maximumToggleActiveDurationSeconds;
    #endregion

    #endregion

    #region Properties
    public PowerUpResourceType ActivationResource
    {
        get
        {
            return activationResource;
        }
    }

    public PowerUpResourceType MaintenanceResource
    {
        get
        {
            return maintenanceResource;
        }
    }

    public float MaximumEnergy
    {
        get
        {
            return maximumEnergy;
        }
    }

    public float InitialEnergy
    {
        get
        {
            return initialEnergy;
        }
    }

    public float ActivationCost
    {
        get
        {
            return activationCost;
        }
    }

    public float MaintenanceCostPerSecond
    {
        get
        {
            return maintenanceCostPerSecond;
        }
    }

    public bool IsToggleable
    {
        get
        {
            return isToggleable;
        }
    }

    public float MaintenanceTicksPerSecond
    {
        get
        {
            return maintenanceTicksPerSecond;
        }
    }

    public float MinimumActivationEnergyPercent
    {
        get
        {
            return minimumActivationEnergyPercent;
        }
    }

    public PowerUpChargeType ChargeType
    {
        get
        {
            return chargeType;
        }
    }

    public float ChargePerTrigger
    {
        get
        {
            return chargePerTrigger;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }

    public bool AllowRechargeDuringToggleStartupLock
    {
        get
        {
            return allowRechargeDuringToggleStartupLock;
        }
    }

    public float MaximumToggleActiveDurationSeconds
    {
        get
        {
            return maximumToggleActiveDurationSeconds;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue)
    {
        Configure(activationResourceValue,
                  maintenanceResourceValue,
                  maximumEnergyValue,
                  activationCostValue,
                  maintenanceCostPerSecondValue,
                  minimumActivationEnergyPercentValue,
                  chargeTypeValue,
                  chargePerTriggerValue,
                  0f,
                  false,
                  4f,
                  false);
    }

    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue,
                          float cooldownSecondsValue)
    {
        Configure(activationResourceValue,
                  maintenanceResourceValue,
                  maximumEnergyValue,
                  activationCostValue,
                  maintenanceCostPerSecondValue,
                  minimumActivationEnergyPercentValue,
                  chargeTypeValue,
                  chargePerTriggerValue,
                  cooldownSecondsValue,
                  false,
                  4f,
                  false);
    }

    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue,
                          float cooldownSecondsValue,
                          bool isToggleableValue,
                          float maintenanceTicksPerSecondValue,
                          bool allowRechargeDuringToggleStartupLockValue)
    {
        Configure(activationResourceValue,
                  maintenanceResourceValue,
                  maximumEnergyValue,
                  activationCostValue,
                  maintenanceCostPerSecondValue,
                  minimumActivationEnergyPercentValue,
                  chargeTypeValue,
                  chargePerTriggerValue,
                  cooldownSecondsValue,
                  isToggleableValue,
                  maintenanceTicksPerSecondValue,
                  allowRechargeDuringToggleStartupLockValue,
                  0f);
    }

    /// <summary>
    /// Assigns resource, toggle-maintenance, and optional finite-lifetime settings without mutating invalid authored values.
    /// </summary>
    /// <param name="activationResourceValue">Resource charged when activation succeeds.</param>
    /// <param name="maintenanceResourceValue">Resource charged while the toggle remains active.</param>
    /// <param name="maximumEnergyValue">Maximum internal energy capacity.</param>
    /// <param name="activationCostValue">Resource amount charged on activation.</param>
    /// <param name="maintenanceCostPerSecondValue">Resource amount charged per active second.</param>
    /// <param name="minimumActivationEnergyPercentValue">Minimum energy percentage required for activation.</param>
    /// <param name="chargeTypeValue">Runtime event used to recharge energy.</param>
    /// <param name="chargePerTriggerValue">Energy restored by each recharge trigger.</param>
    /// <param name="cooldownSecondsValue">Cooldown or toggle startup-lock duration.</param>
    /// <param name="isToggleableValue">Whether activation switches persistent compatible effects on and off.</param>
    /// <param name="maintenanceTicksPerSecondValue">Number of maintenance payments attempted per second.</param>
    /// <param name="allowRechargeDuringToggleStartupLockValue">Whether recharge remains enabled during the startup lock.</param>
    /// <param name="maximumToggleActiveDurationSecondsValue">Maximum active lifetime, or zero for no time limit.</param>
    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue,
                          float cooldownSecondsValue,
                          bool isToggleableValue,
                          float maintenanceTicksPerSecondValue,
                          bool allowRechargeDuringToggleStartupLockValue,
                          float maximumToggleActiveDurationSecondsValue)
    {
        Configure(activationResourceValue,
                  maintenanceResourceValue,
                  maximumEnergyValue,
                  maximumEnergyValue,
                  activationCostValue,
                  maintenanceCostPerSecondValue,
                  minimumActivationEnergyPercentValue,
                  chargeTypeValue,
                  chargePerTriggerValue,
                  cooldownSecondsValue,
                  isToggleableValue,
                  maintenanceTicksPerSecondValue,
                  allowRechargeDuringToggleStartupLockValue,
                  maximumToggleActiveDurationSecondsValue);
    }

    /// <summary>
    /// Assigns the complete resource-gate payload, including the energy available when the power-up is obtained.
    /// </summary>
    /// <param name="activationResourceValue">Resource charged when activation succeeds.</param>
    /// <param name="maintenanceResourceValue">Resource charged while the toggle remains active.</param>
    /// <param name="maximumEnergyValue">Maximum internal energy capacity.</param>
    /// <param name="initialEnergyValue">Energy available on acquisition or slot reset.</param>
    /// <param name="activationCostValue">Resource amount charged on activation.</param>
    /// <param name="maintenanceCostPerSecondValue">Resource amount charged per active second.</param>
    /// <param name="minimumActivationEnergyPercentValue">Minimum energy percentage required for activation.</param>
    /// <param name="chargeTypeValue">Runtime event used to recharge energy.</param>
    /// <param name="chargePerTriggerValue">Energy restored by each recharge trigger.</param>
    /// <param name="cooldownSecondsValue">Cooldown or toggle startup-lock duration.</param>
    /// <param name="isToggleableValue">Whether activation switches persistent compatible effects on and off.</param>
    /// <param name="maintenanceTicksPerSecondValue">Number of maintenance payments attempted per second.</param>
    /// <param name="allowRechargeDuringToggleStartupLockValue">Whether recharge remains enabled during the startup lock.</param>
    /// <param name="maximumToggleActiveDurationSecondsValue">Maximum active lifetime, or zero for no time limit.</param>
    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float initialEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue,
                          float cooldownSecondsValue,
                          bool isToggleableValue,
                          float maintenanceTicksPerSecondValue,
                          bool allowRechargeDuringToggleStartupLockValue,
                          float maximumToggleActiveDurationSecondsValue)
    {
        activationResource = activationResourceValue;
        maintenanceResource = maintenanceResourceValue;
        maximumEnergy = maximumEnergyValue;
        initialEnergy = initialEnergyValue;
        activationCost = activationCostValue;
        maintenanceCostPerSecond = maintenanceCostPerSecondValue;
        isToggleable = isToggleableValue;
        maintenanceTicksPerSecond = maintenanceTicksPerSecondValue;
        minimumActivationEnergyPercent = minimumActivationEnergyPercentValue;
        chargeType = chargeTypeValue;
        chargePerTrigger = chargePerTriggerValue;
        cooldownSeconds = cooldownSecondsValue;
        allowRechargeDuringToggleStartupLock = allowRechargeDuringToggleStartupLockValue;
        maximumToggleActiveDurationSeconds = maximumToggleActiveDurationSecondsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpCooldownGateModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Seconds that must elapse before this power up can be activated again.")]
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

    #region Setup
    public void Configure(float cooldownSecondsValue)
    {
        cooldownSeconds = cooldownSecondsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps the payload callable from shared validation paths without snapping authored values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpSuppressShootingModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, base shooting is blocked while this state is active.")]
    [SerializeField] private bool suppressBaseShootingWhileActive = true;

    [Tooltip("When enabled, activation interrupts charging or active effects on the opposite slot.")]
    [SerializeField] private bool interruptOtherSlotOnEnter;

    [Tooltip("When enabled, interruption clears only opposite-slot charge state.")]
    [SerializeField] private bool interruptOtherSlotChargingOnly = true;
    #endregion

    #endregion

    #region Properties
    public bool SuppressBaseShootingWhileActive
    {
        get
        {
            return suppressBaseShootingWhileActive;
        }
    }

    public bool InterruptOtherSlotOnEnter
    {
        get
        {
            return interruptOtherSlotOnEnter;
        }
    }

    public bool InterruptOtherSlotChargingOnly
    {
        get
        {
            return interruptOtherSlotChargingOnly;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(bool suppressBaseShootingWhileActiveValue)
    {
        Configure(suppressBaseShootingWhileActiveValue, false, true);
    }

    public void Configure(bool suppressBaseShootingWhileActiveValue,
                          bool interruptOtherSlotOnEnterValue,
                          bool interruptOtherSlotChargingOnlyValue)
    {
        suppressBaseShootingWhileActive = suppressBaseShootingWhileActiveValue;
        interruptOtherSlotOnEnter = interruptOtherSlotOnEnterValue;
        interruptOtherSlotChargingOnly = interruptOtherSlotChargingOnlyValue;
    }
    #endregion

    #endregion
}
#endregion
