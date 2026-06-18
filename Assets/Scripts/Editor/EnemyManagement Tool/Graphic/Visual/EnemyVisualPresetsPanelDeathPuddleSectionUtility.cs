using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the conditional Death Puddle foldout shown directly below Death VFX.
/// </summary>
internal static class EnemyVisualPresetsPanelDeathPuddleSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds all Death Puddle controls, contextual help and authored-value warnings.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset.</param>
    /// <param name="puddleProperty">Serialized death puddle settings block.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block containing shared death palette data.</param>
    /// <returns>Configured Death Puddle foldout.</returns>
    public static Foldout Build(EnemyVisualPresetsPanel panel,
                                SerializedProperty puddleProperty,
                                SerializedProperty prefabsProperty)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(puddleProperty,
                                                                                  "Death Puddle",
                                                                                  "EnemyVisualPrefabsDeathPuddle",
                                                                                  true);
        foldout.tooltip = "Pooled ECS render-only liquid mark left at the killed enemy ground position.";
        foldout.style.marginTop = 4f;
        foldout.style.marginBottom = 4f;

        if (panel == null || puddleProperty == null)
            return foldout;

        SerializedProperty enabledProperty = puddleProperty.FindPropertyRelative("enabled");
        EnemyVisualPresetsPanelSectionsUtility.AddReactiveToggleField(panel,
                                                                      foldout,
                                                                      enabledProperty,
                                                                      "Enabled",
                                                                      "Enables a pooled ECS render-only puddle when this enemy is killed.");

        if (enabledProperty != null && !enabledProperty.boolValue)
        {
            foldout.Add(new HelpBox("Death Puddle is disabled for this visual preset.", HelpBoxMessageType.Info));
            return foldout;
        }

        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                foldout,
                                                                puddleProperty,
                                                                "puddlePrefab",
                                                                "Puddle Prefab",
                                                                "Optional ECS-compatible puddle prefab override. Empty uses the shared standard enemy death puddle prefab.");

        AddField(panel, foldout, puddleProperty, "lifetimeSeconds", "Lifetime Seconds");
        AddField(panel, foldout, puddleProperty, "stableFraction", "Stable Fraction");
        AddField(panel, foldout, puddleProperty, "finalScaleRatio", "Final Scale Ratio");
        SerializedProperty evaporationCurveProperty = puddleProperty.FindPropertyRelative("evaporationCurve");
        SerializedProperty sizeModeProperty = puddleProperty.FindPropertyRelative("sizeMode");
        AddReactiveEnumField(panel,
                             foldout,
                             evaporationCurveProperty,
                             "Evaporation Curve",
                             (EnemyDeathPuddleEvaporationCurve)evaporationCurveProperty.enumValueIndex);
        AddReactiveEnumField(panel,
                             foldout,
                             sizeModeProperty,
                             "Size Mode",
                             (EnemyDeathPuddleSizeMode)sizeModeProperty.enumValueIndex);

        if (sizeModeProperty != null && sizeModeProperty.enumValueIndex == (int)EnemyDeathPuddleSizeMode.FixedWorldSize)
            AddField(panel, foldout, puddleProperty, "fixedWorldSize", "Fixed World Size");
        else
            AddField(panel, foldout, puddleProperty, "footprintScaleMultiplier", "Footprint Scale Multiplier");

        AddField(panel, foldout, puddleProperty, "randomSizeVariation", "Random Size Variation");
        AddField(panel, foldout, puddleProperty, "randomRotation", "Random Rotation");
        AddField(panel, foldout, puddleProperty, "groundOffset", "Ground Offset");
        AddField(panel, foldout, puddleProperty, "edgeIrregularity", "Edge Irregularity");
        AddField(panel, foldout, puddleProperty, "borderWidth", "Border Width");
        AddField(panel, foldout, puddleProperty, "edgeFeather", "Edge Feather");
        AddField(panel, foldout, puddleProperty, "secondaryPaletteBlend", "Secondary Palette Blend");
        AddField(panel, foldout, puddleProperty, "flowSpeed", "Flow Speed");
        AddField(panel, foldout, puddleProperty, "viscosity", "Viscosity");
        AddField(panel, foldout, puddleProperty, "surfaceDistortion", "Surface Distortion");
        AddField(panel, foldout, puddleProperty, "highlightStrength", "Highlight Strength");
        EnemyVisualPresetsPanelPaletteControlsUtility.AddDeathPaletteControls(panel, foldout, prefabsProperty);
        AddLiveDiagnostics(puddleProperty, foldout);
        return foldout;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one standard property field using the tooltip declared on the serialized field.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="target">Parent receiving the field.</param>
    /// <param name="parentProperty">Serialized settings block.</param>
    /// <param name="propertyName">Relative serialized property name.</param>
    /// <param name="label">Visible field label.</param>
    private static void AddField(EnemyVisualPresetsPanel panel,
                                 VisualElement target,
                                 SerializedProperty parentProperty,
                                 string propertyName,
                                 string label)
    {
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                target,
                                                                parentProperty,
                                                                propertyName,
                                                                label,
                                                                parentProperty.FindPropertyRelative(propertyName)?.tooltip);
    }

    /// <summary>
    /// Adds an enum field that rebuilds dependent controls when its value changes.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="target">Parent receiving the field.</param>
    /// <param name="property">Serialized enum property.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="currentValue">Typed enum value currently stored by the property.</param>
    private static void AddReactiveEnumField(EnemyVisualPresetsPanel panel,
                                             VisualElement target,
                                             SerializedProperty property,
                                             string label,
                                             System.Enum currentValue)
    {
        if (panel == null || target == null || property == null)
            return;

        EnumField field = new EnumField(label, currentValue);
        field.tooltip = property.tooltip;
        field.RegisterValueChangedCallback(evt =>
        {
            Object targetObject = panel.PresetSerializedObject.targetObject;
            Undo.RecordObject(targetObject, "Edit Enemy Visual Settings");
            panel.PresetSerializedObject.Update();
            property.enumValueIndex = System.Convert.ToInt32(evt.newValue);
            panel.PresetSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
            EnemyManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
            panel.RebuildActiveDetailsSection();
        });
        target.Add(field);
    }

    /// <summary>
    /// Adds warnings for custom puddle prefab overrides that cannot be used as detached render prefabs.
    /// </summary>
    /// <param name="prefabProperty">Serialized custom prefab property.</param>
    /// <param name="target">Parent receiving warnings.</param>
    private static void AddPrefabWarning(SerializedProperty prefabProperty, VisualElement target)
    {
        GameObject prefab = prefabProperty.objectReferenceValue as GameObject;

        if (prefab == null)
            return;

        if (prefab.scene.IsValid() ||
            prefab.GetComponent<EnemyAuthoring>() != null ||
            prefab.GetComponent<PlayerAuthoring>() != null ||
            prefab.GetComponent<EnemyDeathPuddlePrefabAuthoring>() == null)
        {
            target.Add(new HelpBox("Puddle Prefab must be a prefab asset containing EnemyDeathPuddlePrefabAuthoring and no EnemyAuthoring or PlayerAuthoring component.",
                                   HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Adds authored-value warnings without mutating Death Puddle settings.
    /// </summary>
    /// <param name="puddleProperty">Serialized death puddle settings block.</param>
    /// <param name="target">Parent receiving warnings.</param>
    private static void AddWarnings(SerializedProperty puddleProperty, VisualElement target)
    {
        AddRangeWarning(puddleProperty, target, "lifetimeSeconds", 0.1f, 30f, "Lifetime Seconds should stay between 0.1 and 30.");
        AddRangeWarning(puddleProperty, target, "stableFraction", 0f, 0.95f, "Stable Fraction should stay between 0 and 0.95.");
        AddRangeWarning(puddleProperty, target, "finalScaleRatio", 0f, 1f, "Final Scale Ratio should stay between 0 and 1.");
        AddRangeWarning(puddleProperty, target, "randomSizeVariation", 0f, 0.75f, "Random Size Variation should stay between 0 and 0.75.");
        AddRangeWarning(puddleProperty, target, "groundOffset", -0.1f, 0.5f, "Ground Offset should stay between -0.1 and 0.5.");
        AddRangeWarning(puddleProperty, target, "edgeIrregularity", 0f, 1f, "Edge Irregularity should stay between 0 and 1.");
        AddRangeWarning(puddleProperty, target, "borderWidth", 0f, 0.5f, "Border Width should stay between 0 and 0.5.");
        AddRangeWarning(puddleProperty, target, "edgeFeather", 0.001f, 0.5f, "Edge Feather should stay between 0.001 and 0.5.");
        AddRangeWarning(puddleProperty, target, "secondaryPaletteBlend", 0f, 1f, "Secondary Palette Blend should stay between 0 and 1.");
        AddRangeWarning(puddleProperty, target, "flowSpeed", 0f, 3f, "Flow Speed should stay between 0 and 3.");
        AddRangeWarning(puddleProperty, target, "viscosity", 0f, 1f, "Viscosity should stay between 0 and 1.");
        AddRangeWarning(puddleProperty, target, "surfaceDistortion", 0f, 0.35f, "Surface Distortion should stay between 0 and 0.35.");
        AddRangeWarning(puddleProperty, target, "highlightStrength", 0f, 1f, "Highlight Strength should stay between 0 and 1.");

        SerializedProperty evaporationCurveProperty = puddleProperty.FindPropertyRelative("evaporationCurve");
        SerializedProperty sizeModeProperty = puddleProperty.FindPropertyRelative("sizeMode");
        SerializedProperty fixedSizeProperty = puddleProperty.FindPropertyRelative("fixedWorldSize");

        if (evaporationCurveProperty != null &&
            !System.Enum.IsDefined(typeof(EnemyDeathPuddleEvaporationCurve),
                                   (EnemyDeathPuddleEvaporationCurve)evaporationCurveProperty.intValue))
        {
            target.Add(new HelpBox("Evaporation Curve contains an unsupported enum value.", HelpBoxMessageType.Warning));
        }

        if (sizeModeProperty != null &&
            !System.Enum.IsDefined(typeof(EnemyDeathPuddleSizeMode),
                                   (EnemyDeathPuddleSizeMode)sizeModeProperty.intValue))
        {
            target.Add(new HelpBox("Size Mode contains an unsupported enum value.", HelpBoxMessageType.Warning));
        }

        if (sizeModeProperty != null &&
            sizeModeProperty.intValue == (int)EnemyDeathPuddleSizeMode.EnemyFootprint)
        {
            AddRangeWarning(puddleProperty,
                            target,
                            "footprintScaleMultiplier",
                            0.1f,
                            4f,
                            "Footprint Scale Multiplier should stay between 0.1 and 4.");
        }

        if (sizeModeProperty != null &&
            sizeModeProperty.intValue == (int)EnemyDeathPuddleSizeMode.FixedWorldSize &&
            fixedSizeProperty != null &&
            (!IsFinite(fixedSizeProperty.vector2Value.x) ||
             !IsFinite(fixedSizeProperty.vector2Value.y) ||
             fixedSizeProperty.vector2Value.x <= 0f ||
             fixedSizeProperty.vector2Value.y <= 0f))
        {
            target.Add(new HelpBox("Fixed World Size components must be finite and greater than zero.", HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Adds a diagnostics container that refreshes only its messages while relevant authored values change.
    /// </summary>
    /// <param name="puddleProperty">Serialized death puddle settings block.</param>
    /// <param name="target">Parent receiving diagnostics and property trackers.</param>
    private static void AddLiveDiagnostics(SerializedProperty puddleProperty, VisualElement target)
    {
        VisualElement diagnostics = new VisualElement();
        string[] trackedPropertyNames =
        {
            "puddlePrefab",
            "lifetimeSeconds",
            "stableFraction",
            "finalScaleRatio",
            "evaporationCurve",
            "sizeMode",
            "footprintScaleMultiplier",
            "fixedWorldSize",
            "randomSizeVariation",
            "groundOffset",
            "edgeIrregularity",
            "borderWidth",
            "edgeFeather",
            "secondaryPaletteBlend",
            "flowSpeed",
            "viscosity",
            "surfaceDistortion",
            "highlightStrength"
        };

        target.Add(diagnostics);
        RefreshDiagnostics(puddleProperty, diagnostics);

        // Track only properties that can change the current contextual diagnostics.
        for (int propertyIndex = 0; propertyIndex < trackedPropertyNames.Length; propertyIndex++)
        {
            SerializedProperty trackedProperty = puddleProperty.FindPropertyRelative(trackedPropertyNames[propertyIndex]);

            if (trackedProperty == null)
                continue;

            target.TrackPropertyValue(trackedProperty, changedProperty =>
            {
                RefreshDiagnostics(puddleProperty, diagnostics);
            });
        }
    }

    /// <summary>
    /// Rebuilds contextual prefab information and authored-value warnings without rebuilding the parent foldout.
    /// </summary>
    /// <param name="puddleProperty">Serialized death puddle settings block.</param>
    /// <param name="target">Diagnostics container receiving the current messages.</param>
    private static void RefreshDiagnostics(SerializedProperty puddleProperty, VisualElement target)
    {
        SerializedProperty prefabProperty = puddleProperty.FindPropertyRelative("puddlePrefab");
        target.Clear();

        if (prefabProperty == null || prefabProperty.objectReferenceValue == null)
            target.Add(new HelpBox("The shared Resources/PF_EnemyDeathPuddle prefab will be used.", HelpBoxMessageType.Info));
        else
            AddPrefabWarning(prefabProperty, target);

        AddWarnings(puddleProperty, target);
    }

    /// <summary>
    /// Adds a warning for one float outside an expected finite range.
    /// </summary>
    /// <param name="parentProperty">Serialized parent block.</param>
    /// <param name="target">Parent receiving warnings.</param>
    /// <param name="propertyName">Relative float property name.</param>
    /// <param name="minimum">Expected inclusive minimum.</param>
    /// <param name="maximum">Expected inclusive maximum.</param>
    /// <param name="message">Warning message.</param>
    private static void AddRangeWarning(SerializedProperty parentProperty,
                                        VisualElement target,
                                        string propertyName,
                                        float minimum,
                                        float maximum,
                                        string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);

        if (property != null &&
            (!IsFinite(property.floatValue) || property.floatValue < minimum || property.floatValue > maximum))
        {
            target.Add(new HelpBox(message, HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Returns whether one authored float is finite.
    /// </summary>
    /// <param name="value">Float value to inspect.</param>
    /// <returns>True when the value is neither NaN nor Infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
