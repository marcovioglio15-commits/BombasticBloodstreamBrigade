using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one drop-pool definition and warns when its tier percentages do not sum to 100%.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpDropPoolDefinition))]
public sealed class PowerUpDropPoolDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float PercentageWarningTolerance = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one drop-pool definition.
    /// </summary>
    /// <param name="property">Serialized drop-pool definition property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty poolIdProperty = property.FindPropertyRelative("poolId");
        SerializedProperty tierRollsProperty = property.FindPropertyRelative("tierRolls");

        if (poolIdProperty == null || tierRollsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Drop-pool fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PropertyField poolIdField = new PropertyField(poolIdProperty, "Pool ID");
        poolIdField.BindProperty(poolIdProperty);
        root.Add(poolIdField);

        PropertyField tierRollsField = new PropertyField(tierRollsProperty, "Tier Rolls");
        tierRollsField.BindProperty(tierRollsProperty);
        root.Add(tierRollsField);

        HelpBox percentageWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(percentageWarningBox);

        HelpBox duplicateWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(duplicateWarningBox);

        void RefreshWarnings()
        {
            float totalPercentage = CalculateTierRollPercentageSum(tierRollsProperty);

            if (Mathf.Abs(totalPercentage - 100f) <= PercentageWarningTolerance)
                percentageWarningBox.style.display = DisplayStyle.None;
            else
            {
                percentageWarningBox.text = string.Format(CultureInfo.InvariantCulture,
                                                          "Drop-pool tier percentages currently sum to {0:0.##}%. Set the total to 100% to match the intended extraction odds.",
                                                          totalPercentage);
                percentageWarningBox.style.display = DisplayStyle.Flex;
            }

            string duplicateWarning = BuildDuplicateTierWarning(tierRollsProperty);

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
    /// Sums all non-negative tier percentages configured inside the serialized drop-pool tier array.
    /// </summary>
    /// <param name="tierRollsProperty">Serialized tier-roll array belonging to the drop-pool definition.</param>
    /// <returns>Total configured percentage for the current drop pool.</returns>
    private static float CalculateTierRollPercentageSum(SerializedProperty tierRollsProperty)
    {
        if (tierRollsProperty == null || !tierRollsProperty.isArray)
            return 0f;

        float totalPercentage = 0f;

        for (int tierRollIndex = 0; tierRollIndex < tierRollsProperty.arraySize; tierRollIndex++)
        {
            SerializedProperty tierRollProperty = tierRollsProperty.GetArrayElementAtIndex(tierRollIndex);

            if (tierRollProperty == null)
                continue;

            SerializedProperty selectionPercentageProperty = tierRollProperty.FindPropertyRelative("selectionPercentage");

            if (selectionPercentageProperty == null)
                continue;

            totalPercentage += Mathf.Max(0f, selectionPercentageProperty.floatValue);
        }

        return totalPercentage;
    }

    /// <summary>
    /// Builds a warning message when the same Tier ID appears more than once inside one drop pool.
    /// </summary>
    /// <param name="tierRollsProperty">Serialized tier-roll array belonging to the drop-pool definition.</param>
    /// <returns>Warning text when duplicates are detected; otherwise an empty string.</returns>
    private static string BuildDuplicateTierWarning(SerializedProperty tierRollsProperty)
    {
        if (tierRollsProperty == null || !tierRollsProperty.isArray)
            return string.Empty;

        Dictionary<string, int> occurrenceCountByTierId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<string> duplicateTierIds = new List<string>();

        for (int tierRollIndex = 0; tierRollIndex < tierRollsProperty.arraySize; tierRollIndex++)
        {
            SerializedProperty tierRollProperty = tierRollsProperty.GetArrayElementAtIndex(tierRollIndex);

            if (tierRollProperty == null)
                continue;

            SerializedProperty tierIdProperty = tierRollProperty.FindPropertyRelative("tierId");
            string tierId = ResolveNormalizedStringValue(tierIdProperty);

            if (string.IsNullOrWhiteSpace(tierId))
                continue;

            if (occurrenceCountByTierId.TryGetValue(tierId, out int occurrenceCount))
            {
                occurrenceCountByTierId[tierId] = occurrenceCount + 1;

                if (occurrenceCount == 1)
                    duplicateTierIds.Add(tierId);

                continue;
            }

            occurrenceCountByTierId.Add(tierId, 1);
        }

        if (duplicateTierIds.Count <= 0)
            return string.Empty;

        return string.Format("Duplicate Tier IDs detected in this drop pool: {0}. Entries are preserved and rolled independently, so repeated tiers increase their effective weight.",
                             string.Join(", ", duplicateTierIds));
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
