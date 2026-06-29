using System;
using UnityEngine;

/// <summary>
/// Stores optional stylized outline and internal paint-streak settings for one syringe channel.
/// </summary>
[Serializable]
public sealed class PlayerSyringeOutlineStyleSettings
{
    #region Serialized Fields
    [Tooltip("Enables non-uniform painted outline variation and optional internal streaks for this syringe channel.")]
    [SerializeField] private bool enabled;

    [Tooltip("Normalized strength of deterministic edge wobble applied to outline and frame masks.")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeWobbleStrength = 0.35f;

    [Tooltip("Number of deterministic edge-wobble cells sampled along the syringe length.")]
    [Range(1f, 64f)]
    [SerializeField] private float edgeWobbleFrequency = 16f;

    [Tooltip("Normalized opacity of thin internal painted streaks blended inside the chamber and liquid.")]
    [Range(0f, 1f)]
    [SerializeField] private float innerStreakStrength = 0.25f;

    [Tooltip("Approximate normalized density of internal painted streak columns.")]
    [Range(0f, 1f)]
    [SerializeField] private float innerStreakDensity = 0.3f;

    [Tooltip("Maximum normalized vertical length of internal paint streaks descending from the chamber top.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float innerStreakLength = 0.16f;
    #endregion

    #region Properties
    public bool Enabled => enabled;
    public float EdgeWobbleStrength => edgeWobbleStrength;
    public float EdgeWobbleFrequency => edgeWobbleFrequency;
    public float InnerStreakStrength => innerStreakStrength;
    public float InnerStreakDensity => innerStreakDensity;
    public float InnerStreakLength => innerStreakLength;
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Reports invalid stylized-outline values without mutating serialized data.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name used by warning messages.</param>
    /// <param name="channelLabel">User-facing channel label used by warning messages.</param>
    public void Validate(string ownerAssetName, string channelLabel)
    {
        if (!enabled)
            return;

        if (!IsFinite(edgeWobbleStrength) || edgeWobbleStrength < 0f || edgeWobbleStrength > 1f)
            LogWarning(ownerAssetName, channelLabel, "Edge Wobble Strength should be finite and within 0-1.");

        if (!IsFinite(edgeWobbleFrequency) || edgeWobbleFrequency < 1f || edgeWobbleFrequency > 64f)
            LogWarning(ownerAssetName, channelLabel, "Edge Wobble Frequency should be finite and within 1-64.");

        if (!IsFinite(innerStreakStrength) || innerStreakStrength < 0f || innerStreakStrength > 1f)
            LogWarning(ownerAssetName, channelLabel, "Inner Streak Strength should be finite and within 0-1.");

        if (!IsFinite(innerStreakDensity) || innerStreakDensity < 0f || innerStreakDensity > 1f)
            LogWarning(ownerAssetName, channelLabel, "Inner Streak Density should be finite and within 0-1.");

        if (!IsFinite(innerStreakLength) || innerStreakLength < 0f || innerStreakLength > 0.5f)
            LogWarning(ownerAssetName, channelLabel, "Inner Streak Length should be finite and within 0-0.5.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes one stylized-outline preset warning.
    /// </summary>
    /// <param name="ownerAssetName">Visual preset asset name.</param>
    /// <param name="channelLabel">User-facing channel label.</param>
    /// <param name="message">Warning message.</param>
    private static void LogWarning(string ownerAssetName, string channelLabel, string message)
    {
        Debug.LogWarning(string.Format("[PlayerVisualPreset] '{0}' - Health Bars/{1}/Painted Outline: {2}",
                                       ownerAssetName,
                                       channelLabel,
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
