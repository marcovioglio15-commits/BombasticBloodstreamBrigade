using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/// <summary>
/// Stores baked shader-driven elastic hit response settings.
/// </summary>
public struct EnemyElasticHitConfig : IComponentData
{
    public float DurationSeconds;
    public float MaximumCompression;
    public float VolumeCompensation;
    public float OscillationCount;
    public float Damping;
    public float Directionality;
    public float MinimumRetriggerInterval;
    public EnemyElasticHitTriggerMode TriggerMode;
    public EnemyElasticHitRetriggerMode RetriggerMode;
    public byte Enabled;
    public byte AnchorToGround;
}

/// <summary>
/// Stores mutable elastic hit playback and retrigger state.
/// </summary>
public struct EnemyElasticHitState : IComponentData
{
    public float RemainingSeconds;
    public float LastTriggerTime;
    public float3 DirectionWorld;
}

/// <summary>
/// Marks enemies whose elastic hit response still requires an expiry reset.
/// </summary>
public struct EnemyElasticHitActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Per-instance world-space hit direction used by compatible enemy shaders.
/// </summary>
[MaterialProperty("_ElasticHitDirection")]
public struct MaterialElasticHitDirection : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance trigger time, duration, compression and volume compensation.
/// </summary>
[MaterialProperty("_ElasticHitTiming")]
public struct MaterialElasticHitTiming : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Per-instance oscillation count, damping, directionality and ground anchoring.
/// </summary>
[MaterialProperty("_ElasticHitMotion")]
public struct MaterialElasticHitMotion : IComponentData
{
    public float4 Value;
}
