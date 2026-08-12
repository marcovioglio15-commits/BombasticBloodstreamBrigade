using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides allocation-free phase, scroll, and authored image-pair operations for the Synchro Meter.
/// </summary>
public static class HUDSynchroMeterWaveUtility
{
    #region Constants
    private const float PrecisionEpsilon = 0.001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves rank-based wave separation so the maximum authored rank reaches the configured final overlap.
    /// </summary>
    /// <param name="currentRankIndex">Current zero-based rank index, or a negative value before the first rank.</param>
    /// <param name="rankCount">Total number of runtime rank thresholds.</param>
    /// <param name="lowestRankOffset">Normalized wave separation requested at the first rank.</param>
    /// <param name="highestRankOffset">Normalized wave separation requested at the maximum rank.</param>
    /// <param name="responseExponent">Exponent shaping convergence across intermediate ranks.</param>
    /// <returns>Safe normalized wave separation for the current rank.</returns>
    public static float ResolveRankPhaseOffset(int currentRankIndex,
                                               int rankCount,
                                               float lowestRankOffset,
                                               float highestRankOffset,
                                               float responseExponent)
    {
        float safeLowestOffset = SanitizeNormalizedPhase(lowestRankOffset, 0.25f);
        float safeHighestOffset = SanitizeNormalizedPhase(highestRankOffset, 0f);

        if (currentRankIndex < 0)
            return safeLowestOffset;

        if (rankCount <= 1)
            return safeHighestOffset;

        float normalizedRank = Mathf.Clamp01((float)currentRankIndex / (rankCount - 1));
        float safeExponent = IsFinite(responseExponent) && responseExponent > PrecisionEpsilon
            ? responseExponent
            : 1f;
        float response = Mathf.Pow(normalizedRank, safeExponent);
        return Mathf.Lerp(safeLowestOffset, safeHighestOffset, response);
    }

    /// <summary>
    /// Resolves continuous or stepped wave convergence across a configured Single Rank Progression window.
    /// </summary>
    /// <param name="progressNormalized">Authoritative normalized progress across the single rank.</param>
    /// <param name="initialOffset">Normalized separation before convergence starts.</param>
    /// <param name="finalOffset">Normalized separation after convergence ends.</param>
    /// <param name="startProgressPercent">Progress percentage at which convergence begins.</param>
    /// <param name="endProgressPercent">Progress percentage at which convergence ends.</param>
    /// <param name="mode">Continuous or stepped convergence mode.</param>
    /// <param name="stepCount">Number of equal convergence intervals used by Steps mode.</param>
    /// <returns>Safe normalized wave separation for current single-rank progress.</returns>
    public static float ResolveSingleRankPhaseOffset(float progressNormalized,
                                                     float initialOffset,
                                                     float finalOffset,
                                                     float startProgressPercent,
                                                     float endProgressPercent,
                                                     GameHudSynchroSingleRankConvergenceMode mode,
                                                     int stepCount)
    {
        float safeProgress = SanitizeNormalizedPhase(progressNormalized, 0f);
        float safeStart = SanitizeNormalizedPhase(startProgressPercent * 0.01f, 0f);
        float safeEnd = SanitizeNormalizedPhase(endProgressPercent * 0.01f, 1f);
        float windowProgress = safeEnd > safeStart + PrecisionEpsilon
            ? Mathf.InverseLerp(safeStart, safeEnd, safeProgress)
            : safeProgress;

        if (mode == GameHudSynchroSingleRankConvergenceMode.Steps)
        {
            int safeStepCount = Mathf.Max(1, stepCount);
            windowProgress = Mathf.Floor(windowProgress * safeStepCount) / safeStepCount;

            if (safeProgress >= safeEnd)
                windowProgress = 1f;
        }

        return Mathf.Lerp(SanitizeNormalizedPhase(initialOffset, 0.25f),
                          SanitizeNormalizedPhase(finalOffset, 0f),
                          windowProgress);
    }

    /// <summary>
    /// Resolves a shared wave scroll rate that accelerates linearly across Single Rank Progression.
    /// </summary>
    /// <param name="baseCyclesPerSecond">Wave tile cycles per second used at zero progress.</param>
    /// <param name="maximumCyclesPerSecond">Wave tile cycles per second used at full progress.</param>
    /// <param name="progressNormalized">Authoritative normalized progress across the single rank.</param>
    /// <param name="accelerateWithProgress">Whether progression controls the scroll rate.</param>
    /// <returns>Finite non-negative wave tile cycles per second.</returns>
    public static float ResolveSingleRankScrollCycles(float baseCyclesPerSecond,
                                                      float maximumCyclesPerSecond,
                                                      float progressNormalized,
                                                      bool accelerateWithProgress)
    {
        float safeBase = SanitizeNonNegative(baseCyclesPerSecond, 0.12f);

        if (!accelerateWithProgress)
            return safeBase;

        return Mathf.Lerp(safeBase,
                          SanitizeNonNegative(maximumCyclesPerSecond, safeBase),
                          SanitizeNormalizedPhase(progressNormalized, 0f));
    }

    /// <summary>
    /// Advances one phase value toward its target using a duration expressed for one normalized tile cycle.
    /// </summary>
    /// <param name="currentPhase">Current normalized relative wave phase.</param>
    /// <param name="targetPhase">Target normalized relative wave phase.</param>
    /// <param name="transitionDuration">Seconds required to traverse one complete normalized phase unit.</param>
    /// <param name="deltaTime">Frame time used for convergence.</param>
    /// <returns>Updated normalized phase value.</returns>
    public static float AdvancePhase(float currentPhase,
                                     float targetPhase,
                                     float transitionDuration,
                                     float deltaTime)
    {
        float safeTarget = SanitizeNormalizedPhase(targetPhase, 0f);
        float safeCurrent = SanitizeNormalizedPhase(currentPhase, safeTarget);
        float safeDuration = SanitizeNonNegative(transitionDuration, 0f);

        if (safeDuration <= PrecisionEpsilon)
            return safeTarget;

        float phaseStep = SanitizeNonNegative(deltaTime, 0f) / safeDuration;
        return Mathf.MoveTowards(safeCurrent, safeTarget, phaseStep);
    }

    /// <summary>
    /// Advances the shared scroll phase and wraps it to a bounded normalized tile cycle.
    /// </summary>
    /// <param name="currentScroll">Current normalized scroll phase.</param>
    /// <param name="cyclesPerSecond">Complete image-tile cycles advanced per second.</param>
    /// <param name="deltaTime">Frame time used for scrolling.</param>
    /// <returns>Wrapped normalized scroll phase.</returns>
    public static float AdvanceScroll(float currentScroll, float cyclesPerSecond, float deltaTime)
    {
        float safeCurrentScroll = IsFinite(currentScroll) ? currentScroll : 0f;
        float safeCyclesPerSecond = SanitizeNonNegative(cyclesPerSecond, 0f);
        float safeDeltaTime = SanitizeNonNegative(deltaTime, 0f);
        return Mathf.Repeat(safeCurrentScroll + (safeCyclesPerSecond * safeDeltaTime), 1f);
    }

    /// <summary>
    /// Positions two authored wave images edge-to-edge and wraps their shared horizontal phase without allocations.
    /// </summary>
    /// <param name="leadingImage">First authored image in the seamless pair.</param>
    /// <param name="trailingImage">Second authored image in the seamless pair.</param>
    /// <param name="normalizedPhase">Shared normalized tile phase including scroll and optional rank offset.</param>
    public static void ApplySeamlessPair(Image leadingImage,
                                         Image trailingImage,
                                         float normalizedPhase)
    {
        if (leadingImage == null || trailingImage == null)
            return;

        RectTransform leadingTransform = leadingImage.rectTransform;
        RectTransform trailingTransform = trailingImage.rectTransform;
        float tileWidth = Mathf.Abs(leadingTransform.rect.width);

        if (tileWidth <= PrecisionEpsilon)
            tileWidth = Mathf.Abs(leadingTransform.sizeDelta.x);

        if (tileWidth <= PrecisionEpsilon)
            return;

        float leadingPositionX = -Mathf.Repeat(IsFinite(normalizedPhase) ? normalizedPhase : 0f, 1f) * tileWidth;
        Vector2 leadingPosition = leadingTransform.anchoredPosition;
        Vector2 trailingPosition = trailingTransform.anchoredPosition;
        leadingPosition.x = leadingPositionX;
        trailingPosition.x = leadingPositionX + tileWidth;
        leadingTransform.anchoredPosition = leadingPosition;
        trailingTransform.anchoredPosition = trailingPosition;
    }

    /// <summary>
    /// Converts a phase to the finite normalized 0..1 range or returns the supplied fallback.
    /// </summary>
    /// <param name="value">Authored or runtime phase value.</param>
    /// <param name="fallback">Fallback used when the source is not finite.</param>
    /// <returns>Finite phase clamped to the normalized range.</returns>
    public static float SanitizeNormalizedPhase(float value, float fallback)
    {
        return Mathf.Clamp01(IsFinite(value) ? value : fallback);
    }

    /// <summary>
    /// Converts a scalar to a finite non-negative value or returns the supplied fallback.
    /// </summary>
    /// <param name="value">Authored or runtime scalar.</param>
    /// <param name="fallback">Fallback used when the source is not finite.</param>
    /// <returns>Finite scalar no lower than zero.</returns>
    public static float SanitizeNonNegative(float value, float fallback)
    {
        return Mathf.Max(0f, IsFinite(value) ? value : fallback);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether a floating-point value can safely participate in UI animation math.
    /// </summary>
    /// <param name="value">Value being inspected.</param>
    /// <returns>True when the value is neither NaN nor infinite.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
