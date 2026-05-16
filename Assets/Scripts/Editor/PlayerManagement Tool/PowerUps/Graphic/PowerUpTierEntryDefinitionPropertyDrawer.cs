using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one tier entry with enum-style power-up ID selection sourced from modular power-up definitions.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpTierEntryDefinition))]
public sealed class PowerUpTierEntryDefinitionPropertyDrawer : PropertyDrawer
{
    #region Fields
    private static readonly List<PowerUpTierEntryKind> SupportedEntryKinds = new List<PowerUpTierEntryKind>
    {
        PowerUpTierEntryKind.Active,
        PowerUpTierEntryKind.Passive
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one tier entry.
    /// </summary>
    /// <param name="property">Serialized tier-entry property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty entryKindProperty = property.FindPropertyRelative("entryKind");
        SerializedProperty powerUpIdProperty = property.FindPropertyRelative("powerUpId");
        SerializedProperty selectionWeightProperty = property.FindPropertyRelative("selectionWeight");

        if (entryKindProperty == null || powerUpIdProperty == null || selectionWeightProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Tier entry fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PowerUpTierEntryKind initialEntryKind = ResolveEntryKind(entryKindProperty);
        PopupField<PowerUpTierEntryKind> entryKindPopup = new PopupField<PowerUpTierEntryKind>("Power-Up Type", SupportedEntryKinds, initialEntryKind);
        entryKindPopup.formatListItemCallback = FormatEntryKind;
        entryKindPopup.formatSelectedValueCallback = FormatEntryKind;
        root.Add(entryKindPopup);

        VisualElement powerUpIdContainer = new VisualElement();
        root.Add(powerUpIdContainer);
        SerializedObject serializedObject = property.serializedObject;
        string rootPropertyPath = property.propertyPath;

        void RebuildPowerUpIdSelector(SerializedProperty rootProperty)
        {
            powerUpIdContainer.Clear();

            if (rootProperty == null)
                return;

            SerializedProperty resolvedEntryKindProperty = rootProperty.FindPropertyRelative("entryKind");
            SerializedProperty resolvedPowerUpIdProperty = rootProperty.FindPropertyRelative("powerUpId");

            if (resolvedEntryKindProperty == null || resolvedPowerUpIdProperty == null)
            {
                HelpBox missingSelectorHelpBox = new HelpBox("Tier entry selector fields are missing.", HelpBoxMessageType.Warning);
                powerUpIdContainer.Add(missingSelectorHelpBox);
                return;
            }

            PowerUpTierEntryKind entryKind = ResolveEntryKind(resolvedEntryKindProperty);
            List<string> options = PowerUpTierOptionsUtility.BuildPowerUpIdOptions(rootProperty.serializedObject, entryKind);
            string currentPowerUpId = string.IsNullOrWhiteSpace(resolvedPowerUpIdProperty.stringValue)
                ? string.Empty
                : resolvedPowerUpIdProperty.stringValue.Trim();

            if (options.Count <= 0)
            {
                HelpBox missingOptionsHelpBox = new HelpBox("No modular power-up IDs available for this entry type.", HelpBoxMessageType.Warning);
                powerUpIdContainer.Add(missingOptionsHelpBox);

                if (!string.IsNullOrWhiteSpace(currentPowerUpId))
                {
                    TextField previewField = new TextField("Power-Up ID");
                    previewField.value = currentPowerUpId;
                    previewField.SetEnabled(false);
                    powerUpIdContainer.Add(previewField);
                }

                return;
            }

            List<string> popupOptions = new List<string>(options);
            string selectedPowerUpId = ResolveSelectedPowerUpId(options, currentPowerUpId);

            if (string.IsNullOrWhiteSpace(currentPowerUpId))
            {
                resolvedPowerUpIdProperty.serializedObject.Update();
                resolvedPowerUpIdProperty.stringValue = selectedPowerUpId;
                resolvedPowerUpIdProperty.serializedObject.ApplyModifiedProperties();
            }
            else if (!ContainsPowerUpIdOption(options, currentPowerUpId))
            {
                popupOptions.Insert(0, currentPowerUpId);
                selectedPowerUpId = currentPowerUpId;
                HelpBox invalidPowerUpWarningBox = new HelpBox("The current Power-Up ID is not available in the selected category. Choose a valid power-up to replace this missing or mismatched reference.", HelpBoxMessageType.Warning);
                powerUpIdContainer.Add(invalidPowerUpWarningBox);
            }

            PopupField<string> powerUpIdPopup = new PopupField<string>("Power-Up ID", popupOptions, selectedPowerUpId);
            powerUpIdPopup.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty resolvedRootProperty = TryResolveRootProperty(serializedObject, rootPropertyPath);

                if (resolvedRootProperty == null)
                    return;

                SerializedProperty targetPowerUpIdProperty = resolvedRootProperty.FindPropertyRelative("powerUpId");

                if (targetPowerUpIdProperty == null)
                    return;

                targetPowerUpIdProperty.serializedObject.Update();
                targetPowerUpIdProperty.stringValue = evt.newValue;
                targetPowerUpIdProperty.serializedObject.ApplyModifiedProperties();
            });
            powerUpIdContainer.Add(powerUpIdPopup);
        }

        entryKindPopup.RegisterValueChangedCallback(evt =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(serializedObject, rootPropertyPath);

            if (resolvedRootProperty == null)
                return;

            SerializedProperty resolvedEntryKindProperty = resolvedRootProperty.FindPropertyRelative("entryKind");
            SerializedProperty resolvedPowerUpIdProperty = resolvedRootProperty.FindPropertyRelative("powerUpId");

            if (resolvedEntryKindProperty == null || resolvedPowerUpIdProperty == null)
                return;

            List<string> powerUpIdOptions = PowerUpTierOptionsUtility.BuildPowerUpIdOptions(serializedObject, evt.newValue);
            string currentPowerUpId = string.IsNullOrWhiteSpace(resolvedPowerUpIdProperty.stringValue)
                ? string.Empty
                : resolvedPowerUpIdProperty.stringValue.Trim();
            string nextPowerUpId = currentPowerUpId;

            if (!ContainsPowerUpIdOption(powerUpIdOptions, currentPowerUpId))
                nextPowerUpId = ResolveSelectedPowerUpId(powerUpIdOptions, string.Empty);

            resolvedEntryKindProperty.serializedObject.Update();
            resolvedEntryKindProperty.enumValueIndex = (int)evt.newValue;
            resolvedPowerUpIdProperty.stringValue = nextPowerUpId;
            resolvedEntryKindProperty.serializedObject.ApplyModifiedProperties();

            RebuildPowerUpIdSelector(TryResolveRootProperty(serializedObject, rootPropertyPath));
        });

        root.TrackPropertyValue(entryKindProperty, changedProperty =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(changedProperty.serializedObject, rootPropertyPath);

            if (resolvedRootProperty == null)
                return;

            SerializedProperty resolvedEntryKindProperty = resolvedRootProperty.FindPropertyRelative("entryKind");

            if (resolvedEntryKindProperty == null)
                return;

            PowerUpTierEntryKind selectedEntryKind = ResolveEntryKind(resolvedEntryKindProperty);

            if (entryKindPopup.value != selectedEntryKind)
                entryKindPopup.SetValueWithoutNotify(selectedEntryKind);

            RebuildPowerUpIdSelector(resolvedRootProperty);
        });

        root.TrackPropertyValue(powerUpIdProperty, changedProperty =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(changedProperty.serializedObject, rootPropertyPath);
            RebuildPowerUpIdSelector(resolvedRootProperty);
        });

        RebuildPowerUpIdSelector(property);

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement weightField = PlayerScalingFieldElementFactory.CreateField(selectionWeightProperty,
                                                                                 scalingRulesProperty,
                                                                                 "Selection Percentage (%)");
        root.Add(weightField);

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Formats the tier-entry category label shown inside the popup.
    /// </summary>
    /// <param name="entryKind">Category value to format.</param>
    /// <returns>User-facing category label.</returns>
    private static string FormatEntryKind(PowerUpTierEntryKind entryKind)
    {
        switch (entryKind)
        {
            case PowerUpTierEntryKind.Passive:
                return "Passive";
            default:
                return "Active";
        }
    }

    /// <summary>
    /// Resolves the serialized root property again after UI callbacks or validation refreshes.
    /// </summary>
    /// <param name="serializedObject">Owning serialized object.</param>
    /// <param name="propertyPath">Serialized path of the tier entry.</param>
    /// <returns>Resolved root property when still available; otherwise null.</returns>
    private static SerializedProperty TryResolveRootProperty(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(propertyPath))
            return null;

        serializedObject.Update();
        return serializedObject.FindProperty(propertyPath);
    }

    /// <summary>
    /// Checks whether the current serialized power-up ID still exists in the local dropdown options.
    /// </summary>
    /// <param name="options">Available dropdown options resolved from the owning power-ups preset.</param>
    /// <param name="currentPowerUpId">Serialized power-up ID currently stored by the tier entry.</param>
    /// <returns>True when the current power-up ID is still selectable; otherwise false.</returns>
    private static bool ContainsPowerUpIdOption(List<string> options, string currentPowerUpId)
    {
        if (options == null || options.Count <= 0 || string.IsNullOrWhiteSpace(currentPowerUpId))
            return false;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex], currentPowerUpId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves one serialized tier-entry category while guarding against invalid enum payloads.
    /// </summary>
    /// <param name="entryKindProperty">Serialized enum property storing the tier-entry category.</param>
    /// <returns>Resolved tier-entry category or Active when the payload is invalid.</returns>
    private static PowerUpTierEntryKind ResolveEntryKind(SerializedProperty entryKindProperty)
    {
        if (entryKindProperty == null || entryKindProperty.propertyType != SerializedPropertyType.Enum)
            return PowerUpTierEntryKind.Active;

        int enumValue = entryKindProperty.enumValueIndex;

        if (!System.Enum.IsDefined(typeof(PowerUpTierEntryKind), enumValue))
            return PowerUpTierEntryKind.Active;

        return (PowerUpTierEntryKind)enumValue;
    }

    /// <summary>
    /// Resolves the power-up ID selected by default for the current dropdown options.
    /// </summary>
    /// <param name="options">Available power-up IDs resolved from the preset.</param>
    /// <param name="currentValue">Current serialized power-up ID.</param>
    /// <returns>Selected power-up ID to display or persist.</returns>
    private static string ResolveSelectedPowerUpId(List<string> options, string currentValue)
    {
        if (options == null || options.Count <= 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(currentValue))
            return options[0];

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            string option = options[optionIndex];

            if (string.Equals(option, currentValue, System.StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }
    #endregion

    #endregion
}
