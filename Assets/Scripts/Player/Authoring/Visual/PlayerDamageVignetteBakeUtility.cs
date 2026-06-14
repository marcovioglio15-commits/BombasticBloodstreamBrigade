using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the baked damage vignette config and the matching initial state from the resolved (already scaled) <see cref="PlayerVisualPreset"/>.
/// Invoked by <see cref="PlayerAuthoringBaker"/> during the visual feedback bake pass so the runtime presentation system finds ready-to-use data on the player entity.
/// </summary>
public static class PlayerDamageVignetteBakeUtility
{
    #region Constants
    private const float MinimumDurationSeconds = 0f;
    private const float MaximumDurationSeconds = 5f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the damage vignette config from the resolved visual preset.
    /// Returns false when no preset is available so the baker can skip both components and avoid useless archetype churn.
    /// </summary>
    /// <param name="visualPreset">Resolved visual preset, already scaled when Add Scaling is enabled.</param>
    /// <param name="config">Built ECS config populated from authored values.</param>
    /// <returns>True when the preset provides damage vignette settings, otherwise false.</returns>
    public static bool TryBuildConfig(PlayerVisualPreset visualPreset, out PlayerDamageVignetteConfig config)
    {
        config = default;

        if (visualPreset == null)
            return false;

        PlayerVisualDamageVignetteSettings shieldSettings = visualPreset.ShieldDamageVignette;
        PlayerVisualDamageVignetteSettings healthSettings = visualPreset.HealthDamageVignette;

        // Both blocks are guaranteed by OnValidate but we stay defensive: missing blocks contribute neutral data.
        config = new PlayerDamageVignetteConfig
        {
            ShieldSprite = ResolveSprite(shieldSettings),
            ShieldTint = ResolveTint(shieldSettings),
            ShieldMaxAlpha = ResolveMaxAlpha(shieldSettings),
            ShieldFadeInSeconds = ResolveFadeInSeconds(shieldSettings),
            ShieldFadeOutSeconds = ResolveFadeOutSeconds(shieldSettings),

            HealthSprite = ResolveSprite(healthSettings),
            HealthTint = ResolveTint(healthSettings),
            HealthMaxAlpha = ResolveMaxAlpha(healthSettings),
            HealthFadeInSeconds = ResolveFadeInSeconds(healthSettings),
            HealthFadeOutSeconds = ResolveFadeOutSeconds(healthSettings)
        };
        return true;
    }

    /// <summary>
    /// Builds the initial vignette state used the first time the runtime presentation system observes the player entity.
    /// Previous health and shield snapshots stay zeroed until the presentation system seeds them on the first update.
    /// </summary>
    /// <returns>Default-initialized state suitable to add alongside the config.</returns>
    public static PlayerDamageVignetteState BuildInitialState()
    {
        return new PlayerDamageVignetteState
        {
            PreviousHealth = 0f,
            PreviousShield = 0f,
            Initialized = 0,
            ActiveChannel = PlayerDamageVignetteChannel.None,
            ActivePhase = PlayerDamageVignettePhase.Idle,
            ActiveElapsedSeconds = 0f,
            ActiveAlpha = 0f,
            ActiveTriggerPulseId = 0
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the authored sprite reference into a baked managed-asset reference. Null sprites yield the default UnityObjectRef.
    /// </summary>
    /// <param name="settings">Authored channel settings block.</param>
    /// <returns>UnityObjectRef wrapping the authored sprite or the default reference.</returns>
    private static UnityObjectRef<Sprite> ResolveSprite(PlayerVisualDamageVignetteSettings settings)
    {
        if (settings == null || settings.Sprite == null)
            return default;

        return settings.Sprite;
    }

    /// <summary>
    /// Resolves the authored tint into a linear float4 reusable from URP-friendly UI paths.
    /// Alpha is forced to one because vignette opacity is driven by Max Alpha and the fade durations.
    /// </summary>
    /// <param name="settings">Authored channel settings block.</param>
    /// <returns>Linear-space tint with alpha forced to one.</returns>
    private static float4 ResolveTint(PlayerVisualDamageVignetteSettings settings)
    {
        if (settings == null)
            return new float4(1f, 1f, 1f, 1f);

        Color tint = settings.Tint;
        tint.a = 1f;
        return DamageFlashRuntimeUtility.ToLinearFloat4(tint);
    }

    /// <summary>
    /// Resolves the authored peak alpha clamped to the 0..1 range so a misauthored value cannot push the overlay above opaque.
    /// </summary>
    /// <param name="settings">Authored channel settings block.</param>
    /// <returns>Peak alpha in the [0..1] range.</returns>
    private static float ResolveMaxAlpha(PlayerVisualDamageVignetteSettings settings)
    {
        if (settings == null)
            return 0f;

        return math.saturate(settings.MaxAlpha);
    }

    /// <summary>
    /// Resolves the authored fade-in duration clamped against the supported authoring envelope.
    /// </summary>
    /// <param name="settings">Authored channel settings block.</param>
    /// <returns>Fade-in seconds in the [0..MaximumDurationSeconds] range.</returns>
    private static float ResolveFadeInSeconds(PlayerVisualDamageVignetteSettings settings)
    {
        if (settings == null)
            return 0f;

        return math.clamp(settings.FadeInSeconds, MinimumDurationSeconds, MaximumDurationSeconds);
    }

    /// <summary>
    /// Resolves the authored fade-out duration clamped against the supported authoring envelope.
    /// </summary>
    /// <param name="settings">Authored channel settings block.</param>
    /// <returns>Fade-out seconds in the [0..MaximumDurationSeconds] range.</returns>
    private static float ResolveFadeOutSeconds(PlayerVisualDamageVignetteSettings settings)
    {
        if (settings == null)
            return 0f;

        return math.clamp(settings.FadeOutSeconds, MinimumDurationSeconds, MaximumDurationSeconds);
    }
    #endregion

    #endregion
}
