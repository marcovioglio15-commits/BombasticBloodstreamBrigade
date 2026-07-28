using Unity.Entities;

/// <summary>
/// Stores runtime layer masks used by gameplay systems.
/// </summary>
public struct PlayerWorldLayersConfig : IComponentData
{
    public int WallsLayerMask;
    public int PortalBarrierLayerMask;
}
