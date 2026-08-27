using System.Collections.Generic;
using System.Text;
using Unity.Collections;

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
        ValidateSynchroMeterSettings(preset.SynchroMeterSettings, warnings);
        ValidateMilestoneSelectionSettings(preset.MilestoneSelectionSettings, warnings);
        ValidateDamageVignetteSettings(preset.DamageVignetteSettings, warnings);
        GameHudSupplementalPresetValidationUtility.ValidatePowerUpSummary(preset.PowerUpSummarySettings, warnings);
        GameHudSupplementalPresetValidationUtility.ValidateWaveClearAnnouncement(
            preset.WaveClearAnnouncementSettings,
            warnings);
        GameHudSupplementalPresetValidationUtility.ValidateButtonInteractions(preset.ButtonInteractionSettings, warnings);
        GameHudSupplementalPresetValidationUtility.ValidateSettingsNavigation(preset.SettingsNavigationSettings, warnings);
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
    /// Validates Synchro Meter wave, phase, visibility, and fade settings.
    /// </summary>
    /// <param name="settings">Synchro Meter settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateSynchroMeterSettings(GameHudSynchroMeterSettings settings, List<string> warnings)
    {
        if (settings == null)
        {
            warnings.Add("Synchro Meter settings are missing.");
            return;
        }

        ValidateNonNegative(settings.WaveScrollCyclesPerSecond, "Synchro Wave Scroll Cycles Per Second", warnings);
        ValidateNormalized(settings.LowestRankPhaseOffsetNormalized, "Synchro Lowest Rank Phase Offset", warnings);
        ValidateNormalized(settings.HighestRankPhaseOffsetNormalized, "Synchro Highest Rank Phase Offset", warnings);
        ValidatePositive(settings.PhaseOffsetResponseExponent, "Synchro Phase Offset Response Exponent", warnings);
        ValidateNonNegative(settings.SingleRankMaximumWaveScrollCyclesPerSecond, "Synchro Single Rank Maximum Wave Scroll Cycles Per Second", warnings);
        ValidateNormalized(settings.SingleRankInitialPhaseOffsetNormalized, "Synchro Single Rank Initial Phase Offset", warnings);
        ValidateNormalized(settings.SingleRankFinalPhaseOffsetNormalized, "Synchro Single Rank Final Phase Offset", warnings);
        ValidatePercentage(settings.SingleRankConvergenceStartProgressPercent, "Synchro Single Rank Convergence Start Progress Percent", warnings);
        ValidatePercentage(settings.SingleRankConvergenceEndProgressPercent, "Synchro Single Rank Convergence End Progress Percent", warnings);
        ValidateNonNegative(settings.PhaseTransitionDuration, "Synchro Phase Transition Duration", warnings);
        ValidateNonNegative(settings.ProgressSmoothingSeconds, "Synchro Progress Smoothing Seconds", warnings);
        ValidateNonNegative(settings.FadeInDuration, "Synchro Fade In Duration", warnings);
        ValidateNonNegative(settings.FadeOutDuration, "Synchro Fade Out Duration", warnings);

        switch (settings.VisualMode)
        {
            case GameHudSynchroMeterVisualMode.Standard:
                break;
            case GameHudSynchroMeterVisualMode.ProgressionText:
                ValidateSynchroProgressionTextFormat(settings.ProgressionTextFormat, warnings);
                ValidateSynchroProgressionTextLayout(settings, warnings);
                break;
            default:
                warnings.Add("Synchro Visual Mode is unsupported. Runtime will use the standard overlay composition.");
                break;
        }

        if (settings.HighestRankPhaseOffsetNormalized > settings.LowestRankPhaseOffsetNormalized)
            warnings.Add("Synchro Highest Rank Phase Offset exceeds the Lowest Rank value, so higher ranks will diverge instead of converging.");

        if (settings.SingleRankFinalPhaseOffsetNormalized > settings.SingleRankInitialPhaseOffsetNormalized)
            warnings.Add("Synchro Single Rank Final Phase Offset exceeds its Initial Phase Offset, so progression will make the waves diverge instead of converge.");

        if (settings.SingleRankFinalPhaseOffsetNormalized > 0f)
            warnings.Add("Synchro Single Rank Final Phase Offset is above 0, so the waves will retain separation at full progression instead of overlapping.");

        if (settings.SingleRankConvergenceEndProgressPercent <= settings.SingleRankConvergenceStartProgressPercent)
            warnings.Add("Synchro Single Rank Convergence End Progress Percent should be greater than its Start Progress Percent.");

        if (settings.SingleRankConvergenceMode == GameHudSynchroSingleRankConvergenceMode.Steps &&
            settings.SingleRankConvergenceStepCount <= 0)
            warnings.Add("Synchro Single Rank Convergence Step Count should be > 0 while Steps mode is selected.");

        if (settings.SingleRankAccelerateWavesWithProgress &&
            settings.SingleRankMaximumWaveScrollCyclesPerSecond < settings.WaveScrollCyclesPerSecond)
            warnings.Add("Synchro Single Rank Maximum Wave Scroll Cycles Per Second is below the base speed, so progression will slow the waves instead of accelerating them.");

        if (string.IsNullOrWhiteSpace(settings.IdleRankLabel))
            warnings.Add("Synchro Idle Rank Label is empty. Runtime fallback will display SYNCHRO.");
    }

    /// <summary>
    /// Validates the optional progression-label format without changing the authored text.
    /// </summary>
    /// <param name="format">Authored label format inspected for token and ECS storage compatibility.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateSynchroProgressionTextFormat(string format, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            warnings.Add("Synchro Progression Text Format is empty. Runtime will use the default format.");
            return;
        }

        if (Encoding.UTF8.GetByteCount(format) > FixedString512Bytes.UTF8MaxLengthInBytes)
            warnings.Add("Synchro Progression Text Format exceeds ECS text capacity. Runtime will use the default format.");

        if (format.IndexOf(GameHudSynchroMeterSettings.ProgressionPercentageToken,
                           System.StringComparison.Ordinal) < 0)
        {
            warnings.Add("Synchro Progression Text Format does not contain [ProgressionPercentage], so its label will remain static.");
        }
    }

    /// <summary>
    /// Validates progression-label size, alignment, and wave spacing without modifying the preset.
    /// </summary>
    /// <param name="settings">Synchro Meter settings supplying progression-label layout values.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateSynchroProgressionTextLayout(GameHudSynchroMeterSettings settings,
                                                             List<string> warnings)
    {
        ValidatePositive(settings.ProgressionTextFontSize, "Synchro Progression Text Font Size", warnings);
        ValidateNonNegative(settings.ProgressionTextWaveDistance, "Synchro Progression Text Wave Distance", warnings);

        switch (settings.ProgressionTextAlignment)
        {
            case GameHudSynchroMeterTextAlignment.Left:
            case GameHudSynchroMeterTextAlignment.Center:
            case GameHudSynchroMeterTextAlignment.Right:
                break;
            default:
                warnings.Add("Synchro Progression Text Alignment is unsupported. Runtime will use centered alignment.");
                break;
        }
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
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            warnings.Add(label + " is not finite. Runtime will use a safe fallback.");
            return;
        }

        if (value < 0f)
            warnings.Add(label + " is negative. Runtime will use a safe non-negative fallback.");
    }

    /// <summary>
    /// Adds a warning when a scalar is not finite or does not remain strictly above zero.
    /// </summary>
    /// <param name="value">Authored scalar value.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidatePositive(float value, string label, List<string> warnings)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            warnings.Add(label + " should be finite and above 0. Runtime will use a safe positive fallback.");
    }

    /// <summary>
    /// Adds a warning when a percentage is not finite or lies outside the inclusive 0..100 range.
    /// </summary>
    /// <param name="value">Authored percentage value.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidatePercentage(float value, string label, List<string> warnings)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 100f)
            warnings.Add(label + " should be finite and remain between 0 and 100.");
    }

    /// <summary>
    /// Adds a warning when a phase value is not finite or lies outside one normalized image-tile cycle.
    /// </summary>
    /// <param name="value">Authored normalized phase value.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateNormalized(float value, string label, List<string> warnings)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            warnings.Add(label + " should stay within the finite 0..1 range. Runtime will clamp the effective phase.");
    }
    #endregion

    #endregion
}
