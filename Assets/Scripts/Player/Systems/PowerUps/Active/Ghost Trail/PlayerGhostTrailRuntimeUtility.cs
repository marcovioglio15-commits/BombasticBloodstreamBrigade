using Unity.Mathematics;

/// <summary>
/// Centralizes Ghost Trail activation, toggle matching and unscaled timeline transitions.
/// </summary>
public static class PlayerGhostTrailRuntimeUtility
{
    #region Constants
    private const byte PhaseIdle = 0;
    private const byte PhaseEaseIn = 1;
    private const byte PhaseHold = 2;
    private const byte PhaseEaseOut = 3;
    private const float ComparisonEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether one Ghost Trail config can emit visible snapshots.
    /// </summary>
    /// <param name="config">Runtime Ghost Trail configuration.</param>
    /// <param name="toggleActivation">True when activation belongs to a toggleable Resource Gate.</param>
    /// <param name="matchedActiveDurationSeconds">Host active's own finite duration, used when a non-toggleable active matches the activation duration.</param>
    /// <returns>True when duration and snapshot settings can produce a trail.</returns>
    public static bool CanActivate(in GhostTrailPowerUpConfig config,
                                   bool toggleActivation,
                                   float matchedActiveDurationSeconds = 0f)
    {
        return config.SnapshotLifetimeSeconds > ComparisonEpsilon &&
               config.MaximumActiveSnapshots > 0 &&
               (config.DurationSeconds > ComparisonEpsilon ||
                (config.MatchToggleActivationDuration != 0 &&
                 (toggleActivation || matchedActiveDurationSeconds > ComparisonEpsilon)));
    }

    /// <summary>
    /// Starts or refreshes Ghost Trail emission after a successful power-up activation.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    /// <param name="config">Resolved slot-level Ghost Trail configuration.</param>
    /// <param name="toggleActivation">True when the owning active power-up remains toggle-active.</param>
    /// <param name="toggleSlotIndex">Owning toggle slot index, or byte.MaxValue for finite activations.</param>
    /// <param name="matchedActiveDurationSeconds">Host active's own finite duration, used when a non-toggleable active (e.g. Dash) matches the activation duration.</param>
    public static void Activate(ref PlayerGhostTrailState state,
                                in GhostTrailPowerUpConfig config,
                                bool toggleActivation,
                                byte toggleSlotIndex = byte.MaxValue,
                                float matchedActiveDurationSeconds = 0f)
    {
        if (!CanActivate(in config, toggleActivation, matchedActiveDurationSeconds))
            return;

        // Non-toggleable actives (e.g. Dash) have no toggle on/off lifecycle, so when the trail is authored to match
        // the activation duration it inherits the host active's own finite duration instead of the unbounded toggle hold.
        bool matchHostActiveDuration = !toggleActivation &&
                                       config.MatchToggleActivationDuration != 0 &&
                                       config.DurationSeconds <= ComparisonEpsilon &&
                                       matchedActiveDurationSeconds > ComparisonEpsilon;
        float resolvedDurationSeconds = matchHostActiveDuration
            ? matchedActiveDurationSeconds
            : math.max(0f, config.DurationSeconds);

        state.IsActive = 1;
        state.Phase = config.EaseInUnscaledSeconds > ComparisonEpsilon ? PhaseEaseIn : PhaseHold;
        state.MatchToggleActivationDuration = toggleActivation && config.MatchToggleActivationDuration != 0 ? (byte)1 : (byte)0;
        state.MatchedToggleSlotIndex = state.MatchToggleActivationDuration != 0 ? toggleSlotIndex : byte.MaxValue;
        state.RemainingDurationSeconds = state.MatchToggleActivationDuration != 0 ? 0f : resolvedDurationSeconds;
        state.EaseInUnscaledSeconds = math.max(0f, config.EaseInUnscaledSeconds);
        state.EaseOutUnscaledSeconds = math.max(0f, config.EaseOutUnscaledSeconds);
        state.PhaseElapsedUnscaledSeconds = 0f;
        state.EffectElapsedUnscaledSeconds = 0f;
        state.TotalDurationUnscaledSeconds = math.max(ComparisonEpsilon,
                                                      resolvedDurationSeconds +
                                                      state.EaseInUnscaledSeconds +
                                                      state.EaseOutUnscaledSeconds);
        state.CurrentBlend = state.Phase == PhaseEaseIn ? 0f : 1f;
        state.Config = config;
    }

    /// <summary>
    /// Stops matched-toggle emission and preserves the configured ease-out tail.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    /// <param name="toggleSlotIndex">Owning toggle slot index requesting deactivation.</param>
    public static void StopMatchedToggle(ref PlayerGhostTrailState state, byte toggleSlotIndex)
    {
        if (state.IsActive == 0 || state.MatchToggleActivationDuration == 0)
            return;

        if (state.MatchedToggleSlotIndex != toggleSlotIndex)
            return;

        BeginEaseOut(ref state);
    }

    /// <summary>
    /// Clears all Ghost Trail state immediately.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    public static void Clear(ref PlayerGhostTrailState state)
    {
        state = default;
    }

    /// <summary>
    /// Advances one active Ghost Trail timeline with unscaled time.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    /// <param name="unscaledDeltaTime">Current unscaled frame delta.</param>
    /// <returns>True while emission or its ease-out remains active.</returns>
    public static bool Tick(ref PlayerGhostTrailState state, float unscaledDeltaTime)
    {
        if (state.IsActive == 0)
            return false;

        float safeDeltaTime = math.max(0f, unscaledDeltaTime);
        state.EffectElapsedUnscaledSeconds += safeDeltaTime;
        state.PhaseElapsedUnscaledSeconds += safeDeltaTime;

        switch (state.Phase)
        {
            case PhaseEaseIn:
                TickEaseIn(ref state);
                break;
            case PhaseHold:
                TickHold(ref state, safeDeltaTime);
                break;
            case PhaseEaseOut:
                TickEaseOut(ref state);
                break;
            default:
                Clear(ref state);
                break;
        }

        return state.IsActive != 0;
    }
    #endregion

    #region Timeline
    /// <summary>
    /// Advances entry blending into full trail intensity.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    private static void TickEaseIn(ref PlayerGhostTrailState state)
    {
        if (state.EaseInUnscaledSeconds <= ComparisonEpsilon ||
            state.PhaseElapsedUnscaledSeconds >= state.EaseInUnscaledSeconds)
        {
            state.Phase = PhaseHold;
            state.PhaseElapsedUnscaledSeconds = 0f;
            state.CurrentBlend = 1f;
            return;
        }

        state.CurrentBlend = EvaluateEasing(state.Config.EasingMode,
                                            state.PhaseElapsedUnscaledSeconds / state.EaseInUnscaledSeconds);
    }

    /// <summary>
    /// Advances finite duration while matched toggles remain active indefinitely.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    /// <param name="safeDeltaTime">Non-negative unscaled delta.</param>
    private static void TickHold(ref PlayerGhostTrailState state, float safeDeltaTime)
    {
        state.CurrentBlend = 1f;

        if (state.MatchToggleActivationDuration != 0)
            return;

        state.RemainingDurationSeconds = math.max(0f, state.RemainingDurationSeconds - safeDeltaTime);

        if (state.RemainingDurationSeconds <= ComparisonEpsilon)
            BeginEaseOut(ref state);
    }

    /// <summary>
    /// Advances recovery blending and clears state at zero intensity.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    private static void TickEaseOut(ref PlayerGhostTrailState state)
    {
        if (state.EaseOutUnscaledSeconds <= ComparisonEpsilon ||
            state.PhaseElapsedUnscaledSeconds >= state.EaseOutUnscaledSeconds)
        {
            Clear(ref state);
            return;
        }

        state.CurrentBlend = 1f - EvaluateEasing(state.Config.EasingMode,
                                                 state.PhaseElapsedUnscaledSeconds / state.EaseOutUnscaledSeconds);
    }

    /// <summary>
    /// Enters the recovery phase or clears immediately when no recovery is authored.
    /// </summary>
    /// <param name="state">Mutable Ghost Trail state.</param>
    private static void BeginEaseOut(ref PlayerGhostTrailState state)
    {
        state.PhaseElapsedUnscaledSeconds = 0f;
        state.MatchToggleActivationDuration = 0;

        if (state.EaseOutUnscaledSeconds <= ComparisonEpsilon)
        {
            Clear(ref state);
            return;
        }

        state.Phase = PhaseEaseOut;
        state.CurrentBlend = 1f;
    }

    /// <summary>
    /// Evaluates the shared normalized easing modes used by Ghost Trail and Impact Frame.
    /// </summary>
    /// <param name="easingMode">Selected easing mode.</param>
    /// <param name="normalizedProgress">Normalized input progress.</param>
    /// <returns>Eased normalized progress.</returns>
    private static float EvaluateEasing(ImpactFrameEasingMode easingMode, float normalizedProgress)
    {
        float t = math.saturate(normalizedProgress);

        switch (easingMode)
        {
            case ImpactFrameEasingMode.EaseInOutSine:
                return -(math.cos(math.PI * t) - 1f) * 0.5f;
            case ImpactFrameEasingMode.EaseOutCubic:
                return 1f - math.pow(1f - t, 3f);
            case ImpactFrameEasingMode.EaseInExpo:
                return t <= ComparisonEpsilon ? 0f : math.pow(2f, 10f * t - 10f);
            case ImpactFrameEasingMode.EaseOutExpo:
                return t >= 1f - ComparisonEpsilon ? 1f : 1f - math.pow(2f, -10f * t);
            default:
                return t;
        }
    }
    #endregion

    #endregion
}
