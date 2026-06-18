/// <summary>
/// Immutable setup data used to write one default scene definition.
/// </summary>
internal readonly struct GameSceneDefinitionSetup
{
    #region Fields
    public readonly string SceneId;
    public readonly string SceneName;
    public readonly string ScenePath;
    public readonly GameSceneKind SceneKind;
    public readonly GameSceneUnloadPolicy UnloadPolicy;
    public readonly string CompanionUiSceneId;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates setup data for one managed scene entry.
    /// </summary>
    /// <param name="sceneId">Stable Scene Manager scene ID.</param>
    /// <param name="sceneName">Unity scene name.</param>
    /// <param name="scenePath">Project-relative scene path.</param>
    /// <param name="sceneKind">High-level scene role.</param>
    /// <param name="unloadPolicy">Automatic unload policy.</param>
    /// <param name="companionUiSceneId">Optional scene ID loaded additively with this scene.</param>
    public GameSceneDefinitionSetup(string sceneId,
                                    string sceneName,
                                    string scenePath,
                                    GameSceneKind sceneKind,
                                    GameSceneUnloadPolicy unloadPolicy,
                                    string companionUiSceneId)
    {
        SceneId = sceneId;
        SceneName = sceneName;
        ScenePath = scenePath;
        SceneKind = sceneKind;
        UnloadPolicy = unloadPolicy;
        CompanionUiSceneId = companionUiSceneId;
    }
    #endregion
}

/// <summary>
/// Immutable setup data used to write one default transition definition.
/// </summary>
internal readonly struct GameSceneTransitionDefinitionSetup
{
    #region Fields
    public readonly string TransitionId;
    public readonly string FromSceneId;
    public readonly string ToSceneId;
    public readonly GameSceneTransitionMode TransitionMode;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates setup data for one scene transition entry.
    /// </summary>
    /// <param name="transitionId">Stable transition ID.</param>
    /// <param name="fromSceneId">Source scene ID.</param>
    /// <param name="toSceneId">Target scene ID.</param>
    /// <param name="transitionMode">Request mode expected to start the transition.</param>
    public GameSceneTransitionDefinitionSetup(string transitionId,
                                              string fromSceneId,
                                              string toSceneId,
                                              GameSceneTransitionMode transitionMode)
    {
        TransitionId = transitionId;
        FromSceneId = fromSceneId;
        ToSceneId = toSceneId;
        TransitionMode = transitionMode;
    }
    #endregion
}
