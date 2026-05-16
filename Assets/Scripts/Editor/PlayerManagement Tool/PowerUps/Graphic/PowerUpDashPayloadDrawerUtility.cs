using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Dash payload UI shared by modules, overrides, and legacy active-tool drawers.
/// </summary>
public static class PowerUpDashPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds Dash controls, Add Scaling buttons, contextual visibility and non-mutating warnings to the provided container.
    /// </summary>
    /// <param name="payloadContainer">Container that receives Dash controls.</param>
    /// <param name="dashPayloadProperty">Serialized DashToolData payload.</param>
    public static void BuildDashPayloadUi(VisualElement payloadContainer, SerializedProperty dashPayloadProperty)
    {
        if (payloadContainer == null || dashPayloadProperty == null)
            return;

        SerializedProperty distanceProperty = dashPayloadProperty.FindPropertyRelative("distance");
        SerializedProperty directionModeProperty = dashPayloadProperty.FindPropertyRelative("directionMode");
        SerializedProperty durationProperty = dashPayloadProperty.FindPropertyRelative("duration");
        SerializedProperty speedTransitionInSecondsProperty = dashPayloadProperty.FindPropertyRelative("speedTransitionInSeconds");
        SerializedProperty speedTransitionOutSecondsProperty = dashPayloadProperty.FindPropertyRelative("speedTransitionOutSeconds");
        SerializedProperty wallBounceIntensityProperty = dashPayloadProperty.FindPropertyRelative("wallBounceIntensity");
        SerializedProperty grantsInvulnerabilityProperty = dashPayloadProperty.FindPropertyRelative("grantsInvulnerability");
        SerializedProperty invulnerabilityExtraTimeProperty = dashPayloadProperty.FindPropertyRelative("invulnerabilityExtraTime");

        if (distanceProperty == null ||
            directionModeProperty == null ||
            durationProperty == null ||
            speedTransitionInSecondsProperty == null ||
            speedTransitionOutSecondsProperty == null ||
            wallBounceIntensityProperty == null ||
            grantsInvulnerabilityProperty == null ||
            invulnerabilityExtraTimeProperty == null)
        {
            HelpBox errorBox = new HelpBox("Dash payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, distanceProperty, "Distance");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, directionModeProperty, "Direction Mode");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, durationProperty, "Duration");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, speedTransitionInSecondsProperty, "Speed Transition In Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, speedTransitionOutSecondsProperty, "Speed Transition Out Seconds");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, wallBounceIntensityProperty, "Wall Bounce Intensity");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer, grantsInvulnerabilityProperty, "Grants Invulnerability");

        VisualElement invulnerabilityContainer = new VisualElement();
        invulnerabilityContainer.style.marginLeft = 12f;
        payloadContainer.Add(invulnerabilityContainer);
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(invulnerabilityContainer, invulnerabilityExtraTimeProperty, "Invulnerability Extra Time");

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);

        UpdateBooleanContainerVisibility(grantsInvulnerabilityProperty, invulnerabilityContainer);
        RefreshDashWarnings(distanceProperty,
                            durationProperty,
                            speedTransitionInSecondsProperty,
                            speedTransitionOutSecondsProperty,
                            wallBounceIntensityProperty,
                            grantsInvulnerabilityProperty,
                            invulnerabilityExtraTimeProperty,
                            warningBox);

        payloadContainer.TrackPropertyValue(grantsInvulnerabilityProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, invulnerabilityContainer);
            RefreshDashWarnings(distanceProperty,
                                durationProperty,
                                speedTransitionInSecondsProperty,
                                speedTransitionOutSecondsProperty,
                                wallBounceIntensityProperty,
                                changedProperty,
                                invulnerabilityExtraTimeProperty,
                                warningBox);
        });
        RegisterDashWarningRefresh(payloadContainer,
                                   distanceProperty,
                                   durationProperty,
                                   speedTransitionInSecondsProperty,
                                   speedTransitionOutSecondsProperty,
                                   wallBounceIntensityProperty,
                                   grantsInvulnerabilityProperty,
                                   invulnerabilityExtraTimeProperty,
                                   warningBox);
    }
    #endregion

    #region Visibility
    /// <summary>
    /// Shows or hides one dependent Dash options group from a serialized boolean toggle.
    /// </summary>
    /// <param name="toggleProperty">Serialized boolean toggle controlling the section.</param>
    /// <param name="container">Visual section shown only when the toggle is enabled.</param>
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
    #endregion

    #region Warnings
    /// <summary>
    /// Refreshes validation warnings for Dash payload fields without mutating serialized values.
    /// </summary>
    /// <param name="distanceProperty">Serialized Distance field.</param>
    /// <param name="durationProperty">Serialized Duration field.</param>
    /// <param name="speedTransitionInSecondsProperty">Serialized transition-in field.</param>
    /// <param name="speedTransitionOutSecondsProperty">Serialized transition-out field.</param>
    /// <param name="wallBounceIntensityProperty">Serialized wall bounce intensity field.</param>
    /// <param name="grantsInvulnerabilityProperty">Serialized invulnerability toggle.</param>
    /// <param name="invulnerabilityExtraTimeProperty">Serialized post-dash invulnerability field.</param>
    /// <param name="warningBox">HelpBox receiving the current warning text.</param>
    private static void RefreshDashWarnings(SerializedProperty distanceProperty,
                                            SerializedProperty durationProperty,
                                            SerializedProperty speedTransitionInSecondsProperty,
                                            SerializedProperty speedTransitionOutSecondsProperty,
                                            SerializedProperty wallBounceIntensityProperty,
                                            SerializedProperty grantsInvulnerabilityProperty,
                                            SerializedProperty invulnerabilityExtraTimeProperty,
                                            HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();
        float duration = durationProperty != null ? durationProperty.floatValue : 0f;
        float transitionIn = speedTransitionInSecondsProperty != null ? speedTransitionInSecondsProperty.floatValue : 0f;
        float transitionOut = speedTransitionOutSecondsProperty != null ? speedTransitionOutSecondsProperty.floatValue : 0f;
        float wallBounceIntensity = wallBounceIntensityProperty != null ? wallBounceIntensityProperty.floatValue : 0f;

        if (distanceProperty != null && distanceProperty.floatValue <= 0f)
            warningLines.Add("Distance should be > 0 for a usable dash.");

        if (durationProperty != null && duration <= 0f)
            warningLines.Add("Duration should be > 0 for a usable dash.");

        if (speedTransitionInSecondsProperty != null && transitionIn < 0f)
            warningLines.Add("Speed Transition In Seconds should be >= 0.");

        if (speedTransitionOutSecondsProperty != null && transitionOut < 0f)
            warningLines.Add("Speed Transition Out Seconds should be >= 0.");

        if (duration > 0f && transitionIn + transitionOut > duration)
            warningLines.Add("Transition In + Transition Out is longer than Duration; runtime clamps transition timing before preserving dash distance.");

        if (wallBounceIntensityProperty != null && wallBounceIntensity < 0f)
            warningLines.Add("Wall Bounce Intensity should be >= 0.");
        else if (wallBounceIntensityProperty != null && wallBounceIntensity > 1f)
            warningLines.Add("Wall Bounce Intensity above 1 is clamped at runtime.");

        if (grantsInvulnerabilityProperty != null &&
            grantsInvulnerabilityProperty.boolValue &&
            invulnerabilityExtraTimeProperty != null &&
            invulnerabilityExtraTimeProperty.floatValue < 0f)
            warningLines.Add("Invulnerability Extra Time should be >= 0 when Grants Invulnerability is enabled.");

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
    /// Registers serialized-property watchers that refresh Dash warnings after field edits.
    /// </summary>
    /// <param name="payloadContainer">Root payload element used to observe serialized-property changes.</param>
    /// <param name="distanceProperty">Serialized Distance field.</param>
    /// <param name="durationProperty">Serialized Duration field.</param>
    /// <param name="speedTransitionInSecondsProperty">Serialized transition-in field.</param>
    /// <param name="speedTransitionOutSecondsProperty">Serialized transition-out field.</param>
    /// <param name="wallBounceIntensityProperty">Serialized wall bounce intensity field.</param>
    /// <param name="grantsInvulnerabilityProperty">Serialized invulnerability toggle.</param>
    /// <param name="invulnerabilityExtraTimeProperty">Serialized post-dash invulnerability field.</param>
    /// <param name="warningBox">HelpBox receiving the current warning text.</param>
    private static void RegisterDashWarningRefresh(VisualElement payloadContainer,
                                                   SerializedProperty distanceProperty,
                                                   SerializedProperty durationProperty,
                                                   SerializedProperty speedTransitionInSecondsProperty,
                                                   SerializedProperty speedTransitionOutSecondsProperty,
                                                   SerializedProperty wallBounceIntensityProperty,
                                                   SerializedProperty grantsInvulnerabilityProperty,
                                                   SerializedProperty invulnerabilityExtraTimeProperty,
                                                   HelpBox warningBox)
    {
        RegisterDashWarningRefresh(payloadContainer,
                                   () =>
                                   {
                                       RefreshDashWarnings(distanceProperty,
                                                           durationProperty,
                                                           speedTransitionInSecondsProperty,
                                                           speedTransitionOutSecondsProperty,
                                                           wallBounceIntensityProperty,
                                                           grantsInvulnerabilityProperty,
                                                           invulnerabilityExtraTimeProperty,
                                                           warningBox);
                                   },
                                   distanceProperty,
                                   durationProperty,
                                   speedTransitionInSecondsProperty,
                                   speedTransitionOutSecondsProperty,
                                   wallBounceIntensityProperty,
                                   invulnerabilityExtraTimeProperty);
    }

    /// <summary>
    /// Registers one warning refresh callback for every provided Dash payload property.
    /// </summary>
    /// <param name="payloadContainer">Root payload element used to observe serialized-property changes.</param>
    /// <param name="refreshWarnings">Callback that recomputes current warning text.</param>
    /// <param name="watchedProperties">Serialized fields that should trigger warning refreshes.</param>
    private static void RegisterDashWarningRefresh(VisualElement payloadContainer,
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
