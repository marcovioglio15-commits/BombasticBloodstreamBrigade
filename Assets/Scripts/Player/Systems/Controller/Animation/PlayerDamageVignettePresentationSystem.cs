using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Detects player health and shield drops every presentation frame and advances the per-channel fade-in / fade-out state machine that drives the full-screen damage vignette.
/// Selects the Health channel when damage reaches health (regardless of whether the shield also took damage in the same frame) and the Shield channel when the hit was fully absorbed by the shield.
/// Stores the resulting alpha into <see cref="PlayerDamageVignetteState.ActiveAlpha"/> so the scene UI binder can apply it without polling damage-call sites.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerDamageFlashPresentationSystem))]
public partial struct PlayerDamageVignettePresentationSystem : ISystem
{
    #region Constants
    private const float DamageDetectionEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerDamageVignetteConfig>();
        state.RequireForUpdate<PlayerDamageVignetteState>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerDamageVignetteConfig> vignetteConfig,
                  RefRW<PlayerDamageVignetteState> vignetteState,
                  RefRO<PlayerHealth> playerHealth,
                  RefRO<PlayerShield> playerShield)
                 in SystemAPI.Query<RefRO<PlayerDamageVignetteConfig>,
                                    RefRW<PlayerDamageVignetteState>,
                                    RefRO<PlayerHealth>,
                                    RefRO<PlayerShield>>()
                             .WithAll<PlayerControllerConfig>())
        {
            PlayerDamageVignetteState nextState = vignetteState.ValueRO;
            float currentHealth = playerHealth.ValueRO.Current;
            float currentShield = playerShield.ValueRO.Current;

            // Seed the previous-value snapshot on the very first observation so spawn-time deltas never spawn a phantom pulse.
            if (nextState.Initialized == 0)
            {
                nextState.PreviousHealth = currentHealth;
                nextState.PreviousShield = currentShield;
                nextState.Initialized = 1;
            }

            float healthDelta = nextState.PreviousHealth - currentHealth;
            float shieldDelta = nextState.PreviousShield - currentShield;

            // Trigger health channel as soon as actual health was lost; the shield channel reacts only when no health damage happened on the same frame.
            PlayerDamageVignetteChannel pulseChannel = PlayerDamageVignetteChannel.None;

            if (healthDelta > DamageDetectionEpsilon)
                pulseChannel = PlayerDamageVignetteChannel.Health;
            else if (shieldDelta > DamageDetectionEpsilon)
                pulseChannel = PlayerDamageVignetteChannel.Shield;

            if (pulseChannel != PlayerDamageVignetteChannel.None)
                StartPulse(ref nextState, pulseChannel);

            // Advance whichever channel pulse is currently in flight. Idle pulses keep alpha at zero with no work.
            AdvancePulse(ref nextState, in vignetteConfig.ValueRO, deltaTime);

            nextState.PreviousHealth = currentHealth;
            nextState.PreviousShield = currentShield;
            vignetteState.ValueRW = nextState;
        }
    }
    #endregion

    #region Pulse State Machine
    /// <summary>
    /// Restarts the per-channel fade-in / fade-out state machine. Overrides any in-flight pulse so a new hit always feels punchy.
    /// </summary>
    /// <param name="state">Mutable runtime state being updated this frame.</param>
    /// <param name="channel">Newly selected damage channel.</param>
    private static void StartPulse(ref PlayerDamageVignetteState state, PlayerDamageVignetteChannel channel)
    {
        state.ActiveChannel = channel;
        state.ActivePhase = PlayerDamageVignettePhase.FadeIn;
        state.ActiveElapsedSeconds = 0f;

        // Bumping the pulse ID lets external consumers detect a brand-new pulse even if the alpha was already non-zero from a previous one.
        state.ActiveTriggerPulseId = (byte)((state.ActiveTriggerPulseId + 1) & 0xFF);
    }

    /// <summary>
    /// Advances the active pulse along its fade curve and writes the resolved alpha back into the runtime state.
    /// </summary>
    /// <param name="state">Mutable runtime state being updated this frame.</param>
    /// <param name="config">Immutable per-channel tuning baked from the visual preset.</param>
    /// <param name="deltaTime">Current frame delta time in seconds.</param>
    private static void AdvancePulse(ref PlayerDamageVignetteState state,
                                     in PlayerDamageVignetteConfig config,
                                     float deltaTime)
    {
        if (state.ActiveChannel == PlayerDamageVignetteChannel.None ||
            state.ActivePhase == PlayerDamageVignettePhase.Idle)
        {
            state.ActiveAlpha = 0f;
            return;
        }

        ResolveChannelTuning(in config,
                              state.ActiveChannel,
                              out float maxAlpha,
                              out float fadeInSeconds,
                              out float fadeOutSeconds);

        // A misauthored channel with all-zero values must not freeze the state machine in FadeIn forever, so collapse straight to Idle.
        if (maxAlpha <= 0f)
        {
            state.ActiveChannel = PlayerDamageVignetteChannel.None;
            state.ActivePhase = PlayerDamageVignettePhase.Idle;
            state.ActiveElapsedSeconds = 0f;
            state.ActiveAlpha = 0f;
            return;
        }

        state.ActiveElapsedSeconds += math.max(0f, deltaTime);

        switch (state.ActivePhase)
        {
            case PlayerDamageVignettePhase.FadeIn:
            {
                if (fadeInSeconds <= 0f)
                {
                    state.ActiveAlpha = maxAlpha;
                    state.ActivePhase = PlayerDamageVignettePhase.FadeOut;
                    state.ActiveElapsedSeconds = 0f;
                    return;
                }

                float fadeInNormalized = math.saturate(state.ActiveElapsedSeconds / fadeInSeconds);
                state.ActiveAlpha = fadeInNormalized * maxAlpha;

                if (fadeInNormalized < 1f)
                    return;

                state.ActivePhase = PlayerDamageVignettePhase.FadeOut;
                state.ActiveElapsedSeconds = 0f;
                return;
            }
            case PlayerDamageVignettePhase.FadeOut:
            {
                if (fadeOutSeconds <= 0f)
                {
                    state.ActiveChannel = PlayerDamageVignetteChannel.None;
                    state.ActivePhase = PlayerDamageVignettePhase.Idle;
                    state.ActiveElapsedSeconds = 0f;
                    state.ActiveAlpha = 0f;
                    return;
                }

                float fadeOutNormalized = math.saturate(state.ActiveElapsedSeconds / fadeOutSeconds);
                state.ActiveAlpha = (1f - fadeOutNormalized) * maxAlpha;

                if (fadeOutNormalized < 1f)
                    return;

                state.ActiveChannel = PlayerDamageVignetteChannel.None;
                state.ActivePhase = PlayerDamageVignettePhase.Idle;
                state.ActiveElapsedSeconds = 0f;
                state.ActiveAlpha = 0f;
                return;
            }
        }
    }

    /// <summary>
    /// Extracts per-channel tuning from the baked config so the state machine reads exactly one numeric block per advance call.
    /// </summary>
    /// <param name="config">Immutable per-channel tuning baked from the visual preset.</param>
    /// <param name="channel">Currently active damage channel.</param>
    /// <param name="maxAlpha">Peak alpha reached at the end of the fade-in.</param>
    /// <param name="fadeInSeconds">Seconds spent ramping from zero to peak alpha.</param>
    /// <param name="fadeOutSeconds">Seconds spent ramping from peak alpha back to zero.</param>
    private static void ResolveChannelTuning(in PlayerDamageVignetteConfig config,
                                              PlayerDamageVignetteChannel channel,
                                              out float maxAlpha,
                                              out float fadeInSeconds,
                                              out float fadeOutSeconds)
    {
        switch (channel)
        {
            case PlayerDamageVignetteChannel.Shield:
                maxAlpha = config.ShieldMaxAlpha;
                fadeInSeconds = config.ShieldFadeInSeconds;
                fadeOutSeconds = config.ShieldFadeOutSeconds;
                return;
            case PlayerDamageVignetteChannel.Health:
                maxAlpha = config.HealthMaxAlpha;
                fadeInSeconds = config.HealthFadeInSeconds;
                fadeOutSeconds = config.HealthFadeOutSeconds;
                return;
            default:
                maxAlpha = 0f;
                fadeInSeconds = 0f;
                fadeOutSeconds = 0f;
                return;
        }
    }
    #endregion

    #endregion
}
