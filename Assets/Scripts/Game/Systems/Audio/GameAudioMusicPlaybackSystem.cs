using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Selects menu, boss or gameplay music from ECS state and forwards the resulting mix to FMOD.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct GameAudioMusicPlaybackSystem : ISystem, ISystemStartStop
{
    #region Fields
    private GameAudioMusicContext previousContext;
    private static FixedString512Bytes cachedFixedPath;
    private static FixedString64Bytes cachedFixedBank;
    private static string cachedPath;
    private static string cachedBank;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Allows menu playback as soon as the persistent audio singleton exists, without requiring a player.
    /// </summary>
    /// <param name="state">Owning presentation system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameAudioRuntimeConfig>();
    }

    /// <summary>
    /// Clears selection history when the persistent audio singleton first becomes available.
    /// </summary>
    /// <param name="state">Owning presentation system state.</param>
    public void OnStartRunning(ref SystemState state)
    {
        previousContext = GameAudioMusicContext.None;
    }

    /// <summary>
    /// Silences music when the singleton disappears before world teardown.
    /// </summary>
    /// <param name="state">Owning presentation system state.</param>
    public void OnStopRunning(ref SystemState state)
    {
        GameAudioFmodMusicRuntimeUtility.StopAll(false);
    }

    /// <summary>
    /// Releases music and the bank references owned by its runtime bridge.
    /// </summary>
    /// <param name="state">Owning presentation system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        GameAudioFmodMusicRuntimeUtility.StopAll(true);
    }

    /// <summary>
    /// Reads only boss entities; projectile and ordinary enemy counts do not affect music selection cost.
    /// </summary>
    /// <param name="state">Owning presentation system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        GameAudioRuntimeConfig config = SystemAPI.GetSingleton<GameAudioRuntimeConfig>();

        if (config.Enabled == 0)
        {
            GameAudioFmodMusicRuntimeUtility.StopAll(false);
            previousContext = GameAudioMusicContext.None;
            return;
        }

        bool isMainMenu = SystemAPI.TryGetSingleton<GameSceneManagerConfig>(out GameSceneManagerConfig sceneConfig) &&
                          SystemAPI.TryGetSingleton<GameSceneTransitionState>(out GameSceneTransitionState transition) &&
                          GameAudioMusicSelectionUtility.IsMainMenu(in sceneConfig, in transition);
        bool hasBoss = false;

        // Pooled and inactive enemies are excluded by the EnemyActive enableable component.
        if (!isMainMenu &&
            (GameAudioMusicSelectionUtility.CanStart(in config.BossMusic) ||
             (previousContext == GameAudioMusicContext.Boss && config.BossMusic.StopWhenDisabled == 0)))
        {
            foreach ((RefRO<EnemyBossHudConfig> hud, RefRO<EnemyHealth> health)
                     in SystemAPI.Query<RefRO<EnemyBossHudConfig>, RefRO<EnemyHealth>>().WithAll<EnemyBossTag, EnemyActive>())
            {
                if (hud.ValueRO.Enabled == 0 || health.ValueRO.Current <= 0f)
                    continue;

                hasBoss = true;
                break;
            }
        }

        GameAudioMusicContext context = GameAudioMusicSelectionUtility.ResolveContext(isMainMenu, hasBoss);
        GameAudioMusicTrackConfig track = GameAudioMusicSelectionUtility.ResolveTrack(in config, context);

        if (!GameAudioMusicSelectionUtility.CanStart(in track))
        {
            // Retention applies only within the same context; leaving a scene always retires its music.
            if (previousContext == context && track.StopWhenDisabled == 0)
                GameAudioFmodMusicRuntimeUtility.Tick();
            else
                GameAudioFmodMusicRuntimeUtility.Sync(GameAudioMusicContext.None, string.Empty, string.Empty, 0f,
                                                       true, config.MusicCrossfadeSeconds, config.LogMissingEventPaths != 0);

            previousContext = context;
            return;
        }

        // Convert fixed strings only when selection or authored resources change.
        if (cachedPath == null || !cachedFixedPath.Equals(track.EventPath))
        {
            cachedFixedPath = track.EventPath;
            cachedPath = track.EventPath.ToString();
        }

        if (cachedBank == null || !cachedFixedBank.Equals(track.BankName))
        {
            cachedFixedBank = track.BankName;
            cachedBank = track.BankName.ToString();
        }

        GameAudioFmodMusicRuntimeUtility.Sync(context, cachedPath, cachedBank,
                                               math.max(0f, config.MasterVolume) * track.Volume,
                                               track.RestartWhenPathChanges != 0, config.MusicCrossfadeSeconds,
                                               config.LogMissingEventPaths != 0);
        previousContext = context;
    }
    #endregion

    #endregion
}
