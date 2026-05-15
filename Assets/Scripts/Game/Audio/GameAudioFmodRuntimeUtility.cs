using Unity.Mathematics;
using UnityEngine;

#if NASHCORE_FMOD
using FMOD;
using FMOD.Studio;
using FMODUnity;
#endif

/// <summary>
/// Dispatches runtime audio events through FMOD when the NASHCORE_FMOD scripting define is enabled.
/// /params None.
/// /returns None.
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
    /// /params eventPath FMOD event path resolved from the Audio Manager preset.
    /// /params position World-space playback position.
    /// /params hasPosition True when the event should receive 3D attributes.
    /// /params volume Playback volume after binding and global multipliers.
    /// /params pitch Playback pitch after binding and request multipliers.
    /// /params logMissingEventPath True when empty paths should be reported in development contexts.
    /// /returns None.
    /// </summary>
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
    /// /params eventPath FMOD music event path.
    /// /params bankName FMOD bank that contains the music event, or empty when already loaded elsewhere.
    /// /params enabled True when music playback is enabled.
    /// /params autoStart True when music should start automatically.
    /// /params volume Music volume after preset and routing multipliers.
    /// /params restartWhenPathChanges True when changing event path should restart the current music.
    /// /params stopWhenDisabled True when disabling music should stop the current instance.
    /// /params logMissingEventPath True when missing or disabled backend states should be logged.
    /// /returns None.
    /// </summary>
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
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void StopBackgroundMusic()
    {
#if NASHCORE_FMOD
        if (!backgroundMusicInstanceValid)
        {
            backgroundMusicEventPath = string.Empty;
            backgroundMusicBankName = string.Empty;
            return;
        }

        backgroundMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        backgroundMusicInstance.release();
        backgroundMusicInstance = default;
        backgroundMusicInstanceValid = false;
        cachedBackgroundMusicListenerTransform = null;
        nextBackgroundMusicListenerResolveTime = 0f;
#endif
        backgroundMusicEventPath = string.Empty;
        backgroundMusicBankName = string.Empty;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Logs an empty path warning only in contexts where runtime diagnostics are useful.
    /// /params shouldLog True when the current preset allows missing-path logs.
    /// /returns None.
    /// </summary>
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
    /// /params eventPath Event path that would have been played.
    /// /params shouldLog True when the current preset allows diagnostic logs.
    /// /returns None.
    /// </summary>
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
    /// /params eventPath FMOD event path.
    /// /params bankName FMOD bank that contains the music event.
    /// /params volume Music volume.
    /// /params logMissingEventPath True when diagnostics are enabled.
    /// /returns None.
    /// </summary>
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
    /// /params bankName Bank name authored in the Audio Manager preset.
    /// /params shouldLog True when diagnostic logs are enabled.
    /// /returns True when playback can continue.
    /// </summary>
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
    /// /params eventPath FMOD event path used for diagnostics.
    /// /params shouldLog True when diagnostic logs are enabled.
    /// /returns None.
    /// </summary>
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
    /// /params elapsedTime Current unscaled Unity time used to rate-limit scene scans.
    /// /returns Active listener or camera transform, or null when none is available yet.
    /// </summary>
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
    /// /params eventPath FMOD event path to resolve.
    /// /params shouldLog True when diagnostic logs are enabled.
    /// /params eventDescription Output event description when resolution succeeds.
    /// /returns True when FMOD resolves the event path.
    /// </summary>
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
    /// /params operation Operation being attempted.
    /// /params target FMOD path or bank name involved in the operation.
    /// /params result FMOD result code returned by the API.
    /// /params shouldLog True when diagnostics are enabled.
    /// /returns None.
    /// </summary>
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
    /// /params operation Operation being attempted.
    /// /params target FMOD path or bank name involved in the operation.
    /// /params exception Exception thrown by the FMOD Unity wrapper.
    /// /params shouldLog True when diagnostics are enabled.
    /// /returns None.
    /// </summary>
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
    /// /params eventPath FMOD event path that was started.
    /// /params bankName FMOD bank loaded before the event was resolved.
    /// /params shouldLog True when diagnostics are enabled.
    /// /returns None.
    /// </summary>
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
    /// /params shouldLog True when diagnostics are enabled.
    /// /returns None.
    /// </summary>
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
    /// /params eventPath Music event path.
    /// /params shouldLog True when diagnostics are enabled.
    /// /returns None.
    /// </summary>
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
