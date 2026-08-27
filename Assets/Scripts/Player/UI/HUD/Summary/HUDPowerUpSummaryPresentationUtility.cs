using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Provides allocation-free presentation conversions shared by the power-up summary view.
/// </summary>
public static class HUDPowerUpSummaryPresentationUtility
{
    #region Methods

    /// <summary>
    /// Evaluates one authored panel easing without allocating an AnimationCurve.
    /// </summary>
    /// <param name="easing">Easing mode selected by the baked HUD configuration.</param>
    /// <param name="normalizedTime">Normalized transition time.</param>
    /// <returns>Eased interpolation factor.</returns>
    public static float EvaluateSlide(GameHudSummarySlideEasing easing,
                                      float normalizedTime)
    {
        switch (easing)
        {
            case GameHudSummarySlideEasing.Linear:
                return normalizedTime;
            case GameHudSummarySlideEasing.SmoothStep:
                return normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            default:
                float inverse = 1f - normalizedTime;
                return 1f - inverse * inverse * inverse;
        }
    }

    /// <summary>
    /// Converts an ECS color to the Unity UI representation.
    /// </summary>
    /// <param name="value">RGBA color stored in ECS.</param>
    /// <returns>Unity color used by UI graphics.</returns>
    public static Color ToColor(float4 value)
    {
        return new Color(value.x, value.y, value.z, value.w);
    }
    #endregion
}
