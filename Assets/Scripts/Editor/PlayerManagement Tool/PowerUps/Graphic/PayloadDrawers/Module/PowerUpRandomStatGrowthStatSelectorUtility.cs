using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds a scalable popup limited to numeric custom player statistics.
/// </summary>
internal static class PowerUpRandomStatGrowthStatSelectorUtility
{
    #region Constants
    private const string EmptyLabel = "<Select Numeric Stat>";
    private const string MissingPrefix = "<Missing> ";
    #endregion

    #region Fields
    private static readonly List<PlayerConditionalWeaponSwitchStatOption> cachedNumericOptions =
        new List<PlayerConditionalWeaponSwitchStatOption>();
    private static bool numericOptionsCacheValid;
    #endregion

    #region Constructors
    /// <summary>
    /// Invalidates shared numeric options only when the selected progression scope changes.
    /// </summary>
    static PowerUpRandomStatGrowthStatSelectorUtility()
    {
        PlayerManagementSelectionContext.ContextChanged += InvalidateNumericOptions;
        PlayerManagementSelectionContext.ProgressionPresetContentChanged += InvalidateNumericOptions;
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates an enum-like custom-stat selector while retaining token Add Scaling controls.
    /// </summary>
    /// <param name="statNameProperty">Serialized custom scalable-stat identifier.</param>
    /// <param name="scalingRulesProperty">Serialized unified scaling-rule collection.</param>
    /// <returns>Scalable selector root.</returns>
    public static VisualElement Create(SerializedProperty statNameProperty,
                                       SerializedProperty scalingRulesProperty)
    {
        if (statNameProperty == null)
            return new HelpBox("Custom scalable-stat field is missing.", HelpBoxMessageType.Warning);

        VisualElement scalableField = PlayerScalingFieldElementFactory.CreateField(statNameProperty,
                                                                                    scalingRulesProperty,
                                                                                    "Custom Scalable Stat",
                                                                                    null,
                                                                                    true);
        scalableField.tooltip = "Numeric scalable stat increased when this candidate is selected.";
        VisualElement popupContainer = new VisualElement();
        popupContainer.style.flexGrow = 1f;
        PropertyField generatedValueField = scalableField.Q<PropertyField>();

        if (generatedValueField != null && generatedValueField.parent != null)
        {
            VisualElement valueRow = generatedValueField.parent;
            int fieldIndex = valueRow.IndexOf(generatedValueField);
            generatedValueField.style.display = DisplayStyle.None;
            valueRow.Insert(fieldIndex, popupContainer);
        }
        else
        {
            scalableField.Insert(0, popupContainer);
        }

        Action rebuild = () => Rebuild(popupContainer, statNameProperty);
        scalableField.TrackPropertyValue(statNameProperty, changedProperty => rebuild());
        scalableField.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged += rebuild;
            PlayerManagementSelectionContext.ProgressionPresetContentChanged += rebuild;
        });
        scalableField.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged -= rebuild;
            PlayerManagementSelectionContext.ProgressionPresetContentChanged -= rebuild;
        });
        rebuild();
        return scalableField;
    }

    /// <summary>
    /// Reports whether one stat name currently resolves to a numeric scalable stat.
    /// </summary>
    /// <param name="statName">Stat identifier to validate.</param>
    /// <returns>True when the scoped progression preset exposes a matching numeric stat.</returns>
    public static bool ContainsNumericStat(string statName)
    {
        List<PlayerConditionalWeaponSwitchStatOption> options = BuildNumericOptions();

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex].StatName, statName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #region Construction
    /// <summary>
    /// Rebuilds popup choices from the current progression-preset scope.
    /// </summary>
    /// <param name="container">Popup host.</param>
    /// <param name="statNameProperty">Serialized stat identifier.</param>
    private static void Rebuild(VisualElement container, SerializedProperty statNameProperty)
    {
        if (container == null || statNameProperty == null)
            return;

        container.Clear();
        List<PlayerConditionalWeaponSwitchStatOption> options = BuildNumericOptions();
        List<string> displayOptions = new List<string>(options.Count + 2) { EmptyLabel };
        List<string> serializedOptions = new List<string>(options.Count + 2) { string.Empty };
        string currentValue = string.IsNullOrWhiteSpace(statNameProperty.stringValue)
            ? string.Empty
            : statNameProperty.stringValue.Trim();
        int selectedIndex = 0;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            PlayerConditionalWeaponSwitchStatOption option = options[optionIndex];
            displayOptions.Add(string.Format("{0} ({1})", option.StatName, option.StatType));
            serializedOptions.Add(option.StatName);

            if (string.Equals(option.StatName, currentValue, StringComparison.Ordinal))
                selectedIndex = serializedOptions.Count - 1;
        }

        if (!string.IsNullOrWhiteSpace(currentValue) && selectedIndex == 0)
        {
            displayOptions.Insert(1, MissingPrefix + currentValue);
            serializedOptions.Insert(1, currentValue);
            selectedIndex = 1;
        }

        PopupField<string> popup = new PopupField<string>("Custom Scalable Stat", displayOptions, selectedIndex);
        popup.tooltip = "Numeric scalable stat increased when this candidate is selected.";
        popup.style.flexGrow = 1f;
        popup.RegisterValueChangedCallback(evt =>
        {
            int optionIndex = displayOptions.IndexOf(evt.newValue);

            if (optionIndex < 0 || optionIndex >= serializedOptions.Count)
                return;

            string selectedValue = serializedOptions[optionIndex];

            if (string.Equals(statNameProperty.stringValue, selectedValue, StringComparison.Ordinal))
                return;

            if (statNameProperty.serializedObject.targetObject != null)
                Undo.RecordObject(statNameProperty.serializedObject.targetObject, "Change Random Growth Stat");

            statNameProperty.serializedObject.Update();
            statNameProperty.stringValue = selectedValue;
            statNameProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        container.Add(popup);
    }

    /// <summary>
    /// Filters the shared progression-stat options to types supported by additive growth.
    /// </summary>
    /// <returns>Ordered numeric stat options.</returns>
    private static List<PlayerConditionalWeaponSwitchStatOption> BuildNumericOptions()
    {
        if (numericOptionsCacheValid)
            return cachedNumericOptions;

        List<PlayerConditionalWeaponSwitchStatOption> allOptions =
            PlayerConditionalWeaponSwitchStatSelectorUtility.BuildScopedStatOptions();
        cachedNumericOptions.Clear();

        for (int optionIndex = 0; optionIndex < allOptions.Count; optionIndex++)
        {
            PlayerScalableStatType statType = allOptions[optionIndex].StatType;

            if (statType == PlayerScalableStatType.Float ||
                statType == PlayerScalableStatType.Integer ||
                statType == PlayerScalableStatType.Unsigned)
            {
                cachedNumericOptions.Add(allOptions[optionIndex]);
            }
        }

        numericOptionsCacheValid = true;
        return cachedNumericOptions;
    }

    /// <summary>
    /// Marks the shared option cache stale after the progression selection or its stat content changes.
    /// </summary>
    private static void InvalidateNumericOptions()
    {
        numericOptionsCacheValid = false;
    }
    #endregion

    #endregion
}
