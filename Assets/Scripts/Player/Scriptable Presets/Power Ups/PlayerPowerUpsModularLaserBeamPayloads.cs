using System;
using UnityEngine;
using UnityEngine.Serialization;

#region Enums
/// <summary>
/// Selects the body silhouette used by the Laser Beam presentation runtime.
/// </summary>
public enum LaserBeamBodyProfile
{
    RoundedTube = 0,
    TaperedJet = 1,
    DenseRibbon = 2
}

/// <summary>
/// Selects the cap-shape family used by Laser Beam source and impact visuals.
/// </summary>
public enum LaserBeamCapShape
{
    BubbleBurst = 0,
    StarBloom = 1,
    SoftDisc = 2
}
#endregion

#region Runtime Payloads
/// <summary>
/// Stores scalable gameplay and presentation settings for the Laser Beam passive shooting override.
/// </summary>
[Serializable]
public sealed class PowerUpLaserBeamModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Gameplay")]
    [Tooltip("Damage multiplier applied by each traveling damage packet emitted by the beam on tick.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Damage-per-second multiplier applied continuously across the whole beam length while the beam is active.")]
    [SerializeField] private float continuousDamagePerSecondMultiplier = 0.28f;

    [Tooltip("Speed multiplier applied to the virtual projectile speed that drives Laser Beam reach growth and throughput.")]
    [SerializeField] private float virtualProjectileSpeedMultiplier = 1f;

    [Tooltip("Seconds between consecutive Laser Beam damage applications while at least one lane is valid.")]
    [SerializeField] private float damageTickIntervalSeconds = 0.15f;

    [Tooltip("Maximum uninterrupted active time before Laser Beam overheats. Ignored when Cooldown Seconds is 0.")]
    [SerializeField] private float maximumContinuousActiveSeconds = 2.5f;

    [Tooltip("Cooldown applied only after the uninterrupted active-time cap is reached. When set to 0 the beam can remain active indefinitely while held.")]
    [SerializeField] private float cooldownSeconds = 2f;

    [Tooltip("Maximum number of reflected wall segments resolved per lane when bouncing projectiles are also active.")]
    [SerializeField] private int maximumBounceSegments = 0;

    [Header("Presentation")]
    [Tooltip("Stable Laser Beam visual preset ID resolved against the active Player Visual Preset library at runtime.")]
    [PlayerLaserBeamVisualPresetSelector]
    [FormerlySerializedAs("visualPalette")]
    [SerializeField] private int visualPresetId = PlayerLaserBeamVisualDefaultsUtility.DefaultVisualPresetId;

    [Tooltip("Body silhouette profile used by the beam shader and segment scaling.")]
    [SerializeField] private LaserBeamBodyProfile bodyProfile = LaserBeamBodyProfile.DenseRibbon;

    [Tooltip("Shape family used by the source electrical bloom near the muzzle.")]
    [SerializeField] private LaserBeamCapShape sourceShape = LaserBeamCapShape.BubbleBurst;

    [Tooltip("Shape family used by the impact electrical bloom at the current beam termination point.")]
    [FormerlySerializedAs("impactShape")]
    [SerializeField] private LaserBeamCapShape terminalCapShape = LaserBeamCapShape.StarBloom;

    [Tooltip("Visual width multiplier applied to the rendered beam body.")]
    [SerializeField] private float bodyWidthMultiplier = 4.25f;

    [Tooltip("Additional width multiplier applied to gameplay collision checks performed by the beam.")]
    [SerializeField] private float collisionWidthMultiplier = 1f;

    [Tooltip("Scale multiplier applied to the source burst effect.")]
    [SerializeField] private float sourceScaleMultiplier = 1.35f;

    [Tooltip("Scale multiplier applied to the always-on rounded terminal cap rendered at the end of the beam.")]
    [FormerlySerializedAs("impactScaleMultiplier")]
    [SerializeField] private float terminalCapScaleMultiplier = 1.85f;

    [Tooltip("Scale multiplier applied to the conditional wall-contact flare layered on top of the terminal cap.")]
    [SerializeField] private float contactFlareScaleMultiplier = 1.2f;

    [Tooltip("Overall opacity multiplier applied by the beam shader.")]
    [SerializeField] private float bodyOpacity = 0.96f;

    [Tooltip("Relative thickness of the inner white-hot core rendered inside the main beam sheath.")]
    [SerializeField] private float coreWidthMultiplier = 0.52f;

    [Tooltip("Brightness multiplier applied to the inner beam core.")]
    [SerializeField] private float coreBrightness = 1.45f;

    [Tooltip("Brightness multiplier applied to the external storm rim and charged edge accents.")]
    [SerializeField] private float rimBrightness = 1.32f;

    [Tooltip("Speed multiplier applied to the traveling body-flow streaks visible inside the beam.")]
    [SerializeField] private float flowScrollSpeed = 1.15f;

    [Tooltip("Frequency of the secondary shimmer that keeps the beam volume alive between damage bursts.")]
    [SerializeField] private float flowPulseFrequency = 1.6f;

    [Tooltip("Twisting speed of the electrical storm strands orbiting around the beam body.")]
    [FormerlySerializedAs("tickPulseTravelSpeed")]
    [SerializeField] private float stormTwistSpeed = 14f;

    [Tooltip("Additional seconds for which the storm trail remains active after the tick pulse reaches the end of the beam.")]
    [FormerlySerializedAs("tickPulseLength")]
    [FormerlySerializedAs("stormBurstDurationSeconds")]
    [SerializeField] private float stormTickPostTravelHoldSeconds = 0.6f;

    [Tooltip("Baseline electrical storm intensity visible even while no damage burst is currently active.")]
    [FormerlySerializedAs("tickPulseWidthBoost")]
    [SerializeField] private float stormIdleIntensity = 0.48f;

    [Tooltip("Additional electrical storm intensity layered on top of the idle storm when damage is applied.")]
    [FormerlySerializedAs("tickPulseBrightnessBoost")]
    [SerializeField] private float stormBurstIntensity = 1.1f;

    [Tooltip("Additional offset applied from the muzzle before the visible beam opens into the main flow.")]
    [SerializeField] private float sourceOffset = 0.08f;

    [Tooltip("Intensity multiplier applied to the electrical discharge accents rendered around the source aperture.")]
    [SerializeField] private float sourceDischargeIntensity = 1.2f;

    [Tooltip("Additional width multiplier applied to the detached outer storm shell rendered around the beam body.")]
    [SerializeField] private float stormShellWidthMultiplier = 1.12f;

    [Tooltip("World-space separation factor used to pull the detached storm shell away from the main flow.")]
    [SerializeField] private float stormShellSeparation = 0.32f;

    [Tooltip("Frequency of the looped storm rings distributed along the beam length.")]
    [SerializeField] private float stormRingFrequency = 5.4f;

    [Tooltip("Thickness of the looped storm rings rendered by the detached shell.")]
    [SerializeField] private float stormRingThickness = 0.18f;

    [Tooltip("Travel speed of the highlighted storm packet emitted on each damage tick.")]
    [SerializeField] private float stormTickTravelSpeed = 1f;

    [Tooltip("Additional world-space distance appended to the storm damage front behind each traveling packet.")]
    [SerializeField] private float stormTickDamageLengthTolerance = 0.18f;

    [Tooltip("Brightness multiplier applied to the rounded terminal cap that closes the beam.")]
    [SerializeField] private float terminalCapIntensity = 1.12f;

    [Tooltip("Brightness multiplier applied to the conditional wall-contact flare layered on top of the terminal cap.")]
    [SerializeField] private float contactFlareIntensity = 1.28f;

    [Tooltip("Amplitude of the secondary body breathing used to keep the beam volume fluid even when no damage pulse is crossing it.")]
    [SerializeField] private float wobbleAmplitude = 0.08f;

    [Tooltip("Speed of the secondary drift noise used by source and impact effects.")]
    [SerializeField] private float bubbleDriftSpeed = 1.8f;
    #endregion

    #endregion

    #region Properties
    public float DamageMultiplier
    {
        get
        {
            return damageMultiplier;
        }
    }

    public float ContinuousDamagePerSecondMultiplier
    {
        get
        {
            return continuousDamagePerSecondMultiplier;
        }
    }

    public float VirtualProjectileSpeedMultiplier
    {
        get
        {
            return virtualProjectileSpeedMultiplier;
        }
    }

    public float DamageTickIntervalSeconds
    {
        get
        {
            return damageTickIntervalSeconds;
        }
    }

    public float MaximumContinuousActiveSeconds
    {
        get
        {
            return maximumContinuousActiveSeconds;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }

    public int MaximumBounceSegments
    {
        get
        {
            return maximumBounceSegments;
        }
    }

    public int VisualPresetId
    {
        get
        {
            return visualPresetId;
        }
    }

    public LaserBeamBodyProfile BodyProfile
    {
        get
        {
            return bodyProfile;
        }
    }

    public LaserBeamCapShape SourceShape
    {
        get
        {
            return sourceShape;
        }
    }

    public LaserBeamCapShape TerminalCapShape
    {
        get
        {
            return terminalCapShape;
        }
    }

    public float BodyWidthMultiplier
    {
        get
        {
            return bodyWidthMultiplier;
        }
    }

    public float CollisionWidthMultiplier
    {
        get
        {
            return collisionWidthMultiplier;
        }
    }

    public float SourceScaleMultiplier
    {
        get
        {
            return sourceScaleMultiplier;
        }
    }

    public float TerminalCapScaleMultiplier
    {
        get
        {
            return terminalCapScaleMultiplier;
        }
    }

    public float ContactFlareScaleMultiplier
    {
        get
        {
            return contactFlareScaleMultiplier;
        }
    }

    public float BodyOpacity
    {
        get
        {
            return bodyOpacity;
        }
    }

    public float CoreWidthMultiplier
    {
        get
        {
            return coreWidthMultiplier;
        }
    }

    public float CoreBrightness
    {
        get
        {
            return coreBrightness;
        }
    }

    public float RimBrightness
    {
        get
        {
            return rimBrightness;
        }
    }

    public float FlowScrollSpeed
    {
        get
        {
            return flowScrollSpeed;
        }
    }

    public float FlowPulseFrequency
    {
        get
        {
            return flowPulseFrequency;
        }
    }

    public float StormTwistSpeed
    {
        get
        {
            return stormTwistSpeed;
        }
    }

    public float StormTickPostTravelHoldSeconds
    {
        get
        {
            return stormTickPostTravelHoldSeconds;
        }
    }

    public float StormIdleIntensity
    {
        get
        {
            return stormIdleIntensity;
        }
    }

    public float StormBurstIntensity
    {
        get
        {
            return stormBurstIntensity;
        }
    }

    public float SourceOffset
    {
        get
        {
            return sourceOffset;
        }
    }

    public float SourceDischargeIntensity
    {
        get
        {
            return sourceDischargeIntensity;
        }
    }

    public float StormShellWidthMultiplier
    {
        get
        {
            return stormShellWidthMultiplier;
        }
    }

    public float StormShellSeparation
    {
        get
        {
            return stormShellSeparation;
        }
    }

    public float StormRingFrequency
    {
        get
        {
            return stormRingFrequency;
        }
    }

    public float StormRingThickness
    {
        get
        {
            return stormRingThickness;
        }
    }

    public float StormTickTravelSpeed
    {
        get
        {
            return stormTickTravelSpeed;
        }
    }

    public float StormTickDamageLengthTolerance
    {
        get
        {
            return stormTickDamageLengthTolerance;
        }
    }

    public float TerminalCapIntensity
    {
        get
        {
            return terminalCapIntensity;
        }
    }

    public float ContactFlareIntensity
    {
        get
        {
            return contactFlareIntensity;
        }
    }

    public float WobbleAmplitude
    {
        get
        {
            return wobbleAmplitude;
        }
    }

    public float BubbleDriftSpeed
    {
        get
        {
            return bubbleDriftSpeed;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Keeps the payload API symmetrical with the other modular payloads without mutating authored numeric values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
#endregion
