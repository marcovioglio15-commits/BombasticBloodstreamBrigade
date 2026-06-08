using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds runtime-safe enemy visual feedback configs without mutating authored preset values.
/// </summary>
public static class EnemyVisualFeedbackBakeUtility
{
    #region Constants
    private const float MinimumDurationSeconds = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one runtime-safe death puddle config from authored settings and the already sampled enemy palette.
    /// </summary>
    /// <param name="settings">Authored death puddle settings, or null when the feature is unavailable.</param>
    /// <param name="prefabEntity">Resolved ECS puddle prefab entity.</param>
    /// <param name="palette">Bake-time enemy visual palette shared with death debris.</param>
    /// <returns>Runtime-safe death puddle config.</returns>
    public static EnemyDeathPuddleConfig BuildDeathPuddleConfig(EnemyVisualDeathPuddleSettings settings,
                                                                 Entity prefabEntity,
                                                                 in EnemyDeathDebrisColorPalette palette)
    {
        if (settings == null)
            return default;

        Vector2 fixedWorldSize = settings.FixedWorldSize;

        return new EnemyDeathPuddleConfig
        {
            PrefabEntity = prefabEntity,
            LifetimeSeconds = math.clamp(ResolveFinite(settings.LifetimeSeconds, 4f), 0.1f, 30f),
            StableFraction = math.clamp(ResolveFinite(settings.StableFraction, 0.2f), 0f, 0.95f),
            FinalScaleRatio = math.saturate(ResolveFinite(settings.FinalScaleRatio, 0.08f)),
            FootprintScaleMultiplier = math.clamp(ResolveFinite(settings.FootprintScaleMultiplier, 1.1f), 0.1f, 4f),
            FixedWorldSize = new float2(ResolvePositive(fixedWorldSize.x, 1f),
                                        ResolvePositive(fixedWorldSize.y, 1f)),
            RandomSizeVariation = math.clamp(ResolveFinite(settings.RandomSizeVariation, 0.12f), 0f, 0.75f),
            GroundOffset = math.clamp(ResolveFinite(settings.GroundOffset, 0.012f), -0.1f, 0.5f),
            EdgeIrregularity = math.saturate(ResolveFinite(settings.EdgeIrregularity, 0.28f)),
            BorderWidth = math.clamp(ResolveFinite(settings.BorderWidth, 0.1f), 0f, 0.5f),
            EdgeFeather = math.clamp(ResolveFinite(settings.EdgeFeather, 0.04f), 0.001f, 0.5f),
            SecondaryPaletteBlend = math.saturate(ResolveFinite(settings.SecondaryPaletteBlend, 0.55f)),
            FlowSpeed = math.clamp(ResolveFinite(settings.FlowSpeed, 0.35f), 0f, 3f),
            Viscosity = math.saturate(ResolveFinite(settings.Viscosity, 0.7f)),
            SurfaceDistortion = math.clamp(ResolveFinite(settings.SurfaceDistortion, 0.08f), 0f, 0.35f),
            HighlightStrength = math.saturate(ResolveFinite(settings.HighlightStrength, 0.18f)),
            PrimaryColor = palette.PrimaryColor,
            SecondaryColor = palette.SecondaryColor,
            EvaporationCurve = ResolveEvaporationCurve(settings.EvaporationCurve),
            SizeMode = ResolveSizeMode(settings.SizeMode),
            Enabled = settings.Enabled && prefabEntity != Entity.Null ? (byte)1 : (byte)0,
            RandomRotation = settings.RandomRotation ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds one runtime-safe elastic hit config from authored visual preset settings.
    /// </summary>
    /// <param name="settings">Authored elastic hit settings, or null when the feature is unavailable.</param>
    /// <returns>Runtime-safe elastic hit config.</returns>
    public static EnemyElasticHitConfig BuildElasticHitConfig(EnemyVisualElasticHitSettings settings)
    {
        if (settings == null)
            return default;

        return new EnemyElasticHitConfig
        {
            DurationSeconds = math.clamp(ResolveFinite(settings.DurationSeconds, 0.16f), 0.02f, 1f),
            MaximumCompression = math.clamp(ResolveFinite(settings.MaximumCompression, 0.18f), 0f, 0.75f),
            VolumeCompensation = math.saturate(ResolveFinite(settings.VolumeCompensation, 0.65f)),
            OscillationCount = math.clamp(ResolveFinite(settings.OscillationCount, 1.25f), 0.1f, 5f),
            Damping = math.clamp(ResolveFinite(settings.Damping, 5.5f), 0f, 20f),
            Directionality = math.saturate(ResolveFinite(settings.Directionality, 0.85f)),
            MinimumRetriggerInterval = math.clamp(ResolveFinite(settings.MinimumRetriggerInterval, 0.035f), 0f, 0.5f),
            TriggerMode = ResolveTriggerMode(settings.TriggerMode),
            RetriggerMode = ResolveRetriggerMode(settings.RetriggerMode),
            Enabled = settings.Enabled ? (byte)1 : (byte)0,
            AnchorToGround = settings.AnchorToGround ? (byte)1 : (byte)0
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns a finite authored value or a conservative fallback.
    /// </summary>
    /// <param name="value">Authored numeric value.</param>
    /// <param name="fallback">Fallback used for NaN or Infinity.</param>
    /// <returns>Finite value suitable for runtime ECS.</returns>
    private static float ResolveFinite(float value, float fallback)
    {
        return math.isfinite(value) ? value : fallback;
    }

    /// <summary>
    /// Returns a positive finite authored value or a conservative fallback.
    /// </summary>
    /// <param name="value">Authored numeric value.</param>
    /// <param name="fallback">Fallback used when the value is invalid or non-positive.</param>
    /// <returns>Positive finite value suitable for runtime ECS.</returns>
    private static float ResolvePositive(float value, float fallback)
    {
        float resolvedValue = ResolveFinite(value, fallback);
        return resolvedValue > 0f ? resolvedValue : math.max(MinimumDurationSeconds, fallback);
    }

    /// <summary>
    /// Resolves supported death puddle size modes.
    /// </summary>
    /// <param name="sizeMode">Authored enum value.</param>
    /// <returns>Runtime-supported size mode.</returns>
    private static EnemyDeathPuddleSizeMode ResolveSizeMode(EnemyDeathPuddleSizeMode sizeMode)
    {
        switch (sizeMode)
        {
            case EnemyDeathPuddleSizeMode.EnemyFootprint:
            case EnemyDeathPuddleSizeMode.FixedWorldSize:
                return sizeMode;

            default:
                return EnemyDeathPuddleSizeMode.EnemyFootprint;
        }
    }

    /// <summary>
    /// Resolves supported death puddle evaporation curves.
    /// </summary>
    /// <param name="curve">Authored enum value.</param>
    /// <returns>Runtime-supported evaporation curve.</returns>
    private static EnemyDeathPuddleEvaporationCurve ResolveEvaporationCurve(EnemyDeathPuddleEvaporationCurve curve)
    {
        switch (curve)
        {
            case EnemyDeathPuddleEvaporationCurve.SmoothStep:
            case EnemyDeathPuddleEvaporationCurve.Linear:
            case EnemyDeathPuddleEvaporationCurve.EaseIn:
                return curve;

            default:
                return EnemyDeathPuddleEvaporationCurve.SmoothStep;
        }
    }

    /// <summary>
    /// Resolves supported elastic hit trigger modes.
    /// </summary>
    /// <param name="triggerMode">Authored enum value.</param>
    /// <returns>Runtime-supported trigger mode.</returns>
    private static EnemyElasticHitTriggerMode ResolveTriggerMode(EnemyElasticHitTriggerMode triggerMode)
    {
        switch (triggerMode)
        {
            case EnemyElasticHitTriggerMode.DirectImpactsOnly:
            case EnemyElasticHitTriggerMode.AllNonLethalDamage:
                return triggerMode;

            default:
                return EnemyElasticHitTriggerMode.DirectImpactsOnly;
        }
    }

    /// <summary>
    /// Resolves supported elastic hit retrigger modes.
    /// </summary>
    /// <param name="retriggerMode">Authored enum value.</param>
    /// <returns>Runtime-supported retrigger mode.</returns>
    private static EnemyElasticHitRetriggerMode ResolveRetriggerMode(EnemyElasticHitRetriggerMode retriggerMode)
    {
        switch (retriggerMode)
        {
            case EnemyElasticHitRetriggerMode.StrongestWins:
            case EnemyElasticHitRetriggerMode.Restart:
                return retriggerMode;

            default:
                return EnemyElasticHitRetriggerMode.StrongestWins;
        }
    }
    #endregion

    #endregion
}
