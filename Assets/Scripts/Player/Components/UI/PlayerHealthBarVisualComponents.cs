using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Points from the player entity to its dedicated health-bar visual configuration entity.
/// </summary>
public struct PlayerHealthBarVisualReference : IComponentData
{
    public Entity ConfigEntity;
}

/// <summary>
/// Points from the dedicated health-bar visual configuration entity back to its authoritative player.
/// </summary>
public struct PlayerHealthBarVisualOwner : IComponentData
{
    public Entity PlayerEntity;
}

/// <summary>
/// Stores the direct procedural palette consumed by one player syringe shader instance.
/// </summary>
public struct PlayerSyringePaletteConfig
{
    public float4 Outline;
    public float4 Body;
    public float4 BodyShadow;
    public float4 Chamber;
    public float4 Liquid;
    public float4 LiquidHighlight;
    public float4 Bubbles;
    public float4 Graduation;
    public float4 Label;
    public float4 LabelOutline;
    public float4 Plunger;
    public float4 PlungerWindow;
    public float4 TerminationOutline;
    public float4 TerminationInterior;
}

/// <summary>
/// Stores optional procedural paint-drip settings consumed by one syringe shader instance.
/// </summary>
public struct PlayerSyringePaintDripConfig
{
    public float Density;
    public float Length;
    public float Width;
    public float Irregularity;
    public byte Enabled;
}

/// <summary>
/// Stores fluid animation settings consumed by one player syringe shader instance.
/// </summary>
public struct PlayerSyringeFluidConfig
{
    public float FlowSpeed;
    public float WaveAmplitude;
    public float WaveFrequency;
    public float Viscosity;
    public float BubbleDensity;
    public float BubbleMinimumSize;
    public float BubbleMaximumSize;
    public float BubbleRiseSpeed;
    public float BubbleDrift;
    public byte FlowEnabled;
    public byte BubblesEnabled;
}

/// <summary>
/// Stores optional player-movement and value-change reactions consumed by one syringe view.
/// </summary>
public struct PlayerSyringeMotionConfig
{
    public float SloshStrength;
    public float SurfaceSloshStrength;
    public float HorizontalSloshStrength;
    public float SloshSpring;
    public float SloshDamping;
    public float MaximumSlosh;
    public float MaximumTiltDegrees;
    public float TiltSpring;
    public float TiltDamping;
    public float ValueImpulseStrength;
    public float ValueImpulseDecay;
    public byte MovementReactionEnabled;
    public byte HorizontalSloshEnabled;
    public byte TiltEnabled;
    public byte ValueImpulseEnabled;
}

/// <summary>
/// Stores scalable runtime presentation settings for one health or shield syringe.
/// </summary>
public struct PlayerSyringeChannelConfig
{
    public PlayerSyringePaletteConfig Palette;
    public PlayerSyringeFluidConfig Fluid;
    public PlayerSyringeMotionConfig Motion;
    public float SmoothingSeconds;
    public byte Enabled;
    public byte HideWhenMaximumUnavailable;
    public byte SloshAffectsBubblesOnly;
}

/// <summary>
/// Stores the complete scalable ECS-authoritative player health and shield syringe configuration.
/// </summary>
public struct PlayerHealthBarVisualConfig : IComponentData
{
    public PlayerSyringeChannelConfig Health;
    public PlayerSyringeChannelConfig Shield;
    public UnityObjectRef<TMP_FontAsset> FontAsset;
    public float2 LabelOffset;
    public float VerticalSpacing;
    public float UnitsPerMajorDivision;
    public float PixelsPerMajorDivision;
    public float MinimumLength;
    public float MaximumLength;
    public float LabelMinimumSpacing;
    public float GraduationEndPadding;
    public float LabelFontSize;
    public float LabelOutlineWidth;
    public float GraduationVerticalOffset;
    public float BarHeight;
    public float OutlineThickness;
    public float ChamberInset;
    public float PlungerWidth;
    public float EndCapWidth;
    public float TerminationOffset;
    public int MinorDivisionsPerMajor;
    public int LabelEveryMajorDivision;
    public int MaximumLabelCount;
    public int UniformLabelCount;
    public PlayerSyringePaintDripConfig PaintDrips;
    public PlayerSyringeBodyStyle BodyStyle;
    public PlayerSyringeLabelPlacement LabelPlacement;
    public PlayerSyringeGraduationMode GraduationMode;
    public PlayerSyringeTerminationStyle TerminationStyle;
    public byte Enabled;
    public byte HideWhenPlayerMissing;
    public byte ClampPlungerStartInsideBody;
    public byte ClampPlungerEndInsideBody;
    public byte StopLiquidAtPlunger;
}

/// <summary>
/// Stores the immutable player health-bar visual baseline used by runtime scaling rebuilds.
/// </summary>
public struct PlayerBaseHealthBarVisualConfig : IComponentData
{
    public PlayerHealthBarVisualConfig Config;
}

/// <summary>
/// Tracks the unified runtime scaling hash last applied to the player health-bar visual configuration.
/// </summary>
public struct PlayerHealthBarVisualScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores one player health-bar visual scaling entry baked from Player Visual Preset Add Scaling authoring.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeHealthBarVisualScalingElement : IBufferElementData
{
    public FixedString128Bytes PayloadPath;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
