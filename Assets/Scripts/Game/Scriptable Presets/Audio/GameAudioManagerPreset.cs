using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scriptable preset that stores FMOD-backed gameplay audio settings for the Game Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "GameAudioManagerPreset", menuName = "Game/Audio Manager Preset", order = 20)]
public sealed class GameAudioManagerPreset : ScriptableObject
{
    #region Constants
    private const int DefaultEnemyProjectileMaxPlays = 8;
    private const float DefaultEnemyProjectileWindowSeconds = 0.25f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this audio manager preset, used for stable editor references.")]
    [SerializeField] private string presetId;

    [Tooltip("Audio manager preset name displayed in Game Management Tool.")]
    [SerializeField] private string presetName = "New Audio Manager Preset";

    [Tooltip("Short description of this audio setup and its intended gameplay use.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this audio preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Playback")]
    [Tooltip("Global runtime playback controls used by the ECS audio playback system.")]
    [SerializeField] private GameAudioPlaybackSettings playbackSettings = new GameAudioPlaybackSettings();

    [Header("FMOD Routing")]
    [Tooltip("FMOD bus paths and mix defaults reserved for game-wide audio routing.")]
    [SerializeField] private GameAudioRoutingSettings routingSettings = new GameAudioRoutingSettings();

    [Header("Background Music")]
    [Tooltip("Background music event and runtime control settings.")]
    [SerializeField] private GameAudioBackgroundMusicSettings backgroundMusicSettings = new GameAudioBackgroundMusicSettings();

    [Header("Boss Music")]
    [Tooltip("Independent music selected while at least one active boss has BOSS UI enabled.")]
    [SerializeField]
    private GameAudioBackgroundMusicSettings bossMusicSettings = new GameAudioBackgroundMusicSettings();

    [Header("Main Menu Music")]
    [Tooltip("Independent background music selected in the Main Menu scene.")]
    [SerializeField]
    private GameAudioBackgroundMusicSettings mainMenuMusicSettings = new GameAudioBackgroundMusicSettings();

    [Header("Music Transitions")]
    [Tooltip("Crossfade duration in real seconds between gameplay, boss and menu music. Must be greater than zero.")]
    [SerializeField]
    private float musicCrossfadeSeconds = 1.5f;

    [Header("Event Sound Map")]
    [Tooltip("Gameplay event to FMOD event-path bindings baked into ECS.")]
    [SerializeField] private List<GameAudioEventBinding> eventBindings = new List<GameAudioEventBinding>();
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

    public GameAudioPlaybackSettings PlaybackSettings
    {
        get
        {
            return playbackSettings;
        }
    }

    public GameAudioRoutingSettings RoutingSettings
    {
        get
        {
            return routingSettings;
        }
    }

    public GameAudioBackgroundMusicSettings BackgroundMusicSettings
    {
        get
        {
            return backgroundMusicSettings;
        }
    }

    public GameAudioBackgroundMusicSettings BossMusicSettings => bossMusicSettings;
    public GameAudioBackgroundMusicSettings MainMenuMusicSettings => mainMenuMusicSettings;
    public float MusicCrossfadeSeconds => musicCrossfadeSeconds;

    public IReadOnlyList<GameAudioEventBinding> EventBindings
    {
        get
        {
            return eventBindings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures required object references and a stable ID exist without clamping authored numeric values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (playbackSettings == null)
            playbackSettings = new GameAudioPlaybackSettings();

        if (routingSettings == null)
            routingSettings = new GameAudioRoutingSettings();

        if (backgroundMusicSettings == null)
            backgroundMusicSettings = new GameAudioBackgroundMusicSettings();

        if (bossMusicSettings == null)
            bossMusicSettings = new GameAudioBackgroundMusicSettings();

        if (mainMenuMusicSettings == null)
            mainMenuMusicSettings = new GameAudioBackgroundMusicSettings();

        if (eventBindings == null)
            eventBindings = new List<GameAudioEventBinding>();
    }

    /// <summary>
    /// Rebuilds the event map with all supported default event descriptors.
    /// Existing FMOD paths are discarded and must be reauthored intentionally.
    /// </summary>
    public void ResetEventBindingsToDefaults()
    {
        EnsureInitialized();
        eventBindings.Clear();
        GameAudioDefaultEventDefinition[] definitions = GameAudioDefaultEventDefinitions.Definitions;

        for (int index = 0; index < definitions.Length; index++)
        {
            GameAudioEventBinding binding = CreateDefaultBinding(definitions[index]);
            eventBindings.Add(binding);
        }
    }

    /// <summary>
    /// Adds missing default bindings while preserving existing authored FMOD paths and rate caps.
    /// </summary>
    /// <returns>Number of bindings added to the preset.</returns>
    public int EnsureDefaultEventBindings()
    {
        EnsureInitialized();
        int addedCount = 0;
        GameAudioDefaultEventDefinition[] definitions = GameAudioDefaultEventDefinitions.Definitions;

        for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
        {
            GameAudioDefaultEventDefinition definition = definitions[definitionIndex];

            if (ContainsBinding(definition.EventId))
                continue;

            eventBindings.Add(CreateDefaultBinding(definition));
            addedCount++;
        }

        return addedCount;
    }

    /// <summary>
    /// Updates identity text on existing default events without touching FMOD paths or authored mix values.
    /// </summary>
    /// <returns>Number of bindings whose identity was synchronized.</returns>
    public int SynchronizeDefaultEventIdentities()
    {
        EnsureInitialized();
        int synchronizedCount = 0;

        for (int bindingIndex = 0; bindingIndex < eventBindings.Count; bindingIndex++)
        {
            GameAudioEventBinding binding = eventBindings[bindingIndex];

            if (binding == null)
                continue;

            if (!GameAudioDefaultEventDefinitions.TryGetDefinition(binding.EventId, out GameAudioDefaultEventDefinition definition))
                continue;

            binding.ConfigureIdentity(definition);
            synchronizedCount++;
        }

        return synchronizedCount;
    }

    /// <summary>
    /// Populates verified project FMOD defaults only for bindings whose path is still empty.
    /// </summary>
    /// <returns>Number of bindings that received a default path.</returns>
    public int EnsureDefaultEventPaths()
    {
        EnsureInitialized();
        int configuredCount = 0;

        for (int bindingIndex = 0; bindingIndex < eventBindings.Count; bindingIndex++)
        {
            GameAudioEventBinding binding = eventBindings[bindingIndex];

            if (binding == null ||
                !GameAudioDefaultEventDefinitions.TryGetDefinition(
                    binding.EventId,
                    out GameAudioDefaultEventDefinition definition))
            {
                continue;
            }

            if (binding.ConfigureDefaultEventPath(definition))
                configuredCount++;
        }

        return configuredCount;
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Keeps required reference containers alive in the editor without changing authored tuning values.
    /// </summary>
    private void OnValidate()
    {
        EnsureInitialized();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one default event binding and applies special default caps for dense events.
    /// </summary>
    /// <param name="definition">Event descriptor used to initialize identity fields.</param>
    /// <returns>Created event binding.</returns>
    private static GameAudioEventBinding CreateDefaultBinding(GameAudioDefaultEventDefinition definition)
    {
        GameAudioEventBinding binding = new GameAudioEventBinding();
        binding.ConfigureIdentity(definition);
        binding.ConfigureDefaultEventPath(definition);

        if (definition.EventId == GameAudioEventId.EnemyShootProjectile)
            binding.ConfigureEnemyProjectileCap(DefaultEnemyProjectileMaxPlays, DefaultEnemyProjectileWindowSeconds);

        return binding;
    }

    /// <summary>
    /// Checks whether the preset already has a binding for one event ID.
    /// </summary>
    /// <param name="eventId">Event identifier to search.</param>
    /// <returns>True when a matching binding exists.</returns>
    private bool ContainsBinding(GameAudioEventId eventId)
    {
        for (int index = 0; index < eventBindings.Count; index++)
        {
            GameAudioEventBinding binding = eventBindings[index];

            if (binding == null)
                continue;

            if (binding.EventId != eventId)
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
