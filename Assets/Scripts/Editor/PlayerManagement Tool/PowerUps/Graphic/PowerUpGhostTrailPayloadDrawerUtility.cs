using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws the scaling-aware Ghost Trail payload with contextual toggle-duration and screen-feedback controls.
/// </summary>
internal static class PowerUpGhostTrailPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Ghost Trail payload editor and its non-mutating validation warnings.
    /// </summary>
    /// <param name="container">Container receiving the Ghost Trail controls.</param>
    /// <param name="payloadProperty">Serialized Ghost Trail payload.</param>
    /// <param name="showToggleDurationOption">True when the owning active contains a toggleable Resource Gate.</param>
    public static void Build(VisualElement container,
                             SerializedProperty payloadProperty,
                             bool showToggleDurationOption)
    {
        if (container == null || payloadProperty == null)
            return;

        SerializedProperty duration = payloadProperty.FindPropertyRelative("durationSeconds");
        SerializedProperty matchToggleDuration = payloadProperty.FindPropertyRelative("matchToggleActivationDuration");
        SerializedProperty screenFeedbackEnabled = payloadProperty.FindPropertyRelative("screenFeedbackEnabled");
        SerializedProperty screenFeedback = payloadProperty.FindPropertyRelative("screenFeedback");

        if (duration == null ||
            matchToggleDuration == null ||
            screenFeedbackEnabled == null ||
            screenFeedback == null)
        {
            container.Add(new HelpBox("Ghost Trail payload fields are missing. Reopen the asset after recompiling.",
                                      HelpBoxMessageType.Warning));
            return;
        }

        Foldout timing = CreateFoldout("Timing", true);
        Foldout capture = CreateFoldout("Capture", true);
        Foldout appearance = CreateFoldout("Appearance", true);
        Foldout screen = CreateFoldout("Screen And Camera Feedback", false);
        container.Add(timing);
        container.Add(capture);
        container.Add(appearance);
        container.Add(screen);

        VisualElement durationField = AddField(timing, duration, "Duration Seconds");
        VisualElement matchToggleDurationField = AddField(timing,
                                                           matchToggleDuration,
                                                           "Match Toggle Activation Duration");
        matchToggleDurationField.style.display = showToggleDurationOption ? DisplayStyle.Flex : DisplayStyle.None;
        AddField(timing, payloadProperty.FindPropertyRelative("easeInUnscaledSeconds"), "Ease In Unscaled Seconds");
        AddField(timing, payloadProperty.FindPropertyRelative("easeOutUnscaledSeconds"), "Ease Out Unscaled Seconds");
        AddField(timing, payloadProperty.FindPropertyRelative("easingMode"), "Easing Mode");
        AddField(timing, payloadProperty.FindPropertyRelative("emissionIntervalSeconds"), "Emission Interval Seconds");
        AddField(timing, payloadProperty.FindPropertyRelative("snapshotLifetimeSeconds"), "Snapshot Lifetime Seconds");

        AddField(capture, payloadProperty.FindPropertyRelative("captureScope"), "Capture Scope");
        AddField(capture, payloadProperty.FindPropertyRelative("movementDistanceThreshold"), "Movement Distance Threshold");
        AddField(capture, payloadProperty.FindPropertyRelative("rotationAngleThresholdDegrees"), "Rotation Angle Threshold Degrees");
        AddField(capture, payloadProperty.FindPropertyRelative("maximumActiveSnapshots"), "Maximum Active Snapshots");
        BuildScalableTint(appearance, payloadProperty.FindPropertyRelative("tint"));

        VisualElement screenFeedbackEnabledField = AddField(screen, screenFeedbackEnabled, "Enabled");
        VisualElement screenFeedbackDetails = new VisualElement();
        screen.Add(screenFeedbackDetails);
        PowerUpImpactFrameExtendedPayloadDrawerUtility.BuildStandaloneEffect(screenFeedbackDetails,
                                                                            screenFeedback,
                                                                            "Effect Profile");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        container.Add(warningBox);

        System.Action refresh = () =>
        {
            durationField.style.display = showToggleDurationOption && matchToggleDuration.boolValue
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            screenFeedbackDetails.style.display = screenFeedbackEnabled.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshWarnings(payloadProperty,
                            showToggleDurationOption,
                            matchToggleDuration,
                            screenFeedbackEnabled,
                            screenFeedback,
                            warningBox);
        };
        matchToggleDurationField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        screenFeedbackEnabledField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        container.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
        refresh();
    }
    #endregion

    #region Builders
    /// <summary>
    /// Draws a color preview and four scaling-aware tint channels.
    /// </summary>
    /// <param name="parent">Parent receiving tint controls.</param>
    /// <param name="tintProperty">Serialized Vector4 tint property.</param>
    private static void BuildScalableTint(VisualElement parent, SerializedProperty tintProperty)
    {
        if (parent == null || tintProperty == null)
            return;

        Vector4 tint = tintProperty.vector4Value;
        ColorField colorField = new ColorField("Tint");
        colorField.showAlpha = true;
        colorField.tooltip = tintProperty.tooltip;
        colorField.SetValueWithoutNotify(new Color(tint.x, tint.y, tint.z, tint.w));
        parent.Add(colorField);
        Foldout channels = CreateFoldout("Scalable Tint Channels", false);
        parent.Add(channels);
        AddField(channels, tintProperty.FindPropertyRelative("x"), "Red");
        AddField(channels, tintProperty.FindPropertyRelative("y"), "Green");
        AddField(channels, tintProperty.FindPropertyRelative("z"), "Blue");
        AddField(channels, tintProperty.FindPropertyRelative("w"), "Alpha");

        colorField.RegisterValueChangedCallback(evt =>
        {
            tintProperty.serializedObject.Update();
            tintProperty.vector4Value = new Vector4(evt.newValue.r,
                                                    evt.newValue.g,
                                                    evt.newValue.b,
                                                    evt.newValue.a);
            tintProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        colorField.TrackPropertyValue(tintProperty, property =>
        {
            Vector4 value = property.vector4Value;
            colorField.SetValueWithoutNotify(new Color(value.x, value.y, value.z, value.w));
        });
    }

    /// <summary>
    /// Adds one scaling-aware serialized field.
    /// </summary>
    /// <param name="parent">Parent receiving the field.</param>
    /// <param name="property">Serialized property to draw.</param>
    /// <param name="label">Visible field label.</param>
    /// <returns>Created field root, or null when the property is unavailable.</returns>
    private static VisualElement AddField(VisualElement parent, SerializedProperty property, string label)
    {
        return PowerUpModuleDefinitionPayloadDrawerUtility.AddField(parent, property, label);
    }

    /// <summary>
    /// Creates a compact Ghost Trail foldout.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="expanded">Initial expansion state.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string title, bool expanded)
    {
        Foldout foldout = new Foldout
        {
            text = title,
            value = expanded
        };
        foldout.style.marginLeft = 8f;
        return foldout;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Refreshes coherent Ghost Trail warnings without snapping authored values.
    /// </summary>
    /// <param name="payloadProperty">Serialized Ghost Trail payload.</param>
    /// <param name="showToggleDurationOption">True when matched toggle duration is valid.</param>
    /// <param name="matchToggleDuration">Serialized matched-toggle flag.</param>
    /// <param name="screenFeedbackEnabled">Serialized screen-feedback flag.</param>
    /// <param name="screenFeedback">Serialized reusable screen-feedback profile.</param>
    /// <param name="warningBox">Warning box receiving validation messages.</param>
    private static void RefreshWarnings(SerializedProperty payloadProperty,
                                        bool showToggleDurationOption,
                                        SerializedProperty matchToggleDuration,
                                        SerializedProperty screenFeedbackEnabled,
                                        SerializedProperty screenFeedback,
                                        HelpBox warningBox)
    {
        List<string> warnings = new List<string>();

        if (!showToggleDurationOption || !matchToggleDuration.boolValue)
            AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("durationSeconds"), "Duration Seconds", false);

        AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("easeInUnscaledSeconds"), "Ease In Unscaled Seconds", true);
        AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("easeOutUnscaledSeconds"), "Ease Out Unscaled Seconds", true);
        AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("emissionIntervalSeconds"), "Emission Interval Seconds", false);
        AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("snapshotLifetimeSeconds"), "Snapshot Lifetime Seconds", false);
        AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("movementDistanceThreshold"), "Movement Distance Threshold", true);
        AddPositiveWarning(warnings, payloadProperty.FindPropertyRelative("rotationAngleThresholdDegrees"), "Rotation Angle Threshold Degrees", true);
        AddTintWarning(warnings, payloadProperty.FindPropertyRelative("tint"));

        SerializedProperty maximumActiveSnapshots = payloadProperty.FindPropertyRelative("maximumActiveSnapshots");

        if (maximumActiveSnapshots != null && maximumActiveSnapshots.intValue <= 0)
            warnings.Add("Maximum Active Snapshots must be greater than 0.");

        if (matchToggleDuration.boolValue && !showToggleDurationOption)
            warnings.Add("Match Toggle Activation Duration requires an enabled toggleable Resource Gate on the owning active power-up.");

        if (screenFeedbackEnabled.boolValue)
            PowerUpImpactFrameExtendedPayloadDrawerUtility.AddStandaloneEffectWarnings(warnings,
                                                                                       screenFeedback,
                                                                                       "Ghost Trail");

        PowerUpPayloadWarningBoxUtility.ApplyWarnings(warningBox, warnings);
    }

    /// <summary>
    /// Adds a non-negative or strictly-positive numeric warning.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="property">Serialized numeric property.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="allowZero">True when zero is valid.</param>
    private static void AddPositiveWarning(List<string> warnings,
                                           SerializedProperty property,
                                           string label,
                                           bool allowZero)
    {
        if (property == null)
            return;

        if (allowZero && property.floatValue < 0f)
            warnings.Add(label + " should not be negative.");
        else if (!allowZero && property.floatValue <= 0f)
            warnings.Add(label + " must be greater than 0.");
    }

    /// <summary>
    /// Adds one coherent warning when any authored residual-image tint channel is outside the runtime range.
    /// </summary>
    /// <param name="warnings">Mutable warning list.</param>
    /// <param name="tintProperty">Serialized RGBA residual-image tint.</param>
    private static void AddTintWarning(List<string> warnings, SerializedProperty tintProperty)
    {
        if (tintProperty == null)
            return;

        Vector4 tint = tintProperty.vector4Value;

        if (tint.x < 0f ||
            tint.x > 1f ||
            tint.y < 0f ||
            tint.y > 1f ||
            tint.z < 0f ||
            tint.z > 1f ||
            tint.w < 0f ||
            tint.w > 1f)
        {
            warnings.Add("Tint channels are clamped at runtime to the 0-1 range.");
        }
    }
    #endregion

    #endregion
}
