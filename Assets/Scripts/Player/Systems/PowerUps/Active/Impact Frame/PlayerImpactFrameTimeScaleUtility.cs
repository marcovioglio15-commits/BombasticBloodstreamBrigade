using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Owns the global Time.timeScale writes requested by active Impact Frame effects.
/// </summary>
internal static class PlayerImpactFrameTimeScaleUtility
{
    #region Constants
    private const float ComparisonEpsilon = 0.0005f;
    #endregion

    #region Fields
    private static bool ownsTimeScale;
    private static float previousTimeScale = 1f;
    private static float lastAppliedTimeScale = 1f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the strongest active Impact Frame slowdown to Unity's global time scale.
    /// </summary>
    /// <param name="slowPercent">Resolved slowdown percentage in the 0-100 range.</param>
    public static void ApplySlowPercent(float slowPercent)
    {
        float clampedSlowPercent = math.clamp(slowPercent, 0f, 100f);

        if (!ownsTimeScale)
            CaptureCurrentTimeScale();

        float targetTimeScale = previousTimeScale * math.saturate(1f - clampedSlowPercent * 0.01f);

        if (!CanWriteTimeScale(targetTimeScale))
            return;

        Time.timeScale = targetTimeScale;
        lastAppliedTimeScale = targetTimeScale;
    }

    /// <summary>
    /// Restores the time scale captured before Impact Frame ownership when no active request remains.
    /// </summary>
    public static void Clear()
    {
        if (!ownsTimeScale)
            return;

        float currentTimeScale = Time.timeScale;
        bool stillOwnsCurrentValue = math.abs(currentTimeScale - lastAppliedTimeScale) <= ComparisonEpsilon;

        if (stillOwnsCurrentValue)
            Time.timeScale = previousTimeScale;

        ownsTimeScale = false;
        lastAppliedTimeScale = Time.timeScale;
    }

    /// <summary>
    /// Resets static ownership when Unity reloads the runtime domain.
    /// </summary>
    public static void Reset()
    {
        ownsTimeScale = false;
        previousTimeScale = 1f;
        lastAppliedTimeScale = 1f;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Captures Time.timeScale before the first active Impact Frame write.
    /// </summary>
    private static void CaptureCurrentTimeScale()
    {
        previousTimeScale = math.max(0f, Time.timeScale);
        lastAppliedTimeScale = previousTimeScale;
        ownsTimeScale = true;
    }

    /// <summary>
    /// Resolves whether Impact Frame can safely write the requested time scale this frame.
    /// </summary>
    /// <param name="targetTimeScale">Time scale value Impact Frame wants to apply.</param>
    /// <returns>True when the current global value still appears to be owned or can be lowered by Impact Frame.</returns>
    private static bool CanWriteTimeScale(float targetTimeScale)
    {
        float currentTimeScale = Time.timeScale;

        if (math.abs(currentTimeScale - lastAppliedTimeScale) <= ComparisonEpsilon)
            return true;

        return currentTimeScale > targetTimeScale;
    }
    #endregion

    #endregion
}
