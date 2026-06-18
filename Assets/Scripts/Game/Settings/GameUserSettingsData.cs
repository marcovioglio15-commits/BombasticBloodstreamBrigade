using Unity.Entities;

/// <summary>
/// Runtime user preferences shared by menu UI, FMOD bus control, screen mode and ECS presentation systems.
/// </summary>
public struct GameUserSettingsData
{
    #region Fields
    public float MasterVolume;
    public float SfxVolume;
    public float MusicVolume;
    public byte VisualPointerEnabled;
    public byte FullscreenEnabled;
    public int FrameRateLimit;
    public float DamageRumbleMultiplier;
    public float FireRumbleMultiplier;
    #endregion
}

/// <summary>
/// Optional ECS singleton mirroring the local user experience settings consumed by presentation systems.
/// </summary>
public struct PlayerUserExperienceSettings : IComponentData
{
    #region Fields
    public byte VisualPointerEnabled;
    public float DamageRumbleMultiplier;
    public float FireRumbleMultiplier;
    #endregion
}

/// <summary>
/// FMOD bus paths used when applying local audio preferences to the active runtime backend.
/// </summary>
public readonly struct GameUserSettingsAudioBusPaths
{
    #region Fields
    public readonly string MasterBusPath;
    public readonly string SfxBusPath;
    public readonly string MusicBusPath;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable bus-path set for audio preference application.
    /// </summary>
    /// <param name="masterBusPath">FMOD master bus path.</param>
    /// <param name="sfxBusPath">FMOD SFX bus path.</param>
    /// <param name="musicBusPath">FMOD music bus path.</param>
    public GameUserSettingsAudioBusPaths(string masterBusPath, string sfxBusPath, string musicBusPath)
    {
        MasterBusPath = masterBusPath;
        SfxBusPath = sfxBusPath;
        MusicBusPath = musicBusPath;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Returns the project default FMOD bus paths used by the shipped audio preset.
    /// </summary>
    /// <returns>Default immutable FMOD bus-path set.</returns>
    public static GameUserSettingsAudioBusPaths CreateDefault()
    {
        return new GameUserSettingsAudioBusPaths("bus:/", "bus:/SFX", "bus:/Music");
    }
    #endregion
}

/// <summary>
/// Project-level windowed resolution used when applying fullscreen user preferences.
/// </summary>
public readonly struct GameUserSettingsWindowedDisplaySettings
{
    #region Fields
    public readonly int Width;
    public readonly int Height;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable windowed display size.
    /// </summary>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    public GameUserSettingsWindowedDisplaySettings(int width, int height)
    {
        Width = width;
        Height = height;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Returns the fallback windowed display size used when no Audio Manager runtime config is available.
    /// </summary>
    /// <returns>Default immutable windowed display settings.</returns>
    public static GameUserSettingsWindowedDisplaySettings CreateDefault()
    {
        return new GameUserSettingsWindowedDisplaySettings(1280, 720);
    }
    #endregion
}

/// <summary>
/// Runtime options needed to apply local user settings without hardcoding project-level Audio Manager values.
/// </summary>
public readonly struct GameUserSettingsRuntimeOptions
{
    #region Fields
    public readonly GameUserSettingsAudioBusPaths AudioBusPaths;
    public readonly GameUserSettingsWindowedDisplaySettings WindowedDisplay;
    public readonly GameUserSettingsData DefaultSettings;
    public readonly RuntimeMenuGamepadNavigationOptions MenuNavigation;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates immutable runtime options for applying local user settings.
    /// </summary>
    /// <param name="audioBusPaths">FMOD bus paths used by Settings menu volume sliders.</param>
    /// <param name="windowedDisplay">Windowed display size used when fullscreen is disabled.</param>
    public GameUserSettingsRuntimeOptions(in GameUserSettingsAudioBusPaths audioBusPaths,
                                          in GameUserSettingsWindowedDisplaySettings windowedDisplay)
    {
        AudioBusPaths = audioBusPaths;
        WindowedDisplay = windowedDisplay;
        DefaultSettings = GameUserSettingsStore.CreateDefaults();
        MenuNavigation = RuntimeMenuGamepadNavigationOptions.CreateDefault();
    }

    /// <summary>
    /// Creates immutable runtime options for applying local user settings with project-authored reset defaults.
    /// </summary>
    /// <param name="audioBusPaths">FMOD bus paths used by Settings menu volume sliders.</param>
    /// <param name="windowedDisplay">Windowed display size used when fullscreen is disabled.</param>
    /// <param name="defaultSettings">Settings restored by Reset Defaults and first-run loading.</param>
    public GameUserSettingsRuntimeOptions(in GameUserSettingsAudioBusPaths audioBusPaths,
                                          in GameUserSettingsWindowedDisplaySettings windowedDisplay,
                                          in GameUserSettingsData defaultSettings)
        : this(in audioBusPaths,
               in windowedDisplay,
               in defaultSettings,
               RuntimeMenuGamepadNavigationOptions.CreateDefault())
    {
    }

    /// <summary>
    /// Creates immutable runtime options for applying local user settings with project-authored reset defaults and
    /// controller navigation options.
    /// </summary>
    /// <param name="audioBusPaths">FMOD bus paths used by Settings menu volume sliders.</param>
    /// <param name="windowedDisplay">Windowed display size used when fullscreen is disabled.</param>
    /// <param name="defaultSettings">Settings restored by Reset Defaults and first-run loading.</param>
    /// <param name="menuNavigation">Controller navigation options used by runtime menu overlays.</param>
    public GameUserSettingsRuntimeOptions(in GameUserSettingsAudioBusPaths audioBusPaths,
                                          in GameUserSettingsWindowedDisplaySettings windowedDisplay,
                                          in GameUserSettingsData defaultSettings,
                                          in RuntimeMenuGamepadNavigationOptions menuNavigation)
    {
        AudioBusPaths = audioBusPaths;
        WindowedDisplay = windowedDisplay;
        DefaultSettings = GameUserSettingsStore.ClampForRuntime(defaultSettings);
        MenuNavigation = menuNavigation;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Returns fallback runtime options used before the ECS Audio Manager config exists.
    /// </summary>
    /// <returns>Default immutable runtime options.</returns>
    public static GameUserSettingsRuntimeOptions CreateDefault()
    {
        GameUserSettingsAudioBusPaths audioBusPaths = GameUserSettingsAudioBusPaths.CreateDefault();
        GameUserSettingsWindowedDisplaySettings windowedDisplay = GameUserSettingsWindowedDisplaySettings.CreateDefault();
        GameUserSettingsData defaultSettings = GameUserSettingsStore.CreateDefaults();
        RuntimeMenuGamepadNavigationOptions menuNavigation = RuntimeMenuGamepadNavigationOptions.CreateDefault();
        return new GameUserSettingsRuntimeOptions(in audioBusPaths, in windowedDisplay, in defaultSettings, in menuNavigation);
    }
    #endregion
}

/// <summary>
/// Immutable controller-navigation options used by runtime menu overlays when a gamepad is connected.
/// </summary>
public readonly struct RuntimeMenuGamepadNavigationOptions
{
    #region Fields
    public readonly RuntimeMenuGamepadNavigationMode Mode;
    public readonly string NavigateActionName;
    public readonly string SubmitActionName;
    public readonly string CancelActionName;
    public readonly float NavigateDeadzone;
    public readonly float RepeatDelaySeconds;
    public readonly float RepeatIntervalSeconds;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates immutable runtime controller-navigation options.
    /// </summary>
    /// <param name="mode">Gamepad navigation mode selected by the Settings Manager preset.</param>
    /// <param name="navigateActionName">Input action used to move direct UI focus.</param>
    /// <param name="submitActionName">Input action used to submit the focused UI option.</param>
    /// <param name="cancelActionName">Input action used to close the menu.</param>
    /// <param name="navigateDeadzone">Minimum direct-navigation input magnitude.</param>
    /// <param name="repeatDelaySeconds">Initial held-input repeat delay.</param>
    /// <param name="repeatIntervalSeconds">Held-input repeat cadence after the delay.</param>
    public RuntimeMenuGamepadNavigationOptions(RuntimeMenuGamepadNavigationMode mode,
                                               string navigateActionName,
                                               string submitActionName,
                                               string cancelActionName,
                                               float navigateDeadzone,
                                               float repeatDelaySeconds,
                                               float repeatIntervalSeconds)
    {
        Mode = mode;
        NavigateActionName = navigateActionName;
        SubmitActionName = submitActionName;
        CancelActionName = cancelActionName;
        NavigateDeadzone = navigateDeadzone;
        RepeatDelaySeconds = repeatDelaySeconds;
        RepeatIntervalSeconds = repeatIntervalSeconds;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Returns the project fallback controller-navigation options used before baked Settings config exists.
    /// </summary>
    /// <returns>Default immutable controller-navigation options.</returns>
    public static RuntimeMenuGamepadNavigationOptions CreateDefault()
    {
        return new RuntimeMenuGamepadNavigationOptions(RuntimeMenuGamepadNavigationMode.Hybrid,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultNavigateActionName,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultSubmitActionName,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultCancelActionName,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultNavigateDeadzone,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultRepeatDelaySeconds,
                                                       GameSettingsManagerControllerNavigationSettings.DefaultRepeatIntervalSeconds);
    }
    #endregion
}

/// <summary>
/// Runtime preview event used by one Settings menu audio slider.
/// </summary>
public readonly struct GameAudioSettingsPreviewEvent
{
    #region Fields
    public readonly string EventPath;
    public readonly string BankName;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable settings preview event reference.
    /// </summary>
    /// <param name="eventPath">FMOD event path to preview.</param>
    /// <param name="bankName">Optional FMOD bank loaded before playback.</param>
    public GameAudioSettingsPreviewEvent(string eventPath, string bankName)
    {
        EventPath = eventPath;
        BankName = bankName;
    }
    #endregion
}

/// <summary>
/// Runtime preview event set used by the Settings menu audio sliders.
/// </summary>
public readonly struct GameAudioSettingsPreviewSet
{
    #region Fields
    public readonly GameAudioSettingsPreviewEvent Master;
    public readonly GameAudioSettingsPreviewEvent Sfx;
    public readonly GameAudioSettingsPreviewEvent Music;
    public readonly bool MasterPlaysAllOthers;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable preview set for Master, SFX and Music sliders.
    /// </summary>
    /// <param name="master">Preview event for the Master volume slider.</param>
    /// <param name="sfx">Preview event for the SFX volume slider.</param>
    /// <param name="music">Preview event for the Music volume slider.</param>
    /// <param name="masterPlaysAllOthers">True when the Master slider previews the SFX and Music events together instead of its own event.</param>
    public GameAudioSettingsPreviewSet(GameAudioSettingsPreviewEvent master,
                                       GameAudioSettingsPreviewEvent sfx,
                                       GameAudioSettingsPreviewEvent music,
                                       bool masterPlaysAllOthers)
    {
        Master = master;
        Sfx = sfx;
        Music = music;
        MasterPlaysAllOthers = masterPlaysAllOthers;
    }
    #endregion
}
