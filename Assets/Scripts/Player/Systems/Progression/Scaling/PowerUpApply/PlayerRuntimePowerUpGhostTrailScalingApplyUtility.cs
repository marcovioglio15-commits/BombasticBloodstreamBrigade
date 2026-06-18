using System;
using Unity.Mathematics;

/// <summary>
/// Applies runtime Add Scaling values to Ghost Trail capture settings and its reusable screen-feedback profile.
/// </summary>
internal static class PlayerRuntimePowerUpGhostTrailScalingApplyUtility
{
    #region Constants
    private const string ScreenFeedbackPrefix = "ghostTrail.screenFeedback.";
    private const string ImpactFramePrefix = "impactFrame.";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one numeric or enum-like Add Scaling result to a Ghost Trail runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula result already evaluated against scalable-stat runtime values.</param>
    /// <param name="config">Mutable Ghost Trail config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted a Ghost Trail field.</returns>
    public static bool TryApplyValue(string payloadPath, float resolvedValue, ref GhostTrailPowerUpConfig config)
    {
        switch (payloadPath)
        {
            case "ghostTrail.durationSeconds":
                config.DurationSeconds = math.max(0f, resolvedValue);
                return true;
            case "ghostTrail.easeInUnscaledSeconds":
                config.EaseInUnscaledSeconds = math.max(0f, resolvedValue);
                return true;
            case "ghostTrail.easeOutUnscaledSeconds":
                config.EaseOutUnscaledSeconds = math.max(0f, resolvedValue);
                return true;
            case "ghostTrail.easingMode":
                config.EasingMode = PlayerRuntimeScalingEnumUtility.ResolveImpactFrameEasingMode(resolvedValue);
                return true;
            case "ghostTrail.emissionIntervalSeconds":
                config.EmissionIntervalSeconds = math.max(0.0001f, resolvedValue);
                return true;
            case "ghostTrail.snapshotLifetimeSeconds":
                config.SnapshotLifetimeSeconds = math.max(0.0001f, resolvedValue);
                return true;
            case "ghostTrail.captureScope":
                config.CaptureScope = PlayerRuntimeScalingEnumUtility.ResolveGhostTrailCaptureScope(resolvedValue);
                return true;
            case "ghostTrail.movementDistanceThreshold":
                config.MovementDistanceThreshold = math.max(0f, resolvedValue);
                return true;
            case "ghostTrail.rotationAngleThresholdDegrees":
                config.RotationAngleThresholdDegrees = math.max(0f, resolvedValue);
                return true;
            case "ghostTrail.maximumActiveSnapshots":
                config.MaximumActiveSnapshots = math.max(1, (int)resolvedValue);
                return true;
            case "ghostTrail.tint.x":
                config.TintRgba.x = math.saturate(resolvedValue);
                return true;
            case "ghostTrail.tint.y":
                config.TintRgba.y = math.saturate(resolvedValue);
                return true;
            case "ghostTrail.tint.z":
                config.TintRgba.z = math.saturate(resolvedValue);
                return true;
            case "ghostTrail.tint.w":
                config.TintRgba.w = math.saturate(resolvedValue);
                return true;
        }

        if (!payloadPath.StartsWith(ScreenFeedbackPrefix, StringComparison.Ordinal))
            return false;

        ImpactFramePowerUpConfig effectContainer = new ImpactFramePowerUpConfig
        {
            Effect = config.ScreenFeedback
        };
        string normalizedPath = ImpactFramePrefix + payloadPath.Substring(ScreenFeedbackPrefix.Length);
        bool applied = PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyValue(normalizedPath,
                                                                                        resolvedValue,
                                                                                        ref effectContainer);
        config.ScreenFeedback = effectContainer.Effect;
        return applied;
    }

    /// <summary>
    /// Applies one boolean Add Scaling result to a Ghost Trail runtime config field.
    /// </summary>
    /// <param name="payloadPath">Modular payload path extracted from the scaling rule stat key.</param>
    /// <param name="resolvedValue">Formula boolean result.</param>
    /// <param name="config">Mutable Ghost Trail config rebuilt from immutable baselines.</param>
    /// <returns>True when the payload path targeted a Ghost Trail boolean field.</returns>
    public static bool TryApplyBooleanValue(string payloadPath, bool resolvedValue, ref GhostTrailPowerUpConfig config)
    {
        switch (payloadPath)
        {
            case "ghostTrail.matchToggleActivationDuration":
                config.MatchToggleActivationDuration = resolvedValue ? (byte)1 : (byte)0;
                return true;
            case "ghostTrail.screenFeedbackEnabled":
                config.ScreenFeedbackEnabled = resolvedValue ? (byte)1 : (byte)0;
                return true;
        }

        if (!payloadPath.StartsWith(ScreenFeedbackPrefix, StringComparison.Ordinal))
            return false;

        ImpactFramePowerUpConfig effectContainer = new ImpactFramePowerUpConfig
        {
            Effect = config.ScreenFeedback
        };
        string normalizedPath = ImpactFramePrefix + payloadPath.Substring(ScreenFeedbackPrefix.Length);
        bool applied = PlayerRuntimePowerUpImpactFrameScalingApplyUtility.TryApplyBooleanValue(normalizedPath,
                                                                                               resolvedValue,
                                                                                               ref effectContainer);
        config.ScreenFeedback = effectContainer.Effect;
        return applied;
    }
    #endregion

    #endregion
}
