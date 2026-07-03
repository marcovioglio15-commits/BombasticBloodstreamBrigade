using System.Collections.Generic;

/// <summary>
/// Produces non-mutating validation warnings for GameHudManagerPreset assets.
/// </summary>
public static class GameHudManagerPresetValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects warnings describing values that may lead to invalid gameplay HUD behavior.
    /// </summary>
    /// <param name="preset">HUD manager preset to inspect.</param>
    /// <param name="warnings">Mutable list that receives warning text.</param>
    public static void CollectWarnings(GameHudManagerPreset preset, List<string> warnings)
    {
        if (warnings == null)
            return;

        warnings.Clear();

        if (preset == null)
        {
            warnings.Add("HUD Manager preset is missing.");
            return;
        }

        ValidateLevelExperienceSettings(preset.LevelExperienceSettings, warnings);
        ValidateActivePowerUpSettings(preset.ActivePowerUpSettings, warnings);
        ValidateRunTimerSettings(preset.RunTimerSettings, warnings);
        ValidateComboCounterSettings(preset.ComboCounterSettings, warnings);
        ValidateMilestoneSelectionSettings(preset.MilestoneSelectionSettings, warnings);
        ValidateDamageVignetteSettings(preset.DamageVignetteSettings, warnings);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Validates level label and legacy experience bar settings.
    /// </summary>
    /// <param name="settings">Level and experience settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateLevelExperienceSettings(GameHudLevelExperienceSettings settings, List<string> warnings)
    {
        if (settings == null)
        {
            warnings.Add("Level & Experience settings are missing.");
            return;
        }

        ValidateNonNegative(settings.ExperienceBarSmoothingSeconds, "Experience Bar Smoothing Seconds", warnings);
        ValidateNonNegative(settings.LegacyExperienceDeltaTriggerThreshold, "Legacy Experience Delta Trigger Threshold", warnings);
        ValidateNonNegative(settings.LegacyExperienceDeltaMotionStrength, "Legacy Experience Delta Motion Strength", warnings);
        ValidateNonNegative(settings.LegacyExperienceDeltaMotionDecaySeconds, "Legacy Experience Delta Motion Decay Seconds", warnings);
    }

    /// <summary>
    /// Validates active power-up fallback settings.
    /// </summary>
    /// <param name="settings">Active power-up HUD settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateActivePowerUpSettings(GameHudActivePowerUpSettings settings, List<string> warnings)
    {
        if (settings == null)
        {
            warnings.Add("Active Power-Up settings are missing.");
            return;
        }

        ValidateNonNegative(settings.EnergyBarSmoothingSeconds, "Energy Bar Smoothing Seconds", warnings);
        ValidateNonNegative(settings.ChargeBarSmoothingSeconds, "Charge Bar Smoothing Seconds", warnings);
    }

    /// <summary>
    /// Validates run timer settings.
    /// </summary>
    /// <param name="settings">Run timer settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateRunTimerSettings(GameHudRunTimerSettings settings, List<string> warnings)
    {
        if (settings == null)
        {
            warnings.Add("Run Timer settings are missing.");
            return;
        }

        ValidateNonNegative(settings.InitialSeconds, "Run Timer Initial Seconds", warnings);
    }

    /// <summary>
    /// Validates combo counter visibility and fade settings.
    /// </summary>
    /// <param name="settings">Combo counter settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateComboCounterSettings(GameHudComboCounterSettings settings, List<string> warnings)
    {
        if (settings == null)
        {
            warnings.Add("Combo Counter settings are missing.");
            return;
        }

        ValidateNonNegative(settings.FadeInDuration, "Combo Fade In Duration", warnings);
        ValidateNonNegative(settings.FadeOutDuration, "Combo Fade Out Duration", warnings);

        if (string.IsNullOrWhiteSpace(settings.IdleRankLabel))
            warnings.Add("Combo Idle Rank Label is empty. Runtime fallback will display COMBO.");
    }

    /// <summary>
    /// Validates milestone selection navigation settings.
    /// </summary>
    /// <param name="settings">Milestone selection settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateMilestoneSelectionSettings(GameHudMilestoneSelectionSettings settings, List<string> warnings)
    {
        if (settings == null)
        {
            warnings.Add("Milestone Selection settings are missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SkipHoldFillImageName))
            warnings.Add("Milestone Skip Hold Fill Image Name is empty. Runtime auto-discovery will skip named lookup.");

        if (settings.NavigationInputDeadzone < 0f || settings.NavigationInputDeadzone > 1f)
            warnings.Add("Milestone Navigation Input Deadzone is outside the 0..1 range. Runtime will clamp the value.");

        ValidateNonNegative(settings.NavigationRepeatCooldownSeconds, "Milestone Navigation Repeat Cooldown Seconds", warnings);
    }

    /// <summary>
    /// Validates damage vignette settings.
    /// </summary>
    /// <param name="settings">Damage vignette settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateDamageVignetteSettings(GameHudDamageVignetteSettings settings, List<string> warnings)
    {
        if (settings == null)
            warnings.Add("Damage Vignette settings are missing.");
    }

    /// <summary>
    /// Adds a warning when a scalar is below zero without mutating the authored value.
    /// </summary>
    /// <param name="value">Authored scalar value.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateNonNegative(float value, string label, List<string> warnings)
    {
        if (value < 0f)
            warnings.Add(label + " is negative. Runtime will use a safe non-negative fallback.");
    }
    #endregion

    #endregion
}
