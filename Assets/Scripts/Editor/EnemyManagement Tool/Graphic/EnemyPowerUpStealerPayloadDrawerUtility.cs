using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the dedicated UI Toolkit payload editor for enemy Power-Up Stealer modules.
/// </summary>
internal static class EnemyPowerUpStealerPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the complete Power-Up Stealer payload editor, including smart visibility and warnings.
    /// </summary>
    /// <param name="payloadDataProperty">Serialized payload data root that owns the stealer payload.</param>
    /// <param name="payloadContainer">Visual container receiving the generated editor controls.</param>
    /// <returns>True when the editor was built; otherwise false.</returns>
    public static bool BuildPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        if (payloadContainer == null)
            return false;

        if (payloadDataProperty == null)
        {
            payloadContainer.Add(new HelpBox("Power-Up Stealer payload root is missing.", HelpBoxMessageType.Warning));
            return false;
        }

        SerializedProperty stealerProperty = payloadDataProperty.FindPropertyRelative("powerUpStealer");

        if (stealerProperty == null)
        {
            payloadContainer.Add(new HelpBox("Power-Up Stealer payload data is missing.", HelpBoxMessageType.Warning));
            return false;
        }

        SerializedProperty triggerModeProperty = stealerProperty.FindPropertyRelative("triggerMode");
        SerializedProperty targetKindProperty = stealerProperty.FindPropertyRelative("targetKind");
        SerializedProperty activeTargetBiasPercentProperty = stealerProperty.FindPropertyRelative("activeTargetBiasPercent");
        SerializedProperty recoverAfterDamageTakenPercentProperty = stealerProperty.FindPropertyRelative("recoverAfterDamageTakenPercent");
        SerializedProperty recoveryDamageTakenPercentProperty = stealerProperty.FindPropertyRelative("recoveryDamageTakenPercent");
        SerializedProperty recoverAfterDamageWindowProperty = stealerProperty.FindPropertyRelative("recoverAfterDamageWindow");
        SerializedProperty recoveryDamageWindowPercentProperty = stealerProperty.FindPropertyRelative("recoveryDamageWindowPercent");
        SerializedProperty recoveryDamageWindowSecondsProperty = stealerProperty.FindPropertyRelative("recoveryDamageWindowSeconds");

        if (triggerModeProperty == null ||
            targetKindProperty == null ||
            activeTargetBiasPercentProperty == null ||
            recoverAfterDamageTakenPercentProperty == null ||
            recoveryDamageTakenPercentProperty == null ||
            recoverAfterDamageWindowProperty == null ||
            recoveryDamageWindowPercentProperty == null ||
            recoveryDamageWindowSecondsProperty == null)
        {
            payloadContainer.Add(new HelpBox("Power-Up Stealer payload fields are missing.", HelpBoxMessageType.Warning));
            return false;
        }

        Foldout stealFoldout = CreatePayloadFoldout(stealerProperty, "Steal", "PowerUpStealer");
        payloadContainer.Add(stealFoldout);
        BuildStealSection(stealFoldout,
                          triggerModeProperty,
                          targetKindProperty,
                          activeTargetBiasPercentProperty);

        Foldout recoveryFoldout = CreatePayloadFoldout(stealerProperty, "Recovery", "PowerUpStealerRecovery");
        payloadContainer.Add(recoveryFoldout);
        BuildRecoverySection(recoveryFoldout,
                             recoverAfterDamageTakenPercentProperty,
                             recoveryDamageTakenPercentProperty,
                             recoverAfterDamageWindowProperty,
                             recoveryDamageWindowPercentProperty,
                             recoveryDamageWindowSecondsProperty);

        HelpBox weaponSettingsInfoBox = new HelpBox("Minimum/maximum range, activation gates, look control, and behaviour-trigger feedback are configured on the Weapon Interaction assembly.", HelpBoxMessageType.Info);
        payloadContainer.Add(weaponSettingsInfoBox);
        return true;
    }
    #endregion

    #region Build Sections
    /// <summary>
    /// Builds trigger, target kind, and active-bias controls for the steal subsection.
    /// </summary>
    /// <param name="stealFoldout">Foldout that receives the controls.</param>
    /// <param name="triggerModeProperty">Serialized trigger mode property.</param>
    /// <param name="targetKindProperty">Serialized target kind property.</param>
    /// <param name="activeTargetBiasPercentProperty">Serialized active-target bias percentage property.</param>
    private static void BuildStealSection(Foldout stealFoldout,
                                          SerializedProperty triggerModeProperty,
                                          SerializedProperty targetKindProperty,
                                          SerializedProperty activeTargetBiasPercentProperty)
    {
        EnemyAdvancedPatternDrawerUtility.AddField(stealFoldout, triggerModeProperty, "Trigger Mode");
        EnemyAdvancedPatternDrawerUtility.AddField(stealFoldout, targetKindProperty, "Target Kind");

        VisualElement activeBiasContainer = new VisualElement();
        activeBiasContainer.tooltip = "Shown only when the module can steal either active or passive power-ups.";
        stealFoldout.Add(activeBiasContainer);
        AddPercentageSlider(activeBiasContainer,
                            activeTargetBiasPercentProperty,
                            "Active Target Bias %",
                            "Chance to attempt an active power-up before a passive one. Runtime falls back if the preferred category is unavailable.");
        UpdateActiveBiasVisibility(targetKindProperty, activeBiasContainer);
        stealFoldout.TrackPropertyValue(targetKindProperty, changedProperty =>
        {
            UpdateActiveBiasVisibility(changedProperty, activeBiasContainer);
        });

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        stealFoldout.Add(warningBox);
        RefreshStealWarnings(activeTargetBiasPercentProperty, warningBox);
        stealFoldout.TrackPropertyValue(activeTargetBiasPercentProperty, changedProperty =>
        {
            RefreshStealWarnings(changedProperty, warningBox);
        });
    }

    /// <summary>
    /// Builds optional damage-based recovery controls and their validation warnings.
    /// </summary>
    /// <param name="recoveryFoldout">Foldout that receives the controls.</param>
    /// <param name="recoverAfterDamageTakenPercentProperty">Serialized total-damage recovery toggle.</param>
    /// <param name="recoveryDamageTakenPercentProperty">Serialized total-damage percentage threshold.</param>
    /// <param name="recoverAfterDamageWindowProperty">Serialized timed-damage recovery toggle.</param>
    /// <param name="recoveryDamageWindowPercentProperty">Serialized timed-damage percentage threshold.</param>
    /// <param name="recoveryDamageWindowSecondsProperty">Serialized timed-damage window duration.</param>
    private static void BuildRecoverySection(Foldout recoveryFoldout,
                                             SerializedProperty recoverAfterDamageTakenPercentProperty,
                                             SerializedProperty recoveryDamageTakenPercentProperty,
                                             SerializedProperty recoverAfterDamageWindowProperty,
                                             SerializedProperty recoveryDamageWindowPercentProperty,
                                             SerializedProperty recoveryDamageWindowSecondsProperty)
    {
        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout,
                                                   recoverAfterDamageTakenPercentProperty,
                                                   "Recover After Damage Taken %");
        VisualElement totalDamageContainer = new VisualElement();
        totalDamageContainer.style.marginLeft = 12f;
        recoveryFoldout.Add(totalDamageContainer);
        AddPercentageSlider(totalDamageContainer,
                            recoveryDamageTakenPercentProperty,
                            "Damage Taken %",
                            "Max-health percentage lost after the steal before the stolen power-up is returned.");

        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout,
                                                   recoverAfterDamageWindowProperty,
                                                   "Recover After Damage Window");
        VisualElement damageWindowContainer = new VisualElement();
        damageWindowContainer.style.marginLeft = 12f;
        recoveryFoldout.Add(damageWindowContainer);
        AddPercentageSlider(damageWindowContainer,
                            recoveryDamageWindowPercentProperty,
                            "Window Damage %",
                            "Max-health percentage lost inside the window before the stolen power-up is returned.");
        EnemyAdvancedPatternDrawerUtility.AddField(damageWindowContainer,
                                                   recoveryDamageWindowSecondsProperty,
                                                   "Window Seconds");

        UpdateRecoveryVisibility(recoverAfterDamageTakenPercentProperty,
                                 recoverAfterDamageWindowProperty,
                                 totalDamageContainer,
                                 damageWindowContainer);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        recoveryFoldout.Add(warningBox);
        RefreshRecoveryWarnings(recoverAfterDamageTakenPercentProperty,
                                recoveryDamageTakenPercentProperty,
                                recoverAfterDamageWindowProperty,
                                recoveryDamageWindowPercentProperty,
                                recoveryDamageWindowSecondsProperty,
                                warningBox);

        RegisterRecoveryRefresh(recoveryFoldout,
                                recoverAfterDamageTakenPercentProperty,
                                recoveryDamageTakenPercentProperty,
                                recoverAfterDamageWindowProperty,
                                recoveryDamageWindowPercentProperty,
                                recoveryDamageWindowSecondsProperty,
                                totalDamageContainer,
                                damageWindowContainer,
                                warningBox);
    }
    #endregion

    #region Controls
    /// <summary>
    /// Creates one bound percentage slider with numeric input.
    /// </summary>
    /// <param name="parent">Container that receives the slider.</param>
    /// <param name="property">Serialized float property bound to the slider.</param>
    /// <param name="label">Visible slider label.</param>
    /// <param name="tooltip">Tooltip shown on hover.</param>
    private static void AddPercentageSlider(VisualElement parent, SerializedProperty property, string label, string tooltip)
    {
        if (parent == null || property == null)
            return;

        Slider slider = new Slider(label, 0f, 100f);
        slider.showInputField = true;
        slider.tooltip = tooltip;
        slider.BindProperty(property);
        parent.Add(slider);
    }

    /// <summary>
    /// Creates a foldout with a stable key scoped to the stealer payload property.
    /// </summary>
    /// <param name="property">Serialized property that identifies the payload context.</param>
    /// <param name="title">Visible foldout title.</param>
    /// <param name="suffix">Local suffix used to distinguish sibling foldouts.</param>
    /// <returns>Configured foldout element.</returns>
    private static Foldout CreatePayloadFoldout(SerializedProperty property, string title, string suffix)
    {
        Foldout foldout = ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property, title, "Payload" + suffix, true);
        foldout.tooltip = "Groups " + title + " Power-Up Stealer settings.";
        return foldout;
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Shows active-bias controls only when both active and passive categories are valid targets.
    /// </summary>
    /// <param name="targetKindProperty">Serialized target kind enum.</param>
    /// <param name="activeBiasContainer">Container that owns the active-bias slider.</param>
    private static void UpdateActiveBiasVisibility(SerializedProperty targetKindProperty, VisualElement activeBiasContainer)
    {
        if (activeBiasContainer == null)
            return;

        EnemyPowerUpStealTargetKind targetKind = EnemyPowerUpStealTargetKind.ActiveOrPassive;

        if (targetKindProperty != null && targetKindProperty.propertyType == SerializedPropertyType.Enum)
            targetKind = (EnemyPowerUpStealTargetKind)targetKindProperty.enumValueIndex;

        activeBiasContainer.style.display = targetKind == EnemyPowerUpStealTargetKind.ActiveOrPassive
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>
    /// Shows recovery detail groups only when their owning toggles are enabled.
    /// </summary>
    /// <param name="recoverAfterDamageTakenPercentProperty">Serialized total-damage recovery toggle.</param>
    /// <param name="recoverAfterDamageWindowProperty">Serialized timed-damage recovery toggle.</param>
    /// <param name="totalDamageContainer">Container that owns total-damage threshold controls.</param>
    /// <param name="damageWindowContainer">Container that owns timed-damage threshold controls.</param>
    private static void UpdateRecoveryVisibility(SerializedProperty recoverAfterDamageTakenPercentProperty,
                                                 SerializedProperty recoverAfterDamageWindowProperty,
                                                 VisualElement totalDamageContainer,
                                                 VisualElement damageWindowContainer)
    {
        if (totalDamageContainer != null)
        {
            totalDamageContainer.style.display = recoverAfterDamageTakenPercentProperty != null &&
                                                recoverAfterDamageTakenPercentProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (damageWindowContainer != null)
        {
            damageWindowContainer.style.display = recoverAfterDamageWindowProperty != null &&
                                                 recoverAfterDamageWindowProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Registers property tracking callbacks used to refresh recovery visibility and warnings.
    /// </summary>
    /// <param name="root">Root foldout that owns the tracked properties.</param>
    /// <param name="recoverAfterDamageTakenPercentProperty">Serialized total-damage recovery toggle.</param>
    /// <param name="recoveryDamageTakenPercentProperty">Serialized total-damage percentage threshold.</param>
    /// <param name="recoverAfterDamageWindowProperty">Serialized timed-damage recovery toggle.</param>
    /// <param name="recoveryDamageWindowPercentProperty">Serialized timed-damage percentage threshold.</param>
    /// <param name="recoveryDamageWindowSecondsProperty">Serialized timed-damage window duration.</param>
    /// <param name="totalDamageContainer">Container that owns total-damage threshold controls.</param>
    /// <param name="damageWindowContainer">Container that owns timed-damage threshold controls.</param>
    /// <param name="warningBox">Warning box refreshed after any tracked property changes.</param>
    private static void RegisterRecoveryRefresh(VisualElement root,
                                                SerializedProperty recoverAfterDamageTakenPercentProperty,
                                                SerializedProperty recoveryDamageTakenPercentProperty,
                                                SerializedProperty recoverAfterDamageWindowProperty,
                                                SerializedProperty recoveryDamageWindowPercentProperty,
                                                SerializedProperty recoveryDamageWindowSecondsProperty,
                                                VisualElement totalDamageContainer,
                                                VisualElement damageWindowContainer,
                                                HelpBox warningBox)
    {
        TrackRecoveryProperty(root,
                              recoverAfterDamageTakenPercentProperty,
                              recoverAfterDamageTakenPercentProperty,
                              recoveryDamageTakenPercentProperty,
                              recoverAfterDamageWindowProperty,
                              recoveryDamageWindowPercentProperty,
                              recoveryDamageWindowSecondsProperty,
                              totalDamageContainer,
                              damageWindowContainer,
                              warningBox);
        TrackRecoveryProperty(root,
                              recoveryDamageTakenPercentProperty,
                              recoverAfterDamageTakenPercentProperty,
                              recoveryDamageTakenPercentProperty,
                              recoverAfterDamageWindowProperty,
                              recoveryDamageWindowPercentProperty,
                              recoveryDamageWindowSecondsProperty,
                              totalDamageContainer,
                              damageWindowContainer,
                              warningBox);
        TrackRecoveryProperty(root,
                              recoverAfterDamageWindowProperty,
                              recoverAfterDamageTakenPercentProperty,
                              recoveryDamageTakenPercentProperty,
                              recoverAfterDamageWindowProperty,
                              recoveryDamageWindowPercentProperty,
                              recoveryDamageWindowSecondsProperty,
                              totalDamageContainer,
                              damageWindowContainer,
                              warningBox);
        TrackRecoveryProperty(root,
                              recoveryDamageWindowPercentProperty,
                              recoverAfterDamageTakenPercentProperty,
                              recoveryDamageTakenPercentProperty,
                              recoverAfterDamageWindowProperty,
                              recoveryDamageWindowPercentProperty,
                              recoveryDamageWindowSecondsProperty,
                              totalDamageContainer,
                              damageWindowContainer,
                              warningBox);
        TrackRecoveryProperty(root,
                              recoveryDamageWindowSecondsProperty,
                              recoverAfterDamageTakenPercentProperty,
                              recoveryDamageTakenPercentProperty,
                              recoverAfterDamageWindowProperty,
                              recoveryDamageWindowPercentProperty,
                              recoveryDamageWindowSecondsProperty,
                              totalDamageContainer,
                              damageWindowContainer,
                              warningBox);
    }

    /// <summary>
    /// Tracks one serialized property and refreshes the recovery UI when it changes.
    /// </summary>
    /// <param name="root">Root visual element used for property tracking.</param>
    /// <param name="trackedProperty">Property that should trigger a refresh.</param>
    /// <param name="recoverAfterDamageTakenPercentProperty">Serialized total-damage recovery toggle.</param>
    /// <param name="recoveryDamageTakenPercentProperty">Serialized total-damage percentage threshold.</param>
    /// <param name="recoverAfterDamageWindowProperty">Serialized timed-damage recovery toggle.</param>
    /// <param name="recoveryDamageWindowPercentProperty">Serialized timed-damage percentage threshold.</param>
    /// <param name="recoveryDamageWindowSecondsProperty">Serialized timed-damage window duration.</param>
    /// <param name="totalDamageContainer">Container that owns total-damage threshold controls.</param>
    /// <param name="damageWindowContainer">Container that owns timed-damage threshold controls.</param>
    /// <param name="warningBox">Warning box refreshed after the property changes.</param>
    private static void TrackRecoveryProperty(VisualElement root,
                                              SerializedProperty trackedProperty,
                                              SerializedProperty recoverAfterDamageTakenPercentProperty,
                                              SerializedProperty recoveryDamageTakenPercentProperty,
                                              SerializedProperty recoverAfterDamageWindowProperty,
                                              SerializedProperty recoveryDamageWindowPercentProperty,
                                              SerializedProperty recoveryDamageWindowSecondsProperty,
                                              VisualElement totalDamageContainer,
                                              VisualElement damageWindowContainer,
                                              HelpBox warningBox)
    {
        if (root == null || trackedProperty == null)
            return;

        root.TrackPropertyValue(trackedProperty, changedProperty =>
        {
            UpdateRecoveryVisibility(recoverAfterDamageTakenPercentProperty,
                                     recoverAfterDamageWindowProperty,
                                     totalDamageContainer,
                                     damageWindowContainer);
            RefreshRecoveryWarnings(recoverAfterDamageTakenPercentProperty,
                                    recoveryDamageTakenPercentProperty,
                                    recoverAfterDamageWindowProperty,
                                    recoveryDamageWindowPercentProperty,
                                    recoveryDamageWindowSecondsProperty,
                                    warningBox);
        });
    }

    /// <summary>
    /// Refreshes steal-section warnings without mutating authored values.
    /// </summary>
    /// <param name="activeTargetBiasPercentProperty">Serialized active-target bias percentage property.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshStealWarnings(SerializedProperty activeTargetBiasPercentProperty, HelpBox warningBox)
    {
        List<string> warnings = new List<string>();

        if (activeTargetBiasPercentProperty != null &&
            (activeTargetBiasPercentProperty.floatValue < 0f || activeTargetBiasPercentProperty.floatValue > 100f))
        {
            warnings.Add("Active Target Bias % is outside 0-100. Runtime clamps it while baking.");
        }

        ApplyWarnings(warnings, warningBox);
    }

    /// <summary>
    /// Refreshes recovery-section warnings without mutating authored values.
    /// </summary>
    /// <param name="recoverAfterDamageTakenPercentProperty">Serialized total-damage recovery toggle.</param>
    /// <param name="recoveryDamageTakenPercentProperty">Serialized total-damage percentage threshold.</param>
    /// <param name="recoverAfterDamageWindowProperty">Serialized timed-damage recovery toggle.</param>
    /// <param name="recoveryDamageWindowPercentProperty">Serialized timed-damage percentage threshold.</param>
    /// <param name="recoveryDamageWindowSecondsProperty">Serialized timed-damage window duration.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshRecoveryWarnings(SerializedProperty recoverAfterDamageTakenPercentProperty,
                                                SerializedProperty recoveryDamageTakenPercentProperty,
                                                SerializedProperty recoverAfterDamageWindowProperty,
                                                SerializedProperty recoveryDamageWindowPercentProperty,
                                                SerializedProperty recoveryDamageWindowSecondsProperty,
                                                HelpBox warningBox)
    {
        List<string> warnings = new List<string>();

        if (recoverAfterDamageTakenPercentProperty != null &&
            recoverAfterDamageTakenPercentProperty.boolValue &&
            recoveryDamageTakenPercentProperty != null &&
            recoveryDamageTakenPercentProperty.floatValue <= 0f)
        {
            warnings.Add("Damage Taken % is enabled but not positive, so total-damage recovery will never trigger.");
        }

        if (recoverAfterDamageWindowProperty != null &&
            recoverAfterDamageWindowProperty.boolValue)
        {
            if (recoveryDamageWindowPercentProperty != null && recoveryDamageWindowPercentProperty.floatValue <= 0f)
                warnings.Add("Window Damage % is enabled but not positive, so timed recovery will never trigger.");

            if (recoveryDamageWindowSecondsProperty != null && recoveryDamageWindowSecondsProperty.floatValue <= 0f)
                warnings.Add("Window Seconds is enabled but not positive, so timed recovery will never trigger.");
        }

        ApplyWarnings(warnings, warningBox);
    }

    /// <summary>
    /// Writes warning lines to a HelpBox and hides it when there are no warnings.
    /// </summary>
    /// <param name="warnings">Warning lines to display.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void ApplyWarnings(List<string> warnings, HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        if (warnings == null || warnings.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warnings);
        warningBox.messageType = HelpBoxMessageType.Warning;
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
