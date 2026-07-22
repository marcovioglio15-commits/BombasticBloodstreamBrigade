#if UNITY_EDITOR
using System;
using Unity.Entities;

/// <summary>
/// Provides focused assertions for purpose-specific procedural transition presentation policy.
/// </summary>
public static class GameProceduralTransitionPresentationSmokeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Verifies detailed loading presentation can be suppressed only for intra-level room traversal.
    /// </summary>
    public static void ValidateRoomLoadingSuppressionPolicy()
    {
        World world = new World("GameProceduralRoomLoadingSuppressionPolicySmokeTest");

        try
        {
            EntityManager entityManager = world.EntityManager;
            Entity managerEntity = entityManager.CreateEntity(typeof(GameProceduralLevelConfig));
            entityManager.SetComponentData(managerEntity, new GameProceduralLevelConfig
            {
                HideLoadingProgressDuringRoomTransitions = 1
            });
            Require(GameSceneTransitionPurposeUtility.ShouldHideDetailedLoadingProgress(entityManager,
                                                                                         managerEntity,
                                                                                         GameSceneTransitionPurpose.ProceduralRoomTraversal),
                    "Enabled room-loading suppression was not applied to intra-level traversal.");
            Require(!GameSceneTransitionPurposeUtility.ShouldHideDetailedLoadingProgress(entityManager,
                                                                                          managerEntity,
                                                                                          GameSceneTransitionPurpose.ProceduralInitialRoom),
                    "Room-loading suppression incorrectly hid initial run loading presentation.");
            Require(!GameSceneTransitionPurposeUtility.ShouldHideDetailedLoadingProgress(entityManager,
                                                                                          managerEntity,
                                                                                          GameSceneTransitionPurpose.ProceduralLevelBoundary),
                    "Room-loading suppression incorrectly hid level-boundary loading presentation.");

            GameProceduralLevelConfig config = entityManager.GetComponentData<GameProceduralLevelConfig>(managerEntity);
            config.HideLoadingProgressDuringRoomTransitions = 0;
            entityManager.SetComponentData(managerEntity, config);
            Require(!GameSceneTransitionPurposeUtility.ShouldHideDetailedLoadingProgress(entityManager,
                                                                                          managerEntity,
                                                                                          GameSceneTransitionPurpose.ProceduralRoomTraversal),
                    "Disabled room-loading suppression still hid traversal loading presentation.");
        }
        finally
        {
            world.Dispose();
        }
    }
    #endregion

    #region Assertion Methods
    /// <summary>
    /// Throws one actionable smoke-test failure when a transition-presentation invariant is not satisfied.
    /// </summary>
    /// <param name="condition">Invariant result that must be true.</param>
    /// <param name="message">Failure message describing the violated invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("GameProceduralTransitionPresentationSmokeUtility: " + message);
    }
    #endregion

    #endregion
}
#endif
