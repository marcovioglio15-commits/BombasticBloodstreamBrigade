using System;
using UnityEngine;

/// <summary>
/// Selects the procedural body silhouette used by both player syringe channels.
/// </summary>
public enum PlayerSyringeBodyStyle : byte
{
    SimplePaintedContainer = 0,
    DetailedSyringe = 1
}

/// <summary>
/// Selects where graduation ticks and numeric labels are presented.
/// </summary>
public enum PlayerSyringeLabelPlacement : byte
{
    InsideChamber = 0,
    GraduationPlate = 1
}

/// <summary>
/// Selects the procedural shape used by both syringe end caps.
/// </summary>
public enum PlayerSyringeTerminationStyle : byte
{
    Flat = 0,
    Angular = 1,
    Rounded = 2,
    Needle = 3
}

/// <summary>
/// Stores optional procedural paint-drip settings shared by both syringe channels.
/// </summary>
[Serializable]
public sealed class PlayerSyringePaintDripSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables procedural paint-like drips extending from the syringe body borders.")]
    [SerializeField] private bool enabled;

    [Tooltip("Normalized probability that each procedural border cell produces a paint drip.")]
    [Range(0f, 1f)]
    [SerializeField] private float density = 0.38f;

    [Tooltip("Maximum normalized length reached by procedural paint drips.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float length = 0.1f;

    [Tooltip("Reference-length normalized horizontal width of each procedural paint drip. Runtime compensation preserves its pixel footprint across short and long syringes.")]
    [Range(0.0001f, 0.25f)]
    [SerializeField] private float width = 0.018f;

    [Tooltip("Controls deterministic variation between neighboring paint-drip lengths and widths.")]
    [Range(0f, 1f)]
    [SerializeField] private float irregularity = 0.65f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public float Density => density;
    public float Length => length;
    public float Width => width;
    public float Irregularity => irregularity;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid paint-drip authoring values without mutating serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    public void Validate(string ownerAssetName)
    {
        if (!enabled)
            return;

        if (!IsFinite(density) || density < 0f || density > 1f)
            LogWarning(ownerAssetName, "Paint Drip Density should be finite and within 0-1.");

        if (!IsFinite(length) || length < 0f || length > 0.5f)
            LogWarning(ownerAssetName, "Paint Drip Length should be finite and within 0-0.5.");

        if (!IsFinite(width) || width <= 0f || width > 0.25f)
            LogWarning(ownerAssetName, "Paint Drip Width should be finite, greater than zero, and no higher than 0.25.");

        if (!IsFinite(irregularity) || irregularity < 0f || irregularity > 1f)
            LogWarning(ownerAssetName, "Paint Drip Irregularity should be finite and within 0-1.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one paint-drip-specific preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Health Bars/Paint Drips: {1}",
                                       ownerAssetName,
                                       message));
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
