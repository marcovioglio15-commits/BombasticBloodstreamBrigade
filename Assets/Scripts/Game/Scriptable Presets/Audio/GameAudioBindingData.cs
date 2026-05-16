using System;
using UnityEngine;

/// <summary>
/// Global playback options applied by the Game Audio playback system before dispatching FMOD events.
/// </summary>
[Serializable]
public sealed class GameAudioPlaybackSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("Enables runtime playback for requests emitted by gameplay ECS systems.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Master scalar multiplied into every game audio event volume.")]
    [SerializeField] private float masterVolume = 1f;

    [Tooltip("Fallback minimum 3D attenuation distance used when an event binding does not override it.")]
    [SerializeField] private float defaultMinimumDistance = 1f;

    [Tooltip("Fallback maximum 3D attenuation distance used when an event binding does not override it.")]
    [SerializeField] private float defaultMaximumDistance = 45f;

    [Tooltip("Logs missing FMOD event paths while running in the editor or a development build.")]
    [SerializeField] private bool logMissingEventPaths = true;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public float MasterVolume
    {
        get
        {
            return masterVolume;
        }
    }

    public float DefaultMinimumDistance
    {
        get
        {
            return defaultMinimumDistance;
        }
    }

    public float DefaultMaximumDistance
    {
        get
        {
            return defaultMaximumDistance;
        }
    }

    public bool LogMissingEventPaths
    {
        get
        {
            return logMissingEventPaths;
        }
    }
    #endregion
}

/// <summary>
/// FMOD bus path and routing values stored in the audio manager preset for project-level mixing control.
/// </summary>
[Serializable]
public sealed class GameAudioRoutingSettings
{
    #region Fields

    #region Serialized Fields
    [Header("FMOD Buses")]
    [Tooltip("FMOD master bus path used by game-wide volume automation.")]
    [SerializeField] private string masterBusPath = "bus:/";

    [Tooltip("FMOD SFX bus path used by moment-to-moment gameplay sound effects.")]
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [Tooltip("FMOD music bus path reserved for music control from future game management sections.")]
    [SerializeField] private string musicBusPath = "bus:/Music";

    [Tooltip("Default SFX bus volume scalar stored for tooling and future runtime bus automation.")]
    [SerializeField] private float sfxVolume = 1f;

    [Tooltip("Default music bus volume scalar stored for tooling and future runtime bus automation.")]
    [SerializeField] private float musicVolume = 1f;
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

    public float SfxVolume
    {
        get
        {
            return sfxVolume;
        }
    }

    public float MusicVolume
    {
        get
        {
            return musicVolume;
        }
    }
    #endregion
}

/// <summary>
/// Background music settings baked into the global audio singleton.
/// </summary>
[Serializable]
public sealed class GameAudioBackgroundMusicSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Background Music")]
    [Tooltip("Enables background music management from the Audio Manager preset.")]
    [SerializeField] private bool enabled;

    [Tooltip("FMOD event path for the background music loop, for example event:/Music/Stage01.")]
    [SerializeField] private string eventPath;

    [Tooltip("FMOD bank that contains the background music event. Leave empty only when the bank is loaded elsewhere before playback.")]
    [SerializeField] private string bankName = "BankMusic";

    [Tooltip("Volume scalar applied to background music before routing music volume.")]
    [Range(0f, 2f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("Starts background music automatically when runtime audio becomes available.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("When enabled, runtime playback restarts the music if the event path changes after rebake or config reload.")]
    [SerializeField] private bool restartWhenPathChanges = true;

    [Tooltip("Stops the currently playing background music when this section or global audio playback is disabled.")]
    [SerializeField] private bool stopWhenDisabled = true;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

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

    public float Volume
    {
        get
        {
            return volume;
        }
    }

    public bool AutoStart
    {
        get
        {
            return autoStart;
        }
    }

    public bool RestartWhenPathChanges
    {
        get
        {
            return restartWhenPathChanges;
        }
    }

    public bool StopWhenDisabled
    {
        get
        {
            return stopWhenDisabled;
        }
    }
    #endregion
}

/// <summary>
/// Per-event cap used to avoid flooding FMOD with dense ECS gameplay events.
/// </summary>
[Serializable]
public sealed class GameAudioRateLimitSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this binding can only play a limited number of times inside the configured time window.")]
    [SerializeField] private bool enabled;

    [Tooltip("Maximum play count allowed inside one rate-limit window. Values below one are reported by validation.")]
    [SerializeField] private int maxPlaysPerWindow;

    [Tooltip("Window length in seconds used by the per-event rate cap.")]
    [SerializeField] private float windowSeconds = 1f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public int MaxPlaysPerWindow
    {
        get
        {
            return maxPlaysPerWindow;
        }
    }

    public float WindowSeconds
    {
        get
        {
            return windowSeconds;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Writes the rate-limit values used by default enemy projectile audio to prevent burst spam.
    /// </summary>
    /// <param name="maxPlays">Maximum allowed plays in one window.</param>
    /// <param name="seconds">Window length in seconds.</param>
    public void Configure(int maxPlays, float seconds)
    {
        enabled = true;
        maxPlaysPerWindow = maxPlays;
        windowSeconds = seconds;
    }

    /// <summary>
    /// Disables the cap while keeping the authored numeric values visible in the inspector.
    /// </summary>
    public void Disable()
    {
        enabled = false;
    }
    #endregion
}

/// <summary>
/// One FMOD event binding exposed in the Audio Manager section and baked into ECS for runtime lookup.
/// </summary>
[Serializable]
public sealed class GameAudioEventBinding
{
    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable gameplay event identifier used by ECS systems.")]
    [SerializeField] private GameAudioEventId eventId;

    [Tooltip("Production-facing event code shown in the Game Management Tool.")]
    [SerializeField] private string eventCode;

    [Tooltip("Readable label used by tool panels.")]
    [SerializeField] private string displayName;

    [Tooltip("Short description of the gameplay moment that requests this event.")]
    [SerializeField] private string description;

    [Header("FMOD Event")]
    [Tooltip("FMOD event path, for example event:/SFX/Player/Shoot.")]
    [SerializeField] private string eventPath;

    [Tooltip("Volume scalar applied to this event before the global master volume.")]
    [SerializeField] private float volume = 1f;

    [Tooltip("Pitch scalar applied when the FMOD backend is enabled.")]
    [SerializeField] private float pitch = 1f;

    [Tooltip("When enabled and a request position is available, the event is emitted as 3D audio.")]
    [SerializeField] private bool spatialize = true;

    [Tooltip("Minimum 3D attenuation distance used by FMOD for this event.")]
    [SerializeField] private float minimumDistance = 1f;

    [Tooltip("Maximum 3D attenuation distance used by FMOD for this event.")]
    [SerializeField] private float maximumDistance = 45f;

    [Header("Rate Limit")]
    [Tooltip("Optional per-event cap that limits dense repeated requests over a short time window.")]
    [SerializeField] private GameAudioRateLimitSettings rateLimit = new GameAudioRateLimitSettings();
    #endregion

    #endregion

    #region Properties
    public GameAudioEventId EventId
    {
        get
        {
            return eventId;
        }
    }

    public string EventCode
    {
        get
        {
            return eventCode;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string EventPath
    {
        get
        {
            return eventPath;
        }
    }

    public float Volume
    {
        get
        {
            return volume;
        }
    }

    public float Pitch
    {
        get
        {
            return pitch;
        }
    }

    public bool Spatialize
    {
        get
        {
            return spatialize;
        }
    }

    public float MinimumDistance
    {
        get
        {
            return minimumDistance;
        }
    }

    public float MaximumDistance
    {
        get
        {
            return maximumDistance;
        }
    }

    public GameAudioRateLimitSettings RateLimit
    {
        get
        {
            return rateLimit;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Applies stable default identity metadata without touching user-authored FMOD event paths.
    /// </summary>
    /// <param name="definition">Default descriptor used by the preset factory.</param>
    public void ConfigureIdentity(GameAudioDefaultEventDefinition definition)
    {
        eventId = definition.EventId;
        eventCode = definition.EventCode;
        displayName = definition.DisplayName;
        description = definition.Description;

        if (rateLimit == null)
            rateLimit = new GameAudioRateLimitSettings();
    }

    /// <summary>
    /// Applies the default enemy projectile cap used by newly created presets.
    /// </summary>
    /// <param name="maxPlays">Maximum allowed plays in one window.</param>
    /// <param name="seconds">Window length in seconds.</param>
    public void ConfigureEnemyProjectileCap(int maxPlays, float seconds)
    {
        if (rateLimit == null)
            rateLimit = new GameAudioRateLimitSettings();

        rateLimit.Configure(maxPlays, seconds);
    }
    #endregion
}
