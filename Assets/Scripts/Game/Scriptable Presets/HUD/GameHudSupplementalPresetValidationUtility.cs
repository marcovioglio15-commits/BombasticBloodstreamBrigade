using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;

/// <summary>
/// Produces non-mutating warnings for supplemental HUD presentation and interaction settings.
/// </summary>
public static class GameHudSupplementalPresetValidationUtility
{
    #region Methods

    #region Power-Up Summary
    /// <summary>
    /// Appends warnings for summary layout, pool capacity, refresh, and statistic definitions.
    /// </summary>
    /// <param name="settings">Inline summary settings to inspect.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    public static void ValidatePowerUpSummary(GameHudPowerUpSummarySettings settings, List<string> warnings)
    {
        if (warnings == null)
            return;

        if (settings == null)
        {
            warnings.Add("Power-Up Summary settings are missing. The runtime summary remains disabled until the HUD preset is initialized.");
            return;
        }

        ValidatePositive(settings.ExpandedWidth, "Power-Up Summary Expanded Width", warnings);
        ValidateNonNegative(settings.CollapsedHandleWidth, "Power-Up Summary Collapsed Handle Width", warnings);
        ValidateNonNegative(settings.ContentPadding, "Power-Up Summary Content Padding", warnings);
        ValidateNonNegative(settings.PowerUpColumnSpacing, "Power-Up Summary Column Spacing", warnings);
        ValidateNonNegative(settings.SectionSpacing, "Power-Up Summary Section Spacing", warnings);
        ValidateNormalized(settings.PowerUpAreaHeightNormalized, "Power-Up Summary Power-Up Area Height", warnings);
        ValidateNonNegative(settings.SlideDurationSeconds, "Power-Up Summary Slide Duration", warnings);
        ValidatePoolCapacity(settings.MaximumVisibleActivePowerUps,
                             GameHudPowerUpSummarySettings.AuthoredActiveSlotCapacity,
                             "active power-up",
                             warnings);
        ValidatePoolCapacity(settings.MaximumVisiblePassivePowerUps,
                             GameHudPowerUpSummarySettings.AuthoredPassiveSlotCapacity,
                             "passive power-up",
                             warnings);
        ValidatePositive(settings.IconSize, "Power-Up Summary Icon Size", warnings);
        ValidateNonNegative(settings.IconSpacing, "Power-Up Summary Icon Spacing", warnings);
        ValidatePositive(settings.CounterFontSize, "Power-Up Summary Counter Font Size", warnings);
        ValidatePositive(settings.TitleFontSize, "Power-Up Summary Title Font Size", warnings);
        ValidatePositive(settings.SeparatorThickness, "Power-Up Summary Separator Thickness", warnings);
        ValidateNonNegative(settings.StatisticRefreshIntervalSeconds, "Power-Up Summary Statistic Refresh Interval", warnings);

        if (settings.EnableInputToggle && string.IsNullOrWhiteSpace(settings.ToggleActionId))
            warnings.Add("Power-Up Summary input toggle is enabled but no Input Action is selected.");

        ValidateStatistics(settings.Statistics, warnings);
    }

    /// <summary>
    /// Validates ordered statistic rows and their fixed preauthored capacity.
    /// </summary>
    /// <param name="statistics">Statistic definitions authored in the inline HUD settings.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateStatistics(IReadOnlyList<GameHudStatisticDisplayDefinition> statistics,
                                           List<string> warnings)
    {
        if (statistics == null)
        {
            warnings.Add("Power-Up Summary statistic list is missing.");
            return;
        }

        if (statistics.Count > GameHudPowerUpSummarySettings.AuthoredStatisticRowCapacity)
            warnings.Add("Power-Up Summary statistic count exceeds the preauthored row pool. Extra rows will not be baked.");

        for (int statisticIndex = 0; statisticIndex < statistics.Count; statisticIndex++)
        {
            GameHudStatisticDisplayDefinition definition = statistics[statisticIndex];

            if (definition == null)
            {
                warnings.Add(string.Format("Power-Up Summary statistic row {0} is missing.", statisticIndex + 1));
                continue;
            }

            if (definition.Statistic == GameHudPlayerStatistic.CustomScalableStat &&
                string.IsNullOrWhiteSpace(definition.ScalableStatName))
                warnings.Add(string.Format("Power-Up Summary statistic row {0} selects Custom Scalable Stat without choosing a stat.", statisticIndex + 1));

            if (definition.DecimalPlaces < 0 || definition.DecimalPlaces > 6)
                warnings.Add(string.Format("Power-Up Summary statistic row {0} decimal places should remain between 0 and 6.", statisticIndex + 1));

            if (!IsFinite(definition.DisplayMultiplier))
                warnings.Add(string.Format("Power-Up Summary statistic row {0} display multiplier is not finite.", statisticIndex + 1));

            ValidatePositive(definition.FontSize,
                             string.Format("Power-Up Summary statistic row {0} font size", statisticIndex + 1),
                             warnings);
        }
    }
    #endregion

    #region Wave Clear Announcement
    /// <summary>
    /// Appends warnings for content capacity, motion timing, placement, and typography without changing authored values.
    /// </summary>
    /// <param name="settings">Room-clear announcement settings to inspect.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    public static void ValidateWaveClearAnnouncement(GameHudWaveClearAnnouncementSettings settings,
                                                     List<string> warnings)
    {
        if (warnings == null)
            return;

        if (settings == null)
        {
            warnings.Add("Room Clear Announcement settings are missing.");
            return;
        }

        if (!settings.IsEnabled)
            return;

        if (string.IsNullOrWhiteSpace(settings.Content))
            warnings.Add("Room Clear Announcement content is empty, so no visible message will be presented.");
        else if (Encoding.UTF8.GetByteCount(settings.Content) > FixedString512Bytes.UTF8MaxLengthInBytes)
            warnings.Add("Room Clear Announcement content exceeds the baked 512-byte UTF-8 capacity and will be truncated at runtime.");

        ValidateAnnouncementMode(settings.PresentationMode,
                                 settings.TraversalDurationSeconds,
                                 settings.PauseAtCenter,
                                 settings.CenterHoldDurationSeconds,
                                 settings.PaintRevealDurationSeconds,
                                 settings.PaintHoldDurationSeconds,
                                 settings.PaintFadeOutDurationSeconds,
                                 "Room Clear Announcement",
                                 warnings);
        ValidateAnnouncementDirection(settings.Direction,
                                      "Room Clear Announcement direction",
                                      warnings);

        if (settings.PresentationMode == GameHudWaveClearAnnouncementPresentationMode.PaintReveal)
            ValidateAnnouncementDirection(settings.PaintExitDirection,
                                          "Room Clear Announcement removal direction",
                                          warnings);

        ValidateNormalized(settings.VerticalPositionNormalized,
                           "Room Clear Announcement vertical position",
                           warnings);
        if (settings.PresentationMode == GameHudWaveClearAnnouncementPresentationMode.Traversal ||
            settings.UseFinalWaveOverride &&
            settings.FinalWavePresentationMode == GameHudWaveClearAnnouncementPresentationMode.Traversal)
        {
            ValidateNonNegative(settings.HorizontalOffscreenPadding,
                                "Room Clear Announcement off-screen padding",
                                warnings);
        }

        ValidatePositive(settings.FontSize, "Room Clear Announcement font size", warnings);

        if (settings.PlayAudioEvent && settings.AudioEventId == GameAudioEventId.None)
            warnings.Add("Room Clear Announcement audio is enabled but no standard Audio Event is selected.");

        if (settings.UseFinalWaveOverride)
        {
            if (string.IsNullOrWhiteSpace(settings.FinalWaveContent))
                warnings.Add("Room Clear Announcement terminal Boss content is empty, so the victory menu will not be delayed by a visible message.");
            else if (Encoding.UTF8.GetByteCount(settings.FinalWaveContent) > FixedString512Bytes.UTF8MaxLengthInBytes)
                warnings.Add("Room Clear Announcement terminal Boss content exceeds the baked 512-byte UTF-8 capacity and will be truncated at runtime.");

            ValidateAnnouncementMode(settings.FinalWavePresentationMode,
                                     settings.FinalWaveTraversalDurationSeconds,
                                     settings.FinalWavePauseAtCenter,
                                     settings.FinalWaveCenterHoldDurationSeconds,
                                     settings.FinalWavePaintRevealDurationSeconds,
                                     settings.FinalWavePaintHoldDurationSeconds,
                                     settings.FinalWavePaintFadeOutDurationSeconds,
                                     "Room Clear Announcement terminal Boss",
                                     warnings);
            ValidateAnnouncementDirection(settings.FinalWaveDirection,
                                          "Room Clear Announcement terminal Boss direction",
                                          warnings);

            if (settings.FinalWavePresentationMode == GameHudWaveClearAnnouncementPresentationMode.PaintReveal)
                ValidateAnnouncementDirection(settings.FinalWavePaintExitDirection,
                                              "Room Clear Announcement terminal Boss removal direction",
                                              warnings);

            if (settings.PlayFinalWaveAudioEvent && settings.FinalWaveAudioEventId == GameAudioEventId.None)
                warnings.Add("Room Clear Announcement terminal Boss audio is enabled but no Audio Event is selected.");
        }

        if (!IsFinite(settings.Color))
            warnings.Add("Room Clear Announcement color should contain only finite channels.");

        bool usesPaint = settings.PresentationMode == GameHudWaveClearAnnouncementPresentationMode.PaintReveal ||
                         settings.UseFinalWaveOverride &&
                         settings.FinalWavePresentationMode == GameHudWaveClearAnnouncementPresentationMode.PaintReveal;

        if (!usesPaint)
            return;

        if (!IsFinite(settings.PaintBackgroundColor))
            warnings.Add("Room Clear Announcement paint background color should contain only finite channels.");

        if (settings.PaintBackgroundSprite == null)
            warnings.Add("Room Clear Announcement requires a paint background sprite to define its final aerosol silhouette.");

        if (!IsFinite(settings.PaintBackgroundPadding) ||
            settings.PaintBackgroundPadding.x < 0f ||
            settings.PaintBackgroundPadding.y < 0f)
        {
            warnings.Add("Room Clear Announcement paint background padding should contain finite non-negative components.");
        }

        ValidateRange(settings.PaintEdgeSoftness,
                      0.001f,
                      0.25f,
                      "Room Clear Announcement deposit softness",
                      warnings);
        ValidateRange(settings.PaintNoiseStrength,
                      0f,
                      0.5f,
                      "Room Clear Announcement deposit variation",
                      warnings);
        ValidateRange(settings.PaintNoiseScale,
                      0.25f,
                      12f,
                      "Room Clear Announcement deposit scale",
                      warnings);
        ValidateRange(settings.PaintBristleStrength,
                      0f,
                      0.25f,
                      "Room Clear Announcement mist strength",
                      warnings);
        ValidateRange(settings.PaintBristleScale,
                      1f,
                      96f,
                      "Room Clear Announcement mist density",
                      warnings);
    }
    #endregion

    #region Settings Navigation
    /// <summary>
    /// Appends warnings for missing Input Actions and invalid direct-navigation repeat tuning.
    /// </summary>
    /// <param name="settings">Inline Settings menu navigation configuration.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    public static void ValidateSettingsNavigation(GameHudSettingsNavigationSettings settings, List<string> warnings)
    {
        if (warnings == null || settings == null || !settings.IsEnabled)
            return;

        ValidateActionId(settings.PreviousTabActionId, "Settings Previous Tab", warnings);
        ValidateActionId(settings.NextTabActionId, "Settings Next Tab", warnings);
        ValidateActionId(settings.VerticalNavigationActionId, "Settings Vertical Navigation", warnings);
        ValidateActionId(settings.HorizontalNavigationActionId, "Settings Horizontal Navigation", warnings);
        ValidateActionId(settings.SubmitActionId, "Settings Submit", warnings);
        ValidateActionId(settings.CancelActionId, "Settings Cancel", warnings);
        ValidateNormalized(settings.InputDeadzone, "Settings navigation input deadzone", warnings);
        ValidateNonNegative(settings.RepeatDelaySeconds, "Settings navigation repeat delay", warnings);
        ValidatePositive(settings.RepeatIntervalSeconds, "Settings navigation repeat interval", warnings);

        if (!settings.CustomizeSelectionPresentation)
            return;

        if (settings.OverrideSelectionGraphicColors &&
            (!IsFinite(settings.UnselectedGraphicColor) || !IsFinite(settings.SelectedGraphicColor)))
            warnings.Add("Settings selection graphic colors should contain only finite channels.");

        if (settings.OverrideSelectionTextStyle &&
            (!IsFinite(settings.UnselectedTextColor) || !IsFinite(settings.SelectedTextColor)))
            warnings.Add("Settings selection text colors should contain only finite channels.");

        if (settings.OverrideSelectionScale)
        {
            ValidateScale(settings.UnselectedScale, "Settings unselected scale", warnings);
            ValidateScale(settings.SelectedScale, "Settings selected scale", warnings);
        }

        if (settings.ShowSelectionOutline)
        {
            if (!IsFinite(settings.SelectionOutlineColor))
                warnings.Add("Settings selection outline color should contain only finite channels.");

            if (!IsFinite(settings.SelectionOutlineDistance))
                warnings.Add("Settings selection outline distance should contain only finite components.");
        }
    }
    #endregion

    #region Button Interactions
    /// <summary>
    /// Appends warnings for duplicate menu profiles and invalid state-transition values.
    /// </summary>
    /// <param name="settings">Independent menu-button interaction settings to inspect.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    public static void ValidateButtonInteractions(GameHudButtonInteractionSettings settings, List<string> warnings)
    {
        if (warnings == null)
            return;

        if (settings == null || settings.MenuProfiles == null)
        {
            warnings.Add("Menu Button Interaction settings are missing.");
            return;
        }

        HashSet<GameUiMenuKind> visitedMenus = new HashSet<GameUiMenuKind>();

        for (int profileIndex = 0; profileIndex < settings.MenuProfiles.Count; profileIndex++)
        {
            GameUiMenuButtonInteractionDefinition profile = settings.MenuProfiles[profileIndex];

            if (profile == null)
            {
                warnings.Add(string.Format("Menu Button Interaction profile {0} is missing.", profileIndex + 1));
                continue;
            }

            if (!visitedMenus.Add(profile.MenuKind))
                warnings.Add(string.Format("Menu Button Interaction contains more than one {0} profile. Only the first entry is baked.", profile.MenuKind));

            switch (profile.MotionTarget)
            {
                case GameUiButtonMotionTarget.WholeButton:
                case GameUiButtonMotionTarget.ContentOnly:
                    break;
                default:
                    warnings.Add(string.Format("{0} button motion target is unsupported. Runtime will use the complete button target.", profile.MenuKind));
                    break;
            }

            ValidateNonNegative(profile.TransitionDurationSeconds,
                                string.Format("{0} button transition duration", profile.MenuKind),
                                warnings);

            if (profile.HoverTransformMode == GameUiButtonHoverTransformMode.Pulse)
            {
                ValidatePositive(profile.HoverPulseCycleSeconds,
                                 string.Format("{0} hover pulse cycle duration", profile.MenuKind),
                                 warnings);

                if (profile.HoverPulseCycles < 1)
                    warnings.Add(string.Format("{0} hover pulse cycles should be at least one.", profile.MenuKind));
            }

            if (!IsFinite(profile.HoverScale) || !IsFinite(profile.PressedScale))
                warnings.Add(string.Format("{0} button scale states contain a non-finite component.", profile.MenuKind));

            if (profile.OverrideTextStyle)
            {
                ValidatePositive(profile.NormalFontSize, string.Format("{0} normal button font size", profile.MenuKind), warnings);
                ValidatePositive(profile.EmphasizedFontSize, string.Format("{0} emphasized button font size", profile.MenuKind), warnings);
            }

            ValidateButtonImageContent(profile, warnings);
        }
    }

    /// <summary>
    /// Validates stable IDs, required normal sprites, duplicate mappings, and finite image tints.
    /// </summary>
    /// <param name="profile">Menu profile whose image-content path is inspected.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateButtonImageContent(GameUiMenuButtonInteractionDefinition profile,
                                                   List<string> warnings)
    {
        if (profile.ContentMode != GameUiButtonContentMode.Image)
            return;

        if (profile.ImageContentDefinitions == null || profile.ImageContentDefinitions.Count == 0)
        {
            warnings.Add(string.Format("{0} uses Image button content but has no per-button image mappings.", profile.MenuKind));
            return;
        }

        HashSet<string> visitedButtonIds = new HashSet<string>(StringComparer.Ordinal);

        for (int contentIndex = 0; contentIndex < profile.ImageContentDefinitions.Count; contentIndex++)
        {
            GameUiButtonImageContentDefinition content = profile.ImageContentDefinitions[contentIndex];

            if (content == null)
            {
                warnings.Add(string.Format("{0} image mapping {1} is missing.", profile.MenuKind, contentIndex + 1));
                continue;
            }

            if (string.IsNullOrWhiteSpace(content.ButtonId))
            {
                warnings.Add(string.Format("{0} image mapping {1} has no Button ID.", profile.MenuKind, contentIndex + 1));
                continue;
            }

            string buttonId = content.ButtonId.Trim();

            if (!visitedButtonIds.Add(buttonId))
                warnings.Add(string.Format("{0} contains more than one image mapping for Button ID '{1}'. Only the first is baked.", profile.MenuKind, buttonId));

            if (content.NormalSprite == null)
                warnings.Add(string.Format("{0} image mapping '{1}' has no normal sprite. Its relay will retain TMP text.", profile.MenuKind, buttonId));

            if (!IsFinite(content.NormalColor) ||
                !IsFinite(content.HoverColor) ||
                !IsFinite(content.PressedColor) ||
                !IsFinite(content.DisabledColor))
            {
                warnings.Add(string.Format("{0} image mapping '{1}' contains a non-finite tint channel.", profile.MenuKind, buttonId));
            }

            if (content.NormalColor.a <= 0f)
            {
                warnings.Add(string.Format(
                    "{0} image mapping '{1}' has a fully transparent normal tint, so its image is invisible while the button is idle.",
                    profile.MenuKind,
                    buttonId));
            }
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Validates only the timing family consumed by one announcement presentation mode.
    /// </summary>
    /// <param name="mode">Selected presentation mode.</param>
    /// <param name="traversalDuration">Complete edge-to-edge traversal duration.</param>
    /// <param name="pauseAtCenter">Whether the traversal pauses at screen center.</param>
    /// <param name="centerHoldDuration">Traversal center hold duration.</param>
    /// <param name="paintRevealDuration">Paint coverage duration.</param>
    /// <param name="paintHoldDuration">Fully revealed paint hold duration.</param>
    /// <param name="paintRemovalDuration">Directional paint-removal duration.</param>
    /// <param name="label">Diagnostic prefix identifying the presentation.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateAnnouncementMode(GameHudWaveClearAnnouncementPresentationMode mode,
                                                 float traversalDuration,
                                                 bool pauseAtCenter,
                                                 float centerHoldDuration,
                                                 float paintRevealDuration,
                                                 float paintHoldDuration,
                                                 float paintRemovalDuration,
                                                 string label,
                                                 List<string> warnings)
    {
        switch (mode)
        {
            case GameHudWaveClearAnnouncementPresentationMode.PaintReveal:
                ValidatePositive(paintRevealDuration, label + " paint reveal duration", warnings);
                ValidateNonNegative(paintHoldDuration, label + " paint hold duration", warnings);
                ValidatePositive(paintRemovalDuration, label + " paint removal duration", warnings);
                return;
            default:
                ValidatePositive(traversalDuration, label + " traversal duration", warnings);

                if (pauseAtCenter)
                    ValidateNonNegative(centerHoldDuration, label + " center hold duration", warnings);

                return;
        }
    }

    /// <summary>
    /// Warns when one announcement direction does not map to a supported horizontal frontier.
    /// </summary>
    /// <param name="direction">Authored announcement direction.</param>
    /// <param name="label">Setting label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateAnnouncementDirection(GameHudWaveClearAnnouncementDirection direction,
                                                      string label,
                                                      List<string> warnings)
    {
        switch (direction)
        {
            case GameHudWaveClearAnnouncementDirection.LeftToRight:
            case GameHudWaveClearAnnouncementDirection.RightToLeft:
                return;
            default:
                warnings.Add(label + " is unsupported. Runtime will retain the serialized value without changing the preset.");
                return;
        }
    }

    /// <summary>
    /// Warns when one scalar lies outside its finite inclusive range.
    /// </summary>
    /// <param name="value">Authored scalar value.</param>
    /// <param name="minimum">Inclusive supported minimum.</param>
    /// <param name="maximum">Inclusive supported maximum.</param>
    /// <param name="label">Setting label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateRange(float value,
                                      float minimum,
                                      float maximum,
                                      string label,
                                      List<string> warnings)
    {
        if (!IsFinite(value) || value < minimum || value > maximum)
            warnings.Add(string.Format("{0} should remain inside the finite {1}..{2} range. Runtime will use a safe fallback without changing the preset.",
                                       label,
                                       minimum,
                                       maximum));
    }

    /// <summary>
    /// Warns when one required stable Input Action ID is empty.
    /// </summary>
    /// <param name="actionId">Stable Input Action ID to inspect.</param>
    /// <param name="label">Action label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateActionId(string actionId, string label, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            warnings.Add(label + " Input Action is not selected.");
    }

    /// <summary>
    /// Warns when one configured count cannot fit the fixed authored UI pool.
    /// </summary>
    /// <param name="value">Configured visible count.</param>
    /// <param name="capacity">Fixed authored pool capacity.</param>
    /// <param name="label">Entry type included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidatePoolCapacity(int value, int capacity, string label, List<string> warnings)
    {
        if (value < 0 || value > capacity)
            warnings.Add(string.Format("Power-Up Summary maximum visible {0} count should remain between 0 and the preauthored capacity of {1}.", label, capacity));
    }

    /// <summary>
    /// Warns when a scalar is negative or not finite.
    /// </summary>
    /// <param name="value">Authored scalar value.</param>
    /// <param name="label">Setting label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateNonNegative(float value, string label, List<string> warnings)
    {
        if (!IsFinite(value) || value < 0f)
            warnings.Add(label + " should be finite and non-negative. Runtime will use a safe fallback without changing the preset.");
    }

    /// <summary>
    /// Warns when a scalar is not strictly positive and finite.
    /// </summary>
    /// <param name="value">Authored scalar value.</param>
    /// <param name="label">Setting label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidatePositive(float value, string label, List<string> warnings)
    {
        if (!IsFinite(value) || value <= 0f)
            warnings.Add(label + " should be finite and above 0. Runtime will use a safe fallback without changing the preset.");
    }

    /// <summary>
    /// Warns when a normalized scalar lies outside its supported range.
    /// </summary>
    /// <param name="value">Authored normalized scalar.</param>
    /// <param name="label">Setting label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateNormalized(float value, string label, List<string> warnings)
    {
        if (!IsFinite(value) || value < 0f || value > 1f)
            warnings.Add(label + " should remain inside the finite 0..1 range. Runtime will use a safe fallback without changing the preset.");
    }

    /// <summary>
    /// Checks whether every vector component is finite.
    /// </summary>
    /// <param name="value">Vector to inspect.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(UnityEngine.Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>
    /// Appends a warning when a scale contains invalid or non-positive components.
    /// </summary>
    /// <param name="value">Scale value to inspect.</param>
    /// <param name="label">Setting label included in the warning.</param>
    /// <param name="warnings">Mutable warning list receiving diagnostics.</param>
    private static void ValidateScale(UnityEngine.Vector3 value, string label, List<string> warnings)
    {
        if (!IsFinite(value) || value.x <= 0f || value.y <= 0f || value.z <= 0f)
            warnings.Add(label + " should contain finite components above 0. Runtime will use a safe fallback without changing the preset.");
    }

    /// <summary>
    /// Checks whether both vector components are finite.
    /// </summary>
    /// <param name="value">Vector to inspect.</param>
    /// <returns>True when both components are finite.</returns>
    private static bool IsFinite(UnityEngine.Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    /// <summary>
    /// Checks whether every color channel is finite.
    /// </summary>
    /// <param name="value">Color to inspect.</param>
    /// <returns>True when every channel is finite.</returns>
    private static bool IsFinite(UnityEngine.Color value)
    {
        return IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
    }

    /// <summary>
    /// Checks whether a scalar is neither NaN nor infinite.
    /// </summary>
    /// <param name="value">Scalar to inspect.</param>
    /// <returns>True when the scalar is finite.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
