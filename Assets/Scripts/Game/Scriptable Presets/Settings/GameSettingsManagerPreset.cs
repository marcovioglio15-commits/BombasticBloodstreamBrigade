using System;
using UnityEngine;

/// <summary>
/// Scriptable preset that owns project defaults and runtime preview options for the Settings menu.
/// </summary>
[CreateAssetMenu(fileName = "GameSettingsManagerPreset", menuName = "Game/Settings Manager Preset", order = 25)]
public sealed class GameSettingsManagerPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this settings manager preset, used for stable editor references.")]
    [SerializeField] private string presetId;

    [Tooltip("Settings manager preset name displayed in Game Management Tool.")]
    [SerializeField] private string presetName = "New Settings Manager Preset";

    [Tooltip("Short description of this Settings menu configuration.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this settings preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Settings Panels")]
    [Tooltip("Audio panel defaults, bus paths and slider preview events used by the runtime Settings menu.")]
    [SerializeField] private GameSettingsManagerAudioSettings audioSettings = new GameSettingsManagerAudioSettings();

    [Tooltip("Gameplay defaults used by Reset Defaults, frame-rate locking and windowed display application.")]
    [SerializeField] private GameSettingsManagerExperienceSettings experienceSettings = new GameSettingsManagerExperienceSettings();

    [Tooltip("Controller navigation mode and actions used by the runtime Settings menu while a gamepad is connected.")]
    [SerializeField] private GameSettingsManagerControllerNavigationSettings controllerNavigationSettings = new GameSettingsManagerControllerNavigationSettings();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public GameSettingsManagerAudioSettings AudioSettings
    {
        get
        {
            return audioSettings;
        }
    }

    public GameSettingsManagerExperienceSettings ExperienceSettings
    {
        get
        {
            return experienceSettings;
        }
    }

    public GameSettingsManagerControllerNavigationSettings ControllerNavigationSettings
    {
        get
        {
            return controllerNavigationSettings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures nested settings containers and stable metadata exist without clamping authored values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (audioSettings == null)
            audioSettings = new GameSettingsManagerAudioSettings();

        audioSettings.EnsureInitialized();

        if (experienceSettings == null)
            experienceSettings = new GameSettingsManagerExperienceSettings();

        if (controllerNavigationSettings == null)
            controllerNavigationSettings = new GameSettingsManagerControllerNavigationSettings();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps required settings containers alive when the preset is edited.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #endregion
}

/// <summary>
/// Supported runtime frame-rate locks exposed by the Gameplay settings panel.
/// </summary>
public enum GameFrameRateLimit
{
    Fps60 = 60,
    Fps120 = 120,
    Fps180 = 180
}

/// <summary>
/// Controller navigation modes supported by authored runtime menu overlays.
/// </summary>
public enum RuntimeMenuGamepadNavigationMode
{
    VirtualMouse = 0,
    DirectSelection = 1,
    Hybrid = 2
}

/// <summary>
/// Settings menu Audio panel defaults and preview-event references.
/// </summary>
[Serializable]
public sealed class GameSettingsManagerAudioSettings
{
    #region Fields

    #region Serialized Fields
    [Header("FMOD Buses")]
    [Tooltip("FMOD master bus path controlled by the Settings menu Master volume slider.")]
    [SerializeField] private string masterBusPath = "bus:/";

    [Tooltip("FMOD SFX bus path controlled by the Settings menu SFX volume slider.")]
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [Tooltip("FMOD music bus path controlled by the Settings menu Music volume slider.")]
    [SerializeField] private string musicBusPath = "bus:/Music";

    [Header("Reset Defaults")]
    [Tooltip("Default Master volume restored by the Settings menu Reset Defaults button.")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultMasterVolume = 1f;

    [Tooltip("Default SFX volume restored by the Settings menu Reset Defaults button.")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultSfxVolume = 1f;

    [Tooltip("Default Music volume restored by the Settings menu Reset Defaults button.")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultMusicVolume = 1f;

    [Header("Preview Events")]
    [Tooltip("When enabled the Master slider previews every other slider event (SFX and Music) at once instead of its own event. Useful because a Master event that matches the live background music is silently suppressed at runtime.")]
    [SerializeField] private bool masterPlaysAllPreviews;

    [Tooltip("FMOD event previewed while the Settings menu Master volume slider is adjusted. Ignored when 'Master Plays All Previews' is enabled.")]
    [SerializeField] private GameSettingsManagerPreviewEventSettings masterVolumePreview = GameSettingsManagerPreviewEventSettings.CreateSfxDefault();

    [Tooltip("FMOD event previewed while the Settings menu SFX volume slider is adjusted.")]
    [SerializeField] private GameSettingsManagerPreviewEventSettings sfxVolumePreview = GameSettingsManagerPreviewEventSettings.CreateSfxDefault();

    [Tooltip("FMOD event previewed while the Settings menu Music volume slider is adjusted.")]
    [SerializeField] private GameSettingsManagerPreviewEventSettings musicVolumePreview = GameSettingsManagerPreviewEventSettings.CreateMusicDefault();
    #endregion

    #endregion

    #region Properties
    public string MasterBusPath
    {
        get
        {
            return masterBusPath;
        }
    }

    public string SfxBusPath
    {
        get
        {
            return sfxBusPath;
        }
    }

    public string MusicBusPath
    {
        get
        {
            return musicBusPath;
        }
    }

    public bool MasterPlaysAllPreviews
    {
        get
        {
            return masterPlaysAllPreviews;
        }
    }

    public float DefaultMasterVolume
    {
        get
        {
            return defaultMasterVolume;
        }
    }

    public float DefaultSfxVolume
    {
        get
        {
            return defaultSfxVolume;
        }
    }

    public float DefaultMusicVolume
    {
        get
        {
            return defaultMusicVolume;
        }
    }

    public GameSettingsManagerPreviewEventSettings MasterVolumePreview
    {
        get
        {
            return masterVolumePreview;
        }
    }

    public GameSettingsManagerPreviewEventSettings SfxVolumePreview
    {
        get
        {
            return sfxVolumePreview;
        }
    }

    public GameSettingsManagerPreviewEventSettings MusicVolumePreview
    {
        get
        {
            return musicVolumePreview;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Ensures preview settings objects exist without changing authored path or volume values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (masterVolumePreview == null)
            masterVolumePreview = GameSettingsManagerPreviewEventSettings.CreateSfxDefault();

        if (sfxVolumePreview == null)
            sfxVolumePreview = GameSettingsManagerPreviewEventSettings.CreateSfxDefault();

        if (musicVolumePreview == null)
            musicVolumePreview = GameSettingsManagerPreviewEventSettings.CreateMusicDefault();
    }
    #endregion
}

/// <summary>
/// Settings menu gameplay defaults restored by Reset Defaults and used for display mode application.
/// </summary>
[Serializable]
public sealed class GameSettingsManagerExperienceSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Reset Defaults")]
    [Tooltip("Default Visual Pointer state restored by the Settings menu Reset Defaults button.")]
    [SerializeField] private bool defaultVisualPointerEnabled = true;

    [Tooltip("Default fullscreen state restored by the Settings menu Reset Defaults button.")]
    [SerializeField] private bool defaultFullscreenEnabled = true;

    [Tooltip("Default frame-rate lock restored by the Settings menu Reset Defaults button.")]
    [SerializeField] private GameFrameRateLimit defaultFrameRateLimit = GameFrameRateLimit.Fps60;

    [Tooltip("Default damage feedback rumble multiplier restored by the Settings menu Reset Defaults button.")]
    [Range(0f, 2f)]
    [SerializeField] private float defaultDamageRumbleMultiplier = 1f;

    [Tooltip("Default fire feedback rumble multiplier restored by the Settings menu Reset Defaults button.")]
    [Range(0f, 2f)]
    [SerializeField] private float defaultFireRumbleMultiplier = 1f;

    [Header("Windowed Display Defaults")]
    [Tooltip("Window width in pixels used when fullscreen is disabled from the runtime Settings menu.")]
    [SerializeField] private int windowedWidth = 1280;

    [Tooltip("Window height in pixels used when fullscreen is disabled from the runtime Settings menu.")]
    [SerializeField] private int windowedHeight = 720;
    #endregion

    #endregion

    #region Properties
    public bool DefaultVisualPointerEnabled
    {
        get
        {
            return defaultVisualPointerEnabled;
        }
    }

    public bool DefaultFullscreenEnabled
    {
        get
        {
            return defaultFullscreenEnabled;
        }
    }

    public GameFrameRateLimit DefaultFrameRateLimit
    {
        get
        {
            return defaultFrameRateLimit;
        }
    }

    public float DefaultDamageRumbleMultiplier
    {
        get
        {
            return defaultDamageRumbleMultiplier;
        }
    }

    public float DefaultFireRumbleMultiplier
    {
        get
        {
            return defaultFireRumbleMultiplier;
        }
    }

    public int WindowedWidth
    {
        get
        {
            return windowedWidth;
        }
    }

    public int WindowedHeight
    {
        get
        {
            return windowedHeight;
        }
    }
    #endregion
}

/// <summary>
/// Runtime Settings menu controller navigation defaults baked from the Settings Manager preset.
/// </summary>
[Serializable]
public sealed class GameSettingsManagerControllerNavigationSettings
{
    #region Constants
    public const string DefaultNavigateActionName = "UI/Navigate";
    public const string DefaultSubmitActionName = "UI/Submit";
    public const string DefaultCancelActionName = "UI/Cancel";
    public const float DefaultNavigateDeadzone = 0.5f;
    public const float DefaultRepeatDelaySeconds = 0.32f;
    public const float DefaultRepeatIntervalSeconds = 0.11f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Mode")]
    [Tooltip("Controls whether connected gamepads use the virtual mouse cursor, direct selectable navigation, or both at the same time.")]
    [SerializeField] private RuntimeMenuGamepadNavigationMode gamepadNavigationMode = RuntimeMenuGamepadNavigationMode.Hybrid;

    [Header("Actions")]
    [Tooltip("Input System action name or path used to move focus between tabs, dropdown headers and settings rows.")]
    [SerializeField] private string navigateActionName = DefaultNavigateActionName;

    [Tooltip("Input System action name or path used to activate the focused tab, dropdown header or setting.")]
    [SerializeField] private string submitActionName = DefaultSubmitActionName;

    [Tooltip("Input System action name or path used to close the Settings menu from controller navigation.")]
    [SerializeField] private string cancelActionName = DefaultCancelActionName;

    [Header("Repeat")]
    [Tooltip("Minimum absolute navigate input required before direct menu focus moves.")]
    [Range(0f, 1f)]
    [SerializeField] private float navigateDeadzone = DefaultNavigateDeadzone;

    [Tooltip("Unscaled seconds before a held navigate input repeats for direct controller navigation.")]
    [SerializeField] private float repeatDelaySeconds = DefaultRepeatDelaySeconds;

    [Tooltip("Unscaled seconds between repeated focus moves after the initial repeat delay.")]
    [SerializeField] private float repeatIntervalSeconds = DefaultRepeatIntervalSeconds;
    #endregion

    #endregion

    #region Properties
    public RuntimeMenuGamepadNavigationMode GamepadNavigationMode
    {
        get
        {
            return gamepadNavigationMode;
        }
    }

    public string NavigateActionName
    {
        get
        {
            return navigateActionName;
        }
    }

    public string SubmitActionName
    {
        get
        {
            return submitActionName;
        }
    }

    public string CancelActionName
    {
        get
        {
            return cancelActionName;
        }
    }

    public float NavigateDeadzone
    {
        get
        {
            return navigateDeadzone;
        }
    }

    public float RepeatDelaySeconds
    {
        get
        {
            return repeatDelaySeconds;
        }
    }

    public float RepeatIntervalSeconds
    {
        get
        {
            return repeatIntervalSeconds;
        }
    }
    #endregion
}

/// <summary>
/// FMOD event and bank reference used by one Settings menu audio-slider preview.
/// </summary>
[Serializable]
public sealed class GameSettingsManagerPreviewEventSettings
{
    #region Constants
    private const string DefaultSfxPreviewEventPath = "event:/SFX/Weapon/SFX_Shoot_Projectile";
    private const string DefaultSfxPreviewBankName = "BankSounds";
    private const string DefaultMusicPreviewEventPath = "event:/MUSIC/mus_past";
    private const string DefaultMusicPreviewBankName = "BankMusic";
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("FMOD event path used when previewing one Settings menu audio slider.")]
    [SerializeField] private string eventPath;

    [Tooltip("Optional FMOD bank loaded before the preview event is resolved. Leave empty only when the bank is loaded elsewhere.")]
    [SerializeField] private string bankName;
    #endregion

    #endregion

    #region Properties
    public string EventPath
    {
        get
        {
            return eventPath;
        }
    }

    public string BankName
    {
        get
        {
            return bankName;
        }
    }
    #endregion

    #region Construction
    /// <summary>
    /// Creates an empty preview event settings object for Unity serialization.
    /// </summary>
    public GameSettingsManagerPreviewEventSettings()
    {
    }

    /// <summary>
    /// Creates a preview event settings object with an authored event path and bank name.
    /// </summary>
    /// <param name="eventPath">FMOD event path to preview.</param>
    /// <param name="bankName">Optional FMOD bank containing the event.</param>
    private GameSettingsManagerPreviewEventSettings(string eventPath, string bankName)
    {
        this.eventPath = eventPath;
        this.bankName = bankName;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Creates the default short SFX preview used by Master and SFX sliders.
    /// </summary>
    /// <returns>Default SFX preview settings.</returns>
    public static GameSettingsManagerPreviewEventSettings CreateSfxDefault()
    {
        return new GameSettingsManagerPreviewEventSettings(DefaultSfxPreviewEventPath, DefaultSfxPreviewBankName);
    }

    /// <summary>
    /// Creates the default music preview used by the Music slider.
    /// </summary>
    /// <returns>Default music preview settings.</returns>
    public static GameSettingsManagerPreviewEventSettings CreateMusicDefault()
    {
        return new GameSettingsManagerPreviewEventSettings(DefaultMusicPreviewEventPath, DefaultMusicPreviewBankName);
    }
    #endregion
}
