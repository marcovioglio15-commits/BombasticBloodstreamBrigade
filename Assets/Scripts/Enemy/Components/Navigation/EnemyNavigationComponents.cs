using Unity.Entities;
using Unity.Mathematics;

#region Enemy Navigation Components
/// <summary>
/// Tags the singleton entity that stores shared enemy navigation flow-field data.
/// None.
/// </summary>
public struct EnemyNavigationGridTag : IComponentData
{
}

/// <summary>
/// Stores the shared navigation-grid layout derived from static wall colliders.
/// None.
/// </summary>
public struct EnemyNavigationGridState : IComponentData
{
    public float2 Origin;
    public float CellSize;
    public float InverseCellSize;
    public float AgentRadius;
    public int Width;
    public int Height;
    public int PlayerCellIndex;
    public float NextFlowRefreshTime;
    public uint StaticLayoutHash;
    public byte Initialized;
    public byte FlowReady;
}

/// <summary>
/// Stores one navigation cell with baked walkability, neighbor links, and runtime flow direction.
/// None.
/// </summary>
public struct EnemyNavigationCellElement : IBufferElementData
{
    public int Cost;
    public byte Walkable;
    public byte NeighborMask;
    public float2 FlowDirection;
}
#endregion
