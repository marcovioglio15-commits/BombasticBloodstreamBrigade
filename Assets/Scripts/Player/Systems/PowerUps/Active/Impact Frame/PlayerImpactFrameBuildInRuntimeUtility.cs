using Unity.Mathematics;

/// <summary>
/// Owns charge-driven Impact Frame build-in requests and their rapid smooth release when no producer refreshes them.
/// </summary>
public static class PlayerImpactFrameBuildInRuntimeUtility
{
    #region Constants
    private const float ComparisonEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Requests one build-in profile for the current frame. Stronger charge progress wins when both active slots request
    /// a profile during the same update.
    /// </summary>
    /// <param name="state">Mutable build-in state.</param>
    /// <param name="config">Build-in config authored on the paired Impact Frame module.</param>
    /// <param name="normalizedCharge">Current charge divided by maximum charge.</param>
    public static void Request(ref PlayerImpactFrameBuildInState state,
                               in ImpactFrameBuildInConfig config,
                               float normalizedCharge)
    {
        if (config.Enabled == 0)
            return;

        float requestedBlend = EvaluateEasing(config.EasingMode, math.saturate(normalizedCharge));

        if (state.RequestedThisFrame != 0 && requestedBlend <= state.RequestedBlend)
            return;

        state.IsActive = 1;
        state.RequestedThisFrame = 1;
        state.IsReleasing = 0;
        state.RequestedBlend = requestedBlend;
        state.CurrentBlend = requestedBlend;
        state.ReleaseStartBlend = requestedBlend;
        state.ReleaseElapsedUnscaledSeconds = 0f;
        state.ReleaseUnscaledSeconds = math.max(0f, config.ReleaseUnscaledSeconds);
        state.EasingMode = config.EasingMode;
        state.Effect = config.Effect;
    }

    /// <summary>
    /// Advances the build-in release when no charge producer refreshed it during the current frame.
    /// </summary>
    /// <param name="state">Mutable build-in state.</param>
    /// <param name="unscaledDeltaTime">Current unscaled frame delta.</param>
    /// <returns>True while the build-in effect remains visible.</returns>
    public static bool Tick(ref PlayerImpactFrameBuildInState state, float unscaledDeltaTime)
    {
        if (state.IsActive == 0)
            return false;

        if (state.RequestedThisFrame != 0)
        {
            state.RequestedThisFrame = 0;
            return state.CurrentBlend > ComparisonEpsilon;
        }

        if (state.IsReleasing == 0)
        {
            state.IsReleasing = 1;
            state.ReleaseStartBlend = state.CurrentBlend;
            state.ReleaseElapsedUnscaledSeconds = 0f;
        }

        if (state.ReleaseUnscaledSeconds <= ComparisonEpsilon)
        {
            state = default;
            return false;
        }

        state.ReleaseElapsedUnscaledSeconds += math.max(0f, unscaledDeltaTime);
        float normalizedRelease = math.saturate(state.ReleaseElapsedUnscaledSeconds / state.ReleaseUnscaledSeconds);
        state.CurrentBlend = state.ReleaseStartBlend * (1f - EvaluateEasing(state.EasingMode, normalizedRelease));

        if (normalizedRelease < 1f)
            return state.CurrentBlend > ComparisonEpsilon;

        state = default;
        return false;
    }

    /// <summary>
    /// Evaluates the shared Impact Frame easing modes for charge growth and release.
    /// </summary>
    /// <param name="easingMode">Selected easing curve.</param>
    /// <param name="normalizedProgress">Normalized input in the 0-1 range.</param>
    /// <returns>Eased progress in the 0-1 range.</returns>
    public static float EvaluateEasing(ImpactFrameEasingMode easingMode, float normalizedProgress)
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
