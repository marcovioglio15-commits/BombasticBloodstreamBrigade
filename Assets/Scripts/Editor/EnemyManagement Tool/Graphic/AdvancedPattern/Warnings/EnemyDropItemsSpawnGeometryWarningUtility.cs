using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds editor warnings for DropItems spawn geometry settings without mutating authored values.
/// </summary>
internal static class EnemyDropItemsSpawnGeometryWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes a warning box with spawn geometry messages for one DropItems payload section.
    /// </summary>
    /// <param name="dropRadiusProperty">Serialized radial spread property.</param>
    /// <param name="groundHeightOffsetProperty">Serialized vertical offset property.</param>
    /// <param name="payloadLabel">Readable payload label used in warning text.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    public static void Refresh(SerializedProperty dropRadiusProperty,
                               SerializedProperty groundHeightOffsetProperty,
                               string payloadLabel,
                               HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warnings = new List<string>();
        AppendWarnings(dropRadiusProperty,
                       groundHeightOffsetProperty,
                       payloadLabel,
                       warnings);

        if (warnings.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warnings);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Appends spawn geometry messages to an existing warning list so callers can merge payload-specific warnings.
    /// </summary>
    /// <param name="dropRadiusProperty">Serialized radial spread property.</param>
    /// <param name="groundHeightOffsetProperty">Serialized vertical offset property.</param>
    /// <param name="payloadLabel">Readable payload label used in warning text.</param>
    /// <param name="warnings">Mutable warning list receiving messages.</param>
    public static void AppendWarnings(SerializedProperty dropRadiusProperty,
                                      SerializedProperty groundHeightOffsetProperty,
                                      string payloadLabel,
                                      List<string> warnings)
    {
        if (warnings == null)
            return;

        string resolvedPayloadLabel = string.IsNullOrWhiteSpace(payloadLabel)
            ? "Drop"
            : payloadLabel;

        AppendDropRadiusWarning(dropRadiusProperty, resolvedPayloadLabel, warnings);
        AppendGroundHeightOffsetWarning(groundHeightOffsetProperty, resolvedPayloadLabel, warnings);
    }
    #endregion

    #region Warning Builders
    /// <summary>
    /// Appends radial spread warnings when the authored radius cannot produce coherent spawn geometry.
    /// </summary>
    /// <param name="dropRadiusProperty">Serialized radial spread property.</param>
    /// <param name="payloadLabel">Readable payload label used in warning text.</param>
    /// <param name="warnings">Mutable warning list receiving messages.</param>
    private static void AppendDropRadiusWarning(SerializedProperty dropRadiusProperty,
                                                string payloadLabel,
                                                List<string> warnings)
    {
        if (!TryReadFloat(dropRadiusProperty, out float dropRadius))
            return;

        if (!IsFinite(dropRadius))
        {
            warnings.Add(payloadLabel + " Drop Radius is not finite. Runtime spread placement can become invalid.");
            return;
        }

        if (dropRadius < 0f)
            warnings.Add(payloadLabel + " Drop Radius is negative. Runtime treats it as 0.");
    }

    /// <summary>
    /// Appends vertical offset warnings when authored values are non-finite or unusually large.
    /// </summary>
    /// <param name="groundHeightOffsetProperty">Serialized vertical offset property.</param>
    /// <param name="payloadLabel">Readable payload label used in warning text.</param>
    /// <param name="warnings">Mutable warning list receiving messages.</param>
    private static void AppendGroundHeightOffsetWarning(SerializedProperty groundHeightOffsetProperty,
                                                        string payloadLabel,
                                                        List<string> warnings)
    {
        if (!TryReadFloat(groundHeightOffsetProperty, out float groundHeightOffset))
            return;

        if (!IsFinite(groundHeightOffset))
        {
            warnings.Add(payloadLabel + " Ground Height Offset is not finite. Runtime uses the shared default offset.");
            return;
        }

        if (UnityEngine.Mathf.Abs(groundHeightOffset) > EnemyDropItemsSpawnSettingsDefaults.GroundHeightOffsetWarningMagnitude)
            warnings.Add(payloadLabel + " Ground Height Offset is unusually large. Pickups may appear detached from the floor or buried.");
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Reads a serialized float property when it is present and typed correctly.
    /// </summary>
    /// <param name="property">Serialized property to read.</param>
    /// <param name="value">Resolved float value when available.</param>
    /// <returns>True when a float value was read.</returns>
    private static bool TryReadFloat(SerializedProperty property, out float value)
    {
        value = 0f;

        if (property == null)
            return false;

        if (property.propertyType != SerializedPropertyType.Float)
            return false;

        value = property.floatValue;
        return true;
    }

    /// <summary>
    /// Checks whether a floating-point value can safely be baked into drop spawn geometry.
    /// </summary>
    /// <param name="value">Candidate value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
