using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds Player Management Tool controls for the player world-space hit-box shadow.
/// </summary>
internal static class PlayerVisualPresetsPanelGroundShadowSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Ground Shadow subsection for the active Player Visual Preset.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured Ground Shadow subsection.</returns>
    public static VisualElement Build(PlayerVisualPresetsPanel panel)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout("Ground Shadow",
                                                                          "NashCore.PlayerManagement.Visual.GroundShadow",
                                                                          true);
        foldout.tooltip = "Controls the player hit-box shadow rendered under the player without health or shield rings.";

        if (panel == null || panel.PresetSerializedObject == null)
            return foldout;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settingsProperty = serializedObject.FindProperty("groundShadow");
        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");

        if (settingsProperty == null)
        {
            foldout.Add(new HelpBox("Ground Shadow settings are missing from the selected Visual Preset.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty enabledProperty = settingsProperty.FindPropertyRelative("enabled");
        SerializedProperty projectionModeProperty = settingsProperty.FindPropertyRelative("projectionMode");
        VisualElement details = new VisualElement();
        VisualElement projectionDistanceContainer = new VisualElement();
        VisualElement warnings = new VisualElement();

        AddScalableField(foldout,
                         enabledProperty,
                         scalingRulesProperty,
                         "Enabled",
                         "When enabled, the player renders a world-space shadow that identifies the authored hit-box footprint.");
        AddScalableField(details,
                         settingsProperty.FindPropertyRelative("hitAreaMultiplier"),
                         scalingRulesProperty,
                         "Hit Area Multiplier",
                         "Multiplier applied to the player's automatic hit-area radius (the circle inside which enemy fire can hit the player) to size the shadow disc. Use 1 to match the hit area exactly.");
        AddScalableField(details,
                         settingsProperty.FindPropertyRelative("positionOffsetXZ"),
                         scalingRulesProperty,
                         "Position Offset XZ",
                         "Local root-space XZ offset applied from the player pivot to the represented hit-box center.");
        AddScalableField(details,
                         settingsProperty.FindPropertyRelative("heightOffset"),
                         scalingRulesProperty,
                         "Height Offset",
                         "Vertical clearance applied above the raised quad or projected ground point to avoid z-fighting.");
        AddScalableField(details,
                         projectionModeProperty,
                         scalingRulesProperty,
                         "Projection Mode",
                         "Controls whether the player shadow uses a raised quad or ray-projects onto the ground surface below the hit-box center.");
        AddScalableField(projectionDistanceContainer,
                         settingsProperty.FindPropertyRelative("projectionMaxDistance"),
                         scalingRulesProperty,
                         "Projection Max Distance",
                         "Maximum downward distance in meters used when Projection Mode is Project Onto Ground. If no ground is found within this distance, the raised quad fallback is used.");
        AddScalableField(details,
                         settingsProperty.FindPropertyRelative("shadowColor"),
                         scalingRulesProperty,
                         "Shadow Color",
                         "Tint applied to the player hit-box shadow disc. Alpha controls overall shadow strength on top of Shadow Alpha.");
        AddScalableField(details,
                         settingsProperty.FindPropertyRelative("shadowAlpha"),
                         scalingRulesProperty,
                         "Shadow Alpha",
                         "Final opacity multiplier applied to the player shadow disc on top of Shadow Color alpha.");
        AddScalableField(details,
                         settingsProperty.FindPropertyRelative("shadowEdgeSoftness"),
                         scalingRulesProperty,
                         "Shadow Edge Softness",
                         "Normalized falloff width applied at the outer edge of the player shadow disc.");
        details.Add(projectionDistanceContainer);
        foldout.Add(details);
        foldout.Add(warnings);

        Refresh();
        TrackRefresh(foldout, enabledProperty, Refresh);
        TrackRefresh(foldout, projectionModeProperty, Refresh);
        return foldout;

        void Refresh()
        {
            bool enabled = enabledProperty == null || enabledProperty.boolValue;
            bool projectOntoGround = projectionModeProperty != null &&
                                     projectionModeProperty.enumValueIndex == (int)GroundShadowProjectionMode.ProjectOntoGround;
            details.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            projectionDistanceContainer.style.display = projectOntoGround ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(settingsProperty, warnings, enabled, projectOntoGround);
        }
    }
    #endregion

    #region Field Construction
    /// <summary>
    /// Adds one unified-formula Add Scaling field to the subsection.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized target property.</param>
    /// <param name="scalingRulesProperty">Visual preset Add Scaling rule list.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="tooltip">Field behavior description.</param>
    private static void AddScalableField(VisualElement parent,
                                         SerializedProperty property,
                                         SerializedProperty scalingRulesProperty,
                                         string label,
                                         string tooltip)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label);
        field.tooltip = tooltip;
        parent.Add(field);
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Registers an inexpensive serialized-property tracker that refreshes conditional controls.
    /// </summary>
    /// <param name="root">Element owning the property tracker.</param>
    /// <param name="property">Property whose changes trigger a refresh.</param>
    /// <param name="refresh">Refresh callback.</param>
    private static void TrackRefresh(VisualElement root,
                                     SerializedProperty property,
                                     System.Action refresh)
    {
        if (root == null || property == null || refresh == null)
            return;

        root.TrackPropertyValue(property, changedProperty => refresh());
    }

    /// <summary>
    /// Rebuilds Ground Shadow authoring warnings without modifying serialized values.
    /// </summary>
    /// <param name="settingsProperty">Serialized Ground Shadow settings.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="enabled">True when authored controls currently affect runtime presentation.</param>
    /// <param name="projectOntoGround">True when projection distance affects runtime presentation.</param>
    private static void RefreshWarnings(SerializedProperty settingsProperty,
                                        VisualElement warnings,
                                        bool enabled,
                                        bool projectOntoGround)
    {
        if (warnings == null)
            return;

        warnings.Clear();

        if (settingsProperty == null || !enabled)
            return;

        AddNonPositiveWarning(settingsProperty, warnings, "hitAreaMultiplier", "Hit Area Multiplier should be greater than zero.");
        AddInvalidVector2Warning(settingsProperty, warnings, "positionOffsetXZ", "Position Offset XZ contains invalid numeric values.");
        AddInvalidFloatWarning(settingsProperty, warnings, "heightOffset", "Height Offset should be finite.");
        AddInvalidEnumWarning(settingsProperty, warnings, "projectionMode", "Projection Mode has an unsupported value.");
        AddRangeWarning(settingsProperty, warnings, "shadowAlpha", 0f, 1f, "Shadow Alpha should stay between 0 and 1.");
        AddRangeWarning(settingsProperty, warnings, "shadowEdgeSoftness", 0f, 1f, "Shadow Edge Softness should stay between 0 and 1.");

        if (projectOntoGround)
            AddNegativeWarning(settingsProperty, warnings, "projectionMaxDistance", "Projection Max Distance must be zero or positive.");
    }

    /// <summary>
    /// Adds a warning when one float property is non-finite or non-positive.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddNonPositiveWarning(SerializedProperty parentProperty,
                                              VisualElement warnings,
                                              string relativePropertyName,
                                              string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && (!IsFinite(property.floatValue) || property.floatValue <= 0f))
            warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when one float property is non-finite or negative.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddNegativeWarning(SerializedProperty parentProperty,
                                           VisualElement warnings,
                                           string relativePropertyName,
                                           string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && (!IsFinite(property.floatValue) || property.floatValue < 0f))
            warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when one float property is non-finite or outside the expected range.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="minimum">Expected inclusive minimum.</param>
    /// <param name="maximum">Expected inclusive maximum.</param>
    /// <param name="message">Warning text.</param>
    private static void AddRangeWarning(SerializedProperty parentProperty,
                                        VisualElement warnings,
                                        string relativePropertyName,
                                        float minimum,
                                        float maximum,
                                        string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null &&
            (!IsFinite(property.floatValue) ||
             property.floatValue < minimum ||
             property.floatValue > maximum))
        {
            warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Adds a warning when an enum property contains an unsupported serialized value.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative enum property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddInvalidEnumWarning(SerializedProperty parentProperty,
                                              VisualElement warnings,
                                              string relativePropertyName,
                                              string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null || property.propertyType != SerializedPropertyType.Enum)
            return;

        if (property.enumValueIndex >= 0 &&
            property.enumValueIndex <= (int)GroundShadowProjectionMode.ProjectOntoGround)
        {
            return;
        }

        warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when a Vector2 property contains NaN or Infinity components.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative Vector2 property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddInvalidVector2Warning(SerializedProperty parentProperty,
                                                 VisualElement warnings,
                                                 string relativePropertyName,
                                                 string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null)
            return;

        Vector2 value = property.vector2Value;

        if (IsFinite(value.x) && IsFinite(value.y))
            return;

        warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Adds a warning when one float property is non-finite.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddInvalidFloatWarning(SerializedProperty parentProperty,
                                               VisualElement warnings,
                                               string relativePropertyName,
                                               string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && !IsFinite(property.floatValue))
            warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Returns whether a float can be safely used by runtime ECS presentation.
    /// </summary>
    /// <param name="value">Float value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
