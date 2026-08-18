using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds scaling-aware payload controls and non-mutating warnings for conditional power-up application modules.
/// </summary>
public static class PowerUpConditionalApplicationPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Delayed Shoot Application payload editor and interval warning.
    /// </summary>
    /// <param name="container">Visual container receiving the controls.</param>
    /// <param name="payloadProperty">Serialized Delayed Shoot Application payload.</param>
    public static void BuildDelayedShootApplication(VisualElement container, SerializedProperty payloadProperty)
    {
        if (container == null || payloadProperty == null)
            return;

        SerializedProperty shotIntervalProperty = payloadProperty.FindPropertyRelative("shotInterval");

        if (shotIntervalProperty == null)
        {
            container.Add(new HelpBox("Delayed Shoot Application payload fields are missing.", HelpBoxMessageType.Warning));
            return;
        }

        VisualElement shotIntervalField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(container,
                                                                                               shotIntervalProperty,
                                                                                               "Shot Interval");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        container.Add(warningBox);
        Action refreshWarnings = () => SetWarnings(warningBox,
                                                   shotIntervalProperty.intValue > 0
                                                       ? null
                                                       : "Shot Interval should be > 0.");
        RegisterRefresh(shotIntervalField, refreshWarnings);
        refreshWarnings();
    }

    /// <summary>
    /// Builds context-sensitive Sudden Strike condition, tolerance, movement-slow, and recovery controls.
    /// </summary>
    /// <param name="container">Visual container receiving the controls.</param>
    /// <param name="payloadProperty">Serialized Sudden Strike payload.</param>
    public static void BuildSuddenStrike(VisualElement container, SerializedProperty payloadProperty)
    {
        if (container == null || payloadProperty == null)
            return;

        SerializedProperty conditionModeProperty = payloadProperty.FindPropertyRelative("conditionMode");
        SerializedProperty countRotationProperty = payloadProperty.FindPropertyRelative("countRotationAsMovement");
        SerializedProperty speedToleranceProperty = payloadProperty.FindPropertyRelative("stationarySpeedTolerance");
        SerializedProperty rotationToleranceProperty = payloadProperty.FindPropertyRelative("stationaryRotationToleranceDegrees");
        SerializedProperty applySlowProperty = payloadProperty.FindPropertyRelative("applyChargeMovementSlow");
        SerializedProperty slowRecoveryProperty = payloadProperty.FindPropertyRelative("movementSlowRecoverySeconds");

        if (conditionModeProperty == null ||
            countRotationProperty == null ||
            speedToleranceProperty == null ||
            rotationToleranceProperty == null ||
            applySlowProperty == null ||
            slowRecoveryProperty == null)
        {
            container.Add(new HelpBox("Sudden Strike payload fields are missing.", HelpBoxMessageType.Warning));
            return;
        }

        VisualElement conditionModeField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(container,
                                                                                                 conditionModeProperty,
                                                                                                 "Charge Condition");
        VisualElement stationaryContainer = new VisualElement();
        stationaryContainer.style.marginLeft = 12f;
        container.Add(stationaryContainer);
        VisualElement countRotationField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(stationaryContainer,
                                                                                                countRotationProperty,
                                                                                                "Count Rotation As Movement");
        VisualElement speedToleranceField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(stationaryContainer,
                                                                                                  speedToleranceProperty,
                                                                                                  "Stationary Speed Tolerance");
        VisualElement rotationToleranceContainer = new VisualElement();
        rotationToleranceContainer.style.marginLeft = 12f;
        stationaryContainer.Add(rotationToleranceContainer);
        VisualElement rotationToleranceField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(rotationToleranceContainer,
                                                                                                     rotationToleranceProperty,
                                                                                                     "Stationary Rotation Tolerance");
        VisualElement applySlowField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(container,
                                                                                            applySlowProperty,
                                                                                            "Apply Charge Movement Slow");
        VisualElement slowRecoveryContainer = new VisualElement();
        slowRecoveryContainer.style.marginLeft = 12f;
        container.Add(slowRecoveryContainer);
        VisualElement slowRecoveryField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(slowRecoveryContainer,
                                                                                               slowRecoveryProperty,
                                                                                               "Movement Slow Recovery Seconds");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        container.Add(warningBox);

        Action refresh = () =>
        {
            bool isStationaryMode = conditionModeProperty.enumValueIndex == (int)SuddenStrikeChargeConditionMode.Stationary;
            stationaryContainer.style.display = isStationaryMode ? DisplayStyle.Flex : DisplayStyle.None;
            rotationToleranceContainer.style.display = isStationaryMode && countRotationProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            slowRecoveryContainer.style.display = applySlowProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            List<string> warnings = new List<string>();

            if (isStationaryMode && speedToleranceProperty.floatValue < 0f)
                warnings.Add("Stationary Speed Tolerance should be >= 0.");

            if (isStationaryMode && countRotationProperty.boolValue && rotationToleranceProperty.floatValue < 0f)
                warnings.Add("Stationary Rotation Tolerance should be >= 0.");

            if (applySlowProperty.boolValue && slowRecoveryProperty.floatValue < 0f)
                warnings.Add("Movement Slow Recovery Seconds should be >= 0.");

            SetWarnings(warningBox, warnings.Count > 0 ? string.Join("\n", warnings) : null);
        };

        RegisterRefresh(conditionModeField, refresh);
        RegisterRefresh(countRotationField, refresh);
        RegisterRefresh(speedToleranceField, refresh);
        RegisterRefresh(rotationToleranceField, refresh);
        RegisterRefresh(applySlowField, refresh);
        RegisterRefresh(slowRecoveryField, refresh);
        refresh();
    }

    /// <summary>
    /// Builds Self-Preservation Instinct threshold controls with mode-specific labels and range warnings.
    /// </summary>
    /// <param name="container">Visual container receiving the controls.</param>
    /// <param name="payloadProperty">Serialized Self-Preservation Instinct payload.</param>
    public static void BuildSelfPreservationInstinct(VisualElement container, SerializedProperty payloadProperty)
    {
        if (container == null || payloadProperty == null)
            return;

        SerializedProperty thresholdModeProperty = payloadProperty.FindPropertyRelative("thresholdMode");
        SerializedProperty healthThresholdProperty = payloadProperty.FindPropertyRelative("healthThreshold");

        if (thresholdModeProperty == null || healthThresholdProperty == null)
        {
            container.Add(new HelpBox("Self-Preservation Instinct payload fields are missing.", HelpBoxMessageType.Warning));
            return;
        }

        VisualElement thresholdModeField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(container,
                                                                                                 thresholdModeProperty,
                                                                                                 "Threshold Mode");
        VisualElement healthThresholdField = PowerUpModuleDefinitionPayloadDrawerUtility.AddField(container,
                                                                                                   healthThresholdProperty,
                                                                                                   "Health Threshold");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        container.Add(warningBox);
        Action refresh = () =>
        {
            bool usesPercent = thresholdModeProperty.enumValueIndex == (int)SelfPreservationHealthThresholdMode.MaximumHealthPercent;
            healthThresholdField.tooltip = usesPercent
                ? "Percentage of maximum health that triggers sibling active effects on a downward crossing. Supports Add Scaling."
                : "Direct current-health value that triggers sibling active effects on a downward crossing. Supports Add Scaling.";
            string warning = usesPercent && (healthThresholdProperty.floatValue < 0f || healthThresholdProperty.floatValue > 100f)
                ? "Health Threshold should be between 0 and 100 in Maximum Health Percent mode."
                : healthThresholdProperty.floatValue < 0f
                    ? "Health Threshold should be >= 0."
                    : null;
            SetWarnings(warningBox, warning);
        };
        RegisterRefresh(thresholdModeField, refresh);
        RegisterRefresh(healthThresholdField, refresh);
        refresh();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Registers a local value-change callback without observing unrelated preset properties.
    /// </summary>
    /// <param name="field">Field root that emits serialized property changes.</param>
    /// <param name="refresh">Callback that refreshes contextual state.</param>
    private static void RegisterRefresh(VisualElement field, Action refresh)
    {
        if (field == null || refresh == null)
            return;

        field.RegisterCallback<SerializedPropertyChangeEvent>(changeEvent => refresh());
    }

    /// <summary>
    /// Shows or hides one warning box from an optional message.
    /// </summary>
    /// <param name="warningBox">Warning element to update.</param>
    /// <param name="message">Warning text, or null when the payload is coherent.</param>
    private static void SetWarnings(HelpBox warningBox, string message)
    {
        if (warningBox == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        warningBox.text = hasMessage ? message : string.Empty;
        warningBox.style.display = hasMessage ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
