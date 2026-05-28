using System.Globalization;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds non-destructive editor warnings for the camera follow spring values. Out-of-range numbers are
/// never snapped on the asset: they are surfaced here and clamped defensively by the runtime at point of use.
/// </summary>
internal static class PlayerControllerCameraWarningUtility
{
    #region Constants
    private const float SmoothTimeEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes the camera value coherence warnings without mutating the serialized settings.
    /// Called by the controller camera section whenever the behavior or a smoothing value changes.
    /// </summary>
    /// <param name="warningsRoot">Container that receives the warning HelpBoxes.</param>
    /// <param name="showWarnings">True when the current behavior actually uses the follow spring values.</param>
    /// <param name="smoothTimeProperty">Serialized smooth time property, or null when unavailable.</param>
    /// <param name="maxFollowDistanceProperty">Serialized max follow distance property, or null when unavailable.</param>
    /// <param name="deadZoneRadiusProperty">Serialized dead zone radius property, or null when unavailable.</param>
    public static void RefreshCameraValueWarnings(VisualElement warningsRoot,
                                                  bool showWarnings,
                                                  SerializedProperty smoothTimeProperty,
                                                  SerializedProperty maxFollowDistanceProperty,
                                                  SerializedProperty deadZoneRadiusProperty)
    {
        if (warningsRoot == null)
            return;

        warningsRoot.Clear();

        if (!showWarnings)
            return;

        // Resolve current values once; missing properties default to neutral values that raise no warning.
        float smoothTime = smoothTimeProperty != null ? smoothTimeProperty.floatValue : 1f;
        float maxFollowDistance = maxFollowDistanceProperty != null ? maxFollowDistanceProperty.floatValue : 0f;
        float deadZoneRadius = deadZoneRadiusProperty != null ? deadZoneRadiusProperty.floatValue : 0f;

        // Smooth time coherence: negative is invalid, exact zero disables smoothing entirely.
        if (smoothTime < 0f)
            AddWarning(warningsRoot, "Smooth Time is negative. The follow spring clamps it to a near-instant value at runtime.");
        else if (smoothTime <= SmoothTimeEpsilon)
            AddWarning(warningsRoot, "Smooth Time is 0. The camera snaps instantly to the target with no smoothing.");

        // Leash and dead zone individual ranges.
        if (maxFollowDistance < 0f)
            AddWarning(warningsRoot, "Max Follow Distance is negative. Runtime treats it as 0, disabling the follow leash.");

        if (deadZoneRadius < 0f)
            AddWarning(warningsRoot, "Dead Zone Radius is negative. Runtime clamps it to 0.");

        // Cross-field coherence: a dead zone wider than the leash leaves the camera unable to chase the target.
        if (maxFollowDistance > 0f && deadZoneRadius >= maxFollowDistance)
            AddWarning(warningsRoot,
                       string.Format(CultureInfo.InvariantCulture,
                                     "Dead Zone Radius ({0:0.###}) is greater than or equal to Max Follow Distance ({1:0.###}); the leash sits inside the dead zone, so the camera may stop following the player.",
                                     deadZoneRadius,
                                     maxFollowDistance));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends one warning HelpBox to the warnings container.
    /// </summary>
    /// <param name="warningsRoot">Container that receives the HelpBox.</param>
    /// <param name="message">Warning message to display.</param>
    private static void AddWarning(VisualElement warningsRoot, string message)
    {
        HelpBox warningBox = new HelpBox(message, HelpBoxMessageType.Warning);
        warningsRoot.Add(warningBox);
    }
    #endregion

    #endregion
}
