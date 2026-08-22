using System;
using UnityEngine;

#region Module Payloads
[Serializable]
public sealed class PowerUpHoldChargeModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Charge amount required to release the charged effect.")]
    [SerializeField] private float requiredCharge = 500f;

    [Tooltip("Upper cap for accumulated charge while the trigger is held.")]
    [SerializeField] private float maximumCharge = 500f;

    [Tooltip("Charge gained per second while the trigger is held.")]
    [SerializeField] private float chargeRatePerSecond = 125f;

    [Tooltip("Optional upper-body animation slot played continuously while this Trigger Hold Charge module is charging. None leaves the current upper-body presentation unchanged.")]
    [SerializeField] private PlayerChargeAnimationClipSlot chargeAnimationClipSlot;

    [Tooltip("Optional upper-body animation slot played once when this Trigger Hold Charge module is released. None skips the release animation.")]
    [SerializeField] private PlayerReleaseAnimationClipSlot releaseAnimationClipSlot;

    [Tooltip("When enabled, stored charge decays over time after the trigger is released instead of resetting immediately.")]
    [SerializeField] private bool decayAfterRelease;

    [Tooltip("Percentage of Maximum Charge lost per second after release while Decay After Release is enabled.")]
    [SerializeField] private float decayAfterReleasePercentPerSecond = 25f;

    [Tooltip("When enabled, charge can build over time even while the trigger is not pressed.")]
    [SerializeField] private bool passiveChargeGainWhileReleased;

    [Tooltip("Percentage of Maximum Charge gained per second while the trigger is not pressed when Passive Charge Gain While Released is enabled.")]
    [SerializeField] private float passiveChargeGainPercentPerSecond = 10f;

    [Tooltip("Seconds for which a Laser Beam triggered by this active charge-shot remains active after release.")]
    [SerializeField] private float laserDurationSeconds = 0.45f;

    [Tooltip("When projectile speed inheritance is enabled, ignore inherited player velocity on the world X axis for charge-shot projectiles emitted by this module.")]
    [SerializeField] private bool ignoreInheritedPlayerVelocityX;

    [Tooltip("When projectile speed inheritance is enabled, ignore inherited player velocity on the world Z axis for charge-shot projectiles emitted by this module.")]
    [SerializeField] private bool ignoreInheritedPlayerVelocityZ;

    [Header("Charged Laser Beam")]
    [Tooltip("When enabled, a normal fully charged release fires a standalone Laser Beam instead of projectile requests. With Sudden Strike, the beam fires alongside the qualifying base shot. It ignores unrelated passive tools and power-up hooks.")]
    [SerializeField] private bool useChargedLaserBeam;

    [Tooltip("Seconds for which the standalone charged Laser Beam remains active after a fully charged release.")]
    [SerializeField] private float chargedLaserDurationSeconds = 0.45f;

    [Tooltip("Standalone Laser Beam settings used only by this hold-charge release when Use Charged Laser Beam is enabled.")]
    [SerializeField] private PowerUpLaserBeamModuleData chargedLaserBeam = new PowerUpLaserBeamModuleData();

    [Tooltip("When enabled, the player's movement is slowed progressively while this charge trigger is held.")]
    [SerializeField] private bool slowPlayerWhileCharging;

    [Tooltip("Maximum movement slow percentage applied when charge progress reaches the end of the normalized slow curve.")]
    [SerializeField] private float maximumPlayerSlowPercent = 35f;

    [Tooltip("Normalized movement slow curve evaluated from 0 to 1 charge progress. Curve values are multiplied by Maximum Player Slow Percent during bake/runtime.")]
    [SerializeField] private AnimationCurve playerSlowCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    #endregion

    #endregion

    #region Properties
    public float RequiredCharge
    {
        get
        {
            return requiredCharge;
        }
    }

    public float MaximumCharge
    {
        get
        {
            return maximumCharge;
        }
    }

    public float ChargeRatePerSecond
    {
        get
        {
            return chargeRatePerSecond;
        }
    }

    public PlayerChargeAnimationClipSlot ChargeAnimationClipSlot
    {
        get
        {
            return chargeAnimationClipSlot;
        }
    }

    public PlayerReleaseAnimationClipSlot ReleaseAnimationClipSlot
    {
        get
        {
            return releaseAnimationClipSlot;
        }
    }

    public bool DecayAfterRelease
    {
        get
        {
            return decayAfterRelease;
        }
    }

    public float DecayAfterReleasePercentPerSecond
    {
        get
        {
            return decayAfterReleasePercentPerSecond;
        }
    }

    public bool PassiveChargeGainWhileReleased
    {
        get
        {
            return passiveChargeGainWhileReleased;
        }
    }

    public float PassiveChargeGainPercentPerSecond
    {
        get
        {
            return passiveChargeGainPercentPerSecond;
        }
    }

    public float LaserDurationSeconds
    {
        get
        {
            return laserDurationSeconds;
        }
    }

    public bool IgnoreInheritedPlayerVelocityX
    {
        get
        {
            return ignoreInheritedPlayerVelocityX;
        }
    }

    public bool IgnoreInheritedPlayerVelocityZ
    {
        get
        {
            return ignoreInheritedPlayerVelocityZ;
        }
    }

    public bool UseChargedLaserBeam
    {
        get
        {
            return useChargedLaserBeam;
        }
    }

    public float ChargedLaserDurationSeconds
    {
        get
        {
            return chargedLaserDurationSeconds;
        }
    }

    public PowerUpLaserBeamModuleData ChargedLaserBeam
    {
        get
        {
            return chargedLaserBeam;
        }
    }

    public bool SlowPlayerWhileCharging
    {
        get
        {
            return slowPlayerWhileCharging;
        }
    }

    public float MaximumPlayerSlowPercent
    {
        get
        {
            return maximumPlayerSlowPercent;
        }
    }

    public AnimationCurve PlayerSlowCurve
    {
        get
        {
            return playerSlowCurve;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float requiredChargeValue, float maximumChargeValue, float chargeRatePerSecondValue)
    {
        Configure(requiredChargeValue,
                  maximumChargeValue,
                  chargeRatePerSecondValue,
                  false,
                  25f,
                  false,
                  10f,
                  0.45f,
                  false,
                  35f,
                  CreateDefaultSlowCurve());
    }

    /// <summary>
    /// Assigns the optional upper-body animation slots used while charging and on release.
    /// </summary>
    /// <param name="chargeAnimationClipSlotValue">Animation bindings slot played while charging.</param>
    /// <param name="releaseAnimationClipSlotValue">Animation bindings slot played once on release.</param>
    public void ConfigureAnimations(PlayerChargeAnimationClipSlot chargeAnimationClipSlotValue,
                                    PlayerReleaseAnimationClipSlot releaseAnimationClipSlotValue)
    {
        chargeAnimationClipSlot = chargeAnimationClipSlotValue;
        releaseAnimationClipSlot = releaseAnimationClipSlotValue;
    }

    public void Configure(float requiredChargeValue,
                          float maximumChargeValue,
                          float chargeRatePerSecondValue,
                          bool decayAfterReleaseValue,
                          float decayAfterReleasePercentPerSecondValue,
                          bool passiveChargeGainWhileReleasedValue,
                          float passiveChargeGainPercentPerSecondValue,
                          float laserDurationSecondsValue)
    {
        Configure(requiredChargeValue,
                  maximumChargeValue,
                  chargeRatePerSecondValue,
                  decayAfterReleaseValue,
                  decayAfterReleasePercentPerSecondValue,
                  passiveChargeGainWhileReleasedValue,
                  passiveChargeGainPercentPerSecondValue,
                  laserDurationSecondsValue,
                  false,
                  0.45f,
                  null,
                  false,
                  35f,
                  CreateDefaultSlowCurve());
    }

    public void Configure(float requiredChargeValue,
                          float maximumChargeValue,
                          float chargeRatePerSecondValue,
                          bool decayAfterReleaseValue,
                          float decayAfterReleasePercentPerSecondValue,
                          bool passiveChargeGainWhileReleasedValue,
                          float passiveChargeGainPercentPerSecondValue,
                          float laserDurationSecondsValue,
                          bool slowPlayerWhileChargingValue,
                          float maximumPlayerSlowPercentValue,
                          AnimationCurve playerSlowCurveValue)
    {
        Configure(requiredChargeValue,
                  maximumChargeValue,
                  chargeRatePerSecondValue,
                  decayAfterReleaseValue,
                  decayAfterReleasePercentPerSecondValue,
                  passiveChargeGainWhileReleasedValue,
                  passiveChargeGainPercentPerSecondValue,
                  laserDurationSecondsValue,
                  false,
                  0.45f,
                  null,
                  slowPlayerWhileChargingValue,
                  maximumPlayerSlowPercentValue,
                  playerSlowCurveValue);
    }

    public void Configure(float requiredChargeValue,
                          float maximumChargeValue,
                          float chargeRatePerSecondValue,
                          bool decayAfterReleaseValue,
                          float decayAfterReleasePercentPerSecondValue,
                          bool passiveChargeGainWhileReleasedValue,
                          float passiveChargeGainPercentPerSecondValue,
                          float laserDurationSecondsValue,
                          bool useChargedLaserBeamValue,
                          float chargedLaserDurationSecondsValue,
                          PowerUpLaserBeamModuleData chargedLaserBeamValue,
                          bool slowPlayerWhileChargingValue,
                          float maximumPlayerSlowPercentValue,
                          AnimationCurve playerSlowCurveValue)
    {
        requiredCharge = requiredChargeValue;
        maximumCharge = maximumChargeValue;
        chargeRatePerSecond = chargeRatePerSecondValue;
        decayAfterRelease = decayAfterReleaseValue;
        decayAfterReleasePercentPerSecond = decayAfterReleasePercentPerSecondValue;
        passiveChargeGainWhileReleased = passiveChargeGainWhileReleasedValue;
        passiveChargeGainPercentPerSecond = passiveChargeGainPercentPerSecondValue;
        laserDurationSeconds = laserDurationSecondsValue;
        ignoreInheritedPlayerVelocityX = false;
        ignoreInheritedPlayerVelocityZ = false;
        useChargedLaserBeam = useChargedLaserBeamValue;
        chargedLaserDurationSeconds = chargedLaserDurationSecondsValue;
        chargedLaserBeam = chargedLaserBeamValue != null ? chargedLaserBeamValue : new PowerUpLaserBeamModuleData();
        slowPlayerWhileCharging = slowPlayerWhileChargingValue;
        maximumPlayerSlowPercent = maximumPlayerSlowPercentValue;
        playerSlowCurve = playerSlowCurveValue != null ? playerSlowCurveValue : CreateDefaultSlowCurve();
    }
    #endregion

    #region Validation
    /// <summary>
    /// Keeps reference payloads allocated without snapping authored numeric values.
    /// </summary>
    public void Validate()
    {
        if (chargedLaserBeam == null)
            chargedLaserBeam = new PowerUpLaserBeamModuleData();

        chargedLaserBeam.Validate();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates the default normalized charge-slow curve used when a payload is initialized from code.
    /// </summary>
    /// <returns>Linear normalized curve from 0 charge to full charge.</returns>
    private static AnimationCurve CreateDefaultSlowCurve()
    {
        return AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
    #endregion

    #endregion
}

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
        activationResource = activationResourceValue;
        maintenanceResource = maintenanceResourceValue;
        maximumEnergy = maximumEnergyValue;
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
