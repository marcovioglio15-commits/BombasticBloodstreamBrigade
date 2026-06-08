using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes selector, Weapon Id, animation, and default-attachment warnings for the Weapon Visuals subsection.
/// </summary>
internal static class PlayerVisualPresetsPanelWeaponVisualWarningsUtility
{
    #region Constants
    private const string EntryWeaponIdRelativePath = "weaponId";
    private const string EntryShootAnimationClipRelativePath = "shootAnimationClip";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Refreshes the visible weapon controls based on whether a runtime visual bridge prefab is assigned.
    /// </summary>
    /// <param name="runtimePrefabProperty">Serialized runtime visual bridge prefab property.</param>
    /// <param name="missingPrefabBox">Information box shown while no prefab is assigned.</param>
    /// <param name="baseGunContainer">Base Gun controls container.</param>
    /// <param name="additionalWeaponsContainer">Mountable weapons controls container.</param>
    public static void RefreshVisibility(SerializedProperty runtimePrefabProperty,
                                         HelpBox missingPrefabBox,
                                         VisualElement baseGunContainer,
                                         VisualElement additionalWeaponsContainer)
    {
        bool hasRuntimePrefab = runtimePrefabProperty != null && runtimePrefabProperty.objectReferenceValue != null;
        missingPrefabBox.style.display = hasRuntimePrefab ? DisplayStyle.None : DisplayStyle.Flex;
        baseGunContainer.style.display = hasRuntimePrefab ? DisplayStyle.Flex : DisplayStyle.None;
        additionalWeaponsContainer.style.display = hasRuntimePrefab ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Rebuilds coherent live warnings for IDs, selectors, clips, duplicates, and the default attachment.
    /// </summary>
    /// <param name="runtimePrefabProperty">Serialized runtime visual bridge prefab property.</param>
    /// <param name="defaultAdditionalWeaponProperty">Serialized default attachment ID property.</param>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    /// <param name="baseGunBinding">Binding wrapping Base Gun controls.</param>
    /// <param name="additionalBindings">Per-entry mountable bindings.</param>
    /// <param name="warningsContainer">Warnings container rebuilt in place.</param>
    public static void RefreshWarnings(SerializedProperty runtimePrefabProperty,
                                       SerializedProperty defaultAdditionalWeaponProperty,
                                       SerializedProperty additionalWeaponsProperty,
                                       WeaponReferenceBinding baseGunBinding,
                                       IReadOnlyList<WeaponReferenceBinding> additionalBindings,
                                       VisualElement warningsContainer)
    {
        warningsContainer.Clear();
        GameObject runtimePrefab = runtimePrefabProperty != null
            ? runtimePrefabProperty.objectReferenceValue as GameObject
            : null;
        HashSet<string> uniqueSelectors = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> uniqueIds = new HashSet<string>(StringComparer.Ordinal);

        AppendSelectorWarnings(warningsContainer, baseGunBinding, runtimePrefab, uniqueSelectors);

        if (additionalWeaponsProperty != null && additionalWeaponsProperty.isArray)
        {
            for (int entryIndex = 0; entryIndex < additionalBindings.Count; entryIndex++)
            {
                WeaponReferenceBinding binding = additionalBindings[entryIndex];
                SerializedProperty entryProperty = entryIndex < additionalWeaponsProperty.arraySize
                    ? additionalWeaponsProperty.GetArrayElementAtIndex(entryIndex)
                    : null;
                SerializedProperty weaponIdProperty = entryProperty != null
                    ? entryProperty.FindPropertyRelative(EntryWeaponIdRelativePath)
                    : null;
                SerializedProperty shootClipProperty = entryProperty != null
                    ? entryProperty.FindPropertyRelative(EntryShootAnimationClipRelativePath)
                    : null;

                AppendWeaponIdWarnings(warningsContainer, weaponIdProperty, binding.SlotLabel, uniqueIds);
                AppendSelectorWarnings(warningsContainer, binding, runtimePrefab, uniqueSelectors);

                if (shootClipProperty != null && shootClipProperty.objectReferenceValue == null)
                    AddWarning(warningsContainer, string.Format("{0} has no shoot animation clip assigned.", binding.SlotLabel));
            }
        }

        AppendDefaultAttachmentWarnings(warningsContainer,
                                        defaultAdditionalWeaponProperty,
                                        additionalWeaponsProperty);
    }

    /// <summary>
    /// Resolves one object reference selector against a prefab asset.
    /// </summary>
    /// <param name="runtimePrefab">Runtime visual bridge prefab asset.</param>
    /// <param name="selector">Prefab-relative path or unique GameObject name.</param>
    /// <returns>Resolved child GameObject or null.</returns>
    public static GameObject ResolveReferenceObject(GameObject runtimePrefab, string selector)
    {
        if (runtimePrefab == null)
            return null;

        if (!PlayerWeaponVisualReferenceUtility.TryResolve(runtimePrefab.transform,
                                                            selector,
                                                            out Transform resolvedTransform))
            return null;

        return resolvedTransform.gameObject;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Appends empty, oversized, duplicate, and unresolved selector warnings.
    /// </summary>
    /// <param name="warningsContainer">Warnings container receiving HelpBoxes.</param>
    /// <param name="binding">Binding to inspect.</param>
    /// <param name="runtimePrefab">Runtime visual bridge prefab used to resolve selectors.</param>
    /// <param name="uniqueSelectors">Set used to detect cross-binding duplicates.</param>
    private static void AppendSelectorWarnings(VisualElement warningsContainer,
                                               WeaponReferenceBinding binding,
                                               GameObject runtimePrefab,
                                               HashSet<string> uniqueSelectors)
    {
        if (binding == null || binding.SelectorProperty == null)
            return;

        string selector = binding.SelectorProperty.stringValue;

        if (string.IsNullOrWhiteSpace(selector))
        {
            AddWarning(warningsContainer, string.Format("{0} reference is empty.", binding.SlotLabel));
            return;
        }

        string normalizedSelector = selector.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedSelector) > PlayerWeaponVisualSettings.MaximumReferenceSelectorUtf8Bytes)
            AddWarning(warningsContainer, string.Format("{0} reference exceeds the ECS fixed-string capacity.", binding.SlotLabel));

        if (!uniqueSelectors.Add(normalizedSelector))
            AddWarning(warningsContainer, string.Format("{0} duplicates another weapon reference.", binding.SlotLabel));

        if (runtimePrefab != null && ResolveReferenceObject(runtimePrefab, normalizedSelector) == null)
            AddWarning(warningsContainer, string.Format("{0} reference does not resolve inside the assigned Runtime Visual Bridge Prefab.", binding.SlotLabel));
    }

    /// <summary>
    /// Appends empty, oversized, and duplicate Weapon Id warnings for one mountable entry.
    /// </summary>
    /// <param name="warningsContainer">Warnings container receiving HelpBoxes.</param>
    /// <param name="weaponIdProperty">Serialized Weapon Id property.</param>
    /// <param name="entryLabel">Entry label included in warning text.</param>
    /// <param name="uniqueIds">Set used to detect duplicate IDs.</param>
    private static void AppendWeaponIdWarnings(VisualElement warningsContainer,
                                               SerializedProperty weaponIdProperty,
                                               string entryLabel,
                                               HashSet<string> uniqueIds)
    {
        if (weaponIdProperty == null || string.IsNullOrWhiteSpace(weaponIdProperty.stringValue))
        {
            AddWarning(warningsContainer, string.Format("{0} has no Weapon Id.", entryLabel));
            return;
        }

        string normalizedId = weaponIdProperty.stringValue.Trim();

        if (Encoding.UTF8.GetByteCount(normalizedId) > PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
            AddWarning(warningsContainer, string.Format("{0} Weapon Id exceeds the ECS fixed-string capacity.", entryLabel));

        if (!uniqueIds.Add(normalizedId))
            AddWarning(warningsContainer, string.Format("Weapon Id '{0}' appears in more than one mountable entry.", normalizedId));
    }

    /// <summary>
    /// Validates Default Additional Weapon Id against the authored mountable entries.
    /// </summary>
    /// <param name="warningsContainer">Warnings container receiving HelpBoxes.</param>
    /// <param name="defaultAdditionalWeaponProperty">Serialized default attachment ID property.</param>
    /// <param name="additionalWeaponsProperty">Serialized mountable weapons array.</param>
    private static void AppendDefaultAttachmentWarnings(VisualElement warningsContainer,
                                                        SerializedProperty defaultAdditionalWeaponProperty,
                                                        SerializedProperty additionalWeaponsProperty)
    {
        if (defaultAdditionalWeaponProperty == null)
        {
            AddWarning(warningsContainer, "Default Additional Weapon Id property is missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(defaultAdditionalWeaponProperty.stringValue))
            return;

        string defaultId = defaultAdditionalWeaponProperty.stringValue.Trim();

        if (Encoding.UTF8.GetByteCount(defaultId) > PlayerWeaponVisualSettings.MaximumWeaponIdUtf8Bytes)
        {
            AddWarning(warningsContainer, "Default Additional Weapon Id exceeds the ECS fixed-string capacity.");
            return;
        }

        if (additionalWeaponsProperty == null || !additionalWeaponsProperty.isArray)
            return;

        for (int entryIndex = 0; entryIndex < additionalWeaponsProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = additionalWeaponsProperty.GetArrayElementAtIndex(entryIndex);
            SerializedProperty weaponIdProperty = entryProperty != null
                ? entryProperty.FindPropertyRelative(EntryWeaponIdRelativePath)
                : null;

            if (weaponIdProperty != null &&
                string.Equals(weaponIdProperty.stringValue.Trim(), defaultId, StringComparison.Ordinal))
                return;
        }

        AddWarning(warningsContainer,
                   string.Format("Default Additional Weapon Id '{0}' does not match any mountable entry.", defaultId));
    }

    /// <summary>
    /// Adds one warning HelpBox to the supplied container.
    /// </summary>
    /// <param name="container">Destination warning container.</param>
    /// <param name="message">Human-readable warning message.</param>
    private static void AddWarning(VisualElement container, string message)
    {
        container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
    }
    #endregion

    #endregion
}

#region Reference Binding
/// <summary>
/// Couples one scalable reference selector with its compact validation label.
/// </summary>
internal sealed class WeaponReferenceBinding
{
    #region Fields
    public readonly string SlotLabel;
    public readonly SerializedProperty SelectorProperty;
    #endregion

    #region Methods

    #region Constructor
    /// <summary>
    /// Creates one immutable reference validation binding.
    /// </summary>
    /// <param name="slotLabel">Compact entry label used by warnings.</param>
    /// <param name="selectorProperty">Serialized scalable reference selector.</param>
    public WeaponReferenceBinding(string slotLabel,
                                  SerializedProperty selectorProperty)
    {
        SlotLabel = slotLabel;
        SelectorProperty = selectorProperty;
    }
    #endregion

    #endregion
}
#endregion
