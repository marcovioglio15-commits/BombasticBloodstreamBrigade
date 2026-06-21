using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds scalable Player Visual Preset controls for active power-up HUD icon, energy syringe, and charge ring visuals.
/// </summary>
internal static class PlayerVisualPresetsPanelActivePowerUpHudSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Active Power-Up HUD visual-preset subsection.
    /// </summary>
    /// <param name="panel">Owning visual preset panel providing serialized authoring data.</param>
    /// <returns>Configured Active Power-Up HUD subsection.</returns>
    public static VisualElement Build(PlayerVisualPresetsPanel panel)
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
            root.Add(new HelpBox("Active Power-Up HUD settings are missing from the selected Player Visual Preset.",
                                 HelpBoxMessageType.Warning));
            return root;
        }

        SerializedProperty enabled = settings.FindPropertyRelative("enabled");
        VisualElement details = new VisualElement();
        VisualElement warnings = new VisualElement();
        AddField(root, enabled, scalingRules, "Enabled", "Enables the redesigned active power-up HUD widgets.");
        BuildGeneral(details, settings, scalingRules);
        BuildNestedSettings(details, settings.FindPropertyRelative("energySyringe"), scalingRules, "Energy Syringe", "EnergySyringe");
        BuildNestedSettings(details, settings.FindPropertyRelative("requirementMarker"), scalingRules, "Activation Requirement Marker", "RequirementMarker");
        BuildNestedSettings(details, settings.FindPropertyRelative("chargeRing"), scalingRules, "Charge Semiring", "ChargeRing");
        BuildNestedSettings(details, settings.FindPropertyRelative("iconCooldown"), scalingRules, "Icon Cooldown", "IconCooldown");
        root.Add(details);
        root.Add(warnings);

        Refresh();
        TrackRefresh(root, enabled, Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("hideWhenPlayerMissing"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("hideEnergyWhenModuleMissing"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("hideChargeWhenModuleMissing"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("requirementMarker").FindPropertyRelative("enabled"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("chargeRing").FindPropertyRelative("enabled"), Refresh);
        TrackRefresh(root, settings.FindPropertyRelative("iconCooldown").FindPropertyRelative("enabled"), Refresh);
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
    /// Builds a nested settings block recursively so every leaf field keeps Add Scaling support.
    /// </summary>
    /// <param name="parent">Parent container receiving the foldout.</param>
    /// <param name="settings">Serialized settings root to expose.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="title">User-facing foldout title.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    private static void BuildNestedSettings(VisualElement parent,
                                            SerializedProperty settings,
                                            SerializedProperty scalingRules,
                                            string title,
                                            string stateSuffix)
    {
        Foldout foldout = CreateFoldout(title, stateSuffix);

        if (settings == null)
        {
            foldout.Add(new HelpBox(title + " settings are missing.", HelpBoxMessageType.Warning));
            parent.Add(foldout);
            return;
        }

        AddRecursiveFields(foldout, settings, scalingRules, stateSuffix);
        parent.Add(foldout);
    }
    #endregion

    #region Recursive Fields
    /// <summary>
    /// Adds leaf fields or nested foldouts for every visible serialized child.
    /// </summary>
    /// <param name="parent">Parent container receiving fields.</param>
    /// <param name="rootProperty">Serialized root property whose children should be exposed.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    private static void AddRecursiveFields(VisualElement parent,
                                           SerializedProperty rootProperty,
                                           SerializedProperty scalingRules,
                                           string stateSuffix)
    {
        SerializedProperty iterator = rootProperty.Copy();
        SerializedProperty endProperty = rootProperty.GetEndProperty();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
        {
            SerializedProperty child = iterator.Copy();

            if (child.depth != rootProperty.depth + 1)
            {
                enterChildren = false;
                continue;
            }

            if (ShouldCreateNestedFoldout(child))
                AddNestedFoldout(parent, child, scalingRules, stateSuffix);
            else
                AddField(parent, child, scalingRules, ObjectNames.NicifyVariableName(child.name), child.tooltip);

            enterChildren = false;
        }
    }

    /// <summary>
    /// Adds one nested foldout and recursively exposes its leaf fields.
    /// </summary>
    /// <param name="parent">Parent container receiving the nested foldout.</param>
    /// <param name="property">Serialized nested property.</param>
    /// <param name="scalingRules">Serialized Add Scaling rules list.</param>
    /// <param name="stateSuffix">Stable foldout-state suffix.</param>
    private static void AddNestedFoldout(VisualElement parent,
                                         SerializedProperty property,
                                         SerializedProperty scalingRules,
                                         string stateSuffix)
    {
        string title = ObjectNames.NicifyVariableName(property.name);
        Foldout foldout = CreateFoldout(title, stateSuffix + "." + property.name);
        AddRecursiveFields(foldout, property, scalingRules, stateSuffix + "." + property.name);
        parent.Add(foldout);
    }

    /// <summary>
    /// Returns whether the property should become a nested foldout instead of one direct field.
    /// </summary>
    /// <param name="property">Serialized property to inspect.</param>
    /// <returns>True when the property is a custom serializable block.</returns>
    private static bool ShouldCreateNestedFoldout(SerializedProperty property)
    {
        if (property == null)
            return false;

        if (property.propertyType != SerializedPropertyType.Generic)
            return false;

        if (!property.hasVisibleChildren)
            return false;

        return !property.isArray;
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
