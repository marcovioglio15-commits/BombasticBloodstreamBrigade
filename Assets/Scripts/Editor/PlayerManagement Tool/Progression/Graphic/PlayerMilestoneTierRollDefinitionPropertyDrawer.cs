using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one milestone tier-roll entry with enum-style tier selection sourced from power-ups presets.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerMilestoneTierRollDefinition))]
public sealed class PlayerMilestoneTierRollDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one milestone tier-roll entry.
    /// </summary>
    /// <param name="property">Serialized milestone tier-roll property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty tierIdProperty = property.FindPropertyRelative("tierId");
        SerializedProperty selectionPercentageProperty = property.FindPropertyRelative("selectionPercentage");

        if (tierIdProperty == null || selectionPercentageProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone tier roll fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        List<string> tierIdOptions = PlayerProgressionTierOptionsUtility.BuildTierIdOptionsFromPowerUpsLibrary();

        if (tierIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Power-Up tier IDs found. Create tiers in a Power-Ups preset first.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
            TextField tierIdPreviewField = new TextField("Tier ID");
            tierIdPreviewField.value = "<No tiers available>";
            tierIdPreviewField.SetEnabled(false);
            root.Add(tierIdPreviewField);
        }
        else
        {
            string currentTierId = string.IsNullOrWhiteSpace(tierIdProperty.stringValue)
                ? string.Empty
                : tierIdProperty.stringValue.Trim();
            List<string> popupOptions = new List<string>(tierIdOptions);
            string selectedTierId = ResolveSelectedTierId(tierIdOptions, currentTierId);

            if (string.IsNullOrWhiteSpace(currentTierId))
            {
                tierIdProperty.serializedObject.Update();
                tierIdProperty.stringValue = selectedTierId;
                tierIdProperty.serializedObject.ApplyModifiedProperties();
            }
            else if (!ContainsTierIdOption(tierIdOptions, currentTierId))
            {
                popupOptions.Insert(0, currentTierId);
                selectedTierId = currentTierId;

                HelpBox invalidTierWarningBox = new HelpBox("The current Tier ID is not available in the active Power-Ups preset. Choose a valid tier to replace this legacy or missing reference.", HelpBoxMessageType.Warning);
                root.Add(invalidTierWarningBox);
            }

            PopupField<string> tierIdPopup = new PopupField<string>("Tier ID", popupOptions, selectedTierId);
            tierIdPopup.RegisterValueChangedCallback(evt =>
            {
                tierIdProperty.serializedObject.Update();
                tierIdProperty.stringValue = evt.newValue;
                tierIdProperty.serializedObject.ApplyModifiedProperties();
            });
            root.Add(tierIdPopup);
        }

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement selectionPercentageField = PlayerScalingFieldElementFactory.CreateField(selectionPercentageProperty,
                                                                                              scalingRulesProperty,
                                                                                              "Selection Percentage (%)");
        root.Add(selectionPercentageField);

        return root;
    }
    #endregion

    #region Private Methods
    private static string ResolveSelectedTierId(List<string> options, string currentTierId)
    {
        if (options == null || options.Count <= 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(currentTierId))
            return options[0];

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            string option = options[optionIndex];

            if (string.Equals(option, currentTierId, System.StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }

    /// <summary>
    /// Checks whether the current serialized tier ID still exists in the active dropdown options.
    /// </summary>
    /// <param name="options">Available dropdown options resolved from the scoped power-ups preset.</param>
    /// <param name="currentTierId">Serialized tier ID currently stored by the milestone roll.</param>
    /// <returns>True when the current tier ID is still selectable; otherwise false.</returns>
    private static bool ContainsTierIdOption(List<string> options, string currentTierId)
    {
        if (options == null || options.Count <= 0 || string.IsNullOrWhiteSpace(currentTierId))
            return false;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex], currentTierId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
