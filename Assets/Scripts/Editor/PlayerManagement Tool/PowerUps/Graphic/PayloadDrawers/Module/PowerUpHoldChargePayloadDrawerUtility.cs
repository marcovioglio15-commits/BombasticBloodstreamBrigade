using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static PowerUpModuleDefinitionPayloadDrawerUtility;

/// <summary>
/// Keeps hold-charge controls and their warnings separate from the module editor dispatcher.
/// </summary>
public static class PowerUpHoldChargePayloadDrawerUtility
{
    #region Methods

    #region Layout
    /// <summary>
    /// Builds the complete hold-charge form for module defaults and binding overrides.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the scalable payload controls.</param>
    /// <param name="holdChargePayloadProperty">Serialized hold-charge payload being edited.</param>
    public static void Build(VisualElement payloadContainer, SerializedProperty holdChargePayloadProperty)
    {
        if (payloadContainer == null || holdChargePayloadProperty == null)
            return;

        SerializedProperty requiredChargeProperty = holdChargePayloadProperty.FindPropertyRelative("requiredCharge");
        SerializedProperty maximumChargeProperty = holdChargePayloadProperty.FindPropertyRelative("maximumCharge");
        SerializedProperty chargeRatePerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("chargeRatePerSecond");
        SerializedProperty chargeAnimationClipSlotProperty = holdChargePayloadProperty.FindPropertyRelative("chargeAnimationClipSlot");
        SerializedProperty releaseAnimationClipSlotProperty = holdChargePayloadProperty.FindPropertyRelative("releaseAnimationClipSlot");
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
            chargeAnimationClipSlotProperty == null ||
            releaseAnimationClipSlotProperty == null ||
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
        AddField(payloadContainer, chargeAnimationClipSlotProperty, "Charge Animation");
        AddField(payloadContainer, releaseAnimationClipSlotProperty, "Release Animation");
        PowerUpChargeRumbleDrawerUtility.Build(payloadContainer, holdChargePayloadProperty);
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
                                      chargeAnimationClipSlotProperty,
                                      releaseAnimationClipSlotProperty,
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
                                         chargeAnimationClipSlotProperty,
                                         releaseAnimationClipSlotProperty,
                                         decayAfterReleasePercentPerSecondProperty,
                                         passiveChargeGainPercentPerSecondProperty,
                                         laserDurationSecondsProperty,
                                         chargedLaserDurationSecondsProperty,
                                         maximumPlayerSlowPercentProperty,
                                         playerSlowCurveProperty);
    }

    #endregion

    #region Validation
    /// <summary>
    /// Refreshes validation warnings for hold-charge payload fields without mutating serialized values.
    /// </summary>
    /// <param name="requiredChargeProperty">Serialized Required Charge field.</param>
    /// <param name="maximumChargeProperty">Serialized Maximum Charge field.</param>
    /// <param name="chargeRatePerSecondProperty">Serialized Charge Rate Per Second field.</param>
    /// <param name="chargeAnimationClipSlotProperty">Serialized optional charging animation slot.</param>
    /// <param name="releaseAnimationClipSlotProperty">Serialized optional release animation slot.</param>
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
                                                  SerializedProperty chargeAnimationClipSlotProperty,
                                                  SerializedProperty releaseAnimationClipSlotProperty,
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

        if (chargeAnimationClipSlotProperty != null &&
            (chargeAnimationClipSlotProperty.intValue < (int)PlayerChargeAnimationClipSlot.None ||
             chargeAnimationClipSlotProperty.intValue > (int)PlayerChargeAnimationClipSlot.Secondary))
        {
            warningLines.Add("Charge Animation is outside the supported slot range and will be clamped at bake/runtime.");
        }

        if (releaseAnimationClipSlotProperty != null &&
            (releaseAnimationClipSlotProperty.intValue < (int)PlayerReleaseAnimationClipSlot.None ||
             releaseAnimationClipSlotProperty.intValue > (int)PlayerReleaseAnimationClipSlot.Secondary))
        {
            warningLines.Add("Release Animation is outside the supported slot range and will be clamped at bake/runtime.");
        }

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

    #endregion

    #endregion
}
