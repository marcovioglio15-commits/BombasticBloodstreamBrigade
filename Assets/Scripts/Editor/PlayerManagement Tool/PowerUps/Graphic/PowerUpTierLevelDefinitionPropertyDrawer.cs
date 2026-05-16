using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one tier definition and warns when its entry percentages do not sum to 100%.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpTierLevelDefinition))]
public sealed class PowerUpTierLevelDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float PercentageWarningTolerance = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one tier definition.
    /// </summary>
    /// <param name="property">Serialized tier definition property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty tierIdProperty = property.FindPropertyRelative("tierId");
        SerializedProperty entriesProperty = property.FindPropertyRelative("entries");

        if (tierIdProperty == null || entriesProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Tier level fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PropertyField tierIdField = new PropertyField(tierIdProperty, "Tier ID");
        tierIdField.BindProperty(tierIdProperty);
        root.Add(tierIdField);

        PropertyField entriesField = new PropertyField(entriesProperty, "Entries");
        entriesField.BindProperty(entriesProperty);
        root.Add(entriesField);

        HelpBox percentageWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(percentageWarningBox);

        HelpBox duplicateWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(duplicateWarningBox);

        void RefreshWarnings()
        {
            float totalPercentage = CalculateEntryPercentageSum(entriesProperty);

            if (Mathf.Abs(totalPercentage - 100f) <= PercentageWarningTolerance)
                percentageWarningBox.style.display = DisplayStyle.None;
            else
            {
                percentageWarningBox.text = string.Format(CultureInfo.InvariantCulture,
                                                          "Tier entry percentages currently sum to {0:0.##}%. Set the total to 100% to match the intended extraction odds.",
                                                          totalPercentage);
                percentageWarningBox.style.display = DisplayStyle.Flex;
            }

            string duplicateWarning = BuildDuplicateEntryWarning(entriesProperty);

            if (string.IsNullOrWhiteSpace(duplicateWarning))
            {
                duplicateWarningBox.style.display = DisplayStyle.None;
                return;
            }

            duplicateWarningBox.text = duplicateWarning;
            duplicateWarningBox.style.display = DisplayStyle.Flex;
        }

        root.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshWarnings());
        RefreshWarnings();
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Sums all non-negative entry percentages configured inside the serialized tier entry array.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array belonging to the tier definition.</param>
    /// <returns>Total configured percentage for the current tier.</returns>
    private static float CalculateEntryPercentageSum(SerializedProperty entriesProperty)
    {
        if (entriesProperty == null || !entriesProperty.isArray)
            return 0f;

        float totalPercentage = 0f;

        for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);

            if (entryProperty == null)
                continue;

            SerializedProperty selectionWeightProperty = entryProperty.FindPropertyRelative("selectionWeight");

            if (selectionWeightProperty == null)
                continue;

            totalPercentage += Mathf.Max(0f, selectionWeightProperty.floatValue);
        }

        return totalPercentage;
    }

    /// <summary>
    /// Builds a warning message when the same typed power-up entry appears more than once inside one tier.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array belonging to the tier definition.</param>
    /// <returns>Warning text when duplicates are detected; otherwise an empty string.</returns>
    private static string BuildDuplicateEntryWarning(SerializedProperty entriesProperty)
    {
        if (entriesProperty == null || !entriesProperty.isArray)
            return string.Empty;

        Dictionary<string, int> occurrenceCountByEntryKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<string> duplicateEntryLabels = new List<string>();

        for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);

            if (entryProperty == null)
                continue;

            SerializedProperty entryKindProperty = entryProperty.FindPropertyRelative("entryKind");
            SerializedProperty powerUpIdProperty = entryProperty.FindPropertyRelative("powerUpId");
            PowerUpTierEntryKind entryKind = ResolveEntryKind(entryKindProperty);
            string powerUpId = ResolveNormalizedStringValue(powerUpIdProperty);

            if (string.IsNullOrWhiteSpace(powerUpId))
                continue;

            string duplicateKey = string.Format("{0}|{1}", (int)entryKind, powerUpId);

            if (occurrenceCountByEntryKey.TryGetValue(duplicateKey, out int occurrenceCount))
            {
                occurrenceCountByEntryKey[duplicateKey] = occurrenceCount + 1;

                if (occurrenceCount == 1)
                    duplicateEntryLabels.Add(string.Format("{0}:{1}", entryKind, powerUpId));

                continue;
            }

            occurrenceCountByEntryKey.Add(duplicateKey, 1);
        }

        if (duplicateEntryLabels.Count <= 0)
            return string.Empty;

        return string.Format("Duplicate tier entries detected: {0}. Entries are preserved and rolled independently, so repeated power-ups increase their effective weight.",
                             string.Join(", ", duplicateEntryLabels));
    }

    /// <summary>
    /// Resolves one serialized tier-entry kind while guarding against invalid enum payloads.
    /// </summary>
    /// <param name="entryKindProperty">Serialized enum property storing the tier-entry kind.</param>
    /// <returns>Resolved tier-entry kind or Active when the payload is invalid.</returns>
    private static PowerUpTierEntryKind ResolveEntryKind(SerializedProperty entryKindProperty)
    {
        if (entryKindProperty == null || entryKindProperty.propertyType != SerializedPropertyType.Enum)
            return PowerUpTierEntryKind.Active;

        int enumValue = entryKindProperty.enumValueIndex;

        if (!Enum.IsDefined(typeof(PowerUpTierEntryKind), enumValue))
            return PowerUpTierEntryKind.Active;

        return (PowerUpTierEntryKind)enumValue;
    }

    /// <summary>
    /// Returns one trimmed string value from a serialized property when available.
    /// </summary>
    /// <param name="stringProperty">Serialized string property to normalize.</param>
    /// <returns>Trimmed string value or an empty string when unavailable.</returns>
    private static string ResolveNormalizedStringValue(SerializedProperty stringProperty)
    {
        if (stringProperty == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(stringProperty.stringValue)
            ? string.Empty
            : stringProperty.stringValue.Trim();
    }
    #endregion

    #endregion
}
