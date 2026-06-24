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
#if UNITY_WEBGL && !UNITY_EDITOR
    private const float WebGlWarmupStartDelaySeconds = 1.5f;
    private const float WebGlWarmupIntervalSeconds = 0.08f;
    private const int WebGlMaxOneShotStartsPerFrame = 8;
    private const float WebGlBackgroundMusicStartDelaySeconds = 0.5f;
#endif
    private static readonly bool[] cachedEventPathValidById = new bool[byte.MaxValue + 1];
    private static readonly FixedString512Bytes[] cachedEventFixedPathById = new FixedString512Bytes[byte.MaxValue + 1];
    private static readonly string[] cachedEventManagedPathById = new string[byte.MaxValue + 1];
    private static bool cachedBackgroundMusicPathValid;
    private static FixedString512Bytes cachedBackgroundMusicFixedPath;
    private static string cachedBackgroundMusicManagedPath;
    private static bool cachedBackgroundMusicBankValid;
    private static FixedString64Bytes cachedBackgroundMusicFixedBank;
    private static string cachedBackgroundMusicManagedBank;
#if UNITY_WEBGL && !UNITY_EDITOR
    private static int webGlWarmupCursor;
    private static float webGlNextWarmupTime;
    private static bool webGlBackgroundMusicDelayPending = true;
    private static float webGlBackgroundMusicReadyTime;
#endif
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

#if UNITY_WEBGL && !UNITY_EDITOR
        webGlWarmupCursor = 0;
        webGlNextWarmupTime = WebGlWarmupStartDelaySeconds;
        webGlBackgroundMusicDelayPending = true;
        webGlBackgroundMusicReadyTime = 0f;
#endif
    }

    /// <summary>
    /// Stops managed background music and any still-tracked single-instance gameplay voice when the ECS audio
    /// playback system is destroyed so stale FMOD handles do not survive into the next play session.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnDestroy(ref SystemState state)
    {
        GameAudioFmodRuntimeUtility.StopBackgroundMusic();
        GameAudioFmodRuntimeUtility.StopAllTrackedSingleInstances();
        GameAudioFmodRuntimeUtility.StopAllWebGlGuardedOneShots();
    }

    /// <summary>
    /// Resolves queued audio requests, applies per-event caps, and clears consumed requests.
    /// </summary>
    /// <param name="state">Mutable system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        Entity audioEntity = SystemAPI.GetSingletonEntity<GameAudioRuntimeConfig>();
        EntityManager entityManager = state.EntityManager;
        GameAudioRuntimeConfig runtimeConfig = entityManager.GetComponentData<GameAudioRuntimeConfig>(audioEntity);
        DynamicBuffer<GameAudioEventRequest> requests = entityManager.GetBuffer<GameAudioEventRequest>(audioEntity);
        DynamicBuffer<GameAudioEventBindingElement> bindings = entityManager.GetBuffer<GameAudioEventBindingElement>(audioEntity);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        bool hasTransitionState = SystemAPI.TryGetSingleton<GameSceneTransitionState>(out GameSceneTransitionState transitionState);
        bool shouldStopBackgroundMusicForMainMenu = SystemAPI.TryGetSingleton<GameSceneManagerConfig>(out GameSceneManagerConfig sceneConfig) &&
                                                    hasTransitionState &&
                                                    ShouldStopBackgroundMusicForMainMenu(in sceneConfig, in transitionState);
#if UNITY_WEBGL && !UNITY_EDITOR
        GameAudioFmodRuntimeUtility.UpdateWebGlGuardedOneShots(runtimeConfig.LogMissingEventPaths != 0);
        bool webGlRestartTransitionActive = hasTransitionState && IsRestartTransitionActive(in transitionState);
        GameAudioFmodRuntimeUtility.UpdateWebGlReloadTransitionFade(webGlRestartTransitionActive,
                                                                   UnityEngine.Time.unscaledDeltaTime,
                                                                   runtimeConfig.LogMissingEventPaths != 0);
#endif

        if (runtimeConfig.Enabled == 0)
        {
            SyncBackgroundMusic(in runtimeConfig, false, false, 0f);
            requests.Clear();
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WarmUpWebGlAudioEvents(bindings, in runtimeConfig, elapsedTime);
        UpdateWebGlBackgroundMusicDelay(shouldStopBackgroundMusicForMainMenu || webGlRestartTransitionActive, elapsedTime);
        bool backgroundMusicAutoStart = runtimeConfig.BackgroundMusicAutoStart != 0 && IsWebGlBackgroundMusicReady(elapsedTime);
#else
        bool backgroundMusicAutoStart = runtimeConfig.BackgroundMusicAutoStart != 0;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        if (webGlRestartTransitionActive)
        {
            GameAudioFmodRuntimeUtility.StopBackgroundMusic();
            requests.Clear();
            return;
        }
#endif

        if (shouldStopBackgroundMusicForMainMenu)
            GameAudioFmodRuntimeUtility.StopBackgroundMusicImmediate();
        else
        {
            SyncBackgroundMusic(in runtimeConfig,
                                runtimeConfig.BackgroundMusicEnabled != 0,
                                backgroundMusicAutoStart,
                                math.max(0f, runtimeConfig.MasterVolume) * math.max(0f, runtimeConfig.BackgroundMusicVolume));
        }

        if (requests.Length <= 0)
            return;

        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates = entityManager.GetBuffer<GameAudioRateLimitStateElement>(audioEntity);
#if UNITY_WEBGL && !UNITY_EDITOR
        int webGlOneShotStarts = 0;
#endif

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

#if UNITY_WEBGL && !UNITY_EDITOR
            if (webGlOneShotStarts >= WebGlMaxOneShotStartsPerFrame && !IsWebGlAudioBudgetExempt(request.EventId))
                continue;
#endif

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
#if UNITY_WEBGL && !UNITY_EDITOR
            webGlOneShotStarts++;
#endif
        }

        requests.Clear();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves whether background music should be stopped because the scene manager is entering or already in main menu.
    /// </summary>
    /// <param name="sceneConfig">Runtime scene manager configuration.</param>
    /// <param name="transitionState">Current scene transition state.</param>
    /// <returns>True when gameplay background music must not run.</returns>
    private static bool ShouldStopBackgroundMusicForMainMenu(in GameSceneManagerConfig sceneConfig,
                                                             in GameSceneTransitionState transitionState)
    {
        if (sceneConfig.MainMenuSceneId.Length <= 0)
            return false;

        if (transitionState.IsTransitioning != 0 &&
            transitionState.TargetSceneId.Equals(sceneConfig.MainMenuSceneId))
            return true;

        return transitionState.ActiveSceneId.Equals(sceneConfig.MainMenuSceneId);
    }

    /// <summary>
    /// Forwards the baked background music config to the FMOD runtime bridge.
    /// </summary>
    /// <param name="runtimeConfig">Current baked audio singleton config.</param>
    /// <param name="enabled">True when background music should be active.</param>
    /// <param name="autoStart">True when music should start automatically.</param>
    /// <param name="volume">Final music volume after master and routing multipliers.</param>
    private static void SyncBackgroundMusic(in GameAudioRuntimeConfig runtimeConfig,
                                            bool enabled,
                                            bool autoStart,
                                            float volume)
    {
        GameAudioFmodRuntimeUtility.SyncBackgroundMusic(ResolveManagedBackgroundMusicPath(in runtimeConfig),
                                                        ResolveManagedBackgroundMusicBankName(in runtimeConfig),
                                                        enabled,
                                                        autoStart,
                                                        volume,
                                                        runtimeConfig.BackgroundMusicRestartWhenPathChanges != 0,
                                                        runtimeConfig.BackgroundMusicStopWhenDisabled != 0,
                                                        runtimeConfig.LogMissingEventPaths != 0);
    }

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
    /// Resolves the baked music path to a managed string only when the fixed string changes.
    /// </summary>
    /// <param name="runtimeConfig">Current baked audio singleton config.</param>
    /// <returns>Cached managed music path.</returns>
    private static string ResolveManagedBackgroundMusicPath(in GameAudioRuntimeConfig runtimeConfig)
    {
        FixedString512Bytes eventPath = runtimeConfig.BackgroundMusicEventPath;

        if (!cachedBackgroundMusicPathValid || !cachedBackgroundMusicFixedPath.Equals(eventPath))
        {
            cachedBackgroundMusicFixedPath = eventPath;
            cachedBackgroundMusicManagedPath = eventPath.ToString();
            cachedBackgroundMusicPathValid = true;
        }

        return cachedBackgroundMusicManagedPath;
    }

    /// <summary>
    /// Resolves the baked music bank name to a managed string only when the fixed string changes.
    /// </summary>
    /// <param name="runtimeConfig">Current baked audio singleton config.</param>
    /// <returns>Cached managed music bank name.</returns>
    private static string ResolveManagedBackgroundMusicBankName(in GameAudioRuntimeConfig runtimeConfig)
    {
        FixedString64Bytes bankName = runtimeConfig.BackgroundMusicBankName;

        if (!cachedBackgroundMusicBankValid || !cachedBackgroundMusicFixedBank.Equals(bankName))
        {
            cachedBackgroundMusicFixedBank = bankName;
            cachedBackgroundMusicManagedBank = bankName.ToString();
            cachedBackgroundMusicBankValid = true;
        }

        return cachedBackgroundMusicManagedBank;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Resolves a restart from the transition state. Equal non-empty source and target IDs only occur for an
    /// accepted restart request because ordinary same-scene load requests are rejected by the scene manager.
    /// </summary>
    /// <param name="transitionState">Current scene transition lifecycle state.</param>
    /// <returns>True while the active gameplay scene is being restarted.</returns>
    private static bool IsRestartTransitionActive(in GameSceneTransitionState transitionState)
    {
        if (transitionState.IsTransitioning == 0)
            return false;

        if (transitionState.SourceSceneId.Length <= 0 || transitionState.TargetSceneId.Length <= 0)
            return false;

        return transitionState.SourceSceneId.Equals(transitionState.TargetSceneId);
    }

    /// <summary>
    /// Prepares one FMOD event at a paced interval after startup so WebGL avoids both first-use hitches and
    /// a burst of sample decoding during the first rendered frames.
    /// </summary>
    /// <param name="bindings">Baked audio event bindings.</param>
    /// <param name="runtimeConfig">Current baked audio singleton config.</param>
    /// <param name="elapsedTime">Current world elapsed time.</param>
    private static void WarmUpWebGlAudioEvents(DynamicBuffer<GameAudioEventBindingElement> bindings,
                                               in GameAudioRuntimeConfig runtimeConfig,
                                               float elapsedTime)
    {
        if (elapsedTime < webGlNextWarmupTime)
            return;

        if (bindings.Length <= 0)
            return;

        bool logMissingEventPaths = runtimeConfig.LogMissingEventPaths != 0;
        int visitedCount = 0;

        while (visitedCount < bindings.Length)
        {
            int bindingIndex = webGlWarmupCursor % bindings.Length;
            webGlWarmupCursor = (webGlWarmupCursor + 1) % bindings.Length;
            visitedCount++;

            GameAudioEventBindingElement binding = bindings[bindingIndex];

            if (binding.EventPath.Length <= 0)
                continue;

            GameAudioFmodRuntimeUtility.PrepareEventPath(ResolveManagedEventPath(in binding), logMissingEventPaths);
            webGlNextWarmupTime = elapsedTime + WebGlWarmupIntervalSeconds;
            return;
        }

        webGlNextWarmupTime = elapsedTime + WebGlWarmupIntervalSeconds;
    }

    /// <summary>
    /// Holds music start briefly after main-menu exits or gameplay-world recreation to avoid WebAudio underruns
    /// while Unity is still settling a WebGL scene load.
    /// </summary>
    /// <param name="shouldStopBackgroundMusicForMainMenu">True while gameplay music must be silent.</param>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    private static void UpdateWebGlBackgroundMusicDelay(bool shouldStopBackgroundMusicForMainMenu, float elapsedTime)
    {
        if (shouldStopBackgroundMusicForMainMenu)
        {
            webGlBackgroundMusicDelayPending = true;
            webGlBackgroundMusicReadyTime = elapsedTime + WebGlBackgroundMusicStartDelaySeconds;
            return;
        }

        if (!webGlBackgroundMusicDelayPending)
            return;

        webGlBackgroundMusicReadyTime = elapsedTime + WebGlBackgroundMusicStartDelaySeconds;
        webGlBackgroundMusicDelayPending = false;
    }

    /// <summary>
    /// Checks whether WebGL background music may start after the scene-entry grace period.
    /// </summary>
    /// <param name="elapsedTime">Current world elapsed time in seconds.</param>
    /// <returns>True when music auto-start can be forwarded to FMOD.</returns>
    private static bool IsWebGlBackgroundMusicReady(float elapsedTime)
    {
        return elapsedTime >= webGlBackgroundMusicReadyTime;
    }

    /// <summary>
    /// Lets important one-shot events bypass the WebGL per-frame SFX budget.
    /// </summary>
    /// <param name="eventId">Requested gameplay audio event.</param>
    /// <returns>True when this event should play even after the frame budget is full.</returns>
    private static bool IsWebGlAudioBudgetExempt(GameAudioEventId eventId)
    {
        switch (eventId)
        {
            case GameAudioEventId.PlayerSpawn:
            case GameAudioEventId.PlayerDeath:
            case GameAudioEventId.PlayerDeathJingle:
            case GameAudioEventId.PlayerVictory:
            case GameAudioEventId.PlayerLevelUp:
            case GameAudioEventId.PlayerLevelUpMilestone:
                return true;
            default:
                return false;
        }
    }
#endif

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
