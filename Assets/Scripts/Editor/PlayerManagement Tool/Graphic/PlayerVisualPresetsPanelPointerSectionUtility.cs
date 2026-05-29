using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the "Visual Pointer" subsection of the player visual preset panel, exposing the aiming laser pointer fields with Add Scaling, conditional display and type-coherent warnings.
/// </summary>
internal static class PlayerVisualPresetsPanelPointerSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Visual Pointer subsection content for the provided panel.
    /// </summary>
    /// <param name="panel">Owning player visual preset panel providing the serialized preset.</param>
    /// <returns>Configured subsection element, or an empty foldout when no preset is selected.</returns>
    public static VisualElement BuildVisualPointerSubSection(PlayerVisualPresetsPanel panel)
    {
        Foldout container = ManagementToolFoldoutStateUtility.CreateFoldout("Visual Pointer",
                                                                            "NashCore.PlayerManagement.Visual.SubSection.VisualPointer",
                                                                            true);
        container.style.marginTop = 4f;

        if (panel == null || panel.PresetSerializedObject == null)
            return container;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;
        SerializedProperty scalingRulesProperty = presetSerializedObject.FindProperty("scalingRules");
        SerializedProperty enablePointerProperty = presetSerializedObject.FindProperty("enablePointer");
        SerializedProperty freezeProperty = presetSerializedObject.FindProperty("freezePointerWithOrbitalProjectiles");
        SerializedProperty laserBeamProperty = presetSerializedObject.FindProperty("laserBeam");
        SerializedProperty bodyMaterialProperty = laserBeamProperty != null ? laserBeamProperty.FindPropertyRelative("bodyMaterial") : null;

        // The enable toggle gates the whole pointer, so it stays visible while the configuration fields fold away when disabled.
        AddScalableField(container, scalingRulesProperty, enablePointerProperty, "Enable Pointer",
                         "When enabled, a precision laser aiming pointer is rendered straight out of the weapon muzzle along the current shot direction.");

        VisualElement detailsContainer = new VisualElement();
        VisualElement warningsContainer = new VisualElement();
        container.Add(detailsContainer);
        container.Add(warningsContainer);

        // Build the customizable pointer fields top to bottom, capturing the frozen-length field for its own conditional display.
        AddScalableField(detailsContainer, scalingRulesProperty, presetSerializedObject.FindProperty("pointerVisualPresetId"), "Pointer Palette",
                         "Laser Beam visual preset ID whose palette colors are reused to tint the aiming pointer.");
        AddScalableField(detailsContainer, scalingRulesProperty, presetSerializedObject.FindProperty("pointerWidth"), "Pointer Width",
                         "Rendered beam diameter of the aiming pointer in world units.");
        AddScalableField(detailsContainer, scalingRulesProperty, presetSerializedObject.FindProperty("pointerLengthMultiplier"), "Pointer Length Multiplier",
                         "Multiplier applied to the projectile range-derived length so the pointer can be shortened or extended relative to the real shot reach.");
        AddScalableField(detailsContainer, scalingRulesProperty, presetSerializedObject.FindProperty("pointerMaxLength"), "Pointer Max Length",
                         "Optional hard cap on the rendered pointer length in world units. Set 0 to follow the resolved projectile range without a cap.");
        AddScalableField(detailsContainer, scalingRulesProperty, presetSerializedObject.FindProperty("pointerOpacity"), "Pointer Opacity",
                         "Opacity multiplier applied to the aiming pointer body. Use lower values for a subtle sight line.");
        AddScalableField(detailsContainer, scalingRulesProperty, presetSerializedObject.FindProperty("pointerVerticalLift"), "Pointer Vertical Lift",
                         "Vertical lift in world units applied to the aiming pointer to avoid floor z-fighting.");
        AddScalableField(detailsContainer, scalingRulesProperty, freezeProperty, "Freeze With Orbital Projectiles",
                         "When enabled, the pointer length stops adapting to the projectile range and stays fixed while the Orbital Projectiles power-up is active.");
        SerializedProperty frozenLengthProperty = presetSerializedObject.FindProperty("pointerOrbitalFrozenLength");
        VisualElement frozenLengthField = AddScalableField(detailsContainer, scalingRulesProperty, frozenLengthProperty, "Orbital Frozen Length",
                                                          "Fixed pointer length in world units used while the Orbital Projectiles power-up is active and freezing is enabled. Set 0 to keep the authored base shot range instead.");

        // Conditional display: pointer details only matter when the pointer is enabled, and the frozen length only when freezing is on.
        RefreshConditionalDisplay(enablePointerProperty, freezeProperty, detailsContainer, frozenLengthField);
        RefreshWarnings(warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);

        container.TrackPropertyValue(enablePointerProperty, changedProperty =>
        {
            RefreshConditionalDisplay(changedProperty, freezeProperty, detailsContainer, frozenLengthField);
            RefreshWarnings(warningsContainer, changedProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        });

        if (freezeProperty != null)
        {
            container.TrackPropertyValue(freezeProperty, changedProperty =>
            {
                RefreshConditionalDisplay(enablePointerProperty, changedProperty, detailsContainer, frozenLengthField);
                RefreshWarnings(warningsContainer, enablePointerProperty, changedProperty, bodyMaterialProperty, presetSerializedObject);
            });
        }

        // Refresh warnings whenever a value that can become incoherent changes.
        TrackWarningSource(container, presetSerializedObject.FindProperty("pointerWidth"), warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        TrackWarningSource(container, presetSerializedObject.FindProperty("pointerLengthMultiplier"), warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        TrackWarningSource(container, presetSerializedObject.FindProperty("pointerMaxLength"), warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        TrackWarningSource(container, presetSerializedObject.FindProperty("pointerOpacity"), warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        TrackWarningSource(container, presetSerializedObject.FindProperty("pointerVerticalLift"), warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        TrackWarningSource(container, frozenLengthProperty, warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);

        if (bodyMaterialProperty != null)
            TrackWarningSource(container, bodyMaterialProperty, warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);

        return container;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates a scalable field for one property and appends it to the target container.
    /// </summary>
    /// <param name="target">Destination container.</param>
    /// <param name="scalingRulesProperty">Serialized scaling-rule list backing Add Scaling state.</param>
    /// <param name="property">Serialized property to render.</param>
    /// <param name="label">Field label.</param>
    /// <param name="tooltip">Field tooltip.</param>
    /// <returns>The created field element, or null when the property is missing.</returns>
    private static VisualElement AddScalableField(VisualElement target,
                                                  SerializedProperty scalingRulesProperty,
                                                  SerializedProperty property,
                                                  string label,
                                                  string tooltip)
    {
        if (target == null || property == null)
            return null;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        field.tooltip = tooltip;
        target.Add(field);
        return field;
    }

    /// <summary>
    /// Tracks one serialized property so warnings refresh whenever it changes.
    /// </summary>
    /// <param name="root">Element used to host the property tracker.</param>
    /// <param name="trackedProperty">Property to observe.</param>
    /// <param name="warningsContainer">Container rebuilt with the current warnings.</param>
    /// <param name="enablePointerProperty">Enable-pointer toggle property.</param>
    /// <param name="freezeProperty">Freeze-with-orbital toggle property.</param>
    /// <param name="bodyMaterialProperty">Reused Laser Beam body material property.</param>
    /// <param name="presetSerializedObject">Serialized visual preset.</param>
    private static void TrackWarningSource(VisualElement root,
                                           SerializedProperty trackedProperty,
                                           VisualElement warningsContainer,
                                           SerializedProperty enablePointerProperty,
                                           SerializedProperty freezeProperty,
                                           SerializedProperty bodyMaterialProperty,
                                           SerializedObject presetSerializedObject)
    {
        if (root == null || trackedProperty == null)
            return;

        root.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            RefreshWarnings(warningsContainer, enablePointerProperty, freezeProperty, bodyMaterialProperty, presetSerializedObject);
        });
    }

    /// <summary>
    /// Updates the visibility of the pointer detail fields and the orbital frozen-length field based on the current toggles.
    /// </summary>
    /// <param name="enablePointerProperty">Enable-pointer toggle property.</param>
    /// <param name="freezeProperty">Freeze-with-orbital toggle property.</param>
    /// <param name="detailsContainer">Container holding the conditional pointer detail fields.</param>
    /// <param name="frozenLengthField">Orbital frozen-length field shown only while freezing is enabled.</param>
    private static void RefreshConditionalDisplay(SerializedProperty enablePointerProperty,
                                                  SerializedProperty freezeProperty,
                                                  VisualElement detailsContainer,
                                                  VisualElement frozenLengthField)
    {
        bool pointerEnabled = enablePointerProperty != null && enablePointerProperty.boolValue;

        if (detailsContainer != null)
            detailsContainer.style.display = pointerEnabled ? DisplayStyle.Flex : DisplayStyle.None;

        if (frozenLengthField == null)
            return;

        bool freezeEnabled = freezeProperty != null && freezeProperty.boolValue;
        frozenLengthField.style.display = pointerEnabled && freezeEnabled ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Rebuilds the warning boxes for the pointer subsection using only type-coherent incoherence checks, without snapping any authored value.
    /// </summary>
    /// <param name="warningsContainer">Container rebuilt with the current warnings.</param>
    /// <param name="enablePointerProperty">Enable-pointer toggle property.</param>
    /// <param name="freezeProperty">Freeze-with-orbital toggle property.</param>
    /// <param name="bodyMaterialProperty">Reused Laser Beam body material property.</param>
    /// <param name="presetSerializedObject">Serialized visual preset.</param>
    private static void RefreshWarnings(VisualElement warningsContainer,
                                        SerializedProperty enablePointerProperty,
                                        SerializedProperty freezeProperty,
                                        SerializedProperty bodyMaterialProperty,
                                        SerializedObject presetSerializedObject)
    {
        if (warningsContainer == null)
            return;

        warningsContainer.Clear();

        if (enablePointerProperty == null || !enablePointerProperty.boolValue)
            return;

        // The pointer reuses the Laser Beam body material, so a missing reference blocks rendering entirely.
        if (bodyMaterialProperty == null || bodyMaterialProperty.objectReferenceValue == null)
            AddWarning(warningsContainer, "Assign a Laser Beam Body Material (VFX subsection) so the Visual Pointer can be rendered.");

        AddNonPositiveFloatWarning(warningsContainer, presetSerializedObject.FindProperty("pointerWidth"), "Pointer Width should be greater than zero.");
        AddNonPositiveFloatWarning(warningsContainer, presetSerializedObject.FindProperty("pointerLengthMultiplier"), "Pointer Length Multiplier should be greater than zero.");
        AddNegativeFloatWarning(warningsContainer, presetSerializedObject.FindProperty("pointerMaxLength"), "Pointer Max Length should not be negative. Use 0 to disable the cap.");
        AddNegativeFloatWarning(warningsContainer, presetSerializedObject.FindProperty("pointerVerticalLift"), "Pointer Vertical Lift should not be negative.");
        AddOpacityWarning(warningsContainer, presetSerializedObject.FindProperty("pointerOpacity"));

        // The frozen length only matters while freezing is enabled, so its warning is scoped to that case.
        if (freezeProperty != null && freezeProperty.boolValue)
            AddNegativeFloatWarning(warningsContainer, presetSerializedObject.FindProperty("pointerOrbitalFrozenLength"), "Orbital Frozen Length should not be negative. Use 0 to fall back to the base shot range.");
    }

    /// <summary>
    /// Adds a warning when a float property is zero or negative.
    /// </summary>
    /// <param name="container">Warning container.</param>
    /// <param name="property">Float property to inspect.</param>
    /// <param name="message">Warning message shown when the value is not strictly positive.</param>
    private static void AddNonPositiveFloatWarning(VisualElement container, SerializedProperty property, string message)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Float)
            return;

        if (property.floatValue > 0f)
            return;

        AddWarning(container, message);
    }

    /// <summary>
    /// Adds a warning when a float property is negative.
    /// </summary>
    /// <param name="container">Warning container.</param>
    /// <param name="property">Float property to inspect.</param>
    /// <param name="message">Warning message shown when the value is below zero.</param>
    private static void AddNegativeFloatWarning(VisualElement container, SerializedProperty property, string message)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Float)
            return;

        if (property.floatValue >= 0f)
            return;

        AddWarning(container, message);
    }

    /// <summary>
    /// Adds a warning when an opacity property leaves the usable 0-1 range, since the runtime clamps it without altering the authored value.
    /// </summary>
    /// <param name="container">Warning container.</param>
    /// <param name="property">Opacity float property to inspect.</param>
    private static void AddOpacityWarning(VisualElement container, SerializedProperty property)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Float)
            return;

        if (property.floatValue > 0f && property.floatValue <= 1f)
            return;

        AddWarning(container, "Pointer Opacity is expected within the 0-1 range; out-of-range values are clamped at runtime.");
    }

    /// <summary>
    /// Appends one warning help box to the warning container.
    /// </summary>
    /// <param name="container">Warning container.</param>
    /// <param name="message">Warning message text.</param>
    private static void AddWarning(VisualElement container, string message)
    {
        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion
}
