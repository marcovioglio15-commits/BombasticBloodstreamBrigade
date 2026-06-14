using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds intelligent Player Management Tool controls for scalable projectile-death VFX settings.
/// </summary>
internal static class PlayerVisualPresetsPanelProjectileDeathVfxSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the projectile-death VFX foldout owned by the Visual Preset VFX subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured projectile-death VFX foldout.</returns>
    public static VisualElement Build(PlayerVisualPresetsPanel panel)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout("Projectile Death VFX",
                                                                          "NashCore.PlayerManagement.Visual.VFX.ProjectileDeath",
                                                                          true);
        foldout.tooltip = "Configures one-shot VFX for projectiles that expire without a previous enemy hit and optional terminal wall impacts. Successful Bouncing Projectiles reflections never trigger these VFX.";

        if (panel == null || panel.PresetSerializedObject == null)
            return foldout;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settingsProperty = serializedObject.FindProperty("projectileDeathVfx");
        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");

        if (settingsProperty == null)
        {
            foldout.Add(new HelpBox("Projectile Death VFX settings are missing from the selected Visual Preset.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty rangeOrLifetimeProperty = settingsProperty.FindPropertyRelative("rangeOrLifetime");
        SerializedProperty terminalWallHitProperty = settingsProperty.FindPropertyRelative("terminalWallHit");
        SerializedProperty rangeOrLifetimePrefabProperty = rangeOrLifetimeProperty != null
            ? rangeOrLifetimeProperty.FindPropertyRelative("vfxPrefab")
            : null;

        BuildEvent(panel,
                   foldout,
                   rangeOrLifetimeProperty,
                   scalingRulesProperty,
                   "Range / Lifetime Expiry",
                   "RangeOrLifetime",
                   "Spawns only when a projectile reaches its configured range or lifetime without any previous valid enemy hit.",
                   null);
        BuildEvent(panel,
                   foldout,
                   terminalWallHitProperty,
                   scalingRulesProperty,
                   "Terminal Wall Hit",
                   "TerminalWallHit",
                   "Spawns only when a wall impact terminates the projectile. Successful Bouncing Projectiles reflections are explicitly excluded.",
                   rangeOrLifetimePrefabProperty);
        return foldout;
    }
    #endregion

    #region Event Construction
    /// <summary>
    /// Builds one scalable projectile-death event editor with conditional prefab details and warnings.
    /// </summary>
    /// <param name="panel">Owning visual preset panel.</param>
    /// <param name="parent">Parent container receiving the event foldout.</param>
    /// <param name="eventProperty">Serialized projectile-death event property.</param>
    /// <param name="scalingRulesProperty">Visual preset Add Scaling rule list.</param>
    /// <param name="title">User-facing event title.</param>
    /// <param name="stateSuffix">Stable foldout-state key suffix.</param>
    /// <param name="tooltip">Event behavior description.</param>
    /// <param name="fallbackPrefabProperty">Optional fallback prefab property used when this event has no override.</param>
    private static void BuildEvent(PlayerVisualPresetsPanel panel,
                                   VisualElement parent,
                                   SerializedProperty eventProperty,
                                   SerializedProperty scalingRulesProperty,
                                   string title,
                                   string stateSuffix,
                                   string tooltip,
                                   SerializedProperty fallbackPrefabProperty)
    {
        Foldout eventFoldout = ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                                               "NashCore.PlayerManagement.Visual.VFX.ProjectileDeath." + stateSuffix,
                                                                               true);
        eventFoldout.tooltip = tooltip;
        parent.Add(eventFoldout);

        if (eventProperty == null)
        {
            eventFoldout.Add(new HelpBox(title + " settings are missing.", HelpBoxMessageType.Warning));
            return;
        }

        SerializedProperty enabledProperty = eventProperty.FindPropertyRelative("enabled");
        SerializedProperty prefabProperty = eventProperty.FindPropertyRelative("vfxPrefab");
        SerializedProperty offsetProperty = eventProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty scaleProperty = eventProperty.FindPropertyRelative("scaleMultiplier");
        SerializedProperty lifetimeProperty = eventProperty.FindPropertyRelative("lifetimeSeconds");
        VisualElement eventBody = new VisualElement();
        VisualElement details = new VisualElement();
        VisualElement warnings = new VisualElement();

        AddScalableField(eventFoldout,
                         enabledProperty,
                         scalingRulesProperty,
                         "Enabled",
                         tooltip);
        AddPropertyField(eventBody,
                         prefabProperty,
                         fallbackPrefabProperty != null ? "VFX Prefab Override" : "VFX Prefab",
                         fallbackPrefabProperty != null
                             ? "Optional one-shot VFX override for terminal wall hits. Leave empty to reuse the Range / Lifetime Expiry prefab."
                             : "One-shot VFX prefab spawned for this projectile despawn occasion.");
        AddScalableField(details,
                         offsetProperty,
                         scalingRulesProperty,
                         "Spawn Offset",
                         "Projectile-local offset applied at the final projectile pose. The offset scales with the current projectile size.");
        AddScalableField(details,
                         scaleProperty,
                         scalingRulesProperty,
                         "Scale Multiplier",
                         "Uniform VFX scale multiplier applied on top of the current projectile size.");
        AddScalableField(details,
                         lifetimeProperty,
                         scalingRulesProperty,
                         "Lifetime Seconds",
                         "Lifetime in seconds before the one-shot VFX returns to the managed VFX pool.");
        eventBody.Add(details);
        eventBody.Add(warnings);
        eventFoldout.Add(eventBody);

        Refresh();
        TrackRefresh(eventFoldout, enabledProperty, Refresh);
        TrackRefresh(eventFoldout, prefabProperty, Refresh);
        TrackRefresh(eventFoldout, fallbackPrefabProperty, Refresh);
        TrackRefresh(eventFoldout, offsetProperty, Refresh);
        TrackRefresh(eventFoldout, scaleProperty, Refresh);
        TrackRefresh(eventFoldout, lifetimeProperty, Refresh);

        void Refresh()
        {
            bool enabled = enabledProperty != null && enabledProperty.boolValue;
            bool hasPrefab = prefabProperty != null && prefabProperty.objectReferenceValue != null;
            bool hasFallbackPrefab = fallbackPrefabProperty != null && fallbackPrefabProperty.objectReferenceValue != null;
            eventBody.style.display = enabled || hasPrefab ? DisplayStyle.Flex : DisplayStyle.None;
            details.style.display = hasPrefab || enabled && hasFallbackPrefab ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(warnings,
                            enabled,
                            hasPrefab || hasFallbackPrefab,
                            offsetProperty,
                            scaleProperty,
                            lifetimeProperty);
        }
    }
    #endregion

    #region Field Construction
    /// <summary>
    /// Adds one standard serialized property field and marks the draft session dirty on edits.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="tooltip">Field behavior description.</param>
    private static void AddPropertyField(VisualElement parent,
                                         SerializedProperty property,
                                         string label,
                                         string tooltip)
    {
        if (parent == null || property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.tooltip = tooltip;
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        parent.Add(field);
    }

    /// <summary>
    /// Adds one unified-formula Add Scaling field.
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
    /// Registers an inexpensive serialized-property tracker that refreshes one event editor.
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
    /// Rebuilds event-specific authoring warnings without modifying serialized values.
    /// </summary>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="enabled">Current authored event-enabled value.</param>
    /// <param name="hasPrefab">True when an event prefab is assigned.</param>
    /// <param name="offsetProperty">Serialized offset property.</param>
    /// <param name="scaleProperty">Serialized scale property.</param>
    /// <param name="lifetimeProperty">Serialized lifetime property.</param>
    private static void RefreshWarnings(VisualElement warnings,
                                        bool enabled,
                                        bool hasPrefab,
                                        SerializedProperty offsetProperty,
                                        SerializedProperty scaleProperty,
                                        SerializedProperty lifetimeProperty)
    {
        warnings.Clear();

        if (enabled && !hasPrefab)
            warnings.Add(new HelpBox("This event is enabled but no VFX prefab is assigned.", HelpBoxMessageType.Warning));

        if (!hasPrefab)
            return;

        if (offsetProperty != null && !IsFinite(offsetProperty.vector3Value))
            warnings.Add(new HelpBox("Spawn Offset contains an invalid numeric value.", HelpBoxMessageType.Warning));

        if (scaleProperty != null && (!IsFinite(scaleProperty.floatValue) || scaleProperty.floatValue <= 0f))
            warnings.Add(new HelpBox("Scale Multiplier should be finite and greater than zero.", HelpBoxMessageType.Warning));

        if (lifetimeProperty != null && (!IsFinite(lifetimeProperty.floatValue) || lifetimeProperty.floatValue <= 0f))
            warnings.Add(new HelpBox("Lifetime Seconds should be finite and greater than zero.", HelpBoxMessageType.Warning));
    }

    /// <summary>
    /// Checks whether every vector component is finite.
    /// </summary>
    /// <param name="value">Vector value to inspect.</param>
    /// <returns>True when every component is finite.</returns>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>
    /// Checks whether one floating-point value is finite.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when the value is neither NaN nor infinity.</returns>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #endregion
}
