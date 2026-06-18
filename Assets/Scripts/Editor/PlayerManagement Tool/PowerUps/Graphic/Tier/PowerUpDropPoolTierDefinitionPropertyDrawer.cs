using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Draws one drop-pool tier candidate with tier selection constrained to the current power-ups preset.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpDropPoolTierDefinition))]
public sealed class PowerUpDropPoolTierDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one drop-pool tier candidate.
    /// </summary>
    /// <param name="property">Serialized drop-pool tier property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty tierIdProperty = property.FindPropertyRelative("tierId");
        SerializedProperty selectionPercentageProperty = property.FindPropertyRelative("selectionPercentage");

        if (tierIdProperty == null || selectionPercentageProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Drop-pool tier fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        List<string> tierIdOptions = PowerUpTierOptionsUtility.BuildTierIdOptions(property.serializedObject);

        if (tierIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Tier IDs found in this Power-Ups preset. Create at least one tier first.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
            TextField previewField = new TextField("Tier ID");
            previewField.value = "<No tiers available>";
            previewField.SetEnabled(false);
            root.Add(previewField);
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
                HelpBox invalidTierWarningBox = new HelpBox("The current Tier ID is not available in this Power-Ups preset. Choose a valid tier to replace the missing reference.", HelpBoxMessageType.Warning);
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
    /// Checks whether the current serialized tier ID still exists in the local preset dropdown options.
    /// </summary>
    /// <param name="options">Available dropdown options resolved from the owning power-ups preset.</param>
    /// <param name="currentTierId">Serialized tier ID currently stored by the drop-pool candidate.</param>
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
