using Unity.Entities;
using UnityEngine;

/// <summary>
/// Applies local user settings to Unity screen state, FMOD buses and optional ECS presentation data.
/// </summary>
public static class GameUserSettingsRuntimeUtility
{
    #region Fields
    private static GameUserSettingsData currentSettings;
    private static bool hasCurrentSettings;
    #endregion

    #region Properties
    public static GameUserSettingsData CurrentSettings
    {
        get
        {
            EnsureCurrentSettingsLoaded();
            return currentSettings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads saved settings and applies them to every runtime target that is currently available.
    /// </summary>
    /// <param name="audioBusPaths">FMOD bus paths used for audio volume application.</param>
    /// <returns>Loaded settings after runtime clamping.</returns>
    public static GameUserSettingsData LoadAndApply(in GameUserSettingsAudioBusPaths audioBusPaths)
    {
        GameUserSettingsWindowedDisplaySettings windowedDisplay = GameUserSettingsWindowedDisplaySettings.CreateDefault();
        GameUserSettingsRuntimeOptions runtimeOptions = new GameUserSettingsRuntimeOptions(in audioBusPaths, in windowedDisplay);
        return LoadAndApply(in runtimeOptions);
    }

    /// <summary>
    /// Loads saved settings and applies them using Settings Manager-derived runtime options.
    /// </summary>
    /// <param name="runtimeOptions">Runtime bus paths, display defaults and reset defaults.</param>
    /// <returns>Loaded settings after runtime clamping.</returns>
    public static GameUserSettingsData LoadAndApply(in GameUserSettingsRuntimeOptions runtimeOptions)
    {
        GameUserSettingsData settings = GameUserSettingsStore.Load(in runtimeOptions);
        Apply(settings, in runtimeOptions, true, true, true);
        return settings;
    }

    /// <summary>
    /// Applies one settings snapshot to the requested runtime targets without saving it.
    /// </summary>
    /// <param name="settings">Settings snapshot to apply.</param>
    /// <param name="audioBusPaths">FMOD bus paths used for audio volume application.</param>
    /// <param name="applyScreen">True to update Unity fullscreen state.</param>
    /// <param name="applyAudio">True to update FMOD bus volumes.</param>
    /// <param name="syncEcs">True to mirror pointer and rumble values into the default ECS world when available.</param>
    public static void Apply(GameUserSettingsData settings,
                             in GameUserSettingsAudioBusPaths audioBusPaths,
                             bool applyScreen,
                             bool applyAudio,
                             bool syncEcs)
    {
        GameUserSettingsWindowedDisplaySettings windowedDisplay = GameUserSettingsWindowedDisplaySettings.CreateDefault();
        GameUserSettingsRuntimeOptions runtimeOptions = new GameUserSettingsRuntimeOptions(in audioBusPaths, in windowedDisplay);
        Apply(settings, in runtimeOptions, applyScreen, applyAudio, syncEcs);
    }

    /// <summary>
    /// Applies one settings snapshot to runtime targets using Settings Manager-derived project options.
    /// </summary>
    /// <param name="settings">Settings snapshot to apply.</param>
    /// <param name="runtimeOptions">Runtime bus paths, display defaults and reset defaults.</param>
    /// <param name="applyScreen">True to update Unity fullscreen state.</param>
    /// <param name="applyAudio">True to update FMOD bus volumes.</param>
    /// <param name="syncEcs">True to mirror pointer and rumble values into the default ECS world when available.</param>
    public static void Apply(GameUserSettingsData settings,
                             in GameUserSettingsRuntimeOptions runtimeOptions,
                             bool applyScreen,
                             bool applyAudio,
                             bool syncEcs)
    {
        currentSettings = GameUserSettingsStore.ClampForRuntime(settings);
        hasCurrentSettings = true;

        if (applyScreen)
            ApplyScreenState(in currentSettings, in runtimeOptions.WindowedDisplay);

        if (applyAudio)
            ApplyAudioVolumes(in currentSettings, in runtimeOptions.AudioBusPaths);

        if (syncEcs)
            SyncEcsSettings(in currentSettings);
    }

    /// <summary>
    /// Saves and applies a confirmed settings snapshot.
    /// </summary>
    /// <param name="settings">Settings snapshot confirmed by the user.</param>
    /// <param name="audioBusPaths">FMOD bus paths used for audio volume application.</param>
    public static void SaveAndApply(GameUserSettingsData settings, in GameUserSettingsAudioBusPaths audioBusPaths)
    {
        GameUserSettingsWindowedDisplaySettings windowedDisplay = GameUserSettingsWindowedDisplaySettings.CreateDefault();
        GameUserSettingsRuntimeOptions runtimeOptions = new GameUserSettingsRuntimeOptions(in audioBusPaths, in windowedDisplay);
        SaveAndApply(settings, in runtimeOptions);
    }

    /// <summary>
    /// Saves and applies a confirmed settings snapshot using Settings Manager-derived runtime options.
    /// </summary>
    /// <param name="settings">Settings snapshot confirmed by the user.</param>
    /// <param name="runtimeOptions">Runtime bus paths, display defaults and reset defaults.</param>
    public static void SaveAndApply(GameUserSettingsData settings, in GameUserSettingsRuntimeOptions runtimeOptions)
    {
        GameUserSettingsData clampedSettings = GameUserSettingsStore.ClampForRuntime(settings);
        GameUserSettingsStore.Save(in clampedSettings);
        Apply(clampedSettings, in runtimeOptions, true, true, true);
    }

    /// <summary>
    /// Re-applies the cached ECS settings into a newly created default world.
    /// </summary>
    public static void RefreshEcsMirror()
    {
        EnsureCurrentSettingsLoaded();
        SyncEcsSettings(in currentSettings);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the static cache has a settings snapshot before a runtime system reads it.
    /// </summary>
    private static void EnsureCurrentSettingsLoaded()
    {
        if (hasCurrentSettings)
            return;

        if (GameSettingsMenuRuntimeConfigUtility.TryResolve(out GameUserSettingsRuntimeOptions runtimeOptions, out _))
            currentSettings = GameUserSettingsStore.Load(in runtimeOptions);
        else
            currentSettings = GameUserSettingsStore.Load();

        hasCurrentSettings = true;
    }

    /// <summary>
    /// Applies fullscreen or windowed resolution according to the current user setting.
    /// </summary>
    /// <param name="settings">Settings snapshot containing fullscreen state.</param>
    /// <param name="windowedDisplay">Project-level windowed size from Settings Manager runtime config.</param>
    private static void ApplyScreenState(in GameUserSettingsData settings,
                                         in GameUserSettingsWindowedDisplaySettings windowedDisplay)
    {
        ApplyFrameRateLock(settings.FrameRateLimit);

#if UNITY_WEBGL && !UNITY_EDITOR
        // Browser fullscreen must be requested from a direct user gesture in the HTML host.
        return;
#else
        if (settings.FullscreenEnabled != 0)
        {
            Screen.fullScreen = true;
            return;
        }

        int width = ResolvePositiveDimension(windowedDisplay.Width, 1280);
        int height = ResolvePositiveDimension(windowedDisplay.Height, 720);
        Screen.SetResolution(width, height, false);
#endif
    }

    /// <summary>
    /// Applies the supported target frame-rate lock and disables v-sync so Unity honors Application.targetFrameRate.
    /// </summary>
    /// <param name="frameRateLimit">Requested frame-rate lock.</param>
    private static void ApplyFrameRateLock(int frameRateLimit)
    {
        int resolvedFrameRateLimit = GameUserSettingsStore.ResolveSupportedFrameRateLimit(frameRateLimit);

        if (QualitySettings.vSyncCount != 0)
            QualitySettings.vSyncCount = 0;

        if (Application.targetFrameRate != resolvedFrameRateLimit)
            Application.targetFrameRate = resolvedFrameRateLimit;
    }

    /// <summary>
    /// Applies the user audio volumes to the configured FMOD buses.
    /// </summary>
    /// <param name="settings">Settings snapshot containing volume scalars.</param>
    /// <param name="audioBusPaths">FMOD bus paths used for routing.</param>
    private static void ApplyAudioVolumes(in GameUserSettingsData settings, in GameUserSettingsAudioBusPaths audioBusPaths)
    {
        GameAudioSettingsFmodRuntimeUtility.SetBusVolume(ResolveBusPath(audioBusPaths.MasterBusPath, "bus:/"),
                                                         settings.MasterVolume,
                                                         true);
        GameAudioSettingsFmodRuntimeUtility.SetBusVolume(ResolveBusPath(audioBusPaths.SfxBusPath, "bus:/SFX"),
                                                         settings.SfxVolume,
                                                         true);
        GameAudioSettingsFmodRuntimeUtility.SetBusVolume(ResolveBusPath(audioBusPaths.MusicBusPath, "bus:/Music"),
                                                         settings.MusicVolume,
                                                         true);
    }

    /// <summary>
    /// Mirrors settings into the default world if ECS is currently available.
    /// </summary>
    /// <param name="settings">Settings snapshot containing presentation preferences.</param>
    private static void SyncEcsSettings(in GameUserSettingsData settings)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerUserExperienceSettings>());
        PlayerUserExperienceSettings component = new PlayerUserExperienceSettings
        {
            VisualPointerEnabled = settings.VisualPointerEnabled,
            DamageRumbleMultiplier = settings.DamageRumbleMultiplier,
            FireRumbleMultiplier = settings.FireRumbleMultiplier
        };

        if (query.IsEmptyIgnoreFilter)
        {
            Entity entity = entityManager.CreateEntity(typeof(PlayerUserExperienceSettings));
            entityManager.SetComponentData(entity, component);
            query.Dispose();
            return;
        }

        entityManager.SetComponentData(query.GetSingletonEntity(), component);
        query.Dispose();
    }

    /// <summary>
    /// Resolves a usable FMOD bus path, falling back when a serialized value is empty.
    /// </summary>
    /// <param name="busPath">Serialized bus path.</param>
    /// <param name="fallback">Fallback bus path.</param>
    /// <returns>Trimmed bus path suitable for FMOD lookup.</returns>
    private static string ResolveBusPath(string busPath, string fallback)
    {
        if (string.IsNullOrWhiteSpace(busPath))
            return fallback;

        return busPath.Trim();
    }

    /// <summary>
    /// Resolves a positive screen dimension for runtime application.
    /// </summary>
    /// <param name="dimension">Authored dimension value.</param>
    /// <param name="fallback">Fallback dimension used when authored data is invalid.</param>
    /// <returns>Positive dimension in pixels.</returns>
    private static int ResolvePositiveDimension(int dimension, int fallback)
    {
        if (dimension > 0)
            return dimension;

        return fallback;
    }
    #endregion

    #endregion
}
