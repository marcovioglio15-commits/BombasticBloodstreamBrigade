using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the themed Prefabs subsection for enemy visual presets.
/// </summary>
internal static class EnemyVisualPresetsPanelPrefabsSectionUtility
{
    #region Constants
    private const string FoldoutStateSuffixPrefix = "EnemyVisualPrefabs";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds prefab, hit VFX, and paint-metadata controls for the selected enemy visual preset.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <returns>Prefabs subsection content grouped by themed foldouts.</returns>
    public static VisualElement BuildPrefabsSubSection(EnemyVisualPresetsPanel panel)
    {
        VisualElement container = EnemyVisualPresetsPanelSectionsUtility.CreateSubSectionContainer("Prefabs");

        if (panel == null || panel.PresetSerializedObject == null)
            return container;

        SerializedProperty prefabsProperty = panel.PresetSerializedObject.FindProperty("prefabs");

        if (prefabsProperty == null)
            return container;

        // Keep each concern in a separate foldout so dense visual presets stay scannable.
        container.Add(BuildEnemyPrefabFoldout(panel, prefabsProperty));
        container.Add(BuildHitVfxFoldout(panel, prefabsProperty));
        container.Add(BuildPaintMetadataFoldout(panel, prefabsProperty));
        return container;
    }
    #endregion

    #region Foldout Builders
    /// <summary>
    /// Builds controls related to the authoritative enemy prefab reference.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing enemy prefab controls.</returns>
    private static Foldout BuildEnemyPrefabFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Enemy Prefab",
                                              "EnemyPrefab",
                                              "Prefab reference used by spawners and master-preset activation workflows.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                foldout,
                                                                prefabsProperty,
                                                                "enemyPrefab",
                                                                "Enemy Prefab",
                                                                "Enemy prefab associated with this enemy type. It should contain EnemyAuthoring.");
        return foldout;
    }

    /// <summary>
    /// Builds hit VFX controls and hides dependent settings until a VFX prefab is assigned.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing hit VFX controls.</returns>
    private static Foldout BuildHitVfxFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Hit VFX",
                                              "HitVfx",
                                              "One-shot visual feedback spawned when player projectiles damage this enemy.");
        SerializedProperty hitVfxPrefabProperty = prefabsProperty.FindPropertyRelative("hitVfxPrefab");
        AddReactivePropertyField(panel,
                                 foldout,
                                 hitVfxPrefabProperty,
                                 "Hit VFX Prefab",
                                 "Optional one-shot VFX prefab spawned every time this enemy receives a projectile hit.");
        HelpBox missingPrefabBox = new HelpBox("Assign a Hit VFX prefab to enable spawn offset, lifetime, and scale controls.",
                                               HelpBoxMessageType.Info);
        VisualElement detailsContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();

        foldout.Add(missingPrefabBox);
        foldout.Add(detailsContainer);
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                detailsContainer,
                                                                prefabsProperty,
                                                                "hitVfxSpawnOffset",
                                                                "Hit VFX Spawn Offset",
                                                                "World-space offset added to the resolved impact position before spawning the enemy hit VFX.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                detailsContainer,
                                                                prefabsProperty,
                                                                "hitVfxLifetimeSeconds",
                                                                "Hit VFX Lifetime Seconds",
                                                                "Lifetime in seconds assigned to each spawned hit VFX instance.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                detailsContainer,
                                                                prefabsProperty,
                                                                "hitVfxScaleMultiplier",
                                                                "Hit VFX Scale Multiplier",
                                                                "Uniform scale multiplier applied to the spawned hit VFX instance.");
        detailsContainer.Add(warningsContainer);
        RefreshHitVfxDetailsVisibility(hitVfxPrefabProperty,
                                       missingPrefabBox,
                                       detailsContainer,
                                       warningsContainer,
                                       prefabsProperty);
        TrackHitVfxDependentFields(foldout,
                                   prefabsProperty,
                                   hitVfxPrefabProperty,
                                   missingPrefabBox,
                                   detailsContainer,
                                   warningsContainer);
        return foldout;
    }

    /// <summary>
    /// Builds metadata controls used by wave painting and editor previews.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <returns>Foldout containing editor paint metadata controls.</returns>
    private static Foldout BuildPaintMetadataFoldout(EnemyVisualPresetsPanel panel, SerializedProperty prefabsProperty)
    {
        Foldout foldout = CreatePrefabFoldout(prefabsProperty,
                                              "Painter Metadata",
                                              "PainterMetadata",
                                              "Editor-only paint metadata used by wave authoring and scene previews.");
        EnemyVisualPresetsPanelSectionsUtility.AddPropertyField(panel,
                                                                foldout,
                                                                prefabsProperty,
                                                                "spawnPaintColor",
                                                                "Spawn Paint Color",
                                                                "Color used by the wave painter and scene preview for this enemy type.");
        return foldout;
    }
    #endregion

    #region Field Helpers
    /// <summary>
    /// Adds a property field that marks preset state dirty without rebuilding the active details view.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    /// <param name="target">Parent element receiving the field.</param>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="label">Visible field label.</param>
    /// <param name="tooltip">Tooltip explaining the field.</param>
    private static void AddReactivePropertyField(EnemyVisualPresetsPanel panel,
                                                 VisualElement target,
                                                 SerializedProperty property,
                                                 string label,
                                                 string tooltip)
    {
        if (panel == null || target == null || property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            MarkPanelDirty(panel);
        });
        target.Add(propertyField);
    }

    /// <summary>
    /// Tracks hit VFX properties that affect local visibility or warning presentation.
    /// </summary>
    /// <param name="root">Root element used to register UI Toolkit property trackers.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="hitVfxPrefabProperty">Serialized Hit VFX prefab property.</param>
    /// <param name="missingPrefabBox">Info box shown while no Hit VFX prefab is assigned.</param>
    /// <param name="detailsContainer">Container holding dependent Hit VFX controls.</param>
    /// <param name="warningsContainer">Container receiving current authored-value warnings.</param>
    private static void TrackHitVfxDependentFields(VisualElement root,
                                                   SerializedProperty prefabsProperty,
                                                   SerializedProperty hitVfxPrefabProperty,
                                                   HelpBox missingPrefabBox,
                                                   VisualElement detailsContainer,
                                                   VisualElement warningsContainer)
    {
        if (root == null || prefabsProperty == null)
            return;

        if (hitVfxPrefabProperty != null)
        {
            root.TrackPropertyValue(hitVfxPrefabProperty, changedProperty =>
            {
                RefreshHitVfxDetailsVisibility(changedProperty,
                                               missingPrefabBox,
                                               detailsContainer,
                                               warningsContainer,
                                               prefabsProperty);
            });
        }

        TrackHitVfxWarningField(root, prefabsProperty, "hitVfxSpawnOffset", warningsContainer);
        TrackHitVfxWarningField(root, prefabsProperty, "hitVfxLifetimeSeconds", warningsContainer);
        TrackHitVfxWarningField(root, prefabsProperty, "hitVfxScaleMultiplier", warningsContainer);
    }

    /// <summary>
    /// Tracks one Hit VFX warning source and refreshes the local warning container when it changes.
    /// </summary>
    /// <param name="root">Root element used to register UI Toolkit property trackers.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="relativePropertyName">Relative property name to track.</param>
    /// <param name="warningsContainer">Container receiving current authored-value warnings.</param>
    private static void TrackHitVfxWarningField(VisualElement root,
                                                SerializedProperty prefabsProperty,
                                                string relativePropertyName,
                                                VisualElement warningsContainer)
    {
        SerializedProperty trackedProperty = prefabsProperty.FindPropertyRelative(relativePropertyName);

        if (trackedProperty == null)
            return;

        root.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            RefreshHitVfxWarnings(prefabsProperty, warningsContainer);
        });
    }

    /// <summary>
    /// Updates local Hit VFX dependent controls without rebuilding the whole visual preset section.
    /// </summary>
    /// <param name="hitVfxPrefabProperty">Serialized Hit VFX prefab property.</param>
    /// <param name="missingPrefabBox">Info box shown while no Hit VFX prefab is assigned.</param>
    /// <param name="detailsContainer">Container holding dependent Hit VFX controls.</param>
    /// <param name="warningsContainer">Container receiving current authored-value warnings.</param>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    private static void RefreshHitVfxDetailsVisibility(SerializedProperty hitVfxPrefabProperty,
                                                       HelpBox missingPrefabBox,
                                                       VisualElement detailsContainer,
                                                       VisualElement warningsContainer,
                                                       SerializedProperty prefabsProperty)
    {
        bool hasHitVfxPrefab = hitVfxPrefabProperty != null && hitVfxPrefabProperty.objectReferenceValue != null;

        if (missingPrefabBox != null)
            missingPrefabBox.style.display = hasHitVfxPrefab ? DisplayStyle.None : DisplayStyle.Flex;

        if (detailsContainer != null)
            detailsContainer.style.display = hasHitVfxPrefab ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshHitVfxWarnings(hasHitVfxPrefab ? prefabsProperty : null, warningsContainer);
    }

    /// <summary>
    /// Creates a persisted foldout styled for nested prefab-setting groups.
    /// </summary>
    /// <param name="prefabsProperty">Serialized prefab settings block used to scope the state key.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="stateSuffix">Stable suffix used for persisted foldout state.</param>
    /// <param name="tooltip">Tooltip explaining the group.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreatePrefabFoldout(SerializedProperty prefabsProperty,
                                               string title,
                                               string stateSuffix,
                                               string tooltip)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(prefabsProperty,
                                                                                  title,
                                                                                  FoldoutStateSuffixPrefix + stateSuffix,
                                                                                  true);
        foldout.tooltip = tooltip;
        foldout.style.marginTop = 4f;
        foldout.style.marginBottom = 4f;
        return foldout;
    }

    /// <summary>
    /// Marks the active preset as modified without rebuilding list or detail UI.
    /// </summary>
    /// <param name="panel">Visual preset panel that owns the active serialized preset.</param>
    private static void MarkPanelDirty(EnemyVisualPresetsPanel panel)
    {
        if (panel == null || panel.PresetSerializedObject == null)
            return;

        Object targetObject = panel.PresetSerializedObject.targetObject;

        if (targetObject != null)
            EditorUtility.SetDirty(targetObject);

        EnemyManagementDraftSession.MarkDirty();
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Adds warnings for authored hit VFX values without mutating serialized data.
    /// </summary>
    /// <param name="prefabsProperty">Serialized prefab settings block.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    private static void RefreshHitVfxWarnings(SerializedProperty prefabsProperty, VisualElement container)
    {
        if (container == null)
            return;

        container.Clear();

        if (prefabsProperty == null)
            return;

        EnemyVisualPresetsPanelSectionsUtility.AddNonPositiveValueWarning(prefabsProperty,
                                                                          container,
                                                                          "hitVfxLifetimeSeconds",
                                                                          "Hit VFX Lifetime Seconds should be greater than zero.");
        EnemyVisualPresetsPanelSectionsUtility.AddNonPositiveValueWarning(prefabsProperty,
                                                                          container,
                                                                          "hitVfxScaleMultiplier",
                                                                          "Hit VFX Scale Multiplier should be greater than zero.");
        AddInvalidVector3Warning(prefabsProperty,
                                 container,
                                 "hitVfxSpawnOffset",
                                 "Hit VFX Spawn Offset contains invalid numeric values.");
    }

    /// <summary>
    /// Adds a warning when a Vector3 property contains NaN or Infinity components.
    /// </summary>
    /// <param name="parentProperty">Serialized parent object.</param>
    /// <param name="container">Parent element receiving warning boxes.</param>
    /// <param name="relativePropertyName">Relative Vector3 property name.</param>
    /// <param name="message">Warning text.</param>
    private static void AddInvalidVector3Warning(SerializedProperty parentProperty,
                                                 VisualElement container,
                                                 string relativePropertyName,
                                                 string message)
    {
        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null)
            return;

        Vector3 value = property.vector3Value;

        if (!IsInvalidFloat(value.x) && !IsInvalidFloat(value.y) && !IsInvalidFloat(value.z))
            return;

        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Returns whether a float cannot be safely propagated to runtime ECS.
    /// </summary>
    /// <param name="value">Float value to inspect.</param>
    /// <returns>True when the value is NaN or Infinity.</returns>
    private static bool IsInvalidFloat(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value);
    }
    #endregion

    #endregion
}
