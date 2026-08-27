using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#region Baked Configuration
/// <summary>
/// Stores immutable global settings baked from one Room Clear Rewards preset.
/// </summary>
public struct GameRoomRewardConfig : IComponentData
{
    public FixedString64Bytes PresetId;
    public FixedString64Bytes PlayerPresetId;
    public int ModuleCount;
    public int RewardCount;
    public int MappingCount;
    public float3 PlayerLogWorldOffset;
    public GameRoomRewardValueDisplayMode PlayerLogValueDisplayMode;
    public float PlayerLogFontSize;
    public float PlayerLogRowSpacing;
    public int PlayerLogVisibleRows;
    public int PlayerLogQueueCapacity;
    public float PlayerLogEnterDuration;
    public float PlayerLogHoldDuration;
    public float PlayerLogExitDuration;
    public float PlayerLogScrollDistance;
    public UnityObjectRef<TMPro.TMP_FontAsset> PlayerLogFont;
    public float3 PortalWorldOffset;
    public GameRoomRewardPortalLogLayoutMode PortalLayoutMode;
    public GameRoomRewardValueDisplayMode PortalValueDisplayMode;
    public float PortalFontSize;
    public float PortalCellSpacing;
    public int PortalVisibleCells;
    public float PortalScrollSpeed;
    public float PortalInitialPause;
    public float PortalLoopPause;
    public UnityObjectRef<TMPro.TMP_FontAsset> PortalFont;
    public float PortalStaticRowSpacing;
    public float2 PortalStaticPanelPadding;
    public float2 PortalStaticMinimumPanelSize;
    public float4 PortalStaticBackgroundColor;
    public UnityObjectRef<Sprite> PortalStaticBackgroundSprite;
    public int PortalAnimationCount;
    public int PortalPrefabReplacementCount;
    public GameRoomPortalUnlockAudioPlaybackMode PortalUnlockAudioPlaybackMode;
    public byte PortalUnlockAudioEnabled;
    public byte PortalIndicatorsEnabled;
    public float3 PortalIndicatorWorldOffset;
    public float4 PortalIndicatorColor;
    public float PortalIndicatorSizePixels;
    public float PortalIndicatorEdgePaddingPixels;
    public int PortalIndicatorSortingOrder;
    public UnityObjectRef<Sprite> PortalIndicatorSprite;
}

/// <summary>
/// Stores one flattened atomic room reward module.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomRewardModuleElement : IBufferElementData
{
    public FixedString64Bytes TechnicalId;
    public FixedString128Bytes DisplayName;
    public FixedString128Bytes Description;
    public FixedString64Bytes TargetStatName;
    public FixedString512Bytes Formula;
    public FixedString64Bytes FlatTokenValue;
    public GameRoomRewardTargetDomain TargetDomain;
    public GameRoomRewardValueSource ValueSource;
    public GameRoomRewardDuration Duration;
    public GameRoomRewardResource Resource;
    public PlayerScalableStatType TargetStatType;
    public float FlatNumericValue;
    public byte FlatBooleanValue;
    public int DurationRooms;
    public int SortOrder;
    public int PresentationMappingIndex;
}

/// <summary>
/// Stores one flattened composed room reward and its contiguous module-binding range.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomRewardDefinitionElement : IBufferElementData
{
    public FixedString64Bytes TechnicalId;
    public FixedString128Bytes DisplayName;
    public FixedString128Bytes Description;
    public GameRoomRewardMenuGroup MenuGroup;
    public int ModuleBindingStartIndex;
    public int ModuleBindingCount;
}

/// <summary>
/// Stores one ordered module reference belonging to a flattened room reward.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomRewardModuleBindingElement : IBufferElementData
{
    public int RewardIndex;
    public int ModuleIndex;
    public int Quantity;
    public int Order;
}

/// <summary>
/// Stores one ordered room reward assignment belonging to a flattened procedural tile.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomRewardTileBindingElement : IBufferElementData
{
    public int TileIndex;
    public int RewardIndex;
    public int Quantity;
    public int Order;
    public FixedString64Bytes SelectionGroupId;
    public FixedString64Bytes DifficultyCoefficientId;
    public float MinimumDifficulty;
    public float MaximumDifficulty;
    public float SelectionWeight;
    public byte UseDifficultySelection;
}

/// <summary>
/// Stores one shared text or sprite mapping for a used stat or resource target.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomRewardPresentationElement : IBufferElementData
{
    public FixedString64Bytes TargetStatName;
    public FixedString64Bytes DisplayLabel;
    public FixedString64Bytes SpriteCaption;
    public GameRoomRewardTargetDomain TargetDomain;
    public GameRoomRewardResource Resource;
    public GameRoomRewardPresentationMode Mode;
    public float4 TextColor;
    public UnityObjectRef<Sprite> Sprite;
    public int SortOrder;
}

/// <summary>
/// Stores one immutable Transform or Animator-clip animation applied by a managed portal anchor.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomPortalActivationAnimationElement : IBufferElementData
{
    public FixedString64Bytes TargetBindingId;
    public FixedString128Bytes AnimatorPath;
    public GameRoomPortalActivationAnimationSource Source;
    public GameRoomPortalTransformAnimationMode Mode;
    public GameRoomPortalTransformAnimationPlayback Playback;
    public GameRoomPortalTransformAnimationEase Easing;
    public float StartDelay;
    public float Duration;
    public float3 PositionOffset;
    public float3 RotationOffset;
    public float3 ScaleMultiplier;
    public float AnimatorSpeed;
    public UnityObjectRef<AnimationClip> AnimatorClip;
}

/// <summary>
/// Stores one immutable prefab replacement applied before portal activation animations.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameRoomPortalPrefabReplacementElement : IBufferElementData
{
    public FixedString64Bytes TargetBindingId;
    public UnityObjectRef<GameObject> ReplacementPrefab;
}

/// <summary>
/// Stores room-scoped dispatch state for the shared portal-unlock audio event.
/// </summary>
public struct GameRoomPortalUnlockAudioRuntimeState : IComponentData
{
    public uint GenerationVersion;
    public int NodeIndex;
    public byte Dispatched;
}
#endregion

#region Player Runtime State
/// <summary>
/// Stores the last authoritative room-clear event committed to one player.
/// </summary>
public struct PlayerRoomRewardGrantState : IComponentData
{
    public uint LastRunSeed;
    public uint LastGenerationVersion;
    public uint LastClearVersion;
    public int LastNodeIndex;
}

/// <summary>
/// Stores the versioned room-visit state used by temporary reward activation and scaling hashes.
/// </summary>
public struct PlayerRoomRewardTemporaryState : IComponentData
{
    public uint Version;
    public uint LastVisitOrdinal;
}

/// <summary>
/// Stores one pending or active temporary scalable-stat modifier.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRoomRewardTemporaryModifierElement : IBufferElementData
{
    public FixedString64Bytes ModuleTechnicalId;
    public FixedString64Bytes TargetStatName;
    public FixedString512Bytes Formula;
    public FixedString64Bytes FlatTokenValue;
    public PlayerScalableStatType TargetStatType;
    public GameRoomRewardValueSource ValueSource;
    public float FlatNumericValue;
    public byte FlatBooleanValue;
    public uint ActiveFromVisitOrdinal;
    public uint ExpireAtVisitOrdinal;
    public uint GrantSequence;
    public int PresentationMappingIndex;
}

/// <summary>
/// Stores one temporary resource stipend granted on each covered distinct room visit.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRoomRewardTemporaryResourceElement : IBufferElementData
{
    public FixedString64Bytes ModuleTechnicalId;
    public FixedString512Bytes Formula;
    public GameRoomRewardResource Resource;
    public GameRoomRewardValueSource ValueSource;
    public float FlatNumericValue;
    public uint ActiveFromVisitOrdinal;
    public uint ExpireAtVisitOrdinal;
    public uint GrantSequence;
    public int PresentationMappingIndex;
}

/// <summary>
/// Stores one allocation-free presentation event shared by the player log and grant diagnostics.
/// </summary>
[InternalBufferCapacity(8)]
public struct PlayerRoomRewardPresentationEvent : IBufferElementData
{
    public FixedString64Bytes TargetStatName;
    public FixedString64Bytes TokenValue;
    public GameRoomRewardTargetDomain TargetDomain;
    public GameRoomRewardResource Resource;
    public GameRoomRewardValueSource ValueSource;
    public PlayerScalableStatType StatType;
    public float NumericDelta;
    public byte BooleanValue;
    public byte IsTemporary;
    public byte StartsNextRoom;
    public int DurationRooms;
    public int PresentationMappingIndex;
    public uint Sequence;
}
#endregion
