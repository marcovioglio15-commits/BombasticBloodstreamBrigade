using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds scalable UI Visual Preset controls for active power-up HUD icon, energy syringe, and charge ring visuals.
/// </summary>
internal static class PlayerVisualPresetsPanelActivePowerUpHudSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Active Power-Up HUD UI visual-preset subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured Active Power-Up HUD subsection.</returns>
    public static VisualElement Build(IPlayerVisualPresetEditorPanel panel)
    {
        Foldout root = ManagementToolFoldoutStateUtility.CreateFoldout("Active Power-Up HUD",
                                                                        "NashCore.PlayerManagement.Visual.ActivePowerUpHud",
                                                                        true);
        root.tooltip = "Configures icon cooldown, active energy syringes, requirement markers, and charge semirings.";

        if (panel == null || panel.PresetSerializedObject == null)
            return root;

        SerializedObject serializedObject = panel.PresetSerializedObject;
        SerializedProperty settings = serializedObject.FindProperty("activePowerUpHud");
        SerializedProperty scalingRules = serializedObject.FindProperty("scalingRules");

        if (settings == null)
        {
            root.Add(new HelpBox("Active Power-Up HUD settings are missing from the selected UI Visual Preset.",
                                 HelpBoxMessageType.Warning));
            return root;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        VisualElement warnings = new VisualElement();
        AddField(root, enabled, scalingRules, "Enabled", "Enables the redesigned active power-up HUD widgets.");
        BuildGeneral(details, settings, scalingRules);
        PlayerVisualPresetsPanelActivePowerUpHudEnergySyringeSectionUtility.Build(details,
                                                                                  settings.FindPropertyRelative("energySyringe"),
                                                                                  scalingRules);
        BuildRequirementMarker(details, settings.FindPropertyRelative("requirementMarker"), scalingRules);
        BuildChargeRing(details, settings.FindPropertyRelative("chargeRing"), scalingRules);
        BuildIconCooldown(details, settings.FindPropertyRelative("iconCooldown"), scalingRules);
        root.Add(details);
        root.Add(warnings);

        Refresh();
        TrackRefresh(root, enabled, Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("hideWhenPlayerMissing"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("hideEnergyWhenModuleMissing"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("hideChargeWhenModuleMissing"), Refresh);
        return root;

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshWarnings(warnings, settings);
        }
    }
    #endregion

    #region Section Construction
    /// <summary>
    /// Builds shared visibility controls for active power-up HUD widgets.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized Active Power-Up HUD root.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildGeneral(VisualElement parent,
                                     SerializedProperty settings,
                                     SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("General", "General");
        AddField(foldout, settings.FindPropertyRelative("hideWhenPlayerMissing"), scalingRules, "Hide When Player Missing", "Hides active power-up HUD widgets while no valid player entity is available.");
        AddField(foldout, settings.FindPropertyRelative("hideEnergyWhenModuleMissing"), scalingRules, "Hide Energy When Module Missing", "Hides a slot energy syringe while the equipped active power-up has no energy module.");
        AddField(foldout, settings.FindPropertyRelative("hideChargeWhenModuleMissing"), scalingRules, "Hide Charge When Module Missing", "Hides a slot charge semiring while the equipped active power-up has no hold-charge module.");
        parent.Add(foldout);
    }

    /// <summary>
    /// Builds activation requirement marker controls, showing marker tuning only when markers are enabled.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized requirement-marker settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildRequirementMarker(VisualElement parent,
                                               SerializedProperty settings,
                                               SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Activation Requirement Marker", "RequirementMarker");

        if (settings == null)
        {
            foldout.Add(new HelpBox("Activation Requirement Marker settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Shows a triangle marker on the energy syringe when the active power-up has an energy activation requirement.");
        AddField(details, settings.FindPropertyRelative("color"), scalingRules, "Color", "Direct color applied to the activation-requirement triangle marker.");
        AddField(details, settings.FindPropertyRelative("width"), scalingRules, "Width", "Reference-length normalized width of the marker. Runtime compensation keeps its pixel footprint stable across syringe lengths.");
        AddField(details, settings.FindPropertyRelative("height"), scalingRules, "Height", "Normalized marker height in the syringe shader UV space.");
        AddField(details, settings.FindPropertyRelative("verticalOffset"), scalingRules, "Vertical Offset", "Normalized marker offset from the chamber top. Positive values move the marker upward.");
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds charge semiring controls, showing shader tuning only when the semiring is enabled.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized charge-ring settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildChargeRing(VisualElement parent,
                                        SerializedProperty settings,
                                        SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Charge Semiring", "ChargeRing");

        if (settings == null)
        {
            foldout.Add(new HelpBox("Charge Semiring settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Shows the charge progress as a procedural semiring around the active power-up icon.");
        AddField(details, settings.FindPropertyRelative("backgroundColor"), scalingRules, "Background Color", "Direct color used by the unfilled semiring track.");
        AddField(details, settings.FindPropertyRelative("fillColor"), scalingRules, "Fill Color", "Direct color used by the filled semiring arc.");
        AddField(details, settings.FindPropertyRelative("outlineColor"), scalingRules, "Outline Color", "Direct color used by the semiring outline.");
        AddField(details, settings.FindPropertyRelative("fillDirection"), scalingRules, "Fill Direction", "Direction used by the charge semiring to grow along its authored arc.");
        AddField(details, settings.FindPropertyRelative("thickness"), scalingRules, "Thickness", "Normalized semiring band thickness relative to the widget half-size.");
        AddField(details, settings.FindPropertyRelative("outlineThickness"), scalingRules, "Outline Thickness", "Normalized outline thickness around both edges of the semiring.");
        AddField(details, settings.FindPropertyRelative("startAngleDegrees"), scalingRules, "Start Angle Degrees", "Start angle in degrees for the semiring. Zero points right and positive values rotate counter-clockwise.");
        AddField(details, settings.FindPropertyRelative("arcDegrees"), scalingRules, "Arc Degrees", "Total arc length in degrees covered by the charge semiring.");
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Builds icon cooldown reveal controls, showing reveal tuning only when cooldown visuals are enabled.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized icon-cooldown settings.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    private static void BuildIconCooldown(VisualElement parent,
                                          SerializedProperty settings,
                                          SerializedProperty scalingRules)
    {
        Foldout foldout = CreateFoldout("Icon Cooldown", "IconCooldown");

        if (settings == null)
        {
            foldout.Add(new HelpBox("Icon Cooldown settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        AddField(foldout, enabled, scalingRules, "Enabled", "Desaturates the active power-up icon while cooldown or toggle reactivation lock is still running.");
        AddField(details, settings.FindPropertyRelative("fillDirection"), scalingRules, "Fill Direction", "Direction used by the icon to reveal original colors while cooldown expires.");
        AddField(details, settings.FindPropertyRelative("desaturationStrength"), scalingRules, "Desaturation Strength", "Strength of grayscale conversion while the icon is locked by cooldown.");
        AddField(details, settings.FindPropertyRelative("lockedTint"), scalingRules, "Locked Tint", "Tint multiplied over the desaturated locked portion of the icon.");
        AddField(details, settings.FindPropertyRelative("revealFeather"), scalingRules, "Reveal Feather", "Softness of the transition between locked grayscale and revealed original colors.");
        foldout.Add(details);
        parent.Add(foldout);

        Refresh();
        TrackRefresh(foldout, enabled, Refresh);

        void Refresh()
        {
            details.style.display = enabled != null && enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #region Field Construction

    /// <summary>
    /// Adds one unified Add Scaling field with direct authoring and an explanatory tooltip.
    /// </summary>
    /// <param name="parent">Parent container receiving the field.</param>
    /// <param name="property">Serialized target property.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="label">User-facing field label.</param>
    /// <param name="tooltip">Field behavior description.</param>
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRules,
                                 string label,
                                 string tooltip)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRules,
                                                                           label,
                                                                           null);
        field.tooltip = tooltip;
        parent.Add(field);
    }

    /// <summary>
    /// Creates one themed nested foldout with a stable state key.
    /// </summary>
    /// <param name="title">User-facing foldout title.</param>
    /// <param name="stateSuffix">Stable state-key suffix.</param>
    /// <returns>Configured nested foldout.</returns>
    private static Foldout CreateFoldout(string title, string stateSuffix)
    {
        return ManagementToolFoldoutStateUtility.CreateFoldout(title,
                                                                "NashCore.PlayerManagement.Visual.ActivePowerUpHud." + stateSuffix,
                                                                true);
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Registers a serialized-property tracker for conditional controls and warnings.
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
    /// Rebuilds active power-up HUD warnings without modifying serialized values.
    /// </summary>
    /// <param name="warnings">Container receiving warning boxes.</param>
    /// <param name="settings">Serialized Active Power-Up HUD settings root.</param>
    private static void RefreshWarnings(VisualElement warnings, SerializedProperty settings)
    {
        warnings.Clear();
        AddToggleInfo(warnings,
                      settings.FindPropertyRelative("requirementMarker").FindPropertyRelative("enabled"),
                      "Activation requirement markers are disabled; energy costs will not be called out on the syringe.");
        AddToggleInfo(warnings,
                      settings.FindPropertyRelative("chargeRing").FindPropertyRelative("enabled"),
                      "Charge semirings are disabled; hold-charge progress will not be displayed around active icons.");
        AddToggleInfo(warnings,
                      settings.FindPropertyRelative("iconCooldown").FindPropertyRelative("enabled"),
                      "Icon cooldown reveal is disabled; active icons will stay fully colored during cooldown locks.");
    }

    /// <summary>
    /// Adds one informational warning when a feature toggle is disabled.
    /// </summary>
    /// <param name="warnings">Container receiving the help box.</param>
    /// <param name="toggle">Serialized feature toggle.</param>
    /// <param name="message">Message shown when the feature is disabled.</param>
    private static void AddToggleInfo(VisualElement warnings, SerializedProperty toggle, string message)
    {
        if (toggle == null || toggle.boolValue)
            return;

        warnings.Add(new HelpBox(message, HelpBoxMessageType.Info));
    }
    #endregion

    #endregion
}
