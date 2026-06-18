using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the Footprint UI subsection used by the enemy visual presets management tool.
/// Controls cover the shader-driven shadow disc plus the optional concentric fillable rings or arcs,
/// including per-enemy ring colors and ring-only knobs that hide automatically when runtime gates suppress them.
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
        SerializedProperty ringsEnabledProperty = footprintProperty.FindPropertyRelative("healthRingsEnabled");
        bool healthRingsEnabled = ringsEnabledProperty == null || ringsEnabledProperty.boolValue;
        bool ringWarningsSuppressed = ringsSuppressed || !healthRingsEnabled;
        Foldout shadowLayoutFoldout = CreateFootprintFoldout(footprintProperty,
                                                             "Shadow Layout",
                                                             "ShadowLayout",
                                                             "Footprint size, center and vertical placement used by the shader-driven shadow.");
        Foldout shadowAppearanceFoldout = CreateFootprintFoldout(footprintProperty,
                                                                 "Shadow Appearance",
                                                                 "ShadowAppearance",
                                                                 "Visual color and softness controls applied to the hit-box shadow.");
        Foldout healthRingsFoldout = CreateFootprintFoldout(footprintProperty,
                                                            "Health Rings",
                                                            "HealthRings",
                                                            "Health and shield ring enable, layout, appearance and orientation controls.");

        container.Add(shadowLayoutFoldout);
        container.Add(shadowAppearanceFoldout);
        container.Add(healthRingsFoldout);
        BuildShadowLayoutControls(panel, shadowLayoutFoldout, footprintProperty);
        BuildShadowAppearanceControls(panel, shadowAppearanceFoldout, footprintProperty);

        if (ringsEnabledProperty != null)
        {
            EnemyVisualPresetsPanelSectionsUtility.AddReactiveToggleField(panel,
                                                                          healthRingsFoldout,
                                                                          ringsEnabledProperty,
                                                                          "Health Rings Enabled",
                                                                          "When enabled, the ground footprint renders health and shield rings around the hit-box shadow. Disable this to keep only the shadow.");
        }

        if (ringsSuppressed)
        {
            healthRingsFoldout.Add(new HelpBox("Ring controls are hidden because Boss UI is enabled on this preset. The screen-space boss HUD owns health and shield bars, so the world-space rings are suppressed at runtime. Shadow controls above still apply.",
                                               HelpBoxMessageType.Info));
        }
        else if (!healthRingsEnabled)
        {
            healthRingsFoldout.Add(new HelpBox("Ring controls are hidden because Health Rings Enabled is off. The shadow remains active and uses the layout settings above.",
                                               HelpBoxMessageType.Info));
        }
        else
        {
            BuildRingControls(panel, healthRingsFoldout, footprintProperty);
        }

        AddFootprintWarnings(footprintProperty, container, ringWarningsSuppressed);
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
    /// Creates one themed foldout bound to the edited footprint property so expanded state persists across panel refreshes.
    /// </summary>
    /// <param name="footprintProperty">Serialized footprint settings property used to derive a stable state key.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="stateSuffix">Local state-key suffix unique inside the footprint subsection.</param>
    /// <param name="tooltip">Tooltip explaining the group purpose.</param>
    /// <returns>Configured themed foldout.</returns>
    private static Foldout CreateFootprintFoldout(SerializedProperty footprintProperty,
                                                  string title,
                                                  string stateSuffix,
                                                  string tooltip)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(footprintProperty,
                                                                                  title,
                                                                                  stateSuffix,
                                                                                  true);
        foldout.tooltip = tooltip;
        return foldout;
    }

    /// <summary>
    /// Builds shadow layout controls that are always visible because the ground shadow remains active even when rings are hidden.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the controls.</param>
    /// <param name="footprintProperty">Serialized footprint settings property.</param>
    private static void BuildShadowLayoutControls(EnemyVisualPresetsPanel panel, VisualElement container, SerializedProperty footprintProperty)
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
        BuildShadowProjectionControls(panel, container, footprintProperty);
    }

    /// <summary>
    /// Builds projection controls and hides the max-distance field until ground projection is active.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the controls.</param>
    /// <param name="footprintProperty">Serialized footprint settings property.</param>
    private static void BuildShadowProjectionControls(EnemyVisualPresetsPanel panel, VisualElement container, SerializedProperty footprintProperty)
    {
        SerializedProperty projectionModeProperty = footprintProperty.FindPropertyRelative("projectionMode");

        if (projectionModeProperty == null)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                container,
                                                                footprintProperty,
                                                                "projectionMode",
                                                                "Projection Mode",
                                                                "Controls whether the shadow remains on the authored raised quad or ray-projects onto the ground surface below the hit center.");
        container.TrackPropertyValue(projectionModeProperty, changedProperty =>
        {
            panel.RebuildActiveDetailsSection();
        });

        if (projectionModeProperty.enumValueIndex != (int)GroundShadowProjectionMode.ProjectOntoGround)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              container,
                                                              footprintProperty.FindPropertyRelative("projectionMaxDistance"),
                                                              "Projection Max Distance",
                                                              0f,
                                                              16f,
                                                              "Maximum downward distance in meters used to find a ground surface for projected shadows. If no hit is found, the raised quad fallback is used.");
    }

    /// <summary>
    /// Builds shadow appearance controls that are independent from health and shield ring visibility.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="container">Container receiving the controls.</param>
    /// <param name="footprintProperty">Serialized footprint settings property.</param>
    private static void BuildShadowAppearanceControls(EnemyVisualPresetsPanel panel, VisualElement container, SerializedProperty footprintProperty)
    {
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
        Foldout layoutFoldout = CreateFootprintFoldout(footprintProperty,
                                                       "Ring Layout",
                                                       "RingLayout",
                                                       "Distance, thickness, spacing and arc width used by health and shield rings.");
        Foldout appearanceFoldout = CreateFootprintFoldout(footprintProperty,
                                                           "Ring Appearance",
                                                           "RingAppearance",
                                                           "Fill colors, track colors and edge softness for health and shield rings.");
        Foldout orientationFoldout = CreateFootprintFoldout(footprintProperty,
                                                            "Ring Orientation",
                                                            "RingOrientation",
                                                            "Camera-facing or fixed-world fill-anchor controls for partial ring arcs.");

        container.Add(layoutFoldout);
        container.Add(appearanceFoldout);
        container.Add(orientationFoldout);
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              layoutFoldout,
                                                              footprintProperty.FindPropertyRelative("ringDistanceFromShadow"),
                                                              "Ring Distance From Shadow",
                                                              -0.5f,
                                                              1.5f,
                                                              "World-space gap between the shadow outer edge and the inner edge of the first fillable ring. Negative values let the ring overlap the shadow for compact widgets.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              layoutFoldout,
                                                              footprintProperty.FindPropertyRelative("spatialUiRingThickness"),
                                                              "Ring Thickness",
                                                              0f,
                                                              1f,
                                                              "World-space radial thickness of each fillable ring drawn around the enemy shadow.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              layoutFoldout,
                                                              footprintProperty.FindPropertyRelative("spatialUiRingSpacing"),
                                                              "Ring Spacing",
                                                              0f,
                                                              1f,
                                                              "World-space gap between the health ring and the shield ring when both are drawn.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              layoutFoldout,
                                                              footprintProperty.FindPropertyRelative("ringArcDegrees"),
                                                              "Ring Arc Degrees",
                                                              1f,
                                                              EnemyVisualFootprintSettings.DefaultRingArcDegrees,
                                                              "Angular width in degrees used by health and shield tracks. Use 360 for full rings, or a smaller value to render only a camera-facing arc.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              appearanceFoldout,
                                                              footprintProperty.FindPropertyRelative("ringEdgeSoftness"),
                                                              "Ring Edge Softness",
                                                              0f,
                                                              1f,
                                                              "Normalized radial falloff applied at the inner and outer edges of each ring band. Higher values produce softer ring borders.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              appearanceFoldout,
                                                              footprintProperty.FindPropertyRelative("ringAngularSoftness"),
                                                              "Ring Angular Softness",
                                                              0f,
                                                              1f,
                                                              "Angular falloff in radians applied at the depleting edge of each ring fill. Higher values smooth the edge as the ring drains.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                appearanceFoldout,
                                                                footprintProperty,
                                                                "healthRingFillColor",
                                                                "Health Ring Fill Color",
                                                                "Fill color of the health ring when the enemy is at full health. Alpha controls ring opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                appearanceFoldout,
                                                                footprintProperty,
                                                                "healthRingBackgroundColor",
                                                                "Health Ring Background Color",
                                                                "Background color of the health ring track shown behind the depleting fill. Alpha controls track opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              appearanceFoldout,
                                                              footprintProperty.FindPropertyRelative("healthRingBackgroundAlpha"),
                                                              "Health Ring Background Alpha",
                                                              0f,
                                                              1f,
                                                              "Final opacity multiplier applied to the health ring background on top of its color alpha.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                appearanceFoldout,
                                                                footprintProperty,
                                                                "shieldRingFillColor",
                                                                "Shield Ring Fill Color",
                                                                "Fill color of the shield ring when the enemy is at full shield. Alpha controls ring opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                appearanceFoldout,
                                                                footprintProperty,
                                                                "shieldRingBackgroundColor",
                                                                "Shield Ring Background Color",
                                                                "Background color of the shield ring track shown behind the depleting fill. Alpha controls track opacity.");
        EnemyVisualPresetsPanelSectionsUtility.AddSliderField(panel,
                                                              appearanceFoldout,
                                                              footprintProperty.FindPropertyRelative("shieldRingBackgroundAlpha"),
                                                              "Shield Ring Background Alpha",
                                                              0f,
                                                              1f,
                                                              "Final opacity multiplier applied to the shield ring background on top of its color alpha.");
        BuildRingOrientationControls(panel, orientationFoldout, footprintProperty);
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
    /// Ring warnings are skipped when authored runtime gates hide rings so the user is not bothered
    /// by ring-related warnings that do not affect presentation.
    /// </summary>
    /// <param name="footprintProperty">Serialized footprint settings.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="ringWarningsSuppressed">True when ring-related warnings should be hidden with the ring controls.</param>
    private static void AddFootprintWarnings(SerializedProperty footprintProperty, VisualElement container, bool ringWarningsSuppressed)
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
        AddProjectionWarnings(footprintProperty, container);

        if (ringWarningsSuppressed)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddNegativeValueWarning(footprintProperty, container, "spatialUiRingThickness", "Ring Thickness must be zero or positive.");
        EnemyVisualPresetsPanelSectionsUtility.AddNegativeValueWarning(footprintProperty, container, "spatialUiRingSpacing", "Ring Spacing must be zero or positive.");
        AddRangeWarning(footprintProperty, container, "ringArcDegrees", 0.0001f, EnemyVisualFootprintSettings.DefaultRingArcDegrees, "Ring Arc Degrees should be greater than 0 and at most 360 degrees.");
        AddRangeWarning(footprintProperty, container, "ringEdgeSoftness", 0f, 1f, "Ring Edge Softness should stay between 0 and 1.");
        AddRangeWarning(footprintProperty, container, "ringAngularSoftness", 0f, 1f, "Ring Angular Softness should stay between 0 and 1 radians.");
        AddRangeWarning(footprintProperty, container, "healthRingBackgroundAlpha", 0f, 1f, "Health Ring Background Alpha should stay between 0 and 1.");
        AddRangeWarning(footprintProperty, container, "shieldRingBackgroundAlpha", 0f, 1f, "Shield Ring Background Alpha should stay between 0 and 1.");

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

    /// <summary>
    /// Adds authored-value warnings for shadow ground projection settings.
    /// </summary>
    /// <param name="footprintProperty">Serialized footprint settings.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    private static void AddProjectionWarnings(SerializedProperty footprintProperty, VisualElement container)
    {
        SerializedProperty projectionModeProperty = footprintProperty.FindPropertyRelative("projectionMode");

        if (projectionModeProperty != null &&
            (projectionModeProperty.enumValueIndex < 0 ||
             projectionModeProperty.enumValueIndex > (int)GroundShadowProjectionMode.ProjectOntoGround))
        {
            container.Add(new HelpBox("Projection Mode has an unsupported value. Runtime bake falls back to Raised Quad.", HelpBoxMessageType.Warning));
        }

        if (projectionModeProperty == null ||
            projectionModeProperty.enumValueIndex != (int)GroundShadowProjectionMode.ProjectOntoGround)
        {
            return;
        }

        EnemyVisualPresetsPanelSectionsUtility.AddNegativeValueWarning(footprintProperty,
                                                                       container,
                                                                       "projectionMaxDistance",
                                                                       "Projection Max Distance must be zero or positive.");
    }
    #endregion

    #endregion
}
