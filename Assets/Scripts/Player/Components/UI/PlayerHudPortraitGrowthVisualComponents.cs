using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Points from the player entity to its dedicated HUD portrait configuration entity.
/// </summary>
public struct PlayerPortraitHudVisualReference : IComponentData
{
    public Entity ConfigEntity;
}

/// <summary>
/// Points from the HUD portrait configuration entity back to the authoritative player entity.
/// </summary>
public struct PlayerPortraitHudVisualOwner : IComponentData
{
    public Entity PlayerEntity;
}

/// <summary>
/// Stores global switches for the player portrait HUD presentation.
/// </summary>
public struct PlayerPortraitHudVisualConfig : IComponentData
{
    public byte Enabled;
    public byte HideWhenPlayerMissing;
}

/// <summary>
/// Stores the immutable player portrait HUD baseline used by runtime scaling rebuilds.
/// </summary>
public struct PlayerBasePortraitHudVisualConfig : IComponentData
{
    public PlayerPortraitHudVisualConfig Config;
}

/// <summary>
/// Tracks the unified runtime scaling hash last applied to the portrait HUD visual configuration.
/// </summary>
public struct PlayerPortraitHudVisualScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores one runtime portrait animation entry and its frame range inside the shared frame buffer.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPortraitHudAnimationElement : IBufferElementData
{
    public int AnimationId;
    public PlayerPortraitHudAnimationRole Role;
    public FixedString64Bytes TriggerKey;
    public int FrameStartIndex;
    public int FrameCount;
    public float SecondsPerFrame;
    public float PlaybackSpeedMultiplier;
    public PlayerPortraitHudPlaybackMode PlaybackMode;
    public int Priority;
    public byte RestartWhenReentered;
}

/// <summary>
/// Stores one immutable portrait animation baseline entry used by runtime scaling rebuilds.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBasePortraitHudAnimationElement : IBufferElementData
{
    public int AnimationId;
    public PlayerPortraitHudAnimationRole Role;
    public FixedString64Bytes TriggerKey;
    public int FrameStartIndex;
    public int FrameCount;
    public float SecondsPerFrame;
    public float PlaybackSpeedMultiplier;
    public PlayerPortraitHudPlaybackMode PlaybackMode;
    public int Priority;
    public byte RestartWhenReentered;
}

/// <summary>
/// Stores one portrait sprite frame referenced by a baked portrait animation.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerPortraitHudFrameElement : IBufferElementData
{
    public int AnimationId;
    public UnityObjectRef<Sprite> Sprite;
}

/// <summary>
/// Stores one Portrait section scaling rule baked from Player Visual Preset Add Scaling authoring data.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimePortraitHudVisualScalingElement : IBufferElementData
{
    public FixedString128Bytes PayloadPath;
    public int AnimationId;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}

/// <summary>
/// Points from the player entity to its dedicated HUD growth-sequence configuration entity.
/// </summary>
public struct PlayerGrowthSequenceHudVisualReference : IComponentData
{
    public Entity ConfigEntity;
}

/// <summary>
/// Points from the HUD growth-sequence configuration entity back to the authoritative player entity.
/// </summary>
public struct PlayerGrowthSequenceHudVisualOwner : IComponentData
{
    public Entity PlayerEntity;
}

/// <summary>
/// Stores global switches for the player growth-sequence HUD presentation.
/// </summary>
public struct PlayerGrowthSequenceHudVisualConfig : IComponentData
{
    public byte Enabled;
    public byte HideWhenPlayerMissing;
    public int MaximumVisibleSteps;
}

/// <summary>
/// Stores the immutable growth-sequence HUD baseline used by runtime scaling rebuilds.
/// </summary>
public struct PlayerBaseGrowthSequenceHudVisualConfig : IComponentData
{
    public PlayerGrowthSequenceHudVisualConfig Config;
}

/// <summary>
/// Tracks the unified runtime scaling hash last applied to the growth-sequence HUD visual configuration.
/// </summary>
public struct PlayerGrowthSequenceHudVisualScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}

/// <summary>
/// Stores one runtime HUD growth-sequence visual step.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerGrowthSequenceHudStepVisualElement : IBufferElementData
{
    public FixedString64Bytes ScheduleId;
    public int StepIndex;
    public FixedString64Bytes StatName;
    public FixedString128Bytes Text;
    public PlayerGrowthSequenceHudPresentationMode PresentationMode;
    public UnityObjectRef<Sprite> NextSprite;
    public UnityObjectRef<Sprite> NormalSprite;
    public UnityObjectRef<TMP_FontAsset> NextFontAsset;
    public UnityObjectRef<TMP_FontAsset> NormalFontAsset;
    public float NextFontSize;
    public float NormalFontSize;
    public float4 NextColor;
    public float4 NormalColor;
    public float4 NextOutlineColor;
    public float4 NormalOutlineColor;
    public float NextOutlineWidth;
    public float NormalOutlineWidth;
}

/// <summary>
/// Stores one immutable HUD growth-sequence visual step used by runtime scaling rebuilds.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseGrowthSequenceHudStepVisualElement : IBufferElementData
{
    public FixedString64Bytes ScheduleId;
    public int StepIndex;
    public FixedString64Bytes StatName;
    public FixedString128Bytes Text;
    public PlayerGrowthSequenceHudPresentationMode PresentationMode;
    public UnityObjectRef<Sprite> NextSprite;
    public UnityObjectRef<Sprite> NormalSprite;
    public UnityObjectRef<TMP_FontAsset> NextFontAsset;
    public UnityObjectRef<TMP_FontAsset> NormalFontAsset;
    public float NextFontSize;
    public float NormalFontSize;
    public float4 NextColor;
    public float4 NormalColor;
    public float4 NextOutlineColor;
    public float4 NormalOutlineColor;
    public float NextOutlineWidth;
    public float NormalOutlineWidth;
}

/// <summary>
/// Stores one Growth Sequence section scaling rule baked from Player Visual Preset Add Scaling authoring data.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeGrowthSequenceHudVisualScalingElement : IBufferElementData
{
    public FixedString128Bytes PayloadPath;
    public FixedString64Bytes ScheduleId;
    public int StepIndex;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString64Bytes BaseTokenValue;
    public FixedString512Bytes Formula;
}
