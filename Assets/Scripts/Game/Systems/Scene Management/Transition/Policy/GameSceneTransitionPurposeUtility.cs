/// <summary>
/// Centralizes transition behavior that is specific to authoritative procedural room progression.
/// </summary>
internal static class GameSceneTransitionPurposeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether one transition purpose belongs to the procedural room lifecycle.
    /// </summary>
    /// <param name="purpose">Transition purpose to inspect.</param>
    /// <returns>True for initial-room, room-traversal and level-boundary transitions.</returns>
    public static bool IsProcedural(GameSceneTransitionPurpose purpose)
    {
        switch (purpose)
        {
            case GameSceneTransitionPurpose.ProceduralInitialRoom:
            case GameSceneTransitionPurpose.ProceduralRoomTraversal:
            case GameSceneTransitionPurpose.ProceduralLevelBoundary:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves whether a same-scene request is valid because it represents two different logical room nodes.
    /// </summary>
    /// <param name="purpose">Transition purpose to inspect.</param>
    /// <returns>True when a procedural request may reload the same reusable room scene.</returns>
    public static bool AllowsSameSceneReload(GameSceneTransitionPurpose purpose)
    {
        return IsProcedural(purpose);
    }

    /// <summary>
    /// Resolves whether a transition must wait for the complete gameplay pool prewarm contract before revealing its target.
    /// Persistent runtime is already initialized during room traversal and level-boundary transitions, where active combat
    /// entities must not be mistaken for an incomplete first-load prewarm.
    /// </summary>
    /// <param name="purpose">Transition purpose to inspect.</param>
    /// <returns>False only for procedural transitions that reuse an already initialized persistent gameplay runtime.</returns>
    public static bool RequiresFullGameplayWarmup(GameSceneTransitionPurpose purpose)
    {
        switch (purpose)
        {
            case GameSceneTransitionPurpose.ProceduralRoomTraversal:
            case GameSceneTransitionPurpose.ProceduralLevelBoundary:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Resolves whether the main source scene must be removed before the next procedural room can load.
    /// </summary>
    /// <param name="unloadSourceBeforeLoad">True when the active transition uses pre-load source removal.</param>
    /// <param name="hasSourceScene">True when a source scene definition was resolved.</param>
    /// <param name="sourceScene">Resolved source scene definition.</param>
    /// <returns>True when the source scene is present and allows automatic unloading.</returns>
    public static bool ShouldUnloadSourceBeforeLoad(bool unloadSourceBeforeLoad,
                                                    bool hasSourceScene,
                                                    GameSceneDefinitionElement sourceScene)
    {
        return unloadSourceBeforeLoad &&
               hasSourceScene &&
               sourceScene.UnloadPolicy == GameSceneUnloadPolicy.UnloadOnTransition;
    }

    /// <summary>
    /// Resolves whether the source companion must be removed before load while preserving a shared target companion.
    /// </summary>
    /// <param name="unloadSourceBeforeLoad">True when the active transition uses pre-load source removal.</param>
    /// <param name="hasSourceCompanionScene">True when a source companion scene definition was resolved.</param>
    /// <param name="hasTargetCompanionScene">True when a target companion scene definition was resolved.</param>
    /// <param name="reloadTargetCompanion">True when an explicit restart must recreate the companion scene.</param>
    /// <param name="sourceCompanionScene">Resolved source companion scene definition.</param>
    /// <param name="targetCompanionScene">Resolved target companion scene definition.</param>
    /// <returns>True when the source companion should be unloaded before loading the target room.</returns>
    public static bool ShouldUnloadSourceCompanionBeforeLoad(bool unloadSourceBeforeLoad,
                                                             bool hasSourceCompanionScene,
                                                             bool hasTargetCompanionScene,
                                                             bool reloadTargetCompanion,
                                                             GameSceneDefinitionElement sourceCompanionScene,
                                                             GameSceneDefinitionElement targetCompanionScene)
    {
        if (!unloadSourceBeforeLoad || !hasSourceCompanionScene)
            return false;

        if (!reloadTargetCompanion &&
            hasTargetCompanionScene &&
            sourceCompanionScene.SceneId.Equals(targetCompanionScene.SceneId))
        {
            return false;
        }

        return sourceCompanionScene.UnloadPolicy == GameSceneUnloadPolicy.UnloadOnTransition;
    }
    #endregion

    #endregion
}
