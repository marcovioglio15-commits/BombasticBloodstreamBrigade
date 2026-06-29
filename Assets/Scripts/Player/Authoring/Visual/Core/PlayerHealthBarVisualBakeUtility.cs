using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds safe ECS health-bar visual configurations from Player Visual Preset authoring data.
/// </summary>
public static class PlayerHealthBarVisualBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime player health-bar visual configuration from the currently scaled visual preset.
    /// </summary>
    /// <param name="visualPreset">Scaled visual preset currently used by the baker.</param>
    /// <returns>Safe runtime player health-bar visual configuration.</returns>
    public static PlayerHealthBarVisualConfig BuildConfig(PlayerVisualPreset visualPreset)
    {
        PlayerHealthBarsVisualSettings settings = visualPreset != null && visualPreset.HealthBars != null
            ? visualPreset.HealthBars
            : new PlayerHealthBarsVisualSettings();
        return BuildConfig(settings);
    }

    /// <summary>
    /// Builds the runtime syringe visual configuration from a resolved settings block.
    /// </summary>
    /// <param name="settings">Scaled syringe settings used by the caller-specific HUD view.</param>
    /// <returns>Safe runtime syringe visual configuration.</returns>
    public static PlayerHealthBarVisualConfig BuildConfig(PlayerHealthBarsVisualSettings settings)
    {
        PlayerHealthBarsVisualSettings resolvedSettings = settings ?? new PlayerHealthBarsVisualSettings();
        PlayerSyringePaintDripSettings paintDrips = resolvedSettings.PaintDrips ?? new PlayerSyringePaintDripSettings();

        return new PlayerHealthBarVisualConfig
        {
            Health = BuildChannel(resolvedSettings.Health),
            Shield = BuildChannel(resolvedSettings.Shield),
            Experience = BuildChannel(resolvedSettings.Experience),
            ExperienceShape = BuildShapeConfig(resolvedSettings.ExperienceShape),
            FontAsset = resolvedSettings.FontAsset,
            LabelOffset = new float2(resolvedSettings.LabelOffset.x, resolvedSettings.LabelOffset.y),
            VerticalSpacing = ResolveFinite(resolvedSettings.VerticalSpacing, 12f),
            UnitsPerMajorDivision = math.max(0.0001f, ResolveFinite(resolvedSettings.UnitsPerMajorDivision, 1f)),
            PixelsPerMajorDivision = math.max(0.0001f, ResolveFinite(resolvedSettings.PixelsPerMajorDivision, 52f)),
            MinimumLength = math.max(1f, ResolveFinite(resolvedSettings.MinimumLength, 340f)),
            MaximumLength = math.max(1f, ResolveFinite(resolvedSettings.MaximumLength, 760f)),
            LabelMinimumSpacing = math.max(1f, ResolveFinite(resolvedSettings.LabelMinimumSpacing, 46f)),
            GraduationEndPadding = math.max(0f, ResolveFinite(resolvedSettings.GraduationEndPadding, 0f)),
            LabelFontSize = math.max(1f, ResolveFinite(resolvedSettings.LabelFontSize, 15f)),
            LabelOutlineWidth = math.clamp(ResolveFinite(resolvedSettings.LabelOutlineWidth, 0.12f), 0f, 1f),
            GraduationVerticalOffset = math.clamp(ResolveFinite(resolvedSettings.GraduationVerticalOffset, 0f), -0.5f, 0.5f),
            BarHeight = math.max(1f, ResolveFinite(resolvedSettings.BarHeight, 88f)),
            OutlineThickness = math.max(0f, ResolveFinite(resolvedSettings.OutlineThickness, 0.035f)),
            ChamberInset = math.clamp(ResolveFinite(resolvedSettings.ChamberInset, 0.16f), 0f, 0.49f),
            PlungerWidth = math.max(0f, ResolveFinite(resolvedSettings.PlungerWidth, 0.032f)),
            EndCapWidth = math.max(0f, ResolveFinite(resolvedSettings.EndCapWidth, 36f)),
            TerminationOffset = math.max(0f, ResolveFinite(resolvedSettings.TerminationOffset, 8f)),
            MinorDivisionsPerMajor = math.max(1, resolvedSettings.MinorDivisionsPerMajor),
            LabelEveryMajorDivision = math.max(1, resolvedSettings.LabelEveryMajorDivision),
            MaximumLabelCount = math.clamp(resolvedSettings.MaximumLabelCount, 2, PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
            UniformLabelCount = math.clamp(resolvedSettings.UniformLabelCount, 0, PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
            PaintDrips = BuildPaintDrips(paintDrips),
            BodyStyle = ResolveBodyStyle(resolvedSettings.BodyStyle),
            LabelPlacement = ResolveLabelPlacement(resolvedSettings.LabelPlacement),
            GraduationMode = ResolveGraduationMode(resolvedSettings.GraduationMode),
            TerminationStyle = ResolveTerminationStyle(resolvedSettings.TerminationStyle),
            Enabled = resolvedSettings.Enabled ? (byte)1 : (byte)0,
            HideWhenPlayerMissing = resolvedSettings.HideWhenPlayerMissing ? (byte)1 : (byte)0,
            ClampPlungerStartInsideBody = resolvedSettings.ClampPlungerStartInsideBody ? (byte)1 : (byte)0,
            ClampPlungerEndInsideBody = resolvedSettings.ClampPlungerEndInsideBody ? (byte)1 : (byte)0,
            StopLiquidAtPlunger = resolvedSettings.StopLiquidAtPlunger ? (byte)1 : (byte)0,
            TerminationEnabled = resolvedSettings.TerminationEnabled ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds the immutable player health-bar visual baseline from the unscaled visual preset.
    /// </summary>
    /// <param name="visualPreset">Unscaled source visual preset.</param>
    /// <returns>Immutable player health-bar visual baseline.</returns>
    public static PlayerBaseHealthBarVisualConfig BuildBaseConfig(PlayerVisualPreset visualPreset)
    {
        return new PlayerBaseHealthBarVisualConfig
        {
            Config = BuildConfig(visualPreset)
        };
    }

    /// <summary>
    /// Builds one runtime direct palette from a built-in authoring preset.
    /// </summary>
    /// <param name="preset">Built-in palette profile selected by the visual preset or scaling formula.</param>
    /// <returns>Runtime direct palette matching the selected profile.</returns>
    public static PlayerSyringePaletteConfig BuildPalette(PlayerSyringePalettePreset preset)
    {
        return BuildPalette(new PlayerSyringePaletteSettings(preset));
    }

    /// <summary>
    /// Builds one runtime syringe-shape configuration from authored silhouette, graduation, and label settings.
    /// </summary>
    /// <param name="settings">Authored syringe-shape settings.</param>
    /// <returns>Safe runtime syringe-shape configuration.</returns>
    public static PlayerSyringeShapeConfig BuildShapeConfig(PlayerSyringeShapeSettings settings)
    {
        PlayerSyringeShapeSettings resolvedSettings = settings ?? PlayerSyringeShapeSettings.CreateExperienceDefaults();
        PlayerSyringePaintDripSettings paintDrips = resolvedSettings.PaintDrips ?? new PlayerSyringePaintDripSettings();

        return new PlayerSyringeShapeConfig
        {
            LabelOffset = new float2(resolvedSettings.LabelOffset.x, resolvedSettings.LabelOffset.y),
            UnitsPerMajorDivision = math.max(0.0001f, ResolveFinite(resolvedSettings.UnitsPerMajorDivision, 1f)),
            PixelsPerMajorDivision = math.max(0.0001f, ResolveFinite(resolvedSettings.PixelsPerMajorDivision, 52f)),
            MinimumLength = math.max(1f, ResolveFinite(resolvedSettings.MinimumLength, 340f)),
            MaximumLength = math.max(1f, ResolveFinite(resolvedSettings.MaximumLength, 760f)),
            LabelMinimumSpacing = math.max(1f, ResolveFinite(resolvedSettings.LabelMinimumSpacing, 46f)),
            GraduationEndPadding = math.max(0f, ResolveFinite(resolvedSettings.GraduationEndPadding, 0f)),
            LabelFontSize = math.max(1f, ResolveFinite(resolvedSettings.LabelFontSize, 15f)),
            LabelOutlineWidth = math.clamp(ResolveFinite(resolvedSettings.LabelOutlineWidth, 0.12f), 0f, 1f),
            GraduationVerticalOffset = math.clamp(ResolveFinite(resolvedSettings.GraduationVerticalOffset, 0f), -0.5f, 0.5f),
            BarHeight = math.max(1f, ResolveFinite(resolvedSettings.BarHeight, 88f)),
            OutlineThickness = math.max(0f, ResolveFinite(resolvedSettings.OutlineThickness, 0.035f)),
            ChamberInset = math.clamp(ResolveFinite(resolvedSettings.ChamberInset, 0.16f), 0f, 0.49f),
            PlungerWidth = math.max(0f, ResolveFinite(resolvedSettings.PlungerWidth, 0.032f)),
            EndCapWidth = math.max(0f, ResolveFinite(resolvedSettings.EndCapWidth, 36f)),
            TerminationOffset = math.max(0f, ResolveFinite(resolvedSettings.TerminationOffset, 8f)),
            MinorDivisionsPerMajor = math.max(1, resolvedSettings.MinorDivisionsPerMajor),
            LabelEveryMajorDivision = math.max(1, resolvedSettings.LabelEveryMajorDivision),
            MaximumLabelCount = math.clamp(resolvedSettings.MaximumLabelCount, 2, PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
            UniformLabelCount = math.clamp(resolvedSettings.UniformLabelCount, 0, PlayerHealthBarsVisualSettings.AuthoredLabelPoolCapacity),
            PaintDrips = BuildPaintDrips(paintDrips),
            BodyStyle = ResolveBodyStyle(resolvedSettings.BodyStyle),
            LabelPlacement = ResolveLabelPlacement(resolvedSettings.LabelPlacement),
            GraduationMode = ResolveGraduationMode(resolvedSettings.GraduationMode),
            TerminationStyle = ResolveTerminationStyle(resolvedSettings.TerminationStyle),
            ClampPlungerStartInsideBody = resolvedSettings.ClampPlungerStartInsideBody ? (byte)1 : (byte)0,
            ClampPlungerEndInsideBody = resolvedSettings.ClampPlungerEndInsideBody ? (byte)1 : (byte)0,
            StopLiquidAtPlunger = resolvedSettings.StopLiquidAtPlunger ? (byte)1 : (byte)0,
            TerminationEnabled = resolvedSettings.TerminationEnabled ? (byte)1 : (byte)0
        };
    }
    #endregion

    #region Construction
    /// <summary>
    /// Builds one safe runtime syringe-channel configuration.
    /// </summary>
    /// <param name="settings">Authored channel settings.</param>
    /// <returns>Safe runtime syringe-channel configuration.</returns>
    private static PlayerSyringeChannelConfig BuildChannel(PlayerSyringeChannelSettings settings)
    {
        PlayerSyringeChannelSettings resolvedSettings = settings ?? new PlayerSyringeChannelSettings();
        PlayerSyringePaletteSettings palette = resolvedSettings.Palette ?? new PlayerSyringePaletteSettings();
        PlayerSyringeFluidSettings fluid = resolvedSettings.Fluid ?? new PlayerSyringeFluidSettings();
        PlayerSyringeMotionSettings motion = resolvedSettings.Motion ?? new PlayerSyringeMotionSettings();
        PlayerSyringeOutlineStyleSettings outlineStyle = resolvedSettings.OutlineStyle ?? new PlayerSyringeOutlineStyleSettings();

        return new PlayerSyringeChannelConfig
        {
            Palette = BuildPalette(palette),
            Fluid = new PlayerSyringeFluidConfig
            {
                FlowSpeed = ResolveFinite(fluid.FlowSpeed, 0.45f),
                WaveAmplitude = math.max(0f, ResolveFinite(fluid.WaveAmplitude, 0.035f)),
                WaveFrequency = math.max(0f, ResolveFinite(fluid.WaveFrequency, 3f)),
                Viscosity = math.clamp(ResolveFinite(fluid.Viscosity, 0.72f), 0f, 1f),
                BubbleDensity = math.clamp(ResolveFinite(fluid.BubbleDensity, 0.35f), 0f, 1f),
                BubbleMinimumSize = math.max(0f, ResolveFinite(fluid.BubbleMinimumSize, 0.025f)),
                BubbleMaximumSize = math.max(0f, ResolveFinite(fluid.BubbleMaximumSize, 0.07f)),
                BubbleRiseSpeed = ResolveFinite(fluid.BubbleRiseSpeed, 0.18f),
                BubbleDrift = ResolveFinite(fluid.BubbleDrift, 0.12f),
                FlowEnabled = fluid.FlowEnabled ? (byte)1 : (byte)0,
                BubblesEnabled = fluid.BubblesEnabled ? (byte)1 : (byte)0
            },
            Motion = new PlayerSyringeMotionConfig
            {
                SloshStrength = ResolveFinite(motion.SloshStrength, 0.08f),
                SurfaceSloshStrength = math.max(0f, ResolveFinite(motion.SurfaceSloshStrength, 0.45f)),
                HorizontalSloshStrength = math.max(0f, ResolveFinite(motion.HorizontalSloshStrength, 0.16f)),
                SloshSpring = math.max(0f, ResolveFinite(motion.SloshSpring, 18f)),
                SloshDamping = math.max(0f, ResolveFinite(motion.SloshDamping, 8f)),
                MaximumSlosh = math.max(0f, ResolveFinite(motion.MaximumSlosh, 0.25f)),
                MaximumTiltDegrees = math.max(0f, ResolveFinite(motion.MaximumTiltDegrees, 2.5f)),
                TiltSpring = math.max(0f, ResolveFinite(motion.TiltSpring, 16f)),
                TiltDamping = math.max(0f, ResolveFinite(motion.TiltDamping, 8f)),
                ValueImpulseStrength = ResolveFinite(motion.ValueImpulseStrength, 0.65f),
                ValueImpulseDecay = math.max(0f, ResolveFinite(motion.ValueImpulseDecay, 7f)),
                MovementReactionEnabled = motion.MovementReactionEnabled ? (byte)1 : (byte)0,
                HorizontalSloshEnabled = motion.HorizontalSloshEnabled ? (byte)1 : (byte)0,
                TiltEnabled = motion.TiltEnabled ? (byte)1 : (byte)0,
                ValueImpulseEnabled = motion.ValueImpulseEnabled ? (byte)1 : (byte)0
            },
            OutlineStyle = new PlayerSyringeOutlineStyleConfig
            {
                EdgeWobbleStrength = math.saturate(ResolveFinite(outlineStyle.EdgeWobbleStrength, 0.35f)),
                EdgeWobbleFrequency = math.clamp(ResolveFinite(outlineStyle.EdgeWobbleFrequency, 16f), 1f, 64f),
                InnerStreakStrength = math.saturate(ResolveFinite(outlineStyle.InnerStreakStrength, 0.25f)),
                InnerStreakDensity = math.saturate(ResolveFinite(outlineStyle.InnerStreakDensity, 0.3f)),
                InnerStreakLength = math.clamp(ResolveFinite(outlineStyle.InnerStreakLength, 0.16f), 0f, 0.5f),
                Enabled = outlineStyle.Enabled ? (byte)1 : (byte)0
            },
            SmoothingSeconds = math.max(0f, ResolveFinite(resolvedSettings.SmoothingSeconds, 0.08f)),
            Enabled = resolvedSettings.Enabled ? (byte)1 : (byte)0,
            HideWhenMaximumUnavailable = resolvedSettings.HideWhenMaximumUnavailable ? (byte)1 : (byte)0,
            SloshAffectsBubblesOnly = resolvedSettings.SloshAffectsBubblesOnly ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds one runtime direct palette from authored direct color settings.
    /// </summary>
    /// <param name="palette">Authored direct color settings.</param>
    /// <returns>Runtime direct palette matching the authored colors.</returns>
    private static PlayerSyringePaletteConfig BuildPalette(PlayerSyringePaletteSettings palette)
    {
        return new PlayerSyringePaletteConfig
        {
            Outline = ToFloat4(palette.Outline),
            Body = ToFloat4(palette.Body),
            BodyShadow = ToFloat4(palette.BodyShadow),
            Chamber = ToFloat4(palette.Chamber),
            Liquid = ToFloat4(palette.Liquid),
            LiquidHighlight = ToFloat4(palette.LiquidHighlight),
            Bubbles = ToFloat4(palette.Bubbles),
            Graduation = ToFloat4(palette.Graduation),
            Label = ToFloat4(palette.Label),
            LabelOutline = ToFloat4(palette.LabelOutline),
            Plunger = ToFloat4(palette.Plunger),
            PlungerWindow = ToFloat4(palette.PlungerWindow),
            TerminationOutline = ToFloat4(palette.TerminationOutline),
            TerminationInterior = ToFloat4(palette.TerminationInterior)
        };
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds safe runtime paint-drip settings for a procedural syringe shape.
    /// </summary>
    /// <param name="paintDrips">Authored paint-drip settings.</param>
    /// <returns>Safe runtime paint-drip settings.</returns>
    private static PlayerSyringePaintDripConfig BuildPaintDrips(PlayerSyringePaintDripSettings paintDrips)
    {
        PlayerSyringePaintDripSettings resolvedPaintDrips = paintDrips ?? new PlayerSyringePaintDripSettings();

        return new PlayerSyringePaintDripConfig
        {
            Density = math.saturate(ResolveFinite(resolvedPaintDrips.Density, 0.38f)),
            Length = math.clamp(ResolveFinite(resolvedPaintDrips.Length, 0.1f), 0f, 0.5f),
            Width = math.clamp(ResolveFinite(resolvedPaintDrips.Width, 0.018f), 0.0001f, 0.25f),
            Irregularity = math.saturate(ResolveFinite(resolvedPaintDrips.Irregularity, 0.65f)),
            Enabled = resolvedPaintDrips.Enabled ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Resolves one authored body style to a supported runtime value.
    /// </summary>
    /// <param name="value">Authored body style.</param>
    /// <returns>Supported runtime body style.</returns>
    private static PlayerSyringeBodyStyle ResolveBodyStyle(PlayerSyringeBodyStyle value)
    {
        switch (value)
        {
            case PlayerSyringeBodyStyle.SimplePaintedContainer:
            case PlayerSyringeBodyStyle.DetailedSyringe:
                return value;
            default:
                return PlayerSyringeBodyStyle.SimplePaintedContainer;
        }
    }

    /// <summary>
    /// Resolves one authored label placement to a supported runtime value.
    /// </summary>
    /// <param name="value">Authored label placement.</param>
    /// <returns>Supported runtime label placement.</returns>
    private static PlayerSyringeLabelPlacement ResolveLabelPlacement(PlayerSyringeLabelPlacement value)
    {
        switch (value)
        {
            case PlayerSyringeLabelPlacement.InsideChamber:
            case PlayerSyringeLabelPlacement.GraduationPlate:
                return value;
            default:
                return PlayerSyringeLabelPlacement.InsideChamber;
        }
    }

    /// <summary>
    /// Resolves one authored graduation mode to a supported runtime distribution.
    /// </summary>
    /// <param name="value">Authored graduation mode.</param>
    /// <returns>Supported runtime graduation mode.</returns>
    private static PlayerSyringeGraduationMode ResolveGraduationMode(PlayerSyringeGraduationMode value)
    {
        switch (value)
        {
            case PlayerSyringeGraduationMode.FixedUnits:
            case PlayerSyringeGraduationMode.UniformLabels:
            case PlayerSyringeGraduationMode.Hidden:
                return value;
            default:
                return PlayerSyringeGraduationMode.FixedUnits;
        }
    }

    /// <summary>
    /// Resolves one authored termination style to a supported runtime value.
    /// </summary>
    /// <param name="value">Authored termination style.</param>
    /// <returns>Supported runtime termination style.</returns>
    private static PlayerSyringeTerminationStyle ResolveTerminationStyle(PlayerSyringeTerminationStyle value)
    {
        switch (value)
        {
            case PlayerSyringeTerminationStyle.Flat:
            case PlayerSyringeTerminationStyle.Angular:
            case PlayerSyringeTerminationStyle.Rounded:
            case PlayerSyringeTerminationStyle.Needle:
                return value;
            default:
                return PlayerSyringeTerminationStyle.Angular;
        }
    }

    /// <summary>
    /// Converts one Unity color into an unmanaged shader color.
    /// </summary>
    /// <param name="color">Authored Unity color.</param>
    /// <returns>Equivalent unmanaged RGBA value.</returns>
    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    /// <summary>
    /// Replaces non-finite authoring data only at the bake boundary.
    /// </summary>
    /// <param name="value">Authored value.</param>
    /// <param name="fallback">Safe fallback used for non-finite values.</param>
    /// <returns>Finite value safe for ECS runtime use.</returns>
    private static float ResolveFinite(float value, float fallback)
    {
        return math.isfinite(value) ? value : fallback;
    }
    #endregion

    #endregion
}
