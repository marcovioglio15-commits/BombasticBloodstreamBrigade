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

        if (!hasPosition)
            GameAudioFmodGlobalVoiceRuntimeUtility.Track(instance);

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
    /// Prepares music resources under the scene-transition overlay without starting playback.
    /// </summary>
    /// <param name="eventPath">Music event that may be selected after loading.</param>
    /// <param name="bankName">Owning FMOD bank.</param>
    /// <param name="logMissingEventPath">Whether failures should emit diagnostics.</param>
    /// <returns>True when resources are ready or the optional path is empty.</returns>
    public static bool PrepareBackgroundMusic(string eventPath, string bankName, bool logMissingEventPath)
    {
        return GameAudioFmodMusicRuntimeUtility.Prepare(eventPath, bankName, logMissingEventPath);
    }

    /// <summary>
    /// Checks all music contexts, including outgoing crossfade voices.
    /// </summary>
    /// <param name="eventPath">Path requested by a settings-menu preview.</param>
    /// <returns>True when a live music instance owns the requested event.</returns>
    public static bool IsBackgroundMusicEventActive(string eventPath)
    {
        return GameAudioFmodMusicRuntimeUtility.IsEventActive(eventPath);
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
        GameAudioFmodGlobalVoiceRuntimeUtility.StopAll();
        GameAudioFmodSingleInstanceRuntimeUtility.StopAllTrackedSingleInstances();
#endif
    }

    /// <summary>
    /// Advances listener anchoring even when the ECS request buffer is empty.
    /// </summary>
    public static void UpdateGlobalVoices()
    {
#if NASHCORE_FMOD || UNITY_EDITOR
        GameAudioFmodGlobalVoiceRuntimeUtility.Update();
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

#endif

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

    #endregion

    #endregion
}
