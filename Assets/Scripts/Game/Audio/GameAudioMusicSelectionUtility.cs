/// <summary>
/// Shares music context and track resolution between presentation, scene warmup and verification.
/// </summary>
public static class GameAudioMusicSelectionUtility
{
    #region Methods

    #region Selection
    /// <summary>
    /// Starts menu music when returning to the menu and retains it until a gameplay target is ready to fade in.
    /// </summary>
    /// <param name="config">Scene identities from the scene manager preset.</param>
    /// <param name="transition">Authoritative scene transition state.</param>
    /// <returns>True when menu music owns the current transition phase.</returns>
    public static bool IsMainMenu(in GameSceneManagerConfig config, in GameSceneTransitionState transition)
    {
        if (config.MainMenuSceneId.Length == 0)
            return false;

        if (transition.IsTransitioning != 0)
        {
            if (transition.TargetSceneId.Equals(config.MainMenuSceneId))
                return true;

            if (transition.Phase == GameSceneTransitionPhase.FadeIn)
                return false;
        }

        return transition.ActiveSceneId.Equals(config.MainMenuSceneId);
    }

    /// <summary>
    /// Gives menu scenes priority over remaining boss entities during cleanup.
    /// </summary>
    /// <param name="isMainMenu">Whether the current scene flow belongs to the main menu.</param>
    /// <param name="hasBoss">Whether a live enemy with enabled BOSS UI is present.</param>
    /// <returns>Music context selected for the current frame.</returns>
    public static GameAudioMusicContext ResolveContext(bool isMainMenu, bool hasBoss)
    {
        if (isMainMenu)
            return GameAudioMusicContext.MainMenu;

        return hasBoss ? GameAudioMusicContext.Boss : GameAudioMusicContext.Gameplay;
    }

    /// <summary>
    /// Resolves the independent track while preserving existing gameplay music settings.
    /// </summary>
    /// <param name="config">Baked persistent audio configuration.</param>
    /// <param name="context">Context whose event should be used.</param>
    /// <returns>Compiled event and mix settings for the context.</returns>
    public static GameAudioMusicTrackConfig ResolveTrack(in GameAudioRuntimeConfig config, GameAudioMusicContext context)
    {
        switch (context)
        {
            case GameAudioMusicContext.MainMenu:
                return config.MainMenuMusic;
            case GameAudioMusicContext.Boss:
                return config.BossMusic;
            case GameAudioMusicContext.Gameplay:
                return new GameAudioMusicTrackConfig
                {
                    Enabled = config.BackgroundMusicEnabled,
                    AutoStart = config.BackgroundMusicAutoStart,
                    RestartWhenPathChanges = config.BackgroundMusicRestartWhenPathChanges,
                    StopWhenDisabled = config.BackgroundMusicStopWhenDisabled,
                    EventPath = config.BackgroundMusicEventPath,
                    BankName = config.BackgroundMusicBankName,
                    Volume = config.BackgroundMusicVolume
                };
            default:
                return default;
        }
    }

    /// <summary>
    /// Treats unassigned optional music as unavailable so it cannot suppress an existing gameplay track.
    /// </summary>
    /// <param name="track">Music settings being inspected.</param>
    /// <returns>True when automatic playback is enabled and an event path is assigned.</returns>
    public static bool CanStart(in GameAudioMusicTrackConfig track)
    {
        return track.Enabled != 0 && track.AutoStart != 0 && track.EventPath.Length > 0;
    }
    #endregion

    #endregion
}
