using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared EditorPrefs persistence helpers used by management tool editor panels.
/// Used by Player/Enemy Management windows and preset panels to restore UI state after close/reopen.
/// </summary>
public static class ManagementToolStateUtility
{
    #region Methods

    #region Enum State
    /// <summary>
    /// Loads a persisted enum value from EditorPrefs. 
    /// Takes as arguments a key to identify the state, and a fallback enum value to return if loading fails.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <param name="fallbackValue"></param>
    /// <returns> Returns the loaded enum value, or the fallback value if loading fails. </returns>
    public static TEnum LoadEnumValue<TEnum>(string stateKey, TEnum fallbackValue) where TEnum : struct, Enum
    {
        // Abort and return fallback when the key is invalid.
        if (string.IsNullOrWhiteSpace(stateKey))
            return fallbackValue;

        // Read persisted int value and map it back to the enum safely.
        int fallbackIntValue = Convert.ToInt32(fallbackValue, CultureInfo.InvariantCulture);
        int savedValue = EditorPrefs.GetInt(stateKey, fallbackIntValue);

        if (!Enum.IsDefined(typeof(TEnum), savedValue))
            return fallbackValue;

        object boxedValue = Enum.ToObject(typeof(TEnum), savedValue);
        return (TEnum)boxedValue;
    }

    /// <summary>
    /// Saves an enum value to EditorPrefs. 
    /// The value is serialized as its integer representation.
    /// Takes as arguments a key to identify the state and the enum value to save.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <param name="value"></param>
    public static void SaveEnumValue<TEnum>(string stateKey, TEnum value) where TEnum : struct, Enum
    {
        // Skip writes for invalid keys.
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        // Persist enum as integer for stable storage.
        int serializedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        EditorPrefs.SetInt(stateKey, serializedValue);
    }

    /// <summary>
    /// Loads a persisted list of enum values from EditorPrefs.
    /// Takes as an argument a key to identify the state, and returns the loaded list of enum values.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <returns> Returns the loaded list of enum values, or an empty list if loading fails. </returns>
    public static List<TEnum> LoadEnumList<TEnum>(string stateKey) where TEnum : struct, Enum
    {
        // Create the output list once and return it for all failure paths.
        List<TEnum> values = new List<TEnum>();

        if (string.IsNullOrWhiteSpace(stateKey))
            return values;

        string rawValue = EditorPrefs.GetString(stateKey, string.Empty);

        if (string.IsNullOrWhiteSpace(rawValue))
            return values;

        string[] tokens = rawValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        HashSet<int> uniqueValues = new HashSet<int>();

        // Parse, validate, and de-duplicate each serialized enum token.
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
                continue;

            if (!Enum.IsDefined(typeof(TEnum), parsedValue))
                continue;

            if (uniqueValues.Contains(parsedValue))
                continue;

            uniqueValues.Add(parsedValue);
            object boxedValue = Enum.ToObject(typeof(TEnum), parsedValue);
            values.Add((TEnum)boxedValue);
        }

        return values;
    }

    /// <summary>
    /// Saves a list of enum values to EditorPrefs.
    /// Takes as arguments a key to identify the state and the list of enum values to save.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <param name="values"></param>
    public static void SaveEnumList<TEnum>(string stateKey, IList<TEnum> values) where TEnum : struct, Enum
    {
        // Skip writes for invalid keys.
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        // Remove stale state when the incoming list is null or empty.
        if (values == null || values.Count == 0)
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        // Serialize enum values to a compact comma-separated integer list.
        StringBuilder builder = new StringBuilder(values.Count * 3);

        for (int index = 0; index < values.Count; index++)
        {
            if (index > 0)
                builder.Append(',');

            int serializedValue = Convert.ToInt32(values[index], CultureInfo.InvariantCulture);
            builder.Append(serializedValue.ToString(CultureInfo.InvariantCulture));
        }

        EditorPrefs.SetString(stateKey, builder.ToString());
    }
    #endregion

    #region Color State
    /// <summary>
    /// Saves one text/background color pair to EditorPrefs for customizable management-tool labels.
    /// Takes as arguments a key to identify the state and the two colors to persist.
    /// </summary>
    /// <param name="stateKey"></param>
    /// <param name="textColor"></param>
    /// <param name="backgroundColor"></param>
    public static void SaveColorPair(string stateKey, Color textColor, Color backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        string serializedValue = SerializeColor(textColor) + ";" + SerializeColor(backgroundColor);
        EditorPrefs.SetString(stateKey, serializedValue);
    }

    /// <summary>
    /// Tries to load one persisted text/background color pair from EditorPrefs.
    /// Takes as arguments a key to identify the state and outputs the loaded colors when successful.
    /// </summary>
    /// <param name="stateKey"></param>
    /// <param name="textColor"></param>
    /// <param name="backgroundColor"></param>
    /// <returns> Returns true when a valid color pair is found and parsed. </returns>
    public static bool TryLoadColorPair(string stateKey, out Color textColor, out Color backgroundColor)
    {
        textColor = Color.white;
        backgroundColor = Color.clear;

        if (string.IsNullOrWhiteSpace(stateKey))
            return false;

        string rawValue = EditorPrefs.GetString(stateKey, string.Empty);

        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        string[] tokens = rawValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 2)
            return false;

        if (!TryDeserializeColor(tokens[0], out textColor))
            return false;

        if (!TryDeserializeColor(tokens[1], out backgroundColor))
            return false;

        return true;
    }

    /// <summary>
    /// Deletes one persisted EditorPrefs key used by management-tool UI state.
    /// Takes as an argument the state key to remove.
    /// </summary>
    /// <param name="stateKey"></param>
    public static void DeleteState(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        EditorPrefs.DeleteKey(stateKey);
    }
    #endregion

    #region Asset State
    /// <summary>
    /// Saves a reference to an asset in EditorPrefs by storing its asset path.
    /// Takes as arguments a key to identify the state and the asset object reference to save.
    /// </summary>
    /// <param name="stateKey"></param>
    /// <param name="assetObject"></param>
    public static void SaveAssetPath(string stateKey, UnityEngine.Object assetObject)
    {
        // Skip writes for invalid keys.
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        // Remove persisted key when the asset reference is null.
        if (assetObject == null)
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        // Resolve and validate the asset path before persisting it.
        string assetPath = AssetDatabase.GetAssetPath(assetObject);

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        EditorPrefs.SetString(stateKey, assetPath);
    }

    /// <summary>
    /// Loads a reference to a GameObject asset from EditorPrefs by reading its asset path and loading the asset.
    /// Takes as an argument a key to identify the state, and returns the loaded GameObject asset.
    /// </summary>
    /// <param name="stateKey"></param>
    /// <returns> Returns the loaded GameObject asset, or null if loading fails. </returns>
    public static GameObject LoadGameObjectAsset(string stateKey)
    {
        // Return null when key is invalid or missing.
        if (string.IsNullOrWhiteSpace(stateKey))
            return null;

        // Read asset path and load the GameObject reference.
        string assetPath = EditorPrefs.GetString(stateKey, string.Empty);

        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }

    /// <summary>
    /// Loads a typed asset reference from EditorPrefs by reading its persisted asset path. Used by panels
    /// to restore the last-edited preset on reopen so the user lands exactly where they left off.
    /// </summary>
    /// <typeparam name="TAsset">Concrete asset type expected at the persisted path.</typeparam>
    /// <param name="stateKey">EditorPrefs key holding the asset path.</param>
    /// <returns>Returns the loaded asset, or null if the path is missing or invalid.</returns>
    public static TAsset LoadAsset<TAsset>(string stateKey) where TAsset : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return null;

        string assetPath = EditorPrefs.GetString(stateKey, string.Empty);

        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
    }
    #endregion

    #region Scroll State
    /// <summary>
    /// Persists one scroll offset as a Vector2 component pair. Used by management-tool panels so the
    /// detail scroll position survives Apply/Discard refreshes and tool close/reopen cycles.
    /// </summary>
    /// <param name="stateKey">EditorPrefs base key (x/y suffixes are appended internally).</param>
    /// <param name="scrollOffset">Scroll offset to persist.</param>
    public static void SaveScrollOffset(string stateKey, Vector2 scrollOffset)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        EditorPrefs.SetFloat(stateKey + ".x", scrollOffset.x);
        EditorPrefs.SetFloat(stateKey + ".y", scrollOffset.y);
    }

    /// <summary>
    /// Loads a persisted scroll offset. Defaults to Vector2.zero when no value is recorded yet.
    /// </summary>
    /// <param name="stateKey">EditorPrefs base key whose x/y suffixes are read.</param>
    /// <returns>Returns the restored scroll offset.</returns>
    public static Vector2 LoadScrollOffset(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return Vector2.zero;

        float scrollX = EditorPrefs.GetFloat(stateKey + ".x", 0f);
        float scrollY = EditorPrefs.GetFloat(stateKey + ".y", 0f);
        return new Vector2(scrollX, scrollY);
    }

    /// <summary>
    /// Checks whether a persisted scroll offset exists for the provided state key.
    /// </summary>
    /// <param name="stateKey">EditorPrefs base key whose x/y suffixes are checked.</param>
    /// <returns>Returns true when at least one scroll component has persisted state.</returns>
    public static bool HasScrollOffset(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return false;

        return EditorPrefs.HasKey(stateKey + ".x") || EditorPrefs.HasKey(stateKey + ".y");
    }
    #endregion

    #region Serialization Helpers
    /// <summary>
    /// Serializes one color to an invariant comma-separated RGBA string.
    /// Takes as an argument the color to serialize and returns the encoded string.
    /// </summary>
    /// <param name="color"></param>
    /// <returns> Returns the serialized RGBA string. </returns>
    private static string SerializeColor(Color color)
    {
        return string.Format(CultureInfo.InvariantCulture,
                             "{0},{1},{2},{3}",
                             color.r,
                             color.g,
                             color.b,
                             color.a);
    }

    /// <summary>
    /// Tries to deserialize one invariant comma-separated RGBA color string.
    /// Takes as an argument the serialized value and outputs the decoded color when successful.
    /// </summary>
    /// <param name="serializedColor"></param>
    /// <param name="color"></param>
    /// <returns> Returns true when the color string is valid. </returns>
    private static bool TryDeserializeColor(string serializedColor, out Color color)
    {
        color = Color.clear;

        if (string.IsNullOrWhiteSpace(serializedColor))
            return false;

        string[] tokens = serializedColor.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 4)
            return false;

        if (!float.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float red))
            return false;

        if (!float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float green))
            return false;

        if (!float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float blue))
            return false;

        if (!float.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha))
            return false;

        color = new Color(red, green, blue, alpha);
        return true;
    }
    #endregion

    #endregion
}
