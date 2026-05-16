using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Lightweight helpers used by ECS systems to enqueue gameplay audio requests.
/// </summary>
public static class GameAudioEventRequestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one positioned audio event request to an already resolved singleton request buffer.
    /// </summary>
    /// <param name="requests">Mutable singleton audio request buffer.</param>
    /// <param name="eventId">Gameplay audio event to request.</param>
    /// <param name="position">World-space position for 3D playback.</param>
    public static void EnqueuePositioned(DynamicBuffer<GameAudioEventRequest> requests,
                                         GameAudioEventId eventId,
                                         float3 position)
    {
        Enqueue(requests, eventId, position, true, 1f, 1f);
    }

    /// <summary>
    /// Adds one non-positioned audio event request to an already resolved singleton request buffer.
    /// </summary>
    /// <param name="requests">Mutable singleton audio request buffer.</param>
    /// <param name="eventId">Gameplay audio event to request.</param>
    public static void EnqueueGlobal(DynamicBuffer<GameAudioEventRequest> requests, GameAudioEventId eventId)
    {
        Enqueue(requests, eventId, float3.zero, false, 1f, 1f);
    }

    /// <summary>
    /// Adds one audio request with explicit playback multipliers.
    /// </summary>
    /// <param name="requests">Mutable singleton audio request buffer.</param>
    /// <param name="eventId">Gameplay audio event to request.</param>
    /// <param name="position">World-space position for 3D playback.</param>
    /// <param name="hasPosition">True when position should be used.</param>
    /// <param name="volumeMultiplier">Request-local volume multiplier.</param>
    /// <param name="pitchMultiplier">Request-local pitch multiplier.</param>
    public static void Enqueue(DynamicBuffer<GameAudioEventRequest> requests,
                               GameAudioEventId eventId,
                               float3 position,
                               bool hasPosition,
                               float volumeMultiplier,
                               float pitchMultiplier)
    {
        if (!requests.IsCreated)
            return;

        if (eventId == GameAudioEventId.None)
            return;

        requests.Add(new GameAudioEventRequest
        {
            EventId = eventId,
            Position = position,
            HasPosition = hasPosition ? (byte)1 : (byte)0,
            VolumeMultiplier = volumeMultiplier,
            PitchMultiplier = pitchMultiplier
        });
    }
    #endregion

    #endregion
}
