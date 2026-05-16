using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides shared UI and preset helper methods for the animation bindings management panel.
/// none.
/// </summary>
public static class PlayerAnimationBindingsPanelUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether one animation bindings preset matches the current search text.
    /// Used by the preset list refresh to keep filtering logic outside the panel container.
    /// </summary>
    /// <param name="preset">Preset inspected by the filter.</param>
    /// <param name="searchText">Search text typed by the user.</param>
    /// <returns>True when the preset should remain visible in the list.</returns>
    public static bool MatchesSearch(PlayerAnimationBindingsPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string search = searchText.Trim();

        if (string.IsNullOrWhiteSpace(search))
            return true;

        if (ContainsIgnoreCase(preset.PresetName, search))
            return true;

        if (ContainsIgnoreCase(preset.Description, search))
            return true;

        if (ContainsIgnoreCase(preset.name, search))
            return true;

        return false;
    }

    /// <summary>
    /// Creates one titled section and adds it to the details scroll view.
    /// </summary>
    /// <param name="detailsRoot">Scroll view receiving the new section.</param>
    /// <param name="title">Visible title of the created section.</param>
    /// <returns>Root element of the created section.</returns>
    public static VisualElement CreateSection(ScrollView detailsRoot, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(titleLabel, "NashCore.PlayerManagement.Animation.Section." + title);
        section.Add(titleLabel);

        if (detailsRoot != null)
            detailsRoot.Add(section);

        return section;
    }

    /// <summary>
    /// Builds one property field using the shared scaling-aware field factory.
    /// </summary>
    /// <param name="serializedObject">Serialized preset currently edited by the panel.</param>
    /// <param name="propertyName">Serialized property path to resolve.</param>
    /// <param name="labelOverride">Custom label shown in the inspector row.</param>
    /// <returns>UI element bound to the requested property.</returns>
    public static VisualElement CreatePropertyField(SerializedObject serializedObject, string propertyName, string labelOverride)
    {
        if (serializedObject == null)
            return new Label("Missing serialized object.");

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        SerializedProperty scalingRulesProperty = serializedObject.FindProperty("scalingRules");
        return PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, labelOverride);
    }

    /// <summary>
    /// Builds a simple read-only label row used for immutable preset metadata.
    /// </summary>
    /// <param name="label">Static label displayed on the left.</param>
    /// <param name="value">Value displayed on the right.</param>
    /// <returns>One row element containing the label/value pair.</returns>
    public static VisualElement CreateReadOnlyText(string label, string value)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        Label labelElement = new Label(label + ": ");
        labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(labelElement);

        Label valueElement = new Label(string.IsNullOrWhiteSpace(value) ? "<empty>" : value);
        row.Add(valueElement);
        return row;
    }

    /// <summary>
    /// Renames one animation bindings preset and updates the draft session bookkeeping.
    /// </summary>
    /// <param name="preset">Preset asset to rename.</param>
    /// <param name="newName">Requested display and asset name.</param>
    public static void RenamePreset(PlayerAnimationBindingsPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedPreset.FindProperty("presetName");
        serializedPreset.Update();

        if (presetNameProperty != null)
            presetNameProperty.stringValue = normalizedName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
    }

    /// <summary>
    /// Resolves the compact display label shown inside the preset list.
    /// </summary>
    /// <param name="preset">Preset converted to display text.</param>
    /// <returns>User-facing list label.</returns>
    public static string GetPresetDisplayName(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string displayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return displayName;

        return displayName + " v. " + version;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether one string contains another using case-insensitive comparison.
    /// </summary>
    /// <param name="value">Source text to inspect.</param>
    /// <param name="search">Search fragment requested by the user.</param>
    /// <returns>True when the fragment exists inside the source text.</returns>
    private static bool ContainsIgnoreCase(string value, string search)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(search))
            return false;

        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #endregion
}
