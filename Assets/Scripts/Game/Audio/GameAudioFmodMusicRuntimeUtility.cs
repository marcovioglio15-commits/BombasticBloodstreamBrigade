using System;
using System.Collections.Generic;
using UnityEngine;

#if NASHCORE_FMOD || UNITY_EDITOR
using FMOD;
using FMOD.Studio;
using FMODUnity;
#endif

/// <summary>
/// Owns music voices and crossfades them using unscaled time, independently of gameplay one-shots.
/// </summary>
public static class GameAudioFmodMusicRuntimeUtility
{
    #region Fields
#if NASHCORE_FMOD || UNITY_EDITOR
    private static readonly List<MusicVoice> voices = new List<MusicVoice>(3);
    private static readonly HashSet<string> ownedBanks = new HashSet<string>(StringComparer.Ordinal);
    private static MusicVoice selectedVoice;
    private static GameAudioMusicContext selectedContext;
    private static string failedTarget;
    private static float retryTime;
    private static string diagnosticKey;
#endif
    #endregion

    #region Types
#if NASHCORE_FMOD || UNITY_EDITOR
    /// <summary>
    /// Retains one playing event and its current fade envelope until it becomes silent.
    /// </summary>
    private sealed class MusicVoice
    {
        public readonly EventInstance Instance;
        public readonly string EventPath;
        public readonly string BankName;
        public readonly bool Is3D;
        public float Volume;
        public float AppliedVolume = float.NaN;
        public GameAudioMusicFadeState Fade;

        #region Methods
        /// <summary>
        /// Takes ownership of a successfully started event; the initial mix is silent.
        /// </summary>
        /// <param name="instance">Newly started FMOD instance.</param>
        /// <param name="eventPath">Verified event path.</param>
        /// <param name="bankName">Bank used for event preparation.</param>
        /// <param name="is3D">Whether listener attributes need ongoing updates.</param>
        public MusicVoice(EventInstance instance, string eventPath, string bankName, bool is3D)
        {
            Instance = instance;
            EventPath = eventPath;
            BankName = bankName;
            Is3D = is3D;
        }
        #endregion
    }
#endif
    #endregion

    #region Methods

    #region Public API
    /// <summary>
    /// Prepares a bank and event before a scene reveal without starting audible playback.
    /// </summary>
    /// <param name="eventPath">Music event that the target scene may select.</param>
    /// <param name="bankName">Owning bank, or empty when loaded elsewhere.</param>
    /// <param name="logWarnings">Whether preparation failures should be reported.</param>
    /// <returns>True when resources are available or the optional path is empty.</returns>
    public static bool Prepare(string eventPath, string bankName, bool logWarnings)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            return true;

#if NASHCORE_FMOD || UNITY_EDITOR
        return TryResolveEvent(eventPath, bankName, logWarnings, out EventDescription _);
#else
        return true;
#endif
    }

    /// <summary>
    /// Selects a music context and advances its mix without restarting unchanged events each frame.
    /// </summary>
    /// <param name="context">Context selected from authoritative ECS scene and boss state.</param>
    /// <param name="eventPath">Event for the selected context.</param>
    /// <param name="bankName">Bank containing the selected event.</param>
    /// <param name="volume">Final preset, master and routing volume.</param>
    /// <param name="restartWhenPathChanges">Whether a config edit may replace music within the same context.</param>
    /// <param name="fadeSeconds">Duration of a complete crossfade.</param>
    /// <param name="logWarnings">Whether missing events should emit diagnostics.</param>
    public static void Sync(GameAudioMusicContext context, string eventPath, string bankName, float volume,
                             bool restartWhenPathChanges, float fadeSeconds, bool logWarnings)
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        // A scene or encounter change always takes effect, even if config-edit restarts are disabled.
        bool contextChanged = selectedContext != context;
        bool pathChanged = selectedVoice == null || !Matches(selectedVoice, eventPath, bankName);

        if (context == GameAudioMusicContext.None)
            SelectVoice(null, context, fadeSeconds);
        else if (selectedVoice == null || contextChanged || (pathChanged && restartWhenPathChanges))
        {
            MusicVoice candidate = FindVoice(eventPath, bankName);

            if (candidate == null)
                candidate = TryStartVoice(eventPath, bankName, logWarnings);

            // Keep the current music audible if a replacement cannot be resolved.
            if (candidate != null)
                SelectVoice(candidate, context, fadeSeconds);
        }

        if (selectedVoice != null && selectedContext == context)
            selectedVoice.Volume = Mathf.Max(0f, volume);

        AdvanceVoices(Time.unscaledDeltaTime);
#endif
    }

    /// <summary>
    /// Advances existing fades when authored settings intentionally retain a disabled music context.
    /// </summary>
    public static void Tick()
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        AdvanceVoices(Time.unscaledDeltaTime);
#endif
    }

    /// <summary>
    /// Stops all music immediately during world teardown or global audio disable.
    /// </summary>
    /// <param name="releaseBanks">Whether to release banks acquired by this music owner.</param>
    public static void StopAll(bool releaseBanks)
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        // Each voice owns exactly one release, independent of the one-shot stores.
        for (int index = 0; index < voices.Count; index++)
            ReleaseVoice(voices[index]);

        voices.Clear();
        selectedVoice = null;
        selectedContext = GameAudioMusicContext.None;
        failedTarget = null;
        retryTime = 0f;
        diagnosticKey = null;

        if (!releaseBanks)
            return;

        // World teardown may run after FMOD; never recreate its manager just to release a bank.
        if (RuntimeManager.IsInitialized)
        {
            foreach (string bankName in ownedBanks)
                RuntimeManager.UnloadBank(bankName);
        }

        ownedBanks.Clear();
#endif
    }

    /// <summary>
    /// Includes outgoing music during a crossfade when preventing duplicate settings-menu previews.
    /// </summary>
    /// <param name="eventPath">Event path requested by the preview.</param>
    /// <returns>True when a live music voice owns the path.</returns>
    public static bool IsEventActive(string eventPath)
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        // This scan runs on preview requests, not once per gameplay sound.
        for (int index = 0; index < voices.Count; index++)
        {
            MusicVoice voice = voices[index];

            if (voice.EventPath == eventPath && voice.Instance.isValid() &&
                voice.Instance.getPlaybackState(out PLAYBACK_STATE playbackState) == RESULT.OK &&
                playbackState != PLAYBACK_STATE.STOPPED)
                return true;
        }
#endif
        return false;
    }
    #endregion

#if NASHCORE_FMOD || UNITY_EDITOR
    #region Crossfade
    /// <summary>
    /// Redirects active envelopes from their current weights so interrupted transitions remain continuous.
    /// </summary>
    /// <param name="voice">New selected voice, or null to fade to silence.</param>
    /// <param name="context">New music context.</param>
    /// <param name="fadeSeconds">Requested crossfade duration.</param>
    private static void SelectVoice(MusicVoice voice, GameAudioMusicContext context, float fadeSeconds)
    {
        if (ReferenceEquals(selectedVoice, voice) && selectedContext == context)
            return;

        selectedVoice = voice;
        selectedContext = context;

        // Preserve every in-flight voice during rapid boss/menu transitions.
        for (int index = 0; index < voices.Count; index++)
        {
            MusicVoice candidate = voices[index];
            candidate.Fade.Retarget(ReferenceEquals(candidate, voice) ? 1f : 0f, fadeSeconds);
        }
    }

    /// <summary>
    /// Applies smooth fade envelopes, updates only 3D anchors and releases silent outgoing voices.
    /// </summary>
    /// <param name="deltaTime">Unscaled presentation frame duration.</param>
    private static void AdvanceVoices(float deltaTime)
    {
        ATTRIBUTES_3D attributes = default;
        bool attributesResolved = false;

        // Voice count follows music transitions, never the number of enemies or projectiles.
        for (int index = voices.Count - 1; index >= 0; index--)
        {
            MusicVoice voice = voices[index];

            if (!voice.Instance.isValid())
            {
                if (ReferenceEquals(selectedVoice, voice))
                    selectedVoice = null;

                voices.RemoveAt(index);
                continue;
            }

            voice.Fade.Advance(deltaTime);

            if (voice.Fade.TargetWeight <= 0f && voice.Fade.Weight <= 0f)
            {
                ReleaseVoice(voice);
                voices.RemoveAt(index);
                continue;
            }

            // Stable 2D music performs no per-frame FMOD volume or position writes.
            float volume = voice.Volume * voice.Fade.Weight;

            if (!Mathf.Approximately(voice.AppliedVolume, volume))
            {
                voice.Instance.setVolume(volume);
                voice.AppliedVolume = volume;
            }

            if (!voice.Is3D)
                continue;

            if (!attributesResolved)
            {
                attributes = GameAudioFmodAttributesRuntimeUtility.ResolveListenerCenteredAttributes(Time.unscaledTime);
                attributesResolved = true;
            }

            voice.Instance.set3DAttributes(attributes);
        }
    }
    #endregion

    #region FMOD Ownership
    /// <summary>
    /// Reuses an outgoing instance if its event becomes selected again during a crossfade.
    /// </summary>
    /// <param name="eventPath">Requested event path.</param>
    /// <param name="bankName">Requested bank name.</param>
    /// <returns>Matching live voice, or null.</returns>
    private static MusicVoice FindVoice(string eventPath, string bankName)
    {
        for (int index = 0; index < voices.Count; index++)
        {
            if (Matches(voices[index], eventPath, bankName) && voices[index].Instance.isValid())
                return voices[index];
        }

        return null;
    }

    /// <summary>
    /// Compares the complete resource identity without allocating combined keys each frame.
    /// </summary>
    /// <param name="voice">Voice being inspected.</param>
    /// <param name="eventPath">Requested path.</param>
    /// <param name="bankName">Requested bank.</param>
    /// <returns>True when both resource identifiers match.</returns>
    private static bool Matches(MusicVoice voice, string eventPath, string bankName)
    {
        return string.Equals(voice.EventPath, eventPath, StringComparison.Ordinal) &&
               string.Equals(voice.BankName, bankName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Starts a replacement silently and rate-limits retries for unavailable banks or paths.
    /// </summary>
    /// <param name="eventPath">Requested music path.</param>
    /// <param name="bankName">Bank prepared before resolving the event.</param>
    /// <param name="logWarnings">Whether failed operations should be reported.</param>
    /// <returns>Newly owned voice, or null on failure.</returns>
    private static MusicVoice TryStartVoice(string eventPath, string bankName, bool logWarnings)
    {
        if (string.IsNullOrWhiteSpace(eventPath) || (failedTarget == eventPath && Time.unscaledTime < retryTime))
            return null;

        failedTarget = eventPath;
        retryTime = Time.unscaledTime + 1f;

        if (!TryResolveEvent(eventPath, bankName, logWarnings, out EventDescription description))
            return null;

        RESULT result = description.createInstance(out EventInstance instance);

        if (result != RESULT.OK)
        {
            Warn(eventPath, result.ToString(), logWarnings);
            return null;
        }

        // Attributes are applied before start to support authored 3D music safely.
        description.is3D(out bool is3D);

        if (is3D)
            instance.set3DAttributes(GameAudioFmodAttributesRuntimeUtility.ResolveListenerCenteredAttributes(Time.unscaledTime));

        instance.setVolume(0f);
        result = instance.start();

        if (result != RESULT.OK)
        {
            instance.release();
            Warn(eventPath, result.ToString(), logWarnings);
            return null;
        }

        failedTarget = null;
        MusicVoice voice = new MusicVoice(instance, eventPath, bankName, is3D);
        voices.Add(voice);
        return voice;
    }

    /// <summary>
    /// Loads each bank at most once per owner and resolves the event without repeated path exceptions.
    /// </summary>
    /// <param name="eventPath">Event path to resolve.</param>
    /// <param name="bankName">Optional bank dependency.</param>
    /// <param name="logWarnings">Whether failures should be reported.</param>
    /// <param name="description">Resolved event description.</param>
    /// <returns>True when an instance can be created.</returns>
    private static bool TryResolveEvent(string eventPath, string bankName, bool logWarnings, out EventDescription description)
    {
        description = default;

        try
        {
            if (!string.IsNullOrWhiteSpace(bankName) && !RuntimeManager.HasBankLoaded(bankName))
            {
                RuntimeManager.LoadBank(bankName);
                ownedBanks.Add(bankName);
            }

            RESULT result = RuntimeManager.StudioSystem.getEvent(eventPath, out description);

            if (result == RESULT.OK)
                return true;

            Warn(eventPath, result.ToString(), logWarnings);
        }
        catch (Exception exception)
        {
            Warn(eventPath, exception.Message, logWarnings);
        }

        return false;
    }

    /// <summary>
    /// Releases an owned voice after its fade or during immediate shutdown.
    /// </summary>
    /// <param name="voice">Voice being retired.</param>
    private static void ReleaseVoice(MusicVoice voice)
    {
        if (!voice.Instance.isValid())
            return;

        voice.Instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        voice.Instance.release();
    }

    /// <summary>
    /// Reports a failed resource once until its failure identity changes.
    /// </summary>
    /// <param name="target">Event involved in the failure.</param>
    /// <param name="message">FMOD result or exception text.</param>
    /// <param name="enabled">Whether diagnostics are enabled.</param>
    private static void Warn(string target, string message, bool enabled)
    {
        if (!enabled)
            return;

        string key = target + "|" + message;

        if (diagnosticKey == key)
            return;

        diagnosticKey = key;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] Music event '" + target + "' is unavailable: " + message);
#endif
    }
    #endregion
#endif

    #endregion
}
