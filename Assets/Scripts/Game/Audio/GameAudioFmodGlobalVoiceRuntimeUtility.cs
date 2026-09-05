#if NASHCORE_FMOD || UNITY_EDITOR
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using UnityEngine;

/// <summary>
/// Keeps non-spatialized voices authored as 3D events at the listener for their entire playback.
/// </summary>
internal static class GameAudioFmodGlobalVoiceRuntimeUtility
{
    #region Fields
    private static readonly List<GlobalVoice> voices = new List<GlobalVoice>(32);
    #endregion

    #region Types
    /// <summary>
    /// Retains a borrowed handle; release ownership stays with the one-shot or single-instance store.
    /// </summary>
    private readonly struct GlobalVoice
    {
        public readonly EventInstance Instance;
        public readonly int StartFrame;

        #region Methods
        /// <summary>
        /// Records the creation frame so queued FMOD starts are not mistaken for completed playback.
        /// </summary>
        /// <param name="instance">Started instance whose attributes must follow the listener.</param>
        public GlobalVoice(EventInstance instance)
        {
            Instance = instance;
            StartFrame = Time.frameCount;
        }
        #endregion
    }
    #endregion

    #region Methods

    #region Playback
    /// <summary>
    /// Tracks only authored 3D events; ordinary 2D UI and player sounds need no frame updates.
    /// </summary>
    /// <param name="instance">Borrowed instance handle after a successful start.</param>
    public static void Track(EventInstance instance)
    {
        // Inspect the event once, outside the active-voice update loop.
        if (instance.getDescription(out EventDescription description) != RESULT.OK ||
            description.is3D(out bool is3D) != RESULT.OK || !is3D)
            return;

        voices.Add(new GlobalVoice(instance));
    }

    /// <summary>
    /// Updates only live global 3D voices, sharing one listener snapshot across the whole batch.
    /// </summary>
    public static void Update()
    {
        if (voices.Count == 0)
            return;

        ATTRIBUTES_3D attributes = GameAudioFmodAttributesRuntimeUtility.ResolveListenerCenteredAttributes(Time.unscaledTime);
        UpdateAnchors(in attributes);
    }

    /// <summary>
    /// Applies one shared listener snapshot to every tracked global voice.
    /// </summary>
    /// <param name="attributes">Current listener-centered position and orientation.</param>
    internal static void UpdateAnchors(in ATTRIBUTES_3D attributes)
    {
        // Remove completed handles by swapping with the tail; ordering has no playback meaning.
        for (int index = voices.Count - 1; index >= 0; index--)
        {
            GlobalVoice voice = voices[index];
            EventInstance instance = voice.Instance;

            if (!instance.isValid() ||
                (voice.StartFrame != Time.frameCount &&
                 (instance.getPlaybackState(out PLAYBACK_STATE playbackState) != RESULT.OK || playbackState == PLAYBACK_STATE.STOPPED)))
            {
                voices[index] = voices[voices.Count - 1];
                voices.RemoveAt(voices.Count - 1);
                continue;
            }

            instance.set3DAttributes(attributes);
        }
    }

    /// <summary>
    /// Stops borrowed voices during world teardown and discards stale listener references.
    /// </summary>
    public static void StopAll()
    {
        // Owners already manage releases; this store only requests the final stop.
        for (int index = 0; index < voices.Count; index++)
        {
            EventInstance instance = voices[index].Instance;

            if (instance.isValid())
                instance.stop(STOP_MODE.IMMEDIATE);
        }

        voices.Clear();
        GameAudioFmodAttributesRuntimeUtility.ClearCachedListener();
    }
    #endregion

    #endregion
}
#endif
