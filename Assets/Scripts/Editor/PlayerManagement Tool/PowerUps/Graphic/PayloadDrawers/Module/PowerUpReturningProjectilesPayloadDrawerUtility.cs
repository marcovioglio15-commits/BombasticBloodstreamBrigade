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
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds every return payload field and hides settings that cannot affect the current configuration.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the module controls.</param>
    /// <param name="payloadProperty">Serialized Returning Projectiles payload.</param>
    /// <param name="showActiveProjectileConcurrencyOption">Whether the active-only overlap setting is relevant.</param>
    public static void Build(VisualElement payloadContainer,
                             SerializedProperty payloadProperty,
                             bool showActiveProjectileConcurrencyOption)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        Foldout projectileFoldout = CreateFoldout("Projectile Override and Return", true);
        Foldout rotationFoldout = CreateFoldout("Rotation", true);
        Foldout outboundHitFoldout = CreateFoldout("Outbound Hits", true);
        Foldout returnHitFoldout = CreateFoldout("Return Hits", true);
        Foldout precisionFoldout = CreateFoldout("Trajectory Precision", false);
        Foldout interactionFoldout = CreateFoldout("Power-Up Interactions", true);
        payloadContainer.Add(projectileFoldout);
        payloadContainer.Add(rotationFoldout);
        payloadContainer.Add(outboundHitFoldout);
        payloadContainer.Add(returnHitFoldout);
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
        AddField(projectileFoldout, payloadProperty, "returnDelaySeconds", "Return Delay (Seconds)");
        AddField(projectileFoldout, payloadProperty, "returnRumbleMultiplier", "Return Rumble Multiplier");
        AddField(projectileFoldout, payloadProperty, "returnCameraShakeMultiplier", "Return Camera Shake Multiplier");
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
        AddField(otherInteractionOptionsContainer, payloadProperty, "completeOrbitalPathBeforeReturn", "Complete External Orbital Path Before Return");
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
        payloadContainer.TrackSerializedObjectValue(payloadProperty.serializedObject, serializedObject => RefreshWarnings());

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
            AddNonNegativeWarning(payloadProperty, "returnDelaySeconds", "Return Delay", warnings);
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
