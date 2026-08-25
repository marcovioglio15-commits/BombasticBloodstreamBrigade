using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Scene authoring component that bakes selected Audio, Settings and HUD Manager presets into ECS singletons.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameAudioManagerAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Game master preset used to resolve the Audio Manager sub-preset.")]
    [SerializeField] private GameMasterPreset masterPreset;

    [Tooltip("Direct Audio Manager preset fallback used when Master Preset is missing or has no Audio Manager assigned.")]
    [SerializeField] private GameAudioManagerPreset audioManagerPreset;

    [Tooltip("Direct Settings Manager preset fallback used when Master Preset is missing or has no Settings Manager assigned.")]
    [SerializeField] private GameSettingsManagerPreset settingsManagerPreset;

    [Tooltip("Direct HUD Manager preset fallback used when Master Preset is missing or has no HUD Manager assigned.")]
    [SerializeField] private GameHudManagerPreset hudManagerPreset;
    #endregion

    #endregion

    #region Properties
    public GameMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public GameAudioManagerPreset AudioManagerPreset
    {
        get
        {
            return audioManagerPreset;
        }
    }

    public GameSettingsManagerPreset SettingsManagerPreset
    {
        get
        {
            return settingsManagerPreset;
        }
    }

    public GameHudManagerPreset HudManagerPreset
    {
        get
        {
            return hudManagerPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the effective Audio Manager preset used by baking.
    /// </summary>
    /// <returns>Audio Manager preset from MasterPreset or direct fallback.</returns>
    public GameAudioManagerPreset ResolveAudioManagerPreset()
    {
        if (masterPreset != null && masterPreset.AudioManagerPreset != null)
            return masterPreset.AudioManagerPreset;

        return audioManagerPreset;
    }

    /// <summary>
    /// Resolves the effective Settings Manager preset used by baking.
    /// </summary>
    /// <returns>Settings Manager preset from MasterPreset or direct fallback.</returns>
    public GameSettingsManagerPreset ResolveSettingsManagerPreset()
    {
        if (masterPreset != null && masterPreset.SettingsManagerPreset != null)
            return masterPreset.SettingsManagerPreset;

        return settingsManagerPreset;
    }

    /// <summary>
    /// Resolves the effective HUD Manager preset used by baking.
    /// </summary>
    /// <returns>HUD Manager preset from MasterPreset or direct fallback.</returns>
    public GameHudManagerPreset ResolveHudManagerPreset()
    {
        if (masterPreset != null && masterPreset.HudManagerPreset != null)
            return masterPreset.HudManagerPreset;

        return hudManagerPreset;
    }
    #endregion

    #endregion
}

/// <summary>
/// Baker that converts GameAudioManagerAuthoring into singleton audio config, settings config and event buffers.
/// </summary>
public sealed class GameAudioManagerAuthoringBaker : Baker<GameAudioManagerAuthoring>
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

    #region Bake
    /// <summary>
    /// Bakes global Settings config, audio config and all event mappings from the selected presets.
    /// </summary>
    /// <param name="authoring">Scene authoring component that chooses the preset.</param>
    public override void Bake(GameAudioManagerAuthoring authoring)
    {
        if (authoring == null)
            return;

        DeclarePresetDependencies(authoring);
        GameAudioManagerPreset audioPreset = authoring.ResolveAudioManagerPreset();
        GameSettingsManagerPreset settingsPreset = authoring.ResolveSettingsManagerPreset();
        GameHudManagerPreset hudPreset = authoring.ResolveHudManagerPreset();

        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, BuildSettingsRuntimeConfig(settingsPreset));
        AddComponent(entity, GameHudManagerPresetBakeUtility.BuildConfig(hudPreset));
        GameHudPowerUpSummarySettings summarySettings = hudPreset != null ? hudPreset.PowerUpSummarySettings : null;
        AddComponent(entity, GameHudSupplementalPresetBakeUtility.BuildSummaryConfig(summarySettings));
        DynamicBuffer<GamePowerUpSummaryStatisticElement> statisticBuffer = AddBuffer<GamePowerUpSummaryStatisticElement>(entity);
        GameHudSupplementalPresetBakeUtility.PopulateStatisticBuffer(summarySettings, statisticBuffer);

        if (audioPreset == null)
            return;

        AddComponent(entity, BuildRuntimeConfig(audioPreset));
        DynamicBuffer<GameAudioEventBindingElement> bindingBuffer = AddBuffer<GameAudioEventBindingElement>(entity);
        DynamicBuffer<GameAudioEventRequest> requestBuffer = AddBuffer<GameAudioEventRequest>(entity);
        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStateBuffer = AddBuffer<GameAudioRateLimitStateElement>(entity);
        PopulateBindingBuffer(audioPreset, bindingBuffer);
        requestBuffer.Clear();
        rateLimitStateBuffer.Clear();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Declares asset dependencies so the audio singleton rebakes when presets change.
    /// </summary>
    /// <param name="authoring">Authoring component that contains the preset references.</param>
    private void DeclarePresetDependencies(GameAudioManagerAuthoring authoring)
    {
        if (authoring.MasterPreset != null)
        {
            DependsOn(authoring.MasterPreset);

            if (authoring.MasterPreset.AudioManagerPreset != null)
                DependsOn(authoring.MasterPreset.AudioManagerPreset);

            if (authoring.MasterPreset.SettingsManagerPreset != null)
                DependsOn(authoring.MasterPreset.SettingsManagerPreset);

            if (authoring.MasterPreset.HudManagerPreset != null)
            {
                DependsOn(authoring.MasterPreset.HudManagerPreset);
                DeclareHudPresentationDependencies(authoring.MasterPreset.HudManagerPreset);
            }
        }

        if (authoring.AudioManagerPreset != null)
            DependsOn(authoring.AudioManagerPreset);

        if (authoring.SettingsManagerPreset != null)
            DependsOn(authoring.SettingsManagerPreset);

        if (authoring.HudManagerPreset != null)
        {
            DependsOn(authoring.HudManagerPreset);
            DeclareHudPresentationDependencies(authoring.HudManagerPreset);
        }
    }

    /// <summary>
    /// Declares inline summary presentation assets referenced by one HUD Manager preset.
    /// </summary>
    /// <param name="hudPreset">HUD Manager preset whose nested object references participate in baking.</param>
    private void DeclareHudPresentationDependencies(GameHudManagerPreset hudPreset)
    {
        if (hudPreset == null)
            return;

        GameHudPowerUpSummarySettings summarySettings = hudPreset.PowerUpSummarySettings;

        if (summarySettings != null)
        {
            if (summarySettings.BackgroundSprite != null)
                DependsOn(summarySettings.BackgroundSprite);

            if (summarySettings.ToggleSprite != null)
                DependsOn(summarySettings.ToggleSprite);

            if (summarySettings.IconBackgroundSprite != null)
                DependsOn(summarySettings.IconBackgroundSprite);

            if (summarySettings.CounterFont != null)
                DependsOn(summarySettings.CounterFont);

            if (summarySettings.TitleFont != null)
                DependsOn(summarySettings.TitleFont);

            IReadOnlyList<GameHudStatisticDisplayDefinition> statistics = summarySettings.Statistics;

            for (int statisticIndex = 0; statisticIndex < statistics.Count; statisticIndex++)
            {
                GameHudStatisticDisplayDefinition statistic = statistics[statisticIndex];

                if (statistic != null && statistic.Font != null)
                    DependsOn(statistic.Font);
            }
        }
    }

    /// <summary>
    /// Builds the runtime singleton config from playback settings.
    /// </summary>
    /// <param name="preset">Source audio preset.</param>
    /// <returns>Baked runtime config component.</returns>
    private static GameAudioRuntimeConfig BuildRuntimeConfig(GameAudioManagerPreset preset)
    {
        GameAudioPlaybackSettings playbackSettings = preset.PlaybackSettings;
        GameAudioRoutingSettings routingSettings = preset.RoutingSettings;
        GameAudioBackgroundMusicSettings backgroundMusicSettings = preset.BackgroundMusicSettings;

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
        string backgroundMusicPath = backgroundMusicSettings != null ? backgroundMusicSettings.EventPath : string.Empty;
        string backgroundMusicBankName = backgroundMusicSettings != null ? backgroundMusicSettings.BankName : string.Empty;
        float backgroundMusicVolume = backgroundMusicSettings != null ? math.max(0f, backgroundMusicSettings.Volume) * musicRoutingVolume : 0f;

        return new GameAudioRuntimeConfig
        {
            Enabled = playbackSettings.Enabled ? (byte)1 : (byte)0,
            LogMissingEventPaths = playbackSettings.LogMissingEventPaths ? (byte)1 : (byte)0,
            BackgroundMusicEnabled = backgroundMusicSettings != null && backgroundMusicSettings.Enabled ? (byte)1 : (byte)0,
            BackgroundMusicAutoStart = backgroundMusicSettings != null && backgroundMusicSettings.AutoStart ? (byte)1 : (byte)0,
            BackgroundMusicRestartWhenPathChanges = backgroundMusicSettings != null && backgroundMusicSettings.RestartWhenPathChanges ? (byte)1 : (byte)0,
            BackgroundMusicStopWhenDisabled = backgroundMusicSettings == null || backgroundMusicSettings.StopWhenDisabled ? (byte)1 : (byte)0,
            BackgroundMusicEventPath = new Unity.Collections.FixedString512Bytes(backgroundMusicPath ?? string.Empty),
            BackgroundMusicBankName = new Unity.Collections.FixedString64Bytes(backgroundMusicBankName ?? string.Empty),
            MasterVolume = math.max(0f, playbackSettings.MasterVolume),
            BackgroundMusicVolume = backgroundMusicVolume,
            DefaultMinimumDistance = math.max(0f, playbackSettings.DefaultMinimumDistance),
            DefaultMaximumDistance = math.max(playbackSettings.DefaultMinimumDistance, playbackSettings.DefaultMaximumDistance)
        };
    }

    /// <summary>
    /// Builds the runtime Settings singleton from Settings Manager defaults.
    /// </summary>
    /// <param name="preset">Source Settings Manager preset, or null to bake project defaults.</param>
    /// <returns>Baked Settings runtime config component.</returns>
    private static GameSettingsRuntimeConfig BuildSettingsRuntimeConfig(GameSettingsManagerPreset preset)
    {
        GameSettingsManagerAudioSettings audioSettings = preset != null ? preset.AudioSettings : null;
        GameSettingsManagerExperienceSettings experienceSettings = preset != null ? preset.ExperienceSettings : null;
        GameSettingsManagerControllerNavigationSettings navigationSettings = preset != null ? preset.ControllerNavigationSettings : null;
        GameSettingsManagerPreviewEventSettings masterPreview = audioSettings != null && audioSettings.MasterVolumePreview != null
            ? audioSettings.MasterVolumePreview
            : GameSettingsManagerPreviewEventSettings.CreateSfxDefault();
        GameSettingsManagerPreviewEventSettings sfxPreview = audioSettings != null && audioSettings.SfxVolumePreview != null
            ? audioSettings.SfxVolumePreview
            : GameSettingsManagerPreviewEventSettings.CreateSfxDefault();
        GameSettingsManagerPreviewEventSettings musicPreview = audioSettings != null && audioSettings.MusicVolumePreview != null
            ? audioSettings.MusicVolumePreview
            : GameSettingsManagerPreviewEventSettings.CreateMusicDefault();

        return new GameSettingsRuntimeConfig
        {
            MasterBusPath = new Unity.Collections.FixedString128Bytes(ResolveString(audioSettings != null ? audioSettings.MasterBusPath : null, DefaultMasterBusPath)),
            SfxBusPath = new Unity.Collections.FixedString128Bytes(ResolveString(audioSettings != null ? audioSettings.SfxBusPath : null, DefaultSfxBusPath)),
            MusicBusPath = new Unity.Collections.FixedString128Bytes(ResolveString(audioSettings != null ? audioSettings.MusicBusPath : null, DefaultMusicBusPath)),
            MasterPreviewEventPath = new Unity.Collections.FixedString512Bytes(ResolvePreviewEventPath(masterPreview)),
            MasterPreviewBankName = new Unity.Collections.FixedString64Bytes(ResolvePreviewBankName(masterPreview)),
            SfxPreviewEventPath = new Unity.Collections.FixedString512Bytes(ResolvePreviewEventPath(sfxPreview)),
            SfxPreviewBankName = new Unity.Collections.FixedString64Bytes(ResolvePreviewBankName(sfxPreview)),
            MusicPreviewEventPath = new Unity.Collections.FixedString512Bytes(ResolvePreviewEventPath(musicPreview)),
            MusicPreviewBankName = new Unity.Collections.FixedString64Bytes(ResolvePreviewBankName(musicPreview)),
            MasterPreviewPlaysAllOthers = audioSettings != null && audioSettings.MasterPlaysAllPreviews ? (byte)1 : (byte)0,
            DefaultMasterVolume = math.clamp(audioSettings != null ? audioSettings.DefaultMasterVolume : 1f, 0f, 1f),
            DefaultSfxVolume = math.clamp(audioSettings != null ? audioSettings.DefaultSfxVolume : 1f, 0f, 1f),
            DefaultMusicVolume = math.clamp(audioSettings != null ? audioSettings.DefaultMusicVolume : 1f, 0f, 1f),
            DefaultVisualPointerEnabled = experienceSettings == null || experienceSettings.DefaultVisualPointerEnabled ? (byte)1 : (byte)0,
            DefaultFullscreenEnabled = experienceSettings == null || experienceSettings.DefaultFullscreenEnabled ? (byte)1 : (byte)0,
            DefaultFrameRateLimit = ResolveFrameRateLimit(experienceSettings != null ? (int)experienceSettings.DefaultFrameRateLimit : DefaultFrameRateLimit),
            DefaultDamageRumbleMultiplier = math.clamp(experienceSettings != null ? experienceSettings.DefaultDamageRumbleMultiplier : 1f, 0f, 2f),
            DefaultFireRumbleMultiplier = math.clamp(experienceSettings != null ? experienceSettings.DefaultFireRumbleMultiplier : 1f, 0f, 2f),
            WindowedWidth = experienceSettings != null ? math.max(1, experienceSettings.WindowedWidth) : DefaultWindowedWidth,
            WindowedHeight = experienceSettings != null ? math.max(1, experienceSettings.WindowedHeight) : DefaultWindowedHeight,
            GamepadNavigationMode = navigationSettings != null ? navigationSettings.GamepadNavigationMode : RuntimeMenuGamepadNavigationMode.Hybrid,
            NavigateActionName = new Unity.Collections.FixedString64Bytes(ResolveString(navigationSettings != null ? navigationSettings.NavigateActionName : null, GameSettingsManagerControllerNavigationSettings.DefaultNavigateActionName)),
            SubmitActionName = new Unity.Collections.FixedString64Bytes(ResolveString(navigationSettings != null ? navigationSettings.SubmitActionName : null, GameSettingsManagerControllerNavigationSettings.DefaultSubmitActionName)),
            CancelActionName = new Unity.Collections.FixedString64Bytes(ResolveString(navigationSettings != null ? navigationSettings.CancelActionName : null, GameSettingsManagerControllerNavigationSettings.DefaultCancelActionName)),
            NavigateDeadzone = math.clamp(navigationSettings != null ? navigationSettings.NavigateDeadzone : GameSettingsManagerControllerNavigationSettings.DefaultNavigateDeadzone, 0f, 1f),
            NavigationRepeatDelaySeconds = math.max(0f, navigationSettings != null ? navigationSettings.RepeatDelaySeconds : GameSettingsManagerControllerNavigationSettings.DefaultRepeatDelaySeconds),
            NavigationRepeatIntervalSeconds = math.max(0.01f, navigationSettings != null ? navigationSettings.RepeatIntervalSeconds : GameSettingsManagerControllerNavigationSettings.DefaultRepeatIntervalSeconds)
        };
    }

    /// <summary>
    /// Resolves a supported target frame-rate value for baked runtime settings.
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
    /// Resolves one authored string with a fallback used only for runtime bake output.
    /// </summary>
    /// <param name="value">Authored string value.</param>
    /// <param name="fallback">Fallback used when value is empty.</param>
    /// <returns>Resolved string value.</returns>
    private static string ResolveString(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    /// <summary>
    /// Resolves one Settings menu preview event path for ECS bake output.
    /// </summary>
    /// <param name="preview">Preview event settings from the Settings Manager preset.</param>
    /// <returns>Preview FMOD event path or an empty string.</returns>
    private static string ResolvePreviewEventPath(GameSettingsManagerPreviewEventSettings preview)
    {
        if (preview == null || string.IsNullOrWhiteSpace(preview.EventPath))
            return string.Empty;

        return preview.EventPath.Trim();
    }

    /// <summary>
    /// Resolves one Settings menu preview bank name for ECS bake output.
    /// </summary>
    /// <param name="preview">Preview event settings from the Settings Manager preset.</param>
    /// <returns>Preview FMOD bank name or an empty string.</returns>
    private static string ResolvePreviewBankName(GameSettingsManagerPreviewEventSettings preview)
    {
        if (preview == null || string.IsNullOrWhiteSpace(preview.BankName))
            return string.Empty;

        return preview.BankName.Trim();
    }

    /// <summary>
    /// Populates the baked binding buffer while skipping null or None bindings.
    /// </summary>
    /// <param name="preset">Source audio preset.</param>
    /// <param name="bindingBuffer">Output buffer on the audio singleton.</param>
    private static void PopulateBindingBuffer(GameAudioManagerPreset preset, DynamicBuffer<GameAudioEventBindingElement> bindingBuffer)
    {
        bindingBuffer.Clear();
        System.Collections.Generic.IReadOnlyList<GameAudioEventBinding> eventBindings = preset.EventBindings;

        if (eventBindings == null)
            return;

        for (int index = 0; index < eventBindings.Count; index++)
        {
            GameAudioEventBinding binding = eventBindings[index];

            if (binding == null)
                continue;

            if (binding.EventId == GameAudioEventId.None)
                continue;

            bindingBuffer.Add(BuildBindingElement(binding));
        }
    }

    /// <summary>
    /// Converts one ScriptableObject binding into an ECS buffer element.
    /// </summary>
    /// <param name="binding">Source event binding.</param>
    /// <returns>Baked buffer element.</returns>
    private static GameAudioEventBindingElement BuildBindingElement(GameAudioEventBinding binding)
    {
        GameAudioRateLimitSettings rateLimit = binding.RateLimit;

        return new GameAudioEventBindingElement
        {
            EventId = binding.EventId,
            EventCode = new Unity.Collections.FixedString64Bytes(binding.EventCode ?? string.Empty),
            EventPath = new Unity.Collections.FixedString512Bytes(binding.EventPath ?? string.Empty),
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

    #endregion
}
