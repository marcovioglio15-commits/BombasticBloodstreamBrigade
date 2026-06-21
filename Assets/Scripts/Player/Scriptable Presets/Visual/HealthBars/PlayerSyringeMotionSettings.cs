using System;
using UnityEngine;

/// <summary>
/// Stores optional movement and value-change reactions used by one player syringe.
/// </summary>
[Serializable]
public sealed class PlayerSyringeMotionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables inertial liquid movement opposite to player acceleration.")]
    [SerializeField] private bool movementReactionEnabled = true;

    [Tooltip("Converts player horizontal acceleration into normalized liquid displacement.")]
    [Range(0f, 4f)]
    [SerializeField] private float sloshStrength = 0.08f;

    [Tooltip("Converts normalized slosh displacement into a visible liquid-surface slope.")]
    [Range(0f, 1f)]
    [SerializeField] private float surfaceSloshStrength = 0.45f;

    [Tooltip("Enables horizontal inertial displacement of the liquid boundary and procedural bubbles.")]
    [SerializeField] private bool horizontalSloshEnabled = true;

    [Tooltip("Converts normalized slosh displacement into horizontal liquid and bubble travel along the graduated value track.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float horizontalSloshStrength = 0.16f;

    [Tooltip("Spring force returning liquid displacement to rest.")]
    [Range(0f, 100f)]
    [SerializeField] private float sloshSpring = 18f;

    [Tooltip("Damping applied while liquid displacement returns to rest.")]
    [Range(0f, 50f)]
    [SerializeField] private float sloshDamping = 8f;

    [Tooltip("Maximum normalized inertial liquid displacement.")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumSlosh = 0.25f;

    [Tooltip("Enables a small Z-axis syringe inclination driven by player movement.")]
    [SerializeField] private bool tiltEnabled = true;

    [Tooltip("Maximum absolute Z-axis inclination in degrees.")]
    [Range(0f, 20f)]
    [SerializeField] private float maximumTiltDegrees = 2.5f;

    [Tooltip("Spring force returning syringe inclination to rest.")]
    [Range(0f, 100f)]
    [SerializeField] private float tiltSpring = 16f;

    [Tooltip("Damping applied while syringe inclination returns to rest.")]
    [Range(0f, 50f)]
    [SerializeField] private float tiltDamping = 8f;

    [Tooltip("Enables a liquid impulse when the represented current value changes.")]
    [SerializeField] private bool valueImpulseEnabled = true;

    [Tooltip("Converts normalized value delta into an additional liquid impulse.")]
    [Range(0f, 4f)]
    [SerializeField] private float valueImpulseStrength = 0.65f;

    [Tooltip("Exponential decay speed of value-change impulses.")]
    [Range(0f, 50f)]
    [SerializeField] private float valueImpulseDecay = 7f;
    #endregion

    #endregion

    #region Properties
    public bool MovementReactionEnabled => movementReactionEnabled;
    public float SloshStrength => sloshStrength;
    public float SurfaceSloshStrength => surfaceSloshStrength;
    public bool HorizontalSloshEnabled => horizontalSloshEnabled;
    public float HorizontalSloshStrength => horizontalSloshStrength;
    public float SloshSpring => sloshSpring;
    public float SloshDamping => sloshDamping;
    public float MaximumSlosh => maximumSlosh;
    public bool TiltEnabled => tiltEnabled;
    public float MaximumTiltDegrees => maximumTiltDegrees;
    public float TiltSpring => tiltSpring;
    public float TiltDamping => tiltDamping;
    public bool ValueImpulseEnabled => valueImpulseEnabled;
    public float ValueImpulseStrength => valueImpulseStrength;
    public float ValueImpulseDecay => valueImpulseDecay;
    #endregion
}
