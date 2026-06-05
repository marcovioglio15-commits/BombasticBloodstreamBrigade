using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds module payload forms that are primarily field-driven and delegates chart-heavy payloads to visualization utilities.
/// </summary>
public static class PowerUpModuleDefinitionPayloadDrawerUtility
{
    #region Constants
    private const float AvailableVariablesBoxHeight = 46f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, Action> characterTuningRefreshByKey = new Dictionary<string, Action>(StringComparer.Ordinal);
    private static string activeCharacterTuningFormulaKey = string.Empty;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the payload editor for the provided module kind.
    /// </summary>
    /// <param name="payloadContainer">Container that will host the payload controls.</param>
    /// <param name="payloadProperty">Serialized payload property to edit.</param>
    /// <param name="moduleKind">Module kind that selects the UI variant.</param>
    /// <param name="payloadLabel">Optional label used by the generic payload fallback.</param>
    /// <param name="showActiveTriggerCharacterTuningOption">True when binding context supports active-trigger-scoped Character Tuning.</param>
    public static void BuildPayloadEditor(VisualElement payloadContainer,
                                          SerializedProperty payloadProperty,
                                          PowerUpModuleKind moduleKind,
                                          string payloadLabel,
                                          bool showActiveTriggerCharacterTuningOption = false)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerHoldCharge:
                BuildHoldChargePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.GateResource:
                BuildResourceGatePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ProjectileSplit:
                PowerUpModuleDefinitionVisualizationUtility.BuildProjectileSplitPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.SpawnObject:
                BuildSpawnObjectPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.Dash:
                PowerUpDashPayloadDrawerUtility.BuildDashPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ImpactFrame:
                PowerUpImpactFramePayloadDrawerUtility.BuildImpactFramePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.Heal:
                BuildHealPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.StateSuppressShooting:
                BuildSuppressShootingPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.CharacterTuning:
                BuildCharacterTuningPayloadUi(payloadContainer,
                                              payloadProperty,
                                              showActiveTriggerCharacterTuningOption);
                return;
            case PowerUpModuleKind.ProjectilesPatternCone:
                PowerUpModuleDefinitionVisualizationUtility.BuildProjectilePatternConePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.OrbitalProjectiles:
                BuildOrbitalProjectilesPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.OrbitalProjections:
                PowerUpOrbitalProjectionsPayloadDrawerUtility.BuildPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.LaserBeam:
                BuildLaserBeamPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.SwitchWeapon:
                PowerUpModuleSwitchWeaponPayloadDrawerUtility.Build(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.AreaTickApplyElement:
                BuildAreaTickApplyElementPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.Stackable:
                BuildStackablePayloadUi(payloadContainer, payloadProperty);
                return;
        }

        BuildDefaultPayloadUi(payloadContainer, payloadProperty, payloadLabel);
    }

    /// <summary>
    /// Creates a serialized field using the shared scaling-aware element factory.
    /// </summary>
    /// <param name="parent">Parent visual element that receives the field.</param>
    /// <param name="property">Serialized property to draw.</param>
    /// <param name="label">Visible label for the created field.</param>
    /// <param name="allowTokenScaling">True when string token fields should expose Add Scaling.</param>
    /// <returns>Created field root, or null when the input is invalid.</returns>
    public static VisualElement AddField(VisualElement parent,
                                         SerializedProperty property,
                                         string label,
                                         bool allowTokenScaling = false)
    {
        if (parent == null)
            return null;

        if (property == null)
            return null;

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label,
                                                                           null,
                                                                           allowTokenScaling);
        parent.Add(field);
        return field;
    }

    #endregion

    #region Generic Payload
    private static void BuildDefaultPayloadUi(VisualElement payloadContainer,
                                              SerializedProperty payloadProperty,
                                              string payloadLabel)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        string resolvedLabel = string.IsNullOrWhiteSpace(payloadLabel) ? payloadProperty.displayName : payloadLabel;
        Foldout payloadFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(payloadProperty,
                                                                                           resolvedLabel,
                                                                                           string.Format("Payload:{0}", resolvedLabel),
                                                                                           true);
        payloadContainer.Add(payloadFoldout);

        if (!payloadProperty.hasVisibleChildren)
        {
            AddField(payloadFoldout, payloadProperty, resolvedLabel);
            return;
        }

        SerializedProperty iterator = payloadProperty.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        int parentDepth = payloadProperty.depth;
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty))
                break;

            enterChildren = false;

            if (iterator.depth != parentDepth + 1)
                continue;

            SerializedProperty childProperty = iterator.Copy();
            AddField(payloadFoldout, childProperty, childProperty.displayName);
        }
    }
    #endregion

    #region Specialized Payloads
    /// <summary>
    /// Builds the AreaTickApplyElement payload UI with scaling-aware nested fields and context-sensitive effect sections.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the area-tick controls.</param>
    /// <param name="areaTickPayloadProperty">Serialized AreaTickApplyElement payload property.</param>
    private static void BuildAreaTickApplyElementPayloadUi(VisualElement payloadContainer, SerializedProperty areaTickPayloadProperty)
    {
        if (payloadContainer == null || areaTickPayloadProperty == null)
            return;

        SerializedProperty effectDataProperty = areaTickPayloadProperty.FindPropertyRelative("effectData");
        SerializedProperty stacksPerTickProperty = areaTickPayloadProperty.FindPropertyRelative("stacksPerTick");
        SerializedProperty applyIntervalSecondsProperty = areaTickPayloadProperty.FindPropertyRelative("applyIntervalSeconds");

        if (effectDataProperty == null ||
            stacksPerTickProperty == null ||
            applyIntervalSecondsProperty == null)
        {
            HelpBox errorBox = new HelpBox("Area Tick Apply Element payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        Foldout tickFoldout = CreatePayloadFoldout("Tick", true);
        payloadContainer.Add(tickFoldout);
        VisualElement stacksPerTickField = AddField(tickFoldout, stacksPerTickProperty, "Stacks Per Tick");
        VisualElement applyIntervalSecondsField = AddField(tickFoldout, applyIntervalSecondsProperty, "Apply Interval Seconds");

        SerializedProperty elementTypeProperty = effectDataProperty.FindPropertyRelative("elementType");
        SerializedProperty effectKindProperty = effectDataProperty.FindPropertyRelative("effectKind");
        SerializedProperty procModeProperty = effectDataProperty.FindPropertyRelative("procMode");
        SerializedProperty reapplyModeProperty = effectDataProperty.FindPropertyRelative("reapplyMode");
        SerializedProperty procThresholdStacksProperty = effectDataProperty.FindPropertyRelative("procThresholdStacks");
        SerializedProperty maximumStacksProperty = effectDataProperty.FindPropertyRelative("maximumStacks");
        SerializedProperty stackDecayPerSecondProperty = effectDataProperty.FindPropertyRelative("stackDecayPerSecond");
        SerializedProperty consumeStacksOnProcProperty = effectDataProperty.FindPropertyRelative("consumeStacksOnProc");
        SerializedProperty dotDamagePerTickProperty = effectDataProperty.FindPropertyRelative("dotDamagePerTick");
        SerializedProperty dotTickIntervalProperty = effectDataProperty.FindPropertyRelative("dotTickInterval");
        SerializedProperty dotDurationSecondsProperty = effectDataProperty.FindPropertyRelative("dotDurationSeconds");
        SerializedProperty impedimentSlowPercentPerStackProperty = effectDataProperty.FindPropertyRelative("impedimentSlowPercentPerStack");
        SerializedProperty impedimentProcSlowPercentProperty = effectDataProperty.FindPropertyRelative("impedimentProcSlowPercent");
        SerializedProperty impedimentMaxSlowPercentProperty = effectDataProperty.FindPropertyRelative("impedimentMaxSlowPercent");
        SerializedProperty impedimentDurationSecondsProperty = effectDataProperty.FindPropertyRelative("impedimentDurationSeconds");

        if (elementTypeProperty == null ||
            effectKindProperty == null ||
            procModeProperty == null ||
            reapplyModeProperty == null ||
            procThresholdStacksProperty == null ||
            maximumStacksProperty == null ||
            stackDecayPerSecondProperty == null ||
            consumeStacksOnProcProperty == null ||
            dotDamagePerTickProperty == null ||
            dotTickIntervalProperty == null ||
            dotDurationSecondsProperty == null ||
            impedimentSlowPercentPerStackProperty == null ||
            impedimentProcSlowPercentProperty == null ||
            impedimentMaxSlowPercentProperty == null ||
            impedimentDurationSecondsProperty == null)
        {
            HelpBox errorBox = new HelpBox("Area Tick elemental effect fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        Foldout elementFoldout = CreatePayloadFoldout("Element Behaviour", true);
        payloadContainer.Add(elementFoldout);
        VisualElement elementTypeField = AddField(elementFoldout, elementTypeProperty, "Element Type");
        VisualElement effectKindField = AddField(elementFoldout, effectKindProperty, "Effect Kind");
        VisualElement procModeField = AddField(elementFoldout, procModeProperty, "Proc Mode");
        VisualElement reapplyModeField = AddField(elementFoldout, reapplyModeProperty, "Reapply Mode");

        Foldout stackingFoldout = CreatePayloadFoldout("Stacking", true);
        payloadContainer.Add(stackingFoldout);
        VisualElement procThresholdStacksField = AddField(stackingFoldout, procThresholdStacksProperty, "Proc Threshold Stacks");
        VisualElement maximumStacksField = AddField(stackingFoldout, maximumStacksProperty, "Maximum Stacks");
        VisualElement stackDecayPerSecondField = AddField(stackingFoldout, stackDecayPerSecondProperty, "Stack Decay Per Second");
        VisualElement consumeStacksOnProcField = AddField(stackingFoldout, consumeStacksOnProcProperty, "Consume Stacks On Proc");

        Foldout dotsFoldout = CreatePayloadFoldout("Dots Effect", false);
        payloadContainer.Add(dotsFoldout);
        VisualElement dotDamagePerTickField = AddField(dotsFoldout, dotDamagePerTickProperty, "Dot Damage Per Tick");
        VisualElement dotTickIntervalField = AddField(dotsFoldout, dotTickIntervalProperty, "Dot Tick Interval");
        VisualElement dotDurationSecondsField = AddField(dotsFoldout, dotDurationSecondsProperty, "Dot Duration Seconds");

        Foldout impedimentFoldout = CreatePayloadFoldout("Impediment Effect", false);
        payloadContainer.Add(impedimentFoldout);
        VisualElement impedimentSlowPercentPerStackField = AddField(impedimentFoldout, impedimentSlowPercentPerStackProperty, "Slow Percent Per Stack");
        VisualElement impedimentProcSlowPercentField = AddField(impedimentFoldout, impedimentProcSlowPercentProperty, "Proc Slow Percent");
        VisualElement impedimentMaxSlowPercentField = AddField(impedimentFoldout, impedimentMaxSlowPercentProperty, "Max Slow Percent");
        VisualElement impedimentDurationSecondsField = AddField(impedimentFoldout, impedimentDurationSecondsProperty, "Duration Seconds");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);

        Action refreshView = () =>
        {
            ElementalEffectKind effectKind = (ElementalEffectKind)effectKindProperty.enumValueIndex;
            ElementalProcMode procMode = (ElementalProcMode)procModeProperty.enumValueIndex;
            dotsFoldout.style.display = effectKind == ElementalEffectKind.Dots ? DisplayStyle.Flex : DisplayStyle.None;
            impedimentFoldout.style.display = effectKind == ElementalEffectKind.Impediment ? DisplayStyle.Flex : DisplayStyle.None;
            impedimentSlowPercentPerStackField.style.display = effectKind == ElementalEffectKind.Impediment &&
                                                               procMode == ElementalProcMode.ProgressiveUntilThreshold
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshAreaTickApplyElementWarnings(stacksPerTickProperty,
                                                applyIntervalSecondsProperty,
                                                elementTypeProperty,
                                                effectKindProperty,
                                                procModeProperty,
                                                procThresholdStacksProperty,
                                                maximumStacksProperty,
                                                stackDecayPerSecondProperty,
                                                dotDamagePerTickProperty,
                                                dotTickIntervalProperty,
                                                dotDurationSecondsProperty,
                                                impedimentSlowPercentPerStackProperty,
                                                impedimentProcSlowPercentProperty,
                                                impedimentMaxSlowPercentProperty,
                                                impedimentDurationSecondsProperty,
                                                warningBox);
        };

        RegisterRefreshCallback(stacksPerTickField, refreshView);
        RegisterRefreshCallback(applyIntervalSecondsField, refreshView);
        RegisterRefreshCallback(elementTypeField, refreshView);
        RegisterRefreshCallback(effectKindField, refreshView);
        RegisterRefreshCallback(procModeField, refreshView);
        RegisterRefreshCallback(reapplyModeField, refreshView);
        RegisterRefreshCallback(procThresholdStacksField, refreshView);
        RegisterRefreshCallback(maximumStacksField, refreshView);
        RegisterRefreshCallback(stackDecayPerSecondField, refreshView);
        RegisterRefreshCallback(consumeStacksOnProcField, refreshView);
        RegisterRefreshCallback(dotDamagePerTickField, refreshView);
        RegisterRefreshCallback(dotTickIntervalField, refreshView);
        RegisterRefreshCallback(dotDurationSecondsField, refreshView);
        RegisterRefreshCallback(impedimentSlowPercentPerStackField, refreshView);
        RegisterRefreshCallback(impedimentProcSlowPercentField, refreshView);
        RegisterRefreshCallback(impedimentMaxSlowPercentField, refreshView);
        RegisterRefreshCallback(impedimentDurationSecondsField, refreshView);
        refreshView();
    }

    private static void BuildHoldChargePayloadUi(VisualElement payloadContainer, SerializedProperty holdChargePayloadProperty)
    {
        if (payloadContainer == null || holdChargePayloadProperty == null)
            return;

        SerializedProperty requiredChargeProperty = holdChargePayloadProperty.FindPropertyRelative("requiredCharge");
        SerializedProperty maximumChargeProperty = holdChargePayloadProperty.FindPropertyRelative("maximumCharge");
        SerializedProperty chargeRatePerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("chargeRatePerSecond");
        SerializedProperty decayAfterReleaseProperty = holdChargePayloadProperty.FindPropertyRelative("decayAfterRelease");
        SerializedProperty decayAfterReleasePercentPerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("decayAfterReleasePercentPerSecond");
        SerializedProperty passiveChargeGainWhileReleasedProperty = holdChargePayloadProperty.FindPropertyRelative("passiveChargeGainWhileReleased");
        SerializedProperty passiveChargeGainPercentPerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("passiveChargeGainPercentPerSecond");
        SerializedProperty laserDurationSecondsProperty = holdChargePayloadProperty.FindPropertyRelative("laserDurationSeconds");
        SerializedProperty useChargedLaserBeamProperty = holdChargePayloadProperty.FindPropertyRelative("useChargedLaserBeam");
        SerializedProperty chargedLaserDurationSecondsProperty = holdChargePayloadProperty.FindPropertyRelative("chargedLaserDurationSeconds");
        SerializedProperty chargedLaserBeamProperty = holdChargePayloadProperty.FindPropertyRelative("chargedLaserBeam");
        SerializedProperty slowPlayerWhileChargingProperty = holdChargePayloadProperty.FindPropertyRelative("slowPlayerWhileCharging");
        SerializedProperty maximumPlayerSlowPercentProperty = holdChargePayloadProperty.FindPropertyRelative("maximumPlayerSlowPercent");
        SerializedProperty playerSlowCurveProperty = holdChargePayloadProperty.FindPropertyRelative("playerSlowCurve");
        SerializedProperty ignoreInheritedPlayerVelocityXProperty = holdChargePayloadProperty.FindPropertyRelative("ignoreInheritedPlayerVelocityX");
        SerializedProperty ignoreInheritedPlayerVelocityZProperty = holdChargePayloadProperty.FindPropertyRelative("ignoreInheritedPlayerVelocityZ");

        if (requiredChargeProperty == null ||
            maximumChargeProperty == null ||
            chargeRatePerSecondProperty == null ||
            decayAfterReleaseProperty == null ||
            decayAfterReleasePercentPerSecondProperty == null ||
            passiveChargeGainWhileReleasedProperty == null ||
            passiveChargeGainPercentPerSecondProperty == null ||
            laserDurationSecondsProperty == null ||
            useChargedLaserBeamProperty == null ||
            chargedLaserDurationSecondsProperty == null ||
            chargedLaserBeamProperty == null ||
            slowPlayerWhileChargingProperty == null ||
            maximumPlayerSlowPercentProperty == null ||
            playerSlowCurveProperty == null ||
            ignoreInheritedPlayerVelocityXProperty == null ||
            ignoreInheritedPlayerVelocityZProperty == null)
        {
            HelpBox errorBox = new HelpBox("Hold charge payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, requiredChargeProperty, "Required Charge");
        AddField(payloadContainer, maximumChargeProperty, "Maximum Charge");
        AddField(payloadContainer, chargeRatePerSecondProperty, "Charge Rate Per Second");
        AddField(payloadContainer, decayAfterReleaseProperty, "Decay After Release");

        VisualElement decayContainer = new VisualElement();
        decayContainer.style.marginLeft = 12f;
        payloadContainer.Add(decayContainer);
        AddField(decayContainer, decayAfterReleasePercentPerSecondProperty, "Decay Percent Per Second");

        AddField(payloadContainer, passiveChargeGainWhileReleasedProperty, "Passive Gain While Released");

        VisualElement passiveGainContainer = new VisualElement();
        passiveGainContainer.style.marginLeft = 12f;
        payloadContainer.Add(passiveGainContainer);
        AddField(passiveGainContainer, passiveChargeGainPercentPerSecondProperty, "Passive Gain Percent Per Second");
        AddField(payloadContainer, laserDurationSecondsProperty, "Laser Duration Seconds");
        AddField(payloadContainer, useChargedLaserBeamProperty, "Use Charged Laser Beam");

        VisualElement chargedLaserContainer = new VisualElement();
        chargedLaserContainer.style.marginLeft = 12f;
        payloadContainer.Add(chargedLaserContainer);
        AddField(chargedLaserContainer, chargedLaserDurationSecondsProperty, "Charged Laser Duration Seconds");
        BuildLaserBeamPayloadUi(chargedLaserContainer,
                                chargedLaserBeamProperty,
                                "A fully charged release fires this standalone Laser Beam once. It uses only this Trigger Hold Charge payload and ignores passive tools or other power-up hooks.");
        AddField(payloadContainer, ignoreInheritedPlayerVelocityXProperty, "Ignore Inherited Velocity X");
        AddField(payloadContainer, ignoreInheritedPlayerVelocityZProperty, "Ignore Inherited Velocity Z");

        AddField(payloadContainer, slowPlayerWhileChargingProperty, "Slow Player While Charging");

        VisualElement playerSlowContainer = new VisualElement();
        playerSlowContainer.style.marginLeft = 12f;
        payloadContainer.Add(playerSlowContainer);
        AddField(playerSlowContainer, maximumPlayerSlowPercentProperty, "Maximum Player Slow Percent");
        AddField(playerSlowContainer, playerSlowCurveProperty, "Player Slow Curve");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);

        UpdateBooleanContainerVisibility(decayAfterReleaseProperty, decayContainer);
        UpdateBooleanContainerVisibility(passiveChargeGainWhileReleasedProperty, passiveGainContainer);
        UpdateBooleanContainerVisibility(useChargedLaserBeamProperty, chargedLaserContainer);
        UpdateBooleanContainerVisibility(slowPlayerWhileChargingProperty, playerSlowContainer);
        Action refreshWarnings = () =>
        {
            RefreshHoldChargeWarnings(requiredChargeProperty,
                                      maximumChargeProperty,
                                      chargeRatePerSecondProperty,
                                      decayAfterReleaseProperty,
                                      decayAfterReleasePercentPerSecondProperty,
                                      passiveChargeGainWhileReleasedProperty,
                                      passiveChargeGainPercentPerSecondProperty,
                                      laserDurationSecondsProperty,
                                      useChargedLaserBeamProperty,
                                      chargedLaserDurationSecondsProperty,
                                      slowPlayerWhileChargingProperty,
                                      maximumPlayerSlowPercentProperty,
                                      playerSlowCurveProperty,
                                      warningBox);
        };

        refreshWarnings();

        payloadContainer.TrackPropertyValue(decayAfterReleaseProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, decayContainer);
            refreshWarnings();
        });
        payloadContainer.TrackPropertyValue(passiveChargeGainWhileReleasedProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, passiveGainContainer);
            refreshWarnings();
        });
        payloadContainer.TrackPropertyValue(useChargedLaserBeamProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, chargedLaserContainer);
            refreshWarnings();
        });
        payloadContainer.TrackPropertyValue(slowPlayerWhileChargingProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, playerSlowContainer);
            refreshWarnings();
        });
        RegisterHoldChargeWarningRefresh(payloadContainer,
                                         refreshWarnings,
                                         requiredChargeProperty,
                                         maximumChargeProperty,
                                         chargeRatePerSecondProperty,
                                         decayAfterReleasePercentPerSecondProperty,
                                         passiveChargeGainPercentPerSecondProperty,
                                         laserDurationSecondsProperty,
                                         chargedLaserDurationSecondsProperty,
                                         maximumPlayerSlowPercentProperty,
                                         playerSlowCurveProperty);
    }

    private static void BuildResourceGatePayloadUi(VisualElement payloadContainer, SerializedProperty resourceGatePayloadProperty)
    {
        if (payloadContainer == null || resourceGatePayloadProperty == null)
            return;

        SerializedProperty activationResourceProperty = resourceGatePayloadProperty.FindPropertyRelative("activationResource");
        SerializedProperty maintenanceResourceProperty = resourceGatePayloadProperty.FindPropertyRelative("maintenanceResource");
        SerializedProperty maximumEnergyProperty = resourceGatePayloadProperty.FindPropertyRelative("maximumEnergy");
        SerializedProperty activationCostProperty = resourceGatePayloadProperty.FindPropertyRelative("activationCost");
        SerializedProperty maintenanceCostPerSecondProperty = resourceGatePayloadProperty.FindPropertyRelative("maintenanceCostPerSecond");
        SerializedProperty isToggleableProperty = resourceGatePayloadProperty.FindPropertyRelative("isToggleable");
        SerializedProperty maintenanceTicksPerSecondProperty = resourceGatePayloadProperty.FindPropertyRelative("maintenanceTicksPerSecond");
        SerializedProperty minimumActivationEnergyPercentProperty = resourceGatePayloadProperty.FindPropertyRelative("minimumActivationEnergyPercent");
        SerializedProperty chargeTypeProperty = resourceGatePayloadProperty.FindPropertyRelative("chargeType");
        SerializedProperty chargePerTriggerProperty = resourceGatePayloadProperty.FindPropertyRelative("chargePerTrigger");
        SerializedProperty cooldownSecondsProperty = resourceGatePayloadProperty.FindPropertyRelative("cooldownSeconds");
        SerializedProperty allowRechargeDuringToggleStartupLockProperty = resourceGatePayloadProperty.FindPropertyRelative("allowRechargeDuringToggleStartupLock");

        if (activationResourceProperty == null ||
            maintenanceResourceProperty == null ||
            maximumEnergyProperty == null ||
            activationCostProperty == null ||
            maintenanceCostPerSecondProperty == null ||
            isToggleableProperty == null ||
            maintenanceTicksPerSecondProperty == null ||
            minimumActivationEnergyPercentProperty == null ||
            chargeTypeProperty == null ||
            chargePerTriggerProperty == null ||
            cooldownSecondsProperty == null ||
            allowRechargeDuringToggleStartupLockProperty == null)
        {
            HelpBox errorBox = new HelpBox("Resource gate payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, activationResourceProperty, "Activation Resource");
        AddField(payloadContainer, maintenanceResourceProperty, "Maintenance Resource");
        AddField(payloadContainer, maximumEnergyProperty, "Maximum Energy");
        AddField(payloadContainer, activationCostProperty, "Activation Cost");
        AddField(payloadContainer, maintenanceCostPerSecondProperty, "Maintenance Cost Per Second");
        AddField(payloadContainer, minimumActivationEnergyPercentProperty, "Minimum Energy Activation Percent");
        AddField(payloadContainer, chargeTypeProperty, "Charge Type");
        AddField(payloadContainer, chargePerTriggerProperty, "Charge Per Trigger");
        AddField(payloadContainer, cooldownSecondsProperty, "Cooldown Seconds");
        AddField(payloadContainer, isToggleableProperty, "Is Toggleable");

        VisualElement toggleableContainer = new VisualElement();
        toggleableContainer.style.marginLeft = 12f;
        payloadContainer.Add(toggleableContainer);

        HelpBox toggleableHelpBox = new HelpBox("When toggleable is enabled, Cooldown Seconds becomes the startup lock interval: maintenance is not paid and the power-up cannot be disabled during that time.", HelpBoxMessageType.Info);
        toggleableContainer.Add(toggleableHelpBox);
        AddField(toggleableContainer, maintenanceTicksPerSecondProperty, "Maintenance Ticks Per Second");
        AddField(toggleableContainer, allowRechargeDuringToggleStartupLockProperty, "Allow Recharge During Startup Lock");

        UpdateBooleanContainerVisibility(isToggleableProperty, toggleableContainer);
        payloadContainer.TrackPropertyValue(isToggleableProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, toggleableContainer);
        });
    }

    private static void BuildSpawnObjectPayloadUi(VisualElement payloadContainer, SerializedProperty spawnPayloadProperty)
    {
        PowerUpBombPayloadDrawerUtility.BuildBombPayloadUi(payloadContainer, spawnPayloadProperty);
    }

    private static void BuildHealPayloadUi(VisualElement payloadContainer, SerializedProperty healPayloadProperty)
    {
        if (payloadContainer == null || healPayloadProperty == null)
            return;

        SerializedProperty applyModeProperty = healPayloadProperty.FindPropertyRelative("applyMode");
        SerializedProperty healAmountProperty = healPayloadProperty.FindPropertyRelative("healAmount");
        SerializedProperty durationSecondsProperty = healPayloadProperty.FindPropertyRelative("durationSeconds");
        SerializedProperty tickIntervalSecondsProperty = healPayloadProperty.FindPropertyRelative("tickIntervalSeconds");
        SerializedProperty stackPolicyProperty = healPayloadProperty.FindPropertyRelative("stackPolicy");

        if (applyModeProperty == null ||
            healAmountProperty == null ||
            durationSecondsProperty == null ||
            tickIntervalSecondsProperty == null ||
            stackPolicyProperty == null)
        {
            HelpBox errorBox = new HelpBox("Heal payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, applyModeProperty, "Apply Mode");
        AddField(payloadContainer, healAmountProperty, "Heal Amount");

        VisualElement overTimeContainer = new VisualElement();
        overTimeContainer.style.marginLeft = 12f;
        payloadContainer.Add(overTimeContainer);
        AddField(overTimeContainer, durationSecondsProperty, "Duration Seconds");
        AddField(overTimeContainer, tickIntervalSecondsProperty, "Tick Interval Seconds");
        AddField(overTimeContainer, stackPolicyProperty, "Stack Policy");

        UpdateHealOverTimeContainerVisibility(applyModeProperty, overTimeContainer);
        payloadContainer.TrackPropertyValue(applyModeProperty, changedProperty =>
        {
            UpdateHealOverTimeContainerVisibility(changedProperty, overTimeContainer);
        });
    }

    private static void BuildSuppressShootingPayloadUi(VisualElement payloadContainer, SerializedProperty suppressPayloadProperty)
    {
        if (payloadContainer == null || suppressPayloadProperty == null)
            return;

        SerializedProperty suppressBaseShootingProperty = suppressPayloadProperty.FindPropertyRelative("suppressBaseShootingWhileActive");
        SerializedProperty interruptOtherSlotOnEnterProperty = suppressPayloadProperty.FindPropertyRelative("interruptOtherSlotOnEnter");
        SerializedProperty interruptOtherSlotChargingOnlyProperty = suppressPayloadProperty.FindPropertyRelative("interruptOtherSlotChargingOnly");

        if (suppressBaseShootingProperty == null ||
            interruptOtherSlotOnEnterProperty == null ||
            interruptOtherSlotChargingOnlyProperty == null)
        {
            HelpBox errorBox = new HelpBox("Suppress Shooting payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, suppressBaseShootingProperty, "Suppress Base Shooting While Active");
        AddField(payloadContainer, interruptOtherSlotOnEnterProperty, "Interrupt Other Slot On Enter");

        VisualElement interruptOptionsContainer = new VisualElement();
        interruptOptionsContainer.style.marginLeft = 12f;
        payloadContainer.Add(interruptOptionsContainer);
        AddField(interruptOptionsContainer, interruptOtherSlotChargingOnlyProperty, "Interrupt Other Slot Charging Only");

        UpdateInterruptOptionsVisibility(interruptOtherSlotOnEnterProperty, interruptOptionsContainer);
        payloadContainer.TrackPropertyValue(interruptOtherSlotOnEnterProperty, changedProperty =>
        {
            UpdateInterruptOptionsVisibility(changedProperty, interruptOptionsContainer);
        });
    }

    private static void BuildCharacterTuningPayloadUi(VisualElement payloadContainer,
                                                      SerializedProperty characterTuningPayloadProperty,
                                                      bool showActiveTriggerCharacterTuningOption)
    {
        if (payloadContainer == null || characterTuningPayloadProperty == null)
            return;

        SerializedObject serializedObject = characterTuningPayloadProperty.serializedObject;
        SerializedProperty applyFormulasOnlyOnActiveTriggerProperty = characterTuningPayloadProperty.FindPropertyRelative("applyFormulasOnlyOnActiveTrigger");
        SerializedProperty formulasProperty = characterTuningPayloadProperty.FindPropertyRelative("formulas");

        if (applyFormulasOnlyOnActiveTriggerProperty == null || formulasProperty == null)
        {
            HelpBox errorBox = new HelpBox("Character Tuning payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        HelpBox infoBox = new HelpBox("Each entry uses [TargetStat] = expression syntax. The right-hand expression supports the same operators and functions available in Add Scaling formulas, including switch(condition, case:value, ..., fallback).", HelpBoxMessageType.Info);
        payloadContainer.Add(infoBox);

        if (showActiveTriggerCharacterTuningOption)
        {
            AddField(payloadContainer,
                     applyFormulasOnlyOnActiveTriggerProperty,
                     "Apply Only On Active Trigger");
            HelpBox triggerScopeInfoBox = new HelpBox("When enabled here, this active power-up skips acquisition-time Character Tuning and applies the formulas only while its non-toggle activation trigger is executed.", HelpBoxMessageType.Info);
            payloadContainer.Add(triggerScopeInfoBox);
        }

        string formulasLabel = showActiveTriggerCharacterTuningOption
            ? "Character Tuning Formulas"
            : "Acquisition Formulas";
        VisualElement formulasField = AddField(payloadContainer, formulasProperty, formulasLabel);
        ScrollView availableVariablesScrollView = new ScrollView(ScrollViewMode.Vertical);
        availableVariablesScrollView.style.marginTop = 2f;
        availableVariablesScrollView.style.height = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.maxHeight = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.flexShrink = 0f;
        payloadContainer.Add(availableVariablesScrollView);

        Label availableVariablesLabel = new Label(string.Empty);
        availableVariablesLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        availableVariablesLabel.style.whiteSpace = WhiteSpace.Normal;
        availableVariablesLabel.style.flexShrink = 0f;
        availableVariablesScrollView.Add(availableVariablesLabel);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);
        string formulasPropertyPath = formulasProperty.propertyPath;
        string formulaKey = BuildCharacterTuningFormulaKey(serializedObject, formulasPropertyPath);
        RegisterCharacterTuningRefresher(formulaKey, RefreshCharacterTuningUi);
        payloadContainer.RegisterCallback<DetachFromPanelEvent>(evt => UnregisterCharacterTuningRefresher(formulaKey));
        payloadContainer.RegisterCallback<MouseDownEvent>(evt =>
        {
            SetActiveCharacterTuningFormula(formulaKey);
        });
        payloadContainer.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (evt.relatedTarget is VisualElement nextFocusedElement && payloadContainer.Contains(nextFocusedElement))
                return;

            ClearActiveCharacterTuningFormula(formulaKey);
        });

        if (formulasField != null)
        {
            formulasField.RegisterCallback<FocusInEvent>(evt =>
            {
                SetActiveCharacterTuningFormula(formulaKey);
            });
            formulasField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                SetActiveCharacterTuningFormula(formulaKey);
            });
        }

        RefreshCharacterTuningUi();
        RegisterCharacterTuningFormulaRefresh(payloadContainer,
                                              serializedObject,
                                              formulasPropertyPath,
                                              RefreshCharacterTuningUi);

        void RefreshCharacterTuningUi()
        {
            SerializedProperty reboundFormulasProperty = serializedObject != null
                ? serializedObject.FindProperty(formulasPropertyPath)
                : null;
            RefreshCharacterTuningAvailableVariables(serializedObject, availableVariablesLabel);
            RefreshCharacterTuningWarnings(serializedObject, reboundFormulasProperty, warningBox);
            availableVariablesScrollView.style.display = IsActiveCharacterTuningFormula(formulaKey)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    private static void BuildStackablePayloadUi(VisualElement payloadContainer, SerializedProperty stackablePayloadProperty)
    {
        if (payloadContainer == null || stackablePayloadProperty == null)
            return;

        SerializedProperty maxAcquisitionsProperty = stackablePayloadProperty.FindPropertyRelative("maxAcquisitions");

        if (maxAcquisitionsProperty == null)
        {
            HelpBox errorBox = new HelpBox("Stackable payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        HelpBox infoBox = new HelpBox("Stackable controls how many times the same power-up can be acquired from milestones. Pair it with Character Tuning or Orbital Projections so repeated pickups have a meaningful acquisition effect.", HelpBoxMessageType.Info);
        payloadContainer.Add(infoBox);
        AddField(payloadContainer, maxAcquisitionsProperty, "Max Acquisitions");
    }

    private static void BuildOrbitalProjectilesPayloadUi(VisualElement payloadContainer, SerializedProperty orbitalPayloadProperty)
    {
        if (payloadContainer == null || orbitalPayloadProperty == null)
            return;

        SerializedProperty pathModeProperty = orbitalPayloadProperty.FindPropertyRelative("pathMode");
        SerializedProperty radialEntrySpeedProperty = orbitalPayloadProperty.FindPropertyRelative("radialEntrySpeed");
        SerializedProperty heightOffsetProperty = orbitalPayloadProperty.FindPropertyRelative("heightOffset");
        SerializedProperty goldenAngleDegreesProperty = orbitalPayloadProperty.FindPropertyRelative("goldenAngleDegrees");
        SerializedProperty orbitalSpeedProperty = orbitalPayloadProperty.FindPropertyRelative("orbitalSpeed");
        SerializedProperty orbitRadiusMinProperty = orbitalPayloadProperty.FindPropertyRelative("orbitRadiusMin");
        SerializedProperty orbitRadiusMaxProperty = orbitalPayloadProperty.FindPropertyRelative("orbitRadiusMax");
        SerializedProperty orbitPulseFrequencyProperty = orbitalPayloadProperty.FindPropertyRelative("orbitPulseFrequency");
        SerializedProperty orbitEntryRatioProperty = orbitalPayloadProperty.FindPropertyRelative("orbitEntryRatio");
        SerializedProperty orbitBlendDurationProperty = orbitalPayloadProperty.FindPropertyRelative("orbitBlendDuration");
        SerializedProperty spiralStartRadiusProperty = orbitalPayloadProperty.FindPropertyRelative("spiralStartRadius");
        SerializedProperty spiralMaximumRadiusProperty = orbitalPayloadProperty.FindPropertyRelative("spiralMaximumRadius");
        SerializedProperty spiralAngularSpeedDegreesPerSecondProperty = orbitalPayloadProperty.FindPropertyRelative("spiralAngularSpeedDegreesPerSecond");
        SerializedProperty spiralGrowthMultiplierProperty = orbitalPayloadProperty.FindPropertyRelative("spiralGrowthMultiplier");
        SerializedProperty spiralTurnsBeforeDespawnProperty = orbitalPayloadProperty.FindPropertyRelative("spiralTurnsBeforeDespawn");
        SerializedProperty spiralClockwiseProperty = orbitalPayloadProperty.FindPropertyRelative("spiralClockwise");

        if (pathModeProperty == null ||
            radialEntrySpeedProperty == null ||
            heightOffsetProperty == null ||
            goldenAngleDegreesProperty == null ||
            orbitalSpeedProperty == null ||
            orbitRadiusMinProperty == null ||
            orbitRadiusMaxProperty == null ||
            orbitPulseFrequencyProperty == null ||
            orbitEntryRatioProperty == null ||
            orbitBlendDurationProperty == null ||
            spiralStartRadiusProperty == null ||
            spiralMaximumRadiusProperty == null ||
            spiralAngularSpeedDegreesPerSecondProperty == null ||
            spiralGrowthMultiplierProperty == null ||
            spiralTurnsBeforeDespawnProperty == null ||
            spiralClockwiseProperty == null)
        {
            HelpBox errorBox = new HelpBox("Orbital projectiles payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, pathModeProperty, "Path Mode");
        AddField(payloadContainer, radialEntrySpeedProperty, "Radial Entry Speed");
        AddField(payloadContainer, heightOffsetProperty, "Height Offset");
        AddField(payloadContainer, goldenAngleDegreesProperty, "Golden Angle Degrees");

        VisualElement circleContainer = new VisualElement();
        circleContainer.style.marginLeft = 12f;
        payloadContainer.Add(circleContainer);
        AddField(circleContainer, orbitalSpeedProperty, "Orbital Speed");
        AddField(circleContainer, orbitRadiusMinProperty, "Orbit Radius Min");
        AddField(circleContainer, orbitRadiusMaxProperty, "Orbit Radius Max");
        AddField(circleContainer, orbitPulseFrequencyProperty, "Orbit Pulse Frequency");
        AddField(circleContainer, orbitEntryRatioProperty, "Orbit Entry Ratio");
        AddField(circleContainer, orbitBlendDurationProperty, "Orbit Blend Duration");

        VisualElement spiralContainer = new VisualElement();
        spiralContainer.style.marginLeft = 12f;
        payloadContainer.Add(spiralContainer);
        AddField(spiralContainer, spiralStartRadiusProperty, "Spiral Start Radius");
        AddField(spiralContainer, spiralMaximumRadiusProperty, "Spiral Maximum Radius");
        AddField(spiralContainer, spiralAngularSpeedDegreesPerSecondProperty, "Spiral Angular Speed Degrees Per Second");
        AddField(spiralContainer, spiralGrowthMultiplierProperty, "Spiral Growth Multiplier");
        AddField(spiralContainer, spiralTurnsBeforeDespawnProperty, "Spiral Turns Before Despawn");
        AddField(spiralContainer, spiralClockwiseProperty, "Spiral Clockwise");

        UpdateOrbitPathModeContainers(pathModeProperty, circleContainer, spiralContainer);
        payloadContainer.TrackPropertyValue(pathModeProperty, changedProperty =>
        {
            UpdateOrbitPathModeContainers(changedProperty, circleContainer, spiralContainer);
        });
    }

    private static void BuildLaserBeamPayloadUi(VisualElement payloadContainer,
                                                SerializedProperty laserBeamPayloadProperty,
                                                string infoText = null)
    {
        if (payloadContainer == null || laserBeamPayloadProperty == null)
            return;

        SerializedProperty damageMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("damageMultiplier");
        SerializedProperty continuousDamagePerSecondMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("continuousDamagePerSecondMultiplier");
        SerializedProperty virtualProjectileSpeedMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("virtualProjectileSpeedMultiplier");
        SerializedProperty damageTickIntervalSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("damageTickIntervalSeconds");
        SerializedProperty maximumContinuousActiveSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("maximumContinuousActiveSeconds");
        SerializedProperty cooldownSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("cooldownSeconds");
        SerializedProperty maximumBounceSegmentsProperty = laserBeamPayloadProperty.FindPropertyRelative("maximumBounceSegments");
        SerializedProperty applyPlayerHandlingNerfWhileFiringProperty = laserBeamPayloadProperty.FindPropertyRelative("applyPlayerHandlingNerfWhileFiring");
        SerializedProperty firingMoveSpeedMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("firingMoveSpeedMultiplier");
        SerializedProperty firingRotationSpeedMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("firingRotationSpeedMultiplier");
        SerializedProperty visualPresetIdProperty = laserBeamPayloadProperty.FindPropertyRelative("visualPresetId");
        SerializedProperty bodyProfileProperty = laserBeamPayloadProperty.FindPropertyRelative("bodyProfile");
        SerializedProperty sourceShapeProperty = laserBeamPayloadProperty.FindPropertyRelative("sourceShape");
        SerializedProperty terminalCapShapeProperty = laserBeamPayloadProperty.FindPropertyRelative("terminalCapShape");
        SerializedProperty bodyWidthMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("bodyWidthMultiplier");
        SerializedProperty collisionWidthMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("collisionWidthMultiplier");
        SerializedProperty sourceScaleMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("sourceScaleMultiplier");
        SerializedProperty terminalCapScaleMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("terminalCapScaleMultiplier");
        SerializedProperty contactFlareScaleMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("contactFlareScaleMultiplier");
        SerializedProperty bodyOpacityProperty = laserBeamPayloadProperty.FindPropertyRelative("bodyOpacity");
        SerializedProperty coreWidthMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("coreWidthMultiplier");
        SerializedProperty coreBrightnessProperty = laserBeamPayloadProperty.FindPropertyRelative("coreBrightness");
        SerializedProperty rimBrightnessProperty = laserBeamPayloadProperty.FindPropertyRelative("rimBrightness");
        SerializedProperty flowScrollSpeedProperty = laserBeamPayloadProperty.FindPropertyRelative("flowScrollSpeed");
        SerializedProperty flowPulseFrequencyProperty = laserBeamPayloadProperty.FindPropertyRelative("flowPulseFrequency");
        SerializedProperty stormTwistSpeedProperty = laserBeamPayloadProperty.FindPropertyRelative("stormTwistSpeed");
        SerializedProperty stormTickPostTravelHoldSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("stormTickPostTravelHoldSeconds");
        SerializedProperty stormIdleIntensityProperty = laserBeamPayloadProperty.FindPropertyRelative("stormIdleIntensity");
        SerializedProperty stormBurstIntensityProperty = laserBeamPayloadProperty.FindPropertyRelative("stormBurstIntensity");
        SerializedProperty sourceOffsetProperty = laserBeamPayloadProperty.FindPropertyRelative("sourceOffset");
        SerializedProperty sourceDischargeIntensityProperty = laserBeamPayloadProperty.FindPropertyRelative("sourceDischargeIntensity");
        SerializedProperty stormShellWidthMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("stormShellWidthMultiplier");
        SerializedProperty stormShellSeparationProperty = laserBeamPayloadProperty.FindPropertyRelative("stormShellSeparation");
        SerializedProperty stormRingFrequencyProperty = laserBeamPayloadProperty.FindPropertyRelative("stormRingFrequency");
        SerializedProperty stormRingThicknessProperty = laserBeamPayloadProperty.FindPropertyRelative("stormRingThickness");
        SerializedProperty stormTickTravelSpeedProperty = laserBeamPayloadProperty.FindPropertyRelative("stormTickTravelSpeed");
        SerializedProperty stormTickDamageLengthToleranceProperty = laserBeamPayloadProperty.FindPropertyRelative("stormTickDamageLengthTolerance");
        SerializedProperty terminalCapIntensityProperty = laserBeamPayloadProperty.FindPropertyRelative("terminalCapIntensity");
        SerializedProperty contactFlareIntensityProperty = laserBeamPayloadProperty.FindPropertyRelative("contactFlareIntensity");
        SerializedProperty wobbleAmplitudeProperty = laserBeamPayloadProperty.FindPropertyRelative("wobbleAmplitude");
        SerializedProperty bubbleDriftSpeedProperty = laserBeamPayloadProperty.FindPropertyRelative("bubbleDriftSpeed");

        if (damageMultiplierProperty == null ||
            continuousDamagePerSecondMultiplierProperty == null ||
            virtualProjectileSpeedMultiplierProperty == null ||
            damageTickIntervalSecondsProperty == null ||
            maximumContinuousActiveSecondsProperty == null ||
            cooldownSecondsProperty == null ||
            maximumBounceSegmentsProperty == null ||
            applyPlayerHandlingNerfWhileFiringProperty == null ||
            firingMoveSpeedMultiplierProperty == null ||
            firingRotationSpeedMultiplierProperty == null ||
            visualPresetIdProperty == null ||
            bodyProfileProperty == null ||
            sourceShapeProperty == null ||
            terminalCapShapeProperty == null ||
            bodyWidthMultiplierProperty == null ||
            collisionWidthMultiplierProperty == null ||
            sourceScaleMultiplierProperty == null ||
            terminalCapScaleMultiplierProperty == null ||
            contactFlareScaleMultiplierProperty == null ||
            bodyOpacityProperty == null ||
            coreWidthMultiplierProperty == null ||
            coreBrightnessProperty == null ||
            rimBrightnessProperty == null ||
            flowScrollSpeedProperty == null ||
            flowPulseFrequencyProperty == null ||
            stormTwistSpeedProperty == null ||
            stormTickPostTravelHoldSecondsProperty == null ||
            stormIdleIntensityProperty == null ||
            stormBurstIntensityProperty == null ||
            sourceOffsetProperty == null ||
            sourceDischargeIntensityProperty == null ||
            stormShellWidthMultiplierProperty == null ||
            stormShellSeparationProperty == null ||
            stormRingFrequencyProperty == null ||
            stormRingThicknessProperty == null ||
            stormTickTravelSpeedProperty == null ||
            stormTickDamageLengthToleranceProperty == null ||
            terminalCapIntensityProperty == null ||
            contactFlareIntensityProperty == null ||
            wobbleAmplitudeProperty == null ||
            bubbleDriftSpeedProperty == null)
        {
            HelpBox errorBox = new HelpBox("Laser Beam payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        string resolvedInfoText = string.IsNullOrWhiteSpace(infoText)
            ? "Laser Beam overrides base projectile spawning while the Shoot input is held. It always behaves as hold-to-fire, even if the current controller shooting trigger mode uses single-shot or toggle semantics."
            : infoText;
        HelpBox infoBox = new HelpBox(resolvedInfoText, HelpBoxMessageType.Info);
        payloadContainer.Add(infoBox);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);

        Foldout gameplayFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                            "Gameplay",
                                                                                            "LaserBeamPayloadGameplay",
                                                                                            true);
        payloadContainer.Add(gameplayFoldout);

        Foldout gameplayDamageFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                                   "Damage",
                                                                                                   "LaserBeamPayloadGameplayDamage",
                                                                                                   true);
        gameplayFoldout.Add(gameplayDamageFoldout);
        AddField(gameplayDamageFoldout, continuousDamagePerSecondMultiplierProperty, "Continuous Damage Per Second Multiplier");
        AddField(gameplayDamageFoldout, damageMultiplierProperty, "Tick Damage Multiplier");
        AddField(gameplayDamageFoldout, damageTickIntervalSecondsProperty, "Tick Interval Seconds");

        Foldout gameplayBehaviourFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                                      "Behaviour",
                                                                                                      "LaserBeamPayloadGameplayBehaviour",
                                                                                                      true);
        gameplayFoldout.Add(gameplayBehaviourFoldout);
        AddField(gameplayBehaviourFoldout, virtualProjectileSpeedMultiplierProperty, "Virtual Projectile Speed Multiplier");
        AddField(gameplayBehaviourFoldout, collisionWidthMultiplierProperty, "Collision Width Multiplier");
        AddField(gameplayBehaviourFoldout, maximumBounceSegmentsProperty, "Maximum Bounce Segments");
        AddField(gameplayBehaviourFoldout, cooldownSecondsProperty, "Cooldown Seconds");

        VisualElement cooldownContainer = new VisualElement();
        cooldownContainer.style.marginLeft = 12f;
        gameplayBehaviourFoldout.Add(cooldownContainer);
        AddField(cooldownContainer, maximumContinuousActiveSecondsProperty, "Maximum Continuous Active Seconds");

        Foldout playerHandlingFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                                   "Player Handling",
                                                                                                   "LaserBeamPayloadGameplayPlayerHandling",
                                                                                                   false);
        gameplayFoldout.Add(playerHandlingFoldout);
        AddField(playerHandlingFoldout, applyPlayerHandlingNerfWhileFiringProperty, "Apply Handling Nerf While Firing");

        VisualElement playerHandlingValuesContainer = new VisualElement();
        playerHandlingValuesContainer.style.marginLeft = 12f;
        playerHandlingFoldout.Add(playerHandlingValuesContainer);
        AddField(playerHandlingValuesContainer, firingMoveSpeedMultiplierProperty, "Firing Move Speed Multiplier");
        AddField(playerHandlingValuesContainer, firingRotationSpeedMultiplierProperty, "Firing Rotation Speed Multiplier");

        Foldout visualsFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                           "Presentation",
                                                                                           "LaserBeamPayloadPresentation",
                                                                                           true);
        payloadContainer.Add(visualsFoldout);

        Foldout bodyFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                        "Body",
                                                                                        "LaserBeamPayloadPresentationBody",
                                                                                        true);
        visualsFoldout.Add(bodyFoldout);
        Foldout bodyShapeFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                             "Shape",
                                                                                             "LaserBeamPayloadPresentationBodyShape",
                                                                                             true);
        bodyFoldout.Add(bodyShapeFoldout);
        AddField(bodyShapeFoldout, visualPresetIdProperty, "Visual Preset");
        AddField(bodyShapeFoldout, bodyWidthMultiplierProperty, "Body Width Multiplier");
        AddField(bodyShapeFoldout, bodyOpacityProperty, "Body Opacity");
        AddField(bodyShapeFoldout, coreWidthMultiplierProperty, "Core Width Multiplier");
        AddField(bodyShapeFoldout, coreBrightnessProperty, "Core Brightness");
        AddField(bodyShapeFoldout, rimBrightnessProperty, "Rim Brightness");

        Foldout bodyMotionFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                              "Motion",
                                                                                              "LaserBeamPayloadPresentationBodyMotion",
                                                                                              true);
        bodyFoldout.Add(bodyMotionFoldout);
        AddField(bodyMotionFoldout, flowScrollSpeedProperty, "Body Flow Speed");
        AddField(bodyMotionFoldout, flowPulseFrequencyProperty, "Flow Shimmer Frequency");
        AddField(bodyMotionFoldout, wobbleAmplitudeProperty, "Body Breathing Amplitude");

        Foldout sourceFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                          "Source",
                                                                                          "LaserBeamPayloadPresentationSource",
                                                                                          true);
        visualsFoldout.Add(sourceFoldout);
        AddField(sourceFoldout, sourceScaleMultiplierProperty, "Source Scale Multiplier");
        AddField(sourceFoldout, sourceOffsetProperty, "Source Offset");
        AddField(sourceFoldout, sourceDischargeIntensityProperty, "Source Discharge Intensity");

        Foldout stormFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                         "Storm",
                                                                                         "LaserBeamPayloadPresentationStorm",
                                                                                         true);
        visualsFoldout.Add(stormFoldout);
        Foldout stormShellFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                              "Shell",
                                                                                              "LaserBeamPayloadPresentationStormShell",
                                                                                              true);
        stormFoldout.Add(stormShellFoldout);
        AddField(stormShellFoldout, stormIdleIntensityProperty, "Storm Idle Intensity");
        AddField(stormShellFoldout, stormBurstIntensityProperty, "Storm Burst Intensity");
        AddField(stormShellFoldout, stormTwistSpeedProperty, "Storm Twist Speed");
        AddField(stormShellFoldout, stormShellWidthMultiplierProperty, "Storm Shell Width Multiplier");
        AddField(stormShellFoldout, stormShellSeparationProperty, "Storm Shell Separation");
        AddField(stormShellFoldout, stormRingFrequencyProperty, "Storm Ring Frequency");
        AddField(stormShellFoldout, stormRingThicknessProperty, "Storm Ring Thickness");

        Foldout stormPacketFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                               "Tick Packet",
                                                                                               "LaserBeamPayloadPresentationStormPacket",
                                                                                               true);
        stormFoldout.Add(stormPacketFoldout);
        AddField(stormPacketFoldout, stormTickTravelSpeedProperty, "Storm Tick Travel Speed");
        AddField(stormPacketFoldout, stormTickPostTravelHoldSecondsProperty, "Storm Tick Post Travel Hold Seconds");
        AddField(stormPacketFoldout, stormTickDamageLengthToleranceProperty, "Storm Tick Damage Length Tolerance");

        Foldout terminalFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                            "Terminal",
                                                                                            "LaserBeamPayloadPresentationTerminal",
                                                                                            true);
        visualsFoldout.Add(terminalFoldout);
        AddField(terminalFoldout, terminalCapScaleMultiplierProperty, "Terminal Cap Scale Multiplier");
        AddField(terminalFoldout, terminalCapIntensityProperty, "Terminal Cap Intensity");
        AddField(terminalFoldout, contactFlareScaleMultiplierProperty, "Contact Flare Scale Multiplier");
        AddField(terminalFoldout, contactFlareIntensityProperty, "Contact Flare Intensity");

        Foldout advancedVisualsFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                                    "Advanced Overrides",
                                                                                                    "LaserBeamPayloadPresentationAdvanced",
                                                                                                    false);
        visualsFoldout.Add(advancedVisualsFoldout);
        AddField(advancedVisualsFoldout, bodyProfileProperty, "Body Profile Override");
        AddField(advancedVisualsFoldout, sourceShapeProperty, "Source Shape Override");
        AddField(advancedVisualsFoldout, terminalCapShapeProperty, "Terminal Cap Shape Override");
        AddField(advancedVisualsFoldout, bubbleDriftSpeedProperty, "Secondary Drift Noise Speed");

        void RefreshWarnings()
        {
            RefreshLaserBeamWarnings(continuousDamagePerSecondMultiplierProperty,
                                     damageMultiplierProperty,
                                     virtualProjectileSpeedMultiplierProperty,
                                     cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     maximumBounceSegmentsProperty,
                                     applyPlayerHandlingNerfWhileFiringProperty,
                                     firingMoveSpeedMultiplierProperty,
                                     firingRotationSpeedMultiplierProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     sourceOffsetProperty,
                                     sourceDischargeIntensityProperty,
                                     terminalCapScaleMultiplierProperty,
                                     contactFlareScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     coreWidthMultiplierProperty,
                                     stormTwistSpeedProperty,
                                     stormTickPostTravelHoldSecondsProperty,
                                     stormIdleIntensityProperty,
                                     stormBurstIntensityProperty,
                                     stormShellWidthMultiplierProperty,
                                     stormShellSeparationProperty,
                                     stormRingFrequencyProperty,
                                     stormRingThicknessProperty,
                                     stormTickTravelSpeedProperty,
                                     stormTickDamageLengthToleranceProperty,
                                     terminalCapIntensityProperty,
                                     contactFlareIntensityProperty,
                                     warningBox);
        }

        UpdateLaserBeamCooldownVisibility(cooldownSecondsProperty, cooldownContainer);
        UpdateBooleanContainerVisibility(applyPlayerHandlingNerfWhileFiringProperty, playerHandlingValuesContainer);
        RefreshWarnings();

        payloadContainer.TrackPropertyValue(cooldownSecondsProperty, changedProperty =>
        {
            UpdateLaserBeamCooldownVisibility(changedProperty, cooldownContainer);
            RefreshWarnings();
        });
        payloadContainer.TrackPropertyValue(applyPlayerHandlingNerfWhileFiringProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, playerHandlingValuesContainer);
            RefreshWarnings();
        });
        RegisterLaserBeamWarningRefresh(payloadContainer,
                                        RefreshWarnings,
                                        continuousDamagePerSecondMultiplierProperty,
                                        damageMultiplierProperty,
                                        virtualProjectileSpeedMultiplierProperty,
                                        damageTickIntervalSecondsProperty,
                                        maximumContinuousActiveSecondsProperty,
                                        maximumBounceSegmentsProperty,
                                        firingMoveSpeedMultiplierProperty,
                                        firingRotationSpeedMultiplierProperty,
                                        bodyWidthMultiplierProperty,
                                        collisionWidthMultiplierProperty,
                                        sourceScaleMultiplierProperty,
                                        sourceOffsetProperty,
                                        sourceDischargeIntensityProperty,
                                        terminalCapScaleMultiplierProperty,
                                        contactFlareScaleMultiplierProperty,
                                        bodyOpacityProperty,
                                        coreWidthMultiplierProperty,
                                        stormTwistSpeedProperty,
                                        stormTickPostTravelHoldSecondsProperty,
                                        stormIdleIntensityProperty,
                                        stormBurstIntensityProperty,
                                        stormShellWidthMultiplierProperty,
                                        stormShellSeparationProperty,
                                        stormRingFrequencyProperty,
                                        stormRingThicknessProperty,
                                        stormTickTravelSpeedProperty,
                                        stormTickDamageLengthToleranceProperty,
                                        terminalCapIntensityProperty,
                                        contactFlareIntensityProperty);
    }
    #endregion

    #region Visibility
    private static void UpdateBooleanContainerVisibility(SerializedProperty toggleProperty, VisualElement container)
    {
        if (container == null)
            return;

        if (toggleProperty == null)
        {
            container.style.display = DisplayStyle.None;
            return;
        }

        container.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void UpdateHealOverTimeContainerVisibility(SerializedProperty applyModeProperty, VisualElement overTimeContainer)
    {
        if (overTimeContainer == null)
            return;

        if (applyModeProperty == null)
        {
            overTimeContainer.style.display = DisplayStyle.None;
            return;
        }

        PowerUpHealApplicationMode applyMode = (PowerUpHealApplicationMode)applyModeProperty.enumValueIndex;
        overTimeContainer.style.display = applyMode == PowerUpHealApplicationMode.OverTime ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void UpdateInterruptOptionsVisibility(SerializedProperty interruptOtherSlotOnEnterProperty, VisualElement interruptOptionsContainer)
    {
        if (interruptOptionsContainer == null)
            return;

        if (interruptOtherSlotOnEnterProperty == null)
        {
            interruptOptionsContainer.style.display = DisplayStyle.None;
            return;
        }

        interruptOptionsContainer.style.display = interruptOtherSlotOnEnterProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void UpdateLaserBeamCooldownVisibility(SerializedProperty cooldownSecondsProperty, VisualElement cooldownContainer)
    {
        if (cooldownContainer == null)
            return;

        if (cooldownSecondsProperty == null)
        {
            cooldownContainer.style.display = DisplayStyle.None;
            return;
        }

        cooldownContainer.style.display = cooldownSecondsProperty.floatValue > 0f ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void UpdateElementalPayloadOptionsVisibility(SerializedProperty applyElementalOnHitProperty, VisualElement elementalPayloadContainer)
    {
        if (elementalPayloadContainer == null)
            return;

        if (applyElementalOnHitProperty == null)
        {
            elementalPayloadContainer.style.display = DisplayStyle.None;
            return;
        }

        elementalPayloadContainer.style.display = applyElementalOnHitProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Creates a compact nested payload foldout used to group related settings inside specialized module drawers.
    /// </summary>
    /// <param name="title">Foldout title shown in the tool.</param>
    /// <param name="expanded">Initial expanded state.</param>
    /// <returns>Configured foldout ready to receive fields.</returns>
    private static Foldout CreatePayloadFoldout(string title, bool expanded)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        foldout.value = expanded;
        foldout.style.marginLeft = 8f;
        return foldout;
    }

    /// <summary>
    /// Registers one serialized-property refresh callback on a scaling-aware field.
    /// </summary>
    /// <param name="field">Field that emits SerializedPropertyChangeEvent.</param>
    /// <param name="refreshAction">Action used to update dependent visibility and warnings.</param>
    private static void RegisterRefreshCallback(VisualElement field, Action refreshAction)
    {
        if (field == null || refreshAction == null)
            return;

        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            refreshAction();
        });
    }

    /// <summary>
    /// Tracks one serialized property and invokes a warning refresh callback when it changes.
    /// </summary>
    /// <param name="root">Root visual element that owns the binding.</param>
    /// <param name="property">Property to track.</param>
    /// <param name="callback">Callback invoked after the property changes.</param>
    private static void TrackWarningProperty(VisualElement root,
                                             SerializedProperty property,
                                             Action<SerializedProperty> callback)
    {
        if (root == null || property == null || callback == null)
            return;

        root.TrackPropertyValue(property, callback);
    }
    #endregion

    #region Warnings
    /// <summary>
    /// Refreshes validation warnings for AreaTickApplyElement payload fields without mutating serialized values.
    /// </summary>
    /// <param name="stacksPerTickProperty">Serialized Stacks Per Tick field.</param>
    /// <param name="applyIntervalSecondsProperty">Serialized Apply Interval Seconds field.</param>
    /// <param name="elementTypeProperty">Serialized Element Type enum.</param>
    /// <param name="effectKindProperty">Serialized Effect Kind enum.</param>
    /// <param name="procModeProperty">Serialized Proc Mode enum.</param>
    /// <param name="procThresholdStacksProperty">Serialized Proc Threshold Stacks field.</param>
    /// <param name="maximumStacksProperty">Serialized Maximum Stacks field.</param>
    /// <param name="stackDecayPerSecondProperty">Serialized Stack Decay Per Second field.</param>
    /// <param name="dotDamagePerTickProperty">Serialized Dot Damage Per Tick field.</param>
    /// <param name="dotTickIntervalProperty">Serialized Dot Tick Interval field.</param>
    /// <param name="dotDurationSecondsProperty">Serialized Dot Duration Seconds field.</param>
    /// <param name="impedimentSlowPercentPerStackProperty">Serialized progressive slow field.</param>
    /// <param name="impedimentProcSlowPercentProperty">Serialized proc slow field.</param>
    /// <param name="impedimentMaxSlowPercentProperty">Serialized max slow field.</param>
    /// <param name="impedimentDurationSecondsProperty">Serialized impediment duration field.</param>
    /// <param name="warningBox">HelpBox receiving the current warning text.</param>
    private static void RefreshAreaTickApplyElementWarnings(SerializedProperty stacksPerTickProperty,
                                                            SerializedProperty applyIntervalSecondsProperty,
                                                            SerializedProperty elementTypeProperty,
                                                            SerializedProperty effectKindProperty,
                                                            SerializedProperty procModeProperty,
                                                            SerializedProperty procThresholdStacksProperty,
                                                            SerializedProperty maximumStacksProperty,
                                                            SerializedProperty stackDecayPerSecondProperty,
                                                            SerializedProperty dotDamagePerTickProperty,
                                                            SerializedProperty dotTickIntervalProperty,
                                                            SerializedProperty dotDurationSecondsProperty,
                                                            SerializedProperty impedimentSlowPercentPerStackProperty,
                                                            SerializedProperty impedimentProcSlowPercentProperty,
                                                            SerializedProperty impedimentMaxSlowPercentProperty,
                                                            SerializedProperty impedimentDurationSecondsProperty,
                                                            HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();
        ElementalEffectKind effectKind = effectKindProperty != null
            ? (ElementalEffectKind)effectKindProperty.enumValueIndex
            : ElementalEffectKind.Dots;
        ElementalProcMode procMode = procModeProperty != null
            ? (ElementalProcMode)procModeProperty.enumValueIndex
            : ElementalProcMode.ThresholdOnly;

        if (stacksPerTickProperty != null && stacksPerTickProperty.floatValue < 0f)
            warningLines.Add("Stacks Per Tick should be >= 0.");

        if (applyIntervalSecondsProperty != null && applyIntervalSecondsProperty.floatValue < 0.01f)
            warningLines.Add("Apply Interval Seconds should be >= 0.01.");

        if (elementTypeProperty != null && (ElementType)elementTypeProperty.enumValueIndex == ElementType.Custom)
            warningLines.Add("Custom Element Type is legacy-only here; prefer Fire, Ice or Poison for AreaTickApplyElement.");

        if (procThresholdStacksProperty != null && procThresholdStacksProperty.floatValue < 0.1f)
            warningLines.Add("Proc Threshold Stacks should be >= 0.1.");

        if (maximumStacksProperty != null && maximumStacksProperty.floatValue < 0.1f)
            warningLines.Add("Maximum Stacks should be >= 0.1.");

        if (procThresholdStacksProperty != null &&
            maximumStacksProperty != null &&
            maximumStacksProperty.floatValue > 0f &&
            procThresholdStacksProperty.floatValue > maximumStacksProperty.floatValue)
        {
            warningLines.Add("Maximum Stacks is lower than Proc Threshold Stacks, so runtime scaling will clamp the maximum.");
        }

        if (stackDecayPerSecondProperty != null && stackDecayPerSecondProperty.floatValue < 0f)
            warningLines.Add("Stack Decay Per Second should be >= 0.");

        if (effectKind == ElementalEffectKind.Dots)
        {
            if (dotDamagePerTickProperty != null && dotDamagePerTickProperty.floatValue < 0f)
                warningLines.Add("Dot Damage Per Tick should be >= 0.");

            if (dotTickIntervalProperty != null && dotTickIntervalProperty.floatValue < 0.01f)
                warningLines.Add("Dot Tick Interval should be >= 0.01.");

            if (dotDurationSecondsProperty != null && dotDurationSecondsProperty.floatValue < 0.05f)
                warningLines.Add("Dot Duration Seconds should be >= 0.05.");
        }

        if (effectKind == ElementalEffectKind.Impediment)
        {
            if (procMode == ElementalProcMode.ProgressiveUntilThreshold &&
                impedimentSlowPercentPerStackProperty != null &&
                (impedimentSlowPercentPerStackProperty.floatValue < 0f || impedimentSlowPercentPerStackProperty.floatValue > 100f))
            {
                warningLines.Add("Slow Percent Per Stack should stay within 0-100.");
            }

            if (impedimentProcSlowPercentProperty != null &&
                (impedimentProcSlowPercentProperty.floatValue < 0f || impedimentProcSlowPercentProperty.floatValue > 100f))
            {
                warningLines.Add("Proc Slow Percent should stay within 0-100.");
            }

            if (impedimentMaxSlowPercentProperty != null &&
                (impedimentMaxSlowPercentProperty.floatValue < 0f || impedimentMaxSlowPercentProperty.floatValue > 100f))
            {
                warningLines.Add("Max Slow Percent should stay within 0-100.");
            }

            if (impedimentDurationSecondsProperty != null && impedimentDurationSecondsProperty.floatValue < 0.05f)
                warningLines.Add("Impediment Duration Seconds should be >= 0.05.");
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Refreshes validation warnings for hold-charge payload fields without mutating serialized values.
    /// </summary>
    /// <param name="requiredChargeProperty">Serialized Required Charge field.</param>
    /// <param name="maximumChargeProperty">Serialized Maximum Charge field.</param>
    /// <param name="chargeRatePerSecondProperty">Serialized Charge Rate Per Second field.</param>
    /// <param name="decayAfterReleaseProperty">Serialized Decay After Release toggle.</param>
    /// <param name="decayAfterReleasePercentPerSecondProperty">Serialized released-state decay percentage field.</param>
    /// <param name="passiveChargeGainWhileReleasedProperty">Serialized Passive Gain While Released toggle.</param>
    /// <param name="passiveChargeGainPercentPerSecondProperty">Serialized released-state passive gain percentage field.</param>
    /// <param name="laserDurationSecondsProperty">Serialized Laser Duration Seconds field.</param>
    /// <param name="useChargedLaserBeamProperty">Serialized standalone charged Laser Beam toggle.</param>
    /// <param name="chargedLaserDurationSecondsProperty">Serialized standalone charged Laser Beam duration field.</param>
    /// <param name="slowPlayerWhileChargingProperty">Serialized movement slow toggle.</param>
    /// <param name="maximumPlayerSlowPercentProperty">Serialized maximum movement slow percentage.</param>
    /// <param name="playerSlowCurveProperty">Serialized normalized movement slow curve.</param>
    /// <param name="warningBox">HelpBox receiving the current warning text.</param>
    private static void RefreshHoldChargeWarnings(SerializedProperty requiredChargeProperty,
                                                  SerializedProperty maximumChargeProperty,
                                                  SerializedProperty chargeRatePerSecondProperty,
                                                  SerializedProperty decayAfterReleaseProperty,
                                                  SerializedProperty decayAfterReleasePercentPerSecondProperty,
                                                  SerializedProperty passiveChargeGainWhileReleasedProperty,
                                                  SerializedProperty passiveChargeGainPercentPerSecondProperty,
                                                  SerializedProperty laserDurationSecondsProperty,
                                                  SerializedProperty useChargedLaserBeamProperty,
                                                  SerializedProperty chargedLaserDurationSecondsProperty,
                                                  SerializedProperty slowPlayerWhileChargingProperty,
                                                  SerializedProperty maximumPlayerSlowPercentProperty,
                                                  SerializedProperty playerSlowCurveProperty,
                                                  HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();

        if (requiredChargeProperty != null && requiredChargeProperty.floatValue < 0f)
            warningLines.Add("Required Charge should be >= 0.");

        if (maximumChargeProperty != null && requiredChargeProperty != null && maximumChargeProperty.floatValue < requiredChargeProperty.floatValue)
            warningLines.Add("Maximum Charge should be >= Required Charge.");

        if (chargeRatePerSecondProperty != null && chargeRatePerSecondProperty.floatValue <= 0f)
            warningLines.Add("Charge Rate Per Second should be > 0 for a usable hold charge.");

        if (laserDurationSecondsProperty != null && laserDurationSecondsProperty.floatValue < 0f)
            warningLines.Add("Laser Duration Seconds should be >= 0.");

        if (useChargedLaserBeamProperty != null &&
            useChargedLaserBeamProperty.boolValue &&
            chargedLaserDurationSecondsProperty != null &&
            chargedLaserDurationSecondsProperty.floatValue <= 0f)
        {
            warningLines.Add("Charged Laser Duration Seconds should be > 0 when Use Charged Laser Beam is enabled.");
        }

        if (decayAfterReleaseProperty != null &&
            decayAfterReleaseProperty.boolValue &&
            decayAfterReleasePercentPerSecondProperty != null &&
            decayAfterReleasePercentPerSecondProperty.floatValue < 0f)
        {
            warningLines.Add("Decay Percent Per Second should be >= 0 when Decay After Release is enabled.");
        }

        if (passiveChargeGainWhileReleasedProperty != null &&
            passiveChargeGainWhileReleasedProperty.boolValue &&
            passiveChargeGainPercentPerSecondProperty != null &&
            passiveChargeGainPercentPerSecondProperty.floatValue < 0f)
        {
            warningLines.Add("Passive Gain Percent Per Second should be >= 0 when Passive Gain While Released is enabled.");
        }

        if (slowPlayerWhileChargingProperty != null && slowPlayerWhileChargingProperty.boolValue)
            AppendHoldChargeSlowWarnings(maximumPlayerSlowPercentProperty,
                                         playerSlowCurveProperty,
                                         warningLines);

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Appends warnings specific to the progressive movement slow subsection of a hold-charge payload.
    /// </summary>
    /// <param name="maximumPlayerSlowPercentProperty">Serialized maximum movement slow percentage.</param>
    /// <param name="playerSlowCurveProperty">Serialized normalized movement slow curve.</param>
    /// <param name="warningLines">Mutable warning list receiving any detected issues.</param>
    private static void AppendHoldChargeSlowWarnings(SerializedProperty maximumPlayerSlowPercentProperty,
                                                     SerializedProperty playerSlowCurveProperty,
                                                     List<string> warningLines)
    {
        if (warningLines == null)
            return;

        if (maximumPlayerSlowPercentProperty != null && maximumPlayerSlowPercentProperty.floatValue <= 0f)
            warningLines.Add("Maximum Player Slow Percent should be > 0 when Slow Player While Charging is enabled.");
        else if (maximumPlayerSlowPercentProperty != null && maximumPlayerSlowPercentProperty.floatValue > 100f)
            warningLines.Add("Maximum Player Slow Percent above 100 is clamped at runtime.");

        AppendNormalizedSlowCurveWarnings(playerSlowCurveProperty, warningLines);
    }

    /// <summary>
    /// Validates the normalized movement slow curve without mutating its keys.
    /// </summary>
    /// <param name="curveProperty">Serialized AnimationCurve field to inspect.</param>
    /// <param name="warningLines">Mutable warning list receiving any detected issues.</param>
    private static void AppendNormalizedSlowCurveWarnings(SerializedProperty curveProperty,
                                                         List<string> warningLines)
    {
        if (curveProperty == null)
            return;

        AnimationCurve curve = curveProperty.animationCurveValue;

        if (curve == null || curve.length <= 0)
        {
            warningLines.Add("Player Slow Curve should contain at least one key.");
            return;
        }

        bool hasPositiveValue = false;

        for (int keyIndex = 0; keyIndex < curve.length; keyIndex++)
        {
            Keyframe key = curve.keys[keyIndex];

            if (key.time < 0f || key.time > 1f)
                warningLines.Add(string.Format("Player Slow Curve key #{0} time should stay in the normalized 0-1 range.", keyIndex + 1));

            if (key.value < 0f || key.value > 1f)
                warningLines.Add(string.Format("Player Slow Curve key #{0} value should stay in the normalized 0-1 range.", keyIndex + 1));

            if (key.value > 0f)
                hasPositiveValue = true;
        }

        if (!hasPositiveValue)
            warningLines.Add("Player Slow Curve is fully zeroed, so the enabled charge slow has no runtime effect.");
    }

    /// <summary>
    /// Registers serialized-property watchers that refresh hold-charge warnings after field edits.
    /// </summary>
    /// <param name="payloadContainer">Root payload element used to observe serialized-property changes.</param>
    /// <param name="refreshWarnings">Callback that recomputes current warning text.</param>
    /// <param name="watchedProperties">Serialized fields that should trigger warning refreshes.</param>
    private static void RegisterHoldChargeWarningRefresh(VisualElement payloadContainer,
                                                         Action refreshWarnings,
                                                         params SerializedProperty[] watchedProperties)
    {
        if (payloadContainer == null || refreshWarnings == null || watchedProperties == null)
            return;

        for (int propertyIndex = 0; propertyIndex < watchedProperties.Length; propertyIndex++)
        {
            SerializedProperty watchedProperty = watchedProperties[propertyIndex];

            if (watchedProperty == null)
                continue;

            payloadContainer.TrackPropertyValue(watchedProperty, changedProperty =>
            {
                refreshWarnings();
            });
        }
    }

    private static void RefreshCharacterTuningWarnings(SerializedObject serializedObject,
                                                       SerializedProperty formulasProperty,
                                                       HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        if (serializedObject == null || formulasProperty == null || !formulasProperty.isArray)
        {
            warningBox.text = "Character Tuning formulas are not available.";
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        List<string> warningLines = new List<string>();
        HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject);
        Dictionary<string, PlayerFormulaValueType> variableTypes = PlayerScalingFormulaValidationUtility.BuildScopedVariableTypeMap(serializedObject);

        if (formulasProperty.arraySize <= 0)
            warningLines.Add("No acquisition formula configured. Character Tuning currently has no effect.");

        for (int formulaIndex = 0; formulaIndex < formulasProperty.arraySize; formulaIndex++)
        {
            SerializedProperty formulaEntryProperty = formulasProperty.GetArrayElementAtIndex(formulaIndex);

            if (formulaEntryProperty == null)
            {
                warningLines.Add(string.Format("Formula #{0} is missing.", formulaIndex + 1));
                continue;
            }

            SerializedProperty formulaProperty = formulaEntryProperty.FindPropertyRelative("formula");

            if (formulaProperty == null || formulaProperty.propertyType != SerializedPropertyType.String)
            {
                warningLines.Add(string.Format("Formula #{0} payload is invalid.", formulaIndex + 1));
                continue;
            }

            string formulaValue = formulaProperty.stringValue;

            if (string.IsNullOrWhiteSpace(formulaValue))
            {
                warningLines.Add(string.Format("Formula #{0} is empty.", formulaIndex + 1));
                continue;
            }

            if (PlayerCharacterTuningFormulaValidationUtility.TryValidateAssignmentFormula(formulaValue,
                                                                                          allowedVariables,
                                                                                          variableTypes,
                                                                                          out string warningMessage))
            {
                continue;
            }

            warningLines.Add(string.Format("Formula #{0}: {1}", formulaIndex + 1, warningMessage));
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    private static void RefreshLaserBeamWarnings(SerializedProperty continuousDamagePerSecondMultiplierProperty,
                                                 SerializedProperty damageMultiplierProperty,
                                                 SerializedProperty virtualProjectileSpeedMultiplierProperty,
                                                 SerializedProperty cooldownSecondsProperty,
                                                 SerializedProperty damageTickIntervalSecondsProperty,
                                                 SerializedProperty maximumContinuousActiveSecondsProperty,
                                                 SerializedProperty maximumBounceSegmentsProperty,
                                                 SerializedProperty applyPlayerHandlingNerfWhileFiringProperty,
                                                 SerializedProperty firingMoveSpeedMultiplierProperty,
                                                 SerializedProperty firingRotationSpeedMultiplierProperty,
                                                 SerializedProperty bodyWidthMultiplierProperty,
                                                 SerializedProperty collisionWidthMultiplierProperty,
                                                 SerializedProperty sourceScaleMultiplierProperty,
                                                 SerializedProperty sourceOffsetProperty,
                                                 SerializedProperty sourceDischargeIntensityProperty,
                                                 SerializedProperty terminalCapScaleMultiplierProperty,
                                                 SerializedProperty contactFlareScaleMultiplierProperty,
                                                 SerializedProperty bodyOpacityProperty,
                                                 SerializedProperty coreWidthMultiplierProperty,
                                                 SerializedProperty stormTwistSpeedProperty,
                                                 SerializedProperty stormTickPostTravelHoldSecondsProperty,
                                                 SerializedProperty stormIdleIntensityProperty,
                                                 SerializedProperty stormBurstIntensityProperty,
                                                 SerializedProperty stormShellWidthMultiplierProperty,
                                                 SerializedProperty stormShellSeparationProperty,
                                                 SerializedProperty stormRingFrequencyProperty,
                                                 SerializedProperty stormRingThicknessProperty,
                                                 SerializedProperty stormTickTravelSpeedProperty,
                                                 SerializedProperty stormTickDamageLengthToleranceProperty,
                                                 SerializedProperty terminalCapIntensityProperty,
                                                 SerializedProperty contactFlareIntensityProperty,
                                                 HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();

        if (continuousDamagePerSecondMultiplierProperty != null && continuousDamagePerSecondMultiplierProperty.floatValue < 0f)
            warningLines.Add("Continuous Damage Per Second Multiplier should be >= 0.");

        if (damageMultiplierProperty != null && damageMultiplierProperty.floatValue < 0f)
            warningLines.Add("Tick Damage Multiplier should be >= 0.");

        if (virtualProjectileSpeedMultiplierProperty != null && virtualProjectileSpeedMultiplierProperty.floatValue < 0f)
            warningLines.Add("Virtual Projectile Speed Multiplier should be >= 0.");

        if (damageTickIntervalSecondsProperty != null && damageTickIntervalSecondsProperty.floatValue <= 0f)
            warningLines.Add("Damage Tick Interval Seconds should be > 0.");
        else if (damageTickIntervalSecondsProperty != null && damageTickIntervalSecondsProperty.floatValue < 0.03f)
            warningLines.Add("Damage Tick Interval Seconds below 0.03 can create very dense beam hit pulses and hurt runtime stability.");

        if (cooldownSecondsProperty != null && cooldownSecondsProperty.floatValue <= 0f)
        {
            if (maximumContinuousActiveSecondsProperty != null && maximumContinuousActiveSecondsProperty.floatValue > 0f)
                warningLines.Add("Maximum Continuous Active Seconds is ignored while Cooldown Seconds is 0.");
        }
        else if (maximumContinuousActiveSecondsProperty != null && maximumContinuousActiveSecondsProperty.floatValue <= 0f)
        {
            warningLines.Add("Maximum Continuous Active Seconds should be > 0 when Cooldown Seconds is enabled.");
        }

        if (bodyWidthMultiplierProperty != null && bodyWidthMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Body Width Multiplier should be > 0.");
        else if (bodyWidthMultiplierProperty != null && bodyWidthMultiplierProperty.floatValue > 32f)
            warningLines.Add("Body Width Multiplier is extremely high. Runtime beam safety may clamp oversized body geometry.");

        if (collisionWidthMultiplierProperty != null && collisionWidthMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Collision Width Multiplier should be > 0.");
        else if (collisionWidthMultiplierProperty != null && collisionWidthMultiplierProperty.floatValue > 24f)
            warningLines.Add("Collision Width Multiplier is extremely high. Runtime beam safety may clamp oversized collision radii.");

        if (maximumBounceSegmentsProperty != null && maximumBounceSegmentsProperty.intValue > PlayerLaserBeamUtility.MaximumSupportedBounceSegments)
            warningLines.Add(string.Format("Maximum Bounce Segments above {0} is clamped at runtime to keep beam lane rebuild stable.", PlayerLaserBeamUtility.MaximumSupportedBounceSegments));

        if (applyPlayerHandlingNerfWhileFiringProperty != null &&
            applyPlayerHandlingNerfWhileFiringProperty.boolValue)
        {
            if (firingMoveSpeedMultiplierProperty != null &&
                firingMoveSpeedMultiplierProperty.floatValue < 0f)
            {
                warningLines.Add("Firing Move Speed Multiplier should be >= 0.");
            }

            if (firingRotationSpeedMultiplierProperty != null &&
                firingRotationSpeedMultiplierProperty.floatValue < 0f)
            {
                warningLines.Add("Firing Rotation Speed Multiplier should be >= 0.");
            }

            if (firingMoveSpeedMultiplierProperty != null &&
                firingRotationSpeedMultiplierProperty != null &&
                Mathf.Approximately(firingMoveSpeedMultiplierProperty.floatValue, 1f) &&
                Mathf.Approximately(firingRotationSpeedMultiplierProperty.floatValue, 1f))
            {
                warningLines.Add("Apply Handling Nerf While Firing is enabled but both handling multipliers are 1, so it has no practical effect.");
            }
        }

        if (virtualProjectileSpeedMultiplierProperty != null && virtualProjectileSpeedMultiplierProperty.floatValue > 12f)
            warningLines.Add("Virtual Projectile Speed Multiplier is very high. Beam reach can hit the runtime travel safety cap.");

        if (sourceScaleMultiplierProperty != null && sourceScaleMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Source Scale Multiplier should be > 0.");
        else if (sourceScaleMultiplierProperty != null && sourceScaleMultiplierProperty.floatValue > 10f)
            warningLines.Add("Source Scale Multiplier is unusually high and can produce oversized endpoint visuals.");

        if (sourceOffsetProperty != null && sourceOffsetProperty.floatValue < 0f)
            warningLines.Add("Source Offset should be >= 0.");

        if (sourceDischargeIntensityProperty != null && sourceDischargeIntensityProperty.floatValue < 0f)
            warningLines.Add("Source Discharge Intensity should be >= 0.");

        if (terminalCapScaleMultiplierProperty != null && terminalCapScaleMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Terminal Cap Scale Multiplier should be > 0.");
        else if (terminalCapScaleMultiplierProperty != null && terminalCapScaleMultiplierProperty.floatValue > 10f)
            warningLines.Add("Terminal Cap Scale Multiplier is unusually high and can produce oversized endpoint visuals.");

        if (contactFlareScaleMultiplierProperty != null && contactFlareScaleMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Contact Flare Scale Multiplier should be > 0.");
        else if (contactFlareScaleMultiplierProperty != null && contactFlareScaleMultiplierProperty.floatValue > 12f)
            warningLines.Add("Contact Flare Scale Multiplier is unusually high and can produce oversized endpoint visuals.");

        if (bodyOpacityProperty != null && bodyOpacityProperty.floatValue <= 0f)
            warningLines.Add("Body Opacity should be > 0.");

        if (coreWidthMultiplierProperty != null && coreWidthMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Core Width Multiplier should be > 0.");

        if (stormTwistSpeedProperty != null && stormTwistSpeedProperty.floatValue < 0f)
            warningLines.Add("Storm Twist Speed should be >= 0.");

        if (stormTickPostTravelHoldSecondsProperty != null && stormTickPostTravelHoldSecondsProperty.floatValue < 0f)
            warningLines.Add("Storm Tick Post Travel Hold Seconds should be >= 0.");

        if (stormIdleIntensityProperty != null && stormIdleIntensityProperty.floatValue < 0f)
            warningLines.Add("Storm Idle Intensity should be >= 0.");

        if (stormBurstIntensityProperty != null && stormBurstIntensityProperty.floatValue < 0f)
            warningLines.Add("Storm Burst Intensity should be >= 0.");

        if (stormShellWidthMultiplierProperty != null && stormShellWidthMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Storm Shell Width Multiplier should be > 0.");

        if (stormShellSeparationProperty != null && stormShellSeparationProperty.floatValue < 0f)
            warningLines.Add("Storm Shell Separation should be >= 0.");

        if (stormRingFrequencyProperty != null && stormRingFrequencyProperty.floatValue <= 0f)
            warningLines.Add("Storm Ring Frequency should be > 0.");

        if (stormRingThicknessProperty != null && stormRingThicknessProperty.floatValue <= 0f)
            warningLines.Add("Storm Ring Thickness should be > 0.");

        if (stormTickTravelSpeedProperty != null && stormTickTravelSpeedProperty.floatValue < 0f)
            warningLines.Add("Storm Tick Travel Speed should be >= 0.");

        if (stormTickDamageLengthToleranceProperty != null && stormTickDamageLengthToleranceProperty.floatValue < 0f)
            warningLines.Add("Storm Tick Damage Length Tolerance should be >= 0.");

        if (terminalCapIntensityProperty != null && terminalCapIntensityProperty.floatValue < 0f)
            warningLines.Add("Terminal Cap Intensity should be >= 0.");

        if (contactFlareIntensityProperty != null && contactFlareIntensityProperty.floatValue < 0f)
            warningLines.Add("Contact Flare Intensity should be >= 0.");

        bool hasVisibleStorm = (stormIdleIntensityProperty != null && stormIdleIntensityProperty.floatValue > 0f) ||
                               (stormBurstIntensityProperty != null && stormBurstIntensityProperty.floatValue > 0f);

        if (!hasVisibleStorm)
            warningLines.Add("Both Storm Idle Intensity and Storm Burst Intensity are 0. The electrical storm feedback will not be visible.");

        bool hasAnyDamage = (continuousDamagePerSecondMultiplierProperty != null && continuousDamagePerSecondMultiplierProperty.floatValue > 0f) ||
                            (damageMultiplierProperty != null && damageMultiplierProperty.floatValue > 0f);

        if (!hasAnyDamage)
            warningLines.Add("Both Continuous Damage Per Second Multiplier and Tick Damage Multiplier are 0. The beam will not deal damage.");

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Registers one warning refresh callback for every provided Laser Beam payload property.
    /// </summary>
    /// <param name="payloadContainer">Container used to observe serialized-property edits.</param>
    /// <param name="refreshWarnings">Callback that recomputes the current warning text.</param>
    /// <param name="watchedProperties">Properties that should trigger a warning refresh when edited.</param>
    private static void RegisterLaserBeamWarningRefresh(VisualElement payloadContainer,
                                                        Action refreshWarnings,
                                                        params SerializedProperty[] watchedProperties)
    {
        if (payloadContainer == null || refreshWarnings == null || watchedProperties == null)
            return;

        for (int propertyIndex = 0; propertyIndex < watchedProperties.Length; propertyIndex++)
        {
            SerializedProperty watchedProperty = watchedProperties[propertyIndex];

            if (watchedProperty == null)
                continue;

            payloadContainer.TrackPropertyValue(watchedProperty, changedProperty =>
            {
                refreshWarnings();
            });
        }
    }

    private static void RefreshCharacterTuningAvailableVariables(SerializedObject serializedObject, Label availableVariablesLabel)
    {
        if (availableVariablesLabel == null)
            return;

        HashSet<string> allowedVariables = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PlayerScalableStatType> variableTypes = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject)
            : new Dictionary<string, PlayerScalableStatType>(StringComparer.OrdinalIgnoreCase);

        availableVariablesLabel.text = PlayerScalingFormulaValidationUtility.BuildAvailableVariablesLabelText(allowedVariables, variableTypes);
    }

    /// <summary>
    /// Refreshes Character Tuning helper UI only when the local formulas payload changes, avoiding global serialized-object watchers on reorderable cards.
    /// </summary>
    /// <param name="payloadContainer">Parent element that receives bubbled serialized change events.</param>
    /// <param name="serializedObject">Serialized object that owns the formulas payload.</param>
    /// <param name="formulasPropertyPath">Property path of the formulas array to re-resolve after local edits.</param>
    /// <param name="refreshUi">Callback that rebinds helper text and warnings after a local formulas edit.</param>
    private static void RegisterCharacterTuningFormulaRefresh(VisualElement payloadContainer,
                                                              SerializedObject serializedObject,
                                                              string formulasPropertyPath,
                                                              Action refreshUi)
    {
        if (payloadContainer == null)
            return;

        if (serializedObject == null)
            return;

        if (string.IsNullOrWhiteSpace(formulasPropertyPath))
            return;

        payloadContainer.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (evt == null || evt.changedProperty == null)
                return;

            string changedPath = evt.changedProperty.propertyPath;

            if (string.IsNullOrWhiteSpace(changedPath))
                return;

            if (!string.Equals(changedPath, formulasPropertyPath, StringComparison.Ordinal) &&
                !changedPath.StartsWith(formulasPropertyPath + ".Array.data[", StringComparison.Ordinal))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            refreshUi?.Invoke();
        });
    }

    private static string BuildCharacterTuningFormulaKey(SerializedObject serializedObject, string formulasPropertyPath)
    {
        if (serializedObject == null || serializedObject.targetObject == null)
            return formulasPropertyPath ?? string.Empty;

        return string.Format("{0}:{1}",
                             serializedObject.targetObject.GetInstanceID(),
                             formulasPropertyPath ?? string.Empty);
    }

    private static bool IsActiveCharacterTuningFormula(string formulaKey)
    {
        return !string.IsNullOrWhiteSpace(formulaKey) &&
               string.Equals(activeCharacterTuningFormulaKey, formulaKey, StringComparison.Ordinal);
    }

    private static void SetActiveCharacterTuningFormula(string formulaKey)
    {
        if (string.IsNullOrWhiteSpace(formulaKey))
            return;

        activeCharacterTuningFormulaKey = formulaKey;
        RefreshRegisteredCharacterTuningFormulas();
    }

    private static void ClearActiveCharacterTuningFormula(string formulaKey)
    {
        if (string.IsNullOrWhiteSpace(formulaKey))
            return;

        if (!string.Equals(activeCharacterTuningFormulaKey, formulaKey, StringComparison.Ordinal))
            return;

        activeCharacterTuningFormulaKey = string.Empty;
        RefreshRegisteredCharacterTuningFormulas();
    }

    private static void RegisterCharacterTuningRefresher(string formulaKey, Action refreshUi)
    {
        if (string.IsNullOrWhiteSpace(formulaKey) || refreshUi == null)
            return;

        characterTuningRefreshByKey[formulaKey] = refreshUi;
    }

    private static void UnregisterCharacterTuningRefresher(string formulaKey)
    {
        if (string.IsNullOrWhiteSpace(formulaKey))
            return;

        characterTuningRefreshByKey.Remove(formulaKey);
        ClearActiveCharacterTuningFormula(formulaKey);
    }

    private static void RefreshRegisteredCharacterTuningFormulas()
    {
        foreach (Action refreshUi in characterTuningRefreshByKey.Values)
            refreshUi?.Invoke();
    }

    private static void UpdateOrbitPathModeContainers(SerializedProperty pathModeProperty,
                                                      VisualElement circleContainer,
                                                      VisualElement spiralContainer)
    {
        ProjectileOrbitPathMode pathMode = ProjectileOrbitPathMode.Circle;

        if (pathModeProperty != null)
            pathMode = (ProjectileOrbitPathMode)pathModeProperty.enumValueIndex;

        if (circleContainer != null)
            circleContainer.style.display = pathMode == ProjectileOrbitPathMode.Circle ? DisplayStyle.Flex : DisplayStyle.None;

        if (spiralContainer != null)
            spiralContainer.style.display = pathMode == ProjectileOrbitPathMode.GoldenSpiral ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
