using System;
using System.Text;
using TMPro;
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
    /// Rebuilds a progression label into a reusable buffer, replacing every supported token with the supplied numeric
    /// percentage and preserving all surrounding authored text without per-update string allocations.
    /// </summary>
    /// <param name="textBuilder">Reusable destination buffer cleared before content is appended.</param>
    /// <param name="format">Authored format containing zero or more [ProgressionPercentage] tokens.</param>
    /// <param name="progressionPercentage">Numeric percentage written without an implicit percent sign.</param>
    public static void PopulateProgressionText(StringBuilder textBuilder,
                                               string format,
                                               int progressionPercentage)
    {
        if (textBuilder == null)
            return;

        textBuilder.Clear();
        string resolvedFormat = string.IsNullOrWhiteSpace(format)
            ? GameHudSynchroMeterSettings.DefaultProgressionTextFormat
            : format;
        string token = GameHudSynchroMeterSettings.ProgressionPercentageToken;
        int searchIndex = 0;
        int safePercentage = Mathf.Clamp(progressionPercentage, 0, 100);

        // Append unchanged text spans and numeric replacements without allocating substrings.
        while (searchIndex < resolvedFormat.Length)
        {
            int tokenIndex = resolvedFormat.IndexOf(token, searchIndex, StringComparison.Ordinal);

            if (tokenIndex < 0)
            {
                textBuilder.Append(resolvedFormat, searchIndex, resolvedFormat.Length - searchIndex);
                return;
            }

            textBuilder.Append(resolvedFormat, searchIndex, tokenIndex - searchIndex);
            textBuilder.Append(safePercentage);
            searchIndex = tokenIndex + token.Length;
        }
    }

    /// <summary>
    /// Applies one tokenized progression value to an authored TMP label through a reusable text buffer.
    /// </summary>
    /// <param name="progressionText">Authored label receiving the formatted percentage.</param>
    /// <param name="textBuilder">Reusable buffer that avoids transient formatted strings.</param>
    /// <param name="format">Authored progression-label format.</param>
    /// <param name="progressionPercentage">Numeric percentage written into every supported token.</param>
    public static void ApplyProgressionText(TMP_Text progressionText,
                                            StringBuilder textBuilder,
                                            string format,
                                            int progressionPercentage)
    {
        if (progressionText == null || textBuilder == null)
            return;

        PopulateProgressionText(textBuilder, format, progressionPercentage);
        progressionText.SetText(textBuilder);
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
