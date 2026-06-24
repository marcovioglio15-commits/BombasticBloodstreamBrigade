using Unity.Mathematics;
using UnityEngine;

#if NASHCORE_FMOD || UNITY_EDITOR
using FMOD;
using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
#endif

/// <summary>
/// Dispatches runtime audio events through FMOD in player builds when the NASHCORE_FMOD scripting define is enabled,
/// and in the Unity Editor when the FMOD integration is present.
/// </summary>
public static class GameAudioFmodRuntimeUtility
{
    #region Constants
    private const string MasterBusPath = "bus:/";
    private const float WebGlReloadFadeOutSeconds = 0.12f;
    private const float WebGlReloadFadeInSeconds = 0.35f;
    private const float WebGlReloadVolumeEpsilon = 0.0005f;
    private const float WebGlGuardedOneShotFallbackSeconds = 10f;
    private const float WebGlGuardedOneShotTailSeconds = 0.75f;
    #endregion

    #region Fields
#if NASHCORE_FMOD || UNITY_EDITOR
    private static EventInstance backgroundMusicInstance;
    private static bool backgroundMusicInstanceValid;
    private static bool backgroundMusicBankLoaded;
    private static bool backgroundMusicBankLoadRequested;
    private static string loadedBackgroundMusicBankName;
    private static string lastBackgroundMusicDiagnosticKey;
    private static readonly Dictionary<string, EventDescription> cachedEventDescriptionsByPath = new Dictionary<string, EventDescription>(32);
    private static readonly HashSet<string> preloadedEventPaths = new HashSet<string>();
    private static string lastEventDiagnosticKey;
    private static Bus webGlReloadMasterBus;
    private static bool webGlReloadMasterBusValid;
    private static bool webGlReloadFadeEngaged;
    private static float webGlReloadRestoreVolume = 1f;
    private static float webGlReloadAppliedVolume = 1f;
#if UNITY_WEBGL && !UNITY_EDITOR
    private static readonly List<WebGlGuardedOneShot> webGlGuardedOneShots = new List<WebGlGuardedOneShot>(16);
#endif
#endif
    private static string backgroundMusicEventPath;
    private static string backgroundMusicBankName;
    private static string lastDisabledBackendMusicLogPath;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Plays one authored FMOD event path as a one-shot sound. When hasPosition is true and the authored event
    /// is 3D, the minimum and maximum attenuation distances are overridden so the Audio Manager preset stays
    /// authoritative about how close-vs-far playback feels at runtime. When singleInstance is true any previous
    /// still-playing instance for the same gameplay event is stopped first so the new request replaces it.
    /// </summary>
    /// <param name="eventId">Gameplay event id used to key the optional single-instance store.</param>
    /// <param name="eventPath">FMOD event path resolved from the Audio Manager preset.</param>
    /// <param name="position">World-space playback position.</param>
    /// <param name="hasPosition">True when the event should receive 3D attributes.</param>
    /// <param name="volume">Playback volume after binding and global multipliers.</param>
    /// <param name="pitch">Playback pitch after binding and request multipliers.</param>
    /// <param name="minimumDistance">Resolved 3D minimum attenuation distance, in world units.</param>
    /// <param name="maximumDistance">Resolved 3D maximum attenuation distance, in world units.</param>
    /// <param name="singleInstance">True when the binding requires voice stealing across consecutive requests.</param>
    /// <param name="logMissingEventPath">True when empty paths should be reported in development contexts.</param>
    public static void PlayOneShot(GameAudioEventId eventId,
                                   string eventPath,
                                   float3 position,
                                   bool hasPosition,
                                   float volume,
                                   float pitch,
                                   float minimumDistance,
                                   float maximumDistance,
                                   bool singleInstance,
                                   bool logMissingEventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingPath(logMissingEventPath);
            return;
        }

#if NASHCORE_FMOD || UNITY_EDITOR
        // Single-instance bindings stop and release the previous still-playing instance before creating the
        // new one so the perceived audio stays as one continuous voice instead of stacking copies of the clip.
        if (singleInstance)
            GameAudioFmodSingleInstanceRuntimeUtility.StopTrackedSingleInstance(eventId);

        if (!TryCreateEventInstance(eventPath, logMissingEventPath, out EventInstance instance))
            return;

        ATTRIBUTES_3D attributes = GameAudioFmodAttributesRuntimeUtility.ResolveOneShotAttributes(position, hasPosition);
        instance.set3DAttributes(attributes);
        instance.setVolume(Mathf.Max(0f, volume));
        instance.setPitch(Mathf.Max(0.0001f, pitch));

        if (hasPosition)
        {
            // Override the authored 3D attenuation bounds so the Audio Manager preset drives near/far balance
            // consistently across every spatialized event, instead of inheriting tight FMOD-authored curves.
            ApplyAttenuationDistances(ref instance, minimumDistance, maximumDistance);
        }

        instance.start();

        if (singleInstance)
        {
            // Keep the handle alive for the next steal request; release happens when the next single-instance
            // request lands or when the instance finishes naturally and FMOD invalidates the handle.
            GameAudioFmodSingleInstanceRuntimeUtility.StoreTrackedSingleInstance(eventId, instance, eventPath);
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        else if (ShouldGuardWebGlOneShot(eventId))
        {
            TrackWebGlGuardedOneShot(instance, eventPath, logMissingEventPath);
        }
#endif
        else
        {
            instance.release();
        }
#else
        LogFmodDisabled(eventPath, logMissingEventPath);
#endif
    }

    /// <summary>
    /// Resolves and preloads one FMOD event path ahead of the first audible request. On WebGL this moves the
    /// event lookup and sample-data request out of gameplay spike frames such as scene entry or restart.
    /// </summary>
    /// <param name="eventPath">FMOD event path resolved from the Audio Manager preset.</param>
    /// <param name="logMissingEventPath">True when missing paths should be reported in development contexts.</param>
    public static void PrepareEventPath(string eventPath, bool logMissingEventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingPath(logMissingEventPath);
            return;
        }

#if NASHCORE_FMOD || UNITY_EDITOR
        TryResolveEventDescription(eventPath, logMissingEventPath, true, out EventDescription eventDescription);
#else
        LogFmodDisabled(eventPath, logMissingEventPath);
#endif
    }

    /// <summary>
    /// Fades the FMOD master bus out while a WebGL gameplay restart is active, then restores its previous volume
    /// after the scene transition completes. The cached restore value preserves the user's master-volume setting.
    /// </summary>
    /// <param name="restartTransitionActive">True while the active scene is being restarted.</param>
    /// <param name="deltaSeconds">Unscaled frame delta used by the fade.</param>
    /// <param name="logWarnings">True when FMOD bus lookup failures should be logged.</param>
    public static void UpdateWebGlReloadTransitionFade(bool restartTransitionActive,
                                                       float deltaSeconds,
                                                       bool logWarnings)
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        if (!restartTransitionActive && !webGlReloadFadeEngaged)
            return;

        if (!TryResolveWebGlReloadMasterBus(logWarnings))
            return;

        if (restartTransitionActive && !webGlReloadFadeEngaged)
        {
            RESULT getVolumeResult = webGlReloadMasterBus.getVolume(out float currentVolume, out float finalVolume);

            if (getVolumeResult != RESULT.OK)
            {
                LogEventFmodResultWarning("read WebGL reload master bus volume",
                                          MasterBusPath,
                                          getVolumeResult,
                                          logWarnings);
                return;
            }

            webGlReloadRestoreVolume = Mathf.Clamp01(currentVolume);
            webGlReloadAppliedVolume = webGlReloadRestoreVolume;
            webGlReloadFadeEngaged = true;
        }

        float targetVolume = restartTransitionActive ? 0f : webGlReloadRestoreVolume;
        float fadeSeconds = restartTransitionActive ? WebGlReloadFadeOutSeconds : WebGlReloadFadeInSeconds;
        float volumeRange = Mathf.Max(WebGlReloadVolumeEpsilon, webGlReloadRestoreVolume);
        float maximumDelta = fadeSeconds > WebGlReloadVolumeEpsilon
            ? volumeRange * Mathf.Max(0f, deltaSeconds) / fadeSeconds
            : volumeRange;
        webGlReloadAppliedVolume = Mathf.MoveTowards(webGlReloadAppliedVolume, targetVolume, maximumDelta);
        RESULT setVolumeResult = webGlReloadMasterBus.setVolume(webGlReloadAppliedVolume);

        if (setVolumeResult != RESULT.OK)
        {
            LogEventFmodResultWarning("set WebGL reload master bus volume",
                                      MasterBusPath,
                                      setVolumeResult,
                                      logWarnings);
            webGlReloadMasterBusValid = false;
            return;
        }

        if (!restartTransitionActive &&
            Mathf.Abs(webGlReloadAppliedVolume - webGlReloadRestoreVolume) <= WebGlReloadVolumeEpsilon)
        {
            webGlReloadAppliedVolume = webGlReloadRestoreVolume;
            webGlReloadFadeEngaged = false;
        }
#endif
    }

    /// <summary>
    /// Starts, updates or stops the managed background music event instance.
    /// </summary>
    /// <param name="eventPath">FMOD music event path.</param>
    /// <param name="bankName">FMOD bank that contains the music event, or empty when already loaded elsewhere.</param>
    /// <param name="enabled">True when music playback is enabled.</param>
    /// <param name="autoStart">True when music should start automatically.</param>
    /// <param name="volume">Music volume after preset and routing multipliers.</param>
    /// <param name="restartWhenPathChanges">True when changing event path should restart the current music.</param>
    /// <param name="stopWhenDisabled">True when disabling music should stop the current instance.</param>
    /// <param name="logMissingEventPath">True when missing or disabled backend states should be logged.</param>
    public static void SyncBackgroundMusic(string eventPath,
                                           string bankName,
                                           bool enabled,
                                           bool autoStart,
                                           float volume,
                                           bool restartWhenPathChanges,
                                           bool stopWhenDisabled,
                                           bool logMissingEventPath)
    {
        if (!enabled || !autoStart)
        {
            if (stopWhenDisabled)
                StopBackgroundMusic();

            return;
        }

        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingMusicPath(logMissingEventPath);
            return;
        }

#if NASHCORE_FMOD || UNITY_EDITOR
        bool pathChanged = !string.Equals(backgroundMusicEventPath, eventPath, System.StringComparison.Ordinal);
        bool bankChanged = !string.Equals(backgroundMusicBankName, bankName, System.StringComparison.Ordinal);

        if (backgroundMusicInstanceValid && (pathChanged || bankChanged) && restartWhenPathChanges)
            StopBackgroundMusic();

        if (!backgroundMusicInstanceValid)
            StartBackgroundMusic(eventPath, bankName, volume, logMissingEventPath);
        else
        {
            backgroundMusicInstance.setVolume(Mathf.Max(0f, volume));
            SyncBackgroundMusicListenerAnchor(eventPath, logMissingEventPath);
        }
#else
        LogFmodDisabledMusic(eventPath, logMissingEventPath);
#endif
    }

    /// <summary>
    /// Stops the current background music instance if one is active.
    /// </summary>
    public static void StopBackgroundMusic()
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        StopBackgroundMusic(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
#else
        ClearBackgroundMusicState();
#endif
    }

    /// <summary>
    /// Immediately stops the current background music instance when entering non-gameplay scenes.
    /// </summary>
    public static void StopBackgroundMusicImmediate()
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        StopBackgroundMusic(FMOD.Studio.STOP_MODE.IMMEDIATE);
#else
        ClearBackgroundMusicState();
#endif
    }

    /// <summary>
    /// Checks whether the managed background music instance is currently using the requested FMOD event path.
    /// </summary>
    /// <param name="eventPath">FMOD event path to compare against the active background music instance.</param>
    /// <returns>True when that event is already running as background music.</returns>
    public static bool IsBackgroundMusicEventActive(string eventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            return false;

        if (!string.Equals(backgroundMusicEventPath, eventPath, System.StringComparison.Ordinal))
            return false;

#if NASHCORE_FMOD || UNITY_EDITOR
        if (!backgroundMusicInstanceValid || !backgroundMusicInstance.isValid())
            return false;

        RESULT result = backgroundMusicInstance.getPlaybackState(out PLAYBACK_STATE playbackState);

        if (result != RESULT.OK)
            return false;

        return playbackState != PLAYBACK_STATE.STOPPED && playbackState != PLAYBACK_STATE.STOPPING;
#else
        return false;
#endif
    }

    /// <summary>
    /// Checks whether the requested event path is already owned by a managed game-audio instance.
    /// </summary>
    /// <param name="eventPath">FMOD event path to search in managed runtime instances.</param>
    /// <returns>True when background music or a tracked single-instance gameplay event is already using the path.</returns>
    public static bool IsManagedEventPathActive(string eventPath)
    {
        if (IsBackgroundMusicEventActive(eventPath))
            return true;

#if NASHCORE_FMOD || UNITY_EDITOR
        return GameAudioFmodSingleInstanceRuntimeUtility.IsTrackedSingleInstanceEventPathActive(eventPath);
#else
        return false;
#endif
    }

    /// <summary>
    /// Stops the tracked single-instance voice for one gameplay event id, if any is still playing. Safe to call
    /// even when the runtime never produced a tracked instance for the event id.
    /// </summary>
    /// <param name="eventId">Gameplay event id whose tracked voice should be stopped.</param>
    public static void StopTrackedSingleInstanceById(GameAudioEventId eventId)
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        GameAudioFmodSingleInstanceRuntimeUtility.StopTrackedSingleInstance(eventId);
#endif
    }

    /// <summary>
    /// Stops every still-playing single-instance voice tracked by gameplay events. Called when the audio
    /// playback system is destroyed so stale FMOD handles do not survive into the next play session.
    /// </summary>
    public static void StopAllTrackedSingleInstances()
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        GameAudioFmodSingleInstanceRuntimeUtility.StopAllTrackedSingleInstances();
#endif
    }

    /// <summary>
    /// Releases completed guarded WebGL one-shots and force-stops any browser voice that outlives its authored
    /// event duration. This specifically protects long explosion samples from stale WebAudio nodes.
    /// </summary>
    /// <param name="logWarnings">True when FMOD failures should be reported in development contexts.</param>
    public static void UpdateWebGlGuardedOneShots(bool logWarnings)
    {
#if NASHCORE_FMOD && UNITY_WEBGL && !UNITY_EDITOR
        float currentTime = Time.realtimeSinceStartup;

        for (int index = webGlGuardedOneShots.Count - 1; index >= 0; index--)
        {
            WebGlGuardedOneShot guardedOneShot = webGlGuardedOneShots[index];
            EventInstance instance = guardedOneShot.Instance;

            if (!instance.isValid())
            {
                webGlGuardedOneShots.RemoveAt(index);
                continue;
            }

            RESULT playbackResult = instance.getPlaybackState(out PLAYBACK_STATE playbackState);

            if (playbackResult == RESULT.OK && playbackState == PLAYBACK_STATE.STOPPED)
            {
                instance.release();
                webGlGuardedOneShots.RemoveAt(index);
                continue;
            }

            if (currentTime < guardedOneShot.ForceStopTime)
                continue;

            RESULT stopResult = instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

            if (stopResult != RESULT.OK && stopResult != RESULT.ERR_INVALID_HANDLE)
                LogEventFmodResultWarning("force-stop guarded WebGL one-shot",
                                          guardedOneShot.EventPath,
                                          stopResult,
                                          logWarnings);

            instance.release();
            webGlGuardedOneShots.RemoveAt(index);
        }
#endif
    }

    /// <summary>
    /// Stops and releases every guarded WebGL one-shot during world teardown.
    /// </summary>
    public static void StopAllWebGlGuardedOneShots()
    {
#if NASHCORE_FMOD && UNITY_WEBGL && !UNITY_EDITOR
        for (int index = webGlGuardedOneShots.Count - 1; index >= 0; index--)
        {
            EventInstance instance = webGlGuardedOneShots[index].Instance;

            if (!instance.isValid())
                continue;

            instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            instance.release();
        }

        webGlGuardedOneShots.Clear();
#endif
    }
    #endregion

    #region Private Methods
#if NASHCORE_FMOD || UNITY_EDITOR
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Resolves whether a one-shot needs managed lifetime enforcement on WebGL.
    /// </summary>
    /// <param name="eventId">Gameplay audio event id.</param>
    /// <returns>True for browser-sensitive long explosion events.</returns>
    private static bool ShouldGuardWebGlOneShot(GameAudioEventId eventId)
    {
        return eventId == GameAudioEventId.ExplosionBomb;
    }

    /// <summary>
    /// Keeps one WebGL event instance alive until it stops naturally or reaches its authored-duration watchdog.
    /// </summary>
    /// <param name="instance">Started FMOD event instance.</param>
    /// <param name="eventPath">FMOD event path used for diagnostics.</param>
    /// <param name="shouldLog">True when FMOD failures should be logged.</param>
    private static void TrackWebGlGuardedOneShot(EventInstance instance,
                                                 string eventPath,
                                                 bool shouldLog)
    {
        float watchdogSeconds = WebGlGuardedOneShotFallbackSeconds;
        RESULT descriptionResult = instance.getDescription(out EventDescription eventDescription);

        if (descriptionResult == RESULT.OK)
        {
            RESULT lengthResult = eventDescription.getLength(out int lengthMilliseconds);

            if (lengthResult == RESULT.OK && lengthMilliseconds > 0)
                watchdogSeconds = lengthMilliseconds * 0.001f + WebGlGuardedOneShotTailSeconds;
            else if (lengthResult != RESULT.OK)
                LogEventFmodResultWarning("read guarded WebGL one-shot length",
                                          eventPath,
                                          lengthResult,
                                          shouldLog);
        }
        else
        {
            LogEventFmodResultWarning("resolve guarded WebGL one-shot description",
                                      eventPath,
                                      descriptionResult,
                                      shouldLog);
        }

        webGlGuardedOneShots.Add(new WebGlGuardedOneShot
        {
            Instance = instance,
            EventPath = eventPath,
            ForceStopTime = Time.realtimeSinceStartup + Mathf.Max(WebGlGuardedOneShotTailSeconds, watchdogSeconds)
        });
    }
#endif

    /// <summary>
    /// Resolves and caches the FMOD master bus used by the WebGL reload transition fade.
    /// </summary>
    /// <param name="shouldLog">True when lookup failures should be logged.</param>
    /// <returns>True when a valid master bus is available.</returns>
    private static bool TryResolveWebGlReloadMasterBus(bool shouldLog)
    {
        if (webGlReloadMasterBusValid && webGlReloadMasterBus.isValid())
            return true;

        RESULT result = RuntimeManager.StudioSystem.getBus(MasterBusPath, out webGlReloadMasterBus);

        if (result != RESULT.OK)
        {
            LogEventFmodResultWarning("resolve WebGL reload master bus",
                                      MasterBusPath,
                                      result,
                                      shouldLog);
            webGlReloadMasterBus = default;
            webGlReloadMasterBusValid = false;
            return false;
        }

        webGlReloadMasterBusValid = true;
        return true;
    }

    /// <summary>
    /// Applies the resolved minimum and maximum attenuation distances to one FMOD event instance, keeping the
    /// authored curve shape but rescaling the near and far bounds to match the Audio Manager preset.
    /// </summary>
    /// <param name="instance">FMOD event instance being prepared for one-shot playback.</param>
    /// <param name="minimumDistance">Effective 3D minimum distance, in world units. Non-positive values fall back to the authored event value.</param>
    /// <param name="maximumDistance">Effective 3D maximum distance, in world units. Non-positive values fall back to the authored event value.</param>
    private static void ApplyAttenuationDistances(ref EventInstance instance,
                                                  float minimumDistance,
                                                  float maximumDistance)
    {
        float safeMinimumDistance = Mathf.Max(0f, minimumDistance);
        float safeMaximumDistance = Mathf.Max(safeMinimumDistance, maximumDistance);

        if (safeMinimumDistance > 0f)
            instance.setProperty(EVENT_PROPERTY.MINIMUM_DISTANCE, safeMinimumDistance);

        if (safeMaximumDistance > 0f)
            instance.setProperty(EVENT_PROPERTY.MAXIMUM_DISTANCE, safeMaximumDistance);
    }

    /// <summary>
    /// Stops the current background music instance with the requested FMOD stop mode.
    /// </summary>
    /// <param name="stopMode">FMOD stop behavior used for the active music instance.</param>
    private static void StopBackgroundMusic(FMOD.Studio.STOP_MODE stopMode)
    {
        if (!backgroundMusicInstanceValid)
        {
            ClearBackgroundMusicState();
            return;
        }

        backgroundMusicInstance.stop(stopMode);
        backgroundMusicInstance.release();
        backgroundMusicInstance = default;
        backgroundMusicInstanceValid = false;
        GameAudioFmodAttributesRuntimeUtility.ClearCachedListener();
        ClearBackgroundMusicState();
    }
#endif

    /// <summary>
    /// Clears managed background music identity state after a stop or disabled backend call.
    /// </summary>
    private static void ClearBackgroundMusicState()
    {
        backgroundMusicEventPath = string.Empty;
        backgroundMusicBankName = string.Empty;
    }

    /// <summary>
    /// Logs an empty path warning only in contexts where runtime diagnostics are useful.
    /// </summary>
    /// <param name="shouldLog">True when the current preset allows missing-path logs.</param>
    private static void LogMissingPath(bool shouldLog)
    {
        if (!shouldLog)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] Skipped audio request because the FMOD event path is empty.");
#endif
    }

    /// <summary>
    /// Logs the disabled-backend state when FMOD integration has not been compiled into the project.
    /// </summary>
    /// <param name="eventPath">Event path that would have been played.</param>
    /// <param name="shouldLog">True when the current preset allows diagnostic logs.</param>
    private static void LogFmodDisabled(string eventPath, bool shouldLog)
    {
        if (!shouldLog)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[GameAudio] FMOD backend is disabled. Define NASHCORE_FMOD for player builds after installing FMOD Unity integration to play: " + eventPath);
#endif
    }

#if NASHCORE_FMOD || UNITY_EDITOR
    /// <summary>
    /// Creates and starts the background music instance.
    /// </summary>
    /// <param name="eventPath">FMOD event path.</param>
    /// <param name="bankName">FMOD bank that contains the music event.</param>
    /// <param name="volume">Music volume.</param>
    /// <param name="logMissingEventPath">True when diagnostics are enabled.</param>
    private static void StartBackgroundMusic(string eventPath,
                                             string bankName,
                                             float volume,
                                             bool logMissingEventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingMusicPath(logMissingEventPath);
            return;
        }

        if (!EnsureBackgroundMusicBankLoaded(bankName, logMissingEventPath))
            return;

        if (!TryResolveBackgroundMusicEvent(eventPath, logMissingEventPath, out EventDescription eventDescription))
            return;

        RESULT createResult = eventDescription.createInstance(out backgroundMusicInstance);

        if (createResult != RESULT.OK)
        {
            LogMusicFmodResultWarning("create instance", eventPath, createResult, logMissingEventPath);
            backgroundMusicInstance = default;
            return;
        }

        RESULT volumeResult = backgroundMusicInstance.setVolume(Mathf.Max(0f, volume));

        if (volumeResult != RESULT.OK)
            LogMusicFmodResultWarning("set volume", eventPath, volumeResult, logMissingEventPath);

        SyncBackgroundMusicListenerAnchor(eventPath, logMissingEventPath);

        RESULT startResult = backgroundMusicInstance.start();

        if (startResult != RESULT.OK)
        {
            LogMusicFmodResultWarning("start", eventPath, startResult, logMissingEventPath);
            backgroundMusicInstance.release();
            backgroundMusicInstance = default;
            return;
        }

        backgroundMusicInstanceValid = true;
        backgroundMusicEventPath = eventPath;
        backgroundMusicBankName = bankName ?? string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMusicStarted(eventPath, bankName, logMissingEventPath);
#endif
    }

    /// <summary>
    /// Loads the configured music bank once before resolving the FMOD event path.
    /// </summary>
    /// <param name="bankName">Bank name authored in the Audio Manager preset.</param>
    /// <param name="shouldLog">True when diagnostic logs are enabled.</param>
    /// <returns>True when playback can continue.</returns>
    private static bool EnsureBackgroundMusicBankLoaded(string bankName, bool shouldLog)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            return true;

        if (backgroundMusicBankLoaded &&
            string.Equals(loadedBackgroundMusicBankName, bankName, System.StringComparison.Ordinal) &&
            RuntimeManager.HasBankLoaded(bankName))
            return true;

        if (backgroundMusicBankLoadRequested &&
            string.Equals(loadedBackgroundMusicBankName, bankName, System.StringComparison.Ordinal))
        {
            if (!RuntimeManager.HasBankLoaded(bankName))
                return false;

            backgroundMusicBankLoaded = true;
            return true;
        }

        try
        {
            RuntimeManager.LoadBank(bankName);
        }
        catch (System.Exception exception)
        {
            LogMusicExceptionWarning("load bank", bankName, exception, shouldLog);
            return false;
        }

        backgroundMusicBankLoadRequested = true;
        loadedBackgroundMusicBankName = bankName;
        backgroundMusicBankLoaded = RuntimeManager.HasBankLoaded(bankName);
        return backgroundMusicBankLoaded;
    }

    /// <summary>
    /// Keeps background music centered on the active FMOD listener so authored 3D music events behave like global music.
    /// </summary>
    /// <param name="eventPath">FMOD event path used for diagnostics.</param>
    /// <param name="shouldLog">True when diagnostic logs are enabled.</param>
    private static void SyncBackgroundMusicListenerAnchor(string eventPath, bool shouldLog)
    {
        if (!backgroundMusicInstance.isValid())
            return;

        ATTRIBUTES_3D attributes = GameAudioFmodAttributesRuntimeUtility.ResolveListenerCenteredAttributes(Time.unscaledTime);
        RESULT result = backgroundMusicInstance.set3DAttributes(attributes);

        if (result != RESULT.OK)
            LogMusicFmodResultWarning("sync listener anchor", eventPath, result, shouldLog);
    }

    /// <summary>
    /// Resolves the FMOD event description without throwing repeated path lookup exceptions.
    /// </summary>
    /// <param name="eventPath">FMOD event path to resolve.</param>
    /// <param name="shouldLog">True when diagnostic logs are enabled.</param>
    /// <param name="eventDescription">Output event description when resolution succeeds.</param>
    /// <returns>True when FMOD resolves the event path.</returns>
    private static bool TryResolveBackgroundMusicEvent(string eventPath,
                                                       bool shouldLog,
                                                       out EventDescription eventDescription)
    {
        return TryResolveEventDescription(eventPath, shouldLog, true, out eventDescription);
    }

    /// <summary>
    /// Creates one event instance from a cached event description, preloading sample data before the first start.
    /// </summary>
    /// <param name="eventPath">FMOD event path to instantiate.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    /// <param name="instance">Created FMOD event instance when successful.</param>
    /// <returns>True when the event instance was created.</returns>
    private static bool TryCreateEventInstance(string eventPath, bool shouldLog, out EventInstance instance)
    {
        if (!TryResolveEventDescription(eventPath, shouldLog, true, out EventDescription eventDescription))
        {
            instance = default;
            return false;
        }

        RESULT result = eventDescription.createInstance(out instance);

        if (result == RESULT.OK)
            return true;

        LogEventFmodResultWarning("create instance", eventPath, result, shouldLog);
        instance = default;
        return false;
    }

    /// <summary>
    /// Resolves and caches one FMOD event description by path, optionally requesting its sample data.
    /// </summary>
    /// <param name="eventPath">FMOD event path.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    /// <param name="preloadSampleData">True when sample data should be loaded after resolution.</param>
    /// <param name="eventDescription">Resolved event description.</param>
    /// <returns>True when FMOD resolved the event path.</returns>
    private static bool TryResolveEventDescription(string eventPath,
                                                   bool shouldLog,
                                                   bool preloadSampleData,
                                                   out EventDescription eventDescription)
    {
        if (cachedEventDescriptionsByPath.TryGetValue(eventPath, out eventDescription))
        {
            if (eventDescription.isValid())
            {
                if (preloadSampleData)
                    TryLoadSampleData(eventPath, ref eventDescription, shouldLog);

                return true;
            }

            cachedEventDescriptionsByPath.Remove(eventPath);
            preloadedEventPaths.Remove(eventPath);
        }

        RESULT result = RuntimeManager.StudioSystem.getEvent(eventPath, out eventDescription);

        if (result != RESULT.OK)
        {
            LogEventFmodResultWarning("resolve event", eventPath, result, shouldLog);
            return false;
        }

        cachedEventDescriptionsByPath[eventPath] = eventDescription;

        if (preloadSampleData)
            TryLoadSampleData(eventPath, ref eventDescription, shouldLog);

        return true;
    }

    /// <summary>
    /// Requests sample-data loading once for each event path so the first audible instance does less work.
    /// </summary>
    /// <param name="eventPath">FMOD event path.</param>
    /// <param name="eventDescription">Resolved event description.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void TryLoadSampleData(string eventPath, ref EventDescription eventDescription, bool shouldLog)
    {
        if (preloadedEventPaths.Contains(eventPath))
            return;

        RESULT result = eventDescription.loadSampleData();

        if (result == RESULT.OK || result == RESULT.ERR_EVENT_ALREADY_LOADED)
        {
            preloadedEventPaths.Add(eventPath);
            return;
        }

        LogEventFmodResultWarning("load sample data", eventPath, result, shouldLog);
    }

    /// <summary>
    /// Logs one FMOD event-preparation warning per failed operation and path.
    /// </summary>
    /// <param name="operation">Operation being attempted.</param>
    /// <param name="target">FMOD event path involved in the operation.</param>
    /// <param name="result">FMOD result code returned by the API.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void LogEventFmodResultWarning(string operation,
                                                  string target,
                                                  RESULT result,
                                                  bool shouldLog)
    {
        if (!shouldLog)
            return;

        string diagnosticKey = operation + "|" + target + "|" + result;

        if (string.Equals(lastEventDiagnosticKey, diagnosticKey, System.StringComparison.Ordinal))
            return;

        lastEventDiagnosticKey = diagnosticKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] FMOD event failed to " + operation + " for '" + target + "'. FMOD result: " + result + ".");
#endif
    }

    /// <summary>
    /// Logs one FMOD result warning per failed operation and path.
    /// </summary>
    /// <param name="operation">Operation being attempted.</param>
    /// <param name="target">FMOD path or bank name involved in the operation.</param>
    /// <param name="result">FMOD result code returned by the API.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void LogMusicFmodResultWarning(string operation,
                                                  string target,
                                                  RESULT result,
                                                  bool shouldLog)
    {
        if (!shouldLog)
            return;

        string diagnosticKey = operation + "|" + target + "|" + result;

        if (string.Equals(lastBackgroundMusicDiagnosticKey, diagnosticKey, System.StringComparison.Ordinal))
            return;

        lastBackgroundMusicDiagnosticKey = diagnosticKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] Background music failed to " + operation + " for '" + target + "'. FMOD result: " + result + ".");
#endif
    }

    /// <summary>
    /// Logs one FMOD exception warning per failed operation and target.
    /// </summary>
    /// <param name="operation">Operation being attempted.</param>
    /// <param name="target">FMOD path or bank name involved in the operation.</param>
    /// <param name="exception">Exception thrown by the FMOD Unity wrapper.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void LogMusicExceptionWarning(string operation,
                                                 string target,
                                                 System.Exception exception,
                                                 bool shouldLog)
    {
        if (!shouldLog)
            return;

        string diagnosticKey = operation + "|" + target + "|" + exception.GetType().Name;

        if (string.Equals(lastBackgroundMusicDiagnosticKey, diagnosticKey, System.StringComparison.Ordinal))
            return;

        lastBackgroundMusicDiagnosticKey = diagnosticKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] Background music failed to " + operation + " for '" + target + "'. " + exception.Message);
#endif
    }

    /// <summary>
    /// Logs a successful music start once per event path in editor and development builds.
    /// </summary>
    /// <param name="eventPath">FMOD event path that was started.</param>
    /// <param name="bankName">FMOD bank loaded before the event was resolved.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void LogMusicStarted(string eventPath, string bankName, bool shouldLog)
    {
        if (!shouldLog)
            return;

        string diagnosticKey = "started|" + eventPath + "|" + bankName;

        if (string.Equals(lastBackgroundMusicDiagnosticKey, diagnosticKey, System.StringComparison.Ordinal))
            return;

        lastBackgroundMusicDiagnosticKey = diagnosticKey;
        UnityEngine.Debug.Log("[GameAudio] Background music started: " + eventPath + " from bank '" + bankName + "'.");
    }
#endif

    /// <summary>
    /// Logs a missing background music path warning.
    /// </summary>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void LogMissingMusicPath(bool shouldLog)
    {
        if (!shouldLog)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] Background music is enabled but the FMOD event path is empty.");
#endif
    }

    /// <summary>
    /// Logs disabled-backend music diagnostics once per path.
    /// </summary>
    /// <param name="eventPath">Music event path.</param>
    /// <param name="shouldLog">True when diagnostics are enabled.</param>
    private static void LogFmodDisabledMusic(string eventPath, bool shouldLog)
    {
        if (!shouldLog)
            return;

        if (string.Equals(lastDisabledBackendMusicLogPath, eventPath, System.StringComparison.Ordinal))
            return;

        lastDisabledBackendMusicLogPath = eventPath;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[GameAudio] FMOD backend is disabled. Define NASHCORE_FMOD for player builds after installing FMOD Unity integration to play background music: " + eventPath);
#endif
    }
    #endregion

#if NASHCORE_FMOD && UNITY_WEBGL && !UNITY_EDITOR
    #region Nested Types
    private struct WebGlGuardedOneShot
    {
        public EventInstance Instance;
        public string EventPath;
        public float ForceStopTime;
    }
    #endregion
#endif

    #endregion
}
