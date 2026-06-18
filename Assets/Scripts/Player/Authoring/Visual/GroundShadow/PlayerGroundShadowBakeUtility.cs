using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds ECS-authoritative player ground-shadow configurations from Player Visual Preset authoring data.
/// </summary>
public static class PlayerGroundShadowBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime player ground-shadow configuration from the currently scaled visual preset.
    /// </summary>
    /// <param name="visualPreset">Scaled visual preset currently used by the baker.</param>
    /// <returns>Safe runtime player ground-shadow configuration.</returns>
    public static PlayerGroundShadowConfig BuildConfig(PlayerVisualPreset visualPreset)
    {
        PlayerGroundShadowSettings settings = visualPreset != null && visualPreset.GroundShadow != null
            ? visualPreset.GroundShadow
            : new PlayerGroundShadowSettings();
        Vector2 positionOffset = settings.PositionOffsetXZ;

        return new PlayerGroundShadowConfig
        {
            HitAreaMultiplier = math.max(PlayerHitAreaUtility.MinimumShadowMultiplier,
                                         ResolveFinite(settings.HitAreaMultiplier, PlayerGroundShadowSettings.DefaultHitAreaMultiplier)),
            PositionOffsetXZ = new float2(ResolveFinite(positionOffset.x, 0f), ResolveFinite(positionOffset.y, 0f)),
            HeightOffset = ResolveFinite(settings.HeightOffset, 0f),
            ProjectionMode = ResolveProjectionMode(settings.ProjectionMode),
            ProjectionMaxDistance = math.max(0f, ResolveFinite(settings.ProjectionMaxDistance, PlayerGroundShadowSettings.DefaultProjectionMaxDistance)),
            ShadowColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.ShadowColor),
            ShadowAlpha = math.saturate(ResolveFinite(settings.ShadowAlpha, 1f)),
            ShadowEdgeSoftness = math.saturate(ResolveFinite(settings.ShadowEdgeSoftness, 0.08f)),
            Enabled = settings.Enabled ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds the immutable player ground-shadow baseline from the unscaled visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Immutable player ground-shadow baseline.</returns>
    public static PlayerBaseGroundShadowConfig BuildBaseConfig(PlayerVisualPreset visualPreset)
    {
        return new PlayerBaseGroundShadowConfig
        {
            Config = BuildConfig(visualPreset)
        };
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves an authored projection mode to a supported runtime mode.
    /// </summary>
    /// <param name="projectionMode">Authored projection mode.</param>
    /// <returns>Supported runtime projection mode.</returns>
    private static GroundShadowProjectionMode ResolveProjectionMode(GroundShadowProjectionMode projectionMode)
    {
        switch (projectionMode)
        {
            case GroundShadowProjectionMode.RaisedQuad:
            case GroundShadowProjectionMode.ProjectOntoGround:
                return projectionMode;
            default:
                return GroundShadowProjectionMode.RaisedQuad;
        }
    }

    /// <summary>
    /// Replaces non-finite authoring data only at the bake boundary.
    /// </summary>
    /// <param name="value">Authored value.</param>
    /// <param name="fallback">Safe fallback used for non-finite values.</param>
    /// <returns>Finite value safe for ECS runtime use.</returns>
    private static float ResolveFinite(float value, float fallback)
    {
        if (math.isfinite(value))
            return value;

        return fallback;
    }
    #endregion

    #endregion
}
