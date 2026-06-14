using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PassiveToolSplitPieChartUtility
{
    #region Fields
    private static readonly Color SplitUniformColorA = new Color(0.20f, 0.62f, 0.88f, 0.85f);
    private static readonly Color SplitUniformColorB = new Color(0.12f, 0.42f, 0.72f, 0.85f);
    private static readonly Color SplitCustomColor = new Color(0.96f, 0.56f, 0.18f, 0.88f);
    private static readonly Color SplitBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
    #endregion

    #region Methods
    public static void UpdateSplitPieChart(PieChartElement pieChart,
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
            return;

        ProjectileSplitDirectionMode directionMode = (ProjectileSplitDirectionMode)directionModeProperty.enumValueIndex;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionMarkers = new List<float>();
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        switch (directionMode)
        {
            case ProjectileSplitDirectionMode.Uniform:
                BuildUniformSplitSlices(ref slices,
                                        ref directionMarkers,
                                        ref labels,
                                        splitProjectileCountProperty.intValue,
                                        splitOffsetDegreesProperty.floatValue);
                break;
            case ProjectileSplitDirectionMode.CustomAngles:
                BuildCustomSplitSlices(ref slices,
                                       ref directionMarkers,
                                       ref labels,
                                       customAnglesDegreesProperty,
                                       splitOffsetDegreesProperty.floatValue);
                break;
        }

        if (slices.Count == 0)
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = SplitBackgroundColor
            });
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

    private static void BuildUniformSplitSlices(ref List<PieChartElement.PieSlice> slices,
                                                ref List<float> directionMarkers,
                                                ref List<PieChartElement.LabelDescriptor> labels,
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
            AddNormalizedSlice(ref slices,
                               start,
                               end,
                               splitIndex % 2 == 0 ? SplitUniformColorA : SplitUniformColorB);
            directionMarkers.Add(start);

            PieChartElement.LabelDescriptor label = new PieChartElement.LabelDescriptor
            {
                Angle = mid,
                Text = (splitIndex + 1).ToString(),
                RadiusOffset = -12f,
                TextColor = Color.white,
                UseTextColor = true
            };
            labels.Add(label);
        }
    }

    private static void BuildCustomSplitSlices(ref List<PieChartElement.PieSlice> slices,
                                               ref List<float> directionMarkers,
                                               ref List<PieChartElement.LabelDescriptor> labels,
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
            AddNormalizedSlice(ref slices, start, end, SplitCustomColor);
            directionMarkers.Add(angle);

            PieChartElement.LabelDescriptor label = new PieChartElement.LabelDescriptor
            {
                Angle = angle,
                Text = (angleIndex + 1).ToString(),
                RadiusOffset = -8f,
                TextColor = Color.white,
                UseTextColor = true
            };
            labels.Add(label);
        }
    }

    private static void AddNormalizedSlice(ref List<PieChartElement.PieSlice> slices,
                                           float startAngle,
                                           float endAngle,
                                           Color color)
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
}
