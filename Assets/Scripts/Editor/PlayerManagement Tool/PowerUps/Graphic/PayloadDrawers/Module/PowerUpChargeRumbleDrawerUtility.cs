using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws scalable charge-completion rumble controls and non-mutating range warnings.
/// </summary>
public static class PowerUpChargeRumbleDrawerUtility
{
    #region Methods

    #region Layout
    /// <summary>
    /// Adds feedback controls for module defaults and per-binding payload overrides.
    /// </summary>
    /// <param name="parent">Hold-charge editor container.</param>
    /// <param name="payload">Serialized hold-charge module.</param>
    public static void Build(VisualElement parent, SerializedProperty payload)
    {
        // Use the shared factory so every field carries Add Scaling and formula controls.
        SerializedProperty enabled = payload.FindPropertyRelative("chargeCompleteRumbleEnabled");
        SerializedProperty duration = payload.FindPropertyRelative("chargeCompleteRumbleDurationSeconds");
        SerializedProperty lowFrequency = payload.FindPropertyRelative("chargeCompleteRumbleLowFrequency");
        SerializedProperty highFrequency = payload.FindPropertyRelative("chargeCompleteRumbleHighFrequency");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(parent, enabled, "Charge Complete Rumble");
        VisualElement settings = new VisualElement();
        settings.style.marginLeft = 12f;
        parent.Add(settings);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(settings, duration, "Duration Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(settings, lowFrequency, "Low Frequency");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(settings, highFrequency, "High Frequency");
        HelpBox warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        settings.Add(warning);

        // Formula-driven enable flags keep dependent tuning reachable with a false base value.
        Action refresh = () =>
        {
            settings.style.display = enabled.boolValue || PlayerScalingFieldElementFactory.HasEnabledScaling(enabled)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            warning.text = ResolveWarning(duration.floatValue, lowFrequency.floatValue, highFrequency.floatValue);
            warning.style.display = string.IsNullOrEmpty(warning.text) ? DisplayStyle.None : DisplayStyle.Flex;
        };
        refresh();
        parent.TrackSerializedObjectValue(payload.serializedObject, changedObject => refresh());
    }
    #endregion

    #region Validation
    /// <summary>
    /// Reports invalid impulse settings without snapping serialized values.
    /// </summary>
    /// <param name="duration">Authored duration in seconds.</param>
    /// <param name="lowFrequency">Authored low-frequency motor strength.</param>
    /// <param name="highFrequency">Authored high-frequency motor strength.</param>
    /// <returns>One concise warning, or empty text for valid tuning.</returns>
    public static string ResolveWarning(float duration, float lowFrequency, float highFrequency)
    {
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
            return "Charge Complete Rumble Duration must be finite and greater than zero.";

        if (!IsMotorStrengthValid(lowFrequency) || !IsMotorStrengthValid(highFrequency))
            return "Charge Complete Rumble motor strengths must be finite values between 0 and 1.";

        if (lowFrequency <= 0f && highFrequency <= 0f)
            return "Both charge-completion motor strengths are zero; the impulse will be silent.";

        return string.Empty;
    }

    /// <summary>
    /// Checks the input-device range without altering the authored value.
    /// </summary>
    /// <param name="value">Motor strength being inspected.</param>
    /// <returns>True when the strength is finite and within range.</returns>
    private static bool IsMotorStrengthValid(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }
    #endregion

    #endregion
}
