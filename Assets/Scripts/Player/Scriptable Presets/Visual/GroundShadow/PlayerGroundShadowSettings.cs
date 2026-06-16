using System;
using UnityEngine;

/// <summary>
/// Stores player hit-box shadow settings rendered in world space without health or shield rings.
/// </summary>
[Serializable]
public sealed class PlayerGroundShadowSettings
{
    #region Constants
    public const float DefaultHitAreaMultiplier = 1f;
    public const float DefaultProjectionMaxDistance = 4f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("When enabled, the player renders a world-space shadow that identifies the authored hit-box footprint.")]
    [SerializeField]
    private bool enabled = true;

    [Tooltip("Multiplier applied to the player's automatic hit-area radius (the circle inside which enemy fire can hit the player) to size the shadow disc. Use 1 to match the hit area exactly.")]
    [SerializeField]
    private float hitAreaMultiplier = DefaultHitAreaMultiplier;

    [Tooltip("Local root-space XZ offset applied from the player pivot to the represented hit-box center.")]
    [SerializeField]
    private Vector2 positionOffsetXZ = Vector2.zero;

    [Tooltip("Vertical clearance applied above the raised quad or projected ground point to avoid z-fighting.")]
    [SerializeField]
    private float heightOffset = 0.035f;

    [Header("Projection")]
    [Tooltip("Controls whether the player shadow uses a raised quad or ray-projects onto the ground surface below the hit-box center.")]
    [SerializeField]
    private GroundShadowProjectionMode projectionMode = GroundShadowProjectionMode.RaisedQuad;

    [Tooltip("Maximum downward distance in meters used when Projection Mode is Project Onto Ground. If no ground is found within this distance, the raised quad fallback is used.")]
    [SerializeField]
    private float projectionMaxDistance = DefaultProjectionMaxDistance;

    [Header("Appearance")]
    [Tooltip("Tint applied to the player hit-box shadow disc. Alpha controls overall shadow strength on top of Shadow Alpha.")]
    [SerializeField]
    private Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

    [Tooltip("Final opacity multiplier applied to the player shadow disc on top of Shadow Color alpha.")]
    [SerializeField]
    private float shadowAlpha = 1f;

    [Tooltip("Normalized falloff width applied at the outer edge of the player shadow disc.")]
    [SerializeField]
    private float shadowEdgeSoftness = 0.08f;
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

    public float HitAreaMultiplier
    {
        get
        {
            return hitAreaMultiplier;
        }
    }

    public Vector2 PositionOffsetXZ
    {
        get
        {
            return positionOffsetXZ;
        }
    }

    public float HeightOffset
    {
        get
        {
            return heightOffset;
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
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid authored values without mutating the visual preset.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used in warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!IsFinite(hitAreaMultiplier) || hitAreaMultiplier <= 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Hit Area Multiplier should be finite and greater than zero.", ownerAssetName));

        if (!IsFinite(positionOffsetXZ))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Position Offset XZ contains an invalid numeric value.", ownerAssetName));

        if (!IsFinite(heightOffset))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Height Offset should be finite.", ownerAssetName));

        if (projectionMode != GroundShadowProjectionMode.RaisedQuad &&
            projectionMode != GroundShadowProjectionMode.ProjectOntoGround)
        {
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Projection Mode has an unsupported value.", ownerAssetName));
        }

        if (!IsFinite(projectionMaxDistance) || projectionMaxDistance < 0f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Projection Max Distance should be finite and zero or positive.", ownerAssetName));

        if (!IsFinite(shadowColor))
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Shadow Color contains an invalid numeric value.", ownerAssetName));

        if (!IsFinite(shadowAlpha) || shadowAlpha < 0f || shadowAlpha > 1f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Shadow Alpha should be finite and between 0 and 1.", ownerAssetName));

        if (!IsFinite(shadowEdgeSoftness) || shadowEdgeSoftness < 0f || shadowEdgeSoftness > 1f)
            Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Ground Shadow: Shadow Edge Softness should be finite and between 0 and 1.", ownerAssetName));
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether every vector component is finite.
    /// </summary>
    /// <param name="value">Vector value to inspect.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color value to inspect.</param>
    /// <returns>True when every channel is finite.</returns>
    private static bool IsFinite(Color value)
    {
        return IsFinite(value.r) &&
               IsFinite(value.g) &&
               IsFinite(value.b) &&
               IsFinite(value.a);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
