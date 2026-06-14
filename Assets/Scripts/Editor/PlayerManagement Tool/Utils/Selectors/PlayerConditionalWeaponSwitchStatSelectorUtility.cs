using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds the popup selector used by conditional weapon switch conditions to pick a scalable stat declared in
/// the Level-Up & Progression preset. The serialized value is kept as a plain stat name string so the runtime
/// evaluator can match it without reflection. Stat-type metadata is exposed in the popup label so designers can
/// avoid wiring numeric ranges against Token-typed stats by mistake.
/// </summary>
internal static class PlayerConditionalWeaponSwitchStatSelectorUtility
{
    #region Constants
    public const string EmptyDisplayLabel = "<Select Stat>";
    private const string MissingPrefix = "<Missing> ";
    private const string FloatTypeLabel = "Float";
    private const string IntegerTypeLabel = "Integer";
    private const string UnsignedTypeLabel = "Unsigned";
    private const string BooleanTypeLabel = "Boolean";
    private const string TokenTypeLabel = "Token";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one popup bound to a serialized scalable-stat name property. The popup rebuilds itself whenever
    /// the active master-preset selection or progression-preset library content changes so renames are reflected
    /// without a tool reload.
    /// </summary>
    /// <param name="statNameProperty">Serialized stat-name property bound to the popup.</param>
    /// <param name="label">Visible popup label.</param>
    /// <param name="tooltip">Explanatory popup tooltip.</param>
    /// <returns>Configured popup root.</returns>
    public static VisualElement BuildSelector(SerializedProperty statNameProperty, string label, string tooltip)
    {
        if (statNameProperty == null)
            return new HelpBox("Condition stat name property is missing.", HelpBoxMessageType.Warning);

        VisualElement container = new VisualElement();
        container.style.flexGrow = 1f;

        Action rebuildSelector = () => RebuildSelector(container, statNameProperty, label, tooltip);
        container.TrackPropertyValue(statNameProperty, changedProperty => rebuildSelector());
        container.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged += rebuildSelector;
            PlayerManagementSelectionContext.ProgressionPresetContentChanged += rebuildSelector;
        });
        container.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            PlayerManagementSelectionContext.ContextChanged -= rebuildSelector;
            PlayerManagementSelectionContext.ProgressionPresetContentChanged -= rebuildSelector;
        });
        rebuildSelector();
        return container;
    }

    /// <summary>
    /// Returns the list of scalable stat option labels (display string) plus their stat type so external panels
    /// can surface coherent warnings without rebuilding the popup contents.
    /// </summary>
    /// <returns>Ordered scalable-stat entries.</returns>
    public static List<PlayerConditionalWeaponSwitchStatOption> BuildScopedStatOptions()
    {
        List<PlayerConditionalWeaponSwitchStatOption> options = new List<PlayerConditionalWeaponSwitchStatOption>();
        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;
        PlayerProgressionPreset progressionPreset = activeMasterPreset != null ? activeMasterPreset.ProgressionPreset : null;

        if (progressionPreset == null)
        {
            PlayerProgressionPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerProgressionPresetLibrary>(PlayerProgressionPresetLibraryUtility.DefaultLibraryPath);

            if (library == null || library.Presets == null)
                return options;

            HashSet<string> visitedStats = new HashSet<string>(StringComparer.Ordinal);

            for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
                AppendOptions(library.Presets[presetIndex], options, visitedStats);

            return options;
        }

        HashSet<string> visitedScoped = new HashSet<string>(StringComparer.Ordinal);
        AppendOptions(progressionPreset, options, visitedScoped);
        return options;
    }

    /// <summary>
    /// Checks whether a normalized stat name resolves to one of the supplied options. Used by the property
    /// drawer to detect missing references without rebuilding the popup list.
    /// </summary>
    /// <param name="options">Resolved scalable-stat options.</param>
    /// <param name="statName">Normalized stat name to look up.</param>
    /// <returns>True when the stat name is contained in the supplied options.</returns>
    public static bool ContainsStat(IReadOnlyList<PlayerConditionalWeaponSwitchStatOption> options, string statName)
    {
        if (options == null || string.IsNullOrWhiteSpace(statName))
            return false;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex].StatName, statName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the stat type associated with one stat name, or null when the stat is missing from the supplied
    /// options. Callers use this to differentiate numeric and boolean stats from unsupported Token references.
    /// </summary>
    /// <param name="options">Resolved scalable-stat options.</param>
    /// <param name="statName">Normalized stat name to look up.</param>
    /// <returns>Stat type when found, otherwise null.</returns>
    public static PlayerScalableStatType? TryGetStatType(IReadOnlyList<PlayerConditionalWeaponSwitchStatOption> options,
                                                         string statName)
    {
        if (options == null || string.IsNullOrWhiteSpace(statName))
            return null;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            if (string.Equals(options[optionIndex].StatName, statName, StringComparison.Ordinal))
                return options[optionIndex].StatType;
        }

        return null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Rebuilds the popup in place using the current scoped option list. Selecting the empty entry writes an
    /// empty serialized name; missing stat references are preserved as &quot;Missing&gt;&quot;-prefixed entries
    /// so renames don't silently destroy authored data.
    /// </summary>
    /// <param name="container">Container hosting the popup.</param>
    /// <param name="statNameProperty">Serialized stat-name property bound to the popup.</param>
    /// <param name="label">Visible popup label.</param>
    /// <param name="tooltip">Explanatory popup tooltip.</param>
    private static void RebuildSelector(VisualElement container,
                                        SerializedProperty statNameProperty,
                                        string label,
                                        string tooltip)
    {
        if (container == null || statNameProperty == null)
            return;

        container.Clear();
        List<PlayerConditionalWeaponSwitchStatOption> options = BuildScopedStatOptions();
        List<string> displayOptions = new List<string>(options.Count + 2);
        List<string> serializedOptions = new List<string>(options.Count + 2);
        string currentStatName = string.IsNullOrWhiteSpace(statNameProperty.stringValue)
            ? string.Empty
            : statNameProperty.stringValue.Trim();
        int selectedIndex = 0;
        displayOptions.Add(EmptyDisplayLabel);
        serializedOptions.Add(string.Empty);

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            PlayerConditionalWeaponSwitchStatOption option = options[optionIndex];
            displayOptions.Add(BuildDisplayLabel(option));
            serializedOptions.Add(option.StatName);

            if (string.Equals(option.StatName, currentStatName, StringComparison.Ordinal))
                selectedIndex = serializedOptions.Count - 1;
        }

        if (!string.IsNullOrWhiteSpace(currentStatName) && selectedIndex == 0)
        {
            displayOptions.Insert(1, MissingPrefix + currentStatName);
            serializedOptions.Insert(1, currentStatName);
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

            string selectedStatName = serializedOptions[optionIndex];

            if (string.Equals(statNameProperty.stringValue, selectedStatName, StringComparison.Ordinal))
                return;

            if (statNameProperty.serializedObject.targetObject != null)
                Undo.RecordObject(statNameProperty.serializedObject.targetObject, "Change " + label);

            statNameProperty.serializedObject.Update();
            statNameProperty.stringValue = selectedStatName;
            statNameProperty.serializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
        });
        container.Add(popup);
    }

    /// <summary>
    /// Appends scalable-stat options from one progression preset, preserving authored order and skipping
    /// duplicates and empty stat names.
    /// </summary>
    /// <param name="progressionPreset">Source progression preset.</param>
    /// <param name="options">Destination option list.</param>
    /// <param name="visitedStats">Hash set guarding against duplicates.</param>
    private static void AppendOptions(PlayerProgressionPreset progressionPreset,
                                      List<PlayerConditionalWeaponSwitchStatOption> options,
                                      HashSet<string> visitedStats)
    {
        if (progressionPreset == null || progressionPreset.ScalableStats == null)
            return;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = progressionPreset.ScalableStats;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[statIndex];

            if (statDefinition == null || string.IsNullOrWhiteSpace(statDefinition.StatName))
                continue;

            string statName = statDefinition.StatName.Trim();

            if (!visitedStats.Add(statName))
                continue;

            options.Add(new PlayerConditionalWeaponSwitchStatOption(statName, statDefinition.StatType));
        }
    }

    /// <summary>
    /// Formats one option label as &quot;statName (Type)&quot; so designers can immediately tell which authored
    /// stats expose a numeric or boolean projection compatible with the inclusive range condition.
    /// </summary>
    /// <param name="option">Option being formatted.</param>
    /// <returns>Display label combining the stat name and its declared type.</returns>
    private static string BuildDisplayLabel(PlayerConditionalWeaponSwitchStatOption option)
    {
        string typeLabel = ResolveStatTypeLabel(option.StatType);
        return option.StatName + " (" + typeLabel + ")";
    }

    /// <summary>
    /// Maps a scalable stat type to a short label string used in the popup display.
    /// </summary>
    /// <param name="statType">Stat type being labeled.</param>
    /// <returns>Short human-readable type label.</returns>
    private static string ResolveStatTypeLabel(PlayerScalableStatType statType)
    {
        switch (statType)
        {
            case PlayerScalableStatType.Float:
                return FloatTypeLabel;
            case PlayerScalableStatType.Integer:
                return IntegerTypeLabel;
            case PlayerScalableStatType.Unsigned:
                return UnsignedTypeLabel;
            case PlayerScalableStatType.Boolean:
                return BooleanTypeLabel;
            case PlayerScalableStatType.Token:
                return TokenTypeLabel;
            default:
                return statType.ToString();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Pairs one scalable-stat name with its declared runtime type so the conditional weapon switch panel can
/// surface coherent warnings without re-reading the source progression preset every time.
/// </summary>
public readonly struct PlayerConditionalWeaponSwitchStatOption
{
    #region Fields
    public readonly string StatName;
    public readonly PlayerScalableStatType StatType;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Stores the pair as an immutable record. Used by <see cref="PlayerConditionalWeaponSwitchStatSelectorUtility"/>.
    /// </summary>
    /// <param name="statNameValue">Scalable stat name.</param>
    /// <param name="statTypeValue">Scalable stat type.</param>
    public PlayerConditionalWeaponSwitchStatOption(string statNameValue, PlayerScalableStatType statTypeValue)
    {
        StatName = statNameValue;
        StatType = statTypeValue;
    }
    #endregion

    #endregion
}
