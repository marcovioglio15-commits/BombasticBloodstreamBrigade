using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds enum-like editor selectors for defined Weapon Id string fields. The serialized value remains
/// a scalable token, while designers select authored options without typing IDs manually.
/// </summary>
internal static class PlayerWeaponIdSelectorUtility
{
    #region Constants
    public const string NoneLabel = "<None>";
    public const string UseVisualDefaultLabel = "<Use Visual Default>";
    private const string MissingPrefix = "<Missing> ";
    #endregion

    #region Methods

    #region UI Construction
    /// <summary>
    /// Creates a scalable token field whose base value is edited through an enum-like popup. The generated raw
    /// string field is hidden while Add Scaling and unified token-formula controls remain available.
    /// </summary>
    /// <param name="weaponIdProperty">Serialized Weapon Id token property.</param>
    /// <param name="scalingRulesProperty">Serialized Add Scaling rules array.</param>
    /// <param name="label">Popup label shown to designers.</param>
    /// <param name="tooltip">Explanatory popup and scalable-field tooltip.</param>
    /// <param name="emptyLabel">Display label mapped to an empty serialized Weapon Id.</param>
    /// <param name="optionsProvider">Callback rebuilding the currently available defined IDs.</param>
    /// <returns>Scalable selector root ready for insertion into the tool.</returns>
    public static VisualElement CreateScalableSelector(SerializedProperty weaponIdProperty,
                                                       SerializedProperty scalingRulesProperty,
                                                       string label,
                                                       string tooltip,
                                                       string emptyLabel,
                                                       Func<List<string>> optionsProvider)
    {
        if (weaponIdProperty == null)
            return new HelpBox("Weapon Id property is missing.", HelpBoxMessageType.Warning);

        VisualElement scalableField = PlayerScalingFieldElementFactory.CreateField(weaponIdProperty,
                                                                                    scalingRulesProperty,
                                                                                    label,
                                                                                    null,
                                                                                    true);
        scalableField.tooltip = tooltip;
        VisualElement popupContainer = new VisualElement();
        popupContainer.style.flexGrow = 1f;
        PropertyField generatedValueField = scalableField.Q<PropertyField>();

        if (generatedValueField != null && generatedValueField.parent != null)
        {
            VisualElement valueRow = generatedValueField.parent;
            int valueFieldIndex = valueRow.IndexOf(generatedValueField);
            generatedValueField.style.display = DisplayStyle.None;
            valueRow.Insert(valueFieldIndex, popupContainer);
        }
        else
        {
            popupContainer.style.marginBottom = 2f;
            scalableField.Insert(0, popupContainer);
        }

        Action rebuildSelector = () => RebuildSelector(popupContainer,
                                                       weaponIdProperty,
                                                       label,
                                                       tooltip,
                                                       emptyLabel,
                                                       optionsProvider);
        scalableField.TrackPropertyValue(weaponIdProperty, changedProperty => rebuildSelector());
        scalableField.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged += rebuildSelector;
            PlayerManagementSelectionContext.VisualPresetContentChanged += rebuildSelector;
        });
        scalableField.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged -= rebuildSelector;
            PlayerManagementSelectionContext.VisualPresetContentChanged -= rebuildSelector;
        });
        rebuildSelector();
        return scalableField;
    }
    #endregion

    #region Option Sources
    /// <summary>
    /// Builds Weapon Id options from one serialized mountable-weapons array while preserving authored order.
    /// Empty and duplicate IDs are excluded because validation reports them separately.
    /// </summary>
    /// <param name="additionalWeaponsProperty">Serialized mountable-weapons array.</param>
    /// <returns>Unique non-empty Weapon Id options.</returns>
    public static List<string> BuildOptions(SerializedProperty additionalWeaponsProperty)
    {
        List<string> options = new List<string>();

        if (additionalWeaponsProperty == null || !additionalWeaponsProperty.isArray)
            return options;

        HashSet<string> visitedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int entryIndex = 0; entryIndex < additionalWeaponsProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = additionalWeaponsProperty.GetArrayElementAtIndex(entryIndex);
            SerializedProperty weaponIdProperty = entryProperty != null
                ? entryProperty.FindPropertyRelative("weaponId")
                : null;

            if (weaponIdProperty == null || string.IsNullOrWhiteSpace(weaponIdProperty.stringValue))
                continue;

            string weaponId = weaponIdProperty.stringValue.Trim();

            if (visitedIds.Add(weaponId))
                options.Add(weaponId);
        }

        return options;
    }

    /// <summary>
    /// Builds Switch Weapon options from the Visual Preset paired with the edited Power-Ups preset by the active
    /// Master Preset. When no matching Master scope exists, merges registered Visual Preset IDs deterministically.
    /// </summary>
    /// <param name="weaponIdProperty">Serialized Switch Weapon ID used to resolve the owning Power-Ups preset.</param>
    /// <returns>Unique defined Weapon Id options available to Switch Weapon.</returns>
    public static List<string> BuildScopedSwitchWeaponOptions(SerializedProperty weaponIdProperty)
    {
        List<string> options = new List<string>();
        HashSet<string> visitedIds = new HashSet<string>(StringComparer.Ordinal);
        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;
        PlayerPowerUpsPreset owningPowerUpsPreset = weaponIdProperty != null && weaponIdProperty.serializedObject != null
            ? weaponIdProperty.serializedObject.targetObject as PlayerPowerUpsPreset
            : null;

        if (activeMasterPreset != null &&
            activeMasterPreset.VisualPreset != null &&
            ReferenceEquals(activeMasterPreset.PowerUpsPreset, owningPowerUpsPreset))
        {
            AddPresetOptions(activeMasterPreset.VisualPreset, options, visitedIds);
            return options;
        }

        PlayerVisualPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerVisualPresetLibrary>(PlayerVisualPresetLibraryUtility.DefaultLibraryPath);

        if (library == null || library.Presets == null)
            return options;

        for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
        {
            AddPresetOptions(library.Presets[presetIndex], options, visitedIds);
        }

        options.Sort(StringComparer.Ordinal);
        return options;
    }

    /// <summary>
    /// Checks whether a normalized Weapon Id exists in an already resolved option list.
    /// </summary>
    /// <param name="options">Resolved defined Weapon Id options.</param>
    /// <param name="weaponId">Normalized defined Weapon Id.</param>
    /// <returns>True when the ID is available in the supplied option list.</returns>
    public static bool ContainsWeaponId(IReadOnlyList<string> options, string weaponId)
    {
        if (options == null || string.IsNullOrWhiteSpace(weaponId))
            return false;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex], weaponId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Rebuilds one popup from the latest option source and preserves unknown serialized values without
    /// modifying them. Selecting the empty display option writes an empty token.
    /// </summary>
    /// <param name="popupContainer">Container rebuilt in place.</param>
    /// <param name="weaponIdProperty">Serialized Weapon Id token property.</param>
    /// <param name="label">Popup label shown to designers.</param>
    /// <param name="tooltip">Explanatory popup tooltip.</param>
    /// <param name="emptyLabel">Display label mapped to an empty serialized ID.</param>
    /// <param name="optionsProvider">Callback rebuilding available IDs.</param>
    private static void RebuildSelector(VisualElement popupContainer,
                                        SerializedProperty weaponIdProperty,
                                        string label,
                                        string tooltip,
                                        string emptyLabel,
                                        Func<List<string>> optionsProvider)
    {
        if (popupContainer == null || weaponIdProperty == null)
            return;

        popupContainer.Clear();
        List<string> weaponIds = optionsProvider != null ? optionsProvider.Invoke() : new List<string>();
        List<string> displayOptions = new List<string>(weaponIds.Count + 2);
        List<string> serializedOptions = new List<string>(weaponIds.Count + 2);
        string currentWeaponId = string.IsNullOrWhiteSpace(weaponIdProperty.stringValue)
            ? string.Empty
            : weaponIdProperty.stringValue.Trim();
        int selectedIndex = 0;
        displayOptions.Add(emptyLabel);
        serializedOptions.Add(string.Empty);

        for (int optionIndex = 0; optionIndex < weaponIds.Count; optionIndex++)
        {
            string weaponId = weaponIds[optionIndex];
            displayOptions.Add(weaponId);
            serializedOptions.Add(weaponId);

            if (string.Equals(weaponId, currentWeaponId, StringComparison.Ordinal))
                selectedIndex = serializedOptions.Count - 1;
        }

        if (!string.IsNullOrWhiteSpace(currentWeaponId) && selectedIndex == 0)
        {
            displayOptions.Insert(1, MissingPrefix + currentWeaponId);
            serializedOptions.Insert(1, currentWeaponId);
            selectedIndex = 1;
        }

        PopupField<string> popup = new PopupField<string>(label, displayOptions, selectedIndex);
        popup.tooltip = tooltip;
        popup.style.flexGrow = 1f;
        popup.RegisterValueChangedCallback(evt =>
        {
            int optionIndex = displayOptions.IndexOf(evt.newValue);

            if (optionIndex < 0 || optionIndex >= serializedOptions.Count)
                return;

            string selectedWeaponId = serializedOptions[optionIndex];

            if (string.Equals(weaponIdProperty.stringValue, selectedWeaponId, StringComparison.Ordinal))
                return;

            if (weaponIdProperty.serializedObject.targetObject != null)
                Undo.RecordObject(weaponIdProperty.serializedObject.targetObject, "Change " + label);

            weaponIdProperty.serializedObject.Update();
            weaponIdProperty.stringValue = selectedWeaponId;
            weaponIdProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        popupContainer.Add(popup);
    }

    /// <summary>
    /// Appends unique non-empty Weapon Ids from one Visual Preset.
    /// </summary>
    /// <param name="preset">Visual Preset providing mountable weapon definitions.</param>
    /// <param name="options">Destination ordered option list.</param>
    /// <param name="visitedIds">Set preventing duplicate IDs.</param>
    private static void AddPresetOptions(PlayerVisualPreset preset,
                                         List<string> options,
                                         HashSet<string> visitedIds)
    {
        if (preset == null || preset.WeaponVisuals == null || preset.WeaponVisuals.AdditionalWeapons == null)
            return;

        IReadOnlyList<PlayerAdditionalWeaponVisualEntry> entries = preset.WeaponVisuals.AdditionalWeapons;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            PlayerAdditionalWeaponVisualEntry entry = entries[entryIndex];

            if (entry == null || string.IsNullOrWhiteSpace(entry.WeaponId))
                continue;

            string weaponId = entry.WeaponId.Trim();

            if (visitedIds.Add(weaponId))
                options.Add(weaponId);
        }
    }
    #endregion

    #endregion
}
