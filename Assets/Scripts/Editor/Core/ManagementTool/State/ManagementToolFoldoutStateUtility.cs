using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides stable state keys and reusable foldout binding helpers shared by management-tool editor panels.
/// </summary>
public static class ManagementToolFoldoutStateUtility
{
    #region Fields
    private static readonly Dictionary<string, bool> foldoutStateByKey = new Dictionary<string, bool>(StringComparer.Ordinal);
    #endregion

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
        Foldout foldout = new Foldout();
        foldout.text = title;
        BindFoldoutState(foldout, stateKey, defaultValue);
        return foldout;
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
        string stateKey = BuildPropertyStateKey(property, suffix);
        return CreateFoldout(title, stateKey, defaultValue);
    }

    /// <summary>
    /// Binds one foldout to the provided state key.
    /// </summary>
    /// <param name="foldout">Foldout that must persist its expanded state.</param>
    /// <param name="stateKey">Stable state key used to restore and store the expanded state.</param>
    /// <param name="defaultValue">Expanded state used when no persisted state exists yet.</param>
    public static void BindFoldoutState(Foldout foldout, string stateKey, bool defaultValue)
    {
        if (foldout == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
        {
            foldout.value = defaultValue;
            return;
        }

        foldout.viewDataKey = stateKey;
        foldout.value = ResolveFoldoutState(stateKey, defaultValue);
        foldout.RegisterValueChangedCallback(evt =>
        {
            SetFoldoutState(stateKey, evt.newValue);
        });
    }

    /// <summary>
    /// Builds the stable key that identifies the serialized object owning one UI subtree.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target stateful controls.</param>
    /// <returns>Stable serialized-object key, or an empty string when unavailable.</returns>
    public static string BuildSerializedObjectStateKey(SerializedObject serializedObject)
    {
        if (serializedObject == null)
            return string.Empty;

        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject == null)
            return string.Empty;

        string assetPath = AssetDatabase.GetAssetPath(targetObject);

        if (!string.IsNullOrWhiteSpace(assetPath))
            return string.Format("{0}|{1}", targetObject.GetType().FullName, assetPath);

        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject);
        string globalObjectIdText = globalObjectId.ToString();

        if (!string.IsNullOrWhiteSpace(globalObjectIdText))
            return string.Format("{0}|{1}", targetObject.GetType().FullName, globalObjectIdText);

        return string.Format("{0}|Instance:{1}",
                             targetObject.GetType().FullName,
                             targetObject.GetInstanceID());
    }

    /// <summary>
    /// Builds the stable context key for one serialized property.
    /// </summary>
    /// <param name="property">Serialized property that identifies the owning data context.</param>
    /// <returns>Stable property context key without any local suffix.</returns>
    public static string BuildPropertyContextKey(SerializedProperty property)
    {
        if (property == null)
            return string.Empty;

        SerializedObject serializedObject = property.serializedObject;

        if (serializedObject == null)
            return string.Empty;

        string objectKey = BuildSerializedObjectStateKey(serializedObject);
        string propertyKey = PlayerScalingStatKeyUtility.NormalizePropertyPath(serializedObject, property.propertyPath);

        if (string.IsNullOrWhiteSpace(propertyKey))
            propertyKey = property.propertyPath;

        if (string.IsNullOrWhiteSpace(objectKey))
            return propertyKey;

        return string.Format("{0}|{1}", objectKey, propertyKey);
    }

    /// <summary>
    /// Builds the stable state key for one serialized property and one local suffix.
    /// </summary>
    /// <param name="property">Serialized property that identifies the owning data context.</param>
    /// <param name="suffix">Local suffix appended to distinguish multiple foldouts under the same property.</param>
    /// <returns>Stable state key, or the property context key when the suffix is empty.</returns>
    public static string BuildPropertyStateKey(SerializedProperty property, string suffix)
    {
        string propertyContextKey = BuildPropertyContextKey(property);

        if (string.IsNullOrWhiteSpace(propertyContextKey))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(suffix))
            return propertyContextKey;

        return string.Format("{0}|{1}", propertyContextKey, suffix.Trim());
    }

    /// <summary>
    /// Resolves the last persisted expanded state for one foldout key.
    /// </summary>
    /// <param name="stateKey">Stable state key used by one foldout.</param>
    /// <param name="defaultValue">Expanded state returned when the key has not been stored yet.</param>
    /// <returns>Persisted expanded state or the provided default.</returns>
    public static bool ResolveFoldoutState(string stateKey, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return defaultValue;

        bool isExpanded;

        if (foldoutStateByKey.TryGetValue(stateKey, out isExpanded))
            return isExpanded;

        return defaultValue;
    }

    /// <summary>
    /// Stores or clears one foldout expanded state.
    /// </summary>
    /// <param name="stateKey">Stable state key used by one foldout.</param>
    /// <param name="expanded">Expanded state that must be persisted.</param>
    public static void SetFoldoutState(string stateKey, bool expanded)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        foldoutStateByKey[stateKey] = expanded;
    }

    /// <summary>
    /// Removes stale foldout states whose keys are no longer part of one valid key set.
    /// </summary>
    /// <param name="keyPrefix">Shared prefix used by the group that is currently being rebuilt.</param>
    /// <param name="validStateKeys">Current valid keys for the rebuilt group.</param>
    public static void PruneFoldoutStates(string keyPrefix, HashSet<string> validStateKeys)
    {
        if (string.IsNullOrWhiteSpace(keyPrefix))
            return;

        if (validStateKeys == null)
            return;

        List<string> keysToRemove = new List<string>();

        foreach (KeyValuePair<string, bool> entry in foldoutStateByKey)
        {
            if (!entry.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                continue;

            if (validStateKeys.Contains(entry.Key))
                continue;

            keysToRemove.Add(entry.Key);
        }

        for (int index = 0; index < keysToRemove.Count; index++)
            foldoutStateByKey.Remove(keysToRemove[index]);
    }

    /// <summary>
    /// Captures the current expanded state of every foldout under the provided root that exposes a state key through viewDataKey.
    /// </summary>
    /// <param name="root">Root visual element whose descendant foldouts must be persisted before a rebuild or clear.</param>
    public static void CaptureFoldoutStates(VisualElement root)
    {
        if (root == null)
            return;

        List<Foldout> foldouts = root.Query<Foldout>().ToList();

        for (int foldoutIndex = 0; foldoutIndex < foldouts.Count; foldoutIndex++)
        {
            Foldout foldout = foldouts[foldoutIndex];

            if (foldout == null)
                continue;

            if (string.IsNullOrWhiteSpace(foldout.viewDataKey))
                continue;

            SetFoldoutState(foldout.viewDataKey, foldout.value);
        }
    }
    #endregion

    #endregion
}
