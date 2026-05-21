using Unity.Entities;
using Unity.Mathematics;

#region Enemy Spawn Control Components
/// <summary>
/// Locks enemy behaviour modules for a short authored interval immediately after spawn.
/// </summary>
public struct EnemySpawnInactivityLock : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Stores the live warning state associated with one reserved or freshly spawned enemy.
/// </summary>
public struct EnemySpawnWarningState : IComponentData, IEnableableComponent
{
    public float3 WorldPosition;
    public float WarningStartTime;
    public float SpawnTime;
    public float FadeOutEndTime;
    public float LeadTimeSeconds;
    public float FadeOutSeconds;
    public float Radius;
    public float RingWidth;
    public float HeightOffset;
    public float MaximumAlpha;
    public float4 Color;
}

/// <summary>
/// Tracks whether the warning-phase spawn VFX has already been queued for the current pooled enemy reuse.
/// </summary>
public struct EnemySpawnVfxRuntimeState : IComponentData
{
    public byte WarningVfxQueued;
}
#endregion
