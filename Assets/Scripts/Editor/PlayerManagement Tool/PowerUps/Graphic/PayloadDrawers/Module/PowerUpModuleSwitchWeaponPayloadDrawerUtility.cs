using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the scaling-aware Switch Weapon payload editor for defined Weapon Id tokens.
/// </summary>
internal static class PowerUpModuleSwitchWeaponPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the Weapon Id token field and coherent fixed-string validation warnings.
    /// </summary>
    /// <param name="payloadContainer">Container receiving the Switch Weapon controls and warnings.</param>
    /// <param name="payloadProperty">Serialized Switch Weapon payload property.</param>
    public static void Build(VisualElement payloadContainer, SerializedProperty payloadProperty)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        SerializedProperty weaponIdProperty = payloadProperty.FindPropertyRelative("weaponId");

        if (weaponIdProperty == null)
        {
            payloadContainer.Add(new HelpBox("Switch Weapon Weapon Id field is missing.", HelpBoxMessageType.Warning));
            return;
        }

        SerializedProperty scalingRulesProperty = weaponIdProperty.serializedObject != null
            ? weaponIdProperty.serializedObject.FindProperty("scalingRules")
            : null;
        string weaponIdTooltip = "Selects a defined Weapon Id from the scoped Player Visual Preset. <Use Visual Default> keeps the preset default attachment. Add Scaling token formulas remain supported.";
        VisualElement weaponIdField = PlayerWeaponIdSelectorUtility.CreateScalableSelector(weaponIdProperty,
                                                                                           scalingRulesProperty,
                                                                                           "Weapon Id",
                                                                                           weaponIdTooltip,
                                                                                           PlayerWeaponIdSelectorUtility.UseVisualDefaultLabel,
                                                                                           () => PlayerWeaponIdSelectorUtility.BuildScopedSwitchWeaponOptions(weaponIdProperty));
        payloadContainer.Add(weaponIdField);

        HelpBox behaviorBox = new HelpBox("Base Gun remains visible. Switch Weapon selects the mountable visual and shoot animation owned by the matching Weapon Id on the active Player Visual Preset.",
                                          HelpBoxMessageType.Info);
        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(behaviorBox);
        payloadContainer.Add(warningBox);

        payloadContainer.TrackPropertyValue(weaponIdProperty, changedProperty =>
        {
            RefreshWarning(changedProperty, warningBox);
        });
        RefreshWarning(weaponIdProperty, warningBox);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Shows warnings for empty and oversized Weapon Id values without mutating authored data.
    /// </summary>
    /// <param name="weaponIdProperty">Serialized defined Weapon Id.</param>
    /// <param name="warningBox">Warning box updated in place.</param>
    private static void RefreshWarning(SerializedProperty weaponIdProperty, HelpBox warningBox)
    {
        string weaponId = weaponIdProperty != null ? weaponIdProperty.stringValue : string.Empty;

        List<string> availableWeaponIds = PlayerWeaponIdSelectorUtility.BuildScopedSwitchWeaponOptions(weaponIdProperty);

        if (availableWeaponIds.Count <= 0)
        {
            ShowWarning(warningBox, "No mountable Weapon Id is available from the scoped or registered Gameplay Visual Presets.");
            return;
        }

        if (string.IsNullOrWhiteSpace(weaponId))
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        if (Encoding.UTF8.GetByteCount(weaponId.Trim()) > PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
        {
            ShowWarning(warningBox, "Weapon Id exceeds the ECS fixed-string capacity and cannot be baked.");
            return;
        }

        if (!PlayerWeaponIdSelectorUtility.ContainsWeaponId(availableWeaponIds, weaponId.Trim()))
        {
            ShowWarning(warningBox, "Weapon Id does not match any mountable entry in the registered Gameplay Visual Presets.");
            return;
        }

        warningBox.text = string.Empty;
        warningBox.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Displays one warning message in the reusable payload HelpBox.
    /// </summary>
    /// <param name="warningBox">Warning box updated in place.</param>
    /// <param name="message">Warning text shown to designers.</param>
    private static void ShowWarning(HelpBox warningBox, string message)
    {
        warningBox.text = message;
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
