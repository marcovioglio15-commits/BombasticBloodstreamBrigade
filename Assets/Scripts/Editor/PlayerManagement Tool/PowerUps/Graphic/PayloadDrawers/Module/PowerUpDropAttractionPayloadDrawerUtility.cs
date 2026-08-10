using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Drop Attraction module editor and its authoring warnings.
/// </summary>
public static class PowerUpDropAttractionPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds Drop Attraction fields and reports a non-positive radius without mutating authored values.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the Drop Attraction controls and warning.</param>
    /// <param name="dropAttractionPayloadProperty">Serialized Drop Attraction payload root.</param>
    public static void Build(VisualElement payloadContainer,
                             SerializedProperty dropAttractionPayloadProperty)
    {
        if (payloadContainer == null || dropAttractionPayloadProperty == null)
            return;

        SerializedProperty attractionRadiusProperty = dropAttractionPayloadProperty.FindPropertyRelative("attractionRadius");
        SerializedProperty consumeUnusableDropsProperty = dropAttractionPayloadProperty.FindPropertyRelative("consumeUnusableDrops");

        if (attractionRadiusProperty == null || consumeUnusableDropsProperty == null)
        {
            payloadContainer.Add(new HelpBox("Drop Attraction payload fields are missing.", HelpBoxMessageType.Warning));
            return;
        }

        payloadContainer.Add(new HelpBox("Passive power-ups extend collection reach continuously. Standard active power-ups attract once after a successful activation, while a toggleable Resource Gate keeps the radius active until the toggle ends.",
                                         HelpBoxMessageType.Info));
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer,
                                                             attractionRadiusProperty,
                                                             "Attraction Radius");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(payloadContainer,
                                                             consumeUnusableDropsProperty,
                                                             "Consume Unusable Drops");
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);
        Action refreshWarning = () => RefreshWarning(attractionRadiusProperty, warningBox);
        refreshWarning();
        payloadContainer.TrackPropertyValue(attractionRadiusProperty, changedProperty => refreshWarning());
    }
    #endregion

    #region Validation
    /// <summary>
    /// Refreshes the radius warning while preserving the authored payload exactly as entered.
    /// </summary>
    /// <param name="attractionRadiusProperty">Serialized radius property being validated.</param>
    /// <param name="warningBox">Warning element receiving the current validation result.</param>
    private static void RefreshWarning(SerializedProperty attractionRadiusProperty,
                                       HelpBox warningBox)
    {
        List<string> warnings = new List<string>();

        if (attractionRadiusProperty.floatValue <= 0f)
            warnings.Add("Attraction Radius must be greater than zero. This module currently has no collection area.");

        PowerUpPayloadWarningBoxUtility.ApplyWarnings(warningBox, warnings);
    }
    #endregion

    #endregion
}
