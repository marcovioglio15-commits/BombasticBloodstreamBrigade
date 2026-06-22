using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Points from the player entity to its dedicated active power-up HUD visual configuration entity.
/// </summary>
public struct PlayerActivePowerUpHudVisualReference : IComponentData
{
    public Entity ConfigEntity;
}

/// <summary>
/// Points from the active power-up HUD visual configuration entity back to its authoritative player.
/// </summary>
public struct PlayerActivePowerUpHudVisualOwner : IComponentData
{
    public Entity PlayerEntity;
}

/// <summary>
/// Stores activation-requirement marker settings consumed by active energy syringe views.
/// </summary>
public struct PlayerPowerUpRequirementMarkerVisualConfig
{
    public float4 Color;
    public float Width;
    public float Height;
    public float VerticalOffset;
    public byte Enabled;
}

/// <summary>
/// Stores charge semiring settings consumed by active power-up HUD views.
/// </summary>
public struct PlayerPowerUpChargeRingVisualConfig
{
    public float4 BackgroundColor;
    public float4 FillColor;
    public float4 OutlineColor;
    public float Thickness;
    public float OutlineThickness;
    public float StartAngleDegrees;
    public float ArcDegrees;
    public PlayerPowerUpChargeRingFillDirection FillDirection;
    public byte Enabled;
}

/// <summary>
/// Stores icon cooldown reveal settings consumed by active power-up HUD views.
/// </summary>
public struct PlayerPowerUpIconCooldownVisualConfig
{
    public float4 LockedTint;
    public float DesaturationStrength;
    public float RevealFeather;
    public PlayerPowerUpIconCooldownFillDirection FillDirection;
    public byte Enabled;
}

/// <summary>
/// Stores the complete active power-up HUD visual configuration resolved from the Player Visual Preset.
/// </summary>
public struct PlayerActivePowerUpHudVisualConfig : IComponentData
{
    public PlayerHealthBarVisualConfig EnergySyringe;
    public PlayerPowerUpRequirementMarkerVisualConfig RequirementMarker;
    public PlayerPowerUpChargeRingVisualConfig ChargeRing;
    public PlayerPowerUpIconCooldownVisualConfig IconCooldown;
    public float ChargeSmoothingSeconds;
    public byte Enabled;
    public byte HideWhenPlayerMissing;
    public byte HideEnergyWhenModuleMissing;
    public byte HideChargeWhenModuleMissing;
}

/// <summary>
/// Stores the immutable active power-up HUD visual baseline used by runtime scaling rebuilds.
/// </summary>
public struct PlayerBaseActivePowerUpHudVisualConfig : IComponentData
{
    public PlayerActivePowerUpHudVisualConfig Config;
}

/// <summary>
/// Tracks the unified runtime scaling hash last applied to active power-up HUD visual configuration.
/// </summary>
public struct PlayerActivePowerUpHudVisualScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores one active power-up HUD visual scaling entry baked from Player Visual Preset Add Scaling authoring.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeActivePowerUpHudVisualScalingElement : IBufferElementData
{
    public FixedString128Bytes PayloadPath;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
