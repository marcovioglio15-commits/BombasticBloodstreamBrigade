using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Extracts readable one-based list identifiers and stable keys from concrete SerializedProperty paths.
/// </summary>
internal static class ExcelDataListIdentityUtility
{
    #region Constants
    private const string UnityListToken = ".Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds concrete list indices, stable element keys and a readable one-based display path.
    /// </summary>
    /// <param name="serializedObject">Serialized owner used to inspect concrete list elements.</param>
    /// <param name="serializedPath">Concrete Unity property path.</param>
    /// <param name="stableKeyCache">Per-asset cache keyed by concrete list element path.</param>
    /// <param name="concreteIndices">Zero-based indices in nesting order.</param>
    /// <param name="stableKeys">Stable keys in nesting order, with empty fallback entries.</param>
    /// <returns>Readable path whose list elements use `_1`, `_2` and equivalent one-based identifiers.</returns>
    public static string BuildReadablePath(SerializedObject serializedObject,
                                           string serializedPath,
                                           IDictionary<string, string> stableKeyCache,
                                           out List<int> concreteIndices,
                                           out List<string> stableKeys)
    {
        concreteIndices = new List<int>();
        stableKeys = new List<string>();

        if (string.IsNullOrWhiteSpace(serializedPath))
            return string.Empty;

        StringBuilder readablePath = new StringBuilder(serializedPath.Length);
        int copyStartIndex = 0;
        int tokenIndex = serializedPath.IndexOf(UnityListToken, StringComparison.Ordinal);

        // Replace each Unity list segment while preserving every non-list path fragment exactly.
        while (tokenIndex >= 0)
        {
            int numberStartIndex = tokenIndex + UnityListToken.Length;
            int numberEndIndex = serializedPath.IndexOf(']', numberStartIndex);

            if (numberEndIndex < 0)
                break;

            int concreteIndex;

            if (!int.TryParse(serializedPath.Substring(numberStartIndex, numberEndIndex - numberStartIndex),
                              NumberStyles.Integer,
                              CultureInfo.InvariantCulture,
                              out concreteIndex))
                break;

            readablePath.Append(serializedPath, copyStartIndex, tokenIndex - copyStartIndex);
            readablePath.Append('_');
            readablePath.Append((concreteIndex + 1).ToString(CultureInfo.InvariantCulture));
            concreteIndices.Add(concreteIndex);
            string elementPath = serializedPath.Substring(0, numberEndIndex + 1);
            string stableKey;

            if (stableKeyCache == null || !stableKeyCache.TryGetValue(elementPath, out stableKey))
            {
                stableKey = ResolveStableElementKey(serializedObject, elementPath);

                if (stableKeyCache != null)
                    stableKeyCache[elementPath] = stableKey;
            }

            stableKeys.Add(stableKey);
            copyStartIndex = numberEndIndex + 1;
            tokenIndex = serializedPath.IndexOf(UnityListToken, copyStartIndex, StringComparison.Ordinal);
        }

        readablePath.Append(serializedPath, copyStartIndex, serializedPath.Length - copyStartIndex);
        return readablePath.ToString().Replace(".Array.size", "_Count", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds searchable text for non-empty stable list keys.
    /// </summary>
    /// <param name="stableKeys">Nested stable keys discovered for one field.</param>
    /// <returns>Space-separated stable key text.</returns>
    public static string BuildStableKeySearchText(IReadOnlyList<string> stableKeys)
    {
        if (stableKeys == null || stableKeys.Count <= 0)
            return string.Empty;

        StringBuilder text = new StringBuilder();

        for (int keyIndex = 0; keyIndex < stableKeys.Count; keyIndex++)
        {
            if (string.IsNullOrWhiteSpace(stableKeys[keyIndex]))
                continue;

            if (text.Length > 0)
                text.Append(' ');

            text.Append(stableKeys[keyIndex]);
        }

        return text.ToString();
    }
    #endregion

    #region Stable Key Resolution
    /// <summary>
    /// Resolves the highest-priority direct identifier child of one concrete list element.
    /// </summary>
    /// <param name="serializedObject">Serialized owner containing the list element.</param>
    /// <param name="elementPath">Concrete path ending at one list element.</param>
    /// <returns>Readable stable key, or an empty string when no identifier is available.</returns>
    private static string ResolveStableElementKey(SerializedObject serializedObject, string elementPath)
    {
        if (serializedObject == null || string.IsNullOrWhiteSpace(elementPath))
            return string.Empty;

        SerializedProperty element = serializedObject.FindProperty(elementPath);

        if (element == null || element.propertyType != SerializedPropertyType.Generic)
            return string.Empty;

        SerializedProperty iterator = element.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        string bestKey = string.Empty;
        int bestPriority = int.MaxValue;
        bool enterChildren = true;

        // Inspect direct children only so nested values cannot accidentally identify the parent list item.
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
        {
            enterChildren = false;

            if (iterator.depth != element.depth + 1)
                continue;

            int priority = ResolveIdentifierPriority(iterator.name);

            if (priority >= bestPriority)
                continue;

            string value = ReadIdentifierValue(iterator);

            if (string.IsNullOrWhiteSpace(value))
                continue;

            bestPriority = priority;
            bestKey = iterator.displayName + "=" + value;
        }

        return bestKey;
    }

    /// <summary>
    /// Assigns deterministic priority to common stable identifier field names.
    /// </summary>
    /// <param name="propertyName">Serialized child field name.</param>
    /// <returns>Lower values for stronger stable identifier candidates.</returns>
    private static int ResolveIdentifierPriority(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return int.MaxValue;

        string normalizedName = propertyName.Replace("_", string.Empty, StringComparison.Ordinal)
                                            .ToLowerInvariant();

        if (normalizedName.StartsWith("m", StringComparison.Ordinal) && normalizedName.Length > 1)
            normalizedName = normalizedName.Substring(1);

        switch (normalizedName)
        {
            case "stableid":
            case "guid":
                return 0;
            case "presetid":
            case "brushid":
            case "id":
            case "key":
            case "identifier":
                return 1;
            case "name":
            case "displayname":
            case "label":
                return 2;
            default:
                return int.MaxValue;
        }
    }

    /// <summary>
    /// Reads one supported identifier value without runtime reflection.
    /// </summary>
    /// <param name="property">Direct identifier candidate.</param>
    /// <returns>Readable invariant value, or an empty string for unsupported types.</returns>
    private static string ReadIdentifierValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.Integer:
                return property.longValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Enum:
                return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                    ? property.enumDisplayNames[property.enumValueIndex]
                    : property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == null ? string.Empty : property.objectReferenceValue.name;
            default:
                return string.Empty;
        }
    }
    #endregion

    #endregion
}
