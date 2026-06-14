using Unity.Collections;

/// <summary>
/// Provides transition unload policy checks for source scenes, companion UI and persistent player scenes.
/// </summary>
internal static class GameSceneTransitionUnloadPolicyUtility
{
    #region Methods

    #region Source Scene
    /// <summary>
    /// Resolves whether the source scene should unload after the target scene has loaded.
    /// </summary>
    /// <param name="hasSourceScene">True when sourceScene contains a valid scene definition.</param>
    /// <param name="reloadActiveScene">True when the transition reloads the active scene.</param>
    /// <param name="sourceSceneId">Runtime ID for the source scene.</param>
    /// <param name="targetSceneId">Runtime ID for the target scene.</param>
    /// <param name="sourceScene">Source scene definition.</param>
    /// <returns>True when the source scene can be unloaded after load.</returns>
    public static bool ShouldUnloadSourceAfterLoad(bool hasSourceScene,
                                                   bool reloadActiveScene,
                                                   FixedString64Bytes sourceSceneId,
                                                   FixedString64Bytes targetSceneId,
                                                   GameSceneDefinitionElement sourceScene)
    {
        if (!hasSourceScene)
            return false;

        if (reloadActiveScene)
            return false;

        if (sourceSceneId.Equals(targetSceneId))
            return false;

        return sourceScene.UnloadPolicy == GameSceneUnloadPolicy.UnloadOnTransition;
    }
    #endregion

    #region Companion Scene
    /// <summary>
    /// Resolves whether the source companion UI scene should unload after the target scene has loaded.
    /// </summary>
    /// <param name="hasSourceCompanionScene">True when sourceCompanionScene contains authored data.</param>
    /// <param name="reloadActiveScene">True when the transition reloads the active scene.</param>
    /// <param name="hasTargetCompanionScene">True when targetCompanionScene contains authored data.</param>
    /// <param name="sourceCompanionScene">Source companion UI scene definition.</param>
    /// <param name="targetCompanionScene">Target companion UI scene definition.</param>
    /// <returns>True when the source companion UI scene can be unloaded after load.</returns>
    public static bool ShouldUnloadSourceCompanionAfterLoad(bool hasSourceCompanionScene,
                                                           bool reloadActiveScene,
                                                           bool hasTargetCompanionScene,
                                                           GameSceneDefinitionElement sourceCompanionScene,
                                                           GameSceneDefinitionElement targetCompanionScene)
    {
        if (!hasSourceCompanionScene)
            return false;

        if (reloadActiveScene)
            return false;

        if (hasTargetCompanionScene && sourceCompanionScene.SceneId.Equals(targetCompanionScene.SceneId))
            return false;

        return sourceCompanionScene.UnloadPolicy == GameSceneUnloadPolicy.UnloadOnTransition;
    }
    #endregion

    #region Aggregate
    /// <summary>
    /// Resolves whether any post-load unload work must run before fade-in.
    /// </summary>
    /// <param name="unloadSourceScene">True when the source scene should unload.</param>
    /// <param name="unloadSourceCompanionScene">True when the source companion UI scene should unload.</param>
    /// <param name="persistentPlayerPostLoadUnloadCount">Number of persistent player scenes queued for post-load unload.</param>
    /// <returns>True when a post-load unload phase is required.</returns>
    public static bool ShouldRunPostUnload(bool unloadSourceScene,
                                           bool unloadSourceCompanionScene,
                                           int persistentPlayerPostLoadUnloadCount)
    {
        return unloadSourceScene ||
               unloadSourceCompanionScene ||
               persistentPlayerPostLoadUnloadCount > 0;
    }
    #endregion

    #endregion
}
