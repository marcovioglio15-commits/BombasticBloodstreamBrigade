using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds chart-heavy payload editors used to preview projectile cone and split configurations.
/// </summary>
public static class PowerUpModuleDefinitionVisualizationUtility
{
    #region Constants
    private static readonly Color SplitUniformColorA = new Color(0.20f, 0.62f, 0.88f, 0.85f);
    private static readonly Color SplitUniformColorB = new Color(0.12f, 0.42f, 0.72f, 0.85f);
    private static readonly Color SplitCustomColor = new Color(0.96f, 0.56f, 0.18f, 0.88f);
    private static readonly Color SplitBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
    private static readonly Color ConeBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.42f);
    private static readonly Color ConeFillColor = new Color(0.18f, 0.72f, 0.92f, 0.82f);
    private static readonly Color ConeDirectionColor = new Color(0.94f, 0.94f, 0.94f, 0.92f);
    private static readonly Color ConeForwardColor = new Color(1f, 0.84f, 0.22f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the cone payload editor and live preview chart.
    /// </summary>
    /// <param name="payloadContainer">Container that hosts the payload UI.</param>
    /// <param name="conePayloadProperty">Serialized cone payload property.</param>
    public static void BuildProjectilePatternConePayloadUi(VisualElement payloadContainer, SerializedProperty conePayloadProperty)
    {
        if (payloadContainer == null || conePayloadProperty == null)
            return;

        SerializedProperty projectileCountProperty = conePayloadProperty.FindPropertyRelative("projectileCount");
        SerializedProperty coneAngleDegreesProperty = conePayloadProperty.FindPropertyRelative("coneAngleDegrees");
        SerializedProperty laserDurationSecondsProperty = conePayloadProperty.FindPropertyRelative("laserDurationSeconds");
        SerializedProperty ignoreInheritedPlayerVelocityXProperty = conePayloadProperty.FindPropertyRelative("ignoreInheritedPlayerVelocityX");
        SerializedProperty ignoreInheritedPlayerVelocityZProperty = conePayloadProperty.FindPropertyRelative("ignoreInheritedPlayerVelocityZ");

        if (projectileCountProperty == null ||
            coneAngleDegreesProperty == null ||
            laserDurationSecondsProperty == null ||
            ignoreInheritedPlayerVelocityXProperty == null ||
            ignoreInheritedPlayerVelocityZProperty == null)
        {
            HelpBox errorBox = new HelpBox("Projectile cone payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, projectileCountProperty, "Projectile Count");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, coneAngleDegreesProperty, "Cone Angle Degrees");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, laserDurationSecondsProperty, "Laser Duration Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, ignoreInheritedPlayerVelocityXProperty, "Ignore Inherited Velocity X");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, ignoreInheritedPlayerVelocityZProperty, "Ignore Inherited Velocity Z");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;
        payloadContainer.Add(warningBox);

        Label chartLabel = new Label("Cone Pattern Preview");
        chartLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        chartLabel.style.marginTop = 4f;
        payloadContainer.Add(chartLabel);

        PieChartElement pieChart = CreatePreviewPieChart();
        payloadContainer.Add(pieChart);

        payloadContainer.TrackPropertyValue(projectileCountProperty, changedProperty =>
        {
            UpdateProjectilePatternConePieChart(pieChart, changedProperty, coneAngleDegreesProperty);
            RefreshWarnings();
        });

        payloadContainer.TrackPropertyValue(coneAngleDegreesProperty, changedProperty =>
        {
            UpdateProjectilePatternConePieChart(pieChart, projectileCountProperty, changedProperty);
            RefreshWarnings();
        });

        payloadContainer.TrackPropertyValue(laserDurationSecondsProperty, changedProperty => RefreshWarnings());

        UpdateProjectilePatternConePieChart(pieChart, projectileCountProperty, coneAngleDegreesProperty);
        RefreshWarnings();

        void RefreshWarnings()
        {
            List<string> warnings = new List<string>();

            if (projectileCountProperty.intValue <= 0)
                warnings.Add("Projectile Count must be greater than zero.");

            if (coneAngleDegreesProperty.floatValue < 0f ||
                coneAngleDegreesProperty.floatValue > PlayerProjectileConePatternUtility.FullCircleDegrees)
            {
                warnings.Add("Cone Angle Degrees must remain within 0-360 degrees.");
            }

            if (laserDurationSecondsProperty.floatValue < 0f)
                warnings.Add("Laser Duration Seconds cannot be negative.");

            warningBox.text = string.Join("\n", warnings);
            warningBox.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds the split payload editor and live preview chart.
    /// </summary>
    /// <param name="payloadContainer">Container that hosts the payload UI.</param>
    /// <param name="splitPayloadProperty">Serialized split payload property.</param>
    public static void BuildProjectileSplitPayloadUi(VisualElement payloadContainer, SerializedProperty splitPayloadProperty)
    {
        if (payloadContainer == null || splitPayloadProperty == null)
            return;

        SerializedProperty triggerModeProperty = splitPayloadProperty.FindPropertyRelative("triggerMode");
        SerializedProperty directionModeProperty = splitPayloadProperty.FindPropertyRelative("directionMode");
        SerializedProperty splitProjectileCountProperty = splitPayloadProperty.FindPropertyRelative("splitProjectileCount");
        SerializedProperty splitOffsetDegreesProperty = splitPayloadProperty.FindPropertyRelative("splitOffsetDegrees");
        SerializedProperty customAnglesDegreesProperty = splitPayloadProperty.FindPropertyRelative("customAnglesDegrees");
        SerializedProperty splitDamagePercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitDamagePercentFromOriginal");
        SerializedProperty splitSizePercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitSizePercentFromOriginal");
        SerializedProperty splitSpeedPercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitSpeedPercentFromOriginal");
        SerializedProperty splitLifetimePercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitLifetimePercentFromOriginal");

        if (triggerModeProperty == null ||
            directionModeProperty == null ||
            splitProjectileCountProperty == null ||
            splitOffsetDegreesProperty == null ||
            customAnglesDegreesProperty == null ||
            splitDamagePercentFromOriginalProperty == null ||
            splitSizePercentFromOriginalProperty == null ||
            splitSpeedPercentFromOriginalProperty == null ||
            splitLifetimePercentFromOriginalProperty == null)
        {
            HelpBox errorBox = new HelpBox("Projectile split payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, triggerModeProperty, "Split Trigger Mode");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, directionModeProperty, "Direction Mode");

        VisualElement uniformContainer = new VisualElement();
        uniformContainer.style.marginLeft = 12f;
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(uniformContainer, splitProjectileCountProperty, "Split Projectile Count");
        payloadContainer.Add(uniformContainer);

        VisualElement customAnglesContainer = new VisualElement();
        customAnglesContainer.style.marginLeft = 12f;
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(customAnglesContainer, customAnglesDegreesProperty, "Custom Angles Degrees");
        payloadContainer.Add(customAnglesContainer);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, splitOffsetDegreesProperty, "Split Offset Degrees");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, splitDamagePercentFromOriginalProperty, "Split Damage % From Original");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, splitSizePercentFromOriginalProperty, "Split Size % From Original");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, splitSpeedPercentFromOriginalProperty, "Split Speed % From Original");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, splitLifetimePercentFromOriginalProperty, "Split Lifetime % From Original");

        UpdateDirectionModeContainers(directionModeProperty, uniformContainer, customAnglesContainer);
        payloadContainer.TrackPropertyValue(directionModeProperty, changedProperty =>
        {
            UpdateDirectionModeContainers(changedProperty, uniformContainer, customAnglesContainer);
        });

        Label chartLabel = new Label("Split Direction Pie");
        chartLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        chartLabel.style.marginTop = 4f;
        payloadContainer.Add(chartLabel);

        PieChartElement pieChart = CreatePreviewPieChart();
        payloadContainer.Add(pieChart);

        payloadContainer.TrackPropertyValue(directionModeProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                changedProperty,
                                splitProjectileCountProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(splitProjectileCountProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                changedProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(splitOffsetDegreesProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                splitProjectileCountProperty,
                                changedProperty,
                                customAnglesDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(customAnglesDegreesProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                splitProjectileCountProperty,
                                splitOffsetDegreesProperty,
                                changedProperty);
        });

        UpdateSplitPieChart(pieChart,
                            directionModeProperty,
                            splitProjectileCountProperty,
                            splitOffsetDegreesProperty,
                            customAnglesDegreesProperty);
    }
    #endregion

    #region Charts
    private static PieChartElement CreatePreviewPieChart()
    {
        PieChartElement pieChart = new PieChartElement();
        pieChart.style.minHeight = 220f;
        pieChart.style.marginTop = 4f;
        pieChart.style.marginBottom = 6f;
        pieChart.SetZoom(0.95f);
        return pieChart;
    }

    private static void UpdateProjectilePatternConePieChart(PieChartElement pieChart,
                                                            SerializedProperty projectileCountProperty,
                                                            SerializedProperty coneAngleDegreesProperty)
    {
        if (pieChart == null || projectileCountProperty == null || coneAngleDegreesProperty == null)
            return;

        int projectileCount = Mathf.Max(1, projectileCountProperty.intValue);
        float coneAngleDegrees = PlayerProjectileConePatternUtility.ResolveConeAngleDegrees(coneAngleDegreesProperty.floatValue);
        float halfCone = coneAngleDegrees * 0.5f;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionMarkers = new List<float>();
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = 0f,
            EndAngle = 360f,
            MidAngle = 180f,
            Color = ConeBackgroundColor
        });

        if (coneAngleDegrees >= PlayerProjectileConePatternUtility.FullCircleDegrees)
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = PlayerProjectileConePatternUtility.FullCircleDegrees,
                MidAngle = PlayerProjectileConePatternUtility.FullCircleDegrees * 0.5f,
                Color = ConeFillColor
            });
        }
        else if (coneAngleDegrees > 0f)
        {
            AddNormalizedSlice(slices, NormalizeAngle(-halfCone), NormalizeAngle(halfCone), ConeFillColor);
        }

        if (projectileCount <= 1)
        {
            directionMarkers.Add(0f);
            labels.Add(new PieChartElement.LabelDescriptor
            {
                Angle = 0f,
                Text = "1",
                RadiusOffset = -10f,
                TextColor = Color.white,
                UseTextColor = true
            });
        }
        else
        {
            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                float angle = PlayerProjectileConePatternUtility.ResolveDirectionAngleDegrees(projectileIndex,
                                                                                                projectileCount,
                                                                                                coneAngleDegrees);
                float normalizedAngle = NormalizeAngle(angle);
                directionMarkers.Add(normalizedAngle);
                labels.Add(new PieChartElement.LabelDescriptor
                {
                    Angle = normalizedAngle,
                    Text = (projectileIndex + 1).ToString(),
                    RadiusOffset = -10f,
                    TextColor = Color.white,
                    UseTextColor = true
                });
            }
        }

        pieChart.SetSlices(slices);
        pieChart.SetDirectionMarkers(directionMarkers, ConeDirectionColor, ConeForwardColor, 0f, true);
        pieChart.SetSegmentLabels(labels);
        pieChart.SetOverlayFields(null);
    }

    private static void UpdateSplitPieChart(PieChartElement pieChart,
                                            SerializedProperty directionModeProperty,
                                            SerializedProperty splitProjectileCountProperty,
                                            SerializedProperty splitOffsetDegreesProperty,
                                            SerializedProperty customAnglesDegreesProperty)
    {
        if (pieChart == null ||
            directionModeProperty == null ||
            splitProjectileCountProperty == null ||
            splitOffsetDegreesProperty == null ||
            customAnglesDegreesProperty == null)
        {
            return;
        }

        ProjectileSplitDirectionMode directionMode = (ProjectileSplitDirectionMode)directionModeProperty.enumValueIndex;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionMarkers = new List<float>();
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        switch (directionMode)
        {
            case ProjectileSplitDirectionMode.Uniform:
                BuildUniformSplitSlices(slices,
                                        directionMarkers,
                                        labels,
                                        splitProjectileCountProperty.intValue,
                                        splitOffsetDegreesProperty.floatValue);
                break;
            case ProjectileSplitDirectionMode.CustomAngles:
                BuildCustomSplitSlices(slices,
                                       directionMarkers,
                                       labels,
                                       customAnglesDegreesProperty,
                                       splitOffsetDegreesProperty.floatValue);
                break;
        }

        if (slices.Count == 0)
        {
            PieChartElement.PieSlice emptySlice = new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = SplitBackgroundColor
            };
            slices.Add(emptySlice);
        }

        pieChart.SetSlices(slices);
        pieChart.SetDirectionMarkers(directionMarkers,
                                     new Color(0.95f, 0.95f, 0.95f, 0.85f),
                                     new Color(1f, 0.9f, 0.3f, 1f),
                                     0f,
                                     true);
        pieChart.SetSegmentLabels(labels);
        pieChart.SetOverlayFields(null);
    }
    #endregion

    #region Helpers
    private static void UpdateDirectionModeContainers(SerializedProperty directionModeProperty,
                                                      VisualElement uniformContainer,
                                                      VisualElement customAnglesContainer)
    {
        if (directionModeProperty == null || uniformContainer == null || customAnglesContainer == null)
            return;

        ProjectileSplitDirectionMode directionMode = (ProjectileSplitDirectionMode)directionModeProperty.enumValueIndex;
        bool showUniform = directionMode == ProjectileSplitDirectionMode.Uniform;
        uniformContainer.style.display = showUniform ? DisplayStyle.Flex : DisplayStyle.None;
        customAnglesContainer.style.display = showUniform ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private static void BuildUniformSplitSlices(List<PieChartElement.PieSlice> slices,
                                                List<float> directionMarkers,
                                                List<PieChartElement.LabelDescriptor> labels,
                                                int splitCountInput,
                                                float splitOffsetDegrees)
    {
        int splitCount = Mathf.Max(1, splitCountInput);
        float step = 360f / splitCount;

        for (int splitIndex = 0; splitIndex < splitCount; splitIndex++)
        {
            float start = NormalizeAngle(splitOffsetDegrees + step * splitIndex);
            float end = NormalizeAngle(start + step);
            float mid = NormalizeAngle(start + step * 0.5f);
            AddNormalizedSlice(slices,
                               start,
                               end,
                               splitIndex % 2 == 0 ? SplitUniformColorA : SplitUniformColorB);
            directionMarkers.Add(start);

            labels.Add(new PieChartElement.LabelDescriptor
            {
                Angle = mid,
                Text = (splitIndex + 1).ToString(),
                RadiusOffset = -12f,
                TextColor = Color.white,
                UseTextColor = true
            });
        }
    }

    private static void BuildCustomSplitSlices(List<PieChartElement.PieSlice> slices,
                                               List<float> directionMarkers,
                                               List<PieChartElement.LabelDescriptor> labels,
                                               SerializedProperty customAnglesDegreesProperty,
                                               float splitOffsetDegrees)
    {
        int angleCount = customAnglesDegreesProperty.arraySize;

        if (angleCount <= 0)
            return;

        for (int angleIndex = 0; angleIndex < angleCount; angleIndex++)
        {
            SerializedProperty angleProperty = customAnglesDegreesProperty.GetArrayElementAtIndex(angleIndex);

            if (angleProperty == null)
                continue;

            float angle = NormalizeAngle(angleProperty.floatValue + splitOffsetDegrees);
            float halfWidth = 8f;
            float start = NormalizeAngle(angle - halfWidth);
            float end = NormalizeAngle(angle + halfWidth);
            AddNormalizedSlice(slices, start, end, SplitCustomColor);
            directionMarkers.Add(angle);

            labels.Add(new PieChartElement.LabelDescriptor
            {
                Angle = angle,
                Text = (angleIndex + 1).ToString(),
                RadiusOffset = -8f,
                TextColor = Color.white,
                UseTextColor = true
            });
        }
    }

    private static void AddNormalizedSlice(List<PieChartElement.PieSlice> slices, float startAngle, float endAngle, Color color)
    {
        float normalizedStart = NormalizeAngle(startAngle);
        float normalizedEnd = NormalizeAngle(endAngle);

        if (Mathf.Approximately(normalizedStart, normalizedEnd))
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = color
            });
            return;
        }

        if (normalizedEnd > normalizedStart)
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = normalizedStart,
                EndAngle = normalizedEnd,
                MidAngle = NormalizeAngle((normalizedStart + normalizedEnd) * 0.5f),
                Color = color
            });
            return;
        }

        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = normalizedStart,
            EndAngle = 360f,
            MidAngle = NormalizeAngle((normalizedStart + 360f) * 0.5f),
            Color = color
        });

        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = 0f,
            EndAngle = normalizedEnd,
            MidAngle = NormalizeAngle(normalizedEnd * 0.5f),
            Color = color
        });
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle, 360f);
    }
    #endregion

    #endregion
}
