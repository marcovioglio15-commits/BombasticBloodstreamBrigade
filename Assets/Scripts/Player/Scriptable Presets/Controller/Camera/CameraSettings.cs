using System;
using UnityEngine.Serialization;
using UnityEngine;

[Serializable]
public sealed class CameraSettings
{
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
    #endregion

    #region Validation
    /// <summary>
    /// Ensures the camera value and damage-shake blocks stay structurally valid. Numeric ranges are never snapped
    /// here: out-of-range values are surfaced as non-destructive editor warnings and clamped defensively at point of use.
    /// </summary>
    public void Validate()
    {
        if (values == null)
            values = new CameraValues();

        if (damageShake == null)
            damageShake = new CameraDamageShakeSettings();
    }
    #endregion
}

[Serializable]
public sealed class CameraValues
{
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

[Serializable]
public sealed class CameraDamageShakeSettings
{
    #region Serialized Fields
    [Tooltip("Master toggle for the damage-driven camera shake. When disabled no trauma is accumulated and no offset is applied.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Seconds a single full-strength hit takes to fully fade out. Trauma decays linearly at 1/Duration per second, so weaker or damage-scaled hits fade proportionally faster. Base feel is a fast 0.25s kick.")]
    [SerializeField] private float durationSeconds = 0.25f;

    [Tooltip("Maximum positional displacement in world units applied along the camera screen plane at full shake strength.")]
    [SerializeField] private float positionalAmplitude = 0.5f;

    [Tooltip("Maximum roll in degrees applied around the camera view axis at full shake strength. Set to 0 to keep the shake purely positional.")]
    [SerializeField] private float rotationalAmplitude = 1.5f;

    [Tooltip("Perlin noise sampling speed in cycles per second. Higher values feel sharper and more frantic, lower values feel like a slow sway. 0 produces a static, non-oscillating push.")]
    [SerializeField] private float frequency = 22f;

    [Tooltip("Envelope shape mapping the remaining trauma to the shake magnitude. Linear is constant decay, Smooth eases in and out, Quadratic keeps a punchy peak with a soft tail.")]
    [SerializeField] private CameraShakeFalloff falloff = CameraShakeFalloff.Smooth;

    [Tooltip("When enabled, a hit's added trauma scales with how much survivability it removed instead of always applying a full-strength shake.")]
    [SerializeField] private bool scaleWithDamage;

    [Tooltip("Damage amount (health plus shield removed by one hit) that produces a full-strength shake when Scale With Damage is enabled. Lighter hits shake proportionally less.")]
    [SerializeField] private float damageForFullStrength = 25f;

    [Header("Controller Rumble")]
    [Tooltip("When enabled, a connected gamepad rumbles alongside the camera shake using the same trauma envelope. Disable to keep the on-screen shake without any controller vibration.")]
    [SerializeField] private bool rumbleEnabled = true;

    [Tooltip("Heavy (low-frequency) motor intensity at full shake strength, in the [0..1] range. The motor follows the same trauma decay as the shake, so it fades out together with the on-screen kick. 0 silences the heavy motor.")]
    [SerializeField] private float rumbleLowFrequency = 0.6f;

    [Tooltip("Light (high-frequency) motor intensity at full shake strength, in the [0..1] range. The motor follows the same trauma decay as the shake, so it fades out together with the on-screen kick. 0 silences the light motor.")]
    [SerializeField] private float rumbleHighFrequency = 0.35f;
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

    public float PositionalAmplitude
    {
        get
        {
            return positionalAmplitude;
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

    public CameraShakeFalloff Falloff
    {
        get
        {
            return falloff;
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

    public bool RumbleEnabled
    {
        get
        {
            return rumbleEnabled;
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
