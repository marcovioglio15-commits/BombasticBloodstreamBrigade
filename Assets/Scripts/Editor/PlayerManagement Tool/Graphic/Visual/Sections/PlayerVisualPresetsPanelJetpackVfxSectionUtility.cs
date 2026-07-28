using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds intelligent Player Management Tool controls for a -authored Visual Player Jetpack VFX.
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
        foldout.tooltip = "Controls a Jetpack VFX GameObject already positioned by s inside the Visual Player hierarchy.";

        if (panel == null || panel.PresetSerializedObject == null)
            return foldout;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty runtimePrefabProperty = serializedObject.FindProperty("runtimeVisualBridgePrefab");
        SerializedProperty settingsProperty = serializedObject.FindProperty("playerJetpackVfx");
        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");

        if (settingsProperty == null)
        {
            foldout.Add(new HelpBox("Player Jetpack VFX settings are missing from the selected Visual Preset.", HelpBoxMessageType.Warning));
            return foldout;
        }

        SerializedProperty runtimeReferenceProperty = settingsProperty.FindPropertyRelative("runtimeReference");
        SerializedProperty activationModeProperty = settingsProperty.FindPropertyRelative("activationMode");
        SerializedProperty movementThresholdProperty = settingsProperty.FindPropertyRelative("movementSpeedThreshold");
        SerializedProperty rotationThresholdProperty = settingsProperty.FindPropertyRelative("rotationSpeedThresholdDegrees");
        SerializedProperty scaleWithMovementSpeedProperty = settingsProperty.FindPropertyRelative("scaleWithMovementSpeed");
        SerializedProperty speedForMaximumScaleProperty = settingsProperty.FindPropertyRelative("speedForMaximumScale");
        SerializedProperty normalScaleSpeedPercentProperty = settingsProperty.FindPropertyRelative("normalScaleSpeedPercent");
        SerializedProperty scaleVariationPercentProperty = settingsProperty.FindPropertyRelative("scaleVariationPercent");
        VisualElement details = new VisualElement();
        VisualElement movementThresholdContainer = new VisualElement();
        VisualElement rotationThresholdContainer = new VisualElement();
        VisualElement movementScaleContainer = new VisualElement();
        VisualElement warnings = new VisualElement();

        AddScalableField(foldout,
                         runtimeReferenceProperty,
                         scalingRulesProperty,
                         "Runtime Reference",
                         "Prefab-relative path or unique GameObject name resolving the Jetpack VFX already positioned inside the Visual Player. Token formulas can swap the reference.",
                         true);
        AddScalableField(details,
                         activationModeProperty,
                         scalingRulesProperty,
                         "Activation Mode",
                         "Controls whether the Jetpack VFX is always visible or only visible while the player moves, rotates, or performs either activity.",
                         false);
        AddScalableField(movementThresholdContainer,
                         movementThresholdProperty,
                         scalingRulesProperty,
                         "Movement Speed Threshold",
                         "Minimum player movement speed in world units per second required by movement-based activation modes.",
                         false);
        AddScalableField(rotationThresholdContainer,
                         rotationThresholdProperty,
                         scalingRulesProperty,
                         "Rotation Speed Threshold",
                         "Minimum player angular speed in degrees per second required by rotation-based activation modes.",
                         false);
        AddScalableField(details,
                         scaleWithMovementSpeedProperty,
                         scalingRulesProperty,
                         "Scale With Movement Speed",
                         "When enabled, shrinks or grows the Jetpack VFX around its -authored local scale according to current player movement speed.",
                         false);
        AddScalableField(movementScaleContainer,
                         speedForMaximumScaleProperty,
                         scalingRulesProperty,
                         "Speed For Maximum Scale",
                         "Player movement speed in world units per second at which the Jetpack VFX reaches its maximum configured size.",
                         false);
        AddScalableField(movementScaleContainer,
                         normalScaleSpeedPercentProperty,
                         scalingRulesProperty,
                         "Normal Scale Speed Percent",
                         "Percentage of Speed For Maximum Scale at which the Jetpack VFX uses its -authored local scale.",
                         false);
        AddScalableField(movementScaleContainer,
                         scaleVariationPercentProperty,
                         scalingRulesProperty,
                         "Scale Variation Percent",
                         "Total scale variation across the full zero-to-Speed For Maximum Scale range. For example, Normal Scale Speed Percent 50 and Scale Variation Percent 100 produce 50% scale at rest, authored scale at half the reference speed, and 150% scale at the reference speed.",
                         false);
        details.Add(movementThresholdContainer);
        details.Add(rotationThresholdContainer);
        details.Add(movementScaleContainer);
        foldout.Add(details);
        foldout.Add(warnings);

        Refresh();
        TrackRefresh(foldout, runtimePrefabProperty, Refresh);
        TrackRefresh(foldout, runtimeReferenceProperty, Refresh);
        TrackRefresh(foldout, activationModeProperty, Refresh);
        TrackRefresh(foldout, movementThresholdProperty, Refresh);
        TrackRefresh(foldout, rotationThresholdProperty, Refresh);
        TrackRefresh(foldout, scaleWithMovementSpeedProperty, Refresh);
        TrackRefresh(foldout, speedForMaximumScaleProperty, Refresh);
        TrackRefresh(foldout, normalScaleSpeedPercentProperty, Refresh);
        TrackRefresh(foldout, scaleVariationPercentProperty, Refresh);
        return foldout;

        void Refresh()
        {
            bool hasRuntimeReference = runtimeReferenceProperty != null &&
                                       !string.IsNullOrWhiteSpace(runtimeReferenceProperty.stringValue);
            PlayerJetpackVfxActivationMode activationMode = activationModeProperty != null
                ? (PlayerJetpackVfxActivationMode)activationModeProperty.enumValueIndex
                : PlayerJetpackVfxActivationMode.WhileMoving;
            bool scaleWithMovementSpeed = scaleWithMovementSpeedProperty != null &&
                                          scaleWithMovementSpeedProperty.boolValue;
            details.style.display = hasRuntimeReference ? DisplayStyle.Flex : DisplayStyle.None;
            movementThresholdContainer.style.display = UsesMovement(activationMode) ? DisplayStyle.Flex : DisplayStyle.None;
            rotationThresholdContainer.style.display = UsesRotation(activationMode) ? DisplayStyle.Flex : DisplayStyle.None;
            movementScaleContainer.style.display = scaleWithMovementSpeed ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(warnings,
                            runtimePrefabProperty,
                            runtimeReferenceProperty,
                            activationMode,
                            activationModeProperty,
                            movementThresholdProperty,
                            rotationThresholdProperty,
                            scaleWithMovementSpeed,
                            speedForMaximumScaleProperty,
                            normalScaleSpeedPercentProperty,
                            scaleVariationPercentProperty);
        }
    }
    #endregion

    #region Field Construction
    /// <summary>
    /// Adds one unified-formula Add Scaling field.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized target property.</param>
    /// <param name="scalingRulesProperty">Visual preset Add Scaling rule list.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="tooltip">Field behavior description.</param>
    /// <param name="allowTokenScaling">True when string token formulas should be enabled.</param>
    private static void AddScalableField(VisualElement parent,
                                         SerializedProperty property,
                                         SerializedProperty scalingRulesProperty,
                                         string label,
                                         string tooltip,
                                         bool allowTokenScaling)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label,
                                                                           null,
                                                                           allowTokenScaling);
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
    /// <param name="runtimePrefabProperty">Serialized Visual Player prefab property.</param>
    /// <param name="runtimeReferenceProperty">Serialized prefab-relative Jetpack VFX reference.</param>
    /// <param name="activationMode">Current Jetpack VFX activation mode.</param>
    /// <param name="activationModeProperty">Serialized activity mode property.</param>
    /// <param name="movementThresholdProperty">Serialized movement threshold property.</param>
    /// <param name="rotationThresholdProperty">Serialized rotation threshold property.</param>
    /// <param name="scaleWithMovementSpeed">True when movement-speed scale controls are enabled.</param>
    /// <param name="speedForMaximumScaleProperty">Serialized custom speed at which the VFX reaches maximum configured size.</param>
    /// <param name="normalScaleSpeedPercentProperty">Serialized percentage of the custom reference speed preserving the authored scale.</param>
    /// <param name="scaleVariationPercentProperty">Serialized total scale variation percentage.</param>
    private static void RefreshWarnings(VisualElement warnings,
                                        SerializedProperty runtimePrefabProperty,
                                        SerializedProperty runtimeReferenceProperty,
                                        PlayerJetpackVfxActivationMode activationMode,
                                        SerializedProperty activationModeProperty,
                                        SerializedProperty movementThresholdProperty,
                                        SerializedProperty rotationThresholdProperty,
                                        bool scaleWithMovementSpeed,
                                        SerializedProperty speedForMaximumScaleProperty,
                                        SerializedProperty normalScaleSpeedPercentProperty,
                                        SerializedProperty scaleVariationPercentProperty)
    {
        warnings.Clear();
        GameObject runtimePrefab = runtimePrefabProperty != null
            ? runtimePrefabProperty.objectReferenceValue as GameObject
            : null;
        string runtimeReference = runtimeReferenceProperty != null
            ? runtimeReferenceProperty.stringValue
            : string.Empty;

        if (string.IsNullOrWhiteSpace(runtimeReference))
        {
            warnings.Add(new HelpBox("Jetpack VFX is disabled. Place it inside the Visual Player and enter its prefab-relative path or unique GameObject name to enable it.", HelpBoxMessageType.Info));
            return;
        }

        if (runtimePrefab == null)
            warnings.Add(new HelpBox("Assign a Runtime Visual Bridge Prefab to validate the -authored Jetpack VFX reference.", HelpBoxMessageType.Info));

        string normalizedReference = runtimeReference.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedReference) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            warnings.Add(new HelpBox("Runtime Reference exceeds the ECS fixed-string capacity.", HelpBoxMessageType.Warning));
        else if (runtimePrefab != null &&
                 PlayerVisualPresetsPanelWeaponVisualWarningsUtility.ResolveReferenceObject(runtimePrefab,
                                                                                            normalizedReference) == null)
            warnings.Add(new HelpBox("Runtime Reference does not resolve inside the assigned Runtime Visual Bridge Prefab.", HelpBoxMessageType.Warning));

        if (activationModeProperty != null &&
            (activationModeProperty.intValue < (int)PlayerJetpackVfxActivationMode.Always ||
             activationModeProperty.intValue > (int)PlayerJetpackVfxActivationMode.WhileMovingOrRotating))
            warnings.Add(new HelpBox("Activation Mode contains an unsupported enum value.", HelpBoxMessageType.Warning));

        if (UsesMovement(activationMode) &&
            movementThresholdProperty != null &&
            (!IsFinite(movementThresholdProperty.floatValue) || movementThresholdProperty.floatValue < 0f))
            warnings.Add(new HelpBox("Movement Speed Threshold should be finite and non-negative.", HelpBoxMessageType.Warning));

        if (UsesRotation(activationMode) &&
            rotationThresholdProperty != null &&
            (!IsFinite(rotationThresholdProperty.floatValue) || rotationThresholdProperty.floatValue < 0f))
            warnings.Add(new HelpBox("Rotation Speed Threshold should be finite and non-negative.", HelpBoxMessageType.Warning));

        if (scaleWithMovementSpeed &&
            speedForMaximumScaleProperty != null &&
            (!IsFinite(speedForMaximumScaleProperty.floatValue) || speedForMaximumScaleProperty.floatValue <= 0f))
            warnings.Add(new HelpBox("Speed For Maximum Scale should be finite and greater than zero.", HelpBoxMessageType.Warning));

        if (scaleWithMovementSpeed &&
            normalScaleSpeedPercentProperty != null &&
            (!IsFinite(normalScaleSpeedPercentProperty.floatValue) ||
             normalScaleSpeedPercentProperty.floatValue < 0f ||
             normalScaleSpeedPercentProperty.floatValue > 100f))
            warnings.Add(new HelpBox("Normal Scale Speed Percent should be finite and between zero and one hundred.", HelpBoxMessageType.Warning));

        if (scaleWithMovementSpeed &&
            scaleVariationPercentProperty != null &&
            (!IsFinite(scaleVariationPercentProperty.floatValue) || scaleVariationPercentProperty.floatValue < 0f))
            warnings.Add(new HelpBox("Scale Variation Percent should be finite and non-negative.", HelpBoxMessageType.Warning));

        if (scaleWithMovementSpeed &&
            normalScaleSpeedPercentProperty != null &&
            scaleVariationPercentProperty != null &&
            IsFinite(normalScaleSpeedPercentProperty.floatValue) &&
            IsFinite(scaleVariationPercentProperty.floatValue) &&
            1f - normalScaleSpeedPercentProperty.floatValue * 0.01f * scaleVariationPercentProperty.floatValue * 0.01f <= 0f)
            warnings.Add(new HelpBox("Configured variation reaches a non-positive scale at zero speed and will use the runtime safety minimum.", HelpBoxMessageType.Warning));
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
