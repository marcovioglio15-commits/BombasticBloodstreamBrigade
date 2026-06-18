using Unity.Entities;

#region Components
/// <summary>
/// Global counter used by gameplay systems that react to enemy kills.
/// </summary>
public struct GlobalEnemyKillCounter : IComponentData
{
    public uint TotalKilled;
}
#endregion
