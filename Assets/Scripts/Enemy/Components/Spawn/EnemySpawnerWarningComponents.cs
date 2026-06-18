using Unity.Entities;
using Unity.Mathematics;

#region Enemy Spawner Warning Components
/// <summary>
/// Stores immutable visual tuning used by one enemy spawner to preview upcoming spawn events.
/// </summary>
public struct EnemySpawnWarningConfig : IComponentData
{
    public byte Enabled;
    public float LeadTimeSeconds;
    public float FadeOutSeconds;
    public float RadiusScale;
    public float RingWidth;
    public float HeightOffset;
    public float MaximumAlpha;
    public float4 Color;
    public float CellSize;
}
#endregion
