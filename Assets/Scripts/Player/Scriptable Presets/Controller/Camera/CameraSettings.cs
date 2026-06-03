using System;
using UnityEngine.Serialization;
using UnityEngine;

#region Camera Settings
/// <summary>
/// Owns the camera follow behavior, smoothing values and the two trauma-based shake channels (damage on hit, fire on shot).
/// Both shake channels expose the same multi-axis, zoom and single-impulse extension so designers can mix sharp tactile
/// feedback with the existing noise model from one consistent authoring surface.
/// </summary>
[Serializable]
public sealed class CameraSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Camera Behavior")]
    [Tooltip("Defines the overall camera behavior for the player.")]
    [FormerlySerializedAs("m_Behavior")]
    [SerializeField] private CameraBehavior behavior = CameraBehavior.FollowWithAutoOffset;

    [Tooltip("Fixed follow offset when using FollowWithOffset behavior.")]
    [FormerlySerializedAs("m_FollowOffset")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 10f, -8f);

    [Tooltip("Anchor used when RoomFixed behavior is selected.")]
    [FormerlySerializedAs("m_RoomAnchor")]
    [SerializeField] private Transform roomAnchor;

    [Header("Camera Values")]
    [Tooltip("Numeric camera tuning values.")]
    [FormerlySerializedAs("m_Values")]
    [SerializeField] private CameraValues values = new CameraValues();

    [Header("Camera Damage Shake")]
    [Tooltip("Customizable trauma-based camera shake played when the player takes valid damage.")]
    [SerializeField] private CameraDamageShakeSettings damageShake = new CameraDamageShakeSettings();

    [Header("Camera Fire Shake")]
    [Tooltip("Customizable trauma-based camera shake played every time the player fires a primary shot. Splitting projectiles spawned from existing shots do not retrigger this shake.")]
    [SerializeField] private CameraFireShakeSettings fireShake = new CameraFireShakeSettings();
    #endregion

    #endregion

    #region Properties
    public CameraBehavior Behavior
    {
        get
        {
            return behavior;
        }
    }

    public Vector3 FollowOffset
    {
        get
        {
            return followOffset;
        }
    }

    public Transform RoomAnchor
    {
        get
        {
            return roomAnchor;
        }
    }

    public CameraValues Values
    {
        get
        {
            return values;
        }
    }

    public CameraDamageShakeSettings DamageShake
    {
        get
        {
            return damageShake;
        }
    }

    public CameraFireShakeSettings FireShake
    {
        get
        {
            return fireShake;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Ensures the camera value, damage-shake and fire-shake blocks stay structurally valid. Numeric ranges are never
    /// snapped here: out-of-range values are surfaced as non-destructive editor warnings and clamped defensively at
    /// point of use.
    /// </summary>
    public void Validate()
    {
        if (values == null)
            values = new CameraValues();

        if (damageShake == null)
            damageShake = new CameraDamageShakeSettings();

        if (fireShake == null)
            fireShake = new CameraFireShakeSettings();
    }
    #endregion

    #endregion
}
#endregion

#region Camera Values
/// <summary>
/// Numeric tuning of the critically damped follow spring: smoothing time, leash radius and dead-zone radius.
/// </summary>
[Serializable]
public sealed class CameraValues
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Approximate seconds for the follow camera to reach the player. Drives a critically damped spring (SmoothDamp): velocity-continuous, no overshoot, frame-rate independent. Lower is snappier, higher is floatier; 0 makes the camera snap instantly.")]
    [SerializeField] private float smoothTime = 0.15f;

    [Tooltip("Maximum distance the camera is allowed to lag behind the target. The target is leashed to this radius before smoothing. 0 disables the leash so the spring alone governs the follow.")]
    [FormerlySerializedAs("m_MaxFollowDistance")]
    [SerializeField] private float maxFollowDistance = 6f;

    [Tooltip("Radius around the target where the camera stays still. The spring eases to rest a dead-zone radius short of the target instead of snapping at the threshold.")]
    [FormerlySerializedAs("m_DeadZoneRadius")]
    [SerializeField] private float deadZoneRadius = 0.2f;
    #endregion

    #endregion

    #region Properties
    public float SmoothTime
    {
        get
        {
            return smoothTime;
        }
    }

    public float MaxFollowDistance
    {
        get
        {
            return maxFollowDistance;
        }
    }

    public float DeadZoneRadius
    {
        get
        {
            return deadZoneRadius;
        }
    }
    #endregion
}
#endregion

#region Damage Shake Settings
/// <summary>
/// Damage-driven trauma camera shake. Adds trauma on a valid hit, decays it linearly over the configured duration and
/// produces a per-frame position offset, view-axis roll, optional camera-forward dolly and optional FOV zoom output.
/// Each axis (Right, Up, Forward) can be toggled independently and the whole motion can switch from the perlin-noise
/// "Continuous" model to a clean "SingleImpulse" single jolt. The connected-gamepad rumble follows the same envelope
/// and can also be re-shaped into a single-impulse burst that fires once per accepted hit and rests right after.
/// </summary>
[Serializable]
public sealed class CameraDamageShakeSettings
{
    #region Fields

    #region Serialized Fields - Master
    [Tooltip("Master toggle for the damage-driven camera shake. When disabled no trauma is accumulated and no offset is applied.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Seconds a single full-strength hit takes to fully fade out. Trauma decays linearly at 1/Duration per second, so weaker or damage-scaled hits fade proportionally faster. Base feel is a fast 0.25s kick.")]
    [SerializeField] private float durationSeconds = 0.25f;

    [Tooltip("Envelope shape mapping the remaining trauma to the shake magnitude. Linear is constant decay, Smooth eases in and out, Quadratic keeps a punchy peak with a soft tail.")]
    [SerializeField] private CameraShakeFalloff falloff = CameraShakeFalloff.Smooth;

    [Tooltip("Continuous oscillates the offset along the active axes through a perlin field driven by Frequency. Single Impulse skips the oscillation and applies one clean jolt that follows the trauma envelope and rests as soon as it decays.")]
    [SerializeField] private CameraShakeMotionMode motionMode = CameraShakeMotionMode.Continuous;
    #endregion

    #region Serialized Fields - Damage Scaling
    [Tooltip("When enabled, a hit's added trauma scales with how much survivability it removed instead of always applying a full-strength shake.")]
    [SerializeField] private bool scaleWithDamage;

    [Tooltip("Damage amount (health plus shield removed by one hit) that produces a full-strength shake when Scale With Damage is enabled. Lighter hits shake proportionally less.")]
    [SerializeField] private float damageForFullStrength = 25f;
    #endregion

    #region Serialized Fields - Axes & Amplitudes
    [Tooltip("When enabled, the shake displaces the camera along the camera Right (left/right) axis.")]
    [SerializeField] private bool axisRightEnabled = true;

    [Tooltip("When enabled, the shake displaces the camera along the camera Up (vertical) axis.")]
    [SerializeField] private bool axisUpEnabled = true;

    [Tooltip("When enabled, the shake displaces the camera along the camera Forward (push/pull, depth) axis.")]
    [SerializeField] private bool axisForwardEnabled;

    [Tooltip("Maximum positional displacement in world units applied along the planar (Right/Up) axes at full shake strength. Each axis can be toggled independently with Axis Right/Up Enabled.")]
    [SerializeField] private float positionalAmplitude = 0.5f;

    [Tooltip("Maximum positional displacement in world units applied along the camera Forward (depth) axis at full shake strength. Use a smaller value than Positional Amplitude to keep the push/pull subtle.")]
    [SerializeField] private float forwardAmplitude = 0.2f;

    [Tooltip("Maximum roll in degrees applied around the camera view axis at full shake strength. Set to 0 to keep the shake purely positional.")]
    [SerializeField] private float rotationalAmplitude = 1.5f;

    [Tooltip("Perlin noise sampling speed in cycles per second for the Continuous motion mode. Higher feels sharper and more frantic, lower feels like a slow sway. 0 produces a static, non-oscillating push.")]
    [SerializeField] private float frequency = 22f;
    #endregion

    #region Serialized Fields - Zoom
    [Tooltip("When enabled, the shake also pulses the camera field-of-view following the same trauma envelope. Use small deltas to keep the zoom readable.")]
    [SerializeField] private bool zoomEnabled;

    [Tooltip("Peak FOV delta in degrees applied at full shake strength. Negative values zoom IN (FOV decreases), positive values zoom OUT (FOV increases). The runtime restores the base FOV as the trauma decays.")]
    [SerializeField] private float zoomFovDelta = -3f;
    #endregion

    #region Serialized Fields - Controller Rumble
    [Tooltip("When enabled, a connected gamepad rumbles alongside the camera shake using the same trauma envelope. Disable to keep the on-screen shake without any controller vibration.")]
    [SerializeField] private bool rumbleEnabled = true;

    [Tooltip("Continuous mirrors the on-screen shake decay so the rumble fades out together with the kick. Single Impulse fires a clean burst of fixed duration once per accepted hit and rests right after.")]
    [SerializeField] private CameraShakeRumbleMotionMode rumbleMotionMode = CameraShakeRumbleMotionMode.Continuous;

    [Tooltip("Seconds the Single Impulse rumble holds at full motor speed before resting. Ignored while Rumble Motion Mode is Continuous.")]
    [SerializeField] private float rumbleImpulseDurationSeconds = 0.12f;

    [Tooltip("Heavy (low-frequency) motor intensity at full shake strength, in the [0..1] range. 0 silences the heavy motor.")]
    [SerializeField] private float rumbleLowFrequency = 0.6f;

    [Tooltip("Light (high-frequency) motor intensity at full shake strength, in the [0..1] range. 0 silences the light motor.")]
    [SerializeField] private float rumbleHighFrequency = 0.35f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public float DurationSeconds
    {
        get
        {
            return durationSeconds;
        }
    }

    public CameraShakeFalloff Falloff
    {
        get
        {
            return falloff;
        }
    }

    public CameraShakeMotionMode MotionMode
    {
        get
        {
            return motionMode;
        }
    }

    public bool ScaleWithDamage
    {
        get
        {
            return scaleWithDamage;
        }
    }

    public float DamageForFullStrength
    {
        get
        {
            return damageForFullStrength;
        }
    }

    public bool AxisRightEnabled
    {
        get
        {
            return axisRightEnabled;
        }
    }

    public bool AxisUpEnabled
    {
        get
        {
            return axisUpEnabled;
        }
    }

    public bool AxisForwardEnabled
    {
        get
        {
            return axisForwardEnabled;
        }
    }

    public float PositionalAmplitude
    {
        get
        {
            return positionalAmplitude;
        }
    }

    public float ForwardAmplitude
    {
        get
        {
            return forwardAmplitude;
        }
    }

    public float RotationalAmplitude
    {
        get
        {
            return rotationalAmplitude;
        }
    }

    public float Frequency
    {
        get
        {
            return frequency;
        }
    }

    public bool ZoomEnabled
    {
        get
        {
            return zoomEnabled;
        }
    }

    public float ZoomFovDelta
    {
        get
        {
            return zoomFovDelta;
        }
    }

    public bool RumbleEnabled
    {
        get
        {
            return rumbleEnabled;
        }
    }

    public CameraShakeRumbleMotionMode RumbleMotionMode
    {
        get
        {
            return rumbleMotionMode;
        }
    }

    public float RumbleImpulseDurationSeconds
    {
        get
        {
            return rumbleImpulseDurationSeconds;
        }
    }

    public float RumbleLowFrequency
    {
        get
        {
            return rumbleLowFrequency;
        }
    }

    public float RumbleHighFrequency
    {
        get
        {
            return rumbleHighFrequency;
        }
    }
    #endregion
}
#endregion

#region Fire Shake Settings
/// <summary>
/// Fire-driven trauma camera shake. Mirror of <see cref="CameraDamageShakeSettings"/> minus the damage-scaling block:
/// trauma is added once per primary-shot spawn and split-children are suppressed at the producer so secondary fragments
/// never retrigger this kick. The same multi-axis/zoom/single-impulse extensions are exposed so designers can author
/// fire feedback that complements the damage shake without coupling their amplitudes.
/// </summary>
[Serializable]
public sealed class CameraFireShakeSettings
{
    #region Fields

    #region Serialized Fields - Master
    [Tooltip("Master toggle for the fire-driven camera shake. When disabled no trauma is accumulated and no offset is applied even if the player shoots.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Seconds a single full-strength shot takes to fully fade out. Trauma decays linearly at 1/Duration per second, so rapid fire stacks into a sustained shake while a single shot fades quickly. Base feel is a snappy 0.18s kick.")]
    [SerializeField] private float durationSeconds = 0.18f;

    [Tooltip("Envelope shape mapping the remaining trauma to the shake magnitude. Linear is constant decay, Smooth eases in and out, Quadratic keeps a punchy peak with a soft tail.")]
    [SerializeField] private CameraShakeFalloff falloff = CameraShakeFalloff.Quadratic;

    [Tooltip("Continuous oscillates the offset along the active axes through a perlin field driven by Frequency. Single Impulse skips the oscillation and applies one clean jolt per shot that follows the trauma envelope and rests as soon as it decays.")]
    [SerializeField] private CameraShakeMotionMode motionMode = CameraShakeMotionMode.Continuous;

    [Tooltip("When enabled, the fire shake is fully suppressed while the player is firing a Laser Beam (passive or active-triggered). Use this to keep the continuous laser tick from stacking trauma into a sustained kick or rumble.")]
    [SerializeField] private bool suppressOnLaserBeam = true;
    #endregion

    #region Serialized Fields - Axes & Amplitudes
    [Tooltip("When enabled, the shake displaces the camera along the camera Right (left/right) axis.")]
    [SerializeField] private bool axisRightEnabled = true;

    [Tooltip("When enabled, the shake displaces the camera along the camera Up (vertical) axis.")]
    [SerializeField] private bool axisUpEnabled = true;

    [Tooltip("When enabled, the shake displaces the camera along the camera Forward (push/pull, depth) axis.")]
    [SerializeField] private bool axisForwardEnabled;

    [Tooltip("Maximum positional displacement in world units applied along the planar (Right/Up) axes at full shake strength. Each axis can be toggled independently with Axis Right/Up Enabled.")]
    [SerializeField] private float positionalAmplitude = 0.18f;

    [Tooltip("Maximum positional displacement in world units applied along the camera Forward (depth) axis at full shake strength. Use a smaller value than Positional Amplitude to keep the push/pull subtle.")]
    [SerializeField] private float forwardAmplitude = 0.1f;

    [Tooltip("Maximum roll in degrees applied around the camera view axis at full shake strength. Set to 0 to keep the shake purely positional.")]
    [SerializeField] private float rotationalAmplitude = 0.6f;

    [Tooltip("Perlin noise sampling speed in cycles per second for the Continuous motion mode. Higher feels sharper and more frantic, lower feels like a slow sway. 0 produces a static, non-oscillating push.")]
    [SerializeField] private float frequency = 28f;
    #endregion

    #region Serialized Fields - Zoom
    [Tooltip("When enabled, the shake also pulses the camera field-of-view following the same trauma envelope. Use small deltas to keep the zoom readable while firing.")]
    [SerializeField] private bool zoomEnabled;

    [Tooltip("Peak FOV delta in degrees applied at full shake strength. Negative values zoom IN (FOV decreases), positive values zoom OUT (FOV increases). The runtime restores the base FOV as the trauma decays.")]
    [SerializeField] private float zoomFovDelta = -1.5f;
    #endregion

    #region Serialized Fields - Controller Rumble
    [Tooltip("When enabled, a connected gamepad rumbles alongside the camera shake using the same trauma envelope. Disable to keep the on-screen shake without any controller vibration.")]
    [SerializeField] private bool rumbleEnabled = true;

    [Tooltip("Continuous mirrors the on-screen shake decay so the rumble fades out together with the kick. Single Impulse fires a clean burst of fixed duration once per shot and rests right after.")]
    [SerializeField] private CameraShakeRumbleMotionMode rumbleMotionMode = CameraShakeRumbleMotionMode.Continuous;

    [Tooltip("Seconds the Single Impulse rumble holds at full motor speed before resting. Ignored while Rumble Motion Mode is Continuous.")]
    [SerializeField] private float rumbleImpulseDurationSeconds = 0.08f;

    [Tooltip("Heavy (low-frequency) motor intensity at full shake strength, in the [0..1] range. 0 silences the heavy motor.")]
    [SerializeField] private float rumbleLowFrequency = 0.25f;

    [Tooltip("Light (high-frequency) motor intensity at full shake strength, in the [0..1] range. 0 silences the light motor.")]
    [SerializeField] private float rumbleHighFrequency = 0.45f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public float DurationSeconds
    {
        get
        {
            return durationSeconds;
        }
    }

    public CameraShakeFalloff Falloff
    {
        get
        {
            return falloff;
        }
    }

    public CameraShakeMotionMode MotionMode
    {
        get
        {
            return motionMode;
        }
    }

    public bool SuppressOnLaserBeam
    {
        get
        {
            return suppressOnLaserBeam;
        }
    }

    public bool AxisRightEnabled
    {
        get
        {
            return axisRightEnabled;
        }
    }

    public bool AxisUpEnabled
    {
        get
        {
            return axisUpEnabled;
        }
    }

    public bool AxisForwardEnabled
    {
        get
        {
            return axisForwardEnabled;
        }
    }

    public float PositionalAmplitude
    {
        get
        {
            return positionalAmplitude;
        }
    }

    public float ForwardAmplitude
    {
        get
        {
            return forwardAmplitude;
        }
    }

    public float RotationalAmplitude
    {
        get
        {
            return rotationalAmplitude;
        }
    }

    public float Frequency
    {
        get
        {
            return frequency;
        }
    }

    public bool ZoomEnabled
    {
        get
        {
            return zoomEnabled;
        }
    }

    public float ZoomFovDelta
    {
        get
        {
            return zoomFovDelta;
        }
    }

    public bool RumbleEnabled
    {
        get
        {
            return rumbleEnabled;
        }
    }

    public CameraShakeRumbleMotionMode RumbleMotionMode
    {
        get
        {
            return rumbleMotionMode;
        }
    }

    public float RumbleImpulseDurationSeconds
    {
        get
        {
            return rumbleImpulseDurationSeconds;
        }
    }

    public float RumbleLowFrequency
    {
        get
        {
            return rumbleLowFrequency;
        }
    }

    public float RumbleHighFrequency
    {
        get
        {
            return rumbleHighFrequency;
        }
    }
    #endregion
}
#endregion
