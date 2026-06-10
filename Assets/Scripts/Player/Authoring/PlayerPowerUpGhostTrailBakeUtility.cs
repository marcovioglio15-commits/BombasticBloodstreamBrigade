using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts scalable Ghost Trail authoring data into compact ECS runtime configuration.
/// </summary>
internal static class PlayerPowerUpGhostTrailBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one Ghost Trail runtime configuration without mutating authored values.
    /// </summary>
    /// <param name="data">Resolved Ghost Trail module payload.</param>
    /// <param name="config">Sanitized runtime configuration.</param>
    /// <returns>True when a payload exists.</returns>
    public static bool TryBuildConfig(PowerUpGhostTrailModuleData data, out GhostTrailPowerUpConfig config)
    {
        config = default;

        if (data == null)
            return false;

        Vector4 tint = data.Tint;
        config = new GhostTrailPowerUpConfig
        {
            DurationSeconds = math.max(0f, data.DurationSeconds),
            MatchToggleActivationDuration = data.MatchToggleActivationDuration ? (byte)1 : (byte)0,
            EaseInUnscaledSeconds = math.max(0f, data.EaseInUnscaledSeconds),
            EaseOutUnscaledSeconds = math.max(0f, data.EaseOutUnscaledSeconds),
            EasingMode = data.EasingMode,
            EmissionIntervalSeconds = math.max(0.01f, data.EmissionIntervalSeconds),
            SnapshotLifetimeSeconds = math.max(0.01f, data.SnapshotLifetimeSeconds),
            CaptureScope = data.CaptureScope,
            MovementDistanceThreshold = math.max(0f, data.MovementDistanceThreshold),
            RotationAngleThresholdDegrees = math.max(0f, data.RotationAngleThresholdDegrees),
            MaximumActiveSnapshots = math.max(1, data.MaximumActiveSnapshots),
            TintRgba = math.saturate(new float4(tint.x, tint.y, tint.z, tint.w)),
            ScreenFeedbackEnabled = data.ScreenFeedbackEnabled ? (byte)1 : (byte)0,
            ScreenFeedback = PlayerPowerUpImpactFrameBakeUtility.BuildEffectConfig(data.ScreenFeedback)
        };
        return true;
    }
    #endregion

    #endregion
}
