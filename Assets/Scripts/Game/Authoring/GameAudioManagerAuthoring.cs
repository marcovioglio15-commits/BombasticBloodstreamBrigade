using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Scene authoring component that bakes the selected Game Audio Manager preset into an ECS audio singleton.
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
    #endregion

    #endregion
}

/// <summary>
/// Baker that converts GameAudioManagerAuthoring into singleton audio config and event binding buffers.
/// </summary>
public sealed class GameAudioManagerAuthoringBaker : Baker<GameAudioManagerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Bakes global audio config and all event mappings from the selected preset.
    /// </summary>
    /// <param name="authoring">Scene authoring component that chooses the preset.</param>
    public override void Bake(GameAudioManagerAuthoring authoring)
    {
        if (authoring == null)
            return;

        DeclarePresetDependencies(authoring);
        GameAudioManagerPreset preset = authoring.ResolveAudioManagerPreset();

        if (preset == null)
            return;

        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, BuildRuntimeConfig(preset));
        DynamicBuffer<GameAudioEventBindingElement> bindingBuffer = AddBuffer<GameAudioEventBindingElement>(entity);
        DynamicBuffer<GameAudioEventRequest> requestBuffer = AddBuffer<GameAudioEventRequest>(entity);
        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStateBuffer = AddBuffer<GameAudioRateLimitStateElement>(entity);
        PopulateBindingBuffer(preset, bindingBuffer);
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
        }

        if (authoring.AudioManagerPreset != null)
            DependsOn(authoring.AudioManagerPreset);
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
