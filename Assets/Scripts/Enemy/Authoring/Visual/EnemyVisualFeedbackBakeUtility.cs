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

    /// <summary>
    /// Builds runtime-safe face flipbook config from authored visual preset settings.
    /// </summary>
    /// <param name="settings">Authored face flipbook settings, or null when the feature is unavailable.</param>
    /// <returns>Runtime-safe face flipbook config.</returns>
    public static EnemyFaceFlipbookConfig BuildFaceFlipbookConfig(EnemyVisualFaceFlipbookSettings settings)
    {
        if (settings == null)
            return default;

        EnemyFaceFlipbookStateSettings idle = settings.Idle;
        EnemyFaceFlipbookStateSettings attack = settings.Attack;
        EnemyFaceFlipbookStateSettings damage = settings.Damage;

        return new EnemyFaceFlipbookConfig
        {
            Enabled = settings.Enabled ? (byte)1 : (byte)0,
            IdleEnabled = IsStateEnabled(idle) ? (byte)1 : (byte)0,
            AttackEnabled = IsStateEnabled(attack) ? (byte)1 : (byte)0,
            DamageEnabled = IsStateEnabled(damage) ? (byte)1 : (byte)0,
            IdleGrid = BuildFaceGrid(idle, 4, 2, 8),
            AttackGrid = BuildFaceGrid(attack, 4, 1, 4),
            DamageGrid = BuildFaceGrid(damage, 4, 1, 4),
            IdleFramesPerSecond = ResolvePositiveFinite(idle != null ? idle.FramesPerSecond : 8f, 8f),
            AttackFramesPerSecond = ResolvePositiveFinite(attack != null ? attack.FramesPerSecond : 10f, 10f),
            DamageFramesPerSecond = ResolvePositiveFinite(damage != null ? damage.FramesPerSecond : 12f, 12f),
            IdleStartFrame = ResolveNonNegativeFinite(idle != null ? idle.StartFrame : 0f),
            AttackStartFrame = ResolveNonNegativeFinite(attack != null ? attack.StartFrame : 0f),
            DamageStartFrame = ResolveNonNegativeFinite(damage != null ? damage.StartFrame : 0f),
            AttackDurationSeconds = ResolvePositiveFinite(attack != null ? attack.DurationSeconds : 0.18f, 0.18f),
            DamageDurationSeconds = ResolvePositiveFinite(damage != null ? damage.DurationSeconds : 0.14f, 0.14f),
            IdleAtlas = idle != null ? idle.Atlas : null,
            AttackAtlas = attack != null ? attack.Atlas : null,
            DamageAtlas = damage != null ? damage.Atlas : null
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
    /// Checks whether one face state is present and enabled.
    /// </summary>
    /// <param name="settings">State settings to inspect.</param>
    /// <returns>True when the state can be used at runtime.</returns>
    private static bool IsStateEnabled(EnemyFaceFlipbookStateSettings settings)
    {
        return settings != null && settings.Enabled;
    }

    /// <summary>
    /// Builds a shader grid vector with safe fallback values.
    /// </summary>
    /// <param name="settings">State settings supplying authored grid values.</param>
    /// <param name="defaultColumns">Fallback column count.</param>
    /// <param name="defaultRows">Fallback row count.</param>
    /// <param name="defaultFrameCount">Fallback frame count.</param>
    /// <returns>Grid vector storing columns, rows, frame count and reserved data.</returns>
    private static float4 BuildFaceGrid(EnemyFaceFlipbookStateSettings settings,
                                        int defaultColumns,
                                        int defaultRows,
                                        int defaultFrameCount)
    {
        int columns = ResolvePositiveInt(settings != null ? settings.Columns : defaultColumns, defaultColumns);
        int rows = ResolvePositiveInt(settings != null ? settings.Rows : defaultRows, defaultRows);
        int availableFrames = math.max(1, columns * rows);
        int frameCount = ResolvePositiveInt(settings != null ? settings.FrameCount : defaultFrameCount, defaultFrameCount);
        return new float4(columns, rows, math.clamp(frameCount, 1, availableFrames), 0f);
    }

    /// <summary>
    /// Resolves a positive integer fallback for invalid authored values.
    /// </summary>
    /// <param name="value">Authored integer value.</param>
    /// <param name="fallback">Fallback used when the value is not positive.</param>
    /// <returns>Positive integer suitable for ECS runtime.</returns>
    private static int ResolvePositiveInt(int value, int fallback)
    {
        if (value > 0)
            return value;

        return math.max(1, fallback);
    }

    /// <summary>
    /// Resolves a finite positive float fallback for runtime config fields.
    /// </summary>
    /// <param name="value">Authored float value.</param>
    /// <param name="fallback">Fallback used when the value is invalid.</param>
    /// <returns>Positive finite float suitable for ECS runtime.</returns>
    private static float ResolvePositiveFinite(float value, float fallback)
    {
        float resolvedValue = ResolveFinite(value, fallback);

        if (resolvedValue > 0f)
            return resolvedValue;

        return math.max(MinimumDurationSeconds, fallback);
    }

    /// <summary>
    /// Resolves a finite zero-or-positive float for runtime config fields.
    /// </summary>
    /// <param name="value">Authored float value.</param>
    /// <returns>Zero-or-positive finite value suitable for ECS runtime.</returns>
    private static float ResolveNonNegativeFinite(float value)
    {
        float resolvedValue = ResolveFinite(value, 0f);
        return math.max(0f, resolvedValue);
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
