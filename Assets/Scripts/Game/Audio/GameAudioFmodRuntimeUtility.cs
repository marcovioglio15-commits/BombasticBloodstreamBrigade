using Unity.Mathematics;
using UnityEngine;

#if NASHCORE_FMOD
using FMOD;
using FMOD.Studio;
using FMODUnity;
#endif

/// <summary>
/// Dispatches runtime audio events through FMOD when the NASHCORE_FMOD scripting define is enabled.
/// </summary>
public static class GameAudioFmodRuntimeUtility
{
    #region Constants
#if NASHCORE_FMOD
    private const float BackgroundMusicListenerResolveRetryIntervalSeconds = 0.5f;
#endif
    #endregion

    #region Fields
#if NASHCORE_FMOD
    private static EventInstance backgroundMusicInstance;
    private static bool backgroundMusicInstanceValid;
    private static bool backgroundMusicBankLoaded;
    private static string loadedBackgroundMusicBankName;
    private static string lastBackgroundMusicDiagnosticKey;
    private static Transform cachedBackgroundMusicListenerTransform;
    private static float nextBackgroundMusicListenerResolveTime;
#endif
    private static string backgroundMusicEventPath;
    private static string backgroundMusicBankName;
    private static string lastDisabledBackendMusicLogPath;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Plays one authored FMOD event path as a one-shot sound.
    /// </summary>
    /// <param name="eventPath">FMOD event path resolved from the Audio Manager preset.</param>
    /// <param name="position">World-space playback position.</param>
    /// <param name="hasPosition">True when the event should receive 3D attributes.</param>
    /// <param name="volume">Playback volume after binding and global multipliers.</param>
    /// <param name="pitch">Playback pitch after binding and request multipliers.</param>
    /// <param name="logMissingEventPath">True when empty paths should be reported in development contexts.</param>
    public static void PlayOneShot(string eventPath,
                                   float3 position,
                                   bool hasPosition,
                                   float volume,
                                   float pitch,
                                   bool logMissingEventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingPath(logMissingEventPath);
            return;
        }

#if NASHCORE_FMOD
        EventInstance instance = RuntimeManager.CreateInstance(eventPath);
        instance.setVolume(Mathf.Max(0f, volume));
        instance.setPitch(Mathf.Max(0.0001f, pitch));

        if (hasPosition)
        {
            Vector3 unityPosition = new Vector3(position.x, position.y, position.z);
            ATTRIBUTES_3D attributes = RuntimeUtils.To3DAttributes(unityPosition);
            instance.set3DAttributes(attributes);
        }

        instance.start();
        instance.release();
#else
        LogFmodDisabled(eventPath, logMissingEventPath);
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

#if NASHCORE_FMOD
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
#if NASHCORE_FMOD
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
#if NASHCORE_FMOD
        StopBackgroundMusic(FMOD.Studio.STOP_MODE.IMMEDIATE);
#else
        ClearBackgroundMusicState();
#endif
    }
    #endregion

    #region Private Methods
#if NASHCORE_FMOD
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
        cachedBackgroundMusicListenerTransform = null;
        nextBackgroundMusicListenerResolveTime = 0f;
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
        UnityEngine.Debug.Log("[GameAudio] FMOD backend is disabled. Define NASHCORE_FMOD after installing FMOD Unity integration to play: " + eventPath);
#endif
    }

#if NASHCORE_FMOD
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

        Transform listenerTransform = ResolveBackgroundMusicListenerTransform(Time.unscaledTime);
        Vector3 listenerPosition = listenerTransform != null
            ? listenerTransform.position
            : Vector3.zero;
        ATTRIBUTES_3D attributes = RuntimeUtils.To3DAttributes(listenerPosition);
        RESULT result = backgroundMusicInstance.set3DAttributes(attributes);

        if (result != RESULT.OK)
            LogMusicFmodResultWarning("sync listener anchor", eventPath, result, shouldLog);
    }

    /// <summary>
    /// Resolves and caches the transform currently acting as FMOD listener for music anchoring.
    /// </summary>
    /// <param name="elapsedTime">Current unscaled Unity time used to rate-limit scene scans.</param>
    /// <returns>Active listener or camera transform, or null when none is available yet.</returns>
    private static Transform ResolveBackgroundMusicListenerTransform(float elapsedTime)
    {
        if (cachedBackgroundMusicListenerTransform != null)
            return cachedBackgroundMusicListenerTransform;

        if (elapsedTime < nextBackgroundMusicListenerResolveTime)
            return null;

        nextBackgroundMusicListenerResolveTime = elapsedTime + BackgroundMusicListenerResolveRetryIntervalSeconds;
        StudioListener studioListener = Object.FindFirstObjectByType<StudioListener>(FindObjectsInactive.Exclude);

        if (studioListener != null)
        {
            cachedBackgroundMusicListenerTransform = studioListener.transform;
            return cachedBackgroundMusicListenerTransform;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            cachedBackgroundMusicListenerTransform = mainCamera.transform;
            return cachedBackgroundMusicListenerTransform;
        }

        Camera[] allCameras = Camera.allCameras;

        for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
        {
            Camera candidateCamera = allCameras[cameraIndex];

            if (candidateCamera == null)
                continue;

            if (!candidateCamera.isActiveAndEnabled)
                continue;

            cachedBackgroundMusicListenerTransform = candidateCamera.transform;
            return cachedBackgroundMusicListenerTransform;
        }

        return null;
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
        UnityEngine.Debug.Log("[GameAudio] FMOD backend is disabled. Define NASHCORE_FMOD after installing FMOD Unity integration to play background music: " + eventPath);
#endif
    }
    #endregion

    #endregion
}
