using UnityEngine;

/// <summary>
/// Loads and saves local user settings through PlayerPrefs using explicit defaults for a fresh install.
/// </summary>
public static class GameUserSettingsStore
{
    #region Constants
    private const string MasterVolumeKey = "NashCore.Settings.Audio.MasterVolume";
    private const string SfxVolumeKey = "NashCore.Settings.Audio.SfxVolume";
    private const string MusicVolumeKey = "NashCore.Settings.Audio.MusicVolume";
    private const string VisualPointerKey = "NashCore.Settings.Exp.VisualPointerEnabled";
    private const string FullscreenKey = "NashCore.Settings.Exp.FullscreenEnabled";
    private const string FrameRateLimitKey = "NashCore.Settings.Gameplay.FrameRateLimit";
    private const string DamageRumbleMultiplierKey = "NashCore.Settings.Exp.DamageRumbleMultiplier";
    private const string FireRumbleMultiplierKey = "NashCore.Settings.Exp.FireRumbleMultiplier";
    public const float DefaultVolume = 1f;
    public const float DefaultRumbleMultiplier = 1f;
    public const int DefaultFrameRateLimit = 60;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the default settings used by Reset Defaults and by first-run PlayerPrefs loading.
    /// </summary>
    /// <returns>Default local user settings.</returns>
    public static GameUserSettingsData CreateDefaults()
    {
        return new GameUserSettingsData
        {
            MasterVolume = DefaultVolume,
            SfxVolume = DefaultVolume,
            MusicVolume = DefaultVolume,
            VisualPointerEnabled = 1,
            FullscreenEnabled = Screen.fullScreen ? (byte)1 : (byte)0,
            FrameRateLimit = DefaultFrameRateLimit,
            DamageRumbleMultiplier = DefaultRumbleMultiplier,
            FireRumbleMultiplier = DefaultRumbleMultiplier
        };
    }

    /// <summary>
    /// Builds the project-authored default settings used by Reset Defaults and first-run PlayerPrefs loading.
    /// </summary>
    /// <param name="runtimeOptions">Runtime options resolved from the Settings Manager preset.</param>
    /// <returns>Runtime-clamped default local user settings.</returns>
    public static GameUserSettingsData CreateDefaults(in GameUserSettingsRuntimeOptions runtimeOptions)
    {
        return ClampForRuntime(runtimeOptions.DefaultSettings);
    }

    /// <summary>
    /// Loads settings from PlayerPrefs, falling back to defaults for missing keys.
    /// </summary>
    /// <returns>Loaded and runtime-clamped local user settings.</returns>
    public static GameUserSettingsData Load()
    {
        GameUserSettingsData defaults = CreateDefaults();
        return Load(in defaults);
    }

    /// <summary>
    /// Loads settings from PlayerPrefs, falling back to Settings Manager defaults for missing keys.
    /// </summary>
    /// <param name="runtimeOptions">Runtime options containing reset defaults.</param>
    /// <returns>Loaded and runtime-clamped local user settings.</returns>
    public static GameUserSettingsData Load(in GameUserSettingsRuntimeOptions runtimeOptions)
    {
        GameUserSettingsData defaults = CreateDefaults(in runtimeOptions);
        return Load(in defaults);
    }

    /// <summary>
    /// Loads settings from PlayerPrefs, falling back to the provided defaults for missing keys.
    /// </summary>
    /// <param name="defaults">Default values used when PlayerPrefs keys do not exist yet.</param>
    /// <returns>Loaded and runtime-clamped local user settings.</returns>
    public static GameUserSettingsData Load(in GameUserSettingsData defaults)
    {
        GameUserSettingsData settings = new GameUserSettingsData
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaults.MasterVolume),
            SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, defaults.SfxVolume),
            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaults.MusicVolume),
            VisualPointerEnabled = PlayerPrefs.GetInt(VisualPointerKey, defaults.VisualPointerEnabled) != 0 ? (byte)1 : (byte)0,
            FullscreenEnabled = PlayerPrefs.GetInt(FullscreenKey, defaults.FullscreenEnabled) != 0 ? (byte)1 : (byte)0,
            FrameRateLimit = PlayerPrefs.GetInt(FrameRateLimitKey, defaults.FrameRateLimit),
            DamageRumbleMultiplier = PlayerPrefs.GetFloat(DamageRumbleMultiplierKey, defaults.DamageRumbleMultiplier),
            FireRumbleMultiplier = PlayerPrefs.GetFloat(FireRumbleMultiplierKey, defaults.FireRumbleMultiplier)
        };

        return ClampForRuntime(settings);
    }

    /// <summary>
    /// Saves the provided settings into PlayerPrefs and flushes them immediately for menu-confirm flows.
    /// </summary>
    /// <param name="settings">Local user settings to persist.</param>
    public static void Save(in GameUserSettingsData settings)
    {
        GameUserSettingsData clampedSettings = ClampForRuntime(settings);
        PlayerPrefs.SetFloat(MasterVolumeKey, clampedSettings.MasterVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, clampedSettings.SfxVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, clampedSettings.MusicVolume);
        PlayerPrefs.SetInt(VisualPointerKey, clampedSettings.VisualPointerEnabled);
        PlayerPrefs.SetInt(FullscreenKey, clampedSettings.FullscreenEnabled);
        PlayerPrefs.SetInt(FrameRateLimitKey, clampedSettings.FrameRateLimit);
        PlayerPrefs.SetFloat(DamageRumbleMultiplierKey, clampedSettings.DamageRumbleMultiplier);
        PlayerPrefs.SetFloat(FireRumbleMultiplierKey, clampedSettings.FireRumbleMultiplier);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Returns a runtime-safe copy of the provided settings for immediate application.
    /// </summary>
    /// <param name="settings">Settings to clamp for runtime use.</param>
    /// <returns>Runtime-safe settings values.</returns>
    public static GameUserSettingsData ClampForRuntime(in GameUserSettingsData settings)
    {
        return new GameUserSettingsData
        {
            MasterVolume = Mathf.Clamp01(settings.MasterVolume),
            SfxVolume = Mathf.Clamp01(settings.SfxVolume),
            MusicVolume = Mathf.Clamp01(settings.MusicVolume),
            VisualPointerEnabled = settings.VisualPointerEnabled != 0 ? (byte)1 : (byte)0,
            FullscreenEnabled = settings.FullscreenEnabled != 0 ? (byte)1 : (byte)0,
            FrameRateLimit = ResolveSupportedFrameRateLimit(settings.FrameRateLimit),
            DamageRumbleMultiplier = Mathf.Clamp(settings.DamageRumbleMultiplier, 0f, 2f),
            FireRumbleMultiplier = Mathf.Clamp(settings.FireRumbleMultiplier, 0f, 2f)
        };
    }

    /// <summary>
    /// Resolves PlayerPrefs or authored data to one supported frame-rate lock.
    /// </summary>
    /// <param name="frameRateLimit">Candidate frame-rate limit.</param>
    /// <returns>Supported frame-rate limit in frames per second.</returns>
    public static int ResolveSupportedFrameRateLimit(int frameRateLimit)
    {
        switch (frameRateLimit)
        {
            case 60:
            case 120:
            case 180:
                return frameRateLimit;
            default:
                return DefaultFrameRateLimit;
        }
    }
    #endregion

    #endregion
}
