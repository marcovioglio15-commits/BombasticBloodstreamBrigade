using Unity.Mathematics;
using UnityEngine;

#if NASHCORE_FMOD || UNITY_EDITOR
using FMOD;
using FMOD.Studio;
using FMODUnity;
#endif

/// <summary>
/// Dispatches runtime audio events through FMOD in player builds when the NASHCORE_FMOD scripting define is enabled,
/// and in the Unity Editor when the FMOD integration is present.
/// </summary>
public static class GameAudioFmodRuntimeUtility
{
    #region Fields
#if NASHCORE_FMOD || UNITY_EDITOR
    private static EventInstance backgroundMusicInstance;
    private static bool backgroundMusicInstanceValid;
    private static bool backgroundMusicBankLoaded;
    private static string loadedBackgroundMusicBankName;
    private static string lastBackgroundMusicDiagnosticKey;
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

        EventInstance instance = RuntimeManager.CreateInstance(eventPath);
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
        else
        {
            instance.release();
        }
#else
        LogFmodDisabled(eventPath, logMissingEventPath);
#endif
    }

    /// <summary>
    /// Loads and resolves the configured background-music resources without starting playback. Scene-transition
    /// readiness calls this while the target is still covered so FMOD bank I/O cannot stall the first visible frame.
    /// </summary>
    /// <param name="eventPath">FMOD music event path that will be started during the target reveal.</param>
    /// <param name="bankName">FMOD bank containing the configured music event.</param>
    /// <param name="logMissingEventPath">True when preparation failures should emit development diagnostics.</param>
    /// <returns>True when the bank and event description are ready, or when FMOD is not compiled into this build.</returns>
    public static bool PrepareBackgroundMusic(string eventPath,
                                              string bankName,
                                              bool logMissingEventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingMusicPath(logMissingEventPath);
            return true;
        }

#if NASHCORE_FMOD || UNITY_EDITOR
        if (!EnsureBackgroundMusicBankLoaded(bankName, logMissingEventPath))
            return false;

        return TryResolveBackgroundMusicEvent(eventPath,
                                              logMissingEventPath,
                                              out EventDescription _);
#else
        return true;
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
    #endregion

    #region Private Methods
#if NASHCORE_FMOD || UNITY_EDITOR
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
            string.Equals(loadedBackgroundMusicBankName, bankName, System.StringComparison.Ordinal))
            return true;

        try
        {
            RuntimeManager.LoadBank(bankName);
        }
        catch (System.Exception exception)
        {
            LogMusicExceptionWarning("load bank", bankName, exception, shouldLog);
            return false;
        }

        backgroundMusicBankLoaded = true;
        loadedBackgroundMusicBankName = bankName;
        return true;
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
        RESULT result = RuntimeManager.StudioSystem.getEvent(eventPath, out eventDescription);

        if (result == RESULT.OK)
            return true;

        LogMusicFmodResultWarning("resolve event", eventPath, result, shouldLog);
        return false;
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

    #endregion
}
