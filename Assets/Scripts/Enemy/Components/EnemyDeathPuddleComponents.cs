using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/// <summary>
/// Stores baked render-only death puddle settings for one enemy type.
/// </summary>
public struct EnemyDeathPuddleConfig : IComponentData
{
    public Entity PrefabEntity;
    public float LifetimeSeconds;
    public float StableFraction;
    public float FinalScaleRatio;
    public float FootprintScaleMultiplier;
    public float2 FixedWorldSize;
    public float RandomSizeVariation;
    public float GroundOffset;
    public float EdgeIrregularity;
    public float BorderWidth;
    public float EdgeFeather;
    public float SecondaryPaletteBlend;
    public float FlowSpeed;
    public float Viscosity;
    public float SurfaceDistortion;
    public float HighlightStrength;
    public float4 PrimaryColor;
    public float4 SecondaryColor;
    public EnemyDeathPuddleEvaporationCurve EvaporationCurve;
    public EnemyDeathPuddleSizeMode SizeMode;
    public byte Enabled;
    public byte RandomRotation;
}

/// <summary>
/// Requests one detached puddle at a confirmed killed-enemy ground position.
/// </summary>
public struct EnemyDeathPuddleSpawnRequest : IBufferElementData
{
    public Entity PrefabEntity;
    public float3 Position;
    public float2 WorldSize;
    public float LifetimeSeconds;
    public float StableFraction;
    public float FinalScaleRatio;
    public float EdgeIrregularity;
    public float BorderWidth;
    public float EdgeFeather;
    public float SecondaryPaletteBlend;
    public float FlowSpeed;
    public float Viscosity;
    public float SurfaceDistortion;
    public float HighlightStrength;
    public float4 PrimaryColor;
    public float4 SecondaryColor;
    public uint Seed;
    public EnemyDeathPuddleEvaporationCurve EvaporationCurve;
    public byte RandomRotation;
}

/// <summary>
/// Stores mutable lifetime and source-prefab data for one pooled puddle entity.
/// </summary>
public struct EnemyDeathPuddleRuntimeState : IComponentData
{
    public Entity PrefabEntity;
    public float RemainingSeconds;
}

/// <summary>
/// Marks a pooled death puddle that is currently visible and consuming lifetime.
/// </summary>
public struct EnemyDeathPuddleActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Per-instance primary death puddle palette color.
/// </summary>
[MaterialProperty("_PuddlePrimaryColor")]
public struct MaterialDeathPuddlePrimaryColor : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance secondary death puddle palette color.
/// </summary>
[MaterialProperty("_PuddleSecondaryColor")]
public struct MaterialDeathPuddleSecondaryColor : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance spawn time, lifetime, stable fraction and final scale ratio.
/// </summary>
[MaterialProperty("_PuddleTiming")]
public struct MaterialDeathPuddleTiming : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance irregularity, border width, edge feather and deterministic seed.
/// </summary>
[MaterialProperty("_PuddleShape")]
public struct MaterialDeathPuddleShape : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance secondary palette blend and evaporation curve selector.
/// </summary>
[MaterialProperty("_PuddleStyle")]
public struct MaterialDeathPuddleStyle : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance flow speed, viscosity, surface distortion and highlight strength.
/// </summary>
[MaterialProperty("_PuddleFluid")]
public struct MaterialDeathPuddleFluid : IComponentData
{
    public float4 Value;
}
