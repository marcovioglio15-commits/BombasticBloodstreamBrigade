using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable key-value container used to persist dictionary-like data in Unity inspectors.
/// </summary>
[Serializable]
public class SerializableDictionary<TKey, TValue>
{
    #region Fields
    [Tooltip("Serialized key list used to persist dictionary entries.")]
    [SerializeField] private List<TKey> keys = new List<TKey>();

    [Tooltip("Serialized value list aligned by index with the key list.")]
    [SerializeField] private List<TValue> values = new List<TValue>();

    private Dictionary<TKey, TValue> dictionary;
    #endregion

    #region Methods
    #region Lifecycle
    /// <summary>
    /// Initializes the runtime dictionary cache from serialized key/value lists.
    /// </summary>

    public SerializableDictionary()
    {
        PopulateDictionaryIfEmpty();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Returns the runtime dictionary, populating it from serialized data when required.
    /// </summary>
    /// <returns>Dictionary view backed by serialized keys and values.</returns>
    public Dictionary<TKey, TValue> GetDictionary()
    {
        PopulateDictionaryIfEmpty();
        return dictionary;
    }

    /// <summary>
    /// Clears serialized and runtime dictionary content.
    /// </summary>

    public void Clear()
    {
        keys.Clear();
        values.Clear();

        if (dictionary == null)
            return;

        dictionary.Clear();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Builds runtime dictionary content from serialized key/value lists when cache is empty.
    /// </summary>

    private void PopulateDictionaryIfEmpty()
    {
        if (dictionary != null && dictionary.Count > 0)
            return;

        dictionary = new Dictionary<TKey, TValue>();
        int entryCount = Mathf.Min(keys.Count, values.Count);

        for (int index = 0; index < entryCount; index++)
            dictionary[keys[index]] = values[index];
    }
    #endregion
    #endregion
}
