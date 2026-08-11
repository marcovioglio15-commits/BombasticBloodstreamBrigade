using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides allocation-free progress and Graphic operations shared by Synchro Meter presentation layers.
/// </summary>
public static class HUDSynchroMeterPresentationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Advances normalized progress toward its target using a duration for one complete fill traversal.
    /// </summary>
    /// <param name="currentProgress">Current displayed progress or float.MinValue before initialization.</param>
    /// <param name="targetProgress">Authoritative target progress.</param>
    /// <param name="smoothingSeconds">Seconds required to traverse the complete normalized range.</param>
    /// <param name="deltaTime">Frame time used for smoothing.</param>
    /// <returns>Safe normalized progress for the current frame.</returns>
    public static float AdvanceProgress(float currentProgress,
                                        float targetProgress,
                                        float smoothingSeconds,
                                        float deltaTime)
    {
        float safeTarget = Mathf.Clamp01(targetProgress);

        if (currentProgress == float.MinValue)
            return safeTarget;

        float safeDuration = HUDSynchroMeterWaveUtility.SanitizeNonNegative(smoothingSeconds, 0f);

        if (safeDuration <= 0f)
            return safeTarget;

        return Mathf.MoveTowards(Mathf.Clamp01(currentProgress),
                                 safeTarget,
                                 HUDSynchroMeterWaveUtility.SanitizeNonNegative(deltaTime, 0f) / safeDuration);
    }

    /// <summary>
    /// Assigns one Graphic color when its authored reference is available.
    /// </summary>
    /// <param name="graphic">Graphic receiving the configured color.</param>
    /// <param name="color">Color applied to the Graphic.</param>
    public static void ApplyGraphicColor(Graphic graphic, Color color)
    {
        if (graphic != null)
            graphic.color = color;
    }

    /// <summary>
    /// Assigns one Graphic alpha while preserving its current RGB channels.
    /// </summary>
    /// <param name="graphic">Graphic receiving the visibility alpha.</param>
    /// <param name="alpha">Normalized alpha value.</param>
    public static void ApplyGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    /// <summary>
    /// Enables or disables one authored Graphic when its reference is available.
    /// </summary>
    /// <param name="graphic">Graphic whose renderer state is updated.</param>
    /// <param name="isVisible">True when the Graphic should render.</param>
    public static void SetGraphicEnabled(Graphic graphic, bool isVisible)
    {
        if (graphic != null)
            graphic.enabled = isVisible;
    }

    /// <summary>
    /// Converts an ECS float4 color to the managed UI color representation.
    /// </summary>
    /// <param name="color">Color channels stored in ECS runtime config.</param>
    /// <returns>Equivalent managed Unity color.</returns>
    public static Color ToColor(float4 color)
    {
        return new Color(color.x, color.y, color.z, color.w);
    }
    #endregion

    #endregion
}
