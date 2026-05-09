/// <summary>
/// Immutable setup data used to write one default scene definition.
/// /params None.
/// /returns None.
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
    /// /params sceneId Stable Scene Manager scene ID.
    /// /params sceneName Unity scene name.
    /// /params scenePath Project-relative scene path.
    /// /params sceneKind High-level scene role.
    /// /params unloadPolicy Automatic unload policy.
    /// /params companionUiSceneId Optional scene ID loaded additively with this scene.
    /// /returns None.
    /// </summary>
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
/// /params None.
/// /returns None.
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
    /// /params transitionId Stable transition ID.
    /// /params fromSceneId Source scene ID.
    /// /params toSceneId Target scene ID.
    /// /params transitionMode Request mode expected to start the transition.
    /// /returns None.
    /// </summary>
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
