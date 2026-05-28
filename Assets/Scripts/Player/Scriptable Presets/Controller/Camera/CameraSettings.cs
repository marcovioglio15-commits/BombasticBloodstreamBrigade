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
    #endregion

    #region Validation
    /// <summary>
    /// Ensures the camera values block stays structurally valid. Numeric ranges are never snapped here:
    /// out-of-range values are surfaced as non-destructive editor warnings and clamped defensively at point of use.
    /// </summary>
    public void Validate()
    {
        if (values == null)
            values = new CameraValues();
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
