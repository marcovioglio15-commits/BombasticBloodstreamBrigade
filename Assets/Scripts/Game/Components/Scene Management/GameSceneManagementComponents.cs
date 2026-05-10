using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores singleton runtime settings for the Game Scene Manager.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneManagerConfig : IComponentData
{
    public FixedString64Bytes BootstrapSceneId;
    public FixedString64Bytes InitialSceneId;
    public FixedString64Bytes MainMenuSceneId;
    public FixedString64Bytes DefaultGameplaySceneId;
    public GameSceneLoadBackend LoadBackend;
    public byte AutoLoadInitialScene;
    public byte LogTransitions;
    public byte LockGameplayInput;
    public byte SetTimeScaleDuringTransition;
    public float FadeOutSeconds;
    public float PostLoadReadyExtraSeconds;
    public float FadeInSeconds;
    public float4 FadeColor;
    public FixedString64Bytes TransitionLayerName;
    public float DefaultTriggerCooldownSeconds;
    public byte TriggerRequirePlayer;
    public byte TriggerOneShotByDefault;
}

/// <summary>
/// Stores one baked scene definition available to the runtime scene manager.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneDefinitionElement : IBufferElementData
{
    public FixedString64Bytes SceneId;
    public FixedString64Bytes SceneName;
    public FixedString512Bytes ScenePath;
    public FixedString64Bytes SceneGuid;
    public FixedString128Bytes AddressableKey;
    public FixedString64Bytes CompanionUiSceneId;
    public int BuildIndex;
    public int OrderIndex;
    public GameSceneKind SceneKind;
    public GameSceneUnloadPolicy UnloadPolicy;
}

/// <summary>
/// Stores one baked transition definition available to runtime requests and triggers.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneTransitionElement : IBufferElementData
{
    public FixedString64Bytes TransitionId;
    public FixedString64Bytes FromSceneId;
    public FixedString64Bytes ToSceneId;
    public FixedString64Bytes TriggerId;
    public int Priority;
    public GameSceneTransitionMode TransitionMode;
    public byte OneShotTrigger;
    public byte OverrideFadeSettings;
    public byte AllowDuringPause;
    public byte AllowWhenRunFinalized;
    public float TriggerCooldownOverrideSeconds;
    public float FadeOutSeconds;
    public float PostLoadReadyExtraSeconds;
    public float FadeInSeconds;
}

/// <summary>
/// Stores scene transition requests submitted by UI, triggers or gameplay systems.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneTransitionRequest : IBufferElementData
{
    public GameSceneTransitionRequestType RequestType;
    public FixedString64Bytes TargetSceneId;
    public FixedString64Bytes TransitionId;
}

/// <summary>
/// Stores current transition lifecycle state for tools, UI and runtime guards.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneTransitionState : IComponentData
{
    public FixedString64Bytes ActiveSceneId;
    public FixedString64Bytes SourceSceneId;
    public FixedString64Bytes TargetSceneId;
    public GameSceneTransitionPhase Phase;
    public byte Initialized;
    public byte IsTransitioning;
}

/// <summary>
/// Stores fade overlay state consumed by the managed presentation bridge.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneFadePresentationState : IComponentData
{
    public float Alpha;
    public float4 Color;
    public byte Visible;
}

/// <summary>
/// Stores one baked scene transition trigger volume in world space.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneTransitionTrigger : IComponentData
{
    public FixedString64Bytes TriggerId;
    public FixedString64Bytes TransitionId;
    public FixedString64Bytes TargetSceneId;
    public float3 Center;
    public float3 HalfExtents;
    public float CooldownSeconds;
    public byte OneShot;
    public byte RequirePlayer;
}

/// <summary>
/// Stores mutable cooldown and activation state for one scene transition trigger.
/// /params None.
/// /returns None.
/// </summary>
public struct GameSceneTransitionTriggerRuntimeState : IComponentData
{
    public float CooldownRemainingSeconds;
    public byte WasPlayerInside;
    public byte HasTriggered;
}
