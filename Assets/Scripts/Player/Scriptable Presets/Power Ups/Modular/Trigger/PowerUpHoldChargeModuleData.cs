using System;
using UnityEngine;

/// <summary>
/// Stores the scalable trigger, release and feedback settings for manual charge and Sudden Strike.
/// </summary>
[Serializable]
public sealed class PowerUpHoldChargeModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Charge Complete Rumble")]
    [Tooltip("Emits one controller impulse at Maximum Charge, or when Sudden Strike becomes armed. Uses the Fire Rumble multiplier in user settings.")]
    [SerializeField]
    private bool chargeCompleteRumbleEnabled;

    [Tooltip("Duration in real seconds of the charge-completion impulse. Must be greater than zero.")]
    [SerializeField]
    private float chargeCompleteRumbleDurationSeconds = 0.12f;

    [Tooltip("Low-frequency motor strength for charge completion, from 0 to 1.")]
    [SerializeField]
    private float chargeCompleteRumbleLowFrequency = 0.35f;

    [Tooltip("High-frequency motor strength for charge completion, from 0 to 1.")]
    [SerializeField]
    private float chargeCompleteRumbleHighFrequency = 0.65f;

    [Header("Charge")]
    [Tooltip("Charge amount required to release the charged effect.")]
    [SerializeField]
    private float requiredCharge = 500f;

    [Tooltip("Upper cap for accumulated charge while the trigger is held.")]
    [SerializeField]
    private float maximumCharge = 500f;

    [Tooltip("Charge gained per second while the trigger is held.")]
    [SerializeField]
    private float chargeRatePerSecond = 125f;

    [Tooltip("Optional upper-body animation slot played continuously while this Trigger Hold Charge module is charging. None leaves the current upper-body presentation unchanged.")]
    [SerializeField]
    private PlayerChargeAnimationClipSlot chargeAnimationClipSlot;

    [Tooltip("Optional upper-body animation slot played once when this Trigger Hold Charge module is released. None skips the release animation.")]
    [SerializeField]
    private PlayerReleaseAnimationClipSlot releaseAnimationClipSlot;

    [Tooltip("When enabled, stored charge decays over time after the trigger is released instead of resetting immediately.")]
    [SerializeField]
    private bool decayAfterRelease;

    [Tooltip("Percentage of Maximum Charge lost per second after release while Decay After Release is enabled.")]
    [SerializeField]
    private float decayAfterReleasePercentPerSecond = 25f;

    [Tooltip("When enabled, charge can build over time even while the trigger is not pressed.")]
    [SerializeField]
    private bool passiveChargeGainWhileReleased;

    [Tooltip("Percentage of Maximum Charge gained per second while the trigger is not pressed when Passive Charge Gain While Released is enabled.")]
    [SerializeField]
    private float passiveChargeGainPercentPerSecond = 10f;

    [Tooltip("Seconds for which a Laser Beam triggered by this active charge-shot remains active after release.")]
    [SerializeField]
    private float laserDurationSeconds = 0.45f;

    [Tooltip("When projectile speed inheritance is enabled, ignore inherited player velocity on the world X axis for charge-shot projectiles emitted by this module.")]
    [SerializeField]
    private bool ignoreInheritedPlayerVelocityX;

    [Tooltip("When projectile speed inheritance is enabled, ignore inherited player velocity on the world Z axis for charge-shot projectiles emitted by this module.")]
    [SerializeField]
    private bool ignoreInheritedPlayerVelocityZ;

    [Header("Charged Laser Beam")]
    [Tooltip("When enabled, a normal fully charged release fires a standalone Laser Beam instead of projectile requests. With Sudden Strike, the beam fires alongside the qualifying base shot. It ignores unrelated passive tools and power-up hooks.")]
    [SerializeField]
    private bool useChargedLaserBeam;

    [Tooltip("Seconds for which the standalone charged Laser Beam remains active after a fully charged release.")]
    [SerializeField]
    private float chargedLaserDurationSeconds = 0.45f;

    [Tooltip("Standalone Laser Beam settings used only by this hold-charge release when Use Charged Laser Beam is enabled.")]
    [SerializeField]
    private PowerUpLaserBeamModuleData chargedLaserBeam = new PowerUpLaserBeamModuleData();

    [Tooltip("When enabled, the player's movement is slowed progressively while this charge trigger is held.")]
    [SerializeField]
    private bool slowPlayerWhileCharging;

    [Tooltip("Maximum movement slow percentage applied when charge progress reaches the end of the normalized slow curve.")]
    [SerializeField]
    private float maximumPlayerSlowPercent = 35f;

    [Tooltip("Normalized movement slow curve evaluated from 0 to 1 charge progress. Curve values are multiplied by Maximum Player Slow Percent during bake/runtime.")]
    [SerializeField]
    private AnimationCurve playerSlowCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    #endregion

    #endregion

    #region Properties
    public bool ChargeCompleteRumbleEnabled => chargeCompleteRumbleEnabled;
    public float ChargeCompleteRumbleDurationSeconds => chargeCompleteRumbleDurationSeconds;
    public float ChargeCompleteRumbleLowFrequency => chargeCompleteRumbleLowFrequency;
    public float ChargeCompleteRumbleHighFrequency => chargeCompleteRumbleHighFrequency;

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
    /// <summary>
    /// Initializes charge behavior for preset defaults or a scripted payload while retaining optional feedback tuning.
    /// </summary>
    /// <param name="requiredChargeValue">Minimum charge needed to execute a release.</param>
    /// <param name="maximumChargeValue">Charge cap used by the full-charge HUD and rumble.</param>
    /// <param name="chargeRatePerSecondValue">Charge accumulated each second while held.</param>
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

    /// <summary>
    /// Initializes charge behavior for preset defaults or a scripted payload while retaining optional feedback tuning.
    /// </summary>
    /// <param name="requiredChargeValue">Minimum charge needed to execute a release.</param>
    /// <param name="maximumChargeValue">Charge cap used by the full-charge HUD and rumble.</param>
    /// <param name="chargeRatePerSecondValue">Charge accumulated each second while held.</param>
    /// <param name="decayAfterReleaseValue">Whether stored charge decays after release.</param>
    /// <param name="decayAfterReleasePercentPerSecondValue">Percentage of maximum charge lost per second.</param>
    /// <param name="passiveChargeGainWhileReleasedValue">Whether charge builds while input is released.</param>
    /// <param name="passiveChargeGainPercentPerSecondValue">Percentage of maximum charge gained per second.</param>
    /// <param name="laserDurationSecondsValue">Duration for the compatible laser release path.</param>
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

    /// <summary>
    /// Initializes charge behavior for preset defaults or a scripted payload while retaining optional feedback tuning.
    /// </summary>
    /// <param name="requiredChargeValue">Minimum charge needed to execute a release.</param>
    /// <param name="maximumChargeValue">Charge cap used by the full-charge HUD and rumble.</param>
    /// <param name="chargeRatePerSecondValue">Charge accumulated each second while held.</param>
    /// <param name="decayAfterReleaseValue">Whether stored charge decays after release.</param>
    /// <param name="decayAfterReleasePercentPerSecondValue">Percentage of maximum charge lost per second.</param>
    /// <param name="passiveChargeGainWhileReleasedValue">Whether charge builds while input is released.</param>
    /// <param name="passiveChargeGainPercentPerSecondValue">Percentage of maximum charge gained per second.</param>
    /// <param name="laserDurationSecondsValue">Duration for the compatible laser release path.</param>
    /// <param name="slowPlayerWhileChargingValue">Whether charge applies the movement slow curve.</param>
    /// <param name="maximumPlayerSlowPercentValue">Maximum movement reduction as a percentage.</param>
    /// <param name="playerSlowCurveValue">Normalized slow curve, or null for the default linear curve.</param>
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

    /// <summary>
    /// Initializes charge behavior for preset defaults or a scripted payload while retaining optional feedback tuning.
    /// </summary>
    /// <param name="requiredChargeValue">Minimum charge needed to execute a release.</param>
    /// <param name="maximumChargeValue">Charge cap used by the full-charge HUD and rumble.</param>
    /// <param name="chargeRatePerSecondValue">Charge accumulated each second while held.</param>
    /// <param name="decayAfterReleaseValue">Whether stored charge decays after release.</param>
    /// <param name="decayAfterReleasePercentPerSecondValue">Percentage of maximum charge lost per second.</param>
    /// <param name="passiveChargeGainWhileReleasedValue">Whether charge builds while input is released.</param>
    /// <param name="passiveChargeGainPercentPerSecondValue">Percentage of maximum charge gained per second.</param>
    /// <param name="laserDurationSecondsValue">Duration for the compatible laser release path.</param>
    /// <param name="useChargedLaserBeamValue">Whether release uses a standalone charged beam.</param>
    /// <param name="chargedLaserDurationSecondsValue">Duration of the standalone charged beam.</param>
    /// <param name="chargedLaserBeamValue">Standalone beam tuning, or null for defaults.</param>
    /// <param name="slowPlayerWhileChargingValue">Whether charge applies the movement slow curve.</param>
    /// <param name="maximumPlayerSlowPercentValue">Maximum movement reduction as a percentage.</param>
    /// <param name="playerSlowCurveValue">Normalized slow curve, or null for the default linear curve.</param>
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
        // Assign authored values without clamping; validation reports invalid tuning.
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
