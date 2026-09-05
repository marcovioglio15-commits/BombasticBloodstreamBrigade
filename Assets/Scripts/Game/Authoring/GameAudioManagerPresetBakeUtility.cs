using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Converts Audio and Settings Manager presets into shared ECS data for both baking and regular-scene bootstrap.
/// </summary>
public static class GameAudioManagerPresetBakeUtility
{
    #region Constants
    private const string DefaultMasterBusPath = "bus:/";
    private const string DefaultSfxBusPath = "bus:/SFX";
    private const string DefaultMusicBusPath = "bus:/Music";
    private const int DefaultWindowedWidth = 1280;
    private const int DefaultWindowedHeight = 720;
    private const int DefaultFrameRateLimit = 60;
    #endregion

    #region Methods

    #region Config Builders
    /// <summary>
    /// Builds the runtime audio singleton config from playback, routing, and background-music settings.
    /// </summary>
    /// <param name="preset">Source Audio Manager preset.</param>
    /// <returns>Baked runtime audio config.</returns>
    public static GameAudioRuntimeConfig BuildAudioRuntimeConfig(GameAudioManagerPreset preset)
    {
        GameAudioPlaybackSettings playbackSettings = preset != null ? preset.PlaybackSettings : null;
        GameAudioRoutingSettings routingSettings = preset != null ? preset.RoutingSettings : null;
        GameAudioBackgroundMusicSettings backgroundMusicSettings =
            preset != null ? preset.BackgroundMusicSettings : null;

        if (playbackSettings == null)
        {
            return new GameAudioRuntimeConfig
            {
                Enabled = 0,
                LogMissingEventPaths = 1,
                BackgroundMusicEnabled = 0,
                BackgroundMusicAutoStart = 0,
                BackgroundMusicRestartWhenPathChanges = 0,
                BackgroundMusicStopWhenDisabled = 1,
                BackgroundMusicEventPath = default,
                BackgroundMusicBankName = default,
                MasterVolume = 0f,
                BackgroundMusicVolume = 0f,
                DefaultMinimumDistance = 8f,
                DefaultMaximumDistance = 45f
            };
        }

        float musicRoutingVolume = routingSettings != null ? math.max(0f, routingSettings.MusicVolume) : 1f;
        string backgroundMusicPath = backgroundMusicSettings != null
            ? backgroundMusicSettings.EventPath
            : string.Empty;
        string backgroundMusicBankName = backgroundMusicSettings != null
            ? backgroundMusicSettings.BankName
            : string.Empty;
        float backgroundMusicVolume = backgroundMusicSettings != null
            ? math.max(0f, backgroundMusicSettings.Volume) * musicRoutingVolume
            : 0f;

        return new GameAudioRuntimeConfig
        {
            Enabled = playbackSettings.Enabled ? (byte)1 : (byte)0,
            LogMissingEventPaths = playbackSettings.LogMissingEventPaths ? (byte)1 : (byte)0,
            BackgroundMusicEnabled = backgroundMusicSettings != null && backgroundMusicSettings.Enabled
                ? (byte)1
                : (byte)0,
            BackgroundMusicAutoStart = backgroundMusicSettings != null && backgroundMusicSettings.AutoStart
                ? (byte)1
                : (byte)0,
            BackgroundMusicRestartWhenPathChanges =
                backgroundMusicSettings != null && backgroundMusicSettings.RestartWhenPathChanges
                    ? (byte)1
                    : (byte)0,
            BackgroundMusicStopWhenDisabled = backgroundMusicSettings == null || backgroundMusicSettings.StopWhenDisabled
                ? (byte)1
                : (byte)0,
            BackgroundMusicEventPath = new FixedString512Bytes(backgroundMusicPath ?? string.Empty),
            BackgroundMusicBankName = new FixedString64Bytes(backgroundMusicBankName ?? string.Empty),
            MasterVolume = math.max(0f, playbackSettings.MasterVolume),
            BackgroundMusicVolume = backgroundMusicVolume,
            BossMusic = BuildMusicTrack(preset.BossMusicSettings, musicRoutingVolume),
            MainMenuMusic = BuildMusicTrack(preset.MainMenuMusicSettings, musicRoutingVolume),
            MusicCrossfadeSeconds = math.isfinite(preset.MusicCrossfadeSeconds) && preset.MusicCrossfadeSeconds > 0f
                ? preset.MusicCrossfadeSeconds
                : 1.5f,
            DefaultMinimumDistance = math.max(0f, playbackSettings.DefaultMinimumDistance),
            DefaultMaximumDistance = math.max(playbackSettings.DefaultMinimumDistance,
                                              playbackSettings.DefaultMaximumDistance)
        };
    }

    /// <summary>
    /// Compiles an independent music event through the same builder for bake and regular-scene bootstrap.
    /// </summary>
    /// <param name="settings">Music settings from the Audio Manager preset.</param>
    /// <param name="routingVolume">Preset music routing multiplier.</param>
    /// <returns>Complete ECS event identity and playback controls.</returns>
    public static GameAudioMusicTrackConfig BuildMusicTrack(GameAudioBackgroundMusicSettings settings, float routingVolume)
    {
        if (settings == null)
            return default;

        return new GameAudioMusicTrackConfig
        {
            Enabled = settings.Enabled ? (byte)1 : (byte)0,
            AutoStart = settings.AutoStart ? (byte)1 : (byte)0,
            RestartWhenPathChanges = settings.RestartWhenPathChanges ? (byte)1 : (byte)0,
            StopWhenDisabled = settings.StopWhenDisabled ? (byte)1 : (byte)0,
            EventPath = new FixedString512Bytes(settings.EventPath ?? string.Empty),
            BankName = new FixedString64Bytes(settings.BankName ?? string.Empty),
            Volume = math.isfinite(settings.Volume) ? math.max(0f, settings.Volume) * routingVolume : 0f
        };
    }

    /// <summary>
    /// Builds the runtime Settings singleton from Settings Manager defaults and controller-navigation options.
    /// </summary>
    /// <param name="preset">Source Settings Manager preset, or null to use project defaults.</param>
    /// <returns>Baked Settings runtime config.</returns>
    public static GameSettingsRuntimeConfig BuildSettingsRuntimeConfig(GameSettingsManagerPreset preset)
    {
        GameSettingsManagerAudioSettings audioSettings = preset != null ? preset.AudioSettings : null;
        GameSettingsManagerExperienceSettings experienceSettings = preset != null ? preset.ExperienceSettings : null;
        GameSettingsManagerControllerNavigationSettings navigationSettings =
            preset != null ? preset.ControllerNavigationSettings : null;
        GameSettingsManagerPreviewEventSettings masterPreview =
            audioSettings != null && audioSettings.MasterVolumePreview != null
                ? audioSettings.MasterVolumePreview
                : GameSettingsManagerPreviewEventSettings.CreateSfxDefault();
        GameSettingsManagerPreviewEventSettings sfxPreview =
            audioSettings != null && audioSettings.SfxVolumePreview != null
                ? audioSettings.SfxVolumePreview
                : GameSettingsManagerPreviewEventSettings.CreateSfxDefault();
        GameSettingsManagerPreviewEventSettings musicPreview =
            audioSettings != null && audioSettings.MusicVolumePreview != null
                ? audioSettings.MusicVolumePreview
                : GameSettingsManagerPreviewEventSettings.CreateMusicDefault();

        return new GameSettingsRuntimeConfig
        {
            MasterBusPath = new FixedString128Bytes(ResolveString(
                audioSettings != null ? audioSettings.MasterBusPath : null,
                DefaultMasterBusPath)),
            SfxBusPath = new FixedString128Bytes(ResolveString(
                audioSettings != null ? audioSettings.SfxBusPath : null,
                DefaultSfxBusPath)),
            MusicBusPath = new FixedString128Bytes(ResolveString(
                audioSettings != null ? audioSettings.MusicBusPath : null,
                DefaultMusicBusPath)),
            MasterPreviewEventPath = new FixedString512Bytes(ResolvePreviewEventPath(masterPreview)),
            MasterPreviewBankName = new FixedString64Bytes(ResolvePreviewBankName(masterPreview)),
            SfxPreviewEventPath = new FixedString512Bytes(ResolvePreviewEventPath(sfxPreview)),
            SfxPreviewBankName = new FixedString64Bytes(ResolvePreviewBankName(sfxPreview)),
            MusicPreviewEventPath = new FixedString512Bytes(ResolvePreviewEventPath(musicPreview)),
            MusicPreviewBankName = new FixedString64Bytes(ResolvePreviewBankName(musicPreview)),
            MasterPreviewPlaysAllOthers = audioSettings != null && audioSettings.MasterPlaysAllPreviews
                ? (byte)1
                : (byte)0,
            DefaultMasterVolume = math.clamp(audioSettings != null ? audioSettings.DefaultMasterVolume : 1f, 0f, 1f),
            DefaultSfxVolume = math.clamp(audioSettings != null ? audioSettings.DefaultSfxVolume : 1f, 0f, 1f),
            DefaultMusicVolume = math.clamp(audioSettings != null ? audioSettings.DefaultMusicVolume : 1f, 0f, 1f),
            DefaultVisualPointerEnabled = experienceSettings == null || experienceSettings.DefaultVisualPointerEnabled
                ? (byte)1
                : (byte)0,
            DefaultFullscreenEnabled = experienceSettings == null || experienceSettings.DefaultFullscreenEnabled
                ? (byte)1
                : (byte)0,
            DefaultFrameRateLimit = ResolveFrameRateLimit(
                experienceSettings != null ? (int)experienceSettings.DefaultFrameRateLimit : DefaultFrameRateLimit),
            DefaultDamageRumbleMultiplier = math.clamp(
                experienceSettings != null ? experienceSettings.DefaultDamageRumbleMultiplier : 1f,
                0f,
                2f),
            DefaultFireRumbleMultiplier = math.clamp(
                experienceSettings != null ? experienceSettings.DefaultFireRumbleMultiplier : 1f,
                0f,
                2f),
            WindowedWidth = experienceSettings != null
                ? math.max(1, experienceSettings.WindowedWidth)
                : DefaultWindowedWidth,
            WindowedHeight = experienceSettings != null
                ? math.max(1, experienceSettings.WindowedHeight)
                : DefaultWindowedHeight,
            GamepadNavigationMode = navigationSettings != null
                ? navigationSettings.GamepadNavigationMode
                : RuntimeMenuGamepadNavigationMode.Hybrid,
            NavigateActionName = new FixedString64Bytes(ResolveString(
                navigationSettings != null ? navigationSettings.NavigateActionName : null,
                GameSettingsManagerControllerNavigationSettings.DefaultNavigateActionName)),
            SubmitActionName = new FixedString64Bytes(ResolveString(
                navigationSettings != null ? navigationSettings.SubmitActionName : null,
                GameSettingsManagerControllerNavigationSettings.DefaultSubmitActionName)),
            CancelActionName = new FixedString64Bytes(ResolveString(
                navigationSettings != null ? navigationSettings.CancelActionName : null,
                GameSettingsManagerControllerNavigationSettings.DefaultCancelActionName)),
            NavigateDeadzone = math.clamp(
                navigationSettings != null
                    ? navigationSettings.NavigateDeadzone
                    : GameSettingsManagerControllerNavigationSettings.DefaultNavigateDeadzone,
                0f,
                1f),
            NavigationRepeatDelaySeconds = math.max(
                0f,
                navigationSettings != null
                    ? navigationSettings.RepeatDelaySeconds
                    : GameSettingsManagerControllerNavigationSettings.DefaultRepeatDelaySeconds),
            NavigationRepeatIntervalSeconds = math.max(
                0.01f,
                navigationSettings != null
                    ? navigationSettings.RepeatIntervalSeconds
                    : GameSettingsManagerControllerNavigationSettings.DefaultRepeatIntervalSeconds)
        };
    }

    /// <summary>
    /// Builds safe automatic data-collection runtime settings without changing invalid authored values.
    /// </summary>
    /// <param name="settingsPreset">Source Settings Manager preset, or null to use disabled-safe defaults.</param>
    /// <param name="dataPreset">Global Data Collection Manager preset controlling feature availability.</param>
    /// <returns>Baked data-collection config consumed by ECS producers and the managed HTTPS boundary.</returns>
    public static GameDataCollectionRuntimeConfig BuildDataCollectionRuntimeConfig(
        GameSettingsManagerPreset settingsPreset,
        GameDataCollectionManagerPreset dataPreset)
    {
        GameDataCollectionSettings settings = settingsPreset != null
            ? settingsPreset.DataCollectionSettings
            : null;
        string serviceBaseUrl = ResolveString(settings != null ? settings.ServiceBaseUrl : null,
                                              GameDataCollectionSettings.DefaultServiceBaseUrl);
        string schemaVersion = ResolveString(settings != null ? settings.SchemaVersion : null,
                                             GameDataCollectionSettings.DefaultSchemaVersion);
        string consentPolicyVersion = ResolveString(settings != null ? settings.ConsentPolicyVersion : null,
                                                    GameDataCollectionSettings.DefaultConsentPolicyVersion);
        string revealActionId = ResolveString(settings != null ? settings.RevealDevActionsActionId : null,
                                              GameDataCollectionSettings.DefaultRevealDevActionsActionName);

        return new GameDataCollectionRuntimeConfig
        {
            Enabled = dataPreset != null &&
                      dataPreset.DataCollectionEnabled &&
                      settings != null
                ? (byte)1
                : (byte)0,
            CollectInEditor = settings != null && settings.CollectInEditor ? (byte)1 : (byte)0,
            Environment = settings != null
                ? settings.Environment
                : GameDataCollectionEnvironment.Development,
            ServiceBaseUrl = BuildFixedString512(serviceBaseUrl),
            SchemaVersion = BuildFixedString64(schemaVersion),
            ConsentPolicyVersion = BuildFixedString64(consentPolicyVersion),
            RevealDevActionsActionId = BuildFixedString64(revealActionId),
            PerformanceSampleIntervalSeconds = math.max(
                0.1f,
                settings != null
                    ? settings.PerformanceSampleIntervalSeconds
                    : GameDataCollectionSettings.DefaultPerformanceSampleIntervalSeconds),
            RenderingSampleIntervalSeconds = math.max(
                0.25f,
                settings != null
                    ? settings.RenderingSampleIntervalSeconds
                    : GameDataCollectionSettings.DefaultRenderingSampleIntervalSeconds),
            UploadIntervalSeconds = math.max(
                1f,
                settings != null
                    ? settings.UploadIntervalSeconds
                    : GameDataCollectionSettings.DefaultUploadIntervalSeconds),
            MaximumEventsPerBatch = math.clamp(
                settings != null
                    ? settings.MaximumEventsPerBatch
                    : GameDataCollectionSettings.DefaultMaximumEventsPerBatch,
                1,
                100),
            MaximumPendingEvents = math.max(
                1,
                settings != null
                    ? settings.MaximumPendingEvents
                    : GameDataCollectionSettings.DefaultMaximumPendingEvents),
            MaximumPayloadBytes = math.clamp(
                settings != null
                    ? settings.MaximumPayloadBytes
                    : GameDataCollectionSettings.DefaultMaximumPayloadBytes,
                4096,
                1048576),
            RequestTimeoutSeconds = math.max(
                1f,
                settings != null
                    ? settings.RequestTimeoutSeconds
                    : GameDataCollectionSettings.DefaultRequestTimeoutSeconds),
            InitialRetryDelaySeconds = math.max(
                0.5f,
                settings != null
                    ? settings.InitialRetryDelaySeconds
                    : GameDataCollectionSettings.DefaultInitialRetryDelaySeconds),
            MaximumRetryDelaySeconds = math.max(
                settings != null
                    ? settings.InitialRetryDelaySeconds
                    : GameDataCollectionSettings.DefaultInitialRetryDelaySeconds,
                settings != null
                    ? settings.MaximumRetryDelaySeconds
                    : GameDataCollectionSettings.DefaultMaximumRetryDelaySeconds),
            PersistPendingEvents = settings != null && settings.PersistPendingEvents ? (byte)1 : (byte)0,
            PendingEventRetentionDays = math.clamp(
                settings != null
                    ? settings.PendingEventRetentionDays
                    : GameDataCollectionSettings.DefaultPendingEventRetentionDays,
                1,
                30),
            DashboardPageSize = math.clamp(
                settings != null
                    ? settings.DashboardPageSize
                    : GameDataCollectionSettings.DefaultDashboardPageSize,
                1,
                100)
        };
    }
    #endregion

    #region Buffer Population
    /// <summary>
    /// Populates one ECS audio binding buffer while skipping null and None entries.
    /// </summary>
    /// <param name="preset">Source Audio Manager preset.</param>
    /// <param name="bindingBuffer">Output binding buffer on the audio singleton.</param>
    public static void PopulateBindingBuffer(GameAudioManagerPreset preset,
                                             DynamicBuffer<GameAudioEventBindingElement> bindingBuffer)
    {
        bindingBuffer.Clear();

        if (preset == null)
            return;

        IReadOnlyList<GameAudioEventBinding> eventBindings = preset.EventBindings;

        if (eventBindings == null)
            return;

        for (int index = 0; index < eventBindings.Count; index++)
        {
            GameAudioEventBinding binding = eventBindings[index];

            if (binding == null || binding.EventId == GameAudioEventId.None)
                continue;

            bindingBuffer.Add(BuildBindingElement(binding));
        }
    }

    /// <summary>
    /// Converts one authored audio binding into a runtime ECS element.
    /// </summary>
    /// <param name="binding">Source event binding.</param>
    /// <returns>Baked binding element.</returns>
    private static GameAudioEventBindingElement BuildBindingElement(GameAudioEventBinding binding)
    {
        GameAudioRateLimitSettings rateLimit = binding.RateLimit;

        return new GameAudioEventBindingElement
        {
            EventId = binding.EventId,
            EventCode = new FixedString64Bytes(binding.EventCode ?? string.Empty),
            EventPath = new FixedString512Bytes(binding.EventPath ?? string.Empty),
            Volume = math.max(0f, binding.Volume),
            Pitch = math.max(0.0001f, binding.Pitch),
            Spatialize = binding.Spatialize ? (byte)1 : (byte)0,
            MinimumDistance = math.max(0f, binding.MinimumDistance),
            MaximumDistance = math.max(binding.MinimumDistance, binding.MaximumDistance),
            SingleInstance = binding.SingleInstance ? (byte)1 : (byte)0,
            RateLimitEnabled = rateLimit != null && rateLimit.Enabled ? (byte)1 : (byte)0,
            MaxPlaysPerWindow = rateLimit != null ? math.max(0, rateLimit.MaxPlaysPerWindow) : 0,
            WindowSeconds = rateLimit != null ? math.max(0f, rateLimit.WindowSeconds) : 0f
        };
    }
    #endregion

    #region Value Resolution
    /// <summary>
    /// Resolves a supported target frame-rate value for runtime Settings data.
    /// </summary>
    /// <param name="frameRateLimit">Authored frame-rate limit.</param>
    /// <returns>Supported target frame rate in frames per second.</returns>
    private static int ResolveFrameRateLimit(int frameRateLimit)
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

    /// <summary>
    /// Copies one managed string into a bounded 64-byte ECS string without allowing invalid authoring to break baking.
    /// </summary>
    /// <param name="value">Managed source value.</param>
    /// <returns>Bounded ECS string containing as much of the source as fits.</returns>
    private static FixedString64Bytes BuildFixedString64(string value)
    {
        FixedString64Bytes result = default;
        result.CopyFromTruncated(value ?? string.Empty);
        return result;
    }

    /// <summary>
    /// Copies one managed string into a bounded 512-byte ECS string without allowing invalid authoring to break baking.
    /// </summary>
    /// <param name="value">Managed source value.</param>
    /// <returns>Bounded ECS string containing as much of the source as fits.</returns>
    private static FixedString512Bytes BuildFixedString512(string value)
    {
        FixedString512Bytes result = default;
        result.CopyFromTruncated(value ?? string.Empty);
        return result;
    }

    /// <summary>
    /// Resolves one authored string with a runtime fallback.
    /// </summary>
    /// <param name="value">Authored string value.</param>
    /// <param name="fallback">Fallback used when the value is empty.</param>
    /// <returns>Trimmed authored value or the fallback.</returns>
    private static string ResolveString(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    /// <summary>
    /// Resolves one Settings preview event path for ECS runtime data.
    /// </summary>
    /// <param name="preview">Preview event settings from the Settings Manager preset.</param>
    /// <returns>Trimmed FMOD event path or an empty string.</returns>
    private static string ResolvePreviewEventPath(GameSettingsManagerPreviewEventSettings preview)
    {
        if (preview == null || string.IsNullOrWhiteSpace(preview.EventPath))
            return string.Empty;

        return preview.EventPath.Trim();
    }

    /// <summary>
    /// Resolves one Settings preview bank name for ECS runtime data.
    /// </summary>
    /// <param name="preview">Preview event settings from the Settings Manager preset.</param>
    /// <returns>Trimmed FMOD bank name or an empty string.</returns>
    private static string ResolvePreviewBankName(GameSettingsManagerPreviewEventSettings preview)
    {
        if (preview == null || string.IsNullOrWhiteSpace(preview.BankName))
            return string.Empty;

        return preview.BankName.Trim();
    }
    #endregion

    #endregion
}
