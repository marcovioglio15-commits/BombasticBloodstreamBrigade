using Unity.Entities;

/// <summary>
/// Groups runtime systems that process scene transition requests before gameplay simulation consumes input.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public sealed partial class GameSceneManagementSystemGroup : ComponentSystemGroup
{
}
