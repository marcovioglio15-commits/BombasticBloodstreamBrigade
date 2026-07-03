using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Converts GameHudManagerPreset assets into compact ECS runtime HUD config components.
/// </summary>
public static class GameHudManagerPresetBakeUtility
{
    #region Constants
    private const float DefaultExperienceSmoothingSeconds = 0.08f;
    private const float DefaultEnergySmoothingSeconds = 0.08f;
    private const float DefaultChargeSmoothingSeconds = 0.05f;
    private const float DefaultRunTimerInitialSeconds = 450f;
    private const float DefaultLegacyExperienceDeltaThreshold = 0.0125f;
    private const float DefaultLegacyExperienceDeltaStrength = 0.9f;
    private const float DefaultLegacyExperienceDeltaDecaySeconds = 0.3f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the runtime HUD config from the authored preset, using project defaults when the preset is missing.
    /// </summary>
    /// <param name="preset">Source HUD preset, or null to bake default values.</param>
    /// <returns>Runtime HUD config component used by managed scene HUD sections.</returns>
    public static GameHudRuntimeConfig BuildConfig(GameHudManagerPreset preset)
    {
        GameHudLevelExperienceSettings levelExperienceSettings = preset != null ? preset.LevelExperienceSettings : null;
        GameHudActivePowerUpSettings activePowerUpSettings = preset != null ? preset.ActivePowerUpSettings : null;
        GameHudRunTimerSettings runTimerSettings = preset != null ? preset.RunTimerSettings : null;
        GameHudComboCounterSettings comboCounterSettings = preset != null ? preset.ComboCounterSettings : null;
        GameHudMilestoneSelectionSettings milestoneSelectionSettings = preset != null ? preset.MilestoneSelectionSettings : null;
        GameHudDamageVignetteSettings damageVignetteSettings = preset != null ? preset.DamageVignetteSettings : null;

        return new GameHudRuntimeConfig
        {
            HideLevelTextWhenPlayerMissing = ToByte(levelExperienceSettings == null || levelExperienceSettings.HideLevelTextWhenPlayerMissing),
            ExperienceBarSmoothingSeconds = math.max(0f, levelExperienceSettings != null ? levelExperienceSettings.ExperienceBarSmoothingSeconds : DefaultExperienceSmoothingSeconds),
            HideExperienceBarWhenPlayerMissing = ToByte(levelExperienceSettings == null || levelExperienceSettings.HideExperienceBarWhenPlayerMissing),
            EnableLegacyExperienceLiquidShader = ToByte(levelExperienceSettings == null || levelExperienceSettings.EnableLegacyExperienceLiquidShader),
            EnableLegacyExperiencePiston = ToByte(levelExperienceSettings != null && levelExperienceSettings.EnableLegacyExperiencePiston),
            LegacyExperiencePistonLocalOffsetX = levelExperienceSettings != null ? levelExperienceSettings.LegacyExperiencePistonLocalOffsetX : 0f,
            LegacyExperiencePistonLocalOffsetY = levelExperienceSettings != null ? levelExperienceSettings.LegacyExperiencePistonLocalOffsetY : 0f,
            EnableLegacyExperienceValueDeltaMotion = ToByte(levelExperienceSettings == null || levelExperienceSettings.EnableLegacyExperienceValueDeltaMotion),
            LegacyExperienceDeltaTriggerThreshold = math.max(0f, levelExperienceSettings != null ? levelExperienceSettings.LegacyExperienceDeltaTriggerThreshold : DefaultLegacyExperienceDeltaThreshold),
            LegacyExperienceDeltaMotionStrength = math.max(0f, levelExperienceSettings != null ? levelExperienceSettings.LegacyExperienceDeltaMotionStrength : DefaultLegacyExperienceDeltaStrength),
            LegacyExperienceDeltaMotionDecaySeconds = math.max(0f, levelExperienceSettings != null ? levelExperienceSettings.LegacyExperienceDeltaMotionDecaySeconds : DefaultLegacyExperienceDeltaDecaySeconds),
            EnergyBarSmoothingSeconds = math.max(0f, activePowerUpSettings != null ? activePowerUpSettings.EnergyBarSmoothingSeconds : DefaultEnergySmoothingSeconds),
            HideEnergyBarsWhenPlayerMissing = ToByte(activePowerUpSettings == null || activePowerUpSettings.HideEnergyBarsWhenPlayerMissing),
            HideEnergyBarsWhenModuleMissing = ToByte(activePowerUpSettings == null || activePowerUpSettings.HideEnergyBarsWhenModuleMissing),
            ChargeBarSmoothingSeconds = math.max(0f, activePowerUpSettings != null ? activePowerUpSettings.ChargeBarSmoothingSeconds : DefaultChargeSmoothingSeconds),
            HideChargeBarsWhenPlayerMissing = ToByte(activePowerUpSettings == null || activePowerUpSettings.HideChargeBarsWhenPlayerMissing),
            HideChargeBarsWhenModuleMissing = ToByte(activePowerUpSettings == null || activePowerUpSettings.HideChargeBarsWhenModuleMissing),
            RunTimerEnabled = ToByte(runTimerSettings == null || runTimerSettings.IsEnabled),
            RunTimerDirection = runTimerSettings != null ? runTimerSettings.Direction : PlayerRunTimerDirection.Backward,
            RunTimerInitialSeconds = math.max(0f, runTimerSettings != null ? runTimerSettings.InitialSeconds : DefaultRunTimerInitialSeconds),
            RunTimerHideWhenPlayerMissing = ToByte(runTimerSettings == null || runTimerSettings.HideWhenPlayerMissing),
            ComboCounterEnabled = ToByte(comboCounterSettings == null || comboCounterSettings.IsEnabled),
            ComboDefaultBadgeTint = ToFloat4(comboCounterSettings != null ? comboCounterSettings.DefaultBadgeTint : Color.white),
            ComboDefaultRankTextColor = ToFloat4(comboCounterSettings != null ? comboCounterSettings.DefaultRankTextColor : Color.white),
            ComboDefaultValueTextColor = ToFloat4(comboCounterSettings != null ? comboCounterSettings.DefaultComboValueTextColor : Color.white),
            ComboDefaultProgressFillColor = ToFloat4(comboCounterSettings != null ? comboCounterSettings.DefaultProgressFillColor : Color.white),
            ComboDefaultProgressBackgroundColor = ToFloat4(comboCounterSettings != null ? comboCounterSettings.DefaultProgressBackgroundColor : new Color(1f, 1f, 1f, 0.25f)),
            ComboShowRankBadgeImage = ToByte(comboCounterSettings == null || comboCounterSettings.ShowRankBadgeImage),
            ComboShowProgressBar = ToByte(comboCounterSettings == null || comboCounterSettings.ShowProgressBar),
            ComboHideWhenPlayerMissing = ToByte(comboCounterSettings == null || comboCounterSettings.HideWhenPlayerMissing),
            ComboHideWhenZero = ToByte(comboCounterSettings == null || comboCounterSettings.HideWhenZeroCombo),
            ComboHideWhenNoActiveRank = ToByte(comboCounterSettings == null || comboCounterSettings.HideWhenNoActiveRank),
            ComboFadeInDuration = math.max(0f, comboCounterSettings != null ? comboCounterSettings.FadeInDuration : 0.18f),
            ComboFadeOutDuration = math.max(0f, comboCounterSettings != null ? comboCounterSettings.FadeOutDuration : 0.18f),
            ComboIdleRankLabel = new FixedString64Bytes(ResolveString(comboCounterSettings != null ? comboCounterSettings.IdleRankLabel : null, "COMBO")),
            MilestoneHideOptionTitleNumbers = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.HideOptionTitleNumbers),
            MilestoneSkipHoldFillImageName = new FixedString64Bytes(ResolveString(milestoneSelectionSettings != null ? milestoneSelectionSettings.SkipHoldFillImageName : null, "SkipHoldFill")),
            MilestoneConfigureSkipHoldFillImage = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.ConfigureSkipHoldFillImage),
            MilestoneAutoDiscoverOptionViewsFromPanelRoot = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.AutoDiscoverOptionViewsFromPanelRoot),
            MilestoneNavigationInputDeadzone = math.clamp(milestoneSelectionSettings != null ? milestoneSelectionSettings.NavigationInputDeadzone : 0.5f, 0f, 1f),
            MilestoneNavigationRepeatCooldownSeconds = math.max(0f, milestoneSelectionSettings != null ? milestoneSelectionSettings.NavigationRepeatCooldownSeconds : 0.15f),
            MilestoneWrapNavigation = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.WrapNavigation),
            MilestoneFollowPointerHoverSelection = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.FollowPointerHoverSelection),
            MilestoneSuspendEventSystemNavigation = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.SuspendEventSystemNavigationWhileSelectionActive),
            MilestoneAutoSelectFirstOfferWhenUiMissing = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.AutoSelectFirstOfferWhenUiMissing),
            MilestoneLockButtonsAfterSelectionClick = ToByte(milestoneSelectionSettings == null || milestoneSelectionSettings.LockButtonsAfterSelectionClick),
            DamageVignetteEnabled = ToByte(damageVignetteSettings == null || damageVignetteSettings.IsEnabled),
            DamageVignetteHideWhenPlayerMissing = ToByte(damageVignetteSettings == null || damageVignetteSettings.HideWhenPlayerMissing)
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts a Boolean setting into the byte representation used by ECS components.
    /// </summary>
    /// <param name="value">Boolean value to convert.</param>
    /// <returns>One when true, otherwise zero.</returns>
    private static byte ToByte(bool value)
    {
        return value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Converts a Unity color into the math representation used by ECS components.
    /// </summary>
    /// <param name="color">Authored color value.</param>
    /// <returns>Float4 value with RGBA channels.</returns>
    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }

    /// <summary>
    /// Resolves a non-empty string with a fallback for fixed-string config fields.
    /// </summary>
    /// <param name="value">Authored string value.</param>
    /// <param name="fallback">Fallback used when the authored value is empty.</param>
    /// <returns>Resolved string value.</returns>
    private static string ResolveString(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value;
    }
    #endregion

    #endregion
}
