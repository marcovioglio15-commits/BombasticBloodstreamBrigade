using Unity.Entities;

/// <summary>
/// System group containing all enemy runtime systems.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerControllerSystemGroup))]
public sealed partial class EnemySystemGroup : ComponentSystemGroup
{
    #region Fields
    private EntityQuery transitionStateQuery;
    #endregion

    #region Methods

    #region Lifecycle
    protected override void OnCreate()
    {
        base.OnCreate();
        transitionStateQuery = GetEntityQuery(ComponentType.ReadOnly<GameSceneTransitionState>());
    }

    protected override void OnUpdate()
    {
        if (GameSceneTransitionRuntimeGuardUtility.ShouldBlockGameplay(EntityManager, transitionStateQuery))
            return;

        base.OnUpdate();
    }
    #endregion

    #endregion
}
