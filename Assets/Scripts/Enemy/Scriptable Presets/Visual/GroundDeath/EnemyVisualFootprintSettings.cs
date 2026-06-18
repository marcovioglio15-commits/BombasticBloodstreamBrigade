using System;
using UnityEngine;

/// <summary>
/// Defines how the enemy shadow and dual-ring fillable bars cover the baked projectile hit footprint.
/// </summary>
public enum EnemyShadowCoverageMode : byte
{
    ShadowOnly = 0,
    ShadowAndSpatialUi = 1
}

/// <summary>
/// Stores ground-footprint presentation settings consumed by the shader-driven enemy ground indicator,
/// which renders the hit-box shadow and optionally two concentric fillable rings or arcs for health and shield.
/// </summary>
[Serializable]
public sealed class EnemyVisualFootprintSettings
{
    #region Constants
    public const float DefaultRingArcDegrees = 360f;
    #endregion

    #region Fields

    #region Serialized Fields

    #region Layout
    [Tooltip("Controls whether the shadow alone covers the hit footprint or whether the shadow shrinks to leave room for the spatial UI rings.")]
    [SerializeField] private EnemyShadowCoverageMode shadowCoverageMode = EnemyShadowCoverageMode.ShadowOnly;

    [Tooltip("Vertical offset applied to the ground-projected indicator quad. Positive values lift the quad above the floor plane to avoid z-fighting; negative values sink it below the pivot when authored prefab origins sit above the ground.")]
    [SerializeField] private float spatialUiHeightOffset = 0.035f;

    [Tooltip("Local root-space XZ fine-tune added after the automatic visual-bounds center detection. Contact damage, debug rings, shadow and the indicator use this same resolved center.")]
    [SerializeField] private Vector2 positionOffsetXZ = Vector2.zero;

    [Tooltip("Controls whether the shadow uses the authored raised quad position or ray-projects onto the ground surface below the hit center.")]
    [SerializeField] private GroundShadowProjectionMode projectionMode = GroundShadowProjectionMode.RaisedQuad;

    [Tooltip("Maximum downward distance in meters used when Projection Mode is Project Onto Ground. If no ground is found within this distance, the raised quad fallback is used.")]
    [SerializeField] private float projectionMaxDistance = 4f;

    [Tooltip("When enabled, the ground footprint renders health and shield rings around the hit-box shadow. Disable this to keep only the shadow while preserving the same hit-footprint center.")]
    [SerializeField] private bool healthRingsEnabled = true;

    [Tooltip("World-space gap between the shadow outer edge and the inner edge of the first fillable ring.")]
    [SerializeField] private float ringDistanceFromShadow = 0.05f;

    [Tooltip("World-space radial thickness of each fillable ring drawn around the enemy shadow.")]
    [SerializeField] private float spatialUiRingThickness = 0.08f;

    [Tooltip("World-space gap between the health ring and the shield ring when both are drawn.")]
    [SerializeField] private float spatialUiRingSpacing = 0.03f;

    [Tooltip("Angular width in degrees used by health and shield tracks. Use 360 for full rings, or a smaller value to render only a camera-facing arc.")]
    [SerializeField] private float ringArcDegrees = DefaultRingArcDegrees;
    #endregion

    #region Shadow Appearance
    [Tooltip("Tint applied to the hit-box shadow disc. Alpha controls overall shadow strength on top of Shadow Alpha.")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("Final opacity multiplier applied to the shadow disc on top of the shadow color alpha.")]
    [SerializeField] private float shadowAlpha = 1f;

    [Tooltip("Normalized falloff width applied at the outer edge of the shadow disc. Higher values produce softer rims.")]
    [SerializeField] private float shadowEdgeSoftness = 0.08f;
    #endregion

    #region Ring Appearance
    [Tooltip("Fill color of the health ring when the enemy is at full health. Alpha controls ring opacity.")]
    [SerializeField] private Color healthRingFillColor = new Color(0.92f, 0.18f, 0.16f, 0.95f);

    [Tooltip("Background color of the health ring track shown behind the depleting fill. Alpha controls track opacity.")]
    [SerializeField] private Color healthRingBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 0.7f);

    [Tooltip("Final opacity multiplier applied to the health ring background on top of its color alpha.")]
    [SerializeField] private float healthRingBackgroundAlpha = 1f;

    [Tooltip("Fill color of the shield ring when the enemy is at full shield. Alpha controls ring opacity.")]
    [SerializeField] private Color shieldRingFillColor = new Color(0.25f, 0.85f, 1f, 0.95f);

    [Tooltip("Background color of the shield ring track shown behind the depleting fill. Alpha controls track opacity.")]
    [SerializeField] private Color shieldRingBackgroundColor = new Color(0.04f, 0.04f, 0.04f, 0.7f);

    [Tooltip("Final opacity multiplier applied to the shield ring background on top of its color alpha.")]
    [SerializeField] private float shieldRingBackgroundAlpha = 1f;

    [Tooltip("Normalized radial falloff applied at the inner and outer edges of each ring band. Higher values produce softer ring borders.")]
    [SerializeField] private float ringEdgeSoftness = 0.05f;

    [Tooltip("Angular falloff in radians applied at the depleting edge of each ring fill. Higher values smooth the edge as the ring drains.")]
    [SerializeField] private float ringAngularSoftness = 0.02f;
    #endregion

    #region Ring Orientation
    [Tooltip("When enabled, the fillable arcs stop tracking the active camera and stay anchored to a fixed world-space direction defined by Locked Ring World Angle. Useful for top-down cameras where camera-facing rotation is undesired.")]
    [SerializeField] private bool lockRingsToWorld;

    [Tooltip("World-space anchor direction for the depleting fill when Lock Rings To World is enabled, expressed as a degree offset from world forward (+Z) rotating clockwise around +Y. 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.")]
    [SerializeField] private float lockedRingsWorldAngleDegrees;
    #endregion

    #endregion

    #endregion

    #region Properties
    public EnemyShadowCoverageMode ShadowCoverageMode
    {
        get
        {
            return shadowCoverageMode;
        }
    }

    public float SpatialUiRingThickness
    {
        get
        {
            return spatialUiRingThickness;
        }
    }

    public float SpatialUiRingSpacing
    {
        get
        {
            return spatialUiRingSpacing;
        }
    }

    public float SpatialUiHeightOffset
    {
        get
        {
            return spatialUiHeightOffset;
        }
    }

    public bool HealthRingsEnabled
    {
        get
        {
            return healthRingsEnabled;
        }
    }

    public Vector2 PositionOffsetXZ
    {
        get
        {
            return positionOffsetXZ;
        }
    }

    public GroundShadowProjectionMode ProjectionMode
    {
        get
        {
            return projectionMode;
        }
    }

    public float ProjectionMaxDistance
    {
        get
        {
            return projectionMaxDistance;
        }
    }

    public float RingDistanceFromShadow
    {
        get
        {
            return ringDistanceFromShadow;
        }
    }

    public float RingArcDegrees
    {
        get
        {
            return ringArcDegrees;
        }
    }

    public Color ShadowColor
    {
        get
        {
            return shadowColor;
        }
    }

    public float ShadowAlpha
    {
        get
        {
            return shadowAlpha;
        }
    }

    public float ShadowEdgeSoftness
    {
        get
        {
            return shadowEdgeSoftness;
        }
    }

    public Color HealthRingFillColor
    {
        get
        {
            return healthRingFillColor;
        }
    }

    public Color HealthRingBackgroundColor
    {
        get
        {
            return healthRingBackgroundColor;
        }
    }

    public float HealthRingBackgroundAlpha
    {
        get
        {
            return healthRingBackgroundAlpha;
        }
    }

    public Color ShieldRingFillColor
    {
        get
        {
            return shieldRingFillColor;
        }
    }

    public Color ShieldRingBackgroundColor
    {
        get
        {
            return shieldRingBackgroundColor;
        }
    }

    public float ShieldRingBackgroundAlpha
    {
        get
        {
            return shieldRingBackgroundAlpha;
        }
    }

    public float RingEdgeSoftness
    {
        get
        {
            return ringEdgeSoftness;
        }
    }

    public float RingAngularSoftness
    {
        get
        {
            return ringAngularSoftness;
        }
    }

    public bool LockRingsToWorld
    {
        get
        {
            return lockRingsToWorld;
        }
    }

    public float LockedRingsWorldAngleDegrees
    {
        get
        {
            return lockedRingsWorldAngleDegrees;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Keeps the validation hook available while authored warnings and bake fallbacks handle invalid values without mutating presets.
    /// Authored ranges are surfaced as HelpBox warnings in the management tool; runtime bake clamps to safe defaults.
    /// </summary>
    public void Validate()
    {
        // Intentionally a no-op. The management tool reports authored inconsistencies, while the baker clamps runtime data.
    }
    #endregion

    #endregion
}
