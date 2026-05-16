/// <summary>
/// Identifies the high-level purpose of a scene handled by the Game Scene Manager.
/// </summary>
public enum GameSceneKind : byte
{
    Bootstrap = 0,
    MainMenu = 1,
    Gameplay = 2,
    PersistentUi = 3,
    Loading = 4,
    Test = 5,
    SubScene = 6,
    PersistentPlayer = 7
}

/// <summary>
/// Describes how a loaded scene should be treated when another scene becomes active.
/// </summary>
public enum GameSceneUnloadPolicy : byte
{
    UnloadOnTransition = 0,
    Persistent = 1,
    Manual = 2
}

/// <summary>
/// Selects the scene loading backend used by the Game Scene Manager.
/// </summary>
public enum GameSceneLoadBackend : byte
{
    BuildSettings = 0,
    Addressables = 1
}

/// <summary>
/// Defines how a transition can be requested by gameplay, UI or authored trigger volumes.
/// </summary>
public enum GameSceneTransitionMode : byte
{
    MenuCommand = 0,
    TriggerVolume = 1,
    ScriptedRequest = 2,
    OrderedNext = 3
}

/// <summary>
/// Defines the runtime request submitted to the scene manager singleton.
/// </summary>
public enum GameSceneTransitionRequestType : byte
{
    LoadScene = 0,
    LoadDefaultGameplay = 1,
    LoadMainMenu = 2,
    RestartActiveScene = 3,
    LoadNextScene = 4
}

/// <summary>
/// Runtime phase used by the scene transition execution system.
/// </summary>
public enum GameSceneTransitionPhase : byte
{
    Idle = 0,
    FadeOut = 1,
    PreUnload = 2,
    Loading = 3,
    PostUnload = 4,
    HoldBlack = 5,
    FadeIn = 6
}
