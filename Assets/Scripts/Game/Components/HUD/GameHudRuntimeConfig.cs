using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Singleton runtime configuration baked from GameHudManagerPreset for managed HUD scene sections.
/// </summary>
public struct GameHudRuntimeConfig : IComponentData
{
    #region Fields
    public byte HideLevelTextWhenPlayerMissing;
    public float ExperienceBarSmoothingSeconds;
    public byte HideExperienceBarWhenPlayerMissing;
    public byte EnableLegacyExperienceLiquidShader;
    public byte EnableLegacyExperiencePiston;
    public float LegacyExperiencePistonLocalOffsetX;
    public float LegacyExperiencePistonLocalOffsetY;
    public byte EnableLegacyExperienceValueDeltaMotion;
    public float LegacyExperienceDeltaTriggerThreshold;
    public float LegacyExperienceDeltaMotionStrength;
    public float LegacyExperienceDeltaMotionDecaySeconds;
    public float EnergyBarSmoothingSeconds;
    public byte HideEnergyBarsWhenPlayerMissing;
    public byte HideEnergyBarsWhenModuleMissing;
    public float ChargeBarSmoothingSeconds;
    public byte HideChargeBarsWhenPlayerMissing;
    public byte HideChargeBarsWhenModuleMissing;
    public byte RunTimerEnabled;
    public PlayerRunTimerDirection RunTimerDirection;
    public float RunTimerInitialSeconds;
    public byte RunTimerHideWhenPlayerMissing;
    public byte ComboCounterEnabled;
    public float4 ComboDefaultBadgeTint;
    public float4 ComboDefaultRankTextColor;
    public float4 ComboDefaultValueTextColor;
    public float4 ComboDefaultProgressFillColor;
    public float4 ComboDefaultProgressBackgroundColor;
    public byte ComboShowRankBadgeImage;
    public byte ComboShowProgressBar;
    public byte ComboHideWhenPlayerMissing;
    public byte ComboHideWhenZero;
    public byte ComboHideWhenNoActiveRank;
    public float ComboFadeInDuration;
    public float ComboFadeOutDuration;
    public FixedString64Bytes ComboIdleRankLabel;
    public byte MilestoneHideOptionTitleNumbers;
    public FixedString64Bytes MilestoneSkipHoldFillImageName;
    public byte MilestoneConfigureSkipHoldFillImage;
    public byte MilestoneAutoDiscoverOptionViewsFromPanelRoot;
    public float MilestoneNavigationInputDeadzone;
    public float MilestoneNavigationRepeatCooldownSeconds;
    public byte MilestoneWrapNavigation;
    public byte MilestoneFollowPointerHoverSelection;
    public byte MilestoneSuspendEventSystemNavigation;
    public byte MilestoneAutoSelectFirstOfferWhenUiMissing;
    public byte MilestoneLockButtonsAfterSelectionClick;
    public byte DamageVignetteEnabled;
    public byte DamageVignetteHideWhenPlayerMissing;
    #endregion
}
