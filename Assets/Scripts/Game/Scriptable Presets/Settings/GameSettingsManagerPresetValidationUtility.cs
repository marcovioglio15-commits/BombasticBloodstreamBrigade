using System.Collections.Generic;

/// <summary>
/// Produces non-mutating validation warnings for GameSettingsManagerPreset assets.
/// </summary>
public static class GameSettingsManagerPresetValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects warnings describing values that may lead to invalid Settings menu behavior.
    /// </summary>
    /// <param name="preset">Settings manager preset to inspect.</param>
    /// <param name="warnings">Mutable list that receives warning text.</param>
    public static void CollectWarnings(GameSettingsManagerPreset preset, List<string> warnings)
    {
        if (warnings == null)
            return;

        warnings.Clear();

        if (preset == null)
        {
            warnings.Add("Settings Manager preset is missing.");
            return;
        }

        ValidateAudioSettings(preset.AudioSettings, warnings);
        ValidateGameplaySettings(preset.ExperienceSettings, warnings);
        ValidateControllerNavigationSettings(preset.ControllerNavigationSettings, warnings);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Validates Settings menu Audio panel defaults and preview references.
    /// </summary>
    /// <param name="audioSettings">Audio settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateAudioSettings(GameSettingsManagerAudioSettings audioSettings, List<string> warnings)
    {
        if (audioSettings == null)
        {
            warnings.Add("Audio settings are missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(audioSettings.MasterBusPath))
            warnings.Add("Master Bus Path is empty.");

        if (string.IsNullOrWhiteSpace(audioSettings.SfxBusPath))
            warnings.Add("SFX Bus Path is empty.");

        if (string.IsNullOrWhiteSpace(audioSettings.MusicBusPath))
            warnings.Add("Music Bus Path is empty.");

        ValidateNormalizedDefault(audioSettings.DefaultMasterVolume, "Default Master Volume", warnings);
        ValidateNormalizedDefault(audioSettings.DefaultSfxVolume, "Default SFX Volume", warnings);
        ValidateNormalizedDefault(audioSettings.DefaultMusicVolume, "Default Music Volume", warnings);
        ValidateMasterPreview(audioSettings, warnings);
        ValidatePreviewEvent(audioSettings.SfxVolumePreview, "SFX slider preview", warnings);
        ValidatePreviewEvent(audioSettings.MusicVolumePreview, "Music slider preview", warnings);
    }

    /// <summary>
    /// Validates the Master slider preview, accounting for the mode that previews every other slider event at once.
    /// Only the controls that actually drive runtime playback are validated, so the Master event is not flagged while
    /// it is intentionally ignored.
    /// </summary>
    /// <param name="audioSettings">Audio settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateMasterPreview(GameSettingsManagerAudioSettings audioSettings, List<string> warnings)
    {
        if (!audioSettings.MasterPlaysAllPreviews)
        {
            ValidatePreviewEvent(audioSettings.MasterVolumePreview, "Master slider preview", warnings);
            WarnIfMasterMatchesMusic(audioSettings, warnings);
            return;
        }

        // Master ignores its own event in this mode, so warn only when there is nothing else left to preview.
        bool sfxEmpty = audioSettings.SfxVolumePreview == null || string.IsNullOrWhiteSpace(audioSettings.SfxVolumePreview.EventPath);
        bool musicEmpty = audioSettings.MusicVolumePreview == null || string.IsNullOrWhiteSpace(audioSettings.MusicVolumePreview.EventPath);

        if (sfxEmpty && musicEmpty)
            warnings.Add("Master Plays All Previews is enabled but both SFX and Music slider previews are empty. The Master slider will produce no preview sound.");
    }

    /// <summary>
    /// Warns when the Master preview event matches the Music preview event, which is silently suppressed at runtime
    /// whenever that event is the live background music. Enabling Master Plays All Previews is the recommended fix.
    /// </summary>
    /// <param name="audioSettings">Audio settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void WarnIfMasterMatchesMusic(GameSettingsManagerAudioSettings audioSettings, List<string> warnings)
    {
        GameSettingsManagerPreviewEventSettings masterPreview = audioSettings.MasterVolumePreview;
        GameSettingsManagerPreviewEventSettings musicPreview = audioSettings.MusicVolumePreview;

        if (masterPreview == null || musicPreview == null)
            return;

        if (string.IsNullOrWhiteSpace(masterPreview.EventPath))
            return;

        if (!string.Equals(masterPreview.EventPath, musicPreview.EventPath, System.StringComparison.Ordinal))
            return;

        warnings.Add("Master slider preview uses the same event as the Music slider preview. If that event is the live background music it is silently suppressed and the Master slider stays quiet. Enable 'Master Plays All Previews' or pick a distinct event.");
    }

    /// <summary>
    /// Validates Settings menu Gameplay defaults.
    /// </summary>
    /// <param name="experienceSettings">Gameplay settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateGameplaySettings(GameSettingsManagerExperienceSettings experienceSettings, List<string> warnings)
    {
        if (experienceSettings == null)
        {
            warnings.Add("Gameplay settings are missing.");
            return;
        }

        ValidateFrameRateLimit(experienceSettings.DefaultFrameRateLimit, warnings);
        ValidateMultiplierDefault(experienceSettings.DefaultDamageRumbleMultiplier, "Default Damage Rumble Multiplier", warnings);
        ValidateMultiplierDefault(experienceSettings.DefaultFireRumbleMultiplier, "Default Fire Rumble Multiplier", warnings);

        if (experienceSettings.WindowedWidth <= 0)
            warnings.Add("Windowed Display Width is not positive. Runtime will use a fallback width.");

        if (experienceSettings.WindowedHeight <= 0)
            warnings.Add("Windowed Display Height is not positive. Runtime will use a fallback height.");

        if (experienceSettings.WindowedWidth > 0 && experienceSettings.WindowedWidth < 640)
            warnings.Add("Windowed Display Width is below 640 pixels and may make runtime UI cramped.");

        if (experienceSettings.WindowedHeight > 0 && experienceSettings.WindowedHeight < 360)
            warnings.Add("Windowed Display Height is below 360 pixels and may make runtime UI cramped.");
    }

    /// <summary>
    /// Validates controller navigation actions and repeat timing without changing authored values.
    /// </summary>
    /// <param name="navigationSettings">Controller navigation settings to inspect.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateControllerNavigationSettings(GameSettingsManagerControllerNavigationSettings navigationSettings, List<string> warnings)
    {
        if (navigationSettings == null)
        {
            warnings.Add("Controller Navigation settings are missing.");
            return;
        }

        if (navigationSettings.GamepadNavigationMode != RuntimeMenuGamepadNavigationMode.VirtualMouse)
        {
            ValidateActionReference(navigationSettings.NavigateActionName, "Navigate Action", warnings);
            ValidateActionReference(navigationSettings.SubmitActionName, "Submit Action", warnings);
        }

        ValidateActionReference(navigationSettings.CancelActionName, "Cancel Action", warnings);

        if (navigationSettings.NavigateDeadzone < 0f || navigationSettings.NavigateDeadzone > 1f)
            warnings.Add("Navigate Deadzone is outside the 0..1 range. Runtime direct navigation will use a safe fallback.");

        if (navigationSettings.RepeatDelaySeconds < 0f)
            warnings.Add("Repeat Delay Seconds is negative. Runtime direct navigation will use a safe fallback.");

        if (navigationSettings.RepeatIntervalSeconds <= 0f)
            warnings.Add("Repeat Interval Seconds is not positive. Runtime direct navigation will use a safe fallback.");
    }

    /// <summary>
    /// Validates the authored frame-rate lock enum.
    /// </summary>
    /// <param name="frameRateLimit">Frame-rate lock selected by the preset.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateFrameRateLimit(GameFrameRateLimit frameRateLimit, List<string> warnings)
    {
        switch (frameRateLimit)
        {
            case GameFrameRateLimit.Fps60:
            case GameFrameRateLimit.Fps120:
            case GameFrameRateLimit.Fps180:
                return;
            default:
                warnings.Add("Default Frame Rate Cap must be 60, 120 or 180 FPS. Runtime reset will use a safe fallback.");
                return;
        }
    }

    /// <summary>
    /// Validates one Input System action reference used by controller menu navigation.
    /// </summary>
    /// <param name="actionReference">Authored action id, name or path.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateActionReference(string actionReference, string label, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(actionReference))
            warnings.Add(label + " is empty. Runtime controller navigation will fall back to the default UI action.");
    }

    /// <summary>
    /// Validates one Settings menu preview event path.
    /// </summary>
    /// <param name="preview">Preview settings to inspect.</param>
    /// <param name="label">Warning label for the preview slot.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidatePreviewEvent(GameSettingsManagerPreviewEventSettings preview,
                                             string label,
                                             List<string> warnings)
    {
        if (preview == null)
        {
            warnings.Add(label + " settings are missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(preview.EventPath))
            warnings.Add(label + " event path is empty. The slider will change bus volume without a preview sound.");
    }

    /// <summary>
    /// Validates one 0..1 default scalar without changing it.
    /// </summary>
    /// <param name="value">Authored default value.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateNormalizedDefault(float value, string label, List<string> warnings)
    {
        if (value < 0f || value > 1f)
            warnings.Add(label + " is outside the 0..1 range. Runtime reset clamps it.");
    }

    /// <summary>
    /// Validates one 0..2 rumble default without changing it.
    /// </summary>
    /// <param name="value">Authored multiplier default.</param>
    /// <param name="label">Display label included in warnings.</param>
    /// <param name="warnings">Mutable warning output list.</param>
    private static void ValidateMultiplierDefault(float value, string label, List<string> warnings)
    {
        if (value < 0f || value > 2f)
            warnings.Add(label + " is outside the 0..2 range. Runtime reset clamps it.");
    }
    #endregion

    #endregion
}
