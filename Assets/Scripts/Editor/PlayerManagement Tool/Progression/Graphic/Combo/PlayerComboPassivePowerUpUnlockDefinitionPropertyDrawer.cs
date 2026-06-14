using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one combo-rank passive power-up unlock entry with Add Scaling and scoped passive selection support.
/// none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerComboPassivePowerUpUnlockDefinition))]
public sealed class PlayerComboPassivePowerUpUnlockDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const string EmptySelectionLabel = "<Select Passive>";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one combo passive unlock entry.
    /// </summary>
    /// <param name="property">Serialized passive unlock property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty passivePowerUpIdProperty = property.FindPropertyRelative("passivePowerUpId");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (isEnabledProperty == null || passivePowerUpIdProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo passive unlock fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        root.Add(PlayerScalingFieldElementFactory.CreateField(isEnabledProperty,
                                                              scalingRulesProperty,
                                                              "Enabled"));

        VisualElement enabledContent = new VisualElement();
        root.Add(enabledContent);
        enabledContent.Add(PlayerScalingFieldElementFactory.CreateField(passivePowerUpIdProperty,
                                                                        scalingRulesProperty,
                                                                        "Passive PowerUpId",
                                                                        null,
                                                                        true));
        BuildPassivePowerUpPopup(enabledContent, passivePowerUpIdProperty);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        enabledContent.Add(warningBox);

        RefreshEnabledContent(isEnabledProperty, passivePowerUpIdProperty, enabledContent, warningBox);
        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshEnabledContent(isEnabledProperty, passivePowerUpIdProperty, enabledContent, warningBox);
        });
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes scoped selector visibility and validation warnings for the enabled passive unlock entry.
    /// </summary>
    /// <param name="isEnabledProperty">Serialized enable flag.</param>
    /// <param name="passivePowerUpIdProperty">Serialized passive PowerUpId.</param>
    /// <param name="enabledContent">Container hidden when the unlock is disabled.</param>
    /// <param name="warningBox">Warning element refreshed in place.</param>
    private static void RefreshEnabledContent(SerializedProperty isEnabledProperty,
                                              SerializedProperty passivePowerUpIdProperty,
                                              VisualElement enabledContent,
                                              HelpBox warningBox)
    {
        if (enabledContent == null || warningBox == null)
        {
            return;
        }

        bool isEnabled = isEnabledProperty == null || isEnabledProperty.boolValue;
        enabledContent.style.display = isEnabled ? DisplayStyle.Flex : DisplayStyle.None;

        if (!isEnabled)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        List<string> passivePowerUpIds = BuildScopedPassivePowerUpIdOptions();
        string passivePowerUpId = passivePowerUpIdProperty != null && !string.IsNullOrWhiteSpace(passivePowerUpIdProperty.stringValue)
            ? passivePowerUpIdProperty.stringValue.Trim()
            : string.Empty;
        List<string> warningLines = new List<string>();

        if (passivePowerUpIds.Count <= 0)
        {
            warningLines.Add("No passive PowerUpId is available from the scoped Power-Ups preset.");
        }
        else if (!string.IsNullOrWhiteSpace(passivePowerUpId) && !ContainsPassivePowerUpId(passivePowerUpIds, passivePowerUpId))
        {
            warningLines.Add(string.Format("Passive PowerUpId '{0}' is not available in the scoped Power-Ups preset.", passivePowerUpId));
        }

        if (string.IsNullOrWhiteSpace(passivePowerUpId))
        {
            warningLines.Add("Enabled passive unlocks need a Passive PowerUpId.");
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
    /// Adds a scoped passive PowerUpId picker that writes the same token field rendered with Add Scaling support.
    /// </summary>
    /// <param name="parent">Container receiving the popup.</param>
    /// <param name="passivePowerUpIdProperty">Serialized passive PowerUpId property.</param>
    private static void BuildPassivePowerUpPopup(VisualElement parent, SerializedProperty passivePowerUpIdProperty)
    {
        if (parent == null || passivePowerUpIdProperty == null)
        {
            return;
        }

        List<string> passivePowerUpIds = BuildScopedPassivePowerUpIdOptions();

        if (passivePowerUpIds.Count <= 0)
        {
            return;
        }

        List<string> popupOptions = new List<string>();
        string currentPassivePowerUpId = string.IsNullOrWhiteSpace(passivePowerUpIdProperty.stringValue)
            ? string.Empty
            : passivePowerUpIdProperty.stringValue.Trim();
        string selectedOption = EmptySelectionLabel;
        popupOptions.Add(EmptySelectionLabel);

        for (int optionIndex = 0; optionIndex < passivePowerUpIds.Count; optionIndex++)
        {
            string option = passivePowerUpIds[optionIndex];
            popupOptions.Add(option);

            if (string.Equals(option, currentPassivePowerUpId, StringComparison.OrdinalIgnoreCase))
            {
                selectedOption = option;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentPassivePowerUpId) &&
            string.Equals(selectedOption, EmptySelectionLabel, StringComparison.Ordinal))
        {
            popupOptions.Insert(1, currentPassivePowerUpId);
            selectedOption = currentPassivePowerUpId;
        }

        PopupField<string> passivePowerUpPopup = new PopupField<string>("Pick Passive", popupOptions, selectedOption);
        passivePowerUpPopup.tooltip = "Selects a passive PowerUpId from the scoped Power-Ups preset and writes it into the scalable Passive PowerUpId token field.";
        passivePowerUpPopup.RegisterValueChangedCallback(evt =>
        {
            string resolvedPowerUpId = string.Equals(evt.newValue, EmptySelectionLabel, StringComparison.Ordinal)
                ? string.Empty
                : evt.newValue;
            passivePowerUpIdProperty.serializedObject.Update();
            passivePowerUpIdProperty.stringValue = resolvedPowerUpId;
            passivePowerUpIdProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        parent.Add(passivePowerUpPopup);
    }

    /// <summary>
    /// Builds a scoped passive PowerUpId option list from the active Power-Ups preset context.
    /// none.
    /// </summary>
    /// <returns>Sorted passive PowerUpId options.</returns>
    private static List<string> BuildScopedPassivePowerUpIdOptions()
    {
        List<string> passivePowerUpIds = new List<string>();

        if (!PlayerProgressionTierOptionsUtility.TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset scopedPreset))
        {
            return passivePowerUpIds;
        }

        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = scopedPreset.PassivePowerUps;

        if (passivePowerUps == null)
        {
            return passivePowerUpIds;
        }

        HashSet<string> visitedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int powerUpIndex = 0; powerUpIndex < passivePowerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition passivePowerUp = passivePowerUps[powerUpIndex];

            if (passivePowerUp == null || passivePowerUp.CommonData == null || string.IsNullOrWhiteSpace(passivePowerUp.CommonData.PowerUpId))
            {
                continue;
            }

            string passivePowerUpId = passivePowerUp.CommonData.PowerUpId.Trim();

            if (!visitedIds.Add(passivePowerUpId))
            {
                continue;
            }

            passivePowerUpIds.Add(passivePowerUpId);
        }

        passivePowerUpIds.Sort(StringComparer.OrdinalIgnoreCase);
        return passivePowerUpIds;
    }

    /// <summary>
    /// Checks whether one passive PowerUpId exists in a case-insensitive option list.
    /// </summary>
    /// <param name="passivePowerUpIds">Available passive PowerUpId options.</param>
    /// <param name="passivePowerUpId">Requested passive PowerUpId.</param>
    /// <returns>True when the requested PowerUpId exists in the option list.</returns>
    private static bool ContainsPassivePowerUpId(IReadOnlyList<string> passivePowerUpIds, string passivePowerUpId)
    {
        if (passivePowerUpIds == null || string.IsNullOrWhiteSpace(passivePowerUpId))
        {
            return false;
        }

        for (int optionIndex = 0; optionIndex < passivePowerUpIds.Count; optionIndex++)
        {
            if (!string.Equals(passivePowerUpIds[optionIndex], passivePowerUpId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
