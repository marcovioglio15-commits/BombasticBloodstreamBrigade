using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides pie chart, discrete multiplier table and look-direction visualization helpers for player controller preset panels.
/// </summary>
internal static class PlayerControllerPresetsPanelVisualizationUtility
{
    #region Constants
    private const float MultiplierFieldWidth = 70f;
    private const float MultiplierFieldHeight = 18f;
    private const float MultiplierLabelWidth = 56f;
    private const float MultiplierAngleWidth = 52f;
    private const float MultiplierRangeWidth = 110f;
    private const float MultiplierRowSpacing = 2f;
    private static readonly Color SliceColorA = new Color(0.2f, 0.6f, 0.9f, 0.75f);
    private static readonly Color SliceColorB = new Color(0.1f, 0.4f, 0.7f, 0.75f);
    private static readonly Color FrontConeColor = new Color(0.2f, 0.8f, 0.4f, 0.7f);
    private static readonly Color BackConeColor = new Color(0.8f, 0.3f, 0.3f, 0.7f);
    private static readonly Color LeftConeColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
    private static readonly Color RightConeColor = new Color(0.9f, 0.7f, 0.2f, 0.7f);
    private static readonly Color DirectionMarkerColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
    private static readonly Color DirectionLabelColor = new Color(0f, 0f, 0f, 1f);
    private static readonly Color ForwardMarkerColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Color MaxSpeedMultiplierColor = new Color(0.2f, 0.8f, 1f, 0.85f);
    private static readonly Color AccelerationMultiplierColor = new Color(1f, 0.65f, 0.2f, 0.85f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the discrete multiplier section and returns its table root plus header label.
    /// </summary>
    /// <param name="tableRoot">Receives the table container where multiplier rows are injected.</param>
    /// <param name="headerLabel">Receives the section header label whose title changes with sampling mode.</param>
    /// <returns>Returns the configured multipliers section container.</returns>
    public static VisualElement BuildDiscreteMultipliersSection(out VisualElement tableRoot, out Label headerLabel)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;

        headerLabel = new Label("Directional Multipliers");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.marginBottom = 2f;
        container.Add(headerLabel);

        tableRoot = new VisualElement();
        tableRoot.style.flexDirection = FlexDirection.Column;
        tableRoot.style.alignItems = Align.FlexStart;
        container.Add(tableRoot);
        return container;
    }

    /// <summary>
    /// Rebuilds the discrete multiplier table based on sampling mode, slice count and serialized multiplier arrays.
    /// </summary>
    /// <param name="panel">Owning panel used for undo and serialized object updates.</param>
    /// <param name="tableRoot">Container that receives the rebuilt rows.</param>
    /// <param name="samplingMode">Current multiplier sampling mode.</param>
    /// <param name="count">Requested discrete direction count.</param>
    /// <param name="offset">Current direction offset in degrees.</param>
    /// <param name="maxSpeedMultipliersProperty">Serialized multiplier array for max speed.</param>
    /// <param name="accelerationMultipliersProperty">Serialized multiplier array for acceleration.</param>

    public static void UpdateDiscreteMultipliersTable(PlayerControllerPresetsPanel panel,
                                                      VisualElement tableRoot,
                                                      LookMultiplierSampling samplingMode,
                                                      int count,
                                                      float offset,
                                                      SerializedProperty maxSpeedMultipliersProperty,
                                                      SerializedProperty accelerationMultipliersProperty)
    {
        if (tableRoot == null)
            return;

        tableRoot.Clear();

        if (maxSpeedMultipliersProperty == null || accelerationMultipliersProperty == null)
            return;

        int sliceCount = Mathf.Max(1, count);
        EnsureArraySize(panel, maxSpeedMultipliersProperty, sliceCount);
        EnsureArraySize(panel, accelerationMultipliersProperty, sliceCount);

        string maxSpeedTooltip = samplingMode == LookMultiplierSampling.ArcConstant
            ? "Max speed multiplier for this look arc (constant across the arc)."
            : "Max speed multiplier for this look direction (blended by alignment).";
        string accelerationTooltip = samplingMode == LookMultiplierSampling.ArcConstant
            ? "Acceleration multiplier for this look arc (constant across the arc)."
            : "Acceleration multiplier for this look direction (blended by alignment).";

        tableRoot.Add(BuildMultipliersHeaderRow(samplingMode));

        float step = 360f / sliceCount;

        for (int index = 0; index < sliceCount; index++)
        {
            SerializedProperty maxSpeedElement = maxSpeedMultipliersProperty.GetArrayElementAtIndex(index);
            SerializedProperty accelerationElement = accelerationMultipliersProperty.GetArrayElementAtIndex(index);

            if (samplingMode == LookMultiplierSampling.ArcConstant)
            {
                float startAngle = offset - (step * 0.5f) + (index * step);
                float endAngle = startAngle + step;
                tableRoot.Add(BuildArcRow(index,
                                          FormatAngleRange(startAngle, endAngle),
                                          maxSpeedElement,
                                          accelerationElement,
                                          maxSpeedTooltip,
                                          accelerationTooltip));
                continue;
            }

            float angle = Mathf.Repeat(offset + (index * step), 360f);
            tableRoot.Add(BuildDirectionRow(index, angle, maxSpeedElement, accelerationElement, maxSpeedTooltip, accelerationTooltip));
        }
    }

    /// <summary>
    /// Updates pie chart labels for discrete look direction modes and clears them for other modes.
    /// </summary>
    /// <param name="pieChart">Pie chart to update.</param>
    /// <param name="directionsMode">Current look directions mode.</param>
    /// <param name="samplingMode">Current multiplier sampling mode.</param>
    /// <param name="count">Discrete direction count.</param>
    /// <param name="offset">Current direction offset in degrees.</param>

    public static void UpdateLookLabels(PieChartElement pieChart,
                                        LookDirectionsMode directionsMode,
                                        LookMultiplierSampling samplingMode,
                                        int count,
                                        float offset)
    {
        if (pieChart == null)
            return;

        if (directionsMode != LookDirectionsMode.DiscreteCount)
        {
            pieChart.SetSegmentLabels(null);
            return;
        }

        string prefix = samplingMode == LookMultiplierSampling.ArcConstant ? "Arc" : "Dir";
        pieChart.SetSegmentLabels(BuildDirectionalLabels(count, offset, prefix));
    }

    /// <summary>
    /// Updates one movement pie chart with alternating discrete slices, labels and direction markers.
    /// </summary>
    /// <param name="pieChart">Pie chart to rebuild.</param>
    /// <param name="count">Discrete direction count.</param>
    /// <param name="offset">Current direction offset in degrees.</param>

    public static void UpdateDiscretePieChart(PieChartElement pieChart, int count, float offset)
    {
        int sliceCount = Mathf.Max(1, count);
        float step = 360f / sliceCount;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionAngles = new List<float>();

        for (int index = 0; index < sliceCount; index++)
        {
            float startAngle = offset - (step * 0.5f) + (index * step);
            AddSliceByStep(slices, startAngle, step, index % 2 == 0 ? SliceColorA : SliceColorB);
            directionAngles.Add(Mathf.Repeat(offset + (index * step), 360f));
        }

        pieChart.SetSlices(slices);
        pieChart.SetOverlayFields(null);
        pieChart.SetSegmentLabels(BuildDirectionalLabels(sliceCount, offset, "Dir"));
        pieChart.SetDirectionMarkers(directionAngles, DirectionMarkerColor, ForwardMarkerColor, 0f, true);
    }

    /// <summary>
    /// Updates the look-direction pie chart to reflect the current look mode and cone configuration.
    /// </summary>
    /// <param name="pieChart">Pie chart to rebuild.</param>
    /// <param name="mode">Current look directions mode.</param>
    /// <param name="count">Discrete direction count.</param>
    /// <param name="offset">Current direction offset in degrees.</param>
    /// <param name="frontEnabled">Serialized flag for the front cone.</param>
    /// <param name="frontAngle">Serialized angle for the front cone.</param>
    /// <param name="backEnabled">Serialized flag for the back cone.</param>
    /// <param name="backAngle">Serialized angle for the back cone.</param>
    /// <param name="leftEnabled">Serialized flag for the left cone.</param>
    /// <param name="leftAngle">Serialized angle for the left cone.</param>
    /// <param name="rightEnabled">Serialized flag for the right cone.</param>
    /// <param name="rightAngle">Serialized angle for the right cone.</param>

    public static void UpdateLookPieChart(PieChartElement pieChart,
                                          LookDirectionsMode mode,
                                          int count,
                                          float offset,
                                          SerializedProperty frontEnabled,
                                          SerializedProperty frontAngle,
                                          SerializedProperty backEnabled,
                                          SerializedProperty backAngle,
                                          SerializedProperty leftEnabled,
                                          SerializedProperty leftAngle,
                                          SerializedProperty rightEnabled,
                                          SerializedProperty rightAngle)
    {
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();

        if (mode == LookDirectionsMode.DiscreteCount)
        {
            int sliceCount = Mathf.Max(1, count);
            float step = 360f / sliceCount;
            List<float> directionAngles = new List<float>();

            for (int index = 0; index < sliceCount; index++)
            {
                float startAngle = offset - (step * 0.5f) + (index * step);
                AddSliceByStep(slices, startAngle, step, index % 2 == 0 ? SliceColorA : SliceColorB);
                directionAngles.Add(Mathf.Repeat(offset + (index * step), 360f));
            }

            pieChart.SetDirectionMarkers(directionAngles, DirectionMarkerColor, ForwardMarkerColor, 0f, true);
        }
        else if (mode == LookDirectionsMode.Cones)
        {
            AddConeSlices(slices, 0f, frontEnabled, frontAngle, FrontConeColor);
            AddConeSlices(slices, 180f, backEnabled, backAngle, BackConeColor);
            AddConeSlices(slices, 270f, leftEnabled, leftAngle, LeftConeColor);
            AddConeSlices(slices, 90f, rightEnabled, rightAngle, RightConeColor);
            pieChart.SetDirectionMarkers(null, DirectionMarkerColor, ForwardMarkerColor, 0f, false);
        }
        else
        {
            pieChart.SetDirectionMarkers(null, DirectionMarkerColor, ForwardMarkerColor, 0f, false);
        }

        pieChart.SetSlices(slices);
    }

    /// <summary>
    /// Creates a percent-backed float field for multiplier editing.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="color">Text color used by the field.</param>
    /// <param name="tooltip">Tooltip text displayed by the field.</param>
    /// <param name="label">Optional field label.</param>
    /// <returns>Returns the configured float field.</returns>
    public static FloatField CreatePercentField(SerializedProperty property, Color color, string tooltip, string label)
    {
        FloatField field = new FloatField(label);
        field.isDelayed = true;
        field.style.width = MultiplierFieldWidth;
        field.style.minWidth = MultiplierFieldWidth;
        field.style.maxWidth = MultiplierFieldWidth;
        field.style.flexGrow = 0f;
        field.style.flexShrink = 0f;
        field.style.height = MultiplierFieldHeight;
        field.style.fontSize = 10f;
        field.style.unityTextAlign = TextAnchor.MiddleCenter;
        field.style.color = color;
        field.tooltip = tooltip;

        if (string.IsNullOrWhiteSpace(label))
            field.labelElement.style.display = DisplayStyle.None;

        if (property != null)
        {
            field.value = property.floatValue * 100f;
            field.RegisterValueChangedCallback(evt =>
            {
                float storedValue = Mathf.Clamp(evt.newValue / 100f, 0f, 1f);
                property.floatValue = storedValue;
                property.serializedObject.ApplyModifiedProperties();
                field.SetValueWithoutNotify(storedValue * 100f);
            });
        }

        return field;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the multipliers table header row for the requested sampling mode.
    /// </summary>
    /// <param name="samplingMode">Current multiplier sampling mode.</param>
    /// <returns>Returns the header row.</returns>
    private static VisualElement BuildMultipliersHeaderRow(LookMultiplierSampling samplingMode)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;
        row.style.marginBottom = MultiplierRowSpacing;

        if (samplingMode == LookMultiplierSampling.ArcConstant)
        {
            row.Add(BuildHeaderLabel("Arc", MultiplierLabelWidth));
            row.Add(BuildHeaderLabel("Range", MultiplierRangeWidth));
        }
        else
        {
            row.Add(BuildHeaderLabel("Dir", MultiplierLabelWidth));
            row.Add(BuildHeaderLabel("Angle", MultiplierAngleWidth));
        }

        row.Add(BuildHeaderLabel("Max %", MultiplierFieldWidth));
        row.Add(BuildHeaderLabel("Accel %", MultiplierFieldWidth));
        return row;
    }

    /// <summary>
    /// Builds one discrete direction multiplier row.
    /// </summary>
    /// <param name="index">Zero-based direction index.</param>
    /// <param name="angle">Displayed direction angle.</param>
    /// <param name="maxSpeedProperty">Serialized max-speed multiplier property.</param>
    /// <param name="accelerationProperty">Serialized acceleration multiplier property.</param>
    /// <param name="maxSpeedTooltip">Tooltip for the max-speed field.</param>
    /// <param name="accelerationTooltip">Tooltip for the acceleration field.</param>
    /// <returns>Returns the constructed direction row.</returns>
    private static VisualElement BuildDirectionRow(int index,
                                                   float angle,
                                                   SerializedProperty maxSpeedProperty,
                                                   SerializedProperty accelerationProperty,
                                                   string maxSpeedTooltip,
                                                   string accelerationTooltip)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;
        row.style.marginBottom = MultiplierRowSpacing;
        row.Add(BuildRowLabel("Dir " + (index + 1), MultiplierLabelWidth));
        row.Add(BuildRowLabel(angle.ToString("0.#") + "°", MultiplierAngleWidth));
        row.Add(CreatePercentField(maxSpeedProperty, MaxSpeedMultiplierColor, maxSpeedTooltip, string.Empty));
        row.Add(CreatePercentField(accelerationProperty, AccelerationMultiplierColor, accelerationTooltip, string.Empty));
        return row;
    }

    /// <summary>
    /// Builds one arc multiplier row.
    /// </summary>
    /// <param name="index">Zero-based arc index.</param>
    /// <param name="range">Displayed angle range.</param>
    /// <param name="maxSpeedProperty">Serialized max-speed multiplier property.</param>
    /// <param name="accelerationProperty">Serialized acceleration multiplier property.</param>
    /// <param name="maxSpeedTooltip">Tooltip for the max-speed field.</param>
    /// <param name="accelerationTooltip">Tooltip for the acceleration field.</param>
    /// <returns>Returns the constructed arc row.</returns>
    private static VisualElement BuildArcRow(int index,
                                             string range,
                                             SerializedProperty maxSpeedProperty,
                                             SerializedProperty accelerationProperty,
                                             string maxSpeedTooltip,
                                             string accelerationTooltip)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;
        row.style.marginBottom = MultiplierRowSpacing;
        row.Add(BuildRowLabel("Arc " + (index + 1), MultiplierLabelWidth));
        row.Add(BuildRowLabel(range, MultiplierRangeWidth));
        row.Add(CreatePercentField(maxSpeedProperty, MaxSpeedMultiplierColor, maxSpeedTooltip, string.Empty));
        row.Add(CreatePercentField(accelerationProperty, AccelerationMultiplierColor, accelerationTooltip, string.Empty));
        return row;
    }

    /// <summary>
    /// Builds one fixed-width header label used by the multipliers table.
    /// </summary>
    /// <param name="text">Header text.</param>
    /// <param name="width">Fixed label width.</param>
    /// <returns>Returns the configured header label.</returns>
    private static Label BuildHeaderLabel(string text, float width)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.maxWidth = width;
        label.style.flexGrow = 0f;
        label.style.flexShrink = 0f;
        label.style.fontSize = 10f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    /// <summary>
    /// Builds one fixed-width row label used by the multipliers table.
    /// </summary>
    /// <param name="text">Visible row text.</param>
    /// <param name="width">Fixed label width.</param>
    /// <returns>Returns the configured row label.</returns>
    private static Label BuildRowLabel(string text, float width)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.maxWidth = width;
        label.style.flexGrow = 0f;
        label.style.flexShrink = 0f;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    /// <summary>
    /// Formats an angular range and annotates wraps across the 360-degree seam.
    /// </summary>
    /// <param name="startAngle">Range start in degrees.</param>
    /// <param name="endAngle">Range end in degrees.</param>
    /// <returns>Returns the formatted range string.</returns>
    private static string FormatAngleRange(float startAngle, float endAngle)
    {
        float normalizedStart = Mathf.Repeat(startAngle, 360f);
        float normalizedEnd = Mathf.Repeat(endAngle, 360f);
        string startText = normalizedStart.ToString("0.#") + "°";
        string endText = normalizedEnd.ToString("0.#") + "°";

        if (normalizedEnd < normalizedStart)
            return startText + " - " + endText + " (wrap)";

        return startText + " - " + endText;
    }

    /// <summary>
    /// Builds the directional label descriptors used by movement and look pie charts.
    /// </summary>
    /// <param name="count">Discrete direction count.</param>
    /// <param name="offset">Current direction offset in degrees.</param>
    /// <param name="prefix">Prefix displayed before the direction index.</param>
    /// <returns>Returns the list of configured label descriptors.</returns>
    private static List<PieChartElement.LabelDescriptor> BuildDirectionalLabels(int count, float offset, string prefix)
    {
        int sliceCount = Mathf.Max(1, count);
        float step = 360f / sliceCount;
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        for (int index = 0; index < sliceCount; index++)
        {
            PieChartElement.LabelDescriptor descriptor = new PieChartElement.LabelDescriptor();
            descriptor.Angle = Mathf.Repeat(offset + (index * step), 360f);
            descriptor.Text = prefix + " " + (index + 1);
            descriptor.RadiusOffset = 0f;
            descriptor.TextColor = DirectionLabelColor;
            descriptor.UseTextColor = true;
            labels.Add(descriptor);
        }

        return labels;
    }

    /// <summary>
    /// Adds one cone slice when enabled by the associated serialized properties.
    /// </summary>
    /// <param name="slices">Target slice collection.</param>
    /// <param name="centerAngle">Cone center angle in degrees.</param>
    /// <param name="enabledProperty">Serialized cone enabled flag.</param>
    /// <param name="angleProperty">Serialized cone angle property.</param>
    /// <param name="color">Cone slice color.</param>

    private static void AddConeSlices(List<PieChartElement.PieSlice> slices,
                                      float centerAngle,
                                      SerializedProperty enabledProperty,
                                      SerializedProperty angleProperty,
                                      Color color)
    {
        if (enabledProperty == null || angleProperty == null)
            return;

        if (!enabledProperty.boolValue)
            return;

        float angle = Mathf.Clamp(angleProperty.floatValue, 1f, 360f);
        float half = angle * 0.5f;
        AddSlice(slices, centerAngle - half, centerAngle + half, color);
    }

    /// <summary>
    /// Adds one pie slice that spans the requested angular step.
    /// </summary>
    /// <param name="slices">Target slice collection.</param>
    /// <param name="startAngle">Slice start angle in degrees.</param>
    /// <param name="step">Slice step in degrees.</param>
    /// <param name="color">Slice color.</param>

    private static void AddSliceByStep(List<PieChartElement.PieSlice> slices, float startAngle, float step, Color color)
    {
        if (step >= 359.99f)
        {
            AddSlice(slices, 0f, 360f, color);
            return;
        }

        AddSlice(slices, startAngle, startAngle + step, color);
    }

    /// <summary>
    /// Adds one pie slice with the provided start and end angles.
    /// </summary>
    /// <param name="slices">Target slice collection.</param>
    /// <param name="startAngle">Slice start angle in degrees.</param>
    /// <param name="endAngle">Slice end angle in degrees.</param>
    /// <param name="color">Slice color.</param>

    private static void AddSlice(List<PieChartElement.PieSlice> slices, float startAngle, float endAngle, Color color)
    {
        PieChartElement.PieSlice slice = new PieChartElement.PieSlice();
        slice.StartAngle = startAngle;
        slice.EndAngle = endAngle;
        slice.MidAngle = startAngle + ((endAngle - startAngle) * 0.5f);
        slice.Color = color;
        slices.Add(slice);
    }

    /// <summary>
    /// Ensures that one serialized float array matches the requested size, initializing new entries to one.
    /// </summary>
    /// <param name="panel">Owning panel used for undo and serialized object updates.</param>
    /// <param name="arrayProperty">Serialized array property to resize.</param>
    /// <param name="size">Requested array size.</param>

    private static void EnsureArraySize(PlayerControllerPresetsPanel panel, SerializedProperty arrayProperty, int size)
    {
        if (panel == null || arrayProperty == null || arrayProperty.arraySize == size)
            return;

        if (panel.SelectedPreset != null)
        {
            Undo.RecordObject(panel.SelectedPreset, "Resize Direction Multipliers");
            panel.PresetSerializedObject.Update();
        }

        if (arrayProperty.arraySize < size)
        {
            int startIndex = arrayProperty.arraySize;
            arrayProperty.arraySize = size;

            for (int index = startIndex; index < size; index++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(index);

                if (element != null)
                    element.floatValue = 1f;
            }

            panel.PresetSerializedObject.ApplyModifiedProperties();
            return;
        }

        arrayProperty.arraySize = size;
        panel.PresetSerializedObject.ApplyModifiedProperties();
    }
    #endregion

    #endregion
}
