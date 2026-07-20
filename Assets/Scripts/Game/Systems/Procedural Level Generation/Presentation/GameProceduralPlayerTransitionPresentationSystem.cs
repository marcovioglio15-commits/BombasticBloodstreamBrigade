using Unity.Entities;

/// <summary>
/// Activates player-only rendering and optional animation for intra-level transitions while leaving first and boundary loads fully black.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameProceduralPlayerTransitionPresentationSystem : SystemBase
{
    #region Fields
    private EntityQuery managerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the unique scene and procedural configuration query.
    /// </summary>
    protected override void OnCreate()
    {
        managerQuery = GetEntityQuery(typeof(GameSceneTransitionState),
                                      typeof(GameProceduralLevelConfig));
    }

    /// <summary>
    /// Starts presentation once when a room traversal begins and restores it after the transition completes.
    /// </summary>
    protected override void OnUpdate()
    {
        if (managerQuery.CalculateEntityCount() != 1)
        {
            GameProceduralPlayerTransitionPresentationUtility.End();
            return;
        }

        Entity managerEntity = managerQuery.GetSingletonEntity();
        GameSceneTransitionState transitionState = EntityManager.GetComponentData<GameSceneTransitionState>(managerEntity);
        GameProceduralLevelConfig config = EntityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);
        bool shouldShowPlayer = transitionState.IsTransitioning != 0 &&
                                transitionState.Purpose == GameSceneTransitionPurpose.ProceduralRoomTraversal &&
                                config.KeepPlayerVisible != 0;

        if (shouldShowPlayer)
        {
            GameProceduralPlayerTransitionPresentationUtility.Begin(EntityManager, config);
            return;
        }

        GameProceduralPlayerTransitionPresentationUtility.End();
    }

    /// <summary>
    /// Restores all managed presentation state if the ECS world is disposed during a transition.
    /// </summary>
    protected override void OnDestroy()
    {
        GameProceduralPlayerTransitionPresentationUtility.End();
    }
    #endregion

    #endregion
}
