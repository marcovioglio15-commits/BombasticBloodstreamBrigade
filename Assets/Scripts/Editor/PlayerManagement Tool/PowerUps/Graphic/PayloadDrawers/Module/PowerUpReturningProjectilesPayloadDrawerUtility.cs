using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Returning Projectiles form with contextual controls and non-mutating validation warnings.
/// </summary>
public static class PowerUpReturningProjectilesPayloadDrawerUtility
{
    #region Constants
    internal const string OtherInteractionOptionsContainerName = "returning-projectiles-other-interaction-options";
    internal const string ProjectileVfxOptionsContainerName = "returning-projectiles-projectile-vfx-options";
    internal const string AdditionalOutboundHitsContainerName = "returning-projectiles-additional-outbound-hits";
    internal const string AutomaticReturnDelayContainerName = "returning-projectiles-automatic-delay";
    internal const string ActivationRecallOptionsContainerName = "returning-projectiles-activation-recall-options";
    internal const string ActivationRecallResourceGateContainerName = "returning-projectiles-activation-recall-resource-gate";
    internal const string ReturnTransitionContainerName = "returning-projectiles-return-transition";
    internal const string RepeatedContactDamageSettingsContainerName = "returning-projectiles-repeated-contact-damage-settings";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds every return payload field and hides settings that cannot affect the current configuration.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the module controls.</param>
    /// <param name="payloadProperty">Serialized Returning Projectiles payload.</param>
    /// <param name="showActiveProjectileConcurrencyOption">Whether the active-only overlap setting is relevant.</param>
    /// <param name="hasOwningResourceGate">Whether the owning Active can reapply Resource Gate recall costs.</param>
    public static void Build(VisualElement payloadContainer,
                             SerializedProperty payloadProperty,
                             bool showActiveProjectileConcurrencyOption,
                             bool hasOwningResourceGate = false)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        Foldout projectileFoldout = CreateFoldout("Projectile Override and Return", true);
        Foldout rotationFoldout = CreateFoldout("Rotation", true);
        Foldout outboundHitFoldout = CreateFoldout("Outbound Hits", true);
        Foldout returnHitFoldout = CreateFoldout("Return Hits", true);
        Foldout repeatedContactDamageFoldout = CreateFoldout("Repeated Contact Damage", true);
        Foldout precisionFoldout = CreateFoldout("Trajectory Precision", false);
        Foldout interactionFoldout = CreateFoldout("Power-Up Interactions", true);
        payloadContainer.Add(projectileFoldout);
        payloadContainer.Add(rotationFoldout);
        payloadContainer.Add(outboundHitFoldout);
        payloadContainer.Add(returnHitFoldout);
        payloadContainer.Add(repeatedContactDamageFoldout);
        payloadContainer.Add(precisionFoldout);
        payloadContainer.Add(interactionFoldout);

        SerializedProperty replacementProjectilePrefabProperty = AddField(projectileFoldout,
                                                                            payloadProperty,
                                                                            "replacementProjectilePrefab",
                                                                            "Replacement Projectile Prefab");
        VisualElement projectileVfxContainer = new VisualElement();
        projectileVfxContainer.name = ProjectileVfxOptionsContainerName;
        projectileFoldout.Add(projectileVfxContainer);
        AddField(projectileVfxContainer, payloadProperty, "keepProjectileVfx", "Keep Player Projectile VFX");
        AddField(projectileVfxContainer, payloadProperty, "keepMuzzleFlashVfx", "Keep Muzzle Flash VFX");
        AddField(projectileVfxContainer, payloadProperty, "keepHitVfx", "Keep Hit VFX");
        AddField(projectileVfxContainer, payloadProperty, "keepDeathVfx", "Keep Death VFX");
        SerializedProperty returnPathModeProperty = AddField(projectileFoldout, payloadProperty, "returnPathMode", "Return Path Mode");
        AddField(projectileFoldout, payloadProperty, "returnSpeedMultiplier", "Return Speed Multiplier");
        AddField(projectileFoldout, payloadProperty, "outboundRangeMultiplier", "Outbound Range Multiplier");
        AddField(projectileFoldout, payloadProperty, "outboundLifetimeMultiplier", "Outbound Lifetime Multiplier");
        SerializedProperty returnStartModeProperty = payloadProperty.FindPropertyRelative("returnStartMode");
        SerializedProperty returnDelaySecondsProperty = payloadProperty.FindPropertyRelative("returnDelaySeconds");
        VisualElement returnTransitionContainer = new VisualElement();
        returnTransitionContainer.name = ReturnTransitionContainerName;
        VisualElement automaticReturnDelayContainer = new VisualElement();
        automaticReturnDelayContainer.name = AutomaticReturnDelayContainerName;
        automaticReturnDelayContainer.style.marginLeft = 12f;
        VisualElement activationRecallOptionsContainer = new VisualElement();
        activationRecallOptionsContainer.name = ActivationRecallOptionsContainerName;
        activationRecallOptionsContainer.style.marginLeft = 12f;
        VisualElement activationRecallResourceGateContainer = new VisualElement();
        activationRecallResourceGateContainer.name = ActivationRecallResourceGateContainerName;
        projectileFoldout.Add(returnTransitionContainer);

        if (showActiveProjectileConcurrencyOption)
        {
            PowerUpModuleDefinitionPayloadDrawerUtility.AddField(returnTransitionContainer, returnStartModeProperty, "Return Start Mode");
            AddField(activationRecallOptionsContainer, payloadProperty, "allowEarlyActivationRecall", "Allow Early Activation Recall");

            if (hasOwningResourceGate)
            {
                AddField(activationRecallResourceGateContainer,
                         payloadProperty,
                         "reapplyResourceGateCostOnRecall",
                         "Reapply Resource Gate Cost on Recall");
            }
        }
        else
        {
            Label returnTransitionLabel = new Label("Return Transition");
            returnTransitionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            returnTransitionContainer.Add(returnTransitionLabel);
        }

        returnTransitionContainer.Add(automaticReturnDelayContainer);
        returnTransitionContainer.Add(activationRecallOptionsContainer);
        activationRecallOptionsContainer.Add(activationRecallResourceGateContainer);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(automaticReturnDelayContainer,
                                                             returnDelaySecondsProperty,
                                                             "Return Delay (Seconds)");
        AddField(returnTransitionContainer, payloadProperty, "returnRumbleMultiplier", "Return Rumble Multiplier");
        AddField(returnTransitionContainer, payloadProperty, "returnCameraShakeMultiplier", "Return Camera Shake Multiplier");
        AddField(projectileFoldout, payloadProperty, "outboundSizeMultiplier", "Outbound Size Multiplier");
        AddField(projectileFoldout, payloadProperty, "returnSizeMultiplier", "Return Size Multiplier");

        SerializedProperty spinDuringFlightProperty = AddField(rotationFoldout, payloadProperty, "spinDuringFlight", "Spin During Flight");
        VisualElement flightSpinContainer = new VisualElement();
        VisualElement turnaroundContainer = new VisualElement();
        rotationFoldout.Add(flightSpinContainer);
        rotationFoldout.Add(turnaroundContainer);
        AddField(flightSpinContainer, payloadProperty, "spinSpeedDegreesPerSecond", "Spin Speed (Degrees/Second)");
        AddField(flightSpinContainer, payloadProperty, "spinAxis", "Spin Axis");
        AddField(turnaroundContainer, payloadProperty, "turnaroundRotationSpeedDegreesPerSecond", "Turnaround Speed (Degrees/Second)");
        AddField(turnaroundContainer, payloadProperty, "turnaroundAxis", "Turnaround Axis");

        SerializedProperty outboundHitPolicyProperty = AddField(outboundHitFoldout,
                                                                 payloadProperty,
                                                                 "outboundHitPolicy",
                                                                 "Outbound Hit Policy");
        VisualElement additionalOutboundHitsContainer = new VisualElement();
        additionalOutboundHitsContainer.name = AdditionalOutboundHitsContainerName;
        outboundHitFoldout.Add(additionalOutboundHitsContainer);
        AddField(additionalOutboundHitsContainer, payloadProperty, "additionalOutboundHits", "Additional Outbound Hits");

        SerializedProperty returnHitPolicyProperty = AddField(returnHitFoldout, payloadProperty, "returnHitPolicy", "Return Hit Policy");
        VisualElement additionalHitsContainer = new VisualElement();
        returnHitFoldout.Add(additionalHitsContainer);
        AddField(additionalHitsContainer, payloadProperty, "additionalReturnHits", "Additional Return Hits");

        SerializedProperty enableRepeatedContactDamageProperty = AddField(repeatedContactDamageFoldout,
                                                                           payloadProperty,
                                                                           "enableRepeatedContactDamage",
                                                                           "Enable Repeated Contact Damage");
        VisualElement repeatedContactDamageSettingsContainer = new VisualElement();
        repeatedContactDamageSettingsContainer.name = RepeatedContactDamageSettingsContainerName;
        repeatedContactDamageSettingsContainer.style.marginLeft = 12f;
        repeatedContactDamageFoldout.Add(repeatedContactDamageSettingsContainer);
        AddField(repeatedContactDamageSettingsContainer, payloadProperty, "repeatedContactDamage", "Damage Per Tick");
        AddField(repeatedContactDamageSettingsContainer,
                 payloadProperty,
                 "repeatedContactDamageIntervalSeconds",
                 "Tick Interval Seconds");

        VisualElement pathSamplingContainer = new VisualElement();
        precisionFoldout.Add(pathSamplingContainer);
        AddField(pathSamplingContainer, payloadProperty, "pathSampleDistance", "Path Sample Distance");
        AddField(precisionFoldout, payloadProperty, "returnCompletionDistance", "Return Completion Distance");

        SerializedProperty allowOtherInteractionsProperty = AddField(interactionFoldout,
                                                                     payloadProperty,
                                                                     "allowOtherPowerUpInteractions",
                                                                     "Allow Interactions With Other Power-Ups");
        VisualElement otherInteractionOptionsContainer = new VisualElement();
        otherInteractionOptionsContainer.name = OtherInteractionOptionsContainerName;
        interactionFoldout.Add(otherInteractionOptionsContainer);
        AddField(otherInteractionOptionsContainer, payloadProperty, "enableProjectileSplitting", "Enable Projectile Splitting");
        AddField(otherInteractionOptionsContainer, payloadProperty, "applyToSplitProjectiles", "Apply to External Projectile Split Children");
        AddField(otherInteractionOptionsContainer, payloadProperty, "completeBouncesBeforeReturn", "Complete External Bounces Before Return");
        AddField(otherInteractionOptionsContainer, payloadProperty, "completeOrbitalPathBeforeReturn", "Enable and Complete Orbital Path Before Return");
        AddField(otherInteractionOptionsContainer, payloadProperty, "applyTinyMegaProjectileScaling", "Apply Tiny/Mega Projectile Scaling");

        if (showActiveProjectileConcurrencyOption)
            AddField(interactionFoldout, payloadProperty, "allowConcurrentActiveProjectiles", "Allow Concurrent Active Projectiles");
        else
            AddField(otherInteractionOptionsContainer, payloadProperty, "applyToActivePowerUpProjectiles", "Apply to Other Active Projectile Shots");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        warningBox.style.display = DisplayStyle.None;
        payloadContainer.Add(warningBox);

        RefreshConditionalUi();
        RefreshWarnings();
        payloadContainer.TrackPropertyValue(spinDuringFlightProperty, changeEvent => RefreshConditionalUi());
        payloadContainer.TrackPropertyValue(outboundHitPolicyProperty, changeEvent => RefreshConditionalUi());
        payloadContainer.TrackPropertyValue(returnHitPolicyProperty, changeEvent => RefreshConditionalUi());
        payloadContainer.TrackPropertyValue(returnPathModeProperty, changeEvent => RefreshConditionalUi());
        payloadContainer.TrackPropertyValue(replacementProjectilePrefabProperty, changeEvent => RefreshConditionalUi());
        payloadContainer.TrackPropertyValue(allowOtherInteractionsProperty, changeEvent => RefreshConditionalUi());
        payloadContainer.TrackPropertyValue(enableRepeatedContactDamageProperty, changeEvent => RefreshConditionalUi());

        if (showActiveProjectileConcurrencyOption)
            payloadContainer.TrackPropertyValue(returnStartModeProperty, changeEvent => RefreshConditionalUi());

        payloadContainer.RegisterCallback<SerializedPropertyChangeEvent>(changeEvent => RefreshWarnings());

        void RefreshConditionalUi()
        {
            projectileVfxContainer.style.display = replacementProjectilePrefabProperty.objectReferenceValue != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            flightSpinContainer.style.display = spinDuringFlightProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            turnaroundContainer.style.display = spinDuringFlightProperty.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            additionalOutboundHitsContainer.style.display = outboundHitPolicyProperty.enumValueIndex == (int)ProjectileOutboundHitPolicy.LimitedAdditionalHits
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            additionalHitsContainer.style.display = returnHitPolicyProperty.enumValueIndex == (int)ProjectileReturnHitPolicy.LimitedAdditionalHits
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            pathSamplingContainer.style.display = returnPathModeProperty.enumValueIndex == (int)ProjectileReturnPathMode.RetraceOutboundPath
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            otherInteractionOptionsContainer.style.display = allowOtherInteractionsProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            bool waitsForActivationRecall = showActiveProjectileConcurrencyOption &&
                                            returnStartModeProperty.enumValueIndex == (int)ProjectileReturnStartMode.ActivationTap;
            automaticReturnDelayContainer.style.display = waitsForActivationRecall ? DisplayStyle.None : DisplayStyle.Flex;
            activationRecallOptionsContainer.style.display = waitsForActivationRecall ? DisplayStyle.Flex : DisplayStyle.None;
            activationRecallResourceGateContainer.style.display = waitsForActivationRecall && hasOwningResourceGate
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            repeatedContactDamageSettingsContainer.style.display = enableRepeatedContactDamageProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshWarnings();
        }

        void RefreshWarnings()
        {
            List<string> warnings = new List<string>();
            GameObject replacementPrefab = replacementProjectilePrefabProperty.objectReferenceValue as GameObject;

            if (replacementPrefab != null && PrefabUtility.GetPrefabAssetType(replacementPrefab) == PrefabAssetType.NotAPrefab)
                warnings.Add("Replacement Projectile Prefab must reference a prefab asset.");

            if (replacementPrefab != null && replacementPrefab.GetComponentInChildren<PlayerAuthoring>(true) != null)
                warnings.Add("Replacement Projectile Prefab cannot contain PlayerAuthoring because pooled shots must not duplicate player runtime data.");

            AddPositiveWarning(payloadProperty, "returnSpeedMultiplier", "Return Speed Multiplier", warnings);
            AddPositiveWarning(payloadProperty, "outboundRangeMultiplier", "Outbound Range Multiplier", warnings);
            AddPositiveWarning(payloadProperty, "outboundLifetimeMultiplier", "Outbound Lifetime Multiplier", warnings);
            if (!showActiveProjectileConcurrencyOption ||
                returnStartModeProperty.enumValueIndex == (int)ProjectileReturnStartMode.AutomaticDelay)
            {
                AddNonNegativeWarning(payloadProperty, "returnDelaySeconds", "Return Delay", warnings);
            }

            SerializedProperty reapplyResourceGateCostProperty = payloadProperty.FindPropertyRelative("reapplyResourceGateCostOnRecall");

            if (showActiveProjectileConcurrencyOption &&
                returnStartModeProperty.enumValueIndex == (int)ProjectileReturnStartMode.ActivationTap &&
                !hasOwningResourceGate &&
                reapplyResourceGateCostProperty != null &&
                reapplyResourceGateCostProperty.boolValue)
            {
                warnings.Add("Reapply Resource Gate Cost on Recall is ignored because this Active does not contain Resource Gate.");
            }

            AddNonNegativeWarning(payloadProperty, "returnRumbleMultiplier", "Return Rumble Multiplier", warnings);
            AddNonNegativeWarning(payloadProperty, "returnCameraShakeMultiplier", "Return Camera Shake Multiplier", warnings);
            AddPositiveWarning(payloadProperty, "outboundSizeMultiplier", "Outbound Size Multiplier", warnings);
            AddPositiveWarning(payloadProperty, "returnSizeMultiplier", "Return Size Multiplier", warnings);
            AddPositiveWarning(payloadProperty, "returnCompletionDistance", "Return Completion Distance", warnings);

            if (spinDuringFlightProperty.boolValue)
                AddPositiveWarning(payloadProperty, "spinSpeedDegreesPerSecond", "Spin Speed", warnings);
            else
                AddPositiveWarning(payloadProperty, "turnaroundRotationSpeedDegreesPerSecond", "Turnaround Speed", warnings);

            if (outboundHitPolicyProperty.enumValueIndex == (int)ProjectileOutboundHitPolicy.LimitedAdditionalHits)
                AddPositiveIntegerWarning(payloadProperty, "additionalOutboundHits", "Additional Outbound Hits", warnings);

            if (returnHitPolicyProperty.enumValueIndex == (int)ProjectileReturnHitPolicy.LimitedAdditionalHits)
                AddPositiveIntegerWarning(payloadProperty, "additionalReturnHits", "Additional Return Hits", warnings);

            if (enableRepeatedContactDamageProperty.boolValue)
            {
                AddPositiveWarning(payloadProperty, "repeatedContactDamage", "Repeated Contact Damage", warnings);
                AddPositiveWarning(payloadProperty,
                                   "repeatedContactDamageIntervalSeconds",
                                   "Repeated Contact Damage Tick Interval",
                                   warnings);
                SerializedProperty repeatedContactDamageIntervalProperty = payloadProperty.FindPropertyRelative("repeatedContactDamageIntervalSeconds");

                if (repeatedContactDamageIntervalProperty != null &&
                    repeatedContactDamageIntervalProperty.floatValue > 0f &&
                    repeatedContactDamageIntervalProperty.floatValue < 0.03f)
                {
                    warnings.Add("Repeated Contact Damage Tick Interval below 0.03 can create dense overlap damage pulses with large projectile counts.");
                }
            }

            if (returnPathModeProperty.enumValueIndex == (int)ProjectileReturnPathMode.RetraceOutboundPath)
                AddPositiveWarning(payloadProperty, "pathSampleDistance", "Path Sample Distance", warnings);

            warningBox.text = string.Join("\n", warnings);
            warningBox.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one field through the shared scaling factory.
    /// </summary>
    /// <param name="parent">Container receiving the field.</param>
    /// <param name="payloadProperty">Payload containing the serialized child.</param>
    /// <param name="propertyName">Relative serialized property name.</param>
    /// <param name="label">Visible field label.</param>
    /// <returns>The resolved serialized property, or null when the payload layout is invalid.</returns>
    private static SerializedProperty AddField(VisualElement parent,
                                               SerializedProperty payloadProperty,
                                               string propertyName,
                                               string label)
    {
        SerializedProperty property = payloadProperty.FindPropertyRelative(propertyName);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(parent, property, label);
        return property;
    }

    /// <summary>
    /// Creates one compact payload foldout.
    /// </summary>
    /// <param name="title">Foldout title.</param>
    /// <param name="expanded">Initial expanded state.</param>
    /// <returns>Configured foldout.</returns>
    private static Foldout CreateFoldout(string title, bool expanded)
    {
        return new Foldout
        {
            text = title,
            value = expanded
        };
    }

    /// <summary>
    /// Adds a warning when one floating-point field is not strictly positive.
    /// </summary>
    /// <param name="payloadProperty">Payload containing the field.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="label">Readable warning label.</param>
    /// <param name="warnings">Warning collection receiving any issue.</param>
    private static void AddPositiveWarning(SerializedProperty payloadProperty,
                                           string propertyName,
                                           string label,
                                           List<string> warnings)
    {
        SerializedProperty property = payloadProperty.FindPropertyRelative(propertyName);

        if (property != null && property.floatValue <= 0f)
            warnings.Add(label + " must be greater than zero.");
    }

    /// <summary>
    /// Adds a warning when one floating-point field is negative while preserving zero as its supported disabled state.
    /// </summary>
    /// <param name="payloadProperty">Payload containing the field.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="label">Readable warning label.</param>
    /// <param name="warnings">Warning collection receiving any issue.</param>
    private static void AddNonNegativeWarning(SerializedProperty payloadProperty,
                                              string propertyName,
                                              string label,
                                              List<string> warnings)
    {
        SerializedProperty property = payloadProperty.FindPropertyRelative(propertyName);

        if (property != null && property.floatValue < 0f)
            warnings.Add(label + " cannot be negative.");
    }

    /// <summary>
    /// Adds a warning when one integer field is not strictly positive.
    /// </summary>
    /// <param name="payloadProperty">Payload containing the field.</param>
    /// <param name="propertyName">Relative field name.</param>
    /// <param name="label">Readable warning label.</param>
    /// <param name="warnings">Warning collection receiving any issue.</param>
    private static void AddPositiveIntegerWarning(SerializedProperty payloadProperty,
                                                  string propertyName,
                                                  string label,
                                                  List<string> warnings)
    {
        SerializedProperty property = payloadProperty.FindPropertyRelative(propertyName);

        if (property != null && property.intValue <= 0)
            warnings.Add(label + " must be greater than zero.");
    }
    #endregion

    #endregion
}
