using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the look section UI for player controller preset panels and wires its live visualization callbacks.
/// </summary>
internal static class PlayerControllerPresetsPanelLookSectionUtility
{
    #region Constants
    private const float MultiplierRowSpacing = 2f;
    private const float SubSectionMarginLeft = 5f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the full look section inside the provided container and wires all property-driven refresh callbacks.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and helper utilities.</param>
    /// <param name="section">Pre-created section container that receives the look UI.</param>

    public static void BuildLookSection(PlayerControllerPresetsPanel panel, VisualElement section)
    {
        if (panel == null || section == null)
            return;

        SerializedProperty lookProperty = panel.PresetSerializedObject.FindProperty("lookSettings");

        if (lookProperty == null)
            return;

        SerializedProperty directionsModeProperty = lookProperty.FindPropertyRelative("m_DirectionsMode");
        SerializedProperty countProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionCount");
        SerializedProperty offsetProperty = lookProperty.FindPropertyRelative("m_DirectionOffsetDegrees");
        SerializedProperty rotationModeProperty = lookProperty.FindPropertyRelative("m_RotationMode");
        SerializedProperty rotationSpeedProperty = lookProperty.FindPropertyRelative("m_RotationSpeed");
        SerializedProperty samplingProperty = lookProperty.FindPropertyRelative("m_MultiplierSampling");
        SerializedProperty maxSpeedMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionMaxSpeedMultipliers");
        SerializedProperty accelerationMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionAccelerationMultipliers");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        SerializedProperty frontEnabledProperty = lookProperty.FindPropertyRelative("m_FrontConeEnabled");
        SerializedProperty frontAngleProperty = lookProperty.FindPropertyRelative("m_FrontConeAngle");
        SerializedProperty frontMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeMaxSpeedMultiplier");
        SerializedProperty frontAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeAccelerationMultiplier");
        SerializedProperty backEnabledProperty = lookProperty.FindPropertyRelative("m_BackConeEnabled");
        SerializedProperty backAngleProperty = lookProperty.FindPropertyRelative("m_BackConeAngle");
        SerializedProperty backMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeMaxSpeedMultiplier");
        SerializedProperty backAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeAccelerationMultiplier");
        SerializedProperty leftEnabledProperty = lookProperty.FindPropertyRelative("m_LeftConeEnabled");
        SerializedProperty leftAngleProperty = lookProperty.FindPropertyRelative("m_LeftConeAngle");
        SerializedProperty leftMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeMaxSpeedMultiplier");
        SerializedProperty leftAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeAccelerationMultiplier");
        SerializedProperty rightEnabledProperty = lookProperty.FindPropertyRelative("m_RightConeEnabled");
        SerializedProperty rightAngleProperty = lookProperty.FindPropertyRelative("m_RightConeAngle");
        SerializedProperty rightMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeMaxSpeedMultiplier");
        SerializedProperty rightAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeAccelerationMultiplier");

        VisualElement directionsModeField = PlayerScalingFieldElementFactory.CreateField(directionsModeProperty,
                                                                                         scalingRulesProperty,
                                                                                         "Allowed Directions");
        section.Add(directionsModeField);

        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        VisualElement countFieldElement = PlayerScalingFieldElementFactory.CreateField(countProperty, scalingRulesProperty, "Direction Count");
        discreteContainer.Add(countFieldElement);
        VisualElement offsetFieldElement = PlayerScalingFieldElementFactory.CreateField(offsetProperty, scalingRulesProperty, "Direction Offset");
        discreteContainer.Add(offsetFieldElement);

        VisualElement discreteWarningsRoot = new VisualElement();
        discreteWarningsRoot.style.marginTop = 4f;
        discreteContainer.Add(discreteWarningsRoot);

        VisualElement conesContainer = new VisualElement();
        conesContainer.style.marginLeft = 8f;
        conesContainer.style.marginTop = 4f;

        List<VisualElement> coneRefreshFields = new List<VisualElement>();
        conesContainer.Add(BuildConeRow("Front",
                                        frontEnabledProperty,
                                        frontAngleProperty,
                                        frontMaxSpeedMultiplierProperty,
                                        frontAccelerationMultiplierProperty,
                                        scalingRulesProperty,
                                        coneRefreshFields));
        conesContainer.Add(BuildConeRow("Back",
                                        backEnabledProperty,
                                        backAngleProperty,
                                        backMaxSpeedMultiplierProperty,
                                        backAccelerationMultiplierProperty,
                                        scalingRulesProperty,
                                        coneRefreshFields));
        conesContainer.Add(BuildConeRow("Left",
                                        leftEnabledProperty,
                                        leftAngleProperty,
                                        leftMaxSpeedMultiplierProperty,
                                        leftAccelerationMultiplierProperty,
                                        scalingRulesProperty,
                                        coneRefreshFields));
        conesContainer.Add(BuildConeRow("Right",
                                        rightEnabledProperty,
                                        rightAngleProperty,
                                        rightMaxSpeedMultiplierProperty,
                                        rightAccelerationMultiplierProperty,
                                        scalingRulesProperty,
                                        coneRefreshFields));

        PieChartElement pieChart = new PieChartElement();
        Slider lookZoomSlider = PlayerControllerPresetsPanelFieldUtility.CreatePieZoomSlider(pieChart);
        VisualElement multipliersSection = PlayerControllerPresetsPanelVisualizationUtility.BuildDiscreteMultipliersSection(out VisualElement multipliersTableRoot, out Label multipliersHeader);

        section.Add(discreteContainer);
        section.Add(conesContainer);
        section.Add(pieChart);
        section.Add(lookZoomSlider);
        section.Add(multipliersSection);

        VisualElement rotationModeField = PlayerScalingFieldElementFactory.CreateField(rotationModeProperty,
                                                                                       scalingRulesProperty,
                                                                                       "Rotation Mode");
        section.Add(rotationModeField);

        VisualElement rotationSpeedField = PlayerScalingFieldElementFactory.CreateField(rotationSpeedProperty, scalingRulesProperty, "Rotation Speed");
        section.Add(rotationSpeedField);

        VisualElement samplingField = PlayerScalingFieldElementFactory.CreateField(samplingProperty,
                                                                                   scalingRulesProperty,
                                                                                   "Multiplier Sampling");
        section.Add(samplingField);

        SerializedProperty lookActionProperty = panel.PresetSerializedObject.FindProperty("lookActionId");
        PlayerControllerPresetsPanelFieldUtility.EnsureDefaultActionId(panel, lookActionProperty, "Look");

        Foldout bindingsFoldout = PlayerControllerPresetsPanelFieldUtility.BuildBindingsFoldout(panel.InputAsset,
                                                                                                panel.PresetSerializedObject,
                                                                                                lookActionProperty,
                                                                                                InputActionSelectionElement.SelectionMode.Look);
        section.Add(bindingsFoldout);

        SerializedProperty valuesProperty = lookProperty.FindPropertyRelative("m_Values");
        Foldout valuesFoldout = PlayerControllerPresetsPanelFieldUtility.BuildValuesFoldout(valuesProperty,
                                                                                            scalingRulesProperty,
                                                                                            new string[]
        {
            "m_RotationDamping",
            "m_RotationMaxSpeed",
            "m_RotationDeadZone",
            "m_DigitalReleaseGraceSeconds"
        });
        section.Add(valuesFoldout);

        System.Action updateView = () =>
        {
            LookDirectionsMode directionsMode = (LookDirectionsMode)directionsModeProperty.enumValueIndex;
            RotationMode rotationMode = (RotationMode)rotationModeProperty.enumValueIndex;
            LookMultiplierSampling samplingMode = samplingProperty != null ? (LookMultiplierSampling)samplingProperty.enumValueIndex : LookMultiplierSampling.DirectionalBlend;
            bool isDiscrete = directionsMode == LookDirectionsMode.DiscreteCount;
            bool isCones = directionsMode == LookDirectionsMode.Cones;
            bool followMovement = directionsMode == LookDirectionsMode.FollowMovementDirection;

            discreteContainer.style.display = isDiscrete && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerDirectionWarningUtility.RefreshOffsetWarnings(discreteWarningsRoot,
                                                                         isDiscrete && !followMovement,
                                                                         countProperty.intValue,
                                                                         offsetProperty.floatValue);
            conesContainer.style.display = isCones && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            pieChart.style.display = directionsMode == LookDirectionsMode.AllDirections || followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            lookZoomSlider.style.display = directionsMode == LookDirectionsMode.AllDirections || followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            rotationModeField.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            rotationSpeedField.style.display = rotationMode == RotationMode.Continuous && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            samplingField.style.display = isDiscrete && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            bindingsFoldout.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            valuesFoldout.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;

            PlayerControllerPresetsPanelVisualizationUtility.UpdateLookPieChart(pieChart,
                                                                                directionsMode,
                                                                                countProperty.intValue,
                                                                                offsetProperty.floatValue,
                                                                                frontEnabledProperty,
                                                                                frontAngleProperty,
                                                                                backEnabledProperty,
                                                                                backAngleProperty,
                                                                                leftEnabledProperty,
                                                                                leftAngleProperty,
                                                                                rightEnabledProperty,
                                                                                rightAngleProperty);
            PlayerControllerPresetsPanelVisualizationUtility.UpdateLookLabels(pieChart, directionsMode, samplingMode, countProperty.intValue, offsetProperty.floatValue);
            pieChart.SetOverlayFields(null);

            multipliersSection.style.display = isDiscrete && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            multipliersSection.style.marginLeft = SubSectionMarginLeft;

            if (multipliersHeader != null)
                multipliersHeader.text = samplingMode == LookMultiplierSampling.ArcConstant ? "Arc Multipliers" : "Directional Multipliers";

            if (isDiscrete)
            {
                PlayerControllerPresetsPanelVisualizationUtility.UpdateDiscreteMultipliersTable(panel,
                                                                                                multipliersTableRoot,
                                                                                                samplingMode,
                                                                                                countProperty.intValue,
                                                                                                offsetProperty.floatValue,
                                                                                                maxSpeedMultipliersProperty,
                                                                                                accelerationMultipliersProperty);
            }
        };

        directionsModeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        countFieldElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        countFieldElement.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            ScheduleViewRefresh();
        });

        offsetFieldElement.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ScheduleViewRefresh();
        });

        offsetFieldElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        rotationModeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        samplingField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        for (int index = 0; index < coneRefreshFields.Count; index++)
        {
            VisualElement refreshField = coneRefreshFields[index];
            refreshField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                updateView();
            });
        }

        updateView();

        void ScheduleViewRefresh()
        {
            section.schedule.Execute(() =>
            {
                if (panel.PresetSerializedObject != null)
                    panel.PresetSerializedObject.Update();

                updateView();
            }).ExecuteLater(0);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one cone row with scaling-aware enabled and angle fields plus percentage preview controls.
    /// </summary>
    /// <param name="label">Displayed cone label.</param>
    /// <param name="enabledProperty">Serialized cone enabled property.</param>
    /// <param name="angleProperty">Serialized cone angle property.</param>
    /// <param name="maxSpeedProperty">Serialized max-speed multiplier property.</param>
    /// <param name="accelerationProperty">Serialized acceleration multiplier property.</param>
    /// <param name="scalingRulesProperty">Controller scaling-rules array used by Add Scaling.</param>
    /// <param name="refreshFields">Optional list that receives fields which should trigger preview refreshes.</param>
    /// <returns>Returns the constructed cone row.</returns>
    private static VisualElement BuildConeRow(string label,
                                              SerializedProperty enabledProperty,
                                              SerializedProperty angleProperty,
                                              SerializedProperty maxSpeedProperty,
                                              SerializedProperty accelerationProperty,
                                              SerializedProperty scalingRulesProperty,
                                              List<VisualElement> refreshFields)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.FlexStart;
        row.style.marginBottom = MultiplierRowSpacing;

        VisualElement enabledField = PlayerScalingFieldElementFactory.CreateField(enabledProperty,
                                                                                  scalingRulesProperty,
                                                                                  label);
        enabledField.style.minWidth = 180f;
        enabledField.style.flexGrow = 1f;
        row.Add(enabledField);

        VisualElement angleField = PlayerScalingFieldElementFactory.CreateField(angleProperty,
                                                                                scalingRulesProperty,
                                                                                "Angle");
        angleField.style.width = 160f;
        angleField.style.flexShrink = 0f;
        row.Add(angleField);

        row.Add(PlayerControllerPresetsPanelVisualizationUtility.CreatePercentField(maxSpeedProperty,
                                                                                    new Color(0.2f, 0.8f, 1f, 0.85f),
                                                                                    "Max speed multiplier for this cone.",
                                                                                    "Max %"));
        row.Add(PlayerControllerPresetsPanelVisualizationUtility.CreatePercentField(accelerationProperty,
                                                                                    new Color(1f, 0.65f, 0.2f, 0.85f),
                                                                                    "Acceleration multiplier for this cone.",
                                                                                    "Accel %"));

        if (refreshFields != null)
        {
            refreshFields.Add(enabledField);
            refreshFields.Add(angleField);
        }

        return row;
    }
    #endregion

    #endregion
}
