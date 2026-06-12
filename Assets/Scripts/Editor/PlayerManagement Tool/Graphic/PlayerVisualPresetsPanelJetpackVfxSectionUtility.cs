using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds intelligent Player Management Tool controls for scalable player Jetpack VFX settings.
/// </summary>
internal static class PlayerVisualPresetsPanelJetpackVfxSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Jetpack VFX foldout owned by the Visual Preset VFX subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured player Jetpack VFX foldout.</returns>
    public static VisualElement Build(PlayerVisualPresetsPanel panel)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreateFoldout("Player Jetpack VFX",
                                                                          "NashCore.PlayerManagement.Visual.VFX.PlayerJetpack",
                                                                          true);
        foldout.tooltip = "Configures one looping VFX attached to the player and displayed always, while moving, while rotating, or during either activity.";

        if (panel == null || panel.PresetSerializedObject == null)
            return foldout;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settingsProperty = serializedObject.FindProperty("playerJetpackVfx");
        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");

        if (settingsProperty == null)
        {
            foldout.Add(new HelpBox("Player Jetpack VFX settings are missing from the selected Visual Preset.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty prefabProperty = settingsProperty.FindPropertyRelative("vfxPrefab");
        SerializedProperty activationModeProperty = settingsProperty.FindPropertyRelative("activationMode");
        SerializedProperty offsetProperty = settingsProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty scaleProperty = settingsProperty.FindPropertyRelative("scaleMultiplier");
        SerializedProperty movementThresholdProperty = settingsProperty.FindPropertyRelative("movementSpeedThreshold");
        SerializedProperty rotationThresholdProperty = settingsProperty.FindPropertyRelative("rotationSpeedThresholdDegrees");
        VisualElement details = new VisualElement();
        VisualElement movementThresholdContainer = new VisualElement();
        VisualElement rotationThresholdContainer = new VisualElement();
        VisualElement warnings = new VisualElement();

        AddPropertyField(foldout,
                         prefabProperty,
                         "VFX Prefab",
                         "Optional looping VFX prefab attached to the player while the configured activity condition is valid.");
        AddScalableField(details,
                         activationModeProperty,
                         scalingRulesProperty,
                         "Activation Mode",
                         "Controls whether the Jetpack VFX is always visible or only visible while the player moves, rotates, or performs either activity.");
        AddScalableField(details,
                         offsetProperty,
                         scalingRulesProperty,
                         "Spawn Offset",
                         "Player-local offset applied to the Jetpack VFX. The offset rotates with the player.");
        AddScalableField(details,
                         scaleProperty,
                         scalingRulesProperty,
                         "Scale Multiplier",
                         "Uniform scale multiplier applied to the attached Jetpack VFX instance.");
        AddScalableField(movementThresholdContainer,
                         movementThresholdProperty,
                         scalingRulesProperty,
                         "Movement Speed Threshold",
                         "Minimum player movement speed in world units per second required by movement-based activation modes.");
        AddScalableField(rotationThresholdContainer,
                         rotationThresholdProperty,
                         scalingRulesProperty,
                         "Rotation Speed Threshold",
                         "Minimum player angular speed in degrees per second required by rotation-based activation modes.");
        details.Add(movementThresholdContainer);
        details.Add(rotationThresholdContainer);
        foldout.Add(details);
        foldout.Add(warnings);

        Refresh();
        TrackRefresh(foldout, prefabProperty, Refresh);
        TrackRefresh(foldout, activationModeProperty, Refresh);
        TrackRefresh(foldout, offsetProperty, Refresh);
        TrackRefresh(foldout, scaleProperty, Refresh);
        TrackRefresh(foldout, movementThresholdProperty, Refresh);
        TrackRefresh(foldout, rotationThresholdProperty, Refresh);
        return foldout;

        void Refresh()
        {
            bool hasPrefab = prefabProperty != null && prefabProperty.objectReferenceValue != null;
            PlayerJetpackVfxActivationMode activationMode = activationModeProperty != null
                ? (PlayerJetpackVfxActivationMode)activationModeProperty.enumValueIndex
                : PlayerJetpackVfxActivationMode.WhileMoving;
            details.style.display = hasPrefab ? DisplayStyle.Flex : DisplayStyle.None;
            movementThresholdContainer.style.display = UsesMovement(activationMode) ? DisplayStyle.Flex : DisplayStyle.None;
            rotationThresholdContainer.style.display = UsesRotation(activationMode) ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(warnings,
                            hasPrefab,
                            activationMode,
                            activationModeProperty,
                            offsetProperty,
                            scaleProperty,
                            movementThresholdProperty,
                            rotationThresholdProperty);
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

    #region Conditional Display
    /// <summary>
    /// Checks whether one activation mode consumes the movement-speed threshold.
    /// </summary>
    /// <param name="activationMode">Current Jetpack VFX activation mode.</param>
    /// <returns>True when movement activity contributes to visibility.</returns>
    private static bool UsesMovement(PlayerJetpackVfxActivationMode activationMode)
    {
        return activationMode == PlayerJetpackVfxActivationMode.WhileMoving ||
               activationMode == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }

    /// <summary>
    /// Checks whether one activation mode consumes the angular-speed threshold.
    /// </summary>
    /// <param name="activationMode">Current Jetpack VFX activation mode.</param>
    /// <returns>True when rotation activity contributes to visibility.</returns>
    private static bool UsesRotation(PlayerJetpackVfxActivationMode activationMode)
    {
        return activationMode == PlayerJetpackVfxActivationMode.WhileRotating ||
               activationMode == PlayerJetpackVfxActivationMode.WhileMovingOrRotating;
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Registers an inexpensive serialized-property tracker that refreshes the Jetpack editor.
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
    /// Rebuilds Jetpack-specific authoring warnings without modifying serialized values.
    /// </summary>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="hasPrefab">True when a Jetpack VFX prefab is assigned.</param>
    /// <param name="activationMode">Current Jetpack VFX activation mode.</param>
    /// <param name="activationModeProperty">Serialized activity mode property.</param>
    /// <param name="offsetProperty">Serialized local offset property.</param>
    /// <param name="scaleProperty">Serialized uniform scale property.</param>
    /// <param name="movementThresholdProperty">Serialized movement threshold property.</param>
    /// <param name="rotationThresholdProperty">Serialized rotation threshold property.</param>
    private static void RefreshWarnings(VisualElement warnings,
                                        bool hasPrefab,
                                        PlayerJetpackVfxActivationMode activationMode,
                                        SerializedProperty activationModeProperty,
                                        SerializedProperty offsetProperty,
                                        SerializedProperty scaleProperty,
                                        SerializedProperty movementThresholdProperty,
                                        SerializedProperty rotationThresholdProperty)
    {
        warnings.Clear();

        if (!hasPrefab)
        {
            warnings.Add(new HelpBox("Assign a Player Jetpack VFX prefab to enable activity, offset, scale, and threshold controls.", HelpBoxMessageType.Info));
            return;
        }

        if (activationModeProperty != null &&
            (activationModeProperty.intValue < (int)PlayerJetpackVfxActivationMode.Always ||
             activationModeProperty.intValue > (int)PlayerJetpackVfxActivationMode.WhileMovingOrRotating))
            warnings.Add(new HelpBox("Activation Mode contains an unsupported enum value.", HelpBoxMessageType.Warning));

        if (offsetProperty != null && !IsFinite(offsetProperty.vector3Value))
            warnings.Add(new HelpBox("Spawn Offset contains an invalid numeric value.", HelpBoxMessageType.Warning));

        if (scaleProperty != null && (!IsFinite(scaleProperty.floatValue) || scaleProperty.floatValue <= 0f))
            warnings.Add(new HelpBox("Scale Multiplier should be finite and greater than zero.", HelpBoxMessageType.Warning));

        if (UsesMovement(activationMode) &&
            movementThresholdProperty != null &&
            (!IsFinite(movementThresholdProperty.floatValue) || movementThresholdProperty.floatValue < 0f))
            warnings.Add(new HelpBox("Movement Speed Threshold should be finite and non-negative.", HelpBoxMessageType.Warning));

        if (UsesRotation(activationMode) &&
            rotationThresholdProperty != null &&
            (!IsFinite(rotationThresholdProperty.floatValue) || rotationThresholdProperty.floatValue < 0f))
            warnings.Add(new HelpBox("Rotation Speed Threshold should be finite and non-negative.", HelpBoxMessageType.Warning));
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
