using Unity.Entities;

/// <summary>
/// Provides shared helpers used by runtime systems that must react to finalized player-run outcomes.
/// None.
/// </summary>
public static class PlayerRunOutcomeRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns whether the requested entity owns a finalized run outcome.
    /// Used by gameplay systems that must stop consuming live state after victory or defeat.
    /// </summary>
    /// <param name="entity">Entity whose run-outcome state should be inspected.</param>
    /// <param name="runOutcomeLookup">Component lookup used to read PlayerRunOutcomeState.</param>
    /// <returns>True when the entity has a finalized run outcome, otherwise false.</returns>
    public static bool IsFinalized(Entity entity, in ComponentLookup<PlayerRunOutcomeState> runOutcomeLookup)
    {
        if (!runOutcomeLookup.HasComponent(entity))
            return false;

        return runOutcomeLookup[entity].IsFinalized != 0;
    }
    #endregion

    #endregion
}
