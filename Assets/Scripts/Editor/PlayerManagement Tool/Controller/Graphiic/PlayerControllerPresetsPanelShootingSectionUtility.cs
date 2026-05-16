using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the general shooting subsection of the Player Controller preset panel.
/// </summary>
internal static class PlayerControllerPresetsPanelShootingSectionUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete shooting settings section for the selected preset.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and callbacks.</param>
    /// <param name="section">Pre-created section container that receives all controls.</param>
    public static void BuildShootingSection(PlayerControllerPresetsPanel panel, VisualElement section)
    {
        if (panel == null || section == null)
            return;

        SerializedProperty shootingProperty = panel.PresetSerializedObject.FindProperty("shootingSettings");

        if (shootingProperty == null)
            return;

        SerializedProperty triggerModeProperty = shootingProperty.FindPropertyRelative("triggerMode");
        SerializedProperty inheritPlayerSpeedProperty = shootingProperty.FindPropertyRelative("projectilesInheritPlayerSpeed");
        SerializedProperty projectilePrefabProperty = shootingProperty.FindPropertyRelative("projectilePrefab");
        SerializedProperty shootOffsetProperty = shootingProperty.FindPropertyRelative("shootOffset");
        SerializedProperty valuesProperty = shootingProperty.FindPropertyRelative("values");
        SerializedProperty initialPoolCapacityProperty = shootingProperty.FindPropertyRelative("initialPoolCapacity");
        SerializedProperty poolExpandBatchProperty = shootingProperty.FindPropertyRelative("poolExpandBatch");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        section.Add(CreateField(triggerModeProperty,
                                scalingRulesProperty,
                                "Trigger Mode"));
        section.Add(CreateField(inheritPlayerSpeedProperty,
                                scalingRulesProperty,
                                "Projectiles Inherit Player Speed",
                                "When enabled, projectiles inherit the player's horizontal velocity while they are active."));

        ObjectField projectilePrefabField = new ObjectField("Projectile Prefab");
        projectilePrefabField.objectType = typeof(GameObject);
        projectilePrefabField.BindProperty(projectilePrefabProperty);
        section.Add(projectilePrefabField);

        section.Add(CreateField(shootOffsetProperty,
                                scalingRulesProperty,
                                "Shoot Offset",
                                "Offset applied from the Weapon Reference set on PlayerAuthoring, or from the player transform when no reference is assigned."));

        SerializedProperty shootActionProperty = panel.PresetSerializedObject.FindProperty("shootActionId");
        PlayerControllerPresetsPanelFieldUtility.EnsureDefaultActionId(panel, shootActionProperty, "Shoot");

        Foldout bindingsFoldout = PlayerControllerPresetsPanelFieldUtility.BuildBindingsFoldout(panel.InputAsset,
                                                                                                panel.PresetSerializedObject,
                                                                                                shootActionProperty,
                                                                                                InputActionSelectionElement.SelectionMode.Shooting);
        section.Add(bindingsFoldout);
        section.Add(BuildValuesFoldout(valuesProperty, scalingRulesProperty));
        section.Add(PlayerControllerPresetsPanelElementBulletSectionUtility.BuildElementBulletSettingsFoldout(valuesProperty, scalingRulesProperty));
        section.Add(BuildObjectPoolFoldout(initialPoolCapacityProperty, poolExpandBatchProperty, scalingRulesProperty));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds the standard projectile values foldout.
    /// </summary>
    /// <param name="valuesProperty">Serialized shooting values property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing the standard projectile fields.</returns>
    private static Foldout BuildValuesFoldout(SerializedProperty valuesProperty, SerializedProperty scalingRulesProperty)
    {
        SerializedProperty shootSpeedProperty = valuesProperty.FindPropertyRelative("shootSpeed");
        SerializedProperty rateOfFireProperty = valuesProperty.FindPropertyRelative("rateOfFire");
        SerializedProperty projectileSizeMultiplierProperty = valuesProperty.FindPropertyRelative("projectileSizeMultiplier");
        SerializedProperty explosionRadiusProperty = valuesProperty.FindPropertyRelative("explosionRadius");
        SerializedProperty rangeProperty = valuesProperty.FindPropertyRelative("range");
        SerializedProperty lifetimeProperty = valuesProperty.FindPropertyRelative("lifetime");
        SerializedProperty damageProperty = valuesProperty.FindPropertyRelative("damage");
        SerializedProperty penetrationModeProperty = valuesProperty.FindPropertyRelative("penetrationMode");
        SerializedProperty maxPenetrationsProperty = valuesProperty.FindPropertyRelative("maxPenetrations");
        SerializedProperty knockbackProperty = valuesProperty.FindPropertyRelative("knockback");

        Foldout valuesFoldout = new Foldout();
        valuesFoldout.text = "Values";
        valuesFoldout.value = true;

        valuesFoldout.Add(BuildProjectileCoreFoldout(shootSpeedProperty,
                                                     rateOfFireProperty,
                                                     projectileSizeMultiplierProperty,
                                                     damageProperty,
                                                     scalingRulesProperty));
        valuesFoldout.Add(BuildLifetimeAndAreaFoldout(explosionRadiusProperty,
                                                      rangeProperty,
                                                      lifetimeProperty,
                                                      scalingRulesProperty));
        valuesFoldout.Add(BuildPenetrationFoldout(penetrationModeProperty,
                                                  maxPenetrationsProperty,
                                                  scalingRulesProperty));
        valuesFoldout.Add(BuildKnockbackFoldout(knockbackProperty, scalingRulesProperty));
        return valuesFoldout;
    }

    /// <summary>
    /// Builds the core projectile tuning subsection.
    /// </summary>
    /// <param name="shootSpeedProperty">Serialized projectile speed property.</param>
    /// <param name="rateOfFireProperty">Serialized fire cadence property.</param>
    /// <param name="projectileSizeMultiplierProperty">Serialized projectile size multiplier property.</param>
    /// <param name="damageProperty">Serialized projectile damage property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing the main projectile tuning values.</returns>
    private static Foldout BuildProjectileCoreFoldout(SerializedProperty shootSpeedProperty,
                                                      SerializedProperty rateOfFireProperty,
                                                      SerializedProperty projectileSizeMultiplierProperty,
                                                      SerializedProperty damageProperty,
                                                      SerializedProperty scalingRulesProperty)
    {
        Foldout projectileFoldout = CreateNestedFoldout("Projectile");
        projectileFoldout.Add(PlayerScalingFieldElementFactory.CreateField(shootSpeedProperty, scalingRulesProperty, "Shoot Speed"));
        projectileFoldout.Add(PlayerScalingFieldElementFactory.CreateField(rateOfFireProperty, scalingRulesProperty, "Rate Of Fire"));
        projectileFoldout.Add(PlayerScalingFieldElementFactory.CreateField(projectileSizeMultiplierProperty, scalingRulesProperty, "Projectile Size Multiplier"));
        projectileFoldout.Add(PlayerScalingFieldElementFactory.CreateField(damageProperty, scalingRulesProperty, "Damage"));
        return projectileFoldout;
    }

    /// <summary>
    /// Builds the projectile lifetime and area subsection.
    /// </summary>
    /// <param name="explosionRadiusProperty">Serialized explosion radius property.</param>
    /// <param name="rangeProperty">Serialized range property.</param>
    /// <param name="lifetimeProperty">Serialized lifetime property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing area and lifetime values.</returns>
    private static Foldout BuildLifetimeAndAreaFoldout(SerializedProperty explosionRadiusProperty,
                                                       SerializedProperty rangeProperty,
                                                       SerializedProperty lifetimeProperty,
                                                       SerializedProperty scalingRulesProperty)
    {
        Foldout lifetimeFoldout = CreateNestedFoldout("Lifetime & Area");
        lifetimeFoldout.Add(PlayerScalingFieldElementFactory.CreateField(explosionRadiusProperty, scalingRulesProperty, "Explosion Radius"));
        lifetimeFoldout.Add(PlayerScalingFieldElementFactory.CreateField(rangeProperty, scalingRulesProperty, "Range"));
        lifetimeFoldout.Add(PlayerScalingFieldElementFactory.CreateField(lifetimeProperty, scalingRulesProperty, "Lifetime"));
        return lifetimeFoldout;
    }

    /// <summary>
    /// Builds the projectile penetration subsection.
    /// </summary>
    /// <param name="penetrationModeProperty">Serialized penetration mode property.</param>
    /// <param name="maxPenetrationsProperty">Serialized max penetrations property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing penetration-specific controls.</returns>
    private static Foldout BuildPenetrationFoldout(SerializedProperty penetrationModeProperty,
                                                   SerializedProperty maxPenetrationsProperty,
                                                   SerializedProperty scalingRulesProperty)
    {
        Foldout penetrationFoldout = CreateNestedFoldout("Penetration");
        VisualElement penetrationModeField = PlayerScalingFieldElementFactory.CreateField(penetrationModeProperty,
                                                                                          scalingRulesProperty,
                                                                                          "Penetration Mode");
        VisualElement maxPenetrationsField = PlayerScalingFieldElementFactory.CreateField(maxPenetrationsProperty,
                                                                                          scalingRulesProperty,
                                                                                          "Max Penetrations");
        penetrationFoldout.Add(penetrationModeField);
        penetrationFoldout.Add(maxPenetrationsField);

        System.Action refreshPenetrationFieldVisibility = () =>
        {
            ProjectilePenetrationMode penetrationMode = (ProjectilePenetrationMode)penetrationModeProperty.enumValueIndex;
            bool shouldShowMaxPenetration = ShouldDisplayMaxPenetrationField(penetrationMode);
            maxPenetrationsField.style.display = shouldShowMaxPenetration ? DisplayStyle.Flex : DisplayStyle.None;
        };

        RegisterRefreshCallback(penetrationModeField, refreshPenetrationFieldVisibility);
        refreshPenetrationFieldVisibility();
        return penetrationFoldout;
    }

    /// <summary>
    /// Builds the projectile knockback subsection.
    /// </summary>
    /// <param name="knockbackProperty">Serialized knockback payload property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing knockback controls and warnings.</returns>
    private static Foldout BuildKnockbackFoldout(SerializedProperty knockbackProperty,
                                                 SerializedProperty scalingRulesProperty)
    {
        Foldout knockbackFoldout = CreateNestedFoldout("Knockback");

        if (knockbackProperty == null)
        {
            knockbackFoldout.Add(new HelpBox("Knockback settings are missing.", HelpBoxMessageType.Warning));
            return knockbackFoldout;
        }

        SerializedProperty enabledProperty = knockbackProperty.FindPropertyRelative("enabled");
        SerializedProperty strengthProperty = knockbackProperty.FindPropertyRelative("strength");
        SerializedProperty durationSecondsProperty = knockbackProperty.FindPropertyRelative("durationSeconds");
        SerializedProperty directionModeProperty = knockbackProperty.FindPropertyRelative("directionMode");
        SerializedProperty stackingModeProperty = knockbackProperty.FindPropertyRelative("stackingMode");

        if (enabledProperty == null ||
            strengthProperty == null ||
            durationSecondsProperty == null ||
            directionModeProperty == null ||
            stackingModeProperty == null)
        {
            knockbackFoldout.Add(new HelpBox("Knockback settings are incomplete.", HelpBoxMessageType.Warning));
            return knockbackFoldout;
        }

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        knockbackFoldout.Add(warningBox);

        VisualElement enabledField = CreateField(enabledProperty,
                                                 scalingRulesProperty,
                                                 "Enabled",
                                                 "Enables default projectile knockback on valid enemy hits.");
        VisualElement strengthField = CreateField(strengthProperty,
                                                  scalingRulesProperty,
                                                  "Strength",
                                                  "Planar push strength converted into knockback velocity.");
        VisualElement durationField = CreateField(durationSecondsProperty,
                                                  scalingRulesProperty,
                                                  "Duration Seconds",
                                                  "Seconds required for the knockback velocity to decay back to zero.");
        VisualElement directionField = CreateField(directionModeProperty,
                                                   scalingRulesProperty,
                                                   "Direction Mode",
                                                   "Chooses whether enemies are pushed by projectile travel or by the hit-to-target vector.");
        VisualElement stackingField = CreateField(stackingModeProperty,
                                                  scalingRulesProperty,
                                                  "Stacking Mode",
                                                  "Defines how new hits combine with knockback already active on the same enemy.");
        knockbackFoldout.Add(enabledField);
        knockbackFoldout.Add(strengthField);
        knockbackFoldout.Add(durationField);
        knockbackFoldout.Add(directionField);
        knockbackFoldout.Add(stackingField);

        System.Action refreshWarnings = () =>
        {
            string warningMessage = BuildKnockbackWarningMessage(enabledProperty,
                                                                strengthProperty,
                                                                durationSecondsProperty);
            warningBox.text = warningMessage;
            warningBox.style.display = string.IsNullOrWhiteSpace(warningMessage) ? DisplayStyle.None : DisplayStyle.Flex;
        };

        RegisterRefreshCallback(enabledField, refreshWarnings);
        RegisterRefreshCallback(strengthField, refreshWarnings);
        RegisterRefreshCallback(durationField, refreshWarnings);
        RegisterRefreshCallback(directionField, refreshWarnings);
        RegisterRefreshCallback(stackingField, refreshWarnings);
        refreshWarnings();
        return knockbackFoldout;
    }

    /// <summary>
    /// Builds the object-pool subsection for projectile spawning.
    /// </summary>
    /// <param name="initialPoolCapacityProperty">Serialized initial capacity property.</param>
    /// <param name="poolExpandBatchProperty">Serialized expand-batch property.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <returns>Foldout containing the pool controls.</returns>
    private static Foldout BuildObjectPoolFoldout(SerializedProperty initialPoolCapacityProperty,
                                                  SerializedProperty poolExpandBatchProperty,
                                                  SerializedProperty scalingRulesProperty)
    {
        Foldout objectPoolFoldout = new Foldout();
        objectPoolFoldout.text = "Object Pool";
        objectPoolFoldout.value = true;

        VisualElement initialPoolCapacityField = PlayerScalingFieldElementFactory.CreateField(initialPoolCapacityProperty, scalingRulesProperty, "Initial Capacity");
        initialPoolCapacityField.tooltip = "Number of projectiles pre-created when the pool initializes.";
        objectPoolFoldout.Add(initialPoolCapacityField);

        VisualElement poolExpandBatchField = PlayerScalingFieldElementFactory.CreateField(poolExpandBatchProperty, scalingRulesProperty, "Expand Batch");
        poolExpandBatchField.tooltip = "Number of projectiles created each time the pool needs expansion.";
        objectPoolFoldout.Add(poolExpandBatchField);
        return objectPoolFoldout;
    }

    /// <summary>
    /// Builds the warning text for authored knockback values.
    /// </summary>
    /// <param name="enabledProperty">Serialized enabled property.</param>
    /// <param name="strengthProperty">Serialized strength property.</param>
    /// <param name="durationSecondsProperty">Serialized duration property.</param>
    /// <returns>Warning text, or an empty string when the authored knockback payload is coherent.</returns>
    private static string BuildKnockbackWarningMessage(SerializedProperty enabledProperty,
                                                       SerializedProperty strengthProperty,
                                                       SerializedProperty durationSecondsProperty)
    {
        if (enabledProperty == null || !enabledProperty.boolValue)
            return string.Empty;

        StringBuilder warningBuilder = new StringBuilder(128);
        AppendWarningLine(warningBuilder, strengthProperty != null && strengthProperty.floatValue <= 0f, "Knockback Strength should be > 0 when knockback is enabled.");
        AppendWarningLine(warningBuilder, durationSecondsProperty != null && durationSecondsProperty.floatValue <= 0f, "Knockback Duration Seconds should be > 0 when knockback is enabled.");
        return warningBuilder.ToString().TrimEnd();
    }

    /// <summary>
    /// Registers a lightweight refresh callback for one scaling-aware field.
    /// </summary>
    /// <param name="field">Existing visual field that emits SerializedPropertyChangeEvent.</param>
    /// <param name="refreshAction">Refresh callback executed after property changes.</param>
    private static void RegisterRefreshCallback(VisualElement field, System.Action refreshAction)
    {
        if (field == null)
            return;

        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            refreshAction();
        });
    }

    /// <summary>
    /// Appends one warning line to the warning builder when the condition is true.
    /// </summary>
    /// <param name="warningBuilder">Destination warning builder.</param>
    /// <param name="condition">Condition that triggers the warning.</param>
    /// <param name="warningLine">Warning text appended when the condition is true.</param>
    private static void AppendWarningLine(StringBuilder warningBuilder, bool condition, string warningLine)
    {
        if (!condition)
            return;

        if (warningBuilder.Length > 0)
            warningBuilder.AppendLine();

        warningBuilder.Append(warningLine);
    }

    /// <summary>
    /// Resolves whether the max penetration field is relevant for the selected penetration mode.
    /// </summary>
    /// <param name="penetrationMode">Currently selected projectile penetration behavior.</param>
    /// <returns>True when the mode requires a maximum penetration count.</returns>
    private static bool ShouldDisplayMaxPenetrationField(ProjectilePenetrationMode penetrationMode)
    {
        switch (penetrationMode)
        {
            case ProjectilePenetrationMode.FixedHits:
            case ProjectilePenetrationMode.DamageBased:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Creates one nested foldout used inside the Shooting Values container.
    /// </summary>
    /// <param name="title">Visible foldout title.</param>
    /// <returns>Configured nested foldout.</returns>
    private static Foldout CreateNestedFoldout(string title)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.value = true;
        foldout.style.marginLeft = 8f;
        return foldout;
    }

    /// <summary>
    /// Builds one scaling-aware field with an optional tooltip override.
    /// </summary>
    /// <param name="property">Serialized property bound to the field.</param>
    /// <param name="scalingRulesProperty">Serialized scaling rules property.</param>
    /// <param name="label">Display label shown in the tool.</param>
    /// <param name="tooltip">Optional tooltip shown in the UI.</param>
    /// <returns>Configured VisualElement for the requested property.</returns>
    private static VisualElement CreateField(SerializedProperty property,
                                             SerializedProperty scalingRulesProperty,
                                             string label,
                                             string tooltip = null)
    {
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);

        if (!string.IsNullOrWhiteSpace(tooltip))
            field.tooltip = tooltip;

        return field;
    }
    #endregion

    #endregion
}
