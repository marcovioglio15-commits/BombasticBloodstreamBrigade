using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the specialized short-range dash payload editor used by advanced-pattern drawers.
/// </summary>
internal static class EnemyShortRangeDashPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the payload editor for the short-range dash module.
    /// </summary>
    /// <param name="payloadDataProperty">Serialized payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>True when UI is built successfully.</returns>
    public static bool BuildShortRangeDashPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty shortRangeDashProperty = payloadDataProperty.FindPropertyRelative("shortRangeDash");

        if (shortRangeDashProperty == null)
        {
            HelpBox missingBox = new HelpBox("ShortRangeDash payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty aimProperty = shortRangeDashProperty.FindPropertyRelative("aim");
        SerializedProperty recoveryProperty = shortRangeDashProperty.FindPropertyRelative("recovery");
        SerializedProperty distanceProperty = shortRangeDashProperty.FindPropertyRelative("distance");
        SerializedProperty pathProperty = shortRangeDashProperty.FindPropertyRelative("path");

        if (aimProperty == null || recoveryProperty == null || distanceProperty == null || pathProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("ShortRangeDash payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        HelpBox categorySettingsInfoBox = new HelpBox("Activation range and release buffer are configured on the Short-Range Interaction assembly.", HelpBoxMessageType.Info);
        payloadContainer.Add(categorySettingsInfoBox);

        Foldout aimFoldout = CreateDashPayloadFoldout(aimProperty, "Aim", "Aim");
        payloadContainer.Add(aimFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(aimFoldout, aimProperty.FindPropertyRelative("aimDurationSeconds"), "Aim Duration Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(aimFoldout, aimProperty.FindPropertyRelative("moveSpeedMultiplierWhileAiming"), "Move Speed Multiplier While Aiming");

        Foldout recoveryFoldout = CreateDashPayloadFoldout(recoveryProperty, "Recovery", "Recovery");
        payloadContainer.Add(recoveryFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, recoveryProperty.FindPropertyRelative("cooldownSeconds"), "Cooldown Seconds");

        Foldout distanceFoldout = CreateDashPayloadFoldout(distanceProperty, "Distance", "Distance");
        payloadContainer.Add(distanceFoldout);

        SerializedProperty distanceSourceProperty = distanceProperty.FindPropertyRelative("distanceSource");
        SerializedProperty playerDistanceMultiplierProperty = distanceProperty.FindPropertyRelative("playerDistanceMultiplier");
        SerializedProperty distanceOffsetProperty = distanceProperty.FindPropertyRelative("distanceOffset");
        SerializedProperty fixedDistanceProperty = distanceProperty.FindPropertyRelative("fixedDistance");
        SerializedProperty minimumTravelDistanceProperty = distanceProperty.FindPropertyRelative("minimumTravelDistance");
        SerializedProperty maximumTravelDistanceProperty = distanceProperty.FindPropertyRelative("maximumTravelDistance");

        EnemyAdvancedPatternDrawerUtility.AddField(distanceFoldout, distanceSourceProperty, "Distance Source");

        VisualElement playerDistanceContainer = new VisualElement();
        playerDistanceContainer.style.marginLeft = 12f;
        distanceFoldout.Add(playerDistanceContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(playerDistanceContainer, playerDistanceMultiplierProperty, "Player Distance Multiplier");
        EnemyAdvancedPatternDrawerUtility.AddField(playerDistanceContainer, distanceOffsetProperty, "Distance Offset");

        VisualElement fixedDistanceContainer = new VisualElement();
        fixedDistanceContainer.style.marginLeft = 12f;
        distanceFoldout.Add(fixedDistanceContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(fixedDistanceContainer, fixedDistanceProperty, "Fixed Distance");

        EnemyAdvancedPatternDrawerUtility.AddField(distanceFoldout, minimumTravelDistanceProperty, "Minimum Travel Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(distanceFoldout, maximumTravelDistanceProperty, "Maximum Travel Distance");

        UpdateDistanceSourceVisibility(distanceSourceProperty, playerDistanceContainer, fixedDistanceContainer);
        distanceFoldout.TrackPropertyValue(distanceSourceProperty, changedProperty =>
        {
            UpdateDistanceSourceVisibility(changedProperty, playerDistanceContainer, fixedDistanceContainer);
        });

        Foldout pathFoldout = CreateDashPayloadFoldout(pathProperty, "Path", "Path");
        payloadContainer.Add(pathFoldout);

        SerializedProperty dashDurationSecondsProperty = pathProperty.FindPropertyRelative("dashDurationSeconds");
        SerializedProperty lateralAmplitudeProperty = pathProperty.FindPropertyRelative("lateralAmplitude");
        SerializedProperty mirrorModeProperty = pathProperty.FindPropertyRelative("mirrorMode");
        SerializedProperty forwardProgressCurveProperty = pathProperty.FindPropertyRelative("forwardProgressCurve");
        SerializedProperty lateralOffsetCurveProperty = pathProperty.FindPropertyRelative("lateralOffsetCurve");

        EnemyAdvancedPatternDrawerUtility.AddField(pathFoldout, dashDurationSecondsProperty, "Dash Duration Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(pathFoldout, lateralAmplitudeProperty, "Lateral Amplitude");
        EnemyAdvancedPatternDrawerUtility.AddField(pathFoldout, mirrorModeProperty, "Mirror Mode");
        EnemyAdvancedPatternDrawerUtility.AddField(pathFoldout, forwardProgressCurveProperty, "Forward Progress Curve");
        EnemyAdvancedPatternDrawerUtility.AddField(pathFoldout, lateralOffsetCurveProperty, "Lateral Offset Curve");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.marginTop = 4f;
        payloadContainer.Add(warningBox);

        RefreshWarning();

        List<SerializedProperty> trackedProperties = new List<SerializedProperty>
        {
            aimProperty.FindPropertyRelative("aimDurationSeconds"),
            aimProperty.FindPropertyRelative("moveSpeedMultiplierWhileAiming"),
            recoveryProperty.FindPropertyRelative("cooldownSeconds"),
            distanceSourceProperty,
            playerDistanceMultiplierProperty,
            distanceOffsetProperty,
            fixedDistanceProperty,
            minimumTravelDistanceProperty,
            maximumTravelDistanceProperty,
            dashDurationSecondsProperty,
            lateralAmplitudeProperty,
            forwardProgressCurveProperty,
            lateralOffsetCurveProperty
        };

        for (int propertyIndex = 0; propertyIndex < trackedProperties.Count; propertyIndex++)
        {
            SerializedProperty trackedProperty = trackedProperties[propertyIndex];

            if (trackedProperty == null)
                continue;

            payloadContainer.TrackPropertyValue(trackedProperty, changedProperty =>
            {
                RefreshWarning();
            });
        }

        if (payloadDataProperty.serializedObject != null)
        {
            payloadContainer.TrackSerializedObjectValue(payloadDataProperty.serializedObject, changedObject =>
            {
                RefreshWarning();
            });
        }

        return true;

        void RefreshWarning()
        {
            List<string> warnings = CollectWarnings(aimProperty,
                                                    recoveryProperty,
                                                    distanceProperty,
                                                    pathProperty);

            if (warnings.Count <= 0)
            {
                warningBox.style.display = DisplayStyle.None;
                warningBox.text = string.Empty;
                return;
            }

            warningBox.style.display = DisplayStyle.Flex;
            warningBox.text = string.Join("\n", warnings.ToArray());
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates a short-range dash foldout with stable state across payload rebuilds.
    /// </summary>
    /// <param name="property">Serialized dash payload subsection.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="suffix">Local suffix used to distinguish sibling foldouts.</param>
    /// <returns>Configured foldout element.</returns>
    private static Foldout CreateDashPayloadFoldout(SerializedProperty property, string title, string suffix)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property, title, "ShortRangeDashPayload" + suffix, true);
        foldout.tooltip = "Groups " + title + " short-range dash settings.";
        return foldout;
    }

    /// <summary>
    /// Updates distance-field visibility according to the selected travel distance source.
    /// </summary>
    /// <param name="distanceSourceProperty">Serialized distance source property.</param>
    /// <param name="playerDistanceContainer">Container for player-distance controls.</param>
    /// <param name="fixedDistanceContainer">Container for fixed-distance controls.</param>
    private static void UpdateDistanceSourceVisibility(SerializedProperty distanceSourceProperty,
                                                       VisualElement playerDistanceContainer,
                                                       VisualElement fixedDistanceContainer)
    {
        EnemyShortRangeDashDistanceSource distanceSource = EnemyShortRangeDashDistanceSource.PlayerDistance;

        if (distanceSourceProperty != null && distanceSourceProperty.propertyType == SerializedPropertyType.Enum)
            distanceSource = (EnemyShortRangeDashDistanceSource)distanceSourceProperty.enumValueIndex;

        if (playerDistanceContainer != null)
            playerDistanceContainer.style.display = distanceSource == EnemyShortRangeDashDistanceSource.PlayerDistance
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        if (fixedDistanceContainer != null)
            fixedDistanceContainer.style.display = distanceSource == EnemyShortRangeDashDistanceSource.FixedDistance
                ? DisplayStyle.Flex
                : DisplayStyle.None;
    }

    /// <summary>
    /// Collects non-destructive authoring warnings for the short-range dash payload.
    /// </summary>
    /// <param name="aimProperty">Serialized aim payload property.</param>
    /// <param name="distanceProperty">Serialized distance payload property.</param>
    /// <param name="pathProperty">Serialized path payload property.</param>
    /// <returns>Ordered warning list.</returns>
    private static List<string> CollectWarnings(SerializedProperty aimProperty,
                                                SerializedProperty recoveryProperty,
                                                SerializedProperty distanceProperty,
                                                SerializedProperty pathProperty)
    {
        List<string> warnings = new List<string>();

        if (pathProperty != null)
        {
            SerializedProperty dashDurationSecondsProperty = pathProperty.FindPropertyRelative("dashDurationSeconds");
            SerializedProperty forwardProgressCurveProperty = pathProperty.FindPropertyRelative("forwardProgressCurve");
            SerializedProperty lateralOffsetCurveProperty = pathProperty.FindPropertyRelative("lateralOffsetCurve");

            if (dashDurationSecondsProperty != null && dashDurationSecondsProperty.floatValue <= 0f)
                warnings.Add("Dash Duration Seconds should be greater than 0 so the dash can advance along its sampled path.");

            if (forwardProgressCurveProperty != null)
                AddForwardCurveWarnings(forwardProgressCurveProperty.animationCurveValue, warnings);

            if (lateralOffsetCurveProperty != null)
                AddLateralCurveWarnings(lateralOffsetCurveProperty.animationCurveValue, warnings);
        }

        if (distanceProperty != null)
        {
            SerializedProperty minimumTravelDistanceProperty = distanceProperty.FindPropertyRelative("minimumTravelDistance");
            SerializedProperty maximumTravelDistanceProperty = distanceProperty.FindPropertyRelative("maximumTravelDistance");

            if (minimumTravelDistanceProperty != null &&
                maximumTravelDistanceProperty != null &&
                maximumTravelDistanceProperty.floatValue < minimumTravelDistanceProperty.floatValue)
            {
                warnings.Add("Maximum Travel Distance is lower than Minimum Travel Distance. Runtime will clamp to the minimum.");
            }
        }

        if (aimProperty != null)
        {
            SerializedProperty aimDurationSecondsProperty = aimProperty.FindPropertyRelative("aimDurationSeconds");

            if (aimDurationSecondsProperty != null && aimDurationSecondsProperty.floatValue <= 0f)
                warnings.Add("Aim Duration Seconds is 0, so the enemy will release the dash instantly without a visible telegraph.");
        }

        if (recoveryProperty != null)
        {
            SerializedProperty cooldownSecondsProperty = recoveryProperty.FindPropertyRelative("cooldownSeconds");

            if (cooldownSecondsProperty != null && cooldownSecondsProperty.floatValue <= 0f)
                warnings.Add("Cooldown Seconds is 0, so the enemy can begin a new dash aim immediately after the previous committed dash ends.");
        }

        return warnings;
    }

    /// <summary>
    /// Adds authoring warnings for the forward progression curve.
    /// </summary>
    /// <param name="forwardProgressCurve">Authored forward progression curve.</param>
    /// <param name="warnings">Mutable warning list.</param>
    private static void AddForwardCurveWarnings(AnimationCurve forwardProgressCurve, List<string> warnings)
    {
        if (forwardProgressCurve == null || warnings == null)
            return;

        float startValue = forwardProgressCurve.Evaluate(0f);
        float endValue = forwardProgressCurve.Evaluate(1f);

        if (Mathf.Abs(startValue) > 0.02f)
            warnings.Add("Forward Progress Curve does not start near 0. Runtime will still start the dash from the current position.");

        if (Mathf.Abs(endValue - 1f) > 0.02f)
            warnings.Add("Forward Progress Curve does not end near 1. Runtime will force the final sample to reach full forward travel distance.");
    }

    /// <summary>
    /// Adds authoring warnings for the lateral offset curve.
    /// </summary>
    /// <param name="lateralOffsetCurve">Authored lateral offset curve.</param>
    /// <param name="warnings">Mutable warning list.</param>
    private static void AddLateralCurveWarnings(AnimationCurve lateralOffsetCurve, List<string> warnings)
    {
        if (lateralOffsetCurve == null || warnings == null)
            return;

        float startValue = lateralOffsetCurve.Evaluate(0f);

        if (Mathf.Abs(startValue) > 0.02f)
            warnings.Add("Lateral Offset Curve does not start near 0. Runtime will still force the dash to begin from the enemy current position.");
    }
    #endregion

    #endregion
}
