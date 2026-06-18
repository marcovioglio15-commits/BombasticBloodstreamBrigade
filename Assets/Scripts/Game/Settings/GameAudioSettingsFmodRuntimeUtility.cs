using UnityEngine;

#if NASHCORE_FMOD
using FMOD;
using FMOD.Studio;
using FMODUnity;
#endif

/// <summary>
/// Applies Settings-menu FMOD bus volumes and slider preview events without coupling the UI to FMOD APIs. Preview
/// playback supports several simultaneous voices so the Master slider can preview every other slider event at once,
/// ramps each voice in and out so previews never click on start or cut on stop, and reports silently-suppressed
/// previews (paths already owned by managed audio such as the live background music) once so authoring mistakes
/// are easy to spot.
/// </summary>
public static class GameAudioSettingsFmodRuntimeUtility
{
    #region Constants
    // Headroom for the Master "play all other previews" mode, which previews the SFX and Music events together.
    private const int PreviewVoiceCapacity = 3;
    private const float DefaultPreviewFadeInSeconds = 0.04f;
    private const float DefaultPreviewFadeOutSeconds = 0.18f;
    private const float MinFadeSeconds = 0.0001f;
    private const float VolumeApplyEpsilon = 0.0005f;
    #endregion

    #region Fields
#if NASHCORE_FMOD
    private static readonly EventInstance[] previewInstances = new EventInstance[PreviewVoiceCapacity];
    private static readonly bool[] previewInstanceValid = new bool[PreviewVoiceCapacity];
    private static readonly string[] previewInstancePaths = new string[PreviewVoiceCapacity];

    // Per-voice fade state: target is the slider-driven volume, gain is the 0..1 fade multiplier applied on top of it,
    // applied is the last volume pushed to FMOD (to skip redundant calls), and fadingOut marks voices ramping to zero.
    private static readonly float[] previewTargetVolume = new float[PreviewVoiceCapacity];
    private static readonly float[] previewFadeGain = new float[PreviewVoiceCapacity];
    private static readonly float[] previewAppliedVolume = new float[PreviewVoiceCapacity];
    private static readonly bool[] previewFadingOut = new bool[PreviewVoiceCapacity];

    // Reusable desired-voice buffers reconciled against the tracked voices on every preview request, so dragging a
    // slider never allocates and never restarts an already-playing voice for the same event.
    private static readonly string[] desiredPaths = new string[PreviewVoiceCapacity];
    private static readonly string[] desiredBanks = new string[PreviewVoiceCapacity];
    private static readonly float[] desiredVolumes = new float[PreviewVoiceCapacity];
    private static int desiredCount;
#endif
    private static float previewFadeInSeconds = DefaultPreviewFadeInSeconds;
    private static float previewFadeOutSeconds = DefaultPreviewFadeOutSeconds;
    private static string lastBusDiagnosticKey;
    private static string lastSettingsPreviewDiagnosticKey;
    private static string lastSuppressedPreviewKey;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies a normalized volume scalar to one FMOD bus path when the FMOD backend is compiled in.
    /// </summary>
    /// <param name="busPath">FMOD bus path, for example bus:/SFX.</param>
    /// <param name="volume">Normalized volume scalar in the 0..1 range.</param>
    /// <param name="logWarnings">True when lookup failures should be logged in editor or development builds.</param>
    public static void SetBusVolume(string busPath, float volume, bool logWarnings)
    {
        if (string.IsNullOrWhiteSpace(busPath))
        {
            LogBusWarning("missing", "empty", logWarnings);
            return;
        }

#if NASHCORE_FMOD
        RESULT result = RuntimeManager.StudioSystem.getBus(busPath, out Bus bus);

        if (result != RESULT.OK)
        {
            LogBusWarning("resolve", busPath + "|" + result, logWarnings);
            return;
        }

        result = bus.setVolume(Mathf.Clamp01(volume));

        if (result != RESULT.OK)
            LogBusWarning("set volume", busPath + "|" + result, logWarnings);
#else
        LogFmodDisabled("bus volume: " + busPath, logWarnings);
#endif
    }

    /// <summary>
    /// Configures the fade-in and fade-out durations applied to preview voices. Called by the Settings menu so the
    /// timings can be authored on the prefab instead of hardcoded here.
    /// </summary>
    /// <param name="fadeInSeconds">Seconds a freshly started preview session takes to ramp up from silence.</param>
    /// <param name="fadeOutSeconds">Seconds a stopped preview voice takes to ramp down before it is released.</param>
    public static void ConfigurePreviewFades(float fadeInSeconds, float fadeOutSeconds)
    {
        previewFadeInSeconds = Mathf.Max(0f, fadeInSeconds);
        previewFadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
    }

    /// <summary>
    /// Plays or refreshes one settings-preview FMOD event as the only active preview voice. Used by the SFX and Music
    /// sliders and by the Master slider when it previews its own event.
    /// </summary>
    /// <param name="eventPath">FMOD event path to preview.</param>
    /// <param name="bankName">Optional FMOD bank name to load before resolving the event.</param>
    /// <param name="volume">Preview instance volume.</param>
    /// <param name="logWarnings">True when lookup failures should be logged in editor or development builds.</param>
    public static void PlayPreviewEvent(string eventPath, string bankName, float volume, bool logWarnings)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogSettingsPreviewWarning("missing path", "empty", logWarnings);
            StopPreviewEvent();
            return;
        }

#if NASHCORE_FMOD
        BeginDesired();
        AddDesired(eventPath, bankName, volume);
        ReconcilePreviewVoices(logWarnings);
#else
        LogFmodDisabled(eventPath, logWarnings);
#endif
    }

    /// <summary>
    /// Plays or refreshes up to two settings-preview FMOD events simultaneously. Used by the Master slider when it is
    /// configured to preview every other slider event (SFX and Music) at once instead of a single own event.
    /// </summary>
    /// <param name="firstEventPath">First FMOD event path to preview.</param>
    /// <param name="firstBankName">Optional FMOD bank name for the first event.</param>
    /// <param name="firstVolume">Preview volume for the first event.</param>
    /// <param name="secondEventPath">Second FMOD event path to preview.</param>
    /// <param name="secondBankName">Optional FMOD bank name for the second event.</param>
    /// <param name="secondVolume">Preview volume for the second event.</param>
    /// <param name="logWarnings">True when lookup failures should be logged in editor or development builds.</param>
    public static void PlayPreviewEvents(string firstEventPath,
                                         string firstBankName,
                                         float firstVolume,
                                         string secondEventPath,
                                         string secondBankName,
                                         float secondVolume,
                                         bool logWarnings)
    {
#if NASHCORE_FMOD
        BeginDesired();
        AddDesired(firstEventPath, firstBankName, firstVolume);
        AddDesired(secondEventPath, secondBankName, secondVolume);

        // No usable events means there is nothing to preview: fade out any lingering voices instead of leaving them on.
        if (desiredCount == 0)
        {
            LogSettingsPreviewWarning("missing path", "empty-set", logWarnings);
            StopPreviewEvent();
            return;
        }

        ReconcilePreviewVoices(logWarnings);
#else
        LogFmodDisabled(ResolveFirstNonEmptyPath(firstEventPath, secondEventPath), logWarnings);
#endif
    }

    /// <summary>
    /// Advances the fade ramp of every active preview voice. Driven by the Settings menu each frame on unscaled time so
    /// fades keep progressing while gameplay is paused.
    /// </summary>
    /// <param name="unscaledDeltaTime">Unscaled time elapsed since the previous tick.</param>
    public static void TickPreviewFades(float unscaledDeltaTime)
    {
#if NASHCORE_FMOD
        if (unscaledDeltaTime <= 0f)
            return;

        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
        {
            if (!previewInstanceValid[voiceIndex])
                continue;

            // Fading-out voices ramp to silence and are released; others ramp in toward full gain.
            if (previewFadingOut[voiceIndex])
            {
                previewFadeGain[voiceIndex] -= unscaledDeltaTime / Mathf.Max(MinFadeSeconds, previewFadeOutSeconds);

                if (previewFadeGain[voiceIndex] <= 0f)
                {
                    ReleaseVoiceImmediate(voiceIndex);
                    continue;
                }
            }
            else if (previewFadeGain[voiceIndex] < 1f)
            {
                previewFadeGain[voiceIndex] = Mathf.Min(1f, previewFadeGain[voiceIndex] + unscaledDeltaTime / Mathf.Max(MinFadeSeconds, previewFadeInSeconds));
            }

            ApplyVoiceVolume(voiceIndex);
        }
#endif
    }

    /// <summary>
    /// Fades out every active settings-preview voice. Used when the slider has not moved for a while so loop-capable
    /// previews do not linger or cut abruptly.
    /// </summary>
    public static void StopPreviewEvent()
    {
#if NASHCORE_FMOD
        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
            BeginVoiceFadeOut(voiceIndex);
#endif
    }

    /// <summary>
    /// Immediately stops and releases every settings-preview voice without fading. Used when the menu closes, where the
    /// fade tick can no longer run.
    /// </summary>
    public static void StopPreviewImmediate()
    {
#if NASHCORE_FMOD
        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
            ReleaseVoiceImmediate(voiceIndex);
#endif
    }
    #endregion

    #region Private Methods
#if NASHCORE_FMOD
    /// <summary>
    /// Resets the reusable desired-voice buffer before a new preview request is described.
    /// </summary>
    private static void BeginDesired()
    {
        desiredCount = 0;
    }

    /// <summary>
    /// Appends one requested preview voice to the desired buffer, skipping empty paths and duplicate event paths so a
    /// single FMOD voice is created per distinct event even when two sliders point at the same event.
    /// </summary>
    /// <param name="eventPath">FMOD event path requested for preview.</param>
    /// <param name="bankName">Optional FMOD bank for the event.</param>
    /// <param name="volume">Preview volume for the event.</param>
    private static void AddDesired(string eventPath, string bankName, float volume)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            return;

        if (desiredCount >= PreviewVoiceCapacity)
            return;

        // Collapse duplicate paths onto the loudest requested volume so the shared voice reflects either slider.
        for (int existingIndex = 0; existingIndex < desiredCount; existingIndex++)
        {
            if (!string.Equals(desiredPaths[existingIndex], eventPath, System.StringComparison.Ordinal))
                continue;

            desiredVolumes[existingIndex] = Mathf.Max(desiredVolumes[existingIndex], Mathf.Max(0f, volume));
            return;
        }

        desiredPaths[desiredCount] = eventPath;
        desiredBanks[desiredCount] = bankName;
        desiredVolumes[desiredCount] = Mathf.Max(0f, volume);
        desiredCount++;
    }

    /// <summary>
    /// Reconciles the tracked preview voices with the desired buffer: voices that are no longer wanted or that became
    /// owned by managed audio are faded out, surviving voices have their target volume refreshed, and missing voices
    /// are started. New voices fade in only when the preview session is starting from silence (or crossfading from a
    /// voice that is fading out) so re-triggered one-shot events stay punchy.
    /// </summary>
    /// <param name="logWarnings">True when lookup and suppression diagnostics should be logged.</param>
    private static void ReconcilePreviewVoices(bool logWarnings)
    {
        // Fade out tracked voices that are no longer requested or now collide with a managed (live) event.
        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
        {
            if (!previewInstanceValid[voiceIndex])
                continue;

            string trackedPath = previewInstancePaths[voiceIndex];

            if (!IsPathDesired(trackedPath) || GameAudioFmodRuntimeUtility.IsManagedEventPathActive(trackedPath))
                BeginVoiceFadeOut(voiceIndex);
        }

        // Captured after the fade-out pass so a fresh session (or a crossfade replacing a fading voice) ramps in.
        bool fadeInNewVoices = CountActiveVoices() == 0;

        // Start or refresh one voice per distinct desired event.
        for (int desiredIndex = 0; desiredIndex < desiredCount; desiredIndex++)
        {
            string eventPath = desiredPaths[desiredIndex];

            // A path already owned by managed audio (for example the live background music) is intentionally not
            // double-played; reporting it once explains an otherwise silent slider.
            if (GameAudioFmodRuntimeUtility.IsManagedEventPathActive(eventPath))
            {
                LogPreviewSuppressed(eventPath, logWarnings);
                continue;
            }

            if (TryRefreshExistingVoice(eventPath, desiredVolumes[desiredIndex]))
                continue;

            StartPreviewVoice(eventPath, desiredBanks[desiredIndex], desiredVolumes[desiredIndex], fadeInNewVoices, logWarnings);
        }
    }

    /// <summary>
    /// Refreshes a tracked voice that is still playing the requested event: it updates the target volume and cancels a
    /// pending fade-out (reviving it), but reports a finished or invalid voice as not-refreshable so the caller
    /// re-triggers it. This keeps short one-shot preview events (such as the weapon shot) repeating while the slider is
    /// actively edited instead of sounding a single time.
    /// </summary>
    /// <param name="eventPath">FMOD event path to refresh.</param>
    /// <param name="volume">New target preview volume.</param>
    /// <returns>True when a still-playing voice for the path was found and refreshed.</returns>
    private static bool TryRefreshExistingVoice(string eventPath, float volume)
    {
        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
        {
            if (!previewInstanceValid[voiceIndex])
                continue;

            if (!string.Equals(previewInstancePaths[voiceIndex], eventPath, System.StringComparison.Ordinal))
                continue;

            if (!previewInstances[voiceIndex].isValid())
            {
                ReleaseVoiceImmediate(voiceIndex);
                return false;
            }

            // A one-shot that already reached the end is released so the next slider change re-triggers it.
            RESULT playbackResult = previewInstances[voiceIndex].getPlaybackState(out PLAYBACK_STATE playbackState);

            if (playbackResult != RESULT.OK || playbackState == PLAYBACK_STATE.STOPPED || playbackState == PLAYBACK_STATE.STOPPING)
            {
                ReleaseVoiceImmediate(voiceIndex);
                return false;
            }

            // Still playing: revive a fading voice and let the next tick apply the refreshed slider volume.
            previewFadingOut[voiceIndex] = false;
            previewTargetVolume[voiceIndex] = Mathf.Max(0f, volume);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates and starts one tracked preview voice for an event path, loading its optional bank first and seeding its
    /// fade state.
    /// </summary>
    /// <param name="eventPath">FMOD event path to play.</param>
    /// <param name="bankName">Optional FMOD bank name loaded before resolving the event.</param>
    /// <param name="volume">Target preview volume.</param>
    /// <param name="fadeIn">True when the voice should ramp in from silence instead of starting at full volume.</param>
    /// <param name="logWarnings">True when lookup failures should be logged.</param>
    private static void StartPreviewVoice(string eventPath, string bankName, float volume, bool fadeIn, bool logWarnings)
    {
        int freeIndex = FindFreePreviewSlot();

        if (freeIndex < 0)
            return;

        if (!EnsurePreviewBankLoaded(bankName, logWarnings))
            return;

        RESULT result = RuntimeManager.StudioSystem.getEvent(eventPath, out EventDescription eventDescription);

        if (result != RESULT.OK)
        {
            LogSettingsPreviewWarning("resolve event", eventPath + "|" + result, logWarnings);
            return;
        }

        result = eventDescription.createInstance(out EventInstance instance);

        if (result != RESULT.OK)
        {
            LogSettingsPreviewWarning("create instance", eventPath + "|" + result, logWarnings);
            return;
        }

        float targetVolume = Mathf.Max(0f, volume);
        float fadeGain = fadeIn && previewFadeInSeconds > MinFadeSeconds ? 0f : 1f;
        float startVolume = targetVolume * fadeGain;
        instance.setVolume(startVolume);
        result = instance.start();

        if (result != RESULT.OK)
        {
            LogSettingsPreviewWarning("start", eventPath + "|" + result, logWarnings);
            instance.release();
            return;
        }

        previewInstances[freeIndex] = instance;
        previewInstanceValid[freeIndex] = true;
        previewInstancePaths[freeIndex] = eventPath;
        previewTargetVolume[freeIndex] = targetVolume;
        previewFadeGain[freeIndex] = fadeGain;
        previewAppliedVolume[freeIndex] = startVolume;
        previewFadingOut[freeIndex] = false;
    }

    /// <summary>
    /// Pushes the effective volume (slider target times fade gain) to one voice, skipping redundant FMOD calls when it
    /// has not changed meaningfully since the last apply.
    /// </summary>
    /// <param name="voiceIndex">Tracked preview voice slot index.</param>
    private static void ApplyVoiceVolume(int voiceIndex)
    {
        if (!previewInstances[voiceIndex].isValid())
        {
            ReleaseVoiceImmediate(voiceIndex);
            return;
        }

        float effectiveVolume = previewTargetVolume[voiceIndex] * previewFadeGain[voiceIndex];

        if (Mathf.Abs(effectiveVolume - previewAppliedVolume[voiceIndex]) < VolumeApplyEpsilon)
            return;

        previewInstances[voiceIndex].setVolume(effectiveVolume);
        previewAppliedVolume[voiceIndex] = effectiveVolume;
    }

    /// <summary>
    /// Marks one tracked voice to fade out on the next ticks, or releases it immediately when fade-out is disabled.
    /// </summary>
    /// <param name="voiceIndex">Tracked preview voice slot index.</param>
    private static void BeginVoiceFadeOut(int voiceIndex)
    {
        if (!previewInstanceValid[voiceIndex])
            return;

        if (previewFadeOutSeconds <= MinFadeSeconds)
        {
            ReleaseVoiceImmediate(voiceIndex);
            return;
        }

        previewFadingOut[voiceIndex] = true;
    }

    /// <summary>
    /// Stops, releases and clears one tracked preview voice slot if it is still alive.
    /// </summary>
    /// <param name="voiceIndex">Tracked preview voice slot index.</param>
    private static void ReleaseVoiceImmediate(int voiceIndex)
    {
        if (!previewInstanceValid[voiceIndex])
            return;

        EventInstance instance = previewInstances[voiceIndex];
        previewInstanceValid[voiceIndex] = false;
        previewInstances[voiceIndex] = default;
        previewInstancePaths[voiceIndex] = string.Empty;
        previewTargetVolume[voiceIndex] = 0f;
        previewFadeGain[voiceIndex] = 0f;
        previewAppliedVolume[voiceIndex] = 0f;
        previewFadingOut[voiceIndex] = false;

        if (!instance.isValid())
            return;

        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        instance.release();
    }

    /// <summary>
    /// Counts tracked voices that are alive and not already fading out, used to decide whether a starting voice begins
    /// a fresh fade-in session.
    /// </summary>
    /// <returns>Number of active (non-fading) preview voices.</returns>
    private static int CountActiveVoices()
    {
        int activeCount = 0;

        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
        {
            if (previewInstanceValid[voiceIndex] && !previewFadingOut[voiceIndex])
                activeCount++;
        }

        return activeCount;
    }

    /// <summary>
    /// Finds the first free tracked preview voice slot.
    /// </summary>
    /// <returns>Free slot index, or -1 when every voice slot is in use.</returns>
    private static int FindFreePreviewSlot()
    {
        for (int voiceIndex = 0; voiceIndex < PreviewVoiceCapacity; voiceIndex++)
        {
            if (!previewInstanceValid[voiceIndex])
                return voiceIndex;
        }

        return -1;
    }

    /// <summary>
    /// Checks whether one tracked event path is still part of the current desired preview set.
    /// </summary>
    /// <param name="eventPath">Tracked FMOD event path to test.</param>
    /// <returns>True when the path is still requested by the active preview.</returns>
    private static bool IsPathDesired(string eventPath)
    {
        for (int desiredIndex = 0; desiredIndex < desiredCount; desiredIndex++)
        {
            if (string.Equals(desiredPaths[desiredIndex], eventPath, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Loads an optional settings-preview bank before resolving its event path.
    /// </summary>
    /// <param name="bankName">FMOD bank name to load, or empty when already loaded.</param>
    /// <param name="shouldLog">True when failures should be logged.</param>
    /// <returns>True when preview playback can continue.</returns>
    private static bool EnsurePreviewBankLoaded(string bankName, bool shouldLog)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            return true;

        try
        {
            RuntimeManager.LoadBank(bankName);
        }
        catch (System.Exception exception)
        {
            LogSettingsPreviewWarning("load bank", bankName + "|" + exception.GetType().Name, shouldLog);
            return false;
        }

        return true;
    }
#else
    /// <summary>
    /// Resolves the first non-empty path from a preview pair for disabled-backend diagnostics.
    /// </summary>
    /// <param name="firstEventPath">First candidate path.</param>
    /// <param name="secondEventPath">Second candidate path.</param>
    /// <returns>First non-empty path, or an empty string when both are empty.</returns>
    private static string ResolveFirstNonEmptyPath(string firstEventPath, string secondEventPath)
    {
        if (!string.IsNullOrWhiteSpace(firstEventPath))
            return firstEventPath;

        if (!string.IsNullOrWhiteSpace(secondEventPath))
            return secondEventPath;

        return string.Empty;
    }
#endif

    /// <summary>
    /// Logs the disabled-backend state when FMOD integration has not been compiled into the project.
    /// </summary>
    /// <param name="target">Target that would have been controlled through FMOD.</param>
    /// <param name="shouldLog">True when diagnostics should be logged.</param>
    private static void LogFmodDisabled(string target, bool shouldLog)
    {
        if (!shouldLog)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[GameAudioSettings] FMOD backend is disabled. Define NASHCORE_FMOD after installing FMOD Unity integration to control: " + target);
#endif
    }

    /// <summary>
    /// Logs FMOD bus-routing diagnostics once per unique operation and target.
    /// </summary>
    /// <param name="operation">Operation being attempted.</param>
    /// <param name="target">Bus path or diagnostic target.</param>
    /// <param name="shouldLog">True when diagnostics should be logged.</param>
    private static void LogBusWarning(string operation, string target, bool shouldLog)
    {
        if (!shouldLog)
            return;

        string diagnosticKey = operation + "|" + target;

        if (string.Equals(lastBusDiagnosticKey, diagnosticKey, System.StringComparison.Ordinal))
            return;

        lastBusDiagnosticKey = diagnosticKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudioSettings] Failed to " + operation + " FMOD bus '" + target + "'.");
#endif
    }

    /// <summary>
    /// Logs settings-preview diagnostics once per unique operation and target.
    /// </summary>
    /// <param name="operation">Operation being attempted.</param>
    /// <param name="target">FMOD event path, bank name or diagnostic target.</param>
    /// <param name="shouldLog">True when diagnostics should be logged.</param>
    private static void LogSettingsPreviewWarning(string operation, string target, bool shouldLog)
    {
        if (!shouldLog)
            return;

        string diagnosticKey = operation + "|" + target;

        if (string.Equals(lastSettingsPreviewDiagnosticKey, diagnosticKey, System.StringComparison.Ordinal))
            return;

        lastSettingsPreviewDiagnosticKey = diagnosticKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudioSettings] Preview failed to " + operation + " for '" + target + "'.");
#endif
    }

    /// <summary>
    /// Reports once that a preview was intentionally not played because its event is already owned by managed audio,
    /// which is the usual reason a Master or Music slider that points at the live background music stays silent.
    /// </summary>
    /// <param name="eventPath">FMOD event path that was suppressed.</param>
    /// <param name="shouldLog">True when diagnostics should be logged.</param>
    private static void LogPreviewSuppressed(string eventPath, bool shouldLog)
    {
        if (!shouldLog)
            return;

        if (string.Equals(lastSuppressedPreviewKey, eventPath, System.StringComparison.Ordinal))
            return;

        lastSuppressedPreviewKey = eventPath;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[GameAudioSettings] Preview '" + eventPath + "' was not played because it is already active as managed audio (for example the live background music). Pick a different preview event or enable 'Master Plays All Previews'.");
#endif
    }
    #endregion

    #endregion
}
