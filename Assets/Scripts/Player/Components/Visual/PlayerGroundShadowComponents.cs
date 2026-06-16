using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Runtime player hit-box shadow configuration consumed by world-space presentation.
/// </summary>
public struct PlayerGroundShadowConfig : IComponentData
{
    public float HitAreaMultiplier;
    public float2 PositionOffsetXZ;
    public float HeightOffset;
    public GroundShadowProjectionMode ProjectionMode;
    public float ProjectionMaxDistance;
    public float4 ShadowColor;
    public float ShadowAlpha;
    public float ShadowEdgeSoftness;
    public byte Enabled;
}

/// <summary>
/// Immutable player hit-box shadow baseline used when runtime scaling formulas rebuild visual settings.
/// </summary>
public struct PlayerBaseGroundShadowConfig : IComponentData
{
    public PlayerGroundShadowConfig Config;
}

/// <summary>
/// Tracks the unified runtime scaling hash last applied to player hit-box shadow settings.
/// </summary>
public struct PlayerGroundShadowScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores one player ground-shadow scaling entry baked from Visual Preset Add Scaling authoring data.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeGroundShadowScalingElement : IBufferElementData
{
    public FixedString128Bytes PayloadPath;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}
