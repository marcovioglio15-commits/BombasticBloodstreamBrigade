using Unity.Collections;

/// <summary>
/// Provides transition unload policy checks for source scenes, companion UI and persistent player scenes.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameSceneTransitionUnloadPolicyUtility
{
    #region Methods

    #region Source Scene
    /// <summary>
    /// Resolves whether the source scene should unload after the target scene has loaded.
    /// /params hasSourceScene True when sourceScene contains a valid scene definition.
    /// /params reloadActiveScene True when the transition reloads the active scene.
    /// /params sourceSceneId Runtime ID for the source scene.
    /// /params targetSceneId Runtime ID for the target scene.
    /// /params sourceScene Source scene definition.
    /// /returns True when the source scene can be unloaded after load.
    /// </summary>
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
    /// /params hasSourceCompanionScene True when sourceCompanionScene contains authored data.
    /// /params reloadActiveScene True when the transition reloads the active scene.
    /// /params hasTargetCompanionScene True when targetCompanionScene contains authored data.
    /// /params sourceCompanionScene Source companion UI scene definition.
    /// /params targetCompanionScene Target companion UI scene definition.
    /// /returns True when the source companion UI scene can be unloaded after load.
    /// </summary>
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
    /// /params unloadSourceScene True when the source scene should unload.
    /// /params unloadSourceCompanionScene True when the source companion UI scene should unload.
    /// /params persistentPlayerPostLoadUnloadCount Number of persistent player scenes queued for post-load unload.
    /// /returns True when a post-load unload phase is required.
    /// </summary>
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
