using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Configuration And Definitions
/// <summary>
/// Stores singleton runtime settings for the Game Scene Manager.
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
    public byte EnablePlayerCameraOcclusion;
    public byte LockGameplayInput;
    public byte SetTimeScaleDuringTransition;
    public float FadeOutSeconds;
    public float PostLoadReadyExtraSeconds;
    public float FadeInSeconds;
    public float4 FadeColor;
    public GameSceneFadeMode FadeMode;
    public GameSceneFadeWipeDirection FadeWipeDirection;
    public GameSceneFadeEasing FadeEasing;
    public float FadeDirectionalEdgeSoftness;
    public float FadeDirectionalNoiseStrength;
    public float FadeDirectionalNoiseScale;
    public byte ShowLoadingProgress;
    public byte ShowLoadingProgressPercentage;
    public byte ShowLoadingProgressStatusText;
    public float LoadingProgressSpinnerRotationDegreesPerSecond;
    public float4 LoadingProgressRingColor;
    public float4 LoadingProgressTrackColor;
    public float4 LoadingProgressTextColor;
    public int LoadingProgressRingSegmentCount;
    public float LoadingProgressRingSegmentGapDegrees;
    public float LoadingProgressRingThickness;
    public FixedString64Bytes LoadingProgressLoadingStatusPrefix;
    public FixedString64Bytes LoadingProgressUnloadingStatusPrefix;
    public FixedString128Bytes LoadingProgressReadinessStatusText;
    public FixedString128Bytes LoadingProgressReadyStatusText;
    public FixedString64Bytes TransitionLayerName;
    public float DefaultTriggerCooldownSeconds;
    public byte TriggerRequirePlayer;
    public byte TriggerOneShotByDefault;
}

/// <summary>
/// Stores one baked scene definition available to the runtime scene manager.
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
#endregion

#region Transition Runtime
/// <summary>
/// Stores scene transition requests submitted by UI, triggers or gameplay systems.
/// </summary>
public struct GameSceneTransitionRequest : IBufferElementData
{
    public GameSceneTransitionRequestType RequestType;
    public GameSceneTransitionPurpose Purpose;
    public GameSceneFadeWipeDirection PortalWipeDirection;
    public FixedString64Bytes TargetSceneId;
    public FixedString64Bytes TransitionId;
    public byte ReloadPersistentPlayer;
}

/// <summary>
/// Stores current transition lifecycle state for tools, UI and runtime guards.
/// </summary>
public struct GameSceneTransitionState : IComponentData
{
    public FixedString64Bytes ActiveSceneId;
    public FixedString64Bytes SourceSceneId;
    public FixedString64Bytes TargetSceneId;
    public GameSceneTransitionPhase Phase;
    public GameSceneTransitionPurpose Purpose;
    public byte Initialized;
    public byte IsTransitioning;
}

/// <summary>
/// Stores fade overlay state consumed by the managed presentation bridge.
/// </summary>
public struct GameSceneFadePresentationState : IComponentData
{
    public float Alpha;
    public float4 Color;
    public GameSceneFadeMode Mode;
    public GameSceneFadeWipeDirection WipeDirection;
    public GameSceneFadeEasing Easing;
    public float DirectionalEdgeSoftness;
    public float DirectionalNoiseStrength;
    public float DirectionalNoiseScale;
    public byte Visible;
    public byte OpaquePresented;
}

/// <summary>
/// Stores loading-progress overlay state consumed by the managed presentation bridge.
/// </summary>
public struct GameSceneLoadingProgressPresentationState : IComponentData
{
    public FixedString128Bytes StatusText;
    public float ProgressNormalized;
    public float SpinnerRotationDegreesPerSecond;
    public float4 RingColor;
    public float4 TrackColor;
    public float4 TextColor;
    public int RingSegmentCount;
    public float RingSegmentGapDegrees;
    public float RingThickness;
    public byte Visible;
    public byte ShowPercentage;
    public byte ShowStatusText;
}
#endregion

#region Transition Triggers
/// <summary>
/// Stores one baked scene transition trigger volume in world space.
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
/// </summary>
public struct GameSceneTransitionTriggerRuntimeState : IComponentData
{
    public float CooldownRemainingSeconds;
    public byte WasPlayerInside;
    public byte HasTriggered;
}
#endregion
