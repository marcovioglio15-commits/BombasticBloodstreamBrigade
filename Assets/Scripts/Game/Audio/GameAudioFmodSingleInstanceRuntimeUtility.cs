#if NASHCORE_FMOD || UNITY_EDITOR
using FMOD;
using FMOD.Studio;

/// <summary>
/// Tracks FMOD single-instance gameplay voices so repeated events can steal earlier still-playing instances.
/// </summary>
internal static class GameAudioFmodSingleInstanceRuntimeUtility
{
    #region Fields
    private static readonly EventInstance[] singleInstanceByEventId = new EventInstance[byte.MaxValue + 1];
    private static readonly bool[] singleInstanceValidByEventId = new bool[byte.MaxValue + 1];
    private static readonly string[] singleInstanceEventPathByEventId = new string[byte.MaxValue + 1];
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Stops and releases the previously tracked single-instance voice for one gameplay event id, so a fresh
    /// request can take over without overlapping the existing playback.
    /// </summary>
    /// <param name="eventId">Gameplay event id whose tracked instance should be stolen.</param>
    public static void StopTrackedSingleInstance(GameAudioEventId eventId)
    {
        int eventIndex = (byte)eventId;

        if (!singleInstanceValidByEventId[eventIndex])
            return;

        EventInstance trackedInstance = singleInstanceByEventId[eventIndex];
        ClearTrackedInstance(eventIndex);

        if (!trackedInstance.isValid())
            return;

        trackedInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        trackedInstance.release();
    }

    /// <summary>
    /// Stores the freshly started instance so the next single-instance request for the same gameplay event id can steal it.
    /// </summary>
    /// <param name="eventId">Gameplay event id keyed into the tracking store.</param>
    /// <param name="instance">Newly started FMOD instance to track.</param>
    /// <param name="eventPath">FMOD event path represented by the tracked instance.</param>
    public static void StoreTrackedSingleInstance(GameAudioEventId eventId, EventInstance instance, string eventPath)
    {
        int eventIndex = (byte)eventId;
        singleInstanceByEventId[eventIndex] = instance;
        singleInstanceValidByEventId[eventIndex] = true;
        singleInstanceEventPathByEventId[eventIndex] = eventPath ?? string.Empty;
    }

    /// <summary>
    /// Checks tracked single-instance gameplay voices for one active FMOD event path.
    /// </summary>
    /// <param name="eventPath">FMOD event path to search.</param>
    /// <returns>True when a still-playing tracked gameplay instance uses the requested path.</returns>
    public static bool IsTrackedSingleInstanceEventPathActive(string eventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            return false;

        for (int eventIndex = 0; eventIndex < singleInstanceValidByEventId.Length; eventIndex++)
        {
            if (!singleInstanceValidByEventId[eventIndex])
                continue;

            if (!string.Equals(singleInstanceEventPathByEventId[eventIndex], eventPath, System.StringComparison.Ordinal))
                continue;

            if (IsTrackedInstancePlaying(eventIndex))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Stops every still-playing single-instance voice tracked by gameplay events during audio system cleanup.
    /// </summary>
    public static void StopAllTrackedSingleInstances()
    {
        for (int eventIndex = 0; eventIndex < singleInstanceValidByEventId.Length; eventIndex++)
        {
            if (!singleInstanceValidByEventId[eventIndex])
                continue;

            EventInstance trackedInstance = singleInstanceByEventId[eventIndex];
            ClearTrackedInstance(eventIndex);

            if (!trackedInstance.isValid())
                continue;

            trackedInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            trackedInstance.release();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether one tracked FMOD instance is still valid and actively playing.
    /// </summary>
    /// <param name="eventIndex">Byte-backed gameplay event index inside the tracking arrays.</param>
    /// <returns>True when FMOD reports the tracked instance as playing or starting.</returns>
    private static bool IsTrackedInstancePlaying(int eventIndex)
    {
        EventInstance trackedInstance = singleInstanceByEventId[eventIndex];

        if (!trackedInstance.isValid())
            return false;

        RESULT result = trackedInstance.getPlaybackState(out PLAYBACK_STATE playbackState);

        if (result != RESULT.OK)
            return false;

        return playbackState != PLAYBACK_STATE.STOPPED && playbackState != PLAYBACK_STATE.STOPPING;
    }

    /// <summary>
    /// Clears one tracking slot after ownership has been transferred to the caller.
    /// </summary>
    /// <param name="eventIndex">Byte-backed gameplay event index inside the tracking arrays.</param>
    private static void ClearTrackedInstance(int eventIndex)
    {
        singleInstanceValidByEventId[eventIndex] = false;
        singleInstanceByEventId[eventIndex] = default;
        singleInstanceEventPathByEventId[eventIndex] = string.Empty;
    }
    #endregion

    #endregion
}
#endif
