using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides stable state keys and reusable foldout binding helpers for Player Management Tool UI Toolkit rebuilds.
/// This keeps foldout open/closed state aligned with the same serialized object and property even after redraws or array reorders.
/// </summary>
public static class PlayerManagementFoldoutStateUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one foldout already bound to a persistent state key.
    /// </summary>
    /// <param name="title">Visible title shown in the foldout header.</param>
    /// <param name="stateKey">Stable state key used to restore and store the expanded state.</param>
    /// <param name="defaultValue">Expanded state used when the key was never seen before.</param>
    /// <returns>Configured foldout instance.</returns>
    public static Foldout CreateFoldout(string title, string stateKey, bool defaultValue)
    {
        return ManagementToolFoldoutStateUtility.CreateFoldout(title, stateKey, defaultValue);
    }

    /// <summary>
    /// Creates one foldout whose state key is derived from a serialized property plus a local suffix.
    /// </summary>
    /// <param name="property">Serialized property that identifies the owning data context.</param>
    /// <param name="title">Visible title shown in the foldout header.</param>
    /// <param name="suffix">Local suffix appended to the property key when multiple foldouts share the same property root.</param>
    /// <param name="defaultValue">Expanded state used when no persisted state exists yet.</param>
    /// <returns>Configured foldout instance.</returns>
    public static Foldout CreatePropertyFoldout(SerializedProperty property,
                                                string title,
                                                string suffix,
                                                bool defaultValue)
    {
        return ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property, title, suffix, defaultValue);
    }

    /// <summary>
    /// Binds one foldout to the provided state key.
    /// </summary>
    /// <param name="foldout">Foldout that must persist its expanded state.</param>
    /// <param name="stateKey">Stable state key used to restore and store the expanded state.</param>
    /// <param name="defaultValue">Expanded state used when no persisted state exists yet.</param>
    public static void BindFoldoutState(Foldout foldout, string stateKey, bool defaultValue)
    {
        ManagementToolFoldoutStateUtility.BindFoldoutState(foldout, stateKey, defaultValue);
    }

    /// <summary>
    /// Builds the stable key that identifies the serialized object owning one UI subtree.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target stateful controls.</param>
    /// <returns>Stable serialized-object key, or an empty string when unavailable.</returns>
    public static string BuildSerializedObjectStateKey(SerializedObject serializedObject)
    {
        return ManagementToolFoldoutStateUtility.BuildSerializedObjectStateKey(serializedObject);
    }

    /// <summary>
    /// Builds the stable context key for one serialized property.
    /// </summary>
    /// <param name="property">Serialized property that identifies the owning data context.</param>
    /// <returns>Stable property context key without any local suffix.</returns>
    public static string BuildPropertyContextKey(SerializedProperty property)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyContextKey(property);
    }

    /// <summary>
    /// Builds the stable state key for one serialized property and one local suffix.
    /// </summary>
    /// <param name="property">Serialized property that identifies the owning data context.</param>
    /// <param name="suffix">Local suffix appended to distinguish multiple foldouts under the same property.</param>
    /// <returns>Stable state key, or the property context key when the suffix is empty.</returns>
    public static string BuildPropertyStateKey(SerializedProperty property, string suffix)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyStateKey(property, suffix);
    }

    /// <summary>
    /// Resolves the last persisted expanded state for one foldout key.
    /// </summary>
    /// <param name="stateKey">Stable state key used by one foldout.</param>
    /// <param name="defaultValue">Expanded state returned when the key has not been stored yet.</param>
    /// <returns>Persisted expanded state or the provided default.</returns>
    public static bool ResolveFoldoutState(string stateKey, bool defaultValue)
    {
        return ManagementToolFoldoutStateUtility.ResolveFoldoutState(stateKey, defaultValue);
    }

    /// <summary>
    /// Stores or clears one foldout expanded state.
    /// </summary>
    /// <param name="stateKey">Stable state key used by one foldout.</param>
    /// <param name="expanded">Expanded state that must be persisted.</param>
    public static void SetFoldoutState(string stateKey, bool expanded)
    {
        ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, expanded);
    }

    /// <summary>
    /// Removes stale foldout states whose keys are no longer part of one valid key set.
    /// </summary>
    /// <param name="keyPrefix">Shared prefix used by the group that is currently being rebuilt.</param>
    /// <param name="validStateKeys">Current valid keys for the rebuilt group.</param>
    public static void PruneFoldoutStates(string keyPrefix, HashSet<string> validStateKeys)
    {
        ManagementToolFoldoutStateUtility.PruneFoldoutStates(keyPrefix, validStateKeys);
    }

    /// <summary>
    /// Captures the current expanded state of every foldout under the provided root that exposes a state key through viewDataKey.
    /// </summary>
    /// <param name="root">Root visual element whose descendant foldouts must be persisted before a rebuild/clear.</param>
    public static void CaptureFoldoutStates(VisualElement root)
    {
        ManagementToolFoldoutStateUtility.CaptureFoldoutStates(root);
    }
    #endregion

    #endregion
}
