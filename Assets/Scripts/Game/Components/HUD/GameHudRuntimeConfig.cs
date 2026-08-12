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
    public byte SynchroMeterEnabled;
    public float4 SynchroBackgroundTint;
    public float4 SynchroCoverTint;
    public float4 SynchroPrimaryWaveTint;
    public float4 SynchroSecondaryWaveTint;
    public float4 SynchroRankTextColor;
    public float4 SynchroValueTextColor;
    public float4 SynchroProgressFillTint;
    public float4 SynchroProgressBackgroundTint;
    public byte SynchroShowBackground;
    public byte SynchroShowCover;
    public byte SynchroShowRankText;
    public byte SynchroShowValueText;
    public byte SynchroShowProgressBar;
    public float SynchroWaveScrollCyclesPerSecond;
    public float SynchroLowestRankPhaseOffsetNormalized;
    public float SynchroHighestRankPhaseOffsetNormalized;
    public float SynchroPhaseOffsetResponseExponent;
    public byte SynchroSingleRankAccelerateWavesWithProgress;
    public float SynchroSingleRankMaximumWaveScrollCyclesPerSecond;
    public GameHudSynchroSingleRankConvergenceMode SynchroSingleRankConvergenceMode;
    public float SynchroSingleRankInitialPhaseOffsetNormalized;
    public float SynchroSingleRankFinalPhaseOffsetNormalized;
    public float SynchroSingleRankConvergenceStartProgressPercent;
    public float SynchroSingleRankConvergenceEndProgressPercent;
    public int SynchroSingleRankConvergenceStepCount;
    public float SynchroPhaseTransitionDuration;
    public byte SynchroUseUnscaledTime;
    public float SynchroProgressSmoothingSeconds;
    public byte SynchroHideWhenPlayerMissing;
    public byte SynchroHideWhenZeroValue;
    public byte SynchroHideWhenNoActiveRank;
    public float SynchroFadeInDuration;
    public float SynchroFadeOutDuration;
    public FixedString64Bytes SynchroIdleRankLabel;
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
