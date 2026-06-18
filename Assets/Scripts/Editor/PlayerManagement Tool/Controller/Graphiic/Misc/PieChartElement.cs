using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Represents a visual element for displaying a pie chart with configurable slices, direction markers, and overlay
/// fields.
/// </summary>
public sealed class PieChartElement : VisualElement
{
    #region Constants
    private const float FieldWidth = 46f;
    private const float FieldHeight = 16f;
    private const float ForwardLabelSize = 14f;
    private const float ForwardLabelPadding = 2f;
    private const float DirectionLineWidth = 2f;
    private const float DirectionLineRadiusFactor = 0.5f;
    private const float PieRadiusFactor = 0.48f;
    private const float OverlayRadiusFactor = 0.34f;
    private const float LabelWidth = 52f;
    private const float LabelHeight = 14f;
    private const float LabelFontSize = 9f;
    private const float DirectionLabelFontSize = 24f;
    private const float LabelRadiusFactor = 0.55f;
    private const float LabelRadiusFactorMultiplier = 1.3f;
    private const float BaseMinSize = 220f;
    private const float OverlayGroupPadding = 6f;
    private const float OverlayAngleKeyScale = 1000f;
    #endregion

    #region Fields
    // Internal collections and state for managing pie slices, overlay bindings, and direction markers.
    private readonly List<PieSlice> m_Slices = new List<PieSlice>();
    private readonly List<OverlayBinding> m_OverlayBindings = new List<OverlayBinding>();
    private readonly List<float> m_DirectionAngles = new List<float>();
    private readonly List<LabelBinding> m_LabelBindings = new List<LabelBinding>();
    private readonly VisualElement m_OverlayRoot;
    private readonly VisualElement m_FieldRoot;
    private readonly VisualElement m_LabelRoot;
    private readonly Label m_ForwardLabel;
    private Color m_DirectionColor = new Color(1f, 1f, 1f, 0.9f);
    private Color m_ForwardColor = new Color(1f, 0.9f, 0.2f, 1f);
    private Color m_LabelColor = new Color(1f, 1f, 1f, 0.85f);
    private float m_ForwardAngle;
    private bool m_ShowForwardLabel;
    private float pieZoom = 1f;
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the PieChartElement class, setting up layout, overlay elements, and event
    /// handlers.
    /// </summary>
    public PieChartElement()
    {
        // style
        style.flexGrow = 1f;
        style.minHeight = BaseMinSize;
        style.minWidth = BaseMinSize;
        style.marginTop = 12f;
        style.marginBottom = 6f;

        // overlay root
        m_OverlayRoot = new VisualElement();
        m_OverlayRoot.style.position = Position.Absolute;
        m_OverlayRoot.style.left = 0f;
        m_OverlayRoot.style.top = 0f;
        m_OverlayRoot.style.right = 0f;
        m_OverlayRoot.style.bottom = 0f;

        // field root
        m_FieldRoot = new VisualElement();
        m_FieldRoot.style.position = Position.Absolute;
        m_FieldRoot.style.left = 0f;
        m_FieldRoot.style.top = 0f;
        m_FieldRoot.style.right = 0f;
        m_FieldRoot.style.bottom = 0f;
        m_FieldRoot.pickingMode = PickingMode.Ignore;
        m_OverlayRoot.Add(m_FieldRoot);

        // label root
        m_LabelRoot = new VisualElement();
        m_LabelRoot.style.position = Position.Absolute;
        m_LabelRoot.style.left = 0f;
        m_LabelRoot.style.top = 0f;
        m_LabelRoot.style.right = 0f;
        m_LabelRoot.style.bottom = 0f;
        m_LabelRoot.pickingMode = PickingMode.Ignore;
        m_OverlayRoot.Add(m_LabelRoot);

        // forward label
        m_ForwardLabel = new Label("F");
        m_ForwardLabel.style.position = Position.Absolute;
        m_ForwardLabel.style.width = ForwardLabelSize;
        m_ForwardLabel.style.height = ForwardLabelSize;
        m_ForwardLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        m_ForwardLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        m_ForwardLabel.style.fontSize = 10f;
        m_ForwardLabel.style.display = DisplayStyle.None;
        m_ForwardLabel.pickingMode = PickingMode.Ignore;
        m_OverlayRoot.Add(m_ForwardLabel);

        // add overlay root
        Add(m_OverlayRoot);

        // events
        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Replaces the current collection of pie slices with the specified list and updates the layout.
    /// </summary>
    /// <param name="slices">The new list of PieSlice objects to set.</param>
    public void SetSlices(List<PieSlice> slices)
    {
        m_Slices.Clear();

        if (slices != null)
            m_Slices.AddRange(slices);

        MarkDirtyRepaint();
        UpdateOverlayLayout();
    }

    /// <summary>
    /// Configures overlay fields based on the provided list of overlay descriptors.
    /// </summary>
    /// <param name="descriptors">A list of OverlayDescriptor objects used to create and bind overlay fields.</param>
    public void SetOverlayFields(List<OverlayDescriptor> descriptors)
    {
        m_FieldRoot.Clear();
        m_OverlayBindings.Clear();

        if (descriptors == null)
        {
            return;
        }

        for (int i = 0; i < descriptors.Count; i++)
        {
            OverlayDescriptor descriptor = descriptors[i];

            if (descriptor == null)
                continue;

            FloatField field = new FloatField();
            field.label = string.Empty;
            field.style.position = Position.Absolute;
            field.style.width = FieldWidth;
            field.style.height = FieldHeight;
            field.style.fontSize = 10f;
            field.tooltip = descriptor.Tooltip;
            field.isDelayed = true;
            field.style.unityTextAlign = TextAnchor.MiddleCenter;

            if (descriptor.UseFieldColor)
            {
                field.style.color = descriptor.FieldColor;
            }

            if (descriptor.Property != null)
            {
                float value = descriptor.Property.floatValue;
                field.value = descriptor.DisplayAsPercent ? value * 100f : value;

                field.RegisterValueChangedCallback(evt =>
                {
                    if (descriptor.Property == null)
                    {
                        return;
                    }

                    float inputValue = evt.newValue;
                    float storedValue = descriptor.DisplayAsPercent ? Mathf.Clamp(inputValue / 100f, 0f, 1f) : inputValue;
                    descriptor.Property.floatValue = storedValue;
                    descriptor.Property.serializedObject.ApplyModifiedProperties();

                    if (descriptor.DisplayAsPercent)
                    {
                        field.SetValueWithoutNotify(storedValue * 100f);
                    }
                });
            }

            OverlayBinding binding = new OverlayBinding
            {
                Angle = descriptor.Angle,
                Field = field,
                Property = descriptor.Property,
                RadiusOffset = descriptor.RadiusOffset,
                DisplayAsPercent = descriptor.DisplayAsPercent
            };

            m_OverlayBindings.Add(binding);
            m_FieldRoot.Add(field);
        }

        UpdateOverlayLayout();
    }

    /// <summary>
    /// Configures text labels displayed on the pie chart based on the provided descriptors.
    /// </summary>
    /// <param name="descriptors">A list of LabelDescriptor objects used to create and position labels.</param>
    public void SetSegmentLabels(List<LabelDescriptor> descriptors)
    {
        m_LabelRoot.Clear();
        m_LabelBindings.Clear();

        if (descriptors == null)
        {
            return;
        }

        for (int i = 0; i < descriptors.Count; i++)
        {
            LabelDescriptor descriptor = descriptors[i];

            if (descriptor == null)
            {
                continue;
            }

            Label label = new Label(descriptor.Text);
            label.style.position = Position.Absolute;
            label.style.width = LabelWidth;
            label.style.height = LabelHeight;
            label.style.fontSize = LabelFontSize;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.pickingMode = PickingMode.Ignore;

            if (descriptor.UseTextColor)
            {
                label.style.color = descriptor.TextColor;
            }
            else
            {
                label.style.color = m_LabelColor;
            }

            LabelBinding binding = new LabelBinding
            {
                Angle = descriptor.Angle,
                Label = label,
                RadiusOffset = descriptor.RadiusOffset
            };

            m_LabelBindings.Add(binding);
            m_LabelRoot.Add(label);
        }

        UpdateOverlayLayout();
    }

    /// <summary>
    /// Configures the direction markers with specified angles, colors, forward angle, and label visibility.
    /// </summary>
    /// <param name="directionAngles">A list of angles representing the directions to display.</param>
    /// <param name="directionColor">The color used for the direction markers.</param>
    /// <param name="forwardColor">The color used for the forward direction marker and label.</param>
    /// <param name="forwardAngle">The angle representing the forward direction.</param>
    /// <param name="showForwardLabel">Indicates whether the forward label should be shown.</param>
    public void SetDirectionMarkers(List<float> directionAngles, Color directionColor, Color forwardColor, float forwardAngle, bool showForwardLabel)
    {
        m_DirectionAngles.Clear();

        if (directionAngles != null)
            m_DirectionAngles.AddRange(directionAngles);

        m_DirectionColor = directionColor;
        m_ForwardColor = forwardColor;
        m_ForwardAngle = forwardAngle;
        m_ShowForwardLabel = showForwardLabel;

        if (m_ForwardLabel != null)
            m_ForwardLabel.style.color = m_ForwardColor;

        UpdateForwardLabelVisibility();
        MarkDirtyRepaint();
        UpdateOverlayLayout();
    }

    /// <summary>
    /// Sets the zoom factor for the pie chart and overlay layout.
    /// </summary>
    /// <param name="zoom">Zoom multiplier applied to the pie and overlay radii.</param>
    public void SetZoom(float zoom)
    {
        pieZoom = Mathf.Max(0.1f, zoom);
        UpdateSizeForZoom();
        MarkDirtyRepaint();
        UpdateOverlayLayout();
    }
    #endregion

    #region VisualElement Rendering
    /// <summary>
    /// Renders pie slices and directional lines within the visual element using the provided mesh generation context.
    /// </summary>
    /// <param name="mgc">The mesh generation context used for drawing visual content.</param>
    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        // early out
        Rect rect = contentRect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        // Center and radius
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * PieRadiusFactor;

        // Painter
        Painter2D painter = mgc.painter2D;
        painter.lineWidth = 1f;

        //for each slice in the pie, draw the corresponding arc segment with fill and stroke
        for (int i = 0; i < m_Slices.Count; i++)
        {
            PieSlice slice = m_Slices[i];
            painter.fillColor = slice.Color;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.35f);

            float delta = slice.EndAngle - slice.StartAngle;

            if (delta <= 0f)
            {
                continue;
            }

            float startAngle = GetPainterStartAngle(slice.StartAngle);
            float endAngle = startAngle + delta;

            painter.BeginPath();
            painter.MoveTo(center);
            painter.Arc(center, radius, startAngle, endAngle);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        painter.strokeColor = new Color(0f, 0f, 0f, 0.45f);
        painter.BeginPath();
        painter.Arc(center, radius, 0f, 360f);
        painter.ClosePath();
        painter.Stroke();

        if (m_DirectionAngles.Count > 0)
        {
            painter.lineWidth = DirectionLineWidth;

            for (int i = 0; i < m_DirectionAngles.Count; i++)
            {
                float angle = m_DirectionAngles[i];
                Vector2 dir = GetDirectionVector(angle);
                Vector2 end = center + (dir * radius * DirectionLineRadiusFactor);

                painter.strokeColor = IsForwardAngle(angle) ? m_ForwardColor : m_DirectionColor;
                painter.BeginPath();
                painter.MoveTo(center);
                painter.LineTo(end);
                painter.Stroke();
            }
        }
    }
    #endregion

    #region Layout Methods
    /// <summary>
    /// Handles geometry change events by updating the overlay layout.
    /// </summary>
    /// <param name="evt">The geometry changed event data.</param>
    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        UpdateOverlayLayout();
    }

    #region Update Layout
    /// <summary>
    /// Arranges overlay fields in a circular layout around the content rectangle and updates their positions and
    /// values.
    /// </summary>
    private void UpdateOverlayLayout()
    {
        Rect rect = contentRect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * OverlayRadiusFactor;
        float pieRadius = Mathf.Min(rect.width, rect.height) * PieRadiusFactor;

        if (m_OverlayBindings.Count > 0)
        {
            Dictionary<int, List<OverlayBinding>> groupedBindings = new Dictionary<int, List<OverlayBinding>>();

            for (int i = 0; i < m_OverlayBindings.Count; i++)
            {
                OverlayBinding binding = m_OverlayBindings[i];
                int key = GetAngleKey(binding.Angle);
                List<OverlayBinding> group;

                if (groupedBindings.TryGetValue(key, out group) == false)
                {
                    group = new List<OverlayBinding>();
                    groupedBindings.Add(key, group);
                }

                group.Add(binding);
            }

            foreach (KeyValuePair<int, List<OverlayBinding>> pair in groupedBindings)
            {
                List<OverlayBinding> group = pair.Value;

                if (group == null || group.Count == 0)
                {
                    continue;
                }

                group.Sort(CompareBindingsByRadiusOffset);

                float angle = group[0].Angle;
                Vector2 dir = GetDirectionVector(angle);
                Vector2 tangent = new Vector2(-dir.y, dir.x);
                float spacing = GetOverlayGroupSpacing(tangent);
                float startOffset = -0.5f * spacing * (group.Count - 1);

                for (int i = 0; i < group.Count; i++)
                {
                    OverlayBinding binding = group[i];
                    FloatField field = binding.Field;

                    if (field == null)
                    {
                        continue;
                    }

                    float adjustedRadius = radius + binding.RadiusOffset;
                    float tangentialOffset = startOffset + (i * spacing);
                    Vector2 position = center + (dir * adjustedRadius) + (tangent * tangentialOffset);

                    field.style.left = position.x - (FieldWidth * 0.5f);
                    field.style.top = position.y - (FieldHeight * 0.5f);

                    if (binding.Property != null)
                    {
                        float value = binding.Property.floatValue;
                        field.SetValueWithoutNotify(binding.DisplayAsPercent ? value * 100f : value);
                    }
                }
            }
        }

        UpdateForwardLabelPosition(center, pieRadius);
        UpdateLabelLayout(center, pieRadius);
    }

    /// <summary>
    /// Updates the visibility of the forward label based on the m_ShowForwardLabel flag.
    /// </summary>
    private void UpdateForwardLabelVisibility()
    {
        if (m_ForwardLabel == null)
            return;

        m_ForwardLabel.style.display = m_ShowForwardLabel ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Positions the forward label at a calculated point around a center based on a specified radius and angle.
    /// </summary>
    /// <param name="center">The center point from which to position the forward label.</param>
    /// <param name="radius">The radius distance from the center to place the forward label.</param>
    private void UpdateForwardLabelPosition(Vector2 center, float radius)
    {
        if (m_ShowForwardLabel == false || m_ForwardLabel == null)
        {
            return;
        }

        float radians = (m_ForwardAngle - 90f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        float labelRadius = radius + (ForwardLabelSize * 0.5f) + ForwardLabelPadding;
        Vector2 position = center + (dir * labelRadius);

        m_ForwardLabel.style.left = position.x - (ForwardLabelSize * 0.5f);
        m_ForwardLabel.style.top = position.y - (ForwardLabelSize * 0.5f);
    }

    /// <summary>
    /// Positions pie chart labels around the specified center point using the given pie radius.
    /// </summary>
    /// <param name="center">The center point of the pie chart.</param>
    /// <param name="pieRadius">The radius of the pie chart.</param>
    private void UpdateLabelLayout(Vector2 center, float pieRadius)
    {
        if (m_LabelBindings.Count == 0)
        {
            return;
        }

        float labelRadius = pieRadius * LabelRadiusFactor * LabelRadiusFactorMultiplier;

        // for each label binding, calculate its position based on its angle and radius offset,
        // and update its style
        for (int i = 0; i < m_LabelBindings.Count; i++)
        {
            LabelBinding binding = m_LabelBindings[i];
            Label label = binding.Label;

            if (label == null)
            {
                continue;
            }

            Vector2 dir = GetDirectionVector(binding.Angle);
            Vector2 position = center + (dir * (labelRadius + binding.RadiusOffset));



            label.style.left = position.x - (LabelWidth * 0.5f);
            label.style.top = position.y - (LabelHeight * 0.5f);
            label.style.fontSize = DirectionLabelFontSize * pieZoom;
        }
    }

    /// <summary>
    /// Adjusts the minimum width and height of the style based on the current zoom level.
    /// </summary>
    private void UpdateSizeForZoom()
    {
        float scaledSize = BaseMinSize * pieZoom;
        style.minHeight = scaledSize;
        style.minWidth = scaledSize;
    }
    #endregion

    /// <summary>
    /// Determines whether the specified angle is within 0.5 degrees of the forward angle.
    /// </summary>
    /// <param name="angle">The angle to compare, in degrees.</param>
    /// <returns>True if the angle is within 0.5 degrees of the forward angle; otherwise, false.</returns>
    private bool IsForwardAngle(float angle)
    {
        return Mathf.Abs(Mathf.DeltaAngle(angle, m_ForwardAngle)) < 0.5f;
    }


    #region Get
    /// <summary>
    /// Calculates a scaled integer key based on the normalized angle value.
    /// </summary>
    /// <param name="angle">The angle in degrees to be normalized and converted.</param>
    /// <returns>An integer key representing the scaled normalized angle.</returns>
    private int GetAngleKey(float angle)
    {
        float normalized = Mathf.Repeat(angle, 360f);
        return Mathf.RoundToInt(normalized * OverlayAngleKeyScale);
    }

    /// <summary>
    /// Calculates a 2D unit vector representing the direction of the given angle in degrees.
    /// </summary>
    /// <param name="angle">Angle in degrees to convert to a direction vector.</param>
    /// <returns>A Vector2 representing the direction of the specified angle.</returns>
    private Vector2 GetDirectionVector(float angle)
    {
        float radians = (angle - 90f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    /// <summary>
    /// Calculates the normalized painter start angle by subtracting 90 degrees from the input angle and wrapping it
    /// within the 0 to 360 degree range.
    /// </summary>
    /// <param name="angle">The original angle in degrees.</param>
    /// <returns>The adjusted and wrapped angle in degrees.</returns>
    private float GetPainterStartAngle(float angle)
    {
        float adjusted = angle - 90f;
        return Mathf.Repeat(adjusted, 360f);
    }

    /// <summary>
    /// Calculates the spacing required for an overlay group based on the field dimensions and the provided tangent
    /// vector.
    /// </summary>
    /// <param name="tangent">The tangent vector used to determine the projection direction for spacing calculation.</param>
    /// <returns>The computed overlay group spacing as a float value.</returns>
    private float GetOverlayGroupSpacing(Vector2 tangent)
    {
        float halfWidth = FieldWidth * 0.5f;
        float halfHeight = FieldHeight * 0.5f;
        float projectedHalf = (halfWidth * Mathf.Abs(tangent.x)) + (halfHeight * Mathf.Abs(tangent.y));
        return (projectedHalf * 2f) + OverlayGroupPadding;
    }
    #endregion

    /// <summary>
    /// Compares two OverlayBinding instances based on their RadiusOffset values for sorting purposes.
    /// </summary>
    /// <param name="left">The first OverlayBinding to compare.</param>
    /// <param name="right">The second OverlayBinding to compare.</param>
    /// <returns>A signed integer indicating the relative order of the bindings by RadiusOffset.</returns>
    private static int CompareBindingsByRadiusOffset(OverlayBinding left, OverlayBinding right)
    {
        // Handle null cases to ensure consistent sorting when radius offsets are not defined.
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        // Compare the RadiusOffset values of the two bindings to determine their order in sorting.
        return left.RadiusOffset.CompareTo(right.RadiusOffset);
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Represents a slice of a pie chart, defined by its start angle, end angle, midpoint angle, and color.
    /// </summary>
    public struct PieSlice
    {
        public float StartAngle;
        public float EndAngle;
        public float MidAngle;
        public Color Color;
    }

    /// <summary>
    /// Represents a descriptor for an overlay, containing angle, property, and tooltip information.
    /// </summary>
    public sealed class OverlayDescriptor
    {
        public float Angle;
        public SerializedProperty Property;
        public string Tooltip;
        public float RadiusOffset;
        public Color FieldColor;
        public bool UseFieldColor;
        public bool DisplayAsPercent;
    }

    /// <summary>
    /// Represents a descriptor for a text label displayed at a specific angle around the pie chart.
    /// </summary>
    public sealed class LabelDescriptor
    {
        public float Angle;
        public string Text;
        public float RadiusOffset;
        public Color TextColor;
        public bool UseTextColor;
    }

    /// <summary>
    /// Represents a binding between a float angle, a FloatField, and a SerializedProperty for overlay operations.
    /// </summary>
    private sealed class OverlayBinding
    {
        public float Angle;
        public FloatField Field;
        public SerializedProperty Property;
        public float RadiusOffset;
        public bool DisplayAsPercent;
    }

    /// <summary>
    /// Represents a binding between a float angle and a Label for display around the pie chart.
    /// </summary>
    private sealed class LabelBinding
    {
        public float Angle;
        public Label Label;
        public float RadiusOffset;
    }
    #endregion
}
