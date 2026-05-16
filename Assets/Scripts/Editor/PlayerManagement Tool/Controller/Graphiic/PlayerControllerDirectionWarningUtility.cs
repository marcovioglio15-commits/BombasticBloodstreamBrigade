using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds non-destructive editor warnings for discrete controller direction settings.
/// </summary>
internal static class PlayerControllerDirectionWarningUtility
{
    #region Constants
    private const float AlignmentEpsilon = 0.001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes discrete-direction warnings without mutating the serialized settings.
    /// </summary>
    /// <param name="warningsRoot">Container that receives warning HelpBoxes.</param>
    /// <param name="showWarnings">True when the current section is using discrete directions.</param>
    /// <param name="discreteDirectionCount">Configured discrete direction count.</param>
    /// <param name="directionOffsetDegrees">Configured direction offset in degrees.</param>
    public static void RefreshOffsetWarnings(VisualElement warningsRoot,
                                             bool showWarnings,
                                             int discreteDirectionCount,
                                             float directionOffsetDegrees)
    {
        if (warningsRoot == null)
            return;

        warningsRoot.Clear();

        if (!showWarnings)
            return;

        if (discreteDirectionCount < 1)
        {
            HelpBox warningBox = new HelpBox("Direction Count is below 1. Runtime treats it as 1.", HelpBoxMessageType.Warning);
            warningsRoot.Add(warningBox);
            return;
        }

        if (discreteDirectionCount <= 1)
            return;

        if (IsAlignedToDiscreteStep(directionOffsetDegrees, discreteDirectionCount))
            return;

        float stepDegrees = 360f / discreteDirectionCount;
        HelpBox alignmentWarningBox = new HelpBox(string.Format(CultureInfo.InvariantCulture,
                                                                "Direction Offset is not aligned to the current discrete step size of {0:0.###} deg. Runtime keeps the raw offset, so allowed directions rotate exactly as authored instead of snapping to the nearest step.",
                                                                stepDegrees),
                                                  HelpBoxMessageType.Warning);
        warningsRoot.Add(alignmentWarningBox);
    }

    /// <summary>
    /// Checks whether one discrete direction offset lands exactly on the current step grid.
    /// </summary>
    /// <param name="directionOffsetDegrees">Offset value to inspect.</param>
    /// <param name="discreteDirectionCount">Configured discrete direction count.</param>
    /// <returns>True when the offset already matches the discrete step grid; otherwise false.</returns>
    public static bool IsAlignedToDiscreteStep(float directionOffsetDegrees, int discreteDirectionCount)
    {
        if (discreteDirectionCount <= 1)
            return true;

        float stepDegrees = 360f / discreteDirectionCount;
        float normalizedOffset = Mathf.Repeat(directionOffsetDegrees, 360f);
        float snappedOffset = Mathf.Round(normalizedOffset / stepDegrees) * stepDegrees;
        float wrappedSnappedOffset = Mathf.Repeat(snappedOffset, 360f);
        return Mathf.Abs(wrappedSnappedOffset - normalizedOffset) <= AlignmentEpsilon;
    }
    #endregion

    #endregion
}
