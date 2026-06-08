using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds conditional Elastic Hit Deformation controls inside the Damage Feedback subsection.
/// </summary>
internal static class EnemyVisualPresetsPanelElasticHitSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds elastic hit controls, shader compatibility diagnostics and authored-value warnings.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the serialized preset.</param>
    /// <param name="elasticProperty">Serialized elastic hit settings block.</param>
    /// <returns>Configured Elastic Hit Deformation foldout.</returns>
    public static Foldout Build(EnemyVisualPresetsPanel panel, SerializedProperty elasticProperty)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(elasticProperty,
                                                                                  "Elastic Hit Deformation",
                                                                                  "EnemyVisualDamageElasticHit",
                                                                                  true);
        foldout.tooltip = "Shader-driven squash and rebound shown only after non-lethal enemy damage.";
        foldout.style.marginTop = 6f;

        if (panel == null || elasticProperty == null)
            return foldout;

        SerializedProperty enabledProperty = elasticProperty.FindPropertyRelative("enabled");
        EnemyVisualPresetsPanelSectionsUtility.AddReactiveToggleField(panel,
                                                                      foldout,
                                                                      enabledProperty,
                                                                      "Enabled",
                                                                      "Enables shader-driven elastic deformation after valid non-lethal enemy damage.");

        if (enabledProperty != null && !enabledProperty.boolValue)
        {
            foldout.Add(new HelpBox("Elastic Hit Deformation is disabled for this visual preset.", HelpBoxMessageType.Info));
            return foldout;
        }

        AddField(panel, foldout, elasticProperty, "triggerMode", "Trigger Mode");
        AddField(panel, foldout, elasticProperty, "durationSeconds", "Duration Seconds");
        AddField(panel, foldout, elasticProperty, "maximumCompression", "Maximum Compression");
        AddField(panel, foldout, elasticProperty, "volumeCompensation", "Volume Compensation");
        AddField(panel, foldout, elasticProperty, "oscillationCount", "Oscillation Count");
        AddField(panel, foldout, elasticProperty, "damping", "Damping");
        AddField(panel, foldout, elasticProperty, "directionality", "Directionality");
        AddField(panel, foldout, elasticProperty, "anchorToGround", "Anchor To Ground");
        AddField(panel, foldout, elasticProperty, "minimumRetriggerInterval", "Minimum Retrigger Interval");
        AddField(panel, foldout, elasticProperty, "retriggerMode", "Retrigger Mode");
        AddLiveDiagnostics(panel, elasticProperty, foldout);
        return foldout;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one standard property field using its serialized tooltip.
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
    /// Adds authored-value warnings without mutating elastic hit settings.
    /// </summary>
    /// <param name="elasticProperty">Serialized elastic settings block.</param>
    /// <param name="target">Parent receiving warnings.</param>
    private static void AddWarnings(SerializedProperty elasticProperty, VisualElement target)
    {
        AddRangeWarning(elasticProperty, target, "durationSeconds", 0.02f, 1f, "Duration Seconds should stay between 0.02 and 1.");
        AddRangeWarning(elasticProperty, target, "maximumCompression", 0f, 0.75f, "Maximum Compression should stay between 0 and 0.75.");
        AddRangeWarning(elasticProperty, target, "volumeCompensation", 0f, 1f, "Volume Compensation should stay between 0 and 1.");
        AddRangeWarning(elasticProperty, target, "oscillationCount", 0.1f, 5f, "Oscillation Count should stay between 0.1 and 5.");
        AddRangeWarning(elasticProperty, target, "damping", 0f, 20f, "Damping should stay between 0 and 20.");
        AddRangeWarning(elasticProperty, target, "directionality", 0f, 1f, "Directionality should stay between 0 and 1.");
        AddRangeWarning(elasticProperty, target, "minimumRetriggerInterval", 0f, 0.5f, "Minimum Retrigger Interval should stay between 0 and 0.5.");

        SerializedProperty triggerModeProperty = elasticProperty.FindPropertyRelative("triggerMode");
        SerializedProperty retriggerModeProperty = elasticProperty.FindPropertyRelative("retriggerMode");

        if (triggerModeProperty != null &&
            !System.Enum.IsDefined(typeof(EnemyElasticHitTriggerMode),
                                   (EnemyElasticHitTriggerMode)triggerModeProperty.intValue))
        {
            target.Add(new HelpBox("Trigger Mode contains an unsupported enum value.", HelpBoxMessageType.Warning));
        }

        if (retriggerModeProperty != null &&
            !System.Enum.IsDefined(typeof(EnemyElasticHitRetriggerMode),
                                   (EnemyElasticHitRetriggerMode)retriggerModeProperty.intValue))
        {
            target.Add(new HelpBox("Retrigger Mode contains an unsupported enum value.", HelpBoxMessageType.Warning));
        }
    }

    /// <summary>
    /// Adds a diagnostics container that refreshes warnings without rebuilding the elastic hit foldout.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="elasticProperty">Serialized elastic settings block.</param>
    /// <param name="target">Parent receiving diagnostics and property trackers.</param>
    private static void AddLiveDiagnostics(EnemyVisualPresetsPanel panel,
                                           SerializedProperty elasticProperty,
                                           VisualElement target)
    {
        VisualElement diagnostics = new VisualElement();
        string[] trackedPropertyNames =
        {
            "triggerMode",
            "durationSeconds",
            "maximumCompression",
            "volumeCompensation",
            "oscillationCount",
            "damping",
            "directionality",
            "minimumRetriggerInterval",
            "retriggerMode"
        };

        target.Add(diagnostics);
        RefreshDiagnostics(panel, elasticProperty, diagnostics);

        // Track only properties that can change authored-value diagnostics.
        for (int propertyIndex = 0; propertyIndex < trackedPropertyNames.Length; propertyIndex++)
        {
            SerializedProperty trackedProperty = elasticProperty.FindPropertyRelative(trackedPropertyNames[propertyIndex]);

            if (trackedProperty == null)
                continue;

            target.TrackPropertyValue(trackedProperty, changedProperty =>
            {
                RefreshDiagnostics(panel, elasticProperty, diagnostics);
            });
        }

        SerializedProperty prefabsProperty = panel.PresetSerializedObject.FindProperty("prefabs");
        SerializedProperty enemyPrefabProperty = prefabsProperty?.FindPropertyRelative("enemyPrefab");

        if (enemyPrefabProperty != null)
        {
            target.TrackPropertyValue(enemyPrefabProperty, changedProperty =>
            {
                RefreshDiagnostics(panel, elasticProperty, diagnostics);
            });
        }
    }

    /// <summary>
    /// Rebuilds authored-value and shader compatibility warnings without rebuilding the parent foldout.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="elasticProperty">Serialized elastic settings block.</param>
    /// <param name="target">Diagnostics container receiving the current messages.</param>
    private static void RefreshDiagnostics(EnemyVisualPresetsPanel panel,
                                           SerializedProperty elasticProperty,
                                           VisualElement target)
    {
        target.Clear();
        AddWarnings(elasticProperty, target);
        AddShaderCompatibilityWarning(panel, target);
    }

    /// <summary>
    /// Warns when the linked enemy prefab has no material exposing elastic hit shader properties.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="target">Parent receiving the warning.</param>
    private static void AddShaderCompatibilityWarning(EnemyVisualPresetsPanel panel, VisualElement target)
    {
        SerializedProperty prefabsProperty = panel.PresetSerializedObject.FindProperty("prefabs");
        SerializedProperty enemyPrefabProperty = prefabsProperty?.FindPropertyRelative("enemyPrefab");
        GameObject enemyPrefab = enemyPrefabProperty?.objectReferenceValue as GameObject;

        if (enemyPrefab == null)
            return;

        Renderer[] renderers = enemyPrefab.GetComponentsInChildren<Renderer>(true);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material != null && material.HasProperty("_ElasticHitTiming"))
                    return;
            }
        }

        target.Add(new HelpBox("The linked Enemy Prefab has no material exposing elastic hit shader properties. Gameplay transforms will not be scaled as a fallback.",
                               HelpBoxMessageType.Warning));
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
