using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes gameplay audio requests from the audio singleton and dispatches them to the FMOD wrapper.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct GameAudioPlaybackSystem : ISystem
{
    #region Fields
    private static readonly bool[] cachedEventPathValidById = new bool[byte.MaxValue + 1];
    private static readonly FixedString512Bytes[] cachedEventFixedPathById = new FixedString512Bytes[byte.MaxValue + 1];
    private static readonly string[] cachedEventManagedPathById = new string[byte.MaxValue + 1];
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the singleton buffers required for runtime playback.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameAudioRuntimeConfig>();
        state.RequireForUpdate<GameAudioEventBindingElement>();
        state.RequireForUpdate<GameAudioEventRequest>();
        state.RequireForUpdate<GameAudioRateLimitStateElement>();
    }

    /// <summary>
    /// Stops tracked gameplay voices on world teardown; the music system owns music cleanup.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        GameAudioFmodRuntimeUtility.StopAllTrackedSingleInstances();
    }

    /// <summary>
    /// Resolves queued audio requests, applies per-event caps, and clears consumed requests.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        GameAudioFmodRuntimeUtility.UpdateGlobalVoices();
        Entity audioEntity = SystemAPI.GetSingletonEntity<GameAudioRuntimeConfig>();
        EntityManager entityManager = state.EntityManager;
        GameAudioRuntimeConfig runtimeConfig = entityManager.GetComponentData<GameAudioRuntimeConfig>(audioEntity);
        DynamicBuffer<GameAudioEventRequest> requests = entityManager.GetBuffer<GameAudioEventRequest>(audioEntity);

        if (runtimeConfig.Enabled == 0)
        {
            requests.Clear();
            return;
        }

        if (requests.Length <= 0)
            return;

        DynamicBuffer<GameAudioEventBindingElement> bindings = entityManager.GetBuffer<GameAudioEventBindingElement>(audioEntity);
        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates = entityManager.GetBuffer<GameAudioRateLimitStateElement>(audioEntity);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        for (int requestIndex = 0; requestIndex < requests.Length; requestIndex++)
        {
            GameAudioEventRequest request = requests[requestIndex];

            // Stop requests bypass binding resolution and rate-limits: their only job is to silence the tracked
            // single-instance voice for the event id so continuous sources end cleanly.
            if (request.StopRequest != 0)
            {
                GameAudioFmodRuntimeUtility.StopTrackedSingleInstanceById(request.EventId);
                continue;
            }

            if (!TryResolveBinding(bindings, request.EventId, out GameAudioEventBindingElement binding))
                continue;

            if (!CanPlayNow(rateLimitStates, binding, elapsedTime))
                continue;

            float volume = math.max(0f, runtimeConfig.MasterVolume) *
                           math.max(0f, binding.Volume) *
                           math.max(0f, request.VolumeMultiplier);
            float pitch = math.max(0.0001f, binding.Pitch) *
                          math.max(0.0001f, request.PitchMultiplier);
            // Per-binding values authored in the Audio Manager preset take precedence; non-positive values
            // fall back to the global defaults so missing per-event tuning never collapses into a 0-distance curve.
            float minimumDistance = binding.MinimumDistance > 0f
                ? binding.MinimumDistance
                : math.max(0f, runtimeConfig.DefaultMinimumDistance);
            float maximumDistance = binding.MaximumDistance > 0f
                ? math.max(minimumDistance, binding.MaximumDistance)
                : math.max(minimumDistance, runtimeConfig.DefaultMaximumDistance);

            GameAudioFmodRuntimeUtility.PlayOneShot(binding.EventId,
                                                    ResolveManagedEventPath(in binding),
                                                    request.Position,
                                                    request.HasPosition != 0 && binding.Spatialize != 0,
                                                    volume,
                                                    pitch,
                                                    minimumDistance,
                                                    maximumDistance,
                                                    binding.SingleInstance != 0,
                                                    runtimeConfig.LogMissingEventPaths != 0);
        }

        requests.Clear();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Finds the first binding matching a requested event ID.
    /// </summary>
    /// <param name="bindings">Baked binding buffer.</param>
    /// <param name="eventId">Requested event identifier.</param>
    /// <param name="binding">Output binding when found.</param>
    /// <returns>True when a matching binding exists.</returns>
    private static bool TryResolveBinding(DynamicBuffer<GameAudioEventBindingElement> bindings,
                                          GameAudioEventId eventId,
                                          out GameAudioEventBindingElement binding)
    {
        for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
        {
            GameAudioEventBindingElement candidate = bindings[bindingIndex];

            if (candidate.EventId != eventId)
                continue;

            binding = candidate;
            return true;
        }

        binding = default;
        return false;
    }

    /// <summary>
    /// Resolves one baked event path to a managed string only when the fixed string changes.
    /// </summary>
    /// <param name="binding">Baked audio event binding.</param>
    /// <returns>Cached managed event path.</returns>
    private static string ResolveManagedEventPath(in GameAudioEventBindingElement binding)
    {
        int eventIndex = (byte)binding.EventId;
        FixedString512Bytes eventPath = binding.EventPath;

        if (!cachedEventPathValidById[eventIndex] || !cachedEventFixedPathById[eventIndex].Equals(eventPath))
        {
            cachedEventFixedPathById[eventIndex] = eventPath;
            cachedEventManagedPathById[eventIndex] = eventPath.ToString();
            cachedEventPathValidById[eventIndex] = true;
        }

        return cachedEventManagedPathById[eventIndex];
    }

    /// <summary>
    /// Applies one binding's runtime rate limit and records the accepted play when allowed.
    /// </summary>
    /// <param name="rateLimitStates">Mutable singleton buffer storing event windows.</param>
    /// <param name="binding">Event binding being evaluated.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    /// <returns>True when playback may proceed.</returns>
    private static bool CanPlayNow(DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates,
                                   GameAudioEventBindingElement binding,
                                   float elapsedTime)
    {
        if (binding.RateLimitEnabled == 0)
            return true;

        if (binding.MaxPlaysPerWindow <= 0 || binding.WindowSeconds <= 0f)
            return true;

        int stateIndex = FindRateLimitStateIndex(rateLimitStates, binding.EventId);

        if (stateIndex < 0)
        {
            rateLimitStates.Add(new GameAudioRateLimitStateElement
            {
                EventId = binding.EventId,
                WindowStartTime = elapsedTime,
                PlaysInWindow = 1
            });
            return true;
        }

        GameAudioRateLimitStateElement rateLimitState = rateLimitStates[stateIndex];
        float elapsedInWindow = elapsedTime - rateLimitState.WindowStartTime;

        if (elapsedInWindow >= binding.WindowSeconds || elapsedInWindow < 0f)
        {
            rateLimitState.WindowStartTime = elapsedTime;
            rateLimitState.PlaysInWindow = 0;
        }

        if (rateLimitState.PlaysInWindow >= binding.MaxPlaysPerWindow)
        {
            rateLimitStates[stateIndex] = rateLimitState;
            return false;
        }

        rateLimitState.PlaysInWindow++;
        rateLimitStates[stateIndex] = rateLimitState;
        return true;
    }

    /// <summary>
    /// Finds the buffer index for a rate-limit state entry.
    /// </summary>
    /// <param name="rateLimitStates">Mutable singleton state buffer.</param>
    /// <param name="eventId">Event identifier to search.</param>
    /// <returns>Buffer index when found; otherwise -1.</returns>
    private static int FindRateLimitStateIndex(DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates,
                                               GameAudioEventId eventId)
    {
        for (int index = 0; index < rateLimitStates.Length; index++)
        {
            if (rateLimitStates[index].EventId != eventId)
                continue;

            return index;
        }

        return -1;
    }
    #endregion

    #endregion
}
