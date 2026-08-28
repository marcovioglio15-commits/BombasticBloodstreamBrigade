using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Centralizes persistent filter values and stable foldout keys used by modular power-up definition drawers.
/// </summary>
public static class ModularPowerUpDefinitionDrawerStateUtility
{
    #region Methods

    #region Context
    /// <summary>
    /// Builds the stable state context for one power-up's module-binding section.
    /// </summary>
    /// <param name="powerUpProperty">Serialized power-up definition owning the section.</param>
    /// <returns>Stable property-scoped state key.</returns>
    public static string BuildContextKey(SerializedProperty powerUpProperty)
    {
        return PlayerManagementFoldoutStateUtility.BuildPropertyStateKey(powerUpProperty, "ModuleBindingsSection");
    }

    /// <summary>
    /// Builds the stable foldout key for one module binding.
    /// </summary>
    /// <param name="contextKey">Power-up section context key.</param>
    /// <param name="bindingProperty">Serialized binding represented by the foldout.</param>
    /// <returns>Stable binding-specific foldout key, or an empty string when the context is unavailable.</returns>
    public static string BuildBindingFoldoutStateKey(string contextKey, SerializedProperty bindingProperty)
    {
        if (string.IsNullOrWhiteSpace(contextKey))
            return string.Empty;

        string bindingId = ModularPowerUpBindingDrawerUtility.ResolveBindingStableId(bindingProperty);

        if (string.IsNullOrWhiteSpace(bindingId))
            bindingId = PlayerManagementFoldoutStateUtility.BuildPropertyContextKey(bindingProperty);

        return string.Format("{0}|Binding:{1}", contextKey, bindingId);
    }

    /// <summary>
    /// Removes foldout states that no longer map to bindings in the current power-up.
    /// </summary>
    /// <param name="contextKey">Power-up section context key.</param>
    /// <param name="validStateKeys">Current binding foldout keys retained by the drawer.</param>
    public static void PruneBindingFoldoutStates(string contextKey, HashSet<string> validStateKeys)
    {
        PlayerManagementFoldoutStateUtility.PruneFoldoutStates(string.Format("{0}|Binding:", contextKey), validStateKeys);
    }
    #endregion

    #region Filters
    /// <summary>
    /// Resolves one stored filter without creating empty dictionary entries.
    /// </summary>
    /// <param name="filterByContextKey">Filter storage indexed by drawer context.</param>
    /// <param name="contextKey">Current power-up drawer context.</param>
    /// <returns>Stored filter text, or an empty string when no filter exists.</returns>
    public static string ResolveFilterValue(Dictionary<string, string> filterByContextKey, string contextKey)
    {
        if (filterByContextKey == null || string.IsNullOrWhiteSpace(contextKey))
            return string.Empty;

        if (filterByContextKey.TryGetValue(contextKey, out string filterValue))
            return filterValue;

        return string.Empty;
    }

    /// <summary>
    /// Stores non-empty filter text and removes cleared values from the shared dictionary.
    /// </summary>
    /// <param name="filterByContextKey">Filter storage indexed by drawer context.</param>
    /// <param name="contextKey">Current power-up drawer context.</param>
    /// <param name="value">New filter text.</param>
    public static void StoreFilterValue(Dictionary<string, string> filterByContextKey,
                                        string contextKey,
                                        string value)
    {
        if (filterByContextKey == null || string.IsNullOrWhiteSpace(contextKey))
            return;

        if (string.IsNullOrWhiteSpace(value))
        {
            filterByContextKey.Remove(contextKey);
            return;
        }

        filterByContextKey[contextKey] = value;
    }
    #endregion

    #endregion
}
