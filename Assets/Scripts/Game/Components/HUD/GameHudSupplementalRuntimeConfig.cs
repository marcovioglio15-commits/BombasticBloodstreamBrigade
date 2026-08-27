using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores the baked layout and presentation settings for the preauthored power-up summary HUD section.
/// </summary>
public struct GamePowerUpSummaryRuntimeConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public byte StartsExpanded;
    public byte HideWhenPlayerMissing;
    public GameHudSummaryPanelSide PanelSide;
    public GameHudSummaryPowerUpOrder PowerUpOrder;
    public GameHudSummaryPowerUpVisibility PowerUpVisibility;
    public float ExpandedWidth;
    public float CollapsedHandleWidth;
    public float ContentPadding;
    public float PowerUpColumnSpacing;
    public float SectionSpacing;
    public float PowerUpAreaHeightNormalized;
    public float SlideDurationSeconds;
    public GameHudSummarySlideEasing SlideEasing;
    public byte UseUnscaledTime;
    public byte EnableInputToggle;
    public FixedString64Bytes ToggleActionId;
    public byte EnableClickToggle;
    public int MaximumVisibleActivePowerUps;
    public int MaximumVisiblePassivePowerUps;
    public float IconSize;
    public float IconSpacing;
    public float4 IconTint;
    public UnityObjectRef<Sprite> IconBackgroundSprite;
    public float4 IconBackgroundTint;
    public byte HideEmptyActiveColumn;
    public byte HideEmptyPassiveColumn;
    public UnityObjectRef<TMP_FontAsset> CounterFont;
    public float CounterFontSize;
    public float4 CounterColor;
    public FixedString64Bytes CounterPrefix;
    public byte ShowSingleCollectionCount;
    public FixedString64Bytes ActiveTitle;
    public FixedString64Bytes PassiveTitle;
    public FixedString64Bytes StatisticsTitle;
    public UnityObjectRef<TMP_FontAsset> TitleFont;
    public float TitleFontSize;
    public float4 TitleColor;
    public byte ShowPowerUpColumnSeparator;
    public byte ShowStatisticsSeparator;
    public float4 SeparatorColor;
    public float SeparatorThickness;
    public UnityObjectRef<Sprite> BackgroundSprite;
    public float4 BackgroundTint;
    public UnityObjectRef<Sprite> ToggleSprite;
    public float4 ToggleTint;
    public float StatisticRefreshIntervalSeconds;
    #endregion
}

/// <summary>
/// Stores the baked presentation, terminal override, and audio values consumed by the preauthored room-clear announcement.
/// </summary>
public struct GameHudWaveClearAnnouncementRuntimeConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public FixedString512Bytes Content;
    public byte PlayAudioEvent;
    public GameAudioEventId AudioEventId;
    public GameHudWaveClearAnnouncementDirection Direction;
    public float TraversalDurationSeconds;
    public GameHudWaveClearAnnouncementEasing Easing;
    public byte PauseAtCenter;
    public float CenterHoldDurationSeconds;
    public byte UseUnscaledTime;
    public byte UseFinalWaveOverride;
    public FixedString512Bytes FinalWaveContent;
    public GameHudWaveClearAnnouncementDirection FinalWaveDirection;
    public float FinalWaveTraversalDurationSeconds;
    public GameHudWaveClearAnnouncementEasing FinalWaveEasing;
    public byte FinalWavePauseAtCenter;
    public float FinalWaveCenterHoldDurationSeconds;
    public byte PlayFinalWaveAudioEvent;
    public GameAudioEventId FinalWaveAudioEventId;
    public float VerticalPositionNormalized;
    public float HorizontalOffscreenPadding;
    public UnityObjectRef<TMP_FontAsset> Font;
    public float FontSize;
    public int FontStyle;
    public float4 Color;
    #endregion
}

/// <summary>
/// Coordinates ECS room-clear requests, managed presentation completion, room-change cancellation, and victory-menu gating.
/// </summary>
public struct GameHudWaveClearAnnouncementPresentationState : IComponentData
{
    #region Fields
    public uint RequestedVersion;
    public uint CompletedVersion;
    public uint GenerationVersion;
    public int NodeIndex;
    public byte Pending;
    public byte Active;
    public byte IsFinalWave;
    public byte BlocksVictoryMenu;
    #endregion
}

/// <summary>
/// Stores one ordered player-stat row baked from the inline HUD summary settings.
/// </summary>
[InternalBufferCapacity(0)]
public struct GamePowerUpSummaryStatisticElement : IBufferElementData
{
    #region Fields
    public GameHudPlayerStatistic Statistic;
    public FixedString64Bytes ScalableStatName;
    public FixedString64Bytes Label;
    public GameHudStatisticValueFormat ValueFormat;
    public int DecimalPlaces;
    public float DisplayMultiplier;
    public FixedString64Bytes Suffix;
    public byte ShowLabel;
    public FixedString64Bytes TrueText;
    public FixedString64Bytes FalseText;
    public UnityObjectRef<TMP_FontAsset> Font;
    public float FontSize;
    public int FontStyle;
    public float4 Color;
    #endregion
}

/// <summary>
/// Stores baked Input Action-only navigation settings for the reusable Settings menu.
/// </summary>
public struct GameHudSettingsNavigationRuntimeConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public byte WrapTabs;
    public FixedString64Bytes PreviousTabActionId;
    public FixedString64Bytes NextTabActionId;
    public FixedString64Bytes VerticalNavigationActionId;
    public FixedString64Bytes HorizontalNavigationActionId;
    public FixedString64Bytes SubmitActionId;
    public FixedString64Bytes CancelActionId;
    public byte IncludeDropdownHeadersInNavigation;
    public byte CustomizeSelectionPresentation;
    public byte OverrideSelectionGraphicColors;
    public float4 UnselectedGraphicColor;
    public float4 SelectedGraphicColor;
    public byte OverrideSelectionTextStyle;
    public float4 UnselectedTextColor;
    public float4 SelectedTextColor;
    public int UnselectedFontStyle;
    public int SelectedFontStyle;
    public byte OverrideSelectionScale;
    public float3 UnselectedScale;
    public float3 SelectedScale;
    public byte ShowSelectionOutline;
    public float4 SelectionOutlineColor;
    public float2 SelectionOutlineDistance;
    public float InputDeadzone;
    public float RepeatDelaySeconds;
    public float RepeatIntervalSeconds;
    #endregion
}

/// <summary>
/// Stores one independently selectable menu-button interaction profile and all referenced presentation assets.
/// </summary>
[InternalBufferCapacity(0)]
public struct GameUiMenuButtonInteractionElement : IBufferElementData
{
    #region Fields
    public GameUiMenuKind MenuKind;
    public byte Enabled;
    public GameUiButtonMotionMode MotionMode;
    public float TransitionDurationSeconds;
    public byte UseUnscaledTime;
    public GameUiButtonHoverTransformMode HoverTransformMode;
    public float HoverPulseCycleSeconds;
    public int HoverPulseCycles;
    public byte LoopHoverPulse;
    public float3 HoverScale;
    public float3 HoverPositionOffset;
    public float3 HoverRotationOffset;
    public float3 PressedScale;
    public float3 PressedPositionOffset;
    public float3 PressedRotationOffset;
    public UnityObjectRef<AnimationClip> NormalClip;
    public UnityObjectRef<AnimationClip> HoverClip;
    public UnityObjectRef<AnimationClip> PressedClip;
    public UnityObjectRef<AnimationClip> DisabledClip;
    public byte OverrideSprites;
    public byte AllowEmptySprites;
    public UnityObjectRef<Sprite> NormalSprite;
    public UnityObjectRef<Sprite> HoverSprite;
    public UnityObjectRef<Sprite> PressedSprite;
    public UnityObjectRef<Sprite> DisabledSprite;
    public byte OverrideGraphicColors;
    public float4 NormalGraphicColor;
    public float4 HoverGraphicColor;
    public float4 PressedGraphicColor;
    public float4 DisabledGraphicColor;
    public byte OverrideTextStyle;
    public UnityObjectRef<TMP_FontAsset> NormalFont;
    public UnityObjectRef<TMP_FontAsset> EmphasizedFont;
    public float NormalFontSize;
    public float EmphasizedFontSize;
    public int NormalFontStyle;
    public int EmphasizedFontStyle;
    public float4 NormalTextColor;
    public float4 HoverTextColor;
    public float4 PressedTextColor;
    public float4 DisabledTextColor;
    #endregion
}
