using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds scalable player health-bar visual settings only when the unified runtime scaling hash changes.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerRuntimeHealthBarVisualScalingSystem : ISystem
{
    #region Types
    private enum SyringeChannelTarget : byte
    {
        Health = 0,
        Shield = 1,
        Experience = 2
    }
    #endregion

    #region Fields
    private static readonly Dictionary<string, PlayerFormulaValue> VariableContext = new Dictionary<string, PlayerFormulaValue>(64, StringComparer.OrdinalIgnoreCase);
    private static readonly List<PlayerScalableStatElement> EffectiveScalableStats = new List<PlayerScalableStatElement>(64);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required to rebuild scalable player health-bar visual settings.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerHealthBarVisualOwner>();
        state.RequireForUpdate<PlayerHealthBarVisualScalingState>();
        state.RequireForUpdate<PlayerBaseHealthBarVisualConfig>();
        state.RequireForUpdate<PlayerHealthBarVisualConfig>();
        state.RequireForUpdate<PlayerRuntimeHealthBarVisualScalingElement>();
    }

    /// <summary>
    /// Restores the immutable baseline and applies all health-bar visual formulas when scalable stats change.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(true);
        BufferLookup<PlayerRuntimeHealthBarVisualScalingElement> scalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeHealthBarVisualScalingElement>(true);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(true);
        ComponentLookup<PlayerRuntimeComboCounterConfig> comboConfigLookup = SystemAPI.GetComponentLookup<PlayerRuntimeComboCounterConfig>(true);
        ComponentLookup<PlayerComboCounterState> comboStateLookup = SystemAPI.GetComponentLookup<PlayerComboCounterState>(true);
        BufferLookup<PlayerRuntimeComboRankElement> comboRanksLookup = SystemAPI.GetBufferLookup<PlayerRuntimeComboRankElement>(true);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);

        foreach ((RefRO<PlayerHealthBarVisualOwner> owner,
                  RefRW<PlayerHealthBarVisualScalingState> visualScalingState,
                  RefRO<PlayerBaseHealthBarVisualConfig> baseConfig,
                  RefRW<PlayerHealthBarVisualConfig> runtimeConfig,
                  Entity configEntity)
                 in SystemAPI.Query<RefRO<PlayerHealthBarVisualOwner>,
                                    RefRW<PlayerHealthBarVisualScalingState>,
                                    RefRO<PlayerBaseHealthBarVisualConfig>,
                                    RefRW<PlayerHealthBarVisualConfig>>()
                             .WithAll<PlayerRuntimeHealthBarVisualScalingElement>()
                             .WithEntityAccess())
        {
            Entity playerEntity = owner.ValueRO.PlayerEntity;

            if (!runtimeScalingStateLookup.HasComponent(playerEntity))
                continue;

            PlayerRuntimeScalingState runtimeScalingState = runtimeScalingStateLookup[playerEntity];

            if (runtimeScalingState.Initialized == 0)
                continue;

            if (visualScalingState.ValueRO.Initialized != 0 &&
                visualScalingState.ValueRO.LastScalableStatsHash == runtimeScalingState.LastScalableStatsHash)
            {
                continue;
            }

            runtimeConfig.ValueRW = baseConfig.ValueRO.Config;
            PlayerRuntimeScalingFormulaContextUtility.Fill(playerEntity,
                                                           in scalableStatsLookup,
                                                           in comboConfigLookup,
                                                           in comboStateLookup,
                                                           in comboRanksLookup,
                                                           in characterTuningLookup,
                                                           EffectiveScalableStats,
                                                           VariableContext);
            ApplyScaling(scalingLookup[configEntity], ref runtimeConfig.ValueRW, VariableContext);
            visualScalingState.ValueRW.Initialized = 1;
            visualScalingState.ValueRW.LastScalableStatsHash = runtimeScalingState.LastScalableStatsHash;
        }
    }
    #endregion

    #region Scaling
    /// <summary>
    /// Applies all health-bar visual formulas to a freshly restored runtime configuration.
    /// </summary>
    /// <param name="scalingBuffer">Runtime scaling metadata.</param>
    /// <param name="runtimeConfig">Mutable runtime player health-bar visual configuration.</param>
    /// <param name="variableContext">Resolved formula variables for the current player entity.</param>
    public static void ApplyScaling(DynamicBuffer<PlayerRuntimeHealthBarVisualScalingElement> scalingBuffer,
                                    ref PlayerHealthBarVisualConfig runtimeConfig,
                                    Dictionary<string, PlayerFormulaValue> variableContext)
    {
        for (int scalingIndex = 0; scalingIndex < scalingBuffer.Length; scalingIndex++)
        {
            PlayerRuntimeHealthBarVisualScalingElement scalingElement = scalingBuffer[scalingIndex];
            ApplyScalingElement(scalingElement, ref runtimeConfig, variableContext);
        }
    }

    /// <summary>
    /// Applies one health-bar visual formula to a mutable syringe configuration.
    /// </summary>
    /// <param name="scalingElement">Runtime scaling metadata element.</param>
    /// <param name="runtimeConfig">Mutable runtime player health-bar visual configuration.</param>
    /// <param name="variableContext">Resolved formula variables for the current player entity.</param>
    public static void ApplyScalingElement(PlayerRuntimeHealthBarVisualScalingElement scalingElement,
                                           ref PlayerHealthBarVisualConfig runtimeConfig,
                                           Dictionary<string, PlayerFormulaValue> variableContext)
    {
        string payloadPath = scalingElement.PayloadPath.ToString();

        switch ((PlayerFormulaValueType)scalingElement.ValueType)
        {
            case PlayerFormulaValueType.Boolean:
                if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    ApplyBooleanValue(payloadPath, resolvedBoolean, ref runtimeConfig);
                }
                break;
            case PlayerFormulaValueType.Number:
                if (PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseValue,
                                                                                          scalingElement.IsInteger != 0,
                                                                                          variableContext,
                                                                                          out float resolvedValue))
                {
                    ApplyNumericValue(payloadPath, resolvedValue, ref runtimeConfig);
                }
                break;
        }
    }

    /// <summary>
    /// Applies one boolean formula result to a shared or channel-specific health-bar visual toggle.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to healthBars.</param>
    /// <param name="resolvedValue">Resolved boolean result.</param>
    /// <param name="runtimeConfig">Mutable runtime player health-bar visual configuration.</param>
    private static void ApplyBooleanValue(string payloadPath,
                                          bool resolvedValue,
                                          ref PlayerHealthBarVisualConfig runtimeConfig)
    {
        byte byteValue = resolvedValue ? (byte)1 : (byte)0;

        switch (payloadPath)
        {
            case "enabled":
                runtimeConfig.Enabled = byteValue;
                return;
            case "hideWhenPlayerMissing":
                runtimeConfig.HideWhenPlayerMissing = byteValue;
                return;
            case "clampPlungerStartInsideBody":
                runtimeConfig.ClampPlungerStartInsideBody = byteValue;
                return;
            case "clampPlungerEndInsideBody":
                runtimeConfig.ClampPlungerEndInsideBody = byteValue;
                return;
            case "stopLiquidAtPlunger":
                runtimeConfig.StopLiquidAtPlunger = byteValue;
                return;
            case "terminationEnabled":
                runtimeConfig.TerminationEnabled = byteValue;
                return;
            case "paintDrips.enabled":
                runtimeConfig.PaintDrips.Enabled = byteValue;
                return;
        }

        if (TryResolveChannelPath(payloadPath, out SyringeChannelTarget target, out string channelPath))
        {
            ApplyTargetChannelBooleanValue(channelPath, byteValue, target, ref runtimeConfig);
            return;
        }

        if (payloadPath.StartsWith("experienceShape.", StringComparison.Ordinal))
            ApplyShapeBooleanValue(payloadPath.Substring("experienceShape.".Length), byteValue, ref runtimeConfig.ExperienceShape);
    }

    /// <summary>
    /// Applies one numeric formula result to a shared or channel-specific health-bar visual field.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to healthBars.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="runtimeConfig">Mutable runtime player health-bar visual configuration.</param>
    private static void ApplyNumericValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerHealthBarVisualConfig runtimeConfig)
    {
        switch (payloadPath)
        {
            case "verticalSpacing":
                runtimeConfig.VerticalSpacing = resolvedValue;
                return;
            case "unitsPerMajorDivision":
                runtimeConfig.UnitsPerMajorDivision = resolvedValue;
                return;
            case "pixelsPerMajorDivision":
                runtimeConfig.PixelsPerMajorDivision = resolvedValue;
                return;
            case "minimumLength":
                runtimeConfig.MinimumLength = resolvedValue;
                return;
            case "maximumLength":
                runtimeConfig.MaximumLength = resolvedValue;
                return;
            case "labelMinimumSpacing":
                runtimeConfig.LabelMinimumSpacing = resolvedValue;
                return;
            case "graduationEndPadding":
                runtimeConfig.GraduationEndPadding = resolvedValue;
                return;
            case "minorDivisionsPerMajor":
                runtimeConfig.MinorDivisionsPerMajor = (int)math.round(resolvedValue);
                return;
            case "labelEveryMajorDivision":
                runtimeConfig.LabelEveryMajorDivision = (int)math.round(resolvedValue);
                return;
            case "maximumLabelCount":
                runtimeConfig.MaximumLabelCount = (int)math.round(resolvedValue);
                return;
            case "uniformLabelCount":
                runtimeConfig.UniformLabelCount = (int)math.round(resolvedValue);
                return;
            case "labelFontSize":
                runtimeConfig.LabelFontSize = resolvedValue;
                return;
            case "labelOutlineWidth":
                runtimeConfig.LabelOutlineWidth = resolvedValue;
                return;
            case "graduationVerticalOffset":
                runtimeConfig.GraduationVerticalOffset = resolvedValue;
                return;
            case "labelOffset.x":
                runtimeConfig.LabelOffset.x = resolvedValue;
                return;
            case "labelOffset.y":
                runtimeConfig.LabelOffset.y = resolvedValue;
                return;
            case "barHeight":
                runtimeConfig.BarHeight = resolvedValue;
                return;
            case "outlineThickness":
                runtimeConfig.OutlineThickness = resolvedValue;
                return;
            case "chamberInset":
                runtimeConfig.ChamberInset = resolvedValue;
                return;
            case "plungerWidth":
                runtimeConfig.PlungerWidth = resolvedValue;
                return;
            case "endCapWidth":
                runtimeConfig.EndCapWidth = resolvedValue;
                return;
            case "terminationOffset":
                runtimeConfig.TerminationOffset = resolvedValue;
                return;
            case "bodyStyle":
                runtimeConfig.BodyStyle = (PlayerSyringeBodyStyle)math.clamp((int)math.round(resolvedValue),
                                                                            (int)PlayerSyringeBodyStyle.SimplePaintedContainer,
                                                                            (int)PlayerSyringeBodyStyle.DetailedSyringe);
                return;
            case "labelPlacement":
                runtimeConfig.LabelPlacement = (PlayerSyringeLabelPlacement)math.clamp((int)math.round(resolvedValue),
                                                                                       (int)PlayerSyringeLabelPlacement.InsideChamber,
                                                                                       (int)PlayerSyringeLabelPlacement.GraduationPlate);
                return;
            case "graduationMode":
                runtimeConfig.GraduationMode = (PlayerSyringeGraduationMode)math.clamp((int)math.round(resolvedValue),
                                                                                       (int)PlayerSyringeGraduationMode.FixedUnits,
                                                                                       (int)PlayerSyringeGraduationMode.Hidden);
                return;
            case "terminationStyle":
                runtimeConfig.TerminationStyle = (PlayerSyringeTerminationStyle)math.clamp((int)math.round(resolvedValue),
                                                                                          (int)PlayerSyringeTerminationStyle.Flat,
                                                                                          (int)PlayerSyringeTerminationStyle.Needle);
                return;
            case "paintDrips.density":
                runtimeConfig.PaintDrips.Density = resolvedValue;
                return;
            case "paintDrips.length":
                runtimeConfig.PaintDrips.Length = resolvedValue;
                return;
            case "paintDrips.width":
                runtimeConfig.PaintDrips.Width = resolvedValue;
                return;
            case "paintDrips.irregularity":
                runtimeConfig.PaintDrips.Irregularity = resolvedValue;
                return;
            case "experiencePalettePreset":
                runtimeConfig.Experience.Palette = PlayerHealthBarVisualBakeUtility.BuildPalette(ResolvePalettePreset(resolvedValue));
                return;
        }

        if (TryResolveChannelPath(payloadPath, out SyringeChannelTarget target, out string channelPath))
        {
            ApplyTargetChannelNumericValue(channelPath, resolvedValue, target, ref runtimeConfig);
            return;
        }

        if (payloadPath.StartsWith("experienceShape.", StringComparison.Ordinal))
            ApplyShapeNumericValue(payloadPath.Substring("experienceShape.".Length), resolvedValue, ref runtimeConfig.ExperienceShape);
    }

    #endregion

    #region Shape Application
    /// <summary>
    /// Applies one boolean formula result to an explicit syringe shape profile.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the shape root.</param>
    /// <param name="resolvedValue">Resolved byte-backed boolean value.</param>
    /// <param name="shape">Mutable runtime shape configuration.</param>
    private static void ApplyShapeBooleanValue(string payloadPath,
                                               byte resolvedValue,
                                               ref PlayerSyringeShapeConfig shape)
    {
        switch (payloadPath)
        {
            case "clampPlungerStartInsideBody":
                shape.ClampPlungerStartInsideBody = resolvedValue;
                break;
            case "clampPlungerEndInsideBody":
                shape.ClampPlungerEndInsideBody = resolvedValue;
                break;
            case "stopLiquidAtPlunger":
                shape.StopLiquidAtPlunger = resolvedValue;
                break;
            case "terminationEnabled":
                shape.TerminationEnabled = resolvedValue;
                break;
            case "paintDrips.enabled":
                shape.PaintDrips.Enabled = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric formula result to an explicit syringe shape profile.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the shape root.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="shape">Mutable runtime shape configuration.</param>
    private static void ApplyShapeNumericValue(string payloadPath,
                                               float resolvedValue,
                                               ref PlayerSyringeShapeConfig shape)
    {
        switch (payloadPath)
        {
            case "unitsPerMajorDivision":
                shape.UnitsPerMajorDivision = resolvedValue;
                break;
            case "pixelsPerMajorDivision":
                shape.PixelsPerMajorDivision = resolvedValue;
                break;
            case "minimumLength":
                shape.MinimumLength = resolvedValue;
                break;
            case "maximumLength":
                shape.MaximumLength = resolvedValue;
                break;
            case "labelMinimumSpacing":
                shape.LabelMinimumSpacing = resolvedValue;
                break;
            case "graduationEndPadding":
                shape.GraduationEndPadding = resolvedValue;
                break;
            case "minorDivisionsPerMajor":
                shape.MinorDivisionsPerMajor = (int)math.round(resolvedValue);
                break;
            case "labelEveryMajorDivision":
                shape.LabelEveryMajorDivision = (int)math.round(resolvedValue);
                break;
            case "maximumLabelCount":
                shape.MaximumLabelCount = (int)math.round(resolvedValue);
                break;
            case "uniformLabelCount":
                shape.UniformLabelCount = (int)math.round(resolvedValue);
                break;
            case "labelFontSize":
                shape.LabelFontSize = resolvedValue;
                break;
            case "labelOutlineWidth":
                shape.LabelOutlineWidth = resolvedValue;
                break;
            case "graduationVerticalOffset":
                shape.GraduationVerticalOffset = resolvedValue;
                break;
            case "labelOffset.x":
                shape.LabelOffset.x = resolvedValue;
                break;
            case "labelOffset.y":
                shape.LabelOffset.y = resolvedValue;
                break;
            case "barHeight":
                shape.BarHeight = resolvedValue;
                break;
            case "outlineThickness":
                shape.OutlineThickness = resolvedValue;
                break;
            case "chamberInset":
                shape.ChamberInset = resolvedValue;
                break;
            case "plungerWidth":
                shape.PlungerWidth = resolvedValue;
                break;
            case "endCapWidth":
                shape.EndCapWidth = resolvedValue;
                break;
            case "terminationOffset":
                shape.TerminationOffset = resolvedValue;
                break;
            case "bodyStyle":
                shape.BodyStyle = (PlayerSyringeBodyStyle)math.clamp((int)math.round(resolvedValue),
                                                                      (int)PlayerSyringeBodyStyle.SimplePaintedContainer,
                                                                      (int)PlayerSyringeBodyStyle.DetailedSyringe);
                break;
            case "labelPlacement":
                shape.LabelPlacement = (PlayerSyringeLabelPlacement)math.clamp((int)math.round(resolvedValue),
                                                                               (int)PlayerSyringeLabelPlacement.InsideChamber,
                                                                               (int)PlayerSyringeLabelPlacement.GraduationPlate);
                break;
            case "graduationMode":
                shape.GraduationMode = (PlayerSyringeGraduationMode)math.clamp((int)math.round(resolvedValue),
                                                                               (int)PlayerSyringeGraduationMode.FixedUnits,
                                                                               (int)PlayerSyringeGraduationMode.Hidden);
                break;
            case "terminationStyle":
                shape.TerminationStyle = (PlayerSyringeTerminationStyle)math.clamp((int)math.round(resolvedValue),
                                                                                  (int)PlayerSyringeTerminationStyle.Flat,
                                                                                  (int)PlayerSyringeTerminationStyle.Needle);
                break;
            case "paintDrips.density":
                shape.PaintDrips.Density = resolvedValue;
                break;
            case "paintDrips.length":
                shape.PaintDrips.Length = resolvedValue;
                break;
            case "paintDrips.width":
                shape.PaintDrips.Width = resolvedValue;
                break;
            case "paintDrips.irregularity":
                shape.PaintDrips.Irregularity = resolvedValue;
                break;
        }
    }
    #endregion

    #region Channel Application
    /// <summary>
    /// Routes one boolean formula result to the resolved syringe channel.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the channel root.</param>
    /// <param name="resolvedValue">Resolved byte-backed boolean value.</param>
    /// <param name="target">Resolved syringe channel target.</param>
    /// <param name="runtimeConfig">Mutable runtime player health-bar visual configuration.</param>
    private static void ApplyTargetChannelBooleanValue(string payloadPath,
                                                       byte resolvedValue,
                                                       SyringeChannelTarget target,
                                                       ref PlayerHealthBarVisualConfig runtimeConfig)
    {
        switch (target)
        {
            case SyringeChannelTarget.Health:
                ApplyChannelBooleanValue(payloadPath, resolvedValue, ref runtimeConfig.Health);
                return;
            case SyringeChannelTarget.Shield:
                ApplyChannelBooleanValue(payloadPath, resolvedValue, ref runtimeConfig.Shield);
                return;
            case SyringeChannelTarget.Experience:
                ApplyChannelBooleanValue(payloadPath, resolvedValue, ref runtimeConfig.Experience);
                return;
        }
    }

    /// <summary>
    /// Routes one numeric formula result to the resolved syringe channel.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the channel root.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="target">Resolved syringe channel target.</param>
    /// <param name="runtimeConfig">Mutable runtime player health-bar visual configuration.</param>
    private static void ApplyTargetChannelNumericValue(string payloadPath,
                                                       float resolvedValue,
                                                       SyringeChannelTarget target,
                                                       ref PlayerHealthBarVisualConfig runtimeConfig)
    {
        switch (target)
        {
            case SyringeChannelTarget.Health:
                ApplyChannelNumericValue(payloadPath, resolvedValue, ref runtimeConfig.Health);
                return;
            case SyringeChannelTarget.Shield:
                ApplyChannelNumericValue(payloadPath, resolvedValue, ref runtimeConfig.Shield);
                return;
            case SyringeChannelTarget.Experience:
                ApplyChannelNumericValue(payloadPath, resolvedValue, ref runtimeConfig.Experience);
                return;
        }
    }

    /// <summary>
    /// Applies one boolean formula result to a syringe channel.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the channel root.</param>
    /// <param name="resolvedValue">Resolved byte-backed boolean value.</param>
    /// <param name="channel">Mutable runtime channel configuration.</param>
    private static void ApplyChannelBooleanValue(string payloadPath,
                                                 byte resolvedValue,
                                                 ref PlayerSyringeChannelConfig channel)
    {
        switch (payloadPath)
        {
            case "enabled":
                channel.Enabled = resolvedValue;
                break;
            case "hideWhenMaximumUnavailable":
                channel.HideWhenMaximumUnavailable = resolvedValue;
                break;
            case "sloshAffectsBubblesOnly":
                channel.SloshAffectsBubblesOnly = resolvedValue;
                break;
            case "fluid.flowEnabled":
                channel.Fluid.FlowEnabled = resolvedValue;
                break;
            case "fluid.bubblesEnabled":
                channel.Fluid.BubblesEnabled = resolvedValue;
                break;
            case "motion.movementReactionEnabled":
                channel.Motion.MovementReactionEnabled = resolvedValue;
                break;
            case "motion.horizontalSloshEnabled":
                channel.Motion.HorizontalSloshEnabled = resolvedValue;
                break;
            case "motion.tiltEnabled":
                channel.Motion.TiltEnabled = resolvedValue;
                break;
            case "motion.valueImpulseEnabled":
                channel.Motion.ValueImpulseEnabled = resolvedValue;
                break;
            case "outlineStyle.enabled":
                channel.OutlineStyle.Enabled = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric formula result to a syringe channel.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the channel root.</param>
    /// <param name="resolvedValue">Resolved numeric result.</param>
    /// <param name="channel">Mutable runtime channel configuration.</param>
    private static void ApplyChannelNumericValue(string payloadPath,
                                                 float resolvedValue,
                                                 ref PlayerSyringeChannelConfig channel)
    {
        if (TryApplyPaletteChannel(payloadPath, resolvedValue, ref channel.Palette))
            return;

        switch (payloadPath)
        {
            case "smoothingSeconds":
                channel.SmoothingSeconds = resolvedValue;
                break;
            case "fluid.flowSpeed":
                channel.Fluid.FlowSpeed = resolvedValue;
                break;
            case "fluid.waveAmplitude":
                channel.Fluid.WaveAmplitude = resolvedValue;
                break;
            case "fluid.waveFrequency":
                channel.Fluid.WaveFrequency = resolvedValue;
                break;
            case "fluid.viscosity":
                channel.Fluid.Viscosity = resolvedValue;
                break;
            case "fluid.bubbleDensity":
                channel.Fluid.BubbleDensity = resolvedValue;
                break;
            case "fluid.bubbleMinimumSize":
                channel.Fluid.BubbleMinimumSize = resolvedValue;
                break;
            case "fluid.bubbleMaximumSize":
                channel.Fluid.BubbleMaximumSize = resolvedValue;
                break;
            case "fluid.bubbleRiseSpeed":
                channel.Fluid.BubbleRiseSpeed = resolvedValue;
                break;
            case "fluid.bubbleDrift":
                channel.Fluid.BubbleDrift = resolvedValue;
                break;
            case "motion.sloshStrength":
                channel.Motion.SloshStrength = resolvedValue;
                break;
            case "motion.surfaceSloshStrength":
                channel.Motion.SurfaceSloshStrength = resolvedValue;
                break;
            case "motion.horizontalSloshStrength":
                channel.Motion.HorizontalSloshStrength = resolvedValue;
                break;
            case "motion.sloshSpring":
                channel.Motion.SloshSpring = resolvedValue;
                break;
            case "motion.sloshDamping":
                channel.Motion.SloshDamping = resolvedValue;
                break;
            case "motion.maximumSlosh":
                channel.Motion.MaximumSlosh = resolvedValue;
                break;
            case "motion.maximumTiltDegrees":
                channel.Motion.MaximumTiltDegrees = resolvedValue;
                break;
            case "motion.tiltSpring":
                channel.Motion.TiltSpring = resolvedValue;
                break;
            case "motion.tiltDamping":
                channel.Motion.TiltDamping = resolvedValue;
                break;
            case "motion.valueImpulseStrength":
                channel.Motion.ValueImpulseStrength = resolvedValue;
                break;
            case "motion.valueImpulseDecay":
                channel.Motion.ValueImpulseDecay = resolvedValue;
                break;
            case "outlineStyle.edgeWobbleStrength":
                channel.OutlineStyle.EdgeWobbleStrength = resolvedValue;
                break;
            case "outlineStyle.edgeWobbleFrequency":
                channel.OutlineStyle.EdgeWobbleFrequency = resolvedValue;
                break;
            case "outlineStyle.innerStreakStrength":
                channel.OutlineStyle.InnerStreakStrength = resolvedValue;
                break;
            case "outlineStyle.innerStreakDensity":
                channel.OutlineStyle.InnerStreakDensity = resolvedValue;
                break;
            case "outlineStyle.innerStreakLength":
                channel.OutlineStyle.InnerStreakLength = resolvedValue;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric formula result to a direct palette color channel.
    /// </summary>
    /// <param name="payloadPath">Target field path relative to the syringe channel.</param>
    /// <param name="resolvedValue">Resolved numeric color-channel value.</param>
    /// <param name="palette">Mutable runtime direct palette.</param>
    /// <returns>True when the path targeted a supported palette color channel.</returns>
    private static bool TryApplyPaletteChannel(string payloadPath,
                                               float resolvedValue,
                                               ref PlayerSyringePaletteConfig palette)
    {
        if (string.IsNullOrWhiteSpace(payloadPath) || !payloadPath.StartsWith("palette.", StringComparison.Ordinal))
            return false;

        int channelSeparatorIndex = payloadPath.LastIndexOf('.');

        if (channelSeparatorIndex <= "palette.".Length || channelSeparatorIndex >= payloadPath.Length - 1)
            return false;

        string colorPath = payloadPath.Substring("palette.".Length, channelSeparatorIndex - "palette.".Length);
        char channelName = payloadPath[channelSeparatorIndex + 1];

        switch (colorPath)
        {
            case "outline":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Outline);
            case "body":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Body);
            case "bodyShadow":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.BodyShadow);
            case "chamber":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Chamber);
            case "liquid":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Liquid);
            case "liquidHighlight":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.LiquidHighlight);
            case "bubbles":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Bubbles);
            case "graduation":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Graduation);
            case "label":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Label);
            case "labelOutline":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.LabelOutline);
            case "plunger":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.Plunger);
            case "plungerWindow":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.PlungerWindow);
            case "terminationOutline":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.TerminationOutline);
            case "terminationInterior":
                return TryWriteColorChannel(channelName, resolvedValue, ref palette.TerminationInterior);
            default:
                return false;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves a health, shield, or experience channel path without runtime reflection.
    /// </summary>
    /// <param name="payloadPath">Target path relative to healthBars.</param>
    /// <param name="target">Resolved channel target.</param>
    /// <param name="channelPath">Path relative to the resolved channel root.</param>
    /// <returns>True when the path targets a supported syringe channel.</returns>
    private static bool TryResolveChannelPath(string payloadPath, out SyringeChannelTarget target, out string channelPath)
    {
        target = SyringeChannelTarget.Health;
        channelPath = string.Empty;

        if (payloadPath.StartsWith("health.", StringComparison.Ordinal))
        {
            target = SyringeChannelTarget.Health;
            channelPath = payloadPath.Substring("health.".Length);
            return true;
        }

        if (payloadPath.StartsWith("shield.", StringComparison.Ordinal))
        {
            target = SyringeChannelTarget.Shield;
            channelPath = payloadPath.Substring("shield.".Length);
            return true;
        }

        if (!payloadPath.StartsWith("experience.", StringComparison.Ordinal))
            return false;

        target = SyringeChannelTarget.Experience;
        channelPath = payloadPath.Substring("experience.".Length);
        return true;
    }

    /// <summary>
    /// Resolves one formula-driven numeric enum value into a supported palette preset.
    /// </summary>
    /// <param name="resolvedValue">Numeric formula result targeting Experience Palette Preset.</param>
    /// <returns>Supported built-in syringe palette preset.</returns>
    private static PlayerSyringePalettePreset ResolvePalettePreset(float resolvedValue)
    {
        return (PlayerSyringePalettePreset)math.clamp((int)math.round(resolvedValue),
                                                      (int)PlayerSyringePalettePreset.Health,
                                                      (int)PlayerSyringePalettePreset.Experience);
    }

    /// <summary>
    /// Writes one resolved formula value into an unmanaged RGBA channel.
    /// </summary>
    /// <param name="channelName">Serialized channel name.</param>
    /// <param name="resolvedValue">Resolved numeric channel value.</param>
    /// <param name="color">Mutable unmanaged color.</param>
    /// <returns>True when the channel name is supported.</returns>
    private static bool TryWriteColorChannel(char channelName, float resolvedValue, ref float4 color)
    {
        switch (channelName)
        {
            case 'r':
                color.x = resolvedValue;
                return true;
            case 'g':
                color.y = resolvedValue;
                return true;
            case 'b':
                color.z = resolvedValue;
                return true;
            case 'a':
                color.w = resolvedValue;
                return true;
            default:
                return false;
        }
    }
    #endregion

    #endregion
}
