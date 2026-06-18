using Unity.Entities;

/// <summary>
/// Resolves runtime Settings menu options from the baked Settings Manager ECS singleton.
/// </summary>
public static class GameSettingsMenuRuntimeConfigUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Tries to resolve Settings menu runtime options from the default ECS world.
    /// </summary>
    /// <param name="runtimeOptions">Resolved user-settings runtime options, or defaults when unavailable.</param>
    /// <param name="previewSet">Resolved audio preview events, or empty defaults when unavailable.</param>
    /// <returns>True when a baked Settings Manager runtime config was found.</returns>
    public static bool TryResolve(out GameUserSettingsRuntimeOptions runtimeOptions,
                                  out GameAudioSettingsPreviewSet previewSet)
    {
        runtimeOptions = GameUserSettingsRuntimeOptions.CreateDefault();
        previewSet = default;
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameSettingsRuntimeConfig>());

        if (query.IsEmptyIgnoreFilter)
        {
            query.Dispose();
            return false;
        }

        if (query.CalculateEntityCount() != 1)
        {
            query.Dispose();
            return false;
        }

        GameSettingsRuntimeConfig config = entityManager.GetComponentData<GameSettingsRuntimeConfig>(query.GetSingletonEntity());
        query.Dispose();
        runtimeOptions = BuildRuntimeOptions(in config);
        previewSet = BuildPreviewSet(in config);
        return true;
    }
    #endregion

    #region Builders
    /// <summary>
    /// Builds runtime user-settings options from the baked Settings Manager config.
    /// </summary>
    /// <param name="config">Baked Settings Manager runtime config.</param>
    /// <returns>Runtime options for applying local user settings.</returns>
    private static GameUserSettingsRuntimeOptions BuildRuntimeOptions(in GameSettingsRuntimeConfig config)
    {
        GameUserSettingsAudioBusPaths busPaths = new GameUserSettingsAudioBusPaths(ResolveString(config.MasterBusPath.ToString(), "bus:/"),
                                                                                   ResolveString(config.SfxBusPath.ToString(), "bus:/SFX"),
                                                                                   ResolveString(config.MusicBusPath.ToString(), "bus:/Music"));
        GameUserSettingsWindowedDisplaySettings windowedDisplay = new GameUserSettingsWindowedDisplaySettings(ResolvePositiveDimension(config.WindowedWidth, 1280),
                                                                                                             ResolvePositiveDimension(config.WindowedHeight, 720));
        GameUserSettingsData defaultSettings = new GameUserSettingsData
        {
            MasterVolume = config.DefaultMasterVolume,
            SfxVolume = config.DefaultSfxVolume,
            MusicVolume = config.DefaultMusicVolume,
            VisualPointerEnabled = config.DefaultVisualPointerEnabled,
            FullscreenEnabled = config.DefaultFullscreenEnabled,
            FrameRateLimit = ResolveFrameRateLimit(config.DefaultFrameRateLimit),
            DamageRumbleMultiplier = config.DefaultDamageRumbleMultiplier,
            FireRumbleMultiplier = config.DefaultFireRumbleMultiplier
        };
        RuntimeMenuGamepadNavigationOptions menuNavigation = BuildNavigationOptions(in config);
        return new GameUserSettingsRuntimeOptions(in busPaths, in windowedDisplay, in defaultSettings, in menuNavigation);
    }

    /// <summary>
    /// Builds runtime controller-navigation options from the baked Settings Manager config.
    /// </summary>
    /// <param name="config">Baked Settings Manager runtime config.</param>
    /// <returns>Runtime menu navigation options.</returns>
    private static RuntimeMenuGamepadNavigationOptions BuildNavigationOptions(in GameSettingsRuntimeConfig config)
    {
        return new RuntimeMenuGamepadNavigationOptions(config.GamepadNavigationMode,
                                                       ResolveString(config.NavigateActionName.ToString(), GameSettingsManagerControllerNavigationSettings.DefaultNavigateActionName),
                                                       ResolveString(config.SubmitActionName.ToString(), GameSettingsManagerControllerNavigationSettings.DefaultSubmitActionName),
                                                       ResolveString(config.CancelActionName.ToString(), GameSettingsManagerControllerNavigationSettings.DefaultCancelActionName),
                                                       Unity.Mathematics.math.clamp(config.NavigateDeadzone, 0f, 1f),
                                                       Unity.Mathematics.math.max(0f, config.NavigationRepeatDelaySeconds),
                                                       Unity.Mathematics.math.max(0.01f, config.NavigationRepeatIntervalSeconds));
    }

    /// <summary>
    /// Builds runtime audio preview settings from the baked Settings Manager config.
    /// </summary>
    /// <param name="config">Baked Settings Manager runtime config.</param>
    /// <returns>Preview event set for the Settings menu audio sliders.</returns>
    private static GameAudioSettingsPreviewSet BuildPreviewSet(in GameSettingsRuntimeConfig config)
    {
        GameAudioSettingsPreviewEvent master = new GameAudioSettingsPreviewEvent(config.MasterPreviewEventPath.ToString(),
                                                                                 config.MasterPreviewBankName.ToString());
        GameAudioSettingsPreviewEvent sfx = new GameAudioSettingsPreviewEvent(config.SfxPreviewEventPath.ToString(),
                                                                              config.SfxPreviewBankName.ToString());
        GameAudioSettingsPreviewEvent music = new GameAudioSettingsPreviewEvent(config.MusicPreviewEventPath.ToString(),
                                                                                config.MusicPreviewBankName.ToString());
        return new GameAudioSettingsPreviewSet(master, sfx, music, config.MasterPreviewPlaysAllOthers != 0);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves a non-empty string value from baked config data.
    /// </summary>
    /// <param name="value">Baked string value.</param>
    /// <param name="fallback">Fallback used when the value is empty.</param>
    /// <returns>Resolved string value.</returns>
    private static string ResolveString(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    /// <summary>
    /// Resolves a positive screen dimension for runtime use.
    /// </summary>
    /// <param name="dimension">Baked dimension value.</param>
    /// <param name="fallback">Fallback dimension used when baked data is invalid.</param>
    /// <returns>Positive dimension in pixels.</returns>
    private static int ResolvePositiveDimension(int dimension, int fallback)
    {
        if (dimension > 0)
            return dimension;

        return fallback;
    }

    /// <summary>
    /// Resolves a supported target frame-rate lock for runtime defaults.
    /// </summary>
    /// <param name="frameRateLimit">Baked frame-rate limit.</param>
    /// <returns>Supported frame-rate limit in frames per second.</returns>
    private static int ResolveFrameRateLimit(int frameRateLimit)
    {
        switch (frameRateLimit)
        {
            case 60:
            case 120:
            case 180:
                return frameRateLimit;
            default:
                return 60;
        }
    }
    #endregion

    #endregion
}
