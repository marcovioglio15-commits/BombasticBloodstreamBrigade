using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Draws one milestone power-up extraction entry using one scoped drop-pool selector.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerMilestonePowerUpUnlockDefinition))]
public sealed class PlayerMilestonePowerUpUnlockDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const string EmptySelectionLabel = "<Select Pool ID>";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one milestone power-up extraction definition.
    /// </summary>
    /// <param name="property">Serialized milestone power-up extraction property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty dropPoolIdProperty = property.FindPropertyRelative("dropPoolId");
        SerializedProperty legacyTierRollsProperty = property.FindPropertyRelative("legacyTierRolls");

        if (dropPoolIdProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone power-up unlock fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        bool hasScopedPowerUpsPreset = PlayerProgressionTierOptionsUtility.TryResolveScopedPowerUpsPreset(out PlayerPowerUpsPreset _);
        List<string> dropPoolIdOptions = PlayerProgressionTierOptionsUtility.BuildDropPoolIdOptionsFromPowerUpsLibrary();

        if (!hasScopedPowerUpsPreset)
        {
            HelpBox missingPresetWarningBox = new HelpBox("No scoped Power-Ups preset could be resolved from the active Master Preset or from a Master Preset that references this progression preset.", HelpBoxMessageType.Warning);
            root.Add(missingPresetWarningBox);
        }

        if (dropPoolIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Pool IDs detected in the scoped Power-Ups preset. Configure Drop Pools & Tiers first.", HelpBoxMessageType.Warning);
            root.Add(warningBox);

            TextField previewField = new TextField("Drop Pool ID");
            previewField.value = string.IsNullOrWhiteSpace(dropPoolIdProperty.stringValue)
                ? "<No pools available>"
                : dropPoolIdProperty.stringValue;
            previewField.SetEnabled(false);
            root.Add(previewField);
        }
        else
        {
            BuildDropPoolPopup(root, dropPoolIdProperty, dropPoolIdOptions);
        }

        if (HasLegacyTierRolls(legacyTierRollsProperty))
        {
            HelpBox legacyWarningBox = new HelpBox("Legacy inline tier rolls are still serialized on this unlock. Runtime keeps them only as compatibility fallback until you assign a valid Drop Pool ID.", HelpBoxMessageType.Warning);
            root.Add(legacyWarningBox);
        }

        if (string.IsNullOrWhiteSpace(dropPoolIdProperty.stringValue))
        {
            HelpBox missingSelectionBox = new HelpBox("This milestone unlock has no Drop Pool ID selected yet, so the scoped pool configuration will not drive its roll until you choose one.", HelpBoxMessageType.Warning);
            root.Add(missingSelectionBox);
        }

        return root;
    }
    #endregion

    #region Private Methods
    private static void BuildDropPoolPopup(VisualElement root,
                                           SerializedProperty dropPoolIdProperty,
                                           System.Collections.Generic.List<string> dropPoolIdOptions)
    {
        string currentDropPoolId = string.IsNullOrWhiteSpace(dropPoolIdProperty.stringValue)
            ? string.Empty
            : dropPoolIdProperty.stringValue.Trim();
        System.Collections.Generic.List<string> popupOptions = new System.Collections.Generic.List<string>();
        string selectedOption = EmptySelectionLabel;

        if (string.IsNullOrWhiteSpace(currentDropPoolId))
        {
            popupOptions.Add(EmptySelectionLabel);

            for (int optionIndex = 0; optionIndex < dropPoolIdOptions.Count; optionIndex++)
                popupOptions.Add(dropPoolIdOptions[optionIndex]);
        }
        else if (ContainsDropPoolId(dropPoolIdOptions, currentDropPoolId))
        {
            for (int optionIndex = 0; optionIndex < dropPoolIdOptions.Count; optionIndex++)
                popupOptions.Add(dropPoolIdOptions[optionIndex]);

            selectedOption = ResolveSelectedDropPoolId(dropPoolIdOptions, currentDropPoolId);
        }
        else
        {
            popupOptions.Add(currentDropPoolId);

            for (int optionIndex = 0; optionIndex < dropPoolIdOptions.Count; optionIndex++)
                popupOptions.Add(dropPoolIdOptions[optionIndex]);

            selectedOption = currentDropPoolId;
            HelpBox invalidSelectionBox = new HelpBox("The current Drop Pool ID is not available in the scoped Power-Ups preset. Choose a valid pool to replace this missing or legacy reference.", HelpBoxMessageType.Warning);
            root.Add(invalidSelectionBox);
        }

        PopupField<string> dropPoolPopup = new PopupField<string>("Drop Pool ID", popupOptions, selectedOption);
        dropPoolPopup.tooltip = "Select the scoped drop pool used by this milestone offer roll.";
        dropPoolPopup.RegisterValueChangedCallback(evt =>
        {
            string resolvedValue = string.Equals(evt.newValue, EmptySelectionLabel, System.StringComparison.Ordinal)
                ? string.Empty
                : evt.newValue;

            dropPoolIdProperty.serializedObject.Update();
            dropPoolIdProperty.stringValue = resolvedValue;
            dropPoolIdProperty.serializedObject.ApplyModifiedProperties();
        });
        root.Add(dropPoolPopup);
    }

    private static bool HasLegacyTierRolls(SerializedProperty legacyTierRollsProperty)
    {
        if (legacyTierRollsProperty == null || !legacyTierRollsProperty.isArray)
            return false;

        return legacyTierRollsProperty.arraySize > 0;
    }

    private static string ResolveSelectedDropPoolId(System.Collections.Generic.List<string> options, string currentDropPoolId)
    {
        if (options == null || options.Count <= 0)
            return string.Empty;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex], currentDropPoolId, System.StringComparison.OrdinalIgnoreCase))
                return options[optionIndex];
        }

        return options[0];
    }

    private static bool ContainsDropPoolId(System.Collections.Generic.List<string> options, string currentDropPoolId)
    {
        if (options == null || options.Count <= 0 || string.IsNullOrWhiteSpace(currentDropPoolId))
            return false;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex], currentDropPoolId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
