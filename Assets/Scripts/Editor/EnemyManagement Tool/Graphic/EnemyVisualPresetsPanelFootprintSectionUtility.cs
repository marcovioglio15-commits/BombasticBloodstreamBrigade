using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Footprint UI subsection used by the enemy visual presets management tool.
/// Controls cover the shader-driven shadow disc plus the two concentric fillable rings or arcs,
/// including per-enemy ring colors and ring-only knobs that hide automatically when Boss UI suppresses them.
/// </summary>
internal static class EnemyVisualPresetsPanelFootprintSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the footprint subsection content for the active enemy visual preset.
    /// </summary>
    /// <param name="panel">Owning visual preset panel exposing the serialized preset and refresh hooks.</param>
    /// <returns>Subsection container with all footprint controls.</returns>
    public static VisualElement BuildFootprintSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty footprintProperty = panel.PresetSerializedObject.FindProperty("footprint");
        VisualElement container = EnemyVisualPresetsPanelSectionsUtility.CreateSubSectionContainer("Footprint UI");

        if (footprintProperty == null)
            return container;

        bool ringsSuppressed = ResolveRingsSuppressedByBossUi(panel);
        BuildShadowControls(panel, container, footprintProperty);

        if (ringsSuppressed)
        {
            container.Add(new HelpBox("Ring controls are hidden because Boss UI is enabled on this preset. The screen-space boss HUD owns health and shield bars, so the world-space rings are suppressed at runtime. Shadow controls above still apply.",
                                       HelpBoxMessageType.Info));
        }
        else
        {
            BuildRingControls(panel, container, footprintProperty);
        }

        AddFootprintWarnings(footprintProperty, container, ringsSuppressed);
        return container;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves whether the ring controls should be hidden because the Boss UI is enabled on the
    /// currently edited preset. Mirrors the runtime suppress gate evaluated by the baker.
    /// </summary>
    /// <param name="panel">Visual preset panel exposing the serialized preset.</param>
    /// <returns>True when ring controls should be hidden from the subsection.</returns>
    private static bool ResolveRingsSuppressedByBossUi(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty bossUiProperty = panel.PresetSerializedObject.FindProperty("bossUi");

        if (bossUiProperty == null)
            return false;

        SerializedProperty enabledProperty = bossUiProperty.FindPropertyRelative("enabled");
        SerializedProperty showHealthBarProperty = bossUiProperty.FindPropertyRelative("showHealthBar");

        if (enabledProperty == null || !enabledProperty.boolValue)
            return false;

        if (showHealthBarProperty == null)
            return false;

        return showHealthBarProperty.boolValue;
    }

    /// <summary>
    /// Builds the shadow-only controls that are always visible in the subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the controls.</param>
    /// <param name="footprintProperty">Serialized footprint settings property.</param>
    private static void BuildShadowControls(EnemyVisualPresetsPanel panel, VisualElement container, SerializedProperty footprintProperty)
    {
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "shadowCoverageMode",
                                                                "Shadow Coverage Mode",
                                                                "Controls whether the shadow alone covers the hit footprint or whether the shadow shrinks to leave room for the spatial UI rings.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("spatialUiHeightOffset"),
                                                              "Height Offset",
                                                              -0.5f,
                                                              0.5f,
                                                              "Vertical offset applied to the indicator quad. Positive values lift above the floor (z-fight avoidance); negative values sink below the pivot for prefabs whose origin sits above the ground.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "positionOffsetXZ",
                                                                "Position Offset (XZ)",
                                                                "Local root-space XZ fine-tune added after automatic visual-bounds center detection. Contact damage, debug rings and shadow use this same resolved center.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "shadowColor",
                                                                "Shadow Color",
                                                                "Tint applied to the hit-box shadow disc. Alpha controls overall shadow strength on top of Shadow Alpha.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("shadowAlpha"),
                                                              "Shadow Alpha",
                                                              0f,
                                                              1f,
                                                              "Final opacity multiplier applied to the shadow disc on top of the shadow color alpha.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("shadowEdgeSoftness"),
                                                              "Shadow Edge Softness",
                                                              0f,
                                                              1f,
                                                              "Normalized falloff width applied at the outer edge of the shadow disc. Higher values produce softer rims.");
    }

    /// <summary>
    /// Builds the ring-only controls shown when Boss UI does not suppress the world-space rings.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the controls.</param>
    /// <param name="footprintProperty">Serialized footprint settings property.</param>
    private static void BuildRingControls(EnemyVisualPresetsPanel panel, VisualElement container, SerializedProperty footprintProperty)
    {
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("ringDistanceFromShadow"),
                                                              "Ring Distance From Shadow",
                                                              -0.5f,
                                                              1.5f,
                                                              "World-space gap between the shadow outer edge and the inner edge of the first fillable ring. Negative values let the ring overlap the shadow for compact widgets.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("spatialUiRingThickness"),
                                                              "Ring Thickness",
                                                              0f,
                                                              1f,
                                                              "World-space radial thickness of each fillable ring drawn around the enemy shadow.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("spatialUiRingSpacing"),
                                                              "Ring Spacing",
                                                              0f,
                                                              1f,
                                                              "World-space gap between the health ring and the shield ring when both are drawn.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("ringArcDegrees"),
                                                              "Ring Arc Degrees",
                                                              1f,
                                                              EnemyVisualFootprintSettings.DefaultRingArcDegrees,
                                                              "Angular width in degrees used by health and shield tracks. Use 360 for full rings, or a smaller value to render only a camera-facing arc.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("ringEdgeSoftness"),
                                                              "Ring Edge Softness",
                                                              0f,
                                                              1f,
                                                              "Normalized radial falloff applied at the inner and outer edges of each ring band. Higher values produce softer ring borders.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("ringAngularSoftness"),
                                                              "Ring Angular Softness",
                                                              0f,
                                                              1f,
                                                              "Angular falloff in radians applied at the depleting edge of each ring fill. Higher values smooth the edge as the ring drains.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "healthRingFillColor",
                                                                "Health Ring Fill Color",
                                                                "Fill color of the health ring when the enemy is at full health. Alpha controls ring opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "healthRingBackgroundColor",
                                                                "Health Ring Background Color",
                                                                "Background color of the health ring track shown behind the depleting fill. Alpha controls track opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "shieldRingFillColor",
                                                                "Shield Ring Fill Color",
                                                                "Fill color of the shield ring when the enemy is at full shield. Alpha controls ring opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "shieldRingBackgroundColor",
                                                                "Shield Ring Background Color",
                                                                "Background color of the shield ring track shown behind the depleting fill. Alpha controls track opacity.");
        BuildRingOrientationControls(panel, container, footprintProperty);
    }

    /// <summary>
    /// Builds the ring-orientation controls. The locked-angle slider is shown only when Lock Rings To World
    /// is enabled, since otherwise the fill anchor tracks the active camera and the authored angle is unused.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the controls.</param>
    /// <param name="footprintProperty">Serialized footprint settings property.</param>
    private static void BuildRingOrientationControls(EnemyVisualPresetsPanel panel, VisualElement container, SerializedProperty footprintProperty)
    {
        SerializedProperty lockProperty = footprintProperty.FindPropertyRelative("lockRingsToWorld");

        if (lockProperty == null)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddReactiveToggleField(panel,
                                                                       container,
                                                                       lockProperty,
                                                                       "Lock Rings To World",
                                                                       "When enabled, the fillable arcs stop tracking the active camera and stay anchored to a fixed world-space direction defined by Locked Rings World Angle.");

        if (!lockProperty.boolValue)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("lockedRingsWorldAngleDegrees"),
                                                              "Locked Rings World Angle",
                                                              0f,
                                                              360f,
                                                              "World-space anchor direction for the depleting fill, expressed as a degree offset from world forward (+Z) rotating clockwise around +Y. 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.");
    }

    /// <summary>
    /// Adds authored-value warnings for the footprint subsection without mutating the serialized values.
    /// Ring warnings are skipped when the rings are suppressed by Boss UI so the user is not bothered
    /// by ring-related warnings that do not affect runtime presentation.
    /// </summary>
    /// <param name="footprintProperty">Serialized footprint settings.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="ringsSuppressed">True when ring controls are hidden by Boss UI suppression.</param>
    private static void AddFootprintWarnings(SerializedProperty footprintProperty, VisualElement container, bool ringsSuppressed)
    {
        if (footprintProperty == null || container == null)
            return;

        SerializedProperty coverageModeProperty = footprintProperty.FindPropertyRelative("shadowCoverageMode");

        if (coverageModeProperty != null &&
            (coverageModeProperty.enumValueIndex < 0 ||
             coverageModeProperty.enumValueIndex > (int)EnemyShadowCoverageMode.ShadowAndSpatialUi))
        {
            container.Add(new HelpBox("Shadow Coverage Mode has an unsupported value. Runtime bake falls back to Shadow Only.", HelpBoxMessageType.Warning));
        }

        AddRangeWarning(footprintProperty, container, "shadowAlpha", 0f, 1f, "Shadow Alpha should stay between 0 and 1.");
        AddRangeWarning(footprintProperty, container, "shadowEdgeSoftness", 0f, 1f, "Shadow Edge Softness should stay between 0 and 1.");

        if (ringsSuppressed)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddNegativeValueWarning(footprintProperty, container, "spatialUiRingThickness", "Ring Thickness must be zero or positive.");
        EnemyVisualPresetsPanelSectionsUtility.AddNegativeValueWarning(footprintProperty, container, "spatialUiRingSpacing", "Ring Spacing must be zero or positive.");
        AddRangeWarning(footprintProperty, container, "ringArcDegrees", 0.0001f, EnemyVisualFootprintSettings.DefaultRingArcDegrees, "Ring Arc Degrees should be greater than 0 and at most 360 degrees.");
        AddRangeWarning(footprintProperty, container, "ringEdgeSoftness", 0f, 1f, "Ring Edge Softness should stay between 0 and 1.");
        AddRangeWarning(footprintProperty, container, "ringAngularSoftness", 0f, 1f, "Ring Angular Softness should stay between 0 and 1 radians.");

        SerializedProperty lockProperty = footprintProperty.FindPropertyRelative("lockRingsToWorld");

        if (lockProperty != null && lockProperty.boolValue)
            AddRangeWarning(footprintProperty, container, "lockedRingsWorldAngleDegrees", 0f, 360f, "Locked Rings World Angle should stay between 0 and 360 degrees.");
    }

    /// <summary>
    /// Adds a warning when a float property falls outside the expected authored range.
    /// Local helper so the section utility stays free of dependencies on internal sections methods.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative float property name.</param>
    /// <param name="minimum">Expected inclusive minimum.</param>
    /// <param name="maximum">Expected inclusive maximum.</param>
    /// <param name="message">Warning text.</param>
    private static void AddRangeWarning(SerializedProperty parentProperty,
                                        VisualElement container,
                                        string relativePropertyName,
                                        float minimum,
                                        float maximum,
                                        string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property != null && (property.floatValue < minimum || property.floatValue > maximum))
            container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion
}
