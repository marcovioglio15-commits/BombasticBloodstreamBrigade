using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the live pie preview used by orbital projection cone-bounce authoring fields.
/// </summary>
public static class PowerUpOrbitalProjectionConePieChartUtility
{
    #region Constants
    private static readonly Color BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.42f);
    private static readonly Color ConeColor = new Color(0.16f, 0.78f, 0.56f, 0.84f);
    private static readonly Color CenterDirectionColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
    private static readonly Color ForwardDirectionColor = new Color(1f, 0.84f, 0.22f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the orbital cone preview chart with the common Player Management Tool sizing.
    /// </summary>
    /// <returns>Configured pie chart ready for a cone preview refresh.</returns>
    public static PieChartElement CreatePreviewChart()
    {
        PieChartElement pieChart = new PieChartElement();
        pieChart.style.minHeight = 220f;
        pieChart.style.marginTop = 4f;
        pieChart.style.marginBottom = 6f;
        pieChart.SetZoom(0.95f);
        return pieChart;
    }

    /// <summary>
    /// Updates the live pie preview for one authored orbital projection cone.
    /// </summary>
    /// <param name="pieChart">Pie chart receiving slices and markers.</param>
    /// <param name="coneCenterAngleProperty">Serialized cone center angle property.</param>
    /// <param name="coneAngleProperty">Serialized cone width property.</param>
    public static void UpdatePreview(PieChartElement pieChart,
                                     SerializedProperty coneCenterAngleProperty,
                                     SerializedProperty coneAngleProperty)
    {
        if (pieChart == null || coneCenterAngleProperty == null || coneAngleProperty == null)
            return;

        float coneCenterDegrees = NormalizeAngle(coneCenterAngleProperty.floatValue);
        float coneWidthDegrees = Mathf.Clamp(coneAngleProperty.floatValue, 0f, 360f);
        float halfConeDegrees = coneWidthDegrees * 0.5f;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionMarkers = new List<float>();
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        // Keep the whole orbit visible while the cone occupies only its authored angular sector.
        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = 0f,
            EndAngle = 360f,
            MidAngle = 180f,
            Color = BackgroundColor
        });

        if (coneWidthDegrees > 0f)
            AddNormalizedSlice(slices,
                               NormalizeAngle(coneCenterDegrees - halfConeDegrees),
                               NormalizeAngle(coneCenterDegrees + halfConeDegrees),
                               ConeColor);

        directionMarkers.Add(coneCenterDegrees);
        labels.Add(new PieChartElement.LabelDescriptor
        {
            Angle = coneCenterDegrees,
            Text = "Cone",
            RadiusOffset = -12f,
            TextColor = Color.white,
            UseTextColor = true
        });

        pieChart.SetSlices(slices);
        pieChart.SetDirectionMarkers(directionMarkers,
                                     CenterDirectionColor,
                                     ForwardDirectionColor,
                                     0f,
                                     true);
        pieChart.SetSegmentLabels(labels);
        pieChart.SetOverlayFields(null);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Adds one cone slice and splits it when the authored interval wraps through zero degrees.
    /// </summary>
    /// <param name="slices">Mutable pie slice list.</param>
    /// <param name="startDegrees">Normalized slice start angle.</param>
    /// <param name="endDegrees">Normalized slice end angle.</param>
    /// <param name="color">Slice color.</param>
    private static void AddNormalizedSlice(List<PieChartElement.PieSlice> slices,
                                           float startDegrees,
                                           float endDegrees,
                                           Color color)
    {
        if (slices == null)
            return;

        if (Mathf.Approximately(startDegrees, endDegrees))
        {
            AddSlice(slices, 0f, 360f, color);
            return;
        }

        if (startDegrees < endDegrees)
        {
            AddSlice(slices, startDegrees, endDegrees, color);
            return;
        }

        AddSlice(slices, startDegrees, 360f, color);
        AddSlice(slices, 0f, endDegrees, color);
    }

    /// <summary>
    /// Adds one pie slice with a midpoint that stays inside the requested angular interval.
    /// </summary>
    /// <param name="slices">Mutable pie slice list.</param>
    /// <param name="startDegrees">Slice start angle.</param>
    /// <param name="endDegrees">Slice end angle.</param>
    /// <param name="color">Slice color.</param>
    private static void AddSlice(List<PieChartElement.PieSlice> slices,
                                 float startDegrees,
                                 float endDegrees,
                                 Color color)
    {
        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = startDegrees,
            EndAngle = endDegrees,
            MidAngle = startDegrees + (endDegrees - startDegrees) * 0.5f,
            Color = color
        });
    }

    /// <summary>
    /// Normalizes one angle into the zero-through-360 preview range.
    /// </summary>
    /// <param name="angleDegrees">Angle value edited in the inspector.</param>
    /// <returns>Normalized preview angle.</returns>
    private static float NormalizeAngle(float angleDegrees)
    {
        float normalizedDegrees = angleDegrees % 360f;

        if (normalizedDegrees < 0f)
            normalizedDegrees += 360f;

        return normalizedDegrees;
    }
    #endregion

    #endregion
}
